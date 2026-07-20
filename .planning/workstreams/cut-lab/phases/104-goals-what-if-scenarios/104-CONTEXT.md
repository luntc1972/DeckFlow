# Phase 104: Goals & What-If Scenarios - Context

**Gathered:** 2026-07-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Turn the fixed consistency model into a **user-steerable goal + scenario lab** on top of the Phase 103 Cut Lab. Delivers exactly three requirements:

- **GOAL-01** — user defines turn-based goals (cast commander by turn N, ramp by turn N, interaction by turn N, engine/payoff by turn N).
- **GOAL-02** — user saves and reloads **named scenarios** capturing goals + locks + deck intent (+ working state).
- **GOAL-03** — user runs a **what-if swap** (replace working-list card A with card B) and immediately sees all goal + consistency metrics recalculated using the Phase 103 engine.

In scope: editable goal targets, named scenario save/load, non-destructive swap preview, per-goal results display.
Out of scope: new simulation math (reuse 103 engine), builder export (Phase 105), server-side/per-user persistence (no accounts), free-form custom goal categories outside the 103 metric families.

</domain>

<decisions>
## Implementation Decisions

### Goal definition model (GOAL-01)
- **D-01:** Goals = **editable turn targets on the fixed 103 metric families** (commander, ramp, interaction, engine, payoff). User edits the **turn number** per family; no add/remove of custom categories — this keeps every goal backed by an existing 103 sim metric (SIM-01: "no new simulation math"). Rejected: free custom goal rows (would need new sim math), preset-only (doesn't satisfy "define").
- **D-02 (discretion, planner to firm up):** Seed the editable turn targets from the existing **`CedhMulliganCalibration`** turn-caps per bracket/play-experience (commander-by-3, engine-by-2, representative-line-by-4, etc. — the same fixed caps 103 used under D-17). User adjusts from that seed. Which exact families are exposed as goals and their per-bracket seed defaults = planner/researcher to enumerate against the 103 metric family list.

### Scenario storage (GOAL-02)
- **D-03:** Named scenarios persist in **browser `localStorage` named slots** (save / load / delete / list by name). Per-browser, zero backend — matches the no-accounts constraint. Rejected: named session-JSON download/upload (manual file handling), both (extra surface). File export/import is a possible later enhancement (see Deferred).
- **D-04:** A scenario captures a **full snapshot**: goals + locks + deck intent + the current working-list / cuts-made / baseline state. Reuse the existing `CutLabState` serializer as the snapshot payload so a loaded scenario restores the whole session, not just config.

### What-if swap UX (GOAL-03)
- **D-05:** The swap is a **non-destructive preview**: pick card A from the working list, card B from the **cuts-made pile / cards not currently in the working list** (the original pool minus the working 100 — all already resolved, so no new Scryfall calls). Show all goal + consistency metric **deltas** instantly via the 103 engine, then **Keep** (commit to the working list) or **Discard** (revert). Rejected: committed-edit swap (no try-before-commit).
- **D-06 (discretion):** Card B via **Scryfall card search** (a brand-new card not in the original pool) is a possible enhancement but adds resolve latency + cache work — planner may defer it; the baseline swap source is the cut pile / non-working original-pool cards.

### Goal results + engine coupling (D-07)
- **D-07:** Editable goals are **display-only**. Per-goal **pass/miss + probability-vs-target** renders in the existing metrics / compare panels; the 103 **cut-round ordering and determinism stay unchanged** — goals inform the user, they do not re-drive cuts. Rejected: feeding goals back into cut ordering (a meaningful change to the just-shipped 103 engine + its determinism guard/tests; revisit only if UAT shows it's needed).

### Claude's Discretion
- D-02 (goal seed defaults + exact exposed families), D-06 (whether card-search is in scope for swap source), the localStorage slot schema/versioning + quota handling, and the exact per-goal results widget (badge vs bar vs %-vs-target) are left to research/planning within the decisions above.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/workstreams/cut-lab/ROADMAP.md` §"Phase 104" — goal, success criteria, dependencies (103 engine + 101 locks/intent).
- `.planning/workstreams/cut-lab/REQUIREMENTS.md` — GOAL-01/02/03, SIM-01/02.
- `.planning/workstreams/cut-lab/phases/103-simulation-engine-guided-cut-rounds/103-CONTEXT.md` §D-17 — the fixed CedhMulliganCalibration caps that 104 makes editable (explicit hand-off).

### Reuse targets (103/101 engine + state)
- `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` — 7-family metric projection + baseline builder (the recalc engine; reuse, no new math).
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` — shared context/delta producer for page + endpoint (the seam a what-if preview should reuse).
- `DeckFlow.Web/Services/CutLab/CutLabDeltaCache.cs` — on-demand delta cache (working-list hash + card) for fast recalc.
- `DeckFlow.Web/Models/CutLab/CutLabState.cs` + `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` — the snapshot payload for GOAL-02 scenarios.
- `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` — the turn-cap model that seeds GOAL-01 goal targets.
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` — client state, decision fetch/patch, restore forms (the swap/preview + scenario-slot UI hangs off here).
- `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` — existing sessionStorage persistence pattern (reference for localStorage slot handling).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **103 simulation + delta engine** (`CutLabSimulationService`, `CutLabAnalysisContextBuilder`, `CutLabDeltaCache`) — the what-if recalc and per-goal probabilities ride these directly; no new sim math.
- **`CutLabState` + serializer** — already the whole-session snapshot; GOAL-02 scenario = a named localStorage copy of this payload.
- **Decision fetch/patch path in `cut-lab.ts`** (accept/reject/defer/restore) — the what-if swap preview is a sibling interaction (compute deltas, then Keep = commit / Discard = revert).
- **`CedhMulliganCalibration`** — per-bracket turn caps become the editable goal seeds.

### Established Patterns
- No user accounts → all persistence is client-side (localStorage/sessionStorage) or the server session cache. Scenarios MUST be localStorage.
- 103 metrics are computed only for fixed families → goals stay bound to those families (no custom categories).
- Progressive enhancement (server render + JS patch, both paths in sync) — any new UI (goals editor, scenario slots, swap preview) needs a no-JS story consistent with 103.

### Integration Points
- Goals editor + per-goal results slot into the existing metrics/compare panels (display-only, D-07).
- What-if swap reuses `CutLabAnalysisContextBuilder` to produce deltas against the current working list.
- Scenario save/load wraps `CutLabStateSerializer` output in named localStorage slots.

</code_context>

<specifics>
## Specific Ideas

- Goal targets seeded from the same `CedhMulliganCalibration` caps 103 already uses (continuity: the fixed model becomes the editable default).
- What-if swap B-source is the cut pile / non-working original-pool cards first (already resolved) — Scryfall search for arbitrary new cards is the stretch.
- "Keep / Discard" language for the non-destructive preview mirrors the accept/reject decision vocabulary users already learned in 103.

</specifics>

<deferred>
## Deferred Ideas

- **Named session-JSON file export/import** for cross-browser scenario portability — localStorage slots ship first; file export is a later enhancement (D-03).
- **What-if swap card B via Scryfall search** (arbitrary card not in the original pool) — adds resolve latency/cache work; baseline uses cut-pile/original-pool cards (D-06).
- **Goals feeding back into cut-round ordering** — display-only for now; revisit only if UAT shows goal-driven cut ordering is wanted (D-07). Would touch the 103 determinism guard.
- **Custom goal categories** beyond the 103 metric families — needs new simulation math; out of milestone scope.

</deferred>

---

*Phase: 104-Goals & What-If Scenarios*
*Context gathered: 2026-07-20*
