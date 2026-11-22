using System.Security.Cryptography;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

/// <summary>
/// Recebe envios dos formulários do site e cria o lead no funil configurado.
/// O responsável é fixo ou sai do rodízio entre os participantes. Contato já existente (mesmo telefone ou e-mail) é reaproveitado.
/// </summary>
public class CapturaService(IAppDbContext db)
{
    /// <summary>Nomes aceitos para os campos principais (o formulário do site pode usar qualquer um deles).</summary>
    static readonly string[] CamposNome = { "nome", "name", "seu_nome", "contato" };
    static readonly string[] CamposEmail = { "email", "e-mail", "e_mail" };
    static readonly string[] CamposFone = { "telefone", "celular", "whatsapp", "fone", "phone", "tel" };
    static readonly string[] CamposEmpresa = { "empresa", "company", "razao_social", "loja" };
    static readonly string[] CamposMensagem = { "mensagem", "message", "obs", "observacao", "interesse" };
    const int TamanhoMaximoValor = 2000;

    public static string NovoToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public async Task<Lead> ReceberAsync(string token, IReadOnlyDictionary<string, string> dados, DateTime? agoraUtc = null)
    {
        var form = await db.Formularios.FirstOrDefaultAsync(f => f.Token == token && f.Ativo)
                   ?? throw new RegraNegocioException("Formulário não encontrado ou desativado.");
        var d = dados.Where(kv => !kv.Key.StartsWith('_')) // campos começando com _ são de controle (ex.: _redirect)
            .ToDictionary(kv => kv.Key.Trim().ToLowerInvariant(), kv => Cortar(kv.Value?.Trim() ?? ""));
        string? Pegar(string[] nomes) => nomes.Select(n => d.TryGetValue(n, out var v) && v != "" ? v : null).FirstOrDefault(v => v is not null);

        var nome = Pegar(CamposNome);
        var email = Pegar(CamposEmail);
        var fone = Pegar(CamposFone);
        if (nome is null && email is null && fone is null) throw new RegraNegocioException("Envie pelo menos nome, e-mail ou telefone.");

        var responsavel = await EscolherResponsavelAsync(form);
        var sistema = new UsuarioAtual(0, "Formulário do site", Perfil.Administrador);

        var etapas = await db.Etapas.Where(e => e.FunilId == form.FunilId).OrderBy(e => e.Ordem).ToListAsync();
        var etapa = etapas.FirstOrDefault(e => e.Id == form.EtapaId) ?? etapas.FirstOrDefault(e => e.Tipo == TipoEtapa.Entrada) ?? etapas.FirstOrDefault()
                    ?? throw new RegraNegocioException("O funil do formulário não tem etapas.");

        var contato = await Consultas.Duplicados(db.Contatos, fone, email).OrderBy(c => c.Id).FirstOrDefaultAsync();
        var reaproveitado = contato is not null;
        contato ??= new Contato { Nome = nome ?? email ?? fone!, Celular = fone, Email = email, ResponsavelId = responsavel };
        if (contato.Id != 0)
        {
            contato.Celular ??= fone;
            contato.Email ??= email;
        }

        var empresaNome = Pegar(CamposEmpresa);
        var lead = new Lead
        {
            Titulo = empresaNome is null ? nome ?? email ?? fone! : $"{nome ?? email ?? fone} – {empresaNome}",
            FunilId = form.FunilId, EtapaId = etapa.Id, ResponsavelId = responsavel, Origem = "Formulário: " + form.Nome,
            Posicao = (await db.Leads.Where(l => l.EtapaId == etapa.Id).MinAsync(l => (double?)l.Posicao) ?? 0) - 1
        };
        if (lead.Titulo.Length > 250) lead.Titulo = lead.Titulo[..250];
        if (empresaNome is not null)
            lead.Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nome == empresaNome) ?? new Empresa { Nome = empresaNome, ResponsavelId = responsavel };
        lead.Contatos.Add(new LeadContato { Contato = contato, Principal = true });

        var linhas = d.Where(kv => kv.Value != "").Select(kv => $"{kv.Key}: {kv.Value}");
        lead.Atividades.Add(new Atividade
        {
            Tipo = TipoAtividade.Criacao,
            Texto = $"Lead recebido pelo formulário \"{form.Nome}\"" + (reaproveitado ? " (contato já existia no CRM)" : "") + "\n" + string.Join("\n", linhas)
        });
        db.Leads.Add(lead);
        NotificacaoService.Avisar(db, responsavel, sistema, TipoNotificacao.LeadAtribuido, $"Novo lead do site: \"{lead.Titulo}\"", lead);

        form.Recebidos++;
        form.UltimoRecebimento = agoraUtc ?? DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Campos personalizados com o mesmo nome do campo enviado (ex.: utm_source, Url Landing Page) são preenchidos.
        var campos = await db.Campos.Where(c => c.Entidade == EntidadeCampo.Lead && c.Ativo).Include(c => c.Opcoes).ToListAsync();
        foreach (var c in campos)
        {
            if (!d.TryGetValue(c.Nome.Trim().ToLowerInvariant(), out var v) || v == "") continue;
            var valor = c.Tipo switch
            {
                TipoCampo.Lista => c.Opcoes.FirstOrDefault(o => o.Valor.Equals(v, StringComparison.OrdinalIgnoreCase))?.Id.ToString(),
                TipoCampo.MultiplaEscolha => string.Join(",", v.Split(',').Select(x => c.Opcoes.FirstOrDefault(o => o.Valor.Equals(x.Trim(), StringComparison.OrdinalIgnoreCase))?.Id).Where(x => x is not null)),
                TipoCampo.Checkbox => v is "1" or "true" or "sim" or "on" ? "1" : null,
                _ => v
            };
            if (!string.IsNullOrEmpty(valor)) db.ValoresCampo.Add(new ValorCampo { CampoId = c.Id, Entidade = EntidadeCampo.Lead, RegistroId = lead.Id, Valor = valor });
        }
        await Automacoes.DispararAsync(db, lead, etapa.Id, sistema with { Id = responsavel ?? 0 }, agoraUtc);
        await db.SaveChangesAsync();
        return lead;
    }

    /// <summary>Rodízio: cada envio vai para o próximo participante ativo da lista.</summary>
    async Task<int?> EscolherResponsavelAsync(FormularioCaptura f)
    {
        if (f.Distribuicao == Distribuicao.Fixo)
            return f.ResponsavelFixoId is int r && await db.Usuarios.AnyAsync(u => u.Id == r && u.Ativo) ? r : null;
        var ids = f.Participantes;
        var ativos = await db.Usuarios.Where(u => ids.Contains(u.Id) && u.Ativo).Select(u => u.Id).ToListAsync();
        var ordem = ids.Where(ativos.Contains).ToList();
        if (ordem.Count == 0) return f.ResponsavelFixoId;
        var escolhido = ordem[((f.ProximoDoRodizio % ordem.Count) + ordem.Count) % ordem.Count];
        f.ProximoDoRodizio = (f.ProximoDoRodizio + 1) % ordem.Count;
        return escolhido;
    }

    static string Cortar(string s) => s.Length > TamanhoMaximoValor ? s[..TamanhoMaximoValor] : s;
}
