using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Application;

public interface IAppDbContext
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<Funil> Funis { get; }
    DbSet<Etapa> Etapas { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Contato> Contatos { get; }
    DbSet<Empresa> Empresas { get; }
    DbSet<LeadContato> LeadContatos { get; }
    DbSet<Tag> Tags { get; }
    DbSet<LeadTag> LeadTags { get; }
    DbSet<MotivoPerda> MotivosPerda { get; }
    DbSet<Tarefa> Tarefas { get; }
    DbSet<Atividade> Atividades { get; }
    DbSet<CampoPersonalizado> Campos { get; }
    DbSet<CampoOpcao> CampoOpcoes { get; }
    DbSet<ValorCampo> ValoresCampo { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Quem está usando o sistema agora.</summary>
public record UsuarioAtual(int Id, string Nome, Perfil Perfil)
{
    public bool VeTudo => Perfil >= Perfil.Gestor;
    public bool Admin => Perfil == Perfil.Administrador;
}

public class RegraNegocioException(string mensagem) : Exception(mensagem);
