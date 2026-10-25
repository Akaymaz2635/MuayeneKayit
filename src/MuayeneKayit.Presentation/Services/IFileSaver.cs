namespace MuayeneKayit.Presentation;

/// <summary>Kaydetme penceresi portu. Vazgeçilirse null döner.</summary>
public interface IFileSaver
{
    /// <summary>CSV dosyası için kayıt yolu seçtirir.</summary>
    Task<string?> PickCsvPathAsync(string suggestedName);
}
