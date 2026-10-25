<#
  Dağıtım paketi hazırlar (tek komut). Visual Studio'da: Görünüm > Terminal (Developer PowerShell) içinde
      .\build-release.ps1
  Adımlar: testler -> Inspector ve Admin yayını (x64 + ARM64) -> dist\MuayeneKayit-<sürüm>\ klasörü ve zip.
  Zip'in içi olduğu gibi ağ klasörüne kopyalanır (bkz. KURULUM.md > Dağıtım).
  -SkipTests: testleri atlar.  -X64Only: yalnızca x64 (Surface / ARM64 yoksa daha hızlı).
#>
param(
    [switch]$SkipTests,
    [switch]$X64Only
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$name = "MuayeneKayit-$version"
$dist = Join-Path $root "dist\$name"
Write-Host "Sürüm $version hazırlanıyor -> $dist" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "Testler çalışıyor..." -ForegroundColor Cyan
    dotnet run --project tests\MuayeneKayit.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw "Testler geçmedi; paket hazırlanmadı." }
}

$arches = if ($X64Only) { @("x64") } else { @("x64", "arm64") }
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($app in "Inspector", "Admin") {
    foreach ($arch in $arches) {
        Write-Host "Yayın: $app $arch" -ForegroundColor Cyan
        dotnet publish "src\MuayeneKayit.$app" -p:PublishProfile=win-$arch
        if ($LASTEXITCODE -ne 0) { throw "Yayın başarısız: $app $arch" }

        $out = Join-Path $root "publish\$app-$arch"
        if (-not (Test-Path (Join-Path $out "MuayeneKayit.$app.exe"))) { throw "Exe bulunamadı: $out" }
        Copy-Item $out (Join-Path $dist "$app-$arch") -Recurse
    }
}

# Dağıtım dosyaları: kurulum / başlatma betikleri ve ortak ayar örneği.
Copy-Item (Join-Path $root "dagitim\Kur.cmd"), (Join-Path $root "dagitim\Baslat.cmd"), (Join-Path $root "dagitim\OKU-BENI.txt") $dist
Copy-Item (Join-Path $root "src\MuayeneKayit.Inspector\MuayeneKayit.settings.example.json") $dist

$zip = Join-Path $root "dist\$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zip
Write-Host ("Hazır: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
