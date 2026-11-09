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
/// <summary>Ölçüm formunun diskteki güncel kayıtla birleştirilmiş hâli (bkz. <see cref="MeasurementEditor.Rebase"/>).</summary>
public sealed record RebasedForm(MeasurementSnapshot Values, string? Notes, string? Parts, IReadOnlyList<string> Conflicts);

public static class MeasurementEditor
{
    /// <summary>
    /// Eşzamanlı düzenleme (iyimser eşzamanlılık): form açıldığındaki değerler (<paramref name="loaded"/>), kullanıcının formda
    /// bıraktığı değerler (<paramref name="form"/>) ve diskteki güncel kayıt (<paramref name="disk"/>) birleştirilir.
    /// Kullanıcının değiştirmediği alan diskteki güncel değerinde kalır (başkasının değişikliği geri alınmaz); kullanıcının
    /// değiştirdiği alan, diskte başkası ellemediyse (ya da aynı değere getirdiyse) yazılır; iki taraf da farklı değere
    /// değiştirdiyse yazılmaz, diskteki değer kalır ve alan <see cref="RebasedForm.Conflicts"/> listesine eklenir.
    /// </summary>
    public static RebasedForm Rebase(
        MeasurementSnapshot loaded, string? loadedNotes, string? loadedParts,
        MeasurementSnapshot form, string? formNotes, string? formParts,
        Defect disk)
    {
        var current = Snapshot(disk);
        var conflicts = new List<string>();

        T Pick<T>(T l, T f, T k, string label)
        {
            var eq = EqualityComparer<T>.Default;
            if (eq.Equals(f, l))
                return k;
            if (eq.Equals(k, l) || eq.Equals(k, f))
                return f;
            conflicts.Add(label);
            return k;
        }

        var custom = new List<CustomMeasure>();
        var keys = CustomMeasures.Clean(current.Custom).Select(m => m.Key)
            .Concat(CustomMeasures.Clean(form.Custom).Select(m => m.Key))
            .Concat(CustomMeasures.Clean(loaded.Custom).Select(m => m.Key))
            .Distinct().ToList();
        foreach (var key in keys)
        {
            var l = CustomMeasures.Find(loaded.Custom, key);
            var f = CustomMeasures.Find(form.Custom, key);
            var k = CustomMeasures.Find(current.Custom, key);
            bool Same(CustomMeasure? a, CustomMeasure? b) =>
                CustomMeasures.SameValues(a is null ? null : new[] { a }, b is null ? null : new[] { b });

            CustomMeasure? pick;
            if (Same(f, l))
                pick = k;
            else if (Same(k, l) || Same(k, f))
                pick = f;
            else
            {
                conflicts.Add((f ?? k ?? l)!.Name);
                pick = k;
            }
            if (pick is not null)
                custom.Add(pick);
        }

        var values = new MeasurementSnapshot(
            Pick(loaded.Depth, form.Depth, current.Depth, "Derinlik"),
            Pick(loaded.Width, form.Width, current.Width, "Genişlik"),
            Pick(loaded.Length, form.Length, current.Length, "Uzunluk"),
            Pick(loaded.Radius, form.Radius, current.Radius, "Çap"),
            Pick(loaded.AngleDeg, form.AngleDeg, current.AngleDeg, "Açı"),
            Pick(loaded.Color, form.Color, current.Color, "Renk"),
            Pick(loaded.HighMetal, form.HighMetal, current.HighMetal, "High metal"),
            custom.Count > 0 ? custom : null);
        var notes = Pick(loadedNotes, formNotes, disk.Notes, "Not");
        var parts = Pick(loadedParts, formParts, disk.AffectedParts, "Etkilenen parçalar");
        return new RebasedForm(values, notes, parts, conflicts);
    }

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
