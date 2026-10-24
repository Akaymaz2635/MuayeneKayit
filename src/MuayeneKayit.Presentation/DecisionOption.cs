using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Disposition listesindeki bir seçenek. Decision boşsa "rework sonrası muayene sonucu" seçeneğidir.</summary>
public sealed record DecisionOption(string Label, Decision? Decision, bool IsReinspect)
{
    /// <summary>Ekran okuyucu ve UI testleri seçeneği etiketiyle bulur.</summary>
    public override string ToString() => Label;
}
