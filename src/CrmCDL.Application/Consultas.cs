using CrmCDL.Domain;

namespace CrmCDL.Application;

/// <summary>Filtros usados tanto nas telas quanto na exportação para Excel, para a planilha trazer exatamente o que a tela mostra.</summary>
public record FiltroLeads(int FunilId, string? Busca = null, int? ResponsavelId = null, bool IncluirFechadosAntigos = false);

public static class Consultas
{
    /// <summary>Ganhos e perdidos mais antigos que isso só aparecem quando o filtro "Fechados antigos" está marcado.</summary>
    public const int DiasFechadosVisiveis = 30;

    public static IQueryable<Lead> FiltrarLeads(IQueryable<Lead> q, FiltroLeads f, DateTime agoraUtc)
    {
        q = q.Where(l => l.FunilId == f.FunilId);
        if (!string.IsNullOrWhiteSpace(f.Busca))
        {
            var b = f.Busca.Trim();
            q = q.Where(l => l.Titulo.Contains(b) || (l.Empresa != null && l.Empresa.Nome.Contains(b))
                             || l.Contatos.Any(c => c.Contato!.Nome.Contains(b) || (c.Contato.Celular != null && c.Contato.Celular.Contains(b)) || (c.Contato.Email != null && c.Contato.Email.Contains(b))));
        }
        if (f.ResponsavelId is int r) q = q.Where(l => l.ResponsavelId == r);
        if (!f.IncluirFechadosAntigos)
        {
            var limite = agoraUtc.AddDays(-DiasFechadosVisiveis);
            q = q.Where(l => l.FechadoEm == null || l.FechadoEm >= limite);
        }
        return q;
    }

    public static IQueryable<Contato> FiltrarContatos(IQueryable<Contato> q, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca)) return q;
        var b = busca.Trim();
        return q.Where(c => c.Nome.Contains(b) || (c.Email != null && c.Email.Contains(b)) || (c.Celular != null && c.Celular.Contains(b))
                            || (c.Telefone != null && c.Telefone.Contains(b)) || (c.Cpf != null && c.Cpf.Contains(b)) || (c.Empresa != null && c.Empresa.Nome.Contains(b)));
    }

    /// <summary>Texto legível de um campo personalizado (listas guardam ids das opções; Sim/Não guarda "1").</summary>
    public static string TextoDoCampo(CampoPersonalizado campo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "";
        return campo.Tipo switch
        {
            TipoCampo.Lista or TipoCampo.MultiplaEscolha => string.Join(", ",
                valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => campo.Opcoes.FirstOrDefault(o => o.Id.ToString() == id)?.Valor ?? id)),
            TipoCampo.Checkbox => valor == "1" ? "Sim" : "Não",
            _ => valor
        };
    }
}
