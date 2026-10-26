using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Geri bildirim 1: ViewModel akışları (öneri gizleme, çoklu yeni hata, bölge sonuçları, void sızıntısı).
// Program.cs bu sınıfı "using static" ile kullanır; özel yardımcılar (With, Check, Wait ...) ViewModelTests.cs'dedir.
public static partial class ViewModelTests
{
    private const string Ph1 = "1_photo_01.jpg";
    private const string Ph2 = "1_photo_02.jpg";
    private const string Ph3 = "1_photo_03.jpg";

    /// <summary>Ölçülü Dent (#1) ve 3 fotoğraf (dosyalar gerekmez; kartlar yalnızca adı kullanır).</summary>
    private static void AddThreePhotoDent(Ctx c)
    {
        AddMeasuredDent(c);
        c.Store.UpdateDefect(c.Folder, 1, d => d.Photos.AddRange(new[] { Ph1, Ph2, Ph3 }));
        c.Vm.Refresh(1);
    }

    private static void SendToReworkWithRegions(Ctx c, params string[] continuing)
    {
        c.Dialogs.Decisions.Enqueue(new DecisionInput("OP40", null, null, null, continuing));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
    }

    private static readonly MeasurementSnapshot Depth07 = new(0.7, null, null, null, null, null);

    // ───────────────────────── 3: öneri gizleme ─────────────────────────

    public static void VmHiddenSuggestionsPerBoxAndReturnOnUse() => With(c =>
    {
        c.Open();                                            // PD170 / OP40 / SN001
        c.Store.OpenOrCreate(new Session { PartNumber = "PD171", OperationNumber = "OP10", SerialNumber = "A1", Inspector = "x", Project = "PD171" });
        c.Store.OpenOrCreate(new Session { PartNumber = "PD170", OperationNumber = "OP50", SerialNumber = "B2", Inspector = "x", Project = "PD170" });

        // Parça önerisi gizlenir (büyük/küçük harf fark etmez); operasyon ve seri kutuları etkilenmez.
        c.Vm.HideSuggestion(SuggestLevel.Part, "pd171");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "").SequenceEqual(new[] { "PD170" }), "Gizlenen parça önerilmemeli");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "171").Count == 0, "Arama gizliyi getirmemeli");

        c.Vm.PartNumber = "PD170";
        Check(c.Vm.Suggestions(SuggestLevel.Operation, "").SequenceEqual(new[] { "OP40", "OP50" }), "Operasyonlar etkilenmemeli");
        c.Vm.HideSuggestion(SuggestLevel.Operation, "OP50");
        Check(c.Vm.Suggestions(SuggestLevel.Operation, "").SequenceEqual(new[] { "OP40" }), "Operasyon gizlendi");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "").SequenceEqual(new[] { "PD170" }), "Parça listesi aynı kalmalı");
        c.Vm.OperationNumber = "OP40";
        Check(c.Vm.Suggestions(SuggestLevel.Serial, "").SequenceEqual(new[] { "SN001" }), "Seriler etkilenmemeli");

        // Klasör silinmez: yalnızca bu bilgisayarda öneriden çıkar.
        Check(Directory.Exists(c.Store.FolderFor("PD171", "OP10", "A1")), "Klasör silinmemeli");

        // Açılamayan muayene gizlemeyi kaldırmaz.
        c.Vm.ChangeSession();
        c.Vm.PartNumber = "PD171"; c.Vm.OperationNumber = "OP10"; c.Vm.SerialNumber = "A1"; c.Vm.InspectorName = "";
        c.Vm.OpenSession();
        Check(c.Notes.Last().Kind == StatusKind.Error, "Inspector boşken açılmamalı");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "").SequenceEqual(new[] { "PD170" }), "Başarısız açılış gizlemeyi kaldırmamalı");

        // O değerlerle muayene açılınca gizleme kalkar ("tekrar kullanınca geri gelir"); diğerleri gizli kalır.
        c.Vm.InspectorName = "A. Yılmaz";
        c.Vm.OpenSession();
        Check(c.Vm.IsSessionOpen, "Açılmalı");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "").SequenceEqual(new[] { "PD170", "PD171" }), "PD171 geri gelmeli");
        c.Vm.PartNumber = "PD170";
        Check(c.Vm.Suggestions(SuggestLevel.Operation, "").SequenceEqual(new[] { "OP40" }), "OP50 hâlâ gizli");
    });

    // ───────────────────────── Metinler ─────────────────────────

    public static void VmEnglishInspectorTexts() => With(c =>
    {
        Check(c.Vm.InspectorHeader == "Inspector", $"Başlık: {c.Vm.InspectorHeader}");
        c.Open();
        Check(c.Vm.SessionSubtitle.StartsWith("Inspector: A. Yılmaz"), $"Alt başlık: {c.Vm.SessionSubtitle}");

        c.Vm.ChangeSession();
        c.Vm.InspectorName = "";
        c.Vm.OpenSession();
        Check(c.Notes.Last() == ("Inspector adı boş olamaz.", StatusKind.Error), $"Hata: {c.Notes.Last().Message}");

        SharedConfig.SaveUseWindowsUser(c.Store.Root, true);
        c.Vm.ChangeSession();
        Check(c.Vm.InspectorHeader == "Inspector (Windows oturumu)", $"Windows başlığı: {c.Vm.InspectorHeader}");

        c.Vm.PartNumber = "PD170"; c.Vm.OperationNumber = "OP40"; c.Vm.SerialNumber = "SN001";
        c.Vm.OpenSession();
        c.Vm.AddDefect("Braze Void");
        Check(c.Vm.RadiusHeader == "Çap (mm)", $"Çap başlığı: {c.Vm.RadiusHeader}");
        c.Vm.RadiusText = "abc";
        c.Vm.SaveMeasurements();
        Check(c.Notes.Last().Message.Contains("Çap sayı olmalı"), $"Ayrıştırma mesajı: {c.Notes.Last().Message}");
    });

    // ───────────────────────── 5: birden fazla yeni hata ─────────────────────────

    public static void VmReinspectMultipleNewDefects() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);

        var scratch = c.Vm.TypeNames.First(n => n != "Dent");
        var nick = c.Vm.TypeNames.First(n => n != "Dent" && n != scratch);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { scratch, scratch, nick }, "çoklu"));
        Wait(c.Vm.ReinspectAsync());

        Check(c.Vm.Defects.Count == 4 && c.Vm.SelectedId == 2, $"3 yeni kusur, ilki seçili: {c.Vm.Defects.Count}/{c.Vm.SelectedId}");
        Check(StateOf(c, 1) == DefectState.Superseded, "Parent devredilmeli");
        Check(Enumerable.Range(2, 3).All(i => Load(c, i).SupersedesId == 1 && Load(c, i).FoundAtPhoto is null), "Child'lar bağlı, bölgesiz");
        Check(Load(c, 3).DefectType == scratch && Load(c, 4).DefectType == nick, "Tipler sırayla");
        Check(c.Audit.Entries.Any(e => e.EndsWith($"|disposition.continue|#1 -> #2 {scratch}, #3 {scratch}, #4 {nick}")), "Denetim satırı");
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message.Contains("#2 seçildi"), $"Bildirim: {c.Notes.Last().Message}");

        c.Vm.Select(1);
        Check(c.Vm.Sentence.Contains("Continues as Defects 2, 3, 4."), c.Vm.Sentence);
        Check(c.Vm.NextStepText == "Takip #2, #3, #4 numaralı kusurlar üzerinden yapılıyor.", c.Vm.NextStepText);
        Check(c.Vm.Defects.First(d => d.Id == 1).NextStep.Contains("#2, #3, #4"), "Liste satırı çoğul");
    });

    public static void VmReinspectNewDefectWithoutTypeIsRejected() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new string?[] { null }, null));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Notes.Last().Message == "Yeni hata tipini seçin.", $"Mesaj: {c.Notes.Last().Message}");
        Check(StateOf(c, 1) == DefectState.AwaitingReinspect && c.Vm.Defects.Count == 1, "Hiçbir şey yazılmamalı");
    });

    // ───────────────────────── 6: bölge bazında sonuç ─────────────────────────

    public static void VmDecisionAsksRegionsAndMarksUncheckedConforming() => With(c =>
    {
        AddThreePhotoDent(c);

        // 3 aktif bölge: karar penceresi bölgeleri ister.
        SendToReworkWithRegions(c, Ph1, Ph2);
        var req = c.Dialogs.DecisionRequests.Last();
        Check(req.Regions is { Count: 3 } && req.Regions.Select(r => r.Label).SequenceEqual(new[] { "1.1", "1.2", "1.3" }), "Üç bölge sorulmalı");
        Check(req.Regions![0].ThumbnailPath.EndsWith(Ph1), "Küçük resim yolu");
        var rework = Load(c, 1).Dispositions.Last();
        Check(rework.Decision == Decision.Rework
              && rework.PhotoOutcomes![Ph1] == PhotoOutcome.Continues && rework.PhotoOutcomes[Ph2] == PhotoOutcome.Continues
              && rework.PhotoOutcomes[Ph3] == PhotoOutcome.Conforms, "İşaretsiz bölge uygun sayılmalı");
        Check(DefectRegions.ActivePhotos(Load(c, 1)).SequenceEqual(new[] { Ph1, Ph2 }), "Sonraki adım yalnızca aktif bölgeleri bilir");

        // Rework sonrası pencere yalnızca iki aktif bölgeyi sorar.
        c.Dialogs.Reinspects.Enqueue(null);
        Wait(c.Vm.ReinspectAsync());
        var rr = c.Dialogs.ReinspectRequests.Last();
        Check(rr.Regions is { Count: 2 } && rr.Regions.Select(r => r.Label).SequenceEqual(new[] { "1.1", "1.2" }), "Reinspect yalnızca aktif bölgeleri sormalı");

        // Geri alma kararı da bölge durumlarını geri getirir.
        c.Dialogs.UndoReasons.Enqueue("yanlış bölge seçimi");
        Wait(c.Vm.UndoLastAsync());
        Check(DefectRegions.ActivePhotos(Load(c, 1)).Count == 3, "Undo sonrası üç bölge aktif");
        Check(c.Vm.Photos.All(p => p.RegionBadge == ""), "Rozet kalmamalı");
    });

    public static void VmDecisionRegionSelectionIsValidated() => With(c =>
    {
        AddThreePhotoDent(c);
        var accept = Option(c, Decision.Accept);

        foreach (var bad in new IReadOnlyList<string>?[] { null, new string[0], new[] { "yok.jpg" } })
        {
            c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, null, null, bad));
            Wait(c.Vm.GiveDecisionAsync(accept));
            Check(c.Notes.Last().Kind == StatusKind.Error && Load(c, 1).Dispositions.Count == 0, "Geçersiz bölge seçimi karar yazmamalı");
        }

        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "KAB-1", null, null, new[] { Ph2 }));
        Wait(c.Vm.GiveDecisionAsync(accept));
        var d = Load(c, 1);
        Check(StateOf(c, 1) == DefectState.Closed && d.Dispositions.Last().PhotoOutcomes![Ph2] == PhotoOutcome.Continues
              && d.Dispositions.Last().PhotoOutcomes![Ph1] == PhotoOutcome.Conforms, "Kabul: işaretli bölge kapsamda, diğerleri uygun");

        // Tek aktif bölgesi olan kusurda bölge sorusu yoktur.
        var d2Id = AddSecondDent(c);
        c.Store.UpdateDefect(c.Folder, d2Id, x => x.Photos.AddRange(new[] { "2_photo_01.jpg" }));
        c.Vm.Refresh(d2Id);
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(c.Dialogs.DecisionRequests.Last().DefectId == d2Id && c.Dialogs.DecisionRequests.Last().Regions is null, "Tek bölge: soru yok");
    });

    private static int AddSecondDent(Ctx c)
    {
        c.Vm.AddDefect("Dent");
        return c.Vm.SelectedId!.Value;
    }

    public static void VmReinspectPerRegionOutcomes() => With(c =>
    {
        AddThreePhotoDent(c);
        SendToReworkWithRegions(c, Ph1, Ph2, Ph3);

        // 1.1 devam ediyor, 1.2 yeni hata (2 kusur), 1.3 giderildi → parent açık kalır, 2 child.
        var scratch = c.Vm.TypeNames.First(n => n != "Dent");
        var nick = c.Vm.TypeNames.First(n => n != "Dent" && n != scratch);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, Depth07, null, null, null, new[]
        {
            new RegionChoice(Ph1, PhotoOutcome.Continues, Array.Empty<string?>()),
            new RegionChoice(Ph2, PhotoOutcome.NewDefect, new string?[] { scratch, nick }),
            new RegionChoice(Ph3, PhotoOutcome.Conforms, Array.Empty<string?>()),
        }));
        Wait(c.Vm.ReinspectAsync());

        var parent = Load(c, 1);
        Check(StateOf(c, 1) == DefectState.Open && parent.Depth == 0.7, $"Parent açık, ölçüm güncel: {StateOf(c, 1)} {parent.Depth}");
        Check(parent.Dispositions.Last().Decision == Decision.AfterReworkSameDefect && parent.Dispositions.Last().NewDefectIds!.SequenceEqual(new[] { 2, 3 }), "Karar + yeni kusurlar");
        Check(Load(c, 2).FoundAtPhoto == Ph2 && Load(c, 3).FoundAtPhoto == Ph2 && Load(c, 2).DefectType == scratch, "Child'lar 1.2'de bulunmuş");
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message.Contains("Yeni kusur: #2, #3"), $"Bildirim: {c.Notes.Last().Message}");
        Check(c.Vm.SelectedId == 1, "Parent seçili kalmalı (devam ediyor)");

        // Ekran: rozetler, cümle, geçmiş.
        Check(c.Vm.Photos.Single(p => p.FileName == Ph3).RegionBadge == "Giderildi", "1.3 rozeti");
        Check(c.Vm.Photos.Single(p => p.FileName == Ph2).RegionBadge == "Yeni kusur #2, #3", "1.2 rozeti");
        Check(c.Vm.Photos.Single(p => p.FileName == Ph1).RegionBadge == "" && !c.Vm.Photos.Single(p => p.FileName == Ph1).HasRegionBadge, "Devam eden bölgede rozet yok");
        Check(c.Vm.Sentence.Contains("New defects found after rework: 2, 3."), c.Vm.Sentence);
        var history = string.Join("\n", c.Vm.History);
        Check(history.Contains("bölgeler: 1.1 devam, 1.2 yeni kusur #2, #3, 1.3 giderildi") && history.Contains("açılan kusurlar: #2, #3"), history);

        // Sonraki tur yalnızca devam eden bölgeyi sorar (tek aktif bölge → bugünkü 3 seçenek).
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));        // vazgeç (cevap yok)
        c.Dialogs.Decisions.Enqueue(new DecisionInput("OP40", null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
        Check(c.Dialogs.DecisionRequests.Last().Regions is null, "Tek aktif bölge: karar penceresinde bölge sorusu yok");
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Dialogs.ReinspectRequests.Last().Regions is null, "Tek aktif bölge: reinspect penceresinde bölge listesi yok");
        parent = Load(c, 1);
        Check(StateOf(c, 1) == DefectState.Closed && parent.Dispositions.Last().PhotoOutcomes![Ph1] == PhotoOutcome.Conforms, "Son bölge giderilince kapanır, sonucu kayıtlı");
    });

    public static void VmReinspectRegionsMissingChoiceIsRejected() => With(c =>
    {
        AddThreePhotoDent(c);
        SendToReworkWithRegions(c, Ph1, Ph2, Ph3);
        var path = Path.Combine(c.Folder, "defects", "1.json");
        var before = File.ReadAllText(path);

        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null, new[]
        {
            new RegionChoice(Ph1, PhotoOutcome.Conforms, Array.Empty<string?>()),
            new RegionChoice(Ph2, null, Array.Empty<string?>()),
            new RegionChoice(Ph3, PhotoOutcome.Conforms, Array.Empty<string?>()),
        }));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && File.ReadAllText(path) == before, "Seçimsiz bölge işlenmemeli");

        // Yeni hata seçilen bölgede tip yoksa da işlenmez.
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, null, null, new[]
        {
            new RegionChoice(Ph1, PhotoOutcome.Conforms, Array.Empty<string?>()),
            new RegionChoice(Ph2, PhotoOutcome.NewDefect, new string?[] { null }),
            new RegionChoice(Ph3, PhotoOutcome.Conforms, Array.Empty<string?>()),
        }));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && File.ReadAllText(path) == before, "Tipsiz yeni hata işlenmemeli");

        // Hepsi giderildi → kapanır, çocuk açılmaz.
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null, new[]
        {
            new RegionChoice(Ph1, PhotoOutcome.Conforms, Array.Empty<string?>()),
            new RegionChoice(Ph2, PhotoOutcome.Conforms, Array.Empty<string?>()),
            new RegionChoice(Ph3, PhotoOutcome.Conforms, Array.Empty<string?>()),
        }));
        Wait(c.Vm.ReinspectAsync());
        Check(StateOf(c, 1) == DefectState.Closed && c.Vm.Defects.Count == 1, "Hepsi giderildi: kapanır");
    });

    // ───────────────────────── 4: void / geri alma ─────────────────────────

    public static void VmVoidLastChildReopensParentWithoutLeaks() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { c.Vm.TypeNames.First(n => n != "Dent") }, null));
        Wait(c.Vm.ReinspectAsync());
        Check(c.Vm.SelectedId == 2, "Child seçili");

        c.Dialogs.VoidReasons.Enqueue("yanlış tip seçilmişti");
        Wait(c.Vm.VoidAsync());
        Check(StateOf(c, 2) == DefectState.Voided && StateOf(c, 1) == DefectState.AwaitingReinspect, "Child void, parent tekrar bekler");
        Check(c.Notes.Last().Kind == StatusKind.Success && c.Notes.Last().Message.Contains("#1 yeniden rework sonrası muayene bekliyor"), $"Bildirim: {c.Notes.Last().Message}");
        Check(c.Audit.Entries.Any(e => e.Contains("|disposition.undo|#1 AfterReworkNewDefect")), "Sistem geri almasının denetim satırı");

        c.Vm.Select(1);
        Check(!c.Vm.Sentence.Contains("Continues") && !c.Vm.Sentence.Contains("2"), $"Cümle: {c.Vm.Sentence}");
        Check(c.Vm.IsAwaiting && c.Vm.NextStepText.Contains("Rework'te"), $"Ekran: {c.Vm.NextStepText}");
        Check(!c.Vm.History.Any(h => h.Contains("#2") || h.Contains("new defect")), "Geçmişte void child ve geri alınan karar yok");
        Check(!SessionGate.CanComplete(c.Store.LoadDefects(c.Folder)), "Parent açık: muayene kapanmamalı");
        c.Vm.Complete();
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message.Contains("#1"), "Complete parent'ı açık saymalı");
    });

    public static void VmUndoMultipleChildrenAndVoidOneOfTwo() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        var other = c.Vm.TypeNames.First(n => n != "Dent");
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { other, other }, null));
        Wait(c.Vm.ReinspectAsync());

        // İkisinden biri void: parent devredilmiş kalır, yalnızca kalan anılır.
        c.Vm.Select(2);
        c.Dialogs.VoidReasons.Enqueue("yanlış");
        Wait(c.Vm.VoidAsync());
        Check(StateOf(c, 1) == DefectState.Superseded, "Parent devredilmiş kalmalı");
        c.Vm.Select(1);
        Check(c.Vm.Sentence.Contains("Continues as Defect 3.") && c.Vm.NextStepText == "Takip #3 numaralı kusur üzerinden yapılıyor.", $"{c.Vm.Sentence} / {c.Vm.NextStepText}");

        // Geri alma penceresi yalnızca void olmamış child'ı bildirir; geri alınca o da void olur.
        c.Dialogs.UndoReasons.Enqueue("yanlış devir");
        Wait(c.Vm.UndoLastAsync());
        Check(c.Dialogs.UndoRequests.Last().ChildrenToVoid.SequenceEqual(new[] { 3 }), "Yalnızca #3 void edilecek");
        Check(StateOf(c, 3) == DefectState.Voided && StateOf(c, 1) == DefectState.AwaitingReinspect, "Hepsi void, parent bekler");
    });

    public static void VmUndoTwoChildrenListsBoth() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        var other = c.Vm.TypeNames.First(n => n != "Dent");
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { other, "Dent" }, null));
        Wait(c.Vm.ReinspectAsync());

        c.Vm.Select(1);
        c.Dialogs.UndoReasons.Enqueue("yanlış devir");
        Wait(c.Vm.UndoLastAsync());
        Check(c.Dialogs.UndoRequests.Last().ChildrenToVoid.SequenceEqual(new[] { 2, 3 }), "İki child bildirilmeli");
        Check(StateOf(c, 2) == DefectState.Voided && StateOf(c, 3) == DefectState.Voided && StateOf(c, 1) == DefectState.AwaitingReinspect, "İkisi de void");
        Check(c.Audit.Entries.Any(e => e.Contains("|disposition.undo|") && e.Contains("child #2, #3 void")), "Denetim satırı çoğul");
        Check(c.Notes.Last().Message.Contains("#2, #3 void edildi"), $"Bildirim: {c.Notes.Last().Message}");
    });
}
