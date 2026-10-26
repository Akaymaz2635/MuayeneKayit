# Geri bildirim listesi 2 (biriktiriliyor)

Kullanıcı "hazır" deyince plana dökülür (Opus), onaydan sonra Sonnet kodlar. Liste 1: geri-bildirim-listesi.md (tamamlandı, 2e0e7c1).

1. **Rapor başlığındaki ikinci satır kaldırılsın.**
   Kaldırılacak: "Proje DEMO-1400 · Inspector Ali Kaymaz (12345) · 27.10.2026 · Rapor v1 · Taslak, muayene henüz tamamlanmadı".
   (Üst satır "Parça … Operasyon … Seri …" kalır.)
   **Kullanıcı cevabı:** tarih alt bilgide (footer) kalsın, **MM.DD.YYYY** biçiminde (ör. 10.27.2026).
   Inspector, rapor sürümü ve taslak bilgisi rapordan kalkar (sürüm dosya adında zaten var).
   **Kullanıcı cevabı 2:** footer tarihi = muayenenin açıldığı tarih (Session.CreatedAt, mevcuttaki gibi).
   Ay sayı değil harfle yazılsın (ay-gün-yıl sırası): ör. **"Oct.27.2026"**. Karar geçmişi ve ölçüm düzeltmeleri tablolarındaki
   tarihler de aynı biçime geçsin (saatle: "Oct.27.2026 09:10").
   Planlarken teyit et: ay kısaltması İngilizce mi (Oct) Türkçe mi (Eki), büyük harf mi (OCT.27.2026)? Rapor iki dilli olduğu için
   önerilen: İngilizce üç harf, ilk harf büyük ("Oct.27.2026") — kültürden bağımsız sabit (InvariantCulture) yazılmalı.

2. **Kusur sayfasındaki tüm yazılar Türkçe + " / İngilizce".**
   Örnek: "Ölçümler / Measurements", "Derinlik / Depth", "Genişlik / Width", "Uzunluk / Length", "Çap / Diameter",
   "Açı / Angle", "Renk / Color", "Karar geçmişi / Disposition history", tablo başlıkları (Tarih / Date, Kim / By,
   Karar / Decision, Not / Note, Referans / Reference, Ölçüm (karar anı) / Measurement at decision),
   durum rozeti ("Kapandı / Closed"), karar adları (şu an İngilizce "Rework / Reinspect", "After rework conforms" →
   "Rework → Yeniden muayene / Rework → Reinspect" gibi), "Ölçüm düzeltmeleri / Measurement corrections", "Not / Note",
   bağlantı ve bölge satırları, "Fotoğraf yok / No photo", "Henüz karar yok / No decision yet",
   alt bilgi ("Kusur #9 · Deformation", "Sayfa 10 / 17" → "Kusur / Defect", "Sayfa / Page").
   Planlarken netleştir: kullanıcı girdisi (not, renk, kusur tipi adı) çevrilmez; İngilizce kusur cümlesi
   (DefectSentence, "Defect 3: There is …") raporda yok, değişmez. Tablo sütunları dar: iki dilli başlıklar alt satıra kayabilir.
