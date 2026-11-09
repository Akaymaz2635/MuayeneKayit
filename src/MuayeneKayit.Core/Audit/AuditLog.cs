using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuayeneKayit.Core;

/// <summary>Denetim kaydı satırı. Her satır bir önceki satırın özetini (Prev) taşır.</summary>
public sealed record AuditEntry(
    string Ts,
    string Actor,
    string Action,
    string Details,
    string Prev,
    string Hash);

/// <summary>
/// Denetim doğrulama sonucu: zincir hataları (<see cref="Problems"/>) ve denetimden sonra elle değiştirilmiş
/// kayıt dosyaları (<see cref="ModifiedRecords"/>). Her ikisi de boşsa <see cref="Ok"/> true'dur.
/// </summary>
public sealed record AuditCheck(bool Ok, int? BadLine, IReadOnlyList<string> Problems, IReadOnlyList<string> ModifiedRecords)
{
    /// <summary>Arayüzde gösterilecek tüm bulgular (önce zincir, sonra kayıtlar).</summary>
    public IReadOnlyList<string> All => Problems.Concat(ModifiedRecords).ToList();
}

/// <summary>
/// Yalnızca eklemeli denetim kaydı. Her satır bir öncekinin SHA-256 özetine bağlıdır; satır değişirse ya da silinirse
/// zincir kırılır ve Verify bunu bildirir. Ek korumalar:
/// <list type="bullet">
/// <item>Kayıt yazan satırların ayrıntısına yazılan kayıt dosyasının SHA-256 özeti eklenir; sonradan elle düzenlenen
/// kayıt "denetim dışı değiştirilmiş" olarak bildirilir.</item>
/// <item>Kök klasördeki "_audit.head" dosyası son satır sayısını ve özetini tutar; günlüğün sondan kısaltılması ya da
/// baştan hesaplanmış sahte zincir bununla yakalanır.</item>
/// <item>Bozuk son satır yeni yazmaları engellemez: açık bir işaret satırıyla yeni zincir bölümü başlar ve Verify bunu bildirir.</item>
/// </list>
/// SINIR: Anahtarsız zincirdir. Ortak alana tam yazma yetkisi olan biri günlüğü, başlık dosyasını ve kayıtları birlikte,
/// tutarlı biçimde yeniden yazarsa bu program onu yakalayamaz; bunun için _audit.log ve _audit.head NTFS'te
/// yalnız-ekleme izniyle korunmalıdır (KURULUM.md).
/// </summary>
public static class AuditLog
{
    public const string FileName = "_audit.log";
    public const string HeadFileName = "_audit.head";
    private const string LockName = "_audit.lock";
    private static readonly string GenesisHash = new('0', 64);

    /// <summary>Bozuk satırdan sonra yeni zincir bölümünü başlatan işaret satırının eylemi.</summary>
    public const string ChainResetAction = "audit.chain.reset";

    private sealed record Head(int Lines, string Hash);

    public static void Append(SessionStore store, string actor, string action, string details)
    {
        var path = Path.Combine(store.Root, FileName);

        using var _ = FileLock.Acquire(Path.Combine(store.Root, LockName));

        var state = ReadState(path);
        var pending = store.TakePendingWrites();
        try
        {
            var lines = new StringBuilder();
            var prev = state.LastHash ?? GenesisHash;
            var count = state.Lines;

            if (state.LastLineCorrupt)
            {
                // Bozuk son satırın üstüne yazılmaz; yeni zincir bölümü açık bir işaretle başlar ve Verify bunu bildirir.
                var markTs = DateTime.Now.ToString("o");
                var markDetails = $"{state.Lines}. satır okunamadı; yeni zincir bölümü başlatıldı ({Environment.MachineName})";
                var markHash = ComputeHash(GenesisHash, markTs, "sistem", ChainResetAction, markDetails);
                lines.AppendLine(JsonSerializer.Serialize(new AuditEntry(markTs, "sistem", ChainResetAction, markDetails, GenesisHash, markHash)));
                prev = markHash;
                count++;
            }

            var ts = DateTime.Now.ToString("o");
            var full = details + RecordHashes(store, pending);
            var hash = ComputeHash(prev, ts, actor, action, full);
            lines.AppendLine(JsonSerializer.Serialize(new AuditEntry(ts, actor, action, full, prev, hash)));
            count++;

            File.AppendAllText(path, lines.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            WriteHead(store, new Head(count, hash));
        }
        catch
        {
            // Satır yazılamadıysa özetler bir sonraki denetim satırına kalır.
            store.RestorePendingWrites(pending);
            throw;
        }
    }

    /// <summary>Zinciri baştan doğrular. Bozuk ilk satırın numarasını döndürür (satıra bağlanmayan sorunlarda null).</summary>
    public static (bool Ok, int? BadLine) Verify(SessionStore store)
    {
        var check = Check(store);
        return (check.Ok, check.BadLine);
    }

    /// <summary>
    /// Ayrıntılı doğrulama: zincir, günlük kısalması / silinmesi ve denetimden sonra değiştirilmiş kayıt dosyaları.
    /// Yalnızca okur.
    /// </summary>
    public static AuditCheck Check(SessionStore store)
    {
        var problems = new List<string>();
        var modified = new List<string>();
        int? badLine = null;

        var path = Path.Combine(store.Root, FileName);
        var head = ReadHead(store);

        if (!File.Exists(path))
        {
            if (head is not null)
                problems.Add("Denetim günlüğü (_audit.log) yok ama başlık dosyası var: günlük silinmiş.");
            else if (HasAnyRecord(store.Root))
                problems.Add("Kayıtlar var ama denetim günlüğü (_audit.log) yok: günlük silinmiş olabilir.");
            return new AuditCheck(problems.Count == 0, null, problems, modified);
        }

        void Bad(int line, string message)
        {
            badLine ??= line;
            problems.Add($"{line}. satır: {message}");
        }

        var prev = GenesisHash;
        var lineNo = 0;
        var nonEmpty = 0;
        var lastHash = "";
        var expectReset = false;
        var lastRecordHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in File.ReadLines(path))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            nonEmpty++;

            AuditEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<AuditEntry>(raw);
            }
            catch (JsonException)
            {
                entry = null;
            }

            if (entry is null || entry.Prev is null || entry.Hash is null || entry.Ts is null
                || entry.Actor is null || entry.Action is null || entry.Details is null)
            {
                Bad(lineNo, "satır okunamıyor ya da eksik.");
                expectReset = true;
                continue;
            }

            if (entry.Action == ChainResetAction && entry.Prev == GenesisHash)
            {
                // Bilinen kesinti: bozuk satır sonrası yeni zincir bölümü. Zincir sağlam sayılmaz, açıkça bildirilir.
                if (ComputeHash(entry.Prev, entry.Ts, entry.Actor, entry.Action, entry.Details) != entry.Hash)
                    Bad(lineNo, "zincir yeniden başlatma işareti geçersiz.");
                else
                {
                    badLine ??= lineNo;
                    problems.Add($"{lineNo}. satır: zincir yeniden başlatılmış (öncesinde bozuk satır var): {entry.Details}");
                }
                prev = entry.Hash;
                lastHash = entry.Hash;
                expectReset = false;
                continue;
            }

            if (entry.Prev != prev)
                Bad(lineNo, expectReset ? "bozuk satırdan sonra zincir devam ediyor." : "zincir kırık (satır silinmiş ya da değiştirilmiş).");
            else if (ComputeHash(entry.Prev, entry.Ts, entry.Actor, entry.Action, entry.Details) != entry.Hash)
                Bad(lineNo, "satır içeriği özetiyle uyuşmuyor (değiştirilmiş).");

            expectReset = false;
            prev = entry.Hash;
            lastHash = entry.Hash;
            foreach (var (rel, hash) in ParseRecordHashes(entry.Details))
                lastRecordHash[rel] = hash;
        }

        if (head is not null)
        {
            if (nonEmpty < head.Lines)
                problems.Add($"Günlük kısalmış: başlık dosyasında {head.Lines} satır var, günlükte {nonEmpty}.");
            else if (nonEmpty == head.Lines && !string.Equals(lastHash, head.Hash, StringComparison.OrdinalIgnoreCase))
                problems.Add("Günlüğün son satırı başlık dosyasındaki özetle uyuşmuyor (günlük yeniden yazılmış olabilir).");
        }

        // Denetimden sonra elle değiştirilmiş kayıtlar: son denetimli özet ile dosyanın şimdiki özeti.
        var fullRoot = Path.GetFullPath(store.Root) + Path.DirectorySeparatorChar;
        foreach (var (rel, expected) in lastRecordHash.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var file = Path.GetFullPath(Path.Combine(store.Root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!file.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!File.Exists(file))
                modified.Add($"{rel}: kayıt dosyası yok (denetim dışı silinmiş).");
            else if (!string.Equals(HashFile(file), expected, StringComparison.OrdinalIgnoreCase))
                modified.Add($"{rel}: kayıt denetim dışı değiştirilmiş.");
        }

        return new AuditCheck(problems.Count == 0 && modified.Count == 0, badLine, problems, modified);
    }

    /// <summary>Son kayıtlar, yeniden eskiye. Okunamayan satırlar atlanır (doğrulama Verify ile yapılır).</summary>
    public static List<AuditEntry> ReadTail(SessionStore store, int count)
    {
        var path = Path.Combine(store.Root, FileName);
        if (!File.Exists(path))
            return new List<AuditEntry>();

        var queue = new Queue<string>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            queue.Enqueue(line);
            if (queue.Count > count)
                queue.Dequeue();
        }

        var entries = new List<AuditEntry>();
        foreach (var line in queue.Reverse())
        {
            try
            {
                if (JsonSerializer.Deserialize<AuditEntry>(line) is AuditEntry entry)
                    entries.Add(entry);
            }
            catch (JsonException)
            {
            }
        }
        return entries;
    }

    /// <summary>Günlükte daha önce bir "admin." eylemi (PIN, ayar, giriş) yazılmış mı. Admin PIN'inin sıfırlandığını anlamak için.</summary>
    public static bool HasAdminActivity(SessionStore store)
    {
        var path = Path.Combine(store.Root, FileName);
        if (!File.Exists(path))
            return false;

        foreach (var line in File.ReadLines(path))
        {
            // Kök değişimi satırı yeni köke de yazılır ama o kökte PIN kullanıldığı anlamına gelmez; sayılmaz.
            if (line.Contains("\"action\":\"admin.", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("\"action\":\"admin.root.change\"", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ───────────────────────── Yardımcılar ─────────────────────────

    private readonly record struct State(string? LastHash, int Lines, bool LastLineCorrupt);

    private static State ReadState(string path)
    {
        if (!File.Exists(path))
            return new State(null, 0, false);

        string? last = null;
        var count = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            last = line;
            count++;
        }

        if (last is null)
            return new State(null, 0, false);

        try
        {
            var entry = JsonSerializer.Deserialize<AuditEntry>(last);
            return entry?.Hash is string hash ? new State(hash, count, false) : new State(null, count, true);
        }
        catch (JsonException)
        {
            return new State(null, count, true);
        }
    }

    private static Head? ReadHead(SessionStore store)
    {
        try
        {
            var path = Path.Combine(store.Root, HeadFileName);
            if (!File.Exists(path) || new FileInfo(path).Length > 4096)
                return null;
            var head = JsonSerializer.Deserialize<Head>(File.ReadAllText(path));
            return head?.Hash is null ? null : head;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void WriteHead(SessionStore store, Head head)
    {
        try
        {
            AtomicFile.WriteAllText(Path.Combine(store.Root, HeadFileName), JsonSerializer.Serialize(head));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Başlık yazılamazsa denetim satırı yine geçerlidir; yalnızca kısaltma denetimi o satıra kadar eski kalır.
        }
    }

    // Ayrıntıya eklenen kayıt özetleri: " [sha256: PD170/OP40/SN1/defects/1.json=<64 hex>; ...]"
    private static readonly Regex RecordHashSuffix = new(@" \[sha256: (?<list>[^\]]+)\]\z", RegexOptions.CultureInvariant);

    private static string RecordHashes(SessionStore store, IEnumerable<string> paths)
    {
        var root = Path.GetFullPath(store.Root) + Path.DirectorySeparatorChar;
        var parts = new List<string>();
        foreach (var full in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                continue;
            var rel = full[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
            try
            {
                parts.Add($"{rel}={HashFile(full)}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Okunamayan dosyanın özeti yazılmaz; Verify o dosyayı karşılaştırmaz.
            }
        }
        return parts.Count == 0 ? "" : $" [sha256: {string.Join("; ", parts)}]";
    }

    private static IEnumerable<(string Path, string Hash)> ParseRecordHashes(string details)
    {
        var match = RecordHashSuffix.Match(details);
        if (!match.Success)
            yield break;

        foreach (var item in match.Groups["list"].Value.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = item.LastIndexOf('=');
            if (eq > 0 && item.Length - eq - 1 == 64)
                yield return (item[..eq], item[(eq + 1)..]);
        }
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Kökte en az bir oturum kaydı (session.json) var mı. Yeniden çözümleme noktaları izlenmez.</summary>
    private static bool HasAnyRecord(string root)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        try
        {
            foreach (var part in Directory.EnumerateDirectories(root, "*", options))
            {
                if (Path.GetFileName(part).StartsWith('_'))
                    continue;
                foreach (var op in Directory.EnumerateDirectories(part, "*", options))
                foreach (var serial in Directory.EnumerateDirectories(op, "*", options))
                {
                    if (File.Exists(Path.Combine(serial, "session.json")))
                        return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return false;
    }

    private static string ComputeHash(string prev, string ts, string actor, string action, string details)
    {
        // Alanlar JSON dizisi olarak birleştirilir; ayırıcı karakter çakışması olmaz.
        var payload = JsonSerializer.Serialize(new[] { prev, ts, actor, action, details });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
