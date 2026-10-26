using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Rework sonrası pencerede "Yeni hata" için hata listesi: satır başına tip seçimi ve "×" (satırı sil),
/// altında "+ Hata ekle". Doğrulama kuralı ReinspectForm'dadır (en az bir satır, her satırda tip seçili);
/// burası yalnızca çizer ve seçili tipleri verir. Başlangıçta tek boş satır vardır; son satır silinemez.
/// </summary>
internal sealed class NewDefectListEditor : StackPanel
{
    private readonly IReadOnlyList<string> _typeNames;
    private readonly StackPanel _rows = new() { Spacing = 6 };

    /// <summary>Satır eklendi / silindi ya da tip seçildi.</summary>
    public event Action? Changed;

    public NewDefectListEditor(IReadOnlyList<string> typeNames, string addText)
    {
        _typeNames = typeNames;
        Spacing = 6;

        var add = new Button { Content = "+ " + addText };
        add.Click += (_, _) =>
        {
            AddRow();
            Changed?.Invoke();
        };

        Children.Add(_rows);
        Children.Add(add);
        AddRow();
    }

    /// <summary>Satırlardaki seçili tipler (seçilmemiş satır null).</summary>
    public IReadOnlyList<string?> Types =>
        _rows.Children.OfType<Grid>().Select(g => g.Children.OfType<ComboBox>().First().SelectedItem as string).ToList();

    private void AddRow()
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var combo = new ComboBox { ItemsSource = _typeNames, PlaceholderText = "Tip seçin", HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectionChanged += (_, _) => Changed?.Invoke();

        var remove = new Button
        {
            Content = "×",
            Width = 36,
        };
        ToolTipService.SetToolTip(remove, "Satırı sil");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, "Satırı sil");
        remove.Click += (_, _) =>
        {
            _rows.Children.Remove(row);
            UpdateRemoveButtons();
            Changed?.Invoke();
        };

        Grid.SetColumn(remove, 1);
        row.Children.Add(combo);
        row.Children.Add(remove);
        _rows.Children.Add(row);
        UpdateRemoveButtons();
    }

    /// <summary>Tek satır kaldıysa silinemez (en az bir yeni hata olmalı).</summary>
    private void UpdateRemoveButtons()
    {
        var single = _rows.Children.Count <= 1;
        foreach (var grid in _rows.Children.OfType<Grid>())
            grid.Children.OfType<Button>().First().IsEnabled = !single;
    }
}

/// <summary>Bölge satırlarının ortak çizimi (karar ve rework sonrası pencereleri).</summary>
internal static class RegionControls
{
    /// <summary>Küçük resim (varsa temiz kopya) ve etiket ("3.1").</summary>
    public static FrameworkElement Header(PhotoRegion region, double thumbWidth = 72, double thumbHeight = 54)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new Border
        {
            Width = thumbWidth,
            Height = thumbHeight,
            CornerRadius = new CornerRadius(4),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black),
            Child = new ThumbnailView { Path = region.ThumbnailPath, Width = thumbWidth, Height = thumbHeight },
        });
        row.Children.Add(new TextBlock
        {
            Text = "Bölge " + region.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }
}
