using System.Globalization;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

/// <summary>Horário de Brasília para regras que dependem do dia (prazos de tarefas automáticas, metas do mês).</summary>
public static class Fuso
{
    public static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia);
    public static DateTime ParaUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Brasilia);
}

/// <summary>Tarefas criadas sozinhas quando um lead entra numa etapa (ex.: "Ligar em 1 dia" ao entrar em "Interesse").</summary>
public static class Automacoes
{
    /// <summary>Adiciona (sem salvar) as tarefas das automações ativas da etapa. Devolve quantas criou.</summary>
    public static async Task<int> DispararAsync(IAppDbContext db, Lead lead, int etapaId, UsuarioAtual autor, DateTime? agoraUtc = null)
    {
        var regras = await db.Automacoes.Where(a => a.EtapaId == etapaId && a.Ativo).ToListAsync();
        if (regras.Count == 0) return 0;
        var hoje = Fuso.Local(agoraUtc ?? DateTime.UtcNow).Date;
        foreach (var r in regras)
        {
            var prazoLocal = hoje.AddDays(Math.Max(0, r.PrazoDias)).AddHours(Math.Clamp(r.HoraPrazo, 0, 23));
            var tarefa = new Tarefa
            {
                Lead = lead.Id == 0 ? lead : null, LeadId = lead.Id == 0 ? null : lead.Id,
                Tipo = r.TipoTarefa, Texto = r.Texto, Prazo = Fuso.ParaUtc(prazoLocal),
                ResponsavelId = lead.ResponsavelId, CriadoPorId = autor.Id
            };
            db.Tarefas.Add(tarefa);
            var atividade = new Atividade { Tipo = TipoAtividade.TarefaCriada, UsuarioId = autor.Id, Texto = $"Tarefa automática para {prazoLocal:dd/MM HH:mm}: {r.Texto}" };
            if (lead.Id == 0) atividade.Lead = lead; else atividade.LeadId = lead.Id;
            db.Atividades.Add(atividade);
            NotificacaoService.Avisar(db, lead.ResponsavelId, autor, TipoNotificacao.TarefaAtribuida, $"Nova tarefa automática ({prazoLocal:dd/MM HH:mm}): {r.Texto}", lead.Id == 0 ? lead : null, lead.Id == 0 ? null : lead.Id);
        }
        return regras.Count;
    }
}
