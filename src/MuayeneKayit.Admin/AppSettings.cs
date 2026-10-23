using System.Text.Json;

namespace MuayeneKayit.Admin;

/// <summary>
/// Admin kurulum ayarları: exe'nin yanındaki MuayeneKayit.settings.json (ortak alan yolu).
/// Muayene exe'leri kendi klasöründeki aynı adlı dosyayı okur.
/// </summary>
internal static class AppSettings
{
    public const string FileName = "MuayeneKayit.settings.json";

    public static string LoadRootPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);

        try
        {
            if (File.Exists(path))
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
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Bozuk ya da okunamayan ayar dosyası uygulamayı durdurmaz; varsayılan yol kullanılır.
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "MuayeneKayitDemo");
    }

    public static void SaveRootPath(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || root.Trim().Length > 260)
            throw new ArgumentException("Ortak alan yolu geçersiz.");

        var json = JsonSerializer.Serialize(new { rootPath = root.Trim() }, new JsonSerializerOptions { WriteIndented = true });
        MuayeneKayit.Core.AtomicFile.WriteAllText(Path.Combine(AppContext.BaseDirectory, FileName), json);
    }
}
