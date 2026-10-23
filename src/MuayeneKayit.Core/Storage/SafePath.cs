using System.Text.RegularExpressions;

namespace MuayeneKayit.Core;

/// <summary>
/// Parça, operasyon ve seri numaralarını klasör adı olarak kullanmadan önce doğrular.
/// Yol dolaşımını (..\ ve benzeri) ve kök dizin dışına çıkmayı engeller.
/// </summary>
public static class SafePath
{
    // Harf/rakam/alt çizgi/tire ile başlar. Nokta ve boşluk ortada olabilir, sonda olamaz.
    private static readonly Regex SegmentPattern = new(
        @"^[\p{L}\p{N}_\-][\p{L}\p{N}_. \-]{0,63}$",
        RegexOptions.CultureInvariant);

    public static string ValidateSegment(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} boş olamaz.");

        var trimmed = value.Trim();
        if (!SegmentPattern.IsMatch(trimmed) || trimmed.Contains("..") || trimmed.EndsWith('.'))
            throw new ArgumentException($"{field} geçersiz karakter içeriyor: '{value}'.");

        return trimmed;
    }

    /// <summary>Kök\Parça\Operasyon\Seri yolunu üretir. Kök dışına çıkarsa hata verir.</summary>
    public static string SessionFolder(string root, string part, string operation, string serial)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folder = Path.GetFullPath(Path.Combine(
            fullRoot,
            ValidateSegment(part, "Parça no"),
            ValidateSegment(operation, "Operasyon no"),
            ValidateSegment(serial, "Seri no")));

        if (!folder.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yol kök dizin dışına çıkıyor.");

        return folder;
    }
}
