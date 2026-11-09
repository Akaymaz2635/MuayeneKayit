using System.Text.Json;

namespace MuayeneKayit.Core;

/// <summary>
/// Kurulum ayarı (ortak veri klasörünün yolu): MuayeneKayit.settings.json. Önce exe'nin klasörüne, bulunamazsa üst
/// klasörlere (en fazla <see cref="MaxLevelsUp"/>) bakılır. Ağ klasöründen çalıştırmada sürüm klasörleri
/// (Program\1.0.0\Inspector-x64) kökteki tek dosyayı (Program\MuayeneKayit.settings.json) kullanır.
/// </summary>
public static class InstallSettings
{
    public const string FileName = "MuayeneKayit.settings.json";
    public const int MaxLevelsUp = 3;

    /// <summary>Ayar dosyasının yolu; hiçbir yerde yoksa null.</summary>
    public static string? Find(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        for (var level = 0; dir is not null && level <= MaxLevelsUp; level++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, FileName);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    /// <summary>Ayardaki ortak klasör; dosya yoksa ya da okunamıyorsa kullanıcı klasöründeki demo klasörü.</summary>
    public static string LoadRootPath(string startDirectory)
    {
        try
        {
            if (Find(startDirectory) is string path)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("rootPath", out var value) &&
                    value.GetString() is string root &&
                    !string.IsNullOrWhiteSpace(root))
                {
                    return Environment.ExpandEnvironmentVariables(root.Trim());
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Bozuk ya da okunamayan ayar dosyası uygulamayı durdurmaz; varsayılan yol kullanılır.
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MuayeneKayitDemo");
    }

    /// <summary>
    /// Okunan dosyaya yazar. Dosya henüz yoksa ağdan çalıştırma düzeninde program klasörünün köküne (surum.txt'nin yanına)
    /// yazar: Admin'den girilen yolu tüm sürümlerdeki Inspector'lar görür. Düzen yoksa exe'nin klasörüne yazar.
    /// </summary>
    public static void SaveRootPath(string startDirectory, string root)
    {
        if (string.IsNullOrWhiteSpace(root) || root.Trim().Length > 260)
            throw new ArgumentException("Ortak alan yolu geçersiz.");

        var json = JsonSerializer.Serialize(new { rootPath = root.Trim() }, new JsonSerializerOptions { WriteIndented = true });
        AtomicFile.WriteAllText(Find(startDirectory) ?? Path.Combine(ProgramRoot(startDirectory) ?? startDirectory, FileName), json);
    }

    /// <summary>Üst klasörlerde (en fazla <see cref="MaxLevelsUp"/>) surum.txt bulunan program kökü; yoksa null.</summary>
    private static string? ProgramRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        for (var level = 0; dir is not null && level <= MaxLevelsUp; level++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, ProgramLayout.VersionFile)))
                return dir.FullName;
        }
        return null;
    }
}
