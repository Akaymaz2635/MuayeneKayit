using CommunityToolkit.Mvvm.ComponentModel;

namespace MuayeneKayit.Presentation;

/// <summary>Admin panelindeki sayfalar (gezinme etiketleriyle aynı).</summary>
public enum AdminPage
{
    Records,
    Types,
    Texts,
    Maintenance,
    Settings,
}

/// <summary>
/// Admin kabuğu: giriş / kilit durumu, boşta kilit, sayfa seçimi ve alt ViewModel'ler.
/// Alt ViewModel'lerin bildirimleri tek durum çubuğuna iletilir.
/// </summary>
public sealed partial class AdminShellViewModel : ObservableObject
{
    public static readonly TimeSpan IdleLock = TimeSpan.FromMinutes(10);

    private readonly AdminContext _ctx;
    private readonly IClock _clock;
    private DateTime _lastInput;

    public AdminShellViewModel(
        AdminContext ctx,
        IClock clock,
        LoginViewModel login,
        RecordsViewModel records,
        TypesViewModel types,
        DecisionTextsViewModel texts,
        MaintenanceViewModel maintenance,
        SettingsViewModel settings)
    {
        _ctx = ctx;
        _clock = clock;
        _lastInput = clock.Now;
        Login = login;
        Records = records;
        Types = types;
        Texts = texts;
        Maintenance = maintenance;
        Settings = settings;

        login.LoggedIn += Enter;
        settings.LockRequested += message => Lock(message);
        types.Saved += names => records.SetTypes(names);

        records.Notify += Forward;
        types.Notify += Forward;
        texts.Notify += Forward;
        maintenance.Notify += Forward;
        settings.Notify += Forward;
    }

    /// <summary>Durum çubuğu bildirimi.</summary>
    public event Action<string, StatusKind>? Notify;

    public LoginViewModel Login { get; }
    public RecordsViewModel Records { get; }
    public TypesViewModel Types { get; }
    public DecisionTextsViewModel Texts { get; }
    public MaintenanceViewModel Maintenance { get; }
    public SettingsViewModel Settings { get; }

    public string Root => _ctx.Root;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedOut))]
    private bool _isLoggedIn;

    public bool IsLoggedOut => !IsLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordsPage), nameof(IsTypesPage), nameof(IsTextsPage), nameof(IsMaintenancePage), nameof(IsSettingsPage))]
    private AdminPage _page = AdminPage.Records;

    public bool IsRecordsPage => Page == AdminPage.Records;
    public bool IsTypesPage => Page == AdminPage.Types;
    public bool IsTextsPage => Page == AdminPage.Texts;
    public bool IsMaintenancePage => Page == AdminPage.Maintenance;
    public bool IsSettingsPage => Page == AdminPage.Settings;

    /// <summary>Kullanıcı bir şey yaptı (fare / tuş): boşta kilit sayacı sıfırlanır.</summary>
    public void RegisterInput() => _lastInput = _clock.Now;

    /// <summary>Zamanlayıcı çağırır: giriş yapılmışsa ve 10 dakikadan uzun süredir işlem yoksa kilitler.</summary>
    public void CheckIdle()
    {
        if (IsLoggedIn && _clock.Now - _lastInput > IdleLock)
            Lock("10 dakika işlem yapılmadığı için kilitlendi.");
    }

    /// <summary>Oturumu kapatır ve giriş ekranını gösterir.</summary>
    public void Lock(string? message)
    {
        Login.Reset(message);
        IsLoggedIn = false;
        OnPropertyChanged(nameof(Root));
    }

    private void Enter()
    {
        _lastInput = _clock.Now;
        Types.Load();
        Records.SetTypes(Types.TypeNames);
        Settings.Load();
        IsLoggedIn = true;
        Navigate("records");
        if (Login.PostLoginNotice.Length > 0)
            Forward(Login.PostLoginNotice, StatusKind.Warning);
    }

    /// <summary>Gezinme etiketine göre sayfayı açar ("lock" oturumu kilitler).</summary>
    public void Navigate(string? tag)
    {
        switch (tag)
        {
            case "lock":
                Lock("Kilitlendi.");
                return;
            case "records":
                Page = AdminPage.Records;
                Records.Refresh();
                break;
            case "types":
                Page = AdminPage.Types;
                break;
            case "texts":
                Page = AdminPage.Texts;
                Texts.Load();
                break;
            case "maint":
                Page = AdminPage.Maintenance;
                Maintenance.LoadAudit();
                break;
            case "settings":
                Page = AdminPage.Settings;
                break;
        }
    }

    private void Forward(string message, StatusKind kind) => Notify?.Invoke(message, kind);
}
