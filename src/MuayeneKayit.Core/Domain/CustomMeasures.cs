using System.Globalization;

namespace MuayeneKayit.Core;

/// <summary>Admin'de tanımlanan ek ölçüm alanının türü.</summary>
public enum CustomFieldKind
{
    /// <summary>Kusurun birimiyle (mm / in) uzunluk; pozitif olmalı (ör. Yükseklik).</summary>
    Length,

    /// <summary>Derece, 0-360.</summary>
    Angle,

    /// <summary>Birimsiz sayı; işareti serbest (eksi de girilebilir).</summary>
    Number,

    /// <summary>Kısa metin (en fazla 40 karakter).</summary>
    Text,
}

/// <summary>
/// Admin panelinde tanımlanan ek ölçüm alanı (ör. ad "Yükseklik", kısaltma "H", tür uzunluk). Kusur tipi şablonlarında
/// <see cref="DefectTypeTemplate.CustomFields"/> ile anahtarından işaretlenir.
/// </summary>
public sealed class CustomFieldDef
{
    /// <summary>Değişmeyen kimlik ("f1a2b3c4"). Ad ya da kısaltma değişse de şablonlar ve kayıtlar bununla eşleşir.</summary>
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Abbrev { get; set; } = "";
    public CustomFieldKind Kind { get; set; } = CustomFieldKind.Length;
}

/// <summary>
/// Kusurdaki ek ölçüm değeri. Alanın adı, kısaltması ve türü kayda kopyalanır: alan Admin'den silinse ya da yeniden
/// adlandırılsa da eski kayıt ve rapor değişmez. Sayı türlerinde <paramref name="Number"/>, metinde <paramref name="Text"/> doludur.
/// </summary>
public sealed record CustomMeasure(string Key, string Name, string Abbrev, CustomFieldKind Kind, double? Number, string? Text);

/// <summary>Ek ölçüm değerleri için yardımcılar: karşılaştırma, birleştirme, biçim ve doğrulama.</summary>
public static class CustomMeasures
{
    public const int MaxTextLength = 40;

    public static CustomMeasure Of(CustomFieldDef def, double? number, string? text) =>
        new(def.Key, def.Name, def.Abbrev, def.Kind, def.Kind == CustomFieldKind.Text ? null : number,
            def.Kind == CustomFieldKind.Text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null);

    /// <summary>Kayıttaki değerden alan tanımı (alan Admin'den silinmişse muayene ekranında yine gösterilebilsin).</summary>
    public static CustomFieldDef DefOf(CustomMeasure m) => new() { Key = m.Key, Name = m.Name, Abbrev = m.Abbrev, Kind = m.Kind };

    public static bool HasValue(CustomMeasure m) =>
        m.Kind == CustomFieldKind.Text ? !string.IsNullOrWhiteSpace(m.Text) : m.Number is not null;

    /// <summary>Yalnızca değeri olanlar, her anahtardan bir tane (sonraki geçerli), sıra korunur.</summary>
    public static List<CustomMeasure> Clean(IEnumerable<CustomMeasure>? list)
    {
        var result = new List<CustomMeasure>();
        foreach (var m in list ?? Enumerable.Empty<CustomMeasure>())
        {
            var i = result.FindIndex(x => x.Key == m.Key);
            if (i >= 0)
                result.RemoveAt(i);
            if (HasValue(m))
                result.Insert(i >= 0 ? i : result.Count, m);
        }
        return result;
    }

    /// <summary>Değerler aynı mı (anahtar, tür ve değer; ad / kısaltma farkı değişiklik sayılmaz).</summary>
    public static bool SameValues(IReadOnlyList<CustomMeasure>? a, IReadOnlyList<CustomMeasure>? b)
    {
        var x = Clean(a);
        var y = Clean(b);
        if (x.Count != y.Count)
            return false;
        foreach (var m in x)
        {
            var other = y.FirstOrDefault(n => n.Key == m.Key);
            if (other is null || other.Kind != m.Kind || other.Number != m.Number || !string.Equals(other.Text, m.Text, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>Önceki değerlerin üzerine girilenler yazılır; girilmeyen alan önceki değerinde kalır.</summary>
    public static List<CustomMeasure> Merge(IReadOnlyList<CustomMeasure>? before, IReadOnlyList<CustomMeasure>? entered) =>
        Clean((before ?? Array.Empty<CustomMeasure>()).Concat(Clean(entered)));

    public static CustomMeasure? Find(IReadOnlyList<CustomMeasure>? list, string key) =>
        list?.FirstOrDefault(m => m.Key == key && HasValue(m));

    /// <summary>"0.3 mm", "15°", "-2", "gri". Uzunluk kusurun birimiyle yazılır.</summary>
    public static string ValueText(CustomMeasure m, LengthUnit unit) => m.Kind switch
    {
        CustomFieldKind.Length when m.Number is double v => $"{Units.Format(v, unit)} {Units.Suffix(unit)}",
        CustomFieldKind.Angle when m.Number is double v => $"{Num(v)}°",
        CustomFieldKind.Number when m.Number is double v => Num(v),
        CustomFieldKind.Text => m.Text?.Trim() ?? "",
        _ => "",
    };

    /// <summary>Kutu içinde düzenlenecek metin (birimsiz): "0.3", "15", "gri".</summary>
    public static string EditText(CustomMeasure? m, LengthUnit unit) => m switch
    {
        null => "",
        { Kind: CustomFieldKind.Text } => m.Text ?? "",
        { Kind: CustomFieldKind.Length, Number: double v } => Units.Format(v, unit),
        { Number: double v } => Num(v),
        _ => "",
    };

    /// <summary>Kısa yazım: "H 0.3 mm".</summary>
    public static string Short(CustomMeasure m, LengthUnit unit) => $"{m.Abbrev} {ValueText(m, unit)}";

    /// <summary>Kutu başlığı: "Yükseklik (mm)", "Eğim (°)", "Adet".</summary>
    public static string Header(CustomFieldDef def, LengthUnit unit) => def.Kind switch
    {
        CustomFieldKind.Length => Units.Header(def.Name, unit),
        CustomFieldKind.Angle => $"{def.Name} (°)",
        _ => def.Name,
    };

    public static string KindLabel(CustomFieldKind kind) => kind switch
    {
        CustomFieldKind.Length => "uzunluk (mm / in)",
        CustomFieldKind.Angle => "açı (°)",
        CustomFieldKind.Number => "sayı (birimsiz, eksi olabilir)",
        _ => "metin",
    };

    /// <summary>Değer doğrulaması (DefectValidator çağırır).</summary>
    public static void Validate(IReadOnlyList<CustomMeasure>? list, List<string> errors)
    {
        foreach (var m in Clean(list))
        {
            switch (m.Kind)
            {
                case CustomFieldKind.Length when m.Number is double v && (double.IsNaN(v) || double.IsInfinity(v) || v <= 0):
                    errors.Add($"{m.Name} pozitif bir sayı olmalı.");
                    break;
                case CustomFieldKind.Angle when m.Number is double v && (double.IsNaN(v) || v < 0 || v > 360):
                    errors.Add($"{m.Name} 0 ile 360 derece arasında olmalı.");
                    break;
                case CustomFieldKind.Number when m.Number is double v && (double.IsNaN(v) || double.IsInfinity(v)):
                    errors.Add($"{m.Name} geçerli bir sayı olmalı.");
                    break;
                case CustomFieldKind.Text when m.Text is { Length: > MaxTextLength }:
                    errors.Add($"{m.Name} en fazla {MaxTextLength} karakter olabilir.");
                    break;
            }
        }
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
