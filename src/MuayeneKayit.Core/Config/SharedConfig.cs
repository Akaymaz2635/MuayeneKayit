using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuayeneKayit.Core;

public enum MeasureField
{
    Depth,
    Width,
    Length,
    Radius,
    Angle,
    Color,
}

/// <summary>
/// Admin panelinde tanımlanan kusur tipi şablonu. Muayene ekranında yalnızca şablondaki
/// ölçüm alanları gösterilir; spec adı yeni kusura varsayılan olarak yazılır.
/// </summary>
public sealed class DefectTypeTemplate
{
    public string Name { get; set; } = "";
    public List<MeasureField> Fields { get; set; } = new();

    /// <summary>İşaretli ek ölçüm alanlarının anahtarları (<see cref="CustomFieldDef.Key"/>). Eski dosyalarda yok.</summary>
    public List<string> CustomFields { get; set; } = new();

    public string? SpecName { get; set; }
}

/// <summary>Admin PIN'i. Yalnızca tuzlu PBKDF2 özeti saklanır.</summary>
public sealed class AdminPinRecord
{
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Iterations { get; set; }
}

/// <summary>
/// Ortak alandaki "_config" klasörü. Muayene uygulaması yalnızca okur; admin uygulaması yazar.
/// Klasöre yazma yetkisi işletim sistemi izinleriyle (NTFS) yalnızca admin grubuna verilmelidir.
/// </summary>
public static class SharedConfig
{
    public const string FolderName = "_config";
    private const string TypesFile = "defect_types.json";
    private const string PinFile = "admin.json";
    private const int PinIterations = 200_000;

    public static readonly IReadOnlyList<DefectTypeTemplate> Defaults = new[]
    {
        T("Dent", MeasureField.Depth, MeasureField.Width, MeasureField.Length),
        T("Nick", MeasureField.Depth, MeasureField.Width, MeasureField.Length),
        T("Scratch", MeasureField.Depth, MeasureField.Width, MeasureField.Length),
        T("Undercut", MeasureField.Depth, MeasureField.Length),
        T("Deformation", MeasureField.Width, MeasureField.Length, MeasureField.Angle),
        T("Burrs", MeasureField.Width, MeasureField.Length),
        T("Braze Void", MeasureField.Width, MeasureField.Length, MeasureField.Radius),
        T("Discoloration", MeasureField.Width, MeasureField.Length, MeasureField.Color),
        T("Excess of Material", MeasureField.Width, MeasureField.Length, MeasureField.Radius),
    };

    private static readonly Regex NamePattern = new(@"^[A-Za-z0-9ÇĞİÖŞÜçğıöşü _\-./()]{1,40}$");

    public static string Folder(string root) => Path.Combine(Path.GetFullPath(root), FolderName);

    // ───── Kusur tipleri ─────

    /// <summary>Şablonları okur. Dosya yoksa ya da bozuksa varsayılanlar döner; muayene durmaz.</summary>
    public static IReadOnlyList<DefectTypeTemplate> LoadDefectTypes(string root)
    {
        try
        {
            var path = Path.Combine(Folder(root), TypesFile);
            if (!File.Exists(path))
                return Defaults;

            var list = SessionStore.ReadJson<List<DefectTypeTemplate>>(path);
            return ValidateTypes(list).Count == 0 && list.Count > 0 ? list : Defaults;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return Defaults;
        }
    }

    public static IReadOnlyList<string> ValidateTypes(IReadOnlyList<DefectTypeTemplate> types)
    {
        var errors = new List<string>();
        if (types.Count == 0)
            errors.Add("En az bir kusur tipi olmalı.");
        if (types.Count > 100)
            errors.Add("En fazla 100 kusur tipi tanımlanabilir.");

        foreach (var t in types)
        {
            if (!NamePattern.IsMatch(t.Name ?? ""))
                errors.Add($"Geçersiz kusur tipi adı: \"{t.Name}\" (1-40 karakter, harf, rakam, boşluk, - _ . / ( )).");
            if (t.SpecName is { Length: > 60 })
                errors.Add($"{t.Name}: spec adı en fazla 60 karakter.");
        }

        var duplicate = types.GroupBy(t => t.Name?.Trim() ?? "", StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            errors.Add($"Aynı ad iki kez var: {duplicate.Key}");

        return errors;
    }

    public static void SaveDefectTypes(string root, IReadOnlyList<DefectTypeTemplate> types)
    {
        var errors = ValidateTypes(types);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        Directory.CreateDirectory(Folder(root));
        AtomicFile.WriteAllText(Path.Combine(Folder(root), TypesFile), JsonSerializer.Serialize(types, SessionStore.JsonOptions));
    }

    // ───── Ek ölçüm alanları ─────

    private const string CustomFieldsFile = "measure_fields.json";
    public const int MaxCustomFields = 30;

    /// <summary>Yerleşik alanların kısaltmaları; ek alan bunları kullanamaz (cümlede karışmasın).</summary>
    private static readonly string[] ReservedAbbrevs = { "D", "W", "L", "Ø", "Ç", "A" };

    private static readonly Regex AbbrevPattern = new(@"^[A-Za-z0-9ÇĞİÖŞÜçğıöşüØ°]{1,6}$");
    private static readonly Regex KeyPattern = new(@"^[a-z0-9]{1,16}$");

    /// <summary>Ek ölçüm alanlarını okur. Dosya yoksa ya da bozuksa boş liste döner; muayene durmaz.</summary>
    public static IReadOnlyList<CustomFieldDef> LoadCustomFields(string root)
    {
        try
        {
            var path = Path.Combine(Folder(root), CustomFieldsFile);
            if (!File.Exists(path))
                return Array.Empty<CustomFieldDef>();

            var list = SessionStore.ReadJson<List<CustomFieldDef>>(path);
            return ValidateCustomFields(list).Count == 0 ? list : Array.Empty<CustomFieldDef>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return Array.Empty<CustomFieldDef>();
        }
    }

    public static IReadOnlyList<string> ValidateCustomFields(IReadOnlyList<CustomFieldDef> fields)
    {
        var errors = new List<string>();
        if (fields.Count > MaxCustomFields)
            errors.Add($"En fazla {MaxCustomFields} ek ölçüm alanı tanımlanabilir.");

        foreach (var f in fields)
        {
            if (!KeyPattern.IsMatch(f.Key ?? ""))
                errors.Add($"Geçersiz alan anahtarı: \"{f.Key}\".");
            if (!NamePattern.IsMatch(f.Name ?? "") || f.Name!.Trim().Length > 30)
                errors.Add($"Geçersiz alan adı: \"{f.Name}\" (1-30 karakter, harf, rakam, boşluk, - _ . / ( )).");
            if (!AbbrevPattern.IsMatch(f.Abbrev ?? ""))
                errors.Add($"{f.Name}: kısaltma 1-6 harf ya da rakam olmalı (ör. H).");
            else if (ReservedAbbrevs.Contains(f.Abbrev, StringComparer.OrdinalIgnoreCase))
                errors.Add($"{f.Name}: \"{f.Abbrev}\" kısaltması yerleşik bir alana ait (D, W, L, Ø, A); başka bir kısaltma seçin.");
            if (!Enum.IsDefined(f.Kind))
                errors.Add($"{f.Name}: geçersiz alan türü.");
        }

        string? Dup(Func<CustomFieldDef, string?> by) =>
            fields.GroupBy(f => by(f)?.Trim() ?? "", StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Key.Length > 0 && g.Count() > 1)?.Key;
        if (Dup(f => f.Key) is string k)
            errors.Add($"Aynı alan anahtarı iki kez var: {k}");
        if (Dup(f => f.Name) is string n)
            errors.Add($"Aynı alan adı iki kez var: {n}");
        if (Dup(f => f.Abbrev) is string a)
            errors.Add($"Aynı kısaltma iki kez var: {a}");

        return errors;
    }

    public static void SaveCustomFields(string root, IReadOnlyList<CustomFieldDef> fields)
    {
        var errors = ValidateCustomFields(fields);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        Directory.CreateDirectory(Folder(root));
        AtomicFile.WriteAllText(Path.Combine(Folder(root), CustomFieldsFile), JsonSerializer.Serialize(fields, SessionStore.JsonOptions));
    }

    /// <summary>Yeni ek alan için benzersiz anahtar.</summary>
    public static string NewFieldKey() => "f" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Şablonda işaretli ek alanların tanımları (tanımı silinmiş anahtarlar atlanır), şablondaki sırayla.</summary>
    public static IReadOnlyList<CustomFieldDef> FieldsOf(DefectTypeTemplate? template, IReadOnlyList<CustomFieldDef> defs) =>
        template is null
            ? defs
            : template.CustomFields.Select(k => defs.FirstOrDefault(d => d.Key == k)).OfType<CustomFieldDef>().ToList();

    // ───── Karar açıklamaları ─────

    private const string DecisionTextsFile = "decision_texts.json";
    public const int MaxDecisionTextLength = 600;

    /// <summary>Muayene ekranındaki karar penceresinde gösterilen açıklamalar (admin panelinden düzenlenir).</summary>
    public static readonly IReadOnlyDictionary<Decision, string> DefaultDecisionTexts = new Dictionary<Decision, string>
    {
        [Decision.Accept] = "Kusur SPA'ya göre kabul edilir ve kapanır.",
        [Decision.Rework] = "Parça rework'e gider. Kusur 'Rework sonrası muayene bekliyor' olur ve ölçümler kilitlenir. " +
                            "Rework bitince bu kusurda 'Rework sonrası muayene sonucunu gir' düğmesiyle sonucu girersiniz: " +
                            "giderildi, aynı kusur devam ediyor ya da yeni hata oluştu.",
        [Decision.SubmitMrb] = "Kusur MRB'ye gönderilir. Parça akışta ilerleyebilir ama muayene, MRB kararı (kabul / red) girilene kadar kapanmaz.",
        [Decision.MrbAccepted] = "MRB 'accept as is' kararı verdi. Kusur kapanır.",
        [Decision.MrbRejected] = "MRB reddetti. Kusur otomatik olarak hurdaya (Scrap) yazılır ve kesin kapanır.",
        [Decision.Repair] = "Rework ile düzeltilemeyen, prosedür dışı tamir. Kusur 'Repair' olarak açık kalır; sonra kabul, hurda, reinspect ya da MRB verilir.",
        [Decision.Reinspect] = "Belirtilen yerde (CTP ya da operasyon) yeniden görsel kontrol yapılır. Sonuç girilene kadar kusur açık kalır.",
        [Decision.ReturnToVendor] = "Parça tedarikçiye gider. Kusur 'Tedarikçide' olarak açık kalır; parça dönünce reinspect verilir.",
        [Decision.Scrap] = "Parça hurdaya ayrılır. Kusur kesin olarak kapanır ve sonradan yeniden açılamaz.",
        [Decision.Reopen] = "Kapalı kusur yeniden açılır ve yeni karar verilebilir. Eski kararlar geçmişte kalır.",
    };

    /// <summary>Açıklamaları okur; dosyada olmayan ya da geçersiz olanlar için varsayılan metin döner.</summary>
    public static IReadOnlyDictionary<Decision, string> LoadDecisionTexts(string root)
    {
        var result = new Dictionary<Decision, string>(DefaultDecisionTexts);
        try
        {
            var path = Path.Combine(Folder(root), DecisionTextsFile);
            if (!File.Exists(path))
                return result;

            var saved = SessionStore.ReadJson<Dictionary<Decision, string>>(path);
            foreach (var (decision, text) in saved)
            {
                if (DefaultDecisionTexts.ContainsKey(decision) && ValidateDecisionText(text) is null)
                    result[decision] = text.Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            // Bozuk dosya muayeneyi durdurmaz; varsayılan açıklamalar kullanılır.
        }
        return result;
    }

    /// <summary>Hata yoksa null döner.</summary>
    public static string? ValidateDecisionText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Açıklama boş olamaz.";
        if (text.Trim().Length > MaxDecisionTextLength)
            return $"Açıklama en fazla {MaxDecisionTextLength} karakter olabilir.";
        if (text.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            return "Açıklamada kontrol karakteri olamaz.";
        return null;
    }

    public static void SaveDecisionTexts(string root, IReadOnlyDictionary<Decision, string> texts)
    {
        var errors = texts
            .Where(t => !DefaultDecisionTexts.ContainsKey(t.Key) || ValidateDecisionText(t.Value) is not null)
            .Select(t => $"{t.Key}: {ValidateDecisionText(t.Value) ?? "bu karar için açıklama tanımlanamaz"}")
            .ToList();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        Directory.CreateDirectory(Folder(root));
        var clean = texts.ToDictionary(t => t.Key, t => t.Value.Trim());
        AtomicFile.WriteAllText(Path.Combine(Folder(root), DecisionTextsFile), JsonSerializer.Serialize(clean, SessionStore.JsonOptions));
    }

    // ───── Muayeneci kimliği ─────

    private const string IdentityFile = "inspector_identity.json";

    private sealed record IdentitySettings(bool UseWindowsUser);

    /// <summary>
    /// Açıksa muayeneci adı elle yazılmaz; Windows oturumundaki kullanıcıdan (sicil no + AD'deki ad) alınır.
    /// Admin panelinden açılıp kapatılır. Dosya yoksa ya da bozuksa kapalı sayılır.
    /// </summary>
    public static bool LoadUseWindowsUser(string root)
    {
        try
        {
            var path = Path.Combine(Folder(root), IdentityFile);
            return File.Exists(path) && SessionStore.ReadJson<IdentitySettings>(path).UseWindowsUser;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SaveUseWindowsUser(string root, bool value)
    {
        Directory.CreateDirectory(Folder(root));
        AtomicFile.WriteAllText(Path.Combine(Folder(root), IdentityFile),
            JsonSerializer.Serialize(new IdentitySettings(value), SessionStore.JsonOptions));
    }

    // ───── Admin PIN ─────

    public static bool HasPin(string root) => File.Exists(Path.Combine(Folder(root), PinFile));

    /// <summary>PIN 6-12 rakamdır. Sabit zamanlı karşılaştırma yapılır.</summary>
    public static void SetPin(string root, string pin)
    {
        if (!Regex.IsMatch(pin ?? "", @"^\d{6,12}$"))
            throw new ArgumentException("PIN 6-12 haneli rakam olmalı.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var record = new AdminPinRecord
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(Derive(pin!, salt, PinIterations)),
            Iterations = PinIterations,
        };

        Directory.CreateDirectory(Folder(root));
        AtomicFile.WriteAllText(Path.Combine(Folder(root), PinFile), JsonSerializer.Serialize(record, SessionStore.JsonOptions));
    }

    public static bool VerifyPin(string root, string pin)
    {
        var path = Path.Combine(Folder(root), PinFile);
        if (!File.Exists(path) || string.IsNullOrEmpty(pin) || pin.Length > 12)
            return false;

        var record = SessionStore.ReadJson<AdminPinRecord>(path);
        if (record.Iterations < 10_000)
            return false;

        var expected = Convert.FromBase64String(record.Hash);
        var actual = Derive(pin, Convert.FromBase64String(record.Salt), record.Iterations);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256, 32);

    // ───── Kilit bakımı ─────

    /// <summary>Kök altında eski kalmış kilit dosyaları (program çöktüğünde ya da ağ koptuğunda kalır).</summary>
    public static IReadOnlyList<string> StaleLocks(string root) =>
        Directory.EnumerateFiles(Path.GetFullPath(root), "*.lock", SearchOption.AllDirectories)
            // Tam bu sırada bırakılan kilit 1601 tarihiyle döner; eski sayılmaz.
            .Where(p => File.GetLastWriteTimeUtc(p) is var w && w.Year > 1601 && DateTime.UtcNow - w > FileLock.StaleAfter)
            .ToList();

    /// <summary>
    /// Eski kilidi siler. Yalnızca kök altındaki, ".lock" uzantılı ve eski sayılan dosyalar silinebilir;
    /// kullanımda olan (yeni) kilit silinmez.
    /// </summary>
    public static void RemoveStaleLock(string root, string lockPath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(lockPath);
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Yalnızca ortak alandaki kilit dosyaları silinebilir.");
        if (!File.Exists(full))
            return;
        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(full) <= FileLock.StaleAfter)
            throw new InvalidOperationException("Kilit henüz eski değil; bir kullanıcı şu an yazıyor olabilir.");
        File.Delete(full);
    }

    private static DefectTypeTemplate T(string name, params MeasureField[] fields) =>
        new() { Name = name, Fields = fields.ToList() };
}
