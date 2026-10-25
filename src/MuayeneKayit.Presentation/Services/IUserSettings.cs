using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Uygulama teması. Admin etkilenmez; yalnızca Inspector'da kullanılır.</summary>
public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Bu bilgisayara özel kullanıcı ayarları (ölçü birimi, tema). Dosyaya yazılır, ortak klasöre değil.
/// Birim yalnızca yeni kusurlara uygulanır; mevcut kusurlar girildiği birimle kalır.
/// </summary>
public interface IUserSettings
{
    /// <summary>Yeni kusurlara yazılacak ölçü birimi. Atama dosyaya yazılır.</summary>
    LengthUnit Unit { get; set; }

    AppTheme Theme { get; set; }
}