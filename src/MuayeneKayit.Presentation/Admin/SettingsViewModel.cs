using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Admin "Ayarlar" sayfası: ortak alan yolu ve admin PIN'i.</summary>
public sealed partial class SettingsViewModel : AdminViewModelBase
{
    private readonly AdminContext _ctx;
    private readonly IAdminSettings _settings;

    public SettingsViewModel(AdminContext ctx, IAdminSettings settings)
    {
        _ctx = ctx;
        _settings = settings;
        _rootText = ctx.Root;
    }

    /// <summary>Kök değişti: oturum kilitlenmeli (yeni alandaki PIN ile yeniden giriş). Parametre giriş ekranı mesajı.</summary>
    public event Action<string>? LockRequested;

    [ObservableProperty] private string _rootText;
    [ObservableProperty] private string _oldPin = "";
    [ObservableProperty] private string _newPin = "";
    [ObservableProperty] private string _newPinRepeat = "";

    /// <summary>Muayeneci adı Windows oturumundan (sicil no + AD'deki ad) alınsın mı; tüm muayene bilgisayarlarına uygulanır.</summary>
    [ObservableProperty] private bool _useWindowsUser;

    private bool _loading;

    /// <summary>Kök kutusunu geçerli köke, kimlik anahtarını ortak alandaki ayara getirir (giriş sonrası).</summary>
    public void Load()
    {
        RootText = _ctx.Root;
        _loading = true;
        try
        {
            UseWindowsUser = SharedConfig.LoadUseWindowsUser(_ctx.Root);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Anahtar değişince hemen kaydedilir; muayene ekranları bir sonraki muayene seçiminde uygular.</summary>
    partial void OnUseWindowsUserChanged(bool value)
    {
        if (_loading)
            return;

        Run(() =>
        {
            SharedConfig.SaveUseWindowsUser(_ctx.Root, value);
            _ctx.Audit.Append("admin", "admin.identity", value ? "windows" : "manual");
            Say(value
                ? "Muayeneci artık Windows oturumundan alınacak (sicil no + ad). Muayene ekranları bir sonraki muayene seçiminde uygular."
                : "Muayeneci adı yeniden elle yazılacak.", StatusKind.Success);
        });
    }

    public void SaveRoot() => Run(() =>
    {
        var root = Environment.ExpandEnvironmentVariables(RootText.Trim());
        if (!Directory.Exists(root))
            throw new InvalidOperationException("Klasöre erişilemiyor. Yolu ve ağ bağlantısını kontrol edin.");

        _settings.SaveRootPath(RootText.Trim());
        _ctx.SetRoot(root);
        LockRequested?.Invoke("Ortak alan değişti; yeni alandaki PIN ile tekrar giriş yapın.");
    });

    public void ChangePin() => Run(() =>
    {
        if (!SharedConfig.VerifyPin(_ctx.Root, OldPin))
            throw new InvalidOperationException("Mevcut PIN hatalı.");
        if (NewPin != NewPinRepeat)
            throw new InvalidOperationException("Yeni PIN'ler aynı değil.");

        SharedConfig.SetPin(_ctx.Root, NewPin);
        _ctx.Audit.Append("admin", "admin.pin.change", Environment.MachineName);
        OldPin = NewPin = NewPinRepeat = "";
        Say("PIN değiştirildi.", StatusKind.Success);
    });
}
