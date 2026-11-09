# MuayeneKayit

Motor parçası görsel muayene kayıt sistemi: tek program; yönetim (Admin) paneli sol menüden PIN ile açılır.
WinUI 3, .NET 9. Veritabanı ve internet gerekmez; kayıtlar ortak bir ağ klasöründe tutulur.

## Hızlı başlangıç

**Yalnızca kullanmak / dağıtmak (derleme yok)** — Releases sayfasındaki `MuayeneKayit-<sürüm>.zip`'i indirin,
içeriğini ağdaki program klasörüne açın ve içindeki `OKU-BENI.txt`'yi izleyin. Kullanıcılar ağdaki `MuayeneKayit.exe`'nin
masaüstü kısayoluyla açar; kurulum ve yönetici yetkisi gerekmez.

**Visual Studio'da derlemek**
1. Visual Studio 2022 (17.12+) iş yükleri: *.NET masaüstü geliştirme* ve *WinUI uygulama geliştirme*.
2. *Depoyu kopyala* → `MuayeneKayit.sln` → üst çubukta **Release | x64** → **Derle**. F5 muayene programını açar.
   Paketler ilk derlemede nuget.org'dan iner. İnmiyorsa: `KURULUM.md` > *Paketler inmiyorsa*.
3. Dağıtım paketi: **Görünüm > Terminal** içinde `.\build-release.ps1` → `dist\MuayeneKayit-<sürüm>.zip`.

Ayrıntılar: [KURULUM.md](KURULUM.md) (derleme, dağıtım, güncelleme, ağ izinleri, kamera).
Tasarım ve gereksinimler: [PLAN.md](PLAN.md), [DESIGN.md](DESIGN.md).

## Proje yapısı

| Klasör | İçerik |
|---|---|
| `src/MuayeneKayit.Core` | Alan modeli, kayıt (JSON + dosya kilidi), denetim zinciri, rapor |
| `src/MuayeneKayit.Presentation` | ViewModel'ler (WinUI'den bağımsız, test edilir) |
| `src/MuayeneKayit.Inspector` | Program (WinUI 3); `Admin\` klasöründe PIN korumalı admin penceresi |
| `src/MuayeneKayit.Launcher` | Ağ klasöründeki başlatıcı (`MuayeneKayit.exe`) |
| `tests/MuayeneKayit.Tests` | Testler: `dotnet run --project tests\MuayeneKayit.Tests` |
