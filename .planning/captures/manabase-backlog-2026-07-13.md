# Manabase backlog — post gap-closure phase (captured 2026-07-13)

Status snapshot: phase `manabase-research-gap-closure` plans 01-10 ALL SHIPPED to prod
(`61595280` live 2026-07-13 03:15). Both new flags seeded OFF in prod DB.
This doc is the continuation list, ordered by recommended pickup.

---

## 1. Flag flips (operator action, near-term)

| Flag | Prod | Gate before flip | Notes |
|------|------|------------------|-------|
| `analysis.manabase.ritual-land-credit` | OFF | none left — calibration DONE + user accepted 0.5/cap 3.0 (2026-07-12 checkpoint) | Evidence committed: `.planning/phases/manabase-research-gap-closure/05-calibration-{before,after}.md` (3281 decks, under-flag 21.8%→11.1%, 0 newly-flagged, floor-22 holds). Flip = 1 prod UPDATE via /Admin/Flags or SQL. |
| `analysis.manabase.restricted-lands` | OFF | **golden-deck diff required (D-04)** — run a before/after on a deck set containing Cavern/Unclaimed/Ziggurat/Nykthos, review weight deltas + disclosure rendering, then flip | Classifier gated by `restrictedLands` param; flag-off byte-identical proven by parity tests. |

## 2. MBGAP-09 — cEDH castability surface (own phase, per locked D-02)

Early-interaction turns-1-3 color-access lens for cEDH mode. Deliberately excluded
from the gap phase. Context anchor: `.planning/phases/manabase-research-gap-closure/CONTEXT.md`
(D-02 + deferred section). Needs its own discuss-phase → plan → execute cycle.

## 3. Tier-3 research minors (small, batchable into one plan)

From the 2026-07-12 research-vs-implementation audit (CONTEXT.md deferred section):
- **MBGAP-06** — scry-0.2 source credit
- **MBGAP-07** — casual low-curve guard (verify with a real ~1.8-MV deck FIRST — may be a non-issue)
- **MBGAP-08** — snow color category
- **MBGAP-10** — LOW sweep: L4 (verify-then-fix), L5, L6, L9, L13

## 4. UX research LOW items (page polish, one small plan)

From `.planning/ui-design/manabase-ux-research.md` (HIGH/MED all shipped in plan 10):
- **LOW-8** — fold Ramp/draw advisory + Command-zone castability into the lens-card visual system
- **LOW-9** — pair headline sim percentages with distribution shape (keep-size pattern extended to cast-rate)
- **LOW-10** — "condensed view" toggle (Archidekt precedent) — ONLY if length complaints persist post-cap

## 5. Refactor follow-ups (deferred by /simplify passes, quality-only)

From `.foreman/ledger.md` Runs 3-4:
- **Table-driven `ClassifySpecialLand`**: replace the ~8-branch if-chain with an ordered
  `SpecialLandRule[]` (regex + builder delegate), mirroring `ConditionalTypeTemplates`.
  Every future land family becomes one array entry. (`ManabaseClassifier.cs`)
- **`CedhCalibration` TargetColumn generalization**: Old/New/RitualCredit triplication →
  `IReadOnlyList<TargetColumn>` + shared `ComputeVariantStats`; a 4th target variant is
  near-certain. ALSO: its ~90 lines of aggregation math (means/percents/un-flag deltas)
  have NO unit tests — surfaced and accepted 2026-07-12; add tests when touched.
- **`AddLandCopies` context record**: 11-param signature (6 shared context + 2 out-collections)
  → `LandClassificationContext` readonly record struct per project >3-param guidance.

## 6. Research-corpus deliberate exclusions (unscheduled; revisit only on demand)

X-spells (Salubrious X=3/2/1), landcycling, "+1 Wastes vs +1 Basic" perturbation diagnostic,
cost-cheater toggle, Treasure stockpiling / sac-outlet engines, Chrome Mox / Spirit Guide
imprint-exile producers, commander recast tax, rocks-removability toggle.

## 7. Bookkeeping

- Phase archive/closeout: 10/10 SUMMARYs + VERIFICATION.md (passed, plans 1-9; plan 10
  human-approved after) — fold into next milestone archive sweep.
- ROADMAP backlog bullet "Manabase research-gap closure" is now DONE — pruned in the same
  commit as this capture.
- Manabase engine SRP refactor: still parked (needs parity harness first) — pre-existing
  backlog, unchanged by this phase.

---

*Session artifacts: `.foreman/ledger.md` (full run history), Codex threads
`019f57b8-24da` (plan review) / `019f57cb-7cce` (execution) / `019f595e-2748` (simplify+fixes).*
