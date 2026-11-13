# All Will Fall Tweaks v0.6.5.1 — Performance Diagnostics

BepInEx 5 mod for All Will Fall. Retains v0.6.4 resources, 10x storage, 5x housing, instant/free construction, F8 dashboard and persisted config settings.

## Build / install
Run `build.cmd`, then `install.cmd`. Scripts use game DLLs from the local All Will Fall installation; no game binaries are included. Back up saves and your working v0.6.4 plugin.

## Performance changes
Capacity synchronization no longer runs on the 0.65-second timer. The background discovery interval remains configurable (default 5 seconds). Storage capacities sync on scheduled city discovery and after capacity settings change. Stock refresh is only requested when the Resources tab is visible.

## Diagnostics
BepInEx config `[Diagnostics]`: `PerformanceDiagnostics = true` and `SlowOperationThresholdMs = 8`. The game log includes `PERF SLOW`, `PERF FRAME HITCH` (at most one immediate hitch warning per 10 seconds), and `PERF SUMMARY (30s)` reports. They measure wall-clock duration of mod scans; frame hitches can originate in the base game or other systems. Test with F8 closed for ~60 seconds and share the log. Disable performance diagnostics after testing to reduce logging.

## Limitations
Performance work was not compiled or tested in a Windows game session. If errors occur, revert to v0.6.4.
