# Phase: Manabase Research-Gap Closure - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-12
**Phase:** manabase-research-gap-closure
**Areas discussed:** Scope & tier cut, Conditional lands, Land cycles, Math & calibration

---

## Scope & tier cut

| Option | Description | Selected |
|--------|-------------|----------|
| Tier 1 + Tier 2 | Core accuracy (MBGAP-01/02/03/04) + verdict-polish batch (05a-d) | ✓ |
| Tier 1 only | Just the 4 accuracy gaps | |
| Everything (T1+T2+T3) | Also scry-0.2, low-curve guard, snow, LOW sweep | |
| T1+T2 + select T3 | Core + polish + picked Tier-3 items | |

**MBGAP-09 (cEDH castability surface):** Own later phase ✓ (vs Include here / Drop entirely)
**MBGAP-11/12 (help re-audit + lens visual verify):** Include as closing tasks ✓ (vs Skip)

---

## Conditional lands (MBGAP-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Composition-gated | Per-class rules via deck composition (check-land census pattern): Cavern/Unclaimed dominant-type share, Ziggurat creature share, Nykthos conditional low weight | ✓ |
| Flat discount weight | All restriction lands as partial any-color (e.g. 0.5) + disclosure | |
| Full spend-restriction sim | Per-spell spendability masks in Monte-Carlo | |

**Flag:** New flag `analysis.manabase.restricted-lands`, ship OFF ✓ (vs fold into accuracy bundle)
**Disclosure:** Row marker (alt-cost `1*` pattern) + existing panel ✓ (vs panel only)

---

## Land cycles (MBGAP-02)

| Option | Description | Selected |
|--------|-------------|----------|
| All six | fast, slow, ELD threshold, Verge, Vivid, Training Compound | ✓ |
| Turn-conditional core | fast/slow/ELD/Verge only | |
| Verge + fast only | Minimal scope | |

**Mechanism:** Per-trial sim evaluation for count-based conditions; static census for type-based (Verge) ✓ (vs static census for all)
**Flag:** Rides `analysis.manabase.accuracy` bundle ON ✓ (vs new flag OFF first) — matches bond/check/Snarl precedent

---

## Math & calibration (MBGAP-03 + MBGAP-04)

**Ritual land-target credit:**

| Option | Description | Selected |
|--------|-------------|----------|
| Calibration-fit weight | Start ~0.5/ritual capped, tune against 1597-deck harness | ✓ |
| Fixed Karsten-style weight | Hand-pick and ship | |
| Fold into fastMana term | Count rituals in existing fast-mana deduction | |

**Ritual flag:** New flag `analysis.manabase.ritual-land-credit`, ship OFF ✓ (vs reuse live `ritual-burst-mana`)

**Threshold (MBGAP-04):**

| Option | Description | Selected |
|--------|-------------|----------|
| Research spike first | Decision doc re-verifying Karsten 2022 + (85+M) multiplayer case; implement only if supported | ✓ |
| Implement now behind flag | Ship (85+M) OFF-flagged alongside spike | |
| Doc-fix only | Resolve contradiction, leave threshold untouched | |

---

## Deferred ideas raised

None new during discussion — deferred list carried from the gap-review CONTEXT (Tier 3, MBGAP-09, research-corpus deliberate exclusions).

## Claude's discretion

Vivid charge-counter modeling depth; verdict-polish exact copy; calibration acceptance bars (default to cEDH-land-target precedent).
