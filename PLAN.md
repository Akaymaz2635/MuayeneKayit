# MuayeneKayit — Program Planı

Yerel ağ (LAN) üzerinde çalışan, ortak klasöre yazıp okuyan, dış bağlantısı olmayan muayene ve hata kayıt programı.
Veritabanı, bulut, telemetri ve otomatik güncelleme yoktur.

## 1. Uygulamalar

| Proje | Ne | Kim |
|---|---|---|
| `src/MuayeneKayit.Core` | Kurallar, depolama, index, denetim, rapor, arka plan kaldırma, admin ayarları. Yalnızca .NET BCL. | İkisi de kullanır |
| `src/MuayeneKayit.Inspector` | Muayene exe'si (WinUI 3): kusur, ölçüm, kamera, fotoğraf işaretleme, disposition, rapor, dashboard | Muayeneci |
| `src/MuayeneKayit.Admin` | Admin exe'si (WinUI 3): PIN girişi, kusur tipi şablonları, tüm kayıtlar, CSV, bakım, ayarlar | Admin |
| `tests/MuayeneKayit.Tests` | Bağımlılıksız test koşucusu (43 test) | Geliştirici |

Yayın: `publish\Inspector`, `publish\Inspector-arm64`, `publish\Admin`, `publish\Admin-arm64`. Hepsi bağımsız (self-contained), kurulum gerektirmez.
ARM64 sürümü Surface Pro 9 (SQ3, 5G) içindir; Intel Surface ve notebook'lar x64 kullanır.

## 2. Ortak klasör düzeni

```
<KÖK>\
  _config\defect_types.json     ← kusur tipi şablonları (admin yazar, muayene okur)
  _config\admin.json            ← admin PIN özeti (PBKDF2-SHA256, 200.000 tur, tuzlu)
  _index.json                   ← dashboard index'i
  _audit.log                    ← yalnızca eklemeli denetim kaydı, SHA-256 zinciri
  <Parça>\<Operasyon>\<Seri>\
    session.json
    defects\
      1.json                    ← kusur: ölçümler, kararlar, işaretler (markups)
      1_photo_01.jpg            ← orijinal fotoğraf, hiç değişmez
      1_photo_01_clean.jpg      ← arka planı kaldırılmış kopya (isteğe bağlı)
    reports\Rapor_v1.html / Rapor_v1.pdf
```

İzinler ve kurulum için: [KURULUM.md](KURULUM.md).

## 3. Akış

1. Muayeneci parça / operasyon / seri girer (önceki kayıtlardan öneri gelir), **Muayene oluştur**.
2. Kusur ekler. Kusur tipinin şablonundaki ölçüm alanları görünür; spec adı varsayılan yazılır.
3. Fotoğraf: **Kamera ile çek** (C920 / Surface arka kamera) ya da dosyadan.
4. **İşaretle / arka plan**: kutu, daire, ok + kusur no etiketi. İsteğe bağlı arka plan kaldırma, "Orijinale dön" ile geri alınır.
5. **Rapor oluştur (PDF)**: yatay A4, kusur başına bir sayfa, işaretler fotoğraf üzerinde. Yazdırılır, mühendis kağıt üzerinde karar verir.
6. Karar sisteme girilir (Disposition) ve rapor yeni sürüm olarak yeniden üretilir.
7. Açık kusur kalmayınca **Muayeneyi tamamla**.

## 4. Disposition modeli

| Karar | Sonuç |
|---|---|
| Accept (SPA) | Kapandı |
| Rework → Reinspect (hedef operasyon, varsayılan muayenenin operasyonu) | Rework sonrası muayene bekliyor |
| Rework sonrası: uygun | Kapandı |
| Rework sonrası: aynı kusur devam (değer değişebilir, artış/azalış gösterilir) | Açık, yeniden karar |
| Rework sonrası: eski kusur gitti, yeni hata oluştu | Child kusur açılır, "Defect 1 continues as Defect 5" |
| Reinspect (CTP), Repair | Açık sayılır |
| Submit MRB | MRB bekliyor (parça akışta ilerleyebilir, muayene açık kalır) |
| MRB kabul | Kapandı (accept as is) |
| MRB red | Otomatik Scrap |
| Return to vendor | Tedarikçide, dönüşte reinspect |
| Scrap | Kapandı, kesin |
| Void | Yalnızca karar geçmişi olmayan kusur, gerekçe zorunlu, onay penceresi. Kayıt kalır, rapora girmez |
| Yeniden aç | Not zorunlu, geçmiş korunur. Scrap ve MRB red açılamaz |

| Son kararı geri al | Gerekçe zorunlu. Kayıt silinmez, "geri alındı" olarak işaretlenir; MRB reddi otomatik hurdayla birlikte geri alınır. Child devri geri alınırsa bağ çözülür, child void olur (child'ın kararı varsa önce o geri alınır). Void geri alınamaz |

Karar seçilince onay penceresi açılır: kararın ne yapacağı (açıklamalar admin panelinden düzenlenir) ve yalnızca o kararın alanları.
Hedef operasyon muayenenin operasyon numarasıyla dolu gelir, değiştirilebilir.
Karar vermeden yapılan ölçüm düzeltmeleri önce/sonra olarak kaydedilir.
Denetim kaydına işlemi yapan muayeneci (Muayeneci kutusu) yazılır, oturumu açan kişi değil.

## 5. Güvenlik

| Risk | Önlem |
|---|---|
| Yol dolaşımı | Segment doğrulama, kök altı kontrolü, fotoğraf/kilit adları için kalıp kontrolü |
| Bozuk / büyük veri | JSON 5 MB, fotoğraf ve tarama 25 MB sınırı, magic bytes (JPG, PNG, PDF) |
| HTML enjeksiyonu | Rapordaki tüm metinler HTML-encode, SVG sayıları değişmez biçimde yazılır |
| CSV formül enjeksiyonu | `= + - @` ile başlayan hücrelerin başına `'` |
| Admin PIN | Yalnızca tuzlu PBKDF2 özeti, sabit zamanlı karşılaştırma, 5 hatada 60 sn bekleme, 10 dk boşta kilit |
| Kilit kalması | 10 dk'dan eski kilitler admin panelinde listelenir; yalnızca eski olanlar silinebilir |
| Denetim kaydı | SHA-256 zinciri, admin panelinde ve dashboard'da doğrulama |
| PDF dönüştürme | Windows'taki Edge, Program Files'tan, geçici profil, ağ/eklenti/senkronizasyon kapalı |

Sınır: ortak klasöre yazma yetkisi olan biri dosya silebilir; program engelleyemez, denetim zinciri bunu gösterir. Asıl koruma NTFS izinleridir.

## 6. Bağımlılıklar

- Core ve testler: dış paket yok.
- Uygulamalar: `Microsoft.WindowsAppSDK 1.8.260921001`, `Microsoft.Windows.SDK.BuildTools 10.0.26100.9169` (sabit sürüm).
- OpenCV kullanılmadı: arka plan kaldırma Core içinde (GrabCut'ın basitleştirilmiş hali, eğitim yok). Böylece ARM64'te de çalışıyor.
- PDF için ek paket yok (Windows ile gelen Edge).
- `nuget.config` yerel `packages` klasörünü kullanır; otomatik güncelleme yok.

## 7. Durum

| Faz | İçerik | Durum |
|---|---|---|
| 1-2 | Core, durum makinesi, depolama, index, denetim, testler | Tamam |
| 3 | Muayene ekranı, disposition, rework senaryoları, void, ölçüm düzeltme | Tamam |
| 4 | Kamera (C920, Surface arka kamera, zoom, odak) | Tamam, sahada canlı test bekliyor |
| 5 | Fotoğraf işaretleme, arka plan kaldırma | Tamam |
| 6 | Admin exe: PIN, şablonlar, kayıtlar, CSV, bakım | Tamam, kullanıcı testi bekliyor |
| 8 | Gerçek ağ paylaşımında çok kullanıcılı deneme | Sahada yapılacak |

## 8. Açık konular

1. Kamera: notebook + C920 ve Surface Pro 9 üzerinde canlı deneme.
2. Ağ paylaşımında iki muayenecinin aynı anda aynı seride çalışması (kilit davranışı) sahada denenmeli.
3. CTP'nin tam anlamı netleştirilmeli; kayıtta yalnızca kaynak olarak tutuluyor.
