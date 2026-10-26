using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Faz 3: Dashboard ViewModel testleri (gerçek SessionStore + geçici klasör).
public static class DashboardViewModelTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private sealed class Ctx
    {
        public required DashboardViewModel Dash { get; init; }
        public required SessionStore Store { get; init; }
        public List<(string Message, StatusKind Kind)> Notes { get; } = new();
        public string Root => Store.Root;
    }

    private static void With(Action<Ctx> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-dash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var c = new Ctx { Dash = new DashboardViewModel(store, SharedConfig.Defaults), Store = store };
            c.Dash.Notify += (m, k) => c.Notes.Add((m, k));
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
        Session S(string part, string op, string serial) => new()
        {
            PartNumber = part, OperationNumber = op, SerialNumber = serial, Inspector = "A. Yılmaz", Project = part,
        };

        var (f1, _) = c.Store.OpenOrCreate(S("PD170", "OP40", "SN-A1"));
        c.Store.AddDefect(f1, "Dent", d => DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "M. Kaya"));

        var (f2, s2) = c.Store.OpenOrCreate(S("PD170", "OP50", "SN-A2"));
        c.Store.AddDefect(f2, "Scratch", d => DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya", reference: "S-1"));
        s2.Status = SessionStatus.Completed;
        c.Store.SaveSession(f2, s2);

        var (f3, _) = c.Store.OpenOrCreate(S("TS1400", "OP40", "SN-B1"));
        c.Store.AddDefect(f3, "Undercut", d => DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "M. Kaya", targetOperation: "SAME"));
    }

    private static string Card(SummaryCardData[] cards, string label) => cards.First(x => x.Label == label).Value;

    public static void DashEmptyBeforeRefreshThenFiltersAndSummary() => With(c =>
    {
        Seed(c);
        Check(c.Dash.Rows.Count == 0 && c.Dash.SummaryCards.Count == 0, "Yenilemeden önce boş");

        c.Dash.Refresh();
        Check(c.Dash.Rows.Count == 3, $"3 satır beklenir: {c.Dash.Rows.Count}");
        Check(c.Dash.EmptyText is null, "Doluyken açıklama yok");

        var cards = c.Dash.SummaryCards.ToArray();
        Check(cards.Length == 5 && cards[0].Label == "Seri / operasyon" && cards[0].Accent == "#1A3A5C", "Beş özet kartı");
        Check(Card(cards, "Seri / operasyon") == "3", "Seri sayısı");
        Check(Card(cards, "Açık kusur") == "2", "Açık kusur: MRB + rework");
        Check(Card(cards, "MRB bekleyen") == "1", "MRB sayısı");
        Check(Card(cards, "Rework sonrası bekleyen") == "1", "Rework sonrası sayısı");
        Check(Card(cards, "Tamamlanan muayene") == "1", "Tamamlanan sayısı");

        // Parça süzgeci (içerir, büyük/küçük harf duyarsız).
        c.Dash.PartFilter = "pd170";
        Check(c.Dash.Rows.Count == 2 && Card(c.Dash.SummaryCards.ToArray(), "Seri / operasyon") == "2", "Parça süzgeci özeti de günceller");

        // Hata tipi süzgeci.
        c.Dash.PartFilter = "";
        c.Dash.TypeFilter = "Undercut";
        Check(c.Dash.Rows.Single().Row.Serial == "SN-B1", "Tip süzgeci");

        // Durum süzgeci.
        c.Dash.TypeFilter = DashboardViewModel.AllTypes;
        c.Dash.StateFilter = DashboardViewModel.StateFilters.First(s => s.Filter == IndexStateFilter.MrbPending);
        Check(c.Dash.Rows.Single().Row.Serial == "SN-A1", "MRB süzgeci");
        c.Dash.StateFilter = DashboardViewModel.StateFilters.First(s => s.Filter == IndexStateFilter.Completed);
        Check(c.Dash.Rows.Single().Row.Serial == "SN-A2", "Tamamlanan süzgeci");
    });

    public static void DashEmptyTextsAndClearFilters() => With(c =>
    {
        c.Dash.Refresh();
        Check(c.Dash.Rows.Count == 0 && c.Dash.EmptyText == "Henüz kayıt yok. Muayene ekranından oturum açın ya da Yenile'ye basın.", $"Kayıt yok metni: {c.Dash.EmptyText}");

        Seed(c);
        c.Dash.Refresh();
        c.Dash.PartFilter = "yok-boyle-parca";
        Check(c.Dash.Rows.Count == 0 && c.Dash.EmptyText == "Filtrelere uyan kayıt yok.", $"Süzgeç metni: {c.Dash.EmptyText}");

        c.Dash.SerialFilter = "x";
        c.Dash.OperationFilter = "y";
        c.Dash.TypeFilter = "Dent";
        c.Dash.StateFilter = DashboardViewModel.StateFilters[2];

        var changes = 0;
        c.Dash.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DashboardViewModel.Rows)) changes++; };
        c.Dash.ClearFilters();
        Check(changes == 1, $"Temizleme tek kez uygulanmalı: {changes}");
        Check(c.Dash.PartFilter == "" && c.Dash.OperationFilter == "" && c.Dash.SerialFilter == ""
              && c.Dash.TypeFilter == DashboardViewModel.AllTypes && c.Dash.StateFilter == DashboardViewModel.StateFilters[0], "Süzgeçler sıfırlanmalı");
        Check(c.Dash.Rows.Count == 3 && c.Dash.EmptyText is null, "Temizleyince tüm satırlar");
    });

    public static void DashRowCardsCarryChipsAndOpenEvent() => With(c =>
    {
        Seed(c);
        c.Dash.Refresh();

        var mrb = c.Dash.Rows.First(r => r.Row.Serial == "SN-A1");
        Check(mrb.Title == "PD170  ·  OP40  ·  SN-A1", $"Başlık: {mrb.Title}");
        Check(mrb.Badge == new ChipData("1 açık kusur", "#FEF3C7", "#B45309") && mrb.HasOpenDefects, "Açık kusur rozeti");
        Check(mrb.DefectChips.Single() == new ChipData("#1 Dent · MRB bekliyor", "#EDE9FE", "#6D28D9") || mrb.DefectChips.Single().Background == "#EDE9FE",
            $"Kusur etiketi: {mrb.DefectChips.Single().Text}");
        Check(mrb.Meta.StartsWith("Inspector: A. Yılmaz   ·   Açılış: ") && mrb.Meta.EndsWith("   ·   1 kusur"), $"Meta: {mrb.Meta}");

        var done = c.Dash.Rows.First(r => r.Row.Serial == "SN-A2");
        Check(done.Badge == new ChipData("Tamamlandı", "#DCFCE7", "#15803D") && !done.HasOpenDefects, "Tamamlandı rozeti");
        Check(done.DefectChips.Single().Background == "#DCFCE7", "Kapalı kusur yeşil");

        IndexRow? opened = null;
        c.Dash.OpenRequested += r => opened = r;
        c.Dash.Open(done);
        Check(opened?.Serial == "SN-A2", "Aç olayı satırı iletmeli");
    });

    public static void DashVerifyAuditIntactAndBroken() => With(c =>
    {
        AuditLog.Append(c.Store, "A. Yılmaz", "defect.add", "#1 Dent");
        AuditLog.Append(c.Store, "M. Kaya", "disposition", "#1 Rework");
        AuditLog.Append(c.Store, "M. Kaya", "disposition", "#1 Reinspect");

        c.Dash.VerifyAudit();
        Check(c.Dash.IsAuditOpen && c.Dash.AuditKind == StatusKind.Success
              && c.Dash.AuditTitle == "Denetim kaydı sağlam" && c.Dash.AuditMessage == "Zincir baştan sona doğrulandı.", "Sağlam zincir sonucu");

        var path = Path.Combine(c.Root, AuditLog.FileName);
        var lines = File.ReadAllLines(path);
        lines[1] = lines[1].Replace("Rework", "Accept");
        File.WriteAllLines(path, lines);

        c.Dash.VerifyAudit();
        Check(c.Dash.AuditKind == StatusKind.Error && c.Dash.AuditTitle == "Denetim kaydı bozulmuş"
              && c.Dash.AuditMessage == "2. satırda zincir kırılmış.", $"Bozuk zincir: {c.Dash.AuditMessage}");
    });
}
