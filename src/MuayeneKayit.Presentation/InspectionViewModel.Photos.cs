using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

// Fotoğraflar, kamera, düzenleyici, rapor ve öneriler: ne yapılacağı burada; pencerelerin çizimi
// IWindowService / IFilePicker / IPdfConverter arkasındadır.
public sealed partial class InspectionViewModel
{
    // ───────────────────────── Fotoğraf kartları ─────────────────────────

    /// <summary>Kartlar; işaret sayısı ve arka plan durumu ile.</summary>
    private void UpdatePhotos(Defect? defect)
    {
        Photos.Clear();
        if (defect is not null && _folder is not null)
        {
            var directory = Path.Combine(_folder, "defects");
            foreach (var photo in defect.Photos)
            {
                var markup = defect.Markups.GetValueOrDefault(photo);
                var clean = markup?.CleanedFile;
                var shown = clean is not null && File.Exists(Path.Combine(directory, clean)) ? clean : photo;

                var info = new List<string>();
                if (markup?.Shapes.Count > 0) info.Add($"{markup.Shapes.Count} işaret");
                if (shown != photo) info.Add("arka plan kaldırıldı");

                Photos.Add(new PhotoItemViewModel
                {
                    FileName = photo,
                    Label = $"{defect.Id}.{defect.Photos.IndexOf(photo) + 1}",
                    DisplayFile = shown,
                    ThumbnailPath = Path.Combine(directory, shown),
                    InfoText = info.Count == 0 ? "İşaret yok" : string.Join(" · ", info),
                    HasInfo = info.Count > 0,
                    HasMarks = markup?.Shapes.Count > 0,
                    IsEditable = IsEditable,
                });
            }
        }
        HasPhotos = Photos.Count > 0;
        NoPhotos = Photos.Count == 0;
    }

    // ───────────────────────── Dosyadan ekle ─────────────────────────

    public Task AddPhotoFromFileAsync() => RunAsync(async () =>
    {
        RequireSelection();
        AutoSave();

        var path = await _filePicker.PickImageAsync();
        if (path is null)
            return;

        var id = SelectedId!.Value;
        var folder = _folder!;
        var name = PhotoImporter.Import(folder, id, path);

        _store.UpdateDefect(folder, id, d => d.Photos.Add(name));
        _audit.Append(_user, "defect.photo", $"#{id} {name}");
        Refresh(id);
        Send($"Fotoğraf eklendi: {name}", StatusKind.Success);
    });

    // ───────────────────────── Kamera ─────────────────────────

    /// <summary>
    /// Kamera penceresini açar. Fotoğraflar, pencere açılırken seçili olan kusura kaydedilir;
    /// sonradan listede başka kusur seçilse bile hedef değişmez.
    /// </summary>
    public void OpenCamera() => Run(() =>
    {
        RequireSelection();
        AutoSave();

        var id = SelectedId!.Value;
        var folder = _folder!;
        var by = _user;
        var target = $"#{id} {Current!.DefectType}   ·   {_session!.PartNumber} / {_session!.OperationNumber} / {_session!.SerialNumber}";

        _windows.OpenCamera(new CameraRequest(target, id),
            photo =>
            {
                var name = PhotoImporter.Import(folder, id, photo.TempPath);
                _store.UpdateDefect(folder, id, d =>
                {
                    d.Photos.Add(name);
                    // Çerçeve açıkken çekildiyse ortadaki kare kusur işareti olarak kaydedilir.
                    if (photo.Framed && photo.Width > 0 && photo.Height > 0)
                    {
                        d.Markups[name] = new PhotoMarkup
                        {
                            ImageWidth = photo.Width,
                            ImageHeight = photo.Height,
                            Shapes = { Annotation.CenterSquare(photo.Width, photo.Height, id, photo.FrameFraction) },
                        };
                    }
                });
                _audit.Append(by, "defect.photo", $"#{id} {name} (kamera{(photo.Framed ? ", çerçeveli" : "")})");
                return name;
            },
            saved =>
            {
                // Fotoğraf kaydedildiyse doğrudan işaretleme ekranına geçilir.
                if (saved is not null && SelectedId == id && _folder == folder)
                {
                    Refresh(id);
                    SelectedStep = InspectionStep.Photo;
                    OpenEditor(saved);
                }
            });
    });

    // ───────────────────────── Düzenleyici ─────────────────────────

    /// <summary>Fotoğrafı işaretleme / arka plan düzenleyicisinde açar (kapalı kusurda yalnızca görüntüler).</summary>
    public void OpenEditor(string? photo) => Run(() =>
    {
        if (photo is null || !PhotoMarkupValidator.IsPhotoFileName(photo))
            throw new InvalidOperationException("Fotoğraf okunamadı.");

        RequireSelection();

        var id = SelectedId!.Value;
        var folder = _folder!;
        var by = _user;
        var defects = _store.LoadDefects(folder);
        var defect = defects.First(d => d.Id == id);
        var state = DefectStateMachine.GetState(defect);
        var editable = state is not (DefectState.Closed or DefectState.Voided or DefectState.Superseded or DefectState.AwaitingReinspect);

        if (!editable)
        {
            // Kapalı kusurda yalnızca görüntüleme: işaretli hali rapordan görülür, ham fotoğraf burada açılır.
            _windows.OpenFile(Path.Combine(folder, "defects", defect.Markups.GetValueOrDefault(photo)?.CleanedFile ?? photo));
            return;
        }

        var labels = defects
            .Where(d => DefectStateMachine.GetState(d) != DefectState.Voided)
            .Select(d => d.Id)
            .ToList();

        _windows.OpenPhotoEditor(new PhotoEditorRequest(
                $"#{id} {defect.DefectType}   ·   {photo}   ·   {_session!.PartNumber} / {_session!.OperationNumber} / {_session!.SerialNumber}",
                Path.Combine(folder, "defects"),
                photo,
                defect.Markups.GetValueOrDefault(photo),
                labels,
                id),
            result =>
            {
                var markup = result.Markup;
                if (result.NewCleanJpeg is not null)
                    markup.CleanedFile = PhotoImporter.WriteCleaned(folder, photo, result.NewCleanJpeg);

                var errors = PhotoMarkupValidator.Validate(markup);
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(" ", errors));

                _store.UpdateDefect(folder, id, d =>
                {
                    if (markup.Shapes.Count == 0 && markup.CleanedFile is null)
                        d.Markups.Remove(photo);
                    else
                        d.Markups[photo] = markup;
                });
                _audit.Append(by, "defect.markup",
                    $"#{id} {photo} {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırıldı")}");

                if (SelectedId == id && _folder == folder)
                    Refresh(id);
                Send($"{photo} kaydedildi: {markup.Shapes.Count} işaret{(markup.CleanedFile is null ? "" : ", arka plan kaldırılmış kopya")}. Orijinal fotoğraf korunuyor.", StatusKind.Success);
            });
    });

    // ───────────────────────── Rapor ─────────────────────────

    /// <summary>Rapor üretilirken düğme kilitlenir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateReport))]
    private bool _isReportRunning;

    public bool CanCreateReport => IsSessionOpen && !IsReportRunning;

    /// <summary>Durum çubuğunu kapatma isteği (PDF başarılıysa bildirim kalmaz).</summary>
    public event Action? StatusCleared;

    /// <summary>
    /// Raporu HTML olarak yazar, ardından bilgisayardaki Edge ile yerelde PDF'e çevirir (ağ kullanılmaz).
    /// PDF üretilemezse HTML açılır; tarayıcıdan yazdırılabilir.
    /// </summary>
    public Task CreateReportAsync() => RunAsync(async () =>
    {
        AutoSave();
        try
        {
            RequireSession();
            IsReportRunning = true;

            var reportsDir = Path.Combine(_folder!, "reports");
            Directory.CreateDirectory(reportsDir);

            var version = ReportFiles.LatestVersion(_folder!) + 1;
            var htmlPath = Path.Combine(reportsDir, $"Rapor_v{version}.html");
            var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");

            var defects = _store.LoadDefects(_folder!);
            AtomicFile.WriteAllText(htmlPath, ReportBuilder.Build(_session!, defects, version));

            Send($"Rapor v{version} hazırlanıyor (PDF)...", StatusKind.Info);
            var pdfError = await _pdf.TryConvertAsync(htmlPath, pdfPath);

            // PDF asıl kayıttır (fotoğraflar içinde). HTML yalnızca ara dosya: PDF oluştuysa silinir,
            // oluşmadıysa yedek olarak kalır ve açılır.
            if (pdfError is null)
                ReportFiles.DeleteHtml(htmlPath);

            _audit.Append(_user, "report.create", $"Rapor_v{version}{(pdfError is null ? " (pdf)" : "")}");
            _windows.OpenFile(pdfError is null ? pdfPath : htmlPath);
            if (pdfError is null)
                StatusCleared?.Invoke();
            else
                Send($"Rapor v{version} HTML olarak açıldı (PDF üretilemedi: {pdfError}). Tarayıcıdan 'PDF olarak kaydet' ile yazdırabilirsiniz.",
                    StatusKind.Warning);
        }
        finally
        {
            IsReportRunning = false;
        }
    });

    // ───────────────────────── Öneriler ─────────────────────────

    /// <summary>
    /// Önceki kayıtlardaki parça, operasyon ve seri numaralarını önerir; yazılan metne göre süzer.
    /// Üst alan boş ya da geçersizse öneri yoktur.
    /// </summary>
    public IReadOnlyList<string> Suggestions(SuggestLevel level, string text)
    {
        text = text.Trim();
        return FolderNames(level)
            .Where(n => text.Length == 0 || n.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();
    }

    private IEnumerable<string> FolderNames(SuggestLevel level)
    {
        try
        {
            var root = _store.Root;
            string folder;
            if (level == SuggestLevel.Part)
                folder = root;
            else if (level == SuggestLevel.Operation)
                folder = Path.Combine(root, SafePath.ValidateSegment(PartNumber, "Parça no"));
            else
                folder = Path.Combine(root,
                    SafePath.ValidateSegment(PartNumber, "Parça no"),
                    SafePath.ValidateSegment(OperationNumber, "Operasyon no"));

            if (!Directory.Exists(folder))
                return Array.Empty<string>();

            return Directory.EnumerateDirectories(folder)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('_'))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Üst alan boş ya da geçersizse öneri yok.
            return Array.Empty<string>();
        }
    }
}
