$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"
try {
    & "$PSScriptRoot\build.ps1"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing installed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { throw 'BepInEx 5 Mono not installed.' }
    New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
    $target = Join-Path $PluginDir "$ModName.dll"
    Copy-Item -Force -LiteralPath (Join-Path $PSScriptRoot "dist\$ModName.dll") -Destination $target
    Write-Host "[OK] Installed: $target" -ForegroundColor Green
    Write-Host 'Launch game, load a city, F8 -> Rescan. Test on backup save.'
} catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
