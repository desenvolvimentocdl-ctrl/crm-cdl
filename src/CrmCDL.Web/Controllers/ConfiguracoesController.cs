using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

[Authorize(Policy = Politicas.Admin)]
public class ConfiguracoesController(AppDbContext db, ConfiguracaoService config, IPasswordHasher<Usuario> hasher, IImportadorKommo kommo) : BaseController
{
    [HttpGet("/configuracoes")]
    public IActionResult Inicio() => Redirect("/configuracoes/funis");

    // ---------------- funis e etapas ----------------

    [HttpGet("/configuracoes/funis")]
    public async Task<IActionResult> Funis()
    {
        ViewBag.Contagem = await db.Leads.GroupBy(l => l.FunilId).Select(g => new { g.Key, Qtd = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Qtd);
        return View(await db.Funis.Include(f => f.Etapas).OrderBy(f => f.Ordem).ToListAsync());
    }

    [HttpPost("/configuracoes/funis/novo")]
    public async Task<IActionResult> NovoFunil(string nome)
    {
        var f = await config.CriarFunilAsync(nome);
        return Redirect($"/configuracoes/funis/{f.Id}");
    }

    [HttpGet("/configuracoes/funis/{id:int}")]
    public async Task<IActionResult> Funil(int id)
    {
        var f = await db.Funis.Include(x => x.Etapas).FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound();
        ViewBag.Contagem = await db.Leads.Where(l => l.FunilId == id).GroupBy(l => l.EtapaId).Select(g => new { g.Key, Qtd = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Qtd);
        return View(f);
    }

    [HttpPost("/configuracoes/funis/{id:int}")]
    public async Task<IActionResult> SalvarFunil(int id, string nome, bool principal, List<EtapaEdicao> etapas, string? nomeGanho, string? nomePerdido)
    {
        await config.SalvarFunilAsync(id, nome, principal, etapas ?? new(), nomeGanho, nomePerdido);
        return VoltarCom("Funil salvo.", $"/configuracoes/funis/{id}");
    }

    [HttpPost("/configuracoes/funis/{id:int}/ativo")]
    public async Task<IActionResult> AlternarFunil(int id)
    {
        var f = await db.Funis.FindAsync(id);
        if (f is null) return NotFound();
        f.Ativo = !f.Ativo;
        await db.SaveChangesAsync();
        return VoltarCom(f.Ativo ? "Funil reativado." : "Funil arquivado (some do quadro, mas os leads continuam salvos).");
    }

    [HttpPost("/configuracoes/funis/{id:int}/mover")]
    public async Task<IActionResult> MoverFunil(int id, int direcao)
    {
        var lista = await db.Funis.OrderBy(f => f.Ordem).ToListAsync();
        var i = lista.FindIndex(f => f.Id == id);
        var j = i + Math.Sign(direcao);
        if (i >= 0 && j >= 0 && j < lista.Count) (lista[i], lista[j]) = (lista[j], lista[i]);
        for (var k = 0; k < lista.Count; k++) lista[k].Ordem = k + 1;
        await db.SaveChangesAsync();
        return Redirect("/configuracoes/funis");
    }

    [HttpPost("/configuracoes/funis/{id:int}/excluir")]
    public async Task<IActionResult> ExcluirFunil(int id)
    {
        await config.ExcluirFunilAsync(id);
        return VoltarCom("Funil excluído.", "/configuracoes/funis");
    }

    // ---------------- campos personalizados ----------------

    [HttpGet("/configuracoes/campos")]
    public async Task<IActionResult> Campos(EntidadeCampo entidade = EntidadeCampo.Lead)
    {
        ViewBag.Entidade = entidade;
        return View(await db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == entidade).OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync());
    }

    [HttpPost("/configuracoes/campos/novo")]
    public async Task<IActionResult> NovoCampo(EntidadeCampo entidade, string nome, TipoCampo tipo)
    {
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do campo.");
        var ordem = (await db.Campos.Where(c => c.Entidade == entidade).MaxAsync(c => (int?)c.Ordem) ?? 0) + 1;
        var c = new CampoPersonalizado { Entidade = entidade, Nome = nome.Trim(), Tipo = tipo, Ordem = ordem };
        db.Campos.Add(c);
        await db.SaveChangesAsync();
        return Redirect($"/configuracoes/campos/{c.Id}");
    }

    [HttpGet("/configuracoes/campos/{id:int}")]
    public async Task<IActionResult> Campo(int id)
    {
        var c = await db.Campos.Include(x => x.Opcoes).FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : View(c);
    }

    /// <summary>Opções chegam uma por linha. Opções existentes são mantidas pelo texto, para não perder valores já gravados nos leads.</summary>
    [HttpPost("/configuracoes/campos/{id:int}")]
    public async Task<IActionResult> SalvarCampo(int id, string nome, int ordem, bool ativo, string? opcoes)
    {
        var c = await db.Campos.Include(x => x.Opcoes).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do campo.");
        c.Nome = nome.Trim(); c.Ordem = ordem; c.Ativo = ativo;
        if (c.Tipo is TipoCampo.Lista or TipoCampo.MultiplaEscolha)
        {
            var linhas = (opcoes ?? "").Split('\n').Select(l => l.Trim()).Where(l => l != "").Distinct().ToList();
            var usadas = await db.ValoresCampo.Where(v => v.CampoId == id).Select(v => v.Valor).ToListAsync();
            foreach (var op in c.Opcoes.Where(o => !linhas.Contains(o.Valor)).ToList())
            {
                if (usadas.Any(u => u.Split(',').Contains(op.Id.ToString())))
                    return VoltarCom($"A opção \"{op.Valor}\" já está em uso e não pode ser removida. Desative o campo ou mantenha a opção.");
                db.CampoOpcoes.Remove(op);
            }
            for (var i = 0; i < linhas.Count; i++)
            {
                var op = c.Opcoes.FirstOrDefault(o => o.Valor == linhas[i]);
                if (op is null) c.Opcoes.Add(op = new CampoOpcao { Valor = linhas[i] });
                op.Ordem = i;
            }
        }
        await db.SaveChangesAsync();
        return VoltarCom("Campo salvo.", $"/configuracoes/campos?entidade={c.Entidade}");
    }

    [HttpPost("/configuracoes/campos/{id:int}/excluir")]
    public async Task<IActionResult> ExcluirCampo(int id)
    {
        var c = await db.Campos.FindAsync(id);
        if (c is null) return NotFound();
        db.Campos.Remove(c);
        await db.SaveChangesAsync();
        return VoltarCom("Campo excluído (os valores gravados nele também).", $"/configuracoes/campos?entidade={c.Entidade}");
    }

    // ---------------- usuários ----------------

    [HttpGet("/configuracoes/usuarios")]
    public async Task<IActionResult> Usuarios() => View(await db.Usuarios.OrderByDescending(u => u.Ativo).ThenBy(u => u.Nome).ToListAsync());

    [HttpPost("/configuracoes/usuarios/novo")]
    public async Task<IActionResult> NovoUsuario(string nome, string email, Perfil perfil, string senha)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(nome) || !email.Contains('@')) return VoltarCom("Informe nome e e-mail válidos.");
        if ((senha ?? "").Length < 8) return VoltarCom("A senha inicial precisa ter pelo menos 8 caracteres.");
        if (await db.Usuarios.AnyAsync(u => u.Email == email)) return VoltarCom("Já existe um usuário com este e-mail.");
        var u = new Usuario { Nome = nome.Trim(), Email = email, Perfil = perfil };
        u.SenhaHash = hasher.HashPassword(u, senha!);
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return VoltarCom($"Usuário {u.Nome} criado. Passe a senha inicial para a pessoa e peça para trocar em Minha conta.");
    }

    [HttpPost("/configuracoes/usuarios/{id:int}")]
    public async Task<IActionResult> SalvarUsuario(int id, string nome, Perfil perfil, bool ativo, string? novaSenha)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return NotFound();
        if (u.Id == Eu.Id && (!ativo || perfil != Perfil.Administrador)) return VoltarCom("Você não pode desativar nem rebaixar o próprio acesso.");
        if (!string.IsNullOrWhiteSpace(nome)) u.Nome = nome.Trim();
        u.Perfil = perfil; u.Ativo = ativo;
        if (!string.IsNullOrWhiteSpace(novaSenha))
        {
            if (novaSenha.Length < 8) return VoltarCom("A nova senha precisa ter pelo menos 8 caracteres.");
            u.SenhaHash = hasher.HashPassword(u, novaSenha);
        }
        await db.SaveChangesAsync();
        return VoltarCom("Usuário salvo.");
    }

    // ---------------- motivos de perda ----------------

    [HttpGet("/configuracoes/motivos")]
    public async Task<IActionResult> Motivos() => View(await db.MotivosPerda.OrderBy(m => m.Ordem).ThenBy(m => m.Nome).ToListAsync());

    [HttpPost("/configuracoes/motivos")]
    public async Task<IActionResult> SalvarMotivo(int? id, string nome, bool excluir = false)
    {
        var m = id is int i ? await db.MotivosPerda.FindAsync(i) : null;
        if (excluir && m is not null) db.MotivosPerda.Remove(m);
        else if (!string.IsNullOrWhiteSpace(nome))
        {
            if (m is null) db.MotivosPerda.Add(m = new MotivoPerda { Ordem = (await db.MotivosPerda.MaxAsync(x => (int?)x.Ordem) ?? 0) + 1 });
            m.Nome = nome.Trim();
        }
        await db.SaveChangesAsync();
        return Redirect("/configuracoes/motivos");
    }

    // ---------------- importação da Kommo ----------------

    [HttpGet("/configuracoes/kommo")]
    public IActionResult Kommo() => View(kommo.Configurado);

    [HttpPost("/configuracoes/kommo")]
    public async Task<IActionResult> ImportarKommo(CancellationToken ct)
    {
        var r = await kommo.ImportarEstruturaAsync(ct);
        TempData["kommo"] = System.Text.Json.JsonSerializer.Serialize(r);
        return Redirect("/configuracoes/kommo");
    }
}
