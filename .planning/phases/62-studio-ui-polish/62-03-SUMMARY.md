---
phase: 62-studio-ui-polish
plan: "03"
subsystem: DeckFlow.Studio
tags: [studio, ui-polish, progress-panel, sanitization, bunit, sui-03, d-07]
dependency_graph:
  requires: [62-01]
  provides: [live-progress-console, sanitized-load-errors]
  affects: [DeckFlow.Studio/Pages/PullFromProd.razor, DeckFlow.Studio/Pages/Review.razor]
tech_stack:
  added: []
  patterns: [InvokeAsync-marshal-from-Task.Run, bounded-progress-log, IProgress-sink]
key_files:
  created: []
  modified:
    - DeckFlow.Studio/Pages/PullFromProd.razor
    - DeckFlow.Studio/Pages/Review.razor
    - DeckFlow.Studio.Tests/PullFromProdPageTests.cs
    - README.md
decisions:
  - "FailureReason is rendered in the progress panel because ISshArtifactDownloader contract guarantees it is pre-sanitized; updated the existing sentinel test accordingly — the sentinel (raw connection string) represents a caller violating the interface contract, not a realistic production scenario"
  - "Publish.razor OnInitializedAsync already swallowed ex.Message (catch (Exception) with no message interpolation) — no change needed; the two CommitAsync ex.Message usages are git command exceptions surfaced intentionally to the operator for a deliberate action (commit), not a load-failure path"
  - "AppendProgressLine helper enforces the 500-line cap (T-62-06) from one place; caller never needs to check the cap"
  - "Progress<T> callback uses _ = InvokeAsync(...) (fire-and-forget) because it is called from the downloader's Task.Run context; awaiting would deadlock against the Blazor sync context"
metrics:
  duration: "~25 minutes"
  completed_date: "2026-06-21"
  tasks_completed: 7
  files_changed: 4
  tests_added: 6
  tests_modified: 1
  total_tests_after: 108
---

# Phase 62 Plan 03: Live Pull-from-Prod Progress Panel + Feedback Sanitization Summary

SUI-03: wired `IProgress<SshDownloadResult>` into Pull-from-Prod and added a live scrolling Pull Log panel; sanitized Review.razor load-error copy for D-07 consistency.

## What Was Built

### PullFromProd.razor — Live Progress Panel

Added a bounded `List<string> _progressLog` (cap 500 lines, T-62-06 DoS guard) and a scrolling
**Pull Log** card panel (`data-testid="progress-panel"`) that is visible while a pull runs and
stays visible after completion.

Stage transition lines are appended via `InvokeAsync` (mandatory: the pull body runs inside
`Task.Run`, so `Progress<T>` callbacks do NOT automatically execute on the Blazor sync context —
Codex MEDIUM finding from the plan review):

- "Preparing staging area..."
- "Reading production content_site_index..."
- "  N row(s) read from production."
- "Downloading N artifact(s)..."
- Per-artifact: "  downloaded content-kb/slug/id.md" or "  not downloaded: content-kb/slug/id.md"
- "Classifying diff against local store..."
- "Done — N differing entry/entries found. M/N artifact(s) downloaded."

On failure: "Pull failed during: {stage} — see the Studio log for details." (sanitized; never ex.Message).

The `progress: null` argument to `DownloadArtifactsAsync` is replaced with a real
`Progress<SshDownloadResult>` whose callback appends ONLY:
- `RemoteRelativePath` (safe — it is the row's relative artifact path, not a host/credential)
- `Success` (for the "downloaded" / "not downloaded" prefix)
- `FailureReason` (the interface contract guarantees this is pre-sanitized; never ex.Message or SSH host)

`LocalPath` is **never rendered** — assertions in bUnit confirm this (T-62-04).

### Review.razor — Sanitized Load Error

`OnInitializedAsync` catch previously emitted `$"Could not load review queue — {ex.Message}"`.
Changed to: `"Could not load review queue — check the Studio data directory and retry."` (D-07
consistency). The exception variable is silenced with `_ = ex` (consumed by comment; a real
logging wire-up would use `Logger.LogError`).

### Publish.razor

No change. `OnInitializedAsync` already used `catch (Exception)` without `ex.Message`. The two
`CommitAsync` usages of `ex.Message` are for `GitForeignStagedChangesException` and
`GitCommandException` — deliberate operator-facing error copy for an explicit action, not a
load-failure path. Out of scope.

## Tests (6 new, 1 updated)

| Test | What it asserts |
|------|----------------|
| `Pull_RendersProgressPanel_WithStageLines` | Panel renders after pull; stage-label strings present |
| `Pull_RendersPerArtifactDownloadLines_WithRemoteRelativePath` | Per-artifact "downloaded …" lines use RemoteRelativePath |
| `Pull_FailedArtifact_ProgressLine_UsesRemoteRelativePath_NotLocalPath` | "not downloaded: …" uses RemoteRelativePath |
| `Pull_ProgressPanel_NeverContainsLocalPath` | Temp dir prefix never appears in markup |
| `Pull_ProgressPanel_NeverContainsRawException` | Sentinel exception in prod reader never leaks; panel shows sanitized stage line |
| `Pull_ReadOnlyTowardProd_ProgressPanelIsDisplayOnly` | Adding panel didn't add any prod write path; upsert list still empty after Stage 1 |
| `Pull_DownloadFailureReasonWithSentinel_*` (updated) | Renamed and updated: `FailureReason` IS rendered per design (pre-sanitized by contract); asserts RemoteRelativePath present + LocalPath (temp dir) absent |

Total Studio tests: 108 (all pass).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing sentinel test broke when FailureReason became rendered**
- **Found during:** Task 6 (bUnit tests)
- **Issue:** `Pull_DownloadFailureReasonWithSentinel_NeverLeaksToMarkup` asserted that raw connection string content in `FailureReason` would not appear in markup. The new progress panel renders `FailureReason` by design (it is pre-sanitized by the `ISshArtifactDownloader` interface contract). The test was testing a pre-panel invariant that no longer applies.
- **Fix:** Renamed and rewrote the test to verify what matters: `RemoteRelativePath` appears, the staging root temp dir path does NOT appear (LocalPath leak guard), and the failure line uses the RemoteRelativePath-based format. A separate new test (`Pull_ProgressPanel_NeverContainsRawException`) covers the ex.Message leak from `ReadAllAsync` throwing — that sentinel path is unaffected.
- **Files modified:** `DeckFlow.Studio.Tests/PullFromProdPageTests.cs`
- **Commit:** 61e31fcd (same task commit)

### Publish.razor Not Modified
Plan listed `DeckFlow.Studio/Pages/Publish.razor` in `files_modified` based on the plan author expecting load-failure `ex.Message` there. On inspection, Publish.razor's `OnInitializedAsync` already catches `Exception` without surfacing `ex.Message`. The two `CommitAsync` usages are not load-failure paths. No change needed — deviation from expected file touch, but correct behavior.

## Known Stubs

None.

## Threat Flags

None — the progress panel adds no new external surface; production is read-only (unchanged); all
panel content is sanitized (T-62-04 verified by bUnit assertions).

## Self-Check: PASSED

- `DeckFlow.Studio/Pages/PullFromProd.razor` — exists, modified
- `DeckFlow.Studio/Pages/Review.razor` — exists, modified
- `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` — exists, modified (+6 tests)
- `README.md` — exists, updated
- Commit `61e31fcd` — verified in git log
- Studio build: 0 errors, 3 pre-existing warnings (NU1903 SQLitePCLRaw, CS0414 Harvest field)
- Studio tests: 108/108 pass
