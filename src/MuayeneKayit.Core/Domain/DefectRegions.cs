namespace MuayeneKayit.Core;

/// <summary>
/// Bölge (kusurun bir fotoğrafı, raporda 3.1, 3.2 ...) ve parent-child bağı kuralları. Hepsi kayıtlı kararlardan
/// türetilir, saklanmaz. Ölçüm kusur başına tek kalır; bölge yalnızca "bu fotoğraftaki kusur ne oldu" sorusunu taşır.
/// </summary>
public static class DefectRegions
{
    /// <summary>Bölge etiketi: kusur no + fotoğrafın sırası ("3.2"). Fotoğraf kusurda yoksa boş.</summary>
    public static string Label(Defect defect, string photo)
    {
        var index = defect.Photos.IndexOf(photo);
        return index < 0 ? "" : $"{defect.Id}.{index + 1}";
    }

    /// <summary>
    /// Bölgenin durumu: geçerli (undo edilmemiş) kararlardan, o fotoğrafı anan en sonuncusu belirler. Karar yoksa Active.
    /// Yeniden açma bölgeleri sıfırlar (kusur baştan açılır).
    /// </summary>
    public static RegionStatus Status(Defect defect, string photo) => StatusInfo(defect, photo).Status;

    /// <summary>Bölgenin durumu ve (varsa) onu belirleyen karar.</summary>
    public static (RegionStatus Status, Disposition? By) StatusInfo(Defect defect, string photo)
    {
        var effective = DefectStateMachine.Effective(defect);
        for (var i = effective.Count - 1; i >= 0; i--)
        {
            var x = effective[i];
            if (x.Decision == Decision.Reopen)
                return (RegionStatus.Active, null);

            if (x.PhotoOutcomes is not null && x.PhotoOutcomes.TryGetValue(photo, out var outcome))
            {
                var status = outcome switch
                {
                    PhotoOutcome.Conforms => RegionStatus.Conforms,
                    PhotoOutcome.NewDefect => RegionStatus.NewDefect,
                    _ => RegionStatus.Active,
                };
                return (status, x);
            }
        }
        return (RegionStatus.Active, null);
    }

    /// <summary>Hâlâ aktif bölgeler (fotoğraf adları, kusurdaki sırayla). Sonraki kararlar ve muayeneler yalnızca bunları sorar.</summary>
    public static IReadOnlyList<string> ActivePhotos(Defect defect) =>
        defect.Photos.Where(p => Status(defect, p) == RegionStatus.Active).ToList();

    /// <summary>Hiçbir geçerli kararda bölge sonucu kaydedilmediyse false (eski kayıtlar, bölge sorulmamış kusurlar).</summary>
    public static bool HasRegionData(Defect defect) =>
        DefectStateMachine.Effective(defect).Any(x => x.PhotoOutcomes is { Count: > 0 });

    /// <summary>
    /// Bir kararla açılan yeni kusurlar. Eski kayıtta (NewDefectIds yok) AfterReworkNewDefect kararı için
    /// tek bağ olan SupersededById kullanılır.
    /// </summary>
    public static IReadOnlyList<int> ChildIdsOf(Defect defect, Disposition decision)
    {
        if (decision.NewDefectIds is { Count: > 0 } ids)
            return ids;
        if (decision.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is int legacy)
            return new[] { legacy };
        return Array.Empty<int>();
    }

    /// <summary>Kusurun geçerli kararlarıyla açılmış tüm yeni kusurlar (void olanlar dahil), açılış sırasıyla.</summary>
    public static IReadOnlyList<int> ChildIds(Defect defect)
    {
        var all = new List<int>();
        foreach (var x in DefectStateMachine.Effective(defect))
        {
            if (x.Decision is not (Decision.AfterReworkSameDefect or Decision.AfterReworkNewDefect))
                continue;
            foreach (var id in ChildIdsOf(defect, x))
            {
                if (!all.Contains(id))
                    all.Add(id);
            }
        }
        return all;
    }

    /// <summary>
    /// Void edilmemiş yeni kusurlar. Rapor, cümle, ekran ve muayene kapanışı yalnızca bunları kullanır.
    /// Liste verilmezse (çocukların durumu bilinmiyor) hepsi sayılır.
    /// </summary>
    public static IReadOnlyList<int> ActiveChildIds(Defect defect, IReadOnlyList<Defect>? all) =>
        ChildIds(defect)
            .Where(id => all?.FirstOrDefault(c => c.Id == id) is not Defect child
                         || DefectStateMachine.GetState(child) != DefectState.Voided)
            .ToList();

    /// <summary>Bir bölgede bulunup açılan (void olmayan) yeni kusurlar.</summary>
    public static IReadOnlyList<int> ActiveChildIdsForPhoto(Defect defect, string photo, IReadOnlyList<Defect>? all) =>
        ActiveChildIds(defect, all)
            .Where(id => all?.FirstOrDefault(c => c.Id == id)?.FoundAtPhoto == photo)
            .ToList();

    /// <summary>Child'ın bulunduğu bölgenin etiketi ("3.3"). Parent listede yoksa ya da bölgesizse boş.</summary>
    public static string FoundAtLabel(Defect child, IReadOnlyList<Defect>? all)
    {
        if (child.SupersedesId is not int parentId || child.FoundAtPhoto is null)
            return "";
        var parent = all?.FirstOrDefault(d => d.Id == parentId);
        return parent is null ? "" : Label(parent, child.FoundAtPhoto);
    }
}
