# Changelog

## v0.5.0 — Clean baseline & default configuration

- Matched fresh-generated BepInEx configuration to the user-tested v0.4.6 settings.
- Fresh-session default resource amount is now 100 (previously 1000).
- Added optional `[Diagnostics] VerboseLogging`, default `false`; hides repeated reflection and per-storage tracing except when troubleshooting.
- Preserved v0.4.6 capacity sync, auto-scan, resource injection, modern UI and Input System guard.
- Retained existing configs on installation; added sample defaults and updated documentation.

## v0.4.6

- Auto scan after city load, retries and background discovery.
