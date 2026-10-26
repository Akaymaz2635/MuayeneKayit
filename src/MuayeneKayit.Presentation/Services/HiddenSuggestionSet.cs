using System.Text.Json;
using System.Text.Json.Nodes;

namespace MuayeneKayit.Presentation;

/// <summary>
/// Bu bilgisayarda "×" ile gizlenen öneriler (parça no, operasyon, seri no). Büyük/küçük harf duyarsız.
/// Saklama biçimi: <c>{ "part": [...], "operation": [...], "serial": [...] }</c>
/// (Inspector'da %LOCALAPPDATA%\MuayeneKayit\hidden-suggestions.json). Dosya işlemi adaptördedir; burası saf kuraldır.
/// </summary>
public sealed class HiddenSuggestionSet
{
    private readonly Dictionary<SuggestLevel, HashSet<string>> _sets = new()
    {
        [SuggestLevel.Part] = new(StringComparer.OrdinalIgnoreCase),
        [SuggestLevel.Operation] = new(StringComparer.OrdinalIgnoreCase),
        [SuggestLevel.Serial] = new(StringComparer.OrdinalIgnoreCase),
    };

    public IReadOnlySet<string> Get(SuggestLevel level) => _sets[level];

    /// <summary>Gizler. Değişiklik olduysa true (boş değer gizlenmez).</summary>
    public bool Hide(SuggestLevel level, string value)
    {
        value = value.Trim();
        return value.Length > 0 && _sets[level].Add(value);
    }

    /// <summary>Açılan muayenenin parça / operasyon / seri değerlerinin gizlemesini kaldırır. Değişiklik olduysa true.</summary>
    public bool Unhide(string part, string operation, string serial)
    {
        var changed = _sets[SuggestLevel.Part].Remove(part.Trim());
        changed |= _sets[SuggestLevel.Operation].Remove(operation.Trim());
        changed |= _sets[SuggestLevel.Serial].Remove(serial.Trim());
        return changed;
    }

    public string ToJson()
    {
        var obj = new JsonObject();
        foreach (var (level, key) in Keys)
        {
            var array = new JsonArray();
            foreach (var value in _sets[level].OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
                array.Add(value);
            obj[key] = array;
        }
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Dosya içeriğinden okur. Bozuk ya da beklenmedik içerik hata vermez, o kısım boş sayılır.</summary>
    public static HiddenSuggestionSet FromJson(string? json)
    {
        var result = new HiddenSuggestionSet();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject obj)
                return result;

            foreach (var (level, key) in Keys)
            {
                if (obj[key] is not JsonArray array)
                    continue;
                foreach (var item in array)
                {
                    if (item is JsonValue v && v.TryGetValue<string>(out var text))
                        result.Hide(level, text);
                }
            }
        }
        catch (JsonException)
        {
            // Bozuk dosya: hiçbir öneri gizli sayılmaz.
        }
        return result;
    }

    private static readonly (SuggestLevel Level, string Key)[] Keys =
    {
        (SuggestLevel.Part, "part"),
        (SuggestLevel.Operation, "operation"),
        (SuggestLevel.Serial, "serial"),
    };
}
