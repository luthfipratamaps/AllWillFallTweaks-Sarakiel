# Changelog

## v0.3.2 — Unity Input System guard (experimental)
- Adds Harmony suppression for InputAction.IsPressed / WasPressedThisFrame / WasReleasedThisFrame / WasPerformedThisFrame / triggered.
- Adds ButtonControl pressed/edge getters suppression for newer Unity Input System.
- Detects presence and logs number of patch targets at startup.
- Config: `[Input] BlockNewInputSystemWhileMenuOpen = true`.
- Preserves v0.3.1 legacy guard, F8 menu, and resource injection.
- Warning: game-specific handlers bypassing these APIs may still click through.
- Warning: all InputActions (including gameplay keyboard bindings) may be muted while the menu is visible.

# Changelog

## v0.3.1
- Attempt to block game-world mouse input for the entire time F8 menu is open.
- Harmony prefixes for UnityEngine.Input.GetMouseButton/Down/Up and mouse KeyCode GetKey/Down/Up.
- Retain v0.3.0 EventSystem guard and IMGUI event capture.
- Toggle option `Input.BlockWorldMouseWhileMenuOpen` (default true).
- Logs patch count and failures to facilitate game-specific fallback.
- No changes to proven resource modification path.

## v0.3.0
- Modern resource dashboard.
