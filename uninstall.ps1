$ErrorActionPreference='Stop'
. "$PSScriptRoot\config.ps1"
$target=Join-Path $PluginsDir 'AllWillFallTweaks.dll'
if(Test-Path $target) { Remove-Item $target; Write-Host "[REMOVED] $target" }
else { Write-Host 'Plugin not installed.' }
Write-Host 'Existing configuration preserved.'
