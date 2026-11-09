using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

/// <summary>
/// Savunma amaçlı güvenlik testleri (docs/test/GUVENLIK-TEST-PLANI.md). Yalnızca kendi geçici kökünde çalışır,
/// gerçek veri köküne / ağ paylaşımına dokunmaz. UNC ve mutlak yollar yalnızca METİN olarak kullanılır.
/// Bugün başarısız olması beklenen (bulgu belgeleyen) testler Program.cs'te openFindings listesindedir.
/// </summary>
public static class SecurityTests
{
    /// <summary>Test atlandı (ortam / yetki nedeniyle); Program.cs "ATLANDI" yazar, çıkış kodunu etkilemez.</summary>
    public sealed class SkipException(string reason) : Exception(reason);

    private const string TestPin = "48291657";
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 };

    // ───────────────────────── Yardımcılar ─────────────────────────

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    /// <summary>Rapora doğrudan girecek biçim: beklenen güvenli davranış + gözlenen + dosya:satır.</summary>
    private static void Fail(string expected, string observed, string where) =>
        throw new Exception($"Beklenen: {expected}. Gözlenen: {observed}. {where}");

    private static string Obs(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

    private static void NoThrow(string expected, string where, Action action)
    {
        try { action(); }
        catch (Exception ex) { Fail(expected, "istisna sızdı " + Obs(ex), where); }
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    /// <summary>Beklenen türde istisnayı döndürür; istisna yoksa ya da başka türdeyse testi düşürür.</summary>
    private static T Catch<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T ex) { return ex; }
        catch (Exception ex) { throw new Exception($"{typeof(T).Name} beklenirdi, {ex.GetType().Name} geldi: {ex.Message}"); }
        throw new Exception($"{typeof(T).Name} beklenirdi, istisna oluşmadı");
    }

    /// <summary>Süre sınırı: iş parçacığı arka planda kalır (IsBackground), sonuç beklenmez.</summary>
    private static bool CompletesWithin(Action action, TimeSpan limit)
    {
        var thread = new Thread(() => { try { action(); } catch { } }) { IsBackground = true };
        thread.Start();
        return thread.Join(limit);
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WithRoot(Action<string> body)
    {
        var root = TempRoot();
        try { body(root); }
        finally { Cleanup(root); }
    }

    /// <summary>Junction içeren kökte önce bağlantılar (özyinelemesiz) kaldırılır, sonra kök silinir.</summary>
    private static void Cleanup(string root)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    Directory.Delete(dir, recursive: false);
            }
            Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class ListAudit : IAuditTrail
    {
        public List<string> Entries { get; } = new();
        public void Append(string actor, string action, string details) => Entries.Add($"{actor}|{action}|{details}");
    }

    private sealed class Env
    {
        public required string Root { get; init; }
        public required SessionStore Store { get; init; }
        public required InspectionViewModel Vm { get; init; }
        public required FakeDialogService Dialogs { get; init; }
        public required FakeFilePicker Picker { get; init; }
        public required FakeWindowService Windows { get; init; }
        public required FakePdfConverter Pdf { get; init; }
        public List<(string Message, StatusKind Kind)> Notes { get; } = new();
        public string Folder => Vm.Folder!;

        public void Open(string serial = "SN001")
        {
            Vm.PartNumber = "PD170";
            Vm.OperationNumber = "OP40";
            Vm.SerialNumber = serial;
            Vm.InspectorName = "A. Yılmaz";
            Vm.OpenSession();
        }
    }

    private static void WithVm(Action<Env> body, bool realAudit = false)
    {
        WithRoot(root =>
        {
            var store = new SessionStore(root);
            IAuditTrail audit = realAudit ? new FileAuditTrail(store) : new ListAudit();
            var dialogs = new FakeDialogService();
            var picker = new FakeFilePicker();
            var windows = new FakeWindowService();
            var pdf = new FakePdfConverter();
            var env = new Env
            {
                Root = root,
                Store = store,
                Dialogs = dialogs,
                Picker = picker,
                Windows = windows,
                Pdf = pdf,
                Vm = new InspectionViewModel(store, audit, SharedConfig.Defaults, dialogs, picker, windows, pdf,
                    new FakeUserSettings(), new FakeWindowsUser(), null),
            };
            env.Vm.Notify += (m, k) => env.Notes.Add((m, k));
            body(env);
        });
    }

    private sealed class AdminEnv
    {
        public required string Root { get; init; }
        public required AdminContext Context { get; init; }
        public required FakeClock Clock { get; init; }
        public required FakeShell Os { get; init; }
        public required FakeFileSaver Saver { get; init; }
        public List<(string Message, StatusKind Kind)> Notes { get; } = new();
    }

    private static void WithAdmin(Action<AdminEnv> body) => WithRoot(root =>
        body(new AdminEnv
        {
            Root = root,
            Context = new AdminContext(root),
            Clock = new FakeClock(),
            Os = new FakeShell(),
            Saver = new FakeFileSaver(),
        }));

    private static string Seed(SessionStore store, string part = "PD170", string op = "OP40", string serial = "SN001")
    {
        var (folder, _) = store.OpenOrCreate(new Session
        {
            PartNumber = part, OperationNumber = op, SerialNumber = serial, Inspector = "A. Yılmaz", Project = part,
        });
        return folder;
    }

    private static string DefectFile(string folder, int id) => Path.Combine(folder, "defects", $"{id}.json");

    private static void EditJson(string path, Action<JsonObject> edit)
    {
        var obj = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;
        edit(obj);
        File.WriteAllText(path, obj.ToJsonString());
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string TempJpeg(string dir, string name = "kaynak.jpg")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, JpegBytes);
        return path;
    }

    private static Session SampleSession(string serial = "SN001") => new()
    {
        PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = serial, Inspector = "A. Yılmaz",
        CreatedAt = new DateTime(2026, 1, 1, 9, 0, 0),
    };

    private const string Html = "<b>x</b>";

    // ═════════════════════════ S-PATH ═════════════════════════

    public static void SegmentsRejectAbsoluteDriveUncAds()
    {
        foreach (var bad in new[] { "C:x", "\\\\testhost\\s", "a:b", "x::$DATA" })
            Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment(bad, "Parça no")), $"S-PATH-002: '{bad}' reddedilmeli");
    }

    public static void SegmentsRejectReservedDeviceNames()
    {
        var accepted = new List<string>();
        foreach (var name in new[] { "CON", "nul", "AUX", "PRN", "COM1", "LPT1", "con.txt", "NUL.x" })
        {
            try { SafePath.ValidateSegment(name, "Parça no"); accepted.Add(name); }
            catch (ArgumentException) { }
        }
        if (accepted.Count > 0)
            Fail("ayrılmış aygıt adları ArgumentException ile reddedilir", "kabul edildi: " + string.Join(", ", accepted),
                "SafePath.cs:12-14,22 (desen yalnız \\p{L}\\p{N}_.-, aygıt adı denetimi yok)");
    }

    public static void SegmentsRejectTrailingDotAndInvisibleChars()
    {
        Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment("PD170.", "Parça no")), "Sonda nokta reddedilmeli");
        Expect(SafePath.ValidateSegment("PD170 ", "Parça no") == "PD170", "Sonda boşluk kırpılmalı");
        Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment("PD\u200B170", "Parça no")), "Sıfır genişlikli karakter reddedilmeli");
        Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment("PD\u202E170", "Parça no")), "Yön değiştirme karakteri reddedilmeli");
    }

    public static void SegmentLengthLimit()
    {
        Expect(SafePath.ValidateSegment(new string('A', 64), "Parça no").Length == 64, "64 karakter kabul edilmeli");
        Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment(new string('A', 65), "Parça no")), "65 karakter reddedilmeli");

        // Üç segment 64 karakter: tam yol 260'ı aşabilir; yalnızca beklenen hata türleri kabul edilir.
        WithRoot(root =>
        {
            var s = new string('B', 64);
            try
            {
                var folder = SafePath.SessionFolder(root, s, s, s);
                Console.WriteLine($"        · 3x64 karakterlik yol uzunluğu: {folder.Length}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or PathTooLongException) { }
        });
    }

    /// <summary>mklink /J ile junction kurar; başarısızsa (yetki / ortam) testi atlar.</summary>
    private static void MakeJunction(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        // ArgumentList tırnakları kaçırdığı için cmd ayrıştıramaz; ham Arguments kullanılır.
        psi.Arguments = $"/c mklink /J \"{link}\" \"{target}\"";
        using var p = Process.Start(psi) ?? throw new SkipException("cmd başlatılamadı");
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        if (p.ExitCode != 0 || !Directory.Exists(link))
            throw new SkipException("mklink /J başarısız (yetki yok / ortam)");
    }

    public static void JunctionInsideRootIsNotFollowed() => WithRoot(root =>
    {
        var outside = Path.Combine(root + "-dis");
        Directory.CreateDirectory(outside);
        try
        {
            // Kök: <root>\kok ; kök dışı hedef: <root>-dis\hedef\OP40\SN9\session.json
            var kok = Path.Combine(root, "kok");
            Directory.CreateDirectory(kok);
            var store = new SessionStore(kok);
            var targetStore = new SessionStore(Path.Combine(outside, "hedef"));
            Directory.CreateDirectory(Path.Combine(outside, "hedef"));
            Seed(targetStore, "DISPART", "OP40", "SN9");
            // hedef\DISPART\OP40\SN9 -> junction, kök altında PD170 olarak görünür.
            var link = Path.Combine(kok, "PD170");
            MakeJunction(link, Path.Combine(outside, "hedef", "DISPART"));

            var rows = IndexBuilder.Rebuild(store);
            Directory.Delete(link, recursive: false);
            if (rows.Count > 0)
                Fail("kök içindeki reparse point izlenmez, kök dışı oturum indekse girmez",
                    $"{rows.Count} satır okundu (Part={rows[0].Part}, kök dışından)", "IndexBuilder.cs:35-47 (reparse-point denetimi yok)");
        }
        finally { Cleanup(outside); }
    });

    public static void AdminOpenFolderRejectsSiblingPrefix() => WithAdmin(a =>
    {
        var veri = Path.Combine(a.Root, "Veri");
        var veri2 = Path.Combine(a.Root, "Veri2");
        Directory.CreateDirectory(veri);
        Directory.CreateDirectory(Path.Combine(veri2, "P", "O", "S"));
        var ctx = new AdminContext(veri);
        var records = new RecordsViewModel(ctx, a.Saver, a.Os, a.Clock);
        records.Notify += (m, k) => a.Notes.Add((m, k));

        var row = new IndexRow("P", "O", "S", "Open", 0, 0, "", DateTime.Now, new List<IndexDefect>());
        records.OpenFolder(new AdminRecordRowViewModel(row, veri2));
        if (a.Os.Folders.Count > 0)
            Fail("kardeş klasör (Veri2) kök sayılmaz, OpenFolder çağrılmaz", $"kabuğa gitti: {a.Os.Folders[0]}",
                "RecordsViewModel.cs:144 (ayraçsız StartsWith)");
    });

    public static void AdminOpenLatestReportStaysUnderRoot() => WithAdmin(a =>
    {
        var veri = Path.Combine(a.Root, "Veri");
        var veri2 = Path.Combine(a.Root, "Veri2");
        Directory.CreateDirectory(veri);
        var reports = Path.Combine(veri2, "P", "O", "S", "reports");
        Directory.CreateDirectory(reports);
        File.WriteAllBytes(Path.Combine(reports, "Rapor_v1.pdf"), new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var ctx = new AdminContext(veri);
        var records = new RecordsViewModel(ctx, a.Saver, a.Os, a.Clock);

        var row = new IndexRow("P", "O", "S", "Open", 0, 0, "", DateTime.Now, new List<IndexDefect>());
        records.OpenLatestReport(new AdminRecordRowViewModel(row, veri2));
        if (a.Os.Files.Count > 0)
            Fail("kök dışı klasördeki rapor kabuğa verilmez", $"kabuğa gitti: {a.Os.Files[0]}",
                "RecordsViewModel.cs:149-157 (OpenLatestReport kök denetimi yapmıyor)");
    });

    /// <summary>Kapalı kusurda CleanedFile ayarlanıp OpenEditor çağrılır; kabuğa giden yollar döner.</summary>
    private static List<string> OpenClosedWithCleanedFile(Env e, string cleanedFile)
    {
        e.Open();
        e.Vm.AddDefect("Dent");
        e.Vm.DepthText = "0.5";
        e.Vm.SaveMeasurements();
        e.Store.UpdateDefect(e.Folder, 1, d =>
        {
            d.Photos.Add("1_photo_01.jpg");
            DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya", reference: "S-1");
        });
        e.Vm.Refresh(1);
        // CleanedFile, VM yenilenmeden sonra yazılır: UNC yolunda File.Exists (ağ erişimi) tetiklenmez.
        e.Store.UpdateDefect(e.Folder, 1, d => d.Markups["1_photo_01.jpg"] = new PhotoMarkup { CleanedFile = cleanedFile });
        e.Vm.OpenEditor("1_photo_01.jpg");
        return e.Windows.OpenedFiles.ToList();
    }

    private static bool IsSafeOpenedPhoto(string folder, string path)
    {
        var defects = Path.GetFullPath(Path.Combine(folder, "defects")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        return full.StartsWith(defects, StringComparison.OrdinalIgnoreCase)
               && PhotoMarkupValidator.IsPhotoFileName(Path.GetFileName(full));
    }

    public static void ClosedDefectCleanedFileStaysInDefects() => WithVm(e =>
    {
        var opened = OpenClosedWithCleanedFile(e, "..\\x.txt");
        var bad = opened.Where(p => !IsSafeOpenedPhoto(e.Folder, p)).ToList();
        if (bad.Count > 0)
            Fail("OpenFile yalnız defects\\ altındaki IsPhotoFileName eşleşen dosyayı alır; geçersiz ad hata mesajı verir",
                $"OpenFile'a gitti: {bad[0]}", "InspectionViewModel.Photos.cs:147, EdgeBrowser.cs:30 (UseShellExecute=true)");
    });

    public static void ClosedDefectAbsoluteAndUncCleanedFileRejected() => WithVm(e =>
    {
        // UNC ve mutlak yol yalnızca metin; FakeWindowService yakalar, hiçbir yere erişilmez.
        var opened = OpenClosedWithCleanedFile(e, "\\\\testhost\\paylasim\\x.jpg");
        e.Vm.OpenEditor("1_photo_01.jpg");
        e.Store.UpdateDefect(e.Folder, 1, d => d.Markups["1_photo_01.jpg"].CleanedFile = "C:\\Windows\\win.ini");
        e.Windows.OpenedFiles.Clear();
        e.Vm.OpenEditor("1_photo_01.jpg");
        var all = opened.Concat(e.Windows.OpenedFiles).ToList();
        var bad = all.Where(p => !IsSafeOpenedPhoto(e.Folder, p)).ToList();
        if (bad.Count > 0)
            Fail("mutlak / UNC CleanedFile reddedilir, açma isteği gitmez",
                $"OpenFile'a gitti: {string.Join(" | ", bad)}", "InspectionViewModel.Photos.cs:147 (Path.Combine mutlak yolu olduğu gibi döndürür)");
    });

    /// <summary>defects\1.json içindeki photos listesine verilen adı yazar, oturumu açıp kusuru seçer; küçük resim yolları döner.</summary>
    private static List<string> ThumbnailPathsFor(Env e, string photoName)
    {
        var folder = Seed(e.Store);
        e.Store.AddDefect(folder, "Dent");
        EditJson(DefectFile(folder, 1), o => o["photos"] = new JsonArray(photoName));
        e.Open();
        e.Vm.Select(1);
        return e.Vm.Photos.Select(p => p.ThumbnailPath).ToList();
    }

    public static void PhotoNamesTraversalDoesNotEscapeDefectsFolder() => WithVm(e =>
    {
        var paths = ThumbnailPathsFor(e, "..\\x.jpg");
        var bad = paths.Where(p => !IsSafeOpenedPhoto(e.Folder, p)).ToList();
        if (bad.Count > 0)
            Fail("geçersiz fotoğraf adı atlanır / uyarılır; ThumbnailPath defects\\ dışına çıkmaz",
                $"ThumbnailPath={bad[0]}", "InspectionViewModel.Photos.cs:21-36 (Photos adı doğrulanmıyor)");
    });

    public static void PhotoNameUncDoesNotProducePath() => WithVm(e =>
    {
        var paths = ThumbnailPathsFor(e, "\\\\testhost\\s\\x.jpg");
        var bad = paths.Where(p => p.StartsWith("\\\\", StringComparison.Ordinal)).ToList();
        if (bad.Count > 0)
            Fail("UNC fotoğraf adı reddedilir, ThumbnailPath UNC olmaz",
                $"ThumbnailPath={bad[0]} (ThumbnailView File.OpenRead ile açar)", "InspectionViewModel.Photos.cs:36, ThumbnailView.cs:38");
    });

    public static void ReportPhotoSourceStaysInDefectsFolder()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Photos = { "../x.jpg" } };
        var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
        if (html.Contains("defects/../", StringComparison.Ordinal))
            Fail("src içinde dolaşım yok, geçersiz ad 'fotoğraf yok' sayılır",
                "HTML'de src=\"../defects/../x.jpg\" üretildi", "ReportBuilder.cs:248-252 (Photos adı IsPhotoFileName denetimi görmüyor)");
    }

    public static void ImportedFileNameIsIndependentOfSource() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        var src = TempJpeg(root, "..x.jpg");
        var name = PhotoImporter.Import(folder, 1, src);
        Expect(PhotoMarkupValidator.IsPhotoFileName(name), $"Ad kalıba uymalı: {name}");
        Expect(File.Exists(Path.Combine(folder, "defects", name)), "Dosya defects altında olmalı");
    });

    // ═════════════════════════ S-HTML ═════════════════════════

    private static (Session Session, Defect Defect) HostileData()
    {
        var s = new Session
        {
            PartNumber = "PD" + Html, OperationNumber = "OP" + Html, SerialNumber = "SN" + Html,
            Inspector = Html, CreatedAt = new DateTime(2026, 1, 1),
        };
        var d = new Defect
        {
            Id = 1, DefectType = "Dent" + Html, Notes = Html, AffectedParts = Html, Color = Html,
            Custom = new List<CustomMeasure> { new("h", Html, "H", CustomFieldKind.Text, null, Html) },
        };
        DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, Html, note: Html, reference: Html);
        d.MeasurementEdits.Add(new MeasurementEdit(DateTime.Now, Html, new MeasurementSnapshot(0.1, null, null, null, null, Html), new MeasurementSnapshot(0.2, null, null, null, null, Html)));
        return (s, d);
    }

    public static void ReportEscapesAllUserFields()
    {
        var (s, d) = HostileData();
        var html = ReportBuilder.Build(s, new[] { d }, 1);
        Expect(!html.Contains("<b>"), "Ham <b> etiketi çıktıya sızdı");
        Expect(!html.Contains("</b>"), "Ham </b> etiketi çıktıya sızdı");
        Expect(html.Contains("&lt;b&gt;x&lt;/b&gt;"), "Kaçılmış metin beklenir");
    }

    public static void ReportAttributeContextIsEscaped()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Photos = { "a\"b.jpg" } };
        var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
        // Geçersiz ad rapora hiç girmez (ÖB-02 düzeltmesi): ne ham ne kaçılmış hali öznitelikte bulunur.
        Expect(!html.Contains("a\"b.jpg"), "Öznitelik dışına taşma var");
        Expect(!html.Contains("src=\"../defects/a"), "Geçersiz fotoğraf adı src'ye girmemeli");
        Expect(html.Contains("no-photo"), "Geçersiz ad 'fotoğraf yok' sayılmalı");
    }

    public static void ReportTitleIsEscaped()
    {
        var s = SampleSession();
        s.SerialNumber = "</title><b>x";
        var html = ReportBuilder.Build(s, Array.Empty<Defect>(), 1);
        Expect(!html.Contains("</title><b>"), "Başlık kapatılabildi");
        Expect(html.Contains("&lt;/title&gt;&lt;b&gt;x"), "Kaçılmış başlık beklenir");
    }

    public static void ReportCustomMeasureNamesEscaped()
    {
        var d = new Defect
        {
            Id = 1, DefectType = "Dent",
            Custom = new List<CustomMeasure> { new("h", Html, "H", CustomFieldKind.Text, null, Html) },
        };
        var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
        Expect(!html.Contains("<b>"), "Ek ölçüm adı/değeri kaçılmalı");
    }

    public static void ReportRegionLabelsEscaped()
    {
        var parent = new Defect { Id = 1, DefectType = "Dent", Photos = { Html + ".jpg" } };
        var child = new Defect { Id = 2, DefectType = "Dent", SupersedesId = 1, FoundAtPhoto = Html + ".jpg", Photos = { Html + "2.jpg" } };
        var html = ReportBuilder.Build(SampleSession(), new[] { parent, child }, 1);
        Expect(!html.Contains("<b>"), "Bölge etiketi / bağlantı cümlesi kaçılmalı");
    }

    public static void ReportHasNoActiveContent()
    {
        var (s, d) = HostileData();
        d.Notes = "<script>alert(1)</script><img src=x onerror=alert(1)><iframe src=x></iframe><object data=x></object>";
        d.Photos.Add("1_photo_01.jpg");
        d.Markups["1_photo_01.jpg"] = new PhotoMarkup { ImageWidth = 100, ImageHeight = 100, Shapes = { Annotation.CenterSquare(100, 100, 1) } };
        var html = ReportBuilder.Build(s, new[] { d }, 1);

        Expect(!Regex.IsMatch(html, "<script|<iframe|<object", RegexOptions.IgnoreCase), "Etkin etiket üretildi");
        Expect(!Regex.IsMatch(html, @"<[^>]*\son[a-z]+\s*=", RegexOptions.IgnoreCase), "Olay öznitelikli etiket üretildi");
        Expect(!html.Contains("javascript:", StringComparison.OrdinalIgnoreCase), "javascript: üretildi");
        var withoutNs = html.Replace("xmlns=\"http://www.w3.org/2000/svg\"", "");
        Expect(!Regex.IsMatch(withoutNs, @"(src|href)=""https?://", RegexOptions.IgnoreCase), "Dış kaynak öznitelikleri üretildi");
        Expect(!Regex.IsMatch(html, @"(src|href)=""(?!\.\./defects/)"), "Beklenmeyen src/href hedefi");
    }

    public static void ReportHasContentSecurityPolicy()
    {
        var html = ReportBuilder.Build(SampleSession(), new[] { new Defect { Id = 1, DefectType = "Dent" } }, 1);
        var head = html[..html.IndexOf("</head>", StringComparison.Ordinal)];
        if (!Regex.IsMatch(head, "http-equiv=\"Content-Security-Policy\"", RegexOptions.IgnoreCase))
            Fail("<head> içinde CSP meta (default-src 'none'; img-src file: data:; style-src 'unsafe-inline')",
                "CSP yok", "ReportBuilder.cs:27-30");
    }

    public static void ReportInvalidNumbersDoNotReachSvg()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Photos = { "1_photo_01.jpg" } };
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, 1e308 })
        {
            d.Markups["1_photo_01.jpg"] = new PhotoMarkup
            {
                ImageWidth = 100, ImageHeight = 100,
                Shapes = { new Annotation { Kind = AnnotationKind.Box, X1 = bad, Y1 = 0.1, X2 = 0.5, Y2 = 0.5, DefectId = 1 } },
            };
            var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
            Expect(!html.Contains("NaN") && !html.Contains("Infinity") && !html.Contains("E+"), $"Geçersiz sayı SVG'ye yazıldı ({bad})");
        }
        d.Markups["1_photo_01.jpg"] = new PhotoMarkup { ImageWidth = 0, ImageHeight = 0, Shapes = { Annotation.CenterSquare(100, 100, 1) } };
        Expect(ReportBuilder.Build(SampleSession(), new[] { d }, 1).Contains("<img"), "ImageWidth=0 ile rapor üretilmeli");
    }

    public static void ReportShapeCountIsBounded()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Photos = { "1_photo_01.jpg" } };
        var markup = new PhotoMarkup { ImageWidth = 100, ImageHeight = 100 };
        for (var i = 0; i < 10_000; i++)
            markup.Shapes.Add(Annotation.CenterSquare(100, 100, 1));
        d.Markups["1_photo_01.jpg"] = markup;
        var sw = Stopwatch.StartNew();
        var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
        sw.Stop();
        var drawn = Regex.Matches(html, "<rect ").Count;
        Expect(drawn <= PhotoMarkupValidator.MaxShapes * 2, $"Çizilen şekil sayısı sınırsız: {drawn}");
        Expect(sw.Elapsed < TimeSpan.FromSeconds(2), $"Rapor süresi {sw.ElapsedMilliseconds} ms");
    }

    public static void ReportIgnoresInvalidCleanedFile()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Photos = { "1_photo_01.jpg" } };
        d.Markups["1_photo_01.jpg"] = new PhotoMarkup { CleanedFile = "..\\x.jpg" };
        var html = ReportBuilder.Build(SampleSession(), new[] { d }, 1);
        Expect(html.Contains("src=\"../defects/1_photo_01.jpg\""), "Orijinal fotoğraf kullanılmalı");
        Expect(!html.Contains("x.jpg"), "Geçersiz temiz kopya adı rapora girdi");
    }

    public static void AdminLatestReportPrefersPdfAndHtmlOnlyInsideRoot() => WithAdmin(a =>
    {
        var folder = Path.Combine(a.Root, "P", "O", "S");
        var reports = Path.Combine(folder, "reports");
        Directory.CreateDirectory(reports);
        File.WriteAllText(Path.Combine(reports, "Rapor_v1.html"), "<html></html>");
        var records = new RecordsViewModel(a.Context, a.Saver, a.Os, a.Clock);
        records.Notify += (m, k) => a.Notes.Add((m, k));
        var row = new IndexRow("P", "O", "S", "Open", 0, 0, "", DateTime.Now, new List<IndexDefect>());

        // Yalnızca yedek HTML var ve kök içinde: açılır (PDF üretilemeyen raporun tek kopyası).
        records.OpenLatestReport(new AdminRecordRowViewModel(row, a.Root));
        Expect(a.Os.Files.Count == 1 && a.Os.Files[0].EndsWith("Rapor_v1.html", StringComparison.OrdinalIgnoreCase), "Kök içindeki yedek HTML açılmalı");

        // PDF de varsa PDF tercih edilir, HTML açılmaz.
        File.WriteAllBytes(Path.Combine(reports, "Rapor_v1.pdf"), new byte[] { 0x25, 0x50, 0x44, 0x46 });
        a.Os.Files.Clear();
        records.OpenLatestReport(new AdminRecordRowViewModel(row, a.Root));
        Expect(a.Os.Files.Count == 1 && a.Os.Files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase), "PDF tercih edilmeli");

        // Kök dışındaki klasördeki yalnızca-HTML rapor hiçbir zaman açılmaz.
        var other = a.Root + "-dis";
        try
        {
            Directory.CreateDirectory(Path.Combine(other, "P", "O", "S", "reports"));
            File.WriteAllText(Path.Combine(other, "P", "O", "S", "reports", "Rapor_v1.html"), "<html></html>");
            a.Os.Files.Clear();
            records.OpenLatestReport(new AdminRecordRowViewModel(row, other));
            Expect(a.Os.Files.Count == 0, "Kök dışındaki HTML kabuğa verilmemeli");
        }
        finally { Cleanup(other); }
    });

    // ═════════════════════════ S-CSV ═════════════════════════

    public static void CsvFormulaPrefixesNeutralised()
    {
        foreach (var v in new[] { "+1", "-1", "@x", "\t=1", "\r=1", "=1+1" })
        {
            var cell = CsvExport.Cell(v).TrimStart('"');
            Expect(cell.StartsWith("'"), $"'{v.Replace("\t", "\\t").Replace("\r", "\\r")}' önek almadı: {cell}");
        }
    }

    /// <summary>Basit CSV ayrıştırıcı (; ayraç, "" kaçışı, tırnak içinde satır sonu).</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ';') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>(); }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    public static void CsvSeparatorQuoteNewlineStayConsistent()
    {
        var row = new IndexRow("a;b", "a\"b", "a\nb", "Open", 1, 1, "", DateTime.Now,
            new List<IndexDefect> { new(1, "x;y", "Open", 0, "a\r\nb", "1-2") }, "i;\"n\nm");
        var rows = ParseCsv(CsvExport.Build(new[] { row }));
        Expect(rows.Count == 2, $"2 kayıt beklenir (başlık + 1): {rows.Count}");
        Expect(rows.All(r => r.Count == 13), "Her kayıt 13 sütun olmalı: " + string.Join(",", rows.Select(r => r.Count)));
        Expect(rows[1][0] == "a;b" && rows[1][1] == "a\"b" && rows[1][2] == "a\nb", "Hücre değerleri kayıpsız dönmeli");
    }

    public static void CsvAllColumnsGoThroughCell()
    {
        var row = new IndexRow("=P", "=O", "=S", "=St", 1, 1, "", DateTime.Now,
            new List<IndexDefect> { new(1, "=T", "=Dur", 0, "=Karar", "=1-5") }, "=I");
        var fields = ParseCsv(CsvExport.Build(new[] { row }))[1];
        var bad = fields.Where(f => f.StartsWith("=")).ToList();
        Expect(bad.Count == 0, $"Formül öneki almayan sütun: {string.Join(",", bad)}");
    }

    public static void CsvCommaDelimitedLocaleDoesNotCreateFormulaCell()
    {
        // Virgül ayraçlı yerel ayarda (en-US) Excel "a,=1+1" metnini iki hücreye böler; ikincisi '=' ile başlar.
        var cell = CsvExport.Cell("a,=1+1");
        if (!cell.StartsWith("\"") && !cell.Contains("'="))
            Fail("virgül içeren hücre tırnaklanır (ya da sep=; başlığı yazılır): bölünürse '=' ile başlayan hücre oluşmaz",
                $"Cell(\"a,=1+1\") = {cell} (tırnaksız; yalnız ; \" \\n \\r kontrol ediliyor)", "CsvExport.cs:35-41, :11-14");
    }

    public static void CsvLeadingSpaceAndFullWidthObservation()
    {
        // Gözlem (Excel doğrulaması yapılamadı): bu iki değer bugün önek almıyor.
        Expect(CsvExport.Cell(" =1+1") == " =1+1" && CsvExport.Cell("＝1+1") == "＝1+1", "Gözlem değişti: bu girdiler artık işleniyor, raporu güncelleyin");
    }

    // ═════════════════════════ S-JSON ═════════════════════════

    public static void DashboardSurvivesTruncatedSession() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        Seed(store, "PD170", "OP40", "SN-OK");
        var bad = Seed(store, "PD170", "OP40", "SN-BAD");
        File.WriteAllText(Path.Combine(bad, "session.json"), "{\"partNumber\":");
        var dash = new DashboardViewModel(store, SharedConfig.Defaults);
        NoThrow("Refresh istisna sızdırmaz, hata mesajı verir, sağlam oturum listelenir",
            "DashboardViewModel.cs:136,205-217 (Run JsonException yakalamıyor)", () => dash.Refresh());
        Expect(dash.Rows.Count == 1, $"Sağlam oturum listelenmeli: {dash.Rows.Count}");
    });

    public static void CorruptDefectDoesNotCrashInspection() => WithVm(e =>
    {
        var f = Seed(e.Store);
        Directory.CreateDirectory(Path.Combine(f, "defects"));
        File.WriteAllText(DefectFile(f, 2), "{");
        NoThrow("VM istisna sızdırmaz, uyarı / hata mesajı verir", "InspectionViewModel.cs:797 (Run JsonException yakalamıyor), SessionStore.cs:66-67", () => e.Open());
        // Bozuk dosya atlanır ve uyarı olarak bildirilir; işlemin son mesajı uyarıdır.
        Expect(e.Notes.Any(n => n.Kind is StatusKind.Error or StatusKind.Warning), "Uyarı / hata bildirimi beklenir");
        Expect(e.Notes.Last().Kind == StatusKind.Warning && e.Notes.Last().Message.Contains("2.json"), $"Son mesaj bozuk dosyayı anmalı: {e.Notes.Last().Message}");
    });

    public static void CorruptDefectBlocksCompleteAndReport() => WithVm(e =>
    {
        e.Open();
        e.Vm.AddDefect("Dent");
        File.WriteAllText(DefectFile(e.Folder, 2), "{");
        e.Notes.Clear();
        e.Vm.Complete();
        Expect(e.Notes.Any(n => n.Kind == StatusKind.Error && n.Message.Contains("okunamayan")), "Okunamayan kusur dosyası varken muayene kapatılmamalı");
        Expect(e.Store.LoadSession(e.Folder)!.Status == SessionStatus.Open, "Oturum açık kalmalı");
        e.Vm.CreateReportAsync().GetAwaiter().GetResult();
        Expect(e.Pdf.Calls.Count == 0 && e.Notes.Last().Message.Contains("okunamayan"), "Eksik kusurla rapor üretilmemeli");
    });

    public static void NextDefectIdNeverReusesCorruptFileNumber() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        File.WriteAllText(DefectFile(f, 2), "{");
        var added = store.AddDefect(f, "Nick");
        Expect(added.Id == 3, $"Bozuk 2.json'un numarası yeniden kullanılmamalı: {added.Id}");
        Expect(File.ReadAllText(DefectFile(f, 2)) == "{", "Bozuk dosya ezilmemeli");
    });

    public static void NullCollectionsBehaveLikeEmpty()
    {
        var bad = new List<string>();
        foreach (var prop in new[] { "photos", "dispositions", "markups" })
        {
            try
            {
                WithVm(e =>
                {
                    var f = Seed(e.Store);
                    e.Store.AddDefect(f, "Dent");
                    EditJson(DefectFile(f, 1), o => { if (prop == "markups") o["photos"] = new JsonArray("1_photo_01.jpg"); o[prop] = null; });
                    e.Open();
                    e.Vm.Select(1);
                });
            }
            catch (Exception ex) { bad.Add($"{prop}=null -> {ex.GetType().Name}"); }
        }
        if (bad.Count > 0)
            Fail("null koleksiyon boş liste gibi davranır ya da kontrollü hata verir", string.Join("; ", bad),
                "Models.cs:51-54,68-71 (setter null kabul eder), InspectionViewModel.Photos.cs:22");
    }

    public static void OversizedSessionFileRejectedWithMessage() => WithVm(e =>
    {
        var f = Seed(e.Store);
        var path = Path.Combine(f, "session.json");
        File.WriteAllText(path, "{\"partNumber\":\"PD170\"}" + new string(' ', (int)SessionStore.MaxJsonBytes + 1));
        Expect(Throws<InvalidDataException>(() => e.Store.LoadSession(f)), "Store InvalidDataException atmalı");
        e.Open();
        Expect(e.Notes.Any(n => n.Kind == StatusKind.Error), "VM hata mesajı göstermeli");
    });

    public static void DeepNestingIsHandledByViewModel() => WithVm(e =>
    {
        var f = Seed(e.Store);
        Directory.CreateDirectory(Path.Combine(f, "defects"));
        File.WriteAllText(DefectFile(f, 1), "{\"id\":1,\"notes\":" + new string('[', 200) + new string(']', 200) + "}");
        Expect(e.Store.LoadDefects(f).Count == 0 && e.Store.TakeWarnings().Count == 1, "Derin iç içe dosya (MaxDepth 64) atlanmalı ve uyarı üretmeli");
        NoThrow("JsonException yakalanır, hata mesajı", "InspectionViewModel.cs:797", () => e.Open());
    });

    public static void EnumOutOfRangeRejected() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        EditJson(Path.Combine(f, "session.json"), o => o["status"] = 999);
        var problems = new List<string>();
        try
        {
            var s = store.LoadSession(f);
            if (s is not null && !Enum.IsDefined(s.Status)) problems.Add($"Session.Status={(int)s.Status} (tanımsız) kabul edildi");
        }
        catch (InvalidDataException) { }

        store.AddDefect(f, "Dent");
        var d = store.LoadDefects(f)[0];
        d.Dispositions.Add(new Disposition { Seq = 1, Decision = Decision.Accept, Source = DecisionSource.Engineer, DecidedBy = "x" });
        File.WriteAllText(DefectFile(f, 1), JsonSerializer.Serialize(d, SessionStore.JsonOptions));
        EditJson(DefectFile(f, 1), o =>
        {
            var disp = (JsonObject)((JsonArray)o["dispositions"]!)[0]!;
            disp["decision"] = 999;
            disp["source"] = -1;
        });
        // Aralık dışı sayılar artık kabul edilmez: kusur dosyası atlanır, uyarı üretilir (kayıt yüklenmez).
        var reloaded = store.LoadDefects(f);
        if (reloaded.Count > 0)
            problems.Add($"Disposition.Decision={(int)reloaded[0].Dispositions[0].Decision} (tanımsız) kabul edildi");
        if (store.TakeWarnings().Count == 0)
            problems.Add("bozuk kusur dosyası için uyarı yok");
        Expect(Throws<InvalidDataException>(() => store.UpdateDefect(f, 1, x => x.Notes = "x")), "Aralık dışı karar yazma yolunda da reddedilmeli");

        if (problems.Count > 0)
            Fail("aralık dışı enum sayısı reddedilir (AllowIntegerValues=false) ya da 'bilinmeyen' sayılır", string.Join("; ", problems), "SessionStore.cs:19 (JsonStringEnumConverter tamsayı kabul eder)");
    });

    public static void EnumUnknownTextHandledByViewModel() => WithVm(e =>
    {
        var f = Seed(e.Store);
        EditJson(Path.Combine(f, "session.json"), o => o["status"] = "Foo");
        NoThrow("bilinmeyen enum metni JsonException olarak yakalanır, mesaj gösterilir", "InspectionViewModel.cs:797", () => e.Open());
    });

    private static void CorruptOneOfThree(string root)
    {
        var store = new SessionStore(root);
        Seed(store, "PD170", "OP40", "SN-1");
        Seed(store, "PD170", "OP40", "SN-2");
        var bad = Seed(store, "PD170", "OP40", "SN-3");
        File.WriteAllText(Path.Combine(bad, "session.json"), "{\"partNumber\":");
    }

    public static void IndexRebuildSurvivesOneCorruptSession() => WithRoot(root =>
    {
        CorruptOneOfThree(root);
        List<IndexRow>? rows = null;
        NoThrow("2 satır döner, bozuk olan raporlanır", "IndexBuilder.cs:43-47 (try/catch yok)", () => rows = IndexBuilder.Rebuild(new SessionStore(root)));
        Expect(rows is { Count: 2 }, $"2 satır beklenir: {rows?.Count}");
    });

    public static void AdminRecordsOpensWithCorruptSession() => WithAdmin(a =>
    {
        CorruptOneOfThree(a.Root);
        var records = new RecordsViewModel(a.Context, a.Saver, a.Os, a.Clock);
        records.Notify += (m, k) => a.Notes.Add((m, k));
        NoThrow("Refresh hata mesajı verir, istisna sızmaz", "RecordsViewModel.cs:108-112, AdminViewModelBase.cs:20", () => records.Refresh());
    });

    public static void DefectTypeTemplateFileVariantsFallBackToDefaults() => WithRoot(root =>
    {
        var bad = new List<string>();
        var variants = new Dictionary<string, string>
        {
            ["[null]"] = "[null]",
            ["fields:null"] = "[{\"name\":\"x\",\"fields\":null}]",
            ["{}"] = "{}",
            ["101 tip"] = "[" + string.Join(",", Enumerable.Range(0, 101).Select(i => $"{{\"name\":\"T{i}\",\"fields\":[]}}")) + "]",
        };
        Directory.CreateDirectory(SharedConfig.Folder(root));
        foreach (var (label, json) in variants)
        {
            File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "defect_types.json"), json);
            try
            {
                var list = SharedConfig.LoadDefectTypes(root);
                if (list.Any(t => t is null || t.Fields is null))
                    bad.Add($"{label}: null alanlı şablon döndü");
                else if (label != "fields:null" && !ReferenceEquals(list, SharedConfig.Defaults))
                    bad.Add($"{label}: varsayılanlara dönmedi");
            }
            catch (Exception ex) { bad.Add($"{label}: {ex.GetType().Name}"); }
        }
        if (bad.Count > 0)
            Fail("bozuk / null elemanlı şablon varsayılanlara döner, istisna yok", string.Join("; ", bad), "SharedConfig.cs:72-87,97-99 (null eleman Name erişimi, Fields doğrulanmıyor)");
    });

    public static void OtherConfigFilesFallBackToDefaults() => WithRoot(root =>
    {
        var bad = new List<string>();
        Directory.CreateDirectory(SharedConfig.Folder(root));
        foreach (var content in new[] { "{", "null", "{\"999\":\"x\"}", "[null]" })
        {
            foreach (var file in new[] { "measure_fields.json", "decision_texts.json", "inspector_identity.json" })
                File.WriteAllText(Path.Combine(SharedConfig.Folder(root), file), content);
            try { Expect(SharedConfig.LoadCustomFields(root).Count == 0, "ek alan boş olmalı"); }
            catch (Exception ex) { bad.Add($"measure_fields '{content}': {ex.GetType().Name}"); }
            try { Expect(SharedConfig.LoadDecisionTexts(root).Count == SharedConfig.DefaultDecisionTexts.Count, "varsayılan açıklamalar"); }
            catch (Exception ex) { bad.Add($"decision_texts '{content}': {ex.GetType().Name}"); }
            try { Expect(!SharedConfig.LoadUseWindowsUser(root), "kapalı sayılmalı"); }
            catch (Exception ex) { bad.Add($"inspector_identity '{content}': {ex.GetType().Name}"); }
        }
        if (bad.Count > 0)
            Fail("bozuk yapılandırma dosyaları varsayılana döner, istisna yok", string.Join("; ", bad), "SharedConfig.cs:134-149,225-246,284-295");
    });

    public static void CorruptIndexFileReadIsControlled() => WithRoot(root =>
    {
        File.WriteAllText(Path.Combine(root, IndexBuilder.FileName), "{");
        try { IndexBuilder.Read(new SessionStore(root)); }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { }
        catch (Exception ex) { Fail("kontrollü hata (InvalidDataException) ya da boş liste", Obs(ex), "IndexBuilder.cs:81-87"); }
    });

    public static void IdMismatchDoesNotOverwrite() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        store.AddDefect(f, "Scratch");
        store.AddDefect(f, "Nick");
        File.Copy(DefectFile(f, 3), DefectFile(f, 5));   // 5.json içinde "id": 3
        var before = Sha(DefectFile(f, 3));
        try { store.UpdateDefect(f, 5, d => d.Notes = "x"); }
        catch (Exception) { /* uyuşmazlık hatası da kabul */ }
        var after = Sha(DefectFile(f, 3));
        if (before != after)
            Fail("5.json / id=3 uyuşmazlığı hata verir, 3.json değişmez (SHA-256 aynı)", "3.json SHA-256 değişti (5.json güncellemesi 3.json'un üzerine yazdı)",
                "SessionStore.cs:94-100 (okuma 5.json), :297-307 (yazma DefectPath(defect.Id) = 3.json)");
    });

    public static void DuplicateIdsAreDetected() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        File.Copy(DefectFile(f, 1), Path.Combine(f, "defects", "kopya.json"));
        try
        {
            var list = store.LoadDefects(f);
            if (list.Count(d => d.Id == 1) > 1)
                Fail("yinelenen Id raporlanır, ikinci dosya yok sayılır", $"LoadDefects iki kayıt döndürdü (Id=1 x{list.Count(d => d.Id == 1)})", "SessionStore.cs:60-70 (ad/Id eşleşmesi denetlenmiyor)");
        }
        catch (InvalidDataException) { }
    });

    public static void NextIdDoesNotOverflow() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        // Adı ve Id'si uyuşan en büyük numara: bir sonraki numara taşmaz, kontrollü hata verilir.
        File.Move(DefectFile(f, 1), DefectFile(f, int.MaxValue));
        EditJson(DefectFile(f, int.MaxValue), o => o["id"] = int.MaxValue);
        Expect(Throws<InvalidOperationException>(() => store.AddDefect(f, "Scratch")), "Sınırda InvalidOperationException beklenir");
        var negative = Directory.EnumerateFiles(Path.Combine(f, "defects"), "-*.json").ToList();
        if (negative.Count > 0)
            Fail("kontrollü hata (checked / üst sınır); negatif numaralı dosya oluşmaz", $"{Path.GetFileName(negative[0])} oluştu (Max+1 taştı)", "SessionStore.cs NextDefectId");

        // Fotoğraf adı 5 haneli numarayla sınırlı: 99999 numaralı kusurdan sonra yeni kusur açılmaz.
        File.Move(DefectFile(f, int.MaxValue), DefectFile(f, SessionStore.MaxDefectId));
        EditJson(DefectFile(f, SessionStore.MaxDefectId), o => o["id"] = SessionStore.MaxDefectId);
        Expect(Throws<InvalidOperationException>(() => store.AddDefect(f, "Scratch")), $"{SessionStore.MaxDefectId} sonrası yeni kusur açılmamalı");
        Expect(Directory.EnumerateFiles(Path.Combine(f, "defects"), "*.json").Count() == 1, "Başka dosya oluşmamalı");
    });

    public static void NonPositiveIdsRejectedOnLoad() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        var bad = new List<string>();
        foreach (var id in new[] { 0, -1 })
        {
            File.Copy(DefectFile(f, 1), DefectFile(f, 7), overwrite: true);
            EditJson(DefectFile(f, 7), o => o["id"] = id);
            try
            {
                if (store.LoadDefects(f).Any(d => d.Id == id)) bad.Add($"Id={id} yüklendi");
            }
            catch (InvalidDataException) { }
        }
        if (bad.Count > 0)
            Fail("Id <= 0 yüklemede reddedilir", string.Join("; ", bad), "SessionStore.cs:60-70");
    });

    public static void NonDefectJsonFilesAreIgnored() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        Directory.CreateDirectory(Path.Combine(f, "defects"));
        File.WriteAllText(Path.Combine(f, "defects", "notlar.json"), "{}");
        if (store.LoadDefects(f).Count > 0)
            Fail("ad ^\\d+\\.json$ değilse okunmaz", "notlar.json Id=0 kusur olarak listelendi", "SessionStore.cs:66 (EnumerateFiles(\"*.json\"))");
    });

    public static void HiddenSuggestionsFromJsonIsTolerant()
    {
        foreach (var json in new[] { "{", "null", new string('a', 10 * 1024 * 1024), "[1,2]", "{\"part\":5}" })
            NoThrow("bozuk / büyük içerik varsayılana döner", "HiddenSuggestionSet.cs:52-75", () => HiddenSuggestionSet.FromJson(json));
    }

    public static void InstallSettingsCorruptFallsBackToDemoRoot() => WithRoot(root =>
    {
        var start = Path.Combine(root, "a", "b", "c", "d");
        Directory.CreateDirectory(start);
        var demo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MuayeneKayitDemo");
        foreach (var json in new[] { "{", "{\"rootPath\":5}", "null", "[]" })
        {
            File.WriteAllText(Path.Combine(start, InstallSettings.FileName), json);
            string result = "";
            NoThrow($"bozuk ayar ('{json}') demo köke düşer", "InstallSettings.cs:29-50", () => result = InstallSettings.LoadRootPath(start));
            Expect(result == demo, $"'{json}' -> {result}");
        }
    });

    public static void ManyShapesLoadFastInPhotoCards() => WithVm(e =>
    {
        var f = Seed(e.Store);
        e.Store.AddDefect(f, "Dent");
        e.Store.UpdateDefect(f, 1, d =>
        {
            d.Photos.Add("1_photo_01.jpg");
            var m = new PhotoMarkup { ImageWidth = 100, ImageHeight = 100 };
            for (var i = 0; i < 10_000; i++) m.Shapes.Add(Annotation.CenterSquare(100, 100, 1));
            d.Markups["1_photo_01.jpg"] = m;
        });
        e.Open();
        var sw = Stopwatch.StartNew();
        e.Vm.Select(1);
        sw.Stop();
        Expect(e.Vm.Photos.Count == 1 && sw.Elapsed < TimeSpan.FromSeconds(1), $"Kart listesi {sw.ElapsedMilliseconds} ms");
    });

    public static void NanCoordinateInFileHandledByViewModel() => WithVm(e =>
    {
        var f = Seed(e.Store);
        e.Store.AddDefect(f, "Dent");
        var text = File.ReadAllText(DefectFile(f, 1))
            .Replace("\"photos\": []", "\"photos\": [\"1_photo_01.jpg\"]")
            .Replace("\"markups\": {}", "\"markups\": {\"1_photo_01.jpg\":{\"imageWidth\":1,\"imageHeight\":1,\"shapes\":[{\"kind\":\"Box\",\"x1\":NaN,\"y1\":0,\"x2\":1,\"y2\":1,\"defectId\":1}]}}");
        File.WriteAllText(DefectFile(f, 1), text);
        NoThrow("NaN koordinat kontrollü hata verir, çökme yok", "InspectionViewModel.cs:797 (JsonException), SessionStore.cs:19", () => { e.Open(); e.Vm.Select(1); });
    });

    // ═════════════════════════ S-PIN ═════════════════════════

    public static void PinIsHashedWithUniqueSalt()
    {
        string a = TempRoot(), b = TempRoot();
        try
        {
            SharedConfig.SetPin(a, TestPin);
            SharedConfig.SetPin(b, TestPin);
            var ta = File.ReadAllText(Path.Combine(SharedConfig.Folder(a), "admin.json"));
            var tb = File.ReadAllText(Path.Combine(SharedConfig.Folder(b), "admin.json"));
            Expect(!ta.Contains(TestPin) && !tb.Contains(TestPin), "admin.json PIN metnini içermemeli");
            var ra = JsonSerializer.Deserialize<AdminPinRecord>(ta, SessionStore.JsonOptions)!;
            var rb = JsonSerializer.Deserialize<AdminPinRecord>(tb, SessionStore.JsonOptions)!;
            Expect(ra.Salt != rb.Salt && ra.Hash != rb.Hash, "Tuz ve özet her kökte farklı olmalı");
            Expect(SharedConfig.VerifyPin(a, TestPin) && !SharedConfig.VerifyPin(a, "000000"), "Doğrulama çalışmalı");
        }
        finally { Cleanup(a); Cleanup(b); }
    }

    public static void PinVerifyCostIsMeasured() => WithRoot(root =>
    {
        SharedConfig.SetPin(root, TestPin);
        var total = Stopwatch.StartNew();
        var runs = 0;
        var sw = Stopwatch.StartNew();
        while (runs < 10 && total.Elapsed < TimeSpan.FromSeconds(30))
        {
            SharedConfig.VerifyPin(root, "111111");
            runs++;
        }
        sw.Stop();
        var ms = sw.Elapsed.TotalMilliseconds / runs;
        var hours = ms * 100_000_000 / 1000 / 3600;   // 8 haneli yeni PIN uzayı: 10^8
        Console.WriteLine($"        · ÖLÇÜM: VerifyPin ≈ {ms:0.0} ms/deneme ({runs} ölçüm); 10^8 PIN uzayı (8 hane) tek çekirdekte ≈ {hours:0.0} saat (en kötü durum), 16 çekirdekte ≈ {hours / 16:0.0} saat.");
    });

    public static void LockoutSurvivesNewLoginViewModel() => WithAdmin(a =>
    {
        SharedConfig.SetPin(a.Root, TestPin);
        var first = new LoginViewModel(a.Context, a.Clock);
        for (var i = 0; i < 5; i++) { first.Pin = "000000"; first.Login(); }
        Expect(first.Error.Length > 0, "Beşinci hatalı denemeden sonra hata beklenir");

        var second = new LoginViewModel(a.Context, a.Clock);
        var loggedIn = false;
        second.LoggedIn += () => loggedIn = true;
        second.Pin = TestPin;
        second.Login();
        if (loggedIn)
            Fail("kilitlenme yeniden açılışta sürer (kalıcı durum)", "yeni LoginViewModel ile doğru PIN hemen kabul edildi (sayaç ve _lockedUntil yalnız bellekte)",
                "LoginViewModel.cs:17-18,79-91");
    });

    public static void FailedLoginIsAudited() => WithAdmin(a =>
    {
        SharedConfig.SetPin(a.Root, TestPin);
        var vm = new LoginViewModel(a.Context, a.Clock);
        vm.Pin = "000000";
        vm.Login();
        var last = AuditLog.ReadTail(a.Context.Store, 1).FirstOrDefault();
        if (last is null || !last.Action.Contains("fail", StringComparison.OrdinalIgnoreCase))
            Fail("her hatalı deneme denetim kaydına yazılır (admin.login.fail)", $"son satır: {(last is null ? "(denetim kaydı boş)" : last.Action)}", "LoginViewModel.cs:79-94 (yalnız kilitlenme yazılıyor)");
    });

    public static void AdminJsonDeletionDoesNotSilentlyResetPin() => WithAdmin(a =>
    {
        var vm = new LoginViewModel(a.Context, a.Clock);
        vm.Pin = vm.PinRepeat = TestPin;
        vm.Login();
        File.Delete(Path.Combine(SharedConfig.Folder(a.Root), "admin.json"));
        var attacker = new LoginViewModel(a.Context, a.Clock);
        if (attacker.IsFirstRun && !attacker.Hint.Contains("sıfırl", StringComparison.OrdinalIgnoreCase))
            Fail("PIN daha önce belirlenmiş bir kökte admin.json silinince ilk kurulum açılmaz ya da sıfırlama uyarısı/ikinci doğrulama istenir",
                "IsFirstRun=true; kullanıcı kendi PIN'ini belirleyip admin olur (iz yalnız 'İlk PIN belirlendi')", "LoginViewModel.cs:41,69-78");
    });

    public static void HugeIterationCountDoesNotFreezeVerify() => WithRoot(root =>
    {
        Directory.CreateDirectory(SharedConfig.Folder(root));
        var rec = new AdminPinRecord { Salt = Convert.ToBase64String(new byte[16]), Hash = Convert.ToBase64String(new byte[32]), Iterations = int.MaxValue };
        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "admin.json"), JsonSerializer.Serialize(rec, SessionStore.JsonOptions));
        if (!CompletesWithin(() => SharedConfig.VerifyPin(root, TestPin), TimeSpan.FromSeconds(2)))
            Fail("≤ 2 sn'de false / hata döner (tur sayısına üst sınır)", "2 sn içinde dönmedi (Iterations=int.MaxValue arayüz iş parçacığını kilitler)", "SharedConfig.cs:332-334");
    });

    private static void LoginWithAdminJson(string content)
    {
        WithAdmin(a =>
        {
            Directory.CreateDirectory(SharedConfig.Folder(a.Root));
            File.WriteAllText(Path.Combine(SharedConfig.Folder(a.Root), "admin.json"), content);
            var vm = new LoginViewModel(a.Context, a.Clock);
            vm.Pin = TestPin;
            NoThrow("login hata mesajı verir, istisna sızmaz", "SharedConfig.cs:332-337, LoginViewModel.cs:97-98", () => vm.Login());
            Expect(vm.Error.Length > 0, "Hata mesajı beklenir");
        });
    }

    public static void BadBase64AdminJsonHandled() =>
        LoginWithAdminJson("{\"salt\":\"" + Convert.ToBase64String(new byte[16]) + "\",\"hash\":\"***\",\"iterations\":200000}");

    public static void CorruptAdminJsonHandled() => LoginWithAdminJson("{");

    public static void PinFormatValidation() => WithRoot(root =>
    {
        foreach (var bad in new[] { "1234567890123", "12a456", "", "12345" })
            Expect(Throws<ArgumentException>(() => SharedConfig.SetPin(root, bad)), $"PIN '{bad}' reddedilmeli");
        SharedConfig.SetPin(root, TestPin);
        Expect(!SharedConfig.VerifyPin(root, "1234567890123"), "13 haneli doğrulama false olmalı");
        Expect(!SharedConfig.VerifyPin(root, ""), "Boş PIN false olmalı");
    });

    public static void PinRejectsNonAsciiDigits() => WithRoot(root =>
    {
        if (!Throws<ArgumentException>(() => SharedConfig.SetPin(root, "１２３４５６")))
            Fail("PIN yalnızca ASCII rakam (0-9) kabul eder", "tam genişlik rakamlar (U+FF11..) kabul edildi (\\d Unicode rakamları eşler)", "SharedConfig.cs:311 (Regex \\d, RegexOptions.ECMAScript yok)");
    });

    // ═════════════════════════ S-AUD ═════════════════════════

    private static SessionStore ThreeEntryAudit(string root)
    {
        var store = new SessionStore(root);
        AuditLog.Append(store, "a", "x.one", "d1");
        AuditLog.Append(store, "a", "x.two", "d2");
        AuditLog.Append(store, "a", "x.three", "d3");
        return store;
    }

    public static void AuditTailDeletionIsDetected() => WithRoot(root =>
    {
        var store = ThreeEntryAudit(root);
        var path = Path.Combine(root, AuditLog.FileName);
        File.WriteAllLines(path, File.ReadAllLines(path).Take(2));
        var (ok, bad) = AuditLog.Verify(store);
        if (ok)
            Fail("sondan satır silinmesi bütünlük hatası verir (dış çapa)", "Verify (true, null) döndü", "AuditLog.cs:42-76 (son özet dışarıda saklanmıyor)");
    });

    private static string Chain(string prev, string ts, string actor, string action, string details) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { prev, ts, actor, action, details })))).ToLowerInvariant();

    public static void AuditChainIsKeyed() => WithRoot(root =>
    {
        var store = ThreeEntryAudit(root);
        var path = Path.Combine(root, AuditLog.FileName);
        var entries = File.ReadAllLines(path).Select(l => JsonSerializer.Deserialize<AuditEntry>(l)!).ToList();
        // Saldırgan: 2. satırın ayrıntısını değiştirip zinciri genel algoritmayla baştan hesaplar.
        var forged = new List<string>();
        var prev = new string('0', 64);
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var details = i == 1 ? "SAHTE" : e.Details;
            var hash = Chain(prev, e.Ts, e.Actor, e.Action, details);
            forged.Add(JsonSerializer.Serialize(new AuditEntry(e.Ts, e.Actor, e.Action, details, prev, hash)));
            prev = hash;
        }
        File.WriteAllLines(path, forged);
        if (AuditLog.Verify(store).Ok)
            Fail("anahtarlı zincir (HMAC / imza): yeniden hesaplanmış zincir reddedilir", "Verify, baştan hesaplanmış sahte zinciri 'sağlam' buldu", "AuditLog.cs:128-134 (anahtarsız SHA-256)");
    });

    public static void RecordContentIsBoundToAudit() => WithVm(e =>
    {
        e.Open();
        e.Vm.AddDefect("Dent");
        e.Vm.DepthText = "0.5";
        e.Vm.SaveMeasurements();
        Expect(AuditLog.Verify(e.Store).Ok, "Ön koşul: zincir sağlam");
        EditJson(DefectFile(e.Folder, 1), o => o["notes"] = "elle değiştirildi");
        if (AuditLog.Verify(e.Store).Ok)
            Fail("kayıt içeriği özeti denetimde; elle düzenleme doğrulamada uyarı verir", "defects\\1.json doğrudan değiştirildi, Verify 'sağlam' dedi", "SessionStore.cs:297-304, AuditLog.cs:27-39 (details yalnız #id/ad)");
    }, realAudit: true);

    public static void CorruptAuditTailDoesNotBreakOperations() => WithVm(e =>
    {
        e.Open();
        e.Vm.AddDefect("Dent");
        File.AppendAllText(Path.Combine(e.Root, AuditLog.FileName), "{bozuk" + Environment.NewLine);
        e.Picker.Picks.Enqueue(TempJpeg(e.Root));
        NoThrow("istisna sızmaz; kayıt ve denetim tutarlı ya da açık uyarı", "AuditLog.cs:110-126 (LastHash JsonException), InspectionViewModel.Photos.cs:71-72",
            () => e.Vm.AddPhotoFromFileAsync().GetAwaiter().GetResult());
    }, realAudit: true);

    public static void AuditLineInjectionImpossible() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        AuditLog.Append(store, "ilk", "x.first", "d");
        var before = File.ReadAllLines(Path.Combine(root, AuditLog.FileName)).Length;
        AuditLog.Append(store, "a\n{\"ts\":\"x\"}", "x.inject", "<b>x</b>\r\n");
        var lines = File.ReadAllLines(Path.Combine(root, AuditLog.FileName));
        Expect(lines.Length == before + 1, $"Tek satır eklenmeli: {before} -> {lines.Length}");
        Expect(AuditLog.Verify(store).Ok, "Zincir sağlam kalmalı");
    });

    public static void AuditFileDeletionIsDetected() => WithRoot(root =>
    {
        var store = ThreeEntryAudit(root);
        Seed(store);
        store.AddDefect(Path.Combine(root, "PD170", "OP40", "SN001"), "Dent");
        File.Delete(Path.Combine(root, AuditLog.FileName));
        var (ok, bad) = AuditLog.Verify(store);
        if (ok)
            Fail("kayıtlar varken _audit.log silinmesi 'kayıt yok ama veri var' uyarısı verir", "Verify (true, null) döndü", "AuditLog.cs:44-46");
    });

    public static void InspectorActionsAreAudited() => WithVm(e =>
    {
        e.Open();
        e.Vm.AddDefect("Dent");
        e.Vm.DepthText = "0.5";
        e.Vm.SaveMeasurements();
        e.Picker.Picks.Enqueue(TempJpeg(e.Root));
        e.Vm.AddPhotoFromFileAsync().GetAwaiter().GetResult();
        e.Dialogs.Decisions.Enqueue(new DecisionInput(null, "SPA-1", null, null));
        e.Vm.GiveDecisionAsync(e.Vm.DecisionOptions.First(o => o.Decision == Decision.Accept)).GetAwaiter().GetResult();
        e.Dialogs.UndoReasons.Enqueue("yanlış karar");
        e.Vm.UndoLastAsync().GetAwaiter().GetResult();
        e.Vm.AddDefect("Scratch");
        e.Dialogs.VoidReasons.Enqueue("yanlış tip");
        e.Vm.VoidAsync().GetAwaiter().GetResult();
        e.Vm.CreateReportAsync().GetAwaiter().GetResult();

        var actions = AuditLog.ReadTail(e.Store, 100).Select(a => a.Action).ToHashSet();
        var missing = new[] { "session.open", "defect.add", "defect.measure", "defect.photo", "disposition", "disposition.undo", "defect.void", "report.create" }
            .Where(a => !actions.Contains(a)).ToList();
        Expect(missing.Count == 0, "Denetim satırı yazılmayan işlemler: " + string.Join(", ", missing));
    }, realAudit: true);

    public static void AdminRootChangeIsAudited() => WithAdmin(a =>
    {
        var other = a.Root + "-yeni";
        Directory.CreateDirectory(other);
        try
        {
            var settings = new FakeAdminSettings { Root = a.Root };
            var vm = new SettingsViewModel(a.Context, settings) { RootText = other };
            vm.SaveRoot();
            var all = AuditLog.ReadTail(new SessionStore(a.Root), 50).Concat(AuditLog.ReadTail(new SessionStore(other), 50)).ToList();
            if (!all.Any(x => x.Action.Contains("root", StringComparison.OrdinalIgnoreCase)))
                Fail("kök değişimi denetim kaydına yazılır (admin.root.change)", $"eski ve yeni kökte kök değişimi satırı yok (kayıtlı eylemler: {string.Join(",", all.Select(x => x.Action).Distinct())})",
                    "SettingsViewModel.cs:63-72");
        }
        finally { Cleanup(other); }
    });

    // ═════════════════════════ S-LOCK ═════════════════════════

    private static string MakeLock(string path, TimeSpan age)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    public static void StaleSessionLockGivesControlledError() => WithVm(e =>
    {
        e.Open();
        MakeLock(Path.Combine(e.Folder, "session.lock"), TimeSpan.FromMinutes(11));
        var before = e.Store.LoadDefects(e.Folder).Count;
        NoThrow("anlaşılır mesaj (admin'e yönlendirir), istisna VM dışına sızmaz", "FileLock.cs:40-42", () => e.Vm.AddDefect("Dent"));
        Expect(e.Notes.Last().Kind == StatusKind.Error && e.Notes.Last().Message.Contains("admin"), $"Mesaj admin'i anmalı: {e.Notes.Last().Message}");
        Expect(e.Store.LoadDefects(e.Folder).Count == before, "Veri değişmemeli");
    });

    public static void FutureDatedLockIsNotAnEndlessTimeout() => WithRoot(root =>
    {
        var path = MakeLock(Path.Combine(root, "x.lock"), TimeSpan.FromDays(-1));
        try { FileLock.Acquire(path, TimeSpan.FromSeconds(1)).Dispose(); Fail("açıklayıcı hata", "kilit alındı", "FileLock.cs:54-69"); }
        catch (TimeoutException)
        {
            Fail("gelecek tarihli kilit 'şüpheli' sayılır, açıklayıcı hata / admin listesine düşer", "TimeoutException — her işlem 15 sn bekleyip düşer", "FileLock.cs:54-69 (IsStale yalnız geçmişe bakar)");
        }
        catch (InvalidOperationException) { }
    });

    public static void AdminListsFutureDatedLock() => WithRoot(root =>
    {
        var path = MakeLock(Path.Combine(root, "x.lock"), TimeSpan.FromDays(-1));
        var past = MakeLock(Path.Combine(root, "y.lock"), TimeSpan.FromMinutes(20));
        var list = SharedConfig.StaleLocks(root);
        Expect(list.Contains(past), "Eski kilit listelenmeli (regresyon)");
        if (!list.Contains(path))
            Fail("ileri tarihli kilit admin bakım listesinde görünür", "StaleLocks listesinde yok; arayüzden kaldırılamaz", "SharedConfig.cs:347-351");
    });

    public static void LiveLockCannotBeRemoved() => WithRoot(root =>
    {
        var path = Path.Combine(root, "live.lock");
        using var held = FileLock.Acquire(path);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - TimeSpan.FromMinutes(11));
        var threw = false;
        try { SharedConfig.RemoveStaleLock(root, path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { threw = true; }
        Expect(threw, "Açık tutamaçlı kilit silinememeli (kontrollü hata)");
        Expect(File.Exists(path), "Kilit dosyası yerinde kalmalı");
    });

    public static void RemoveStaleLockRejectsOutsideAndNonLock() => WithRoot(root =>
    {
        var sibling = root + "2";
        Directory.CreateDirectory(sibling);
        try
        {
            Expect(Throws<ArgumentException>(() => SharedConfig.RemoveStaleLock(root, Path.Combine(root, "..", "x.lock"))), "..\\x.lock reddedilmeli");
            Expect(Throws<ArgumentException>(() => SharedConfig.RemoveStaleLock(root, Path.Combine(root, "x.txt"))), "x.txt reddedilmeli");
            var other = MakeLock(Path.Combine(sibling, "x.lock"), TimeSpan.FromMinutes(20));
            Expect(Throws<ArgumentException>(() => SharedConfig.RemoveStaleLock(root, other)), "Kardeş kök reddedilmeli");
            Expect(File.Exists(other), "Kardeş kökteki dosya silinmemeli");
        }
        finally { Cleanup(sibling); }
    });

    public static void RootLocksDoNotHangApplicationForever() => WithVm(e =>
    {
        // Kökte yeni tarihli (eski sayılmayan) kilitler: 15 sn sonra TimeoutException, VM / dashboard mesaj verir.
        e.Open();
        MakeLock(Path.Combine(e.Root, "_audit.lock"), TimeSpan.Zero);
        MakeLock(Path.Combine(e.Root, "_index.lock"), TimeSpan.Zero);
        var dash = new DashboardViewModel(e.Store, SharedConfig.Defaults);
        var dashNotes = new List<StatusKind>();
        dash.Notify += (_, k) => dashNotes.Add(k);
        try
        {
            Parallel.Invoke(
                () => e.Vm.AddDefect("Dent"),
                () => dash.Refresh());
        }
        catch (AggregateException ex) { throw ex.InnerExceptions[0]; }
        Expect(e.Notes.Last().Kind == StatusKind.Error && e.Notes.Last().Message.Contains("Kilit"), $"VM mesajı: {e.Notes.Last().Message}");
        Expect(dashNotes.Contains(StatusKind.Error), "Dashboard hata mesajı vermeli");
        // Gözlem: audit kilidi yüzünden kusur dosyası yazıldı ama denetim satırı yazılamadı.
        Console.WriteLine($"        · GÖZLEM: _audit.lock varken kusur dosyası sayısı={e.Store.LoadDefects(e.Folder).Count}, denetim satırı eklenemedi (ÖB-04/ÖB-12).");
    }, realAudit: true);

    public static void LockPathThatIsDirectoryGivesControlledError() => WithVm(e =>
    {
        e.Open();
        Directory.CreateDirectory(Path.Combine(e.Folder, "session.lock"));
        var finished = CompletesWithin(() => e.Vm.AddDefect("Dent"), TimeSpan.FromSeconds(5));
        Expect(finished, "Döngüden çıkmalı");
        Expect(e.Notes.Last().Kind == StatusKind.Error, "Hata mesajı beklenir");
    });

    // ═════════════════════════ S-PHOTO ═════════════════════════

    public static void ImportRejectsBadExtensionAndSignature() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        var txt = Path.Combine(root, "a.txt"); File.WriteAllText(txt, "merhaba");
        var fakeJpg = Path.Combine(root, "b.jpg"); File.WriteAllText(fakeJpg, "bu bir metindir");
        var pngAsJpeg = Path.Combine(root, "c.png"); File.WriteAllBytes(pngAsJpeg, JpegBytes);
        foreach (var p in new[] { txt, fakeJpg, pngAsJpeg })
            Expect(Throws<InvalidDataException>(() => PhotoImporter.Import(folder, 1, p)), $"{Path.GetFileName(p)} reddedilmeli");
        var defects = Path.Combine(folder, "defects");
        Expect(!Directory.Exists(defects) || !Directory.EnumerateFiles(defects).Any(), "Kopya oluşmamalı");
    });

    public static void ImportRejectsOversizeFile() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        var big = Path.Combine(root, "big.jpg");
        using (var fs = new FileStream(big, FileMode.Create))
        {
            fs.Write(JpegBytes);
            fs.SetLength(PhotoImporter.MaxBytes + 1);
        }
        Expect(Throws<InvalidDataException>(() => PhotoImporter.Import(folder, 1, big)), "25 MB + 1 bayt reddedilmeli");
        File.Delete(big);
    });

    public static void BackgroundMaskRejectsOverflowingDimensions()
    {
        var bad = new List<string>();
        foreach (var (w, h) in new[] { (50_000, 50_000), (65_536, 65_536) })
        {
            try { BackgroundRemover.ComputeMask(new byte[16], w, h, new PixelRect(0, 0, 100, 100)); bad.Add($"{w}x{h}: kabul edildi"); }
            catch (ArgumentException) { }
            catch (Exception ex) { bad.Add($"{w}x{h}: {ex.GetType().Name}"); }
        }
        if (bad.Count > 0)
            Fail("geçersiz boyut ArgumentException ile reddedilir", string.Join("; ", bad), "BackgroundRemover.cs:18 (width * height * 4 int taşması)");
    }

    public static void ImportStripsExif() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        var exif = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x10 };
        exif.AddRange(Encoding.ASCII.GetBytes("Exif\0\0"));
        exif.AddRange(new byte[8]);
        exif.AddRange(new byte[] { 0xFF, 0xD9 });
        var src = Path.Combine(root, "gps.jpg");
        File.WriteAllBytes(src, exif.ToArray());
        var name = PhotoImporter.Import(folder, 1, src);
        var bytes = File.ReadAllBytes(Path.Combine(folder, "defects", name));
        if (Encoding.Latin1.GetString(bytes).Contains("Exif"))
            Fail("içe aktarılan kopyada EXIF yok (ya da belgelenmiş kabul)", "hedef dosyada 'Exif' APP1 segmenti aynen duruyor (bayt bayt kopya)", "PhotoImporter.cs:36 (File.Copy)");
    });

    public static void WriteCleanedValidatesNameAndContent() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        Directory.CreateDirectory(Path.Combine(folder, "defects"));
        Expect(Throws<ArgumentException>(() => PhotoImporter.WriteCleaned(folder, "..\\x.jpg", JpegBytes)), "..\\x.jpg reddedilmeli");
        Expect(Throws<ArgumentException>(() => PhotoImporter.WriteCleaned(folder, "1_photo_01_clean.jpg", JpegBytes)), "_clean adı reddedilmeli");
        Expect(Throws<InvalidDataException>(() => PhotoImporter.WriteCleaned(folder, "1_photo_01.jpg", new byte[] { 1, 2, 3, 4 })), "JPEG olmayan içerik reddedilmeli");
        Expect(PhotoImporter.WriteCleaned(folder, "1_photo_01.jpg", JpegBytes) == "1_photo_01_clean.jpg", "Geçerli temiz kopya yazılmalı");
    });

    public static void ImportNeverOverwritesExistingPhoto() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        var defects = Path.Combine(folder, "defects");
        Directory.CreateDirectory(defects);
        File.WriteAllBytes(Path.Combine(defects, "1_photo_01.jpg"), JpegBytes.Concat(new byte[] { 1 }).ToArray());
        File.WriteAllBytes(Path.Combine(defects, "1_photo_03.jpg"), JpegBytes.Concat(new byte[] { 3 }).ToArray());   // 02 silinmiş
        var before = Sha(Path.Combine(defects, "1_photo_03.jpg"));
        try { PhotoImporter.Import(folder, 1, TempJpeg(root)); }
        catch (IOException) { }
        Expect(Sha(Path.Combine(defects, "1_photo_03.jpg")) == before, "Var olan dosya değişmemeli");
    });

    // ═════════════════════════ S-LAUNCH ═════════════════════════

    private static string ProgramRootWith(string root, string version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ProgramLayout.VersionFile), version);
        return root;
    }

    public static void ProgramLayoutRejectsEscapingVersions() => WithRoot(root =>
    {
        foreach (var v in new[] { "..\\x", "C:\\x", "\\\\testhost\\s", "", "CON" })
        {
            ProgramRootWith(root, v);
            Expect(Throws<FileNotFoundException>(() => ProgramLayout.Resolve(root, "Inspector", false)), $"surum.txt='{v}' reddedilmeli");
        }
    });

    private static void PlaceExe(string root, string versionDir)
    {
        var dir = Path.Combine(root, versionDir, "Inspector-x64");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "MuayeneKayit.Inspector.exe"), new byte[] { 0x4D, 0x5A });
    }

    public static void ProgramLayoutRejectsDotVersion() => WithRoot(root =>
    {
        ProgramRootWith(root, ".");
        PlaceExe(root, ".");
        string? exe = null;
        try { exe = ProgramLayout.Resolve(root, "Inspector", false); } catch (FileNotFoundException) { }
        if (exe is not null)
            Fail("surum.txt='.' reddedilir", $"çözüldü: {exe}", "ProgramLayout.cs:25,30");
    });

    public static void ProgramLayoutAcceptsOnlyNumericVersions() => WithRoot(root =>
    {
        var bad = new List<string>();
        foreach (var v in new[] { "abc", "x y", "1.0.2 x" })
        {
            ProgramRootWith(root, v);
            PlaceExe(root, v);
            try { ProgramLayout.Resolve(root, "Inspector", false); bad.Add(v); } catch (FileNotFoundException) { }
        }
        if (bad.Count > 0)
            Fail("yalnızca ^\\d+(\\.\\d+){1,3}$ sürüm biçimi kabul edilir", "kabul edilen: " + string.Join(" | ", bad.Select(b => $"'{b}'")), "ProgramLayout.cs:25");
    });

    public static void InstallSettingsSearchDepth() => WithRoot(root =>
    {
        var start = Path.Combine(root, "a", "b", "c", "d");
        Directory.CreateDirectory(start);
        var third = Path.Combine(root, "a", InstallSettings.FileName);   // d -> c(1) -> b(2) -> a(3)
        File.WriteAllText(third, "{}");
        Expect(InstallSettings.Find(start) == third, "3. üst klasördeki ayar bulunmalı");
        File.Delete(third);
        File.WriteAllText(Path.Combine(root, InstallSettings.FileName), "{}");   // 4. üst
        Expect(InstallSettings.Find(start) is null, "4. üst klasördeki ayar bulunmamalı");
    });

    public static void RootPathExpansionAndRelativeHandling() => WithRoot(root =>
    {
        var start = Path.Combine(root, "a", "b", "c", "d");
        Directory.CreateDirectory(start);
        var file = Path.Combine(start, InstallSettings.FileName);

        File.WriteAllText(file, "{\"rootPath\":\"%TEMP%\\\\MK-SEC-0123456789abcdef0123456789abcdef\"}");
        var expanded = InstallSettings.LoadRootPath(start);
        Expect(expanded.StartsWith(Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) && !expanded.Contains('%'), $"Genişletilmeli: {expanded}");

        File.WriteAllText(file, "{\"rootPath\":\"\\\\\\\\testhost\\\\s\"}");
        Expect(InstallSettings.LoadRootPath(start) == "\\\\testhost\\s", "UNC yalnız metin olarak döner (erişilmez)");

        File.WriteAllText(file, "{\"rootPath\":\"veri\"}");
        // Göreli yol çalışma klasörüne bağlı çözülürdü: açık hata ile reddedilir (uygulama yanlış klasöre yazmasın).
        var ex = Catch<InvalidDataException>(() => InstallSettings.LoadRootPath(start));
        Expect(ex.Message.Contains("tam yol"), $"Mesaj tam yol gereğini söylemeli: {ex.Message}");
        Expect(Throws<ArgumentException>(() => InstallSettings.SaveRootPath(start, "veri")), "Göreli yol kaydedilemez");
        Expect(Throws<ArgumentException>(() => InstallSettings.SaveRootPath(start, "..\\veri")), "Üst klasöre göreli yol kaydedilemez");
    });

    // ═════════════════════════ S-NTFS (UNIT kısmı) ═════════════════════════

    public static void ReadOnlyConfigGivesControlledError() => WithAdmin(a =>
    {
        SharedConfig.SaveDefectTypes(a.Root, SharedConfig.Defaults);
        var file = Path.Combine(SharedConfig.Folder(a.Root), "defect_types.json");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            Expect(Throws<UnauthorizedAccessException>(() => SharedConfig.SaveDefectTypes(a.Root, SharedConfig.Defaults)), "Yazma erişim hatası vermeli");
            var types = new TypesViewModel(a.Context, new FakeConfirm());
            types.Load();
            types.Notify += (m, k) => a.Notes.Add((m, k));
            NoThrow("'erişim reddedildi' mesajı, çökme yok", "TypesViewModel.cs:317-334, AdminViewModelBase.cs:20", () => types.Save());
            Expect(a.Notes.Any(n => n.Kind == StatusKind.Error), "Hata bildirimi beklenir");
        }
        finally { File.SetAttributes(file, FileAttributes.Normal); }
    });

    // ═════════════════════════ Düzeltme regresyonları (güvenlik düzeltmeleri) ═════════════════════════

    public static void PinNewRecordsUseHigherIterationsAndLegacyStillVerifies() => WithRoot(root =>
    {
        SharedConfig.SetPin(root, TestPin);
        var record = JsonSerializer.Deserialize<AdminPinRecord>(File.ReadAllText(Path.Combine(SharedConfig.Folder(root), "admin.json")), SessionStore.JsonOptions)!;
        Expect(record.Iterations >= 600_000, $"Yeni PIN en az 600.000 tur kullanmalı: {record.Iterations}");
        Expect(SharedConfig.VerifyPin(root, TestPin), "Yeni kayıt doğrulanmalı");

        // Eski kayıt (6 hane, 200.000 tur) hâlâ doğrulanır.
        TestPins.WriteLegacy(root, "123456");
        Expect(SharedConfig.VerifyPin(root, "123456") && !SharedConfig.VerifyPin(root, "123457"), "Eski 6 haneli kayıt doğrulanmalı");
    });

    public static void PinIterationsOutsideRangeAreRejectedClearly() => WithRoot(root =>
    {
        foreach (var iterations in new[] { 1, SharedConfig.MinPinIterations - 1, SharedConfig.MaxPinIterations + 1 })
        {
            Directory.CreateDirectory(SharedConfig.Folder(root));
            var rec = new AdminPinRecord { Salt = Convert.ToBase64String(new byte[16]), Hash = Convert.ToBase64String(new byte[32]), Iterations = iterations };
            File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "admin.json"), JsonSerializer.Serialize(rec, SessionStore.JsonOptions));
            var ex = Catch<InvalidDataException>(() => SharedConfig.VerifyPin(root, TestPin));
            Expect(ex.Message.Contains("admin.json"), $"Mesaj admin.json'u anmalı: {ex.Message}");
        }
    });

    public static void PinLengthRulesForNewAndLegacyPins() => WithRoot(root =>
    {
        foreach (var bad in new[] { "482916", "4829165", "1234567890123", "１２３４５６７８" })
            Expect(Throws<ArgumentException>(() => SharedConfig.SetPin(root, bad)), $"Yeni PIN '{bad}' reddedilmeli");
        foreach (var good in new[] { "48291657", "482916571234" })
            SharedConfig.SetPin(root, good);
        Expect(SharedConfig.VerifyPin(root, "482916571234"), "12 haneli PIN doğrulanmalı");
    });

    public static void LegacySixDigitPinLogsInAndSuggestsChange() => WithAdmin(a =>
    {
        TestPins.WriteLegacy(a.Root, "123456");
        var vm = new LoginViewModel(a.Context, a.Clock);
        var loggedIn = false;
        vm.LoggedIn += () => loggedIn = true;
        vm.Pin = "123456";
        vm.Login();
        Expect(loggedIn && vm.Error.Length == 0, $"Eski 6 haneli PIN girişte kabul edilmeli: {vm.Error}");
        Expect(vm.PostLoginNotice.Contains("8-12"), $"Değiştirme önerisi beklenir: {vm.PostLoginNotice}");
    });

    public static void AdminPinResetNeedsExplicitConfirmationAndIsAudited() => WithAdmin(a =>
    {
        var first = new LoginViewModel(a.Context, a.Clock);
        Expect(first.IsFirstRun && !first.NeedsResetConfirm, "Temiz kökte normal ilk kurulum");
        first.Pin = first.PinRepeat = TestPin;
        first.Login();
        File.Delete(Path.Combine(SharedConfig.Folder(a.Root), "admin.json"));

        var vm = new LoginViewModel(a.Context, a.Clock);
        var loggedIn = false;
        vm.LoggedIn += () => loggedIn = true;
        Expect(vm.IsFirstRun && vm.NeedsResetConfirm && vm.Hint.Contains("sıfırl"), "Sıfırlama uyarısı beklenir");

        vm.Pin = vm.PinRepeat = "99887766";
        vm.Login();
        Expect(!loggedIn && !SharedConfig.HasPin(a.Root), "Onay olmadan PIN belirlenmemeli");

        vm.ResetConfirmed = true;
        vm.Login();
        Expect(loggedIn && SharedConfig.VerifyPin(a.Root, "99887766"), $"Onaydan sonra yeni PIN geçerli: {vm.Error}");
        var actions = AuditLog.ReadTail(a.Context.Store, 20).Select(x => x.Action).ToList();
        Expect(actions.Contains("admin.pin.reset"), "admin.pin.reset denetime yazılmalı: " + string.Join(",", actions));
    });

    public static void PriorAdminConfigFilesAlsoTriggerResetWarning() => WithAdmin(a =>
    {
        // admin.json hiç yazılmadan _config altında başka dosya var: sessiz ilk kurulum yok.
        SharedConfig.SaveDefectTypes(a.Root, SharedConfig.Defaults);
        var vm = new LoginViewModel(a.Context, a.Clock);
        Expect(vm.IsFirstRun && vm.NeedsResetConfirm, "_config dolu kökte sıfırlama uyarısı beklenir");
    });

    public static void LockoutPersistsAcrossRestartAndIsExpiredByClock() => WithAdmin(a =>
    {
        TestPins.WriteLegacy(a.Root, "123456");
        for (var round = 0; round < 4; round++)
        {
            var vm = new LoginViewModel(a.Context, a.Clock);   // her turda "yeniden açılış"
            vm.Pin = "000000";
            vm.Login();
        }
        var fifth = new LoginViewModel(a.Context, a.Clock);
        fifth.Pin = "000000";
        fifth.Login();
        var locked = new LoginViewModel(a.Context, a.Clock);
        locked.Pin = "123456";
        locked.Login();
        Expect(locked.Error.StartsWith("Çok fazla hatalı deneme."), $"5. hatadan sonra yeni açılışta da bekleme sürmeli: {locked.Error}");

        a.Clock.Advance(TimeSpan.FromSeconds(61));
        var later = new LoginViewModel(a.Context, a.Clock);
        var ok = false;
        later.LoggedIn += () => ok = true;
        later.Pin = "123456";
        later.Login();
        Expect(ok, $"Süre dolunca giriş: {later.Error}");
        var actions = AuditLog.ReadTail(a.Context.Store, 50).Select(x => x.Action).ToList();
        Expect(actions.Count(x => x == "admin.login.fail") == 5 && actions.Contains("admin.login.lockout"), "Her hatalı deneme ve kilitlenme denetlenmeli: " + string.Join(",", actions));
    });

    public static void CorruptAuditTailStartsReportedChainSegment() => WithRoot(root =>
    {
        var store = ThreeEntryAudit(root);
        var path = Path.Combine(root, AuditLog.FileName);
        File.AppendAllText(path, "{bozuk" + Environment.NewLine);
        AuditLog.Append(store, "a", "x.after", "d4");
        AuditLog.Append(store, "a", "x.after2", "d5");

        var check = AuditLog.Check(store);
        Expect(!check.Ok, "Bozuk satır ve zincir bölümü Verify tarafından bildirilmeli");
        Expect(check.Problems.Any(p => p.Contains("okunamıyor")) && check.Problems.Any(p => p.Contains("yeniden başlatılmış")), "Hem bozuk satır hem işaret satırı bildirilmeli: " + string.Join(" | ", check.Problems));
        Expect(AuditLog.ReadTail(store, 10).Any(x => x.Action == AuditLog.ChainResetAction), "İşaret satırı yazılmalı");
        // İşaretten sonraki satırlar yeni bölümde sağlam: yalnızca ilk bozulma bildirilir (bozuk satır + işaret).
        Expect(check.Problems.Count == 2, $"Yalnızca iki bulgu beklenir: {check.Problems.Count}");
    });

    public static void MaintenanceReportsRecordsChangedOutsideAudit() => WithAdmin(a =>
    {
        var folder = Seed(a.Context.Store);
        var defect = a.Context.Store.AddDefect(folder, "Dent");
        a.Context.Audit.Append("t", "defect.add", $"#{defect.Id}");
        var maintenance = new MaintenanceViewModel(a.Context, new FakeConfirm());
        maintenance.Notify += (m, k) => a.Notes.Add((m, k));
        maintenance.VerifyAudit();
        Expect(a.Notes.Last().Kind == StatusKind.Success, $"Başlangıçta sağlam: {a.Notes.Last().Message}");

        EditJson(DefectFile(folder, defect.Id), o => o["notes"] = "elle değiştirildi");
        maintenance.VerifyAudit();
        Expect(a.Notes.Last().Kind == StatusKind.Error, "Denetim dışı değişiklik hata olarak bildirilmeli");
        Expect(maintenance.AuditLines.Any(l => l.StartsWith("!!") && l.Contains("denetim dışı değiştirilmiş")), "Liste 'kayıt denetim dışı değiştirilmiş' satırını göstermeli");

        // Denetimli yeni yazma özeti günceller: yeniden sağlam.
        a.Context.Store.UpdateDefect(folder, defect.Id, d => d.Notes = "denetimli");
        a.Context.Audit.Append("t", "defect.measure", $"#{defect.Id}");
        maintenance.VerifyAudit();
        Expect(a.Notes.Last().Kind == StatusKind.Success, $"Denetimli yazmadan sonra sağlam: {a.Notes.Last().Message}");
    });

    public static void LockFileRecordsOwnerAndFutureLockMessageNamesIt() => WithRoot(root =>
    {
        var path = Path.Combine(root, "o.lock");
        using (FileLock.Acquire(path))
        {
            // Açık kilidin içeriği başka süreççe okunamaz (FileShare.None); kapanınca dosya silinir. Sahip metni kilit anında yazılır.
        }
        // Sahip bilgisi: kilit bırakılmadan önce yazılır; eski kalan kilitte okunabilir.
        File.WriteAllText(path, $"{Environment.MachineName}\\kisi pid=1 2026-01-01T00:00:00Z");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(1));
        var ex = Catch<InvalidOperationException>(() => FileLock.Acquire(path, TimeSpan.FromSeconds(1)));
        Expect(ex.Message.Contains("admin") && ex.Message.Contains("sahibi"), $"Mesaj admin'i ve sahibi anmalı: {ex.Message}");
        Expect(FileLock.IsStale(path), "İleri tarihli kilit eski sayılmalı");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(30));
        Expect(!FileLock.IsStale(path), "Küçük saat farkı eski sayılmamalı");
    });

    public static void NewLockWritesOwnerText() => WithRoot(root =>
    {
        var path = Path.Combine(root, "p.lock");
        var raw = "";
        using (FileLock.Acquire(path))
        {
            // Başka tutamaç yalnızca ReadWrite paylaşımıyla açabilir; kilit FileShare.None olduğundan okuma reddedilir.
            Expect(FileLock.DescribeOwner(path) is null, "Açık kilidin sahibi okunamaz (kilit tutamacı özel)");
        }
        Expect(!File.Exists(path), "Kilit bırakılınca silinmeli");
        // Süreç çökerse kalan dosyada sahip metni olur: aynı biçimi elle yazıp okuma yolunu sına.
        File.WriteAllText(path, $"{Environment.MachineName}\\{Environment.UserName} pid=1 2026-01-01T00:00:00Z");
        raw = FileLock.DescribeOwner(path) ?? "";
        Expect(raw.Contains(Environment.MachineName), "DescribeOwner makine adını döndürmeli");
    });

    public static void StaleLocksSkipJunctionAndRemoveRefusesIt() => WithRoot(root =>
    {
        var outside = root + "-dis";
        Directory.CreateDirectory(outside);
        try
        {
            var kok = Path.Combine(root, "kok");
            Directory.CreateDirectory(kok);
            var stale = MakeLock(Path.Combine(outside, "x.lock"), TimeSpan.FromMinutes(20));
            var link = Path.Combine(kok, "jn");
            MakeJunction(link, outside);
            try
            {
                Expect(SharedConfig.StaleLocks(kok).Count == 0, "Junction ardındaki kilit listelenmemeli");
                var viaLink = Path.Combine(link, "x.lock");
                Expect(Throws<InvalidOperationException>(() => SharedConfig.RemoveStaleLock(kok, viaLink)), "Junction üzerinden kilit silinmemeli");
                Expect(File.Exists(stale), "Kök dışındaki kilit dosyası yerinde kalmalı");
            }
            finally { Directory.Delete(link, recursive: false); }
        }
        finally { Cleanup(outside); }
    });

    public static void SegmentsRejectReservedNamesWithExtensionAndTrailingSpace()
    {
        foreach (var bad in new[] { "con", "Prn.log", "AUX.x.y", "nul ", "COM9", "lpt3.txt", "COM1 .x" })
            Expect(Throws<ArgumentException>(() => SafePath.ValidateSegment(bad, "Parça no")), $"'{bad}' reddedilmeli");
        foreach (var good in new[] { "COM10", "CONSOLE", "NULL1", "PD170", "LPT" })
            Expect(SafePath.ValidateSegment(good, "Parça no") == good, $"'{good}' kabul edilmeli");
    }

    public static void UnderRootCheckRejectsSiblingPrefix() => WithRoot(root =>
    {
        var veri = Path.Combine(root, "Veri");
        Directory.CreateDirectory(veri);
        Expect(SafePath.IsUnderRootWithoutLinks(veri, Path.Combine(veri, "P", "x.pdf")), "Kök altı kabul edilmeli");
        Expect(!SafePath.IsUnderRootWithoutLinks(veri, Path.Combine(root, "Veri2", "x.pdf")), "Kardeş önek reddedilmeli");
        Expect(!SafePath.IsUnderRootWithoutLinks(veri, veri), "Kökün kendisi 'altında' sayılmaz");
        Expect(!SafePath.IsUnderRootWithoutLinks(veri, Path.Combine(veri, "..", "x.pdf")), "Üst klasöre kaçış reddedilmeli");
    });

    public static void ImportRejectsHugePixelCountWithoutDecoding() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        // PNG: imza + IHDR (20000 x 20000 = 400 MP). Dosya çok küçük ama başlık dev görüntü bildirir.
        var png = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        png.AddRange(new byte[] { 0, 0, 0x4E, 0x20, 0, 0, 0x4E, 0x20, 8, 6, 0, 0, 0 });
        var pngPath = Path.Combine(root, "dev.png");
        File.WriteAllBytes(pngPath, png.ToArray());
        var ex = Catch<InvalidDataException>(() => PhotoImporter.Import(folder, 1, pngPath));
        Expect(ex.Message.Contains("megapiksel"), $"Mesaj piksel sınırını anmalı: {ex.Message}");

        // JPEG: SOI + APP0 + SOF0 (65535 x 65535).
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46 };
        jpeg.AddRange(new byte[] { 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x01, 0x11, 0x00 });
        var jpgPath = Path.Combine(root, "dev.jpg");
        File.WriteAllBytes(jpgPath, jpeg.ToArray());
        Catch<InvalidDataException>(() => PhotoImporter.Import(folder, 1, jpgPath));
        Expect(!Directory.Exists(Path.Combine(folder, "defects")) || !Directory.EnumerateFiles(Path.Combine(folder, "defects")).Any(), "Reddedilen dosya kopyalanmamalı");

        Expect(ImageHeader.ExceedsLimit(65_536, 65_536) && !ImageHeader.ExceedsLimit(8_000, 6_000), "Sınır 60 MP");
    });

    public static void ImportStripsGpsButKeepsOrientation() => WithRoot(root =>
    {
        var folder = Seed(new SessionStore(root));
        // Exif APP1: büyük bayt sıralı TIFF, IFD0'da yalnızca Orientation=6; ayrıca "GPS" metni içeren ek veri.
        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 0x2A, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, 6, 0, 0, 0, 0, 0, 0 };
        tiff.AddRange(Encoding.ASCII.GetBytes("GPSLatitude=41.0;GPSLongitude=29.0"));
        var payload = new List<byte>(Encoding.ASCII.GetBytes("Exif\0\0"));
        payload.AddRange(tiff);
        var length = payload.Count + 2;
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        jpeg.AddRange(payload);
        jpeg.AddRange(new byte[] { 0xFF, 0xDB, 0x00, 0x04, 0x00, 0x01, 0xFF, 0xDA, 0x00, 0x02, 0x11, 0x22, 0xFF, 0xD9 });
        var src = Path.Combine(root, "gps.jpg");
        File.WriteAllBytes(src, jpeg.ToArray());

        var name = PhotoImporter.Import(folder, 1, src);
        var bytes = File.ReadAllBytes(Path.Combine(folder, "defects", name));
        var text = Encoding.Latin1.GetString(bytes);
        Expect(!text.Contains("GPS"), "GPS verisi kalmamalı");
        // Yön etiketi (0x0112, SHORT, 1, değer 6) korunur.
        var marker = new byte[] { 0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x06 };
        Expect(bytes.AsSpan().IndexOf(marker) >= 0, "EXIF yönü (Orientation=6) korunmalı");
        // Görüntü verisi (DQT + SOS ... EOI) aynen duruyor.
        Expect(bytes.AsSpan().IndexOf(new byte[] { 0xFF, 0xDA, 0x00, 0x02, 0x11, 0x22, 0xFF, 0xD9 }) >= 0, "Görüntü verisi değişmemeli");
        Expect(bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9, "Geçerli JPEG kalmalı");
    });

    public static void EdgeBrowserNeverOpensUnknownFileTypes() => WithRoot(root =>
    {
        foreach (var name in new[] { "a.exe", "b.bat", "c.lnk", "d.txt", "e.cmd", "f.ps1", "g.js", "h" })
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, "x");
            Expect(Throws<InvalidOperationException>(() => EdgeBrowser.OpenFile(path)), $"{name} açılmamalı");
        }
        Expect(Throws<FileNotFoundException>(() => EdgeBrowser.OpenFile(Path.Combine(root, "yok.pdf"))), "Olmayan rapor FileNotFoundException vermeli (süreç başlatılmaz)");
        Expect(EdgeBrowser.IsOpenable("Rapor_v1.PDF") && EdgeBrowser.IsOpenable("x.html") && EdgeBrowser.IsOpenable("1_photo_01.jpg"), "Rapor ve fotoğraf türleri açılabilir");
    });

    public static void ProgramLayoutAcceptsNumericVersion() => WithRoot(root =>
    {
        ProgramRootWith(root, "1.0.2");
        PlaceExe(root, "1.0.2");
        Expect(ProgramLayout.Resolve(root, "Inspector", false).EndsWith("MuayeneKayit.Inspector.exe"), "1.0.2 çözülmeli");
    });

    public static void CsvQuotesCommaCells()
    {
        Expect(CsvExport.Cell("a,b") == "\"a,b\"", "Virgüllü hücre tırnaklanmalı");
        Expect(CsvExport.Cell("=1,2").StartsWith("\"'="), "Formül + virgül: önek ve tırnak");
    }

    public static void MalformedPhotoNamesAreDroppedOnLoadWithWarning() => WithRoot(root =>
    {
        var store = new SessionStore(root);
        var f = Seed(store);
        store.AddDefect(f, "Dent");
        EditJson(DefectFile(f, 1), o => o["photos"] = new JsonArray("1_photo_01.jpg", "..\\x.jpg", "\\\\h\\s\\x.jpg", "C:\\a.jpg", "1_photo_02.jpg\n", null));
        var d = store.LoadDefects(f).Single();
        Expect(d.Photos.SequenceEqual(new[] { "1_photo_01.jpg" }), "Yalnızca geçerli ad kalmalı: " + string.Join("|", d.Photos));
        Expect(store.TakeWarnings().Any(w => w.Contains("geçersiz fotoğraf adı")), "Uyarı beklenir");
    });
}
