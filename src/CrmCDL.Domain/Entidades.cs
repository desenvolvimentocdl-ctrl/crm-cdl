namespace CrmCDL.Domain;

/// <summary>Perfis de acesso. Vendedor vê só os próprios leads; Gestor vê tudo e os relatórios; Administrador também configura o sistema.</summary>
public enum Perfil { Vendedor = 0, Gestor = 1, Administrador = 2 }

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public Perfil Perfil { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public long? KommoId { get; set; }
}

public class Funil
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int Ordem { get; set; }
    public bool Principal { get; set; }
    public bool Ativo { get; set; } = true;
    public long? KommoId { get; set; }
    public List<Etapa> Etapas { get; set; } = new();
}

/// <summary>Entrada = leads que chegaram e ainda não foram triados; Ganho e Perdido fecham o lead.</summary>
public enum TipoEtapa { Normal = 0, Entrada = 1, Ganho = 2, Perdido = 3 }

public class Etapa
{
    public int Id { get; set; }
    public int FunilId { get; set; }
    public Funil? Funil { get; set; }
    public string Nome { get; set; } = "";
    public string Cor { get; set; } = "#99ccff";
    public int Ordem { get; set; }
    public TipoEtapa Tipo { get; set; }
    public long? KommoId { get; set; }

    public bool Fechada => Tipo is TipoEtapa.Ganho or TipoEtapa.Perdido;
}

public class Lead
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public decimal Valor { get; set; }
    public int FunilId { get; set; }
    public Funil? Funil { get; set; }
    public int EtapaId { get; set; }
    public Etapa? Etapa { get; set; }
    public int? ResponsavelId { get; set; }
    public Usuario? Responsavel { get; set; }
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public int? MotivoPerdaId { get; set; }
    public MotivoPerda? MotivoPerda { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? FechadoEm { get; set; }
    /// <summary>Posição do cartão dentro da coluna (menor = mais acima).</summary>
    public double Posicao { get; set; }
    public long? KommoId { get; set; }

    public List<LeadContato> Contatos { get; set; } = new();
    public List<LeadTag> Tags { get; set; } = new();
    public List<Tarefa> Tarefas { get; set; } = new();
    public List<Atividade> Atividades { get; set; } = new();
}

public class Contato
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Cargo { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? Cpf { get; set; }
    public bool AceitouTermos { get; set; }
    public bool OptInMarketing { get; set; }
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public int? ResponsavelId { get; set; }
    public Usuario? Responsavel { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public long? KommoId { get; set; }
    public List<LeadContato> Leads { get; set; } = new();
}

public class Empresa
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Cnpj { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Site { get; set; }
    public string? Endereco { get; set; }
    public int? ResponsavelId { get; set; }
    public Usuario? Responsavel { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public long? KommoId { get; set; }
    public List<Contato> Contatos { get; set; } = new();
    public List<Lead> Leads { get; set; } = new();
}

public class LeadContato
{
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int ContatoId { get; set; }
    public Contato? Contato { get; set; }
    public bool Principal { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Cor { get; set; } = "#c7d8ff";
    public long? KommoId { get; set; }
}

public class LeadTag
{
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}

public class MotivoPerda
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int Ordem { get; set; }
    public long? KommoId { get; set; }
}

public enum TipoTarefa { Retorno = 0, Ligacao = 1, Reuniao = 2, Email = 3, Visita = 4 }

public class Tarefa
{
    public int Id { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public TipoTarefa Tipo { get; set; }
    public string Texto { get; set; } = "";
    public DateTime Prazo { get; set; }
    public int? ResponsavelId { get; set; }
    public Usuario? Responsavel { get; set; }
    public bool Concluida { get; set; }
    public DateTime? ConcluidaEm { get; set; }
    public string? Resultado { get; set; }
    public int? CriadoPorId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public enum TipoAtividade { Nota = 0, Criacao = 1, MudancaEtapa = 2, MudancaResponsavel = 3, TarefaCriada = 4, TarefaConcluida = 5, CampoAlterado = 6 }

/// <summary>Linha do tempo do lead: notas escritas pela equipe e eventos registrados pelo sistema.</summary>
public class Atividade
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }
    public TipoAtividade Tipo { get; set; }
    public string Texto { get; set; } = "";
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public enum TipoNotificacao { LeadAtribuido = 0, TarefaAtribuida = 1, TarefaHoje = 2, TarefaAtrasada = 3 }

/// <summary>Aviso para um usuário (sino no menu). Chave evita repetir o mesmo lembrete automático.</summary>
public class Notificacao
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public TipoNotificacao Tipo { get; set; }
    public string Texto { get; set; } = "";
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public string? Chave { get; set; }
    public bool Lida { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public string Link => LeadId is int id ? $"/leads/{id}" : "/tarefas";
}

public enum EntidadeCampo { Lead = 0, Contato = 1, Empresa = 2 }

public enum TipoCampo { Texto = 0, TextoLongo = 1, Numero = 2, Data = 3, DataHora = 4, Lista = 5, MultiplaEscolha = 6, Url = 7, Checkbox = 8, Rastreamento = 9 }

/// <summary>Campos que a equipe cria sem programar (ex.: Consultora responsável, Origem do lead, Interesse).</summary>
public class CampoPersonalizado
{
    public int Id { get; set; }
    public EntidadeCampo Entidade { get; set; }
    public string Nome { get; set; } = "";
    public TipoCampo Tipo { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
    public long? KommoId { get; set; }
    public List<CampoOpcao> Opcoes { get; set; } = new();
}

public class CampoOpcao
{
    public int Id { get; set; }
    public int CampoId { get; set; }
    public CampoPersonalizado? Campo { get; set; }
    public string Valor { get; set; } = "";
    public int Ordem { get; set; }
    public long? KommoId { get; set; }
}

/// <summary>Valor de um campo personalizado para um lead, contato ou empresa. Múltipla escolha guarda os ids das opções separados por vírgula.</summary>
public class ValorCampo
{
    public int Id { get; set; }
    public int CampoId { get; set; }
    public CampoPersonalizado? Campo { get; set; }
    public EntidadeCampo Entidade { get; set; }
    public int RegistroId { get; set; }
    public string Valor { get; set; } = "";
}
