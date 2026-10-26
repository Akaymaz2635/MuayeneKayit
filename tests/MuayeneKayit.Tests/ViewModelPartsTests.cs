using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Geri bildirim 3: Etkilenen parçalar kutusu (otomatik kayıt, geçersiz girdi, geçmişe yazılmama).
// Özel yardımcılar (With, Check, Load ...) ViewModelTests.cs'dedir.
public static partial class ViewModelTests
{
    public static void VmAffectedPartsAutoSave() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(c.Vm.AffectedPartsText == "", "Yeni kusurda kutu boş");

        // Kutudan çıkılınca normalize edilip kaydedilir, kutuya geri yazılır; ölçüm geçmişine girmez.
        c.Vm.AffectedPartsText = "1,2,3,4,5 8";
        c.Vm.AutoSave();
        Check(Load(c, 1).AffectedParts == "1-5, 8", $"Kayıt: {Load(c, 1).AffectedParts}");
        Check(c.Vm.AffectedPartsText == "1-5, 8", $"Kutu normalize yazılmalı: {c.Vm.AffectedPartsText}");
        Check(Load(c, 1).MeasurementEdits.Count == 0, "Parça listesi ölçüm düzeltme geçmişine yazılmamalı");
        Check(c.Vm.Sentence.Contains("on parts 1–5 and 8"), c.Vm.Sentence);

        // Geçersiz girdi kaydedilmez, hata bildirilir.
        c.Vm.AffectedPartsText = "abc";
        c.Vm.AutoSave();
        Check(Load(c, 1).AffectedParts == "1-5, 8" && c.Notes.Last().Kind == StatusKind.Error, "Geçersiz girdi kaydedilmemeli");

        // Boşaltılınca temizlenir (tüm parça).
        c.Vm.AffectedPartsText = "";
        c.Vm.AutoSave();
        Check(Load(c, 1).AffectedParts is null && c.Vm.Sentence.Contains("on part."), "Boş = tümü");

        // Kusur değişirken kaydedilir.
        c.Vm.AffectedPartsText = "7";
        c.Vm.AddDefect("Crack");
        Check(Load(c, 1).AffectedParts == "7" && c.Vm.AffectedPartsText == "", "Kusur değişince önceki kusurun parçaları kaydedilmeli");
    });
}
