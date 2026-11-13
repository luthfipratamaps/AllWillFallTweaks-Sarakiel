# All Will Fall Tweaks v0.4.5 (experimental)

BepInEx 5 Mono plugin. Same working resource controls, housing 5x, and click-through guard as prior versions.

## Install
Game: `C:\Games\All Will Fall`
Plugin folder: `C:\Games\All Will Fall\BepInEx\plugins`
Double-click `install.cmd`. `build.cmd` builds; `release.cmd` packages; `uninstall.cmd` removes plugin. Requires installed game and BepInEx on Windows.

## Storage fix being tested
The game can read `Storage.Capacity` before a building-to-storage mapping is available. This update scans owners periodically, caches original backing capacity, and sets the live `Storage.capacity` field to original * multiplier. This may also correct the slider which appears to use the backing capacity. Food Storage variants also map to food.

For water (300), food warehouse (600), fuel warehouse (1000), and general warehouse (600), default is 10x; for houses (8), 5x. Live config changes supported.

**Back up your save** before testing: modified backing fields could be serialized by the game. After updating, check water/food/fuel/general displayed capacities, slider fill, physical stock beyond normal limits, then housing and input guard.

Look for `CAPACITY OWNER` and `CAPACITY APPLY` in BepInEx/LogOutput.log. This build has NOT been compiled in the user's Windows environment or runtime-verified.
