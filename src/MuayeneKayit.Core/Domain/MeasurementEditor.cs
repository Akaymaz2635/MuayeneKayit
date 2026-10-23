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
        new(d.Depth, d.Width, d.Length, d.Radius, d.AngleDeg, d.Color);

    /// <returns>Ölçüm değiştiyse true.</returns>
    public static bool Apply(Defect defect, MeasurementSnapshot values, string by, DateTime? at = null)
    {
        var before = Snapshot(defect);
        if (before == values)
            return false;

        defect.Depth = values.Depth;
        defect.Width = values.Width;
        defect.Length = values.Length;
        defect.Radius = values.Radius;
        defect.AngleDeg = values.AngleDeg;
        defect.Color = values.Color;

        defect.MeasurementEdits.Add(new MeasurementEdit(at ?? DateTime.Now, by, before, values));
        return true;
    }
}
