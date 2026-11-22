using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using CrmCDL.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

public class TarefasPainelController(AppDbContext db, LeadService leads) : BaseController
{
    [HttpGet("/tarefas")]
    public async Task<IActionResult> Tarefas(string filtro = "abertas", int? responsavel = null)
    {
        var q = db.Tarefas.Include(t => t.Lead).Include(t => t.Responsavel).AsQueryable();
        if (!Eu.VeTudo) q = q.Where(t => t.ResponsavelId == Eu.Id);
        else if (responsavel is int r) q = q.Where(t => t.ResponsavelId == r);

        var hojeUtcIni = Hora.ParaUtc(Hora.HojeLocal);
        var amanhaUtcIni = Hora.ParaUtc(Hora.HojeLocal.AddDays(1));
        q = filtro switch
        {
            "atrasadas" => q.Where(t => !t.Concluida && t.Prazo < DateTime.UtcNow),
            "hoje" => q.Where(t => !t.Concluida && t.Prazo >= hojeUtcIni && t.Prazo < amanhaUtcIni),
            "concluidas" => q.Where(t => t.Concluida),
            _ => q.Where(t => !t.Concluida)
        };
        q = filtro == "concluidas" ? q.OrderByDescending(t => t.ConcluidaEm) : q.OrderBy(t => t.Prazo);
        ViewBag.Filtro = filtro;
        ViewBag.Responsavel = responsavel;
        ViewBag.Usuarios = Eu.VeTudo ? await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync() : new List<Usuario>();
        return View(await q.Take(500).ToListAsync());
    }

    [HttpPost("/tarefas/nova")]
    public async Task<IActionResult> Nova(TipoTarefa tipo, string texto, DateTime prazo, int? responsavelId)
    {
        await leads.CriarTarefaAsync(null, tipo, texto, Hora.ParaUtc(prazo), responsavelId, Eu, prazo);
        return VoltarCom("Tarefa criada.");
    }

    [HttpPost("/tarefas/{id:int}/concluir")]
    public async Task<IActionResult> Concluir(int id, string? resultado)
    {
        await leads.ConcluirTarefaAsync(id, resultado, Eu);
        return VoltarCom("Tarefa concluída.");
    }

    /// <summary>Painel dos gestores: resultado do período por funil, etapa e responsável.</summary>
    [Authorize(Policy = Politicas.Gestor), HttpGet("/painel")]
    public async Task<IActionResult> Painel(int? funil, DateTime? de, DateTime? ate)
    {
        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        if (funis.Count == 0) return View("Mensagem", "Nenhum funil cadastrado.");
        var atual = funis.FirstOrDefault(f => f.Id == funil) ?? funis.FirstOrDefault(f => f.Principal) ?? funis[0];
        var inicio = (de ?? new DateTime(Hora.HojeLocal.Year, Hora.HojeLocal.Month, 1)).Date;
        var fim = (ate ?? Hora.HojeLocal).Date;
        var iniUtc = Hora.ParaUtc(inicio);
        var fimUtc = Hora.ParaUtc(fim.AddDays(1));

        var doFunil = db.Leads.Where(l => l.FunilId == atual.Id);
        var etapas = await db.Etapas.Where(e => e.FunilId == atual.Id).OrderBy(e => e.Ordem).ToListAsync();
        var fechadosPeriodo = doFunil.Where(l => l.FechadoEm >= iniUtc && l.FechadoEm < fimUtc);

        var vm = new PainelVm
        {
            Funis = funis, Funil = atual, Inicio = inicio, Fim = fim,
            Novos = await doFunil.CountAsync(l => l.CriadoEm >= iniUtc && l.CriadoEm < fimUtc),
            Ganhos = await fechadosPeriodo.CountAsync(l => l.Etapa!.Tipo == TipoEtapa.Ganho),
            Perdidos = await fechadosPeriodo.CountAsync(l => l.Etapa!.Tipo == TipoEtapa.Perdido),
            ValorGanho = await fechadosPeriodo.Where(l => l.Etapa!.Tipo == TipoEtapa.Ganho).SumAsync(l => (decimal?)l.Valor) ?? 0,
            EmAberto = await doFunil.CountAsync(l => l.FechadoEm == null),
            ValorAberto = await doFunil.Where(l => l.FechadoEm == null).SumAsync(l => (decimal?)l.Valor) ?? 0,
            TarefasAtrasadas = await db.Tarefas.CountAsync(t => !t.Concluida && t.Prazo < DateTime.UtcNow && t.Lead!.FunilId == atual.Id),
        };

        var porEtapa = await doFunil.Where(l => l.FechadoEm == null).GroupBy(l => l.EtapaId)
            .Select(g => new { g.Key, Qtd = g.Count(), Valor = g.Sum(l => l.Valor) }).ToListAsync();
        vm.PorEtapa = etapas.Where(e => !e.Fechada)
            .Select(e => (e, porEtapa.FirstOrDefault(p => p.Key == e.Id)?.Qtd ?? 0, porEtapa.FirstOrDefault(p => p.Key == e.Id)?.Valor ?? 0m)).ToList();

        var usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync();
        var abertosPor = await doFunil.Where(l => l.FechadoEm == null).GroupBy(l => l.ResponsavelId).Select(g => new { g.Key, Qtd = g.Count() }).ToListAsync();
        var fechadosPor = await fechadosPeriodo.GroupBy(l => new { l.ResponsavelId, l.Etapa!.Tipo })
            .Select(g => new { g.Key.ResponsavelId, g.Key.Tipo, Qtd = g.Count(), Valor = g.Sum(l => l.Valor) }).ToListAsync();
        var atrasadasPor = await db.Tarefas.Where(t => !t.Concluida && t.Prazo < DateTime.UtcNow).GroupBy(t => t.ResponsavelId).Select(g => new { g.Key, Qtd = g.Count() }).ToListAsync();
        vm.PorResponsavel = usuarios.Select(u => (u.Nome,
                abertosPor.FirstOrDefault(x => x.Key == u.Id)?.Qtd ?? 0,
                fechadosPor.Where(x => x.ResponsavelId == u.Id && x.Tipo == TipoEtapa.Ganho).Sum(x => x.Qtd),
                fechadosPor.Where(x => x.ResponsavelId == u.Id && x.Tipo == TipoEtapa.Perdido).Sum(x => x.Qtd),
                fechadosPor.Where(x => x.ResponsavelId == u.Id && x.Tipo == TipoEtapa.Ganho).Sum(x => x.Valor),
                atrasadasPor.FirstOrDefault(x => x.Key == u.Id)?.Qtd ?? 0))
            .Where(x => x.Item2 + x.Item3 + x.Item4 + x.Item6 > 0).ToList();

        vm.Motivos = (await fechadosPeriodo.Where(l => l.Etapa!.Tipo == TipoEtapa.Perdido)
                .GroupBy(l => l.MotivoPerda != null ? l.MotivoPerda.Nome : "Sem motivo informado")
                .Select(g => new { g.Key, Qtd = g.Count() }).OrderByDescending(x => x.Qtd).ToListAsync())
            .Select(x => (x.Key, x.Qtd)).ToList();

        // Previsão: valor em aberto × chance da etapa, agrupado pelo mês previsto de fechamento.
        var abertos = await doFunil.Where(l => l.FechadoEm == null)
            .Select(l => new { l.Valor, l.DataPrevista, Prob = l.Etapa!.Probabilidade }).ToListAsync();
        vm.ValorPonderado = abertos.Sum(a => a.Valor * a.Prob / 100m);
        var mesAtual = new DateTime(Hora.HojeLocal.Year, Hora.HojeLocal.Month, 1);
        vm.Previsao = Enumerable.Range(0, 3).Select(i => mesAtual.AddMonths(i))
            .Select(m => (m.ToString("MMM/yy", Hora.PtBr),
                abertos.Where(a => a.DataPrevista is DateTime d && d.Year == m.Year && d.Month == m.Month).Sum(a => a.Valor),
                abertos.Where(a => a.DataPrevista is DateTime d && d.Year == m.Year && d.Month == m.Month).Sum(a => a.Valor * a.Prob / 100m)))
            .Append(("Sem data", abertos.Where(a => a.DataPrevista is null).Sum(a => a.Valor), abertos.Where(a => a.DataPrevista is null).Sum(a => a.Valor * a.Prob / 100m)))
            .ToList();
        var limiteParado = DateTime.UtcNow.AddDays(-Consultas.DiasParado);
        vm.Parados = await doFunil.CountAsync(l => l.FechadoEm == null && l.AtualizadoEm < limiteParado);

        // Metas do mês atual (todas as vendas ganhas no mês, em qualquer funil).
        var iniMes = Hora.ParaUtc(mesAtual);
        var fimMes = Hora.ParaUtc(mesAtual.AddMonths(1));
        var metas = await db.Metas.Include(m => m.Usuario).Where(m => m.Ano == mesAtual.Year && m.Mes == mesAtual.Month).ToListAsync();
        var ganhosMes = await db.Leads.Where(l => l.FechadoEm >= iniMes && l.FechadoEm < fimMes && l.Etapa!.Tipo == TipoEtapa.Ganho)
            .GroupBy(l => l.ResponsavelId).Select(g => new { g.Key, Qtd = g.Count(), Valor = g.Sum(l => l.Valor) }).ToListAsync();
        vm.Metas = metas.OrderBy(m => m.Usuario!.Nome).Select(m => (m.Usuario!.Nome, m.Valor,
            ganhosMes.FirstOrDefault(g => g.Key == m.UsuarioId)?.Valor ?? 0m, m.QtdVendas,
            ganhosMes.FirstOrDefault(g => g.Key == m.UsuarioId)?.Qtd ?? 0)).ToList();
        vm.MesMetas = mesAtual.ToString("MMMM", Hora.PtBr);
        return View(vm);
    }
}
