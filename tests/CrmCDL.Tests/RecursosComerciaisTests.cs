using CrmCDL.Application;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Tests;

/// <summary>Produtos no lead, troca de funil, ações em massa, automações, captura do site e duplicados.</summary>
public class RecursosComerciaisTests : IDisposable
{
    readonly BancoTeste b = new();
    public void Dispose() => b.Dispose();

    // ---------- produtos ----------

    [Fact]
    public async Task Produtos_calculam_o_valor_do_lead_com_desconto()
    {
        var curso = new Produto { Nome = "Curso NR1", Preco = 300 };
        b.Db.Produtos.Add(curso);
        await b.Db.SaveChangesAsync();
        var lead = await b.CriarLeadAsync("Padaria", b.Ana, valor: 999);

        await b.Leads().AdicionarItemAsync(lead.Id, curso.Id, null, 2, null, 10, b.Ana);
        await b.Leads().AdicionarItemAsync(lead.Id, null, "Taxa de inscrição", 1, 50, 0, b.Ana);

        using var ctx = b.NovoContexto();
        Assert.Equal(590m, (await ctx.Leads.FindAsync(lead.Id))!.Valor); // 2 × 300 − 10% + 50
        Assert.Equal("Curso NR1", (await ctx.LeadItens.FirstAsync(i => i.ProdutoId == curso.Id)).Descricao);
    }

    [Fact]
    public async Task Com_produtos_o_valor_digitado_e_ignorado_e_sem_produtos_volta_a_valer()
    {
        var lead = await b.CriarLeadAsync("Loja", b.Ana);
        await b.Leads().AdicionarItemAsync(lead.Id, null, "Associação", 1, 1200, 0, b.Ana);

        await b.Leads().AtualizarAsync(lead.Id, "Loja", 5, null, b.Ana);
        Assert.Equal(1200m, (await b.NovoContexto().Leads.FindAsync(lead.Id))!.Valor);

        var item = await b.Db.LeadItens.FirstAsync();
        await b.Leads().RemoverItemAsync(lead.Id, item.Id, b.Ana);
        await b.Leads().AtualizarAsync(lead.Id, "Loja", 5, null, b.Ana);
        Assert.Equal(5m, (await b.NovoContexto().Leads.FindAsync(lead.Id))!.Valor);
    }

    [Fact]
    public async Task Vendedor_nao_mexe_nos_produtos_do_lead_de_outro()
    {
        var lead = await b.CriarLeadAsync("Do Bruno", b.Bruno);
        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().AdicionarItemAsync(lead.Id, null, "X", 1, 10, 0, b.Ana));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 101)]
    [InlineData(1, -1)]
    public async Task Item_com_quantidade_ou_desconto_invalido_e_recusado(decimal qtd, decimal desconto)
    {
        var lead = await b.CriarLeadAsync("Loja", b.Ana);
        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().AdicionarItemAsync(lead.Id, null, "X", qtd, 10, desconto, b.Ana));
    }

    // ---------- outro funil ----------

    [Fact]
    public async Task Mover_de_funil_leva_para_a_etapa_escolhida_e_registra_no_historico()
    {
        var lead = await b.CriarLeadAsync("Loja", b.Ana);
        var destino = b.OutroFunil.Etapas.Single(e => e.Nome == "Negociação");

        await b.Leads().MoverFunilAsync(lead.Id, b.OutroFunil.Id, destino.Id, b.Ana);

        using var ctx = b.NovoContexto();
        var salvo = await ctx.Leads.FindAsync(lead.Id);
        Assert.Equal(b.OutroFunil.Id, salvo!.FunilId);
        Assert.Equal(destino.Id, salvo.EtapaId);
        Assert.Contains(await ctx.Atividades.Where(a => a.LeadId == lead.Id).Select(a => a.Texto).ToListAsync(), t => t.Contains("Movido do funil"));
    }

    // ---------- ações em massa ----------

    [Fact]
    public async Task Gestor_passa_varios_leads_para_outra_pessoa_e_ela_e_avisada()
    {
        var l1 = await b.CriarLeadAsync("A", b.Gestora, b.Ana.Id);
        var l2 = await b.CriarLeadAsync("B", b.Gestora, b.Ana.Id);

        var n = await b.Leads().EmMassaAsync(new[] { l1.Id, l2.Id }, AcaoEmMassa.Responsavel, b.Bruno.Id, null, b.Gestora);

        Assert.Equal(2, n);
        using var ctx = b.NovoContexto();
        Assert.All(await ctx.Leads.ToListAsync(), l => Assert.Equal(b.Bruno.Id, l.ResponsavelId));
        Assert.True(await ctx.Notificacoes.AnyAsync(x => x.UsuarioId == b.Bruno.Id && x.Texto.Contains("2 lead")));
    }

    [Fact]
    public async Task Vendedor_nao_troca_responsavel_nem_exclui_em_massa()
    {
        var l = await b.CriarLeadAsync("A", b.Ana);
        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().EmMassaAsync(new[] { l.Id }, AcaoEmMassa.Responsavel, b.Bruno.Id, null, b.Ana));
        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().EmMassaAsync(new[] { l.Id }, AcaoEmMassa.Excluir, null, null, b.Ana));
    }

    [Fact]
    public async Task Acao_em_massa_ignora_leads_que_a_pessoa_nao_ve()
    {
        var meu = await b.CriarLeadAsync("Meu", b.Ana);
        var dele = await b.CriarLeadAsync("Dele", b.Bruno);

        var n = await b.Leads().EmMassaAsync(new[] { meu.Id, dele.Id }, AcaoEmMassa.Tag, null, "evento", b.Ana);

        Assert.Equal(1, n);
        using var ctx = b.NovoContexto();
        Assert.Equal(new[] { meu.Id }, await ctx.LeadTags.Select(x => x.LeadId).ToListAsync());
    }

    [Fact]
    public async Task Mover_em_massa_para_perdido_e_recusado_porque_precisa_do_motivo()
    {
        var l = await b.CriarLeadAsync("A", b.Ana);
        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().EmMassaAsync(new[] { l.Id }, AcaoEmMassa.Etapa, b.Perdido.Id, null, b.Ana));
    }

    // ---------- automações ----------

    [Fact]
    public async Task Entrar_na_etapa_cria_a_tarefa_automatica_para_o_responsavel()
    {
        b.Db.Automacoes.Add(new AutomacaoEtapa { EtapaId = b.Negociacao.Id, TipoTarefa = TipoTarefa.Ligacao, Texto = "Ligar para negociar", PrazoDias = 2, HoraPrazo = 10 });
        await b.Db.SaveChangesAsync();
        var lead = await b.CriarLeadAsync("Loja", b.Gestora, b.Ana.Id);

        await b.Leads().MoverAsync(lead.Id, b.Negociacao.Id, null, null, b.Gestora);

        using var ctx = b.NovoContexto();
        var t = await ctx.Tarefas.SingleAsync();
        Assert.Equal("Ligar para negociar", t.Texto);
        Assert.Equal(b.Ana.Id, t.ResponsavelId);
        var local = Fuso.Local(t.Prazo);
        Assert.Equal(Fuso.Local(DateTime.UtcNow).Date.AddDays(2), local.Date);
        Assert.Equal(10, local.Hour);
    }

    [Fact]
    public async Task Automacao_desativada_nao_cria_tarefa()
    {
        b.Db.Automacoes.Add(new AutomacaoEtapa { EtapaId = b.Negociacao.Id, Texto = "X", Ativo = false });
        await b.Db.SaveChangesAsync();
        var lead = await b.CriarLeadAsync("Loja", b.Ana);

        await b.Leads().MoverAsync(lead.Id, b.Negociacao.Id, null, null, b.Ana);

        Assert.False(await b.NovoContexto().Tarefas.AnyAsync());
    }

    // ---------- captura do site ----------

    async Task<FormularioCaptura> FormularioAsync(Distribuicao d, params int[] participantes)
    {
        var f = new FormularioCaptura
        {
            Nome = "Associe-se", Token = CapturaService.NovoToken(), FunilId = b.Funil.Id, EtapaId = b.Entrada.Id,
            Distribuicao = d, ResponsavelFixoId = b.Ana.Id, ParticipantesIds = string.Join(",", participantes)
        };
        b.Db.Formularios.Add(f);
        await b.Db.SaveChangesAsync();
        return f;
    }

    static Dictionary<string, string> Envio(string nome, string fone, string? email = null) => new()
    {
        ["nome"] = nome, ["telefone"] = fone, ["email"] = email ?? "", ["empresa"] = "Loja X", ["_redirect"] = "https://x"
    };

    [Fact]
    public async Task Formulario_cria_lead_contato_empresa_e_avisa_o_responsavel()
    {
        var f = await FormularioAsync(Distribuicao.Fixo);

        var lead = await new CapturaService(b.Db).ReceberAsync(f.Token, Envio("Maria", "(34) 99999-1234", "maria@x.com"));

        using var ctx = b.NovoContexto();
        var salvo = await ctx.Leads.Include(l => l.Contatos).ThenInclude(c => c.Contato).Include(l => l.Empresa).FirstAsync(l => l.Id == lead.Id);
        Assert.Equal(b.Entrada.Id, salvo.EtapaId);
        Assert.Equal(b.Ana.Id, salvo.ResponsavelId);
        Assert.Equal("Maria", salvo.Contatos.Single().Contato!.Nome);
        Assert.Equal("Loja X", salvo.Empresa!.Nome);
        Assert.StartsWith("Formulário", salvo.Origem);
        Assert.True(await ctx.Notificacoes.AnyAsync(n => n.UsuarioId == b.Ana.Id && n.LeadId == lead.Id));
        Assert.Equal(1, (await ctx.Formularios.FindAsync(f.Id))!.Recebidos);
        // campos de controle (_redirect) não vão para o histórico
        Assert.DoesNotContain("_redirect", (await ctx.Atividades.FirstAsync(a => a.LeadId == lead.Id)).Texto);
    }

    [Fact]
    public async Task Rodizio_reveza_entre_os_participantes_ativos()
    {
        var f = await FormularioAsync(Distribuicao.Rodizio, b.Ana.Id, b.Bruno.Id);
        var s = new CapturaService(b.Db);

        var r = new List<int?>();
        for (var i = 0; i < 4; i++) r.Add((await s.ReceberAsync(f.Token, Envio($"Pessoa {i}", $"3499999000{i}"))).ResponsavelId);

        Assert.Equal(new int?[] { b.Ana.Id, b.Bruno.Id, b.Ana.Id, b.Bruno.Id }, r);
    }

    [Fact]
    public async Task Rodizio_pula_quem_foi_desativado()
    {
        var f = await FormularioAsync(Distribuicao.Rodizio, b.Ana.Id, b.Bruno.Id);
        (await b.Db.Usuarios.FindAsync(b.Ana.Id))!.Ativo = false;
        await b.Db.SaveChangesAsync();

        var lead = await new CapturaService(b.Db).ReceberAsync(f.Token, Envio("Maria", "34999990000"));

        Assert.Equal(b.Bruno.Id, lead.ResponsavelId);
    }

    [Fact]
    public async Task Mesmo_telefone_com_outra_formatacao_reaproveita_o_contato()
    {
        var f = await FormularioAsync(Distribuicao.Fixo);
        var s = new CapturaService(b.Db);
        await s.ReceberAsync(f.Token, Envio("Maria", "(34) 99999-1234"));
        await s.ReceberAsync(f.Token, Envio("Maria Souza", "+55 34 9 9999 1234"));

        using var ctx = b.NovoContexto();
        Assert.Equal(1, await ctx.Contatos.CountAsync());
        Assert.Equal(2, await ctx.Leads.CountAsync());
    }

    [Fact]
    public async Task Formulario_desativado_ou_token_errado_e_recusado()
    {
        var f = await FormularioAsync(Distribuicao.Fixo);
        f.Ativo = false;
        await b.Db.SaveChangesAsync();
        var s = new CapturaService(b.Db);

        await Assert.ThrowsAsync<RegraNegocioException>(() => s.ReceberAsync(f.Token, Envio("Maria", "34999991234")));
        await Assert.ThrowsAsync<RegraNegocioException>(() => s.ReceberAsync("token-que-nao-existe", Envio("Maria", "34999991234")));
    }

    [Fact]
    public async Task Envio_sem_nome_email_nem_telefone_e_recusado()
    {
        var f = await FormularioAsync(Distribuicao.Fixo);
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            new CapturaService(b.Db).ReceberAsync(f.Token, new Dictionary<string, string> { ["mensagem"] = "oi" }));
    }

    [Fact]
    public async Task Campo_personalizado_com_o_mesmo_nome_do_enviado_e_preenchido()
    {
        var origem = new CampoPersonalizado { Nome = "Origem Lead", Entidade = EntidadeCampo.Lead, Tipo = TipoCampo.Lista, Opcoes = { new CampoOpcao { Valor = "Site" } } };
        var utm = new CampoPersonalizado { Nome = "utm_source", Entidade = EntidadeCampo.Lead, Tipo = TipoCampo.Rastreamento };
        b.Db.Campos.AddRange(origem, utm);
        await b.Db.SaveChangesAsync();
        var f = await FormularioAsync(Distribuicao.Fixo);
        var dados = Envio("Maria", "34999991234");
        dados["utm_source"] = "instagram";
        dados["Origem Lead"] = "site";

        var lead = await new CapturaService(b.Db).ReceberAsync(f.Token, dados);

        var valores = await b.NovoContexto().ValoresCampo.Where(v => v.RegistroId == lead.Id).ToDictionaryAsync(v => v.CampoId, v => v.Valor);
        Assert.Equal("instagram", valores[utm.Id]);
        Assert.Equal(origem.Opcoes[0].Id.ToString(), valores[origem.Id]);
    }

    // ---------- duplicados e parados ----------

    [Theory]
    [InlineData("(34) 99999-1234", null, true)]
    [InlineData("+55 34 999991234", null, true)]
    [InlineData(null, "MARIA@X.COM", true)]
    [InlineData("(34) 98888-0000", "outra@x.com", false)]
    [InlineData("123", null, false)]
    public async Task Duplicados_por_telefone_ou_email(string? fone, string? email, bool achou)
    {
        b.Db.Contatos.Add(new Contato { Nome = "Maria", Celular = "34 99999 1234", Email = "maria@x.com" });
        await b.Db.SaveChangesAsync();

        var r = await Consultas.Duplicados(b.NovoContexto().Contatos, fone, email).AnyAsync();

        Assert.Equal(achou, r);
    }

    [Fact]
    public async Task Filtro_de_parados_traz_so_abertos_sem_movimento_ha_uma_semana()
    {
        var parado = await b.CriarLeadAsync("Parado", b.Ana);
        await b.CriarLeadAsync("Recente", b.Ana);
        var ganhoAntigo = await b.CriarLeadAsync("Ganho antigo", b.Ana);
        await b.Leads().MoverAsync(ganhoAntigo.Id, b.Ganho.Id, null, null, b.Ana);
        foreach (var l in await b.Db.Leads.Where(l => l.Id != parado.Id - 100 && l.Titulo != "Recente").ToListAsync())
            l.AtualizadoEm = DateTime.UtcNow.AddDays(-10);
        await b.Db.SaveChangesAsync();

        var r = await Consultas.FiltrarLeads(b.NovoContexto().Leads, new FiltroLeads(b.Funil.Id, SoParados: true, IncluirFechadosAntigos: true), DateTime.UtcNow)
            .Select(l => l.Titulo).ToListAsync();

        Assert.Equal(new[] { "Parado" }, r);
    }
}
