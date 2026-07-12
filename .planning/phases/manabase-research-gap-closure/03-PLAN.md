---
phase: manabase-research-gap-closure
plan: 03
type: execute
wave: 3
depends_on: ["01", "02"]
files_modified:
  - DeckFlow.Core/Manabase/ManabaseClassifier.cs
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseLiveOracleCanaryTests.cs
  - docs/manabase-analysis-rules.md
autonomous: true
requirements: [MBGAP-01]
must_haves:
  truths:
    - "Cavern of Souls / Unclaimed Territory count as a full color source only up to the deck's dominant-creature-type share; otherwise they are heavily discounted (D-03, not a flat discount)"
    - "Ancient Ziggurat is weighted by the creature share of the deck (D-03)"
    - "Nykthos, Shrine to Nyx is modeled as a conditional low-weight source (D-03, reuses the IsConditional Bernoulli pattern)"
    - "A per-deck creature-subtype histogram and dominant-type-share fraction is computed from TypeLine em-dash splitting"
    - "Each restricted-land source is flagged so the view (plan 04) can render the disclosure marker"
    - "Every new oracle-text regex has a canary assertion"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseClassifier.cs"
      provides: "SpendOnlyCreatureRegex + Nykthos devotion detector + subtype-share census + composition-gated weights"
      contains: "devotion"
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      provides: "IsRestrictedSourceUsed flag on CardCastability (or equivalent) for the plan-04 disclosure marker"
      contains: "IsRestrictedSourceUsed"
  key_links:
    - from: "ManabaseClassifier.cs"
      to: "creature-subtype histogram"
      via: "TypeLine.Split('—') → max(subtype count)/totalCreatureCount → weight scaling"
      pattern: "Split"
---

<objective>
Implement the classification half of MBGAP-01 (D-03): composition-gated per-class
modeling of the four conditional-restriction lands — Cavern of Souls, Unclaimed
Territory, Ancient Ziggurat, and Nykthos, Shrine to Nyx. This is behind-the-flag math
only; flag wiring, disclosure UI, and parity are plan 04.

D-03 is explicit: NOT a flat discount, NOT a full spend-restriction sim mask. Instead a
weight scaled by a computed deck fraction (dominant-creature-type share for
Cavern/Unclaimed, creature share for Ziggurat) and a conditional low weight for Nykthos.

Purpose: fixes efficacy finding M8 (these lands currently overstate color fixing).
Output: new regexes + canaries, a genuinely-new creature-subtype-share census, four
composition-gated weight rules, and the model flag that plan 04's marker consumes.
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
<!-- Templates to mirror (extracted from source + PATTERNS.md). -->

From DeckFlow.Core/Manabase/ManabaseClassifier.cs:
- Regex-family block CheckLandRegex/SnarlRevealRegex/ConditionalTypeTemplates (Cls:487-507) — add SpendOnlyCreatureRegex + Nykthos devotion detector as siblings
- Fetch-land composition weight `basicFetch && deckColorCount >= 3 ? 0.67 : 1.0` (Cls:348-364) — template for weight-from-composition (Cavern/Unclaimed/Ziggurat)
- AddGrantedSources IsConditional=true, Weight=0.25 (Cls:1477-1542) — template for Nykthos conditional low weight
- IsType(TypeLine, "Creature") — the only existing subtype handling; there is NO subtype/tribal census today (build it)
- CardFact.TypeLine = raw Scryfall string e.g. "Legendary Creature — Elf Druid"; split on em-dash (—) to get subtypes

Oracle text (RESEARCH MBGAP-01 table, MEDIUM confidence — verify via canary):
- Cavern of Souls / Unclaimed Territory: "Spend this mana only to cast a creature spell of the chosen type" (type-restricted; Cavern adds "and that spell can't be countered")
- Ancient Ziggurat: "Spend this mana only to cast a creature spell." (any-creature, NOT type-restricted)
- Nykthos: activated ability with "devotion to that color" (no "spend this mana only" clause — needs a distinct detector)

From DeckFlow.Core/Manabase/ManabaseModels.cs:
- CardCastability record (Models:169), IsCostOverridden bool (Models:159,196) — add IsRestrictedSourceUsed sibling bool (use `{ get; init; }`)

Formula (RESEARCH D-03, researcher-proposed — planner PINS it here):
dominantTypeShare = max(subtypeHistogram.Values) / totalCreatureCount (Quantity-weighted).
- Cavern/Unclaimed: full color-source weight up to dominantTypeShare, heavy discount on the remainder → effective weight = dominantTypeShare (clamped to [minFloor, 1.0]); assume the user names the deck's dominant type (best case).
- Ziggurat: weight = creatureShare = totalCreatureCount / nonlandCardCount (any-creature spend).
- Nykthos: fixed IsConditional low weight (0.25), independent of composition.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Restricted-land detection + creature-subtype-share census</name>
  <behavior>
    - Cavern in an Elf-tribal deck (dominant type share ~1.0) → near-full color weight
    - Cavern in a 3-type deck with no dominant tribe (share ~0.4) → weight ~0.4 (heavy discount), not full
    - Ziggurat in a 60% creature deck → weight ~0.6
    - Nykthos → a single IsConditional source at weight 0.25
    - A deck with none of these four lands → classification byte-identical to before
  </behavior>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseClassifier.cs (Cls:340-520 weight+regex, Cls:990-1075 census, Cls:1440-1545 granted-source)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (ManaSource + CardCastability records)
  </read_first>
  <action>
    (a) Add `SpendOnlyCreatureRegex` matching "spend this mana only to cast a creature spell(?: of the chosen type)?" (the
    optional "of the chosen type" group distinguishes Cavern/Unclaimed from Ziggurat) and a Nykthos detector matching
    "devotion to that color" in the activated-ability line. Add the [ASSUMED] verification comment pointing at the canary test,
    matching the existing CheckLandRegex comment style.
    (b) Build the new creature-subtype-share census (no existing helper): a private static method that splits each creature
    CardFact.TypeLine on the em-dash (—), takes the post-dash subtype tokens, builds a Quantity-weighted histogram, and returns
    dominantTypeShare = max(histogram.Values)/totalCreatureCount plus creatureShare = totalCreatureCount/nonlandCount. Guard
    divide-by-zero (0 creatures → share 0).
    (c) Apply composition-gated weights using the fetch-land weight template (Cls:348-364), NOT a flat constant:
    Cavern/Unclaimed weight = Clamp(dominantTypeShare, floor, 1.0); Ziggurat weight = creatureShare; Nykthos = one IsConditional
    source at Weight=0.25 via the AddGrantedSources pattern. Pin `floor` as a named private const (e.g.
    RestrictedLandMinWeight = 0.25) — no magic numbers.
    (d) Set IsRestrictedSourceUsed=true on the resulting CardCastability rows (add the bool to CardCastability in
    ManabaseModels.cs using `{ get; init; }`) so plan 04's marker can gate on it. All new behavior must be reachable ONLY when
    the plan-04 restricted-lands flag is on — thread a `bool restrictedLands = false` guard param through the classify path so
    that flag-off is byte-identical (the flag itself is registered in plan 04; here just add the trailing-optional guard and
    default it false).
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -Ec "SpendOnlyCreatureRegex|devotion" DeckFlow.Core/Manabase/ManabaseClassifier.cs` returns >= 2
    - `grep -c "IsRestrictedSourceUsed" DeckFlow.Core/Manabase/ManabaseModels.cs` returns >= 1 and uses `{ get; init; }`
    - `grep -c "Split" DeckFlow.Core/Manabase/ManabaseClassifier.cs` shows the new em-dash split census
    - No flat-constant weight for these four lands (weights are Clamp(dominantTypeShare,...)/creatureShare/0.25-conditional)
    - `dotnet build DeckFlow.sln` 0/0
  </acceptance_criteria>
  <done>Four restricted lands composition-gated; subtype census built; model flag added; flag-off guard defaults false.</done>
</task>

<task type="auto">
  <name>Task 2: Restricted-land unit tests + canaries + docs</name>
  <read_first>
    - DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseLiveOracleCanaryTests.cs
    - docs/manabase-analysis-rules.md
  </read_first>
  <action>
    (a) Add ManabaseClassifierTests cases matching Task 1 <behavior> (tribal Cavern near-full, multi-type Cavern discounted,
    Ziggurat by creature share, Nykthos conditional 0.25, and a no-restricted-land deck unchanged with flag on).
    (b) Add canary assertions for SpendOnlyCreatureRegex (both Cavern "of the chosen type" and Ziggurat any-creature forms) and
    the Nykthos "devotion to that color" detector against the verified oracle strings.
    (c) Update docs/manabase-analysis-rules.md: document the composition-gated model (dominant-type-share formula, creature-share,
    Nykthos conditional weight), that it is gated by analysis.manabase.restricted-lands (registered in plan 04), and that the
    disclosure marker surfaces it. Changed lines only, LF.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests|FullyQualifiedName~ManabaseLiveOracleCanary" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - New classifier tests pass; >= 3 new canary assertions pass
    - docs/manabase-analysis-rules.md documents the dominant-type-share formula and the restricted-lands flag
    - No EOL churn on touched files (git diff --stat vs --ignore-all-space --stat)
  </acceptance_criteria>
  <done>MBGAP-01 classification tests+canaries green; docs updated.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| decklist → classifier | No new input surface |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap01-01 | Tampering | restricted-land oracle regex drift | mitigate | canaries in ManabaseLiveOracleCanaryTests (Task 2) |
| T-mbgap01-SC | Tampering | NuGet installs | accept | No new packages this plan |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase"` green.
- Flag-off guard (default false) keeps existing decks byte-identical (proven fully in plan 04's parity test).
</verification>

<success_criteria>
The four conditional-restriction lands are composition-gated per D-03 (dominant-type-share / creature-share / Nykthos conditional), the subtype census exists, the disclosure model flag is set, canaries added, docs updated; behavior is guarded behind a default-false param awaiting the plan-04 flag.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/03-SUMMARY.md` when done.
</output>
