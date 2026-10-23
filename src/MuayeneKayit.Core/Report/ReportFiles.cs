namespace MuayeneKayit.Core;

/// <summary>Oturumun reports klasöründeki rapor sürümleri.</summary>
public static class ReportFiles
{
    /// <summary>En son rapor sürümü; rapor yoksa 0.</summary>
    public static int LatestVersion(string folder)
    {
        var directory = Path.Combine(folder, "reports");
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "Rapor_v*.html")
            .Select(p => Path.GetFileNameWithoutExtension(p)["Rapor_v".Length..])
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
    }
}
