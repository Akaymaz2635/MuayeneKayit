using System.Globalization;

namespace MuayeneKayit.Core;

/// <summary>
/// Rework öncesi (karar anı görüntüsü) ile şimdiki ölçümleri karşılaştırır.
/// Her satır "Alan: önce → sonra birim (artış | azalış | aynı | yeni | kaldırıldı)" biçimindedir.
/// </summary>
public static class MeasurementCompare
{
    public static IReadOnlyList<string> Lines(MeasurementSnapshot? before, Defect after) =>
        Lines(before, after, after.Unit);

    /// <summary>Uzunluk ölçüleri verilen birimle yazılır; açı her zaman derecedir.</summary>
    public static IReadOnlyList<string> Lines(MeasurementSnapshot? before, Defect after, LengthUnit unit)
    {
        if (before is null)
            return new[] { "Rework öncesi ölçüm kaydı yok." };

        var lines = new List<string>();
        Add(lines, "Derinlik", before.Depth, after.Depth, unit);
        Add(lines, "Genişlik", before.Width, after.Width, unit);
        Add(lines, "Uzunluk", before.Length, after.Length, unit);
        Add(lines, "Yarıçap", before.Radius, after.Radius, unit);
        Add(lines, "Açı", before.AngleDeg, after.AngleDeg, null);

        if (!string.Equals(before.Color ?? "", after.Color ?? "", StringComparison.Ordinal))
            lines.Add($"Renk: {Text(before.Color)} → {Text(after.Color)}");

        if (before.HighMetal != after.HighMetal)
            lines.Add(before.HighMetal ? "High metal: var → giderildi" : "High metal: yok → var (yeni)");

        return lines.Count == 0 ? new[] { "Ölçüm alanı yok." } : lines;
    }

    private static void Add(List<string> lines, string label, double? before, double? after, LengthUnit? unit)
    {
        if (before is null && after is null)
            return;

        if (before is null)
        {
            lines.Add($"{label}: yeni → {Num(after!.Value, unit)}{Suffix(unit)}");
            return;
        }

        if (after is null)
        {
            lines.Add($"{label}: {Num(before.Value, unit)}{Suffix(unit)} → kaldırıldı");
            return;
        }

        var b = before.Value;
        var a = after.Value;
        var direction = Math.Abs(a - b) < 1e-9 ? "aynı" : a > b ? "artış" : "azalış";
        lines.Add($"{label}: {Num(b, unit)} → {Num(a, unit)}{Suffix(unit)} ({direction})");
    }

    /// <summary>Birim null ise açıdır (mm biçimi, "°" eki).</summary>
    private static string Num(double v, LengthUnit? unit) =>
        unit is LengthUnit u ? Units.Format(v, u) : v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Suffix(LengthUnit? unit) => unit is LengthUnit u ? " " + Units.Suffix(u) : " °";

    private static string Text(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!;
}
