# Geri bildirim listesi (biriktiriliyor)

Kullanıcı "hazır" deyince bu liste plana dökülür (Opus), onaydan sonra Sonnet kodlar.

1. **Başlangıç kartı: "Muayeneci" etiketi → "Inspector"** (İngilizce yazımla; kullanıcı düzeltti, "İnspektör" değil).
   Muayene oluşturma kartındaki "Muayeneci" kutusunun başlığı "Inspector" olacak.
   Planlarken netleştir: "Muayeneci (Windows oturumu)" başlığı, oturum başlığındaki "Muayeneci: …" satırı,
   Admin'deki "Muayeneci kimliği" kartı, Dashboard / kayıt kartları, rapor ve CSV'deki "Muayeneci" geçen yerler de değişsin mi?

2. **Başlangıç kartı İngilizce** (kullanıcı: başlık "Visual Inspect", düğme "Inspect Oluştur"; gramerce doğrusu seçilsin):
   - Başlık "Muayene" → **"Visual Inspection"** ("inspect" fiil; başlıkta isim hâli doğru).
   - "Muayene oluştur" düğmesi → **"Create Inspection"** (Türkçe-İngilizce karışık "Inspect Oluştur" yerine).
   Planlarken netleştir: sol menüdeki "Muayene" öğesi ve kartın açıklama metni de İngilizce olsun mu?

3. **Öneri listesinde "×" ile öneriyi gizleme** (Parça no, Operasyon, Seri no).
   - Her önerinin yanında bir çarpı olsun. Örn. denemelik yazılmış "1" artık önerilmesin.
   - Çarpıya basılan değer, kullanıcı o değeri tekrar kullanana kadar (bir sonraki "1" yazıp muayene açışına kadar) önerilmez;
     tekrar kullanılınca gizleme kalkar ve yine önerilir.
   - Kayıt silinmez; yalnızca öneri gizlenir.
   Planlarken netleştir: gizleme bu bilgisayara mı (kişisel ayar, %LOCALAPPDATA%) yoksa herkese mi uygulansın?
   "Tekrar kullanma" = o değerle muayene açmak mı, yoksa kutuya aynısını yazmak mı?

4. **Void edilmiş kusurlar raporda olmasın.**
   Mevcut durum: ReportBuilder void kusurların sayfasını zaten çıkarıyor (test: Program.cs "Void kayıt rapora girmemeli").
   Planlarken kontrol et: void kusura başka yerden atıf kalıyor mu (devredilen kusurun "takip #N" bağlantısı, karar geçmişi,
   sayfa numaraları / boşluklar, rapor başlığındaki kusur sayısı)? Kullanıcıya raporda void'i nerede gördüğünü sor (ekran görüntüsü).

5. **Reinspect / yeniden kontrol: "eski kusur gitti ama yeni hata oluştu" seçeneğinde birden fazla yeni hata.**
   Şu an tek bir "Yeni hatanın tipi" seçiliyor ve tek yeni kusur açılıyor (#4 → "Defect 4 continues as Defect N").
   İstenen: birden fazla yeni hata girilebilsin (ör. "+ Hata ekle" ile ikinci, üçüncü tip satırı).
   Planlarken düşün: her biri için ayrı yeni kusur açılır; eski kusurun devir kaydı / cümlesi birden fazla kusuru göstermeli
   ("Defect 4 continues as Defects 7, 8"); rapor, geçmiş, dashboard, undo (geri al) ve "takip #N" bağlantıları çoklu devre uymalı;
   eski JSON (tek SupersededById) geriye uyumlu okunmalı. Aynı tipten iki yeni hata olabilir mi? Ölçüm / fotoğraf her yeni kusurda sonra girilir.

6. **Çok fotoğraflı kusurda fotoğraf (bölge) bazında karar ve yeniden kontrol.**
   Bir kusura birden fazla fotoğraf eklenince rapor 3.1, 3.2, 3.3 diye ayırıyor. İstenen:
   - Disposition verilirken hangi fotoğraflara (3.1 / 3.2 / hepsi) uygulandığı seçilebilsin.
   - Yeniden kontrolde (reinspect / rework sonrası) her fotoğraf için ayrı sonuç: 3.1 giderildi, 3.2 devam ediyor,
     3.3 üzerinde yeni bir hata oluştu (→ yeni kusur, madde 5 ile birlikte).
   - Rapor ve geçmiş, her fotoğrafın (bölgenin) durumunu göstersin.
   **Önerilen yön (kullanıcıyla konuşuldu, kesin değil — planda teyit et):** ayrı kayıt (alt kusur) YOK, hafif çözüm:
   - Kusur tek kayıt, ölçüm kusur başına tek (en kötü / devam eden bölgenin değeri).
   - Karar ve yeniden kontrol penceresinde fotoğraf küçük resimleri; her fotoğrafa bölge durumu:
     Conforms / Giderildi · Devam ediyor · Yeni hata (→ yeni kusur, madde 5).
   - Karar "devam ediyor" fotoğraflarına uygulanır; hepsi conforms ise kusur kapanır.
   - Raporda her fotoğraf sayfasında bölge durumu ("3.1 — Conforms (rework sonrası)", "3.2 — Devam ediyor, Submit MRB").
   - Bölgelerin değerleri gerçekten farklıysa istisna: o bölge bugünkü gibi ayrı kusur olarak eklenir.
   - Fotoğraf durumu karar kaydına (Disposition) ve geçmişe yazılır; eski kayıtlar (durumsuz fotoğraflar) "hepsi" sayılır. Madde 5 ile ortak tasarlanmalı.
   **Kullanıcı onayladı:** değerler farklıysa ayrı kusur yazacak; değerler aynıysa tek kusura birden fazla fotoğraf çekecek
   → fotoğraf (bölge) bazında durum yukarıdaki hafif çözümle yapılacak. Plan aşamasında pencere taslağını yine göster.

7. **Ölçüm alanı "Yarıçap" → "Çap"** (kusur tiplerinde / hata modlarında). Raporda İngilizce "Radius" → "Diameter".

8. **İş bitince deneme PDF raporu:** her türden veri girilmiş (tüm ölçüm alanları, high metal, inch kusur, çok fotoğraflı
   kusur ve bölge durumları, rework / yeni hatalar, MRB, void, ölçüm düzeltmesi) bir test muayenesinin PDF'i kullanıcıya gönderilsin;
   başlık ve yazıları gözden geçirecek.

Kullanıcı cevapları (plan öncesi): İngilizce = kart + "Muayeneci" geçen her yer "Inspector"; gizleme = bu bilgisayar, o değerle
muayene açılınca geri gelir; yeni hatalar = aynı tipten de farklı tipten de bir ya da birden fazla; void = görülmedi, kontrol amaçlı.
