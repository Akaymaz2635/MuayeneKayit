using System.Text;
using System.Text.RegularExpressions;

namespace MuayeneKayit.Core;

/// <summary>
/// Lot içindeki etkilenen parça numaraları ("1-5, 8"). Kusurda isteğe bağlıdır; boş = tüm parça / tek parça.
/// Girdi: pozitif tam sayılar; ayırıcı virgül, boşluk ya da noktalı virgül; aralık "1-5" (tire ya da en dash).
/// </summary>
public static partial class PartList
{
    /// <summary>En büyük kabul edilen parça numarası.</summary>
    public const int MaxPart = 9999;

    // Tire çevresindeki boşluklar ("1 - 5") aralığı bozmasın diye önce sıkıştırılır.
    [GeneratedRegex(@"\s*[-–—]\s*")]
    private static partial Regex DashRegex();

    /// <summary>
    /// Metni sıralı, tekrarsız numara listesine çevirir. Boş metin boş liste verir.
    /// Geçersizse (harf, 0, ters aralık, <see cref="MaxPart"/> üstü …) açıklayıcı Türkçe <see cref="ArgumentException"/> fırlatır.
    /// </summary>
    public static IReadOnlyList<int> Parse(string? text)
    {
        var set = new SortedSet<int>();
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<int>();

        var compact = DashRegex().Replace(text.Trim(), "-");
        foreach (var token in compact.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var bounds = token.Split('-');
            if (bounds.Length == 1)
            {
                set.Add(Number(bounds[0], token));
            }
            else if (bounds.Length == 2)
            {
                var from = Number(bounds[0], token);
                var to = Number(bounds[1], token);
                if (from > to)
                    throw new ArgumentException($"Etkilenen parçalar: '{token}' aralığı ters (küçük sayı önce yazılmalı).");
                for (var n = from; n <= to; n++)
                    set.Add(n);
            }
            else
            {
                throw new ArgumentException($"Etkilenen parçalar: '{token}' anlaşılamadı. Örnek: 1-5, 8");
            }
        }
        return set.ToList();
    }

    /// <summary>Kayıtlı değeri okur; bozuk / geçersizse boş liste verir (rapor ve cümle hata fırlatmasın).</summary>
    public static IReadOnlyList<int> TryParse(string? text)
    {
        try { return Parse(text); }
        catch (ArgumentException) { return Array.Empty<int>(); }
    }

    /// <summary>Girdiyi saklanacak biçime çevirir: "1,2,3,4,5 8" → "1-5, 8". Boş girdi boş metin verir. Geçersizse hata fırlatır.</summary>
    public static string Normalize(string? text)
    {
        var parts = Parse(text);
        if (parts.Count == 0)
            return "";
        return string.Join(", ", Runs(parts).Select(r => r.From == r.To ? r.From.ToString()
            : r.To - r.From == 1 ? $"{r.From}, {r.To}" : $"{r.From}-{r.To}"));
    }

    /// <summary>
    /// İngilizce cümle parçası: "on the part" (boş), "on part 3", "on parts 1, 2 and 5", "on parts 1–5 and 8".
    /// Üç ve daha uzun ardışık diziler en dash ile sıkıştırılır.
    /// </summary>
    public static string Describe(IReadOnlyList<int> parts)
    {
        if (parts.Count == 0)
            return "on the part";

        var ordered = parts.Distinct().OrderBy(n => n).ToList();
        if (ordered.Count == 1)
            return $"on part {ordered[0]}";

        var items = new List<string>();
        foreach (var (from, to) in Runs(ordered))
        {
            if (to - from >= 2)
                items.Add($"{from}–{to}");
            else
                for (var n = from; n <= to; n++)
                    items.Add(n.ToString());
        }

        var sb = new StringBuilder("on parts ");
        if (items.Count == 1)
            return sb.Append(items[0]).ToString();
        sb.Append(string.Join(", ", items.Take(items.Count - 1))).Append(" and ").Append(items[^1]);
        return sb.ToString();
    }

    /// <summary>
    /// Rework sonrası devam eden parçaların doğrulaması. <paramref name="before"/> doluysa <paramref name="after"/> boş olamaz ve
    /// <paramref name="before"/>'un alt kümesi olmalı. <paramref name="before"/> boşsa (tüm parçalar) geçerli her liste (boş dahil) olur.
    /// Hata varsa Türkçe mesaj, yoksa null.
    /// </summary>
    public static string? ValidateContinuing(string? before, string? after)
    {
        IReadOnlyList<int> now;
        try { now = Parse(after); }
        catch (ArgumentException ex) { return ex.Message; }

        var was = TryParse(before);
        if (was.Count == 0)
            return null;
        if (now.Count == 0)
            return "Devam eden en az bir parça yazın; kusur hiçbir parçada kalmadıysa 'Uygun' seçin.";

        var outside = now.Where(n => !was.Contains(n)).ToList();
        return outside.Count == 0
            ? null
            : $"Rework sonrası devam eden parçalar, rework öncesindeki parçaların içinden seçilmeli: {string.Join(", ", outside)} listede yok.";
    }

    /// <summary><paramref name="before"/> içinde olup <paramref name="after"/> içinde olmayan parçalar (giderilenler). <paramref name="before"/> ya da <paramref name="after"/> boşsa (tümü) boş.</summary>
    public static IReadOnlyList<int> Removed(string? before, string? after)
    {
        var was = TryParse(before);
        if (was.Count == 0)
            return Array.Empty<int>();
        var now = TryParse(after);
        if (now.Count == 0)
            return Array.Empty<int>(); // boş = tüm parçalar: hiçbiri giderilmiş sayılmaz
        return was.Where(n => !now.Contains(n)).ToList();
    }

    /// <summary>Parça listesini saklama biçiminde yazar ("2, 4", ardışıksa "2-4").</summary>
    public static string Join(IReadOnlyList<int> parts) => Normalize(string.Join(",", parts));

    private static int Number(string text, string token)
    {
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n))
            throw new ArgumentException($"Etkilenen parçalar: '{token}' geçerli bir parça numarası değil. Örnek: 1-5, 8");
        if (n < 1 || n > MaxPart)
            throw new ArgumentException($"Etkilenen parçalar: '{token}' 1 ile {MaxPart} arasında olmalı.");
        return n;
    }

    /// <summary>Sıralı listeyi ardışık dizilere böler (her biri bir aralık).</summary>
    private static IEnumerable<(int From, int To)> Runs(IReadOnlyList<int> sorted)
    {
        var start = sorted[0];
        var prev = start;
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == prev + 1)
            {
                prev = sorted[i];
                continue;
            }
            yield return (start, prev);
            start = prev = sorted[i];
        }
        yield return (start, prev);
    }
}
