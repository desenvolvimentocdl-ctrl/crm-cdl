using CrmCDL.Application;
using CrmCDL.Infrastructure.Kommo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrmCDL.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration config)
    {
        var conexao = config.GetConnectionString("CrmCDL") is { Length: > 0 } c ? c : null;
        conexao = conexao
                      ?? throw new InvalidOperationException("Defina ConnectionStrings:CrmCDL (variável de ambiente ConnectionStrings__CrmCDL).");

        // Versão fixa para não precisar abrir conexão só para descobrir o servidor (o site usa MySQL 8).
        services.AddDbContext<AppDbContext>(o => o.UseMySql(conexao, new MySqlServerVersion(new Version(8, 0, 36)),
            m => m.EnableRetryOnFailure(3)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<KommoOptions>(config.GetSection("Kommo"));
        services.AddHttpClient<IImportadorKommo, ImportadorKommo>(c => c.Timeout = TimeSpan.FromSeconds(60));

        services.AddScoped<LeadService>();
        services.AddScoped<ConfiguracaoService>();
        services.AddScoped<NotificacaoService>();
        return services;
    }
}
