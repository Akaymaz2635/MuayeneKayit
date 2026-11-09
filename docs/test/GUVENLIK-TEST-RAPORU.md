# MuayeneKayit — Güvenlik Test Raporu

Plan: `docs/test/GUVENLIK-TEST-PLANI.md` · Depo: `C:\Users\PC\Documents\JUNK\MuayeneKayit` · Dal: `main` · Commit: `17cf3bf` (çalışma ağacında yalnızca test eklemeleri var) · Tarih: 2026-11-09
Yürütme: planın §0 güvenlik kuralları ve §4 uygulama sırası izlendi. `src/` değiştirilmedi; commit/push yapılmadı; arayüz açılmadı; ağ erişimi ve kurulum yapılmadı.
Değişen / eklenen dosyalar: `tests/MuayeneKayit.Tests/SecurityTests.cs` (yeni), `tests/MuayeneKayit.Tests/Program.cs` (kayıt + `openFindings` listesi + `ATLANDI` yakalama), bu rapor. Betikler: `C:\Users\PC\AppData\Local\Temp\claude\0d131e098d594c64\b4602378-972e-45da-8ba3-71896c66e7e3\scratchpad\sec\` (repo dışı).

## 1. Özet

Toplam 132 plan vakası değerlendirildi: **61 PASS · 68 AÇIK BULGU · 3 ATLANDI**.

| Alan | PASS | AÇIK BULGU | ATLANDI | Toplam |
|---|---:|---:|---:|---:|
| S-PATH (yol / kök sınırı) | 6 | 10 | 0 | 16 |
| S-HTML (rapor HTML/SVG/PDF) | 10 | 4 | 1 | 15 |
| S-CSV (dışa aktarım) | 5 | 1 | 1 | 7 |
| S-JSON (bozuk / büyük / aralık dışı veri) | 3 | 17 | 0 | 20 |
| S-PIN (admin PIN) | 6 | 7 | 0 | 13 |
| S-AUD (denetim kaydı) | 4 | 7 | 0 | 11 |
| S-LOCK (kilitler) | 6 | 3 | 0 | 9 |
| S-PHOTO (fotoğraf) | 6 | 5 | 0 | 11 |
| S-LAUNCH (başlatıcı) | 6 | 4 | 0 | 10 |
| S-SEC (depo gizli bilgi / bağımlılık) | 5 | 2 | 1 | 8 |
| S-PRIV (kişisel veri) | 3 | 3 | 0 | 6 |
| S-NTFS (izin belgeleri) | 1 | 5 | 0 | 6 |
| **Toplam** | **61** | **68** | **3** | **132** |

ATLANDI: S-HTML-015 (Edge çalıştırılması ağ erişimi riski taşıdığı için; görevin "ağ yok" kuralı), S-CSV-003 (Excel olmadan doğrulanamadı), S-SEC-005 (`dotnet list package --vulnerable` ağ gerektirir).

Plan dışı, test sırasında bulunan ek vakalar (sayıma dahil değil, tabloda "ek" etiketli): S-PIN-011b, S-AUD-011b, S-JSON-020b, S-CSV-005 (plan ID'si), S-LAUNCH-002/003/006 (plan ID'li ama ÖB'siz yeni bulgular).

Ön bulgu durumu: ÖB-01 … ÖB-16'nın **tamamı doğrulandı** (yanlış alarm yok). ÖB-06'daki "tek çekirdekte ~1 gün" tahmini ölçümle düzeltildi: bu makinede `VerifyPin` ≈ 19,3 ms/deneme → 10^6'lık 6 haneli PIN uzayı tek çekirdekte en kötü durumda ≈ 5,4 saat, 16 çekirdekte ≈ 20 dakika (risk plandaki tahminden daha yüksek).

Birim test koşusu (`dotnet run --project tests/MuayeneKayit.Tests`): normal liste **212/212 geçti** (önceki 174 + 38 yeni güvenlik regresyon testi), çıkış kodu 0. `openFindings` listesinde 49 test var; **49/49 "AÇIK BULGU"** (hiçbiri "DÜZELDİ" değil), çıkış kodunu etkilemez.

Koşunun tam son çıktı satırları:

```
Açık bulgu: 49/49

Tümü geçti (212/212).
```

## 2. Onaylanan bulgular (önem sırasına göre)

Satır numaraları fiilen okunan dosyalara göredir; planda bazı dosyalar için (ör. `FileLock.cs`, `PhotoImporter.cs`, `CsvExport.cs`) verilen satırlar bu commit ile uyuşmadı, burada düzeltilmiştir.

| # | Önem | ÖB | Bulgu | Kanıt | Somut düzeltme önerisi |
|---|---|---|---|---|---|
| 1 | **Kritik** | ÖB-01 | Kapalı kusurda `Markups[photo].CleanedFile` doğrulanmadan `Path.Combine`'a verilir; `..\x.txt`, `\\host\s\x.jpg`, `C:\Windows\win.ini` olduğu gibi `OpenFile`'a gider; `.pdf/.html` dışındakiler `UseShellExecute=true` ile açılır. `defects\N.json`'a yazabilen herkes kaydı açan kişinin makinesinde exe/bat/lnk çalıştırabilir. | Test S-PATH-010/011 (`defects\..\x.txt` ve UNC yolu `OpenedFiles`'a düştü); `InspectionViewModel.Photos.cs:147`, `EdgeBrowser.cs:26-30`, `ShellService.cs:13`, `WinUiWindowService.cs:52` | (a) `OpenEditor`'da `CleanedFile` için `PhotoMarkupValidator.IsPhotoFileName` zorunlu; çözülen tam yolun `defects\` altında kaldığını denetle. (b) `EdgeBrowser.OpenFile`'da uzantı izin listesi (`.jpg .jpeg .png .pdf .html`); listede olmayan ya da Edge yoksa `UseShellExecute=true` ile açma, hata ver. |
| 2 | Yüksek | ÖB-02 | `Defect.Photos` adları okunurken doğrulanmaz: küçük resim yolu `defects\..\x.jpg` / `\\testhost\s\x.jpg` olur (ThumbnailView `File.OpenRead` ile açar → UNC'de NTLM özeti sızar); raporda `src="../defects/../x.jpg"` üretilir. | Test S-PATH-012/013/014; `InspectionViewModel.Photos.cs:22-36,53`, `ThumbnailView.cs:38`, `ReportBuilder.cs:248-252` | `LoadDefects` sonrası `Photos`'u `IsPhotoFileName` ile süz (geçersizi at + uyarı); `UpdatePhotos` ve `ReportBuilder.PhotoFigure` ham ada değil süzülmüş adlara baksın; `ThumbnailPath` hesabından sonra tam yolun `defects\` altında olduğunu denetle. |
| 3 | Yüksek | ÖB-03 | `JsonException`, `NullReferenceException` (`photos:null`), `ArgumentNullException` (`markups:null`), `FormatException`, `[null]` elemanlı şablonlar hiçbir `Run` sarmalayıcısında yakalanmıyor; global `UnhandledException` işleyicisi yok. Tek bozuk `session.json` / `defects\N.json` dashboard, muayene ekranı ve admin "Kayıtlar"ı çökertir (`IndexBuilder.Rebuild` tüm kökü tarar). Ek: aralık dışı enum sayıları (`status:999`, `decision:999`, `source:-1`) kabul ediliyor; `NaN` koordinatlı dosya çöküyor. | Testler S-JSON-001/002/003/005/006/007/008/009/010/011/012/020b; `SessionStore.cs:19,309-317`, `IndexBuilder.cs:43-47`, `InspectionViewModel.cs:797`, `DashboardViewModel.cs:205-217`, `AdminViewModelBase.cs:20`, `SharedConfig.cs:72-87,97-99,134-149` | (a) `SessionStore.ReadJson` içinde `JsonException`'ı `InvalidDataException`'a çevir; yüklemeden sonra null koleksiyonları boş listeye normalize et. (b) `IndexBuilder.Rebuild` döngüsünde oturum başına try/catch, bozukları ayrı listede raporla. (c) `JsonStringEnumConverter(allowIntegerValues:false)` ve `Enum.IsDefined` denetimi. (d) `ValidateTypes/ValidateCustomFields` başında null eleman ele. (e) `App.xaml.cs`'te `UnhandledException` işleyicisi (mesaj + günlük). |
| 4 | Yüksek | ÖB-04 | `_audit.log` son satırı bozuksa `AuditLog.LastHash` `JsonException` atar; her denetimli işlem kayıt dosyası yazıldıktan sonra çöker (kayıt değişti, denetim satırı yok). Ek gözlem: `_audit.lock` tutulurken de kusur dosyası yazılıp denetim satırı eklenemiyor (15 sn sonra `TimeoutException`). | Test S-AUD-006, S-LOCK-007 gözlemi; `AuditLog.cs:110-126`, `InspectionViewModel.Photos.cs:71-72`, `InspectionViewModel.cs:301` | `LastHash` bozuk satırı atlayıp sondan geriye ilk geçerli satırı kullansın (ya da kontrollü hata). Denetim satırını kayıttan önce "niyet" satırı olarak yaz ya da kayıt+denetimi aynı kilit altında yap; başarısızlıkta kullanıcıya açık uyarı. |
| 5 | Yüksek | ÖB-05 | Denetim zinciri anahtarsız SHA-256: baştan hesaplanmış sahte zincir "sağlam" bulunur; sondan satır silme ve dosyanın tamamen silinmesi `(true,null)` döner; kayıt içeriği (karar/ölçüm) denetime bağlı olmadığından `defects\1.json` elle düzenlenince iz kalmaz. KURULUM.md:124 güvencesi bu yüzden yanlış. | Testler S-AUD-002/003/004/008; `AuditLog.cs:42-76,128-134`, `SessionStore.cs:297-304`, `KURULUM.md:124` | Zinciri HMAC-SHA256 yap, anahtarı `_config` dışında (yalnız admin okuyabilen yer) tut; son satır özetini/sayacını ayrı yere (örn. `_config\audit.anchor`) yaz; `details`'e kayıt dosyasının kayıt-sonrası SHA-256'sını ekle; `Verify` "kayıt var ama günlük yok/kısalmış"ı hata saysın. NTFS'te `_audit.log` için yalnız-ekleme (AD) izni öner. |
| 6 | Yüksek | ÖB-06 | `admin.json` (PIN özeti) muayeneci/okuyucuya okunabilir (`RX`); 6 haneli PIN ≈ 19,3 ms/deneme ile çevrimdışı ≈ 5,4 saatte tek çekirdekte kırılır; kilitlenme (5 hata/60 sn) yalnız bellekte, yeniden açılışta sıfırlanır; tek tek hatalı denemeler denetlenmez. | Testler S-PIN-003 (ölçüm), S-PIN-004, S-PIN-005; `LoginViewModel.cs:17-18,79-91`, `SharedConfig.cs:50,309-342`, `KURULUM.md:102-105` | `admin.json`'u yalnız `MK_Admin`'in okuyabildiği ayrı klasöre al ya da ACL'i `_config` içinde bu dosya için daralt; PIN yerine ≥ 10 karakterli parola; kilitlenme sayacını/`lockedUntil`'i `_config`'e ya da yerel profile kalıcı yaz; her başarısız girişi `admin.login.fail` olarak denetle. |
| 7 | Yüksek | ÖB-07 | `OKU-BENI.txt:19` "VERİ klasörüne okuma + yazma" der, `_config` kısıtından söz etmez; bu kurulumla herkes `admin.json`'u silip ilk kurulum ekranında kendi PIN'ini belirleyip admin olur (`IsFirstRun=true`, iz yalnız "İlk PIN belirlendi"). Belgeler çelişkili (KURULUM.md:88-89,102-105 ayrı kısıt söyler). | Test S-PIN-007; `LoginViewModel.cs:41,69-78`, `OKU-BENI.txt:19`, `KURULUM.md:88-105` | OKU-BENI.txt'ye "`_config`'e muayeneci yalnız okur, yalnız admin yazar; KURULUM §3'ü uygulayın" ekle. Kodda: denetim kaydında daha önce `admin.pin.set` varsa ilk kurulum yerine uyarı ("PIN sıfırlandı") yaz/ikinci doğrulama iste. |
| 8 | Orta | ÖB-08 | `VerifyPin` yalnız tur sayısına alt sınır koyar: `Iterations=int.MaxValue` iş parçacığını (UI) saatlerce kilitler; bozuk Base64 `FormatException`, bozuk JSON `JsonException` login'i çökertir. | Testler S-PIN-008/009/010; `SharedConfig.cs:326-339`, `LoginViewModel.cs:97-98` | `Iterations`'a üst sınır (örn. ≤ 1.000.000); `VerifyPin`'i try/catch ile `false`+hata mesajına çevir; `LoginViewModel` catch'ine `FormatException`/`JsonException` ekle; PBKDF2'yi arka planda çalıştır. |
| 9 | Orta | ÖB-09 | Edge başsız: JS açık, `--allow-file-access-from-files`, ağ kapalı değil, raporda CSP yok; HTML önce paylaşıma yazılıp Edge doğrudan oradan okur (TOCTOU); admin "son rapor"da PDF yoksa paylaşımdaki HTML'i tarayıcıda açar. | Testler S-HTML-011/014; REVIEW S-HTML-012/013; `EdgePdfConverter.cs:33-38`, `InspectionViewModel.Photos.cs:213-224`, `ReportBuilder.cs:27-30`, `RecordsViewModel.cs:149-157` | Rapora `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src file: data:; style-src 'unsafe-inline'">` ekle; `--allow-file-access-from-files` kaldır, `--blink-settings=scriptEnabled=false` ve ağ kapatma bayrakları ekle; HTML'i `%TEMP%`'te yerelde üretip PDF'i paylaşıma kopyala; `OpenLatestReport` PDF yoksa HTML açmasın. |
| 10 | Orta | ÖB-10 | Kusur numarası dosya adından değil içerikten alınır: `5.json` içinde `"id":3` iken `UpdateDefect(5)` `3.json`'un üzerine yazar; yinelenen `Id` kabul edilir; `Id=int.MaxValue` ise sonraki numara taşıp `-2147483648.json` oluşur; `Id<=0` ve `notlar.json` (Id=0) kusur olarak yüklenir. | Testler S-JSON-013/014/015/016/017; `SessionStore.cs:60-70,81,94-100,155,297-307` | `LoadDefects` yalnız `^\d+\.json$` okusun, `Id == dosya adı` ve `Id > 0` doğrulansın, uyuşmazlıkta `InvalidDataException`; `nextId` için `checked` ve üst sınır. |
| 11 | Orta | ÖB-11 | Fotoğraf düzenleyici piksel boyutu sınırı olmadan tam çözünürlükte BGRA'ya açar; catch filtresi `OutOfMemoryException`'ı kapsamaz ve `_ = LoadAsync()` gözlenmez. `BackgroundRemover.ComputeMask`'te `width*height*4` int taşar: 65536×65536 doğrulamadan geçip `IndexOutOfRangeException` verir. | Test S-PHOTO-004 (65536×65536 → `IndexOutOfRangeException`); REVIEW S-PHOTO-003/010; `BackgroundRemover.cs:18`, `PhotoEditorWindow.xaml.cs:82,117-118,130-143`, `PhotoImporter.cs:19-27` | `checked` / `long` aritmetiği ve `ArgumentException`; içe aktarmada PNG IHDR / JPEG SOF'tan boyut oku, ≤ ~50 MP sınırı; düzenleyicide `BitmapTransform` ile ölçekle; catch'e `OutOfMemoryException` ekle, `LoadAsync` istisnasını gözle. |
| 12 | Orta | ÖB-12 | İleri tarihli kilit hiçbir zaman eski sayılmaz: her işlem 15 sn sonra `TimeoutException`; admin `StaleLocks` bunu listelemez (arayüzden kaldırılamaz). Kilitte sahip bilgisi yok. Kökteki tek `_audit.lock`/`_index.lock` tüm denetimli işlemleri/yenilemeyi 15 sn durdurur. | Testler S-LOCK-003/004, S-LOCK-007; `FileLock.cs:28-35,54-69`, `SharedConfig.cs:347-351` | `IsStale` gelecekteki `LastWriteTime`'ı (şimdi + 1 dk üstü) "şüpheli/eski" say; `StaleLocks` bunları da listelesin; kilit içine kullanıcı/makine/PID/zaman yaz. |
| 13 | Orta | ÖB-13 | `SafePath` ayrılmış aygıt adlarını (`CON`, `NUL`, `AUX`, `PRN`, `COM1`, `LPT1`, `con.txt`, `NUL.x`) kabul eder; kök içindeki junction izlenir: `IndexBuilder.Rebuild` kök dışı oturumu indekse alır; `RemoveStaleLock` junction ardındaki kök dışı `.lock` dosyasını **siler** (script ile doğrulandı). | Test S-PATH-003/006; script S-PATH-006/009; `SafePath.cs:12-14,22,31-39`, `IndexBuilder.cs:35-47`, `SharedConfig.cs:347-367` | `ValidateSegment`'e aygıt adı denetimi (kök ad `^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$`, uzantılı dahil); `IndexBuilder` ve `StaleLocks`'ta `FileAttributes.ReparsePoint` olan dizinleri atla; `RemoveStaleLock`'ta dosyanın üst zincirinde reparse point yoksa sil. |
| 14 | Orta (yeni) | — | Admin kök klasör değişimi (`SettingsViewModel.SaveRoot`) denetim kaydına yazılmıyor (PIN değişimi, kimlik, tip/açıklama değişimi yazıyor). | Test S-AUD-011b; `SettingsViewModel.cs:63-72` | `SaveRoot` içinde `admin.root.change` (eski → yeni yol) satırı ekle. |
| 15 | Düşük | ÖB-14 | İçe aktarma dosyayı bayt bayt kopyalar; EXIF (GPS vb.) paylaşımda kalır, temiz kopya yoksa rapor orijinali kullanır. | Test S-PHOTO-005; `PhotoImporter.cs:36`, `ReportBuilder.cs:248-252`, `CameraWindow.xaml.cs:604-610` | İçe aktarmada JPEG'i yeniden kodla / APP1 segmentlerini sil (ya da politika olarak belgele). |
| 16 | Düşük | ÖB-16 | Admin "klasörü aç" kök denetimi ayraçsız önek (`Veri2`, `Veri-yedek` geçer); `OpenLatestReport` klasörü hiç denetlemez. | Test S-PATH-007/008; `RecordsViewModel.cs:144,149-157` | `StartsWith(fullRoot + '\\')` ve `Path.GetFullPath(row.Folder)` denetimini `OpenLatestReport`'a da ekle (junction için ReparsePoint denetimi). |
| 17 | Düşük (yeni) | — | `surum.txt`: `.`, `abc`, `x y`, `1.0.2 x` kabul edilir (`Path.Combine(root, ".", ...)`); `rootPath` göreli yol olduğu gibi döner (çalışma klasörüne bağlı); PIN regex `\d` tam genişlik rakamları kabul eder; virgül içeren CSV hücresi tırnaklanmaz. | Testler S-LAUNCH-002/003/006, S-PIN-011b, S-CSV-005; `ProgramLayout.cs:25`, `InstallSettings.cs:36-40`, `SharedConfig.cs:311`, `CsvExport.cs:35-41` | Sürüm için `^\d+(\.\d+){1,3}$`; göreli `rootPath`'i reddet; PIN regex'ine `RegexOptions.ECMAScript` ya da `[0-9]`; CSV'de `,` içeren hücreyi tırnakla veya dosya başına `sep=;` yaz. |
| 18 | Düşük | — | Belge / süreç: `.gitignore` `*.pfx`, `*.snk`, `admin.json`, `_config/`, `_audit.log` içermiyor; KVKK saklama süresi / okuyucu erişimi belgelenmemiş; `KURULUM.md` ikinci izin düzeninde muayenecinin `_audit.log`'u silebileceği ve yalnız-ekleme önerisi yok; admin'in `MuayeneKayit.settings.json`'u program klasörüne yazması için gereken yetki yazılmamış. | REVIEW S-SEC-007, S-PRIV-002, S-NTFS-002/004/005/006 | İlgili belgelere birer paragraf; `.gitignore`'a ilgili desenler. |
| 19 | Düşük (bilgi) | ÖB-15 | Git commit üst verisinde kişisel e-posta: 2 kimlik, 40 commit (`a***@gmail.com`). | Script S-SEC-004 | Geçmiş yeniden yazılmaz; ileride `git config user.email`'i kurumsal/noreply adrese çek. |

## 3. Vaka tablosu

Sütunlar: ID · Sonuç · ÖB · Kanıt (dosya:satır) · Gözlem · Önerilen düzeltme. "mevcut" = mevcut testin geçmesi regresyon olarak yeterli sayıldı (174/174 temel çizgi). Test adı `SecurityTests.cs` içindeki metottur.

### 3.1 S-PATH

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-PATH-001 | PASS | — | `SafePath.cs:16-26` | Mevcut `SafePathRejectsTraversal` geçti | — |
| S-PATH-002 | PASS | — | `SafePath.cs:12-14` | `C:x`, `\\testhost\s`, `a:b`, `x::$DATA` ArgumentException | — |
| S-PATH-003 | AÇIK BULGU | ÖB-13 | `SafePath.cs:12-14,22` | `CON, nul, AUX, PRN, COM1, LPT1, con.txt, NUL.x` hepsi kabul edildi | Aygıt adı denetimi ekle |
| S-PATH-004 | PASS | — | `SafePath.cs:21-22` | Sonda nokta, `\u200B`, `\u202E` reddedildi; sonda boşluk kırpıldı | — |
| S-PATH-005 | PASS | — | `SafePath.cs:13` | 64 kabul, 65 red; 3×64 karakterlik tam yol 276 karakter, hata atmadı (.NET uzun yol) | Çok uzun yolda anlaşılır hata (isteğe bağlı) |
| S-PATH-006 | AÇIK BULGU | ÖB-13 | `IndexBuilder.cs:35-47`, `SafePath.cs:31-39` | `mklink /J` ile kök içi junction → kök dışı oturum indekse girdi (UNIT + script, yetki gerekmedi) | Reparse point dizinlerini atla |
| S-PATH-007 | AÇIK BULGU | ÖB-16 | `RecordsViewModel.cs:144` | `Veri2\P\O\S` `Veri` kökü altında sayılıp `OpenFolder`'a gitti | Ayraçlı kök önekiyle karşılaştır |
| S-PATH-008 | AÇIK BULGU | ÖB-16 | `RecordsViewModel.cs:149-157` | Kök dışı `Rapor_v1.pdf` kabuğa verildi | Aynı kök denetimini ekle |
| S-PATH-009 | AÇIK BULGU | ÖB-13 | `SharedConfig.cs:347-367` | Script: `StaleLocks` `jn\x.lock` (junction ardı) listeledi, `RemoveStaleLock` kök dışı `x.lock`'u **sildi** | ReparsePoint denetimi |
| S-PATH-010 | AÇIK BULGU | ÖB-01 | `InspectionViewModel.Photos.cs:147`, `EdgeBrowser.cs:30` | `CleanedFile="..\x.txt"` → `OpenFile(…\defects\..\x.txt)` | `IsPhotoFileName` + `defects\` altı denetimi; OpenFile uzantı izin listesi |
| S-PATH-011 | AÇIK BULGU | ÖB-01 | `InspectionViewModel.Photos.cs:147` | UNC (`\\testhost\paylasim\x.jpg`) ve `C:\Windows\win.ini` `OpenFile`'a gitti (yalnız metin, erişilmedi) | Aynı |
| S-PATH-012 | AÇIK BULGU | ÖB-02 | `InspectionViewModel.Photos.cs:21-36` | `photos:["..\x.jpg"]` → `ThumbnailPath=…\defects\..\x.jpg` | `Photos`'u `IsPhotoFileName` ile süz |
| S-PATH-013 | AÇIK BULGU | ÖB-02 | `InspectionViewModel.Photos.cs:36`, `ThumbnailView.cs:38` | `ThumbnailPath=\\testhost\s\x.jpg` | Aynı; UNC'yi reddet |
| S-PATH-014 | AÇIK BULGU | ÖB-02 | `ReportBuilder.cs:248-252` | `src="../defects/../x.jpg"` üretildi | Fotoğraf adını rapor öncesi doğrula |
| S-PATH-015 | PASS | — | `PhotoImporter.cs:29-37` | Kaynak adı `..x.jpg` → hedef `1_photo_01.jpg`, `IsPhotoFileName` true | — |
| S-PATH-016 | PASS | — | `SessionStore.cs:147-153` | Mevcut `AllRegionsConformClosesAndInvalidPhotoRejected` geçti | — |

### 3.2 S-HTML

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-HTML-001 | PASS | — | `ReportBuilder.cs:161,329` | Mevcut `ReportEscapesAndHidesVoid` geçti | — |
| S-HTML-002 | PASS | — | `ReportBuilder.cs:28,38,53,64,86,144,158,185-205` | Parça/operasyon/seri/Inspector/tip/etkilenen parçalar/not/karar veren/referans/ek ölçüm/düzeltme alanlarında `<b>x</b>` kaçıldı | — |
| S-HTML-003 | PASS | — | `ReportBuilder.cs:249-252` | `a"b.jpg` → `a&quot;b.jpg`, öznitelik taşması yok | — |
| S-HTML-004 | PASS | — | `ReportBuilder.cs:251,318` | NaN/∞/1e308 koordinat ve `ImageWidth=0`: `Validate` hepsini eler, SVG yerine `<img>`; "NaN/Infinity/E+" yazılmadı | — |
| S-HTML-005 | PASS | — | `PhotoMarkup.cs:65-73`, `ReportBuilder.cs:251` | 10.000 şekil: `MaxShapes` aşımı ile işaretler tümden atlanır, çizilen şekil ≤ 30, süre < 2 sn | — |
| S-HTML-006 | PASS | — | `ReportBuilder.cs:248` | `CleanedFile="..\x.jpg"` → orijinal kullanıldı | — |
| S-HTML-007 | PASS | — | `ReportBuilder.cs:28` | `</title><b>x` kaçıldı | — |
| S-HTML-008 | PASS | — | `ReportBuilder.cs:153-154,297` | Ek ölçüm adı/değeri `<b>H</b>` kaçıldı | — |
| S-HTML-009 | PASS | — | `ReportBuilder.cs:111-116,140` | Bölge etiketi / bağlantı cümlesinde ham `<b>` yok (etiket sayısal biçimli olduğundan sınırlı kanıt) | — |
| S-HTML-010 | PASS | — | `ReportBuilder.cs` | `<script`, `<iframe`, `<object`, olay öznitelikleri, `javascript:`, dış `src/href` yok (SVG `xmlns` hariç tutuldu) | — |
| S-HTML-011 | AÇIK BULGU | ÖB-09 | `ReportBuilder.cs:27-30` | `<head>`'de CSP meta yok | CSP meta ekle |
| S-HTML-012 | AÇIK BULGU | ÖB-09 | `EdgePdfConverter.cs:33-38,70` | `--allow-file-access-from-files` var; JS kapatılmamış; ağ kapatılmamış (yalnız `--disable-background-networking`); profil siliniyor (`:70`, iyi) | Bayrakları sıkılaştır |
| S-HTML-013 | AÇIK BULGU | ÖB-09 | `InspectionViewModel.Photos.cs:213-224` | HTML `<oturum>\reports` (paylaşım) altına yazılıp Edge'e oradan veriliyor, PDF de paylaşımda `.tmp` üretiliyor | Yerel `%TEMP%`'te üret, PDF'i kopyala |
| S-HTML-014 | AÇIK BULGU | ÖB-09 | `RecordsViewModel.cs:153-157` | Yalnız `Rapor_v1.html` varken kabuğa `.html` verildi | PDF yoksa uyarı |
| S-HTML-015 | ATLANDI — Edge çalıştırılması "ağ yok" kuralıyla bağdaşmadı (bayraklara rağmen arka plan ağ trafiği olasılığı); isteğe bağlı vaka | — | — | — | Ağ yalıtımlı ortamda çalıştırılabilir |

### 3.3 S-CSV

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-CSV-001 | PASS | — | `CsvExport.cs:35-41` | Mevcut `CsvEscapes` geçti | — |
| S-CSV-002 | PASS | — | `CsvExport.cs:37` | `+1 -1 @x \t=1 \r=1 =1+1` `'` ile başlıyor | — |
| S-CSV-003 | ATLANDI — Excel olmadan doğrulanamadı | — | `CsvExport.cs:37` | Gözlem: `" =1+1"` ve `"＝1+1"` önek almıyor (test bunu belgeler) | Excel'de elle doğrula; güvenli taraf için baştaki boşluklar kırpılıp önekle |
| S-CSV-004 | PASS | — | `CsvExport.cs:38` | `a;b`, `a"b`, `a\nb`, `\r\n` tırnaklı/ikilenmiş; her kayıt 13 sütun | — |
| S-CSV-005 | AÇIK BULGU | — (yeni) | `CsvExport.cs:35-41,11-14` | Virgül ayraçlı yerelde `a,=1+1` bölünürse `=` ile başlayan hücre; `Cell` virgülü tırnaklamıyor, `sep=;` başlığı yok | `,` içeren hücreyi tırnakla / `sep=;` yaz |
| S-CSV-006 | PASS | — | `CsvExport.cs:11-33` | Tüm sütunlar (Parça…Etkilenen parçalar) `'=` önekli | — |
| S-CSV-007 | PASS | — | `RecordsViewModel.cs:160-175` | Mevcut `RecordsExportCsvWritesBomAndFilteredRows` geçti | — |

### 3.4 S-JSON

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-JSON-001 | AÇIK BULGU | ÖB-03 | `DashboardViewModel.cs:136,205-217` | Yarım `session.json` → `Refresh`'ten `JsonException` sızdı | `ReadJson` JsonException→InvalidDataException; oturum başına try/catch |
| S-JSON-002 | AÇIK BULGU | ÖB-03 | `InspectionViewModel.cs:797`, `SessionStore.cs:66-67` | `defects\2.json="{"` → `OpenSession`'dan `JsonException` sızdı | Aynı |
| S-JSON-003 | AÇIK BULGU | ÖB-03 | `Models.cs:51-54,68-71`, `InspectionViewModel.Photos.cs:22` | `photos:null` → NullReferenceException; `markups:null` → ArgumentNullException (`dispositions:null` yüklemede çökmedi) | Yüklemede null → boş liste |
| S-JSON-004 | PASS | — | `SessionStore.cs:311-312` | 5 MB+1 → `InvalidDataException`, VM hata mesajı gösterdi | — |
| S-JSON-005 | AÇIK BULGU | ÖB-03 | `InspectionViewModel.cs:797` | 200 seviye iç içe → Store `JsonException` atar (beklenen), VM yakalamıyor | Aynı |
| S-JSON-006 | AÇIK BULGU | ÖB-03 | `SessionStore.cs:19` | `status:999`, `decision:999`, `source:-1` kabul edildi | `allowIntegerValues:false` + `Enum.IsDefined` |
| S-JSON-007 | AÇIK BULGU | ÖB-03 | `InspectionViewModel.cs:797` | `status:"Foo"` → VM'den `JsonException` sızdı | Aynı |
| S-JSON-008 | AÇIK BULGU | ÖB-03 | `IndexBuilder.cs:43-47` | 3 oturumdan biri bozuk → `Rebuild` istisna attı (2 satır dönmedi) | Oturum başına try/catch |
| S-JSON-009 | AÇIK BULGU | ÖB-03 | `RecordsViewModel.cs:108-112`, `AdminViewModelBase.cs:20` | Admin Kayıtlar `Refresh` istisna sızdırdı | Aynı |
| S-JSON-010 | AÇIK BULGU | ÖB-03 | `SharedConfig.cs:72-87,97-99` | `[null]` → NullReferenceException; `fields:null` şablonu null alanla döndü; `{}` ve 101 tip varsayılana döndü | Null eleman/alan doğrulaması |
| S-JSON-011 | AÇIK BULGU | ÖB-03 | `SharedConfig.cs:134-149,225-246,284-295` | `measure_fields.json="[null]"` → NullReferenceException (diğer bozuk/`null`/`{"999":"x"}` varyantları varsayılana döndü) | Null eleman denetimi |
| S-JSON-012 | AÇIK BULGU | ÖB-03 | `IndexBuilder.cs:81-87` | Bozuk `_index.json` → `JsonException` | InvalidDataException'a çevir / boş liste |
| S-JSON-013 | AÇIK BULGU | ÖB-10 | `SessionStore.cs:94-100,297-307` | `5.json`(id=3) güncellemesi `3.json`'un SHA-256'sını değiştirdi | Ad/Id eşleşmesi zorunlu |
| S-JSON-014 | AÇIK BULGU | ÖB-10 | `SessionStore.cs:60-70` | `1.json` + `kopya.json` (id=1) → iki kayıt döndü | Yinelenen Id hatası |
| S-JSON-015 | AÇIK BULGU | ÖB-10 | `SessionStore.cs:81` | `id=int.MaxValue` → `-2147483648.json` oluştu | `checked` + üst sınır |
| S-JSON-016 | AÇIK BULGU | ÖB-10 | `SessionStore.cs:60-70` | `id=0` ve `id=-1` yüklendi | `Id>0` zorunlu |
| S-JSON-017 | AÇIK BULGU | ÖB-10 | `SessionStore.cs:66` | `notlar.json` Id=0 kusur olarak listelendi | `^\d+\.json$` filtresi |
| S-JSON-018 | PASS | — | `HiddenSuggestionSet.cs:52-75`, `UserSettingsAdapter.cs:75-81,105-121`, `CameraWindow.xaml.cs:652-665` | `FromJson` bozuk/`null`/10 MB/dizi/yanlış tip için çökmedi; diğer iki yükleyici JsonException/IO yakalıyor (REVIEW) | — |
| S-JSON-019 | PASS | — | `InstallSettings.cs:29-50` | `{`, `{"rootPath":5}`, `null`, `[]` → demo kök, istisna yok | — |
| S-JSON-020 | AÇIK BULGU | ÖB-03 | `SessionStore.cs:19`, `InspectionViewModel.cs:797` | 10.000 şekil kart listesini yavaşlatmadı (< 1 sn); ancak dosyada `NaN` koordinat `JsonException` ile VM'den sızdı (S-JSON-020b) | ÖB-03 düzeltmesi |

### 3.5 S-PIN

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-PIN-001 | PASS | ÖB-06 | `SharedConfig.cs:309-324` | `admin.json`'da PIN metni yok; iki kökte tuz ve özet farklı | — |
| S-PIN-002 | PASS | ÖB-06 | `SharedConfig.cs:50,314,342` | PBKDF2-HMAC-SHA256, 200.000 tur, 16 bayt tuz, 32 bayt çıktı; risk anahtar uzayında (6-12 rakam) | Parola/uzun PIN; bkz. S-PIN-003/006 |
| S-PIN-003 | PASS (ölçüm) | ÖB-06 | `SharedConfig.cs:326-339` | VerifyPin ≈ 19,3 ms/deneme (10 ölçüm); 10^6 uzay tek çekirdekte ≈ 5,4 saat, 16 çekirdekte ≈ 20 dk | Tur sayısını artır + `admin.json`'u koru |
| S-PIN-004 | AÇIK BULGU | ÖB-06 | `LoginViewModel.cs:17-18,79-91` | 5 hata sonrası yeni `LoginViewModel` ile doğru PIN hemen kabul edildi | Kalıcı sayaç/`lockedUntil` |
| S-PIN-005 | AÇIK BULGU | ÖB-06 | `LoginViewModel.cs:79-94` | Tek hatalı deneme sonrası denetim kaydı boş | `admin.login.fail` yaz |
| S-PIN-006 | AÇIK BULGU | ÖB-06 | `KURULUM.md:102-105` | `_config` için `MK_Muayene` ve `MK_Okuma` `RX`: `admin.json` herkese okunur | `admin.json`'u ayrı admin-only klasöre koy |
| S-PIN-007 | AÇIK BULGU | ÖB-07 | `LoginViewModel.cs:41,69-78` | `admin.json` silinince `IsFirstRun=true`, yeni PIN ile admin olunur | Sıfırlama uyarısı/ikinci doğrulama |
| S-PIN-008 | AÇIK BULGU | ÖB-08 | `SharedConfig.cs:332-334` | `Iterations=int.MaxValue` → 2 sn içinde dönmedi | Üst sınır |
| S-PIN-009 | AÇIK BULGU | ÖB-08 | `SharedConfig.cs:336-337`, `LoginViewModel.cs:97-98` | Bozuk Base64 → `FormatException` sızdı | catch'e ekle |
| S-PIN-010 | AÇIK BULGU | ÖB-08 | `SharedConfig.cs:332`, `LoginViewModel.cs:97-98` | Bozuk `admin.json` → `JsonException` sızdı | catch'e ekle |
| S-PIN-011 | PASS | — | `SharedConfig.cs:311,329` | 13 hane, `12a456`, boş, 5 hane reddedildi; 13 haneli/boş doğrulama false | — |
| S-PIN-011b (ek) | AÇIK BULGU | — (yeni) | `SharedConfig.cs:311` | Tam genişlik rakamlar (U+FF11..) PIN olarak kabul edildi | ASCII `[0-9]` |
| S-PIN-012 | PASS | — | `SharedConfig.cs:338` | `CryptographicOperations.FixedTimeEquals` | — |
| S-PIN-013 | PASS | — | `AdminShellViewModel.cs` | Mevcut `IdleLockAfterTenMinutesOfNoInput` geçti | — |

### 3.6 S-AUD

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-AUD-001 | PASS | — | `AuditLog.cs:42-76` | Mevcut `AuditChainDetectsTamper` geçti | — |
| S-AUD-002 | AÇIK BULGU | ÖB-05 | `AuditLog.cs:42-76` | Sondan satır silinince `Verify` (true, null) | Dış çapa (satır sayısı/son özet) |
| S-AUD-003 | AÇIK BULGU | ÖB-05 | `AuditLog.cs:128-134` | Baştan hesaplanmış sahte zincir "sağlam" bulundu | HMAC / imza |
| S-AUD-004 | AÇIK BULGU | ÖB-05 | `SessionStore.cs:297-304`, `AuditLog.cs:27-39` | `defects\1.json` elle düzenlendi, `Verify` "sağlam" | Denetime kayıt SHA-256 |
| S-AUD-005 | AÇIK BULGU | ÖB-05 | `InspectionViewModel.cs:301,744`, `Dialogs.cs:84,170,266`, `Photos.cs:72,111,180,231` | Tüm `Append` `details`'ları yalnız `#id`/ad/karar adı; kayıt-sonrası özet yok | `details`'e SHA-256 ekle |
| S-AUD-006 | AÇIK BULGU | ÖB-04 | `AuditLog.cs:110-126`, `InspectionViewModel.Photos.cs:71-72` | Bozuk son satırdan sonra fotoğraf ekleme `JsonException` sızdırdı (kayıt yazıldı, denetim yok) | LastHash toleranslı; kayıt+denetim tutarlı |
| S-AUD-007 | PASS | — | `AuditLog.cs:37-38` | `\n`/`\r\n` içeren actor/details tek satır; zincir sağlam | — |
| S-AUD-008 | AÇIK BULGU | ÖB-05 | `AuditLog.cs:44-46` | Günlük silinince `Verify` (true, null) | "Kayıt var, günlük yok" uyarısı |
| S-AUD-009 | PASS | — | `AuditLog.cs:31` | Mevcut `ParallelAuditAppendsKeepChain` geçti | — |
| S-AUD-010 | PASS (ölçüm) | — | `AuditLog.cs:110-120` | 50 MB / 208.051 satırlık günlükte 10 ekleme 611 ms (≈ 61 ms/ekleme); her eklemede dosya baştan okunuyor (doğrusal büyüme) | Son satırı dosya sonundan oku |
| S-AUD-011 | AÇIK BULGU | — (yeni) | `SettingsViewModel.cs:63-72` | Muayeneci tarafı (oturum, kusur, ölçüm, fotoğraf, karar, geri alma, void, rapor) denetleniyor; PIN, tip/açıklama, kilit silme, kimlik de yazıyor; **kök değişimi yazılmıyor** (S-AUD-011b) | `admin.root.change` ekle |

### 3.7 S-LOCK

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-LOCK-001 | PASS | — | `FileLock.cs:16-52` | Mevcut eşzamanlılık testleri geçti | — |
| S-LOCK-002 | PASS | ÖB-12 | `FileLock.cs:40-42` | 11 dk eski `session.lock`: VM mesajı "admin kontrol etmeli", veri değişmedi, istisna sızmadı | — |
| S-LOCK-003 | AÇIK BULGU | ÖB-12 | `FileLock.cs:54-69` | Gelecek tarihli kilit `TimeoutException` (15 sn varsayılan) | Gelecek tarihi şüpheli say |
| S-LOCK-004 | AÇIK BULGU | ÖB-12 | `SharedConfig.cs:347-351` | `StaleLocks` ileri tarihli kilidi listelemiyor | Listele |
| S-LOCK-005 | PASS | — | `SharedConfig.cs:357-367` | Açık tutamaçlı kilit silinemedi, dosya kaldı | — |
| S-LOCK-006 | PASS | — | `SharedConfig.cs:359-362` | `..\x.lock`, `x.txt`, kardeş kök `x.lock` ArgumentException; kardeş dosya kaldı | — |
| S-LOCK-007 | PASS | ÖB-12 | `AuditLog.cs:24-31`, `IndexBuilder.cs:71` | Kökte yeni tarihli `_audit.lock`/`_index.lock`: 15 sn sonra VM/dashboard hata mesajı verdi, uygulama çalıştı; gözlem: kusur dosyası yazıldı ama denetim satırı eklenemedi | Denetim kilidi sorunu için kayıt öncesi niyet satırı |
| S-LOCK-008 | PASS | — | `FileLock.cs:28-48` | `session.lock` adında klasör: ≤ 5 sn'de döngüden çıktı, VM hata mesajı verdi | — |
| S-LOCK-009 | AÇIK BULGU | ÖB-12 | `FileLock.cs:28-35` | Kilit dosyası boş: kullanıcı/makine/PID/zaman yok | İçine sahip bilgisi yaz |

### 3.8 S-PHOTO

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-PHOTO-001 | PASS | — | `PhotoImporter.cs:19-27,61-72` | `.txt`, metin içerikli `.jpg`, JPEG baytlı `.png` InvalidDataException; kopya yok | — |
| S-PHOTO-002 | PASS | — | `PhotoImporter.cs:23-24` | 25 MB + 1 bayt reddedildi | — |
| S-PHOTO-003 | AÇIK BULGU | ÖB-11 | `PhotoImporter.cs:19-27`, `PhotoEditorWindow.xaml.cs:130-143` | İçe aktarmada yalnız uzantı/boyut/imza; düzenleyici `GetPixelDataAsync` + `new BitmapTransform()` ile tam çözünürlüğü BGRA'ya açar | Boyut sınırı + ölçekleme |
| S-PHOTO-004 | AÇIK BULGU | ÖB-11 | `BackgroundRemover.cs:18` | 50000×50000 ArgumentException; 65536×65536 (int çarpımı 0'a sarar) doğrulamadan geçip `IndexOutOfRangeException` | `checked`/`long` + ArgumentException |
| S-PHOTO-005 | AÇIK BULGU | ÖB-14 | `PhotoImporter.cs:36` | APP1 `Exif` hedef dosyada aynen duruyor | JPEG yeniden kodla / segment sil |
| S-PHOTO-006 | AÇIK BULGU | ÖB-14 | `ReportBuilder.cs:248-252` | Temiz kopya yoksa rapor orijinal (EXIF'li) dosyaya bağlanır; PDF'e EXIF'in gömülüp gömülmediği doğrulanmadı | Temizlenmiş kopya kullan |
| S-PHOTO-007 | PASS | — | `PhotoImporter.cs:44-59` | `..\x.jpg`, `_clean` adı ArgumentException; JPEG olmayan bayt InvalidDataException | — |
| S-PHOTO-008 | PASS | — | `PhotoImporter.cs:32-36` | `_01` ve `_03` varken (02 silinmiş) ikinci içe aktarma `File.Copy(overwrite:false)` ile IOException; mevcut dosya değişmedi | — |
| S-PHOTO-009 | PASS | — | `ThumbnailView.cs:37` | `DecodePixelWidth = 360` | — |
| S-PHOTO-010 | AÇIK BULGU | ÖB-11 | `PhotoEditorWindow.xaml.cs:82,117-118` | Catch filtresi `OutOfMemoryException` içermez; `_ = LoadAsync()` gözlenmez | Filtreye ekle, görevi gözle |
| S-PHOTO-011 | PASS | — | `CameraWindow.xaml.cs:600-622` | Ara `%TEMP%\muayene_*.jpg` `finally`'de siliniyor | — |

### 3.9 S-LAUNCH

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-LAUNCH-001 | PASS | — | `ProgramLayout.cs:24-26` | `..\x`, `C:\x`, `\\testhost\s`, boş, `CON` → FileNotFoundException | — |
| S-LAUNCH-002 | AÇIK BULGU | — (yeni) | `ProgramLayout.cs:25,30` | `surum.txt="."` → `<Program>\Inspector-x64\…exe` çözüldü | Sürüm biçimi regex'i |
| S-LAUNCH-003 | AÇIK BULGU | — (yeni) | `ProgramLayout.cs:25` | `abc`, `x y`, `1.0.2 x` kabul edildi | `^\d+(\.\d+){1,3}$` |
| S-LAUNCH-004 | PASS | — | `Launcher/Program.cs:24-31` | `UseShellExecute=false`, mutlak yol, `ArgumentList` | — |
| S-LAUNCH-005 | PASS | — | `InstallSettings.cs:13-25` | 3. üst klasördeki ayar bulundu, 4.'sü bulunmadı; 1-3. üst klasörlerin salt okunur olması belgelenmemiş (öneri) | Belgeye ekle |
| S-LAUNCH-006 | AÇIK BULGU | — (yeni) | `InstallSettings.cs:36-40` | `%TEMP%\…` genişletildi, UNC yalnız metin döndü (erişilmedi); göreli `veri` olduğu gibi döndü | Göreli yolu reddet |
| S-LAUNCH-007 | PASS | — | `EdgeBrowser.cs:12-21` | Yalnız `Program Files (x86)` ve `Program Files`; PATH yok | — |
| S-LAUNCH-008 | AÇIK BULGU | ÖB-01 | `EdgeBrowser.cs:26-30`, `ShellService.cs:13`, `WinUiWindowService.cs:52` | Uzantı `.pdf/.html/.htm` değilse (ya da Edge yoksa) `UseShellExecute=true` ile her dosya açılır | Uzantı + kök altı izin listesi |
| S-LAUNCH-009 | PASS | — | `OKU-BENI.txt:19`, `KURULUM.md:69` | Program klasöründe kullanıcıya yalnız okuma; iki belge tutarlı | — |
| S-LAUNCH-010 | PASS | — | `Inspector/app.manifest` | `requestedExecutionLevel` yok → yönetici yetkisi istenmiyor (örtük `asInvoker`) | `level="asInvoker"` açıkça yaz (öneri) |

### 3.10 S-SEC

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-SEC-001 | PASS | — | `git grep` | 7 eşleşme: yalnızca `PasswordBox` ve plan metni; gerçek değer yok | — |
| S-SEC-002 | PASS | — | `git log -p --all` | Eklenen satırlarda 21 eşleşme, hepsi `PasswordBox/Password` kod adları; `settings.json` geçmişi yalnız `%USERPROFILE%\MuayeneKayitDemo` | — |
| S-SEC-003 | PASS | — | `git log -p --all` | UNC önekleri: `\\SUNUCU\`, `\\MuayeneKayit\`, `\\Veri\`, `\\Yerel\` (örnek/yol parçaları); gerçek host adı yok | — |
| S-SEC-004 | AÇIK BULGU (bilgi) | ÖB-15 | `git log --format='%an|%ae'` | 2 kimlik, 40 commit, kişisel e-posta (`a***@gmail.com`) | `user.email`'i noreply'a çek (geçmiş yeniden yazılmaz) |
| S-SEC-005 | ATLANDI — `dotnet list package --vulnerable` ağ gerektirir (§0.3/§0.4) | — | `Presentation.csproj:14`, `Inspector.csproj:46-48` | Sürümler: CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.DependencyInjection 9.0.0, Microsoft.WindowsAppSDK 1.8.260921001, Microsoft.Windows.SDK.BuildTools 10.0.26100.9169; `docs/kurulum-video/package-lock.json` (yalnız geliştirme) incelenmedi | Ağ izni olduğunda `--vulnerable` çalıştır |
| S-SEC-006 | PASS | — | `nuget.config:11`, `offline-paketler.ps1:63` | Yalnız `https` nuget.org + yerel klasör | — |
| S-SEC-007 | AÇIK BULGU | — | `.gitignore:1-9` | `*.pfx`, `*.snk`, `admin.json`, `_config/`, `_audit.log` dışlanmamış | Desenleri ekle |
| S-SEC-008 | PASS | — | `build-release.ps1` | İndirme, sabit kimlik bilgisi, imza anahtarı yok (not: çıktı exe imzasız) | — |

### 3.11 S-PRIV

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-PRIV-001 | PASS | — | `WindowsUserAdapter.cs:242-252`, `Models.cs:9,107`, `LoginViewModel.cs:86,94` | Envanter: ad/sicil no (`Session.Inspector`, `Disposition.DecidedBy`, `MeasurementEdit.By`, denetim `actor`), makine adı (`admin.login*`, `admin.pin.change`), AD görünen adı; amaç dışı alan görülmedi | — |
| S-PRIV-002 | AÇIK BULGU | — | `AuditLog.cs`, `LoginViewModel.cs:86,94` | Denetim günlüğünde ad/makine adı var; okuyucu erişimi ve saklama süresi (KVKK) hiçbir belgede yok | Saklama/erişim politikasını belgele |
| S-PRIV-003 | AÇIK BULGU | ÖB-14 | `PhotoImporter.cs:36` | EXIF paylaşıma gidiyor (bkz. S-PHOTO-005/006) | Bkz. S-PHOTO-005 |
| S-PRIV-004 | PASS | — | `UserSettingsAdapter.cs`, `CameraWindow.xaml.cs:652-678` | Yalnız birim/tema/gizli öneri (parça no) ve kamera cihaz kimliği | — |
| S-PRIV-005 | PASS | — | `EdgePdfConverter.cs:66-71`, `ReportFiles.cs` | Mevcut `VmReportWritesHtmlAndOpensPdf` (PDF başarılıysa HTML siliniyor) geçti; `finally` `.tmp` ve Edge profilini siliyor | — |
| S-PRIV-006 | AÇIK BULGU (bilgi) | ÖB-15 | S-SEC-004 | Not düşüldü | Bkz. S-SEC-004 |

### 3.12 S-NTFS

| ID | Sonuç | ÖB | Kanıt | Gözlem | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-NTFS-001 | AÇIK BULGU | ÖB-07 | `OKU-BENI.txt:19` vs `KURULUM.md:89,102-105` | OKU-BENI `_config` kısıtından söz etmiyor (VERİ klasörüne okuma+yazma) | OKU-BENI'ye `_config` kısıtı ve KURULUM §3 yönlendirmesi |
| S-NTFS-002 | AÇIK BULGU | ÖB-07 | `KURULUM.md:88-89` | "Yalnız admin grubu yazabilmeli" yazıyor; PIN sıfırlamanın denetlenmesi gerektiği yazmıyor | Sıfırlama denetim şartını ekle |
| S-NTFS-003 | PASS | ÖB-07 | `SharedConfig.cs:119`, `AdminViewModelBase.cs:20` | UNIT: salt okunur dosyada `UnauthorizedAccessException`, `TypesViewModel.Save` hata mesajı verdi; script: `icacls deny W` ile `_config` yazımı `UnauthorizedAccessException` (çökme yok, dosya oluşmadı); `icacls /remove:d` yapıldı | — |
| S-NTFS-004 | AÇIK BULGU | ÖB-05 | `KURULUM.md:111-118` | İkinci izin düzeninde muayenecinin `_audit.log`'u silebileceği / yalnız tespit edildiği ve yalnız-ekleme (AD) önerisi yazılı değil | Belgeye ekle |
| S-NTFS-005 | AÇIK BULGU | ÖB-05 | `KURULUM.md:124` | "Silindiğini ya da değiştirildiğini gösterir" cümlesi S-AUD-002/003/008 ile çelişiyor | Cümleyi düzelt ya da ÖB-05'i gider |
| S-NTFS-006 | AÇIK BULGU | — | `InstallSettings.cs:52-63`, `KURULUM.md:53,69`, `OKU-BENI.txt:19` | Admin panelinin `MuayeneKayit.settings.json`'u program klasörüne yazması için gereken yetki belgelenmemiş; "kullanıcıya program klasörü salt okunur" ile çelişir | Yetkiyi (yalnız admin hesabı yazar) belgele |

## 4. Betik (SCRIPT) çıktılarından özet

Tüm betikler yalnızca `%TEMP%\MK-SEC-<guid>` köklerinde çalıştı (`Assert-SafeRoot`), junction'lar önce `rmdir` ile kaldırıldı, `icacls` yasağı geri alındı; bitişte `MK-SEC-*` ve `MuayeneKayit-test-*` kalıntısı yok.

- S-PATH-006: junction üzerinden kök dışı oturum indekse girdi (`Index satır sayısı: 1`, `PD170\OP40\SN9`).
- S-PATH-009: `StaleLocks` junction ardındaki `jn\x.lock`'u listeledi; `RemoveStaleLock` istisnasız çalıştı ve kök dışı `x.lock` silindi.
- S-AUD-010: 50,0 MB günlük (208.051 satır), 10 ekleme 611 ms.
- S-NTFS-003: `SaveDefectTypes` → `UnauthorizedAccessException`, dosya oluşmadı.
- S-SEC-001..004: bölüm 3.10'daki bulgular (e-posta maskeli).

Not: PowerShell betiklerinin Türkçe etiketleri konsolda bozuk karakterle görünür (betik BOM'suz kaydedildi); sonuç değerleri etkilenmez.

## 5. Koşu notları

- Çıkış kodu 0. `openFindings` listesindeki testler çıkış kodunu etkilemez; bunlardan biri düzeltilirse koşu "DÜZELDİ — normal listeye taşıyın" yazar.
- `ATLANDI` yakalama (`SecurityTests.SkipException`) `Program.cs`'e eklendi; son koşuda hiçbir birim test atlanmadı (junction testi için `mklink /J` çalıştı).
- Mevcut testlerin bıraktığı `%TEMP%\MuayeneKayit-src-*` / `MuayeneKayit-bad-*` dosyaları (yeni testlerle ilgisiz) silinmedi.
- `git status`: `tests/MuayeneKayit.Tests/Program.cs` (değişti), `tests/MuayeneKayit.Tests/SecurityTests.cs` (yeni), `docs/test/` (plan + bu rapor, izlenmeyen). `src/` değişmedi.
