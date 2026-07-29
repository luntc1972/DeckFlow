# Phase 3: Commander-Aware Floor Defaults - Research

**Researched:** 2026-07-28
**Domain:** ASP.NET Core / C# service-layer floor resolution (Cut Lab), CLI snapshot generation, Razor + TypeScript UI
**Confidence:** HIGH (both blocking questions resolved from committed code with file:line evidence; no live corpus query was needed or attempted)

## Summary

Both blocking open questions are resolved definitively from the codebase, not inferred.

**O-1: Cut Lab role assignment is explicitly NOT mutually exclusive.** `CutLabRoleAssigner.AssignRoles`'s own docstring states "Multi-role membership is allowed" (`CutLabRoleAssigner.cs:13-14`), and this is enforced in production: `CutLabAnalysisContextBuilder.cs:219-224` increments **every** role a card carries by its quantity, so one physical card slot inflates two or more role counts simultaneously. Only two pairs are exclusive by explicit gate: lands↔ramp (`CutLabRoleAssigner.cs:133-139`) and interaction-targeted↔interaction-mass (`CutLabRoleAssigner.cs:150-163`, plus a dedicated test at `CutLabRoleAssignerTests.cs:230-250`). Every other pair can co-occur — most notably **engines is a strict subset of draw** (any card assigned "engines" must first satisfy `IsDrawCard(oracle)`, the sole gate for "draw" — proven by `CutLabRoleAssignerTests.cs:270-284,303-318`, `Rhystic Study`/`Phyrexian Arena` → `["draw","engines"]`), and **wincons can co-occur with any other role via `isComboPiece`**, independent of every other predicate (`CutLabRoleAssignerTests.cs:252-267`, Swords to Plowshares + combo-piece → `["interaction-targeted","wincons"]`). D-06's aggregate-infeasibility arithmetic (78 vs ~63 slots) was measured through the same production classifier and therefore already contains this double-counting — the true card cost to satisfy overlapping floors is *less* than the naive floor sum, which loosens the infeasibility trigger rather than tightening it. See the full breakdown below.

**O-3: `CutLabCutRoundEngine`'s overshoot ranking does NOT have in-pool counts or the effective floor in scope today**, and both must be threaded in for D-13. `RolePriority`/`AdvisoryRoleFor` (`CutLabCutRoundEngine.cs:438-444`, called from `BuildLockedOvershootAdvisory` at line 402-436) receive only `IReadOnlyList<CutLabRoundInputCard> workingList` — no floor dictionary, no in-pool-count dictionary. `floorByRole` exists one level up, at `BuildFindingsAndRoundPlan` (`CutLabCutRoundEngine.cs:321-330`), but is only forwarded to `CutLabStructuralFindings.Compute` (line 333-342) — it is **not** passed to the `BuildQueue` call at line 343-348. The in-pool `RoleCounts` dictionary already exists on `CutLabAnalysisContext` (`CutLabAnalysisContextBuilder.cs:66`) and is available at the `BuildFindingsAndRoundPlan` call site (via the `context` parameter) but likewise never reaches `BuildQueue`. Every signature between `BuildFindingsAndRoundPlan` and `RolePriority` must change. See the full call chain below.

**Primary recommendation:** Plan D-06's aggregate-feasibility advisory as a genuine overlap-aware calculation (or at minimum caveat the naive-sum trigger threshold in the advisory copy), and plan D-13 as a three-parameter threading change (`BuildQueue` → `BuildLockedOvershootAdvisory` → `AdvisoryRoleFor`) plus a rewrite of the two overshoot-order tests that currently assert the old fixed order.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Commander floor resolution (p25 lookup, max() with bracket) | API / Backend (`DeckFlow.Web/Services/CutLab`) | — | Pure C# service logic, no I/O beyond the bundled JSON read |
| Bundled snapshot storage | Database / Storage (flat file under `Data/`) | API / Backend (in-memory cache) | Mirrors `cedh-land-baseline`: file-backed, `IMemoryCache`-fronted, no live DB query at request time |
| Snapshot generation | API / Backend (`DeckFlow.CLI`) | Database / Storage (reads committed `RESEARCH-FINDINGS.json`) | Offline generator, not part of the request path |
| Role-floor UI (six-column table) | Frontend Server (SSR, Razor) | Browser/Client (`cut-lab.ts` reads data-attributes, no new AJAX) | Full server render per request; existing client JS only toggles visual state, does not fetch role-floor data |
| Overshoot advisory (LockedOvershootAdvisory) | API / Backend (`CutLabCutRoundEngine`) | Browser/Client (`CutLabUiPatchBuilder` → AJAX patch → `cut-lab.ts` DOM update) | This one, unlike the floor table, **is** live-patched after every accept/reject decision |

## User Constraints (from CONTEXT.md)

<user_constraints>

### Locked Decisions

- **D-01: The commander floor is p25, not the mean.** Standalone, not pre-clamped to bracket.
- **D-02: Fractional p25 truncates down (`Math.Floor`)**, never `Math.Round` (banker's rounding would make `7.5→8` but `6.5→6`).
- **D-03: `p25 = 0` is treated as no signal** and falls back to the bracket value (13 commander-role pairs affected: engines 8, ramp 2, draw 2, interaction-targeted 1).
- **D-04: The effective default is `max(bracket-derived, commander-derived)`.** Commander data may only RAISE a floor, never lower one. **This AMENDS RFLR-05** from a priority chain to a max — planning must carry this amendment into REQUIREMENTS.md.
- **D-05: The ramp/draw 24-slot coupling is broken.** `drawDefault = 24 - rampDefault` no longer holds after `max()`; both resolve independently and may sum past 24. The stale comment at `CutLabFloorDefaults.cs:68` must be corrected.
- **D-06: Infeasible aggregate floor sums are detected and warned, never silently clamped.** Measured (assuming exclusivity — now known to be an overestimate, see O-1 below): 3/841 at bracket 2, 16/841 at bracket 4, 23/841 at bracket 5 exceed ~63 nonland slots; worst case `The Watcher in the Water` at 78 vs today's 56. Cut Lab has no aggregate feasibility guard today.
- **D-07: Commander floors come from the Postgres arm only.** EDHREC's 13,725 cells carry `count`/`deckCount` only, no percentile, and uneven bracket coverage.
- **D-08: The bundled snapshot carries 678 commanders and adopted floors only** (cleared `clearsBar` AND `p25 > 0`). Minifies to 55.8 KB. Cannot distinguish "commander absent" from "role did not clear" at runtime — D-11 must not pretend otherwise.
- **D-09: A new `DeckFlow.CLI` converter produces the snapshot, guarded by a fail-closed drift check**, mirroring `CedhBaselineCommandRunner.cs`. **O-2 is CLOSED per the orchestrator: the branch will be rebased onto main (`1511dd95`) before implementation**, at which point `CedhBaselineDriftCheck` and `CedhDriftThresholds` exist and can be mirrored directly (verified present on `main`, absent in this worktree — see below).
- **D-10: Commander lookup reuses `CedhLandBaselineProvider.CandidateKeys`' shape** — solo name, then both partner orders, never splitting a DFC's `" // "` form. Corpus has zero partner-pair keys, 50 DFC keys.
- **D-11: The table gains two labelled columns: `Role | In pool | Bracket | Commander | Floor | Source`.** Match the mock in `<specifics>` literally.
- **D-12: Two empty-cell states** — `n/a` for structurally out-of-scope roles (lands, interaction-mass, protection); an empty marker for a GO role with no commander match.
- **D-13: `LockedOvershootRoleOrder` is reconciled, not merely justified.** Primary sort becomes headroom = `(in-pool count − effective floor)` descending; the existing fixed array becomes the deterministic tiebreak only.

### Claude's Discretion

- Source column wording ("Commander" / "Bracket") and its coexistence with the `Adjusted` badge and Reset button.
- Reset-to-default target: `data-cut-lab-floor-default` must carry the `max()` under D-04, in both Razor and `cut-lab.ts` (confirmed as the only writer of this attribute at `CutLab.cshtml:793,816` and the only reader at `cut-lab.ts:3948`).
- Theme handling for two extra columns across the 24 guild themes; layout CSS in `site-common.css`, never `site.css`.
- Whether the D-06 infeasibility advisory is a new `CutLabFindingKind` or a panel-level notice.

### Deferred Ideas (OUT OF SCOPE)

- Re-measuring lands properly (three options recorded in `02-08-SUMMARY.md`); lands is PULLED this phase.
- Protection floors — blocked on Phase 01.2's vocabulary widening.
- Fixing `DescribeHarnessCommitSha` for WSL worktrees.
- `.gitignore` decision for research caches.
- Dead `normalizeForScryfall` parameter.
- Bracket-aware commander floors — explicit Phase 5 deferral; commander floors are bracket-agnostic this cycle.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RFLR-05 | Priority-chain floor resolution (amended by D-04 to `max()`) | `CutLabFloorDefaults.ResolveDefaults`/`ResolveLandsDefault` fully traced below; exact insertion point for the new commander lookup identified at `CutLabFloorDefaults.cs:74-94` |
| RFLR-06 | Byte-identical output for below-bar commanders/roles | `CedhLandBaselineProvider`'s fail-open pattern (missing/corrupt file → "no baseline") traced below as the mechanism that makes this free |
| RFLR-07 | Unit coverage for commander-hit, fallback, role-not-in-scope paths | Existing test fixture pattern (`FakeBaselineProvider`, `FakeCedhBaselineProvider` in `CutLabFloorDefaultsTests.cs:200-238`) identified as the template for a new fake provider |
| RFLR-08 | Side-by-side Bracket/Commander UI columns, explicit empty marker | Full consumer chain traced: `CutLabResolvedFloor` → `CutLabFloorRowView` → `CutLab.cshtml` table (lines 779-821) → `cut-lab.ts` (reads only, no live floor-table patching today) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- Tech stack pinned: ASP.NET 10 + Razor, no framework migration.
- HTTP resilience: existing RestSharp + Polly v8 pattern only — not relevant to this phase (no new HTTP calls; the new provider reads a bundled file, same as `CedhLandBaselineProvider`).
- Theme CSS: layout changes belong in `site-common.css`, never `site.css`; token additions in `:root` of each theme file.
- Public repo: no secrets. Not applicable here (no credentials touched).
- Testing: VSTest unreliable in WSL; use `dotnet build` clean + targeted harness, or push-and-watch CI. The project's dotnet is the Windows binary at `"/mnt/c/Program Files/dotnet/dotnet.exe"`; do not set `MTG_DATA_DIR`.
- Formatting: `.editorconfig` changed-lines gate; the five carve-outs apply, most relevantly **never convert `{ get; init; }` to `{ get; }`** — `CutLabResolvedFloor` and the new snapshot DTOs are exactly the shape that has broken deserialization before (`EdhTop16Client` precedent cited directly in CLAUDE.md).
- Commits: plain author, no Co-Authored-By trailer, README updated when behavior changes.

## O-1 — Full Analysis: Is Cut Lab role assignment mutually exclusive?

**No.** `CutLabRoleAssigner.AssignRoles` (`DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs:112-197`) builds a `List<string>` and can append multiple role keys for the same card. The type's own XML doc says so explicitly:

> "Multi-role membership is allowed; cutting a card reduces every role count it currently fills." — `CutLabRoleAssigner.cs:13-14`

This is not just a theoretical allowance — it is load-bearing in the counting code that feeds every floor comparison. `CutLabAnalysisContextBuilder.cs:219-224`:

```csharp
foreach (string role in roles)
{
    roleCounts[role] = roleCounts.TryGetValue(role, out int count)
        ? count + entry.Quantity
        : entry.Quantity;
}
```

Every role a card carries gets its own increment by the card's quantity. A single physical card slot can therefore be counted toward two or more role floors simultaneously.

### Which pairs are actually exclusive (by construction)

1. **lands ↔ ramp** — explicit gate at `CutLabRoleAssigner.cs:136` (`if (!isLand && DeckStatClassifier.IsRampCard(...))`), commented as deliberate: "Lands and ramp stay disjoint by construction" (line 135).
2. **interaction-targeted ↔ interaction-mass** — explicit gate at `CutLabRoleAssigner.cs:154` (`if (!isMass && (...))`), and a dedicated regression test asserts this for five representative cards: `CutLabRoleAssignerTests.cs:230-250` (`AssignRoles_InteractionRolesStayMutuallyExclusiveAcrossRepresentativeCards`).

**Every other pair among the nine role keys can co-occur.** Concrete, code-proven examples:

| Pair | Mechanism | Evidence |
|---|---|---|
| **draw ⊇ engines (strict subset)** | Engines requires `roles.HasFlag(PlanRole.Engine) && !IsNonPermanentFront && IsDrawCard(oracle)` (`CutLabRoleAssigner.cs:174-179`). Draw's sole gate is `IsDrawCard(oracle)` (line 141). Any card satisfying the Engines gate therefore unconditionally satisfies Draw's gate too — **100% of "engines" cards are also "draw" cards.** | `CutLabRoleAssignerTests.cs:270-284` (Rhystic Study → `["draw","engines"]`), `:303-318` (Phyrexian Arena → `["draw","engines"]`) |
| **wincons ↔ any role via `isComboPiece`** | Wincons' gate is `IsClosingPowerCard(typeLine, oracle) \|\| isComboPiece` (`CutLabRoleAssigner.cs:186`). The `isComboPiece` branch is a Commander-Spellbook signal wholly independent of every other predicate, so it can fire alongside any other role a card already carries. | `CutLabRoleAssignerTests.cs:252-267` (Swords to Plowshares, normally `interaction-targeted`-only, with `isComboPiece: true` → `["interaction-targeted","wincons"]`) |
| **engines ↔ payoffs** | Both flow from `PlanRole` (`[Flags]` enum, `Payoff = 1`, `Engine = 2`). `PlanRoleClassifier.FromCategories`'s own doc: "A card tagged both 'Win Condition' and 'Card Draw' earns Payoff \| Engine." (`PlanRoleClassifier.cs:116`). Both bits set independently at lines 132-135 and 156-159 in the same loop — no exclusivity gate. | `PlanRoleClassifier.cs:113-163` (doc + code) |
| **payoffs ↔ wincons** | `IsClosingPowerCard(typeLine, oracle)` feeds `PlanRole.Payoff` via the heuristic path (`PlanRoleClassifier.cs:183-186`) *and* feeds Wincons directly (`CutLabRoleAssigner.cs:186`). A card where `IsClosingPowerCard` is true and role computation used the heuristic (not a category override) gets both. | `PlanRoleClassifier.cs:183-186`, `CutLabRoleAssigner.cs:186` |

### Consequence for D-06's arithmetic

D-06's 78-vs-63 / 23-of-841 figures were **measured through this same production classifier** (RFLR-01 requires "the real production classifiers," and `RoleFloorResearchCommandRunner.cs:364,399` calls `CutLabRoleAssigner.AssignRoles` directly). This means the per-role p25 counts already contain the same overlap the production floor table will show at runtime — an engines p25 of 4 for a commander already double-counts cards that are also drawing toward that commander's draw p25.

**Practical effect:** summing floors naively across roles (`ramp + draw + interaction-targeted + engines + payoffs + wincons`) overstates the number of *distinct card slots* actually required, because:
- Every card counted toward an "engines" floor is *also* counted toward "draw" — satisfying both floors from the same card pool costs `max(floor(draw), floor(engines))` cards for that overlap, not the sum.
- Any combo-piece card satisfies its other role(s) *and* wincons simultaneously — a wincons floor is partially "free" whenever the deck already runs combo pieces that qualify under another role.

The naive-sum trigger (78 vs ~63, 23/841 at bracket 5) is therefore an **overestimate of true infeasibility** — the real constraint is looser. This does not mean D-06 should be dropped; it means either (a) the advisory copy should avoid asserting a precise slot deficit as if it were an exact card count, or (b) the feasibility check should be reworked to account for overlap (e.g., dedupe by unique card, not by role-slot), which is a materially larger undertaking than the naive sum and was not scoped in CONTEXT.md. **This is a planning decision, not a research finding to resolve unilaterally** — flag it explicitly for the planner/discuss-phase.

No corpus-level overlap quantification exists in the Phase 2 artifacts (`grep -i overlap RESEARCH-FINDINGS.md` returns zero hits) — the harness measured each role independently and never cross-tabulated shared cards. Computing an exact overlap percentage would require a new corpus pass; that is out of scope for this research step and is called out below as an open question.

## O-3 — Full Analysis: Does `CutLabCutRoundEngine` have in-pool counts and effective floor in scope for the overshoot order?

**No — neither is currently threaded to the ranking code**, though both already exist nearby and would only need to be passed down, not recomputed from scratch.

### The exact call chain (current state)

```
CutLabPageService.cs:291-302   — resolvedFloors = CutLabFloorDefaults.ResolveDefaults(...)
                                  floorByRole = resolvedFloors.ToDictionary(f => f.Role, f => f.Floor)   <- EFFECTIVE floor, has both bracket-fallback and user-override baked in
CutLabPageService.cs:284-290   — analysisContext = _analysisContextBuilder.BuildAsync(...)               <- analysisContext.RoleCounts is the IN-POOL count (CutLabAnalysisContextBuilder.cs:66)
CutLabPageService.cs:364-368   — CutLabCutRoundEngine.BuildFindingsAndRoundPlan(derivedWorkingList, analysisContext, floorByRole, state.Decisions)
    |
    v
CutLabCutRoundEngine.cs:321-330  BuildFindingsAndRoundPlan(workingList, context, floorByRole, decisions, round3DeltaMagnitudes)
    |  -- floorByRole AND context.RoleCounts are BOTH IN SCOPE HERE (context is a full CutLabAnalysisContext) --
    |
    +--> line 333-342: CutLabStructuralFindings.Compute(..., floorByRole, ...)   <- floorByRole forwarded here, fine
    |
    +--> line 343-348: CutLabCutRoundEngine.BuildQueue(
             BuildInputs(workingList, context.AnalyzedCards),   <- only Roles+Quantity per card carried through, NOT floorByRole, NOT context.RoleCounts
             findings,
             decisions,
             workingList.Sum(card => card.Quantity) - TargetDeckSize,
             round3DeltaMagnitudes)
         ^^^ floorByRole and context.RoleCounts are DROPPED HERE — neither reaches BuildQueue
    |
    v
CutLabCutRoundEngine.cs:184-292  BuildQueue(workingList, findings, decisions, cardsToCutTarget, round3DeltaMagnitudes)
    |
    +--> line 283: BuildLockedOvershootAdvisory(workingList)   <- only IReadOnlyList<CutLabRoundInputCard>, no floor dict, no count dict
    |
    v
CutLabCutRoundEngine.cs:402-436  BuildLockedOvershootAdvisory(IReadOnlyList<CutLabRoundInputCard> workingList)
    |
    +--> line 421-427: rankedCards = lockedCards.Select(card => (RoleKey: AdvisoryRoleFor(card.Roles), ...)).OrderBy(entry => RolePriority(entry.RoleKey))...
    |
    v
CutLabCutRoundEngine.cs:438-439  AdvisoryRoleFor(IReadOnlyList<string> roles)
                                    => roles.OrderBy(RolePriority).FirstOrDefault() ?? "other";
CutLabCutRoundEngine.cs:441-444  RolePriority(string roleKey)
                                    => Array.IndexOf(LockedOvershootRoleOrder, ...) ...   <- PURE static array lookup, zero floor/count awareness
```

**`workingList: IReadOnlyList<CutLabRoundInputCard>` does carry `Roles` and `Quantity` per card** (record at `CutLabCutRoundEngine.cs:17-26`), so an in-pool count *could* be recomputed locally inside `BuildLockedOvershootAdvisory` by replicating the same aggregation `CutLabAnalysisContextBuilder.cs:219-224` already does — but that duplicates logic that already exists once in `context.RoleCounts`. **The effective floor cannot be recovered locally** — `CutLabRoundInputCard` has no floor field at all, so `floorByRole` must be threaded in as a new parameter; there is no way to derive it from `workingList`.

### Every signature/call site D-13 touches

1. **`CutLabCutRoundEngine.BuildFindingsAndRoundPlan`** (`CutLabCutRoundEngine.cs:321-330`) — the internal call to `BuildQueue` at line 343-348 must additionally pass `floorByRole` and either `context.RoleCounts` or `context` itself. The method's own public signature does not need to change (it already receives both).
2. **`CutLabCutRoundEngine.BuildQueue`** (`CutLabCutRoundEngine.cs:184-189`, public) — needs two new parameters (e.g. `IReadOnlyDictionary<string,int> floorByRole`, `IReadOnlyDictionary<string,int> roleCounts`). This is a public-API signature change with **14 direct call sites**, all in test code:
   - `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` — 12 call sites (lines 21, 49, 74, 136, 154, 174, 192, 213, 227-228, 263, 293, 321, 372, 388)
   - `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs:343`
   - No non-test callers exist outside `BuildFindingsAndRoundPlan` — production code only calls `BuildQueue` indirectly through it.
3. **`BuildLockedOvershootAdvisory`** (`CutLabCutRoundEngine.cs:402-403`, private) — needs the same two new parameters (or just enough to compute headroom per role).
4. **`AdvisoryRoleFor`** (`CutLabCutRoundEngine.cs:438-439`, private) — currently picks a card's *display* role by static priority alone. Under D-13 it must pick by headroom among the card's own roles (or the ranking pipeline at lines 421-427 must compute headroom per role and sort the whole `rankedCards` sequence by it, using `AdvisoryRoleFor`/`RolePriority` only as the tiebreak). Signature change required either way.
5. **`RolePriority`** (`CutLabCutRoundEngine.cs:441-444`, private) — unchanged in body; becomes the `.ThenBy(...)` tiebreak instead of the primary `.OrderBy(...)`.
6. **Two existing tests assert the OLD fixed order and must be rewritten, not merely extended:**
   - `BuildQueue_LockedOvershootRanksLeastCriticalRolesThenPrimaryTypes` (`CutLabCutRoundEngineTests.cs:309-360`) — asserts wincons-first, payoffs-second, etc., exactly the order D-13 reconciles.
   - `BuildQueue_LockedOvershootAdvisoryAppearsBeforeQueueIsExhausted` (`CutLabCutRoundEngineTests.cs:363-383`) — does not assert group order directly but does call `BuildQueue` with the current arity and will need updated arguments regardless.
7. **`CutLabUiPatchBuilder`** — confirmed to consume `BuildLockedOvershootAdvisory`'s output through `CutLabDecideApiResponse.Patch.LockedOvershootAdvisory` (`CutLabUiPatchDto.cs:47`, `CutLabDecideApiResponse.cs` references `CutLabLockedOvershootAdvisoryDto`/`CutLabLockedOvershootGroupDto`, `CutLabDecideApiResponse.cs:117-136`). **This is the one CutLab.io surface that IS live-patched via AJAX after every accept/reject decision** (`CutLabApiController.PostDecideAsync`, `CutLabApiController.cs:55`). So D-13's reorder automatically flows through the existing patch plumbing once `BuildQueue`/`BuildLockedOvershootAdvisory` are updated — no separate DTO change is needed for D-13 specifically, but the plan must account for `CutLabApiController.cs:92`'s second `BuildFindingsAndRoundPlan` call (the "before" state snapshot) needing the same new arguments.

### Correcting one detail in CONTEXT.md's citation

CONTEXT.md's canonical-refs section cites the consumer at "line 442" — in this worktree's current file, the `RolePriority` method body is at line 441-444 and `AdvisoryRoleFor` is at 438-439 (both shifted a few lines from whatever snapshot CONTEXT.md was written against, but the same methods, same file). No functional discrepancy — cite `CutLabCutRoundEngine.cs:438-444` for both together going forward.

## A note on the floor table's UI-patch coverage (relevant to D-11, not D-13)

CONTEXT.md's canonical-refs section states the patch builder "must represent the two new columns" for D-11. Tracing this precisely: **the six-column floor table itself is not part of the AJAX patch payload today, and neither is the existing four-column table.** `CutLabUiPatchDto` (`Models/Api/CutLabUiPatchDto.cs:7-69`) and `CutLabDecideApiResponse` (`Models/Api/CutLabDecideApiResponse.cs:7-35`) carry `FloorWarnings` (break-floor warnings for the *next proposed cut*) and `LockedOvershootAdvisory`, but **no per-role floor-row DTO** (no `InPoolCount`, `DefaultValue`, `SourceLabel`, or any analog). `cut-lab.ts` reads `data-cut-lab-floor-*` attributes once at initial render (`cut-lab.ts:860-866,1110-1121`) and only ever *writes* client-computed visual state back (the "at floor" marker, the "Adjusted" badge) — it never receives updated `InPoolCount`/`Bracket`/`Commander` numbers from the server after a decide action. The floor table is rendered fresh only on a full page load (`CutLabViewModel.From` → `BuildFloorRows`, `Models/CutLabViewModel.cs:463-487`).

**Conclusion:** the two new D-11 columns (Bracket, Commander) need `CutLabResolvedFloor` → `CutLabFloorRowView` → `CutLab.cshtml` changes only (Razor full-render path). They do **not** structurally require `CutLabUiPatchBuilder`/`CutLabUiPatchDto` changes *unless* the plan wants the floor table's "In pool" number (which today is also static-per-load) to live-update after an accept/reject — that would be new scope beyond RFLR-08's literal ask and should be raised as a discretion/scope question, not assumed. The genuine patch-builder touch point in this phase is **D-13's `LockedOvershootAdvisory`**, not D-11's table.

## `CutLabFloorDefaults.ResolveDefaults` — exact current shape

`DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs:52-97`:

```csharp
public static IReadOnlyList<CutLabResolvedFloor> ResolveDefaults(
    int? declaredBracket,
    string playExperience,
    double commanderManaValue,
    IReadOnlyList<string> commanderNames,
    IManabaseBaselineProvider? baseline,
    ICedhLandBaselineProvider? cedhBaseline,
    IReadOnlyList<CutLabRoleFloor> priorFloors)
```

Per-role default resolution (line 74-94) currently branches:
- `"lands"` → `ResolveLandsDefault(resolvedBracket, commanderNames, baseline, cedhBaseline)` (private helper, line 184-201) — the exact priority-chain template D-04's new provider should mirror: cEDH lands baseline lookup first (bracket 5 only), then bracket-band baseline, then a constant fallback (`FallbackLands = 36`).
- `"ramp"` → `ManabaseRampDrawBudgetCalculator.CalculateTargetRamp(commanderManaValue)` — **not** a bracket-band lookup; a formula. D-04's "Bracket" column value for ramp is this computed number, not a table row.
- `"draw"` → `24 - rampDefault` — the coupling D-05 breaks. The stale comment is at line 68: `// Mirror ManabaseRampDrawBudgetCalculator's fixed 24-slot split: draw gets whatever ramp does not.`
- everything else (`interaction-targeted`, `interaction-mass`, `protection`, `engines`, `payoffs`, `wincons`) → `GetBracketBand(role, resolvedBracket)` (line 140-166), the `[ASSUMED] ... awaiting product sign-off` table CONTEXT.md's D-04 framing cites.

**`CutLabResolvedFloor` record** (`CutLabFloorDefaults.cs:205-224`) today: `Role`, `Floor` (effective = user override ?? default), `IsUserSet`, `DefaultValue` (pre-override default), `ResolvedBracket`, `BracketWasFallback`. **This record must grow** to carry the bracket value and commander value as two separate fields (e.g. rename `DefaultValue`→`BracketValue`, add `CommanderValue: int?`), with `Floor` becoming `userOverride ?? Math.Max(BracketValue, CommanderValue ?? 0)` per D-04. Every consumer of `DefaultValue` needs auditing: `CutLabViewModel.cs:478,482-483` (`BuildFloorRows`), `CutLab.cshtml:793,816` (`data-cut-lab-floor-default` on both the row and the Reset button).

**New commander-role-floor provider insertion point:** inside the `foreach (string role in CutLabFloorRules.RoleKeys)` loop at line 74, alongside the existing `GetBracketBand`/`landsDefault`/`rampDefault`/`drawDefault` branches — a new lookup against the commander-role-floor provider (keyed by `commanderNames` + `role`) needs to run for the six GO roles (ramp, draw, interaction-targeted, engines, payoffs, wincons) and feed into the `max()` per D-04.

## `CedhLandBaselineProvider` — the exact template to mirror

`DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` (151 lines, read in full):

- **Interface + DI-facing ctor:** `IWebHostEnvironment env, IMemoryCache cache, ILogger<T>? logger = null` → resolves `Path.Combine(env.ContentRootPath, "Data", "<name>", "latest.json")` (lines 40-49).
- **Internal test-seam ctor:** `internal CedhLandBaselineProvider(string dataFilePath, IMemoryCache cache, ILogger? logger = null)` (lines 52-59) — takes an explicit path, defaults logger to `NullLogger.Instance`.
- **Cache:** single `IMemoryCache` entry keyed by a constant string (`"manabase:cedh-land-baseline"`), 24-hour absolute TTL (`_cache.Set(CacheKey, new CacheEntry(snapshot), TimeSpan.FromHours(24))`, line 132). Caches the *miss* too (a `CacheEntry(null)` on load failure), so a corrupt file is not re-read every request for 24h.
- **Fail-open catch set:** `IOException`, `UnauthorizedAccessException`, `JsonException` only (line 124-127) — anything else propagates. Logs once via `Interlocked.Exchange(ref _loadFailureLogged, 1)` guard (lines 136-147).
- **`CandidateKeys`** (lines 98-109): solo name yielded as-is; two-name (partner) lists yield both `"A / B"` and `"B / A"` orders; DFC names (containing `" // "`) are never split because they only ever appear as a single-element `commanderNames` list.
- **Registration:** `builder.Services.AddSingleton<ICedhLandBaselineProvider, CedhLandBaselineProvider>()` (`Program.cs:94`), plus an explicit `EnsureLoaded()` warm-up call at `Program.cs:310` inside startup.

The new commander-role-floor provider should be a near-literal copy of this shape: `Data/role-floor-baseline/latest.json` (per D-08's file location convention), same cache pattern, same fail-open catch set, same `CandidateKeys` reuse (per D-10 — **do not reimplement**, either extract `CandidateKeys` to a shared static helper both providers call, or duplicate the ~10 lines verbatim; either is a legitimate plan choice, but the matching semantics must be byte-identical per D-10's "Record the partner gap explicitly" instruction).

**Snapshot shape today** (`DeckFlow.Web/Data/cedh-land-baseline/latest.json`, 11 KB, read directly): top-level `generated`, `sampleSize`, `overallMeanLands`, `commanders: { "<name>": { n, landsMean, landsSd } }`. The new snapshot's shape per CONTEXT.md `<specifics>` is `{"<name>": { "n": N, "floors": { "<role>": p25 } } }` — flatter (no top-level wrapper shown in the example, but D-09 will likely want at least a `generated` stamp for provenance parity with the lands precedent; this is a plan-level choice, not settled by CONTEXT.md).

## `CedhBaselineCommandRunner` — the CLI generator template for D-09

`DeckFlow.CLI/CedhBaselineCommandRunner.cs` (this worktree's copy has **no drift check** — confirmed by `grep -c Drift` returning zero matches; `main`'s copy has it fully wired). Verified directly via `git show main:DeckFlow.CLI/CedhBaselineCommandRunner.cs`, which additionally has:

- A `--thresholds` path argument loading `CedhDriftThresholds.FromJson(...)` (fails fast if the file is missing — "Drift thresholds file not found").
- A missing-card-name accumulation guard (`missingCardNames`) that **refuses to write any artifact** if any input card name failed to resolve — this is the exact "zero-unresolved" gate pattern RFLR-09 established for the research harness, mirrored here for the generator.
- The drift check itself runs **before any file write**, comparing the freshly built snapshot against the previously committed `latest.json` (if one exists — first-run bootstrap is allowed to skip the check with a printed notice) via `CedhBaselineDriftCheck.Evaluate(previous, candidate, thresholds)` (`DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`, confirmed present on `main`, read in full):
  - `AddDroppedEstablishedCommanders` — a commander with `n >= MinEstablishedN` in the old snapshot that vanishes from the candidate is a hard failure.
  - `AddSampleCollapses` — a commander with `n >= MinPopulousN` whose sample drops by more than `MaxSampleDropPct`.
  - `AddOneSidedDrift` — too many same-direction "movers" (magnitude ≥ `MoverThresholdLands`) is suspicious of a systematic bug rather than real metagame movement.
- All threshold values are `required` (no defaults) — `CedhDriftThresholds.FromJson` throws `JsonException` on any missing field, "because a typo in the config file would otherwise disable the guard silently."

**Since O-2 is closed** (rebase precedes implementation), the plan should treat `CedhBaselineDriftCheck`/`CedhDriftThresholds` as available and write a role-floor-specific analog with its own threshold semantics (e.g. dropped-commander, sample-collapse, one-sided-drift rules adapted to `floors: {role: p25}` instead of `landsMean`). Do not attempt to write these guard primitives from scratch — they exist on `main` after rebase and should be reused/generalized where the shape allows, or mirrored 1:1 where role-floor semantics genuinely differ (e.g. "dropped role within an established commander" is a new rule category the lands guard doesn't need, since lands has no sub-role dimension).

**CLI logic placement per project convention (user memory, confirmed applicable):** "CLI additions → Core + Core.Tests" — `DeckFlow.CLI` logic should live in `DeckFlow.Core` with tests in `DeckFlow.Core.Tests` where feasible. `CedhBaselineCommandRunner` itself is `internal static class` directly in `DeckFlow.CLI` (not Core) doing I/O orchestration (file reads, `Console.WriteLine`), while the *pure* logic (`CedhLandBaseline.Build`, `CedhBaselineDriftCheck.Evaluate`) lives in `DeckFlow.Core.Manabase`. The new D-09 runner should follow the same split: an `internal static class RoleFloorBaselineCommandRunner` (or similar) in `DeckFlow.CLI` for I/O, with the actual snapshot-building/filtering logic (apply `clearsBar && p25 > 0`, `Math.Floor` truncation) as pure static methods in `DeckFlow.Core`, tested in `DeckFlow.Core.Tests`. Note: `DeckFlow.Core.Tests` already references `DeckFlow.CLI` and has `[InternalsVisibleTo("DeckFlow.Core.Tests")]` wired (per project conventions), so even CLI-internal logic is directly testable without extraction if a plan prefers not to split it.

## RESEARCH-FINDINGS.json — exact shape (read directly, not assumed)

Verified via `python3 -m json.tool`:

```json
{
  "methodology": {...},
  "corpusBaseline": {...},
  "commanders": {
    "<Commander Name>": {
      "rawN": 921,
      "n": 874,
      "roles": {
        "<role-key>": {
          "source": "postgres",
          "mean": 26.47,
          "p25": 24,
          "ratio": 1.44,
          "z": 22.86,
          "cohensD": 0.77,
          "clearsBar": true
        },
        ...
      }
    },
    ... (841 commander keys)
  },
  "edhrec": {...},
  "goNoGo": { "lands": ..., "ramp": ..., "draw": ..., "interaction-targeted": ..., "interaction-mass": ..., "protection": ..., "engines": ..., "payoffs": ..., "wincons": ... },
  "rolesInScopeForPhase3": ["lands", "ramp", "draw", "interaction-targeted", "engines", "payoffs", "wincons"],
  "signalPresentRoles": [...],
  "protectionUnderDetection": {...},
  "corpusHygiene": {...},
  "casualBiasObjection": {...},
  "landsCalibration": {...},
  "rampCalibration": {...}
}
```

**Confirmed:** `rolesInScopeForPhase3` still lists `lands` (7 entries) — this is the artifact-not-hand-edited fact CONTEXT.md warns about. `02-08-SUMMARY.md` is the authority that overrides it to 6 roles (ramp, draw, interaction-targeted, engines, payoffs, wincons). **The D-09 generator must read `commanders.*.roles.*` directly, apply the `clearsBar == true` gate per role, apply D-03's `p25 > 0` rule, and explicitly exclude `lands` regardless of what `rolesInScopeForPhase3` says** — do not trust that field as the role allowlist; hardcode the six-role list (or read it from `signalPresentRoles`/`goNoGo` if that field's semantics match — verify at generator-implementation time, since this research pass did not exhaustively diff every field against `02-08-SUMMARY.md`'s six-role verdict).

## Consumer chain — every place the table becomes six columns

1. **`CutLabResolvedFloor`** (`CutLabFloorDefaults.cs:205-224`) — record grows to carry bracket + commander values separately (see above).
2. **`CutLabFloorDefaults.ResolveDefaults`** (`CutLabFloorDefaults.cs:52-97`) — gains the new provider parameter and the `max()` merge logic.
3. **`CutLabPageService.cs:291-302`** — the `ResolveDefaults` call site; needs the new provider resolved from DI and passed through. `floorByRole` built at line 299-302 (`resolvedFloors.ToDictionary(f => f.Role, f => f.Floor)`) is unaffected in shape (still `Role → int`) since `Floor` remains the single effective number consumers like `CutLabFloorRules.Evaluate` need.
4. **`CutLabViewModel.BuildFloorRows`** (`CutLabViewModel.cs:463-487`) — must read the two new fields off `CutLabResolvedFloor` and populate two new fields on `CutLabFloorRowView`.
5. **`CutLabFloorRowView`** (`CutLabViewModel.cs:1050-1075`) — record grows two fields (e.g. `BracketValue`, `CommanderValue: int?`) plus whatever D-12's two-empty-state logic needs to distinguish `n/a` (structurally out of scope) from "no match" (in scope, empty).
6. **`CutLab.cshtml`** table (lines 779-821) — `<thead>` gains two `<th>` (lines 782-786), `<tbody>` loop gains two `<td>` with `data-label` attributes (per the existing `data-label` convention on every cell, D-11 explicitly calls this out).
7. **`cut-lab.ts`** — no live-patch changes needed for the table itself (see the UI-patch note above); only the Reset-button discretion item (`data-cut-lab-floor-default` must read the `max()`, both server-side attribute value and the TS reader at `cut-lab.ts:3948` are unaffected in *code* since the attribute already just holds "the default to reset to" — only its *value* changes semantically once `CutLabFloorRowView.DefaultValue`/replacement carries the max instead of the bracket-only default).
8. **`CutLabUiPatchBuilder`** — no D-11 changes required (see UI-patch note); D-13 changes only, as traced in O-3 above.

## Test surface

- **`CutLabFloorDefaultsTests.cs`** (239 lines, `DeckFlow.Web.Tests`) — existing fake-provider pattern to mirror: `FakeBaselineProvider` (`:200-213`, implements `IManabaseBaselineProvider`) and `FakeCedhBaselineProvider` (`:215-238`, implements `ICedhLandBaselineProvider`, constructor takes an optional `double? mean` and returns `TryGetBaseline` accordingly). RFLR-07's three required paths (commander-hit, fallback, role-not-in-scope) map directly onto a new `FakeRoleFloorBaselineProvider` following this exact shape.
- **`CutLabRoleAssignerTests.cs`** (381 lines, `DeckFlow.Web.Tests`) — already has explicit multi-role-membership tests (`AssignRoles_RhysticStudy_CanHoldDrawAndEngineRoles`, `AssignRoles_PermanentDrawEngine_IsEngine`, `AssignRoles_CanonicalEmissionOrder_PutsTargetedBeforeLaterRoles`) — useful precedent for how the team already tests overlap; no changes needed for this phase, but a plan touching D-06 should be aware this file is where such assertions belong if any new overlap-awareness logic needs coverage.
- **`CutLabCutRoundEngineTests.cs`** (452 lines, `DeckFlow.Web.Tests`) — home of the two overshoot tests D-13 must rewrite (see O-3 above), plus 12 total `BuildQueue` call sites that need the new parameter(s) threaded through once the signature changes.
- **DI registration test surface:** `Program.cs:94` (`AddSingleton<ICedhLandBaselineProvider, ...>`) and `Program.cs:310` (`EnsureLoaded()` warm-up) are the two lines a new provider registration must mirror; no dedicated Program.cs test exists for these registrations today (integration-level, covered implicitly by the app booting in CI).

## Common Pitfalls

### Pitfall 1: Treating `DefaultValue` as a single number after D-04
**What goes wrong:** Code or tests that still read `CutLabResolvedFloor.DefaultValue` as "the" default will silently see only the bracket value (or only the commander value, depending on how the rename lands) after the record splits into two fields.
**Why it happens:** `DefaultValue` is referenced in exactly two other files today (`CutLabViewModel.cs:478,482-483`, `CutLab.cshtml:793,816`) — easy to miss one during the rename.
**How to avoid:** `grep -rn "DefaultValue" DeckFlow.Web` before considering the record change complete; audit both hits above.
**Warning signs:** The Reset button restoring the bracket value instead of the max() after a user override is cleared.

### Pitfall 2: Assuming role-floor sums are additive when planning D-06
**What goes wrong:** Treating the measured 78-vs-63 slot deficit as a precise, actionable number.
**Why it happens:** The number was computed by naively summing per-role floors, and those floors were computed by a classifier that allows role overlap (O-1 above).
**How to avoid:** Either explicitly caveat the advisory copy ("approximate," "assumes disjoint roles") or scope a genuine overlap-aware calculation as part of D-06 — raise this with the user/planner rather than silently picking one.
**Warning signs:** A test asserting an exact slot-deficit number that doesn't match manual card-by-card counting on a real overlapping pool.

### Pitfall 3: Wiring `floorByRole`/`RoleCounts` into `BuildQueue` without updating all 14 test call sites
**What goes wrong:** `dotnet build` succeeds locally against production code but every test in `CutLabCutRoundEngineTests.cs`/`CutLabStructuralFindingsTests.cs` fails to compile.
**Why it happens:** `BuildQueue` is a public static method with a wide test surface (14 call sites across 2 files) that all construct `IReadOnlyList<CutLabRoundInputCard>` inline.
**How to avoid:** Grep for `CutLabCutRoundEngine.BuildQueue(` before starting D-13's implementation task, and budget test-file updates as part of the same task rather than a follow-up.
**Warning signs:** N/A — this is a compile-time failure, caught immediately by `dotnet build`.

### Pitfall 4: Assuming the floor table needs `CutLabUiPatchBuilder` changes for D-11
**What goes wrong:** Time spent adding `BracketValue`/`CommanderValue` to `CutLabUiPatchDto`/`CutLabDecideApiResponse` that nothing reads, because the floor table is never re-rendered from that payload.
**Why it happens:** CONTEXT.md's canonical-refs section states this as a requirement, but tracing the actual data flow shows no floor-row DTO exists in the patch path today (see the dedicated note above).
**How to avoid:** Confirm with the user/planner whether live floor-table updates after accept/reject are new in-scope behavior before adding patch-builder code for D-11 specifically; if not, skip it and reserve patch-builder changes for D-13's `LockedOvershootAdvisory` only.
**Warning signs:** New DTO fields added to `CutLabUiPatchDto` with no corresponding `cut-lab.ts` reader.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The new commander-role-floor snapshot file should live at `Data/role-floor-baseline/latest.json` (mirroring `cedh-land-baseline`'s path convention) | Consumer chain / provider template | Low — this is CONTEXT.md's own stated convention (D-08 "bundled snapshot"), not a research invention; naming is a planner discretion item regardless |
| A2 | `signalPresentRoles`/`goNoGo` fields in `RESEARCH-FINDINGS.json` may or may not exactly match the six-role `02-08-SUMMARY.md` verdict — this research pass read their presence but did not exhaustively diff every value against the six-role list | RESEARCH-FINDINGS.json shape | Medium — if the generator trusts a JSON field instead of hardcoding the six-role allowlist, a stale/mismatched field could silently include `lands` or `interaction-mass`/`protection` |
| A3 | Extracting `CandidateKeys` to a shared helper vs. duplicating it verbatim in the new provider is left as a plan-level choice | `CedhLandBaselineProvider` template section | Low — either satisfies D-10's "byte-identical matching semantics" requirement; a plan that duplicates without keeping the two in sync over time is a latent drift risk, not a Phase 3 defect |

**If this table is empty:** N/A — see above.

## Open Questions

> **Both questions below were RESOLVED during `/gsd-plan-phase 3` on 2026-07-28. Retained verbatim with
> inline resolution markers so the reasoning stays auditable — the findings themselves are unedited.**

1. **Should D-06's aggregate-feasibility check account for role overlap, or ship as a caveated naive sum?**
   - What we know: the naive sum overstates infeasibility because roles overlap (O-1), most severely for draw/engines (strict subset) and any wincons/combo-piece overlap.
   - What's unclear: no corpus measurement exists of how much the naive sum overstates the true deficit — computing it precisely would require a new harness pass cross-tabulating shared cards per commander, which is out of this research phase's scope.
   - Recommendation: raise explicitly in planning/discuss-phase as a scope decision — either (a) ship the naive-sum advisory with caveated copy ("approximate; assumes non-overlapping roles"), or (b) scope a genuine overlap-aware calculation as an explicit, separately-estimated task within D-06.
   - **RESOLVED 2026-07-28 — option (b), bounded.** Recorded as `03-CONTEXT.md` **D-06a**: a structural overlap
     correction, analytic rather than empirical, with no new corpus pass. It authorizes **exactly two**
     corrections — count `max(engines, draw)` in place of `engines + draw` (the proven strict subset), and treat
     `wincons` as free-riding — and no others. Every remaining role, **`payoffs` included**, counts additively:
     the payoffs/engines and payoffs/wincons relationships this section documents are co-occurrence, not the
     proven subset relationship, and no magnitude was measured. Because the correction under-counts demand, the
     advisory copy must state the estimate is conservative rather than exact. Implemented by plan `03-06`.

2. **Exact wrapper shape for the D-09 role-floor snapshot (top-level `generated`/`sampleSize` stamp or bare commander map)?**
   - What we know: the `cedh-land-baseline` precedent has a wrapper (`generated`, `sampleSize`, `overallMeanLands`, `commanders`); CONTEXT.md's `<specifics>` example shows a bare `{"<name>": {...}}` map with no wrapper.
   - What's unclear: whether D-09's generator should add a provenance wrapper for parity with the lands precedent (useful for the drift check's "previous.Generated" field, which the lands drift check reads directly).
   - Recommendation: add a `generated` field at minimum — `CedhBaselineDriftCheck`'s `EmptyPreviousSnapshot` finding references `previous.Generated` in its message; a role-floor-specific drift check will likely want the same provenance hook.
   - **RESOLVED 2026-07-28 — wrapper, mirroring the lands precedent.** `RoleFloorBaselineSnapshot` (plan `03-01`)
     is `{ generated, sampleSize, adoptedPairs, commanders }`, not the bare name-to-floors map shown in
     `03-CONTEXT.md` `<specifics>` — that example illustrates a single row, not the file. `generated` feeds
     `EmptyPreviousSnapshot`'s message and `adoptedPairs` feeds the new `AdoptedPairCollapse` drift rule.

## Environment Availability

Skipped — this phase has no new external dependencies. It reads a bundled JSON file (mirroring an existing pattern), writes via an existing CLI project, and touches only ASP.NET Core/Razor/TypeScript already present in the solution. `.NET 10 SDK`, `Node.js`, and the Windows `dotnet.exe` binary referenced in the constraints are all already required by the existing build and were not independently re-verified in this research pass (no build was run, per the read-only research mandate).

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`), `xunit.runner.visualstudio` 3.1.4 for discovery |
| Config file | none — standard `Microsoft.NET.Sdk` test projects, no custom xunit.runner.json found |
| Quick run command | `dotnet build DeckFlow.sln -c Release` (VSTest is unreliable in WSL per CLAUDE.md — build-clean is the fast local signal) |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` (Windows dotnet binary, no `MTG_DATA_DIR` set) or push-and-watch CI (`.github/workflows`, runs `dotnet test DeckFlow.sln -c Release --no-build`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RFLR-05 | `ResolveDefaults` applies `max(bracket, commander)` per D-04 for the six GO roles | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabFloorDefaultsTests` | ✅ `DeckFlow.Web.Tests/CutLabFloorDefaultsTests.cs` |
| RFLR-06 | Below-bar commander / out-of-scope role produces byte-identical floor to today (fail-open path) | unit | same filter, new `FakeRoleFloorBaselineProvider` returning no match | ✅ (extend existing file) |
| RFLR-07 | Commander-hit, fallback, role-not-in-scope paths all covered | unit | same filter | ✅ (extend existing file) |
| RFLR-08 | `BuildFloorRows` produces correct `n/a` vs empty-marker vs populated cells | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabViewModel` | ⚠️ No dedicated `CutLabViewModelTests.cs` found via `find` — Wave 0 gap, see below |
| D-06 (advisory) | Aggregate floor sum infeasibility warning fires/doesn't fire correctly | unit | new test class or extend `CutLabStructuralFindingsTests.cs` / a new advisory-specific class | ⚠️ No existing test file for an aggregate-feasibility check (feature doesn't exist yet) — Wave 0 gap |
| D-13 | Overshoot advisory ranks by headroom, ties broken by fixed array | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabCutRoundEngineTests` | ✅ `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` (2 tests must be rewritten, not just extended — see O-3) |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln -c Release` (clean build, changed-lines format gate via the pre-commit hook if `core.hooksPath` is configured).
- **Per wave merge:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` full suite, or push-and-watch CI given known WSL VSTest unreliability.
- **Phase gate:** Full suite green before `/gsd:verify-work`.

### Wave 0 Gaps

- [ ] A dedicated `CutLabViewModelTests.cs` (or equivalent) covering `BuildFloorRows`'s D-12 two-empty-state logic — no such file exists today; `find . -iname "CutLabViewModelTests.cs"` returned nothing.
- [ ] A test home for D-06's new aggregate-infeasibility check — does not exist because the feature doesn't exist yet; plan should specify whether this lives in `CutLabStructuralFindingsTests.cs`, a new `CutLabCutRoundEngineTests.cs` section, or a new file, given the "Claude's Discretion" note on whether it's a `CutLabFindingKind` or a panel-level notice.
- [ ] A fake commander-role-floor provider test double (`FakeRoleFloorBaselineProvider` or similar) mirroring `FakeCedhBaselineProvider` at `CutLabFloorDefaultsTests.cs:215-238` — does not exist yet, needed for RFLR-07.
- Framework install: none — xUnit is already fully wired in both test projects.

## Security Domain

`security_enforcement` is absent from `.planning/config.json` (treated as enabled per the default), but this phase has effectively no new attack surface: no new user input, no new HTTP endpoints, no new authentication/authorization boundary, no new cryptography. The one new I/O surface (a bundled JSON file read at startup/first-request, mirroring `CedhLandBaselineProvider`) is a trusted, git-committed artifact generated by an offline CLI, not user-controlled input.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No new auth surface |
| V3 Session Management | No | No new session surface |
| V4 Access Control | No | No new access-controlled resource |
| V5 Input Validation | Marginal | The new provider's `TryGetBaseline`-equivalent lookup takes `commanderNames` already resolved upstream (same trust boundary as `CedhLandBaselineProvider.TryGetBaseline`) — no new validation needed beyond what the existing provider already does (fail-open on missing/corrupt file) |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed/corrupt bundled JSON causing an unhandled exception at startup or first request | Denial of Service | Fail-open catch on `IOException`/`UnauthorizedAccessException`/`JsonException` only, exactly as `CedhLandBaselineProvider.cs:124-130` already does — mirror verbatim, do not widen or narrow the catch set without reason |
| A drift-check bypass allowing a corrupt/mis-generated snapshot to silently overwrite the committed one | Tampering | `CedhBaselineDriftCheck`-style fail-closed evaluation before any file write, as traced above for D-09 |

## Sources

### Primary (HIGH confidence — read directly from the worktree in this session)

- `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs` (full file)
- `DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs` (full file)
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` (lines 40-250)
- `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` (lines 100-215)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` (lines 150-210, `PlanRole` enum)
- `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs` (full file)
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (lines 270-370)
- `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs` (full file)
- `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs` (full file)
- `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` (full file)
- `DeckFlow.Core/Manabase/CedhLandBaseline.cs` (lines 1-100)
- `DeckFlow.CLI/CedhBaselineCommandRunner.cs` (this worktree, no drift check, confirmed via grep)
- `git show main:DeckFlow.CLI/CedhBaselineCommandRunner.cs` (full drift-check-wired version)
- `git show main:DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs` (full file)
- `DeckFlow.Web/Models/CutLabViewModel.cs` (lines 280-490, 1040-1075)
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` (lines 755-825)
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (targeted greps + lines 210-230, 1105-1125, 3935-3960)
- `DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs` (targeted greps + lines 1-250)
- `DeckFlow.Web/Models/Api/CutLabUiPatchDto.cs`, `CutLabDecideApiResponse.cs` (full DTO listings)
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (targeted greps)
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.json` (structure inspected via `python3 -m json.tool`, one full commander record read)
- `DeckFlow.Web/Data/cedh-land-baseline/latest.json` (first 800 bytes read directly)
- `DeckFlow.Web.Tests/CutLabFloorDefaultsTests.cs` (full file)
- `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` (lines 1-30, 309-410)
- `DeckFlow.Web/Program.cs` (lines 80-110, 300-315)
- `.planning/config.json`
- `.github/workflows/*` (grepped for `dotnet test`/`dotnet build`)

### Secondary (MEDIUM confidence)

- None — no external web sources were needed; this research is entirely a codebase archaeology task against a fully-owned repository.

### Tertiary (LOW confidence)

- None.

## Metadata

**Confidence breakdown:**
- O-1 (role exclusivity): HIGH — resolved from the classifier's own docstring, production counting code, and existing regression tests proving concrete overlapping cases.
- O-3 (call-chain scope): HIGH — resolved by tracing every method signature from `CutLabPageService` down to `RolePriority`, with line numbers for every hop.
- Standard stack / templates (`CedhLandBaselineProvider`, `CedhBaselineCommandRunner`, `CedhBaselineDriftCheck`): HIGH — all read in full from the worktree or `main` via `git show`.
- D-06 overlap-quantification precision: LOW — the *existence* of overlap is HIGH confidence; the *magnitude* of how much it loosens the 78-vs-63 arithmetic is unmeasured and flagged as Open Question 1.
- UI-patch-builder scope for D-11 vs D-13: HIGH — resolved by reading every DTO in the patch path and confirming which one wires to `BuildLockedOvershootAdvisory`.

**Research date:** 2026-07-28
**Valid until:** Stable — this is first-party code archaeology against a pinned worktree state, not subject to upstream drift. Re-verify only if the worktree is rebased onto `main` (O-2) before this research is consumed, since line numbers may shift slightly (functional citations should still resolve to the same methods).
