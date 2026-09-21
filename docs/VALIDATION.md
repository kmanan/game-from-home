# Validation — v0.1.0

Validated on Windows 11 x64, 21 September 2026. The app, core, and fixtures built with zero compiler errors or warnings. **17 regression checks passed**; see [test-results.json](test-results.json).

Coverage includes normal/hidden cooperative exits, vetoes, protected apps, incomplete identity, PID reuse, remaining children, canceled runs, path collisions, corrupt profiles, and signed memory deltas. Third-party apps were inspected read-only; individual versions were not all closed during development.

The native screen was rendered with real process/memory data to check layout and icons. Its values are a snapshot, not a benchmark or savings promise. Production code contains no process force-termination or OS reboot path.

The workflow template in `ci/windows-build.yml` compiles fixtures but runs headless checks only. It is not active in GitHub Actions. Full fixture tests need an interactive Windows desktop session and run via `build.ps1 -Test`.
