# Changelog

## 0.7.2 — Stabilization
- Preserve all working v0.7.1 gameplay hooks.
- Reduce expensive Unity-wide storage scans from every scheduled scan to every 30 seconds (plus scene initialization/manual Rescan).
- Prefer the city-maintained storage registry at each scheduled check.
- Skip expensive discovery before the city manager is ready.
- Disable verbose performance profiling by default on fresh installs; it can be enabled in configuration.
- Update dashboard and package version.
