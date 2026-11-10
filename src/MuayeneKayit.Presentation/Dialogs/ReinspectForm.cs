using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Rework sonrası muayene penceresinin kuralları. Pencere canlı güncelleme ve Kaydet etkinliği için bunu çağırır,
/// ViewModel de sonucu doğrularken aynı kuralları kullanır.
/// </summary>
public static class ReinspectForm
{
    /// <summary>Ölçüm kutusundaki metnin durumu: boş, geçerli sayı ya da (sayı olmayan) hatalı.</summary>
    public enum EntryState
    {
        Empty,
        Valid,
        Invalid,
    }

    /// <summary>
    /// Ölçüm kutusu ayrıştırma kuralı (pencere ve test aynı kuralı kullanır): boş metin boştur (önceki değer kalır),
    /// sayı (güncel kültür ya da nokta ayraçlı) geçerlidir, başka her metin hatalıdır. Hatalı metin sessizce "boş" sayılmaz:
    /// pencere satır içi hata gösterir ve Kaydet kapalı kalır.
    /// </summary>
    public static EntryState ParseEntry(string? text, out double? value)
    {
        value = null;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
            return EntryState.Empty;

        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var v)
            || double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                return EntryState.Invalid;
            value = v;
            return EntryState.Valid;
        }
        return EntryState.Invalid;
    }

    /// <summary>Boş bırakılan alan önceki değerinde kalır; high metal cevabı yoksa öncekini korur.</summary>
    public static MeasurementSnapshot Merge(MeasurementSnapshot before, MeasurementSnapshot entered, bool? highMetalStillThere) =>
        new(entered.Depth ?? before.Depth, entered.Width ?? before.Width, entered.Length ?? before.Length,
            entered.Radius ?? before.Radius, entered.AngleDeg ?? before.AngleDeg, entered.Color ?? before.Color,
            highMetalStillThere ?? before.HighMetal, CustomMeasures.Merge(before.Custom, entered.Custom) is { Count: > 0 } c ? c : null);

    /// <summary>Kutulardan en az birine değer yazıldı mı.</summary>
    public static bool AnyValue(MeasurementSnapshot entered) =>
        entered.Depth is not null || entered.Width is not null || entered.Length is not null
        || entered.Radius is not null || entered.AngleDeg is not null || entered.Color is not null
        || CustomMeasures.Clean(entered.Custom).Count > 0;

    /// <summary>"Yeni hata" listesi geçerli mi: en az bir satır var ve her satırda tip seçili.</summary>
    public static bool ValidNewTypes(IReadOnlyList<string?>? types) =>
        types is { Count: > 0 } && types.All(t => !string.IsNullOrWhiteSpace(t));

    /// <summary>
    /// Kaydet açılır mı (aktif bölge 0 ya da 1 iken). Giderildi: her zaman. Aynı kusur: high metal varsa cevabı zorunlu
    /// (ölçümler boş kalabilir), yoksa en az bir ölçüm. Yeni hata: en az bir satır ve her satırda tip seçili.
    /// </summary>
    public static bool CanSave(ReinspectOutcome? outcome, bool hadHighMetal, bool? highMetalStillThere, bool anyValue, IReadOnlyList<string?>? newTypes) =>
        outcome switch
        {
            ReinspectOutcome.Conforms => true,
            ReinspectOutcome.Same => SameReady(hadHighMetal, highMetalStillThere, anyValue),
            ReinspectOutcome.New => ValidNewTypes(newTypes),
            _ => false,
        };

    private static bool SameReady(bool hadHighMetal, bool? highMetalStillThere, bool anyValue) =>
        hadHighMetal ? highMetalStillThere is not null : anyValue;

    /// <summary>
    /// Kaydet açılır mı (2+ aktif bölge iken): her bölge için seçim yapılmış, "Yeni hata" bölgelerinin her satırında tip seçili;
    /// devam eden bölge varsa ölçüm adımı da tamam (aynı kusur kuralı).
    /// </summary>
    public static bool CanSaveRegions(IReadOnlyList<RegionChoice> regions, bool hadHighMetal, bool? highMetalStillThere, bool anyValue)
    {
        if (regions.Count == 0 || regions.Any(r => r.Outcome is null))
            return false;
        if (regions.Any(r => r.Outcome == PhotoOutcome.NewDefect && !ValidNewTypes(r.NewTypes)))
            return false;
        return !regions.Any(r => r.Outcome == PhotoOutcome.Continues) || SameReady(hadHighMetal, highMetalStillThere, anyValue);
    }

    /// <summary>Devam eden bölge var mı: varsa ölçüm alanları (aynı kusur adımı) açılır.</summary>
    public static bool AnyContinues(IReadOnlyList<RegionChoice> regions) =>
        regions.Any(r => r.Outcome == PhotoOutcome.Continues);

    /// <summary>Bölge seçimlerinden otomatik sonuç: devam eden varsa aynı kusur; yoksa yeni hata varsa yeni hata; yoksa giderildi.</summary>
    public static ReinspectOutcome OutcomeFor(IReadOnlyList<RegionChoice> regions) =>
        AnyContinues(regions) ? ReinspectOutcome.Same
        : regions.Any(r => r.Outcome == PhotoOutcome.NewDefect) ? ReinspectOutcome.New
        : ReinspectOutcome.Conforms;

    /// <summary>
    /// Pencerenin altındaki otomatik sonuç özeti: "#3 kapanır" / "#3 açık kalır; 2 yeni kusur açılır" /
    /// "#3 yeni kusurlara devredilir". Seçimi eksik bölge varsa boş.
    /// </summary>
    public static string Summary(int defectId, IReadOnlyList<RegionChoice> regions)
    {
        if (regions.Count == 0 || regions.Any(r => r.Outcome is null))
            return "";

        var newCount = regions.Where(r => r.Outcome == PhotoOutcome.NewDefect).Sum(r => r.NewTypes.Count);
        if (AnyContinues(regions))
            return newCount > 0 ? $"#{defectId} açık kalır; {newCount} yeni kusur açılır" : $"#{defectId} açık kalır";
        return newCount > 0 ? $"#{defectId} yeni kusurlara devredilir" : $"#{defectId} kapanır";
    }

    /// <summary>
    /// Pencerenin cevabını işlenecek sonuca çevirir (bölge sonuçları, açılacak yeni kusurlar, güncel ölçümler, not).
    /// Eksik / çelişkili cevapta InvalidOperationException fırlatır. Aktif bölge 0 ya da 1 ise tek sonuç (Outcome),
    /// 2+ ise bölge seçimleri (Regions) kullanılır. Çok fotoğraflı kusurda tek aktif bölgenin sonucu da kaydedilir;
    /// tek fotoğraflı kusurda bölge kaydı tutulmaz (eski kayıtlarla aynı).
    /// </summary>
    public static ReinspectResult ToResult(Defect defect, MeasurementSnapshot before, ReinspectInput input)
    {
        var active = DefectRegions.ActivePhotos(defect);
        var noteText = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var news = new List<NewDefectSpec>();
        Dictionary<string, PhotoOutcome>? outcomes = null;
        bool continues;

        if (active.Count >= 2)
        {
            var regions = input.Regions ?? throw new InvalidOperationException("Her bölge için sonucu seçin.");
            if (regions.Count != active.Count || !active.All(p => regions.Count(r => r.Photo == p) == 1)
                || regions.Any(r => r.Outcome is null))
                throw new InvalidOperationException("Her bölge için sonucu seçin.");

            outcomes = regions.ToDictionary(r => r.Photo, r => r.Outcome!.Value);
            foreach (var r in regions.Where(r => r.Outcome == PhotoOutcome.NewDefect))
            {
                if (!ValidNewTypes(r.NewTypes))
                    throw new InvalidOperationException("Yeni hata seçilen her bölge için en az bir hata tipi seçin.");
                news.AddRange(r.NewTypes.Select(t => new NewDefectSpec(t!.Trim(), r.Photo)));
            }
            continues = AnyContinues(regions);
        }
        else
        {
            var photo = active.Count == 1 && defect.Photos.Count >= 2 ? active[0] : null;
            PhotoOutcome mapped;
            switch (input.Outcome)
            {
                case ReinspectOutcome.Conforms:
                    continues = false;
                    mapped = PhotoOutcome.Conforms;
                    break;
                case ReinspectOutcome.Same:
                    continues = true;
                    mapped = PhotoOutcome.Continues;
                    break;
                default:
                    if (!ValidNewTypes(input.NewTypes))
                        throw new InvalidOperationException("Yeni hata tipini seçin.");
                    continues = false;
                    mapped = PhotoOutcome.NewDefect;
                    news.AddRange(input.NewTypes!.Select(t => new NewDefectSpec(t!.Trim(), photo)));
                    break;
            }
            if (photo is not null)
                outcomes = new Dictionary<string, PhotoOutcome> { [photo] = mapped };
        }

        var measurements = continues ? Merge(before, input.Entered, input.HighMetalStillThere) : null;

        // Devam eden parçalar (kutu önceden mevcut değerle dolu olduğundan içeriği cevaptır). null = pencere sormadı, parçalar değişmez.
        if (measurements is not null && input.ContinuingParts is not null)
        {
            if (PartList.ValidateContinuing(defect.AffectedParts, input.ContinuingParts) is { } partsError)
                throw new InvalidOperationException(partsError);
            measurements = measurements with { Parts = PartList.Normalize(input.ContinuingParts) };
        }

        // High metal giderildiyse ve not yazılmadıysa otomatik not: kalan ana kusurun kararı buna göre verilir.
        var note = noteText ?? (continues && before.HighMetal && measurements is { HighMetal: false }
            ? "High metal giderildi; ana kusur devam ediyor."
            : null);

        return new ReinspectResult(continues, measurements, outcomes, news, note);
    }

    /// <summary>Kutudaki parça listesi mevcut listeden farklı mı (geçersiz girdi değişiklik sayılmaz). Ölçüm girilmeden kaydetmeye izin verir.</summary>
    public static bool PartsChanged(string? current, string? entered)
    {
        try { return PartList.Normalize(entered) != PartList.Normalize(current); }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// "Kusurun devam ettiği parçalar" kutusunun canlı satırı (pencere ve test aynı kuralı kullanır).
    /// Geçersizse hata mesajı (Valid=false; Kaydet kapalı kalır), geçerli ve parça giderildiyse "Giderildi: 2, 4", aksi hâlde boş.
    /// </summary>
    public static (bool Valid, string Text) PartsFeedback(string? current, string? entered)
    {
        if (PartList.ValidateContinuing(current, entered) is { } error)
            return (false, error);
        var removed = PartList.Removed(current, entered);
        return (true, removed.Count == 0 ? "" : $"Giderildi: {PartList.Join(removed)}");
    }

    /// <summary>Canlı değişim satırı (artış / azalış). Ölçüm ya da high metal cevabı yoksa yönlendirir.</summary>
    public static string DeltaText(MeasurementSnapshot before, MeasurementSnapshot merged, bool anyValue, bool highMetalAnswered, LengthUnit unit = LengthUnit.Mm)
    {
        if (!anyValue && !highMetalAnswered)
            return "Değişimi görmek için ölçüm girin.";

        var now = new Defect
        {
            Depth = merged.Depth, Width = merged.Width, Length = merged.Length, Radius = merged.Radius,
            AngleDeg = merged.AngleDeg, Color = merged.Color, HighMetal = merged.HighMetal,
            Custom = merged.Custom?.ToList(),
            AffectedParts = merged.Parts ?? before.Parts,
        };
        return string.Join("\n", MeasurementCompare.Lines(before, now, unit));
    }
}
