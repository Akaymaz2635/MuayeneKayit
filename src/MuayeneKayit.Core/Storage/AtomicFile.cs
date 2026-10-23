using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Dosyayı geçici ad ile yazar, diske boşaltır, sonra asıl adına taşır.
/// Yazma sırasında kesinti olursa eski içerik bozulmadan kalır.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("Geçersiz dosya yolu.", nameof(path));
        Directory.CreateDirectory(directory);

        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
