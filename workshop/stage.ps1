# Stage the Workshop item: copies the built mod (dll + manifest + pck) and the README
# into workshop\content\, ready for `steamcmd +workshop_build_item workshop_item.vdf`.
#
#   1. dotnet publish -c Release            (from the repo root)
#   2. .\workshop\stage.ps1
#   3. steamcmd +login <steam user> +workshop_build_item "<abs path>\workshop\workshop_item.vdf" +quit
#
# First upload: leave publishedfileid at 0 in workshop_item.vdf; steamcmd writes the new id
# back into the file. Later uploads reuse that id.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$mods = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Slay the Spire 2\mods\SpireTalk"
$content = Join-Path $PSScriptRoot "content"

if (-not (Test-Path (Join-Path $mods "SpireTalk.dll"))) { throw "Build first: dotnet publish -c Release (missing $mods\SpireTalk.dll)" }
if (-not (Test-Path (Join-Path $mods "SpireTalk.pck"))) { throw "Missing SpireTalk.pck - delete the old pck and run dotnet publish again" }

if (Test-Path $content) { Remove-Item -Recurse -Force $content }
New-Item -ItemType Directory -Force $content | Out-Null

Copy-Item (Join-Path $mods "SpireTalk.dll") $content
Copy-Item (Join-Path $mods "SpireTalk.pck") $content
Copy-Item (Join-Path $root "SpireTalk.json") $content
Copy-Item (Join-Path $root "README.md") $content

# Never ship the developer bridge switch: it only arms from a flag file in the player's
# AppData, but make sure nothing in the content folder could look like one.
Get-ChildItem $content -Recurse | Where-Object { $_.Name -like "*bridge*" } | Remove-Item -Force

$manifest = Get-Content (Join-Path $root "SpireTalk.json") | ConvertFrom-Json
Write-Host "Staged SpireTalk $($manifest.version) in $content"
Get-ChildItem $content | ForEach-Object { Write-Host ("  {0,-16} {1,10:N0} bytes" -f $_.Name, $_.Length) }
