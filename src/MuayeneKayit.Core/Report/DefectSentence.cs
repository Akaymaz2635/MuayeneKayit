using System.Globalization;
using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Kağıt raporda kusurun tek cümlelik özetini üretir. Örnek:
/// "Defect 3: There is Dent on part with Depth: 0.12 mm, Width: 2.4 mm."
/// "Disposition: Rework. Continues as Defect 5."
/// </summary>
public static class DefectSentence
{
    public static string Describe(Defect defect)
    {
        var sb = new StringBuilder();
        sb.Append($"Defect {defect.Id}: There is {TypeText(defect)} on part");

        var measurements = new List<string>();
        AddMm(measurements, "Depth", defect.Depth);
        AddMm(measurements, "Width", defect.Width);
        AddMm(measurements, "Length", defect.Length);
        AddMm(measurements, "Radius", defect.Radius);
        if (defect.AngleDeg is double angle)
            measurements.Add($"Angle: {Num(angle)}°");
        if (!string.IsNullOrWhiteSpace(defect.Color))
            measurements.Add($"Color: {defect.Color}");

        if (measurements.Count > 0)
            sb.Append(" with ").Append(string.Join(", ", measurements));
        sb.Append('.');

        if (!string.IsNullOrWhiteSpace(defect.SpecName))
            sb.Append($" Spec: {defect.SpecName}.");
        if (!string.IsNullOrWhiteSpace(defect.AcceptanceReference))
            sb.Append($" Acceptance: {defect.AcceptanceReference}.");

        sb.Append('\n');
        sb.Append($"Disposition: {LastDecisionLabel(defect)}.");

        if (defect.SupersededById is int nextId)
            sb.Append($" Continues as Defect {nextId}.");
        if (defect.SupersedesId is int prevId)
            sb.Append($" Continues from Defect {prevId}.");

        return sb.ToString();
    }

    /// <summary>"Dent" ya da yan kusurla "Dent with high metal".</summary>
    public static string TypeText(Defect defect) =>
        defect.HighMetal ? $"{defect.DefectType} with high metal" : defect.DefectType;

    /// <summary>Karar etiketi; rework kapsamı yalnızca high metal ise belirtilir.</summary>
    public static string DecisionLabel(Disposition d) =>
        d.Decision == Decision.Rework && d.ReworkScope == ReworkScope.HighMetalOnly
            ? "Rework / Reinspect (high metal only)"
            : DecisionLabel(d.Decision);

    private static void AddMm(List<string> target, string label, double? value)
    {
        if (value is double v)
            target.Add($"{label}: {Num(v)} mm");
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
