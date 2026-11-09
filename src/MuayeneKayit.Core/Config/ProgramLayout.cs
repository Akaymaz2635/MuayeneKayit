namespace MuayeneKayit.Core;

/// <summary>
/// Ağ klasöründen çalıştırma düzeni. Program klasörünün kökünde başlatıcı (MuayeneKayit.exe) ve surum.txt durur;
/// her sürüm kendi klasöründedir:
/// <code>
/// Program\MuayeneKayit.exe, surum.txt ("1.0.1"), MuayeneKayit.settings.json
/// Program\1.0.1\Inspector-x64\MuayeneKayit.Inspector.exe   (Inspector-arm64; admin paneli bu programın içindedir)
/// </code>
/// Güncelleme: yeni sürüm klasörü kopyalanır, surum.txt değiştirilir. Açık programlar eski klasörden çalışmaya devam eder.
/// </summary>
public static class ProgramLayout
{
    public const string VersionFile = "surum.txt";

    private static readonly System.Text.RegularExpressions.Regex VersionPattern =
        new(@"^[0-9]+(\.[0-9]+){1,3}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Açılacak exe'nin yolu. ARM64 sürümü yoksa x64 (ARM64 Windows'ta öykünmeyle çalışır) kullanılır.</summary>
    /// <exception cref="FileNotFoundException">Sürüm dosyası ya da exe bulunamadı (mesaj kullanıcıya gösterilir).</exception>
    public static string Resolve(string programRoot, string app, bool arm64)
    {
        var versionPath = Path.Combine(programRoot, VersionFile);
        if (!File.Exists(versionPath))
            throw new FileNotFoundException($"Sürüm dosyası bulunamadı:\n{versionPath}\n\nProgram klasörünün kurulumunu kontrol edin.");

        var version = File.ReadLines(versionPath).FirstOrDefault()?.Trim() ?? "";
        // Yalnızca "1.0.2" biçimi: rakam ve nokta (".", "abc", "1.0 x", "CON" sürüm klasörü olarak kullanılmaz).
        if (!VersionPattern.IsMatch(version))
            throw new FileNotFoundException($"{VersionFile} geçersiz: \"{version}\"");

        foreach (var arch in arm64 ? new[] { "arm64", "x64" } : new[] { "x64" })
        {
            var exe = Path.Combine(programRoot, version, $"{app}-{arch}", $"MuayeneKayit.{app}.exe");
            if (File.Exists(exe))
                return exe;
        }

        throw new FileNotFoundException(
            $"{version} sürümünde {app} bulunamadı:\n{Path.Combine(programRoot, version)}\n\n{VersionFile} dosyasındaki sürüm klasörünün kopyalandığını kontrol edin.");
    }
}
