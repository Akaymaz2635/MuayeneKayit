# Inspector: Ayarlar sekmesi (ölçü birimi mm/inch, açık/koyu tema)

## Amaç
Muayene ekranına (Inspector) üçüncü bir gezinme öğesi **"Ayarlar"** eklenir. Kullanıcı burada iki şeyi seçer:
1. **Ölçü birimi:** `mm` ya da `inch`.
2. **Tema:** `Açık` ya da `Koyu`.

Kararlar (kullanıcıyla netleşti):
- Ayar **bu bilgisayara** özeldir: exe'nin yanındaki `MuayeneKayit.settings.json` dosyasında saklanır (ortak klasöre yazılmaz).
- Ölçümler **girildiği birimle** kaydedilir; dönüştürme yapılmaz. Eski kayıtlar `mm` kalır.
  Birim kusur başına saklanır: kusur oluşturulurken o anki ayar kusura yazılır. Ayar değişince yalnızca **yeni** kusurlar etkilenir.
- Inch biçimi: 1'den küçük değerler baştaki sıfır olmadan yazılır: `0.123` → `.123`, `-0.5` → `-.5`, `0` → `0`, `1.25` → `1.25`.
  Inch en fazla 4 ondalık (`0.####`), mm mevcut gibi 2 ondalık (`0.##`).
- Açı (°) birimden etkilenmez.

## Kurallar
- **`main`** dalında çalış. Push yok. Türkçe metin ve yorum, mevcut stil (yorum yoğunluğu, adlandırma).
- **Uygulamayı başlatma.** Doğrulama: derleme + testler.
- Yeni NuGet paketi yok.
- Admin uygulaması değişmez (Admin açık temada kalır), yalnızca Core'daki ortak değişiklikler derlenmeli.

## 1. Core (`src/MuayeneKayit.Core`)
1. `Domain/Enums.cs`: `public enum LengthUnit { Mm, Inch }`.
2. `Domain/Models.cs`: `Defect.Unit` (`LengthUnit`, varsayılan `Mm`). JSON `JsonStringEnumConverter` ile `"Mm"`/`"Inch"` yazılır;
   alanı olmayan eski dosyalar `Mm` okunur. `MeasurementSnapshot` değişmez (anlık görüntü kusurun birimini kullanır).
3. Yeni `Domain/Units.cs` (statik):
   - `string Format(double value, LengthUnit unit)` — mm: `0.##`; inch: `0.####` ve baştaki `0.` → `.` (negatifte `-0.` → `-.`). InvariantCulture.
   - `string Suffix(LengthUnit unit)` → `"mm"` / `"in"`.
   - `string Header(string label, LengthUnit unit)` → `"Derinlik (mm)"` / `"Derinlik (in)"`.
4. `Storage/SessionStore.cs`:
   - Yeni kusur (`line ~83`, `new Defect { Id = nextId, DefectType = defectType }`): birim parametresi alınır
     (`AddDefect(..., LengthUnit unit = LengthUnit.Mm)` gibi isteğe bağlı son parametre; mevcut çağrılar derlenmeye devam eder).
     `ISessionStore` arayüzü de aynı şekilde güncellenir, sahte (`Fake*`) depolar da.
   - Bölünen/alt kusur (`line ~147`, `childType`): **ana kusurun birimini** kopyalar.
5. `Report/DefectSentence.cs`: `AddMm` → birimli (`Depth: .123 in` / `Depth: 12 mm`). `Num` açı için aynı kalır.
6. `Report/MeasurementCompare.cs`: `"mm"` sabitleri yerine kusurun birimi; sayılar `Units.Format`. İmza gerekirse
   `Compare(before, after, LengthUnit unit = LengthUnit.Mm)`; çağıranlar kusurun birimini geçer.
7. `Report/ReportBuilder.cs`: `Mm(d.Depth)` → kusurun birimiyle; `SnapshotText` (geçmiş ve düzeltme tabloları) D/W/L/R değerleri
   `Units.Format(…, d.Unit)` ile. SVG/sayfa koordinatlarındaki `Num` dokunulmaz.

## 2. Presentation (`src/MuayeneKayit.Presentation`)
1. Yeni port `Services/IUserSettings.cs`:
   ```csharp
   public interface IUserSettings
   {
       LengthUnit Unit { get; set; }   // set → dosyaya yazar
       AppTheme Theme { get; set; }
   }
   public enum AppTheme { Light, Dark }
   ```
   Testlerde `FakeUserSettings` (bellek içi).
2. `DefectPresentation`: `Fmt(double?)` mevcut çağrılar için kalır (açı vb.); yeni `Fmt(double?, LengthUnit)` ve
   `SnapshotText(MeasurementSnapshot, LengthUnit)`. Ölçüm ile ilgili tüm çağrılar kusurun birimini geçer
   (`InspectionViewModel.cs:412-420`, geçmiş satırları `:131`, `:136`).
3. `InspectionViewModel`:
   - `IUserSettings` enjekte edilir. Yeni kusur eklenirken `_settings.Unit` depoya geçer.
   - Seçili kusurun birimine göre başlıklar: `DepthHeader`, `WidthHeader`, `LengthHeader`, `RadiusHeader` (`[ObservableProperty]`,
     ör. `"Derinlik (in)"`), seçim değişince güncellenir. Kusur yokken ayardaki birim.
   - Kutulara değer yazarken (`DepthText = …`) inch biçimi (`.123`). Ayrıştırma (`DefectPresentation` `TryParse`, `:111`) `.123`'ü zaten kabul
     ediyor; bunun testini ekle.
   - Diyaloglara giden isteklerde (ölçüm düzeltme / rework sonrası yeniden muayene) birim taşınır (istek modeline `LengthUnit Unit` ekle).
4. Yeni `SettingsViewModel` (Inspector için, `Presentation` kökünde, Admin'deki aynı adlı sınıfla çakışmaması için
   **`UserSettingsViewModel`** adını kullan):
   - `bool IsInch` / `bool IsDark` (TwoWay için) ya da `int UnitIndex` / `int ThemeIndex`; değişince `IUserSettings`'e yazar.
   - `event Action<AppTheme>? ThemeChanged` (View temayı uygular).
   - Açıklama metni: "Birim yalnızca yeni kusurlara uygulanır; mevcut kayıtlar girildiği birimle kalır."

## 3. Inspector (View)
1. `UserSettingsAdapter : IUserSettings` — `AppSettings` dosyasını (`MuayeneKayit.settings.json`, exe yanında) okur/yazar.
   **`rootPath` ve diğer mevcut alanlar korunmalı** (dosyayı `JsonNode` ile oku, yalnızca `unit` / `theme` alanlarını değiştir, geri yaz).
   Yazma hatası (salt okunur klasör vb.) uygulamayı düşürmez; bellekteki değer geçerli kalır.
   DI: `services.AddSingleton<IUserSettings, UserSettingsAdapter>(); services.AddSingleton<UserSettingsViewModel>();`
2. `MainWindow.xaml`:
   - Gezinmeye üçüncü öğe: `NavigationViewItem Content="Ayarlar" Tag="settings"` (ikon `Setting`), `AutomationId="SettingsNavItem"`.
   - Yeni panel `SettingsPanel` (diğer paneller gibi `Visibility` ile), `CardStyle` kart içinde:
     - "Ölçü birimi": `RadioButtons` (mm / inch), `AutomationId="UnitRadio"`.
     - "Tema": `RadioButtons` (Açık / Koyu), `AutomationId="ThemeRadio"`.
     - Açıklama metni (ikincil yazı rengi).
   - Ölçüm kutularının `Header`'ları `{x:Bind Vm.DepthHeader, Mode=OneWay}` vb.
3. **Tema:**
   - `App.xaml` ve `MainWindow.xaml`'daki `RequestedTheme="Light"` kaldırılır; tema kök öğeye (`NavigationView`) kod ile uygulanır:
     `Nav.RequestedTheme = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;` (açılışta ayardan, değişince hemen).
   - Başlık çubuğu: `AppWindow.TitleBar.PreferredTheme` (WindowsAppSDK 1.8'de var: `TitleBarTheme.Light/Dark`).
   - **Sabit renkler** koyu temada okunabilir olmalı:
     - `MainWindow.xaml:18-20` NavigationView arka plan `#FFFFFF` fırçaları → kaldır ya da `ThemeResource` kullan
       (ör. `{ThemeResource SolidBackgroundFillColorBaseBrush}` / `LayerFillColorDefaultBrush`).
     - `MainWindow.xaml:333-336` Dashboard başlık kartı `White`, `#DDE2EA`, `#1A3A5C` → `CardStyle` ve tema fırçaları
       (başlık rengi için `App.xaml`'da `ThemeDictionaries` ile `BrandTextBrush`: Light `#1A3A5C`, Dark `#9CB8DA`).
     - `DashboardCards.cs`: `#FFFFFF`/`#DDE2EA`/`#666666`/`#888888`/`#1A3A5C` → `Application.Current.Resources` içindeki tema fırçaları
       (`CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `TextFillColorSecondaryBrush`, `TextFillColorTertiaryBrush`,
       `BrandTextBrush`). Kartlar çizilirken temanın uygulanmış olması için fırçayı kök öğenin kaynaklarından çöz ya da
       `ThemeResource` davranışı için kartları XAML'deki bir `Style` ile kur — hangisi basitse; tema değişince Dashboard yeniden çizilsin.
     - Özet kartı sayı renkleri (`DashboardViewModel.cs:191-195`, `#1A3A5C` vb.): koyu temada lacivert okunmaz. Lacivert (`#1A3A5C`)
       değeri View'de `BrandTextBrush`'a eşlensin; diğer renkler (turuncu, mor, mavi, yeşil) koyu zeminde yeterince okunur, kalabilir.
     - Durum çipleri (açık zemin + koyu yazı) kendi içinde kontrastlı, değişmez.
     - `Dialogs/WinUiDialogService.cs` içindeki sabit renkleri kontrol et; zemin/yazı rengi sabitse tema fırçasına çevir.
       Diyaloglar (`ContentDialog`) `XamlRoot` üzerinden açıldığı için kök öğenin temasını alır; gerekirse `dialog.RequestedTheme = Nav.ActualTheme`.
     - `PhotoEditorWindow` ve `CameraWindow` kapsam dışı (kendi sabit temaları var: düzenleyici açık, kamera koyu). Dokunma.
4. `Dialogs/WinUiDialogService.cs:187-199`: `"Derinlik (mm)"` vb. başlıklar ve `önce: …` yer tutucuları kusurun birimiyle
   (`Units.Header`, `Units.Format`). `:165` `SnapshotText(before)` birimli.

## 4. Testler (`tests/MuayeneKayit.Tests`, `Program.cs`'e kaydet)
1. `Units.Format`: inch `0.123`→`.123`, `0.5`→`.5`, `-0.25`→`-.25`, `0`→`0`, `1.25`→`1.25`, `0.12345`→`.1235`; mm `0.123`→`0.12`, `12`→`12`.
2. `DefectSentence`: inch kusur → `There is Dent on part with Depth: .123 in.`; mm değişmez.
3. JSON: `unit` alanı olmayan eski kusur `Mm` okunur; `Inch` yazılıp geri okunur.
4. `MeasurementCompare` inch: `Depth: .123 → .1 in (azaldı)` biçimi (mevcut yön metniyle).
5. ViewModel: `FakeUserSettings.Unit = Inch` iken kusur ekle → kusur `Inch`, `DepthHeader == "Derinlik (in)"`; değer `0.123` kaydedilince
   `DepthText == ".123"`; ayar `Mm`'ye çevrilince mevcut kusur `Inch` kalır, yeni kusur `Mm`.
6. `.123` ve `,123` (tr-TR) girişleri ayrıştırılır.
7. Bölünen alt kusur ana kusurun birimini alır.
8. `UserSettingsViewModel`: tema değişince `ThemeChanged` olayı ve `IUserSettings.Theme` güncellenir.
9. Rapor HTML'i inch kusurda `.123 in` içerir.

## 5. Doğrulama ve teslim
1. Tüm testler geçer (106 + yeni). Inspector ve Admin Debug x64 derlemeleri 0 hata, 0 uyarı
   (`dotnet build src\MuayeneKayit.Inspector -c Debug -p:Platform=x64`; Admin aynı).
2. **Uygulama başlatılmaz.**
3. `PLAN.md` ve `KURULUM.md`'ye kısa not (Ayarlar sekmesi, settings.json'daki `unit`/`theme` alanları).
4. Commit (`main`), Türkçe mesaj, yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`,
   mesajı dosyadan ver (`git commit -F`; dosyayı BOM'suz UTF-8 yaz), son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
5. Rapor: değişen dosyalar, test sayısı, derleme sonucu, plandan sapmalar, doğrulanamayanlar (ekranda görülmeyenler).
