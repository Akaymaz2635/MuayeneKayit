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
        Add(lines, "Çap", before.Radius, after.Radius, unit);
        Add(lines, "Açı", before.AngleDeg, after.AngleDeg, null);

        if (!string.Equals(before.Color ?? "", after.Color ?? "", StringComparison.Ordinal))
            lines.Add($"Renk: {Text(before.Color)} → {Text(after.Color)}");

        // Ek ölçümler: önceki ya da şimdiki kayıtta değeri olanlar, kayıttaki adıyla.
        var beforeCustom = CustomMeasures.Clean(before.Custom);
        var afterCustom = CustomMeasures.Clean(after.Custom);
        foreach (var key in beforeCustom.Concat(afterCustom).Select(m => m.Key).Distinct())
        {
            var b = beforeCustom.FirstOrDefault(m => m.Key == key);
            var a = afterCustom.FirstOrDefault(m => m.Key == key);
            var field = a ?? b!;
            if (field.Kind == CustomFieldKind.Text)
            {
                if (!string.Equals(b?.Text, a?.Text, StringComparison.Ordinal))
                    lines.Add($"{field.Name}: {Text(b?.Text)} → {Text(a?.Text)}");
            }
            else
            {
                Add(lines, field.Name, b?.Number, a?.Number, field.Kind == CustomFieldKind.Length ? unit : null,
                    field.Kind == CustomFieldKind.Number ? "" : null);
            }
        }

        if (before.HighMetal != after.HighMetal)
            lines.Add(before.HighMetal ? "High metal: var → giderildi" : "High metal: yok → var (yeni)");

        // Etkilenen parçalar: iki taraf da biliniyorsa (eski kayıtta null) ve değiştiyse.
        if (before.Parts is not null && MeasurementEditor.Snapshot(after).Parts is { } afterParts && before.Parts != afterParts)
        {
            var cleared = PartList.Removed(before.Parts, afterParts);
            lines.Add($"Etkilenen parçalar: {PartsText(before.Parts)} → {PartsText(afterParts)}"
                      + (cleared.Count == 0 ? "" : $" (giderildi: {PartList.Join(cleared)})"));
        }

        return lines.Count == 0 ? new[] { "Ölçüm alanı yok." } : lines;
    }

    /// <summary>Rework öncesi ile sonrası arasında giderilen parçalar (iki taraf da biliniyorsa); yoksa boş.</summary>
    public static IReadOnlyList<int> ClearedParts(MeasurementSnapshot? before, MeasurementSnapshot? after) =>
        before?.Parts is { } b && after?.Parts is { } a ? PartList.Removed(b, a) : Array.Empty<int>();

    /// <summary>
    /// Bir "rework sonrası" kararın karşılaştırılacağı rework kararının görüntüsü: karardan önceki son rework.
    /// <paramref name="effective"/> geri alınmamış kararlar listesidir (<see cref="DefectStateMachine.Effective"/>).
    /// </summary>
    public static MeasurementSnapshot? ReworkBefore(IReadOnlyList<Disposition> effective, Disposition afterRework) =>
        effective.TakeWhile(x => !ReferenceEquals(x, afterRework)).LastOrDefault(x => x.Decision == Decision.Rework)?.Snapshot;

    private static string PartsText(string parts) => parts.Length == 0 ? "tümü" : parts;

    /// <param name="suffix">Verilirse birim eki yerine kullanılır (birimsiz sayı için "").</param>
    private static void Add(List<string> lines, string label, double? before, double? after, LengthUnit? unit, string? suffix = null)
    {
        string Suffix(LengthUnit? u) => suffix ?? MeasurementCompare.Suffix(u);

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
