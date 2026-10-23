using System.Text.Json;

namespace MuayeneKayit.Core;

/// <summary>Bir operasyondaki tek kusurun özeti. Dashboard ağacında kullanılır.</summary>
public sealed record IndexDefect(int Id, string Type, string State, int Photos, string LastDecision = "");

/// <summary>Bir seri numarasının bir operasyondaki özeti. Dashboard bu satırları okur.</summary>
public sealed record IndexRow(
    string Part,
    string Operation,
    string Serial,
    string Status,
    int TotalDefects,
    int OpenDefects,
    string OpenSummary,
    DateTime UpdatedAt,
    IReadOnlyList<IndexDefect> Defects,
    string Inspector = "",
    DateTime? CreatedAt = null);

/// <summary>
/// Ortak alandaki oturumları tarayıp _index.json dosyasını yeniden üretir.
/// Dashboard yalnızca bu dosyayı okur; yazma muayene ekranlarından yapılır.
/// </summary>
public static class IndexBuilder
{
    public const string FileName = "_index.json";
    private const string LockName = "_index.lock";

    public static List<IndexRow> Rebuild(SessionStore store)
    {
        var rows = new List<IndexRow>();

        foreach (var partDir in Directory.EnumerateDirectories(store.Root))
        {
            if (Path.GetFileName(partDir).StartsWith('_'))
                continue;

            foreach (var opDir in Directory.EnumerateDirectories(partDir))
            foreach (var serialDir in Directory.EnumerateDirectories(opDir))
            {
                var session = store.LoadSession(serialDir);
                if (session is null)
                    continue;

                var defects = store.LoadDefects(serialDir);
                var blocking = SessionGate.BlockingDefects(defects);

                rows.Add(new IndexRow(
                    Part: Path.GetFileName(partDir),
                    Operation: Path.GetFileName(opDir),
                    Serial: Path.GetFileName(serialDir),
                    Status: session.Status.ToString(),
                    TotalDefects: defects.Count,
                    OpenDefects: blocking.Count,
                    OpenSummary: Summarize(blocking),
                    UpdatedAt: DateTime.Now,
                    Defects: defects.Select(d => new IndexDefect(
                        d.Id,
                        d.DefectType,
                        DefectStateMachine.GetState(d).ToString(),
                        d.Photos.Count,
                        LastDecisionLabel(d))).ToList(),
                    Inspector: session.Inspector,
                    CreatedAt: session.CreatedAt));
            }
        }

        using (FileLock.Acquire(Path.Combine(store.Root, LockName)))
        {
            AtomicFile.WriteAllText(
                Path.Combine(store.Root, FileName),
                JsonSerializer.Serialize(rows, SessionStore.JsonOptions));
        }

        return rows;
    }

    public static List<IndexRow> Read(SessionStore store)
    {
        var path = Path.Combine(store.Root, FileName);
        return File.Exists(path)
            ? SessionStore.ReadJson<List<IndexRow>>(path)
            : new List<IndexRow>();
    }

    /// <summary>Bir seri numarasının tüm operasyon satırları. Büyük/küçük harf duyarsız.</summary>
    public static IEnumerable<IndexRow> ForSerial(IEnumerable<IndexRow> rows, string serial) =>
        rows.Where(r => string.Equals(r.Serial, serial, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Operation, StringComparer.OrdinalIgnoreCase);

    private static string LastDecisionLabel(Defect d)
    {
        var last = DefectStateMachine.Effective(d).LastOrDefault(x => x.Source != DecisionSource.System);
        return last is null ? "" : DefectSentence.DecisionLabel(last.Decision);
    }

    private static string Summarize(IReadOnlyList<Defect> blocking) =>
        string.Join(", ", blocking.Select(d =>
            $"#{d.Id} {DefectStateMachine.GetState(d)}"));
}
