namespace MuayeneKayit.Core;

/// <summary>
/// Muayene kapanışı kontrolü. Açık kusuru olan muayene Completed yapılamaz.
/// Superseded kusur, void olmayan tüm child'ları çözüldüğünde çözülmüş sayılır (void child sayılmaz).
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
                // Döngüsel zincire karşı koruma: aynı zincirde kusura ikinci kez gelinirse çözülmemiş say.
                if (!visited.Add(defect.Id))
                    return false;
                try
                {
                    // Hiç yeni kusur kaydı yoksa çözülmüş sayılmaz. Void edilen yeni kusurlar sayılmaz;
                    // geri kalan hepsi çözülünce parent çözülür. (Yeni akışta son aktif yeni kusur void edilince parent'ın kararı
                    // sistem tarafından geri alınır; hepsi void kalmış Superseded parent yalnızca eski kayıtlarda görülür ve
                    // eskiden olduğu gibi çözülmüş sayılır, aksi halde muayene hiç kapatılamazdı.)
                    var childIds = DefectRegions.ChildIds(defect);
                    if (childIds.Count == 0)
                        return false;

                    foreach (var childId in childIds)
                    {
                        if (!byId.TryGetValue(childId, out var child))
                            return false;
                        if (DefectStateMachine.GetState(child) == DefectState.Voided)
                            continue;
                        if (!IsResolved(child, byId, visited))
                            return false;
                    }
                    return true;
                }
                finally
                {
                    visited.Remove(defect.Id);
                }

            default:
                return false;
        }
    }
}
