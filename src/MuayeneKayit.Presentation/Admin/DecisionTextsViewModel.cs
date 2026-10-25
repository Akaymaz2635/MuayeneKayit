using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Düzenlenebilir bir karar açıklaması (karar adı + metin).</summary>
public sealed partial class DecisionTextItem : ObservableObject
{
    private readonly Action _changed;

    public DecisionTextItem(Decision decision, string label, string text, Action changed)
    {
        Decision = decision;
        Label = label;
        _text = text;
        _changed = changed;
    }

    public Decision Decision { get; }
    public string Label { get; }
    public int MaxLength => SharedConfig.MaxDecisionTextLength;

    [ObservableProperty] private string _text;

    partial void OnTextChanged(string value) => _changed();

    /// <summary>Bu kararın metnini varsayılana döndürür (kaydedilene kadar kalıcı olmaz).</summary>
    public void ResetOne() => Text = SharedConfig.DefaultDecisionTexts[Decision];
}

/// <summary>Admin "Karar açıklamaları" sayfası: karar başına metin düzenleme, kaydetme, varsayılana dönme.</summary>
public sealed partial class DecisionTextsViewModel : AdminViewModelBase
{
    /// <summary>Muayene ekranındaki karar listesiyle aynı sıra ve adlar.</summary>
    public static readonly (Decision Decision, string Label)[] DecisionLabels =
    {
        (Decision.Accept, "Kabul (SPA)"),
        (Decision.Rework, "Rework → Reinspect"),
        (Decision.SubmitMrb, "Submit MRB"),
        (Decision.MrbAccepted, "MRB kabul (accept as is)"),
        (Decision.MrbRejected, "MRB red (otomatik hurda)"),
        (Decision.Repair, "Repair"),
        (Decision.Reinspect, "Reinspect (CTP)"),
        (Decision.ReturnToVendor, "Return to vendor"),
        (Decision.Scrap, "Scrap"),
        (Decision.Reopen, "Yeniden aç"),
    };

    private readonly AdminContext _ctx;
    private readonly IConfirmService _confirm;

    public DecisionTextsViewModel(AdminContext ctx, IConfirmService confirm)
    {
        _ctx = ctx;
        _confirm = confirm;
    }

    [ObservableProperty] private IReadOnlyList<DecisionTextItem> _items = Array.Empty<DecisionTextItem>();
    [ObservableProperty] private string _dirtyText = "";

    /// <summary>Ortak alandaki metinleri yükler (sayfa açılınca).</summary>
    public void Load() => Render(SharedConfig.LoadDecisionTexts(_ctx.Root));

    private void Render(IReadOnlyDictionary<Decision, string> texts)
    {
        DirtyText = "";
        Items = DecisionLabels
            .Select(x => new DecisionTextItem(x.Decision, x.Label, texts.GetValueOrDefault(x.Decision, ""),
                () => DirtyText = "Kaydedilmemiş değişiklik var."))
            .ToList();
    }

    public void Save() => Run(() =>
    {
        var texts = Items.ToDictionary(x => x.Decision, x => x.Text);
        SharedConfig.SaveDecisionTexts(_ctx.Root, texts);
        _ctx.Audit.Append("admin", "config.decision_texts", $"{texts.Count} açıklama");
        DirtyText = "";
        Say("Karar açıklamaları kaydedildi. Muayene ekranında bir sonraki karar seçiminde görünür.", StatusKind.Success);
    });

    public async Task ResetAllAsync()
    {
        if (!await _confirm.ConfirmAsync("Açıklamaları varsayılana döndür",
                "Düzenlediğiniz tüm karar açıklamaları varsayılan metinlerle değiştirilecek. " +
                "Ortak alana kaydedilene kadar kalıcı olmaz.", "Varsayılana döndür"))
            return;
        Render(SharedConfig.DefaultDecisionTexts);
        DirtyText = "Varsayılanlar yüklendi; kalıcı olması için 'Ortak alana kaydet'e basın.";
    }
}
