# MVVM: Admin uygulaması

## Amaç
Admin exe'si (`src/MuayeneKayit.Admin/MainWindow.xaml.cs`, ≈685 satır code-behind) muayene ekranıyla aynı yapıya
(MVVM + portlar + DI) taşınır. Görünüm ve davranış **birebir aynı** kalır: metinler, renkler, PIN kuralları,
onay pencereleri (onay düğmesi koyu, vazgeç sade, `DefaultButton = None`), boşta kilit.

## Kurallar
- Faz 1 planındaki "Kurallar" geçerli (`docs/plans/2026-10-24-mvvm-muayene-ekrani.md`): dal `ui-yeniden-tasarim` değil,
  **`main`** üzerinde çalış (yeniden tasarım `main`'e alındı). Push yok. Türkçe metin ve yorum, mevcut stil.
- **Uygulamayı hiç başlatma** (ne Admin ne Inspector). Doğrulama yalnızca derleme + testler.
- Yeni NuGet yok: Admin projesi Presentation'a ve `Microsoft.Extensions.DependencyInjection 9.0.0`'a başvurur (Inspector'daki gibi).
- Mantığı birebir taşı.

## 1. Presentation: `src/MuayeneKayit.Presentation/Admin/`
Ortak portlar (`Services/` altına ya da Admin klasörüne):
- `IConfirmService { Task<bool> ConfirmAsync(string title, string message, string confirmText); }`
- `IClock { DateTime Now { get; } }` (+ `SystemClock`) — PIN kilitlenmesi ve boşta kilit test edilebilsin.
- `IAdminSettings { string LoadRootPath(); void SaveRootPath(string root); }` → Admin'deki `AppSettings` uygular.
- `IFileSaver { Task<string?> PickCsvPathAsync(string suggestedName); }` → `FileSavePicker`.
- Mevcut `IWindowService.OpenFile` / klasör açma için `IShell { void OpenFolder(string path); void OpenFile(string path); }`
  (Admin'de `explorer.exe` ve `Process.Start` kodları).

ViewModel'ler (`ObservableObject`, mevcut code-behind'dan birebir):
1. `AdminShellViewModel`: `Root`, `IsLoggedIn`, `Login` (aşağıdaki), `Lock(string? message)`, boşta kilit kontrolü
   `CheckIdle()` (son girişten 10 dk; `RegisterInput()` ile güncellenir; View `DispatcherTimer` ile `CheckIdle` çağırır),
   alt ViewModel'ler: `Records`, `Types`, `Texts`, `Maintenance`, `Settings`; `event Action<string, StatusKind>? Notify`.
2. `LoginViewModel`: `Pin`, `PinRepeat`, `IsFirstRun` (PIN yok), `Hint`, `ButtonText`, `Error`, `RootText`,
   `Login()` → 5 hatalı denemede 60 sn bekleme (`IClock`), ilk kurulumda PIN belirleme (eşleşme kontrolü),
   denetim satırları (`admin.pin.set`, `admin.login`, `admin.login.lockout`) — `AuditLog`'u `IAuditTrail` ile çağır.
3. `RecordsViewModel`: mevcut kayıt listesi + filtreler + özet kartları (Inspector'daki `DashboardViewModel` mantığına
   benziyorsa **yeniden kullan** ya da ortak sınıfa çıkar; görünüm Admin'deki gibi kalmalı), `ExportCsvAsync()`
   (`CsvExport`, `IFileSaver`, UTF-8 BOM), `OpenFolder(row)`, `OpenLatestReport(row)` (kök altı kontrolü aynı).
4. `TypesViewModel`: `Types` listesi, seçili tip, `Name`, `SpecName`, alan bayrakları (6 adet), `Apply()`
   (doğrulama `SharedConfig.ValidateTypes`), `Add()`, `DeleteAsync()` (onay), `MoveUp()/MoveDown()`,
   `ResetAsync()` (onay), `Save()`, `IsDirty`/`DirtyText`.
5. `DecisionTextsViewModel`: karar başına düzenlenebilir metin öğeleri (`Decision`, `Label`, `Text` TwoWay,
   `ResetOne()`), `Save()`, `ResetAllAsync()` (onay), `DirtyText`.
6. `MaintenanceViewModel`: `RebuildIndex()`, `VerifyAudit()`, `AuditLines` (son 300), `StaleLocks`, `SelectedLock`,
   `ScanLocks()`, `RemoveLockAsync()` (onay, mevcut metinler).
7. `SettingsViewModel`: `RootText`, `SaveRoot()` (klasör var mı, kaydet, oturumu kilitle — mevcut davranış),
   `OldPin/NewPin/NewPinRepeat`, `ChangePin()`.

## 2. Admin (View)
- `App.xaml.cs`: DI (`ServiceCollection`) — portların WinUI uygulamaları (`WinUiConfirmService` mevcut `ConfirmAsync`
  görünümüyle, `AdminSettingsAdapter`, `WinUiFileSaver`, `ShellService`, `SystemClock`, `IAuditTrail` kök değişince
  yeniden kurulacak şekilde — ör. `Func<string, IAuditTrail>` ya da kökü alan bir fabrika), ViewModel'ler, `MainWindow`.
- `MainWindow.xaml`: `x:Bind` (OneWay/TwoWay, TextBox'larda `UpdateSourceTrigger=PropertyChanged`, PasswordBox için
  `Password` TwoWay ya da code-behind'da `PasswordChanged` → VM). Kartlar (kayıt kartı, özet, rozet) mevcut görünümle
  çizilir (Inspector'daki `DashboardCards` örneği gibi veri → kart).
- `MainWindow.xaml.cs`: yalnızca bağlama, PIN kutusu Enter tuşu, boşta kilit zamanlayıcısı, gezinme ve kart çizimi.

## 3. Testler (`tests/MuayeneKayit.Tests`)
Sahte portlar (`FakeConfirm`, `FakeClock`, `FakeAdminSettings`, `FakeFileSaver`, `FakeShell`) + `TempRoot()`. En az:
1. İlk kurulum: PIN'ler eşleşmezse hata; eşleşirse PIN yazılır, giriş yapılır, denetim satırı.
2. Hatalı PIN 5 kez → 60 sn bekleme; saat ilerleyince tekrar denenebilir.
3. Boşta kilit: `RegisterInput` sonrası 10 dk geçince `CheckIdle` kilitler; daha erken kilitlemez.
4. Tipler: ekle → "Yeni tip 1", uygula geçersiz adla hata, yukarı/aşağı, sil (onay hayır → değişmez), kaydet → dosya.
5. Açıklamalar: düzenle + kaydet → `SharedConfig.LoadDecisionTexts` yeni metni döner; tümünü sıfırla onayla.
6. Bakım: eski kilit listelenir, onay hayır → silinmez, evet → silinir ve denetim satırı.
7. Kayıtlar: filtre ve CSV dışa aktarım (`FakeFileSaver` yolu) içerik başlığı ve satırlar.
8. Ayarlar: olmayan klasör → hata; PIN değiştir (eski yanlış → hata, yeni eşleşmezse hata, doğru → yeni PIN geçerli).

## 4. Doğrulama ve teslim
1. Tüm testler geçer (89 + yeni). Admin ve Inspector Debug x64 derlemeleri 0 hata, 0 uyarı.
2. Uygulama başlatılmaz.
3. Commit (`main`), Türkçe mesaj, yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`,
   son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
4. Rapor: dosyalar, `MainWindow.xaml.cs` satır sayısı önce/sonra, test sayısı, derleme, sapmalar, doğrulanamayanlar.
