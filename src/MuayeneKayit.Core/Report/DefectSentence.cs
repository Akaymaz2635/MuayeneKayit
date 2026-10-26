using System.Globalization;
using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Kağıt raporda kusurun tek cümlelik özetini üretir. Örnek:
/// "Defect 3: There is a Dent on the part: D 0.12 mm, W 2.4 mm." (inch kusurda: "D .123 in")
/// "Disposition: Rework. Continues as Defect 5."
/// </summary>
public static class DefectSentence
{
    public static string Describe(Defect defect) => Describe(defect, null);

    /// <summary>
    /// <paramref name="all"/>: oturumun kusurları. Verilirse void edilen yeni kusurlar anılmaz ve child'ın bulunduğu bölge
    /// etiketi yazılır; verilmezse yeni kusurların durumu bilinmediğinden hepsi anılır.
    /// </summary>
    public static string Describe(Defect defect, IReadOnlyList<Defect>? all)
    {
        var sb = new StringBuilder();
        sb.Append($"Defect {defect.Id}: ").Append(Statement(defect));

        if (!string.IsNullOrWhiteSpace(defect.SpecName))
            sb.Append($" Spec: {defect.SpecName}.");
        if (!string.IsNullOrWhiteSpace(defect.AcceptanceReference))
            sb.Append($" Acceptance: {defect.AcceptanceReference}.");

        sb.Append('\n');
        sb.Append($"Disposition: {LastDecisionLabel(defect)}.");

        var children = DefectRegions.ActiveChildIds(defect, all);
        if (children.Count > 0)
        {
            var list = string.Join(", ", children);
            if (DefectStateMachine.GetState(defect) == DefectState.Superseded)
                sb.Append(children.Count == 1 ? $" Continues as Defect {list}." : $" Continues as Defects {list}.");
            else
                sb.Append(children.Count == 1 ? $" New defect found after rework: {list}." : $" New defects found after rework: {list}.");
        }

        if (defect.SupersedesId is int prevId)
        {
            var region = DefectRegions.FoundAtLabel(defect, all);
            sb.Append(region.Length == 0 ? $" Continues from Defect {prevId}." : $" Continues from Defect {prevId} (region {region}).");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Açıklama cümlesi (önek ve disposition olmadan): "There is a Dent with high metal on parts 1–5 and 8: D 0.5 mm, W 2.4 mm, L 6 mm, Ø 3 mm, A 15°, color gray."
    /// Ekranda ve raporun "Uygunsuzluk açıklaması" bölümünde aynı cümle kullanılır. Ölçü yoksa iki nokta ve liste yazılmaz.
    /// </summary>
    public static string Statement(Defect defect)
    {
        var type = defect.DefectType ?? "";
        var sb = new StringBuilder();
        sb.Append("There is ").Append(Article(type)).Append(' ').Append(type);
        if (defect.HighMetal)
            sb.Append(" with high metal");
        sb.Append(' ').Append(PartList.Describe(PartList.TryParse(defect.AffectedParts)));

        var measurements = new List<string>();
        AddLength(measurements, "D", defect.Depth, defect.Unit);
        AddLength(measurements, "W", defect.Width, defect.Unit);
        AddLength(measurements, "L", defect.Length, defect.Unit);
        // Alan adı Radius kalır (JSON uyumu); kullanıcı metninde Çap = Diameter (Ø).
        AddLength(measurements, "Ø", defect.Radius, defect.Unit);
        if (defect.AngleDeg is double angle)
            measurements.Add($"A {Num(angle)}°");
        if (!string.IsNullOrWhiteSpace(defect.Color))
            measurements.Add($"color {defect.Color.Trim()}");

        if (measurements.Count > 0)
            sb.Append(": ").Append(string.Join(", ", measurements));
        sb.Append('.');
        return sb.ToString();
    }

    /// <summary>Belirsiz artikel: sesli harfle başlayan tipte "an", değilse "a". Tip adı değiştirilmez.</summary>
    private static string Article(string type)
    {
        var first = type.TrimStart().FirstOrDefault();
        return first != default && "aeiouAEIOU".IndexOf(first) >= 0 ? "an" : "a";
    }

    /// <summary>"Dent" ya da yan kusurla "Dent with high metal".</summary>
    public static string TypeText(Defect defect) =>
        defect.HighMetal ? $"{defect.DefectType} with high metal" : defect.DefectType;

    /// <summary>Karar etiketi; rework kapsamı yalnızca high metal ise belirtilir.</summary>
    public static string DecisionLabel(Disposition d) =>
        d.Decision == Decision.Rework && d.ReworkScope == ReworkScope.HighMetalOnly
            ? "Rework / Reinspect (high metal only)"
            : DecisionLabel(d.Decision);

    private static void AddLength(List<string> target, string label, double? value, LengthUnit unit)
    {
        if (value is double v)
            target.Add($"{label} {Units.Format(v, unit)} {Units.Suffix(unit)}");
    }

    private static string Num(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Sistem tarafından eklenen kayıtlar (örn. MRB reddi sonrası hurda) atlanır.</summary>
    private static string LastDecisionLabel(Defect defect)
    {
        var last = DefectStateMachine.Effective(defect).LastOrDefault(d => d.Source != DecisionSource.System);
        if (last is null)
            return "Pending";

        return DecisionLabel(last);
    }

    public static string DecisionLabel(Decision decision) => decision switch
    {
            Decision.Accept => "Accept",
            Decision.Rework => "Rework / Reinspect",
            Decision.Reinspect => "Reinspect",
            Decision.Repair => "Repair",
            Decision.Scrap => "Scrap",
            Decision.SubmitMrb => "Submit MRB (pending)",
            Decision.ReturnToVendor => "Return to vendor",
            Decision.Void => "Void",
            Decision.MrbAccepted => "MRB accepted (accept as is)",
            Decision.MrbRejected => "MRB rejected",
            Decision.AfterReworkConforms => "After rework conforms",
            Decision.AfterReworkSameDefect => "After rework: same defect continues",
            Decision.AfterReworkNewDefect => "After rework: new defect found",
            Decision.Reopen => "Reopened",
            Decision.Undo => "Undo (decision withdrawn)",
            _ => decision.ToString(),
    };
}
