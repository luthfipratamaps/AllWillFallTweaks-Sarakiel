# All Will Fall Tweaks v0.7.2 — Stabilization

Maintenance release based on the user-verified v0.7.1 Instant Research and Free Research implementation.

## Performance changes
- Storage owner discovery uses `CityResourceManager.resourceBuildings` on each scheduled scan.
- `Resources.FindObjectsOfTypeAll(ResourceBuilding)` is limited to once per scene, manual Rescan, and every 30 seconds as a fallback.
- No Unity asset scan is performed before `CityResourceManager` becomes available.
- `[Diagnostics] PerformanceDiagnostics` now defaults to `false` for new installations. Set `true` to profile timing; existing configs keep their settings.
- Full scan fallback may delay recognition of unregistered new storage buildings by up to 30 seconds. Use Rescan for immediate recognition.

## Tested baseline
- Resource injection, capacity multiplier, construction toggles and research hooks were tested by the mod author in v0.7.1.
- This v0.7.2 build has not been tested in-game. Keep the previous DLL and a backup save.

## Installation
Set `$GameDir` in `config.ps1` if necessary, then run `build.cmd` and `install.cmd`.
