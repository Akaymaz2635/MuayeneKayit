using MuayeneKayit.Core;
using MuayeneKayit.Presentation;

// Faz 3: fotoğraflar, kamera, düzenleyici, rapor ve öneriler (ViewModelTests'in devamı).
public static partial class ViewModelTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 };

    private static string TempJpeg(Ctx c, string name = "kaynak.jpg")
    {
        var path = Path.Combine(Path.GetTempPath(), "MuayeneKayit-src-" + Guid.NewGuid().ToString("N") + "-" + name);
        File.WriteAllBytes(path, JpegBytes);
        return path;
    }

    public static void VmPhotoCardsShowLabelsAndCleanCopy() => With(c =>
    {
        AddMeasuredDent(c);
        var dir = Path.Combine(c.Folder, "defects");
        Directory.CreateDirectory(dir);
        c.Store.UpdateDefect(c.Folder, 1, d =>
        {
            d.Photos.Add("1_photo_01.jpg");
            d.Photos.Add("1_photo_02.jpg");
            d.Markups["1_photo_01.jpg"] = new PhotoMarkup
            {
                ImageWidth = 100, ImageHeight = 100,
                Shapes = { Annotation.CenterSquare(100, 100, 1), Annotation.CenterSquare(100, 100, 1) },
                CleanedFile = "1_photo_01_clean.jpg",
            };
        });
        File.WriteAllBytes(Path.Combine(dir, "1_photo_01_clean.jpg"), JpegBytes);
        c.Vm.Refresh(1);

        Check(c.Vm.HasPhotos && !c.Vm.NoPhotos && c.Vm.Photos.Count == 2, "İki kart beklenir");
        var first = c.Vm.Photos[0];
        Check(first.Label == "1.1" && c.Vm.Photos[1].Label == "1.2", $"Etiketler: {first.Label} {c.Vm.Photos[1].Label}");
        Check(first.DisplayFile == "1_photo_01_clean.jpg", $"Temiz kopya gösterilmeli: {first.DisplayFile}");
        Check(first.InfoText == "2 işaret · arka plan kaldırıldı" && first.HasMarks && first.HasInfo, $"Bilgi: {first.InfoText}");
        Check(c.Vm.Photos[1].InfoText == "İşaret yok" && !c.Vm.Photos[1].HasInfo, "İşaretsiz kartta 'İşaret yok'");
        Check(first.IsEditable && first.EditButtonText == "İşaretle", "Açık kusurda İşaretle");

        // Temiz kopya dosyası yoksa orijinal gösterilir.
        File.Delete(Path.Combine(dir, "1_photo_01_clean.jpg"));
        c.Vm.Refresh(1);
        Check(c.Vm.Photos[0].DisplayFile == "1_photo_01.jpg" && c.Vm.Photos[0].InfoText == "2 işaret", "Dosya yoksa orijinal");

        c.Vm.ChangeSession();
        Check(c.Vm.Photos.Count == 0 && c.Vm.NoPhotos, "Oturum değişince kartlar temizlenmeli");
    });

    public static void VmPhotoCardsAreViewOnlyWhenClosed() => With(c =>
    {
        AddMeasuredDent(c);
        c.Store.UpdateDefect(c.Folder, 1, d =>
        {
            d.Photos.Add("1_photo_01.jpg");
            DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya", reference: "S-1");
        });
        c.Vm.Refresh(1);
        Check(!c.Vm.Photos[0].IsEditable && c.Vm.Photos[0].EditButtonText == "Görüntüle", "Kapalı kusurda Görüntüle");
    });

    public static void VmAddPhotoFromFile() => With(c =>
    {
        AddMeasuredDent(c);

        // Vazgeçilirse değişmez.
        Wait(c.Vm.AddPhotoFromFileAsync());
        Check(c.Picker.Calls == 1 && c.Vm.Photos.Count == 0, "Seçim yoksa fotoğraf eklenmemeli");

        c.Picker.Picks.Enqueue(TempJpeg(c));
        Wait(c.Vm.AddPhotoFromFileAsync());
        Check(c.Vm.Photos.Count == 1 && Load(c, 1).Photos.Count == 1, "Fotoğraf eklenmeli");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|defect.photo|#1 1_photo_01.jpg")), "Denetim satırı beklenir");
        Check(c.Notes.Last() == ("Fotoğraf eklendi: 1_photo_01.jpg", StatusKind.Success), $"Bildirim: {c.Notes.Last().Message}");

        // Sahte uzantı: hata bildirilir, eklenmez.
        var bad = Path.Combine(Path.GetTempPath(), "MuayeneKayit-bad-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4 });
        c.Picker.Picks.Enqueue(bad);
        Wait(c.Vm.AddPhotoFromFileAsync());
        Check(c.Notes.Last().Kind == StatusKind.Error && c.Vm.Photos.Count == 1, "Geçersiz dosya hata vermeli");
    });

    public static void VmCameraSavesFramedMarkupAndOpensEditor() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.OpenCamera();
        var cam = c.Windows.Cameras.Single();
        Check(cam.Request.DefectId == 1 && cam.Request.Target == "#1 Dent   ·   PD170 / OP40 / SN001", $"Hedef: {cam.Request.Target}");

        var name = cam.OnSave(new CapturedPhotoData(TempJpeg(c), 1000, 500, true, 0.4));
        Check(name == "1_photo_01.jpg", $"Ad: {name}");
        var markup = Load(c, 1).Markups[name];
        var expected = Annotation.CenterSquare(1000, 500, 1, 0.4);
        Check(markup.ImageWidth == 1000 && markup.Shapes.Count == 1 && markup.Shapes[0].X1 == expected.X1 && markup.Shapes[0].Y2 == expected.Y2,
            "Çerçeveli çekimde ortadaki kare işaret olmalı");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|defect.photo|#1 1_photo_01.jpg (kamera, çerçeveli)")), "Kamera denetim satırı");

        // Çerçevesiz ikinci çekim: işaret yok.
        var second = cam.OnSave(new CapturedPhotoData(TempJpeg(c), 1000, 500, false, 0.2));
        Check(!Load(c, 1).Markups.ContainsKey(second), "Çerçevesiz çekimde işaret olmamalı");

        // Kapanınca: liste yenilenir, Fotoğraf sekmesi seçilir, düzenleyici açılır.
        c.Vm.SelectedStep = InspectionStep.Measure;
        cam.OnClosed(name);
        Check(c.Vm.Photos.Count == 2, "Liste yenilenmeli");
        Check(c.Vm.SelectedStep == InspectionStep.Photo, "Fotoğraf sekmesi seçilmeli");
        Check(c.Windows.Editors.Count == 1 && c.Windows.Editors[0].Request.Photo == name, "Düzenleyici açılmalı");
    });

    public static void VmCameraClosedWithoutPhotoDoesNothing() => With(c =>
    {
        AddMeasuredDent(c);
        c.Vm.OpenCamera();
        c.Windows.Cameras.Single().OnClosed(null);
        Check(c.Windows.Editors.Count == 0, "Kayıt yoksa düzenleyici açılmamalı");
    });

    public static void VmEditorSaveWritesMarkupAndCleanCopy() => With(c =>
    {
        AddMeasuredDent(c);
        c.Picker.Picks.Enqueue(TempJpeg(c));
        Wait(c.Vm.AddPhotoFromFileAsync());
        const string photo = "1_photo_01.jpg";

        c.Vm.OpenEditor(photo);
        var editor = c.Windows.Editors.Single();
        Check(editor.Request.CurrentId == 1 && editor.Request.Labels.SequenceEqual(new[] { 1 }), "Etiket listesi void olmayan kusurlar");
        Check(editor.Request.Title == "#1 Dent   ·   1_photo_01.jpg   ·   PD170 / OP40 / SN001", $"Başlık: {editor.Request.Title}");

        // İşaret ve temiz kopya ile kaydet.
        var markup = new PhotoMarkup { ImageWidth = 100, ImageHeight = 100, Shapes = { Annotation.CenterSquare(100, 100, 1) } };
        editor.OnSave(new PhotoEditResultData(markup, JpegBytes));
        var saved = Load(c, 1).Markups[photo];
        Check(saved.CleanedFile == "1_photo_01_clean.jpg" && File.Exists(Path.Combine(c.Folder, "defects", saved.CleanedFile)), "Temiz kopya yazılmalı");
        Check(c.Vm.Photos[0].InfoText == "1 işaret · arka plan kaldırıldı", $"Kart: {c.Vm.Photos[0].InfoText}");
        Check(c.Notes.Last().Message == "1_photo_01.jpg kaydedildi: 1 işaret, arka plan kaldırılmış kopya. Orijinal fotoğraf korunuyor.", c.Notes.Last().Message);
        Check(c.Audit.Entries.Any(e => e.EndsWith("|defect.markup|#1 1_photo_01.jpg 1 işaret, arka plan kaldırıldı")), "İşaret denetim satırı");

        // İşaret yok ve temiz kopya yok: kayıt Markups'tan çıkar.
        editor.OnSave(new PhotoEditResultData(new PhotoMarkup { ImageWidth = 100, ImageHeight = 100 }, null));
        Check(!Load(c, 1).Markups.ContainsKey(photo), "Boş işaret kaydı kaldırılmalı");
    });

    public static void VmOpenEditorOnClosedDefectOpensFile() => With(c =>
    {
        AddMeasuredDent(c);
        c.Store.UpdateDefect(c.Folder, 1, d =>
        {
            d.Photos.Add("1_photo_01.jpg");
            d.Markups["1_photo_01.jpg"] = new PhotoMarkup { CleanedFile = "1_photo_01_clean.jpg" };
            DefectStateMachine.Apply(d, Decision.Scrap, DecisionSource.Engineer, "M. Kaya", reference: "S-1");
        });
        c.Vm.Refresh(1);

        c.Vm.OpenEditor("1_photo_01.jpg");
        Check(c.Windows.Editors.Count == 0, "Kapalı kusurda düzenleyici açılmamalı");
        Check(c.Windows.OpenedFiles.Single() == Path.Combine(c.Folder, "defects", "1_photo_01_clean.jpg"), "Temiz kopya açılmalı");

        // Geçersiz ad hata verir.
        c.Vm.OpenEditor("../x.jpg");
        Check(c.Notes.Last() == ("Fotoğraf okunamadı.", StatusKind.Error), "Geçersiz ad hatası");
    });

    public static void VmReportWritesHtmlAndOpensPdf() => With(c =>
    {
        AddMeasuredDent(c);
        Check(c.Vm.CanCreateReport, "Oturum açıkken rapor düğmesi açık");

        Wait(c.Vm.CreateReportAsync());
        var html = Path.Combine(c.Folder, "reports", "Rapor_v1.html");
        // HTML ve fotoğraflar yerel geçici klasörde hazırlanır (paylaşımda değil); yalnızca PDF paylaşıma kopyalanır.
        var call = c.Pdf.Calls.Single();
        var temp = Path.GetFullPath(Path.GetTempPath());
        Check(Path.GetFileName(call.Html) == "Rapor_v1.html" && Path.GetFullPath(call.Html).StartsWith(temp, StringComparison.OrdinalIgnoreCase)
              && !Path.GetFullPath(call.Html).StartsWith(Path.GetFullPath(c.Folder), StringComparison.OrdinalIgnoreCase),
            $"PDF dönüştürücü yerel geçici klasördeki HTML'den çağrılmalı: {call.Html}");
        Check(Path.GetFileName(call.Pdf) == "Rapor_v1.pdf" && Path.GetFullPath(call.Pdf).StartsWith(temp, StringComparison.OrdinalIgnoreCase), "PDF önce yerel geçici klasöre yazılmalı");
        Check(!File.Exists(call.Html) && !Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(call.Html))), "Geçici klasör silinmeli");
        Check(!File.Exists(html) && File.Exists(Path.ChangeExtension(html, ".pdf")), "Paylaşıma yalnızca PDF kopyalanmalı, HTML yazılmamalı");
        Check(!Directory.EnumerateFiles(Path.Combine(c.Folder, "reports"), "*.tmp").Any(), "Paylaşımda .tmp kalmamalı");
        Check(c.Windows.OpenedFiles.Single() == Path.Combine(c.Folder, "reports", "Rapor_v1.pdf"), "PDF açılmalı");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|report.create|Rapor_v1 (pdf)")), "Rapor denetim satırı");
        Check(!c.Notes.Any(n => n.Kind == StatusKind.Warning || n.Kind == StatusKind.Error), "PDF başarılıysa uyarı yok");
        Check(c.Notes.Last().Message == "Rapor v1 hazırlanıyor (PDF)...", "Hazırlanıyor bildirimi");
        Check(c.Vm.CanCreateReport && !c.Vm.IsReportRunning, "Düğme yeniden açılmalı");

        // İkinci rapor sürümü artırır (HTML silinmiş olsa da sürüm PDF'ten sayılır).
        Wait(c.Vm.CreateReportAsync());
        Check(File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v2.pdf")) && File.Exists(Path.Combine(c.Folder, "reports", "Rapor_v1.pdf")),
            "Sürüm artmalı, eski PDF korunmalı");
    });

    public static void VmReportFallsBackToHtml() => With(c =>
    {
        AddMeasuredDent(c);
        c.Pdf.Error = "Microsoft Edge bulunamadı";

        Wait(c.Vm.CreateReportAsync());
        var html = Path.Combine(c.Folder, "reports", "Rapor_v1.html");
        Check(c.Windows.OpenedFiles.Single() == html && File.Exists(html), "PDF yoksa HTML paylaşıma yedek olarak yazılmalı ve açılmalı");
        Check(!Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(c.Pdf.Calls.Single().Html))), "Geçici klasör silinmeli");
        Check(c.Notes.Last() == ("Rapor v1 HTML olarak açıldı (PDF üretilemedi: Microsoft Edge bulunamadı). Tarayıcıdan 'PDF olarak kaydet' ile yazdırabilirsiniz.", StatusKind.Warning),
            $"Uyarı: {c.Notes.Last().Message}");
        Check(c.Audit.Entries.Any(e => e.EndsWith("|report.create|Rapor_v1")), "PDF'siz denetim satırı");
    });

    public static void VmReportWithoutSessionErrors() => With(c =>
    {
        Check(!c.Vm.CanCreateReport, "Oturum yokken rapor düğmesi kapalı");
        Wait(c.Vm.CreateReportAsync());
        Check(c.Notes.Last() == ("Önce bir oturum açın.", StatusKind.Error) && c.Pdf.Calls.Count == 0, "Oturumsuz rapor hata vermeli");
    });

    public static void VmSuggestionsListFoldersAndSkipUnderscore() => With(c =>
    {
        c.Open();                                            // PD170 / OP40 / SN001
        Directory.CreateDirectory(Path.Combine(c.Store.Root, "_config"));
        c.Store.OpenOrCreate(new Session { PartNumber = "PD171", OperationNumber = "OP10", SerialNumber = "A1", Inspector = "x", Project = "PD171" });
        c.Store.OpenOrCreate(new Session { PartNumber = "PD170", OperationNumber = "OP50", SerialNumber = "B2", Inspector = "x", Project = "PD170" });

        var parts = c.Vm.Suggestions(SuggestLevel.Part, "");
        Check(parts.SequenceEqual(new[] { "PD170", "PD171" }), $"Parçalar: {string.Join(",", parts)}");
        Check(c.Vm.Suggestions(SuggestLevel.Part, "171").SequenceEqual(new[] { "PD171" }), "İçerir araması");

        c.Vm.PartNumber = "PD170";
        Check(c.Vm.Suggestions(SuggestLevel.Operation, "").SequenceEqual(new[] { "OP40", "OP50" }), "Operasyonlar parçaya göre");

        c.Vm.OperationNumber = "OP50";
        Check(c.Vm.Suggestions(SuggestLevel.Serial, "").SequenceEqual(new[] { "B2" }), "Seriler operasyona göre");

        // Geçersiz ya da boş üst alan: öneri yok.
        c.Vm.PartNumber = "../x";
        Check(c.Vm.Suggestions(SuggestLevel.Operation, "").Count == 0, "Geçersiz parça için öneri yok");
        c.Vm.PartNumber = "";
        Check(c.Vm.Suggestions(SuggestLevel.Serial, "").Count == 0, "Boş parça için öneri yok");
    });
}
