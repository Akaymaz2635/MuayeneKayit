using System.Text.Json;

namespace MuayeneKayit.Core;

/// <summary>Bir operasyondaki tek kusurun özeti. Dashboard ağacında kullanılır.</summary>
public sealed record IndexDefect(int Id, string Type, string State, int Photos, string LastDecision = "", string AffectedParts = "");

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

    public static List<IndexRow> Rebuild(SessionStore store) => Rebuild(store, null);

    /// <summary>
    /// Kökü tarar ve indeksi yeniden üretir. Okunamayan oturum atlanır ve <paramref name="problems"/> listesine yazılır
    /// (tek bozuk kayıt diğerlerini engellemez). Kök içindeki yeniden çözümleme noktaları (junction) izlenmez.
    /// </summary>
    public static List<IndexRow> Rebuild(SessionStore store, List<string>? problems)
    {
        var rows = new List<IndexRow>();

        foreach (var partDir in SubDirectories(store.Root))
        {
            if (Path.GetFileName(partDir).StartsWith('_'))
                continue;

            foreach (var opDir in SubDirectories(partDir))
            foreach (var serialDir in SubDirectories(opDir))
            {
                Session? session;
                List<Defect> defects;
                try
                {
                    session = store.LoadSession(serialDir);
                    if (session is null)
                        continue;
                    defects = store.LoadDefects(serialDir);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    problems?.Add($"{Path.GetRelativePath(store.Root, serialDir)}: {ex.Message}");
                    continue;
                }

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
                        LastDecisionLabel(d),
                        d.AffectedParts ?? "")).ToList(),
                    Inspector: session.Inspector,
                    CreatedAt: session.CreatedAt));
            }
        }

        if (problems is not null)
            problems.AddRange(store.TakeWarnings());
        else
            store.TakeWarnings();

        using (FileLock.Acquire(Path.Combine(store.Root, LockName)))
        {
            AtomicFile.WriteAllText(
                Path.Combine(store.Root, FileName),
                JsonSerializer.Serialize(rows, SessionStore.JsonOptions));
        }

        return rows;
    }

    private static IEnumerable<string> SubDirectories(string folder)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        return Directory.EnumerateDirectories(folder, "*", options);
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
