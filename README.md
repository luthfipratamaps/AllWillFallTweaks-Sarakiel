# All Will Fall Tweaks — Sarakiel v0.5.0

BepInEx 5 (Mono) mod. Stable-feature cleanup release based on v0.4.6, with no changes to the verified resource, housing, storage-capacity or input-guard algorithms.

## Install and build

- Game: `C:\Games\All Will Fall`
- Plugins: `C:\Games\All Will Fall\BepInEx\plugins`
- Double-click `install.cmd` to build and install (requires the same Windows compiler and game files as v0.4.6).
- `build.cmd` builds only; `release.cmd` creates a distributable ZIP; `uninstall.cmd` removes the mod DLL.
- Press **F8** for the dashboard; the city and newly constructed buildings are scanned automatically.

## Default settings

The plugin *generates* its config under `BepInEx\config\id.sarakiel.allwillfall.resourcetweaks.cfg` using `Config.Bind` defaults on first launch. A short reference copy is provided at `defaults/default-settings.cfg.example`.

- Storage multiplier 10x, house multiplier 5x.
- Water, food, fuel, general warehouse, and housing enabled.
- Auto scan enabled, repeat interval 5 seconds.
- Both mouse input guards enabled. F8 menu; resource amount 100; window X=110, Y=90.
- Verbose diagnostics disabled by default.

**Existing configs are retained**: BepInEx does not overwrite your saved preferences when a plugin version changes. To regenerate *all* factory defaults, first back up or rename the existing `.cfg` while the game is closed, then launch the game.

## Notes

Capacity adjustments can affect saves; test on a backup city. Changes to an in-use storage may be saved as in-game values. The game must be installed locally for compilation. This package hasn't been tested on your Windows system.
