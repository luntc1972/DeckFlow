---
phase: 102-structural-analysis-role-floors
verified: 2026-07-19T21:45:00Z
status: passed
score: 36/36 must-haves verified
overrides_applied: 0
deferred:
  - truth: "No later cut suggestion may silently break a floor (enforcement inside a live cut loop)"
    addressed_in: "Phase 103"
    evidence: "ROADMAP Phase 103 depends on 'Phase 102 (structural findings drive round ordering and floor warnings)' and SC3: 'Every proposed cut shows its measurable tradeoff deltas before the user decides'. Phase 102 ships the mandatory CutLabFloorRules.Evaluate contract (quantity-aware after WR-03 fix, commit 18cd8d40); Phase 103's cut engine must route every proposed cut through it."
human_verification:
  - test: "Eyeball the [ASSUMED] floor/finding product constants: FallbackLands=36 (CutLabFloorDefaults.cs:11), the unsigned per-role floor table (CutLabFloorDefaults.cs:99), and the fixed finding thresholds (CutLabStructuralFindings.cs:70)"
    expected: "Constants match your play experience across brackets (B1 fallback, missing-bracket fallback, cEDH); adjust any value that reads wrong before Phase 103 builds cut logic on top of them"
    why_human: "Values are product judgment flagged [ASSUMED] in RESEARCH A3 and in code xmldoc; grep can only prove they exist, not that they are right"
  - test: "Flip tool.cut-lab.enabled ON in a local/dev environment, import a real 101-150 card pool you know well, and read the three new sections (role accordions, structural findings, role floors)"
    expected: "Role assignments look sensible for cards you know (no land counted as ramp, one-shot removal in interaction, multi-role cards honest); findings read as useful advisories, not noise; floor edit -> Recalculate round-trip keeps your value and the Adjusted badge"
    why_human: "Classification quality on real decks and advisory copy usefulness are UX judgments; e2e proves the mechanics, not the read's sensibility"
---

# Phase 102: Structural Analysis & Role Floors Verification Report

**Phase Goal:** A builder sees exactly how their pool is structurally composed — and what floors protect it — before any cut is ever proposed. (SLOT-01, SLOT-02, FLOOR-01, FLOOR-02, behind the OFF `tool.cut-lab.enabled` flag)
**Verified:** 2026-07-19T21:45:00Z at HEAD `8fc652b0` (branch `gsd/cycle18-cut-lab`)
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths — ROADMAP Success Criteria

| # | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1 | Pool cards grouped by functional slot competition using existing role/category inference, no new classification model | ✓ VERIFIED | `CutLabRoleAssigner.AssignRoles` (CutLabRoleAssigner.cs:63) composes only existing signals: `PlanRoleClassifier.Classify` (pre-gate overload, :75), `DeckStatClassifier.Is*` (:87-119), `CutLabLockRules.IsLand`, combo membership. View renders 8 `details.cutlab-role-group` accordions under "How your pool competes" (CutLab.cshtml:310-314); e2e asserts count 8 (cut-lab-structure.spec.ts:105) |
| 2 | Structural findings with supporting evidence: curve congestion, stranded subthemes, redundant finishers, weak floor cases, enabler-starved | ✓ VERIFIED | All five detectors present and wired (CutLabStructuralFindings.cs:140-152: ComputeCurveCongestion, ComputeStrandedSubthemes, ComputeRedundantFinishers, ComputeWeakFloorCases, ComputeEnablerStarved); per-card evidence + two availability flags (`categoryDataAvailable` param :132, `ComboDataUnavailable`/`CategoryDataUnavailable` rendered CutLab.cshtml:391-396) |
| 3 | Default floors for all 8 roles (lands, ramp, draw, interaction, protection, engines, payoffs, wincons) derived from declared bracket and plan | ✓ VERIFIED | `CutLabFloorRules.RoleKeys` = exactly the 8 keys (:13-23); `CutLabFloorDefaults.ResolveDefaults` (:52) derives lands via `TryGetBracketBaseline` (:156, FallbackLands=36 never 0/null), ramp/draw via promoted `public static int CalculateTargetRamp` (ManabaseRampDrawBudget.cs:114, consumed at CutLabFloorDefaults.cs:67) |
| 4 | User can adjust any role floor; breaking one always carries an explicit warning, never silent | ✓ VERIFIED (contract) | 8-row floor editor (CutLab.cshtml:405-424, `data-cut-lab-floor`), TS serializes `roleFloors` camelCase (cut-lab.ts:34,117,355), `IsUserSet` merge wins over recomputed defaults (CutLabFloorDefaults.cs:87-93,130-137), `Evaluate` returns one `CutLabFloorWarning` per broken floor with fixed copy, quantity-aware (CutLabFloorRules.cs:95-100 `int quantity = 1`; test `Evaluate_QuantityCut_UsesProvidedQuantityAndClampsAtZero`). Live cut-loop enforcement deferred to Phase 103 by design (see Deferred Items) |

### Observable Truths — Plan must_haves (32/32)

| Plan | Truth | Status | Evidence |
| ---- | ----- | ------ | -------- |
| 01 | Pre-102 blob deserializes with RoleFloors=[] | ✓ | `RoleFloors { get; init; } = []` additive (CutLabState.cs:22); test `Deserialize_Pre102JsonWithoutRoleFloors_ReturnsEmptyRoleFloors_AndReLocksCommander` |
| 01 | Tampered roleFloors corrected/dropped at deserialize | ✓ | `Deserialize` returns `CutLabFloorRules.ClampFloors(CutLabLockRules.EnforceCommanderLock(state))` (CutLabStateSerializer.cs:56); tests `Deserialize_TamperedRoleFloors_ClampsAndDropsInvalidEntries`, `ClampFloors_NegativeAndAbsurdFloors/UnknownRoleKey/DuplicateRoleKeys` |
| 01 | Defaults for 8 roles from bracket + play experience, deliberate fallbacks, never 0/null lands | ✓ | `FallbackLands = 36` (CutLabFloorDefaults.cs:11,158); ResolveDefaults covers B1/missing bracket/missing baseline; CutLabFloorDefaultsTests |
| 01 | User-set floor survives default recomputation | ✓ | Override merge keyed on `IsUserSet` (CutLabFloorDefaults.cs:87-93,130-137); e2e round-trip proves persistence |
| 01 | Floor-break evaluation returns explicit warning per broken floor, fixed FLOOR-02 copy | ✓ | `CutLabFloorWarning` record with "Fixed UI warning copy" (CutLabFloorRules.cs:72-84), Evaluate :95 |
| 02 | Every pool card maps via ONLY existing signals | ✓ | AssignRoles body uses PlanRoleClassifier/DeckStatClassifier/IsLand/isComboPiece only — no new keyword lists in CutLabRoleAssigner.cs |
| 02 | Land NEVER counts as ramp | ✓ | `!CutLabLockRules.IsLand(typeLine) && DeckStatClassifier.IsRampCard(...)` with // Why: comment (CutLabRoleAssigner.cs:84-87) |
| 02 | One-shot removal + cEDH counterspells land in interaction | ✓ | `interactionMeritPreGate` out-param captured pre-permanent-gate (PlanRoleClassifier.cs:48,70; CutLabRoleAssigner.cs:75,97) |
| 02 | Ramp/draw/lands populate despite PlanRole exclusion | ✓ | DeckStatClassifier.IsRampCard/IsDrawCard + IsLand drive those roles directly (:87-92) |
| 02 | Multi-role cards allowed (payoffs+wincons overlap honest) | ✓ | wincons = `IsClosingPowerCard || isComboPiece` ungated (:119); roles accumulate, no precedence |
| 02 | Five detectors return finding+evidence when triggered, nothing otherwise | ✓ | Five Compute* methods, each yield-based on signal (CutLabStructuralFindings.cs:158-261); CutLabStructuralFindingsTests |
| 02 | Stranded-subtheme exclusion via shared PlanRoleClassifier vocabulary | ✓ | `PlanRoleClassifier.CategoryMapsToPlanRole` (:100) consumed at CutLabStructuralFindings.cs:206 — no duplicated substring list |
| 02 | Failed-open sources report data-unavailable, not false negatives | ✓ | `categoryDataAvailable` gate (:142-144) skips stranded-subthemes; nearCombos gate (:152) skips enabler-starved |
| 03 | POST computes roles/findings/floors server-side; derived data never serialized | ✓ | Stages wired in ProcessAsync (AssignRoles :485, ResolveDefaults :246, Findings.Compute :258); CutLabState carries only RoleFloors — no role/finding members |
| 03 | Spellbook once + ONE batched category query, fail-open with flags | ✓ | Single `GetCategoriesForNamesAsync` call site (CutLabPageService.cs:583); availability flags flow to findings/view |
| 03 | Outage still renders page | ✓ | Fail-open catch blocks incl. banlist (post CR-01 fix, :230-240, commit a2fe4408) |
| 03 | DI-guard: dropped registration cannot silently degrade | ✓ (as specified) | `HasStructuralAnalysisDependencies` internal probe (:134) + ServiceCollection-mirror test (CutLabPageServiceTests.cs:563,574). WR-04 documents that the mirror cannot catch a real Program.cs regression — accepted as plan-specified limitation (plan 03 prescribed exactly this mechanism) |
| 03 | User floors survive POST round-trip | ✓ | Merge + re-serialize; e2e "Adjusted badge persists after Recalculate" (cut-lab-structure.spec.ts:126-160) |
| 03 | PoolStatusText gone | ✓ | Zero matches repo-wide (excluding obj/bin) |
| 04 | Three sections render in order | ✓ | h2 "How your pool competes" :310, "Structural findings" :356, "Role floors" :405 in CutLab.cshtml |
| 04 | Pool table single canonical lock surface; chips display-only | ✓ | `syncRoleGroupLockState` reads pool-table checkbox state (cut-lab.ts:287-307); chips have no handlers |
| 04 | Standalone Lock-all-lands pill gone; per-group pills | ✓ | `data-cut-lab-lock-all-lands` zero matches anywhere; `data-cut-lab-lock-role` pill per group (CutLab.cshtml:324; cut-lab.ts:148,762) |
| 04 | Multi-role token attribute, TS matches by token | ✓ | `data-cut-lab-role="@roleKeys"` space-separated (:193); `hasRoleToken` split(/\s+/) match (cut-lab.ts:78-89); Vitest pins `hasRoleToken('land','lands')===false` |
| 04 | Floor edit marks Adjusted, live at-floor marker, camelCase roleFloors serialization | ✓ | cut-lab.ts floor rows (:157-159,355); e2e regex `/"roleFloors":\[.*"role":"interaction".*"isUserSet":true/` |
| 04 | Findings use gold-warning idiom, never --danger | ✓ | `.cutlab-finding` uses `var(--gold-warning, var(--warning, #c8a040))` (site-common.css:4192+); no `--danger` in the block |
| 04 | Castability copy replaced; no hard-coded form selector | ✓ | `form[action` zero matches in cut-lab.ts (data-cache-key hook) |
| 04 | Test-only BulkLockRoleGroup server path removed | ✓ | Zero matches; removed in commit 58341ad1 |
| 05 | Live import renders all three sections, 8 accordions, 8-row floor editor | ✓ | cut-lab-structure.spec.ts:104-107 (`toHaveCount(8)` groups + floor inputs); spec passed per orchestrator |
| 05 | Floor adjust writes roleFloors; Adjusted + value persist after Recalculate | ✓ | spec :126-160 (`[data-cut-lab-recalculate]` click + post-round-trip assertions) |
| 05 | At-floor marker within 1 of pool count | ✓ | spec :163-174 (`[data-cut-lab-floor-at-marker]` toContainText('at floor')) |
| 05 | Theme x viewport screenshots exist | ✓ | 3 themes x 2 viewports present under `.planning/ui-design/cut-lab/screenshots/` (classic/azorius/nyx, desktop/mobile); UI review completed per orchestrator |
| 05 | Full Cut Lab test surface green | ✓ | Orchestrator-verified at HEAD: Core 1598/1598, Web 1696/1696 (+16 PG skips), Vitest 47/47, both e2e specs pass headless (one known Scryfall-throttle screenshot flake under combined load, passes isolated) |

**Score:** 36/36 truths verified (4 roadmap SCs + 32 plan truths)

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | FLOOR-02 enforcement inside a live cut loop ("no later cut suggestion may silently break a floor") | Phase 103 | Phase 103 depends on "Phase 102 (structural findings drive round ordering and floor warnings)"; Phase 102 ships the mandatory, quantity-aware `CutLabFloorRules.Evaluate` contract with xmldoc mandating Phase 103 route every proposed cut through it |

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs` | ClampFloors + Evaluate contract | ✓ VERIFIED | 172 lines; ClampFloors :32, Evaluate :95 (quantity param post WR-03) |
| `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs` | ResolveDefaults + merge | ✓ VERIFIED | 183 lines; ResolveDefaults :52, override merge, FallbackLands |
| `DeckFlow.Web/Models/CutLab/CutLabState.cs` | Additive RoleFloors | ✓ VERIFIED | `{ get; init; } = []` (carve-out compliant, never get-only) |
| `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` | public CalculateTargetRamp | ✓ VERIFIED | :114 `public static int CalculateTargetRamp` |
| `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs` | AssignRoles, ramp land-gated | ✓ VERIFIED | 126 lines; all key links wired |
| `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` | 5 detectors + availability flags | ✓ VERIFIED | 347 lines; all five Compute* present |
| `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` | CategoryMapsToPlanRole shared helper | ✓ VERIFIED | :100, consumed by findings :206 |
| `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` | Stages A-F + DI probe | ✓ VERIFIED | 721 lines; GetCategoriesForNamesAsync :583, ToCardFact :481, HasStructuralAnalysisDependencies :134 |
| `DeckFlow.Web/Models/CutLabViewModel.cs` | RoleGroups/Findings/FloorRows | ✓ VERIFIED | FloorRows :74,129; BuildFloorRows :168; rendered by view |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` | Three sections + role column + copy | ✓ VERIFIED | 474 lines; headings :310/:356/:405; role tokens :193 |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` | Floor serialization, token matching, per-group lock | ✓ VERIFIED | 841 lines; roleFloors, hasRoleToken, data-cut-lab-lock-role handler |
| `DeckFlow.Web/wwwroot/css/site-common.css` | .cutlab-role-group/.cutlab-finding/... | ✓ VERIFIED | New classes at :4154+, gold-warning idiom, layout in site-common.css (constraint honored) |
| `DeckFlow.Web/e2e/cut-lab-structure.spec.ts` | E2e proof + screenshots | ✓ VERIFIED | 215 lines; setToolEnabled harness, roleFloors regex, screenshot matrix |

### Key Link Verification

| From | To | Via | Status |
| ---- | --- | --- | ------ |
| CutLabStateSerializer.Deserialize | CutLabFloorRules.ClampFloors | chained after EnforceCommanderLock (:56) | ✓ WIRED |
| CutLabFloorDefaults | CalculateTargetRamp | ramp/draw derivation (:67) | ✓ WIRED |
| CutLabFloorDefaults | IManabaseBaselineProvider.TryGetBracketBaseline | lands floor (:156) | ✓ WIRED |
| CutLabRoleAssigner.AssignRoles | PlanRoleClassifier.Classify | interactionMeritPreGate out-param (:75) | ✓ WIRED |
| CutLabRoleAssigner.AssignRoles | DeckStatClassifier | land-gated ramp + 5 other predicates (:84-119) | ✓ WIRED |
| CutLabStructuralFindings | PlanRoleClassifier.CategoryMapsToPlanRole | vocabulary exclusion (:206) | ✓ WIRED |
| CutLabStructuralFindings | SpellbookAlmostCombo | enabler-starved evidence (:129,261-263) | ✓ WIRED |
| CutLabPageService | ToCardFact / AssignRoles / ResolveDefaults / Findings.Compute | :481 / :485 / :246 / :258 | ✓ WIRED |
| CutLabPageServiceTests | HasStructuralAnalysisDependencies probe | :563,:574 | ✓ WIRED (WR-04 limitation accepted) |
| CutLab.cshtml floor inputs | cut-lab.ts buildCutLabStateJson | data-cut-lab-floor -> roleFloors camelCase | ✓ WIRED |
| CutLab.cshtml pool rows | cut-lab.ts token matching | space-separated data-cut-lab-role | ✓ WIRED |
| role-group pills | pool-table checkboxes | data-cut-lab-lock-role, single lock source | ✓ WIRED |
| cut-lab-structure.spec.ts | /cut-lab page | setToolEnabled harness + hidden-state roleFloors regex | ✓ WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Real Data | Status |
| -------- | ------------- | ------ | --------- | ------ |
| CutLab.cshtml role accordions | Model.RoleGroups | CutLabViewModel.From <- ProcessAsync AssignRoles over resolved Scryfall pool | Yes | ✓ FLOWING |
| CutLab.cshtml findings | Model.Findings | CutLabStructuralFindings.Compute over analyzed cards + Spellbook + category store | Yes (fail-open flags rendered when degraded) | ✓ FLOWING |
| CutLab.cshtml floor table | Model.FloorRows | BuildFloorRows(ResolveDefaults(bracket, baseline, commander MV, prior user floors)) | Yes | ✓ FLOWING |
| Hidden CutLabStateJson | roleFloors | User edits -> TS serialization -> server clamp -> merge -> re-serialize | Yes (e2e round-trip proven) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Check | Result | Status |
| -------- | ----- | ------ | ------ |
| Full test surface | Orchestrator-verified at HEAD (per no-full-suite constraint) | Core 1598/1598, Web 1696/1696, Vitest 47/47, both e2e green | ✓ PASS (delegated) |
| Screenshot evidence | ls .planning/ui-design/cut-lab/screenshots/ | 3 themes x 2 viewports present | ✓ PASS |
| SUMMARY commit hashes | git cat-file -t on all hashes from 5 SUMMARYs | All resolve to commits | ✓ PASS |

### Probe Execution

No `scripts/*/tests/probe-*.sh` exist and no plan declares probes — SKIPPED (not a migration/tooling phase).

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
| ----------- | ------------ | ----------- | ------ | -------- |
| SLOT-01 | 102-02, 03, 04, 05 | Pool grouped by functional slot competition via existing inference | ✓ SATISFIED | CutLabRoleAssigner + 8 accordions + e2e |
| SLOT-02 | 102-02, 03, 04, 05 | Structural findings with evidence (5 kinds) | ✓ SATISFIED | Five detectors + evidence + degradation flags rendered |
| FLOOR-01 | 102-01, 03, 04, 05 | Default 8-role floors from bracket and plan | ✓ SATISFIED | ResolveDefaults (baseline lands, CalculateTargetRamp ramp/draw, bracket-scaled table) |
| FLOOR-02 | 102-01, 03, 04, 05 | Adjustable floors; break always carries explicit warning | ✓ SATISFIED (contract) | Editor + persistence + quantity-aware Evaluate; live-loop enforcement deferred to Phase 103 per roadmap |

No orphaned requirements: REQUIREMENTS.md maps exactly SLOT-01/02 + FLOOR-01/02 to Phase 102; all four claimed by plans. 21/21 cycle requirements remain mapped.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| — | — | No TBD/FIXME/XXX/TODO/HACK/placeholder/stub markers in any phase-modified file | — | — |

`[ASSUMED]` xmldoc markers on floor/finding constants (CutLabFloorDefaults.cs:10,99; CutLabStructuralFindings.cs:70) are deliberate research-mandated provenance flags, not debt markers — routed to human verification below.

### Code Review Fix Verification

| Finding | Severity | Claimed Fix | Verified in Code |
| ------- | -------- | ----------- | ---------------- |
| CR-01 banlist failure wipes session | Critical | a2fe4408 | ✓ try/catch HttpRequestException around ResolveBannedCardsPresentAsync (:230-240), fail-open warning |
| WR-01 lock-count unit mismatch | Warning | 673c3609 | ✓ TS `parseRowQuantity` quantity-weighted (cut-lab.ts:189,301) |
| WR-02 Deserialize cap + package pruning | Warning | 59eb7fac | ✓ `GetByteCount(json) > MaxUploadBytes` reject (:40); tampered-packages test caps at 50 |
| WR-03 quantity-blind Evaluate | Warning | 18cd8d40 | ✓ `int quantity = 1` param + `Evaluate_QuantityCut_...` test |
| WR-04 DI-guard scope | Warning | Accepted | ✓ Plan-specified mechanism; limitation documented in review |
| WR-05 first-commander MV latch | Warning | 8fc652b0 | ✓ `Math.Max(commanderManaValue, fact.ManaValue)` (:494) |

### Human Verification Required

### 1. [ASSUMED] product constants sanity pass

**Test:** Review FallbackLands=36, the per-role floor table (CutLabFloorDefaults.cs:99 region), and finding thresholds (CutLabStructuralFindings.cs) against your bracket/play experience.
**Expected:** Values match real Commander/cEDH intuition; adjust anything that reads wrong before Phase 103 builds the cut engine on these floors.
**Why human:** These are product judgments flagged [ASSUMED] by 102-RESEARCH A3 explicitly "listed for user adjustment at review" — not programmatically verifiable.

### 2. Live structural-read UAT behind the flag

**Test:** Enable `tool.cut-lab.enabled` locally, import a familiar 101-150 pool, and read all three sections; edit a floor and Recalculate.
**Expected:** Role assignments look sensible for known cards, findings are useful advisories, floor round-trip preserves your edit and the Adjusted badge.
**Why human:** Classification quality and advisory copy usefulness on real decks are UX judgments; e2e proves mechanics only.

### Gaps Summary

No gaps. All 36 must-haves verified directly in code at HEAD `8fc652b0`: the floor domain layer (clamp chain, defaults, quantity-aware Evaluate contract), the two pure rule sets (8-role assigner with land-gated ramp; five finding detectors with fail-open flags), the page-service orchestration (single batched category query, fail-open banlist/Spellbook, DI probe), the full UI (three sections, token-based multi-role table, floor editor with camelCase roleFloors persistence, gold-warning findings, no --danger), and the e2e structure spec with screenshot matrix. All five review fixes (CR-01, WR-01/02/03/05) are present in code, WR-04 accepted as plan-specified. The feature remains correctly behind the OFF `tool.cut-lab.enabled` flag (FeatureFlagStore seeds FALSE/0 both dialects). FLOOR-02's live cut-loop enforcement is a Phase 103 deliverable by roadmap design and is recorded as deferred, not a gap. Status is human_needed solely for the two judgment items above.

---

_Verified: 2026-07-19T21:45:00Z_
_Verifier: Claude (gsd-verifier)_
