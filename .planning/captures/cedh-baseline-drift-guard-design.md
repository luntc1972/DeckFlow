# cEDH Baseline Drift Guard — Design

**Date:** 2026-07-27
**Status:** Approved, not implemented
**Scope:** `scripts/cedh-baseline/fetch.py`, `DeckFlow.Core/Manabase/`, `DeckFlow.CLI` `cedh-land-baseline`, `DeckFlow.Web.Tests/Manabase/CedhLandBaselineProviderTests.cs`

## Problem

The 2026-07-27 monthly refresh produced a corrupt snapshot and reported success. The pipeline
exited 0 and printed only `Warning: Scryfall could not resolve N names in this batch.` lines that
read as routine.

Two `fetch.py` defects were the cause (both fixed in commits `ce081c9b`, `37d6a8ab`):

1. The DFC front-face retry fired unthrottled, crashing the run with HTTP 429.
2. Scryfall's `/cards/collection` rejects the full `"A // B"` name of a double-faced card, so the
   resolver retries with the front face. That query succeeds, but the response echoes the **full**
   name in `card["name"]` while the retry bookkeeping looked the result up under the front face.
   Every successfully fetched DFC was discarded as unresolved.

Damage: 208 distinct names / 7,568 card instances dropped, heavily weighted toward modal-DFC
**lands**. Result was ~1.9 lands per deck under-counted and commanders whose own card is a DFC
mis-keyed (`Ral, Monsoon Mage` fell from 105 decks to 7, its mean from 21.6 to 17.9).

Nothing detected this. The corruption was caught only by a manual diff of the regenerated
`latest.json` against the committed one. The builder's own tests passed, because the builder
correctly processed the bad input it was given.

## Goals

- Fail closed on corrupt data rather than writing it.
- Work unattended (non-zero exit), even though a human runs it today.
- Catch the known failure class at its source, and unknown classes by their statistical shape.
- Keep the last-known-good artifacts intact when a refresh fails.

## Non-goals

- Detecting genuine metagame shifts. A real meta change that trips the guard is expected; the
  operator retunes the thresholds and commits that decision.
- Validating the pipeline's network layer, retry policy, or throttling. Already fixed.
- Any change to how `CedhLandBaselineProvider` serves requests at runtime.

## Architecture

Two independent gates at different stages, each failing closed.

```
fetch.py ──[GATE 1: zero unresolved names]──> _calib/
                                                 │
DeckFlow.CLI cedh-land-baseline                  ▼
   build candidate snapshot from _calib
   load previous latest.json
   CedhBaselineDriftCheck.Evaluate(prev, candidate, thresholds)
        │                    │
      FAIL                 PASS
        │                    │
   exit 1, write        write YYYY-MM.{json,md} + latest.json
   NOTHING
```

Ordering is load-bearing: **the drift check runs before any file is written.** The 2026-07-27
corrupt run overwrote the committed artifacts, which then had to be recovered from git. Failing
before the write leaves the last-known-good snapshot in place.

## Gate 1 — unresolved-card check (`fetch.py`)

After the card cache is persisted, diff the distinct card names across all decks against the
resolved cache. If any name is unresolved:

- Print the unresolved names (first 20, plus the total count) to stderr.
- `return 1`.

Zero tolerance, no allowlist. The corrected 2026-07 run resolved 6717/6717, so zero is achievable.
If a genuinely unresolvable name appears later (a token, a joke card, an EDHTop16 typo), the
operator handles it then; an allowlist is deferred until a real case exists.

The cache is written **before** the failure so the run stays resumable — the expensive Scryfall
work is not thrown away.

## Gate 2 — drift check (`DeckFlow.Core` + CLI)

### `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`

```
Evaluate(previous, candidate, thresholds) -> DriftVerdict { bool Passed, IReadOnlyList<DriftFinding> Findings }
```

Pure function. No I/O, no network, no clock. Unit-testable with synthetic snapshots in the same
style as the existing `CedhLandBaselineTests`. Lives in Core per the project rule that pure domain
logic carries no framework dependencies.

`DriftFinding` carries the rule name, the commander (where applicable), the observed value, and the
threshold it breached, so the CLI can print an actionable failure rather than a bare boolean.

### Rules

| Rule | Fires when |
|---|---|
| `DroppedEstablishedCommander` | a commander with previous `n >= minEstablishedN` is absent from the candidate |
| `SampleCollapse` | a commander with previous `n >= minPopulousN` loses more than `maxSampleDropPct` of its sample |
| `OneSidedDrift` | at least `minMoversForDirectionTest` commanders move `>= moverThresholdLands`, and at least `maxOneSidedPct` of them move in the same direction |

`OneSidedDrift` is the subtlest and the most general. Meta drift scatters; systematic corruption
pushes one way. The minimum-mover count keeps the rule inert on quiet months, where a handful of
movers could be one-sided by chance.

### Thresholds file

`scripts/cedh-baseline/drift-thresholds.json`, committed. Placed beside the runbook rather than in
`DeckFlow.Web/Data/` because it is pipeline configuration, not a runtime web asset, and
`DeckFlow.Web/Data/` ships to production.

```json
{
  "minEstablishedN": 10,
  "minPopulousN": 20,
  "maxSampleDropPct": 40,
  "moverThresholdLands": 0.5,
  "minMoversForDirectionTest": 10,
  "maxOneSidedPct": 90
}
```

Overriding a legitimate trip means editing and committing this file, so the new normal is reviewed
in a diff rather than living in shell history.

### Threshold evidence

Calibrated against the real incident. Both snapshots are from the same 2026-07 refresh, so the
comparison isolates the corruption from meta drift.

| Signal | Good run (corrected) | Corrupt run | Threshold | Margin |
|---|---|---|---|---|
| Worst per-commander sample drop (prev n>=20) | −9.5% | −93.3% | fail above 40% | ~4x over good, ~2.3x under bad |
| Movers >=0.5 lands | 4 (1 up / 3 down) | 42 (0 up / 42 down) | fail at >=10 movers and >=90% one-sided | rule inert on good run |
| Commanders dropped with prev n>=10 | 0 | 1 (`The Cabbage Merchant`, n=18) | fail on any | absolute |

Each rule independently catches the corrupt run and clears the good run. The redundancy is
deliberate — one rule drifting out of calibration should not open the gate.

Two candidate signals were **rejected** for weak separation: overall mean shift (−0.2 good vs −0.4
corrupt) and total sample size (+6.4% good vs −2.5% corrupt). Both would mostly generate
threshold-tuning noise.

## Error handling

| Condition | Behavior | Rationale |
|---|---|---|
| No previous `latest.json` | Skip drift check, print a note, proceed | Bootstrap / first run is legitimate |
| Malformed previous `latest.json` | Fail hard | Cannot verify, so do not write |
| Missing or malformed thresholds file | Fail hard | See below |

Rejecting a fallback to built-in defaults is deliberate. A typo in the config would otherwise
silently disable the gate, which is precisely the failure mode this feature exists to prevent.

This is the opposite of `CedhLandBaselineProvider`'s runtime fail-open behavior, and correctly so:
fail-open is right when serving a user request (a missing baseline should degrade the analysis, not
500 the page); fail-closed is right when writing data that everything downstream trusts.

## Folded in: re-scope the pinned provider tests

`CedhLandBaselineProviderTests` pins exact sample counts (`n`) for Kinnan, Plagon, and
Rograkh/Thrasios. These fail on **every** refresh by construction — the 2026-07-27 run had to
update 327→337 and 241→255 — while carrying no correctness signal, because sample counts move
whenever the 6-month window slides.

With Gate 2 covering sample-population sanity far more thoroughly, the exact-`n` pins are redundant
churn. Re-scope them to:

- Keep the **mean** and **sd** pins with their existing tolerance. These carry real signal — a large
  swing indicates corrupt data, which is exactly what would have caught this incident.
- Replace exact `n` equality with a **floor** set at roughly 60% of the 2026-07 value, mirroring
  the 40% `maxSampleDropPct` collapse threshold: Kinnan `n >= 200` (from 337), Rograkh/Thrasios
  `n >= 150` (from 255). This preserves the tripwire intent — a commander must not fall out of
  usable sample size — without failing on ordinary window slide.
- Keep Plagon's `n >= 10` assertion and its comment untouched. It already has the right shape and
  is still satisfied at n=23.

## Testing

- **Per-rule unit tests** in `DeckFlow.Core.Tests/Manabase/`: each rule fires, does not fire, and
  behaves correctly at the exact threshold boundary. Synthetic snapshots, no I/O.
- **Regression fixture from the real incident.** Commit two trimmed snapshots — the 2026-07-11
  previous and the corrupt 2026-07-27 candidate — and assert the guard rejects that pair and
  accepts the corrected pair.
- **Gate 1** is Python and the repo has no Python test framework; do not introduce one. Verify by
  targeted manual run, as was done for the two fixes it accompanies.

The regression fixture is the highest-value item. Threshold guards rot because nobody remembers
what the numbers were calibrated against; six months on, someone widens `maxSampleDropPct` to
unblock a run and quietly destroys the gate. Pinning the real incident means such an edit fails a
test that says, in effect, *this change would have let the July 2026 corruption through.* The
thresholds stop being arbitrary constants and become a claim about a specific known failure.

CI already runs `dotnet test DeckFlow.sln`, so Gate 2 is covered automatically with no workflow
change.

## Out of scope

- Allowlist for genuinely unresolvable card names (deferred until a real case exists).
- Standalone `cedh-baseline-verify` command. The same Core class can be exposed through a second
  command later if standalone snapshot comparison is ever wanted.
- Overall-mean and total-sample-size drift signals (rejected above).
