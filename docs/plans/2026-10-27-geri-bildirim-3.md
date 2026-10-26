# Geri bildirim 3: Nonconformance description cümlesi ve lot parça numaraları

Kaynak: `docs/plans/geri-bildirim-listesi-3.md` (madde 1-2). Madde 3 (çeviri) kullanıcı tarafından **iptal edildi**.

## Kurallar
- `main`, push yok (push'u ben yapacağım). Türkçe yorum, mevcut stil, MVVM (mantık Core / Presentation, test edilebilir saf fonksiyonlar).
- **Uygulamayı başlatma.** Doğrulama: derleme + testler.
- Derleme `dotnet build MuayeneKayit.sln -c Debug -p:Platform=x64` (0 hata, 0 uyarı); testler `dotnet run --project tests\MuayeneKayit.Tests`
  (şu an 158/158; yeni testleri `Program.cs` listesine ekle). PowerShell 5.1. UTF-8; Edit aracını kullan.
- Eski JSON kayıtları aynen açılmalı (yeni alan isteğe bağlı).

## 1. Yeni açıklama cümlesi (DefectSentence)
`DefectSentence` İngilizce cümlesi düzgün İngilizce ve kısa ölçü yazımına geçer. Ekran (`InspectionViewModel.Sentence`) ve rapor aynı fonksiyonu kullanır.
- Biçim: `There is a {type} on part: D 0.5 mm, W 2.4 mm, L 6 mm, Ø 3 mm, A 15°, color gray.`
  - Belirsiz artikel: sesli harfle başlayan tipte `an` ("an undercut"), değilse `a`. Tip adı küçük harfe çevrilmez (kullanıcı girdisi;
    "Dent" → "a Dent" yerine "a dent" mi? → **tip adı olduğu gibi yazılır**, yalnızca artikel eklenir: "There is a Dent …").
  - High metal: `a Dent with high metal`.
  - Ölçü kısaltmaları: D (depth), W (width), L (length), Ø (diameter; alan adı Radius), A (angle); değer ile birim arasında boşluk,
    birim kusurun birimi (mm / in, inch'te `.012 in`). Renk: `color {değer}` (kullanıcı girdisi, çevrilmez). Ölçü yoksa iki nokta ve liste yok:
    `There is a Crack on part.`
  - Ölçüler birbirinden ", " ile ayrılır; cümle "." ile biter.
- Parça kapsamı (madde 2): "on part" yerine:
  - boş: `on part`
  - tek: `on part 3`
  - çok: `on parts 1, 2 and 5`; ardışık aralıklar sıkıştırılır: `on parts 1–5 and 8` (en dash `–`), `on parts 1–3, 7 and 9–10`.
- Cümlenin ikinci satırı (Disposition / Continues …) mevcut biçimde kalır; ilk satır yukarıdaki gibi. `Defect N:` öneki ekranda kalır.
- Raporda (her kusurun **ilk** sayfasında, Ölçümler bölümünün üstünde): başlık `Uygunsuzluk açıklaması / Nonconformance description`,
  altında yalnızca ilk satır (önek ve disposition satırı olmadan). Void kusur zaten raporda yok.
- Testler: artikel (a / an), high metal, mm ve inch, ölçüsüz, renk, Ø, tek / çok / aralıklı parça listeleri, eski kayıt (alan yok).

## 2. Lot parça numaraları
- Model: `Defect.AffectedParts : string?` — normalize edilmiş liste metni ("1-5, 8"); null / boş = tüm parça / tek parça.
- Core: `PartList` yardımcı sınıfı:
  - `Parse(string text)` → sıralı, tekrarsız `IReadOnlyList<int>`; kabul: pozitif tam sayılar, virgül / boşluk / noktalı virgül ayırıcı,
    aralık `1-5` (tire ya da en dash). Geçersizse (harf, 0, ters aralık 5-1, 10000 üstü vb.) açıklayıcı Türkçe hata.
  - `Normalize(text)` → "1-5, 8"; `Describe(list)` → İngilizce cümle parçası (yukarıdaki kurallar).
- Inspector ölçüm sekmesi: Not kutusunun üstünde ya da ölçümlerin yanında metin kutusu
  `Etkilenen parçalar (lot; boş = tümü)` / yer tutucu `örn. 1-5, 8`, `AutomationId="AffectedPartsBox"`. **Her kusurda görünür**, isteğe bağlı.
  Ölçümler gibi otomatik kaydedilir (LostFocus / sekme / kusur değişimi; mevcut `AutoSave` akışına eklenir). Geçersiz girdi kaydedilmez,
  hata bildirimi gösterilir. Kaydedilen değer normalize edilmiş biçimde kutuya geri yazılır. Ölçüm düzeltme geçmişine **yazılmaz**
  (not alanı gibi). Düzenlenebilirlik ölçüm kutularıyla aynı kural (kapalı / void kusurda salt okunur).
- Rework sonrası "yeni hata" ile açılan kusurlar parent'ın `AffectedParts` değerini devralır (parent'la aynı parçalarda bulunduğu varsayılır;
  kullanıcı değiştirebilir).
- Rapor: kusur bilgisinde (Ölçümler bölümünde ayrı satır ya da açıklama cümlesinde) `Etkilenen parçalar / Affected parts: 1-5, 8`;
  boşsa satır gösterilmez. Dashboard / CSV: CSV'ye `Etkilenen parçalar` sütunu eklenir.
- Testler: Parse / Normalize / Describe kenar durumları, VM otomatik kayıt + geçersiz girdi, child devri, rapor satırı, CSV sütunu.

## Teslim
Testler geçer, Inspector + Admin Debug x64 0 hata / 0 uyarı. `KURULUM.md`'ye kısa not (lot alanı). Commit (`main`), Türkçe mesaj, mesaj dosyadan
(`git commit -F`, BOM'suz UTF-8), yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`,
son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Plan / liste dosyalarını da commit'le. Push yok.
Rapor: değişen dosyalar, test sayısı, derleme, sapmalar.
