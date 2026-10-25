using Microsoft.UI.Xaml;
using MuayeneKayit.Presentation;
using WinRT.Interop;

namespace MuayeneKayit.Admin;

/// <summary>Kaydetme penceresini (FileSavePicker) ana pencereye bağlı açar.</summary>
public sealed class WinUiFileSaver : IFileSaver
{
    private Func<Window?>? _window;

    /// <summary>Seçici bu pencereye bağlanır; MainWindow oluşunca bağlanır.</summary>
    public void Attach(Func<Window?> window) => _window = window;

    public async Task<string?> PickCsvPathAsync(string suggestedName)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = suggestedName };
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window?.Invoke()
            ?? throw new InvalidOperationException("Ana pencere hazır değil.")));
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
