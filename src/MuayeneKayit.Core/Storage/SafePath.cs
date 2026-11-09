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

    // Windows'ta dosya adı olarak açılamayan aygıt adları (CON, NUL.txt gibi uzantılı olanlar dahil).
    private static readonly Regex ReservedName = new(
        @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string ValidateSegment(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} boş olamaz.");

        var trimmed = value.Trim();
        if (!SegmentPattern.IsMatch(trimmed) || trimmed.Contains("..") || trimmed.EndsWith('.') || trimmed.EndsWith(' '))
            throw new ArgumentException($"{field} geçersiz karakter içeriyor: '{value}'.");

        if (IsReservedDeviceName(trimmed))
            throw new ArgumentException($"{field} Windows'ta ayrılmış bir ad olamaz: '{value}'.");

        return trimmed;
    }

    /// <summary>"CON", "nul.txt", "COM1 .x" gibi ayrılmış aygıt adı mı (uzantı ve boşluk yok sayılır).</summary>
    public static bool IsReservedDeviceName(string name)
    {
        var dot = name.IndexOf('.');
        var stem = (dot >= 0 ? name[..dot] : name).TrimEnd(' ');
        return ReservedName.IsMatch(stem);
    }

    /// <summary>Yol bir yeniden çözümleme noktası (junction / sembolik bağ) mı.</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Yol kök klasörün altında mı ve kök ile yol arasındaki hiçbir öğe (yolun kendisi dahil) yeniden çözümleme noktası değil mi.
    /// Ayraçlı karşılaştırma yapılır: "Veri2", "Veri" kökünün altında sayılmaz.
    /// </summary>
    public static bool IsUnderRootWithoutLinks(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        for (var current = full; current.Length > fullRoot.Length; current = Path.GetDirectoryName(current) ?? fullRoot)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && IsReparsePoint(current))
                return false;
        }
        return true;
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
