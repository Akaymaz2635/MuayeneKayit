using System.Globalization;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Fonksiyonel test planı (docs/test/FONKSIYONEL-TEST-PLANI.md): muayene ekranı ViewModel testleri.
// ŞÜPHELİ (B-xx) testler "--supheli" bayrağıyla koşar; beklenti DOĞRU davranıştır, kırmızı = hata doğrulandı.
public static partial class ViewModelTests
{
    // ───────────────────────── Yardımcılar ─────────────────────────

    private static void WithRoot(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            body(root);
        }
        finally
        {
            foreach (var p in new[] { root, root + "_gone" })
            {
                try { if (Directory.Exists(p)) Directory.Delete(p, recursive: true); } catch (IOException) { }
            }
        }
    }

    /// <summary>Var olan kök üzerinde ikinci (üçüncü...) bilgisayarı temsil eden bağlam.</summary>
    private static Ctx NewCtx(string root, IReadOnlyList<DefectTypeTemplate>? types = null,
        IReadOnlyList<CustomFieldDef>? fields = null, bool realAudit = false)
    {
        var store = new SessionStore(root);
        var audit = new FakeAudit();
        var dialogs = new FakeDialogService();
        var picker = new FakeFilePicker();
        var windows = new FakeWindowService();
        var pdf = new FakePdfConverter();
        var settings = new FakeUserSettings();
        var user = new FakeWindowsUser();
        IAuditTrail trail = realAudit ? new FileAuditTrail(store) : audit;
        var c = new Ctx
        {
            Vm = new InspectionViewModel(store, trail, types ?? SharedConfig.Defaults, dialogs, picker, windows, pdf, settings, user, fields),
            WindowsUser = user, Store = store, Audit = audit, Dialogs = dialogs, Picker = picker, Windows = windows, Pdf = pdf, Settings = settings,
        };
        c.Vm.Notify += (m, k) => c.Notes.Add((m, k));
        return c;
    }

    private static string? Escaped(Action a)
    {
        try { a(); return null; }
        catch (Exception ex) { return ex.GetType().Name; }
    }

    private static void Problems(string bug, List<string> problems)
    {
        if (problems.Count > 0)
            throw new Exception($"{bug} doğrulandı: " + string.Join(" | ", problems));
    }

    private static string PhotoFile(string tag)
    {
        var path = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-img-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(path, JpegBytes.Concat(System.Text.Encoding.ASCII.GetBytes(tag)).ToArray());
        return path;
    }

    private static string DefectsDir(Ctx c) => Path.Combine(c.Folder, "defects");

    private static void OpenAs(Ctx c, string part, string op, string serial, string inspector = "Test Inspector")
    {
        c.Vm.PartNumber = part;
        c.Vm.OperationNumber = op;
        c.Vm.SerialNumber = serial;
        c.Vm.InspectorName = inspector;
        c.Vm.OpenSession();
    }

    private static bool LastIsError(Ctx c) => c.Notes.Count > 0 && c.Notes.Last().Kind == StatusKind.Error;

    // ═════════════════════════ ŞÜPHELİ (B-xx) ═════════════════════════

    // B-01 F-MEAS-018
    public static void Fonk_MEAS018_RoundTripKeepsPrecision()
    {
        var problems = new List<string>();

        With(c =>
        {
            c.Open();
            c.Vm.AddDefect("Dent");
            c.Vm.DepthText = "0.125";
            c.Vm.SaveMeasurements();
            Check(Load(c, 1).Depth == 0.125, "ön koşul: 0.125 yazılmalı");
            c.Vm.NotesText = "yalnız not";
            c.Vm.AutoSave();
            var d = Load(c, 1);
            if (d.Depth != 0.125)
                problems.Add($"mm: D 0.125 -> {d.Depth?.ToString(CultureInfo.InvariantCulture)} (düzeltme kaydı: {d.MeasurementEdits.Count})");
        });

        With(c =>
        {
            c.Open();
            c.Vm.AddDefect("Deformation");
            c.Vm.AngleText = "12.345";
            c.Vm.SaveMeasurements();
            Check(Load(c, 1).AngleDeg == 12.345, "ön koşul: açı 12.345 yazılmalı");
            c.Vm.NotesText = "yalnız not";
            c.Vm.AutoSave();
            var d = Load(c, 1);
            if (d.AngleDeg != 12.345)
                problems.Add($"açı: 12.345 -> {d.AngleDeg?.ToString(CultureInfo.InvariantCulture)}");
        });

        With(c =>
        {
            c.Settings.Unit = LengthUnit.Inch;
            c.Open();
            c.Vm.AddDefect("Dent");
            c.Vm.DepthText = ".123456";
            c.Vm.SaveMeasurements();
            Check(Load(c, 1).Depth == 0.123456, "ön koşul: inç değeri yazılmalı");
            c.Vm.NotesText = "yalnız not";
            c.Vm.AutoSave();
            var d = Load(c, 1);
            if (d.Depth != 0.123456)
                problems.Add($"inch: 0.123456 -> {d.Depth?.ToString(CultureInfo.InvariantCulture)}");
        });

        // Karar öncesi otomatik kayıt da aynı değeri Snapshot'a taşır.
        With(c =>
        {
            c.Open();
            c.Vm.AddDefect("Dent");
            c.Vm.DepthText = "0.125";
            c.Vm.SaveMeasurements();
            c.Dialogs.Decisions.Enqueue(new DecisionInput("OP40", null, null, null));
            Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
            var snap = Load(c, 1).Dispositions.Last().Snapshot;
            if (snap?.Depth != 0.125)
                problems.Add($"Rework Snapshot D: {snap?.Depth?.ToString(CultureInfo.InvariantCulture)}");
        });

        Problems("B-01", problems);
    }

    // B-03 F-CONC-004
    public static void Fonk_CONC004_StaleFormDoesNotOverwriteOtherUsersMeasurement() => WithRoot(root =>
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

        b.Vm.NotesText = "yalnızca not";
        b.Vm.AutoSave();

        var d = a.Store.LoadDefects(a.Folder)[0];
        Check(d.Depth == 0.5,
            $"B-03 doğrulandı: A'nın D=0.5 ölçümü eski ekrandaki B'nin yalnızca-not düzeltmesiyle {d.Depth?.ToString(CultureInfo.InvariantCulture)} oldu " +
            $"(geçmiş: {string.Join("; ", d.MeasurementEdits.Select(e => $"{e.By}"))})");
    });

    // B-04 F-COMP-004
    public static void Fonk_COMP004_CompletedSessionCannotGetOpenDefect() => With(c =>
    {
        c.Open();
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "ön koşul: oturum tamamlanmalı");
        c.Vm.AddDefect("Dent");

        var status = c.Store.LoadSession(c.Folder)!.Status;
        var blocking = SessionGate.BlockingDefects(c.Store.LoadDefects(c.Folder)).Count;
        Check(!(status == SessionStatus.Completed && blocking > 0),
            $"B-04 doğrulandı: tamamlanmış oturumda açık kusur var (durum {status}, açık kusur {blocking}, son bildirim: {c.Notes.Last().Kind})");
    });

    // B-04 F-CONC-006
    public static void Fonk_CONC006_CompleteAndAddDefectRace() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        a.Open();
        b.Open();
        a.Vm.Complete();
        b.Vm.AddDefect("Dent");

        var status = a.Store.LoadSession(a.Folder)!.Status;
        var blocking = SessionGate.BlockingDefects(a.Store.LoadDefects(a.Folder)).Count;
        Check(!(status == SessionStatus.Completed && blocking > 0),
            $"B-04 doğrulandı: A tamamladıktan sonra B kusur ekledi; oturum {status}, açık kusur {blocking}");
    });

    // B-02 F-CFG-009
    public static void Fonk_CFG009_CorruptSessionJsonDoesNotCrash()
    {
        var problems = new List<string>();
        foreach (var (label, content) in new[] { ("yarım JSON", "{\"partNumber\":\"PD1"), ("0 bayt", ""), ("çöp metin", "bu json değil") })
        {
            WithRoot(root =>
            {
                var c = NewCtx(root);
                var folder = c.Store.FolderFor("PD170", "OP40", "SN001");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "session.json"), content);
                var ex = Escaped(c.Open);
                if (ex is not null)
                    problems.Add($"{label}: {ex} OpenSession'dan dışarı kaçtı");
                else if (!LastIsError(c))
                    problems.Add($"{label}: hata bildirilmedi");
            });
        }
        Problems("B-02", problems);
    }

    // B-02 F-CFG-010
    public static void Fonk_CFG010_CorruptDefectFileDoesNotCrash() => With(c =>
    {
        var problems = new List<string>();
        c.Open();
        c.Vm.AddDefect("Dent");
        File.WriteAllText(Path.Combine(DefectsDir(c), "2.json"), "{bozuk");

        c.Vm.ChangeSession();
        var ex = Escaped(c.Open);
        if (ex is not null) problems.Add($"oturum açma: {ex} dışarı kaçtı");

        ex = Escaped(() => new DashboardViewModel(c.Store, SharedConfig.Defaults).Refresh());
        if (ex is not null) problems.Add($"Dashboard.Refresh: {ex} dışarı kaçtı (tek bozuk kusur tüm listeyi düşürür)");

        ex = Escaped(() => IndexBuilder.Rebuild(c.Store));
        if (ex is not null) problems.Add($"IndexBuilder.Rebuild: {ex} dışarı kaçtı");

        Problems("B-02", problems);
    });

    // B-02 F-CFG-011
    public static void Fonk_CFG011_TruncatedAuditTailDoesNotBreakOperations() => WithRoot(root =>
    {
        var c = NewCtx(root, realAudit: true);
        c.Open();
        File.AppendAllText(Path.Combine(root, AuditLog.FileName), "{\"ts\":\"2026-01-01T00:00:00\",\"actor\":\"x\",\"act");
        var problems = new List<string>();
        var ex = Escaped(() => c.Vm.AddDefect("Dent"));
        if (ex is not null)
            problems.Add($"AddDefect: {ex} dışarı kaçtı; kusur dosyası yazıldı mı: {File.Exists(Path.Combine(c.Folder, "defects", "1.json"))} (denetimsiz kayıt)");
        Problems("B-02", problems);
    });

    // B-05 F-CFG-013
    public static void Fonk_CFG013_HugeNoteDoesNotBrickSession() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.NotesText = new string('a', 6_000_000);
        c.Vm.SaveMeasurements();
        var saveNote = c.Notes.Last();
        var size = new FileInfo(Path.Combine(DefectsDir(c), "1.json")).Length;
        var ex = Escaped(() => c.Store.LoadDefects(c.Folder));
        Check(ex is null, $"B-05 doğrulandı: {size / 1024 / 1024} MB'lık kusur dosyası yazıldı, sonraki okuma {ex} ile başarısız (oturum açılamaz); kayıt anındaki bildirim: {saveNote.Kind} '{saveNote.Message}'");
    });

    // B-22 F-NET-003
    public static void Fonk_NET003_RootGoneDoesNotRecreateRoot() => WithRoot(root =>
    {
        var c = NewCtx(root);
        c.Open();
        Directory.Move(root, root + "_gone");
        OpenAs(c, "PD999", "OP10", "SN999");
        var recreated = Directory.Exists(root);
        Check(!recreated && LastIsError(c),
            $"B-22 doğrulandı: kök erişilemezken yeni oturum açıldı (kök yeniden oluşturuldu={recreated}, son bildirim={c.Notes.Last().Kind}: {c.Notes.Last().Message})");
    });

    // B-08 F-SESS-008
    public static void Fonk_SESS008_UnderscorePartIsRejectedOrVisible()
    {
        var problems = new List<string>();
        WithRoot(root =>
        {
            var c = NewCtx(root);
            OpenAs(c, "_X", "OP40", "SN001");
            if (c.Vm.IsSessionOpen)
            {
                c.Vm.AddDefect("Dent");
                var visible = IndexBuilder.Rebuild(c.Store).Any(r => r.Part == "_X");
                if (!visible)
                    problems.Add("'_X' parça no kabul edildi ama Index/Dashboard/Admin Kayıtlar'da görünmüyor");
            }

            var c2 = NewCtx(root);
            OpenAs(c2, "_config", "OP40", "SN001");
            if (Directory.Exists(Path.Combine(root, "_config", "OP40")))
                problems.Add("'_config' parça no ile muayene klasörü _config altına yazıldı");
        });
        Problems("B-08", problems);
    }

    // B-12 F-CUST-009
    public static void Fonk_CUST009_FieldKindChangeDoesNotRewriteStoredValue() => WithRoot(root =>
    {
        var types = new[] { new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth }, CustomFields = { CustomFieldTests.Height.Key } } };
        var c1 = NewCtx(root, types, new[] { CustomFieldTests.Height });
        c1.Open();
        c1.Vm.AddDefect("Dent");
        c1.Vm.CustomInputs.Single().Text = "0.3";
        c1.Vm.SaveMeasurements();
        Check(Load(c1, 1).Custom?.Single().Number == 0.3, "ön koşul: H 0.3 yazılmalı");

        var asText = new CustomFieldDef { Key = CustomFieldTests.Height.Key, Name = "Yükseklik", Abbrev = "H", Kind = CustomFieldKind.Text };
        var c2 = NewCtx(root, types, new[] { asText });
        c2.Open();
        c2.Vm.Select(1);
        c2.Vm.NotesText = "başka bir alan";
        c2.Vm.AutoSave();

        var m = c2.Store.LoadDefects(c2.Folder)[0].Custom?.SingleOrDefault();
        Check(m is { Kind: CustomFieldKind.Length, Number: 0.3 },
            $"B-12 doğrulandı: kayıtlı değer sessizce yeniden yazıldı (tür={m?.Kind}, sayı={m?.Number}, metin={m?.Text})");
    });

    // B-13 F-TAB-006
    public static void Fonk_TAB006_TypeWithoutMeasureFieldsGoesToPhoto() => With(c =>
    {
        c.Open();
        Check(c.Vm.AddDefect("Ölçümsüz"), "kusur eklenmeli");
        Check(c.Vm.CurrentStep == InspectionStep.Photo && !c.Vm.IsSavePrimary,
            $"B-13 doğrulandı: ölçüm alanı olmayan tipte sıradaki adım {c.Vm.CurrentStep}, Kaydet vurgulu={c.Vm.IsSavePrimary}, ipucu: {c.Vm.NextStepText}");
    }, new[] { new DefectTypeTemplate { Name = "Ölçümsüz" } }, null);

    // B-10 F-PHOTO-008
    public static void Fonk_PHOTO008_PhotoNameCollisionAfterMissingFile() => With(c =>
    {
        AddMeasuredDent(c);
        foreach (var tag in new[] { "A", "B" })
        {
            c.Picker.Picks.Enqueue(PhotoFile(tag));
            Wait(c.Vm.AddPhotoFromFileAsync());
        }
        Check(Load(c, 1).Photos.Count == 2, "ön koşul: iki fotoğraf eklenmeli");
        File.Delete(Path.Combine(DefectsDir(c), "1_photo_01.jpg"));
        var before = File.ReadAllBytes(Path.Combine(DefectsDir(c), "1_photo_02.jpg"));

        c.Picker.Picks.Enqueue(PhotoFile("C"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        var after = File.ReadAllBytes(Path.Combine(DefectsDir(c), "1_photo_02.jpg"));
        Check(before.SequenceEqual(after), "var olan 1_photo_02.jpg üzerine yazılmamalı");
        Check(c.Notes.Last().Kind == StatusKind.Success && Load(c, 1).Photos.Count == 3,
            $"B-10 doğrulandı: ekleme başarısız ({c.Notes.Last().Kind}: {c.Notes.Last().Message}); kusurda {Load(c, 1).Photos.Count} fotoğraf");
    });

    // B-10 F-CONC-007
    public static void Fonk_CONC007_ParallelPhotoAddsBothSucceed() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        a.Open();
        b.Open();
        var collisions = 0;
        string? firstMsg = null;
        for (var i = 1; i <= 15; i++)
        {
            a.Vm.AddDefect("Dent");
            b.Vm.Refresh(i);
            a.Picker.Picks.Enqueue(PhotoFile("a" + i));
            b.Picker.Picks.Enqueue(PhotoFile("b" + i));
            using var barrier = new Barrier(2);
            var t1 = Task.Run(() => { barrier.SignalAndWait(); Wait(a.Vm.AddPhotoFromFileAsync()); });
            var t2 = Task.Run(() => { barrier.SignalAndWait(); Wait(b.Vm.AddPhotoFromFileAsync()); });
            Task.WaitAll(t1, t2);
            var n = a.Store.LoadDefects(a.Folder).First(d => d.Id == i).Photos.Count;
            if (n != 2 || LastIsError(a) || LastIsError(b))
            {
                collisions++;
                firstMsg ??= $"fotoğraf sayısı {n}; A: {a.Notes.Last().Message}; B: {b.Notes.Last().Message}";
            }
        }
        Check(collisions == 0, $"B-10 doğrulandı: 15 denemenin {collisions} tanesinde paralel fotoğraf eklemesi hata verdi / biri kayboldu. İlk örnek: {firstMsg}");
    });

    // B-14 F-EDIT-020
    public static void Fonk_EDIT020_EditorSaveOnDefectClosedByOtherUser() => With(c =>
    {
        AddMeasuredDent(c);
        c.Picker.Picks.Enqueue(PhotoFile("E"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        c.Vm.OpenEditor("1_photo_01.jpg");
        var editor = c.Windows.Editors.Single();

        c.Store.UpdateDefect(c.Folder, 1, d => DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "Başka Kullanıcı"));
        // Gerçek düzenleyici penceresi bu hatayı yakalayıp ekranda gösterir ("Kaydedilemedi: ..."); pencere açık kalır.
        string? refusal = null;
        try
        {
            editor.OnSave(new PhotoEditResultData(
                new PhotoMarkup { ImageWidth = 100, ImageHeight = 100, Shapes = { Annotation.CenterSquare(100, 100, 1) } }, null));
        }
        catch (InvalidOperationException ex)
        {
            refusal = ex.Message;
        }

        var written = Load(c, 1).Markups.ContainsKey("1_photo_01.jpg");
        Check(!written, "B-14: kapatılmış kusura işaret yazıldı (geri çağrı durumu kontrol etmiyor)");
        Check(refusal is not null && refusal.Contains("kaydedilmedi"), $"B-14: kullanıcıya açık ret mesajı verilmeli: '{refusal}'");
        Check(!Audit(c).Any(e => e.Contains("|defect.markup|")), "B-14: reddedilen kayıt için denetim satırı yazılmamalı");
    });

    private static List<string> Audit(Ctx c) => c.Audit.Entries;

    // B-20 F-CONC-013
    public static void Fonk_CONC013_ParallelReportsGetDistinctVersions() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        var collisions = 0;
        for (var i = 1; i <= 25; i++)
        {
            OpenAs(a, "PD170", "OP40", "R" + i);
            OpenAs(b, "PD170", "OP40", "R" + i);
            using var barrier = new Barrier(2);
            var t1 = Task.Run(() => { barrier.SignalAndWait(); Wait(a.Vm.CreateReportAsync()); });
            var t2 = Task.Run(() => { barrier.SignalAndWait(); Wait(b.Vm.CreateReportAsync()); });
            Task.WaitAll(t1, t2);
            var pdfs = Directory.GetFiles(Path.Combine(a.Folder, "reports"), "*.pdf").Length;
            if (pdfs != 2)
                collisions++;
        }
        Check(collisions == 0, $"B-20 doğrulandı: 25 eşzamanlı rapor çiftinin {collisions} tanesi aynı sürüm numarasını üretti");
    });

    // B-15 F-COMP-005
    public static void Fonk_COMP005_SecondCompleteWritesNoSecondAudit() => With(c =>
    {
        c.Open();
        c.Vm.Complete();
        c.Vm.Complete();
        var n = c.Audit.Entries.Count(e => e.Contains("|session.complete|"));
        Check(n == 1, $"B-15 doğrulandı: tamamlanmış oturum yeniden tamamlanınca {n} adet session.complete denetim satırı var");
    });

    // F-CFG-020
    public static void Fonk_CFG020_ForeignJsonInDefectsIsIgnored() => With(c =>
    {
        c.Open();
        Directory.CreateDirectory(DefectsDir(c));
        File.WriteAllText(Path.Combine(DefectsDir(c), "notlar.json"), "{}");
        c.Vm.ChangeSession();
        c.Open();
        Check(!c.Vm.Defects.Any(d => d.Id == 0), "yabancı JSON '#0' adlı boş kusur olarak göründü (F-CFG-020)");
    });

    // ═════════════════════════ ANA LİSTE (yeşil beklenir) ═════════════════════════

    // F-SESS-003
    public static void Fonk_SESS003_BlankInspectorRejected() => With(c =>
    {
        foreach (var name in new[] { "", "   " })
        {
            c.Vm.PartNumber = "PD170"; c.Vm.OperationNumber = "OP40"; c.Vm.SerialNumber = "SN001"; c.Vm.InspectorName = name;
            c.Vm.OpenSession();
            Check(LastIsError(c) && c.Notes.Last().Message == "Inspector adı boş olamaz.", $"Mesaj: {c.Notes.Last().Message}");
            Check(!c.Vm.IsSessionOpen && Directory.GetFileSystemEntries(c.Store.Root).Length == 0, "Oturum açılmamalı, klasör oluşmamalı");
        }
    });

    // F-SESS-004
    public static void Fonk_SESS004_BlankFieldsRejected() => With(c =>
    {
        var cases = new[]
        {
            ("", "OP40", "SN001", "Parça no boş olamaz."),
            ("PD170", "", "SN001", "Operasyon no boş olamaz."),
            ("PD170", "OP40", "  ", "Seri no boş olamaz."),
        };
        foreach (var (p, o, s, msg) in cases)
        {
            OpenAs(c, p, o, s);
            Check(LastIsError(c) && c.Notes.Last().Message == msg, $"Beklenen '{msg}', gelen '{c.Notes.Last().Message}'");
        }
        Check(Directory.GetFileSystemEntries(c.Store.Root).Length == 0, "Kökte klasör oluşmamalı");
    });

    // F-SESS-005
    public static void Fonk_SESS005_TraversalPartsRejected() => With(c =>
    {
        foreach (var bad in new[] { "PD/170", @"PD\170", "..", "PD170.", ".PD", "a:b", "PD*", "<x>", @"C:\x", "PD..170" })
        {
            OpenAs(c, bad, "OP40", "SN001");
            Check(!c.Vm.IsSessionOpen && LastIsError(c) && c.Notes.Last().Message.StartsWith("Parça no geçersiz karakter içeriyor"),
                $"'{bad}' reddedilmeli: {c.Notes.Last().Message}");
        }
        Check(Directory.GetFileSystemEntries(c.Store.Root).Length == 0, "Kök içinde / dışında hiçbir şey yazılmamalı");
    });

    // F-SESS-006
    public static void Fonk_SESS006_PartLengthBoundary() => With(c =>
    {
        OpenAs(c, new string('P', 64), "OP40", "SN001");
        Check(c.Vm.IsSessionOpen, $"64 karakter kabul edilmeli: {c.Notes.Last().Message}");
        c.Vm.ChangeSession();
        OpenAs(c, new string('P', 65), "OP40", "SN001");
        Check(!c.Vm.IsSessionOpen && LastIsError(c), "65 karakter reddedilmeli");
    });

    // F-SESS-007
    public static void Fonk_SESS007_TrimAndTurkishLetters() => With(c =>
    {
        OpenAs(c, "PD170", "  OP 40 ", "SN001");
        Check(c.Vm.IsSessionOpen && c.Vm.Folder!.EndsWith(Path.Combine("PD170", "OP 40", "SN001")), $"Klasör: {c.Vm.Folder}");
        c.Vm.ChangeSession();
        OpenAs(c, "ÇĞİÖŞÜ-ğ_1", "OP40", "SN002");
        Check(c.Vm.IsSessionOpen, $"Türkçe harfler kabul edilmeli: {c.Notes.Last().Message}");
    });

    // F-SESS-002 (başka kullanıcı mevcut oturumu açar)
    public static void Fonk_SESS002_SecondInspectorKeepsSessionInspector() => With(c =>
    {
        OpenAs(c, "PD170", "OP40", "SN001", "A");
        c.Vm.AddDefect("Dent");
        c.Vm.ChangeSession();
        OpenAs(c, "PD170", "OP40", "SN001", "B");
        Check(c.Notes.Last().Message.StartsWith("Kayıtlı muayene açıldı"), c.Notes.Last().Message);
        Check(c.Store.LoadSession(c.Folder)!.Inspector == "A", "session.json'daki Inspector 'A' kalmalı");
        Check(c.Vm.SessionSubtitle.Contains("Inspector: B"), $"Alt satır: {c.Vm.SessionSubtitle}");
        c.Vm.Select(1);
        c.Vm.DepthText = "0.2";
        c.Vm.SaveMeasurements();
        Check(c.Audit.Entries.Last(e => e.Contains("|defect.measure|")).StartsWith("B|"), "Denetim satırı B adına olmalı");
    });

    // F-SESS-016
    public static void Fonk_SESS016_ChangeSessionSavesPendingText() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.3";
        c.Vm.ChangeSession();
        c.Open();
        Check(Load(c, 1).Depth == 0.3, "0.3 diske yazılmış olmalı");
    });

    // F-SESS-018
    public static void Fonk_SESS018_LongAndSpecialInspectorNamesKeepAuditIntact() => WithRoot(root =>
    {
        foreach (var name in new[] { new string('İ', 500), "Ali \"x\" | ; \\ /" })
        {
            var c = NewCtx(root, realAudit: true);
            OpenAs(c, "PD170", "OP40", "S" + Math.Abs(name.GetHashCode()), name);
            Check(c.Vm.IsSessionOpen, $"Oturum açılmalı: {c.Notes.Last().Message}");
            c.Vm.AddDefect("Dent");
            Check(AuditLog.Verify(c.Store).Ok, "Denetim zinciri sağlam olmalı");
        }
    });

    // F-DEF-002
    public static void Fonk_DEF002_NullTypeRejected() => With(c =>
    {
        c.Open();
        Check(!c.Vm.AddDefect(null), "false dönmeli");
        Check(LastIsError(c) && c.Notes.Last().Message == "Kusur tipi seçin.", c.Notes.Last().Message);
        Check(!Directory.Exists(DefectsDir(c)) || Directory.GetFiles(DefectsDir(c)).Length == 0, "dosya olmamalı");
    });

    // F-DEF-003
    public static void Fonk_DEF003_AddDefectWithoutSession() => With(c =>
    {
        Check(!c.Vm.AddDefect("Dent"), "false dönmeli");
        Check(LastIsError(c) && c.Notes.Last().Message == "Önce bir oturum açın.", c.Notes.Last().Message);
        Check(Directory.GetFileSystemEntries(c.Store.Root).Length == 0, "hiçbir yere yazmamalı");
    });

    // F-DEF-005
    public static void Fonk_DEF005_SpecNameCopied() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(Load(c, 1).SpecName == "SPEC-1", $"SpecName: {Load(c, 1).SpecName}");
    }, new[] { new DefectTypeTemplate { Name = "Dent", Fields = { MeasureField.Depth }, SpecName = "SPEC-1" } }, null);

    // F-DEF-012
    public static void Fonk_DEF012_OrphanTypeShowsAllFields() => With(c =>
    {
        c.Open();
        c.Store.AddDefect(c.Folder, "OldType");
        c.Vm.Refresh(1);
        Check(c.Vm.HasSelection && c.Vm.ShowDepth && c.Vm.ShowWidth && c.Vm.ShowLength && c.Vm.ShowRadius && c.Vm.ShowAngle && c.Vm.ShowColor,
            "şablon yoksa tüm yerleşik alanlar görünmeli");
    });

    private static void ExpectMeasureError(Ctx c, string text, Action<string> set, string label, string contains)
    {
        var path = Path.Combine(DefectsDir(c), "1.json");
        var before = File.ReadAllText(path);
        set(text);
        c.Vm.SaveMeasurements();
        Check(LastIsError(c) && c.Notes.Last().Message.Contains(contains), $"{label}='{text}': {c.Notes.Last().Message}");
        Check(File.ReadAllText(path) == before, $"{label}='{text}': json değişmemeli");
    }

    // F-MEAS-003
    public static void Fonk_MEAS003_NonPositiveRejected() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        foreach (var v in new[] { "0", "-1", "-0" })
        {
            ExpectMeasureError(c, v, t => c.Vm.DepthText = t, "Derinlik", "Derinlik pozitif bir sayı olmalı.");
            c.Vm.DepthText = "";
            ExpectMeasureError(c, v, t => c.Vm.WidthText = t, "Genişlik", "Genişlik pozitif bir sayı olmalı.");
            c.Vm.WidthText = "";
            ExpectMeasureError(c, v, t => c.Vm.LengthText = t, "Uzunluk", "Uzunluk pozitif bir sayı olmalı.");
            c.Vm.LengthText = "";
            ExpectMeasureError(c, v, t => c.Vm.RadiusText = t, "Çap", "Çap pozitif bir sayı olmalı.");
            c.Vm.RadiusText = "";
        }
    });

    // F-MEAS-004
    public static void Fonk_MEAS004_NaNInfinityOverflowRejected() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        foreach (var v in new[] { "NaN", "Infinity", "1e309" })
        {
            ExpectMeasureError(c, v, t => c.Vm.DepthText = t, "Derinlik", "Derinlik");
            c.Vm.DepthText = "";
        }
    });

    // F-MEAS-005 (bilgi: üst sınır yok)
    public static void Fonk_MEAS005_TinyAndHugeValues() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        foreach (var v in new[] { "0.0001", "1e-9" })
        {
            c.Vm.DepthText = v;
            c.Vm.SaveMeasurements();
            Check(c.Notes.Last().Kind == StatusKind.Success, $"'{v}' kabul edilmeli: {c.Notes.Last().Message}");
        }
        c.Vm.DepthText = "1e308";
        c.Vm.SaveMeasurements();
        Console.WriteLine($"        BİLGİ F-MEAS-005: 1e308 {(c.Notes.Last().Kind == StatusKind.Success ? "KABUL EDİLDİ (üst sınır yok; ürün kararı)" : "reddedildi")}");
    });

    // F-MEAS-006
    public static void Fonk_MEAS006_AngleRange() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        foreach (var v in new[] { "0", "360" })
        {
            c.Vm.AngleText = v;
            c.Vm.SaveMeasurements();
            Check(c.Notes.Last().Kind == StatusKind.Success, $"açı {v} kabul edilmeli: {c.Notes.Last().Message}");
        }
        foreach (var v in new[] { "-0.01", "360.01" })
            ExpectMeasureError(c, v, t => c.Vm.AngleText = t, "Açı", "Açı 0 ile 360 derece arasında olmalı.");
    });

    // F-MEAS-007
    public static void Fonk_MEAS007_ColorLength() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.ColorText = new string('k', 40);
        c.Vm.SaveMeasurements();
        Check(c.Notes.Last().Kind == StatusKind.Success, $"40 karakter kabul: {c.Notes.Last().Message}");
        ExpectMeasureError(c, new string('k', 41), t => c.Vm.ColorText = t, "Renk", "Renk en fazla 40 karakter olabilir.");
        c.Vm.ColorText = "   ";
        c.Vm.SaveMeasurements();
        Check(Load(c, 1).Color is null, "yalnızca boşluk → null");
    });

    // F-MEAS-009
    public static void Fonk_MEAS009_ThousandSeparatorsNeverSilentlyWrong()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "en-US" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                With(c =>
                {
                    c.Open();
                    c.Vm.AddDefect("Dent");
                    foreach (var v in new[] { "1.234,5", "1,234.5", "1 234" })
                    {
                        var path = Path.Combine(DefectsDir(c), "1.json");
                        var before = File.ReadAllText(path);
                        c.Vm.DepthText = v;
                        c.Vm.SaveMeasurements();
                        var d = Load(c, 1);
                        Check(File.ReadAllText(path) == before && LastIsError(c),
                            $"[{culture}] '{v}' sessizce kabul edildi: D={d.Depth?.ToString(CultureInfo.InvariantCulture)}");
                    }
                });
            }
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    // F-MEAS-010
    public static void Fonk_MEAS010_EmptyFormSavesNothing() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.SaveMeasurements();
        Check(c.Notes.Last().Message == "#1 ölçümleri kayıtlı.", c.Notes.Last().Message);
        Check(Load(c, 1).MeasurementEdits.Count == 0 && c.Vm.SelectedStep == InspectionStep.Measure, "düzeltme yok, sekme değişmez");
    });

    // F-MEAS-012
    public static void Fonk_MEAS012_NoChangeNoWrite() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.2";
        c.Vm.SaveMeasurements();
        var path = Path.Combine(DefectsDir(c), "1.json");
        var stamp = File.GetLastWriteTimeUtc(path);
        var audits = c.Audit.Entries.Count;
        Thread.Sleep(50);
        c.Vm.AutoSave();
        Check(File.GetLastWriteTimeUtc(path) == stamp && c.Audit.Entries.Count == audits, "dosya ve denetim değişmemeli");
    });

    // F-MEAS-014
    public static void Fonk_MEAS014_InvalidTextBlocksAutoSaveThenFixSaves() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "x";
        c.Vm.AutoSave();
        Check(LastIsError(c) && Load(c, 1).Depth is null && c.Vm.DepthText == "x", "hata, kayıt yok, kutu 'x' kalmalı");
        c.Vm.DepthText = "0.2";
        c.Vm.AutoSave();
        Check(Load(c, 1).Depth == 0.2, "düzeltilince kaydedilmeli");
    });

    // F-MEAS-019
    public static void Fonk_MEAS019_SaveWithoutSelection() => With(c =>
    {
        c.Open();
        c.Vm.SaveMeasurements();
        Check(LastIsError(c) && c.Notes.Last().Message == "Önce listeden bir kusur seçin.", c.Notes.Last().Message);
    });

    // F-HM-007
    public static void Fonk_HM007_HighMetalWithInvalidDepthNothingWritten() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "x";
        c.Vm.HighMetal = true;
        Check(LastIsError(c) && !Load(c, 1).HighMetal, "hata bildirilmeli, hiçbir şey yazılmamalı");
        c.Vm.DepthText = "0.2";
        c.Vm.AutoSave();
        var d = Load(c, 1);
        Check(d.Depth == 0.2 && d.HighMetal && c.Vm.HighMetal, "D ve high metal birlikte kaydedilmeli");
    });

    // F-PHOTO-005
    public static void Fonk_PHOTO005_AllowedExtensions() => WithRoot(root =>
    {
        var folder = Path.Combine(root, "s");
        Directory.CreateDirectory(folder);
        var bytes = JpegBytes;
        foreach (var ext in new[] { ".bmp", ".gif", ".webp" })
        {
            var p = Path.Combine(root, "x" + ext);
            File.WriteAllBytes(p, bytes);
            var ex = Assert.Throws<InvalidDataException>(() => PhotoImporter.Import(folder, 1, p));
            Check(ex.Message == "Yalnızca JPG ve PNG dosyaları eklenebilir.", $"{ext}: {ex.Message}");
        }
        foreach (var name in new[] { "OK.JPG", "ok.jpeg" })
        {
            var p = Path.Combine(root, name);
            File.WriteAllBytes(p, bytes);
            PhotoImporter.Import(folder, 1, p);
        }
    });

    // F-PHOTO-004
    public static void Fonk_PHOTO004_SizeLimitBoundary() => WithRoot(root =>
    {
        var folder = Path.Combine(root, "s");
        Directory.CreateDirectory(folder);
        void Make(string name, long size)
        {
            using var fs = File.Create(Path.Combine(root, name));
            fs.Write(new byte[] { 0xFF, 0xD8, 0xFF }, 0, 3);
            fs.SetLength(size);
        }
        Make("big.jpg", 25L * 1024 * 1024 + 1);
        Make("exact.jpg", 25L * 1024 * 1024);
        var ex = Assert.Throws<InvalidDataException>(() => PhotoImporter.Import(folder, 1, Path.Combine(root, "big.jpg")));
        Check(ex.Message == "Fotoğraf 25 MB sınırını aşıyor.", ex.Message);
        Check(PhotoImporter.Import(folder, 1, Path.Combine(root, "exact.jpg")) == "1_photo_01.jpg", "tam 25 MB kabul edilmeli");
    });

    // F-PHOTO-006
    public static void Fonk_PHOTO006_PickedFileDeleted() => With(c =>
    {
        AddMeasuredDent(c);
        var p = PhotoFile("x");
        File.Delete(p);
        c.Picker.Picks.Enqueue(p);
        Wait(c.Vm.AddPhotoFromFileAsync());
        Check(LastIsError(c) && c.Notes.Last().Message == "Fotoğraf bulunamadı.", c.Notes.Last().Message);
    });

    // F-PHOTO-007
    public static void Fonk_PHOTO007_SequentialNames() => With(c =>
    {
        AddMeasuredDent(c);
        var png = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 });
        foreach (var p in new[] { PhotoFile("1"), png, PhotoFile("3") })
        {
            c.Picker.Picks.Enqueue(p);
            Wait(c.Vm.AddPhotoFromFileAsync());
        }
        Check(Load(c, 1).Photos.SequenceEqual(new[] { "1_photo_01.jpg", "1_photo_02.png", "1_photo_03.jpg" }), string.Join(",", Load(c, 1).Photos));
        Check(c.Vm.Photos.Select(p => p.Label).SequenceEqual(new[] { "1.1", "1.2", "1.3" }), "etiketler");
    });

    // F-PHOTO-012
    public static void Fonk_PHOTO012_BadPhotoNamesIgnored() => With(c =>
    {
        AddMeasuredDent(c);
        Wait(c.Vm.RemovePhotoAsync("../x.jpg"));
        Wait(c.Vm.RemovePhotoAsync("abc.jpg"));
        Check(c.Dialogs.PhotoRequests.Count == 0, "diyalog açılmamalı");
    });

    // F-PHOTO-014
    public static void Fonk_PHOTO014_MissingCleanCopyShowsOriginal() => With(c =>
    {
        AddMeasuredDent(c);
        c.Picker.Picks.Enqueue(PhotoFile("p"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        c.Store.UpdateDefect(c.Folder, 1, d => d.Markups["1_photo_01.jpg"] = new PhotoMarkup { ImageWidth = 10, ImageHeight = 10, CleanedFile = "1_photo_01_clean.jpg" });
        c.Vm.Refresh(1);
        var card = c.Vm.Photos.Single();
        Check(card.DisplayFile == "1_photo_01.jpg" && !card.InfoText.Contains("arka plan"), $"Kart: {card.DisplayFile} / {card.InfoText}");
    });

    // F-RWK-007
    public static void Fonk_RWK007_NegativeDepthOnReinspectRejected() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        var before = File.ReadAllText(Path.Combine(DefectsDir(c), "1.json"));
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, new MeasurementSnapshot(-1, null, null, null, null, null), null, null, null));
        Wait(c.Vm.ReinspectAsync());
        Check(LastIsError(c) && c.Notes.Last().Message.Contains("Derinlik pozitif bir sayı olmalı."), c.Notes.Last().Message);
        Check(File.ReadAllText(Path.Combine(DefectsDir(c), "1.json")) == before && StateOf(c, 1) == DefectState.AwaitingReinspect, "hiçbir şey yazılmamalı");
    });

    // F-DEC-006
    public static void Fonk_DEC006_BlankReworkTargetUsesSessionOperation() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput("   ", null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
        Check(Load(c, 1).Dispositions.Last().TargetOperation == "OP40", $"Hedef: {Load(c, 1).Dispositions.Last().TargetOperation}");
    });

    // F-DEC-016
    public static void Fonk_DEC016_PendingDepthSavedBeforeDecision() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.DepthText = "0.9";
        c.Dialogs.Decisions.Enqueue(new DecisionInput("OP50", null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
        Check(Load(c, 1).Dispositions.Last().Snapshot?.Depth == 0.9, "Snapshot'ta D 0.9 olmalı");
    });

    // F-DEC-021 + F-UNDO-009
    public static void Fonk_DEC021_DeciderAndUndoerAreCurrentUser() => With(c =>
    {
        OpenAs(c, "PD170", "OP40", "SN001", "A");
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.4";
        c.Vm.SaveMeasurements();
        c.Vm.ChangeSession();
        OpenAs(c, "PD170", "OP40", "SN001", "B");
        c.Vm.Select(1);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(Load(c, 1).Dispositions.Last().DecidedBy == "B" && c.Audit.Entries.Last(e => e.Contains("|disposition|")).StartsWith("B|"), "karar B adına");
        c.Dialogs.UndoReasons.Enqueue("yanlış");
        Wait(c.Vm.UndoLastAsync());
        Check(Load(c, 1).Dispositions.Last().DecidedBy == "B" && StateOf(c, 1) == DefectState.Open, "geri alma B adına, durum Açık");
    });

    // F-UNDO-006 / 007 / 008
    public static void Fonk_UNDO006_UndoReopenAndChain() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, "yanlış kabul", null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reopen)));
        Check(StateOf(c, 1) == DefectState.Open, "Reopen sonrası Açık");
        c.Dialogs.UndoReasons.Enqueue("reopen yanlıştı");
        Wait(c.Vm.UndoLastAsync());
        Check(StateOf(c, 1) == DefectState.Closed, "Reopen geri alınınca Kapandı");

        // Rework -> Undo -> Accept'in geri alınması (zincir)
        c.Dialogs.UndoReasons.Enqueue("kabul yanlıştı");
        Wait(c.Vm.UndoLastAsync());
        Check(StateOf(c, 1) == DefectState.Open, "Accept geri alınınca Açık");
        SendToRework(c);
        c.Dialogs.UndoReasons.Enqueue("rework yanlıştı");
        Wait(c.Vm.UndoLastAsync());
        Check(StateOf(c, 1) == DefectState.Open, "Rework geri alınınca Açık");
        var undos = Load(c, 1).Dispositions.Count(d => d.Decision == Decision.Undo);
        Check(undos == 3, $"3 ayrı geri alma satırı beklenir: {undos}");
    });

    public static void Fonk_UNDO008_NothingToUndo() => With(c =>
    {
        AddMeasuredDent(c);
        Check(!c.Vm.CanUndo, "CanUndo false olmalı");
        Wait(c.Vm.UndoLastAsync());
        Check(LastIsError(c) && c.Notes.Last().Message == "Geri alınacak karar yok.", c.Notes.Last().Message);
    });

    // F-COMP-006
    public static void Fonk_COMP006_PendingTextSavedBeforeComplete() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R", null, null));
        c.Vm.DepthText = "0.7";
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(Load(c, 1).Depth == 0.7, "değer diskte olmalı");
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "tamamlanmalı");
    });

    // F-ID-002
    public static void Fonk_ID002_FakeInspectorTextIgnoredWhenWindowsIdentityOn() => With(c =>
    {
        SharedConfig.SaveUseWindowsUser(c.Store.Root, true);
        c.Vm.RefreshIdentity();
        c.Vm.PartNumber = "PD170"; c.Vm.OperationNumber = "OP40"; c.Vm.SerialNumber = "SN001";
        c.Vm.InspectorName = "Sahte";
        c.Vm.OpenSession();
        Check(c.Vm.User == "Ali Kaymaz (12345)" && c.Store.LoadSession(c.Folder)!.Inspector == "Ali Kaymaz (12345)", $"Kullanıcı: {c.Vm.User}");
        c.Vm.AddDefect("Dent");
        Check(c.Audit.Entries.Last(e => e.Contains("|defect.add|")).StartsWith("Ali Kaymaz (12345)|"), "denetim Windows adıyla");
    });

    // F-CONC-005
    public static void Fonk_CONC005_StaleScreenDecisionRejected() => WithRoot(root =>
    {
        var a = NewCtx(root);
        var b = NewCtx(root);
        a.Open();
        a.Vm.AddDefect("Dent");
        a.Vm.DepthText = "0.4";
        a.Vm.SaveMeasurements();
        b.Open();
        b.Vm.Select(1);
        a.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R", null, null));
        Wait(a.Vm.GiveDecisionAsync(Option(a, Decision.Accept)));

        var staleRework = b.Vm.DecisionOptions.First(o => o.Decision == Decision.Rework);
        b.Dialogs.Decisions.Enqueue(new DecisionInput("OP50", null, null, null));
        Wait(b.Vm.GiveDecisionAsync(staleRework));
        Check(LastIsError(b) && b.Notes.Last().Message.Contains("'Kapandı' durumunda 'Rework' kararı verilemez."), b.Notes.Last().Message);
        Check(a.Store.LoadDefects(a.Folder)[0].Dispositions.Count == 1, "ikinci karar yazılmamalı");
    });

    // F-NET-001 [YENİ B-23]: kök yokken kusur ekleme hata vermeli; FileLock.Acquire oturum klasörünü yeniden oluşturur
    public static void Fonk_NET001_RootGoneAddDefectFailsAndDoesNotRecreate() => WithRoot(root =>
    {
        var c = NewCtx(root);
        c.Open();
        c.Vm.AddDefect("Dent");
        Directory.Move(root, root + "_gone");
        var ex = Escaped(() => c.Vm.AddDefect("Dent"));
        var recreated = Directory.Exists(root);
        Check(ex is null && LastIsError(c) && !recreated,
            $"B-23 doğrulandı: kök yokken kusur eklendi (istisna={ex}, son bildirim={c.Notes.Last().Kind}, kök/oturum klasörü yeniden oluşturuldu={recreated})");
    });

    // F-NET-002: kutudaki değer kalır, geri gelince kaydedilir
    public static void Fonk_NET002_RootGoneAutoSaveKeepsTextThenSaves() => WithRoot(root =>
    {
        var c = NewCtx(root);
        c.Open();
        c.Vm.AddDefect("Dent");
        Directory.Move(root, root + "_gone");
        c.Vm.DepthText = "0.3";
        var ex = Escaped(c.Vm.AutoSave);
        Check(ex is null && LastIsError(c) && c.Vm.DepthText == "0.3", $"hata, değer kutuda kalmalı (istisna: {ex})");
        // FileLock yan etkisi (B-23): boş klasör ağacı yeniden oluşmuş olabilir; geri adlandırmadan önce temizlenir.
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.Move(root + "_gone", root);
        c.Vm.AutoSave();
        Check(c.Store.LoadDefects(c.Folder).First(d => d.Id == 1).Depth == 0.3, "geri gelince kaydedilmeli");
        Check(c.Vm.AddDefect("Nick"), "yeniden başlatmadan çalışmalı");
    });

    // F-NET-004 [YENİ B-23]: kök yokken rapor hata vermeli
    public static void Fonk_NET004_ReportWithRootGoneFailsAndDoesNotRecreate() => WithRoot(root =>
    {
        var c = NewCtx(root);
        c.Open();
        Directory.Move(root, root + "_gone");
        var ex = Escaped(() => Wait(c.Vm.CreateReportAsync()));
        var recreated = Directory.Exists(root);
        Check(ex is null && LastIsError(c) && !c.Vm.IsReportRunning && !recreated,
            $"B-23 doğrulandı: kök yokken rapor üretildi (istisna={ex}, son bildirim={c.Notes.Last().Kind}, kök yeniden oluşturuldu={recreated}, rapor sayısı yeni kökte={(recreated ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0)})");
    });

    // F-NET-007
    public static void Fonk_NET007_AddPhotoWithRootGone() => WithRoot(root =>
    {
        var c = NewCtx(root);
        c.Open();
        c.Vm.AddDefect("Dent");
        var src = PhotoFile("n");
        Directory.Move(root, root + "_gone");
        c.Picker.Picks.Enqueue(src);
        var ex = Escaped(() => Wait(c.Vm.AddPhotoFromFileAsync()));
        Check(ex is null && LastIsError(c) && File.Exists(src), $"Hata bildirilmeli, kaynak dosya durmalı (istisna: {ex})");
    });

    // F-RPT-012
    public static void Fonk_RPT012_ReportsIsAFile() => With(c =>
    {
        c.Open();
        File.WriteAllText(Path.Combine(c.Folder, "reports"), "dosya");
        var ex = Escaped(() => Wait(c.Vm.CreateReportAsync()));
        Check(ex is null && LastIsError(c) && !c.Vm.IsReportRunning, $"Hata, düğme açılmalı (istisna: {ex})");
    });

    // F-RPT-007 / 008 / 009 / 011 / 015
    public static void Fonk_RPT_VariousSessions() => With(c =>
    {
        c.Open();
        Wait(c.Vm.CreateReportAsync()); // kusursuz oturum
        Check(c.Notes.Any(n => n.Message.StartsWith("Rapor v1")) && Directory.GetFiles(Path.Combine(c.Folder, "reports"), "Rapor_v1.pdf").Length == 1, "kusursuz oturumda rapor");

        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.2";
        c.Vm.SaveMeasurements();
        c.Picker.Picks.Enqueue(PhotoFile("r"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        // diskte olmayan foto
        File.Delete(Path.Combine(DefectsDir(c), "1_photo_01.jpg"));
        Wait(c.Vm.CreateReportAsync());
        Check(!LastIsError(c) && File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v2.pdf")), $"açık kusurlu + eksik foto: {c.Notes.Last().Message}");

        // eski HTML v1 kalmış, PDF yok
        File.WriteAllText(Path.Combine(c.Folder, "reports", "Rapor_v3.html"), "<html/>");
        Wait(c.Vm.CreateReportAsync());
        Check(File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v4.pdf")) && File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v3.html")), "v4 üretilmeli, v3.html silinmemeli");

        // kaldırılan fotoğraf rapora girmez
        c.Store.UpdateDefect(c.Folder, 1, d => { d.Photos.Add("1_photo_09.jpg"); });
        c.Dialogs.PhotoConfirms.Enqueue(true);
        Wait(c.Vm.RemovePhotoAsync("1_photo_09.jpg"));
        Wait(c.Vm.CreateReportAsync());
        Check(c.Pdf.Calls.Count == 4 && !File.ReadAllText(Path.Combine(c.Folder, "reports", "Rapor_v4.pdf")).Contains("photo_09"), "kaldırılan foto");
    });

    // F-ADM-... yok; F-SET-006
    public static void Fonk_SET006_InvalidIndicesIgnored()
    {
        var s = new FakeUserSettings();
        var vm = new UserSettingsViewModel(s);
        var raised = 0;
        vm.PropertyChanged += (_, _) => raised++;
        vm.UnitIndex = -1;
        vm.ThemeIndex = 2;
        Check(s.Unit == LengthUnit.Mm && s.Theme == AppTheme.Light, "ayar değişmemeli");
    }

    // F-DEF-006 benzeri F-SET: yok. F-CAM-011
    public static void Fonk_CAM011_CameraTargetIsFixedToOriginalDefect() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.AddDefect("Nick"); // #2 seçili
        c.Vm.Select(1);
        c.Vm.OpenCamera();
        var cam = c.Windows.Cameras.Single();
        c.Vm.Select(2);
        var tmp = PhotoFile("cam");
        var name = cam.OnSave(new CapturedPhotoData(tmp, 0, 0, false, 0));
        Check(Load(c, 1).Photos.Contains(name) && Load(c, 2).Photos.Count == 0, "foto #1'e eklenmeli");
        cam.OnClosed(name);
        Check(c.Windows.Editors.Count == 0, "#2 seçiliyken düzenleyici açılmamalı");
    });

    // F-EDIT-017
    public static void Fonk_EDIT017_EditorSaveGoesToOriginalDefect() => With(c =>
    {
        AddMeasuredDent(c);
        c.Picker.Picks.Enqueue(PhotoFile("e"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        c.Vm.AddDefect("Nick");
        c.Vm.Select(1);
        c.Vm.OpenEditor("1_photo_01.jpg");
        var editor = c.Windows.Editors.Single();
        c.Vm.Select(2);
        editor.OnSave(new PhotoEditResultData(new PhotoMarkup { ImageWidth = 100, ImageHeight = 100, Shapes = { Annotation.CenterSquare(100, 100, 1) } }, null));
        Check(Load(c, 1).Markups.ContainsKey("1_photo_01.jpg") && c.Vm.SelectedId == 2, "işaret #1'e yazılmalı, seçim #2 kalmalı");
    });

    // F-COMP-001 + F-CONC...: tamamlama engeli
    public static void Fonk_COMP001_OpenDefectsBlockComplete() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.AddDefect("Nick");
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "M1", null, null));
        c.Vm.Select(2);
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.SubmitMrb)));
        c.Vm.Complete();
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message == "Muayene kapatılamaz, açık kusurlar var: #1 (Açık), #2 (MRB bekliyor)", c.Notes.Last().Message);
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Open, "durum değişmemeli");
    });

    // F-E2E-009
    public static void Fonk_E2E009_RepairPaths() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Repair)));
        Check(StateOf(c, 1) == DefectState.Repairing, "Repair");
        c.Dialogs.Decisions.Enqueue(new DecisionInput("CTP", null, null, null));
        c.Dialogs.ReinspectNow.Enqueue(true);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reinspect)));
        Check(StateOf(c, 1) == DefectState.Closed, $"Reinspect->Giderildi: {StateOf(c, 1)}");

        c.Vm.AddDefect("Nick");
        c.Vm.DepthText = "0.1";
        c.Vm.SaveMeasurements();
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Repair)));
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "SPA-2", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(StateOf(c, 2) == DefectState.Closed, "Repair->Kabul");
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "tamamlanmalı");
    });

    // F-E2E-001 (VM zinciri)
    public static void Fonk_E2E001_FullHappyPath() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.12"; c.Vm.WidthText = "1.5"; c.Vm.LengthText = "3";
        c.Vm.SaveMeasurements();
        c.Picker.Picks.Enqueue(PhotoFile("e2e"));
        Wait(c.Vm.AddPhotoFromFileAsync());
        c.Vm.OpenEditor("1_photo_01.jpg");
        c.Windows.Editors.Single().OnSave(new PhotoEditResultData(new PhotoMarkup { ImageWidth = 100, ImageHeight = 100, Shapes = { Annotation.CenterSquare(100, 100, 1) } }, null));
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "SPA-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        c.Vm.Complete();
        Wait(c.Vm.CreateReportAsync());
        Check(StateOf(c, 1) == DefectState.Closed && c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "Kapandı + Tamamlandı");
        Check(File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v1.pdf")), "PDF");
        var order = new[] { "session.open", "defect.add", "defect.measure", "defect.photo", "defect.markup", "disposition", "session.complete", "report.create" };
        var actions = c.Audit.Entries.Select(e => e.Split('|')[1]).ToList();
        var idx = -1;
        foreach (var a in order)
        {
            var next = actions.FindIndex(idx + 1, x => x == a);
            Check(next > idx, $"denetim sırası: '{a}' bulunamadı ({string.Join(",", actions)})");
            idx = next;
        }
    });

    // F-E2E-007
    public static void Fonk_E2E007_VoidWrongTypeThenDent() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Nick");
        c.Dialogs.VoidReasons.Enqueue("yanlış tip");
        Wait(c.Vm.VoidAsync());
        c.Vm.AddDefect("Dent");
        Check(c.Vm.SelectedId == 2, "Dent #2 olmalı");
        c.Vm.DepthText = "0.1";
        c.Vm.SaveMeasurements();
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "R", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "tamamlama geçmeli");
        Wait(c.Vm.CreateReportAsync());
        Check(c.Vm.DefectCountText == "Kusurlar (1)", c.Vm.DefectCountText);
    });

    // F-E2E-008
    public static void Fonk_E2E008_VendorFlow() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.ReturnToVendor)));
        c.Vm.Complete();
        Check(c.Notes.Last().Kind == StatusKind.Warning, "tedarikçide açık sayılmalı");
        c.Dialogs.Decisions.Enqueue(new DecisionInput("CTP", null, null, null));
        c.Dialogs.ReinspectNow.Enqueue(true);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reinspect)));
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "kapanınca tamamlanmalı");
    });

    // F-E2E-016
    public static void Fonk_E2E016_ReopenThenReworkConforms() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "SPA-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, "yanlış kabul", null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reopen)));
        c.Dialogs.Decisions.Enqueue(new DecisionInput("OP50", null, null, null));
        c.Dialogs.ReinspectNow.Enqueue(true);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
        var seq = Load(c, 1).Dispositions.Select(d => d.Decision).ToList();
        Check(seq.SequenceEqual(new[] { Decision.Accept, Decision.Reopen, Decision.Rework, Decision.AfterReworkConforms }) && StateOf(c, 1) == DefectState.Closed,
            string.Join(">", seq));
    });

    // F-E2E-005
    public static void Fonk_E2E005_MrbRejectedFlow() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "MRB-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.SubmitMrb)));
        c.Vm.Complete();
        Check(c.Notes.Last().Kind == StatusKind.Warning, "MRB beklerken tamamlama engellenmeli");
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "MRB-1", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.MrbRejected)));
        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "tamamlanmalı");
        Check(!c.Vm.DecisionOptions.Any(o => o.Decision == Decision.Reopen), "Yeniden aç seçeneği olmamalı");
        Check(Load(c, 1).Dispositions.Last().Source == DecisionSource.System, "otomatik hurda (System)");
    });
}

/// <summary>Küçük bir Assert yardımcısı (dış paket yok).</summary>
internal static class Assert
{
    public static T Throws<T>(Action a) where T : Exception
    {
        try { a(); }
        catch (T ex) { return ex; }
        catch (Exception ex) { throw new Exception($"{typeof(T).Name} beklendi, {ex.GetType().Name} geldi: {ex.Message}"); }
        throw new Exception($"{typeof(T).Name} beklendi, istisna yok");
    }
}
