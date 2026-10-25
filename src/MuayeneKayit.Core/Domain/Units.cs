using System.Globalization;

namespace MuayeneKayit.Core;

/// <summary>
/// Uzunluk ölçülerinin biçimi. mm en fazla 2 ondalık ("0.12"); inch en fazla 4 ondalık ve
/// 1'den küçük değerlerde baştaki sıfır olmadan yazılır (".123", "-.5").
/// </summary>
public static class Units
{
    public static string Format(double value, LengthUnit unit)
    {
        if (unit == LengthUnit.Mm)
            return value.ToString("0.##", CultureInfo.InvariantCulture);

        var text = value.ToString("0.####", CultureInfo.InvariantCulture);
        if (text.StartsWith("0.", StringComparison.Ordinal))
            return text.Substring(1);
        if (text.StartsWith("-0.", StringComparison.Ordinal))
            return "-" + text.Substring(2);
        return text;
    }

    public static string Suffix(LengthUnit unit) => unit == LengthUnit.Inch ? "in" : "mm";

    /// <summary>"Derinlik (mm)" / "Derinlik (in)".</summary>
    public static string Header(string label, LengthUnit unit) => $"{label} ({Suffix(unit)})";
}