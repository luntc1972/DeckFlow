---
phase: 03-commander-aware-floor-defaults
verified: 2026-07-29T08:06:37Z
status: gaps_found
score: 8/9 must-haves verified
verdict: GOAL ACHIEVED — one WARNING-severity integration gap, no BLOCKER
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
gaps:
  - truth: "Roadmap SC5 — LockedOvershootRoleOrder is reconciled with the commander data (D-13 headroom ranking)"
    status: partial
    severity: warning
    reason: >-
      The headroom reconciliation is real, tested, and correct on the full-page render path, but the
      commander-aware floors never reach it on the AJAX decide/restart path. CutLabApiController builds
      floorByRole from state.RoleFloors, and state.RoleFloors only ever carries USER-SET floors
      (CutLabPageService.cs:313 / :732 server side, cut-lab.ts:1233-1239 client side). Every
      non-overridden role therefore resolves floor = 0 inside HeadroomFor, so after the first
      accept/reject the live-patched locked-overshoot advisory ranks by raw in-pool count and can
      contradict the commander-aware floors displayed in the table on the same page — the exact defect
      shape SC5 names.
    artifacts:
      - path: "DeckFlow.Web/Controllers/Api/CutLabApiController.cs:81,224,355"
        issue: "floorByRole = BuildFloorMap(state.RoleFloors) — user-set floors only"
      - path: "DeckFlow.Web/Services/CutLab/CutLabPageService.cs:313,732"
        issue: "state.RoleFloors persists only floors where IsUserSet == true"
      - path: "DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs:460-476"
        issue: "HeadroomFor defaults a missing floor to 0, silently degrading headroom to raw count"
    missing:
      - "Carry the resolved effective floors (or re-resolve them) into the AJAX decide/restart path so headroom is computed against the same numbers the table displays"
      - "A test that drives CutLabApiController.PostDecideAsync end to end and asserts the advisory order matches the rendered floors (the current CutLabUiPatchBuilderTests proof hands BuildAsync a synthetic complete map, so it cannot catch this)"
human_verification:
  - test: "Load Cut Lab with a commander whose resolved floors are infeasible (e.g. a high engines/payoffs commander at bracket 5) and inspect the new floor-feasibility warning banner"
    expected: "Banner renders above the role-floors table, copy is legible, contrast holds on at least one light and one dark guild theme, and it stacks acceptably at <= 600px"
    why_human: "The 03-06 advisory banner shipped AFTER 03-05's human-verify checkpoint, so no human has ever seen it rendered. xUnit pins the copy string, not the rendering."
---

# Phase 3: Commander-Aware Floor Defaults — Verification Report

**Phase Goal (ROADMAP):** For any role Phase 2 found real signal for, Cut Lab's floor default reflects
that commander's own corpus data via a priority chain, while every commander and role without
qualifying signal keeps today's bracket+plan floor unchanged.
**Verified:** 2026-07-29T08:06:37Z
**Status:** gaps_found (1 WARNING, 0 BLOCKER)
**Re-verification:** No — initial verification.

## Verdict

**The phase goal is achieved.** Cut Lab genuinely resolves commander-aware role floors from a shipped,
independently-reproducible corpus snapshot, and genuinely renders the bracket floor and the commander
floor side by side with two distinct empty states. This is not seven plans that executed — the data
flows end to end and I traced every hop.

One WARNING-severity integration gap sits between plan 03-04 and plan 03-07: the effective floors
03-04 computes never reach 03-07's headroom ranking on the AJAX decide path. It does not block the
headline goal or any of RFLR-05..08, but it partially defeats roadmap success criterion 5.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | **SC1 / RFLR-05** — floor resolution combines commander data with the bracket+plan value (amended by D-04 from a priority chain to `max()`) | ✓ VERIFIED | `CutLabFloorDefaults.cs:106` `int effectiveDefault = Math.Max(bracketValue, commanderValue ?? 0);`. Commander lookup gated at `:94-99` by `RoleFloorBaseline.AdoptedRoleKeys`. Amendment is ratified in `REQUIREMENTS.md:31`, `ROADMAP.md:315-317`, `03-CONTEXT.md` D-04 — a recorded deviation, not an unauthorized one. |
| 2 | **SC2 / RFLR-06** — insufficient-data commanders and out-of-scope roles produce byte-identical output to today | ✓ VERIFIED | `max(bracket, 0) == bracket` when no commander value exists; `ResolveDefaults_NoCommanderMatch_IsIdenticalToBracketOnly` (`CutLabFloorDefaultsTests.cs:276-306`) compares a null provider against an empty provider field-by-field. The provider is genuinely fail-open (`RoleFloorBaselineProvider.cs:114-136` catches `IOException`/`UnauthorizedAccessException`/`JsonException`, plus an explicit-JSON-null guard at `:118-124`), proven by 6 tests including three explicit-null shapes. |
| 3 | **SC3 / RFLR-07** — unit coverage for commander-hit, fallback, and role-not-in-scope | ✓ VERIFIED | Hit: `ResolveDefaults_CommanderFloorAboveBracket_RaisesTheDefault`. Fallback: `..._CommanderFloorBelowBracket_KeepsBracketButStillReportsCommander` and `..._NoCommanderMatch_IsIdenticalToBracketOnly`. Not-in-scope: `..._OutOfScopeRoles_AreNeverQueried` (asserts on the fake's `QueriedRoles` — proves the provider is never consulted, not merely that the value is ignored). Plus equality-tie, user-override, independent ramp/draw, and six-role-query cases. |
| 4 | **SC4 / RFLR-08** — bracket and commander floors side by side at every bracket, with an explicit empty marker | ✓ VERIFIED | `CutLab.cshtml:786-793` headers `Role \| In pool \| Bracket \| Commander \| Floor \| Source`; `:811-812` render `@row.BracketValue` and `@row.CommanderDisplay`, both carrying `data-label`. Two distinct markers built at `CutLabViewModel.cs:521-530`: `n/a` for structurally out-of-scope roles, `—` for a GO role with no match. Neither is a silent substitution — the Floor column is a separate cell. |
| 5 | **SC5 / D-13** — `LockedOvershootRoleOrder` reconciled with commander data | ⚠ PARTIAL | Engine side is genuinely reconciled: `CutLabCutRoundEngine.cs:432` sorts by `HeadroomFor(...)` descending with the fixed array demoted to tiebreak (`:433`), and the discriminating test `BuildQueue_LockedOvershootRanksByHeadroomDescending` asserts an order (`draw, payoffs, interaction-mass, interaction-targeted, wincons`) that the old fixed array cannot produce. **But** the commander-aware floors only reach it on the full-page render. See "Integration Gap" below. |
| 6 | Shipped snapshot is really derived from the Phase 2 findings under the documented filter | ✓ VERIFIED | I independently recomputed the filter (six roles ∩ `source == "postgres"` ∩ `clearsBar` ∩ `floor(p25) > 0`) over `RESEARCH-FINDINGS.json` in Python: 678 commanders, 1463 pairs, **0 mismatched commanders**, key sets identical, `sampleSize` 841 = findings commander count. Snapshot contains only the six GO roles; no `lands` / `interaction-mass` / `protection` key anywhere; min floor 1, max 23, all integers; 0 partner-pair keys, 45 DFC keys. |
| 7 | The provider is DI-registered and warmed, and the snapshot ships to the app | ✓ VERIFIED | `Program.cs:96` `AddSingleton<IRoleFloorBaselineProvider, RoleFloorBaselineProvider>()`; `Program.cs:314` `EnsureLoaded()` at startup beside the other baselines. `DeckFlow.Web.csproj:43-45` copies `Data\role-floor-baseline\*.json`; confirmed present in `bin/Release/net10.0/Data/role-floor-baseline/latest.json`. `CutLabPageServiceTests.cs:2539` asserts the production container shape includes the registration. |
| 8 | The generator is fail-closed | ✓ VERIFIED | `RoleFloorBaselineCommandRunner.cs` reaches a verdict **before** `Directory.CreateDirectory` (`:124-169`): non-postgres source guard at `:83-112`, zero-commander guard at `:115-119`, drift check at `:146-160`. Six drift rules in `RoleFloorBaselineDriftCheck.cs` (`EmptyPreviousSnapshot`, `DroppedEstablishedCommander`, `DroppedEstablishedRole`, `SampleCollapse`, `AdoptedPairCollapse`, `OneSidedDrift`), all seven thresholds present in `scripts/role-floor-baseline/drift-thresholds.json`, `required` on the thresholds record so a missing key throws. 19 Core tests. |
| 9 | **D-06a** — the infeasibility advisory applies the overlap correction and states its own conservatism | ✓ VERIFIED | `CutLabFloorFeasibility.cs:46-61` computes `ramp + max(draw, engines) + interaction-targeted + interaction-mass + protection + payoffs`, omitting `wincons`. Exactly two corrections, no third; `payoffs` deliberately still additive with the reason inline. The honesty sentence is emitted verbatim in both branches of `CutLabViewModel.BuildFloorFeasibilityMessage` (`:406-434`). Banner rendered at `CutLab.cshtml:761-766` using the existing `warning-banner` class. |

**Score: 8/9 truths verified, 1 partial.**

---

### End-to-End Reachability Trace (requested check #1)

Every hop is wired and invoked on the real request path:

| # | Hop | File:line | Status |
|---|-----|-----------|--------|
| 1 | Shipped snapshot | `DeckFlow.Web/Data/role-floor-baseline/latest.json` (51,694 bytes, 678 commanders) | ✓ |
| 2 | Copied to output | `DeckFlow.Web/DeckFlow.Web.csproj:43-45` → `bin/Release/net10.0/Data/role-floor-baseline/latest.json` | ✓ |
| 3 | DI registration | `DeckFlow.Web/Program.cs:96` | ✓ |
| 4 | Startup warm-up | `DeckFlow.Web/Program.cs:314` `EnsureLoaded()` | ✓ |
| 5 | Provider load + lookup | `RoleFloorBaselineProvider.cs:106-137` (24h cache), `:72-104` (`CommanderBaselineKeys.Candidates`) | ✓ |
| 6 | Injected into the page service | `CutLabPageService.cs:142` ctor param, `:121` field, `:167-173` DI guard | ✓ |
| 7 | Passed to resolution | `CutLabPageService.cs:298-306` — the **only** production call site of `ResolveDefaults` | ✓ |
| 8 | Resolution | `CutLabFloorDefaults.cs:94-119` — gate, `max()`, and both components stored | ✓ |
| 9 | Result carrier | `CutLabResolvedFloor.BracketValue` / `.CommanderValue` (`CutLabFloorDefaults.cs:245-249`), populated at `:115-116` | ✓ |
| 10 | Surfaced on the result | `CutLabPageService.cs:475` `ResolvedFloors = resolvedFloors` | ✓ |
| 11 | View-model projection | `CutLabViewModel.cs:310` `BuildFloorRows(result.ResolvedFloors, ...)` → `:512-555` | ✓ |
| 12 | Rendered cell | `CutLab.cshtml:811-812` (`Bracket`, `Commander`), `:823-828` reset carries `DefaultValue` = the max | ✓ |

No hop compiles-but-is-never-invoked. The `data-cut-lab-floor-default` DOM contract (`CutLab.cshtml:800`, `:828`) carries `DefaultValue`, which `CutLabFloorDefaults.cs:114` sets to the `max()` — so Reset restores the effective floor, satisfying the D-04 discretionary item without a TypeScript change.

---

### Requirements Coverage

| Requirement | Description (as amended) | Status | Evidence |
|---|---|---|---|
| **RFLR-05** | `max(bracket-and-plan derived, commander-derived)`; both numbers retained | ✓ SATISFIED | `CutLabFloorDefaults.cs:106`, `:115-116`. Amendment recorded in `REQUIREMENTS.md:31`. Commander data can only raise. |
| **RFLR-06** | Below-bar commander / non-clearing role ⇒ byte-identical to shipped behavior | ✓ SATISFIED | Structural (`max(b,0) == b`) + `ResolveDefaults_NoCommanderMatch_IsIdenticalToBracketOnly` + 6 fail-open provider tests. The provider degrades, it does not throw — verified including three explicit-JSON-null shapes that would otherwise NRE past `required`. |
| **RFLR-07** | Unit coverage: commander-hit / fallback / role-not-in-scope | ✓ SATISFIED | All three paths have real, discriminating tests (see truth 3). The not-in-scope test asserts the provider was never *queried*, which is stronger than asserting the value was ignored. |
| **RFLR-08** | Both numbers on screen at every bracket, explicit empty marker, never a silently substituted number | ✓ SATISFIED | Markers are **distinct and proven distinct**: `BuildFloorRows_OutOfScopeRole_ShowsNotApplicable` supplies **non-null** commander values (40/9/8) for lands/interaction-mass/protection and still demands `n/a` — it proves suppression of data that exists, not merely absence. `BuildFloorRows_GoRoleWithNoCommanderMatch_ShowsEmptyMarker` asserts both `== "—"` and `!= "n/a"`. No silent substitution: Bracket, Commander, and Floor are three separate cells. |

No orphaned requirements: `REQUIREMENTS.md` maps exactly RFLR-05..08 to Phase 3 and all four are claimed by the plans.

---

## Integration Gap Between Plans (requested check #4)

Per-plan verification could not see this, and it is the one finding of substance.

**03-04 produces a value that 03-07 cannot consume on the live path.**

- `03-04` computes commander-aware effective floors into `CutLabResolvedFloor.Floor`.
- `03-07`'s `HeadroomFor` needs `floorByRole` + `roleCounts` (`CutLabCutRoundEngine.cs:460-476`).
- On the **full-page render** the map is complete: `CutLabPageService.cs:305-310` builds `floorByRole` from *all* `resolvedFloors`, and `:372-376` passes it into `BuildFindingsAndRoundPlan`. Criterion 5 holds here.
- On the **AJAX decide / restart-rounds paths** the map is not: `CutLabApiController.cs:81`, `:224`, `:355` build it via `BuildFloorMap(state.RoleFloors)`, and `state.RoleFloors` is persisted as **user-set floors only** — server side at `CutLabPageService.cs:313-320` and `:732-739`, and client side at `cut-lab.ts:1233-1239` (`.filter(({ row }) => row.dataset.cutLabFloorUserSet === 'true')`).
- Consequence: `HeadroomFor` falls back to floor `0` for every non-overridden role, so headroom degenerates to the raw in-pool count. Worked example: the table shows `engines` at a commander-raised floor of 9 with 10 in pool (headroom 1, should rank near-last); after the first accept/reject the patched advisory computes headroom 10 and ranks it near-**first**. That is the "hardcoded order that contradicts commander-aware floors on the same page" that criterion 5 calls a defect, arrived at by a different route.

**Why this was invisible per-plan:** `03-07`'s own proof test (`CutLabUiPatchBuilderTests.BuildAsync_LockedOvershootAdvisory_GroupsFollowHeadroomOrder`, `:284-315`) hands `BuildAsync` a hand-built complete floor map. It proves the plumbing accepts a map; it cannot prove the controller supplies one.

**Severity: WARNING, not BLOCKER.**
- The underlying `state.RoleFloors == user-set only` design is **pre-existing** — `git show 84a4d5f4:.../CutLabApiController.cs` already had `BuildFloorMap(state.RoleFloors)` at the same two lines. Phase 3 did not create it.
- No Phase 3 requirement (RFLR-05..08) binds the patch path.
- The engine reconciliation itself is real, tested, and correct.

**Pre-existing collateral worth recording (NOT a Phase 3 defect):** the same empty map also starves
`CutLabStructuralFindings.Compute` (`FloorFor` returns `0` on a miss, `CutLabStructuralFindings.cs:390-391`)
and `BuildFloorWarnings` on the decide path — so `WeakFloorCase` findings and decide-time floor-break
warnings likewise only see user-set floors. Phase 3 raises the stakes on this (commander floors are
strictly higher), which is why it is worth a follow-up ticket even though it predates the phase.

---

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Produces real data | Status |
|---|---|---|---|---|
| `CutLab.cshtml` role-floors table | `Model.FloorRows` | `CutLabViewModel.BuildFloorRows(result.ResolvedFloors, …)` | Yes — snapshot → provider → `ResolveDefaults` → `ResolvedFloors` | ✓ FLOWING |
| `Commander` cell | `row.CommanderDisplay` | `floor.CommanderValue` from `IRoleFloorBaselineProvider.TryGetRoleFloor` | Yes — 678-commander snapshot verified reproducible | ✓ FLOWING |
| Feasibility banner | `Model.FloorFeasibility` | `CutLabFloorFeasibility.Evaluate(result.ResolvedFloors)` | Yes — same resolved floors | ✓ FLOWING |
| Locked-overshoot advisory (page render) | `floorByRole`, `context.RoleCounts` | `CutLabPageService.cs:305`, `:372` | Yes | ✓ FLOWING |
| Locked-overshoot advisory (AJAX patch) | `floorByRole` | `BuildFloorMap(state.RoleFloors)` | **No** — user-set floors only | ⚠ HOLLOW |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Solution builds clean | `dotnet.exe build DeckFlow.sln -c Release` | Build succeeded, 0 Warning(s), 0 Error(s) | ✓ PASS |
| Full suite green | `dotnet.exe test DeckFlow.sln -c Release --no-build` | Studio 426/0, Web 2155/0 (16 skipped), Core 2011/0 → **4592 passed, 0 failed** | ✓ PASS |
| Snapshot is reproducible from the research artifact | Independent Python re-implementation of the adoption filter over `RESEARCH-FINDINGS.json` | 678 commanders / 1463 pairs, key sets identical, **0 mismatched commanders** | ✓ PASS |
| Snapshot contains no out-of-scope role | JSON role-key scan | Only `ramp, draw, interaction-targeted, engines, payoffs, wincons` | ✓ PASS |
| Snapshot floors are positive integers | JSON value scan | min 1, max 23, all `int` | ✓ PASS |
| Snapshot copied to build output | `ls bin/Release/net10.0/Data/role-floor-baseline/` | `latest.json` present (Web and Web.Tests) | ✓ PASS |
| Six-column table renders correctly across themes/mobile | Playwright | Performed by executor under 03-05 Task 4 and **developer-approved 2026-07-29**; not re-run here (no browser per instruction) | ? SKIP (already human-approved) |

Probe execution: N/A — this repository has no `scripts/*/tests/probe-*.sh` convention and neither the
plans nor the validation contract declare probes.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| — | — | `TODO` / `FIXME` / `XXX` / `TBD` / `HACK` / `PLACEHOLDER` scan across all 29 changed non-planning files | — | **None found.** Debt-marker gate passes. |
| `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts` | 264 | Stale fixture: `<span data-cut-lab-floor-source-default>Default for B3: 5</span>` — the real markup now renders `Bracket`/`Commander` with that sentence moved to `title`. | ℹ Info | Fixture only exercises the hidden-class toggle; no assertion depends on the text. Not a defect. |

Line endings and CSS fences hold: `git diff --stat 84a4d5f4..HEAD -- wwwroot/css/` touches **only**
`site-common.css` (+13), `site.css` and all 24 guild themes unchanged, no new CSS custom property.

---

## Honesty of the Summaries (requested check #5)

Six claims spot-checked against code; five substantiated, one structurally corroborated.

| # | Claim | Source | Finding |
|---|---|---|---|
| 1 | "678 commanders, 1463 adoptedPairs, sampleSize 841, no `lands`/`interaction-mass`/`protection` anywhere, every floor an integer >= 1" | 03-02 | **SUBSTANTIATED — independently recomputed.** Exact match on every count and key, 0 mismatched commanders. This is the strongest single piece of evidence in the phase. |
| 2 | "Registered `IRoleFloorBaselineProvider` in `Program.cs` beside the existing baseline registrations and added startup `EnsureLoaded()` warm-up" | 03-03 | **SUBSTANTIATED** — `Program.cs:96` and `:314`. |
| 3 | "`CutLabStructuralFindingsTests.cs:343` needed no edit and was never modified" | 03-07 | **SUBSTANTIATED** — `git diff 84a4d5f4..HEAD` for that file is empty. |
| 4 | Three exact advisory sentences, including the conservatism disclosure | 03-06 | **SUBSTANTIATED** — verbatim at `CutLabViewModel.cs:399-434`, present in **both** branches (with and without relax candidates). |
| 5 | "`BuildFloorRows_OutOfScopeRole_ShowsNotApplicable` pins the first by supplying **non-null** commander values (40 / 9 / 8) and still demanding `n/a`" | 03-05 | **SUBSTANTIATED** — `CutLabViewModelTests.cs:13-17`. The claim about the test's discriminating power is accurate. |
| 6 | "Reset to default on `interaction-targeted` restored 10 (the max), not 7 (the bracket)" + per-theme/mobile observations | 03-05 Task 4 | **NOT INDEPENDENTLY VERIFIABLE** (screenshots untracked at `.foreman/scratch/`, no browser permitted here). **Structurally corroborated**: `CutLab.cshtml:800` and `:828` emit `data-cut-lab-floor-default="@row.DefaultValue"`, and `CutLabFloorDefaults.cs:114` sets `DefaultValue = effectiveDefault` = the `max()`. The DOM contract the claim depends on is correct. Developer-approved, so accepted. |

The mutation-check narrative in 03-01 could not be re-executed (it requires temporarily inverting
production code). The test it names, `Build_P25BelowOne_IsDroppedAsNoSignal`, exists and passes
(`RoleFloorBaselineTests.cs:33`), and the ordering it claims to pin is visible at
`RoleFloorBaseline.cs:63-67` (`Math.Floor` **then** `<= 0`).

No claim was found to be false.

---

## Known Deliberate Gaps — Confirmed As Described

All three are exactly as the phase recorded them. None is a new finding.

| Item | Confirmation |
|---|---|
| `CedhLandBaselineProvider` retains the explicit-JSON-null NRE gap that was fixed in `RoleFloorBaselineProvider` | ✓ Confirmed — `CedhLandBaselineProvider.cs:96-119` has no `snapshot is null \|\| snapshot.Commanders is null` guard, unlike `RoleFloorBaselineProvider.cs:118-124`. Live production code, no Phase 3 requirement binds it. Correctly deferred. |
| `--generated` regex uses `$` rather than `\z` | ✓ Confirmed — `RoleFloorBaselineCommandRunner.cs:15` `new(@"^\d{4}-\d{2}-\d{2}$")`, mirroring the `main` precedent. |
| `min-width: 601px` leaves a 600 < w < 601 sliver | ✓ Confirmed — `site-common.css:4447` opens `@media (min-width: 601px)`; the five other Cut Lab blocks are `max-width: 600px`. Table degrades to auto-width, not broken. |

---

## What Is NOT Done (requested check #6)

**Promised but not delivered:**

1. **Criterion 5 does not reach the AJAX path.** See "Integration Gap" above. WARNING, not BLOCKER.
2. **The 03-06 feasibility banner has never been seen by a human.** 03-05's Task 4 checkpoint ran
   before 03-06 existed, and 03-06 carried no human-verify gate of its own. The banner's copy is
   unit-pinned; its rendering across themes and at mobile widths is unverified. 03-05's own summary
   records that "this defect shipped past 2,138 passing Web tests" for exactly this class of problem
   (a CSS collapse xUnit cannot see) — the same exposure now exists for the banner.
3. **Roadmap/requirements bookkeeping is stale.** `ROADMAP.md:16` marks the phase `[x]` complete, but
   `ROADMAP.md:370` still reads `0/7 | Planned`, all seven plan checkboxes at `:320-326` are `- [ ]`
   while the heading above them says "7/7 plans complete", and `REQUIREMENTS.md:31-34` / `:72-75`
   still show RFLR-05..08 as `- [ ]` / `Pending`. Documentation only; no code impact.

**Delivered that no requirement asked for (all justified, none scope creep):**

- `CutLabFloorFeasibility` (03-06) — not in RFLR-05..08, but mandated by D-06/D-06a, which exist
  because `max()` only raises with nothing pushing back. Defensible.
- `CommanderBaselineKeys` (03-03) — a behavior-preserving extraction of
  `CedhLandBaselineProvider.CandidateKeys`; the diff is a pure move plus a delegation call.
- `CutLabFloorRowView.SourceDetail` — a new field created by splitting the old `SourceLabel` sentence
  into a one-word label plus a `title` tooltip. Settles the "Source column wording" item that
  03-CONTEXT left to the implementer's discretion. Verified not to leak into the patch DTO, so there
  is no server/client drift.

**Not tested, but matching precedent:** `RoleFloorBaselineCommandRunner` has no automated tests; its
three failure paths were exercised by live runs documented in 03-02-SUMMARY. `CedhBaselineCommandRunner`
on `main` is equally untested, and all the pure logic (`RoleFloorBaseline`, `RoleFloorBaselineDriftCheck`)
does live in `DeckFlow.Core` with 19 Core tests. Not counted as a gap.

---

## Gaps Summary

The phase delivers what it promised. The snapshot is real and independently reproducible from the
Phase 2 research artifact; the provider is registered, warmed, and fail-open; the `max()` resolution is
correct and carries both components; the table shows six columns with two genuinely distinct empty
states proven by a test that supplies real data and demands suppression. 4,592 tests pass, zero fail,
zero debt markers.

The single gap is a seam, not a hole: plan 03-04's commander-aware floors reach plan 03-07's headroom
ranking on the page render but not on the AJAX decide path, because Cut Lab has only ever persisted
user-set floors in its state. That is a pre-existing design that Phase 3 inherited and now depends on.
It should be closed — either by carrying effective floors into the serialized state or by re-resolving
them server-side in `CutLabApiController` — before the locked-overshoot advisory can be described as
commander-aware without qualification.

Recommendation: **proceed**, with the integration gap and the unverified banner logged as follow-ups.

---

*Verified: 2026-07-29T08:06:37Z*
*Verifier: Claude (gsd-verifier), goal-backward, FORCE stance*
