# Phase 76: Bracket Classifier + Balancer — Research

**Researched:** 2026-06-28
**Domain:** Commander Bracket classification, Game Changers data versioning, paste-artifact rendering (3-variant), flag-gated tool registration
**Confidence:** HIGH

---

## Summary

Phase 76 is the headline differentiator for Cycle 13: a standalone `tool.bracket.enabled`-gated tool at `/bracket` that auto-classifies a deck into the official WotC 5-tier Commander bracket and, when a target bracket is chosen, produces a paste artifact listing floor violations and starter cuts. It also migrates the existing hardcoded bracket data out of a `.cs` literal into a versioned JSON seed file in `DeckFlow.Core`.

The phase touches four distinct technical areas, each with a clear codebase analog: (1) the bracket rubric + Game Changers data (new Core models, JSON seed, startup loader); (2) two-card combo detection via the existing `ICommanderSpellbookService`; (3) three decoupled prompt variants following the Primer/Analysis pattern; and (4) full tool-registry wiring following the Manabase tool as the closest analog.

The research confirmed the complete Game Changers list (53 cards as of February 9, 2026), the exact bracket tier thresholds, and the hard-floor gating conditions. No new packages are needed — Phase 76 is explicitly constrained to in-solution technology.

**Primary recommendation:** Mirror the Manabase tool end-to-end (`ManabaseController`, `FeatureFlagGateAttribute`, `ManabaseViewRenderTests`, `ToolRegistry` entry) and the Primer prompt-variant pattern (`IPrimerPromptVariant` → 3 classes + registry). Put the versioned JSON seed file in `DeckFlow.Web/Data/` (copied to output); load it into `IMemoryCache` at startup via a new singleton service. Keep MLD and extra-turn card name lists in the same JSON file so they version together.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BRACKET-01 | Auto-classify deck into B1-B5 from Game Changers count, two-card-combo presence, MLD/extra-turns/extra-cards — NOT tutor count | §1 bracket rubric + §3 combo detection + §4 MLD/extra-turn detection |
| BRACKET-02 | Game Changers live as versioned seed file (effective-date stamped, loaded at startup into IMemoryCache); existing `CommanderBracketCatalog.cs` migrated to Core | §2 data migration pattern (analog: ContentKbSeedLoader + HelpContentService) |
| BRACKET-03 | Target bracket → paste artifact: floor violations + starter cuts framed for AI refinement; null Spellbook = disclosed, never silent "zero combos" | §3 combo null semantics + §5 artifact rendering |
| BRACKET-04 | All three prompt variants (ChatGpt/Claude/Gemini) with no shared helper; parity test asserts both blocks in all three | §5 ADR-0001 pattern (analog: Primer folder) |
| BRACKET-05 | Artifact stamped with Game Changers effective-date; AI re-confirms membership; flag `tool.bracket.enabled` seeded OFF; full tool-registry entry | §6 effective-date + §7 flag/registry wiring |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Game Changers catalog (data model + classifier) | DeckFlow.Core | — | Pure domain logic; no HTTP/ASP.NET references needed; shared with Phase 77 Power axis |
| Game Changers JSON seed file loading | DeckFlow.Web (Service) | — | Needs `IMemoryCache` + `IWebHostEnvironment`; Core has no DI container |
| Two-card combo detection | DeckFlow.Web (Service, via existing `ICommanderSpellbookService`) | — | External HTTP call; already wired in Web |
| Bracket classification orchestration | DeckFlow.Web (Service) | DeckFlow.Core (classifier) | HTTP import + combo call in Web; pure bracket logic in Core |
| Paste-artifact building (3 variants) | DeckFlow.Web (PromptBuilders/Bracket/) | — | AI-variant rendering is a Web concern |
| Controller + view rendering | DeckFlow.Web (BracketController + Bracket.cshtml) | — | MVC layer |
| Flag registration + tool registry | DeckFlow.Web (FeatureFlagStore, ToolRegistry) | — | App configuration |
| Tests: classifier unit tests | DeckFlow.Core.Tests | — | Pure logic, no DI |
| Tests: view render, parity, controller | DeckFlow.Web.Tests | — | Integration-style, needs MVC host |

---

## Standard Stack

### Core (all in-solution, no new packages)

| Component | Location | Version | Purpose |
|-----------|----------|---------|---------|
| `ICommanderSpellbookService` | `DeckFlow.Web/Services/CommanderSpellbookService.cs` | existing | Two-card combo detection |
| `IMemoryCache` | `Microsoft.Extensions.Caching.Memory` (built-in) | existing | Game Changers catalog cache |
| `IDeckEntryLoader` | `DeckFlow.Core/Loading/` | existing | Deck URL/text import |
| `FeatureFlagGateAttribute` | `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` | existing | Route-level flag gating |
| `DeckToolControllerBase` | `DeckFlow.Web/Controllers/DeckToolControllerBase.cs` | existing | Timeout scope helper |
| `System.Text.Json` | built-in | net10.0 | JSON seed file deserialization |
| xUnit + `IRazorViewEngine` | `DeckFlow.Web.Tests` | 2.9.3 | View render tests (flag OFF/ON) |

**No new NuGet / npm packages.** All features are built on in-solution technology per the explicit constraint in REQUIREMENTS.md "Out of Scope" section.

### Package Legitimacy Audit

> Not applicable. Phase 76 introduces zero new external packages. All libraries are already installed in the solution.

---

## Section 1: Official Bracket Rubric (BRACKET-01)

### 1.1 Tier Definitions

The Commander Bracket system was updated October 21, 2025, with a smaller February 9, 2026 update adding 2 Game Changers. The authoritative source is the WotC announcement and the commanderbrackets.com community tracker.

| Tier | Name | Game Changers Allowed | Turn Expectation | Hard Floor Conditions |
|------|------|----------------------|-----------------|----------------------|
| B1 | Exhibition | 0 | 9+ turns | none |
| B2 | Core | 0 | 8+ turns | none |
| B3 | Upgraded | ≤ 3 | 6+ turns | any two-card win combo OR any MLD forces B4+ |
| B4 | Optimized | Unlimited | 4+ turns | — |
| B5 | cEDH | Unlimited | Any turn | — |

**Classification logic (planner: encode thresholds exactly as these):**

```
if (gameChangerCount >= 4) → B4+
if (hasTwoCardWinCombo) → B4+
if (hasMassLandDenial) → B4+
if (gameChangerCount >= 1 AND gameChangerCount <= 3) → at least B3
if (gameChangerCount == 0 AND !hasTwoCardWinCombo AND !hasMassLandDenial) → B1 or B2
```

Distinguishing B4 from B5: the rubric treats B5 as metagame-tuned cEDH. In practice DeckFlow should classify B4 for any deck that triggers a B4 hard floor, and surface B5 only when the deck has an exceptionally high Game Changers count (e.g., ≥ 10) or shows clear cEDH optimization signals. This is a judgment call — recommend the artifact discloses "classified B4 (Optimized); upgrade to B5 only if playing in a cEDH meta" rather than trying to auto-distinguish B4 from B5.

**Explicitly NOT a bracket gate (removed October 2025):** Tutor count. The existing `ChatGptAnalysisPromptVariant` references tutors in bracket guidance narrative — that is user-facing copy, not a gating signal.

**Extra turn spells:** The scrollvault rubric shows "3+ extra turn spells → B3 minimum" as a soft signal, not a hard B4 floor. Extra turns are INFORMATIONAL (show in reasons, count them) but do NOT by themselves force B4. [CITED: scrollvault.net/guides/commander-brackets.html]

**Mass extra-card draw:** Not a hard gate in the current rubric. Listed as informational in the reasons output. [ASSUMED] — the WotC docs do not specify a hard extra-card-draw gate; if the user has Necropotence or Consecrated Sphinx those are already Game Changers.

### 1.2 Game Changers List (53 cards as of February 9, 2026)

The following list is the authoritative versioned data to seed in `bracket-data.json`:

**White:** Drannith Magistrate, Humility, Serra's Sanctum, Smothering Tithe, Enlightened Tutor, Teferi's Protection, Farewell, Aura Shards

**Blue:** Consecrated Sphinx, Cyclonic Rift, Force of Will, Fierce Guardianship, Gifts Ungiven, Intuition, Mystical Tutor, Narset Parter of Veils, Rhystic Study, Thassa's Oracle, Notion Thief, Grand Arbiter Augustin IV

**Black:** Ad Nauseam, Bolas's Citadel, Braids Cabal Minion, Demonic Tutor, Imperial Seal, Necropotence, Opposition Agent, Orcish Bowmasters, Tergrid God of Fright, Vampiric Tutor

**Red:** Gamble, Jeska's Will, Underworld Breach

**Green:** Crop Rotation, Gaea's Cradle, Natural Order, Seedborn Muse, Survival of the Fittest, Worldly Tutor, Biorhythm

**Multicolor/Colorless:** Coalition Victory, Lion's Eye Diamond, Ancient Tomb, Chrome Mox, Field of the Dead, Glacial Chasm, Grim Monolith, Mana Vault, Mishra's Workshop, Mox Diamond, Panoptic Mirror, The One Ring, The Tabernacle at Pendrell Vale

**Effective date:** `2026-02-09`

[CITED: magic.wizards.com/en/news/announcements/commander-brackets-beta-update-october-21-2025 + magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026]

> **Open Question:** The count above is 51 — the WotC October 2025 list — plus Farewell and Biorhythm for February 2026 = 53 total. Verify the complete 53 against a current authoritative source before seeding. The scrollvault game-changers guide lists 53 cards as of February 2026. Use that as cross-check.

### 1.3 Curated Detection Lists for Non-Game-Changer Signals

These card-name lists live in the same `bracket-data.json` versioned file:

**Mass Land Denial (forces B4+):** Armageddon, Ravages of War, Obliterate, Jokulhaups, Wildfire, Devastation, Decree of Annihilation, Fall of the Thran, Land Equilibrium, Catastrophe, Ruination, Boom // Bust. [ASSUMED: curated from community knowledge; confirm against official sources before seeding]

**Extra Turn Spells (informational; count displayed in reasons):** Time Walk, Time Warp, Temporal Manipulation, Capture of Jingzhou, Savor the Moment, Walk the Aeons, Alrund's Epiphany, Temporal Trespass, Expropriate (removed from Game Changers Oct 2025, still extra-turns). [ASSUMED: curated from community knowledge]

**Mass Extra-Card Draw (informational):** Not recommended as a separate gate — the most powerful mass draw cards (Consecrated Sphinx, Necropotence, Rhystic Study) are already Game Changers. Show "0 mass extra-card draw" in reasons output only. [ASSUMED: requires design decision by planner]

---

## Section 2: Game Changers Data Migration (BRACKET-02)

### 2.1 What `CommanderBracketCatalog.cs` Currently Contains

**File:** `DeckFlow.Web/Models/CommanderBracketCatalog.cs` (63 lines)

Contains:
- `CommanderBracketOption` record: `Value`, `Label`, `Summary`, `TurnsExpectation`
- `CommanderBracketCatalog` static class with 5 bracket options as C# list literals
- `Find(string? value)` method (used by `ChatGptAnalysisPromptVariant`, `ClaudeAnalysisPromptVariant`, `GeminiAnalysisPromptVariant`, and the Primer variants)
- `IsCedh(string? bracketValue)` helper

**Does NOT contain:** Any Game Changers list, bracket tier thresholds, MLD lists. Those are net-new.

**Migration plan:** The 5 bracket option records (B1-B5 text data) move from `CommanderBracketCatalog.cs` into the JSON seed file as `bracketTiers[]`. The existing `CommanderBracketCatalog` class is REPLACED by new Core types that load from the cached catalog (or can remain as a thin static helper if the analysis variants still need a zero-dependency bracket lookup). The planner should decide: (a) keep `CommanderBracketCatalog.cs` in Web/Models for the analysis variants (which don't use the new Core catalog), or (b) migrate the bracket option lookup into the new Core catalog and update all callers. Option (a) is lower risk for Phase 76 — less blast radius.

### 2.2 Recommended Seed File Format

**Location:** `DeckFlow.Web/Data/bracket-data.json` — marked `<Content CopyToOutputDirectory="Always" />` in `DeckFlow.Web.csproj`.

**Schema:**
```json
{
  "effectiveDate": "2026-02-09",
  "gameChangers": [
    "Ad Nauseam",
    "Ancient Tomb",
    ...
  ],
  "massLandDenialCards": [
    "Armageddon",
    "Ravages of War",
    ...
  ],
  "extraTurnCards": [
    "Time Walk",
    "Time Warp",
    ...
  ],
  "bracketTiers": [
    {
      "number": 1,
      "name": "Exhibition",
      "label": "Bracket 1: Exhibition",
      "summary": "Theme-first showcase decks...",
      "turnsExpectation": "Expect to play at least nine turns before you win or lose.",
      "maxGameChangers": 0
    },
    ...
  ]
}
```

### 2.3 Startup Loading Pattern

**Analog 1 — HelpContentService** (`DeckFlow.Web/Services/HelpContentService.cs`): Loads from `Path.Combine(environment.ContentRootPath, "Help")` in DI constructor via `Lazy<T>`. Registered as `AddSingleton`.

**Analog 2 — ContentKbSeedLoader** (`DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs`): Called at startup from `Program.cs:255` via `await seedLoader.LoadIfPresentAsync()`. Reads a JSON file from a known path, upserts to DB.

**Recommended pattern for Game Changers:**

Create `DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs`:
- Registered as `AddSingleton<IGameChangerCatalogService, GameChangerCatalogService>()`
- Constructor takes `IWebHostEnvironment` (to locate `Data/bracket-data.json`) and `IMemoryCache`
- Loads and deserializes the JSON file on first request (lazy `Lazy<GameChangerCatalog>`) or at startup
- Caches with `IMemoryCache` key `"bracket-game-changers"`, TTL = 24h (or `SlidingExpiration = null` with `AbsoluteExpiration` set far in the future, since the data only changes with deployments)
- Returns `GameChangerCatalog` (the deserialized Core model)

Called from `BracketClassificationService` which injects `IGameChangerCatalogService`.

NOT called via a startup async block in Program.cs (unlike ContentKbSeedLoader) — the lazy singleton is sufficient since the file is local and load is fast.

### 2.4 Core Model Types

Create `DeckFlow.Core/Bracket/` namespace with:

```csharp
// DeckFlow.Core/Bracket/GameChangerCatalog.cs
public sealed record GameChangerCatalog(
    DateOnly EffectiveDate,
    IReadOnlyList<string> GameChangers,        // sorted, OrdinalIgnoreCase-compared
    IReadOnlyList<string> MassLandDenialCards,
    IReadOnlyList<string> ExtraTurnCards,
    IReadOnlyList<BracketTier> Tiers);

public sealed record BracketTier(
    int Number,
    string Name,
    string Label,
    string Summary,
    string TurnsExpectation,
    int MaxGameChangers);  // -1 = unlimited
```

```csharp
// DeckFlow.Core/Bracket/BracketClassification.cs
public sealed record BracketClassification(
    int BracketNumber,                             // 1-5
    IReadOnlyList<string> DetectedGameChangers,    // card names from deck ∩ game changers list
    IReadOnlyList<string> DetectedMassLandDenial,  // card names from deck ∩ MLD list
    IReadOnlyList<string> DetectedExtraTurnCards,  // card names from deck ∩ extra-turn list
    IReadOnlyList<SpellbookCombo>? TwoCardCombos,  // null = spellbook unavailable; empty = no combos
    bool ComboDetectionAvailable,                  // false when FindCombosAsync returns null
    string EffectiveDate);                         // from catalog, e.g. "2026-02-09"
```

```csharp
// DeckFlow.Core/Bracket/BracketClassifier.cs
public static class BracketClassifier
{
    public static BracketClassification Classify(
        IReadOnlyList<DeckEntry> entries,
        GameChangerCatalog catalog,
        CommanderSpellbookResult? comboResult);  // null = unavailable
}
```

The classifier lives in Core because Phase 77 (SCORE-02 Power axis) needs the Game Changers count signal. The `BracketClassifier.Classify()` is a pure static method — no DI, no HTTP.

---

## Section 3: Two-Card Infinite Combo Detection (BRACKET-01)

### 3.1 How `CommanderSpellbookService` Works

**File:** `DeckFlow.Web/Services/CommanderSpellbookService.cs` (317 lines)

```csharp
// Interface
Task<CommanderSpellbookResult?> FindCombosAsync(
    IReadOnlyList<DeckEntry> entries,
    CancellationToken cancellationToken = default);

// Result record
public sealed record CommanderSpellbookResult(
    IReadOnlyList<SpellbookCombo> IncludedCombos,        // fully in deck, up to 20
    IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos); // one card away, up to 15

public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,    // e.g. "Win the game", "Infinite mana"
    string Instructions,
    int? Popularity = null,
    int? ManaValueNeeded = null);
```

### 3.2 Null/Failure Semantics (Critical for BRACKET-03)

- Returns `null` on **any failure**: HTTP error, JSON parse failure, empty response, or when `main.Count == 0`
- Catches `Exception` broadly, logs at Warning level, returns `null`
- Has 30-minute `IMemoryCache` so repeated calls within a session hit cache

**Null means "unavailable", never "no combos".** This is the core semantic for bracket classification:

```csharp
// BracketClassifier must distinguish these cases
bool comboDetectionAvailable = comboResult != null;
var twoCardCombos = comboResult?.IncludedCombos
    .Where(c => c.CardNames.Count == 2)
    .ToList() ?? [];
```

### 3.3 What Counts as a "Two-Card Win Combo"

The WotC rubric says "early-game two-card combo." Commander Spellbook's `Results` array includes combo outcomes. For Phase 76, the recommended approach (per UI-SPEC §11 open question 2) is: **any detected IncludedCombo with exactly 2 cards counts as a two-card win combo**, regardless of timing. Surface it as a reason and let the pasted AI prompt nuance timing. This avoids fragile `Results` text-matching.

However: large combos (3+ cards) do NOT count as "two-card combos" for bracket gating. Only `CardNames.Count == 2` triggers the gate.

### 3.4 Reuse Pattern

The bracket classification service calls `FindCombosAsync` the same way `DeckAnalysisPacketService` does — inject `ICommanderSpellbookService` and call it after the deck is loaded. The 30-minute cache means if the user submits the same deck to both Deck Analysis and Bracket Check in the same session, the second call is free.

---

## Section 4: Mass-Land-Denial / Extra-Turns / Extra-Cards Detection

### 4.1 Recommended Detection Approach

**Signal source: curated card-name list in the versioned JSON seed file.**

Rationale:
- Oracle text matching (DeckStatClassifier pattern) is unreliable for rare edge cases (e.g., "Boom // Bust" doesn't contain "destroy all lands")
- Scryfall tagger is explicitly out of scope for the bracket classification hot path (REQUIREMENTS.md Out of Scope: "Live Scryfall call in the bracket classification hot path")
- Curated name lists are the same approach used for Game Changers — consistent, versioned, maintainable

**Detection algorithm:**

```csharp
var deckCardNames = entries
    .Where(e => e.Board == "mainboard" || e.Board == "commander")
    .Select(e => e.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var mldDetected = catalog.MassLandDenialCards
    .Where(c => deckCardNames.Contains(c))
    .ToList();

var extraTurnsDetected = catalog.ExtraTurnCards
    .Where(c => deckCardNames.Contains(c))
    .ToList();
```

### 4.2 Bracket Gating

- **MLD detected:** Forces B4+ (hard floor). Disclose each card name in reasons.
- **Extra-turn spells detected:** Informational only (displayed in reasons as "N extra-turn cards: [names]"). NOT a hard B4 gate by itself per current rubric. [CITED: scrollvault.net/guides/commander-brackets.html]
- **Mass extra-card draw:** No separate curated list needed — the most powerful draw cards are already Game Changers. Display "0 mass extra-card draw" in reasons (always from Game Changers detection only).

---

## Section 5: Paste-Artifact + 3-Variant Rendering (BRACKET-03/04)

### 5.1 ADR-0001 Constraint

REQUIREMENTS.md BRACKET-04 and STATE.md explicitly mandate: "every new artifact section (bracket, score, stale-banner if in prompt) must be hand-edited into all 3 variants — ChatGpt/Claude/Gemini — with a parity test."

**No shared base class, no shared helper method.** Confirmed by codebase: the Analysis, Comparison, Primer, MetaGap, and SetUpgrade all have 3 separate classes sharing only an interface.

### 5.2 Pattern to Mirror

**Closest analog:** `DeckFlow.Web/Services/PromptBuilders/Primer/`

```
IPrimerPromptVariant.cs          → IBracketPromptVariant.cs
ChatGptPrimerPromptVariant.cs    → ChatGptBracketPromptVariant.cs
ClaudePrimerPromptVariant.cs     → ClaudeBracketPromptVariant.cs
GeminiPrimerPromptVariant.cs     → GeminiBracketPromptVariant.cs
PrimerPromptVariantRegistry.cs   → BracketPromptVariantRegistry.cs
```

**New folder:** `DeckFlow.Web/Services/PromptBuilders/Bracket/`

### 5.3 Interface Shape

```csharp
internal interface IBracketPromptVariant
{
    AiPlatform Platform { get; }

    string Build(
        BracketClassification classification,
        int? targetBracketNumber,         // null = classify only, no target
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default);
}
```

Each variant's `Build()` contains TWO blocks:
1. **Classification block:** Tier verdict + reasons (Game Changers count, combos, MLD, extra-turns, effective-date stamp)
2. **Balancer block (when `targetBracketNumber != null` AND deck is over target):** Floor violations + starter cuts + AI-refine framing

### 5.4 Registry Pattern

Identical to `AnalysisPromptVariantRegistry` — injected `IEnumerable<IBracketPromptVariant>`, keyed by `Platform`, falls back to `AiPlatform.Default`.

### 5.5 Parity Test

File: `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs`

Following `ResultContractTests.cs` pattern:

```csharp
private static BracketPromptVariantRegistry BuildRegistry() => new(new IBracketPromptVariant[]
{
    new ChatGptBracketPromptVariant(),
    new ClaudeBracketPromptVariant(),
    new GeminiBracketPromptVariant(),
});

[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Build_ClassificationBlock_AppearsInAllThreeVariants(string platform)
{
    // asserts that the classification block key phrase appears for all 3 platforms
}

[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Build_BalancerBlock_AppearsInAllThreeVariants_WhenTargetSelected(string platform)
{
    // asserts that the balancer block appears when targetBracketNumber < classification.BracketNumber
}
```

### 5.6 Where Brackets Appear in the Artifact

The bracket paste artifact is STANDALONE — it is NOT folded into the existing `/deck-analysis` prompt (that is Phase 77 SCORE). The balancer artifact is shown in a `details.result-panel.nested-panel` on the `/bracket` page (per UI-SPEC §3 wireframe and §5 components).

---

## Section 6: Effective-Date Stamping (BRACKET-05)

The effective date is sourced from `GameChangerCatalog.EffectiveDate` (a `DateOnly` or `string` from the JSON file, e.g. `"2026-02-09"`). It threads through:

1. `BracketClassification.EffectiveDate` (passed from the catalog)
2. `BracketViewModel.EffectiveDate` (from the service result)
3. `Views/Deck/Bracket.cshtml` — renders the `.bracket-stamp` block (§5 components)
4. All three prompt variant `Build()` outputs — each variant hand-codes the stamp line:
   - Example: `"Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps."`

**Graceful staleness:** The JSON file is never live-updated mid-deployment. "Stale" here means the JSON file in the running deployment may be behind the latest WotC update. The effective-date stamp + AI re-confirm instruction is the degradation path — the AI will notice if a card's status has changed.

---

## Section 7: Flag + Tool-Registry Wiring (BRACKET-05)

### 7.1 Flag Registration

**Template analog:** `analysis.manabase.tap-analyzer` (Phase 75, seeded FALSE in both dialects)

**4 files to change:**

**A. `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`**
- Add to `PostgresSeedSql`:
  ```sql
  ('tool.bracket.enabled', FALSE),
  ```
- Add to `SqliteSeedSql`:
  ```sql
  ('tool.bracket.enabled', 0),
  ```
- Pattern: `ON CONFLICT (key) DO NOTHING` — preserves operator-set value on re-bootstrap (FLAG-01 contract)

**B. `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`**
- Add entry:
  ```csharp
  ["tool.bracket.enabled"] =
      "Enable the Bracket Check tool — auto-classify a Commander deck into its official 1-5 bracket and generate a balancer prompt. Off = byte-identical to pre-Phase-76.",
  ```

**C. `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs`**
- Add `[InlineData("tool.bracket.enabled")]` to `Describe_EverySeededFlag_HasNonEmptyDescription` theory

**D. `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs`**
- Add `[InlineData("tool.bracket.enabled", false)]` to seed test theory

### 7.2 Tool Registry Entry

**File:** `DeckFlow.Web/Services/Tools/ToolRegistry.cs`

Add to `Definitions` (in the Analyze block, after `manabase` per UI-SPEC §2):

```csharp
Create("bracket", "Bracket", "/bracket", ToolNavSection.Analyze,
    "tool.bracket.enabled", false /*core*/,
    "Bracket Check",
    "Classify a Commander deck into its official 1-5 bracket from Game Changers, two-card combos, and mass-land-denial — then generate a balancer prompt to hit a target bracket. No tutor-counting.",
    "bracket", DeckPageTab.Bracket, false /*isPrimaryTile*/),
```

### 7.3 DeckPageTab Enum

**File:** `DeckFlow.Web/Models/DeckPageTab.cs`

Add: `Bracket = 15,` (next free value — existing max is `Manabase = 14`; UI-SPEC §2 confirms `15`)

### 7.4 Tool Tile Icon

**File:** `DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml`

Add before `default:`:
```razor
case "bracket":
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><line x1="4" y1="16" x2="6" y2="16"/><line x1="8" y1="13" x2="10" y2="13"/><line x1="12" y1="10" x2="14" y2="10"/><line x1="16" y1="7" x2="16" y2="7"/><polyline points="4,16 6,16 6,13 10,13 10,10 14,10 14,7 17,7"/></svg>
    break;
```

(SVG from UI-SPEC §2 — five ascending steps staircase glyph)

### 7.5 Nav Strip

**`_DeckToolTabs.cshtml` does NOT need to be edited.** The tab strip iterates the registry — the `Bracket = 15` tab appears automatically once the registry entry exists and the flag is ON.

### 7.6 Admin Warning

The "admin warning when disabled" in the registry creates the operator-facing description shown on `/Admin/Flags` (resolved via `FeatureFlagCatalog.Describe("tool.bracket.enabled")`). There is no special admin-warning UI component — the flag entry + description IS the warning. [VERIFIED via codebase inspection of `FeatureFlagCatalog.cs` and `ToolDefinition.cs`]

---

## Architecture Patterns

### System Architecture Diagram

```
Browser (POST /bracket)
    |
    v
BracketController
    |──── IDeckEntryLoader ──────────────────► Archidekt/Moxfield API
    |──── IGameChangerCatalogService ─────────► bracket-data.json (local, IMemoryCache)
    |──── ICommanderSpellbookService ─────────► CommanderSpellbook API (nullable)
    |──── BracketClassifier.Classify() ──────► BracketClassification (pure, Core)
    |──── BracketPromptVariantRegistry.Build() ► ChatGpt/Claude/Gemini variants
    |
    v
BracketViewModel ──► Bracket.cshtml ──► HTML (badge + reasons + violations + stamp + textarea)
```

### Recommended Project Structure (net-new files)

```
DeckFlow.Core/
└── Bracket/
    ├── BracketClassification.cs      # result record
    ├── BracketClassifier.cs          # pure static classifier
    ├── GameChangerCatalog.cs         # catalog record + BracketTier record
    └── BracketRubricThresholds.cs    # optional: constants for tier thresholds

DeckFlow.Web/
├── Data/
│   └── bracket-data.json             # versioned seed (effective-date + card lists + tier text)
├── Controllers/
│   └── BracketController.cs          # GET /bracket + POST /bracket + [FeatureFlagGate]
├── Models/
│   ├── BracketViewModel.cs
│   └── BracketRequest.cs
├── Services/
│   ├── Bracket/
│   │   ├── IGameChangerCatalogService.cs
│   │   ├── GameChangerCatalogService.cs    # loads JSON → IMemoryCache at first call
│   │   ├── IBracketClassificationService.cs
│   │   └── BracketClassificationService.cs # orchestrates import+combo+classify+artifact
│   └── PromptBuilders/
│       └── Bracket/
│           ├── IBracketPromptVariant.cs
│           ├── ChatGptBracketPromptVariant.cs
│           ├── ClaudeBracketPromptVariant.cs
│           ├── GeminiBracketPromptVariant.cs
│           └── BracketPromptVariantRegistry.cs
└── Views/
    └── Deck/
        └── Bracket.cshtml                  # @model BracketViewModel

DeckFlow.Web.Tests/
└── Bracket/
    ├── BracketClassifierTests.cs           # unit tests for pure classifier logic
    ├── BracketViewRenderTests.cs           # IRazorViewEngine flag-OFF/ON test
    └── BracketPromptVariantParityTests.cs  # 3-platform parity test
```

### Controller Pattern (mirror ManabaseController exactly)

`BracketController` inherits from `DeckToolControllerBase`:

```csharp
[HttpGet("/bracket")]
[FeatureFlagGate("tool.bracket.enabled")]
public IActionResult Bracket() => View("Bracket", new BracketViewModel());

[HttpPost("/bracket")]
[ValidateAntiForgeryToken]
[FeatureFlagGate("tool.bracket.enabled")]
public async Task<IActionResult> Bracket(BracketRequest request)
{
    using var timeoutScope = CreateTimeoutScope(LookupTimeout);
    try { ... }
    catch (OperationCanceledException) { ... }
    catch (InvalidOperationException) { ... }
    catch (HttpRequestException) { ... }
    catch (Exception) { ... }
}
```

The POST has a single action (no separate "load" phase like Manabase) since there are no pre-detection override steps. Per UI-SPEC §3 controller note: "load vs analyze can collapse to a single POST."

### Anti-Patterns to Avoid

- **Treat null Spellbook as zero combos:** Must disclose "combo detection unavailable" (BRACKET-03, UI-SPEC §6 States table)
- **Gate on tutor count:** Explicitly removed from rubric October 2025 (BRACKET-01)
- **Gate extra-turn spells as B4 hard floor:** They are informational, not a hard gate
- **Extract shared prompt text across 3 variants:** ADR-0001 forbids; reverts have happened before (STATE.md pitfalls)
- **Put bracket data in a .cs literal:** BRACKET-02 requires a versioned file
- **Call Scryfall in the bracket classification hot path:** Out of scope per REQUIREMENTS.md

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Two-card combo detection | Custom API client for Commander Spellbook | Existing `ICommanderSpellbookService.FindCombosAsync` |
| Deck URL import | URL parsing + HTTP client for Archidekt/Moxfield | Existing `IDeckEntryLoader` |
| Feature flag gating | Custom middleware or view logic | `[FeatureFlagGate("tool.bracket.enabled")]` attribute |
| 3-variant routing | Switch statement on platform string | `BracketPromptVariantRegistry` (same pattern as `AnalysisPromptVariantRegistry`) |
| HTTP resilience | Manual retry/timeout logic | Existing Polly pipeline via `ICommanderSpellbookService` |
| Request timeout | `CancellationToken` + manual timer | `DeckToolControllerBase.CreateTimeoutScope(LookupTimeout)` |

---

## Common Pitfalls

### Pitfall 1: Silent "no combos" on null Spellbook response
**What goes wrong:** BracketClassifier treats `comboResult == null` as "no combos found," classifies a two-card-combo deck as B3 when it should be B4.
**Why it happens:** Pattern-matching on null without a separate `ComboDetectionAvailable` flag.
**How to avoid:** `BracketClassification` has a boolean `ComboDetectionAvailable` field. When false, the view and prompt both show the disclosure note from UI-SPEC §6 States ("Combo detection is temporarily unavailable…") and do NOT assert "0 combos."
**Warning signs:** Test — make `FindCombosAsync` return null and assert classification does not claim "0 two-card combos."

### Pitfall 2: Tutor detection gates bracket
**What goes wrong:** Analyst counts tutors (Demonic Tutor, etc.) and gates brackets, following old pre-October 2025 logic.
**Why it happens:** Training data and community resources pre-October 2025 all mention tutor gating.
**How to avoid:** Tutors on the Game Changers list (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) are counted as Game Changers (which they are), NOT as a separate tutor count. No separate "tutor gate."
**Warning signs:** Any bracket classification code with a `tutorCount` variable as a threshold input.

### Pitfall 3: Byte-identity regression when flag is OFF
**What goes wrong:** The bracket page flag gate works, but some new CSS class or nav entry leaks into other pages when the flag is OFF.
**Why it happens:** CSS in `site-common.css` is always served; new nav entries leak if the registry filter has a bug.
**How to avoid:** The tool registry already filters disabled tools before rendering nav and tiles. The `[FeatureFlagGate]` attribute returns 404 for the route. The IRazorViewEngine render test (`BracketViewRenderTests`) verifies byte-identity with existing pages — but the more important test is that `/bracket` returns 404 when flag is OFF.
**Warning signs:** `FeatureFlagGateAttributeTests.cs` covers the gate attribute directly; add a `BracketController_ReturnsFlaggedOut_WhenFlagOff` test.

### Pitfall 4: Effective-date string format mismatch
**What goes wrong:** JSON has `"2026-02-09"`, C# parses as `DateOnly`, view renders in locale-specific format.
**Why it happens:** `DateOnly.ToString()` is culture-sensitive.
**How to avoid:** Store as string in catalog and display verbatim; or use `DateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)` consistently. Don't rely on default `ToString()`.

### Pitfall 5: B4 vs B5 disambiguation
**What goes wrong:** Classifier auto-promotes a B4 deck to B5 based on heuristics, incorrectly labeling non-cEDH decks as cEDH.
**Why it happens:** There is no crisp card-count gate for B5 — the WotC rubric describes it as "metagame-tuned."
**How to avoid:** Phase 76 classifies B4 for any deck meeting B4 criteria. B5 is reserved for decks explicitly flagged in `BracketRequest` as cEDH context, OR optionally if total Game Changers count is very high (e.g., >= 10). Disclose the ambiguity in the artifact text. This is an open question for the planner — see §11.

### Pitfall 6: FeatureFlagCatalog/Seed tests fail
**What goes wrong:** Adding `tool.bracket.enabled` to the flag seed without updating the catalog description → `FeatureFlagCatalogTests` fails in CI.
**Why it happens:** The guard test is `[InlineData("tool.bracket.enabled")]` in `FeatureFlagCatalogTests` — not a surprise if you know about it.
**How to avoid:** Both files must be updated in the same commit: `FeatureFlagStore.cs` seed + `FeatureFlagCatalog.cs` description + both test files.

---

## Code Examples

### Example 1: Classifier core logic

```csharp
// Source: designed for DeckFlow.Core/Bracket/BracketClassifier.cs
public static BracketClassification Classify(
    IReadOnlyList<DeckEntry> entries,
    GameChangerCatalog catalog,
    CommanderSpellbookResult? comboResult)
{
    var deckNames = entries
        .Where(e => e.Board is "mainboard" or "commander")
        .Select(e => e.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var detectedGCs = catalog.GameChangers
        .Where(gc => deckNames.Contains(gc))
        .ToList();
    var detectedMld = catalog.MassLandDenialCards
        .Where(c => deckNames.Contains(c))
        .ToList();
    var detectedExtraTurns = catalog.ExtraTurnCards
        .Where(c => deckNames.Contains(c))
        .ToList();

    bool comboAvailable = comboResult != null;
    var twoCardCombos = comboResult?.IncludedCombos
        .Where(c => c.CardNames.Count == 2)
        .ToList() ?? [];

    int bracketNumber;
    if (detectedMld.Count > 0 || twoCardCombos.Count > 0 || detectedGCs.Count >= 4)
        bracketNumber = 4;  // B4+ hard floor
    else if (detectedGCs.Count >= 1)
        bracketNumber = 3;  // Game Changers present → at least B3
    else
        bracketNumber = 1;  // no signals → exhibition/core (further refinement TBD)

    return new BracketClassification(
        bracketNumber, detectedGCs, detectedMld, detectedExtraTurns,
        twoCardCombos.Count > 0 ? twoCardCombos : null,
        comboAvailable,
        catalog.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
```

### Example 2: Catalog service startup load

```csharp
// Source: designed for GameChangerCatalogService.cs (mirrors HelpContentService lazy-init pattern)
public sealed class GameChangerCatalogService : IGameChangerCatalogService
{
    private const string CacheKey = "bracket:game-changer-catalog";
    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;

    public GameChangerCatalogService(IWebHostEnvironment env, IMemoryCache cache)
    {
        _dataFilePath = Path.Combine(env.ContentRootPath, "Data", "bracket-data.json");
        _cache = cache;
    }

    public GameChangerCatalog GetCatalog()
    {
        if (_cache.TryGetValue<GameChangerCatalog>(CacheKey, out var cached) && cached is not null)
            return cached;

        var json = File.ReadAllText(_dataFilePath);
        var catalog = JsonSerializer.Deserialize<GameChangerCatalog>(json, JsonOptions)!;
        _cache.Set(CacheKey, catalog, TimeSpan.FromHours(24));
        return catalog;
    }
}
```

### Example 3: Flag-OFF IRazorViewEngine render test (mirror of ManabaseViewRenderTests.cs)

```csharp
// Source: follows DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs exactly
[Fact]
public async Task OffState_FlagFalse_BracketPageReturns404()
{
    // Use FeatureFlagGateAttribute integration test (per FeatureFlagGateAttributeTests.cs)
    // OR test the controller action directly with a fake flag cache returning false
}
```

The view render test structure (from `ManabaseViewRenderTests`) is the authoritative test pattern for verifying flag-gated view content. Phase 76 should add `BracketViewRenderTests.cs` following the same `IRazorViewEngine` + `FakeFeatureFlagCache` approach.

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|-----------------|--------|
| Tutor count gates brackets (pre-Oct 2025) | Tutors removed as bracket gate; tutors on Game Changers list count as GCs | Do NOT gate on separate tutor count |
| Game Changers list had 57 cards (2024 initial) | October 2025: removed 10 cards → ~47; February 2026: added 2 → 53 | Use current 53-card list as seed |
| Bracket system was "proposed" / "beta" | Still labeled beta but widely adopted as de-facto standard | Note "beta" status in artifact prose |

---

## Open Questions (RESOLVED)

1. **B4 vs B5 auto-distinction:** RESOLVED: plans encode the cEDH Game-Changer threshold (`BracketRubricThresholds.CedhGameChangerCount = 10`) for auto-B5; auto-classification otherwise stops at B4 (per 76-01).
   - What we know: WotC rubric doesn't provide a crisp card-count gate for B5 vs B4
   - What's unclear: Should Phase 76 ever auto-assign B5, or always stop at B4 for decks meeting the hard floors?
   - Recommendation: Default to B4 for all hard-floor violations; let the pasted prompt ask the AI to judge B4 vs B5 based on the cEDH metagame fit. Avoids mislabeling casual-ish high-power decks as cEDH.

2. **B1 vs B2 distinction:** RESOLVED: zero-signal decks default to B2 via `ZeroSignalBracket = 2` (per 76-01).
   - What we know: Both have 0 Game Changers, no MLD, no two-card combos
   - What's unclear: How to distinguish Exhibition (B1) from Core (B2) without subjective assessment
   - Recommendation: Default all zero-signal decks to B2 (Core); add a `BracketRequest.DeckIntent` optional field ("exhibition") that lets the user self-declare B1. Pure auto-classification cannot reliably distinguish B1/B2.

3. **Extra-turn spells as informational vs gate:** RESOLVED: extra-turns are informational-only, NOT a hard gate (behavior locked in 76-01 classifier unit tests).
   - What we know: scrollvault.net cites "3+ extra-turn spells → B3 minimum" as a signal
   - What's unclear: Is this now a hard floor or just a soft signal after October 2025 update?
   - Recommendation: Treat as informational for Phase 76 (display count in reasons, do not change bracket). Flag for discuss/plan if community sources suggest otherwise.

4. **Phase 77 Control-axis dependency noted in UI-SPEC §11 open question 1:** RESOLVED: `BracketClassifier.Classify` and the Core-local `TwoCardCombo` record are designed for Phase 77 Power-axis reuse (GC count + combo signals), keeping `DeckFlow.Core` free of any `DeckFlow.Web` reference.
   - Recommendation: Build the MLD/extra-turns detector in `DeckFlow.Core/Bracket/BracketClassifier.cs` with a shared interface so Phase 77 can import the same detection logic without duplicating it. The bracket classifier's category-detection maps well to what a "control" axis would need.

---

## Environment Availability

> Phase 76 has no external dependencies beyond what Phase 75 already validated. The app uses RestSharp + Polly for all HTTP (already wired). The Commander Spellbook API is already used by the existing packet services.

| Dependency | Required By | Available | Notes |
|------------|------------|-----------|-------|
| .NET 10 SDK | Build | ✓ | same as Phase 75 |
| Commander Spellbook API | Combo detection | ✓ | existing `ICommanderSpellbookService` |
| `bracket-data.json` | Game Changers catalog | NEW — must be created | no runtime dependency |
| `IMemoryCache` | Catalog caching | ✓ | already registered in DI |
| `IWebHostEnvironment` | JSON file path | ✓ | already used by HelpContentService |

---

## Validation Architecture

> `workflow.nyquist_validation = true` in `.planning/config.json` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (uses xunit.runner.visualstudio 3.1.4) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -x --filter "FullyQualifiedName~Bracket" --no-build` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BRACKET-01 | Classifier returns B4 when GC count ≥ 4 | unit | `dotnet test ... --filter "FullyQualifiedName~BracketClassifierTests"` | ❌ Wave 0 |
| BRACKET-01 | Classifier returns B4 when two-card combo present | unit | same | ❌ Wave 0 |
| BRACKET-01 | Classifier returns B4 when MLD present | unit | same | ❌ Wave 0 |
| BRACKET-01 | Classifier returns B3 when 1-3 GCs, no combo, no MLD | unit | same | ❌ Wave 0 |
| BRACKET-01 | Extra-turn spells are informational only (no bracket change) | unit | same | ❌ Wave 0 |
| BRACKET-02 | Game Changers JSON file loads + IMemoryCache populated | unit | `dotnet test ... --filter "FullyQualifiedName~GameChangerCatalogServiceTests"` | ❌ Wave 0 |
| BRACKET-02 | Seed flag `tool.bracket.enabled` defaults to OFF | unit | `dotnet test ... --filter "FullyQualifiedName~FeatureFlagStoreSeedTests"` | ❌ update existing |
| BRACKET-02 | Flag description in FeatureFlagCatalog is non-empty | unit | `dotnet test ... --filter "FullyQualifiedName~FeatureFlagCatalogTests"` | ❌ update existing |
| BRACKET-03 | null Spellbook → ComboDetectionAvailable=false in classification | unit | same BracketClassifierTests | ❌ Wave 0 |
| BRACKET-03 | Balancer block lists floor violations for target bracket | unit | `dotnet test ... --filter "FullyQualifiedName~BracketPromptVariantParityTests"` | ❌ Wave 0 |
| BRACKET-04 | Classification block appears in all 3 prompt variants | unit | same | ❌ Wave 0 |
| BRACKET-04 | Balancer block appears in all 3 variants when target set | unit | same | ❌ Wave 0 |
| BRACKET-05 | Effective-date appears in artifact prose for all 3 variants | unit | same | ❌ Wave 0 |
| BRACKET-05 | Flag OFF → Bracket.cshtml renders no bracket-badge markup | render | `dotnet test ... --filter "FullyQualifiedName~BracketViewRenderTests"` | ❌ Wave 0 |
| BRACKET-05 | Flag ON → Bracket.cshtml renders bracket-badge markup | render | same | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -x --filter "FullyQualifiedName~Bracket" --no-build`
- **Per wave merge:** Full suite: `dotnet test DeckFlow.sln --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs` — covers BRACKET-01 classifier unit tests (GC threshold, MLD gate, combo gate, extra-turn informational-only, null-combo disclosure)
- [ ] `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` — covers BRACKET-04 parity (classification block + balancer block in all 3 variants)
- [ ] `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs` — covers BRACKET-05 flag-OFF/ON view invariant (IRazorViewEngine, following ManabaseViewRenderTests.cs)
- [ ] Update `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — add `[InlineData("tool.bracket.enabled")]`
- [ ] Update `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — add `[InlineData("tool.bracket.enabled", false)]`

---

## Security Domain

> `security_enforcement` not set to false in config — section required.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Bracket tool is public (no auth required, same as all other deck tools) |
| V3 Session Management | No | No session state used by bracket classification |
| V4 Access Control | No | No privileged endpoint |
| V5 Input Validation | Yes | `BracketRequest` model binding: deck URL validated by existing `IDeckEntryLoader`; target bracket number validated as int 1-5; `[ValidateAntiForgeryToken]` on POST |
| V6 Cryptography | No | No cryptographic operations |

### Known Threat Patterns for This Phase

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed deck URL / injection | Tampering | Existing `IDeckEntryLoader` validates URLs; same threat surface as Manabase |
| Forged POST (CSRF) | Tampering | `[ValidateAntiForgeryToken]` on POST `/bracket` (mandatory per codebase convention, same as ManabaseController) |
| Out-of-range target bracket | Tampering | Validate `targetBracketNumber` is 1-5 in controller before passing to service |
| Spellbook API response injection | Tampering | Existing `CommanderSpellbookService` JSON parsing is already defensive (try/catch on parse, null-return) |

---

## Project Constraints (from CLAUDE.md)

- **No new packages:** Phase 76 explicitly constrained to in-solution technology per REQUIREMENTS.md Out-of-Scope
- **RestSharp + Polly v8 pattern:** Bracket tool's HTTP calls go through `ICommanderSpellbookService` (already uses this pattern) — do NOT introduce new HTTP clients
- **ADR-0001 holds:** No shared helper across ChatGpt/Claude/Gemini bracket variants
- **Flag key namespace:** `tool.bracket.enabled` (follows `tool.*` naming convention)
- **Theme CSS:** Net-new classes go in `site-common.css`, not individual theme files (3 classes: `.bracket-badge`, `.bracket-violation`, `.bracket-stamp`)
- **Build coupling:** No new TypeScript; CSS only in `site-common.css`
- **Public repo:** No secrets in commits; seed JSON contains only public card names and public bracket data
- **Commits:** Plain default-author commits (`luntc1972`), no Co-Authored-By trailer
- **Testing:** UI testing via `scripts/run-web-test.sh` (`DECKFLOW_DISABLE_AUTO_BROWSER=true`); xUnit for unit + view render tests; browser screenshots for theme/mobile verification before phase close

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Extra-turn spells are informational only (not a hard B4 gate) in the current Oct-2025+ rubric | §1.1, §4.2 | Would need to add extra-turn list as a B4 hard-floor gate; minor implementation change |
| A2 | Mass extra-card draw has no separate curated list needed (covered by Game Changers) | §4.2 | Would need a third curated card list; adds a category tag to the reasons output |
| A3 | MLD curated list: Armageddon, Ravages of War, Obliterate, Jokulhaups, Wildfire, Devastation, Decree of Annihilation, Fall of the Thran, Land Equilibrium, Catastrophe, Ruination, Boom // Bust | §1.3 | Incomplete list misses some MLD cards; user discovers during bracket check and reports; low severity since it's a versioned file |
| A4 | Extra-turn curated list: Time Walk, Time Warp, Temporal Manipulation, Capture of Jingzhou, Savor the Moment, Walk the Aeons, Alrund's Epiphany, Temporal Trespass, Expropriate | §1.3 | Same as A3 — updateable via JSON file |
| A5 | B1 vs B2 cannot be auto-distinguished; default zero-signal decks to B2 | §Open Questions #2 | Users who intend B1/Exhibition will see B2; acceptable since they can self-declare |
| A6 | Game Changers list is 53 cards as sourced; the exact 53 should be cross-checked against scrollvault.net/guides/game-changers.html before seeding | §1.2 | Minor count discrepancy if a card was missed; correctable by updating JSON file |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` — verified existing content, confirmed what the file currently holds and does NOT hold
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — verified null semantics, result types, FindCombosAsync signature
- `DeckFlow.Web/Services/Tools/ToolRegistry.cs` — verified exact Create() pattern and all existing entries
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — verified seed SQL pattern (ON CONFLICT DO NOTHING, both dialects)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — verified description format
- `DeckFlow.Web/Controllers/ManabaseController.cs` — verified controller shape (DeckToolControllerBase, FeatureFlagGate, RunGuardedAsync)
- `DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml` — verified existing icons + default SVG dimensions
- `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` — verified IRazorViewEngine test pattern for flag-OFF/ON
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — verified InlineData test guard pattern
- `DeckFlow.Web.Tests/ResultContractTests.cs` — verified 3-platform parity test pattern
- `DeckFlow.Web/Services/PromptBuilders/Primer/` (5 files) — verified 3-variant + registry pattern
- `.planning/phases/76-bracket-classifier-balancer/UI-SPEC.md` — verified full design contract

### Secondary (MEDIUM confidence)
- [WotC Commander Brackets Beta Update — October 21, 2025](https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-october-21-2025) — Game Changers list after Oct 2025 update; 10 removals confirmed
- [WotC Commander Brackets Beta Update — February 9, 2026](https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026) — 2 additions (Farewell + Biorhythm) confirmed
- [scrollvault.net Commander Brackets Guide](https://scrollvault.net/guides/commander-brackets.html) — bracket thresholds (B1-B5 GC limits, B4 hard floor for MLD + two-card combos), extra-turn soft signal
- [commanderbrackets.com](https://commanderbrackets.com/) — B3 ≤ 3 GCs threshold confirmed

### Tertiary (LOW confidence)
- Curated MLD and extra-turn card name lists (§1.3): assembled from training knowledge + community patterns; confirm before seeding

---

## Metadata

**Confidence breakdown:**
- Bracket rubric (thresholds, tutor removal): HIGH — two WotC official sources + community cross-check
- Game Changers list (53 cards): MEDIUM — WotC sources list cards but no single canonical text dump; count and names cross-checked across 3 sources
- Architecture patterns: HIGH — all verified in actual codebase files with line references
- Pitfalls: HIGH — null-combo handling and tutor gate verified from codebase + requirements

**Research date:** 2026-06-28
**Valid until:** 2026-07-28 for architecture (stable); 2026-07-05 for Game Changers list (WotC may update; the JSON seed file handles this gracefully)
