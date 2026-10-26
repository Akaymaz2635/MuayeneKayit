using System.Globalization;
using System.Text.Json;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Ölçü birimi (mm / inch) ve Ayarlar testleri. Program.cs bu sınıfı "using static" ile kullanır.
public static partial class ViewModelTests
{
    public static void UnitsFormatRules()
    {
        Check(Units.Format(0.123, LengthUnit.Inch) == ".123", $"0.123 → {Units.Format(0.123, LengthUnit.Inch)}");
        Check(Units.Format(0.5, LengthUnit.Inch) == ".5", "0.5 → .5");
        Check(Units.Format(-0.25, LengthUnit.Inch) == "-.25", "-0.25 → -.25");
        Check(Units.Format(0, LengthUnit.Inch) == "0", "0 → 0");
        Check(Units.Format(1.25, LengthUnit.Inch) == "1.25", "1.25 → 1.25");
        Check(Units.Format(0.12345, LengthUnit.Inch) == ".1235", $"0.12345 → {Units.Format(0.12345, LengthUnit.Inch)}");
        Check(Units.Format(0.123, LengthUnit.Mm) == "0.12", "mm 0.123 → 0.12");
        Check(Units.Format(12, LengthUnit.Mm) == "12", "mm 12 → 12");
        Check(Units.Suffix(LengthUnit.Inch) == "in" && Units.Suffix(LengthUnit.Mm) == "mm", "Ek");
        Check(Units.Header("Derinlik", LengthUnit.Inch) == "Derinlik (in)", "Başlık in");
        Check(Units.Header("Derinlik", LengthUnit.Mm) == "Derinlik (mm)", "Başlık mm");
    }

    public static void UnitSentenceInchAndMm()
    {
        var inch = new Defect { Id = 1, DefectType = "Dent", Depth = 0.123, Unit = LengthUnit.Inch };
        var text = DefectSentence.Describe(inch);
        Check(text.Contains("There is a Dent on the part: D .123 in."), text);

        var mm = new Defect { Id = 2, DefectType = "Dent", Depth = 12 };
        Check(DefectSentence.Describe(mm).Contains("There is a Dent on the part: D 12 mm."), "mm cümlesi değişmemeli");

        var angle = new Defect { Id = 3, DefectType = "Dent", AngleDeg = 0.5, Unit = LengthUnit.Inch };
        Check(DefectSentence.Describe(angle).Contains("A 0.5°"), "Açı birimden etkilenmemeli");
    }

    public static void UnitJsonOldAndRoundTrip()
    {
        var old = JsonSerializer.Deserialize<Defect>("{\"id\":1,\"defectType\":\"Dent\",\"depth\":0.1}", SessionStore.JsonOptions)!;
        Check(old.Unit == LengthUnit.Mm, "Birim alanı olmayan eski kusur mm okunmalı");

        var json = JsonSerializer.Serialize(new Defect { Id = 2, DefectType = "Dent", Unit = LengthUnit.Inch }, SessionStore.JsonOptions);
        Check(json.Contains("\"unit\": \"Inch\""), json);
        Check(JsonSerializer.Deserialize<Defect>(json, SessionStore.JsonOptions)!.Unit == LengthUnit.Inch, "Inch geri okunmalı");
    }

    public static void UnitCompareInch()
    {
        var before = new MeasurementSnapshot(0.123, null, null, null, 10, null);
        var after = new Defect { Depth = 0.1, AngleDeg = 12, Unit = LengthUnit.Inch };
        var lines = MeasurementCompare.Lines(before, after);
        Check(lines.Any(l => l == "Derinlik: .123 → .1 in (azalış)"), string.Join(" | ", lines));
        Check(lines.Any(l => l == "Açı: 10 → 12 ° (artış)"), string.Join(" | ", lines));

        // Açık birim verilirse kusurun kendi birimini geçersiz kılar.
        var mm = MeasurementCompare.Lines(before, after, LengthUnit.Mm);
        Check(mm.Any(l => l.Contains("Derinlik: 0.12 → 0.1 mm")), string.Join(" | ", mm));
    }

    public static void VmUnitFollowsSettingsPerDefect() => With(c =>
    {
        c.Settings.Unit = LengthUnit.Inch;
        c.Open();
        Check(c.Vm.DepthHeader == "Derinlik (in)", "Kusur yokken başlık ayardaki birimde olmalı");

        c.Vm.AddDefect("Dent");
        Check(c.Store.LoadDefects(c.Folder).Single().Unit == LengthUnit.Inch, "Yeni kusur inch olmalı");
        Check(c.Vm.DepthHeader == "Derinlik (in)" && c.Vm.WidthHeader == "Genişlik (in)"
              && c.Vm.LengthHeader == "Uzunluk (in)" && c.Vm.RadiusHeader == "Çap (in)", "Başlıklar inch olmalı");

        c.Vm.DepthText = "0.123";
        c.Vm.SaveMeasurements();
        Check(c.Vm.DepthText == ".123", $"Kutu inch biçiminde olmalı: '{c.Vm.DepthText}'");
        Check(c.Vm.Sentence.Contains("D .123 in"), c.Vm.Sentence);

        // Ayar mm'ye dönünce mevcut kusur inch kalır, yeni kusur mm olur.
        c.Settings.Unit = LengthUnit.Mm;
        c.Vm.Refresh(1);
        Check(c.Vm.DepthHeader == "Derinlik (in)" && c.Vm.DepthText == ".123", "Mevcut kusur inch kalmalı");

        c.Vm.AddDefect("Dent");
        Check(c.Store.LoadDefects(c.Folder).Single(d => d.Id == 2).Unit == LengthUnit.Mm, "Yeni kusur mm olmalı");
        Check(c.Vm.DepthHeader == "Derinlik (mm)", "Yeni kusurda başlık mm olmalı");

        c.Vm.Select(1);
        Check(c.Vm.DepthHeader == "Derinlik (in)", "Eski kusuru seçince başlık inch'e dönmeli");
    });

    public static void VmUnitHeadersFollowSettingsWhenNoDefect() => With(c =>
    {
        c.Open();
        Check(c.Vm.DepthHeader == "Derinlik (mm)", "Varsayılan mm");
        c.Settings.Unit = LengthUnit.Inch;
        c.Vm.RefreshUnitHeaders();
        Check(c.Vm.DepthHeader == "Derinlik (in)", "Ayar değişince boştaki başlık güncellenmeli");
    });

    public static void ParseAcceptsLeadingDotAndComma()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Check(DefectPresentation.ParseOptional(".123", "Derinlik") == 0.123, ".123 ayrıştırılmalı");
            Check(DefectPresentation.ParseOptional(",123", "Derinlik") == 0.123, ",123 (tr-TR) ayrıştırılmalı");
            Check(DefectPresentation.ParseOptional("-.5", "Derinlik") == -0.5, "-.5 ayrıştırılmalı");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    public static void ChildDefectInheritsParentUnit()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(root);
            var (folder, _) = store.OpenOrCreate(new Session { PartNumber = "P", OperationNumber = "O", SerialNumber = "S" });
            var parent = store.AddDefect(folder, "Undercut", null, LengthUnit.Inch);
            Check(parent.Unit == LengthUnit.Inch, "Üst kusur inch olmalı");
            store.UpdateDefect(folder, parent.Id, d =>
                DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
            var child = store.ContinueAsChild(folder, parent.Id, "Dent", DecisionSource.Engineer, "M. Kaya", null, null);
            Check(child.Unit == LengthUnit.Inch, "Alt kusur ana kusurun birimini almalı");
            Check(store.LoadDefects(folder).Single(d => d.Id == child.Id).Unit == LengthUnit.Inch, "Birim diske yazılmalı");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    public static void UserSettingsViewModelWritesAndRaises()
    {
        var settings = new FakeUserSettings { Unit = LengthUnit.Inch, Theme = AppTheme.Dark };
        var vm = new UserSettingsViewModel(settings);
        Check(vm.UnitIndex == 1 && vm.ThemeIndex == 1, "Açılışta ayardan okunmalı");

        var themes = new List<AppTheme>();
        var unitEvents = 0;
        vm.ThemeChanged += themes.Add;
        vm.UnitChanged += () => unitEvents++;

        vm.ThemeIndex = 0;
        Check(settings.Theme == AppTheme.Light && themes.SequenceEqual(new[] { AppTheme.Light }), "Tema Açık'a yazılmalı ve olay gelmeli");
        vm.ThemeIndex = 1;
        Check(settings.Theme == AppTheme.Dark && themes.Last() == AppTheme.Dark, "Tema Koyu'ya yazılmalı");

        vm.UnitIndex = 0;
        Check(settings.Unit == LengthUnit.Mm && unitEvents == 1, "Birim mm'ye yazılmalı");
        vm.UnitIndex = -1;
        Check(settings.Unit == LengthUnit.Mm && unitEvents == 1, "Seçim kalkınca ayar değişmemeli");
    }

    public static void ReportShowsInchValues()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.123, Width = 1.5, Unit = LengthUnit.Inch };
        DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME");
        d.Dispositions[0].Snapshot = MeasurementEditor.Snapshot(d);
        var html = ReportBuilder.Build(new Session { PartNumber = "P", OperationNumber = "O", SerialNumber = "S", Inspector = "A" }, new[] { d }, 1);
        Check(html.Contains(".123 in"), "Rapor inch değerini göstermeli");
        Check(html.Contains("1.5 in"), "Rapor genişliği inch göstermeli");
        Check(html.Contains("D .123"), "Karar anı ölçümü inch biçiminde olmalı");
        Check(!html.Contains("0.123"), "Baştaki sıfır olmamalı");
    }
}