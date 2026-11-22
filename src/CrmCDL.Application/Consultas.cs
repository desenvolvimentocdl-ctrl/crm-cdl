using CrmCDL.Domain;

namespace CrmCDL.Application;

/// <summary>Filtros usados tanto nas telas quanto na exportação para Excel, para a planilha trazer exatamente o que a tela mostra.</summary>
public record FiltroLeads(int FunilId, string? Busca = null, int? ResponsavelId = null, bool IncluirFechadosAntigos = false,
    int? EtapaId = null, bool SoParados = false, string? Tag = null, string? Situacao = null);

public static class Consultas
{
    /// <summary>Ganhos e perdidos mais antigos que isso só aparecem quando o filtro "Fechados antigos" está marcado.</summary>
    public const int DiasFechadosVisiveis = 30;
    /// <summary>Lead em aberto sem nenhuma movimentação há esse tanto de dias aparece como "parado".</summary>
    public const int DiasParado = 7;

    public static bool Parado(Lead l, DateTime agoraUtc) => l.FechadoEm == null && l.AtualizadoEm < agoraUtc.AddDays(-DiasParado);

    public static IQueryable<Lead> FiltrarLeads(IQueryable<Lead> q, FiltroLeads f, DateTime agoraUtc)
    {
        if (f.FunilId > 0) q = q.Where(l => l.FunilId == f.FunilId);
        if (f.EtapaId is int e) q = q.Where(l => l.EtapaId == e);
        if (!string.IsNullOrWhiteSpace(f.Tag)) { var t = f.Tag.Trim(); q = q.Where(l => l.Tags.Any(x => x.Tag!.Nome == t)); }
        if (f.SoParados) { var limiteParado = agoraUtc.AddDays(-DiasParado); q = q.Where(l => l.FechadoEm == null && l.AtualizadoEm < limiteParado); }
        q = f.Situacao switch
        {
            "abertos" => q.Where(l => l.FechadoEm == null),
            "ganhos" => q.Where(l => l.Etapa!.Tipo == TipoEtapa.Ganho),
            "perdidos" => q.Where(l => l.Etapa!.Tipo == TipoEtapa.Perdido),
            _ => q
        };
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

    /// <summary>Só os dígitos (para comparar telefones escritos de jeitos diferentes).</summary>
    public static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Últimos 8 dígitos: o número sem DDD/DDI e sem o 9 extra, o que basta para achar o mesmo telefone.</summary>
    public static string? ChaveFone(string? fone)
    {
        var d = Digitos(fone);
        return d.Length >= 8 ? d[^8..] : null;
    }

    /// <summary>Contatos que parecem ser a mesma pessoa (mesmo e-mail ou mesmo telefone).</summary>
    public static IQueryable<Contato> Duplicados(IQueryable<Contato> q, string? fone, string? email, int? ignorarId = null)
    {
        var chave = ChaveFone(fone);
        var e = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower();
        if (chave is null && e is null) return q.Where(_ => false);
        if (ignorarId is int id) q = q.Where(c => c.Id != id);
        return q.Where(c => (chave != null && c.FoneDigitos != null && c.FoneDigitos.Contains(chave)) || (e != null && c.Email != null && c.Email.ToLower() == e));
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
