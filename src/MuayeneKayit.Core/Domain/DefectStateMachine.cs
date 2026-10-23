namespace MuayeneKayit.Core;

/// <summary>
/// Kusur durumlarını ve geçişlerini yönetir. Durum, kayıtlı kararlardan türetilir, saklanmaz.
/// Karar geçmişi yalnızca eklenir: hiçbir karar silinmez, yeniden açmalar da ayrı kayıt olur.
/// </summary>
public static class DefectStateMachine
{
    private static readonly Dictionary<DefectState, Decision[]> AllowedByState = new()
    {
        [DefectState.Open] = new[]
        {
            Decision.Accept, Decision.Rework, Decision.Reinspect, Decision.Repair,
            Decision.Scrap, Decision.SubmitMrb, Decision.ReturnToVendor, Decision.Void,
        },
        [DefectState.Repairing] = new[]
        {
            Decision.Accept, Decision.Scrap, Decision.Reinspect, Decision.SubmitMrb,
        },
        [DefectState.AwaitingReinspect] = new[]
        {
            Decision.AfterReworkConforms, Decision.AfterReworkSameDefect, Decision.AfterReworkNewDefect,
            Decision.Repair, Decision.SubmitMrb, Decision.Scrap,
        },
        [DefectState.WaitingVendor] = new[]
        {
            Decision.Reinspect, Decision.Scrap,
        },
        [DefectState.MrbPending] = new[]
        {
            Decision.MrbAccepted, Decision.MrbRejected,
        },
        [DefectState.Closed] = new[]
        {
            Decision.Reopen,
        },
    };

    /// <summary>Açık sayılan durumlar. Muayene kapanışını engeller.</summary>
    public static readonly DefectState[] OpenStates =
    {
        DefectState.Open,
        DefectState.Reworking,
        DefectState.Repairing,
        DefectState.AwaitingReinspect,
        DefectState.WaitingVendor,
        DefectState.MrbPending,
    };

    /// <summary>
    /// Geçerli kararlar: Undo kayıtları ve geri alınmış kararlar (onlara bağlı otomatik System kayıtları dahil)
    /// çıkarılır. Durum ve raporlardaki "son karar" bu listeden hesaplanır.
    /// </summary>
    public static IReadOnlyList<Disposition> Effective(Defect defect)
    {
        var undone = UndoneSeqs(defect);
        return defect.Dispositions
            .Where(d => d.Decision != Decision.Undo && !undone.Contains(d.Seq))
            .ToList();
    }

    /// <summary>Geri alınmış kararların sıra numaraları (otomatik eklenen System kayıtları dahil).</summary>
    public static HashSet<int> UndoneSeqs(Defect defect)
    {
        var undone = new HashSet<int>();
        foreach (var undo in defect.Dispositions.Where(d => d.Decision == Decision.Undo && d.UndoesSeq is not null))
        {
            var target = undo.UndoesSeq!.Value;
            undone.Add(target);

            // MRB reddiyle otomatik yazılan hurda gibi, hemen ardından gelen System kayıtları da geri alınır.
            foreach (var next in defect.Dispositions.Where(d => d.Seq > target).OrderBy(d => d.Seq))
            {
                if (next.Source != DecisionSource.System)
                    break;
                undone.Add(next.Seq);
            }
        }
        return undone;
    }

    /// <summary>Geri alınabilecek son karar (System kaydı değil). Yoksa null.</summary>
    public static Disposition? LastUndoable(Defect defect) =>
        Effective(defect).LastOrDefault(d => d.Source != DecisionSource.System && d.Decision != Decision.Void);

    /// <summary>
    /// Son kararı geri alır: kayıt silinmez, "Undo" satırı eklenir ve durum önceki karara döner.
    /// Gerekçe zorunludur. Child'a devredilen kararın geri alınması SessionStore.UndoLastDecision ile yapılır.
    /// </summary>
    public static Disposition Undo(Defect defect, string decidedBy, string? note, DateTime? at = null)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Geri almak için gerekçe yazın (örn. yanlış karar seçildi).");

        var target = LastUndoable(defect)
            ?? throw new InvalidOperationException("Geri alınacak karar yok.");

        if (target.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is not null)
            throw new InvalidOperationException("Bu karar yeni bir kusura bağlı; geri alma child kusurla birlikte yapılmalı.");

        defect.Dispositions.Add(new Disposition
        {
            Seq = defect.Dispositions.Count + 1,
            Decision = Decision.Undo,
            Source = target.Source,
            DecidedBy = decidedBy,
            Note = note.Trim(),
            UndoesSeq = target.Seq,
            DecidedAt = at ?? DateTime.Now,
        });
        return target;
    }

    public static DefectState GetState(Defect defect)
    {
        if (defect.SupersededById is not null)
            return DefectState.Superseded;

        var effective = Effective(defect);
        if (effective.Count == 0)
            return DefectState.Open;

        return effective[^1].Decision switch
        {
            Decision.Void => DefectState.Voided,
            Decision.Accept or Decision.Scrap or Decision.MrbAccepted or Decision.MrbRejected
                or Decision.AfterReworkConforms => DefectState.Closed,
            Decision.Rework => DefectState.AwaitingReinspect,
            Decision.AfterReworkSameDefect => DefectState.Open,
            Decision.Repair => DefectState.Repairing,
            Decision.Reinspect => DefectState.AwaitingReinspect,
            Decision.SubmitMrb => DefectState.MrbPending,
            Decision.ReturnToVendor => DefectState.WaitingVendor,
            Decision.Reopen => DefectState.Open,
            _ => DefectState.Open,
        };
    }

    public static bool IsOpen(DefectState state) => OpenStates.Contains(state);

    /// <summary>Verilen karar bu durumda ve bu kaynaktan geçerli mi?</summary>
    public static (bool Ok, string? Reason) Check(Defect defect, Decision decision, DecisionSource source)
    {
        var state = GetState(defect);

        if (!AllowedByState.TryGetValue(state, out var allowed) || !allowed.Contains(decision))
            return (false, $"'{Label(state)}' durumunda '{decision}' kararı verilemez.");

        if (!SourcesFor(decision).Contains(source))
            return (false, $"'{decision}' kararını '{source}' veremez.");

        if (decision == Decision.Void && Effective(defect).Count > 0)
            return (false, "Karar geçmişi olan kusur void edilemez. Yanlış giriş yalnızca karar verilmeden önce void edilir.");

        if (decision == Decision.Reopen && !IsReopenable(defect))
            return (false, "Bu kusur kesin olarak kapandı (hurda ya da MRB reddi), yeniden açılamaz.");

        return (true, null);
    }

    /// <summary>
    /// Kararı kusura ekler. Rework ve rework sonrası kararlarda ölçüm anlık görüntüsü alınır.
    /// Yeniden açmada not zorunludur. Geçersiz geçişte InvalidOperationException fırlatır.
    /// </summary>
    public static void Apply(
        Defect defect,
        Decision decision,
        DecisionSource source,
        string decidedBy,
        string? note = null,
        string? reference = null,
        string? targetOperation = null,
        DateTime? decidedAt = null)
    {
        var (ok, reason) = Check(defect, decision, source);
        if (!ok)
            throw new InvalidOperationException(reason);

        if (decision == Decision.Rework && string.IsNullOrWhiteSpace(targetOperation))
            throw new InvalidOperationException("Rework için hedef operasyonu girin (örn. OP40).");

        if (decision == Decision.Void && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Void için gerekçe notu zorunludur (örn. yanlış hata tipi girildi).");

        if (decision == Decision.Reopen && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Yeniden açmak için bir gerekçe notu zorunludur.");

        Add(defect, decision, source, decidedBy, note, reference, targetOperation, decidedAt);

        if (decision == Decision.MrbRejected)
        {
            Add(defect, Decision.Scrap, DecisionSource.System, decidedBy,
                "MRB reddi, otomatik hurda", reference, null, decidedAt);
        }
    }

    /// <summary>
    /// Rework sonrası yeniden muayene yeni bir kusur bulduğunda parent'ı child'a bağlar.
    /// Parent, AfterReworkNewDefect kararını zaten almış ya da AwaitingReinspect durumunda olmalıdır.
    /// </summary>
    public static void Supersede(Defect parent, Defect child)
    {
        var effective = Effective(parent);
        var lastIsContinues = effective.Count > 0
            && effective[^1].Decision == Decision.AfterReworkNewDefect;

        if (GetState(parent) != DefectState.AwaitingReinspect && !(lastIsContinues && parent.SupersededById is null))
            throw new InvalidOperationException(
                $"Kusur #{parent.Id} rework sonrası yeniden muayene bekleme durumunda değil.");

        parent.SupersededById = child.Id;
        child.SupersedesId = parent.Id;
    }

    /// <summary>Kapalı bir kusur yeniden açılabilir mi? Hurda ve MRB reddi kesindir.</summary>
    public static bool IsReopenable(Defect defect)
    {
        var last = Effective(defect).LastOrDefault(d => d.Source != DecisionSource.System);
        if (last is null)
            return false;

        return last.Decision is not (Decision.Scrap or Decision.MrbRejected);
    }

    private static DecisionSource[] SourcesFor(Decision decision) => decision switch
    {
        Decision.Void => new[] { DecisionSource.Operator },
        Decision.MrbAccepted or Decision.MrbRejected => new[] { DecisionSource.Mrb },
        Decision.Reinspect or Decision.AfterReworkConforms or Decision.AfterReworkSameDefect or Decision.AfterReworkNewDefect
            => new[] { DecisionSource.Engineer, DecisionSource.Ctp },
        _ => new[] { DecisionSource.Engineer },
    };

    private static string Label(DefectState state) => state switch
    {
        DefectState.Open => "Açık",
        DefectState.Reworking => "Rework",
        DefectState.Repairing => "Repair",
        DefectState.AwaitingReinspect => "Rework sonrası muayene bekliyor",
        DefectState.WaitingVendor => "Tedarikçide",
        DefectState.MrbPending => "MRB bekliyor",
        DefectState.Closed => "Kapandı",
        DefectState.Voided => "Void",
        DefectState.Superseded => "Devredildi",
        _ => state.ToString(),
    };

    private static void Add(
        Defect defect,
        Decision decision,
        DecisionSource source,
        string decidedBy,
        string? note,
        string? reference,
        string? targetOperation,
        DateTime? decidedAt)
    {
        var snapshot = decision is Decision.Rework or Decision.AfterReworkConforms
            or Decision.AfterReworkSameDefect or Decision.AfterReworkNewDefect
            ? new MeasurementSnapshot(defect.Depth, defect.Width, defect.Length, defect.Radius, defect.AngleDeg, defect.Color)
            : null;

        defect.Dispositions.Add(new Disposition
        {
            Seq = defect.Dispositions.Count + 1,
            Decision = decision,
            Source = source,
            DecidedBy = decidedBy,
            Note = note,
            Reference = reference,
            TargetOperation = targetOperation,
            DecidedAt = decidedAt ?? DateTime.Now,
            Snapshot = snapshot,
        });
    }
}
