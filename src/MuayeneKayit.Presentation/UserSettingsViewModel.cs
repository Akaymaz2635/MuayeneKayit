using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Inspector "Ayarlar" sekmesi: ölçü birimi ve tema. Seçim hemen <see cref="IUserSettings"/>'e yazılır
/// (bu bilgisayarın ayar dosyası). Admin'deki SettingsViewModel'den ayrı bir sınıftır.
/// </summary>
public sealed partial class UserSettingsViewModel : ObservableObject
{
    private readonly IUserSettings _settings;

    public UserSettingsViewModel(IUserSettings settings)
    {
        _settings = settings;
        // Alanlara doğrudan atanır: açılışta ayar dosyasına geri yazılmaz.
        _unitIndex = settings.Unit == LengthUnit.Inch ? 1 : 0;
        _themeIndex = settings.Theme == AppTheme.Dark ? 1 : 0;
    }

    /// <summary>Tema değişti; View temayı pencereye uygular.</summary>
    public event Action<AppTheme>? ThemeChanged;

    /// <summary>Birim değişti; View ölçüm başlıklarını yeniler.</summary>
    public event Action? UnitChanged;

    /// <summary>RadioButtons sırası: 0 = mm, 1 = inch.</summary>
    [ObservableProperty] private int _unitIndex;

    /// <summary>RadioButtons sırası: 0 = Açık, 1 = Koyu.</summary>
    [ObservableProperty] private int _themeIndex;

    public string Description =>
        "Birim yalnızca yeni kusurlara uygulanır; mevcut kayıtlar girildiği birimle kalır.";

    partial void OnUnitIndexChanged(int value)
    {
        // RadioButtons seçimi kaldırırsa (-1) ayar değişmez.
        if (value is < 0 or > 1)
            return;

        _settings.Unit = value == 1 ? LengthUnit.Inch : LengthUnit.Mm;
        UnitChanged?.Invoke();
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (value is < 0 or > 1)
            return;

        var theme = value == 1 ? AppTheme.Dark : AppTheme.Light;
        _settings.Theme = theme;
        ThemeChanged?.Invoke(theme);
    }
}