using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Muayene ekranı ViewModel testleri: gerçek SessionStore + geçici klasör, sahte denetim kaydı.
// Program.cs bu sınıfı "using static" ile kullanır.
public static class ViewModelTests
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

    private static void With(Action<Ctx> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "MuayeneKayit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SessionStore(root);
            var audit = new FakeAudit();
            var ctx = new Ctx
            {
                Vm = new InspectionViewModel(store, audit, SharedConfig.Defaults),
                Store = store,
                Audit = audit,
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
        c.Store.UpdateDefect(c.Folder, 1, d => { d.Depth = 0.1; d.Photos.Add("1_1.jpg"); });

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
}
