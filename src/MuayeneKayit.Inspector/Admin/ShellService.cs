using MuayeneKayit.Core;
using System.Diagnostics;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>Klasörü Gezgin'de, raporu Edge'de, diğer dosyaları varsayılan uygulamasında açar.</summary>
public sealed class ShellService : IShell
{
    public void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });

    public void OpenFile(string path) => EdgeBrowser.OpenFile(path);
}
