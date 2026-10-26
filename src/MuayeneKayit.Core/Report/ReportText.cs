using System.Globalization;

namespace MuayeneKayit.Core;

/// <summary>Rapor tarihleri: ay İngilizce üç harf, ay-gün-yıl; bilgisayarın dil ayarından bağımsız.</summary>
public static class ReportDates
{
    /// <summary>"Oct.27.2026"</summary>
    public static string Day(DateTime t) => t.ToString("MMM.dd.yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Oct.27.2026 09:10"</summary>
    public static string DayTime(DateTime t) => t.ToString("MMM.dd.yyyy HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// Raporda görünen sabit metinlerin "Türkçe / English" karşılıkları tek yerde.
/// Kullanıcı girdisi (not, renk, referans, kusur tipi, Inspector, dosya adı) buradan geçmez, çevrilmez.
/// Döndürülen metin HTML'e gömülmeden önce çağıran tarafından kodlanır.
/// </summary>
public static class ReportText
{
    public static string Bi(string tr, string en) => $"{tr} / {en}";

    public static readonly string Part = Bi("Parça", "Part");
    public static readonly string Operation = Bi("Operasyon", "Operation");
    public static readonly string Serial = Bi("Seri", "Serial");
    public static readonly string Defect = Bi("Kusur", "Defect");
    public static readonly string Page = Bi("Sayfa", "Page");
    public static readonly string Photo = Bi("Fotoğraf", "Photo");
    public static readonly string NoPhoto = Bi("Fotoğraf yok", "No photo");
    public static readonly string NoDefects = Bi("Kusur kaydı yok.", "No defects recorded.");
    public static readonly string NoDecisionYet = Bi("Henüz karar yok.", "No decision yet.");
    public static readonly string CleanedBackground = Bi("arka plan kaldırıldı", "background removed");

    public static readonly string Measurements = Bi("Ölçümler", "Measurements");
    public static readonly string DispositionHistory = Bi("Karar geçmişi", "Disposition history");
    public static readonly string MeasurementCorrections = Bi("Ölçüm düzeltmeleri", "Measurement corrections");
    public static readonly string Note = Bi("Not", "Note");

    public static readonly string Depth = Bi("Derinlik", "Depth");
    public static readonly string Width = Bi("Genişlik", "Width");
    public static readonly string Length = Bi("Uzunluk", "Length");
    public static readonly string Diameter = Bi("Çap", "Diameter");
    public static readonly string Angle = Bi("Açı", "Angle");
    public static readonly string Color = Bi("Renk", "Color");

    public static readonly string Date = Bi("Tarih", "Date");
    public static readonly string By = Bi("Kim", "By");
    public static readonly string DecisionHeader = Bi("Karar", "Decision");
    public static readonly string Reference = Bi("Referans", "Reference");
    public static readonly string MeasurementAtDecision = Bi("Ölçüm (karar anı)", "Measurement at decision");
    public static readonly string Before = Bi("Önce", "Before");
    public static readonly string After = Bi("Sonra", "After");

    public static readonly string Region = Bi("Bölge", "Region");
    public static readonly string Resolved = Bi("Giderildi", "Resolved");
    public static readonly string Continues = Bi("Devam ediyor", "Continues");
    public static readonly string NewDefect = Bi("Yeni kusur", "New defect");
    public static readonly string Closed = Bi("Kapandı", "Closed");

    /// <summary>Sistemin kendi yazdığı karar notu (kullanıcı girdisi değildir).</summary>
    public static string SystemNote(string? note) =>
        note == "MRB reddi, otomatik hurda" ? Bi(note, "MRB rejected, automatic scrap") : note ?? "";

    public static string DecisionName(Disposition d) =>
        d.Decision == Decision.Rework && d.ReworkScope == ReworkScope.HighMetalOnly
            ? Bi("Rework → Yeniden muayene (yalnızca high metal)", DefectSentence.DecisionLabel(d))
            : DecisionName(d.Decision);

    public static string DecisionName(Decision decision) => Bi(decision switch
    {
        Decision.Accept => "Kabul (SPA)",
        Decision.Rework => "Rework → Yeniden muayene",
        Decision.Reinspect => "Yeniden muayene",
        Decision.Repair => "Repair",
        Decision.Scrap => "Hurda",
        Decision.SubmitMrb => "MRB'ye gönderildi",
        Decision.ReturnToVendor => "Tedarikçiye iade",
        Decision.Void => "Void",
        Decision.MrbAccepted => "MRB kabul",
        Decision.MrbRejected => "MRB red",
        Decision.AfterReworkConforms => "Rework sonrası uygun",
        Decision.AfterReworkSameDefect => "Rework sonrası aynı kusur devam",
        Decision.AfterReworkNewDefect => "Rework sonrası yeni hata",
        Decision.Reopen => "Yeniden açıldı",
        Decision.Undo => "Geri alındı",
        _ => decision.ToString(),
    }, DefectSentence.DecisionLabel(decision));

    public static string State(DefectState s) => s switch
    {
        DefectState.Open => Bi("Açık", "Open"),
        DefectState.Reworking => Bi("Rework", "Rework"),
        DefectState.Repairing => Bi("Repair'de", "In repair"),
        DefectState.AwaitingReinspect => Bi("Rework sonrası muayene bekliyor", "Awaiting reinspection"),
        DefectState.WaitingVendor => Bi("Tedarikçide", "At vendor"),
        DefectState.MrbPending => Bi("MRB bekliyor", "MRB pending"),
        DefectState.Closed => Bi("Kapandı", "Closed"),
        DefectState.Voided => Bi("Void", "Void"),
        DefectState.Superseded => Bi("Devredildi", "Superseded"),
        _ => s.ToString(),
    };
}
