using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Şablon editöründe bir ek ölçüm alanının onay kutusu ("Yükseklik (H)").</summary>
public sealed partial class CustomFieldToggle : ObservableObject
{
    public CustomFieldToggle(string key, string label, bool isChecked)
    {
        Key = key;
        Label = label;
        _isChecked = isChecked;
    }

    public string Key { get; }
    public string Label { get; }

    [ObservableProperty] private bool _isChecked;
}

/// <summary>
/// Admin "Kusur tipleri" sayfası: liste, seçili tipin şablonu (ad, spec adı, 6 yerleşik + ek ölçüm alanları),
/// sıralama, ekleme / silme (onaylı), varsayılanlara dönme ve ortak alana kaydetme. Ek ölçüm alanları (ör. Yükseklik)
/// aynı sayfada tanımlanır: ad, kısaltma ve tür.
/// </summary>
public sealed partial class TypesViewModel : AdminViewModelBase
{
    private readonly AdminContext _ctx;
    private readonly IConfirmService _confirm;
    private List<DefectTypeTemplate> _types = new();
    private List<CustomFieldDef> _fields = new();

    public TypesViewModel(AdminContext ctx, IConfirmService confirm)
    {
        _ctx = ctx;
        _confirm = confirm;
    }

    /// <summary>Kaydedildi: yeni tip adları (Kayıtlar sayfasındaki hata tipi listesi yenilenir).</summary>
    public event Action<IReadOnlyList<string>>? Saved;

    /// <summary>Listedeki tip adları (şu anki, kaydedilmemiş hâliyle).</summary>
    public IReadOnlyList<string> TypeNames => _types.Select(t => t.Name).ToList();

    /// <summary>Ek alan türü seçenekleri (FieldKindIndex bu sırayla).</summary>
    public IReadOnlyList<string> FieldKindOptions { get; } =
        Enum.GetValues<CustomFieldKind>().Select(CustomMeasures.KindLabel).ToList();

    [ObservableProperty] private IReadOnlyList<string> _items = Array.Empty<string>();
    [ObservableProperty] private int _selectedIndex = -1;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _specName = "";
    [ObservableProperty] private bool _fieldDepth;
    [ObservableProperty] private bool _fieldWidth;
    [ObservableProperty] private bool _fieldLength;
    [ObservableProperty] private bool _fieldRadius;
    [ObservableProperty] private bool _fieldAngle;
    [ObservableProperty] private bool _fieldColor;

    /// <summary>Seçili tipin ek alan onay kutuları.</summary>
    public ObservableCollection<CustomFieldToggle> CustomToggles { get; } = new();

    [ObservableProperty] private bool _hasCustomFields;

    // Ek ölçüm alanı editörü
    [ObservableProperty] private IReadOnlyList<string> _fieldItems = Array.Empty<string>();
    [ObservableProperty] private int _selectedFieldIndex = -1;
    [ObservableProperty] private string _fieldName = "";
    [ObservableProperty] private string _fieldAbbrev = "";
    [ObservableProperty] private int _fieldKindIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _dirtyText = "";

    public bool IsDirty => DirtyText.Length > 0;

    /// <summary>Ortak alandaki tipleri ve ek alanları yükler (giriş sonrası).</summary>
    public void Load()
    {
        _types = SharedConfig.LoadDefectTypes(_ctx.Root).Select(Clone).ToList();
        _fields = SharedConfig.LoadCustomFields(_ctx.Root).Select(CloneField).ToList();
        RefreshFields(null);
        RefreshList(0);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value < 0 || value >= _types.Count)
            return;
        var t = _types[value];
        Name = t.Name;
        SpecName = t.SpecName ?? "";
        FieldDepth = t.Fields.Contains(MeasureField.Depth);
        FieldWidth = t.Fields.Contains(MeasureField.Width);
        FieldLength = t.Fields.Contains(MeasureField.Length);
        FieldRadius = t.Fields.Contains(MeasureField.Radius);
        FieldAngle = t.Fields.Contains(MeasureField.Angle);
        FieldColor = t.Fields.Contains(MeasureField.Color);
        RefreshToggles(t);
    }

    private void RefreshToggles(DefectTypeTemplate? t)
    {
        CustomToggles.Clear();
        foreach (var f in _fields)
            CustomToggles.Add(new CustomFieldToggle(f.Key, $"{f.Name} ({f.Abbrev})", t?.CustomFields.Contains(f.Key) == true));
        HasCustomFields = _fields.Count > 0;
    }

    private void RefreshList(int? select)
    {
        // Liste yenilenince seçim önce düşer, sonra yeniden seçilir (alanlar yeniden yüklensin).
        SelectedIndex = -1;
        Items = _types.Select(t => $"{t.Name}   ({FieldsText(t)})").ToList();
        if (select is int i && i >= 0 && i < _types.Count)
            SelectedIndex = i;
        else
            RefreshToggles(null);
    }

    private string FieldsText(DefectTypeTemplate t)
    {
        var labels = t.Fields.Select(FieldLabel)
            .Concat(SharedConfig.FieldsOf(t, _fields).Select(f => f.Abbrev))
            .ToList();
        return labels.Count == 0 ? "ölçüm yok" : string.Join(", ", labels);
    }

    private static string FieldLabel(MeasureField f) => f switch
    {
        MeasureField.Depth => "D",
        MeasureField.Width => "W",
        MeasureField.Length => "L",
        MeasureField.Radius => "Ç",
        MeasureField.Angle => "Açı",
        MeasureField.Color => "Renk",
        _ => f.ToString(),
    };

    private List<MeasureField> CheckedFields()
    {
        var fields = new List<MeasureField>();
        if (FieldDepth) fields.Add(MeasureField.Depth);
        if (FieldWidth) fields.Add(MeasureField.Width);
        if (FieldLength) fields.Add(MeasureField.Length);
        if (FieldRadius) fields.Add(MeasureField.Radius);
        if (FieldAngle) fields.Add(MeasureField.Angle);
        if (FieldColor) fields.Add(MeasureField.Color);
        return fields;
    }

    /// <summary>Düzenlenen şablonu seçili tipe uygular (doğrulamalı); henüz ortak alana yazılmaz.</summary>
    public void Apply() => Run(() => ApplyEditor(requireSelection: true));

    private DefectTypeTemplate EditorCandidate() => new()
    {
        Name = Name.Trim(),
        SpecName = string.IsNullOrWhiteSpace(SpecName) ? null : SpecName.Trim(),
        Fields = CheckedFields(),
        CustomFields = CustomToggles.Where(c => c.IsChecked).Select(c => c.Key).ToList(),
    };

    private static bool SameTemplate(DefectTypeTemplate a, DefectTypeTemplate b) =>
        a.Name == b.Name && (a.SpecName ?? "") == (b.SpecName ?? "")
        && a.Fields.OrderBy(f => f).SequenceEqual(b.Fields.OrderBy(f => f))
        && a.CustomFields.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(b.CustomFields.OrderBy(k => k, StringComparer.Ordinal));

    /// <summary>Editördeki şablonu seçili tipe yazar (doğrulamalı, hata fırlatır). Değişiklik yapıldıysa true.</summary>
    private bool ApplyEditor(bool requireSelection)
    {
        var i = SelectedIndex;
        if (i < 0 || i >= _types.Count)
        {
            if (requireSelection)
                throw new InvalidOperationException("Listeden bir tip seçin.");
            return false;
        }

        var candidate = EditorCandidate();
        if (!requireSelection && SameTemplate(candidate, _types[i]))
            return false;

        var trial = _types.ToList();
        trial[i] = candidate;
        var errors = SharedConfig.ValidateTypes(trial);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        _types = trial;
        RefreshList(i);
        MarkDirty();
        return true;
    }

    public void Add()
    {
        var n = 1;
        while (_types.Any(t => t.Name.Equals($"Yeni tip {n}", StringComparison.OrdinalIgnoreCase)))
            n++;
        _types.Add(new DefectTypeTemplate { Name = $"Yeni tip {n}", Fields = { MeasureField.Depth, MeasureField.Width, MeasureField.Length } });
        RefreshList(_types.Count - 1);
        MarkDirty();
    }

    public async Task DeleteAsync()
    {
        var i = SelectedIndex;
        if (i < 0 || i >= _types.Count)
            return;
        if (!await _confirm.ConfirmAsync("Kusur tipini sil",
                $"'{_types[i].Name}' listeden silinecek. Ortak alana kaydedilene kadar muayene ekranı etkilenmez; " +
                "kaydedildikten sonra bu tip yeni kusurlarda seçilemez (eski kayıtlar değişmez).", "Sil"))
            return;
        _types.RemoveAt(i);
        RefreshList(Math.Min(i, _types.Count - 1));
        MarkDirty();
    }

    public void MoveUp() => Move(-1);

    public void MoveDown() => Move(1);

    private void Move(int delta)
    {
        var i = SelectedIndex;
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _types.Count)
            return;
        (_types[i], _types[j]) = (_types[j], _types[i]);
        RefreshList(j);
        MarkDirty();
    }

    public async Task ResetAsync()
    {
        if (!await _confirm.ConfirmAsync("Varsayılanlara dön",
                $"Listedeki {_types.Count} kusur tipi ve şablonları silinip varsayılan {SharedConfig.Defaults.Count} tip yüklenecek. " +
                "Ek ölçüm alanları silinmez ama varsayılan tiplerde işaretli olmaz. Ortak alana kaydedilene kadar kalıcı olmaz.", "Varsayılanlara dön"))
            return;
        _types = SharedConfig.Defaults.Select(Clone).ToList();
        RefreshList(0);
        MarkDirty();
    }

    // ───── Ek ölçüm alanları ─────

    partial void OnSelectedFieldIndexChanged(int value)
    {
        if (value < 0 || value >= _fields.Count)
            return;
        var f = _fields[value];
        FieldName = f.Name;
        FieldAbbrev = f.Abbrev;
        FieldKindIndex = (int)f.Kind;
    }

    private void RefreshFields(int? select)
    {
        SelectedFieldIndex = -1;
        FieldItems = _fields.Select(f => $"{f.Name}   ({f.Abbrev} · {CustomMeasures.KindLabel(f.Kind)})").ToList();
        if (select is int i && i >= 0 && i < _fields.Count)
        {
            SelectedFieldIndex = i;
        }
        else
        {
            FieldName = "";
            FieldAbbrev = "";
            FieldKindIndex = 0;
        }
    }

    private CustomFieldDef FieldFromEditor(string key) => new()
    {
        Key = key,
        Name = FieldName.Trim(),
        Abbrev = FieldAbbrev.Trim(),
        Kind = FieldKindIndex >= 0 && FieldKindIndex < FieldKindOptions.Count ? (CustomFieldKind)FieldKindIndex : CustomFieldKind.Length,
    };

    /// <summary>Editördeki ad / kısaltma / türle yeni ek alan ekler (henüz hiçbir tipte işaretli değildir).</summary>
    public void AddField() => Run(() =>
    {
        var field = FieldFromEditor(SharedConfig.NewFieldKey());
        var trial = _fields.Append(field).ToList();
        var errors = SharedConfig.ValidateCustomFields(trial);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));
        _fields = trial;
        AfterFieldsChanged(_fields.Count - 1);
        Say($"'{field.Name}' eklendi. Kullanılacağı kusur tiplerinde işaretleyip 'Şablonu uygula' deyin, sonra ortak alana kaydedin.", StatusKind.Info);
    });

    /// <summary>Seçili ek alanın adını / kısaltmasını / türünü günceller. Kayıtlı kusurlardaki değerler eski adıyla kalır.</summary>
    public void ApplyField() => Run(() =>
    {
        var i = SelectedFieldIndex;
        if (i < 0)
            throw new InvalidOperationException("Listeden bir ölçüm alanı seçin.");

        // Tür değişirse kayıtlı değerler yeni türe göre yeniden yorumlanırdı (ör. 0.3 mm metin olurdu): tür sabittir.
        var wanted = FieldFromEditor(_fields[i].Key).Kind;
        if (wanted != _fields[i].Kind)
        {
            FieldKindIndex = (int)_fields[i].Kind;
            throw new InvalidOperationException(
                $"Mevcut ölçüm alanının türü değiştirilemez (kayıtlı değerler bozulur). '{FieldName.Trim()}' için farklı bir tür gerekiyorsa yeni bir alan oluşturun.");
        }
        var trial = _fields.ToList();
        trial[i] = FieldFromEditor(_fields[i].Key);
        var errors = SharedConfig.ValidateCustomFields(trial);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));
        _fields = trial;
        AfterFieldsChanged(i);
    });

    public async Task DeleteFieldAsync()
    {
        var i = SelectedFieldIndex;
        if (i < 0 || i >= _fields.Count)
            return;
        var f = _fields[i];
        var users = _types.Where(t => t.CustomFields.Contains(f.Key)).Select(t => t.Name).ToList();
        if (!await _confirm.ConfirmAsync("Ölçüm alanını sil",
                $"'{f.Name}' silinecek{(users.Count > 0 ? $" ve şu tiplerden kaldırılacak: {string.Join(", ", users)}" : "")}. " +
                "Kayıtlı kusurlardaki değerler silinmez; raporda ve geçmişte görünmeye devam eder.", "Sil"))
            return;
        _fields.RemoveAt(i);
        foreach (var t in _types)
            t.CustomFields.Remove(f.Key);
        AfterFieldsChanged(Math.Min(i, _fields.Count - 1));
    }

    private void AfterFieldsChanged(int? selectField)
    {
        RefreshFields(selectField);
        RefreshList(SelectedIndex);
        MarkDirty();
    }

    public void Save() => Run(() =>
    {
        // Editörde "Şablonu uygula"ya basılmamış değişiklik varsa kaybolmasın: doğrulanarak otomatik uygulanır
        // (geçersizse hata verilir ve hiçbir şey kaydedilmez).
        ApplyEditor(requireSelection: false);

        var typeErrors = SharedConfig.ValidateTypes(_types);
        if (typeErrors.Count > 0)
            throw new ArgumentException(string.Join(" ", typeErrors));

        // Önce alanlar yazılır; şablonlar yalnızca tanımlı alanlara işaret eder.
        var keys = _fields.Select(f => f.Key).ToHashSet();
        foreach (var t in _types)
            t.CustomFields.RemoveAll(k => !keys.Contains(k));
        SharedConfig.SaveCustomFields(_ctx.Root, _fields);
        _ctx.Audit.Append("admin", "config.measure_fields", string.Join(", ", _fields.Select(f => $"{f.Name} ({f.Abbrev}, {f.Kind})")));
        SharedConfig.SaveDefectTypes(_ctx.Root, _types);
        _ctx.Audit.Append("admin", "config.defect_types", string.Join(", ", _types.Select(t => t.Name)));
        DirtyText = "";
        Saved?.Invoke(TypeNames);
        Say("Kusur tipleri ve ölçüm alanları kaydedildi. Muayene uygulamaları bir sonraki açılışta yeni listeyi kullanır.", StatusKind.Success);
    });

    private void MarkDirty() => DirtyText = "Kaydedilmemiş değişiklik var. 'Tümünü ortak alana kaydet' ile kaydedin.";

    private static DefectTypeTemplate Clone(DefectTypeTemplate t) =>
        new() { Name = t.Name, SpecName = t.SpecName, Fields = t.Fields.ToList(), CustomFields = t.CustomFields.ToList() };

    private static CustomFieldDef CloneField(CustomFieldDef f) =>
        new() { Key = f.Key, Name = f.Name, Abbrev = f.Abbrev, Kind = f.Kind };
}
