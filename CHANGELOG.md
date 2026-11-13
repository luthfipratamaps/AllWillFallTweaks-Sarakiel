# Changelog

## v0.9.5 — Performance and UI stabilization

- Preserved all gameplay controls from the user-confirmed v0.9.4 baseline.
- Removed API discovery buttons, runtime counters, and engineering instructions from all tabs.
- Kept automatic full storage discovery on city readiness and immediate capacity application.
- Removed recurring 30-second Unity-object discovery; routine monitoring uses the city's registry.
- Preserved manual Resources → Refresh for exceptional/unregistered storage detection.
- Reduced routine scan allocations by removing Stopwatch timing around scheduled checks.
- Gated verbose per-storage logging behind the existing `VerboseLogging` config toggle.
- Clarified the difference between resource-producing Work Speed and other citizen tasks.
- Preserved settings and existing build/install scripts.

## v0.9.4 — Citizen productivity (working baseline)

- Added independent Work Speed and Carry Capacity multipliers, with GUI controls and saved settings.
- User confirmed in-game working speed and carrying capacity.

For older versions, consult the existing comprehensive development changelog retained separately in prior releases.
