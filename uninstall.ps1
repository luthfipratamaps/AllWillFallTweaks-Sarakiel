$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config.ps1')
if ([string]::IsNullOrWhiteSpace($PluginsDir)) { throw 'PluginsDir is missing. Check config.ps1.' }
$target = Join-Path $PluginsDir 'AllWillFallTweaks.dll'
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target; Write-Host "[REMOVED] $target" }
else { Write-Host 'Plugin not installed.' }
Write-Host 'Existing configuration preserved.'
