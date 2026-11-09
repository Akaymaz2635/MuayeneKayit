using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Fonksiyonel test planı: Admin ViewModel testleri (docs/test/FONKSIYONEL-TEST-PLANI.md).
public static partial class AdminViewModelTests
{
    private static string? AdminEscaped(Action a)
    {
        try { a(); return null; }
        catch (Exception ex) { return ex.GetType().Name; }
    }

    private static string AdminFile(Ctx c) => Path.Combine(SharedConfig.Folder(c.Root), "admin.json");

    // ═════════════════════════ ŞÜPHELİ ═════════════════════════

    // B-02 F-CFG-008
    public static void Fonk_CFG008_CorruptAdminJsonDoesNotCrashLogin() => With(c =>
    {
        var problems = new List<string>();
        Directory.CreateDirectory(SharedConfig.Folder(c.Root));

        foreach (var (label, json) in new[]
                 {
                     ("çöp metin", "bu json değil"),
                     ("geçersiz Base64 tuz", "{\"salt\":\"%%%\",\"hash\":\"AAAA\",\"iterations\":200000}"),
                 })
        {
            File.WriteAllText(AdminFile(c), json);
            c.Shell.Lock(null);
            c.Login.Pin = "123456";
            var ex = AdminEscaped(c.Login.Login);
            if (ex is not null)
                problems.Add($"{label}: {ex} Login()'den dışarı kaçtı (uygulama çöker)");
            else if (c.Login.Error.Length == 0)
                problems.Add($"{label}: LoginError boş");
        }
        if (problems.Count > 0)
            throw new Exception("B-02 doğrulandı: " + string.Join(" | ", problems));
    });

    // B-11 F-ADM-TYPE-016
    public static void Fonk_TYPE016_RenameWithoutApplyIsNotSilentlyLost() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = 0;
        c.Types.Name = "Yeniden Adlandı";
        c.Types.Save();
        var saved = SharedConfig.LoadDefectTypes(c.Root).Any(t => t.Name == "Yeniden Adlandı");
        var warned = c.Notes.Any(n => n.Kind == StatusKind.Warning && n.Message.Contains("uygulanmamış", StringComparison.OrdinalIgnoreCase));
        Check(saved || warned,
            $"B-11 doğrulandı: editördeki ad değişikliği ne kaydedildi ne uyarıldı; son bildirim: {c.Notes.Last().Kind} '{c.Notes.Last().Message}'");
    });

    // B-09 F-ADM-SET-005
    public static void Fonk_SET005_RelativeRootRejected() => With(c =>
    {
        c.SignIn();
        var cwd = Directory.GetCurrentDirectory();
        var parent = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(parent, "rel"));
        try
        {
            Directory.SetCurrentDirectory(parent);
            foreach (var rel in new[] { "rel", "." })
            {
                c.SettingsVm.RootText = rel;
                c.SettingsVm.SaveRoot();
                Check(c.Settings.Saved.Count == 0,
                    $"B-09 doğrulandı: göreli yol '{rel}' kabul edilip kaydedildi (çalışma klasörüne göre çözülür)");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            try { Directory.Delete(parent, true); } catch (IOException) { }
        }
    });

    // B-19 F-ADM-LOGIN-013: ürün kararı. admin.json yokken ama kökte önceki admin etkinliği varken PIN sessizce yeniden
    // belirlenemez: uyarı + açık onay istenir ve "admin.pin.reset" denetim satırı yazılır.
    public static void Fonk_LOGIN013_DeletedAdminJsonNeedsConfirmedReset() => With(c =>
    {
        c.SignIn();                       // admin.login denetim satırı: kökte önceki admin etkinliği var
        File.Delete(AdminFile(c));
        c.Shell.Lock(null);
        Check(c.Login.NeedsResetConfirm && c.Login.Hint.StartsWith("UYARI") && c.Login.ButtonText == "PIN'i sıfırla ve gir",
            $"sıfırlama uyarısı ve onay beklenir (NeedsResetConfirm={c.Login.NeedsResetConfirm}, düğme='{c.Login.ButtonText}')");

        c.Login.Pin = NewPin8;
        c.Login.PinRepeat = NewPin8;
        c.Login.Login();
        Check(!c.Shell.IsLoggedIn && !SharedConfig.HasPin(c.Root) && c.Login.Error.Contains("onay"),
            $"onay verilmeden PIN belirlenmemeli (hata='{c.Login.Error}')");

        c.Login.ResetConfirmed = true;
        c.Login.Login();
        Check(c.Shell.IsLoggedIn && SharedConfig.VerifyPin(c.Root, NewPin8), $"onaydan sonra yeni PIN ile giriş (hata='{c.Login.Error}')");
        Check(c.AuditActions().Contains("admin.pin.reset"), "denetim kaydında admin.pin.reset olmalı: " + string.Join(",", c.AuditActions()));
    });

    // B-19 (karşıt durum): kökte hiç admin etkinliği yoksa gerçek ilk kurulumdur, onay istenmez.
    public static void Fonk_LOGIN013_FreshRootIsPlainFirstRun() => With(c =>
    {
        Check(c.Login.IsFirstRun && !c.Login.NeedsResetConfirm, "temiz kökte onaysız ilk kurulum");
    });

    // B-16 F-ADM-LOGIN-008
    public static void Fonk_LOGIN008_LockoutSurvivesNewLoginViewModel() => With(c =>
    {
        c.SetupPin();
        for (var i = 0; i < 5; i++)
        {
            c.Login.Pin = "000000";
            c.Login.Login();
        }
        Check(c.Login.Error.Contains("Çok fazla") || c.Login.Error == "PIN hatalı.", "ön koşul: 5 yanlış deneme");
        var fresh = new LoginViewModel(c.Context, c.Clock);
        fresh.Pin = Pin;
        fresh.Login();
        Check(fresh.Error.Contains("Çok fazla"),
            $"B-16 doğrulandı: yeni LoginViewModel (uygulama yeniden başlatma) kilidi sıfırladı; doğru PIN ile giriş yapıldı (hata='{fresh.Error}')");
    });

    // B-22 F-NET-006
    public static void Fonk_NET006_RootGoneAdminDoesNotOfferFirstRun() => With(c =>
    {
        c.SetupPin();
        Directory.Move(c.Root, c.Root + "_gone");
        try
        {
            c.Shell.Lock(null);
            var offersFirstRun = c.Login.IsFirstRun;
            c.Login.Pin = Pin;
            c.Login.PinRepeat = Pin;
            c.Login.Login();
            var recreated = Directory.Exists(c.Root);
            Check(!offersFirstRun && !recreated,
                $"B-22 doğrulandı: kök erişilemezken admin 'İlk kurulum' sunuyor={offersFirstRun}, giriş sonrası kök yeniden oluşturuldu={recreated}, giriş yapıldı={c.Shell.IsLoggedIn}");
        }
        finally
        {
            try { if (Directory.Exists(c.Root)) Directory.Delete(c.Root, true); } catch (IOException) { }
            if (Directory.Exists(c.Root + "_gone"))
                Directory.Move(c.Root + "_gone", c.Root);
        }
    });

    // ═════════════════════════ ANA LİSTE ═════════════════════════

    // F-ADM-LOGIN-014
    public static void Fonk_LOGIN014_WeakIterationsRejected() => With(c =>
    {
        c.SetupPin();
        var rec = System.Text.Json.JsonSerializer.Deserialize<AdminPinRecord>(File.ReadAllText(AdminFile(c)), SessionStore.JsonOptions)!;
        rec.Iterations = 5000;
        rec.Hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(System.Text.Encoding.UTF8.GetBytes(Pin), Convert.FromBase64String(rec.Salt), 5000, HashAlgorithmName.SHA256, 32));
        File.WriteAllText(AdminFile(c), System.Text.Json.JsonSerializer.Serialize(rec, SessionStore.JsonOptions));
        c.Login.Pin = Pin;
        c.Login.Login();
        // PBKDF2 tur sayısı 10 000-5 000 000 dışındaki kayıt (5000) kabul edilmez; açık hata verilir.
        Check(c.Login.Error.Contains("tur sayısı (5000)") && !c.Shell.IsLoggedIn, $"zayıf kayıt kabul edilmemeli: '{c.Login.Error}'");
    });

    // F-ADM-LOGIN-003
    public static void Fonk_LOGIN003_PinFormatRules() => With(c =>
    {
        foreach (var bad in new[] { "12345", "1234567", "abc12345", "", "1234567890123" })
        {
            c.Login.Pin = bad;
            c.Login.PinRepeat = bad;
            c.Login.Login();
            Check(c.Login.Error == "PIN 8-12 haneli rakam (0-9) olmalı." && !SharedConfig.HasPin(c.Root), $"'{bad}': {c.Login.Error}");
        }
        c.Login.Pin = "123456789012";
        c.Login.PinRepeat = "123456789012";
        c.Login.Login();
        Check(c.Shell.IsLoggedIn && SharedConfig.VerifyPin(c.Root, "123456789012"), "12 hane kabul edilmeli");
    });

    private static void TypeError(Ctx c, string contains)
    {
        Check(c.Notes.Count > 0 && c.Notes.Last().Kind == StatusKind.Error && c.Notes.Last().Message.Contains(contains),
            $"'{contains}' hatası beklenirdi: {(c.Notes.Count > 0 ? c.Notes.Last().Message : "(bildirim yok)")}");
    }

    // F-ADM-TYPE-003..006
    public static void Fonk_TYPE003_NameRules() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = 1; // Nick

        foreach (var bad in new[] { "", "   ", new string('A', 41), "Dent#" })
        {
            c.Notes.Clear();
            c.Types.SelectedIndex = 1;
            c.Types.Name = bad;
            c.Types.Apply();
            TypeError(c, "Geçersiz kusur tipi adı");
        }
        foreach (var good in new[] { new string('A', 40), "Çapak-1 (iç)" })
        {
            c.Notes.Clear();
            c.Types.SelectedIndex = 1;
            c.Types.Name = good;
            c.Types.Apply();
            Check(c.Notes.Count == 0 || c.Notes.Last().Kind != StatusKind.Error, $"'{good}' kabul edilmeli");
        }
        foreach (var dup in new[] { "dent", "Dent " })
        {
            c.Notes.Clear();
            c.Types.Load();
            c.Types.SelectedIndex = 1;
            c.Types.Name = dup;
            c.Types.Apply();
            TypeError(c, "Aynı ad iki kez var");
        }
    });

    public static void Fonk_TYPE004_SpecLength() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = 0;
        c.Types.SpecName = new string('s', 60);
        c.Types.Apply();
        Check(c.Notes.Count == 0 || c.Notes.Last().Kind != StatusKind.Error, "60 karakter kabul");
        c.Types.SelectedIndex = 0;
        c.Types.SpecName = new string('s', 61);
        c.Types.Apply();
        TypeError(c, "spec adı en fazla 60 karakter");
    });

    public static void Fonk_TYPE005_ApplyWithoutSelection() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = -1;
        c.Types.Apply();
        TypeError(c, "Listeden bir tip seçin.");
    });

    public static void Fonk_TYPE006_NoMeasureFieldsAllowed() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.SelectedIndex = 0;
        c.Types.FieldDepth = c.Types.FieldWidth = c.Types.FieldLength = false;
        c.Types.Apply();
        Check(c.Types.Items[0].Contains("ölçüm yok"), c.Types.Items[0]);
    });

    // F-ADM-TYPE-009
    public static void Fonk_TYPE009_DeleteAllThenSaveRejected() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        var n = c.Types.Items.Count;
        for (var i = 0; i < n; i++)
        {
            c.Confirm.Answers.Enqueue(true);
            c.Types.SelectedIndex = 0;
            Wait(c.Types.DeleteAsync());
        }
        c.Types.Save();
        TypeError(c, "En az bir kusur tipi olmalı.");
        Check(!File.Exists(Path.Combine(SharedConfig.Folder(c.Root), "defect_types.json")), "dosya yazılmamalı");
    });

    private static void Wait(Task t) => t.GetAwaiter().GetResult();

    // F-ADM-FIELD-006 / 007
    public static void Fonk_FIELD006_KindIndexMapping() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        var expected = new[] { "uzunluk (mm / in)", "açı (°)", "sayı (birimsiz, eksi olabilir)", "metin" };
        for (var i = 0; i < 4; i++)
        {
            c.Types.FieldName = "Alan" + i;
            c.Types.FieldAbbrev = "X" + i;
            c.Types.FieldKindIndex = i;
            c.Types.AddField();
            Check(c.Types.FieldItems.Last().Contains(expected[i]), $"tür {i}: {c.Types.FieldItems.Last()}");
        }
        c.Types.FieldName = "Alan9";
        c.Types.FieldAbbrev = "X9";
        c.Types.FieldKindIndex = 9;
        c.Types.AddField();
        Check(c.Types.FieldItems.Last().Contains("uzunluk (mm / in)"), $"geçersiz tür uzunluğa düşmeli: {c.Types.FieldItems.Last()}");
    });

    public static void Fonk_FIELD007_ApplyFieldRules() => With(c =>
    {
        c.SignIn();
        c.Types.Load();
        c.Types.FieldName = "Yükseklik"; c.Types.FieldAbbrev = "H"; c.Types.FieldKindIndex = 0;
        c.Types.AddField();
        c.Types.SelectedFieldIndex = -1;
        c.Types.ApplyField();
        TypeError(c, "Listeden bir ölçüm alanı seçin.");

        c.Types.SelectedFieldIndex = 0;
        c.Types.FieldName = "Boy";
        c.Types.FieldAbbrev = "B";
        c.Types.ApplyField();
        c.Types.Save();
        var saved = SharedConfig.LoadCustomFields(c.Root).Single();
        Check(saved.Name == "Boy" && saved.Abbrev == "B", $"{saved.Name}/{saved.Abbrev}");
    });

    // F-ADM-TXT-002..004
    public static void Fonk_TXT002_DecisionTextRules()
    {
        Check(SharedConfig.ValidateDecisionText("") == "Açıklama boş olamaz.", "boş");
        Check(SharedConfig.ValidateDecisionText(new string('a', 600)) is null, "600 kabul");
        Check(SharedConfig.ValidateDecisionText(new string('a', 601)) == "Açıklama en fazla 600 karakter olabilir.", "601 red");
        Check(SharedConfig.ValidateDecisionText("a\tb") == "Açıklama kontrol karakteri olamaz." || SharedConfig.ValidateDecisionText("a\tb") == "Açıklamada kontrol karakteri olamaz.", "sekme red");
        Check(SharedConfig.ValidateDecisionText("a\nb") is null, "satır sonu kabul");

        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => SharedConfig.SaveDecisionTexts(root,
                new Dictionary<Decision, string> { [Decision.Accept] = "" }));
            Check(ex.Message.Contains("Accept: Açıklama boş olamaz."), ex.Message);
            Check(!File.Exists(Path.Combine(SharedConfig.Folder(root), "decision_texts.json")), "dosya yazılmamalı");
        }
        finally { Directory.Delete(root, true); }
    }

    // F-ADM-MNT-003
    public static void Fonk_MNT003_AuditLoadLimitsAndSkipsBadLine() => With(c =>
    {
        var path = Path.Combine(c.Root, AuditLog.FileName);
        for (var i = 0; i < 301; i++)
            c.Context.Audit.Append("t", "a.test", "n" + i);
        // Bozuk satır, son 300 satırın ortasına eklenir (Append sonradan değil, dosya düzenlenerek).
        var all = File.ReadAllLines(path).ToList();
        all.Insert(150, "{bozuk satır");
        File.WriteAllLines(path, all);
        c.Maint.LoadAudit();
        var lines = c.Maint.AuditLines;
        Check(lines.Count <= 300 && lines.Count > 0, $"en fazla 300 satır: {lines.Count}");
        Check(lines[0].Contains("n300"), $"en yeniden eskiye: {lines[0]}");
        Check(Regex.IsMatch(lines[0], @"\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}"), $"tarih biçimi: {lines[0]}");
    });

    // F-ADM-MNT-009
    public static void Fonk_MNT009_NineMinuteLockNotStale() => With(c =>
    {
        var dir = Path.Combine(c.Root, "PD170", "OP40", "SN001");
        Directory.CreateDirectory(dir);
        var lockPath = Path.Combine(dir, "session.lock");
        File.WriteAllText(lockPath, "");
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddMinutes(-9));
        Check(SharedConfig.StaleLocks(c.Root).Count == 0, "9 dk eski kilit listelenmemeli");
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddMinutes(-11));
        Check(SharedConfig.StaleLocks(c.Root).Count == 1, "11 dk eski kilit listelenmeli");
    });

    // F-ADM-SET-003 / 004
    public static void Fonk_SET003_BlankRootRejected() => With(c =>
    {
        c.SignIn();
        foreach (var v in new[] { "", "   " })
        {
            c.SettingsVm.RootText = v;
            c.SettingsVm.SaveRoot();
            // Boş / göreli yol tam yol kuralıyla reddedilir (güvenlik düzeltmesi, B-09).
            TypeError(c, "Ortak alan yolu tam yol olmalı");
        }
        Check(c.Settings.Saved.Count == 0, "settings değişmemeli");
    });

    public static void Fonk_SET004_EnvVarRootStoredRaw() => With(c =>
    {
        c.SignIn();
        var sub = "MuayeneKayit-fonk-env-" + Guid.NewGuid().ToString("N");
        var real = Path.Combine(Path.GetTempPath(), sub);
        Directory.CreateDirectory(real);
        try
        {
            var raw = @"%TEMP%\" + sub;
            c.SettingsVm.RootText = raw;
            c.SettingsVm.SaveRoot();
            Check(c.Settings.Saved.SequenceEqual(new[] { raw }), "dosyaya ham (genişletilmemiş) metin yazılmalı");
            Check(c.Context.Root == Environment.ExpandEnvironmentVariables(raw), "bağlam genişletilmiş yolu kullanmalı");
        }
        finally { Directory.Delete(real, true); }
    });

    // F-ADM-REC-003 / 005 / 010
    public static void Fonk_REC003_ManySessionsCap() => With(c =>
    {
        c.SignIn();
        for (var i = 0; i < 600; i++)
            c.Context.Store.OpenOrCreate(new Session { PartNumber = "P" + (i % 20), OperationNumber = "OP" + (i / 20 % 30), SerialNumber = "S" + i, Inspector = "t" });
        c.Records.Refresh();
        Check(c.Records.Rows.Count == 500, $"500 kart: {c.Records.Rows.Count}");
        Check(c.Records.SummaryCards[0].Value == "600", $"özet 600: {c.Records.SummaryCards[0].Value}");
        var csv = Path.Combine(c.Root, "..", "fonk-" + Guid.NewGuid().ToString("N") + ".csv");
        c.Saver.Path = csv;
        Wait(c.Records.ExportCsvAsync());
        try
        {
            var lines = File.ReadAllLines(csv).Length;
            Check(lines >= 601, $"CSV >= 601 satır: {lines}");
        }
        finally { File.Delete(csv); }
    });

    public static void Fonk_REC005_ExportCancelAndReadOnlyTarget() => With(c =>
    {
        c.SignIn();
        Seed(c);
        c.Records.Refresh();
        c.Saver.Path = null;
        var before = c.Notes.Count;
        Wait(c.Records.ExportCsvAsync());
        Check(c.Notes.Count == before, "iptalde bildirim olmamalı");

        var ro = Path.Combine(Path.GetTempPath(), "fonk-ro-" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(ro, "x");
        File.SetAttributes(ro, FileAttributes.ReadOnly);
        try
        {
            c.Saver.Path = ro;
            var ex = AdminEscaped(() => Wait(c.Records.ExportCsvAsync()));
            Check(ex is null && c.Notes.Last().Kind == StatusKind.Error, $"Salt okunur: Error beklenir (istisna={ex})");
        }
        finally { File.SetAttributes(ro, FileAttributes.Normal); File.Delete(ro); }
    });

    public static void Fonk_REC010_RecordsPageOnlyTouchesIndexAndAudit() => With(c =>
    {
        c.SignIn();
        Seed(c);
        string Snap() => string.Join("\n", Directory.EnumerateFiles(c.Root, "*", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith("_index.json") && !p.EndsWith(AuditLog.FileName) && !p.EndsWith("_index.lock"))
            .OrderBy(p => p)
            .Select(p => p + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));
        var before = Snap();
        c.Records.Refresh();
        c.Saver.Path = Path.Combine(Path.GetTempPath(), "fonk-rec-" + Guid.NewGuid().ToString("N") + ".csv");
        Wait(c.Records.ExportCsvAsync());
        File.Delete(c.Saver.Path);
        c.Maint.RebuildIndex();
        Check(Snap() == before, "muayene kayıtları değişmemeli");
    });

    // F-CFG-021
    public static void Fonk_CFG021_NoConfigFolderDefaults() => With(c =>
    {
        Check(!Directory.Exists(SharedConfig.Folder(c.Root)), "ön koşul: _config yok");
        Check(c.Login.IsFirstRun, "ilk kurulum");
        Check(SharedConfig.LoadDefectTypes(c.Root).Count == SharedConfig.Defaults.Count, "varsayılan tipler");
        Check(SharedConfig.LoadDecisionTexts(c.Root).Count == SharedConfig.DefaultDecisionTexts.Count, "varsayılan metinler");
        c.Texts.Load();
        c.SignIn();
        Check(c.Types.Items.Count == 9, "tip listesi 9");
    });

    // F-ADM-LOGIN-009 (UNIT kısmı)
    public static void Fonk_LOGIN009_IdleLockSeconds() => With(c =>
    {
        c.SignIn();
        c.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        c.Shell.CheckIdle();
        Check(!c.Shell.IsLoggedIn && c.Login.Error == "10 dakika işlem yapılmadığı için kilitlendi.", c.Login.Error);
    });
}
