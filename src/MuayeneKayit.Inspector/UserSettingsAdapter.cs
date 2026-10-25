using System.Text.Json;
using System.Text.Json.Nodes;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kullanıcı ayarlarını (ölçü birimi, tema) exe'nin yanındaki MuayeneKayit.settings.json dosyasında saklar.
/// Dosya bu bilgisayara özeldir. Yazarken dosya JsonNode olarak okunur ve yalnızca "unit" / "theme" değişir;
/// rootPath ve diğer alanlar korunur. Yazma hatası uygulamayı düşürmez, bellekteki değer geçerli kalır.
/// </summary>
internal sealed class UserSettingsAdapter : IUserSettings
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, AppSettings.FileName);

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
            if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject obj)
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
            var obj = new JsonObject();
            if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject existing)
                obj = existing;

            obj[key] = value;
            AtomicFile.WriteAllText(FilePath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
                                    or InvalidOperationException or FormatException)
        {
            // Salt okunur klasör ya da bozuk dosya: ayar bu oturumda bellekte geçerli kalır, uygulama kapanmaz.
            // Bozuk dosya üzerine yazılmaz (rootPath kaybolmasın).
        }
    }
}