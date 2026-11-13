$ErrorActionPreference='Stop'
& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Build failed; release aborted.' }
. "$PSScriptRoot\config.ps1"
$archive=Join-Path $PSScriptRoot ("dist\AllWillFallTweaks-v"+$ModVersion+".zip")
if (Test-Path $archive) { Remove-Item $archive }
Compress-Archive -Path (Join-Path $PSScriptRoot 'dist\AllWillFallTweaks.dll') -DestinationPath $archive
Write-Host "[RELEASE] $archive"
