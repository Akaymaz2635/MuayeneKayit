using MuayeneKayit.Core;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>
/// Admin paneli: muayene programının sol menüsündeki "Admin" ile açılan ayrı pencere. PIN ile açılır; kusur tipi
/// şablonlarını, ortak alan bakımını ve tüm kayıtların salt okunur görünümünü yönetir. Muayene kayıtlarını değiştirmez.
/// Pencere kapanınca oturum kilitlenir; bir sonraki açılışta PIN yeniden sorulur.
/// Mantık AdminShellViewModel ve alt ViewModel'lerindedir; burası bağlama, kart çizimi, boşta kilit
/// zamanlayıcısı ve gezinme ile sınırlıdır.
/// </summary>
public sealed partial class AdminWindow : Window
{
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    /// <summary>Gezinme öğesi programla seçilirken SelectionChanged ViewModel'e geri yazılmaz.</summary>
    private bool _syncingNav;

    public AdminShellViewModel Shell { get; }

    /// <summary>
    /// Kusur tipleri ya da ortak alan bu pencerede değişti: muayene ekranı bunları açılışta okuduğu için
    /// programın yeniden açılması gerekir.
    /// </summary>
    public bool RestartNeeded { get; private set; }

    public AdminWindow(AdminShellViewModel shell, WinUiConfirmService confirm, WinUiFileSaver saver)
    {
        Shell = shell;
        InitializeComponent();
        Title = $"MuayeneKayit Admin {AppInfo.Version}";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));

        // Onay pencereleri bu pencerenin kökünde açılır; kaydetme seçicisi bu pencereye bağlanır.
        confirm.Attach(() => Nav.XamlRoot);
        saver.Attach(() => this);

        // ViewModel'ler uygulama boyunca yaşar; pencere kapanınca abonelikler bırakılır.
        Shell.Notify += Shell_Notify;
        Shell.PropertyChanged += Shell_PropertyChanged;
        Shell.Records.PropertyChanged += Records_PropertyChanged;
        Shell.Types.Saved += Types_Saved;
        Shell.Settings.LockRequested += Settings_LockRequested;

        // Boşta kalan oturum kilitlenir: admin ekranı açık bırakılırsa başkası kullanamaz.
        Root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => Shell.RegisterInput()), true);
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => Shell.RegisterInput()), true);
        _idleTimer.Tick += IdleTimer_Tick;
        _idleTimer.Start();

        Closed += AdminWindow_Closed;
        PinBox.Focus(FocusState.Programmatic);
    }

    private void Shell_Notify(string message, StatusKind kind) => Show(message, SeverityFor(kind));

    private void Types_Saved(IReadOnlyList<string> names) => RestartNeeded = true;

    private void Settings_LockRequested(string message) => RestartNeeded = true;

    private void IdleTimer_Tick(object? sender, object e) => Shell.CheckIdle();

    private void AdminWindow_Closed(object sender, WindowEventArgs args)
    {
        _idleTimer.Stop();
        _idleTimer.Tick -= IdleTimer_Tick;
        Shell.Notify -= Shell_Notify;
        Shell.PropertyChanged -= Shell_PropertyChanged;
        Shell.Records.PropertyChanged -= Records_PropertyChanged;
        Shell.Types.Saved -= Types_Saved;
        Shell.Settings.LockRequested -= Settings_LockRequested;
        Shell.Lock(null);
    }

    private FrameworkElement Root => (FrameworkElement)Content;

    private static InfoBarSeverity SeverityFor(StatusKind kind) => kind switch
    {
        StatusKind.Success => InfoBarSeverity.Success,
        StatusKind.Warning => InfoBarSeverity.Warning,
        StatusKind.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    private void Show(string message, InfoBarSeverity severity)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }

    // ───────────────────────── Giriş ve gezinme ─────────────────────────

    private void Shell_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AdminShellViewModel.IsLoggedIn))
            return;

        if (Shell.IsLoggedIn)
        {
            _syncingNav = true;
            Nav.SelectedItem = Nav.MenuItems[0];
            _syncingNav = false;
        }
        else
        {
            PinBox.Focus(FocusState.Programmatic);
        }
    }

    private void PinBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            Shell.Login.Login();
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingNav)
            return;
        Shell.Navigate((args.SelectedItem as NavigationViewItem)?.Tag as string);
    }

    // ───────────────────────── Kayıtlar ─────────────────────────

    private void Records_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Satırlar en son atanır: özet ve kartlar birlikte yeniden çizilir.
        if (e.PropertyName == nameof(RecordsViewModel.Rows))
            RenderRecords();
    }

    private void RenderRecords()
    {
        var records = Shell.Records;
        SummaryCards.Children.Clear();
        foreach (var card in records.SummaryCards)
            SummaryCards.Children.Add(AdminCards.StatCard(card));

        ResultsPanel.Children.Clear();
        if (records.EmptyText is { } empty)
            ResultsPanel.Children.Add(AdminCards.EmptyMessage(empty));

        foreach (var row in records.Rows)
            ResultsPanel.Children.Add(AdminCards.RecordCard(row, records.OpenFolder, records.OpenLatestReport));
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e) => await Shell.Records.ExportCsvAsync();

    // ───────────────────────── Onaylı işlemler ─────────────────────────

    private async void DeleteType_Click(object sender, RoutedEventArgs e) => await Shell.Types.DeleteAsync();

    private async void ResetTypes_Click(object sender, RoutedEventArgs e) => await Shell.Types.ResetAsync();

    private async void ResetTexts_Click(object sender, RoutedEventArgs e) => await Shell.Texts.ResetAllAsync();

    private async void RemoveLock_Click(object sender, RoutedEventArgs e) => await Shell.Maintenance.RemoveLockAsync();
}
