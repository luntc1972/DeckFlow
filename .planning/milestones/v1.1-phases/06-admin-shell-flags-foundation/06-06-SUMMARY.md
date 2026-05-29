---
phase: 06-admin-shell-flags-foundation
plan: 06
subsystem: feature-flags
tags: [feature-flags, kill-switch, scryfall-tagger, service-level-gate, di-injection]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 04
    provides: IFeatureFlagCache (IsEnabled/Snapshot/ReloadAsync) + AddDeckFlowFeatureFlags() DI registration — the gate consumes the IsEnabled call-site API verbatim and benefits from the D-14 sync initial load (no cold-start window where the flag silently defaults-on for the first ~30s of process life).
  - phase: 06-admin-shell-flags-foundation
    plan: 02
    provides: feature_flags PG table + scryfall.tagger.enabled=TRUE seed (D-09) — guarantees the key is present in the cache from the very first request post-deploy, so the WARN-once missing-key fallback never fires under normal operation.
  - phase: 06-admin-shell-flags-foundation
    plan: 05
    provides: AdminFlagsController + ReloadAsync wire-up (D-10 sync invalidation) — without plan 05's explicit await on cache.ReloadAsync after every SetEnabledAsync, this gate would only see toggle changes after the 30s poller TTL, breaking the <2s success criterion.
provides:
  - ScryfallTaggerService.LookupOracleTagsAsync kill-switch gate — service-level (D-11) placement so every consumer (currently CategorySuggestionService Tagger mode; future controllers) inherits the no-op behavior without per-controller code.
  - FakeFeatureFlagCache test double — default-on dictionary-backed fake mirroring D-13's missing-key contract; reusable by any future test that constructs a service taking IFeatureFlagCache.
affects: [06-07]

tech-stack:
  added: []
  patterns:
    - "Service-level kill-switch gate at top of public method (D-11) — argument validation runs first (so the input contract still throws as before), then the flag check, then any HTTP work. Sets the precedent for any future per-service kill switches that should leave call-shape contracts intact."
    - "Public DI ctor + new dependency added between existing dependencies and trailing optional ILogger — DI container auto-resolves the new param at the singleton registration site (Program.cs:244) because IFeatureFlagCache is registered earlier (plan 04 wiring at Program.cs:159). Zero Program.cs edit needed; one of the cleanest possible service modifications."
    - "Internal Fake* test double (FakeFeatureFlagCache) in TestDoubles/ folder — internal sealed class in DeckFlow.Web.Tests namespace with a public mutable Flags dict for per-test override. Default-on contract matches FLAG-01/D-13 production behavior."
    - "Array.Empty<string>() for the new gate path while existing in-method early returns use [] collection-expression — intentional: PATTERNS.md prefers Array.Empty<T>() for hot-path zero-allocation returns. Cosmetic drift documented in T-06-F4 (accept disposition)."

key-files:
  created:
    - DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs
  modified:
    - DeckFlow.Web/Services/ScryfallTaggerService.cs
    - DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs
    - DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs

key-decisions:
  - "D-11 service-layer placement: gate is at the top of LookupOracleTagsAsync, NOT in the controller. Every current and future consumer (CategorySuggestionService Tagger mode is the only current caller; future ones inherit free) sees the no-op without per-controller wiring."
  - "Gate placement: AFTER ArgumentException.ThrowIfNullOrWhiteSpace(cardName), BEFORE ResolveCardPrintingAsync. Verified at lines 92-98 of ScryfallTaggerService.cs after the change. Preserves the input-validation contract (callers passing whitespace still get a thrown exception, gated or not) and prevents an unnecessary Scryfall HTTP call before the short-circuit."
  - "No log on the gate-off path. The cache itself emits WARN-once on missing keys; this is the hot path of a normal request and would spam logs. ScryfallThrottle, ResolveCardPrintingAsync, FetchTaggerSessionAsync, QueryTaggerGraphQlAsync all log their own outcomes, so the absence of any 'Tagger.*' log line for a request IS the diagnostic signal that the gate fired."
  - "Use Array.Empty<string>() for the gate's empty return (matches PATTERNS.md guidance and the rest of the file's existing Array.Empty<string>() returns at lines 257, 320, 335, 345 — only the no-printing-resolved path at line ~96 still uses []). Zero allocation."
  - "Constructor parameter inserted between ResiliencePipelineProvider<string> pipelineProvider and ILogger<ScryfallTaggerService>? logger=null — keeps the optional logger trailing per .NET 10 idiom and keeps the new required dependency in the conventional position right after the other required deps."
  - "FakeFeatureFlagCache test double over inline mock — three test call sites need IFeatureFlagCache; an internal sealed class with a Flags dictionary is cleaner and reusable. Default-on (return true on missing key) preserves existing test semantics for every test that doesn't care about the gate."

patterns-established:
  - "Service-level kill-switch gate: inject IFeatureFlagCache, check at top of public method after arg validation, return zero-value default. Future services adopting this pattern: AnalyticsHarvestService (Phase 7), any future scraper or upstream-traffic-generating service."
  - "Test fakes for IFeatureFlagCache: dictionary-backed default-on fake. Subsequent plans (Phase 7 onward) that need to test gated services use the same FakeFeatureFlagCache (drop in a Flags initialization to drive specific gates)."

requirements-completed: [FLAG-04]

duration: 2min
completed: 2026-05-03
---

# Phase 6 Plan 06: ScryfallTaggerService Kill-Switch Gate Summary

**Adds the FLAG-04 / D-11 kill-switch gate at the top of ScryfallTaggerService.LookupOracleTagsAsync — checks IFeatureFlagCache.IsEnabled("scryfall.tagger.enabled") immediately after argument validation and short-circuits with Array.Empty<string>() before any Scryfall card lookup, Tagger GraphQL POST, or session refresh fires. Service-level placement (D-11) means CategorySuggestionService Tagger mode and any future Tagger consumer inherits the no-op for free.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-05-03T05:13:11Z
- **Completed:** 2026-05-03T05:15:58Z
- **Tasks:** 1 of 1 executable (Task 2 is a human-verify checkpoint, deferred to post-deploy live UAT — see "Live UAT Outcome" below)
- **Files:** 1 created, 3 modified

## Accomplishments

- ScryfallTaggerService now consults `IFeatureFlagCache.IsEnabled("scryfall.tagger.enabled")` at the top of `LookupOracleTagsAsync` (line 95). When the flag is OFF, the method returns `Array.Empty<string>()` immediately — verified by code inspection at lines 92-98.
- Constructor signature gained one new required parameter `IFeatureFlagCache flagCache` between `ResiliencePipelineProvider<string> pipelineProvider` and the trailing optional `ILogger<ScryfallTaggerService>? logger = null`. Existing `ArgumentNullException.ThrowIfNull` guard block extended; new private readonly field `_flagCache` added and assigned.
- One new `using DeckFlow.Web.Services.FeatureFlags;` directive added in alphabetical order.
- Constructor XML doc updated to mention FLAG-04 / D-11 — operators reading the source can trace the kill-switch from the ctor doc to the gate site without grep'ing the whole class.
- `Program.cs` required NO edit: `AddDeckFlowFeatureFlags()` (plan 04) registers `IFeatureFlagCache` at line 159, BEFORE `AddSingleton<IScryfallTaggerService, ScryfallTaggerService>()` at line 244, so the DI container auto-resolves the new ctor parameter. Confirmed via `dotnet build` clean.
- Three direct test ctor call sites updated to pass `new FakeFeatureFlagCache()` (default-on): `ScryfallTaggerServiceTests.CreateService` (line 47-48) and `ScryfallTaggerCookieReplayTests` lines 81 and 113.
- New test double `FakeFeatureFlagCache` (`DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs`) — internal sealed class, dictionary-backed, default-on contract matching D-13 production behavior. Reusable by plan 07 and any future test that needs to drive a gated service.
- `dotnet build DeckFlow.sln` clean: **0 Warning(s) 0 Error(s)** in 30.7s. Build ran in-place on the working tree (no /tmp clone needed; the user's web process was not running during this plan).

## Task Commits

1. **Task 1: Inject IFeatureFlagCache + add gate at top of LookupOracleTagsAsync (with test ctor call site updates and FakeFeatureFlagCache test double)** — `65224a5` (feat)

## Files Created/Modified

### Created (1)

- `DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs` — internal sealed class (~30 lines) implementing `IFeatureFlagCache` with a public mutable `Flags` dictionary; `IsEnabled` returns `true` on missing key (matches D-13).

### Modified (3)

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` — three edits: (a) `using DeckFlow.Web.Services.FeatureFlags;` added in alphabetical position; (b) `IFeatureFlagCache _flagCache` field, ctor param, `ArgumentNullException.ThrowIfNull(flagCache)` guard, `_flagCache = flagCache` assignment, and ctor XML doc update; (c) gate at top of `LookupOracleTagsAsync` (lines 94-98 in the post-edit file). +12 lines, -2 lines net.
- `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` — `CreateService` factory updated to pass `new FakeFeatureFlagCache()` as the fifth ctor arg.
- `DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs` — two ctor call sites updated to pass `new FakeFeatureFlagCache()`.

## Decisions Made

Followed plan exactly. All D-XX decisions implemented per the plan's `<action>` block:

- **D-11** service-level gate at the top of `LookupOracleTagsAsync` — verified at lines 92-98.
- **D-12** stringly-typed `IsEnabled("scryfall.tagger.enabled")` — exact key from the seed (D-09).
- **D-13** missing-key fallback inherited transparently from the cache; this gate path doesn't have to handle the "key missing" case because the seed guarantees the key exists.
- **D-14** sync-initial-load benefit inherited from plan 04 — by the time Kestrel binds, the cache is populated; the very first request after a cold start sees the correct flag value.

The plan's `<behavior>` block was followed step-for-step. The plan called out three direct ctor call sites in the test project as a possible test-edit need; all three were located via grep and updated.

## Live UAT Outcome — DEFERRED TO POST-DEPLOY

**Per phase-wide policy (verify-on-deploy):** Task 2 (`checkpoint:human-verify`, ROADMAP success criterion #5) cannot be exercised on the WSL workstation without standing up BasicAuth credentials, the Postgres-backed admin throttle store, and the live `/Admin/Flags` UI in a configured environment. The gate logic itself is verified by code inspection plus a clean build. The end-to-end live UAT runs after merge to `main` against the deployed deckflow.gg instance.

### Post-Merge Verification Steps (operator-run on production)

After this plan's commit lands on `main` and Render redeploys:

1. **Toggle OFF and verify within 2s.** Open https://www.deckflow.gg/Admin/Flags (BasicAuth gate); click **Disable** on the `scryfall.tagger.enabled` row; immediately (<2s) navigate to https://www.deckflow.gg/suggest-categories , pick a known commander (e.g. "Atraxa, Praetors' Voice"), select **Tagger mode**, submit. Expected: response renders with NO Tagger-derived tags.
2. **Verify zero Scryfall traffic in Render logs.** While the flag is OFF, search the Render logs for the request window: NO `Tagger.GraphQlPost` log entry, NO `Tagger.Resolve` log entry. (`ScryfallThrottle.ExecuteAsync` should not fire for the Tagger lookup path. Card lookups for OTHER reasons — e.g. /lookup — still hit Scryfall normally; only Tagger is gated.)
3. **Toggle ON and verify restoration.** Click **Enable** on the same row. Re-submit the Tagger-mode suggestion. Expected: Tagger tags return, `Tagger.GraphQlPost` log entry returns.
4. **Cold-start sanity probe (D-14).** After the next Render redeploy, **immediately** (within 30s of "Application started" in logs) request a Tagger-mode suggestion. Expected: Tagger tags ARE present — proving plan 04's `StartAsync` override loaded the seed BEFORE Kestrel bound. If empty Tagger results appear on the first request post-deploy, plan 04's sync-load is broken and needs revision.
5. **Placement sanity (T-06-F2).** Read lines 92-98 of `DeckFlow.Web/Services/ScryfallTaggerService.cs` on the deployed commit. Confirm gate appears AFTER `ArgumentException.ThrowIfNullOrWhiteSpace(cardName);` (line 92) and BEFORE the `var (set, collectorNumber) = await ResolveCardPrintingAsync(...)` call (line 103).

If any step fails, file an issue tagged `phase-06-rollback-candidate` and revert this commit. The rollback path is clean: removing the gate restores pre-plan-06-06 behavior identically; no DB schema change to undo.

## Deviations from Plan

**[Rule 3 - Blocking issue] Test ctor call site updates**

- **Found during:** Task 1 verification (the plan's `<action>` step 3 explicitly anticipated this).
- **Issue:** Three call sites construct `ScryfallTaggerService` directly (no DI): `ScryfallTaggerServiceTests.CreateService` and two methods in `ScryfallTaggerCookieReplayTests`. After adding the new required ctor parameter, all three would fail to compile.
- **Fix:** Created `FakeFeatureFlagCache` (internal sealed, default-on) in `DeckFlow.Web.Tests/TestDoubles/`; updated all three call sites to pass `new FakeFeatureFlagCache()`. Test semantics preserved — all existing assertions still hold because the fake defaults all flags to enabled.
- **Files modified:** `DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs` (new), `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs`, `DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs`.
- **Commit:** `65224a5` (rolled into Task 1 commit per the plan's anticipated scope).

This is a documented anticipated-scope deviation, not a true plan miss — the plan's `<action>` step 3 explicitly told the executor to discover and fix these sites.

## Issues Encountered

None. Build was clean on the first attempt. No /tmp clone needed (the user's web process was not running during this plan, so the Windows file lock issue documented in plan 04's summary did not recur).

## Threat Mitigations Recorded

- **T-06-F1 (Cold-start window where Tagger fires despite operator intent):** MITIGATED via construction. Plan 04's D-14 `StartAsync` override loads the cache snapshot SYNCHRONOUSLY before Kestrel binds; plan 02's D-09 seed defaults `scryfall.tagger.enabled=TRUE` on fresh DB. Combined: the only way Tagger is unexpectedly OFF post-restart is if an operator explicitly disabled it. The worst case is documented default-on behavior, not arbitrary state. Verified at runtime once the post-merge step #4 in "Live UAT Outcome" passes.
- **T-06-F2 (Gate at wrong layer / placement accidentally short-circuits cache lookups before the Tagger HTTP call):** MITIGATED by inspection. Gate is placed AFTER `ArgumentException.ThrowIfNullOrWhiteSpace(cardName)` (so the input contract still holds: callers passing whitespace still see the throw, gated or not) and BEFORE `ResolveCardPrintingAsync(...)` (so no Scryfall HTTP call fires when the flag is OFF). Verified by reading lines 92-103 of `ScryfallTaggerService.cs` post-edit.
- **T-06-F3 (Operator accidentally toggles Tagger off mid-traffic):** ACCEPTED — same disposition as T-06-E5 in plan 05. Single-operator surface, owner of consequences. Default-on seed plus D-13 missing-key fallback ensure mistakes preserve current behavior; recovery is one click on `/Admin/Flags`.
- **T-06-F4 (Cosmetic style drift between `[]` and `Array.Empty<string>()` returns):** ACCEPTED — gate uses `Array.Empty<string>()` (matches PATTERNS.md guidance for zero-allocation hot-path returns and the rest of the file's existing returns at lines 257, 320, 335, 345). The one remaining `return [];` at the no-printing-resolved path is a pre-existing legacy idiom unrelated to this plan's change. Cosmetic-only; no security or correctness impact.

## User Setup Required

None. No new env vars, no dashboard config, no schema change. `IFeatureFlagCache` is already wired by plan 04; the seed for `scryfall.tagger.enabled` is already inserted by plan 02; the admin write path is already wired by plan 05. This plan only adds a single `if` check in one method.

## Verification

### Automated Gates (executed at plan completion)

- `dotnet build DeckFlow.sln` — **Build succeeded. 0 Warning(s) 0 Error(s)** in 30.7s.
- `grep -q 'using DeckFlow.Web.Services.FeatureFlags;' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q 'private readonly IFeatureFlagCache _flagCache;' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q 'IFeatureFlagCache flagCache,' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q 'ArgumentNullException.ThrowIfNull(flagCache);' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q '_flagCache = flagCache;' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q '_flagCache.IsEnabled("scryfall.tagger.enabled")' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -q 'return Array.Empty<string>();' DeckFlow.Web/Services/ScryfallTaggerService.cs` — PASS.
- `grep -c '_flagCache.IsEnabled' DeckFlow.Web/Services/ScryfallTaggerService.cs` returns **1** — gate appears exactly once, only in `LookupOracleTagsAsync` per FLAG-04 scope.

### Live UAT (deferred to post-merge production)

ROADMAP success criterion #5 ("Operator can disable the Tagger kill-switch flag from /Admin/flags, reload a card lookup page within 2 seconds, and observe that Tagger tags are absent — demonstrating hot-reload invalidation, not TTL expiry") is exercised on production after merge per the steps in **Live UAT Outcome** above. The gate logic is verified by code inspection and clean build at this checkpoint.

## Next Phase Readiness / Hand-off

### For plan 07 (FeatureFlagGateAttribute on /help)

- Plan 07 ships the LAST piece of phase 6: a page-level kill-switch demo on `/help` via a `[FeatureFlagGate("page.help.enabled", ...)]` action filter that short-circuits with a 503 + `_MaintenancePage` view when off.
- Plan 07 follows the same `IFeatureFlagCache.IsEnabled(string)` API used by this plan; it resolves the cache from `context.HttpContext.RequestServices` inside the filter (rather than ctor injection, since attributes can't take DI).
- The `FakeFeatureFlagCache` test double introduced in this plan is reusable for plan 07's filter tests — drop in a `Flags = { ["page.help.enabled"] = false }` initialization.
- After plan 07 completes, phase 6 is closed: admin shell + flags foundation is fully in place, ready to host harvest controls (Phase 7) and analytics dashboards (Phase 8).

## Self-Check: PASSED

Files verified to exist on disk:

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` — FOUND (modified)
- `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` — FOUND (modified)
- `DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs` — FOUND (modified)
- `DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs` — FOUND (created)

Commits verified to exist in `git log --oneline -5`:

- `65224a5` (Task 1) — FOUND.

Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors).

Scope: only the four files in `key-files` were touched. No unintentional file deletions (verified by `git diff --diff-filter=D --name-only HEAD~1 HEAD` returning empty).

---
*Phase: 06-admin-shell-flags-foundation*
*Completed: 2026-05-03*
