# Architecture Patterns — v1.5 Integration

**Domain:** DeckFlow v1.5 — Deck Primer Generator + Content KB Integration + Housekeeping
**Researched:** 2026-06-03
**Source files verified:** DeckController.cs, DeckAnalysisPacketService.cs, PacketArtifactStore.cs,
  AnalysisPromptVariantRegistry.cs, IAnalysisPromptVariant.cs, DeckPageTab.cs,
  IContentSiteIndexStore.cs, ContentSiteIndexStore.cs, ContentArtifactSpec.cs,
  ContentKbArtifactPathResolver.cs, ContentKbController.cs, ILlmDistillationService.cs,
  LlmDistillationProviderFactory.cs, CliLlmDistillationService.cs, CliCommandSpec.cs,
  PacketArtifactStore.cs (zip manifest), .editorconfig (doc gate)

---

## Recommended Architecture

All three v1.5 feature tracks plug into existing seams without structural surgery.

### Track A — Deck Primer Generator

New workflow sitting beside DeckAnalysis / DeckComparison / CedhMetaGap. Uses the same
packet/zip pattern but does NOT require the Scryfall enrichment pipeline — its grounding
data comes from Spellbook combos, category knowledge, and EdhTop16. This makes it cheaper
and faster than DeckAnalysis.

### Track B — Content KB to Deck-Analysis Integration

Read-path only on the web tier. Retrieval = load all visible index rows from
`IContentSiteIndexStore`, filter in-memory by tag overlap against the deck's archetype
context, read matching `.md` artifact files from disk via `ContentKbArtifactPathResolver`,
inject excerpts into the analysis prompt variant and render a "What experts say" panel in
the view.

### Track C — Housekeeping

Two orthogonal changes: (1) `.editorconfig` scope expansion for Core doc gate, plus
backfill of 186 undocumented Core sites; (2) KB-12 codex backend — implement the
`NotSupportedException` stub in `LlmDistillationProviderFactory.Resolve`.

---

## Component Boundaries

### Track A — Deck Primer Generator: New Components

| Component | Type | Location | Responsibility |
|-----------|------|----------|---------------|
| `DeckPrimerRequest` | Model (record) | `DeckFlow.Web/Models/` | Form inputs: deck source, bracket, per-section booleans |
| `DeckPrimerViewModel` | Model | `DeckFlow.Web/Models/` | View state: request + result fields |
| `DeckPrimerPacketResult` | Record | `DeckFlow.Web/Services/` | Output: input summary, primer prompt text, zip bytes |
| `IDeckPrimerPacketService` | Interface | `DeckFlow.Web/Services/` | Contract for primer build |
| `DeckPrimerPacketService` | Class (sealed) | `DeckFlow.Web/Services/` | Orchestrates deck load, combo fetch, category query, EdhTop16 fetch (bracket 5 only), prompt composition |
| `IPrimerPromptVariant` | Interface | `DeckFlow.Web/Services/PromptBuilders/Primer/` | Per-AI strategy: `Build(request, decklistText, comboText, categoryText, archetypeList, selectedSections)` |
| `ChatGptPrimerPromptVariant` | Class | `DeckFlow.Web/Services/PromptBuilders/Primer/` | ChatGPT variant (ships first) |
| `ClaudePrimerPromptVariant` | Class | same folder | Claude variant |
| `GeminiPrimerPromptVariant` | Class | same folder | Gemini variant (still flag-gated) |
| `PrimerPromptVariantRegistry` | Class | same folder | Dispatches to `IPrimerPromptVariant` by `AiPlatform`; falls back to Default |
| `DeckPrimer.cshtml` | View | `DeckFlow.Web/Views/Deck/` | Collapsible-group section checklist, bracket selector, generate / download / upload |
| `DeckPageTab.DeckPrimer` | Enum value | `DeckFlow.Web/Models/DeckPageTab.cs` | New tab entry (int = 12 to avoid collision with existing 0-11) |
| Primer zip manifest in `PacketArtifactStore` | Static methods | `DeckFlow.Web/Services/PacketArtifactStore.cs` | `BuildPrimerZip(...)` + `LoadPrimerFromZip` + `PrimerAllowedNames` allowlist |

### Track A — Existing Seams Consumed

| Seam | Location | What Primer Uses It For |
|------|----------|------------------------|
| `IMoxfieldDeckImporter` / `IArchidektDeckImporter` | `DeckFlow.Core/Integration/` | Load deck from URL or pasted text — identical to DeckAnalysis |
| `MoxfieldParser` / `ArchidektParser` | `DeckFlow.Core/Parsing/` | Parse pasted text to `DeckEntry[]` |
| `ICommanderSpellbookService` | `DeckFlow.Web/Services/` | Fetch confirmed combo lines for sections #10/#11/#20 |
| `IEdhTop16Client` | `DeckFlow.Web/Services/` | Named archetypes for bracket-5 matchup sections #22/#23/#25 |
| `ICategoryKnowledgeStore` | `DeckFlow.Web/Services/` | Category labels for engine (#8), mulligan (#14), tutor (#17) sections |
| `AiPlatform` (value object) | `DeckFlow.Web/Models/` | Drives variant dispatch in `PrimerPromptVariantRegistry` |
| `PacketSessionCache` | `DeckFlow.Web/Services/` | Cache key for preview-to-download short-circuit (same pattern as DeckAnalysis) |
| `PacketArtifactStore` | `DeckFlow.Web/Services/` | `BuildPrimerZip` + `LoadPrimerFromZip` for round-trip |
| `_AiSelector` partial | `DeckFlow.Web/Views/Shared/` | Renders the AI target picker; drop into `DeckPrimer.cshtml` unchanged |
| `_WorkflowStepTabs` partial | `DeckFlow.Web/Views/Shared/` | Nav strip; add `DeckPageTab.DeckPrimer` entry |
| `PacketArtifactStore.SuggestPacketZipFileName` | `DeckFlow.Web/Services/PacketArtifactStore.cs` | Filename for download |

**What Primer does NOT consume from DeckAnalysis:**
Scryfall collection/search/named endpoints, `ICommanderBanListService`, `IMechanicLookupService`,
`IScryfallSetService`, set-upgrade prompt builder. Primer is pure deck-load + combo +
category + EdhTop16 — no per-card Scryfall hydration required. This is the key
simplification vs DeckAnalysis.

### Track A — Controller Integration

`DeckController` gains four new actions following the established three-action pattern
(GET render, POST build, POST download, POST upload):

```
GET  /deck-primer          -> DeckPrimer()
POST /deck-primer          -> DeckPrimer(DeckPrimerRequest request)
POST /deck-primer/download -> DeckPrimerDownload(DeckPrimerRequest request)
POST /deck-primer/upload   -> DeckPrimerUpload(IFormFile zipFile)
```

`DeckController`'s constructor gains one new injected parameter: `IDeckPrimerPacketService`.
This is the only change to the controller's public surface.

### Track A — Section Selection Data Flow

The 31-section catalog and bracket presets live as constants/static data within
`DeckPrimerPacketService` (or a companion static helper). The `DeckPrimerRequest` carries
the final resolved bool array — UI JavaScript applies presets client-side, the server only
sees the resolved selection. This avoids server-side preset logic on each POST.

**Section groups rendered as `<details>` elements:**
- Identity (#1-7)
- Combos (#10-12, #20)
- Gameplay (#13-19)
- Matchups (#21-26)
- Maintenance (#8, #9, #27-31)

---

### Track B — Content KB Integration: New Components

| Component | Type | Location | Responsibility |
|-----------|------|----------|---------------|
| `IContentKbRelevanceService` | Interface | `DeckFlow.Web/Services/` | Query + relevance filter for deck-analysis injection |
| `ContentKbRelevanceService` | Class (sealed) | `DeckFlow.Web/Services/` | Loads visible rows from `IContentSiteIndexStore`, matches by tag overlap, reads `.md` artifact files via `ContentKbArtifactPathResolver`, returns ranked excerpts |
| `ContentKbExcerpt` | Record | `DeckFlow.Web/Models/` | Slim struct: source, title, url, summary text (200 words or fewer), artifact path |
| `_ContentKbPanel.cshtml` | Partial view | `DeckFlow.Web/Views/Shared/` | Renders the "What experts say" collapsible panel; included in `DeckAnalysis.cshtml` |

### Track B — Retrieval Design

**Tag matching — NOT commander-specific.** Tags are archetype/strategy + format/bracket +
card-category. The `DeckAnalysisRequest` carries bracket context already (from
`TargetCommanderBracket`). The relevance service derives a tag set from the request
(bracket maps to bracket tags; detected archetypes from category store query map to
archetype tags) and computes overlap score against each visible index row.

**Retrieval path:**
1. `GetPublishedRowsAsync()` — pulls all visible rows from Postgres (slim index; in-memory
   fit on Render 512MB; count is bounded by admin curation, expected O(tens to hundreds)).
2. Score each row: count of tag-set intersection (archetype + bracket). Top-N by score,
   minimum-overlap threshold to exclude irrelevant entries.
3. For matched rows, read `.md` artifact files via
   `ContentKbArtifactPathResolver.ResolveArtifactFullPath(row.ArtifactPath)`.
4. Parse front matter + Summary section from markdown (reuse `ContentArtifactParser`
   which already exists at `DeckFlow.Web/Services/ContentArtifactParser.cs`).
5. Return `IReadOnlyList<ContentKbExcerpt>` (capped at 3-5 items).

**Where injection happens:** Inside `IDeckAnalysisPacketService.BuildAsync`, after deck
load, before prompt composition. A `contentKbBlock` string is assembled from matched
excerpts and passed into `AnalysisPromptVariantRegistry.Build(...)`. Each
`IAnalysisPromptVariant.Build` signature gains `string? contentKbBlock = null`. Each
variant appends the block independently (variants are intentionally decoupled — see
`reference_prompt_variants_intentionally_decoupled.md`; never extract shared appending
logic).

**Flag gate:** `content.kb.enabled` feature flag (already exists; prod flip is part of
this phase per PROJECT.md). `ContentKbRelevanceService` short-circuits to empty list when
the flag is off — no DB or disk I/O when disabled.

**UI panel seam:** `DeckAnalysis.cshtml` includes `_ContentKbPanel.cshtml` partial.
Panel renders only when `DeckAnalysisViewModel.ContentKbExcerpts` is non-empty.
`DeckAnalysisViewModel` gains `IReadOnlyList<ContentKbExcerpt> ContentKbExcerpts`.
No panel rendered on GET or when flag is off; panel appears after POST generates the packet
with matched excerpts.

### Track B — Modified Files

| File | Change Type |
|------|-------------|
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | Inject `IContentKbRelevanceService`; call after deck load; pass `contentKbBlock` to prompt builder |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` | Add `string? contentKbBlock = null` to `Build(...)` |
| `ChatGptAnalysisPromptVariant.cs` | Append KB block when non-null (independently per variant decoupling rule) |
| `ClaudeAnalysisPromptVariant.cs` | Same |
| `GeminiAnalysisPromptVariant.cs` | Same |
| `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | Add `ContentKbExcerpts` property |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | Include `_ContentKbPanel` partial |
| `DeckFlow.Web/Program.cs` | Register `IContentKbRelevanceService` as scoped |

---

### Track C — Core Doc Gate Widening

**editorconfig change (one file, `.editorconfig` at repo root):**

Add a new scoped section after the existing `[DeckFlow.Web/**.cs]` block:

```ini
[DeckFlow.Core/**.cs]
# vX.Y DOC-0N: XML doc-comment gate widened to DeckFlow.Core
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
```

No csproj edits needed. Verify `<TreatWarningsAsErrors>` is not set in
`DeckFlow.Core.csproj` — if it is, warnings become errors and the gate must not be
enabled until every site is documented.

**Backfill order for 186 sites** — dependency order prevents chasing interfaces before
implementations:

1. `Models/` — pure records, simplest, no dependencies
2. `Parsing/` — depends only on Models
3. `Diffing/` — depends on Models
4. `Exporting/` — depends on Models
5. `Filtering/` — depends on Models
6. `Normalization/` — depends on Models
7. `Knowledge/` — `ContentArtifactSpec`, `DistillationSchemas`, `DistillationResults`
8. `Storage/` — dialect interfaces and implementations
9. `Content/` — `IContentSiteIndexStore`, `ContentSiteIndexStore`, `ContentVideoStore`
10. `Integration/` — importers, `ILlmDistillationService`, `LlmDistillationProviderFactory`, `CliLlmDistillationService`
11. `Loading/` — depends on Integration
12. `Reporting/` — depends on Models
13. `CLI/` entrypoints last

**Critical rule:** Backfill all 186 sites in one phase (or batch of commits), then add
the gate line in the same or immediately following commit. Never gate before backfill.

### Track C — KB-12 Codex Distill Backend

**Current state (verified in `LlmDistillationProviderFactory.cs` lines 49-53):**
The `codex` branch throws `NotSupportedException("...deferred to Phase 21.3 / KB-12...")`.
The factory already recognizes the string `"codex"` — no routing change needed.

**Envelope shape difference:** `CliLlmDistillationService.BuildCommandSpec` currently
hard-guards `if (!string.Equals(_provider, ClaudeProvider, ...)) throw`. Claude CLI emits
`CliEnvelopeKind.ClaudeJson` (JSON envelope with `result` field). Codex CLI emits raw
stdout (`CliEnvelopeKind.Raw`). These differ materially.

**Recommended implementation:** Add `CodexCliLlmDistillationService` (new sealed class)
rather than widening `CliLlmDistillationService`. `CodexCliLlmDistillationService` builds
a `CliCommandSpec` with `CliEnvelopeKind.Raw` and invokes the same static
`RunProcessAsync` via composition (extract it to a shared internal helper, or duplicate
the compact process-runner inline). `LlmDistillationProviderFactory.Resolve` switches on
`"codex"` to construct it. The factory's error message gains `codex` to the supported
list.

**Expected codex CLI shape:**
```
codex exec --full-auto --color never --skip-git-repo-check -
```
stdin = instruction + transcript. stdout = raw model output. `CliEnvelopeKind.Raw`.
3-attempt retry + per-call timeout from the existing `CliLlmDistillationService` pattern
applies identically.

---

## Data Flow

### Deck Primer — Request Lifecycle

```
Browser POST /deck-primer
  -> DeckController.DeckPrimer(DeckPrimerRequest)
      -> DeckPrimerPacketService.BuildAsync(request)
          -> [deck load]    MoxfieldParser / ArchidektParser
                            OR IMoxfieldDeckImporter / IArchidektDeckImporter
          -> [combos]       ICommanderSpellbookService.FindCombosAsync(commander)
          -> [EdhTop16]     IEdhTop16Client  (bracket == 5 only)
          -> [categories]   ICategoryKnowledgeStore.GetCategoriesAsync(commander)
          -> [prompt]       PrimerPromptVariantRegistry.Build(platform, sections, ...)
                                -> IPrimerPromptVariant.Build(...)
          -> return DeckPrimerPacketResult
      -> View("DeckPrimer", viewModel)
```

### Content KB Injection — Request Lifecycle

```
Browser POST /deck-analysis
  -> DeckController.DeckAnalysis(DeckAnalysisRequest)
      -> DeckAnalysisPacketService.BuildAsync(request)
          -> [existing pipeline: deck load, Scryfall hydration, banlist, combos]
          -> ContentKbRelevanceService.GetRelevantExcerptsAsync(bracketTag, archetypeTags)
              -> IFeatureFlagCache["content.kb.enabled"]  [short-circuit if false]
              -> IContentSiteIndexStore.GetPublishedRowsAsync()   [Postgres slim query]
              -> [score by tag overlap — in-memory]
              -> ContentKbArtifactPathResolver.ResolveArtifactFullPath(row.ArtifactPath)
              -> File.ReadAllTextAsync(path)              [disk read per matched row]
              -> ContentArtifactParser.ParseSummary(markdown)
              -> return IReadOnlyList<ContentKbExcerpt>   [cap 3-5]
          -> AnalysisPromptVariantRegistry.Build(..., contentKbBlock)
      -> DeckAnalysisViewModel { ContentKbExcerpts = excerpts }
      -> View("DeckAnalysis", model)
          -> _ContentKbPanel.cshtml  [renders when excerpts non-empty]
```

### KB-12 Codex Distill — CLI Lifecycle

```
dotnet run --project DeckFlow.CLI -- distill --provider codex ...
  -> CommandRunners.RunDistillAsync(...)
      -> LlmDistillationProviderFactory.Resolve("codex", httpClient)
          -> new CodexCliLlmDistillationService()    [replaces NotSupportedException]
      -> [existing distill loop unchanged]
          -> CodexCliLlmDistillationService.SummarizeAsync(transcript)
              -> CliCommandSpec("codex", ["exec","--full-auto",...], CliEnvelopeKind.Raw)
              -> RunProcessAsync -> stdout
              -> ExtractModelText(Raw, stdout) -> raw text
              -> ExtractBalancedJsonObject + deserialize + validate (same as Claude path)
```

---

## Patterns to Follow

### Pattern 1: Packet Service Structure

Every packet service follows: interface (`IDeck*PacketService`) + sealed implementation +
result record. Constructor uses the test-seam pattern (public DI ctor + internal override
ctor with delegate injection). `BuildAsync` is the single orchestration method.

```csharp
public interface IDeckPrimerPacketService
{
    Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request,
        CancellationToken cancellationToken = default);
    Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request,
        CancellationToken cancellationToken);
}
```

### Pattern 2: Prompt Variant Registry

Registry takes `IEnumerable<IPrimerPromptVariant>` from DI, builds a
`Dictionary<AiPlatform, IPrimerPromptVariant>`, falls back to Default. Each variant is
`internal sealed class` registered as `AddSingleton` in `Program.cs`. Three variants per
workflow (ChatGpt, Claude, Gemini) — content is NEVER shared between variants even when
identical prose appears (intentional decoupling — `reference_prompt_variants_intentionally_decoupled.md`).

### Pattern 3: PacketArtifactStore Zip Round-Trip

New `BuildPrimerZip` + `LoadPrimerFromZip` static methods on `PacketArtifactStore`.
`PrimerAllowedNames` allowlist guards upload security (mirrors the three existing
allowlists). Primer-specific filenames follow the established `NN-description.txt`
convention:

```
00-primer-input-summary.txt
01-request-context.txt
10-deck-list.txt
10b-deck-original.txt
20-combo-lines.txt
30-primer-prompt.txt
```

### Pattern 4: Content KB In-Memory Tag Matching

`IContentSiteIndexStore` has no parameterized tag-filter query — all tag data returns as
JSON-serialized arrays per row (`ContentArtifactSpec.DeserializeTags` already handles
deserialization). Match in-memory after `GetPublishedRowsAsync()`. This is correct for
curated scale (O(hundreds) max). Do NOT add a parameterized SQL tag-filter query — it
keeps the store interface stable and avoids Postgres-specific JSON operators that break
SQLite fallback.

### Pattern 5: Feature-Flag Short-Circuit

Check `IFeatureFlagCache["content.kb.enabled"]` at the top of
`ContentKbRelevanceService.GetRelevantExcerptsAsync` before any DB or disk I/O. Return
`Array.Empty<ContentKbExcerpt>()`. Mirrors the `[FeatureFlagGate]` attribute used on
controller actions (see `ContentKbController.cs`).

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Scryfall Hydration in Primer

DeckAnalysis calls Scryfall to hydrate every card (oracle text, color identity, etc.).
Primer does NOT need this — it uses the deck as an opaque list plus commander identity
from category/spellbook results. Adding Scryfall hydration adds latency and an upstream
failure mode with zero payoff for the primer's 31 sections.

### Anti-Pattern 2: Shared Prompt Text Between AI Variants

The `reference_prompt_variants_intentionally_decoupled.md` memory note is explicit:
ChatGpt/Claude/Gemini prompt prose duplication is intentional. Never extract shared
guidance into a helper called by all three variants. Content changes require editing all
three files independently.

### Anti-Pattern 3: Commander-Name Tag Matching for Content KB

Tags are archetype/strategy + format/bracket + card-category — NOT commander name. The
`content_site_index` schema has no commander field (verified in `ContentArtifactSpec.cs`
and `ContentSiteIndexRow`). Matching on commander name would always miss. Scoring must
operate on archetype and bracket tags only.

### Anti-Pattern 4: Widening editorconfig Gate Before Backfill

Adding `[DeckFlow.Core/**.cs]` CS1591 = warning before the 186 sites are documented
produces 186+ build warnings and makes the gate meaningless. Backfill all sites first,
then add the gate in the same or immediately following commit.

### Anti-Pattern 5: ClaudeJson Envelope for Codex Backend

`CliLlmDistillationService.ExtractModelText` parses a `result` JSON field when
`CliEnvelopeKind.ClaudeJson`. Codex CLI emits raw stdout, not a JSON envelope with a
`result` field. Using `ClaudeJson` on codex output causes a parse failure on every call.
Use `CliEnvelopeKind.Raw`.

### Anti-Pattern 6: SQL Tag Filtering in ContentSiteIndexStore

Do not add Postgres-specific JSON operators (`@>`, `json_array_elements`, etc.) to the
store for tag filtering. The store's interface must remain SQLite-compatible for dev/test
environments. In-memory filtering after the full row load is the correct pattern at this
scale.

---

## Build Order

Tracks A and B can execute in parallel after Track C housekeeping is complete (or
independently, since neither depends on the other). Track C's two subtracks (doc backfill,
KB-12) are fully independent of each other.

### Suggested Phase Sequence

```
Phase N   — KB-12 Codex Backend
            New: CodexCliLlmDistillationService
            Modified: LlmDistillationProviderFactory (remove stub, add codex branch + supported list)
            Dependency: none. Fast win.

Phase N+1 — Core XML-Doc Backfill
            186 sites across DeckFlow.Core in dependency order (Models -> Parsing ->
            Diffing -> Exporting -> Filtering -> Normalization -> Knowledge -> Storage ->
            Content -> Integration -> Loading -> Reporting)
            Dependency: none.

Phase N+2 — Core Doc Gate Widen
            Modified: .editorconfig (add [DeckFlow.Core/**.cs] section)
            Dependency: Phase N+1 complete (zero undocumented sites before gate).

Phase N+3 — Content KB -> Deck-Analysis Integration
            New: IContentKbRelevanceService, ContentKbRelevanceService, ContentKbExcerpt,
                 _ContentKbPanel.cshtml
            Modified: DeckAnalysisPacketService, IAnalysisPromptVariant (+ 3 variants),
                      DeckAnalysisViewModel, DeckAnalysis.cshtml, Program.cs
            Includes: prod flag flip (content.kb.enabled = ON)
            Dependency: ContentKbArtifactPathResolver + IContentSiteIndexStore (both exist).

Phase N+4 — Deck Primer Generator
            Recommended sub-phases to limit blast radius:
              4a: Models/Request/ViewModel/Result + DeckPageTab.DeckPrimer + routing stubs +
                  PacketArtifactStore.BuildPrimerZip + PrimerAllowedNames
              4b: DeckPrimerPacketService.BuildAsync — deck load + Spellbook + category +
                  bracket routing + EdhTop16 for bracket-5
              4c: Primer prompt variants (ChatGpt first; Claude + Gemini stubs) +
                  PrimerPromptVariantRegistry + Program.cs registration
              4d: Download/upload round-trip, session cache key, JS section-preset logic
            Dependency: DeckPageTab (added in 4a), ICommanderSpellbookService (exists),
                        IEdhTop16Client (exists), ICategoryKnowledgeStore (exists)
```

**Why this order:**
- KB-12 is a pure Core change with no web surface — lowest risk, ships first.
- Doc backfill before gate — avoids polluting build signal with spurious warnings.
- Content KB integration before Primer — smaller surface, validates the flag-flip path
  in prod before Primer takes any downstream dependency on KB context (Primer may benefit
  from KB excerpts in a later phase).
- Primer last because it is the largest, most visible, and has no upstream blockers.

---

## Scalability Considerations

| Concern | At Current Scale | If Scale Grows |
|---------|-----------------|---------------|
| KB in-memory tag match | O(hundreds) rows, negligible | If KB grows to O(thousands), add tag-indexed query to `IContentSiteIndexStore` using a Postgres-only override path |
| Primer EdhTop16 fetch | One fetch per bracket-5 primer request | Verify caching applies (MetaGapService has its own cache; primer needs equivalent or shared client-level caching) |
| PacketArtifactStore zip manifest | Static allowlist per workflow | Each new workflow adds its own `*AllowedNames` set — no cross-contamination risk by design |
| Core doc warnings | 186 sites at gate enable | Should be zero after backfill phase; gate prevents regression going forward |
| Primer combo data richness | Spellbook returns combo pieces without step-by-step lines | Seed note flags `spike-combo-data-to-primer-grounding` — may need pre-phase spike to verify Spellbook payload is rich enough for section #11 |

---

## Sources

- `DeckFlow.Web/Controllers/DeckController.cs` — action pattern, constructor injection shape
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — packet service structure, dependencies
- `DeckFlow.Web/Services/PacketArtifactStore.cs` — zip manifest, `BuildZip` / `BuildComparisonZip` / `BuildCedhMetaGapZip` patterns and allowlists
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` — variant interface signature
- `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` — registry/dispatch pattern
- `DeckFlow.Web/Models/DeckPageTab.cs` — existing tab enum values (0-11); new entry = 12
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — index store interface
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `GetPublishedRowsAsync` query shape (no tag filter in SQL)
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ContentSiteIndexRow` tag fields, `DeserializeTags`
- `DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs` — artifact file resolution
- `DeckFlow.Web/Controllers/ContentKbController.cs` — flag gate pattern, in-memory tag grouping
- `DeckFlow.Core/Integration/ILlmDistillationService.cs` — three-method distill interface
- `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` — codex stub at lines 49-53
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs` — process runner, envelope extraction, retry loop
- `DeckFlow.Core/Integration/CliCommandSpec.cs` — `CliEnvelopeKind.Raw` vs `ClaudeJson`
- `.planning/seeds/deck-primer-generator.md` — feature shape and pre-made decisions
- `.planning/notes/deck-primer-prompt-design.md` — 31-section catalog, bracket routing, combo handling, group layout
- `.planning/PROJECT.md` — v1.5 feature targets, constraints
- `.editorconfig` — Phase 23 doc gate scoped to `[DeckFlow.Web/**.cs]` only; Core still suppressed
- Memory: `reference_prompt_variants_intentionally_decoupled.md` — variants never share prose
