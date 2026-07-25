---
status: resolved
created: 2026-06-24
updated: 2026-07-05
---

# Implementation spec — manabase health-band headline floor (Approach 1)

Status: resolved — shipped (was: REVIEWED by Codex (gpt-5.5), BLOCK→folded. Ready to implement).
Flag-gated, default OFF. Cross-cutting (changes verdict label for every prod deck) — measure before/after.

## Problem

`DeckFlow.Core/Manabase/ManabaseModels.cs` `Health` getter (~:577) +
`ComputeColorSignals()` (~:641) derive the band ONLY from Karsten per-color
source deficits + land-count delta. They IGNORE the simulated headline
avg-on-curve %. A deck the sim says casts fine can be forced to the worst tier
by two soft signals stacking.

Repro: Brago WU control, 100 cards / 33 lands. White 24.6/26 (deficit ~1.4),
Blue OK, sim avg-on-curve 88%. Band = "Needs work" because
`sourceShort = Deficit > 1` fires on White (→ ColorsWithIssue=1) AND
`landShort = LandDelta <= -2` fires (33 vs ~36.2 target), tripping the
`landShort && ColorsWithIssue>=1 -> NeedsWork` branch. The 8 mana rocks cover
the land gap; the sim says 88%; the band can't hear either.

Prior session shipped "Approach 2" (per-color sim feeds ColorsWithIssue, flag
`manabase.health-band-castability`, default OFF) — can only DEMOTE. This spec is
the deferred "Approach 1" (headline floor) that PROMOTES a functional deck off
Needs-work. Both ON = "Approach 3".

## Change (folded with Codex review fixes)

### New flag
`manabase.health-band-headline-floor`, default OFF. Seed in
`DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (SQLite + PG), catalog
entry in `FeatureFlagCatalog.cs`, threaded through
`ManabaseAnalyzer.Analyze(...)` as a new bool parameter (mirror
`useHealthBandCastability`), surfaced as
`ManabaseReport.UseHealthBandHeadlineFloor`, wired in
`DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`.

### Core-derived fields (REQUIRED — they do NOT exist; do not call Web from Core)
- `AvgOnCurvePercent` — add to `ManabaseReport` (Core), derived from
  `Castability`. (`ManabaseDisplay.cs:105` has Web-only avg logic — replicate the
  same definition in Core; do not reference the Web helper from Core.)
- `WorstColorCastPercent` — derive as
  `ColorFindings.Count == 0 ? 100 : ColorFindings.Min(f => f.WorstSpellCastPercent)`.
  (`WorstSpellCastPercent` already on `ColorSourceFinding` ~:321.) Safe pass-value
  (100) when no color findings.

### REVISION 2 (post-implementation) — color-limited broad signal

The first implementation overfit (added `MaxColorDeficit<=3` + a `WorstColorCast<=60`
broad tolerance) because Brago trips the EXISTING `BroadUnderSupport` signal, which
counts ALL under-supported spells including MANA-limited curve cards (Deadeye MV6 @
57% is land-count, not color). Those curve cards must NOT block a promote.

Fix: add a SECOND, color-only broad signal `BroadColorUnderSupport` computed from
`ColorLimitedUnderSupportedCount > tolerance` (NOT total `UnderSupportedCount`). The
headline-floor promote gates on `!BroadColorUnderSupport`. The existing flag-OFF
NeedsWork branch keeps using the original total `BroadUnderSupport` — UNCHANGED, so
flag OFF is byte-identical.

REMOVE entirely from the promote predicate: `MaxColorDeficit<=3`, the
`WorstColorCast<=60` broad tolerance, and the modification to the severe-deficit
hard-fail line. `AnySevereColorDeficit` and `ColorsWithIssue>=2` stay ABSOLUTE
hard-fails the floor can never override.

```csharp
bool simFunctions =
       UseHealthBandHeadlineFloor
    && AvgOnCurvePercent >= 85
    && WorstColorCastPercent >= 50
    && !s.AnySevereColorDeficit
    && !s.BroadColorUnderSupport;     // color-limited broad only; curve cards ignored

if (s.AnySevereColorDeficit || s.ColorsWithIssue >= 2)   // RESTORE absolute hard-fail
    return ManabaseHealth.NeedsWork;

if (landShort && (s.ColorsWithIssue >= 1 || s.BroadUnderSupport))
    return (simFunctions && s.ColorsWithIssue == 1)
        ? ManabaseHealth.Workable
        : ManabaseHealth.NeedsWork;
```
`LandShortfallCoveredByRamp` mirrors with the same `simFunctions && ColorsWithIssue==1`.

If after this Brago STILL won't promote (e.g. its color-limited under-support also
exceeds tolerance, or worst-color < 50), STOP and report the measured numbers — do
NOT add another waiver. We decide the threshold from the measurement.

### (original) Health getter — narrowed promote

```csharp
bool simFunctions =
       UseHealthBandHeadlineFloor
    && AvgOnCurvePercent >= 85          // tunable A: headline floor
    && WorstColorCastPercent >= 50      // tunable B: no catastrophic color
    && !s.AnySevereColorDeficit         // no color short by >2 Karsten sources
    && !s.BroadUnderSupport;            // sim does NOT see a broad failure

// hard fail — unchanged. headline floor can NEVER override these,
// including a ColorsWithIssue raised by manabase.health-band-castability.
if (s.AnySevereColorDeficit || s.ColorsWithIssue >= 2)
    return ManabaseHealth.NeedsWork;

if (landShort && (s.ColorsWithIssue >= 1 || s.BroadUnderSupport))
    return (simFunctions && s.ColorsWithIssue == 1)   // narrow: exactly one soft color issue
        ? ManabaseHealth.Workable                      // NEW promote
        : ManabaseHealth.NeedsWork;
```

### LandShortfallCoveredByRamp — keep coupled (same narrowed signal)

```csharp
return (s.ColorsWithIssue == 0 && !s.BroadUnderSupport && !s.AnySevereColorDeficit)
       || (simFunctions && s.ColorsWithIssue == 1);
```

`PrimaryFix` already skips land advice when `LandShortfallCoveredByRamp` is true
(~:839); raw color deficits still win first (~:812-831), which is correct for
Brago (it should still say "white is short"). No change to PrimaryFix needed —
verify it stays consistent in tests.

## Tunables
- A = avg floor 85 (backstop). B = worst-color floor 50 (discriminator).
- B is the sensitive one (Brago ~53 vs graveyard ~47, 6pt gap). The narrowing
  gates (`!BroadUnderSupport`, `ColorsWithIssue == 1`, `!AnySevereColorDeficit`)
  are the real safety — B is secondary, not sole separator.

## REQUIRED before locking B: confirm Brago's live number
Run `ManabaseHealthBandBaselineHarness` (or the existing flag-baseline harness)
on the Brago list and PRINT Brago's actual `WorstColorCastPercent` +
`AvgOnCurvePercent`. If WorstColor < 50, lower B to ~45 (graveyard 47 is already
Solid and unaffected, so B is safe down to ~40). Do not guess — measure.

## Projection (must hold in regression guard)

| Deck             | Avg | WorstColor% | Now        | New (flag ON) |
|------------------|-----|-------------|------------|---------------|
| Brago WU         | 88  | 53          | Needs work | Workable ✅    |
| Marchesa         | 85  | 28          | Needs work | Needs work    |
| army now         | 85  | 37          | Needs work | Needs work    |
| Necrobloom       | 79  | 37          | Needs work | Needs work    |
| Meren            | 94  | 71          | Solid      | Solid         |
| Avatar           | 94  | 73          | Solid      | Solid         |
| graveyard fungus | 89  | 47          | Solid      | Solid         |
| Townos           | 96  | —           | Excellent  | Excellent     |
| Kenrith 5c       | 99  | —           | Excellent  | Excellent     |

Only Brago flips. Flag OFF = byte-identical to today for all 9.

## Tests (REQUIRED)
1. Permanent CI regression (`DeckFlow.Web.Tests/Manabase/ManabaseHealthBandRegressionTests.cs`):
   - Brago flag OFF → Needs work; flag ON → Workable.
   - All 8 other decks: flag OFF == flag ON (no regression).
2. Both-flags-ON (Approach 3): a deck where `health-band-castability` raises
   ColorsWithIssue — confirm headline floor does NOT promote it past the
   `>=2` / severe / broad-under-support hard fails.
3. A broad-under-support deck (BroadUnderSupport true) under flag ON stays
   NeedsWork — proves HIGH-1 fix.
4. Coupling: when Brago promotes to Workable, assert
   `LandShortfallCoveredByRamp == true` and `PrimaryFix` does not contradict
   (no "add N lands" if ramp covers, but white-short color advice may remain).

## Definition of done
Core + Web build clean, no new warnings. Full Core + Web suites green
(11 PG-skip expected). Flag OFF inert. New flag seeded both providers.
README/help untouched (flag default OFF, no behavior change shipped).

## Resolution (closed 2026-07-05)

Implemented and shipped as `bd26ac4b feat(manabase): add headline-floor health band`.
The headline-floor promote logic (`simFunctions` gated on `AvgOnCurvePercent`,
`WorstColorCastPercent`, `!AnySevereColorDeficit`, `!BroadColorUnderSupport`) landed
per this spec, together with the sibling Approach 2 castability coupling
(`d6a1b4be feat(manabase): flag-gate health-band castability coupling (Gate C)`) and
its regression guard (`54c155ff test(manabase): Avatar health-band regression guard
for Gate C`). The flag key was later namespaced to
`analysis.manabase.health-band-headline-floor` under quick task `260627-flag-key-namespacing`
(`2d8b1a7b`). Part of the Cycle 12 manabase verdict overhaul; not a Cycle 15 item.
Marked resolved.
