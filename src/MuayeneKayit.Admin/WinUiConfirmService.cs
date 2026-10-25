using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        // İçerik yalnızca metin: ilk odak onay düğmesine düşer. Vazgeç'e taşınır ki Enter / Space silmesin.
        dialog.Opened += (_, _) => dialog.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (FocusManager.GetFocusedElement(dialog.XamlRoot) is Button { Name: "PrimaryButton" })
                FindButton(dialog, "CloseButton")?.Focus(FocusState.Programmatic);
        });
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static Button? FindButton(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button b && b.Name == name)
                return b;
            if (FindButton(child, name) is Button found)
                return found;
        }
        return null;
    }
}
