using System.Diagnostics;
using System.Text;
using MuayeneKayit.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace MuayeneKayit.Admin;

/// <summary>
/// Admin paneli. PIN ile açılır; kusur tipi şablonlarını, ortak alan bakımını ve tüm kayıtların
/// salt okunur görünümünü yönetir. Muayene kayıtlarını değiştirmez.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan IdleLock = TimeSpan.FromMinutes(10);

    private string _root = AppSettings.LoadRootPath();
    private int _failed;
    private DateTime _lockedUntil = DateTime.MinValue;
    private DateTime _lastInput = DateTime.Now;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private List<IndexRow> _rows = new();
    private List<DefectTypeTemplate> _types = new();
    private bool _ready;

    private static readonly (string Label, IndexStateFilter Tag)[] StateFilters =
    {
        ("Tümü", IndexStateFilter.All),
        ("Açık kusuru olanlar", IndexStateFilter.HasOpen),
        ("MRB bekleyen", IndexStateFilter.MrbPending),
        ("Rework sonrası muayene bekleyen", IndexStateFilter.AwaitingReinspect),
        ("Tedarikçide", IndexStateFilter.WaitingVendor),
        ("Tamamlanan muayeneler", IndexStateFilter.Completed),
    };

    public MainWindow()
    {
        InitializeComponent();
        Title = "MuayeneKayit Admin";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));

        foreach (var (label, tag) in StateFilters)
            FState.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        FState.SelectedIndex = 0;

        // Boşta kalan oturum kilitlenir: admin ekranı açık bırakılırsa başkası kullanamaz.
        Root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => _lastInput = DateTime.Now), true);
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => _lastInput = DateTime.Now), true);
        _idleTimer.Tick += (_, _) =>
        {
            if (Nav.Visibility == Visibility.Visible && DateTime.Now - _lastInput > IdleLock)
                Lock("10 dakika işlem yapılmadığı için kilitlendi.");
        };

        ShowLogin(null);
    }

    private FrameworkElement Root => (FrameworkElement)Content;

    // ───────────────────────── Giriş ─────────────────────────

    private void ShowLogin(string? message)
    {
        var first = !SafeHasPin();
        LoginHint.Text = first
            ? "İlk kurulum: admin PIN'i belirleyin (6-12 rakam). PIN ortak alanda yalnızca özet olarak saklanır."
            : "Devam etmek için admin PIN'ini girin.";
        PinRepeatBox.Visibility = first ? Visibility.Visible : Visibility.Collapsed;
        LoginButton.Content = first ? "PIN'i belirle ve gir" : "Giriş";
        LoginError.Text = message ?? "";
        LoginRoot.Text = $"Ortak alan: {_root}";
        PinBox.Password = PinRepeatBox.Password = "";
        LoginPanel.Visibility = Visibility.Visible;
        Nav.Visibility = Visibility.Collapsed;
        PinBox.Focus(FocusState.Programmatic);
    }

    private bool SafeHasPin()
    {
        try { return SharedConfig.HasPin(_root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private void PinBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            Login_Click(sender, new RoutedEventArgs());
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DateTime.Now < _lockedUntil)
            {
                LoginError.Text = $"Çok fazla hatalı deneme. {(int)(_lockedUntil - DateTime.Now).TotalSeconds + 1} sn bekleyin.";
                return;
            }

            if (!SharedConfig.HasPin(_root))
            {
                if (PinBox.Password != PinRepeatBox.Password)
                {
                    LoginError.Text = "PIN'ler aynı değil.";
                    return;
                }
                SharedConfig.SetPin(_root, PinBox.Password);
                AuditLog.Append(new SessionStore(_root), "admin", "admin.pin.set", "İlk PIN belirlendi");
            }
            else if (!SharedConfig.VerifyPin(_root, PinBox.Password))
            {
                _failed++;
                if (_failed >= MaxAttempts)
                {
                    _failed = 0;
                    _lockedUntil = DateTime.Now + LockoutTime;
                    AuditLog.Append(new SessionStore(_root), "admin", "admin.login.lockout", Environment.MachineName);
                }
                LoginError.Text = "PIN hatalı.";
                PinBox.Password = "";
                return;
            }

            _failed = 0;
            AuditLog.Append(new SessionStore(_root), "admin", "admin.login", Environment.MachineName);
            LoginPanel.Visibility = Visibility.Collapsed;
            Nav.Visibility = Visibility.Visible;
            _lastInput = DateTime.Now;
            _idleTimer.Start();
            EnterAdmin();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                    or InvalidDataException or TimeoutException or InvalidOperationException)
        {
            LoginError.Text = ex.Message;
        }
    }

    private void Lock(string? message)
    {
        _idleTimer.Stop();
        ShowLogin(message);
    }

    private void EnterAdmin()
    {
        _ready = false;
        _types = SharedConfig.LoadDefectTypes(_root).Select(Clone).ToList();
        FType.ItemsSource = new[] { "Tümü" }.Concat(_types.Select(t => t.Name)).ToList();
        FType.SelectedIndex = 0;
        RootBox.Text = _root;
        _ready = true;

        Nav.SelectedItem = Nav.MenuItems[0];
        RefreshTypeList(0);
    }

    // ───────────────────────── Navigasyon ─────────────────────────

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag == "lock")
        {
            Lock("Kilitlendi.");
            return;
        }

        RecordsPanel.Visibility = tag == "records" ? Visibility.Visible : Visibility.Collapsed;
        TypesPanel.Visibility = tag == "types" ? Visibility.Visible : Visibility.Collapsed;
        TextsPanel.Visibility = tag == "texts" ? Visibility.Visible : Visibility.Collapsed;
        MaintPanel.Visibility = tag == "maint" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "records")
            RefreshRecords_Click(this, new RoutedEventArgs());
        else if (tag == "maint")
            LoadAudit();
        else if (tag == "texts")
            RenderTexts(SharedConfig.LoadDecisionTexts(_root));
    }

    // ───────────────────────── Karar açıklamaları ─────────────────────────

    /// <summary>Muayene ekranındaki karar listesiyle aynı sıra ve adlar.</summary>
    private static readonly (Decision Decision, string Label)[] DecisionLabels =
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

    private readonly Dictionary<Decision, TextBox> _textBoxes = new();

    private void RenderTexts(IReadOnlyDictionary<Decision, string> texts)
    {
        TextsList.Children.Clear();
        _textBoxes.Clear();
        TextsDirty.Text = "";

        foreach (var (decision, label) in DecisionLabels)
        {
            var box = new TextBox
            {
                Text = texts.GetValueOrDefault(decision, ""),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 64,
                MaxLength = SharedConfig.MaxDecisionTextLength,
            };
            box.TextChanged += (_, _) => TextsDirty.Text = "Kaydedilmemiş değişiklik var.";
            _textBoxes[decision] = box;

            var reset = new Button { Content = "Varsayılan" };
            reset.Click += (_, _) => box.Text = SharedConfig.DefaultDecisionTexts[decision];

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("#1A3A5C"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(reset, 1);
            header.Children.Add(reset);

            TextsList.Children.Add(new Border
            {
                Background = Brush("#FFFFFF"),
                BorderBrush = Brush("#DDE2EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 12),
                Child = new StackPanel { Spacing = 8, Children = { header, box } },
            });
        }
    }

    private void SaveTexts_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var texts = _textBoxes.ToDictionary(x => x.Key, x => x.Value.Text);
        SharedConfig.SaveDecisionTexts(_root, texts);
        AuditLog.Append(new SessionStore(_root), "admin", "config.decision_texts", $"{texts.Count} açıklama");
        TextsDirty.Text = "";
        Show("Karar açıklamaları kaydedildi. Muayene ekranında bir sonraki karar seçiminde görünür.", InfoBarSeverity.Success);
    });

    private void ResetTexts_Click(object sender, RoutedEventArgs e)
    {
        RenderTexts(SharedConfig.DefaultDecisionTexts);
        TextsDirty.Text = "Varsayılanlar yüklendi; kalıcı olması için 'Ortak alana kaydet'e basın.";
    }

    // ───────────────────────── Kayıtlar ─────────────────────────

    private void RefreshRecords_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        _rows = IndexBuilder.Rebuild(new SessionStore(_root));
        ApplyFilters();
    });

    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void FilterCombo_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private List<IndexRow> Filtered()
    {
        var type = FType.SelectedItem as string;
        var state = (FState.SelectedItem as ComboBoxItem)?.Tag is IndexStateFilter s ? s : IndexStateFilter.All;
        return IndexFilter.Apply(_rows, new IndexCriteria(
            FPart.Text, FOp.Text, FSerial.Text, type is null or "Tümü" ? null : type, state));
    }

    private void ApplyFilters()
    {
        if (!_ready)
            return;

        var list = Filtered();
        SummaryCards.Children.Clear();
        SummaryCards.Children.Add(Stat("Kayıt", list.Count.ToString(), "#1A3A5C"));
        SummaryCards.Children.Add(Stat("Açık kusurlu", list.Count(r => r.OpenDefects > 0).ToString(), "#B45309"));
        SummaryCards.Children.Add(Stat("MRB bekleyen", list.Count(r => r.Defects.Any(d => d.State == nameof(DefectState.MrbPending))).ToString(), "#7C3AED"));
        SummaryCards.Children.Add(Stat("Tamamlanan", list.Count(r => r.Status == nameof(SessionStatus.Completed)).ToString(), "#15803D"));

        ResultsPanel.Children.Clear();
        if (list.Count == 0)
            ResultsPanel.Children.Add(new TextBlock { Text = "Kriterlere uyan kayıt yok.", Foreground = Brush("#888888") });

        foreach (var r in list.Take(500))
            ResultsPanel.Children.Add(RecordCard(r));
    }

    private UIElement RecordCard(IndexRow r)
    {
        var title = new TextBlock
        {
            Text = $"{r.Part}  ·  {r.Operation}  ·  {r.Serial}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("#1A3A5C"),
            FontSize = 15,
        };
        var meta = new TextBlock
        {
            Text = $"Muayeneci: {r.Inspector}   ·   Açılış: {r.CreatedAt:dd.MM.yyyy HH:mm}   ·   {r.TotalDefects} kusur, {r.OpenDefects} açık   ·   {(r.Status == nameof(SessionStatus.Completed) ? "Tamamlandı" : "Açık")}",
            Foreground = Brush("#666666"),
            FontSize = 12,
        };
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var d in r.Defects.Take(12))
            chips.Children.Add(Chip($"#{d.Id} {d.Type} · {d.State}", d.State));

        var folder = Path.Combine(_root, r.Part, r.Operation, r.Serial);
        var open = new Button { Content = "Klasörü aç" };
        open.Click += (_, _) => Run(() => OpenFolder(folder));
        var report = new Button { Content = "Son rapor" };
        report.Click += (_, _) => Run(() => OpenLatestReport(folder));

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Spacing = 4, Children = { title, meta, chips } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { report, open } };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = Brush(r.OpenDefects > 0 ? "#F59E0B" : "#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = grid,
        };
    }

    /// <summary>Klasörü Gezgin'de açar. Yol, kök altında olduğu doğrulanmış index satırından gelir.</summary>
    private void OpenFolder(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (!full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full))
            throw new InvalidOperationException("Klasör bulunamadı.");
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { full }, UseShellExecute = false });
    }

    private void OpenLatestReport(string folder)
    {
        var version = ReportFiles.LatestVersion(folder);
        if (version == 0)
            throw new InvalidOperationException("Bu kayıt için rapor yok.");
        var pdf = Path.Combine(folder, "reports", $"Rapor_v{version}.pdf");
        var html = Path.ChangeExtension(pdf, ".html");
        Process.Start(new ProcessStartInfo(File.Exists(pdf) ? pdf : html) { UseShellExecute = true });
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"MuayeneKayit_{DateTime.Now:yyyyMMdd_HHmm}" };
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            var list = Filtered();
            await File.WriteAllTextAsync(file.Path, CsvExport.Build(list), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Show($"{list.Count} kayıt dışa aktarıldı: {file.Path}", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }

    // ───────────────────────── Kusur tipleri ─────────────────────────

    private void RefreshTypeList(int? select)
    {
        TypeList.ItemsSource = _types.Select(t => $"{t.Name}   ({FieldsText(t)})").ToList();
        if (select is int i && i >= 0 && i < _types.Count)
            TypeList.SelectedIndex = i;
    }

    private static string FieldsText(DefectTypeTemplate t) =>
        t.Fields.Count == 0 ? "ölçüm yok" : string.Join(", ", t.Fields.Select(FieldLabel));

    private static string FieldLabel(MeasureField f) => f switch
    {
        MeasureField.Depth => "D",
        MeasureField.Width => "W",
        MeasureField.Length => "L",
        MeasureField.Radius => "R",
        MeasureField.Angle => "Açı",
        MeasureField.Color => "Renk",
        _ => f.ToString(),
    };

    private (CheckBox Box, MeasureField Field)[] FieldBoxes => new[]
    {
        (FieldDepth, MeasureField.Depth), (FieldWidth, MeasureField.Width), (FieldLength, MeasureField.Length),
        (FieldRadius, MeasureField.Radius), (FieldAngle, MeasureField.Angle), (FieldColor, MeasureField.Color),
    };

    private void TypeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var i = TypeList.SelectedIndex;
        if (i < 0 || i >= _types.Count)
            return;
        var t = _types[i];
        TypeName.Text = t.Name;
        TypeSpec.Text = t.SpecName ?? "";
        foreach (var (box, field) in FieldBoxes)
            box.IsChecked = t.Fields.Contains(field);
    }

    private void ApplyType_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var i = TypeList.SelectedIndex;
        if (i < 0)
            throw new InvalidOperationException("Listeden bir tip seçin.");

        var candidate = new DefectTypeTemplate
        {
            Name = TypeName.Text.Trim(),
            SpecName = string.IsNullOrWhiteSpace(TypeSpec.Text) ? null : TypeSpec.Text.Trim(),
            Fields = FieldBoxes.Where(x => x.Box.IsChecked == true).Select(x => x.Field).ToList(),
        };
        var trial = _types.ToList();
        trial[i] = candidate;
        var errors = SharedConfig.ValidateTypes(trial);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        _types = trial;
        RefreshTypeList(i);
        MarkDirty();
    });

    private void AddType_Click(object sender, RoutedEventArgs e)
    {
        var n = 1;
        while (_types.Any(t => t.Name.Equals($"Yeni tip {n}", StringComparison.OrdinalIgnoreCase)))
            n++;
        _types.Add(new DefectTypeTemplate { Name = $"Yeni tip {n}", Fields = { MeasureField.Depth, MeasureField.Width, MeasureField.Length } });
        RefreshTypeList(_types.Count - 1);
        MarkDirty();
    }

    private void DeleteType_Click(object sender, RoutedEventArgs e)
    {
        var i = TypeList.SelectedIndex;
        if (i < 0)
            return;
        _types.RemoveAt(i);
        RefreshTypeList(Math.Min(i, _types.Count - 1));
        MarkDirty();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        var i = TypeList.SelectedIndex;
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _types.Count)
            return;
        (_types[i], _types[j]) = (_types[j], _types[i]);
        RefreshTypeList(j);
        MarkDirty();
    }

    private void ResetTypes_Click(object sender, RoutedEventArgs e)
    {
        _types = SharedConfig.Defaults.Select(Clone).ToList();
        RefreshTypeList(0);
        MarkDirty();
    }

    private void SaveTypes_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        SharedConfig.SaveDefectTypes(_root, _types);
        AuditLog.Append(new SessionStore(_root), "admin", "config.defect_types", string.Join(", ", _types.Select(t => t.Name)));
        TypesDirty.Text = "";
        FType.ItemsSource = new[] { "Tümü" }.Concat(_types.Select(t => t.Name)).ToList();
        FType.SelectedIndex = 0;
        Show("Kusur tipleri kaydedildi. Muayene uygulamaları bir sonraki açılışta yeni listeyi kullanır.", InfoBarSeverity.Success);
    });

    private void MarkDirty() => TypesDirty.Text = "Kaydedilmemiş değişiklik var. 'Tümünü ortak alana kaydet' ile kaydedin.";

    private static DefectTypeTemplate Clone(DefectTypeTemplate t) =>
        new() { Name = t.Name, SpecName = t.SpecName, Fields = t.Fields.ToList() };

    // ───────────────────────── Bakım ─────────────────────────

    private void RebuildIndex_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        _rows = IndexBuilder.Rebuild(new SessionStore(_root));
        Show($"Index yeniden oluşturuldu: {_rows.Count} kayıt.", InfoBarSeverity.Success);
    });

    private void VerifyAudit_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var (ok, bad) = AuditLog.Verify(new SessionStore(_root));
        Show(ok ? "Denetim zinciri sağlam." : $"Denetim zinciri {bad}. satırda bozuk: kayıt değiştirilmiş ya da silinmiş.",
            ok ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        LoadAudit();
    });

    private void LoadAudit()
    {
        Run(() =>
        {
            AuditList.ItemsSource = AuditLog.ReadTail(new SessionStore(_root), 300)
                .Select(a => $"{(DateTime.TryParse(a.Ts, out var ts) ? ts.ToString("dd.MM.yyyy HH:mm:ss") : a.Ts)}   {a.Actor,-14}   {a.Action,-22}   {a.Details}")
                .ToList();
        });
    }

    private void ScanLocks_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var locks = SharedConfig.StaleLocks(_root);
        LockList.ItemsSource = locks.ToList();
        Show(locks.Count == 0 ? "Eski kilit yok." : $"{locks.Count} eski kilit bulundu.", locks.Count == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    });

    private void RemoveLock_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (LockList.SelectedItem is not string path)
            throw new InvalidOperationException("Listeden bir kilit seçin.");
        SharedConfig.RemoveStaleLock(_root, path);
        AuditLog.Append(new SessionStore(_root), "admin", "admin.lock.remove", path);
        LockList.ItemsSource = SharedConfig.StaleLocks(_root).ToList();
        Show("Kilit silindi.", InfoBarSeverity.Success);
    });

    // ───────────────────────── Ayarlar ─────────────────────────

    private void SaveRoot_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var root = Environment.ExpandEnvironmentVariables(RootBox.Text.Trim());
        if (!Directory.Exists(root))
            throw new InvalidOperationException("Klasöre erişilemiyor. Yolu ve ağ bağlantısını kontrol edin.");

        AppSettings.SaveRootPath(RootBox.Text.Trim());
        _root = root;
        Lock("Ortak alan değişti; yeni alandaki PIN ile tekrar giriş yapın.");
    });

    private void ChangePin_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!SharedConfig.VerifyPin(_root, OldPin.Password))
            throw new InvalidOperationException("Mevcut PIN hatalı.");
        if (NewPin.Password != NewPinRepeat.Password)
            throw new InvalidOperationException("Yeni PIN'ler aynı değil.");

        SharedConfig.SetPin(_root, NewPin.Password);
        AuditLog.Append(new SessionStore(_root), "admin", "admin.pin.change", Environment.MachineName);
        OldPin.Password = NewPin.Password = NewPinRepeat.Password = "";
        Show("PIN değiştirildi.", InfoBarSeverity.Success);
    });

    // ───────────────────────── Yardımcılar ─────────────────────────

    private static Border Stat(string label, string value, string color) => new()
    {
        Background = Brush("#FFFFFF"),
        BorderBrush = Brush("#DDE2EA"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 10, 16, 10),
        MinWidth = 150,
        Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = value, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush(color) },
                new TextBlock { Text = label, FontSize = 12, Foreground = Brush("#666666") },
            },
        },
    };

    private static Border Chip(string text, string state)
    {
        var (bg, fg) = state switch
        {
            nameof(DefectState.Closed) => ("#DCFCE7", "#15803D"),
            nameof(DefectState.MrbPending) => ("#EDE9FE", "#6D28D9"),
            nameof(DefectState.Voided) => ("#F3F4F6", "#6B7280"),
            nameof(DefectState.Superseded) => ("#E0E7FF", "#3730A3"),
            _ => ("#FEF3C7", "#B45309"),
        };
        return new Border
        {
            Background = Brush(bg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = Brush(fg) },
        };
    }

    private static SolidColorBrush Brush(string hex)
    {
        var v = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v));
    }

    private void Show(string message, InfoBarSeverity severity)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Show(ex.Message, InfoBarSeverity.Error);
        }
    }
}
