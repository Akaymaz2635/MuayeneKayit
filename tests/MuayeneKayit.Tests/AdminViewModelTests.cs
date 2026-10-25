using System.Text;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Admin uygulaması ViewModel testleri (gerçek SessionStore + geçici klasör, sahte portlar).
public static class AdminViewModelTests
{
    private const string Pin = "123456";

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private sealed class Ctx
    {
        public required AdminContext Context { get; init; }
        public required AdminShellViewModel Shell { get; init; }
        public required FakeConfirm Confirm { get; init; }
        public required FakeClock Clock { get; init; }
        public required FakeAdminSettings Settings { get; init; }
        public required FakeFileSaver Saver { get; init; }
        public required FakeShell Os { get; init; }
        public List<(string Message, StatusKind Kind)> Notes { get; } = new();

        public string Root => Context.Root;
        public LoginViewModel Login => Shell.Login;
        public RecordsViewModel Records => Shell.Records;
        public TypesViewModel Types => Shell.Types;
        public DecisionTextsViewModel Texts => Shell.Texts;
        public MaintenanceViewModel Maint => Shell.Maintenance;
        public SettingsViewModel SettingsVm => Shell.Settings;

        /// <summary>PIN'i yazar (ilk kurulumu atlar) ve giriş ekranını yeniler.</summary>
        public void SetupPin()
        {
            SharedConfig.SetPin(Root, Pin);
            Shell.Lock(null);
        }

        /// <summary>PIN ile giriş yapar.</summary>
        public void SignIn()
        {
            if (!SharedConfig.HasPin(Root))
                SetupPin();
            Login.Pin = Pin;
            Login.Login();
            Check(Shell.IsLoggedIn, $"Giriş yapılmalı: {Login.Error}");
        }

        public List<string> AuditActions() => AuditLog.ReadTail(Context.Store, 100).Select(a => a.Action).ToList();
    }

    private static void With(Action<Ctx> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-admin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var context = new AdminContext(root);
            var confirm = new FakeConfirm();
            var clock = new FakeClock();
            var settings = new FakeAdminSettings { Root = root };
            var saver = new FakeFileSaver();
            var os = new FakeShell();
            var shell = new AdminShellViewModel(
                context, clock,
                new LoginViewModel(context, clock),
                new RecordsViewModel(context, saver, os, clock),
                new TypesViewModel(context, confirm),
                new DecisionTextsViewModel(context, confirm),
                new MaintenanceViewModel(context, confirm),
                new SettingsViewModel(context, settings));
            var c = new Ctx { Context = context, Shell = shell, Confirm = confirm, Clock = clock, Settings = settings, Saver = saver, Os = os };
            shell.Notify += (m, k) => c.Notes.Add((m, k));
            body(c);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// PD170/OP40/SN-A1: Dent, MRB bekliyor. PD170/OP50/SN-A2: Scratch, kapalı, muayene tamamlandı.
    /// TS1400/OP40/SN-B1: Undercut, rework sonrası muayene bekliyor.
    /// </summary>
    private static void Seed(Ctx c)
    {
        var store = c.Context.Store;
        Session S(string part, string op, string serial) => new()
        {
            PartNumber = part, OperationNumber = op, SerialNumber = serial, Inspector = "A. Yılmaz", Project = part,
        };

        var (f1, _) = store.OpenOrCreate(S("PD170", "OP40", "SN-A1"));
        store.AddDefect(f1, "Dent", d => DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya"));

        var (f2, s2) = store.OpenOrCreate(S("PD170", "OP50", "SN-A2"));
        store.AddDefect(f2, "Scratch", d => DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya", reference: "S-1"));
        s2.Status = SessionStatus.Completed;
        store.SaveSession(f2, s2);

        var (f3, _) = store.OpenOrCreate(S("TS1400", "OP40", "SN-B1"));
        store.AddDefect(f3, "Undercut", d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
    }

    // ───────────────────────── Giriş ─────────────────────────

    public static void FirstRunPinMustMatchAndIsAudited() => With(c =>
    {
        Check(c.Login.IsFirstRun && c.Login.ButtonText == "PIN'i belirle ve gir", "İlk kurulum metni");
        Check(c.Login.Hint.StartsWith("İlk kurulum: admin PIN'i belirleyin (6-12 rakam)."), "İlk kurulum ipucu");
        Check(c.Login.RootText == $"Ortak alan: {c.Root}", "Kök metni");

        c.Login.Pin = Pin;
        c.Login.PinRepeat = "654321";
        c.Login.Login();
        Check(c.Login.Error == "PIN'ler aynı değil." && !c.Shell.IsLoggedIn && !SharedConfig.HasPin(c.Root), "Eşleşmeyen PIN reddedilmeli");

        c.Login.PinRepeat = Pin;
        c.Login.Login();
        Check(c.Shell.IsLoggedIn && SharedConfig.HasPin(c.Root) && SharedConfig.VerifyPin(c.Root, Pin), "PIN yazılmalı, giriş yapılmalı");
        var actions = c.AuditActions();
        Check(actions.Contains("admin.pin.set") && actions.Contains("admin.login"), $"Denetim satırları: {string.Join(",", actions)}");

        // Kısa PIN kuralı Core'dan gelir ve hata satırında görünür.
        c.Shell.Lock(null);
        Check(!c.Login.IsFirstRun && c.Login.ButtonText == "Giriş" && c.Login.Hint == "Devam etmek için admin PIN'ini girin.", "PIN varken normal giriş ekranı");
    });

    public static void ShortPinShowsRuleError() => With(c =>
    {
        c.Login.Pin = c.Login.PinRepeat = "123";
        c.Login.Login();
        Check(c.Login.Error == "PIN 6-12 haneli rakam olmalı." && !c.Shell.IsLoggedIn, $"Kısa PIN: {c.Login.Error}");
    });

    public static void FiveWrongPinsWaitSixtySecondsThenRetry() => With(c =>
    {
        c.SetupPin();
        c.Login.Pin = "000000";
        for (var i = 0; i < 5; i++)
        {
            c.Login.Pin = "000000";
            c.Login.Login();
            Check(c.Login.Error == "PIN hatalı." && c.Login.Pin == "", $"Hatalı deneme {i + 1}");
        }
        Check(c.AuditActions().Contains("admin.login.lockout"), "5. hatada kilitlenme denetime yazılmalı");

        // Doğru PIN bile beklemede reddedilir.
        c.Login.Pin = Pin;
        c.Login.Login();
        Check(c.Login.Error.StartsWith("Çok fazla hatalı deneme.") && c.Login.Error.EndsWith("sn bekleyin.") && !c.Shell.IsLoggedIn, $"Bekleme: {c.Login.Error}");

        c.Clock.Advance(TimeSpan.FromSeconds(30));
        c.Login.Login();
        Check(c.Login.Error == "Çok fazla hatalı deneme. 31 sn bekleyin.", $"Kalan süre: {c.Login.Error}");

        c.Clock.Advance(TimeSpan.FromSeconds(31));
        c.Login.Login();
        Check(c.Shell.IsLoggedIn, $"Süre dolunca giriş: {c.Login.Error}");
    });

    public static void WrongPinCounterResetsAfterSuccess() => With(c =>
    {
        c.SetupPin();
        for (var i = 0; i < 4; i++)
        {
            c.Login.Pin = "000000";
            c.Login.Login();
        }
        c.Login.Pin = Pin;
        c.Login.Login();
        Check(c.Shell.IsLoggedIn, "4 hatadan sonra doğru PIN girer");

        c.Shell.Lock(null);
        c.Login.Pin = "000000";
        c.Login.Login();
        Check(c.Login.Error == "PIN hatalı.", "Sayaç sıfırlanmış olmalı (bekleme yok)");
    });

    // ───────────────────────── Kabuk ─────────────────────────

    public static void IdleLockAfterTenMinutesOfNoInput() => With(c =>
    {
        c.SignIn();

        c.Clock.Advance(TimeSpan.FromMinutes(9));
        c.Shell.CheckIdle();
        Check(c.Shell.IsLoggedIn, "9 dakikada kilitlenmemeli");

        c.Shell.RegisterInput();
        c.Clock.Advance(TimeSpan.FromMinutes(9));
        c.Shell.CheckIdle();
        Check(c.Shell.IsLoggedIn, "Girdi sayacı sıfırlamalı");

        c.Clock.Advance(TimeSpan.FromMinutes(2));
        c.Shell.CheckIdle();
        Check(!c.Shell.IsLoggedIn && c.Login.Error == "10 dakika işlem yapılmadığı için kilitlendi.", $"Boşta kilit: {c.Login.Error}");
        Check(c.Login.Pin == "" && !c.Login.IsFirstRun, "Kilitte PIN kutusu temizlenir");

        // Giriş yapılmamışken CheckIdle bir şey yapmaz.
        c.Login.Error = "";
        c.Clock.Advance(TimeSpan.FromHours(1));
        c.Shell.CheckIdle();
        Check(c.Login.Error == "", "Kilitliyken tekrar kilitleme mesajı yok");
    });

    public static void NavigationPagesLockAndRefresh() => With(c =>
    {
        Seed(c);
        c.SignIn();
        Check(c.Shell.Page == AdminPage.Records && c.Shell.IsRecordsPage && c.Records.Rows.Count == 3, "Girişte kayıtlar açılır ve yenilenir");

        c.Shell.Navigate("types");
        Check(c.Shell.IsTypesPage && !c.Shell.IsRecordsPage, "Tipler sayfası");

        c.Shell.Navigate("texts");
        Check(c.Shell.IsTextsPage && c.Texts.Items.Count == 10, "Açıklamalar sayfası yüklenir");

        c.Shell.Navigate("maint");
        Check(c.Shell.IsMaintenancePage && c.Maint.AuditLines.Count > 0, "Bakım sayfası denetim satırlarını yükler");

        c.Shell.Navigate("settings");
        Check(c.Shell.IsSettingsPage && c.SettingsVm.RootText == c.Root, "Ayarlar sayfası");

        c.Shell.Navigate("lock");
        Check(!c.Shell.IsLoggedIn && c.Login.Error == "Kilitlendi.", "Kilitle düğmesi");
    });

    // ───────────────────────── Kusur tipleri ─────────────────────────

    public static void TypesAddApplyMoveDeleteSave() => With(c =>
    {
        c.SignIn();
        var start = c.Types.Items.Count;
        Check(start == SharedConfig.Defaults.Count && c.Types.SelectedIndex == 0, "Varsayılan tipler yüklenir, ilki seçili");
        Check(c.Types.Name == SharedConfig.Defaults[0].Name, "Seçili tipin adı kutuya gelir");
        Check(c.Types.Items[0].Contains("   ("), "Liste metni ad + alanlar");

        c.Types.Add();
        Check(c.Types.Items.Count == start + 1 && c.Types.Name == "Yeni tip 1" && c.Types.SelectedIndex == start, "Yeni tip 1 eklenir ve seçilir");
        Check(c.Types.FieldDepth && c.Types.FieldWidth && c.Types.FieldLength && !c.Types.FieldRadius && !c.Types.FieldAngle && !c.Types.FieldColor, "Varsayılan ölçüm alanları D/W/L");
        Check(c.Types.IsDirty && c.Types.DirtyText.StartsWith("Kaydedilmemiş değişiklik var."), "Kirli işareti");
        c.Types.Add();
        Check(c.Types.Name == "Yeni tip 2", "İkinci yeni tip numarası artar");

        // Geçersiz ad (boş) -> hata, liste değişmez.
        c.Types.Name = "   ";
        c.Types.Apply();
        Check(c.Notes.Count > 0 && c.Notes[^1].Kind == StatusKind.Error, "Geçersiz ad hata bildirir");
        Check(c.Types.Items.Count == start + 2, "Hatada liste değişmez");

        // Geçerli ad + alanlar.
        c.Types.Name = "Özel tip";
        c.Types.SpecName = " Spec-1 ";
        c.Types.FieldColor = true;
        c.Types.FieldDepth = false;
        c.Types.Apply();
        Check(c.Types.Items[start + 1].StartsWith("Özel tip   (W, L, Renk)"), $"Uygulanan şablon: {c.Types.Items[start + 1]}");

        // Yukarı / aşağı.
        c.Types.MoveUp();
        Check(c.Types.SelectedIndex == start && c.Types.TypeNames[start] == "Özel tip", "Yukarı taşı");
        c.Types.MoveDown();
        Check(c.Types.SelectedIndex == start + 1, "Aşağı taşı");
        c.Types.MoveDown();
        Check(c.Types.SelectedIndex == start + 1, "Sondan aşağı taşınmaz");

        // Sil: önce hayır.
        c.Confirm.Answers.Enqueue(false);
        c.Types.DeleteAsync().GetAwaiter().GetResult();
        Check(c.Types.Items.Count == start + 2 && c.Confirm.Calls.Count == 1 && c.Confirm.Calls[0].ConfirmText == "Sil"
              && c.Confirm.Calls[0].Title == "Kusur tipini sil", "Onay hayır: değişmez");
        c.Confirm.Answers.Enqueue(true);
        c.Types.DeleteAsync().GetAwaiter().GetResult();
        Check(c.Types.Items.Count == start + 1 && !c.Types.TypeNames.Contains("Özel tip"), "Onay evet: silinir");

        // Kaydet -> dosya + denetim + Kayıtlar tip listesi.
        c.Types.Save();
        Check(!c.Types.IsDirty && c.Notes[^1].Kind == StatusKind.Success, "Kaydedince kirli işareti kalkar");
        var saved = SharedConfig.LoadDefectTypes(c.Root);
        Check(saved.Count == start + 1 && saved[^1].Name == "Yeni tip 1", $"Dosyaya yazıldı: {saved.Count}");
        Check(c.AuditActions().Contains("config.defect_types"), "Denetim satırı");
        Check(c.Records.TypeOptions.Count == start + 2 && c.Records.TypeOptions[0] == "Tümü" && c.Records.TypeFilter == "Tümü", "Kayıtlar hata tipi listesi yenilenir");
    });

    public static void TypesResetToDefaultsAsksConfirm() => With(c =>
    {
        c.SignIn();
        c.Types.Add();
        var extra = c.Types.Items.Count;

        c.Confirm.Answers.Enqueue(false);
        c.Types.ResetAsync().GetAwaiter().GetResult();
        Check(c.Types.Items.Count == extra, "Vazgeç: değişmez");

        c.Confirm.Answers.Enqueue(true);
        c.Types.ResetAsync().GetAwaiter().GetResult();
        Check(c.Types.Items.Count == SharedConfig.Defaults.Count && c.Types.SelectedIndex == 0 && c.Types.IsDirty, "Onay: varsayılanlar yüklenir");
        Check(c.Confirm.Calls[^1].Message.Contains($"Listedeki {extra} kusur tipi"), "Onay metni sayıyı içerir");
    });

    // ───────────────────────── Karar açıklamaları ─────────────────────────

    public static void TextsEditSaveAndResetAll() => With(c =>
    {
        c.SignIn();
        c.Shell.Navigate("texts");
        Check(c.Texts.Items.Count == 10 && c.Texts.Items[0].Label == "Kabul (SPA)" && c.Texts.Items[9].Label == "Yeniden aç", "Karar sırası ve adları");
        Check(c.Texts.DirtyText == "" && c.Texts.Items[0].MaxLength == SharedConfig.MaxDecisionTextLength, "Başlangıçta temiz");

        var item = c.Texts.Items.First(x => x.Decision == Decision.Scrap);
        item.Text = "Parça hurdaya ayrılır.";
        Check(c.Texts.DirtyText == "Kaydedilmemiş değişiklik var.", "Düzenleme kirli işaretler");

        c.Texts.Save();
        Check(c.Texts.DirtyText == "" && c.Notes[^1].Kind == StatusKind.Success, "Kaydedince temiz");
        Check(SharedConfig.LoadDecisionTexts(c.Root)[Decision.Scrap] == "Parça hurdaya ayrılır.", "Yeni metin okunur");
        Check(c.AuditActions().Contains("config.decision_texts"), "Denetim satırı");

        // Tek karar varsayılana döner.
        item.ResetOne();
        Check(item.Text == SharedConfig.DefaultDecisionTexts[Decision.Scrap] && c.Texts.DirtyText != "", "Varsayılan (tek karar)");

        // Tümünü sıfırla: önce hayır.
        c.Confirm.Answers.Enqueue(false);
        c.Texts.ResetAllAsync().GetAwaiter().GetResult();
        Check(c.Texts.Items.Contains(item), "Hayır: öğeler yeniden kurulmaz");

        c.Confirm.Answers.Enqueue(true);
        c.Texts.ResetAllAsync().GetAwaiter().GetResult();
        Check(c.Texts.DirtyText == "Varsayılanlar yüklendi; kalıcı olması için 'Ortak alana kaydet'e basın.", "Sıfırlama mesajı");
        Check(c.Texts.Items.All(x => x.Text == SharedConfig.DefaultDecisionTexts[x.Decision]), "Hepsi varsayılan");
        // Henüz kaydedilmedi: dosyada hâlâ özel metin.
        Check(SharedConfig.LoadDecisionTexts(c.Root)[Decision.Scrap] == "Parça hurdaya ayrılır.", "Kaydedilene kadar kalıcı değil");
    });

    // ───────────────────────── Bakım ─────────────────────────

    private static string MakeStaleLock(Ctx c, TimeSpan age)
    {
        var path = Path.Combine(c.Root, "PD170", "OP40", "SN-A1", "x.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    public static void MaintenanceStaleLockRemovalNeedsConfirm() => With(c =>
    {
        c.SignIn();
        var stale = MakeStaleLock(c, TimeSpan.FromHours(1));

        c.Maint.ScanLocks();
        Check(c.Maint.StaleLocks.Count == 1 && c.Maint.StaleLocks[0] == stale, "Eski kilit listelenir");
        Check(c.Notes[^1].Message == "1 eski kilit bulundu." && c.Notes[^1].Kind == StatusKind.Warning, "Tarama bildirimi");

        // Seçim yok.
        c.Maint.RemoveLockAsync().GetAwaiter().GetResult();
        Check(c.Notes[^1] == ("Listeden bir kilit seçin.", StatusKind.Error) && c.Confirm.Calls.Count == 0, "Seçim yokken onay sorulmaz");

        // Hayır.
        c.Maint.SelectedLock = stale;
        c.Confirm.Answers.Enqueue(false);
        c.Maint.RemoveLockAsync().GetAwaiter().GetResult();
        Check(File.Exists(stale) && c.Confirm.Calls[0].ConfirmText == "Kilidi sil" && c.Confirm.Calls[0].Message.StartsWith(stale), "Hayır: silinmez");

        // Evet.
        c.Confirm.Answers.Enqueue(true);
        c.Maint.RemoveLockAsync().GetAwaiter().GetResult();
        Check(!File.Exists(stale) && c.Maint.StaleLocks.Count == 0, "Evet: silinir ve liste yenilenir");
        Check(c.Notes[^1] == ("Kilit silindi.", StatusKind.Success), "Silindi bildirimi");
        Check(c.AuditActions().Contains("admin.lock.remove"), "Denetim satırı");

        c.Maint.ScanLocks();
        Check(c.Notes[^1] == ("Eski kilit yok.", StatusKind.Success), "Eski kilit yok bildirimi");
    });

    public static void MaintenanceRebuildAndVerifyAudit() => With(c =>
    {
        Seed(c);
        c.SignIn();

        c.Maint.RebuildIndex();
        Check(c.Notes[^1] == ("Index yeniden oluşturuldu: 3 kayıt.", StatusKind.Success), $"Index: {c.Notes[^1].Message}");

        c.Maint.VerifyAudit();
        Check(c.Notes[^1] == ("Denetim zinciri sağlam.", StatusKind.Success), "Zincir sağlam");
        Check(c.Maint.AuditLines.Any(l => l.Contains("admin.login")), "Denetim satırı biçimi gösterilir");

        var path = Path.Combine(c.Root, AuditLog.FileName);
        var lines = File.ReadAllLines(path);
        lines[0] = lines[0].Replace("admin.login", "admin.xxx");
        File.WriteAllLines(path, lines);
        c.Maint.VerifyAudit();
        Check(c.Notes[^1].Kind == StatusKind.Error && c.Notes[^1].Message.StartsWith("Denetim zinciri 1. satırda bozuk"), $"Bozuk zincir: {c.Notes[^1].Message}");
    });

    // ───────────────────────── Kayıtlar ─────────────────────────

    public static void RecordsFiltersSummaryAndCards() => With(c =>
    {
        Seed(c);
        c.SignIn();

        Check(c.Records.Rows.Count == 3, $"3 kart: {c.Records.Rows.Count}");
        var cards = c.Records.SummaryCards.ToArray();
        Check(cards.Select(x => x.Label).SequenceEqual(new[] { "Kayıt", "Açık kusurlu", "MRB bekleyen", "Tamamlanan" }), "Dört özet kartı");
        Check(cards.Select(x => x.Value).SequenceEqual(new[] { "3", "2", "1", "1" }), $"Özet değerleri: {string.Join(",", cards.Select(x => x.Value))}");

        c.Records.PartFilter = "pd170";
        Check(c.Records.Rows.Count == 2 && c.Records.SummaryCards.First().Value == "2", "Parça süzgeci");
        c.Records.PartFilter = "";
        c.Records.TypeFilter = "Undercut";
        Check(c.Records.Rows.Single().Row.Serial == "SN-B1", "Tip süzgeci");
        c.Records.TypeFilter = "Tümü";
        c.Records.StateFilter = c.Records.StateOptions.First(s => s.Filter == IndexStateFilter.Completed);
        Check(c.Records.Rows.Single().Row.Serial == "SN-A2", "Durum süzgeci");

        c.Records.SerialFilter = "yok-boyle";
        Check(c.Records.Rows.Count == 0 && c.Records.EmptyText == "Kriterlere uyan kayıt yok.", "Boş sonuç metni");

        c.Records.SerialFilter = "";
        c.Records.StateFilter = c.Records.StateOptions[0];
        var mrb = c.Records.Rows.First(r => r.Row.Serial == "SN-A1");
        Check(mrb.Title == "PD170  ·  OP40  ·  SN-A1" && mrb.HasOpenDefects, "Kart başlığı");
        Check(mrb.Meta.StartsWith("Muayeneci: A. Yılmaz   ·   Açılış: ") && mrb.Meta.EndsWith("   ·   1 kusur, 1 açık   ·   Açık"), $"Meta: {mrb.Meta}");
        Check(mrb.DefectChips.Single().Text == "#1 Dent · MrbPending" && mrb.DefectChips.Single().Background == "#EDE9FE", $"Kusur etiketi: {mrb.DefectChips.Single().Text}");
        var done = c.Records.Rows.First(r => r.Row.Serial == "SN-A2");
        Check(done.Meta.EndsWith("Tamamlandı") && done.DefectChips.Single().Background == "#DCFCE7", "Tamamlandı kartı");
    });

    public static void RecordsExportCsvWritesBomAndFilteredRows() => With(c =>
    {
        Seed(c);
        c.SignIn();

        // Vazgeç: dosya yazılmaz.
        c.Saver.Path = null;
        c.Records.ExportCsvAsync().GetAwaiter().GetResult();
        Check(c.Saver.SuggestedNames.Count == 1 && c.Saver.SuggestedNames[0] == "MuayeneKayit_20260101_0900", $"Önerilen ad: {c.Saver.SuggestedNames[0]}");

        var target = Path.Combine(c.Root, "cikti.csv");
        c.Saver.Path = target;
        c.Records.PartFilter = "PD170";
        c.Records.ExportCsvAsync().GetAwaiter().GetResult();

        var bytes = File.ReadAllBytes(target);
        Check(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "UTF-8 BOM yazılmalı");
        var text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        Check(text == CsvExport.Build(IndexFilter.Apply(IndexBuilder.Rebuild(c.Context.Store),
                new IndexCriteria("PD170", "", "", null, IndexStateFilter.All))), "İçerik süzülmüş satırlar");
        Check(text.Contains("SN-A1") && text.Contains("SN-A2") && !text.Contains("SN-B1"), "Yalnızca süzülen kayıtlar");
        Check(c.Notes[^1] == ($"2 kayıt dışa aktarıldı: {target}", StatusKind.Success), $"Bildirim: {c.Notes[^1].Message}");

        // Yazılamayan yol hata bildirir.
        c.Saver.Path = Path.Combine(c.Root, "yok-klasor", "a.csv");
        c.Records.ExportCsvAsync().GetAwaiter().GetResult();
        Check(c.Notes[^1].Kind == StatusKind.Error, "Yazma hatası bildirilir");
    });

    public static void RecordsOpenFolderAndLatestReport() => With(c =>
    {
        Seed(c);
        c.SignIn();
        var row = c.Records.Rows.First(r => r.Row.Serial == "SN-A1");

        c.Records.OpenFolder(row);
        Check(c.Os.Folders.Single() == Path.GetFullPath(row.Folder), "Klasör kabukta açılır");

        c.Records.OpenLatestReport(row);
        Check(c.Os.Files.Count == 0 && c.Notes[^1] == ("Bu kayıt için rapor yok.", StatusKind.Error), "Rapor yoksa hata");

        var reports = Path.Combine(row.Folder, "reports");
        Directory.CreateDirectory(reports);
        File.WriteAllText(Path.Combine(reports, "Rapor_v1.html"), "<html/>");
        File.WriteAllText(Path.Combine(reports, "Rapor_v2.html"), "<html/>");
        c.Records.OpenLatestReport(row);
        Check(c.Os.Files.Single() == Path.Combine(reports, "Rapor_v2.html"), "PDF yoksa HTML açılır");

        File.WriteAllBytes(Path.Combine(reports, "Rapor_v2.pdf"), new byte[] { 0x25 });
        c.Records.OpenLatestReport(row);
        Check(c.Os.Files[^1] == Path.Combine(reports, "Rapor_v2.pdf"), "PDF varsa PDF açılır");

        // Kök dışı klasör açılmaz.
        var outside = new AdminRecordRowViewModel(row.Row with { Part = ".." }, c.Root);
        c.Records.OpenFolder(outside);
        Check(c.Os.Folders.Count == 1 && c.Notes[^1] == ("Klasör bulunamadı.", StatusKind.Error), "Kök dışı ya da olmayan klasör reddedilir");
    });

    // ───────────────────────── Ayarlar ─────────────────────────

    public static void SettingsRootMustExistThenLocksSession() => With(c =>
    {
        c.SignIn();
        var newRoot = Path.Combine(Path.GetTempPath(), "MuayeneKayit-admin-yeni-" + Guid.NewGuid().ToString("N"));

        c.SettingsVm.RootText = newRoot;
        c.SettingsVm.SaveRoot();
        Check(c.Notes[^1] == ("Klasöre erişilemiyor. Yolu ve ağ bağlantısını kontrol edin.", StatusKind.Error)
              && c.Shell.IsLoggedIn && c.Settings.Saved.Count == 0, "Olmayan klasör reddedilir");

        Directory.CreateDirectory(newRoot);
        try
        {
            c.SettingsVm.RootText = newRoot + " ";
            c.SettingsVm.SaveRoot();
            Check(c.Settings.Saved.Single() == newRoot, "Ayar kırpılmış yol ile kaydedilir");
            Check(!c.Shell.IsLoggedIn && c.Login.Error == "Ortak alan değişti; yeni alandaki PIN ile tekrar giriş yapın.", "Oturum kilitlenir");
            Check(c.Shell.Root == newRoot && c.Login.RootText == $"Ortak alan: {newRoot}", "Giriş ekranı yeni kökü gösterir");
            Check(c.Login.IsFirstRun, "Yeni alanda PIN yok: ilk kurulum");

            // Yeni köke yazıldığını doğrula: ilk kurulum yeni alanda PIN yazar.
            c.Login.Pin = c.Login.PinRepeat = Pin;
            c.Login.Login();
            Check(c.Shell.IsLoggedIn && SharedConfig.HasPin(newRoot), "PIN yeni alanda belirlenir");
        }
        finally
        {
            try { Directory.Delete(newRoot, recursive: true); } catch (IOException) { }
        }
    });

    public static void SettingsChangePinValidatesOldAndRepeat() => With(c =>
    {
        c.SignIn();

        c.SettingsVm.OldPin = "000000";
        c.SettingsVm.NewPin = c.SettingsVm.NewPinRepeat = "222222";
        c.SettingsVm.ChangePin();
        Check(c.Notes[^1] == ("Mevcut PIN hatalı.", StatusKind.Error) && SharedConfig.VerifyPin(c.Root, Pin), "Eski PIN yanlış");

        c.SettingsVm.OldPin = Pin;
        c.SettingsVm.NewPinRepeat = "333333";
        c.SettingsVm.ChangePin();
        Check(c.Notes[^1] == ("Yeni PIN'ler aynı değil.", StatusKind.Error) && SharedConfig.VerifyPin(c.Root, Pin), "Yeni PIN eşleşmiyor");

        c.SettingsVm.NewPin = c.SettingsVm.NewPinRepeat = "12";
        c.SettingsVm.ChangePin();
        Check(c.Notes[^1].Kind == StatusKind.Error && SharedConfig.VerifyPin(c.Root, Pin), "Kısa PIN reddedilir");

        c.SettingsVm.NewPin = c.SettingsVm.NewPinRepeat = "222222";
        c.SettingsVm.ChangePin();
        Check(c.Notes[^1] == ("PIN değiştirildi.", StatusKind.Success), "PIN değişti");
        Check(SharedConfig.VerifyPin(c.Root, "222222") && !SharedConfig.VerifyPin(c.Root, Pin), "Yeni PIN geçerli, eskisi değil");
        Check(c.SettingsVm.OldPin == "" && c.SettingsVm.NewPin == "" && c.SettingsVm.NewPinRepeat == "", "Alanlar temizlenir");
        Check(c.AuditActions().Contains("admin.pin.change"), "Denetim satırı");
    });
}
