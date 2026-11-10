using System.Text.Json;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Rework sonrası devam eden parçalar: PartList doğrulaması, MeasurementSnapshot.Parts, düzeltme geçmişi, rework penceresi akışı.
// Özel yardımcılar (With, Check, Load, SendToRework ...) ViewModelTests.cs'dedir.
public static partial class ViewModelTests
{
    public static void ReworkPartsValidateAndRemoved()
    {
        Check(PartList.ValidateContinuing("1-5", "1, 3, 5") is null, "Alt küme geçerli");
        Check(PartList.ValidateContinuing("1-5", "1-5") is null, "Aynı küme geçerli (hiçbiri giderilmedi)");
        var superset = PartList.ValidateContinuing("1-5", "1-6");
        Check(superset is not null && superset.Contains("6 listede yok"), $"Üst küme hata vermeli: {superset}");
        var empty = PartList.ValidateContinuing("1-5", "");
        Check(empty is not null && empty.Contains("'Uygun'"), $"Boş cevap hata vermeli: {empty}");
        Check(PartList.ValidateContinuing("", "") is null && PartList.ValidateContinuing(null, "7") is null && PartList.ValidateContinuing("", "2-4") is null,
            "Önceki boşsa (tümü) her geçerli liste uygun");
        Check(PartList.ValidateContinuing("", "abc") is not null && PartList.ValidateContinuing("1-5", "x") is not null, "Geçersiz metin hata");

        Check(PartList.Removed("1-5", "1, 3, 5").SequenceEqual(new[] { 2, 4 }), "Giderilenler 2, 4");
        Check(PartList.Removed("", "1").Count == 0 && PartList.Removed("1-5", "1-5").Count == 0, "Boş önceki ya da değişmeyen: giderilen yok");
        Check(PartList.Join(PartList.Removed("1-5", "1, 3, 5")) == "2, 4", "Join");
    }

    public static void ReworkPartsOnlyChangeCanSave()
    {
        // Ölçüm girmeden yalnızca parça listesini daraltmak kaydetmeye yeter (pencere anyValue'yu buna göre açar).
        Check(ReinspectForm.PartsChanged("1-5", "1,3,5"), "Daraltma değişikliktir");
        Check(!ReinspectForm.PartsChanged("1-5", "1,2,3,4,5"), "Aynı liste farklı yazımla değişiklik değildir");
        Check(!ReinspectForm.PartsChanged("1-5", "x"), "Geçersiz girdi değişiklik sayılmaz");
        Check(ReinspectForm.CanSave(ReinspectOutcome.Same, false, null, ReinspectForm.PartsChanged("1-5", "1,3,5"), null), "Yalnızca parça daraltma ile Kaydet açık");
    }

    public static void ReworkPartsSnapshotEquality()
    {
        var a = new MeasurementSnapshot(0.5, null, null, null, null, null);
        Check(a with { Parts = "1-5" } == a, "Parts null bilinmiyor demek: fark sayılmaz");
        Check((a with { Parts = "1-5" }) == (a with { Parts = "1-5" }), "Aynı parça eşit");
        Check((a with { Parts = "1-5" }) != (a with { Parts = "1, 3, 5" }), "Farklı parça eşit olmamalı");
        Check((a with { Parts = "" }) != (a with { Parts = "1-5" }), "Tümü ile 1-5 farklı");
    }

    public static void ReworkPartsApplyRecordsEdit()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.5, AffectedParts = "1-5" };
        Check(MeasurementEditor.Snapshot(d).Parts == "1-5" && MeasurementEditor.Snapshot(new Defect()).Parts == "", "Snapshot parçaları taşır; boş = \"\"");

        Check(MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Parts = "1,3,5" }, "A"), "Yalnızca parça değişimi değişikliktir");
        Check(d.AffectedParts == "1, 3, 5" && d.MeasurementEdits is [{ Before.Parts: "1-5", After.Parts: "1, 3, 5" }], "Tek kayıt: 1-5 → 1, 3, 5");

        // Parts null: parçalara dokunulmaz, değişiklik yoksa kayıt yok.
        Check(!MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Parts = null }, "A") && d.MeasurementEdits.Count == 1, "Parts null tek başına kayıt oluşturmaz");
        Check(MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Parts = null, Depth = 0.9 }, "A") && d.AffectedParts == "1, 3, 5"
              && d.MeasurementEdits.Last().After.Parts == "1, 3, 5", "Parts null ölçümle birlikte: parçalar korunur");

        // Tümü: boş metin kayıtta null olur.
        Check(MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Parts = "" }, "A") && d.AffectedParts is null, "Boş = tümü, kayıtta null");

        // Otomatik kayıt birleştirme: aynı kişi, pencere içinde tek satır.
        var m = new Defect { Id = 2, DefectType = "Dent", Depth = 0.5, AffectedParts = "1-5" };
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0);
        var window = TimeSpan.FromMinutes(5);
        MeasurementEditor.Apply(m, MeasurementEditor.Snapshot(m) with { Parts = "1-4" }, "A", t0, window);
        MeasurementEditor.Apply(m, MeasurementEditor.Snapshot(m) with { Parts = "1-3" }, "A", t0.AddMinutes(1), window);
        Check(m.MeasurementEdits is [{ Before.Parts: "1-5", After.Parts: "1-3" }], "Pencere içi parça düzeltmeleri tek satırda birleşir");
        MeasurementEditor.Apply(m, MeasurementEditor.Snapshot(m) with { Parts = "1-5" }, "A", t0.AddMinutes(2), window);
        Check(m.MeasurementEdits.Count == 0, "İlk hâline dönünce satır kalkar");
    }

    public static void ReworkPartsOldJsonHasNoParts()
    {
        var old = JsonSerializer.Deserialize<MeasurementSnapshot>("{\"depth\":0.1,\"highMetal\":false}", SessionStore.JsonOptions)!;
        Check(old.Parts is null, "Eski kayıtta parça yok (null)");
        var now = new Defect { DefectType = "Dent", Depth = 0.1, AffectedParts = "1-5" };
        Check(!MeasurementCompare.Lines(old, now).Any(l => l.StartsWith("Etkilenen parçalar")), "Eski kayıtta parça satırı çıkmaz");
        Check(old == MeasurementEditor.Snapshot(now), "Eski kayıt, parçası olan kusurla fark sayılmaz");

        var round = JsonSerializer.Deserialize<MeasurementSnapshot>(JsonSerializer.Serialize(old with { Parts = "1-5" }, SessionStore.JsonOptions), SessionStore.JsonOptions)!;
        Check(round.Parts == "1-5", "Parts yazılıp okunur");
    }

    public static void ReworkPartsCompareLine()
    {
        var before = new MeasurementSnapshot(null, null, null, null, null, null, Parts: "1-5");
        string Line(string? parts) => MeasurementCompare.Lines(before, new Defect { DefectType = "Dent", AffectedParts = parts }).Single(l => l.StartsWith("Etkilenen parçalar"));
        Check(Line("1, 3, 5") == "Etkilenen parçalar: 1-5 → 1, 3, 5 (giderildi: 2, 4)", Line("1, 3, 5"));
        Check(Line(null) == "Etkilenen parçalar: 1-5 → tümü", Line(null));
        Check(!MeasurementCompare.Lines(before, new Defect { DefectType = "Dent", AffectedParts = "1-5" }).Any(l => l.StartsWith("Etkilenen parçalar")), "Değişmediyse satır yok");
    }

    public static void VmReworkNarrowsPartsAndRecordsHistory() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.AffectedPartsText = "1-5";
        c.Vm.SaveMeasurements();
        SendToRework(c);
        Check(Load(c, 1).Dispositions.Last().Snapshot!.Parts == "1-5", "Rework kararı öncesi parçaları kaydeder");

        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, NoValues, null, null, null, ContinuingParts: "1,3,5"));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Dialogs.ReinspectRequests.Last().AffectedParts == "1-5", "Pencere mevcut parçalarla açılır");

        var d = Load(c, 1);
        Check(StateOf(c, 1) == DefectState.Open && d.AffectedParts == "1, 3, 5", $"Açık, parçalar 1, 3, 5: {d.AffectedParts}");
        var reworkSnap = d.Dispositions.First(x => x.Decision == Decision.Rework).Snapshot!;
        var afterSnap = d.Dispositions.Last().Snapshot!;
        Check(reworkSnap.Parts == "1-5" && afterSnap.Parts == "1, 3, 5", $"Karar görüntüleri: {reworkSnap.Parts} / {afterSnap.Parts}");
        Check(d.MeasurementEdits.Any(e => e.Before.Parts == "1-5" && e.After.Parts == "1, 3, 5"), "Düzeltme geçmişinde parça değişimi");
        Check(MeasurementCompare.Lines(reworkSnap, d).Contains("Etkilenen parçalar: 1-5 → 1, 3, 5 (giderildi: 2, 4)"), "Karşılaştırma satırı");
        Check(c.Vm.DeltaText.Contains("(giderildi: 2, 4)"), $"Değişim satırı: {c.Vm.DeltaText}");
        Check(DefectPresentation.HistoryLines(d).Any(l => l.Contains("giderilen parçalar: 2, 4")), "Geçmiş satırı");
        Check(d.Dispositions.Last().Note is null, "Not uydurulmaz");
        Check(DefectSentence.Statement(d).Contains("on parts 1, 3 and 5"), DefectSentence.Statement(d));

        var html = ReportBuilder.Build(c.Store.LoadSession(c.Folder)!, new[] { d }, 1);
        Check(html.Contains("cleared: 2, 4"), "Raporda giderilen parçalar");
        Check(html.Contains("Parts: 1-5") && html.Contains("Parts: 1, 3, 5"), "Rapor anlık görüntülerinde parçalar");

        // Üst küme: kaydedilmez.
        SendToRework(c);
        var count = Load(c, 1).Dispositions.Count;
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, NoValues, null, null, null, ContinuingParts: "1-6"));
        Wait(c.Vm.ReinspectAsync());
        d = Load(c, 1);
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Notes.Last().Message.Contains("6 listede yok"), $"Hata beklenir: {c.Notes.Last().Message}");
        Check(StateOf(c, 1) == DefectState.AwaitingReinspect && d.AffectedParts == "1, 3, 5" && d.Dispositions.Count == count, "Hiçbir şey kaydedilmemeli");

        // Boş cevap (önceki dolu): hata. Giderildi seçilirse parça sorulmaz, doğrulanmaz.
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, NoValues, null, null, null, ContinuingParts: ""));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && StateOf(c, 1) == DefectState.AwaitingReinspect, "Boş parça listesi reddedilir");
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null, ContinuingParts: "99"));
        Wait(c.Vm.ReinspectAsync());
        Check(StateOf(c, 1) == DefectState.Closed, "Devam etmiyorsa parça cevabı yok sayılır");
    });

    public static void VmManualPartsEditGoesToHistory() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.AffectedPartsText = "1-5";
        c.Vm.SaveMeasurements();
        var edits = Load(c, 1).MeasurementEdits;
        Check(edits.Count > 0 && edits.Last().After.Parts == "1-5" && edits.Last().Before.Parts == "", "Ana ekrandan parça girişi düzeltme geçmişine yazılır");

        c.Vm.AffectedPartsText = "1,3,5";
        c.Vm.SaveMeasurements();
        var d = Load(c, 1);
        Check(d.AffectedParts == "1, 3, 5" && d.MeasurementEdits.Last().After.Parts == "1, 3, 5", "İkinci değişiklik de geçmişte");
        Check(DefectPresentation.HistoryLines(d).Any(l => l.Contains("Parçalar: 1, 3, 5")), "Geçmiş satırında parçalar görünür");
    });
}
