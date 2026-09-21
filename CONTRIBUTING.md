# Contributing

Keep the app focused on cleanly exiting reviewed apps and reporting RAM accurately. Preserve Discord/Codex protection, exact process identity, same-user/session boundaries, refusal handling, and honest partial results.

Use the SDK pinned in `global.json`. Run `./build.ps1 -Test` in an interactive Windows test session. Add path/name collision tests for discovery rules. Test shutdown changes with disposable apps, not a user's working apps.

Do not introduce forced termination, automatic elevation, telemetry, resident services, broad runtime-name targeting, or unrelated Windows tweaks. Issues should identify app distribution/version and observed behavior; redact private content from diagnostics.
