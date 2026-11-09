namespace MuayeneKayit.Core;

/// <summary>
/// Dışarıdan seçilen fotoğrafı oturum klasörüne kopyalar. Uzantı, içerik başlığı (magic bytes)
/// ve boyut sınırı kontrol edilir. Kaynak dosya değiştirilmez.
/// </summary>
public static class PhotoImporter
{
    public const long MaxBytes = 25L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

    /// <summary>
    /// Fotoğrafı kopyalar ve kusur klasöründeki dosya adını döndürür. Sıradaki numara kusur kilidi altında, diskteki
    /// mevcut dosyaların en büyük numarasından (ve <paramref name="reservedNames"/> içindeki adlardan) sonra seçilir;
    /// dosya FileMode.CreateNew ile yazılır, ad yine de doluysa sıradaki numara denenir: boşluklu ya da paralel eklemede
    /// var olan fotoğrafın üzerine yazılmaz ve çakışma hatası çıkmaz.
    /// </summary>
    public static string Import(string folder, int defectId, string sourcePath, IEnumerable<string>? reservedNames = null)
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

        // Çok büyük görüntü (piksel bombası) kodu çözülmeden reddedilir; düzenleyici tam çözünürlükte belleğe açar.
        if (ImageHeader.TryReadSize(sourcePath) is var (w, h) && ImageHeader.ExceedsLimit(w, h))
            throw new InvalidDataException(
                $"Fotoğraf çok büyük ({w}x{h}); en fazla {ImageHeader.MaxPixels / 1_000_000} megapiksel kabul edilir.");

        var directory = Path.Combine(folder, "defects");

        // Konum / cihaz üst verisi (EXIF, XMP) paylaşıma gitmez; görüntü verisi değişmez. Biçim tanınmazsa dosya olduğu gibi kopyalanır.
        var bytes = File.ReadAllBytes(sourcePath);
        var clean = MetadataStripper.Strip(bytes, extension);
        var payload = clean ?? bytes;

        // Numara seçimi ve dosya oluşturma kusur (oturum) kilidi altında; oturum klasörü yoksa yeniden oluşturulmaz.
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));
        Directory.CreateDirectory(directory);

        var reserved = new HashSet<string>(reservedNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var pattern = new System.Text.RegularExpressions.Regex(
            "^" + defectId + @"_photo_(\d{1,5})\.(jpg|jpeg|png)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var next = 1;
        foreach (var path in Directory.EnumerateFiles(directory, $"{defectId}_photo_*"))
            next = Math.Max(next, NumberOf(pattern, Path.GetFileName(path)) + 1);
        foreach (var name in reserved)
            next = Math.Max(next, NumberOf(pattern, name) + 1);

        for (var attempt = 0; attempt < 200; attempt++, next++)
        {
            var fileName = $"{defectId}_photo_{next:00}{extension}";
            if (reserved.Contains(fileName))
                continue;

            try
            {
                using var stream = new FileStream(Path.Combine(directory, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(payload);
                return fileName;
            }
            catch (IOException) when (File.Exists(Path.Combine(directory, fileName)))
            {
                // Ad başkası tarafından alındı: sıradaki numara denenir.
            }
        }
        throw new IOException("Fotoğraf için boş bir ad bulunamadı.");
    }

    private static int NumberOf(System.Text.RegularExpressions.Regex pattern, string fileName) =>
        pattern.Match(fileName) is { Success: true } m ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

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
