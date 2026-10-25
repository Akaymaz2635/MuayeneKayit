using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MuayeneKayit.Core;
using MuayeneKayit.Presentation;
using static MuayeneKayit.Presentation.DefectPresentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Diyalogları ContentDialog ile çizer. Akış ve kurallar ViewModel'dedir; burası yalnızca görünümdür.
/// Tüm onay pencerelerinde onay düğmesi vurgulu (koyu), vazgeç sade; varsayılan düğme yok: Enter yanlışlıkla kaydetmez.
/// Aynı anda yalnızca bir ContentDialog açık olabilir; her pencere beklenerek gösterilir.
/// </summary>
public sealed class WinUiDialogService : IDialogService
{
    private Func<XamlRoot?>? _xamlRoot;

    /// <summary>Pencereler bu kökte açılır; MainWindow yüklenince bağlanır.</summary>
    public void Attach(Func<XamlRoot?> xamlRoot) => _xamlRoot = xamlRoot;

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private static Style ConfirmButtonStyle => (Style)Application.Current.Resources["AccentButtonStyle"];

    private static Visibility Vis(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private ContentDialog NewDialog(string title, object content, string primary, string close, bool primaryEnabled = true)
    {
        var xamlRoot = _xamlRoot?.Invoke() ?? throw new InvalidOperationException("Pencere henüz hazır değil.");
        return new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.None,
            PrimaryButtonStyle = ConfirmButtonStyle,
            IsPrimaryButtonEnabled = primaryEnabled,
            XamlRoot = xamlRoot,
            // Pencere kökündeki tema (Açık / Koyu) diyaloğa da uygulanır.
            RequestedTheme = (xamlRoot.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
        };
    }

    // ───────────────────────── Karar ─────────────────────────

    public async Task<DecisionInput?> AskDecisionAsync(DecisionRequest request)
    {
        // Sabit genişlik: pencere içeriğe göre açılır, metin kırpılmadan alt satıra geçer.
        var panel = new StackPanel { Spacing = 12, Width = 480 };

        panel.Children.Add(new TextBlock
        {
            Text = $"#{request.DefectId} {request.DefectType}  ·  şu an: {request.StateText}",
            Style = Res("SecondaryTextStyle"),
        });
        panel.Children.Add(new Border
        {
            Style = Res("SubtleCardStyle"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new TextBlock { Text = request.EffectText, TextWrapping = TextWrapping.Wrap },
        });

        TextBox? target = null;
        if (request.TargetHeader is not null)
        {
            target = new TextBox { Header = request.TargetHeader, Text = request.TargetDefault ?? "" };
            target.SelectAll();
            panel.Children.Add(target);
        }

        TextBox? reference = null;
        if (request.NeedsReference)
        {
            reference = new TextBox
            {
                Header = "Mühendis kağıdındaki referans / MRB no (isteğe bağlı)",
                PlaceholderText = "örn. SPA-2024-118",
            };
            panel.Children.Add(reference);
        }

        RadioButtons? scope = null;
        if (request.NeedsReworkScope)
        {
            scope = new RadioButtons { Header = "Rework kapsamı (zorunlu)" };
            scope.Items.Add(new RadioButton { Content = "Kusur + high metal giderilecek", Tag = ReworkScope.DefectAndHighMetal });
            scope.Items.Add(new RadioButton { Content = "Yalnızca high metal giderilecek (ana kusur kalabilir)", Tag = ReworkScope.HighMetalOnly });
            panel.Children.Add(scope);
        }

        var note = new TextBox
        {
            Header = request.NoteRequired ? "Neden yeniden açılıyor? (zorunlu)" : "Not (isteğe bağlı)",
            AcceptsReturn = true,
            Height = 70,
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(note);

        panel.Children.Add(new TextBlock
        {
            Text = "Yanlış kaydederseniz 'Son kararı geri al' ile geri alabilirsiniz. Geri alma da geçmişe yazılır.",
            Style = Res("SecondaryTextStyle"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        ReworkScope? SelectedScope() => (scope?.SelectedItem as RadioButton)?.Tag as ReworkScope?;

        var dialog = NewDialog(request.Label, panel, "Kararı kaydet", "Vazgeç",
            primaryEnabled: DecisionForm.CanSave(request.NoteRequired, note.Text, request.NeedsReworkScope, SelectedScope()));
        void UpdateSave() => dialog.IsPrimaryButtonEnabled =
            DecisionForm.CanSave(request.NoteRequired, note.Text, request.NeedsReworkScope, SelectedScope());
        note.TextChanged += (_, _) => UpdateSave();
        if (scope is not null)
            scope.SelectionChanged += (_, _) => UpdateSave();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return new DecisionInput(target?.Text, reference?.Text, note.Text, SelectedScope());
    }

    public async Task<bool> AskReinspectNowAsync(Decision decision, int defectId)
    {
        var ask = NewDialog(
            decision == Decision.Rework ? "Rework şimdi yapıldı mı?" : "Kontrol şimdi yapılıyor mu?",
            new TextBlock
            {
                Width = 480,
                TextWrapping = TextWrapping.Wrap,
                Text = decision == Decision.Rework
                    ? $"Parça şimdi tekrar muayene edilecekse sonucu hemen girin. Parça rework'e gittiyse 'Sonra' deyin; kusur 'rework'te' olarak bekler, döndüğünde #{defectId} seçilip sonuç girilir."
                    : $"Kontrol şimdi yapılıyorsa sonucu hemen girin. Değilse 'Sonra' deyin; #{defectId} kontrol bekliyor olarak kalır.",
            },
            "Sonucu şimdi gir", "Sonra");
        return await ask.ShowAsync() == ContentDialogResult.Primary;
    }

    // ───────────────────────── Rework sonrası muayene ─────────────────────────

    public async Task<ReinspectInput?> AskReinspectAsync(ReinspectRequest request)
    {
        var before = request.Before;
        var hadHighMetal = before.HighMetal;
        var panel = new StackPanel { Spacing = 12, Width = 480 };
        if (request.HighMetalOnlyScope)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Rework kapsamı: yalnızca high metal için yapıldı. Ana kusur kalabilir.",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = $"{(request.IsRework ? "Rework öncesi" : "Önceki")} ölçümler: {SnapshotText(before, request.Unit)}" +
                   (string.IsNullOrWhiteSpace(request.TargetOperation) ? "" : $"   ·   muayene yeri: {request.TargetOperation}"),
            Style = Res("SecondaryTextStyle"),
            TextWrapping = TextWrapping.Wrap,
        });

        // 1. adım: sonuç
        var outcome = new RadioButtons { Header = "1. Muayene sonucu ne?" };
        outcome.Items.Add(new RadioButton { Content = "Kusur giderildi (uygun) → kusur kapanır", Tag = ReinspectOutcome.Conforms });
        outcome.Items.Add(new RadioButton
        {
            Content = hadHighMetal
                ? "Ana kusur hâlâ var (high metal giderilmiş olabilir) → yeni ölçüm / durum gir"
                : "Aynı kusur hâlâ var → yeni ölçümleri gir, sonra yeni karar ver",
            Tag = ReinspectOutcome.Same,
        });
        outcome.Items.Add(new RadioButton { Content = "Eski kusur gitti ama yeni bir hata oluştu → yeni kusur aç", Tag = ReinspectOutcome.New });
        panel.Children.Add(outcome);

        // 2. adım (aynı kusur): yeni ölçümler, boş gelir
        var headers = new (MeasureField Field, string Header, double? Old)[]
        {
            (MeasureField.Depth, Units.Header("Derinlik", request.Unit), before.Depth),
            (MeasureField.Width, Units.Header("Genişlik", request.Unit), before.Width),
            (MeasureField.Length, Units.Header("Uzunluk", request.Unit), before.Length),
            (MeasureField.Radius, Units.Header("Yarıçap", request.Unit), before.Radius),
            (MeasureField.Angle, "Açı (°)", before.AngleDeg),
        };
        var boxes = new Dictionary<MeasureField, TextBox>();
        var measureGrid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (field, header, old) in headers)
        {
            if (!request.Fields.Contains(field))
                continue;
            var box = new TextBox { Header = header, Width = 120, PlaceholderText = old is double o ? $"önce: {(field == MeasureField.Angle ? Fmt(o) : Fmt(o, request.Unit))}" : "" };
            boxes[field] = box;
            measureGrid.Children.Add(box);
        }
        TextBox? colorBox = null;
        if (request.ShowColor)
        {
            colorBox = new TextBox { Header = "Renk", Width = 140, PlaceholderText = before.Color is null ? "" : $"önce: {before.Color}" };
            measureGrid.Children.Add(colorBox);
        }
        var delta = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Style = Res("WarnTextStyle") };
        var samePanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed,
            Children =
            {
                new TextBlock { Text = (hadHighMetal ? "3." : "2.") + " Rework sonrası ölçümleri girin (boş bırakılan alan önceki değerinde kalır)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                new ScrollViewer { Content = measureGrid, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollMode = ScrollMode.Disabled },
                delta,
            },
        };
        // High metal'li kusurda durum açıkça sorulur (varsayılan yok): yalnızca high metal gidip değerler aynı kalabilir.
        RadioButtons? highMetal = null;
        if (hadHighMetal)
        {
            highMetal = new RadioButtons { Header = "2. High metal durumu (zorunlu)" };
            highMetal.Items.Add(new RadioButton { Content = "Giderildi", Tag = false });
            highMetal.Items.Add(new RadioButton { Content = "Hâlâ var", Tag = true });
            samePanel.Children.Insert(0, highMetal);
        }
        panel.Children.Add(samePanel);

        // 2. adım (yeni hata): child tipi, varsayılan seçili değil
        var childType = new ComboBox { Header = "2. Yeni hatanın tipi", ItemsSource = request.TypeNames, PlaceholderText = "Tip seçin", Width = 300 };
        var newPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed,
            Children =
            {
                childType,
                new TextBlock
                {
                    Text = $"#{request.DefectId} kapanmış sayılmaz, yeni kusura devredilir ('Defect {request.DefectId} continues as Defect N'). Yeni kusurun ölçümlerini ve fotoğrafını kaydettikten sonra girersiniz.",
                    TextWrapping = TextWrapping.Wrap,
                    Style = Res("SecondaryTextStyle"),
                    FontSize = 12,
                },
            },
        };
        panel.Children.Add(newPanel);

        var note = new TextBox { Header = "Not (isteğe bağlı)", AcceptsReturn = true, Height = 60, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(note);

        var dialog = NewDialog(
            $"#{request.DefectId} {request.DefectType}: {(request.IsRework ? "rework sonrası muayene" : "yeniden kontrol sonucu")}",
            panel, "Kaydet", "Vazgeç", primaryEnabled: false);

        ReinspectOutcome? Selected() => (outcome.SelectedItem as RadioButton)?.Tag as ReinspectOutcome?;
        bool? HighMetalAnswer() => (highMetal?.SelectedItem as RadioButton)?.Tag as bool?;
        string? ChildType() => childType.SelectedItem as string;

        MeasurementSnapshot ReadValues()
        {
            static double? Try(TextBox? b) =>
                b is null || string.IsNullOrWhiteSpace(b.Text) ? null
                : double.TryParse(b.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ||
                  double.TryParse(b.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : null;
            return new MeasurementSnapshot(
                Try(boxes.GetValueOrDefault(MeasureField.Depth)), Try(boxes.GetValueOrDefault(MeasureField.Width)),
                Try(boxes.GetValueOrDefault(MeasureField.Length)), Try(boxes.GetValueOrDefault(MeasureField.Radius)),
                Try(boxes.GetValueOrDefault(MeasureField.Angle)),
                string.IsNullOrWhiteSpace(colorBox?.Text) ? null : colorBox!.Text.Trim());
        }

        void Refresh()
        {
            var tag = Selected();
            samePanel.Visibility = Vis(tag == ReinspectOutcome.Same);
            newPanel.Visibility = Vis(tag == ReinspectOutcome.New);

            var entered = ReadValues();
            var anyValue = ReinspectForm.AnyValue(entered);
            if (tag == ReinspectOutcome.Same)
            {
                var merged = ReinspectForm.Merge(before, entered, HighMetalAnswer());
                delta.Text = ReinspectForm.DeltaText(before, merged, anyValue, highMetal is not null && HighMetalAnswer() is not null, request.Unit);
            }

            dialog.IsPrimaryButtonEnabled = ReinspectForm.CanSave(tag, hadHighMetal, HighMetalAnswer(), anyValue, ChildType());
        }

        outcome.SelectionChanged += (_, _) => Refresh();
        childType.SelectionChanged += (_, _) => Refresh();
        if (highMetal is not null)
            highMetal.SelectionChanged += (_, _) => Refresh();
        foreach (var box in boxes.Values.Append(colorBox).OfType<TextBox>())
            box.TextChanged += (_, _) => Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        var result = Selected() ?? throw new InvalidOperationException("Muayene sonucunu seçin.");
        return new ReinspectInput(result, ReadValues(), HighMetalAnswer(), ChildType(), note.Text);
    }

    // ───────────────────────── Geri al, void, fotoğraf ─────────────────────────

    public async Task<string?> AskUndoReasonAsync(UndoRequest request)
    {
        var panel = new StackPanel { Spacing = 12, Width = 480 };
        panel.Children.Add(new TextBlock
        {
            Text = $"#{request.DefectId} {request.DefectType} için son karar geri alınacak:\n" +
                   $"{request.DecisionLabel}   ({request.DecidedAt:dd.MM.yyyy HH:mm}, {request.DecidedBy})",
            TextWrapping = TextWrapping.Wrap,
        });
        if (request.ChildToVoid is int childId)
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Message = $"Bu kararla açılan yeni kusur #{childId} void edilecek (silinmez, rapora girmez). " +
                          $"#{request.DefectId} tekrar 'rework sonrası muayene bekliyor' olur.",
            });
        }
        var reason = new TextBox
        {
            Header = "Neden geri alınıyor? (zorunlu)",
            PlaceholderText = "örn. yanlış karar seçildi",
            AcceptsReturn = true,
            Height = 70,
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(reason);

        var dialog = NewDialog("Son kararı geri al", panel, "Geri al", "Vazgeç", primaryEnabled: false);
        reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? reason.Text : null;
    }

    public async Task<string?> AskVoidReasonAsync(int defectId, string defectType)
    {
        var reason = new TextBox
        {
            Header = "Void gerekçesi (zorunlu)",
            PlaceholderText = "Örn. yanlış hata tipi seçildi, kusur başka parçaya ait",
            AcceptsReturn = true,
            Height = 80,
            TextWrapping = TextWrapping.Wrap,
        };

        var panel = new StackPanel { Spacing = 10, Width = 480 };
        panel.Children.Add(new TextBlock
        {
            Text = $"#{defectId} {defectType} kaydı geçersiz sayılacak. Kayıt silinmez ama rapora girmez ve bu işlem geri alınamaz.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(reason);

        var dialog = NewDialog("Kusuru void et", panel, "Void et", "Vazgeç", primaryEnabled: false);
        reason.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reason.Text);

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? reason.Text : null;
    }

    public async Task<bool> ConfirmRemovePhotoAsync(int defectId, string photo)
    {
        var dialog = NewDialog(
            "Fotoğrafı kaldır",
            new TextBlock
            {
                Width = 440,
                TextWrapping = TextWrapping.Wrap,
                Text = $"#{defectId} kusurundan {photo} kaldırılacak; rapora girmez. Dosya ortak alanda saklanmaya devam eder.",
            },
            "Kaldır", "Vazgeç");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmSkipMeasurementAsync(int defectId)
    {
        var dialog = NewDialog(
            "Ölçüm girilmedi",
            new TextBlock
            {
                Width = 440,
                TextWrapping = TextWrapping.Wrap,
                Text = $"#{defectId} kusuru için henüz ölçüm kaydedilmedi. Ölçüm adımı atlanmamalıdır; " +
                       "ölçümleri girip 'Ölçümleri kaydet' ile kaydedin.\n\nYine de fotoğraf adımına geçmek istiyor musunuz?",
            },
            "Yine de fotoğrafa geç", "Ölçüme dön");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
