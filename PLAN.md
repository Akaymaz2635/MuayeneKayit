# MuayeneKayit — Program Planı

Yerel ağ (LAN) üzerinde çalışan, ortak klasöre yazıp okuyan, dış bağlantısı olmayan muayene ve hata kayıt programı.
Veritabanı, bulut, telemetri ve otomatik güncelleme yoktur.

## 1. Uygulamalar ve mimari

| Proje | Ne | Kim |
|---|---|---|
| `src/MuayeneKayit.Core` | Alan modeli ve kurallar (durum makinesi, karar geçmişi, geçit), depolama, index, denetim, rapor, arka plan kaldırma, admin ayarları. Portlar: `ISessionStore`, `IAuditTrail`, `IPdfConverter`. Yalnızca .NET BCL. | Hepsi |
| `src/MuayeneKayit.Presentation` | Muayene ekranının ViewModel'leri (MVVM, `CommunityToolkit.Mvvm`): sıradaki adım, sekme/vurgu kuralları, karar ve rework akışları, fotoğraflar, rapor, dashboard. Pencere ve dosya seçimi portları: `IDialogService`, `IWindowService`, `IFilePicker`. WinUI'ye bağlı değil, test edilir. | Muayene exe'si |
| `src/MuayeneKayit.Inspector` | Muayene exe'si (WinUI 3): görünüm (XAML + `x:Bind`), portların WinUI uygulamaları (diyaloglar, kamera, fotoğraf düzenleyici, Edge PDF), DI kurulumu | Muayeneci |
| `src/MuayeneKayit.Admin` | Admin exe'si (WinUI 3): PIN girişi, kusur tipi şablonları, karar açıklamaları, tüm kayıtlar, CSV, bakım, ayarlar | Admin |
| `tests/MuayeneKayit.Tests` | Bağımlılıksız test koşucusu: Core kuralları, senaryolar ve ViewModel testleri (sahte portlarla) | Geliştirici |

Mimari: **MVVM + Ports & Adapters (hexagonal) + DI** (`Microsoft.Extensions.DependencyInjection`).
Yeni bir servis (ör. MES aktarımı, etiket yazıcısı) Core'da bir arayüz ve ayrı bir uygulamayla eklenir; ekran kodu değişmez.
Ayrıntı: `docs/plans/` altındaki faz planları.

Yayın: `publish\Inspector`, `publish\Inspector-arm64`, `publish\Admin`, `publish\Admin-arm64`. Hepsi bağımsız (self-contained), kurulum gerektirmez.
ARM64 sürümü Surface Pro 9 (SQ3, 5G) içindir; Intel Surface ve notebook'lar x64 kullanır.

## 2. Ortak klasör düzeni

```
<KÖK>\
  _config\defect_types.json     ← kusur tipi şablonları (admin yazar, muayene okur)
  _config\decision_texts.json   ← karar penceresindeki açıklamalar (admin yazar)
  _config\admin.json            ← admin PIN özeti (PBKDF2-SHA256, 200.000 tur, tuzlu)
  _index.json                   ← dashboard index'i
  _audit.log                    ← yalnızca eklemeli denetim kaydı, SHA-256 zinciri
  <Parça>\<Operasyon>\<Seri>\
    session.json
    defects\
      1.json                    ← kusur: ölçümler, high metal, kararlar, işaretler (markups)
      1_photo_01.jpg            ← orijinal fotoğraf, hiç değişmez
      1_photo_01_clean.jpg      ← arka planı kaldırılmış kopya (isteğe bağlı)
    reports\Rapor_v1.html / Rapor_v1.pdf
```

İzinler ve kurulum için: [KURULUM.md](KURULUM.md).

## 3. Akış (operatör)

1. Muayeneci parça / operasyon / seri girer (önceki kayıtlardan öneri gelir), **Muayene oluştur**.
2. **Kusur ekle**. Kusur tipinin şablonundaki ölçüm alanları görünür; spec adı varsayılan yazılır. Yan kusur **High metal** için onay kutusu.
3. **Fotoğraf**: kamera (C920 / Surface arka kamera). Ortadaki **çerçeve** (aç/kapa, boyutu ayarlanabilir, varsayılan kısa kenarın %20'si)
   kaydedilen fotoğrafta "#N" kutusu olur. Kaydedince kamera kapanır, fotoğraf işaretleme ekranında açılır. Dosyadan ekleme de var.
4. **İşaretle**: kutu, daire, ok + kusur no etiketi; işaretler seçilip sürüklenir, köşeden boyutlandırılır, Delete ile silinir.
   İsteğe bağlı arka plan kaldırma, "Orijinale dön" ile geri alınır. Fotoğraf kaldırılabilir (dosya ortak alanda kalır).
5. **Rapor (PDF)**: yatay A4; her fotoğraf ayrı sayfa (6.1, 6.2 …); işaretler fotoğraf üzerinde; geri alınan kararlar görünmez.
   Yazdırılır, mühendis kağıt üzerinde karar verir.
6. Karar sisteme girilir (**Disposition**) ve rapor yeni sürüm olarak yeniden üretilir.
7. Açık kusur kalmayınca **Muayeneyi tamamla**.

Ekran yönlendirmesi: adımlar (1. Ölçüm · 2. Fotoğraflar · 3. Disposition · Geçmiş) tamamlanınca ✓; seçilen kusurda sıradaki adımın
sekmesi açılır; ekranda yalnızca sıradaki işin düğmesi vurgulu (ISA-101: renk yalnızca dikkat gereken yerde; kapanan kusur yeşil).
Onay pencerelerinde onay düğmesi koyu, vazgeç sade; Enter hiçbir düğmeyi tetiklemez.

## 4. Disposition modeli

| Karar | Sonuç |
|---|---|
| Accept (SPA) | Kapandı |
| Rework → Reinspect (hedef operasyon, varsayılan muayenenin operasyonu) | Rework sonrası muayene bekliyor; kaydedince "şimdi mi yapıldı?" sorulur |
| Rework kapsamı (yalnızca High metal'li kusurda, zorunlu) | Kusur + high metal / yalnızca high metal |
| Rework sonrası: uygun | Kapandı |
| Rework sonrası: aynı kusur devam (değerler girilir; high metal varsa durumu zorunlu) | Açık, yeniden karar; artış/azalış gösterilir |
| Rework sonrası: eski kusur gitti, yeni hata oluştu | Child kusur açılır, "Defect 1 continues as Defect 5" |
| Reinspect (CTP), Repair | Açık sayılır |
| Submit MRB | MRB bekliyor (parça akışta ilerleyebilir, muayene açık kalır) |
| MRB kabul | Kapandı (accept as is) |
| MRB red | Otomatik Scrap |
| Return to vendor | Tedarikçide, dönüşte reinspect |
| Scrap | Kapandı, kesin |
| Void | Yalnızca karar geçmişi olmayan kusur, gerekçe zorunlu, onay penceresi. Kayıt kalır, rapora girmez |
| Yeniden aç | Not zorunlu, geçmiş korunur. Scrap ve MRB red açılamaz |
| Son kararı geri al | Gerekçe zorunlu. Kayıt silinmez; geri alınan karar ekranda ve raporda görünmez, ham dosyada ve denetim kaydında kalır. MRB reddi otomatik hurdayla birlikte geri alınır. Child devri geri alınırsa bağ çözülür, child void olur. Void geri alınamaz |

High metal: cümlede "There is Dent with high metal on part …". Yalnızca high metal giderildiyse kusur "Dent" olarak açık kalır,
otomatik not yazılır ve kalan kusur için yeni karar (ör. MRB) verilir.
Karar penceresindeki açıklamalar admin panelinden düzenlenir. Karar vermeden yapılan ölçüm ve high metal düzeltmeleri önce/sonra kaydedilir.
Denetim kaydına işlemi yapan muayeneci (Muayeneci kutusu) yazılır, oturumu açan kişi değil.

## 5. Güvenlik

| Risk | Önlem |
|---|---|
| Yol dolaşımı | Segment doğrulama, kök altı kontrolü, fotoğraf/kilit adları için kalıp kontrolü |
| Bozuk / büyük veri | JSON 5 MB, fotoğraf 25 MB sınırı, magic bytes (JPG, PNG) |
| HTML enjeksiyonu | Rapordaki tüm metinler HTML-encode, SVG sayıları değişmez biçimde yazılır |
| CSV formül enjeksiyonu | `= + - @` ile başlayan hücrelerin başına `'` |
| Admin PIN | Yalnızca tuzlu PBKDF2 özeti, sabit zamanlı karşılaştırma, 5 hatada 60 sn bekleme, 10 dk boşta kilit |
| Kilit kalması | 10 dk'dan eski kilitler admin panelinde listelenir; yalnızca eski olanlar, onayla silinir |
| Denetim kaydı | SHA-256 zinciri, admin panelinde ve dashboard'da doğrulama |
| PDF dönüştürme | Windows'taki Edge, Program Files'tan, geçici profil, ağ/eklenti/senkronizasyon kapalı |

Sınır: ortak klasöre yazma yetkisi olan biri dosya silebilir; program engelleyemez, denetim zinciri bunu gösterir. Asıl koruma NTFS izinleridir.

## 6. Bağımlılıklar

- Core ve testler: dış paket yok.
- Presentation: `CommunityToolkit.Mvvm 8.4.0`. Inspector: `Microsoft.Extensions.DependencyInjection 9.0.0`.
- Uygulamalar: `Microsoft.WindowsAppSDK 1.8.260921001`, `Microsoft.Windows.SDK.BuildTools 10.0.26100.9169`.
- Tüm sürümler sabit; `nuget.config` yerel `packages` klasörünü kullanır; otomatik güncelleme yok.
- OpenCV kullanılmadı: arka plan kaldırma Core içinde (GrabCut'ın basitleştirilmiş hali, eğitim yok). ARM64'te de çalışır.
- PDF için ek paket yok (Windows ile gelen Edge).

## 7. Durum

| Faz | İçerik | Durum |
|---|---|---|
| 1-2 | Core, durum makinesi, depolama, index, denetim, testler | Tamam |
| 3 | Muayene ekranı, disposition, rework senaryoları, void, ölçüm düzeltme, geri al, high metal | Tamam |
| 4 | Kamera (C920, Surface arka kamera, zoom, odak, kusur çerçevesi) | Tamam, sahada canlı test bekliyor |
| 5 | Fotoğraf işaretleme (taşı/boyutlandır), arka plan kaldırma | Tamam |
| 6 | Admin exe: PIN, şablonlar, karar açıklamaları, kayıtlar, CSV, bakım | Tamam, kullanıcı testi bekliyor |
| 7 | Yeni tasarım (Fluent, adım yönlendirmesi) ve MVVM + DI dönüşümü (muayene ekranı) | Tamam |
| 8 | Gerçek ağ paylaşımında çok kullanıcılı deneme | Sahada yapılacak |

## 8. Açık konular

1. Kamera: notebook + C920 ve Surface Pro 9 üzerinde canlı deneme.
2. Ağ paylaşımında iki muayenecinin aynı anda aynı seride çalışması (kilit davranışı) sahada denenmeli.
3. CTP'nin tam anlamı netleştirilmeli; kayıtta yalnızca kaynak olarak tutuluyor.
4. Admin exe henüz code-behind; gerekirse aynı MVVM yapısına taşınır.
