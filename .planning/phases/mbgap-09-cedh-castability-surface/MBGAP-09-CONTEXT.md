# Phase MBGAP-09: cEDH Castability Surface - Context

**Gathered:** 2026-07-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Give cEDH mode a real early-interaction castability surface. Today cEDH hides the
per-card castability table (`Manabase.cshtml:822` — "Castability view is available in
Casual mode") and the turns-1-3 cheap-interaction color-access idea exists only as prose
in the swap prompt (`ManabaseSwapPromptBuilder.cs:52`). This phase delivers the deferred
promise from `.planning/manabase-modes-castability-SPEC.md` (mode table row
"evaluate cheap-interaction color access at turns 1–3" + line 61 "detailed cEDH
castability surface can follow"):

1. cEDH mode computes a **by-turn-3 holdable** metric for cheap interaction spells
   (untapped color access, turns 1–3).
2. cEDH mode shows an **"Early interaction" third lens** in the header strip plus the
   **full per-card castability table** (v1 note removed).
3. The lens data joins **both prompt artifacts** (report text + swap prompt).

Out of scope: verdict/health math changes (informational v1), Casual-mode behavior
changes, engine SRP refactor, Tier-3 minors (MBGAP-06/08/10).

</domain>

<decisions>
## Implementation Decisions

### Cheap-interaction definition
- **D-01:** Qualifying spells = `PlanRole.Interaction` (existing classifier tag) with
  **effective MV ≤ 2**. No new detection heuristics.
- **D-02:** Effective MV = **after the existing reduced/alt-cost override machinery**
  (the `1*` marker path). Fierce Guardianship / Deflecting Swat with a commander-out
  override at MV0 qualify; printed-MV3 without an override does not.
- **D-03:** Empty state (zero qualifying spells): lens renders a **caution-styled
  warning** — "no cheap interaction found" — because that is itself a cEDH finding.
  Do not hide the lens silently.
- **D-04:** Auditability: the lens lists **per-spell rows** (show the work,
  FORMULA-01 philosophy) so mis-tagged spells are visible.

### Metric & math engine
- **D-05:** Engine = **sim-based, per-trial** — reuse `CastabilitySimulator`'s existing
  per-trial board state (untapped/tapped tracking, London mulligan, ritual burst,
  conditional cycles). Record per-trial castability with untapped sources. NOT a second
  analytic Karsten idiom (phase-64 VALIDATION showed ~30-pt independence errors).
- **D-06:** Measured quantity per spell = **by-turn-3 holdable**: P(spell castable with
  untapped mana on AT LEAST one of turns 1–3). One number per spell — not on-curve-only,
  not a T1/T2/T3 breakdown.
- **D-07:** **Raw availability v1** — ignore competition from the deck's own proactive
  plays. Lens caption carries the caveat ("assumes you hold mana open").
- **D-08:** Headline aggregate = **N/M spells on target at the existing
  `CedhSupportThreshold` (88)** — "X / Y interaction held up by turn 3", mirroring the
  Karsten lens's "N/M colors on target" idiom. Reuse the constant; do not fork it.

### cEDH table & placement
- **D-09:** cEDH mode now renders the **full per-card castability table** (same math
  already runs in both modes). Remove the `mode-note` at `Manabase.cshtml:820-823`.
- **D-10:** The interaction lens renders as a **third lens in the header strip**
  (cEDH-only "Early interaction" joining "Karsten source check" + "Simulated cast
  rate"). Extend the `manabase-twolens` layout to a responsive 3-up; single/dual states
  must still work (Casual stays two lenses).
- **D-11:** Lens shows **worst-holdable 5 spells + native `<details>` "view all"
  expander** for the rest. Lens stays lens-sized; tail-risk rows always visible.
- **D-12:** cEDH castability table = **identical columns to Casual + a holdable %
  column/badge on interaction rows only**. Keep Casual's worst-first sort contract.

### Verdict / prompt / rollout
- **D-13:** **Informational v1** — verdict/health synthesis untouched. (Corroboration
  or full verdict input is a later, calibrated follow-up.)
- **D-14:** Lens data joins **both prompt artifacts**: `ManabaseReportTextBuilder` gains
  an "Early interaction (turns 1–3)" block; the swap prompt upgrades its generic prose
  line (`ManabaseSwapPromptBuilder.cs:52`) with the real N/M number + worst spells.
  Core value: a lens ChatGPT can't see is a half-shipped lens.
- **D-15:** **One new cEDH-only flag, seeded ON** (suggested name
  `analysis.manabase.cedh-interaction-lens`) gating lens + table exposure + artifact
  blocks. Flag-off = byte-identical current output (kill switch). Follows the recent
  default-ON flag batch precedent; no owed operator flip.

### Claude's Discretion
- Lens copy/naming ("Early interaction" vs alternatives), met/short glyph reuse.
- 3-up lens strip CSS details, mobile stacking order, theme-token choices
  (use `--panel` in dark themes, not `--theme-surface`).
- MV0 rows (free after override) trivially 100% color-holdable — presentation choice.
- Whether the sim stat rides existing trials or a dedicated counter struct.
- Flag name final spelling; whether it must join any prompt-cache invalidation set
  (check `PromptMutatingAnalysisFlags` precedent — that set is analysis-packet-side;
  verify whether manabase artifacts have an equivalent replay cache).

### Mandatory closing tasks (not gray areas — M12 precedent)
- `Help/manabase.md` documents the lens, its threshold, and the raw-availability caveat.
- Both formula panels ("How the analysis works" + "This deck's numbers") cover the
  new metric with the deck's plugged-in numbers.
- README behavior-change entry.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase origin & locked scope
- `.planning/captures/manabase-backlog-2026-07-13.md` §2 — MBGAP-09 backlog entry (this phase).
- `.planning/phases/manabase-research-gap-closure/CONTEXT.md` — prior phase's D-02 (own-phase lock) + deferred section; flag/disclosure precedents D-04/D-05/D-08.
- `.planning/manabase-modes-castability-SPEC.md` — mode table ("evaluate cheap-interaction color access at turns 1–3", untapped weighting) and the deferred cEDH-surface sentence (line 61); castability model + CardCastability shape.

### Findings that shape constraints
- `.planning/captures/manabase-efficacy-findings-r2.md` — M9 (never show two disagreeing numbers for one concept), M12 (help-doc overclaim class), L2 (no silent truncation — the worst-5 expander must disclose the remainder count).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CastabilitySimulator` (`DeckFlow.Core/Manabase/CastabilitySimulator.cs`) — per-trial board state already tracks untapped/tapped lands, mulligans, ritual burst, conditional-cycle timing; add per-trial interaction-holdable bookkeeping, do not build a second engine.
- `PlanRole.Interaction` (`ManabaseModels.cs:153`) — classifier already tags removal/counters/protection; D-01 consumes it as-is.
- Alt-cost override machinery (the `1*` marker path) — supplies effective MV for D-02.
- `CedhSupportThreshold = 88` (`ManabaseAnalyzer.cs:17`) — D-08 headline threshold; reuse the constant.
- Two-lens header (`Manabase.cshtml:430-455`, `manabase-twolens` / `manabase-lens-*` CSS in `site-common.css`) — extend to 3-up; met/short glyph helpers in `ManabaseDisplay.cs:109-121`.
- `ManabaseReportTextBuilder` + `ManabaseSwapPromptBuilder` — artifact insertion points (swap-prompt prose line at `ManabaseSwapPromptBuilder.cs:52`).
- Capped-table mobile pattern from gap-closure plan 10 — reuse for the holdable column.

### Established Patterns
- cEDH-only feature gate: `flagOn && mode == ManabaseMode.Cedh` (ritual burst/credit precedent, `ManabaseAnalyzer.cs:169,179`).
- Flag seeding: recent batches seed ON with flag-off byte-identical output as the safety property.
- Disclosure: per-row marker + panel entry (D-05 restricted-lands precedent).
- Layout CSS goes in `site-common.css`, never `site.css`; dark themes use `--panel`.
- Web-page change rule: xUnit + Playwright + desktop/mobile screenshots across themes in the same change.

### Integration Points
- `ManabaseViewModel.ShowCastability` (`ManabaseViewModel.cs:110`) — currently `Mode == Casual`; becomes mode-aware under the new flag (D-09).
- `Manabase.cshtml:216-233` lens-strip conditionals + anchor list — third lens entry.
- `Manabase.cshtml:820-823` — cEDH mode-note removal (D-09).
- `ManabaseAnalyzer` → report model: new lens data rides `ManabaseReport` (additive `{ get; init; }` property — carve-out: never let the formatter convert to `{ get; }`).

</code_context>

<specifics>
## Specific Ideas

- Headline reads like the Karsten lens: big "N / M" + "interaction held up by turn 3".
- Lens caption carries the raw-availability caveat ("assumes you hold mana open").
- Empty state is a caution, not silence — a cEDH list with no cheap interaction should
  say so.

</specifics>

<deferred>
## Deferred Ideas

- Verdict/health integration of the interaction lens (corroboration or full input) —
  needs calibration against real cEDH lists; explicitly out of v1 (D-13).
- After-development residual hold-up modeling (mana competition from the deck's own
  proactive plays) — requires a proactive-play heuristic the sim doesn't have (D-07).
- Per-turn T1/T2/T3 breakdown view — rejected for v1 width/mobile cost (D-06).
- Exposing the interaction lens in Casual mode.

</deferred>

---

*Phase: MBGAP-09-cedh-castability-surface*
*Context gathered: 2026-07-13*
