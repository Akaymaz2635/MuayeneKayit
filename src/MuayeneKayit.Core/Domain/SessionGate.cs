namespace MuayeneKayit.Core;

/// <summary>
/// Muayene kapanışı kontrolü. Açık kusuru olan muayene Completed yapılamaz.
/// Superseded kusur, child'ı çözüldüğünde çözülmüş sayılır.
/// </summary>
public static class SessionGate
{
    public static IReadOnlyList<Defect> BlockingDefects(IReadOnlyList<Defect> defects)
    {
        var byId = defects.ToDictionary(d => d.Id);
        return defects
            .Where(d => !IsResolved(d, byId, new HashSet<int>()))
            .ToList();
    }

    public static bool CanComplete(IReadOnlyList<Defect> defects) =>
        BlockingDefects(defects).Count == 0;

    private static bool IsResolved(Defect defect, Dictionary<int, Defect> byId, HashSet<int> visited)
    {
        var state = DefectStateMachine.GetState(defect);

        switch (state)
        {
            case DefectState.Closed:
            case DefectState.Voided:
                return true;

            case DefectState.Superseded:
                // Döngüsel zincire karşı koruma: aynı kusura ikinci kez gelinirse çözülmemiş say.
                if (!visited.Add(defect.Id))
                    return false;
                return defect.SupersededById is int childId
                    && byId.TryGetValue(childId, out var child)
                    && IsResolved(child, byId, visited);

            default:
                return false;
        }
    }
}
