using System.Diagnostics;

namespace MuayeneKayit.Core;

/// <summary>
/// Windows ile gelen Microsoft Edge. Raporlar (PDF/HTML) varsayılan PDF uygulaması yerine Edge'de açılır:
/// raporu Edge üretir, bazı PDF okuyucular (ör. Acrobat) bu sistemde hata verebiliyor.
/// </summary>
public static class EdgeBrowser
{
    /// <summary>Program Files altındaki msedge.exe (PATH'e güvenilmez); yoksa null.</summary>
    public static string? Find()
    {
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var path = Path.Combine(Environment.GetFolderPath(folder), "Microsoft", "Edge", "Application", "msedge.exe");
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    /// <summary>Rapor dosyası (.pdf/.html) Edge'de, diğer dosyalar ya da Edge yoksa varsayılan uygulamada açılır.</summary>
    public static void OpenFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var edge = ext is ".pdf" or ".html" or ".htm" ? Find() : null;
        if (edge is null)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }
        Process.Start(new ProcessStartInfo(edge) { ArgumentList = { new Uri(Path.GetFullPath(path)).AbsoluteUri }, UseShellExecute = false });
    }
}
