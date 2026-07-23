# Phase: Manabase Research-Gap Closure - Context

**Gathered:** 2026-07-12 (foreman gap review + /gsd-discuss-phase same day)
**Status:** Ready for planning

<domain>
## Phase Boundary

Close the remaining gaps between the manabase research corpus and the shipped
`/manabase` analyzer. Scope locked by discussion (2026-07-12): **Tier 1 accuracy
gaps (MBGAP-01/02/03/04) + Tier 2 verdict-polish batch (MBGAP-05a-d) + closing
tasks MBGAP-11/12.** Tier 3 minors stay backlog; MBGAP-09 (cEDH castability
surface) is its own later phase.

The review confirmed all 5 HIGH and nearly all MEDIUM efficacy findings are
already fixed in code (see "Verified fixed" below — do NOT re-fix).

Out of scope: the manabase engine SRP refactor (separate backlog, needs parity
harness first); Casual/60-card land-target formula changes; MBGAP-09 cEDH
castability surface; Tier 3 items (scry-0.2, casual low-curve guard, snow
color category, LOW sweep L4/L5/L6/L9/L13).
</domain>

<decisions>
## Implementation Decisions (LOCKED 2026-07-12)

### Scope & tier cut
- **D-01:** Phase ships Tier 1 (MBGAP-01/02/03/04) + Tier 2 (MBGAP-05a-d) +
  MBGAP-11/12 as closing tasks. Tier 3 stays in backlog.
- **D-02:** MBGAP-09 (cEDH castability surface / early-interaction turns-1-3
  color-access lens) = own later phase. Keep the ROADMAP backlog pointer.

### MBGAP-01 — conditional-restriction lands
- **D-03:** **Composition-gated per-class modeling** (mirrors the check-land
  census pattern): Cavern of Souls / Unclaimed Territory = full color source
  only for the deck's dominant-creature-type share (heavy discount otherwise);
  Ancient Ziggurat = weight by creature share of deck; Nykthos = conditional
  low weight. NOT a flat discount, NOT full spend-restriction sim masks.
- **D-04:** **New flag `analysis.manabase.restricted-lands`, ships OFF.**
  Golden-deck diff + calibration before operator flip. Flag-off byte-identical.
- **D-05:** Disclosure = per-row marker in the castability table (reuse the
  alt-cost `1*` marker pattern) + entry in the existing
  unsupported-interactions panel.

### MBGAP-02 — untapped-land cycles (Phase 2 of conditional-untapped work)
- **D-06:** **All six cycles** get real rules: fast lands, slow lands, ELD
  threshold lands (Mystic Sanctuary class), Verge cycle, Vivid lands,
  Training Compound. Closes the classifier's documented backlog completely
  (`ManabaseClassifier.cs:479-501`).
- **D-07:** **Count-based conditions evaluated per-trial in the sim** (fast /
  slow / ELD: sim already tracks lands-in-play — evaluate at land-play time).
  Type-based cycles (Verge) use the static census pattern like check lands
  (≥6 matching-type rule). Vivid = ETB-tapped + limited any-color charges
  (modeling depth at planner discretion).
- **D-08:** Rides the **`analysis.manabase.accuracy` bundle, ON** — same lane
  as bond/check/Snarl conditional-untapped precedent. No new flag.

### MBGAP-03 — ritual land-target credit (cEDH-only, RIT O-4)
- **D-09:** **Calibration-fit weight**: start ~0.5 land per net-positive
  ritual (capped), tune against the 1597-deck cEDH harness
  (`cedh-land-calibrate` CLI) until under-flag% stays calibrated — same
  method that set floor 22 / blend 0.5. The data decides the constant.
- **D-10:** **New flag `analysis.manabase.ritual-land-credit`, ships OFF.**
  Do NOT reuse `ritual-burst-mana` (already ON in prod — reuse would change
  live land targets the moment the code deploys). Calibrate → operator flip.

### MBGAP-04 — consistency threshold
- **D-11:** **Research spike first.** This phase delivers a decision doc that
  (a) re-verifies Karsten 2022's escalation (settles EF2 L14 + the corpus
  contradiction between manabase-math.md "[H, verbatim]" and EF2
  "unconfirmed"), (b) evaluates the (85+M)% multiplayer relaxation case.
  Implement only if evidence supports — as a follow-up or a small gated plan.
  Doc contradiction fix in `docs/manabase-analysis-rules.md` regardless.

### Tier 2 — verdict polish batch (one plan)
- **D-12:** MBGAP-05a (`Math.Ceiling(-LandDelta)` overstatement,
  `ManabaseVerdictSynthesizer.cs:63`), 05b (silent 3-line truncation → append
  "…plus N more"), 05c (`(s)` plural artifacts in synthesizer + view), 05d
  (label per-color deficit as heuristic guidance on page + .txt + swap prompt
  per the parked EF1 #4 decision). Exact copy at Claude's discretion.

### Closing tasks
- **D-13:** MBGAP-11: re-audit `DeckFlow.Web/Help/manabase.md` line-by-line
  for overclaims (file rewritten since EF2; old line numbers dead).
- **D-14:** MBGAP-12: visual verification of tap-analyzer + mulligan lenses
  in a browser, 2 viewports (never done; UI review scored markup only).

### Claude's Discretion
- Vivid charge-counter modeling depth (D-07 note).
- Verdict-polish exact copy/wording (D-12).
- Calibration acceptance bars for D-04/D-09 flips (default: match the
  cEDH-land-target precedent — no re-opened under-flag regression, grindy
  decks stay healthy).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

Research corpus (claims being closed):
- `.planning/captures/manabase-efficacy-findings-r2.md` — M8 (MBGAP-01), L14 (MBGAP-04), L1/L2 (MBGAP-05)
- `.planning/captures/manabase-efficacy-findings.md` — EF1 #4 parked decision (MBGAP-05d)
- `.planning/captures/manabase-ritual-burst-mana-spec.md` — O-4 + OUT-v1 lanes (MBGAP-03)
- `.planning/captures/manabase-cedh-land-target-phaseB-PLAN.md` — calibration method precedent (floor 22 / blend 0.5 / ceiling 45)
- `.planning/manabase-mode-research.md` §4 — recommendations #2 (threshold) context
- `.planning/research/manabase-math.md` §1-2 — Karsten thresholds + source-counting rules
- `.planning/ui-reviews/manabase-UI-REVIEW.md` — plural artifacts, lens-verification gap

Engine ground truth:
- `docs/manabase-analysis-rules.md` — actively-maintained rule reference ("code wins" caveat); update it in the SAME change when behavior ships
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs:479-501` — the documented cycle backlog comment (MBGAP-02)
- `DeckFlow.Core/Manabase/KarstenManabase.cs` — land-target constants + cEDH clamp path (MBGAP-03 credit lands here)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — per-trial land tracking for D-07
- `DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs:54-63` — MBGAP-05a/b/c anchor
- `DeckFlow.CLI` `cedh-land-calibrate` + `cedh-land-baseline` commands — calibration harness for D-04/D-09
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — new flags D-04/D-10 register here (seed OFF)

Prod state (verified live 2026-07-12): ALL 15 `manabase.*` flags ON, including
`cedh-land-target` + `ritual-burst-mana` (flipped 2026-07-11).
</canonical_refs>

<code_context>
## Reusable Assets & Patterns

- Check-land census pattern (`CheckLandMatchTypeThreshold=6`, union type count) — template for D-03 composition gates + Verge (D-07).
- Alt-cost `1*` row-marker + suggestion plumbing — template for D-05 disclosure marker.
- `UnsupportedInteraction` record + `<details>` panel — D-05 panel entries.
- Conditional-source Bernoulli machinery (granted sources, weight 0.25) — candidate for Nykthos low-weight modeling.
- Flag rollout precedent: new flag OFF → golden diff → calibration harness → operator flip (`cedh-land-target`, `ritual-burst-mana`).
- `CedhCalibration.cs` + `_calib` harness runs — reuse for D-09 weight fitting.
- Standing rules: Codex codes / Claude plans+reviews; page changes need xUnit + Playwright + 2-viewport visual; README + `docs/manabase-analysis-rules.md` updated in-change.
</code_context>

<verified_fixed>
## Verified fixed — do NOT re-fix (LEAD spot-checked in source 2026-07-12)

EF2 H1 (enters-tapped wording, `ManabaseClassifier.cs:947`), H2 (Treasure-maker
5-color sources — `HasRepeatableManaAbility` gate), H3 (swap-prompt ramp-covered
third branch, `ManabaseSwapPromptBuilder.cs:70`), H4 (verdict shares
`ColorIssueFindings`/`ComputeColorSignals`), H5 (60-card constants now
19.59 + 1.90·MV), M1 (mulligan bottoming past drawable prefix), M2 (slack-turn
tapped-fixer sequencing, `CastabilitySimulator.cs:1256`), M3 (`gateRampOnCastable`
hardcoded ON), M4/M4b (reminder-text strip + one-shot sac guard), M6 (`modal_dfc`
layout gate), M7 (draws-two shared predicate), M9 (single shared
`AvgOnCurvePercent`), M10 (.txt command-zone block via
`ManabaseCommandZoneFormatter`), M11 (`OverridesTouched` + `NotAppliedOverrides`).
</verified_fixed>

<deferred>
## Deferred Ideas (explicitly out of this phase)

- **MBGAP-09** — cEDH castability surface (own later phase; ROADMAP backlog).
- Tier 3: MBGAP-06 scry-0.2 source credit, MBGAP-07 casual low-curve guard
  (verify with a real 1.8-MV deck first — may be a non-issue), MBGAP-08 snow
  color category, MBGAP-10 LOW sweep (L4 verify-then-fix, L5, L6, L9, L13).
- Research-corpus deliberate exclusions: X-spells (Salubrious X=3/2/1),
  landcycling, "+1 Wastes vs +1 Basic" perturbation diagnostic, cost-cheater
  toggle, Treasure stockpiling / sac-outlet engines, Chrome Mox / Spirit
  Guide imprint-exile producers, commander recast tax, rocks-removability
  toggle.
</deferred>
