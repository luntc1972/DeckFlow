---
slug: manabase-health-band-coupling
status: resolved
trigger: "Manabase health band (Excellent/Solid/Workable/Needs work) ignores the castability headline — it derives only from raw per-color source counts, so the label can read 'Solid' while the sim numbers say otherwise. Couple the band to castability so the verdict word tracks the numbers."
created: 2026-06-24
updated: 2026-06-24
---

# Debug: couple manabase health band to castability (deferred 6th defect)

## Background

Follow-up to resolved session `manabase-too-optimistic` (ramp deploy-friction fix, commits 35024d9a / 2a0cf359 / 5a6aa78c). That session fixed the **numbers** (Avatar deck 96%→94%, weakest color Blue→White matching Salubrious Snail) but explicitly DEFERRED the **label** problem, scoped here.

## Symptoms

- **Expected:** The health band label should track the actual castability sim. A deck that casts its spells late / has a weak color should not read "Solid"/"Excellent".
- **Actual:** Band derives ONLY from raw per-color source math (Karsten `Deficit > 1` + color-limited under-support), INDEPENDENT of the headline avg-on-curve %. So a deck can read "Solid" at a low headline. Avatar deck post-numbers-fix: headline 94%, White weakest, but band falls through to **Functional ("Solid")** because no single color crosses the `Deficit>1` bar.
- **Errors:** None — misleading label, not a crash.
- **Reproduction:** Analyze the Avatar fixture (`.planning/debug/manabase-too-optimistic-deck.txt`); observe band = Solid despite White being the clear weakest color.

## Current band logic (mapped in prior session)

`ManabaseModels.cs`:
- `Health` getter (574-606): NeedsWork = severe color deficit OR 2+ colors with issue OR land-short+color-issue; Workable = exactly 1 color with issue; Healthy = land-adequate + every color clear; **Functional ("Solid") = fallback**.
- `ComputeColorSignals()` (638+): drives the above from `ColorSourceFinding.Deficit` and `ColorLimitedUnderSupportedCount` — NOT from headline/per-spell CastPercent.
- Display labels via `HealthLabel` (locate — not in Core/ManabaseModels; likely a display helper).
- `LandShortfallCoveredByRamp` + `PrimaryFix` ALSO consume `ComputeColorSignals` so verdict + land-advice ("add N lands") never contradict. **Any coupling change must preserve that consistency.**

## Candidate approaches (evaluate empirically, then checkpoint)

1. **Headline-% floor caps** — band can't exceed a tier if avg-on-curve is below a threshold (e.g. <90% can't be Excellent, <85% can't be Solid).
2. **Per-color cast-rate as a color-issue signal** — count a color as "an issue" when its demanding cards cast late in the sim, even if raw source count rounds to OK. (Feeds the existing `ColorsWithIssue` path → naturally tips Functional→Workable.)
3. **Both** — floor + per-color cast signal.

## Constraints / risks

- Cross-cutting: changes the verdict LABEL for EVERY deck in prod. Must NOT regress genuinely-good decks into false "Needs work".
- Must keep `Health` / `LandShortfallCoveredByRamp` / `PrimaryFix` consistent (shared `ComputeColorSignals`).
- Consider flag-gating (mirror `land-ramp-sim`) so it's a safe toggle — DECISION to surface at checkpoint.
- Calibration set: `.planning/phases/70-manabase-accuracy-mana-quantity/snail-decklists/`, `archidekt-baseline-decks.json`, and the Avatar fixture. Existing harnesses: `DeckFlow.Web.Tests/Manabase/ManabaseFlagBaselineHarness.cs`.

## Current Focus

- hypothesis: Approach 2 (per-color cast-rate feeding `ColorsWithIssue`) most naturally tips Avatar Functional→Workable without a blunt headline cap, and reuses the existing color-issue plumbing so land-advice stays consistent. A headline floor (approach 1) may still be wanted as a backstop.
- test: Measure the band distribution across the full calibration set BEFORE any change (baseline), then after each candidate, to ensure good decks don't regress and Avatar tips to Workable.
- expecting: Avatar → Workable (White issue); known-good decks stay Solid/Excellent; no deck flips to a false Needs-work.
- next_action: RESOLVED — Gate C applied, 9-deck guard passed, commits made.
- reasoning_checkpoint:
    hypothesis: "Per-color WorstSpellCastPercent < CasualSupportThreshold(80) on composite-weakest color feeds ColorsWithIssue when useHealthBandCastability=true"
    confirming_evidence:
      - "Avatar/White WorstSpellCastPercent=73 < 80 → fires as intended, band Solid→Workable"
      - "Meren/Green WorstSpellCastPercent=71 < 80 → fires as regression, band Solid→Workable"
      - "graveyard-fungus/Green WorstSpellCastPercent=47 < 80 → fires as regression, band Solid→Workable"
    falsification_test: "If a deck whose Green worst-spell% is above threshold stays Solid, approach is sound — only the threshold value is wrong"
    fix_rationale: "Approach 2 is correct; CasualSupportThreshold=80 is too coarse. Need a tighter threshold or a different condition."
    blind_spots: "WorstSpellCastPercent is a per-color minimum — a single extreme outlier card can drag it below 80 even when the color is generally well-supported"
- tdd_checkpoint:

## Evidence

- timestamp: 2026-06-24
  checked: Full 9-deck calibration set with useHealthBandCastability=true, CasualSupportThreshold=80, all other flags OFF
  found: |
    | Deck                                | Avg % | Weakest | WorstSpell% | Flag OFF | Flag ON    |
    |-------------------------------------|-------|---------|-------------|----------|------------|
    | Brago (WU control)                  |  85   | Blue    |  53         | Needs work | Needs work |
    | Kenrith 5c rocks                    |  99   | —       |   0         | Excellent  | Excellent  |
    | Meren Golgari ramp/ritual           |  94   | Green   |  71         | Solid      | Workable   | ← REGRESSION
    | Avatar — Sokka/Aang (Jeskai)        |  94   | White   |  73         | Solid      | Workable   | ← INTENDED
    | Archidekt 23563520 — Marchesa       |  85   | Black   |  28         | Needs work | Needs work |
    | Archidekt 23753514 — graveyard fungus|  89  | Green   |  47         | Solid      | Workable   | ← REGRESSION
    | Archidekt 23638601 — Townos         |  96   | —       |   0         | Excellent  | Excellent  |
    | Archidekt 8066726  — Necrobloom     |  79   | White   |  37         | Needs work | Needs work |
    | Archidekt 7084567  — army now       |  85   | White   |  37         | Needs work | Needs work |
  implication: |
    REGRESSION GUARD TRIPPED. The 80% threshold is too permissive:
    - Meren/Green worst-spell=71% < 80 → incorrectly tips to Workable
    - graveyard-fungus/Green worst-spell=47% < 80 → incorrectly tips to Workable
    Root insight: WorstSpellCastPercent is a per-color MINIMUM — a single outlier card can drag
    a well-supported color below 80. Avatar/White at 73% is the SAME threshold as Meren/Green at
    71% — these decks are not distinguishable by a simple worst-spell floor at 80.
    The three flagged-to-watch Solid decks (Meren 94%, graveyard-fungus 89%, Avatar 94%) all have
    their weakest-color worst-spell BELOW 80%, so the flat threshold cannot separate them.
    IMPLEMENTATION IS PAUSED — the Core/Web/flag infrastructure is in place (uncommitted), but
    the threshold logic in ComputeColorSignals needs a user decision before proceeding.



- timestamp: 2026-06-24
  checked: HealthLabel location
  found: DeckFlow.Web/Models/ManabaseDisplay.cs — static HealthLabel(ManabaseHealth) method; maps Healthy→"Excellent", Functional→"Solid", Workable→"Workable", _→"Needs work"
  implication: Label is in Web layer, not Core. Test project has ProjectReference to DeckFlow.Web so ManabaseDisplay is accessible.

- timestamp: 2026-06-24
  checked: Health getter + ComputeColorSignals in ManabaseModels.cs (lines 574-681)
  found: Health is derived ENTIRELY from ColorSourceFinding.Deficit and ColorLimitedUnderSupportedCount — zero reference to Castability rows or per-card CastPercent. ColorsWithIssue increments when sourceShort (Deficit>1) OR colorStarved (ColorLimitedUnderSupportedCount > tolerance). Neither criterion fires on Avatar/White because: (a) White's raw Deficit is ≤1 (sources round to sufficient per Karsten), (b) ColorLimitedUnderSupportedCount for White is within tolerance (White spells just barely squeak through). Result: band stays Functional ("Solid") despite White being sim-weakest.
  implication: Confirms root cause. The sim sees White as the weakest color but the band logic doesn't count it as "an issue" because neither Karsten deficit nor color-starved threshold fires.

- timestamp: 2026-06-24
  checked: Calibration deck inventory
  found: Fact caches exist for 8 decks: Brago (WU), Kenrith 5c, Golgari Meren, and 5 Archidekt decks (23563520 Marchesa, 23753514 graveyard fungus, 23638601 Townos, 8066726 Necrobloom, 7084567 army). Avatar fixture at .planning/debug/manabase-too-optimistic-deck.txt has NO cache — harness will resolve via Scryfall and write .manabase-avatar-facts.json.
  implication: 9 decks total in BEFORE baseline (8 cached + Avatar via Scryfall on first run).

- timestamp: 2026-06-24
  checked: ManabaseAnalyzer.Analyze signature
  found: Analyze(deck, mode, importance, costOverrides, useManaQuantity, colorAwareMulligan, gateRampOnCastable). All flags default false. Baseline uses all defaults (Casual, Standard, all flags off) to measure current health band.
  implication: Harness can call Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard) with no extra flags for the BEFORE baseline.

- timestamp: 2026-06-24
  checked: BEFORE BASELINE RUN — ManabaseHealthBandBaselineHarness (all 9 decks, Casual, all flags OFF)
  found: |
    | Deck                                              | Avg % | Weakest | Band       |
    |---------------------------------------------------|-------|---------|------------|
    | Brago (WU control)                                |  85   | Blue    | Needs work |
    | Kenrith 5c rocks                                  |  99   | —       | Excellent  |
    | Meren Golgari ramp/ritual                         |  94   | Green   | Solid      |
    | Avatar — Sokka/Aang (Jeskai)                      |  94   | White   | Solid      |  ← DEFECT
    | Archidekt 23563520 — Marchesa Value               |  85   | Black   | Needs work |
    | Archidekt 23753514 — graveyard fungus             |  89   | Green   | Solid      |
    | Archidekt 23638601 — Townos                       |  96   | —       | Excellent  |
    | Archidekt 8066726  — The Necrobloom               |  79   | White   | Needs work |
    | Archidekt 7084567  — army now                     |  85   | White   | Needs work |

    Avatar per-color detail:
    | Color | ActualSrc | RequiredSrc | Deficit | ColorLimitedUnderSupp | SpellCount | Tolerance |
    |-------|-----------|-------------|---------|----------------------|------------|-----------|
    | White |  32.3     | 24          | -8.30   | 1                    | 17         | 3         |
    | Blue  |  33.1     | 24          | -9.10   | 0                    | 27         | 5         |
    | Red   |  33.3     | 18          | -15.30  | 0                    | 15         | 3         |

    Avatar castability worst-10:
    | Card                                    | MV | Turn | Cast% | Limiting    |
    |-----------------------------------------|----|------|-------|-------------|
    | Suki, Courageous Rescuer                |  3 |    2 |   73  | color:White |
    | Echocasting Symposium                   |  6 |    6 |   78  | mana        |
    | Sink into Stupor                        |  3 |    3 |   88  | color:Blue  |
    | Avatar's Wrath                          |  4 |    4 |   90  | both        |
    | The Legend of Kuruk                     |  4 |    4 |   90  | both        |
    | Aang, Swift Savior                      |  3 |    2 |   91  | color:White |
    | Katara's Reversal                       |  4 |    4 |   91  | both        |
    | Boros Charm                             |  2 |    2 |   92  | color:Red   |
    | Lyse Hext                              |  3 |    2 |   92  | color:White |
    | Bria, Riptide Rogue                     |  4 |    2 |   93  | color:Blue  |
  implication: |
    ROOT CAUSE CONFIRMED numerically. Avatar White: Deficit = -8.30 (NEGATIVE = surplus, not short),
    ColorLimitedUnderSupportedCount = 1, Tolerance = 3. Neither sourceShort (Deficit>1) nor
    colorStarved (1 > 3) fires → colorsWithIssue stays 0 → band falls through to Functional/Solid.
    But the sim shows 3 White cards below 93% with limiting factor color:White, and weakest=White.
    The disconnect: the Karsten source count is generous (32.3 vs 24 required = 8 surplus) so the
    band logic concludes White is fine, while the sim's actual cast outcomes show White is the
    tightest color in practice.

    KEY OBSERVATION ON OTHER DECKS:
    - Brago at 85% → Needs work (correct: Blue is genuinely short)
    - Necrobloom at 79% → Needs work (correct)
    - army now at 85% → Needs work (correct)
    - Marchesa at 85% → Needs work (correct)
    - Meren at 94% → Solid (acceptable: Green is weak but not problematic)
    - graveyard fungus at 89% → Solid (borderline — Green at 89% avg)
    - Townos at 96% → Excellent (correct, no weakest color)
    - Kenrith 5c at 99% → Excellent (correct)
    The two Solid decks (Avatar 94%, Meren 94%, graveyard fungus 89%) are the ones to watch:
    any approach must NOT regress Meren/graveyard-fungus to Needs-work while fixing Avatar.

## Eliminated

(none yet)

## Resolution

- root_cause: |
    The manabase health band (Excellent/Solid/Workable/Needs work) derived entirely from Karsten
    raw per-color source counts (Deficit > 1 and ColorLimitedUnderSupportedCount > tolerance)
    with NO reference to the castability simulation. Avatar/Jeskai showed "Solid" despite White
    being the clear sim-weakest color because White's Karsten source count was generous enough
    (32.3 actual vs 24 required = 8 surplus) that neither Deficit>1 nor colorStarved fired.
    The sim correctly identified White as the tightest color (Suki 73%, genuinely color:White-
    limited), but the band logic had no path to count that as an issue.

- fix: |
    Approach 2 (per-color cast-rate feeding ColorsWithIssue) with Gate C refinement, flag-gated
    behind manabase.health-band-castability (default OFF). Added a simWeakestProblem condition
    to ComputeColorSignals() in ManabaseModels.cs:
      bool simWeakestProblem = UseHealthBandCastability
          && f.Color == compositeProblemWorst?.Color
          && f.ColorLimitedUnderSupportedCount >= 1    // Gate C: must be color-access-limited, not just mana-limited
          && f.WorstSpellCastPercent < supportThreshold;
    Gate C (ColorLimitedUnderSupportedCount >= 1) is the key separation: Avatar/White has
    ColorLimitedUnderSupportedCount=1 (Suki is genuinely color:White-limited → fires), while
    Meren/Green and graveyard-fungus/Green have ColorLimitedUnderSupportedCount=0 (their low
    worst-cast cards are mana-limited curve bombs: Old Gnawbone 7MV, Protean Hulk 7MV,
    Ziatora 6MV → do NOT fire). Infrastructure (flag seed in FeatureFlagStore SQLite+PG,
    catalog entry, ManabaseAnalyzer.Analyze parameter, ManabaseReport.UseHealthBandCastability,
    and ManabaseAnalysisService wiring) was already in place from the session.

- verification: |
    9-deck Gate C regression guard (all cached, no HTTP) with useHealthBandCastability=true:
    | Deck                                 | Flag OFF  | Flag ON    | Expected |
    |--------------------------------------|-----------|------------|----------|
    | Brago (WU control)                   | Needs work| Needs work | PASS     |
    | Kenrith 5c rocks                     | Excellent | Excellent  | PASS     |
    | Meren Golgari ramp/ritual            | Solid     | Solid      | PASS (no regression) |
    | Avatar — Sokka/Aang (Jeskai)         | Solid     | Workable   | PASS (intended fix) |
    | Archidekt 23563520 — Marchesa        | Needs work| Needs work | PASS     |
    | Archidekt 23753514 — graveyard fungus| Solid     | Solid      | PASS (no regression) |
    | Archidekt 23638601 — Townos          | Excellent | Excellent  | PASS     |
    | Archidekt 8066726 — Necrobloom       | Needs work| Needs work | PASS     |
    | Archidekt 7084567 — army now         | Needs work| Needs work | PASS     |
    Permanent CI tests: ManabaseHealthBandRegressionTests.Avatar_FlagOff_BandIsSolid and
    Avatar_FlagOn_BandIsWorkable_WeakestColorWhite — both PASS.
    Full suites: Core 774/774, Web 770/770 (11 PG-skipped).
    Health/LandShortfallCoveredByRamp/PrimaryFix invariant confirmed: all three share
    ComputeColorSignals(); Avatar flag ON has ColorsWithIssue=1, so LandShortfallCoveredByRamp
    returns false and PrimaryFix land-advice is consistent with Workable verdict.

- files_changed:
    - DeckFlow.Core/Manabase/ManabaseModels.cs (simWeakestProblem Gate C condition)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (useHealthBandCastability parameter)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs (flag catalog entry)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (SQLite+PG seed)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (flag wiring)
    - DeckFlow.Web.Tests/Manabase/ManabaseHealthBandRegressionTests.cs (new CI regression test)
