---
phase: 07-harvest-controls-stats
plan: 03
subsystem: harvest-scheduler
tags: [harvest, schedule, cache, hosted-service, feature-flag-gate, periodic-timer, background-service]

# Dependency graph
requires:
  - phase: 07-harvest-controls-stats
    provides: "IHarvestScheduleStore + HarvestScheduleSnapshot + IHarvestRunStore.GetLastSuccessUtcAsync (Plan 07-01)"
  - phase: 06-admin-shell-flags-foundation
    provides: "IFeatureFlagCache + harvest.cron.enabled flag (default-on); FeatureFlagCache shape mirrored line-for-line"
provides:
  - "IHarvestScheduleCache contract (Snapshot + ReloadAsync)"
  - "HarvestScheduleCache: sealed BackgroundService — sync StartAsync initial load + 30s PeriodicTimer poller + atomic snapshot replace + preserve-on-failure"
  - "HarvestScheduleService: sealed BackgroundService — 60s tick, harvest.cron.enabled-gated, fires EnqueueAsync(60min) when due"
affects: [07-04-admin-harvest-controller, 07-07-di-wiring, harvest-scheduler-tick-loop]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cache + BackgroundService dual-shape mirroring FeatureFlagCache (Phase 6 D-14)"
    - "Per-tick try/catch isolating transient failures from the BackgroundService loop (T-07-14)"
    - "Flag-gate-first short-circuit ordering (no PG read when kill switch is off)"

key-files:
  created:
    - DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs
    - DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs
    - DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs
  modified: []

key-decisions:
  - "Mirrored FeatureFlagCache exactly: BackgroundService base + sync StartAsync ReloadAsync + 30s PeriodicTimer + volatile snapshot field"
  - "Default snapshot is (IntervalHours=null, Paused=false, UpdatedUtc=DateTimeOffset.MinValue) — Off / unpaused so any pre-load tick is a no-op"
  - "Preserve-on-failure logging: ReloadAsync logs Harvest.Schedule.ReloadFailure but never overwrites a good snapshot with a stub on PG failure"
  - "TickAsync ordering: flag gate -> snapshot read -> Paused/Off short-circuit -> last_success PG read -> due check -> EnqueueAsync; the cheapest checks happen first"
  - "First-run semantics: when last_success_utc is null, fire immediately on the first eligible tick (do not wait an entire interval after enabling cron)"
  - "Per-tick try/catch wraps TickAsync; OperationCanceledException on stoppingToken propagates (normal shutdown), all other exceptions log and continue (T-07-14)"
  - "FireDuration constant TimeSpan.FromMinutes(60) (matches D-04 60-minute Run-Now cap)"

patterns-established:
  - "Hosted cache services use a private static readonly DefaultSnapshot record so the volatile field is always non-null pre-load"
  - "Flag-gate ordering: IsEnabled() check is the first line of TickAsync — protects PG and the job service when the operator flips the kill switch"
  - "Field constants for tick / poll intervals (TickInterval, PollInterval) — single source of truth for the tick cadence number"

requirements-completed: [HARV-04, HARV-05]

# Metrics
duration: ~7min
completed: 2026-05-03
---

# Phase 7 Plan 03: Schedule Cache + Tick Service Summary

**Mirrored FeatureFlagCache to ship IHarvestScheduleCache + HarvestScheduleCache, then layered HarvestScheduleService — a 60s flag-gated BackgroundService that fires bulk harvests when `now >= last_success + interval_hours` (or immediately when there is no prior success).**

## Performance

- **Duration:** ~7 min
- **Tasks:** 2 / 2
- **Files created:** 3
- **Files modified:** 0
- **Build:** `dotnet build DeckFlow.sln` exits 0 (no warnings, no errors)

## Accomplishments

- Hot-reloadable in-memory snapshot of the `harvest_schedule` row, sized for lock-free reads on the scheduler hot path
- 30-second PeriodicTimer backstop refresh + sync StartAsync initial load (no cold-start window where the snapshot is stale)
- Recurring scheduler that respects the Phase 6 `harvest.cron.enabled` kill switch — flipping the flag at `/Admin/Flags` instantly silences the loop without a deploy
- Robust per-tick error isolation: a transient PG failure or a job-service hiccup never kills the BackgroundService loop
- Pre-Plan-07 dormancy: nothing is DI-registered yet, so the new code is shipped but inert until the wiring plan lands — keeps Wave 2 mergeable independently

## Task Commits

Each task was committed atomically:

1. **Task 1: HarvestScheduleCache (interface + sealed BackgroundService impl)** — `16dcae5` (feat)
2. **Task 2: HarvestScheduleService BackgroundService — 60s tick, flag-gated, fires bulk harvest when due** — `bb0f0ea` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs` — Cache contract: `Snapshot()` + `ReloadAsync(CancellationToken)`. Analog of `IFeatureFlagCache`.
- `DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs` — Sealed `BackgroundService` + `IHarvestScheduleCache`. 30s `PeriodicTimer` poller, sync StartAsync initial load, atomic `volatile` snapshot replace, preserve-on-failure logging.
- `DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` — Sealed `BackgroundService`. 60s `PeriodicTimer` loop. Per-tick: flag gate → snapshot read → pause/Off short-circuit → `IHarvestRunStore.GetLastSuccessUtcAsync` → due check → `IArchidektCacheJobService.EnqueueAsync(TimeSpan.FromMinutes(60))`.

## Decisions Made

- **Mirrored FeatureFlagCache shape exactly** so the harvest cache and the flag cache stay literally interchangeable mental models for future maintainers — same lifecycle, same constants, same preserve-on-failure semantics.
- **Field constants for the cadence numbers** (`PollInterval = 30s`, `TickInterval = 60s`, `FireDuration = 60min`, `CronEnabledFlagKey = "harvest.cron.enabled"`) so a future tweak only edits one line.
- **Flag-gate first** in `TickAsync` so toggling `harvest.cron.enabled` Off saves a PG round-trip every 60s on top of disabling the harvest fire — the kill switch is performance-positive too.
- **First-run fire-immediately** when `last_success_utc` is null. Operator flipping cron On after a fresh deploy doesn't have to wait 24h to see anything happen.
- **Default snapshot is Off / unpaused / `DateTimeOffset.MinValue`.** Even if the scheduler ticks before the first reload completes (impossible in practice because StartAsync awaits ReloadAsync, but defensive), the Paused/Off short-circuit means a no-op rather than an erroneous fire.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Installed missing TypeScript build infrastructure in worktree**

- **Found during:** Task 1 (initial `dotnet build` after first file creation)
- **Issue:** The worktree was provisioned without `DeckFlow.Web/package.json` and `DeckFlow.Web/node_modules`. The `CompileTypeScriptAssets` MSBuild target executed `node ./node_modules/typescript/bin/tsc -p tsconfig.json` and failed with `Cannot find module … tsc` (MSB3073, exit code 1). Both `package.json` (gitignored per repo `.gitignore:9`) and `node_modules/` (gitignored) are intentionally not tracked, so a worktree creation that did not also bootstrap the npm install left builds broken.
- **Fix:** Copied `DeckFlow.Web/package.json` from the main repo working tree into the worktree and symlinked `DeckFlow.Web/node_modules` to the main repo's already-installed `node_modules`. Both targets are gitignored — neither is included in either Task 1 or Task 2 commits.
- **Files modified:** `DeckFlow.Web/package.json` (gitignored), `DeckFlow.Web/node_modules` (gitignored symlink)
- **Verification:** `dotnet build DeckFlow.sln` now exits 0 with 0 warnings and 0 errors after both tasks.
- **Committed in:** N/A (gitignored — no commit, intentional)

---

**Total deviations:** 1 auto-fixed (1 blocking — worktree build infrastructure)
**Impact on plan:** Necessary to run `dotnet build` at all in this worktree. No source-code or scope changes. Both production tasks executed exactly as written in the plan.

## Issues Encountered

- None during the actual implementation. The TypeScript build chain bootstrap was a one-time worktree setup cost (covered in Deviations).

## Plan-Spec Quick-Reference

(Per the plan's `<output>` block — record the cadence numbers and flag key.)

| Constant                    | Value                       | Source                                                                          |
| --------------------------- | --------------------------- | ------------------------------------------------------------------------------- |
| Cache poll interval         | 30 seconds                  | `HarvestScheduleCache.PollInterval` (mirrors `FeatureFlagCache.PollInterval`)   |
| Schedule tick interval      | 60 seconds                  | `HarvestScheduleService.TickInterval`                                           |
| Fire duration (per fire)    | 60 minutes                  | `HarvestScheduleService.FireDuration`                                           |
| Kill-switch flag key        | `harvest.cron.enabled`      | `HarvestScheduleService.CronEnabledFlagKey`                                     |
| Default cache snapshot      | (null, false, MinValue)     | `HarvestScheduleCache.DefaultSnapshot`                                          |

**Deviations from FeatureFlagCache pattern:** None of substance. The harvest cache differs only where the underlying type forces it: `_snapshot` is `volatile HarvestScheduleSnapshot` (a sealed record reference) rather than `volatile IReadOnlyDictionary<…>`, and there is no `WarnMissingKeyOnce`/missing-key path because the schedule has a single seeded row (no concept of "missing key"). The reload, StartAsync, and ExecuteAsync bodies are line-for-line equivalent.

## User Setup Required

None — no external service configuration. DI wiring is deferred to Plan 07-07.

## Next Phase Readiness

- Plan 07-04 (admin controller + view) can wire `IHarvestScheduleCache.ReloadAsync()` from the admin write path immediately on top of this commit.
- Plan 07-07 (DI registration) needs to register the cache as Singleton + IHostedService dual (mirroring `AddDeckFlowFeatureFlags`) and `HarvestScheduleService` as IHostedService — neither is wired here so Wave 2 can land independently.
- Both BackgroundServices are inert until DI registration, so this commit is safe to merge ahead of Plan 07-07 without any runtime behavior change.

## Self-Check

Verifications run:

- `[ -f DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs ]` → FOUND
- `[ -f DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs ]` → FOUND
- `[ -f DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs ]` → FOUND
- `git log --oneline | grep 16dcae5` → FOUND (Task 1)
- `git log --oneline | grep bb0f0ea` → FOUND (Task 2)
- `dotnet build DeckFlow.sln --nologo` → 0 warnings, 0 errors

## Self-Check: PASSED

---
*Phase: 07-harvest-controls-stats*
*Plan: 03*
*Completed: 2026-05-03*
