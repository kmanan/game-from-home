# Architecture — v0.2.2

The WPF interface shows only entries with `AppSnapshot.CanClose == true` above 500,000,000 bytes of private working set, sorted largest first. App grouping occurs before filtering. Below-threshold entries do not enter the selection or one-click cleanup; the cleanup verifier still receives the full discovered inventory. App row units are decimal MB/GB to match the threshold. Idle scans run every five seconds while visible. There is no resident service or global hotkey listener.

## Discovery and ownership

Toolhelp enumerates process IDs and parents. Native inspection obtains creation time, executable path, session, user SID, and private working set. Known app rules group supported apps. Unknown desktop applications with visible top-level windows outside Windows system paths are offered for opt-in cooperative exit. Same-executable helpers and verified app-owned descendants are included. Every PID appears only once.

Runtime executables are not swept into a desktop app merely by parentage. Codex-owned binaries under its installation/runtime directory are associated only through a verified Codex ancestor. Standalone or user-launched runtimes remain separate internal entries and require a supported exit adapter to appear in the cleanup list. Raw command lines are read transiently only for Bun/Node worker identification; they are not displayed, stored in profiles, or written to diagnostics.

Everything not grouped remains an internal process entry. Protected, incomplete, and inspection-only entries are excluded from the cleanup list regardless of RAM usage. Other-user, service/system, inaccessible, and unadapted background processes are inspection-only. Windows performance counters supply private working set where direct inspection is unavailable; these values are cached for approximately ten seconds. Unknown memory stays unknown. Drivers and kernel pools cannot all be attributed to individual processes; the overall physical-memory counter remains the reference total.

## Selection and closure

`AppSnapshot.CanClose` rejects protected, incomplete, or inspection-only targets. Codex and newly discovered apps default to unchecked. Discord and Game From Home cannot be closed by the cleanup service. Codex is ordered last in a selected cleanup. Dynamic desktop-app selections persist only with a reviewed installation fingerprint; processes identified only by PID are never persisted as closure targets.

Desktop exits use Windows Restart Manager with flags zero, registering exact PID plus creation time and no files/services. The affected scope must match and exclude console, service, Explorer, and critical types. Process instances are revalidated before the request, then original instances and rediscovery are observed afterward. Remaining helpers or relaunches prevent a false success.

The claude-mem adapter reads the default per-user worker PID file. It requires a same-user Bun/Node process whose command identifies the claude-mem worker script, a matching listening TCP port owned by that process, and a matching health-response PID. It rechecks process identity and listener ownership immediately before the worker's own HTTP shutdown request. HTTP uses loopback only, no proxy, no redirects, bounded response size/time. No arbitrary commands, restart, or force-termination fallback run. Actual process exit is still verified. Custom data directories and unsupported layouts remain internal inspection-only runtimes.

## Reporting and preferences

System RAM change is the signed difference between median available-memory samples. It is not a sum of selected process memory and can be negative due to other activity. Atomic JSON files in LocalAppData store selections and the last cleanup report. Installation identity changes require review. No telemetry is sent.

## Single-file distribution (v0.2.4)

The Portable publish profile targets win-x64 and bundles app assemblies into one framework-dependent EXE. The runtime is not bundled. Microsoft's native WinExe apphost detects missing runtimes before managed startup and supplies the download dialog. Both WindowsDesktop and NETCore framework requirements are preserved; the application project orders WindowsDesktop first so a machine missing both is directed to the Desktop installer, which includes the core runtime. This ordering is included in the SDK runtime-configuration input hash to avoid stale incremental build output. There is no custom downloader or silent installer. SDK single-file analysis/build tooling is restored from the official NuGet feed; the application has no third-party runtime packages.
