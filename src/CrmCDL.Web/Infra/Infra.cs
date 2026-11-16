using System.Globalization;
using System.Security.Claims;
using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Infra;

public static class Politicas
{
    public const string Gestor = "Gestor";
    public const string Admin = "Admin";
}

public abstract class BaseController : Controller
{
    protected UsuarioAtual Eu => new(
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        User.Identity!.Name!,
        Enum.Parse<Perfil>(User.FindFirstValue(ClaimTypes.Role)!));

    protected IActionResult VoltarCom(string mensagem, string? url = null)
    {
        TempData["msg"] = mensagem;
        var referer = Request.Headers.Referer.ToString();
        return Redirect(url ?? (referer.Length > 0 ? referer : "/"));
    }
}

/// <summary>Erros de regra de negócio viram mensagem amigável (JSON para chamadas do quadro, aviso no topo para formulários).</summary>
public class RegraNegocioFilter : IExceptionFilter
{
    public void OnException(ExceptionContext ctx)
    {
        if (ctx.Exception is not RegraNegocioException e) return;
        var req = ctx.HttpContext.Request;
        if (req.Headers.Accept.ToString().Contains("application/json"))
            ctx.Result = new BadRequestObjectResult(new { erro = e.Message });
        else
        {
            var tempData = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>().GetTempData(ctx.HttpContext);
            tempData["erro"] = e.Message;
            var volta = req.Headers.Referer.ToString();
            ctx.Result = new RedirectResult(string.IsNullOrEmpty(volta) ? "/" : volta);
        }
        ctx.ExceptionHandled = true;
    }
}

/// <summary>Datas são gravadas em UTC e mostradas no horário de Brasília.</summary>
public static class Hora
{
    static readonly TimeZoneInfo Br = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Br);
    public static DateTime ParaUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Br);
    public static DateTime HojeLocal => Local(DateTime.UtcNow).Date;

    public static string Relativa(DateTime utc)
    {
        var d = DateTime.UtcNow - utc;
        if (d.TotalMinutes < 1) return "agora";
        if (d.TotalMinutes < 60) return $"há {(int)d.TotalMinutes} min";
        if (d.TotalHours < 24) return $"há {(int)d.TotalHours} h";
        if (d.TotalDays < 7) return $"há {(int)d.TotalDays} d";
        return Local(utc).ToString("dd/MM/yyyy", PtBr);
    }

    public static string Prazo(DateTime utc)
    {
        var l = Local(utc);
        var hoje = HojeLocal;
        if (l.Date == hoje) return "Hoje " + l.ToString("HH:mm");
        if (l.Date == hoje.AddDays(1)) return "Amanhã " + l.ToString("HH:mm");
        if (l.Date == hoje.AddDays(-1)) return "Ontem " + l.ToString("HH:mm");
        return l.ToString("dd/MM HH:mm", PtBr);
    }

    public static string Dinheiro(decimal v) => v.ToString("C0", PtBr);
}

public static class Texto
{
    public static string Iniciais(string? nome)
    {
        var p = (nome ?? "?").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return p.Length == 0 ? "?" : p.Length == 1 ? p[0][..1].ToUpper() : (p[0][..1] + p[^1][..1]).ToUpper();
    }

    public static string NomePerfil(Perfil p) => p switch
    {
        Perfil.Administrador => "Administrador",
        Perfil.Gestor => "Gestor",
        _ => "Vendedor(a)"
    };

    public static string NomeTarefa(TipoTarefa t) => t switch
    {
        TipoTarefa.Ligacao => "Ligação",
        TipoTarefa.Reuniao => "Reunião",
        TipoTarefa.Email => "E-mail",
        TipoTarefa.Visita => "Visita",
        _ => "Retorno"
    };

    public static string NomeTipoCampo(TipoCampo t) => t switch
    {
        TipoCampo.TextoLongo => "Texto longo",
        TipoCampo.Numero => "Número",
        TipoCampo.Data => "Data",
        TipoCampo.DataHora => "Data e hora",
        TipoCampo.Lista => "Lista (uma opção)",
        TipoCampo.MultiplaEscolha => "Múltipla escolha",
        TipoCampo.Url => "Link",
        TipoCampo.Checkbox => "Sim/Não",
        TipoCampo.Rastreamento => "Rastreamento (UTM)",
        _ => "Texto"
    };

    /// <summary>Lê os campos personalizados enviados como campo[ID] (múltipla escolha chega com vários valores).</summary>
    public static Dictionary<int, string?> CamposDoForm(IFormCollection form)
    {
        var r = new Dictionary<int, string?>();
        foreach (var k in form.Keys.Where(k => k.StartsWith("campo[")))
            if (int.TryParse(k[6..^1], out var id))
                r[id] = string.Join(",", form[k].Where(v => !string.IsNullOrWhiteSpace(v)));
        foreach (var k in form.Keys.Where(k => k.StartsWith("campos_presentes[")))
            if (int.TryParse(k[17..^1], out var id) && !r.ContainsKey(id)) r[id] = "";
        return r;
    }
}

public static class Inicializacao
{
    public static async Task ExecutarAsync(IServiceProvider sp, IConfiguration config, ILogger log)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Usuarios.AnyAsync())
        {
            var email = config["Admin:Email"];
            var senha = config["Admin:Senha"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                log.LogWarning("Nenhum usuário cadastrado. Defina Admin__Email e Admin__Senha para criar o primeiro administrador.");
                return;
            }
            var admin = new Usuario { Nome = config["Admin:Nome"] ?? "Administrador", Email = email.Trim().ToLowerInvariant(), Perfil = Perfil.Administrador };
            admin.SenhaHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(admin, senha);
            db.Usuarios.Add(admin);
            await db.SaveChangesAsync();
            log.LogInformation("Administrador inicial criado: {Email}", admin.Email);
        }
    }
}
