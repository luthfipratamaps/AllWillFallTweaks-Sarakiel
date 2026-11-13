# Changelog — All Will Fall Tweaks

## [0.11.5] — Battery Electricity Capacity

### Added
- Battery-only capacity multiplier in **CAP** with 1x, 10x, 100x and 1000x presets, independent of warehouse capacity.
- Runtime, once-only electricity-storage capacity getter patch with supported int/long/float return signatures.
- BepInEx `Capacity.BatteryCapacityEnabled` and `Capacity.BatteryCapacityMultiplier` (defaults enabled, 10x).

### Notes
- Getter patch needs in-game verification; base storage asset values are not modified.
- Ordinary inventory, construction, research, citizens, structural and weather features are unchanged.
- No recurring scene scans added.
- Electricity yield hook warning reported in v0.11.4 is a separate issue, not resolved here.

## [0.11.4] — Electricity Production Support (experimental)

- Add direct electricity output multiplier to `ElectricityProducer.BuffedProduction(): int` instead of globally modifying city electricity balances.
- Recognize `ElectricityProducer` in production work providers for speed, preserving existing combined speed limit.
- Keep all existing output controls and their saved configuration.
- Preserve zero-output conditions, factory/resource inputs, existing features, and no-recurring-scan performance policy.
- Update Output tab copy to mention electricity generation.
- Pending verification on a working sunlight-powered or fuel-powered electricity generator.

# Changelog — All Will Fall Tweaks

## v0.11.3 — Structural cleanup & stabilization (2026-11-06)

### Changed
- Preserved the v0.11.2 strength-data multiplier and the `StressLayer.get_Collapses()` overload guard. **No new gameplay cheat hooks.**
- Restores the original cached `StressParameters.LinkStrength` values during plugin disable/destruction as well as when switching the multiplier off or changing scenes.
- Reapplies strength from the **original values**, never previously multiplied values; 5× to 50× remains 50× rather than 250×.
- Skips unnecessary asset field writes when the existing strength value already matches the target.
- Removed structural experimental-status messages, scan explanations, and hook counters from the player-facing F8 STRUCT tab.
- Reduced routine structural logging; detailed strength update messages require `Diagnostics.VerboseLogging=true`.
- Retained the single full structural-asset discovery on city initialization; no recurring structural Unity-object scans.
- Existing settings, GUI controls, and all previous resource/capacity/build/research/output/citizen/weather features are preserved.

### Notes
- v0.11.2 was confirmed working by the user for unusual load-bearing structures. This package has been source-checked but has **not** been Windows-compiled or tested in the game here.
- The overload-collapse guard targets a managed structural-layer flag, not general demolition. Burst/native paths or missing connectivity can behave differently.
- Keep your v0.11.2 DLL and save as rollback when upgrading.

## v0.11.2 — Structural data and collapse-condition experiment
- Replaced v0.11.1 managed stress-result patches with source asset multiplier `StressParameters.LinkStrength`.
- Cached baseline strength for noncumulative multiplier switching and restoration.
- Added experimental `StressLayer.get_Collapses()` guard without patching global `Collapser.Collapse`.
- Single asset discovery at city load.
- Confirmed working for the user's in-game test of extreme structures.

## v0.11.1 — Managed stress calculation experiment
- Patched `StressJobUtility.CalculateStress` overloads; hooks installed but failed to reliably stop the user's tower from collapsing.

## v0.11.0 — Structural API discovery
- Added STRUCT tab and discovered stress layer, parameters and solver types. No effective structural gameplay changes yet.

## v0.10.1 — Weather direct hooks
- Native sea-level adjustment, rain intensity, weather presets and storm parameter overrides; user confirmed working.

## v0.10.0 — Weather discovery
- Added Weather tab, saved options and bounded API inspection; controls did not yet modify gameplay.

## v0.9.5 — Performance / GUI stabilization
- Preserved single city-initialization storage scan, removed recurring full-scene fallback scans, and hid player-facing API discovery controls.

## v0.9.4 — Citizen productivity
- Citizen carrying capacity and resource-producing job work interval controls; user confirmed working.

## v0.9.3 — Citizen needs
- Food/water consumption and satisfaction fixes; user reported working.

## v0.9.2 — Citizen group HUD
- Recruited Workers/Sailors/Engineers appear in top HUD; user confirmed working.

## v0.9.1 — Citizen direct hooks
- Happiness, starving/dehydrated checks, movement and native group recruitment; user confirmed features with later fixes.

## v0.9.0 — Citizen API discovery
- Added Citizens tab and experimental hooks.

## v0.8 — Production tweaks
- Yield multiplier through `ResourceProducer.Produce(ResourceBlock)` and speed multiplier through `WorkProvider.get_WorkInterval()`; confirmed working in v0.8.3.1.

## v0.7 — Research tweaks
- Instant/free research; later cleaned up init scans and GUI.

## v0.6 — Construction tweaks
- Instant/free construction with saved toggles, GUI fixes and performance fixes.

## v0.5 / v0.4 — Capacity
- Housing and storage capacity multipliers and automatic city scanning.

## v0.3 / v0.2 / v0.1 — Foundations
- Resource addition for nine types, modern F8 interface, world-input click-through prevention, and build/install tooling.

For details on intermediate experimental and build-fix versions, see the earlier archived CHANGELOG.md from the project.
