@echo off
setlocal
rem One-click Workshop upload. Double-click this file.
rem First run: steamcmd asks for your Steam password and Steam Guard code in this window
rem (it never shows the password). After that the login is cached and it just uploads.

set "HERE=%~dp0"
set "STEAMCMD=%HERE%..\..\tools\steamcmd\steamcmd.exe"
set "VDF=%HERE%workshop_item.vdf"
set "USERFILE=%APPDATA%\SlayTheSpire2\spiretalk-workshop-user.txt"

if not exist "%STEAMCMD%" (
  echo steamcmd not found at %STEAMCMD%
  pause
  exit /b 1
)
if not exist "%HERE%content\SpireTalk.dll" (
  echo Nothing staged. Run "dotnet publish -c Release" and then workshop\stage.ps1 first.
  pause
  exit /b 1
)

if exist "%USERFILE%" (
  set /p STEAMUSER=<"%USERFILE%"
) else (
  set /p STEAMUSER=Your Steam login name (not the display name):
  > "%USERFILE%" echo %STEAMUSER%
)

echo Uploading as %STEAMUSER% ...
"%STEAMCMD%" +login %STEAMUSER% +workshop_build_item "%VDF%" +quit
echo.
echo Done. If it says "Success", the item id was written into workshop_item.vdf.
echo Manage it at https://steamcommunity.com/app/2868840/workshop/  (your profile - Workshop items)
pause
