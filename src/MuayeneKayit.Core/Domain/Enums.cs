namespace MuayeneKayit.Core;

/// <summary>Disposition kararları. Mühendis, operatör, CTP ya da MRB tarafından verilir.</summary>
public enum Decision
{
    Accept,
    Rework,
    Reinspect,
    Repair,
    Scrap,
    SubmitMrb,
    ReturnToVendor,
    Void,
    MrbAccepted,
    MrbRejected,
    AfterReworkConforms,
    AfterReworkSameDefect,
    AfterReworkNewDefect,
    Reopen,

    /// <summary>Son kararı geri alır (yanlış tıklama). Kayıt silinmez; geri alınan karar geçmişte işaretli kalır.</summary>
    Undo,
}

/// <summary>Kararı kimin verdiği. Yetki kontrolü bunun üzerinden yapılır.</summary>
public enum DecisionSource
{
    Engineer,
    Operator,
    Ctp,
    Mrb,
    System,
}

/// <summary>Bir kusurun türetilmiş durumu. Dispositions listesinden hesaplanır, saklanmaz.</summary>
public enum DefectState
{
    Open,
    Reworking,
    Repairing,
    AwaitingReinspect,
    WaitingVendor,
    MrbPending,
    Closed,
    Voided,
    Superseded,
}

/// <summary>Rework'ün kapsamı: kusurla birlikte high metal mi, yalnızca high metal mi giderilecek.</summary>
public enum ReworkScope
{
    DefectAndHighMetal,
    HighMetalOnly,
}

/// <summary>Ölçü birimi. Kusur oluşturulurken o anki ayar kusura yazılır; sonradan dönüştürülmez.</summary>
public enum LengthUnit
{
    Mm,
    Inch,
}

public enum SessionStatus
{
    Open,
    Completed,
}
