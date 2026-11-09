# MuayeneKayit — Fonksiyonel Test Raporu

- **Plan:** `docs/test/FONKSIYONEL-TEST-PLANI.md` (413 durum)
- **Kod:** `C:\Users\PC\Documents\JUNK\MuayeneKayit`, dal `main`, HEAD `f9b6bfa`. `src\` altında hiçbir dosya değiştirilmedi (`git status`: yalnızca `tests/` altında yeni dosyalar ve `Program.cs` değişikliği).
- **Rapor tarihi:** 2026-11-10
- **Araçlar:** `dotnet run --project tests/MuayeneKayit.Tests` (UNIT), `winapp ui` 0.7.1 (UI Automation, UI testleri)
- **Eklenen test dosyaları (yalnızca `tests/MuayeneKayit.Tests/`):** `FonkViewModelTests.cs`, `FonkAdminTests.cs`, `FonkCoreTests.cs`, `FonkSupheliTests.cs`; `Program.cs` içinde `tests.AddRange(...)` ve `--supheli` bayrağı.

## 1. Yürütme özeti

| Aşama | Sonuç |
|---|---|
| Taban (HEAD `f9b6bfa`) | `Tümü geçti (212/212).` Plan 174 diyordu; güvenlik testleri sonradan eklendi. 49 açık güvenlik bulgusu listesi olduğu gibi bırakıldı. |
| Normal koşu (yeni testlerle) | `Tümü geçti (312/312).` (+100 yeni test, hepsi yeşil) |
| `--supheli` koşusu | 27 ŞÜPHELİ test eklenir; **27'si de kırmızı** (hata doğrulandı). Hiçbiri "geçsin diye" mevcut davranışa uydurulmadı. |
| UI (gerçek uygulama, UI Automation) | G1 duman akışı, G2 diyalog güvenliği, G3'ün bir kısmı (Rework → rework sonrası muayene, geri al, void), Dashboard, Admin ilk kurulum / giriş, Ayarlar birim. Admin Bakım sayfasında UIA okuması uygulamayı kilitledi, kalan Admin UI testleri yürütülemedi (bkz. Gözlemler). |

### Önemli dürüstlük notları

1. **GEÇTİ sayısının 128'i "mevcut otomatik test" kaydıdır.** Plan Bölüm 7.2 notuna uygun olarak, planın "Mevcut test" sütununda tam karşılığı olan durumlar, koşucu yeşil olduğu için GEÇTİ işaretlendi; bunlar tek tek yeniden doğrulanmadı. Bu satırların "Gözlem" sütununda bu açıkça yazılıdır. Bu raporda **bizzat yürütülen** durum sayısı 133 GEÇTİ + 31 HATA'dır.
2. **ATLANDI (97)** durumların hepsi zaman / kapsam sınırı nedeniyle yürütülmedi (güvenlik gerekçesiyle atlanan durum yok). Gözlem sütunu hangi parçanın yapılmadığını söyler. Başlıca yapılmayanlar: Admin UI sayfaları (Kusur tipleri, Karar açıklamaları, Bakım, Ayarlar kök / PIN değişimi UI'da), düzenleyici penceresi (F-EDIT UI), çok bölgeli rework UI senaryoları, Windows kimliği UI, Launcher UI, iki örnekli eşzamanlılık UI, ağ yolu kaybı UI, tema testleri, 40 fotoğraflı rapor.
3. **MANUEL (24):** kamera donanımı (F-CAM-001..015), F-KEY-004, F-RPT-010, F-RPT-014, F-DASH-010, F-SET-005, F-CFG-018, F-NET-008, F-NET-009, F-LNC-009, F-LNC-010. F-ADM-LOGIN-009'un UNIT kısmı yürütüldü, gerçek 10 dk bekleme yapılmadı.
4. B-02 için UNIT düzeyinde istisnanın ViewModel'den **dışarı kaçtığı** doğrulandı. Uygulamanın gerçekten çöktüğü UI'da denenmedi (bozuk dosya UI'da üretilmedi); `App.xaml.cs` içinde `UnhandledException` işleyicisi olmadığı kod okumasıyla görüldü.
5. B-07, B-17, B-18 için test yürütülmedi (aşağıda "Doğrulanamayan").

### Ortam ve güvenlik kayıtları

- UI testleri Debug x64 derlemesinin **kopyasıyla** koşuldu: `C:\Users\PC\AppData\Local\Temp\claude\0d131e098d594c64\b4602378-972e-45da-8ba3-71896c66e7e3\scratchpad\uitest\app` (kopya yanındaki `MuayeneKayit.settings.json` `rootPath` = `...\uitest\root`, doğrulandı; kaynak derlemedeki ayar `%USERPROFILE%\MuayeneKayitDemo` idi ve **kullanılmadı**, o klasöre dokunulmadı).
- Başlatılan süreçler (`...\uitest\pids.txt`): **66548** (Admin Bakım sayfasında kilitlendi, yalnızca bu PID `Stop-Process` ile kapatıldı) ve **58936** (iş bitiminde kapatıldı). Başka süreç kapatılmadı.
- Admin PIN'i yalnızca geçici kökte, rastgele 8 hane olarak üretildi (`test-pin.txt`), sohbete / rapora yazılmadı.
- `%LOCALAPPDATA%\MuayeneKayit`: başlangıçta yalnızca `camera.json` vardı; yedeklendi ve geri kopyalandı (içerik aynı, değişiklik zamanı yenilendi). **`user-settings.json` testten önce yoktu, F-SET-001 ile oluştu** (şu an `{"unit":"mm","theme":"light"}`, yani varsayılanlar); plan gereği silinmedi, kullanıcıya bildirilir.
- Testin açtığı **Edge penceresi** ("Rapor_v1.pdf - Profil 2 - Microsoft Edge", PID 62056) emin olunamadığı için **açık bırakıldı** (kullanıcının Edge profili; sekme kapatma riski).
- Yanlışlıkla `C:\Users\PC\Documents\JUNK\GUI_PYTHON-main\itm-reworkreinspect-b263\` adlı bir klasör oluştu (PowerShell'de `$s`/`$S` değişken adı çakışması, içinde tek ekran görüntüsü vardı); fark edilir edilmez silindi.
- Dosya seçici: bir kez diyalog, ben ayar yapmadan kapandı ve kusura `1_photo_01.png` (10 324 bayt, `fixtures\` dosyalarından biri **değil**) eklendi. Kaynağı seçicinin varsayılan klasöründeki bir dosya olabilir (kopyalama yalnızca geçici köke yapıldı; içeriğine bakılmadı). Nedeni bilinmiyor (kullanıcının eşzamanlı etkileşimi ya da seçici varsayılanı olabilir). Sonraki seçici çağrılarında doğru dosya (`ok.jpg` 5 588 bayt, `ok.png` 3 833 bayt) eklendi.
- Kanıt klasörü (yerinde bırakıldı): `...\scratchpad\uitest` (`shots\` 34 ekran görüntüsü, `root\` geçici veri, `fixtures\`).

## 2. Alan bazında sayılar

(GEÇTİ = bu raporda geçen + mevcut testle karşılanan; HATA = kırmızı / hata doğrulandı)

| Alan | GEÇTİ | HATA | MANUEL | ATLANDI | Toplam |
|---|---|---|---|---|---|
| F-SESS | 14 | 1 | 0 | 4 | 19 |
| F-DEF | 8 | 0 | 0 | 4 | 12 |
| F-MEAS | 16 | 1 | 0 | 2 | 19 |
| F-CUST | 4 | 1 | 0 | 4 | 9 |
| F-PART | 5 | 0 | 0 | 3 | 8 |
| F-HM | 6 | 0 | 0 | 1 | 7 |
| F-TAB | 4 | 1 | 0 | 3 | 8 |
| F-PHOTO | 13 | 2 | 0 | 0 | 15 |
| F-CAM | 1 | 0 | 14 | 0 | 15 |
| F-EDIT | 6 | 1 | 0 | 13 | 20 |
| F-SM | 20 | 0 | 0 | 1 | 21 |
| F-DEC | 13 | 0 | 0 | 8 | 21 |
| F-RWK | 11 | 1 | 0 | 5 | 17 |
| F-UNDO | 8 | 1 | 0 | 1 | 10 |
| F-VOID | 5 | 0 | 0 | 2 | 7 |
| F-COMP | 4 | 2 | 0 | 2 | 8 |
| F-RPT | 11 | 0 | 2 | 2 | 15 |
| F-DASH | 7 | 0 | 1 | 2 | 10 |
| F-SET | 4 | 0 | 1 | 5 | 10 |
| F-ID | 4 | 0 | 0 | 2 | 6 |
| F-ADM-LOGIN | 9 | 2 | 0 | 4 | 15 |
| F-ADM-REC | 9 | 0 | 0 | 1 | 10 |
| F-ADM-TYPE | 12 | 1 | 0 | 4 | 17 |
| F-ADM-FIELD | 8 | 0 | 0 | 3 | 11 |
| F-ADM-TXT | 6 | 0 | 0 | 2 | 8 |
| F-ADM-MNT | 8 | 0 | 0 | 1 | 9 |
| F-ADM-SET | 7 | 1 | 0 | 2 | 10 |
| F-CONC | 4 | 4 | 0 | 5 | 13 |
| F-CFG | 9 | 7 | 1 | 4 | 21 |
| F-NET | 2 | 4 | 2 | 1 | 9 |
| F-KEY | 1 | 1 | 1 | 4 | 7 |
| F-E2E | 14 | 0 | 0 | 2 | 16 |
| F-LNC | 8 | 0 | 2 | 0 | 10 |
| **Toplam** | **261** | **31** | **24** | **97** | **413** |

## 3. Alan tabloları

Ekran görüntüsü yolları `...\scratchpad\uitest\shots\` altındadır; `FONK F-...` değerleri `tests/MuayeneKayit.Tests` içindeki test adlarıdır (`Program.cs` listesi ya da `FonkSupheliTests.cs`).

### 4.1 Oturum girişi ve öneriler (F-SESS)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-SESS-001 | GEÇTİ | UI: Success "Yeni muayene oluşturuldu: PD170 / OP40 / SN001"; session.json ve _audit.log oluştu. (UNIT mevcut: VmOpensNewSession) | shots\01-start.png, shots\02-defect-added.png; VmOpensNewSession |
| F-SESS-002 | GEÇTİ | UNIT: ikinci muayeneci mevcut oturumu açar, session.json Inspector "A" kalır, denetim "B" adına | FONK F-SESS-002 |
| F-SESS-003 | GEÇTİ | UNIT: boş / boşluk Inspector reddedildi, klasör oluşmadı | FONK F-SESS-003 |
| F-SESS-004 | GEÇTİ | UNIT + UI ("Parça no boş olamaz." gözlendi); kökte klasör oluşmadı | FONK F-SESS-004 |
| F-SESS-005 | GEÇTİ | UNIT: 10 tehlikeli parça no reddedildi, hiçbir şey yazılmadı | FONK F-SESS-005 |
| F-SESS-006 | GEÇTİ | 64 karakter kabul, 65 red | FONK F-SESS-006 |
| F-SESS-007 | GEÇTİ | Baş/son boşluk kırpıldı, Türkçe harfler kabul | FONK F-SESS-007 |
| F-SESS-008 | HATA | B-08 doğrulandı: "_X" kabul edildi ama Index'te görünmüyor; "_config" altına muayene klasörü yazıldı | FONK F-SESS-008 [B-08] |
| F-SESS-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SESS-010 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SESS-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmSuggestionsListFoldersAndSkipUnderscore |
| F-SESS-012 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmSuggestionsListFoldersAndSkipUnderscore |
| F-SESS-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmHiddenSuggestionsPerBoxAndReturnOnUse |
| F-SESS-014 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-SESS-015 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmChangeSessionClears |
| F-SESS-016 | GEÇTİ | ChangeSession bekleyen 0.3 değerini yazdı | FONK F-SESS-016 |
| F-SESS-017 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SESS-018 | GEÇTİ | 500 karakter ve özel karakterli Inspector adıyla denetim zinciri sağlam | FONK F-SESS-018 |
| F-SESS-019 | GEÇTİ | UI: başlık "MuayeneKayit 1.0.2" | shots\01-start.png |

### 4.2 Kusur listesi (F-DEF)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-DEF-001 | GEÇTİ | UI: "Kusur #1 (Dent) eklendi. Ölçümleri girin."; defects\1.json oluştu | shots\02-defect-added.png |
| F-DEF-002 | GEÇTİ | false döner, "Kusur tipi seçin." | FONK F-DEF-002 |
| F-DEF-003 | GEÇTİ | "Önce bir oturum açın." | FONK F-DEF-003 |
| F-DEF-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StoreIdsNeverReused |
| F-DEF-005 | GEÇTİ | SpecName kusura kopyalandı | FONK F-DEF-005 |
| F-DEF-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmUnitFollowsSettingsPerDefect |
| F-DEF-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEF-008 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmHighMetalShowsInTypeAndSentence |
| F-DEF-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmAutoSaveOnDefectSwitch, VmFirstSelectionAfterOpenUsesCurrentStep |
| F-DEF-010 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEF-011 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEF-012 | GEÇTİ | Şablonsuz tipte tüm yerleşik alanlar görünür | FONK F-DEF-012 |

### 4.3 Ölçüm, otomatik kayıt ve sınırlar (F-MEAS)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-MEAS-001 | GEÇTİ | UI: D 0.12 / W 1.5 / L 3 kaydedildi, düzeltme kaydı json'da; "✓ 1. Ölçüm", Fotoğraf sekmesi. Not: UIA ile odak değiştiği için bildirim "#1 ölçümleri kayıtlı." (blur otomatik kaydı önce yazdı); "güncellendi, düzeltme geçmişe yazıldı" metni #4'te görüldü. | shots\03-measured.png |
| F-MEAS-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmInvalidMeasurementNotifiesError |
| F-MEAS-003 | GEÇTİ | 0, -1, -0 dört alanda reddedildi, json değişmedi | FONK F-MEAS-003 |
| F-MEAS-004 | GEÇTİ | NaN, Infinity, 1e309 reddedildi | FONK F-MEAS-004 |
| F-MEAS-005 | GEÇTİ | 0.0001 ve 1e-9 kabul; BİLGİ: 1e308 kabul ediliyor (üst sınır yok, ürün kararı) | FONK F-MEAS-005 |
| F-MEAS-006 | GEÇTİ | 0 ve 360 kabul; -0.01 ve 360.01 red | FONK F-MEAS-006 |
| F-MEAS-007 | GEÇTİ | 40 kabul, 41 red, boşluk → null | FONK F-MEAS-007 |
| F-MEAS-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ParseAcceptsLeadingDotAndComma |
| F-MEAS-009 | GEÇTİ | tr-TR ve en-US'te "1.234,5", "1,234.5", "1 234" sessizce yanlış sayıya dönmüyor | FONK F-MEAS-009 |
| F-MEAS-010 | GEÇTİ | "#1 ölçümleri kayıtlı.", düzeltme yok, sekme değişmedi | FONK F-MEAS-010 |
| F-MEAS-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmAutoSaveMergesAndKeepsStep |
| F-MEAS-012 | GEÇTİ | Değişiklik yoksa dosya/denetim yazılmaz | FONK F-MEAS-012 |
| F-MEAS-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MeasurementEditMergeRules, VmAutoSaveMergesAndKeepsStep |
| F-MEAS-014 | GEÇTİ | Geçersiz metinle otomatik kayıt yazmaz, düzeltilince yazar | FONK F-MEAS-014 |
| F-MEAS-015 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmPhotoCardsAreViewOnlyWhenClosed |
| F-MEAS-016 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmCustomFieldOrphanValueStaysVisible |
| F-MEAS-017 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmUnitHeadersFollowSettingsWhenNoDefect |
| F-MEAS-018 | HATA | B-01 doğrulandı: mm 0.125→0.13, açı 12.345→12.35, inç 0.123456→0.1235, Rework Snapshot D 0.13 | FONK F-MEAS-018 [B-01] |
| F-MEAS-019 | GEÇTİ | "Önce listeden bir kusur seçin." | FONK F-MEAS-019 |

### 4.4 Ek ölçüm alanları — muayene tarafı (F-CUST)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-CUST-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmCustomFieldShownSavedAndInSentence |
| F-CUST-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmCustomFieldValidation |
| F-CUST-003 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: SentenceReportCompareValidate |
| F-CUST-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmCustomFieldOrphanValueStaysVisible |
| F-CUST-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectMergesCustomField |
| F-CUST-006 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CUST-007 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CUST-008 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-CUST-009 | HATA | B-12 doğrulandı: uzunluk 0.3 → tür=Text, metin "0.3" olarak yeniden yazıldı | FONK F-CUST-009 [B-12] |

### 4.5 Etkilenen parçalar / lot (F-PART)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-PART-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmAffectedPartsAutoSave, PartListParseNormalizeDescribe |
| F-PART-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: PartListRejectsInvalid, VmAffectedPartsAutoSave |
| F-PART-003 | GEÇTİ | Kabul/red kümesi doğru | FONK F-PART-003 |
| F-PART-004 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: StatementPartScopeAndOldRecord |
| F-PART-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ChildInheritsAffectedParts |
| F-PART-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ReportNonconformanceAndPartsLines, CsvHasAffectedPartsColumn |
| F-PART-007 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmAffectedPartsAutoSave |
| F-PART-008 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.6 High metal (F-HM)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-HM-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmHighMetalShowsInTypeAndSentence |
| F-HM-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: HighMetalEditLogged |
| F-HM-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReworkHighMetalAsksScopeAndWaits |
| F-HM-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: Scenario5_HighMetalOnlyRework, VmReinspectSameHighMetalGoneKeepsDepth |
| F-HM-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: HighMetalSentenceAndReport |
| F-HM-006 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-HM-007 | GEÇTİ | Geçersiz D iken HM işaretlenince hata, yazılmaz; düzeltince birlikte yazılır | FONK F-HM-007 |

### 4.7 Adım sekmeleri ve yönlendirme (F-TAB)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-TAB-001 | GEÇTİ | UI: "Ölçüme dön" ile Ölçüm'de kalır, "Yine de fotoğrafa geç" ile Fotoğraf | shots\09-skip-measure-dialog.png, shots\10-photo-tab-no-measure.png |
| F-TAB-002 | GEÇTİ | UI: Enter ve Esc Ölçüm'de bırakır | shots\09-skip-measure-dialog.png |
| F-TAB-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmSaveMeasurementsGoesToPhoto, VmDecisionTabCheckAfterDecision |
| F-TAB-004 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-TAB-005 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-TAB-006 | HATA | B-13 doğrulandı: sıradaki adım Measure, Kaydet vurgulu | FONK F-TAB-006 [B-13] |
| F-TAB-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmRefreshKeepsSelectedStep, VmListViewReselectAfterRefreshKeepsTab |
| F-TAB-008 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmVoidLastChildReopensParentWithoutLeaks |

### 4.8 Fotoğraf ekleme, kartlar, kaldırma (F-PHOTO)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-PHOTO-001 | GEÇTİ | UI: fixtures\ok.jpg eklendi (4_photo_01.jpg, 5588 bayt); not: seçici diyaloğu bir kez beklenmedik biçimde kendiliğinden kapandı (bkz. Gözlemler) | shots\22-picker-state.png |
| F-PHOTO-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmAddPhotoFromFile |
| F-PHOTO-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: PhotoImportRules, VmAddPhotoFromFile |
| F-PHOTO-004 | GEÇTİ | 25 MB+1 red, tam 25 MB kabul | FONK F-PHOTO-004 |
| F-PHOTO-005 | GEÇTİ | BMP/GIF/WEBP red; .JPG ve .jpeg kabul | FONK F-PHOTO-005 |
| F-PHOTO-006 | GEÇTİ | "Fotoğraf bulunamadı." | FONK F-PHOTO-006 |
| F-PHOTO-007 | GEÇTİ | 1_photo_01.jpg, 1_photo_02.png, 1_photo_03.jpg; 1.1/1.2/1.3 | FONK F-PHOTO-007 |
| F-PHOTO-008 | HATA | B-10 doğrulandı: IOException "already exists", ekleme başarısız | FONK F-PHOTO-008 [B-10] |
| F-PHOTO-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmRemovePhotoConfirmation |
| F-PHOTO-010 | GEÇTİ | UI: kaldırma sonrası photos=0, dosya diskte kaldı, denetimde defect.photo.remove (not: kaldırma Enter ile tetiklendi, bkz. F-PHOTO-011) | shots\23-remove-photo-dialog.png; _audit.log |
| F-PHOTO-011 | HATA | B-24 (YENİ) doğrulandı: UIA odağı diyalog açıldıktan 2,5 sn sonra "Kaldır" düğmesinde; Enter fotoğrafı kaldırdı | shots\23-remove-photo-dialog.png; get-focused çıktısı |
| F-PHOTO-012 | GEÇTİ | Geçersiz adlar yok sayıldı, diyalog açılmadı | FONK F-PHOTO-012 |
| F-PHOTO-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmPhotoCardsAreViewOnlyWhenClosed |
| F-PHOTO-014 | GEÇTİ | Temiz kopya yoksa orijinal gösterilir | FONK F-PHOTO-014 |
| F-PHOTO-015 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectPerRegionOutcomes |

### 4.9 Kamera (F-CAM) — donanım gerektirir

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-CAM-001 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-002 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-003 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-004 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-005 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-006 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-007 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-008 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-009 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-010 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-011 | GEÇTİ | UNIT kısmı: foto #1'e eklenir, #2 seçiliyken düzenleyici açılmaz (MANUAL kısmı yürütülmedi) | FONK F-CAM-011 |
| F-CAM-012 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-013 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-014 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CAM-015 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |

### 4.10 Fotoğraf düzenleyici ve arka plan kaldırma (F-EDIT)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-EDIT-001 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-003 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-004 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-005 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-008 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-010 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MarkupValidation |
| F-EDIT-011 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-012 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: BackgroundSeparatesPart, CleanedCopyKeepsOriginal, VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-014 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-015 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-016 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-017 | GEÇTİ | İşaret #1'e yazıldı, seçim #2 kaldı | FONK F-EDIT-017 |
| F-EDIT-018 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-EDIT-019 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmOpenEditorOnClosedDefectOpensFile |
| F-EDIT-020 | HATA | B-14 doğrulandı: kapatılmış kusura işaret yazıldı | FONK F-EDIT-020 [B-14] |

### 4.11 Durum makinesi (F-SM) — Bölüm 3.7 tablosuna karşı

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-SM-001 | GEÇTİ | 10 durum × 14 karar tam matris | FONK F-SM-001 |
| F-SM-002 | GEÇTİ | Kaynak matrisi | FONK F-SM-002 |
| F-SM-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmDecisionAcceptClosesAndCancelChangesNothing |
| F-SM-004 | GEÇTİ | Rework hedefi zorunlu | FONK F-SM-004 |
| F-SM-005 | GEÇTİ | Reinspect: AwaitingReinspect, Snapshot yok | FONK F-SM-005 |
| F-SM-006 | GEÇTİ | Repairing geçişleri | FONK F-SM-006 |
| F-SM-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ScrapIsFinal, Scenario3_ReopenAndScrapFinal |
| F-SM-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StateMrbRejectedScraps, Scenario4_MrbRejected |
| F-SM-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StateVendorIsOpen |
| F-SM-010 | GEÇTİ | AfterRework geçişleri | FONK F-SM-010 |
| F-SM-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VoidOnlyWithoutHistory |
| F-SM-012 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ReopenRules, VmReopenRequiresNote |
| F-SM-013 | GEÇTİ | Kapsam / yeni kusur numarası yalnızca izinli kararlarda | FONK F-SM-013 |
| F-SM-014 | GEÇTİ | Voided/Superseded hiçbir kararı kabul etmez | FONK F-SM-014 |
| F-SM-015 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: SameDefectLoops, Scenario1_SameDefectThenConforms |
| F-SM-016 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: UndoRestoresPreviousState |
| F-SM-017 | GEÇTİ | Accept→Reopen→Undo: Kapandı (VM düzeyinde) | FONK F-UNDO-006 |
| F-SM-018 | GEÇTİ | Void geri alınamaz; MrbRejected geri alınabilir | FONK F-SM-018 |
| F-SM-019 | GEÇTİ | Store dışı Undo child'lı kararı reddeder | FONK F-SM-019 |
| F-SM-020 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: GateBlocksOpen, GateSupersedeResolves |
| F-SM-021 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmDecisionOptions |

### 4.12 Karar diyaloğu (F-DEC)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-DEC-001 | GEÇTİ | UI: Kabul diyaloğu başlık/durum/açıklama doğru; kayıt sonrası Kapandı (referans girilmedi) | shots\06-decision-dialog.png, shots\07-closed.png |
| F-DEC-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmDecisionAcceptClosesAndCancelChangesNothing |
| F-DEC-003 | GEÇTİ | UI: Esc diyaloğu kapatır, kayıt yok (Kabul, Rework, Submit MRB, Scrap) | shots\11-after-decision-keys.png |
| F-DEC-004 | GEÇTİ | UI: ilk açılışta Enter kayıt yapmaz (not kutusunda Enter satırı denenmedi) | shots\11-after-decision-keys.png |
| F-DEC-005 | GEÇTİ | UI: hedef varsayılan OP40, OP50 yazıldı ve kaydedildi, "Sonra" ile Rework sonrası muayene bekliyor | shots\12-rework-dialog.png, shots\14-awaiting-reinspect.png |
| F-DEC-006 | GEÇTİ | Boş hedef → oturum operasyonu | FONK F-DEC-006 |
| F-DEC-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReworkHighMetalAsksScopeAndWaits |
| F-DEC-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReworkNowOpensReinspectDialog |
| F-DEC-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEC-010 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReopenRequiresNote |
| F-DEC-011 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmDecisionOptions |
| F-DEC-012 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmDecisionOptions |
| F-DEC-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StateMrbRejectedScraps |
| F-DEC-014 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmDecisionAsksRegionsAndMarksUncheckedConforming, VmDecisionRegionSelectionIsValidated |
| F-DEC-015 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: ReinspectFormRegionRules |
| F-DEC-016 | GEÇTİ | Kaydedilmemiş D=0.9 karardan önce yazıldı | FONK F-DEC-016 |
| F-DEC-017 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-DEC-018 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEC-019 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: ReportEscapesAndHidesVoid |
| F-DEC-020 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DEC-021 | GEÇTİ | Karar ve geri alma B adına | FONK F-DEC-021 |

### 4.13 Rework sonrası muayene (F-RWK)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-RWK-001 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-RWK-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectConformsAndWrongStateErrors |
| F-RWK-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: Scenario1_SameDefectThenConforms |
| F-RWK-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectSameHighMetalGoneKeepsDepth |
| F-RWK-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectNewOpensChild, VmReinspectMultipleNewDefects, VmReinspectNewDefectWithoutTypeIsRejected |
| F-RWK-006 | HATA | B-06 UI'da doğrulandı: D="abc" + W="0,7" ile Kaydet açıldı; D 0.4 kaldı, W 0.7 yazıldı, hata yok | shots\17-B06-abc-and-W.png |
| F-RWK-007 | GEÇTİ | D=-1 reddedildi, kusur beklemeye devam | FONK F-RWK-007 |
| F-RWK-008 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VmReinspectConformsAndWrongStateErrors |
| F-RWK-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: Scenario5_HighMetalOnlyRework |
| F-RWK-010 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectPerRegionOutcomes, VmReinspectRegionsMissingChoiceIsRejected, MixedRegionOutcomes |
| F-RWK-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectPerRegionOutcomes |
| F-RWK-012 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-RWK-013 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ChildDefectInheritsParentUnit, ChildInheritsAffectedParts |
| F-RWK-014 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReinspectConformsAndWrongStateErrors |
| F-RWK-015 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-RWK-016 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: AllRegionsConformClosesAndInvalidPhotoRejected |
| F-RWK-017 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |

### 4.14 Geri alma (F-UNDO)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-UNDO-001 | HATA | P3: UI'da "Son karar: Accept (...)" İngilizce etiket; plan "Kabul" bekliyor | shots\07-closed.png |
| F-UNDO-002 | GEÇTİ | UI: gerekçe boş/boşluk iken "Geri al" kapalı; "yanlış seçim" ile geri alındı, disposition.undo yazıldı | shots\18-undo-dialog.png, shots\20-after-undo.png |
| F-UNDO-003 | GEÇTİ | UI: Esc ile vazgeçilince kayıt değişmedi | shots\18-undo-dialog.png |
| F-UNDO-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: UndoMrbRejectedRemovesAutoScrap |
| F-UNDO-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmUndoChildTransferVoidsChild, VmUndoTwoChildrenListsBoth, UndoVoidsAllChildrenAndRestoresRegions |
| F-UNDO-006 | GEÇTİ | Reopen geri alınır, Accept geri alınır | FONK F-UNDO-006 |
| F-UNDO-007 | GEÇTİ | Ardışık geri almalar ayrı satır | FONK F-UNDO-006 |
| F-UNDO-008 | GEÇTİ | "Geri alınacak karar yok." | FONK F-UNDO-008 |
| F-UNDO-009 | GEÇTİ | B kullanıcısı geri alır, kayıt B adına | FONK F-DEC-021 |
| F-UNDO-010 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: VoidChildOfSameDefectDecisionKeepsParentOpen, VmUndoMultipleChildrenAndVoidOneOfTwo |

### 4.15 Void (F-VOID)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-VOID-001 | GEÇTİ | UI: VoidButton yalnızca karar geçmişi olmayan açık kusurda | shots\21-voided.png |
| F-VOID-002 | GEÇTİ | UI: gerekçe yokken "Void et" kapalı; "yanlış tip" ile void, listede Void, sayıdan düştü | shots\21-voided.png |
| F-VOID-003 | GEÇTİ | UI: Esc/Enter ile kayıt yok | shots\21-voided.png |
| F-VOID-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmVoidLastChildReopensParentWithoutLeaks, VoidLastChildReopensParent |
| F-VOID-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VoidOneOfManyChildrenKeepsParentTransferred |
| F-VOID-006 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-VOID-007 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: IndexRebuildsRows |

### 4.16 Muayeneyi tamamlama (F-COMP)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-COMP-001 | GEÇTİ | Warning "Muayene kapatılamaz, açık kusurlar var: #1 (Açık), #2 (MRB bekliyor)" | FONK F-COMP-001 |
| F-COMP-002 | GEÇTİ | UI: "Muayene tamamlandı. Rapor oluşturabilirsiniz."; session.json Completed; session.complete | shots\07-closed.png |
| F-COMP-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: GateSupersedeResolves |
| F-COMP-004 | HATA | B-04 UI ve UNIT'te doğrulandı: Tamamlandı oturuma kusur eklendi, durum Completed kaldı; Dashboard "Tamamlandı" + açık kusur | shots\08-B04-completed-with-open-defect.png, shots\30-dashboard.png; FONK F-COMP-004 [B-04] |
| F-COMP-005 | HATA | B-15 doğrulandı: 2 adet session.complete denetim satırı | FONK F-COMP-005 [B-15] |
| F-COMP-006 | GEÇTİ | Tamamlamadan önce bekleyen metin yazıldı | FONK F-COMP-006 |
| F-COMP-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-COMP-008 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.17 Rapor (F-RPT)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-RPT-001 | GEÇTİ | UI: Rapor_v1.pdf (82 871 bayt), HTML silindi, report.create Rapor_v1 (pdf); Edge penceresi "Rapor_v1.pdf - Profil 2 - Microsoft Edge" açıldı (bırakıldı) | scratch root\PD170\OP40\SN001\reports\Rapor_v1.pdf; _audit.log |
| F-RPT-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReportWritesHtmlAndOpensPdf, ReportVersionRules |
| F-RPT-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReportFallsBackToHtml |
| F-RPT-004 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-RPT-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmReportWithoutSessionErrors |
| F-RPT-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ReportEscapesAndHidesVoid, ReportDrawsMarkup, ReportShowsInchValues, ReportPhotoPerPage, ReportBilingualTests.*, ReportShowsRegionLinesAndPluralLinks, ReportNonconformanceAndPartsLines |
| F-RPT-007 | GEÇTİ | Açık kusurlu + kusursuz oturumda rapor üretildi (F-RPT-008/009/011/015 ile aynı test) | FONK F-RPT-007/008/009/011/015 |
| F-RPT-008 | GEÇTİ | Kusursuz oturum raporu | FONK F-RPT-007/008/009/011/015 |
| F-RPT-009 | GEÇTİ | Kaldırılan foto rapora girmez | FONK F-RPT-007/008/009/011/015 |
| F-RPT-010 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-RPT-011 | GEÇTİ | v3.html silinmeden v4 üretildi | FONK F-RPT-007/008/009/011/015 |
| F-RPT-012 | GEÇTİ | "reports" dosya ise hata, IsReportRunning false | FONK F-RPT-012 |
| F-RPT-013 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-RPT-014 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-RPT-015 | GEÇTİ | Diskte olmayan foto ile rapor üretildi | FONK F-RPT-007/008/009/011/015 |

### 4.18 Dashboard (F-DASH)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-DASH-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DashEmptyBeforeRefreshThenFiltersAndSummary |
| F-DASH-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DashboardFilter, DashEmptyBeforeRefreshThenFiltersAndSummary |
| F-DASH-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DashEmptyTextsAndClearFilters |
| F-DASH-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DashEmptyTextsAndClearFilters |
| F-DASH-005 | GEÇTİ | UI: sağlam zincir için "Denetim kaydı sağlam / Zincir baştan sona doğrulandı." (bozuk zincir UI'da denenmedi; UNIT mevcut) | shots\31-dash-audit-ok.png |
| F-DASH-006 | GEÇTİ | UI: "Aç" Inspector boşken P/O/S doldurur, InspectorBox odakta, bilgi mesajı | shots\32-dash-open.png |
| F-DASH-007 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: DashRowCardsCarryChipsAndOpenEvent |
| F-DASH-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DashRowCardsCarryChipsAndOpenEvent |
| F-DASH-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-DASH-010 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |

### 4.19 Ayarlar: birim ve tema (F-SET)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-SET-001 | GEÇTİ | UI: ayar inch iken yeni kusur #5 Inch, başlıklar "(in)"; sonra mm'e geri alındı | shots\35-inch-defect.png; user-settings.json |
| F-SET-002 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SET-003 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SET-004 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SET-005 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-SET-006 | GEÇTİ | Geçersiz indeksler ayarı değiştirmez | FONK F-SET-006 |
| F-SET-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: UnitCompareInch |
| F-SET-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ReportShowsInchValues, UnitJsonOldAndRoundTrip |
| F-SET-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-SET-010 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.20 Windows kimliği (F-ID)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ID-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmInspectorFromWindowsUser, WindowsUserSwitchSavesSharedSetting |
| F-ID-002 | GEÇTİ | Windows kimliği açıkken kutudaki "Sahte" yok sayıldı | FONK F-ID-002 |
| F-ID-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmInspectorFromWindowsUser |
| F-ID-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmInspectorFromWindowsUser |
| F-ID-005 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-ID-006 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.21 Admin — giriş, kilit, pencere (F-ADM-LOGIN)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-LOGIN-001 | GEÇTİ | UI: ilk kurulum ipucu, PIN + PIN (tekrar), "PIN'i belirle ve gir", LoginRoot geçici kök | shots\25-admin-login.png |
| F-ADM-LOGIN-002 | GEÇTİ | UI: "PIN'ler aynı değil.", admin.json oluşmadı | shots\26-admin-pin-mismatch.png |
| F-ADM-LOGIN-003 | GEÇTİ | UNIT: 5 hane, harf, boş, 13 hane red; 12 hane kabul | FONK F-ADM-LOGIN-003 |
| F-ADM-LOGIN-004 | GEÇTİ | UI: giriş yapıldı, _config\admin.json, denetimde admin.pin.set ve admin.login | shots\27-admin-records.png |
| F-ADM-LOGIN-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: FiveWrongPinsWaitSixtySecondsThenRetry |
| F-ADM-LOGIN-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: FiveWrongPinsWaitSixtySecondsThenRetry, WrongPinCounterResetsAfterSuccess |
| F-ADM-LOGIN-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-LOGIN-008 | HATA | B-16 doğrulandı (UNIT): yeni LoginViewModel kilidi sıfırlıyor (UI'da denenmedi) | FONK F-ADM-LOGIN-008 [B-16] |
| F-ADM-LOGIN-009 | GEÇTİ | UNIT kısmı: 10 dk 1 sn sonra kilitlendi (gerçek 10 dk bekleme MANUEL) | FONK F-ADM-LOGIN-009 |
| F-ADM-LOGIN-010 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-LOGIN-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: NavigationPagesLockAndRefresh |
| F-ADM-LOGIN-012 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-LOGIN-013 | HATA | B-19 (ürün kararı): admin.json silinince İlk kurulum ekranı geliyor | FONK F-ADM-LOGIN-013 [B-19] |
| F-ADM-LOGIN-014 | GEÇTİ | Iterations=5000 olan kayıt kabul edilmedi | FONK F-ADM-LOGIN-014 |
| F-ADM-LOGIN-015 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.22 Admin — Kayıtlar (F-ADM-REC)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-REC-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: RecordsFiltersSummaryAndCards |
| F-ADM-REC-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: RecordsFiltersSummaryAndCards |
| F-ADM-REC-003 | GEÇTİ | 600 oturum: 500 kart, özet 600, CSV ≥601 satır | FONK F-ADM-REC-003 |
| F-ADM-REC-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: RecordsExportCsvWritesBomAndFilteredRows |
| F-ADM-REC-005 | GEÇTİ | İptalde bildirim yok; salt okunur hedefte Error | FONK F-ADM-REC-005 |
| F-ADM-REC-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: CsvEscapes |
| F-ADM-REC-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: RecordsOpenFolderAndLatestReport |
| F-ADM-REC-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: RecordsOpenFolderAndLatestReport |
| F-ADM-REC-009 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-REC-010 | GEÇTİ | Kayıtlar sayfası yalnızca _index.json/_audit.log değiştirir | FONK F-ADM-REC-010 |

### 4.23 Admin — Kusur tipleri (F-ADM-TYPE)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-TYPE-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-003 | GEÇTİ | Ad kuralları | FONK F-ADM-TYPE-003 |
| F-ADM-TYPE-004 | GEÇTİ | Spec 60/61 karakter | FONK F-ADM-TYPE-004 |
| F-ADM-TYPE-005 | GEÇTİ | "Listeden bir tip seçin." | FONK F-ADM-TYPE-005 |
| F-ADM-TYPE-006 | GEÇTİ | Alansız tip "ölçüm yok" | FONK F-ADM-TYPE-006 |
| F-ADM-TYPE-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-009 | GEÇTİ | Tüm tipler silinip kaydedilemez, dosya yazılmaz | FONK F-ADM-TYPE-009 |
| F-ADM-TYPE-010 | GEÇTİ | 100 kabul, 101 red | FONK F-ADM-TYPE-010 |
| F-ADM-TYPE-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesResetToDefaultsAsksConfirm |
| F-ADM-TYPE-012 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-013 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-TYPE-014 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-ADM-TYPE-015 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-TYPE-016 | HATA | B-11 doğrulandı: ad değişikliği ne kaydedildi ne uyarıldı, "kaydedildi" mesajı geldi | FONK F-ADM-TYPE-016 [B-11] |
| F-ADM-TYPE-017 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |

### 4.24 Admin — Ek ölçüm alanları (F-ADM-FIELD)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-FIELD-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-002 | GEÇTİ | Ad kuralları | FONK F-ADM-FIELD-002 |
| F-ADM-FIELD-003 | GEÇTİ | Kısaltma kuralları | FONK F-ADM-FIELD-003 |
| F-ADM-FIELD-004 | GEÇTİ | Yinelenen ad / kısaltma | FONK F-ADM-FIELD-004 |
| F-ADM-FIELD-005 | GEÇTİ | 30 kabul, 31 red | FONK F-ADM-FIELD-005 |
| F-ADM-FIELD-006 | GEÇTİ | Tür indeksleri | FONK F-ADM-FIELD-006 |
| F-ADM-FIELD-007 | GEÇTİ | Güncelleme kuralları | FONK F-ADM-FIELD-007 |
| F-ADM-FIELD-008 | ATLANDI | Aynı kök neden: bkz. F-CUST-009 (B-12), orada HATA doğrulandı. | FONK F-CUST-009 [B-12] |
| F-ADM-FIELD-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-010 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-011 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |

### 4.25 Admin — Karar açıklamaları (F-ADM-TXT)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-TXT-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TextsEditSaveAndResetAll |
| F-ADM-TXT-002 | GEÇTİ | Boş açıklama / 601 / sekme kuralları (F-ADM-TXT-003, -004 ile aynı test) | FONK F-ADM-TXT-002 |
| F-ADM-TXT-003 | GEÇTİ | 600 kabul, 601 red | FONK F-ADM-TXT-002 |
| F-ADM-TXT-004 | GEÇTİ | Sekme red, satır sonu kabul | FONK F-ADM-TXT-002 |
| F-ADM-TXT-005 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: TextsEditSaveAndResetAll |
| F-ADM-TXT-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: TextsEditSaveAndResetAll |
| F-ADM-TXT-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DecisionTextRules |
| F-ADM-TXT-008 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.26 Admin — Bakım (F-ADM-MNT)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-MNT-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MaintenanceRebuildAndVerifyAudit |
| F-ADM-MNT-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MaintenanceRebuildAndVerifyAudit |
| F-ADM-MNT-003 | GEÇTİ | ≤300 satır, yeniden eskiye, bozuk satır atlanır | FONK F-ADM-MNT-003 |
| F-ADM-MNT-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MaintenanceStaleLockRemovalNeedsConfirm |
| F-ADM-MNT-005 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-MNT-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MaintenanceStaleLockRemovalNeedsConfirm |
| F-ADM-MNT-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StaleLockRules |
| F-ADM-MNT-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: StaleLockRules |
| F-ADM-MNT-009 | GEÇTİ | 9 dk kilit listelenmez, 11 dk listelenir | FONK F-ADM-MNT-009 |

### 4.27 Admin — Ayarlar (F-ADM-SET)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-ADM-SET-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: SettingsRootMustExistThenLocksSession |
| F-ADM-SET-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: SettingsRootMustExistThenLocksSession |
| F-ADM-SET-003 | GEÇTİ | Boş yol reddedildi | FONK F-ADM-SET-003 |
| F-ADM-SET-004 | GEÇTİ | Ham metin yazılır, bağlam genişletilmiş yolu kullanır | FONK F-ADM-SET-004 |
| F-ADM-SET-005 | HATA | B-09 doğrulandı: göreli yol "rel" kaydedildi | FONK F-ADM-SET-005 [B-09] |
| F-ADM-SET-006 | GEÇTİ | 261 karakter: "Ortak alan yolu geçersiz." | FONK F-ADM-SET-006 |
| F-ADM-SET-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-ADM-SET-008 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: SettingsChangePinValidatesOldAndRepeat |
| F-ADM-SET-009 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: WindowsUserSwitchSavesSharedSetting |
| F-ADM-SET-010 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |

### 4.28 Eşzamanlılık — iki kullanıcı aynı oturum klasöründe (F-CONC)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-CONC-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ParallelAddDefectsGetUniqueIds |
| F-CONC-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ParallelUpdatesSameDefectLoseNothing |
| F-CONC-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ParallelAuditAppendsKeepChain |
| F-CONC-004 | HATA | B-03 doğrulandı: A'nın D=0.5 ölçümü B'nin yalnızca-not kaydıyla 0.1'e döndü | FONK F-CONC-004 [B-03] |
| F-CONC-005 | GEÇTİ | Eski ekran Rework → "'Kapandı' durumunda 'Rework' kararı verilemez." | FONK F-CONC-005 |
| F-CONC-006 | HATA | B-04 doğrulandı: Completed + açık kusur | FONK F-CONC-006 [B-04] |
| F-CONC-007 | HATA | B-10 doğrulandı: 15/15 denemede biri "already exists" IOException aldı | FONK F-CONC-007 [B-10] |
| F-CONC-008 | ATLANDI | UNIT kısmı yürütüldü (2 sn tutulan kilit beklenir, GEÇTİ: FONK F-CONC-008); UI kısmı (15 sn sonra hata, B-07 donma) yürütülmedi | FONK F-CONC-008 |
| F-CONC-009 | ATLANDI | Mevcut test yalnızca kısmen kapsıyor, tamamlayıcı test yazılmadı / UI yürütülmedi. | kısmen: StaleLockRules |
| F-CONC-010 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CONC-011 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CONC-012 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-CONC-013 | HATA | B-20 doğrulandı: 25/25 eşzamanlı rapor çiftinde aynı sürüm | FONK F-CONC-013 [B-20] |

### 4.29 Bozuk / eksik yapılandırma ve veri dosyaları (F-CFG)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-CFG-001 | GEÇTİ | Ayar dosyası yoksa %USERPROFILE%\MuayeneKayitDemo yolu döner (klasöre dokunulmadı) | FONK F-CFG-001 |
| F-CFG-002 | GEÇTİ | Çöp / {} / boş / sayı / null / [] ayar dosyası → varsayılan yol, istisna yok | FONK F-CFG-002 |
| F-CFG-003 | GEÇTİ | Genişletme ve 3/4 seviye | FONK F-CFG-003 |
| F-CFG-004 | GEÇTİ | Çöp / [] / yinelenen ad → varsayılan 9 tip | FONK F-CFG-004 |
| F-CFG-005 | HATA | B-21 doğrulandı: 0/2 geçerli alan yüklendi | FONK F-CFG-005 [B-21] |
| F-CFG-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: DecisionTextRules |
| F-CFG-007 | GEÇTİ | Bozuk kimlik dosyası → elle | FONK F-CFG-007 |
| F-CFG-008 | HATA | B-02 doğrulandı: JsonException ve FormatException Login()'den dışarı kaçtı | FONK F-CFG-008 [B-02] |
| F-CFG-009 | HATA | B-02 doğrulandı: yarım/boş/çöp session.json JsonException ile OpenSession'dan kaçtı | FONK F-CFG-009 [B-02] |
| F-CFG-010 | HATA | B-02 doğrulandı: bozuk defects\2.json oturum açma, Dashboard.Refresh ve IndexBuilder.Rebuild'i düşürüyor | FONK F-CFG-010 [B-02] |
| F-CFG-011 | HATA | B-02 doğrulandı: yarım _audit.log son satırı AddDefect'te JsonException; kusur dosyası yazıldı, denetimsiz | FONK F-CFG-011 [B-02] |
| F-CFG-012 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: AuditChainDetectsTamper |
| F-CFG-013 | HATA | B-05 doğrulandı: 5 MB'lık kusur dosyası yazıldı, sonraki okuma InvalidDataException | FONK F-CFG-013 [B-05] |
| F-CFG-014 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CFG-015 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CFG-016 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: HiddenSuggestionSetRulesAndJson |
| F-CFG-017 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-CFG-018 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-CFG-019 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-CFG-020 | HATA | P3: notlar.json {} "#0" kusur olarak listelendi | FONK F-CFG-020 |
| F-CFG-021 | GEÇTİ | _config yokken ilk kurulum + varsayılanlar | FONK F-CFG-021 |

### 4.30 Ağ yolu kaybı (F-NET) — geçici kökü yeniden adlandırarak

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-NET-001 | HATA | B-23 (YENİ) doğrulandı: kök yokken kusur eklendi (FileLock.Acquire klasör ağacını yeniden oluşturdu) | FONK F-NET-001 [B-23] |
| F-NET-002 | GEÇTİ | Kök yokken otomatik kayıt hata verir, değer kutuda kalır, kök dönünce kaydedilir | FONK F-NET-002 |
| F-NET-003 | HATA | B-22 doğrulandı | FONK F-NET-003 [B-22] |
| F-NET-004 | HATA | B-23 doğrulandı: kök yokken rapor üretildi (yeni boş kökte) | FONK F-NET-004 [B-23] |
| F-NET-005 | ATLANDI | UNIT testi yazılmadı (zaman / kapsam sınırı). | - |
| F-NET-006 | HATA | B-22 doğrulandı: İlk kurulum sunuldu ve kök yeniden oluşturuldu | FONK F-NET-006 [B-22] |
| F-NET-007 | GEÇTİ | Kök yokken foto ekleme hata verir, kaynak durur | FONK F-NET-007 |
| F-NET-008 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-NET-009 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |

### 4.31 Klavye ve diyalog güvenliği (F-KEY)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-KEY-001 | HATA | UI: karar (Kabul, Rework, MRB, Scrap), ölçüm atla, geri al, void diyaloglarında Enter kayıt yapmadı; "Fotoğrafı kaldır" diyaloğunda Enter fotoğrafı kaldırdı (B-24) | shots\11-after-decision-keys.png, shots\23-remove-photo-dialog.png |
| F-KEY-002 | GEÇTİ | UI: Esc: karar, geri al, void, ölçüm atla diyaloglarında vazgeçer; "Rework şimdi" → "Sonra" gibi davranır (foto kaldır diyaloğunda Esc ayrıca denenmedi) | shots\13-rework-now-dialog.png |
| F-KEY-003 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-KEY-004 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-KEY-005 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-KEY-006 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-KEY-007 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |

### 4.32 Uçtan uca senaryolar (F-E2E)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-E2E-001 | GEÇTİ | UNIT zinciri geçti (UI: oturum→kusur→ölçüm→foto→Kabul→Tamamla→Rapor→Dashboard doğrula yürütüldü; editörde işaret çizme ve Admin "Son rapor" atlandı) | FONK F-E2E-001; shots\02..08, 31 |
| F-E2E-002 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: Scenario1_SameDefectThenConforms, VmReinspectSameHighMetalGoneKeepsDepth |
| F-E2E-003 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: Scenario2_NewDefectChild, MultipleNewDefectsLinkAndGate |
| F-E2E-004 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: MixedRegionOutcomes, VmReinspectPerRegionOutcomes |
| F-E2E-005 | GEÇTİ | UNIT: MRB bekleyen tamamlama engellenir, MRB red sonrası otomatik hurda, Yeniden aç yok | FONK F-E2E-005 |
| F-E2E-006 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: UndoChildLinkVoidsChild, VmUndoChildTransferVoidsChild |
| F-E2E-007 | GEÇTİ | UNIT: Void sonrası Dent #2, tamamlama geçer | FONK F-E2E-007 |
| F-E2E-008 | GEÇTİ | UNIT: tedarikçide açık sayılır, sonra kapanır | FONK F-E2E-008 |
| F-E2E-009 | GEÇTİ | UNIT: Repair→Reinspect→Giderildi ve Repair→Kabul | FONK F-E2E-009 |
| F-E2E-010 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ChildInheritsAffectedParts, CsvHasAffectedPartsColumn |
| F-E2E-011 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmUnitFollowsSettingsPerDefect, ReportShowsInchValues |
| F-E2E-012 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmCustomFieldShownSavedAndInSentence, VmReinspectMergesCustomField |
| F-E2E-013 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-E2E-014 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: VmInspectorFromWindowsUser |
| F-E2E-015 | ATLANDI | UI senaryosu yürütülmedi (zaman / kapsam sınırı). | - |
| F-E2E-016 | GEÇTİ | UNIT: Kabul→Yeniden aç→Rework→Giderildi | FONK F-E2E-016 |

### 4.33 Launcher (F-LNC)

| ID | Sonuç | Gözlem | Kanıt |
|---|---|---|---|
| F-LNC-001 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ProgramLayoutResolvesVersionAndArch |
| F-LNC-002 | GEÇTİ | UNIT kısmı: "Sürüm dosyası bulunamadı:" (UI MessageBox yürütülmedi) | FONK F-LNC-002 |
| F-LNC-003 | GEÇTİ | Geçersiz surum.txt | FONK F-LNC-003 |
| F-LNC-004 | GEÇTİ | Sürüm klasörü yok | FONK F-LNC-004 |
| F-LNC-005 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: ProgramLayoutResolvesVersionAndArch |
| F-LNC-006 | GEÇTİ | İlk satır kırpılır, BOM sorunsuz | FONK F-LNC-006 |
| F-LNC-007 | GEÇTİ | Mevcut otomatik test kapsıyor; koşucu yeşil (312/312). Ayrıca doğrulanmadı. | mevcut: InstallSettingsFoundInParentFolders |
| F-LNC-008 | GEÇTİ | Ayar dosyası surum.txt'nin yanına yazılır | FONK F-LNC-008 |
| F-LNC-009 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |
| F-LNC-010 | MANUEL | İnsan / donanım / gerçek ağ gerekir; yürütülmedi (plan Bölüm 7.4). | - |


## 4. Doğrulanan hatalar

Öncelik: P1 = veri kaybı / yanlış kayıt / çökme, P2 = yanlış davranış, P3 = kozmetik. Satır numaraları HEAD `f9b6bfa` içindir. Tüm UNIT kanıtları `dotnet run --project tests/MuayeneKayit.Tests -- --supheli` ile yeniden üretilir.

### B-01 (P1) Ölçüm formu yuvarlanmış metni geri okuyup kayıtlı değeri değiştirir
- **Tekrar:** mm kusurda D `0.125` kaydet; yalnızca Not'u değiştirip kutudan çık (otomatik kayıt). D `0.13` olur, düzeltme kaydı yazılır. Açı `12.345` → `12.35`, inç `0.123456` → `0.1235`. Karar öncesi otomatik kayıt Rework Snapshot'ına da `0.13` taşır (`FONK F-MEAS-018 [B-01]`).
- **Kod:** `InspectionViewModel.cs:496-500` (form yuvarlanmış metinle doldurulur), `:685-702` (AutoSave), `:704-715` (ReadForm), `MuayeneKayit.Core/Domain/Units.cs:11-19` (`0.##` / `0.####`), `DefectPresentation.cs:141-146`.
- **Düzeltme önerisi:** `ReadForm` içinde kutu metni, kayıtlı değerin biçimlenmiş hâline eşitse alanı **kayıtlı ham değerle** bırak (yalnızca kullanıcı gerçekten metni değiştirdiyse yeniden ayrıştır); ya da kutuda gidiş-dönüş güvenli (`"R"`) biçim kullan.

### B-02 (P1) `JsonException` / `FormatException` hiçbir catch listesinde yok
- **Tekrar (UNIT, ViewModel'den dışarı kaçış):** bozuk / yarım / 0 bayt `session.json` ile `OpenSession`; bozuk `defects\2.json` ile oturum açma, `Dashboard.Refresh`, `IndexBuilder.Rebuild` (tek bozuk dosya tüm listeyi düşürür); `admin.json` çöp ya da `Salt="%%%"` ile `Login()`; `_audit.log` son satırı yarım JSON ile `AddDefect` (kusur dosyası yazılır, denetim satırı yazılamaz) (`FONK F-CFG-008..011`). Uygulamanın çökmesi UI'da denenmedi.
- **Kod:** `SessionStore.cs:309-317` (`ReadJson`), `InspectionViewModel.cs:797`, `InspectionViewModel.Dialogs.cs:305`, `DashboardViewModel.cs:211`, `LoginViewModel.cs:97`, `SharedConfig.cs:326-340` (`VerifyPin`, `FromBase64String`), `AuditLog.cs:110-121` (`LastHash`) ve `:27-39` (`Append`). `App.xaml.cs`: `UnhandledException` işleyicisi yok.
- **Düzeltme önerisi:** `ReadJson` içinde `JsonException`'ı `InvalidDataException`'a çevir; `VerifyPin`'de `FormatException`'ı yakala; `AuditLog.LastHash/Append` bozuk son satırı atlayıp ya da kontrollü hata vererek çalışsın, denetim kaydı kusur yazılmadan önce alınsın; `App`'e `UnhandledException` işleyicisi ekle.

### B-03 (P1) Eski ekrandaki kullanıcı başkasının ölçümünü sessizce geri alır
- **Tekrar:** A ve B aynı kusurda (D 0.1). A D=0.5 kaydeder. B (yenilenmemiş ekran) yalnızca Not yazıp kutudan çıkar: D `0.1`'e döner (`FONK F-CONC-004 [B-03]`).
- **Kod:** `InspectionViewModel.cs:687-697` (AutoSave belleğteki `Current` ile karşılaştırır), `:718-745` (`StoreForm` tüm formu yazar).
- **Düzeltme önerisi:** form açıldığındaki anlık görüntü ile kutu değerini karşılaştırıp yalnızca **değişen** alanları diskteki güncel kayda uygula; ya da kayıtta sürüm / zaman damgası denetimi ve çakışmada uyarı.

### B-04 (P1) Tamamlanmış muayeneye kusur eklenebilir, durum "Completed" kalır
- **Tekrar:** boş oturumda Tamamla → Kusur ekle. UI'da doğrulandı: alt satır "Tamamlandı", 2 kusur 1 açık; Dashboard kartı "Tamamlandı" + açık kusurlar (`shots\08-B04-completed-with-open-defect.png`, `shots\30-dashboard.png`). Eşzamanlı sürüm: A tamamlar, B kusur ekler (`FONK F-COMP-004`, `F-CONC-006`).
- **Kod:** `InspectionViewModel.cs:289-307` (`AddDefect`), `:765-784` (`Complete`), `SessionStore.cs:53-58` (`SaveSession` kilitsiz).
- **Düzeltme önerisi:** kusur ekleme / karar sırasında oturum `Completed` ise ya reddet ya da durumu kilit altında `Open`'a çevir; `SaveSession` ve kusur ekleme aynı `session.lock` altında olsun.

### B-05 (P1) Not için uzunluk sınırı yok, yazılan dosya sonra okunamaz
- **Tekrar:** 6 000 000 karakterlik not kaydet: 5 MB'ı aşan `defects\1.json` yazılır; `LoadDefects` `InvalidDataException` verir (oturum açılamaz hâle gelir). Kaydın kendi bildirimi de `Refresh` hatasıdır (`FONK F-CFG-013 [B-05]`).
- **Kod:** `SessionStore.cs:13` ve `:309-312` (sınır yalnızca okumada), `InspectionViewModel.cs:714` (not sınırsız).
- **Düzeltme önerisi:** Not uzunluğunu sınırla (ör. 4 000 karakter) ve `WriteDefect` içinde yazmadan önce boyut denetimi yap.

### B-06 (P1, UI) Rework sonrası muayene diyaloğu geçersiz sayıyı sessizce boş sayar
- **Tekrar (UI):** #2 rework'te → "Aynı kusur hâlâ var" → Derinlik `abc`, Genişlik `0,7` → "Kaydet" açılır; kayıt: D `0.4` aynı kalır, W `0.7` yazılır, hata yok (`shots\17-B06-abc-and-W.png`). Yalnız `abc` iken Kaydet kapalı.
- **Kod:** `MuayeneKayit.Inspector/Dialogs/WinUiDialogService.cs:386-402` (`ReadValues`, `Try`), `:450-454`.
- **Düzeltme önerisi:** ayrıştırılamayan metinde alanı hata durumuna al ve `CanSave`'i kapat (ya da `ReinspectInput`'a ayrıştırma hatası taşı).

### B-08 (P2) "_" ile başlayan parça no
- **Tekrar:** `_X` ile oturum açılır ama Index / Dashboard / Admin Kayıtlar'da görünmez; `_config` parça no'su `root\_config\OP40\SN001` klasörünü oluşturur (`FONK F-SESS-008`).
- **Kod:** `SafePath.cs:12-14` (desen `_`'ı başa izin verir), `IndexBuilder.cs:37`, `InspectionViewModel.Photos.cs:289`.
- **Düzeltme önerisi:** `SegmentPattern` ilk karakterde `_`'ı reddetsin.

### B-09 (P2) Admin kök yolu göreli olabilir
- **Tekrar:** `RootBox` = `rel` ya da `.` kaydedilir (`FONK F-ADM-SET-005`).
- **Kod:** `Presentation/Admin/SettingsViewModel.cs:63-73`.
- **Düzeltme önerisi:** `Path.IsPathRooted` değilse reddet.

### B-10 (P2) Fotoğraf adı çakışması
- **Tekrar:** `1_photo_01` diskte yokken `1_photo_02` varsa yeni fotoğraf `IOException: ... already exists` ile eklenemez (`FONK F-PHOTO-008`). Eşzamanlı eklemede 15/15 denemede biri hata alır (`FONK F-CONC-007`).
- **Kod:** `MuayeneKayit.Core/Storage/PhotoImporter.cs:32-36`.
- **Düzeltme önerisi:** sayaç yerine mevcut en büyük numara + 1; ad çakışırsa artırıp yeniden dene (`FileMode.CreateNew`); kusur kaydındaki `Photos` listesinden de numara al.

### B-11 (P2) Tip adı değişikliği sessizce kaybolur
- **Tekrar:** Kusur tipleri'nde Ad'ı değiştir, "Şablonu uygula"ya basmadan "Tümünü ortak alana kaydet": kaydedilen liste eski ad, ama "kaydedildi" mesajı (`FONK F-ADM-TYPE-016`).
- **Kod:** `Presentation/Admin/TypesViewModel.cs:318-336` (`Save` listeyi yazar, editörü değil).
- **Düzeltme önerisi:** `Save` öncesi editör ile seçili öğe farklıysa otomatik `Apply` yap ya da uyarı ver.

### B-12 (P2) Ek alan türü değişince kayıtlı değer yeniden yazılır
- **Tekrar:** uzunluk türünde H=0.3 kayıtlı; alan türü Metin yapılır; başka kutu otomatik kaydedilince H metin "0.3" (tür=Text) olur (`FONK F-CUST-009`).
- **Kod:** `InspectionViewModel.cs:568-602` (`ApplyTemplateFields`), `CustomFieldInput.cs:29-32`.
- **Düzeltme önerisi:** kayıtlı değerin türüyle kutu tanımının türü uyuşmuyorsa o alanı salt okunur / değiştirilmeden bırak.

### B-13 (P2) Ölçüm alanı olmayan tipte sıradaki adım "ölçümleri girin"
- **Tekrar:** alanı olmayan tipte kusur ekle: `CurrentStep=Measure`, Kaydet vurgulu (`FONK F-TAB-006`).
- **Kod:** `DefectPresentation.cs:111-122` (`CurrentStep`), `HasMeasurement`.
- **Düzeltme önerisi:** şablonda hiç ölçüm alanı yoksa `HasMeasurement` yerine "ölçüm gerekmiyor" kabul edip Fotoğraf adımına geç.

### B-14 (P2) Düzenleyici kaydı kapatılmış kusura yazılır
- **Tekrar:** düzenleyici açıkken kusur başka kullanıcıca Accept ile kapatılır; düzenleyici kaydı işareti kapalı kusura yazar (`FONK F-EDIT-020`).
- **Kod:** `InspectionViewModel.Photos.cs:163-185` (geri çağrı durumu denetlemez).
- **Düzeltme önerisi:** `UpdateDefect` içinde durum `Closed/Voided/Superseded/AwaitingReinspect` ise `InvalidOperationException` fırlat.

### B-15 (P3) İkinci Tamamla ikinci denetim satırı yazar
- **Tekrar:** iki kez Tamamla: 2 adet `session.complete` (`FONK F-COMP-005`).
- **Kod:** `InspectionViewModel.cs:765-784`.
- **Düzeltme önerisi:** `Status == Completed` ise bilgi mesajı ver ve yazma.

### B-16 (P3) PIN kilidi yalnızca bellekte
- **Tekrar:** 5 yanlış PIN sonrası yeni `LoginViewModel` ile doğru PIN girer, kilit yok (`FONK F-ADM-LOGIN-008`). UI'da yeniden başlatma denenmedi.
- **Kod:** `LoginViewModel.cs:17-18`, `:63`.
- **Düzeltme önerisi:** kilit zamanını `_config` altında (ya da yerel dosyada) sakla.

### B-19 (P2, ürün kararı) `admin.json` silinirse herkes yeni PIN belirleyebilir
- **Tekrar:** `admin.json` sil → "İlk kurulum" ekranı (`FONK F-ADM-LOGIN-013`, ayrıca mevcut `S-PIN-007`).
- **Kod:** `LoginViewModel.cs:44-56` (`SafeHasPin`), `SharedConfig.cs:306`.
- **Düzeltme önerisi:** PIN dosyası yokken ilk kurulumu yalnızca `_config` klasörü de yoksa sun; aksi halde denetim kaydına yaz ve ek onay iste.

### B-20 (P2) Eşzamanlı rapor aynı sürüm numarasını kullanır
- **Tekrar:** iki VM aynı anda rapor: 25/25 denemede aynı `Rapor_vN` (`FONK F-CONC-013`).
- **Kod:** `InspectionViewModel.Photos.cs:216` (`LatestVersion + 1`, kilitsiz).
- **Düzeltme önerisi:** sürüm numarası ve HTML dosyası oturum kilidi altında `FileMode.CreateNew` ile ayrılsın.

### B-21 (P2) Tek geçersiz ek alan tüm listeyi düşürür
- **Tekrar:** 3 alandan biri yerleşik kısaltmalı (`D`) → 0/2 geçerli alan yüklenir (`FONK F-CFG-005`).
- **Kod:** `SharedConfig.cs:143` (`ValidateCustomFields(list).Count == 0 ? list : boş`).
- **Düzeltme önerisi:** geçersiz alanları ayıklayıp geçerlileri döndür, ayıklananı denetime yaz.

### B-22 (P1) Kök erişilemezken yeni oturum ve admin ilk kurulumu kökü yeniden oluşturur
- **Tekrar:** kök klasör yeniden adlandırılır → yeni P/O/S ile oturum açılır (Success), kök yeniden oluşturulur (`FONK F-NET-003`); admin "İlk kurulum" ekranı gelir, PIN belirlenince kök yeniden oluşur ve giriş yapılır (`FONK F-NET-006`).
- **Kod:** `SessionStore.cs:42` (`OpenOrCreate`), `SharedConfig.cs:309-324` (`SetPin` → `CreateDirectory(Folder(root))`), `LoginViewModel.cs:44-56`.
- **Düzeltme önerisi:** `OpenOrCreate` yalnızca `Root` zaten varsa oluştursun (kök yoksa "Ortak alana erişilemiyor"); `SafeHasPin` kök yoksa `IOException` ile hata gösterip ilk kurulumu sunmasın.

### B-23 (P1, YENİ) Kök kaybında kusur ekleme / rapor da sessizce yeni boş kökü oluşturur
- **Tekrar:** oturum açıkken kök klasör yeniden adlandırılır → "Kusur ekle": başarı mesajı, `kök\PD170\OP40\SN001\defects\1.json` yeni (boş) kökte yazılır; rapor da aynı şekilde yeni kökte üretilir. Beklenen: "dizin bulunamadı" hatası, F-NET-001/004 (`FONK F-NET-001`, `F-NET-004`). F-NET-002 (otomatik kayıt) hata verir ama yan etki olarak boş klasör ağacı yeniden oluşur.
- **Kod:** `MuayeneKayit.Core/Storage/FileLock.cs:20` (`Directory.CreateDirectory(directory)`), `InspectionViewModel.Photos.cs:214` (`CreateDirectory(reportsDir)`), `SessionStore.cs:300`.
- **Düzeltme önerisi:** `FileLock.Acquire` klasörü oluşturmasın (yoksa `DirectoryNotFoundException` → anlaşılır hata); rapor klasörü yalnızca oturum klasörü varsa oluşturulsun. B-22 ile aynı koruma (kök varlık denetimi) tek yerde toplanabilir.

### B-24 (P1, YENİ, UI) "Fotoğrafı kaldır" diyaloğunda Enter fotoğrafı kaldırır
- **Tekrar (UI):** fotoğraflı açık kusurda çöp kutusu düğmesi → diyalog açıldıktan 2,5 sn sonra `get-focused` = `Kaldır` (birincil) düğmesi; Enter → fotoğraf kaldırıldı (`photos=0`, denetim `defect.photo.remove`). F-PHOTO-011 beklentisi (odak "Vazgeç"te, Enter kaldırmaz) sağlanmıyor. İçinde metin kutusu olan diyaloglarda (karar, geri al, void) sorun yok; "Ölçüm girilmedi" diyaloğu güvenli düğmeyi varsayılan yapıyor (sorun yok). Ekran görüntüsü: `shots\23-remove-photo-dialog.png`.
- **Kod:** `MuayeneKayit.Inspector/Dialogs/WinUiDialogService.cs:48-53` (`Opened` + `DispatcherQueue.TryEnqueue(Low)` ile odak kaydırma; `FocusManager.GetFocusedElement` bu anda henüz `PrimaryButton` döndürmüyor ya da odak sonradan geri alınıyor).
- **Düzeltme önerisi:** güvenli düğmeyi doğrudan varsayılan yap: `DefaultButton = ContentDialogButton.Close` (içinde giriş kutusu olmayan diyaloglar için), ya da `Loaded`'dan sonra koşulsuz `CloseButton.Focus(Programmatic)` ve gecikmeli yeniden deneme. Aynı düzeltme "Rework şimdi mi?" diyaloğu için de uygulanabilir.

### Küçük bulgular
- **N-1 (P3)** `UndoButton` metni "Son karar: Accept (…)" ve geçmiş cümlesi "After rework: same defect continues" gibi **İngilizce etiketler** gösteriyor (plan "Kabul" bekliyor); Türkçe arayüzde karışık dil. Kod: `DefectSentence.DecisionLabel`, `InspectionViewModel.cs:667-669`.
- **N-2 (P3)** `defects\notlar.json` (`{}`) "#0" adlı kusur olarak listelenir (`FONK F-CFG-020`; mevcut `S-JSON-017` ile aynı). Kod: `SessionStore.cs:66` (`EnumerateFiles("*.json")`). Öneri: yalnızca `^\d+\.json$` oku.
- **N-3 (gözlem, doğrulanmadı)** Admin → Bakım sayfası açıkken UIA ağacı okuma (`winapp ui inspect`) uygulamanın UI iş parçacığını yaklaşık %100 CPU ile kilitledi (`Responding=False`, 1,5 dk boyunca düzelmedi, süreç kapatıldı). UIA istemcisi kaynaklı olabilir; ancak Denetim listesinin (ItemsControl / ListView) erişilebilirlik ağacında sonsuz döngü olasılığı ekran okuyucular için risktir. Tekrar edilmedi.

### Doğrulanamayan (yürütülmedi)
- **B-07** (FileLock `Thread.Sleep` ile UI iş parçacığında 15 sn'ye kadar bekler): UI testi yürütülmedi; `FileLock.cs:47` kod okumasıyla sync çağrı zincirinde UI iş parçacığında olduğu görülüyor. UNIT'te yalnızca "kilit bırakılana dek beklenir" doğrulandı (`FONK F-CONC-008`).
- **B-17** (kaydedilmemiş değişiklik uyarısı yok), **B-18** (Admin koyu tema kontrastı): yürütülmedi.

### B-01..B-22 içinde yanlış alarm
**Yok.** Yürütülen 19 bulgunun (B-01..B-06, B-08..B-16, B-19..B-22) tümü doğrulandı. B-05'in plandaki tarifine tek ek: ihlal yazımda değil, yazılan dosya okunamaz hâle geldiğinde ortaya çıkıyor (kayıt anında `Refresh` hatası bildirimi görünür). B-19 bir hata değil ürün kararıdır, plan da öyle tanımlıyor. B-20 ve B-10 (eşzamanlı) planın tahmininden daha kolay tetikleniyor (25/25, 15/15).

## 5. Gözlemler (hata değil)

- F-MEAS-001: UIA ile `set-value` sonrası `SaveMeasButton` çağrılınca LostFocus otomatik kaydı önce yazdığı için bildirim "#1 ölçümleri kayıtlı." oldu; "güncellendi, düzeltme geçmişe yazıldı" metni #4'te görüldü. İşlev doğru.
- F-MEAS-005: Üst sınır yok, `1e308` kabul ediliyor (ürün kararı).
- Fotoğraf kaldırma sonrası dosya diskte kalır; sonraki fotoğraf adı dosya sayısına göre üretilir (`4_photo_02.png`, ilk dosya silinmediği için) — B-10 ile bağlantılı.
- Admin penceresi, ana pencere kapanmadan tek örnek; "Admin" `SelectionItemPattern` ile açılıyor.

## 6. Testlerin son çıktısı

Normal koşu (`dotnet run --project tests/MuayeneKayit.Tests`), son satırlar:

```
Açık bulgu: 49/49

Tümü geçti (312/312).
```

`--supheli` koşusu (`dotnet run --project tests/MuayeneKayit.Tests -- --supheli`): normal 312 test + 27 ŞÜPHELİ test; 27 kırmızı, son satırlar:

```
Açık bulgu: 49/49

27 test başarısız.
```

Kırmızı (hata doğrulandı) ŞÜPHELİ testler: F-MEAS-018 (B-01), F-CFG-008/009/010/011 (B-02), F-CONC-004 (B-03), F-COMP-004 ve F-CONC-006 (B-04), F-CFG-013 (B-05), F-SESS-008 (B-08), F-ADM-SET-005 (B-09), F-PHOTO-008 ve F-CONC-007 (B-10), F-ADM-TYPE-016 (B-11), F-CUST-009 (B-12), F-TAB-006 (B-13), F-EDIT-020 (B-14), F-COMP-005 (B-15), F-ADM-LOGIN-008 (B-16), F-ADM-LOGIN-013 (B-19), F-CONC-013 (B-20), F-CFG-005 (B-21), F-NET-003 ve F-NET-006 (B-22), F-NET-001 ve F-NET-004 (B-23, yeni), F-CFG-020 (N-2).

Güvenlik açık bulgu listesi (49 adet) bu çalışmada olduğu gibi bırakıldı.

## 7. Düzeltmeler

Bu bölüm, Bölüm 4'te doğrulanan hataların giderilmesini kaydeder (güvenlik düzeltmeleri korunmuştur). Eski ŞÜPHELİ testler ana listeye (`FonkFixedTests`) taşındı; `--supheli` listesi artık boştur. Ek doğrulama testleri `FonkDuzeltmeTests.cs` içindedir ("DÜZELTME ..." adlı testler).

| Hata | Değişiklik | Doğrulayan test |
|---|---|---|
| B-01 yuvarlanan ölçüm | `InspectionViewModel.ReadForm`: kutu metni, forma yüklenen kayıtlı değerin biçimlenmiş hâliyle aynıysa kayıtlı **ham** değer korunur (mm, inç, açı, renk, not, parçalar, ek alanlar); yalnızca değişen kutu ayrıştırılır. Rework Snapshot'ı bu sayede tam değeri taşır. | F-MEAS-018; DÜZELTME B-01 |
| B-03 eski ekran başkasının ölçümünü ezer | Forma yüklenen değerler `_loaded` içinde tutulur; yazma kilit altında `MeasurementEditor.Rebase` ile diskteki güncel kayıtla üç yönlü birleştirilir: dokunulmayan alan diskte kalır, değiştirilen alan yazılır, aynı alanı iki taraf değiştirdiyse yazılmaz, uyarı verilir ve form yenilenir. Kusur o arada kapanmışsa yazma reddedilir. | F-CONC-004; DÜZELTME B-03 (farklı alan, aynı alan çakışması, birleştirme kuralları) |
| B-04 tamamlanmış oturum | `SessionStore`: tüm yazma yolları (AddDefect, UpdateDefect(s), ApplyReinspect, ContinueAsChild, VoidDefect, UndoLastDecision) kilit altında oturum durumunu denetler ve "Bu muayene tamamlandı; ..." hatası verir; fotoğraf kopyalanmadan önce `RequireWritable`; yeni `CompleteSession` açık kusur denetimi ile durum yazımını tek kilitte yapar (Tamamla / Kusur ekle yarışı). | F-COMP-004, F-CONC-006; DÜZELTME B-04 (iki test) |
| B-08 "_" ile başlayan ad | `SafePath.ValidateSegment`: "_" ile başlayan parça / operasyon / seri açık mesajla reddedilir ("'_' ile başlayamaz"), ortadaki "_" serbest. | F-SESS-008; DÜZELTME B-08 |
| B-10 fotoğraf adı çakışması | `PhotoImporter.Import`: oturum kilidi altında mevcut dosyaların en büyük numarası + 1 (kayıtlı adlar da rezerve), `FileMode.CreateNew` ve çakışmada yeniden deneme. | F-PHOTO-008, F-CONC-007; DÜZELTME B-10 (boşluk + 24 paralel) |
| B-11 tip adı sessizce kaybolur | `TypesViewModel.Save` önce editördeki bekleyen değişikliği doğrulayarak uygular (geçersizse hata, hiçbir şey yazılmaz). | F-ADM-TYPE-016; DÜZELTME B-11 |
| B-12 alan türü değişimi | Admin: mevcut alanın **türü değiştirilemez** ("yeni bir alan oluşturun"); muayene tarafı: kayıtlı değerin türü şimdiki tanımdan farklıysa o kutu için kayıttaki tanım kullanılır. | F-CUST-009; DÜZELTME B-12 (iki test) |
| B-13 ölçüm alanı olmayan tip | `DefectPresentation.CurrentStep/NextStepFor` şablonda hiç (yerleşik ya da ek) alan yoksa Fotoğraf adımına geçer; ekleme mesajı "Fotoğraf çekin". | F-TAB-006; DÜZELTME B-13 |
| B-14 düzenleyici kapalı kusura yazar | Düzenleyici kaydı kilit altında kusur durumunu denetler, kapalı / void / devredilmiş ise `InvalidOperationException` ile reddeder (pencere "Kaydedilemedi: ..." gösterir); temiz kopya dosyası yalnızca kabul edilirse yazılır. | F-EDIT-020 |
| B-15 ikinci Tamamla | `Complete` zaten tamamlanmışsa bilgi mesajı verir, denetim satırı yazmaz (`CompleteSession` false döner). | F-COMP-005; DÜZELTME B-04/B-15 |
| B-19 ürün kararı | Davranış zaten güvenlik düzeltmesindeydi; test onu doğrular: uyarı + açık onay kutusu + `admin.pin.reset`; temiz kökte onaysız ilk kurulum. | F-ADM-LOGIN-013 (iki test) |
| B-20 aynı rapor sürümü | `ReportFiles.Reserve`: reports klasörü kilidi altında sıradaki sürüm ayrılır ve boş `Rapor_vN.html` yer tutucusu `CreateNew` ile oluşturulur; PDF yayımlanınca ya da hata olunca yer tutucu silinir (`Release`). | F-CONC-013; DÜZELTME B-20 (3 test) |
| B-21 tek geçersiz alan | `LoadCustomFields` yalnızca geçersiz kayıtları atlar. `LoadDefectTypes` da aynı kuralı izler (100'ü aşan ya da hiç geçerli tipi olmayan liste varsayılanlara döner; S-JSON-010 korunur). | F-CFG-005, F-CFG-004 |
| B-22 / B-23 kök yeniden oluşur | Yeni `RootGuard.Require`: `SessionStore.OpenOrCreate/SaveSession/kilitler`, `AuditLog.Append`, `IndexBuilder.Rebuild`, `SharedConfig` yazıcıları ve rapor kök yoksa "Ortak alana erişilemiyor: <yol>. Ağ bağlantısını kontrol edin." hatası verir. `FileLock.Acquire` klasör oluşturmaz. Admin girişi kök yokken ilk kurulum sunmaz, aynı mesajı gösterir. | F-NET-001/003/004/006; DÜZELTME B-22/B-23 (2 test, 3 test) |
| B-24 Enter fotoğrafı kaldırır | Yalnızca metin içeren onaylarda (`ConfirmRemovePhotoAsync`, `AskReinspectNowAsync`, Admin `WinUiConfirmService`) `DefaultButton = Close`; diğer diyaloglarda odak giriş kutusunda, "Ölçüm girilmedi" diyaloğunda varsayılan zaten güvenli düğme. | UI testi çalıştırılmadı (uygulama başlatılmadı); derleme ve kod incelemesi |
| B-06 geçersiz sayı sessizce boş | `ReinspectForm.ParseEntry` (Boş / Geçerli / Hatalı) kuralı; pencere hatalı kutuda satır içi "Sayı olmalı: ..." gösterir ve Kaydet'i kapatır. | DÜZELTME B-06 (kural testi); pencere UI'da denenmedi |
| N-1 İngilizce etiketler | `DefectPresentation.DecisionLabelTr`: "Son karar", geçmiş ve geri alma metinleri Türkçe; rapor etiketleri iki dilli kalır. | DÜZELTME N-1 |
| N-2 `notlar.json` "#0" | Güvenlik düzeltmesindeki `^[1-9][0-9]{0,8}\.json$` kuralı yeterli; ek testle sabitlendi. | F-CFG-020; DÜZELTME N-2 |
| N-3 Bakım sayfası takılması | Neden kesinleştirilemedi (uygulama başlatılmadı, tekrarlanmadı). Bulunan gerçek maliyet: sayfa açılırken `LoadAudit` denetim günlüğünün **tamamını** arayüz iş parçacığında satır satır okuyordu, satırlar sınırsız uzunlukta listeleniyordu. `AuditLog.ReadTail` artık dosyayı sondan bloklarla okur, liste satırları 400 karakterle sınırlandı. Kilit taraması (`StaleLocks`) yalnızca düğmeyle çalışır; sonsuz döngü / bağlama döngüsü bulunmadı. | DÜZELTME N-3 |

Mevcut testlerde yeni amaçlanan davranışa göre güncellenenler: F-ADM-LOGIN-014 (tur sayısı 5000 aralık dışı), F-ADM-LOGIN-003 (PIN 8-12 hane), F-ADM-SET-003 (boş / göreli kök mesajı), F-LNC-004 ve F-LNC-006 (sürüm "9.0.0" / "1.2.3"), F-CFG-004 (tek geçersiz tip atlanır), F-EDIT-020 (reddedilen kayıt `InvalidOperationException`), "Birim: alt kusur..." testi (kök artık örtük oluşmaz).

Son koşu: normal liste 432/432 geçti, `--supheli` 0 test (hepsi taşındı), açık güvenlik bulgusu 0; `dotnet build MuayeneKayit.sln -c Release -p:Platform=x64` 0 uyarı / 0 hata.
