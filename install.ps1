$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config.ps1')
if ([string]::IsNullOrWhiteSpace($PluginsDir)) { throw 'PluginsDir is missing. Check config.ps1.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) {
    throw "BepInEx installation not found under: $GameDir"
}
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed; installation aborted.' }
$builtDll = Join-Path $PSScriptRoot 'dist\AllWillFallTweaks.dll'
if (-not (Test-Path -LiteralPath $builtDll)) { throw "Built DLL missing: $builtDll" }
New-Item -ItemType Directory -Force -Path $PluginsDir | Out-Null
$target = Join-Path $PluginsDir 'AllWillFallTweaks.dll'
Copy-Item -LiteralPath $builtDll -Destination $target -Force
Write-Host "[INSTALLED] $target" -ForegroundColor Green
