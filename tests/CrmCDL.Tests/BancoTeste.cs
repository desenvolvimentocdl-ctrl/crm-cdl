using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Tests;

/// <summary>
/// Banco SQLite em memória, novo a cada teste, com uma equipe e um funil prontos.
/// Usa o mesmo AppDbContext do sistema, então chaves, índices únicos e exclusões em cascata valem como no MySQL.
/// </summary>
public sealed class BancoTeste : IDisposable
{
    readonly SqliteConnection _conexao;
    public AppDbContext Db { get; }

    public UsuarioAtual Ana { get; }      // vendedora
    public UsuarioAtual Bruno { get; }    // vendedor
    public UsuarioAtual Gestora { get; }
    public Funil Funil { get; }
    public Funil OutroFunil { get; }
    public Etapa Entrada => Funil.Etapas.Single(e => e.Tipo == TipoEtapa.Entrada);
    public Etapa Contato => Funil.Etapas.Single(e => e.Nome == "Contato inicial");
    public Etapa Negociacao => Funil.Etapas.Single(e => e.Nome == "Negociação");
    public Etapa Ganho => Funil.Etapas.Single(e => e.Tipo == TipoEtapa.Ganho);
    public Etapa Perdido => Funil.Etapas.Single(e => e.Tipo == TipoEtapa.Perdido);
    public MotivoPerda Preco { get; }

    public BancoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        Db = NovoContexto();
        Db.Database.EnsureCreated();

        var ana = new Usuario { Nome = "Ana", Email = "ana@teste", Perfil = Perfil.Vendedor };
        var bruno = new Usuario { Nome = "Bruno", Email = "bruno@teste", Perfil = Perfil.Vendedor };
        var gestora = new Usuario { Nome = "Gestora", Email = "gestora@teste", Perfil = Perfil.Gestor };
        Db.Usuarios.AddRange(ana, bruno, gestora);

        Funil = NovoFunil("Associação", 1, principal: true);
        OutroFunil = NovoFunil("Eventos", 2, principal: false);
        Preco = new MotivoPerda { Nome = "Preço", Ordem = 1 };
        Db.Funis.AddRange(Funil, OutroFunil);
        Db.MotivosPerda.Add(Preco);
        Db.SaveChanges();

        Ana = new(ana.Id, ana.Nome, ana.Perfil);
        Bruno = new(bruno.Id, bruno.Nome, bruno.Perfil);
        Gestora = new(gestora.Id, gestora.Nome, gestora.Perfil);
    }

    static Funil NovoFunil(string nome, int ordem, bool principal) => new()
    {
        Nome = nome, Ordem = ordem, Principal = principal,
        Etapas =
        {
            new Etapa { Nome = "Leads de entrada", Ordem = 0, Tipo = TipoEtapa.Entrada },
            new Etapa { Nome = "Contato inicial", Ordem = 1 },
            new Etapa { Nome = "Negociação", Ordem = 2 },
            new Etapa { Nome = "Venda ganha", Ordem = 98, Tipo = TipoEtapa.Ganho },
            new Etapa { Nome = "Venda perdida", Ordem = 99, Tipo = TipoEtapa.Perdido },
        }
    };

    /// <summary>Outro contexto no mesmo banco, para conferir o que de fato foi gravado (sem o cache do contexto que salvou).</summary>
    public AppDbContext NovoContexto()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);

    public LeadService Leads() => new(Db);
    public NotificacaoService Notificacoes() => new(Db);

    public Task<Lead> CriarLeadAsync(string titulo, UsuarioAtual autor, int? responsavelId = null, int? etapaId = null, decimal valor = 0)
        => Leads().CriarAsync(new NovoLead(titulo, valor, Funil.Id, etapaId, responsavelId, null, null, null, null), autor);

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
