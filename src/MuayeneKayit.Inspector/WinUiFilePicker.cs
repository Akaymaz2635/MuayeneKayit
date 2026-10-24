using Microsoft.UI.Xaml;
using MuayeneKayit.Presentation;
using WinRT.Interop;

namespace MuayeneKayit.Inspector;

/// <summary>Dosya seçme penceresini (FileOpenPicker) ana pencereye bağlı açar.</summary>
public sealed class WinUiFilePicker : IFilePicker
{
    private Func<Window?>? _window;

    /// <summary>Seçici bu pencereye bağlanır; MainWindow oluşunca bağlanır.</summary>
    public void Attach(Func<Window?> window) => _window = window;

    public async Task<string?> PickImageAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window?.Invoke()
            ?? throw new InvalidOperationException("Ana pencere hazır değil.")));
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
