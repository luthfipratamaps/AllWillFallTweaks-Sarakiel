$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\config.ps1"
try {
    $target = Join-Path $PluginDir "$ModName.dll"
    if (Test-Path -LiteralPath $target) {
        Remove-Item -Force -LiteralPath $target
        Write-Host "[OK] Removed: $target" -ForegroundColor Green
    } else { Write-Host "Not installed: $target" }
} catch { Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
