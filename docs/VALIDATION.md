# v0.2.4 single-file startup verification

Validated on Windows 11 x64, 22 September 2026. Release build and single-file publish passed with zero warnings/errors. The distributed EXE is 505,416 bytes. Only that EXE was copied to a separate folder for startup verification; no companion DLL, runtimeconfig, or deps files were present.

- Installed .NET 9 Desktop Runtime: the exact release EXE rendered the native UI with live process/memory data; icons and core discovery loaded successfully.
- Empty runtime location (no host): native download dialog appeared and cancellation exited without starting the app.
- Host present, no frameworks: native dialog requested Microsoft.WindowsDesktop.App 9.0.0 x64.
- Core runtime present without Desktop Runtime: native dialog requested Microsoft.WindowsDesktop.App 9.0.0 x64.
- Both the no-host and missing-framework download URLs resolved with HTTP 200 to Microsoft's x64 .NET 9.0.20 Desktop Runtime installer page.

Missing-runtime checks used only child-process DOTNET_ROOT overrides and isolated folders; installed runtimes and machine environment variables were not changed. Dialogs were inspected and cancelled; no runtime was installed and no user app was closed. Installing the runtime through the prompt on a clean Windows VM was not tested. The user must reopen the app after runtime installation. Existing app-closure logic is unchanged from the earlier regression suite below.

# v0.2.3 interface and window verification

Release build passed with zero warnings/errors. Native screenshot confirms the search field is removed. During an eight-second normal-start observation, the app owned exactly one visible window; a duplicate launch exited without creating a window. Screenshot capture now renders offscreen without a taskbar entry or activation; native window bounds verified it stayed offscreen. The user-reported blank popup was not reproduced during normal startup, so its source remains unconfirmed. No user applications were closed during these checks.

# v0.2.2 actionable-list verification

The Release build passed with zero warnings/errors. The native screen was rendered with live process data and visually inspected: Edge, Codex, and Teams above 500 MB appeared; Memory Compression, Discord, and other non-closable entries did not. No user apps were closed during verification. The shutdown implementation is unchanged.

# v0.2.1 threshold verification

The build passed with zero warnings/errors. The native screen was inspected with live data: only four grouped entries above 500,000,000 bytes appeared; smaller entries were excluded from the selection. The shutdown implementation is unchanged from the 35-check v0.2.0 validation below.

# Validation — v0.2.0

Validated on Windows 11 x64, 21 September 2026. Application, core, and fixtures build with zero warnings/errors. **35 regression checks passed**; see [test-results.json](test-results.json).

Checks cover normal/hidden exits, vetoes, protected targets, incomplete/stale identities, remaining children, cancellation, profile validation, Codex opt-in selection, runtime/worker classification, health PID matching, local TCP ownership, and deduplicated discovery. Only disposable test-owned apps were closed.

Read-only live verification found Codex, the installed claude-mem 13.25.2 worker, Bun/Node processes, discovered desktop apps, WSL, and Memory Compression. The worker health endpoint matched its PID; no live worker shutdown was requested. Actual Codex and claude-mem shutdown remain untested in this session to avoid interrupting the user's work. The worker adapter has no forced fallback.

The native UI was rendered with real process/memory data. Its values are a snapshot, not a benchmark. Searchable virtualized rows show the broader inventory without rendering every row at once. Individual versions of every third-party app are not certified for cooperative exit.

The workflow template in `ci/windows-build.yml` is included but is not active in GitHub Actions. Full disposable fixture tests run via `build.ps1 -Test`.
