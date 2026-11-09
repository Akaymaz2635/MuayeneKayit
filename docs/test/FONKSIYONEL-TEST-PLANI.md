# MuayeneKayit — Fonksiyonel Test Planı

- **Kapsam:** Inspector (muayene ekranı, Dashboard, Ayarlar, Kamera, Fotoğraf düzenleyici, tüm diyaloglar), Admin penceresi (Inspector içinden), Launcher, Core (alan modeli, JSON depo + dosya kilidi, denetim zinciri, rapor) ve Presentation (ViewModel'ler).
- **Kod tabanı:** `C:\Users\PC\Documents\JUNK\MuayeneKayit`, dal `main`, HEAD `17cf3bf` ("Admin'den tanımlanan ek ölçüm alanları ...").
- **Mevcut otomatik testler:** `dotnet run --project tests/MuayeneKayit.Tests` → 174 test, hepsi geçiyor. Bu plandaki "Mevcut test" sütunu o koşucudaki C# metot adını verir.
- **Plan tarihi:** 2026-11-09. Bu belge kaynak koda dokunmadan; XAML, code-behind, ViewModel ve Core okunarak hazırlandı.

---

## 0. GÜVENLİK KURALLARI (yürütücü bunlara harfiyen uyar; her şeyden önce gelir)

1. **UI testleri yalnızca Debug derlemesinin bir KOPYASI ile çalışır.** Kaynak: `src\MuayeneKayit.Inspector\bin\x64\Debug\net9.0-windows10.0.19041.0\win-x64\` (ARM64 makinede `bin\ARM64\Debug\net9.0-windows10.0.19041.0\win-arm64\`). Bu klasör olduğu gibi scratch klasöre kopyalanır (ör. `%TEMP%\MuayeneKayit-fonk-<guid>\app\`). Repo içindeki `bin` klasöründen asla doğrudan çalıştırılmaz.
2. **Kopyanın yanına kendi `MuayeneKayit.settings.json` dosyası konur**; `rootPath` taze, boş bir geçici klasörü gösterir (ör. `%TEMP%\MuayeneKayit-fonk-<guid>\root`). Uygulama başlatılmadan önce dosyanın var olduğu, geçerli JSON olduğu ve `rootPath`'in bu geçici klasör olduğu doğrulanır. **Ayar dosyası yoksa ya da bozuksa uygulama `%USERPROFILE%\MuayeneKayitDemo` klasörüne düşer** (`InstallSettings.LoadRootPath`). Bu yüzden uygulama asla ayar dosyası olmadan ya da bozuk ayar dosyasıyla başlatılmaz; "bozuk / eksik ayar dosyası" testleri (F-CFG-001..003) yalnızca [UNIT] olarak yapılır.
3. **Kullanıcının gerçek veri köküne asla dokunulmaz.** Gerçek UNC yolu, kullanıcının ortak alanı ve `%USERPROFILE%\MuayeneKayitDemo` açılmaz, okunmaz, yazılmaz, silinmez. Testler yalnızca bu plan için oluşturulan geçici klasörlerde çalışır.
4. **Admin PIN'i yalnızca test için üretilir** (rastgele 8 rakam), yalnızca geçici kökte "ilk kurulum" ekranında belirlenir ve scratch klasördeki `test-pin.txt` dosyasında tutulur. PIN değeri sohbete ya da rapora yazılmaz. Kullanıcının gerçek ortak alanında PIN girilmez, değiştirilmez.
5. **Kullanıcı kayıtları asla silinmez ya da düzenlenmez.** `src\` altında hiçbir dosya değiştirilmez. Yürütücü yalnızca `tests\MuayeneKayit.Tests\` altına yeni test dosyası ekler ve `Program.cs` test listesine satır ekler; sonuç raporu `docs\test\` altına yazılır.
6. **Yalnızca yürütücünün başlattığı uygulama örnekleri kapatılır.** Başlatılan her sürecin PID'i kaydedilir; kapatma yalnızca o PID'lerle yapılır. `msedge`, `explorer` gibi kullanıcı süreçleri asla öldürülmez. Rapor Edge'de, "Klasörü aç" Gezgin'de açılır: bu pencereler yalnızca başlıklarından (ör. `Rapor_v1.pdf`, geçici seri klasörünün adı) tanınıyorsa ve testin açtığı kesinse kapatılır; emin değilseniz açık bırakıp raporda belirtin.
7. **`%LOCALAPPDATA%\MuayeneKayit\` bu bilgisayardaki gerçek kullanıcı ayarlarıdır** (`user-settings.json` = birim/tema, `hidden-suggestions.json` = gizlenen öneriler, `camera.json` = son kamera). UI testlerinden önce bu üç dosya (varsa) scratch klasöre kopyalanır; test bitince aynı içerikle geri kopyalanır. Önceden olmayan bir dosya test sırasında oluştuysa silinmez; raporda kullanıcıya bildirilir.
8. **Uygulamayı açmadan önce kullanıcıdan onay alınır** (kullanıcının kalıcı tercihi: UI kontrolü için uygulamayı habersiz açma).
9. **Ağ kopması gerçek ağda simüle edilmez.** Ağ bağdaştırıcısı, VPN, paylaşım, `net use` / `subst` gibi sistem ayarlarına dokunulmaz. "Ağ yolu kaybı" testleri geçici kök klasörünün **yeniden adlandırılması** ile yapılır (F-NET). Gerçek UNC kopması yalnızca [MANUAL] olarak insan tarafından yapılır.
10. **Admin "Ortak alan" değişikliği** yalnızca ikinci bir geçici klasöre (`root2`) yapılır; test sonunda scratch kopyadaki `MuayeneKayit.settings.json` ilk geçici köke geri çevrilir.
11. **Kamera ve Windows gizlilik ayarları değiştirilmez.** Kamera testleri [MANUAL]'dir; donanım ve izinler insan tarafından ayarlanır.
12. Test görüntüleri (JPG / PNG / sahte / 25 MB üstü) scratch altındaki `fixtures\` klasöründe üretilir; internetten dosya indirilmez.
13. Herhangi bir adımda bu kurallardan birini çiğneme riski görülürse test atlanır ve raporda "ATLANDI (güvenlik)" olarak yazılır.

---

## 1. Ortam hazırlığı (yürütücü için)

```powershell
# 1) Taban: mevcut testler geçiyor mu?
dotnet run --project C:\Users\PC\Documents\JUNK\MuayeneKayit\tests\MuayeneKayit.Tests   # beklenen: "Tümü geçti (174/174)."

# 2) Debug derlemesi (kaynağa dokunmadan, yalnızca derleme)
dotnet build C:\Users\PC\Documents\JUNK\MuayeneKayit\src\MuayeneKayit.Inspector -c Debug -p:Platform=x64

# 3) Scratch yerleşimi
$S    = Join-Path $env:TEMP ("MuayeneKayit-fonk-" + [guid]::NewGuid().ToString("N"))
$App  = Join-Path $S "app"; $Root = Join-Path $S "root"; $Root2 = Join-Path $S "root2"
$Fx   = Join-Path $S "fixtures"; $Bk = Join-Path $S "backup"
New-Item -ItemType Directory -Force $App,$Root,$Root2,$Fx,$Bk | Out-Null
Copy-Item -Recurse "C:\Users\PC\Documents\JUNK\MuayeneKayit\src\MuayeneKayit.Inspector\bin\x64\Debug\net9.0-windows10.0.19041.0\win-x64\*" $App
@{ rootPath = $Root } | ConvertTo-Json | Out-File -Encoding utf8 (Join-Path $App "MuayeneKayit.settings.json")
Get-Content (Join-Path $App "MuayeneKayit.settings.json")   # rootPath = $Root olmalı; değilse DUR.

# 4) Kullanıcı ayarlarının yedeği (geri yükleme: aynı dosyaları geri kopyala; silme yok)
$Ud = Join-Path $env:LOCALAPPDATA "MuayeneKayit"
foreach ($f in "user-settings.json","hidden-suggestions.json","camera.json") { if (Test-Path "$Ud\$f") { Copy-Item "$Ud\$f" $Bk } }

# 5) Test PIN'i (sohbete yazma)
-join ((1..8) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }) | Out-File -Encoding ascii (Join-Path $S "test-pin.txt")

# 6) Fixture görüntüleri
Add-Type -AssemblyName System.Drawing
$b = New-Object System.Drawing.Bitmap 640,480; $g = [System.Drawing.Graphics]::FromImage($b)
$g.Clear([System.Drawing.Color]::DimGray); $g.FillRectangle([System.Drawing.Brushes]::Silver,160,120,320,240); $g.Dispose()
$b.Save("$Fx\ok.jpg",[System.Drawing.Imaging.ImageFormat]::Jpeg); $b.Save("$Fx\ok.png",[System.Drawing.Imaging.ImageFormat]::Png)
$b.Save("$Fx\ok.bmp",[System.Drawing.Imaging.ImageFormat]::Bmp); $b.Dispose()
"bu bir resim degil" | Out-File -Encoding ascii "$Fx\fake.jpg"
$fs = [IO.File]::Create("$Fx\big.jpg");     $fs.Write([byte[]](0xFF,0xD8,0xFF),0,3); $fs.SetLength(25MB + 1); $fs.Close()
$fs = [IO.File]::Create("$Fx\exact25.jpg"); $fs.Write([byte[]](0xFF,0xD8,0xFF),0,3); $fs.SetLength(25MB);     $fs.Close()

# 7) Başlatma (kullanıcı onayından sonra) ve PID kaydı
$p = Start-Process -FilePath (Join-Path $App "MuayeneKayit.Inspector.exe") -WorkingDirectory $App -PassThru
$p.Id | Out-File -Append (Join-Path $S "pids.txt")
```

Bitişte: yalnızca `pids.txt` içindeki süreçleri kapat (`Stop-Process -Id <pid>`), `backup\` içindeki dosyaları `%LOCALAPPDATA%\MuayeneKayit\` altına geri kopyala, scratch klasörü kanıt olarak yerinde bırak ve yolunu raporla.

---

## 2. Gösterim

| Alan | Anlamı |
|---|---|
| **ID** | `F-<ALAN>-<NNN>`. Alanlar: SESS (oturum), DEF (kusur listesi), MEAS (ölçüm), CUST (ek ölçüm, muayene tarafı), PART (etkilenen parçalar), HM (high metal), TAB (adımlar / sekmeler), PHOTO, CAM, EDIT, SM (durum makinesi), DEC (karar diyaloğu), RWK (rework sonrası muayene), UNDO, VOID, COMP (tamamlama), RPT (rapor), DASH, SET (birim / tema), ID (Windows kimliği), ADM-LOGIN / ADM-REC / ADM-TYPE / ADM-FIELD / ADM-TXT / ADM-MNT / ADM-SET (admin), CONC (eşzamanlılık), CFG (bozuk / eksik dosyalar), NET (ağ yolu kaybı), KEY (klavye ve diyalog güvenliği), E2E (uçtan uca), LNC (launcher). |
| **Ö** | Öncelik. **P1** = veri kaybı / yanlış kayıt / çökme. **P2** = yanlış davranış. **P3** = kozmetik / kullanılabilirlik. |
| **Yöntem** | **[UNIT]** `tests/MuayeneKayit.Tests`'e ViewModel / Core üzerinden eklenebilir. **[UI]** gerçek uygulama UI Automation ile sürülür (AutomationId / Name verilir). **[MANUAL]** insan gerekir (kamera donanımı, gerçek ağ, görsel değerlendirme). Birden fazla yazılıysa ilki tercih edilen yoldur. |
| **Mevcut test** | Koşucuda bu durumu (tamamen ya da kısmen) kapsayan metot. "—" = yok. "kısmen:" = yalnızca bir parçası. |
| **ŞÜPHELİ** | Kod okumasında olası hata görüldü. "Beklenen" sütunu **doğru davranışı** yazar; mevcut kod büyük olasılıkla farklı davranır. Test kırmızı çıkarsa yürütücü kodu düzeltmez, Bölüm 6'daki bulgu numarasıyla (B-nn) raporlar. |

UI Automation notları:
- WinUI'de `AutomationProperties.AutomationId` verilmemiş öğelerde UIA AutomationId olarak **`x:Name`** döner (ör. `CaptureButton`, `PinBox`, `RootBox`). x:Name de yoksa öğe **Name** ile bulunur (düğmede `Content` metni, kutuda `Header` metni).
- ContentDialog düğmeleri: AutomationId `PrimaryButton` / `SecondaryButton` / `CloseButton`, Name = düğme metni ("Kararı kaydet", "Vazgeç" ...).
- Dinamik ek ölçüm kutuları: AutomationId `Custom_<alanAnahtarı>` (ör. `Custom_f1a2b3c4`). Admin şablonundaki ek alan onay kutuları: AutomationId = alan anahtarı. Anahtar `_config\measure_fields.json` dosyasından okunur.

---

## 3. Kontrol envanteri

### 3.1 Ana pencere (`MainWindow.xaml`) — başlık `MuayeneKayit {sürüm}`

| Bölge | Kontrol | AutomationId / Name | Tür | Bağlantı |
|---|---|---|---|---|
| Sol menü | Muayene | Name "Muayene" | NavigationViewItem | Tag `inspect` |
| Sol menü | Dashboard | Name "Dashboard" | NavigationViewItem | Tag `dash` → `Dash.Refresh()` |
| Sol menü | Ayarlar | `SettingsNavItem` | NavigationViewItem | Tag `settings` |
| Sol menü alt | Admin | `AdminNavItem` | NavigationViewItem (seçili olmaz) | AdminWindow (tek örnek, açıksa öne gelir) |
| Muayene | Durum çubuğu | `StatusInfoBar` | InfoBar (Success/Info 4 sn sonra kapanır; Warning/Error kalır) | `Vm.Notify` |
| Giriş | Parça no | `PartBox` | AutoSuggestBox | `Vm.PartNumber`, öneri, Enter |
| Giriş | Operasyon | `OpBox` | AutoSuggestBox | `Vm.OperationNumber` |
| Giriş | Seri no | `SerialBox` | AutoSuggestBox | `Vm.SerialNumber` |
| Giriş | Öneri satırındaki "×" | Name "Öneriyi gizle" | Button (şablonda) | `Vm.HideSuggestion` |
| Giriş | Inspector | `InspectorBox` | TextBox (Windows kimliğinde salt okunur) | `Vm.InspectorName`, Enter |
| Giriş | Create Inspection | `OpenSessionButton` | Button | `Vm.OpenSession()` |
| Oturum başlığı | Başka muayene | `ChangeSessionButton` | Button | `Vm.ChangeSession()` |
| Oturum başlığı | Rapor (PDF) | `ReportButton` | Button (`CanCreateReport`) | `Vm.CreateReportAsync()` |
| Oturum başlığı | Muayeneyi tamamla | `CompleteButton` | Button | `Vm.Complete()` |
| Kusur listesi | Kusur ekle | `AddDefectButton` | Button + Flyout | — |
| Flyout | Kusur tipi | `DefectTypeBox` | ComboBox (ilk tip seçili) | `Vm.TypeNames` |
| Flyout | Ekle | `ConfirmAddDefectButton` | Button | `Vm.AddDefect(type)` |
| Kusur listesi | Liste | `DefectList` | ListView (tekli seçim) | `Vm.Defects`, `Vm.SelectedDefect` |
| Detay | Adım sekmeleri | `DetailTabs`; öğeler x:Name `MeasureTabItem`, `PhotoTabItem`, `DecisionTabItem`, `HistoryTabItem` | SelectorBar | `Vm.SelectedStep`, `GoToPhotoStepAsync` |
| Ölçüm | Rework sonrası muayene sonucunu gir | `ReinspectButton` | Button (yalnızca bekleme durumunda) | `Vm.ReinspectAsync()` |
| Ölçüm | Derinlik / Genişlik / Uzunluk / Çap | `DepthBox`, `WidthBox`, `LengthBox`, `RadiusBox` | TextBox (başlıkta birim) | `*Text`; LostFocus = `AutoSave` |
| Ölçüm | Açı (°) | `AngleBox` | TextBox | `AngleText` |
| Ölçüm | Renk | `ColorBox` | TextBox | `ColorText` |
| Ölçüm | Ek ölçüm kutuları | `Custom_<key>` | TextBox (ItemsRepeater) | `Vm.CustomInputs` |
| Ölçüm | Etkilenen parçalar (lot; boş = tümü) | `AffectedPartsBox` | TextBox (placeholder "örn. 1-5, 8") | `AffectedPartsText` |
| Ölçüm | Not | `NotesBox` | TextBox (çok satır) | `NotesText` |
| Ölçüm | High metal mevcut | `HighMetalBox` | CheckBox (değişince AutoSave) | `HighMetal` |
| Ölçüm | Ölçümleri kaydet | `SaveMeasButton` | Button | `Vm.SaveMeasurements()` |
| Ölçüm | Void (yanlış giriş) | `VoidButton` | Button (`CanVoid`) | `Vm.VoidAsync()` |
| Ölçüm | Otomatik kayıt metni | `AutoSaveText` | TextBlock | `AutoSaveText` |
| Fotoğraf | Kamera ile çek | `CameraButton` | Button | `Vm.OpenCamera()` |
| Fotoğraf | Dosyadan ekle | `AddPhotoButton` | Button | `Vm.AddPhotoFromFileAsync()` |
| Fotoğraf kartı | İşaretle / Görüntüle | Name "İşaretle" ya da "Görüntüle" | Button | `Vm.OpenEditor(file)` |
| Fotoğraf kartı | Kaldır (çöp kutusu) | Name "`<dosya>` fotoğrafını kaldır" | Button | `Vm.RemovePhotoAsync(file)` |
| Disposition | Karar | `DecisionCombo` (placeholder "Karar seçin") | ComboBox (seçince diyalog; sonra -1'e döner) | `Vm.DecisionOptions`, `GiveDecisionAsync` |
| Disposition | Son kararı geri al | `UndoButton` | Button (`CanUndo`) | `Vm.UndoLastAsync()` |
| Geçmiş | Geçmiş listesi | HistoryTab içindeki ListView | ListView | `Vm.History` |
| Dashboard | Yenile / Filtreleri temizle / Denetim kaydını doğrula | Name = metin | Button | `Dash.Refresh / ClearFilters / VerifyAudit` |
| Dashboard | Parça no / Operasyon / Seri no | x:Name `FilterPartBox`, `FilterOpBox`, `SerialFilterBox` | TextBox | `Dash.*Filter` |
| Dashboard | Hata tipi / Durum | x:Name `FilterTypeBox`, `FilterStateBox` | ComboBox | `TypeFilter`, `StateFilter` (6 seçenek) |
| Dashboard | Denetim sonucu | x:Name `DashInfo` | InfoBar | `Dash.IsAuditOpen` |
| Dashboard | Kart "Aç" | Name "Aç" | Button | `Dash.Open` → muayene ekranında açar |
| Ayarlar | Ölçü birimi | `UnitRadio` ("mm (milimetre)", "inch (inç)") | RadioButtons | `Settings.UnitIndex` |
| Ayarlar | Tema | `ThemeRadio` ("Açık", "Koyu") | RadioButtons | `Settings.ThemeIndex` |

### 3.2 Diyaloglar (`Dialogs/WinUiDialogService.cs`) — hepsi ana pencere kökünde ContentDialog

| Diyalog (başlık) | İçerik | Onay / iptal | Onay etkinlik kuralı |
|---|---|---|---|
| Karar — başlık = karar adı ("Kabul (SPA)", "Rework → Reinspect", "Submit MRB", "MRB kabul (accept as is)", "MRB red (otomatik hurda)", "Repair", "Reinspect (CTP)", "Return to vendor", "Scrap", "Yeniden aç") | Durum satırı; açıklama (Admin'den); [bölge onay kutuları Name "Bölge 1.1"... (2+ aktif bölge, Void/Reopen hariç)]; [hedef operasyon (Rework/Reinspect, varsayılan oturum op'u)]; [referans / MRB no (Accept, SubmitMrb, MrbAccepted, MrbRejected, Repair, Scrap)]; [Rework kapsamı (High metal'li Rework)]; Not (Reopen'da zorunlu) | "Kararı kaydet" / "Vazgeç"; varsayılan düğme yok | `DecisionForm.CanSave` |
| "Rework şimdi yapıldı mı?" / "Kontrol şimdi yapılıyor mu?" | Açıklama | "Sonucu şimdi gir" / "Sonra" | her zaman |
| "#N Tip: rework sonrası muayene" / "... yeniden kontrol sonucu" | [HM-only kapsam satırı]; önceki ölçümler; sonuç RadioButtons (3 seçenek) ya da bölge başına (Giderildi / Devam ediyor / Yeni hata, Name "Bölge 1.1 sonucu"); [HM durumu (zorunlu)]; ölçüm kutuları (placeholder "önce: X"); Renk; ek ölçümler; canlı değişim satırı; yeni hata listesi (combo "Tip seçin" + "×" Name "Satırı sil", "+ Hata ekle" / "+ Bu bölgeye hata ekle"); özet; Not | "Kaydet" / "Vazgeç"; başta kapalı | `ReinspectForm.CanSave` / `CanSaveRegions` |
| "Son kararı geri al" | Karar bilgisi; [child void uyarısı]; gerekçe (zorunlu) | "Geri al" / "Vazgeç"; başta kapalı | gerekçe boş değil |
| "Kusuru void et" | Uyarı; gerekçe (zorunlu) | "Void et" / "Vazgeç"; başta kapalı | gerekçe boş değil |
| "Fotoğrafı kaldır" | Açıklama | "Kaldır" / "Vazgeç"; odak onaydaysa Vazgeç'e taşınır | her zaman |
| "Ölçüm girilmedi" | Uyarı | "Ölçüme dön" (Primary, varsayılan) / "Yine de fotoğrafa geç" (Secondary) | — |
| Admin onayları (`WinUiConfirmService`) | Başlık + mesaj | onay metni ("Sil", "Varsayılanlara dön", "Varsayılana döndür", "Kilidi sil") / "Vazgeç"; varsayılan yok | — |

### 3.3 Kamera penceresi (`CameraWindow.xaml`) — başlık `Kamera — <hedef>`, büyütülmüş, koyu tema

| Kontrol | AutomationId / Name | Not |
|---|---|---|
| Hedef metni | x:Name `TargetText` | "#1 Dent · PD170 / OP40 / SN001" |
| Kamera listesi | x:Name `DeviceBox` (placeholder "Kamera seçin") | ad + (harici / arka / ön / dahili) |
| Kameraları yenile | Name "Kameraları yenile" | |
| Çerçeve | x:Name `FrameToggle` ("Çerçeve açık" / "Çerçeve kapalı") | |
| Otomatik odak | x:Name `AutoFocusToggle` | yalnızca destekleniyorsa görünür |
| Çözünürlük | x:Name `ResolutionText` | |
| Önizleme | `PreviewArea` | dokun-odakla, çift dokun 1×/2×, tekerlek, sıkıştırma |
| Durum metni | x:Name `StatusText` | hata / yardım metni |
| Zoom | `Zoom1Button`, `Zoom2Button`, `Zoom4Button`, `ZoomText` | 1..5× |
| Çerçeve boyutu | x:Name `FrameSizeSlider` (0.05–0.9, adım 0.05), `FrameSizeText` | çerçeve kapalıyken devre dışı |
| Fotoğraf çek (Boşluk) | x:Name `CaptureButton`; **Space** | kamera hazır olana dek kapalı |
| Kaydet (Enter) | x:Name `SaveButton`; **Enter** | yalnızca inceleme modunda görünür |
| Tekrar çek (Esc) | x:Name `RetakeButton`; **Escape** | yalnızca inceleme modunda görünür |
| Kayıt bilgisi | x:Name `SavedText` | |

### 3.4 Fotoğraf düzenleyici (`PhotoEditorWindow.xaml`) — büyütülmüş

| Kontrol | AutomationId / Name | Not |
|---|---|---|
| Hedef | x:Name `TargetText` | |
| Kutu / Daire / Ok | x:Name `BoxTool` (varsayılan), `CircleTool`, `ArrowTool` | ToggleButton, karşılıklı dışlayan |
| Kusur no | x:Name `LabelBox` | void olmayan kusurlar "Kusur #N"; seçili işaretin numarasını da değiştirir |
| Son işareti geri al | Name "Son işareti geri al"; **Ctrl+Z** | |
| İşaretleri sil | Name "İşaretleri sil" | onaysız |
| Arka plan: parçayı çerçevele | x:Name `BgTool` | |
| Arka planı kaldır | x:Name `BgApplyButton` | çerçeve çizilince açılır |
| Orijinale dön | x:Name `BgRevertButton` | |
| Çizim yüzeyi | x:Name `Ink` | sürükle-çiz, seç, taşı, köşeden boyutlandır; **Delete** seçili işareti siler |
| Meşgul | x:Name `Busy` | ProgressRing |
| İpucu | x:Name `HintText` | |
| Vazgeç | Name "Vazgeç" | onaysız kapatır |
| Kaydet | x:Name `SaveButton` | görüntü yüklenince açılır |

### 3.5 Admin penceresi (`Admin/AdminWindow.xaml`) — başlık `MuayeneKayit Admin {sürüm}`, 1280×860

| Sayfa | Kontrol | AutomationId / Name | Bağlantı |
|---|---|---|---|
| Giriş | İpucu / hata / kök | x:Name `LoginHint`, `LoginError`, `LoginRoot` | `Login.Hint / Error / RootText` |
| Giriş | PIN | x:Name `PinBox` (MaxLength 12); Enter = giriş | `Login.Pin` |
| Giriş | PIN (tekrar) | x:Name `PinRepeatBox` (yalnızca ilk kurulum) | `Login.PinRepeat` |
| Giriş | "Giriş" / "PIN'i belirle ve gir" | x:Name `LoginButton` | `Login.Login()` |
| Menü | Kayıtlar / Kusur tipleri / Karar açıklamaları / Bakım / Ayarlar / Kilitle (alt) | Name = metin | `Shell.Navigate(tag)` |
| Tümü | Durum | x:Name `Status` | `Shell.Notify` |
| Kayıtlar | Yenile, CSV olarak dışa aktar | Name = metin | `Records.Refresh`, `ExportCsvAsync` |
| Kayıtlar | Parça no / Operasyon / Seri no | x:Name `FPart`, `FOp`, `FSerial` | süzgeçler |
| Kayıtlar | Hata tipi / Durum | x:Name `FType`, `FState` | süzgeçler |
| Kayıtlar | Kart düğmeleri | Name "Klasörü aç", "Son rapor" | `OpenFolder`, `OpenLatestReport` |
| Kusur tipleri | Liste | x:Name `TypeList` | `Types.Items / SelectedIndex` |
| Kusur tipleri | Yeni tip, Yukarı, Aşağı, Sil | Name = metin | `Add / MoveUp / MoveDown / DeleteAsync` (onaylı) |
| Kusur tipleri | Ad (MaxLength 40), Spec adı (MaxLength 60) | x:Name `TypeName`, `TypeSpec` | |
| Kusur tipleri | Derinlik / Genişlik / Uzunluk / Çap / Açı / Renk | x:Name `FieldDepth`, `FieldWidth`, `FieldLength`, `FieldRadius`, `FieldAngle`, `FieldColor` | |
| Kusur tipleri | Ek alan onay kutuları | AutomationId = alan anahtarı | `CustomToggles` |
| Kusur tipleri | Şablonu uygula, Tümünü ortak alana kaydet, Varsayılanlara dön | Name = metin | `Apply`, `Save`, `ResetAsync` (onaylı) |
| Kusur tipleri | Kirli metni | x:Name `TypesDirty` | |
| Ölçüm alanı | Liste | x:Name `FieldList` | `FieldItems` |
| Ölçüm alanı | Ad (MaxLength 30), Kısaltma (MaxLength 6), Tür | x:Name `FieldNameBox`, `FieldAbbrevBox`, `FieldKindBox` | 4 tür: uzunluk, açı, sayı, metin |
| Ölçüm alanı | Yeni alan ekle, Seçili alanı güncelle, Seçili alanı sil | x:Name `AddFieldButton`; Name = metin | `AddField`, `ApplyField`, `DeleteFieldAsync` (onaylı) |
| Karar açıklamaları | Ortak alana kaydet, Tümünü varsayılana döndür | Name = metin | `Texts.Save`, `ResetAllAsync` (onaylı) |
| Karar açıklamaları | Satır başına metin (MaxLength 600) + "Varsayılan" | x:Name `TextsList` içinde | `DecisionTextItem.Text / ResetOne` |
| Bakım | Index'i yeniden oluştur, Denetim zincirini doğrula, Kilitleri tara, Seçili kilidi sil | Name = metin | `Maintenance.*` (silme onaylı) |
| Bakım | Kilit listesi, Denetim listesi (son 300) | x:Name `LockList`, `AuditList` | |
| Ayarlar | Kök klasör, Kaydet ve yeniden bağlan | x:Name `RootBox`; Name = metin | `Settings.SaveRoot` |
| Ayarlar | Inspector kimliği | `WindowsUserSwitch` | `Settings.UseWindowsUser` (anında kaydedilir) |
| Ayarlar | Mevcut PIN / Yeni PIN (6-12 rakam) / Yeni PIN (tekrar), PIN'i değiştir | x:Name `OldPin`, `NewPin`, `NewPinRepeat`; Name = metin | `Settings.ChangePin` |

### 3.6 Klavye kısayolları (tam liste)

| Yer | Tuş | Etki |
|---|---|---|
| `InspectorBox` | Enter | Oturum aç |
| `PartBox` / `OpBox` / `SerialBox` | Enter | Öneri seçilmediyse oturum aç; seçildiyse yalnızca seç |
| Kamera | Space / Enter / Esc | Çek / Kaydet / Tekrar çek |
| Düzenleyici | Ctrl+Z / Delete | Son işareti geri al / seçili işareti sil |
| Admin `PinBox`, `PinRepeatBox` | Enter | Giriş |
| "Ölçüm girilmedi" | Enter / Esc | "Ölçüme dön" (güvenli seçenek) |
| Diğer ContentDialog'lar | Esc | Vazgeç; varsayılan düğme olmadığından Enter kaydetmez |

### 3.7 Durum makinesi (izinli kararlar — `DefectStateMachine.AllowedByState`)

| Durum | İzinli kararlar → yeni durum | Kaynak kısıtı |
|---|---|---|
| Open | Accept → Closed · Rework (hedef op zorunlu) → AwaitingReinspect · Reinspect → AwaitingReinspect · Repair → Repairing · Scrap → Closed (kesin) · SubmitMrb → MrbPending · ReturnToVendor → WaitingVendor · Void (yalnızca karar geçmişi yoksa, not zorunlu) → Voided | Void yalnızca Operator; Reinspect Engineer/Ctp; diğerleri Engineer |
| Repairing | Accept, Scrap, Reinspect, SubmitMrb | |
| AwaitingReinspect | AfterReworkConforms → Closed · AfterReworkSameDefect → Open · AfterReworkNewDefect (yeni kusur no zorunlu) → Superseded · Repair · SubmitMrb · Scrap | AfterRework* Engineer/Ctp |
| WaitingVendor | Reinspect, Scrap | |
| MrbPending | MrbAccepted → Closed · MrbRejected → Closed + System Scrap (kesin) | yalnızca Mrb |
| Closed | Reopen (not zorunlu; son karar Scrap ya da MrbRejected ise yasak) → Open | |
| Voided, Superseded | hiçbiri | |
| Her durum | Undo: son geri alınabilir karar (System ve Void hariç), gerekçe zorunlu; child'lı devir yalnızca `SessionStore.UndoLastDecision` ile | |

`Reworking` durumu enum'da var ama hiçbir karar onu üretmez (ölü durum).

---

## 4. Test durumları

### Ortak ön koşul kısaltmaları

| Kod | Anlamı |
|---|---|
| **K0** | [UI] Uygulama scratch kopyadan, boş geçici kökle açık; Muayene sekmesi; Inspector boş. |
| **K1** | K0 + oturum PD170 / OP40 / SN001 açık, Inspector "Test Inspector". |
| **K2** | K1 + #1 Dent eklenmiş, seçili, ölçümsüz (varsayılan şablon: D, W, L). |
| **K3** | K2 + D 0.12 kaydedilmiş + 1 fotoğraf (`fixtures\ok.jpg`) eklenmiş → Disposition adımı. |
| **U** | [UNIT] `ViewModelTests.With(c => ...)` bağlamı (gerçek `SessionStore` + geçici klasör, sahte diyalog/pencere/PDF); `c.Open()` ile PD170/OP40/SN001 açık. Diyalog cevapları `c.Dialogs.Decisions/Reinspects/...` kuyruklarına önceden konur. |
| **UA** | [UNIT] `AdminViewModelTests.With(c => ...)` + gerekiyorsa `c.SignIn()`. |
| **UC** | [UNIT] Core düzeyi: `TempRoot()` + `new SessionStore(root)`. |

### 4.1 Oturum girişi ve öneriler (F-SESS)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-SESS-001 | P2 | [UI] [UNIT] | K0 | `PartBox`=PD170, `OpBox`=OP40, `SerialBox`=SN001, `InspectorBox`="Test Inspector" → `OpenSessionButton` | `StatusInfoBar` Success "Yeni muayene oluşturuldu: PD170 / OP40 / SN001"; başlık "PD170  ·  OP40  ·  SN001", alt satır "Inspector: Test Inspector · 0 kusur, 0 açık · Devam ediyor"; `root\PD170\OP40\SN001\session.json` var; `_audit.log`'da `session.open`; `AddDefectButton` vurgulu | VmOpensNewSession |
| F-SESS-002 | P2 | [UNIT] [UI] | Oturum diskte var (Inspector "A") | Inspector "B" ile aynı P/O/S'yi aç | "Kayıtlı muayene açıldı: ..."; `session.json`'daki Inspector "A" kalır; alt satır ve sonraki kararlar/denetim "B" ile yazılır | kısmen: StoreOpenOrCreateKeepsExisting |
| F-SESS-003 | P1 | [UNIT] [UI] | K0 / U | Inspector boş ve yalnızca boşluk ("   ") ile oturum aç | Error "Inspector adı boş olamaz."; hiçbir klasör oluşmaz; oturum açılmaz | — |
| F-SESS-004 | P1 | [UNIT] [UI] | K0 / U | Sırayla Parça, Operasyon, Seri boş bırak | "Parça no boş olamaz." / "Operasyon no boş olamaz." / "Seri no boş olamaz."; kökte klasör oluşmaz | kısmen: SafePathRejectsTraversal |
| F-SESS-005 | P1 | [UNIT] | U | Parça no: `PD/170`, `PD\170`, `..`, `PD170.`, `.PD`, `a:b`, `PD*`, `<x>`, `C:\x`, `PD..170` | Her biri "Parça no geçersiz karakter içeriyor: '...'" hatası; kök dışına ya da alt klasöre hiçbir şey yazılmaz | kısmen: SafePathRejectsTraversal |
| F-SESS-006 | P2 | [UNIT] | U | 64 karakterlik parça no; 65 karakterlik parça no | 64 kabul; 65 reddedilir (geçersiz karakter mesajı) | — |
| F-SESS-007 | P2 | [UNIT] [UI] | U | `"  OP 40 "`, iç boşluk, Türkçe harf "ÇĞİÖŞÜ-ğ_1" | Baş/son boşluk kırpılır, "OP 40" klasörü; Türkçe harfler kabul | kısmen: SafePathAcceptsValid |
| F-SESS-008 | P2 | [UNIT] | U | Parça no `_X` ve `_config` ile oturum aç, kusur ekle; Dashboard `Refresh`; öneri listesi | **ŞÜPHELİ (B-08)**: "_" ile başlayan değer reddedilmeli (ya da index'te görünmeli). Bugünkü kod: kabul ediyor, klasör `root\_X\...` oluşuyor ama `IndexBuilder` ve öneriler "_" klasörlerini atladığından kayıt Dashboard'da / Admin Kayıtlar'da görünmez; `_config` altına muayene klasörü yazılır | — |
| F-SESS-009 | P2 | [UI] | K0, alanlar dolu | `InspectorBox`'ta Enter | Oturum açılır (düğmeye basmakla aynı) | — |
| F-SESS-010 | P2 | [UI] | K0, kökte PD170 ve PD200 klasörleri var | `PartBox`'a "PD" yaz, öneri listesinde ↓ ile PD200'ü seç, Enter; sonra listeyi kapatıp tekrar Enter | İlk Enter yalnızca PD200'ü seçer, oturum açmaz; ikinci Enter (öneri seçili değilken) oturumu açar | — |
| F-SESS-011 | P3 | [UI] [UNIT] | Kökte PD170, PD200, `_config` | `PartBox`'a odaklan; sonra "d2" yaz | Odakta PD170, PD200 listelenir (`_config` yok); "d2" yazınca yalnızca PD200 (büyük/küçük harf duyarsız "içerir") | VmSuggestionsListFoldersAndSkipUnderscore |
| F-SESS-012 | P3 | [UNIT] | U | Parça boş/geçersizken Operasyon önerisi; Parça+Op geçersizken Seri önerisi | Boş liste, hata bildirimi yok | VmSuggestionsListFoldersAndSkipUnderscore |
| F-SESS-013 | P3 | [UI] [UNIT] | K0, öneri listesi açık; LOCALAPPDATA yedeği alınmış | Bir önerinin "×" düğmesi (Name "Öneriyi gizle") | Öneri yalnızca o kutunun listesinden kalkar, kutudaki metin değişmez, liste açık kalır; gizlenen değerle oturum açılınca öneri geri gelir | VmHiddenSuggestionsPerBoxAndReturnOnUse |
| F-SESS-014 | P3 | [UNIT] | Kökte 60 parça klasörü | `Suggestions(Part, "")` | En fazla 50 öğe, alfabetik | — |
| F-SESS-015 | P2 | [UI] [UNIT] | K2 | `ChangeSessionButton` | Giriş ekranı görünür; `StatusInfoBar` kapanır; liste boşalır; P/O/S kutuları önceki değerleri korur | VmChangeSessionClears |
| F-SESS-016 | P1 | [UNIT] [UI] | K2 | `DepthBox`=0.3 yaz, kutudan çıkmadan `ChangeSessionButton` | 0.3 diske yazılmış olur (ChangeSession içindeki AutoSave); aynı oturum yeniden açılınca D 0.3 | — |
| F-SESS-017 | P1 | [UI] | K2 | `DepthBox`=0.4 yaz (odak kutuda), ana pencereyi kapat (X); yeniden başlat, aynı oturumu aç | D 0.4 kayıtlı (Closed olayındaki AutoSave); açık kamera/düzenleyici pencereleri de kapanmış | — |
| F-SESS-018 | P2 | [UNIT] | U (gerçek `FileAuditTrail` ile) | Inspector adı 500 karakter; ayrıca `Ali "x" \| ;` gibi özel karakterli | Oturum açılır; `AuditLog.Verify` sağlam (JSON kaçışı doğru) | — |
| F-SESS-019 | P3 | [UI] | K0 | Pencere başlığını oku | "MuayeneKayit {AppInfo.Version}" | — |

### 4.2 Kusur listesi (F-DEF)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-DEF-001 | P2 | [UI] [UNIT] | K1 | `AddDefectButton` → `DefectTypeBox` (varsayılan ilk tip "Dent") → `ConfirmAddDefectButton` | "Kusur #1 (Dent) eklendi. Ölçümleri girin."; flyout kapanır; #1 seçili; Ölçüm sekmesi; `SaveMeasButton` vurgulu; `defects\1.json` | VmAddDefectSelectsMeasure |
| F-DEF-002 | P2 | [UNIT] | U | `AddDefect(null)` | "Kusur tipi seçin." Error; `false` döner (flyout açık kalır); dosya yok | — |
| F-DEF-003 | P1 | [UNIT] | ViewModel, oturum açılmamış | `AddDefect("Dent")` | "Önce bir oturum açın."; hiçbir yere yazmaz | — |
| F-DEF-004 | P1 | [UNIT] | UC | 3 kusur ekle, #3'ü void et, yeni kusur ekle | Yeni kusur #4 (numara yeniden kullanılmaz) | StoreIdsNeverReused |
| F-DEF-005 | P2 | [UNIT] | U, tipte SpecName "SPEC-1" | Kusur ekle | `defect.SpecName == "SPEC-1"` | — |
| F-DEF-006 | P2 | [UNIT] | U, ayar inch | Kusur ekle; ayarı mm yap; yeni kusur ekle | #1 inch, #2 mm; #1 inch kalır | VmUnitFollowsSettingsPerDefect |
| F-DEF-007 | P2 | [UI] | Admin'de tip sırası değiştirilmiş + kaydedilmiş, uygulama yeniden başlatılmış | `DefectTypeBox` listesini oku | Admin'deki sıra ve adlar | — |
| F-DEF-008 | P2 | [UI] | K3 + High metal işaretli | `DefectList` öğesini oku | "#1", "Dent + High metal", durum etiketi ("Açık") tonlu, "1 fotoğraf", sıradaki adım metni | kısmen: VmHighMetalShowsInTypeAndSentence |
| F-DEF-009 | P2 | [UI] [UNIT] | K1, #1 ve #2 var | #1'de `DepthBox`=0.2 yaz, odak kutudayken listeden #2'yi seç; #1'e dön | #1'de D 0.2 kaydedilmiş; #2 kendi sıradaki adımında açılır | VmAutoSaveOnDefectSwitch, VmFirstSelectionAfterOpenUsesCurrentStep |
| F-DEF-010 | P3 | [UI] | K1 | Kusur yokken ekranı oku | "Henüz kusur yok..." metni; detayda "Kusur seçilmedi" | — |
| F-DEF-011 | P2 | [UI] [UNIT] | #1 void, #2 açık, #3 kapalı | Başlık ve liste başlığını oku | "Kusurlar (2)", alt satır "2 kusur, 1 açık" (void sayılmaz) | — |
| F-DEF-012 | P2 | [UNIT] | U, şablonda olmayan tip adıyla (Admin'de silinmiş "OldType") kusur dosyası | Oturumu aç, kusuru seç | Kusur açılır; şablon bulunamadığından tüm yerleşik alanlar görünür; hata yok | — |

### 4.3 Ölçüm, otomatik kayıt ve sınırlar (F-MEAS)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-MEAS-001 | P1 | [UI] [UNIT] | K2 | `DepthBox`=0.12, `WidthBox`=1.5, `LengthBox`=3 → `SaveMeasButton` | "#1 ölçümleri güncellendi, düzeltme geçmişe yazıldı."; sekme Fotoğraf'a geçer; "✓ 1. Ölçüm"; `CameraButton` vurgulu; json'da değerler | VmSaveMeasurementsGoesToPhoto |
| F-MEAS-002 | P1 | [UNIT] [UI] | K2 | `DepthBox`="abc" → kaydet | Error "Derinlik sayı olmalı: 'abc'"; json değişmez; kutu "abc" olarak kalır (düzeltilebilir) | VmInvalidMeasurementNotifiesError |
| F-MEAS-003 | P1 | [UNIT] | U | D, W, L, Ç kutularına sırayla "0", "-1", "-0" | "<Alan> pozitif bir sayı olmalı."; json değişmez | kısmen: ValidatorRules |
| F-MEAS-004 | P1 | [UNIT] | U | `DepthText` = "NaN", "Infinity", "1e309" (taşma → ∞) | Hepsi reddedilir ("pozitif bir sayı olmalı" ya da "sayı olmalı"); json değişmez | — |
| F-MEAS-005 | P2 | [UNIT] | U | "0.0001", "1e-9", "1e308" | İlk ikisi kabul. 1e308 kabul ediliyor: üst sınır yok — beklenen davranış ürün kararı; sonucu raporda yaz (bilgi) | — |
| F-MEAS-006 | P2 | [UNIT] | U | Açı: "0", "360", "-0.01", "360.01" | 0 ve 360 kabul; diğerleri "Açı 0 ile 360 derece arasında olmalı." | kısmen: ValidatorRules |
| F-MEAS-007 | P2 | [UNIT] | U | Renk: 40 karakter, 41 karakter, "   " | 40 kabul; 41 "Renk en fazla 40 karakter olabilir."; yalnızca boşluk → null | — |
| F-MEAS-008 | P2 | [UNIT] | U, `CultureInfo.CurrentCulture` tr-TR ve en-US ayrı ayrı | "0,5", "0.5", ".123", ",123" | Her iki kültürde de 0.5 / 0.123 | ParseAcceptsLeadingDotAndComma |
| F-MEAS-009 | P1 | [UNIT] | U, tr-TR ve en-US | "1.234,5", "1,234.5", "1 234" | Sessizce yanlış sayıya dönmemeli: hata mesajı, json değişmez | — |
| F-MEAS-010 | P2 | [UNIT] | U | Tüm kutular boş → `SaveMeasurements` | "#1 ölçümleri kayıtlı."; düzeltme kaydı yok; sekme Fotoğraf'a geçmez | — |
| F-MEAS-011 | P1 | [UNIT] [UI] | K2 | `DepthBox`=0.2 yaz, Tab ile çık | Otomatik kayıt; `AutoSaveText` "Otomatik kaydedildi HH:mm"; sekme değişmez | VmAutoSaveMergesAndKeepsStep |
| F-MEAS-012 | P2 | [UNIT] | U | Değer değiştirmeden kutudan çık | Dosya ve denetim kaydı yazılmaz | kısmen: MeasurementEditsLogged |
| F-MEAS-013 | P2 | [UNIT] | U | Aynı kişi 10 dk içinde 3 düzeltme; farklı kişi; 10 dk sonrası; arada karar | Aynı kişi 10 dk içi tek geçmiş satırında birleşir; farklı kişi, 10 dk sonrası ya da araya giren karar ayrı satır | MeasurementEditMergeRules, VmAutoSaveMergesAndKeepsStep |
| F-MEAS-014 | P1 | [UNIT] | U | `DepthText`="x" iken kutudan çık | Error; hiçbir şey kaydedilmez; kutu "x" kalır; düzeltip çıkınca kaydedilir | kısmen: VmAutoSaveMergesAndKeepsStep |
| F-MEAS-015 | P1 | [UI] [UNIT] | Kusur sırayla Closed, Voided, Superseded, AwaitingReinspect | Ölçüm kutularına yazmayı dene | Kutular salt okunur, `HighMetalBox` ve `SaveMeasButton` kapalı (`IsEditable=false`); bekleme durumunda kutular yerine açıklama kutusu ve `ReinspectButton` | kısmen: VmPhotoCardsAreViewOnlyWhenClosed |
| F-MEAS-016 | P2 | [UI] [UNIT] | Undercut (D, L), Discoloration (W, L, Renk) kusurları | Kutuları say | Yalnızca şablon alanları görünür; şablon dışı alanda kayıtlı değer varsa o kutu da görünür | kısmen: VmCustomFieldOrphanValueStaysVisible |
| F-MEAS-017 | P2 | [UI] | K2 (mm) ve inch kusur | Başlıkları oku | "Derinlik (mm)" / "Derinlik (in)"; açı "Açı (°)" | VmUnitHeadersFollowSettingsWhenNoDefect |
| F-MEAS-018 | P1 | [UNIT] | U, mm kusur | `DepthText`="0.125" → kaydet; sonra yalnızca `NotesText`'i değiştir → `AutoSave()` | **ŞÜPHELİ (B-01)**: D 0.125 olarak kalmalı. Bugünkü kod: form "0.13" (mm 2 ondalık) gösterdiği için sonraki otomatik kayıt D'yi sessizce 0.13 yapar ve düzeltme kaydı oluşturur. Aynısını inch (5 ondalık) ve açı (3 ondalık) için tekrarla | — |
| F-MEAS-019 | P2 | [UNIT] | U | Kusur seçili değilken `SaveMeasurements()` | "Önce listeden bir kusur seçin." | — |

### 4.4 Ek ölçüm alanları — muayene tarafı (F-CUST)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-CUST-001 | P2 | [UNIT] [UI] | Admin'de "Yükseklik / H / uzunluk" tanımlı, Dent'te işaretli, uygulama yeniden başlatılmış | Dent ekle; `Custom_<key>`=0.3; kaydet | Kutu başlığı "Yükseklik (mm)"; json'da custom değer; cümlede "H 0.3 mm" | VmCustomFieldShownSavedAndInSentence |
| F-CUST-002 | P1 | [UNIT] | U, uzunluk ve sayı türünde iki alan | Uzunluk "-1" ve "0"; sayı "-5" | Uzunluk reddedilir ("pozitif bir sayı olmalı"); sayı -5 kabul | VmCustomFieldValidation |
| F-CUST-003 | P2 | [UNIT] | U, açı ve metin türünde alan | Açı 0/360/361; metin 40/41 karakter; sayı türünde "abc" | 361 ve 41 karakter reddedilir; "abc" → "<Ad> sayı olmalı" | kısmen: SentenceReportCompareValidate |
| F-CUST-004 | P2 | [UNIT] | Değeri olan alan şablondan kaldırılmış / silinmiş | Kusuru aç | Kutu görünmeye devam eder, değer gizlenmez | VmCustomFieldOrphanValueStaysVisible |
| F-CUST-005 | P2 | [UNIT] | Rework bekleyen kusur, H 0.3 | Reinspect "aynı kusur", H boş, D yeni | H 0.3 korunur | VmReinspectMergesCustomField |
| F-CUST-006 | P2 | [UNIT] | Yalnızca ek alanı olan tip (yerleşik alan yok) | Ölçümsüz fotoğraf sekmesine geç; sonra H gir | İlk durumda "Ölçüm girilmedi" uyarısı sorulur; H girildikten sonra "✓ 1. Ölçüm" | — |
| F-CUST-007 | P2 | [UNIT] | Ayar inch | Uzunluk türü ek alanı olan kusur ekle | Başlık "Yükseklik (in)", değer ".0625" biçiminde | — |
| F-CUST-008 | P3 | [UI] | F-CUST-001 | `Custom_<key>` kutusuna yaz, Tab | Otomatik kayıttan sonra odak kaybolmaz / kutu yeniden kurulmaz | — |
| F-CUST-009 | P2 | [UNIT] | Kayıtlı uzunluk değeri (0.3) olan alanın türü Admin'de "metin" yapılmış | Kusuru aç, başka bir kutuyu değiştirip otomatik kaydet | **ŞÜPHELİ (B-12)**: Kayıtlı değerin türü sessizce değişmemeli. Bugünkü kod: kutu tanımı yeni türden, metin eski değerden gelir; otomatik kayıtta değer "0.3" metni olarak (tür = metin) yeniden yazılır ve düzeltme kaydı oluşur | — |

### 4.5 Etkilenen parçalar / lot (F-PART)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-PART-001 | P2 | [UI] [UNIT] | K2 | `AffectedPartsBox`="8, 1-5" → Tab | Kaydedilen değer "1-5, 8"; cümlede "on parts 1–5 and 8" | VmAffectedPartsAutoSave, PartListParseNormalizeDescribe |
| F-PART-002 | P1 | [UNIT] [UI] | K2 | "5-1" yaz, aynı anda `DepthBox`=0.2 değiştir, Tab | Error "aralığı ters"; hiçbir şey (ölçüm dahil) kaydedilmez; kutu metni kalır; düzeltilince ikisi birlikte kaydedilir | PartListRejectsInvalid, VmAffectedPartsAutoSave |
| F-PART-003 | P2 | [UNIT] | UC | "1", "9999", "0", "10000", "1-9999", "-5", "1--5", "a", "1,,2", "1 - 5", "1–5" (en dash), "1;2;3", "2,1,2", "1-2" | Kabul: 1, 9999, 1-9999, "1, 2", 1-5, 1-5, 1-3, "1, 2", "1, 2". Red: 0, 10000, -5, 1--5, a ("1 ile 9999 arasında" / "anlaşılamadı" / "geçerli bir parça numarası değil") | kısmen: PartListParseNormalizeDescribe, PartListRejectsInvalid |
| F-PART-004 | P2 | [UNIT] | U | Boş ve yalnızca boşluk | `AffectedParts == null`; cümle "on the part" | kısmen: StatementPartScopeAndOldRecord |
| F-PART-005 | P2 | [UNIT] | Parent "1-3" | Reinspect "yeni hata" | Child'lar "1-3" devralır | ChildInheritsAffectedParts |
| F-PART-006 | P2 | [UNIT] | — | Rapor ve CSV | Rapor "etkilenen parçalar" satırı; CSV sütunu | ReportNonconformanceAndPartsLines, CsvHasAffectedPartsColumn |
| F-PART-007 | P3 | [UNIT] | U | Yalnızca parça listesini değiştir | Ölçüm düzeltme geçmişine satır eklenmez; denetimde `defect.measure` | kısmen: VmAffectedPartsAutoSave |
| F-PART-008 | P2 | [UI] | Kapalı kusur | `AffectedPartsBox`'a yazmayı dene | Salt okunur | — |

### 4.6 High metal (F-HM)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-HM-001 | P2 | [UI] [UNIT] | K2 | `HighMetalBox` işaretle | Hemen otomatik kayıt; listede "Dent + High metal", başlık "#1  Dent + High metal", cümlede high metal | VmHighMetalShowsInTypeAndSentence |
| F-HM-002 | P2 | [UNIT] | U | Yalnızca high metal işaretle / kaldır | Her değişim düzeltme kaydı oluşturur | HighMetalEditLogged |
| F-HM-003 | P1 | [UNIT] | High metal'li açık kusur | Rework kararı | Kapsam sorulur, seçilmeden kaydedilemez; seçilen kapsam karara yazılır | VmReworkHighMetalAsksScopeAndWaits |
| F-HM-004 | P1 | [UNIT] | HM-only rework | Reinspect "aynı kusur", HM "Giderildi", ölçüm boş | Kaydedilir; ölçümler korunur; HM false; otomatik not "High metal giderildi; ana kusur devam ediyor." | Scenario5_HighMetalOnlyRework, VmReinspectSameHighMetalGoneKeepsDepth |
| F-HM-005 | P2 | [UNIT] | — | Rapor ve cümle | Başlıkta ve cümlede high metal; eski kayıt (alan yok) uyumlu | HighMetalSentenceAndReport |
| F-HM-006 | P2 | [UI] | Kapalı kusur | `HighMetalBox` | Devre dışı | — |
| F-HM-007 | P2 | [UNIT] | U | `DepthText`="x" iken `HighMetal=true` | Error bildirilir, hiçbir şey yazılmaz; `DepthText` düzeltilip kutudan çıkılınca HM ve D birlikte kaydedilir (kutudaki işaret ile dosya sonunda tutarlı) | — |

### 4.7 Adım sekmeleri ve yönlendirme (F-TAB)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-TAB-001 | P2 | [UNIT] [UI] | K2 | `PhotoTabItem`'a tıkla → "Ölçüme dön"; tekrar → "Yine de fotoğrafa geç" | İlkinde Ölçüm'de kalır; ikincisinde Fotoğraf sekmesi | VmPhotoStepWithoutMeasurementAsks |
| F-TAB-002 | P2 | [UI] | K2 | "Ölçüm girilmedi" açıkken Enter; tekrar aç, Esc | İkisinde de Ölçüm'de kalır | — |
| F-TAB-003 | P2 | [UI] | K3 | Sekme etiketlerini oku | "✓ 1. Ölçüm", "✓ 2. Fotoğraflar (1)", "3. Disposition" kalın; karar sonrası "✓ 3. Disposition"; "Geçmiş (N)" | VmSaveMeasurementsGoesToPhoto, VmDecisionTabCheckAfterDecision |
| F-TAB-004 | P2 | [UI] | K2 | `DepthBox`=0.3 yaz, odak kutudayken `HistoryTabItem`'a tıkla | Değer önce kaydedilir, sonra Geçmiş açılır; geçmişte düzeltme satırı | — |
| F-TAB-005 | P3 | [UI] | Kapalı kusur | Detayı oku | Yönlendirme kutusu (sıradaki adım) gizli; durum "Kapandı" | — |
| F-TAB-006 | P2 | [UNIT] | Admin'de hiçbir ölçüm alanı işaretli olmayan tip ("ölçüm yok") | Bu tipte kusur ekle | **ŞÜPHELİ (B-13)**: Ölçüm alanı olmayan tipte sıradaki adım Fotoğraf olmalı. Bugünkü kod: tüm kutular gizli ama sıradaki adım "ölçümleri girin" olarak kalır ve `SaveMeasButton` vurgulu | — |
| F-TAB-007 | P2 | [UNIT] | U | Aynı kusur yenilenir / ListView aynı öğeyi yeniden seçer | Seçili sekme korunur | VmRefreshKeepsSelectedStep, VmListViewReselectAfterRefreshKeepsTab |
| F-TAB-008 | P2 | [UNIT] | Karar ve 2 ölçüm düzeltmesi olan kusur | `History` | Satırlar eskiden yeniye; geri alınan karar işaretli; void child anılmaz | kısmen: VmVoidLastChildReopensParentWithoutLeaks |

### 4.8 Fotoğraf ekleme, kartlar, kaldırma (F-PHOTO)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-PHOTO-001 | P2 | [UI] [UNIT] | K2 + ölçüm | `AddPhotoButton` → dosya seçicide `fixtures\ok.jpg` | "Fotoğraf eklendi: 1_photo_01.jpg"; kart: "1.1", "İşaret yok", "İşaretle" düğmesi; listede "1 fotoğraf" | VmAddPhotoFromFile |
| F-PHOTO-002 | P2 | [UI] [UNIT] | K2 | Dosya seçicide İptal | Hiçbir değişiklik, bildirim yok | VmAddPhotoFromFile |
| F-PHOTO-003 | P1 | [UNIT] [UI] | K2 | `fixtures\fake.jpg` | "Dosya içeriği uzantısıyla eşleşmiyor, görüntü olarak kabul edilmedi."; dosya kopyalanmaz | PhotoImportRules, VmAddPhotoFromFile |
| F-PHOTO-004 | P1 | [UNIT] | U | `big.jpg` (25 MB + 1 bayt) ve `exact25.jpg` (tam 25 MB) | Büyük olan "Fotoğraf 25 MB sınırını aşıyor."; tam 25 MB kabul | — |
| F-PHOTO-005 | P2 | [UNIT] | U | `ok.bmp`, `.gif`, `.webp`; büyük harf `OK.JPG`, `.jpeg` | BMP/GIF/WEBP: "Yalnızca JPG ve PNG dosyaları eklenebilir."; `.JPG` ve `.jpeg` kabul | — |
| F-PHOTO-006 | P2 | [UNIT] | U | Seçiciden yol döndükten sonra dosya silinmiş | "Fotoğraf bulunamadı." | — |
| F-PHOTO-007 | P2 | [UNIT] | U | jpg, png, jpg sırayla ekle | Adlar `1_photo_01.jpg`, `1_photo_02.png`, `1_photo_03.jpg`; etiketler 1.1, 1.2, 1.3 | kısmen: VmPhotoCardsShowLabelsAndCleanCopy |
| F-PHOTO-008 | P2 | [UNIT] | `1_photo_01.jpg` diskten (testte) silinmiş, `1_photo_02.jpg` duruyor | Yeni jpg ekle | **ŞÜPHELİ (B-10)**: Ekleme başarılı olmalı, var olan dosyanın üzerine yazılmamalı. Bugünkü kod: sayıma göre ad `1_photo_02.jpg` üretir, `File.Copy(overwrite:false)` IOException → kusura bir daha fotoğraf eklenemez | — |
| F-PHOTO-009 | P1 | [UI] [UNIT] | K3 | Kartın kaldır düğmesi (Name "1_photo_01.jpg fotoğrafını kaldır") → "Vazgeç" | Hiçbir şey değişmez | VmRemovePhotoConfirmation |
| F-PHOTO-010 | P1 | [UI] [UNIT] | K3 | Kaldır → "Kaldır" | Fotoğraf listeden ve işaretlerden çıkar; dosya diskte kalır; denetimde `defect.photo.remove`; rapora girmez | VmRemovePhotoConfirmation |
| F-PHOTO-011 | P1 | [UI] | K3 | Kaldır diyaloğu açılır açılmaz Enter | Odak "Vazgeç"te olduğundan fotoğraf **kaldırılmaz** | — |
| F-PHOTO-012 | P2 | [UNIT] | U | `RemovePhotoAsync("../x.jpg")`, `RemovePhotoAsync("abc.jpg")` | Ad kalıbına uymayan ad sessizce yok sayılır; diyalog açılmaz | — |
| F-PHOTO-013 | P2 | [UI] [UNIT] | Kapalı kusur, fotoğraflı | Kartları oku | Düğme "Görüntüle"; kaldır düğmesi yok; `CameraButton` ve `AddPhotoButton` kapalı | VmPhotoCardsAreViewOnlyWhenClosed |
| F-PHOTO-014 | P2 | [UNIT] | Temiz kopyası olan fotoğraf; temiz kopya dosyası diskten silinmiş | Kartı oku | Temiz kopya yoksa orijinal gösterilir, "arka plan kaldırıldı" yazmaz, hata yok | kısmen: VmPhotoCardsShowLabelsAndCleanCopy |
| F-PHOTO-015 | P2 | [UNIT] | Çok bölgeli kusur, bölge sonuçları kayıtlı | Kart rozetleri | "Giderildi" / "Yeni hata → #N" rozetleri; void child numarası rozette yok | VmReinspectPerRegionOutcomes |

### 4.9 Kamera (F-CAM) — donanım gerektirir

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-CAM-001 | P2 | [MANUAL] | Kamera takılı değil, K2 + ölçüm | `CameraButton` | Pencere büyütülmüş açılır, başlık "Kamera — #1 Dent · PD170 / OP40 / SN001"; `StatusText` "Kamera bulunamadı..."; `CaptureButton` kapalı; uygulama çökmez | — |
| F-CAM-002 | P2 | [MANUAL] | Harici + dahili kamera | Pencereyi aç; `DeviceBox`; "Kameraları yenile" | Sıra: son kullanılan → C920 → harici → arka; ön kamera otomatik seçilmez; yenile listeyi tazeler | — |
| F-CAM-003 | P2 | [MANUAL] | Kamera izni zaten kapalı olan bir makine (ayar değiştirme) | Pencereyi aç | "Kamera erişimi kapalı..." mesajı | — |
| F-CAM-004 | P2 | [MANUAL] | Kamera başka uygulamada açık | Pencereyi aç | "Kamera başka bir uygulama tarafından kullanılıyor..." | — |
| F-CAM-005 | P1 | [MANUAL] [UNIT] | Kamera hazır, çerçeve açık | Space → önizleme → Enter | Pencere kapanır; foto kusura eklenir; çerçeve kare işareti (`CenterSquare`, kesir = slider) yazılır; Fotoğraf sekmesi; düzenleyici otomatik açılır | VmCameraSavesFramedMarkupAndOpensEditor |
| F-CAM-006 | P2 | [MANUAL] | İnceleme modu | Esc | Canlı görüntüye döner, `SavedText` boşalır, foto eklenmez | — |
| F-CAM-007 | P2 | [MANUAL] [UNIT] | Kamera açık | Kaydetmeden pencereyi kapat | Foto eklenmez, düzenleyici açılmaz | VmCameraClosedWithoutPhotoDoesNothing |
| F-CAM-008 | P2 | [MANUAL] | Kamera hazır | `Zoom1Button`/`Zoom2Button`/`Zoom4Button`; tekerlek; çift tık; sıkıştırma | `ZoomText` 1.0×/2.0×/4.0×; 1–5 arası sınırlanır; kaydedilen foto ekranda görülen alan kadar kırpılır | — |
| F-CAM-009 | P2 | [MANUAL] [UNIT] | Kamera hazır | `FrameToggle` kapat/aç; `FrameSizeSlider` 0.05 ve 0.9 uçları | Kapalıyken slider devre dışı, foto işaretsiz kaydedilir; `FrameSizeText` %5 / %90; kare kısa kenarın o oranı | CameraFrameSquare |
| F-CAM-010 | P3 | [MANUAL] | Odak destekli kamera | Önizlemeye dokun; `AutoFocusToggle` | Odak halkası; destek yoksa anahtar gizli | — |
| F-CAM-011 | P1 | [UNIT] [MANUAL] | #1 ve #2 var; kamera #1 için açık | Ana pencerede #2'yi seç, sonra kamerada çek-kaydet | Foto #1'e eklenir (hedef sabit); #2'de seçiliyken düzenleyici açılmaz | — |
| F-CAM-012 | P2 | [MANUAL] | Kamera açık | `CameraButton`'a tekrar bas | İlk pencere kapanır, yenisi açılır (tek kamera penceresi) | — |
| F-CAM-013 | P2 | [MANUAL] | Kamera açık | Ana pencereyi kapat | Kamera penceresi de kapanır, kamera serbest kalır | — |
| F-CAM-014 | P2 | [MANUAL] | Kamera açık, kök klasör yeniden adlandırılmış (F-NET yöntemi) | Çek → Kaydet | `StatusText` "Kaydedilemedi: ..."; pencere açık kalır; geçici dosya silinir | — |
| F-CAM-015 | P3 | [MANUAL] | Kamera yok | Space, Enter, Esc | Hiçbir şey olmaz, pencere kapanmaz | — |

### 4.10 Fotoğraf düzenleyici ve arka plan kaldırma (F-EDIT)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-EDIT-001 | P2 | [UI] | K3 | Kartta "İşaretle" | Düzenleyici büyütülmüş; `TargetText` "#1 Dent · 1_photo_01.jpg · ..."; `LabelBox` void olmayan kusurlar, "Kusur #1" seçili; görüntü yüklenince `SaveButton` açılır | — |
| F-EDIT-002 | P2 | [UI] [UNIT] | Düzenleyici açık | `Ink` üzerinde sürükleyerek kutu çiz → Kaydet | Pencere kapanır; "1_photo_01.jpg kaydedildi: 1 işaret. Orijinal fotoğraf korunuyor."; kart "1 işaret" | VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-003 | P2 | [UI] | Düzenleyici açık | `CircleTool` ve `ArrowTool` ile çiz | Araçlar karşılıklı dışlayan; daire ve ok çizilir; ok etiketi kuyrukta | — |
| F-EDIT-004 | P3 | [UI] | Düzenleyici açık | Görüntünün %1'inden kısa sürükleme | İşaret eklenmez | — |
| F-EDIT-005 | P2 | [UI] | 2 işaret var | Ctrl+Z; "Son işareti geri al"; işaret yokken tekrar | Her biri son işareti siler; boşken hata yok | — |
| F-EDIT-006 | P2 | [UI] [UNIT] | İşaretli foto | "İşaretleri sil" → Kaydet | Kusurun `Markups` kaydından foto çıkar (boş işaret + temiz kopya yok) | VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-007 | P2 | [UI] | İşaret var | İşarete tıkla → Delete; seçim yokken Delete | Seçili silinir; seçim yokken hiçbir şey | — |
| F-EDIT-008 | P2 | [UI] | #1, #2 var; işaret seçili | `LabelBox` → "Kusur #2"; seçimi kaldırıp yeni işaret çiz | Seçili işaretin numarası 2 olur; yeni işaret de 2 ile çizilir | — |
| F-EDIT-009 | P3 | [UI] | İşaret var | Taşı; köşe tutamacından boyutlandır; görüntü dışına sürükle | Konum/boyut değişir; görüntü sınırına kısılır | — |
| F-EDIT-010 | P2 | [UI] [UNIT] | 30 işaret var | 31. işareti çiz | `HintText` "Bir fotoğrafta en fazla 30 işaret olabilir."; eklenmez. UNIT: 31 işaretli markup `Validate` hatası | MarkupValidation |
| F-EDIT-011 | P2 | [UI] | İşaret çizilmiş | "Vazgeç" | Pencere kapanır; json ve denetim değişmez. Not (P3, B-17): kaydedilmemiş değişiklik için onay sorulmuyor | — |
| F-EDIT-012 | P2 | [UI] | İşaret çizilmiş | Pencereyi X ile kapat | Vazgeç ile aynı; kayıt yok | — |
| F-EDIT-013 | P2 | [UI] [UNIT] | K3 | `BgTool` → parçayı çerçevele → `BgApplyButton` → `Busy` biter → Kaydet | `1_photo_01_clean.jpg` yazılır; orijinal değişmez; kart "arka plan kaldırıldı", küçük resim temiz kopya; rapor temiz kopyayı kullanır | BackgroundSeparatesPart, CleanedCopyKeepsOriginal, VmEditorSaveWritesMarkupAndCleanCopy |
| F-EDIT-014 | P2 | [UI] | Temiz kopyalı foto | Düzenleyici → `BgRevertButton` → Kaydet | `CleanedFile` null; kart/rapor orijinali gösterir; temiz dosya diskte kalabilir | — |
| F-EDIT-015 | P2 | [UI] | Temiz kopyalı foto | Düzenleyiciyi yeniden aç | Temiz görüntü görünür; `BgRevertButton` açık | — |
| F-EDIT-016 | P2 | [UI] | Geçici kökte `defects\1_photo_01.jpg` içeriği bozuk bayta çevrilmiş (yalnızca geçici kök) | "İşaretle" | `HintText` "Fotoğraf açılamadı: ..."; `SaveButton` kapalı; çökme yok | — |
| F-EDIT-017 | P1 | [UNIT] | U, #1 ve #2 | #1 için `OpenEditor`; `Select(2)`; sonra editör kaydı (`Editors[0].OnSave`) | İşaret #1'e yazılır; liste #2 seçiliyken yenilenmez | — |
| F-EDIT-018 | P3 | [UI] | Düzenleyici açık, işaret çizilmiş | Ana pencerede başka kartta "İşaretle" | İlk pencere kaydetmeden kapanır (tek düzenleyici); çizim kaybolur — beklenen mi, raporda belirt | — |
| F-EDIT-019 | P2 | [UNIT] | Kapalı kusur | `OpenEditor(foto)` | Düzenleyici açılmaz; dosya (varsa temiz kopya) `OpenFile` ile açılır | VmOpenEditorOnClosedDefectOpensFile |
| F-EDIT-020 | P2 | [UNIT] | U, editör #1 için açık; bu sırada #1 başka kullanıcı tarafından Accept ile kapatılmış (store'a doğrudan yaz) | Editör kaydı | **ŞÜPHELİ (B-14)**: Kapalı kusurda işaret değişmemeli (hata vermeli). Bugünkü kod: geri çağrı durumu kontrol etmez, işareti kapalı kusura yazar | — |

### 4.11 Durum makinesi (F-SM) — Bölüm 3.7 tablosuna karşı

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-SM-001 | P1 | [UNIT] | UC, her durum için hazırlanmış kusur (Open, Repairing, AwaitingReinspect, WaitingVendor, MrbPending, Closed (Accept), Closed (Scrap), Closed (MrbRejected), Voided, Superseded) | Her durum × her karar (Undo hariç 14) için `Check(d, karar, izinli kaynak)` | `Ok` yalnızca 3.7 tablosundaki hücrelerde true; Closed(Scrap/MrbRejected)'da Reopen false; Voided/Superseded'da hepsi false (tam matris tek testte) | kısmen: StateVoidRules, StateSourceRules |
| F-SM-002 | P1 | [UNIT] | UC, Open kusur | Her karar × her kaynak (Engineer, Operator, Ctp, Mrb, System) | Void yalnızca Operator; MrbAccepted/MrbRejected yalnızca Mrb; Reinspect ve AfterRework* Engineer/Ctp; diğerleri yalnızca Engineer; System hiçbir normal kararı veremez | kısmen: StateSourceRules |
| F-SM-003 | P1 | [UNIT] | Open | Accept | Closed | VmDecisionAcceptClosesAndCancelChangesNothing |
| F-SM-004 | P1 | [UNIT] | Open | Rework hedef boş / "  " / "OP50" | Boş ve boşluk: "Rework için hedef operasyonu girin (örn. OP40)."; OP50: AwaitingReinspect, Snapshot alınmış | StateReworkReinspectAccept |
| F-SM-005 | P2 | [UNIT] | Open | Reinspect | AwaitingReinspect; Snapshot yok | — |
| F-SM-006 | P1 | [UNIT] | Open → Repair | Repairing'de Accept, Scrap, Reinspect, SubmitMrb (ayrı ayrı); Rework ve Void | İlk dördü geçerli (Closed/Closed/AwaitingReinspect/MrbPending); Rework ve Void reddedilir | — |
| F-SM-007 | P1 | [UNIT] | Open | Scrap → Reopen | Closed; Reopen reddedilir "kesin olarak kapandı" | ScrapIsFinal, Scenario3_ReopenAndScrapFinal |
| F-SM-008 | P1 | [UNIT] | Open → SubmitMrb | MrbAccepted; ayrı kusurda MrbRejected; ikisinde Reopen | MrbAccepted → Closed, Reopen mümkün; MrbRejected → Closed + System Scrap, Reopen yasak | StateMrbRejectedScraps, Scenario4_MrbRejected |
| F-SM-009 | P2 | [UNIT] | Open → ReturnToVendor | WaitingVendor'da Reinspect, Scrap, Accept | Reinspect → AwaitingReinspect; Scrap → Closed; Accept reddedilir; WaitingVendor açık sayılır | StateVendorIsOpen |
| F-SM-010 | P1 | [UNIT] | AwaitingReinspect | AfterReworkConforms; AfterReworkSameDefect; AfterReworkNewDefect (id'siz ve id'li); Repair; SubmitMrb; Scrap | Closed; Open; id'siz hata "Yeni hata kararı için ... numarası gerekli", id'li Superseded; Repairing; MrbPending; Closed | kısmen: ContinueAsChildLinks, SameDefectLoops |
| F-SM-011 | P1 | [UNIT] | AfterReworkSameDefect sonrası Open | Void | Reddedilir: "Karar geçmişi olan kusur void edilemez..." | VoidOnlyWithoutHistory |
| F-SM-012 | P1 | [UNIT] | Closed (Accept) | Reopen not boş / "  " / "yanlış kabul" | Boşlar: "Yeniden açmak için bir gerekçe notu zorunludur."; dolu: Open, eski kararlar geçmişte | ReopenRules, VmReopenRequiresNote |
| F-SM-013 | P2 | [UNIT] | Open | Accept'e `reworkScope` ver; Accept'e `newDefectIds` ver | "Rework kapsamı yalnızca rework kararında verilir." / "Yeni kusur numaraları yalnızca rework sonrası..." | — |
| F-SM-014 | P1 | [UNIT] | Voided ve Superseded | Her kararı dene | Hepsi reddedilir; durum değişmez | — |
| F-SM-015 | P1 | [UNIT] | Rework → SameDefect → Rework → SameDefect → Rework → Conforms | Durum ve Seq | Her adım beklenen durumda; Seq 1..n kesintisiz; değişim satırı son rework öncesine göre | SameDefectLoops, Scenario1_SameDefectThenConforms |
| F-SM-016 | P1 | [UNIT] | Accept → Undo → Rework → Undo | `Effective`, `GetState`, `LastUndoable` | Undo kayıtları ve geri alınanlar Effective'ten çıkar; durumlar Closed → Open → AwaitingReinspect → Open; Seq artmaya devam eder | UndoRestoresPreviousState |
| F-SM-017 | P1 | [UNIT] | Accept → Reopen | Undo | Reopen geri alınır, durum Closed | — |
| F-SM-018 | P2 | [UNIT] | Void edilmiş kusur; MrbRejected sonrası | `LastUndoable` | Void geri alınamaz (null); MrbRejected geri alınabilir, System Scrap onunla birlikte geri alınır | UndoMrbRejectedRemovesAutoScrap |
| F-SM-019 | P1 | [UNIT] | AfterReworkNewDefect kararlı parent | `DefectStateMachine.Undo` (store dışı) | "Bu karar yeni kusurlara bağlı; ..." hatası | kısmen: UndoChildLinkVoidsChild |
| F-SM-020 | P1 | [UNIT] | Kusur listeleri | `SessionGate.BlockingDefects` | Open/Repairing/AwaitingReinspect/WaitingVendor/MrbPending engeller; Superseded child açıkken engeller, child kapanınca çözülür; Voided ve Closed engellemez | GateBlocksOpen, GateSupersedeResolves |
| F-SM-021 | P2 | [UNIT] | U, her durum için kusur | `Vm.DecisionOptions` etiketleri | Her durumda tam olarak `Check` geçen kararlar, `DecisionOrder` sırasıyla; AwaitingReinspect'te ilk seçenek "Rework sonrası muayene sonucu…" | kısmen: VmDecisionOptions |

### 4.12 Karar diyaloğu (F-DEC)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-DEC-001 | P1 | [UI] [UNIT] | K3 | `DecisionCombo` → "Kabul (SPA)" → referans "SPA-1" → "Kararı kaydet" | Başlık "Kabul (SPA)", "#1 Dent · şu an: Açık", açıklama metni; kayıt sonrası "Yeni durum: Kapandı"; referans karara yazılır; `DecisionCombo` placeholder'a döner | VmDecisionAcceptClosesAndCancelChangesNothing |
| F-DEC-002 | P1 | [UI] [UNIT] | K3 | Karar seç → "Vazgeç" | Değişiklik yok, denetim yok, combo sıfırlanır | VmDecisionAcceptClosesAndCancelChangesNothing |
| F-DEC-003 | P1 | [UI] | K3 | Karar diyaloğunda Esc | Vazgeç ile aynı | — |
| F-DEC-004 | P1 | [UI] | K3 | Diyalog açılınca not kutusuna tıklamadan Enter; not kutusunda Enter | Kayıt yapılmaz (varsayılan düğme yok); not kutusunda yeni satır | — |
| F-DEC-005 | P2 | [UI] [UNIT] | K3 | "Rework → Reinspect"; hedef kutusu "OP40" seçili gelir; "OP50" yaz → kaydet → "Sonra" | Hedef OP50 kaydedilir; durum "Rework sonrası muayene bekliyor"; ölçüm alanı yerine bekleme kutusu "Parça rework'te · OP50 operasyonunda..." ve `ReinspectButton` | kısmen: VmReworkHighMetalAsksScopeAndWaits |
| F-DEC-006 | P2 | [UNIT] | U | Rework, hedef "   " | Oturumun operasyonu (OP40) yazılır | — |
| F-DEC-007 | P1 | [UI] [UNIT] | K3 + High metal | Rework → kapsam seçmeden kaydetmeyi dene; sonra "Yalnızca high metal" seç | Önce "Kararı kaydet" kapalı; seçince açılır; `ReworkScope=HighMetalOnly` | VmReworkHighMetalAsksScopeAndWaits |
| F-DEC-008 | P2 | [UI] [UNIT] | K3 | Rework → kaydet → "Sonucu şimdi gir" | Rework sonrası muayene diyaloğu hemen açılır | VmReworkNowOpensReinspectDialog |
| F-DEC-009 | P2 | [UI] | K3 | "Reinspect (CTP)" | Hedef başlığı "Yeniden kontrol hangi operasyonda yapılacak? (örn. CTP)"; sonraki soru "Kontrol şimdi yapılıyor mu?" | — |
| F-DEC-010 | P1 | [UI] [UNIT] | Kapalı kusur | "Yeniden aç": not boş, "   ", "neden" | Boş ve boşlukta "Kararı kaydet" kapalı; doluyken açık; durum Açık | VmReopenRequiresNote |
| F-DEC-011 | P2 | [UI] [UNIT] | K3 | Her karar türünün diyaloğunu aç | Referans kutusu yalnızca Accept, SubmitMrb, MrbAccepted, MrbRejected, Repair, Scrap'ta; hedef yalnızca Rework/Reinspect'te; not başlığı Reopen'da "(zorunlu)" | kısmen: VmDecisionOptions |
| F-DEC-012 | P2 | [UI] [UNIT] | K3 | Submit MRB → kaydet | MrbPending; seçenekler yalnızca "MRB kabul (accept as is)", "MRB red (otomatik hurda)" | kısmen: VmDecisionOptions |
| F-DEC-013 | P1 | [UI] [UNIT] | MrbPending | "MRB red (otomatik hurda)" → kaydet | Kapandı; geçmişte otomatik hurda (System); "Yeniden aç" seçeneği yok; ipucu "Kesin kapandı..." | StateMrbRejectedScraps |
| F-DEC-014 | P1 | [UI] [UNIT] | 3 aktif fotoğraflı kusur | Rework → bölge kutuları (hepsi işaretli); hepsini kaldır; sonra yalnızca 1.3'ü kaldır → kaydet | Hepsi kaldırılınca kaydet kapalı; 1.3 "Conforms", 1.1–1.2 "Continues" | VmDecisionAsksRegionsAndMarksUncheckedConforming, VmDecisionRegionSelectionIsValidated |
| F-DEC-015 | P2 | [UNIT] | 1 fotoğraflı kusur; 2 fotoğraflı kusurda Reopen | Diyalog isteği | `Regions == null` (tek bölge ya da Reopen/Void'de sorulmaz) | kısmen: ReinspectFormRegionRules |
| F-DEC-016 | P1 | [UNIT] | U | `DepthText`="0.9" (kaydedilmemiş) iken Rework kararı | Karar öncesi otomatik kayıt; Rework Snapshot'ında D 0.9 | — |
| F-DEC-017 | P2 | [UNIT] | U | `_config\decision_texts.json`'a Accept için yeni metin yaz; karar iste | `DecisionRequest.EffectText` yeni metin (yeniden başlatma gerekmez) | — |
| F-DEC-018 | P3 | [UI] | K3 | Aynı kararı seç → Vazgeç → aynı kararı tekrar seç | Diyalog yeniden açılır | — |
| F-DEC-019 | P2 | [UNIT] | U (gerçek audit) | Not 5000 karakter, HTML içerikli `<b>x</b>` | Kaydedilir; raporda kaçışlı görünür | kısmen: ReportEscapesAndHidesVoid |
| F-DEC-020 | P2 | [UI] [UNIT] | Voided ve Superseded kusur | Disposition sekmesi | `DecisionCombo` gizli; ipucu "Void edilmiş kayıt..." / "Bu kusur yeni bir kusura devredildi..." | — |
| F-DEC-021 | P2 | [UNIT] | Oturumu "A" açmış, "B" yeniden açmış | B karar verir | `DecidedBy == "B"`; denetim satırı B | — |

### 4.13 Rework sonrası muayene (F-RWK)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-RWK-001 | P1 | [UI] | AwaitingReinspect (rework, OP50) | `ReinspectButton` | Başlık "#1 Dent: rework sonrası muayene"; "Rework öncesi ölçümler: D 0.12 ... · muayene yeri: OP50"; "Kaydet" kapalı | — |
| F-RWK-002 | P1 | [UI] [UNIT] | F-RWK-001 | "Kusur giderildi (uygun)" → Kaydet | "#1 uygun bulundu ve kapandı." | VmReinspectConformsAndWrongStateErrors |
| F-RWK-003 | P1 | [UI] [UNIT] | F-RWK-001 (HM yok) | "Aynı kusur hâlâ var" → kutular boş (placeholder "önce: 0.12") → Kaydet kapalı; D=0.2 → değişim satırı → Kaydet | D'siz kaydet kapalı; sonra Open; W, L önceki değerinde; Ölçüm sekmesinde "Rework sonrası yeniden ölçüldü..." değişim satırı | Scenario1_SameDefectThenConforms |
| F-RWK-004 | P1 | [UI] [UNIT] | HM'li kusur rework'te | "Ana kusur hâlâ var" → HM sorusu cevapsızken Kaydet; "Giderildi" seç | Cevapsız kapalı; seçince ölçüm girmeden de kaydedilir; not boşsa otomatik not | VmReinspectSameHighMetalGoneKeepsDepth |
| F-RWK-005 | P1 | [UI] [UNIT] | F-RWK-001 | "Yeni bir hata oluştu" → tip seçmeden Kaydet; "+ Hata ekle" (2 satır) → Dent, Scratch → Kaydet | Tip seçilmeden kapalı; son satırın "×" düğmesi tek satırda kapalı; kayıt: #2 Dent, #3 Scratch açılır, #2 seçili; #1 Devredildi | VmReinspectNewOpensChild, VmReinspectMultipleNewDefects, VmReinspectNewDefectWithoutTypeIsRejected |
| F-RWK-006 | P1 | [UI] | F-RWK-001 | "Aynı kusur" → D="abc" ve W="0,7" → Kaydet | **ŞÜPHELİ (B-06)**: Geçersiz "abc" için hata gösterilmeli / Kaydet kapalı olmalı. Bugünkü kod: diyalog geçersiz metni sessizce boş sayar; W girildiği için Kaydet açılır ve D önceki değerinde kalır | — |
| F-RWK-007 | P1 | [UNIT] | U | `ReinspectInput` "aynı kusur", D = -1 | "Derinlik pozitif bir sayı olmalı."; hiçbir şey yazılmaz, kusur hâlâ bekliyor | — |
| F-RWK-008 | P2 | [UI] [UNIT] | F-RWK-001 | Vazgeç | Durum değişmez | kısmen: VmReinspectConformsAndWrongStateErrors |
| F-RWK-009 | P2 | [UI] [UNIT] | HM-only kapsamlı rework | Diyaloğu aç | Üstte "Rework kapsamı: yalnızca high metal için yapıldı..." | Scenario5_HighMetalOnlyRework |
| F-RWK-010 | P1 | [UI] [UNIT] | 3 aktif bölgeli kusur rework'te | Bölge başına: 1.1 Giderildi, 1.2 Devam ediyor, 1.3 Yeni hata (2 satır: Nick, Dent) → ölçüm gir → Kaydet | Özet "#1 açık kalır; 2 yeni kusur açılır"; bir bölge seçimsizken Kaydet kapalı; kayıt: 1.1 Conforms, 1.2 Continues, 1.3 NewDefect → #2, #3 | VmReinspectPerRegionOutcomes, VmReinspectRegionsMissingChoiceIsRejected, MixedRegionOutcomes |
| F-RWK-011 | P2 | [UNIT] | F-RWK-010 sonrası yeni rework | Reinspect isteği | Yalnızca aktif bölge (1.2) sorulur; tek bölge kaldığı için 3 seçenekli mod | VmReinspectPerRegionOutcomes |
| F-RWK-012 | P2 | [UNIT] | inch kusur rework'te | `ReinspectRequest.Unit` ve başlıklar | Unit = Inch; başlıklar "(in)"; placeholder ".0625" biçimi | — |
| F-RWK-013 | P2 | [UNIT] | inch parent, "1-3" parçalı | Yeni hata | Child'lar inch ve "1-3" | ChildDefectInheritsParentUnit, ChildInheritsAffectedParts |
| F-RWK-014 | P2 | [UNIT] | Açık kusur | `ReinspectAsync` | "Bu kusur rework sonrası muayene beklemiyor." | VmReinspectConformsAndWrongStateErrors |
| F-RWK-015 | P2 | [UNIT] | Renk alanı olmayan şablon, eski rengi yok / var | İstek `ShowColor` | Yoksa false, eski renk varsa true | — |
| F-RWK-016 | P1 | [UNIT] | Bölge sonucunda kusurda olmayan foto adı | `ApplyReinspect` | "Bölge #1 kusurunda yok: ..."; hiçbir şey yazılmaz (tek kilit, kısmi yazma yok) | AllRegionsConformClosesAndInvalidPhotoRejected |
| F-RWK-017 | P2 | [UNIT] | Reinspect (CTP) kararıyla bekleyen kusur | Diyalog | Başlık "yeniden kontrol sonucu"; "Önceki ölçümler" | — |

### 4.14 Geri alma (F-UNDO)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-UNDO-001 | P1 | [UI] [UNIT] | Accept verilmiş | Disposition | `UndoButton` görünür; metin "Son karar: Kabul ... (gg.AA SS:dd, Test Inspector)" | VmUndoTextAfterDecision |
| F-UNDO-002 | P1 | [UI] [UNIT] | F-UNDO-001 | `UndoButton` → gerekçe boş / "   " / "yanlış seçim" → "Geri al" | Boşlukta "Geri al" kapalı; kayıt: durum Açık; geçmişte iki satır (karar + geri alma); denetimde `disposition.undo` | UndoRestoresPreviousState |
| F-UNDO-003 | P1 | [UI] [UNIT] | F-UNDO-001 | Geri al → Vazgeç | Değişiklik yok | — |
| F-UNDO-004 | P1 | [UNIT] | MRB red | Geri al | Otomatik hurda ile birlikte geri alınır; MrbPending | UndoMrbRejectedRemovesAutoScrap |
| F-UNDO-005 | P1 | [UI] [UNIT] | 2 child açılmış parent | Geri al | Uyarı "Bu kararla açılan yeni kusurlar #2, #3 void edilecek..."; kayıt: child'lar Voided, parent AwaitingReinspect, bölge durumları geri gelir | VmUndoChildTransferVoidsChild, VmUndoTwoChildrenListsBoth, UndoVoidsAllChildrenAndRestoresRegions |
| F-UNDO-006 | P1 | [UNIT] | Accept → Reopen | Geri al | Durum Kapandı; Reopen geçmişte "geri alındı" | — |
| F-UNDO-007 | P1 | [UNIT] | Rework → SameDefect | Geri al, sonra tekrar geri al | Önce AwaitingReinspect, sonra Açık; her biri ayrı geri alma satırı | — |
| F-UNDO-008 | P2 | [UNIT] | Yalnızca Void ya da yalnızca System kaydı olan kusur | `CanUndo`, `UndoLastAsync` | `CanUndo=false`; çağrılırsa "Geri alınacak karar yok." | — |
| F-UNDO-009 | P2 | [UNIT] | "A" kararı, "B" oturumu | B geri alır | İzin verilir; geri alma kaydı ve denetim B adına | — |
| F-UNDO-010 | P2 | [UNIT] | Child'ı void edilmiş "aynı kusur + yeni kusur" kararı | Geri al | Void child tekrar void edilmez; parent durumu doğru | kısmen: VoidChildOfSameDefectDecisionKeepsParentOpen, VmUndoMultipleChildrenAndVoidOneOfTwo |

### 4.15 Void (F-VOID)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-VOID-001 | P1 | [UI] [UNIT] | K2 / kararlı kusur | `VoidButton` görünürlüğü | Yalnızca karar geçmişi olmayan Açık kusurda görünür | StateVoidRules |
| F-VOID-002 | P1 | [UI] [UNIT] | K2 | Void → gerekçe boş / "  " / "yanlış tip" → "Void et" | Boşlukta kapalı; kayıt: Void, "Kusurlar" sayısından düşer, rapora girmez, salt okunur, numara yeniden kullanılmaz | VmVoidNeedsReason |
| F-VOID-003 | P1 | [UI] [UNIT] | K2 | Void → Vazgeç | Değişiklik yok | VmVoidNeedsReason |
| F-VOID-004 | P1 | [UNIT] | Tek child'lı devredilmiş parent | Child'ı void et | Parent tekrar AwaitingReinspect; mesajda "#1 yeniden rework sonrası muayene bekliyor"; ekranda void child izi yok | VmVoidLastChildReopensParentWithoutLeaks, VoidLastChildReopensParent |
| F-VOID-005 | P1 | [UNIT] | 2 child | Birini void et | Parent devredilmiş kalır | VoidOneOfManyChildrenKeepsParentTransferred |
| F-VOID-006 | P2 | [UI] | Void kusur | Seç | Kutular salt okunur; karar listesi yok; kartlarda "Görüntüle" | — |
| F-VOID-007 | P2 | [UNIT] | Void kusurlu oturum | `IndexBuilder.Rebuild` | Satırda durum "Voided"; açık sayılmaz | kısmen: IndexRebuildsRows |

### 4.16 Muayeneyi tamamlama (F-COMP)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-COMP-001 | P1 | [UI] [UNIT] | #1 Açık, #2 MRB bekliyor | `CompleteButton` | Warning "Muayene kapatılamaz, açık kusurlar var: #1 (Açık), #2 (MRB bekliyor)"; durum değişmez | VmCompleteRules |
| F-COMP-002 | P1 | [UI] [UNIT] | Kusursuz oturum | Tamamla | "Muayene tamamlandı. Rapor oluşturabilirsiniz."; alt satır "Tamamlandı"; `session.json` Completed; denetim `session.complete` | VmCompleteRules |
| F-COMP-003 | P1 | [UNIT] | Kapalı + void + child'ı kapanmış devredilmiş | Tamamla | Başarılı | GateSupersedeResolves |
| F-COMP-004 | P1 | [UNIT] | Tamamlanmış oturum | Kusur ekle; Dashboard satırı | **ŞÜPHELİ (B-04)**: Tamamlanmış muayeneye kusur eklenememeli ya da oturum yeniden "Devam ediyor" olmalı. Bugünkü kod: ekleme serbest, durum "Completed" kalır; Dashboard "Tamamlanan" sayar ama açık kusur var | — |
| F-COMP-005 | P3 | [UNIT] | Tamamlanmış oturum | Tekrar Tamamla | Bilgi mesajı ("zaten tamamlandı") ve tek denetim satırı beklenir. Bugünkü kod: yeniden kaydeder, ikinci `session.complete` yazar (B-15) | — |
| F-COMP-006 | P1 | [UNIT] | `DepthText` kaydedilmemiş | Tamamla | Önce otomatik kayıt; değer diskte | — |
| F-COMP-007 | P3 | [UI] [UNIT] | Açık kusur yok | Düğme stili | `CompleteButton` vurgulu (`IsCompletePrimary`); tamamlandıktan sonra vurgu kalkar | — |
| F-COMP-008 | P2 | [UI] | Tamamlanmış oturum | Dashboard "Aç" ile aç | Alt satır "Tamamlandı"; kusurlar görüntülenir | — |

### 4.17 Rapor (F-RPT)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-RPT-001 | P1 | [UI] [UNIT] | K3, Edge kurulu | `ReportButton` | "Rapor v1 hazırlanıyor (PDF)..."; `reports\Rapor_v1.pdf` oluşur, `Rapor_v1.html` silinir; PDF Edge'de açılır; durum çubuğu kapanır; denetim `report.create Rapor_v1 (pdf)` | VmReportWritesHtmlAndOpensPdf |
| F-RPT-002 | P1 | [UNIT] | U | Üç kez rapor | v1, v2, v3; hiçbiri üzerine yazılmaz | VmReportWritesHtmlAndOpensPdf, ReportVersionRules |
| F-RPT-003 | P1 | [UNIT] | U, `Pdf.Error="x"` | Rapor | HTML kalır ve açılır; Warning "Rapor v1 HTML olarak açıldı (PDF üretilemedi: x)..." | VmReportFallsBackToHtml |
| F-RPT-004 | P2 | [UI] [UNIT] | K3 | `ReportButton`'a hızlı çift tık | Tek rapor sürümü; çalışırken `CanCreateReport=false`, düğme kapalı; bitince açılır | — |
| F-RPT-005 | P2 | [UNIT] | Oturum yok | `CreateReportAsync` | "Önce bir oturum açın."; `IsReportRunning=false` | VmReportWithoutSessionErrors |
| F-RPT-006 | P1 | [UNIT] | Çeşitli kusurlar | Rapor HTML içeriği | Void hariç; kullanıcı metni kaçışlı; işaretler çizili; temiz kopya kullanılır; inch değerler; iki dilli etiketler; bölge satırları; parça satırı; tarih biçimi MMM.dd.yyyy | ReportEscapesAndHidesVoid, ReportDrawsMarkup, ReportShowsInchValues, ReportPhotoPerPage, ReportBilingualTests.*, ReportShowsRegionLinesAndPluralLinks, ReportNonconformanceAndPartsLines |
| F-RPT-007 | P2 | [UNIT] | Açık kusurlu oturum | Rapor | Rapor üretilir (tamamlanma şartı yok); durumlar doğru | — |
| F-RPT-008 | P2 | [UNIT] | Kusursuz oturum | Rapor | Geçerli HTML, hata yok | — |
| F-RPT-009 | P2 | [UNIT] | Kaldırılmış fotoğraf | Rapor | Kaldırılan fotoğraf rapora girmez | — |
| F-RPT-010 | P2 | [MANUAL] [UI] | 40 adet büyük fotoğraf | Rapor | 60 sn içinde PDF ya da "PDF dönüştürme zaman aşımına uğradı" ile HTML yedeği; uygulama donmaz | — |
| F-RPT-011 | P2 | [UNIT] | `reports\Rapor_v1.html` var, PDF yok | Rapor | Yeni rapor v2; v1.html silinmez | kısmen: ReportVersionRules |
| F-RPT-012 | P2 | [UNIT] | Oturum klasöründe `reports` adında **dosya** (klasör değil) | Rapor | Error mesajı; `IsReportRunning` false'a döner (düğme yeniden açılır); çökme yok | — |
| F-RPT-013 | P2 | [UI] | K3, Edge kurulu, ağ bağlantısı gerekmez | Rapor; PDF'i incele | Gerçek `EdgePdfConverter` PDF üretir; fotoğraflar sayfa başına; başlık ve alt bilgi doğru | — |
| F-RPT-014 | P3 | [MANUAL] | Edge'siz makine | Rapor | "Microsoft Edge bulunamadı" uyarısı; HTML kabuk ile açılır | — |
| F-RPT-015 | P2 | [UNIT] | Kayıtta listelenen ama diskte olmayan foto | Rapor | Rapor yine üretilir, çökme yok | — |

### 4.18 Dashboard (F-DASH)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-DASH-001 | P2 | [UI] [UNIT] | Boş kök | Dashboard sekmesi | Otomatik yenilenir; 5 özet kartı 0; "Henüz kayıt yok. Muayene ekranından oturum açın ya da Yenile'ye basın." | DashEmptyBeforeRefreshThenFiltersAndSummary |
| F-DASH-002 | P2 | [UI] [UNIT] | 4 oturum, farklı durumlar | `FilterPartBox`, `FilterOpBox`, `SerialFilterBox` yaz; `FilterTypeBox`; `FilterStateBox` 6 seçeneğin her biri | Liste ve özet kartları anında süzülür | DashboardFilter, DashEmptyBeforeRefreshThenFiltersAndSummary |
| F-DASH-003 | P2 | [UI] [UNIT] | Süzgeçler dolu | "Filtreleri temizle" | Hepsi boş / "Tümü"; tek yenileme | DashEmptyTextsAndClearFilters |
| F-DASH-004 | P2 | [UNIT] | Kayıtlar var | Eşleşmeyen süzgeç | "Filtrelere uyan kayıt yok." | DashEmptyTextsAndClearFilters |
| F-DASH-005 | P1 | [UI] [UNIT] | Sağlam ve (geçici kökte) bir satırı değiştirilmiş `_audit.log` | "Denetim kaydını doğrula" | Sağlam: "Denetim kaydı sağlam"; bozuk: "Denetim kaydı bozulmuş", "N. satırda zincir kırılmış." | DashVerifyAuditIntactAndBroken |
| F-DASH-006 | P2 | [UI] | Kayıt var, Inspector boş | Kartta "Aç" | Muayene sekmesine geçer; P/O/S dolar; `InspectorBox` odakta; bilgi mesajı "Kaydı açmak için Inspector adınızı girip 'Create Inspection'a basın." | kısmen: DashRowCardsCarryChipsAndOpenEvent |
| F-DASH-007 | P2 | [UI] | Kayıt var, Inspector dolu | "Aç" | Oturum doğrudan açılır | kısmen: DashRowCardsCarryChipsAndOpenEvent |
| F-DASH-008 | P3 | [UI] [UNIT] | Kayıtlar | Kart rozetleri ve renkler | Durum rozetleri; açık kusurlu kart vurgulu | DashRowCardsCarryChipsAndOpenEvent |
| F-DASH-009 | P2 | [UI] | Admin'de tip eklenmiş + kaydedilmiş, yeniden başlatılmamış | `FilterTypeBox` | Yeni tip yeniden başlatmaya kadar görünmez (bilinen davranış; restart uyarısıyla tutarlı) | — |
| F-DASH-010 | P3 | [MANUAL] | 500 oturum (geçici kökte betikle üretilmiş) | Yenile | 5 sn içinde; arayüz kilitlenmez | — |

### 4.19 Ayarlar: birim ve tema (F-SET)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-SET-001 | P2 | [UI] [UNIT] | K2 (mm), LOCALAPPDATA yedeği alınmış | `UnitRadio` → "inch (inç)"; Muayene; yeni kusur | #1 mm kalır; yeni kusur inch; seçim yokken başlıklar "(in)" | VmUnitFollowsSettingsPerDefect, VmUnitHeadersFollowSettingsWhenNoDefect, UserSettingsViewModelWritesAndRaises |
| F-SET-002 | P2 | [UI] | F-SET-001 | Uygulamayı yeniden başlat | Birim inch olarak açılır | — |
| F-SET-003 | P2 | [UI] | K1 | `ThemeRadio` → "Koyu"; bir diyalog aç; yeniden başlat | Ana pencere ve başlık çubuğu koyu; diyalog koyu; açılışta koyu | — |
| F-SET-004 | P3 | [UI] [MANUAL] | Koyu tema | Admin penceresini aç | Okunabilir olmalı. Admin'de sabit beyaz zemin (#FFFFFF) ve koyu lacivert metinler var; koyu temada kontrast sorunu olası (B-18) | — |
| F-SET-005 | P3 | [MANUAL] | Açık / koyu | Kamera ve düzenleyici | Kamera her zaman koyu; düzenleyici okunabilir | — |
| F-SET-006 | P2 | [UNIT] | `UserSettingsViewModel` | `UnitIndex=-1`, `ThemeIndex=2` | Ayar değişmez, olay gelmez | kısmen: UserSettingsViewModelWritesAndRaises |
| F-SET-007 | P2 | [UNIT] | inch kusur seçili | Ayarı mm yap | Seçili kusurun başlıkları "(in)" kalır; karşılaştırma inch | UnitCompareInch |
| F-SET-008 | P1 | [UNIT] | inch ayar | D ".0625" gir, rapor | Değer inch olarak saklanır (dönüştürülmez); raporda ".0625 in" | ReportShowsInchValues, UnitJsonOldAndRoundTrip |
| F-SET-009 | P2 | [UI] | inch kusurda `DepthBox`'ta yazılı ama kaydedilmemiş "0.5" | Ayarlar'da mm seç, Muayene'ye dön | Kutudaki metin dönüştürülmez; kusur inch kalır | — |
| F-SET-010 | P3 | [UI] | LOCALAPPDATA yedeği alınmış; `user-settings.json` içine çöp yazılmış | Uygulamayı başlat | Varsayılan (mm, açık); çökme yok. Testten sonra yedeği geri kopyala | — |

### 4.20 Windows kimliği (F-ID)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ID-001 | P2 | [UI] [UNIT] | Admin'de `WindowsUserSwitch` açık | Ana pencerede `ChangeSessionButton` (ya da yeniden başlat) | `InspectorBox` salt okunur, başlık "Inspector (Windows oturumu)", değer "Ad Soyad (sicil)" | VmInspectorFromWindowsUser, WindowsUserSwitchSavesSharedSetting |
| F-ID-002 | P1 | [UNIT] | Anahtar açık | `InspectorName="Sahte"` atayıp oturum aç | Oturum ve kararlar Windows adıyla yazılır (kutuya güvenilmez) | kısmen: VmInspectorFromWindowsUser |
| F-ID-003 | P2 | [UNIT] | Anahtar açıkken kapat | Muayene seçimine dön | Kutu temizlenir ve düzenlenebilir, başlık "Inspector" | VmInspectorFromWindowsUser |
| F-ID-004 | P2 | [UNIT] | Görünen ad yok ya da sicile eşit | `WindowsInspectorText` | Yalnızca sicil no | VmInspectorFromWindowsUser |
| F-ID-005 | P2 | [UNIT] | `_config\inspector_identity.json` bozuk | `RefreshIdentity` | Elle yazma modu; hata yok | — |
| F-ID-006 | P2 | [UI] | Anahtar açık, açık oturum var | Admin'de anahtarı değiştir | Açık oturumdaki kullanıcı değişmez; bir sonraki muayene seçiminde uygulanır | — |

### 4.21 Admin — giriş, kilit, pencere (F-ADM-LOGIN)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-LOGIN-001 | P1 | [UI] [UNIT] | Taze geçici kök | `AdminNavItem` | Admin penceresi; `LoginHint` "İlk kurulum: admin PIN'ini belirleyin (6-12 rakam)..."; `PinBox` + `PinRepeatBox`; düğme "PIN'i belirle ve gir"; `LoginRoot` "Ortak alan: <geçici kök>" (kök geçici değilse DUR) | FirstRunPinMustMatchAndIsAudited |
| F-ADM-LOGIN-002 | P1 | [UI] [UNIT] | F-ADM-LOGIN-001 | Test PIN'i + farklı tekrar | "PIN'ler aynı değil."; `admin.json` oluşmaz | FirstRunPinMustMatchAndIsAudited |
| F-ADM-LOGIN-003 | P1 | [UNIT] [UI] | İlk kurulum | "12345" (5), "abc123", "" (boş, iki kutu da), 13 haneli (UNIT; UI MaxLength 12 keser), "123456" (6), "123456789012" (12) | İlk dördü "PIN 6-12 haneli rakam olmalı."; 6 ve 12 hane kabul | ShortPinShowsRuleError |
| F-ADM-LOGIN-004 | P1 | [UI] [UNIT] | F-ADM-LOGIN-001 | Test PIN'i iki kutuya → giriş | Kayıtlar sayfası açılır; `_config\admin.json` (özet + tuz); denetim `admin.pin.set`, `admin.login` | FirstRunPinMustMatchAndIsAudited |
| F-ADM-LOGIN-005 | P1 | [UI] [UNIT] | PIN var | Yanlış PIN | "PIN hatalı."; `PinBox` temizlenir; yalnızca tek PIN kutusu, düğme "Giriş" | FiveWrongPinsWaitSixtySecondsThenRetry |
| F-ADM-LOGIN-006 | P1 | [UNIT] [UI] | PIN var | 5 yanlış; sonra doğru PIN; 60 sn sonra doğru PIN | 5.'den sonra "Çok fazla hatalı deneme. N sn bekleyin." (doğru PIN de reddedilir); denetim `admin.login.lockout`; süre dolunca giriş | FiveWrongPinsWaitSixtySecondsThenRetry, WrongPinCounterResetsAfterSuccess |
| F-ADM-LOGIN-007 | P2 | [UI] | PIN var | `PinBox`'a PIN yaz, Enter | Giriş yapılır | — |
| F-ADM-LOGIN-008 | P2 | [UI] | 4 yanlış deneme | Admin penceresini kapatıp yeniden aç; 1 yanlış daha | Sayaç pencere kapansa da sürer (singleton) → kilitlenir. Not (B-16): uygulama yeniden başlatılınca sayaç sıfırlanır | — |
| F-ADM-LOGIN-009 | P1 | [UNIT] [MANUAL] | Giriş yapılmış | 10 dk girdi yok (UNIT: `FakeClock.Advance(10dk+1sn)`, `CheckIdle`) | "10 dakika işlem yapılmadığı için kilitlendi."; giriş ekranı. MANUAL: 10.5 dk bekle | IdleLockAfterTenMinutesOfNoInput |
| F-ADM-LOGIN-010 | P1 | [UI] | Giriş yapılmış | Admin penceresini kapat, `AdminNavItem` ile yeniden aç | PIN tekrar sorulur | — |
| F-ADM-LOGIN-011 | P2 | [UI] [UNIT] | Giriş yapılmış | Menüde "Kilitle" | Giriş ekranı, hata satırında "Kilitlendi." | NavigationPagesLockAndRefresh |
| F-ADM-LOGIN-012 | P2 | [UI] | Admin penceresi açık (arkada) | `AdminNavItem`'a tekrar tıkla | İkinci pencere açılmaz; mevcut pencere öne gelir | — |
| F-ADM-LOGIN-013 | P2 | [UNIT] | PIN var | `admin.json` silinir (geçici kök) → `Lock(null)` | İlk kurulum ekranı tekrar görünür: ortak alana yazabilen herkes PIN'i sıfırlayabilir — ürün kararı olarak raporla (B-19) | — |
| F-ADM-LOGIN-014 | P3 | [UNIT] | `admin.json` Iterations=5000'e değiştirilmiş | Doğru PIN | "PIN hatalı." (zayıf kayıt kabul edilmez) | kısmen: AdminPinRules |
| F-ADM-LOGIN-015 | P2 | [UI] | Kusur tipleri kaydedilmiş ya da kök değiştirilmiş | Admin penceresini kapat | Ana pencerede Warning: "Admin'de kusur tipleri ya da ortak alan değişti. ... yeniden açın."; yalnızca karar metni/PIN değiştiyse uyarı yok | — |

### 4.22 Admin — Kayıtlar (F-ADM-REC)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-REC-001 | P2 | [UI] [UNIT] | 3 oturum | Girişten sonra Kayıtlar | 4 özet kartı (Kayıt, Açık kusurlu, MRB bekleyen, Tamamlanan); kartlarda başlık, meta, kusur çipleri, "Klasörü aç", "Son rapor" | RecordsFiltersSummaryAndCards |
| F-ADM-REC-002 | P2 | [UI] [UNIT] | F-ADM-REC-001 | `FPart`, `FOp`, `FSerial`, `FType`, `FState` | Süzme anında; "Kriterlere uyan kayıt yok." | RecordsFiltersSummaryAndCards |
| F-ADM-REC-003 | P2 | [UNIT] | 600 oturum (UC ile üret) | Refresh; CSV | 500 kart; özet 600'ü sayar; CSV 600 satır | — |
| F-ADM-REC-004 | P1 | [UI] [UNIT] | Süzgeç uygulanmış | "CSV olarak dışa aktar" → scratch'e kaydet | Önerilen ad `MuayeneKayit_yyyyMMdd_HHmm`; UTF-8 BOM; yalnızca süzülmüş satırlar; "N kayıt dışa aktarıldı: <yol>" | RecordsExportCsvWritesBomAndFilteredRows |
| F-ADM-REC-005 | P2 | [UNIT] [UI] | — | Kaydetme seçicisinde İptal; salt okunur hedef dosya | İptal: hiçbir şey. Salt okunur: Error mesajı, çökme yok | — |
| F-ADM-REC-006 | P1 | [UNIT] | `=CMD()` içerikli alanlar | CSV | Formül etkisiz (öne tırnak / kaçış) | CsvEscapes |
| F-ADM-REC-007 | P2 | [UI] [UNIT] | Kart | "Klasörü aç"; klasör silinmiş (geçici kökte) kart | Gezgin geçici seri klasöründe açılır (testten sonra yalnızca o pencere kapatılır); silinmişte "Klasör bulunamadı." | RecordsOpenFolderAndLatestReport |
| F-ADM-REC-008 | P2 | [UI] [UNIT] | Raporlu / raporsuz kart | "Son rapor" | PDF varsa PDF, yoksa HTML açılır; raporsuz "Bu kayıt için rapor yok." | RecordsOpenFolderAndLatestReport |
| F-ADM-REC-009 | P2 | [UI] | Inspector'da yeni oturum açılmış | Kayıtlar → "Yenile" | Yeni oturum görünür | — |
| F-ADM-REC-010 | P1 | [UNIT] | Kayıtlar sayfası işlemleri | Önce/sonra kök dosya karması | Yalnızca `_index.json` ve `_audit.log` değişir; muayene kayıtları değişmez | — |

### 4.23 Admin — Kusur tipleri (F-ADM-TYPE)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-TYPE-001 | P2 | [UI] [UNIT] | Giriş, Kusur tipleri | `TypeList`'te Dent'i seç | Satır "Dent   (D, W, L)"; editörde ad, spec, işaretli alanlar | TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-002 | P2 | [UI] [UNIT] | — | "Yeni tip" iki kez | "Yeni tip 1", "Yeni tip 2"; D/W/L işaretli; `TypesDirty` "Kaydedilmemiş değişiklik var..." | TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-003 | P2 | [UNIT] [UI] | Tip seçili | Ad: "" / "   " / 40 karakter / 41 karakter (UNIT) / "Dent#" / "Çapak-1 (iç)" / "dent" (başka tipte "Dent" varken) / "Dent " → "Şablonu uygula" | Boş, 41, "Dent#": "Geçersiz kusur tipi adı..."; 40 ve Türkçe kabul; "dent" ve "Dent " (kırpılınca): "Aynı ad iki kez var: ..." | kısmen: DefectTypeTemplates |
| F-ADM-TYPE-004 | P2 | [UNIT] | Tip seçili | Spec 60 / 61 karakter | 61: "<ad>: spec adı en fazla 60 karakter." | — |
| F-ADM-TYPE-005 | P2 | [UNIT] | Seçim yok | Şablonu uygula | "Listeden bir tip seçin." | — |
| F-ADM-TYPE-006 | P2 | [UNIT] | Tip seçili | Tüm alanları kaldır → uygula | Kabul; satırda "ölçüm yok" (muayene tarafı için F-TAB-006) | — |
| F-ADM-TYPE-007 | P2 | [UI] [UNIT] | İlk tip seçili; son tip seçili | Yukarı (ilkte), Aşağı (sonda); ortada Aşağı | Uçlarda değişiklik yok; ortada yer değişir, seçim taşınan tipte | TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-008 | P2 | [UI] [UNIT] | Tip seçili | Sil → Vazgeç; Sil → "Sil" | Vazgeç: liste aynı; Sil: tip gider, kirli | TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-009 | P2 | [UNIT] | — | Tüm tipleri sil → kaydet | "En az bir kusur tipi olmalı."; dosya yazılmaz | — |
| F-ADM-TYPE-010 | P2 | [UNIT] | — | 100 tip kaydet; 101 tip kaydet | 100 kabul; 101 "En fazla 100 kusur tipi tanımlanabilir." | — |
| F-ADM-TYPE-011 | P2 | [UI] [UNIT] | Değişiklik var, ek alan işaretli | Varsayılanlara dön → Vazgeç; tekrar → onay | Vazgeç değişmez; onay: 9 varsayılan tip, ek alanlar silinmez ama işaretsiz | TypesResetToDefaultsAsksConfirm |
| F-ADM-TYPE-012 | P1 | [UI] [UNIT] | Değişiklik var | "Tümünü ortak alana kaydet" | `measure_fields.json` ve `defect_types.json` yazılır; denetim `config.measure_fields`, `config.defect_types`; kirli metni silinir; Kayıtlar tip süzgeci yenilenir | TypesAddApplyMoveDeleteSave |
| F-ADM-TYPE-013 | P3 | [UI] | Uygulanmış ama kaydedilmemiş değişiklik | Admin penceresini kapat, yeniden giriş yap | Değişiklik uyarısız kaybolur (yeniden girişte `Types.Load`) — raporla (B-17) | — |
| F-ADM-TYPE-014 | P2 | [UNIT] | "Dent" tipli kayıtlı kusur | Tipi "Göçük" olarak yeniden adlandır, kaydet; muayene VM'i yeni tiplerle | Eski kusur "Dent" adını korur; şablon bulunamadığından tüm alanlar görünür (F-DEF-012) | — |
| F-ADM-TYPE-015 | P2 | [UI] | Kaydedilmiş tipler | Muayene ekranında `DefectTypeBox` (yeniden başlatmadan / sonra) | Yeniden başlatmadan eski liste; sonra yeni liste | — |
| F-ADM-TYPE-016 | P2 | [UNIT] | Tip seçili | `Name`'i değiştir, "Şablonu uygula"ya basmadan "Tümünü ortak alana kaydet" | **ŞÜPHELİ (B-11)**: Değişiklik kaydedilmeli ya da "uygulanmamış değişiklik" uyarısı verilmeli. Bugünkü kod: `Save` editördeki değeri değil listeyi yazar; ad değişikliği sessizce kaybolur, "kaydedildi" mesajı gelir | — |
| F-ADM-TYPE-017 | P3 | [UNIT] | İki admin bağlamı aynı kökte | İkisi de farklı tip listesi kaydeder | Son kaydeden kazanır; JSON geçerli | — |

### 4.24 Admin — Ek ölçüm alanları (F-ADM-FIELD)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-FIELD-001 | P2 | [UI] [UNIT] | Kusur tipleri sayfası | `FieldNameBox`="Yükseklik", `FieldAbbrevBox`="H", `FieldKindBox`="uzunluk (mm / in)" → `AddFieldButton` | `FieldList` "Yükseklik   (H · uzunluk (mm / in))"; bilgi mesajı; şablonda "Yükseklik (H)" onay kutusu | TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-002 | P2 | [UNIT] | — | Ad "", 30 karakter, 31 karakter, "Yük#" | Boş, 31, "Yük#": "Geçersiz alan adı..."; 30 kabul | kısmen: FieldDefinitionsSaveAndValidate |
| F-ADM-FIELD-003 | P2 | [UNIT] | — | Kısaltma "", "ABCDEFG", "H-1", "D", "d", "w", "L", "Ø", "Ç", "a", "H°", "Ğ" | "", 7 karakter, "H-1": "kısaltma 1-6 harf ya da rakam olmalı"; D/d/w/L/Ø/Ç/a: "yerleşik bir alana ait"; "H°" ve "Ğ" kabul | kısmen: FieldDefinitionsSaveAndValidate |
| F-ADM-FIELD-004 | P2 | [UNIT] | "Yükseklik / H" var | "yükseklik / X" ekle; "Kalınlık / h" ekle | "Aynı alan adı iki kez var" / "Aynı kısaltma iki kez var" | kısmen: FieldDefinitionsSaveAndValidate |
| F-ADM-FIELD-005 | P2 | [UNIT] | — | 30 alan; 31. alan | 31.: "En fazla 30 ek ölçüm alanı tanımlanabilir." | — |
| F-ADM-FIELD-006 | P2 | [UNIT] | — | `FieldKindIndex` = 0..3 ve 9 | 0..3 → Length/Angle/Number/Text; 9 → Length'e düşer | — |
| F-ADM-FIELD-007 | P2 | [UNIT] [UI] | Seçim yok; seçili alan | "Seçili alanı güncelle" | Seçimsiz: "Listeden bir ölçüm alanı seçin."; seçili: ad/kısaltma güncellenir, anahtar aynı; kayıtlı kusurlardaki değerler eski adla kalır | — |
| F-ADM-FIELD-008 | P2 | [UNIT] | Değeri olan alan | Türü uzunluk → metin, kaydet | Kayıtlı değerler bozulmamalı (muayene tarafı: F-CUST-009, B-12) | — |
| F-ADM-FIELD-009 | P2 | [UI] [UNIT] | Dent ve Nick'te işaretli alan | "Seçili alanı sil" → Vazgeç; → "Sil" | Mesaj "... şu tiplerden kaldırılacak: Dent, Nick"; Vazgeç değişmez; Sil: alan ve işaretler gider; kayıtlı değerler raporda görünmeye devam eder | TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-010 | P2 | [UNIT] | Şablonda olmayan anahtar | Kaydet | Geçersiz anahtarlar şablonlardan ayıklanır; önce alanlar, sonra tipler yazılır | kısmen: TypesCustomFieldsAddCheckSaveDelete |
| F-ADM-FIELD-011 | P2 | [UNIT] | Hiçbir tipte işaretli olmayan alan | Muayene VM: şablonlu tip ve şablonsuz (bilinmeyen) tip | Şablonlu tipte kutu yok; şablonsuz tipte tüm ek alanlar | — |

### 4.25 Admin — Karar açıklamaları (F-ADM-TXT)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-TXT-001 | P2 | [UI] [UNIT] | Giriş | Karar açıklamaları → Accept metnini değiştir → "Ortak alana kaydet" | 10 satır (muayene ekranındaki adlarla); kirli metni; kayıt: `decision_texts.json`, denetim `config.decision_texts`; muayene ekranında bir sonraki Kabul diyaloğunda yeni metin (yeniden başlatmadan) | TextsEditSaveAndResetAll |
| F-ADM-TXT-002 | P2 | [UNIT] [UI] | — | Bir metni boşalt → kaydet | "Accept: Açıklama boş olamaz."; dosya yazılmaz | — |
| F-ADM-TXT-003 | P2 | [UNIT] | — | 600 / 601 karakter (UI MaxLength 600) | 601: "Açıklama en fazla 600 karakter olabilir." | — |
| F-ADM-TXT-004 | P2 | [UNIT] | — | Metne sekme (\t) ve satır sonu | Sekme: "Açıklamada kontrol karakteri olamaz."; satır sonu kabul | — |
| F-ADM-TXT-005 | P2 | [UI] [UNIT] | Değiştirilmiş metin | Satırdaki "Varsayılan" | Metin varsayılana döner, kirli; kaydedilmeden kalıcı değil | kısmen: TextsEditSaveAndResetAll |
| F-ADM-TXT-006 | P2 | [UI] [UNIT] | Değişiklikler | "Tümünü varsayılana döndür" → Vazgeç; → onay | Vazgeç değişmez; onay: varsayılanlar + "Varsayılanlar yüklendi; kalıcı olması için 'Ortak alana kaydet'e basın." | TextsEditSaveAndResetAll |
| F-ADM-TXT-007 | P2 | [UNIT] | Bozuk / tek kararlı dosya | Yükle | Bozuk: varsayılanlar; tek karar: o karar dosyadan, diğerleri varsayılan | DecisionTextRules |
| F-ADM-TXT-008 | P3 | [UI] | Kaydedilmemiş değişiklik | Başka sayfaya git, geri dön | Değişiklik uyarısız kaybolur (sayfa açılışında yeniden yüklenir) — raporla (B-17) | — |

### 4.26 Admin — Bakım (F-ADM-MNT)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-MNT-001 | P2 | [UI] [UNIT] | 3 oturum | "Index'i yeniden oluştur" | "Index yeniden oluşturuldu: 3 kayıt." | MaintenanceRebuildAndVerifyAudit |
| F-ADM-MNT-002 | P1 | [UI] [UNIT] | Sağlam / bozuk denetim | "Denetim zincirini doğrula" | "Denetim zinciri sağlam." / "Denetim zinciri N. satırda bozuk..." (Error); liste yenilenir | MaintenanceRebuildAndVerifyAudit |
| F-ADM-MNT-003 | P2 | [UNIT] | 301 satırlık denetim, bir satır bozuk JSON | `LoadAudit` | En fazla 300 satır, yeniden eskiye; bozuk satır atlanır; tarih "gg.AA.yyyy SS:dd:ss" | — |
| F-ADM-MNT-004 | P2 | [UI] [UNIT] | Kilit yok; sonra geçici kökte `PD170\OP40\SN001\session.lock` LastWriteTime = şimdi-11 dk | "Kilitleri tara" | Önce "Eski kilit yok." (Success); sonra kilit listede, "1 eski kilit bulundu." (Warning) | MaintenanceStaleLockRemovalNeedsConfirm |
| F-ADM-MNT-005 | P2 | [UI] [UNIT] | Liste dolu, seçim yok | "Seçili kilidi sil" | "Listeden bir kilit seçin." | — |
| F-ADM-MNT-006 | P2 | [UI] [UNIT] | Kilit seçili | Sil → Vazgeç; → "Kilidi sil" | Vazgeç: dosya durur; onay: dosya silinir, "Kilit silindi.", denetim `admin.lock.remove` | MaintenanceStaleLockRemovalNeedsConfirm |
| F-ADM-MNT-007 | P2 | [UNIT] | Taramadan sonra kilidin tarihi şimdiye çekilmiş | Sil → onay | "Kilit henüz eski değil; bir kullanıcı şu an yazıyor olabilir." | StaleLockRules |
| F-ADM-MNT-008 | P1 | [UNIT] | — | `RemoveStaleLock` kök dışı yol, `.txt` dosyası, `..\` içeren yol | "Yalnızca ortak alandaki kilit dosyaları silinebilir."; hiçbir şey silinmez | StaleLockRules |
| F-ADM-MNT-009 | P2 | [UNIT] | Kilit 9 dk eski | Tara | Listelenmez (sınır 10 dk) | kısmen: StaleLockRules |

### 4.27 Admin — Ayarlar (F-ADM-SET)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-ADM-SET-001 | P1 | [UI] [UNIT] | Giriş; `root2` var | `RootBox`=`root2` → "Kaydet ve yeniden bağlan" | Scratch kopyadaki `MuayeneKayit.settings.json` `root2` olur; oturum kilitlenir: "Ortak alan değişti; yeni alandaki PIN ile tekrar giriş yapın."; `LoginRoot` root2; root2'de PIN yok → ilk kurulum. Test sonunda settings'i ilk köke geri al | SettingsRootMustExistThenLocksSession |
| F-ADM-SET-002 | P1 | [UNIT] [UI] | — | Var olmayan yol | "Klasöre erişilemiyor. Yolu ve ağ bağlantısını kontrol edin."; settings değişmez; oturum açık kalır | SettingsRootMustExistThenLocksSession |
| F-ADM-SET-003 | P2 | [UNIT] | — | "" ve "   " | Erişilemiyor hatası; settings değişmez | — |
| F-ADM-SET-004 | P2 | [UNIT] | `%TEMP%` altında klasör | `RootBox`="%TEMP%\\<klasör>" | Kontrol genişletilmiş yolla yapılır; dosyaya ham (genişletilmemiş) metin yazılır; `InstallSettings.LoadRootPath` genişletir | — |
| F-ADM-SET-005 | P2 | [UNIT] | CWD altında `rel` klasörü | `RootBox`="rel" ya da "." | **ŞÜPHELİ (B-09)**: Göreli yol reddedilmeli. Bugünkü kod: `Directory.Exists` CWD'ye göre geçerse kabul eder ve göreli yolu kaydeder; sonraki açılışta veriler programın çalışma klasörüne yazılır | — |
| F-ADM-SET-006 | P2 | [UNIT] | — | 261 karakterlik (var olan) yol | "Ortak alan yolu geçersiz." mesajı; çökme yok | — |
| F-ADM-SET-007 | P2 | [UI] | F-ADM-SET-001 | Admin'i kapat; ana pencerede yeni oturum aç | Restart uyarısı; muayene ekranı yeniden başlatılana kadar eski köke yazar (kasıtlı davranış; doğrula) | — |
| F-ADM-SET-008 | P1 | [UI] [UNIT] | Giriş | Mevcut PIN yanlış / yeni PIN'ler farklı / yeni PIN "12ab56" / doğru değerler | "Mevcut PIN hatalı." / "Yeni PIN'ler aynı değil." / "PIN 6-12 haneli rakam olmalı." / "PIN değiştirildi.", kutular temizlenir, denetim `admin.pin.change`, eski PIN artık çalışmaz | SettingsChangePinValidatesOldAndRepeat |
| F-ADM-SET-009 | P2 | [UI] [UNIT] | Giriş | `WindowsUserSwitch` aç / kapat | Anında kaydedilir; mesaj; denetim `admin.identity windows/manual` | WindowsUserSwitchSavesSharedSetting |
| F-ADM-SET-010 | P2 | [UNIT] | `IAdminSettings.SaveRootPath` IOException fırlatan sahte (yalnızca tests/ içinde) | SaveRoot | Error mesajı; kök değişmez; kilitlenmez | — |

### 4.28 Eşzamanlılık — iki kullanıcı aynı oturum klasöründe (F-CONC)

UNIT yöntemi: aynı geçici kök üzerinde iki ayrı `SessionStore` + iki `InspectionViewModel` (iki bilgisayarı temsil eder). UI yöntemi: scratch kopyadan iki örnek başlat (ikisi aynı settings → aynı geçici kök; iki PID kaydet).

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-CONC-001 | P1 | [UNIT] [UI] | İki VM aynı oturumda | Aynı anda 20'şer kusur ekle | Numaralar tekil, ardışık; hiçbir dosya kaybolmaz | ParallelAddDefectsGetUniqueIds |
| F-CONC-002 | P1 | [UNIT] | Aynı kusur | Paralel 50 güncelleme | Hiçbir güncelleme kaybolmaz (store düzeyi) | ParallelUpdatesSameDefectLoseNothing |
| F-CONC-003 | P1 | [UNIT] | — | Paralel denetim yazımı | Zincir sağlam | ParallelAuditAppendsKeepChain |
| F-CONC-004 | P1 | [UNIT] [UI] | A ve B aynı kusuru açmış (D 0.1) | A: D=0.5 kaydet. B (ekranı yenilenmemiş): yalnızca Not yaz, kutudan çık | **ŞÜPHELİ (B-03)**: D 0.5 kalmalı (B yalnızca notu değiştirdi) ya da çakışma bildirilmeli. Bugünkü kod: B'nin eski formu tümüyle yazılır, D sessizce 0.1'e döner (geçmişte "B 0.5→0.1") | — |
| F-CONC-005 | P1 | [UNIT] [UI] | A ve B aynı Açık kusurda | A Accept verir; B (eski ekran) Rework seçer | B'ye "'Kapandı' durumunda 'Rework' kararı verilemez." hatası; ikinci karar yazılmaz | — |
| F-CONC-006 | P1 | [UNIT] | A ve B aynı oturumda | B kusur eklerken A muayeneyi tamamlar (sırayla: A Complete, B AddDefect; ve tersi) | Tamamlanmış oturumda açık kusur kalmamalı (B-04 ile aynı kök neden; `SaveSession` kilitsiz) | — |
| F-CONC-007 | P2 | [UNIT] | Aynı kusur | A ve B aynı anda fotoğraf ekler (paralel `AddPhotoFromFileAsync`) | İkisi de eklenmeli ya da biri anlaşılır hata almalı; json bozulmaz. Bugünkü kod: ad sayımla üretildiği için biri IOException alabilir (B-10) | — |
| F-CONC-008 | P2 | [UI] | K2; PowerShell ile geçici kökte `session.lock` `FileShare.None` açık tutulur | 5 sn tut → `SaveMeasButton`; 20 sn tut → `SaveMeasButton` | 5 sn: işlem bekler sonra başarılı. 20 sn: 15 sn sonra "Kilit alınamadı: ..." hatası. Not (B-07): bekleme UI iş parçacığında, pencere 15 sn yanıt vermez | — |
| F-CONC-009 | P1 | [UI] [UNIT] | Geçici kökte açık olmayan `session.lock`, LastWriteTime -11 dk | Kusur ekle | "Kilit 10 dakikadan eski, admin kontrol etmeli: ..."; Admin Bakım'dan silince işlem başarılı | kısmen: StaleLockRules |
| F-CONC-010 | P2 | [UNIT] | İki admin bağlamı | Aynı anda tip kaydı | Son yazan kazanır; JSON geçerli (atomik yazım) | — |
| F-CONC-011 | P2 | [UNIT] | B sürekli kusur güncellerken | A `Dashboard.Refresh` | Çökme yok; okuma atomik yazım sayesinde tutarlı | — |
| F-CONC-012 | P2 | [UI] | A ve B aynı kusurda | B karar verir; A listeden başka kusura geçip geri döner | A yeni durumu görür | — |
| F-CONC-013 | P2 | [UNIT] | A ve B aynı oturum | Aynı anda rapor | **ŞÜPHELİ (B-20)**: İki farklı sürüm (v1, v2) üretilmeli. Bugünkü kod: ikisi de aynı sürüm numarasını hesaplar; aynı dosyaya yazarlar, biri HTML yedeğine düşebilir | — |

### 4.29 Bozuk / eksik yapılandırma ve veri dosyaları (F-CFG)

Tüm dosya bozmaları **yalnızca geçici kökte** yapılır.

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-CFG-001 | P1 | [UNIT] yalnızca | Geçici klasörde ayar dosyası yok | `InstallSettings.LoadRootPath(tmp)` | `%USERPROFILE%\MuayeneKayitDemo` yolu döner (yalnızca dönen metin kontrol edilir; klasöre dokunulmaz). UI'da asla denenmez | — |
| F-CFG-002 | P1 | [UNIT] yalnızca | Ayar dosyası: çöp, `{}`, `{"rootPath":""}`, `{"rootPath":5}` | `LoadRootPath` | Varsayılan yol; istisna yok | kısmen: InstallSettingsFoundInParentFolders |
| F-CFG-003 | P2 | [UNIT] | `{"rootPath":"%TEMP%\\x"}`; 3 seviye yukarıda ve 4 seviye yukarıda dosya | `LoadRootPath` | Genişletilir; 3 seviyede bulunur, 4'te bulunmaz | InstallSettingsFoundInParentFolders |
| F-CFG-004 | P1 | [UNIT] | `defect_types.json`: çöp / yinelenen ad / `[]` | `LoadDefectTypes` | Varsayılan 9 tip | DefectTypeTemplates |
| F-CFG-005 | P2 | [UNIT] | `measure_fields.json`: çöp; 3 alandan biri yerleşik kısaltmalı | `LoadCustomFields` | Çöp: boş liste. Tek geçersiz alan tüm listeyi düşürür (2 geçerli alan da kaybolur) — raporla (B-21); kayıtlı değerler yetim olarak görünür kalır | kısmen: FieldDefinitionsSaveAndValidate |
| F-CFG-006 | P2 | [UNIT] | `decision_texts.json` çöp | Yükle | Varsayılanlar | DecisionTextRules |
| F-CFG-007 | P2 | [UNIT] | `inspector_identity.json` çöp | `LoadUseWindowsUser` | false | — |
| F-CFG-008 | P1 | [UNIT] | `admin.json` çöp metin; ayrıca geçerli JSON ama `Salt`="%%%" (base64 değil) | `Login.Pin=...; Login.Login()` | **ŞÜPHELİ (B-02)**: `LoginError`'da anlaşılır hata, istisna dışarı kaçmaz. Bugünkü kod: `JsonException` / `FormatException` yakalanan türlerde değil → işlenmeyen istisna → uygulama çöker | — |
| F-CFG-009 | P1 | [UNIT] [UI] | Oturum diskte; `session.json` çöp metin; ayrıca 0 bayt | `OpenSession` | **ŞÜPHELİ (B-02)**: Error mesajı ("dosya bozuk"), çökme yok. Bugünkü kod: `JsonException` VM `Run` tarafından yakalanmaz → çökme. (`null` içerikli dosya InvalidDataException ile yakalanır — karşılaştırma için) | — |
| F-CFG-010 | P1 | [UNIT] [UI] | `defects\2.json` çöp | Oturumu aç; ayrıca Dashboard `Refresh`, Admin Kayıtlar `Refresh`, Bakım "Index'i yeniden oluştur" | **ŞÜPHELİ (B-02)**: Bozuk dosya raporlanmalı, diğer kusurlar/oturumlar görünmeli. Bugünkü kod: `JsonException` → çökme; kökteki tek bozuk dosya Dashboard'u ve Admin Kayıtlar'ı da düşürür | — |
| F-CFG-011 | P1 | [UNIT] | Gerçek `FileAuditTrail` ile VM; `_audit.log` son satırı yarım JSON (ağ kopmasında olası) | Oturum aç / kusur ekle | **ŞÜPHELİ (B-02)**: İşlem sürmeli ya da anlaşılır hata vermeli. Bugünkü kod: `AuditLog.LastHash` her yazımda `JsonException` fırlatır → her denetlenen işlemde çökme; ayrıca kayıt yazıldıktan sonra fırladığından kayıt denetimsiz kalır | — |
| F-CFG-012 | P2 | [UNIT] | Ortadaki bir satırı değiştirilmiş `_audit.log` | Verify; ardından yeni Append | Verify satır numarasını verir; yeni satır eklenebilir | AuditChainDetectsTamper |
| F-CFG-013 | P1 | [UNIT] | U | `NotesText` = 6 milyon karakter → kaydet → oturumu yeniden aç | **ŞÜPHELİ (B-05)**: Yazımda sınır uygulanmalı (ör. not uzunluğu). Bugünkü kod: yazım serbest, dosya 5 MB'ı aşar; sonraki okumada "Dosya boyut sınırını aşıyor" → oturum açılamaz hâle gelir | — |
| F-CFG-014 | P2 | [UNIT] | `session.json` içeriği `null` | OpenSession | "Dosya boş ya da bozuk: ..." Error; çökme yok | — |
| F-CFG-015 | P3 | [UNIT] | `_index.json` çöp | Dashboard Refresh | Yeniden üretilir, hata yok | — |
| F-CFG-016 | P2 | [UNIT] | `hidden-suggestions` JSON çöp | `HiddenSuggestionSet.FromJson` | Boş küme | HiddenSuggestionSetRulesAndJson |
| F-CFG-017 | P3 | [UI] | LOCALAPPDATA yedeği alınmış; `user-settings.json` çöp | Başlat | Varsayılanlar; sonra yedeği geri kopyala (F-SET-010 ile birleşik) | — |
| F-CFG-018 | P3 | [MANUAL] | `camera.json` çöp (yedekli) | Kamerayı aç | Tercih yok sayılır, normal seçim | — |
| F-CFG-019 | P2 | [UNIT] | Kayıtta listelenen foto diskte yok | Seç, kartlar, rapor | Küçük resim boş; çökme yok; rapor üretilir | — |
| F-CFG-020 | P3 | [UNIT] | `defects\notlar.json` = `{}` (kusur olmayan JSON) | Oturumu aç | Yabancı JSON yok sayılmalı; bugünkü kod "#0" adlı boş kusur gösterebilir — raporla | — |
| F-CFG-021 | P2 | [UNIT] | `_config` klasörü yok | Admin girişi, tip yükleme, metinler | İlk kurulum; varsayılan tipler ve metinler | kısmen: FirstRunPinMustMatchAndIsAudited |

### 4.30 Ağ yolu kaybı (F-NET) — geçici kökü yeniden adlandırarak

Yöntem: uygulama açıkken `Rename-Item $Root ($Root + "_gone")`; sonra geri adlandır. UNIT: `Directory.Move` ile aynısı.

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-NET-001 | P1 | [UNIT] [UI] | K2 | Kökü kaldır → kusur ekle; kökü geri getir → kusur ekle | Önce Error (dizin bulunamadı), çökme yok; geri gelince yeniden başlatmadan çalışır | — |
| F-NET-002 | P1 | [UNIT] [UI] | K2 | Kökü kaldır → `DepthBox`=0.3, Tab; geri getir; kutuya girip çık | Önce hata, değer kutuda kalır; geri gelince kaydedilir | — |
| F-NET-003 | P1 | [UNIT] [UI] | K1'den sonra kök kaldırılmış | Yeni P/O/S ile oturum aç | **ŞÜPHELİ (B-22)**: "Ortak alana erişilemiyor" hatası beklenir. Bugünkü kod: `OpenOrCreate` `Directory.CreateDirectory` ile kök yolunu baştan oluşturur → kayıt yeni, boş bir köke yazılır (yerel / eşlenmiş sürücüde veri bölünür) | — |
| F-NET-004 | P1 | [UNIT] | U, kök kaldırılmış | Rapor | Error; `IsReportRunning` false; düğme yeniden açılır | — |
| F-NET-005 | P2 | [UNIT] [UI] | Kök kaldırılmış | Dashboard Yenile | Error mesajı; çökme yok | — |
| F-NET-006 | P1 | [UNIT] [UI] | PIN'li kök kaldırılmış | Admin'i aç | **ŞÜPHELİ (B-22)**: "Ortak alana erişilemiyor" mesajı beklenir. Bugünkü kod: `HasPin` false döner → ilk kurulum ekranı; PIN belirlenirse kök yolu yeniden oluşturulup yeni PIN yazılır | — |
| F-NET-007 | P2 | [UNIT] | K2, kök kaldırılmış | Dosyadan foto ekle | Error; kaynak dosya etkilenmez | — |
| F-NET-008 | P2 | [UI] | Ağ yavaş (MANUAL gerçek ağ) | İşlemler | Kilit zaman aşımında anlaşılır hata | — |
| F-NET-009 | P1 | [MANUAL] | İnsanın kendi test UNC paylaşımı | Oturum açıkken bağlantıyı kes / geri getir | F-NET-001..006 ile aynı beklentiler; UNC'de klasör yeniden oluşturulamadığı için B-22 görülmemeli | — |

### 4.31 Klavye ve diyalog güvenliği (F-KEY)

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-KEY-001 | P1 | [UI] | Her ContentDialog (karar, rework şimdi, reinspect, geri al, void, foto kaldır) | Açılınca hiçbir şeye dokunmadan Enter | Hiçbir diyalogda kayıt / silme olmaz | — |
| F-KEY-002 | P1 | [UI] | Her ContentDialog | Esc | Vazgeç ile aynı; "Rework şimdi" diyaloğunda "Sonra" ile aynı | — |
| F-KEY-003 | P2 | [UI] | Düzenleyici | Ctrl+Z, Delete | F-EDIT-005 / 007 | — |
| F-KEY-004 | P2 | [MANUAL] | Kamera | Space / Enter / Esc | F-CAM-005 / 006 / 015 | — |
| F-KEY-005 | P3 | [UI] | Giriş ekranı | Tab sırası | Parça → Operasyon → Seri → Inspector → Create Inspection | — |
| F-KEY-006 | P2 | [UI] | Karar diyaloğu açık | Ana pencereyi Alt+F4 | Karar kaydedilmez; bekleyen form değeri kaydedilir; çökme yok | — |
| F-KEY-007 | P3 | [UI] | Ana pencere | Tüm ikonlu düğmelerin UIA Name'i | "Başka muayene", "Rapor (PDF)", "Muayeneyi tamamla", "<dosya> fotoğrafını kaldır", "Son kararı geri al", "Öneriyi gizle" dolu | — |

### 4.32 Uçtan uca senaryolar (F-E2E)

Her senaryo hem [UNIT] (tek test içinde VM ile, sahte diyalog cevapları kuyruğa konarak) hem [UI] (gerçek uygulama) olarak koşulabilir. UI sürümünde her adımın sonunda `StatusInfoBar` metni ve geçici kökteki json kontrol edilir.

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-E2E-001 | P1 | [UI] [UNIT] | K0 | Yeni oturum → Dent ekle → D 0.12 / W 1.5 / L 3 kaydet → `ok.jpg` ekle → İşaretle: kutu çiz, kaydet → Kabul (SPA, ref SPA-1) → Tamamla → Rapor → Dashboard → Admin Kayıtlar "Son rapor" → Dashboard "Denetim kaydını doğrula" | Her adım başarı mesajı; durum Kapandı; oturum Tamamlandı; `Rapor_v1.pdf`; Dashboard'da Tamamlanan 1; Admin kartında rapor açılır; zincir sağlam; denetimde sırasıyla session.open, defect.add, defect.measure, defect.photo, defect.markup, disposition, session.complete, report.create | kısmen: VmOpensNewSession … VmReportWritesHtmlAndOpensPdf (ayrı ayrı) |
| F-E2E-002 | P1 | [UI] [UNIT] | K3 + High metal | Rework (kapsam "Kusur + high metal", hedef OP50) → "Sonra" → uygulamayı kapat-aç → Dashboard "Aç" → `ReinspectButton` → "Ana kusur hâlâ var", HM "Giderildi", D 0.2 → Kaydet → Rework (hedef OP50) → "Sonucu şimdi gir" → "Kusur giderildi" → Tamamla → Rapor | Yeniden açılışta kusur bekliyor; aynı kusur sonrası Açık ve değişim satırı (D 0.12→0.2); son durum Kapandı; raporda geçmiş ve değişim | Scenario1_SameDefectThenConforms, VmReinspectSameHighMetalGoneKeepsDepth |
| F-E2E-003 | P1 | [UI] [UNIT] | K1 | Undercut ekle, ölç, foto → Rework → şimdi gir → "Yeni hata": Dent + Scratch → #2, #3 için ölçüm + foto + Kabul → Tamamla → Rapor | #1 Devredildi; #2 seçili gelir; #2/#3 kapanınca tamamlama geçer; raporda "Defect 1 continues as Defect 2 and 3" | Scenario2_NewDefectChild, MultipleNewDefectsLinkAndGate |
| F-E2E-004 | P1 | [UI] [UNIT] | K2 + ölçüm | 3 foto ekle → Rework: bölge 1.3'ü kaldır → şimdi gir → 1.1 Giderildi, 1.2 Yeni hata (Nick) → Kaydet → #2 Nick'i kapat → Tamamla → Rapor | Kart rozetleri (1.3 Giderildi kararla, 1.1 Giderildi, 1.2 Yeni hata → #2); #1 durumu bölgelere göre; raporda bölge satırları | MixedRegionOutcomes, VmReinspectPerRegionOutcomes |
| F-E2E-005 | P1 | [UI] [UNIT] | K3 | Submit MRB → Tamamla (engellenir) → MRB red → Tamamla → "Yeniden aç" seçeneğini ara | Tamamlama ilk seferde uyarı; MRB red sonrası otomatik hurda; tamamlanır; Yeniden aç yok | Scenario4_MrbRejected |
| F-E2E-006 | P1 | [UI] [UNIT] | K3 | Yanlışlıkla Scrap → Geri al ("yanlış seçim") → Rework → şimdi gir → Yeni hata (Dent) → #2'yi Geri al (parent'ta) | Scrap geri alınır (Açık); devir geri alınınca #2 void, #1 tekrar bekliyor; geçmişte tüm satırlar | UndoChildLinkVoidsChild, VmUndoChildTransferVoidsChild |
| F-E2E-007 | P1 | [UI] [UNIT] | K1 | Yanlış tip Nick ekle → Void ("yanlış tip") → Dent ekle → … → Tamamla → Rapor | Dent #2 olur; void #1 sayılmaz, raporda yok; tamamlama geçer | StoreIdsNeverReused, ReportEscapesAndHidesVoid |
| F-E2E-008 | P1 | [UNIT] [UI] | K3 | Return to vendor → Tamamla (engel) → Reinspect → şimdi gir → Giderildi → Tamamla | WaitingVendor açık sayılır; sonra kapanır | StateVendorIsOpen |
| F-E2E-009 | P1 | [UNIT] [UI] | K3 | Repair → Reinspect → Giderildi; ikinci kusurda Repair → Kabul | İki yol da Kapandı | — |
| F-E2E-010 | P2 | [UI] [UNIT] | K2 | Parçalar "1-5, 8" → Rework → Yeni hata → child → CSV dışa aktar | Cümle "on parts 1–5 and 8"; child aynı parçalar; CSV sütunu dolu | ChildInheritsAffectedParts, CsvHasAffectedPartsColumn |
| F-E2E-011 | P2 | [UI] | K1 (mm) | #1 mm ölç → Ayarlar inch → #2 ekle, inch ölç → Rapor | #1 mm, #2 inch; raporda her biri kendi biriminde | VmUnitFollowsSettingsPerDefect, ReportShowsInchValues |
| F-E2E-012 | P2 | [UI] | K0 | Admin: alan "Yükseklik/H/uzunluk" ekle, Dent'te işaretle, uygula, kaydet → Admin'i kapat (restart uyarısı) → uygulamayı yeniden başlat → Dent ekle, H 0.3 → Rework → aynı kusur, H boş, D yeni → Rapor | H kutusu çıkar; cümle "H 0.3 mm"; rework sonrası H korunur; raporda H | VmCustomFieldShownSavedAndInSentence, VmReinspectMergesCustomField |
| F-E2E-013 | P2 | [UI] | K3 | Admin: Accept metnini "TEST METNİ" yap, kaydet → (yeniden başlatmadan) Kabul seç | Diyalogda "TEST METNİ" | — |
| F-E2E-014 | P2 | [UI] | K1 | Admin: Windows kimliğini aç → ana pencerede "Başka muayene" → aynı oturumu aç → karar ver | Inspector kutusu salt okunur, Windows adı; karar `DecidedBy` Windows adı | VmInspectorFromWindowsUser |
| F-E2E-015 | P2 | [UI] | İki örnek, aynı geçici kök | A oturum açar, #1 ekler → B aynı oturumu açar, #1'e Kabul → A listede başka kusura geçip geri döner → A Tamamla | A güncel durumu görür; tamamlama geçer; numaralar çakışmaz | — |
| F-E2E-016 | P1 | [UI] [UNIT] | K3 | Kabul → Yeniden aç ("yanlış kabul") → Rework → şimdi gir → Giderildi | Geçmiş: Kabul, Yeniden aç, Rework, Giderildi; son durum Kapandı | ReopenRules |

### 4.33 Launcher (F-LNC)

UI yöntemi için scratch yerleşimi: `$S\prog\MuayeneKayit.exe` (+ Launcher Debug çıktısının tüm dosyaları, kaynak `src\MuayeneKayit.Launcher\bin\Debug\net9.0-windows\win-x64\`), `$S\prog\surum.txt`, `$S\prog\vtest\Inspector-x64\` (Inspector Debug kopyası), `$S\prog\MuayeneKayit.settings.json` (rootPath = geçici kök). Inspector ayarı 2 üst klasörde bulur.

| ID | Ö | Yöntem | Ön koşul | Adımlar | Beklenen | Mevcut test |
|---|---|---|---|---|---|---|
| F-LNC-001 | P1 | [UNIT] [UI] | `surum.txt`="vtest", exe var | Launcher'ı çalıştır (argümanlı: `--x 1`) | Inspector açılır (PID'i kaydet); çalışma klasörü exe klasörü; argümanlar aktarılır; Launcher 0 ile çıkar | ProgramLayoutResolvesVersionAndArch |
| F-LNC-002 | P1 | [UNIT] [UI] | `surum.txt` yok | Launcher | MessageBox "Muayene Kayıt": "Sürüm dosyası bulunamadı: ..."; çıkış kodu 1 (MessageBox'ı UIA ile "Tamam" ile kapat) | kısmen: ProgramLayoutResolvesVersionAndArch |
| F-LNC-003 | P1 | [UNIT] | — | `surum.txt` boş, "..", "v1/../x", "a<b" | "surum.txt geçersiz: ..." | — |
| F-LNC-004 | P1 | [UNIT] | `surum.txt`="v9", klasör yok | Resolve | "v9 sürümünde Inspector bulunamadı: ..." | kısmen: ProgramLayoutResolvesVersionAndArch |
| F-LNC-005 | P2 | [UNIT] | arm64 ve x64 klasörleri | `Resolve(arm64:true/false)` | ARM64'te arm64, yoksa x64; x64'te asla arm64 | ProgramLayoutResolvesVersionAndArch |
| F-LNC-006 | P2 | [UNIT] | `surum.txt` = "vtest  \r\n2. satır" ve UTF-8 BOM'lu | Resolve | İlk satır kırpılır; BOM sorun çıkarmaz | — |
| F-LNC-007 | P2 | [UI] | F-LNC-001 yerleşimi | Inspector'da `AdminNavItem` → `LoginRoot` | "Ortak alan: <geçici kök>" (ayar program kökünden bulundu) | InstallSettingsFoundInParentFolders |
| F-LNC-008 | P2 | [UNIT] | Program kökünde settings yok, `surum.txt` var | `InstallSettings.SaveRootPath(<exe klasörü>, root)` | Dosya program köküne (surum.txt'nin yanına) yazılır | — |
| F-LNC-009 | P3 | [MANUAL] | Eski kurulumdan kalan "MuayeneKayit Admin.exe" kopyası | Çalıştır | Inspector açılır | — |
| F-LNC-010 | P2 | [MANUAL] | Program klasörü UNC'de | Launcher'ı ağdan çalıştır | Inspector açılır; settings bulunur | — |

---

## 5. Özet

### 5.1 Alan ve önceliğe göre test sayıları

| Alan | P1 | P2 | P3 | Toplam |
|---|---|---|---|---|
| F-SESS Oturum girişi ve öneriler | 5 | 9 | 5 | 19 |
| F-DEF Kusur listesi | 2 | 9 | 1 | 12 |
| F-MEAS Ölçüm ve otomatik kayıt | 9 | 10 | 0 | 19 |
| F-CUST Ek ölçüm (muayene) | 1 | 7 | 1 | 9 |
| F-PART Etkilenen parçalar | 1 | 6 | 1 | 8 |
| F-HM High metal | 2 | 5 | 0 | 7 |
| F-TAB Adımlar / sekmeler | 0 | 7 | 1 | 8 |
| F-PHOTO Fotoğraf | 5 | 10 | 0 | 15 |
| F-CAM Kamera | 2 | 11 | 2 | 15 |
| F-EDIT Düzenleyici / arka plan | 1 | 16 | 3 | 20 |
| F-SM Durum makinesi | 16 | 5 | 0 | 21 |
| F-DEC Karar diyaloğu | 9 | 11 | 1 | 21 |
| F-RWK Rework sonrası muayene | 9 | 8 | 0 | 17 |
| F-UNDO Geri alma | 7 | 3 | 0 | 10 |
| F-VOID Void | 5 | 2 | 0 | 7 |
| F-COMP Tamamlama | 5 | 1 | 2 | 8 |
| F-RPT Rapor | 4 | 10 | 1 | 15 |
| F-DASH Dashboard | 1 | 7 | 2 | 10 |
| F-SET Birim / tema | 1 | 6 | 3 | 10 |
| F-ID Windows kimliği | 1 | 5 | 0 | 6 |
| F-ADM-LOGIN Admin giriş / kilit | 8 | 6 | 1 | 15 |
| F-ADM-REC Admin kayıtlar | 3 | 7 | 0 | 10 |
| F-ADM-TYPE Admin kusur tipleri | 1 | 14 | 2 | 17 |
| F-ADM-FIELD Admin ek alanlar | 0 | 11 | 0 | 11 |
| F-ADM-TXT Admin karar açıklamaları | 0 | 7 | 1 | 8 |
| F-ADM-MNT Admin bakım | 2 | 7 | 0 | 9 |
| F-ADM-SET Admin ayarlar | 3 | 7 | 0 | 10 |
| F-CONC Eşzamanlılık | 7 | 6 | 0 | 13 |
| F-CFG Bozuk / eksik dosyalar | 8 | 9 | 4 | 21 |
| F-NET Ağ yolu kaybı | 6 | 3 | 0 | 9 |
| F-KEY Klavye / diyalog güvenliği | 2 | 3 | 2 | 7 |
| F-E2E Uçtan uca | 10 | 6 | 0 | 16 |
| F-LNC Launcher | 4 | 5 | 1 | 10 |
| **Toplam** | **140** | **239** | **34** | **413** |

### 5.2 Yönteme ve kapsama göre

| Ölçü | Sayı |
|---|---|
| Tercih edilen yöntem [UNIT] | 220 |
| Tercih edilen yöntem [UI] | 170 |
| Tercih edilen yöntem [MANUAL] | 23 |
| Mevcut testle tam kapsanan | 161 |
| Mevcut testle kısmen kapsanan | 51 |
| Hiç kapsanmayan | 201 |
| ŞÜPHELİ işaretli durum (olası hata) | 19 (bulgular B-01 … B-22) |

---

## 6. Kod okumasında görülen olası hatalar (testle doğrulanacak; kod DEĞİŞTİRİLMEZ)

| Bulgu | Ö | Özet | Kanıt (kod) | Doğrulayan test |
|---|---|---|---|---|
| B-01 | P1 | Ölçüm formu mm'de 2, inch'te 4, açıda 2 ondalık gösterir; sonraki otomatik kayıt bu yuvarlanmış metni geri okuyup kayıtlı değeri sessizce değiştirir (ör. 0.125 → 0.13) ve düzeltme kaydı oluşturur. Karar öncesi otomatik kayıt da aynı değeri Snapshot'a taşır | `InspectionViewModel.LoadSelected` (`DefectPresentation.Fmt`), `AutoSave` → `ReadForm`, `Units.Format("0.##")` | F-MEAS-018 |
| B-02 | P1 | `JsonException` (ve `admin.json`'da `FormatException`) hiçbir `Run` / `RunAsync` / `Login` catch listesinde yok ve `App`'te `UnhandledException` işleyicisi yok. Bozuk `session.json`, `defects\N.json`, `admin.json` ya da `_audit.log` son satırı uygulamayı çökertir; tek bozuk kusur dosyası Dashboard'u ve Admin Kayıtlar'ı da düşürür | `SessionStore.ReadJson`, `AuditLog.LastHash`, `SharedConfig.VerifyPin`, `InspectionViewModel.Run`, `AdminViewModelBase.Run`, `LoginViewModel.Login` | F-CFG-008, F-CFG-009, F-CFG-010, F-CFG-011 |
| B-03 | P1 | İki kullanıcı aynı kusurdayken, ekranı eski olan kullanıcının otomatik kaydı tüm form değerlerini yazar; diğerinin yeni ölçümü sessizce geri alınır (yalnızca geçmişte iz kalır) | `InspectionViewModel.AutoSave` (karşılaştırma bellekteki `Current` ile), `StoreForm` | F-CONC-004 |
| B-04 | P1 | Tamamlanmış muayeneye kusur eklenebilir ve karar verilebilir; durum "Completed" kalır. `SaveSession` kilitsiz olduğundan eşzamanlı tamamla/ekle yarışı da aynı sonucu verir | `InspectionViewModel.AddDefect`, `Complete`, `SessionStore.SaveSession` | F-COMP-004, F-CONC-006 |
| B-05 | P1 | Not (ve geçmiş) için uzunluk sınırı yok; 5 MB'ı aşan kusur dosyası yazılır ama sonraki okumada reddedilir → oturum açılamaz | `SessionStore.MaxJsonBytes` yalnızca okumada | F-CFG-013 |
| B-06 | P1 | Rework sonrası muayene diyaloğu sayıya çevrilemeyen metni sessizce boş sayar; diğer kutuda değer varsa Kaydet açılır, yanlış yazılan alan eski değerinde kalır | `WinUiDialogService.AskReinspectAsync` → `ReadValues().Try` | F-RWK-006 |
| B-07 | P2 | Dosya kilidi UI iş parçacığında `Thread.Sleep` ile 15 sn'ye kadar beklenir; pencere bu sürede yanıt vermez | `FileLock.Acquire` | F-CONC-008 |
| B-08 | P2 | "_" ile başlayan parça no kabul edilir ama index ve öneriler "_" klasörlerini atlar; kayıt görünmez olur, `_config` altına muayene yazılabilir | `SafePath.SegmentPattern`, `IndexBuilder.Rebuild`, `FolderNames` | F-SESS-008 |
| B-09 | P2 | Admin kök yolu göreli olabilir ("." / "rel"); çalışma klasörüne göre çözülür | `SettingsViewModel.SaveRoot` | F-ADM-SET-005 |
| B-10 | P2 | Fotoğraf adı mevcut dosya sayısından üretilir; arada eksik dosya varsa ya da iki kullanıcı aynı anda eklerse ad çakışır, `File.Copy(overwrite:false)` hata verir | `PhotoImporter.Import` | F-PHOTO-008, F-CONC-007 |
| B-11 | P2 | Admin tip adını / alanlarını değiştirip "Şablonu uygula"ya basmadan "Tümünü ortak alana kaydet" denirse değişiklik kaybolur, yine de "kaydedildi" mesajı gelir | `TypesViewModel.Save` (`_types` yazılır, editör değil) | F-ADM-TYPE-016 |
| B-12 | P2 | Ek alanın türü değiştirilirse kayıtlı değer bir sonraki otomatik kayıtta yeni türle yeniden yazılır | `ApplyTemplateFields`, `CustomFieldInput.Read` | F-CUST-009, F-ADM-FIELD-008 |
| B-13 | P2 | Ölçüm alanı olmayan tipte sıradaki adım sürekli "ölçümleri girin" kalır | `DefectPresentation.CurrentStep`, `NextStepFor` | F-TAB-006 |
| B-14 | P2 | Düzenleyici açıkken kusur başka kullanıcıca kapatılırsa kayıt yine yazılır | `InspectionViewModel.OpenEditor` geri çağrısı | F-EDIT-020 |
| B-15 | P3 | Tamamlanmış muayeneyi yeniden tamamlamak ikinci denetim satırı yazar | `Complete` | F-COMP-005 |
| B-16 | P3 | 5 hatalı PIN kilidi yalnızca bellekte; uygulama yeniden başlatılınca sıfırlanır | `LoginViewModel` | F-ADM-LOGIN-008 |
| B-17 | P3 | Kaydedilmemiş değişiklik uyarısı yok: düzenleyici Vazgeç / pencere değişimi, Admin tip ve açıklama sayfaları | `PhotoEditorWindow.Cancel_Click`, `WinUiWindowService.OpenPhotoEditor`, `AdminShellViewModel.Navigate` | F-EDIT-011, F-EDIT-018, F-ADM-TYPE-013, F-ADM-TXT-008 |
| B-18 | P3 | Admin penceresinde sabit açık renkler; koyu temada kontrast sorunu olası | `AdminWindow.xaml` (#FFFFFF, #1A3A5C, #555) | F-SET-004 |
| B-19 | P2 | Ortak alanda `admin.json` silinirse herkes yeni PIN belirleyebilir (ürün kararı mı, raporla) | `LoginViewModel.Reset` / `SharedConfig.HasPin` | F-ADM-LOGIN-013 |
| B-20 | P2 | Aynı oturumda eşzamanlı rapor üretimi aynı sürüm numarasını kullanır | `CreateReportAsync` (`LatestVersion + 1`, kilitsiz) | F-CONC-013 |
| B-21 | P2 | `measure_fields.json`'da tek geçersiz alan tüm ek alanları düşürür | `SharedConfig.LoadCustomFields` | F-CFG-005 |
| B-22 | P1 | Kök klasör erişilemezken (yerel / eşlenmiş yol) yeni oturum ve admin ilk kurulumu kök yolunu yeniden oluşturur; kayıtlar ve yeni PIN boş, yanlış bir köke yazılır | `SessionStore.OpenOrCreate`, `SharedConfig.SetPin` (`Directory.CreateDirectory`), `LoginViewModel.SafeHasPin` | F-NET-003, F-NET-006 |

---

## 7. Uygulama sırası (daha ucuz yürütücü model için adım adım)

### 7.0 Değişmez kurallar

- Bölüm 0'daki güvenlik kuralları her adımda geçerlidir. `src\` altına dokunma.
- Her test bir ID taşır; test adı `"FONK F-XXX-NNN: <kısa açıklama>"` biçiminde olur.
- ŞÜPHELİ testlerin beklentisini **doğru davranışa** göre yaz. Test kırmızı çıkarsa testi "geçsin diye" mevcut davranışa uydurma; sonucu B-numarasıyla raporla.
- Sonuçları `docs\test\FONKSIYONEL-TEST-SONUCLARI.md` dosyasına yaz: `ID | Sonuç (GEÇTİ / KALDI / ATLANDI) | Kanıt (test adı, json yolu, ekran görüntüsü yolu) | Not`. En sonda doğrulanan / doğrulanamayan B-numaraları listesi.

### 7.1 Adım 1 — taban

1. `dotnet run --project tests/MuayeneKayit.Tests` → 174/174 geçmeli. Geçmiyorsa DUR ve raporla.

### 7.2 Adım 2 — UNIT testleri ekle (önce bunlar; uygulama açılmaz)

Dosya yerleşimi (yalnızca `tests\MuayeneKayit.Tests\` altında):
- `FonkViewModelTests.cs` → `public static partial class ViewModelTests { ... }`: mevcut özel `With(...)`, `Ctx`, `Check(...)`, `FakeAudit` ve `c.Open()` doğrudan kullanılır. Özel tip listesi gerekiyorsa `With(body, types, customFields)` aşırı yüklemesi.
- `FonkAdminTests.cs` → `public static partial class AdminViewModelTests { ... }`: `With`, `c.SignIn()`, `c.Confirm.Answers`, `c.Clock.Advance`, `c.Saver`, `c.Os` kullanılır.
- `FonkCoreTests.cs` → `public static class FonkCoreTests` (kendi `Expect` ve `TempRoot` yardımcılarıyla; `Program.cs`'deki örnekle aynı).
- `FonkSupheliTests.cs` → yalnızca ŞÜPHELİ testler.
- Kayıt: `Program.cs` içindeki `tests` listesine `("FONK F-...: ...", ViewModelTests.Metot)` satırları. ŞÜPHELİ testler ana listeye **eklenmez**; `Program.cs`'de listeden sonra `if (args.Contains("--supheli")) tests.AddRange(FonkSupheliTests.All);` ile ayrı koşulur (böylece normal koşu yeşil kalır). Koşu: `dotnet run --project tests/MuayeneKayit.Tests -- --supheli`.

Yazım ipuçları:
- Async VM metotları: `c.Vm.GiveDecisionAsync(opt).GetAwaiter().GetResult()` (sahte servisler senkron tamamlanır). Diyalog cevabını çağrıdan **önce** kuyruğa koy: `c.Dialogs.Decisions.Enqueue(new DecisionInput("OP50", null, null, null));`, `c.Dialogs.ReinspectNow.Enqueue(false);`.
- Karar seçeneği: `c.Vm.DecisionOptions.First(o => o.Decision == Decision.Rework)`; reinspect seçeneği `o.IsReinspect`.
- "Çökme yok" beklentisi: çağrıyı `try { ... } catch (Exception ex) { throw new Exception("B-02 doğrulandı: " + ex.GetType().Name + " dışarı kaçtı"); }` ile sar; ayrıca `c.Notes.Last().Kind == StatusKind.Error` kontrol et.
- Gerçek denetim kaydı gereken testler (F-SESS-018, F-CFG-011): `new InspectionViewModel(store, new FileAuditTrail(store), SharedConfig.Defaults, new FakeDialogService(), new FakeFilePicker(), new FakeWindowService(), new FakePdfConverter(), new FakeUserSettings())` ile kendi bağlamını kur; geçici klasörü `finally`'de sil.
- İki bilgisayar (F-CONC-004/005/006/007/013): aynı geçici kök için iki ayrı `SessionStore` ve iki VM kur; ikisinde de aynı P/O/S ile `OpenSession`.
- Kültür: `var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); ... } finally { CultureInfo.CurrentCulture = old; }`.
- Eski kilit: `File.WriteAllText(lockPath, ""); File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddMinutes(-11));` (yalnızca geçici kökte).
- Kök kaybı (F-NET UNIT): `Directory.Move(root, root + "_gone")` → işlem → `Directory.Move` geri; `finally`'de her iki adı da temizle.
- Durum makinesi matrisi (F-SM-001/002): 3.7 tablosunu `Dictionary<DefectState, Decision[]>` olarak testte yeniden yaz; her durumu üreten küçük kurucu fonksiyonlar (`Open()`, `Repairing()`, ...) ile `Check` sonucunu karşılaştır; hata mesajında durum ve kararı yaz.
- 10 dk birleştirme kuralı `DateTime.Now` kullanır; ">10 dk" durumu için json'daki son `MeasurementEdits[].At` değerini geriye çekerek dosyayı yeniden yaz.

Ekleme sırası (her partiden sonra koşucuyu çalıştır, sonucu kaydet):

| Parti | İçerik | ID'ler |
|---|---|---|
| U1 — P1 ŞÜPHELİ (en yüksek değer) | `FonkSupheliTests.cs` | F-CFG-008, F-CFG-009, F-CFG-010, F-CFG-011, F-CFG-013, F-MEAS-018, F-CONC-004, F-COMP-004, F-NET-003, F-NET-006 |
| U2 — P1 yeni | ana liste | F-SM-001, F-SM-002, F-SM-006, F-SM-010, F-SM-014, F-SM-017, F-SM-019, F-SESS-003, F-SESS-004, F-SESS-005, F-SESS-016, F-DEF-003, F-MEAS-003, F-MEAS-004, F-MEAS-009, F-MEAS-014, F-PHOTO-004, F-CAM-011, F-EDIT-017, F-DEC-016, F-RWK-007, F-UNDO-006, F-UNDO-007, F-COMP-006, F-ID-002, F-ADM-REC-010, F-CONC-005, F-CONC-006, F-CFG-001, F-CFG-002, F-NET-001, F-NET-002, F-NET-004, F-E2E-009, F-LNC-002, F-LNC-003, F-LNC-004 |
| U3 — P1 uçtan uca (VM ile) | ana liste | F-E2E-001, F-E2E-002, F-E2E-003, F-E2E-004, F-E2E-005, F-E2E-006, F-E2E-007, F-E2E-008, F-E2E-016 (her biri tek test; mevcut senaryo testlerini tekrar etmez, adımları zincirler) |
| U4 — P2 ŞÜPHELİ | `FonkSupheliTests.cs` | F-SESS-008, F-CUST-009, F-TAB-006, F-PHOTO-008, F-EDIT-020, F-ADM-TYPE-016, F-ADM-SET-005, F-CONC-013 |
| U5 — P2 yeni | ana liste | F-SESS-002, F-SESS-006, F-SESS-007, F-SESS-018, F-DEF-002, F-DEF-005, F-DEF-012, F-MEAS-005, F-MEAS-006, F-MEAS-007, F-MEAS-010, F-MEAS-012, F-MEAS-019, F-CUST-003, F-CUST-006, F-CUST-007, F-PART-003, F-PART-004, F-HM-007, F-TAB-008, F-PHOTO-005, F-PHOTO-006, F-PHOTO-007, F-PHOTO-012, F-PHOTO-014, F-SM-005, F-SM-013, F-SM-021, F-DEC-006, F-DEC-015, F-DEC-017, F-DEC-019, F-DEC-021, F-RWK-012, F-RWK-015, F-RWK-017, F-UNDO-008, F-UNDO-009, F-UNDO-010, F-VOID-007, F-RPT-007, F-RPT-008, F-RPT-009, F-RPT-011, F-RPT-012, F-RPT-015, F-SET-006, F-ID-005 |
| U6 — P2 admin / config / eşzamanlılık | ana liste (F-CONC-007 → ŞÜPHELİ listesi) | F-ADM-LOGIN-013, F-ADM-REC-003, F-ADM-REC-005, F-ADM-TYPE-003, F-ADM-TYPE-004, F-ADM-TYPE-005, F-ADM-TYPE-006, F-ADM-TYPE-009, F-ADM-TYPE-010, F-ADM-TYPE-014, F-ADM-FIELD-002, F-ADM-FIELD-003, F-ADM-FIELD-004, F-ADM-FIELD-005, F-ADM-FIELD-006, F-ADM-FIELD-007, F-ADM-FIELD-008, F-ADM-FIELD-010, F-ADM-FIELD-011, F-ADM-TXT-002, F-ADM-TXT-003, F-ADM-TXT-004, F-ADM-MNT-003, F-ADM-MNT-009, F-ADM-SET-003, F-ADM-SET-004, F-ADM-SET-006, F-ADM-SET-010, F-CONC-007, F-CONC-010, F-CONC-011, F-CFG-005, F-CFG-007, F-CFG-014, F-CFG-019, F-CFG-021, F-NET-005, F-NET-007, F-LNC-006, F-LNC-008 |
| U7 — P3 | ana liste | F-SESS-014, F-PART-007, F-COMP-005 (ŞÜPHELİ listesi), F-ADM-LOGIN-014, F-ADM-TYPE-017, F-CFG-015, F-CFG-020 (ŞÜPHELİ listesi) |

Not: "Mevcut test" sütununda tam karşılığı olan durumlar için yeni test yazılmaz; yalnızca sonuç dosyasında "mevcut: <metot>" olarak GEÇTİ işaretlenir (koşucu yeşilse).

### 7.3 Adım 3 — UI testleri (UNIT bittikten sonra, kullanıcı onayıyla)

**Araç.** Tercihen WinApp CLI `winapp ui` (bkz. `winui:winui-ui-testing` becerisi). Yoksa Windows PowerShell 5.1 + .NET UI Automation:

```powershell
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]
function Win($procId) { $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
  for ($i=0; $i -lt 50; $i++) { $w = $AE::RootElement.FindFirst($TS::Children, $c); if ($w) { return $w }; Start-Sleep -Milliseconds 200 }; throw "pencere yok" }
function ById($root, $id) { $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
  for ($i=0; $i -lt 50; $i++) { $e = $root.FindFirst($TS::Descendants, $c); if ($e) { return $e }; Start-Sleep -Milliseconds 200 }; throw "yok: $id" }
function ByName($root, $name) { $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
  for ($i=0; $i -lt 50; $i++) { $e = $root.FindFirst($TS::Descendants, $c); if ($e) { return $e }; Start-Sleep -Milliseconds 200 }; throw "yok: $name" }
function Click($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function SetText($e, $t) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($t) }
function Pick($e) { $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
```

Sürüş kuralları:
- **TextBox**: `SetText` sonra odağı başka kontrole ver (`ById $w "SaveMeasButton"` üzerinde `SetFocus()`) ki LostFocus → otomatik kayıt tetiklensin.
- **AutoSuggestBox** (`PartBox`, `OpBox`, `SerialBox`): içindeki Edit öğesini bul (`ControlType.Edit` alt öğe) ve `SetText`.
- **PasswordBox** (`PinBox` ...): ValuePattern olmayabilir; `SetFocus()` + `[System.Windows.Forms.SendKeys]::SendWait(<test-pin.txt içeriği>)`. PIN'i asla ekrana / loga yazma.
- **ComboBox** (`DefectTypeBox`, `DecisionCombo`, `FilterStateBox`): `ExpandCollapsePattern.Expand()` → öğeyi Name ile bul → `Pick`. `DecisionCombo` seçimi diyaloğu hemen açar.
- **RadioButtons** (`UnitRadio`, `ThemeRadio`, diyalog seçenekleri): öğeyi Name ile bul → `Pick`.
- **CheckBox / ToggleSwitch** (`HighMetalBox`, `WindowsUserSwitch`): `TogglePattern.Toggle()`.
- **ContentDialog**: ana pencerenin alt ağacında; düğmeler AutomationId `PrimaryButton` / `SecondaryButton` / `CloseButton`. Her diyalog açılışından sonra 300 ms bekle.
- **Dosya açma seçicisi** (Dosyadan ekle): sistem penceresinde "Dosya adı" kutusu (AutomationId `1148`) → yalnızca `fixtures\` altındaki tam yol → Enter. **Kaydetme seçicisi** (CSV): yalnızca scratch altındaki yol.
- **Doğrulama**: önce geçici kökteki json'lar (`session.json`, `defects\N.json`, `_audit.log`, `reports\`), sonra `StatusInfoBar` içindeki metin (alt Text öğesi), sonra görünür durum. Rapor için 70 sn'ye kadar `reports\Rapor_vN.pdf` bekle.
- **Yalıtım**: her UI grubu kendi geçici kökünde çalışır: uygulamayı (kendi PID'i) kapat → yeni `root-<grup>` klasörü oluştur → scratch `MuayeneKayit.settings.json`'u ona çevir → içeriği doğrula → başlat.
- **Edge / Gezgin pencereleri**: rapor ve "Klasörü aç" testlerinde açılan pencereyi başlığından tanı; emin değilsen açık bırak ve raporda yaz.

UI grupları ve sırası:

| Grup | İçerik | ID'ler |
|---|---|---|
| G1 — duman (P1) | Temel akış | F-SESS-001, F-DEF-001, F-MEAS-001, F-PHOTO-001, F-DEC-001, F-COMP-002, F-RPT-001, F-E2E-001 |
| G2 — diyalog güvenliği (P1) | Enter/Esc/Vazgeç | F-KEY-001, F-KEY-002, F-PHOTO-009, F-PHOTO-010, F-PHOTO-011, F-DEC-002, F-DEC-003, F-DEC-004, F-TAB-002, F-UNDO-003, F-VOID-003 |
| G3 — karar ve rework (P1) | | F-MEAS-015, F-DEC-007, F-DEC-010, F-DEC-013, F-DEC-014, F-RWK-001, F-RWK-002, F-RWK-003, F-RWK-004, F-RWK-005, F-RWK-006 (ŞÜPHELİ B-06, yalnızca UI), F-RWK-010, F-UNDO-001, F-UNDO-002, F-UNDO-005, F-VOID-001, F-VOID-002, F-COMP-001, F-SESS-017, F-E2E-002 … F-E2E-007, F-E2E-016 |
| G4 — admin (P1, taze kökte ilk iş PIN kurulumu) | | F-ADM-LOGIN-001, -002, -004, -005, -010, F-ADM-REC-004, F-ADM-TYPE-012, F-ADM-MNT-002, F-ADM-SET-008, F-ADM-SET-001 (en son; sonra settings'i geri al), F-DASH-005 |
| G5 — P1 eşzamanlılık / kök kaybı (kendi kökünde) | İki örnek; yeniden adlandırma | F-CONC-009, F-CONC-004 (UI), F-CONC-005 (UI), F-NET-001, F-NET-002, F-NET-003, F-NET-006 |
| G6 — P2 muayene ekranı | | Bölüm 4'te tercih yöntemi [UI] olan P2 durumların muayene, fotoğraf, düzenleyici, karar, dashboard kısmı: F-SESS-009, -010, -015, F-DEF-007, -008, -009, -011, F-MEAS-016, -017, F-PART-001, -008, F-HM-001, -006, F-TAB-003, -004, F-PHOTO-002, -013, F-EDIT-001 … -016, F-DEC-005, -008, -009, -011, -012, -020, F-RWK-008, -009, F-VOID-006, F-COMP-008, F-RPT-004, -013, F-DASH-001, -002, -003, -006, -007, -009, F-KEY-003, -006, F-E2E-010, -011, -015 |
| G7 — P2 ayarlar (LOCALAPPDATA yedeği zorunlu) | | F-SET-001, -002, -003, -009, F-ID-001, -006, F-E2E-014 |
| G8 — P2 admin | | F-ADM-LOGIN-007, -008, -011, -012, -015, F-ADM-REC-001, -002, -007, -008, -009, F-ADM-TYPE-001, -002, -007, -008, -011, -015, F-ADM-FIELD-001, -009, F-ADM-TXT-001, -005, -006, F-ADM-MNT-001, -004, -005, -006, F-ADM-SET-007, -009, F-CONC-008, F-CONC-012, F-E2E-012, F-E2E-013 |
| G9 — launcher | Bölüm 4.33 yerleşimi | F-LNC-001, F-LNC-002, F-LNC-007 |
| G10 — P3 | | F-SESS-011, -013, -019, F-DEF-010, F-CUST-008, F-TAB-005, F-EDIT-004, -009, -018, F-DEC-018, F-COMP-007, F-DASH-008, F-SET-004, F-SET-010, F-CFG-017, F-ADM-TYPE-013, F-ADM-TXT-008, F-KEY-005, F-KEY-007 |

Bitiş: kendi PID'lerini kapat; `%LOCALAPPDATA%\MuayeneKayit\` yedeklerini geri kopyala; scratch `MuayeneKayit.settings.json`'un ilk geçici kökü gösterdiğini doğrula; scratch yolunu raporla.

### 7.4 Adım 4 — insana bırakılacak [MANUAL] liste

F-CAM-001 … F-CAM-015 (kamera donanımı), F-KEY-004, F-RPT-010 (büyük rapor süresi), F-RPT-014 (Edge'siz makine), F-DASH-010 (500 kayıt performansı), F-SET-005, F-CFG-018, F-NET-008, F-NET-009 (gerçek UNC kopması), F-LNC-009, F-LNC-010, isteğe bağlı F-ADM-LOGIN-009 (10 dk boşta kilit, gerçek bekleme). Yürütücü bunları yalnızca sonuç dosyasında "MANUAL — insan bekliyor" olarak listeler.
