using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuayeneKayit.Core;

/// <summary>
/// Oturum ve kusur dosyalarını okur ve yazar. Her yazma atomiktir.
/// Kusur numarası ve kusur güncellemeleri, oturum kilidi altında yapılır.
/// </summary>
public sealed class SessionStore
{
    /// <summary>Tek bir JSON dosyası için üst sınır. Bozuk ya da kötü niyetli büyük dosyayı reddeder.</summary>
    public const long MaxJsonBytes = 5 * 1024 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _root;

    public SessionStore(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public string Root => _root;

    public string FolderFor(string part, string operation, string serial) =>
        SafePath.SessionFolder(_root, part, operation, serial);

    /// <summary>Oturum varsa onu döndürür, yoksa oluşturur. Mevcut veri asla üzerine yazılmaz.</summary>
    public (string Folder, Session Session) OpenOrCreate(Session input)
    {
        var folder = FolderFor(input.PartNumber, input.OperationNumber, input.SerialNumber);
        var existing = LoadSession(folder);
        if (existing is not null)
            return (folder, existing);

        Directory.CreateDirectory(folder);
        SaveSession(folder, input);
        return (folder, input);
    }

    public Session? LoadSession(string folder)
    {
        var path = Path.Combine(folder, "session.json");
        return File.Exists(path) ? ReadJson<Session>(path) : null;
    }

    public void SaveSession(string folder, Session session)
    {
        AtomicFile.WriteAllText(
            Path.Combine(folder, "session.json"),
            JsonSerializer.Serialize(session, JsonOptions));
    }

    public List<Defect> LoadDefects(string folder)
    {
        var directory = Path.Combine(folder, "defects");
        if (!Directory.Exists(directory))
            return new List<Defect>();

        return Directory.EnumerateFiles(directory, "*.json")
            .Select(ReadJson<Defect>)
            .OrderBy(d => d.Id)
            .ToList();
    }

    /// <summary>
    /// Yeni kusur ekler. Numara, mevcut tüm dosyalara bakılarak (void olanlar dahil) verilir,
    /// böylece hiçbir numara yeniden kullanılmaz.
    /// </summary>
    public Defect AddDefect(string folder, string defectType, Action<Defect>? configure = null)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var existing = LoadDefects(folder);
        var nextId = existing.Count == 0 ? 1 : existing.Max(d => d.Id) + 1;

        var defect = new Defect { Id = nextId, DefectType = defectType };
        configure?.Invoke(defect);
        WriteDefect(folder, defect);
        return defect;
    }

    /// <summary>Kusuru kilit altında günceller. Değişiklik kaydedilmeden önce mutate çalışır.</summary>
    public Defect UpdateDefect(string folder, int defectId, Action<Defect> mutate)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadJson<Defect>(path);
        mutate(defect);
        WriteDefect(folder, defect);
        return defect;
    }

    /// <summary>
    /// Birden fazla kusuru aynı kilit altında günceller. Parent-child devri gibi
    /// iki kaydın birlikte değişmesi gereken durumlar için.
    /// </summary>
    public void UpdateDefects(string folder, Action<IDictionary<int, Defect>> mutate, IEnumerable<int> ids)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var map = new Dictionary<int, Defect>();
        foreach (var id in ids)
        {
            var path = DefectPath(folder, id);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Kusur bulunamadı: #{id}", path);
            map[id] = ReadJson<Defect>(path);
        }

        mutate(map);
        foreach (var defect in map.Values)
            WriteDefect(folder, defect);
    }

    /// <summary>
    /// Rework sonrası kusur devam ediyorsa: parent'a karar yazılır, yeni child kusur açılır ve
    /// ikisi birbirine bağlanır. Tek kilit altında yapılır, yarım kalan bir devir olmaz.
    /// </summary>
    public Defect ContinueAsChild(
        string folder,
        int parentId,
        string childType,
        DecisionSource source,
        string decidedBy,
        string? note,
        string? reference)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var parentPath = DefectPath(folder, parentId);
        if (!File.Exists(parentPath))
            throw new FileNotFoundException($"Kusur bulunamadı: #{parentId}", parentPath);

        var parent = ReadJson<Defect>(parentPath);
        var nextId = LoadDefects(folder).Max(d => d.Id) + 1;
        var child = new Defect { Id = nextId, DefectType = childType };

        DefectStateMachine.Apply(parent, Decision.AfterReworkNewDefect, source, decidedBy, note, reference);
        DefectStateMachine.Supersede(parent, child);

        WriteDefect(folder, child);
        WriteDefect(folder, parent);
        return child;
    }

    /// <summary>
    /// Kusurun son kararını geri alır. Karar yeni bir kusura (child) devretmişse bağ çözülür ve child
    /// void edilir (silinmez, rapora girmez). Child'ın kendi kararları varsa önce onlar geri alınmalıdır.
    /// Tek kilit altında yapılır. Geri alınan karar ve (varsa) void edilen child numarası döner.
    /// </summary>
    public (Disposition Undone, int? VoidedChildId) UndoLastDecision(string folder, int defectId, string by, string? note)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadJson<Defect>(path);
        var target = DefectStateMachine.LastUndoable(defect)
            ?? throw new InvalidOperationException("Geri alınacak karar yok.");
        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Geri almak için gerekçe yazın (örn. yanlış karar seçildi).");

        int? voidedChild = null;
        if (target.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is int childId)
        {
            var childPath = DefectPath(folder, childId);
            if (File.Exists(childPath))
            {
                var child = ReadJson<Defect>(childPath);
                if (DefectStateMachine.Effective(child).Count > 0)
                    throw new InvalidOperationException(
                        $"Yeni kusur #{childId} için karar verilmiş. Önce #{childId} kararlarını geri alın.");

                DefectStateMachine.Apply(child, Decision.Void, DecisionSource.Operator, by,
                    note: $"#{defectId} kararı geri alındı: {note.Trim()}");
                WriteDefect(folder, child);
                voidedChild = childId;
            }
            defect.SupersededById = null;
        }

        var undone = DefectStateMachine.Undo(defect, by, note);
        WriteDefect(folder, defect);
        return (undone, voidedChild);
    }

    private void WriteDefect(string folder, Defect defect)
    {
        var directory = Path.Combine(folder, "defects");
        Directory.CreateDirectory(directory);
        AtomicFile.WriteAllText(
            DefectPath(folder, defect.Id),
            JsonSerializer.Serialize(defect, JsonOptions));
    }

    private static string DefectPath(string folder, int defectId) =>
        Path.Combine(folder, "defects", $"{defectId}.json");

    internal static T ReadJson<T>(string path)
    {
        if (new FileInfo(path).Length > MaxJsonBytes)
            throw new InvalidDataException($"Dosya boyut sınırını aşıyor: {path}");

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Dosya boş ya da bozuk: {path}");
    }
}
