using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

/// <summary>Bellek içi kullanıcı ayarları (dosya yazmaz).</summary>
public sealed class FakeUserSettings : IUserSettings
{
    public LengthUnit Unit { get; set; } = LengthUnit.Mm;
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>Gizlenen öneriler bellekte tutulur (gerçek adaptörle aynı kural: HiddenSuggestionSet).</summary>
    public HiddenSuggestionSet Hidden { get; } = new();

    public IReadOnlySet<string> HiddenSuggestions(SuggestLevel level) => Hidden.Get(level);

    public void HideSuggestion(SuggestLevel level, string value) => Hidden.Hide(level, value);

    public void UnhideSuggestions(string part, string operation, string serial) => Hidden.Unhide(part, operation, serial);
}

/// <summary>Sahte Windows kullanıcısı (sicil no + AD'deki ad).</summary>
public sealed class FakeWindowsUser : IWindowsUser
{
    public string UserName { get; set; } = "12345";
    public string? DisplayName { get; set; } = "Ali Kaymaz";
}

/// <summary>Sıralı yollar veren sahte dosya seçici. Sıra boşsa vazgeçilmiş sayılır.</summary>
public sealed class FakeFilePicker : IFilePicker
{
    public Queue<string?> Picks { get; } = new();
    public int Calls { get; private set; }

    public Task<string?> PickImageAsync()
    {
        Calls++;
        return Task.FromResult(Picks.Count > 0 ? Picks.Dequeue() : null);
    }
}

/// <summary>Pencere açma çağrılarını kaydeder; kaydet / kapanış geri çağrımlarını testten tetiklemek için saklar.</summary>
public sealed class FakeWindowService : IWindowService
{
    public sealed record CameraCall(CameraRequest Request, Func<CapturedPhotoData, string> OnSave, Action<string?> OnClosed);
    public sealed record EditorCall(PhotoEditorRequest Request, Action<PhotoEditResultData> OnSave);

    public List<CameraCall> Cameras { get; } = new();
    public List<EditorCall> Editors { get; } = new();
    public List<string> OpenedFiles { get; } = new();

    public void OpenCamera(CameraRequest request, Func<CapturedPhotoData, string> onSave, Action<string?> onClosed) =>
        Cameras.Add(new CameraCall(request, onSave, onClosed));

    public void OpenPhotoEditor(PhotoEditorRequest request, Action<PhotoEditResultData> onSave) =>
        Editors.Add(new EditorCall(request, onSave));

    public void OpenFile(string path) => OpenedFiles.Add(path);
}

/// <summary>Sahte PDF dönüştürücü: Error null ise PDF dosyasını yazar (başarılı), doluysa o hatayı döndürür.</summary>
public sealed class FakePdfConverter : IPdfConverter
{
    public string? Error { get; set; }
    public List<(string Html, string Pdf)> Calls { get; } = new();

    public Task<string?> TryConvertAsync(string htmlPath, string pdfPath)
    {
        Calls.Add((htmlPath, pdfPath));
        if (Error is null)
            File.WriteAllBytes(pdfPath, new byte[] { 0x25, 0x50, 0x44, 0x46 });
        return Task.FromResult(Error);
    }
}
