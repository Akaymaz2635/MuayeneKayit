namespace MuayeneKayit.Core;

/// <summary>Karar vermeden yapılan ölçüm düzeltmesi. Kusur geçmişinde ve raporda görünür.</summary>
public sealed record MeasurementEdit(
    DateTime At,
    string By,
    MeasurementSnapshot Before,
    MeasurementSnapshot After);

/// <summary>
/// Ölçümleri günceller ve değişiklik varsa düzeltme kaydı ekler.
/// Değer değişmediyse hiçbir şey yazılmaz.
/// </summary>
public static class MeasurementEditor
{
    public static MeasurementSnapshot Snapshot(Defect d) =>
        new(d.Depth, d.Width, d.Length, d.Radius, d.AngleDeg, d.Color, d.HighMetal, d.Custom is { Count: > 0 } c ? c.ToList() : null);

    /// <param name="mergeWithin">
    /// Verilirse: aynı kişinin bu süre içindeki son düzeltmesi (arada karar yoksa) yeni satır eklenmeden güncellenir.
    /// Otomatik kayıtta her alan için ayrı geçmiş satırı oluşmaz; değer ilk hâline dönerse satır kalkar.
    /// </param>
    /// <returns>Ölçüm değiştiyse true.</returns>
    public static bool Apply(Defect defect, MeasurementSnapshot values, string by, DateTime? at = null, TimeSpan? mergeWithin = null)
    {
        var before = Snapshot(defect);
        if (before == values)
            return false;

        var now = at ?? DateTime.Now;
        var last = defect.MeasurementEdits.LastOrDefault();
        if (mergeWithin is TimeSpan window && last is not null && last.By == by && now - last.At <= window
            && !defect.Dispositions.Any(x => x.DecidedAt >= last.At))
        {
            defect.MeasurementEdits.RemoveAt(defect.MeasurementEdits.Count - 1);
            before = last.Before;
        }

        defect.Depth = values.Depth;
        defect.Width = values.Width;
        defect.Length = values.Length;
        defect.Radius = values.Radius;
        defect.AngleDeg = values.AngleDeg;
        defect.Color = values.Color;
        defect.HighMetal = values.HighMetal;
        defect.Custom = CustomMeasures.Clean(values.Custom) is { Count: > 0 } custom ? custom : null;

        if (before != values)
            defect.MeasurementEdits.Add(new MeasurementEdit(now, by, before, values));
        return true;
    }
}
