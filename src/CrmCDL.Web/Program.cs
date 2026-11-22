using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddHostedService<LembreteTarefas>();
builder.Services.Configure<AssistenteOptions>(builder.Configuration.GetSection("Assistente"));
builder.Services.AddScoped<Assistente>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/entrar";
        o.AccessDeniedPath = "/sem-acesso";
        o.Cookie.Name = "crmcdl";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Politicas.Gestor, p => p.RequireRole(nameof(Perfil.Gestor), nameof(Perfil.Administrador)));
    o.AddPolicy(Politicas.Admin, p => p.RequireRole(nameof(Perfil.Administrador)));
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// Formulários do site: no máximo 20 envios por minuto por endereço IP (proteção contra spam).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("captura", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    // Assistente de IA: até 15 perguntas por minuto por usuário (controla o custo da API).
    o.AddPolicy("assistente", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? "?",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF");
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<RegraNegocioFilter>();
});

var app = builder.Build();

// Cria/atualiza as tabelas do banco do CRM e o primeiro administrador.
await Inicializacao.ExecutarAsync(app.Services, app.Configuration, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
