namespace MuayeneKayit.Core;

/// <summary>
/// Ortak alan üzerinde basit dosya kilidi. Kilit dosyası exclusive oluşturularak alınır,
/// dispose edilince silinir. Eski kalmış kilit (StaleAfter) admin tarafından elle açılmalıdır.
/// </summary>
public sealed class FileLock : IDisposable
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

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
                return new FileLock(stream);
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                if (IsStale(lockPath))
                    throw new InvalidOperationException(
                        $"Kilit {StaleAfter.TotalMinutes:0} dakikadan eski, admin kontrol etmeli: {lockPath}");

                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"Kilit alınamadı: {lockPath}");

                Thread.Sleep(200);
            }
        }
    }

    public void Dispose() => _stream.Dispose();

    private static bool IsStale(string lockPath)
    {
        try
        {
            return DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) > StaleAfter;
        }
        catch (IOException)
        {
            // Kilit o an bırakıldı; yeniden denenecek.
            return false;
        }
    }
}
