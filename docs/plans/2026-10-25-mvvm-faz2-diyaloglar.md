# MVVM Faz 2: Diyaloglar `IDialogService` arkasına

## Amaç
Faz 1'de muayene ekranının durumu `InspectionViewModel`'e taşındı. Karar akışları hâlâ `MainWindow.xaml.cs`'de, ContentDialog
koduyla iç içe ve test edilemiyor. Faz 2: **akış ve kurallar ViewModel'e**, **pencere çizimi `IDialogService` arkasına**.
Ekranda görünen her şey (metinler, genişlik 480, onay düğmesi vurgulu, `DefaultButton = None`, zorunlu alan kuralları,
canlı değişim satırı) **birebir aynı kalır**.

Kapsam: karar penceresi (+ "Rework şimdi yapıldı mı?"), rework sonrası muayene, son kararı geri al, void, fotoğraf kaldır.
Kapsam dışı (Faz 3): kamera, fotoğraf düzenleyici, dosyadan ekle, rapor, dashboard.

## Kurallar
- Repo `C:\Users\PC\Documents\JUNK\MuayeneKayit`, dal `ui-yeniden-tasarim`, push yok.
- Türkçe metinleri ve yorum dilini koru; mevcut stil (Faz 1'deki Presentation kodu örnek).
- Yeni NuGet yok. Derleme/test komutları Faz 1 planındaki gibi
  (`docs/plans/2026-10-24-mvvm-muayene-ekrani.md`, "Kurallar").
- **Uygulamayı açma kuralı:** derleme/test düzeltirken uygulamayı başlatma. Ekran kontrolü en sonda **bir kez**
  (aşağıda), bitince süreci kapat. Kullanıcı verisini (TS1400 vb.) değiştirme.
- Mantığı **birebir taşı**; yeniden yazma. Kaynaklar: `MainWindow.xaml.cs` içindeki `ShowDecisionDialog`,
  `DecisionCombo_SelectionChanged`, `Undo_Click`, `VoidButton_Click`, `RemovePhoto_Click`, `ShowReinspectDialog`,
  `ReinspectButton_Click`, `DecisionEffect`.

## 1. Presentation: diyalog sözleşmeleri (WinUI'siz)
`src/MuayeneKayit.Presentation/Dialogs/` altında:

1. `IDialogService`
   ```csharp
   Task<DecisionInput?> AskDecisionAsync(DecisionRequest request);      // null = vazgeç
   Task<bool> AskReinspectNowAsync(Decision decision, int defectId);     // "Rework şimdi yapıldı mı?"
   Task<ReinspectInput?> AskReinspectAsync(ReinspectRequest request);
   Task<string?> AskUndoReasonAsync(UndoRequest request);                // gerekçe ya da null
   Task<string?> AskVoidReasonAsync(int defectId, string defectType);
   Task<bool> ConfirmRemovePhotoAsync(int defectId, string photo);
   ```
2. İstek / sonuç kayıtları (yalnızca veri):
   - `DecisionRequest(int DefectId, string DefectType, string StateText, Decision Decision, string Label, string EffectText,
     string? TargetHeader, string? TargetDefault, bool NeedsReference, bool NoteRequired, bool NeedsReworkScope)`
   - `DecisionInput(string? Target, string? Reference, string? Note, ReworkScope? Scope)`
   - `ReinspectRequest(int DefectId, string DefectType, bool IsRework, bool HighMetalOnlyScope, string? TargetOperation,
     MeasurementSnapshot Before, IReadOnlyList<MeasureField> Fields, bool ShowColor, IReadOnlyList<string> TypeNames)`
     (`Fields`: şablon + eski değeri olan alanlar — mevcut kuraldaki gibi)
   - `ReinspectOutcome { Conforms, Same, New }`
   - `ReinspectInput(ReinspectOutcome Outcome, MeasurementSnapshot Entered, bool? HighMetalStillThere, string? ChildType, string? Note)`
     (`Entered`: kutulara yazılan ham değerler, boşlar null)
   - `UndoRequest(int DefectId, string DefectType, string DecisionLabel, DateTime DecidedAt, string DecidedBy, int? ChildToVoid)`
3. `ReinspectForm` (static, test edilebilir; pencere canlı güncelleme için bunu çağırır):
   - `MeasurementSnapshot Merge(MeasurementSnapshot before, MeasurementSnapshot entered, bool? highMetalStillThere)`
     (boş alan önceki değerde kalır; high metal cevabı yoksa öncekini korur)
   - `bool AnyValue(MeasurementSnapshot entered)`
   - `bool CanSave(ReinspectOutcome? outcome, bool hadHighMetal, bool? highMetalStillThere, bool anyValue, string? childType)`
     (conforms → true; same → high metal varsa cevap zorunlu, yoksa en az bir değer; new → tip seçili)
   - `string DeltaText(MeasurementSnapshot before, MeasurementSnapshot merged, bool anyValue, bool highMetalAnswered)`
     (mevcut "Değişimi görmek için ölçüm girin." kuralı dahil)
4. `DecisionForm.CanSave(bool noteRequired, string? note, bool needsScope, ReworkScope? scope)`.

## 2. InspectionViewModel: akışlar
ctor'a `IDialogService dialogs` ekle (DI). Yeni async metotlar — içerik mevcut code-behind'ın birebir karşılığı,
hata yakalama `Run` gibi (async sürümü) ve bildirim `Notify` ile:
- `Task GiveDecisionAsync(DecisionOption option)`:
  - `IsReinspect` → `ReinspectAsync()`.
  - Aksi halde `DecisionRequest` kur (hedef operasyon varsayılanı = oturum operasyonu; Rework/Reinspect başlıkları mevcut
    metinler; referans gereken kararlar mevcut liste; Reopen not zorunlu; Rework + `HighMetal` → kapsam zorunlu;
    `EffectText = SharedConfig.LoadDecisionTexts(Store.Root)`), sonucu `DefectStateMachine.Apply` ile uygula
    (boş hedef → oturum operasyonu), denetim kaydı, `Refresh`, bildirim; Rework/Reinspect ise
    `AskReinspectNowAsync` → evet ise `ReinspectAsync()`.
- `Task ReinspectAsync()`: durum kontrolü, `ReinspectRequest` kur, sonuca göre conforms / same (Merge + doğrulama +
  `MeasurementEditor.Apply` + `AfterReworkSameDefect`, high metal giderildiyse otomatik not, mesaj farkı) / new
  (`ContinueAsChild`). Mesajlar mevcutla aynı.
- `Task UndoLastAsync()`: `LastUndoable`, child bilgisi (`AfterReworkNewDefect` + `SupersededById`), gerekçe → `UndoLastDecision`.
- `Task VoidAsync()`: gerekçe → `Apply(Void, Operator)`.
- `Task RemovePhotoAsync(string photo)`: onay → `Photos`/`Markups`'tan çıkar, denetim, `Refresh`.
- `DecisionEffect` ve `SourceFor` gibi yardımcılar `DefectPresentation`'da kalır/taşınır.

## 3. Inspector: `WinUiDialogService`
- `src/MuayeneKayit.Inspector/Dialogs/WinUiDialogService.cs` : `IDialogService`. Mevcut ContentDialog kodunu buraya taşı
  (görünüm birebir). `XamlRoot` için `Func<XamlRoot?>` ya da `Attach(XamlRoot)`; MainWindow yüklenince bağlanır.
  Rework sonrası pencerede canlı güncelleme ve Kaydet etkinliği `ReinspectForm`'u çağırır.
- DI: `IDialogService → WinUiDialogService` (singleton).
- `MainWindow.xaml.cs`: ilgili handler'lar tek satıra iner (`await Vm.GiveDecisionAsync(option)` vb.);
  `DecisionCombo` seçimi sonrası seçim sıfırlanır (mevcut davranış). Eski diyalog metotları silinir.

## 4. Testler (`tests/MuayeneKayit.Tests`)
`FakeDialogService` (sıralı cevaplar, çağrıları kaydeder). En az:
1. Kabul: `AskDecisionAsync` → input → durum Kapandı, denetim satırı; vazgeç (null) → değişiklik yok.
2. Rework + High metal: istekte `NeedsReworkScope` true; kapsam `HighMetalOnly` kaydedilir; `AskReinspectNowAsync` false → bekler.
3. Rework → "şimdi gir" true → `AskReinspectAsync` çağrılır.
4. Reinspect same, high metal giderildi, değer boş → durum Açık, `HighMetal` false, not otomatik, derinlik korunur.
5. Reinspect new → child açılır ve seçilir.
6. Undo: child'a devredilmiş kararda `UndoRequest.ChildToVoid` dolu; gerekçe → child void, parent bekliyor.
7. Void: gerekçe null → değişiklik yok; gerekçe → Void.
8. Fotoğraf kaldır: onay false → değişmez; true → listeden ve işaretlerden çıkar.
9. `ReinspectForm.CanSave` ve `Merge` kuralları (tablo testleri), `DecisionForm.CanSave`.
10. Reopen: `NoteRequired` true.

## 5. Doğrulama
1. Tüm testler geçer (60 + yeni). Inspector Debug x64 0 hata, 0 uyarı.
2. **Tek** ekran kontrolü, geçici kayıtla (kullanıcı verisine dokunmadan):
   `C:\Users\PC\AppData\Local\Temp\claude\C--Users-PC-Documents-JUNK-GUI-PYTHON-main\b4602378-972e-45da-8ba3-71896c66e7e3\scratchpad\mv_flow.ps1`
   ( TEST-MVVM / OP10 / SN-MV1 oturumu açar, kusur ekler, derinlik kaydeder, ekran görüntüsü alır). Ardından aynı
   oturumda Disposition sekmesi → `DecisionCombo` → "Kabul (SPA)" → pencerenin ekran görüntüsü (onay düğmesi koyu,
   metin kırpılmamış) → `PrimaryButton` ile kaydet → durum "Kapandı". Değerleri `winapp ui set-value` + `get-value` ile
   doğrula (kullanıcı başka pencerede yazarken kutulara harf düşebilir; eşleşmezse tekrar yaz).
   Bitince uygulamayı kapat ve `C:\Users\PC\MuayeneKayitDemo\TEST-MVVM` klasörünü sil.
3. Commit (dal `ui-yeniden-tasarim`), Türkçe mesaj, yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`,
   son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
4. Rapor: değişen dosyalar, test sayısı, derleme, ekran kontrolü (ne görüldü), plandan sapmalar.
