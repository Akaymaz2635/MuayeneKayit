# Geri bildirim listesi 3 (biriktiriliyor)

Kullanıcı "hazır" deyince plana dökülür (Opus), onaydan sonra Sonnet kodlar. Liste 2 tamamlandı (d90ffd6).

1. **Raporda "Nonconformance description" cümlesi.**
   Kusur sayfasında İngilizce açıklama cümlesi yazmıyor (DefectSentence şu an yalnızca ekranda). Kullanıcı örneği:
   "There is Dent with high metal with value D:0.5 , W:2.4, L:6, Ø:3, A:15°,Color:Gray."
   Gramer düzeltilmiş öneri (planda kullanıcıya göster): "There is a dent with high metal: D 0.5 mm, W 2.4 mm, L 6 mm,
   Ø 3 mm, A 15°, color gray." Kısaltmalar: D (depth), W (width), L (length), Ø (diameter), A (angle). Birim (mm / in) korunur.
   Başlık "Uygunsuzluk açıklaması / Nonconformance description". Ekrandaki cümle de aynı yeni biçime geçsin mi — sor.
   Renk kullanıcı girdisi (Türkçe "gri" yazılırsa çevrilmez; bkz. madde 3).

2. **Lotlu üretim: hatanın olduğu parça numaraları.**
   Bir seri numarasına bağlı birden fazla aynı parça (lot) için, hata tüm parçalarda yoksa kusurda bir metin kutusu:
   "1,2,3,4,5" gibi parça numaraları. Cümleye eklenir: "… on parts 1, 2, 3, 4 and 5" (aralık: "on parts 1–5"; tek: "on part 3").
   Boşsa tüm parçalar / tek parça (bugünkü "on part"). Gramer en düzgün hâle getirilsin.
   **Kullanıcı kararı:** kutu **her kusurda** görünür (isteğe bağlı); muayene başında "lot" seçmeyi unutup kaydı silmek zorunda kalmasın.
   Boş = tüm parça / tek parça. Planlarken sor: lot büyüklüğü (toplam parça sayısı) girilsin mi? Düzeltme geçmişine yazılsın mı
   (ölçüm alanları gibi otomatik kayıtla)? Geçerli girdi: sayılar, virgül, aralık ("1-5, 8").

3. **[İPTAL — kullanıcı: "çeviri şimdilik iptal"]** Türkçe notların İngilizceye otomatik çevirisi — İNTERNETSİZ.
   Model programla gelir, bilgisayarda çalışır (OPUS-MT tr→en, ONNX; ya da eşdeğeri). Çeviri öneri olarak gösterilir,
   Inspector kaydetmeden önce düzeltebilir; raporda "Not / Note" altında Türkçe + İngilizce.
   Planlarken araştır / karar ver: .NET'te çalıştırma yolu (Microsoft.ML.OnnxRuntime + SentencePiece tokenizer; yeni NuGet ve
   offline paket zip'i güncellenmeli), model boyutu (~100-300 MB, ağ klasöründen çalıştırmada açılış süresine etkisi), ARM64 desteği,
   lisans (OPUS-MT CC-BY 4.0 — atıf), hangi alanlar çevrilir (kusur notu, karar notu?), çeviri ne zaman yapılır (not kaydedilince,
   arka planda; ilk çeviride model yüklenme süresi), renk gibi kısa alanlar.
   **Kalite endişesi (kullanıcı):** teknik terimler doğru çevrilmeli. Örnekler: "markalama kenarında çapak kalmış parça pis ve lekeli"
   → "Burr remains on the marking edge; the part is dirty and stained."; "sac büküm izi mevcut" → "Sheet metal bend mark is present."
   Genel model "çapak"ı chip / flash, "markalama"yı branding, "mevcut"u available diyebilir.
   → **Terim sözlüğü (Admin'den yönetilir, ortak klasörde):** çapak→burr, sac→sheet metal, büküm izi→bend mark, markalama→marking,
   mevcut→present, lekeli→stained … Çeviri öncesi / sonrası zorla uygulanır. Çeviri her zaman öneri; Inspector düzeltip kaydeder.
   Gerçek kaliteyi görmek için kullanıcıdan Argos Translate + tr→en modelini (~100 MB) indirip denemek için onay istendi.
