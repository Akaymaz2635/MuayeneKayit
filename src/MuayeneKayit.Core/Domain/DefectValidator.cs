namespace MuayeneKayit.Core;

/// <summary>Ölçüm girişlerinin temel doğrulaması. Spec karşılaştırması yapmaz; yalnızca değer geçerliliğine bakar.</summary>
public static class DefectValidator
{
    public const int MaxColorLength = 40;

    public static IReadOnlyList<string> Validate(Defect defect)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(defect.DefectType))
            errors.Add("Hata tipi boş olamaz.");

        CheckPositive(defect.Depth, "Derinlik", errors);
        CheckPositive(defect.Width, "Genişlik", errors);
        CheckPositive(defect.Length, "Uzunluk", errors);
        CheckPositive(defect.Radius, "Yarıçap", errors);

        if (defect.AngleDeg is double angle && (double.IsNaN(angle) || angle < 0 || angle > 360))
            errors.Add("Açı 0 ile 360 derece arasında olmalı.");

        if (defect.Color is { Length: > MaxColorLength })
            errors.Add($"Renk en fazla {MaxColorLength} karakter olabilir.");

        return errors;
    }

    private static void CheckPositive(double? value, string label, List<string> errors)
    {
        if (value is double v && (double.IsNaN(v) || double.IsInfinity(v) || v <= 0))
            errors.Add($"{label} pozitif bir sayı olmalı.");
    }
}
