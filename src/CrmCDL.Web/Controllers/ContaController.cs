using System.Security.Claims;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

public class ContaController(AppDbContext db, IPasswordHasher<Usuario> hasher) : BaseController
{
    [AllowAnonymous, HttpGet("/entrar")]
    public IActionResult Entrar(string? returnUrl) => View(model: returnUrl);

    [AllowAnonymous, HttpPost("/entrar")]
    public async Task<IActionResult> Entrar(string email, string senha, string? returnUrl)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == e && x.Ativo);
        var ok = u is not null && hasher.VerifyHashedPassword(u, u.SenhaHash, senha ?? "") != PasswordVerificationResult.Failed;
        if (!ok)
        {
            await Task.Delay(400); // dificulta tentativa e erro
            ViewBag.Erro = "E-mail ou senha incorretos.";
            ViewBag.Email = email;
            return View(model: returnUrl);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u!.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Perfil.ToString())
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpPost("/sair")]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/entrar");
    }

    [HttpGet("/minha-conta")]
    public IActionResult MinhaConta() => View();

    [HttpPost("/minha-conta/senha")]
    public async Task<IActionResult> TrocarSenha(string atual, string nova, string confirmacao)
    {
        var u = await db.Usuarios.FirstAsync(x => x.Id == Eu.Id);
        if (hasher.VerifyHashedPassword(u, u.SenhaHash, atual ?? "") == PasswordVerificationResult.Failed)
            return VoltarCom("A senha atual não confere.");
        if ((nova ?? "").Length < 8) return VoltarCom("A nova senha precisa ter pelo menos 8 caracteres.");
        if (nova != confirmacao) return VoltarCom("A confirmação não confere com a nova senha.");
        u.SenhaHash = hasher.HashPassword(u, nova!);
        await db.SaveChangesAsync();
        return VoltarCom("Senha alterada.");
    }

    [AllowAnonymous, HttpGet("/sem-acesso")]
    public IActionResult SemAcesso() => View("Mensagem", "Seu perfil não tem acesso a esta página.");

    [AllowAnonymous, HttpGet("/erro")]
    public IActionResult Erro() => View("Mensagem", "Algo deu errado. Tente de novo em instantes.");
}
