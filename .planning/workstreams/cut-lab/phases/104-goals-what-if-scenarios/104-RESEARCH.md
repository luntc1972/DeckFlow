# Phase 104: Goals & What-If Scenarios - Research

**Researched:** 2026-07-20
**Domain:** ASP.NET 10 / Razor Cut Lab tool extension — client-side (localStorage) scenario persistence, editable simulation-goal display, and non-destructive what-if diffing over an already-built Monte Carlo simulation engine (DeckFlow.Core/Manabase).
**Confidence:** HIGH (all core findings verified by reading the actual Phase 101–103 source; no external library research needed — this phase adds zero new packages)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Goal definition model (GOAL-01)**
- **D-01:** Goals = **editable turn targets on the fixed 103 metric families** (commander, ramp, interaction, engine, payoff). User edits the **turn number** per family; no add/remove of custom categories — this keeps every goal backed by an existing 103 sim metric (SIM-01: "no new simulation math"). Rejected: free custom goal rows (would need new sim math), preset-only (doesn't satisfy "define").
- **D-02 (discretion, planner to firm up):** Seed the editable turn targets from the existing `CedhMulliganCalibration` turn-caps per bracket/play-experience (commander-by-3, engine-by-2, representative-line-by-4, etc. — the same fixed caps 103 used under D-17). User adjusts from that seed. Which exact families are exposed as goals and their per-bracket seed defaults = planner/researcher to enumerate against the 103 metric family list.

**Scenario storage (GOAL-02)**
- **D-03:** Named scenarios persist in browser `localStorage` named slots (save / load / delete / list by name). Per-browser, zero backend — matches the no-accounts constraint. Rejected: named session-JSON download/upload (manual file handling), both (extra surface). File export/import is a possible later enhancement (see Deferred).
- **D-04:** A scenario captures a **full snapshot**: goals + locks + deck intent + the current working-list / cuts-made / baseline state. Reuse the existing `CutLabState` serializer as the snapshot payload so a loaded scenario restores the whole session, not just config.

**What-if swap UX (GOAL-03)**
- **D-05:** The swap is a **non-destructive preview**: pick card A from the working list, card B from the **cuts-made pile / cards not currently in the working list** (the original pool minus the working 100 — all already resolved, so no new Scryfall calls). Show all goal + consistency metric **deltas** instantly via the 103 engine, then **Keep** (commit to the working list) or **Discard** (revert). Rejected: committed-edit swap (no try-before-commit).
- **D-06 (discretion):** Card B via **Scryfall card search** (a brand-new card not in the original pool) is a possible enhancement but adds resolve latency + cache work — planner may defer it; the baseline swap source is the cut pile / non-working original-pool cards.

**Goal results + engine coupling**
- **D-07:** Editable goals are **display-only**. Per-goal pass/miss + probability-vs-target renders in the existing metrics/compare panels; the 103 cut-round ordering and determinism stay unchanged — goals inform the user, they do not re-drive cuts. Rejected: feeding goals back into cut ordering (a meaningful change to the just-shipped 103 engine + its determinism guard/tests; revisit only if UAT shows it's needed).

### Claude's Discretion
- D-02 (goal seed defaults + exact exposed families)
- D-06 (whether card-search is in scope for swap source)
- localStorage slot schema/versioning + quota handling
- exact per-goal results widget (badge vs bar vs %-vs-target)

### Deferred Ideas (OUT OF SCOPE)
- Named session-JSON file export/import for cross-browser scenario portability — localStorage slots ship first (D-03).
- What-if swap card B via Scryfall search (arbitrary card not in the original pool) — baseline uses cut-pile/original-pool cards (D-06).
- Goals feeding back into cut-round ordering — display-only for now; revisit only if UAT shows it's wanted (D-07). Would touch the 103 determinism guard.
- Custom goal categories beyond the 103 metric families — needs new simulation math; out of milestone scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| GOAL-01 | User can define turn-based goals (cast commander by turn 3, ramp by turn 2, interaction by turn 2, engine+payoff by turn 6) | §Architecturally, the 3 existing `CategoryByTurn` metric kinds (`CommanderByTurn`, `EngineByTurn`, `RepresentativeLineByTurn`) are already turn-parameterized against the *same* pre-computed `CardCastability.EarlyCastPercents` arrays. See "D-02 Metric Family Enumeration" below for the exact mapping and the gap between the requirement's plain-language 5 names and the engine's actual 3 turn-indexed kinds. |
| GOAL-02 | User can save and reload named scenarios capturing goals, locks, and deck intent | `CutLabState` + `CutLabStateSerializer` already produce exactly this envelope (Pool, Packages, Decisions, BaselineSnapshot, RoleFloors, Intent). Only a new `Goals` record needs to be added to `CutLabState`. See "Scenario Slot Design" below for the localStorage schema (pattern-matched against `deck-input-store.ts`). |
| GOAL-03 | User can run a what-if swap (replace card A with card B) and immediately see all goal + consistency metrics recalculated using the 103 engine | `CutLabWorkingList.AcceptedCardNames` already identifies the exact "cut pile" (cards excluded from the derived working list) with zero new Scryfall calls. The swap's **Keep** action is a pure composition of two already-existing `CutLabDecisionApplier` operations (`Restore(B)` + `Accept(A)`). The **preview** diff reuses `ICutLabSimulationService.BuildSnapshot` (already public) called twice (before/after) and diffed with the already-public `CutLabMetricDelta.Between` helper — no new simulation math, no new resolve calls. See "Swap Preview Architecture" below. |
| SIM-01 (no new simulation math) | Constrains all of the above | Confirmed compliant for GOAL-01/03 as designed above — every number surfaced already exists in `ManabaseReport`/`CardCastability`; only aggregation/selection code changes. Flagged risk: `PercentByTurn`'s late-turn clamp bug (see Pitfall 1) must be fixed for GOAL-01 to report correct numbers at user-edited turns beyond the current fixed defaults. |
| SIM-02 (before/after baseline compare) | Existing "Compare to baseline" panel | Goals results and swap deltas both slot into the same three-column pattern already rendered for `CutLabViewModel`'s compare section — no new UI framework needed. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **No new NuGet/npm packages without explicit user approval** — this phase needs none; localStorage is a native browser API, and everything server-side reuses existing Cut Lab services.
- **Tech stack pinned**: ASP.NET 10 + Razor, TypeScript → JS via MSBuild (`wwwroot/ts/*.ts` → gitignored `wwwroot/js/*.js`), no framework migration.
- **Render 512MB RAM cap**: any new caching must reuse the existing bounded `IMemoryCache` instances (`CutLabDeltaCache`, `CutLabResolvedCardCache`) or be free (client-side localStorage costs the server nothing).
- **Theme CSS**: layout CSS in `site-common.css`; theme tokens in each theme's `:root`. Do not add layout rules to `site.css`.
- **LF line endings** enforced by `.gitattributes`; `.editorconfig` changed-lines gate applies to new C#/TS lines.
- **Feature flag pattern**: whole Cut Lab tool gated by `tool.cut-lab.enabled` (`[FeatureFlagGate]`), currently OFF in prod. Phase 104 additions stay behind the same flag — no new flag decision found in CONTEXT, so default to reusing the existing gate unless the planner introduces a sub-flag for staged UAT.
- **Testing**: xUnit for `DeckFlow.Web.Tests` / `DeckFlow.Core.Tests`; Vitest (`ts-tests/**/*.test.ts`, jsdom) for TS; Playwright e2e under `DeckFlow.Web/e2e/*.spec.ts`. UI testing must never open a browser on the Windows host (`scripts/run-web-test.sh`).
- **Commits**: plain default-author commits, no Co-Authored-By trailer.

## Summary

Phase 104 adds zero new simulation math and zero new external dependencies. Everything GOAL-01/02/03 needs is either already computed (the Monte Carlo per-turn cast-percent arrays), already serializable (`CutLabState`/`CutLabStateSerializer`), or already composable from two existing primitives (`CutLabDecisionApplier.Restore` + `.Apply(Accept)`, and `ICutLabSimulationService.BuildSnapshot` + `CutLabMetricDelta.Between`). The phase's real work is: (1) deciding exactly which of the 103 metric families become user-editable turn goals given a gap between the CONTEXT's plain-language 5-family list and the engine's actual 3 turn-indexed metric kinds; (2) designing a localStorage scenario-slot schema modeled on the existing `deck-input-store.ts` try/catch persistence pattern, since scenarios are the first Cut Lab feature that is **JS-only with no server-side or no-JS fallback** (a departure from 103's dual-path philosophy); and (3) wiring the what-if swap as a genuinely new (if small) piece of glue code: a preview mode that computes two snapshots without ever calling `CutLabDecisionApplier.Apply` until the user clicks Keep.

**Primary recommendation:** Add turn-editable goals only to the 3 existing `CutLabMetricKind.CategoryByTurn` entries (`CommanderByTurn`, `EngineByTurn`, `RepresentativeLineByTurn`) by threading user-supplied turn overrides into `CutLabSimulationService.BuildMetrics` (replacing the hard-coded `CutLabCategoryByTurnDefaults` reads) — this is the zero-new-metric-kind path and needs no `CutLabMetricsContractTests` changes beyond value assertions. Persist goals as a new `CutLabGoalSettings` record on `CutLabState`. Build the what-if swap as a new controller action pair (`/cut-lab/whatif` no-JS form + `/api/cut-lab/whatif` JSON) that computes `BuildSnapshot(beforeList)` vs `BuildSnapshot(afterList)` (after = A removed, B added via ordinary list surgery, both already in the resolved-card cache) and diffs with `CutLabMetricDelta.Between`; commit ("Keep") by calling `CutLabDecisionApplier.Apply(state, cardB, Restore)` then `.Apply(afterRestore, cardA, Accept, roundKey)`. Persist scenarios as one localStorage index key (`deckflow.cutlab.scenario-index`) plus one key per scenario (`deckflow.cutlab.scenario.<id>`), reusing `CutLabStateSerializer.Serialize`/`Deserialize` verbatim for the payload, with a hard cap (recommend 20 slots) and the same silent-fail try/catch pattern `deck-input-store.ts` already uses for sessionStorage.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Goal turn-target editing (GOAL-01) | Frontend Server (SSR) | Browser/Client | Turn numbers are plain form fields posted like Role Floors (102); server (`CutLabSimulationService`) re-projects metrics against the new turns on every POST. JS only patches the DOM for the async decide-style flow if the planner chooses one; no-JS form POST must work per 103's precedent. |
| Per-goal pass/miss + probability display (GOAL-01) | Frontend Server (SSR) | — | Rendered server-side in the existing `CutLabViewModel`/`CutLab.cshtml` metrics section; pure display, no client compute. |
| Named scenario save/load/delete (GOAL-02) | Browser/Client | — | **No server tier owns this at all** — no accounts, no backend persistence. localStorage is the entire tier. This is the one capability in Cut Lab that cannot degrade to a no-JS path (see Pitfall 2). |
| What-if swap candidate selection UI (GOAL-03) | Browser/Client | Frontend Server (SSR) | Card-A/card-B pickers are DOM interactions (JS enhances, but a no-JS `<select>`-based form must also work, mirroring the Role-Floor / Decision-form precedent). |
| What-if swap delta computation (GOAL-03) | API/Backend | — | `BuildSnapshot` (before/after) + `CutLabMetricDelta.Between` run server-side, identical tier to the existing `/api/cut-lab/decide` endpoint. |
| What-if swap commit (Keep) / revert (Discard) (GOAL-03) | API/Backend | — | Keep mutates `CutLabState.Decisions` via the existing `CutLabDecisionApplier`; Discard is a no-op (preview state is never persisted server-side until Keep is clicked). |
| Resolved-card lookup for swap candidates (GOAL-03) | API/Backend | — | Already-warm `CutLabResolvedCardCache` keyed by pool hash; zero new Scryfall calls (D-05 confirmed). |

## Standard Stack

### Core
No new libraries. This phase is 100% additive C#/TypeScript/Razor code inside the existing DeckFlow.Web project, reusing:

| Component | Location | Purpose | Why reuse (not rebuild) |
|-----------|----------|---------|--------------------------|
| `ICutLabSimulationService.BuildSnapshot` | `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs:19-24` | Already-public 7-family metric projection for any working list | This is the exact function needed for both "before" and "after" swap snapshots — already handles resolve/cache/mode/trials. |
| `CutLabMetricDelta.Between` | `DeckFlow.Web/Models/CutLab/CutLabMetrics.cs:150-183` | Static before/after diff with noise-floor + direction | Already used by `ComputeProposalDeltas`; identical shape needed for swap deltas. |
| `CutLabWorkingList.AcceptedCardNames` / `.Derive` | `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs` | Derives working list / cut-pile from pool + decisions | `Pool - Derive(pool, decisions) == AcceptedCardNames` = exactly the swap-B candidate source per D-05. |
| `CutLabDecisionApplier.Apply` / private `Restore` | `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs` | Pure state-transition function per decision | Keep = `Restore(B)` then `Apply(A, Accept, roundKey)` — literally two calls, no new decision kind needed. |
| `CutLabStateSerializer.Serialize`/`Deserialize` | `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` | JSON round-trip of the whole session, 262,144-byte cap | Exactly the GOAL-02 scenario payload per D-04 — reuse verbatim, no new serializer. |
| `CedhMulliganCalibration` | `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` | Turn-cap constants (3/2/4) + bridge minimums (2/2) | Seeds for the editable goal defaults (D-02). |
| `PercentByTurn` / `MaxPercentByTurn` | `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs:382-399` (private) | Reads `CardCastability.EarlyCastPercents[turn-1]` | Already turn-parameterized; making the turn user-editable is passing a variable instead of a constant — this is the concrete proof SIM-01 is satisfiable with near-zero new code. |
| `deck-input-store.ts` sessionStorage pattern | `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` | Single JSON blob, try/catch guarded, byte-size capped | Direct template for the localStorage scenario-slot read/write functions. |

### Supporting
None — no new NuGet/npm packages.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| localStorage named slots | IndexedDB | IndexedDB scales better for large blobs but is asynchronous and adds real complexity; scenario payloads are capped at 256KB and localStorage's synchronous API matches the existing `deck-input-store.ts` pattern. Rejected — over-engineered for this payload size. |
| Composing existing `Restore`+`Accept` for swap-Keep | New `CutLabDecisionKind.Swapped` enum value | Would require touching `CutLabDecisionApplier`, `CutLabWorkingList`, and every place that switches on `CutLabDecisionKind` (cuts-made list rendering, restore-any logic). Composition of two existing kinds needs zero enum changes. Rejected for the added blast radius. |
| Extending `CutLabMetricKind` with `RampByTurn`/`InteractionByTurn` | Re-labelling the existing 3 turn kinds to cover "ramp"/"interaction"/"payoff" implicitly | See "D-02 Metric Family Enumeration" — this is the actual open decision, not a settled recommendation. Both paths are viable; documented below with a recommended default. |

**Installation:** None required.

**Version verification:** N/A — no new packages.

## Package Legitimacy Audit

Not applicable — this phase introduces zero new external packages (NuGet, npm, or otherwise). Skipped per the audit protocol's scope (only required "whenever this phase installs external packages").

## D-02 Metric Family Enumeration (the phase's central open decision)

The CONTEXT's D-01 names **five** goal families in plain language: *commander, ramp, interaction, engine, payoff*. The actual Phase 103 metric contract (`CutLabMetricKind` in `DeckFlow.Web/Models/CutLab/CutLabMetrics.cs:54-88`, built by `CutLabSimulationService.BuildMetrics`) has exactly **three** turn-indexed (`CutLabMetricFamily.CategoryByTurn`) metrics:

| Kind | Fixed turn constant (source) | Row selection | What it actually measures |
|------|-------------------------------|----------------|----------------------------|
| `CommanderByTurn` | `CedhMulliganCalibration.TurnCapExplosive` = 3 | The commander's own `CardCastability` row | Commander castable-by-turn-3 probability |
| `EngineByTurn` | `CedhMulliganCalibration.TurnCapEngine` = 2 | `deck.Spells` tagged `PlanRole.Engine` (via `PlanRoleClassifier`), best row via `MaxPercentByTurn` | Best Engine-role card castable-by-turn-2 |
| `RepresentativeLineByTurn` | `CedhMulliganCalibration.RepresentativeLineTurnCap` = 4 | `deck.Spells` tagged `PlanRole.Engine \| Payoff \| TutorCombo \| Interaction` (blended), best row via `MaxPercentByTurn` | Best "some meaningful plan card" castable-by-turn-4 — this single metric already blends Payoff + Interaction + TutorCombo + Engine |

There is **no standalone "ramp by turn" metric** and **no standalone "interaction by turn" metric** distinct from `RepresentativeLineByTurn`'s blend:
- `PlanRole` (`DeckFlow.Core/Manabase/ManabaseModels.cs:165-181`) has bits `None/Payoff/Engine/TutorCombo/Interaction` — **no `Ramp` bit**.
- The string-tag "ramp" role used elsewhere in Cut Lab (`CutLabRoleAssigner.cs:17`, used for role-floor/structural purposes) is a **completely separate classification system** from `PlanRole`, and is never consulted by `BuildMetrics`/`PlanRows`.
- `EarlyInteraction` (a real metric, `CutLabMetricFamily.EarlyInteraction`) is a **percentage**, not turn-indexed — it comes from `report.InteractionLens.OnTargetCount/QualifyingCount`, unrelated to `PercentByTurn`.
- `CedhMulliganCalibration.BridgeInteractionMin`/`BridgeDevelopmentMin` are **card-count minimums** (both = 2), not turn caps — they cannot seed a "ramp by turn N" or "interaction by turn N" default.

**Two viable paths — planner must pick one:**

**Option A (recommended default, minimal risk):** Expose editable turn targets on exactly the 3 existing `CategoryByTurn` kinds. Relabel `RepresentativeLineByTurn` in the goals UI as something honest about its blend (e.g., "Engine / payoff / interaction plan by turn N") rather than inventing separate rows the engine doesn't compute. Zero new `CutLabMetricKind` values, zero `CutLabMetricsContractTests` family-count risk, and the CONTEXT's "ramp"/"interaction"/"payoff" language is satisfied only implicitly (folded into the blended representative-line check). Requires: (1) add a `CutLabGoalSettings` record (3 ints) to `CutLabState`; (2) thread those 3 ints into `CutLabSimulationService.BuildSnapshot`/`BuildMetrics` in place of the `CutLabCategoryByTurnDefaults` constants; (3) fix the `PercentByTurn` late-turn clamp bug (Pitfall 1) so user-edited turns beyond the current fixed range read correctly.

**Option B (literal-match, more code, still SIM-01 compliant):** Add two new `CutLabMetricKind` values (`RampByTurn`, `InteractionByTurn`) inside the *existing* `CategoryByTurn` family (family count stays 7, so `CutLabMetricsContractTests.MetricFamilies_MatchTheSevenFamilyContract` is unaffected — only `CutLabMetricKind`'s enum grows, which that test does not lock to a fixed count). Compute them the same way `EngineByTurn` is computed (`MaxPercentByTurn` over selected rows) but select rows using the **existing** `CutLabRoleAssigner`-style "ramp" string-tag and the existing `PlanRole.Interaction` bit respectively — both already available in the Web layer (note `CurveCongestionValue` already independently calls `CutLabRoleAssigner.AssignRoles`/`CutLabStructuralFindings.Compute` inside `CutLabSimulationService`, so cross-referencing role tags there is an established pattern, not a new one). This reuses the exact same `CardCastability.EarlyCastPercents` Monte Carlo output — no new randomness — but does add real aggregation code and two new UI rows.

**Recommendation:** Ship Option A first (satisfies GOAL-01's literal requirement — "user can define turn-based goals" — with the smallest, lowest-risk diff), and treat Option B as a fast-follow if UAT feedback says users expect a literal "ramp" or standalone "interaction" turn goal. This mapping ambiguity should be confirmed with the user before planning locks it — see Assumptions Log.

### Seed defaults (once families are chosen)
Regardless of A or B, seed values come straight from `CedhMulliganCalibration`: `TurnCapExplosive=3` (commander), `TurnCapEngine=2` (engine, and — if Option B — ramp defaults to the same turn-2 pacing target since no separate ramp constant exists), `RepresentativeLineTurnCap=4` (representative line / payoff+interaction blend). There is currently only one `ManabaseMode` branch relevant to Cut Lab (`Cedh` vs. casual, resolved via `CutLabRoleAssigner.ResolveMode(playExperience)`); `CedhMulliganCalibration.GetRepresentativeLineTurnCap(mode)` already returns `int.MaxValue` for casual mode, meaning **casual-mode goals should probably not surface a representative-line turn goal at all** (or should surface an "uncapped" state) — flag this for planner UI-copy handling.

## Architecture Patterns

### System Architecture Diagram

```
GOAL-01 (goals editor, SSR + optional JS patch)
──────────────────────────────────────────────
Browser: <input type=number> turn goals in CutLab.cshtml form
   │ (form POST, same pattern as Role Floors)
   ▼
CutLabController.Process/Decide  ──▶  CutLabState.Goals (new record)
   │
   ▼
CutLabSimulationService.BuildSnapshot(workingList, goals.CommanderByTurn, ...)
   │  (PercentByTurn/MaxPercentByTurn now read goals.* instead of CutLabCategoryByTurnDefaults.*)
   ▼
CutLabMetricSnapshot  ──▶  CutLabViewModel  ──▶  existing metrics/compare panels (pass/miss badge added)


GOAL-02 (named scenarios, Browser-only — no server tier)
──────────────────────────────────────────────
Browser: "Save scenario" button
   │ reads current CutLabStateJson hidden input (already built by cut-lab.ts's
   │ buildCutLabStateJson / writeStateToHiddenInput)
   ▼
localStorage.setItem('deckflow.cutlab.scenario.<id>', stateJson)
localStorage.setItem('deckflow.cutlab.scenario-index', '[{id,name,savedAt}, ...]')

Browser: "Load scenario"
   │ localStorage.getItem(...) → writes into the CutLabStateJson hidden input
   ▼
form.requestSubmit()  (ordinary /cut-lab POST — server re-derives everything from state)


GOAL-03 (what-if swap, non-destructive preview)
──────────────────────────────────────────────
Browser: pick card A (working list) + card B (cut pile, via
   CutLabWorkingList.AcceptedCardNames-equivalent list already rendered server-side)
   │ fetch POST /api/cut-lab/whatif  { cutLabStateJson, cardOut: A, cardIn: B }
   │ (no-JS fallback: form POST /cut-lab/whatif, full page re-render)
   ▼
CutLabApiController (new action)
   before = BuildSnapshot(currentWorkingList)
   afterList = currentWorkingList - A + B (plain list surgery; B already resolved
               in CutLabResolvedCardCache from original pool intake)
   after  = BuildSnapshot(afterList)
   deltas = before.Metrics.Select(m => CutLabMetricDelta.Between(m, afterMetrics[m.Kind]))
   ▼
Response: { deltas, floorWarnings, cardA, cardB }  ── state NOT mutated ──
   │
   ▼
Browser renders Keep / Discard
   Keep  → POST existing decide-style endpoint: Restore(B) then Accept(A, roundKey)
   Discard → no-op, discard the preview payload client-side
```

### Recommended Project Structure
No new folders. Extend existing files:
```
DeckFlow.Web/Models/CutLab/
├── CutLabGoals.cs            # NEW: CutLabGoalSettings record + CutLabGoalResult (pass/miss + probability)
├── CutLabMetrics.cs           # UNCHANGED (Option A) or + 2 CutLabMetricKind values (Option B)
├── CutLabState.cs             # + Goals property
└── ...

DeckFlow.Web/Services/CutLab/
├── CutLabSimulationService.cs # BuildMetrics/BuildSnapshot thread goal turns instead of fixed consts
│                               # + fix PercentByTurn late-turn clamp (Pitfall 1)
│                               # + new ComputeSwapDeltas-style method OR compose BuildSnapshot x2 in controller
├── CutLabWorkingList.cs        # UNCHANGED — AcceptedCardNames already gives the swap-B source
├── CutLabDecisionApplier.cs    # UNCHANGED — Keep = 2 existing calls composed by the controller
└── ...

DeckFlow.Web/Controllers/
├── CutLabController.cs        # + Whatif no-JS action (mirrors Decide)
└── Api/CutLabApiController.cs # + PostWhatifAsync (preview) — mirrors PostDecideAsync structure

DeckFlow.Web/wwwroot/ts/
├── cut-lab.ts                  # + swap preview fetch/patch (mirrors decide-form pattern)
└── cut-lab-scenarios.ts        # NEW (or a section inside cut-lab.ts): localStorage slot read/write,
                                 #     modeled directly on deck-input-store.ts's try/catch pattern

DeckFlow.Web/Views/Deck/CutLab.cshtml   # + Goals section, + Scenarios panel, + Swap preview section
```

### Pattern 1: Editable turn goal threaded through existing per-turn read
**What:** Replace `PercentByTurn(commander, CutLabCategoryByTurnDefaults.CommanderByTurn)` with `PercentByTurn(commander, goals.CommanderByTurn)`.
**When to use:** Any of the 3 turn-indexed metrics once goals are user-editable.
**Example (existing code, for reference):**
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs:284-286 (current, fixed defaults)
Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, "Commander by turn 3",
    PercentByTurn(commander, CutLabCategoryByTurnDefaults.CommanderByTurn), CutLabMetricUnit.Percent),
Metric(CutLabMetricKind.EngineByTurn, CutLabMetricFamily.CategoryByTurn, "Engine by turn 2",
    MaxPercentByTurn(engineRows, CutLabCategoryByTurnDefaults.EngineByTurn), CutLabMetricUnit.Percent),
```
Becomes (illustrative — planner designs exact signature):
```csharp
Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, $"Commander by turn {goals.CommanderByTurn}",
    PercentByTurn(commander, goals.CommanderByTurn), CutLabMetricUnit.Percent),
```

### Pattern 2: Swap-B candidate source = accepted (cut) pool cards
**What:** The what-if swap's card-B picker is exactly the pool minus the derived working list.
**When to use:** Populating the swap-B dropdown/select, server- or client-side.
**Example:**
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs:29-37 (existing)
IReadOnlySet<string> cutPileCardNames = CutLabWorkingList.AcceptedCardNames(state.Decisions);
IReadOnlyList<CutLabPoolCard> swapCandidatesB = state.Pool
    .Where(card => cutPileCardNames.Contains(card.Name))
    .ToArray();
```

### Pattern 3: Swap commit as decision composition (no new decision kind)
**What:** "Keep" a swap = restore B, then accept A, in that order, using existing pure functions.
**Example:**
```csharp
// Source: composition of DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs (existing methods)
CutLabState afterRestore = CutLabDecisionApplier.Apply(state, cardB, CutLabDecideAction.Restore, roundKey: string.Empty);
CutLabState afterSwap = CutLabDecisionApplier.Apply(afterRestore, cardA, CutLabDecideAction.Accept, roundKey: "whatif-swap");
```
Note: `Apply(..., Restore, ...)` ignores its `roundKey` parameter entirely for the Restore branch (see `CutLabDecisionApplier.cs:21-24` — `Restore(state, cardName)` takes no round key), so passing `string.Empty` is safe. `Accept` on card A requires A is not `IsLocked` (existing guard, `CutLabDecisionApplier.cs:26-30`) — the swap UI must exclude locked cards from the "A" (card to remove) picker, mirroring the existing cut-round proposal exclusion.

### Pattern 4: Scenario save/load via localStorage index + per-slot key (modeled on deck-input-store.ts)
**What:** One index key lists `{id, name, savedAt}`; one key per scenario holds the raw `CutLabStateSerializer` JSON.
**Example:**
```typescript
// Pattern source: DeckFlow.Web/wwwroot/ts/deck-input-store.ts:27-47 (try/catch silent-fail precedent)
const SCENARIO_INDEX_KEY = 'deckflow.cutlab.scenario-index';
const SCENARIO_SLOT_PREFIX = 'deckflow.cutlab.scenario.';
const MAX_SCENARIO_SLOTS = 20;

interface ScenarioIndexEntry { id: string; name: string; savedAt: string; }

const saveScenario = (name: string, stateJson: string): 'ok' | 'quota-exceeded' | 'disabled' => {
  try {
    const index: ScenarioIndexEntry[] = JSON.parse(window.localStorage.getItem(SCENARIO_INDEX_KEY) ?? '[]');
    const id = crypto.randomUUID();
    if (index.length >= MAX_SCENARIO_SLOTS) {
      return 'quota-exceeded'; // UI: ask user to delete one first
    }
    window.localStorage.setItem(SCENARIO_SLOT_PREFIX + id, stateJson);
    index.push({ id, name, savedAt: new Date().toISOString() });
    window.localStorage.setItem(SCENARIO_INDEX_KEY, JSON.stringify(index));
    return 'ok';
  } catch (error) {
    // Why: localStorage may be disabled (private mode) or over quota; match
    // deck-input-store.ts's silent-fail-with-user-facing-message convention
    // rather than throwing into the click handler.
    return error instanceof DOMException && (error.name === 'QuotaExceededError' || error.code === 22)
      ? 'quota-exceeded'
      : 'disabled';
  }
};
```

### Anti-Patterns to Avoid
- **Building a new `CutLabDecisionKind.Swapped` enum value.** Every consumer of `CutLabDecisionKind` (`CutLabWorkingList.Derive`, `.LatestDecisionsByCard`, the cuts-made rendering in `cut-lab.ts`, `CutLabApiController.BuildCutsMade`) would need updates. The Restore+Accept composition needs none of that.
- **Re-resolving card B via Scryfall for the swap preview.** D-05 explicitly forbids new Scryfall calls; B is always a `state.Pool` entry already warm in `CutLabResolvedCardCache` (or trivially re-resolvable from the existing pool-key cache, same as `CutLabApiController.TryBuildAfterPreResolvedCards` already does for post-decision recompute).
- **Persisting what-if preview state server-side before Keep.** D-05 requires "non-destructive" — computing `before`/`after` snapshots must never call `CutLabDecisionApplier.Apply` until the explicit Keep action.
- **Assuming a no-JS fallback exists for scenario save/load.** Unlike every other Cut Lab feature, there is no server session to fall back to — localStorage IS the persistence tier. See Pitfall 2.
- **Reading `EarlyCastPercents[turn-1]` for turns beyond the array's bounds without a guard.** See Pitfall 1 — this silently under-reports rather than crashing, so it will not surface in manual testing unless specifically checked.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Swap delta computation | A new "diff two decklists" simulation path | `BuildSnapshot` called twice + `CutLabMetricDelta.Between` | Identical math already exists and is used by `ComputeProposalDeltas`; a bespoke path would duplicate noise-floor/direction logic and risk drifting from the single source of truth. |
| Swap commit / cuts-made bookkeeping | New decision-log entry type | Compose existing `Restore` + `Accept` | The decision log, restore-any UI, and cuts-made list already handle exactly this shape of change. |
| Scenario payload format | A bespoke goals+locks+intent JSON shape | `CutLabStateSerializer.Serialize`/`Deserialize` verbatim | Already handles size capping (262,144 bytes), decision/package bounding, and floor clamping on load — reinventing it risks divergent validation rules between "normal" state and "scenario" state. |
| localStorage error handling | Unwrapped `localStorage.setItem` calls | The try/catch + graceful-degrade pattern from `deck-input-store.ts` | Private browsing modes and full quotas throw; the existing codebase already has a working, tested convention for this exact class of failure. |

**Key insight:** This phase's biggest hand-rolling risk is *not* simulation math (that's already fully reused) — it's re-inventing state-transition plumbing (a new decision kind, a new serializer, a new diff algorithm) that the 101–103 phases already built generically enough to compose.

## Common Pitfalls

### Pitfall 1: `PercentByTurn`'s late-turn clamp silently under-reports for user-edited turns beyond the current fixed defaults
**What goes wrong:** `PercentByTurn(row, turn)` (`CutLabSimulationService.cs:385-399`) clamps its index to `row.EarlyCastPercents.Count - 1` whenever `turn - 1` exceeds that bound. `EarlyCastPercents` is documented (`ManabaseModels.cs:265-268`) to run only "ending at `OnCurveTurn` - 1" — i.e., it stops **before** the card's natural cast turn. Today's fixed goal turns (3, 2, 4) happen to sit inside or near that early window for most decks, so the bug has never been exercised. Once GOAL-01 lets a user type e.g. "engine by turn 6," the function will clamp to the last early-turn value instead of returning the (higher) true on-curve/eventual `CastPercent`.
**Why it happens:** `EarlyCastPercents` was built (Phase 103) purely to serve the 3 fixed defaults, not an open turn range.
**How to avoid:** Add a branch: if `turn >= row.OnCurveTurn`, return `row.CastPercent` (mirroring the `EarlyCastPercents.Count == 0` branch's existing logic) instead of falling through to the clamp.
**Warning signs:** A user sets a goal turn noticeably later than the fixed defaults and the reported probability looks suspiciously flat/low compared to the "Commander on time" or "Keepable hand" headline metrics for the same deck.

### Pitfall 2: Scenario save/load has no no-JS story — unlike every other Cut Lab feature
**What goes wrong:** 101–103 all followed "server render + JS patch, no-JS form POST fallback stays in sync." GOAL-02 cannot follow that pattern because localStorage is a client-only API with no server-side equivalent (no accounts, per the milestone's explicit constraint). A planner used to the existing dual-path convention may reflexively try to build a form-POST fallback for "Save scenario" and discover there is nothing on the server to POST to.
**Why it happens:** The rest of Cut Lab treats JS as an enhancement over a fully-functional server flow; scenarios invert that — JS **is** the feature.
**How to avoid:** Explicitly design the Scenarios panel to require JS. Wrap it in `<noscript>` messaging ("Named scenarios require JavaScript") or render it visually present-but-disabled with an explanatory note when JS is unavailable, rather than attempting a server round-trip that has nowhere to persist to.
**Warning signs:** A plan task says "Scenario save form POST fallback" — that's the tell this pitfall wasn't caught.

### Pitfall 3: `CutLabMetricsContractTests` locks the 7-family count, not the `CutLabMetricKind` count
**What goes wrong:** `MetricFamilies_MatchTheSevenFamilyContract` (`DeckFlow.Web.Tests/CutLabMetricsContractTests.cs:11-27`) asserts `Enum.GetValues<CutLabMetricFamily>().Length == 7` with an exact ordered list. It does **not** assert a fixed count for `CutLabMetricKind`. If Option B (new `RampByTurn`/`InteractionByTurn` kinds) is chosen, this specific test will still pass — but other tests (`CutLabEngineDeterminismTests`, `CutLabSimulationServiceTests`) may assert exact metric counts/labels for the returned `CutLabMetricSnapshot.Metrics` list and will need updating.
**Why it happens:** The contract test protects family *shape*, not kind *cardinality* — easy to assume it's a hard gate against adding metrics when it isn't.
**How to avoid:** Grep all Cut Lab test files for `.Metrics.Count` / hard-coded metric counts before deciding between Option A and B, and update whichever ones assume exactly 11 metric values.
**Warning signs:** Adding a metric kind and having an unrelated-looking test fail with a count mismatch.

### Pitfall 4: Swap-A must exclude locked cards, mirroring the existing proposal-exclusion rule
**What goes wrong:** `CutLabDecisionApplier.Apply` for `Accept` silently no-ops back to `EnforceCommanderLock(state)` when the target card `IsLocked` (`CutLabDecisionApplier.cs:26-30`) rather than throwing — so a swap "Keep" on a locked card A will appear to succeed (200 OK) but the state won't actually change, confusing the user.
**Why it happens:** The lock guard was designed for the cut-round proposal flow, which already filters locked cards out of the queue before they're ever proposed; the swap UI is a new entry point that must reproduce that same filter.
**How to avoid:** Filter the card-A picker to exclude `IsLocked` pool cards client- and server-side (validate again in the `/api/cut-lab/whatif` action, same defense-in-depth pattern the decide endpoint already uses).
**Warning signs:** E2e test picks a locked card as A, clicks Keep, and the response is 200 but the working list is unchanged.

### Pitfall 5: `RepresentativeLineByTurn` is uncapped (`int.MaxValue`) in casual mode
**What goes wrong:** `CedhMulliganCalibration.GetRepresentativeLineTurnCap(mode)` returns `int.MaxValue` for non-cEDH play experience. If the goals UI naively seeds every play-experience with the cEDH default of 4, casual-mode users get a goal turn that doesn't match the engine's own notion of "uncapped" for that mode, and the "representative line" metric label ("by turn 4") is misleading for a deck that was never held to that pacing.
**Why it happens:** 103 only ever consumed this via the fixed constant path; goal-seeding is the first place that has to branch on mode explicitly.
**How to avoid:** Seed goal defaults per `ManabaseMode` (resolved via `CutLabRoleAssigner.ResolveMode(playExperience)`), not as a single global constant, and consider hiding/relabeling the representative-line goal entirely for casual mode.
**Warning signs:** Casual-mode Cut Lab sessions show a "representative line by turn 4" goal that never seems to relate to anything in the casual-mode metric numbers.

## Code Examples

### Deriving the swap-B candidate list (existing helper, zero new code)
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs:29-37
public static IReadOnlySet<string> AcceptedCardNames(IReadOnlyList<CutLabDecision> decisions) =>
    LatestDecisionsByCard(decisions)
        .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
        .Select(entry => entry.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
```

### Building a before/after snapshot pair for the swap preview (composed from existing public API)
```csharp
// Illustrative composition — BuildSnapshot signature source:
// DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs:19-24 (already public)
CutLabMetricSnapshot before = await _simulationService.BuildSnapshot(currentWorkingList, playExperience, poolKey: beforeKey, cancellationToken: ct);
IReadOnlyList<CutLabPoolCard> afterList = currentWorkingList
    .Where(c => !string.Equals(c.Name, cardA, StringComparison.OrdinalIgnoreCase))
    .Append(cutPileCardB)
    .ToArray();
CutLabMetricSnapshot after = await _simulationService.BuildSnapshot(afterList, playExperience, poolKey: null, cancellationToken: ct);
IReadOnlyDictionary<CutLabMetricKind, CutLabMetricValue> afterByKind = after.Metrics.ToDictionary(m => m.Kind);
IReadOnlyList<CutLabMetricDelta> deltas = before.Metrics
    .Select(m => afterByKind.TryGetValue(m.Kind, out var a) ? CutLabMetricDelta.Between(m, a) : null)
    .Where(d => d is not null).Cast<CutLabMetricDelta>().ToArray();
```

### deck-input-store.ts's try/catch persistence convention (direct template for scenario slots)
```typescript
// Source: DeckFlow.Web/wwwroot/ts/deck-input-store.ts:35-47
const setLastDeck = (state: LastDeckState): void => {
  try {
    window.sessionStorage.setItem(LAST_DECK_KEY, JSON.stringify(state));
  } catch {
    // sessionStorage may be disabled or quota-limited; skip persistence silently.
  }
};
```

## State of the Art

| Old Approach (Phase 103) | New Approach (Phase 104) | When Changed | Impact |
|---------------------------|----------------------------|---------------|--------|
| Category-by-turn checkpoints fixed at `CedhMulliganCalibration` constants (D-17 addendum) | Same 3 checkpoints, user-editable turn number, same underlying `PercentByTurn` read | Phase 104 | Requires threading a value instead of a constant into `BuildMetrics`; requires fixing the late-turn clamp (Pitfall 1) since the editable range now exceeds what 103 ever exercised. |
| No persistence beyond one active session's `CutLabState` (a hidden form field round-tripped every POST) | Named, listable, deletable scenario copies of that same `CutLabState`, stored client-side | Phase 104 | First Cut Lab feature with no server-side or no-JS equivalent (Pitfall 2). |
| Decisions only ever move a card from working-list → cut-pile (Accept/Reject/Defer) or cut-pile → working-list (Restore, one at a time) | Same primitives, composed: a "swap" is Restore(B) + Accept(A) presented as one user action with a preview step first | Phase 104 | No new decision kind; UI needs a "preview, don't persist yet" state that the underlying decision log has never needed before (deltas today are always for the *next* proposed decision, computed then immediately actionable — the swap preview must compute deltas for a decision pair that may never be committed). |

**Deprecated/outdated:** Nothing in this phase deprecates prior Cut Lab work — it is purely additive on top of 101–103.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Option A (goals limited to the 3 existing `CategoryByTurn` kinds) is the correct interpretation of D-01's "commander, ramp, interaction, engine, payoff" family list, with "ramp"/"interaction"/"payoff" folded implicitly into the blended `RepresentativeLineByTurn` metric. | D-02 Metric Family Enumeration | If the user actually wants literal, separately-tracked ramp and interaction turn goals (Option B), the plan under-delivers against the CONTEXT's plain-language example and needs a follow-up phase/quick-task to add the 2 new metric kinds. Low-medium risk: this is exactly the ambiguity D-02 flagged as "discretion," so surfacing it explicitly (rather than silently picking A) is the safer move — recommend the planner confirm with the user before locking. |
| A2 | Reusing `tool.cut-lab.enabled` as the single feature flag for goals/scenarios/swap (no new sub-flag) is acceptable, since CONTEXT does not mention a new flag decision. | Project Constraints | If the user wants staged rollout (e.g., ship goals before swap), a sub-flag would be needed; low risk since the whole tool is already OFF in prod pending 104/105 completion. |
| A3 | A 20-slot cap for named scenarios is a reasonable default (not specified in CONTEXT). | Standard Stack / Pattern 4 | Low risk — purely a UX/quota-safety number; easy to change without touching architecture. |
| A4 | Casual-mode goal defaults should differ from cEDH defaults (hide/relabel the representative-line goal) per `CedhMulliganCalibration.GetRepresentativeLineTurnCap`'s `int.MaxValue` casual behavior. | Pitfall 5 | Medium risk if wrong — could ship a nonsensical "by turn 4" goal for casual-mode users; needs explicit confirmation of desired casual-mode goal UX since CONTEXT doesn't address play-experience branching for goals. |

**If this table is empty:** N/A — see entries above; all are flagged for planner/user confirmation before being treated as locked.

## Open Questions

1. **Does the planner pick Option A or Option B for D-02?**
   - What we know: Both are SIM-01 compliant and technically feasible; Option A is lower-risk/lower-effort.
   - What's unclear: Whether the user's mental model (from the CONTEXT's literal "ramp/interaction/payoff" wording) requires separately-tracked metrics, or whether the existing blended `RepresentativeLineByTurn` satisfies "define a goal for ramp/interaction/payoff" well enough in the UI copy.
   - Recommendation: Default to Option A in the plan, but have the plan-checker/user explicitly sign off on the family list before execution, since this determines the shape of `CutLabGoalSettings` and the goals-editor UI.

2. **What exact round key/label does a committed swap use in the Cuts-made list?**
   - What we know: `CutLabCutRoundEngine.LabelFor` falls back to the raw key string for unknown keys (`CutLabCutRoundEngine.cs:97-106`); Round1/2/3 and the two second-pass keys are the only labeled ones today.
   - What's unclear: Whether the planner wants a new registered round key (e.g., `"whatif-swap"` with a `LabelFor` case added, e.g. "What-if swap") or wants swap-committed cuts to inherit whatever round the card would have naturally landed in (via `CutLabDecisionApplier.LatestRoundForCard`).
   - Recommendation: Register a dedicated round key + label ("What-if swap") for clarity in the Cuts-made list, since it's a materially different user action from the guided cut-round flow.

3. **Does the what-if swap need a dedicated `/api/cut-lab/whatif` (preview) + separate commit call, or can commit reuse `/api/cut-lab/decide` with two sequential requests?**
   - What we know: The preview must not persist anything server-side; the commit needs two `CutLabDecisionApplier` operations applied together (not as two round-trips, to avoid a half-applied state if the second call fails).
   - What's unclear: Whether the planner wants one atomic "commit swap" endpoint (apply both decisions in one request/response, as Pattern 3 shows) versus reusing the existing `/api/cut-lab/decide` endpoint twice from the client (simpler server code, but risks a partial-apply if the second call fails and the client doesn't roll back the first).
   - Recommendation: One atomic endpoint (new `PostWhatifCommitAsync` action) that applies Restore+Accept together and returns a single updated state — avoids the partial-apply failure mode entirely.

## Environment Availability

Skipped — this phase has no new external dependencies (no new packages, no new external services/tools). All work happens inside the already-running DeckFlow.Web project against already-integrated services (Scryfall resolver, Manabase engine) that Phases 101–103 already proved available.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Web.Tests`), Vitest (jsdom, `DeckFlow.Web/ts-tests/**/*.test.ts`), Playwright (`DeckFlow.Web/e2e/*.spec.ts`) |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; `DeckFlow.Web/vitest.config.ts`; Playwright config alongside `e2e/` |
| Quick run command | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` ; `npx vitest run ts-tests/cut-lab*` |
| Full suite command | `dotnet build` (clean, per project convention) + `dotnet test` both test projects + `npx vitest run` + `npx --no-install playwright test e2e/cut-lab*.spec.ts` (via `scripts/run-web-test.sh`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|---------------------|--------------|
| GOAL-01 | Editable turn goal changes the reported per-turn probability for the corresponding metric | unit | `dotnet test --filter FullyQualifiedName~CutLabSimulationServiceTests` | ✅ existing file, ❌ new test cases needed (Wave 0) |
| GOAL-01 | `PercentByTurn` returns `CastPercent` (not clamped early value) when goal turn ≥ `OnCurveTurn` | unit | same file/regression test for Pitfall 1 | ❌ Wave 0 |
| GOAL-01 | Goal turn persists across a page POST round-trip (`CutLabState.Goals`) | unit | `dotnet test --filter FullyQualifiedName~CutLabStateSerializerTests` | ✅ existing file, ❌ new cases needed |
| GOAL-02 | Save/load/delete a named scenario round-trips the full `CutLabState` via localStorage | unit (Vitest, jsdom mocks `localStorage`) | `npx vitest run ts-tests/cut-lab-scenarios.test.ts` | ❌ Wave 0 (new test file) |
| GOAL-02 | Scenario slot cap (20) blocks a 21st save with a clear message; quota-exceeded is handled gracefully | unit (Vitest) | same file | ❌ Wave 0 |
| GOAL-02 | Saved scenario is reachable end-to-end: save → reload page → load → pool/locks/intent restored | e2e | `playwright test e2e/cut-lab-scenarios.spec.ts` | ❌ Wave 0 (new spec) |
| GOAL-03 | Swap preview computes correct before/after deltas without mutating server state (Discard leaves state unchanged) | unit | `dotnet test --filter FullyQualifiedName~CutLabWhatifTests` (new) | ❌ Wave 0 |
| GOAL-03 | Swap-B candidates are exactly `Pool - Derive(pool, decisions)` (the accepted/cut-pile set) | unit | same file, or extend `CutLabWorkingListTests` | ✅ existing file, ❌ new cases needed |
| GOAL-03 | Keep commits both decisions atomically (card A now cut, card B now in working list, cuts-made list shows the swap) | integration | `dotnet test --filter FullyQualifiedName~CutLabApiControllerTests` (extend) | ✅ existing file, ❌ new cases needed |
| GOAL-03 | Locked card A is excluded from the swap-A picker / rejected server-side if attempted (Pitfall 4) | unit + e2e | extend `CutLabApiControllerTests` + `cut-lab-structure.spec.ts` | ✅/❌ mixed |
| SIM-01 | No new `Random`/simulation-trial code paths introduced by goals/swap (contract guard) | unit | extend `CutLabMetricsContractTests` / `CutLabEngineDeterminismTests` with a case asserting swap deltas are deterministic for a fixed pool/turn/trials combination | ✅ existing files, ❌ new cases needed |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLab` + `npx vitest run ts-tests/cut-lab*`
- **Per wave merge:** Full suite (`dotnet build` clean + both xUnit projects + Vitest + Playwright `cut-lab*` specs)
- **Phase gate:** Full suite green (including `cut-lab-scenarios.spec.ts` and `cut-lab-whatif.spec.ts` if added) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web/ts-tests/cut-lab-scenarios.test.ts` — covers GOAL-02 (new file; jsdom `localStorage` mock already available via Vitest's jsdom environment)
- [ ] `DeckFlow.Web.Tests/CutLabWhatifTests.cs` (or extend `CutLabApiControllerTests.cs`) — covers GOAL-03 preview + commit
- [ ] `DeckFlow.Web/e2e/cut-lab-scenarios.spec.ts` — end-to-end save/reload/load round trip
- [ ] `DeckFlow.Web/e2e/cut-lab-whatif.spec.ts` (or extend `cut-lab-structure.spec.ts`) — swap pick/preview/keep/discard flow, no-JS fallback path
- [ ] Regression test for Pitfall 1 (`PercentByTurn` late-turn clamp) added to `CutLabSimulationServiceTests.cs`
- [ ] Framework install: none — all frameworks already present.

## Security Domain

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | No accounts in this milestone; scenarios are per-browser localStorage, not per-user. |
| V3 Session Management | No | No new session state beyond the existing `CutLabState` hidden-form round-trip. |
| V4 Access Control | No | No new authorization surface; feature stays behind the existing `tool.cut-lab.enabled` flag gate + same-origin checks already in place. |
| V5 Input Validation | Yes | New POST bodies (`/api/cut-lab/whatif`) must validate `cardOut`/`cardIn` names against the current pool (existing `CutLabStateSerializer.Deserialize` + pool-membership checks, same pattern `CutLabApiController.PostDecideAsync` already uses for `CardName`). Goal turn inputs must be clamped to a sane range (e.g., 1–15) server-side, not just client-side, before being passed into `PercentByTurn`. |
| V6 Cryptography | No | No new secrets/crypto surface. |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Forged cross-origin POST to a new `/api/cut-lab/whatif` endpoint | Spoofing/Tampering | Reuse `SameOriginRequestValidator.IsValid(Request)` exactly as `CutLabApiController.PostDecideAsync` already does (`CutLabApiController.cs:48-51`). |
| Oversized or malformed `CutLabStateJson`/goal payload causing excessive server work | Denial of Service | Reuse `[RequestSizeLimit(2 * 1024 * 1024)]` (matches existing `PostDecideAsync`) and `CutLabStateSerializer.MaxUploadBytes` (262,144 bytes) — do not raise these caps for the new goals field; it's a handful of ints. |
| Client tampers with localStorage scenario JSON to inject an oversized or malformed `CutLabState` on "Load" | Tampering | `CutLabStateSerializer.Deserialize` already fails open to `new CutLabState()` on any parse error or oversize payload (`CutLabStateSerializer.cs:41-46`) — loading a corrupted scenario degrades to an empty session rather than crashing; no new validation needed, but confirm the scenario-load path always routes through `Deserialize`, never a raw `JsonSerializer.Deserialize` call. |
| Swap "Keep" applied to a locked card via a hand-crafted request bypassing the client-side picker filter | Tampering / Elevation of Privilege (local) | Server-side re-validate `IsLocked` before applying Accept, exactly as `CutLabDecisionApplier.Apply` already no-ops (safely) for locked cards (`CutLabDecisionApplier.cs:26-30`) — confirm the new whatif-commit action doesn't bypass this by calling a lower-level method directly. |

## Sources

### Primary (HIGH confidence — read directly in this session)
- `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` — full read; metric projection, `PercentByTurn`/`MaxPercentByTurn`, `BuildSnapshot`, `ComputeProposalDeltas`.
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` — full read; shared resolve/classification context.
- `DeckFlow.Web/Services/CutLab/CutLabDeltaCache.cs` — full read; bounded `IMemoryCache`, TTL, size-limit pattern.
- `DeckFlow.Web/Models/CutLab/CutLabState.cs` — full read; snapshot envelope shape.
- `DeckFlow.Web/Models/CutLab/CutLabMetrics.cs` — full read; the 7-family/11-kind metric contract, noise floor constants.
- `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` — full read; turn-cap constants and their doc comments.
- `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs` — full read; working-list derivation, accepted-card set.
- `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs` — full read; Accept/Reject/Defer/Restore state transitions.
- `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` — full read; size cap, deserialize fail-open behavior.
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` — full read; JSON decide endpoint, same-origin validation, resolved-card seeding pattern.
- `DeckFlow.Web/Controllers/CutLabController.cs` — full read; no-JS form-POST fallback pattern.
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` — full read; client state, decision fetch/patch, DOM patching conventions.
- `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` — full read; sessionStorage try/catch persistence pattern (template for localStorage scenarios).
- `DeckFlow.Core/Manabase/ManabaseModels.cs` (`PlanRole` enum, `EarlyCastPercents` doc comment) — grepped/read relevant sections.
- `DeckFlow.Web.Tests/CutLabMetricsContractTests.cs` — full read; confirms the 7-family lock does not constrain `CutLabMetricKind` count.
- `.planning/workstreams/cut-lab/phases/104-goals-what-if-scenarios/104-CONTEXT.md`, `REQUIREMENTS.md`, `ROADMAP.md`, `STATE.md`, `103-CONTEXT.md` — full reads.
- `.planning/config.json` — confirms `nyquist_validation: true`; no `security_enforcement` key (defaults to enabled).

### Secondary (MEDIUM confidence)
None required — this phase's research is entirely groundable in the existing codebase; no external library/API research was needed.

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new dependencies; all reused components read directly from source.
- Architecture: HIGH for GOAL-02/03 mechanics (verified by reading the exact composable primitives); MEDIUM for the GOAL-01 family mapping (D-02) since it requires a planner/user decision between two documented, equally-valid options.
- Pitfalls: HIGH — Pitfalls 1, 3, 4, 5 are all confirmed by reading the exact source lines cited, not inferred.

**Research date:** 2026-07-20
**Valid until:** No external time pressure — this research is grounded entirely in this repository's own code, which only changes when Cut Lab code changes. Re-verify only if Phase 103's `CutLabSimulationService`, `CutLabDecisionApplier`, or `CutLabMetrics.cs` are modified before Phase 104 planning begins.
