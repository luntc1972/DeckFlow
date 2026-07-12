# Phase: Manabase Research-Gap Closure — Context

**Gathered:** 2026-07-12 (foreman review session — 2-scout research-vs-implementation diff + LEAD source spot-verification)
**Status:** Backlog candidate — needs `/gsd-discuss-phase` to lock scope before planning (no decisions locked yet)
**Source:** Full research-corpus audit (10 docs, 158 extracted claims) diffed against the shipped engine (`DeckFlow.Core/Manabase/`, 24 files) and live prod flag state (verified 2026-07-12: ALL 15 `manabase.*` flags ON, including `cedh-land-target` and `ritual-burst-mana`, both flipped 2026-07-11).

<domain>
## Phase Boundary

Close the remaining gaps between the manabase research corpus
(`.planning/research/manabase-math.md`, `manabase-mode-research.md`,
`manabase-modes-castability-SPEC.md`, efficacy findings r1/r2, ritual-burst spec,
cEDH land-target plans) and the shipped `/manabase` analyzer. Review confirmed all
5 HIGH and nearly all MEDIUM efficacy findings are already fixed in code (see
"Verified fixed" below — do NOT re-fix). What remains is a bounded set of modeling
gaps, verdict-polish LOWs, and one research task.

Out of scope: the manabase engine SRP refactor (separate backlog item, needs parity
harness first); any Casual/60-card land-target formula change beyond MBGAP-07;
cEDH castability full surface redesign beyond what MBGAP-05 scopes.
</domain>

<requirements>
## Requirements (from gap report, priority order)

### Tier 1 — accuracy gaps worth building

- **MBGAP-01 (was G1) — Conditional-restriction lands.** Cavern of Souls /
  Ancient Ziggurat / Unclaimed Territory / Nykthos are counted as unconditional
  any-color sources at face value (`IsConditional=false`); no "spend this mana only
  on X" handling. Disclosure panel exists (`Manabase.cshtml:655`) but counting is
  unchanged. Source: EF2 M8. Biggest open accuracy item.
- **MBGAP-02 (was G2) — Unclassified untapped-land cycles (Phase 2 of
  conditional-untapped work, never started).** Fast lands, slow lands, ELD
  threshold lands (Mystic Sanctuary), Verge cycle, Vivid lands, Training Compound
  all classify as plain taplands. Classifier's own comment marks these backlog
  (`ManabaseClassifier.cs:479-501`). Inflates tap-penalty on modern manabases.
- **MBGAP-03 (was G4) — Ritual land-target credit re-judge (RIT O-4), now
  UNBLOCKED.** Burst mana credits castability only; recommended land count is
  still ritual-blind. Spec deferred this until cast% reflected rituals in prod —
  `ritual-burst-mana` flipped ON 2026-07-11, so the re-judge is actionable.
  cEDH-only credit path per spec; calibrate against the 1597-deck harness
  (`cedh-land-calibrate` CLI) before shipping.
- **MBGAP-04 (was G3) — Multiplayer threshold research.** (89+M)% consistency
  threshold never relaxed for 4-player (MR §4 rec #2 suggests ~(85+M)% or a
  draw-count bonus); compounds with EF2 L14: the escalating 90→96% itself is
  unconfirmed against Karsten 2022 (flat ~90%?). Research/decision task first,
  code second. Also resolve the corpus contradiction (MM says "verbatim [H]",
  EF2 says "unconfirmed") in `docs/manabase-analysis-rules.md`.

### Tier 2 — verdict/UX polish (one small batch ticket)

- **MBGAP-05a (EF2 L1)** — `Math.Ceiling(-LandDelta)` turns a 1.05 shortfall into
  "add ~2 land(s)" (`ManabaseVerdictSynthesizer.cs:63`). Round or show raw delta.
- **MBGAP-05b (EF2 L2)** — verdict silently truncates to 3 issue lines; append
  "…plus N more." (no such string exists in synthesizer today).
- **MBGAP-05c (UI review top fix #1)** — kill `(s)` plural artifacts: `land(s)`
  (`ManabaseVerdictSynthesizer.cs:63`), `card(s)` (`Manabase.cshtml:658`); page
  already has the correct singular/plural pattern elsewhere.
- **MBGAP-05d (EF1 #4 parked decision follow-through)** — per-color deficit is
  heuristic on 3-5c shared-fixer decks; the locked decision was "label it
  heuristic guidance" — confirm/add that label on page + .txt + swap prompt.

### Tier 3 — smaller modeling gaps (pick per discuss-phase)

- **MBGAP-06 (was G6)** — Karsten scry-1 ≈ 0.2 source credit absent.
- **MBGAP-07 (was G7)** — casual low-curve land-target guard (MR §4 rec #5);
  cEDH path got floor 22 / ceiling 45, casual singleton regression is uncapped.
  Verify with a real 1.8-MV casual deck before building — may be a non-issue.
- **MBGAP-08 (was G8)** — snow folded into colorless; Karsten treats snow as its
  own color category (Arcum's Astrolabe class).
- **MBGAP-09 (was G5) — cEDH castability surface.** Castability table hidden in
  cEDH mode (`ShowCastability` requires Casual); MR's core cEDH insight (evaluate
  early-interaction color access, turns 1-3 single/double pips, not spell-MV
  on-curve) never built. Larger design item — scope in discuss-phase.
- **MBGAP-10 (LOW sweep, verify-then-fix)** — EF2 L4 (`ReserveGenericForRamp`
  rationale — possibly obsoleted by P4 deploy-friction rework, check first),
  L5 (analytic ceiling onPlay vs sim baseline off-by-~1), L9 (`DetectGranter`
  misses singular "creature" wording — Relic of Legends), L13 (land-ramp sim
  doesn't thin library — documented behavior gap), L6 (true `{C}` pips folded
  into generic — needs sixth mask bit if ever modeled).

### Explicitly deferred (documented, do not build without new decision)

- X-spells excluded from spell requirements (Salubrious models X=3/2/1) — deliberate.
- Landcycling, "+1 Wastes vs +1 Basic" perturbation diagnostic, cost-cheater
  toggle (Reanimate/Sneak Attack), Treasure stockpiling / sac-outlet engines,
  Chrome Mox / Spirit Guide imprint-exile producers (RIT OUT-v1), commander
  recast tax, rocks-removability toggle (MR §4).

### Operational / docs (no engine code)

- **MBGAP-11** — M12 help-doc re-audit: `Help/manabase.md` rewritten since EF2;
  re-check for overclaims line-by-line (old line numbers dead).
- **MBGAP-12** — tap-analyzer + mulligan lenses have never been visually verified
  in a browser (UI review scored markup/CSS only). Screenshot pass, 2 viewports,
  per standing UI rule.
</requirements>

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

Prod flags: ALL 15 `manabase.*` ON (verified live query 2026-07-12).
</verified_fixed>

<notes>
## Notes for discuss-phase

- Suggested shape: Tier 1 = the phase core (MBGAP-01/02/03 engine work + MBGAP-04
  research spike); Tier 2 = one batch plan; Tier 3 items individually cheap —
  gate on operator appetite. MBGAP-09 may deserve its own later phase.
- MBGAP-03 ships behind existing `ritual-burst-mana` flag semantics (cEDH-only);
  re-baseline goldens expected.
- MBGAP-01/02 change classification → expect golden-deck diffs; use the
  calibration harness + `docs/manabase-analysis-rules.md` regression guards.
- Standing rules apply: Codex codes / Claude plans+reviews; new/changed page
  behavior needs xUnit + visual verify at 2 viewports.
</notes>
