using System.Text;
using System.Text.Json;
using MuayeneKayit.Core;

// Fonksiyonel test planı: Core düzeyi testler (docs/test/FONKSIYONEL-TEST-PLANI.md).
public static class FonkCoreTests
{
    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-fonk-core-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WithTemp(Action<string> body)
    {
        var root = TempRoot();
        try { body(root); }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    // ───────────── Durum makinesi ─────────────

    private static Defect Make(DefectState state)
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.5 };
        void A(Decision dec, DecisionSource src = DecisionSource.Engineer, string? note = null, string? target = null, IReadOnlyList<int>? ids = null) =>
            DefectStateMachine.Apply(d, dec, src, "t", note: note, targetOperation: target, newDefectIds: ids);
        switch (state)
        {
            case DefectState.Open: break;
            case DefectState.Repairing: A(Decision.Repair); break;
            case DefectState.AwaitingReinspect: A(Decision.Rework, target: "OP50"); break;
            case DefectState.WaitingVendor: A(Decision.ReturnToVendor); break;
            case DefectState.MrbPending: A(Decision.SubmitMrb); break;
            case DefectState.Closed: A(Decision.Accept); break;
            case DefectState.Voided: A(Decision.Void, DecisionSource.Operator, "yanlış"); break;
            case DefectState.Superseded:
                A(Decision.Rework, target: "OP50");
                A(Decision.AfterReworkNewDefect, ids: new[] { 2 });
                break;
        }
        return d;
    }

    private static Defect MakeClosedScrap() { var d = new Defect { Id = 1, DefectType = "Dent" }; DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "t"); return d; }

    private static Defect MakeClosedMrbRejected()
    {
        var d = new Defect { Id = 1, DefectType = "Dent" };
        DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "t");
        DefectStateMachine.Apply(d, Decision.MrbRejected, DecisionSource.Mrb, "m");
        return d;
    }

    private static DecisionSource[] SourcesFor(Decision decision) => decision switch
    {
        Decision.Void => new[] { DecisionSource.Operator },
        Decision.MrbAccepted or Decision.MrbRejected => new[] { DecisionSource.Mrb },
        Decision.Reinspect or Decision.AfterReworkConforms or Decision.AfterReworkSameDefect or Decision.AfterReworkNewDefect
            => new[] { DecisionSource.Engineer, DecisionSource.Ctp },
        _ => new[] { DecisionSource.Engineer },
    };

    private static readonly Dictionary<DefectState, Decision[]> Table = new()
    {
        [DefectState.Open] = new[] { Decision.Accept, Decision.Rework, Decision.Reinspect, Decision.Repair, Decision.Scrap, Decision.SubmitMrb, Decision.ReturnToVendor, Decision.Void },
        [DefectState.Repairing] = new[] { Decision.Accept, Decision.Scrap, Decision.Reinspect, Decision.SubmitMrb },
        [DefectState.AwaitingReinspect] = new[] { Decision.AfterReworkConforms, Decision.AfterReworkSameDefect, Decision.AfterReworkNewDefect, Decision.Repair, Decision.SubmitMrb, Decision.Scrap },
        [DefectState.WaitingVendor] = new[] { Decision.Reinspect, Decision.Scrap },
        [DefectState.MrbPending] = new[] { Decision.MrbAccepted, Decision.MrbRejected },
        [DefectState.Closed] = new[] { Decision.Reopen },
        [DefectState.Voided] = Array.Empty<Decision>(),
        [DefectState.Superseded] = Array.Empty<Decision>(),
    };

    private static IEnumerable<Decision> AllDecisions() => Enum.GetValues<Decision>().Where(d => d != Decision.Undo);

    // F-SM-001
    public static void Fonk_SM001_FullMatrix()
    {
        var errors = new List<string>();
        var cases = new List<(string Name, Defect D, DefectState State, Func<Decision, bool> Allowed)>();
        foreach (var s in Table.Keys)
            cases.Add((s.ToString(), Make(s), s, dec => Table[s].Contains(dec)));
        cases.Add(("Closed(Scrap)", MakeClosedScrap(), DefectState.Closed, _ => false));
        cases.Add(("Closed(MrbRejected)", MakeClosedMrbRejected(), DefectState.Closed, _ => false));

        foreach (var (name, d, state, allowed) in cases)
        {
            Expect(DefectStateMachine.GetState(d) == state, $"{name}: kurulum durumu {DefectStateMachine.GetState(d)}");
            foreach (var dec in AllDecisions())
            {
                var src = SourcesFor(dec)[0];
                var ok = DefectStateMachine.Check(d, dec, src).Ok;
                if (ok != allowed(dec))
                    errors.Add($"{name} × {dec}: beklenen {allowed(dec)}, gelen {ok}");
            }
        }
        Expect(errors.Count == 0, string.Join("; ", errors));
    }

    // F-SM-002
    public static void Fonk_SM002_SourceMatrix()
    {
        var errors = new List<string>();
        foreach (var (state, decisions) in Table)
        {
            foreach (var dec in decisions)
            {
                foreach (var src in Enum.GetValues<DecisionSource>())
                {
                    var ok = DefectStateMachine.Check(Make(state), dec, src).Ok;
                    var want = SourcesFor(dec).Contains(src);
                    if (ok != want)
                        errors.Add($"{state} × {dec} × {src}: beklenen {want}, gelen {ok}");
                }
            }
        }
        Expect(errors.Count == 0, string.Join("; ", errors));
    }

    // F-SM-005
    public static void Fonk_SM005_ReinspectHasNoSnapshot()
    {
        var d = Make(DefectState.Open);
        DefectStateMachine.Apply(d, Decision.Reinspect, DecisionSource.Engineer, "t", targetOperation: "CTP");
        Expect(DefectStateMachine.GetState(d) == DefectState.AwaitingReinspect && d.Dispositions[0].Snapshot is null, "Reinspect: AwaitingReinspect, Snapshot yok");
    }

    // F-SM-004
    public static void Fonk_SM004_ReworkTargetRequired()
    {
        foreach (var t in new[] { null, "", "  " })
        {
            var d = new Defect { Id = 1, DefectType = "Dent" };
            var ex = Assert.Throws<InvalidOperationException>(() => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "t", targetOperation: t));
            Expect(ex.Message == "Rework için hedef operasyonu girin (örn. OP40).", ex.Message);
        }
    }

    // F-SM-006
    public static void Fonk_SM006_RepairingTransitions()
    {
        var expected = new Dictionary<Decision, DefectState>
        {
            [Decision.Accept] = DefectState.Closed, [Decision.Scrap] = DefectState.Closed,
            [Decision.Reinspect] = DefectState.AwaitingReinspect, [Decision.SubmitMrb] = DefectState.MrbPending,
        };
        foreach (var (dec, state) in expected)
        {
            var d = Make(DefectState.Repairing);
            DefectStateMachine.Apply(d, dec, DecisionSource.Engineer, "t", targetOperation: "CTP");
            Expect(DefectStateMachine.GetState(d) == state, $"{dec} → {DefectStateMachine.GetState(d)}");
        }
        foreach (var dec in new[] { Decision.Rework, Decision.Void })
            Expect(!DefectStateMachine.Check(Make(DefectState.Repairing), dec, SourcesFor(dec)[0]).Ok, $"{dec} reddedilmeli");
    }

    // F-SM-010
    public static void Fonk_SM010_AfterReworkTransitions()
    {
        void Try(Decision dec, DefectState expect, IReadOnlyList<int>? ids = null)
        {
            var d = Make(DefectState.AwaitingReinspect);
            DefectStateMachine.Apply(d, dec, SourcesFor(dec)[0], "t", newDefectIds: ids);
            Expect(DefectStateMachine.GetState(d) == expect, $"{dec} → {DefectStateMachine.GetState(d)} (beklenen {expect})");
        }
        Try(Decision.AfterReworkConforms, DefectState.Closed);
        Try(Decision.AfterReworkSameDefect, DefectState.Open);
        Try(Decision.AfterReworkNewDefect, DefectState.Superseded, new[] { 2 });
        Try(Decision.Repair, DefectState.Repairing);
        Try(Decision.SubmitMrb, DefectState.MrbPending);
        Try(Decision.Scrap, DefectState.Closed);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DefectStateMachine.Apply(Make(DefectState.AwaitingReinspect), Decision.AfterReworkNewDefect, DecisionSource.Engineer, "t"));
        Expect(ex.Message.Contains("Yeni hata kararı için") && ex.Message.Contains("numarası gerekli"), ex.Message);
    }

    // F-SM-013
    public static void Fonk_SM013_ScopeAndNewIdsOnlyWhereAllowed()
    {
        var ex1 = Assert.Throws<InvalidOperationException>(() =>
            DefectStateMachine.Apply(Make(DefectState.Open), Decision.Accept, DecisionSource.Engineer, "t", reworkScope: ReworkScope.HighMetalOnly));
        Expect(ex1.Message == "Rework kapsamı yalnızca rework kararında verilir.", ex1.Message);
        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            DefectStateMachine.Apply(Make(DefectState.Open), Decision.Accept, DecisionSource.Engineer, "t", newDefectIds: new[] { 2 }));
        Expect(ex2.Message.StartsWith("Yeni kusur numaraları yalnızca rework sonrası"), ex2.Message);
    }

    // F-SM-014
    public static void Fonk_SM014_VoidedAndSupersededRejectEverything()
    {
        foreach (var state in new[] { DefectState.Voided, DefectState.Superseded })
        {
            var d = Make(state);
            var count = d.Dispositions.Count;
            foreach (var dec in AllDecisions())
                foreach (var src in Enum.GetValues<DecisionSource>())
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => DefectStateMachine.Apply(d, dec, src, "t", note: "n", targetOperation: "OP", newDefectIds: new[] { 2 }));
                    _ = ex;
                }
            Expect(d.Dispositions.Count == count && DefectStateMachine.GetState(d) == state, $"{state}: durum değişmemeli");
        }
    }

    // F-SM-019
    public static void Fonk_SM019_UndoWithChildrenOutsideStoreRejected()
    {
        var d = Make(DefectState.Superseded);
        var ex = Assert.Throws<InvalidOperationException>(() => DefectStateMachine.Undo(d, "t", "neden"));
        Expect(ex.Message.StartsWith("Bu karar yeni kusurlara bağlı"), ex.Message);
    }

    // F-SM-018
    public static void Fonk_SM018_VoidNotUndoable()
    {
        Expect(DefectStateMachine.LastUndoable(Make(DefectState.Voided)) is null, "Void geri alınamaz");
        var d = MakeClosedMrbRejected();
        Expect(DefectStateMachine.LastUndoable(d)?.Decision == Decision.MrbRejected, "MrbRejected geri alınabilir");
    }

    // ───────────── Parça listesi ─────────────

    // F-PART-003
    public static void Fonk_PART003_PartListAcceptReject()
    {
        foreach (var ok in new[] { "1", "9999", "1-9999", "1,,2", "1 - 5", "1–5", "1;2;3", "2,1,2", "1-2" })
        {
            var ex = AnyEx(() => PartList.Parse(ok));
            Expect(ex is null, $"'{ok}' kabul edilmeli: {ex}");
        }
        foreach (var bad in new[] { "0", "10000", "-5", "1--5", "a" })
        {
            var ex = AnyEx(() => PartList.Parse(bad));
            Expect(ex is not null && (ex.StartsWith("ArgumentException") || ex.StartsWith("FormatException") || ex.StartsWith("InvalidOperationException")), $"'{bad}' reddedilmeli (istisna: {ex ?? "yok"})");
        }
    }

    private static string? AnyEx(Action a)
    {
        try { a(); return null; } catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }

    // ───────────── Yapılandırma ─────────────

    // F-CFG-001
    public static void Fonk_CFG001_MissingSettingsFileFallsBackToDemoPath() => WithTemp(tmp =>
    {
        var path = InstallSettings.LoadRootPath(tmp);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MuayeneKayitDemo");
        Expect(path == expected, $"Yol: {path}");
    });

    // F-CFG-002
    public static void Fonk_CFG002_BadSettingsFileFallsBack() => WithTemp(tmp =>
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MuayeneKayitDemo");
        foreach (var content in new[] { "çöp", "{}", "{\"rootPath\":\"\"}", "{\"rootPath\":5}", "{\"rootPath\":null}", "[]" })
        {
            File.WriteAllText(Path.Combine(tmp, InstallSettings.FileName), content);
            string? path = null;
            var ex = AnyEx(() => path = InstallSettings.LoadRootPath(tmp));
            Expect(ex is null && path == expected, $"'{content}': istisna={ex}, yol={path}");
        }
    });

    // F-CFG-003
    public static void Fonk_CFG003_EnvExpansionAndParentLevels() => WithTemp(tmp =>
    {
        File.WriteAllText(Path.Combine(tmp, InstallSettings.FileName), "{\"rootPath\":\"%TEMP%\\\\x\"}");
        Expect(InstallSettings.LoadRootPath(tmp) == Path.Combine(Path.GetTempPath().TrimEnd('\\'), "x"), InstallSettings.LoadRootPath(tmp));
        var d3 = Path.Combine(tmp, "a", "b", "c");
        var d4 = Path.Combine(d3, "d");
        Directory.CreateDirectory(d4);
        Expect(InstallSettings.Find(d3) is not null, "3 seviyede bulunmalı");
        Expect(InstallSettings.Find(d4) is null || !InstallSettings.Find(d4)!.StartsWith(tmp), "4 seviyede bulunmamalı");
    });

    // F-CFG-005 (B-21)
    public static void Fonk_CFG005_OneInvalidFieldDropsOnlyThatField() => WithTemp(root =>
    {
        Directory.CreateDirectory(SharedConfig.Folder(root));
        var list = new List<CustomFieldDef>
        {
            new() { Key = "fa1", Name = "Yükseklik", Abbrev = "H", Kind = CustomFieldKind.Length },
            new() { Key = "fa2", Name = "Kötü", Abbrev = "D", Kind = CustomFieldKind.Length }, // yerleşik kısaltma
            new() { Key = "fa3", Name = "Sapma", Abbrev = "S", Kind = CustomFieldKind.Number },
        };
        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "measure_fields.json"), JsonSerializer.Serialize(list, SessionStore.JsonOptions));
        var loaded = SharedConfig.LoadCustomFields(root);
        Expect(loaded.Count == 2, $"B-21 doğrulandı: tek geçersiz alan yüzünden {loaded.Count}/2 geçerli alan yüklendi (tüm liste düştü)");
    });

    // F-CFG-007
    public static void Fonk_CFG007_BadIdentityFileMeansManual() => WithTemp(root =>
    {
        Directory.CreateDirectory(SharedConfig.Folder(root));
        File.WriteAllText(Path.Combine(SharedConfig.Folder(root), "inspector_identity.json"), "çöp");
        Expect(!SharedConfig.LoadUseWindowsUser(root), "false olmalı");
    });

    // F-CFG-004
    public static void Fonk_CFG004_BadTypesFileFallsBackToNine() => WithTemp(root =>
    {
        Directory.CreateDirectory(SharedConfig.Folder(root));
        var path = Path.Combine(SharedConfig.Folder(root), "defect_types.json");
        foreach (var content in new[] { "çöp", "[]", "[null]" })
        {
            File.WriteAllText(path, content);
            Expect(SharedConfig.LoadDefectTypes(root).Count == 9, $"'{content}': varsayılan 9 tip beklenir");
        }

        // Tek geçersiz / yinelenen kayıt tüm listeyi düşürmez: yalnızca o kayıt atlanır (B-21 ile aynı kural).
        File.WriteAllText(path, "[{\"name\":\"A\"},{\"name\":\"a\"},{\"name\":\"Kötü#\"},{\"name\":\"B\"}]");
        var kept = SharedConfig.LoadDefectTypes(root);
        Expect(kept.Select(t => t.Name).SequenceEqual(new[] { "A", "B" }), $"geçerli tipler korunmalı: {string.Join(",", kept.Select(t => t.Name))}");
    });

    // F-ADM-FIELD-002..005
    public static void Fonk_FIELD002_NameRules()
    {
        CustomFieldDef F(string name, string abbrev = "H", string key = "fx") => new() { Key = key, Name = name, Abbrev = abbrev };
        foreach (var bad in new[] { "", new string('A', 31), "Yük#" })
            Expect(SharedConfig.ValidateCustomFields(new[] { F(bad) }).Any(e => e.StartsWith("Geçersiz alan adı")), $"'{bad}' reddedilmeli");
        Expect(SharedConfig.ValidateCustomFields(new[] { F(new string('A', 30)) }).Count == 0, "30 karakter kabul");
    }

    public static void Fonk_FIELD003_AbbrevRules()
    {
        CustomFieldDef F(string abbrev) => new() { Key = "fx", Name = "Alan", Abbrev = abbrev };
        foreach (var bad in new[] { "", "ABCDEFG", "H-1" })
            Expect(SharedConfig.ValidateCustomFields(new[] { F(bad) }).Any(e => e.Contains("kısaltma 1-6 harf ya da rakam olmalı")), $"'{bad}'");
        foreach (var reserved in new[] { "D", "d", "w", "L", "Ø", "Ç", "a" })
            Expect(SharedConfig.ValidateCustomFields(new[] { F(reserved) }).Any(e => e.Contains("yerleşik bir alana ait")), $"'{reserved}' yerleşik");
        foreach (var ok in new[] { "H°", "Ğ" })
            Expect(SharedConfig.ValidateCustomFields(new[] { F(ok) }).Count == 0, $"'{ok}' kabul edilmeli");
    }

    public static void Fonk_FIELD004_DuplicateNameAndAbbrev()
    {
        CustomFieldDef F(string name, string abbrev, string key) => new() { Key = key, Name = name, Abbrev = abbrev };
        Expect(SharedConfig.ValidateCustomFields(new[] { F("Yükseklik", "H", "f1"), F("yükseklik", "X", "f2") }).Any(e => e.Contains("Aynı alan adı iki kez var")), "ad");
        Expect(SharedConfig.ValidateCustomFields(new[] { F("Yükseklik", "H", "f1"), F("Kalınlık", "h", "f2") }).Any(e => e.Contains("Aynı kısaltma iki kez var")), "kısaltma");
    }

    public static void Fonk_FIELD005_MaxThirtyFields()
    {
        CustomFieldDef F(int i) => new() { Key = "f" + i, Name = "Alan " + i, Abbrev = "X" + i };
        Expect(SharedConfig.ValidateCustomFields(Enumerable.Range(1, 30).Select(F).ToList()).Count == 0, "30 kabul");
        Expect(SharedConfig.ValidateCustomFields(Enumerable.Range(1, 31).Select(F).ToList()).Any(e => e.Contains("En fazla 30 ek ölçüm alanı")), "31 red");
    }

    // F-ADM-TYPE-010
    public static void Fonk_TYPE010_MaxHundredTypes() => WithTemp(root =>
    {
        DefectTypeTemplate T(int i) => new() { Name = "T" + i, Fields = { MeasureField.Depth } };
        SharedConfig.SaveDefectTypes(root, Enumerable.Range(1, 100).Select(T).ToList());
        var ex = Assert.Throws<ArgumentException>(() => SharedConfig.SaveDefectTypes(root, Enumerable.Range(1, 101).Select(T).ToList()));
        Expect(ex.Message.Contains("En fazla 100 kusur tipi tanımlanabilir."), ex.Message);
    });

    // F-ADM-SET-006
    public static void Fonk_SET006_TooLongRootPathMessage() => WithTemp(tmp =>
    {
        var ex = Assert.Throws<ArgumentException>(() => InstallSettings.SaveRootPath(tmp, new string('x', 261)));
        Expect(ex.Message == "Ortak alan yolu geçersiz.", ex.Message);
    });

    // F-LNC-002..004, 006, 008
    public static void Fonk_LNC002_MissingVersionFile() => WithTemp(tmp =>
    {
        var ex = Assert.Throws<FileNotFoundException>(() => ProgramLayout.Resolve(tmp, "Inspector", false));
        Expect(ex.Message.StartsWith("Sürüm dosyası bulunamadı:"), ex.Message);
    });

    public static void Fonk_LNC003_InvalidVersionTexts() => WithTemp(tmp =>
    {
        foreach (var v in new[] { "", "..", "v1/../x", "a<b" })
        {
            File.WriteAllText(Path.Combine(tmp, "surum.txt"), v);
            var ex = Assert.Throws<FileNotFoundException>(() => ProgramLayout.Resolve(tmp, "Inspector", false));
            Expect(ex.Message.StartsWith("surum.txt geçersiz"), $"'{v}': {ex.Message}");
        }
    });

    public static void Fonk_LNC004_VersionFolderMissing() => WithTemp(tmp =>
    {
        // Sürüm metni sayısal biçimde olmalı (güvenlik düzeltmesi): "9.0.0".
        File.WriteAllText(Path.Combine(tmp, "surum.txt"), "9.0.0");
        var ex = Assert.Throws<FileNotFoundException>(() => ProgramLayout.Resolve(tmp, "Inspector", false));
        Expect(ex.Message.StartsWith("9.0.0 sürümünde Inspector bulunamadı:"), ex.Message);
    });

    public static void Fonk_LNC006_VersionFileFirstLineAndBom() => WithTemp(tmp =>
    {
        var exe = Path.Combine(tmp, "1.2.3", "Inspector-x64", "MuayeneKayit.Inspector.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllText(exe, "x");
        File.WriteAllText(Path.Combine(tmp, "surum.txt"), "1.2.3  \r\n2. satır", new UTF8Encoding(true));
        Expect(ProgramLayout.Resolve(tmp, "Inspector", false) == exe, "ilk satır kırpılmalı, BOM sorun çıkarmamalı");
    });

    public static void Fonk_LNC008_SaveRootPathNextToVersionFile() => WithTemp(tmp =>
    {
        File.WriteAllText(Path.Combine(tmp, "surum.txt"), "v1");
        var deep = Path.Combine(tmp, "v1", "Inspector-x64");
        Directory.CreateDirectory(deep);
        InstallSettings.SaveRootPath(deep, @"C:\veri");
        Expect(File.Exists(Path.Combine(tmp, InstallSettings.FileName)), "ayar dosyası program köküne yazılmalı");
    });

    // F-FILELOCK (F-CONC-008 UNIT kısmı: 5 sn tutulan kilit beklenir; B-07 gözlemi)
    public static void Fonk_CONC008_LockHeldFiveSecondsThenAcquired() => WithTemp(root =>
    {
        var path = Path.Combine(root, "session.lock");
        var holder = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var t = Task.Run(() => { Thread.Sleep(2000); holder.Dispose(); File.Delete(path); });
        var started = DateTime.UtcNow;
        using (FileLock.Acquire(path)) { }
        t.Wait();
        Expect(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(1500), "kilit bırakılana kadar beklenmeli");
    });
}
