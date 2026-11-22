using System.Text.Json;
using CrmCDL.Web.Infra;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmCDL.Tests;

/// <summary>Ferramentas de consulta do assistente de IA: respondem com dados reais e respeitam as permissões.</summary>
public class AssistenteTests : IDisposable
{
    readonly BancoTeste b = new();
    public void Dispose() => b.Dispose();

    Assistente Novo(string chave = "") => new(Options.Create(new AssistenteOptions { ApiKey = chave }), b.Db, b.Leads(), NullLogger<Assistente>.Instance);

    static Dictionary<string, JsonElement> Args(object o) =>
        JsonSerializer.SerializeToElement(o).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    static string Json(object o) => JsonSerializer.Serialize(o);

    [Fact]
    public async Task Buscar_leads_do_vendedor_nao_mostra_os_de_outra_pessoa()
    {
        await b.CriarLeadAsync("Lead da Ana", b.Ana);
        await b.CriarLeadAsync("Lead do Bruno", b.Bruno);

        var r = Json(await Novo().ExecutarAsync("buscar_leads", Args(new { }), b.Ana));

        Assert.Contains("Lead da Ana", r);
        Assert.DoesNotContain("Lead do Bruno", r);
    }

    [Fact]
    public async Task Detalhes_de_lead_de_outra_pessoa_e_negado()
    {
        var doBruno = await b.CriarLeadAsync("Lead do Bruno", b.Bruno);

        var r = Json(await Novo().ExecutarAsync("detalhes_lead", Args(new { lead_id = doBruno.Id }), b.Ana));

        Assert.Contains("sem permiss", r);
        Assert.DoesNotContain("Lead do Bruno", r);
    }

    [Fact]
    public async Task Detalhes_traz_notas_e_tarefas()
    {
        var lead = await b.CriarLeadAsync("Padaria", b.Ana);
        await b.Leads().AdicionarNotaAsync(lead.Id, "Cliente quer o plano anual", b.Ana);
        await b.Leads().CriarTarefaAsync(lead.Id, CrmCDL.Domain.TipoTarefa.Ligacao, "Ligar amanhã", DateTime.UtcNow.AddDays(1), null, b.Ana);

        var r = Json(await Novo().ExecutarAsync("detalhes_lead", Args(new { lead_id = lead.Id }), b.Ana));

        Assert.Contains("plano anual", r);
        Assert.Contains("Ligar amanh", r);
    }

    [Theory]
    [InlineData("listar_funis")]
    [InlineData("resumo_funil")]
    [InlineData("produtos")]
    public async Task Ferramentas_sem_argumentos_respondem(string nome)
    {
        await b.CriarLeadAsync("Lead", b.Ana);
        var r = Json(await Novo().ExecutarAsync(nome, Args(new { }), b.Gestora));
        Assert.DoesNotContain("erro", r);
    }

    [Fact]
    public async Task Tarefas_atrasadas_do_vendedor()
    {
        var lead = await b.CriarLeadAsync("Loja", b.Ana);
        await b.Leads().CriarTarefaAsync(lead.Id, CrmCDL.Domain.TipoTarefa.Retorno, "Atrasada", DateTime.UtcNow.AddDays(-1), null, b.Ana);
        await b.Leads().CriarTarefaAsync(lead.Id, CrmCDL.Domain.TipoTarefa.Retorno, "Futura", DateTime.UtcNow.AddDays(3), null, b.Ana);

        var r = Json(await Novo().ExecutarAsync("tarefas", Args(new { filtro = "atrasadas" }), b.Ana));

        Assert.Contains("Atrasada", r);
        Assert.DoesNotContain("Futura", r);
    }

    [Fact]
    public async Task Sem_chave_configurada_avisa_sem_chamar_a_api()
    {
        var a = Novo();
        Assert.False(a.Configurado);
        var r = await a.ResponderAsync(new List<FalaAssistente>(), "oi", b.Ana, CancellationToken.None);
        Assert.Contains("não foi configurado", r);
    }
}
