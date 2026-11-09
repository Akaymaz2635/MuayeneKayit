// Fonksiyonel test planı Bölüm 6: ŞÜPHELİ (B-xx) testleri. Beklenti DOĞRU davranıştır; kırmızı = hata doğrulandı.
// Normal koşuya girmez; yalnızca: dotnet run --project tests/MuayeneKayit.Tests -- --supheli
// Doğrulanan 19 hata düzeltildi ve testleri FonkFixedTests (ana liste) içine taşındı; yeni şüpheli bulgular buraya eklenir.
public static class FonkSupheliTests
{
    public static List<(string Name, Action Body)> All { get; } = new();
}
