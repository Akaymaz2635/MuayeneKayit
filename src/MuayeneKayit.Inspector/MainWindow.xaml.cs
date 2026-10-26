using System.ComponentModel;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Muayene ve dashboard ekranı. İş kuralları Core'dadır, ekran akışları ViewModel'lerde; bu sınıf yalnızca arayüzü bağlar:
/// sekme seçimi, durum çubuğu ve dashboard kartlarının çizimi.
/// Karar butonları seçili kusurun durumuna göre açılır/kapanır. Hatalar InfoBar ile gösterilir.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Muayene ekranının durumu ve kuralları.</summary>
    public InspectionViewModel Vm { get; }

    /// <summary>Dashboard'un süzgeçleri, özeti ve kartları.</summary>
    public DashboardViewModel Dash { get; }

    /// <summary>Ayarlar sekmesi: ölçü birimi ve tema.</summary>
    public UserSettingsViewModel Settings { get; }

    /// <summary>Sekme programla seçilirken SelectorBar olayları ViewModel'e geri yazılmaz.</summary>
    private bool _syncingTabs;

    public MainWindow(InspectionViewModel vm, DashboardViewModel dash, UserSettingsViewModel settings,
        IUserSettings userSettings, WinUiDialogService dialogs, WinUiFilePicker filePicker, WinUiWindowService windows)
    {
        Vm = vm;
        Dash = dash;
        Settings = settings;
        InitializeComponent();
        Title = $"MuayeneKayit {AppInfo.Version}";

        // Diyaloglar ana pencerenin kökünde açılır; dosya seçici ana pencereye bağlanır.
        dialogs.Attach(() => Nav.XamlRoot);
        filePicker.Attach(() => this);

        Vm.Notify += Notify;
        Dash.Notify += Notify;
        Vm.StatusCleared += () => Status.IsOpen = false;
        Vm.PropertyChanged += Vm_PropertyChanged;
        Dash.PropertyChanged += Dash_PropertyChanged;
        Dash.OpenRequested += OpenFromDashboard;

        DefectTypeBox.ItemsSource = Vm.TypeNames;
        DefectTypeBox.SelectedIndex = 0;

        // Tema açılışta ayardan, değişince hemen uygulanır. Birim değişince boştaki ölçüm başlıkları yenilenir.
        ApplyTheme(userSettings.Theme);
        Settings.ThemeChanged += ApplyTheme;
        Settings.UnitChanged += Vm.RefreshUnitHeaders;

        Nav.SelectedItem = Nav.MenuItems[0];

        // Ana pencere kapanınca açık kamera penceresi de kapanır (kamera serbest kalır).
        Closed += (_, _) =>
        {
            Vm.AutoSave();
            windows.CloseAll();
        };
    }

    private void Notify(string message, StatusKind kind) => Show(message, SeverityFor(kind));

    /// <summary>Bildirim türünün InfoBar önem derecesi.</summary>
    public static InfoBarSeverity SeverityFor(StatusKind kind) => kind switch
    {
        StatusKind.Success => InfoBarSeverity.Success,
        StatusKind.Warning => InfoBarSeverity.Warning,
        StatusKind.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    /// <summary>ViewModel değişimlerinden yalnızca bağlama ile gösterilemeyenler: sekme ve sekme kalınlığı.</summary>
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InspectionViewModel.SelectedStep):
                ShowTab(TabFor(Vm.SelectedStep));
                break;

            case nameof(InspectionViewModel.Current):
                if (Vm.Current is not null)
                    ShowTab(TabFor(Vm.SelectedStep));
                break;

            case nameof(InspectionViewModel.CurrentStep):
                ApplyTabWeights();
                break;
        }
    }

    // ───────────────────────── Navigasyon ─────────────────────────

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        InspectPanel.Visibility = tag == "inspect" ? Visibility.Visible : Visibility.Collapsed;
        DashPanel.Visibility = tag == "dash" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "dash")
            Dash.Refresh();
    }

    /// <summary>
    /// Temayı kök öğeye (gezinme bölmesinin zemini dahil) ve başlık çubuğuna uygular. Diyaloglar bu temayı kökten alır
    /// (WinUiDialogService.NewDialog ayrıca ActualTheme'i aktarır).
    /// </summary>
    private void ApplyTheme(AppTheme theme)
    {
        Root.RequestedTheme = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        AppWindow.TitleBar.PreferredTheme = theme == AppTheme.Dark
            ? Microsoft.UI.Windowing.TitleBarTheme.Dark
            : Microsoft.UI.Windowing.TitleBarTheme.Light;
    }

    // ───────────────────────── Oturum ─────────────────────────

    private void OpenSession_Click(object sender, RoutedEventArgs e) => Vm.OpenSession();

    /// <summary>
    /// Öneri kutusunda Enter: listeden bir öneri seçildiyse yalnızca o seçilir; seçilmediyse muayene oluşturulur.
    /// </summary>
    private void Suggest_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is not null)
            return;
        sender.IsSuggestionListOpen = false;
        Vm.OpenSession();
    }

    /// <summary>Ölçüm ya da not kutusundan çıkınca form otomatik kaydedilir (değişiklik yoksa hiçbir şey yazılmaz).</summary>
    private void MeasureBox_LostFocus(object sender, RoutedEventArgs e) => Vm.AutoSave();

    private void InspectorBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;
        e.Handled = true;
        Vm.OpenSession();
    }

    // ───────────────────────── Öneriler ─────────────────────────

    private void Suggest_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is AutoSuggestBox box)
            ShowSuggestions(box);
    }

    private void Suggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ShowSuggestions(sender);
    }

    /// <summary>Şu an öneri listesi gösterilen kutu ("×" tıklaması hangi kutunun önerisini gizlediğini buradan bilir).</summary>
    private AutoSuggestBox? _suggestOwner;

    private SuggestLevel LevelOf(AutoSuggestBox box) =>
        ReferenceEquals(box, PartBox) ? SuggestLevel.Part
        : ReferenceEquals(box, OpBox) ? SuggestLevel.Operation
        : SuggestLevel.Serial;

    /// <summary>Öneri listesini ViewModel'den alır ve kutuya verir.</summary>
    private void ShowSuggestions(AutoSuggestBox box)
    {
        _suggestOwner = box;
        var items = Vm.Suggestions(LevelOf(box), box.Text);

        box.ItemsSource = items;
        box.IsSuggestionListOpen = items.Count > 0;
    }

    /// <summary>
    /// Önerideki "×": öneriyi bu bilgisayarda gizler ve listeyi açık hâlde yeniler. Düğme tıklamayı yuttuğu için
    /// öneri seçilmez; kutudaki metin değişmez.
    /// </summary>
    private void HideSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (_suggestOwner is not { } box || (sender as Button)?.Tag is not string value)
            return;

        Vm.HideSuggestion(LevelOf(box), value);
        ShowSuggestions(box);
    }

    // ───────────────────────── Kusur listesi ─────────────────────────

    private void AddDefect_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.AddDefect(DefectTypeBox.SelectedItem as string))
            AddDefectFlyout.Hide();
    }


    // ───────────────────────── Durum renkleri ve vurgu (x:Bind işlevleri) ─────────────────────────

    private static string ToneKey(Tone tone) => tone switch
    {
        Tone.Success => "Success",
        Tone.Attention => "Attention",
        Tone.Neutral => "Neutral",
        Tone.Caution => "Caution",
        _ => "Critical",
    };

    /// <summary>Durum etiketinin arka planı: sistem durum fırçaları (tema ve yüksek kontrast uyumlu).</summary>
    public static Microsoft.UI.Xaml.Media.Brush ToneBackground(Tone tone) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[$"SystemFillColor{ToneKey(tone)}BackgroundBrush"];

    /// <summary>Durum etiketinin yazı rengi.</summary>
    public static Microsoft.UI.Xaml.Media.Brush ToneForeground(Tone tone) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[$"SystemFillColor{ToneKey(tone)}Brush"];

    /// <summary>
    /// Ekranda aynı anda yalnızca sıradaki iş vurgulu (lacivert) olur; diğer düğmeler nötrdür.
    /// ISA-101 yaklaşımı: renk yalnızca dikkat gereken yerde.
    /// </summary>
    public static Style PrimaryStyle(bool primary) =>
        (Style)Application.Current.Resources[primary ? "AccentButtonStyle" : "DefaultButtonStyle"];

    /// <summary>Fotoğraf kartındaki bilgi metninin rengi: işaret ya da arka plan bilgisi varsa vurgulu.</summary>
    public static Microsoft.UI.Xaml.Media.Brush InfoBrush(bool hasInfo) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[hasInfo ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush"];

    // ───────────────────────── Sekmeler ─────────────────────────

    private SelectorBarItem TabFor(InspectionStep step) => step switch
    {
        InspectionStep.Photo => PhotoTabItem,
        InspectionStep.Decision => DecisionTabItem,
        InspectionStep.History => HistoryTabItem,
        _ => MeasureTabItem,
    };

    private InspectionStep StepOf(SelectorBarItem item) =>
        ReferenceEquals(item, PhotoTabItem) ? InspectionStep.Photo
        : ReferenceEquals(item, DecisionTabItem) ? InspectionStep.Decision
        : ReferenceEquals(item, HistoryTabItem) ? InspectionStep.History
        : InspectionStep.Measure;

    private async void DetailTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_syncingTabs)
            return;

        // Tıklanan sekme önce alınır: aşağıdaki kayıt ekranı yeniler ve seçimi bir an eski sekmeye çekebilir.
        var clicked = sender.SelectedItem as SelectorBarItem ?? MeasureTabItem;

        // Ölçüm kutusundan doğrudan sekmeye tıklanınca kutunun LostFocus'u sonra gelebilir: bekleyen değerler önce yazılır.
        Vm.AutoSave();

        // Ölçüm girilmeden fotoğrafa geçilmek istenirse: sekme önce geri alınır, uyarı kabul edilirse fotoğrafa geçilir.
        if (ReferenceEquals(clicked, PhotoTabItem) && Vm.IsSkippingMeasurement)
        {
            ShowTab(TabFor(Vm.SelectedStep));
            await Vm.GoToPhotoStepAsync();
            return;
        }

        Vm.SelectedStep = StepOf(clicked);
        ShowTab(clicked);
    }

    /// <summary>
    /// Sekmeyi seçer ve içeriğini doğrudan gösterir. SelectorBar ilk kez görünür olurken seçim olayı
    /// gelmeyebilir; panel görünürlüğü bu yüzden olaya bırakılmaz.
    /// </summary>
    private void ShowTab(SelectorBarItem item)
    {
        // Detay alanı pencereyle birlikte yüklendiği için (gizliyken de ağaçta) seçim doğrudan uygulanabilir.
        // Kullanıcı tıkladığında seçim zaten doğrudur; yalnızca farklıysa yazılır.
        if (!ReferenceEquals(DetailTabs.SelectedItem, item))
        {
            _syncingTabs = true;
            try
            {
                DetailTabs.SelectedItem = item;
            }
            finally
            {
                _syncingTabs = false;
            }
        }
        ShowPanels(item);
    }

    /// <summary>Seçili kusur yokken detay alanı görünmez ama ağaçta kalır (sekme çubuğu yüklü kalsın diye).</summary>
    public static double OpacityFor(bool visible) => visible ? 1.0 : 0.0;

    private void ShowPanels(SelectorBarItem selected)
    {
        MeasureTab.Visibility = Vis(selected == MeasureTabItem);
        PhotoTab.Visibility = Vis(selected == PhotoTabItem);
        DecisionTab.Visibility = Vis(selected == DecisionTabItem);
        HistoryTab.Visibility = Vis(selected == HistoryTabItem);
    }

    /// <summary>Sıradaki adımın sekmesi kalın yazılır.</summary>
    private void ApplyTabWeights()
    {
        foreach (var (item, step) in new[]
                 {
                     (MeasureTabItem, InspectionStep.Measure),
                     (PhotoTabItem, InspectionStep.Photo),
                     (DecisionTabItem, InspectionStep.Decision),
                 })
            item.FontWeight = Vm.CurrentStep == step ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
    }

    /// <summary>Başka bir parça / seri açmak için muayene seçimine döner. Kayıtlar zaten diske yazılmıştır.</summary>
    private void ChangeSession_Click(object sender, RoutedEventArgs e)
    {
        Vm.ChangeSession();
        Status.IsOpen = false;
    }

    private static Visibility Vis(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // ───────────────────────── Fotoğraf kartları ─────────────────────────

    /// <summary>Fotoğrafı kusurdan kaldırır (onay ile). Akış ViewModel'dedir.</summary>
    private async void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string photo)
            await Vm.RemovePhotoAsync(photo);
    }

    private void EditPhoto_Click(object sender, RoutedEventArgs e) => Vm.OpenEditor((sender as Button)?.Tag as string);

    // ───────────────────────── Ölçüm ve fotoğraf ─────────────────────────

    private void SaveMeasurements_Click(object sender, RoutedEventArgs e) => Vm.SaveMeasurements();

    private void CameraButton_Click(object sender, RoutedEventArgs e) => Vm.OpenCamera();

    private async void AddPhoto_Click(object sender, RoutedEventArgs e) => await Vm.AddPhotoFromFileAsync();

    // ───────────────────────── Karar ─────────────────────────

    /// <summary>
    /// Karar seçilince onay penceresi açılır (akış ViewModel'de). Seçim hemen sıfırlanır: aynı karar yeniden seçilebilir.
    /// </summary>
    private async void DecisionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DecisionCombo.SelectedItem is not DecisionOption option)
            return;

        try
        {
            await Vm.GiveDecisionAsync(option);
        }
        finally
        {
            DecisionCombo.SelectedIndex = -1;
        }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e) => await Vm.UndoLastAsync();

    private void Complete_Click(object sender, RoutedEventArgs e) => Vm.Complete();

    private async void VoidButton_Click(object sender, RoutedEventArgs e) => await Vm.VoidAsync();

    private async void ReinspectButton_Click(object sender, RoutedEventArgs e) => await Vm.ReinspectAsync();

    // ───────────────────────── Rapor ─────────────────────────

    private async void Report_Click(object sender, RoutedEventArgs e) => await Vm.CreateReportAsync();

    // ───────────────────────── Dashboard ─────────────────────────

    private void RefreshIndex_Click(object sender, RoutedEventArgs e) => Dash.Refresh();

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => Dash.ClearFilters();

    private void VerifyAudit_Click(object sender, RoutedEventArgs e) => Dash.VerifyAudit();

    private void Dash_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Satırlar en son atanır: özet ve kartlar birlikte yeniden çizilir.
        if (e.PropertyName == nameof(DashboardViewModel.Rows))
            RenderDashboard();
    }

    private void RenderDashboard()
    {
        SummaryCards.Children.Clear();
        foreach (var card in Dash.SummaryCards)
            SummaryCards.Children.Add(DashboardCards.StatCard(card));

        ResultsPanel.Children.Clear();
        if (Dash.EmptyText is { } empty)
        {
            ResultsPanel.Children.Add(DashboardCards.EmptyMessage(empty));
            return;
        }

        foreach (var row in Dash.Rows)
            ResultsPanel.Children.Add(DashboardCards.ResultCard(row, Dash.Open));
    }

    /// <summary>Dashboard kartındaki kaydı Muayene ekranında açar.</summary>
    private void OpenFromDashboard(IndexRow r)
    {
        Vm.PartNumber = r.Part;
        Vm.OperationNumber = r.Operation;
        Vm.SerialNumber = r.Serial;
        Nav.SelectedItem = Nav.MenuItems[0];

        if (string.IsNullOrWhiteSpace(Vm.InspectorName))
        {
            InspectorBox.Focus(FocusState.Programmatic);
            Show("Kaydı açmak için Inspector adınızı girip 'Create Inspection'a basın.", InfoBarSeverity.Informational);
            return;
        }

        OpenSession_Click(this, new RoutedEventArgs());
    }

    // ───────────────────────── Yardımcılar ─────────────────────────

    private void Show(string message, InfoBarSeverity severity)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;

        _statusTimer?.Stop();
        if (severity is InfoBarSeverity.Success or InfoBarSeverity.Informational)
        {
            _statusTimer ??= CreateStatusTimer();
            _statusTimer.Start();
        }
    }

    private DispatcherTimer? _statusTimer;

    private DispatcherTimer CreateStatusTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (Status.Severity is InfoBarSeverity.Success or InfoBarSeverity.Informational)
                Status.IsOpen = false;
        };
        return timer;
    }
}
