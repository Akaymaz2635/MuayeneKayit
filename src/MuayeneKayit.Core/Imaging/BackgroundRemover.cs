namespace MuayeneKayit.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Kullanıcının parçanın çevresine çizdiği dikdörtgene göre arka planı ayırır. Eğitim ve dış kütüphane
/// gerektirmez; GrabCut'ın basitleştirilmiş hali: dikdörtgen dışı arka plan renk modeli, içeride
/// renk kümeleriyle yinelemeli ayırma, düzeltme, en büyük parçanın tutulması ve delik doldurma.
/// Girdi ve çıktı BGRA8 piksel dizisidir. Orijinal dizi değiştirilmez.
/// </summary>
public static class BackgroundRemover
{
    private const int WorkSize = 480;
    private const int Clusters = 5;

    public static bool[] ComputeMask(byte[] bgra, int width, int height, PixelRect rect)
    {
        // long aritmetiği: width * height * 4 int'te taşıp 0'a sarabilir (65536x65536). Piksel sayısı sınırlıdır.
        if (ImageHeader.ExceedsLimit(width, height) || bgra.Length < (long)width * height * 4)
            throw new ArgumentException("Görüntü verisi geçersiz.");
        rect = Clamp(rect, width, height);
        if (rect.Width < 10 || rect.Height < 10)
            throw new ArgumentException("Parçanın çevresine daha büyük bir dikdörtgen çizin.");

        // Küçük çalışma görüntüsü (blok ortalaması): hız ve gürültü azaltma.
        var s = Math.Max(1, (int)Math.Ceiling(Math.Max(width, height) / (double)WorkSize));
        var sw = (width + s - 1) / s;
        var sh = (height + s - 1) / s;
        var small = Downscale(bgra, width, height, s, sw, sh);

        var r = new PixelRect(rect.X / s, rect.Y / s,
            Math.Max(1, (rect.X + rect.Width) / s - rect.X / s),
            Math.Max(1, (rect.Y + rect.Height) / s - rect.Y / s));
        var inside = new bool[sw * sh];
        for (var y = r.Y; y < Math.Min(sh, r.Y + r.Height); y++)
            for (var x = r.X; x < Math.Min(sw, r.X + r.Width); x++)
                inside[y * sw + x] = true;

        // Arka plan modeli: dikdörtgen dışı. Dikdörtgen tüm görüntüyü kaplıyorsa kenar bandı.
        var bgIdx = Enumerable.Range(0, sw * sh).Where(i => !inside[i]).ToList();
        if (bgIdx.Count < 50)
        {
            var band = Math.Max(2, Math.Min(sw, sh) / 20);
            bgIdx = Enumerable.Range(0, sw * sh)
                .Where(i => i % sw < band || i % sw >= sw - band || i / sw < band || i / sw >= sh - band)
                .ToList();
        }
        var bg = KMeans(small, bgIdx);

        // Ön plan tohumu: içeride arka plana en az benzeyen yarı.
        var insideIdx = Enumerable.Range(0, sw * sh).Where(i => inside[i]).ToList();
        var bgDist = insideIdx.ToDictionary(i => i, i => Nearest(small, i, bg));
        var median = bgDist.Values.OrderBy(v => v).ElementAt(bgDist.Count / 2);
        var seed = insideIdx.Where(i => bgDist[i] >= median && bgDist[i] > 0).ToList();
        if (seed.Count < 10)
            throw new InvalidOperationException("Parça arka plandan ayırt edilemedi. Daha sade bir zemin deneyin.");

        var fg = KMeans(small, seed);
        var mask = new bool[sw * sh];
        for (var iter = 0; iter < 4; iter++)
        {
            foreach (var i in insideIdx)
                mask[i] = Nearest(small, i, fg) < bgDist[i];

            var fgIdx = insideIdx.Where(i => mask[i]).ToList();
            if (fgIdx.Count < 10)
                break;
            fg = KMeans(small, fgIdx);
        }

        Smooth(mask, inside, sw, sh);
        Smooth(mask, inside, sw, sh);
        KeepLargestParts(mask, sw, sh);
        FillHoles(mask, inside, sw, sh);

        // Tam çözünürlüğe büyüt; sınır bloklarında piksel rengine göre yeniden karar ver.
        var full = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            var cy = y / s;
            for (var x = 0; x < width; x++)
            {
                var cx = x / s;
                var c = mask[cy * sw + cx];
                var edge = (cx > 0 && mask[cy * sw + cx - 1] != c) || (cx < sw - 1 && mask[cy * sw + cx + 1] != c)
                        || (cy > 0 && mask[(cy - 1) * sw + cx] != c) || (cy < sh - 1 && mask[(cy + 1) * sw + cx] != c);

                var inRect = x >= rect.X && x < rect.X + rect.Width && y >= rect.Y && y < rect.Y + rect.Height;
                if (edge && s > 1 && inRect)
                {
                    var p = (y * width + x) * 4;
                    var color = new[] { (double)bgra[p + 2], bgra[p + 1], bgra[p] };
                    c = Nearest(color, fg) < Nearest(color, bg);
                }
                full[y * width + x] = c && inRect;
            }
        }
        return full;
    }

    /// <summary>Arka plan piksellerini düz açık renkle doldurur, yeni bir dizi döndürür.</summary>
    public static byte[] Apply(byte[] bgra, bool[] mask, byte fill = 255)
    {
        var output = (byte[])bgra.Clone();
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i])
                continue;
            var p = i * 4;
            output[p] = output[p + 1] = output[p + 2] = fill;
            output[p + 3] = 255;
        }
        return output;
    }

    private static PixelRect Clamp(PixelRect r, int w, int h)
    {
        var x0 = Math.Clamp(Math.Min(r.X, r.X + r.Width), 0, w);
        var y0 = Math.Clamp(Math.Min(r.Y, r.Y + r.Height), 0, h);
        var x1 = Math.Clamp(Math.Max(r.X, r.X + r.Width), 0, w);
        var y1 = Math.Clamp(Math.Max(r.Y, r.Y + r.Height), 0, h);
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Piksel başına RGB, double [r,g,b] düz dizi.</summary>
    private static double[] Downscale(byte[] bgra, int w, int h, int s, int sw, int sh)
    {
        var result = new double[sw * sh * 3];
        for (var cy = 0; cy < sh; cy++)
        {
            for (var cx = 0; cx < sw; cx++)
            {
                double r = 0, g = 0, b = 0;
                var n = 0;
                for (var y = cy * s; y < Math.Min(h, cy * s + s); y++)
                {
                    for (var x = cx * s; x < Math.Min(w, cx * s + s); x++)
                    {
                        var p = (y * w + x) * 4;
                        b += bgra[p];
                        g += bgra[p + 1];
                        r += bgra[p + 2];
                        n++;
                    }
                }
                var o = (cy * sw + cx) * 3;
                result[o] = r / n;
                result[o + 1] = g / n;
                result[o + 2] = b / n;
            }
        }
        return result;
    }

    /// <summary>Belirlenimci k-means: başlangıç merkezleri parlaklığa göre eşit aralıklı seçilir.</summary>
    private static double[][] KMeans(double[] px, List<int> indices)
    {
        var stride = Math.Max(1, indices.Count / 20000);
        var sample = indices.Where((_, k) => k % stride == 0).ToList();
        var sorted = sample.OrderBy(i => px[i * 3] + px[i * 3 + 1] + px[i * 3 + 2]).ToList();
        var k = Math.Min(Clusters, sorted.Count);

        var centers = new double[k][];
        for (var c = 0; c < k; c++)
        {
            var i = sorted[(int)((c + 0.5) * sorted.Count / k)];
            centers[c] = new[] { px[i * 3], px[i * 3 + 1], px[i * 3 + 2] };
        }

        for (var iter = 0; iter < 8; iter++)
        {
            var sums = new double[k, 3];
            var counts = new int[k];
            foreach (var i in sample)
            {
                var best = 0;
                var bestD = double.MaxValue;
                for (var c = 0; c < k; c++)
                {
                    var d = Dist(px, i, centers[c]);
                    if (d < bestD) { bestD = d; best = c; }
                }
                sums[best, 0] += px[i * 3];
                sums[best, 1] += px[i * 3 + 1];
                sums[best, 2] += px[i * 3 + 2];
                counts[best]++;
            }
            for (var c = 0; c < k; c++)
            {
                if (counts[c] > 0)
                    centers[c] = new[] { sums[c, 0] / counts[c], sums[c, 1] / counts[c], sums[c, 2] / counts[c] };
            }
        }
        return centers;
    }

    private static double Dist(double[] px, int i, double[] c)
    {
        var dr = px[i * 3] - c[0];
        var dg = px[i * 3 + 1] - c[1];
        var db = px[i * 3 + 2] - c[2];
        return dr * dr + dg * dg + db * db;
    }

    private static double Nearest(double[] px, int i, double[][] centers)
    {
        var best = double.MaxValue;
        foreach (var c in centers)
            best = Math.Min(best, Dist(px, i, c));
        return best;
    }

    private static double Nearest(double[] color, double[][] centers) => Nearest(color, 0, centers);

    /// <summary>3x3 çoğunluk filtresi; tek tük yanlış pikselleri temizler.</summary>
    private static void Smooth(bool[] mask, bool[] inside, int w, int h)
    {
        var copy = (bool[])mask.Clone();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (!inside[i])
                    continue;
                var on = 0;
                var total = 0;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        total++;
                        if (copy[ny * w + nx]) on++;
                    }
                mask[i] = on * 2 > total;
            }
        }
    }

    /// <summary>Yalnızca büyük ön plan parçalarını tutar (en büyüğün %15'inden küçük adalar silinir).</summary>
    private static void KeepLargestParts(bool[] mask, int w, int h)
    {
        var label = new int[mask.Length];
        var sizes = new List<int> { 0 };
        var stack = new Stack<int>();

        for (var start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || label[start] != 0)
                continue;

            var id = sizes.Count;
            var size = 0;
            stack.Push(start);
            label[start] = id;
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                size++;
                foreach (var n in Neighbors(i, w, h))
                {
                    if (mask[n] && label[n] == 0)
                    {
                        label[n] = id;
                        stack.Push(n);
                    }
                }
            }
            sizes.Add(size);
        }

        if (sizes.Count <= 1)
            return;
        var keep = Math.Max(sizes.Max() * 0.15, mask.Length * 0.002);
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i] && sizes[label[i]] < keep)
                mask[i] = false;
        }
    }

    /// <summary>Dışarıdan ulaşılamayan arka plan adacıkları (parçanın içindeki delikler) ön plana katılır.</summary>
    private static void FillHoles(bool[] mask, bool[] inside, int w, int h)
    {
        var reached = new bool[mask.Length];
        var stack = new Stack<int>();
        for (var i = 0; i < mask.Length; i++)
        {
            var x = i % w;
            var y = i / w;
            var border = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !inside[i];
            if (border && !mask[i])
            {
                reached[i] = true;
                stack.Push(i);
            }
        }

        while (stack.Count > 0)
        {
            var i = stack.Pop();
            foreach (var n in Neighbors(i, w, h))
            {
                if (!mask[n] && !reached[n])
                {
                    reached[n] = true;
                    stack.Push(n);
                }
            }
        }

        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i] && !reached[i])
                mask[i] = true;
        }
    }

    private static IEnumerable<int> Neighbors(int i, int w, int h)
    {
        var x = i % w;
        var y = i / w;
        if (x > 0) yield return i - 1;
        if (x < w - 1) yield return i + 1;
        if (y > 0) yield return i - w;
        if (y < h - 1) yield return i + w;
    }
}
