# Changelog

## 0.4.5 — Storage backing-capacity synchronization (experimental)
- Carry forward working 0.4.4 resource injection, F8 dashboard, input guards and housing getter.
- Scan live ResourceBuilding owners more frequently (0.6 s).
- Cache base `Storage.capacity` on first discovery and apply effective capacity directly to the backing field for the four enabled warehouse families.
- Keep the getter postfix for consistent simulation queries; derive from the saved original capacity.
- Restore base field when storage multiplier is 1x or building type disabled, while game is running.
- Log the first `CAPACITY APPLY` per storage for easier troubleshooting.
- This changes live game state and can be serialized in saves. Test on backup saves.
- UI slider correction is expected if its backing denominator is Storage.capacity; not verified.
