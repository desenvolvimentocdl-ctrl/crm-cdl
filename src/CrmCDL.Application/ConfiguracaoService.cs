using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

public record EtapaEdicao(int? Id, string Nome, string Cor);

/// <summary>Cadastro de funis, etapas e campos personalizados.</summary>
public class ConfiguracaoService(IAppDbContext db)
{
    public async Task<Funil> CriarFunilAsync(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new RegraNegocioException("Informe o nome do funil.");
        var ordem = (await db.Funis.MaxAsync(f => (int?)f.Ordem) ?? 0) + 1;
        var funil = new Funil
        {
            Nome = nome.Trim(), Ordem = ordem, Principal = !await db.Funis.AnyAsync(),
            Etapas =
            {
                new Etapa { Nome = "Leads de entrada", Cor = "#c1c1c1", Ordem = 0, Tipo = TipoEtapa.Entrada },
                new Etapa { Nome = "Contato inicial", Cor = "#99ccff", Ordem = 1 },
                new Etapa { Nome = "Negociação", Cor = "#ffcc66", Ordem = 2 },
                new Etapa { Nome = "Venda ganha", Cor = "#ccff66", Ordem = 98, Tipo = TipoEtapa.Ganho },
                new Etapa { Nome = "Venda perdida", Cor = "#d5d8db", Ordem = 99, Tipo = TipoEtapa.Perdido },
            }
        };
        db.Funis.Add(funil);
        await db.SaveChangesAsync();
        return funil;
    }

    /// <summary>
    /// Salva nome e etapas intermediárias de um funil na ordem recebida.
    /// As etapas de entrada, ganho e perdido existem sempre e só podem ser renomeadas.
    /// Etapa removida que ainda tem leads é recusada, para nenhum lead sumir do quadro.
    /// </summary>
    public async Task SalvarFunilAsync(int funilId, string nome, bool principal, List<EtapaEdicao> etapas, string? nomeGanho, string? nomePerdido)
    {
        var funil = await db.Funis.Include(f => f.Etapas).FirstOrDefaultAsync(f => f.Id == funilId)
                    ?? throw new RegraNegocioException("Funil não encontrado.");
        if (string.IsNullOrWhiteSpace(nome)) throw new RegraNegocioException("Informe o nome do funil.");
        if (etapas.Any(e => string.IsNullOrWhiteSpace(e.Nome))) throw new RegraNegocioException("Toda etapa precisa de nome.");
        if (etapas.Count == 0) throw new RegraNegocioException("O funil precisa de pelo menos uma etapa de trabalho.");

        funil.Nome = nome.Trim();
        if (principal && !funil.Principal)
        {
            await db.Funis.Where(f => f.Principal).ForEachAsync(f => f.Principal = false);
            funil.Principal = true;
        }

        var normais = funil.Etapas.Where(e => e.Tipo == TipoEtapa.Normal).ToList();
        var mantidas = etapas.Where(e => e.Id is not null).Select(e => e.Id!.Value).ToHashSet();
        foreach (var removida in normais.Where(e => !mantidas.Contains(e.Id)))
        {
            if (await db.Leads.AnyAsync(l => l.EtapaId == removida.Id))
                throw new RegraNegocioException($"A etapa \"{removida.Nome}\" ainda tem leads. Mova-os antes de excluir.");
            db.Etapas.Remove(removida);
        }

        var ordem = 1;
        foreach (var e in etapas)
        {
            var etapa = e.Id is int id ? normais.FirstOrDefault(x => x.Id == id) : null;
            if (etapa is null)
            {
                etapa = new Etapa { FunilId = funil.Id, Tipo = TipoEtapa.Normal };
                db.Etapas.Add(etapa);
            }
            etapa.Nome = e.Nome.Trim();
            etapa.Cor = CorValida(e.Cor);
            etapa.Ordem = ordem++;
        }
        var ganho = funil.Etapas.FirstOrDefault(e => e.Tipo == TipoEtapa.Ganho);
        if (ganho is not null && !string.IsNullOrWhiteSpace(nomeGanho)) ganho.Nome = nomeGanho.Trim();
        var perdido = funil.Etapas.FirstOrDefault(e => e.Tipo == TipoEtapa.Perdido);
        if (perdido is not null && !string.IsNullOrWhiteSpace(nomePerdido)) perdido.Nome = nomePerdido.Trim();
        await db.SaveChangesAsync();
    }

    public async Task ExcluirFunilAsync(int funilId)
    {
        var funil = await db.Funis.Include(f => f.Etapas).FirstOrDefaultAsync(f => f.Id == funilId)
                    ?? throw new RegraNegocioException("Funil não encontrado.");
        if (await db.Leads.AnyAsync(l => l.FunilId == funilId))
            throw new RegraNegocioException("Este funil tem leads. Mova-os para outro funil antes de excluir.");
        db.Funis.Remove(funil);
        await db.SaveChangesAsync();
    }

    public static string CorValida(string? cor)
        => cor is not null && System.Text.RegularExpressions.Regex.IsMatch(cor, "^#[0-9a-fA-F]{6}$") ? cor.ToLowerInvariant() : "#99ccff";

    // ---------- valores dos campos personalizados ----------

    public async Task<Dictionary<int, string>> ValoresAsync(EntidadeCampo entidade, int registroId)
        => await db.ValoresCampo.Where(v => v.Entidade == entidade && v.RegistroId == registroId)
            .ToDictionaryAsync(v => v.CampoId, v => v.Valor);

    public async Task SalvarValoresAsync(EntidadeCampo entidade, int registroId, Dictionary<int, string?> valores)
    {
        var campos = await db.Campos.Where(c => c.Entidade == entidade && c.Ativo).Select(c => c.Id).ToListAsync();
        var atuais = await db.ValoresCampo.Where(v => v.Entidade == entidade && v.RegistroId == registroId).ToListAsync();
        foreach (var (campoId, bruto) in valores)
        {
            if (!campos.Contains(campoId)) continue;
            var valor = bruto?.Trim() ?? "";
            var atual = atuais.FirstOrDefault(v => v.CampoId == campoId);
            if (valor == "")
            {
                if (atual is not null) db.ValoresCampo.Remove(atual);
            }
            else if (atual is null) db.ValoresCampo.Add(new ValorCampo { CampoId = campoId, Entidade = entidade, RegistroId = registroId, Valor = valor });
            else atual.Valor = valor;
        }
        await db.SaveChangesAsync();
    }
}
