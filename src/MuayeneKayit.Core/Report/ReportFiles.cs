namespace MuayeneKayit.Core;

/// <summary>Oturumun reports klasöründeki rapor sürümleri.</summary>
public static class ReportFiles
{
    /// <summary>
    /// En son rapor sürümü; rapor yoksa 0. PDF ve HTML birlikte sayılır: PDF üretilince HTML silinir,
    /// PDF üretilemezse yalnızca HTML kalır.
    /// </summary>
    public static int LatestVersion(string folder)
    {
        var directory = Path.Combine(folder, "reports");
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "Rapor_v*.*")
            .Where(p => Path.GetExtension(p).ToLowerInvariant() is ".pdf" or ".html")
            .Select(p => Path.GetFileNameWithoutExtension(p)["Rapor_v".Length..])
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// Yeni rapor sürümünü ayırır: reports klasörü kilidi altında sıradaki numara hesaplanır ve boş bir "Rapor_v{N}.html"
    /// yer tutucusu FileMode.CreateNew ile oluşturulur; eşzamanlı raporlar farklı sürüm alır. Oturum klasörü yoksa
    /// (ağ kaybı) hiçbir şey oluşturulmaz. Yer tutucu raporun yedek HTML'i ile değiştirilir ya da <see cref="Release"/> ile silinir.
    /// </summary>
    public static int Reserve(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Oturum klasörü bulunamadı: {folder}. Ortak alana erişimi kontrol edin.");

        var directory = Path.Combine(folder, "reports");
        Directory.CreateDirectory(directory);
        using var _ = FileLock.Acquire(Path.Combine(directory, "reports.lock"));

        for (var version = LatestVersion(folder) + 1; ; version++)
        {
            try
            {
                using var stream = new FileStream(Path.Combine(directory, $"Rapor_v{version}.html"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return version;
            }
            catch (IOException) when (File.Exists(Path.Combine(directory, $"Rapor_v{version}.html")))
            {
                // Sürüm başkasında: sıradaki denenir.
            }
        }
    }

    /// <summary>Ayrılan sürümün boş yer tutucusunu siler (rapor üretilemediyse); içi dolu HTML'e dokunmaz.</summary>
    public static void Release(string folder, int version)
    {
        var path = Path.Combine(folder, "reports", $"Rapor_v{version}.html");
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length == 0 && !File.Exists(Path.ChangeExtension(path, ".pdf")))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Aynı sürümün PDF'i olan HTML ara dosyalarını siler (PDF'i olmayan HTML yedek olarak kalır).
    /// Silinen dosya sayısını döner.
    /// </summary>
    public static int CleanupHtml(string folder)
    {
        var directory = Path.Combine(folder, "reports");
        if (!Directory.Exists(directory))
            return 0;

        var removed = 0;
        foreach (var html in Directory.EnumerateFiles(directory, "Rapor_v*.html").ToList())
        {
            if (File.Exists(Path.ChangeExtension(html, ".pdf")) && DeleteHtml(html))
                removed++;
        }
        return removed;
    }

    /// <summary>HTML ara dosyasını siler; açık ya da kilitliyse bırakır (rapor yine geçerlidir).</summary>
    public static bool DeleteHtml(string htmlPath)
    {
        try
        {
            File.Delete(htmlPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
