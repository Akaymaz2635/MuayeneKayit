using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MuayeneKayit.Admin;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

public partial class App : Application
{
    private Window? _window;

    /// <summary>Bağımlılık kapsayıcısı. Ekranlar ve ViewModel'ler buradan oluşturulur.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        // Tema Application düzeyinde yalnızca burada, başlatmadan önce verilebilir. Pencere içeriği çalışırken
        // MainWindow kökündeki RequestedTheme ile değişir; burası kod ile çözülen sistem fırçalarının başlangıç temasıdır.
        RequestedTheme = UserSettingsAdapter.LoadTheme() == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var root = AppSettings.LoadRootPath();
        var services = new ServiceCollection();

        // Depo ve denetim kaydı: ViewModel portlara (arayüzlere) bağlanır.
        services.AddSingleton(new SessionStore(root));
        services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<SessionStore>());
        services.AddSingleton<IAuditTrail>(sp => new FileAuditTrail(sp.GetRequiredService<SessionStore>()));

        // Kusur tipi şablonları admin panelinde tanımlanır (_config); yoksa varsayılanlar.
        services.AddSingleton(SharedConfig.LoadDefectTypes(root));
        // Admin'de tanımlanan ek ölçüm alanları (ör. Yükseklik); yoksa boş.
        services.AddSingleton(SharedConfig.LoadCustomFields(root));

        // Diyaloglar: akış ViewModel'de, çizim WinUiDialogService'te (pencere ana pencereye bağlanır).
        services.AddSingleton<WinUiDialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<WinUiDialogService>());

        // Dosya seçici, kamera / düzenleyici pencereleri ve PDF dönüştürücü: ViewModel bunları portlardan kullanır.
        services.AddSingleton<WinUiFilePicker>();
        services.AddSingleton<IFilePicker>(sp => sp.GetRequiredService<WinUiFilePicker>());
        services.AddSingleton<WinUiWindowService>();
        services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WinUiWindowService>());
        services.AddSingleton<IPdfConverter, EdgePdfConverter>();

        // Kullanıcı ayarları (birim, tema): bu bilgisayarın ayar dosyasında.
        services.AddSingleton<IUserSettings, UserSettingsAdapter>();
        services.AddSingleton<UserSettingsViewModel>();

        // Muayeneci Windows oturumundan alınabilir (admin panelinden açılır).
        services.AddSingleton<IWindowsUser, WindowsUserAdapter>();

        // Admin paneli: sol menüdeki "Admin" ile ayrı pencerede, PIN ile açılır. Kendi ortak alan bağlamı vardır
        // (admin yolu değiştirirse muayene ekranı programı yeniden açınca yeni yolu kullanır).
        services.AddSingleton<IAdminSettings, AdminSettingsAdapter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IShell, ShellService>();
        services.AddSingleton(new AdminContext(root));
        services.AddSingleton<WinUiConfirmService>();
        services.AddSingleton<IConfirmService>(sp => sp.GetRequiredService<WinUiConfirmService>());
        services.AddSingleton<WinUiFileSaver>();
        services.AddSingleton<IFileSaver>(sp => sp.GetRequiredService<WinUiFileSaver>());
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<RecordsViewModel>();
        services.AddSingleton<TypesViewModel>();
        services.AddSingleton<DecisionTextsViewModel>();
        services.AddSingleton<MaintenanceViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AdminShellViewModel>();
        services.AddTransient<AdminWindow>();

        services.AddSingleton<InspectionViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
