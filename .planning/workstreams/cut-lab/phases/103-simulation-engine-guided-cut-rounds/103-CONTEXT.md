# Phase 103: Simulation Engine & Guided Cut Rounds - Context

**Gathered:** 2026-07-19
**Status:** Ready for planning

<domain>
## Phase Boundary

A builder works through cuts in a fixed, evidence-backed order (obvious cuts → structural choices → preference calls) and sees the measurable consequence of every proposed cut. After every accepted cut or swap, the working list's metrics recalculate by **reusing** the existing Monte Carlo / mulligan / castability / plan-presence engines in `DeckFlow.Core/Manabase/` — no new simulation math. The tool never labels a card objectively worse. Requirements: CUT-01, CUT-02, CUT-03, SIM-01, SIM-02.

</domain>

<decisions>
## Implementation Decisions

### Cut round mechanics
- **D-01:** "Obvious cut" (round 1) = card flagged by **2+ Phase-102 structural findings** (e.g. surplus role + curve-top + weak castability). Framed as "flagged by N findings" — never "objectively worse".
- **D-02:** Proposals presented **one card at a time**: single proposed cut with its deltas → accept/reject/defer → next.
- **D-03:** Rounds **auto-advance** when a round's queue exhausts, with a clear banner ("Round 2: Structural choices"). No dead-end states.
- **D-04:** Round 2 = cards with **exactly one** structural finding (including surplus-over-floor). Round 3 = all remaining unlocked cards, ordered weakest-delta-first but framed as preference calls. If still >100 after round 3, rounds **loop** over deferred (then rejected) cards until 100 is reached.

### Tradeoff delta display
- **D-05:** Per proposal, show **changed-only compact** deltas: only metrics that meaningfully moved (direction + magnitude), a "no meaningful change" line for the rest, and an expander revealing the full 7-family metric table.
- **D-06:** **Directional colors are OK** (metric down = red/down-arrow, up = green). Neutrality is enforced in copy ("cutting this lowers keepable-hand 2.1%"), not by hiding direction. No verdict language.
- **D-07:** Proposal deltas are measured **against the current working list only**. Cumulative drift vs the original pool baseline lives exclusively in the SIM-02 before/after view.
- **D-08:** **Fixed deterministic seed** per (decklist, iteration count) so re-renders show identical numbers, plus a **noise floor**: deltas below threshold render as "no meaningful change" rather than jitter. Researcher must verify existing engines' seed-injection support.

### Recalc timing & performance
- **D-09:** Accept/reject/defer actions go through **async fetch + partial update**: TypeScript posts to a JSON endpoint (guarded by `SameOriginRequestValidator`, following the `DeckSyncApiController` pattern) and updates the proposal card + metrics in place. Form-POST full-page flow remains the no-JS fallback for state integrity.
- **D-10:** Deltas computed **on-demand for the presented card only**, cached by (working-list hash, card). No upfront round-wide sim burst — bounded cost per action, safe under the 512MB Render RAM cap.
- **D-11:** Latency budget: **~1s target, 3s hard cap** (with spinner) per decision action. If existing engine iteration defaults exceed this, reduce iterations for in-loop deltas; reserve full-fidelity runs for round summaries and the before/after view.
- **D-12:** Original-pool **baseline metrics = compact numeric snapshot stored inside CutLabState** (computed once at pool lock; survives restarts; needed for SIM-02). Per-card **delta cache = IMemoryCache with TTL** — disposable and recomputable, keeps the serialized state field small.

### Baseline compare + defer UX
- **D-13:** SIM-02 before/after = **collapsible panel on the same page**: three-column table (baseline | current | delta), matching Cut Lab's existing accordion sections. Available at any point without leaving the proposal flow.
- **D-14:** Cards-remaining-to-100 = **slim sticky bar**: current round name + "N cards to cut" + accepted-cut count. Always visible on scroll, desktop + mobile, 44px touch-safe, theme-tokened.
- **D-15:** Defer = **parked until loop-around**: deferred cards sit out until all rounds exhaust; the loop-around pass re-proposes deferred (then rejected) cards if still >100.
- **D-16:** Undo via **cuts-made list with restore-any**: every accepted cut is individually restorable — card returns to the working list, metrics recalculate, counter updates. Order-independent.

### Claude's Discretion
- Exact noise-floor threshold values and per-metric formatting precision.
- In-loop vs full-fidelity iteration counts (set from researcher's measured engine cost).
- Delta cache TTL and eviction policy.
- Sticky-bar exact layout/markup within the constraints above.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Workstream planning
- `.planning/workstreams/cut-lab/ROADMAP.md` — Phase 103 goal, success criteria, dependencies on Phases 101/102.
- `.planning/workstreams/cut-lab/REQUIREMENTS.md` — CUT-01..03, SIM-01, SIM-02 exact wording.

### Phase 102 outputs this phase builds on
- `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` — the 5 finding detectors whose flags drive round ordering (D-01, D-04).
- `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs` — 8-role assignment feeding surplus-over-floor logic.
- `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs` — role-floor contract explicitly written in Phase 102 as the Phase 103 interface.
- `DeckFlow.Web/Models/CutLab/CutLabState.cs` + `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` — state round-trip that must absorb working list, decisions, and baseline snapshot (D-12).
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` — stage-based page orchestration to extend.

### Simulation engines to reuse (no new math)
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — top-level analysis entry.
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — Monte Carlo castability.
- `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` — keepable-hand/mulligan model.
- `DeckFlow.Core/Manabase/KarstenManabase.cs`, `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` — deterministic mana math and budget inputs.

### API pattern for the async endpoint
- `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs` — JSON endpoint + `SameOriginRequestValidator` pattern to copy (D-09).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Manabase engine family (`DeckFlow.Core/Manabase/`): Monte Carlo, castability, mulligan, Karsten math — SIM-01 explicitly forbids new simulation math; these are the engines.
- `CutLabPageService` + `CutLabState` serializer: proven form-post state round-trip; extend rather than replace.
- Phase 102 structural findings + role floors: direct inputs to round population.
- Existing accordion sections / theme tokens in `cut-lab` CSS and `cut-lab.ts` TS module.

### Established Patterns
- API endpoints use `SameOriginRequestValidator` + JSON `{ Message }` errors (see `DeckSyncApiController`).
- TS compiled via MSBuild from `wwwroot/ts/`; compiled JS is gitignored — never commit `.js`.
- Layout CSS goes in `site-common.css`; theme tokens in each theme file's `:root`.
- Feature flag `tool.cut-lab` (flag currently OFF in prod) gates the whole tool.

### Integration Points
- `CutLabController` (form POSTs) + new JSON API controller under `Controllers/Api/`.
- `CutLabViewModel` extension for rounds/proposals/compare sections.
- `IMemoryCache` (already registered) for delta cache.

</code_context>

<specifics>
## Specific Ideas

- Evidence framing: proposals justified as "flagged by N findings", listing the findings — the same evidence-first voice as the Phase 102 structural findings sections.
- Deterministic-numbers brand: DeckFlow markets deterministic Karsten math over AI vibes — fixed seeds + noise floor (D-08) keep that promise inside the cut loop.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (What-if swaps and turn-based goals are already Phase 104; export validation is Phase 105.)

</deferred>

---

*Phase: 103-Simulation Engine & Guided Cut Rounds*
*Context gathered: 2026-07-19*
