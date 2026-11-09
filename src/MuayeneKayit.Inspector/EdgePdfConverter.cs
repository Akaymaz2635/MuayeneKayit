using System.Diagnostics;
using MuayeneKayit.Core;

namespace MuayeneKayit.Inspector;

/// <summary>
/// HTML raporu, Windows ile gelen Microsoft Edge'in başsız (headless) yazdırmasıyla PDF'e çevirir.
/// Ek paket gerekmez. Edge yalnızca Program Files altındaki kurulumundan çalıştırılır (PATH'e güvenilmez),
/// kullanıcının tarayıcı profilinden ayrı geçici bir profil kullanılır, eklenti ve senkronizasyon kapalıdır.
/// </summary>
public sealed class EdgePdfConverter : IPdfConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Başarılıysa null, değilse kısa hata açıklaması döner.</summary>
    public async Task<string?> TryConvertAsync(string htmlPath, string pdfPath)
    {
        var edge = EdgeBrowser.Find();
        if (edge is null)
            return "Microsoft Edge bulunamadı";

        var profile = Path.Combine(Path.GetTempPath(), "MuayeneKayit-pdf-" + Guid.NewGuid().ToString("N"));
        var temp = pdfPath + ".tmp";
        try
        {
            var start = new ProcessStartInfo(edge)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
                     {
                         "--headless", "--disable-gpu", "--no-first-run", "--disable-extensions", "--disable-sync",
                         "--disable-background-networking", "--disable-component-update",
                         "--no-pdf-header-footer",
                         // Dosya erişim bayrağı kaldırıldı: <img> aynı klasör ağacındaki yerel dosyayı zaten yükler (Edge 157'de
                         // doğrulandı). Betikler raporun CSP'siyle (default-src 'none') engellenir; DNS çözümleme kapatılır,
                         // böylece sayfa dışarıya istek yapamaz. (--blink-settings=scriptEnabled=false Edge'de PDF üretimini bozuyor.)
                         "--host-resolver-rules=MAP * ~NOTFOUND",
                         "--user-data-dir=" + profile,
                         "--print-to-pdf=" + temp,
                         new Uri(htmlPath).AbsoluteUri,
                     })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start);
            if (process is null)
                return "Edge başlatılamadı";

            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                return "PDF dönüştürme zaman aşımına uğradı";
            }

            if (!File.Exists(temp) || new FileInfo(temp).Length < 100)
                return "Edge PDF üretmedi";

            File.Move(temp, pdfPath, overwrite: false);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return ex.Message;
        }
        finally
        {
            TryDelete(() => File.Delete(temp));
            TryDelete(() => Directory.Delete(profile, recursive: true));
        }
    }

    private static void TryDelete(Action delete)
    {
        try { delete(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
