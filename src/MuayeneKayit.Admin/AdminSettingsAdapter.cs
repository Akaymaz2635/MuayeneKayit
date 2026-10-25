using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>Ayar portunu exe'nin yanındaki ayar dosyasına (<see cref="AppSettings"/>) bağlar.</summary>
public sealed class AdminSettingsAdapter : IAdminSettings
{
    public string LoadRootPath() => AppSettings.LoadRootPath();

    public void SaveRootPath(string root) => AppSettings.SaveRootPath(root);
}
