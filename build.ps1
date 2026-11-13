$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. "$PSScriptRoot\config.ps1"
try {
    $managed = Join-Path $GameDir 'All Will Fall_Data\Managed'
    $bep = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'
    $scriptDll = Join-Path $managed 'ScriptsAssembly.dll'
    if (-not (Test-Path -LiteralPath $scriptDll)) { throw "Game assembly missing: $scriptDll" }
    if (-not (Test-Path -LiteralPath $bep)) { throw "BepInEx 5 Mono missing: $bep" }

    $compilers = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    $csc = $compilers | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $csc) { throw 'C# compiler not found. Install .NET Framework developer tools.' }

    $dist = Join-Path $PSScriptRoot 'dist'
    New-Item -Force -ItemType Directory -Path $dist | Out-Null
    $out = Join-Path $dist "$ModName.dll"
    $source = Join-Path $PSScriptRoot 'src\AllWillFallTweaks.cs'
    if (-not (Test-Path -LiteralPath $source)) { throw "Source missing: $source" }
    $harmony = Join-Path $GameDir 'BepInEx\core\0Harmony.dll'
    if (-not (Test-Path -LiteralPath $harmony)) { throw "Harmony library missing: $harmony" }
    $refFiles = @($bep, $harmony) + @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' -File | ForEach-Object { $_.FullName })
    if ($refFiles.Count -lt 2) { throw "No UnityEngine assemblies found in $managed" }

    # Unity's assemblies target .NET Standard 2.1. The legacy Framework compiler
    # does not automatically reference netstandard.dll, so locate the GAME's facade.
    $netstandardCandidates = @(
        (Join-Path $managed 'netstandard.dll'),
        (Join-Path $GameDir 'All Will Fall_Data\MonoBleedingEdge\lib\mono\4.5\Facades\netstandard.dll'),
        (Join-Path $GameDir 'All Will Fall_Data\MonoBleedingEdge\lib\mono\4.7.1-api\Facades\netstandard.dll'),
        (Join-Path $GameDir 'All Will Fall_Data\MonoBleedingEdge\lib\mono\4.8-api\Facades\netstandard.dll')
    )
    $netstandard = $netstandardCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $netstandard) {
        # Unity versions vary in where the managed facades live.
        $monoRoot = Join-Path $GameDir 'All Will Fall_Data\MonoBleedingEdge\lib\mono'
        if (Test-Path -LiteralPath $monoRoot) {
            $netstandard = Get-ChildItem -LiteralPath $monoRoot -Filter 'netstandard.dll' -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '[\\/]Facades[\\/]' } |
                Select-Object -ExpandProperty FullName -First 1
        }
    }
    if (-not $netstandard) {
        throw 'Missing netstandard.dll (required by Unity assemblies). Search the game Managed and MonoBleedingEdge folders for netstandard.dll; send its path so we can update the references. Do NOT download random DLLs.'
    }
    Write-Host "[INFO] netstandard reference: $netstandard"
    $refFiles += $netstandard
    $refFiles += $scriptDll

    # ScriptsAssembly references GraphProcessor.BaseNode. CSC needs its defining
    # assembly even when our source only touches construction-related types.
    $nodeGraph = Join-Path $managed 'com.alelievr.NodeGraphProcessor.Runtime.dll'
    if (-not (Test-Path -LiteralPath $nodeGraph)) {
        throw "Missing required game dependency: $nodeGraph. Verify game files; do not download random DLLs."
    }
    Write-Host "[INFO] NodeGraphProcessor reference: $nodeGraph"
    $refFiles += $nodeGraph

    # Production API reflection now touches ResourceBlock internals. Those refer
    # to FixedIntArray16, defined in the game's APC.FixedDataTypes assembly.
    $fixedTypes = Join-Path $managed 'APC.FixedDataTypes.dll'
    if (-not (Test-Path -LiteralPath $fixedTypes)) {
        throw "Missing required game dependency: $fixedTypes. Verify game files; do not download random DLLs."
    }
    Write-Host "[INFO] FixedDataTypes reference: $fixedTypes"
    $refFiles += $fixedTypes

    # APC.FixedDataTypes.FixedIntArray16 implements Unity.Collections.IIndexable<T>.
    # Include the game-provided Unity.Collections assembly (do not download third-party DLLs).
    $unityCollections = Join-Path $managed 'Unity.Collections.dll'
    if (-not (Test-Path -LiteralPath $unityCollections)) {
        throw "Missing required game dependency: $unityCollections. Verify your game files."
    }
    Write-Host "[INFO] Unity.Collections reference: $unityCollections"
    $refFiles += $unityCollections

    # WorkProvider implements IStatsUserComponent<BuildingStats> from APC.Stats.
    # The type must be referenced when our speed patch uses WorkProvider directly.
    $apcStats = Join-Path $managed 'APC.Stats.dll'
    if (-not (Test-Path -LiteralPath $apcStats)) {
        throw "Missing required game dependency: $apcStats. Verify your game files."
    }
    Write-Host "[INFO] APC.Stats reference: $apcStats"
    $refFiles += $apcStats

    # A SINGLE-LINE, ANSI response file works reliably with the legacy .NET Framework csc.
    # v0.1.1 constructed the response arguments incorrectly and could pass a stray newline.
    # Build each fully-quoted token separately; never concatenate an array expression.
    $compilerTokens = New-Object 'System.Collections.Generic.List[string]'
    $compilerTokens.Add('/nologo')
    $compilerTokens.Add('/target:library')
    $compilerTokens.Add('/optimize+')
    $compilerTokens.Add('/out:"' + $out + '"')
    foreach ($refFile in $refFiles) {
        $compilerTokens.Add('/reference:"' + $refFile + '"')
    }
    $compilerTokens.Add('"' + $source + '"')
    $rsp = Join-Path $dist 'compiler.rsp'
    $line = [string]::Join(' ', $compilerTokens.ToArray())
    [System.IO.File]::WriteAllText($rsp, $line, [System.Text.Encoding]::Default)
    Write-Host "[BUILD] $ModName -> $out" -ForegroundColor Cyan
    Write-Host "[INFO] Compiler response file: $rsp"
    & $csc ('@' + $rsp)
    if ($LASTEXITCODE -ne 0) { throw "Compiler failed (exit $LASTEXITCODE). Check dist\compiler.rsp" }
    if (-not (Test-Path -LiteralPath $out)) { throw "Output DLL not created: $out" }
    Write-Host "[OK] Built $out" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
