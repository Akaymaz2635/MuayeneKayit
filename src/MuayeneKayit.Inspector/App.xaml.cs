using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
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

        services.AddSingleton<InspectionViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
