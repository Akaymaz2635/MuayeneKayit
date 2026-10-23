namespace MuayeneKayit.Core;

/// <summary>
/// Dışarıdan seçilen fotoğrafı oturum klasörüne kopyalar. Uzantı, içerik başlığı (magic bytes)
/// ve boyut sınırı kontrol edilir. Kaynak dosya değiştirilmez.
/// </summary>
public static class PhotoImporter
{
    public const long MaxBytes = 25L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

    /// <summary>Fotoğrafı kopyalar ve kusur klasöründeki dosya adını döndürür.</summary>
    public static string Import(string folder, int defectId, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Fotoğraf bulunamadı.", sourcePath);

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidDataException("Yalnızca JPG ve PNG dosyaları eklenebilir.");

        if (new FileInfo(sourcePath).Length > MaxBytes)
            throw new InvalidDataException("Fotoğraf 25 MB sınırını aşıyor.");

        if (!HasImageSignature(sourcePath, extension))
            throw new InvalidDataException("Dosya içeriği uzantısıyla eşleşmiyor, görüntü olarak kabul edilmedi.");

        var directory = Path.Combine(folder, "defects");
        Directory.CreateDirectory(directory);

        var existing = Directory.EnumerateFiles(directory, $"{defectId}_photo_*")
            .Count(p => !p.Contains("_clean", StringComparison.Ordinal) && !p.EndsWith(".tmp", StringComparison.Ordinal));
        var fileName = $"{defectId}_photo_{existing + 1:00}{extension}";

        File.Copy(sourcePath, Path.Combine(directory, fileName), overwrite: false);
        return fileName;
    }

    /// <summary>
    /// Arka planı kaldırılmış kopyayı "{orijinal}_clean.jpg" olarak yazar. Orijinal dosyaya dokunmaz;
    /// aynı fotoğraf yeniden işlenirse yalnızca kopya yenilenir.
    /// </summary>
    public static string WriteCleaned(string folder, string originalName, byte[] jpeg)
    {
        if (!PhotoMarkupValidator.IsPhotoFileName(originalName) || originalName.Contains("_clean"))
            throw new ArgumentException("Geçersiz fotoğraf adı.");
        if (jpeg.Length > MaxBytes)
            throw new InvalidDataException("Fotoğraf 25 MB sınırını aşıyor.");
        if (jpeg.Length < 3 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[2] != 0xFF)
            throw new InvalidDataException("Arka plan sonucu JPEG değil.");

        var name = Path.GetFileNameWithoutExtension(originalName) + "_clean.jpg";
        var path = Path.Combine(folder, "defects", name);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, jpeg);
        File.Move(temp, path, overwrite: true);
        return name;
    }

    private static bool HasImageSignature(string path, string extension)
    {
        Span<byte> head = stackalloc byte[8];
        using var stream = File.OpenRead(path);
        var read = stream.Read(head);

        if (extension is ".jpg" or ".jpeg")
            return read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        return read >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
    }
}
