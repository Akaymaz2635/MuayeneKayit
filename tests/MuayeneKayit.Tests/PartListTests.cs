using System.Text.Json;
using MuayeneKayit.Core;

// Geri bildirim 3: açıklama cümlesi (DefectSentence.Statement) ve lot parça numaraları (PartList, Defect.AffectedParts).
public static class PartListTests
{
    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static string Desc(string text) => PartList.Describe(PartList.Parse(text));

    public static void StatementArticleAndMeasurements()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.5, Width = 2.4, Length = 6, Radius = 3, AngleDeg = 15, Color = "gray" };
        Expect(DefectSentence.Statement(d) == "There is a Dent on the part: D 0.5 mm, W 2.4 mm, L 6 mm, Ø 3 mm, A 15°, color gray.", DefectSentence.Statement(d));

        // Sesli harf → "an"; tip adı olduğu gibi (küçük harfe çevrilmez).
        Expect(DefectSentence.Statement(new Defect { DefectType = "undercut" }) == "There is an undercut on the part.", "an undercut");
        Expect(DefectSentence.Statement(new Defect { DefectType = "Undercut" }) == "There is an Undercut on the part.", "an Undercut");
        Expect(DefectSentence.Statement(new Defect { DefectType = "Crack" }) == "There is a Crack on the part.", "Ölçüsüz: iki nokta ve liste yok");

        // High metal.
        Expect(DefectSentence.Statement(new Defect { DefectType = "Dent", HighMetal = true, Depth = 12 }) == "There is a Dent with high metal on the part: D 12 mm.", "high metal");

        // inch: baştaki sıfır yok, birim in.
        var inch = new Defect { DefectType = "Dent", Depth = 0.012, Unit = LengthUnit.Inch };
        Expect(DefectSentence.Statement(inch) == "There is a Dent on the part: D .012 in.", DefectSentence.Statement(inch));

        // Yalnızca renk.
        Expect(DefectSentence.Statement(new Defect { DefectType = "Stain", Color = "blue" }) == "There is a Stain on the part: color blue.", "renk");

        // Ekranda önek ve ikinci satır; ilk satır aynı cümle.
        var full = DefectSentence.Describe(new Defect { Id = 7, DefectType = "Dent", Depth = 1 });
        Expect(full.StartsWith("Defect 7: There is a Dent on the part: D 1 mm.\nDisposition: Pending."), full);
    }

    public static void StatementPartScopeAndOldRecord()
    {
        Expect(DefectSentence.Statement(new Defect { DefectType = "Dent", AffectedParts = "3" }) == "There is a Dent on part 3.", "tek parça");
        Expect(DefectSentence.Statement(new Defect { DefectType = "Dent", AffectedParts = "1-5, 8", Depth = 2 }) == "There is a Dent on parts 1–5 and 8: D 2 mm.", "aralık + tek");
        Expect(DefectSentence.Statement(new Defect { DefectType = "Dent", AffectedParts = "bozuk" }) == "There is a Dent on the part.", "Bozuk kayıtlı değer cümleyi bozmamalı");

        // Eski JSON: alan yok → null, cümle "on the part".
        var old = JsonSerializer.Deserialize<Defect>("{\"id\":1,\"defectType\":\"Dent\",\"depth\":0.1}", SessionStore.JsonOptions)!;
        Expect(old.AffectedParts is null && DefectSentence.Statement(old) == "There is a Dent on the part: D 0.1 mm.", DefectSentence.Statement(old));
        var round = JsonSerializer.Deserialize<Defect>(JsonSerializer.Serialize(new Defect { Id = 2, DefectType = "Dent", AffectedParts = "1-5, 8" }, SessionStore.JsonOptions), SessionStore.JsonOptions)!;
        Expect(round.AffectedParts == "1-5, 8", "AffectedParts yazılıp okunmalı");
    }

    public static void PartListParseNormalizeDescribe()
    {
        Expect(string.Join(",", PartList.Parse("1-5, 8")) == "1,2,3,4,5,8", "1-5, 8");
        Expect(string.Join(",", PartList.Parse(" 8 ; 3  3,1–2 ")) == "1,2,3,8", "Ayırıcılar, tekrar, en dash, sıralama");
        Expect(string.Join(",", PartList.Parse("2 - 4")) == "2,3,4", "Tire çevresinde boşluk");
        Expect(PartList.Parse("").Count == 0 && PartList.Parse(null).Count == 0 && PartList.Parse("  ").Count == 0, "Boş girdi boş liste");

        Expect(PartList.Normalize("1,2,3,4,5 8") == "1-5, 8", PartList.Normalize("1,2,3,4,5 8"));
        Expect(PartList.Normalize("8, 1-5") == "1-5, 8", "Normalize sıralar");
        Expect(PartList.Normalize("4,5") == "4, 5" && PartList.Normalize("7") == "7" && PartList.Normalize("") == "", "Normalize kısa durumlar");

        Expect(PartList.Describe(Array.Empty<int>()) == "on the part", "boş");
        Expect(Desc("3") == "on part 3", Desc("3"));
        Expect(Desc("1, 2, 5") == "on parts 1, 2 and 5", Desc("1, 2, 5"));
        Expect(Desc("1, 2") == "on parts 1 and 2", Desc("1, 2"));
        Expect(Desc("1-5, 8") == "on parts 1–5 and 8", Desc("1-5, 8"));
        Expect(Desc("1-3, 7, 9, 11") == "on parts 1–3, 7, 9 and 11", Desc("1-3, 7, 9, 11"));
        Expect(Desc("4-6") == "on parts 4–6", Desc("4-6"));
    }

    public static void PartListRejectsInvalid()
    {
        foreach (var bad in new[] { "abc", "0", "5-1", "10000", "1-", "-3", "1-2-3", "1.5", "-1", "1,x" })
        {
            var threw = false;
            try { PartList.Parse(bad); }
            catch (ArgumentException ex) { threw = ex.Message.StartsWith("Etkilenen parçalar"); }
            Expect(threw, $"Geçersiz girdi reddedilmeli: '{bad}'");
        }
        Expect(PartList.Parse("9999").Count == 1, "Üst sınır dahil");
        Expect(PartList.TryParse("abc").Count == 0, "TryParse hata fırlatmaz");
    }

    public static void ChildInheritsAffectedParts()
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var (folder, _) = store.OpenOrCreate(new Session { PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-P", Inspector = "A. Yılmaz", Project = "PD170" });
            var d = store.AddDefect(folder, "Dent", x => { x.Depth = 0.5; x.AffectedParts = "1-5, 8"; });
            store.UpdateDefect(folder, d.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "OP40"));

            var result = new ReinspectResult(false, null, null, new[] { new NewDefectSpec("Crack", null) }, null);
            var (_, children) = store.ApplyReinspect(folder, d.Id, result, "A. Yılmaz");
            Expect(children.Count == 1 && children[0].AffectedParts == "1-5, 8", "Child parent'ın parçalarını devralmalı");
            Expect(store.LoadDefects(folder).First(x => x.Id == children[0].Id).AffectedParts == "1-5, 8", "Devir diske yazılmalı");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    public static void ReportNonconformanceAndPartsLines()
    {
        var session = new Session { PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-R", Inspector = "A. Yılmaz", Project = "PD170" };
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 0.5, AffectedParts = "1-5, 8" };
        d.Photos.AddRange(new[] { "1_photo_01.jpg", "1_photo_02.jpg" });
        var html = ReportBuilder.Build(session, new[] { d }, 1);

        Expect(html.Contains("Uygunsuzluk a&#231;&#305;klamas&#305; / Nonconformance description") || html.Contains("Nonconformance description"), "Bölüm başlığı");
        Expect(html.Contains("There is a Dent on parts 1–5 and 8: D 0.5 mm.") || html.Contains("There is a Dent on parts 1&#8211;5 and 8: D 0.5 mm."), "Cümle raporda olmalı");
        Expect(!html.Contains("Defect 1: There is"), "Önek ve disposition satırı raporda olmamalı");
        // Çok fotoğraflı kusurda yalnızca ilk sayfada.
        var count = 0;
        for (var i = html.IndexOf("Nonconformance description", StringComparison.Ordinal); i >= 0; i = html.IndexOf("Nonconformance description", i + 1, StringComparison.Ordinal))
            count++;
        Expect(count == 1, $"Açıklama yalnızca ilk sayfada: {count}");
        Expect(html.Contains("Affected parts:</strong> 1-5, 8"), "Etkilenen parçalar satırı");

        var plain = ReportBuilder.Build(session, new[] { new Defect { Id = 2, DefectType = "Dent" } }, 1);
        Expect(!plain.Contains("Affected parts"), "Boşsa satır gösterilmez");
        Expect(plain.Contains("There is a Dent on the part."), "Açıklama yine de yazılır");
    }

    public static void CsvHasAffectedPartsColumn()
    {
        var row = new IndexRow("P", "O", "S", "Open", 1, 1, "", DateTime.Now,
            new[] { new IndexDefect(1, "Dent", "Open", 0, "", "1-5, 8"), new IndexDefect(2, "Crack", "Open", 0) }, "A", DateTime.Now);
        var lines = CsvExport.Build(new[] { row }).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        Expect(lines[0].EndsWith(";Son karar;Etkilenen parçalar"), lines[0]);
        Expect(lines[1].EndsWith(";\"1-5, 8\"") || lines[1].EndsWith(";1-5, 8"), lines[1]);
        Expect(lines[2].EndsWith(";"), lines[2]);
    }
}
