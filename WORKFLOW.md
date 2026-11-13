# Workflow

1. Edit `src/AllWillFallTweaks.cs`.
2. Double-click `build.cmd`, then `install.cmd` to deploy.
3. Start the game: automatic city/storage scan runs after loading; F8 opens the menu.
4. Double-click `release.cmd` to build a distributable ZIP.
5. To debug, set `[Diagnostics] VerboseLogging = true` in the BepInEx config.

Use `config.ps1` to change game paths. Do not overwrite existing user config files during install.
