using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

public record NovoLead(string Titulo, decimal Valor, int FunilId, int? EtapaId, int? ResponsavelId,
    string? ContatoNome, string? ContatoTelefone, string? ContatoEmail, string? EmpresaNome);

public class LeadService(IAppDbContext db)
{
    /// <summary>Vendedor só enxerga os leads em que é responsável; gestor e administrador enxergam todos.</summary>
    public IQueryable<Lead> Visiveis(UsuarioAtual u)
        => u.VeTudo ? db.Leads : db.Leads.Where(l => l.ResponsavelId == u.Id);

    /// <summary>Leads do quadro (e da exportação) com busca, responsável e fechados, respeitando o que o usuário pode ver.</summary>
    public IQueryable<Lead> Filtrar(FiltroLeads filtro, UsuarioAtual u, DateTime agoraUtc)
        => Consultas.FiltrarLeads(Visiveis(u), filtro, agoraUtc);

    public async Task<Lead> ObterAsync(int id, UsuarioAtual u)
        => await Visiveis(u).FirstOrDefaultAsync(l => l.Id == id)
           ?? throw new RegraNegocioException("Lead não encontrado ou sem permissão de acesso.");

    public async Task<Lead> CriarAsync(NovoLead n, UsuarioAtual u)
    {
        if (string.IsNullOrWhiteSpace(n.Titulo)) throw new RegraNegocioException("Informe o nome do lead.");
        var etapas = await db.Etapas.Where(e => e.FunilId == n.FunilId).OrderBy(e => e.Ordem).ToListAsync();
        if (etapas.Count == 0) throw new RegraNegocioException("Funil sem etapas.");
        var etapa = n.EtapaId is int eid ? etapas.FirstOrDefault(e => e.Id == eid) : null;
        etapa ??= etapas.FirstOrDefault(e => e.Tipo == TipoEtapa.Normal) ?? etapas[0];

        // Vendedor sempre fica como responsável pelo que cria, para não perder o lead de vista.
        int? responsavel = u.VeTudo ? (n.ResponsavelId ?? u.Id) : u.Id;

        var menor = await db.Leads.Where(l => l.EtapaId == etapa.Id).MinAsync(l => (double?)l.Posicao) ?? 0;
        var lead = new Lead
        {
            Titulo = n.Titulo.Trim(), Valor = n.Valor, FunilId = n.FunilId, EtapaId = etapa.Id,
            ResponsavelId = responsavel, Posicao = menor - 1
        };

        if (!string.IsNullOrWhiteSpace(n.EmpresaNome))
        {
            var nome = n.EmpresaNome.Trim();
            lead.Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nome == nome)
                           ?? new Empresa { Nome = nome, ResponsavelId = responsavel };
        }
        if (!string.IsNullOrWhiteSpace(n.ContatoNome))
        {
            var contato = new Contato
            {
                Nome = n.ContatoNome.Trim(), Celular = n.ContatoTelefone?.Trim(), Email = n.ContatoEmail?.Trim(),
                Empresa = lead.Empresa, ResponsavelId = responsavel
            };
            lead.Contatos.Add(new LeadContato { Contato = contato, Principal = true });
        }
        lead.Atividades.Add(new Atividade { Tipo = TipoAtividade.Criacao, UsuarioId = u.Id, Texto = $"Lead criado na etapa \"{etapa.Nome}\"" });
        db.Leads.Add(lead);
        NotificacaoService.Avisar(db, responsavel, u, TipoNotificacao.LeadAtribuido, $"{u.Nome} criou o lead \"{lead.Titulo}\" para você", lead);
        await db.SaveChangesAsync();
        return lead;
    }

    /// <summary>Move o cartão (arrastar e soltar ou troca de etapa na página do lead).</summary>
    public async Task MoverAsync(int leadId, int etapaId, double? posicao, int? motivoPerdaId, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        var etapa = await db.Etapas.FirstOrDefaultAsync(e => e.Id == etapaId && e.FunilId == lead.FunilId)
                    ?? throw new RegraNegocioException("Etapa inválida para este funil.");

        if (lead.EtapaId != etapa.Id)
        {
            var anterior = await db.Etapas.Where(e => e.Id == lead.EtapaId).Select(e => e.Nome).FirstAsync();
            lead.EtapaId = etapa.Id;
            lead.FechadoEm = etapa.Fechada ? DateTime.UtcNow : null;
            lead.MotivoPerdaId = etapa.Tipo == TipoEtapa.Perdido ? motivoPerdaId : null;
            var texto = $"Etapa: \"{anterior}\" → \"{etapa.Nome}\"";
            if (etapa.Tipo == TipoEtapa.Perdido && motivoPerdaId is int m)
                texto += " · motivo: " + await db.MotivosPerda.Where(x => x.Id == m).Select(x => x.Nome).FirstOrDefaultAsync();
            db.Atividades.Add(new Atividade { LeadId = lead.Id, Tipo = TipoAtividade.MudancaEtapa, UsuarioId = u.Id, Texto = texto });
        }
        if (posicao is double p) lead.Posicao = p;
        lead.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task AtualizarAsync(int leadId, string titulo, decimal valor, int? responsavelId, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        if (string.IsNullOrWhiteSpace(titulo)) throw new RegraNegocioException("Informe o nome do lead.");
        lead.Titulo = titulo.Trim();
        lead.Valor = valor;
        if (u.VeTudo && responsavelId != lead.ResponsavelId)
        {
            var nome = responsavelId is int r ? await db.Usuarios.Where(x => x.Id == r).Select(x => x.Nome).FirstOrDefaultAsync() : "ninguém";
            lead.ResponsavelId = responsavelId;
            db.Atividades.Add(new Atividade { LeadId = lead.Id, Tipo = TipoAtividade.MudancaResponsavel, UsuarioId = u.Id, Texto = $"Responsável alterado para {nome}" });
            NotificacaoService.Avisar(db, responsavelId, u, TipoNotificacao.LeadAtribuido, $"{u.Nome} passou o lead \"{lead.Titulo}\" para você", lead);
        }
        lead.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DefinirTagsAsync(int leadId, IEnumerable<string> nomes, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        var limpos = nomes.Select(n => n.Trim()).Where(n => n != "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existentes = await db.Tags.Where(t => limpos.Contains(t.Nome)).ToListAsync();
        foreach (var n in limpos.Where(n => !existentes.Any(t => t.Nome.Equals(n, StringComparison.OrdinalIgnoreCase))))
        {
            var t = new Tag { Nome = n };
            db.Tags.Add(t);
            existentes.Add(t);
        }
        db.LeadTags.RemoveRange(db.LeadTags.Where(x => x.LeadId == lead.Id));
        foreach (var t in existentes) db.LeadTags.Add(new LeadTag { LeadId = lead.Id, Tag = t });
        await db.SaveChangesAsync();
    }

    public async Task AdicionarNotaAsync(int leadId, string texto, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        if (string.IsNullOrWhiteSpace(texto)) throw new RegraNegocioException("Escreva a nota.");
        db.Atividades.Add(new Atividade { LeadId = lead.Id, Tipo = TipoAtividade.Nota, UsuarioId = u.Id, Texto = texto.Trim() });
        lead.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <param name="prazo">Em UTC.</param>
    /// <param name="prazoLocal">Prazo no horário de Brasília, só para o texto do histórico.</param>
    public async Task CriarTarefaAsync(int? leadId, TipoTarefa tipo, string texto, DateTime prazo, int? responsavelId, UsuarioAtual u, DateTime? prazoLocal = null)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new RegraNegocioException("Descreva a tarefa.");
        if (leadId is int id) await ObterAsync(id, u);
        var resp = u.VeTudo ? (responsavelId ?? u.Id) : u.Id;
        db.Tarefas.Add(new Tarefa { LeadId = leadId, Tipo = tipo, Texto = texto.Trim(), Prazo = prazo, ResponsavelId = resp, CriadoPorId = u.Id });
        NotificacaoService.Avisar(db, resp, u, TipoNotificacao.TarefaAtribuida, $"{u.Nome} criou uma tarefa para você ({(prazoLocal ?? prazo):dd/MM HH:mm}): {texto.Trim()}", leadId: leadId);
        if (leadId is int l)
            db.Atividades.Add(new Atividade { LeadId = l, Tipo = TipoAtividade.TarefaCriada, UsuarioId = u.Id, Texto = $"Tarefa para {(prazoLocal ?? prazo):dd/MM HH:mm}: {texto.Trim()}" });
        await db.SaveChangesAsync();
    }

    public async Task ConcluirTarefaAsync(int tarefaId, string? resultado, UsuarioAtual u)
    {
        var t = await db.Tarefas.FirstOrDefaultAsync(x => x.Id == tarefaId && (u.VeTudo || x.ResponsavelId == u.Id))
                ?? throw new RegraNegocioException("Tarefa não encontrada.");
        if (t.Concluida) return;
        t.Concluida = true;
        t.ConcluidaEm = DateTime.UtcNow;
        t.Resultado = string.IsNullOrWhiteSpace(resultado) ? null : resultado.Trim();
        if (t.LeadId is int l)
            db.Atividades.Add(new Atividade { LeadId = l, Tipo = TipoAtividade.TarefaConcluida, UsuarioId = u.Id, Texto = $"Tarefa concluída: {t.Texto}" + (t.Resultado is null ? "" : $" — {t.Resultado}") });
        await db.SaveChangesAsync();
    }

    public async Task VincularContatoAsync(int leadId, int contatoId, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        if (await db.LeadContatos.AnyAsync(x => x.LeadId == lead.Id && x.ContatoId == contatoId)) return;
        var principal = !await db.LeadContatos.AnyAsync(x => x.LeadId == lead.Id);
        db.LeadContatos.Add(new LeadContato { LeadId = lead.Id, ContatoId = contatoId, Principal = principal });
        await db.SaveChangesAsync();
    }

    public async Task DesvincularContatoAsync(int leadId, int contatoId, UsuarioAtual u)
    {
        var lead = await ObterAsync(leadId, u);
        db.LeadContatos.RemoveRange(db.LeadContatos.Where(x => x.LeadId == lead.Id && x.ContatoId == contatoId));
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(int leadId, UsuarioAtual u)
    {
        if (!u.VeTudo) throw new RegraNegocioException("Só gestores podem excluir leads.");
        var lead = await ObterAsync(leadId, u);
        db.ValoresCampo.RemoveRange(db.ValoresCampo.Where(v => v.Entidade == EntidadeCampo.Lead && v.RegistroId == lead.Id));
        db.Leads.Remove(lead);
        await db.SaveChangesAsync();
    }
}
