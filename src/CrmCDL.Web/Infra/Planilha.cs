using ClosedXML.Excel;

namespace CrmCDL.Web.Infra;

public enum FormatoColuna { Texto, Inteiro, Dinheiro, DataHora, Data }

public record ColunaPlanilha(string Titulo, FormatoColuna Formato = FormatoColuna.Texto);

/// <summary>Gera arquivos .xlsx (Excel) com cabeçalho fixo, filtro e colunas formatadas.</summary>
public static class Planilha
{
    public const string TipoConteudo = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    const double LarguraMaxima = 60;
    const int LinhasParaMedirLargura = 300;

    public static string NomeArquivo(string prefixo)
        => $"{prefixo}-{Hora.Local(DateTime.UtcNow):yyyy-MM-dd-HHmm}.xlsx";

    public static byte[] Gerar(string aba, IReadOnlyList<ColunaPlanilha> colunas, IEnumerable<object?[]> linhas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(aba);

        for (var c = 0; c < colunas.Count; c++) ws.Cell(1, c + 1).Value = colunas[c].Titulo;
        var cab = ws.Range(1, 1, 1, Math.Max(1, colunas.Count));
        cab.Style.Font.Bold = true;
        cab.Style.Font.FontColor = XLColor.White;
        cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#061833");

        var r = 2;
        foreach (var linha in linhas)
        {
            for (var c = 0; c < colunas.Count && c < linha.Length; c++)
                Escrever(ws.Cell(r, c + 1), linha[c]);
            r++;
        }
        var ultima = r - 1;

        for (var c = 0; c < colunas.Count; c++)
        {
            var formato = colunas[c].Formato switch
            {
                FormatoColuna.Dinheiro => "R$ #,##0.00",
                FormatoColuna.DataHora => "dd/mm/yyyy hh:mm",
                FormatoColuna.Data => "dd/mm/yyyy",
                FormatoColuna.Inteiro => "0",
                _ => null
            };
            if (formato is not null && ultima >= 2) ws.Range(2, c + 1, ultima, c + 1).Style.NumberFormat.Format = formato;
        }

        ws.SheetView.FreezeRows(1);
        if (colunas.Count > 0) ws.Range(1, 1, ultima, colunas.Count).SetAutoFilter();
        ws.Columns(1, Math.Max(1, colunas.Count)).AdjustToContents(1, Math.Min(ultima, LinhasParaMedirLargura));
        foreach (var col in ws.ColumnsUsed())
            if (col.Width > LarguraMaxima) col.Width = LarguraMaxima;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Textos entram sempre como texto (nunca como fórmula), então "=..." digitado num lead não vira fórmula no Excel.</summary>
    static void Escrever(IXLCell cel, object? v)
    {
        switch (v)
        {
            case null: break;
            case string s: cel.Value = s; break;
            case decimal d: cel.Value = d; break;
            case int i: cel.Value = i; break;
            case double d: cel.Value = d; break;
            case DateTime dt: cel.Value = dt; break;
            case bool b: cel.Value = b ? "Sim" : "Não"; break;
            default: cel.Value = v.ToString(); break;
        }
    }
}
