# Implementation notes

Native WPF UI and a separate core library provide app discovery, memory sampling, cooperative shutdown, and independent verification. Selection is reviewed and stored locally.

## Identity

Catalog rules combine installation paths/package prefixes with executable names. Process instances use PID, creation time, path, user, and session. Protected paths and the utility itself are rejected again before shutdown.

Helpers require verified ancestry and install roots. WebView also requires the expected runtime path. Arbitrary developer runtimes are excluded. Unknown or inaccessible identity causes a skip rather than a guess.

## Exit

Restart Manager registers exact root process instances, never files or services. The affected list must stay within that scope and contain no service, Explorer, console, or critical-process type. `RmShutdown(session, 0, 0)` disables forced shutdown.

Windows sends cooperative end-session notifications; app code decides whether to exit. There is no reboot, app restart, priority change, cache purge, driver action, or forced fallback. Remaining processes produce an incomplete result. API return codes alone do not prove an exit.

Original instances and replacements are observed for five seconds. Cancellation stops new requests without undoing prior exits. In-flight requests use Restart Manager's supported cancellation call.

## RAM and persistence

`PROCESS_MEMORY_COUNTERS_EX2.PrivateWorkingSetSize` supplies private resident memory. `GlobalMemoryStatusEx` supplies available and total physical memory. Three before/after samples about 500 ms apart produce a signed median difference. Logs retain bytes; display uses GiB/MiB.

Live discovery runs while the window is open. Cleanup completion stops the polling timer. Closing leaves no worker or tray process.

`%LOCALAPPDATA%\GameFromHome\preferences.json` and `last-run.json` hold configuration/outcomes. Writes are atomic; invalid schemas require review. Logs omit chat contents, URLs, documents, and window titles. Installation fingerprints keep changed installations out of automatic runs until reviewed.

## References

- [Windows cooperative shutdown protocol](https://learn.microsoft.com/en-us/windows/win32/rstmgr/guidelines-for-applications)
- [RmShutdown](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmshutdown)
- [Exact resource registration](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmregisterresources)
- [Private working set](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2)
- [Available physical memory](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex)
