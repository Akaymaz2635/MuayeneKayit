using System.Runtime.InteropServices;
using System.Text;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Son çare hata yakalayıcı: beklenmeyen bir istisna (ör. bozuk bir kayıt dosyası ya da yakalanmamış bir kod yolu)
/// uygulamayı sessizce çökertmesin. İstisna bu bilgisayardaki günlük dosyasına yazılır (%LOCALAPPDATA%\MuayeneKayit\hata.log;
/// ortak alana yazılmaz) ve kullanıcıya mesaj gösterilir. Kayıtlar atomik yazıldığı için devam etmek veriyi bozmaz;
/// kısa sürede çok hata olursa uygulama kapatılır.
/// </summary>
internal static class CrashReporter
{
    private const long MaxLogBytes = 1024 * 1024;
    private static int _handled;

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuayeneKayit", "hata.log");

    /// <summary>XAML, AppDomain ve görev (Task) düzeyindeki yakalanmamış hataları bağlar.</summary>
    public static void Install(Microsoft.UI.Xaml.Application app)
    {
        app.UnhandledException += (_, e) =>
        {
            Log("Application.UnhandledException", e.Exception);
            // Çok sayıda ardışık hata: kullanıcıyı sonsuz mesaj döngüsüne sokmadan kapat.
            if (Interlocked.Increment(ref _handled) > 5)
            {
                Show("MuayeneKayit kapanıyor", "Tekrarlayan beklenmeyen hata nedeniyle program kapatılıyor. Kayıtlar korunmuştur; programı yeniden açın.\n\nAyrıntı: " + LogPath);
                Environment.Exit(1);
                return;
            }
            e.Handled = true;
            Show("Beklenmeyen hata", Describe(e.Exception));
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log("AppDomain.UnhandledException", ex);
            Show("MuayeneKayit kapanıyor", "Beklenmeyen bir hata nedeniyle program kapanıyor. Kayıtlar korunmuştur; programı yeniden açın.\n\nAyrıntı: " + LogPath);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static string Describe(Exception ex) =>
        "Beklenmeyen bir hata oluştu; yaptığınız son işlem tamamlanmamış olabilir. Kayıtlar korunmuştur.\n\n" +
        ex.GetType().Name + ": " + ex.Message + "\n\nAyrıntı günlüğe yazıldı: " + LogPath;

    /// <summary>İstisnayı yerel günlük dosyasına ekler (1 MB'ı aşınca eski günlük silinir). Günlük yazılamazsa sessiz geçilir.</summary>
    public static void Log(string source, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                File.Delete(LogPath);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}", Encoding.UTF8);
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Başlangıç gibi pencere henüz yokken de çalışan basit mesaj kutusu.</summary>
    public static void Show(string title, string message)
    {
        try
        {
            // MB_OK | MB_ICONERROR | MB_TOPMOST
            MessageBoxW(IntPtr.Zero, message, title, 0x0 | 0x10 | 0x40000);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
