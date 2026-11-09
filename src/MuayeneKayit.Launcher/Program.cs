using System.Diagnostics;
using System.Runtime.InteropServices;
using MuayeneKayit.Core;

namespace MuayeneKayit.Launcher;

/// <summary>
/// Ağ klasöründeki başlatıcı (MuayeneKayit.exe). Kurulum yapmaz, hiçbir şey kopyalamaz: surum.txt'deki güncel sürümün
/// exe'sini bilgisayarın mimarisine göre (x64 / ARM64) ağ klasöründen açar ve kapanır.
/// Admin paneli muayene programının içindedir; ayrı admin başlatıcısı yoktur.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var self = Environment.ProcessPath ?? "";
            // Admin paneli muayene programının içindedir (sol menü > Admin). Eski kurulumlardan kalan
            // "MuayeneKayit Admin.exe" kopyası da muayene programını açar.
            var exe = ProgramLayout.Resolve(AppContext.BaseDirectory, "Inspector", RuntimeInformation.OSArchitecture == Architecture.Arm64);

            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            };
            foreach (var arg in args)
                start.ArgumentList.Add(arg);
            Process.Start(start);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, ex.Message, "Muayene Kayıt", 0x10 /* MB_ICONERROR */);
            return 1;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
