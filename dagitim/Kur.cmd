@echo off
rem ---------------------------------------------------------------------------
rem  Muayene Kayit - kurulum. Ag klasorunden BIR KEZ calistirilir (yonetici yetkisi gerekmez).
rem  Programi bu bilgisayara (%LOCALAPPDATA%\Programs\MuayeneKayit) kopyalar ve masaustune kisayol koyar.
rem  Sonraki acilislarda kisayol ag klasorundeki yeni surumu kendisi alir.
rem  Kullanim: Kur.cmd [Inspector|Admin]   (varsayilan: Inspector; Admin yalnizca yoneticiler icin)
rem ---------------------------------------------------------------------------
setlocal
set "UYGULAMA=%~1"
if "%UYGULAMA%"=="" set "UYGULAMA=Inspector"
set "KAYNAK=%~dp0"
set "HEDEF=%LOCALAPPDATA%\Programs\MuayeneKayit"

if not exist "%HEDEF%" mkdir "%HEDEF%"
>"%HEDEF%\kaynak.txt" echo %KAYNAK%
copy /y "%KAYNAK%Baslat.cmd" "%HEDEF%\Baslat.cmd" >nul

set "AD=Muayene Kayit"
if /i "%UYGULAMA%"=="Admin" set "AD=Muayene Kayit Admin"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop') + '\%AD%.lnk');" ^
  "$s.TargetPath = '%HEDEF%\Baslat.cmd'; $s.Arguments = '%UYGULAMA%'; $s.WorkingDirectory = '%HEDEF%';" ^
  "$s.WindowStyle = 7; $s.IconLocation = '%HEDEF%\%UYGULAMA%\MuayeneKayit.%UYGULAMA%.exe,0'; $s.Save()"

echo Program kopyalaniyor, bu ilk seferde bir-iki dakika surebilir...
call "%HEDEF%\Baslat.cmd" %UYGULAMA%
echo Kurulum tamamlandi. Masaustundeki "%AD%" kisayolunu kullanin.
timeout /t 5 >nul
