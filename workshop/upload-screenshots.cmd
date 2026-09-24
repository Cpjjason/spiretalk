@echo off
setlocal
rem Replaces the Workshop item's gallery with every image in workshop\screenshots\ (sorted by name).
rem Goes through the Steam client you are logged into, so Steam must be running and online.

set "HERE=%~dp0"
set "TOOL=%HERE%..\tools\WorkshopPreviews\bin\Release\net9.0\WorkshopPreviews.exe"
set "SHOTS=%HERE%screenshots"

if exist "%TOOL%" goto run
echo Building the uploader once...
dotnet build "%HERE%..\tools\WorkshopPreviews" -c Release -nologo -v q
if not exist "%TOOL%" goto nobuild

:run
"%TOOL%" 3806015083 "%SHOTS%" --replace --note "screenshots"
echo.
pause
exit /b 0

:nobuild
echo Could not build tools\WorkshopPreviews (needs the .NET 9 SDK).
pause
exit /b 1
