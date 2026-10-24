namespace MuayeneKayit.Core;

public enum AnnotationKind
{
    Box,
    Circle,
    Arrow,
}

/// <summary>
/// Fotoğraf üzerindeki tek işaret. Koordinatlar görüntü boyutuna göre 0..1 aralığındadır,
/// böylece aynı işaret orijinal ve arka planı kaldırılmış sürümde aynı yere düşer.
/// Ok için (X1,Y1) kuyruk, (X2,Y2) uçtur.
/// </summary>
public sealed class Annotation
{
    public AnnotationKind Kind { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    /// <summary>İşaretin gösterdiği kusur numarası. Etikette "#N" olarak yazılır.</summary>
    public int DefectId { get; set; }

    /// <summary>Kamera çerçevesinin varsayılan kenarı: görüntünün kısa kenarına oranı.</summary>
    public const double CameraFrameFraction = 0.2;

    /// <summary>
    /// Kameradaki ortalanmış kare çerçeve. Kenar = kısa kenar × oran; koordinatlar 0..1.
    /// Kamera önizlemesi ve kaydedilen işaret aynı kuralı kullanır.
    /// </summary>
    public static Annotation CenterSquare(int width, int height, int defectId, double fraction = CameraFrameFraction)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Görüntü boyutu geçersiz.");
        var side = Math.Min(width, height) * Math.Clamp(fraction, 0.05, 1.0);
        var hw = side / 2 / width;
        var hh = side / 2 / height;
        return new Annotation
        {
            Kind = AnnotationKind.Box,
            X1 = 0.5 - hw, Y1 = 0.5 - hh,
            X2 = 0.5 + hw, Y2 = 0.5 + hh,
            DefectId = defectId,
        };
    }
}

/// <summary>
/// Bir fotoğrafın işaretleri ve arka plan kaldırma sonucu. Orijinal dosya hiçbir zaman değişmez.
/// </summary>
public sealed class PhotoMarkup
{
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<Annotation> Shapes { get; set; } = new();

    /// <summary>Arka planı kaldırılmış kopyanın dosya adı. Null ise orijinal kullanılır (geri al).</summary>
    public string? CleanedFile { get; set; }
}

public static class PhotoMarkupValidator
{
    public const int MaxShapes = 30;

    public static IReadOnlyList<string> Validate(PhotoMarkup markup)
    {
        var errors = new List<string>();
        if (markup.ImageWidth <= 0 || markup.ImageHeight <= 0)
            errors.Add("Görüntü boyutu geçersiz.");
        if (markup.Shapes.Count > MaxShapes)
            errors.Add($"Bir fotoğrafta en fazla {MaxShapes} işaret olabilir.");

        foreach (var s in markup.Shapes)
        {
            if (!Unit(s.X1) || !Unit(s.Y1) || !Unit(s.X2) || !Unit(s.Y2))
            {
                errors.Add("İşaret koordinatı görüntü dışında.");
                break;
            }
            if (s.DefectId <= 0)
            {
                errors.Add("İşaretin kusur numarası geçersiz.");
                break;
            }
        }

        if (markup.CleanedFile is not null && !IsPhotoFileName(markup.CleanedFile))
            errors.Add("Arka plan dosya adı geçersiz.");

        return errors;
    }

    /// <summary>Yalnızca kusur klasöründe üretilen adlar: "{id}_photo_NN.ext" ya da "..._clean.jpg".</summary>
    public static bool IsPhotoFileName(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d{1,5}_photo_\d{2,3}(_clean)?\.(jpg|jpeg|png)$");

    private static bool Unit(double v) => !double.IsNaN(v) && v >= 0 && v <= 1;
}
