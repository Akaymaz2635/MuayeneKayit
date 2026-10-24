# MVVM Faz 3: Fotoğraflar, kamera/düzenleyici açma, rapor, öneriler, Dashboard

## Amaç
Faz 1–2 sonrası `MainWindow.xaml.cs` (≈890 satır) içinde kalan iş mantığını test edilebilir hale getirmek. Ekranda görünen
her şey (metinler, kart düzeni, renkler, sekme davranışı) **birebir aynı** kalır. WinUI'ye özgü çizim (küçük resim yükleme,
pencere oluşturma, FileOpenPicker, kart Border'ları) View'da kalır; kararlar ve veri ViewModel'e taşınır.

## Kurallar
- Faz 1 planındaki "Kurallar" bölümü geçerli (`docs/plans/2026-10-24-mvvm-muayene-ekrani.md`): dal, stil, Türkçe metin,
  derleme/test komutları, restore bayrağı, commit yazarı/trailer'ı.
- **Uygulamayı hiç başlatma** (kullanıcı ekranda pencere açılmasını istemiyor). Doğrulama yalnızca derleme + testler.
- Yeni NuGet yok. Mantığı birebir taşı; yeniden yazma.

## 1. Fotoğraflar (Presentation)
- `PhotoItemViewModel` (veri): `FileName`, `Label` ("6.1"), `DisplayFile` (temiz kopya varsa o, yoksa orijinal; dosya
  yoksa orijinal), `InfoText` ("İşaret yok" / "2 işaret · arka plan kaldırıldı"), `HasMarks`, `IsEditable`,
  `EditButtonText` ("İşaretle" / "Görüntüle"). Kaynak: `MainWindow.RenderPhotos`.
- `InspectionViewModel.Photos : ObservableCollection<PhotoItemViewModel>`, `HasPhotos`; `LoadSelected`/`ClearSelection`
  ile güncellenir. Dosya varlık kontrolü için `IFileSystem` gerekmez: `File.Exists` Presentation'da kullanılabilir.
- View: `PhotoPanel` `ItemsRepeater` ya da mevcut `VariableSizedWrapGrid` ile `Vm.Photos`'tan çizilir (kart görünümü
  birebir; küçük resim yükleme `LoadThumbnailAsync` View'da kalır). `RenderPhotos` silinir.

## 2. Fotoğraf ekleme ve pencere açma (portlar)
`src/MuayeneKayit.Presentation/Services/`:
- `IFilePicker { Task<string?> PickImageAsync(); }` → Inspector `WinUiFilePicker` (mevcut FileOpenPicker kodu).
- `IWindowService`:
  - `void OpenCamera(CameraRequest request, Func<CapturedPhotoData, string> onSave, Action<string?> onClosed)`
    (`CameraRequest(string Target, int DefectId)`, `CapturedPhotoData(string TempPath, int Width, int Height, bool Framed, double FrameFraction)`)
  - `void OpenPhotoEditor(PhotoEditorRequest request, Action<PhotoEditResultData> onSave)`
    (`PhotoEditorRequest(string Title, string DefectsDir, string Photo, PhotoMarkup? Existing, IReadOnlyList<int> Labels, int CurrentId)`,
     `PhotoEditResultData(PhotoMarkup Markup, byte[]? NewCleanJpeg)`)
  - `void OpenFile(string path)` (rapor / görüntüle için `Process.Start` UseShellExecute)
  → Inspector `WinUiWindowService` (mevcut `CameraWindow`/`PhotoEditorWindow` açma kodu, tek pencere kuralı, ana pencere
  kapanınca kapatma). Inspector'daki `CapturedPhoto`/`PhotoEditResult` kayıtları bu veri tiplerine eşlenir ya da değiştirilir.
- `InspectionViewModel` (yeni partial dosya `InspectionViewModel.Photos.cs`), mevcut code-behind'dan birebir:
  - `Task AddPhotoFromFileAsync()` — `IFilePicker` → `PhotoImporter.Import` → `Photos.Add` → denetim → `Refresh` → bildirim.
  - `void OpenCamera()` — hedef metni, kamera kaydı: import + çerçeve işareti (`Annotation.CenterSquare`) + denetim;
    kapanınca kayıt varsa `Refresh`, `SelectedStep = Photo`, düzenleyiciyi aç.
  - `void OpenEditor(string photo)` — kapalı kusurda `OpenFile` (temiz kopya ya da orijinal), aksi halde düzenleyici;
    kaydet: temiz kopya yaz, doğrula, `Markups` güncelle, denetim, `Refresh`, bildirim.
- DI: `IFilePicker`, `IWindowService` kayıtları. `MainWindow` handler'ları tek satır.

## 3. Rapor (port)
- Core: `IPdfConverter { Task<string?> TryConvertAsync(string htmlPath, string pdfPath); }`; Inspector'daki `ReportPdf`
  bunu uygular (`EdgePdfConverter`).
- `InspectionViewModel.CreateReportAsync()` — `ReportFiles.LatestVersion` + 1, `ReportBuilder.Build`, `AtomicFile`,
  PDF dönüştürme, denetim, `OpenFile` (PDF ya da HTML), PDF başarılıysa bildirim yok (durum çubuğu kapanır →
  `StatusKind`'a `Clear` ekle ya da `Notify` yerine ayrı `ClearStatus` olayı), başarısızsa uyarı metni aynı.
  `IsReportRunning` (düğmeyi kilitlemek için).

## 4. Öneriler (parça / operasyon / seri)
- `InspectionViewModel.Suggestions(SuggestLevel level, string text)` → `IReadOnlyList<string>` (`MainWindow.SuggestionsFor`
  mantığı: kök/parça/operasyon klasörleri, `_` ile başlayanlar hariç, `SafePath.ValidateSegment`, en fazla 50, içerir araması).
  View yalnızca listeyi `AutoSuggestBox.ItemsSource`'a verir.

## 5. Dashboard
- `DashboardViewModel` (Presentation): filtre metinleri (`Part, Operation, Serial`), `TypeFilter` (Tümü + tipler),
  `StateFilter` (`IndexStateFilter`, etiketler `StateFilters`'tan), `Rows` (filtrelenmiş), özet sayıları
  (`MainWindow.RenderSummary` ile aynı kartlar: başlık/değer/ton), sonuç kartı verisi `DashboardRowViewModel`
  (başlık, alt metin, rozetler: metin + `Tone`, açık kusur vurgusu), `Refresh()` (`IndexBuilder.Rebuild` — `SessionStore` gerekir;
  `ISessionStore`'a `Root` var, `new SessionStore(Root)` ya da `IIndexService` portu), `ClearFilters()`, `VerifyAudit()`
  (sonuç metni + ton), `event Action<IndexRow>? OpenRequested`.
- View: kartlar `DashboardRowViewModel`'den çizilir (görünüm birebir, `StateColors` → `Tone` eşlemesi `ToneBackground/Foreground`);
  `OpenFromDashboard` → `Vm.PartNumber/OperationNumber/SerialNumber` doldurur, muayene sekmesine geçer, muayeneci adı varsa açar
  (mevcut davranış).
- DI: `DashboardViewModel`; `MainWindow` ctor'a eklenir.

## 6. Testler
Sahte `IFilePicker`, `IWindowService` (çağrıları kaydeder, kaydet geri çağrımlarını testten tetikler), `IPdfConverter`.
En az:
1. `Photos` etiketleri 6.1/6.2, temiz kopya varsa `DisplayFile`, `InfoText`.
2. Dosyadan ekle: seçim null → değişmez; dosya → `Photos` +1, denetim.
3. Kamera: çerçeveli kayıt → işaret `CenterSquare(fraction)`; kapanınca `SelectedStep == Photo` ve düzenleyici açılır.
4. Düzenleyici kaydı: işaret yoksa ve temiz kopya yoksa `Markups`'tan çıkar; temiz kopya yazılır.
5. Kapalı kusurda `OpenEditor` → `OpenFile` çağrılır, düzenleyici açılmaz.
6. Rapor: sürüm artar, HTML yazılır, PDF başarılı → `OpenFile(pdf)`; başarısız → uyarı + `OpenFile(html)`.
7. Öneriler: `_config` gibi `_` klasörleri gelmez; operasyon önerisi parçaya göre; geçersiz parça → boş.
8. Dashboard: filtreler (parça, tip, durum), özet sayıları, `ClearFilters`, `VerifyAudit` sağlam/bozuk.

## 7. Doğrulama ve teslim
1. Tüm testler geçer (71 + yeni). Inspector Debug x64 0 hata, 0 uyarı.
2. `MainWindow.xaml.cs` satır sayısını raporla (hedef: yalnızca görünüm bağlama, sekme ve kart çizimi).
3. Uygulama başlatılmaz.
4. Commit (dal `ui-yeniden-tasarim`), Türkçe mesaj, yazar ve trailer Faz 1 planındaki gibi.
5. Rapor: değişen dosyalar, satır sayıları, test sayısı, derleme, sapmalar, doğrulanamayanlar.
