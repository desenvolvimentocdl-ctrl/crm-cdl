using ClosedXML.Excel;
using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Web.Infra;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Tests;

/// <summary>Filtros que a tela e a planilha compartilham, e o arquivo Excel gerado.</summary>
public class ExportacaoTests : IDisposable
{
    readonly BancoTeste b = new();
    public void Dispose() => b.Dispose();

    // ---------- filtros ----------

    [Fact]
    public async Task Busca_encontra_lead_pelo_titulo_e_pelo_celular_do_contato()
    {
        await b.Leads().CriarAsync(new NovoLead("Loja Centro", 0, b.Funil.Id, null, null, "Maria", "34999990000", null, null), b.Ana);
        await b.CriarLeadAsync("Padaria", b.Ana);

        Assert.Equal(new[] { "Loja Centro" }, await Titulos(new FiltroLeads(b.Funil.Id, "Loja")));
        Assert.Equal(new[] { "Loja Centro" }, await Titulos(new FiltroLeads(b.Funil.Id, "99999")));
    }

    [Fact]
    public async Task Filtro_por_responsavel_e_por_funil()
    {
        await b.CriarLeadAsync("Da Ana", b.Gestora, responsavelId: b.Ana.Id);
        await b.CriarLeadAsync("Do Bruno", b.Gestora, responsavelId: b.Bruno.Id);
        await b.Leads().CriarAsync(new NovoLead("Outro funil", 0, b.OutroFunil.Id, null, b.Ana.Id, null, null, null, null), b.Gestora);

        Assert.Equal(new[] { "Da Ana" }, await Titulos(new FiltroLeads(b.Funil.Id, ResponsavelId: b.Ana.Id)));
    }

    [Fact]
    public async Task Vendedor_so_exporta_os_proprios_leads_mesmo_filtrando_por_outra_pessoa()
    {
        await b.CriarLeadAsync("Da Ana", b.Ana);
        await b.CriarLeadAsync("Do Bruno", b.Bruno);

        var q = b.Leads().Filtrar(new FiltroLeads(b.Funil.Id, ResponsavelId: b.Bruno.Id), b.Ana, DateTime.UtcNow);

        Assert.Empty(await q.ToListAsync());
    }

    [Fact]
    public async Task Fechados_ha_mais_de_30_dias_so_aparecem_com_o_filtro_marcado()
    {
        var antigo = await b.CriarLeadAsync("Ganho antigo", b.Ana, etapaId: b.Ganho.Id);
        var recente = await b.CriarLeadAsync("Ganho recente", b.Ana, etapaId: b.Ganho.Id);
        antigo.FechadoEm = DateTime.UtcNow.AddDays(-45);
        recente.FechadoEm = DateTime.UtcNow.AddDays(-5);
        await b.Db.SaveChangesAsync();

        Assert.Equal(new[] { "Ganho recente" }, await Titulos(new FiltroLeads(b.Funil.Id)));
        Assert.Equal(2, (await Titulos(new FiltroLeads(b.Funil.Id, IncluirFechadosAntigos: true))).Count);
    }

    [Fact]
    public async Task Busca_de_contatos_encontra_pelo_nome_da_empresa()
    {
        var empresa = new Empresa { Nome = "Supermercado Bom Preço" };
        b.Db.Contatos.AddRange(new Contato { Nome = "João", Empresa = empresa }, new Contato { Nome = "Carla" });
        await b.Db.SaveChangesAsync();

        var nomes = await Consultas.FiltrarContatos(b.Db.Contatos, "Supermercado").Select(c => c.Nome).ToListAsync();

        Assert.Equal(new[] { "João" }, nomes);
    }

    [Fact]
    public void Campos_personalizados_viram_texto_legivel()
    {
        var lista = new CampoPersonalizado
        {
            Tipo = TipoCampo.MultiplaEscolha,
            Opcoes = { new CampoOpcao { Id = 7, Valor = "Associação" }, new CampoOpcao { Id = 9, Valor = "SPC" } }
        };

        Assert.Equal("Associação, SPC", Consultas.TextoDoCampo(lista, "7,9"));
        Assert.Equal("Sim", Consultas.TextoDoCampo(new CampoPersonalizado { Tipo = TipoCampo.Checkbox }, "1"));
        Assert.Equal("", Consultas.TextoDoCampo(lista, null));
        Assert.Equal("texto livre", Consultas.TextoDoCampo(new CampoPersonalizado { Tipo = TipoCampo.Texto }, "texto livre"));
    }

    // ---------- arquivo Excel ----------

    [Fact]
    public void Planilha_tem_cabecalho_valores_com_tipo_certo_e_filtro()
    {
        var data = new DateTime(2026, 11, 19, 14, 30, 0);
        var arquivo = Planilha.Gerar("Leads",
            new[] { new ColunaPlanilha("Lead"), new ColunaPlanilha("Valor", FormatoColuna.Dinheiro), new ColunaPlanilha("Criado em", FormatoColuna.DataHora), new ColunaPlanilha("Vazio") },
            new[] { new object?[] { "Loja Centro", 1500.50m, data, null } });

        using var wb = new XLWorkbook(new MemoryStream(arquivo));
        var ws = wb.Worksheet("Leads");
        Assert.Equal("Lead", ws.Cell(1, 1).GetString());
        Assert.Equal("Loja Centro", ws.Cell(2, 1).GetString());
        Assert.Equal(1500.50, ws.Cell(2, 2).GetDouble());
        Assert.Equal(data, ws.Cell(2, 3).GetDateTime());
        Assert.True(ws.Cell(2, 4).IsEmpty());
        Assert.True(ws.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Texto_que_parece_formula_continua_sendo_texto()
    {
        var arquivo = Planilha.Gerar("Contatos", new[] { new ColunaPlanilha("Nome") }, new[] { new object?[] { "=HYPERLINK(\"http://x\")" } });

        using var wb = new XLWorkbook(new MemoryStream(arquivo));
        var cel = wb.Worksheet(1).Cell(2, 1);
        Assert.False(cel.HasFormula);
        Assert.Equal("=HYPERLINK(\"http://x\")", cel.GetString());
    }

    [Fact]
    public void Planilha_sem_linhas_ainda_abre_com_o_cabecalho()
    {
        var arquivo = Planilha.Gerar("Leads", new[] { new ColunaPlanilha("Lead") }, Array.Empty<object?[]>());

        using var wb = new XLWorkbook(new MemoryStream(arquivo));
        Assert.Equal("Lead", wb.Worksheet(1).Cell(1, 1).GetString());
        Assert.True(wb.Worksheet(1).Cell(2, 1).IsEmpty());
    }

    Task<List<string>> Titulos(FiltroLeads f)
        => b.Leads().Filtrar(f, b.Gestora, DateTime.UtcNow).OrderBy(l => l.Titulo).Select(l => l.Titulo).ToListAsync();
}
