using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>Dashboard özet ve sonuç kartlarının çizimi. Veri DashboardViewModel'den gelir.</summary>
public static class DashboardCards
{
    public static Border StatCard(SummaryCardData card)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = card.Value, FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush(card.Accent) });
        panel.Children.Add(new TextBlock { Text = card.Label, FontSize = 12, Foreground = Brush("#666666") });

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = Brush("#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            MinWidth = 160,
            Child = panel,
        };
    }

    public static TextBlock EmptyMessage(string text) => new()
    {
        Text = text,
        Foreground = Brush("#666666"),
        Margin = new Thickness(4, 12, 0, 0),
    };

    public static Border ResultCard(DashboardRowViewModel r, Action<DashboardRowViewModel> open)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 6 };

        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(new TextBlock
        {
            Text = r.Title,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("#1A3A5C"),
        });
        title.Children.Add(Chip(r.Badge));
        left.Children.Add(title);

        left.Children.Add(new TextBlock { Text = r.Meta, FontSize = 12, Foreground = Brush("#666666") });

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (r.DefectChips.Count == 0)
            chips.Children.Add(new TextBlock { Text = "Kusur kaydı yok", FontSize = 12, Foreground = Brush("#888888") });
        foreach (var chip in r.DefectChips)
            chips.Children.Add(Chip(chip));
        left.Children.Add(new ScrollViewer
        {
            Content = chips,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
        });

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var button = new Button { Content = "Aç", VerticalAlignment = VerticalAlignment.Center, MinWidth = 80 };
        button.Click += (_, _) => open(r);
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return new Border
        {
            Background = Brush("#FFFFFF"),
            BorderBrush = r.HasOpenDefects ? Brush("#F5C26B") : Brush("#DDE2EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid,
        };
    }

    private static Border Chip(ChipData chip) => new()
    {
        Background = Brush(chip.Background),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 2, 8, 2),
        Child = new TextBlock { Text = chip.Text, FontSize = 12, Foreground = Brush(chip.Foreground), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
    };

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(string hex)
    {
        var h = hex.TrimStart('#');
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
            255,
            Convert.ToByte(h.Substring(0, 2), 16),
            Convert.ToByte(h.Substring(2, 2), 16),
            Convert.ToByte(h.Substring(4, 2), 16)));
    }
}
