using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Admin;

/// <summary>Kayıtlar sayfasının özet ve sonuç kartlarının çizimi. Veri RecordsViewModel'den gelir.</summary>
public static class AdminCards
{
    public static Border StatCard(SummaryCardData card) => new()
    {
        Background = Brush("#FFFFFF"),
        BorderBrush = Brush("#DDE2EA"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 10, 16, 10),
        MinWidth = 150,
        Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = card.Value, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush(card.Accent) },
                new TextBlock { Text = card.Label, FontSize = 12, Foreground = Brush("#666666") },
            },
        },
    };

    public static TextBlock EmptyMessage(string text) => new() { Text = text, Foreground = Brush("#888888") };

    public static Border RecordCard(AdminRecordRowViewModel r, Action<AdminRecordRowViewModel> openFolder, Action<AdminRecordRowViewModel> openReport)
    {
        var title = new TextBlock
        {
            Text = r.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("#1A3A5C"),
            FontSize = 15,
        };
        var meta = new TextBlock { Text = r.Meta, Foreground = Brush("#666666"), FontSize = 12 };
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var chip in r.DefectChips)
            chips.Children.Add(Chip(chip));

        var open = new Button { Content = "Klasörü aç" };
        open.Click += (_, _) => openFolder(r);
        var report = new Button { Content = "Son rapor" };
        report.Click += (_, _) => openReport(r);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Spacing = 4, Children = { title, meta, chips } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { report, open } };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = Brush(r.HasOpenDefects ? "#F59E0B" : "#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = grid,
        };
    }

    private static Border Chip(ChipData chip) => new()
    {
        Background = Brush(chip.Background),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 2, 8, 2),
        Child = new TextBlock { Text = chip.Text, FontSize = 11, Foreground = Brush(chip.Foreground) },
    };

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(string hex)
    {
        var v = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v));
    }
}
