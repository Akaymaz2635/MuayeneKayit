using System.Globalization;

namespace MuayeneKayit.Core;

/// <summary>
/// Rework öncesi (karar anı görüntüsü) ile şimdiki ölçümleri karşılaştırır.
/// Her satır "Alan: önce → sonra birim (artış | azalış | aynı | yeni | kaldırıldı)" biçimindedir.
/// </summary>
public static class MeasurementCompare
{
    public static IReadOnlyList<string> Lines(MeasurementSnapshot? before, Defect after)
    {
        if (before is null)
            return new[] { "Rework öncesi ölçüm kaydı yok." };

        var lines = new List<string>();
        Add(lines, "Derinlik", before.Depth, after.Depth, "mm");
        Add(lines, "Genişlik", before.Width, after.Width, "mm");
        Add(lines, "Uzunluk", before.Length, after.Length, "mm");
        Add(lines, "Yarıçap", before.Radius, after.Radius, "mm");
        Add(lines, "Açı", before.AngleDeg, after.AngleDeg, "°");

        if (!string.Equals(before.Color ?? "", after.Color ?? "", StringComparison.Ordinal))
            lines.Add($"Renk: {Text(before.Color)} → {Text(after.Color)}");

        return lines.Count == 0 ? new[] { "Ölçüm alanı yok." } : lines;
    }

    private static void Add(List<string> lines, string label, double? before, double? after, string unit)
    {
        if (before is null && after is null)
            return;

        if (before is null)
        {
            lines.Add($"{label}: yeni → {Num(after!.Value)} {unit}");
            return;
        }

        if (after is null)
        {
            lines.Add($"{label}: {Num(before.Value)} {unit} → kaldırıldı");
            return;
        }

        var b = before.Value;
        var a = after.Value;
        var direction = Math.Abs(a - b) < 1e-9 ? "aynı" : a > b ? "artış" : "azalış";
        lines.Add($"{label}: {Num(b)} → {Num(a)} {unit} ({direction})");
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Text(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!;
}
