using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuayeneKayit.Core;

/// <summary>
/// Eski sürümde (6 haneli PIN, 200.000 tur PBKDF2) yazılmış admin.json üretir. Mevcut kurulumlardaki eski kayıtların
/// girişte hâlâ çalıştığını sınamak için: yeni kod yeni PIN'leri yalnızca 8-12 haneli ve daha yüksek turla yazar.
/// </summary>
public static class TestPins
{
    public static void WriteLegacy(string root, string pin, int iterations = 200_000)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256, 32);
        var record = new AdminPinRecord
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            Iterations = iterations,
        };
        Directory.CreateDirectory(SharedConfig.Folder(root));
        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "admin.json"), JsonSerializer.Serialize(record, SessionStore.JsonOptions));
    }
}
