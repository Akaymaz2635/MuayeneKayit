using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    /// <param name="safeDefault">
    /// İçinde odaklanacak giriş kutusu olmayan (yalnızca metin) onay pencereleri için true: varsayılan düğme vazgeç düğmesidir,
    /// Enter / Space onaylamaz ("Enter hiçbir onay düğmesini tetiklemez").
    /// </param>
    private ContentDialog NewDialog(string title, object content, string primary, string close, bool primaryEnabled = true, bool safeDefault = false)
    {
        var xamlRoot = _xamlRoot?.Invoke() ?? throw new InvalidOperationException("Pencere henüz hazır değil.");
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = safeDefault ? ContentDialogButton.Close : ContentDialogButton.None,
            PrimaryButtonStyle = ConfirmButtonStyle,
            IsPrimaryButtonEnabled = primaryEnabled,
            XamlRoot = xamlRoot,
            // Pencere kökündeki tema (Açık / Koyu) diyaloğa da uygulanır.
            RequestedTheme = (xamlRoot.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
        };

        // İçerikte odaklanacak kutu yoksa ilk odak onay düğmesine düşer ve Enter onaylar (ör. fotoğraf kaldırılır).
        // Odak onay düğmesindeyse vazgeç düğmesine taşınır: Enter / Space güvenli seçeneği seçer.
        dialog.Opened += (_, _) => dialog.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (FocusManager.GetFocusedElement(xamlRoot) is Button { Name: "PrimaryButton" })
                FindButton(dialog, "CloseButton")?.Focus(FocusState.Programmatic);
        });
        return dialog;
    }

    private static Button? FindButton(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button { } b && b.Name == name)
                return b;
            if (FindButton(child, name) is Button found)
                return found;
        }
        return null;
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

        // 2+ aktif bölge: karar hangi bölgeler için? Varsayılan hepsi işaretli; işaretsiz bölge uygun (Conforms) sayılır.
        var regionChecks = new List<(PhotoRegion Info, CheckBox Box)>();
        if (request.Regions is { Count: >= 2 } regions)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Bu karar hangi bölgeler için?",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            foreach (var region in regions)
            {
                var box = new CheckBox
                {
                    IsChecked = true,
                    MinWidth = 0,
                    Content = RegionControls.Header(region),
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, "Bölge " + region.Label);
                regionChecks.Add((region, box));
                panel.Children.Add(box);
            }
            panel.Children.Add(new TextBlock
            {
                Text = "İşaretlemediğiniz bölge uygun (Conforms) sayılır; sonraki kararlar ve muayeneler yalnızca işaretli bölgeleri sorar.",
                Style = Res("SecondaryTextStyle"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        int CheckedRegions() => regionChecks.Count(r => r.Box.IsChecked == true);

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

        var needsRegions = regionChecks.Count > 0;
        var dialog = NewDialog(request.Label, panel, "Kararı kaydet", "Vazgeç",
            primaryEnabled: DecisionForm.CanSave(request.NoteRequired, note.Text, request.NeedsReworkScope, SelectedScope(), needsRegions, CheckedRegions()));
        void UpdateSave() => dialog.IsPrimaryButtonEnabled =
            DecisionForm.CanSave(request.NoteRequired, note.Text, request.NeedsReworkScope, SelectedScope(), needsRegions, CheckedRegions());
        note.TextChanged += (_, _) => UpdateSave();
        if (scope is not null)
            scope.SelectionChanged += (_, _) => UpdateSave();
        foreach (var (_, box) in regionChecks)
        {
            box.Checked += (_, _) => UpdateSave();
            box.Unchecked += (_, _) => UpdateSave();
        }

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return new DecisionInput(target?.Text, reference?.Text, note.Text, SelectedScope(),
            needsRegions ? regionChecks.Where(r => r.Box.IsChecked == true).Select(r => r.Info.Photo).ToList() : null);
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
            "Sonucu şimdi gir", "Sonra", safeDefault: true);
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

        // Aktif bölge 2+ ise her bölge için ayrı sonuç; 0 ya da 1 ise bugünkü 3 seçenek.
        var multi = request.Regions is { Count: >= 2 };
        var regionRows = new List<(PhotoRegion Info, RadioButtons Choice, NewDefectListEditor News)>();
        var outcome = new RadioButtons { Header = "1. Muayene sonucu ne?" };
        NewDefectListEditor? singleNews = null;
        var summary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Visibility = Vis(multi),
        };

        // 1. adım: sonuç
        if (!multi)
        {
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
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = "1. Her bölge (fotoğraf) için muayene sonucu ne?",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (var region in request.Regions!)
            {
                var choice = new RadioButtons { MaxColumns = 3 };
                choice.Items.Add(new RadioButton { Content = "Giderildi", Tag = PhotoOutcome.Conforms });
                choice.Items.Add(new RadioButton { Content = "Devam ediyor", Tag = PhotoOutcome.Continues });
                choice.Items.Add(new RadioButton { Content = "Yeni hata", Tag = PhotoOutcome.NewDefect });
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(choice, "Bölge " + region.Label + " sonucu");

                var news = new NewDefectListEditor(request.TypeNames, "Bu bölgeye hata ekle") { Visibility = Visibility.Collapsed };
                regionRows.Add((region, choice, news));

                var card = new StackPanel { Spacing = 6 };
                card.Children.Add(RegionControls.Header(region));
                card.Children.Add(choice);
                card.Children.Add(news);
                panel.Children.Add(new Border
                {
                    Style = Res("SubtleCardStyle"),
                    Padding = new Thickness(10),
                    Child = card,
                });
            }
        }

        // 2. adım (aynı kusur): yeni ölçümler, boş gelir
        var headers = new (MeasureField Field, string Header, double? Old)[]
        {
            (MeasureField.Depth, Units.Header("Derinlik", request.Unit), before.Depth),
            (MeasureField.Width, Units.Header("Genişlik", request.Unit), before.Width),
            (MeasureField.Length, Units.Header("Uzunluk", request.Unit), before.Length),
            (MeasureField.Radius, Units.Header("Çap", request.Unit), before.Radius),
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
        // Admin'de tanımlanan ek ölçümler (ör. Yükseklik): boş kalan önceki değerinde kalır.
        var customBoxes = new List<(CustomFieldDef Def, TextBox Box)>();
        foreach (var def in request.CustomFields ?? Array.Empty<CustomFieldDef>())
        {
            var old = CustomMeasures.Find(before.Custom, def.Key);
            var box = new TextBox
            {
                Header = CustomMeasures.Header(def, request.Unit),
                Width = 120,
                PlaceholderText = old is null ? "" : $"önce: {CustomMeasures.EditText(old, request.Unit)}",
            };
            customBoxes.Add((def, box));
            measureGrid.Children.Add(box);
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

        // 2. adım (yeni hata, tek sonuç): bir ya da daha fazla yeni hatanın tipi, varsayılan seçili değil
        singleNews = multi ? null : new NewDefectListEditor(request.TypeNames, "Hata ekle");
        var newPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed,
        };
        if (singleNews is not null)
        {
            newPanel.Children.Add(new TextBlock { Text = "2. Yeni hataların tipi (her satır ayrı bir yeni kusur olur)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            newPanel.Children.Add(singleNews);
            newPanel.Children.Add(new TextBlock
            {
                Text = $"#{request.DefectId} kapanmış sayılmaz, yeni kusur(lar)a devredilir ('Defect {request.DefectId} continues as Defect N'). Yeni kusurların ölçümlerini ve fotoğraflarını kaydettikten sonra girersiniz.",
                TextWrapping = TextWrapping.Wrap,
                Style = Res("SecondaryTextStyle"),
                FontSize = 12,
            });
        }
        panel.Children.Add(newPanel);
        panel.Children.Add(summary);

        var note = new TextBox { Header = "Not (isteğe bağlı)", AcceptsReturn = true, Height = 60, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(note);

        var dialog = NewDialog(
            $"#{request.DefectId} {request.DefectType}: {(request.IsRework ? "rework sonrası muayene" : "yeniden kontrol sonucu")}",
            panel, "Kaydet", "Vazgeç", primaryEnabled: false);

        ReinspectOutcome? Selected() => (outcome.SelectedItem as RadioButton)?.Tag as ReinspectOutcome?;
        bool? HighMetalAnswer() => (highMetal?.SelectedItem as RadioButton)?.Tag as bool?;

        // Bölge seçimleri (çok bölgeli mod): seçilmemiş bölgenin sonucu null.
        IReadOnlyList<RegionChoice> Choices() => regionRows
            .Select(r => new RegionChoice(r.Info.Photo, (r.Choice.SelectedItem as RadioButton)?.Tag as PhotoOutcome?, r.News.Types))
            .ToList();

        // Sayı olmayan ölçüm metni "boş" sayılmaz: satır içi hata gösterilir, Kaydet kapalı kalır (kural ReinspectForm.ParseEntry).
        var entryError = new TextBlock { TextWrapping = TextWrapping.Wrap, Style = Res("WarnTextStyle"), Visibility = Visibility.Collapsed };
        samePanel.Children.Add(entryError);

        List<string> InvalidEntries()
        {
            var bad = new List<string>();
            foreach (var (field, box) in boxes)
                if (ReinspectForm.ParseEntry(box.Text, out _) == ReinspectForm.EntryState.Invalid)
                    bad.Add(headers.First(h => h.Field == field).Header);
            foreach (var (def, box) in customBoxes)
                if (def.Kind != CustomFieldKind.Text && ReinspectForm.ParseEntry(box.Text, out _) == ReinspectForm.EntryState.Invalid)
                    bad.Add(CustomMeasures.Header(def, request.Unit));
            return bad;
        }

        MeasurementSnapshot ReadValues()
        {
            static double? Try(TextBox? b) =>
                b is not null && ReinspectForm.ParseEntry(b.Text, out var v) == ReinspectForm.EntryState.Valid ? v : null;
            return new MeasurementSnapshot(
                Try(boxes.GetValueOrDefault(MeasureField.Depth)), Try(boxes.GetValueOrDefault(MeasureField.Width)),
                Try(boxes.GetValueOrDefault(MeasureField.Length)), Try(boxes.GetValueOrDefault(MeasureField.Radius)),
                Try(boxes.GetValueOrDefault(MeasureField.Angle)),
                string.IsNullOrWhiteSpace(colorBox?.Text) ? null : colorBox!.Text.Trim(),
                Custom: CustomMeasures.Clean(customBoxes.Select(c => CustomMeasures.Of(c.Def, Try(c.Box), c.Box.Text))));
        }

        void Refresh()
        {
            var invalid = InvalidEntries();
            entryError.Text = invalid.Count == 0 ? "" : "Sayı olmalı: " + string.Join(", ", invalid) + ". Boş bırakırsanız önceki değer kalır.";
            entryError.Visibility = Vis(invalid.Count > 0);
            var entered = ReadValues();
            var anyValue = ReinspectForm.AnyValue(entered);
            bool showMeasure;

            if (multi)
            {
                var choices = Choices();
                foreach (var (_, choice, news) in regionRows)
                    news.Visibility = Vis((choice.SelectedItem as RadioButton)?.Tag as PhotoOutcome? == PhotoOutcome.NewDefect);
                showMeasure = ReinspectForm.AnyContinues(choices);
                summary.Text = ReinspectForm.Summary(request.DefectId, choices);
                dialog.IsPrimaryButtonEnabled = invalid.Count == 0 && ReinspectForm.CanSaveRegions(choices, hadHighMetal, HighMetalAnswer(), anyValue);
            }
            else
            {
                var tag = Selected();
                newPanel.Visibility = Vis(tag == ReinspectOutcome.New);
                showMeasure = tag == ReinspectOutcome.Same;
                dialog.IsPrimaryButtonEnabled = invalid.Count == 0 && ReinspectForm.CanSave(tag, hadHighMetal, HighMetalAnswer(), anyValue, singleNews!.Types);
            }

            samePanel.Visibility = Vis(showMeasure);
            if (showMeasure)
            {
                var merged = ReinspectForm.Merge(before, entered, HighMetalAnswer());
                delta.Text = ReinspectForm.DeltaText(before, merged, anyValue, highMetal is not null && HighMetalAnswer() is not null, request.Unit);
            }
        }

        outcome.SelectionChanged += (_, _) => Refresh();
        if (singleNews is not null)
            singleNews.Changed += Refresh;
        foreach (var (_, choice, news) in regionRows)
        {
            choice.SelectionChanged += (_, _) => Refresh();
            news.Changed += Refresh;
        }
        if (highMetal is not null)
            highMetal.SelectionChanged += (_, _) => Refresh();
        foreach (var box in boxes.Values.Append(colorBox).Concat(customBoxes.Select(c => c.Box)).OfType<TextBox>())
            box.TextChanged += (_, _) => Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        if (multi)
        {
            var choices = Choices();
            return new ReinspectInput(ReinspectForm.OutcomeFor(choices), ReadValues(), HighMetalAnswer(), null, note.Text, choices);
        }

        var result = Selected() ?? throw new InvalidOperationException("Muayene sonucunu seçin.");
        return new ReinspectInput(result, ReadValues(), HighMetalAnswer(), singleNews!.Types, note.Text);
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
        if (request.ChildrenToVoid.Count > 0)
        {
            var single = request.ChildrenToVoid.Count == 1;
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Message = (single
                              ? $"Bu kararla açılan yeni kusur {IdList(request.ChildrenToVoid)} void edilecek (silinmez, rapora girmez). "
                              : $"Bu kararla açılan yeni kusurlar {IdList(request.ChildrenToVoid)} void edilecek (silinmez, rapora girmez). ") +
                          $"#{request.DefectId} tekrar 'rework sonrası muayene bekliyor' olur; bölgelerin durumu karardan önceki hâline döner.",
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
            "Kaldır", "Vazgeç", safeDefault: true);
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Güvenli seçenek ("Ölçüme dön") vurgulu ve varsayılandır: Enter ve Esc ölçümde bırakır.
    /// Atlamak için ikincil düğmeye bilerek basılmalıdır.
    /// </summary>
    public async Task<bool> ConfirmSkipMeasurementAsync(int defectId)
    {
        var xamlRoot = _xamlRoot?.Invoke() ?? throw new InvalidOperationException("Pencere henüz hazır değil.");
        var dialog = new ContentDialog
        {
            Title = "Ölçüm girilmedi",
            Content = new TextBlock
            {
                Width = 440,
                TextWrapping = TextWrapping.Wrap,
                Text = $"#{defectId} kusuru için henüz ölçüm kaydedilmedi. Ölçüm adımı atlanmamalıdır; " +
                       "ölçümleri girip kaydedin.\n\nYine de fotoğraf adımına geçmek istiyor musunuz?",
            },
            PrimaryButtonText = "Ölçüme dön",
            SecondaryButtonText = "Yine de fotoğrafa geç",
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonStyle = ConfirmButtonStyle,
            XamlRoot = xamlRoot,
            RequestedTheme = (xamlRoot.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Secondary;
    }
}
