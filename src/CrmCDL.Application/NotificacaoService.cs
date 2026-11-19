using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

/// <summary>Avisos do sino: lead ou tarefa recebidos de outra pessoa e lembretes de tarefas para hoje ou atrasadas.</summary>
public class NotificacaoService(IAppDbContext db)
{
    const int TamanhoTexto = 500;
    /// <summary>Tarefas atrasadas há mais tempo que isso não geram lembrete (evita uma enxurrada na primeira execução).</summary>
    public static readonly TimeSpan JanelaAtrasadas = TimeSpan.FromDays(7);

    /// <summary>Adiciona (sem salvar) um aviso para quem recebeu algo de outra pessoa. Quem fez a ação não é avisado.</summary>
    public static void Avisar(IAppDbContext db, int? destinatarioId, UsuarioAtual autor, TipoNotificacao tipo, string texto, Lead? lead = null, int? leadId = null)
    {
        if (destinatarioId is not int d || d == autor.Id) return;
        db.Notificacoes.Add(new Notificacao { UsuarioId = d, Tipo = tipo, Texto = Cortar(texto), Lead = lead, LeadId = lead is null ? leadId : null });
    }

    public Task<int> NaoLidasAsync(int usuarioId)
        => db.Notificacoes.CountAsync(n => n.UsuarioId == usuarioId && !n.Lida);

    public Task<List<Notificacao>> ListarAsync(int usuarioId, int maximo = 100)
        => db.Notificacoes.Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CriadoEm).ThenByDescending(n => n.Id).Take(maximo).ToListAsync();

    /// <summary>Marca como lida e devolve o aviso; null se não existir ou for de outra pessoa.</summary>
    public async Task<Notificacao?> AbrirAsync(int id, int usuarioId)
    {
        var n = await db.Notificacoes.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId);
        if (n is { Lida: false })
        {
            n.Lida = true;
            await db.SaveChangesAsync();
        }
        return n;
    }

    public async Task MarcarTodasComoLidasAsync(int usuarioId)
    {
        var abertas = await db.Notificacoes.Where(n => n.UsuarioId == usuarioId && !n.Lida).ToListAsync();
        abertas.ForEach(n => n.Lida = true);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Cria um lembrete por tarefa em aberto que vence hoje e outro quando ela fica atrasada.
    /// Pode rodar quantas vezes quiser: a chave de cada lembrete impede repetição.
    /// </summary>
    /// <param name="agoraUtc">Momento atual em UTC.</param>
    /// <param name="fimDoDiaUtc">Meia-noite de amanhã (horário de Brasília) em UTC.</param>
    /// <param name="paraLocal">Converte UTC para o horário mostrado no texto.</param>
    /// <returns>Quantos lembretes foram criados.</returns>
    public async Task<int> GerarLembretesAsync(DateTime agoraUtc, DateTime fimDoDiaUtc, Func<DateTime, DateTime> paraLocal)
    {
        var desde = agoraUtc - JanelaAtrasadas;
        var tarefas = await db.Tarefas
            .Where(t => !t.Concluida && t.ResponsavelId != null && t.Prazo < fimDoDiaUtc && t.Prazo >= desde)
            .Select(t => new { t.Id, t.Texto, t.Prazo, t.LeadId, ResponsavelId = t.ResponsavelId!.Value })
            .ToListAsync();
        if (tarefas.Count == 0) return 0;

        var existentes = (await db.Notificacoes
                .Where(n => n.Chave != null && n.Chave.StartsWith("tarefa-") && n.CriadoEm >= desde.AddDays(-1))
                .Select(n => new { n.UsuarioId, n.Chave })
                .ToListAsync())
            .Select(n => (n.UsuarioId, n.Chave!)).ToHashSet();

        var criados = 0;
        foreach (var t in tarefas)
        {
            var atrasada = t.Prazo < agoraUtc;
            var chave = (atrasada ? "tarefa-atrasada:" : "tarefa-hoje:") + t.Id;
            if (!existentes.Add((t.ResponsavelId, chave))) continue;
            var local = paraLocal(t.Prazo);
            db.Notificacoes.Add(new Notificacao
            {
                UsuarioId = t.ResponsavelId,
                Tipo = atrasada ? TipoNotificacao.TarefaAtrasada : TipoNotificacao.TarefaHoje,
                Texto = Cortar(atrasada ? $"Tarefa atrasada desde {local:dd/MM HH:mm}: {t.Texto}" : $"Tarefa para hoje às {local:HH:mm}: {t.Texto}"),
                LeadId = t.LeadId,
                Chave = chave,
                CriadoEm = agoraUtc
            });
            criados++;
        }
        if (criados > 0) await db.SaveChangesAsync();
        return criados;
    }

    static string Cortar(string texto) => texto.Length <= TamanhoTexto ? texto : texto[..(TamanhoTexto - 1)] + "…";
}
