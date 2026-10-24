using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;
using static MuayeneKayit.Presentation.DefectPresentation;
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
    /// <summary>Muayene ekranının durumu ve kuralları. Diyaloglar, kamera, düzenleyici ve rapor bu verileri kullanır.</summary>
    public InspectionViewModel Vm { get; }

    private CameraWindow? _camera;
    private PhotoEditorWindow? _editor;
    private readonly string _rootPath;
    private List<IndexRow> _indexRows = new();
    private bool _filtersReady;

    /// <summary>Sekme programla seçilirken SelectorBar olayları ViewModel'e geri yazılmaz.</summary>
    private bool _syncingTabs;

    public MainWindow(InspectionViewModel vm)
    {
        Vm = vm;
        _rootPath = vm.Store.Root;
        InitializeComponent();
        Title = "MuayeneKayit";

        Vm.Notify += (message, kind) => Show(message, kind switch
        {
            StatusKind.Success => InfoBarSeverity.Success,
            StatusKind.Warning => InfoBarSeverity.Warning,
            StatusKind.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        });
        Vm.PropertyChanged += Vm_PropertyChanged;

        DefectTypeBox.ItemsSource = Vm.TypeNames;
        DefectTypeBox.SelectedIndex = 0;

        FilterTypeBox.ItemsSource = new[] { "Tümü" }.Concat(Vm.TypeNames).ToList();
        FilterTypeBox.SelectedIndex = 0;
        foreach (var (label, tag) in StateFilters)
            FilterStateBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        FilterStateBox.SelectedIndex = 0;
        _filtersReady = true;

        Nav.SelectedItem = Nav.MenuItems[0];

        // Ana pencere kapanınca açık kamera penceresi de kapanır (kamera serbest kalır).
        Closed += (_, _) =>
        {
            _camera?.Close();
            _editor?.Close();
        };
    }

    /// <summary>ViewModel değişimlerinden yalnızca bağlama ile gösterilemeyenler: sekme, fotoğraf kartları, sekme kalınlığı.</summary>
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InspectionViewModel.SelectedStep):
                ShowTab(TabFor(Vm.SelectedStep));
                break;

            case nameof(InspectionViewModel.Current):
                // Fotoğraf kartları seçili kusur her yüklendiğinde yeniden çizilir.
                if (Vm.Current is { } current)
                {
                    RenderPhotos(current, Vm.IsEditable);
                    ShowTab(TabFor(Vm.SelectedStep));
                }
                else
                {
                    PhotoPanel.Children.Clear();
                    PhotoEmpty.Visibility = Visibility.Visible;
                }
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
        if (tag == "dash")
            RefreshIndex_Click(this, new RoutedEventArgs());
    }

    // ───────────────────────── Oturum ─────────────────────────

    private void OpenSession_Click(object sender, RoutedEventArgs e) => Vm.OpenSession();

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

    private void DetailTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_syncingTabs)
            return;

        ShowPanels(sender.SelectedItem ?? MeasureTabItem);
        if (sender.SelectedItem is SelectorBarItem item)
            Vm.SelectedStep = StepOf(item);
    }

    /// <summary>
    /// Sekmeyi seçer ve içeriğini doğrudan gösterir. SelectorBar ilk kez görünür olurken seçim olayı
    /// gelmeyebilir; panel görünürlüğü bu yüzden olaya bırakılmaz.
    /// </summary>
    private void ShowTab(SelectorBarItem item)
    {
        _syncingTabs = true;
        try
        {
            foreach (var i in new[] { MeasureTabItem, PhotoTabItem, DecisionTabItem, HistoryTabItem })
                i.IsSelected = ReferenceEquals(i, item);
        }
        finally
        {
            _syncingTabs = false;
        }
        ShowPanels(item);
    }

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

    /// <summary>Küçük resimler; işaret sayısı ve arka plan durumu ile. Tıklayınca düzenleyici açılır.</summary>
    private void RenderPhotos(Defect defect, bool editable)
    {
        PhotoPanel.Children.Clear();
        PhotoEmpty.Visibility = Vis(defect.Photos.Count == 0);
        var directory = Path.Combine(Vm.Folder!, "defects");

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

    /// <summary>
    /// Diyaloglarda onay düğmesi vurgulu (koyu), vazgeç sade. Varsayılan düğme yok: Enter yanlışlıkla kaydetmez.
    /// </summary>
    private static Style ConfirmButtonStyle => (Style)Application.Current.Resources["AccentButtonStyle"];

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
            Vm.RequireSelection();
            if ((sender as Button)?.Tag is not string photo || !PhotoMarkupValidator.IsPhotoFileName(photo))
                return;

            var id = Vm.SelectedId!.Value;
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
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = ConfirmButtonStyle,
                XamlRoot = Nav.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            Vm.Store.UpdateDefect(Vm.Folder!, id, d =>
            {
                d.Photos.Remove(photo);
                d.Markups.Remove(photo);
            });
            Vm.Audit.Append(Vm.User, "defect.photo.remove", $"#{id} {photo}");
            Vm.Refresh(id);
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
        if ((sender as Button)?.Tag is not string photo || !PhotoMarkupValidator.IsPhotoFileName(photo))
            throw new InvalidOperationException("Fotoğraf okunamadı.");
        OpenEditor(photo);
    });

    /// <summary>Fotoğrafı işaretleme / arka plan düzenleyicisinde açar.</summary>
    private void OpenEditor(string photo)
    {
        Vm.RequireSelection();

        var id = Vm.SelectedId!.Value;
        var folder = Vm.Folder!;
        var store = Vm.Store;
        var by = Vm.User;
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
            $"#{id} {defect.DefectType}   ·   {photo}   ·   {Vm.Session!.PartNumber} / {Vm.Session!.OperationNumber} / {Vm.Session!.SerialNumber}",
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
                Vm.Audit.Append(by, "defect.markup",
                    $"#{id} {photo} {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırıldı")}");

                if (Vm.SelectedId == id && Vm.Folder == folder)
                    Vm.Refresh(id);
                Show($"{photo} kaydedildi: {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırılmış kopya")}. Orijinal fotoğraf korunuyor.", InfoBarSeverity.Success);
            });
        editor.Closed += (_, _) =>
        {
            if (ReferenceEquals(_editor, editor))
                _editor = null;
        };
        _editor = editor;
        editor.Activate();
    }

    // ───────────────────────── Ölçüm ve fotoğraf ─────────────────────────

    private void SaveMeasurements_Click(object sender, RoutedEventArgs e) => Vm.SaveMeasurements();

    /// <summary>
    /// Kamera penceresini açar. Fotoğraflar, pencere açılırken seçili olan kusura kaydedilir;
    /// sonradan listede başka kusur seçilse bile hedef değişmez.
    /// </summary>
    private void CameraButton_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Vm.RequireSelection();

        var id = Vm.SelectedId!.Value;
        var folder = Vm.Folder!;
        var store = Vm.Store;
        var by = Vm.User;
        var target = $"#{id} {Vm.Current!.DefectType}   ·   {Vm.Session!.PartNumber} / {Vm.Session!.OperationNumber} / {Vm.Session!.SerialNumber}";

        // Aynı anda tek kamera penceresi: kamera tek uygulamaya açılabilir.
        _camera?.Close();

        string? saved = null;
        var camera = new CameraWindow(target, id, photo =>
        {
            var name = PhotoImporter.Import(folder, id, photo.TempPath);
            store.UpdateDefect(folder, id, d =>
            {
                d.Photos.Add(name);
                // Çerçeve açıkken çekildiyse ortadaki kare kusur işareti olarak kaydedilir.
                if (photo.Framed && photo.Width > 0 && photo.Height > 0)
                {
                    d.Markups[name] = new PhotoMarkup
                    {
                        ImageWidth = photo.Width,
                        ImageHeight = photo.Height,
                        Shapes = { Annotation.CenterSquare(photo.Width, photo.Height, id, photo.FrameFraction) },
                    };
                }
            });
            Vm.Audit.Append(by, "defect.photo", $"#{id} {name} (kamera{(photo.Framed ? ", çerçeveli" : "")})");
            saved = name;
            return name;
        });
        camera.Closed += (_, _) =>
        {
            if (ReferenceEquals(_camera, camera))
                _camera = null;

            // Fotoğraf kaydedildiyse doğrudan işaretleme ekranına geçilir.
            if (saved is not null && Vm.SelectedId == id && Vm.Folder == folder)
            {
                Vm.Refresh(id);
                ShowTab(PhotoTabItem);
                Run(() => OpenEditor(saved));
            }
        };
        _camera = camera;
        camera.Activate();
    });

    private async void AddPhoto_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Vm.RequireSelection();

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            var id = Vm.SelectedId!.Value;
            var folder = Vm.Folder!;
            var name = PhotoImporter.Import(folder, id, file.Path);

            Vm.Store.UpdateDefect(folder, id, d => d.Photos.Add(name));
            Vm.Audit.Append(Vm.User, "defect.photo", $"#{id} {name}");
            Vm.Refresh(id);
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
        var option = DecisionCombo.SelectedItem as DecisionOption;
        if (option is null || Vm.Current is null)
            return;

        try
        {
            if (option.Decision is Decision decision)
                await ShowDecisionDialog(Vm.Current, decision);
            else if (option.IsReinspect)
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
        var panel = new StackPanel { Spacing = 12, Width = 480 };

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
                Text = Vm.Session?.OperationNumber ?? "",
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

        // High metal'li kusurda rework kapsamı sorulur: yalnızca high metal mi, kusurla birlikte mi.
        RadioButtons? scope = null;
        if (decision == Decision.Rework && defect.HighMetal)
        {
            scope = new RadioButtons { Header = "Rework kapsamı (zorunlu)" };
            scope.Items.Add(new RadioButton { Content = "Kusur + high metal giderilecek", Tag = ReworkScope.DefectAndHighMetal });
            scope.Items.Add(new RadioButton { Content = "Yalnızca high metal giderilecek (ana kusur kalabilir)", Tag = ReworkScope.HighMetalOnly });
            panel.Children.Add(scope);
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
            DefaultButton = ContentDialogButton.None,
            PrimaryButtonStyle = ConfirmButtonStyle,
            IsPrimaryButtonEnabled = !noteRequired && scope is null,
            XamlRoot = Nav.XamlRoot,
        };
        void UpdateSave() => dialog.IsPrimaryButtonEnabled =
            (!noteRequired || !string.IsNullOrWhiteSpace(note.Text)) && (scope is null || scope.SelectedItem is not null);
        note.TextChanged += (_, _) => UpdateSave();
        if (scope is not null)
            scope.SelectionChanged += (_, _) => UpdateSave();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var id = defect.Id;
        var by = Vm.User;
        var source = SourceFor(decision);
        var noteText = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim();
        var refText = string.IsNullOrWhiteSpace(reference?.Text) ? null : reference!.Text.Trim();
        var targetText = target is null ? null : (string.IsNullOrWhiteSpace(target.Text) ? Vm.Session!.OperationNumber : target.Text.Trim());

        Vm.Store.UpdateDefect(Vm.Folder!, id, d =>
            DefectStateMachine.Apply(d, decision, source, by, note: noteText, reference: refText, targetOperation: targetText,
                reworkScope: (scope?.SelectedItem as RadioButton)?.Tag as ReworkScope?));

        Vm.Audit.Append(by, "disposition", $"#{id} {decision}");
        Vm.Refresh(id);
        Show($"#{id} için '{label}' kaydedildi. Yeni durum: {StateLabel(DefectStateMachine.GetState(Vm.Current ?? defect))}.", InfoBarSeverity.Success);

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
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = ConfirmButtonStyle,
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
            Vm.RequireSelection();
            var defect = Vm.Store.LoadDefects(Vm.Folder!).First(d => d.Id == Vm.SelectedId);
            var last = DefectStateMachine.LastUndoable(defect)
                ?? throw new InvalidOperationException("Geri alınacak karar yok.");

            var panel = new StackPanel { Spacing = 12, Width = 480 };
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
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = ConfirmButtonStyle,
                IsPrimaryButtonEnabled = false,
                XamlRoot = Nav.XamlRoot,
            };
            reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var (undone, voided) = Vm.Store.UndoLastDecision(Vm.Folder!, defect.Id, Vm.User, reason.Text);
            Vm.Audit.Append(Vm.User, "disposition.undo",
                $"#{defect.Id} {undone.Decision} (seq {undone.Seq}){(voided is int v ? $", child #{v} void" : "")}");
            Vm.Refresh(defect.Id);
            Show($"#{defect.Id}: '{DefectSentence.DecisionLabel(undone.Decision)}' geri alındı." +
                 (voided is int c ? $" Yeni kusur #{c} void edildi." : "") +
                 $" Durum: {StateLabel(DefectStateMachine.GetState(Vm.Current ?? defect))}.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Complete_Click(object sender, RoutedEventArgs e) => Vm.Complete();

    // ───────────────────────── Void ─────────────────────────

    /// <summary>
    /// Yanlış girilen kusuru geçersiz kılar. Gerekçe yazılmadan onay butonu açılmaz;
    /// kayıt silinmez, ortak alanda "void" olarak kalır ve rapora girmez.
    /// </summary>
    private async void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Vm.RequireSelection();
            var defect = Vm.Current!;

            var reason = new TextBox
            {
                Header = "Void gerekçesi (zorunlu)",
                PlaceholderText = "Örn. yanlış hata tipi seçildi, kusur başka parçaya ait",
                AcceptsReturn = true,
                Height = 80,
                TextWrapping = TextWrapping.Wrap,
            };

            var panel = new StackPanel { Spacing = 10, Width = 480 };
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
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = ConfirmButtonStyle,
                IsPrimaryButtonEnabled = false,
                XamlRoot = Nav.XamlRoot,
            };
            reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            var id = defect.Id;
            var by = Vm.User;
            var note = reason.Text.Trim();

            Vm.Store.UpdateDefect(Vm.Folder!, id, d =>
                DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, by, note: note));
            Vm.Audit.Append(by, "defect.void", $"#{id} {note}");
            Vm.Refresh(id);
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
        Vm.RequireSelection();
        var defect = Vm.Store.LoadDefects(Vm.Folder!).First(d => d.Id == Vm.SelectedId);
        if (DefectStateMachine.GetState(defect) != DefectState.AwaitingReinspect)
            throw new InvalidOperationException("Bu kusur rework sonrası muayene beklemiyor.");

        var effective = DefectStateMachine.Effective(defect);
        var sent = effective.LastOrDefault(x => x.Decision is Decision.Rework or Decision.Reinspect);
        var isRework = sent?.Decision == Decision.Rework;
        var before = sent?.Snapshot ?? MeasurementEditor.Snapshot(defect);
        var template = Vm.Types.FirstOrDefault(t => string.Equals(t.Name, defect.DefectType, StringComparison.OrdinalIgnoreCase));

        var hadHighMetal = before.HighMetal;
        var panel = new StackPanel { Spacing = 12, Width = 480 };
        if (sent?.ReworkScope == ReworkScope.HighMetalOnly)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Rework kapsamı: yalnızca high metal için yapıldı. Ana kusur kalabilir.",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }
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
        outcome.Items.Add(new RadioButton
        {
            Content = hadHighMetal
                ? "Ana kusur hâlâ var (high metal giderilmiş olabilir) → yeni ölçüm / durum gir"
                : "Aynı kusur hâlâ var → yeni ölçümleri gir, sonra yeni karar ver",
            Tag = "same",
        });
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
                new TextBlock { Text = (hadHighMetal ? "3." : "2.") + " Rework sonrası ölçümleri girin (boş bırakılan alan önceki değerinde kalır)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                new ScrollViewer { Content = measureGrid, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollMode = ScrollMode.Disabled },
                delta,
            },
        };
        // High metal'li kusurda durum açıkça sorulur (varsayılan yok): yalnızca high metal gidip değerler aynı kalabilir.
        RadioButtons? highMetal = null;
        if (hadHighMetal)
        {
            highMetal = new RadioButtons { Header = "2. High metal durumu (zorunlu)" };
            highMetal.Items.Add(new RadioButton { Content = "Giderildi", Tag = false });
            highMetal.Items.Add(new RadioButton { Content = "Hâlâ var", Tag = true });
            samePanel.Children.Insert(0, highMetal);
        }
        panel.Children.Add(samePanel);

        // 2. adım (yeni hata): child tipi, varsayılan seçili değil
        var childType = new ComboBox { Header = "2. Yeni hatanın tipi", ItemsSource = Vm.TypeNames, PlaceholderText = "Tip seçin", Width = 300 };
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
            DefaultButton = ContentDialogButton.None,
            PrimaryButtonStyle = ConfirmButtonStyle,
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
            var hm = (highMetal?.SelectedItem as RadioButton)?.Tag is bool b ? b : before.HighMetal;
            return new MeasurementSnapshot(v.Depth ?? before.Depth, v.Width ?? before.Width, v.Length ?? before.Length,
                v.Radius ?? before.Radius, v.AngleDeg ?? before.AngleDeg, v.Color ?? before.Color, hm);
        }

        bool HighMetalAnswered() => highMetal is null || highMetal.SelectedItem is not null;

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
                var now = new Defect { Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color, HighMetal = v.HighMetal };
                delta.Text = AnyValue() || (highMetal is not null && HighMetalAnswered())
                    ? string.Join("\n", MeasurementCompare.Lines(before, now))
                    : "Değişimi görmek için ölçüm girin.";
            }

            dialog.IsPrimaryButtonEnabled = tag switch
            {
                "conforms" => true,
                // High metal varsa durumu zorunlu, ölçümler boş kalabilir (değerler aynı); yoksa en az bir ölçüm.
                "same" => highMetal is not null ? HighMetalAnswered() : AnyValue(),
                "new" => childType.SelectedItem is string,
                _ => false,
            };
        }

        outcome.SelectionChanged += (_, _) => Refresh();
        childType.SelectionChanged += (_, _) => Refresh();
        if (highMetal is not null)
            highMetal.SelectionChanged += (_, _) => Refresh();
        foreach (var box in boxes.Values.Append(colorBox).OfType<TextBox>())
            box.TextChanged += (_, _) => Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var result = Selected() ?? throw new InvalidOperationException("Muayene sonucunu seçin.");
        var id = defect.Id;
        var by = Vm.User;
        var noteText = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim();

        switch (result)
        {
            case "conforms":
                Vm.Store.UpdateDefect(Vm.Folder!, id, d =>
                    DefectStateMachine.Apply(d, Decision.AfterReworkConforms, DecisionSource.Engineer, by, noteText));
                Vm.Audit.Append(by, "disposition", $"#{id} AfterReworkConforms");
                Vm.Refresh(id);
                Show($"#{id} uygun bulundu ve kapandı.", InfoBarSeverity.Success);
                break;

            case "same":
                var v = Merged();
                var probe = new Defect { DefectType = defect.DefectType, Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color };
                var errors = DefectValidator.Validate(probe);
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(" ", errors));

                // High metal giderildiyse ve not yazılmadıysa otomatik not: kalan ana kusurun kararı buna göre verilir.
                var sameNote = noteText ?? (hadHighMetal && !v.HighMetal ? "High metal giderildi; ana kusur devam ediyor." : null);

                // Yeni ölçümler ve karar aynı kilit altında: karar anı görüntüsü yeni değerleri taşır.
                Vm.Store.UpdateDefect(Vm.Folder!, id, d =>
                {
                    MeasurementEditor.Apply(d, v, by);
                    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, by, sameNote);
                });
                Vm.Audit.Append(by, "disposition", $"#{id} AfterReworkSameDefect");
                Vm.Refresh(id);
                Show(hadHighMetal && !v.HighMetal
                        ? $"#{id}: high metal giderildi, ana kusur devam ediyor. Şimdi Disposition'dan kalan kusur için karar verin."
                        : $"#{id}: aynı kusur devam ediyor, yeni ölçümler kaydedildi. Şimdi Disposition'dan yeni karar verin.",
                    InfoBarSeverity.Warning);
                break;

            case "new":
                var newType = childType.SelectedItem as string ?? throw new InvalidOperationException("Yeni hata tipini seçin.");
                var child = Vm.Store.ContinueAsChild(Vm.Folder!, id, newType, DecisionSource.Engineer, by, noteText, null);
                Vm.Audit.Append(by, "disposition.continue", $"#{id} -> #{child.Id} {newType}");
                Vm.Refresh(child.Id);
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
            Vm.RequireSession();
            ReportButton.IsEnabled = false;

            var reportsDir = Path.Combine(Vm.Folder!, "reports");
            Directory.CreateDirectory(reportsDir);

            var version = ReportFiles.LatestVersion(Vm.Folder!) + 1;
            var htmlPath = Path.Combine(reportsDir, $"Rapor_v{version}.html");
            var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");

            var defects = Vm.Store.LoadDefects(Vm.Folder!);
            AtomicFile.WriteAllText(htmlPath, ReportBuilder.Build(Vm.Session!, defects, version));

            Show($"Rapor v{version} hazırlanıyor (PDF)...", InfoBarSeverity.Informational);
            var pdfError = await ReportPdf.TryConvertAsync(htmlPath, pdfPath);

            Vm.Audit.Append(Vm.User, "report.create", $"Rapor_v{version}{(pdfError is null ? " (pdf)" : "")}");
            Process.Start(new ProcessStartInfo(pdfError is null ? pdfPath : htmlPath) { UseShellExecute = true });
            if (pdfError is null)
                Status.IsOpen = false;
            else
                Show($"Rapor v{version} HTML olarak açıldı (PDF üretilemedi: {pdfError}). Tarayıcıdan 'PDF olarak kaydet' ile yazdırabilirsiniz.",
                    InfoBarSeverity.Warning);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            ReportButton.IsEnabled = Vm.IsSessionOpen;
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
        var store = new SessionStore(_rootPath);
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
        Vm.PartNumber = r.Part;
        Vm.OperationNumber = r.Operation;
        Vm.SerialNumber = r.Serial;
        Nav.SelectedItem = Nav.MenuItems[0];

        if (string.IsNullOrWhiteSpace(Vm.InspectorName))
        {
            InspectorBox.Focus(FocusState.Programmatic);
            Show("Kaydı açmak için muayeneci adınızı girip 'Muayene oluştur'a basın.", InfoBarSeverity.Informational);
            return;
        }

        OpenSession_Click(this, new RoutedEventArgs());
    }

    private void VerifyAudit_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var store = new SessionStore(_rootPath);
        var (ok, badLine) = AuditLog.Verify(store);
        DashInfo.Title = ok ? "Denetim kaydı sağlam" : "Denetim kaydı bozulmuş";
        DashInfo.Message = ok ? "Zincir baştan sona doğrulandı." : $"{badLine}. satırda zincir kırılmış.";
        DashInfo.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        DashInfo.IsOpen = true;
    });

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
