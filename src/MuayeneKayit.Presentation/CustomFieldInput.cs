using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

/// <summary>Muayene ekranında Admin'de tanımlanan bir ek ölçüm kutusu (ör. "Yükseklik (mm)").</summary>
public sealed partial class CustomFieldInput : ObservableObject
{
    public CustomFieldInput(CustomFieldDef def, string header, string text, bool isReadOnly)
    {
        Def = def;
        _header = header;
        _text = text;
        _isReadOnly = isReadOnly;
    }

    public CustomFieldDef Def { get; }

    /// <summary>Metin türünde klavye sayısal açılmaz.</summary>
    public bool IsNumeric => Def.Kind != CustomFieldKind.Text;

    public string AutomationId => $"Custom_{Def.Key}";

    [ObservableProperty] private string _header;
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isReadOnly;

    /// <summary>Kutudaki metni değere çevirir. Geçersiz sayıda ArgumentException fırlatır.</summary>
    public CustomMeasure Read() =>
        Def.Kind == CustomFieldKind.Text
            ? CustomMeasures.Of(Def, null, Text)
            : CustomMeasures.Of(Def, DefectPresentation.ParseOptional(Text, Def.Name), null);
}
