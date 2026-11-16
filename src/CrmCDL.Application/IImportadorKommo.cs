namespace CrmCDL.Application;

public record ResultadoImportacao(int Funis, int Etapas, int Campos, int Opcoes, int MotivosPerda, int Tags, List<string> Avisos);

/// <summary>Traz da Kommo a estrutura (funis, etapas, campos, motivos de perda e tags). Só lê a Kommo; nunca altera nada lá.</summary>
public interface IImportadorKommo
{
    bool Configurado { get; }
    Task<ResultadoImportacao> ImportarEstruturaAsync(CancellationToken ct = default);
}
