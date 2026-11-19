using CrmCDL.Application;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CrmCDL.Web.Controllers;

/// <summary>Sino do menu: lista de avisos, contagem para atualizar o número sem recarregar e marcação de lidas.</summary>
public class NotificacoesController(NotificacaoService notificacoes) : BaseController
{
    [HttpGet("/notificacoes")]
    public async Task<IActionResult> Lista()
        => View(await notificacoes.ListarAsync(Eu.Id));

    [HttpGet("/notificacoes/contagem")]
    public async Task<IActionResult> Contagem()
        => Json(new { naoLidas = await notificacoes.NaoLidasAsync(Eu.Id) });

    /// <summary>Abre o lead (ou a lista de tarefas) do aviso e o marca como lido.</summary>
    [HttpGet("/notificacoes/{id:int}")]
    public async Task<IActionResult> Abrir(int id)
    {
        var n = await notificacoes.AbrirAsync(id, Eu.Id);
        return Redirect(n?.Link ?? "/notificacoes");
    }

    [HttpPost("/notificacoes/lidas")]
    public async Task<IActionResult> MarcarTodas()
    {
        await notificacoes.MarcarTodasComoLidasAsync(Eu.Id);
        return VoltarCom("Todas as notificações foram marcadas como lidas.", "/notificacoes");
    }
}
