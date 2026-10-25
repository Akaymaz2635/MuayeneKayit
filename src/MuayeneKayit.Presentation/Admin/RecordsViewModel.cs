using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Admin kayıt kartının verisi (tüm seriler, salt okunur).</summary>
public sealed class AdminRecordRowViewModel
{
    public AdminRecordRowViewModel(IndexRow r, string root)
    {
        Row = r;
        Title = $"{r.Part}  ·  {r.Operation}  ·  {r.Serial}";
        Meta = $"Muayeneci: {r.Inspector}   ·   Açılış: {r.CreatedAt:dd.MM.yyyy HH:mm}   ·   {r.TotalDefects} kusur, {r.OpenDefects} açık   ·   {(r.Status == nameof(SessionStatus.Completed) ? "Tamamlandı" : "Açık")}";
        DefectChips = r.Defects.Take(12).Select(d =>
        {
            var (bg, fg) = d.State switch
            {
                nameof(DefectState.Closed) => ("#DCFCE7", "#15803D"),
                nameof(DefectState.MrbPending) => ("#EDE9FE", "#6D28D9"),
                nameof(DefectState.Voided) => ("#F3F4F6", "#6B7280"),
                nameof(DefectState.Superseded) => ("#E0E7FF", "#3730A3"),
                _ => ("#FEF3C7", "#B45309"),
            };
            return new ChipData($"#{d.Id} {d.Type} · {d.State}", bg, fg);
        }).ToList();
        Folder = Path.Combine(root, r.Part, r.Operation, r.Serial);
    }

    public IndexRow Row { get; }
    public string Title { get; }
    public string Meta { get; }
    public IReadOnlyList<ChipData> DefectChips { get; }

    /// <summary>Seri klasörü (köke göre birleştirilmiş).</summary>
    public string Folder { get; }

    /// <summary>Açık kusuru olan kartın çerçevesi vurgulanır.</summary>
    public bool HasOpenDefects => Row.OpenDefects > 0;
}

/// <summary>
/// Admin "Kayıtlar" sayfası: indeksi okur, süzer, özetler; CSV dışa aktarır; klasör ve son raporu açar.
/// Muayene kayıtlarını değiştirmez.
/// </summary>
public sealed partial class RecordsViewModel : AdminViewModelBase
{
    public const string AllTypes = "Tümü";
    private const int MaxCards = 500;

    private readonly AdminContext _ctx;
    private readonly IFileSaver _saver;
    private readonly IShell _shell;
    private readonly IClock _clock;
    private List<IndexRow> _all = new();
    private bool _suspended;

    public RecordsViewModel(AdminContext ctx, IFileSaver saver, IShell shell, IClock clock)
    {
        _ctx = ctx;
        _saver = saver;
        _shell = shell;
        _clock = clock;
        _typeOptions = new[] { AllTypes };
        _typeFilter = AllTypes;
        _stateFilter = DashboardViewModel.StateFilters[0];
    }

    public IReadOnlyList<StateFilterOption> StateOptions => DashboardViewModel.StateFilters;

    [ObservableProperty] private IReadOnlyList<string> _typeOptions;
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

    [ObservableProperty] private IReadOnlyList<SummaryCardData> _summaryCards = Array.Empty<SummaryCardData>();

    /// <summary>Boşsa açıklama metni; doluysa null.</summary>
    [ObservableProperty] private string? _emptyText;

    /// <summary>Süzülmüş kartlar (en çok 500). Değişince View özeti ve kartları yeniden çizer (en son atanır).</summary>
    [ObservableProperty] private IReadOnlyList<AdminRecordRowViewModel> _rows = Array.Empty<AdminRecordRowViewModel>();

    /// <summary>Hata tipi listesini yeniler ve süzgeci "Tümü"ne çeker (kayıtlar hemen yeniden süzülmez).</summary>
    public void SetTypes(IEnumerable<string> names)
    {
        _suspended = true;
        try
        {
            TypeOptions = new[] { AllTypes }.Concat(names).ToList();
            TypeFilter = AllTypes;
        }
        finally
        {
            _suspended = false;
        }
    }

    /// <summary>Indeksi diskten yeniden üretir ve süzgeçleri uygular.</summary>
    public void Refresh() => Run(() =>
    {
        _all = IndexBuilder.Rebuild(_ctx.Store);
        ApplyFilters();
    });

    private List<IndexRow> Filtered()
    {
        var type = TypeFilter;
        return IndexFilter.Apply(_all, new IndexCriteria(
            PartFilter, OperationFilter, SerialFilter,
            type is null or AllTypes ? null : type,
            StateFilter?.Filter ?? IndexStateFilter.All));
    }

    private void ApplyFilters()
    {
        if (_suspended)
            return;

        var list = Filtered();
        SummaryCards = new[]
        {
            new SummaryCardData("Kayıt", list.Count.ToString(), "#1A3A5C"),
            new SummaryCardData("Açık kusurlu", list.Count(r => r.OpenDefects > 0).ToString(), "#B45309"),
            new SummaryCardData("MRB bekleyen", list.Count(r => r.Defects.Any(d => d.State == nameof(DefectState.MrbPending))).ToString(), "#7C3AED"),
            new SummaryCardData("Tamamlanan", list.Count(r => r.Status == nameof(SessionStatus.Completed)).ToString(), "#15803D"),
        };
        EmptyText = list.Count == 0 ? "Kriterlere uyan kayıt yok." : null;
        Rows = list.Take(MaxCards).Select(r => new AdminRecordRowViewModel(r, _ctx.Root)).ToList();
    }

    /// <summary>Klasörü Gezgin'de açar. Yol, kök altında olduğu doğrulanmış index satırından gelir.</summary>
    public void OpenFolder(AdminRecordRowViewModel row) => Run(() =>
    {
        var full = Path.GetFullPath(row.Folder);
        if (!full.StartsWith(Path.GetFullPath(_ctx.Root), StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full))
            throw new InvalidOperationException("Klasör bulunamadı.");
        _shell.OpenFolder(full);
    });

    public void OpenLatestReport(AdminRecordRowViewModel row) => Run(() =>
    {
        var version = ReportFiles.LatestVersion(row.Folder);
        if (version == 0)
            throw new InvalidOperationException("Bu kayıt için rapor yok.");
        var pdf = Path.Combine(row.Folder, "reports", $"Rapor_v{version}.pdf");
        var html = Path.ChangeExtension(pdf, ".html");
        _shell.OpenFile(File.Exists(pdf) ? pdf : html);
    });

    /// <summary>Süzülmüş kayıtların tamamını (kart sınırı olmadan) UTF-8 BOM'lu CSV olarak yazar.</summary>
    public async Task ExportCsvAsync()
    {
        try
        {
            var path = await _saver.PickCsvPathAsync($"MuayeneKayit_{_clock.Now:yyyyMMdd_HHmm}");
            if (path is null)
                return;

            var list = Filtered();
            await File.WriteAllTextAsync(path, CsvExport.Build(list), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Say($"{list.Count} kayıt dışa aktarıldı: {path}", StatusKind.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say(ex.Message, StatusKind.Error);
        }
    }
}
