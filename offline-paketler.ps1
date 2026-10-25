<#
  İnternete (nuget.org) erişemeyen bilgisayarda derleme için NuGet paketleri.

  İnternetli bilgisayarda (paketleri toplar):
      .\offline-paketler.ps1 -Hazirla
      -> dist\MuayeneKayit-paketler-offline.zip  (GitHub Release'e eklenir)

  İnternetsiz bilgisayarda (zip'i depo köküne indirdikten sonra):
      .\offline-paketler.ps1 -Kullan -Zip .\MuayeneKayit-paketler-offline.zip
      -> paketler packages-offline\ klasörüne açılır ve nuget.config'e yerel kaynak olarak eklenir.
         Sonra Visual Studio'da çözümü açıp derleyin; paketler bu klasörden alınır.
#>
param(
    [switch]$Hazirla,
    [switch]$Kullan,
    [string]$Zip = ".\MuayeneKayit-paketler-offline.zip"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root
$feed = Join-Path $root "packages-offline"

if ($Hazirla) {
    # Boş bir klasöre restore: yalnızca bu projelerin gerçekten kullandığı paketler (x64 + ARM64 çalışma zamanı dahil) iner.
    $temp = Join-Path $env:TEMP ("mk-offline-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force $temp | Out-Null
    try {
        foreach ($project in "src\MuayeneKayit.Inspector", "src\MuayeneKayit.Admin", "tests\MuayeneKayit.Tests") {
            Write-Host "Restore: $project" -ForegroundColor Cyan
            dotnet restore $project --packages $temp
            if ($LASTEXITCODE -ne 0) { throw "Restore başarısız: $project" }
            if ($project -like "src\*") {
                foreach ($rid in "win-x64", "win-arm64") {
                    dotnet restore $project --packages $temp -r $rid
                    if ($LASTEXITCODE -ne 0) { throw "Restore başarısız: $project $rid" }
                }
            }
        }

        # Düz klasör yeterli: NuGet, içinde .nupkg dosyaları olan klasörü kaynak olarak kullanır.
        if (Test-Path $feed) { Remove-Item $feed -Recurse -Force }
        New-Item -ItemType Directory -Force $feed | Out-Null
        Get-ChildItem $temp -Recurse -Filter *.nupkg | Copy-Item -Destination $feed
        $count = (Get-ChildItem $feed -Filter *.nupkg).Count

        New-Item -ItemType Directory -Force (Join-Path $root "dist") | Out-Null
        $out = Join-Path $root "dist\MuayeneKayit-paketler-offline.zip"
        if (Test-Path $out) { Remove-Item $out -Force }
        Compress-Archive -Path (Join-Path $feed "*") -DestinationPath $out
        Write-Host ("Hazır: {0} ({1} paket, {2:N0} MB)" -f $out, $count, ((Get-Item $out).Length / 1MB)) -ForegroundColor Green
    }
    finally {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
elseif ($Kullan) {
    if (-not (Test-Path $Zip)) { throw "Zip bulunamadı: $Zip" }
    if (Test-Path $feed) { Remove-Item $feed -Recurse -Force }
    Expand-Archive -Path $Zip -DestinationPath $feed
    $existing = dotnet nuget list source --configfile nuget.config | Out-String
    if ($existing -notmatch "packages-offline") {
        dotnet nuget add source $feed --name yerel-paketler --configfile nuget.config
    }
    # Ulaşılamayan nuget.org açık kalırsa restore hata verir (NU1301): kapatılır. Geri açmak için:
    #   dotnet nuget enable source nuget.org --configfile nuget.config
    dotnet nuget disable source nuget.org --configfile nuget.config
    Write-Host "Yerel paket kaynağı eklendi: $feed" -ForegroundColor Green
    Write-Host "Visual Studio'da çözümü açın (açıksa kapatıp açın) ve derleyin." -ForegroundColor Green
}
else {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
}
