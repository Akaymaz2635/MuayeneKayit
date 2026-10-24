namespace MuayeneKayit.Presentation;

/// <summary>Fotoğraf kartının verisi. Küçük resim yükleme View'dadır.</summary>
public sealed class PhotoItemViewModel
{
    public required string FileName { get; init; }

    /// <summary>Kusur no ve sıra: "6.1".</summary>
    public required string Label { get; init; }

    /// <summary>Gösterilen dosya: temiz kopya varsa o (dosya yoksa orijinal).</summary>
    public required string DisplayFile { get; init; }

    /// <summary>Gösterilen dosyanın tam yolu (küçük resim için).</summary>
    public required string ThumbnailPath { get; init; }

    public required string InfoText { get; init; }

    /// <summary>İşaret sayısı ya da arka plan bilgisi varsa true (bilgi metni vurgulanır).</summary>
    public bool HasInfo { get; init; }

    public bool HasMarks { get; init; }
    public bool IsEditable { get; init; }

    public string Caption => $"{Label}   ·   {FileName}";
    public string EditButtonText => IsEditable ? "İşaretle" : "Görüntüle";
    public string RemoveAutomationName => $"{FileName} fotoğrafını kaldır";
}
