using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MuayeneKayit.Core;

namespace MuayeneKayit.Inspector;

/// <summary>Dosya yolundan küçük resim yükler (fotoğraf kartları). Yol değişince yeniden yüklenir.</summary>
public sealed class ThumbnailView : UserControl
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path), typeof(string), typeof(ThumbnailView), new PropertyMetadata(null, OnPathChanged));

    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };

    public ThumbnailView() => Content = _image;

    public string? Path
    {
        get => (string?)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ThumbnailView)d;
        view._image.Source = null;
        if (e.NewValue is string path)
            _ = view.LoadAsync(path);
    }

    private async Task LoadAsync(string path)
    {
        // Ek güvenlik: dosya adı yalnızca "{id}_photo_NN.ext" biçiminde olabilir (VM zaten süzer; burada yeniden denetlenir).
        if (!PhotoMarkupValidator.IsPhotoFileName(System.IO.Path.GetFileName(path)))
            return;

        try
        {
            // Akıştan okunur: dosya kilitli kalmaz, ortak alandaki dosya başkası tarafından da açılabilir.
            var bitmap = new BitmapImage { DecodePixelWidth = 360 };
            using var stream = File.OpenRead(path);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            if (Path == path)
                _image.Source = bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or OutOfMemoryException)
        {
            // Küçük resim gösterilemezse kart boş kalır; kayıt etkilenmez.
        }
    }
}
