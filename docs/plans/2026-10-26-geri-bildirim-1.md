# Geri bildirim 1: İngilizce başlıklar, öneri gizleme, void, çoklu yeni hata, bölge (fotoğraf) bazında sonuç, Çap

Kaynak: `docs/plans/geri-bildirim-listesi.md` (madde 1-8, kullanıcı cevaplarıyla).

## Kurallar
- `main` dalında çalış, push yok. Türkçe yorum, mevcut stil (yorum yoğunluğu, adlandırma, MVVM: mantık Presentation / Core'da, View yalnızca çizer).
- **Uygulamayı başlatma.** Doğrulama: derleme + testler. Ekranda deneme ve deneme PDF'i sonradan yapılacak.
- Yeni NuGet yok. Eski JSON kayıtları (alanları olmayan) aynen açılmalı: tüm yeni alanlar isteğe bağlı, varsayılanı eski davranış.
- Derleme: `dotnet build MuayeneKayit.sln -c Debug -p:Platform=x64` (0 hata, 0 uyarı). Testler: `dotnet run --project tests\MuayeneKayit.Tests`
  (özel koşucu; yeni testleri `Program.cs` listesine ekle). Şu an 124/124.
- PowerShell 5.1 (`&&` yok). Türkçe karakterli dosyalarda Edit aracını kullan, kodlamayı bozma.

## 1-2. İngilizce başlıklar ("Muayeneci" → "Inspector")
Inspector başlangıç kartı (`src/MuayeneKayit.Inspector/MainWindow.xaml` ~47-64):
- Başlık `Muayene` → `Visual Inspection`; düğme `Muayene oluştur` → `Create Inspection`; açıklama metni Türkçe kalır.
- Sol menüdeki `Muayene` öğesi **değişmez**.

"Muayeneci" geçen her kullanıcı metni → "Inspector":
- `InspectionViewModel.cs`: `_inspectorHeader` varsayılanı, `InspectorHeader` ("Inspector (Windows oturumu)" / "Inspector"),
  `"Muayeneci adı boş olamaz."` → `"Inspector adı boş olamaz."`, `SessionSubtitle` `"Inspector: …"`.
- `MainWindow.xaml.cs:~400` bilgi mesajı (`muayeneci adınızı girip 'Muayene oluştur'a` → `Inspector adınızı girip 'Create Inspection'a`).
- `DashboardViewModel.cs:~31`, `Admin/RecordsViewModel.cs:~14` (`Muayeneci:` → `Inspector:`).
- `Core/Report/CsvExport.cs:~14` sütun başlığı, `Core/Report/ReportBuilder.cs:~64` başlık satırı.
- Admin `MainWindow.xaml`: `Muayeneci kimliği` → `Inspector kimliği`, açıklama metinleri (`muayeneci adı`), karar açıklamaları sayfasındaki
  `Muayeneci bir karar seçince…` → `Inspector bir karar seçince…`. `Admin/SettingsViewModel.cs` bildirim metinleri.
- Yorumlar (///) değişmek zorunda değil. Test beklentilerindeki metinleri güncelle.

## 3. Öneri listesinde "×" ile gizleme (Parça no, Operasyon, Seri no)
- Kapsam: **bu bilgisayar** (kişisel ayar). Saklama `%LOCALAPPDATA%\MuayeneKayit\hidden-suggestions.json`
  (`{ "part": [...], "operation": [...], "serial": [...] }`; büyük/küçük harf duyarsız karşılaştırma).
- Port: `IUserSettings`'e ekle (ya da yeni `ISuggestionFilter` portu — hangisi temizse):
  `IReadOnlySet<string> HiddenSuggestions(SuggestLevel level)`, `HideSuggestion(level, value)`, `UnhideSuggestions(part, op, serial)`.
  Inspector'da `UserSettingsAdapter` (aynı klasör, yazma hatası uygulamayı düşürmez). Testte bellek içi sahte.
- `InspectionViewModel.Suggestions(level, text)`: gizlenen değerleri listeden çıkarır.
- `InspectionViewModel.HideSuggestion(level, value)`: gizler (View listeyi yeniler).
- `OpenSession()` başarılıysa açılan Parça / Operasyon / Seri değerlerinin gizlemesi kalkar ("tekrar kullanınca geri gelir").
- View: üç `AutoSuggestBox` için `ItemTemplate`: solda metin, sağda küçük "×" düğmesi (`AutomationProperties.Name="Öneriyi gizle"`,
  ToolTip "Bu öneriyi gizle"). × tıklaması öneriyi seçmemeli (Click handled), listeyi aynı açık hâlde yeniler.
- Testler: gizlenen önerilmez, başka kutuyu etkilemez; o değerle oturum açılınca yine önerilir; büyük/küçük harf.

## 4. Void edilen kusur raporda (dolaylı da olsa) görünmesin
Mevcut: `ReportBuilder` void kusur sayfalarını atıyor (test var). **Boşluk:** rework sonrası açılan yeni kusur (child) sonra void edilirse
eski kusur (parent) `SupersededById` ile ona bağlı kalıyor: rapor cümlesi "Continues as Defect N", sayfa "Takip N numaralı kusur…",
ekranda "Takip #N…", `SessionGate` (void child "çözülmüş" sayılıp parent'ı kapatıyor).
- Child void edilince (bkz. madde 5 tasarımı): parent'ın aktif child listesinden düşer; parent'ın son kararı "yeni hata(lar)"
  (AfterReworkNewDefect) ise ve **hiç aktif child kalmadıysa** parent'a sistem kaydıyla o karar geri alınır (Undo, not:
  "Yeni kusur #N void edildi; #P yeniden rework sonrası muayene bekliyor") → parent tekrar AwaitingReinspect. Tek kilit altında
  (SessionStore'da yeni `VoidDefect` işlemi; `InspectionViewModel.VoidAsync` bunu kullanır).
- Rapor, cümle, ekran metinleri, Dashboard ve SessionGate yalnızca **void olmayan** child'ları gösterir / sayar.
- Testler: child void → parent cümlesinde / raporunda child numarası yok, parent tekrar bekliyor; birden çok child'dan biri void →
  diğeri kalır, parent devredilmiş kalır.

## 5 + 6. Birden fazla yeni hata ve bölge (fotoğraf) bazında sonuç

### Kavramlar
- **Bölge** = kusurun bir fotoğrafı (raporda 3.1, 3.2 …). Ölçüm kusur başına tek kalır (kullanıcı: değerler farklıysa ayrı kusur açar).
- Bölge durumu: `Active` (varsayılan; devam ediyor), `Conforms` (giderildi / uygun), `NewDefect` (bu kusurda kapandı, yerine yeni kusur açıldı).
- Bir bölge Conforms ya da NewDefect olunca sonraki kararlar ve muayeneler yalnızca **aktif** bölgeleri sorar.

### Model (`Core/Domain/Models.cs`, `Enums.cs`) — hepsi isteğe bağlı, eski JSON aynen okunur
- `enum PhotoOutcome { Continues, Conforms, NewDefect }`.
- `Disposition.PhotoOutcomes : Dictionary<string, PhotoOutcome>?` — bu kararla bölgelerin durumu (fotoğraf dosya adı → sonuç).
- `Disposition.NewDefectIds : List<int>?` — bu kararla açılan yeni kusurlar.
- `Defect.FoundAtPhoto : string?` — child'da: parent'ın hangi bölgesinde bulunduğu (fotoğraf adı; bölgesizse null).
- `Defect.SupersededById` (eski tek bağ) **okunur ama yeni kodda yazılmaz**: yükleme sonrası normalize et — doluysa ve son
  `AfterReworkNewDefect` kararının `NewDefectIds`'i boşsa `[SupersededById]` kabul et (bunu `DefectStateMachine`'de yardımcıyla yap,
  ör. `ChildIds(defect)`). Mevcut testler bu alanı kullanıyorsa yeni yardımcıya taşı; eski şekilde yazılmış JSON testi ekle.

### Kurallar (`DefectStateMachine` + yeni `DefectRegions` yardımcı sınıfı)
- `DefectRegions.Status(defect, photo)`: etkin (undo edilmemiş) kararlardan en sonuncusunun `PhotoOutcomes` değeri; yoksa Active.
  `ActivePhotos(defect)`.
- `ChildIds(defect)`: etkin kararların `NewDefectIds` birleşimi (+ eski `SupersededById`).
- `GetState`: son etkin (sistem olmayan) karar `AfterReworkNewDefect` ise `Superseded` (eski `SupersededById` kontrolü yerine);
  geri kalan aynı.
- Rework sonrası muayene sonucu (bölgeler ve yeni hatalardan otomatik):
  - Devam eden bölge yok ve yeni hata yok → `AfterReworkConforms` (kapanır).
  - En az bir bölge devam ediyor (ya da bölge yoksa "aynı kusur") → `AfterReworkSameDefect` (+ yeni ölçümler; yeni hata varsa
    `NewDefectIds` ile child'lar da açılır, parent açık kalır).
  - Devam eden yok, en az bir yeni hata → `AfterReworkNewDefect` (`NewDefectIds`; parent devredildi).
- `SessionStore.ApplyReinspect(folder, parentId, ReinspectResult, by)` — tek kilit altında: ölçümler (MeasurementEditor), karar
  (PhotoOutcomes, NewDefectIds), child'lar (`Id` sırayla, `Unit` parent'tan, `SupersedesId = parent`, `FoundAtPhoto`).
  `ContinueAsChild` bunun yerine geçer (ya da ona delege eder).
- Geri al (`UndoLastDecision`): geri alınan kararın `NewDefectIds` child'larının **hepsi** void edilir (herhangi birinin kararı varsa
  hata: "Önce #N kararlarını geri alın"). `UndoRequest.ChildToVoid` → `ChildrenToVoid : IReadOnlyList<int>`.
- Normal karar penceresi (`DecideAsync`): kusurun **2 ya da daha fazla aktif bölgesi** varsa ve karar Void / Reopen değilse
  "Bu karar hangi bölgeler için?" sorulur: aktif bölgeler onay kutusu, varsayılan hepsi işaretli; **işaretlenmeyen bölge uygun
  (Conforms) sayılır** (pencerede bu cümle yazar). En az biri işaretli olmalı. `PhotoOutcomes`: işaretli → Continues, işaretsiz → Conforms.
- `SessionGate`: Superseded parent, **void olmayan** tüm child'ları çözülünce çözülür; void child sayılmaz (madde 4).

### Pencereler (Presentation istek / sonuç kayıtları + `WinUiDialogService`)
Rework sonrası / yeniden kontrol penceresi:
- **Aktif bölge 0 ya da 1 ise:** bugünkü 3 seçenek. "Yeni hata" seçilince yeni hata listesi: satır başına tip ComboBox + "×" (satırı sil),
  altında "+ Hata ekle". En az bir satır ve her satırda tip seçili olmalı. Aynı tip birden fazla kez seçilebilir.
- **Aktif bölge 2+ ise:** "Bölgeler" listesi: her satırda küçük resim (varsa temiz kopya), etiket (3.1), üç seçenek
  (Giderildi / Devam ediyor / Yeni hata), varsayılan seçimsiz. "Yeni hata" seçilen satırın altında o bölgenin yeni hata listesi
  (tip + × , "+ Bu bölgeye hata ekle"). Her bölge için seçim zorunlu.
  Devam eden bölge varsa ölçüm alanları (bugünkü "aynı kusur" adımı, high metal sorusu dahil) açılır.
  Altta otomatik sonuç özeti: "#3 kapanır" / "#3 açık kalır; 2 yeni kusur açılır" / "#3 yeni kusurlara devredilir".
- Doğrulama kuralları `ReinspectForm`'a (test edilebilir, saf fonksiyonlar); View yalnızca çizer.
- Pencere 480 px genişlik kuralı; satırlar sığmazsa dikey yerleşim.

Karar penceresi: yukarıdaki bölge onay kutuları (küçük resim + etiket).

### Gösterim
- `DefectSentence.Describe`: devredildiyse `Continues as Defect 7.` / `Continues as Defects 7, 8.`; açık kalıp yeni hata
  bulunduysa `New defects found after rework: 7, 8.`; child: `Continues from Defect 3 (region 3.3).` (bölge varsa).
  Yalnızca void olmayan child'lar.
- `ReportBuilder`: parent bağlantı metni çoğul; child'da "3 numaralı kusurun 3.3 bölgesinde, rework sonrası muayenede bulundu".
  **Her fotoğraf sayfasında bölge durumu** satırı: "Bölge 3.1 — Giderildi (dd.MM.yyyy, karar)", "Bölge 3.2 — Devam ediyor",
  "Bölge 3.3 — Yeni kusur #7, #8". Tek fotoğraflı kusurda satır gösterilmez.
- Inspector ekranı: geçmiş satırları bölge sonuçlarını ve açılan kusurları gösterir; fotoğraf kartında bölge durumu rozeti
  (Giderildi / Yeni kusur); `NextStepFor` / "Takip #…" metinleri çoğul.
- Dashboard, Admin kayıtları: davranış aynı, yalnızca çoklu child'a uyum.

### Testler (en az)
Çok yeni hata (aynı tip ×2 + farklı tip) → 3 child, parent devredildi, cümle "Defects a, b, c"; bölgeli karışık sonuç
(3.1 giderildi, 3.2 devam, 3.3 yeni hata ×2) → parent açık + ölçüm güncel + 2 child `FoundAtPhoto`; hepsi giderildi → kapanır;
karar penceresinde bölge seçimi → işaretsiz Conforms, sonraki pencerede yalnızca aktif bölgeler; undo → tüm child'lar void,
bölge durumları geri gelir; eski `SupersededById` JSON'u → aynen devredildi görünür; rapor HTML'de bölge satırları ve çoğul bağlantı;
SessionGate çoklu child.

## 7. "Yarıçap" → "Çap"
Yalnızca kullanıcı metinleri: Inspector ölçüm başlığı (`InspectionViewModel` `RadiusHeader`, ayrıştırma mesajı), rework penceresi,
`MeasurementCompare`, `DefectValidator` mesajı, `ReportBuilder` hücresi, Admin kusur tipleri onay kutusu → `Çap`;
`DefectSentence` → `Diameter`. **JSON / C# alan adı `Radius` değişmez** (eski kayıtlar okunur). Test beklentilerini güncelle.

## Teslim
1. Tüm testler geçer (124 + yeni), Inspector ve Admin Debug x64 0 hata / 0 uyarı.
2. `KURULUM.md` / `PLAN.md`'de değişen davranışa kısa not.
3. Commit (`main`), Türkçe mesaj, yazar `-c user.name="Ali Kaymaz" -c user.email="alikaymazbireysel@gmail.com"`, mesaj dosyadan
   (`git commit -F`, BOM'suz UTF-8), son satır `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Gerekirse birden fazla commit.
4. Rapor: değişen dosyalar, test sayısı, derleme, plandan sapmalar, doğrulanamayanlar.
