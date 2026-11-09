using System.Globalization;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Kusurun ekranda nasıl gösterileceğine dair saf kurallar (WinUI'den bağımsız, test edilebilir).</summary>
public static class DefectPresentation
{
    /// <summary>Disposition listesinde gösterilecek kararlar ve sıraları.</summary>
    public static readonly (Decision Decision, string Label)[] DecisionOrder =
    {
        (Decision.Accept, "Kabul (SPA)"),
        (Decision.Rework, "Rework → Reinspect"),
        (Decision.SubmitMrb, "Submit MRB"),
        (Decision.MrbAccepted, "MRB kabul (accept as is)"),
        (Decision.MrbRejected, "MRB red (otomatik hurda)"),
        (Decision.Repair, "Repair"),
        (Decision.Reinspect, "Reinspect (CTP)"),
        (Decision.ReturnToVendor, "Return to vendor"),
        (Decision.Scrap, "Scrap"),
        (Decision.Reopen, "Yeniden aç"),
    };

    public static string StateLabel(DefectState state) => state switch
    {
        DefectState.Open => "Açık",
        DefectState.Reworking => "Rework",
        DefectState.Repairing => "Repair",
        DefectState.AwaitingReinspect => "Rework sonrası muayene bekliyor",
        DefectState.WaitingVendor => "Tedarikçide",
        DefectState.MrbPending => "MRB bekliyor",
        DefectState.Closed => "Kapandı",
        DefectState.Voided => "Void",
        DefectState.Superseded => "Devredildi",
        _ => state.ToString(),
    };

    /// <summary>Durum etiketinin tonu (renk View'da seçilir).</summary>
    public static Tone ToneFor(DefectState state) => state switch
    {
        DefectState.Closed => Tone.Success,
        DefectState.MrbPending => Tone.Attention,
        DefectState.Voided or DefectState.Superseded => Tone.Neutral,
        DefectState.AwaitingReinspect or DefectState.WaitingVendor or DefectState.Repairing => Tone.Caution,
        _ => Tone.Critical,
    };

    /// <summary>"Dent + High metal" ya da "Dent".</summary>
    public static string TypeText(Defect d) => d.HighMetal ? $"{d.DefectType} + High metal" : d.DefectType;

    /// <summary>
    /// Muayenecinin bu kusurda yapması gereken sıradaki iş. <paramref name="all"/>: oturumun kusurları;
    /// verilirse void edilen yeni kusurlar anılmaz.
    /// </summary>
    public static string NextStepFor(Defect d, DefectState state, IReadOnlyList<Defect>? all = null)
    {
        var hasMeasure = HasMeasurement(d);
        return state switch
        {
            DefectState.Open when !hasMeasure => "Sıradaki adım: ölçümleri girin.",
            DefectState.Open when d.Photos.Count == 0 => "Sıradaki adım: fotoğraf çekin.",
            DefectState.Open => "Mühendis kararı bekleniyor: rapordan sonra Disposition girin.",
            DefectState.AwaitingReinspect => "Rework'te: parça dönünce rework sonrası muayene sonucunu girin.",
            DefectState.Repairing => "Repair'de: sonuca göre kabul, hurda, reinspect ya da MRB girin.",
            DefectState.WaitingVendor => "Tedarikçide: parça dönünce reinspect girin.",
            DefectState.MrbPending => "MRB kararı bekleniyor: kabul ya da red gelince girin.",
            DefectState.Closed => "Tamamlandı.",
            DefectState.Superseded => SupersededNextStep(DefectRegions.ActiveChildIds(d, all)),
            DefectState.Voided => "Void edildi, rapora girmez.",
            _ => "",
        };
    }

    private static string SupersededNextStep(IReadOnlyList<int> children) => children.Count switch
    {
        0 => "Devredildi.",
        1 => $"Takip #{children[0]} numaralı kusur üzerinden yapılıyor.",
        _ => $"Takip {IdList(children)} numaralı kusurlar üzerinden yapılıyor.",
    };

    /// <summary>"#7, #8".</summary>
    public static string IdList(IEnumerable<int> ids) => string.Join(", ", ids.Select(i => "#" + i));

    /// <summary>
    /// Fotoğraf kartındaki bölge rozeti: "Giderildi" ya da "Yeni kusur #7, #8". Devam eden bölge, tek fotoğraflı kusur ve
    /// bölge sonucu hiç kaydedilmemiş kusurda boş.
    /// </summary>
    public static string RegionBadge(Defect d, string photo, IReadOnlyList<Defect>? all)
    {
        if (d.Photos.Count < 2 || !DefectRegions.HasRegionData(d))
            return "";

        switch (DefectRegions.Status(d, photo))
        {
            case RegionStatus.Conforms:
                return "Giderildi";
            case RegionStatus.NewDefect:
                var ids = DefectRegions.ActiveChildIdsForPhoto(d, photo, all);
                return ids.Count == 0 ? "Yeni kusur" : $"Yeni kusur {IdList(ids)}";
            default:
                return "";
        }
    }

    public static bool HasMeasurement(Defect d) =>
        d.Depth is not null || d.Width is not null || d.Length is not null
        || d.Radius is not null || d.AngleDeg is not null || !string.IsNullOrWhiteSpace(d.Color)
        || CustomMeasures.Clean(d.Custom).Count > 0;

    /// <summary>Kusurun sıradaki adımı: ölçüm, fotoğraf, karar; kapalı / devredilmiş / void için yok (null).</summary>
    public static InspectionStep? CurrentStep(Defect d, DefectState state)
    {
        if (state is DefectState.Closed or DefectState.Voided or DefectState.Superseded)
            return null;
        if (state == DefectState.AwaitingReinspect)
            return InspectionStep.Measure;
        if (state == DefectState.Open && !HasMeasurement(d))
            return InspectionStep.Measure;
        if (state == DefectState.Open && d.Photos.Count == 0)
            return InspectionStep.Photo;
        return InspectionStep.Decision;
    }

    public static string SnapshotText(MeasurementSnapshot s) => SnapshotText(s, LengthUnit.Mm);

    public static string SnapshotText(MeasurementSnapshot s, LengthUnit unit)
    {
        var parts = new List<string>();
        if (s.Depth is double d) parts.Add($"D {Fmt(d, unit)}");
        if (s.Width is double w) parts.Add($"W {Fmt(w, unit)}");
        if (s.Length is double l) parts.Add($"L {Fmt(l, unit)}");
        if (s.Radius is double r) parts.Add($"Ç {Fmt(r, unit)}");
        if (s.AngleDeg is double a) parts.Add($"A {Fmt(a)}°");
        if (!string.IsNullOrWhiteSpace(s.Color)) parts.Add(s.Color!);
        parts.AddRange(CustomMeasures.Clean(s.Custom).Select(m => CustomMeasures.Short(m, unit)));
        if (s.HighMetal) parts.Add("High metal");
        return parts.Count == 0 ? "—" : string.Join(" ", parts);
    }

    /// <summary>Birimsiz sayılar (açı vb.) için; en fazla 2 ondalık.</summary>
    public static string Fmt(double? value) =>
        value is double v ? v.ToString("0.##", CultureInfo.InvariantCulture) : "";

    /// <summary>Uzunluk ölçüsü: mm en fazla 2, inch en fazla 4 ondalık ve ".123" biçiminde.</summary>
    public static string Fmt(double? value, LengthUnit unit) =>
        value is double v ? Units.Format(v, unit) : "";

    public static double? ParseOptional(string text, string label)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return null;

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ||
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return v;

        throw new ArgumentException($"{label} sayı olmalı: '{text}'");
    }

    /// <param name="all">Oturumun kusurları; verilirse void edilen yeni kusurlar anılmaz.</param>
    public static IReadOnlyList<string> HistoryLines(Defect defect, IReadOnlyList<Defect>? all = null)
    {
        var items = new List<(DateTime At, string Text)>();

        // Geri alınan kararlar gösterilmez; ham dosyada ve denetim kaydında kalır.
        foreach (var x in DefectStateMachine.Effective(defect))
        {
            var parts = new List<string>
            {
                $"{x.DecidedAt:dd.MM HH:mm}  {x.DecidedBy}  ·  {DefectSentence.DecisionLabel(x)}",
            };
            if (!string.IsNullOrWhiteSpace(x.Note)) parts.Add($"not: {x.Note}");
            if (!string.IsNullOrWhiteSpace(x.Reference)) parts.Add($"ref: {x.Reference}");
            if (x.Snapshot is MeasurementSnapshot snap) parts.Add($"ölçüm: {SnapshotText(snap, defect.Unit)}");
            if (RegionOutcomeText(defect, x, all) is { Length: > 0 } regions) parts.Add($"bölgeler: {regions}");
            if (DefectRegions.ChildIdsOf(defect, x).Where(id => DefectRegions.ActiveChildIds(defect, all).Contains(id)).ToList() is { Count: > 0 } opened)
                parts.Add($"açılan kusurlar: {IdList(opened)}");
            items.Add((x.DecidedAt, string.Join("   ", parts)));
        }

        foreach (var m in defect.MeasurementEdits)
            items.Add((m.At, $"{m.At:dd.MM HH:mm}  {m.By}  ·  Ölçüm düzeltmesi: {SnapshotText(m.Before, defect.Unit)}  →  {SnapshotText(m.After, defect.Unit)}"));

        return items.Count == 0
            ? new[] { "Henüz karar ya da düzeltme yok." }
            : items.OrderBy(i => i.At).Select((i, n) => $"{n + 1}.  {i.Text}").ToList();
    }

    /// <summary>Bir kararın bölge sonuçları: "3.1 giderildi, 3.2 devam, 3.3 yeni kusur #7, #8". Bölge sonucu yoksa boş.</summary>
    private static string RegionOutcomeText(Defect defect, Disposition decision, IReadOnlyList<Defect>? all)
    {
        if (decision.PhotoOutcomes is not { Count: > 0 } outcomes)
            return "";

        var parts = new List<string>();
        foreach (var photo in defect.Photos.Where(outcomes.ContainsKey))
        {
            var label = DefectRegions.Label(defect, photo);
            switch (outcomes[photo])
            {
                case PhotoOutcome.Conforms:
                    parts.Add($"{label} giderildi");
                    break;
                case PhotoOutcome.NewDefect:
                    var ids = DefectRegions.ActiveChildIdsForPhoto(defect, photo, all);
                    parts.Add(ids.Count == 0 ? $"{label} yeni kusur" : $"{label} yeni kusur {IdList(ids)}");
                    break;
                default:
                    parts.Add($"{label} devam");
                    break;
            }
        }
        return string.Join(", ", parts);
    }

    public static string DecisionHintFor(Defect defect) => DefectStateMachine.GetState(defect) switch
    {
        DefectState.Open => "Açık kusur: rework, reinspect, kabul, hurda, MRB veya tedarikçi kararı verilebilir.",
        DefectState.Reworking => "Rework yapılıyor: sonra reinspect ile yeniden muayene edin.",
        DefectState.AwaitingReinspect => "Rework yapıldı. Yeniden muayene sonucunu 'Rework sonrası muayene sonucunu gir' ile girin.",
        DefectState.WaitingVendor => "Tedarikçide: geri dönünce reinspect verin.",
        DefectState.MrbPending => "MRB bekleniyor: kabul ya da red gelince girin.",
        DefectState.Closed when !DefectStateMachine.IsReopenable(defect)
            => "Kesin kapandı (hurda ya da MRB reddi). Yeniden açılamaz.",
        DefectState.Closed => "Kapalı kusur. Yeniden açmak için not yazıp 'Yeniden aç' kullanın, geçmiş korunur.",
        DefectState.Superseded => "Bu kusur yeni bir kusura devredildi. İşlem yeni kusur üzerinden yapılır.",
        DefectState.Voided => "Void edilmiş kayıt. Rapora girmez, değiştirilemez.",
        _ => "",
    };

    public static DecisionSource SourceFor(Decision decision) => decision switch
    {
        Decision.Void => DecisionSource.Operator,
        Decision.MrbAccepted or Decision.MrbRejected => DecisionSource.Mrb,
        _ => DecisionSource.Engineer,
    };
}
