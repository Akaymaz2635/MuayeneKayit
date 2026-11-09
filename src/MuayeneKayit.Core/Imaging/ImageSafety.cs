using System.Buffers.Binary;
using System.Text;

namespace MuayeneKayit.Core;

/// <summary>
/// Görüntü başlığından piksel boyutunu okur (görüntüyü açmadan). Çok büyük görüntüler (örn. 65536×65536) kodu çözülmeden
/// reddedilebilsin diye: tam çözünürlükte BGRA'ya açmak gigabaytlarca bellek ister.
/// </summary>
public static class ImageHeader
{
    /// <summary>İçe aktarma ve fotoğraf düzenleyicide kabul edilen en büyük piksel sayısı (60 megapiksel).</summary>
    public const long MaxPixels = 60_000_000;

    /// <summary>PNG (IHDR) ya da JPEG (SOF) boyutu; başlık okunamazsa null (kod çözücü zaten hata verir).</summary>
    public static (int Width, int Height)? TryReadSize(Stream stream)
    {
        try
        {
            var start = stream.Position;
            Span<byte> head = stackalloc byte[8];
            if (stream.Read(head) < 4)
                return null;

            if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
                return ReadPng(stream, start);
            if (head[0] == 0xFF && head[1] == 0xD8)
                return ReadJpeg(stream, start + 2);
            return null;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException)
        {
            return null;
        }
    }

    public static (int Width, int Height)? TryReadSize(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return TryReadSize(stream);
    }

    /// <summary>Boyut biliniyorsa ve <see cref="MaxPixels"/>'i aşıyorsa true (çarpma long ile yapılır).</summary>
    public static bool ExceedsLimit(int width, int height) => width <= 0 || height <= 0 || (long)width * height > MaxPixels;

    private static (int, int)? ReadPng(Stream s, long start)
    {
        // 8 bayt imza + 4 uzunluk + "IHDR" + 4 genişlik + 4 yükseklik
        s.Position = start + 16;
        Span<byte> b = stackalloc byte[8];
        if (s.Read(b) < 8)
            return null;
        return (BinaryPrimitives.ReadInt32BigEndian(b), BinaryPrimitives.ReadInt32BigEndian(b[4..]));
    }

    private static (int, int)? ReadJpeg(Stream s, long position)
    {
        s.Position = position;
        Span<byte> b = stackalloc byte[9];
        for (var guard = 0; guard < 4096; guard++)
        {
            var c = s.ReadByte();
            if (c != 0xFF)
                return null;

            int marker;
            do { marker = s.ReadByte(); } while (marker == 0xFF);   // dolgu baytları
            if (marker < 0 || marker == 0xD9 || marker == 0xDA)
                return null;
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;

            if (s.Read(b[..2]) < 2)
                return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(b);
            if (length < 2)
                return null;

            // SOFn (C4, C8, CC hariç): uzunluk, kesinlik, yükseklik, genişlik
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                if (s.Read(b[..5]) < 5)
                    return null;
                return (BinaryPrimitives.ReadUInt16BigEndian(b[3..]), BinaryPrimitives.ReadUInt16BigEndian(b[1..]));
            }

            s.Seek(length - 2, SeekOrigin.Current);
        }
        return null;
    }
}

/// <summary>
/// İçe aktarılan fotoğraftan konum / cihaz üst verisini (EXIF, XMP, IPTC) bayt düzeyinde ayıklar. Görüntü verisi yeniden
/// kodlanmaz (kalite kaybı olmaz). EXIF yönü (döndürme) korunur: yalnızca yön etiketini taşıyan küçük bir EXIF bloğu eklenir.
/// Dosya beklenmedik biçimdeyse <c>null</c> döner ve dosya olduğu gibi kopyalanır.
/// </summary>
public static class MetadataStripper
{
    private static readonly byte[] ExifId = Encoding.ASCII.GetBytes("Exif\0\0");
    private static readonly byte[] XmpId = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
    private static readonly byte[] PhotoshopId = Encoding.ASCII.GetBytes("Photoshop 3.0\0");

    /// <summary>Temizlenmiş içerik; ayıklanacak bir şey yoksa ya da biçim tanınmıyorsa null.</summary>
    public static byte[]? Strip(byte[] data, string extension) =>
        extension is ".jpg" or ".jpeg" ? StripJpeg(data) : extension == ".png" ? StripPng(data) : null;

    private static bool StartsWith(byte[] d, int at, int end, byte[] prefix) =>
        end - at >= prefix.Length && d.AsSpan(at, prefix.Length).SequenceEqual(prefix);

    private static byte[]? StripJpeg(byte[] d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8)
            return null;

        var output = new MemoryStream(d.Length);
        output.Write(d, 0, 2);
        var changed = false;
        var orientation = 1;
        var i = 2;

        while (i + 4 <= d.Length)
        {
            if (d[i] != 0xFF)
                return null;
            var marker = d[i + 1];
            if (marker == 0xFF)
                return null;   // dolgu baytı: beklenmedik, dokunma
            if (marker is 0xD9 or 0xDA)
                break;
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                output.Write(d, i, 2);
                i += 2;
                continue;
            }

            var length = (d[i + 2] << 8) | d[i + 3];
            var end = i + 2 + length;
            if (length < 2 || end > d.Length)
                return null;

            var payload = i + 4;
            var drop = false;
            if (marker == 0xE1 && StartsWith(d, payload, end, ExifId))
            {
                drop = true;
                orientation = ReadOrientation(d, payload + ExifId.Length, end) ?? orientation;
            }
            else if (marker == 0xE1 && StartsWith(d, payload, end, XmpId))
                drop = true;
            else if (marker == 0xED && StartsWith(d, payload, end, PhotoshopId))
                drop = true;

            if (drop)
                changed = true;
            else
                output.Write(d, i, 2 + length);
            i = end;
        }

        if (!changed)
            return null;

        if (orientation is >= 2 and <= 8)
            WriteOrientationSegment(output, orientation);
        output.Write(d, i, d.Length - i);
        return output.ToArray();
    }

    /// <summary>EXIF (TIFF) bloğundan yön etiketi (0x0112); okunamazsa null.</summary>
    private static int? ReadOrientation(byte[] d, int tiff, int end)
    {
        if (end - tiff < 12)
            return null;

        bool big;
        if (d[tiff] == 'M' && d[tiff + 1] == 'M') big = true;
        else if (d[tiff] == 'I' && d[tiff + 1] == 'I') big = false;
        else return null;

        ushort U16(int at) => big ? BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(at, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at, 2));
        uint U32(int at) => big ? BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(at, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at, 4));

        if (U16(tiff + 2) != 42)
            return null;
        var ifd = (long)tiff + U32(tiff + 4);
        if (ifd + 2 > end)
            return null;

        var count = U16((int)ifd);
        for (var n = 0; n < count; n++)
        {
            var entry = ifd + 2 + n * 12L;
            if (entry + 12 > end)
                return null;
            if (U16((int)entry) == 0x0112 && U16((int)entry + 2) == 3)
            {
                var value = U16((int)entry + 8);
                return value is >= 1 and <= 8 ? value : null;
            }
        }
        return null;
    }

    /// <summary>Yalnızca yön etiketini taşıyan en küçük EXIF bloğu (APP1).</summary>
    private static void WriteOrientationSegment(Stream output, int orientation)
    {
        var segment = new List<byte> { 0xFF, 0xE1, 0x00, 0x22 };
        segment.AddRange(ExifId);
        segment.AddRange(new byte[] { (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08 });   // TIFF başlığı, IFD0 ofseti 8
        segment.AddRange(new byte[] { 0x00, 0x01 });                                                   // 1 etiket
        segment.AddRange(new byte[] { 0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00 });
        segment.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });                                       // sonraki IFD yok
        output.Write(segment.ToArray());
    }

    private static byte[]? StripPng(byte[] d)
    {
        if (d.Length < 12 || d[0] != 0x89 || d[1] != 0x50 || d[2] != 0x4E || d[3] != 0x47)
            return null;

        var output = new MemoryStream(d.Length);
        output.Write(d, 0, 8);
        var changed = false;
        var i = 8;
        while (i + 12 <= d.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(i, 4));
            var total = 12L + length;
            if (i + total > d.Length)
                return null;

            var type = Encoding.ASCII.GetString(d, i + 4, 4);
            // eXIf: konum / cihaz bilgisi. tEXt / iTXt (XMP) gibi metin parçaları da üst veri taşıyabilir.
            if (type == "eXIf")
                changed = true;
            else if (type == "iTXt" && StartsWith(d, i + 8, i + 8 + (int)length, Encoding.ASCII.GetBytes("XML:com.adobe.xmp")))
                changed = true;
            else
                output.Write(d, i, (int)total);
            i += (int)total;
        }

        if (i != d.Length)
            return null;
        return changed ? output.ToArray() : null;
    }
}
