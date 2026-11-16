using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

/// <summary>Contatos e empresas.</summary>
public class CadastrosController(AppDbContext db, ConfiguracaoService config, LeadService leads) : BaseController
{
    const int PorPagina = 50;

    // ---------------- contatos ----------------

    [HttpGet("/contatos")]
    public async Task<IActionResult> Contatos(string? busca, int pagina = 1)
    {
        var q = db.Contatos.AsQueryable();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim();
            q = q.Where(c => c.Nome.Contains(b) || (c.Email != null && c.Email.Contains(b)) || (c.Celular != null && c.Celular.Contains(b))
                             || (c.Telefone != null && c.Telefone.Contains(b)) || (c.Cpf != null && c.Cpf.Contains(b)) || (c.Empresa != null && c.Empresa.Nome.Contains(b)));
        }
        ViewBag.Total = await q.CountAsync();
        ViewBag.Pagina = Math.Max(1, pagina);
        ViewBag.PorPagina = PorPagina;
        ViewBag.Busca = busca;
        var lista = await q.Include(c => c.Empresa).Include(c => c.Responsavel).OrderBy(c => c.Nome)
            .Skip((Math.Max(1, pagina) - 1) * PorPagina).Take(PorPagina).ToListAsync();
        return View(lista);
    }

    [HttpPost("/contatos/novo")]
    public async Task<IActionResult> NovoContato(string nome, string? celular, string? email, string? empresa)
    {
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do contato.");
        var c = new Contato { Nome = nome.Trim(), Celular = celular?.Trim(), Email = email?.Trim(), ResponsavelId = Eu.Id };
        if (!string.IsNullOrWhiteSpace(empresa))
            c.Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nome == empresa.Trim()) ?? new Empresa { Nome = empresa.Trim(), ResponsavelId = Eu.Id };
        db.Contatos.Add(c);
        await db.SaveChangesAsync();
        return Redirect($"/contatos/{c.Id}");
    }

    [HttpGet("/contatos/{id:int}")]
    public async Task<IActionResult> Contato(int id)
    {
        var c = await db.Contatos.Include(x => x.Empresa).Include(x => x.Responsavel).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        ViewBag.Campos = await CamposAsync(EntidadeCampo.Contato);
        ViewBag.Valores = await config.ValoresAsync(EntidadeCampo.Contato, id);
        ViewBag.Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync();
        ViewBag.Leads = await leads.Visiveis(Eu).Where(l => l.Contatos.Any(x => x.ContatoId == id))
            .Include(l => l.Etapa).Include(l => l.Funil).OrderByDescending(l => l.AtualizadoEm).ToListAsync();
        return View(c);
    }

    [HttpPost("/contatos/{id:int}")]
    public async Task<IActionResult> SalvarContato(int id, Contato dados, string? empresaNome)
    {
        var c = await db.Contatos.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dados.Nome)) return VoltarCom("Informe o nome do contato.");
        c.Nome = dados.Nome.Trim(); c.Cargo = dados.Cargo; c.Telefone = dados.Telefone; c.Celular = dados.Celular;
        c.Email = dados.Email; c.Cpf = dados.Cpf; c.AceitouTermos = dados.AceitouTermos; c.OptInMarketing = dados.OptInMarketing;
        c.ResponsavelId = dados.ResponsavelId;
        if (string.IsNullOrWhiteSpace(empresaNome)) c.EmpresaId = null;
        else
        {
            var nome = empresaNome.Trim();
            c.Empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Nome == nome) ?? new Empresa { Nome = nome, ResponsavelId = c.ResponsavelId };
        }
        await db.SaveChangesAsync();
        await config.SalvarValoresAsync(EntidadeCampo.Contato, id, Texto.CamposDoForm(Request.Form));
        return VoltarCom("Contato salvo.", $"/contatos/{id}");
    }

    [HttpPost("/contatos/{id:int}/excluir")]
    public async Task<IActionResult> ExcluirContato(int id)
    {
        if (!Eu.VeTudo) return Forbid();
        var c = await db.Contatos.FindAsync(id);
        if (c is not null)
        {
            db.ValoresCampo.RemoveRange(db.ValoresCampo.Where(v => v.Entidade == EntidadeCampo.Contato && v.RegistroId == id));
            db.Contatos.Remove(c);
            await db.SaveChangesAsync();
        }
        TempData["msg"] = "Contato excluído.";
        return Redirect("/contatos");
    }

    // ---------------- empresas ----------------

    [HttpGet("/empresas")]
    public async Task<IActionResult> Empresas(string? busca, int pagina = 1)
    {
        var q = db.Empresas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim();
            q = q.Where(e => e.Nome.Contains(b) || (e.Cnpj != null && e.Cnpj.Contains(b)) || (e.Email != null && e.Email.Contains(b)));
        }
        ViewBag.Total = await q.CountAsync();
        ViewBag.Pagina = Math.Max(1, pagina);
        ViewBag.PorPagina = PorPagina;
        ViewBag.Busca = busca;
        var lista = await q.OrderBy(e => e.Nome).Skip((Math.Max(1, pagina) - 1) * PorPagina).Take(PorPagina)
            .Select(e => new EmpresaLinha(e.Id, e.Nome, e.Cnpj, e.Telefone, e.Email, e.Contatos.Count, e.Leads.Count)).ToListAsync();
        return View(lista);
    }

    public record EmpresaLinha(int Id, string Nome, string? Cnpj, string? Telefone, string? Email, int Contatos, int Leads);

    [HttpPost("/empresas/nova")]
    public async Task<IActionResult> NovaEmpresa(string nome, string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome da empresa.");
        var e = new Empresa { Nome = nome.Trim(), Cnpj = cnpj?.Trim(), ResponsavelId = Eu.Id };
        db.Empresas.Add(e);
        await db.SaveChangesAsync();
        return Redirect($"/empresas/{e.Id}");
    }

    [HttpGet("/empresas/{id:int}")]
    public async Task<IActionResult> Empresa(int id)
    {
        var e = await db.Empresas.Include(x => x.Contatos).Include(x => x.Responsavel).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        ViewBag.Campos = await CamposAsync(EntidadeCampo.Empresa);
        ViewBag.Valores = await config.ValoresAsync(EntidadeCampo.Empresa, id);
        ViewBag.Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync();
        ViewBag.Leads = await leads.Visiveis(Eu).Where(l => l.EmpresaId == id)
            .Include(l => l.Etapa).Include(l => l.Funil).OrderByDescending(l => l.AtualizadoEm).ToListAsync();
        return View(e);
    }

    [HttpPost("/empresas/{id:int}")]
    public async Task<IActionResult> SalvarEmpresa(int id, Empresa dados)
    {
        var e = await db.Empresas.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dados.Nome)) return VoltarCom("Informe o nome da empresa.");
        e.Nome = dados.Nome.Trim(); e.Cnpj = dados.Cnpj; e.Telefone = dados.Telefone; e.Email = dados.Email;
        e.Site = dados.Site; e.Endereco = dados.Endereco; e.ResponsavelId = dados.ResponsavelId;
        await db.SaveChangesAsync();
        await config.SalvarValoresAsync(EntidadeCampo.Empresa, id, Texto.CamposDoForm(Request.Form));
        return VoltarCom("Empresa salva.", $"/empresas/{id}");
    }

    [HttpPost("/empresas/{id:int}/excluir")]
    public async Task<IActionResult> ExcluirEmpresa(int id)
    {
        if (!Eu.VeTudo) return Forbid();
        var e = await db.Empresas.FindAsync(id);
        if (e is not null)
        {
            db.ValoresCampo.RemoveRange(db.ValoresCampo.Where(v => v.Entidade == EntidadeCampo.Empresa && v.RegistroId == id));
            db.Empresas.Remove(e);
            await db.SaveChangesAsync();
        }
        TempData["msg"] = "Empresa excluída.";
        return Redirect("/empresas");
    }

    Task<List<CampoPersonalizado>> CamposAsync(EntidadeCampo ent)
        => db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == ent && c.Ativo).OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync();
}
