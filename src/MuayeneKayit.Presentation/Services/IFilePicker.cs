namespace MuayeneKayit.Presentation;

/// <summary>Dosya seçme penceresi portu. Vazgeçilirse null döner.</summary>
public interface IFilePicker
{
    /// <summary>JPG / PNG dosyası seçtirir ve yolunu döndürür.</summary>
    Task<string?> PickImageAsync();
}
