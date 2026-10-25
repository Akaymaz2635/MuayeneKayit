@echo off
rem ---------------------------------------------------------------------------
rem  Muayene Kayit - baslatici. Masaustundeki kisayol bunu calistirir.
rem  1) Ag klasorundeki surumle yerel kopyayi esitler (yalnizca degisen dosyalar).
rem  2) Ortak ayari (MuayeneKayit.settings.json) ag klasorunden alir.
rem  3) Programi yerel kopyadan acar. Ag yoksa yerel kopya yine acilir.
rem  Kullanim: Baslat.cmd [Inspector|Admin]   (varsayilan: Inspector)
rem ---------------------------------------------------------------------------
setlocal
set "UYGULAMA=%~1"
if "%UYGULAMA%"=="" set "UYGULAMA=Inspector"
set "HEDEF=%~dp0"
set "KAYNAK="
if exist "%HEDEF%kaynak.txt" set /p KAYNAK=<"%HEDEF%kaynak.txt"

set "MIMARI=x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "MIMARI=arm64"

set "EXE=%HEDEF%%UYGULAMA%\MuayeneKayit.%UYGULAMA%.exe"

if defined KAYNAK if exist "%KAYNAK%%UYGULAMA%-%MIMARI%\MuayeneKayit.%UYGULAMA%.exe" (
    robocopy "%KAYNAK%%UYGULAMA%-%MIMARI%" "%HEDEF%%UYGULAMA%" /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
    if exist "%KAYNAK%MuayeneKayit.settings.json" copy /y "%KAYNAK%MuayeneKayit.settings.json" "%HEDEF%%UYGULAMA%\" >nul
)

if not exist "%EXE%" (
    echo Program bulunamadi: %EXE%
    echo Ag klasorune ulasilamiyor olabilir. Baglantiyi kontrol edip tekrar deneyin.
    pause
    exit /b 1
)

start "" "%EXE%"
