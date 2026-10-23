# MuayeneKayit — Tasarım Kuralları

Bu dosya arayüzün tek kaynağıdır. Yeni bir ekran eklenirken buradaki renk ve boşluk değerleri kullanılır.

## Renkler

| Rol | Değer | Kullanım |
|---|---|---|
| Arka plan | `#F3F5F9` | Sayfa zemini |
| Kart | `#FFFFFF` | Bölüm kartları |
| Kart kenarı | `#DDE2EA` | 1 px kenarlık |
| Vurgu (lacivert) | `#1A3A5C` | Başlıklar, birincil butonlar |
| Metin | varsayılan | Gövde metni |
| İkincil metin | `#555555` | Açıklamalar |

Koyu tema kullanılmaz. Arayüz `RequestedTheme="Light"` ile açılır, saha ortamında parlamayı azaltmak için.

## Durum renkleri

| Durum | Renk | Not |
|---|---|---|
| Açık / bekleyen | `#B45309` (kehribar) | Dikkat gerektirir |
| Kapandı | `#15803D` (yeşil) | Tamamlanmış |
| Void / hata | `#B91C1C` (kırmızı) | Geçersiz ya da hatalı |
| Bilgi | `#1D4ED8` (mavi) | Nötr bildirim |

Durumu yalnızca renkle göstermeyin, metin etiketi de yanında olsun.

## Boşluk ve köşe

- Kart iç boşluğu: 16 px. Kart köşe yuvarlağı: 8 px.
- Bölümler arası boşluk: 12 px.
- Form alanları yan yana en fazla 6 olur, sonra alt satıra geçer.

## Yazı

- Başlıklar: 16 px, kalın, lacivert.
- Gövde: varsayılan boyut.
- Türkçe etiketler. Kusur tipleri ve karar adları İngilizce kalır (standart terim).

## Ekran düzeni

- **Muayene:** üstte oturum çubuğu, solda kusur listesi, sağda seçili kusurun detayı ve kararı.
- **Dashboard:** üstte filtre ve yenile, altta parça → operasyon → seri → kusur ağacı.

## Kurallar

1. Karar butonları yalnızca bir kusur seçiliyken etkin olur.
2. Seçili kusurun numarası ekranda her zaman görünür.
3. Açık kusuru olan seriler dashboard'da varsayılan olarak açılır.
4. Tasarım aracı (bulut tabanlı) üretim verisi için kullanılmaz; yalnızca boş örneklerle çalışılır.
