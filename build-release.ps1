<#
  Dağıtım paketi hazırlar (tek komut). Visual Studio'da: Görünüm > Terminal (Developer PowerShell) içinde
      .\build-release.ps1
  Adımlar: testler -> başlatıcı + muayene programı yayını (x64 + ARM64; admin paneli içindedir) -> dist\MuayeneKayit-<sürüm>\ ve zip.
  Zip'in içi ağdaki Program klasörüne açılır; program oradan çalışır, kurulum yoktur (bkz. KURULUM.md > Dağıtım):
      MuayeneKayit.exe                           başlatıcı (kısayol buna; admin paneli: sol menü > Admin)
      surum.txt                                  açılacak sürüm
      <sürüm>\Inspector-x64, Inspector-arm64
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
New-Item -ItemType Directory -Force (Join-Path $dist $version) | Out-Null

foreach ($app in @("Inspector")) {
    foreach ($arch in $arches) {
        Write-Host "Yayın: $app $arch" -ForegroundColor Cyan
        dotnet publish "src\MuayeneKayit.$app" -p:PublishProfile=win-$arch
        if ($LASTEXITCODE -ne 0) { throw "Yayın başarısız: $app $arch" }

        $out = Join-Path $root "publish\$app-$arch"
        if (-not (Test-Path (Join-Path $out "MuayeneKayit.$app.exe"))) { throw "Exe bulunamadı: $out" }
        Copy-Item $out (Join-Path $dist "$version\$app-$arch") -Recurse
    }
}

# Başlatıcı: tek dosya. Muayene programını (admin paneli dahil) açar.
Write-Host "Yayın: başlatıcı" -ForegroundColor Cyan
$launcherOut = Join-Path $root "publish\Launcher"
dotnet publish src\MuayeneKayit.Launcher -c Release -o $launcherOut
if ($LASTEXITCODE -ne 0) { throw "Başlatıcı yayını başarısız" }
Copy-Item (Join-Path $launcherOut "MuayeneKayit.exe") (Join-Path $dist "MuayeneKayit.exe")

[IO.File]::WriteAllText((Join-Path $dist "surum.txt"), "$version`r`n", (New-Object Text.UTF8Encoding($false)))
Copy-Item (Join-Path $root "dagitim\OKU-BENI.txt") $dist
Copy-Item (Join-Path $root "src\MuayeneKayit.Inspector\MuayeneKayit.settings.example.json") $dist

$zip = Join-Path $root "dist\$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zip
Write-Host ("Hazır: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
