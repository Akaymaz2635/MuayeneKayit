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
