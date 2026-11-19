using CrmCDL.Application;

namespace CrmCDL.Web.Infra;

/// <summary>Roda em segundo plano enquanto o sistema está no ar e cria os lembretes de tarefas para hoje e atrasadas.</summary>
public class LembreteTarefas(IServiceScopeFactory escopos, ILogger<LembreteTarefas> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                using var scope = escopos.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<NotificacaoService>();
                var criados = await svc.GerarLembretesAsync(DateTime.UtcNow, Hora.ParaUtc(Hora.HojeLocal.AddDays(1)), Hora.Local);
                if (criados > 0) log.LogInformation("{Qtd} lembrete(s) de tarefa criados.", criados);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                log.LogError(e, "Falha ao gerar lembretes de tarefas.");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
