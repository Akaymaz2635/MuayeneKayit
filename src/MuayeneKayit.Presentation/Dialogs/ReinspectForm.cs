using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Rework sonrası muayene penceresinin kuralları. Pencere canlı güncelleme ve Kaydet etkinliği için bunu çağırır,
/// ViewModel de sonucu doğrularken aynı kuralları kullanır.
/// </summary>
public static class ReinspectForm
{
    /// <summary>Boş bırakılan alan önceki değerinde kalır; high metal cevabı yoksa öncekini korur.</summary>
    public static MeasurementSnapshot Merge(MeasurementSnapshot before, MeasurementSnapshot entered, bool? highMetalStillThere) =>
        new(entered.Depth ?? before.Depth, entered.Width ?? before.Width, entered.Length ?? before.Length,
            entered.Radius ?? before.Radius, entered.AngleDeg ?? before.AngleDeg, entered.Color ?? before.Color,
            highMetalStillThere ?? before.HighMetal);

    /// <summary>Kutulardan en az birine değer yazıldı mı.</summary>
    public static bool AnyValue(MeasurementSnapshot entered) =>
        entered.Depth is not null || entered.Width is not null || entered.Length is not null
        || entered.Radius is not null || entered.AngleDeg is not null || entered.Color is not null;

    /// <summary>
    /// Kaydet açılır mı. Giderildi: her zaman. Aynı kusur: high metal varsa cevabı zorunlu (ölçümler boş kalabilir),
    /// yoksa en az bir ölçüm. Yeni hata: tip seçili olmalı.
    /// </summary>
    public static bool CanSave(ReinspectOutcome? outcome, bool hadHighMetal, bool? highMetalStillThere, bool anyValue, string? childType) =>
        outcome switch
        {
            ReinspectOutcome.Conforms => true,
            ReinspectOutcome.Same => hadHighMetal ? highMetalStillThere is not null : anyValue,
            ReinspectOutcome.New => childType is not null,
            _ => false,
        };

    /// <summary>Canlı değişim satırı (artış / azalış). Ölçüm ya da high metal cevabı yoksa yönlendirir.</summary>
    public static string DeltaText(MeasurementSnapshot before, MeasurementSnapshot merged, bool anyValue, bool highMetalAnswered, LengthUnit unit = LengthUnit.Mm)
    {
        if (!anyValue && !highMetalAnswered)
            return "Değişimi görmek için ölçüm girin.";

        var now = new Defect
        {
            Depth = merged.Depth, Width = merged.Width, Length = merged.Length, Radius = merged.Radius,
            AngleDeg = merged.AngleDeg, Color = merged.Color, HighMetal = merged.HighMetal,
        };
        return string.Join("\n", MeasurementCompare.Lines(before, now, unit));
    }
}
