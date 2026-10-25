using MuayeneKayit.Core;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>
/// Admin paneli. PIN ile açılır; kusur tipi şablonlarını, ortak alan bakımını ve tüm kayıtların
/// salt okunur görünümünü yönetir. Muayene kayıtlarını değiştirmez.
/// Mantık AdminShellViewModel ve alt ViewModel'lerindedir; burası bağlama, kart çizimi, boşta kilit
/// zamanlayıcısı ve gezinme ile sınırlıdır.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    /// <summary>Gezinme öğesi programla seçilirken SelectionChanged ViewModel'e geri yazılmaz.</summary>
    private bool _syncingNav;

    public AdminShellViewModel Shell { get; }

    public MainWindow(AdminShellViewModel shell, WinUiConfirmService confirm, WinUiFileSaver saver)
    {
        Shell = shell;
        InitializeComponent();
        Title = $"MuayeneKayit Admin {AppInfo.Version}";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));

        // Onay pencereleri ana pencerenin kökünde açılır; kaydetme seçicisi ana pencereye bağlanır.
        confirm.Attach(() => Nav.XamlRoot);
        saver.Attach(() => this);

        Shell.Notify += (message, kind) => Show(message, SeverityFor(kind));
        Shell.PropertyChanged += Shell_PropertyChanged;
        Shell.Records.PropertyChanged += Records_PropertyChanged;

        // Boşta kalan oturum kilitlenir: admin ekranı açık bırakılırsa başkası kullanamaz.
        Root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => Shell.RegisterInput()), true);
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => Shell.RegisterInput()), true);
        _idleTimer.Tick += (_, _) => Shell.CheckIdle();
        _idleTimer.Start();

        PinBox.Focus(FocusState.Programmatic);
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
