using CrmCDL.Application;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Tests;

/// <summary>Regras principais do funil: quem vê o quê, criação, troca de etapa e permissões.</summary>
public class LeadServiceTests : IDisposable
{
    readonly BancoTeste b = new();
    public void Dispose() => b.Dispose();

    // ---------- quem vê o quê ----------

    [Fact]
    public async Task Vendedor_ve_so_os_proprios_leads()
    {
        await b.CriarLeadAsync("Lead da Ana", b.Ana);
        await b.CriarLeadAsync("Lead do Bruno", b.Bruno);

        var daAna = await b.Leads().Visiveis(b.Ana).Select(l => l.Titulo).ToListAsync();

        Assert.Equal(new[] { "Lead da Ana" }, daAna);
    }

    [Fact]
    public async Task Gestor_ve_os_leads_de_todos()
    {
        await b.CriarLeadAsync("Lead da Ana", b.Ana);
        await b.CriarLeadAsync("Lead do Bruno", b.Bruno);

        Assert.Equal(2, await b.Leads().Visiveis(b.Gestora).CountAsync());
    }

    [Fact]
    public async Task Vendedor_nao_abre_lead_de_outro_vendedor()
    {
        var lead = await b.CriarLeadAsync("Lead da Ana", b.Ana);

        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().ObterAsync(lead.Id, b.Bruno));
    }

    // ---------- criação ----------

    [Fact]
    public async Task Lead_sem_nome_e_recusado()
    {
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => b.CriarLeadAsync("   ", b.Ana));
        Assert.Contains("nome", erro.Message);
    }

    [Fact]
    public async Task Vendedor_sempre_fica_como_responsavel_do_que_cria()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana, responsavelId: b.Bruno.Id);

        Assert.Equal(b.Ana.Id, lead.ResponsavelId);
    }

    [Fact]
    public async Task Gestor_pode_criar_lead_para_outra_pessoa()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Gestora, responsavelId: b.Bruno.Id);

        Assert.Equal(b.Bruno.Id, lead.ResponsavelId);
    }

    [Fact]
    public async Task Lead_sem_etapa_vai_para_a_primeira_etapa_de_trabalho_e_no_topo_da_coluna()
    {
        var primeiro = await b.CriarLeadAsync("Primeiro", b.Ana);
        var segundo = await b.CriarLeadAsync("Segundo", b.Ana);

        Assert.Equal(b.Contato.Id, primeiro.EtapaId);
        Assert.True(segundo.Posicao < primeiro.Posicao, "o lead mais novo deve aparecer acima");
    }

    [Fact]
    public async Task Criar_lead_com_contato_e_empresa_vincula_os_dois_e_registra_historico()
    {
        var lead = await b.Leads().CriarAsync(
            new NovoLead("Loja Centro", 1500, b.Funil.Id, null, null, "Maria", "34999990000", "maria@loja", "Loja Centro LTDA"), b.Ana);

        using var db = b.NovoContexto();
        var salvo = await db.Leads.Include(l => l.Empresa).Include(l => l.Contatos).ThenInclude(c => c.Contato).Include(l => l.Atividades)
            .SingleAsync(l => l.Id == lead.Id);
        Assert.Equal("Loja Centro LTDA", salvo.Empresa!.Nome);
        var contato = Assert.Single(salvo.Contatos);
        Assert.True(contato.Principal);
        Assert.Equal("Maria", contato.Contato!.Nome);
        Assert.Equal(salvo.EmpresaId, contato.Contato.EmpresaId);
        Assert.Contains(salvo.Atividades, a => a.Tipo == TipoAtividade.Criacao);
    }

    // ---------- troca de etapa ----------

    [Fact]
    public async Task Mover_para_venda_ganha_fecha_o_lead_e_registra_no_historico()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().MoverAsync(lead.Id, b.Ganho.Id, null, null, b.Ana);

        using var db = b.NovoContexto();
        var salvo = await db.Leads.SingleAsync(l => l.Id == lead.Id);
        Assert.Equal(b.Ganho.Id, salvo.EtapaId);
        Assert.NotNull(salvo.FechadoEm);
        Assert.Contains(await db.Atividades.Where(a => a.LeadId == lead.Id).ToListAsync(),
            a => a.Tipo == TipoAtividade.MudancaEtapa && a.Texto.Contains("Venda ganha"));
    }

    [Fact]
    public async Task Perder_guarda_o_motivo_e_reabrir_limpa_motivo_e_data_de_fechamento()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().MoverAsync(lead.Id, b.Perdido.Id, null, b.Preco.Id, b.Ana);
        Assert.Equal(b.Preco.Id, lead.MotivoPerdaId);
        Assert.NotNull(lead.FechadoEm);

        await b.Leads().MoverAsync(lead.Id, b.Negociacao.Id, null, null, b.Ana);
        Assert.Null(lead.MotivoPerdaId);
        Assert.Null(lead.FechadoEm);
    }

    [Fact]
    public async Task Mover_para_etapa_de_outro_funil_e_recusado()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);
        var etapaDeOutroFunil = b.OutroFunil.Etapas.First(e => e.Tipo == TipoEtapa.Normal);

        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().MoverAsync(lead.Id, etapaDeOutroFunil.Id, null, null, b.Ana));
    }

    [Fact]
    public async Task Vendedor_nao_move_lead_de_outro_vendedor()
    {
        var lead = await b.CriarLeadAsync("Lead da Ana", b.Ana);

        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().MoverAsync(lead.Id, b.Ganho.Id, null, null, b.Bruno));
    }

    [Fact]
    public async Task Mudar_so_a_posicao_na_mesma_coluna_nao_gera_historico()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().MoverAsync(lead.Id, lead.EtapaId, 42, null, b.Ana);

        Assert.Equal(42, lead.Posicao);
        Assert.DoesNotContain(await b.Db.Atividades.Where(a => a.LeadId == lead.Id).ToListAsync(), a => a.Tipo == TipoAtividade.MudancaEtapa);
    }

    // ---------- edição, tarefas e exclusão ----------

    [Fact]
    public async Task Vendedor_nao_consegue_trocar_o_responsavel()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().AtualizarAsync(lead.Id, "Lead", 0, b.Bruno.Id, b.Ana);

        Assert.Equal(b.Ana.Id, lead.ResponsavelId);
    }

    [Fact]
    public async Task Gestor_troca_o_responsavel_e_fica_registrado()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().AtualizarAsync(lead.Id, "Lead", 0, b.Bruno.Id, b.Gestora);

        Assert.Equal(b.Bruno.Id, lead.ResponsavelId);
        Assert.Contains(await b.Db.Atividades.Where(a => a.LeadId == lead.Id).ToListAsync(),
            a => a.Tipo == TipoAtividade.MudancaResponsavel && a.Texto.Contains("Bruno"));
    }

    [Fact]
    public async Task Vendedor_nao_conclui_tarefa_de_outro_vendedor()
    {
        await b.Leads().CriarTarefaAsync(null, TipoTarefa.Ligacao, "Ligar", DateTime.UtcNow.AddHours(1), null, b.Ana);
        var tarefa = await b.Db.Tarefas.SingleAsync();

        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().ConcluirTarefaAsync(tarefa.Id, "feito", b.Bruno));
        Assert.False(tarefa.Concluida);
    }

    [Fact]
    public async Task Concluir_tarefa_do_lead_guarda_resultado_e_registra_no_historico()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);
        await b.Leads().CriarTarefaAsync(lead.Id, TipoTarefa.Ligacao, "Ligar", DateTime.UtcNow.AddHours(1), null, b.Ana);
        var tarefa = await b.Db.Tarefas.SingleAsync();

        await b.Leads().ConcluirTarefaAsync(tarefa.Id, "  Cliente pediu proposta  ", b.Ana);

        Assert.True(tarefa.Concluida);
        Assert.Equal("Cliente pediu proposta", tarefa.Resultado);
        Assert.Contains(await b.Db.Atividades.Where(a => a.LeadId == lead.Id).ToListAsync(), a => a.Tipo == TipoAtividade.TarefaConcluida);
    }

    [Fact]
    public async Task Vendedor_nao_pode_excluir_lead()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await Assert.ThrowsAsync<RegraNegocioException>(() => b.Leads().ExcluirAsync(lead.Id, b.Ana));
    }

    [Fact]
    public async Task Gestor_exclui_lead_junto_com_campos_tarefas_e_notificacoes()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Gestora, responsavelId: b.Ana.Id);
        await b.Leads().CriarTarefaAsync(lead.Id, TipoTarefa.Retorno, "Retornar", DateTime.UtcNow.AddDays(1), b.Ana.Id, b.Gestora);
        var campo = new CampoPersonalizado { Nome = "Origem", Entidade = EntidadeCampo.Lead };
        b.Db.Campos.Add(campo);
        await b.Db.SaveChangesAsync();
        b.Db.ValoresCampo.Add(new ValorCampo { CampoId = campo.Id, Entidade = EntidadeCampo.Lead, RegistroId = lead.Id, Valor = "Site" });
        await b.Db.SaveChangesAsync();

        await b.Leads().ExcluirAsync(lead.Id, b.Gestora);

        using var db = b.NovoContexto();
        Assert.False(await db.Leads.AnyAsync());
        Assert.False(await db.Tarefas.AnyAsync());
        Assert.False(await db.ValoresCampo.AnyAsync());
        Assert.False(await db.Notificacoes.AnyAsync());
    }
}
