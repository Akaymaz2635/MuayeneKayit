namespace MuayeneKayit.Presentation;

/// <summary>Kusur listesindeki bir satır: numara rozeti, tip, durum etiketi, fotoğraf sayısı ve sıradaki adım.</summary>
public sealed class DefectItemViewModel
{
    public int Id { get; init; }
    public string Number { get; init; } = "";
    public string Type { get; init; } = "";
    public string StateText { get; init; } = "";
    public Tone Tone { get; init; }
    public string PhotoText { get; init; } = "";
    public string NextStep { get; init; } = "";

    /// <summary>Ekran okuyucu ve UI testleri satırı bu adla bulur.</summary>
    public override string ToString() => $"{Number} {Type}, {StateText}";
}
