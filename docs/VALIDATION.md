# v0.2.1 threshold verification

The build passed with zero warnings/errors. The native screen was inspected with live data: only four grouped entries above 500,000,000 bytes appeared; smaller entries were excluded from the selection. The shutdown implementation is unchanged from the 35-check v0.2.0 validation below.

# Validation — v0.2.0

Validated on Windows 11 x64, 21 September 2026. Application, core, and fixtures build with zero warnings/errors. **35 regression checks passed**; see [test-results.json](test-results.json).

Checks cover normal/hidden exits, vetoes, protected targets, incomplete/stale identities, remaining children, cancellation, profile validation, Codex opt-in selection, runtime/worker classification, health PID matching, local TCP ownership, and deduplicated discovery. Only disposable test-owned apps were closed.

Read-only live verification found Codex, the installed claude-mem 13.25.2 worker, Bun/Node processes, discovered desktop apps, WSL, and Memory Compression. The worker health endpoint matched its PID; no live worker shutdown was requested. Actual Codex and claude-mem shutdown remain untested in this session to avoid interrupting the user's work. The worker adapter has no forced fallback.

The native UI was rendered with real process/memory data. Its values are a snapshot, not a benchmark. Searchable virtualized rows show the broader inventory without rendering every row at once. Individual versions of every third-party app are not certified for cooperative exit.

The workflow template in `ci/windows-build.yml` is included but is not active in GitHub Actions. Full disposable fixture tests run via `build.ps1 -Test`.
