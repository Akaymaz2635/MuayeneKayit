using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
/// Yalnızca eklemeli denetim kaydı. Her satır bir öncekinin SHA-256 özetine bağlıdır.
/// Bir satır değiştirilirse ya da silinirse zincir kırılır ve Verify bunu bildirir.
/// Ortak alana yazma yetkisi olan biri tüm dosyayı silebilir; bu program onu engelleyemez.
/// </summary>
public static class AuditLog
{
    public const string FileName = "_audit.log";
    private const string LockName = "_audit.lock";
    private static readonly string GenesisHash = new('0', 64);

    public static void Append(SessionStore store, string actor, string action, string details)
    {
        var path = Path.Combine(store.Root, FileName);

        using var _ = FileLock.Acquire(Path.Combine(store.Root, LockName));

        var prev = LastHash(path) ?? GenesisHash;
        var ts = DateTime.Now.ToString("o");
        var hash = ComputeHash(prev, ts, actor, action, details);

        var line = JsonSerializer.Serialize(new AuditEntry(ts, actor, action, details, prev, hash));
        File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Zinciri baştan doğrular. Bozuk ilk satırın numarasını döndürür.</summary>
    public static (bool Ok, int? BadLine) Verify(SessionStore store)
    {
        var path = Path.Combine(store.Root, FileName);
        if (!File.Exists(path))
            return (true, null);

        var prev = GenesisHash;
        var lineNo = 0;
        foreach (var raw in File.ReadLines(path))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            AuditEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<AuditEntry>(raw);
            }
            catch (JsonException)
            {
                return (false, lineNo);
            }

            if (entry is null || entry.Prev != prev)
                return (false, lineNo);

            if (ComputeHash(entry.Prev, entry.Ts, entry.Actor, entry.Action, entry.Details) != entry.Hash)
                return (false, lineNo);

            prev = entry.Hash;
        }

        return (true, null);
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

    private static string? LastHash(string path)
    {
        if (!File.Exists(path))
            return null;

        string? last = null;
        foreach (var line in File.ReadLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
                last = line;
        }

        if (last is null)
            return null;

        return JsonSerializer.Deserialize<AuditEntry>(last)?.Hash;
    }

    private static string ComputeHash(string prev, string ts, string actor, string action, string details)
    {
        // Alanlar JSON dizisi olarak birleştirilir; ayırıcı karakter çakışması olmaz.
        var payload = JsonSerializer.Serialize(new[] { prev, ts, actor, action, details });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
