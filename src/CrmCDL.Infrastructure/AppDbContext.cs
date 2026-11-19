using CrmCDL.Application;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Funil> Funis => Set<Funil>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Contato> Contatos => Set<Contato>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<LeadContato> LeadContatos => Set<LeadContato>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LeadTag> LeadTags => Set<LeadTag>();
    public DbSet<MotivoPerda> MotivosPerda => Set<MotivoPerda>();
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<Atividade> Atividades => Set<Atividade>();
    public DbSet<CampoPersonalizado> Campos => Set<CampoPersonalizado>();
    public DbSet<CampoOpcao> CampoOpcoes => Set<CampoOpcao>();
    public DbSet<ValorCampo> ValoresCampo => Set<ValorCampo>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(180);
            e.Property(x => x.SenhaHash).HasMaxLength(300);
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Funil>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120);
            e.HasMany(x => x.Etapas).WithOne(x => x.Funil!).HasForeignKey(x => x.FunilId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<Etapa>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120);
            e.Property(x => x.Cor).HasMaxLength(9);
            e.Ignore(x => x.Fechada);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<Lead>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(250);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.HasOne(x => x.Funil).WithMany().HasForeignKey(x => x.FunilId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Etapa).WithMany().HasForeignKey(x => x.EtapaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Empresa).WithMany(x => x.Leads).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.MotivoPerda).WithMany().HasForeignKey(x => x.MotivoPerdaId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FunilId, x.EtapaId, x.Posicao });
            e.HasIndex(x => x.ResponsavelId);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<Contato>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(180);
            e.Property(x => x.Cargo).HasMaxLength(120);
            e.Property(x => x.Telefone).HasMaxLength(40);
            e.Property(x => x.Celular).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(180);
            e.Property(x => x.Cpf).HasMaxLength(20);
            e.HasOne(x => x.Empresa).WithMany(x => x.Contatos).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Nome);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<Empresa>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(180);
            e.Property(x => x.Cnpj).HasMaxLength(20);
            e.Property(x => x.Telefone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(180);
            e.Property(x => x.Site).HasMaxLength(250);
            e.Property(x => x.Endereco).HasMaxLength(400);
            e.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Nome);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<LeadContato>(e =>
        {
            e.HasKey(x => new { x.LeadId, x.ContatoId });
            e.HasOne(x => x.Lead).WithMany(x => x.Contatos).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Contato).WithMany(x => x.Leads).HasForeignKey(x => x.ContatoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Tag>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(80);
            e.Property(x => x.Cor).HasMaxLength(9);
            e.HasIndex(x => x.Nome).IsUnique();
        });

        b.Entity<LeadTag>(e =>
        {
            e.HasKey(x => new { x.LeadId, x.TagId });
            e.HasOne(x => x.Lead).WithMany(x => x.Tags).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MotivoPerda>(e => e.Property(x => x.Nome).HasMaxLength(160));

        b.Entity<Tarefa>(e =>
        {
            e.Property(x => x.Texto).HasMaxLength(1000);
            e.Property(x => x.Resultado).HasMaxLength(1000);
            e.HasOne(x => x.Lead).WithMany(x => x.Tarefas).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ResponsavelId, x.Concluida, x.Prazo });
        });

        b.Entity<Atividade>(e =>
        {
            e.Property(x => x.Texto).HasColumnType("text");
            e.HasOne(x => x.Lead).WithMany(x => x.Atividades).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.LeadId, x.CriadoEm });
        });

        b.Entity<CampoPersonalizado>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(200);
            e.HasMany(x => x.Opcoes).WithOne(x => x.Campo!).HasForeignKey(x => x.CampoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<CampoOpcao>(e =>
        {
            e.Property(x => x.Valor).HasMaxLength(250);
            e.HasIndex(x => x.KommoId);
        });

        b.Entity<ValorCampo>(e =>
        {
            e.Property(x => x.Valor).HasColumnType("text");
            e.HasOne(x => x.Campo).WithMany().HasForeignKey(x => x.CampoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Entidade, x.RegistroId, x.CampoId }).IsUnique();
        });

        b.Entity<Notificacao>(e =>
        {
            e.Property(x => x.Texto).HasMaxLength(500);
            e.Property(x => x.Chave).HasMaxLength(80);
            e.Ignore(x => x.Link);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UsuarioId, x.Lida, x.CriadoEm });
            e.HasIndex(x => new { x.UsuarioId, x.Chave }).IsUnique();
        });
    }
}
