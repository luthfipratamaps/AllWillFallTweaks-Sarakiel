$ErrorActionPreference='Stop'
& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Build failed; installation aborted.' }
. "$PSScriptRoot\config.ps1"
New-Item -ItemType Directory -Force -Path $PluginsDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot 'dist\AllWillFallTweaks.dll') (Join-Path $PluginsDir 'AllWillFallTweaks.dll')
Write-Host "[INSTALLED] $PluginsDir\AllWillFallTweaks.dll"
