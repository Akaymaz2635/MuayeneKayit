using System.Runtime.InteropServices.WindowsRuntime;
using MuayeneKayit.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MuayeneKayit.Inspector;

/// <summary>Düzenleyicinin sonucu. NewCleanJpeg doluysa yeni arka plan kopyası yazılmalıdır.</summary>
public sealed record PhotoEditResult(PhotoMarkup Markup, byte[]? NewCleanJpeg);

/// <summary>
/// Fotoğraf işaretleme ve arka plan kaldırma. Orijinal dosya değişmez: işaretler kusur kaydında
/// koordinat olarak, arka planı kaldırılmış görüntü ayrı bir kopya olarak saklanır.
/// Çizim yüzeyi görüntü piksel boyutundadır ve Viewbox ile ekrana sığdırılır.
/// </summary>
public sealed partial class PhotoEditorWindow : Window
{
    private enum CleanState { Unchanged, New, Reverted }

    private static readonly SolidColorBrush Red = new(Windows.UI.Color.FromArgb(255, 0xFF, 0x2D, 0x2D));
    private static readonly SolidColorBrush Cyan = new(Windows.UI.Color.FromArgb(255, 0x00, 0xC8, 0xFF));

    private readonly string _originalPath;
    private readonly string? _existingCleanPath;
    private readonly string? _existingCleanName;
    private readonly List<Annotation> _shapes;
    private readonly Action<PhotoEditResult> _onSave;

    private byte[]? _pixels;
    private byte[]? _cleanPixels;
    private CleanState _cleanState = CleanState.Unchanged;
    private int _w;
    private int _h;

    private bool _drawing;
    private Point _start;
    private Point _current;
    private PixelRect? _bgRect;

    public PhotoEditorWindow(
        string title,
        string defectsDir,
        string photoName,
        PhotoMarkup? existing,
        IReadOnlyList<int> defectIds,
        int currentId,
        Action<PhotoEditResult> onSave)
    {
        InitializeComponent();
        Title = "Fotoğraf işaretle";
        TargetText.Text = title;
        _onSave = onSave;
        _originalPath = System.IO.Path.Combine(defectsDir, photoName);
        _shapes = existing?.Shapes.Select(Copy).ToList() ?? new List<Annotation>();

        if (existing?.CleanedFile is string clean && PhotoMarkupValidator.IsPhotoFileName(clean))
        {
            var path = System.IO.Path.Combine(defectsDir, clean);
            if (File.Exists(path))
            {
                _existingCleanName = clean;
                _existingCleanPath = path;
            }
        }

        foreach (var id in defectIds)
            LabelBox.Items.Add(new ComboBoxItem { Content = $"Kusur #{id}", Tag = id });
        LabelBox.SelectedIndex = Math.Max(0, defectIds.ToList().IndexOf(currentId));

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            presenter.Maximize();

        Hint("Fotoğraf yükleniyor...");
        _ = LoadAsync();
    }

    // ───────────────────────── Yükleme ─────────────────────────

    private async Task LoadAsync()
    {
        try
        {
            (_pixels, _w, _h) = await DecodeAsync(_originalPath);
            Surface.Width = Ink.Width = _w;
            Surface.Height = Ink.Height = _h;

            if (_existingCleanPath is not null)
            {
                var (clean, cw, ch) = await DecodeAsync(_existingCleanPath);
                if (cw == _w && ch == _h)
                {
                    ShowPixels(clean);
                    BgRevertButton.IsEnabled = true;
                }
                else
                {
                    ShowPixels(_pixels);
                }
            }
            else
            {
                ShowPixels(_pixels);
            }

            Redraw();
            SaveButton.IsEnabled = true;
            ToolHint();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                    or System.Runtime.InteropServices.COMException)
        {
            Hint("Fotoğraf açılamadı: " + ex.Message);
        }
    }

    /// <summary>Görüntüyü EXIF yönüne göre döndürülmüş BGRA piksellerine açar.</summary>
    private static async Task<(byte[] Pixels, int Width, int Height)> DecodeAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        return (data.DetachPixelData(), (int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight);
    }

    private void ShowPixels(byte[] pixels)
    {
        var bitmap = new WriteableBitmap(_w, _h);
        using (var s = bitmap.PixelBuffer.AsStream())
            s.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        Photo.Source = bitmap;
    }

    // ───────────────────────── Araçlar ─────────────────────────

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        foreach (var t in new[] { BoxTool, CircleTool, ArrowTool, BgTool })
            t.IsChecked = ReferenceEquals(t, sender);
        ToolHint();
    }

    private AnnotationKind? CurrentKind =>
        BoxTool.IsChecked == true ? AnnotationKind.Box
        : CircleTool.IsChecked == true ? AnnotationKind.Circle
        : ArrowTool.IsChecked == true ? AnnotationKind.Arrow
        : null;

    private int CurrentLabel => (LabelBox.SelectedItem as ComboBoxItem)?.Tag is int id ? id : 1;

    private void ToolHint() => Hint(BgTool.IsChecked == true
        ? "Parçanın tamamını içine alan bir dikdörtgen çizin, sonra 'Arka planı kaldır'a basın. Beğenmezseniz 'Orijinale dön'."
        : ArrowTool.IsChecked == true
            ? "Oku kuyruktan uca doğru sürükleyin. Etiket, seçili kusur numarasıyla kuyruğa yazılır."
            : "Kusurun çevresine sürükleyerek çizin. Etikette seçili kusur numarası yazılır. Geri almak için Ctrl+Z.");

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();

    private void Undo_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Undo();
        args.Handled = true;
    }

    private void Undo()
    {
        if (_shapes.Count > 0)
            _shapes.RemoveAt(_shapes.Count - 1);
        Redraw();
    }

    private void ClearShapes_Click(object sender, RoutedEventArgs e)
    {
        _shapes.Clear();
        Redraw();
    }

    // ───────────────────────── Çizim ─────────────────────────

    private void Ink_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_pixels is null)
            return;
        _drawing = true;
        _start = _current = ClampPoint(e.GetCurrentPoint(Ink).Position);
        Ink.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Ink_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_drawing)
            return;
        _current = ClampPoint(e.GetCurrentPoint(Ink).Position);
        Redraw();
    }

    private void Ink_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_drawing)
            return;
        _drawing = false;
        Ink.ReleasePointerCapture(e.Pointer);

        var dx = _current.X - _start.X;
        var dy = _current.Y - _start.Y;
        var tooSmall = Math.Sqrt(dx * dx + dy * dy) < Math.Max(_w, _h) / 100.0;

        if (!tooSmall)
        {
            if (BgTool.IsChecked == true)
            {
                _bgRect = new PixelRect(
                    (int)Math.Min(_start.X, _current.X), (int)Math.Min(_start.Y, _current.Y),
                    (int)Math.Abs(dx), (int)Math.Abs(dy));
                BgApplyButton.IsEnabled = true;
            }
            else if (CurrentKind is AnnotationKind kind)
            {
                if (_shapes.Count >= PhotoMarkupValidator.MaxShapes)
                {
                    Hint($"Bir fotoğrafta en fazla {PhotoMarkupValidator.MaxShapes} işaret olabilir.");
                }
                else
                {
                    _shapes.Add(new Annotation
                    {
                        Kind = kind,
                        X1 = _start.X / _w, Y1 = _start.Y / _h,
                        X2 = _current.X / _w, Y2 = _current.Y / _h,
                        DefectId = CurrentLabel,
                    });
                }
            }
        }

        Redraw();
    }

    private Point ClampPoint(Point p) => new(Math.Clamp(p.X, 0, _w), Math.Clamp(p.Y, 0, _h));

    /// <summary>Tüm işaretleri yeniden çizer; rapordaki çizgi kalınlığı ve etiket boyutuyla aynıdır.</summary>
    private void Redraw()
    {
        Ink.Children.Clear();
        var stroke = Math.Max(_w, _h) / 220.0;

        foreach (var s in _shapes)
            DrawShape(s.Kind, new Point(s.X1 * _w, s.Y1 * _h), new Point(s.X2 * _w, s.Y2 * _h), s.DefectId, Red, stroke);

        if (_bgRect is PixelRect r && !(_drawing && BgTool.IsChecked == true))
            Ink.Children.Add(DashedRect(r.X, r.Y, r.Width, r.Height, stroke));

        if (_drawing)
        {
            if (BgTool.IsChecked == true)
                Ink.Children.Add(DashedRect(Math.Min(_start.X, _current.X), Math.Min(_start.Y, _current.Y),
                    Math.Abs(_current.X - _start.X), Math.Abs(_current.Y - _start.Y), stroke));
            else if (CurrentKind is AnnotationKind kind)
                DrawShape(kind, _start, _current, CurrentLabel, Red, stroke);
        }

        SaveButton.Content = _shapes.Count == 0 ? "Kaydet" : $"Kaydet ({_shapes.Count} işaret)";
    }

    private void DrawShape(AnnotationKind kind, Point a, Point b, int label, Brush brush, double stroke)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y), w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
        switch (kind)
        {
            case AnnotationKind.Box:
                Place(new Rectangle { Width = w, Height = h, Stroke = brush, StrokeThickness = stroke }, x, y);
                break;
            case AnnotationKind.Circle:
                Place(new Ellipse { Width = w, Height = h, Stroke = brush, StrokeThickness = stroke }, x, y);
                break;
            case AnnotationKind.Arrow:
                Ink.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = brush, StrokeThickness = stroke });
                Ink.Children.Add(ArrowHead(a, b, stroke * 4, brush));
                break;
        }

        var lx = kind == AnnotationKind.Arrow ? a.X : x;
        var ly = kind == AnnotationKind.Arrow ? a.Y : y;
        var font = Math.Max(_w, _h) / 28.0;
        var text = $"#{label}";
        var tag = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(font * 0.2),
            Padding = new Thickness(font * 0.25, 0, font * 0.25, 0),
            Child = new TextBlock { Text = text, FontSize = font, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) },
        };
        var lw = font * (0.62 * text.Length + 0.5);
        Place(tag, Math.Clamp(lx, 0, Math.Max(0, _w - lw)), Math.Clamp(ly - font * 1.25, 0, Math.Max(0, _h - font * 1.25)));
    }

    private static Polygon ArrowHead(Point tail, Point tip, double size, Brush brush)
    {
        var angle = Math.Atan2(tip.Y - tail.Y, tip.X - tail.X);
        var left = new Point(tip.X - size * Math.Cos(angle - 0.45), tip.Y - size * Math.Sin(angle - 0.45));
        var right = new Point(tip.X - size * Math.Cos(angle + 0.45), tip.Y - size * Math.Sin(angle + 0.45));
        return new Polygon { Points = new PointCollection { tip, left, right }, Fill = brush };
    }

    private Rectangle DashedRect(double x, double y, double w, double h, double stroke)
    {
        var rect = new Rectangle
        {
            Width = w, Height = h, Stroke = Cyan, StrokeThickness = stroke,
            StrokeDashArray = new DoubleCollection { 4, 3 },
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        return rect;
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        Ink.Children.Add(element);
    }

    // ───────────────────────── Arka plan ─────────────────────────

    private async void BgApply_Click(object sender, RoutedEventArgs e)
    {
        if (_pixels is null || _bgRect is not PixelRect rect)
            return;

        var pixels = _pixels;
        int w = _w, h = _h;
        SetBusy(true, "Arka plan ayrılıyor...");
        try
        {
            var result = await Task.Run(() => BackgroundRemover.Apply(pixels, BackgroundRemover.ComputeMask(pixels, w, h, rect)));
            _cleanPixels = result;
            _cleanState = CleanState.New;
            _bgRect = null;
            ShowPixels(result);
            BgRevertButton.IsEnabled = true;
            Hint("Arka plan kaldırıldı. Beğenmezseniz 'Orijinale dön' ya da yeniden çerçeveleyin. Orijinal dosya her zaman korunur.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Hint(ex.Message);
        }
        finally
        {
            SetBusy(false, null);
            Redraw();
        }
    }

    private void BgRevert_Click(object sender, RoutedEventArgs e)
    {
        if (_pixels is null)
            return;
        _cleanPixels = null;
        _cleanState = CleanState.Reverted;
        _bgRect = null;
        ShowPixels(_pixels);
        BgRevertButton.IsEnabled = false;
        BgApplyButton.IsEnabled = false;
        Redraw();
        Hint("Orijinal fotoğrafa dönüldü.");
    }

    // ───────────────────────── Kaydet ─────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var markup = new PhotoMarkup
        {
            ImageWidth = _w,
            ImageHeight = _h,
            Shapes = _shapes.Select(Copy).ToList(),
            CleanedFile = _cleanState == CleanState.Unchanged ? _existingCleanName : null,
        };

        SetBusy(true, "Kaydediliyor...");
        try
        {
            var jpeg = _cleanState == CleanState.New && _cleanPixels is not null ? await EncodeJpegAsync(_cleanPixels) : null;
            _onSave(new PhotoEditResult(markup, jpeg));
            Close();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or InvalidDataException or UnauthorizedAccessException or TimeoutException)
        {
            SetBusy(false, null);
            Hint("Kaydedilemedi: " + ex.Message);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async Task<byte[]> EncodeJpegAsync(byte[] pixels)
    {
        using var stream = new InMemoryRandomAccessStream();
        var options = new BitmapPropertySet { { "ImageQuality", new BitmapTypedValue(0.92, Windows.Foundation.PropertyType.Single) } };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)_w, (uint)_h, 96, 96, pixels);
        await encoder.FlushAsync();

        stream.Seek(0);
        using var memory = new MemoryStream();
        await stream.AsStreamForRead().CopyToAsync(memory);
        return memory.ToArray();
    }

    private void SetBusy(bool busy, string? message)
    {
        Busy.IsActive = busy;
        SaveButton.IsEnabled = !busy && _pixels is not null;
        if (message is not null)
            Hint(message);
    }

    private void Hint(string text) => HintText.Text = text;

    private static Annotation Copy(Annotation a) => new()
    {
        Kind = a.Kind, X1 = a.X1, Y1 = a.Y1, X2 = a.X2, Y2 = a.Y2, DefectId = a.DefectId,
    };
}
