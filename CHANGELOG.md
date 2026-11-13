## v0.8.3.1 — APC.Stats compiler reference

- Fix CS0012 (`IStatsUserComponent<T>`) by referencing the game-provided `APC.Stats.dll` in both build paths.
- Keep experimental production speed getter hook and confirmed yield patch unchanged.
- No claim of successful in-game compilation or production speed validation.

## v0.8.3 — Scoped production-speed hook (experimental)

- Target runtime-verified `WorkProvider.get_WorkInterval(): float` instead of nonexistent `get_TotalCycleTime()`.
- Apply speed divisor only to providers with a `ResourceProducer`; keep 0.1 minimum interval and scene-local cache.
- Preserve confirmed-working production yield and all previously working modules.
- Updated version labels and production UI descriptions.
- Requires in-game cycle timing verification.

## 0.8.2.2 — Compiler dependency fix

- Add game-provided `Unity.Collections.dll` to `build.ps1` and the project file.
- Resolve `CS0012` for `Unity.Collections.IIndexable<T>` indirectly required by `APC.FixedDataTypes.FixedIntArray16`.
- No changes to production gameplay hooks. Yield remains experimental; speed remains diagnostic-only.
- Build against your installed game's managed assemblies; Windows compilation not verified here.

## v0.8.2.1 — Build dependency fix

- Added game-managed `APC.FixedDataTypes.dll` to the PowerShell compiler references and project file (fixes CS0012 for `FixedIntArray16`).
- Added a clear missing-dependency check before compilation.
- No changes to production, construction, research, or storage gameplay logic.

# Changelog

## v0.8.2

- Replaced unavailable ResourceBlock multiplication operator with guarded output-only Produce replay for experimental yield.
- Explicitly disabled unsupported speed hook; added WorkProvider/Workplace/ResourceBlock signatures to manual API scan.
- Preserved research, construction, resources, capacity, initialization and configuration.
- No changes to factory input consumption hooks.
