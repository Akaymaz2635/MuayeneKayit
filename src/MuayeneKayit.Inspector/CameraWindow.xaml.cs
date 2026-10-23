using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Enumeration;
using DevicePanel = Windows.Devices.Enumeration.Panel;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kamera penceresi. İki saha donanımı desteklenir:
/// notebook + harici kamera (C920) ve Surface Pro 9 (arka kamera).
/// Seçim sırası: son kullanılan → adında C920 olan → harici → arka kamera.
/// Yalnızca ön (selfie) kamera varsa otomatik seçilmez, kullanıcı listeden seçer.
/// Zoom yalnızca görüntülemek içindir; fotoğraf kameranın tam çözünürlüğünde kaydedilir.
/// </summary>
public sealed partial class CameraWindow : Window
{
    private const string PreferredCameraName = "C920";

    /// <summary>Geçici JPEG yolunu alır, kusura kaydeder ve dosya adını döndürür.</summary>
    private readonly Func<string, string> _onSave;

    private MediaCapture? _capture;
    private InMemoryRandomAccessStream? _lastPhoto;
    private bool _syncingZoom;
    private bool _closed;

    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MuayeneKayit",
        "camera.json");

    public CameraWindow(string target, Func<string, string> onSave)
    {
        InitializeComponent();
        _onSave = onSave;

        Title = $"Kamera — {target}";
        TargetText.Text = target;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 820));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            presenter.Maximize();

        Closed += async (_, _) =>
        {
            _closed = true;
            await StopAsync();
        };

        _ = LoadDevicesAsync();
    }

    // ───────────────────────── Cihaz seçimi ─────────────────────────

    private async Task LoadDevicesAsync()
    {
        Status("Kameralar aranıyor…");
        CaptureButton.IsEnabled = false;

        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        if (_closed)
            return;

        DeviceBox.SelectionChanged -= DeviceBox_SelectionChanged;
        DeviceBox.Items.Clear();
        foreach (var d in devices)
            DeviceBox.Items.Add(new ComboBoxItem { Content = $"{d.Name}  ({LocationLabel(d)})", Tag = d.Id });
        DeviceBox.SelectedIndex = -1;
        DeviceBox.SelectionChanged += DeviceBox_SelectionChanged;

        if (devices.Count == 0)
        {
            Status("Kamera bulunamadı.\nNotebook: harici kamerayı (C920) USB'ye takın.   Surface: kameranın Windows ayarlarında açık olduğunu kontrol edin.\nSonra 'Kameraları yenile'ye basın.");
            return;
        }

        // Son kullanılan → C920 → harici → arka. Ön (selfie) kamera otomatik seçilmez.
        var saved = LoadSavedDeviceId();
        var index = IndexOf(devices, d => d.Id == saved);
        if (index < 0)
            index = IndexOf(devices, d => d.Name.Contains(PreferredCameraName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = IndexOf(devices, IsExternal);
        if (index < 0)
            index = IndexOf(devices, d => d.EnclosureLocation?.Panel == DevicePanel.Back);

        if (index < 0)
        {
            Status("Harici ya da arka kamera bulunamadı.\nHarici kamerayı takıp 'Kameraları yenile'ye basın ya da üstteki listeden bir kamera seçin.");
            return;
        }

        DeviceBox.SelectedIndex = index;
    }

    /// <summary>Gövdeye yerleşik olmayan (USB ile takılan) kameraların konumu tanımsızdır.</summary>
    private static bool IsExternal(DeviceInformation d) =>
        d.EnclosureLocation is null || d.EnclosureLocation.Panel == DevicePanel.Unknown;

    private static string LocationLabel(DeviceInformation d) =>
        IsExternal(d) ? "harici"
        : d.EnclosureLocation!.Panel switch
        {
            DevicePanel.Back => "arka",
            DevicePanel.Front => "ön",
            _ => "dahili",
        };

    private static int IndexOf(DeviceInformationCollection devices, Func<DeviceInformation, bool> match)
    {
        for (var i = 0; i < devices.Count; i++)
        {
            if (match(devices[i]))
                return i;
        }
        return -1;
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        await StopAsync();
        await LoadDevicesAsync();
    }

    private async void DeviceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((DeviceBox.SelectedItem as ComboBoxItem)?.Tag is string id)
            await StartAsync(id);
    }

    // ───────────────────────── Başlat / durdur ─────────────────────────

    private async Task StartAsync(string deviceId)
    {
        await StopAsync();
        ShowLive();
        CaptureButton.IsEnabled = false;
        Status("Kamera açılıyor…");

        var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                VideoDeviceId = deviceId,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            });

            if (_closed)
            {
                capture.Dispose();
                return;
            }

            capture.Failed += (_, args) => DispatcherQueue.TryEnqueue(() =>
            {
                CaptureButton.IsEnabled = false;
                Status($"Kamera bağlantısı koptu ({args.Message}).\nKabloyu kontrol edip 'Kameraları yenile'ye basın.");
            });

            // Renkli kaynak; mümkünse en yüksek çözünürlük (en az 15 fps).
            var source = capture.FrameSources.Values.FirstOrDefault(s =>
                              s.Info.SourceKind == MediaFrameSourceKind.Color &&
                              s.Info.MediaStreamType == MediaStreamType.VideoRecord)
                         ?? capture.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                         ?? throw new InvalidOperationException("Bu cihaz renkli görüntü vermiyor.");

            var best = source.SupportedFormats
                .Where(f => f.VideoFormat.Width > 0 && FrameRate(f) >= 15)
                .OrderByDescending(f => (long)f.VideoFormat.Width * f.VideoFormat.Height)
                .ThenByDescending(FrameRate)
                .FirstOrDefault();
            if (best is not null)
                await source.SetFormatAsync(best);

            _capture = capture;
            Preview.Source = MediaSource.CreateFromMediaFrameSource(source);

            var format = source.CurrentFormat;
            ResolutionText.Text = $"Kayıt: {format.VideoFormat.Width}×{format.VideoFormat.Height}";
            SetupFocus(capture);
            SaveDeviceId(deviceId);

            Status("");
            CaptureButton.IsEnabled = true;
        }
        catch (UnauthorizedAccessException)
        {
            capture.Dispose();
            Status("Kamera erişimi kapalı.\nWindows Ayarlar > Gizlilik ve güvenlik > Kamera bölümünde " +
                   "'Masaüstü uygulamalarının kameranıza erişmesine izin verin' seçeneğini açın.");
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0xC00D3704))
        {
            capture.Dispose();
            Status("Kamera başka bir uygulama tarafından kullanılıyor (Teams, Kamera uygulaması vb.).\nO uygulamayı kapatıp 'Kameraları yenile'ye basın.");
        }
        catch (Exception ex)
        {
            // Kamera sürücüsünden gelen COM hataları burada toplanır; uygulama kapanmaz.
            capture.Dispose();
            Status($"Kamera açılamadı: {ex.Message}");
        }
    }

    private async Task StopAsync()
    {
        try
        {
            if (!_closed)
                Preview.Source = null;
        }
        catch (Exception)
        {
            // Pencere kapanırken görsel öğe artık yok olabilir.
        }

        var capture = _capture;
        _capture = null;
        if (capture is not null)
        {
            await Task.Yield();
            capture.Dispose();
        }

        _lastPhoto?.Dispose();
        _lastPhoto = null;
    }

    private static double FrameRate(MediaFrameFormat f) =>
        f.FrameRate.Denominator == 0 ? 0 : (double)f.FrameRate.Numerator / f.FrameRate.Denominator;

    // ───────────────────────── Odak ─────────────────────────

    private void SetupFocus(MediaCapture capture)
    {
        var focus = capture.VideoDeviceController.FocusControl;
        AutoFocusToggle.Visibility = focus.Supported ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AutoFocus_Toggled(object sender, RoutedEventArgs e)
    {
        var focus = _capture?.VideoDeviceController.FocusControl;
        if (focus is null || !focus.Supported)
            return;

        try
        {
            if (AutoFocusToggle.IsOn)
            {
                var mode = focus.SupportedFocusModes.Contains(FocusMode.Continuous) ? FocusMode.Continuous : FocusMode.Auto;
                focus.Configure(new FocusSettings { Mode = mode, AutoFocusRange = AutoFocusRange.FullRange });
                await focus.FocusAsync();
            }
            else
            {
                // Mevcut odak noktasında sabitlenir; yakın plan çekimde odak kaymasın diye.
                focus.Configure(new FocusSettings { Mode = FocusMode.Manual, Value = focus.Value });
            }
        }
        catch (Exception ex)
        {
            Status($"Odak ayarlanamadı: {ex.Message}");
        }
    }

    // ───────────────────────── Zoom (yalnızca görüntü) ─────────────────────────

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Preview.Width = e.NewSize.Width;
        Preview.Height = e.NewSize.Height;
    }

    private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingZoom || PreviewScroll is null || ZoomText is null)
            return;

        PreviewScroll.ChangeView(null, null, (float)e.NewValue);
        ZoomText.Text = $"{e.NewValue:0.0}×";
    }

    private void PreviewScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        _syncingZoom = true;
        ZoomSlider.Value = PreviewScroll.ZoomFactor;
        ZoomText.Text = $"{PreviewScroll.ZoomFactor:0.0}×";
        _syncingZoom = false;
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e) =>
        PreviewScroll.ChangeView(0, 0, 1f);

    // ───────────────────────── Çek / kaydet ─────────────────────────

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_capture is null || !CaptureButton.IsEnabled)
            return;

        CaptureButton.IsEnabled = false;
        try
        {
            var stream = new InMemoryRandomAccessStream();
            await _capture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);

            _lastPhoto?.Dispose();
            _lastPhoto = stream;

            Review.Source = bitmap;
            Review.Visibility = Visibility.Visible;
            PreviewScroll.Visibility = Visibility.Collapsed;
            CaptureButton.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            RetakeButton.Visibility = Visibility.Visible;
            SavedText.Text = $"Önizleme: {bitmap.PixelWidth}×{bitmap.PixelHeight}";
        }
        catch (Exception ex)
        {
            Status($"Fotoğraf çekilemedi: {ex.Message}");
            CaptureButton.IsEnabled = _capture is not null;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPhoto is null)
            return;

        var temp = Path.Combine(Path.GetTempPath(), $"muayene_{Guid.NewGuid():N}.jpg");
        try
        {
            _lastPhoto.Seek(0);
            using (var file = File.Create(temp))
            {
                await _lastPhoto.AsStreamForRead().CopyToAsync(file);
            }

            var name = _onSave(temp);
            SavedText.Text = $"Kaydedildi: {name}";
            ShowLive();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException
                                    or UnauthorizedAccessException or TimeoutException)
        {
            Status($"Kaydedilemedi: {ex.Message}");
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private void Retake_Click(object sender, RoutedEventArgs e)
    {
        SavedText.Text = "";
        ShowLive();
    }

    private void ShowLive()
    {
        _lastPhoto?.Dispose();
        _lastPhoto = null;
        Review.Source = null;
        Review.Visibility = Visibility.Collapsed;
        PreviewScroll.Visibility = Visibility.Visible;
        SaveButton.Visibility = Visibility.Collapsed;
        RetakeButton.Visibility = Visibility.Collapsed;
        CaptureButton.Visibility = Visibility.Visible;
        CaptureButton.IsEnabled = _capture is not null;
    }

    private void Status(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ───────────────────────── Son kullanılan kamera ─────────────────────────

    private static string? LoadSavedDeviceId()
    {
        try
        {
            if (!File.Exists(PrefsPath))
                return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(PrefsPath));
            return doc.RootElement.TryGetProperty("deviceId", out var v) ? v.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void SaveDeviceId(string deviceId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath)!);
            File.WriteAllText(PrefsPath, JsonSerializer.Serialize(new { deviceId }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tercih kaydedilemezse bir sonraki açılışta yeniden C920 aranır.
        }
    }
}
