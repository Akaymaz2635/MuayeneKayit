using System.Text.Json;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Geri bildirim 1: çoklu yeni hata, bölge (fotoğraf) bazında sonuç, void edilen child'ın sızmaması, eski JSON uyumu.
public static class FeedbackCoreTests
{
    private const string P1 = "1_photo_01.jpg";
    private const string P2 = "1_photo_02.jpg";
    private const string P3 = "1_photo_03.jpg";

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void With(Action<SessionStore, string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var (folder, _) = store.OpenOrCreate(new Session
            {
                PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-F1", Inspector = "A. Yılmaz", Project = "PD170",
            });
            body(store, folder);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static readonly Session ReportSession = new()
    {
        PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-F1", Inspector = "A. Yılmaz", Project = "PD170",
    };

    /// <summary>Rework verilmiş #1 kusuru (0-3 fotoğraflı).</summary>
    private static Defect Reworked(SessionStore store, string folder, int photos)
    {
        var d = store.AddDefect(folder, "Dent", x =>
        {
            x.Depth = 0.5;
            x.Photos.AddRange(new[] { P1, P2, P3 }.Take(photos));
        });
        return store.UpdateDefect(folder, d.Id, x =>
            DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP40"));
    }

    private static ReinspectResult NewOnly(params (string Type, string? Photo)[] news) =>
        new(false, null, null, news.Select(n => new NewDefectSpec(n.Type, n.Photo)).ToList(), null);

    private static Defect Get(SessionStore store, string folder, int id) => store.LoadDefects(folder).First(d => d.Id == id);

    private static void Accept(SessionStore store, string folder, int id) =>
        store.UpdateDefect(folder, id, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya", reference: "KAB-1"));

    // ───────────────────────── 5: birden fazla yeni hata ─────────────────────────

    public static void MultipleNewDefectsLinkAndGate() => With((store, folder) =>
    {
        Reworked(store, folder, 0);
        var (parent, kids) = store.ApplyReinspect(folder, 1, NewOnly(("Scratch", null), ("Scratch", null), ("Nick", null)), "CTP");

        Expect(kids.Select(k => k.Id).SequenceEqual(new[] { 2, 3, 4 }), "Yeni kusurlar sırayla numaralanmalı (aynı tip iki kez olabilir)");
        Expect(kids.All(k => k.SupersedesId == 1 && k.Unit == parent.Unit), "Child'lar parent'a bağlanmalı, birimi taşımalı");
        Expect(parent.SupersededById is null, "Eski tek bağ yeni kodda yazılmamalı");

        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.Superseded, "Parent devredilmiş olmalı");
        Expect(DefectRegions.ChildIds(p).SequenceEqual(new[] { 2, 3, 4 }), "Child listesi kararlardan okunmalı");
        Expect(DefectSentence.Describe(p, all).Contains("Continues as Defects 2, 3, 4."), DefectSentence.Describe(p, all));
        Expect(DefectSentence.Describe(all[1], all).Contains("Continues from Defect 1."), "Bölgesiz child: bölge yazılmaz");
        Expect(DefectPresentation.NextStepFor(p, DefectState.Superseded, all) == "Takip #2, #3, #4 numaralı kusurlar üzerinden yapılıyor.",
            DefectPresentation.NextStepFor(p, DefectState.Superseded, all));

        // Geçit: devredilen kusur, void olmayan TÜM child'lar çözülünce çözülür.
        Expect(!SessionGate.CanComplete(all), "Child'lar açıkken kapanmamalı");
        Accept(store, folder, 2);
        Accept(store, folder, 3);
        Expect(!SessionGate.CanComplete(store.LoadDefects(folder)), "Biri açıkken kapanmamalı");
        Accept(store, folder, 4);
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Hepsi kapanınca kapanabilmeli");
    });

    // ───────────────────────── 6: bölge bazında sonuç ─────────────────────────

    public static void MixedRegionOutcomes() => With((store, folder) =>
    {
        var parent = Reworked(store, folder, 3);
        var measured = MeasurementEditor.Snapshot(parent) with { Depth = 0.9 };
        var result = new ReinspectResult(
            true,
            measured,
            new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Conforms, [P2] = PhotoOutcome.Continues, [P3] = PhotoOutcome.NewDefect },
            new[] { new NewDefectSpec("Scratch", P3), new NewDefectSpec("Nick", P3) },
            null);
        var (p, kids) = store.ApplyReinspect(folder, 1, result, "CTP");

        Expect(DefectStateMachine.GetState(p) == DefectState.Open, "Devam eden bölge var: parent açık kalmalı");
        Expect(p.Depth == 0.9 && p.MeasurementEdits.Count == 1, "Ölçüm güncel olmalı (kusur başına tek ölçüm)");
        Expect(p.Dispositions[^1].Decision == Decision.AfterReworkSameDefect && p.Dispositions[^1].NewDefectIds!.SequenceEqual(new[] { 2, 3 }),
            "Karar: aynı kusur + açılan yeni kusurlar");
        Expect(p.Dispositions[^1].Snapshot?.Depth == 0.9, "Karar anı görüntüsü yeni ölçümü taşımalı");
        Expect(kids.Count == 2 && kids.All(k => k.FoundAtPhoto == P3), "İki child bölge 3'te bulunmuş olmalı");

        Expect(DefectRegions.Status(p, P1) == RegionStatus.Conforms, "1.1 giderildi");
        Expect(DefectRegions.Status(p, P2) == RegionStatus.Active, "1.2 devam ediyor");
        Expect(DefectRegions.Status(p, P3) == RegionStatus.NewDefect, "1.3 yeni kusur");
        Expect(DefectRegions.ActivePhotos(p).SequenceEqual(new[] { P2 }), "Yalnızca aktif bölge sorulmalı");

        var all = store.LoadDefects(folder);
        Expect(DefectSentence.Describe(p, all).Contains("New defects found after rework: 2, 3."), DefectSentence.Describe(p, all));
        Expect(DefectSentence.Describe(all.First(d => d.Id == 2), all).Contains("Continues from Defect 1 (region 1.3)."),
            DefectSentence.Describe(all.First(d => d.Id == 2), all));
        Expect(!SessionGate.CanComplete(all), "Parent açık, child'lar açık");

        // İkinci tur: yalnızca kalan bölge giderilince parent kapanır; yeni kusurlar kendi başına açık kalır.
        store.UpdateDefect(folder, 1, d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP40"));
        var (closed, none) = store.ApplyReinspect(folder, 1,
            new ReinspectResult(false, null, new Dictionary<string, PhotoOutcome> { [P2] = PhotoOutcome.Conforms }, Array.Empty<NewDefectSpec>(), null), "CTP");
        Expect(none.Count == 0 && DefectStateMachine.GetState(closed) == DefectState.Closed, "Hepsi giderilince kapanmalı");
        Expect(DefectRegions.ActivePhotos(closed).Count == 0, "Aktif bölge kalmamalı");
        Expect(DefectRegions.ChildIds(closed).SequenceEqual(new[] { 2, 3 }), "Önceki turun yeni kusurları bağlı kalmalı");
        Expect(SessionGate.BlockingDefects(store.LoadDefects(folder)).Select(d => d.Id).SequenceEqual(new[] { 2, 3 }), "Yalnızca child'lar engellemeli");
    });

    public static void AllRegionsConformClosesAndInvalidPhotoRejected() => With((store, folder) =>
    {
        Reworked(store, folder, 3);

        var bad = false;
        try
        {
            store.ApplyReinspect(folder, 1,
                new ReinspectResult(false, null, new Dictionary<string, PhotoOutcome> { ["yok.jpg"] = PhotoOutcome.Conforms }, Array.Empty<NewDefectSpec>(), null), "CTP");
        }
        catch (InvalidOperationException) { bad = true; }
        Expect(bad && Get(store, folder, 1).Dispositions.Count == 1, "Kusurda olmayan bölge reddedilmeli, dosya değişmemeli");

        var all = new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Conforms, [P2] = PhotoOutcome.Conforms, [P3] = PhotoOutcome.Conforms };
        var (p, _) = store.ApplyReinspect(folder, 1, new ReinspectResult(false, null, all, Array.Empty<NewDefectSpec>(), "hepsi uygun"), "CTP");
        Expect(DefectStateMachine.GetState(p) == DefectState.Closed, "Hepsi giderildi: kapanmalı");
        Expect(p.Dispositions[^1].Decision == Decision.AfterReworkConforms, "Karar: rework sonrası uygun");
    });

    public static void UndoVoidsAllChildrenAndRestoresRegions() => With((store, folder) =>
    {
        var parent = Reworked(store, folder, 3);
        store.ApplyReinspect(folder, 1,
            new ReinspectResult(true, MeasurementEditor.Snapshot(parent),
                new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Conforms, [P2] = PhotoOutcome.Continues, [P3] = PhotoOutcome.NewDefect },
                new[] { new NewDefectSpec("Scratch", P3), new NewDefectSpec("Nick", P3) }, null), "CTP");

        // Child'lardan birinin kararı varsa geri alma hiçbirini değiştirmez.
        store.UpdateDefect(folder, 2, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya"));
        var blocked = false;
        try { store.UndoLastDecision(folder, 1, "M. Kaya", "yanlış"); }
        catch (InvalidOperationException ex) { blocked = ex.Message.Contains("#2"); }
        Expect(blocked, "Kararı olan child varken geri alma reddedilmeli");
        Expect(DefectStateMachine.GetState(Get(store, folder, 3)) == DefectState.Open, "Diğer child void edilmemeli");
        store.UndoLastDecision(folder, 2, "M. Kaya", "yanlış kabul");

        var (undone, voided) = store.UndoLastDecision(folder, 1, "M. Kaya", "yanlış sonuç girildi");
        Expect(undone.Decision == Decision.AfterReworkSameDefect && voided.SequenceEqual(new[] { 2, 3 }), "Tüm child'lar void edilmeli");

        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.AwaitingReinspect, "Parent tekrar rework sonrası muayene beklemeli");
        Expect(DefectRegions.ActivePhotos(p).Count == 3, "Bölge durumları geri gelmeli (hepsi aktif)");
        Expect(DefectRegions.ChildIds(p).Count == 0, "Geri alınan karar child bağı bırakmamalı");
        Expect(all.Where(d => d.Id > 1).All(d => DefectStateMachine.GetState(d) == DefectState.Voided), "Child'lar void");

        var html = ReportBuilder.Build(ReportSession, all, 1);
        Expect(!html.Contains("Region 1.") && !html.Contains("#2 ") && !html.Contains("Defect 2"), "Rapor geri alınan sonucu ve void child'ları göstermemeli");
    });

    // ───────────────────────── 4: void edilen child sızmaz ─────────────────────────

    public static void VoidLastChildReopensParent() => With((store, folder) =>
    {
        Reworked(store, folder, 0);
        store.ApplyReinspect(folder, 1, NewOnly(("Scratch", null)), "CTP");

        var reopened = store.VoidDefect(folder, 2, "A. Yılmaz", "yanlış tip");
        Expect(reopened == 1, "Son aktif child void edilince parent geri açılmalı");

        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.AwaitingReinspect, "Parent tekrar rework sonrası muayene beklemeli");
        var undo = p.Dispositions[^1];
        Expect(undo.Decision == Decision.Undo && undo.Source == DecisionSource.System
               && undo.Note == "Yeni kusur #2 void edildi; #1 yeniden rework sonrası muayene bekliyor", $"Sistem kaydı: {undo.Source} {undo.Note}");
        Expect(DefectStateMachine.GetState(all.First(d => d.Id == 2)) == DefectState.Voided, "Child void olmalı");

        var sentence = DefectSentence.Describe(p, all);
        Expect(!sentence.Contains("Continues as") && !sentence.Contains("2"), $"Cümlede void child olmamalı: {sentence}");
        Expect(DefectPresentation.NextStepFor(p, DefectStateMachine.GetState(p), all).Contains("Rework'te"), "Ekran metni beklemeyi söylemeli");
        var html = ReportBuilder.Build(ReportSession, all, 1);
        Expect(!html.Contains("Takip") && !html.Contains("Defect 2") && !html.Contains("Devredildi") && !html.Contains("Continues"),
            "Rapor void child'a bağlantı vermemeli");
        Expect(!SessionGate.CanComplete(all), "Parent tekrar beklediği için muayene kapanmamalı");

        // Yeniden muayene yeni bir child ile yapılabilir; eski (geri alınmış) karar yeni cümleye karışmaz.
        store.ApplyReinspect(folder, 1, NewOnly(("Nick", null)), "CTP");
        var again = store.LoadDefects(folder);
        Expect(DefectSentence.Describe(again.First(d => d.Id == 1), again).Contains("Continues as Defect 3."), "Yeni child #3 anılmalı");
        Expect(!DefectSentence.Describe(again.First(d => d.Id == 1), again).Contains("2"), "Void #2 anılmamalı");
    });

    public static void VoidOneOfManyChildrenKeepsParentTransferred() => With((store, folder) =>
    {
        Reworked(store, folder, 0);
        store.ApplyReinspect(folder, 1, NewOnly(("Scratch", null), ("Nick", null)), "CTP");

        Expect(store.VoidDefect(folder, 2, "A. Yılmaz", "yanlış") is null, "Aktif child kaldı: parent geri açılmamalı");
        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.Superseded, "Parent devredilmiş kalmalı");
        Expect(DefectSentence.Describe(p, all).Contains("Continues as Defect 3.") && !DefectSentence.Describe(p, all).Contains("2"),
            DefectSentence.Describe(p, all));
        Expect(DefectPresentation.NextStepFor(p, DefectState.Superseded, all) == "Takip #3 numaralı kusur üzerinden yapılıyor.",
            DefectPresentation.NextStepFor(p, DefectState.Superseded, all));

        var html = ReportBuilder.Build(ReportSession, all, 1);
        Expect(html.Contains("Takip 3 numaralı kusur üzerinden") && !html.Contains("2, 3"), "Rapor yalnızca #3'ü anmalı");

        // Geçit: void child sayılmaz; kalan child çözülünce parent de çözülür.
        Expect(!SessionGate.CanComplete(all), "Kalan child açık");
        Accept(store, folder, 3);
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Void olmayan child kapanınca muayene kapanabilmeli");

        // Kalan da (kararsızken) void edilseydi parent geri açılırdı: ayrı oturumda.
    });

    public static void VoidLastOfManyChildrenReopensParent() => With((store, folder) =>
    {
        Reworked(store, folder, 0);
        store.ApplyReinspect(folder, 1, NewOnly(("Scratch", null), ("Nick", null)), "CTP");
        Expect(store.VoidDefect(folder, 3, "A", "yanlış") is null, "İlk void parent'ı açmaz");
        Expect(store.VoidDefect(folder, 2, "A", "yanlış") == 1, "Sonuncu void parent'ı açar");
        Expect(DefectStateMachine.GetState(Get(store, folder, 1)) == DefectState.AwaitingReinspect, "Parent tekrar bekler");
    });

    public static void VoidChildOfSameDefectDecisionKeepsParentOpen() => With((store, folder) =>
    {
        var parent = Reworked(store, folder, 2);
        store.ApplyReinspect(folder, 1,
            new ReinspectResult(true, MeasurementEditor.Snapshot(parent),
                new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Continues, [P2] = PhotoOutcome.NewDefect },
                new[] { new NewDefectSpec("Scratch", P2) }, null), "CTP");

        Expect(store.VoidDefect(folder, 2, "A", "yanlış") is null, "Parent'ın kararı yeni hata değil: geri açılmaz");
        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.Open, "Parent açık kalmalı");
        Expect(!DefectSentence.Describe(p, all).Contains("New defect"), "Void child cümlede olmamalı");
        Expect(DefectPresentation.RegionBadge(p, P2, all) == "Yeni kusur", $"Rozet: {DefectPresentation.RegionBadge(p, P2, all)}");
    });

    // ───────────────────────── Eski JSON uyumu ─────────────────────────

    public static void LegacySupersededByIdStillReadable() => With((store, folder) =>
    {
        var dir = Path.Combine(folder, "defects");
        Directory.CreateDirectory(dir);
        // Eski biçim: tek supersededById, kararda newDefectIds / photoOutcomes yok.
        File.WriteAllText(Path.Combine(dir, "1.json"),
            "{\"id\":1,\"defectType\":\"Undercut\",\"supersededById\":2,\"dispositions\":[" +
            "{\"seq\":1,\"decision\":\"Rework\",\"source\":\"Engineer\",\"targetOperation\":\"OP40\",\"snapshot\":{\"depth\":0.3}}," +
            "{\"seq\":2,\"decision\":\"AfterReworkNewDefect\",\"source\":\"Engineer\",\"decidedBy\":\"CTP\"}]}");
        File.WriteAllText(Path.Combine(dir, "2.json"), "{\"id\":2,\"defectType\":\"Dent\",\"supersedesId\":1}");

        var all = store.LoadDefects(folder);
        var p = all.First(d => d.Id == 1);
        Expect(DefectStateMachine.GetState(p) == DefectState.Superseded, "Eski kayıt aynen devredilmiş görünmeli");
        Expect(DefectRegions.ChildIds(p).SequenceEqual(new[] { 2 }), "Tek bağ [child] sayılmalı");
        Expect(p.Dispositions[1].PhotoOutcomes is null && p.Dispositions[1].NewDefectIds is null && all[1].FoundAtPhoto is null, "Yeni alanlar boş");
        Expect(DefectSentence.Describe(p, all).Contains("Continues as Defect 2."), DefectSentence.Describe(p, all));
        Expect(DefectPresentation.NextStepFor(p, DefectState.Superseded, all) == "Takip #2 numaralı kusur üzerinden yapılıyor.", "Ekran metni");
        Expect(ReportBuilder.Build(ReportSession, all, 1).Contains("Takip 2 numaralı kusur üzerinden yapılıyor."), "Rapor tekil bağlantı");
        Expect(!SessionGate.CanComplete(all), "Child açık");
        Accept(store, folder, 2);
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Child kapanınca çözülmeli");

        // Eski kayıtta child void edilirse parent'ın kararı sistem tarafından geri alınır, eski bağ temizlenir.
        File.WriteAllText(Path.Combine(dir, "2.json"), "{\"id\":2,\"defectType\":\"Dent\",\"supersedesId\":1}");
        Expect(store.VoidDefect(folder, 2, "A", "yanlış") == 1, "Eski kayıtta da parent geri açılmalı");
        var reopened = Get(store, folder, 1);
        Expect(DefectStateMachine.GetState(reopened) == DefectState.AwaitingReinspect && reopened.SupersededById is null, "Parent bekler, bağ çözülür");
    });

    public static void LegacyUndoClearsOldLink() => With((store, folder) =>
    {
        var dir = Path.Combine(folder, "defects");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "1.json"),
            "{\"id\":1,\"defectType\":\"Undercut\",\"supersededById\":2,\"dispositions\":[" +
            "{\"seq\":1,\"decision\":\"Rework\",\"source\":\"Engineer\",\"targetOperation\":\"OP40\"}," +
            "{\"seq\":2,\"decision\":\"AfterReworkNewDefect\",\"source\":\"Engineer\"}]}");
        File.WriteAllText(Path.Combine(dir, "2.json"), "{\"id\":2,\"defectType\":\"Dent\",\"supersedesId\":1}");

        var (undone, voided) = store.UndoLastDecision(folder, 1, "M", "yanlış devir");
        Expect(undone.Decision == Decision.AfterReworkNewDefect && voided.SequenceEqual(new[] { 2 }), "Eski devir geri alınmalı, child void");
        var p = Get(store, folder, 1);
        Expect(p.SupersededById is null && DefectStateMachine.GetState(p) == DefectState.AwaitingReinspect, "Bağ çözülmeli, parent beklemeli");
    });

    public static void NewFieldsRoundTripAndOldJsonLoads() => With((store, folder) =>
    {
        var parent = Reworked(store, folder, 2);
        store.ApplyReinspect(folder, 1,
            new ReinspectResult(false, null,
                new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Conforms, [P2] = PhotoOutcome.NewDefect },
                new[] { new NewDefectSpec("Scratch", P2) }, null), "CTP");

        var json = File.ReadAllText(Path.Combine(folder, "defects", "1.json"));
        Expect(json.Contains("\"photoOutcomes\"") && json.Contains("\"Conforms\"") && json.Contains("\"newDefectIds\""), "Yeni alanlar camelCase / enum adıyla yazılmalı");
        Expect(!json.Contains("\"supersededById\": 2"), "Eski tek bağ yazılmamalı");
        var child = Get(store, folder, 2);
        Expect(child.FoundAtPhoto == P2, "FoundAtPhoto yazılıp okunmalı");

        // Bölgesiz eski karar JSON'u: alanlar boş okunur.
        var old = JsonSerializer.Deserialize<Defect>("{\"id\":9,\"defectType\":\"Dent\",\"dispositions\":[{\"seq\":1,\"decision\":\"Accept\"}]}", SessionStore.JsonOptions)!;
        Expect(old.Dispositions[0].PhotoOutcomes is null && old.Dispositions[0].NewDefectIds is null && old.FoundAtPhoto is null, "Eski JSON aynen yüklenmeli");
        Expect(DefectRegions.ActivePhotos(old).Count == 0 && !DefectRegions.HasRegionData(old), "Bölge verisi yok");
    });

    // ───────────────────────── Bölge kuralları ─────────────────────────

    public static void RegionStatusFollowsEffectiveDecisionsAndReopen()
    {
        var d = new Defect { Id = 5, DefectType = "Dent" };
        d.Photos.AddRange(new[] { "5_photo_01.jpg", "5_photo_02.jpg" });
        DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M", targetOperation: "OP40",
            photoOutcomes: new Dictionary<string, PhotoOutcome> { [d.Photos[0]] = PhotoOutcome.Continues, [d.Photos[1]] = PhotoOutcome.Conforms });
        Expect(DefectRegions.Label(d, d.Photos[1]) == "5.2" && DefectRegions.Label(d, "x.jpg") == "", "Etiket");
        Expect(DefectRegions.ActivePhotos(d).SequenceEqual(new[] { d.Photos[0] }), "Karar kapsamındaki bölge aktif, uygun sayılan değil");

        // Geri alınan karar bölge sonucunu da geri alır.
        DefectStateMachine.Undo(d, "M", "yanlış");
        Expect(DefectRegions.ActivePhotos(d).Count == 2, "Undo sonrası iki bölge de aktif");

        // Yeniden açma bölgeleri sıfırlar.
        DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M",
            photoOutcomes: new Dictionary<string, PhotoOutcome> { [d.Photos[0]] = PhotoOutcome.Conforms, [d.Photos[1]] = PhotoOutcome.Conforms });
        Expect(DefectRegions.ActivePhotos(d).Count == 0, "Hepsi uygun");
        DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M", note: "yanlış kapatılmıştı");
        Expect(DefectRegions.ActivePhotos(d).Count == 2, "Yeniden açılan kusurda bölgeler tekrar aktif");

        // Yeni hata kararı yeni kusur numarası ister; numara yalnızca rework sonrası karara verilir.
        DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M", targetOperation: "OP40");
        var threw = false;
        try { DefectStateMachine.Apply(d, Decision.AfterReworkNewDefect, DecisionSource.Engineer, "M"); }
        catch (InvalidOperationException) { threw = true; }
        Expect(threw, "Numarasız yeni hata kararı reddedilmeli");
        threw = false;
        try { DefectStateMachine.Apply(d, Decision.Repair, DecisionSource.Engineer, "M", newDefectIds: new[] { 9 }); }
        catch (InvalidOperationException) { threw = true; }
        Expect(threw, "Rework sonrası olmayan kararda yeni kusur numarası reddedilmeli");
    }

    // ───────────────────────── Rapor ─────────────────────────

    public static void ReportShowsRegionLinesAndPluralLinks() => With((store, folder) =>
    {
        var parent = Reworked(store, folder, 3);
        store.ApplyReinspect(folder, 1,
            new ReinspectResult(true, MeasurementEditor.Snapshot(parent),
                new Dictionary<string, PhotoOutcome> { [P1] = PhotoOutcome.Conforms, [P2] = PhotoOutcome.Continues, [P3] = PhotoOutcome.NewDefect },
                new[] { new NewDefectSpec("Scratch", P3), new NewDefectSpec("Nick", P3) }, null), "CTP");

        var all = store.LoadDefects(folder);
        var html = ReportBuilder.Build(ReportSession, all, 1);
        Expect(html.Contains("Region 1.1 — Giderildi / Resolved (") && html.Contains("After rework: same defect continues)"), "1.1 giderildi satırı (tarih, karar)");
        Expect(html.Contains("Region 1.2 — Devam ediyor / Continues"), "1.2 devam ediyor");
        Expect(html.Contains("Region 1.3 — Yeni kusur / New defect #2, #3"), "1.3 yeni kusurlar");
        Expect(html.Contains("yeni kusurlar da bulundu: 2, 3 numaralı kusurlar ayrıca takip ediliyor."), "Açık parent çoğul bağlantı");
        Expect(html.Contains("1 numaralı kusurun 1.3 bölgesinde, rework sonrası muayenede bulundu."), "Child bölge bağlantısı");
        Expect(System.Text.RegularExpressions.Regex.Matches(html, "Region 1\\.\\d — ").Count == 3, "Her fotoğraf sayfasında kendi bölgesinin satırı");

        // Tek fotoğraflı kusurda bölge satırı gösterilmez.
        var single = new Defect { Id = 7, DefectType = "Nick" };
        single.Photos.Add("7_photo_01.jpg");
        DefectStateMachine.Apply(single, Decision.Accept, DecisionSource.Engineer, "M",
            photoOutcomes: new Dictionary<string, PhotoOutcome> { [single.Photos[0]] = PhotoOutcome.Continues });
        Expect(!ReportBuilder.Build(ReportSession, new[] { single }, 1).Contains("Region 7."), "Tek fotoğrafta bölge satırı yok");

        // Devredilen parent: çoğul "Takip" bağlantısı.
        With2(store2 =>
        {
            var (f2, _) = store2.OpenOrCreate(new Session { PartNumber = "PD1", OperationNumber = "OP1", SerialNumber = "S1", Inspector = "x", Project = "PD1" });
            var u = store2.AddDefect(f2, "Undercut");
            store2.UpdateDefect(f2, u.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M", targetOperation: "OP40"));
            store2.ApplyReinspect(f2, u.Id, NewOnly(("Dent", null), ("Nick", null)), "CTP");
            var html2 = ReportBuilder.Build(ReportSession, store2.LoadDefects(f2), 1);
            Expect(html2.Contains("yeni kusurlar oluştu. Takip 2, 3 numaralı kusurlar üzerinden yapılıyor."), "Devredilen parent çoğul bağlantı");
        });
    });

    private static void With2(Action<SessionStore> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { body(new SessionStore(root)); }
        finally { try { Directory.Delete(root, recursive: true); } catch (IOException) { } }
    }

    // ───────────────────────── İngilizce metinler, Çap ─────────────────────────

    public static void EnglishInspectorAndDiameterTexts()
    {
        var html = ReportBuilder.Build(ReportSession, new[] { new Defect { Id = 1, DefectType = "Braze Void", Radius = 2 } }, 1);
        Expect(!html.Contains("Muayeneci") && !html.Contains("Yarıçap"), "Rapor Muayeneci / Yarıçap demez");
        Expect(html.Contains("&#199;ap / Diameter"), "Rapor hücresi Çap / Diameter olmalı");

        var d = new Defect { Id = 1, DefectType = "Braze Void", Radius = 2 };
        Expect(DefectSentence.Describe(d).Contains("Ø 2 mm.") && !DefectSentence.Describe(d).Contains("Radius"), DefectSentence.Describe(d));
        Expect(DefectValidator.Validate(new Defect { DefectType = "Dent", Radius = -1 }).Any(e => e.StartsWith("Çap")), "Doğrulama mesajı Çap");
        Expect(MeasurementCompare.Lines(new MeasurementSnapshot(null, null, null, 2, null, null), new Defect { Radius = 3 }).Any(l => l.StartsWith("Çap: 2 → 3 mm")),
            "Karşılaştırma satırı Çap");
        // JSON alan adı değişmez: eski kayıt okunur.
        var old = JsonSerializer.Deserialize<Defect>("{\"id\":1,\"defectType\":\"Dent\",\"radius\":1.5}", SessionStore.JsonOptions)!;
        Expect(old.Radius == 1.5 && JsonSerializer.Serialize(old, SessionStore.JsonOptions).Contains("\"radius\": 1.5"), "JSON alan adı radius kalmalı");

        var csv = CsvExport.Build(new[] { new IndexRow("P", "O", "S", "Open", 0, 0, "", DateTime.Now, Array.Empty<IndexDefect>(), "A", DateTime.Now) });
        Expect(csv.StartsWith("Parça;Operasyon;Seri;Durum;Inspector;"), "CSV sütun başlığı Inspector");
    }

    // ───────────────────────── Saf form kuralları ─────────────────────────

    public static void ReinspectFormRegionRules()
    {
        RegionChoice R(string p, PhotoOutcome? o, params string?[] types) => new(p, o, types);

        // Seçimsiz bölge varsa kaydedilemez, özet boş.
        Expect(!ReinspectForm.CanSaveRegions(new[] { R(P1, PhotoOutcome.Conforms), R(P2, null) }, false, null, false), "Seçimsiz bölge");
        Expect(ReinspectForm.Summary(1, new[] { R(P1, PhotoOutcome.Conforms), R(P2, null) }) == "", "Eksik seçimde özet boş");

        // Hepsi giderildi → kapanır.
        var allOk = new[] { R(P1, PhotoOutcome.Conforms), R(P2, PhotoOutcome.Conforms) };
        Expect(ReinspectForm.CanSaveRegions(allOk, true, null, false), "Devam eden yok: ölçüm / high metal sorulmaz");
        Expect(ReinspectForm.Summary(3, allOk) == "#3 kapanır" && ReinspectForm.OutcomeFor(allOk) == ReinspectOutcome.Conforms, "Kapanır özeti");

        // Devam eden + yeni hatalar → açık kalır, ölçüm gerekir.
        var mixed = new[] { R(P1, PhotoOutcome.Conforms), R(P2, PhotoOutcome.Continues), R(P3, PhotoOutcome.NewDefect, "Scratch", "Nick") };
        Expect(!ReinspectForm.CanSaveRegions(mixed, false, null, false), "Devam eden bölge: ölçüm gerekir");
        Expect(ReinspectForm.CanSaveRegions(mixed, false, null, true), "Ölçüm girilince kaydedilir");
        Expect(ReinspectForm.CanSaveRegions(mixed, true, false, false), "High metal cevabı yeter");
        Expect(ReinspectForm.Summary(3, mixed) == "#3 açık kalır; 2 yeni kusur açılır" && ReinspectForm.OutcomeFor(mixed) == ReinspectOutcome.Same, ReinspectForm.Summary(3, mixed));

        // Yalnız yeni hata → devredilir; tipi seçilmemiş satır kaydı engeller.
        var onlyNew = new[] { R(P1, PhotoOutcome.Conforms), R(P2, PhotoOutcome.NewDefect, "Scratch") };
        Expect(ReinspectForm.Summary(3, onlyNew) == "#3 yeni kusurlara devredilir" && ReinspectForm.CanSaveRegions(onlyNew, false, null, false), "Devredilir");
        Expect(!ReinspectForm.CanSaveRegions(new[] { R(P1, PhotoOutcome.NewDefect, new string?[] { null }) }, false, null, false), "Tipsiz satır");
        Expect(!ReinspectForm.CanSaveRegions(new[] { R(P1, PhotoOutcome.NewDefect) }, false, null, false), "Satırsız yeni hata");

        // Kararda bölge sorusu: 2+ aktif ve Void / Reopen dışında.
        Expect(DecisionForm.AsksRegions(Decision.Rework, 2) && !DecisionForm.AsksRegions(Decision.Rework, 1)
               && !DecisionForm.AsksRegions(Decision.Reopen, 3) && !DecisionForm.AsksRegions(Decision.Void, 3), "Bölge sorusu kuralı");
        Expect(DecisionForm.CanSave(false, null, false, null, true, 1) && !DecisionForm.CanSave(false, null, false, null, true, 0), "En az bir bölge işaretli");
        var map = DecisionForm.RegionOutcomes(new[] { P1, P2, P3 }, new[] { P2 });
        Expect(map[P1] == PhotoOutcome.Conforms && map[P2] == PhotoOutcome.Continues && map[P3] == PhotoOutcome.Conforms, "İşaretsiz bölge uygun sayılır");
    }

    // ───────────────────────── Gizlenen öneriler (saf kural) ─────────────────────────

    public static void HiddenSuggestionSetRulesAndJson()
    {
        var set = new HiddenSuggestionSet();
        Expect(set.Hide(SuggestLevel.Part, " PD170 ") && !set.Hide(SuggestLevel.Part, "pd170") && !set.Hide(SuggestLevel.Serial, "  "), "Büyük/küçük harf duyarsız, boş gizlenmez");
        Expect(set.Get(SuggestLevel.Part).Contains("PD170") && set.Get(SuggestLevel.Part).Contains("pd170") && set.Get(SuggestLevel.Operation).Count == 0,
            "Yalnızca o kutuyu etkiler");

        set.Hide(SuggestLevel.Operation, "OP40");
        set.Hide(SuggestLevel.Serial, "SN1");
        var json = set.ToJson();
        Expect(json.Contains("\"part\"") && json.Contains("\"operation\"") && json.Contains("\"serial\""), "Dosya biçimi: part / operation / serial");
        var back = HiddenSuggestionSet.FromJson(json);
        Expect(back.Get(SuggestLevel.Part).Contains("PD170") && back.Get(SuggestLevel.Serial).Contains("SN1"), "Gidiş dönüş");

        Expect(HiddenSuggestionSet.FromJson("{bozuk").Get(SuggestLevel.Part).Count == 0, "Bozuk dosya: hiçbiri gizli değil");
        Expect(HiddenSuggestionSet.FromJson("[1,2]").Get(SuggestLevel.Part).Count == 0 && HiddenSuggestionSet.FromJson(null).Get(SuggestLevel.Part).Count == 0, "Beklenmedik içerik");
        Expect(HiddenSuggestionSet.FromJson("{\"part\":[1,\"A\",null]}").Get(SuggestLevel.Part).SetEquals(new[] { "A" }), "Metin olmayan öğeler atlanır");

        Expect(set.Unhide("pd170", "x", "x") && !set.Get(SuggestLevel.Part).Contains("PD170") && set.Get(SuggestLevel.Operation).Contains("OP40"), "Yalnızca verilen değer geri gelir");
    }
}
