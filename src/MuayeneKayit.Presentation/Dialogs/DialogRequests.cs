using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

// Diyalog istek ve sonuç kayıtları: yalnızca veri. Pencereyi çizmek için gereken her şey istekte gelir.

/// <summary>Bölge (kusurun bir fotoğrafı): etiket "3.1", küçük resim dosyasının tam yolu (varsa temiz kopya).</summary>
public sealed record PhotoRegion(string Photo, string Label, string ThumbnailPath);

/// <summary>
/// Karar penceresi isteği. Hangi alanların sorulacağını ve zorunluluk kurallarını ViewModel belirler.
/// <paramref name="Regions"/>: kusurun 2 ya da daha fazla aktif bölgesi varsa dolu ("Bu karar hangi bölgeler için?").
/// </summary>
public sealed record DecisionRequest(
    int DefectId,
    string DefectType,
    string StateText,
    Decision Decision,
    string Label,
    string EffectText,
    string? TargetHeader,
    string? TargetDefault,
    bool NeedsReference,
    bool NoteRequired,
    bool NeedsReworkScope,
    IReadOnlyList<PhotoRegion>? Regions = null);

/// <summary>
/// Karar penceresinin sonucu. Boş alanlar null gelir (kırpılmış metin).
/// <paramref name="ContinuingPhotos"/>: bölge sorulduysa kararın geçerli olduğu (işaretli) bölgeler; işaretsizler uygun sayılır.
/// </summary>
public sealed record DecisionInput(string? Target, string? Reference, string? Note, ReworkScope? Scope,
    IReadOnlyList<string>? ContinuingPhotos = null);

/// <summary>Rework (ya da reinspect) sonrası muayene penceresi isteği.</summary>
/// <param name="Fields">Ölçüm alanları: şablondakiler ve eski değeri olanlar (renk hariç).</param>
/// <param name="Unit">Kusurun ölçü birimi; başlıklar ve karşılaştırma bununla yazılır.</param>
/// <param name="Regions">Aktif bölgeler; yalnızca 2 ya da daha fazlaysa dolu (her biri için ayrı sonuç sorulur).</param>
/// <param name="CustomFields">Ek ölçüm alanları: şablondakiler ve eski değeri olanlar.</param>
public sealed record ReinspectRequest(
    int DefectId,
    string DefectType,
    bool IsRework,
    bool HighMetalOnlyScope,
    string? TargetOperation,
    MeasurementSnapshot Before,
    IReadOnlyList<MeasureField> Fields,
    bool ShowColor,
    IReadOnlyList<string> TypeNames,
    LengthUnit Unit = LengthUnit.Mm,
    IReadOnlyList<PhotoRegion>? Regions = null,
    IReadOnlyList<CustomFieldDef>? CustomFields = null);

/// <summary>Rework sonrası muayenenin sonucu.</summary>
public enum ReinspectOutcome
{
    Conforms,
    Same,
    New,
}

/// <summary>
/// Bir bölgenin penceredeki seçimi. Outcome seçilmediyse null (pencerede varsayılan seçimsiz);
/// NewTypes: "Yeni hata" seçilen bölgede açılacak yeni kusur tipleri (satırda tip seçilmediyse null).
/// </summary>
public sealed record RegionChoice(string Photo, PhotoOutcome? Outcome, IReadOnlyList<string?> NewTypes);

/// <summary>
/// Rework sonrası pencerenin sonucu. Entered: kutulara yazılan ham değerler, boşlar null.
/// Aktif bölge 0 ya da 1 ise Outcome ve (Yeni hata için) NewTypes geçerlidir; 2+ ise Regions geçerlidir
/// (Outcome o zaman bölge seçimlerinden türetilmiş değerdir, kullanılmaz).
/// </summary>
public sealed record ReinspectInput(
    ReinspectOutcome Outcome,
    MeasurementSnapshot Entered,
    bool? HighMetalStillThere,
    IReadOnlyList<string?>? NewTypes,
    string? Note,
    IReadOnlyList<RegionChoice>? Regions = null);

/// <summary>Son kararı geri alma penceresi isteği. ChildrenToVoid: kararla açılan, geri alınınca void edilecek yeni kusurlar.</summary>
public sealed record UndoRequest(
    int DefectId,
    string DefectType,
    string DecisionLabel,
    DateTime DecidedAt,
    string DecidedBy,
    IReadOnlyList<int> ChildrenToVoid);
