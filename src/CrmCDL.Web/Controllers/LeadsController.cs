using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using CrmCDL.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

public class LeadsController(AppDbContext db, LeadService leads, ConfiguracaoService config) : BaseController
{
    [HttpGet("/")]
    public IActionResult Inicio() => Redirect("/leads");

    /// <summary>Quadro do funil (colunas por etapa).</summary>
    [HttpGet("/leads")]
    public async Task<IActionResult> Quadro(int? funil, string? busca, int? responsavel, bool fechados = false)
    {
        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        if (funis.Count == 0)
            return Eu.Admin ? Redirect("/configuracoes/kommo")
                            : View("Mensagem", "Nenhum funil cadastrado ainda. Peça a um administrador para importar da Kommo ou criar o primeiro funil.");
        var atual = funis.FirstOrDefault(f => f.Id == funil) ?? funis.FirstOrDefault(f => f.Principal) ?? funis[0];
        var etapas = await db.Etapas.Where(e => e.FunilId == atual.Id).OrderBy(e => e.Ordem).ToListAsync();

        var q = leads.Visiveis(Eu).Where(l => l.FunilId == atual.Id);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim();
            q = q.Where(l => l.Titulo.Contains(b) || (l.Empresa != null && l.Empresa.Nome.Contains(b))
                             || l.Contatos.Any(c => c.Contato!.Nome.Contains(b) || (c.Contato.Celular != null && c.Contato.Celular.Contains(b)) || (c.Contato.Email != null && c.Contato.Email.Contains(b))));
        }
        if (responsavel is int r) q = q.Where(l => l.ResponsavelId == r);
        // Colunas de ganho/perdido mostram só os últimos 30 dias, para o quadro não ficar pesado.
        var limiteFechados = DateTime.UtcNow.AddDays(-30);
        if (!fechados) q = q.Where(l => l.FechadoEm == null || l.FechadoEm >= limiteFechados);

        var agora = DateTime.UtcNow;
        var cartoes = await q.OrderBy(l => l.Posicao).ThenByDescending(l => l.AtualizadoEm)
            .Select(l => new CartaoLead(
                l.Id, l.Titulo, l.Valor, l.EtapaId,
                l.Contatos.Where(c => c.Principal).Select(c => c.Contato!.Nome).FirstOrDefault(),
                l.Empresa != null ? l.Empresa.Nome : null,
                l.Responsavel != null ? l.Responsavel.Nome : null,
                l.Tags.Select(t => t.Tag!.Nome).ToList(),
                l.Tarefas.Where(t => !t.Concluida).OrderBy(t => t.Prazo).Select(t => (DateTime?)t.Prazo).FirstOrDefault(),
                l.AtualizadoEm, l.Posicao))
            .Take(2000).ToListAsync();

        var vm = new QuadroVm(atual, funis, etapas, cartoes, busca, responsavel, fechados,
            Eu.VeTudo ? await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync() : new(),
            await db.MotivosPerda.OrderBy(m => m.Ordem).ToListAsync(), agora);
        return View(vm);
    }

    [HttpPost("/leads/novo")]
    public async Task<IActionResult> Criar(NovoLead novo, bool abrir = false)
    {
        var lead = await leads.CriarAsync(novo, Eu);
        if (abrir) return Redirect($"/leads/{lead.Id}");
        TempData["msg"] = $"Lead \"{lead.Titulo}\" criado.";
        return Redirect($"/leads?funil={lead.FunilId}");
    }

    public record MoverReq(int EtapaId, double? Posicao, int? MotivoPerdaId);

    /// <summary>Chamado pelo quadro ao soltar um cartão em outra posição/etapa.</summary>
    [HttpPost("/leads/{id:int}/mover")]
    public async Task<IActionResult> Mover(int id, [FromBody] MoverReq req)
    {
        await leads.MoverAsync(id, req.EtapaId, req.Posicao, req.MotivoPerdaId, Eu);
        return Ok(new { ok = true });
    }

    [HttpGet("/leads/{id:int}")]
    public async Task<IActionResult> Detalhe(int id)
    {
        var lead = await leads.Visiveis(Eu)
            .Include(l => l.Funil).Include(l => l.Etapa).Include(l => l.Responsavel).Include(l => l.Empresa).Include(l => l.MotivoPerda)
            .Include(l => l.Contatos).ThenInclude(c => c.Contato)
            .Include(l => l.Tags).ThenInclude(t => t.Tag)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null) return NotFound();

        var vm = new LeadVm
        {
            Lead = lead,
            Etapas = await db.Etapas.Where(e => e.FunilId == lead.FunilId).OrderBy(e => e.Ordem).ToListAsync(),
            Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync(),
            Campos = await db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == EntidadeCampo.Lead && c.Ativo).OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync(),
            Valores = await config.ValoresAsync(EntidadeCampo.Lead, lead.Id),
            Atividades = await db.Atividades.Include(a => a.Usuario).Where(a => a.LeadId == id).OrderByDescending(a => a.CriadoEm).Take(300).ToListAsync(),
            TarefasAbertas = await db.Tarefas.Include(t => t.Responsavel).Where(t => t.LeadId == id && !t.Concluida).OrderBy(t => t.Prazo).ToListAsync(),
            MotivosPerda = await db.MotivosPerda.OrderBy(m => m.Ordem).ToListAsync(),
            TodasTags = await db.Tags.OrderBy(t => t.Nome).Select(t => t.Nome).ToListAsync(),
            PodeGerir = Eu.VeTudo
        };
        return View(vm);
    }

    [HttpPost("/leads/{id:int}")]
    public async Task<IActionResult> Salvar(int id, string titulo, decimal valor, int? responsavelId, string? tags)
    {
        await leads.AtualizarAsync(id, titulo, valor, responsavelId, Eu);
        await leads.DefinirTagsAsync(id, (tags ?? "").Split(','), Eu);
        await config.SalvarValoresAsync(EntidadeCampo.Lead, id, Texto.CamposDoForm(Request.Form));
        return VoltarCom("Alterações salvas.", $"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/etapa")]
    public async Task<IActionResult> TrocarEtapa(int id, int etapaId, int? motivoPerdaId)
    {
        await leads.MoverAsync(id, etapaId, null, motivoPerdaId, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/nota")]
    public async Task<IActionResult> Nota(int id, string texto)
    {
        await leads.AdicionarNotaAsync(id, texto, Eu);
        return Redirect($"/leads/{id}#feed");
    }

    [HttpPost("/leads/{id:int}/tarefa")]
    public async Task<IActionResult> Tarefa(int id, TipoTarefa tipo, string texto, DateTime prazo, int? responsavelId)
    {
        await leads.CriarTarefaAsync(id, tipo, texto, Hora.ParaUtc(prazo), responsavelId, Eu, prazo);
        return Redirect($"/leads/{id}#feed");
    }

    [HttpPost("/leads/{id:int}/contatos")]
    public async Task<IActionResult> VincularContato(int id, int? contatoId, string? nome, string? celular, string? email)
    {
        if (contatoId is null)
        {
            if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do contato.");
            var lead = await leads.ObterAsync(id, Eu);
            var c = new Contato { Nome = nome.Trim(), Celular = celular?.Trim(), Email = email?.Trim(), EmpresaId = lead.EmpresaId, ResponsavelId = lead.ResponsavelId };
            db.Contatos.Add(c);
            await db.SaveChangesAsync();
            contatoId = c.Id;
        }
        await leads.VincularContatoAsync(id, contatoId.Value, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/contatos/{contatoId:int}/remover")]
    public async Task<IActionResult> RemoverContato(int id, int contatoId)
    {
        await leads.DesvincularContatoAsync(id, contatoId, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/empresa")]
    public async Task<IActionResult> DefinirEmpresa(int id, int? empresaId, string? nome)
    {
        var lead = await leads.ObterAsync(id, Eu);
        if (empresaId is null && !string.IsNullOrWhiteSpace(nome))
        {
            var e = new Empresa { Nome = nome.Trim(), ResponsavelId = lead.ResponsavelId };
            db.Empresas.Add(e);
            await db.SaveChangesAsync();
            empresaId = e.Id;
        }
        lead.EmpresaId = empresaId;
        await db.SaveChangesAsync();
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/excluir")]
    public async Task<IActionResult> Excluir(int id)
    {
        var funil = (await leads.ObterAsync(id, Eu)).FunilId;
        await leads.ExcluirAsync(id, Eu);
        TempData["msg"] = "Lead excluído.";
        return Redirect($"/leads?funil={funil}");
    }

    /// <summary>Busca rápida usada nos campos de autocompletar (contatos e empresas).</summary>
    [HttpGet("/buscar/{tipo}")]
    public async Task<IActionResult> Buscar(string tipo, string q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Json(Array.Empty<object>());
        if (tipo == "contatos")
            return Json(await db.Contatos.Where(c => c.Nome.Contains(q) || (c.Celular != null && c.Celular.Contains(q)) || (c.Email != null && c.Email.Contains(q)))
                .OrderBy(c => c.Nome).Take(10).Select(c => new { id = c.Id, nome = c.Nome, extra = c.Celular ?? c.Email }).ToListAsync());
        return Json(await db.Empresas.Where(e => e.Nome.Contains(q) || (e.Cnpj != null && e.Cnpj.Contains(q)))
            .OrderBy(e => e.Nome).Take(10).Select(e => new { id = e.Id, nome = e.Nome, extra = e.Cnpj }).ToListAsync());
    }
}
