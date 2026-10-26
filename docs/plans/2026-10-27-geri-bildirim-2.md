# Geri bildirim 2: rapor başlığı, iki dilli kusur sayfası, tarih biçimi

Kaynak: `docs/plans/geri-bildirim-listesi-2.md`. Yalnızca rapor (`src/MuayeneKayit.Core/Report/ReportBuilder.cs` ve
yardımcıları) değişir; muayene ekranı ve İngilizce kusur cümlesi (`DefectSentence.Describe`, raporda yok) değişmez.

## Kurallar
- `main`, push yok. Türkçe yorum, mevcut stil. **Uygulamayı başlatma.** Doğrulama: derleme + testler.
- Derleme `dotnet build MuayeneKayit.sln -c Debug -p:Platform=x64` (0 hata, 0 uyarı); testler
  `dotnet run --project tests\MuayeneKayit.Tests` (şu an 151/151; yeni testleri `Program.cs` listesine ekle).
- PowerShell 5.1. Türkçe karakterli dosyalarda Edit aracını kullan; kodlamayı bozma. HTML'e giden her metin `E(...)` ile kodlanır.

## 1. Başlık
`Header(...)`: ikinci satır ("Proje … · Muayeneci/Inspector … · tarih · Rapor vN · durum") **tamamen kaldırılır**.
Üst satır kalır, iki dilli: `Parça / Part {P}   Operasyon / Operation {O}   Seri / Serial {S}`.
Kalkan bilgiler: Inspector, rapor sürümü, taslak / tamamlandı durumu. (`<title>` etiketindeki "Rapor vN" kalabilir.)
Kusur kaydı olmayan oturumun tek sayfası da aynı başlığı kullanır.

## 2. Tarih biçimi (ay harfle, ay-gün-yıl)
- Tek yardımcı: `ReportDates.Day(DateTime)` → `"Oct.27.2026"`, `ReportDates.DayTime(DateTime)` → `"Oct.27.2026 09:10"`.
  Ay İngilizce üç harf, ilk harf büyük; **InvariantCulture** ile (bilgisayarın dil ayarından bağımsız): `MMM.dd.yyyy`, `MMM.dd.yyyy HH:mm`.
- **Alt bilgi (footer):** sol tarafta mevcut kusur etiketi, ortada / solda **muayenenin açıldığı tarih** (`Session.CreatedAt`, `Day`),
  sağda sayfa no. Örnek: `Kusur / Defect #9 · Deformation   ·   Oct.27.2026            Sayfa / Page 10 / 17`.
- Karar geçmişi ve ölçüm düzeltmeleri tablolarındaki tarihler `DayTime`. Bölge satırındaki tarih (ör. "Giderildi (27.10.2026, …)") `Day`.

## 3. Kusur sayfası iki dilli: "Türkçe / English"
Raporda kullanıcıya görünen **sabit** her metin "Türkçe / English" olur. Kullanıcı girdisi (not, renk, referans, kusur tipi adı,
Inspector adı, dosya adı) çevrilmez. Tek yerde tutulan çeviri tablosu önerilir (ör. `ReportText` sınıfı, `Bi(tr, en)` yardımcısı).
- Bölüm başlıkları: Ölçümler / Measurements · Karar geçmişi / Disposition history · Ölçüm düzeltmeleri / Measurement corrections ·
  Not / Note (not paragrafı etiketi).
- Ölçüm hücreleri: Derinlik / Depth · Genişlik / Width · Uzunluk / Length · Çap / Diameter · Açı / Angle · Renk / Color.
- Karar geçmişi sütunları: # · Tarih / Date · Kim / By · Karar / Decision · Not / Note · Referans / Reference ·
  Ölçüm (karar anı) / Measurement at decision. Ölçüm düzeltmeleri: Tarih / Date · Kim / By · Önce / Before · Sonra / After.
- Durum rozeti (`StateText`): Açık / Open · Kapandı / Closed · Rework sonrası muayene bekliyor / Awaiting reinspection ·
  Repair'de / In repair · Tedarikçide / At vendor · MRB bekliyor / MRB pending · Devredildi / Superseded · Void / Void (mevcut
  Türkçe etiketleri kullan, İngilizcesini ekle).
- Karar adları (geçmiş tablosu): Türkçe / İngilizce. Türkçe karşılıklar `DefectPresentation.DecisionOrder` ve mevcut ekran
  etiketleriyle tutarlı olsun; İngilizcesi mevcut `DefectSentence.DecisionLabel`. Örn. "Kabul (SPA) / Accept",
  "Rework → Yeniden muayene / Rework / Reinspect", "Rework sonrası uygun / After rework conforms",
  "Rework sonrası aynı kusur devam / After rework: same defect continues", "Rework sonrası yeni hata / After rework: new defect found",
  "MRB'ye gönderildi / Submit MRB (pending)", "MRB kabul / MRB accepted (accept as is)", "MRB red / MRB rejected", "Hurda / Scrap",
  "Tedarikçiye iade / Return to vendor", "Yeniden açıldı / Reopened", high-metal kapsamı "(yalnızca high metal / high metal only)".
  Sistem kaydı notu ("MRB reddi, otomatik hurda") kullanıcı girdisi değil sistem metnidir: "MRB reddi, otomatik hurda / MRB rejected, automatic scrap".
- Bağlantı satırları (devir / bulundu / yeni kusurlar): Türkçe cümle, altında (ya da " / " ile) İngilizcesi; uzun olduğu için
  İngilizce satırı aynı kutuda ikinci satır, biraz soluk yaz (CSS sınıfı). Örn. "Bu kusur, 3 numaralı kusurun 3.3 bölgesinde,
  rework sonrası muayenede bulundu." / "Found at region 3.3 of defect 3 during inspection after rework."
- Bölge satırı: "Bölge / Region 3.1 — Giderildi / Resolved (Oct.27.2026, Rework → Yeniden muayene / Rework / Reinspect)",
  "Devam ediyor / Continues", "Yeni kusur / New defect #4, #5", "Kapandı / Closed".
- Diğer: "Fotoğraf 1 / 3" → "Fotoğraf / Photo 1 / 3", "Fotoğraf yok / No photo", "Henüz karar yok / No decision yet",
  "Karar geçmişi ve ölçüm düzeltmeleri #3.1 sayfasında." → + " / Disposition history and corrections on page #3.1.",
  "Kusur kaydı yok. / No defects recorded.", alt bilgi "Kusur / Defect", "Sayfa / Page", "Parça / Part", "Seri / Serial".
- Yerleşim: iki dilli başlıklar dar sütunlarda alt satıra kayabilir; tablo yazı boyutu / sütun genişlikleri gerekirse ayarlanır,
  sayfa A4 yatay sığmalı (mevcut `.page` yüksekliği taşmasın). Ölçüm hücresi etiketleri küçük harf / büyük harf stili mevcut CSS'e uyar.

## 4. Testler
- Başlıkta "Proje", "Inspector", "Rapor v", "Taslak" geçmez; "Parça / Part" geçer.
- Footer'da açılış tarihi `Oct.27.2026` biçiminde (Türkçe kültürde çalışırken de: testte `CultureInfo.CurrentCulture = tr-TR` ayarla).
- Tablo tarihleri `MMM.dd.yyyy HH:mm`.
- İki dilli etiketler: "Ölçümler / Measurements", "Çap / Diameter", "Karar geçmişi / Disposition history", durum rozeti
  "Kapandı / Closed", karar "Kabul (SPA) / Accept", bölge satırı "Bölge / Region".
- Mevcut rapor testlerinin beklentilerini güncelle (HTML kodlaması: `E()` Türkçe karakterleri `&#…;` yapar, mevcut testler buna göre).

## Teslim
Testler geçer, Inspector + Admin Debug x64 0 hata / 0 uyarı. Commit (`main`), Türkçe mesaj, mesaj dosyadan (`git commit -F`, BOM'suz UTF-8),
yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`, son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
Rapor: değişen dosyalar, test sayısı, derleme, sapmalar.
