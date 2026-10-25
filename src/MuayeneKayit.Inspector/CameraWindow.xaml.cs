using System.Text.Json;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
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
/// Dokunmatik ekran için telefon kamerası gibi çalışır: iki parmakla zoom, çift dokunma 1× / 2×, dokunarak odak.
/// Zoom dijitaldir; fotoğraf ekranda görülen alan kadar (ortadan) kırpılarak kaydedilir.
/// </summary>
public sealed partial class CameraWindow : Window
{
    private const string PreferredCameraName = "C920";

    /// <summary>Çekilen fotoğrafı kusura kaydeder ve dosya adını döndürür. Sonra pencere kapanır.</summary>
    private readonly Func<CapturedPhotoData, string> _onSave;
    private readonly int _defectId;

    /// <summary>Çerçeve tercihi ve boyutu oturum boyunca hatırlanır.</summary>
    private static bool _frameOn = true;
    private static double _frameFraction = Annotation.CameraFrameFraction;

    private int _videoWidth;
    private int _videoHeight;
    private int _photoWidth;
    private int _photoHeight;

    private MediaCapture? _capture;
    private InMemoryRandomAccessStream? _lastPhoto;
    private bool _closed;

    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MuayeneKayit",
        "camera.json");

    public CameraWindow(string target, int defectId, Func<CapturedPhotoData, string> onSave)
    {
        InitializeComponent();
        _onSave = onSave;
        _defectId = defectId;
        FrameToggle.IsOn = _frameOn;
        FrameSizeSlider.Value = _frameFraction;
        FrameSizeSlider.IsEnabled = _frameOn;
        FrameSizeText.Text = $"%{_frameFraction * 100:0}";
        SetZoom(1);

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
            _videoWidth = (int)format.VideoFormat.Width;
            _videoHeight = (int)format.VideoFormat.Height;
            UpdatePreviewClip();
            DrawFrame(FrameOverlay, PreviewArea.ActualWidth, PreviewArea.ActualHeight, _videoWidth, _videoHeight);
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

    // ───────────────────────── Zoom (telefon kamerası gibi) ─────────────────────────

    /// <summary>
    /// Dijital zoom: görüntü her zaman ortaya doğru büyür, kaydırma yoktur. Çekilen fotoğraf aynı oranda ortadan
    /// kırpılır; ekranda görülen (genişlik yönünde) çekilir. Kusur çerçevesi ekranda sabittir ve kırpılmış fotoğrafa denk gelir.
    /// </summary>
    private const double MaxZoom = 5;

    private double _zoom = 1;

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 1, MaxZoom);
        PreviewScale.ScaleX = PreviewScale.ScaleY = _zoom;
        ZoomText.Text = $"{_zoom:0.0}×";

        // Seçili hızlı düğme vurgulu (telefondaki gibi).
        foreach (var button in new[] { Zoom1Button, Zoom2Button, Zoom4Button })
        {
            var preset = double.Parse((string)button.Tag, System.Globalization.CultureInfo.InvariantCulture);
            button.Style = (Style)Application.Current.Resources[Math.Abs(_zoom - preset) < 0.05 ? "AccentButtonStyle" : "DefaultButtonStyle"];
        }
    }

    private void ZoomPreset_Click(object sender, RoutedEventArgs e) =>
        SetZoom(double.Parse((string)((Button)sender).Tag, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>İki parmakla açıp kapatma (pinch).</summary>
    private void PreviewArea_ManipulationDelta(object sender, Microsoft.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
    {
        if (e.Delta.Scale > 0)
            SetZoom(_zoom * e.Delta.Scale);
        e.Handled = true;
    }

    /// <summary>Çift dokunma: 1× ↔ 2×.</summary>
    private void PreviewArea_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        SetZoom(_zoom < 1.5 ? 2 : 1);
        e.Handled = true;
    }

    /// <summary>Fare tekerleği: her çentik yaklaşık %10.</summary>
    private void PreviewArea_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(PreviewArea).Properties.MouseWheelDelta;
        SetZoom(_zoom * Math.Pow(1.1, delta / 120.0));
        e.Handled = true;
    }

    private void PreviewArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePreviewClip();
        DrawFrame(FrameOverlay, e.NewSize.Width, e.NewSize.Height, _videoWidth, _videoHeight);
    }

    /// <summary>
    /// Büyütülen görüntü, görüntünün 1×'teki dikdörtgeninin dışına taşmaz (yan boşluklara yayılmaz).
    /// Böylece ekranda görülen, ortadan kırpılarak çekilen fotoğrafla birebir aynıdır.
    /// </summary>
    private void UpdatePreviewClip()
    {
        double areaW = PreviewArea.ActualWidth, areaH = PreviewArea.ActualHeight;
        if (areaW <= 0 || areaH <= 0)
            return;

        var rect = new Windows.Foundation.Rect(0, 0, areaW, areaH);
        if (_videoWidth > 0 && _videoHeight > 0)
        {
            var scale = Math.Min(areaW / _videoWidth, areaH / _videoHeight);
            double shownW = _videoWidth * scale, shownH = _videoHeight * scale;
            rect = new Windows.Foundation.Rect((areaW - shownW) / 2, (areaH - shownH) / 2, shownW, shownH);
        }
        PreviewArea.Clip = new RectangleGeometry { Rect = rect };
    }

    /// <summary>
    /// Dokunarak odak: dokunulan noktaya odak halkası çizilir; kamera destekliyorsa o bölgeye odaklanır.
    /// "Odak sabit" seçiliyse odak değiştirilmez.
    /// </summary>
    private async void PreviewArea_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var capture = _capture;
        var focus = capture?.VideoDeviceController.FocusControl;
        if (capture is null || focus is null || !focus.Supported || !AutoFocusToggle.IsOn)
            return;

        var point = e.GetPosition(PreviewArea);
        ShowFocusRing(point);
        try
        {
            var roi = capture.VideoDeviceController.RegionsOfInterestControl;
            if (roi.AutoFocusSupported && roi.MaxRegions > 0 && TryMapToFrame(point, out var nx, out var ny))
            {
                const double size = 0.12;
                var region = new RegionOfInterest
                {
                    AutoFocusEnabled = true,
                    BoundsNormalized = true,
                    Bounds = new Windows.Foundation.Rect(Math.Clamp(nx - size / 2, 0, 1 - size), Math.Clamp(ny - size / 2, 0, 1 - size), size, size),
                    Type = RegionOfInterestType.Unknown,
                    Weight = 100,
                };
                await roi.SetRegionsAsync(new[] { region }, true);
            }
            await focus.FocusAsync();
        }
        catch (Exception)
        {
            // Bazı kameralar bölgeye odaklamayı desteklemez; görüntü etkilenmez.
        }
    }

    /// <summary>Ekrandaki noktayı (zoom dahil) kare içindeki 0..1 konuma çevirir; görüntü dışındaysa false.</summary>
    private bool TryMapToFrame(Windows.Foundation.Point point, out double nx, out double ny)
    {
        nx = ny = 0;
        double areaW = PreviewArea.ActualWidth, areaH = PreviewArea.ActualHeight;
        if (_videoWidth <= 0 || _videoHeight <= 0 || areaW <= 0 || areaH <= 0)
            return false;

        var scale = Math.Min(areaW / _videoWidth, areaH / _videoHeight);
        double shownW = _videoWidth * scale, shownH = _videoHeight * scale;
        var x = areaW / 2 + (point.X - areaW / 2) / _zoom;
        var y = areaH / 2 + (point.Y - areaH / 2) / _zoom;
        nx = (x - (areaW - shownW) / 2) / shownW;
        ny = (y - (areaH - shownH) / 2) / shownH;
        return nx is >= 0 and <= 1 && ny is >= 0 and <= 1;
    }

    private void ShowFocusRing(Windows.Foundation.Point point)
    {
        FocusOverlay.Children.Clear();
        const double size = 72;
        var ring = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Gold),
            StrokeThickness = 2,
        };
        Canvas.SetLeft(ring, point.X - size / 2);
        Canvas.SetTop(ring, point.Y - size / 2);
        FocusOverlay.Children.Add(ring);

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1.2);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => FocusOverlay.Children.Remove(ring);
        timer.Start();
    }

    /// <summary>Fotoğrafı zoom oranında ortadan kırpar (ekranda görülen alan); yeni JPEG akışı döner.</summary>
    private static async Task<InMemoryRandomAccessStream> CropCenterAsync(InMemoryRandomAccessStream source, double zoom)
    {
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(source);
        uint width = decoder.OrientedPixelWidth, height = decoder.OrientedPixelHeight;
        var cropW = Math.Max(1u, (uint)Math.Round(width / zoom));
        var cropH = Math.Max(1u, (uint)Math.Round(height / zoom));

        var transform = new Windows.Graphics.Imaging.BitmapTransform
        {
            Bounds = new Windows.Graphics.Imaging.BitmapBounds { X = (width - cropW) / 2, Y = (height - cropH) / 2, Width = cropW, Height = cropH },
        };
        var pixels = await decoder.GetPixelDataAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore, transform,
            Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation, Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

        var output = new InMemoryRandomAccessStream();
        var options = new Windows.Graphics.Imaging.BitmapPropertySet
        {
            { "ImageQuality", new Windows.Graphics.Imaging.BitmapTypedValue(0.95, Windows.Foundation.PropertyType.Single) },
        };
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, output, options);
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
            cropW, cropH, decoder.DpiX, decoder.DpiY, pixels.DetachPixelData());
        await encoder.FlushAsync();

        source.Dispose();
        output.Seek(0);
        return output;
    }

    private void FrameSize_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Pencere yüklenirken görüntü alanı henüz yoktur.
        if (FrameSizeText is null || PreviewHost is null || ReviewHost is null || FrameOverlay is null || ReviewOverlay is null)
            return;
        _frameFraction = e.NewValue;
        FrameSizeText.Text = $"%{e.NewValue * 100:0}";
        DrawFrame(FrameOverlay, PreviewArea.ActualWidth, PreviewArea.ActualHeight, _videoWidth, _videoHeight);
        DrawFrame(ReviewOverlay, ReviewHost.ActualWidth, ReviewHost.ActualHeight, _photoWidth, _photoHeight);
    }

    private void ReviewHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DrawFrame(ReviewOverlay, e.NewSize.Width, e.NewSize.Height, _photoWidth, _photoHeight);

    private void FrameToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // Pencere yüklenirken (XAML okunurken) görüntü alanı henüz yoktur.
        if (PreviewHost is null || ReviewHost is null || FrameOverlay is null || ReviewOverlay is null)
            return;
        _frameOn = FrameToggle.IsOn;
        FrameSizeSlider.IsEnabled = FrameToggle.IsOn;
        DrawFrame(FrameOverlay, PreviewArea.ActualWidth, PreviewArea.ActualHeight, _videoWidth, _videoHeight);
        DrawFrame(ReviewOverlay, ReviewHost.ActualWidth, ReviewHost.ActualHeight, _photoWidth, _photoHeight);
    }

    /// <summary>
    /// Ortalanmış kare çerçeveyi, düzenleyicideki kutuyla aynı görünümde (kırmızı çizgi, "#N" etiketi) çizer.
    /// Görüntü alana "Uniform" sığdırıldığı için önce görüntünün ekrandaki dikdörtgeni bulunur.
    /// </summary>
    private void DrawFrame(Canvas canvas, double hostWidth, double hostHeight, int imageWidth, int imageHeight)
    {
        canvas.Children.Clear();
        if (FrameToggle is null || !FrameToggle.IsOn || imageWidth <= 0 || imageHeight <= 0 || double.IsNaN(hostWidth) || hostWidth <= 0 || hostHeight <= 0)
            return;

        var scale = Math.Min(hostWidth / imageWidth, hostHeight / imageHeight);
        double shownW = imageWidth * scale, shownH = imageHeight * scale;
        double offsetX = (hostWidth - shownW) / 2, offsetY = (hostHeight - shownH) / 2;

        var box = Annotation.CenterSquare(imageWidth, imageHeight, _defectId, _frameFraction);
        double x = offsetX + box.X1 * shownW, y = offsetY + box.Y1 * shownH;
        double w = (box.X2 - box.X1) * shownW, h = (box.Y2 - box.Y1) * shownH;

        var red = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0x2D, 0x2D));
        var stroke = Math.Max(2, Math.Max(shownW, shownH) / 220);
        var font = Math.Max(14, Math.Max(shownW, shownH) / 28);

        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle { Width = w, Height = h, Stroke = red, StrokeThickness = stroke };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);

        var tag = new Border
        {
            Background = red,
            CornerRadius = new CornerRadius(font * 0.2),
            Padding = new Thickness(font * 0.25, 0, font * 0.25, 0),
            Child = new TextBlock
            {
                Text = $"#{_defectId}",
                FontSize = font,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            },
        };
        Canvas.SetLeft(tag, x);
        Canvas.SetTop(tag, Math.Max(0, y - font * 1.25));
        canvas.Children.Add(tag);
    }

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

            // Telefon kamerası gibi: zoom yapıldıysa ekranda görülen alan (ortadan) çekilir.
            if (_zoom > 1.01)
                stream = await CropCenterAsync(stream, _zoom);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);

            _lastPhoto?.Dispose();
            _lastPhoto = stream;

            Review.Source = bitmap;
            _photoWidth = bitmap.PixelWidth;
            _photoHeight = bitmap.PixelHeight;
            ReviewHost.Visibility = Visibility.Visible;
            DrawFrame(ReviewOverlay, ReviewHost.ActualWidth, ReviewHost.ActualHeight, _photoWidth, _photoHeight);
            PreviewArea.Visibility = Visibility.Collapsed;
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

            // Kaydedince pencere kapanır; fotoğraf doğrudan işaretleme ekranında açılır.
            _onSave(new CapturedPhotoData(temp, _photoWidth, _photoHeight, FrameToggle.IsOn, _frameFraction));
            Close();
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
        ReviewHost.Visibility = Visibility.Collapsed;
        PreviewArea.Visibility = Visibility.Visible;
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
