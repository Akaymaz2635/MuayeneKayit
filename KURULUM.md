# MuayeneKayit — Kurulum

## 0. İş bilgisayarında derleme (Visual Studio)

Hazır paket gerekiyorsa derlemeye gerek yok: GitHub deposunun **Releases** sayfasındaki `MuayeneKayit-<sürüm>.zip`
doğrudan dağıtılabilir (bölüm 1). Kodu değiştirip yeniden derlemek için:

**Bir kez kurulacaklar** — Visual Studio 2022 (17.12 ya da yeni; Community / Professional / Enterprise fark etmez).
Visual Studio Installer > Değiştir > iş yükleri:
- **.NET masaüstü geliştirme** (.NET Desktop Development)
- **WinUI uygulama geliştirme** (WinUI application development; eski adı "Windows uygulama geliştirme").
  Sağdaki ayrıntılarda **Windows App SDK C# Templates** seçili olsun.

.NET 9 SDK bu iş yükleriyle gelir; ayrıca .NET, Windows App SDK ya da NuGet paketi kurmanız gerekmez.

**Derleme**
1. Visual Studio > **Depoyu kopyala** (Clone a repository) > GitHub adresi > Kopyala.
2. `MuayeneKayit.sln` açılır. Üst çubukta **Release** ve **x64** seçin, **Derle > Çözümü Derle**.
   İlk derlemede paketler (~240 MB) nuget.org'dan kendiliğinden iner.
3. Yayın: Çözüm Gezgini'nde `MuayeneKayit.Inspector` > sağ tık > **Yayımla** > profil `win-x64` > **Yayımla**.
   Çıktı: depo kökünde `publish\Inspector-x64\`. Admin için aynısı `MuayeneKayit.Admin` ile.
   Tek komutla tüm paket: **Görünüm > Terminal** içinde `.\build-release.ps1` → `dist\MuayeneKayit-<sürüm>.zip`
   (testler + başlatıcı + Inspector / Admin, x64 + ARM64). Yalnızca x64 için: `.\build-release.ps1 -X64Only`.

**Paketler inmiyorsa** (nuget.org'a erişim yok / proxy): Releases sayfasından `MuayeneKayit-paketler-offline.zip`'i depo
köküne indirin, terminalde
```powershell
.\offline-paketler.ps1 -Kullan -Zip .\MuayeneKayit-paketler-offline.zip
```
çalıştırın, Visual Studio'yu kapatıp açın ve derleyin. Paketler internet yerine `packages-offline\` klasöründen alınır.

**Sürüm numarası** `Directory.Build.props` > `<Version>` içindedir; yeni dağıtımdan önce artırın. Pencere başlığında görünür.

## 1. Dağıtım (ağ klasöründen çalıştırma, kurulum yok)

Kullanıcı bilgisayarına hiçbir şey kurulmaz ve kopyalanmaz, yönetici yetkisi gerekmez. Program ağdaki bir **program**
klasöründen çalışır. Paket (`MuayeneKayit-<sürüm>.zip`) içeriği olduğu gibi bu klasöre açılır, örneğin `\\SUNUCU\MuayeneKayit\Program\`:

| Dosya / klasör | Ne işe yarar |
|---|---|
| `MuayeneKayit.exe` | Başlatıcı (12 MB): `surum.txt`'deki sürümün muayene programını açar. Kullanıcılar bunu açar. |
| `MuayeneKayit Admin.exe` | Aynı başlatıcı; admin programını açar (yalnızca yöneticiler). |
| `surum.txt` | Açılacak sürüm (ör. `1.0.0`). |
| `1.0.0\Inspector-x64`, `Inspector-arm64`, `Admin-x64`, `Admin-arm64` | Sürüm klasörü. Surface Pro 9 5G (SQ3) ARM64 sürümünü kendiliğinden açar. |
| `MuayeneKayit.settings.example.json` | Ortak ayar örneği |
| `OKU-BENI.txt` | Bu bölümün kısa hâli |

1. Örnekten aynı klasörde `MuayeneKayit.settings.json` oluşturup ortak **veri** klasörünü yazın (program klasörü değil):
   ```json
   { "rootPath": "\\\\SUNUCU\\MuayeneKayit\\Veri" }
   ```
   Sürüm klasörlerindeki programlar bu tek dosyayı kullanır (exe'nin klasöründe yoksa üst klasörlere bakar).
2. **Kullanıcılar:** ağdaki `MuayeneKayit.exe` > sağ tık > *Gönder > Masaüstü (kısayol oluştur)*. Kısayolu BT grup ilkesiyle de dağıtabilir.
3. **Güncelleme** (program açıkken de yapılabilir): yeni paketteki sürüm klasörünü (ör. `1.0.1`) program klasörüne kopyalayın,
   sonra `surum.txt`'yi `1.0.1` yapın. Kullanıcılar bir sonraki açışta yeni sürümü açar; açık programlar eski klasörden
   çalışmaya devam eder. Eski sürüm klasörü herkes kapattıktan sonra silinebilir. Başlatıcılara ve
   `MuayeneKayit.settings.json`'a dokunmayın.

İzinler: kullanıcılara program klasöründe yalnızca **okuma**, veri klasöründe **okuma + yazma** (bölüm 3).
Program ~210 MB olduğu için ağdan ilk açılış birkaç saniye sürebilir (Wi-Fi'da daha uzun). Şirketin uygulama kısıtlamaları
(AppLocker vb.) ağdaki exe'leri engelliyorsa BT'den program klasörü için izin istenmelidir.

Kişisel ayarlar (Ayarlar sekmesi: ölçü birimi, tema; son kullanılan kamera) her kullanıcının kendi Windows profilinde,
`%LOCALAPPDATA%\MuayeneKayit` altında saklanır; program klasörüne yazılmaz.
**Inspector kimliği:** Admin > Ayarlar > *Inspector kimliği* açılırsa Inspector adı elle yazılmaz; Windows'a giriş yapan
kullanıcıdan alınır (Active Directory'deki ad + sicil no, ör. `Ali Kaymaz (12345)`). Tüm bilgisayarlara uygulanır.
Parça / operasyon / seri öneri listesinde "×" ile gizlenen öneriler de bu klasörde, `hidden-suggestions.json` dosyasında tutulur (yalnızca o bilgisayar).

## 2. İlk açılış

1. Admin exe'sini açın. Ortak klasörde PIN yoksa PIN belirleme ekranı gelir (6-12 rakam).
2. **Kusur tipleri** sayfasında tipleri ve her tipin ölçüm alanlarını düzenleyip **Tümünü ortak alana kaydet**.
3. İsterseniz **Karar açıklamaları** sayfasında, Inspector'ın karar seçince gördüğü açıklamaları düzenleyip **Ortak alana kaydet**.
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

## 5. Lot parça numaraları (isteğe bağlı)

Inspector'da her kusurun Ölçüm sekmesinde **Etkilenen parçalar** kutusu vardır (örn. `1-5, 8`; boş = tüm parça). Kutudan çıkınca otomatik kaydedilir, geçersiz girdi kaydedilmez. Değer açıklama cümlesinde ("on parts 1–5 and 8"), raporda ve CSV'de ("Etkilenen parçalar" sütunu) görünür. Eski kayıtlar etkilenmez.

## 6. PDF

Rapor, Windows ile gelen Microsoft Edge ile yerelde PDF'e çevrilir (ağ kullanılmaz).
Edge yoksa ya da çeviri başarısız olursa rapor HTML olarak açılır; tarayıcıdan "PDF olarak kaydet" ile yazdırılabilir.
