using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// PIN girişi. İlk kurulumda PIN belirlenir (6-12 rakam, tekrar eşleşmeli); sonra 5 hatalı denemede
/// 60 saniye beklenir. Girişler denetim kaydına yazılır.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromSeconds(60);

    private readonly AdminContext _ctx;
    private readonly IClock _clock;
    private int _failed;
    private DateTime _lockedUntil = DateTime.MinValue;

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

    /// <summary>Giriş ekranını (yeniden) hazırlar; message varsa hata satırında gösterilir.</summary>
    public void Reset(string? message)
    {
        var first = !SafeHasPin();
        IsFirstRun = first;
        Hint = first
            ? "İlk kurulum: admin PIN'i belirleyin (6-12 rakam). PIN ortak alanda yalnızca özet olarak saklanır."
            : "Devam etmek için admin PIN'ini girin.";
        ButtonText = first ? "PIN'i belirle ve gir" : "Giriş";
        Error = message ?? "";
        RootText = $"Ortak alan: {_ctx.Root}";
        Pin = PinRepeat = "";
    }

    private bool SafeHasPin()
    {
        try { return SharedConfig.HasPin(_ctx.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public void Login()
    {
        try
        {
            var now = _clock.Now;
            if (now < _lockedUntil)
            {
                Error = $"Çok fazla hatalı deneme. {(int)(_lockedUntil - now).TotalSeconds + 1} sn bekleyin.";
                return;
            }

            if (!SharedConfig.HasPin(_ctx.Root))
            {
                if (Pin != PinRepeat)
                {
                    Error = "PIN'ler aynı değil.";
                    return;
                }
                SharedConfig.SetPin(_ctx.Root, Pin);
                _ctx.Audit.Append("admin", "admin.pin.set", "İlk PIN belirlendi");
            }
            else if (!SharedConfig.VerifyPin(_ctx.Root, Pin))
            {
                _failed++;
                if (_failed >= MaxAttempts)
                {
                    _failed = 0;
                    _lockedUntil = now + LockoutTime;
                    _ctx.Audit.Append("admin", "admin.login.lockout", Environment.MachineName);
                }
                Error = "PIN hatalı.";
                Pin = "";
                return;
            }

            _failed = 0;
            _ctx.Audit.Append("admin", "admin.login", Environment.MachineName);
            LoggedIn?.Invoke();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                    or InvalidDataException or TimeoutException or InvalidOperationException)
        {
            Error = ex.Message;
        }
    }
}
