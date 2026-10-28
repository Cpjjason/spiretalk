@echo off
chcp 65001 >nul
if not exist "%~dp0install.ps1" (
  echo 请先把整个压缩包解压出来（右键 - 全部解压缩），再双击解压出来的「点我安装.bat」。
  echo Extract the whole zip first, then run the extracted bat.
  echo.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Src "%~dp0."
echo.
pause
