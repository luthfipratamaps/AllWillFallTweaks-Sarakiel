# All Will Fall Tweaks — Sarakiel v0.11.5

BepInEx 5 / Harmony mod for the **Mono** version of All Will Fall.

## What's included

- **Items:** Add Water, Food, Junk, Wood, Metal, Sand, Fuel, Goods and Tools.
- **Capacity:** Storage, housing, and independent battery electricity capacity multipliers (10x/100x/1000x).
- **Build:** Instant and free construction.
- **Tech:** Instant and free research.
- **Output:** Production yield and production-cycle speed multipliers.
- **Crew:** Happiness, no food/water requirements, recruitment, movement speed, carry capacity and production work-speed controls.
- **Weather:** Sea-level up/down, rain intensity, storm suppression and weather presets.
- **Struct:** Structural strength multiplier (1×, 2×, 5×, 10×, 25×, 50×) and an independent overload-collapse safeguard.

## Build / Install

1. Install **BepInEx 5 Mono** into All Will Fall.
2. Edit `config.ps1` if the game isn't installed at `C:\Games\All Will Fall`.
3. Run `build.cmd`, then `install.cmd` (which also builds before installing).
4. Start the game and press **F8** for the tweaks menu.

The Windows build uses the **DLLs from your installed game**, including `ScriptsAssembly.dll`, Unity assemblies, `APC.Stats.dll`, `APC.FixedDataTypes.dll`, `Unity.Collections.dll`, and `com.alelievr.NodeGraphProcessor.Runtime.dll`. Don't download arbitrary replacement DLLs.

## Structural stabilization

Structural strength modifies the game's `StressParameters.LinkStrength` assets. A one-time asset scan runs when the city manager initializes. Normal background operation does **not** repeat that scan. Original values are cached and used for all new multiplier choices, so changing 5× to 50× doesn't compound the multiplier; turning the feature off or changing scenes restores originals. The plugin also restores these values when disabled/destroyed.

**Prevent Overload Collapse** patches the managed `StressLayer.get_Collapses()` flag; it does not intercept general demolition. Support disconnection, native/Burst execution, and scripted events may still cause collapse. This feature is experimental even though the strength multiplier worked in the user's test.

## Save and configuration

Settings persist in the BepInEx configuration file (`BepInEx/config/id.sarakiel.allwillfall.resourcetweaks.cfg`). Existing settings are retained. Use a backup save when testing physics-related behavior and keep the working v0.11.2 DLL for rollback.

## Performance and diagnostics

- Storage and structural objects get a full discovery scan at **city initialization**, not repeatedly during play.
- New storage buildings are monitored using the game's city registry.
- Developer scanners aren't exposed on the player-facing GUI.
- Optional detailed logs can be enabled in the BepInEx configuration under `Diagnostics.VerboseLogging`.

## Verification

This archive contains the **source project**, not a precompiled DLL. The project/ZIP were inspected in the packaging environment, but it was not compiled against the installed Windows game or in-game regression-tested here.


## Electricity producers (v0.11.5)

The existing **Output** tab now also targets `ElectricityProducer` for electrical generators, including Sun Energy Mirrors:

- **Yield:** a Harmony postfix on `ElectricityProducer.BuffedProduction(): int`. The actual `OnProductionComplete()` method obtains this value via `get_ElectricityOutput()`. Output is scaled by the same 1x–25x **Production Yield** setting, with integer saturation. A zero base output stays zero.
- **Speed:** `WorkProvider.get_WorkInterval(): float` detects `ElectricityProducer` alongside `ResourceProducer` in `WorkProvider.producers`. Same Production Speed + Citizen Work Speed settings and 0.1s minimum interval. Generators outside this work-provider lifecycle may not respond to speed; check the in-game cycle.
- No periodic scans or per-tick reflection were added; methods are patched once at plugin startup.
- Electricity depends on sunlight/weather and workforce. Test a generator under favorable daylight with workers active; a 0% sunshine generator may correctly produce nothing even with 25x yield.
- The original resource-production, research, construction, citizen, storage, weather and structural hooks are unchanged.

**Runtime verification pending:** This package has not been compiled against your Windows game or executed in-game. Keep v0.11.3 for rollback.

## Battery capacity (v0.11.5)

In **F8 → CAP**, enable **Battery Storage** and select **10x**, **100x** or **1000x**. A vanilla battery with 10 energy storage should report 100, 1,000 or 10,000 capacity if the native electricity-storage capacity getter is used by the current game build. 1x restores normal reported capacity.

The electricity storage system is separate from the ordinary `Storage` resource warehouse. v0.11.5 installs one capacity-getter patch on `ElectricityStorage` (or `ElectricityStorageHeight` if the class provides its own getter) at startup. No per-frame Unity object scan or hot-path reflection is added. The original backing capacity remains unchanged; current capacity is multiplied when accessed. Settings are saved in BepInEx config.

**Verification:** Game getter shape was discovered from assembly names, but has not been confirmed in runtime. Inspect `BATTERY CAPACITY PATCH` in `LogOutput.log`; confirm both the battery UI denominator and actual ability to fill past 10 energy. Keep v0.11.4 to roll back.

**Known outstanding item:** Some v0.11.4 logs report `ELECTRICITY YIELD: compatible BuffedProduction() absent`. This battery-only update does not claim to repair that separate electricity-yield hook.
