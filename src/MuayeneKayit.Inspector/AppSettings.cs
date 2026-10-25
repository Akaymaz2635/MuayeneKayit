using MuayeneKayit.Core;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kurulum ayarları. Ortak alan yolu teknisyen ekranında gösterilmez; exe'nin klasöründeki ya da üst klasörlerdeki
/// MuayeneKayit.settings.json dosyasından okunur (bkz. <see cref="InstallSettings"/>) ve yalnızca admin tarafından değiştirilir.
/// </summary>
internal static class AppSettings
{
    public const string FileName = InstallSettings.FileName;

    public static string LoadRootPath() => InstallSettings.LoadRootPath(AppContext.BaseDirectory);
}
