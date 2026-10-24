namespace MuayeneKayit.Core;

/// <summary>HTML raporu PDF'e çeviren port. Uygulama Inspector'dadır (Edge).</summary>
public interface IPdfConverter
{
    /// <summary>Başarılıysa null, değilse kısa hata açıklaması döner.</summary>
    Task<string?> TryConvertAsync(string htmlPath, string pdfPath);
}
