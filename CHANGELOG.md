# Changelog

## v0.6.5.1 — BepInEx 5 compatibility build fix
- Fixed CS1061: removed invalid `ConfigEntryBase.SettingChanged` subscription.
- Detect capacity setting changes with five primitive comparisons during `Update()`; synchronization only runs when settings differ.
- No other gameplay hooks or performance diagnostics modified.
- This fixes the reported compile error; local game compilation is not available in this environment.

# Changelog

## v0.6.5 — Performance diagnostics
- Removed periodic 0.65-second reflection capacity synchronization. Capacities now synchronize on city discovery and when capacity settings change.
- Deferred startup storage scans until the city is ready.
- Avoided background resource count refresh when the Resources tab is hidden.
- Cached reflection metadata for the Storage.capacity field, and removed per-sync dictionary snapshot allocations.
- Added `Diagnostics.PerformanceDiagnostics=true` and `SlowOperationThresholdMs=8` with slow-operation warnings, rate-limited frame hitch warnings and 30-second aggregate performance summaries.
- Preserved GUI, construction patches, config persistence, auto-scanning and capacity features.

**Experimental:** no game-side FPS benchmark was run. Diagnostics are intended to confirm the source of residual lag.
