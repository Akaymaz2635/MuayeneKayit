using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Karar penceresinin zorunlu alan kuralları.</summary>
public static class DecisionForm
{
    /// <summary>
    /// Not zorunluysa (Reopen) boş olamaz; kapsam sorulduysa (high metal'li rework) seçilmiş olmalı;
    /// bölge sorulduysa (2+ aktif bölge) en az biri işaretli olmalı (işaretsizler uygun sayılır).
    /// </summary>
    public static bool CanSave(bool noteRequired, string? note, bool needsScope, ReworkScope? scope,
        bool needsRegions = false, int checkedRegions = 0) =>
        (!noteRequired || !string.IsNullOrWhiteSpace(note))
        && (!needsScope || scope is not null)
        && (!needsRegions || checkedRegions > 0);

    /// <summary>
    /// Karar penceresindeki bölge soru kuralı: kusurun 2 ya da daha fazla aktif bölgesi varsa ve karar Void / Reopen değilse sorulur.
    /// </summary>
    public static bool AsksRegions(Decision decision, int activeRegions) =>
        activeRegions >= 2 && decision is not (Decision.Void or Decision.Reopen);

    /// <summary>Bölge sonuçları: işaretli bölge → Continues (karar geçerli), işaretsiz → Conforms (uygun sayılır).</summary>
    public static IReadOnlyDictionary<string, PhotoOutcome> RegionOutcomes(IEnumerable<string> activePhotos, IReadOnlyCollection<string> continuing) =>
        activePhotos.ToDictionary(p => p, p => continuing.Contains(p) ? PhotoOutcome.Continues : PhotoOutcome.Conforms);
}
