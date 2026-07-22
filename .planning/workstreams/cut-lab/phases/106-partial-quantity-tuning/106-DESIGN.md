# Phase 106 Design — Partial-Quantity Tuning & Add Basics

**Status:** Design approved via brainstorming 2026-07-22. Feeds `/gsd-plan-phase 106`.
**Requirements:** EDIT-01, EDIT-02, EDIT-03.
**Scope decisions (user-approved):** copies + new *basics* only; Approach B; inline UI; reopen Cycle 18.

## Problem

Cut Lab decisions are name-keyed with no per-copy quantity, so `CutLabWorkingList.Derive`
removes a **whole** pool entry on accept. A multi-copy entry (e.g. `35 Island`) is
all-or-nothing. Phase 105 shipped the "Option A" overshoot filter (the engine won't
propose an entry whose full quantity exceeds the remaining budget), which keeps the guided
cut correct but means **basics can't be trimmed a few copies at a time near 100**, and there
is no way to *add* copies or add a basic to reach exactly 100. Phase 106 adds copy-level
tuning without rewriting the guided-cut engine.

## Approach B — quantity-adjustment layer (chosen over a decision-model rewrite)

Leave `Decisions` (whole-entry, name-keyed) and the Option-A engine **untouched**. Add a
separate, additive adjustment layer and an inline tuner.

### Data model
- `CutLabState.QuantityAdjustments: IReadOnlyList<CutLabQuantityAdjustment>` where each entry
  is `{ Name, Delta (signed int), IsAddedBasic (bool) }`.
  - `Delta` = net copies to add (+) or remove (−) for `Name`, on top of the whole-entry
    decision result.
  - `IsAddedBasic` marks a basic land that was **not** in the imported pool (so `Derive` can
    materialize it from the basics constants table).
- Serializer: bound the collection (mirror `MaxDecisions`), clamp per-entry `Delta` to a sane
  range, and keep an empty-initializer for back-compat with pre-106 JSON blobs.

### Derivation
`CutLabWorkingList.Derive(pool, decisions, adjustments)` (new overload; old overload delegates
with empty adjustments):
1. Apply whole-entry `Decisions` exactly as today (Option-A engine unaffected).
2. Fold `QuantityAdjustments` by name onto the result:
   - Existing entry: `qty = clamp(qty + delta, 0, legalMax)`; drop entries that reach 0.
   - `IsAddedBasic` name with no entry: create a `CutLabPoolCard` land entry from the basics
     constants (type line, color identity, land role) with `qty = max(delta, 0)`.
- **Single source of truth:** every consumer (analysis/roles/floors, simulation, count,
  export composer) reads this derived list, so counts and metrics stay consistent.

### Basics constants (no Scryfall)
Known table: Plains/Island/Swamp/Mountain/Forest (W/U/B/R/G identity), their Snow-Covered
variants, and Wastes (colorless). Each maps to `{ typeLine: "Basic Land — X", colorIdentity,
isLand: true }`. Added basics flow through structural analysis (as lands) and simulation
(mana/flood) with no lookup. Copy-deltas on non-basic legal-multiple cards apply only to cards
already resolved in the pool.

### Legality (EDIT-03)
Quantity > 1 is allowed only for basics and the recognized any-number cards: Persistent
Petitioners, Dragon's Approach, Relentless Rats, Rat Colony, Shadowborn Apostle, Slime Against
Humanity, Templar Knights, Nazgûl, Seven Dwarves. All other cards cap at 1 (no + stepper).
Enforce server-side in the adjustment endpoint and reflect in the UI (disable + at cap).

### UI (inline in the Decide workspace)
- `+`/`−` steppers on each basic / legal-multiple working-list row; disabled at legal bounds
  (min 0, singleton max where applicable).
- An "add basic land" control (the known basics) that creates/increments an added-basic
  adjustment.
- Posts to a new adjustment action (JSON + no-JS form, mirroring the decide/goals pattern);
  the sticky remaining-to-100 count and the export validation update. No new step tab.
- Progressive enhancement: JS patches counts; no-JS full-page re-render (same contract as
  decide/goals/what-if).

### Count gate
With adds, the working list can now be brought to exactly 100 by tuning counts, not only by
whole-entry cuts. The "reach 100" gate and the Export tab enablement (Phase 105 wire) key off
the derived count, so they work unchanged.

### Interactions to verify
- **What-if / goals / scenarios (P104):** adjustments must serialize into `CutLabState` and
  survive scenario save/reload; what-if preview/keep and goal recompute run on the
  adjustment-derived list.
- **Restore:** restoring a whole-entry cut composes with adjustments deterministically
  (apply decisions → then adjustments).
- **Export (P105):** no changes — `DiffEngine.Compare(derivedFinal, originalEntries)` already
  emits quantity deltas, so "add 2 Island / cut 3 Island" appears in the CUT/ADD patch in both
  dialects. Added basics not in the original list appear as ADD.

## Out of scope (deferred — see REQUIREMENTS Future)
- Adding arbitrary **new nonbasic** cards (needs Scryfall name-resolution + full
  role/floor/sim/color/banlist re-analysis).
- **Undersized-pool intake** (paste/URL under 101 cards and build up) — changes the INTAKE
  101–150 oversized premise.

## Test targets (for planning)
- `Derive` overload: adjustments fold, clamp-to-zero drops entry, added-basic materialization,
  compose-with-decisions order.
- Legality: + disabled/rejected for singletons; allowed for basics + any-number list.
- Serializer round-trip + bounds/clamp; back-compat with pre-106 blobs.
- Endpoint (JSON + no-JS): apply adjustment, count updates, reach exactly 100.
- Export patch reflects add/cut copies + added basics (both dialects).
- e2e: import → cut to near-100 → trim/add basics to exactly 100 → export shows the tuned
  counts; scenario reload preserves adjustments; theme × viewport screenshots.
