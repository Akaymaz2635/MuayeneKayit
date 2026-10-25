namespace MuayeneKayit.Presentation;

/// <summary>Admin kurulum ayarı portu: exe'nin yanındaki ayar dosyasındaki ortak alan yolu.</summary>
public interface IAdminSettings
{
    string LoadRootPath();

    void SaveRootPath(string root);
}
