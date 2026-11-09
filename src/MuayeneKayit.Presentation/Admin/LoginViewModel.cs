using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// PIN girişi. İlk kurulumda PIN belirlenir (8-12 rakam, tekrar eşleşmeli); sonra 5 hatalı denemede
/// 60 saniye beklenir. Hatalı deneme sayacı ve kilit bu bilgisayarda kalıcıdır (program yeniden açılınca sıfırlanmaz);
/// her hatalı deneme ve giriş denetim kaydına yazılır. admin.json yokken ama ortak alanda daha önce admin kaydı varken
/// "ilk kurulum" sessizce açılmaz: sıfırlama uyarısı gösterilir, açık onay istenir ve "admin.pin.reset" yazılır.
/// Eski 6 haneli PIN'ler girişte kabul edilir; girişten sonra değiştirmesi önerilir.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromSeconds(60);

    private readonly AdminContext _ctx;
    private readonly IClock _clock;

    public LoginViewModel(AdminContext ctx, IClock clock)
    {
        _ctx = ctx;
        _clock = clock;
        Reset(null);
    }

    /// <summary>PIN doğrulandı (ya da ilk PIN belirlendi): admin paneli açılır.</summary>
    public event Action? LoggedIn;

    [ObservableProperty] private string _pin = "";
    [ObservableProperty] private string _pinRepeat = "";
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _hint = "";
    [ObservableProperty] private string _buttonText = "Giriş";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _rootText = "";

    /// <summary>admin.json yok ama daha önce admin kaydı var: onay kutusu gösterilir.</summary>
    [ObservableProperty] private bool _needsResetConfirm;

    /// <summary>Kullanıcı PIN'in sıfırlanmasını açıkça onayladı (yalnızca <see cref="NeedsResetConfirm"/> iken gerekir).</summary>
    [ObservableProperty] private bool _resetConfirmed;

    /// <summary>Girişten sonra gösterilecek uyarı (ör. eski kısa PIN); yoksa boş. Admin kabuğu durum çubuğuna iletir.</summary>
    public string PostLoginNotice { get; private set; } = "";

    /// <summary>Giriş ekranını (yeniden) hazırlar; message varsa hata satırında gösterilir.</summary>
    public void Reset(string? message)
    {
        var first = !SafeHasPin();
        var prior = first && SafePriorActivity();
        IsFirstRun = first;
        NeedsResetConfirm = prior;
        ResetConfirmed = false;
        PostLoginNotice = "";
        Hint = prior
            ? "UYARI: Bu ortak alanda daha önce admin kaydı var ama admin.json bulunamadı. Dosya silinmiş ya da taşınmış olabilir; " +
              "PIN sıfırlanıyor olabilir. Devam ederseniz yeni PIN belirlenir ve işlem denetim kaydına 'admin.pin.reset' olarak yazılır. " +
              "Emin değilseniz sistem yöneticisine haber verin."
            : first
                ? "İlk kurulum: admin PIN'i belirleyin (8-12 rakam). PIN ortak alanda yalnızca özet olarak saklanır."
                : "Devam etmek için admin PIN'ini girin.";
        ButtonText = prior ? "PIN'i sıfırla ve gir" : first ? "PIN'i belirle ve gir" : "Giriş";
        Error = message ?? "";
        RootText = $"Ortak alan: {_ctx.Root}";
        Pin = PinRepeat = "";
    }

    private bool SafeHasPin()
    {
        try { return SharedConfig.HasPin(_ctx.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private bool SafePriorActivity()
    {
        try { return SharedConfig.HasPriorAdminActivity(_ctx.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Denetim satırı yazılamazsa (kilit, erişim) giriş sonucu değişmez; sessizce atlanmaz, hata satırına eklenir.</summary>
    private void TryAudit(string action, string details)
    {
        try
        {
            _ctx.Audit.Append("admin", action, details);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException
                                    or InvalidOperationException or InvalidDataException)
        {
            Error = (Error.Length > 0 ? Error + " " : "") + $"Denetim kaydı yazılamadı: {ex.Message}";
        }
    }

    public void Login()
    {
        try
        {
            var now = _clock.Now;
            var state = LoginThrottle.Load(_ctx.Root);
            if (now < state.LockedUntil)
            {
                Error = $"Çok fazla hatalı deneme. {(int)(state.LockedUntil - now).TotalSeconds + 1} sn bekleyin.";
                return;
            }

            PostLoginNotice = "";
            if (!SharedConfig.HasPin(_ctx.Root))
            {
                var prior = SafePriorActivity();
                if (prior && !ResetConfirmed)
                {
                    Error = "PIN'i sıfırlamak için uyarıyı okuyup onay kutusunu işaretleyin.";
                    return;
                }
                if (Pin != PinRepeat)
                {
                    Error = "PIN'ler aynı değil.";
                    return;
                }
                SharedConfig.SetPin(_ctx.Root, Pin);
                if (prior)
                    _ctx.Audit.Append("admin", "admin.pin.reset", $"PIN sıfırlandı (admin.json yoktu, önceki admin kaydı var): {Environment.MachineName}");
                else
                    _ctx.Audit.Append("admin", "admin.pin.set", "İlk PIN belirlendi");
            }
            else if (!SharedConfig.VerifyPin(_ctx.Root, Pin))
            {
                var failed = state.Failed + 1;
                var locked = failed >= MaxAttempts;
                LoginThrottle.Save(_ctx.Root, locked ? new LoginThrottleState(0, now + LockoutTime) : new LoginThrottleState(failed, DateTime.MinValue));
                Error = "PIN hatalı.";
                Pin = "";
                TryAudit("admin.login.fail", $"{Environment.MachineName} ({failed}/{MaxAttempts})");
                if (locked)
                    TryAudit("admin.login.lockout", Environment.MachineName);
                return;
            }
            else if (Pin.Length < SharedConfig.MinPinLength)
            {
                PostLoginNotice = $"Admin PIN'iniz {SharedConfig.MinPinLength} haneden kısa. Ayarlar sayfasından {SharedConfig.MinPinLength}-{SharedConfig.MaxPinLength} haneli yeni bir PIN belirlemeniz önerilir.";
            }

            LoginThrottle.Clear(_ctx.Root);
            _ctx.Audit.Append("admin", "admin.login", Environment.MachineName);
            LoggedIn?.Invoke();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                    or InvalidDataException or TimeoutException or InvalidOperationException
                                    or FormatException or JsonException)
        {
            Error = ex.Message;
        }
    }
}
