using System.Globalization;
using MuayeneKayit.Core;

// Geri bildirim 2: rapor başlığı, "Türkçe / English" sabit metinler, ay harfli tarih biçimi.
public static class ReportBilingualTests
{
    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    /// <summary>Raporu Türkçe kültürde üretir (tarih biçimi kültürden bağımsız olmalı).</summary>
    private static string BuildInTurkish(Session session, params Defect[] defects)
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            return ReportBuilder.Build(session, defects, 3);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    private static string H(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static Session NewSession() => new()
    {
        PartNumber = "PD170", OperationNumber = "OP40", SerialNumber = "SN-B1", Inspector = "A. Yılmaz", Project = "PD170",
        CreatedAt = new DateTime(2026, 10, 27, 8, 0, 0),
    };

    public static void ReportDatesAreInvariantMonthDayYear()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var t = new DateTime(2026, 10, 27, 9, 10, 0);
            Expect(ReportDates.Day(t) == "Oct.27.2026", ReportDates.Day(t));
            Expect(ReportDates.DayTime(t) == "Oct.27.2026 09:10", ReportDates.DayTime(t));
            Expect(ReportDates.Day(new DateTime(2026, 1, 5)) == "Jan.05.2026", "Gün iki hane");
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    public static void HeaderIsSingleBilingualLine()
    {
        var html = BuildInTurkish(NewSession(), new Defect { Id = 1, DefectType = "Dent" });
        Expect(html.Contains(H("Parça / Part PD170")) && html.Contains("Operasyon / Operation OP40") && html.Contains("Seri / Serial SN-B1"),
            "Başlık iki dilli olmalı");
        var body = html[html.IndexOf("<body>", StringComparison.Ordinal)..];
        Expect(!body.Contains("Proje") && !body.Contains("Inspector") && !body.Contains("Rapor v") && !body.Contains("Taslak") && !body.Contains("Tamamland"),
            "Başlıkta proje / Inspector / sürüm / durum olmamalı");
        Expect(!body.Contains("Yılmaz"), "Inspector adı raporda görünmemeli");

        var empty = BuildInTurkish(NewSession());
        Expect(empty.Contains(H("Parça / Part PD170")) && empty.Contains("Kusur kaydı yok. / No defects recorded."), "Kusursuz oturum aynı başlığı kullanmalı");
        Expect(empty.Contains("Sayfa / Page 1 / 1"), "Kusursuz oturum sayfa numarası");
    }

    public static void FooterShowsSessionOpeningDate()
    {
        var d = new Defect { Id = 9, DefectType = "Deformation" };
        var html = BuildInTurkish(NewSession(), d);
        Expect(html.Contains("Kusur / Defect #9 · Deformation · Oct.27.2026"), "Alt bilgide açılış tarihi");
        Expect(html.Contains("Sayfa / Page 1 / 1"), "Alt bilgide sayfa");
        Expect(!html.Contains("27.10.2026"), "Eski tarih biçimi kalmamalı");
    }

    public static void TableDatesUseMonthNames()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 1 };
        DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
        d.Dispositions[0].DecidedAt = new DateTime(2026, 10, 27, 9, 10, 0);
        MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Depth = 2 }, "A");
        d.MeasurementEdits[0] = d.MeasurementEdits[0] with { At = new DateTime(2026, 11, 3, 14, 5, 0) };

        var html = BuildInTurkish(NewSession(), d);
        Expect(html.Contains("<td>Oct.27.2026 09:10</td>"), "Karar geçmişi tarihi MMM.dd.yyyy HH:mm");
        Expect(html.Contains("<td>Nov.03.2026 14:05</td>"), "Ölçüm düzeltmesi tarihi MMM.dd.yyyy HH:mm");
    }

    public static void DefectPageLabelsAreBilingual()
    {
        var d = new Defect { Id = 1, DefectType = "Dent", Depth = 1, Radius = 2, Notes = "kullanıcı notu" };
        DefectStateMachine.Apply(d, Decision.Accept, DecisionSource.Engineer, "M. Kaya");
        MeasurementEditor.Apply(d, MeasurementEditor.Snapshot(d) with { Depth = 2 }, "A");

        var html = BuildInTurkish(NewSession(), d);
        foreach (var text in new[]
        {
            "Ölçümler / Measurements", "Derinlik / Depth", "Genişlik / Width", "Uzunluk / Length", "Çap / Diameter", "Açı / Angle", "Renk / Color",
            "Karar geçmişi / Disposition history", "Ölçüm düzeltmeleri / Measurement corrections",
            "Tarih / Date", "Kim / By", "Karar / Decision", "Not / Note", "Referans / Reference", "Ölçüm (karar anı) / Measurement at decision",
            "Önce / Before", "Sonra / After",
            "Kapandı / Closed", "Kabul (SPA) / Accept",
        })
            Expect(html.Contains(H(text)), $"İki dilli metin eksik: {text}");

        // Kullanıcı girdisi çevrilmez.
        Expect(html.Contains("kullanıcı notu"), "Not olduğu gibi yazılmalı");
    }

    public static void StateAndDecisionNamesAreBilingual()
    {
        Expect(ReportText.State(DefectState.Repairing) == "Repair'de / In repair", "Repair durumu");
        Expect(ReportText.State(DefectState.AwaitingReinspect) == "Rework sonrası muayene bekliyor / Awaiting reinspection", "Reinspect durumu");
        Expect(ReportText.State(DefectState.WaitingVendor) == "Tedarikçide / At vendor", "Vendor durumu");
        Expect(ReportText.State(DefectState.Superseded) == "Devredildi / Superseded", "Devredildi durumu");
        Expect(ReportText.DecisionName(Decision.Rework) == "Rework → Yeniden muayene / Rework / Reinspect", "Rework kararı");
        Expect(ReportText.DecisionName(Decision.SubmitMrb) == "MRB'ye gönderildi / Submit MRB (pending)", "MRB kararı");
        Expect(ReportText.DecisionName(Decision.ReturnToVendor) == "Tedarikçiye iade / Return to vendor", "Vendor kararı");
        Expect(ReportText.SystemNote("MRB reddi, otomatik hurda") == "MRB reddi, otomatik hurda / MRB rejected, automatic scrap", "Sistem notu");
        Expect(ReportText.SystemNote("Kullanıcı yazdı") == "Kullanıcı yazdı", "Kullanıcı notu çevrilmez");

        var d = new Defect { Id = 1, DefectType = "Dent" };
        DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M", targetOperation: "OP60", reworkScope: ReworkScope.HighMetalOnly);
        Expect(ReportText.DecisionName(d.Dispositions[0]).EndsWith("/ Rework / Reinspect (high metal only)"), "High metal kapsamı");
    }

    public static void LinkSentencesAndRegionLinesAreBilingual() => WithStore((store, folder) =>
    {
        var parent = store.AddDefect(folder, "Dent", x => { x.Depth = 0.5; x.Photos.AddRange(new[] { "1_photo_01.jpg", "1_photo_02.jpg" }); });
        store.UpdateDefect(folder, parent.Id, x => DefectStateMachine.Apply(x, Decision.Rework, DecisionSource.Engineer, "M", targetOperation: "SAME"));
        var p = store.LoadDefects(folder).First();
        store.ApplyReinspect(folder, 1,
            new ReinspectResult(true, MeasurementEditor.Snapshot(p),
                new Dictionary<string, PhotoOutcome> { ["1_photo_01.jpg"] = PhotoOutcome.Conforms, ["1_photo_02.jpg"] = PhotoOutcome.NewDefect },
                new[] { new NewDefectSpec("Scratch", "1_photo_02.jpg") }, null), "CTP");

        var html = ReportBuilder.Build(NewSession(), store.LoadDefects(folder), 1);
        Expect(html.Contains(H("Bölge / Region 1.1 — Giderildi / Resolved (")), "Bölge satırı iki dilli");
        Expect(html.Contains("Yeni kusur / New defect #2"), "Yeni kusur bölge satırı");
        Expect(html.Contains("<span class=\"en\">Found at region 1.2 of defect 1 during inspection after rework.</span>"), "Child bağlantısının İngilizcesi");
        Expect(html.Contains("<span class=\"en\">A new defect was also found during inspection after rework: defect 2 is tracked separately.</span>"), "Parent bağlantısının İngilizcesi");
        Expect(html.Contains("Fotoğraf / Photo 1 / 2"), "Fotoğraf sayısı iki dilli");
        Expect(html.Contains("Disposition history and corrections on page #1.1."), "İlk sayfaya yönlendirme iki dilli");
    });

    private static void WithStore(Action<SessionStore, string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var (folder, _) = store.OpenOrCreate(NewSession());
            body(store, folder);
        }
        finally { try { Directory.Delete(root, recursive: true); } catch (IOException) { } }
    }
}
