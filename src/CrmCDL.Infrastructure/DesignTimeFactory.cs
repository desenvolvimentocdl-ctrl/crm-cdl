using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrmCDL.Infrastructure;

/// <summary>Usado só pelo "dotnet ef" para gerar migrações (não abre conexão).</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var o = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=localhost;Database=CrmCDL;User=crm;Password=x", new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AppDbContext(o);
    }
}
