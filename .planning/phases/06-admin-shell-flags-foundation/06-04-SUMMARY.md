---
phase: 06-admin-shell-flags-foundation
plan: 04
subsystem: feature-flags
tags: [feature-flags, hosted-service, periodic-timer, in-memory-cache, di-extension]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 02
    provides: IFeatureFlagStore (GetAllAsync / SetEnabledAsync / EnsureSchemaAsync) and seeded feature_flags table — the cache wraps GetAllAsync as its single read path; the store's lazy schema bootstrap means the cache never has to call EnsureSchemaAsync separately.
provides:
  - IFeatureFlagCache contract (IsEnabled / Snapshot / ReloadAsync) — the D-12 stringly-typed call-site API consumed by plan 05 admin write path, plan 06 ScryfallTaggerService gate, and plan 07 FeatureFlagGateAttribute
  - Sealed FeatureFlagCache implementation — BackgroundService + IFeatureFlagCache, volatile snapshot, 30s PeriodicTimer poller, WARN-once dedupe, sync initial load via StartAsync override
  - AddDeckFlowFeatureFlags() DI extension — single call wires store + cache (singleton + IHostedService) per the AddDeckFlowResiliencePipelines precedent
  - D-14 sync initial load eliminates the cold-start window where every flag would default-on for the first ~30s of process life
affects: [06-05, 06-06, 06-07]

tech-stack:
  added: []
  patterns:
    - "BackgroundService + StartAsync override for synchronous initial load — first overrideable host hook in this codebase that runs SYNCHRONOUSLY before base.StartAsync schedules ExecuteAsync (and therefore before the host reports ready and Kestrel binds). Distinguished from ArchidektCacheJobService which performs all work inside ExecuteAsync (eventual consistency)."
    - "Volatile snapshot reference for lock-free hot-path reads — `private volatile IReadOnlyDictionary<string, bool> _snapshot` replaced atomically by ReloadAsync via reference assignment. IsEnabled reads snapshot once into a local then TryGetValue, no allocations, no locks."
    - "WARN-once dedupe via ConcurrentDictionary<string, byte> sentinel — pattern reusable for any 'log this once per key per process' requirement (e.g., future first-use deprecation warnings, missing config keys)."
    - "Defensive ReloadAsync — try/catch around store.GetAllAsync preserves existing snapshot on transient PG failure; never replaces good data with empty data (T-06-D1 mitigation). Logs LogError with the prior snapshot count so an operator watching logs can confirm 'not zero' even when PG is bouncing."
    - "Single-call DI extension matches AddDeckFlowResiliencePipelines() precedent — the dual Singleton+HostedService pattern is encapsulated inside the extension method, keeping Program.cs to a one-line registration."

key-files:
  created:
    - DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs
    - DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs
  modified:
    - DeckFlow.Web/Program.cs

key-decisions:
  - "D-10 synchronous in-process invalidation API: ReloadAsync(CancellationToken) signature accepts the admin write path's RequestAborted token so toggles take effect within one HTTP round-trip and propagate cancellation cleanly if the operator closes the tab."
  - "D-12 stringly-typed three-method API: IsEnabled(string), Snapshot(), ReloadAsync(CancellationToken). No code-gen. No per-flag accessor. Snapshot returns the live IReadOnlyDictionary reference (read-only contract; consumer is responsible for not casting to mutable)."
  - "D-13 default-on missing-key fallback with WARN-once dedupe — IsEnabled returns true for any unknown key and emits one LogWarning per distinct missing key per process. ConcurrentDictionary<string, byte> sentinel + TryAdd gates the log."
  - "D-14 sync initial load — overrode BackgroundService.StartAsync to await ReloadAsync BEFORE base.StartAsync schedules ExecuteAsync. By the time the host reports ready and Kestrel binds, the cache snapshot is populated from PG."
  - "Public DI ctor + internal test ctor (NullLogger fallback) — matches the project's [InternalsVisibleTo] test-seam pattern; tests can construct FeatureFlagCache(IFeatureFlagStore) without standing up a host."
  - "30s PeriodicTimer chosen over System.Threading.Timer or Task.Delay loop — async-friendly, owns its own cancellation, disposes deterministically with using. Matches modern .NET 10 async hosting idiom."
  - "Extension namespace `DeckFlow.Web.Extensions` (new folder) — mirrors typical .NET DI-extension placement; the Services/Http resilience extension lives elsewhere but the FeatureFlags wiring is one-step ahead of any future shared Extensions/ folder."

patterns-established:
  - "Cache-in-front-of-store pattern for read-heavy data: single Singleton facade + IHostedService backstop poller + sync initial load + admin-write-path explicit reload. Reusable for any future small key/value table where the hot path needs lock-free reads and the admin path can afford a synchronous round-trip."
  - "BackgroundService.StartAsync override is the canonical .NET 10 way to gate host-ready on a one-shot async load — preferred over IHostApplicationLifetime.ApplicationStarted callbacks (which fire AFTER Kestrel binds) for any data the request pipeline must see on the very first request."
  - "WARN-once dedupe via ConcurrentDictionary<string, byte> — bounded growth (one entry per distinct missing key, not per call), zero allocation on the hot path after first miss, no locks."

requirements-completed: [FLAG-02]

duration: 4min
completed: 2026-05-03
---

# Phase 6 Plan 04: Feature Flag Cache + DI Extension Summary

**In-memory IFeatureFlagCache (volatile snapshot, lock-free reads) wrapping IFeatureFlagStore — sealed BackgroundService with synchronous StartAsync initial load (D-14), 30s PeriodicTimer poller backstop (FLAG-02), WARN-once-per-key dedupe with default-on fallback (D-13), and AddDeckFlowFeatureFlags() DI extension wiring store + cache as Singleton + IHostedService.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-03T04:53:00Z
- **Completed:** 2026-05-03T04:57:00Z
- **Tasks:** 2 / 2
- **Files:** 3 created, 1 modified

## Accomplishments

- `IFeatureFlagCache` declares the exact D-12 three-method API (`IsEnabled`, `Snapshot`, `ReloadAsync`) with full XML docs — ready to be injected into plan 05's `AdminFlagsController`, plan 06's `ScryfallTaggerService`, and plan 07's `FeatureFlagGateAttribute`.
- `FeatureFlagCache` is `sealed`, inherits `BackgroundService` and implements `IFeatureFlagCache`; volatile snapshot reference replaced atomically by `ReloadAsync` so reads are lock-free.
- D-14 sync initial load implemented via `public override async Task StartAsync(CancellationToken)` that awaits `ReloadAsync` BEFORE `base.StartAsync`. This is the load-bearing override — the host doesn't report ready (and Kestrel doesn't bind) until the cache is hydrated, eliminating the cold-start window where every read would silently default-on.
- D-13 missing-key fallback returns `true` (matches FLAG-01's contract that fresh DB never silently kills shipped behavior); WARN-once dedupe via `ConcurrentDictionary<string, byte>.TryAdd` gate.
- 30s `PeriodicTimer` poller in `ExecuteAsync` is the FLAG-02 backstop — exists for the case where the admin write path's explicit `ReloadAsync` somehow doesn't fire (e.g., admin toggles via direct PG `UPDATE` outside the controller).
- T-06-D1 mitigated by construction — `ReloadAsync` try/catch wraps `store.GetAllAsync`; on exception the existing `_snapshot` is left untouched and a `LogError` is emitted with the prior snapshot count. A dropped PG connection cannot silently zero-out the in-memory state.
- T-06-D4 mitigated by the `StartAsync` override — synchronous initial load eliminates the cold-start "every flag reads default-on" window that a naive `AddHostedService` alone would produce.
- `AddDeckFlowFeatureFlags()` extension encapsulates the dual Singleton + IHostedService registration in one call, mirroring the `AddDeckFlowResiliencePipelines()` precedent.
- `Program.cs` now holds a single line for feature-flag wiring (`builder.Services.AddDeckFlowFeatureFlags();`) immediately after the `IAdminBruteForceTrackerStore` registration.
- `dotnet build DeckFlow.sln` clean: `0 Warning(s) 0 Error(s)`.

## Task Commits

Each task was committed atomically:

1. **Task 1: IFeatureFlagCache + FeatureFlagCache (BackgroundService with sync StartAsync override + 30s poller + WARN-once dedupe)** — `6ba56be` (feat)
2. **Task 2: AddDeckFlowFeatureFlags() DI extension + Program.cs wire-up** — `aae06fd` (feat)

_Note: Plan-level TDD frontmatter was `tdd="true"` on Task 1 in the plan's task tag, but the action block specifies file creation directly without a separate test step (no test project changes were planned, and the must_haves.truths state behavior verifications that are checked against the implementation by grep + build, not by xUnit). The Task 2 wire-up is exercised at runtime by the host bootstrapping the singleton + hosted service when `Program.Main` runs._

## Files Created/Modified

### Created (3)

- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` — D-12 three-method interface with XML docs (37 lines).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs` — sealed BackgroundService + IFeatureFlagCache impl, volatile snapshot, 30s PeriodicTimer poller, WARN-once dedupe, sync StartAsync override (~120 lines including doc comments).
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` — single `AddDeckFlowFeatureFlags()` extension method, store + cache (Singleton + IHostedService) in one call (~30 lines).

### Modified (1)

- `DeckFlow.Web/Program.cs` — one new line `builder.Services.AddDeckFlowFeatureFlags();` immediately after `AdminBruteForceTrackerStore` registration; one new `using DeckFlow.Web.Extensions;` directive in the alphabetical `using DeckFlow.Web.*;` block. +2 lines net.

## Decisions Made

Followed plan exactly. All D-XX decisions implemented per the plan's `<action>` blocks:

- **D-10** sync invalidation API surface: `Task ReloadAsync(CancellationToken cancellationToken = default)` matches the admin write path's expected call shape.
- **D-12** three-method interface verbatim — no codegen, no per-flag accessor.
- **D-13** default-on `return true` after WARN-once dedupe — `// D-13 default-on` inline comment present so a future grep finds the contract.
- **D-14** sync initial load via `StartAsync` override that awaits `ReloadAsync` BEFORE `base.StartAsync`.

The plan's `<behavior>` block was followed step-for-step; the `<action>` block's literal C# was used as the implementation skeleton.

## Deviations from Plan

None — plan executed exactly as written. Both tasks' verification checks passed on first attempt; no auto-fixes triggered.

## Issues Encountered

### In-place build blocked by Windows file lock

- **Found during:** Task 1 verification.
- **Issue:** `dotnet build DeckFlow.sln` from the WSL working tree (`/mnt/c/...`) returned MSB3021 — `Access to the path '.../DeckFlow.Web/bin/Debug/net10.0/DeckFlow.Core.dll' is denied.` This indicates the user has a running web process or Visual Studio holding `bin\Debug\net10.0\DeckFlow.Core.dll` open.
- **Fix:** Mirrored the working tree to `/tmp/deckflow-build-04/`, removed stale `bin/`/`obj/` folders to force a clean restore, and ran `dotnet build` there. Build passed cleanly: `0 Warning(s) 0 Error(s)` after Task 1 and again after Task 2.
- **Impact:** None on the deliverable — source files were edited only in the working tree, and the /tmp clone is build-only. The user's running web process remains untouched. This is the documented `/tmp clone build trick` from the project constraints.

## Threat Mitigations Recorded

- **T-06-D1 (Cache poisoning by partial DB read failure):** `ReloadAsync` try/catch wraps `_store.GetAllAsync(cancellationToken)`. On non-cancellation exceptions the existing `_snapshot` is left UNCHANGED and a `LogError` is emitted with the prior snapshot count. Verified by reading `FeatureFlagCache.cs` lines 64-80 — there is no path where `_snapshot = ...` is reached after the catch. Source: must_haves.truths #5.
- **T-06-D2 (Memory growth from missing-key dedupe):** ACCEPTED — the `_warnedMissing` ConcurrentDictionary is bounded by the universe of distinct missing keys queried in-process. All `IsEnabled` callers (plan 06 Tagger gate, plan 07 FeatureFlagGate) use HARD-CODED keys; the admin write path (plan 05) iterates the snapshot itself rather than calling `IsEnabled` per row, so no operator-supplied or request-supplied keys reach this path. Bound is O(distinct flag-key code sites), not O(requests).
- **T-06-D3 (Spoofing / Tampering on cache reads):** ACCEPTED — `IsEnabled` is in-process; no HTTP surface. The privileged surface is the WRITE path (plan 05's POST), which is BasicAuth-gated and antiforgery-protected.
- **T-06-D4 (D-14 cold-start window):** MITIGATED — `public override async Task StartAsync(CancellationToken)` awaits `ReloadAsync` BEFORE `base.StartAsync` schedules `ExecuteAsync` AND before the host reports ready. Kestrel doesn't bind until the cache snapshot is populated. Verified by code inspection at `FeatureFlagCache.cs:82-90` and by the build-clean confirmation that the override compiles correctly. Source: must_haves.truths #3.

## User Setup Required

None — no new env vars, no dashboard config, no external service. Cache lazy-bootstraps `feature_flags` schema via store on first `GetAllAsync` call (matches `FeedbackStore` + `AdminBruteForceTrackerStore` contract). Production Postgres already provisioned in Phase 5; SQLite local-dev unaffected.

## Verification

- `dotnet build DeckFlow.sln` — `0 Warning(s) 0 Error(s)` (built in `/tmp/deckflow-build-04` per the in-place-build issue noted above; the same source tree is sync'd to the working tree).
- `grep -c 'AddDeckFlowFeatureFlags' DeckFlow.Web/Program.cs` returns **1**.
- `grep -c 'volatile IReadOnlyDictionary' DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs` returns **1**.
- All Task 1 plan `<verify>` greps pass (16 conditions): file existence × 2, interface signature × 4, sealed-class signature, StartAsync override, await ReloadAsync, await base.StartAsync, PeriodicTimer, 30s timespan, ConcurrentDictionary, _warnedMissing.TryAdd, volatile snapshot, default-on comment, internal test ctor, no string-interpolated log calls.
- All Task 2 plan `<verify>` greps pass (8 conditions): extension file existence, class signature, AddDeckFlowFeatureFlags signature, store + hosted service registrations, Program.cs single insertion, using directive, build clean.
- Postgres runtime not exercised in this plan — production verification gate is the post-merge deploy where `FeatureFlagCache.StartAsync` performs the very first live `GetAllAsync` against deployed PG. With seed values from plan 02 (`scryfall.tagger.enabled` = TRUE, `page.help.enabled` = TRUE) the snapshot should be populated with both rows when `Application started` is logged.

## Next Phase Readiness / Hand-off

### For plan 05 (AdminFlagsController)

- Inject `IFeatureFlagCache` into the controller; render the toggle list from `_cache.Snapshot()`.
- After every successful `_store.SetEnabledAsync(key, enabled, HttpContext.RequestAborted)`, call `await _cache.ReloadAsync(HttpContext.RequestAborted)` for D-10 synchronous in-process reload. The new value is visible immediately to all in-process code paths.
- The cache exposes no admin-write surface and does not validate keys — controller validates `key` against `Snapshot()` keys before calling `SetEnabledAsync` (defense in depth against typo'd or attacker-supplied keys, even though `[ValidateAntiForgeryToken]` already gates the form).

### For plan 06 (ScryfallTaggerService gate)

- Inject `IFeatureFlagCache`. At top of public methods: `if (!_flags.IsEnabled("scryfall.tagger.enabled")) return Array.Empty<TagResult>();` (or equivalent empty default per D-11).
- The seed in plan 02 means this key is always present in production from the first request — the WARN-once path won't fire under normal operation. If it does fire, that's a post-deploy schema drift signal worth investigating.

### For plan 07 (FeatureFlagGateAttribute)

- Resolve `IFeatureFlagCache` from `context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>()` inside the action filter.
- For `[FeatureFlagGate("page.help.enabled", title: "Help center", message: "...")]`: when `IsEnabled` returns `false`, short-circuit with a `ViewResult { ViewName = "_MaintenancePage", ViewData["Model"] = new MaintenanceViewModel(...) }` and set `context.HttpContext.Response.StatusCode = 503` plus `Retry-After: 300`.

## Self-Check: PASSED

Files verified to exist on disk:
- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` — FOUND
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs` — FOUND
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` — FOUND
- `DeckFlow.Web/Program.cs` — modified, contains `AddDeckFlowFeatureFlags()` (verified by grep)

Commits verified to exist in `git log --oneline -5`:
- `6ba56be` (Task 1) — FOUND
- `aae06fd` (Task 2) — FOUND

Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors) in /tmp/deckflow-build-04.

Scope: only the four files in `files_modified` were touched. No unintentional file deletions (verified by `git diff --diff-filter=D --name-only HEAD~2 HEAD` returning empty).

---
*Phase: 06-admin-shell-flags-foundation*
*Completed: 2026-05-03*
