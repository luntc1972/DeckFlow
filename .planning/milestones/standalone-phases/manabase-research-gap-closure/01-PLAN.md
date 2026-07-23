---
phase: manabase-research-gap-closure
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Core/Manabase/ManabaseClassifier.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseLiveOracleCanaryTests.cs
  - DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs
  - docs/manabase-analysis-rules.md
autonomous: true
requirements: [MBGAP-02]
must_haves:
  truths:
    - "Fast lands (Botanical Sanctum / Concealed Courtyard / Blackcleave Cliffs cycle) are classified with per-trial count metadata (untapped when other-lands <= 2), not a fixed tapped/untapped boolean"
    - "Slow lands (Deathcap Glade cycle) are classified with per-trial count metadata (untapped when other-lands >= 2)"
    - "ELD threshold lands (Mystic Sanctuary class) are classified with count + basic-type-filter metadata (untapped when >= 3 other lands of the named basic type)"
    - "Verge lands are classified as always-untapped with the second color gated by a static basic-type census (D-07 type-based path, not a tapped-state gate)"
    - "Training Compound MSH allied cycle (5 cards) is classified as always-untapped colorless source with the two allied colors gated by a static basic-supertype census (Oracle: 'control a basic land')"
    - "Vivid lands are classified as ETB-tapped with a reduced-weight conditional any-color source (planner-discretion depth per D-07)"
    - "Every new oracle-text regex has a canary assertion in ManabaseLiveOracleCanaryTests.cs"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      provides: "CountConditionKind enum + count-condition metadata fields on ManaSource"
      contains: "CountConditionKind"
    - path: "DeckFlow.Core/Manabase/ManabaseClassifier.cs"
      provides: "Detection + classification for all six MBGAP-02 cycles"
      contains: "Verge"
    - path: "DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs"
      provides: "Skipped-scaffold test file for the new per-trial primitive (Skip removed + real assertions land in plan 02)"
  key_links:
    - from: "ManabaseClassifier.cs"
      to: "ManaSource.CountConditionKind"
      via: "classifier populates count metadata that the sim (plan 02) consumes"
      pattern: "CountConditionKind\\s*="
---

<objective>
Close the classification half of MBGAP-02 (D-06/D-07/D-08): give all six untapped-land
cycles real classifier rules — fast lands, slow lands, ELD threshold lands, the Verge
cycle, Vivid lands, and the MSH "Training Compound" allied cycle — plus define the
per-trial count-condition contract that plan 02's simulator work consumes.

This plan does the STATIC and DETECTION work only. The three count-based cycles
(fast/slow/ELD) are *tagged* here with count metadata on `ManaSource`; their per-trial
tapped/untapped resolution is implemented in plan 02 (`CastabilitySimulator`). Verge,
Training Compound, and Vivid are fully resolved here because they are static (color-gate
or ETB-tapped), not timing-dependent.

Purpose: closes the documented `ManabaseClassifier.cs:479-501` cycle backlog per D-06.
Output: new `CountConditionKind` contract on `ManaSource`, six-cycle detection/classification,
oracle canaries, and a skipped-scaffold test file for the new sim primitive.

Phase scope boundary (D-01/D-02): this phase's plan set (01-09) delivers Tier 1
(MBGAP-01/02/03/04) + Tier 2 (MBGAP-05a-d) + closing tasks MBGAP-11/12, and nothing else.
Per D-01, Tier 3 minors (MBGAP-06/07/08/10) stay in the backlog and are NOT planned here.
Per D-02, MBGAP-09 (cEDH castability surface) is its own later phase — it is NOT planned
here and the existing ROADMAP backlog pointer to it must be preserved (do not delete it).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/CONTEXT.md
@.planning/phases/manabase-research-gap-closure/RESEARCH.md
@.planning/phases/manabase-research-gap-closure/manabase-research-gap-closure-PATTERNS.md

<interfaces>
<!-- Existing classifier machinery to mirror (extracted from source + PATTERNS.md). -->

From DeckFlow.Core/Manabase/ManabaseClassifier.cs (existing census templates — mirror, do not duplicate):
- CheckLandRegex, SnarlRevealRegex, ConditionalTypeTemplates[] (Cls:487-507) — regex-family pattern to extend
- const int CheckLandMatchTypeThreshold = 6 (Cls:507) — the "≥6 lands bearing a named basic type" gate
- CountLandsBearingAnyType(cards, types, candidate) (Cls:1031-1072) — static per-deck type census (counts nonbasic TYPED lands; for Training Compound you need TRUE basics instead — see Task 1(d))
- IsConditionallyUntapped(card, cards) (Cls:1063-1071) — static untapped decision
- AddGrantedSources / IsConditional=true, Weight=0.25 (Cls:1477-1542) — Bernoulli-gated conditional source (reuse for Vivid any-color)
- Fetch-land composition weight: `basicFetch && deckColorCount >= 3 ? 0.67 : 1.0` (Cls:348-364) — weight-from-composition template

From DeckFlow.Core/Manabase/ManabaseModels.cs:
- ManaSource record (colors/weight/IsLand/IsConditional/ManaAmount/EntersUntapped fields) — add new count-condition fields here
- OneShotMana record (Models:349), ManabaseDeck.OneShots (Models:427) — unrelated to this plan, do not touch

MSH "Training Compound" cycle (RESEARCH Addendum Q1 — verified live Scryfall):
Gleaming Bastion (W/U), Hidden Lair (U/B), Dark Fortress (B/R), Training Compound (R/G), Gathering Place (G/W).
Oracle clause (identical, colors vary): "Activate only if this land entered this turn or if you control a basic land."
Always enters UNTAPPED; {C} unconditional; two allied colors gated on the clause above ("control a BASIC land" = Basic supertype).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 0: Scaffold the per-trial ConditionalCountLand test file (Wave 0 gap)</name>
  <read_first>
    - DeckFlow.Core.Tests/Manabase/KarstenManabaseCastabilityTests.cs (closest sim-facing test shape to mirror)
    - .planning/phases/manabase-research-gap-closure/VALIDATION.md (Wave 0 Requirements list — "full tests green after every wave")
  </read_first>
  <action>
    Create DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs in namespace DeckFlow.Core.Tests
    with xUnit method stubs (one per behavior) for: fast-land untapped when other-lands<=2,
    fast-land tapped when other-lands>=3, slow-land tapped when other-lands<2, slow-land untapped when
    other-lands>=2, ELD-threshold untapped only when >=3 other lands of the named basic type. Mark EACH
    stub `[Fact(Skip = "enabled in plan 02 once the ConditionalCountLand sim primitive exists")]` so the
    file compiles and the suite stays GREEN (these tests SKIP, they do NOT go RED — VALIDATION.md requires
    the full suite green after every wave). Give each stub a trivial placeholder body (e.g. `Assert.True(true);`);
    plan 02 removes the Skip and lands the real assertions. Do NOT implement sim logic here.
    Mirror the naming convention Method_Scenario_ExpectedResult.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - File DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs exists in namespace DeckFlow.Core.Tests
    - `dotnet build DeckFlow.sln` succeeds with 0 warnings / 0 errors
    - `grep -c 'Skip = "enabled in plan 02' DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs` returns >= 5
    - The full test suite is GREEN with these 5 tests SKIPPED (0 failing) — the scaffold must not go RED
    - File contains no reference to a CardKind enum value (that primitive does not exist until plan 02)
  </acceptance_criteria>
  <done>Compiling scaffold test file present with >=5 SKIPPED stubs (suite stays green); plan 02 removes the Skip.</done>
</task>

<task type="auto">
  <name>Task 1: Add CountConditionKind contract + classify all six cycles</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseModels.cs (ManaSource record — add fields at the record; preserve `{ get; init; }` carve-out, never get-only)
    - DeckFlow.Core/Manabase/ManabaseClassifier.cs (lines 340-520 regex block + weight math; lines 990-1075 census; lines 1440-1545 granted-source pattern)
    - .planning/phases/manabase-research-gap-closure/RESEARCH.md (MBGAP-02 oracle table + Addendum Q1 Training Compound)
  </read_first>
  <action>
    (a) In ManabaseModels.cs, add to `ManaSource`: a public enum `CountConditionKind { None, FastLand, SlowLand, EldThreshold }`
    plus `CountConditionKind CountCondition { get; init; } = CountConditionKind.None;`, `int CountThreshold { get; init; }`,
    and `IReadOnlyList<string> CountTypeFilter { get; init; } = Array.Empty<string>();` (CountTypeFilter carries the ELD
    named basic type(s), e.g. ["Island"]; empty for fast/slow). Use `{ get; init; }` — do NOT emit get-only. Do not
    reorder or reformat existing ManaSource members.
    (b) In ManabaseClassifier.cs add sibling `private static readonly Regex` fields immediately below the CheckLandRegex block
    (Cls:487-507) for: FastLandRegex ("enters tapped unless you control two or fewer other lands" — e.g. Botanical Sanctum /
    Concealed Courtyard / Blackcleave Cliffs),
    SlowLandRegex ("enters tapped unless you control two or more other lands" — e.g. Deathcap Glade),
    EldThresholdRegex ("enters tapped unless you control three or more other ([A-Za-z]+)s" — capture the basic type),
    VergeSecondColorRegex (the "as long as you control a Plains or an Island"-shape conditional color clause),
    TrainingCompoundRegex ("Activate only if this land entered this turn or if you control a basic land"),
    VividChargeRegex ("with two charge counters on it"). Each new regex gets the [ASSUMED]-style verification comment
    pointing at ManabaseLiveOracleCanaryTests.cs (mirror the existing comment style at the CheckLandRegex block).
    (c) Fast/slow lands: classify as land sources with EntersUntapped left to the sim; set CountCondition=FastLand
    (threshold 2, "<=") or SlowLand (threshold 2, ">="). ELD: CountCondition=EldThreshold, CountThreshold=3,
    CountTypeFilter=[captured basic type].
    (d) Verge + Training Compound (static, D-07 type-based like check lands): always EntersUntapped=true; the conditional
    color(s) are included in Produces only when the relevant census >= CheckLandMatchTypeThreshold; otherwise emit the source
    with only the unconditional color(s) ({C} for Training Compound, the fixed first color for Verge). For Verge, use
    CountLandsBearingAnyType over the two named basic types. For Training Compound, the oracle clause is "control a BASIC land"
    (Basic supertype) — CountLandsBearingAnyType counts nonbasic TYPED lands and is WRONG here; add a dedicated private static
    basic-supertype census helper (e.g. `CountBasicLands(cards)` that counts cards whose TypeLine carries the "Basic"
    supertype, Quantity-weighted) and gate Training Compound's two allied colors on it. Reuse CheckLandMatchTypeThreshold;
    do NOT invent a per-trial path for these.
    (e) Vivid (planner discretion per D-07): classify the land itself as ETB-tapped (EntersUntapped=false), and add ONE
    reduced-weight any-color conditional source via the AddGrantedSources IsConditional=true pattern (Weight=0.25,
    IsConditional=true, Produces=deck colors) to approximate the 2 charge counters — document the approximation depth in
    the docs update (Task 3). Do NOT model per-game "uses remaining"; the sim has no such counter.
    (f) Remove/replace the obsolete backlog comment at Cls:479-501 to reflect that these cycles are now handled.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "CountConditionKind" DeckFlow.Core/Manabase/ManabaseModels.cs` returns >= 2
    - `grep -Ec "FastLandRegex|SlowLandRegex|EldThresholdRegex|VergeSecondColorRegex|TrainingCompoundRegex|VividChargeRegex" DeckFlow.Core/Manabase/ManabaseClassifier.cs` returns >= 6
    - Training Compound gates on a dedicated basic-supertype census (TRUE basics), NOT CountLandsBearingAnyType: `grep -c "CountBasicLands" DeckFlow.Core/Manabase/ManabaseClassifier.cs` returns >= 1
    - ManaSource new members use `{ get; init; }` (verify: `grep -A2 "CountCondition " DeckFlow.Core/Manabase/ManabaseModels.cs` shows `get; init;`, never `get;` alone)
    - The Cls:479-501 "backlog" comment no longer claims fast/slow/ELD/Verge/Vivid are unhandled
    - `dotnet build DeckFlow.sln` 0 warnings / 0 errors
  </acceptance_criteria>
  <done>All six cycles detected+classified; fast/slow/ELD carry count metadata; Verge/Training/Vivid fully static-resolved (Training Compound via true-basic census).</done>
</task>

<task type="auto">
  <name>Task 2: Classifier unit tests + oracle canaries + docs classifier section</name>
  <read_first>
    - DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs (existing check-land/Snarl/bond test shape to mirror)
    - DeckFlow.Core.Tests/Manabase/ManabaseLiveOracleCanaryTests.cs (existing canary assertion shape)
    - docs/manabase-analysis-rules.md (classifier/land-classification section to extend)
  </read_first>
  <action>
    (a) Extend ManabaseClassifierTests.cs with cases proving: a fast land in a small-land deck emits CountCondition=FastLand;
    an ELD land emits EldThreshold + the right CountTypeFilter; a Verge in a deck with >=6 matching basics produces both
    colors, and with <6 produces only the fixed color; a Training Compound with >=6 TRUE basics produces R/G + colorless, and with
    <6 produces colorless-only; a Vivid land is ETB-tapped with one IsConditional any-color source. Use existing CardFact
    construction helpers already used in this test file.
    (b) Add one canary assertion per new regex in ManabaseLiveOracleCanaryTests.cs (fast/slow/ELD/Verge/Training Compound/Vivid)
    asserting the regex matches the exact verified oracle clause string from RESEARCH.md's MBGAP-02 table and Addendum Q1.
    (c) Update docs/manabase-analysis-rules.md: add/extend the land-classification section documenting the six cycles, the
    static-vs-per-trial split (Verge/Training/Vivid static, fast/slow/ELD per-trial in the sim), the Training Compound
    true-basic-census gate, the Vivid approximation depth, and that these ride the analysis.manabase.accuracy bundle
    (D-08, no new flag). Touch only changed lines (LF endings).
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests|FullyQualifiedName~ManabaseLiveOracleCanary" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - New ManabaseClassifierTests cases pass (fast/ELD/Verge/Training/Vivid)
    - ManabaseLiveOracleCanaryTests has >= 6 new assertions covering the new regexes; all pass
    - docs/manabase-analysis-rules.md mentions "Training Compound" and the per-trial-vs-static split
    - `git diff --stat` vs `git diff --ignore-all-space --stat` show no whole-file EOL churn on docs/manabase-analysis-rules.md
  </acceptance_criteria>
  <done>Classifier tests + canaries green; docs classifier section updated.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| user-pasted decklist → classifier | Untrusted text already parsed upstream; this plan adds no new input surface |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap02-01 | Tampering | oracle-text regex drift | mitigate | Canary assertions in ManabaseLiveOracleCanaryTests (Task 2) catch live-wording rot (H1 lesson) |
| T-mbgap02-SC | Tampering | NuGet installs | accept | No new packages added this plan; nothing to install |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean (0/0).
- `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase"` green; the ConditionalCountLandTests scaffold tests are SKIPPED (green, not RED) until plan 02 removes the Skip.
- No EOL churn on any touched file.
</verification>

<success_criteria>
All six MBGAP-02 cycles classified; fast/slow/ELD carry count-condition metadata on ManaSource; Verge/Training Compound/Vivid fully static-resolved (Training Compound via true-basic census); canaries added; classifier tests green; docs updated; sim scaffold file present with tests skipped (suite green) and ready for plan 02.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/01-SUMMARY.md` when done.
</output>
