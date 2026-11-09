using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Muayene ekranı ViewModel testleri: gerçek SessionStore + geçici klasör, sahte denetim kaydı.
// Program.cs bu sınıfı "using static" ile kullanır.
public static partial class ViewModelTests
{
    private sealed class FakeAudit : IAuditTrail
    {
        public List<string> Entries { get; } = new();

        public void Append(string actor, string action, string details) =>
            Entries.Add($"{actor}|{action}|{details}");
    }

    private sealed class Ctx
    {
        public required InspectionViewModel Vm { get; init; }
        public required SessionStore Store { get; init; }
        public required FakeAudit Audit { get; init; }
        public required FakeDialogService Dialogs { get; init; }
        public required FakeFilePicker Picker { get; init; }
        public required FakeWindowService Windows { get; init; }
        public required FakePdfConverter Pdf { get; init; }
        public required FakeUserSettings Settings { get; init; }
        public required FakeWindowsUser WindowsUser { get; init; }
        public List<(string Message, StatusKind Kind)> Notes { get; } = new();
        public string Folder => Vm.Folder!;

        public void Open()
        {
            Vm.PartNumber = "PD170";
            Vm.OperationNumber = "OP40";
            Vm.SerialNumber = "SN001";
            Vm.InspectorName = "A. Yılmaz";
            Vm.OpenSession();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void With(Action<Ctx> body) => With(body, null, null);

    private static void With(Action<Ctx> body, IReadOnlyList<DefectTypeTemplate>? types, IReadOnlyList<CustomFieldDef>? customFields)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var audit = new FakeAudit();
            var dialogs = new FakeDialogService();
            var picker = new FakeFilePicker();
            var windows = new FakeWindowService();
            var pdf = new FakePdfConverter();
            var settings = new FakeUserSettings();
            var windowsUser = new FakeWindowsUser();
            var ctx = new Ctx
            {
                Vm = new InspectionViewModel(store, audit, types ?? SharedConfig.Defaults, dialogs, picker, windows, pdf, settings, windowsUser, customFields),
                WindowsUser = windowsUser,
                Store = store,
                Audit = audit,
                Dialogs = dialogs,
                Picker = picker,
                Windows = windows,
                Pdf = pdf,
                Settings = settings,
            };
            ctx.Vm.Notify += (m, k) => ctx.Notes.Add((m, k));
            body(ctx);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    public static void VmOpensNewSession() => With(c =>
    {
        c.Open();
        Check(c.Vm.IsSessionOpen && !c.Vm.IsSessionClosed, "Oturum açık olmalı");
        Check(c.Vm.SessionTitle == "PD170  ·  OP40  ·  SN001", $"Başlık: {c.Vm.SessionTitle}");
        Check(c.Vm.IsAddDefectPrimary, "Liste boşken Kusur ekle vurgulu olmalı");
        Check(c.Vm.IsListEmpty && c.Vm.Defects.Count == 0, "Liste boş olmalı");
        Check(c.Notes.Last().Kind == StatusKind.Success, "Başarı bildirimi beklenir");
        Check(c.Audit.Entries.Any(e => e.Contains("|session.open|")), "Oturum denetim kaydına yazılmalı");
    });

    public static void VmAddDefectSelectsMeasure() => With(c =>
    {
        c.Open();
        Check(c.Vm.AddDefect("Dent"), "Kusur eklenmeli");
        Check(c.Vm.Defects.Count == 1 && c.Vm.SelectedDefect?.Id == 1 && c.Vm.SelectedId == 1, "Yeni kusur seçilmeli");
        Check(c.Vm.SelectedStep == InspectionStep.Measure, "Ölçüm sekmesi seçili olmalı");
        Check(c.Vm.IsSavePrimary && !c.Vm.IsCameraPrimary, "Kaydet vurgulu olmalı");
        Check(!c.Vm.IsAddDefectPrimary, "Kusur varken Kusur ekle vurgusu kalkmalı");
        Check(c.Vm.HasSelection && c.Vm.IsEditable, "Detay açık ve düzenlenebilir olmalı");
    });

    public static void VmSaveMeasurementsGoesToPhoto() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.12";
        c.Vm.SaveMeasurements();
        Check(c.Vm.SelectedStep == InspectionStep.Photo, $"Fotoğraf sekmesi beklenir: {c.Vm.SelectedStep}");
        Check(c.Vm.MeasureTabLabel == "✓ 1. Ölçüm", $"Etiket: {c.Vm.MeasureTabLabel}");
        Check(c.Vm.IsCameraPrimary && !c.Vm.IsSavePrimary, "Kamera vurgulu olmalı");
        Check(c.Notes.Last().Kind == StatusKind.Success, "Başarı bildirimi beklenir");
    });

    public static void VmInvalidMeasurementNotifiesError() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        var path = Path.Combine(c.Folder, "defects", "1.json");
        var before = File.ReadAllText(path);

        c.Vm.DepthText = "abc";
        c.Vm.SaveMeasurements();

        Check(c.Notes.Last().Kind == StatusKind.Error, "Hata bildirimi beklenir");
        Check(c.Notes.Last().Message.Contains("Derinlik sayı olmalı"), $"Mesaj: {c.Notes.Last().Message}");
        Check(File.ReadAllText(path) == before, "Dosya değişmemeli");
    });

    public static void VmHighMetalShowsInTypeAndSentence() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.1";
        c.Vm.HighMetal = true;
        c.Vm.SaveMeasurements();
        Check(c.Vm.Defects[0].Type == "Dent + High metal", $"Tip: {c.Vm.Defects[0].Type}");
        Check(c.Vm.Sentence.Contains("with high metal"), $"Cümle: {c.Vm.Sentence}");
        Check(c.Vm.Header.EndsWith("Dent + High metal"), $"Başlık: {c.Vm.Header}");
    });

    public static void VmFirstSelectionAfterOpenUsesCurrentStep() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(c.Vm.SelectedStep == InspectionStep.Measure, "Başlangıçta Ölçüm");

        // Kusur ölçüm ve fotoğrafla ilerler; karar bekliyor.
        c.Store.UpdateDefect(c.Folder, 1, d => { d.Depth = 0.1; d.Photos.Add("1_photo_01.jpg"); });

        c.Vm.ChangeSession();
        c.Vm.OpenSession();
        c.Vm.Select(1);
        Check(c.Vm.SelectedStep == InspectionStep.Decision, $"Disposition sekmesi beklenir: {c.Vm.SelectedStep}");
        Check(c.Vm.CurrentStep == InspectionStep.Decision, "Sıradaki adım karar olmalı");
    });

    public static void VmRefreshKeepsSelectedStep() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.SelectedStep = InspectionStep.Photo;
        c.Store.UpdateDefect(c.Folder, 1, d => d.Depth = 0.1);
        c.Vm.Refresh(1);
        Check(c.Vm.SelectedStep == InspectionStep.Photo, $"Sekme korunmalı: {c.Vm.SelectedStep}");
        Check(c.Vm.SelectedId == 1 && c.Vm.SelectedDefect?.Id == 1, "Seçim korunmalı");
    });

    /// <summary>
    /// Regresyon: liste yenilenince ListView seçimi bir an boşa düşürüp aynı kusuru yeniden seçer.
    /// Bu "yeni kusur" sayılmamalı; sekme (ör. fotoğraf eklerken Fotoğraflar) değişmemeli.
    /// </summary>
    public static void VmListViewReselectAfterRefreshKeepsTab() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Store.UpdateDefect(c.Folder, 1, d => { d.Depth = 0.1; d.Photos.Add("1_photo_01.jpg"); });
        c.Vm.Refresh(1);
        c.Vm.SelectedStep = InspectionStep.Photo;

        // ListView'in yaptığı: önce boş seçim, sonra aynı öğe (yeni nesne).
        c.Vm.SelectedDefect = null;
        c.Vm.SelectedDefect = c.Vm.Defects.First(d => d.Id == 1);

        Check(c.Vm.SelectedId == 1 && c.Vm.HasSelection, "Seçim korunmalı");
        Check(c.Vm.SelectedStep == InspectionStep.Photo, $"Sekme Fotoğraflar'da kalmalı: {c.Vm.SelectedStep}");
    });

    public static void VmDecisionTabCheckAfterDecision() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Store.UpdateDefect(c.Folder, 1, d => { d.Depth = 0.1; d.Photos.Add("1_1.jpg"); });
        c.Vm.Refresh(1);
        Check(c.Vm.DecisionTabLabel == "3. Disposition", $"Açık kusurda ✓ olmamalı: {c.Vm.DecisionTabLabel}");

        c.Store.UpdateDefect(c.Folder, 1, d =>
            DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "A. Yılmaz"));
        c.Vm.Refresh(1);
        Check(c.Vm.DecisionTabLabel == "✓ 3. Disposition", $"Karar sonrası ✓ beklenir: {c.Vm.DecisionTabLabel}");
    });

    public static void VmCompleteRules() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.Complete();
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message.Contains("açık kusurlar var"),
            $"Uyarı beklenir: {c.Notes.Last().Message}");
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Open, "Oturum tamamlanmamalı");
        Check(!c.Vm.IsCompletePrimary, "Açık kusurda Tamamla vurgulu olmamalı");

        c.Store.UpdateDefect(c.Folder, 1, d =>
            DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, "A. Yılmaz", note: "Yanlış giriş"));
        c.Vm.Refresh(null);
        Check(c.Vm.IsCompletePrimary, "Açık kusur kalmayınca Tamamla vurgulu olmalı");

        c.Vm.Complete();
        Check(c.Store.LoadSession(c.Folder)!.Status == SessionStatus.Completed, "Oturum tamamlanmalı");
        Check(c.Notes.Last().Kind == StatusKind.Success, "Başarı bildirimi beklenir");
        Check(!c.Vm.IsCompletePrimary, "Tamamlanan oturumda vurgu kalkmalı");
        Check(c.Vm.SessionSubtitle.EndsWith("Tamamlandı"), $"Alt başlık: {c.Vm.SessionSubtitle}");
    });

    public static void VmDecisionOptions() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        var labels = c.Vm.DecisionOptions.Select(o => o.Label).ToList();
        Check(labels.First() == "Kabul (SPA)", $"İlk seçenek Kabul olmalı: {labels.First()}");
        Check(labels.Contains("Rework → Reinspect"), "Rework seçeneği olmalı");
        Check(!c.Vm.DecisionOptions.Any(o => o.IsReinspect) && !c.Vm.IsAwaiting, "Reinspect seçeneği olmamalı");

        c.Store.UpdateDefect(c.Folder, 1, d =>
            DefectStateMachine.Apply(d, Decision.Rework, DecisionSource.Engineer, "A. Yılmaz", targetOperation: "OP40"));
        c.Vm.Refresh(1);
        Check(c.Vm.IsAwaiting, "Rework sonrası muayene beklenmeli");
        Check(c.Vm.DecisionOptions[0].IsReinspect && c.Vm.DecisionOptions[0].Label == "Rework sonrası muayene sonucu…",
            "İlk seçenek rework sonrası muayene olmalı");
        Check(!c.Vm.IsEditable && !c.Vm.ShowMeasureArea, "Ölçümler kilitli olmalı");
    });

    public static void VmUndoTextAfterDecision() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(!c.Vm.CanUndo && c.Vm.UndoText == "", "Karar yokken geri alma olmamalı");

        c.Store.UpdateDefect(c.Folder, 1, d =>
            DefectStateMachine.Apply(d, Decision.SubmitMrb, DecisionSource.Engineer, "A. Yılmaz"));
        c.Vm.Refresh(1);
        Check(c.Vm.CanUndo, "Karar sonrası geri alınabilmeli");
        Check(c.Vm.UndoText.StartsWith("Son karar: ") && c.Vm.UndoText.Contains("A. Yılmaz"), $"Metin: {c.Vm.UndoText}");
    });

    public static void VmChangeSessionClears() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.ChangeSession();
        Check(!c.Vm.IsSessionOpen && c.Vm.IsSessionClosed, "Oturum kapanmalı");
        Check(c.Vm.Defects.Count == 0 && c.Vm.IsListEmpty, "Liste boşalmalı");
        Check(c.Vm.SelectedDefect is null && c.Vm.SelectedId is null && c.Vm.Current is null && !c.Vm.HasSelection,
            "Seçim temizlenmeli");
        Check(c.Vm.Session is null && c.Vm.Folder is null, "Oturum bilgisi temizlenmeli");
    });

    // ───────────────────────── Diyalog akışları (FakeDialogService) ─────────────────────────

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private static DecisionOption Option(Ctx c, Decision decision) =>
        c.Vm.DecisionOptions.First(o => o.Decision == decision);

    private static Defect Load(Ctx c, int id) => c.Store.LoadDefects(c.Folder).First(d => d.Id == id);

    private static DefectState StateOf(Ctx c, int id) => DefectStateMachine.GetState(Load(c, id));

    private static readonly MeasurementSnapshot NoValues = new(null, null, null, null, null, null);

    /// <summary>Dent kusuru ekler, derinlik (ve isteğe bağlı high metal) kaydeder.</summary>
    private static void AddMeasuredDent(Ctx c, bool highMetal = false)
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "0.5";
        c.Vm.HighMetal = highMetal;
        c.Vm.SaveMeasurements();
    }

    /// <summary>Rework verir (kapsam isteğe bağlı); "şimdi gir" cevabı kuyruktan gelmezse rework'te bekler.</summary>
    private static void SendToRework(Ctx c, ReworkScope? scope = null)
    {
        c.Dialogs.Decisions.Enqueue(new DecisionInput("OP40", null, null, scope));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Rework)));
    }

    public static void VmDecisionAcceptClosesAndCancelChangesNothing() => With(c =>
    {
        AddMeasuredDent(c);

        // Vazgeç (cevap yok → null): değişiklik yok.
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(Load(c, 1).Dispositions.Count == 0, "Vazgeçilince karar yazılmamalı");
        Check(!c.Audit.Entries.Any(e => e.Contains("|disposition|")), "Denetim satırı olmamalı");
        var req = c.Dialogs.DecisionRequests.Last();
        Check(req.Label == "Kabul (SPA)" && req.NeedsReference && !req.NoteRequired && req.TargetHeader is null,
            $"İstek: {req.Label}");
        Check(req.EffectText.Length > 0, "Etki metni dolu olmalı");

        // Kabul.
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, "  SPA-1  ", null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(StateOf(c, 1) == DefectState.Closed, $"Kapanmalı: {StateOf(c, 1)}");
        Check(Load(c, 1).Dispositions.Last().Reference == "SPA-1", "Referans kırpılmalı");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|disposition|#1 Accept")), "Denetim satırı beklenir");
        Check(c.Notes.Last().Kind == StatusKind.Success, "Başarı bildirimi beklenir");
    });

    public static void VmReworkHighMetalAsksScopeAndWaits() => With(c =>
    {
        AddMeasuredDent(c, highMetal: true);
        SendToRework(c, ReworkScope.HighMetalOnly);

        var req = c.Dialogs.DecisionRequests.Last();
        Check(req.NeedsReworkScope, "High metal'li rework kapsam sormalı");
        Check(req.TargetHeader == "Rework sonrası muayene hangi operasyonda yapılacak?" && req.TargetDefault == "OP40",
            $"Hedef: {req.TargetHeader} / {req.TargetDefault}");
        Check(Load(c, 1).Dispositions.Last().ReworkScope == ReworkScope.HighMetalOnly, "Kapsam kaydedilmeli");
        Check(c.Dialogs.ReinspectNowCalls.Count == 1 && c.Dialogs.ReinspectRequests.Count == 0, "Şimdi hayır: sonuç penceresi açılmamalı");
        Check(StateOf(c, 1) == DefectState.AwaitingReinspect, "Rework sonrası muayene beklenmeli");
    });

    public static void VmReworkNowOpensReinspectDialog() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.ReinspectNow.Enqueue(true);
        SendToRework(c);
        Check(!c.Dialogs.DecisionRequests.Last().NeedsReworkScope, "High metal yoksa kapsam sorulmaz");
        Check(c.Dialogs.ReinspectRequests.Count == 1, "Sonuç penceresi açılmalı");
        var r = c.Dialogs.ReinspectRequests[0];
        Check(r.IsRework && r.DefectId == 1 && r.Before.Depth == 0.5 && r.TargetOperation == "OP40", "İstek içeriği");
        Check(StateOf(c, 1) == DefectState.AwaitingReinspect, "Vazgeçilince bekler");
    });

    public static void VmReinspectSameHighMetalGoneKeepsDepth() => With(c =>
    {
        AddMeasuredDent(c, highMetal: true);
        SendToRework(c, ReworkScope.HighMetalOnly);

        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Same, NoValues, false, null, null));
        Wait(c.Vm.ReinspectAsync());

        var d = Load(c, 1);
        Check(StateOf(c, 1) == DefectState.Open, $"Durum Açık olmalı: {StateOf(c, 1)}");
        Check(!d.HighMetal && d.Depth == 0.5, $"High metal: {d.HighMetal}, derinlik: {d.Depth}");
        Check(d.Dispositions.Last().Note == "High metal giderildi; ana kusur devam ediyor.", $"Not: {d.Dispositions.Last().Note}");
        Check(c.Notes.Last().Kind == StatusKind.Warning && c.Notes.Last().Message.Contains("high metal giderildi"),
            $"Mesaj: {c.Notes.Last().Message}");
        Check(c.Dialogs.ReinspectRequests[0].HighMetalOnlyScope, "Kapsam bilgisi isteğe girmeli");
    });

    public static void VmReinspectNewOpensChild() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);

        var childType = c.Vm.TypeNames.First(n => n != "Dent");
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { childType }, "yeni hata"));
        Wait(c.Vm.ReinspectAsync());

        Check(c.Vm.Defects.Count == 2 && c.Vm.SelectedId == 2, $"Child seçilmeli: {c.Vm.SelectedId}");
        Check(Load(c, 2).DefectType == childType && Load(c, 2).SupersedesId == 1, "Child parent'a bağlanmalı");
        Check(c.Audit.Entries.Any(e => e.Contains("|disposition.continue|#1 -> #2")), "Denetim satırı beklenir");
    });

    public static void VmReinspectConformsAndWrongStateErrors() => With(c =>
    {
        AddMeasuredDent(c);

        // Beklemeyen kusurda hata bildirilir, pencere açılmaz.
        Wait(c.Vm.ReinspectAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Dialogs.ReinspectRequests.Count == 0, "Durum hatası beklenir");

        SendToRework(c);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.Conforms, NoValues, null, null, null));
        Wait(c.Vm.ReinspectAsync());
        Check(StateOf(c, 1) == DefectState.Closed, "Uygun bulununca kapanmalı");
    });

    public static void VmUndoChildTransferVoidsChild() => With(c =>
    {
        AddMeasuredDent(c);
        SendToRework(c);
        c.Dialogs.Reinspects.Enqueue(new ReinspectInput(ReinspectOutcome.New, NoValues, null, new[] { c.Vm.TypeNames.First(n => n != "Dent") }, null));
        Wait(c.Vm.ReinspectAsync());

        c.Vm.Select(1);
        c.Dialogs.UndoReasons.Enqueue("yanlış devir");
        Wait(c.Vm.UndoLastAsync());

        var req = c.Dialogs.UndoRequests.Last();
        Check(req.ChildrenToVoid.SequenceEqual(new[] { 2 }) && req.DefectId == 1, $"Child bilgisi: {string.Join(",", req.ChildrenToVoid)}");
        Check(StateOf(c, 2) == DefectState.Voided, "Child void olmalı");
        Check(StateOf(c, 1) == DefectState.AwaitingReinspect, "Parent tekrar beklemeli");
        Check(c.Audit.Entries.Any(e => e.Contains("|disposition.undo|") && e.Contains("child #2 void")), "Denetim satırı beklenir");

        // Vazgeç: değişiklik yok.
        var path = Path.Combine(c.Folder, "defects", "1.json");
        var before = File.ReadAllText(path);
        Wait(c.Vm.UndoLastAsync());
        Check(File.ReadAllText(path) == before, "Vazgeçilince dosya değişmemeli");
    });

    public static void VmVoidNeedsReason() => With(c =>
    {
        AddMeasuredDent(c);

        Wait(c.Vm.VoidAsync());
        Check(StateOf(c, 1) == DefectState.Open && c.Dialogs.VoidRequests.Count == 1, "Gerekçesiz değişiklik yok");

        c.Dialogs.VoidReasons.Enqueue("  yanlış tip  ");
        Wait(c.Vm.VoidAsync());
        Check(StateOf(c, 1) == DefectState.Voided, "Void olmalı");
        Check(Load(c, 1).Dispositions.Last().Note == "yanlış tip", "Gerekçe kırpılmalı");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|defect.void|#1 yanlış tip")), "Denetim satırı beklenir");
    });

    public static void VmInspectorFromWindowsUser() => With(c =>
    {
        // Kapalıyken (varsayılan) elle yazılır.
        Check(!c.Vm.IsInspectorFromWindows && c.Vm.InspectorHeader == "Inspector", "Varsayılan elle giriş");

        // Admin açtı: muayene seçimine dönünce ad Windows oturumundan gelir, kutu salt okunur.
        SharedConfig.SaveUseWindowsUser(c.Store.Root, true);
        c.Vm.ChangeSession();
        Check(c.Vm.IsInspectorFromWindows && c.Vm.InspectorName == "Ali Kaymaz (12345)", $"Windows adı: {c.Vm.InspectorName}");

        // Kutuya başka ad yazılsa da oturum Windows kullanıcısıyla açılır ve denetim kaydına o yazılır.
        c.Vm.PartNumber = "PD170"; c.Vm.OperationNumber = "OP40"; c.Vm.SerialNumber = "SN001";
        c.Vm.InspectorName = "Başkası";
        c.Vm.OpenSession();
        Check(c.Vm.User == "Ali Kaymaz (12345)" && c.Vm.Session!.Inspector == "Ali Kaymaz (12345)", $"Kullanıcı: {c.Vm.User}");
        Check(c.Audit.Entries.Any(e => e.StartsWith("Ali Kaymaz (12345)|session.open")), "Denetim satırı Windows kullanıcısıyla");

        // AD adı yoksa yalnızca sicil no.
        c.WindowsUser.DisplayName = null;
        c.Vm.ChangeSession();
        Check(c.Vm.InspectorName == "12345", "Görünen ad yoksa sicil no");

        // Admin kapattı: kutu boşalır ve yeniden elle yazılır.
        SharedConfig.SaveUseWindowsUser(c.Store.Root, false);
        c.Vm.ChangeSession();
        Check(!c.Vm.IsInspectorFromWindows && c.Vm.InspectorName == "" && c.Vm.InspectorHeader == "Inspector", "Kapalıyken elle giriş");
    });

    public static void VmAutoSaveMergesAndKeepsStep() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");

        // Derinlik yazılıp kutudan çıkılır: kaydedilir, sekme ölçümde kalır.
        c.Vm.DepthText = "2";
        c.Vm.AutoSave();
        Check(Load(c, 1).Depth == 2 && c.Vm.SelectedStep == InspectionStep.Measure, "Otomatik kayıt yazmalı, sekme değişmemeli");
        Check(c.Vm.AutoSaveText.StartsWith("Otomatik kaydedildi"), "Kayıt saati gösterilmeli");

        // Genişlik ve High metal: aynı kişi kısa sürede → geçmişte tek satır.
        c.Vm.WidthText = "4";
        c.Vm.AutoSave();
        c.Vm.HighMetal = true;
        var d = Load(c, 1);
        Check(d.Width == 4 && d.HighMetal, "Genişlik ve High metal kaydedilmeli");
        Check(d.MeasurementEdits.Count == 1 && d.MeasurementEdits[0].Before.Depth is null && d.MeasurementEdits[0].After.Width == 4,
            "Kısa sürede yapılan girişler tek satırda birleşmeli");

        // Değişiklik yoksa yazılmaz; geçersiz değer kaydedilmez.
        var audits = c.Audit.Entries.Count;
        c.Vm.AutoSave();
        Check(c.Audit.Entries.Count == audits, "Değişiklik yoksa yazılmamalı");
        c.Vm.DepthText = "abc";
        c.Vm.AutoSave();
        Check(Load(c, 1).Depth == 2 && c.Notes.Last().Kind == StatusKind.Error, "Geçersiz değer kaydedilmemeli, hata bildirilmeli");
    });

    public static void VmAutoSaveOnDefectSwitch() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        c.Vm.DepthText = "1.5";
        c.Vm.NotesText = "kenar";
        // Kutudan çıkmadan yeni kusur eklenir: önceki kusurun formu kaybolmaz.
        c.Vm.AddDefect("Dent");
        Check(Load(c, 1).Depth == 1.5 && Load(c, 1).Notes == "kenar", "Kusur eklenirken önceki form kaydedilmeli");

        c.Vm.DepthText = "3";
        c.Vm.Select(1);
        Check(Load(c, 2).Depth == 3 && c.Vm.DepthText == "1.5", "Kusur değişirken önceki form kaydedilmeli, yeni kusur yüklenmeli");
    });

    public static void MeasurementEditMergeRules()
    {
        var t0 = new DateTime(2026, 10, 25, 10, 0, 0);
        var window = TimeSpan.FromMinutes(10);
        var d = new Defect { Id = 1, DefectType = "Dent" };
        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, null, null, null, null, null), "A", t0, window);
        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, 2, null, null, null, null), "A", t0.AddMinutes(1), window);
        Check(d.MeasurementEdits.Count == 1 && d.MeasurementEdits[0].Before.Depth is null, "Aynı kişi, süre içinde: birleşir");

        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, 3, null, null, null, null), "B", t0.AddMinutes(2), window);
        Check(d.MeasurementEdits.Count == 2, "Başka kişi: yeni satır");

        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, 4, null, null, null, null), "B", t0.AddMinutes(30), window);
        Check(d.MeasurementEdits.Count == 3, "Süre dışında: yeni satır");

        d.Dispositions.Add(new Disposition { Decision = Decision.SubmitMrb, DecidedAt = t0.AddMinutes(31) });
        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, 5, null, null, null, null), "B", t0.AddMinutes(32), window);
        Check(d.MeasurementEdits.Count == 4, "Araya karar girdiyse: yeni satır");

        // Değer son satırın öncesine dönerse satır kalkar.
        MeasurementEditor.Apply(d, new MeasurementSnapshot(1, 4, null, null, null, null), "B", t0.AddMinutes(33), window);
        Check(d.MeasurementEdits.Count == 3 && d.Width == 4, "Geri dönüşte satır kalkmalı");
    }

    public static void VmPhotoStepWithoutMeasurementAsks() => With(c =>
    {
        c.Open();
        c.Vm.AddDefect("Dent");
        Check(c.Vm.IsSkippingMeasurement, "Ölçümsüz açık kusurda atlama sayılmalı");

        // Uyarıda "Ölçüme dön": sekme ölçümde kalır.
        Check(!Wait(c.Vm.GoToPhotoStepAsync()), "Vazgeçince false");
        Check(c.Vm.SelectedStep == InspectionStep.Measure && c.Dialogs.SkipRequests.SequenceEqual(new[] { 1 }), "Ölçümde kalmalı, bir kez sorulmalı");

        // "Yine de fotoğrafa geç".
        c.Dialogs.SkipConfirms.Enqueue(true);
        Check(Wait(c.Vm.GoToPhotoStepAsync()) && c.Vm.SelectedStep == InspectionStep.Photo, "Kabul edince fotoğrafa geçmeli");

        // Ölçüm kaydedildikten sonra sorulmaz.
        c.Vm.SelectedStep = InspectionStep.Measure;
        c.Vm.DepthText = "1";
        c.Vm.SaveMeasurements();
        var asked = c.Dialogs.SkipRequests.Count;
        Check(!c.Vm.IsSkippingMeasurement && Wait(c.Vm.GoToPhotoStepAsync()), "Ölçüm varken doğrudan geçmeli");
        Check(c.Dialogs.SkipRequests.Count == asked, "Ölçüm varken sorulmamalı");
    });

    public static void VmRemovePhotoConfirmation() => With(c =>
    {
        AddMeasuredDent(c);
        const string photo = "1_photo_01.jpg";
        c.Store.UpdateDefect(c.Folder, 1, d =>
        {
            d.Photos.Add(photo);
            d.Markups[photo] = new PhotoMarkup();
        });
        c.Vm.Refresh(1);

        Wait(c.Vm.RemovePhotoAsync(photo));
        Check(Load(c, 1).Photos.Contains(photo), "Onaysız kaldırılmamalı");

        c.Dialogs.PhotoConfirms.Enqueue(true);
        Wait(c.Vm.RemovePhotoAsync(photo));
        Check(!Load(c, 1).Photos.Contains(photo) && !Load(c, 1).Markups.ContainsKey(photo), "Liste ve işaretlerden çıkmalı");
        Check(c.Audit.Entries.Any(e => e.EndsWith($"|defect.photo.remove|#1 {photo}")), "Denetim satırı beklenir");

        // Geçersiz ad: soru sorulmaz.
        var asked = c.Dialogs.PhotoRequests.Count;
        Wait(c.Vm.RemovePhotoAsync("../x.jpg"));
        Check(c.Dialogs.PhotoRequests.Count == asked, "Geçersiz ad için onay istenmemeli");
    });

    public static void VmReopenRequiresNote() => With(c =>
    {
        AddMeasuredDent(c);
        c.Dialogs.Decisions.Enqueue(new DecisionInput(null, null, null, null));
        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Accept)));
        Check(StateOf(c, 1) == DefectState.Closed, "Önce kapanmalı");

        Wait(c.Vm.GiveDecisionAsync(Option(c, Decision.Reopen)));
        var req = c.Dialogs.DecisionRequests.Last();
        Check(req.Decision == Decision.Reopen && req.NoteRequired, "Reopen'da not zorunlu olmalı");
    });

    public static void DialogFormRules()
    {
        var before = new MeasurementSnapshot(0.5, 1.0, null, null, null, "mavi", true);

        // Merge: boş alan öncekini korur, high metal cevabı yoksa önceki kalır.
        var merged = ReinspectForm.Merge(before, new MeasurementSnapshot(0.7, null, 2.0, null, null, null), null);
        Check(merged == new MeasurementSnapshot(0.7, 1.0, 2.0, null, null, "mavi", true), $"Merge: {merged}");
        Check(!ReinspectForm.Merge(before, NoValues, false).HighMetal, "High metal cevabı uygulanmalı");

        // AnyValue
        Check(!ReinspectForm.AnyValue(NoValues), "Boş değerde AnyValue false olmalı");
        Check(ReinspectForm.AnyValue(new MeasurementSnapshot(null, null, null, null, 5, null)), "Açı varsa AnyValue true olmalı");

        // CanSave tablosu: (sonuç, hadHighMetal, hmCevap, anyValue, yeni hata tipleri) → beklenen
        var table = new (ReinspectOutcome? O, bool Had, bool? Hm, bool Any, string?[]? Child, bool Expect)[]
        {
            (null, false, null, true, new string?[] { "x" }, false),
            (ReinspectOutcome.Conforms, true, null, false, null, true),
            (ReinspectOutcome.Same, false, null, false, null, false),
            (ReinspectOutcome.Same, false, null, true, null, true),
            (ReinspectOutcome.Same, true, null, true, null, false),
            (ReinspectOutcome.Same, true, false, false, null, true),
            (ReinspectOutcome.New, false, null, false, null, false),
            (ReinspectOutcome.New, false, null, false, new string?[] { "Scratch" }, true),
            (ReinspectOutcome.New, false, null, false, new string?[] { null }, false),
            (ReinspectOutcome.New, false, null, false, new string?[] { "Scratch", null }, false),
            (ReinspectOutcome.New, false, null, false, new string?[] { "Scratch", "Scratch" }, true),
            (ReinspectOutcome.New, false, null, false, new string?[0], false),
        };
        foreach (var t in table)
            Check(ReinspectForm.CanSave(t.O, t.Had, t.Hm, t.Any, t.Child) == t.Expect, $"CanSave {t}");

        // DeltaText
        const string hint = "Değişimi görmek için ölçüm girin.";
        Check(ReinspectForm.DeltaText(before, before, false, false) == hint, "Delta yönlendirmesi");
        Check(ReinspectForm.DeltaText(before, merged, true, false) != hint, "Ölçüm varken delta gösterilmeli");
        Check(ReinspectForm.DeltaText(before, before, false, true) != hint, "High metal cevabıyla delta gösterilmeli");

        // DecisionForm.CanSave
        var rules = new (bool NoteReq, string? Note, bool Scope, ReworkScope? S, bool Expect)[]
        {
            (false, null, false, null, true),
            (true, null, false, null, false),
            (true, "  ", false, null, false),
            (true, "neden", false, null, true),
            (false, null, true, null, false),
            (false, null, true, ReworkScope.HighMetalOnly, true),
            (true, "neden", true, null, false),
        };
        foreach (var t in rules)
            Check(DecisionForm.CanSave(t.NoteReq, t.Note, t.Scope, t.S) == t.Expect, $"DecisionForm {t}");
    }
}
