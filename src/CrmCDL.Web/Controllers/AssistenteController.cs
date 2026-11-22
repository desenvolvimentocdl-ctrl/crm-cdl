using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrmCDL.Web.Controllers;

/// <summary>Chat com o assistente de IA (painel lateral em todas as telas).</summary>
public class AssistenteController(Assistente assistente) : BaseController
{
    const int TamanhoMaximoPergunta = 4000;

    public record Pedido(List<FalaAssistente>? Historico, string Pergunta);

    [HttpPost("/assistente"), EnableRateLimiting("assistente")]
    public async Task<IActionResult> Perguntar([FromBody] Pedido pedido, CancellationToken ct)
    {
        var pergunta = (pedido.Pergunta ?? "").Trim();
        if (pergunta.Length == 0) return BadRequest(new { erro = "Escreva a pergunta." });
        if (pergunta.Length > TamanhoMaximoPergunta) return BadRequest(new { erro = "Pergunta longa demais." });
        var historico = (pedido.Historico ?? new())
            .Where(f => f.Papel is "user" or "assistant" && !string.IsNullOrWhiteSpace(f.Texto))
            .Select(f => f with { Texto = f.Texto.Length > TamanhoMaximoPergunta * 2 ? f.Texto[..(TamanhoMaximoPergunta * 2)] : f.Texto })
            .ToList();
        var resposta = await assistente.ResponderAsync(historico, pergunta, Eu, ct);
        return Ok(new { resposta });
    }

    [HttpGet("/assistente/status")]
    public IActionResult Status() => Ok(new { configurado = assistente.Configurado });
}
