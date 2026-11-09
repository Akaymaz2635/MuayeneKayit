using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MuayeneKayit.Core;

/// <summary>Bir ortak alandaki admin girişi için hatalı deneme sayısı ve kilidin bitiş zamanı.</summary>
public sealed record LoginThrottleState(int Failed, DateTime LockedUntil);

/// <summary>
/// Admin PIN denemelerinin kalıcı kilitlenme durumu: bu bilgisayarın yerel profilinde (LocalAppData) ortak alan başına
/// bir dosyada tutulur. Program kapatılıp açılınca sayaç sıfırlanmaz. Ortak alanda saklanmaz: PIN özetini okuyabilen biri
/// çevrimdışı deneme yapabilir, ama bu bilgisayardaki çevrimiçi denemeleri bu kayıt sınırlar.
/// </summary>
public static class LoginThrottle
{
    private static string? _folderOverride;

    /// <summary>Durum dosyalarının klasörü. Varsayılan: %LOCALAPPDATA%\MuayeneKayit. Testler geçici klasör verir.</summary>
    public static string StateFolder
    {
        get => _folderOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuayeneKayit");
        set => _folderOverride = value;
    }

    public static LoginThrottleState Load(string root)
    {
        try
        {
            var path = PathFor(root);
            if (File.Exists(path) && new FileInfo(path).Length <= 4096
                && JsonSerializer.Deserialize<LoginThrottleState>(File.ReadAllText(path)) is { Failed: >= 0 } state)
                return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Okunamayan durum dosyası kilidi açmış sayılmaz ama uygulamayı da durdurmaz: bellekteki sayaç geçerlidir.
        }
        return new LoginThrottleState(0, DateTime.MinValue);
    }

    public static void Save(string root, LoginThrottleState state)
    {
        try
        {
            Directory.CreateDirectory(StateFolder);
            AtomicFile.WriteAllText(PathFor(root), JsonSerializer.Serialize(state));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Yerel profil yazılamıyorsa kilitlenme yalnızca bellekte kalır (önceki davranış).
        }
    }

    public static void Clear(string root)
    {
        try
        {
            var path = PathFor(root);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string PathFor(string root)
    {
        var key = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return Path.Combine(StateFolder, $"admin-giris-{hash}.json");
    }
}
