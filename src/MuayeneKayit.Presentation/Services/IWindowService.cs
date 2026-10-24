using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Kamera penceresinin açılış bilgisi: fotoğraflar bu kusura kaydedilir.</summary>
public sealed record CameraRequest(string Target, int DefectId);

/// <summary>Kameradan çekilen geçici fotoğraf; çerçeve açıksa ortadaki kare kusur işareti olur.</summary>
public sealed record CapturedPhotoData(string TempPath, int Width, int Height, bool Framed, double FrameFraction);

/// <summary>Fotoğraf düzenleyicinin açılış bilgisi.</summary>
public sealed record PhotoEditorRequest(
    string Title,
    string DefectsDir,
    string Photo,
    PhotoMarkup? Existing,
    IReadOnlyList<int> Labels,
    int CurrentId);

/// <summary>Düzenleyiciden dönen işaretler ve (varsa) arka planı kaldırılmış JPEG.</summary>
public sealed record PhotoEditResultData(PhotoMarkup Markup, byte[]? NewCleanJpeg);

/// <summary>Kamera, fotoğraf düzenleyici pencereleri ve dosya açma portu. Çizim ve pencere yönetimi View'dadır.</summary>
public interface IWindowService
{
    /// <summary>
    /// Kamera penceresini açar (aynı anda tek pencere). onSave fotoğrafı kaydedip dosya adını döndürür;
    /// pencere kapanınca onClosed, kaydedilen dosya adıyla (kaydedilmediyse null) çağrılır.
    /// </summary>
    void OpenCamera(CameraRequest request, Func<CapturedPhotoData, string> onSave, Action<string?> onClosed);

    /// <summary>Fotoğraf düzenleyiciyi açar (aynı anda tek pencere).</summary>
    void OpenPhotoEditor(PhotoEditorRequest request, Action<PhotoEditResultData> onSave);

    /// <summary>Dosyayı varsayılan uygulamasıyla açar (rapor, görüntüleme).</summary>
    void OpenFile(string path);
}
