using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>
/// Onay penceresini ContentDialog ile çizer. Onay düğmesi vurgulu, vazgeç sade;
/// varsayılan düğme yok, Enter yanlışlıkla onaylamaz.
/// </summary>
public sealed class WinUiConfirmService : IConfirmService
{
    private Func<XamlRoot?>? _xamlRoot;

    /// <summary>Pencere bu kökte açılır; MainWindow oluşunca bağlanır.</summary>
    public void Attach(Func<XamlRoot?> xamlRoot) => _xamlRoot = xamlRoot;

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Width = 440 },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Vazgeç",
            DefaultButton = ContentDialogButton.None,
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            XamlRoot = _xamlRoot?.Invoke() ?? throw new InvalidOperationException("Pencere henüz hazır değil."),
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
