namespace MuayeneKayit.Core;

/// <summary>Rework sonrası muayenede bulunan yeni bir hata: tipi ve (varsa) bulunduğu bölge (parent'ın fotoğraf adı).</summary>
public sealed record NewDefectSpec(string Type, string? FoundAtPhoto);

/// <summary>
/// Rework (ya da reinspect) sonrası muayenenin işlenecek sonucu. Sonuç kararı alanlardan türer:
/// devam eden bölge yok ve yeni hata yok → kapanır; devam eden varsa aynı kusur (yeni hatalar varsa child'lar da açılır,
/// parent açık kalır); devam eden yok, yeni hata varsa parent yeni kusurlara devredilir.
/// </summary>
/// <param name="ParentContinues">En az bir bölge (ya da bölgesiz kusurun kendisi) devam ediyor.</param>
/// <param name="Measurements">Devam eden kusurun güncel ölçümleri (boş bırakılanlar önceki değerle birleşmiş). Devam yoksa null.</param>
/// <param name="PhotoOutcomes">Bölge sonuçları (fotoğraf adı → sonuç). Bölge sorulmadıysa null.</param>
/// <param name="NewDefects">Açılacak yeni kusurlar; sırayla numaralanır.</param>
public sealed record ReinspectResult(
    bool ParentContinues,
    MeasurementSnapshot? Measurements,
    IReadOnlyDictionary<string, PhotoOutcome>? PhotoOutcomes,
    IReadOnlyList<NewDefectSpec> NewDefects,
    string? Note)
{
    /// <summary>Sonuçtan türeyen karar.</summary>
    public Decision Decision =>
        ParentContinues ? Decision.AfterReworkSameDefect
        : NewDefects.Count > 0 ? Decision.AfterReworkNewDefect
        : Decision.AfterReworkConforms;
}
