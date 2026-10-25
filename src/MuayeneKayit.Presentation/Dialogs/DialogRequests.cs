using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

// Diyalog istek ve sonuç kayıtları: yalnızca veri. Pencereyi çizmek için gereken her şey istekte gelir.

/// <summary>Karar penceresi isteği. Hangi alanların sorulacağını ve zorunluluk kurallarını ViewModel belirler.</summary>
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
    bool NeedsReworkScope);

/// <summary>Karar penceresinin sonucu. Boş alanlar null gelir (kırpılmış metin).</summary>
public sealed record DecisionInput(string? Target, string? Reference, string? Note, ReworkScope? Scope);

/// <summary>Rework (ya da reinspect) sonrası muayene penceresi isteği.</summary>
/// <param name="Fields">Ölçüm alanları: şablondakiler ve eski değeri olanlar (renk hariç).</param>
/// <param name="Unit">Kusurun ölçü birimi; başlıklar ve karşılaştırma bununla yazılır.</param>
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
    LengthUnit Unit = LengthUnit.Mm);

/// <summary>Rework sonrası muayenenin sonucu.</summary>
public enum ReinspectOutcome
{
    Conforms,
    Same,
    New,
}

/// <summary>Rework sonrası pencerenin sonucu. Entered: kutulara yazılan ham değerler, boşlar null.</summary>
public sealed record ReinspectInput(
    ReinspectOutcome Outcome,
    MeasurementSnapshot Entered,
    bool? HighMetalStillThere,
    string? ChildType,
    string? Note);

/// <summary>Son kararı geri alma penceresi isteği.</summary>
public sealed record UndoRequest(
    int DefectId,
    string DefectType,
    string DecisionLabel,
    DateTime DecidedAt,
    string DecidedBy,
    int? ChildToVoid);
