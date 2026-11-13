# All Will Fall Tweaks — v0.3.2 (strict mouse input guard)

Preserves v0.3.0 modern F8 menu and verified resource additions. New experimental guard attempts to stop in-game click-through while the F8 menu is OPEN, using Harmony patches on six legacy Unity mouse/key polling methods. The prior EventSystem pointer-over guard remains active.

## Install

Game: `C:\Games\All Will Fall`
Plugins: `C:\Games\All Will Fall\BepInEx\plugins`

Double-click `install.cmd` to build and install. `build.cmd` builds, `release.cmd` packages a release ZIP, and `uninstall.cmd` removes the DLL.

## Test on a backup save

1. Launch game and load city, press F8, click a button over a building. The game should not select or move the building if its input uses legacy Unity polling.
2. Close F8 and verify ordinary world selection works.
3. Check `BepInEx\LogOutput.log` for `Strict mouse guard: X/6` and warnings. If it is 0/6 or the game still registers clicks, the game may use its own or the new Input System: send the log for a game-specific patch.
4. If controls misbehave, quit the game and change `BlockWorldMouseWhileMenuOpen = false` in `BepInEx\config\id.sarakiel.allwillfall.resourcetweaks.cfg`.

**Limits:** This guard has not been tested in the game. Harmony may be unable to patch Unity internal-call input methods; a successful patch also cannot guarantee interception of custom/new Input System handlers. It blocks legacy mouse button polling anywhere on screen while the menu is open (including clicks outside the menu). F8 remains enabled.


## v0.3.2 Input protection trial
While the F8 menu is open, both legacy mouse polling and Unity Input System action/button boolean polling are suppressed. This is an experimental compatibility guard, not a game-specific patch. It may also block game hotkeys while the menu is visible. F8 closes using the legacy key check. To disable the new layer edit `BepInEx\config\id.sarakiel.allwillfall.resourcetweaks.cfg` and set `BlockNewInputSystemWhileMenuOpen = false`. Keep backups of your saves during testing.
