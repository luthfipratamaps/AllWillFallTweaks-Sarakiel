$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config.ps1')
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed; release aborted.' }
$builtDll = Join-Path $PSScriptRoot 'dist\AllWillFallTweaks.dll'
if (-not (Test-Path -LiteralPath $builtDll)) { throw "Built DLL missing: $builtDll" }
$archive = Join-Path $PSScriptRoot ("dist\AllWillFallTweaks-v$ModVersion.zip")
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
Compress-Archive -LiteralPath $builtDll -DestinationPath $archive
Write-Host "[RELEASE] $archive" -ForegroundColor Green
