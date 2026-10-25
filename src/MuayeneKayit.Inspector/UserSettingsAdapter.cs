using System.Text.Json;
using System.Text.Json.Nodes;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kullanıcı ayarlarını (ölçü birimi, tema) kullanıcının kendi klasöründe (%LOCALAPPDATA%\MuayeneKayit) saklar.
/// Program klasörüne yazılmaz: program ağdan dağıtıldığında ya da güncellendiğinde ayarlar ezilmez, kullanıcılar
/// birbirinin ayarını değiştirmez. Eski sürümde exe'nin yanındaki dosyaya yazılan seçim ilk açılışta oradan okunur.
/// Yazma hatası uygulamayı düşürmez, bellekteki değer geçerli kalır.
/// </summary>
internal sealed class UserSettingsAdapter : IUserSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuayeneKayit", "user-settings.json");

    /// <summary>Eski sürümlerin birim / tema yazdığı yer (exe'nin yanındaki kurulum ayarı).</summary>
    private static string LegacyPath => Path.Combine(AppContext.BaseDirectory, AppSettings.FileName);

    private LengthUnit _unit;
    private AppTheme _theme;

    public UserSettingsAdapter()
    {
        (_unit, _theme) = Load();
    }

    public LengthUnit Unit
    {
        get => _unit;
        set
        {
            _unit = value;
            Save("unit", value == LengthUnit.Inch ? "inch" : "mm");
        }
    }

    public AppTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Save("theme", value == AppTheme.Dark ? "dark" : "light");
        }
    }

    /// <summary>Açılışta tema için (App oluşturulurken, servisler kurulmadan önce).</summary>
    public static AppTheme LoadTheme() => Load().Theme;

    private static (LengthUnit Unit, AppTheme Theme) Load()
    {
        var unit = LengthUnit.Mm;
        var theme = AppTheme.Light;

        try
        {
            var path = File.Exists(FilePath) ? FilePath : LegacyPath;
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject obj)
            {
                if (string.Equals(obj["unit"]?.GetValue<string>(), "inch", StringComparison.OrdinalIgnoreCase))
                    unit = LengthUnit.Inch;
                if (string.Equals(obj["theme"]?.GetValue<string>(), "dark", StringComparison.OrdinalIgnoreCase))
                    theme = AppTheme.Dark;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
                                    or InvalidOperationException or FormatException)
        {
            // Bozuk ya da okunamayan ayar dosyası uygulamayı durdurmaz; varsayılanlar kullanılır.
        }

        return (unit, theme);
    }

    private static void Save(string key, string value)
    {
        try
        {
            // İlk yazmada diğer ayar da (eski yerden) taşınır: yalnızca değişen anahtar yazılıp öbürü kaybolmasın.
            var (unit, theme) = Load();
            var obj = new JsonObject
            {
                ["unit"] = unit == LengthUnit.Inch ? "inch" : "mm",
                ["theme"] = theme == AppTheme.Dark ? "dark" : "light",
            };
            obj[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteAllText(FilePath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
                                    or InvalidOperationException or FormatException)
        {
            // Yazılamayan klasör: ayar bu oturumda bellekte geçerli kalır, uygulama kapanmaz.
        }
    }
}