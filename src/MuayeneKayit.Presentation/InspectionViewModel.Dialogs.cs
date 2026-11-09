using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

// Diyalog akışları: ne sorulacağı, cevabın nasıl uygulanacağı ve zorunlu alan kuralları burada;
// pencerenin çizimi IDialogService arkasındadır.
public sealed partial class InspectionViewModel
{
    // ───────────────────────── Karar ─────────────────────────

    /// <summary>
    /// Karar seçilince onay penceresi açılır: kararın sonucu açıklanır, yalnızca o kararın alanları sorulur.
    /// Reinspect seçeneği (rework sonrası muayene) doğrudan sonuç penceresini açar.
    /// </summary>
    public Task GiveDecisionAsync(DecisionOption option) => RunAsync(async () =>
    {
        // Karar anındaki ölçüm görüntüsü kayıtlı değerlerden alınır: bekleyen form değerleri önce yazılır.
        AutoSave();
        if (Current is null)
            return;

        if (option.Decision is Decision decision)
            await DecideAsync(Current, decision);
        else if (option.IsReinspect)
            await ReinspectCoreAsync();
    });

    private async Task DecideAsync(Defect defect, Decision decision)
    {
        var label = DefectPresentation.DecisionOrder.First(x => x.Decision == decision).Label;
        var asksTarget = decision is Decision.Rework or Decision.Reinspect;
        var noteRequired = decision == Decision.Reopen;

        // 2+ aktif bölgesi olan kusurda (Void / Reopen hariç) kararın hangi bölgeler için olduğu sorulur.
        var activePhotos = DefectRegions.ActivePhotos(defect);
        var regionInfos = DecisionForm.AsksRegions(decision, activePhotos.Count) ? PhotoRegions(defect, activePhotos) : null;

        var request = new DecisionRequest(
            defect.Id,
            defect.DefectType,
            DefectPresentation.StateLabel(DefectStateMachine.GetState(defect)),
            decision,
            label,
            DecisionEffect(decision),
            TargetHeader: !asksTarget ? null
                : decision == Decision.Rework
                    ? "Rework sonrası muayene hangi operasyonda yapılacak?"
                    : "Yeniden kontrol hangi operasyonda yapılacak? (örn. CTP)",
            // Varsayılan: bu muayenenin operasyonu. Muayeneci değiştirebilir.
            TargetDefault: asksTarget ? _session?.OperationNumber ?? "" : null,
            NeedsReference: decision is Decision.Accept or Decision.SubmitMrb or Decision.MrbAccepted or Decision.MrbRejected
                or Decision.Repair or Decision.Scrap,
            NoteRequired: noteRequired,
            // High metal'li kusurda rework kapsamı sorulur: yalnızca high metal mi, kusurla birlikte mi.
            NeedsReworkScope: decision == Decision.Rework && defect.HighMetal,
            Regions: regionInfos);

        var input = await _dialogs.AskDecisionAsync(request);
        if (input is null)
            return;

        var id = defect.Id;
        var by = _user;
        var source = DefectPresentation.SourceFor(decision);
        var noteText = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var refText = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        var targetText = request.TargetHeader is null ? null
            : string.IsNullOrWhiteSpace(input.Target) ? _session!.OperationNumber : input.Target.Trim();

        // İşaretli bölgeler karar kapsamında (Continues), işaretsizler uygun (Conforms) sayılır.
        IReadOnlyDictionary<string, PhotoOutcome>? outcomes = null;
        if (regionInfos is not null)
        {
            var continuing = input.ContinuingPhotos;
            if (continuing is null || continuing.Count == 0 || !continuing.All(activePhotos.Contains))
                throw new InvalidOperationException("Karar hangi bölgeler için? En az bir bölgeyi işaretleyin.");
            outcomes = DecisionForm.RegionOutcomes(activePhotos, continuing);
        }

        _store.UpdateDefect(_folder!, id, d =>
            DefectStateMachine.Apply(d, decision, source, by, note: noteText, reference: refText, targetOperation: targetText,
                reworkScope: input.Scope, photoOutcomes: outcomes));

        _audit.Append(by, "disposition", $"#{id} {decision}");
        Refresh(id);
        Send($"#{id} için '{label}' kaydedildi. Yeni durum: {DefectPresentation.StateLabel(DefectStateMachine.GetState(Current ?? defect))}.",
            StatusKind.Success);

        // Rework aynı istasyonda hemen yapıldıysa sonucu beklemeden girilebilir; değilse parça rework'e gider.
        if (asksTarget && await _dialogs.AskReinspectNowAsync(decision, id))
            await ReinspectCoreAsync();
    }

    /// <summary>Kararın ne yapacağı. Metinler admin panelinden düzenlenir (_config\decision_texts.json).</summary>
    public string DecisionEffect(Decision decision) =>
        SharedConfig.LoadDecisionTexts(_store.Root).GetValueOrDefault(decision, "");

    // ───────────────────────── Rework sonrası muayene ─────────────────────────

    /// <summary>
    /// Rework (ya da reinspect) sonrası muayene: önce sonuç seçilir, sonra yalnızca o sonucun alanları açılır.
    /// Giderildi: başka bir şey sorulmaz. Aynı kusur: boş alan önceki değerinde kalır. Yeni hata: bir ya da daha fazla
    /// yeni kusurun tipi seçilir. Kusurun 2+ aktif bölgesi (fotoğrafı) varsa her bölge için ayrı sonuç sorulur;
    /// genel sonuç bölgelerden otomatik çıkar (devam eden yoksa kapanır ya da devredilir, varsa açık kalır).
    /// </summary>
    public Task ReinspectAsync() => RunAsync(ReinspectCoreAsync);

    private async Task ReinspectCoreAsync()
    {
        RequireSelection();
        var defect = _store.LoadDefects(_folder!).First(d => d.Id == SelectedId);
        if (DefectStateMachine.GetState(defect) != DefectState.AwaitingReinspect)
            throw new InvalidOperationException("Bu kusur rework sonrası muayene beklemiyor.");

        var effective = DefectStateMachine.Effective(defect);
        var sent = effective.LastOrDefault(x => x.Decision is Decision.Rework or Decision.Reinspect);
        var isRework = sent?.Decision == Decision.Rework;
        var before = sent?.Snapshot ?? MeasurementEditor.Snapshot(defect);
        var template = _types.FirstOrDefault(t => string.Equals(t.Name, defect.DefectType, StringComparison.OrdinalIgnoreCase));
        var hadHighMetal = before.HighMetal;

        // Şablondaki alanlar ve eski değeri olanlar (renk ayrıca sorulur).
        var measured = new (MeasureField Field, double? Old)[]
        {
            (MeasureField.Depth, before.Depth),
            (MeasureField.Width, before.Width),
            (MeasureField.Length, before.Length),
            (MeasureField.Radius, before.Radius),
            (MeasureField.Angle, before.AngleDeg),
        };
        var fields = measured
            .Where(x => !(template is not null && !template.Fields.Contains(x.Field) && x.Old is null))
            .Select(x => x.Field)
            .ToList();

        // Yalnızca aktif bölgeler sorulur; 2+ ise her biri için ayrı sonuç.
        var activePhotos = DefectRegions.ActivePhotos(defect);
        var request = new ReinspectRequest(
            defect.Id,
            defect.DefectType,
            isRework,
            sent?.ReworkScope == ReworkScope.HighMetalOnly,
            sent?.TargetOperation,
            before,
            fields,
            template is null || template.Fields.Contains(MeasureField.Color) || before.Color is not null,
            TypeNames,
            defect.Unit,
            activePhotos.Count >= 2 ? PhotoRegions(defect, activePhotos) : null,
            CustomFieldsFor(template, before.Custom));

        var input = await _dialogs.AskReinspectAsync(request);
        if (input is null)
            return;

        var id = defect.Id;
        var by = _user;
        var result = ReinspectForm.ToResult(defect, before, input);

        if (result.Measurements is MeasurementSnapshot v)
        {
            var probe = new Defect { DefectType = defect.DefectType, Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color, Custom = v.Custom?.ToList() };
            var errors = DefectValidator.Validate(probe);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(" ", errors));
        }

        // Ölçümler, karar, bölge sonuçları ve yeni kusurlar tek kilit altında yazılır.
        var (_, children) = _store.ApplyReinspect(_folder!, id, result, by);
        _audit.Append(by, "disposition", $"#{id} {result.Decision}");
        if (children.Count > 0)
            _audit.Append(by, "disposition.continue", $"#{id} -> {string.Join(", ", children.Select(c => $"#{c.Id} {c.DefectType}"))}");

        var openedText = children.Count == 0
            ? ""
            : $" Yeni kusur: {DefectPresentation.IdList(children.Select(c => c.Id))} (ölçümlerini ve fotoğraflarını girin).";

        switch (result.Decision)
        {
            case Decision.AfterReworkConforms:
                Refresh(id);
                Send($"#{id} uygun bulundu ve kapandı.", StatusKind.Success);
                break;

            case Decision.AfterReworkSameDefect:
                var highMetalGone = hadHighMetal && result.Measurements is { HighMetal: false };
                Refresh(id);
                Send((highMetalGone
                        ? $"#{id}: high metal giderildi, ana kusur devam ediyor. Şimdi Disposition'dan kalan kusur için karar verin."
                        : $"#{id}: aynı kusur devam ediyor, yeni ölçümler kaydedildi. Şimdi Disposition'dan yeni karar verin.") + openedText,
                    StatusKind.Warning);
                break;

            default:
                Refresh(children[0].Id);
                Send(children.Count == 1
                        ? $"#{id} yeni kusur #{children[0].Id} ({children[0].DefectType}) olarak devam ediyor. #{children[0].Id} seçildi: ölçümlerini ve fotoğrafını girin."
                        : $"#{id} yeni kusurlar olarak devam ediyor: {string.Join(", ", children.Select(c => $"#{c.Id} ({c.DefectType})"))}. " +
                          $"#{children[0].Id} seçildi: her birinin ölçümlerini ve fotoğrafını girin.",
                    StatusKind.Warning);
                break;
        }
    }

    /// <summary>Penceredeki bölge satırları: etiket (3.1) ve küçük resim (varsa temiz kopya).</summary>
    private IReadOnlyList<PhotoRegion> PhotoRegions(Defect defect, IReadOnlyList<string> photos)
    {
        var directory = Path.Combine(_folder!, "defects");
        return photos
            .Select(p => new PhotoRegion(p, DefectRegions.Label(defect, p), Path.Combine(directory, ShownFile(defect, p))))
            .ToList();
    }

    // ───────────────────────── Geri al, void, fotoğraf ─────────────────────────

    /// <summary>
    /// Son kararı geri alır. Karar silinmez; geçmişe "geri alındı" satırı eklenir. Rework sonrası yeni kusurlara
    /// devir ya da yeni kusur açılması geri alınırsa açılan yeni kusurların hepsi void edilir; bölge durumları geri gelir.
    /// </summary>
    public Task UndoLastAsync() => RunAsync(async () =>
    {
        RequireSelection();
        var all = _store.LoadDefects(_folder!);
        var defect = all.First(d => d.Id == SelectedId);
        var last = DefectStateMachine.LastUndoable(defect)
            ?? throw new InvalidOperationException("Geri alınacak karar yok.");

        // Kararla açılan ve henüz void olmayan yeni kusurlar (geri alınınca hepsi void edilir).
        var children = DefectRegions.ChildIdsOf(defect, last)
            .Where(id => all.FirstOrDefault(c => c.Id == id) is { } c && DefectStateMachine.GetState(c) != DefectState.Voided)
            .ToList();
        var reason = await _dialogs.AskUndoReasonAsync(new UndoRequest(
            defect.Id, defect.DefectType, DefectSentence.DecisionLabel(last.Decision), last.DecidedAt, last.DecidedBy, children));
        if (reason is null)
            return;

        var (undone, voided) = _store.UndoLastDecision(_folder!, defect.Id, _user, reason);
        _audit.Append(_user, "disposition.undo",
            $"#{defect.Id} {undone.Decision} (seq {undone.Seq}){(voided.Count > 0 ? $", child {DefectPresentation.IdList(voided)} void" : "")}");
        Refresh(defect.Id);
        Send($"#{defect.Id}: '{DefectSentence.DecisionLabel(undone.Decision)}' geri alındı." +
             (voided.Count > 0 ? $" Yeni kusur {DefectPresentation.IdList(voided)} void edildi." : "") +
             $" Durum: {DefectPresentation.StateLabel(DefectStateMachine.GetState(Current ?? defect))}.", StatusKind.Success);
    });

    /// <summary>
    /// Yanlış girilen kusuru geçersiz kılar. Gerekçe yazılmadan onaylanmaz; kayıt silinmez,
    /// ortak alanda "void" olarak kalır ve rapora girmez. Rework sonrası açılan son yeni kusur void edilirse ana kusurun
    /// "yeni hata" kararı sistem tarafından geri alınır, ana kusur tekrar rework sonrası muayene bekler
    /// (tek kilit altında, SessionStore.VoidDefect).
    /// </summary>
    public Task VoidAsync() => RunAsync(async () =>
    {
        RequireSelection();
        var defect = Current!;

        var reason = await _dialogs.AskVoidReasonAsync(defect.Id, defect.DefectType);
        if (reason is null)
            return;

        var id = defect.Id;
        var by = _user;
        var note = reason.Trim();

        var reopened = _store.VoidDefect(_folder!, id, by, note);
        _audit.Append(by, "defect.void", $"#{id} {note}");
        if (reopened is int parent)
            _audit.Append(by, "disposition.undo", $"#{parent} AfterReworkNewDefect (sistem: yeni kusur #{id} void edildi)");
        Refresh(id);
        Send($"#{id} void edildi. Kayıt ortak alanda kaldı, rapora girmeyecek." +
             (reopened is int p ? $" #{p} yeniden rework sonrası muayene bekliyor." : ""), StatusKind.Success);
    });

    /// <summary>
    /// Fotoğrafı kusurdan kaldırır (onay ile). Dosya ortak alanda kalır, yalnızca kusurun listesinden ve
    /// rapordan çıkar; işlem denetim kaydına yazılır.
    /// </summary>
    public Task RemovePhotoAsync(string photo) => RunAsync(async () =>
    {
        RequireSelection();
        if (!PhotoMarkupValidator.IsPhotoFileName(photo))
            return;

        var id = SelectedId!.Value;
        if (!await _dialogs.ConfirmRemovePhotoAsync(id, photo))
            return;

        _store.UpdateDefect(_folder!, id, d =>
        {
            d.Photos.Remove(photo);
            d.Markups.Remove(photo);
        });
        _audit.Append(_user, "defect.photo.remove", $"#{id} {photo}");
        Refresh(id);
        Send($"{photo} kaldırıldı.", StatusKind.Success);
    });

    /// <summary><see cref="Run"/>'ın async karşılığı: beklenen hataları yakalar ve bildirir.</summary>
    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Send(ex.Message, StatusKind.Error);
        }
    }
}
