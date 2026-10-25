using System.Reflection;

namespace MuayeneKayit.Core;

/// <summary>Çalışan programın sürümü (Directory.Build.props &gt; Version). Pencere başlığında gösterilir.</summary>
public static class AppInfo
{
    public static string Version
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version is null ? "" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
