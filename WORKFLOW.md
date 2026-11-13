# Development workflow

1. `build.cmd` — compile from `src` using installed game/BepInEx references.
2. `install.cmd` — build and install to `C:\Games\All Will Fall\BepInEx\plugins`.
3. Test on a backup save (F8).
4. `release.cmd` — produce release archive.
5. `uninstall.cmd` — remove installed plugin.

Inspect `CAPACITY APPLY` / `CAPACITY OWNER` when diagnosing storage.
