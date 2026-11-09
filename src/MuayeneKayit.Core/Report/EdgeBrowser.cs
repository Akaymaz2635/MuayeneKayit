using System.Diagnostics;

namespace MuayeneKayit.Core;

/// <summary>
/// Windows ile gelen Microsoft Edge. Raporlar (PDF/HTML) varsayılan PDF uygulaması yerine Edge'de açılır:
/// raporu Edge üretir, bazı PDF okuyucular (ör. Acrobat) bu sistemde hata verebiliyor.
/// </summary>
public static class EdgeBrowser
{
    /// <summary>Açılabilen dosya türleri: rapor (.pdf, .html) ve fotoğraf. Başka türler hiçbir zaman açılmaz.</summary>
    private static readonly string[] OpenableExtensions = { ".pdf", ".html", ".htm", ".jpg", ".jpeg", ".png" };

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

    /// <summary>İzin verilen (rapor ya da fotoğraf) bir dosya türü mü.</summary>
    public static bool IsOpenable(string path) =>
        OpenableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Rapor / fotoğraf dosyasını Edge'de açar. Yalnızca <see cref="IsOpenable"/> türleri açılır; başka tür (exe, bat, lnk,
    /// txt ...) hata verir ve kabuğa hiç verilmez. Edge yoksa yalnızca bu güvenli türler varsayılan uygulamayla açılır.
    /// </summary>
    public static void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Dosya yolu boş.");

        var full = Path.GetFullPath(path);
        if (!IsOpenable(full))
            throw new InvalidOperationException($"Bu dosya türü açılamaz ({Path.GetExtension(full)}); yalnızca rapor (PDF, HTML) ve fotoğraf dosyaları açılır.");
        if (!File.Exists(full))
            throw new FileNotFoundException("Dosya bulunamadı.", full);

        try
        {
            var edge = Find();
            if (edge is null)
                Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(edge) { ArgumentList = { new Uri(full).AbsoluteUri }, UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"Dosya açılamadı: {ex.Message}", ex);
        }
    }
}
