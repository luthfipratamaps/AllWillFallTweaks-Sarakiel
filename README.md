# All Will Fall Tweaks v0.8.3.1 — Resource Production Speed Experiment

Based on the proven v0.8.2.2 project. Production yield (confirmed working by user) and all existing resources, capacity, construction, research, input, and scan code are retained.

## Changes

- Harmony postfix on actual `WorkProvider.get_WorkInterval(): float` found in v0.8.2 production scan.
- Speed applies only to work providers whose `producers` list contains a `ResourceProducer`, protecting research and unrelated work.
- Positive-only identity cache avoids repeated reflection for known producers; cache reset on scene change.
- User-selected speed divisor (1–25×); minimum effective WorkInterval is 0.1 (in game's interval units).
- Existing UI presets and settings persistence retained. Version display corrected to 0.8.3.

## Build and install

Use `build.cmd` and then `install.cmd`. Scripts reference managed libraries from `C:\Games\All Will Fall`, including `ScriptsAssembly.dll`, `APC.FixedDataTypes.dll`, and `Unity.Collections.dll`. Modify `config.ps1` if the game is elsewhere.

## Testing on a backup save

With Yield OFF, Speed ON at 2×, assign workers to a production building and time a completed cycle. Compare with Speed OFF at the same working conditions. The UI may retain the base duration even when actual cycle progress is accelerated. Then test Yield ON and Speed ON together. Inspect `LogOutput.log` for `PRODUCTION SPEED PATCH` and GUI `speed` applied-call count.

**Experimental:** This getter may control only some of the game's production calculations; a successfully installed patch does not guarantee the cycle itself accelerates. Don't enable 25× initially; assess performance and save consistency first.

**Build verification:** package/source checked here; Windows game DLL compilation and in-game results not verified.

## v0.8.3.1 compiler dependency fix

Added the game-provided `APC.Stats.dll` to both the PowerShell compiler references and the MSBuild project. `WorkProvider` implements `IStatsUserComponent<BuildingStats>` from that assembly. No production gameplay behavior changed.
