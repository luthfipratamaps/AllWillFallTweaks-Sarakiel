# All Will Fall Tweaks v0.10.1 — Weather Direct Hooks (Experimental)

Includes full v0.9.5 gameplay baseline.

## Weather controls
- Sea level Raise/Lower: finds native `AddSeaLevel` or `ShiftSeaLevel` once per city, then calls it from UI. Native units may not equal meters. If none matches, controls refuse to change level. Half-unit presses buffer when native API takes integers.
- Rain intensity: patches `WeatherSegment.get_Raininess`.
- Suppress storms: overrides `get_Wind`, `get_ExtraPhysicsStress`, and `get_SeaFlow`; does NOT cancel or rewrite scheduled weather blocks or storm sky VFX.
- Weather presets Clear/Rain/Storm: change effective rain/cloud/wind parameters, not timeline blocks or full volume profiles. May have different effects on visuals and gameplay.

This is experimental and not yet verified in game. BACK UP SAVE and v0.9.5 DLL before testing.

## Install
Run `build.cmd` then `install.cmd`. Send `LogOutput.log` and screenshots after checking sea level, rain and storm changes.

## Performance
No recurring reflection scan. Native sea-level method lookup runs once at city initialization, and Harmony postfixes have constant-time logic.
