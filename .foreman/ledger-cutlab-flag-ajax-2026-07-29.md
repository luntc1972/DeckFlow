# Foreman ledger — Cut Lab commander-floors flag + AJAX floorByRole fix

**Run date:** 2026-07-29
**Branch:** `gsd/cycle21-cut-lab` (worktree `deckflow-role-floors`)
**Baseline commit:** `469dc9cf` (tracked tree clean at start)
**Explicit constraint from user:** do NOT merge to main.

## Mode

Codex-boosted, Agent-free. Codex `gpt-5.4` medium writes code; Codex `gpt-5.5` medium
read-only reviews as the fresh second reader. Foreman (Claude) runs the deterministic
gates itself and grades the diff. Claude subagents are not spawned this run, so every
acceptance is labelled with the reader that produced it.

## Why this work exists

Phase 3 shipped commander-aware floors with **no dedicated feature flag**, while
`tool.cut-lab.enabled` is already `true` in prod and `CutLabController` is on
`origin/main`. Merging Phase 3 would therefore go straight to live users with the AJAX
`floorByRole` gap still open. ROADMAP release posture asked for a flag; it was never built.

## Tasks

| # | Task | Seat | Status |
|---|------|------|--------|
| 1 | Feature flag `analysis.cut-lab.commander-floors`, seeded FALSE, gating the commander layer | Codex gpt-5.4 | **ACCEPTED** — reviewed by gpt-5.5, 2 findings fixed, rendered and visually verified |
| 2 | Fix AJAX `floorByRole` starvation via a shared floor-resolution seam | Codex gpt-5.4, planned first | **ACCEPTED** — 2 review rounds, 4 findings fixed; round-2 FAIL verified pre-existing and out of scope |

## Load-bearing facts established before dispatch (foreman, read-only)

1. **No signature change is needed for the flag.** `CutLabFloorDefaults.ResolveDefaults`
   has exactly ONE production call site (`CutLabPageService.cs:298`); the other 16 are in
   `CutLabFloorDefaultsTests.cs`. Its `roleFloorBaseline` parameter is already
   `IRoleFloorBaselineProvider?` and bracket-only behaviour on null is already covered by
   `CutLabFloorDefaultsTests.cs:279-288`. Gating = pass the provider or pass `null`.
   This deliberately avoids the 8-call-site trap that made plan 03-04 internally impossible.
2. **A new flag lands OFF in prod without a migration.** `IFeatureFlagCache` returns
   **true** for a missing key (default-on), so an unseeded key would default ON — the
   opposite of deploy-dark. It does get seeded: the seed runs lazily on every process
   start and uses `ON CONFLICT (key) DO NOTHING` (`FeatureFlagStore.cs:246,297`), which
   backfills new keys into the existing prod DB while preserving operator-set values.
   Both dialect blocks must be edited (PG `FALSE` ~:231, SQLite `0` ~:282).
3. **Flag-off target markup is known exactly.** Pre-Phase-3 header at `84a4d5f4` was
   `Role | In pool | Floor | Source` (4 columns). Phase 3 added **Bracket** and
   **Commander**. So off ⇒ drop those two only; Source predates Phase 3 and stays.
4. **AJAX gap root cause.** `CutLabPageService.cs:298` resolves a COMPLETE floor map, then
   persists only `IsUserSet` floors into `state.RoleFloors` (`:311-320`).
   `CutLabApiController.BuildFloorMap` (`:414-426`) rebuilds the map from that user-set-only
   list, so after the first accept/reject the map is near-empty. `HeadroomFor` defaults a
   missing floor to 0, so the locked-overshoot advisory ranks by raw in-pool count and can
   contradict the table on the same page. Pre-existing at `84a4d5f4`; also starves
   `WeakFloorCase` findings.
5. **Everything the fix needs is reachable** at the three AJAX call sites
   (`CutLabApiController.cs:81,224,355`): `state.Intent.Bracket`, `state.Intent.PlayExperience`,
   `commanderNames`, and `CommanderManaValue` from the analysis context that
   `CutLabUiPatchBuilder` already builds (`:68`). Precedent for the shared-seam shape is
   Phase 104's `CutLabWhatifPreviewService`.

## Attempts

(append-only)

### Task 1, attempt 1 — Codex gpt-5.4 medium — reported DONE

Commits `983ea700` (feat) and `f17a612f` (test). Flag key `analysis.cut-lab.commander-floors`.

**Foreman grading of the diff (not of the narrative):**

| Check | Result |
|---|---|
| Scope vs declared write set | PASS — 10 files, all inside the fence |
| `ResolveDefaults` signature unchanged | PASS — `CutLabFloorDefaults.cs` absent from the diff entirely |
| Seed rows in BOTH dialect blocks | PASS — PG `FALSE` :232, SQLite `0` :284 |
| Line endings | PASS — all touched files 0 CR before and after; 0 CR bytes in the whole diff |
| Tracked tree clean at HEAD | PASS |
| Build (foreman re-ran, `--no-incremental`) | PASS — 0 errors, 9 pre-existing CS8629 |
| `DeckFlow.Web.Tests` (foreman re-ran) | PASS — 2159 passed / 0 failed / 16 skipped (baseline 2155, +4 new) |

**Two design calls made by the worker, both accepted with reasons:**

1. `CutLabPageService.IsFlagOn` deliberately fails **safe-OFF** on a missing key, inverting
   `IFeatureFlagCache`'s documented default-ON (D-13). Correct for a dark-launch flag — a seeding
   failure must mean "stays dark", never "ships to everyone" — and it is commented at the call site.
2. Gating implemented as `commanderFloorsEnabled ? _roleFloorBaseline : null` at the single call
   site, exactly as the ticket required. No signature change, no call-site fan-out.

**The check that actually mattered — DI resolvability.** `IFeatureFlagCache` is an *optional*
constructor parameter. Had it not been registered, .NET would have silently selected a narrower
constructor, leaving `_featureFlags` null, `IsFlagOn` permanently false, and the flag **stuck OFF in
production even after an operator flips it** — invisible to every unit test, since they all pass
`null` deliberately. Verified registered: `Program.cs:113` `AddDeckFlowFeatureFlags()` (singleton,
`FeatureFlagsServiceCollectionExtensions.cs:24`) resolves before `CutLabPageService` at `:183`.

**NOT verified, and not claimed:** nobody has rendered the flag-off page. Same exposure class that
produced the 03-05 CSS collapse (shipped past 2,138 passing tests) and the never-displayed 03-06
banner. Heightened here because the worker also rescoped a `@media (min-width: 601px)` rule in
`site-common.css` for the now-4-column table. Render check requires a headless server + Playwright,
which needs the user's go-ahead per their standing rule. ASKED, not yet answered.

### Task 1, attempt 2 — Codex gpt-5.4 medium — fix batch for review findings

Commit `93f0e3af` `fix(cut-lab): restore commander floors flag identity`.

Cross-family review (Codex gpt-5.5, read-only) returned **JOB A: FAIL** with 1 HIGH + 1 MEDIUM.
Both were independently confirmed by the foreman against the code before any fix was dispatched.

1. HIGH — `CutLab.cshtml:787` emitted `data-cut-lab-commander-floors="false"` UNCONDITIONALLY,
   breaking the byte-identity acceptance bar (baseline `84a4d5f4` carries only
   `data-prompt-cedh-reference-table` and `data-cut-lab-role-floors-table`).
   Fixed: attribute now rendered only when the flag is ON.
2. MEDIUM — the added tests did not discriminate. `CutLabCommanderFloorsFlagTests` never rendered
   the view; its only real assertion echoed its own input; and it constructed `CommanderValue = 9`
   WITH THE FLAG OFF — a state production cannot reach, because flag-off passes a null baseline so
   `CommanderValue` is always null. `CutLabFloorDefaultsTests.cs:328` built its "flag off" case by
   passing `roleFloorBaseline: null`, identical to the pre-existing `bracketOnly` case, so it never
   exercised the flag at all. **Both would have passed with the entire gate deleted.**
   Fixed by three replacements:
   - `RenderAsync_CommanderFloorsFlagOff_OmitsCommanderColumnsAndMarker`
   - `RenderAsync_CommanderFloorsFlagOn_RendersCommanderColumnsAndMarker`
   - `ProcessAsync_CommanderFloorsFlagOff_IgnoresCommanderRoleFloorBaseline` — supplies a REAL
     `FakeRoleFloorBaselineProvider` that would raise floors, then asserts
     `Assert.Empty(roleFloorBaseline.QueriedRoles)`. A **counting fake**, so it fails the moment the
     gate is removed. This is the test the original lacked.

The review cleared the CSS rescope and the TypeScript (selectors, not cell indexes). The foreman
separately confirmed no commander data leaks by any other path: with a null baseline
`CutLabFloorFeasibility.cs:87-88` computes a zero commander raise, so the 03-06 advisory degrades
correctly.

Gates after the fix: build 0 errors / 9 pre-existing CS8629; Web 2160 passed / 0 failed / 16 skipped.
EOL clean (0 CR bytes in the diff). Tracked tree clean at `93f0e3af`.

### Task 1 — RENDER VERIFICATION (the gap Phase 3 kept leaving open)

Temporary spec `DeckFlow.Web/e2e/zz-flagoff-render.spec.ts`, run headless on the `http-no-browser`
profile. **3/3 passed in 25.1s.** Spec deleted afterwards; tree clean.

Screenshots (untracked): `.foreman/scratch/flagoff-shots/` — 2 themes x 2 viewports x flag on/off.

Visually inspected, not merely asserted:
- **Flag OFF, mobile (classic):** four stacked labels — Role / In pool / Floor / Source. No Bracket,
  no Commander. Inputs intact, no column collapse. This is the exact defect class that shipped in
  03-05 past 2,138 passing tests, so it was checked by eye.
- **Flag ON, mobile (classic):** six stacked labels; all three Commander states render (`2`, `—`,
  `n/a`). Incidentally re-confirms the `max(bracket, commander)` rule visually: Ramp 12/2 -> 12,
  Engines 6/2 -> 6.
- **Flag ON, desktop (nyx dark):** six columns, numerics right-aligned, contrast fine.
- **Flag OFF, desktop (nyx dark):** four columns, correctly sized — the ON-only CSS marker does not
  strand the 4-column layout.

Also proven by test 3: the new key was **backfilled into the pre-existing dev database** by the
startup seed and appears on `/Admin/Flags` seeded OFF. That is the exact mechanism prod relies on,
and it had never been exercised — every other flag was seeded when its DB was first created.

**Pre-existing, NOT a regression:** in both mobile screenshots the sticky "ON THIS PAGE" nav strip
overlaps one role-floor row. Identical in flag-on and flag-off states, so it predates this change.
Logged for a future UI pass; not fixed here (out of scope).

### Task 2 — AJAX floorByRole starvation — ACCEPTED

Commits: `164d9bab` (fix) then `f104af8d` (hardening after review).

**Round 1 (`164d9bab`).** All five regression tests (T1-T5) were confirmed FAILING against the
unfixed tree before the fix landed — the evidence the ticket demanded, and the thing plan 03-07 could
not have produced (its proof test supplied a hand-built complete map, so it passed before AND after).
T4's pre-fix failure is the defect in one line: expected `["Wincon Sorcery", "Counterspell"]`,
got the reverse — advisory ordering by raw pool count instead of headroom.

Cross-family review returned FAIL with one HIGH (partner commanders). **The review also asserted no
second floor-map supply path remained. That assertion was wrong**, and the foreman found the real
problem by grading the diff directly:

- `BuildFloorMap` had been deleted by NAME and reincarnated as a private `StateRoleFloorResolver`
  in TWO places (`CutLabApiController.cs:439`, `CutLabUiPatchBuilder.cs:247`), reading only
  `state.RoleFloors` — i.e. the original defect, preserved as a silent default and installed by
  convenience constructors. Production was correct ONLY because `Program.cs:184` happened to register
  the real resolver; deleting that one line would have silently restored the bug with no compile
  error and no failing test.
- **All 12+ constructions in `CutLabUiPatchBuilderTests` used the 2-arg convenience ctor**, so that
  entire class was validating the fallback rather than the fix.

**Round 2 (`f104af8d`).** Three findings fixed in one batch. First dispatch correctly reported
BLOCKED because the write set omitted `CutLabWhatifTests.cs` — a defect in the TICKET, not the
execution. Foreman then enumerated every construction site solution-wide (4 test files, zero
production sites) and re-dispatched.

Result:
- `StateRoleFloorResolver` deleted in both places; both convenience ctors removed.
  `ICutLabFloorResolver` is now a REQUIRED dependency — deleting the DI registration is now a
  compile error rather than a silent reversion. Verified: 0 matches solution-wide.
- `CutLabCommanderNames.Resolve` extracted as the single shared commander-name derivation, replacing
  three independent copies; used at `CutLabApiController` :84/:172/:230/:359 and
  `CutLabController:280`.
- `CutLabUiPatchBuilderTests` re-pointed at the real resolver: **1 assertion added, 0 removed**
  across 155 lines of rewiring — expectations survived rather than being fitted to the change.
- New T6 (two-commander AJAX vs full-page parity) confirmed failing pre-fix.

Gates re-run by foreman (not taken on report): build 0 errors / 9 pre-existing CS8629;
Web **2168 passed / 0 failed / 16 skipped**; Core **2011 / 0**. EOL clean. Tree clean at `f104af8d`.

### Round-2 review verdict: FAIL — but NOT attributable to this work

The second cross-family read returned one HIGH against the **no-JS MVC transport**:
`CutLabController.cs:425` sets `request.SelectedCommander = state.Commander` during state
rehydration, and `CutLabPageService.ResolveCommanderSelection` (:599-618) returns `[selected.Name]`
as soon as `SelectedCommander` is supplied — never reaching the `validatedFlaggedCommanders` branch.
So no-JS decide/adjust/restart/goals/what-if/export collapse partner/background decks to commander 0.

**Verified PRE-EXISTING and out of scope.** That line is byte-identical at `84a4d5f4` (before Phase 3)
and at `469dc9cf` (before today), and the five commits of this run touched neither
`ResolveCommanderSelection` nor `RehydrateIntakeRequestFromState`. It is a third-transport instance
of the same divergence class, discovered by this review — not a regression introduced here.

NOT fixed. The fix would touch `ResolveCommanderSelection`, which governs the main page path for
every deck — materially higher blast radius than the authorised scope, and the user's instruction
was explicitly "fix the ajax gap". Logged for a decision.

## Push state (user pushed mid-run, 2026-07-29 ~13:05)

`origin/gsd/cycle21-cut-lab` advanced to `469dc9cf`, resolving the 189-ahead / 94-behind divergence
created by the 2026-07-28 rebase. The pre-rebase safety net `backup/cycle21-cut-lab-pre-rebase-2026-07-28`
(`37275a87`) is also on origin, so that history survives off-machine.
**The five commits of this run were NOT included** — branch is ahead 5. `origin/main` untouched at
`1511dd95`; nothing from this run is live.
