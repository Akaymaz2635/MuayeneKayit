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
            NeedsReworkScope: decision == Decision.Rework && defect.HighMetal);

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

        _store.UpdateDefect(_folder!, id, d =>
            DefectStateMachine.Apply(d, decision, source, by, note: noteText, reference: refText, targetOperation: targetText,
                reworkScope: input.Scope));

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
    /// Giderildi: başka bir şey sorulmaz. Aynı kusur: boş alan önceki değerinde kalır. Yeni hata: child tipi seçilir.
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
            defect.Unit);

        var input = await _dialogs.AskReinspectAsync(request);
        if (input is null)
            return;

        var id = defect.Id;
        var by = _user;
        var noteText = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        switch (input.Outcome)
        {
            case ReinspectOutcome.Conforms:
                _store.UpdateDefect(_folder!, id, d =>
                    DefectStateMachine.Apply(d, Decision.AfterReworkConforms, DecisionSource.Engineer, by, noteText));
                _audit.Append(by, "disposition", $"#{id} AfterReworkConforms");
                Refresh(id);
                Send($"#{id} uygun bulundu ve kapandı.", StatusKind.Success);
                break;

            case ReinspectOutcome.Same:
                var v = ReinspectForm.Merge(before, input.Entered, input.HighMetalStillThere);
                var probe = new Defect { DefectType = defect.DefectType, Depth = v.Depth, Width = v.Width, Length = v.Length, Radius = v.Radius, AngleDeg = v.AngleDeg, Color = v.Color };
                var errors = DefectValidator.Validate(probe);
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(" ", errors));

                // High metal giderildiyse ve not yazılmadıysa otomatik not: kalan ana kusurun kararı buna göre verilir.
                var sameNote = noteText ?? (hadHighMetal && !v.HighMetal ? "High metal giderildi; ana kusur devam ediyor." : null);

                // Yeni ölçümler ve karar aynı kilit altında: karar anı görüntüsü yeni değerleri taşır.
                _store.UpdateDefect(_folder!, id, d =>
                {
                    MeasurementEditor.Apply(d, v, by);
                    DefectStateMachine.Apply(d, Decision.AfterReworkSameDefect, DecisionSource.Engineer, by, sameNote);
                });
                _audit.Append(by, "disposition", $"#{id} AfterReworkSameDefect");
                Refresh(id);
                Send(hadHighMetal && !v.HighMetal
                        ? $"#{id}: high metal giderildi, ana kusur devam ediyor. Şimdi Disposition'dan kalan kusur için karar verin."
                        : $"#{id}: aynı kusur devam ediyor, yeni ölçümler kaydedildi. Şimdi Disposition'dan yeni karar verin.",
                    StatusKind.Warning);
                break;

            case ReinspectOutcome.New:
                var newType = input.ChildType ?? throw new InvalidOperationException("Yeni hata tipini seçin.");
                var child = _store.ContinueAsChild(_folder!, id, newType, DecisionSource.Engineer, by, noteText, null);
                _audit.Append(by, "disposition.continue", $"#{id} -> #{child.Id} {newType}");
                Refresh(child.Id);
                Send($"#{id} yeni kusur #{child.Id} ({newType}) olarak devam ediyor. #{child.Id} seçildi: ölçümlerini ve fotoğrafını girin.",
                    StatusKind.Warning);
                break;
        }
    }

    // ───────────────────────── Geri al, void, fotoğraf ─────────────────────────

    /// <summary>
    /// Son kararı geri alır. Karar silinmez; geçmişe "geri alındı" satırı eklenir. Rework sonrası yeni kusura
    /// devir geri alınırsa bağ çözülür ve açılan yeni kusur void edilir.
    /// </summary>
    public Task UndoLastAsync() => RunAsync(async () =>
    {
        RequireSelection();
        var defect = _store.LoadDefects(_folder!).First(d => d.Id == SelectedId);
        var last = DefectStateMachine.LastUndoable(defect)
            ?? throw new InvalidOperationException("Geri alınacak karar yok.");

        int? childId = last.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is int c ? c : null;
        var reason = await _dialogs.AskUndoReasonAsync(new UndoRequest(
            defect.Id, defect.DefectType, DefectSentence.DecisionLabel(last.Decision), last.DecidedAt, last.DecidedBy, childId));
        if (reason is null)
            return;

        var (undone, voided) = _store.UndoLastDecision(_folder!, defect.Id, _user, reason);
        _audit.Append(_user, "disposition.undo",
            $"#{defect.Id} {undone.Decision} (seq {undone.Seq}){(voided is int v ? $", child #{v} void" : "")}");
        Refresh(defect.Id);
        Send($"#{defect.Id}: '{DefectSentence.DecisionLabel(undone.Decision)}' geri alındı." +
             (voided is int vc ? $" Yeni kusur #{vc} void edildi." : "") +
             $" Durum: {DefectPresentation.StateLabel(DefectStateMachine.GetState(Current ?? defect))}.", StatusKind.Success);
    });

    /// <summary>
    /// Yanlış girilen kusuru geçersiz kılar. Gerekçe yazılmadan onaylanmaz; kayıt silinmez,
    /// ortak alanda "void" olarak kalır ve rapora girmez.
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

        _store.UpdateDefect(_folder!, id, d =>
            DefectStateMachine.Apply(d, Decision.Void, DecisionSource.Operator, by, note: note));
        _audit.Append(by, "defect.void", $"#{id} {note}");
        Refresh(id);
        Send($"#{id} void edildi. Kayıt ortak alanda kaldı, rapora girmeyecek.", StatusKind.Success);
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
