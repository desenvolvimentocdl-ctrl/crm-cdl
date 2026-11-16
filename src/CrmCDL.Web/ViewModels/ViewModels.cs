using CrmCDL.Domain;

namespace CrmCDL.Web.ViewModels;

public record CartaoLead(int Id, string Titulo, decimal Valor, int EtapaId, string? Contato, string? Empresa,
    string? Responsavel, List<string> Tags, DateTime? ProximaTarefa, DateTime AtualizadoEm, double Posicao);

public record QuadroVm(Funil Funil, List<Funil> Funis, List<Etapa> Etapas, List<CartaoLead> Cartoes,
    string? Busca, int? Responsavel, bool Fechados, List<Usuario> Usuarios, List<MotivoPerda> MotivosPerda, DateTime Agora);

public class LeadVm
{
    public Lead Lead { get; set; } = null!;
    public List<Etapa> Etapas { get; set; } = new();
    public List<Usuario> Usuarios { get; set; } = new();
    public List<CampoPersonalizado> Campos { get; set; } = new();
    public Dictionary<int, string> Valores { get; set; } = new();
    public List<Atividade> Atividades { get; set; } = new();
    public List<Tarefa> TarefasAbertas { get; set; } = new();
    public List<MotivoPerda> MotivosPerda { get; set; } = new();
    public List<string> TodasTags { get; set; } = new();
    public bool PodeGerir { get; set; }
}

public record CamposVm(List<CampoPersonalizado> Campos, Dictionary<int, string> Valores);

public class PainelVm
{
    public List<Funil> Funis { get; set; } = new();
    public Funil Funil { get; set; } = null!;
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public int Novos { get; set; }
    public int Ganhos { get; set; }
    public int Perdidos { get; set; }
    public decimal ValorGanho { get; set; }
    public int EmAberto { get; set; }
    public decimal ValorAberto { get; set; }
    public int TarefasAtrasadas { get; set; }
    public List<(Etapa Etapa, int Qtd, decimal Valor)> PorEtapa { get; set; } = new();
    public List<(string Nome, int Abertos, int Ganhos, int Perdidos, decimal Valor, int Atrasadas)> PorResponsavel { get; set; } = new();
    public List<(string Motivo, int Qtd)> Motivos { get; set; } = new();
}
