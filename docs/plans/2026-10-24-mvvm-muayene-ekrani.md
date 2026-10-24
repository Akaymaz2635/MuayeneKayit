# MVVM + DI: Muayene ekranı (Faz 1)

## Amaç
Muayene ekranının durum ve kural mantığını `MainWindow.xaml.cs`'den (≈1940 satır, test edilemiyor) test edilebilir bir
ViewModel'e taşımak. **Görünüm ve davranış birebir aynı kalır** (metinler, renkler, sekme davranışı, düğme vurguları).
Faz 1'de diyaloglar (karar, rework sonrası, void, geri al, fotoğraf kaldır), kamera, fotoğraf düzenleyici, rapor ve
dashboard code-behind'da kalır; ViewModel'in verilerini kullanır. Faz 2 (sonra): diyaloglar `IDialogService` arkasına.

## Kurallar
- Repo: `C:\Users\PC\Documents\JUNK\MuayeneKayit`, dal `ui-yeniden-tasarim`. Push yok.
- Yorum dili Türkçe, mevcut kod stiline uy (file-scoped namespace, `_camelCase`, XML doc yorumları kısa).
- Ekrandaki Türkçe metinleri değiştirme; birebir taşı.
- NuGet sürümleri sabit: `CommunityToolkit.Mvvm` **8.4.0**, `Microsoft.Extensions.DependencyInjection` **9.0.0**.
  Restore: `dotnet build ... -p:RestoreSources=https://api.nuget.org/v3/index.json` (nuget.config yerel klasöre bakar).
- Admin projesine ve Core iş kurallarına dokunma (yalnızca aşağıdaki arayüzler eklenir).
- Derleme: `dotnet build src/MuayeneKayit.Inspector -c Debug -p:Platform=x64 -p:RestoreSources=https://api.nuget.org/v3/index.json`.
  Dosya kilidi hatası alırsan çalışan `MuayeneKayit.Inspector` sürecini durdur.
- Testler: `dotnet run --project tests/MuayeneKayit.Tests -p:RestoreSources=https://api.nuget.org/v3/index.json`
  (bağımlılıksız konsol koşucusu; yeni testleri `tests` listesine ekle, mevcut `Expect`, `TempRoot`, `SampleSession` yardımcılarını kullan).

## 1. Core: portlar (arayüzler)
`src/MuayeneKayit.Core/Storage/ISessionStore.cs`
- `SessionStore`'un public üyelerinden arayüz çıkar: `Root`, `FolderFor`, `OpenOrCreate`, `LoadSession`, `SaveSession`,
  `LoadDefects`, `AddDefect`, `UpdateDefect`, `UpdateDefects`, `ContinueAsChild`, `UndoLastDecision`.
- `public sealed class SessionStore : ISessionStore`. Başka değişiklik yok (AuditLog/IndexBuilder `SessionStore` almaya devam eder).

`src/MuayeneKayit.Core/Audit/IAuditTrail.cs`
- `public interface IAuditTrail { void Append(string actor, string action, string details); }`
- `public sealed class FileAuditTrail(SessionStore store) : IAuditTrail` → `AuditLog.Append(store, ...)`.

## 2. Yeni proje: `src/MuayeneKayit.Presentation`
- `net9.0` class library, `Nullable` ve `ImplicitUsings` açık, Core'a ve `CommunityToolkit.Mvvm 8.4.0`'a başvurur.
  WinUI'ye **başvurmaz** (test projesi doğrudan kullanabilsin). `MuayeneKayit.sln`'e ekle.
- `MuayeneKayit.Inspector` ve `tests/MuayeneKayit.Tests` bu projeye başvurur.

Dosyalar:
1. `Enums.cs`
   - `Tone { Critical, Caution, Attention, Success, Neutral }` (durum rengi; WinUI fırçası View'da seçilir)
   - `InspectionStep { Measure, Photo, Decision, History }`
   - `StatusKind { Info, Success, Warning, Error }`
2. `DefectPresentation.cs` (static) — `MainWindow.xaml.cs`'den **birebir taşı**:
   `StateLabel`, `NextStepFor`, `HasMeasurement`, `CurrentStep` (Measure/Photo/Decision ya da `null` = yapılacak iş yok),
   `SnapshotText`, `Fmt`, `ParseOptional`, `HistoryLines`, `DecisionHintFor`, `DecisionOrder`, `SourceFor`.
   Ek: `ToneFor(DefectState)` (eşleme `StateBrushes`'taki ile aynı: Closed→Success, MrbPending→Attention,
   Voided/Superseded→Neutral, AwaitingReinspect/WaitingVendor/Repairing→Caution, diğer→Critical),
   `TypeText(Defect)` → `"Dent + High metal"` ya da `"Dent"`.
3. `DefectItemViewModel.cs` — mevcut `Inspector/DefectItem.cs`'in yerine (brush yerine `Tone`):
   `Id, Number, Type, StateText, Tone, PhotoText, NextStep`, `ToString() => $"{Number} {Type}, {StateText}"`.
   `Inspector/DefectItem.cs` silinir.
4. `DecisionOption.cs` — `record DecisionOption(string Label, Decision? Decision, bool IsReinspect)`.
5. `InspectionViewModel.cs` : `ObservableObject` (`[ObservableProperty]` kullan)
   - ctor: `(ISessionStore store, IAuditTrail audit, IReadOnlyList<DefectTypeTemplate> types)`.
   - Oturum girişi (TwoWay): `PartNumber, OperationNumber, SerialNumber, InspectorName`.
   - Oturum: `IsSessionOpen`, `Folder`, `Session`, `User`, `SessionTitle`, `SessionSubtitle`, `DefectCountText`,
     `IsCompletePrimary`, `IsAddDefectPrimary` (UpdateSessionHeader ile aynı kural).
   - Liste: `ObservableCollection<DefectItemViewModel> Defects`, `SelectedDefect`, `IsListEmpty`.
   - Seçili kusur: `Current (Defect?)`, `SelectedId`, `HasSelection`, `Header`, `StateText`, `StateTone`, `NextStepText`,
     `ShowNextStep`, `Sentence`, `DeltaText`, `ShowDelta`, `IsAwaiting`, `AwaitTitle`, `AwaitText`,
     ölçüm metinleri `DepthText, WidthText, LengthText, RadiusText, AngleText, ColorText, NotesText` (TwoWay),
     `HighMetal` (TwoWay), alan görünürlükleri `ShowDepth…ShowColor` (ApplyTemplateFields kuralı), `IsEditable`,
     `CanVoid`, `History (IReadOnlyList<string>)`,
     sekme başlıkları `MeasureTabLabel, PhotoTabLabel, DecisionTabLabel, HistoryTabLabel` (✓ kuralı ApplyStepGuidance ile aynı),
     `CurrentStep (InspectionStep?)`, `IsSavePrimary`, `IsCameraPrimary`,
     `SelectedStep (InspectionStep)` — **sekme seçimi buradadır**: başka kusur seçilince `CurrentStep ?? Measure`;
     aynı kusur yenilenince değişmez; ölçüm kaydı sonrası fotoğraf yoksa ve ölçüm varsa `Photo`.
     `DecisionOptions (IReadOnlyList<DecisionOption>)` (UpdateDecisionButtons kuralı; bekleyen rework için başta
     "Rework sonrası muayene sonucu…"), `DecisionHint`, `CanUndo`, `UndoText`.
   - Olay: `event Action<string, StatusKind>? Notify`.
   - Metotlar (mevcut code-behind mantığıyla aynı; hata mesajları aynı):
     `OpenSession()`, `ChangeSession()`, `AddDefect(string type)`, `Select(int? id)`, `Refresh(int? selectId)`,
     `SaveMeasurements()`, `Complete()`.
     Beklenen hatalar (`ArgumentException, InvalidOperationException, IOException, TimeoutException,
     InvalidDataException, UnauthorizedAccessException`) yakalanır ve `Notify(..., Error)` ile bildirilir.
   - `Refresh` sırasında `SelectedDefect` değişimi seçim mantığını tetiklememeli (eski `_refreshing` korumasının karşılığı).

## 3. Inspector (View)
- `App.xaml.cs`: `ServiceCollection` kur: `SessionStore` (kök = `AppSettings.LoadRootPath()`) hem kendisi hem `ISessionStore`,
  `IAuditTrail → FileAuditTrail`, şablonlar (`SharedConfig.LoadDefectTypes(root)`), `InspectionViewModel`, `MainWindow`.
  `MainWindow` ctor'u `InspectionViewModel vm` alır; `public InspectionViewModel Vm { get; }`.
- `MainWindow.xaml` muayene paneli: metin/görünürlük/IsEnabled/IsReadOnly için `{x:Bind Vm.X, Mode=OneWay}`;
  giriş kutuları `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`; `HighMetalBox.IsChecked` TwoWay.
  `DefectList` → `ItemsSource="{x:Bind Vm.Defects}"`, `SelectedItem` TwoWay; şablon `x:DataType="pres:DefectItemViewModel"`.
  Durum renkleri: MainWindow'da statik fonksiyonlar `ToneBackground(Tone)` / `ToneForeground(Tone)`
  (`SystemFillColor{X}BackgroundBrush` / `SystemFillColor{X}Brush`) ve `PrimaryStyle(bool)` (Accent/Default) — x:Bind fonksiyon bağlama.
  `DecisionCombo` öğeleri `Vm.DecisionOptions`'tan; seçimde mevcut diyalog akışları çalışır.
- `MainWindow.xaml.cs`: muayene paneline ait durum alanları (`_store, _folder, _session, _user, _selectedId, _current,
  _shownId, _refreshing`) ve taşınan metotlar kaldırılır. Kalan kod (diyaloglar, kamera, düzenleyici, fotoğraf kartları,
  rapor, dashboard) `Vm.Store/Vm.Folder/Vm.Session/Vm.User/Vm.Current/Vm.SelectedId` kullanır; değişiklikten sonra
  `Vm.Refresh(id)` çağırır. `Vm.Notify` → mevcut `Show(...)` (StatusKind → InfoBarSeverity).
  `Vm.PropertyChanged(SelectedStep)` → `ShowTab(...)`; `DetailTabs_SelectionChanged` → `Vm.SelectedStep` yazar.
  Fotoğraf kartları `Vm.Current` değişince yeniden çizilir (`RenderPhotos`).
- Davranış birebir aynı kalmalı. Görsel fark olmamalı.

## 4. Testler (ViewModel) — `tests/MuayeneKayit.Tests/Program.cs`
Gerçek `SessionStore` + `TempRoot()`, sahte `IAuditTrail` (listeye yazan). En az:
1. Yeni oturum aç → `IsSessionOpen`, `SessionTitle`, `IsAddDefectPrimary` true (liste boş).
2. Kusur ekle → seçilir, `SelectedStep == Measure`, `IsSavePrimary`.
3. Ölçüm kaydet (derinlik) → `SelectedStep == Photo`, `MeasureTabLabel` "✓ 1. Ölçüm", `IsCameraPrimary`.
4. Geçersiz ölçüm ("abc") → `Notify(Error)`, dosya değişmez.
5. High metal işaretle + kaydet → `Defects[0].Type == "Dent + High metal"`, `Sentence` "with high metal" içerir.
6. **Regresyon:** oturum açıldıktan sonra ilk seçilen, kararı bekleyen kusurda `SelectedStep == Decision`.
7. **Regresyon:** aynı kusur `Refresh` edilince `SelectedStep` korunur (kullanıcı Photo sekmesindeyken).
8. Açık kusurda `DecisionTabLabel` ✓ içermez; karar sonrası (ör. SubmitMrb) içerir.
9. `Complete()` açık kusur varken uyarı verir, oturum Completed olmaz; kusur kapatılınca olur, `IsCompletePrimary` kuralı.
10. `DecisionOptions`: açık kusur → Kabul/Rework/…; rework bekleyen → ilk öğe `IsReinspect`.
11. `CanUndo`/`UndoText` karar sonrası dolu.
12. `ChangeSession()` → `IsSessionOpen` false, liste boş.

## 5. Doğrulama
1. Tüm testler geçer (mevcut 48 + yeni).
2. Inspector Debug x64 derlemesi 0 hata.
3. Ekran kontrolü (winapp CLI, telemetri kapalı: `$env:WINAPP_CLI_TELEMETRY_OPTOUT="1"`):
   `C:\Users\PC\AppData\Local\Temp\claude\C--Users-PC-Documents-JUNK-GUI-PYTHON-main\b4602378-972e-45da-8ba3-71896c66e7e3\scratchpad\ui2.ps1 -Select "#9" -Prefix mv`
   ile TS1400/OP60/SN-B02 açılır ve `#9` seçilir; ekran görüntüsünde Disposition sekmesi seçili ve kalın olmalı,
   "✓ 1. Ölçüm ✓ 2. Fotoğraflar" görünmeli. Kullanıcı verisini **değiştirme** (kaydetme yapma).
4. Commit (dal `ui-yeniden-tasarim`), mesaj Türkçe, sonuna:
   `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`; yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`.
5. Rapor: değişen dosyalar, test sayısı, derleme sonucu, ekran kontrolü sonucu, çözülemeyen nokta varsa açıkça.
