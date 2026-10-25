using System.Diagnostics;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>Klasörü Gezgin'de, dosyayı varsayılan uygulamasında açar.</summary>
public sealed class ShellService : IShell
{
    public void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });

    public void OpenFile(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
