@echo off
rem SpireTalk installer launcher. Pure ASCII + CRLF on purpose: cmd misreads UTF-8 batch files
rem (see 10/29 in the Jianghu Yehua notes); the Chinese hint is printed by PowerShell.
if not exist "%~dp0install.ps1" goto missing
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Src "%~dp0."
echo.
pause
exit /b 0
:missing
powershell -NoProfile -Command "[Console]::OutputEncoding=[Text.Encoding]::UTF8; Write-Host ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('6K+35YWI5oqK5pW05Liq5Y6L57yp5YyF6Kej5Y6L5Ye65p2l77yI5Y+z6ZSuIC0g5YWo6YOo6Kej5Y6L57yp77yJ77yM5YaN5Y+M5Ye76Kej5Y6L5Ye65p2l55qE44CM54K55oiR5a6J6KOFLmJhdOOAjeOAgg==')))"
echo Extract the whole zip first, then run the extracted bat.
echo.
pause
exit /b 1
