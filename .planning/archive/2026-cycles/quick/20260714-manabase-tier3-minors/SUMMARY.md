---
task: manabase-tier3-minors
type: quick
status: complete
completed: 2026-07-14
branch: quick/manabase-tier3-minors
commits: e35da9a9, 1f6af272, 0234fe53, 1ca3d2bb, cad2585c, 46f4dbbf
---

# Summary: Manabase tier-3 research minors (MBGAP-06, MBGAP-08, MBGAP-10)

## What shipped

**MBGAP-10 LOW sweep** (`e35da9a9` + `1f6af272`):
- **L4** — `ReserveGenericForRamp` KEPT, not deleted. The efficacy finding claimed it
  provably dead; gpt-5.5 plan review refuted that (it runs before `ApplyRitualBurst`
  and the mana check), and the new interaction test
  `ReserveGenericForRamp_RitualBridgeDelayIsObservable` proved the effect empirically
  (77%/0.7 delay → 76%/1.0 without it). Rationale comments corrected; test kept as
  regression guard.
- **L5** — singleton Karsten ceiling now uses the draw baseline
  (`SourcesNeeded(..., onPlay: !isSingleton)` in `SimRequiredSources`); 60-card path
  unchanged (test-proven both ways). Example: 99-card 1-pip MV4 ceiling 16 → 15.
- **L9** — `DetectGranter` catches Relic of Legends: entry gate accepts the
  "{T}, Tap …: Add" cost form (clause-anchored regex after review fix) and scope match
  accepts singular "legendary creature you control".
- **L13** — land-ramp library-thinning gap documented (docs §1.6 + `// Why:` comment);
  no behavior change by design.

**MBGAP-06 scry credit** (`0234fe53`), flag `analysis.manabase.scry-credit` (seed ON):
- Cheap (MV≤2) non-land spells with real "scry N" (reminder text stripped) add
  0.2 any-color source per copy to per-color effective counts — analyzer-only lane,
  sim/land-target untouched. Disclosure line on page + .txt
  (`ManabaseWording.ScrySourceCreditLine`). Draw+scry stacking with the −0.28 ramp/draw
  land credit is intentional (different Karsten mechanisms) and documented.
- Flag OFF byte-identical (parity test).

**MBGAP-08 + L6 colorless/snow** (`1ca3d2bb`), flag `analysis.manabase.colorless-snow`
(seed ON):
- `ManaCost` additively parses true-{C} and {S} pip counts (legacy Pips/enum untouched).
- `ManaSource` gains `IsSnow` (front-face type line) + `ProducesColorless`, stamped at
  classification.
- Castability sim mask bits 5 ({C} payable only by colorless producers) and 6 ({S}
  payable only by snow sources) on the flag-on path; flag-off keeps the legacy
  colorless-pip drop byte-identically (parity test).
- Analyzer emits Colorless/Snow requirement rows (Karsten "own color" treatment);
  rocks with category pips (Arcum's Astrolabe) can drive them (review fix).

**Review/verify fixes** (`cad2585c`): granter clause-anchored regex (cross-ability
overmatch), flag-off pip-floor parity in `EffectiveTurn` (verifier catch — {C}/{S}
pips joined the floor ungated), rock special-category drivers, scry credit excluded
from untapped numerators (was rendering "102% untapped"), real "N of M" special-category
denominators.

**Simplify pass** (`46f4dbbf`): 22 items — shared sim-required binary-search core,
shared probe-deck scaffolding, `EffectiveSources` predicate overload, scry constant
moved to `KarstenManabase` (credit-lane convention), capability stamping hoisted out of
quantity loops, `PipArray` allocation trims, shared wording/denominator/`IsSpecialCategory`
helpers, category identity promoted to `ManabaseModels` (killed cross-file magic
strings), dead guard removal (verified), test dedup. One behavior fix: MDFC snow
back-face no longer qualifies its front-face spell.

## Verification

- gpt-5.5 plan review (BLOCK → revise → APPROVE_WITH_CHANGES); its HIGH catch (L4 not
  dead) prevented a live regression.
- Suites green at every stage; final: Core 1459/1459, Web 1394 (+14 skipped, known).
  Build 0 warnings (1 known pre-existing CS8602 in Web.Tests build step).
- Playwright manabase specs 44 passed / 6 skipped (desktop + mobile).
- Live UI verified against worktree server: scry credit line, Colorless row (Warping
  Wail, weighted 1.8 sources), Snow row (Icehide Golem, 20 snow sources); no horizontal
  overflow at 1280px/390px; screenshots captured.
- Blind foreman-verifier: PASS_WITH_NOTES (its F1 parity catch fixed in `cad2585c`).
- EOL churn: none (all touched files LF, verified per commit).

## Operator actions owed

- Prod flag rows: `analysis.manabase.scry-credit` and `analysis.manabase.colorless-snow`
  seed ON for fresh DBs, but existing prod DB needs the two rows flipped ON (or
  inserted) after deploy.

## Deferred (recorded, not done)

- Full unification of `BuildColorFindings` per-spell loop with
  `AddSpecialCategoryFinding` (mirrored-logic comments mark the pairing).
- Flag threading → data-gating seam (zero special fields at entry instead of bool
  params through 8 signatures).
- Optional binding-ceiling strengthening of the 60-card L5 test.
- Backlog §3 is now fully closed (MBGAP-06/07/08/09/10 all done).
