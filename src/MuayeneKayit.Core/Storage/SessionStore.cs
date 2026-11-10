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
        // Tamsayı enum değerleri kabul edilmez (999 gibi tanımsız sayı yerine açık hata); dosyalarda enum adları yazılıdır.
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
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
        // Kök yoksa (ağ yolu / sürücü kayıp) yeni, boş bir kök oluşturulmaz.
        RootGuard.Require(_root);
        var existing = LoadSession(folder);
        if (existing is not null)
            return (folder, existing);

        Directory.CreateDirectory(folder);
        SaveSession(folder, input);
        return (folder, input);
    }

    /// <summary>Oturum ve kusur işlemleri için kilit: kök ve oturum klasörü yoksa yeniden oluşturulmaz, anlaşılır hata verilir.</summary>
    private FileLock AcquireSessionLock(string folder)
    {
        RootGuard.Require(_root);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Oturum klasörü bulunamadı: {folder}. Ortak alana erişimi kontrol edin.");
        return FileLock.Acquire(Path.Combine(folder, "session.lock"));
    }

    /// <summary>Tamamlanmış oturuma yeni kusur, karar, ölçüm ya da fotoğraf yazılmaz (kilit altında çağrılır).</summary>
    private void EnsureWritableLocked(string folder)
    {
        Session? session;
        try
        {
            session = LoadSession(folder);
        }
        catch (InvalidDataException)
        {
            // Okunamayan oturum dosyası: durum bilinmiyor; oturum açma zaten ayrıca reddeder.
            return;
        }
        if (session?.Status == SessionStatus.Completed)
            throw new InvalidOperationException("Bu muayene tamamlandı; yeni kusur, karar, ölçüm ya da fotoğraf eklenemez.");
    }

    /// <summary>Oturum tamamlanmışsa hata verir (fotoğraf dosyası kopyalanmadan önce erken ret için; asıl denetim kilit altındadır).</summary>
    public void RequireWritable(string folder)
    {
        RootGuard.Require(_root);
        EnsureWritableLocked(folder);
    }

    /// <summary>
    /// Oturumu tamamlar. Açık kusur denetimi ve durum yazımı aynı kilit altındadır: tamamlama ile kusur ekleme yarışırsa
    /// "tamamlanmış ama açık kusurlu" oturum oluşmaz. Zaten tamamlanmışsa hiçbir şey yazmaz ve false döner.
    /// </summary>
    public bool CompleteSession(string folder)
    {
        using var _ = AcquireSessionLock(folder);

        var session = LoadSession(folder) ?? throw new InvalidOperationException("Oturum bulunamadı.");
        if (session.Status == SessionStatus.Completed)
            return false;

        var unreadable = UnreadableDefectFiles(folder);
        if (unreadable.Count > 0)
            throw new InvalidOperationException($"Muayene kapatılamaz: okunamayan kusur dosyası var ({string.Join(", ", unreadable.Take(3))}). Yöneticiye bildirin.");

        var blocking = SessionGate.BlockingDefects(LoadDefects(folder));
        if (blocking.Count > 0)
            throw new InvalidOperationException(
                "Muayene kapatılamaz, açık kusurlar var: " + string.Join(", ", blocking.Select(d => $"#{d.Id}")));

        session.Status = SessionStatus.Completed;
        SaveSession(folder, session);
        return true;
    }

    /// <summary>En büyük kusur numarası: fotoğraf adındaki numara 5 hane ("{id}_photo_NN") olabildiği için.</summary>
    public const int MaxDefectId = 99_999;

    // Kusur dosyası adı: sıfırsız başlayan numara + ".json" (007.json, kopya.json, notlar.json kusur sayılmaz).
    private static readonly System.Text.RegularExpressions.Regex DefectFileName =
        new(@"^[1-9][0-9]{0,8}\.json\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // Yüklemede atlanan dosyaların uyarıları (tekrarlar tek satır). TakeWarnings ile alınır.
    private readonly object _warnLock = new();
    private readonly HashSet<string> _warnings = new();

    // Yazılan kayıt dosyaları: bir sonraki denetim satırı bunların özetini taşır (bkz. AuditLog).
    private readonly object _writeLock = new();
    private readonly HashSet<string> _pendingWrites = new(StringComparer.OrdinalIgnoreCase);

    private string Rel(string path) => Path.GetRelativePath(_root, path);

    private void Warn(string message)
    {
        lock (_warnLock)
            _warnings.Add(message);
    }

    public IReadOnlyList<string> TakeWarnings()
    {
        lock (_warnLock)
        {
            var list = _warnings.ToList();
            _warnings.Clear();
            return list;
        }
    }

    /// <summary>Son denetim satırından beri yazılan kayıt dosyalarının tam yolları (alınınca liste boşalır).</summary>
    internal List<string> TakePendingWrites()
    {
        lock (_writeLock)
        {
            var list = _pendingWrites.ToList();
            _pendingWrites.Clear();
            return list;
        }
    }

    internal void RestorePendingWrites(IEnumerable<string> paths)
    {
        lock (_writeLock)
            foreach (var p in paths)
                _pendingWrites.Add(p);
    }

    private void MarkWritten(string path)
    {
        lock (_writeLock)
            _pendingWrites.Add(Path.GetFullPath(path));
    }

    public Session? LoadSession(string folder)
    {
        var path = Path.Combine(folder, "session.json");
        if (!File.Exists(path))
            return null;

        var session = ReadJson<Session>(path);
        // "null" gelen metinler boş sayılır; tanımsız enum sayısı bozuk dosya demektir.
        session.PartNumber ??= "";
        session.OperationNumber ??= "";
        session.SerialNumber ??= "";
        session.Inspector ??= "";
        session.Project ??= "";
        if (!Enum.IsDefined(session.Status))
            throw new InvalidDataException($"Dosyada geçersiz durum değeri: {path}");
        return session;
    }

    public void SaveSession(string folder, Session session)
    {
        RootGuard.Require(_root);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Oturum klasörü bulunamadı: {folder}. Ortak alana erişimi kontrol edin.");
        var path = Path.Combine(folder, "session.json");
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(session, JsonOptions));
        MarkWritten(path);
    }

    /// <summary>
    /// Kusurları okur. Dosya adı "{numara}.json" olmalı ve içindeki Id adla aynı, pozitif olmalı; bozuk, uyuşmayan ya da
    /// yinelenen dosya atlanır ve <see cref="TakeWarnings"/> ile bildirilir (tek bozuk dosya diğer kayıtları engellemez).
    /// </summary>
    public List<Defect> LoadDefects(string folder)
    {
        var directory = Path.Combine(folder, "defects");
        if (!Directory.Exists(directory) || SafePath.IsReparsePoint(directory))
            return new List<Defect>();

        var list = new List<Defect>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            if (!DefectFileName.IsMatch(name))
            {
                Warn($"{Rel(file)}: kusur dosyası adı '<numara>.json' değil, atlandı.");
                continue;
            }

            var fileId = int.Parse(Path.GetFileNameWithoutExtension(name), System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                list.Add(ReadDefect(file, fileId));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Warn($"{Rel(file)}: okunamadı, atlandı ({ex.Message})");
            }
        }

        return list.OrderBy(d => d.Id).ToList();
    }

    public IReadOnlyList<string> UnreadableDefectFiles(string folder)
    {
        var directory = Path.Combine(folder, "defects");
        var bad = new List<string>();
        if (!Directory.Exists(directory))
            return bad;

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            if (!DefectFileName.IsMatch(name))
            {
                bad.Add(name);
                continue;
            }
            try
            {
                ReadDefect(file, int.Parse(Path.GetFileNameWithoutExtension(name), System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                bad.Add(name);
            }
        }
        TakeWarnings();   // bu denetim kendi uyarılarını yeniden üretir; kullanıcıya ayrıca gösterilmesin
        return bad;
    }

    /// <summary>Bir sonraki kusur numarası. Okunamayan dosyaların numaraları da dolu sayılır (üzerine yazılmaz).</summary>
    private int NextDefectId(string folder)
    {
        long max = 0;
        var directory = Path.Combine(folder, "defects");
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.Length is > 0 and <= 18 && stem.All(char.IsAsciiDigit)
                    && long.Parse(stem, System.Globalization.CultureInfo.InvariantCulture) is var n && n > max)
                    max = n;
            }
        }

        // Dosya adı dışındaki kayıtlar da (adı bozuk ama içeriği okunabilen) hesaba katılır.
        foreach (var d in LoadDefects(folder))
            max = Math.Max(max, d.Id);

        if (max >= MaxDefectId)
            throw new InvalidOperationException($"Kusur numarası sınırına ({MaxDefectId}) ulaşıldı; yeni kusur eklenemez.");
        return (int)max + 1;
    }

    /// <summary>
    /// Tek kusur dosyasını okur ve doğrular: Id dosya adındaki numarayla aynı olmalı (aksi halde başka kaydın üzerine
    /// yazılabilirdi), enum değerleri tanımlı olmalı, geçersiz fotoğraf adları atılır.
    /// </summary>
    private Defect ReadDefect(string path, int expectedId)
    {
        var defect = ReadJson<Defect>(path);
        if (defect.Id != expectedId)
            throw new InvalidDataException($"Dosya adındaki numara ({expectedId}) ile içindeki Id ({defect.Id}) uyuşmuyor: {path}");

        var where = Rel(path);
        defect.DefectType ??= "";
        if (!Enum.IsDefined(defect.Unit))
            throw new InvalidDataException($"Dosyada geçersiz birim değeri: {path}");

        // Fotoğraf adları yalnızca düz, tanınan biçimde olabilir; diğerleri hiçbir yerde açılmaz.
        var photos = defect.Photos.Where(p => PhotoMarkupValidator.IsPhotoFileName(p)).Distinct().ToList();
        if (photos.Count != defect.Photos.Count)
            Warn($"{where}: geçersiz fotoğraf adı yok sayıldı.");
        defect.Photos = photos;

        var markups = new Dictionary<string, PhotoMarkup>();
        foreach (var (key, markup) in defect.Markups)
        {
            if (markup is null || !PhotoMarkupValidator.IsPhotoFileName(key))
                continue;
            markup.Shapes.RemoveAll(s => s is null);
            if (markup.CleanedFile is not null && !PhotoMarkupValidator.IsPhotoFileName(markup.CleanedFile))
            {
                Warn($"{where}: geçersiz temiz kopya adı yok sayıldı.");
                markup.CleanedFile = null;
            }
            markups[key] = markup;
        }
        defect.Markups = markups;

        defect.Custom?.RemoveAll(c => c is null);
        defect.MeasurementEdits.RemoveAll(e => e is null || e.Before is null || e.After is null);
        defect.Dispositions.RemoveAll(d => d is null);
        foreach (var d in defect.Dispositions)
        {
            if (!Enum.IsDefined(d.Decision) || !Enum.IsDefined(d.Source)
                || (d.ReworkScope is ReworkScope scope && !Enum.IsDefined(scope))
                || (d.PhotoOutcomes?.Values.Any(o => !Enum.IsDefined(o)) ?? false))
                throw new InvalidDataException($"Dosyada geçersiz karar değeri: {path}");
            d.DecidedBy ??= "";
        }
        return defect;
    }

    /// <summary>
    /// Yeni kusur ekler. Numara, mevcut tüm dosyalara bakılarak (void olanlar dahil) verilir,
    /// böylece hiçbir numara yeniden kullanılmaz.
    /// </summary>
    public Defect AddDefect(string folder, string defectType, Action<Defect>? configure = null, LengthUnit unit = LengthUnit.Mm)
    {
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var nextId = NextDefectId(folder);

        var defect = new Defect { Id = nextId, DefectType = defectType, Unit = unit };
        configure?.Invoke(defect);
        WriteDefect(folder, defect);
        return defect;
    }

    /// <summary>Kusuru kilit altında günceller. Değişiklik kaydedilmeden önce mutate çalışır.</summary>
    public Defect UpdateDefect(string folder, int defectId, Action<Defect> mutate)
    {
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadDefect(path, defectId);
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
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var map = new Dictionary<int, Defect>();
        foreach (var id in ids)
        {
            var path = DefectPath(folder, id);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Kusur bulunamadı: #{id}", path);
            map[id] = ReadDefect(path, id);
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
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var parentPath = DefectPath(folder, parentId);
        if (!File.Exists(parentPath))
            throw new FileNotFoundException($"Kusur bulunamadı: #{parentId}", parentPath);

        var parent = ReadDefect(parentPath, parentId);

        // Bölge adları kusurun kendi fotoğraflarından olmalı (bozuk istek diske yazılmasın).
        foreach (var photo in (result.PhotoOutcomes?.Keys ?? Enumerable.Empty<string>())
                     .Concat(result.NewDefects.Select(n => n.FoundAtPhoto).OfType<string>()))
        {
            if (!parent.Photos.Contains(photo))
                throw new InvalidOperationException($"Bölge #{parentId} kusurunda yok: {photo}");
        }

        var nextId = NextDefectId(folder);
        // Yeni kusurlar ana kusurun birimini taşır.
        var children = result.NewDefects
            .Select((n, i) => new Defect
            {
                Id = nextId + i,
                DefectType = n.Type,
                Unit = parent.Unit,
                SupersedesId = parent.Id,
                FoundAtPhoto = n.FoundAtPhoto,
                // Parent'la aynı parçalarda bulunduğu varsayılır; kullanıcı sonradan değiştirebilir.
                AffectedParts = parent.AffectedParts,
            })
            .ToList();

        // Yeni ölçümler ve karar aynı kilit altında: karar anı görüntüsü yeni değerleri taşır.
        if (result.ParentContinues && result.Measurements is MeasurementSnapshot values)
        {
            // Devam eden parçalar, kilit altındaki güncel parçaların içinden olmalı (arayüz dışı yoldan da korunur).
            if (values.Parts is not null && PartList.ValidateContinuing(parent.AffectedParts, values.Parts) is { } partsError)
                throw new InvalidOperationException(partsError);
            MeasurementEditor.Apply(parent, values, decidedBy);
        }

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
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadDefect(path, defectId);
        DefectStateMachine.Apply(defect, Decision.Void, DecisionSource.Operator, by, note: note);

        Defect? reopened = null;
        if (defect.SupersedesId is int parentId && File.Exists(DefectPath(folder, parentId)))
        {
            var parent = ReadDefect(DefectPath(folder, parentId), parentId);
            var last = DefectStateMachine.Effective(parent).LastOrDefault(x => x.Source != DecisionSource.System);
            if (last is { Decision: Decision.AfterReworkNewDefect } && DefectRegions.ChildIdsOf(parent, last).Contains(defectId))
            {
                // Aynı kararla açılan diğer yeni kusurlardan void olmayan kaldı mı?
                var stillActive = DefectRegions.ChildIdsOf(parent, last)
                    .Where(id => id != defectId && File.Exists(DefectPath(folder, id)))
                    .Any(id => DefectStateMachine.GetState(ReadDefect(DefectPath(folder, id), id)) != DefectState.Voided);

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
        using var _ = AcquireSessionLock(folder);
        EnsureWritableLocked(folder);

        var path = DefectPath(folder, defectId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Kusur bulunamadı: #{defectId}", path);

        var defect = ReadDefect(path, defectId);
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

            var child = ReadDefect(childPath, childId);
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
        if (defect.Id <= 0 || defect.Id > MaxDefectId)
            throw new InvalidOperationException($"Kusur numarası geçersiz: {defect.Id}.");

        var directory = Path.Combine(folder, "defects");
        Directory.CreateDirectory(directory);
        var path = DefectPath(folder, defect.Id);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(defect, JsonOptions));
        MarkWritten(path);
    }

    private static string DefectPath(string folder, int defectId) =>
        Path.Combine(folder, "defects", $"{defectId}.json");

    /// <summary>
    /// JSON dosyasını okur. Bozuk içerik (JsonException, geçersiz biçim) <see cref="InvalidDataException"/> olarak bildirilir:
    /// çağıranlar tek tür beklenen hatayı yakalar, uygulama çökmez.
    /// </summary>
    internal static T ReadJson<T>(string path)
    {
        if (new FileInfo(path).Length > MaxJsonBytes)
            throw new InvalidDataException($"Dosya boyut sınırını aşıyor: {path}");

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidDataException($"Dosya boş ya da bozuk: {path}");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException or InvalidOperationException)
        {
            throw new InvalidDataException($"Dosya bozuk ya da geçersiz: {path} ({ex.Message})", ex);
        }
    }
}
