using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
