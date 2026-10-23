using MuayeneKayit.Core;

// Bağımlılıksız test koşucusu. Dış test paketi kullanılmıyor (güncelleme riski yok).
var tests = new List<(string Name, Action Body)>
{
    ("Yol doğrulama: güvenli segmentler kabul edilir", SafePathAcceptsValid),
    ("Yol doğrulama: dolaşım ve ayraç reddedilir", SafePathRejectsTraversal),
    ("Durum: Rework -> Reinspect -> Accept kapatır", StateReworkReinspectAccept),
    ("Durum: Void yalnızca operatör ve yalnızca Open durumda", StateVoidRules),
    ("Durum: MRB reddi otomatik hurda yazar", StateMrbRejectedScraps),
    ("Durum: Vendor'da kusur açık kalır", StateVendorIsOpen),
    ("Durum: Yetkisiz kaynak reddedilir", StateSourceRules),
    ("Geçit: Açık kusur muayeneyi kapatmaz", GateBlocksOpen),
    ("Geçit: Parent-child zinciri çözülünce geçer", GateSupersedeResolves),
    ("Cümle: Defect cümlesi doğru biçimde üretilir", SentenceFormat),
    ("Depo: Numara void olsa da yeniden kullanılmaz", StoreIdsNeverReused),
    ("Depo: Oturum var ise üzerine yazılmaz", StoreOpenOrCreateKeepsExisting),
    ("Index: Rebuild satırları üretir", IndexRebuildsRows),
    ("Denetim: Zincir doğrulanır ve kurcalama yakalanır", AuditChainDetectsTamper),
    ("Doğrulama: Ölçüm değerleri pozitif ve açı aralığında", ValidatorRules),
    ("Fotoğraf: Geçerli JPG kopyalanır, sahte uzantı reddedilir", PhotoImportRules),
    ("Yeniden açma: not zorunlu, geçmiş korunur", ReopenRules),
    ("Rework döngüsü: aynı kusur devam ederse tekrar rework", SameDefectLoops),
    ("Karşılaştırma: artış ve azalış doğru yazılır", CompareDirections),
    ("Void: karar geçmişi olan kusur void edilemez", VoidOnlyWithoutHistory),
    ("Void: gerekçe notu zorunlu", VoidNeedsNote),
    ("Ölçüm düzeltmesi: önce/sonra kaydı tutulur, değişmezse yazılmaz", MeasurementEditsLogged),
    ("Dashboard: parça, seri, hata tipi ve duruma göre süzme", DashboardFilter),
    ("SENARYO 1: Rework sonrası aynı kusur, değer artar, ikinci rework uygun", Scenario1_SameDefectThenConforms),
    ("SENARYO 2: Undercut giderildi, Dent oluştu (child)", Scenario2_NewDefectChild),
    ("SENARYO 3: Kapalı kusur yeniden açılır, hurda açılamaz", Scenario3_ReopenAndScrapFinal),
    ("SENARYO 4: MRB reddi otomatik hurda, muayene kapanabilir", Scenario4_MrbRejected),
    ("Yeniden açma: hurda kesindir", ScrapIsFinal),
    ("Child: Rework sonrası devam yeni kusur açar ve bağlar", ContinueAsChildLinks),
    ("Rapor: Kullanıcı metni HTML olarak kaçılır, void dışarıda kalır", ReportEscapesAndHidesVoid),
    ("İşaretleme: koordinat ve kusur no doğrulanır", MarkupValidation),
    ("İşaretleme: rapor kutu/ok/daire ve #no çizer, temiz kopyayı kullanır", ReportDrawsMarkup),
    ("Arka plan: parça ayrılır, zemin silinir", BackgroundSeparatesPart),
    ("Arka plan: temiz kopya orijinali ve numaralamayı bozmaz", CleanedCopyKeepsOriginal),
    ("Rapor: son sürüm numarası bulunur", ReportVersionRules),
    ("Admin: kusur tipi şablonu kaydedilir, bozuksa varsayılan", DefectTypeTemplates),
    ("Admin: PIN özetle saklanır ve doğrulanır", AdminPinRules),
    ("Admin: eski olmayan ya da kök dışı kilit silinmez", StaleLockRules),
    ("CSV: formül enjeksiyonu etkisizleştirilir", CsvEscapes),
    ("Geri al: son karar geri alınır, kayıt silinmez", UndoRestoresPreviousState),
    ("Geri al: MRB reddi otomatik hurdayla birlikte geri alınır", UndoMrbRejectedRemovesAutoScrap),
    ("Geri al: child devri geri alınır, child void olur", UndoChildLinkVoidsChild),
    ("Admin: karar açıklamaları kaydedilir, eksik olan varsayılan", DecisionTextRules),
};

int failed = 0;
foreach (var (name, body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"  OK    {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"  FAIL  {name}\n        {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? $"Tümü geçti ({tests.Count}/{tests.Count})." : $"{failed} test başarısız.");
return failed == 0 ? 0 : 1;

// ───────────────────────── Yardımcılar ─────────────────────────

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static Defect NewDefect(int id = 1, string type = "Dent") => new() { Id = id, DefectType = type };

static string TempRoot()
{
    var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    return root;
}

// ───────────────────────── Yol ─────────────────────────

static void SafePathAcceptsValid()
{
    Expect(SafePath.ValidateSegment("PD170", "x") == "PD170", "PD170 kabul edilmeli");
    Expect(SafePath.ValidateSegment("SN-001_A", "x") == "SN-001_A", "SN-001_A kabul edilmeli");
    Expect(SafePath.ValidateSegment("  OP 40 ", "x") == "OP 40", "Baştaki/sondaki boşluk kırpılmalı");
}

static void SafePathRejectsTraversal()
{
    foreach (var bad in new[] { "..", "a..b", @"..\x", "a/b", @"a\b", "x.", "", "   ", "C:" })
    {
        var rejected = false;
        try { SafePath.ValidateSegment(bad, "x"); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, $"'{bad}' reddedilmeli");
    }
}

// ───────────────────────── Durum makinesi ─────────────────────────

static void StateReworkReinspectAccept()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası muayene bekleme durumunda olmalı");


    DefectStateMachine.Apply(d, Decision.AfterReworkConforms, DecisionSource.Engineer, "M. Kaya", reference: "KAB-1");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "Rework sonrası uygun kapatmalı");
    Expect(d.Dispositions[^1].Snapshot is not null, "Rework sonrası karar ölçüm anlık görüntüsü almalı");
}

static void StateVoidRules()
{
    var d = NewDefect();
    var (engineerOk, _) = DefectStateMachine.Check(d, Decision.Void, DecisionSource.Engineer);
    Expect(!engineerOk, "Mühendis void edememeli");

    DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış giriş");
    Expect(DefectStateMachine.GetState(d) == DefectState.Voided, "Void sonrası Voided olmalı");

    var d2 = NewDefect(2);
    DefectStateMachine.Apply(d2, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    var (reworkOk, _) = DefectStateMachine.Check(d2, Decision.Void, DecisionSource.Operator);
    Expect(!reworkOk, "Rework bekleme durumunda void yapılamamalı");
}

static void StateMrbRejectedScraps()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.MrbPending, "SubmitMrb sonrası MrbPending olmalı");

    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB", reference: "MRB-1");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "MRB reddi kapatmalı");
    Expect(d.Dispositions[^1].Decision == Decision.Scrap, "MRB reddi otomatik SCRAP yazmalı");
    Expect(d.Dispositions[^1].Source == DecisionSource.System, "Otomatik hurda System kaynaklı olmalı");
}

static void StateVendorIsOpen()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.ReturnToVendor, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.WaitingVendor, "RTV sonrası WaitingVendor olmalı");
    Expect(DefectStateMachine.IsOpen(DefectStateMachine.GetState(d)), "Vendor'daki kusur açık sayılmalı");

    DefectStateMachine.Apply(d, Decision.Reinspect, DecisionSource.Engineer, "M. Kaya");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Vendor dönüşü reinspect'e gitmeli");
}

static void StateSourceRules()
{
    var d = NewDefect();
    var (mrbOk, _) = DefectStateMachine.Check(d, Decision.MrbAccepted, DecisionSource.Engineer);
    Expect(!mrbOk, "Mühendis MRB kararı veremez");

    var (ctpOk, _) = DefectStateMachine.Check(d, Decision.Reinspect, DecisionSource.Ctp);
    Expect(ctpOk, "CTP, Open durumundaki kusura reinspect verebilmeli");

    var (operatorOk, _) = DefectStateMachine.Check(d, Decision.Reinspect, DecisionSource.Operator);
    Expect(!operatorOk, "Operatör reinspect kararı veremez");
}

// ───────────────────────── Geçit ─────────────────────────

static void GateBlocksOpen()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(!SessionGate.CanComplete(new[] { d }), "Açık kusur muayeneyi kapatmamalı");
}

static void GateSupersedeResolves()
{
    var parent = NewDefect(1);
    DefectStateMachine.Apply(parent, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");


    var child = NewDefect(5, "Scratch");
    DefectStateMachine.Supersede(parent, child);
    Expect(DefectStateMachine.GetState(parent) == DefectState.Superseded, "Parent Superseded olmalı");
    Expect(!SessionGate.CanComplete(new[] { parent, child }), "Child açıkken geçit kapalı olmalı");

    DefectStateMachine.Apply(child, Decision.Accept, DecisionSource.Engineer, "M. Kaya", reference: "KAB-3");
    Expect(SessionGate.CanComplete(new[] { parent, child }), "Child kapanınca parent de çözülmüş sayılmalı");

    // MRB akışı ayrıca doğrulanır: önce SubmitMrb, sonra MrbAccepted.
    var mrbChild = NewDefect(6, "Nick");
    DefectStateMachine.Apply(mrbChild, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    DefectStateMachine.Apply(mrbChild, Decision.MrbAccepted, DecisionSource.Mrb, "MRB", reference: "MRB-2");
    Expect(DefectStateMachine.GetState(mrbChild) == DefectState.Closed, "MRB kabulü kapatmalı");
}

// ───────────────────────── Cümle ─────────────────────────

static void SentenceFormat()
{
    var d = NewDefect(3, "Dent");
    d.Depth = 0.12;
    d.Width = 2.4;
    d.SpecName = "SPEC-DNT-014";
    d.AcceptanceReference = "KAB-2026-0042";
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");

    var text = DefectSentence.Describe(d);
    Expect(text.StartsWith("Defect 3: There is Dent on part with Depth: 0.12 mm, Width: 2.4 mm."),
        $"Başlık beklenenden farklı: {text}");
    Expect(text.Contains("Disposition: Rework / Reinspect."), "Disposition satırı eksik");
    Expect(text.Contains("Acceptance: KAB-2026-0042."), "Kabul numarası eksik");
}

// ───────────────────────── Depo (geçici klasörde) ─────────────────────────

static Session SampleSession(string serial = "SN001") => new()
{
    PartNumber = "PD170",
    OperationNumber = "OP40",
    SerialNumber = serial,
    Inspector = "A. Yılmaz",
    Project = "PD170",
};

static void StoreIdsNeverReused()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession());

        var first = store.AddDefect(folder, "Dent");
        var second = store.AddDefect(folder, "Nick");
        Expect(first.Id == 1 && second.Id == 2, "İlk iki numara 1 ve 2 olmalı");

        store.UpdateDefect(folder, second.Id, d =>
            DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış"));

        var third = store.AddDefect(folder, "Scratch");
        Expect(third.Id == 3, $"Void edilen numara yeniden kullanılmamalı, gelen: {third.Id}");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void StoreOpenOrCreateKeepsExisting()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession());
        store.AddDefect(folder, "Dent");

        var (_, again) = store.OpenOrCreate(SampleSession());
        Expect(again.Inspector == "A. Yılmaz", "Mevcut oturum döndürülmeli");
        Expect(store.LoadDefects(folder).Count == 1, "Mevcut kusurlar korunmalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void IndexRebuildsRows()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN777"));
        store.AddDefect(folder, "Dent", d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));

        var rows = IndexBuilder.Rebuild(store);
        Expect(rows.Count == 1, $"Bir satır beklenirdi, gelen: {rows.Count}");
        Expect(rows[0].OpenDefects == 1, "Açık kusur sayısı 1 olmalı");

        var read = IndexBuilder.Read(store);
        Expect(IndexBuilder.ForSerial(read, "sn777").Count() == 1, "Seri arama büyük/küçük harf duyarsız olmalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void AuditChainDetectsTamper()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        AuditLog.Append(store, "A. Yılmaz", "defect.add", "#1 Dent");
        AuditLog.Append(store, "M. Kaya", "disposition", "#1 Rework");
        AuditLog.Append(store, "M. Kaya", "disposition", "#1 Reinspect");

        Expect(AuditLog.Verify(store).Ok, "Sağlam zincir doğrulanmalı");

        var path = Path.Combine(root, AuditLog.FileName);
        var lines = File.ReadAllLines(path);
        lines[1] = lines[1].Replace("Rework", "Accept");
        File.WriteAllLines(path, lines);

        var (ok, badLine) = AuditLog.Verify(store);
        Expect(!ok && badLine == 2, $"Kurcalanmış satır 2'de yakalanmalı, gelen: {badLine}");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void ValidatorRules()
{
    var ok = new Defect { DefectType = "Dent", Depth = 0.12, Width = 2.4, AngleDeg = 45, Color = "gri" };
    Expect(DefectValidator.Validate(ok).Count == 0, "Geçerli ölçümler hata üretmemeli");

    var bad = new Defect { DefectType = "Dent", Depth = -1, AngleDeg = 400 };
    var errors = DefectValidator.Validate(bad);
    Expect(errors.Any(e => e.Contains("Derinlik")), "Negatif derinlik reddedilmeli");
    Expect(errors.Any(e => e.Contains("Açı")), "360 üstü açı reddedilmeli");

    var empty = new Defect { DefectType = "  " };
    Expect(DefectValidator.Validate(empty).Any(e => e.Contains("Hata tipi")), "Boş hata tipi reddedilmeli");
}

static void PhotoImportRules()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN900"));

        // Geçerli JPEG başlığı (FF D8 FF) ile sahte bir içerik üret.
        var jpg = Path.Combine(root, "ornek.jpg");
        File.WriteAllBytes(jpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 });

        var name = PhotoImporter.Import(folder, 1, jpg);
        Expect(name == "1_photo_01.jpg", $"Dosya adı beklenenden farklı: {name}");
        Expect(File.Exists(Path.Combine(folder, "defects", name)), "Fotoğraf kusur klasörüne kopyalanmalı");

        var name2 = PhotoImporter.Import(folder, 1, jpg);
        Expect(name2 == "1_photo_02.jpg", "İkinci fotoğraf sıradaki numarayı almalı");

        // Uzantısı .jpg ama içeriği metin: reddedilmeli.
        var fake = Path.Combine(root, "sahte.jpg");
        File.WriteAllText(fake, "bu bir resim degil");
        var rejected = false;
        try { PhotoImporter.Import(folder, 1, fake); }
        catch (InvalidDataException) { rejected = true; }
        Expect(rejected, "İçeriği görüntü olmayan dosya reddedilmeli");

        // Desteklenmeyen uzantı.
        var exe = Path.Combine(root, "calistir.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
        var rejectedExt = false;
        try { PhotoImporter.Import(folder, 1, exe); }
        catch (InvalidDataException) { rejectedExt = true; }
        Expect(rejectedExt, ".exe dosyası reddedilmeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}


static void SameDefectLoops()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    d.Depth = 0.20;
    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "M. Kaya", note: "Ölçüm değişti, hâlâ var");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Aynı kusur devam ederse Open'a dönmeli");
    Expect(d.Dispositions[^1].Snapshot?.Depth == 0.20, "Ölçüm değişikliği anlık görüntüde kalmalı");
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Tekrar rework yapılabilmeli");
}

static void ReopenRules()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
    var threw = false;
    try { DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: " "); }
    catch (InvalidOperationException) { threw = true; }
    Expect(threw, "Notsuz yeniden açma reddedilmeli");

    DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: "Ölçüm yanlış girilmişti");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Yeniden açılan kusur Open olmalı");
    Expect(d.Dispositions.Count == 2, "Geçmiş silinmeden yeni kayıt eklenmeli");
    Expect(d.Dispositions[0].Decision == Decision.Accept, "Eski kabul kaydı korunmalı");
}

static void ScrapIsFinal()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya");
    var (ok, _) = DefectStateMachine.Check(d, Decision.Reopen, DecisionSource.Engineer);
    Expect(!ok, "Hurda kararı yeniden açılamamalı");
}

static void ContinueAsChildLinks()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN555"));
        var parent = store.AddDefect(folder, "Dent");
        store.UpdateDefect(folder, parent.Id, d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));


        var child = store.ContinueAsChild(folder, parent.Id, "Scratch", DecisionSource.Ctp, "CTP", "Yeni çizik bulundu", null);
        Expect(child.Id == 2, $"Child yeni numara almalı, gelen: {child.Id}");

        var reloaded = store.LoadDefects(folder);
        var p = reloaded.First(d => d.Id == parent.Id);
        var c = reloaded.First(d => d.Id == child.Id);
        Expect(p.SupersededById == child.Id, "Parent child'a bağlanmalı");
        Expect(c.SupersedesId == parent.Id, "Child parent'a bağlanmalı");
        Expect(p.Dispositions[^1].Decision == Decision.AfterReworkNewDefect, "Parent devam kararını almalı");
        Expect(!SessionGate.CanComplete(reloaded), "Açık child varken muayene kapanmamalı");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void UndoRestoresPreviousState()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası bekleme");

    var noNote = false;
    try { DefectStateMachine.Undo(d, "M. Kaya", " "); }
    catch (InvalidOperationException) { noNote = true; }
    Expect(noNote, "Gerekçesiz geri alma reddedilmeli");

    var undone = DefectStateMachine.Undo(d, "M. Kaya", "Yanlış karar seçildi");
    Expect(undone.Decision == Decision.Rework, "Geri alınan karar Rework olmalı");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Kusur açık duruma dönmeli");
    Expect(d.Dispositions.Count == 2, "Kayıt silinmemeli, Undo satırı eklenmeli");
    Expect(DefectStateMachine.Check(d, Decision.Accept, DecisionSource.Engineer).Ok, "Yeni karar verilebilmeli");

    var none = false;
    try { DefectStateMachine.Undo(d, "M. Kaya", "tekrar"); }
    catch (InvalidOperationException) { none = true; }
    Expect(none, "Geri alınacak karar kalmamışsa hata vermeli");
}

static void UndoMrbRejectedRemovesAutoScrap()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB");
    Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "MRB reddi kapatır");

    DefectStateMachine.Undo(d, "MRB", "Yanlış tıklandı");
    Expect(DefectStateMachine.GetState(d) == DefectState.MrbPending, "Geri alınca MRB beklemeye dönmeli (otomatik hurda da geri alınır)");
    Expect(DefectStateMachine.UndoneSeqs(d).SetEquals(new[] { 2, 3 }), "MRB reddi ve otomatik hurda birlikte geri alınmalı");

    var html = ReportBuilder.Build(SampleSession("SN-U"), new[] { d }, version: 1);
    Expect(!html.Contains("MRB rejected") && !html.Contains("Geri alındı") && html.Contains("Submit MRB"),
        "Raporda geri alınan kararlar görünmemeli, geçerli karar görünmeli");
    Expect(!html.Contains("Kusur özeti") && html.Contains("Sayfa 1 / 1"), "Özet sayfası olmamalı, sayfa sayısı kusur sayısı kadar");
}

static void UndoChildLinkVoidsChild()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-UC"));
        var parent = store.AddDefect(folder, "Undercut");
        store.UpdateDefect(folder, parent.Id, d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        var child = store.ContinueAsChild(folder, parent.Id, "Dent", DecisionSource.Engineer, "M. Kaya", null, null);

        // Child'a karar verilmişse önce o geri alınmalı.
        store.UpdateDefect(folder, child.Id, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya"));
        var blocked = false;
        try { store.UndoLastDecision(folder, parent.Id, "M. Kaya", "yanlış"); }
        catch (InvalidOperationException) { blocked = true; }
        Expect(blocked, "Child'ın kararı varken parent geri alınmamalı");

        store.UndoLastDecision(folder, child.Id, "M. Kaya", "yanlış kabul");
        var (undone, voided) = store.UndoLastDecision(folder, parent.Id, "M. Kaya", "Dent yanlış bağlandı");
        Expect(undone.Decision == Decision.AfterReworkNewDefect && voided == child.Id, "Devir geri alınmalı, child void edilmeli");

        var all = store.LoadDefects(folder);
        var p = all.First(x => x.Id == parent.Id);
        var c = all.First(x => x.Id == child.Id);
        Expect(p.SupersededById is null, "Parent bağı çözülmeli");
        Expect(DefectStateMachine.GetState(p) == DefectState.AwaitingReinspect, "Parent rework sonrası muayene beklemeye dönmeli");
        Expect(DefectStateMachine.GetState(c) == DefectState.Voided, "Child void olmalı (silinmez)");

        var html = ReportBuilder.Build(SampleSession("SN-UC"), all, 1);
        Expect(!html.Contains("Continues as Defect"), "Rapor eski bağı göstermemeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void DecisionTextRules()
{
    var root = TempRoot();
    try
    {
        Expect(SharedConfig.LoadDecisionTexts(root)[Decision.Accept] == SharedConfig.DefaultDecisionTexts[Decision.Accept], "Dosya yoksa varsayılan");

        SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Rework] = "  Parça rework hattına gider.  " });
        var loaded = SharedConfig.LoadDecisionTexts(root);
        Expect(loaded[Decision.Rework] == "Parça rework hattına gider.", "Düzenlenen metin okunmalı (kırpılmış)");
        Expect(loaded[Decision.Scrap] == SharedConfig.DefaultDecisionTexts[Decision.Scrap], "Düzenlenmeyen karar varsayılan kalmalı");

        var rejected = false;
        try { SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Accept] = new string('x', 700) }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Çok uzun metin reddedilmeli");

        rejected = false;
        try { SharedConfig.SaveDecisionTexts(root, new Dictionary<Decision, string> { [Decision.Undo] = "x" }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Listede olmayan karar için metin reddedilmeli");
    }
    finally { Directory.Delete(root, true); }
}

static void ReportEscapesAndHidesVoid()
{
    var session = SampleSession("SN<script>");
    var visible = NewDefect(1, "Dent<img onerror=x>");
    visible.Notes = "<b>not</b>";
    var voided = NewDefect(2, "Nick");
    DefectStateMachine.Apply(voided, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış");

    var html = ReportBuilder.Build(session, new[] { visible, voided }, version: 1);
    Expect(!html.Contains("<script>"), "Seri numarası HTML olarak çıkmamalı");
    Expect(!html.Contains("<img onerror"), "Hata tipi HTML olarak çıkmamalı");
    Expect(html.Contains("&lt;b&gt;not&lt;/b&gt;"), "Notlar kaçılmalı");
    Expect(!html.Contains("Defect 2") && !html.Contains("#2 "), "Void kayıt rapora girmemeli");
    Expect(html.Contains("@page { size: A4 landscape"), "Rapor yatay A4 olmalı");
}

// ───────────────────────── İşaretleme, arka plan, tarama, admin ─────────────────────────

static void MarkupValidation()
{
    var ok = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80 };
    ok.Shapes.Add(new Annotation { Kind = AnnotationKind.Box, X1 = 0.1, Y1 = 0.1, X2 = 0.5, Y2 = 0.6, DefectId = 1 });
    Expect(PhotoMarkupValidator.Validate(ok).Count == 0, "Geçerli işaret kabul edilmeli");

    var outside = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80 };
    outside.Shapes.Add(new Annotation { Kind = AnnotationKind.Arrow, X1 = -0.1, Y1 = 0, X2 = 1.4, Y2 = 1, DefectId = 1 });
    Expect(PhotoMarkupValidator.Validate(outside).Count > 0, "Görüntü dışı koordinat reddedilmeli");

    var badFile = new PhotoMarkup { ImageWidth = 100, ImageHeight = 80, CleanedFile = "..\\..\\session.json" };
    Expect(PhotoMarkupValidator.Validate(badFile).Count > 0, "Temiz kopya adı yol içeremez");
}

static void ReportDrawsMarkup()
{
    var d = NewDefect(3, "Dent");
    d.Photos.Add("3_photo_01.jpg");
    var markup = new PhotoMarkup { ImageWidth = 1920, ImageHeight = 1080, CleanedFile = "3_photo_01_clean.jpg" };
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Box, X1 = 0.2, Y1 = 0.2, X2 = 0.4, Y2 = 0.5, DefectId = 3 });
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Arrow, X1 = 0.8, Y1 = 0.8, X2 = 0.5, Y2 = 0.5, DefectId = 1 });
    markup.Shapes.Add(new Annotation { Kind = AnnotationKind.Circle, X1 = 0.6, Y1 = 0.1, X2 = 0.7, Y2 = 0.3, DefectId = 3 });
    d.Markups["3_photo_01.jpg"] = markup;

    var html = ReportBuilder.Build(SampleSession("SN-M"), new[] { d }, version: 1);
    Expect(html.Contains("viewBox=\"0 0 1920 1080\""), "SVG görüntü boyutunu kullanmalı");
    Expect(html.Contains("<rect x=\"384\" y=\"216\""), "Kutu piksel koordinatına çevrilmeli");
    Expect(html.Contains("<ellipse") && html.Contains("marker-end"), "Daire ve ok çizilmeli");
    Expect(html.Contains(">#3</text>") && html.Contains(">#1</text>"), "Kusur numarası etiketi yazılmalı");
    Expect(html.Contains("href=\"../defects/3_photo_01_clean.jpg\""), "Arka planı kaldırılmış kopya kullanılmalı");
}

static void BackgroundSeparatesPart()
{
    const int w = 300, h = 200;
    var px = new byte[w * h * 4];
    var rng = new Random(7);
    for (var y = 0; y < h; y++)
    {
        for (var x = 0; x < w; x++)
        {
            var part = x >= 90 && x < 210 && y >= 50 && y < 150;
            var hole = x >= 140 && x < 160 && y >= 90 && y < 110;   // parçanın üstünde koyu leke, delik sayılmamalı
            var n = rng.Next(-12, 13);
            var (r, g, b) = part ? (hole ? (20, 20, 25) : (60, 70, 150)) : (215, 210, 200);
            var p = (y * w + x) * 4;
            px[p] = (byte)Math.Clamp(b + n, 0, 255);
            px[p + 1] = (byte)Math.Clamp(g + n, 0, 255);
            px[p + 2] = (byte)Math.Clamp(r + n, 0, 255);
            px[p + 3] = 255;
        }
    }

    var mask = BackgroundRemover.ComputeMask(px, w, h, new PixelRect(60, 30, 180, 140));
    Expect(mask[100 * w + 150 - 30], "Parça ortası ön plan olmalı");
    Expect(mask[100 * w + 150], "Parçadaki koyu leke ön planda kalmalı");
    Expect(!mask[35 * w + 65], "Dikdörtgen içindeki zemin arka plan olmalı");
    Expect(!mask[5 * w + 5], "Dikdörtgen dışı arka plan olmalı");

    var inside = 0;
    for (var y = 50; y < 150; y++) for (var x = 90; x < 210; x++) if (mask[y * w + x]) inside++;
    Expect(inside > 0.97 * 120 * 100, $"Parçanın en az %97'si ön plan olmalı ({inside})");

    var output = BackgroundRemover.Apply(px, mask);
    Expect(output[(5 * w + 5) * 4] == 255 && px[(5 * w + 5) * 4] != 255, "Zemin beyaza boyanmalı, girdi değişmemeli");
}

static void CleanedCopyKeepsOriginal()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-CL"));
        var jpg = Path.Combine(root, "a.jpg");
        File.WriteAllBytes(jpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 });
        var original = PhotoImporter.Import(folder, 1, jpg);

        var clean = PhotoImporter.WriteCleaned(folder, original, new byte[] { 0xFF, 0xD8, 0xFF, 9, 9 });
        Expect(clean == "1_photo_01_clean.jpg", $"Temiz kopya adı: {clean}");
        Expect(File.ReadAllBytes(Path.Combine(folder, "defects", original)).Length == 7, "Orijinal değişmemeli");
        Expect(PhotoImporter.Import(folder, 1, jpg) == "1_photo_02.jpg", "Temiz kopya numaralamaya sayılmamalı");

        var rejected = false;
        try { PhotoImporter.WriteCleaned(folder, "..\\x.jpg", new byte[] { 0xFF, 0xD8, 0xFF }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Geçersiz ad reddedilmeli");
    }
    finally { Directory.Delete(root, true); }
}

static void ReportVersionRules()
{
    var root = TempRoot();
    try
    {
        Expect(ReportFiles.LatestVersion(root) == 0, "Rapor yokken sürüm 0");
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v1.html"), "x");
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v2.html"), "x");
        File.WriteAllText(Path.Combine(root, "reports", "Rapor_v2.pdf"), "x");
        Expect(ReportFiles.LatestVersion(root) == 2, "Son sürüm 2 olmalı");
    }
    finally { Directory.Delete(root, true); }
}

static void DefectTypeTemplates()
{
    var root = TempRoot();
    try
    {
        Expect(SharedConfig.LoadDefectTypes(root).Count == SharedConfig.Defaults.Count, "Dosya yoksa varsayılanlar");

        var types = new List<DefectTypeTemplate>
        {
            new() { Name = "Çentik", Fields = { MeasureField.Depth }, SpecName = "SPA-12" },
            new() { Name = "Pitting", Fields = { MeasureField.Depth, MeasureField.Radius } },
        };
        SharedConfig.SaveDefectTypes(root, types);
        var loaded = SharedConfig.LoadDefectTypes(root);
        Expect(loaded.Count == 2 && loaded[0].Name == "Çentik" && loaded[0].SpecName == "SPA-12", "Şablon geri okunmalı");
        Expect(loaded[1].Fields.SequenceEqual(new[] { MeasureField.Depth, MeasureField.Radius }), "Alanlar korunmalı");

        var rejected = false;
        try { SharedConfig.SaveDefectTypes(root, new[] { new DefectTypeTemplate { Name = "<script>" } }); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Geçersiz ad reddedilmeli");

        var dup = SharedConfig.ValidateTypes(new[] { new DefectTypeTemplate { Name = "Dent" }, new DefectTypeTemplate { Name = "dent" } });
        Expect(dup.Count > 0, "Aynı ad iki kez olmamalı");

        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "defect_types.json"), "{bozuk");
        Expect(SharedConfig.LoadDefectTypes(root).Count == SharedConfig.Defaults.Count, "Bozuk dosyada varsayılanlar");
    }
    finally { Directory.Delete(root, true); }
}

static void AdminPinRules()
{
    var root = TempRoot();
    try
    {
        Expect(!SharedConfig.HasPin(root) && !SharedConfig.VerifyPin(root, "123456"), "PIN yokken giriş olmamalı");

        var rejected = false;
        try { SharedConfig.SetPin(root, "1234"); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "Kısa PIN reddedilmeli");

        SharedConfig.SetPin(root, "482913");
        Expect(SharedConfig.VerifyPin(root, "482913"), "Doğru PIN kabul edilmeli");
        Expect(!SharedConfig.VerifyPin(root, "482914"), "Yanlış PIN reddedilmeli");

        var raw = File.ReadAllText(Path.Combine(SharedConfig.Folder(root), "admin.json"));
        Expect(!raw.Contains("482913"), "PIN düz metin saklanmamalı");
    }
    finally { Directory.Delete(root, true); }
}

static void StaleLockRules()
{
    var root = TempRoot();
    var outside = Path.Combine(Path.GetTempPath(), "mk-outside-" + Guid.NewGuid().ToString("N") + ".lock");
    try
    {
        var fresh = Path.Combine(root, "A", "session.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(fresh)!);
        File.WriteAllText(fresh, "");
        var old = Path.Combine(root, "_index.lock");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(outside, "");
        File.SetLastWriteTimeUtc(outside, DateTime.UtcNow.AddHours(-1));

        var stale = SharedConfig.StaleLocks(root);
        Expect(stale.Count == 1 && stale[0] == old, "Yalnızca eski kilit listelenmeli");

        var blocked = false;
        try { SharedConfig.RemoveStaleLock(root, fresh); }
        catch (InvalidOperationException) { blocked = true; }
        Expect(blocked && File.Exists(fresh), "Yeni kilit silinmemeli");

        blocked = false;
        try { SharedConfig.RemoveStaleLock(root, outside); }
        catch (ArgumentException) { blocked = true; }
        Expect(blocked && File.Exists(outside), "Kök dışındaki dosya silinmemeli");

        SharedConfig.RemoveStaleLock(root, old);
        Expect(!File.Exists(old), "Eski kilit silinmeli");
    }
    finally
    {
        Directory.Delete(root, true);
        File.Delete(outside);
    }
}

static void CsvEscapes()
{
    var row = new IndexRow("=HYPERLINK(\"x\")", "OP;40", "SN-1", "Open", 1, 1, "", DateTime.Now,
        new[] { new IndexDefect(1, "+Dent", "Open", 0, "@SUM") }, "A. Yılmaz", DateTime.Now);
    var csv = CsvExport.Build(new[] { row });
    Expect(csv.Contains("\"'=HYPERLINK(\"\"x\"\")\""), "= ile başlayan hücre tırnaklanmalı");
    Expect(csv.Contains(";\"OP;40\";"), "Ayraç içeren hücre tırnak içine alınmalı");
    Expect(csv.Contains(";'+Dent;") && csv.Contains(";'@SUM"), "+ ve @ etkisizleşmeli");
    Expect(CsvExport.Cell("-5") == "'-5", "- ile başlayan hücre etkisizleşmeli");
}


// ───────────────────────── Senaryolar (adım adım izleme) ─────────────────────────

static void Trace(string step, Defect d)
{
    var last = d.Dispositions.LastOrDefault(x => x.Source != DecisionSource.System);
    var meas = $"D={d.Depth?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"} " +
               $"W={d.Width?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"} " +
               $"L={d.Length?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}";
    Console.WriteLine($"        · {step,-38} #{d.Id} {d.DefectType,-9} durum={DefectStateMachine.GetState(d),-18} " +
                      $"son karar={(last is null ? "-" : DefectSentence.DecisionLabel(last.Decision)),-36} {meas}");
}

static void CompareDirections()
{
    var before = new MeasurementSnapshot(0.12, 2.0, 3.0, null, null, "gri");
    var after = new Defect { Depth = 0.20, Width = 1.5, Length = 3.0, Color = "gri" };
    var lines = MeasurementCompare.Lines(before, after);
    Expect(lines.Any(l => l.Contains("Derinlik: 0.12 → 0.2 mm (artış)")), string.Join(" | ", lines));
    Expect(lines.Any(l => l.Contains("Genişlik: 2 → 1.5 mm (azalış)")), string.Join(" | ", lines));
    Expect(lines.Any(l => l.Contains("Uzunluk: 3 → 3 mm (aynı)")), string.Join(" | ", lines));
}

static void Scenario1_SameDefectThenConforms()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-S1"));

        var d = store.AddDefect(folder, "Dent", x => { x.Depth = 0.12; x.Width = 2.0; });
        Trace("1. Kusur girildi", d);

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        Trace("2. Rework kararı", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Rework sonrası muayene beklemeli");

        // Parça döndü: aynı kusur, derinlik artmış
        d = store.UpdateDefect(folder, d.Id, x => x.Depth = 0.20);
        var before = d.Dispositions.Last(x => x.Decision == Decision.Rework).Snapshot;
        Console.WriteLine("        · Karşılaştırma: " + string.Join(" | ", MeasurementCompare.Lines(before, d)));
        Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect, "Sadece ölçüm değişince kusur kapanmamalı");

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "CTP", "Derinlik arttı"));
        Trace("3. Reinspect: aynı kusur devam", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Aynı kusur Open'a dönmeli");

        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
        Trace("4. İkinci rework", d);

        d = store.UpdateDefect(folder, d.Id, x => { x.Depth = null; x.Width = null; });
        d = store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.AfterReworkConforms, DecisionSource.Engineer, "CTP"));
        Trace("5. Reinspect: uygun", d);
        Expect(DefectStateMachine.GetState(d) == DefectState.Closed, "Uygun kararı kapatmalı");
        Expect(d.Dispositions.Count == 4, $"4 karar kaydı beklenirdi, gelen: {d.Dispositions.Count}");
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Muayene kapanabilmeli");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void Scenario2_NewDefectChild()
{
    var root = TempRoot();
    try
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(SampleSession("SN-S2"));

        var undercut = store.AddDefect(folder, "Undercut", x => x.Depth = 0.3);
        Trace("1. Undercut girildi", undercut);
        undercut = store.UpdateDefect(folder, undercut.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP40"));
        Trace("2. Rework (OP40)", undercut);

        var dent = store.ContinueAsChild(folder, undercut.Id, "Dent", DecisionSource.Engineer, "CTP", "Undercut gitti, dent oluştu", null);
        var all = store.LoadDefects(folder);
        undercut = all.First(x => x.Id == undercut.Id);
        dent = all.First(x => x.Id == dent.Id);
        Trace("3. Reinspect: yeni hata", undercut);
        Trace("   Child açıldı", dent);

        Expect(dent.DefectType == "Dent", "Child tipi Dent olmalı");
        Expect(DefectStateMachine.GetState(undercut) == DefectState.Superseded, "Undercut devredilmiş olmalı");
        Expect(!SessionGate.CanComplete(all), "Child açıkken muayene kapanmamalı");

        dent = store.UpdateDefect(folder, dent.Id, x => DefectStateMachine.Apply(x, Decision.Accept, DecisionSource.Engineer, "M. Kaya", reference: "KAB-77"));
        Trace("4. Dent kabul", dent);
        Expect(SessionGate.CanComplete(store.LoadDefects(folder)), "Child kapanınca muayene kapanabilmeli");
        Console.WriteLine("        · Cümle: " + DefectSentence.Describe(store.LoadDefects(folder).First(x => x.Id == undercut.Id)).Replace("\n", " / "));
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void Scenario3_ReopenAndScrapFinal()
{
    var d = NewDefect(1, "Nick");
    DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
    Trace("1. Kabul", d);
    DefectStateMachine.Apply(d, Decision.Reopen, DecisionSource.Engineer, "M. Kaya", note: "Kabul kağıdı yanlış parçaya aitti");
    Trace("2. Yeniden açıldı (not ile)", d);
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Yeniden açma Open yapmalı");
    DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya");
    Trace("3. Hurda", d);
    Expect(!DefectStateMachine.Check(d, Decision.Reopen, DecisionSource.Engineer).Ok, "Hurda yeniden açılamamalı");
}

static void Scenario4_MrbRejected()
{
    var d = NewDefect(1, "Scratch");
    DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya");
    Trace("1. MRB'ye gönderildi", d);
    Expect(!SessionGate.CanComplete(new[] { d }), "MRB bekleyen kusur muayeneyi kapatmamalı");
    DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "MRB", reference: "MRB-9");
    Trace("2. MRB reddi", d);
    Expect(d.Dispositions[^1].Decision == Decision.Scrap, "Otomatik hurda yazılmalı");
    Expect(SessionGate.CanComplete(new[] { d }), "Hurdadan sonra muayene kapanabilmeli");
}


static void VoidOnlyWithoutHistory()
{
    var d = NewDefect();
    DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, "CTP");
    Expect(DefectStateMachine.GetState(d) == DefectState.Open, "Kusur Open olmalı");
    Expect(!DefectStateMachine.Check(d, Decision.Void, DecisionSource.Operator).Ok, "Geçmişi olan kusur void edilmemeli");
    Expect(DefectStateMachine.Check(NewDefect(2), Decision.Void, DecisionSource.Operator).Ok, "Yeni kusur void edilebilmeli");
}


static void VoidNeedsNote()
{
    var d = NewDefect();
    var threw = false;
    try { DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz"); }
    catch (InvalidOperationException) { threw = true; }
    Expect(threw, "Notsuz void reddedilmeli");
    DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış hata tipi");
    Expect(DefectStateMachine.GetState(d) == DefectState.Voided, "Notlu void kabul edilmeli");
}

static void MeasurementEditsLogged()
{
    var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.12 };
    var changed = MeasurementEditor.Apply(d, new MeasurementSnapshot(0.15, null, null, null, null, null), "A. Yılmaz");
    Expect(changed && d.Depth == 0.15, "Değer güncellenmeli");
    Expect(d.MeasurementEdits.Count == 1 && d.MeasurementEdits[0].Before.Depth == 0.12, "Önceki değer kayıtta olmalı");

    var again = MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d), "A. Yılmaz");
    Expect(!again && d.MeasurementEdits.Count == 1, "Değişiklik yoksa kayıt eklenmemeli");

    var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
    Expect(html.Contains("Ölçüm düzeltmeleri"), "Düzeltmeler raporda görünmeli");
}

static void DashboardFilter()
{
    IndexRow Row(string part, string op, string serial, int open, string status, params (string Type, string State)[] defects) =>
        new(part, op, serial, status, defects.Length, open, "", DateTime.Now,
            defects.Select((d, i) => new IndexDefect(i + 1, d.Type, d.State, 0)).ToList());

    var rows = new[]
    {
        Row("PD170", "OP40", "SN-A1", 1, "Open", ("Dent", "MrbPending")),
        Row("PD170", "OP50", "SN-A2", 0, "Completed", ("Scratch", "Closed")),
        Row("TS1400", "OP40", "SN-B1", 1, "Open", ("Undercut", "AwaitingReinspect")),
    };

    Expect(IndexFilter.Apply(rows, new IndexCriteria(Part: "pd170")).Count == 2, "Parça süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(Serial: "b1")).Count == 1, "Seri içerir araması");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(DefectType: "Undercut")).Single().Serial == "SN-B1", "Hata tipi süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(State: IndexStateFilter.MrbPending)).Single().Serial == "SN-A1", "MRB süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(State: IndexStateFilter.Completed)).Single().Serial == "SN-A2", "Tamamlanan süzgeci");
    Expect(IndexFilter.Apply(rows, new IndexCriteria(Operation: "OP40", State: IndexStateFilter.HasOpen)).Count == 2, "Birleşik süzgeç");
}
