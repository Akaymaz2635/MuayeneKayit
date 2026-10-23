using System.Text.Json;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kurulum ayarları. Ortak alan yolu teknisyen ekranında gösterilmez; exe'nin yanındaki
/// MuayeneKayit.settings.json dosyasından okunur ve yalnızca admin tarafından değiştirilir.
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
}
