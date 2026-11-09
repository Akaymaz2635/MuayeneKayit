using System.Globalization;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Fonksiyonel hata düzeltmelerinin (B-01..B-24, N-1, N-2) ek testleri: düzeltilen davranışın sınırlarını da sınar.
// Ana listeye girer (docs/test/FONKSIYONEL-TEST-RAPORU.md "Düzeltmeler").
public static partial class ViewModelTests
{
    private static string CatchMessage(Action a)
    {
        try { a(); return ""; }
        catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }

    // B-03: farklı alanlar iki kullanıcıdan da korunur
    public static void Fonk_DZ_B03_DifferentFieldsFromTwoUsersBothSurvive() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        a.Open();
        a.Vm.AddDefect("Dent");
        a.Vm.DepthText = "0.1";
        a.Vm.SaveMeasurements();
        b.Open();
        b.Vm.Select(1);

        a.Vm.DepthText = "0.5";
        a.Vm.SaveMeasurements();

        b.Vm.WidthText = "2";
        b.Vm.AutoSave();

        var d = a.Store.LoadDefects(a.Folder)[0];
        Check(d.Depth == 0.5 && d.Width == 2, $"D={d.Depth} W={d.Width}: A'nın D'si ve B'nin W'si birlikte kalmalı");
        Check(!b.Notes.Any(n => n.Kind == StatusKind.Warning), "çakışma yok, uyarı verilmemeli");
        Check(b.Vm.DepthText == "0.5", $"B'nin ekranı güncel değeri göstermeli: {b.Vm.DepthText}");
    });

    // B-03: aynı alanı iki kullanıcı da değiştirdiyse yazılmaz, uyarılır, ekran yenilenir
    public static void Fonk_DZ_B03_SameFieldConflictIsRefusedWithWarning() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        a.Open();
        a.Vm.AddDefect("Dent");
        a.Vm.DepthText = "0.1";
        a.Vm.SaveMeasurements();
        b.Open();
        b.Vm.Select(1);

        a.Vm.DepthText = "0.5";
        a.Vm.SaveMeasurements();

        b.Vm.DepthText = "0.9";
        b.Vm.NotesText = "B notu";
        b.Vm.AutoSave();

        var d = a.Store.LoadDefects(a.Folder)[0];
        Check(d.Depth == 0.5, $"çakışan alan ezilmemeli: D={d.Depth}");
        Check(d.Notes == "B notu", "çakışmayan alan (not) yine de yazılmalı");
        var warn = b.Notes.LastOrDefault(n => n.Kind == StatusKind.Warning);
        Check(warn.Message is not null && warn.Message.Contains("Derinlik") && warn.Message.Contains("başka bir kullanıcı"), $"uyarı: '{warn.Message}'");
        Check(b.Vm.DepthText == "0.5", $"form güncel değerle yenilenmeli: {b.Vm.DepthText}");
    });

    // B-01: renk / ek alan / sayı kutuları yalnızca kullanıcı değiştirirse yeniden ayrıştırılır
    public static void Fonk_DZ_B01_UntouchedBoxesKeepExactStoredValues() => WithRoot(root =>
    {
        var types = new[] { new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth, MeasureField.Color }, CustomFields = { CustomFieldTests.Height.Key } } };
        var c = NewCtx(root, types, new[] { CustomFieldTests.Height });
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.CustomInputs.Single().Text = "0.123456";
        c.Vm.DepthText = "0.125";
        c.Vm.ColorText = "gri";
        c.Vm.SaveMeasurements();
        Check(Load(c, 1).Custom?.Single().Number == 0.123456, "ön koşul: H 0.123456");

        c.Vm.NotesText = "yalnızca not";
        c.Vm.AutoSave();
        var d = Load(c, 1);
        Check(d.Custom?.Single().Number == 0.123456 && d.Depth == 0.125 && d.Color == "gri", $"yuvarlanmamalı: H={d.Custom?.Single().Number} D={d.Depth}");
        Check(d.MeasurementEdits.Count == 1, $"yalnızca ilk kayıt düzeltme geçmişinde olmalı: {d.MeasurementEdits.Count}");

        // Kullanıcı gerçekten değiştirirse yeni değer yazılır.
        c.Vm.DepthText = "0.2";
        c.Vm.AutoSave();
        Check(Load(c, 1).Depth == 0.2, "değiştirilen kutu yazılmalı");
    });

    // B-04: tamamlanmış oturum yeni kusur, karar, ölçüm ya da fotoğraf kabul etmez
    public static void Fonk_DZ_B04_CompletedSessionRejectsEveryWrite() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.1";
        c.Vm.SaveMeasurements();
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(StateOf(c, 1) == DefectState.Closed, "ön koşul: kusur kapanmalı");
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "ön koşul: tamamlanmalı");

        var msg = CatchMessage(() => c.Store.AddDefect(c.Folder, "Dent"));
        Check(msg.StartsWith("InvalidOperationException") && msg.Contains("tamamlandı"), $"AddDefect: {msg}");
        msg = CatchMessage(() => c.Store.UpdateDefect(c.Folder, 1, d => d.Notes = "x"));
        Check(msg.StartsWith("InvalidOperationException") && msg.Contains("tamamlandı"), $"UpdateDefect: {msg}");
        msg = CatchMessage(() => c.Store.VoidDefect(c.Folder, 1, "x", "y"));
        Check(msg.StartsWith("InvalidOperationException"), $"VoidDefect: {msg}");

        c.Notes.Clear();
        Check(!c.Vm.AddDefect("Nick") && LastIsError(c) && c.Notes.Last().Message.Contains("tamamlandı"), $"VM AddDefect: {c.Notes.LastOrDefault().Message}");

        c.Notes.Clear();
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, "neden", null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reopen)));
        Check(StateOf(c, 1) == DefectState.Closed && LastIsError(c), "yeniden açma reddedilmeli");

        c.Notes.Clear();
        c.Picker.Picks.Enqueue(PhotoFile("x"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        Check(LastIsError(c) && !Directory.GetFiles(DefectsDir(c), "*photo*").Any(), "fotoğraf eklenmemeli, dosya kopyalanmamalı");
    });

    // B-08: "_" ile başlayan parça / operasyon / seri reddedilir; içindeki "_" serbest
    public static void Fonk_DZ_B08_UnderscorePrefixRejectedMiddleAllowed() => With(c =>
    {
        foreach (var (part, op, serial, field) in new[]
                 {
                     ("_X", "OP40", "SN001", "Parça no"),
                     ("_config", "OP40", "SN001", "Parça no"),
                     ("PD170", "_OP", "SN001", "Operasyon no"),
                     ("PD170", "OP40", "_index", "Seri no"),
                 })
        {
            c.Notes.Clear();
            OpenAs(c, part, op, serial);
            Check(!c.Vm.IsSessionOpen && LastIsError(c) && c.Notes.Last().Message.Contains(field) && c.Notes.Last().Message.Contains("'_' ile başlayamaz"),
                $"{part}/{op}/{serial}: {c.Notes.LastOrDefault().Message}");
        }
        Check(!Directory.Exists(Path.Combine(c.Store.Root, "_config")), "_config klasörü oluşmamalı");

        OpenAs(c, "PD_170", "OP_40", "SN_001");
        Check(c.Vm.IsSessionOpen, "ortada alt çizgi serbest: " + c.Notes.Last().Message);
    });

    // B-13: ölçüm alanı olmayan tipte sıradaki adım fotoğraf; yalnız ek alanı olan tipte ölçüm
    public static void Fonk_DZ_B13_NextStepDependsOnTemplateFields() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Ölçümsüz");
        Check(c.Vm.NextStepText.Contains("fotoğraf") && c.Notes.Last().Message.Contains("Fotoğraf çekin"), $"'{c.Vm.NextStepText}' / '{c.Notes.Last().Message}'");
        Check(c.Vm.Defects[0].NextStep.Contains("fotoğraf"), "liste satırı da fotoğrafı göstermeli");

        c.Vm.AddDefect("Özel");
        Check(c.Vm.CurrentStep == InspectionStep.Measure && c.Vm.NextStepText.Contains("ölçümleri girin"), $"yalnız ek alanı olan tipte ölçüm: {c.Vm.CurrentStep}");
    }, new[]
    {
        new DefectTypeTemplate { Name = "Ölçümsüz" },
        new DefectTypeTemplate { Name = "Özel", CustomFields = { CustomFieldTests.Height.Key } },
    }, new[] { CustomFieldTests.Height });

    // B-12: kayıtlı değerin türü şimdiki tanımdan farklıysa kayıttaki tanım kullanılır
    public static void Fonk_DZ_B12_StoredKindWinsOverChangedDefinition() => WithRoot(root =>
    {
        var types = new[] { new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth }, CustomFields = { CustomFieldTests.Height.Key } } };
        var c1 = NewCtx(root, types, new[] { CustomFieldTests.Height });
        c1.Open();
        c1.Vm.AddDefect("Dent");
        c1.Vm.CustomInputs.Single().Text = "0.3";
        c1.Vm.SaveMeasurements();

        var asText = new CustomFieldDef { Key = CustomFieldTests.Height.Key, Name = "Yükseklik", Abbrev = "H", Kind = CustomFieldKind.Text };
        var c2 = NewCtx(root, types, new[] { asText });
        c2.Open();
        c2.Vm.Select(1);
        var input = c2.Vm.CustomInputs.Single();
        Check(input.Def.Kind == CustomFieldKind.Length && input.Text == "0.3" && input.IsNumeric, $"kutu kayıttaki tanımı kullanmalı: {input.Def.Kind} '{input.Text}'");

        input.Text = "0.4";
        c2.Vm.AutoSave();
        var m = c2.Store.LoadDefects(c2.Folder)[0].Custom!.Single();
        Check(m is { Kind: CustomFieldKind.Length, Number: 0.4 }, $"kullanıcı değiştirirse kayıttaki türle yazılır: {m.Kind} {m.Number}");
    });

    // B-20: rapor sonrası klasörde artık boş HTML yer tutucusu kalmaz
    public static void Fonk_DZ_B20_ReportLeavesOnlyPdf() => With(c =>
    {
        c.Open();
        Wait(c.Vm.CreateReportAsync());
        Wait(c.Vm.CreateReportAsync());
        var files = Directory.GetFiles(Path.Combine(c.Folder, "reports")).Select(Path.GetFileName).OrderBy(x => x).ToList();
        Check(files.SequenceEqual(new[] { "Rapor_v1.pdf", "Rapor_v2.pdf" }), string.Join(",", files));

        // PDF üretilemezse yedek HTML (dolu) kalır, boş yer tutucu kalmaz.
        c.Pdf.Error = "yok";
        Wait(c.Vm.CreateReportAsync());
        var html = Path.Combine(c.Folder, "reports", "Rapor_v3.html");
        Check(File.Exists(html) && new FileInfo(html).Length > 0, "yedek HTML dolu olmalı");
    });

    // B-20: rapor üretilemezse ayrılan sürüm geri bırakılır
    public static void Fonk_DZ_B20_FailedReportReleasesReservedVersion() => With(c =>
    {
        c.Open();
        Wait(c.Vm.CreateReportAsync());
        Directory.CreateDirectory(DefectsDir(c));
        File.WriteAllText(Path.Combine(DefectsDir(c), "9.json"), "{bozuk");
        Wait(c.Vm.CreateReportAsync());   // okunamayan kusur dosyası: rapor üretilemez
        Check(LastIsError(c), "okunamayan kusur dosyasıyla rapor hata vermeli");
        var files = Directory.GetFiles(Path.Combine(c.Folder, "reports")).Select(Path.GetFileName).ToList();
        Check(files.SequenceEqual(new[] { "Rapor_v1.pdf" }), "boş yer tutucu kalmamalı: " + string.Join(",", files));
    });

    // N-1: Türkçe arayüzde karar etiketleri Türkçe
    public static void Fonk_DZ_N1_DecisionLabelsAreTurkishInUi() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.1";
        c.Vm.SaveMeasurements();
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(c.Vm.UndoText.StartsWith("Son karar: Kabul (SPA)"), c.Vm.UndoText);
        Check(string.Join("|", c.Vm.History).Contains("Kabul (SPA)") && !string.Join("|", c.Vm.History).Contains("Accept"), string.Join("|", c.Vm.History));

        // Raporlardaki iki dilli karar adı değişmez.
        Check(DefectSentence.DecisionLabel(Decision.Accept) == "Accept", "rapor etiketi İngilizce kalır");
        foreach (var d in Enum.GetValues<Decision>())
            Check(DefectPresentation.DecisionLabelTr(d).Length > 0 && !DefectPresentation.DecisionLabelTr(d).StartsWith("After rework"), $"{d}: {DefectPresentation.DecisionLabelTr(d)}");
    });
}

public static partial class AdminViewModelTests
{
    // B-11: uygulanmamış editör değişikliği Save sırasında doğrulanarak otomatik uygulanır
    public static void Fonk_DZ_B11_SaveAutoAppliesPendingEditor() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = 0;
        c.Types.Name = "Yeniden Adlandı";
        c.Types.FieldColor = true;
        c.Types.Save();
        var saved = SharedConfig.LoadDefectTypes(c.Root).First();
        Check(saved.Name == "Yeniden Adlandı" && saved.Fields.Contains(MeasureField.Color), $"kayıtlı: {saved.Name} / {string.Join(",", saved.Fields)}");
        Check(c.Notes.Last().Kind == StatusKind.Success && !c.Types.IsDirty, "başarı bildirimi, kirli işareti kalkmalı");

        // Geçersiz bekleyen değişiklik: hata verilir, hiçbir şey yazılmaz.
        var before = File.ReadAllText(Path.Combine(SharedConfig.Folder(c.Root), "defect_types.json"));
        c.Types.SelectedIndex = 1;
        c.Types.Name = "Kötü#Ad";
        c.Types.Save();
        Check(c.Notes.Last().Kind == StatusKind.Error, "geçersiz ad hata vermeli");
        Check(File.ReadAllText(Path.Combine(SharedConfig.Folder(c.Root), "defect_types.json")) == before, "dosya değişmemeli");
    });

    // B-12: mevcut ek alanın türü değiştirilemez; ad / kısaltma değişebilir
    public static void Fonk_DZ_B12_ExistingFieldKindCannotChange() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.FieldName = "Yükseklik"; c.Types.FieldAbbrev = "H"; c.Types.FieldKindIndex = 0;
        c.Types.AddField();
        c.Types.SelectedFieldIndex = 0;
        c.Types.FieldKindIndex = (int)CustomFieldKind.Text;
        c.Types.ApplyField();
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Notes.Last().Message.Contains("yeni bir alan oluşturun"), $"'{c.Notes.Last().Message}'");
        Check(c.Types.FieldKindIndex == (int)CustomFieldKind.Length, "tür seçimi eski değere dönmeli");
        c.Types.Save();
        Check(SharedConfig.LoadCustomFields(c.Root).Single().Kind == CustomFieldKind.Length, "tür değişmemeli");

        c.Types.FieldName = "Boy";
        c.Types.ApplyField();
        c.Types.Save();
        Check(SharedConfig.LoadCustomFields(c.Root).Single().Name == "Boy", "ad değişebilir");
    });

    // B-22: kök yokken giriş ekranı ilk kurulum sunmaz, açık mesaj verir
    public static void Fonk_DZ_B22_LoginWithRootGoneShowsRootMessage() => With(c =>
    {
        c.SetupPin();
        Directory.Move(c.Root, c.Root + "_gone");
        try
        {
            c.Shell.Lock(null);
            var expected = $"Ortak alana erişilemiyor: {c.Root}. Ağ bağlantısını kontrol edin.";
            Check(!c.Login.IsFirstRun && c.Login.Hint == expected, $"ipucu: '{c.Login.Hint}'");
            c.Login.Pin = Pin;
            c.Login.Login();
            Check(c.Login.Error == expected && !c.Shell.IsLoggedIn && !Directory.Exists(c.Root), $"hata: '{c.Login.Error}'");
        }
        finally
        {
            if (Directory.Exists(c.Root + "_gone"))
                Directory.Move(c.Root + "_gone", c.Root);
        }
    });
}

/// <summary>Core düzeyi düzeltme testleri.</summary>
public static class FonkDuzeltmeCoreTests
{
    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static string TempRoot(bool create = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-dz-" + Guid.NewGuid().ToString("N"));
        if (create)
            Directory.CreateDirectory(root);
        return root;
    }

    private static void WithTemp(Action<string> body)
    {
        var root = TempRoot();
        try { body(root); }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    private static string Catch<T>(Action a) where T : Exception
    {
        try { a(); }
        catch (T ex) { return ex.Message; }
        throw new Exception($"{typeof(T).Name} beklenirdi");
    }

    private static Session S(string part = "P", string op = "O", string serial = "S") =>
        new() { PartNumber = part, OperationNumber = op, SerialNumber = serial, Inspector = "A", Project = part };

    // B-22 / B-23: hiçbir katman kökü ya da oturum klasörünü yeniden oluşturmaz
    public static void RootIsNeverRecreatedImplicitly()
    {
        var root = TempRoot(create: false);
        try
        {
            var expected = $"Ortak alana erişilemiyor: {root}. Ağ bağlantısını kontrol edin.";
            var store = new SessionStore(root);

            Expect(Catch<DirectoryNotFoundException>(() => store.OpenOrCreate(S())) == expected, "OpenOrCreate mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => store.CompleteSession(Path.Combine(root, "P", "O", "S"))) == expected, "CompleteSession mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => AuditLog.Append(store, "x", "y", "z")) == expected, "AuditLog.Append mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => IndexBuilder.Rebuild(store)) == expected, "IndexBuilder.Rebuild mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => SharedConfig.SetPin(root, "12345678")) == expected, "SetPin mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => SharedConfig.SaveDefectTypes(root, SharedConfig.Defaults)) == expected, "SaveDefectTypes mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => SharedConfig.SaveCustomFields(root, Array.Empty<CustomFieldDef>())) == expected, "SaveCustomFields mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => SharedConfig.SaveUseWindowsUser(root, true)) == expected, "SaveUseWindowsUser mesajı");
            Expect(Catch<DirectoryNotFoundException>(() => FileLock.Acquire(Path.Combine(root, "a", "x.lock"))).Length > 0, "FileLock klasör yoksa hata verir");
            Expect(!Directory.Exists(root), "kök hiçbir işlemle yeniden oluşturulmamalı");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    // B-23: kök var, oturum klasörü yok: oturum klasörü yeniden oluşturulmaz
    public static void MissingSessionFolderIsNotRecreated() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var folder = Path.Combine(root, "P", "O", "S");
        Expect(Catch<DirectoryNotFoundException>(() => store.AddDefect(folder, "Dent")).Contains("bulunamadı"), "AddDefect");
        Expect(Catch<DirectoryNotFoundException>(() => store.SaveSession(folder, S())).Contains("bulunamadı"), "SaveSession");
        Expect(Catch<DirectoryNotFoundException>(() => ReportFiles.Reserve(folder)).Contains("bulunamadı"), "Reserve");
        Expect(Directory.GetFileSystemEntries(root).Length == 0, "hiçbir klasör oluşmamalı");
    });

    // B-10: fotoğraf adı boşluklu / paralel eklemede çakışmaz
    public static void PhotoNamesNeverCollide() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(S());
        var defect = store.AddDefect(folder, "Dent");
        var src = Path.Combine(root, "src.jpg");
        File.WriteAllBytes(src, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0, 1 });
        var dir = Path.Combine(folder, "defects");

        // 01 ve 03 var, 02 yok: sıradaki 04 (var olan hiçbir dosya ezilmez).
        File.WriteAllText(Path.Combine(dir, "1_photo_01.jpg"), "a");
        File.WriteAllText(Path.Combine(dir, "1_photo_03.png"), "c");
        File.WriteAllText(Path.Combine(dir, "1_photo_03_clean.jpg"), "temiz");
        Expect(PhotoImporter.Import(folder, defect.Id, src) == "1_photo_04.jpg", "boşluktan sonra en büyük numara + 1");
        Expect(PhotoImporter.Import(folder, defect.Id, src, new[] { "1_photo_05.jpg" }) == "1_photo_06.jpg", "kayıtlı ad rezerve sayılır");
        Expect(File.ReadAllText(Path.Combine(dir, "1_photo_01.jpg")) == "a", "var olan dosya ezilmedi");

        // Paralel: hepsi farklı ad alır.
        var names = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 24, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ => names.Add(PhotoImporter.Import(folder, defect.Id, src)));
        Expect(names.Distinct().Count() == 24, $"24 paralel ekleme 24 farklı ad: {names.Distinct().Count()}");
    });

    // B-20: sürüm numaraları kilit altında ayrılır
    public static void ReportVersionsAreReservedUnderLock() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(S());
        var versions = new System.Collections.Concurrent.ConcurrentBag<int>();
        Parallel.For(0, 16, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ => versions.Add(ReportFiles.Reserve(folder)));
        Expect(versions.OrderBy(v => v).SequenceEqual(Enumerable.Range(1, 16)), string.Join(",", versions.OrderBy(v => v)));
        Expect(ReportFiles.LatestVersion(folder) == 16, "en son sürüm 16");

        ReportFiles.Release(folder, 16);
        Expect(ReportFiles.LatestVersion(folder) == 15, "boş yer tutucu geri bırakılır");
        File.WriteAllText(Path.Combine(folder, "reports", "Rapor_v15.html"), "<html/>");
        ReportFiles.Release(folder, 15);
        Expect(File.Exists(Path.Combine(folder, "reports", "Rapor_v15.html")), "dolu HTML silinmez");
    });

    // B-04 / B-15: oturum tamamlama tek kilit altında, ikincisi no-op
    public static void CompleteSessionIsAtomicAndIdempotent() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(S());
        store.AddDefect(folder, "Dent");
        Expect(Catch<InvalidOperationException>(() => store.CompleteSession(folder)).Contains("açık kusurlar var"), "açık kusurla tamamlanamaz");
        Expect(store.LoadSession(folder)!.Status == SessionStatus.Open, "durum Open kalmalı");

        store.UpdateDefect(folder, 1, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M", reference: "R"));
        Expect(store.CompleteSession(folder), "ilk tamamlama true");
        Expect(!store.CompleteSession(folder), "ikinci tamamlama false (no-op)");
        Expect(Catch<InvalidOperationException>(() => store.RequireWritable(folder)).Contains("tamamlandı"), "RequireWritable");
    });

    // N-2: "<numara>.json" olmayan dosyalar kusur sayılmaz
    public static void ForeignJsonFilesAreNeverDefects() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var (folder, _) = store.OpenOrCreate(S());
        store.AddDefect(folder, "Dent");
        var dir = Path.Combine(folder, "defects");
        foreach (var name in new[] { "notlar.json", "0.json", "007.json", "kopya.json", "1 (2).json", "-1.json" })
            File.WriteAllText(Path.Combine(dir, name), "{}");

        var defects = store.LoadDefects(folder);
        Expect(defects.Select(d => d.Id).SequenceEqual(new[] { 1 }), "yalnızca #1 kusur: " + string.Join(",", defects.Select(d => d.Id)));
        Expect(store.TakeWarnings().Count >= 5, "atlanan dosyalar uyarı üretmeli");
        Expect(store.AddDefect(folder, "Nick").Id > 7, "adı bozuk dosyaların numaraları dolu sayılır, yeni kusur onların üzerine yazılmaz");
    });

    // Bakım sayfası: denetim sonu okuması dosyanın tamamını taramaz ama aynı sonucu verir
    public static void AuditTailReadsFromEndOfLargeLog() => WithTemp(root =>
    {
        var store = new SessionStore(root);
        var path = Path.Combine(root, AuditLog.FileName);
        var lines = Enumerable.Range(0, 3000)
            .Select(i => System.Text.Json.JsonSerializer.Serialize(new AuditEntry("2026-01-01T00:00:00", "ş-ü", "test", "n=" + i + " " + new string('x', 60), "p", "h")));
        File.WriteAllText(path, string.Join("\n", lines) + "\n\n");   // sonda boş satırlar, 64 KB'tan büyük dosya
        Expect(new FileInfo(path).Length > 200_000, "ön koşul: büyük günlük");

        var tail = AuditLog.ReadTail(store, 300);
        Expect(tail.Count == 300 && tail[0].Details.StartsWith("n=2999 ") && tail[299].Details.StartsWith("n=2700 "), $"{tail.Count} satır, ilk: {tail.FirstOrDefault()?.Details[..8]}");
        Expect(AuditLog.ReadTail(store, 5000).Count == 3000, "sınırdan az satır varsa hepsi");
        Expect(AuditLog.ReadTail(store, 3).Select(e => e.Details[..6]).SequenceEqual(new[] { "n=2999", "n=2998", "n=2997" }), "yeniden eskiye");
    });

    // B-06: rework sonrası muayene ölçüm kutusu ayrıştırma kuralı
    public static void ReinspectEntryParseRule()
    {
        Expect(ReinspectForm.ParseEntry("", out var v) == ReinspectForm.EntryState.Empty && v is null, "boş");
        Expect(ReinspectForm.ParseEntry("   ", out _) == ReinspectForm.EntryState.Empty, "boşluk");
        Expect(ReinspectForm.ParseEntry(null, out _) == ReinspectForm.EntryState.Empty, "null");
        Expect(ReinspectForm.ParseEntry(" 0.7 ", out v) == ReinspectForm.EntryState.Valid && v == 0.7, "geçerli");
        foreach (var bad in new[] { "abc", "0.7x", "1,2,3", "NaN", "∞", "1e999", "--1" })
            Expect(ReinspectForm.ParseEntry(bad, out v) == ReinspectForm.EntryState.Invalid && v is null, $"'{bad}' hatalı olmalı (sessizce boş sayılmamalı)");
    }

    // B-03: üç yönlü birleştirme kuralları
    public static void RebaseRules()
    {
        var disk = new Defect { Id = 1, DefectType = "Dent", Depth = 0.5, Width = null, Notes = "disk notu" };
        var loaded = new MeasurementSnapshot(0.1, null, null, null, null, null);

        // Kullanıcı yalnızca not değiştirdi: diskteki D korunur.
        var r = MeasurementEditor.Rebase(loaded, "disk notu", null, loaded, "benim notum", null, disk);
        Expect(r.Values.Depth == 0.5 && r.Notes == "benim notum" && r.Conflicts.Count == 0, "not yazılır, D diskte kalır");

        // Kullanıcı D'yi değiştirdi, diskte başkası da değiştirdi: çakışma.
        r = MeasurementEditor.Rebase(loaded, null, null, loaded with { Depth = 0.9 }, null, null, disk);
        Expect(r.Values.Depth == 0.5 && r.Conflicts.SequenceEqual(new[] { "Derinlik" }), "çakışma bildirilir, disk kalır");

        // İki taraf aynı değere getirdi: çakışma değil.
        r = MeasurementEditor.Rebase(loaded, null, null, loaded with { Depth = 0.5 }, null, null, disk);
        Expect(r.Conflicts.Count == 0 && r.Values.Depth == 0.5, "aynı değer çakışma sayılmaz");

        // Ek alan: kullanıcı H'yi girdi, başkası S'yi girmiş: ikisi de kalır.
        var h = new CustomMeasure("fh", "Yükseklik", "H", CustomFieldKind.Length, 0.3, null);
        var s = new CustomMeasure("fs", "Sapma", "S", CustomFieldKind.Number, -2, null);
        disk.Custom = new List<CustomMeasure> { s };
        r = MeasurementEditor.Rebase(loaded, null, null, loaded with { Custom = new[] { h } }, null, null, disk);
        Expect(r.Conflicts.Count == 0 && r.Values.Custom!.Select(m => m.Key).OrderBy(k => k).SequenceEqual(new[] { "fh", "fs" }), "iki ek alan birlikte");
    }
}
