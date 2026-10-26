using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuayeneKayit.Core;

/// <summary>
/// Oturum ve kusur dosyalarını okur ve yazar. Her yazma atomiktir.
/// Kusur numarası ve kusur güncellemeleri, oturum kilidi altında yapılır.
/// </summary>
public sealed class SessionStore : ISessionStore
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
    public Defect AddDefect(string folder, string defectType, Action<Defect>? configure = null, LengthUnit unit = LengthUnit.Mm)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var existing = LoadDefects(folder);
        var nextId = existing.Count == 0 ? 1 : existing.Max(d => d.Id) + 1;

        var defect = new Defect { Id = nextId, DefectType = defectType, Unit = unit };
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
    /// Rework sonrası muayene sonucunu işler; tek kilit altında yapılır, yarım kalan bir devir olmaz:
    /// ölçümler (devam eden kusurda), karar (bölge sonuçları, yeni kusur numaraları) ve yeni kusurlar (child'lar,
    /// numara sırasıyla; birim parent'tan, bağ ve bulunduğu bölge yazılır). Parent ve açılan child'lar döner.
    /// </summary>
    public (Defect Parent, IReadOnlyList<Defect> Children) ApplyReinspect(
        string folder,
        int parentId,
        ReinspectResult result,
        string decidedBy,
        DecisionSource source = DecisionSource.Engineer,
        string? reference = null)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var parentPath = DefectPath(folder, parentId);
        if (!File.Exists(parentPath))
            throw new FileNotFoundException($"Kusur bulunamadı: #{parentId}", parentPath);

        var parent = ReadJson<Defect>(parentPath);

        // Bölge adları kusurun kendi fotoğraflarından olmalı (bozuk istek diske yazılmasın).
        foreach (var photo in (result.PhotoOutcomes?.Keys ?? Enumerable.Empty<string>())
                     .Concat(result.NewDefects.Select(n => n.FoundAtPhoto).OfType<string>()))
        {
            if (!parent.Photos.Contains(photo))
                throw new InvalidOperationException($"Bölge #{parentId} kusurunda yok: {photo}");
        }

        var nextId = LoadDefects(folder).Max(d => d.Id) + 1;
        // Yeni kusurlar ana kusurun birimini taşır.
        var children = result.NewDefects
            .Select((n, i) => new Defect
            {
                Id = nextId + i,
                DefectType = n.Type,
                Unit = parent.Unit,
                SupersedesId = parent.Id,
                FoundAtPhoto = n.FoundAtPhoto,
            })
            .ToList();

        // Yeni ölçümler ve karar aynı kilit altında: karar anı görüntüsü yeni değerleri taşır.
        if (result.ParentContinues && result.Measurements is MeasurementSnapshot values)
            MeasurementEditor.Apply(parent, values, decidedBy);

        DefectStateMachine.Apply(parent, result.Decision, source, decidedBy, result.Note, reference,
            photoOutcomes: result.PhotoOutcomes, newDefectIds: children.Select(c => c.Id).ToList());

        foreach (var child in children)
            WriteDefect(folder, child);
        WriteDefect(folder, parent);
        return (parent, children);
    }

    /// <summary>
    /// Rework sonrası kusur tek bir yeni hatayla devrediliyorsa: parent'a karar yazılır, yeni child kusur açılır ve
    /// ikisi birbirine bağlanır (<see cref="ApplyReinspect"/>'in tek yeni hatalı kısa yolu).
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
        var result = new ReinspectResult(false, null, null, new[] { new NewDefectSpec(childType, null) }, note);
        return ApplyReinspect(folder, parentId, result, decidedBy, source, reference).Children[0];
    }

    /// <summary>
    /// Kusuru void eder (karar geçmişi yoksa). Void edilen, rework sonrası açılmış bir yeni kusursa ve parent'ın
    /// "yeni hata" kararından geriye hiç aktif yeni kusur kalmadıysa parent'a sistem kaydıyla o karar geri alınır:
    /// parent tekrar rework sonrası muayene bekler. Tek kilit altında yapılır. Geri açılan parent numarası döner (yoksa null).
    /// </summary>
    public int? VoidDefect(string folder, int defectId, string by, string note)
    {
        using var _ = FileLock.Acquire(Path.Combine(folder, "session.lock"));

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadJson<Defect>(path);
        DefectStateMachine.Apply(defect, Decision.Void, DecisionSource.Operator, by, note: note);

        Defect? reopened = null;
        if (defect.SupersedesId is int parentId && File.Exists(DefectPath(folder, parentId)))
        {
            var parent = ReadJson<Defect>(DefectPath(folder, parentId));
            var last = DefectStateMachine.Effective(parent).LastOrDefault(x => x.Source != DecisionSource.System);
            if (last is { Decision: Decision.AfterReworkNewDefect } && DefectRegions.ChildIdsOf(parent, last).Contains(defectId))
            {
                // Aynı kararla açılan diğer yeni kusurlardan void olmayan kaldı mı?
                var stillActive = DefectRegions.ChildIdsOf(parent, last)
                    .Where(id => id != defectId && File.Exists(DefectPath(folder, id)))
                    .Any(id => DefectStateMachine.GetState(ReadJson<Defect>(DefectPath(folder, id))) != DefectState.Voided);

                if (!stillActive)
                {
                    DefectStateMachine.SystemUndo(parent, last, by,
                        $"Yeni kusur #{defectId} void edildi; #{parentId} yeniden rework sonrası muayene bekliyor");
                    if (parent.SupersededById == defectId)
                        parent.SupersededById = null;
                    reopened = parent;
                }
            }
        }

        WriteDefect(folder, defect);
        if (reopened is not null)
            WriteDefect(folder, reopened);
        return reopened?.Id;
    }

    /// <summary>
    /// Kusurun son kararını geri alır. Karar yeni kusurlara (child) devretmişse ya da yeni kusur açmışsa hepsi void edilir
    /// (silinmez, rapora girmez); bölge durumları kararla birlikte geri gelir. Child'lardan birinin kendi kararı varsa
    /// önce onlar geri alınmalıdır. Tek kilit altında yapılır. Geri alınan karar ve void edilen child numaraları döner.
    /// </summary>
    public (Disposition Undone, IReadOnlyList<int> VoidedChildIds) UndoLastDecision(string folder, int defectId, string by, string? note)
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

        // Önce hepsi denetlenir, sonra void edilir: bir child'ın kararı varsa hiçbiri değişmez.
        var toVoid = new List<Defect>();
        foreach (var childId in DefectRegions.ChildIdsOf(defect, target))
        {
            var childPath = DefectPath(folder, childId);
            if (!File.Exists(childPath))
                continue;

            var child = ReadJson<Defect>(childPath);
            if (DefectStateMachine.GetState(child) == DefectState.Voided)
                continue;
            if (DefectStateMachine.Effective(child).Count > 0)
                throw new InvalidOperationException(
                    $"Yeni kusur #{childId} için karar verilmiş. Önce #{childId} kararlarını geri alın.");
            toVoid.Add(child);
        }

        foreach (var child in toVoid)
        {
            DefectStateMachine.Apply(child, Decision.Void, DecisionSource.Operator, by,
                note: $"#{defectId} kararı geri alındı: {note.Trim()}");
            WriteDefect(folder, child);
        }

        // Eski tek bağ da çözülür (yeni kodda yazılmaz ama eski kayıtta dolu olabilir).
        if (target.Decision == Decision.AfterReworkNewDefect && defect.SupersededById is not null)
            defect.SupersededById = null;

        var undone = DefectStateMachine.Undo(defect, by, note, null, childrenHandled: true);
        WriteDefect(folder, defect);
        return (undone, toVoid.Select(c => c.Id).ToList());
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
