using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Durum süzgeci seçeneği (açılır liste).</summary>
public sealed record StateFilterOption(string Label, IndexStateFilter Filter);

/// <summary>Renkli etiket. Renkler #RRGGBB'dir; fırçayı View üretir.</summary>
public sealed record ChipData(string Text, string Background, string Foreground);

/// <summary>Özet kartı: değer, başlık ve vurgu rengi (#RRGGBB).</summary>
public sealed record SummaryCardData(string Label, string Value, string Accent);

/// <summary>Dashboard sonuç kartının verisi.</summary>
public sealed class DashboardRowViewModel
{
    public DashboardRowViewModel(IndexRow row)
    {
        Row = row;
        Title = $"{row.Part}  ·  {row.Operation}  ·  {row.Serial}";

        Badge = row.Status == nameof(SessionStatus.Completed)
            ? new ChipData("Tamamlandı", "#DCFCE7", "#15803D")
            : row.OpenDefects > 0
                ? new ChipData($"{row.OpenDefects} açık kusur", "#FEF3C7", "#B45309")
                : new ChipData("Açık kusur yok, kapanabilir", "#E0F2FE", "#0369A1");

        Meta = string.Join("   ·   ", new[]
        {
            string.IsNullOrWhiteSpace(row.Inspector) ? null : $"Muayeneci: {row.Inspector}",
            row.CreatedAt is DateTime c ? $"Açılış: {c:dd.MM.yyyy HH:mm}" : null,
            $"{row.TotalDefects} kusur",
        }.Where(x => x is not null));

        DefectChips = row.Defects.OrderBy(x => x.Id).Select(d =>
        {
            var state = Enum.TryParse<DefectState>(d.State, out var ds) ? ds : DefectState.Open;
            var (bg, fg) = StateColors(state);
            return new ChipData($"#{d.Id} {d.Type} · {DefectPresentation.StateLabel(state)}", bg, fg);
        }).ToList();
    }

    public IndexRow Row { get; }
    public string Title { get; }
    public ChipData Badge { get; }
    public string Meta { get; }
    public IReadOnlyList<ChipData> DefectChips { get; }

    /// <summary>Açık kusuru olan kartın çerçevesi vurgulanır.</summary>
    public bool HasOpenDefects => Row.OpenDefects > 0;

    private static (string Bg, string Fg) StateColors(DefectState s) => s switch
    {
        DefectState.Closed => ("#DCFCE7", "#15803D"),
        DefectState.Voided => ("#E5E7EB", "#6B7280"),
        DefectState.Superseded => ("#E0E7FF", "#3730A3"),
        DefectState.MrbPending => ("#EDE9FE", "#6D28D9"),
        _ => ("#FEF3C7", "#B45309"),
    };
}

/// <summary>
/// Dashboard: indeks satırlarını okur, süzer ve özetler; denetim zincirini doğrular. WinUI'ye bağlı değildir;
/// kartların çizimi View'dadır.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    public const string AllTypes = "Tümü";

    public static IReadOnlyList<StateFilterOption> StateFilters { get; } = new[]
    {
        new StateFilterOption("Tümü", IndexStateFilter.All),
        new StateFilterOption("Açık kusuru olanlar", IndexStateFilter.HasOpen),
        new StateFilterOption("MRB bekleyen", IndexStateFilter.MrbPending),
        new StateFilterOption("Rework sonrası muayene bekleyen", IndexStateFilter.AwaitingReinspect),
        new StateFilterOption("Tedarikçide", IndexStateFilter.WaitingVendor),
        new StateFilterOption("Tamamlanan muayeneler", IndexStateFilter.Completed),
    };

    private readonly ISessionStore _store;
    private List<IndexRow> _all = new();
    private bool _suspended;

    public DashboardViewModel(ISessionStore store, IReadOnlyList<DefectTypeTemplate> types)
    {
        _store = store;
        TypeOptions = new[] { AllTypes }.Concat(types.Select(t => t.Name)).ToList();
        _typeFilter = AllTypes;
        _stateFilter = StateFilters[0];
    }

    /// <summary>Bildirim (ana pencerenin durum çubuğu).</summary>
    public event Action<string, StatusKind>? Notify;

    /// <summary>Kartın "Aç" düğmesine basıldı: View kaydı muayene ekranında açar.</summary>
    public event Action<IndexRow>? OpenRequested;

    // ───────────────────────── Süzgeçler ─────────────────────────

    public IReadOnlyList<string> TypeOptions { get; }
    public IReadOnlyList<StateFilterOption> StateOptions => StateFilters;

    [ObservableProperty] private string _partFilter = "";
    [ObservableProperty] private string _operationFilter = "";
    [ObservableProperty] private string _serialFilter = "";
    [ObservableProperty] private string? _typeFilter;
    [ObservableProperty] private StateFilterOption? _stateFilter;

    partial void OnPartFilterChanged(string value) => ApplyFilters();
    partial void OnOperationFilterChanged(string value) => ApplyFilters();
    partial void OnSerialFilterChanged(string value) => ApplyFilters();
    partial void OnTypeFilterChanged(string? value) => ApplyFilters();
    partial void OnStateFilterChanged(StateFilterOption? value) => ApplyFilters();

    // ───────────────────────── Sonuçlar ─────────────────────────

    [ObservableProperty] private IReadOnlyList<SummaryCardData> _summaryCards = Array.Empty<SummaryCardData>();

    /// <summary>Boşsa açıklama metni (kayıt yok / süzgeçe uyan yok); doluysa null.</summary>
    [ObservableProperty] private string? _emptyText;

    /// <summary>Süzülmüş kartlar. Değişince View hem özeti hem kartları yeniden çizer (en son atanır).</summary>
    [ObservableProperty] private IReadOnlyList<DashboardRowViewModel> _rows = Array.Empty<DashboardRowViewModel>();

    // ───────────────────────── Denetim doğrulama sonucu ─────────────────────────

    [ObservableProperty] private bool _isAuditOpen;
    [ObservableProperty] private string _auditTitle = "";
    [ObservableProperty] private string _auditMessage = "";
    [ObservableProperty] private StatusKind _auditKind = StatusKind.Info;

    // ───────────────────────── İşlemler ─────────────────────────

    /// <summary>Indeksi diskten yeniden üretir ve süzgeçleri uygular.</summary>
    public void Refresh() => Run(() =>
    {
        var store = new SessionStore(_store.Root);
        _all = IndexBuilder.Rebuild(store);
        ApplyFilters();
    });

    public void ClearFilters()
    {
        _suspended = true;
        try
        {
            PartFilter = "";
            OperationFilter = "";
            SerialFilter = "";
            TypeFilter = AllTypes;
            StateFilter = StateFilters[0];
        }
        finally
        {
            _suspended = false;
        }
        ApplyFilters();
    }

    public void VerifyAudit() => Run(() =>
    {
        var store = new SessionStore(_store.Root);
        var (ok, badLine) = AuditLog.Verify(store);
        AuditTitle = ok ? "Denetim kaydı sağlam" : "Denetim kaydı bozulmuş";
        AuditMessage = ok ? "Zincir baştan sona doğrulandı." : $"{badLine}. satırda zincir kırılmış.";
        AuditKind = ok ? StatusKind.Success : StatusKind.Error;
        IsAuditOpen = true;
    });

    public void Open(DashboardRowViewModel row) => OpenRequested?.Invoke(row.Row);

    private void ApplyFilters()
    {
        if (_suspended)
            return;

        var type = TypeFilter;
        var criteria = new IndexCriteria(
            Part: PartFilter,
            Operation: OperationFilter,
            Serial: SerialFilter,
            DefectType: type is null || type == AllTypes ? null : type,
            State: StateFilter?.Filter ?? IndexStateFilter.All);

        var rows = IndexFilter.Apply(_all, criteria);
        var defects = rows.SelectMany(r => r.Defects).ToList();

        SummaryCards = new[]
        {
            new SummaryCardData("Seri / operasyon", rows.Count.ToString(), "#1A3A5C"),
            new SummaryCardData("Açık kusur", rows.Sum(r => r.OpenDefects).ToString(), "#B45309"),
            new SummaryCardData("MRB bekleyen", defects.Count(d => d.State == nameof(DefectState.MrbPending)).ToString(), "#6D28D9"),
            new SummaryCardData("Rework sonrası bekleyen", defects.Count(d => d.State == nameof(DefectState.AwaitingReinspect)).ToString(), "#1D4ED8"),
            new SummaryCardData("Tamamlanan muayene", rows.Count(r => r.Status == nameof(SessionStatus.Completed)).ToString(), "#15803D"),
        };
        EmptyText = rows.Count > 0 ? null
            : _all.Count == 0
                ? "Henüz kayıt yok. Muayene ekranından oturum açın ya da Yenile'ye basın."
                : "Filtrelere uyan kayıt yok.";
        Rows = rows.Select(r => new DashboardRowViewModel(r)).ToList();
    }

    /// <summary>Beklenen hataları yakalar ve bildirir. Uygulama kapanmaz.</summary>
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException
                                    or FileNotFoundException)
        {
            Notify?.Invoke(ex.Message, StatusKind.Error);
        }
    }
}
