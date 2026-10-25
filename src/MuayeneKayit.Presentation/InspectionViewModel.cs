using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Muayene ekranının durumu ve kuralları. WinUI'ye bağlı değildir: depo ve denetim kaydı portlardan gelir,
/// renkler ton olarak, bildirimler olayla iletilir, diyaloglar IDialogService, pencereler IWindowService, dosya seçimi IFilePicker,
/// PDF IPdfConverter ile yapılır. Fotoğraf, rapor ve öneri akışları InspectionViewModel.Photos.cs içindedir.
/// </summary>
public sealed partial class InspectionViewModel : ObservableObject
{
    private readonly ISessionStore _store;
    private readonly IAuditTrail _audit;
    private readonly IReadOnlyList<DefectTypeTemplate> _types;
    private readonly IDialogService _dialogs;
    private readonly IFilePicker _filePicker;
    private readonly IWindowService _windows;
    private readonly IPdfConverter _pdf;
    private readonly IUserSettings _settings;

    private string? _folder;
    private Session? _session;

    /// <summary>
    /// İşlemi yapan muayeneci (Muayeneci kutusu). Kayıtlı bir muayeneyi başka biri açabilir;
    /// denetim kaydı ve kararlar oturumu oluşturana değil, o anki kullanıcıya yazılır.
    /// </summary>
    private string _user = "";

    /// <summary>Detayda gösterilen kusur; başka kusura geçilince adımlar sıradaki adıma döner.</summary>
    private int? _shownId;

    /// <summary>Liste yenilenirken ListView'in ara seçim olayları yok sayılır (sekme ve seçim korunur).</summary>
    private bool _refreshing;

    /// <summary>Seçili kusurun değerleri forma yazılırken otomatik kayıt çalışmaz.</summary>
    private bool _loadingForm;

    /// <summary>Otomatik kayıtta aynı kişinin bu süre içindeki ölçüm düzeltmeleri geçmişte tek satırda birleşir.</summary>
    private static readonly TimeSpan MeasurementMergeWindow = TimeSpan.FromMinutes(10);

    public InspectionViewModel(ISessionStore store, IAuditTrail audit, IReadOnlyList<DefectTypeTemplate> types, IDialogService dialogs,
        IFilePicker filePicker, IWindowService windows, IPdfConverter pdf, IUserSettings settings)
    {
        _store = store;
        _audit = audit;
        _types = types;
        _dialogs = dialogs;
        _filePicker = filePicker;
        _windows = windows;
        _pdf = pdf;
        _settings = settings;
        Defects = new ObservableCollection<DefectItemViewModel>();
        ApplyDecisionState(null);
        ApplyUnitHeaders(settings.Unit);
    }

    // ───────────────────────── Bağımlılıklar (View kullanır) ─────────────────────────

    public ISessionStore Store => _store;
    public IAuditTrail Audit => _audit;
    public IReadOnlyList<DefectTypeTemplate> Types => _types;
    public string[] TypeNames => _types.Select(t => t.Name).ToArray();

    public string? Folder => _folder;
    public Session? Session => _session;
    public string User => _user;

    /// <summary>Bildirim (durum çubuğu). View, StatusKind'ı InfoBar önem derecesine çevirir.</summary>
    public event Action<string, StatusKind>? Notify;

    // ───────────────────────── Oturum girişi ─────────────────────────

    [ObservableProperty] private string _partNumber = "";
    [ObservableProperty] private string _operationNumber = "";
    [ObservableProperty] private string _serialNumber = "";
    [ObservableProperty] private string _inspectorName = "";

    // ───────────────────────── Oturum ─────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateReport))]
    private bool _isSessionOpen;
    [ObservableProperty] private bool _isSessionClosed = true;
    [ObservableProperty] private string _sessionTitle = "";
    [ObservableProperty] private string _sessionSubtitle = "";
    [ObservableProperty] private string _defectCountText = "Kusurlar";
    [ObservableProperty] private bool _isCompletePrimary;
    [ObservableProperty] private bool _isAddDefectPrimary;

    // ───────────────────────── Liste ─────────────────────────

    public ObservableCollection<DefectItemViewModel> Defects { get; }

    [ObservableProperty] private DefectItemViewModel? _selectedDefect;
    [ObservableProperty] private bool _isListEmpty = true;
    [ObservableProperty] private bool _hasDefects;

    // ───────────────────────── Seçili kusur ─────────────────────────

    [ObservableProperty] private Defect? _current;
    [ObservableProperty] private int? _selectedId;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _noSelection = true;
    [ObservableProperty] private string _header = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private Tone _stateTone = Tone.Neutral;
    [ObservableProperty] private string _nextStepText = "";
    [ObservableProperty] private bool _showNextStep;
    [ObservableProperty] private string _sentence = "";
    [ObservableProperty] private string _deltaText = "";
    [ObservableProperty] private bool _showDelta;
    [ObservableProperty] private bool _isAwaiting;
    [ObservableProperty] private bool _showMeasureArea = true;
    [ObservableProperty] private string _awaitTitle = "";
    [ObservableProperty] private string _awaitText = "";

    [ObservableProperty] private string _depthText = "";
    [ObservableProperty] private string _widthText = "";
    [ObservableProperty] private string _lengthText = "";
    [ObservableProperty] private string _radiusText = "";
    [ObservableProperty] private string _angleText = "";

    /// <summary>Ölçüm kutusu başlıkları; seçili kusurun birimine göre ("Derinlik (in)"), kusur yokken ayardaki birim.</summary>
    [ObservableProperty] private string _depthHeader = "";
    [ObservableProperty] private string _widthHeader = "";
    [ObservableProperty] private string _lengthHeader = "";
    [ObservableProperty] private string _radiusHeader = "";
    [ObservableProperty] private string _colorText = "";
    [ObservableProperty] private string _notesText = "";
    [ObservableProperty] private bool _highMetal;

    /// <summary>Son otomatik kaydın saati ("Otomatik kaydedildi 14:32"); başka kusura geçilince boşalır.</summary>
    [ObservableProperty] private string _autoSaveText = "";

    partial void OnHighMetalChanged(bool value) => AutoSave();

    [ObservableProperty] private bool _showDepth = true;
    [ObservableProperty] private bool _showWidth = true;
    [ObservableProperty] private bool _showLength = true;
    [ObservableProperty] private bool _showRadius = true;
    [ObservableProperty] private bool _showAngle = true;
    [ObservableProperty] private bool _showColor = true;

    [ObservableProperty] private bool _isEditable;
    [ObservableProperty] private bool _isReadOnly = true;
    [ObservableProperty] private bool _canVoid;
    [ObservableProperty] private IReadOnlyList<string> _history = Array.Empty<string>();

    /// <summary>Seçili kusurun fotoğraf kartları (Fotoğraflar sekmesi).</summary>
    public ObservableCollection<PhotoItemViewModel> Photos { get; } = new();

    [ObservableProperty] private bool _hasPhotos;
    [ObservableProperty] private bool _noPhotos = true;

    [ObservableProperty] private string _measureTabLabel = "1. Ölçüm";
    [ObservableProperty] private string _photoTabLabel = "2. Fotoğraflar";
    [ObservableProperty] private string _decisionTabLabel = "3. Disposition";
    [ObservableProperty] private string _historyTabLabel = "Geçmiş";

    [ObservableProperty] private InspectionStep? _currentStep;
    [ObservableProperty] private bool _isSavePrimary;
    [ObservableProperty] private bool _isCameraPrimary;

    /// <summary>
    /// Seçili sekme. Başka kusur seçilince sıradaki adıma (yoksa Ölçüm'e) gider; aynı kusur yenilenince değişmez.
    /// </summary>
    [ObservableProperty] private InspectionStep _selectedStep = InspectionStep.Measure;

    [ObservableProperty] private IReadOnlyList<DecisionOption> _decisionOptions = Array.Empty<DecisionOption>();
    [ObservableProperty] private bool _hasDecisionOptions;
    [ObservableProperty] private string _decisionHint = "";
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private string _undoText = "";

    // ───────────────────────── Oturum işlemleri ─────────────────────────

    public void OpenSession() => Run(() =>
    {
        var input = new Session
        {
            PartNumber = PartNumber.Trim(),
            OperationNumber = OperationNumber.Trim(),
            SerialNumber = SerialNumber.Trim(),
            Inspector = InspectorName.Trim(),
            Project = PartNumber.Trim(),
        };

        if (string.IsNullOrWhiteSpace(input.Inspector))
            throw new ArgumentException("Muayeneci adı boş olamaz.");

        var existed = _store.LoadSession(_store.FolderFor(input.PartNumber, input.OperationNumber, input.SerialNumber)) is not null;
        (_folder, _session) = _store.OpenOrCreate(input);
        _user = input.Inspector;
        _audit.Append(_user, "session.open", _folder);
        IsSessionOpen = true;
        IsSessionClosed = false;
        ClearSelection();
        Refresh(null);
        Send(existed
            ? $"Kayıtlı muayene açıldı: {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}"
            : $"Yeni muayene oluşturuldu: {_session.PartNumber} / {_session.OperationNumber} / {_session.SerialNumber}",
            StatusKind.Success);
    });

    /// <summary>Başka bir parça / seri açmak için muayene seçimine döner. Kayıtlar zaten diske yazılmıştır.</summary>
    public void ChangeSession()
    {
        AutoSave(reload: false);
        ClearSelection();
        _session = null;
        _folder = null;
        WithoutSelectionEvents(() =>
        {
            Defects.Clear();
            SelectedDefect = null;
        });
        IsListEmpty = true;
        HasDefects = false;
        IsSessionOpen = false;
        IsSessionClosed = true;
    }

    public void RequireSession()
    {
        if (_folder is null || _session is null)
            throw new InvalidOperationException("Önce bir oturum açın.");
    }

    public void RequireSelection()
    {
        RequireSession();
        if (SelectedId is null)
            throw new InvalidOperationException("Önce listeden bir kusur seçin.");
    }

    // ───────────────────────── Kusur listesi ─────────────────────────

    /// <summary>Kusur ekler ve seçer. Başarılıysa true döner (View ekleme penceresini kapatır).</summary>
    public bool AddDefect(string? type)
    {
        var added = false;
        Run(() =>
        {
            RequireSession();
            var name = type ?? throw new InvalidOperationException("Kusur tipi seçin.");
            AutoSave(reload: false);

            var spec = _types.FirstOrDefault(t => t.Name == name)?.SpecName;
            // Birim o anki ayardan alınır ve kusura yazılır; ayar sonradan değişse de bu kusur aynı birimde kalır.
            var defect = _store.AddDefect(_folder!, name, d => d.SpecName = spec, _settings.Unit);
            _audit.Append(_user, "defect.add", $"#{defect.Id} {name}");
            added = true;
            Refresh(defect.Id);
            Send($"Kusur #{defect.Id} ({name}) eklendi. Ölçümleri girin.", StatusKind.Success);
        });
        return added;
    }

    /// <summary>Listeden kusur seçer (null: seçimi kaldırır).</summary>
    public void Select(int? id) =>
        SelectedDefect = id is null ? null : Defects.FirstOrDefault(d => d.Id == id);

    partial void OnSelectedDefectChanged(DefectItemViewModel? value)
    {
        if (_refreshing)
            return;

        // Liste yenilenince (öğeler silinip yeniden eklenince) ListView seçimi bir an boşa düşürüp aynı kusuru
        // yeniden seçer. Bu "yeni kusur seçildi" sayılırsa sekme sıradaki adıma atlar. Seçili kusur hâlâ listedeyse
        // boş seçim yok sayılır ve aynı kusur korunur.
        if (value is null && SelectedId is int keepId && _folder is not null)
        {
            var same = Defects.FirstOrDefault(d => d.Id == keepId);
            if (same is not null)
            {
                WithoutSelectionEvents(() => SelectedDefect = same);
                return;
            }
        }

        // Önceki kusurun kaydedilmemiş form değerleri, yeni kusur yüklenmeden yazılır (liste birazdan zaten yenilenir).
        AutoSave(reload: false);

        if (value is not null && _folder is not null)
        {
            SelectedId = value.Id;
            LoadSelected();
        }
        else if (_folder is not null)
        {
            ClearSelection();
        }
    }

    /// <summary>Listeyi diskten yeniler; seçim (selectId ya da mevcut) korunur, sekme değişmez.</summary>
    public void Refresh(int? selectId)
    {
        var defects = _store.LoadDefects(_folder!);
        var rows = defects.Select(ToItem).ToList();

        var keep = selectId ?? SelectedId;
        var match = rows.FirstOrDefault(r => r.Id == keep);

        WithoutSelectionEvents(() =>
        {
            Defects.Clear();
            foreach (var row in rows)
                Defects.Add(row);
            SelectedDefect = match;
        });

        IsListEmpty = rows.Count == 0;
        HasDefects = rows.Count > 0;
        UpdateSessionHeader(defects);

        if (match is not null)
        {
            SelectedId = match.Id;
            LoadSelected(defects);
        }
        else
        {
            ClearSelection();
        }
    }

    private void WithoutSelectionEvents(Action action)
    {
        var was = _refreshing;
        _refreshing = true;
        try
        {
            action();
        }
        finally
        {
            _refreshing = was;
        }
    }

    private static DefectItemViewModel ToItem(Defect d)
    {
        var state = DefectStateMachine.GetState(d);
        return new DefectItemViewModel
        {
            Id = d.Id,
            Number = $"#{d.Id}",
            Type = DefectPresentation.TypeText(d),
            StateText = DefectPresentation.StateLabel(state),
            Tone = DefectPresentation.ToneFor(state),
            PhotoText = d.Photos.Count == 0 ? "fotoğraf yok" : $"{d.Photos.Count} fotoğraf",
            NextStep = DefectPresentation.NextStepFor(d, state),
        };
    }

    private void UpdateSessionHeader(IReadOnlyList<Defect> defects)
    {
        if (_session is null)
            return;
        var open = SessionGate.BlockingDefects(defects).Count;
        var visible = defects.Count(d => DefectStateMachine.GetState(d) != DefectState.Voided);
        SessionTitle = $"{_session.PartNumber}  ·  {_session.OperationNumber}  ·  {_session.SerialNumber}";
        SessionSubtitle = $"Muayeneci: {_user}   ·   {visible} kusur, {open} açık   ·   " +
                          (_session.Status == SessionStatus.Completed ? "Tamamlandı" : "Devam ediyor");
        DefectCountText = visible == 0 ? "Kusurlar" : $"Kusurlar ({visible})";
        IsCompletePrimary = open == 0 && _session.Status != SessionStatus.Completed;
        IsAddDefectPrimary = visible == 0;
    }

    // ───────────────────────── Seçili kusur ─────────────────────────

    private void ClearSelection()
    {
        SelectedId = null;
        Current = null;
        _shownId = null;
        WithoutSelectionEvents(() => SelectedDefect = null);

        ApplyUnitHeaders(_settings.Unit);
        HasSelection = false;
        NoSelection = true;
        ShowDelta = false;
        History = Array.Empty<string>();
        HistoryTabLabel = "Geçmiş";
        UpdatePhotos(null);
        CurrentStep = null;
        IsSavePrimary = false;
        IsCameraPrimary = false;
        IsEditable = false;
        IsReadOnly = true;
        ApplyDecisionState(null);
    }

    private void LoadSelected(IReadOnlyList<Defect>? loaded = null)
    {
        var defect = (loaded ?? _store.LoadDefects(_folder!)).FirstOrDefault(d => d.Id == SelectedId);
        if (defect is null)
        {
            ClearSelection();
            return;
        }

        var state = DefectStateMachine.GetState(defect);
        var newDefect = _shownId != defect.Id;
        _shownId = defect.Id;

        HasSelection = true;
        NoSelection = false;
        Header = $"#{defect.Id}  {defect.DefectType}{(defect.HighMetal ? " + High metal" : "")}";
        StateTone = DefectPresentation.ToneFor(state);
        StateText = DefectPresentation.StateLabel(state);
        NextStepText = DefectPresentation.NextStepFor(defect, state);
        Sentence = DefectSentence.Describe(defect);
        ApplyUnitHeaders(defect.Unit);
        ApplyStepGuidance(defect, state, selectCurrent: newDefect);

        // Değişim satırı yalnızca rework sonrası gerçekten yeniden ölçüldüyse ("aynı kusur devam") gösterilir.
        var effective = DefectStateMachine.Effective(defect);
        var lastDecision = effective.LastOrDefault(x => x.Source != DecisionSource.System);
        var reworkBefore = lastDecision?.Decision == Decision.AfterReworkSameDefect
            ? effective.TakeWhile(x => !ReferenceEquals(x, lastDecision)).LastOrDefault(x => x.Decision == Decision.Rework)?.Snapshot
            : null;
        DeltaText = reworkBefore is null
            ? ""
            : "Rework sonrası yeniden ölçüldü. Rework öncesine göre: " + string.Join("   ·   ", MeasurementCompare.Lines(reworkBefore, defect, defect.Unit));
        ShowDelta = reworkBefore is not null;

        // Rework'teyken ölçüm kutuları yerine ne yapılacağını anlatan kutu gösterilir.
        var awaiting = state == DefectState.AwaitingReinspect;
        ShowMeasureArea = !awaiting;
        if (awaiting)
        {
            var sent = effective.LastOrDefault(x => x.Decision is Decision.Rework or Decision.Reinspect);
            var where = string.IsNullOrWhiteSpace(sent?.TargetOperation) ? "" : $" · {sent!.TargetOperation} operasyonunda yeniden muayene edilecek";
            AwaitTitle = (sent?.Decision == Decision.Reinspect ? "Yeniden kontrol bekliyor" : "Parça rework'te") + where;
            AwaitText = $"{(sent?.Decision == Decision.Reinspect ? "Önceki" : "Rework öncesi")} ölçümler: " +
                        $"{DefectPresentation.SnapshotText(sent?.Snapshot ?? MeasurementEditor.Snapshot(defect), defect.Unit)}. " +
                        "Ölçümler kilitli. Parça geldiğinde aşağıdaki düğmeyle muayene sonucunu girin.";
        }

        _loadingForm = true;
        try
        {
            DepthText = DefectPresentation.Fmt(defect.Depth, defect.Unit);
            WidthText = DefectPresentation.Fmt(defect.Width, defect.Unit);
            LengthText = DefectPresentation.Fmt(defect.Length, defect.Unit);
            RadiusText = DefectPresentation.Fmt(defect.Radius, defect.Unit);
            AngleText = DefectPresentation.Fmt(defect.AngleDeg);
            ColorText = defect.Color ?? "";
            NotesText = defect.Notes ?? "";
            HighMetal = defect.HighMetal;
        }
        finally
        {
            _loadingForm = false;
        }
        if (newDefect)
            AutoSaveText = "";

        ApplyTemplateFields(defect);

        History = DefectPresentation.HistoryLines(defect);
        var historyCount = DefectStateMachine.Effective(defect).Count + defect.MeasurementEdits.Count;
        HistoryTabLabel = historyCount == 0 ? "Geçmiş" : $"Geçmiş ({historyCount})";

        IsEditable = state is not (DefectState.Closed or DefectState.Voided or DefectState.Superseded
            or DefectState.AwaitingReinspect);
        IsReadOnly = !IsEditable;
        ApplyDecisionState(defect);

        // Fotoğraf kartları Current değişince çizilir; bu yüzden en son atanır.
        UpdatePhotos(defect);
        Current = defect;
    }

    private void ApplyUnitHeaders(LengthUnit unit)
    {
        DepthHeader = Units.Header("Derinlik", unit);
        WidthHeader = Units.Header("Genişlik", unit);
        LengthHeader = Units.Header("Uzunluk", unit);
        RadiusHeader = Units.Header("Yarıçap", unit);
    }

    /// <summary>
    /// Ölçüm adımı atlanıyor mu: düzenlenebilir kusurun şablonunda ölçüm alanı var ama kaydedilmiş ölçüm yok.
    /// </summary>
    public bool IsSkippingMeasurement =>
        Current is Defect d && IsEditable && !DefectPresentation.HasMeasurement(d)
        && (ShowDepth || ShowWidth || ShowLength || ShowRadius || ShowAngle || ShowColor);

    /// <summary>
    /// Fotoğraf adımına geçmek istenince çağrılır. Ölçüm girilmemişse uyarı sorulur; kullanıcı kabul ederse
    /// (ya da ölçüm varsa) sekme fotoğrafa geçer ve true döner.
    /// </summary>
    public async Task<bool> GoToPhotoStepAsync()
    {
        AutoSave();
        if (IsSkippingMeasurement && !await _dialogs.ConfirmSkipMeasurementAsync(SelectedId!.Value))
            return false;
        SelectedStep = InspectionStep.Photo;
        return true;
    }

    /// <summary>Ayar değişince kusur seçili değilse başlıklar yeni ayardaki birime döner (seçili kusur kendi birimini korur).</summary>
    public void RefreshUnitHeaders()
    {
        if (Current is null)
            ApplyUnitHeaders(_settings.Unit);
    }

    /// <summary>
    /// Kusur tipinin şablonundaki ölçüm alanlarını gösterir. Şablon dışı bir alanda değer varsa
    /// (eski kayıt) o alan da görünür kalır; veri gizlenmez.
    /// </summary>
    private void ApplyTemplateFields(Defect defect)
    {
        var template = _types.FirstOrDefault(t => string.Equals(t.Name, defect.DefectType, StringComparison.OrdinalIgnoreCase));
        bool Visible(MeasureField field, bool hasValue) => template is null || template.Fields.Contains(field) || hasValue;

        ShowDepth = Visible(MeasureField.Depth, defect.Depth is not null);
        ShowWidth = Visible(MeasureField.Width, defect.Width is not null);
        ShowLength = Visible(MeasureField.Length, defect.Length is not null);
        ShowRadius = Visible(MeasureField.Radius, defect.Radius is not null);
        ShowAngle = Visible(MeasureField.Angle, defect.AngleDeg is not null);
        ShowColor = Visible(MeasureField.Color, !string.IsNullOrWhiteSpace(defect.Color));
    }

    /// <summary>Adım sekmeleri: tamamlanan ✓, sıradaki kalın; vurgulu düğme sıradaki adımdaki düğmedir.</summary>
    private void ApplyStepGuidance(Defect d, DefectState state, bool selectCurrent)
    {
        var step = DefectPresentation.CurrentStep(d, state);
        var measured = DefectPresentation.HasMeasurement(d);
        var photographed = d.Photos.Count > 0;
        // Açık kusurda (ilk karar, rework sonrası ya da yeniden açma) karar bekleniyor: tamamlanmış sayılmaz.
        var decided = state != DefectState.Open && DefectStateMachine.Effective(d).Count > 0;

        MeasureTabLabel = (measured ? "✓ " : "") + "1. Ölçüm";
        PhotoTabLabel = (photographed ? "✓ " : "") + (d.Photos.Count == 0 ? "2. Fotoğraflar" : $"2. Fotoğraflar ({d.Photos.Count})");
        DecisionTabLabel = (decided ? "✓ " : "") + "3. Disposition";

        CurrentStep = step;
        IsSavePrimary = step == InspectionStep.Measure;
        IsCameraPrimary = step == InspectionStep.Photo;

        if (selectCurrent)
            SelectedStep = step ?? InspectionStep.Measure;

        // Yapılacak iş yoksa yönlendirme kutusu gizlenir.
        ShowNextStep = step is not null;
    }

    /// <summary>Karar listesi ve düğmeler, seçili kusurun durumuna göre açılır ya da kapanır.</summary>
    private void ApplyDecisionState(Defect? defect)
    {
        var awaiting = defect is not null && DefectStateMachine.GetState(defect) == DefectState.AwaitingReinspect;
        IsAwaiting = awaiting;
        CanVoid = defect is not null && DefectStateMachine.Check(defect, Decision.Void, DecisionSource.Operator).Ok;

        // Disposition listesi: yalnızca bu durumda geçerli kararlar, iş akışı sırasıyla.
        var options = new List<DecisionOption>();
        if (awaiting)
            options.Add(new DecisionOption("Rework sonrası muayene sonucu…", null, true));
        if (defect is not null)
        {
            foreach (var (decision, label) in DefectPresentation.DecisionOrder)
            {
                if (DefectStateMachine.Check(defect, decision, DefectPresentation.SourceFor(decision)).Ok)
                    options.Add(new DecisionOption(label, decision, false));
            }
        }
        DecisionOptions = options;
        HasDecisionOptions = options.Count > 0;

        var undoable = defect is null ? null : DefectStateMachine.LastUndoable(defect);
        CanUndo = undoable is not null;
        UndoText = undoable is null
            ? ""
            : $"Son karar: {DefectSentence.DecisionLabel(undoable.Decision)} ({undoable.DecidedAt:dd.MM HH:mm}, {undoable.DecidedBy})";

        DecisionHint = defect is null
            ? "Karar vermek için listeden bir kusur seçin."
            : DefectPresentation.DecisionHintFor(defect);
    }

    // ───────────────────────── Ölçüm ve oturum kapanışı ─────────────────────────

    /// <summary>
    /// Ölçüm formunu (değerler, High metal, not) kaydedilmiş hâlinden farklıysa sessizce kaydeder; aynıysa hiçbir şey yazmaz.
    /// Kutudan çıkınca, High metal değişince ve sekme / kusur / oturum değişmeden önce çağrılır.
    /// Geçersiz değerde uyarı verir, kaydetmez. Sekme değişmez (sıradaki adıma yalnızca 'Ölçümleri kaydet' geçer).
    /// </summary>
    public void AutoSave() => AutoSave(reload: true);

    private void AutoSave(bool reload)
    {
        if (_loadingForm || _folder is null || Current is not Defect shown || shown.Id != SelectedId || !IsEditable)
            return;

        Run(() =>
        {
            var (values, notes) = ReadForm();
            if (values == MeasurementEditor.Snapshot(shown) && notes == shown.Notes)
                return;

            StoreForm(shown.Id, values, notes);
            AutoSaveText = $"Otomatik kaydedildi {DateTime.Now:HH:mm}";
            if (reload)
                Refresh(shown.Id);
        });
    }

    private (MeasurementSnapshot Values, string? Notes) ReadForm() => (
        new MeasurementSnapshot(
            DefectPresentation.ParseOptional(DepthText, "Derinlik"),
            DefectPresentation.ParseOptional(WidthText, "Genişlik"),
            DefectPresentation.ParseOptional(LengthText, "Uzunluk"),
            DefectPresentation.ParseOptional(RadiusText, "Yarıçap"),
            DefectPresentation.ParseOptional(AngleText, "Açı"),
            string.IsNullOrWhiteSpace(ColorText) ? null : ColorText.Trim(),
            HighMetal),
        string.IsNullOrWhiteSpace(NotesText) ? null : NotesText.Trim());

    /// <summary>Formu kusura yazar (doğrulama, düzeltme geçmişi, denetim satırı). Ölçüm değiştiyse true.</summary>
    private bool StoreForm(int id, MeasurementSnapshot values, string? notes)
    {
        var by = _user;
        var changed = false;

        _store.UpdateDefect(_folder!, id, d =>
        {
            var probe = new Defect
            {
                DefectType = d.DefectType,
                Depth = values.Depth, Width = values.Width, Length = values.Length,
                Radius = values.Radius, AngleDeg = values.AngleDeg, Color = values.Color,
            };

            // Doğrulama başarısızsa mutate hata fırlatır ve dosya yazılmaz.
            var errors = DefectValidator.Validate(probe);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(" ", errors));

            changed = MeasurementEditor.Apply(d, values, by, mergeWithin: MeasurementMergeWindow);
            d.Notes = notes;
        });

        _audit.Append(by, "defect.measure", $"#{id}");
        return changed;
    }

    public void SaveMeasurements() => Run(() =>
    {
        RequireSelection();

        var (values, notes) = ReadForm();
        var changed = StoreForm(SelectedId!.Value, values, notes);
        Refresh(SelectedId);

        // Ölçüm bitti, fotoğraf yoksa sıradaki adıma geç.
        if (Current is { Photos.Count: 0 } && DefectPresentation.HasMeasurement(Current))
            SelectedStep = InspectionStep.Photo;
        Send(changed
                ? $"#{SelectedId} ölçümleri güncellendi, düzeltme geçmişe yazıldı."
                : $"#{SelectedId} ölçümleri kayıtlı.",
            StatusKind.Success);
    });

    public void Complete() => Run(() =>
    {
        RequireSession();
        AutoSave();
        var defects = _store.LoadDefects(_folder!);
        var blocking = SessionGate.BlockingDefects(defects);

        if (blocking.Count > 0)
        {
            var list = string.Join(", ", blocking.Select(d => $"#{d.Id} ({DefectPresentation.StateLabel(DefectStateMachine.GetState(d))})"));
            Send($"Muayene kapatılamaz, açık kusurlar var: {list}", StatusKind.Warning);
            return;
        }

        _session!.Status = SessionStatus.Completed;
        _store.SaveSession(_folder!, _session);
        _audit.Append(_user, "session.complete", _folder!);
        UpdateSessionHeader(defects);
        Send("Muayene tamamlandı. Rapor oluşturabilirsiniz.", StatusKind.Success);
    });

    // ───────────────────────── Yardımcılar ─────────────────────────

    private void Send(string message, StatusKind kind) => Notify?.Invoke(message, kind);

    /// <summary>Beklenen hataları yakalar ve bildirir. Uygulama kapanmaz.</summary>
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException
                                    or TimeoutException or InvalidDataException or UnauthorizedAccessException)
        {
            Send(ex.Message, StatusKind.Error);
        }
    }
}
