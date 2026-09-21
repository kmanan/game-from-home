# Game From Home

Product and implementation specification · v0.1 · 21 September 2026

**Implementation update (v0.2.0):** The [implementation README](../README.md) describes the current app. Codex is now selectable and opt-in; Discord remains protected. Discovery includes unknown desktop apps, background runtimes and system processes. The identified claude-mem worker has its own shutdown adapter. Other processes without a verified clean-exit method stay visible as read-only rows. The original design below is historical and may describe behavior superseded by the current implementation.

**One button to cleanly quit everyday apps and see RAM become available.**

## 1. Product contract

Game From Home is an on-demand Windows utility. It groups running processes into recognizable apps, shows their physical RAM usage, closes a remembered selection through normal application exit paths, and reports verified exits plus the measured change in available system RAM. Discord stays open.

The user already trusts the applications' normal session persistence. Game From Home does not implement session backups, browser-tab recovery, document saving, or memory snapshots. Its responsibility is to let the app perform its ordinary shutdown and accurately report the outcome.

The everyday interaction is one press of **Free up RAM** after opening the window. A separate shortcut or Stream Deck action can launch and execute the saved profile in one press without first opening the selection screen. Neither mode requires a permanent background process.

The interactive design prototype uses illustrative numbers and performs no live process inspection or termination. The package now also includes a working native Windows application; the implementation README above records its delivered scope and validation.

## 2. Audience and scope

Initial target: the user's Windows 11 x64 PC, with 64 GiB of installed RAM and everyday communication, browser, and development apps. The design uses this capacity for realistic examples; the shipped product reads usable physical memory from Windows.

Initial app catalog: Microsoft Edge, Microsoft Teams, Zoom, WhatsApp, Cursor, ChatGPT, and Claude. Discord is protected. Support is enabled per tested app distribution and version, not merely by recognizing an executable name.

### Included in v1

- On-demand discovery; one row per logical app with aggregated memory.
- Memory-descending suggestions from a known app catalog; stable ordering during interaction.
- Remembered close selections and an explicit protected-app list.
- A normal exit adapter for each supported app.
- Verified results, including partial completion, denied access, and unexpected relaunch.
- Visible RAM before and after cleanup, with honest interpretation.
- Portable, per-user execution; no administrator requirement for the normal path.
- A shortcut command that executes the saved profile once and then exits.
- A local, content-free record of the latest run for troubleshooting.

### Excluded from v1

Game detection, game libraries, FPS promises, process suspension, priority tuning, memory trimming, standby-cache purging, service/driver stopping, registry performance tweaks, forced termination, scheduled cleanup, automatic game launch, permanent tray residency, cloud accounts, telemetry, and automatic restoration of closed apps.

The app must not change another app's startup or background-running settings as an incidental part of cleanup. That would change future behavior beyond the current operation.

## 3. Primary experience

### First use

1. Discover recognized apps in the current user's interactive session.
2. Preselect supported everyday apps from the initial catalog. Make every selection visible before any action. Mark Discord as protected.
3. Show unsupported or unverified variants with the reason and a disabled close control. Do not silently substitute a forced termination.
4. The first click on **Free up RAM** establishes the user's close selection. There is no extra confirmation dialog for an ordinary clean exit.
5. Remember selections unless the user disables that preference. Newly discovered app identities must not silently join an existing profile.

### Normal use

Open → see current apps and RAM → press **Free up RAM** → watch exits → see result. The user need not choose apps again on every run.

Selections apply to an app across its windows and profiles within the current Windows user/session. For Edge, explain during setup that choosing it exits all Edge windows for that user; do not imply one tab or profile is targeted.

The selection label is **10.2 GiB in use**, not **10.2 GiB guaranteed to be freed**. Applications may share memory; other processes continue allocating and releasing memory during the run.

### Shortcut use

`GameFromHome.exe --run-profile default --show-result`

A desktop/taskbar shortcut or the existing Stream Deck launches this command. It loads the approved saved profile, takes fresh measurements, and begins closing. With no saved profile it opens setup instead. A single-instance lock prevents overlapping runs. A second launch brings the active result window forward rather than starting another operation.

Default result behavior: show the report until **Done** closes the utility. An optional **Close Game From Home after showing results** preference closes a successful report after eight seconds. A partial or failed run remains visible. No timers, helper processes, or tray process survive application exit.

## 4. Screens and visual design

### Main window

Default design width around 900 device-independent pixels, resizable with a minimum production width of 560. The web prototype also reflows down to 320 pixels for review. Native Windows caption controls remain present in the shipped application; the prototype focuses on content and does not simulate OS title-bar buttons.

- Header: app icon, **Game From Home**, **Preferences**.
- Hero: **Make room for play.** and **Quit your everyday apps in one go.**
- Right-aligned primary metric: available physical RAM and usable total.
- Thin memory bar: other usage, selected apps' measured private working set, and available RAM. It is a visualization, not a promise of recoverable RAM. If coverage is incomplete, replace the breakdown with used/available only.
- App rows: checkbox, icon, name, process count/status, quiet relative memory bar, right-aligned RAM value.
- Protected strip: **Discord stays open · Always protected**.
- Footer: selected count and measured usage, **Normal exits. No forced shutdowns.**, primary **Free up RAM** button.

The main action is disabled when no closable apps are selected. System services and arbitrary background executables are never mixed into this list as cleanup recommendations.

### Closing

Headline: **Wrapping things up.** Subtitle: **Giving each app time to exit cleanly.**

Keep the rows visible and stable. Each row transitions through **Requesting exit…**, **Waiting for app…**, **Verifying exit…**, and its final result. Freeze the run selection. The primary button becomes disabled with **Closing apps…**; do not create a duplicate action button.

Production includes a secondary **Stop remaining** action while work is in progress. It stops issuing new exit requests, verifies apps already requested, and reports a canceled remainder. It cannot undo exits already completed. Closing the main window while a run is active follows this same bounded cancellation path; it must not leave an invisible service behind.

### Successful result

Headline: **Room to play.** Subtitle: **7 apps closed cleanly. Discord is still running.**

Rows show **Exited cleanly** and **Closed**. The footer shows a signed change, for example **+9.0 GiB available RAM**, and **Before 28.5 GiB → after 37.5 GiB · measured change**. Use measured values, never animation counters disconnected from the backend. Primary action: **Done**.

### Partial result

Headline: **More room. One app stayed.** For multiple failures: **More room. Some apps stayed.**

The remaining app keeps its current RAM value and a specific reason: **Waiting for a response**, **Still running in background**, **Quit not supported for this version**, **Access unavailable**, or **Restarted after exit**. Production may offer **Show app** when a verified HWND exists, and **Retry remaining** after the user resolves an app prompt. It must not offer an automatic force-kill fallback. Report successful exits even when one app remains.

### Zero gain and empty cases

- No recognized apps running: **Nothing to close.** Keep available RAM visible; do not invent optimization work.
- Selected apps already exited: **These apps are already closed.**
- Memory measurement failure: **RAM change unavailable.** Exit results remain independently reportable.
- Zero/negative measured change: **Available RAM changed by −0.2 GiB. Other activity can affect this reading.** Never clamp a negative result to zero or claim a gain.
- Protected app missing: **Discord is not running · protected if opened** rather than claiming it is running.

### Style

Use Segoe UI/system text, warm off-white surfaces in light appearance, near-charcoal surfaces in dark appearance, coral for the one primary action and selected-memory segment, and restrained green/amber for text statuses. Respect system contrast and reduced-motion preferences. The app should feel like a small utility, not a gaming launcher.

Baseline light tokens: background `#F5F3EE`, surface `#FFFEFA`, text `#242921`, secondary `#646A5F`, border `#DEDFD5`, accent `#F58A65`, accent foreground `#2A211B`. Dark tokens: background `#171917`, surface `#20231F`, text `#F2F3EC`, secondary `#ADB5A5`, border `#3D4338`.

Use 10–12 px control/surface corners, 32 px content padding, 14 px body copy, 36 px headline, and tabular numerals for RAM. Production app rows use icons extracted from verified installed executables/packages. Prototype letter marks are deliberate placeholders, not official third-party logos.

Accessibility: keyboard-operable checkboxes and buttons, visible focus, descriptive checkbox labels, non-color status text, a polite live region announcing milestones rather than every memory sample, and usable layouts at 200% scaling. Do not announce animation frames. Respect native Windows high-contrast appearance.

## 5. What counts as a clean exit

An app counts as **Exited cleanly** only when:

1. An approved adapter issued an ordinary application Quit/Exit request, or the app had already exited before any request.
2. Every identified in-scope process belonging to that logical application is gone.
3. The original processes' handles indicate exit, and rediscovery finds no replacement app process during a five-second observation window.

An acknowledged window-close message is not sufficient. Some apps hide in the notification area. A successful API return is not evidence that the application actually exited. Microsoft explicitly distinguishes requesting a window close from forced termination [1].

The five-second observation is bounded evidence, not a guarantee that an independent updater will never launch the app later. Phrase detailed reports as **No app processes detected during verification**. Do not disable or kill an updater/service to manufacture success.

### Adapter strategy

Use a documented app exit interface when one exists. Otherwise use verified Windows UI Automation to invoke the app's own Exit/Quit menu item. A normal window-close request is acceptable only for variants tested to exit completely through that path. Do not use arbitrary guessed command-line flags, hard-coded screen coordinates, or translated menu text as the sole identifier.

If an app displays a save/meeting/confirmation prompt, leave it visible and classify the result as **Needs attention**. If the available UI cannot be identified reliably, skip that app. No hidden responses to prompts and no `TerminateProcess`, `taskkill /F`, or `Process.Kill` in the v1 exit path.

Normal exiting may end calls or background tasks. The product promises ordinary shutdown, not universal detection of active meetings, unsaved work, or terminal jobs. The selected-app list makes the scope clear before execution. App prompts are respected where the app provides them.

### Compatibility work required before release

| App | What the adapter must establish | Critical verification |
|---|---|---|
| Edge | Supported full browser exit across this user's windows | Multiple profiles, background/startup-boost behavior, multiple windows, download prompts; no policy edits |
| Teams | Current distribution's actual Quit operation | Closing to tray, WebView ownership, call/meeting prompts |
| Zoom | Actual Exit operation for desktop client | Tray residency, active call/recording prompts, helper ownership |
| WhatsApp | Package-specific ordinary exit | Store/unpackaged identities, multiple-window or background behavior |
| Cursor | Application Exit, not merely one editor window | Multiple workspaces, dirty buffers, terminals/tasks, unowned child processes |
| ChatGPT | Installed distribution's ordinary Quit/Exit | Companion windows and app-owned helper processes |
| Claude | Installed distribution's ordinary Quit/Exit | Tray state and separation from Claude Code or unrelated command-line workers |
| Discord | Protection only | Never receive close messages; name collision must not bypass protection |

**No per-app exit method has been implemented or validated in this design phase.** Ship an adapter as supported only after reproducing these checks on identified versions. Store a compatibility record with tested version range, package/install type, locale constraints, exit mechanism, and evidence. Unknown versions may remain visible but unselected/unsupported until checked.

## 6. Discovery, identity, and process boundaries

Enumerate apps only in the initiating user's Windows session. Recognize installed apps using executable path, package identity where applicable, and verified publisher/install-root metadata. Capture each PID together with creation time and an open process handle when possible. Never trust PID alone: Windows may reuse it between discovery and action.

Group verified application-owned child/helper processes under the app row. Generic `msedgewebview2.exe`, `node.exe`, `bun.exe`, `python.exe`, or `electron.exe` names are never a cleanup target by themselves. Ownership needs evidence from the actual parent chain plus install/package identity. Shared or ambiguous processes stay excluded and are recorded as unresolved. If ambiguity prevents validating full exit, say so instead of reporting complete success.

Cursor launching a development server does not authorize killing that server. The same applies to Claude Code, Claude-Mem workers, terminals, WSL, or browser-launched helper applications. Only normal app exit behavior and verified adapter scope apply. Protected identities are checked again immediately before each action, including all individual identities in a grouped row.

Freeze target identities at run start. Rediscover replacements solely to verify exit/relaunch; do not chase and repeatedly terminate a restarting app. An app launched by the user during cleanup should not become a new automatic target.

## 7. Measurement rules

### Per-app RAM

Use current private working set, summed over verified member processes. `GetProcessMemoryInfo` with `PROCESS_MEMORY_COUNTERS_EX2.PrivateWorkingSetSize` exposes the required metric on supported recent Windows builds [2, 3]. Feature-detect availability. If unavailable, use a documented private-working-set fallback or show **RAM unavailable**; never quietly relabel private commit or total working set as private resident RAM.

Private commit can be retained in diagnostic details, explicitly labeled. It is not the same as physical RAM. Summed total working sets can double-count shared pages. The selected total is measured usage, not a guaranteed savings estimate.

Sampling: once per second while the window is visible, reduced or paused while minimized. Update values in place; do not reorder rows under the pointer. Re-sort on initial discovery or deliberate refresh. Do not perform periodic work after exit.

### Available RAM before and after

Use `GlobalMemoryStatusEx` and `MEMORYSTATUSEX.ullAvailPhys`; total is `ullTotalPhys` [4]. Available memory includes reusable memory; do not substitute an apparently smaller free-only count or trigger cache purges.

Maintain a small in-memory sampling buffer. Before issuing exit requests, take the median of three samples approximately 500 ms apart. After exit verification completes, take the median of three samples approximately 500 ms apart. Compute `afterAvailableBytes - beforeAvailableBytes`. If samples fail, keep the exit report and mark the delta unavailable. Retain raw values and timestamps in the local run report.

Round for display only. Store bytes, use GiB = 2^30 and MiB = 2^20, and show consistent units. If rounding would display `+0.0`, display **No measurable change** with the raw value available in details. Never claim all system-wide change was caused solely by this app.

## 8. Execution architecture

Recommended implementation: a native C# WPF desktop application on the supported .NET LTS available at implementation time, with Win32 interop for identity, memory, and exit verification. Pin the chosen supported SDK and dependencies in the repository after checking Microsoft's support policy [5]. This is a Windows-only utility; a browser runtime is unnecessary for the shipped UI.

Proposed modules:

| Module | Responsibility |
|---|---|
| AppCatalog | Logical identities, protected list, tested compatibility metadata |
| DiscoveryService | Current-session app/process grouping, instance identity |
| MemorySampler | Per-app physical usage and system available memory |
| ExitAdapterRegistry | Version-specific ordinary exit operations |
| CleanupCoordinator | Frozen selection, bounded execution, cancellation, verification |
| ProfileStore | Atomic local preferences and schema migration |
| RunReportStore | Latest outcome with diagnostic details |
| DesktopUI | Main window, preferences, progress, result accessibility |
| CommandEntry | Saved-profile one-shot execution and single-instance coordination |

Adapter interface concept:

```csharp
interface ICleanExitAdapter {
    SupportResult CheckSupport(AppInstance instance);
    Task<ExitRequestResult> RequestExitAsync(
        AppInstance instance, CancellationToken cancellationToken);
}
```

Support checks return the tested identity/version and why an app is or is not supported. The coordinator owns verification; adapters cannot assert final success themselves.

Run state machine: `Discovering → Ready → MeasuringBefore → Closing → Verifying → MeasuringAfter → Completed | Partial | Canceled`. Unrecoverable discovery failure produces an error without sending any exit request.

App state machine: `Detected → Selected | Protected | Unsupported → ExitRequested → Waiting → Exited | NeedsAttention | StillRunning | AccessDenied | Relaunched | Canceled`. `AlreadyExited` is separate from an app the utility closed.

Issue UI-driven exit requests serially to avoid competing focus/menu interactions. Observe process exits concurrently. Target timeout: 15 seconds per app plus five seconds for relaunch observation; a global cap of 90 seconds stops issuing new requests and reports the remainder. All waits are asynchronous. A prompt may be detected early and left for the user. Timeout means incomplete, never permission to kill.

Production keeps user activity in mind: if UI automation would steal focus while the user is typing, defer that adapter briefly or return **Needs attention**. Use automation patterns instead of synthetic keystrokes wherever available.

## 9. Persistence and privacy

Store versioned JSON under `%LOCALAPPDATA%\GameFromHome\`. Proposed files: `preferences.json`, `profiles.json`, and `last-run.json`. Write atomically with a temporary sibling file and replacement. Corrupt or incompatible profiles must fail closed into setup, not expand the target list.

Profile fields: schema version, profile ID, display name, approved logical app identities, protected identities, remember-selection preference, result auto-exit preference. Do not store transient PIDs as future targets.

Run report fields: run ID, application version, adapter versions, start/end time, status, before/after samples, verified app identities and process-instance IDs, requested action, final outcome, elapsed time, error category. Do not capture window titles, URLs, message contents, document text, or terminal commands by default. Avoid telemetry and network calls in the cleanup path.

No administrator prompt on normal launch. Apps outside accessible privilege/session boundaries are skipped with **Access unavailable**. Do not elevate the entire utility automatically. No service, driver, startup entry, scheduled task, or persistent global hotkey listener is installed.

## 10. Acceptance criteria and release gates

| Scenario | Required result |
|---|---|
| Approved apps running | One press initiates clean exit for saved selection; no repeated confirmation |
| Discord active | Same protected process instances remain running and receive no exit request |
| Window closes but tray process remains | App reports still running; no false green success |
| App asks a question | Prompt stays intact; row reports needs attention |
| App hangs | Timeout yields partial result; no forced termination |
| Multiple Edge windows/profiles | Supported adapter exits declared scope or accurately reports remainder |
| Generic WebView/node/bun processes | Only proven app members are measured; unrelated processes untouched |
| PID reuse or new instance | Identity revalidation prevents action on the wrong process |
| App relaunches during verification | Report relaunched; do not chase it |
| No permission / unknown version | Skip with an explicit reason |
| No apps selected/running | Disabled action or useful empty result; no fake work |
| System memory rises elsewhere | Report signed measured delta honestly |
| Repeated button/shortcut invocation | Only one active run |
| User stops cleanup | No new requests; already-requested exits still verified |
| Utility closes | Zero resident Game From Home processes remain |
| Network disconnected | Full cleanup operation still works |
| 200% DPI / keyboard / high contrast | Controls usable, no clipped critical text, statuses perceivable |

Automated tests should exercise identity matching, exclusions, state transitions, timeout/cancellation, measurement arithmetic, and malformed configuration. Use controlled fixture applications that simulate tray hiding, prompting, hanging, spawning children, and relaunching. Integration tests against real apps belong in a disposable Windows test account with known versions. Never run destructive exit tests on the user's working applications as part of design validation.

Performance targets to validate, not claims: ready within two seconds on a representative PC, under 100 MiB private working set for the utility after warm-up, under 1% average CPU at idle with the window visible, and no background footprint after exit. If a self-contained native package exceeds these budgets, profile it before shipping.

Release gate: every enabled app adapter has passing evidence for supported versions; partial exit cannot be mislabeled success; Discord and unrelated runtime processes survive; no force-termination or global service-management API is reachable from cleanup.

## 11. Delivery plan

1. Approve the visual design and copy. Deliverable: this interactive prototype, icon, and spec.
2. Build the read-only native shell, app grouping, memory measurement, and protected list.
3. Implement and validate adapters one by one. Unknown variants remain unsupported until verified.
4. Add orchestration, exception results, local profiles, and single-shot shortcut execution.
5. Run the acceptance matrix, accessibility checks, and resource budget measurements; package portable Windows x64 binaries with an explicit license and signed releases when signing infrastructure is available.

A permissive open-source license such as MIT is a suitable proposed distribution choice. This design package does not claim that an application repository or signed release already exists.

## 12. Prototype boundaries

The supplied interactive mockup demonstrates selection, preferences, cleanup animation, results, a configurable attention case, and returning to the preview. It includes no backend. It does not yet model production **Stop remaining**, **Show app**, **Retry remaining**, actual version checks, the CLI shortcut, native OS caption controls, or live preference persistence. These remain implementation requirements above.

Prototype design controls allow Coral/Mint accent, comfortable/compact rows, and successful/attention outcomes. They are review controls, not features proposed for the shipped product. Simulated recovery uses a fixed factor only to illustrate an honest difference between selected usage and a reported before/after delta; production must never use that factor.

## 13. References

Reviewed 21 September 2026. These sources establish Windows behavior; they do not validate app-specific quit adapters.

1. [Microsoft: Process.CloseMainWindow](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.closemainwindow?view=net-10.0) — a close request allows normal processing but does not force the application to quit.
2. [Microsoft: GetProcessMemoryInfo](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo) — retrieving memory counters and required process access.
3. [Microsoft: PROCESS_MEMORY_COUNTERS_EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2) — private working set versus commit fields and supported Windows versions.
4. [Microsoft: MEMORYSTATUSEX](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex) — available/total physical memory and memory load.
5. [Microsoft: .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) — choose and pin a supported release during implementation.
