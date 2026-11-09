using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>
/// Ayar portunu kurulum ayar dosyasına (<see cref="InstallSettings"/>) bağlar: exe'nin klasöründeki ya da üst
/// klasörlerdeki MuayeneKayit.settings.json. Muayene ekranı aynı dosyayı okur.
/// </summary>
public sealed class AdminSettingsAdapter : IAdminSettings
{
    public string LoadRootPath() => InstallSettings.LoadRootPath(AppContext.BaseDirectory);

    public void SaveRootPath(string root) => InstallSettings.SaveRootPath(AppContext.BaseDirectory, root);
}
