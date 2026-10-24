namespace MuayeneKayit.Presentation;

/// <summary>Durum rengi. WinUI fırçası View'da seçilir; ViewModel yalnızca tonu bilir.</summary>
public enum Tone
{
    Critical,
    Caution,
    Attention,
    Success,
    Neutral,
}

/// <summary>Seçili kusurun adım sekmeleri.</summary>
public enum InspectionStep
{
    Measure,
    Photo,
    Decision,
    History,
}

/// <summary>Durum çubuğu bildiriminin türü.</summary>
public enum StatusKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Muayene seçimindeki öneri kutusu: hangi alanın önerileri isteniyor.</summary>
public enum SuggestLevel
{
    Part,
    Operation,
    Serial,
}
