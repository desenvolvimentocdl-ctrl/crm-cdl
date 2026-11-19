using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Tests;

/// <summary>Avisos do sino: quando são criados, para quem, e os lembretes automáticos de tarefas.</summary>
public class NotificacaoTests : IDisposable
{
    readonly BancoTeste b = new();
    public void Dispose() => b.Dispose();

    // 19/11/2026 15:00 UTC = 12:00 em Brasília. O dia em Brasília acaba às 03:00 UTC do dia 20.
    static readonly DateTime Agora = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);
    static readonly DateTime FimDoDia = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
    static DateTime Brasilia(DateTime utc) => utc.AddHours(-3);

    Task<int> GerarLembretes(DateTime? agora = null)
        => b.Notificacoes().GerarLembretesAsync(agora ?? Agora, FimDoDia, Brasilia);

    async Task<Tarefa> NovaTarefa(DateTime prazo, int? responsavelId = null, int? leadId = null)
    {
        var t = new Tarefa { Texto = "Ligar para o cliente", Prazo = prazo, ResponsavelId = responsavelId ?? b.Ana.Id, LeadId = leadId };
        b.Db.Tarefas.Add(t);
        await b.Db.SaveChangesAsync();
        return t;
    }

    // ---------- avisos de lead e tarefa ----------

    [Fact]
    public async Task Gestor_cria_lead_para_vendedora_e_ela_e_avisada()
    {
        var lead = await b.CriarLeadAsync("Loja Centro", b.Gestora, responsavelId: b.Ana.Id);

        var n = Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(b.Ana.Id, n.UsuarioId);
        Assert.Equal(TipoNotificacao.LeadAtribuido, n.Tipo);
        Assert.Equal(lead.Id, n.LeadId);
        Assert.Equal($"/leads/{lead.Id}", n.Link);
        Assert.Contains("Loja Centro", n.Texto);
    }

    [Fact]
    public async Task Quem_cria_o_proprio_lead_nao_recebe_aviso()
    {
        await b.CriarLeadAsync("Lead", b.Ana);
        await b.CriarLeadAsync("Lead", b.Gestora); // gestora sem responsável informado fica com o lead

        Assert.False(await b.Db.Notificacoes.AnyAsync());
    }

    [Fact]
    public async Task Trocar_o_responsavel_avisa_quem_recebeu_o_lead()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().AtualizarAsync(lead.Id, "Lead", 0, b.Bruno.Id, b.Gestora);

        var n = Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(b.Bruno.Id, n.UsuarioId);
        Assert.Contains("passou o lead", n.Texto);
    }

    [Fact]
    public async Task Salvar_o_lead_sem_trocar_o_responsavel_nao_avisa_ninguem()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);

        await b.Leads().AtualizarAsync(lead.Id, "Lead renomeado", 10, b.Ana.Id, b.Gestora);

        Assert.False(await b.Db.Notificacoes.AnyAsync());
    }

    [Fact]
    public async Task Tarefa_criada_para_outra_pessoa_gera_aviso_e_para_si_mesmo_nao()
    {
        await b.Leads().CriarTarefaAsync(null, TipoTarefa.Ligacao, "Ligar", Agora, b.Bruno.Id, b.Gestora);
        await b.Leads().CriarTarefaAsync(null, TipoTarefa.Ligacao, "Ligar", Agora, null, b.Bruno);

        var n = Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(b.Bruno.Id, n.UsuarioId);
        Assert.Equal(TipoNotificacao.TarefaAtribuida, n.Tipo);
        Assert.Equal("/tarefas", n.Link);
    }

    // ---------- lembretes automáticos ----------

    [Fact]
    public async Task Tarefa_para_hoje_gera_um_lembrete_so_mesmo_rodando_varias_vezes()
    {
        await NovaTarefa(Agora.AddHours(2));

        Assert.Equal(1, await GerarLembretes());
        Assert.Equal(0, await GerarLembretes());

        var n = Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(TipoNotificacao.TarefaHoje, n.Tipo);
        Assert.Contains("hoje às 14:00", n.Texto); // 17:00 UTC = 14:00 em Brasília
    }

    [Fact]
    public async Task Tarefa_que_passa_do_horario_ganha_um_segundo_lembrete_de_atrasada()
    {
        var t = await NovaTarefa(Agora.AddHours(2));
        await GerarLembretes();

        await GerarLembretes(Agora.AddHours(3));

        var tipos = await b.Db.Notificacoes.OrderBy(n => n.Id).Select(n => n.Tipo).ToListAsync();
        Assert.Equal(new[] { TipoNotificacao.TarefaHoje, TipoNotificacao.TarefaAtrasada }, tipos);
    }

    [Fact]
    public async Task Tarefa_de_amanha_concluida_ou_atrasada_ha_mais_de_uma_semana_nao_gera_lembrete()
    {
        await NovaTarefa(FimDoDia.AddHours(1));                // amanhã
        await NovaTarefa(Agora.AddDays(-8));                   // atrasada há muito tempo
        var feita = await NovaTarefa(Agora.AddHours(1));
        feita.Concluida = true;
        await b.Db.SaveChangesAsync();

        Assert.Equal(0, await GerarLembretes());
    }

    [Fact]
    public async Task Lembrete_de_tarefa_do_lead_abre_o_lead()
    {
        var lead = await b.CriarLeadAsync("Lead", b.Ana);
        await NovaTarefa(Agora.AddHours(-1), leadId: lead.Id);

        await GerarLembretes();

        var n = Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(TipoNotificacao.TarefaAtrasada, n.Tipo);
        Assert.Equal($"/leads/{lead.Id}", n.Link);
    }

    // ---------- leitura ----------

    [Fact]
    public async Task Contagem_e_marcar_como_lida_valem_so_para_o_dono()
    {
        await NovaTarefa(Agora.AddHours(1), b.Ana.Id);
        await NovaTarefa(Agora.AddHours(1), b.Bruno.Id);
        await GerarLembretes();
        var daAna = await b.Db.Notificacoes.SingleAsync(n => n.UsuarioId == b.Ana.Id);

        Assert.Null(await b.Notificacoes().AbrirAsync(daAna.Id, b.Bruno.Id));
        Assert.False(daAna.Lida);

        Assert.NotNull(await b.Notificacoes().AbrirAsync(daAna.Id, b.Ana.Id));
        Assert.Equal(0, await b.Notificacoes().NaoLidasAsync(b.Ana.Id));
        Assert.Equal(1, await b.Notificacoes().NaoLidasAsync(b.Bruno.Id));
    }

    [Fact]
    public async Task Marcar_todas_como_lidas_zera_o_contador()
    {
        await NovaTarefa(Agora.AddHours(1));
        await NovaTarefa(Agora.AddHours(-1));
        await GerarLembretes();
        Assert.Equal(2, await b.Notificacoes().NaoLidasAsync(b.Ana.Id));

        await b.Notificacoes().MarcarTodasComoLidasAsync(b.Ana.Id);

        Assert.Equal(0, await b.Notificacoes().NaoLidasAsync(b.Ana.Id));
    }

    [Fact]
    public async Task Texto_muito_longo_e_cortado_para_caber_no_banco()
    {
        await b.Leads().CriarTarefaAsync(null, TipoTarefa.Ligacao, new string('x', 900), Agora, b.Bruno.Id, b.Gestora);

        var n = await b.Db.Notificacoes.SingleAsync();
        Assert.Equal(500, n.Texto.Length);
        Assert.EndsWith("…", n.Texto);
    }
}
