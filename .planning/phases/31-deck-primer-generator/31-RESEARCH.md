# Phase 31: Deck Primer Generator — Research

**Researched:** 2026-06-08
**Domain:** ASP.NET Core MVC service pipeline + prompt-builder variant pattern + localStorage persistence + zip artifact store
**Confidence:** HIGH — all findings derived from reading the live codebase; no training-data assumptions

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-1 — Spike gates execution only, not planning**
Plan all plans up front. The prompt-builder plan specifies BOTH combo-ranking branches
(priority-ranked when spike verdict is "data sufficient", AI-ranked fallback otherwise).
The spike's recorded verdict selects the branch at execution time. PRM-01 is the first
execution unit; verdict recorded in `31-SPIKE.md` (or STATE decision doc).

**D-2 — Combo grounding: two structurally separated blocks + null disclosure**
Prompt contains a fenced "Known Combos (ground truth — do not speculate)" block from
Commander Spellbook, then a separate fenced "Speculative synergies (you propose)" ask.
The two are never merged. Null return from `FindCombosAsync` emits an explicit disclosure
line: "No verified combos available — treat all synergies as speculative."

**D-3 — Bracket change applies preset but preserves per-bracket custom toggles**
First visit to a bracket seeds that bracket's section preset. Subsequent visits restore
the user's saved custom set. Persistence: localStorage keyed per bracket. Bracket-scoped
section gating enforced regardless of stored toggles.

**D-4 — Gemini paste-cap: defensive char-cap guard like the analysis variant**
Mirror `GeminiAnalysisPromptVariant.DefensivePromptCharCap` (=50000). Threshold set by
PRM-01 spike byte-size measurement. Trim lowest-priority sections to fit with disclosure.

### Carried-Forward Locked Invariants

- Mirror analysis architecture: `DeckPrimerPacketService` + primer variant registry + 3
  decoupled variant files (ChatGPT/Claude/Gemini). No shared prose across variants (ADR 0001).
- `PrimerAllowedNames` first: add primer entries to `PacketArtifactStore` allowlist before
  any other artifact-store work. `ReadEntries` throws on unlisted names.
- `{ get; init; }` guard: every new record preserves `init` accessor. Include a
  System.Text.Json round-trip test per round-tripped record.

### Claude's Discretion

None surfaced in discussion.

### Deferred Ideas (OUT OF SCOPE)

- Gemini full-section paste-limit workaround beyond the defensive trim (deferred to v1.6).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRM-01 | Combo-data spike: Spellbook `Instructions` richness verdict + cEDH primer byte-size measurement | Section 1 (spike design) |
| PRM-02 | Deck Primer page as fourth workflow tab; decklist load via existing import flow | Section 5 (service pipeline pattern) |
| PRM-03 | Bracket 1–5 selection pre-applies section preset; gates bracket-scoped sections | Section 2 (section catalog model) + Section 3 |
| PRM-04 | Toggle individual sections from 31-section catalog in 5 collapsible groups | Section 2 (section catalog model) |
| PRM-05 | Ground-truth combo block + speculative synergies ask + null disclosure | Section 3 (combo grounding) |
| PRM-06 | Matchup sections bracket-routed: EdhTop16 archetypes (bracket 5) vs 5 generic buckets (1–4) | Section 4 (matchup routing) |
| PRM-07 | Ground identity/engine/mulligan sections with category-knowledge distribution numbers | Section 5 (service pipeline) |
| PRM-08 | Combo lines ranked by priority when spike sufficient; AI-ranked fallback | Section 1 + Section 3 |
| PRM-09 | Per-AI variants; PacketArtifactStore zip round-trip with PrimerAllowedNames | Section 5 (variant pattern) |
| PRM-10 | Section selection persists per bracket in localStorage across visits | Section 6 (persistence) |
| PRM-11 | Collapsed group headers show selected-count badges | Section 6 (persistence) |
| PRM-12 | Each section exposes help text | Section 2 (section catalog model) |
</phase_requirements>

---

## Summary

Phase 31 adds a fourth paste-ready workflow (peer of DeckAnalysis / DeckComparison /
CedhMetaGap). The entire implementation path is a brownfield mirror of existing
patterns — the service pipeline, variant registry, zip allowlist, and localStorage
persistence all have working precedents in the codebase that can be followed
file-for-file. No new third-party packages are needed.

The highest-ambiguity item is PRM-01 (the spike): the Spellbook `Instructions` field
exists and carries meaningful text (capped at 300 chars by the existing parser), but
whether that is sufficient for priority-ranking without extra fields (piece count,
resource cost) is a factual question about the live API response that only the spike
can answer. Everything else in the phase is pre-decided: the combo block structure
(D-2), the matchup routing (PRM-06), the Gemini cap pattern (D-4), and the
per-bracket localStorage scheme (D-3) all have clear implementation targets.

The `{ get; init; }` invariant and the `PrimerAllowedNames`-first rule are the two most
likely execution pitfalls. Both are pre-empted by CONTEXT.md's carried-forward
invariants and must appear in the earliest plan waves.

**Primary recommendation:** Implement in this wave order: (W0) PrimerAllowedNames +
section catalog model + DTOs with round-trip tests; (W1) spike harness + verdict
recording; (W2) `DeckPrimerPacketService` + 3 variant files; (W3) controller +
Razor view + localStorage TS module.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Combo lookup (Spellbook) | API / Backend | — | Network I/O; already centralized in `CommanderSpellbookService` |
| Category distribution (ramp/draw/interaction/tutor) | API / Backend | — | DB read via `ICategoryKnowledgeStore`; matches analysis pattern |
| EdhTop16 named archetypes | API / Backend | — | Network I/O via `IEdhTop16Client` |
| Prompt assembly (3 AI variants) | API / Backend | — | Pure CPU; mirrors analysis variant registry |
| Zip build/load | API / Backend | — | Pure CPU; `PacketArtifactStore` static methods |
| Section catalog (31 entries, 5 groups) | API / Backend | Frontend Server (SSR) | Catalog lives in a C# static class; Razor renders it |
| Section selection persistence | Browser / Client | Frontend Server (no-JS path) | localStorage primary; hidden-field form submit as progressive fallback |
| Bracket preset seeding | Browser / Client | Frontend Server (no-JS path) | Preset applied on bracket change; server provides preset data-attribute |
| Selected-count badges | Browser / Client | — | JS reads checkboxes and updates badge spans |
| Gemini char-cap guard | API / Backend | — | `DeckPrimerPacketService` or Gemini variant trims before returning |
| Decklist import | API / Backend | — | Existing `LoadDeckEntriesAsync` pattern reused unchanged |

---

## Standard Stack

No new packages required. All libraries below are already in the solution.

### Core (existing, reused)

| Library | Version in solution | Purpose | Why Standard |
|---------|---------------------|---------|--------------|
| ASP.NET Core MVC 10 | 10.0 | Controller + Razor view | Pinned tech stack |
| RestSharp | 114.0.0 | HTTP to Spellbook / EdhTop16 | Existing all-egress pattern |
| Polly 8.x | 8.x | Resilience pipeline | Existing named pipeline provider |
| System.Text.Json (inbox) | .NET 10 | DTO serialization / zip JSON | Already used everywhere |
| Microsoft.Data.Sqlite / Npgsql | 10.0.0 | Category knowledge DB read | Via `ICategoryKnowledgeStore` |
| TypeScript 6.0.2 | 6.0.2 | Section-selection persistence module | Existing TS build pipeline |

[VERIFIED: codebase grep] All packages confirmed present in solution files.

**Installation:** No new packages. No `npm install` step.

---

## Package Legitimacy Audit

No new external packages are introduced in this phase. All code references existing
DeckFlow dependencies.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
Browser                 ASP.NET Core (Kestrel)            External APIs
  |                            |
  | GET /primer                |
  |--------------------------->| PrimerController.Index
  |                            |  └─ Render Razor view
  |<---------------------------|     (section catalog, bracket selector)
  |
  | (JS) localStorage read
  | Apply bracket preset / restore saved toggles
  | Render selected-count badges
  |
  | POST /primer/build         |
  |--------------------------->| PrimerController.BuildPacket
  |  [bracket, selectedSections,|
  |   deckSource, aiPlatform]  |
  |                            |  IDeckPrimerPacketService.BuildAsync
  |                            |    ├─ LoadDeckEntriesAsync (Moxfield/Archidekt/paste)
  |                            |    ├─ ICommanderSpellbookService.FindCombosAsync
  |                            |    │    └─ null → D-2 disclosure text
  |                            |    ├─ ICategoryKnowledgeStore.GetCategoryRowsForCommanderAsync
  |                            |    │    └─ count ramp/draw/interaction/tutor rows
  |                            |    ├─ IEdhTop16Client (bracket 5 only)
  |                            |    │    └─ named archetypes
  |                            |    └─ PrimerPromptVariantRegistry.Build(platform, ...)
  |                            |         ├─ ChatGptPrimerPromptVariant
  |                            |         ├─ ClaudePrimerPromptVariant
  |                            |         └─ GeminiPrimerPromptVariant (DefensiveCharCap)
  |                            |
  |                            |  PacketArtifactStore.BuildPrimerZip(...)
  |                            |    └─ PrimerAllowedNames (allowlist first)
  |<---------------------------| DeckPrimerPacketResult → Razor result view
  |                            |
  | (JS) localStorage write    |
  | Save section toggles keyed |
  | per bracket                |
```

### Recommended Project Structure

```
DeckFlow.Web/
├── Controllers/
│   └── PrimerController.cs           # new — mirrors DeckController
├── Models/
│   ├── PrimerSectionCatalog.cs       # new — 31-section catalog (mirrors AnalysisQuestionCatalog)
│   ├── PrimerRequest.cs              # new — { DeckSource, Bracket, SelectedSectionIds, AiPlatform, ... }
│   └── PrimerResult.cs               # new — { PrimerPromptText, InputSummary, ZipBytes, ... }
├── Services/
│   ├── DeckPrimerPacketService.cs    # new — mirrors DeckAnalysisPacketService.BuildAsync shape
│   └── PromptBuilders/
│       └── Primer/
│           ├── IPrimerPromptVariant.cs              # new — mirrors IAnalysisPromptVariant
│           ├── PrimerPromptVariantRegistry.cs       # new — mirrors AnalysisPromptVariantRegistry
│           ├── ChatGptPrimerPromptVariant.cs        # new — fully decoupled
│           ├── ClaudePrimerPromptVariant.cs         # new — fully decoupled
│           └── GeminiPrimerPromptVariant.cs         # new — DefensivePromptCharCap
├── Views/
│   └── Primer/
│       ├── Index.cshtml              # new — bracket selector + section catalog + import
│       └── Result.cshtml             # new — prompt display + zip download
└── wwwroot/ts/
    └── primer-sections.ts            # new — localStorage per-bracket persistence + badges
```

---

## 1. PRM-01 Spike Design

### What the spike must answer

**(a) Spellbook `Instructions` richness verdict**

The existing `CommanderSpellbookService.ExtractInstructions` method reads the
`description` field from each variant JSON element and caps it at 300 chars
(`CommanderSpellbookService.cs:266–274`). The returned `SpellbookCombo.Instructions`
string (`CommanderSpellbookService.cs:19`) is already consumed by
`DeckAnalysisPacketService.BuildComboReferenceText` (`DeckAnalysisPacketService.cs:986–1029`),
which emits it as `"   How: {combo.Instructions}"`.

For PRM-08 priority ranking the spike must determine whether the API response also
carries machine-readable fields such as:
- `prerequisite` / `steps` — structured step count (piece count proxy)
- `mana_cost` or `edhrecSaltScore` — assembly cost proxy
- `speed` / `type` — immediacy / combo category

**Mechanism:** Write a throwaway `xunit` test in `DeckFlow.Web.Tests` or a
`DeckFlow.CLI` probe command that calls `CommanderSpellbookService.ParseResponse`
against a live JSON fixture captured from `backend.commanderspellbook.com/find-my-combos`
for a known cEDH commander (e.g., Thrasios + Tymna). Print all top-level variant keys
present on each result object using `JsonDocument.ParseEnumerateObject()`. No existing
service change required — the raw JSON fixture can be recorded by hitting the endpoint
with `curl` and checked in as a test resource.

**Verdict options:**

| Verdict | Meaning | Consequence (D-1) |
|---------|---------|-------------------|
| `sufficient` | API returns `prerequisite` steps count + at least one cost/speed field | Use priority-rank branch (piece count / assembly cost / immediacy) |
| `needs-enrichment` | `description` text is rich enough for heuristic parse (step count by sentence split, "mana" mentions) | Use heuristic-rank branch |
| `fallback` | API returns only card names + results text; instructions sparse | Use AI-ranked fallback branch |

**(b) Representative cEDH primer byte-size measurement**

Build a synthetic full-primer prompt (all 31 sections selected, bracket 5, a known
cEDH commander with ~20 combos) and call `Encoding.UTF8.GetByteCount(promptText)`.
Compare against:
- `AiPlatform.Gemini.PasteWarningBytes` = 32 768 (set at `AiPlatform.cs:28`)
  [VERIFIED: codebase read]
- ChatGPT web paste: no enforced limit, practical ~100 000 chars
- Claude claude.ai: no hard paste limit for Pro/Team tier

The spike records: (1) full-31-section byte count, (2) estimated typical-use byte
count (12–18 sections), (3) recommended `DefensivePromptCharCap` for the Gemini
variant.

**Where the verdict is recorded:** Create `.planning/phases/31-deck-primer-generator/31-SPIKE.md`
containing both verdicts in a structured format the builder plan reads at execution
time to select the ranking branch. This satisfies D-1: no replan cycle required.

**Spike mechanism:** CLI probe command is preferred over a standalone test because it
can hit the live API. Add a `primer-spike` verb to `DeckFlow.CLI/Program.cs` (or use
`dotnet run --project DeckFlow.CLI -- primer-spike <commanderName>`) that:
1. Calls `CommanderSpellbookService` with the live HTTP path
2. Dumps all variant JSON keys + sample Instructions text
3. Builds a full 31-section synthetic prompt and prints byte count

The probe is disposable and does not ship to production.

---

## 2. 31-Section Catalog Model

### Data model (mirroring `AnalysisQuestionCatalog.cs`)

The analysis catalog uses two records + one static class:
- `AnalysisQuestionOption(string Id, string Text)` — the leaf item
- `AnalysisQuestionBucket(string Id, string Label, IReadOnlyList<AnalysisQuestionOption> Questions)`
- `AnalysisQuestionCatalog` — static class with `Buckets`, `AllQuestions`, helper methods

The primer needs a richer leaf because sections carry applicability constraints and
preset membership. Recommended model:

```csharp
// DeckFlow.Web/Models/PrimerSectionCatalog.cs
public sealed record PrimerSection(
    string Id,                        // stable kebab-case identifier
    string Label,                     // user-facing section name
    string HelpText,                  // PRM-12: what good AI output looks like
    PrimerSectionGroup Group,         // which of the 5 groups owns this section
    bool CedhOnly = false,            // sections #24/#25 — hidden for brackets 1–4
    bool CasualOnly = false);         // section #26 — hidden for bracket 5

public enum PrimerSectionGroup
{
    Identity,    // Commander identity, color pie, archetype summary, card rationale
    Combos,      // Verified combos, near-combos, speculative synergies
    Gameplay,    // Game plan, engine, mulligan strategy, role-count grounding
    Matchups,    // Matchups vs archetypes, threat assessment, sideboard thinking
    Maintenance  // Budget cuts, upgrade paths, version history, meta shifts
}

public static class PrimerSectionCatalog
{
    public static IReadOnlyList<PrimerSection> Sections { get; } = [ /* 31 entries */ ];

    public static IReadOnlyList<PrimerSectionGroup> Groups { get; } =
        Enum.GetValues<PrimerSectionGroup>().Cast<PrimerSectionGroup>().ToList();

    public static IReadOnlyList<PrimerSection> ForBracket(string bracketValue) { ... }
    public static IReadOnlyList<string> PresetIdsForBracket(string bracketValue) { ... }
    public static IReadOnlyList<string> NormalizeSelections(IEnumerable<string>? ids) { ... }
}
```

**Why `PrimerSectionGroup` is an enum rather than a string:** The group is a closed,
compile-time-known set; an enum makes the `Group` property serialization-stable and
switch-exhaustive without string comparison. The Razor view iterates
`PrimerSectionCatalog.Groups` to render collapsible `<details>` elements.

**`{ get; init; }` invariant:** `PrimerSection` uses `init` not `get`-only on all
properties. A round-trip test must confirm `JsonSerializer.Deserialize<PrimerSection>(JsonSerializer.Serialize(section))` preserves all fields. [ASSUMED] — the
specific section IDs and labels are TBD; the model shape above is the pattern to follow.

### Preset mapping per bracket (D-3, PRM-03)

| Bracket | Preset name | Preset logic |
|---------|-------------|--------------|
| 1 (Exhibition) | Casual preset | All Identity + basic Gameplay (game plan, mulligan) + Maintenance (budget) |
| 2 (Core) | Casual preset | Same as bracket 1 |
| 3 (Upgraded) | Casual preset | Identity + Gameplay (game plan, engine, mulligan) + basic Matchups |
| 4 (Optimized) | Upgraded preset | Identity + Combos (verified + near) + Gameplay (full) + Matchups (all) |
| 5 (cEDH) | cEDH preset | All sections except casual-only #26; includes cEDH-only #24/#25 |

`PresetIdsForBracket` returns the ordered list of section IDs for the preset. The
localStorage restore (D-3) takes precedence: preset is applied only on a bracket's
first visit. The Razor view emits `data-preset-ids="[...json...]"` on the bracket
selector so the TS module can seed checkboxes without a server round-trip.

**Bracket-scoped gating:** Regardless of saved toggles, sections with `CedhOnly=true`
are hidden (and their IDs stripped from the submission) when bracket != cEDH. Sections
with `CasualOnly=true` are hidden when bracket == cEDH. This enforcement runs in both
the TS module (visual) and the service's `NormalizeSelections` call (server-side guard).

---

## 3. Combo Grounding (D-2, PRM-05/08)

### SpellbookCombo shape (verified from source)

From `CommanderSpellbookService.cs`:
```csharp
// Line 16–19
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions);   // from JSON "description" field, capped at 300 chars
```

The `Instructions` field maps to the variant's `description` JSON key, extracted at
`CommanderSpellbookService.cs:266–274`. `Results` maps to `produces[].feature.name`
(line 256–263). Card names come from `uses[].card.name` (line 241–244).

**No piece count or assembly cost fields are present in the parsed model.** The spike
(Section 1) must determine whether these exist in the raw JSON. The planner must code
both branches.

### Two-block structure (D-2)

```
## Known Combos (ground truth — do not speculate)
[Source: Commander Spellbook API — verified combos already in this deck]

1. Cards: {CardName1} + {CardName2} + ...
   Result: {Results joined}
   How: {Instructions}           ← from API; omit line if Instructions empty
   [Priority rank if spike = sufficient: Piece count: N | Assembly: X | Immediacy: Y]

--- (separator) ---

## Speculative Synergies (you propose)
Commander Spellbook returned {N} verified combos for this deck.
Identify additional non-obvious synergies or near-combos the deck could exploit.
Do not repeat combos from the Known Combos block above.
```

**Null disclosure (D-2):** When `FindCombosAsync` returns `null`:
```
## Known Combos (ground truth — do not speculate)
No verified combos available — treat all synergies as speculative.
(Commander Spellbook API was unreachable at generation time.)
```

**Priority ranking (PRM-08, D-1):**
- If spike verdict = `sufficient`: sort `IncludedCombos` by (piece count ASC, assembly
  cost DESC, immediacy DESC) before emitting. Ranking fields sourced from raw API
  response if available; heuristic (sentence count in Instructions) otherwise.
- If spike verdict = `fallback`: emit combos in API order and add instruction to AI:
  "Rank these combos by ease of assembly and speed of execution."

**Near-combos:** `AlmostIncludedCombos` (capped at `MaxAlmostIncluded = 15`,
`CommanderSpellbookService.cs:58`) emitted in a third sub-block:
```
## Near-Combos (one card away)
1. Missing: {MissingCard} | Have: {CardsInDeck joined}
   Result: {Results joined}
```

### Null handling invariant

`FindCombosAsync` returns `null` on any exception or empty response
(`CommanderSpellbookService.cs:119–146`). The primer service must `await` the task,
check for null, and branch — never throw. The same pattern exists in
`DeckAnalysisPacketService.cs:682–691`.

---

## 4. Matchup Routing (PRM-06)

### EdhTop16 named archetypes (bracket 5)

`IEdhTop16Client.SearchCommanderEntriesAsync` returns `IReadOnlyList<EdhTop16Entry>`
(`EdhTop16Client.cs:78`). Each entry contains `MainDeck` as
`IReadOnlyList<EdhTop16Card>` where `EdhTop16Card` has `Name` and `Type`
(`EdhTop16Client.cs:190–195`). The cEDH meta-gap workflow already projects these into
named archetype summaries.

For the primer the service must extract the commander name from the top-N EdhTop16
results (e.g. top 10 by `Standing`) and present them as a "known archetypes in the
cEDH meta" reference block. The primer prompt instructs the AI to discuss matchups
against each archetype by name.

**EdhTop16 call for the primer:** Use `SearchCommanderEntriesAsync` with the user's
commander name, `CedhMetaTimePeriod.OneYear`, `CedhMetaSortBy.TopPerforming`,
`minEventSize=50`, `maxStanding=null`, `count=10`. Extract the unique commander names
from returned entries' `PlayerName` ... wait — the structure returns the queried
commander's entries, not opponents. The primer needs opponent archetype data.

**Correct interpretation:** The primer for bracket-5 matchups injects the commander
names that top-performed in recent events (i.e., what opponents in a cEDH pod are
likely to be playing). The MetaGap workflow fetches decks for the *user's* commander;
the primer needs a *meta-wide* top commander list instead.

**Resolution:** Use the EdhTop16 GraphQL endpoint differently: query the top commanders
overall (not filtered by commander name). [ASSUMED — verify the GraphQL schema supports
a top-commanders query without a `name` filter.] If only per-commander queries are
supported, the primer can instruct the AI to describe matchups against "the current
top-10 cEDH archetypes" without injecting specific names — degraded but still useful.
This is an open question for the spike to answer (see Section 9, OQ-2).

### Generic strategy buckets (brackets 1–4)

When bracket is not cEDH, the matchup section asks the AI to analyze the deck against
five generic buckets:
1. Aggro (go-wide tokens, combat damage, fast damage)
2. Control (permission, wraths, value engines)
3. Midrange (creature value, incremental advantage)
4. Combo (infinite loops, game-winning combos before turn 8)
5. Stax/Hate (resource denial, tax effects, lock pieces)

These are static strings in the prompt; no external API call is needed for brackets
1–4.

---

## 5. Per-AI Variants + Zip Round-Trip (PRM-09)

### Registry pattern (verified from source)

`AnalysisPromptVariantRegistry` (`AnalysisPromptVariantRegistry.cs:12–49`):
- Constructor takes `IEnumerable<IAnalysisPromptVariant>` from DI
- Dictionary keyed on `AiPlatform` object (reference equality is fine — static singletons)
- Falls back to `AiPlatform.Default` (ChatGPT) when platform not found

`IAnalysisPromptVariant.Build(...)` signature at `IAnalysisPromptVariant.cs:27–37`.

The primer registry is identical in shape:

```csharp
// IPrimerPromptVariant.cs
internal interface IPrimerPromptVariant
{
    AiPlatform Platform { get; }
    string Build(
        PrimerRequest request,
        string decklistText,
        string comboGroundingBlock,
        string categoryDistributionBlock,
        string? matchupReferenceBlock,
        IReadOnlyList<PrimerSection> selectedSections,
        CommanderBracketOption bracket,
        CancellationToken cancellationToken = default);
}
```

**Decoupling invariant (ADR 0001):** Each variant owns its complete prompt text. The
three files share no constants or base classes. A content change (e.g., adding a new
primer instruction) must be hand-applied to all three files. Reviews must not flag
near-duplicate prose as a finding.

### Gemini variant — `DefensivePromptCharCap`

Pattern from `GeminiAnalysisPromptVariant.cs:17–276`:
- `private const int DefensivePromptCharCap = 50000;`
- Builder checks `(builder.Length + estimatedSectionLength) <= DefensivePromptCharCap`
  before appending each optional block
- Trim is section-level (not character-level mid-text truncation)
- Disclosure emitted when trim occurs: "[N section(s) omitted — Gemini paste limit]"

For the primer: trim lowest-priority sections first (Maintenance > Matchups > optional
Gameplay subsections) until within cap. The exact threshold is set by the PRM-01 spike.
Until the spike records its verdict, use `DefensivePromptCharCap = 50000` as a
conservative placeholder (matches analysis variant).

`AiPlatform.Gemini.PasteWarningBytes = 32_768` (`AiPlatform.cs:28`) [VERIFIED: codebase read]
This is the UI warning threshold; the defensive cap in the variant may differ
(50 000 chars ≠ 32 768 bytes because chars may be multi-byte, and the analysis
variant uses chars not bytes for the cap check).

### PrimerAllowedNames

`PacketArtifactStore.ReadEntries` throws `InvalidOperationException` on any entry name
not in the supplied `HashSet<string>` (`PacketArtifactStore.cs:638–641`). The primer
zip needs a new allowlist. Proposed names:

```csharp
private static readonly HashSet<string> PrimerAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-primer-input-summary.txt",
    "01-primer-request-context.txt",
    "10-deck-list.txt",
    "10b-deck-original.txt",
    "20-primer-combos.txt",
    "30-primer-prompt.txt",
    "31-primer-schema.json",
    "40-primer-response.json"
};
```

**The `PrimerAllowedNames` set must be added to `PacketArtifactStore.cs` as the very
first code change in the implementation.** Any plan that touches `BuildPrimerZip` or
`LoadPrimerFromZip` before `PrimerAllowedNames` exists will fail at runtime.

### Round-trip regression test pattern

From `PacketArtifactStoreTests.cs:15–41` and `ContentKbExcerptTests.cs:13–50`:

```csharp
[Fact]
public void BuildPrimerZip_then_LoadPrimerFromZip_round_trips_bracket_and_sections()
{
    var request = new PrimerRequest
    {
        TargetCommanderBracket = "cEDH",
        SelectedSectionIds = ["identity-summary", "verified-combos", "game-plan"]
    };
    var bytes = PacketArtifactStore.BuildPrimerZip(request, ...);
    var loaded = new PrimerRequest();
    using var ms = new MemoryStream(bytes);
    PacketArtifactStore.LoadPrimerFromZip(ms, loaded);
    Assert.Equal("cEDH", loaded.TargetCommanderBracket);
    Assert.Equal(3, loaded.SelectedSectionIds.Count);
}
```

A separate test must verify each DTO record (`PrimerSection`, `PrimerRequest`,
`PrimerResult`) round-trips via `System.Text.Json` with all `init` properties preserved.

---

## 6. Section Selection Persistence (D-3, PRM-10/11)

### localStorage keying pattern

From `kb-selection.ts:50–51`:
```typescript
const PINNED_KEY = 'deckflow.kb.pinned';
const FOLLOWED_KEY = 'deckflow.kb.followed';
```

The primer TS module uses per-bracket keys:
```typescript
const primerSectionsKey = (bracket: string): string =>
  `deckflow.primer.sections.${bracket.toLowerCase()}`;
// Example: 'deckflow.primer.sections.cedh', 'deckflow.primer.sections.core'
```

### Load / save pattern (from `kb-selection.ts:92–135`)

```typescript
const loadSections = (bracket: string): string[] => {
  try {
    const raw = window.localStorage.getItem(primerSectionsKey(bracket));
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return (parsed as unknown[])
      .filter((item): item is string => typeof item === 'string')
      .map(s => s.trim())
      .filter(s => s.length > 0);
  } catch {
    return [];
  }
};

const saveSections = (bracket: string, ids: string[]): void => {
  try {
    if (ids.length === 0) {
      window.localStorage.removeItem(primerSectionsKey(bracket));
      return;
    }
    window.localStorage.setItem(primerSectionsKey(bracket), JSON.stringify(ids));
  } catch {
    return;
  }
};
```

The try/catch is mandatory — `localStorage` throws in private-browsing incognito on
some mobile browsers.

### Preset-seed-then-restore flow (D-3)

1. User selects bracket B.
2. TS module calls `loadSections(B)`.
3. If result is empty (first visit): read `data-preset-ids` from the bracket
   `<option>` element (emitted by Razor as a JSON array of section IDs), apply as
   the default checked set.
4. If result is non-empty: restore the saved set (ignoring preset).
5. After any checkbox toggle: call `saveSections(bracket, currentCheckedIds)`.
6. Gating: always filter against the bracket's allowed section IDs before save and
   before form submit (strip cEDH-only sections if bracket != cEDH, etc.).

### Selected-count badges (PRM-11)

Each collapsible `<details>` group header shows a badge: `"N/M sections selected"`.
The TS module updates badge text after every checkbox change:

```typescript
const updateGroupBadge = (groupId: string): void => {
  const group = document.querySelector(`[data-group="${groupId}"]`);
  if (!group) return;
  const total = group.querySelectorAll<HTMLInputElement>('input[type="checkbox"]').length;
  const checked = group.querySelectorAll<HTMLInputElement>('input[type="checkbox"]:checked').length;
  const badge = group.querySelector<HTMLElement>('.primer-group__badge');
  if (badge) badge.textContent = `${checked}/${total} sections selected`;
};
```

**No-JS fallback:** The Razor view renders hidden inputs for the preset sections by
default. When JS is unavailable the form submits the server-side defaults. The
`NormalizeSelections` method on `PrimerSectionCatalog` strips unknown IDs and enforces
bracket gating on the server regardless.

---

## 7. Gemini Defensive Cap (D-4)

### Pattern from source (verified)

`GeminiAnalysisPromptVariant.cs:17`: `private const int DefensivePromptCharCap = 50000;`

Check at `GeminiAnalysisPromptVariant.cs:260`:
```csharp
if ((builder.Length + estimatedExpertContextLength) <= DefensivePromptCharCap)
{
    // append section
}
```

`EstimateExpertContextLength` is a static helper that sums field lengths with a fixed
overhead per clip (`GeminiAnalysisPromptVariant.cs:279–293`).

### Primer adaptation

The Gemini primer variant must:
1. Build sections in priority order: Identity → Combos → Gameplay → Matchups →
   Maintenance.
2. After each section append, check `builder.Length + estimatedRemainingLength`.
3. If adding the next section would exceed cap, skip it and record its label.
4. After all sections processed, if any were skipped, append:
   ```
   [Sections omitted due to Gemini paste limit: {list}. Re-run with fewer sections selected.]
   ```

**Cap threshold:** Placeholder `DefensivePromptCharCap = 50000` until the PRM-01 spike
records the actual measurement. The spike must recommend a threshold that keeps the
typical-use primer (12–18 sections) comfortably under cap.

---

## 8. Service Pipeline Pattern (DeckPrimerPacketService)

### BuildAsync skeleton (mirroring DeckAnalysisPacketService.cs)

Key structural decisions derived from `DeckAnalysisPacketService.cs:391–784`:

**Cache key:** Build a `PrimerCacheInputs` record analogous to `DeckAnalysisCacheInputs`
(`DeckAnalysisPacketService.cs:281–307`). Fields: `Commander`, `NormalizedDeckSource`,
`TargetBracket`, `SelectedSectionIds` (ordered), `TargetAiPlatformKey`.

**Replay-first guard:** If the request contains a previously-built `PrimerPromptJson`
(step 3+), skip rebuild and return saved data — same pattern as
`DeckAnalysisPacketService.cs:397–451`.

**Category distribution for PRM-07:** The primer grounds Identity, engine, and mulligan
sections with ramp/draw/interaction/tutor counts. The pattern is NOT in
`DeckAnalysisPacketService` (it does not use `ICategoryKnowledgeStore` for the analysis
prompt). The existing precedent for category-aware prompt grounding is
`ContentKbArchetypeDeriver.cs:98` which calls
`GetCategoryRowsForCommanderAsync(commanderName, ct)` and counts rows by category label
substring match. The primer service does the same: query rows for the commander, count
rows where `Category` contains "ramp", "draw", "tutor", "interaction" (case-insensitive)
to produce distribution numbers injected into the prompt block.

```csharp
// PRM-07 pattern:
var rows = await _knowledgeStore.GetCategoryRowsForCommanderAsync(commanderName, ct);
var rampCount = rows.Count(r => r.Category.Contains("ramp", StringComparison.OrdinalIgnoreCase));
var drawCount = rows.Count(r => r.Category.Contains("draw", StringComparison.OrdinalIgnoreCase));
var tutorCount = rows.Count(r => r.Category.Contains("tutor", StringComparison.OrdinalIgnoreCase));
var interactionCount = rows.Count(r => r.Category.Contains("interaction", StringComparison.OrdinalIgnoreCase)
    || r.Category.Contains("removal", StringComparison.OrdinalIgnoreCase));
```

These counts are emitted in the prompt as a grounding block:
```
## Deck Engine Profile (from your harvested knowledge base)
ramp_cards: {rampCount}
draw_engines: {drawCount}
tutors: {tutorCount}
interaction_pieces: {interactionCount}
(Source: DeckFlow category knowledge from harvested Archidekt decks for this commander.
 Missing data = commander not yet harvested — AI should rely on decklist inspection.)
```

**Graceful degradation:** If the knowledge store returns 0 rows (commander not
harvested), omit the block entirely rather than emitting zeroes.

**DI constructor pattern:** The `DeckPrimerPacketService` public constructor takes
`ICategoryKnowledgeStore`, `ICommanderSpellbookService`, `IEdhTop16Client`,
`PrimerPromptVariantRegistry`, `PacketSessionCache`, and `ILogger<...>?`. The
`internal` test constructor replaces the live HTTP delegates with `Func<>` overrides,
matching the analysis service seam at `DeckAnalysisPacketService.cs:88–191`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Deck import (URL/paste) | Custom importer | `LoadDeckEntriesAsync` (already in `DeckAnalysisPacketService`) | Handles Moxfield / Archidekt / paste with fallback; extract as internal static or inject via shared service |
| Combo lookup | Custom API client | `ICommanderSpellbookService.FindCombosAsync` | Handles caching, resilience, null-on-failure contract |
| EdhTop16 query | Custom GraphQL | `IEdhTop16Client.SearchCommanderEntriesAsync` | Handles GraphQL serialization, error mapping |
| ZIP build/load | Custom archive | `PacketArtifactStore.BuildPrimerZip` + `LoadPrimerFromZip` | Security: directory-traversal guard, size limits, allowlist enforcement |
| Platform dispatch | `switch(platform)` | `PrimerPromptVariantRegistry` | Registry pattern; adding a 4th platform requires no edits to switch expressions |
| localStorage try/catch | Roll your own | Mirror `kb-selection.ts:92–135` exactly | Already tested in production; covers mobile private-browsing throw |
| Category distribution query | Custom SQL | `ICategoryKnowledgeStore.GetCategoryRowsForCommanderAsync` | Handles both SQLite and Postgres via dialect abstraction |

**Key insight:** Every data source (Spellbook, EdhTop16, category knowledge, deck
import) already has an injected service with a null-safe or exception-safe contract.
The primer service orchestrates existing services; it does not own any I/O.

---

## Common Pitfalls

### Pitfall 1: `{ get; }` instead of `{ get; init; }` on DTOs

**What goes wrong:** System.Text.Json silently skips deserialization into get-only
properties in .NET 9+. Round-trip tests pass at write time, fail at read time with
empty/default values. This burned the codebase before on `EdhTop16Client` deserialization
(CLAUDE.md, Formatting section).

**Why it happens:** Roslyn analyzer or IDE code cleanup converts `{ get; init; }` to
`{ get; }` when it detects "only set once". ReSharper-style cleanup is explicitly
prohibited by CLAUDE.md.

**How to avoid:** Every new `sealed record` or `sealed class` DTO uses `{ get; init; }`.
Include a `JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(instance))` round-trip
test for every DTO that enters the zip. Run the test before committing the DTO.

**Warning signs:** A round-trip test that asserts non-null but gets null/default, or
a test that never fails even when the property is broken.

### Pitfall 2: Adding zip entries before `PrimerAllowedNames` exists

**What goes wrong:** `PacketArtifactStore.ReadEntries` throws
`InvalidOperationException("Imported zip contains an unsupported entry: ...")` at
runtime when a zip is uploaded that contains a filename not in the active allowlist
(`PacketArtifactStore.cs:638–641`).

**Why it happens:** `BuildPrimerZip` is written first (it produces the zip), but
`LoadPrimerFromZip` calls `ReadEntries` with `PrimerAllowedNames`. If the set is
defined after `BuildPrimerZip` is called in tests, the round-trip test fails with a
misleading error.

**How to avoid:** `PrimerAllowedNames` is the first change in the first plan wave.
Every subsequent plan that references zip filenames checks that the name appears in
`PrimerAllowedNames`.

**Warning signs:** `InvalidOperationException` containing "unsupported entry" in
round-trip tests.

### Pitfall 3: Merging the ground-truth and speculative combo blocks

**What goes wrong:** The prompt instructs the AI to fill speculative synergies; if the
ground-truth block and speculative ask share the same markdown section, the AI cannot
distinguish what it invented from what the API confirmed.

**Why it happens:** D-2 requires structural separation. A single combined section
violates the invariant even if the text says "don't speculate here".

**How to avoid:** Emit two separate `##` headings with an explicit separator. The
ground-truth block carries a machine-parseable label `(ground truth — do not speculate)`
in its header. The speculative block carries `(you propose)`.

**Warning signs:** A single "## Combos" section with both Spellbook data and a
speculative ask in the same block.

### Pitfall 4: Prompt-variant prose sharing

**What goes wrong:** Extracting shared constants or a base class across
ChatGpt/Claude/Gemini primer variants violates ADR 0001. A future content change
to the shared text silently alters all three platforms' outputs in unexpected ways.

**Why it happens:** The three variants have structurally similar (not identical) prose;
a reviewer flags it as duplication. The consolidation attempt happened once before
(commit `a1fa5ad`, reverted same day in `b2ffba7`).

**How to avoid:** Follow ADR 0001. Each variant file is self-contained. Content
changes are hand-applied to all three files. Code reviews must not report cross-variant
duplication as a finding.

**Warning signs:** Any `static` helper, `const`, or base class referenced by more
than one of the three variant files for prose content.

### Pitfall 5: Gemini char-cap check uses bytes not chars (or vice versa)

**What goes wrong:** `AiPlatform.Gemini.PasteWarningBytes = 32_768` uses UTF-8 bytes.
`GeminiAnalysisPromptVariant.DefensivePromptCharCap = 50000` uses C# `string.Length`
(UTF-16 char count). Mixing the two produces incorrect threshold comparisons for
prompts with multi-byte characters.

**Why it happens:** The codebase uses char-count in the variant (fast, no allocation)
and byte-count in `AiPlatform` (UI warning). These are different checks for different
purposes.

**How to avoid:** The primer Gemini variant uses char-count (`builder.Length`) for the
defensive cap, consistent with the analysis variant. The UI paste-warning uses
`PasteWarningBytes` for the byte-count indicator — these are two separate checks and
must stay separate.

### Pitfall 6: EdhTop16 returns the user's commander's entries, not opponent commanders

**What goes wrong:** `IEdhTop16Client.SearchCommanderEntriesAsync` takes a
`commanderName` parameter and returns tournament entries for *that commander*. For
bracket-5 matchup data the primer needs the opponent archetypes, not the user's own
tournament history.

**Why it happens:** The cEDH meta-gap workflow uses EdhTop16 for the user's commander.
The primer has a different data need.

**How to avoid:** See Section 4 and OQ-2 in Open Questions. Until the spike resolves
whether a top-commanders query exists, use a degraded prompt that instructs the AI to
reason about the current cEDH meta without injecting specific opponent data.

---

## Code Examples

### Variant registry dispatch (verified pattern)

```csharp
// Source: AnalysisPromptVariantRegistry.cs:43–47
var variant = _variants.TryGetValue(platform, out var found)
    ? found
    : _variants[AiPlatform.Default];
return variant.Build(request, decklistText, referenceText, ...);
```

### Null-safe combo consumption (verified pattern)

```csharp
// Source: DeckAnalysisPacketService.cs:682–691
var comboResult = await comboTask.ConfigureAwait(false);
// comboResult is null when API was unreachable — BuildComboReferenceText handles null
var comboReferenceText = DeckAnalysisPacketService.BuildComboReferenceText(comboResult);
```

Primer equivalent:
```csharp
var comboResult = await _commanderSpellbookService.FindCombosAsync(entries, ct)
    .ConfigureAwait(false);
var groundTruthBlock = comboResult is null
    ? NullComboDisclosure   // D-2: "No verified combos available..."
    : BuildComboGroundingBlock(comboResult, spikeVerdict);
```

### Instructions field consumption (verified field name)

```csharp
// Source: CommanderSpellbookService.cs:266–274
private static string ExtractInstructions(JsonElement variant)
{
    if (variant.TryGetProperty("description", out var desc))
    {
        var text = desc.GetString() ?? string.Empty;
        return text.Length > 300 ? text[..300].TrimEnd() + "…" : text;
    }
    return string.Empty;
}
```

The spike must check whether the raw JSON also has `prerequisite`, `steps`, or
`manaNeeded` fields at the variant object level.

### Bracket value constants (verified from source)

```csharp
// Source: CommanderBracketCatalog.cs:19–44
// Bracket Values (use these exact strings in PrimerRequest.TargetCommanderBracket):
// "Exhibition" | "Core" | "Upgraded" | "Optimized" | "cEDH"
var bracketOption = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
// Returns null for unknown values — guard before using
```

### Gemini defensive cap check (verified pattern)

```csharp
// Source: GeminiAnalysisPromptVariant.cs:259–274
if ((builder.Length + estimatedExpertContextLength) <= DefensivePromptCharCap)
{
    builder.AppendLine();
    builder.AppendLine("## Expert Context");
    // ... append section
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single BuildAnalysisPromptXxx switch arm | Per-platform variant classes + registry | Phase 15-02 | New workflows must add 3 variant files, not edit a switch |
| Global `chatgpt-*` 301 redirects | Removed | Phase 999.8 | No legacy redirect cleanup needed for primer |
| Prompt shared prose constants | Reverted; variants are fully independent | Phase 10 / ADR 0001 | Never extract shared primer prompt prose |
| `PacketAllowedNames` as single set | Three separate sets (Packet/Comparison/Cedh) | Phase 10-05 | Primer needs a fourth set: `PrimerAllowedNames` |

**Deprecated/outdated:**
- `{get;}` on serialized DTOs: replaced by `{get; init;}` everywhere after the
  EdhTop16 deserialization bug was discovered.
- Global `ScryfallThrottle`: used only for Scryfall calls; primer does not call
  Scryfall directly (it reuses the deck-load path that already throttles).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EdhTop16 GraphQL supports a top-commanders query without a name filter (for bracket-5 matchup data) | Section 4 | Primer bracket-5 matchups degrade to generic "current cEDH meta" text with no named archetypes |
| A2 | Spellbook API variant JSON contains only the fields already parsed (`description`, `uses`, `produces`); no `prerequisite`/`steps`/`manaNeeded` fields | Section 1 | Spike may find additional fields enabling priority ranking; this is the spike's purpose |
| A3 | The 31 specific section IDs and labels (content of `PrimerSectionCatalog.Sections`) are TBD at planning time; the data model shape is correct | Section 2 | If the section count or grouping changes, the model shape is unaffected |
| A4 | `GetCategoryRowsForCommanderAsync` returns rows for a commander that has been harvested; returns empty for unharvested commanders | Section 8 | Empty rows are handled by omitting the distribution block — no null-ref risk |
| A5 | The `PrimerRequest` DTO bracket field uses the same bracket value strings as `CommanderBracketCatalog` ("Exhibition", "Core", "Upgraded", "Optimized", "cEDH") | Sections 2/6 | Low risk — the string values are read from `CommanderBracketCatalog.Options[].Value` at rendering time |

---

## Open Questions

1. **OQ-1 (RESOLVED — D-1):** Does the Spellbook `Instructions` field carry enough
   data for priority ranking?
   - What we know: `Instructions` = `description` JSON field, capped at 300 chars.
     No `steps` or cost fields in the current `SpellbookCombo` record.
   - What's unclear: Whether the raw API response carries additional fields.
   - **Resolution:** Spike (PRM-01, first execution unit) answers this. Planner codes
     both branches; spike verdict in `31-SPIKE.md` selects the branch at execution time.

2. **OQ-2 (OPEN — spike item):** Does the EdhTop16 GraphQL API support a
   top-commanders query (no name filter) for bracket-5 matchup data?
   - What we know: `EdhTop16Client` sends `query($name:String!)` — the `name` parameter
     is required in the existing query (`EdhTop16Client.cs:38`).
   - What's unclear: Whether a separate `topCommanders(first:N)` or `commanders(first:N,
     sortBy:...)` root query exists in the schema.
   - Recommendation: Spike probe (curl the GraphQL introspection endpoint) answers this.
     Fallback: omit EdhTop16 data from the primer matchup block and rely on the AI's
     training knowledge of the cEDH meta.

3. **OQ-3 (RESOLVED — D-3):** How do per-bracket localStorage keys avoid collision
   across the three existing workflows?
   - **Resolution:** Use namespace prefix `deckflow.primer.*` (e.g.,
     `deckflow.primer.sections.cedh`). Existing KB selection uses `deckflow.kb.*`
     (`kb-selection.ts:50–51`). No collision.

4. **OQ-4 (RESOLVED — D-4):** What char threshold should `DefensivePromptCharCap`
   use for the Gemini primer variant?
   - **Resolution:** Spike measures the full-31-section prompt byte/char count and
     recommends a threshold. Until the spike records its verdict, placeholder = 50000
     (matching the analysis variant).

5. **OQ-5 (OPEN — design choice for planner):** Should `DeckPrimerPacketService`
   reuse `LoadDeckEntriesAsync` by moving it to a shared `DeckEntryLoader` service,
   or duplicate the private method?
   - What we know: `LoadDeckEntriesAsync` is a private method on
     `DeckAnalysisPacketService` (`DeckAnalysisPacketService.cs:799`).
   - What's unclear: Whether a shared service is worth the refactor surface.
   - Recommendation: For minimum blast radius, duplicate the method into the primer
     service (it has no side effects beyond `_lastImportNotice`). Extraction to a
     shared service is a separate refactor tracked in backlog.

---

## Environment Availability

Step 2.6: SKIPPED (no new external dependencies — all services already registered in
`Program.cs`; no new runtimes, databases, or CLI tools required).

---

## Security Domain

`security_enforcement` is not explicitly set to false in config; treat as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Primer is a public workflow (same as analysis) |
| V3 Session Management | No | Stateless per-request; zip is client-held |
| V4 Access Control | No | No admin-gated path in primer |
| V5 Input Validation | Yes | `NormalizeSelections` strips unknown IDs; `CommanderBracketCatalog.Find` rejects unknown brackets; `ArgumentNullException.ThrowIfNull` on all constructor deps |
| V6 Cryptography | No | No secrets handled; zip is not encrypted |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Zip path traversal on upload | Tampering | `ReadEntries` checks `entry.FullName.Contains('/')` + `PrimerAllowedNames` allowlist — established pattern at `PacketArtifactStore.cs:633–641` |
| Zip bomb (oversized upload) | DoS | `MaxEntryUncompressedBytes = 2MB` + `MaxTotalUncompressedBytes = 10MB` enforced in `ReadEntries` (`PacketArtifactStore.cs:643–650`) |
| Prompt injection via decklist input | Tampering | Primer prompt is server-assembled; user input (deck text, deck name) is only passed as literal card/deck data, not as instructions |
| localStorage XSS via stored section IDs | Tampering | Server-side `NormalizeSelections` strips non-allowlisted IDs before any use; TS module validates IDs against a data-attribute-provided allowlist |
| CSRF on POST /primer/build | Tampering | `SameOriginRequestValidator` already covers all POST endpoints — ensure primer controller uses the same middleware chain |

---

## Sources

### Primary (HIGH confidence)

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (lines 63–784) — service pipeline, cache key, BuildAsync shape, `BuildComboReferenceText`
- `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` (lines 17, 259–274) — `DefensivePromptCharCap` pattern
- `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` (lines 12–49) — registry dispatch pattern
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` (lines 9–38) — variant interface
- `DeckFlow.Web/Services/PacketArtifactStore.cs` (lines 37–83, 625–659) — allowlist sets, `ReadEntries` security
- `DeckFlow.Web/Models/AnalysisQuestionCatalog.cs` (lines 1–287) — catalog model to mirror
- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` (lines 8–54) — bracket values
- `DeckFlow.Web/Models/AiPlatform.cs` (lines 28, 30) — `PasteWarningBytes = 32_768`
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` (lines 16–35, 161–276) — `SpellbookCombo` shape, `Instructions` field source, null contract
- `DeckFlow.Web/Services/EdhTop16Client.cs` (lines 38–58, 78–144) — GraphQL query shape, commander-name requirement
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (lines 209–236, 291–323) — `GetCategoryRowsForCommanderAsync` pattern
- `DeckFlow.Web/wwwroot/ts/kb-selection.ts` (lines 50–54, 92–135) — localStorage try/catch + progressive-enhancement pattern
- `docs/decisions/0001-prompt-variants-decoupled.md` — variant decoupling invariant
- `.planning/phases/31-deck-primer-generator/31-CONTEXT.md` — D-1 through D-4, carried-forward invariants
- `.planning/REQUIREMENTS.md` — PRM-01 through PRM-12
- `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` (lines 15–77) — round-trip test pattern

### Secondary (MEDIUM confidence)

- `DeckFlow.Web/Services/ContentKbArchetypeDeriver.cs:98` — precedent for
  `GetCategoryRowsForCommanderAsync` usage to derive category counts

### Tertiary (LOW confidence — needing spike validation)

- A2: Spellbook API fields beyond `description`/`uses`/`produces` (spike will verify)
- A1: EdhTop16 top-commanders query availability (spike will verify)

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — all libraries verified in codebase; no new packages
- Architecture: HIGH — direct mirror of verified patterns in production code
- Pitfalls: HIGH — items 1–4 are confirmed historical incidents; items 5–6 are
  direct inferences from API shape
- Spike design: MEDIUM — the spike mechanism is clear; the verdict outcomes are
  conditional on live API data
- EdhTop16 matchup routing for bracket 5: LOW on the top-commanders query; HIGH on
  the fallback behavior

**Research date:** 2026-06-08
**Valid until:** 2026-07-08 (stable stack; Spellbook and EdhTop16 API shapes could
change but are unlikely to in 30 days)
