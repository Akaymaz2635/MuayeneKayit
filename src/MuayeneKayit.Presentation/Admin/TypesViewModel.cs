using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Admin "Kusur tipleri" sayfası: liste, seçili tipin şablonu (ad, spec adı, 6 ölçüm alanı),
/// sıralama, ekleme / silme (onaylı), varsayılanlara dönme ve ortak alana kaydetme.
/// </summary>
public sealed partial class TypesViewModel : AdminViewModelBase
{
    private readonly AdminContext _ctx;
    private readonly IConfirmService _confirm;
    private List<DefectTypeTemplate> _types = new();

    public TypesViewModel(AdminContext ctx, IConfirmService confirm)
    {
        _ctx = ctx;
        _confirm = confirm;
    }

    /// <summary>Kaydedildi: yeni tip adları (Kayıtlar sayfasındaki hata tipi listesi yenilenir).</summary>
    public event Action<IReadOnlyList<string>>? Saved;

    /// <summary>Listedeki tip adları (şu anki, kaydedilmemiş hâliyle).</summary>
    public IReadOnlyList<string> TypeNames => _types.Select(t => t.Name).ToList();

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _dirtyText = "";

    public bool IsDirty => DirtyText.Length > 0;

    /// <summary>Ortak alandaki tipleri yükler (giriş sonrası).</summary>
    public void Load()
    {
        _types = SharedConfig.LoadDefectTypes(_ctx.Root).Select(Clone).ToList();
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
    }

    private void RefreshList(int? select)
    {
        // Liste yenilenince seçim önce düşer, sonra yeniden seçilir (alanlar yeniden yüklensin).
        SelectedIndex = -1;
        Items = _types.Select(t => $"{t.Name}   ({FieldsText(t)})").ToList();
        if (select is int i && i >= 0 && i < _types.Count)
            SelectedIndex = i;
    }

    private static string FieldsText(DefectTypeTemplate t) =>
        t.Fields.Count == 0 ? "ölçüm yok" : string.Join(", ", t.Fields.Select(FieldLabel));

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
    public void Apply() => Run(() =>
    {
        var i = SelectedIndex;
        if (i < 0)
            throw new InvalidOperationException("Listeden bir tip seçin.");

        var candidate = new DefectTypeTemplate
        {
            Name = Name.Trim(),
            SpecName = string.IsNullOrWhiteSpace(SpecName) ? null : SpecName.Trim(),
            Fields = CheckedFields(),
        };
        var trial = _types.ToList();
        trial[i] = candidate;
        var errors = SharedConfig.ValidateTypes(trial);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        _types = trial;
        RefreshList(i);
        MarkDirty();
    });

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
                "Ortak alana kaydedilene kadar kalıcı olmaz.", "Varsayılanlara dön"))
            return;
        _types = SharedConfig.Defaults.Select(Clone).ToList();
        RefreshList(0);
        MarkDirty();
    }

    public void Save() => Run(() =>
    {
        SharedConfig.SaveDefectTypes(_ctx.Root, _types);
        _ctx.Audit.Append("admin", "config.defect_types", string.Join(", ", _types.Select(t => t.Name)));
        DirtyText = "";
        Saved?.Invoke(TypeNames);
        Say("Kusur tipleri kaydedildi. Muayene uygulamaları bir sonraki açılışta yeni listeyi kullanır.", StatusKind.Success);
    });

    private void MarkDirty() => DirtyText = "Kaydedilmemiş değişiklik var. 'Tümünü ortak alana kaydet' ile kaydedin.";

    private static DefectTypeTemplate Clone(DefectTypeTemplate t) =>
        new() { Name = t.Name, SpecName = t.SpecName, Fields = t.Fields.ToList() };
}
