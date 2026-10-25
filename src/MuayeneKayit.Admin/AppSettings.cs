using MuayeneKayit.Core;

namespace MuayeneKayit.Admin;

/// <summary>
/// Admin kurulum ayarı (ortak alan yolu): exe'nin klasöründeki ya da üst klasörlerdeki MuayeneKayit.settings.json
/// (bkz. <see cref="InstallSettings"/>). Muayene exe'leri aynı dosyayı okur.
/// </summary>
internal static class AppSettings
{
    public const string FileName = InstallSettings.FileName;

    public static string LoadRootPath() => InstallSettings.LoadRootPath(AppContext.BaseDirectory);

    public static void SaveRootPath(string root) => InstallSettings.SaveRootPath(AppContext.BaseDirectory, root);
}
