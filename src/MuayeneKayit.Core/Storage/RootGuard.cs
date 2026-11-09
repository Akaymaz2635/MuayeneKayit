namespace MuayeneKayit.Core;

/// <summary>
/// Ortak alan kökünün var olduğunu denetler. Kök (ağ yolu, sürücü) kaybolduysa hiçbir işlem kökü sessizce yeniden
/// oluşturmaz; yeni, boş bir kök veri kaybı gibi görünür ve yazılanlar asıl ortak alana gitmez.
/// Kökün altındaki alt klasörler (oturum, defects, reports, _config) kök varsa oluşturulabilir.
/// </summary>
public static class RootGuard
{
    public static string Message(string root) => $"Ortak alana erişilemiyor: {root}. Ağ bağlantısını kontrol edin.";

    /// <summary>Kök yoksa <see cref="DirectoryNotFoundException"/> (bir IOException) fırlatır.</summary>
    public static void Require(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(Message(root));
    }
}
