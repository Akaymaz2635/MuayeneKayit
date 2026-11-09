using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Index satırlarını Excel'de açılabilecek CSV'ye çevirir (noktalı virgül ayraçlı, UTF-8 BOM).
/// Formül enjeksiyonuna karşı = + - @ ile başlayan hücrelerin başına tek tırnak eklenir.
/// </summary>
public static class CsvExport
{
    public static string Build(IEnumerable<IndexRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Line("Parça", "Operasyon", "Seri", "Durum", "Inspector", "Açılış", "Kusur", "Açık", "Kusur no", "Hata tipi", "Kusur durumu", "Son karar", "Etkilenen parçalar"));

        foreach (var r in rows)
        {
            var created = r.CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            if (r.Defects.Count == 0)
            {
                sb.AppendLine(Line(r.Part, r.Operation, r.Serial, r.Status, r.Inspector, created,
                    r.TotalDefects.ToString(), r.OpenDefects.ToString(), "", "", "", "", ""));
                continue;
            }

            foreach (var d in r.Defects)
            {
                sb.AppendLine(Line(r.Part, r.Operation, r.Serial, r.Status, r.Inspector, created,
                    r.TotalDefects.ToString(), r.OpenDefects.ToString(), d.Id.ToString(), d.Type, d.State, d.LastDecision, d.AffectedParts));
            }
        }
        return sb.ToString();
    }

    public static string Cell(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            v = "'" + v;
        return v.IndexOfAny(new[] { ';', ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    private static string Line(params string?[] cells) => string.Join(";", cells.Select(Cell));
}
