# MuayeneKayit — Kurulum

## 1. Dosyalar

| Klasör | Nereye |
|---|---|
| `publish\Inspector` | Muayene notebook'ları ve Intel Surface (x64) |
| `publish\Inspector-arm64` | Surface Pro 9 5G (SQ3, ARM64) |
| `publish\Admin` / `publish\Admin-arm64` | Yalnızca admin bilgisayarı |

Klasörü olduğu gibi kopyalayın (ör. `C:\MuayeneKayit\`). Kurulum, yönetici hakkı ve internet gerekmez.
Her klasördeki `MuayeneKayit.settings.json` dosyasına ortak klasör yolunu yazın:

```json
{ "rootPath": "\\\\SUNUCU\\Muayene" }
```

Muayene exe'sindeki **Ayarlar** sekmesi aynı dosyaya `unit` (`"mm"` / `"inch"`) ve `theme` (`"light"` / `"dark"`) alanlarını yazar;
bu ayarlar o bilgisayara özeldir, `rootPath` korunur. Alanlar yoksa mm ve açık tema kullanılır. Elle de yazılabilir:

```json
{ "rootPath": "\\\\SUNUCU\\Muayene", "unit": "inch", "theme": "dark" }
```

Exe'yi ağ paylaşımından çalıştırmak yerine her bilgisayara kopyalayın. Paylaşımdaki exe kilitlenir, güncelleme zorlaşır.
Güncelleme: yeni klasörü kopyalayın. Program kendini güncellemez.

## 2. İlk açılış

1. Admin exe'sini açın. Ortak klasörde PIN yoksa PIN belirleme ekranı gelir (6-12 rakam).
2. **Kusur tipleri** sayfasında tipleri ve her tipin ölçüm alanlarını düzenleyip **Tümünü ortak alana kaydet**.
3. İsterseniz **Karar açıklamaları** sayfasında, muayenecinin karar seçince gördüğü açıklamaları düzenleyip **Ortak alana kaydet**.
4. Muayene exe'leri kusur tiplerini bir sonraki açılışta, karar açıklamalarını bir sonraki karar seçiminde kullanır.

PIN unutulursa: admin, ortak klasördeki `_config\admin.json` dosyasını silip PIN'i yeniden belirler.
Bu yüzden `_config` klasörüne yalnızca admin grubu yazabilmelidir.

## 3. Ağ klasörü izinleri (NTFS)

Örnek gruplar: `MK_Admin`, `MK_Muayene`, `MK_Okuma` (mühendis / kalite, yalnızca okur).

```bat
set ROOT=D:\Paylasim\Muayene

rem Kök: admin tam, muayeneci değiştirir (oturum, fotoğraf, denetim kaydı, index), okuyucu okur
icacls "%ROOT%" /inheritance:r
icacls "%ROOT%" /grant "MK_Admin:(OI)(CI)F" "MK_Muayene:(OI)(CI)M" "MK_Okuma:(OI)(CI)RX" "SYSTEM:(OI)(CI)F"

rem _config: muayeneci yalnızca okur (şablonlar ve PIN özeti)
mkdir "%ROOT%\_config"
icacls "%ROOT%\_config" /inheritance:r
icacls "%ROOT%\_config" /grant "MK_Admin:(OI)(CI)F" "MK_Muayene:(OI)(CI)RX" "MK_Okuma:(OI)(CI)RX" "SYSTEM:(OI)(CI)F"
```

Muayenecinin yalnızca kendi operasyonlarına yazması isteniyorsa: parça / operasyon klasörlerini admin önceden açar,
kökte `MK_Muayene` için `M` yerine yalnızca okuma + kökte dosya oluşturma verilir ve her operasyon klasörüne ilgili grup tanımlanır:

```bat
rem Kökte: alt klasör açamaz; kök dosyalarını (_audit.log, _index.json, *.lock) oluşturup değiştirebilir
icacls "%ROOT%" /remove "MK_Muayene"
icacls "%ROOT%" /grant "MK_Muayene:(OI)(CI)RX" "MK_Muayene:(WD)" "MK_Muayene:(OI)(IO)M"

rem OP40 muayenecileri yalnızca PD170\OP40 altına yazar
icacls "%ROOT%\PD170\OP40" /grant "MK_Muayene_OP40:(OI)(CI)M"
```

Bu ikinci düzende `_config` için kurulan ayrı izinler aynen kalır. İzinleri canlıya almadan önce bir test kullanıcısıyla
muayene açma, fotoğraf ekleme, rapor ve dashboard yenilemeyi deneyin.

Program bu izinleri aşamaz; yazma izni olmayan klasörde "erişim reddedildi" mesajı gösterir.
Denetim zinciri (`_audit.log`) bir dosyanın silindiğini ya da değiştirildiğini gösterir ama engellemez.
Paylaşım için düzenli yedek (gölge kopya ya da gece yedeği) alın.

## 4. Kamera

- Notebook: Logitech C920'yi USB'ye takın. Program önce C920'yi, sonra harici kamerayı seçer; dahili ön kameraya kendiliğinden geçmez.
- Surface Pro 9: arka kamera seçilir.
- Windows: Ayarlar > Gizlilik ve güvenlik > Kamera > "Masaüstü uygulamalarının kameranıza erişmesine izin verin" açık olmalı.

## 5. PDF

Rapor, Windows ile gelen Microsoft Edge ile yerelde PDF'e çevrilir (ağ kullanılmaz).
Edge yoksa ya da çeviri başarısız olursa rapor HTML olarak açılır; tarayıcıdan "PDF olarak kaydet" ile yazdırılabilir.
