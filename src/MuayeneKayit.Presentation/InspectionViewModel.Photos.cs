using CommunityToolkit.Mvvm.ComponentModel;
using MuayeneKayit.Core;

namespace MuayeneKayit.Presentation;

// Fotoğraflar, kamera, düzenleyici, rapor ve öneriler: ne yapılacağı burada; pencerelerin çizimi
// IWindowService / IFilePicker / IPdfConverter arkasındadır.
public sealed partial class InspectionViewModel
{
    // ───────────────────────── Fotoğraf kartları ─────────────────────────

    /// <summary>
    /// Kartlar; işaret sayısı, arka plan durumu ve (bölge sonucu kaydedildiyse) bölge rozeti ile.
    /// <paramref name="all"/>: rozetteki yeni kusur numaralarından void olanları ayıklamak için.
    /// </summary>
    private void UpdatePhotos(Defect? defect, IReadOnlyList<Defect>? all = null)
    {
        Photos.Clear();
        if (defect is not null && _folder is not null)
        {
            foreach (var photo in defect.Photos)
            {
                // Yalnızca geçerli düz fotoğraf adları listelenir ve açılır (yol dolaşımı, UNC, mutlak yol yok).
                var thumbnail = ThumbnailFor(defect, photo);
                if (thumbnail is null)
                    continue;

                var markup = defect.Markups.GetValueOrDefault(photo);
                var shown = ShownFile(defect, photo);

                var info = new List<string>();
                if (markup?.Shapes.Count > 0) info.Add($"{markup.Shapes.Count} işaret");
                if (shown != photo) info.Add("arka plan kaldırıldı");

                Photos.Add(new PhotoItemViewModel
                {
                    FileName = photo,
                    Label = $"{defect.Id}.{defect.Photos.IndexOf(photo) + 1}",
                    DisplayFile = shown,
                    ThumbnailPath = thumbnail,
                    InfoText = info.Count == 0 ? "İşaret yok" : string.Join(" · ", info),
                    HasInfo = info.Count > 0,
                    HasMarks = markup?.Shapes.Count > 0,
                    IsEditable = IsEditable,
                    RegionBadge = DefectPresentation.RegionBadge(defect, photo, all),
                });
            }
        }
        HasPhotos = Photos.Count > 0;
        NoPhotos = Photos.Count == 0;
    }

    /// <summary>Gösterilecek dosya: arka planı kaldırılmış kopya varsa (geçerli adlıysa ve diskte duruyorsa) o, yoksa orijinal.</summary>
    private string ShownFile(Defect defect, string photo)
    {
        var clean = defect.Markups.GetValueOrDefault(photo)?.CleanedFile;
        return PhotoMarkupValidator.ResolvePhotoPath(_folder!, clean) is string path && File.Exists(path) ? clean! : photo;
    }

    /// <summary>Kartın küçük resim yolu; ad geçersizse null (kart oluşturulmaz).</summary>
    private string? ThumbnailFor(Defect defect, string photo) =>
        PhotoMarkupValidator.ResolvePhotoPath(_folder!, ShownFile(defect, photo));

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
            // Yol yalnızca kusur klasöründeki geçerli ad olabilir; geçersiz temiz kopya adı yok sayılır, orijinal açılır.
            var cleaned = defect.Markups.GetValueOrDefault(photo)?.CleanedFile;
            var viewPath = PhotoMarkupValidator.ResolvePhotoPath(folder, cleaned) ?? PhotoMarkupValidator.ResolvePhotoPath(folder, photo)
                ?? throw new InvalidOperationException("Fotoğraf okunamadı.");
            _windows.OpenFile(viewPath);
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
        string? workDir = null;
        try
        {
            RequireSession();
            IsReportRunning = true;

            var folder = _folder!;
            var reportsDir = Path.Combine(folder, "reports");
            Directory.CreateDirectory(reportsDir);

            var version = ReportFiles.LatestVersion(folder) + 1;
            var htmlPath = Path.Combine(reportsDir, $"Rapor_v{version}.html");
            var pdfPath = Path.ChangeExtension(htmlPath, ".pdf");

            // Okunamayan kusur dosyası varsa rapor eksik kusurla üretilmez.
            var defects = _store.LoadDefects(folder);
            var unreadable = _store.UnreadableDefectFiles(folder);
            if (unreadable.Count > 0)
                throw new InvalidOperationException($"Rapor üretilemedi: okunamayan kusur dosyası var ({string.Join(", ", unreadable.Take(3))}). Yöneticiye bildirin.");
            var html = ReportBuilder.Build(_session!, defects, version);

            // HTML ve fotoğraflar bu bilgisayardaki geçici klasörde hazırlanır; Edge paylaşımdaki dosyaları değil yerel kopyayı
            // okur (dönüştürme sırasında paylaşımdaki dosya değiştirilse de PDF'e girmez). Yalnızca sonuç paylaşıma kopyalanır.
            workDir = Path.Combine(Path.GetTempPath(), "MuayeneKayit-rapor-" + Guid.NewGuid().ToString("N"));
            var workReports = Path.Combine(workDir, "reports");
            var workDefects = Path.Combine(workDir, "defects");
            Directory.CreateDirectory(workReports);
            Directory.CreateDirectory(workDefects);
            foreach (var file in ReportBuilder.PhotoFiles(defects))
            {
                if (PhotoMarkupValidator.ResolvePhotoPath(folder, file) is string source && File.Exists(source))
                    File.Copy(source, Path.Combine(workDefects, file), overwrite: true);
            }
            var workHtml = Path.Combine(workReports, Path.GetFileName(htmlPath));
            var workPdf = Path.ChangeExtension(workHtml, ".pdf");
            File.WriteAllText(workHtml, html, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Send($"Rapor v{version} hazırlanıyor (PDF)...", StatusKind.Info);
            var pdfError = await _pdf.TryConvertAsync(workHtml, workPdf);

            // PDF asıl kayıttır (fotoğraflar içinde) ve paylaşıma kopyalanır. HTML yalnızca ara dosyadır: PDF üretilemediyse
            // yedek olarak paylaşıma yazılır ve açılır.
            if (pdfError is null)
                PublishReport(workPdf, pdfPath);
            else
                AtomicFile.WriteAllText(htmlPath, html);

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
            if (workDir is not null)
                DeleteQuietly(workDir);
        }
    });

    /// <summary>Üretilen PDF'i paylaşıma önce geçici adla kopyalar, sonra asıl adına taşır (yarım PDF görünmez; var olan sürümün üzerine yazılmaz).</summary>
    private static void PublishReport(string workPdf, string pdfPath)
    {
        var temp = pdfPath + ".tmp";
        try
        {
            File.Copy(workPdf, temp, overwrite: true);
            File.Move(temp, pdfPath, overwrite: false);
        }
        finally
        {
            DeleteQuietly(temp);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Geçici dosya silinemezse rapor yine geçerlidir.
        }
    }

    // ───────────────────────── Öneriler ─────────────────────────

    /// <summary>
    /// Önceki kayıtlardaki parça, operasyon ve seri numaralarını önerir; yazılan metne göre süzer.
    /// Üst alan boş ya da geçersizse öneri yoktur.
    /// </summary>
    public IReadOnlyList<string> Suggestions(SuggestLevel level, string text)
    {
        text = text.Trim();
        // "×" ile gizlenenler bu bilgisayarda önerilmez; diğer kutuların gizlileri birbirini etkilemez.
        var hidden = _settings.HiddenSuggestions(level);
        return FolderNames(level)
            .Where(n => !hidden.Contains(n))
            .Where(n => text.Length == 0 || n.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();
    }

    /// <summary>
    /// Öneriyi bu bilgisayarda gizler (klasör silinmez). Gizlenen değerle muayene açılınca öneri geri gelir.
    /// View listeyi yeniler.
    /// </summary>
    public void HideSuggestion(SuggestLevel level, string value) => _settings.HideSuggestion(level, value);

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
