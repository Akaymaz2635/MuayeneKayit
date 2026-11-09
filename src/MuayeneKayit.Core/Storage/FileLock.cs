using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Ortak alan üzerinde basit dosya kilidi. Kilit dosyası exclusive oluşturularak alınır,
/// dispose edilince silinir. Eski kalmış kilit (StaleAfter) admin tarafından elle açılmalıdır.
/// Kilit dosyasının içine makine, kullanıcı, süreç ve zaman yazılır: eski kalan kilidin sahibi anlaşılır.
/// </summary>
public sealed class FileLock : IDisposable
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Saat farkı payı: bundan fazla ileri tarihli kilit (yanlış saatli makine ya da elle kurcalanmış dosya) hiçbir zaman
    /// kendiliğinden açılmayacağı için eski sayılır.
    /// </summary>
    public static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(2);

    private readonly FileStream _stream;

    private FileLock(FileStream stream) => _stream = stream;

    public static FileLock Acquire(string lockPath, TimeSpan? timeout = null)
    {
        var directory = Path.GetDirectoryName(lockPath)
            ?? throw new ArgumentException("Geçersiz kilit yolu.", nameof(lockPath));
        Directory.CreateDirectory(directory);

        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);

        while (true)
        {
            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
                WriteOwner(stream);
                return new FileLock(stream);
            }
            catch (IOException)
            {
                // Kilit başkasında. Kilit tam bu arada bırakılmış (dosya silinmiş) olabilir: o durumda hemen yeniden denenir.
                if (File.Exists(lockPath) && IsStale(lockPath))
                    throw new InvalidOperationException(
                        $"Kilit {StaleAfter.TotalMinutes:0} dakikadan eski ya da ileri tarihli, admin kontrol etmeli: {lockPath}{OwnerSuffix(lockPath)}");

                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"Kilit alınamadı: {lockPath}");

                Thread.Sleep(File.Exists(lockPath) ? 200 : 10);
            }
        }
    }

    public void Dispose() => _stream.Dispose();

    /// <summary>Kilit sahibini yazar (en iyi çaba: yazılamazsa kilit yine geçerlidir).</summary>
    private static void WriteOwner(FileStream stream)
    {
        try
        {
            var text = $"{Environment.MachineName}\\{Environment.UserName} pid={Environment.ProcessId} {DateTime.UtcNow:o}";
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Kilit dosyasındaki sahip bilgisi; okunamıyorsa (kilit açık tutuluyor, eski sürüm kilidi) null.</summary>
    public static string? DescribeOwner(string lockPath)
    {
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[256];
            var read = stream.Read(buffer, 0, buffer.Length);
            var text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string OwnerSuffix(string lockPath) =>
        DescribeOwner(lockPath) is string owner ? $" (sahibi: {owner})" : "";

    /// <summary>
    /// Kilit eski mi: <see cref="StaleAfter"/>'dan eski ya da <see cref="FutureSkew"/>'den fazla ileri tarihli.
    /// Admin bakım listesi ve kilit alma aynı kuralı kullanır.
    /// </summary>
    public static bool IsStale(string lockPath)
    {
        try
        {
            // Dosya kontrol ile okuma arasında silindiyse Windows 1601 tarihini döndürür (hata değil): eski sayılmaz.
            var written = File.GetLastWriteTimeUtc(lockPath);
            if (written.Year <= 1601)
                return false;

            var age = DateTime.UtcNow - written;
            return age > StaleAfter || -age > FutureSkew;
        }
        catch (IOException)
        {
            // Kilit o an bırakıldı; yeniden denenecek.
            return false;
        }
    }
}
