# SpireTalk installer (点我安装.bat runs this). Finds Slay the Spire 2 (this folder, Steam's
# libraries, or asks), then copies mods\SpireTalk and, when the game has none or an older one,
# mods\BaseLib into the game's mods folder. Windows PowerShell 5.1; this file is UTF-8 with BOM.
param([string]$Src = (Split-Path -Parent $MyInvocation.MyCommand.Path))

$ErrorActionPreference = 'Stop'
function Say([string]$zh, [string]$en) { Write-Host $zh; Write-Host "  $en" -ForegroundColor DarkGray }

$exeName = 'SlayTheSpire2.exe'
$from = Join-Path $Src 'mods'
if (-not (Test-Path (Join-Path $from 'SpireTalk\SpireTalk.dll'))) {
  Say '找不到 mods\SpireTalk。请先把整个压缩包解压出来，再双击解压出来的「点我安装.bat」。' 'mods\SpireTalk is missing. Extract the whole zip first, then run the extracted .bat.'
  exit 1
}

function Find-Game {
  # 1. unzipped straight into the game folder (or one level below it)
  foreach ($d in @($Src, (Split-Path -Parent $Src))) {
    if ($d -and (Test-Path (Join-Path $d $exeName))) { return $d }
  }
  # 2. every Steam library listed in libraryfolders.vdf
  $steam = $null
  foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
    try {
      $p = Get-ItemProperty -Path $key -ErrorAction Stop
      if ($p.SteamPath) { $steam = $p.SteamPath; break }
      if ($p.InstallPath) { $steam = $p.InstallPath; break }
    } catch {}
  }
  $libs = @()
  if ($steam) {
    $libs += $steam
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
      foreach ($m in [regex]::Matches((Get-Content -Raw -LiteralPath $vdf), '"path"\s+"([^"]+)"')) {
        $libs += $m.Groups[1].Value.Replace('\\', '\')
      }
    }
  }
  $libs += 'C:\Program Files (x86)\Steam', 'C:\Program Files\Steam', 'D:\SteamLibrary', 'E:\SteamLibrary', 'D:\Steam', 'E:\Steam'
  foreach ($lib in ($libs | Select-Object -Unique)) {
    $d = Join-Path $lib 'steamapps\common\Slay the Spire 2'
    if (Test-Path (Join-Path $d $exeName)) { return $d }
  }
  # 3. ask
  Say '没自动找到游戏。请在弹出的窗口里选《杀戮尖塔 2》的游戏目录（里面有 SlayTheSpire2.exe）。' 'Could not find the game. Pick the Slay the Spire 2 folder (the one with SlayTheSpire2.exe).'
  Add-Type -AssemblyName System.Windows.Forms
  $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
  $dlg.Description = 'Slay the Spire 2 (SlayTheSpire2.exe)'
  if ($dlg.ShowDialog() -eq 'OK' -and (Test-Path (Join-Path $dlg.SelectedPath $exeName))) { return $dlg.SelectedPath }
  return $null
}

function Version-Of([string]$json) {
  try { return [version]((Get-Content -Raw -LiteralPath $json | ConvertFrom-Json).version.TrimStart('v')) } catch { return [version]'0.0' }
}

$game = Find-Game
if (-not $game) { Say '没有找到游戏，安装取消。' 'Game not found; nothing was installed.'; exit 1 }
Say "游戏目录：$game" "Game folder: $game"

while (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
  Say '游戏正在运行，请先退出游戏，退出后这里会自动继续……' 'The game is running. Quit it and this continues on its own…'
  Start-Sleep -Seconds 3
}

$mods = Join-Path $game 'mods'
New-Item -ItemType Directory -Force -Path $mods | Out-Null

# SpireTalk: replace the folder (old files from earlier versions go away; NPC memories live in %APPDATA%)
$dst = Join-Path $mods 'SpireTalk'
if (Test-Path $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $from 'SpireTalk') -Destination $dst -Recurse -Force
$ver = (Get-Content -Raw -LiteralPath (Join-Path $dst 'SpireTalk.json') | ConvertFrom-Json).version
Say "已装好 SpireTalk $ver" "SpireTalk $ver installed"

# BaseLib: only when the game has none, or an older one (another mod may need the newer copy)
$ourBase = Join-Path $from 'BaseLib'
$theirBase = Join-Path $mods 'BaseLib'
if (Test-Path $ourBase) {
  $ours = Version-Of (Join-Path $ourBase 'BaseLib.json')
  $theirs = if (Test-Path (Join-Path $theirBase 'BaseLib.json')) { Version-Of (Join-Path $theirBase 'BaseLib.json') } else { [version]'0.0' }
  if ($ours -gt $theirs) {
    if (Test-Path $theirBase) { Remove-Item -LiteralPath $theirBase -Recurse -Force }
    Copy-Item -LiteralPath $ourBase -Destination $theirBase -Recurse -Force
    Say "已装好前置 BaseLib $ours" "BaseLib $ours installed"
  } else {
    Say "游戏里已有 BaseLib $theirs，不用换" "BaseLib $theirs already there"
  }
}

Write-Host ''
Say '完成！启动游戏，选「带模组游玩」，主菜单右上角的蘑菇就是 SpireTalk。' 'Done! Start the game, choose Play Modded; the mushie at the top right of the main menu is SpireTalk.'
Say '第一次打开会问你怎么和先古之民说话，选「用 Yumina 账号」，浏览器里登录一下就能聊。' 'On first launch pick "Use a Yumina account" and sign in once in the browser to start talking.'
