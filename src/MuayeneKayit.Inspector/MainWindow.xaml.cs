using System.Diagnostics;
using System.Globalization;
using MuayeneKayit.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Muayene ve dashboard ekranı. İş kuralları Core'dadır; bu sınıf yalnızca arayüzü bağlar.
/// Karar butonları seçili kusurun durumuna göre açılır/kapanır. Hatalar InfoBar ile gösterilir.
/// </summary>
public sealed partial class MainWindow : Window
{
    private SessionStore? _store;
    private string? _folder;
    private Session? _session;

    /// <summary>
    /// İşlemi yapan muayeneci (Muayeneci kutusu). Kayıtlı bir muayeneyi başka biri açabilir;
    /// denetim kaydı ve kararlar oturumu oluşturana değil, o anki kullanıcıya yazılır.
    /// </summary>
    private string _user = "";
    private int? _selectedId;
    private Defect? _current;
    private CameraWindow? _camera;
    private PhotoEditorWindow? _editor;
    private readonly string _rootPath = AppSettings.LoadRootPath();
    private List<IndexRow> _indexRows = new();
    private bool _filtersReady;

    /// <summary>Kusur tipleri admin panelinde tanımlanır (_config); yoksa varsayılanlar.</summary>
    private readonly IReadOnlyList<DefectTypeTemplate> _types;
    private string[] TypeNames => _types.Select(t => t.Name).ToArray();

    /// <summary>Detayda gösterilen kusur; başka kusura geçilince adımlar "1. Ölçüm"e döner.</summary>
    private int? _shownId;

    /// <summary>Liste yenilenirken ListView'in ara seçim olayları yok sayılır (sekme ve seçim korunur).</summary>
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        Title = "MuayeneKayit";


        _types = SharedConfig.LoadDefectTypes(_rootPath);
        DefectTypeBox.ItemsSource = TypeNames;
        DefectTypeBox.SelectedIndex = 0;

        FilterTypeBox.ItemsSource = new[] { "Tümü" }.Concat(TypeNames).ToList();
        FilterTypeBox.SelectedIndex = 0;
        foreach (var (label, tag) in StateFilters)
            FilterStateBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        FilterStateBox.SelectedIndex = 0;
        _filtersReady = true;

        Nav.SelectedItem = Nav.MenuItems[0];
        UpdateDecisionButtons(null);

        // Ana pencere kapanınca açık kamera penceresi de kapanır (kamera serbest kalır).
        Closed += (_, _) =>
        {
            _camera?.Close();
            _editor?.Close();
        };
    }

    // ───────────────────────── Navigasyon ─────────────────────────

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        InspectPanel.Visibility = tag == "inspect" ? Visibility.Visible : Visibility.Collapsed;
        DashPanel.Visibility = tag == "dash" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "dash")
            RefreshIndex_Click(this, new RoutedEventArgs());
    }

    // ───────────────────────── Oturum ─────────────────────────

    private void OpenSession_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        _store = new SessionStore(_rootPath);
        var input = new Session
        {
            PartNumber = PartBox.Text.Trim(),
            OperationNumber = OpBox.Text.Trim(),
            SerialNumber = SerialBox.Text.Trim(),
            Inspector = InspectorBox.Text.Trim(),
            Project = PartBox.Text.Trim(),
        };

        if (string.IsNullOrWhiteSpace(input.Inspector))
            throw new ArgumentException("Muayeneci adı boş olamaz.");

        var existed = _store.LoadSession(_store.FolderFor(input.PartNumber, input.OperationNumber, input.SerialNumber)) is not null;
        (_folder, _session) = _store.OpenOrCreate(input);
        _user = input.Inspector;
        AuditLog.Append(_store, _user, "session.open", _folder);
        ReportButton.IsEnabled = true;
        StartPanel.Visibility = Visibility.Collapsed;
        WorkPanel.Visibility = Visibility.Visible;
        ClearSelection();
        RefreshDefects(null);
        Show(existed
            ? $"Kayıtlı muayene açıldı: {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}"
            : $"Yeni muayene oluşturuldu: {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}",
            InfoBarSeverity.Success);
    });

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

    /// <summary>Önceki kayıtlardaki parça, operasyon ve seri numaralarını önerir; yazılan metne göre süzer.</summary>
    private void ShowSuggestions(AutoSuggestBox box)
    {
        var text = box.Text.Trim();
        var items = SuggestionsFor(box)
            .Where(n => text.Length == 0 || n.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        box.ItemsSource = items;
        box.IsSuggestionListOpen = items.Count > 0;
    }

    private IEnumerable<string> SuggestionsFor(AutoSuggestBox box)
    {
        try
        {
            string folder;
            if (box == PartBox)
                folder = _rootPath;
            else if (box == OpBox)
                folder = Path.Combine(_rootPath, SafePath.ValidateSegment(PartBox.Text, "Parça no"));
            else
                folder = Path.Combine(_rootPath,
                    SafePath.ValidateSegment(PartBox.Text, "Parça no"),
                    SafePath.ValidateSegment(OpBox.Text, "Operasyon no"));

            if (!Directory.Exists(folder))
                return Array.Empty<string>();

            return Directory.EnumerateDirectories(folder)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('_'))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Üst alan boş ya da geçersizse öneri yok.
            return Array.Empty<string>();
        }
    }

    // ───────────────────────── Kusur listesi ─────────────────────────

    private void AddDefect_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RequireSession();
        var type = DefectTypeBox.SelectedItem as string ?? throw new InvalidOperationException("Kusur tipi seçin.");

        var spec = _types.FirstOrDefault(t => t.Name == type)?.SpecName;
        var defect = _store!.AddDefect(_folder!, type, d => d.SpecName = spec);
        AuditLog.Append(_store!, _user, "defect.add", $"#{defect.Id} {type}");
        AddDefectFlyout.Hide();
        RefreshDefects(defect.Id);
        Show($"Kusur #{defect.Id} ({type}) eklendi. Ölçümleri girin.", InfoBarSeverity.Success);
    });

    private void DefectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing)
            return;

        if (DefectList.SelectedItem is DefectItem row && _store is not null)
        {
            _selectedId = row.Id;
            LoadSelectedDefect();
        }
        else if (_store is not null && _folder is not null)
        {
            ClearSelection();
        }
    }

    private void RefreshDefects(int? selectId)
    {
        var defects = _store!.LoadDefects(_folder!);
        var rows = defects.Select(ToItem).ToList();

        var keep = selectId ?? _selectedId;
        var match = rows.FirstOrDefault(r => r.Id == keep);

        _refreshing = true;
        try
        {
            DefectList.ItemsSource = rows;
            DefectList.SelectedItem = match;
        }
        finally
        {
            _refreshing = false;
        }

        DefectListEmpty.Visibility = Vis(rows.Count == 0);
        DefectList.Visibility = Vis(rows.Count > 0);
        UpdateSessionHeader(defects);

        if (match is not null)
        {
            _selectedId = match.Id;
            LoadSelectedDefect();
        }
        else
        {
            ClearSelection();
        }
    }

    // ───────────────────────── Görünüm yardımcıları ─────────────────────────

    private DefectItem ToItem(Defect d)
    {
        var state = DefectStateMachine.GetState(d);
        var (bg, fg) = StateBrushes(state);
        return new DefectItem
        {
            Id = d.Id,
            Number = $"#{d.Id}",
            Type = d.DefectType,
            StateText = StateLabel(state),
            PhotoText = d.Photos.Count == 0 ? "fotoğraf yok" : $"{d.Photos.Count} fotoğraf",
            NextStep = NextStepFor(d, state),
            StateBackground = bg,
            StateForeground = fg,
        };
    }

    /// <summary>Durum etiketinin renkleri: sistem durum fırçaları (tema ve yüksek kontrast uyumlu).</summary>
    private static (Microsoft.UI.Xaml.Media.Brush Background, Microsoft.UI.Xaml.Media.Brush Foreground) StateBrushes(DefectState state)
    {
        var key = state switch
        {
            DefectState.Closed => "Success",
            DefectState.MrbPending => "Attention",
            DefectState.Voided or DefectState.Superseded => "Neutral",
            DefectState.AwaitingReinspect or DefectState.WaitingVendor or DefectState.Repairing => "Caution",
            _ => "Critical",
        };
        var resources = Application.Current.Resources;
        return ((Microsoft.UI.Xaml.Media.Brush)resources[$"SystemFillColor{key}BackgroundBrush"],
                (Microsoft.UI.Xaml.Media.Brush)resources[$"SystemFillColor{key}Brush"]);
    }

    /// <summary>Muayenecinin bu kusurda yapması gereken sıradaki iş.</summary>
    private static string NextStepFor(Defect d, DefectState state)
    {
        var hasMeasure = d.Depth is not null || d.Width is not null || d.Length is not null
            || d.Radius is not null || d.AngleDeg is not null || !string.IsNullOrWhiteSpace(d.Color);
        return state switch
        {
            DefectState.Open when !hasMeasure => "Sıradaki adım: ölçümleri girin.",
            DefectState.Open when d.Photos.Count == 0 => "Sıradaki adım: fotoğraf çekin.",
            DefectState.Open => "Mühendis kararı bekleniyor: rapordan sonra Disposition girin.",
            DefectState.AwaitingReinspect => "Rework'te: parça dönünce rework sonrası muayene sonucunu girin.",
            DefectState.Repairing => "Repair'de: sonuca göre kabul, hurda, reinspect ya da MRB girin.",
            DefectState.WaitingVendor => "Tedarikçide: parça dönünce reinspect girin.",
            DefectState.MrbPending => "MRB kararı bekleniyor: kabul ya da red gelince girin.",
            DefectState.Closed => "Tamamlandı.",
            DefectState.Superseded => d.SupersededById is int to ? $"Takip #{to} numaralı kusur üzerinden yapılıyor." : "Devredildi.",
            DefectState.Voided => "Void edildi, rapora girmez.",
            _ => "",
        };
    }

    private void UpdateSessionHeader(IReadOnlyList<Defect> defects)
    {
        if (_session is null)
            return;
        var open = SessionGate.BlockingDefects(defects).Count;
        var visible = defects.Count(d => DefectStateMachine.GetState(d) != DefectState.Voided);
        SessionTitle.Text = $"{_session.PartNumber}  ·  {_session.OperationNumber}  ·  {_session.SerialNumber}";
        SessionSub.Text = $"Muayeneci: {_user}   ·   {visible} kusur, {open} açık   ·   " +
                          (_session.Status == SessionStatus.Completed ? "Tamamlandı" : "Devam ediyor");
        DefectCountText.Text = visible == 0 ? "Kusurlar" : $"Kusurlar ({visible})";
    }

    private void DetailTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selected = sender.SelectedItem;
        MeasureTab.Visibility = Vis(selected == MeasureTabItem);
        PhotoTab.Visibility = Vis(selected == PhotoTabItem);
        DecisionTab.Visibility = Vis(selected == DecisionTabItem);
        HistoryTab.Visibility = Vis(selected == HistoryTabItem);
    }

    /// <summary>Başka bir parça / seri açmak için muayene seçimine döner. Kayıtlar zaten diske yazılmıştır.</summary>
    private void ChangeSession_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
        _session = null;
        _folder = null;
        DefectList.ItemsSource = null;
        ReportButton.IsEnabled = false;
        WorkPanel.Visibility = Visibility.Collapsed;
        StartPanel.Visibility = Visibility.Visible;
        Status.IsOpen = false;
    }

    private void ClearSelection()
    {
        _selectedId = null;
        _current = null;
        _shownId = null;
        DefectList.SelectedItem = null;
        DetailEmpty.Visibility = Visibility.Visible;
        DetailContent.Visibility = Visibility.Collapsed;
        DeltaText.Visibility = Visibility.Collapsed;
        PhotoPanel.Children.Clear();
        PhotoEmpty.Visibility = Visibility.Visible;
        HistoryList.ItemsSource = null;
        SaveMeasButton.IsEnabled = false;
        AddPhotoButton.IsEnabled = false;
        CameraButton.IsEnabled = false;
        UpdateDecisionButtons(null);
    }

    private void LoadSelectedDefect()
    {
        var defect = _store!.LoadDefects(_folder!).FirstOrDefault(d => d.Id == _selectedId);
        if (defect is null)
        {
            ClearSelection();
            return;
        }

        _current = defect;
        var state = DefectStateMachine.GetState(defect);

        DetailEmpty.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;
        if (_shownId != defect.Id)
        {
            _shownId = defect.Id;
            DetailTabs.SelectedItem = MeasureTabItem;
        }

        SelectedHeader.Text = $"#{defect.Id}  {defect.DefectType}";
        var (chipBg, chipFg) = StateBrushes(state);
        StateChip.Background = chipBg;
        StateChipText.Foreground = chipFg;
        StateChipText.Text = StateLabel(state);
        NextStepText.Text = NextStepFor(defect, state);
        SelectedSentence.Text = DefectSentence.Describe(defect);
        PhotoTabItem.Text = defect.Photos.Count == 0 ? "2. Fotoğraflar" : $"2. Fotoğraflar ({defect.Photos.Count})";

        // Değişim satırı yalnızca rework sonrası gerçekten yeniden ölçüldüyse ("aynı kusur devam") gösterilir.
        var effective = DefectStateMachine.Effective(defect);
        var lastDecision = effective.LastOrDefault(x => x.Source != DecisionSource.System);
        var reworkBefore = lastDecision?.Decision == Decision.AfterReworkSameDefect
            ? effective.TakeWhile(x => !ReferenceEquals(x, lastDecision)).LastOrDefault(x => x.Decision == Decision.Rework)?.Snapshot
            : null;
        DeltaText.Text = reworkBefore is null
            ? ""
            : "Rework sonrası yeniden ölçüldü. Rework öncesine göre: " + string.Join("   ·   ", MeasurementCompare.Lines(reworkBefore, defect));
        DeltaText.Visibility = reworkBefore is null ? Visibility.Collapsed : Visibility.Visible;

        // Rework'teyken ölçüm kutuları yerine ne yapılacağını anlatan kutu gösterilir.
        var awaiting = state == DefectState.AwaitingReinspect;
        AwaitBanner.Visibility = Vis(awaiting);
        MeasureArea.Visibility = Vis(!awaiting);
        if (awaiting)
        {
            var sent = effective.LastOrDefault(x => x.Decision is Decision.Rework or Decision.Reinspect);
            var where = string.IsNullOrWhiteSpace(sent?.TargetOperation) ? "" : $" · {sent!.TargetOperation} operasyonunda yeniden muayene edilecek";
            AwaitTitle.Text = (sent?.Decision == Decision.Reinspect ? "Yeniden kontrol bekliyor" : "Parça rework'te") + where;
            AwaitText.Text = $"{(sent?.Decision == Decision.Reinspect ? "Önceki" : "Rework öncesi")} ölçümler: " +
                             $"{SnapshotText(sent?.Snapshot ?? MeasurementEditor.Snapshot(defect))}. " +
                             "Ölçümler kilitli. Parça geldiğinde aşağıdaki düğmeyle muayene sonucunu girin.";
        }

        DepthBox.Text = Fmt(defect.Depth);
        WidthBox.Text = Fmt(defect.Width);
        LengthBox.Text = Fmt(defect.Length);
        RadiusBox.Text = Fmt(defect.Radius);
        AngleBox.Text = Fmt(defect.AngleDeg);
        ColorBox.Text = defect.Color ?? "";
        NotesBox.Text = defect.Notes ?? "";

        ApplyTemplateFields(defect);

        HistoryList.ItemsSource = HistoryLines(defect);
        var historyCount = DefectStateMachine.Effective(defect).Count + defect.MeasurementEdits.Count;
        HistoryTabItem.Text = historyCount == 0 ? "Geçmiş" : $"Geçmiş ({historyCount})";

        var editable = state is not (DefectState.Closed or DefectState.Voided or DefectState.Superseded
            or DefectState.AwaitingReinspect);
        SaveMeasButton.IsEnabled = editable;
        foreach (var box in new[] { DepthBox, WidthBox, LengthBox, RadiusBox, AngleBox, ColorBox, NotesBox })
            box.IsReadOnly = !editable;
        AddPhotoButton.IsEnabled = editable;
        CameraButton.IsEnabled = editable;
        RenderPhotos(defect, editable);
        UpdateDecisionButtons(defect);
    }

    /// <summary>
    /// Kusur tipinin şablonundaki ölçüm alanlarını gösterir. Şablon dışı bir alanda değer varsa
    /// (eski kayıt) o alan da görünür kalır; veri gizlenmez.
    /// </summary>
    private void ApplyTemplateFields(Defect defect)
    {
        var template = _types.FirstOrDefault(t => string.Equals(t.Name, defect.DefectType, StringComparison.OrdinalIgnoreCase));
        bool Visible(MeasureField field, bool hasValue) => template is null || template.Fields.Contains(field) || hasValue;

        DepthBox.Visibility = Vis(Visible(MeasureField.Depth, defect.Depth is not null));
        WidthBox.Visibility = Vis(Visible(MeasureField.Width, defect.Width is not null));
        LengthBox.Visibility = Vis(Visible(MeasureField.Length, defect.Length is not null));
        RadiusBox.Visibility = Vis(Visible(MeasureField.Radius, defect.Radius is not null));
        AngleBox.Visibility = Vis(Visible(MeasureField.Angle, defect.AngleDeg is not null));
        ColorBox.Visibility = Vis(Visible(MeasureField.Color, !string.IsNullOrWhiteSpace(defect.Color)));
    }

    private static Visibility Vis(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // ───────────────────────── Fotoğraf kartları ─────────────────────────

    /// <summary>Küçük resimler; işaret sayısı ve arka plan durumu ile. Tıklayınca düzenleyici açılır.</summary>
    private void RenderPhotos(Defect defect, bool editable)
    {
        PhotoPanel.Children.Clear();
        PhotoEmpty.Visibility = Vis(defect.Photos.Count == 0);
        var directory = Path.Combine(_folder!, "defects");

        foreach (var photo in defect.Photos)
        {
            var markup = defect.Markups.GetValueOrDefault(photo);
            var clean = markup?.CleanedFile;
            var shown = clean is not null && File.Exists(Path.Combine(directory, clean)) ? clean : photo;

            var image = new Microsoft.UI.Xaml.Controls.Image { Width = 180, Height = 120, Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill };
            _ = LoadThumbnailAsync(image, Path.Combine(directory, shown));

            var info = new List<string>();
            if (markup?.Shapes.Count > 0) info.Add($"{markup.Shapes.Count} işaret");
            if (shown != photo) info.Add("arka plan kaldırıldı");

            var edit = new Button
            {
                Content = editable ? "İşaretle" : "Görüntüle",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = photo,
            };
            edit.Click += EditPhoto_Click;

            var buttons = new Grid { ColumnSpacing = 6 };
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttons.Children.Add(edit);
            if (editable)
            {
                var remove = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 }, Tag = photo };
                ToolTipService.SetToolTip(remove, "Fotoğrafı kaldır");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, $"{photo} fotoğrafını kaldır");
                remove.Click += RemovePhoto_Click;
                Grid.SetColumn(remove, 1);
                buttons.Children.Add(remove);
            }

            var index = defect.Photos.IndexOf(photo) + 1;
            PhotoPanel.Children.Add(new Border
            {
                Width = 196,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = Theme("CardStrokeColorDefaultBrush"),
                Background = Theme("CardBackgroundFillColorSecondaryBrush"),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new Border { CornerRadius = new CornerRadius(4), Background = Brush("#111111"), Child = image },
                        new TextBlock { Text = $"{defect.Id}.{index}   ·   {photo}", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock
                        {
                            Text = info.Count == 0 ? "İşaret yok" : string.Join(" · ", info),
                            FontSize = 11,
                            Foreground = Theme(info.Count == 0 ? "TextFillColorSecondaryBrush" : "SystemFillColorCautionBrush"),
                        },
                        buttons,
                    },
                },
            });
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush Theme(string key) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];

    /// <summary>
    /// Fotoğrafı kusurdan kaldırır (onay ile). Dosya ortak alanda kalır, yalnızca kusurun listesinden ve
    /// rapordan çıkar; işlem denetim kaydına yazılır.
    /// </summary>
    private async void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireSelection();
            if ((sender as Button)?.Tag is not string photo || !PhotoMarkupValidator.IsPhotoFileName(photo))
                return;

            var id = _selectedId!.Value;
            var dialog = new ContentDialog
            {
                Title = "Fotoğrafı kaldır",
                Content = new TextBlock
                {
                    Width = 440,
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"#{id} kusurundan {photo} kaldırılacak; rapora girmez. Dosya ortak alanda saklanmaya devam eder.",
                },
                PrimaryButtonText = "Kaldır",
                CloseButtonText = "Vazgeç",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Nav.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            _store!.UpdateDefect(_folder!, id, d =>
            {
                d.Photos.Remove(photo);
                d.Markups.Remove(photo);
            });
            AuditLog.Append(_store!, _user, "defect.photo.remove", $"#{id} {photo}");
            RefreshDefects(id);
            Show($"{photo} kaldırıldı.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    private static async Task LoadThumbnailAsync(Microsoft.UI.Xaml.Controls.Image target, string path)
    {
        try
        {
            // Akıştan okunur: dosya kilitli kalmaz, ortak alandaki dosya başkası tarafından da açılabilir.
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 360 };
            using var stream = File.OpenRead(path);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            target.Source = bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // Küçük resim gösterilemezse kart boş kalır; kayıt etkilenmez.
        }
    }

    private void EditPhoto_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RequireSelection();
        if ((sender as Button)?.Tag is not string photo || !PhotoMarkupValidator.IsPhotoFileName(photo))
            throw new InvalidOperationException("Fotoğraf okunamadı.");

        var id = _selectedId!.Value;
        var folder = _folder!;
        var store = _store!;
        var by = _user;
        var defects = store.LoadDefects(folder);
        var defect = defects.First(d => d.Id == id);
        var state = DefectStateMachine.GetState(defect);
        var editable = state is not (DefectState.Closed or DefectState.Voided or DefectState.Superseded or DefectState.AwaitingReinspect);

        if (!editable)
        {
            // Kapalı kusurda yalnızca görüntüleme: işaretli hali rapordan görülür, ham fotoğraf burada açılır.
            Process.Start(new ProcessStartInfo(Path.Combine(folder, "defects", defect.Markups.GetValueOrDefault(photo)?.CleanedFile ?? photo)) { UseShellExecute = true });
            return;
        }

        var labels = defects
            .Where(d => DefectStateMachine.GetState(d) != DefectState.Voided)
            .Select(d => d.Id)
            .ToList();

        _editor?.Close();
        var editor = new PhotoEditorWindow(
            $"#{id} {defect.DefectType}   ·   {photo}   ·   {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}",
            Path.Combine(folder, "defects"),
            photo,
            defect.Markups.GetValueOrDefault(photo),
            labels,
            id,
            result =>
            {
                var markup = result.Markup;
                if (result.NewCleanJpeg is not null)
                    markup.CleanedFile = PhotoImporter.WriteCleaned(folder, photo, result.NewCleanJpeg);

                var errors = PhotoMarkupValidator.Validate(markup);
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(" ", errors));

                store.UpdateDefect(folder, id, d =>
                {
                    if (markup.Shapes.Count == 0 && markup.CleanedFile is null)
                        d.Markups.Remove(photo);
                    else
                        d.Markups[photo] = markup;
                });
                AuditLog.Append(store, by, "defect.markup",
                    $"#{id} {photo} {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırıldı")}");

                if (_selectedId == id && _folder == folder)
                    RefreshDefects(id);
                Show($"{photo} kaydedildi: {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırılmış kopya")}. Orijinal fotoğraf korunuyor.", InfoBarSeverity.Success);
            });
        editor.Closed += (_, _) =>
        {
            if (ReferenceEquals(_editor, editor))
                _editor = null;
        };
        _editor = editor;
        editor.Activate();
    });

    // ───────────────────────── Ölçüm ve fotoğraf ─────────────────────────

    private void SaveMeasurements_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RequireSelection();

        var values = new MeasurementSnapshot(
            ParseOptional(DepthBox.Text, "Derinlik"),
            ParseOptional(WidthBox.Text, "Genişlik"),
            ParseOptional(LengthBox.Text, "Uzunluk"),
            ParseOptional(RadiusBox.Text, "Yarıçap"),
            ParseOptional(AngleBox.Text, "Açı"),
            string.IsNullOrWhiteSpace(ColorBox.Text) ? null : ColorBox.Text.Trim());
        var notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();
        var by = _user;
        var changed = false;

        _store!.UpdateDefect(_folder!, _selectedId!.Value, d =>
        {
            var probe = new Defect
            {
                DefectType = d.DefectType,
                Depth = values.Depth, Width = values.Width, Length = values.Length,
                Radius = values.Radius, AngleDeg = values.AngleDeg, Color = values.Color,
            };

            // Doğrulama başarısızsa mutate hata fırlatır ve dosya yazılmaz.
            var errors = DefectValidator.Validate(probe);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(" ", errors));

            changed = MeasurementEditor.Apply(d, values, by);
            d.Notes = notes;
        });

        AuditLog.Append(_store!, by, "defect.measure", $"#{_selectedId}");
        RefreshDefects(_selectedId);

        // Ölçüm bitti, fotoğraf yoksa sıradaki adıma geç.
        if (_current is { Photos.Count: 0 })
            DetailTabs.SelectedItem = PhotoTabItem;
        Show(changed
                ? $"#{_selectedId} ölçümleri güncellendi, düzeltme geçmişe yazıldı."
                : $"#{_selectedId} için ölçüm değişmedi (not kaydedildi).",
            InfoBarSeverity.Success);
    });

    /// <summary>
    /// Kamera penceresini açar. Fotoğraflar, pencere açılırken seçili olan kusura kaydedilir;
    /// sonradan listede başka kusur seçilse bile hedef değişmez.
    /// </summary>
    private void CameraButton_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RequireSelection();

        var id = _selectedId!.Value;
        var folder = _folder!;
        var store = _store!;
        var by = _user;
        var target = $"#{id} {_current!.DefectType}   ·   {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}";

        // Aynı anda tek kamera penceresi: kamera tek uygulamaya açılabilir.
        _camera?.Close();

        var camera = new CameraWindow(target, tempPath =>
        {
            var name = PhotoImporter.Import(folder, id, tempPath);
            store.UpdateDefect(folder, id, d => d.Photos.Add(name));
            AuditLog.Append(store, by, "defect.photo", $"#{id} {name} (kamera)");
            if (_selectedId == id && _folder == folder)
                RefreshDefects(id);
            return name;
        });
        camera.Closed += (_, _) =>
        {
            if (ReferenceEquals(_camera, camera))
                _camera = null;
        };
        _camera = camera;
        camera.Activate();
    });

    private async void AddPhoto_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireSelection();

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            var id = _selectedId!.Value;
            var folder = _folder!;
            var name = PhotoImporter.Import(folder, id, file.Path);

            _store!.UpdateDefect(folder, id, d => d.Photos.Add(name));
            AuditLog.Append(_store!, _user, "defect.photo", $"#{id} {name}");
            RefreshDefects(id);
            Show($"Fotoğraf eklendi: {name}", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    // ───────────────────────── Karar ─────────────────────────

    /// <summary>
    /// Karar seçilince onay penceresi açılır: kararın sonucu açıklanır, yalnızca o kararın alanları sorulur.
    /// "Vazgeç" varsayılandır; Enter'a yanlışlıkla basmak kaydetmez.
    /// </summary>
    private async void DecisionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (DecisionCombo.SelectedItem as ComboBoxItem)?.Tag;
        if (tag is null || _current is null)
            return;

        try
        {
            if (tag is Decision decision)
                await ShowDecisionDialog(_current, decision);
            else if (tag as string == "reinspect")
                await ShowReinspectDialog();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            DecisionCombo.SelectedIndex = -1;
        }
    }

    private async Task ShowDecisionDialog(Defect defect, Decision decision)
    {
        var label = DecisionOrder.First(x => x.Decision == decision).Label;

        // Sabit genişlik: pencere içeriğe göre açılır, metin kırpılmadan alt satıra geçer.
        var panel = new StackPanel { Spacing = 12, Width = 560 };

        panel.Children.Add(new TextBlock
        {
            Text = $"#{defect.Id} {defect.DefectType}  ·  şu an: {StateLabel(DefectStateMachine.GetState(defect))}",
            Foreground = Brush("#666666"),
        });
        panel.Children.Add(new Border
        {
            Background = Brush("#F3F5F9"),
            BorderBrush = Brush("#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new TextBlock { Text = DecisionEffect(decision), TextWrapping = TextWrapping.Wrap },
        });

        TextBox? target = null;
        if (decision is Decision.Rework or Decision.Reinspect)
        {
            target = new TextBox
            {
                Header = decision == Decision.Rework
                    ? "Rework sonrası muayene hangi operasyonda yapılacak?"
                    : "Yeniden kontrol hangi operasyonda yapılacak? (örn. CTP)",
                // Varsayılan: bu muayenenin operasyonu. Muayeneci değiştirebilir.
                Text = _session?.OperationNumber ?? "",
            };
            target.SelectAll();
            panel.Children.Add(target);
        }

        TextBox? reference = null;
        if (decision is Decision.Accept or Decision.SubmitMrb or Decision.MrbAccepted or Decision.MrbRejected
            or Decision.Repair or Decision.Scrap)
        {
            reference = new TextBox
            {
                Header = "Mühendis kağıdındaki referans / MRB no (isteğe bağlı)",
                PlaceholderText = "örn. SPA-2024-118",
            };
            panel.Children.Add(reference);
        }

        var noteRequired = decision == Decision.Reopen;
        var note = new TextBox
        {
            Header = noteRequired ? "Neden yeniden açılıyor? (zorunlu)" : "Not (isteğe bağlı)",
            AcceptsReturn = true,
            Height = 70,
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(note);

        panel.Children.Add(new TextBlock
        {
            Text = "Yanlış kaydederseniz 'Son kararı geri al' ile geri alabilirsiniz. Geri alma da geçmişe yazılır.",
            Foreground = Brush("#666666"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            Title = label,
            Content = panel,
            PrimaryButtonText = "Kararı kaydet",
            CloseButtonText = "Vazgeç",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = !noteRequired,
            XamlRoot = Nav.XamlRoot,
        };
        if (noteRequired)
            note.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(note.Text);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var id = defect.Id;
        var by = _user;
        var source = SourceFor(decision);
        var noteText = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim();
        var refText = string.IsNullOrWhiteSpace(reference?.Text) ? null : reference!.Text.Trim();
        var targetText = target is null ? null : (string.IsNullOrWhiteSpace(target.Text) ? _session!.OperationNumber : target.Text.Trim());

        _store!.UpdateDefect(_folder!, id, d =>
            DefectStateMachine.Apply(d, decision, source, by, note: noteText, reference: refText, targetOperation: targetText));

        AuditLog.Append(_store!, by, "disposition", $"#{id} {decision}");
        RefreshDefects(id);
        Show($"#{id} için '{label}' kaydedildi. Yeni durum: {StateLabel(DefectStateMachine.GetState(_current ?? defect))}.", InfoBarSeverity.Success);

        // Rework aynı istasyonda hemen yapıldıysa sonucu beklemeden girilebilir; değilse parça rework'e gider.
        if (decision is Decision.Rework or Decision.Reinspect)
        {
            var ask = new ContentDialog
            {
                Title = decision == Decision.Rework ? "Rework şimdi yapıldı mı?" : "Kontrol şimdi yapılıyor mu?",
                Content = new TextBlock
                {
                    Width = 480,
                    TextWrapping = TextWrapping.Wrap,
                    Text = decision == Decision.Rework
                        ? $"Parça şimdi tekrar muayene edilecekse sonucu hemen girin. Parça rework'e gittiyse 'Sonra' deyin; kusur 'rework'te' olarak bekler, döndüğünde #{id} seçilip sonuç girilir."
                        : $"Kontrol şimdi yapılıyorsa sonucu hemen girin. Değilse 'Sonra' deyin; #{id} kontrol bekliyor olarak kalır.",
                },
                PrimaryButtonText = "Sonucu şimdi gir",
                CloseButtonText = "Sonra",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Nav.XamlRoot,
            };
            if (await ask.ShowAsync() == ContentDialogResult.Primary)
                await ShowReinspectDialog();
        }
    }

    /// <summary>Kararın ne yapacağı. Metinler admin panelinden düzenlenir (_config\decision_texts.json).</summary>
    private string DecisionEffect(Decision decision) =>
        SharedConfig.LoadDecisionTexts(_rootPath).GetValueOrDefault(decision, "");

    /// <summary>
    /// Son kararı geri alır. Karar silinmez; geçmişe "geri alındı" satırı eklenir. Rework sonrası yeni kusura
    /// devir geri alınırsa bağ çözülür ve açılan yeni kusur void edilir.
    /// </summary>
    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireSelection();
            var defect = _store!.LoadDefects(_folder!).First(d => d.Id == _selectedId);
            var last = DefectStateMachine.LastUndoable(defect)
                ?? throw new InvalidOperationException("Geri alınacak karar yok.");

            var panel = new StackPanel { Spacing = 12, Width = 520 };
            panel.Children.Add(new TextBlock
            {
                Text = $"#{defect.Id} {defect.DefectType} için son karar geri alınacak:\n" +
                       $"{DefectSentence.DecisionLabel(last.Decision)}   ({last.DecidedAt:dd.MM.yyyy HH:mm}, {last.DecidedBy})",
                TextWrapping = TextWrapping.Wrap,
            });
            if (last.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is int childId)
            {
                panel.Children.Add(new InfoBar
                {
                    IsOpen = true,
                    IsClosable = false,
                    Severity = InfoBarSeverity.Warning,
                    Message = $"Bu kararla açılan yeni kusur #{childId} void edilecek (silinmez, rapora girmez). " +
                              $"#{defect.Id} tekrar 'rework sonrası muayene bekliyor' olur.",
                });
            }
            var reason = new TextBox
            {
                Header = "Neden geri alınıyor? (zorunlu)",
                PlaceholderText = "örn. yanlış karar seçildi",
                AcceptsReturn = true,
                Height = 70,
                TextWrapping = TextWrapping.Wrap,
            };
            panel.Children.Add(reason);

            var dialog = new ContentDialog
            {
                Title = "Son kararı geri al",
                Content = panel,
                PrimaryButtonText = "Geri al",
                CloseButtonText = "Vazgeç",
                DefaultButton = ContentDialogButton.Close,
                IsPrimaryButtonEnabled = false,
                XamlRoot = Nav.XamlRoot,
            };
            reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var (undone, voided) = _store!.UndoLastDecision(_folder!, defect.Id, _user, reason.Text);
            AuditLog.Append(_store!, _user, "disposition.undo",
                $"#{defect.Id} {undone.Decision} (seq {undone.Seq}){(voided is int v ? $", child #{v} void" : "")}");
            RefreshDefects(defect.Id);
            Show($"#{defect.Id}: '{DefectSentence.DecisionLabel(undone.Decision)}' geri alındı." +
                 (voided is int c ? $" Yeni kusur #{c} void edildi." : "") +
                 $" Durum: {StateLabel(DefectStateMachine.GetState(_current ?? defect))}.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    private static DecisionSource SourceFor(Decision decision) => decision switch
    {
        Decision.Void => DecisionSource.Operator,
        Decision.MrbAccepted or Decision.MrbRejected => DecisionSource.Mrb,
        _ => DecisionSource.Engineer,
    };

    /// <summary>Her karar butonu, seçili kusurun durumuna göre açılır ya da kapanır.</summary>
    private void UpdateDecisionButtons(Defect? defect)
    {
        var awaiting = defect is not null && DefectStateMachine.GetState(defect) == DefectState.AwaitingReinspect;
        ReinspectButton.IsEnabled = awaiting;
        ReinspectButton.Visibility = awaiting ? Visibility.Visible : Visibility.Collapsed;

        var voidAllowed = defect is not null && DefectStateMachine.Check(defect, Decision.Void, DecisionSource.Operator).Ok;
        VoidButton.Visibility = voidAllowed ? Visibility.Visible : Visibility.Collapsed;

        // Disposition listesi: yalnızca bu durumda geçerli kararlar, iş akışı sırasıyla.
        DecisionCombo.Items.Clear();
        if (awaiting)
            DecisionCombo.Items.Add(new ComboBoxItem { Content = "Rework sonrası muayene sonucu…", Tag = "reinspect" });
        if (defect is not null)
        {
            foreach (var (decision, label) in DecisionOrder)
            {
                if (DefectStateMachine.Check(defect, decision, SourceFor(decision)).Ok)
                    DecisionCombo.Items.Add(new ComboBoxItem { Content = label, Tag = decision });
            }
        }

        var any = DecisionCombo.Items.Count > 0;
        DecisionCombo.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        DecisionCombo.SelectedIndex = -1;

        var undoable = defect is null ? null : DefectStateMachine.LastUndoable(defect);
        UndoPanel.Visibility = undoable is null ? Visibility.Collapsed : Visibility.Visible;
        UndoText.Text = undoable is null
            ? ""
            : $"Son karar: {DefectSentence.DecisionLabel(undoable.Decision)} ({undoable.DecidedAt:dd.MM HH:mm}, {undoable.DecidedBy})";

        DecisionHint.Text = defect is null
            ? "Karar vermek için listeden bir kusur seçin."
            : DecisionHintFor(defect);
    }

    /// <summary>Disposition listesinde gösterilecek kararlar ve sıraları.</summary>
    private static readonly (Decision Decision, string Label)[] DecisionOrder =
    {
        (Decision.Accept, "Kabul (SPA)"),
        (Decision.Rework, "Rework → Reinspect"),
        (Decision.SubmitMrb, "Submit MRB"),
        (Decision.MrbAccepted, "MRB kabul (accept as is)"),
        (Decision.MrbRejected, "MRB red (otomatik hurda)"),
        (Decision.Repair, "Repair"),
        (Decision.Reinspect, "Reinspect (CTP)"),
        (Decision.ReturnToVendor, "Return to vendor"),
        (Decision.Scrap, "Scrap"),
        (Decision.Reopen, "Yeniden aç"),
    };

    private static string DecisionHintFor(Defect defect) => DefectStateMachine.GetState(defect) switch
    {
        DefectState.Open => "Açık kusur: rework, reinspect, kabul, hurda, MRB veya tedarikçi kararı verilebilir.",
        DefectState.Reworking => "Rework yapılıyor: sonra reinspect ile yeniden muayene edin.",
        DefectState.AwaitingReinspect => "Rework yapıldı. Yeniden muayene sonucunu 'Rework sonrası muayene sonucunu gir' ile girin.",
        DefectState.WaitingVendor => "Tedarikçide: geri dönünce reinspect verin.",
        DefectState.MrbPending => "MRB bekleniyor: kabul ya da red gelince girin.",
        DefectState.Closed when !DefectStateMachine.IsReopenable(defect)
            => "Kesin kapandı (hurda ya da MRB reddi). Yeniden açılamaz.",
        DefectState.Closed => "Kapalı kusur. Yeniden açmak için not yazıp 'Yeniden aç' kullanın, geçmiş korunur.",
        DefectState.Superseded => "Bu kusur yeni bir kusura devredildi. İşlem yeni kusur üzerinden yapılır.",
        DefectState.Voided => "Void edilmiş kayıt. Rapora girmez, değiştirilemez.",
        _ => "",
    };

    private static IReadOnlyList<string> HistoryLines(Defect defect)
    {
        var items = new List<(DateTime At, string Text)>();

        // Geri alınan kararlar gösterilmez; ham dosyada ve denetim kaydında kalır.
        foreach (var x in DefectStateMachine.Effective(defect))
        {
            var parts = new List<string>
            {
                $"{x.DecidedAt:dd.MM HH:mm}  {x.DecidedBy}  ·  {DefectSentence.DecisionLabel(x.Decision)}",
            };
            if (!string.IsNullOrWhiteSpace(x.Note)) parts.Add($"not: {x.Note}");
            if (!string.IsNullOrWhiteSpace(x.Reference)) parts.Add($"ref: {x.Reference}");
            if (x.Snapshot is MeasurementSnapshot snap) parts.Add($"ölçüm: {SnapshotText(snap)}");
            items.Add((x.DecidedAt, string.Join("   ", parts)));
        }

        foreach (var m in defect.MeasurementEdits)
            items.Add((m.At, $"{m.At:dd.MM HH:mm}  {m.By}  ·  Ölçüm düzeltmesi: {SnapshotText(m.Before)}  →  {SnapshotText(m.After)}"));

        return items.Count == 0
            ? new[] { "Henüz karar ya da düzeltme yok." }
            : items.OrderBy(i => i.At).Select((i, n) => $"{n + 1}.  {i.Text}").ToList();
    }

    private static string SnapshotText(MeasurementSnapshot s)
    {
        var parts = new List<string>();
        if (s.Depth is double d) parts.Add($"D {Fmt(d)}");
        if (s.Width is double w) parts.Add($"W {Fmt(w)}");
        if (s.Length is double l) parts.Add($"L {Fmt(l)}");
        if (s.Radius is double r) parts.Add($"R {Fmt(r)}");
        if (s.AngleDeg is double a) parts.Add($"A {Fmt(a)}°");
        if (!string.IsNullOrWhiteSpace(s.Color)) parts.Add(s.Color!);
        return parts.Count == 0 ? "—" : string.Join(" ", parts);
    }

    private void Complete_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RequireSession();
        var defects = _store!.LoadDefects(_folder!);
        var blocking = SessionGate.BlockingDefects(defects);

        if (blocking.Count > 0)
        {
            var list = string.Join(", ", blocking.Select(d => $"#{d.Id} ({StateLabel(DefectStateMachine.GetState(d))})"));
            Show($"Muayene kapatılamaz, açık kusurlar var: {list}", InfoBarSeverity.Warning);
            return;
        }

        _session!.Status = SessionStatus.Completed;
        _store!.SaveSession(_folder!, _session);
        AuditLog.Append(_store!, _user, "session.complete", _folder!);
        UpdateSessionHeader(defects);
        Show("Muayene tamamlandı. Rapor oluşturabilirsiniz.", InfoBarSeverity.Success);
    });

    // ───────────────────────── Void ─────────────────────────

    /// <summary>
    /// Yanlış girilen kusuru geçersiz kılar. Gerekçe yazılmadan onay butonu açılmaz;
    /// kayıt silinmez, ortak alanda "void" olarak kalır ve rapora girmez.
    /// </summary>
    private async void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireSelection();
            var defect = _current!;

            var reason = new TextBox
            {
                Header = "Void gerekçesi (zorunlu)",
                PlaceholderText = "Örn. yanlış hata tipi seçildi, kusur başka parçaya ait",
                AcceptsReturn = true,
                Height = 80,
                TextWrapping = TextWrapping.Wrap,
            };

            var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
            panel.Children.Add(new TextBlock
            {
                Text = $"#{defect.Id} {defect.DefectType} kaydı geçersiz sayılacak. Kayıt silinmez ama rapora girmez ve bu işlem geri alınamaz.",
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(reason);

            var dialog = new ContentDialog
            {
                Title = "Kusuru void et",
                Content = panel,
                PrimaryButtonText = "Void et",
                CloseButtonText = "Vazgeç",
                DefaultButton = ContentDialogButton.Close,
                IsPrimaryButtonEnabled = false,
                XamlRoot = Nav.XamlRoot,
            };
            reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var id = defect.Id;
            var by = _user;
            var note = reason.Text.Trim();

            _store!.UpdateDefect(_folder!, id, d =>
                DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, by, note: note));
            AuditLog.Append(_store!, by, "defect.void", $"#{id} {note}");
            RefreshDefects(id);
            Show($"#{id} void edildi. Kayıt ortak alanda kaldı, rapora girmeyecek.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    // ───────────────────────── Rework sonrası muayene ─────────────────────────

    private async void ReinspectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowReinspectDialog();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>
    /// Rework (ya da reinspect) sonrası muayene, adım adım: önce sonuç seçilir, sonra yalnızca o sonucun
    /// alanları açılır. Giderildi: başka bir şey sorulmaz. Aynı kusur: yeni ölçümler boş gelir (eski değer
    /// ipucu olarak yazar), artış / azalış canlı gösterilir. Yeni hata: child tipi seçilir (varsayılan yok).
    /// </summary>
    private async Task ShowReinspectDialog()
    {
        RequireSelection();
        var defect = _store!.LoadDefects(_folder!).First(d => d.Id == _selectedId);
        if (DefectStateMachine.GetState(defect) != DefectState.AwaitingReinspect)
            throw new InvalidOperationException("Bu kusur rework sonrası muayene beklemiyor.");

        var effective = DefectStateMachine.Effective(defect);
        var sent = effective.LastOrDefault(x => x.Decision is Decision.Rework or Decision.Reinspect);
        var isRework = sent?.Decision == Decision.Rework;
        var before = sent?.Snapshot ?? MeasurementEditor.Snapshot(defect);
        var template = _types.FirstOrDefault(t => string.Equals(t.Name, defect.DefectType, StringComparison.OrdinalIgnoreCase));

        var panel = new StackPanel { Spacing = 12, Width = 560 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{(isRework ? "Rework öncesi" : "Önceki")} ölçümler: {SnapshotText(before)}" +
                   (string.IsNullOrWhiteSpace(sent?.TargetOperation) ? "" : $"   ·   muayene yeri: {sent!.TargetOperation}"),
            Foreground = Brush("#666666"),
            TextWrapping = TextWrapping.Wrap,
        });

        // 1. adım: sonuç
        var outcome = new RadioButtons { Header = "1. Muayene sonucu ne?" };
        outcome.Items.Add(new RadioButton { Content = "Kusur giderildi (uygun) → kusur kapanır", Tag = "conforms" });
        outcome.Items.Add(new RadioButton { Content = "Aynı kusur hâlâ var → yeni ölçümleri gir, sonra yeni karar ver", Tag = "same" });
        outcome.Items.Add(new RadioButton { Content = "Eski kusur gitti ama yeni bir hata oluştu → yeni kusur aç", Tag = "new" });
        panel.Children.Add(outcome);

        // 2. adım (aynı kusur): yeni ölçümler, boş gelir
        var fields = new (MeasureField Field, string Header, double? Old)[]
        {
            (MeasureField.Depth, "Derinlik (mm)", before.Depth),
            (MeasureField.Width, "Genişlik (mm)", before.Width),
            (MeasureField.Length, "Uzunluk (mm)", before.Length),
            (MeasureField.Radius, "Yarıçap (mm)", before.Radius),
            (MeasureField.Angle, "Açı (°)", before.AngleDeg),
        };
        var boxes = new Dictionary<MeasureField, TextBox>();
        var measureGrid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (field, header, old) in fields)
        {
            if (template is not null && !template.Fields.Contains(field) && old is null)
                continue;
            var box = new TextBox { Header = header, Width = 120, PlaceholderText = old is double o ? $"önce: {Fmt(o)}" : "" };
            boxes[field] = box;
            measureGrid.Children.Add(box);
        }
        TextBox? colorBox = null;
        if (template is null || template.Fields.Contains(MeasureField.Color) || before.Color is not null)
        {
            colorBox = new TextBox { Header = "Renk", Width = 140, PlaceholderText = before.Color is null ? "" : $"önce: {before.Color}" };
            measureGrid.Children.Add(colorBox);
        }
        var delta = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("#B45309") };
        var samePanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed,
            Children =
            {
                new TextBlock { Text = "2. Rework sonrası ölçümleri girin (boş bırakılan alan önceki değerinde kalır)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                new ScrollViewer { Content = measureGrid, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollMode = ScrollMode.Disabled },
                delta,
            },
        };
        panel.Children.Add(samePanel);

        // 2. adım (yeni hata): child tipi, varsayılan seçili değil
        var childType = new ComboBox { Header = "2. Yeni hatanın tipi", ItemsSource = TypeNames, PlaceholderText = "Tip seçin", Width = 300 };
        var newPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed,
            Children =
            {
                childType,
                new TextBlock
                {
                    Text = $"#{defect.Id} kapanmış sayılmaz, yeni kusura devredilir ('Defect {defect.Id} continues as Defect N'). Yeni kusurun ölçümlerini ve fotoğrafını kaydettikten sonra girersiniz.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush("#666666"),
                    FontSize = 12,
                },
            },
        };
        panel.Children.Add(newPanel);

        var note = new TextBox { Header = "Not (isteğe bağlı)", AcceptsReturn = true, Height = 60, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(note);

        var dialog = new ContentDialog
        {
            Title = $"#{defect.Id} {defect.DefectType}: {(isRework ? "rework sonrası muayene" : "yeniden kontrol sonucu")}",
            Content = panel,
            PrimaryButtonText = "Kaydet",
            CloseButtonText = "Vazgeç",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = Nav.XamlRoot,
        };

        string? Selected() => (outcome.SelectedItem as RadioButton)?.Tag as string;

        MeasurementSnapshot ReadValues()
        {
            static double? Try(TextBox? b) =>
                b is null || string.IsNullOrWhiteSpace(b.Text) ? null
                : double.TryParse(b.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ||
                  double.TryParse(b.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : null;
            return new MeasurementSnapshot(
                Try(boxes.GetValueOrDefault(MeasureField.Depth)), Try(boxes.GetValueOrDefault(MeasureField.Width)),
                Try(boxes.GetValueOrDefault(MeasureField.Length)), Try(boxes.GetValueOrDefault(MeasureField.Radius)),
                Try(boxes.GetValueOrDefault(MeasureField.Angle)),
                string.IsNullOrWhiteSpace(colorBox?.Text) ? null : colorBox!.Text.Trim());
        }

        // Boş bırakılan alan önceki değerinde kalır.
        MeasurementSnapshot Merged()
        {
            var v = ReadValues();
            return new MeasurementSnapshot(v.Depth ?? before.Depth, v.Width ?? before.Width, v.Length ?? before.Length,
                v.Radius ?? before.Radius, v.AngleDeg ?? before.AngleDeg, v.Color ?? before.Color);
        }

        bool AnyValue()
        {
            var v = ReadValues();
            return v.Depth is not null || v.Width is not null || v.Length is not null || v.Radius is not null
                || v.AngleDeg is not null || v.Color is not null;
        }

        void Refresh()
        {
            var tag = Selected();
            samePanel.Visibility = Vis(tag == "same");
            newPanel.Visibility = Vis(tag == "new");

            if (tag == "same")
            {
                var v = Merged();
                var now = new Defect { Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color };
                delta.Text = AnyValue() ? string.Join("\n", MeasurementCompare.Lines(before, now)) : "Değişimi görmek için ölçüm girin.";
            }

            dialog.IsPrimaryButtonEnabled = tag switch
            {
                "conforms" => true,
                "same" => AnyValue(),
                "new" => childType.SelectedItem is string,
                _ => false,
            };
        }

        outcome.SelectionChanged += (_, _) => Refresh();
        childType.SelectionChanged += (_, _) => Refresh();
        foreach (var box in boxes.Values.Append(colorBox).OfType<TextBox>())
            box.TextChanged += (_, _) => Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var result = Selected() ?? throw new InvalidOperationException("Muayene sonucunu seçin.");
        var id = defect.Id;
        var by = _user;
        var noteText = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim();

        switch (result)
        {
            case "conforms":
                _store!.UpdateDefect(_folder!, id, d =>
                    DefectStateMachine.Apply(d, Decision.AfterReworkConforms, DecisionSource.Engineer, by, noteText));
                AuditLog.Append(_store!, by, "disposition", $"#{id} AfterReworkConforms");
                RefreshDefects(id);
                Show($"#{id} uygun bulundu ve kapandı.", InfoBarSeverity.Success);
                break;

            case "same":
                var v = Merged();
                var probe = new Defect { DefectType = defect.DefectType, Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color };
                var errors = DefectValidator.Validate(probe);
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(" ", errors));

                // Yeni ölçümler ve karar aynı kilit altında: karar anı görüntüsü yeni değerleri taşır.
                _store!.UpdateDefect(_folder!, id, d =>
                {
                    MeasurementEditor.Apply(d, v, by);
                    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, by, noteText);
                });
                AuditLog.Append(_store!, by, "disposition", $"#{id} AfterReworkSameDefect");
                RefreshDefects(id);
                Show($"#{id}: aynı kusur devam ediyor, yeni ölçümler kaydedildi. Şimdi Disposition'dan yeni karar verin.", InfoBarSeverity.Warning);
                break;

            case "new":
                var newType = childType.SelectedItem as string ?? throw new InvalidOperationException("Yeni hata tipini seçin.");
                var child = _store!.ContinueAsChild(_folder!, id, newType, DecisionSource.Engineer, by, noteText, null);
                AuditLog.Append(_store!, by, "disposition.continue", $"#{id} -> #{child.Id} {newType}");
                RefreshDefects(child.Id);
                Show($"#{id} yeni kusur #{child.Id} ({newType}) olarak devam ediyor. #{child.Id} seçildi: ölçümlerini ve fotoğrafını girin.", InfoBarSeverity.Warning);
                break;
        }
    }

    // ───────────────────────── Rapor ─────────────────────────

    /// <summary>
    /// Raporu HTML olarak yazar, ardından bilgisayardaki Edge ile yerelde PDF'e çevirir (ağ kullanılmaz).
    /// PDF üretilemezse HTML açılır; tarayıcıdan yazdırılabilir.
    /// </summary>
    private async void Report_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireSession();
            ReportButton.IsEnabled = false;

            var reportsDir = Path.Combine(_folder!, "reports");
            Directory.CreateDirectory(reportsDir);

            var version = ReportFiles.LatestVersion(_folder!) + 1;
            var htmlPath = Path.Combine(reportsDir, $"Rapor_v{version}.html");
            var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");

            var defects = _store!.LoadDefects(_folder!);
            AtomicFile.WriteAllText(htmlPath, ReportBuilder.Build(_session!, defects, version));

            Show($"Rapor v{version} hazırlanıyor (PDF)...", InfoBarSeverity.Informational);
            var pdfError = await ReportPdf.TryConvertAsync(htmlPath, pdfPath);

            AuditLog.Append(_store!, _user, "report.create", $"Rapor_v{version}{(pdfError is null ? " (pdf)" : "")}");
            Process.Start(new ProcessStartInfo(pdfError is null ? pdfPath : htmlPath) { UseShellExecute = true });
            Show(pdfError is null
                    ? $"Rapor v{version} PDF olarak oluşturuldu ve açıldı. Yazdırıp mühendise verin."
                    : $"Rapor v{version} HTML olarak açıldı (PDF üretilemedi: {pdfError}). Tarayıcıdan 'PDF olarak kaydet' ile yazdırabilirsiniz.",
                pdfError is null ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            ReportButton.IsEnabled = _folder is not null;
        }
    }

    // ───────────────────────── Dashboard ─────────────────────────

    private static readonly (string Label, IndexStateFilter Tag)[] StateFilters =
    {
        ("Tümü", IndexStateFilter.All),
        ("Açık kusuru olanlar", IndexStateFilter.HasOpen),
        ("MRB bekleyen", IndexStateFilter.MrbPending),
        ("Rework sonrası muayene bekleyen", IndexStateFilter.AwaitingReinspect),
        ("Tedarikçide", IndexStateFilter.WaitingVendor),
        ("Tamamlanan muayeneler", IndexStateFilter.Completed),
    };

    private void RefreshIndex_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var store = new SessionStore(_store?.Root ?? _rootPath);
        _indexRows = IndexBuilder.Rebuild(store);
        ApplyFilters();
    });

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        _filtersReady = false;
        FilterPartBox.Text = "";
        FilterOpBox.Text = "";
        SerialFilterBox.Text = "";
        FilterTypeBox.SelectedIndex = 0;
        FilterStateBox.SelectedIndex = 0;
        _filtersReady = true;
        ApplyFilters();
    }

    private void FilterText_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void FilterSelection_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (!_filtersReady)
            return;

        var type = FilterTypeBox.SelectedItem as string;
        var criteria = new IndexCriteria(
            Part: FilterPartBox.Text,
            Operation: FilterOpBox.Text,
            Serial: SerialFilterBox.Text,
            DefectType: type is null || type == "Tümü" ? null : type,
            State: (FilterStateBox.SelectedItem as ComboBoxItem)?.Tag is IndexStateFilter st ? st : IndexStateFilter.All);

        var rows = IndexFilter.Apply(_indexRows, criteria);
        RenderSummary(rows);
        RenderResults(rows);
    }

    private void RenderSummary(IReadOnlyList<IndexRow> rows)
    {
        SummaryCards.Children.Clear();
        var defects = rows.SelectMany(r => r.Defects).ToList();

        SummaryCards.Children.Add(StatCard("Seri / operasyon", rows.Count.ToString(), "#1A3A5C"));
        SummaryCards.Children.Add(StatCard("Açık kusur", rows.Sum(r => r.OpenDefects).ToString(), "#B45309"));
        SummaryCards.Children.Add(StatCard("MRB bekleyen", defects.Count(d => d.State == nameof(DefectState.MrbPending)).ToString(), "#6D28D9"));
        SummaryCards.Children.Add(StatCard("Rework sonrası bekleyen", defects.Count(d => d.State == nameof(DefectState.AwaitingReinspect)).ToString(), "#1D4ED8"));
        SummaryCards.Children.Add(StatCard("Tamamlanan muayene", rows.Count(r => r.Status == nameof(SessionStatus.Completed)).ToString(), "#15803D"));
    }

    private static Border StatCard(string label, string value, string accent)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = value, FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush(accent) });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brush("#666666") });

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = Brush("#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            MinWidth = 160,
            Child = panel,
        };
    }

    private void RenderResults(IReadOnlyList<IndexRow> rows)
    {
        ResultsPanel.Children.Clear();

        if (rows.Count == 0)
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = _indexRows.Count == 0
                    ? "Henüz kayıt yok. Muayene ekranından oturum açın ya da Yenile'ye basın."
                    : "Filtrelere uyan kayıt yok.",
                Foreground = Brush("#666666"),
                Margin = new Thickness(4, 12, 0, 0),
            });
            return;
        }

        foreach (var r in rows)
            ResultsPanel.Children.Add(ResultCard(r));
    }

    private Border ResultCard(IndexRow r)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 6 };

        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(new TextBlock
        {
            Text = $"{r.Part}  ·  {r.Operation}  ·  {r.Serial}",
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("#1A3A5C"),
        });

        var (badgeText, badgeBg, badgeFg) = r.Status == nameof(SessionStatus.Completed)
            ? ("Tamamlandı", "#DCFCE7", "#15803D")
            : r.OpenDefects > 0
                ? ($"{r.OpenDefects} açık kusur", "#FEF3C7", "#B45309")
                : ("Açık kusur yok, kapanabilir", "#E0F2FE", "#0369A1");
        title.Children.Add(Chip(badgeText, badgeBg, badgeFg));
        left.Children.Add(title);

        var meta = string.Join("   ·   ", new[]
        {
            string.IsNullOrWhiteSpace(r.Inspector) ? null : $"Muayeneci: {r.Inspector}",
            r.CreatedAt is DateTime c ? $"Açılış: {c:dd.MM.yyyy HH:mm}" : null,
            $"{r.TotalDefects} kusur",
        }.Where(x => x is not null));
        left.Children.Add(new TextBlock { Text = meta, FontSize = 12, Foreground = Brush("#666666") });

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (r.Defects.Count == 0)
            chips.Children.Add(new TextBlock { Text = "Kusur kaydı yok", FontSize = 12, Foreground = Brush("#888888") });
        foreach (var d in r.Defects.OrderBy(x => x.Id))
        {
            var state = Enum.TryParse<DefectState>(d.State, out var ds) ? ds : DefectState.Open;
            var (bg, fg) = StateColors(state);
            chips.Children.Add(Chip($"#{d.Id} {d.Type} · {StateLabel(state)}", bg, fg));
        }
        left.Children.Add(new ScrollViewer
        {
            Content = chips,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
        });

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var open = new Button { Content = "Aç", VerticalAlignment = VerticalAlignment.Center, MinWidth = 80 };
        open.Click += (_, _) => OpenFromDashboard(r);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = r.OpenDefects > 0 ? Brush("#F5C26B") : Brush("#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid,
        };
    }

    private static Border Chip(string text, string bg, string fg) => new()
    {
        Background = Brush(bg),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 2, 8, 2),
        Child = new TextBlock { Text = text, FontSize = 12, Foreground = Brush(fg), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
    };

    private static (string Bg, string Fg) StateColors(DefectState s) => s switch
    {
        DefectState.Closed => ("#DCFCE7", "#15803D"),
        DefectState.Voided => ("#E5E7EB", "#6B7280"),
        DefectState.Superseded => ("#E0E7FF", "#3730A3"),
        DefectState.MrbPending => ("#EDE9FE", "#6D28D9"),
        _ => ("#FEF3C7", "#B45309"),
    };

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(string hex)
    {
        var h = hex.TrimStart('#');
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
            255,
            Convert.ToByte(h.Substring(0, 2), 16),
            Convert.ToByte(h.Substring(2, 2), 16),
            Convert.ToByte(h.Substring(4, 2), 16)));
    }

    /// <summary>Dashboard kartındaki kaydı Muayene ekranında açar.</summary>
    private void OpenFromDashboard(IndexRow r)
    {
        PartBox.Text = r.Part;
        OpBox.Text = r.Operation;
        SerialBox.Text = r.Serial;
        Nav.SelectedItem = Nav.MenuItems[0];

        if (string.IsNullOrWhiteSpace(InspectorBox.Text))
        {
            InspectorBox.Focus(FocusState.Programmatic);
            Show("Kaydı açmak için muayeneci adınızı girip 'Muayene oluştur'a basın.", InfoBarSeverity.Informational);
            return;
        }

        OpenSession_Click(this, new RoutedEventArgs());
    }

    private void VerifyAudit_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var store = new SessionStore(_store?.Root ?? _rootPath);
        var (ok, badLine) = AuditLog.Verify(store);
        DashInfo.Title = ok ? "Denetim kaydı sağlam" : "Denetim kaydı bozulmuş";
        DashInfo.Message = ok ? "Zincir baştan sona doğrulandı." : $"{badLine}. satırda zincir kırılmış.";
        DashInfo.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        DashInfo.IsOpen = true;
    });

    // ───────────────────────── Yardımcılar ─────────────────────────

    private void RequireSession()
    {
        if (_store is null || _folder is null || _session is null)
            throw new InvalidOperationException("Önce bir oturum açın.");
    }

    private void RequireSelection()
    {
        RequireSession();
        if (_selectedId is null)
            throw new InvalidOperationException("Önce listeden bir kusur seçin.");
    }

    private static double? ParseOptional(string text, string label)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return null;

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ||
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return v;

        throw new ArgumentException($"{label} sayı olmalı: '{text}'");
    }

    private static string Fmt(double? value) =>
        value is double v ? v.ToString("0.##", CultureInfo.InvariantCulture) : "";

    private static string StateLabel(DefectState state) => state switch
    {
        DefectState.Open => "Açık",
        DefectState.Reworking => "Rework",
        DefectState.Repairing => "Repair",
        DefectState.AwaitingReinspect => "Rework sonrası muayene bekliyor",
        DefectState.WaitingVendor => "Tedarikçide",
        DefectState.MrbPending => "MRB bekliyor",
        DefectState.Closed => "Kapandı",
        DefectState.Voided => "Void",
        DefectState.Superseded => "Devredildi",
        _ => state.ToString(),
    };

    private void Show(string message, InfoBarSeverity severity)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }

    /// <summary>Beklenen hataları yakalar ve InfoBar'da gösterir. Uygulama kapanmaz.</summary>
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException
                                    or FileNotFoundException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }
}
