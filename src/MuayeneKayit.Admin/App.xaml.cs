using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

public partial class App : Application
{
    private Window? _window;

    /// <summary>Bağımlılık kapsayıcısı. Ekran ve ViewModel'ler buradan oluşturulur.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Ayar, saat ve kabuk: ViewModel portlara (arayüzlere) bağlanır.
        services.AddSingleton<IAdminSettings, AdminSettingsAdapter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IShell, ShellService>();

        // Ortak durum: kök klasör, depo ve denetim kaydı (kök değişince yeniden kurulur).
        services.AddSingleton(sp => new AdminContext(sp.GetRequiredService<IAdminSettings>().LoadRootPath()));

        // Onay penceresi ve kaydetme seçicisi: çizim View'da, ana pencereye bağlanır.
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
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
