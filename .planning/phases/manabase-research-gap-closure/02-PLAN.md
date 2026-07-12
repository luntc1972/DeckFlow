---
phase: manabase-research-gap-closure
plan: 02
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - DeckFlow.Core/Manabase/CastabilitySimulator.cs
  - DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
  - docs/manabase-analysis-rules.md
autonomous: true
requirements: [MBGAP-02]
must_haves:
  truths:
    - "A fast land enters untapped in a sim trial only when the trial's own land-play sequence has <=2 other lands already in play at the moment it is played"
    - "A slow land enters untapped in a sim trial only when >=2 other lands are already in play"
    - "An ELD threshold land enters untapped only when >=3 already-played lands match its named basic type in that trial"
    - "The analysis.manabase.accuracy bundle OFF still produces byte-identical output (the new per-trial primitive is inside the bundle and must not break the toggle)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/CastabilitySimulator.cs"
      provides: "CardKind.ConditionalCountLand + per-trial resolution in PlayOneLand"
      contains: "ConditionalCountLand"
    - path: "DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs"
      provides: "Real per-trial assertions (replaces plan 01 scaffold stubs)"
  key_links:
    - from: "CastabilitySimulator.PlayOneLand"
      to: "landsOnBoard"
      via: "count already-played lands (type-filtered for ELD) against the source's CountThreshold at play time"
      pattern: "ConditionalCountLand"
---

<objective>
Implement the one genuinely-new primitive of MBGAP-02 (D-07): per-trial dynamic
tapped/untapped resolution for fast, slow, and ELD threshold lands inside the
Monte-Carlo simulator. Consumes the `CountConditionKind` metadata that plan 01
attached to `ManaSource`.

The existing static `UntappedLand`/`TappedLand` split cannot represent these cycles
because their tapped state depends on the specific trial's shuffle and land-play order.
This plan adds `CardKind.ConditionalCountLand` and resolves it at the moment the land is
chosen to be played in `PlayOneLand`, using the per-trial `landsOnBoard` state.

Purpose: closes the per-trial half of D-06/D-07 for fast/slow/ELD.
Output: new sim CardKind + resolution logic, real ConditionalCountLandTests assertions,
accuracy-bundle byte-identical-off parity coverage, docs sim section.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/RESEARCH.md
@.planning/phases/manabase-research-gap-closure/manabase-research-gap-closure-PATTERNS.md
@.planning/phases/manabase-research-gap-closure/01-SUMMARY.md

<interfaces>
<!-- Existing sim internals (extracted from source). -->

From DeckFlow.Core/Manabase/CastabilitySimulator.cs:
- private enum CardKind { UntappedLand, TappedLand, Ramp, OneShotMana, Filler } (CS:44-51) — add ConditionalCountLand
- LibraryCard struct (CS:53+) — carries CardKind + color mask + amount; extend to carry CountCondition/CountThreshold/CountTypeFilter
- Static CardKind assignment from ManaSource: `CardKind kind = source.EntersUntapped ? CardKind.UntappedLand : CardKind.TappedLand;` then AddWeighted(...) (CS:884-890) — branch to ConditionalCountLand when source.CountCondition != None
- PlayOneLand (CS:1194-1300): computes scratchOnlineMasks from `landsOnBoard` (List<(int Mask, int OnlineTurn, int Amount)>), then:
  `int onlineTurn = played.Kind == CardKind.TappedLand ? currentTurn + 1 : currentTurn;`
  `landsOnBoard.Add((played.ColorMask, onlineTurn, played.ManaAmount));`

From plan 01 (ManaSource, ManabaseModels.cs):
- CountConditionKind { None, FastLand, SlowLand, EldThreshold }, int CountThreshold, IReadOnlyList<string> CountTypeFilter

Pitfall 2 (RESEARCH): `landsOnBoard` stores a COLOR mask, not a basic-type tag. ELD ("three or more other Islands")
cannot be resolved from the color mask alone. Choose ONE explicitly (see Task 1 action).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add CardKind.ConditionalCountLand + per-trial resolution</name>
  <behavior>
    - Fast land played with 0-2 other lands on board this trial → enters untapped (OnlineTurn = currentTurn)
    - Fast land played with >=3 other lands on board → enters tapped (OnlineTurn = currentTurn + 1)
    - Slow land played with <2 other lands → tapped; with >=2 other lands → untapped
    - ELD land played with >=3 already-played lands matching its CountTypeFilter basic type → untapped; else tapped
    - A deck with no ConditionalCountLand sources produces identical trial outcomes to before (no accidental path change)
  </behavior>
  <read_first>
    - DeckFlow.Core/Manabase/CastabilitySimulator.cs (CS:44-90 enum+LibraryCard, CS:860-900 AddWeighted call, CS:1194-1300 PlayOneLand — read all three ranges before editing)
    - DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs (the scaffold from plan 01 — replace stub bodies with real assertions)
  </read_first>
  <action>
    (a) Add `ConditionalCountLand` to the CardKind enum (CS:44-51). Extend LibraryCard (and the AddWeighted/build path) to carry
    the three plan-01 metadata values (CountConditionKind, int threshold, type-filter mask or string list). For ELD, prefer
    resolving the basic type into a type-tag the sim can compare per already-played land: extend the `landsOnBoard` tuple to
    carry a basic-type tag (Pitfall 2 option (a)) OR, if that is too invasive, resolve ELD via the existing static
    CountLandsBearingAnyType census at classification time (Pitfall 2 option (b), an accepted approximation). Pick ONE and state
    the choice in the SUMMARY and the docs update (Task 2). Fast/slow MUST use the per-trial count path regardless.
    (b) In the CardKind assignment at CS:884-890, when source.CountCondition != None emit CardKind.ConditionalCountLand instead
    of the EntersUntapped ternary.
    (c) In PlayOneLand, before setting `onlineTurn`, when played.Kind == ConditionalCountLand compute the relevant count from
    landsOnBoard (all previously-played lands = "other lands" from this land's perspective; for ELD, the type-filtered subset)
    and compare against played threshold with the direction from CountCondition (FastLand: untapped when count<=2;
    SlowLand: untapped when count>=2; EldThreshold: untapped when typeCount>=3). Set onlineTurn = currentTurn (untapped) or
    currentTurn+1 (tapped) accordingly, then Add to landsOnBoard exactly as the existing code does.
    (d) Replace the plan-01 scaffold stubs in ConditionalCountLandTests.cs with real assertions matching the <behavior> list,
    using a deterministic seed / forced land order where the sim harness supports it (mirror KarstenManabaseCastabilityTests
    seeding). Keep tests fast (bounded trial count where the test seam allows).
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ConditionalCountLand|FullyQualifiedName~KarstenManabaseCastability" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "ConditionalCountLand" DeckFlow.Core/Manabase/CastabilitySimulator.cs` returns >= 3
    - ConditionalCountLandTests.cs has 0 remaining `Assert.True(false` stubs (`grep -c "Assert.True(false" DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs` == 0)
    - All ConditionalCountLandTests pass
    - `dotnet build DeckFlow.sln` 0 warnings / 0 errors (preserve switch-expression + raw-string carve-outs)
  </acceptance_criteria>
  <done>Fast/slow/ELD resolve per-trial; scaffold replaced with green assertions.</done>
</task>

<task type="auto">
  <name>Task 2: accuracy-bundle byte-identical-off parity + docs sim section</name>
  <read_first>
    - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs (existing accuracy-flag parity coverage to extend)
    - DeckFlow.Web.Tests/Manabase/ManabaseFlagBaselineHarness.cs (docstring references the flag-baseline convention)
    - docs/manabase-analysis-rules.md (sim / accuracy-bundle section)
  </read_first>
  <action>
    (a) Add/extend a parity test proving that with analysis.manabase.accuracy OFF, a deck containing fast/slow/ELD/Verge/
    Training-Compound/Vivid lands produces byte-identical ManabaseReport output to the pre-change baseline (i.e. the new
    per-trial primitive is only active when the bundle is ON). Mirror the existing accuracy-flag parity assertions already in
    ManabaseAnalysisServiceTests.cs; do NOT add a new flag (D-08 — this rides the existing bundle).
    (b) Update docs/manabase-analysis-rules.md sim section: document the ConditionalCountLand per-trial resolution, the
    fast(<=2)/slow(>=2)/ELD(>=3 same-type) thresholds, the chosen ELD tracking approach (per-trial type-tag vs static census),
    and that the whole set is gated by analysis.manabase.accuracy (ON in prod). Touch only changed lines, LF endings.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ManabaseAnalysisService" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - A parity test asserts accuracy-OFF byte-identical output for a deck with the new cycles; it passes
    - docs/manabase-analysis-rules.md documents the fast/slow/ELD thresholds and the accuracy-bundle gate
    - `git diff --stat` vs `git diff --ignore-all-space --stat` show no EOL churn on any touched file
  </acceptance_criteria>
  <done>accuracy-OFF parity green; sim docs updated.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| decklist → sim | No new input surface; sim consumes already-classified sources |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap02b-01 | Tampering | accuracy-bundle toggle regression | mitigate | byte-identical-off parity test (Task 2) guards the bundle's existing invariant |
| T-mbgap02b-SC | Tampering | NuGet installs | accept | No new packages this plan |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- Full `dotnet test DeckFlow.sln` green (ConditionalCountLandTests now real+passing).
- accuracy-OFF parity holds.
</verification>

<success_criteria>
Fast/slow/ELD lands resolve tapped/untapped per-trial from the trial's own land sequence; the accuracy bundle stays byte-identical when off; sim docs updated; MBGAP-02 fully closed.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/02-SUMMARY.md` when done.
</output>
