@echo off
setlocal
rem One-click Workshop upload. Double-click this file.
rem First run: steamcmd asks for your Steam password and Steam Guard code in this window.
rem After that the login is cached and it just uploads.

set "HERE=%~dp0"
set "STEAMCMD=%HERE%..\..\tools\steamcmd\steamcmd.exe"
set "VDF=%HERE%workshop_item.vdf"
set "USERFILE=%APPDATA%\SlayTheSpire2\spiretalk-workshop-user.txt"

if not exist "%STEAMCMD%" goto nosteamcmd
if not exist "%HERE%content\SpireTalk.dll" goto nostage
if exist "%USERFILE%" goto haveuser

set /p STEAMUSER=Steam login name, the one you type to log in:
if "%STEAMUSER%"=="" goto nouser
> "%USERFILE%" echo %STEAMUSER%
goto upload

:haveuser
set /p STEAMUSER=<"%USERFILE%"

:upload
echo Uploading as %STEAMUSER% ...
echo.
"%STEAMCMD%" +login %STEAMUSER% +workshop_build_item "%VDF%" +quit
echo.
echo Finished. "Success" above means the item is on the Workshop, hidden, under your account.
echo Manage it at https://steamcommunity.com/app/2868840/workshop/ - your avatar - Workshop items
echo.
pause
exit /b 0

:nosteamcmd
echo steamcmd not found at %STEAMCMD%
pause
exit /b 1

:nostage
echo Nothing staged. Run dotnet publish -c Release and then workshop\stage.ps1 first.
pause
exit /b 1

:nouser
echo No login name given.
pause
exit /b 1
