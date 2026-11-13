# All Will Fall Tweaks v0.9.5 — Stabilization

Cleanup of the user-confirmed working v0.9.4 build. **Back up the v0.9.4 DLL and game save before installing.**

## Player-facing UI

Press **F8**. Six tabs: Resources, Capacity, Build, Research, Output, Citizens.

- Removed all obsolete API scan buttons and technical hook counters from the GUI.
- Removed development-only and experimental labels from player-facing controls.
- Retained **Resources → Refresh** for manual refresh and capacity discovery.
- Retained independently configurable options and persistent BepInEx settings.

## Performance

- **Full scene storage discovery runs once when CityResourceManager first becomes ready** for each loaded city; capacity multipliers are applied in the same pass.
- Lightweight registry-only updates occur at `BackgroundScanIntervalSeconds` (default 5 seconds).
- **Removed the recurring 30-second full scene fallback**, avoiding `Resources.FindObjectsOfTypeAll` during ordinary gameplay.
- Full discovery remains available via **Resources → Refresh**.
- Unnecessary performance-timing stopwatch allocations removed from the scheduled scans.
- Per-storage informational logs are suppressed unless `[Diagnostics] VerboseLogging = true`.

If a building does not appear in the city's registry after construction, press Resources → Refresh once. The scan-on-city-load behavior is unchanged.

## Existing features

Resources, capacity, instant/free construction, instant/free research, production yield/speed, citizen happiness and needs, recruitment/HUD, movement speed, work speed and carry capacity are inherited from v0.9.4.

## Build and install

Set the game folder in `config.ps1` if needed, run `build.cmd` followed by `install.cmd`. Uses the game's installed managed DLLs. No proprietary DLLs redistributed.

**Verification:** static source/ZIP checks performed; this build still needs a Windows compilation and in-game regression test.
