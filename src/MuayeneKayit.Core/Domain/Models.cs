namespace MuayeneKayit.Core;

/// <summary>Bir operasyon için muayene oturumu. session.json dosyasına karşılık gelir.</summary>
public sealed class Session
{
    public string PartNumber { get; set; } = "";
    public string OperationNumber { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Inspector { get; set; } = "";
    public string Project { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public SessionStatus Status { get; set; } = SessionStatus.Open;
}

/// <summary>Bir hata kaydı. defects\{Id}.json dosyasına karşılık gelir.</summary>
public sealed class Defect
{
    public int Id { get; set; }
    public string DefectType { get; set; } = "";

    public double? Depth { get; set; }
    public double? Width { get; set; }
    public double? Length { get; set; }
    public double? Radius { get; set; }
    public double? AngleDeg { get; set; }
    public string? Color { get; set; }
    public string? Notes { get; set; }

    /// <summary>Mühendis kabul kağıdındaki spec adı.</summary>
    public string? SpecName { get; set; }

    /// <summary>Kabul kağıdının numarası.</summary>
    public string? AcceptanceReference { get; set; }

    public List<string> Photos { get; set; } = new();

    /// <summary>Fotoğraf adına göre işaretler ve arka plan sonucu. Orijinal fotoğraflar değişmez.</summary>
    public Dictionary<string, PhotoMarkup> Markups { get; set; } = new();

    /// <summary>Bu kusurun yerini aldığı önceki kusur (child ise dolu).</summary>
    public int? SupersedesId { get; set; }

    /// <summary>Bu kusurun devam ettiği yeni kusur (parent ise dolu).</summary>
    public int? SupersededById { get; set; }

    public List<Disposition> Dispositions { get; set; } = new();

    /// <summary>Karar vermeden yapılan ölçüm düzeltmeleri (önce / sonra).</summary>
    public List<MeasurementEdit> MeasurementEdits { get; set; } = new();
}

/// <summary>Bir kararın anındaki ölçümlerin görüntüsü. Rework öncesi / sonrası karşılaştırması için.</summary>
public sealed record MeasurementSnapshot(
    double? Depth,
    double? Width,
    double? Length,
    double? Radius,
    double? AngleDeg,
    string? Color);

/// <summary>Bir kararın kaydı. Yalnızca eklenir, hiçbir zaman silinmez.</summary>
public sealed class Disposition
{
    public int Seq { get; set; }
    public Decision Decision { get; set; }
    public DecisionSource Source { get; set; }

    /// <summary>Reinspect ya da rework için hedef operasyon. Aynı istasyon ise null.</summary>
    public string? TargetOperation { get; set; }

    public string? Note { get; set; }
    public string? Reference { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.Now;
    public string DecidedBy { get; set; } = "";

    /// <summary>Undo kaydında: geri alınan kararın sıra numarası.</summary>
    public int? UndoesSeq { get; set; }

    /// <summary>Rework ve rework sonrası kararlarda o andaki ölçümler. Diğer kararlarda boş.</summary>
    public MeasurementSnapshot? Snapshot { get; set; }
}
