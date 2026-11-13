$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"
try {
    & "$PSScriptRoot\build.ps1"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; no release created.' }
    $releases = Join-Path $PSScriptRoot 'releases'
    New-Item -Force -ItemType Directory -Path $releases | Out-Null
    $zip = Join-Path $releases "$ModName-v$Version.zip"
    $staging = Join-Path $PSScriptRoot 'dist\release-staging'
    if (Test-Path -LiteralPath $staging) { Remove-Item -Recurse -Force -LiteralPath $staging }
    $dest = Join-Path $staging 'BepInEx\plugins'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "dist\$ModName.dll") -Destination (Join-Path $dest "$ModName.dll")
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $staging
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
    Write-Host "[OK] Release created: $zip" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
