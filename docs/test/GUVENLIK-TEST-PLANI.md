# MuayeneKayit — Güvenlik Tehdit Modeli ve Test Planı

Depo: `C:\Users\PC\Documents\JUNK\MuayeneKayit` · Dal: `main` · İncelenen commit: `17cf3bf` · Sürüm: 1.0.2
Hazırlayan: kod okuma (statik inceleme). Bu belge kaynak kodu değiştirmez; yalnızca test edilecekleri ve şu an görülen ön bulguları listeler.

---

## 0. YÜRÜTME GÜVENLİK KURALLARI (önce bunu okuyun)

Bu kurallar her testten önce geçerlidir; bir test bu kurallarla çelişiyorsa test **atlanır** ve nedeni yazılır.

1. **Yalnızca geçici kök.** Her test, scratch/temp altında yeni açılan bir klasörde çalışır:
   `$Root = Join-Path $env:TEMP ("MK-SEC-" + [guid]::NewGuid().ToString('N'))`.
   Kullanıcının gerçek veri kökü (`MuayeneKayit.settings.json` içindeki `rootPath`, `%USERPROFILE%\MuayeneKayitDemo`) ve **ağ paylaşımı asla kullanılmaz**, okunmaz, listelenmez.
2. **Koruma fonksiyonu.** Her [SCRIPT] dosya işleminden önce şu denetim çağrılır; tutmazsa script durur:
   ```powershell
   function Assert-SafeRoot([string]$p) {
     $full = [IO.Path]::GetFullPath($p)
     $tmp  = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
     if (-not $full.StartsWith($tmp, [StringComparison]::OrdinalIgnoreCase) -or $full -notmatch '\\MK-SEC-[0-9a-f]{32}') {
       throw "GÜVENLİK: $full geçici test kökü değil, durduruldu."
     }
   }
   ```
3. **Ağ saldırısı yok.** Dış host'a, şirket ağına, SMB/HTTP sunucularına istek atılmaz. Ağ davranışı gerekiyorsa yalnızca `127.0.0.1` üzerinde script'in kendi açtığı `System.Net.Sockets.TcpListener` kullanılır. UNC yolları yalnızca **string olarak** üretilir ve birim testinde sahte servisle yakalanır; gerçek UNC erişimi yapılmaz.
4. **Hiçbir şey kurulmaz.** NuGet paketi, dotnet global tool, npm paketi, tarayıcı eklentisi, Sysinternals vb. indirilmez/kurulmaz. Yalnızca makinede zaten olanlar: `dotnet` SDK (9.0.x), `git`, PowerShell 5.1, `cmd` (`mklink`, `icacls`, `fsutil` yalnızca okuma), Windows ile gelen Edge. Yönetici yetkisi istenmez; yönetici gerektiren adım (ör. `mklink` sembolik link) başarısız olursa test "atlandı — yetki yok" diye işaretlenir.
5. **Yıkıcı komut yalnızca geçici kökte.** `Remove-Item -Recurse`, `del`, `icacls /deny`, dosya üzerine yazma yalnızca `Assert-SafeRoot`'tan geçen yollarda. Test bitince geçici kök silinir (`Remove-Item -Recurse -Force $Root` — önce `Assert-SafeRoot $Root`). Junction içeren kök silinirken **önce junction'lar** `cmd /c rmdir <junction>` ile kaldırılır (hedefin içeriği silinmesin diye `Remove-Item -Recurse` junction'a uygulanmaz).
6. **PIN'ler yalnızca test için üretilir.** Örn. `Get-Random -Minimum 100000 -Maximum 999999` ya da birim testinde sabit `"482916"`. Gerçek admin PIN'i sorulmaz, `admin.json` gerçek kökten kopyalanmaz. Kaba kuvvet ölçümü yalnızca test kökündeki, testin kendi ürettiği `admin.json` üzerinde ve **süre sınırlı** (en çok 60 sn) yapılır.
7. **Uygulama açılmaz (izinsiz).** Kullanıcı tercihi: arayüzü açmadan önce sorulur. Bu plandaki testler ViewModel + sahte servislerle ([UNIT]) yazılmıştır; GUI gerekirse adım "kullanıcı onayı gerekir" olarak işaretlenir ve atlanır.
8. **Kaynak kod değiştirilmez.** Yürütücü yalnızca `tests/MuayeneKayit.Tests/` altına yeni test dosyası ekler ve `Program.cs` test listesine kayıt yapar (bkz. §5). `src/` altına dokunulmaz; bulgu düzeltilmez, raporlanır.
9. **Gizli veri yazdırılmaz.** Git geçmişi taramasında bulunan e-posta/anahtar değerleri raporda **maskelenir** (`a***@gmail.com`).

---

## 1. Mimari özeti ve güven sınırları

| Bileşen | Konum | Güven notu |
|---|---|---|
| Ortak veri kökü | `rootPath` (`InstallSettings.cs:29-50`, exe klasörü + en çok 3 üst klasör) | Tüm muayeneciler okur+yazar (KURULUM: `MK_Muayene:(OI)(CI)M`). **Saldırı yüzeyinin merkezi.** |
| `_config` | `<root>\_config` (`SharedConfig.cs:47-67`) | Muayeneci yalnızca okumalı (KURULUM.md:102-105). `admin.json` PIN özeti burada ve **herkes okuyabiliyor**. |
| Oturum kayıtları | `<root>\Parça\Op\Seri\session.json`, `defects\N.json`, fotoğraflar, `reports\` | Program yazar ama herkes doğrudan dosya düzenleyebilir; içerik imzası yok. |
| Denetim kaydı | `<root>\_audit.log` (`AuditLog.cs`) | Anahtarsız SHA-256 zinciri. |
| Kilitler | `*.lock` (`FileLock.cs`) | Oluşturma/silme herkesin elinde. |
| Program klasörü | `Program\MuayeneKayit.exe`, `surum.txt`, `<sürüm>\Inspector-x64\` | Kullanıcı yalnızca okumalı (OKU-BENI.txt:19). Yazabilen = kod çalıştırır. |
| Yerel | `%LOCALAPPDATA%\MuayeneKayit\` (user-settings, hidden-suggestions, camera.json), `%TEMP%` (Edge profili, kamera ara dosyası) | Kullanıcının kendi profili. |
| Edge (headless) | `EdgePdfConverter.cs` | Paylaşımdaki HTML'i JS açık, `--allow-file-access-from-files` ile işler. |
| Admin paneli | Aynı süreç, PIN ile (`LoginViewModel.cs`) | **PIN yalnızca arayüz kapısıdır**; tüm admin işlemleri `_config`'e dosya yazmaktır. Gerçek güvenlik sınırı NTFS'tir. |

### Saldırgan profilleri

- **A1 — Kötü niyetli/hatalı muayeneci:** veri kökünde Modify yetkisi; `_config`'e yalnızca okuma (doğru kurulumda). Ana profil.
- **A2 — Okuyucu (`MK_Okuma`):** yalnızca okur; `admin.json`'u kopyalayıp çevrimdışı kaba kuvvet deneyebilir.
- **A3 — Program klasörüne yazabilen kişi** (yanlış izin): `surum.txt`, exe, DLL, `MuayeneKayit.settings.json` değiştirir.
- **A4 — Aynı PC'deki başka yerel kullanıcı / zararlı yazılım:** `%TEMP%`, profil.
- **A5 — Bozuk dosya (kasıtsız):** ağ kopması, yarım yazma, antivirüs, elle düzenleme.

Kapsam dışı: aktif ağ saldırısı (MITM, SMB relay denemesi), AD/sunucu yapılandırması, fiziksel erişim.

### Varlıklar

Kayıt bütünlüğü (karar geçmişi, ölçümler, MRB/hurda kararları), denetim izi, admin yetkisi, kullanıcı iş istasyonunda kod çalıştırma, NTLM kimlik bilgisi, kişisel veri (ad, sicil no, makine adı), erişilebilirlik (dashboard/rapor).

---

## 2. Ön bulgular (kod okumasından, test ile doğrulanacak)

| No | Önem | Özet | Konum | İlgili test |
|---|---|---|---|---|
| ÖB-01 | **Kritik** | Kapalı kusurda fotoğraf görüntülenirken `Markups[photo].CleanedFile` **doğrulanmadan** `Path.Combine`'a verilir; mutlak yol/UNC/`..\` kabul edilir. Yol `EdgeBrowser.OpenFile`'a gider; `.pdf/.html` değilse `UseShellExecute = true` ile açılır → `defects\N.json`'u düzenleyebilen her muayeneci, o kaydı görüntüleyen başka kullanıcının (admin dahil) bilgisayarında **istediği exe/bat/lnk'yi çalıştırabilir**. | `InspectionViewModel.Photos.cs:147`, `EdgeBrowser.cs:24-33` | S-PATH-010, S-PATH-011 |
| ÖB-02 | Yüksek | `Defect.Photos` listesindeki adlar okunurken doğrulanmaz. Küçük resim yolu `Path.Combine(directory, photo)` → mutlak/UNC yolda `File.OpenRead` (ThumbnailView.cs:38) çalışır: başka klasördeki görüntüler okunur, UNC ile **NTLM özeti dış hosta gider**. Raporda `src="../defects/{photo}"` göreli dolaşıma açıktır. | `InspectionViewModel.Photos.cs:21-36,53`, `InspectionViewModel.Dialogs.cs:208-211`, `ReportBuilder.cs:248-252`, `ThumbnailView.cs:32-41` | S-PATH-012..014 |
| ÖB-03 | Yüksek | `JsonException`, `NullReferenceException` (`"photos": null`), `FormatException` hiçbir `Run` sarmalayıcısında yakalanmıyor ve global `UnhandledException` işleyicisi yok. **Tek bir bozuk `session.json`/`defects\N.json`** dashboard'u ve admin "Kayıtlar"ı (IndexBuilder.Rebuild tüm kökü tarar) herkes için çökertir; kalıcı DoS. | `InspectionViewModel.cs:797`, `DashboardViewModel.cs:211`, `AdminViewModelBase.cs:20`, `LoginViewModel.cs:97`, `IndexBuilder.cs:43-47`, `App.xaml.cs` (işleyici yok) | S-JSON-001..012 |
| ÖB-04 | Yüksek | `_audit.log`'un son satırı bozuksa `AuditLog.LastHash` `JsonException` atar → her denetimli işlem **kayıt dosyası yazıldıktan sonra** çöker (kayıt değişti, denetim satırı yok) ve program kapanır. Herkes `_audit.log`'a satır ekleyebilir. | `AuditLog.cs:110-126` (125), çağıranlar ör. `InspectionViewModel.Photos.cs:71-72` | S-AUD-006 |
| ÖB-05 | Yüksek | Denetim zinciri anahtarsız SHA-256: dosyanın tamamı yeniden hesaplanarak yazılırsa ya da **sondan satır silinirse** `Verify` "sağlam" der. Kayıt içerikleri (karar, ölçüm) denetime özetlenmez → `defects\N.json` elle düzenlenince **iz kalmaz**, rapor düzenlenmiş veriden üretilir. | `AuditLog.cs:27-76,128-134`, `SessionStore.cs:297-304` | S-AUD-001..005, S-AUD-008 |
| ÖB-06 | Yüksek | `admin.json` muayeneci/okuyucu tarafından okunabilir (KURULUM.md:105 RX). 6 haneli PIN + PBKDF2-SHA256 200.000 tur → çevrimdışı kaba kuvvet tek çekirdekte ~1 gün, paralel saatler. Kilitlenme yalnızca bellekte (`_failed`, `_lockedUntil`), program yeniden açılınca sıfırlanır; tek tek hatalı denemeler denetime yazılmaz. | `SharedConfig.cs:50,309-342`, `LoginViewModel.cs:12-18,79-91` | S-PIN-001..006 |
| ÖB-07 | Yüksek | `OKU-BENI.txt:19` "kullanıcıların VERİ klasörüne okuma + yazma" der, `_config` kısıtından söz etmez. Bu kurulumla her kullanıcı `admin.json`'u silip ilk kurulum ekranından **kendi PIN'ini belirler** (KURULUM.md:88 bu yolu "PIN unutulursa" diye belgeler). Belgeler çelişkili. | `OKU-BENI.txt:19`, `KURULUM.md:88-105`, `LoginViewModel.cs:41,69-78` | S-PIN-007, S-NTFS-001..003 |
| ÖB-08 | Orta | `VerifyPin` dosyadaki `Iterations` değerine yalnız alt sınır koyar → `int.MaxValue` arayüz iş parçacığını saatlerce kilitler. Bozuk Base64 → `FormatException`, bozuk JSON → `JsonException` yakalanmaz → admin girişi çöker. | `SharedConfig.cs:326-339`, `LoginViewModel.cs:97-98` | S-PIN-008..010 |
| ÖB-09 | Orta | Edge headless: JS açık, `--allow-file-access-from-files`, ağ kapatılmamış, raporda CSP yok. HTML önce **paylaşıma** yazılıp sonra Edge'e verilir (TOCTOU): A1 arada HTML'e script koyarsa script kurbanın yerel dosyalarını okuyup PDF'e gömebilir/ağa gönderebilir. Admin "son rapor"da PDF yoksa paylaşımdaki `Rapor_vN.html`'i tarayıcıda açar (yerleştirilmiş HTML). | `EdgePdfConverter.cs:31-39`, `InspectionViewModel.Photos.cs:216-232`, `RecordsViewModel.cs:149-157` | S-HTML-010..014 |
| ÖB-10 | Orta | Kusur numarası dosya adından değil içerikten (`Id`) alınır: `LoadDefects` `defects\*.json` altındaki **her** dosyayı okur, ad/içerik eşleşmesini denetlemez; `UpdateDefect(5)` `5.json`'u okur ama `DefectPath(defect.Id)` ile yazar → `5.json` içinde `"id": 3` ise güncelleme `3.json`'un **üzerine yazar**. Aynı `Id`'li iki dosyada `First(d => d.Id == id)` rastgele birini seçer. Yeni numara `Max(Id) + 1` (`unchecked`, projede `CheckForOverflowUnderflow` yok): `Id = int.MaxValue` → `int.MinValue` → numara çakışması, negatif dosya adı (`-2147483648.json`), kayıt üzerine yazma riski. | `SessionStore.cs:66-69` (ad denetimi yok), `:81`, `:155` (taşma), `:94-100`, `:297-307` (içerikteki Id ile yazma), `InspectionViewModel.Photos.cs:140`, `Directory.Build.props` (taşma denetimi kapalı) | S-JSON-013..017 |
| ÖB-11 | Orta | Fotoğraf düzenleyici görüntüyü **piksel boyutu sınırı olmadan** tam çözünürlükte BGRA'ya açar (`GetPixelDataAsync` + `WriteableBitmap(_w,_h)`). İçe aktarmada yalnızca dosya boyutu (25 MB) ve ilk baytlar denetlenir; küçük ama çok büyük boyut bildiren bir PNG/JPEG GB'larca bellek ister. `catch` filtresi `OutOfMemoryException`'ı kapsamaz. `BackgroundRemover.ComputeMask` içindeki `width * height * 4` `int` taşmasına açıktır (denetim yanlış geçebilir). | `PhotoEditorWindow.xaml.cs:125-137`, `:141-143`, `:117-118` (catch filtresi), `PhotoImporter.cs:122,136-140`, `BackgroundRemover.cs:18` | S-PHOTO-003, S-PHOTO-004, S-PHOTO-010 |
| ÖB-12 | Orta | Kilit dosyaları herkesin yazabildiği kökte, sabit adlarla (`session.lock`, `_audit.lock`, `_index.lock`) durur. Var olan kilit **eski** sayılırsa `Acquire` beklemeden hata atar (iş admin müdahalesine kadar durur). "Eski" kararı yalnızca kullanıcının değiştirebildiği `LastWriteTime`'a dayanır: **ileri tarihli** bir kilit hiçbir zaman eski sayılmaz → her işlem 15 sn sonra `TimeoutException`; admin bakım ekranı (`StaleLocks`) bu dosyayı **listelemez**, arayüzden kaldırılamaz. Kökteki tek `_audit.lock` tüm denetimli işlemleri, `_index.lock` dashboard/kayıt yenilemeyi durdurur. Kilitte sahip bilgisi (kullanıcı/makine/PID) yok. | `FileLock.cs:83-88`, `:97-106`, `SharedConfig.cs:347-351`, `:365`, `AuditLog.cs:24,31`, `IndexBuilder.cs:29,71`, `SessionStore.cs:78,92,110,139,207,252` | S-LOCK-002..004, S-LOCK-007, S-LOCK-009 |
| ÖB-13 | Orta | Yol denetimleri yalnızca **metin** üzerindedir: `SafePath` ayrılmış aygıt adlarını (`CON`, `NUL`, `AUX`, `PRN`, `COM1`, `LPT1`, `con.txt` …) kabul eder; `GetFullPath` + `StartsWith` junction/sembolik bağlantıyı çözmez. `IndexBuilder.Rebuild` klasörleri reparse-point denetimi olmadan gezer (kök içindeki bir junction kök dışını okutur ve admin "klasörü aç"a gider); `StaleLocks` `AllDirectories` ile gezer, `RemoveStaleLock` yalnızca metin önekine bakar → junction arkasındaki kök dışı `.lock` silinebilir. | `SafePath.cs:12-14,22`, `:31-39`, `IndexBuilder.cs:35-47`, `SharedConfig.cs:348`, `:359-367` | S-PATH-003, S-PATH-006, S-PATH-009 |
| ÖB-14 | Düşük | Fotoğraf içe aktarma dosyayı **bayt bayt** kopyalar (`File.Copy`); EXIF (GPS konumu, cihaz seri no, sahip adı, çekim zamanı) paylaşımda kalır, temiz kopya yoksa rapor orijinali kullanır. (Düzenleyicinin yazdığı `_clean.jpg` yeniden kodlandığı için EXIF taşımaz.) | `PhotoImporter.cs:149`, `ReportBuilder.cs:248-252`, `CameraWindow.xaml.cs:606` (kamera kaydı akışı aynen yazılır) | S-PHOTO-005, S-PHOTO-006, S-PRIV-003 |
| ÖB-15 | Düşük (bilgi) | Git commit üst verisinde kişisel e-posta adresleri var (2 kimlik, 40 commit; ör. `a***@gmail.com`). Kod güvenliği sorunu değil; depo dışarı paylaşılırsa kişisel veri sızar. **Yalnızca not:** geçmiş yeniden yazılmaz; ileride `git config user.email` kurumsal/noreply adrese çekilebilir. | `git log --format=%ae` | S-SEC-004, S-PRIV-006 |
| ÖB-16 | Düşük | Admin "klasörü aç" kök denetimi **ayraçsız önek** kullanır: kök `D:\Veri` iken `D:\Veri2\…` ve `D:\Veri-yedek\…` geçer; junction da çözülmez. Aynı dosyadaki `OpenLatestReport` klasörü hiç denetlemez. (Karşılaştırma: `SafePath.cs:38` ve `SharedConfig.cs:359` ayraç ekler — doğru örnek.) Satır yolu dizin adlarından üretildiği için etki sınırlı. | `RecordsViewModel.cs:144`, `:149-157`, `:27` | S-PATH-007, S-PATH-008 |

Not: ÖB-10..ÖB-16 bu revizyonda kodla tek tek karşılaştırıldı (commit `17cf3bf`). Satır numaraları bu commit içindir; kod değişirse yürütücü satırı yeniden bulup raporda günceller.

---

## 3. Test vakaları (alan bazında)

Sütunlar: **ID** · **kontrol edilen özellik** (doğrulanacak sağlamlaştırma) · **kod yeri** · **adımlar** (kısa doğrulama, zararsız girdi) · **beklenen güvenli davranış** · **yöntem** · **önem** · **ilgili ön bulgu**.

Yöntem: **[UNIT]** = `tests/MuayeneKayit.Tests` altında C# testi (Core + Presentation, sahte servislerle; bkz. §5). **[SCRIPT]** = PowerShell, yalnızca §0'daki geçici kökte. **[REVIEW]** = kod/belge okuması, sonuç gerekçesiyle yazılır.
"Mevcut test" yazan satırlar regresyon kontrolüdür; aynı testin geçmesi yeterlidir, yeni test yazılmaz.
Tüm girdiler zararsızdır: yol dolaşımı için `..\x`, HTML için `<b>x</b>`, CSV için `=1+1`, UNC için yalnızca metin olarak `\\testhost\paylasim\x.jpg` (erişilmez, sahte servis yakalar).

### 3.1 S-PATH — yol doğrulama ve kök sınırı

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-PATH-001 | Parça/operasyon/seri dolaşım içeremez | `SafePath.cs:16-26` | `ValidateSegment("..\x")`, `"a\b"`, `"a/b"`, `".."` | `ArgumentException`; klasör oluşmaz | [UNIT] (mevcut: `SafePathRejectsTraversal`) | Yüksek | — |
| S-PATH-002 | Mutlak/sürücü/UNC/ADS biçimleri reddedilir | `SafePath.cs:12-14` | `"C:x"`, `"\\testhost\s"`, `"a:b"`, `"x::$DATA"` | Hepsi `ArgumentException` | [UNIT] | Yüksek | — |
| S-PATH-003 | Ayrılmış aygıt adları reddedilir | `SafePath.cs:12-14,22` | `ValidateSegment` ile `"CON"`, `"nul"`, `"AUX"`, `"PRN"`, `"COM1"`, `"LPT1"`, `"con.txt"`, `"NUL.x"` | Hepsi `ArgumentException` | [UNIT] (şu an kabul ediliyor → AÇIK BULGU) | Orta | ÖB-13 |
| S-PATH-004 | Sonda nokta/boşluk ve görünmez karakter reddedilir | `SafePath.cs:21-22` | `"PD170."`, `"PD170 "` (trim sonrası), `"PD\u200B170"`, `"PD\u202E170"` | Sonda nokta → red; sıfır genişlikli/yön karakteri → red (regex `\p{L}\p{N}` dışı) | [UNIT] | Düşük | — |
| S-PATH-005 | Uzunluk sınırı | `SafePath.cs:13` | 64 ve 65 karakterlik segment | 64 kabul, 65 red; tam yol 260 sınırına takılırsa anlaşılır hata | [UNIT] | Düşük | — |
| S-PATH-006 | Kök içindeki junction izlenmez | `SafePath.cs:31-39`, `IndexBuilder.cs:35-47` | Geçici kökte `cmd /c mklink /J $Root\PD170 $Root2\hedef` (yönetici gerekmez); hedefte örnek `session.json`; `SessionStore.FolderFor` ve `IndexBuilder.Rebuild` çağır | Reparse point'li segment reddedilir ya da index'te atlanır; kök dışı dosya okunmaz | [UNIT] + [SCRIPT] (temizlikte önce `rmdir` junction, §0.5) → şu an izleniyor: AÇIK BULGU | Orta | ÖB-13 |
| S-PATH-007 | Admin "klasörü aç" kardeş klasöre çıkmaz | `RecordsViewModel.cs:141-147` | Kök `$Root\Veri`; `AdminRecordRowViewModel` yolu `$Root\Veri2\P\O\S` (klasör var) ile `OpenFolder` | Hata mesajı; `FakeShell.OpenFolder` çağrılmaz | [UNIT] (ayraçsız önek → AÇIK BULGU) | Düşük | ÖB-16 |
| S-PATH-008 | Admin "son rapor" kök altında kalır | `RecordsViewModel.cs:149-157` | Kök dışı klasörlü satırla `OpenLatestReport` | Kök denetimi yapılır, kabuğa yol gitmez | [UNIT] (denetim yok → AÇIK BULGU) | Düşük | ÖB-16 |
| S-PATH-009 | Kilit silme junction ardına geçmez | `SharedConfig.cs:347-367` | Geçici kökte junction → `$Root2`; `$Root2\x.lock` (11 dk eski tarih); `StaleLocks` + `RemoveStaleLock` | Junction arkası listelenmez/silinmez; `$Root2\x.lock` yerinde kalır | [SCRIPT] (yalnızca `$Root`/`$Root2` §0 kökleri) | Orta | ÖB-13 |
| S-PATH-010 | Kapalı kusurda açılan dosya yalnızca kusur klasöründeki fotoğraftır | `InspectionViewModel.Photos.cs:147`, `EdgeBrowser.cs:24-33` | Kapalı kusur; `Markups["1_photo_01.jpg"].CleanedFile = "..\x.txt"`; `OpenPhotoEditor` | `FakeWindowService.OpenedFiles` boş ya da yalnız `defects\` altındaki `IsPhotoFileName` eşleşen dosya; hata mesajı | [UNIT] (şu an doğrulanmıyor → AÇIK BULGU) | Kritik | ÖB-01 |
| S-PATH-011 | Mutlak/UNC `CleanedFile` reddedilir | aynı | `CleanedFile = "\\testhost\paylasim\x.jpg"` ve `"C:\Windows\win.ini"` (yalnızca metin) | Açma isteği gitmez; uzantı resim değilse hiç açılmaz | [UNIT] → AÇIK BULGU | Kritik | ÖB-01 |
| S-PATH-012 | `Photos` adları kusur klasörüne bağlıdır | `InspectionViewModel.Photos.cs:21-36,53` | `defects\1.json` içinde `"photos": ["..\\x.jpg"]`; oturumu aç, kusuru seç | Geçersiz ad atlanır/uyarı; hiçbir `PhotoItemViewModel.ThumbnailPath` `defects\` dışına çıkmaz | [UNIT] → AÇIK BULGU | Yüksek | ÖB-02 |
| S-PATH-013 | UNC fotoğraf adı yol üretmez | aynı, `ThumbnailView.cs:32-41` | `"photos": ["\\\\testhost\\s\\x.jpg"]` | `ThumbnailPath` UNC değildir; ad reddedilir | [UNIT] (ThumbnailView için [REVIEW]) → AÇIK BULGU | Yüksek | ÖB-02 |
| S-PATH-014 | Rapor görsel kaynağı kusur klasöründe kalır | `ReportBuilder.cs:248-252` | `Photos = ["../x.jpg"]` ile `ReportBuilder.Build` | HTML'de `src="../defects/../x.jpg"` gibi dolaşım yok; geçersiz ad "fotoğraf yok" olarak gösterilir | [UNIT] → AÇIK BULGU | Yüksek | ÖB-02 |
| S-PATH-015 | İçe aktarılan dosya adı kaynaktan bağımsızdır | `PhotoImporter.cs:142-150` | Kaynak adı `..x.jpg` olan geçerli JPEG'i içe aktar | Hedef ad `{id}_photo_NN.jpg`, `IsPhotoFileName` true, `defects\` altında | [UNIT] | Düşük | — |
| S-PATH-016 | Rework bölgesi kusurun kendi fotoğrafıdır | `SessionStore.cs:148-153` | `PhotoOutcomes` anahtarı `"..\x.jpg"` | `InvalidOperationException`, diske yazılmaz | [UNIT] (mevcut: `AllRegionsConformClosesAndInvalidPhotoRejected`) | Düşük | — |

### 3.2 S-HTML — rapor HTML, SVG, PDF

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-HTML-001 | Kusur notu kaçılır | `ReportBuilder.cs:161,329` | Not `<b>x</b>` | Çıktıda `&lt;b&gt;x&lt;/b&gt;`, ham `<b>` yok | [UNIT] (mevcut: `ReportEscapesAndHidesVoid`) | Yüksek | — |
| S-HTML-002 | Tüm kullanıcı alanları kaçılır | `ReportBuilder.cs:28,38,53,64,86,144,158,185-205` | Parça/operasyon/seri `PD<b>1`… (SafePath dışı Session nesnesiyle), Inspector, tip, etkilenen parçalar, karar notu, referans, karar veren: hepsine `<b>x</b>` | Hiçbir alan ham `<b>` üretmez | [UNIT] | Yüksek | — |
| S-HTML-003 | Öznitelik bağlamı kırılmaz | `ReportBuilder.cs:249-252,262` | Fotoğraf adı `a"b.jpg` | `src`/`alt`/`href` içinde `&quot;`; öznitelik dışına taşma yok | [UNIT] | Orta | ÖB-02 |
| S-HTML-004 | SVG sayıları geçerli | `ReportBuilder.cs:255-292,318` | Şekil koordinatı `NaN`, `Infinity`, `1e308`, `ImageWidth = 0` (dosyadan) | Şekil atlanır ya da sayı sınırlanır; `NaN`/`∞` SVG'ye yazılmaz; rapor üretilir | [UNIT] | Düşük | — |
| S-HTML-005 | Dosyadan gelen işaret sayısı sınırlı | `PhotoMarkup.cs:65-73`, `ReportBuilder.cs:264` | `Shapes` 10.000 öğe (dosyaya doğrudan yazılmış) | Rapor en çok `MaxShapes` (30) çizer ya da uyarır; süre makul (< 2 sn) | [UNIT] | Düşük | — |
| S-HTML-006 | Geçersiz `CleanedFile` raporda kullanılmaz | `ReportBuilder.cs:248` | `CleanedFile = "..\x.jpg"` | Orijinal fotoğraf kullanılır | [UNIT] (regresyon) | Orta | ÖB-01 |
| S-HTML-007 | `<title>` kaçılır | `ReportBuilder.cs:28` | Seri `</title><b>x` | Kaçılmış metin | [UNIT] | Orta | — |
| S-HTML-008 | Ek ölçüm alan adı/değeri kaçılır | `ReportBuilder.cs:146-157,297` | Admin alan adı `<b>H</b>` (doğrulama dışı, dosyadan) | Kaçılmış | [UNIT] | Orta | — |
| S-HTML-009 | Bölge etiketi ve bağlantı cümleleri kaçılır | `ReportBuilder.cs:111-140` | `FoundAtPhoto` `<b>x</b>.jpg` | `E(region)` ile kaçılmış; ham etiket yok | [UNIT] | Orta | — |
| S-HTML-010 | Rapor etkin içerik içermez | `ReportBuilder.cs` (tümü) | Tüm alanlara S-HTML-002 girdileri; çıktıda ara | `<script`, ` on…=` öznitelikleri, `javascript:`, `http://`, `https://`, `<iframe`, `<object` yok | [UNIT] | Orta | ÖB-09 |
| S-HTML-011 | Raporda CSP var | `ReportBuilder.cs:28-30` | Çıktının `<head>`'inde CSP `meta` ara | `default-src 'none'; img-src file: data:; style-src 'unsafe-inline'` benzeri bir politika | [UNIT] (yok → AÇIK BULGU) | Orta | ÖB-09 |
| S-HTML-012 | Başsız Edge en az yetkiyle çalışır | `EdgePdfConverter.cs:31-39` | Argüman listesini oku | JS kapalı (`--blink-settings=scriptEnabled=false` ya da CSP), `--allow-file-access-from-files` yok, ağ kapalı; ayrı geçici profil siliniyor (`:70`) | [REVIEW] → AÇIK BULGU | Orta | ÖB-09 |
| S-HTML-013 | HTML yerelde üretilip dönüştürülür | `InspectionViewModel.Photos.cs:216-232` | Akışı oku | HTML önce `%TEMP%`'te (yerel) yazılır, PDF paylaşıma kopyalanır; paylaşımdaki HTML'e Edge doğrudan bakmaz | [REVIEW] → AÇIK BULGU | Orta | ÖB-09 |
| S-HTML-014 | Admin son raporda paylaşımdaki HTML'i açmaz | `RecordsViewModel.cs:149-157` | Yalnızca `Rapor_v1.html` olan kayıt; `OpenLatestReport` | PDF yoksa "PDF yok" uyarısı; `FakeShell.OpenFile` `.html` almaz | [UNIT] → AÇIK BULGU | Orta | ÖB-09 |
| S-HTML-015 | PDF'te kaçılmış metin düz yazıdır | `EdgePdfConverter.cs` | §0 kökünde not `<b>x</b>` içeren rapor HTML'i; makinedeki Edge ile `--headless --print-to-pdf` | PDF metninde `<b>x</b>` harfiyen görünür (kalın değil); ağ isteği yok | [SCRIPT] (isteğe bağlı) | Düşük | — |

### 3.3 S-CSV — dışa aktarım

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-CSV-001 | `=` ile başlayan hücre etkisiz | `CsvExport.cs:35-41` | `Cell("=1+1")` | `'=1+1` | [UNIT] (mevcut: `CsvEscapes`) | Orta | — |
| S-CSV-002 | Diğer formül önekleri | `CsvExport.cs:38` | `"+1"`, `"-1"`, `"@x"`, `"\t=1"`, `"\r=1"` | Hepsi `'` ile başlar | [UNIT] | Orta | — |
| S-CSV-003 | Baştaki boşluk / tam genişlik işaret | `CsvExport.cs:38` | `" =1+1"`, `"＝1+1"` (U+FF1D) | Etkisizleştirilir ya da Excel'de formül olmadığı belgelenir | [UNIT] + [REVIEW] | Düşük | — |
| S-CSV-004 | Ayraç/tırnak/satır sonu tutarlı | `CsvExport.cs:40` | `"a;b"`, `"a\"b"`, `"a\nb"` | Tırnaklı, iç tırnak ikilenmiş; her kayıt satırı 13 sütun | [UNIT] | Orta | — |
| S-CSV-005 | Virgül ayraçlı yerel ayarda kayma yok | `CsvExport.cs:11-14` | Hücre `"a,=1+1"`; dosya başında `sep=;` ya da virgüllü hücrenin tırnaklanması | Excel virgülü ayraç sayarsa bile `=` ile başlayan yeni hücre oluşmaz | [REVIEW] (Türkçe Windows ";" kullanır; en-US "," kullanır) | Düşük | — |
| S-CSV-006 | Tüm sütunlar `Cell`'den geçer | `CsvExport.cs:21-29,43` | Inspector `=x`, tip `=x`, etkilenen parçalar `=x` olan IndexRow | Hepsi `'` önekli | [UNIT] | Orta | — |
| S-CSV-007 | Dışa aktarım yalnızca seçilen yola yazar | `RecordsViewModel.cs:160-175` | `FakeFileSaver` → `$Root\out.csv` | Yalnızca o dosya; BOM var; hata mesajı yolu taşır, çökme yok | [UNIT] (mevcut: `RecordsExportCsvWritesBomAndFilteredRows`) | Düşük | — |

### 3.4 S-JSON — bozuk / büyük / iç içe / aralık dışı veri (yükleyici bazında)

Her yükleyici için ortak beklenti: **çökme yok**, kullanıcıya anlaşılır hata, **diğer kayıtlar okunmaya devam eder**, bozuk dosyanın üzerine sessizce yazılmaz.

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-JSON-001 | Yarım `session.json` dashboard'u düşürmez | `SessionStore.cs:47-51,309-317`, `DashboardViewModel.cs:139,211` | Kökte iki oturum; birinin `session.json`'u `{"partNumber":` | `Refresh` istisna sızdırmaz; hata durum mesajı; sağlam oturum listelenir | [UNIT] → AÇIK BULGU | Yüksek | ÖB-03 |
| S-JSON-002 | Bozuk `defects\N.json` muayene ekranını düşürmez | `InspectionViewModel.cs:797`, `SessionStore.cs:60-70` | `defects\2.json` = `{` ; oturumu aç | Hata mesajı; VM'den istisna çıkmaz | [UNIT] → AÇIK BULGU | Yüksek | ÖB-03 |
| S-JSON-003 | `null` koleksiyonlar | `Models.cs`, `InspectionViewModel.Photos.cs:22` | `"photos": null`, `"decisions": null`, `"markups": null` | Boş liste gibi davranır ya da kontrollü hata | [UNIT] → AÇIK BULGU (`NullReferenceException`) | Yüksek | ÖB-03 |
| S-JSON-004 | Boyut sınırı | `SessionStore.cs:13,311-312` | 5 MB + 1 bayt `session.json` | `InvalidDataException`, VM'de mesaj | [UNIT] | Orta | — |
| S-JSON-005 | Derin iç içe | `SessionStore.cs:315` | 200 seviye `[[[…]]]` içeren `notes` alanı | `JsonException` (MaxDepth 64) yakalanır | [UNIT] | Orta | ÖB-03 |
| S-JSON-006 | Enum aralık dışı sayı | `SessionStore.cs:19` (`JsonStringEnumConverter`, tamsayı kabul) | `"status": 999`, karar `"decision": 999`, `"source": -1` | Reddedilir (`AllowIntegerValues=false`) ya da "bilinmeyen" gösterilir; geçit/durum hesabı açık kusuru kapalı saymaz | [UNIT] (şu an sayı kabul ediliyor → durum belgelenir) | Orta | ÖB-03 |
| S-JSON-007 | Enum bilinmeyen metin | aynı | `"status": "Foo"` | `JsonException` yakalanır | [UNIT] | Orta | ÖB-03 |
| S-JSON-008 | Index yeniden üretimi tek bozuk kayıtla durmaz | `IndexBuilder.cs:31-79` | 3 oturumdan biri bozuk; `Rebuild` | 2 satır döner, bozuk olan raporlanır | [UNIT] → AÇIK BULGU | Yüksek | ÖB-03 |
| S-JSON-009 | Admin Kayıtlar bozuk kayıtla açılır | `RecordsViewModel.cs:108-112`, `AdminViewModelBase.cs:20` | S-JSON-008 kökü ile `Refresh` | Hata mesajı, istisna sızmaz | [UNIT] → AÇIK BULGU | Yüksek | ÖB-03 |
| S-JSON-010 | Kusur tipi şablonu | `SharedConfig.cs:74-87` (`defect_types.json`) | `[null]`, `[{"name":"x","fields":null}]`, 101 tip, `{}` | Varsayılanlara döner; istisna yok | [UNIT] (null eleman → `NullReferenceException` olasılığı) | Orta | ÖB-03 |
| S-JSON-011 | Diğer `_config` dosyaları | `SharedConfig.cs:138-149` (`measure_fields.json`), `:228-244` (`decision_texts.json`), `:283-293` (`inspector_identity.json`) | Bozuk JSON, `{"999":"x"}`, `null` | Varsayılan; istisna yok | [UNIT] | Orta | — |
| S-JSON-012 | `_index.json` okuması | `IndexBuilder.cs:81-87` | Bozuk `_index.json` + `Read` | Kontrollü hata ya da boş liste | [UNIT] | Düşük | ÖB-03 |
| S-JSON-013 | Dosya adı ile `Id` eşleşir | `SessionStore.cs:90-101,297-307` | `5.json` içinde `"id": 3`, ayrıca gerçek `3.json`; `UpdateDefect(folder, 5, d => d.Notes = "x")` | Uyuşmazlık hatası; `3.json` **değişmez** (önce/sonra SHA-256 aynı) | [UNIT] → AÇIK BULGU | Orta | ÖB-10 |
| S-JSON-014 | Yinelenen `Id` algılanır | `SessionStore.cs:60-70`, `InspectionViewModel.Photos.cs:140` | `1.json` ve `kopya.json` ikisi de `"id": 1`; `LoadDefects` | Uyuşmazlık raporlanır, ikinci dosya yok sayılır | [UNIT] → AÇIK BULGU | Orta | ÖB-10 |
| S-JSON-015 | Sonraki numara taşmaz | `SessionStore.cs:81,155` | `1.json` içinde `"id": 2147483647`; `AddDefect` | Kontrollü hata (`checked` / üst sınır); negatif numaralı dosya oluşmaz, var olan dosya değişmez | [UNIT] → AÇIK BULGU | Orta | ÖB-10 |
| S-JSON-016 | Sıfır/negatif `Id` reddedilir | aynı | `"id": 0`, `"id": -1` | Yüklemede red | [UNIT] → AÇIK BULGU | Düşük | ÖB-10 |
| S-JSON-017 | Kusur dışı `.json` yok sayılır | `SessionStore.cs:66` | `defects\notlar.json` = `{}` | Ad `^\d+\.json$` değilse okunmaz; `Id = 0` kusur listede görünmez | [UNIT] → AÇIK BULGU | Düşük | ÖB-10 |
| S-JSON-018 | Yerel kullanıcı dosyaları | `HiddenSuggestionSet.FromJson`, `UserSettingsAdapter.cs:72-80,100-120`, `CameraWindow.xaml.cs:651-665` | Bozuk JSON, `null`, 10 MB metin | Varsayılan; çökme yok (`FromJson` [UNIT], diğerleri [REVIEW]) | [UNIT] + [REVIEW] | Düşük | — |
| S-JSON-019 | Kurulum ayarı | `InstallSettings.cs:29-50` | Bozuk `MuayeneKayit.settings.json`; `"rootPath": 5` | Demo köke düşer, istisna yok (`InvalidOperationException` yakalanıyor) | [UNIT] | Düşük | — |
| S-JSON-020 | Dosyadan gelen işaretler | `InspectionViewModel.Photos.cs:24-39` | 10.000 şekil, `NaN` koordinat | Kart listesi < 1 sn; çökme yok | [UNIT] | Düşük | — |

### 3.5 S-PIN — admin PIN ve giriş

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-PIN-001 | PIN düz metin saklanmaz, tuz benzersiz | `SharedConfig.cs:309-324` | Test PIN `"482916"` iki kez ayrı köklere yaz | `admin.json`'da PIN metni yok; iki tuz farklı | [UNIT] (mevcut: `AdminPinRules` + ek) | Yüksek | ÖB-06 |
| S-PIN-002 | KDF parametreleri | `SharedConfig.cs:309-342` | Kodu oku | PBKDF2-SHA256, ≥ 200.000 tur, 32 bayt; öneri: daha uzun PIN/parola veya `_config` dışında saklama | [REVIEW] | Yüksek | ÖB-06 |
| S-PIN-003 | Çevrimdışı deneme maliyeti ölçülür | `SharedConfig.cs:326-339` | Test kökünde üretilen `admin.json` ile tek `VerifyPin` süresi (en çok 10 ölçüm, ≤ 60 sn, §0.6) | Rapor: tek deneme ms, 10⁶ uzay için tahmini süre; risk notu | [UNIT] (ölçüm, her zaman PASS + değer) | Yüksek | ÖB-06 |
| S-PIN-004 | Kilitlenme yeniden açılışta sürer | `LoginViewModel.cs:12-18,79-91` | 5 hatalı giriş; **yeni** `LoginViewModel` (aynı kök) ile doğru PIN | Bekleme süresi devam eder (kalıcı durum) | [UNIT] → AÇIK BULGU | Yüksek | ÖB-06 |
| S-PIN-005 | Tek tek hatalı denemeler denetlenir | `LoginViewModel.cs:79-94` | 1 hatalı giriş, `_audit.log` son satır | `admin.login.fail` benzeri satır | [UNIT] (yalnız kilitlenme yazılıyor → AÇIK BULGU) | Orta | ÖB-06 |
| S-PIN-006 | PIN özeti okuyuculara açık değil | `KURULUM.md:102-105` | ACL önerisini oku | Muayeneci/okuyucu `admin.json`'u okuyamaz (ayrı alt klasör ya da yalnız admin ACL'i) | [REVIEW] → AÇIK BULGU | Yüksek | ÖB-06 |
| S-PIN-007 | `admin.json` yoksa ilk kurulum herkese açılmaz | `LoginViewModel.cs:41,69-78` | Test kökünde `admin.json` yok; `LoginViewModel` | Durum belgelenir: `IsFirstRun = true` → ilk kurulum ekranı; güvenlik yalnız NTFS'e bağlı (S-NTFS ile birlikte değerlendirilir) | [UNIT] (davranış belgeleme) + [REVIEW] | Yüksek | ÖB-07 |
| S-PIN-008 | Tur sayısına üst sınır | `SharedConfig.cs:332-334` | `Iterations = 2147483647` olan test `admin.json`; `VerifyPin` arka plan iş parçacığında, 2 sn bekle | ≤ 2 sn'de `false`/hata döner | [UNIT] (zaman aşımı → AÇIK BULGU; iş parçacığı `IsBackground = true`) | Orta | ÖB-08 |
| S-PIN-009 | Bozuk Base64 | `SharedConfig.cs:336-337`, `LoginViewModel.cs:97-98` | `"hash": "***"` | Login hata mesajı; istisna sızmaz | [UNIT] → AÇIK BULGU | Orta | ÖB-08 |
| S-PIN-010 | Bozuk `admin.json` | aynı | `{` | Login hata mesajı; istisna sızmaz | [UNIT] → AÇIK BULGU | Orta | ÖB-08 |
| S-PIN-011 | PIN biçimi | `SharedConfig.cs:329`, `LoginViewModel.cs` | 13 haneli, `"12a456"`, boş | Reddedilir | [UNIT] (mevcut: `ShortPinShowsRuleError` + ek) | Düşük | — |
| S-PIN-012 | Sabit zamanlı karşılaştırma | `SharedConfig.cs:338` | Kodu oku | `CryptographicOperations.FixedTimeEquals` | [REVIEW] | Düşük | — |
| S-PIN-013 | Boşta ve kapanışta kilit | `LoginViewModel.cs`, `AdminShellViewModel.cs` | 10 dk girdi yok | Oturum kilitlenir | [UNIT] (mevcut: `IdleLockAfterTenMinutesOfNoInput`) | Düşük | — |

### 3.6 S-AUD — denetim kaydı

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-AUD-001 | Ortadaki satırın değişmesi yakalanır | `AuditLog.cs:42-76` | 3 satır; 2.'nin `details`'ını değiştir | `Verify` → `(false, 2)` | [UNIT] (mevcut: `AuditChainDetectsTamper`) | Yüksek | ÖB-05 |
| S-AUD-002 | Sondan satır silinmesi yakalanır | aynı | 3 satır; sonuncuyu sil | Bütünlük hatası (dış çapa: satır sayısı/son özet başka yerde) | [UNIT] → AÇIK BULGU | Yüksek | ÖB-05 |
| S-AUD-003 | Zincir anahtarlı | `AuditLog.cs:128-134` | Testin kendi zincirini aynı genel algoritmayla baştan hesaplanmış satırlarla değiştir | `Verify` bunu ayırt eder (HMAC / imza) | [UNIT] (anahtarsız → AÇIK BULGU) | Yüksek | ÖB-05 |
| S-AUD-004 | Kayıt içeriği denetime bağlı | `SessionStore.cs:297-304`, `AuditLog.cs:27-39` | Kusur ekle (denetimli); sonra `defects\1.json` notunu doğrudan `x` yap | Doğrulama/rapor uyarı verir (içerik özeti denetimde) | [UNIT] → AÇIK BULGU | Yüksek | ÖB-05 |
| S-AUD-005 | Denetim satırı kaydın özetini taşır | `InspectionViewModel*.cs` `_audit.Append` çağrıları | Kodu oku | `details` içinde kayıt sonrası SHA-256 | [REVIEW] → AÇIK BULGU | Orta | ÖB-05 |
| S-AUD-006 | Bozuk son satır işlemi bozmaz | `AuditLog.cs:110-126` | `_audit.log` sonuna `{bozuk` ekle; VM ile fotoğraf ekle | İstisna sızmaz; kayıt ve denetim tutarlı (ya ikisi ya hiçbiri) ya da açık uyarı | [UNIT] → AÇIK BULGU | Yüksek | ÖB-04 |
| S-AUD-007 | Satır enjeksiyonu yok | `AuditLog.cs:37-38` | `actor = "a\n{\"ts\":\"x\"}"`, `details = "<b>x</b>\r\n"` | Tek satır eklenir; `Verify` sağlam; satır sayısı +1 | [UNIT] | Orta | — |
| S-AUD-008 | Dosyanın silinmesi yakalanır | `AuditLog.cs:44-46` | Kayıtlar varken `_audit.log` sil, `Verify` | "kayıt yok ama veri var" uyarısı | [UNIT] (şu an `(true, null)` → AÇIK BULGU) | Orta | ÖB-05 |
| S-AUD-009 | Eşzamanlı ekleme zinciri bozmaz | `AuditLog.cs:31` | Paralel 20 ekleme | `Verify` sağlam | [UNIT] (mevcut: `ParallelAuditAppendsKeepChain`) | Düşük | — |
| S-AUD-010 | Büyük günlükte ekleme süresi | `AuditLog.cs:110-120` | §0 kökünde 50 MB günlük üret, 10 ekleme süresi | Ölçüm raporu; her eklemede tüm dosya okunduğu not edilir | [SCRIPT] | Düşük | — |
| S-AUD-011 | Hassas işlemler denetlenir | `LoginViewModel.cs:77,86,94`, `SettingsViewModel.cs`, `TypesViewModel.cs`, `MaintenanceViewModel.cs:54-70`, VM karar/void/undo/rapor | Her işlem için sahte servislerle çalıştır, son satırın `action`'ını kontrol et | PIN değiştirme, tip/açıklama kaydı, kilit silme, kök değişimi, karar, void, undo, rapor, fotoğraf: hepsi satır yazar | [UNIT] + [REVIEW] | Orta | — |

### 3.7 S-LOCK — kilitler

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-LOCK-001 | Kilit alınır ve bırakılır | `FileLock.cs:59-95` | `Acquire` + `Dispose` | Dosya silinir | [UNIT] (mevcut: eşzamanlılık testleri) | Düşük | — |
| S-LOCK-002 | Eski kilit kontrollü hata verir | `FileLock.cs:83-85` | `session.lock` oluştur, `LastWriteTimeUtc = now-11dk`; VM ile kusur ekle | Anlaşılır mesaj (admin'e yönlendirir); veri değişmez; istisna VM dışına sızmaz | [UNIT] | Orta | ÖB-12 |
| S-LOCK-003 | İleri tarihli kilit sonsuza kadar engellemez | `FileLock.cs:97-106` | `LastWriteTimeUtc = now+1 gün`; `FileLock.Acquire(path, 1 sn)` | Gelecek tarihli kilit "şüpheli" sayılır, açıklayıcı hata/admin listesine düşer | [UNIT] (`TimeoutException` → AÇIK BULGU) | Orta | ÖB-12 |
| S-LOCK-004 | Admin ileri tarihli kilidi görür | `SharedConfig.cs:347-351` | Aynı dosya; `StaleLocks` | Listede görünür | [UNIT] → AÇIK BULGU | Orta | ÖB-12 |
| S-LOCK-005 | Canlı kilit silinmez | `SharedConfig.cs:357-367` | `Acquire` tutulurken tarihi 11 dk geri al; `RemoveStaleLock` | Silme başarısız (açık tutamaç), kontrollü mesaj; dosya kalır | [UNIT] | Düşük | — |
| S-LOCK-006 | Kök dışı / uzantısız silinmez | `SharedConfig.cs:359-362` | `$Root\..\x.lock`, `$Root\x.txt`, kardeş `$Root2\x.lock` | `ArgumentException` | [UNIT] (mevcut: `StaleLockRules` + kardeş ek) | Düşük | — |
| S-LOCK-007 | Kök kilitleri tüm işi durdurmaz | `AuditLog.cs:31`, `IndexBuilder.cs:71` | Kökte `_audit.lock` (yeni tarih) bırak; fotoğraf ekle / dashboard yenile | Belirli süre sonra anlaşılır mesaj, uygulama çalışır kalır; durum raporlanır | [UNIT] | Orta | ÖB-12 |
| S-LOCK-008 | Kilit yolu klasörse kontrollü hata | `FileLock.cs:69-91` | `session.lock` adında klasör; `Acquire` | Kontrollü hata (şu an `UnauthorizedAccessException` döngüden kaçabilir) | [UNIT] | Düşük | — |
| S-LOCK-009 | Kilit sahibi izlenebilir | `FileLock.cs:71-78` | Kodu oku | Kilit içinde kullanıcı/makine/PID/zaman (admin kararı için) | [REVIEW] → AÇIK BULGU | Düşük | ÖB-12 |

### 3.8 S-PHOTO — fotoğraf içe aktarma ve düzenleme

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-PHOTO-001 | Uzantı ve imza denetimi | `PhotoImporter.cs:132-140,174-185` | `.txt`; içeriği metin olan `.jpg`; `.png` uzantılı JPEG | `InvalidDataException`; kopya yok | [UNIT] (mevcut: `PhotoImportRules` + ek) | Orta | — |
| S-PHOTO-002 | Dosya boyutu sınırı | `PhotoImporter.cs:136-137` | §0 kökünde 25 MB + 1 baytlık JPEG başlıklı seyrek dosya (`fsutil file createnew` yerine `FileStream.SetLength`) | Red | [UNIT] | Orta | — |
| S-PHOTO-003 | Piksel boyutu sınırı | `PhotoEditorWindow.xaml.cs:125-137`, `PhotoImporter.cs:139` | Kodu oku; içe aktarmada başlıktaki genişlik×yükseklik okunuyor mu? (PNG IHDR / JPEG SOF) | Ör. ≤ 50 MP üstü reddedilir; düzenleyici `BitmapTransform` ile ölçekler | [REVIEW] → AÇIK BULGU | Orta | ÖB-11 |
| S-PHOTO-004 | Arka plan aracı boyut taşması | `BackgroundRemover.cs:16-19` | `ComputeMask(new byte[16], 50000, 50000, rect)` | `ArgumentException` ("geçersiz"); `IndexOutOfRange`/`OverflowException` değil | [UNIT] (taşma → AÇIK BULGU olasılığı) | Orta | ÖB-11 |
| S-PHOTO-005 | EXIF temizlenir ya da politika belgelenir | `PhotoImporter.cs:149` | Test içinde APP1 `Exif\0\0` segmentli küçük JPEG üret, içe aktar, hedefte `"Exif"` ara | EXIF yok (ya da belgelenmiş kabul) | [UNIT] → AÇIK BULGU | Düşük | ÖB-14 |
| S-PHOTO-006 | Rapor orijinal EXIF'li dosyayı gömmez | `ReportBuilder.cs:248-252` | Kodu oku | Temizlenmiş/yeniden kodlanmış kopya kullanılır | [REVIEW] → AÇIK BULGU | Düşük | ÖB-14 |
| S-PHOTO-007 | Temiz kopya adı ve içeriği doğrulanır | `PhotoImporter.cs:157-172` | `WriteCleaned(folder, "..\x.jpg", jpeg)`, JPEG olmayan bayt | `ArgumentException` / `InvalidDataException` | [UNIT] (mevcut: `CleanedCopyKeepsOriginal` + ek) | Orta | — |
| S-PHOTO-008 | Numara çakışmasında üzerine yazılmaz | `PhotoImporter.cs:145-149` | `1_photo_01.jpg` var, `1_photo_02.jpg` silinmiş durum; iki içe aktarma | `overwrite:false` korunur; var olan dosya değişmez, hata anlaşılır | [UNIT] | Düşük | — |
| S-PHOTO-009 | Küçük resim sınırlı çözülür | `ThumbnailView.cs:37` | Kodu oku | `DecodePixelWidth = 360` (PASS beklenir) | [REVIEW] | Düşük | — |
| S-PHOTO-010 | Düzenleyici hata filtresi bellek hatasını kapsar | `PhotoEditorWindow.xaml.cs:117-121`, `:82` (`_ = LoadAsync()`) | Kodu oku | Büyük görüntüde kontrollü mesaj; gözlenmeyen görev istisnası yok | [REVIEW] → AÇIK BULGU | Düşük | ÖB-11 |
| S-PHOTO-011 | Kamera ara dosyası temizlenir | `CameraWindow.xaml.cs:560-610` | Kodu oku | `%TEMP%` ara dosyası kayıttan sonra silinir | [REVIEW] | Düşük | — |

### 3.9 S-LAUNCH — başlatıcı, sürüm seçimi, harici süreçler

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-LAUNCH-001 | `surum.txt` dolaşım içeremez | `ProgramLayout.cs:24-26` | Geçici program kökünde `surum.txt` = `..\x`, `C:\x`, `\\testhost\s`, boş | `FileNotFoundException` (mesajlı); kök dışı exe seçilmez | [UNIT] (mevcut: `ProgramLayoutResolvesVersionAndArch` + ek) | Orta | — |
| S-LAUNCH-002 | `surum.txt` = `.` | `ProgramLayout.cs:25,30` | `.` | Reddedilir (şu an `Program\Inspector-x64` aranır) | [UNIT] | Düşük | — |
| S-LAUNCH-003 | Sürüm adı biçimi | `ProgramLayout.cs:25` | `CON`, `1.0.2 ` , `1.0.2\r\nx` | Yalnızca `^\d+(\.\d+){1,3}$` kabul | [UNIT] | Düşük | — |
| S-LAUNCH-004 | Başlatıcı kabuk kullanmaz | `Launcher/Program.cs:24-31` | Kodu oku | `UseShellExecute=false`, mutlak yol, `ArgumentList` (PASS beklenir) | [REVIEW] | Düşük | — |
| S-LAUNCH-005 | Ayar dosyası arama derinliği | `InstallSettings.cs:13-25` | `$Root\a\b\c\d\exe` düzeni: ayarı 3. ve 4. üst klasöre koy, `Find` | 3. seviye bulunur, 4. bulunmaz; [REVIEW]: dağıtımda 1-3. üst klasörlerin kullanıcıya salt okunur olduğu belgelenmeli | [UNIT] + [REVIEW] | Orta | — |
| S-LAUNCH-006 | `rootPath` genişletme | `InstallSettings.cs:36-40` | `"%TEMP%\\MK-SEC-…"`, göreli `"veri"`, `"\\\\testhost\\s"` (metin) | Genişletilmiş tam yol; göreli yol reddedilir ya da belgelenir; UNC'ye erişilmez (yalnız metin) | [UNIT] | Düşük | — |
| S-LAUNCH-007 | Edge sabit konumdan | `EdgeBrowser.cs:12-21` | Kodu oku | Yalnız Program Files; PATH yok (PASS beklenir) | [REVIEW] | Düşük | — |
| S-LAUNCH-008 | Kabukla açma resim/rapor ile sınırlı | `EdgeBrowser.cs:24-33` | Kodu oku; `IWindowService.OpenFile` uygulaması | `UseShellExecute=true` yalnız izinli uzantılar (`.jpg/.jpeg/.png/.pdf/.html`) ve kök altı yol için | [REVIEW] → AÇIK BULGU | Kritik | ÖB-01 |
| S-LAUNCH-009 | Program klasörü yazılamaz | `OKU-BENI.txt:19`, `KURULUM.md:69` | Belgeleri oku | Kullanıcı için yalnız okuma (DLL/exe değişimi engelli) — tutarlı (PASS beklenir) | [REVIEW] | Yüksek | — |
| S-LAUNCH-010 | Yönetici yetkisi istenmez | `Inspector/app.manifest` | Oku | `asInvoker` | [REVIEW] | Düşük | — |

### 3.10 S-SEC — depodaki gizli bilgiler ve bağımlılıklar

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-SEC-001 | Çalışma ağacında gizli bilgi yok | tüm depo | `git grep -n -i -E "password|api[_-]?key|secret|BEGIN .*PRIVATE|connectionstring" -- ':!*package-lock.json'` | Gerçek değer yok (statik incelemede yalnız `PasswordBox` ve `token` değişken adları çıktı) | [SCRIPT] | Orta | — |
| S-SEC-002 | Geçmişte gizli bilgi yok | git geçmişi | `git log -p --all` çıktısında aynı desenler + silinen `src/MuayeneKayit.Admin/MuayeneKayit.settings.json` içeriği (`git log --all -p -- '*settings.json'`) | Yalnız demo yolu; değerler raporda maskeli | [SCRIPT] | Orta | — |
| S-SEC-003 | İç sunucu adı sızmaz | `MuayeneKayit.settings.json`, belgeler | `git log -p --all | Select-String '\\\\[A-Za-z0-9-]+\\'` | Yalnız örnek `\\sunucu\…`; gerçek host adı yok (bulunursa maskele) | [SCRIPT] | Düşük | — |
| S-SEC-004 | Commit üst verisi | git | `git log --format='%an|%ae' | sort -u` | Kişisel e-posta varlığı **not** edilir (maskeli); geçmiş yeniden yazılmaz | [SCRIPT] | Düşük | ÖB-15 |
| S-SEC-005 | Paket sürümleri gözden geçirilir | `*.csproj`: `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Extensions.DependencyInjection 9.0.0`, `Microsoft.WindowsAppSDK 1.8.260921001`, `Microsoft.Windows.SDK.BuildTools 10.0.26100.9169`; `docs/kurulum-video/package-lock.json` (yalnız geliştirme) | `dotnet list package --vulnerable` **ağ gerektirir → çalıştırılmaz** (§0.3/§0.4). Sürümler listelenip "ağ izni olduğunda doğrulanacak" diye işaretlenir | Bilinen açığı olan sürüm yok (doğrulanamadı → [REVIEW] notu) | [REVIEW] | Orta | — |
| S-SEC-006 | Paket kaynağı güvenli | `nuget.config`, `offline-paketler.ps1` | Oku | Yalnız `https` ya da yerel klasör; `http` kaynak yok | [REVIEW] | Düşük | — |
| S-SEC-007 | `.gitignore` hassas dosyaları dışlar | `.gitignore` | Oku | `*.pfx`, `*.snk`, `admin.json`, `_config/`, `_audit.log` dışlanmış (şu an yok → öneri) | [REVIEW] | Düşük | — |
| S-SEC-008 | Derleme betikleri | `build-release.ps1` | Oku | İndirme, sabit kimlik bilgisi, imza anahtarı yok | [REVIEW] | Düşük | — |

### 3.11 S-PRIV — kişisel veri

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-PRIV-001 | Kişisel veri envanteri | `WindowsUserAdapter.cs`, `Models.cs` (Session/Disposition), `LoginViewModel.cs:86,94` | Oku; nerede ad, sicil no, makine adı yazılıyor listele | Envanter tablosu; amaç dışı alan yok | [REVIEW] | Orta | — |
| S-PRIV-002 | Denetim günlüğünde kişisel veri | `AuditLog.cs`, `LoginViewModel.cs:86,94` (`MachineName`) | Oku | Okuyucu grubunun erişimi ve saklama süresi belgelenmiş (KVKK) | [REVIEW] | Düşük | — |
| S-PRIV-003 | Fotoğraf EXIF'i | bkz. S-PHOTO-005/006 | — | GPS/cihaz bilgisi paylaşıma ve rapora gitmez | [UNIT] + [REVIEW] | Düşük | ÖB-14 |
| S-PRIV-004 | Yerel dosyalar | `%LOCALAPPDATA%\MuayeneKayit\` (`UserSettingsAdapter.cs`, `CameraWindow.xaml.cs:651-678`) | Oku | Yalnız tercih/cihaz kimliği; kişisel veri yok | [REVIEW] | Düşük | — |
| S-PRIV-005 | Geçici dosyalar silinir | `EdgePdfConverter.cs:67-71`, `ReportFiles.DeleteHtml` | Oku; VM testinde PDF başarılıyken HTML'in silindiğini doğrula | Edge profili ve ara HTML kalmaz | [UNIT] (mevcut: `VmReportWritesHtmlAndOpensPdf`) + [REVIEW] | Düşük | — |
| S-PRIV-006 | Commit e-postası | git | bkz. S-SEC-004 | Not | [SCRIPT] | Düşük | ÖB-15 |

### 3.12 S-NTFS — izin belgeleri tutarlılığı

| ID | Kontrol edilen özellik | Kod yeri | Adımlar | Beklenen güvenli davranış | Yöntem | Önem | Ön bulgu |
|---|---|---|---|---|---|---|---|
| S-NTFS-001 | Dağıtım notu `_config` kısıtını söyler | `dagitim/OKU-BENI.txt:19` vs `KURULUM.md:89,102-105` | İki belgeyi karşılaştır | OKU-BENI de "`_config`'e muayeneci yalnız okur, yalnız admin yazar" der ve KURULUM §3'e yönlendirir | [REVIEW] → AÇIK BULGU | Yüksek | ÖB-07 |
| S-NTFS-002 | PIN sıfırlama yolu güvenli anlatılır | `KURULUM.md:88-89` | Oku | Yalnız admin grubunun silebileceği ve işlemin denetlenmesi gerektiği açıkça yazılı | [REVIEW] | Yüksek | ÖB-07 |
| S-NTFS-003 | Yazma izni yoksa program kontrollü davranır | `SharedConfig.cs:119`, `AdminViewModelBase.cs:20` | §0 kökünde `_config` için `icacls $Root\_config /deny "$env:USERNAME:(W)"`; tip kaydet (`SharedConfig.SaveDefectTypes`) | "Erişim reddedildi" mesajı, çökme yok; testten sonra `icacls /remove:d` ve kök silinir | [SCRIPT] + [UNIT] | Orta | ÖB-07 |
| S-NTFS-004 | İkinci izin düzeni denetim kaydını korur | `KURULUM.md:111-118` | Oku | Belgede muayenecinin `_audit.log`'u silebildiği (M) ve bunun yalnız tespit edildiği (engellenmediği) açıkça yazılı; mümkünse yalnız ekleme (AD) önerisi | [REVIEW] | Orta | ÖB-05 |
| S-NTFS-005 | Belgedeki güvence cümleleri doğru | `KURULUM.md:123-124` | Oku | "Denetim zinciri silindiğini gösterir" cümlesi ÖB-05 (sondan silme, tamamen silme) ile uyumlu hale getirilmeli | [REVIEW] → AÇIK BULGU | Orta | ÖB-05 |
| S-NTFS-006 | Ayar dosyasının yazılma yeri | `InstallSettings.cs:52-60`, `OKU-BENI.txt:11,19`, `KURULUM.md:69` | Oku | Admin'in kök yolunu program klasörüne yazması için gereken yetki belgelerde yazılı (program klasörü kullanıcıya salt okunur) | [REVIEW] | Düşük | — |

---

## 4. Uygulama sırası (yürütücü için)

Yürütücü daha ucuz bir modeldir; aşağıdaki sırayı **adım adım** izler, §0 kurallarını her adımda uygular, `src/`'a dokunmaz.

1. **Hazırlık (10 dk).**
   - `git -C <repo> rev-parse --short HEAD` → `17cf3bf` değilse §2'deki satır numaralarını `grep` ile yeniden bul.
   - `dotnet build tests/MuayeneKayit.Tests` ve `dotnet run --project tests/MuayeneKayit.Tests` → mevcut testlerin geçtiğini kaydet (temel çizgi).
2. **REVIEW turu (kod açmadan önce rapor iskeleti).** Şu ID'leri okuyarak doldur; her biri için "PASS / AÇIK BULGU + dosya:satır + tek cümle gerekçe":
   S-HTML-012, 013 · S-PIN-002, 006, 012 · S-AUD-005 · S-LOCK-009 · S-PHOTO-003, 006, 009, 010, 011 · S-LAUNCH-004, 007, 008, 009, 010 · S-SEC-005..008 · S-PRIV-001, 002, 004 · S-NTFS-001, 002, 004, 005, 006 · S-CSV-005.
3. **UNIT testleri — önce regresyon, sonra açık bulgular.** Yeni dosya: `tests/MuayeneKayit.Tests/SecurityTests.cs` (`public static class SecurityTests`). Yazım sırası (önem sırasına göre):
   1. Kritik/Yüksek: S-PATH-010, 011, 012, 013, 014 · S-JSON-001, 002, 003, 008, 009 · S-AUD-002, 003, 004, 006 · S-PIN-004, 007.
   2. Orta: S-PATH-002, 003, 006 · S-HTML-002, 003, 007..011, 014 · S-CSV-002, 004, 006 · S-JSON-004..007, 010, 011, 013..015 · S-PIN-005, 008..010 · S-AUD-007, 008, 011 · S-LOCK-002..004, 007 · S-PHOTO-001, 002, 004, 007 · S-LAUNCH-001, 005 · S-NTFS-003 (UNIT kısmı).
   3. Düşük: kalan [UNIT] satırları.
   - Her test **testin kendi** `TempRoot()`'unu kullanır (`%TEMP%` altında GUID'li; `finally` içinde silinir). Gerçek kök, `MuayeneKayit.settings.json` ya da ağ yolu **okunmaz**.
   - Geçen davranışı doğrulayan test → normal listeye; bugün başarısız olması beklenen (bulgu belgeleyen) test → `openFindings` listesine (§5).
   - Zaman alabilecek testler (S-PIN-003, S-PIN-008, S-LOCK-003, S-AUD-010) arka plan iş parçacığında, sabit üst süreyle.
4. **SCRIPT testleri.** Tek bir `docs/test/guvenlik-script.ps1` **yazılmaz** (kaynak değişikliği sayılmasın diye); komutlar yürütücünün oturumunda çalıştırılır ve rapora yapıştırılır: S-PATH-006 (junction kısmı), S-PATH-009, S-HTML-015 (isteğe bağlı), S-AUD-010, S-NTFS-003, S-SEC-001..004. Her script `Assert-SafeRoot` ile başlar; junction'lar önce `cmd /c rmdir` ile kaldırılır; git komutları yalnız okur.
5. **Çalıştır ve raporla.** `dotnet run --project tests/MuayeneKayit.Tests` → özetteki "AÇIK BULGU" satırlarını §4.1 tablosuna aktar. Build kırmızıya dönmemeli (çıkış kodu yalnız normal listedeki hatalarla 1 olur).
6. **Temizlik.** `%TEMP%\MK-SEC-*` ve `MuayeneKayit-test-*` kökleri silindi mi kontrol et (önce `Assert-SafeRoot`). `git status` yalnız `tests/MuayeneKayit.Tests/SecurityTests.cs` ve `Program.cs` değişikliğini göstermeli.

### 4.1 Rapor biçimi

Rapor `docs/test/GUVENLIK-TEST-SONUC.md` olarak yazılır (yalnız istenirse; aksi halde sohbet yanıtı). Üstte özet: toplam / PASS / AÇIK BULGU / ATLANDI sayıları, commit kimliği, tarih. Sonra tek tablo:

| ID | Sonuç | Ön bulgu | Kanıt (dosya:satır) | Gözlem (tek cümle) | Önerilen düzeltme |
|---|---|---|---|---|---|
| S-PATH-010 | AÇIK BULGU | ÖB-01 | `InspectionViewModel.Photos.cs:147` | `CleanedFile = "..\x.txt"` ile `OpenFile` kusur klasörü dışı yol aldı | `CleanedFile`/`photo` için `PhotoMarkupValidator.IsPhotoFileName` + `defects\` altında olma denetimi; `OpenFile`'da uzantı izin listesi |
| S-CSV-001 | PASS | — | `CsvExport.cs:38` | `'=1+1` üretildi | — |
| S-PATH-006 | ATLANDI | ÖB-13 | — | `mklink /J` başarısız (yetki) | — |

Kurallar: Sonuç yalnız `PASS`, `AÇIK BULGU`, `ATLANDI — <neden>`. Maskeleme (§0.9) uygulanır. Önerilen düzeltme tek cümle; kod yaması yazılmaz. Sonda "Ön bulgu durumu" listesi: her ÖB için *doğrulandı / doğrulanamadı / yanlış alarm*.

---

## 5. Birim testi düzeni (`tests/MuayeneKayit.Tests`)

- Proje bağımlılıksız özel bir koşucudur; xUnit/NUnit **yok**, paket eklenmez. Testler `Program.cs`'teki `tests` listesine `("Ad", Metot)` olarak kaydedilir; `Expect(bool, string)` başarısızlıkta istisna atar.
- Erişim: proje yalnız **Core** ve **Presentation**'a başvurur (`MuayeneKayit.Tests.csproj`). `Inspector` (WinUI: `PhotoEditorWindow`, `EdgePdfConverter`, `ThumbnailView`, `CameraWindow`) ve `Launcher` test edilemez → [REVIEW]. `SessionStore.ReadJson` `internal`'dır; `LoadSession`/`LoadDefects` üzerinden test edilir. `InternalsVisibleTo` eklenmez (kaynak değişikliği).
- Sahte servisler hazır: `FakeWindowService.OpenedFiles`, `FakeFilePicker`, `FakePdfConverter`, `FakeConfirm`, `FakeClock`, `FakeFileSaver`, `FakeAdminSettings` (`FakeServices.cs`, `FakeAdminServices.cs`). UNC/mutlak yollar yalnız bu sahte servislerde **metin** olarak yakalanır.
- **Açık bulgu listesi (build'i kırmadan).** `Program.cs`'e ikinci bir liste ve döngü eklenir; bu listedeki test başarısız olursa "AÇIK BULGU" yazılır, çıkış kodunu **etkilemez**; geçerse "DÜZELDİ — normal listeye taşıyın" yazılır:

```csharp
// Program.cs — tests listesinden sonra
var openFindings = new List<(string Id, string Name, Action Body)>
{
    ("ÖB-01", "S-PATH-010 kapalı kusurda CleanedFile kusur klasörü dışına çıkmaz", SecurityTests.ClosedDefectCleanedFileStaysInDefects),
    ("ÖB-10", "S-JSON-013 dosya adı/Id uyuşmazlığı başka kaydı ezmez", SecurityTests.IdMismatchDoesNotOverwrite),
    // ...
};

// mevcut foreach döngüsünden sonra, "Tümü geçti" satırından önce
int open = 0;
foreach (var (id, name, body) in openFindings)
{
    try { body(); Console.WriteLine($"  DÜZELDİ  [{id}] {name}  (normal listeye taşıyın)"); }
    catch (Exception ex) { open++; Console.WriteLine($"  AÇIK BULGU  [{id}] {name}\n        {ex.Message}"); }
}
Console.WriteLine($"Açık bulgu: {open}/{openFindings.Count}");
// return failed == 0 ? 0 : 1;   ← değişmez; açık bulgular çıkış kodunu etkilemez
```

- Her açık bulgu testinin `Expect` mesajı rapora doğrudan girecek biçimde yazılır: **beklenen güvenli davranış + gözlenen + dosya:satır** (ör. `"Beklenen: 3.json değişmez. Gözlenen: SHA-256 farklı. SessionStore.cs:302"`).
- Süre sınırı gereken testler için yardımcı (`SecurityTests` içinde): `static bool CompletesWithin(Action a, TimeSpan t) { var th = new Thread(() => { try { a(); } catch { } }) { IsBackground = true }; th.Start(); return th.Join(t); }`.
- Geçici kök: mevcut `TempRoot()` deseni (`%TEMP%\MuayeneKayit-test-<guid>`) kullanılır, `finally { Directory.Delete(root, true); }`. Junction oluşturan testte silmeden önce `Directory.Delete(junction)` (recursive **değil**) çağrılır.
- Test verileri kod içinde üretilir (küçük JPEG/PNG baytları, JSON metinleri); dış dosya ya da indirme yok. Test PIN'i sabit `"482916"`.
