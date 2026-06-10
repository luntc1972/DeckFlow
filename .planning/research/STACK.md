# Technology Stack — v1.5 Deck Primer Generator + Content KB Integration

**Project:** DeckFlow v1.5
**Researched:** 2026-06-03
**Confidence:** HIGH (all conclusions drawn from direct codebase inspection; no external library
research required because the answer is "reuse everything")
**Scope:** NEW capabilities only. The full installed stack (ASP.NET 10, RestSharp 114,
Polly 8.x, Npgsql 10, Microsoft.Data.Sqlite 10, Serilog 9, Markdig 0.38, IMemoryCache,
IHttpClientFactory, ResiliencePipelineProvider<string>, OpenAI 2.10, YoutubeExplode 6.6,
System.ServiceModel.Syndication 10.0.2, xUnit 2.9.3, all v1.0–v1.4 services) is
unchanged and untouched.

---

## Verdict: Zero New Dependencies

Both v1.5 features are fully deliverable by composing existing installed services. Every
building block — data source, HTTP client, prompt infrastructure, artifact storage, session
caching, feature-flag gating — is already registered in DI and proven in production.
The work is composition, not acquisition.

No new NuGet packages. No new npm packages. No new external services.

---

## Reuse Map: Deck Primer Generator

The primer generator is a fourth packet workflow, a sibling of DeckAnalysis /
DeckComparison / CedhMetaGap. The pattern is: decklist load → data hydration → prompt
build → zip artifact → download. Every piece of that pipeline exists.

### Data Sources (all DI-registered, all production-proven)

| Primer need | Existing service | Interface / method | File |
|-------------|----------------|--------------------|------|
| Deck load from URL or paste | `IMoxfieldDeckImporter`, `IArchidektDeckImporter`, `MoxfieldParser`, `ArchidektParser` | `ImportAsync(url)` / `Parse(text)` | `DeckFlow.Core/Integration/`, `DeckFlow.Core/Parsing/` |
| Combo grounding — ground-truth lines (sections 10, 11, 20) | `ICommanderSpellbookService` | `FindCombosAsync(entries)` → `CommanderSpellbookResult` with `IncludedCombos[].Instructions + Results + CardNames` and `AlmostIncludedCombos` | `DeckFlow.Web/Services/CommanderSpellbookService.cs` |
| Bracket-5 matchup archetypes (sections 22, 23, 25) | `IEdhTop16Client` | `SearchCommanderEntriesAsync(commanderName, ...)` → `IReadOnlyList<EdhTop16Entry>` | `DeckFlow.Web/Services/EdhTop16Client.cs` |
| Category/engine-role grounding — mulligan buckets, tutor priority, engine breakdown (sections 8, 9, 14, 17, 29) | `ICategoryKnowledgeStore` | `GetCategoryRowsAsync(cardName)` / `GetCategoryRowsForCommanderAsync(commanderName)` | `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` |
| Commander oracle text + card resolution | Scryfall named-card endpoint via `IScryfallRestClientFactory` + `ResiliencePipeline<RestResponse>("scryfall")` | `_executeNamedAsync` pattern | `DeckFlow.Web/Services/ScryfallRestClientFactory.cs` |
| Bracket presets + labels | `CommanderBracketCatalog` (static) | `Options` list + `Find(string?)` | `DeckFlow.Web/Models/CommanderBracketCatalog.cs` |
| Brackets 1–4 matchup archetypes | Hardcoded 5 generic strategy buckets (Aggro / Control / Midrange / Combo / Stax-Hate) | Inline strings in new `PrimerSectionCatalog` | New static class — no external call |

### Prompt Infrastructure (all existing, all proven)

| Primer need | Existing mechanism | Notes |
|-------------|-------------------|-------|
| AI-platform dispatch (ChatGPT / Claude / Gemini) | `AiPlatform` value object + `Normalize(string?)` | `DeckFlow.Web/Models/AiPlatform.cs`; same three-way fan-out used by all workflows |
| Per-AI variant strategy pattern | `I*PromptVariant` interface + `*PromptVariantRegistry` | See `PromptBuilders/Analysis/`, `Comparison/`, etc.; new `IPrimerPromptVariant` + `PrimerPromptVariantRegistry` follow the identical pattern |
| Prompt text assembly | `System.Text.StringBuilder` (BCL) | All 15 existing prompt variants use raw `StringBuilder`; no templating library needed or wanted |
| Session zip artifact storage | `PacketArtifactStore` (static class) | `DeckFlow.Web/Services/PacketArtifactStore.cs`; needs one new `PrimerAllowedNames` set + `BuildPrimerZip(...)` overload — pure C# extension, zero new deps |
| Preview/download session caching (preview → download reuse, no Scryfall replay) | `PacketSessionCache` (dedicated 10 MB `MemoryCache`, 5-min TTL) | `DeckFlow.Web/Services/PacketSessionCache.cs`; already shared across all three existing packet services; primer result type goes through the same generic `Get<T>` / `Set<T>` |
| Packet cache key computation | `PacketSessionCache.ComputeKey(object fieldBag)` (SHA-256) | Service implements `TryComputeCacheKeyAsync` on the same model as `DeckAnalysisPacketService` |

### New Code Required (no new packages)

All new files follow an existing pattern exactly. No architectural invention.

| Artifact | Follows this existing pattern | Path |
|----------|------------------------------|------|
| `IDeckPrimerPacketService` + `DeckPrimerPacketService` | `IDeckAnalysisPacketService` / `DeckAnalysisPacketService` | `DeckFlow.Web/Services/DeckPrimerPacketService.cs` |
| `IPrimerPromptVariant` | `IAnalysisPromptVariant` | `DeckFlow.Web/Services/PromptBuilders/Primer/IPrimerPromptVariant.cs` |
| `ChatGptPrimerPromptVariant`, `ClaudePrimerPromptVariant` | `ChatGptAnalysisPromptVariant`, `ClaudeAnalysisPromptVariant` | `DeckFlow.Web/Services/PromptBuilders/Primer/` |
| `PrimerPromptVariantRegistry` | `AnalysisPromptVariantRegistry` | Same folder |
| `DeckPrimerRequest` | `DeckAnalysisRequest` — adds `SelectedBracket` (string) + `SelectedSectionIds` (string[]) | `DeckFlow.Web/Models/DeckPrimerRequest.cs` |
| `DeckPrimerResult` record | `DeckAnalysisPacketResult` | Co-located with service |
| `PrimerSectionCatalog` static class | `CommanderBracketCatalog` / `AnalysisQuestionCatalog` static classes | `DeckFlow.Web/Models/PrimerSectionCatalog.cs` — holds 31-section definitions, group assignments (5 collapsible groups), preset defaults (cEDH / Casual) |
| `PacketArtifactStore` primer overloads | Existing `BuildZip` / `BuildComparisonZip` overloads | Extend `DeckFlow.Web/Services/PacketArtifactStore.cs` |
| `DeckPrimer.cshtml` view | `DeckAnalysis.cshtml` — adds collapsible 5-group section selector | `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` |
| DeckController primer routes | Existing workflow route triplets (GET index, POST generate, POST download, POST upload) | Extend `DeckFlow.Web/Controllers/DeckController.cs` |

### DI Registration (additions to `Program.cs`)

```csharp
// Deck Primer Generator
services.AddScoped<IDeckPrimerPacketService, DeckPrimerPacketService>();
services.AddSingleton<PrimerPromptVariantRegistry>();
// ChatGptPrimerPromptVariant / ClaudePrimerPromptVariant registered inside registry ctor,
// same pattern as AnalysisPromptVariantRegistry
```

`PacketSessionCache` is already registered as a singleton; the primer result type uses it
without any registration change.

---

## Reuse Map: Content KB → Deck-Analysis Integration

Injecting KB excerpts into existing deck-analysis prompts and rendering a "What experts say"
panel. All four components needed already exist.

### Data Sources

| KB integration need | Existing service / class | Method | File |
|--------------------|------------------------|--------|------|
| Published KB index, filtered by bracket/archetype | `IContentSiteIndexStore` | `GetPublishedRowsAsync()` → `IReadOnlyList<ContentSiteIndexRow>` (has `ArchetypeTags`, `BracketTags`, `CardCategoryTags`, `ArtifactPath`) | `DeckFlow.Core/Content/ContentSiteIndexStore.cs` |
| Artifact markdown body text | `ContentKbArtifactPathResolver` + `File.ReadAllTextAsync` | `ResolveArtifactFullPath(row.ArtifactPath)` — returns absolute filesystem path to `.md` file | `DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs` |
| Front-matter strip, body extraction | `ContentArtifactParser.SplitHeader(raw)` (static) | Returns `(IReadOnlyDictionary<string,string> Header, string Body)` — body is the paste-ready summary + Key Clips markdown | `DeckFlow.Web/Services/ContentArtifactParser.cs` |
| Feature-flag gate | `IFeatureFlagCache` | `IsEnabled("content.kb.enabled")` | `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` |

### Injection Strategy into Existing Prompts

Two options were evaluated for how KB excerpts reach the analysis prompt text:

**Option A — Extend `IAnalysisPromptVariant.Build(...)` signature**
- Add `IReadOnlyList<ContentKbExcerpt>? kbExcerpts` parameter
- Each variant formats the "What experts say" section in its own prose style
- Requires updating 3 existing implementations + the interface

**Option B — Post-build append in `DeckAnalysisPacketService`**
- `ContentKbInjectionService` runs after all three variant `Build()` calls
- Appends a markdown block to the assembled prompt text
- Zero interface churn; KB body is already markdown prose that pastes cleanly across all three AI platforms

**Recommendation: Option B for v1.5.** The KB artifact body is plain markdown already
formatted for paste. A post-build append is lower-risk, requires no variant signature
changes, and is reversible. Promote to Option A in a future phase if per-AI KB formatting
becomes needed.

### New Code Required (no new packages)

| Artifact | What it does |
|----------|-------------|
| `IContentKbInjectionService` + `ContentKbInjectionService` | Queries `IContentSiteIndexStore.GetPublishedRowsAsync()`, filters rows by `BracketTags` matching the request's commander bracket and/or `ArchetypeTags`; reads up to N artifact bodies via `ContentKbArtifactPathResolver`; parses front matter via `ContentArtifactParser.SplitHeader`; returns `IReadOnlyList<ContentKbExcerpt>`. Returns empty list when `IFeatureFlagCache.IsEnabled("content.kb.enabled")` is false. |
| `ContentKbExcerpt` record | `(string Title, string Source, string VideoUrl, string Body)` — minimal shape for prompt injection and UI panel render |
| `DeckAnalysisPacketService` post-build wiring | Inject `IContentKbInjectionService`; call after prompt text assembly; append "What experts say" markdown block when `kbExcerpts.Count > 0` |
| `DeckAnalysis.cshtml` "What experts say" panel | Renders `IReadOnlyList<ContentKbExcerpt>` below the prompt output; visible only when flag on and excerpts exist |
| Cache key discipline | KB excerpts are display-only, not included in the `PacketSessionCache` key. The cache key is computed before KB injection and remains based solely on deck content + request parameters — prevents KB index changes from invalidating user sessions |

### DI Registration (additions to `Program.cs`)

```csharp
// Content KB injection
services.AddScoped<IContentKbInjectionService, ContentKbInjectionService>();
```

`IContentSiteIndexStore` and `ContentKbArtifactPathResolver` are already registered.

---

## Existing Stack (unchanged — documented for integration reference)

These are the services and technologies the new code will call. No version changes.

| Technology / Service | Version | Role in v1.5 |
|---------------------|---------|-------------|
| `ICommanderSpellbookService` | — | Combo ground truth for primer sections 10, 11, 20 |
| `IEdhTop16Client` | — | Named archetypes for cEDH matchup sections 22, 23, 25 |
| `ICategoryKnowledgeStore` | — | Engine/mulligan/tutor category buckets for primer sections 8, 9, 14, 17, 29 |
| `IContentSiteIndexStore` | — | KB index query for "What experts say" injection |
| `ContentKbArtifactPathResolver` | — | Resolves artifact paths to filesystem for KB body reads |
| `ContentArtifactParser` | — | Strips YAML front matter from KB markdown files |
| `IFeatureFlagCache` | — | Gates KB injection on `content.kb.enabled` flag |
| `PacketArtifactStore` | — | Zip artifact storage for primer download |
| `PacketSessionCache` | — | Preview → download reuse for primer |
| `AiPlatform` value object | — | Three-way AI fan-out for primer prompt variants |
| `CommanderBracketCatalog` | — | Bracket option lookup + preset routing |
| `IMoxfieldDeckImporter` / `IArchidektDeckImporter` | — | Deck load from URL |
| `MoxfieldParser` / `ArchidektParser` | — | Deck load from pasted text |
| RestSharp 114.0.0 | 114.0.0 | HTTP client wrapper (no change) |
| Polly 8.x | 8.x | Named resilience pipelines (no change; no new pipelines needed) |
| ASP.NET Core MVC 10.0 | 10.0 | Controller + Razor view framework |
| Npgsql 10.0.0 / Microsoft.Data.Sqlite 10.0.0 | 10.0.0 | DB providers for `IContentSiteIndexStore` |
| IMemoryCache (built-in) | — | Used inside `PacketSessionCache` (already wired) |
| xUnit 2.9.3 | 2.9.3 | Test framework; new service tests follow existing patterns |

---

## What NOT to Add

| Avoid | Why |
|-------|-----|
| Any templating engine (Scriban, Fluid, Handlebars.NET) | `StringBuilder` + C# raw-string literals is already the pattern across 15 prompt variants; a templating engine adds a dependency with zero leverage for this domain |
| `Microsoft.Extensions.Http.Resilience` standard handler | Explicitly prohibited by project constraints — the existing `RestSharp + direct Polly v8` pattern is the only approved HTTP resilience path |
| `Microsoft.SemanticKernel` or any LLM orchestration SDK | No server-side LLM calls in either v1.5 feature; prompt artifact is built and handed to the user for manual paste |
| EDHREC API / HTTP client | Explicitly out of v1.5 scope; bracket 1–4 matchup archetypes use 5 generic strategy buckets inline in the prompt, no external call |
| Any new test framework or mocking library | Project rule: match existing xUnit + no-mocking-lib-without-asking; primer and KB injection services get the standard `Func<...>` delegate seam pattern |
| Any CSS framework (Bootstrap, Tailwind) | The 5-group collapsible section selector for the primer is `<details><summary>` semantic HTML + existing `site-common.css` utility classes; no framework needed |

---

## Open Risk: Combo Data Richness for Primer Narration

The seed note flags `spike-combo-data-to-primer-grounding` as a pre-phase spike. The
concern: `CommanderSpellbookResult.IncludedCombos[].Instructions` may be too terse to
ground step-by-step narration for primer section 11 (Core Combo Lines).

**Current state (verified from codebase):** `SpellbookCombo` carries `CardNames` (list),
`Results` (list of outcome strings), and `Instructions` (single string). The `Instructions`
field is used today in the DeckAnalysis prompt (section 30-reference.txt) and is passed
verbatim to the AI.

**Assessment:** The `Instructions` text is sufficient for an AI to narrate a combo line —
it is the same ground-truth text Spellbook itself shows users. The spike should validate
whether the text is detailed enough for the specific primer framing (step-by-step, labeled
"piece A + piece B → result") vs a summary mention. This is a prompt-design question, not
a stack question. No new data source is needed regardless of the spike outcome; the prompt
framing around the existing `Instructions` field is what gets tuned.

**Confidence:** MEDIUM (stack is fine; prompt quality is the open variable).

---

## Confidence Assessment

| Area | Confidence | Basis |
|------|------------|-------|
| Zero-new-dependencies verdict | HIGH | Direct inspection of all referenced services; every interface and pattern exists in production |
| Primer packet service pattern | HIGH | `DeckAnalysisPacketService` is a complete, proven template; shape is identical |
| KB injection via post-build append | HIGH | `ContentArtifactParser.SplitHeader` + `ContentKbArtifactPathResolver.ResolveArtifactFullPath` already used in `ContentKbController`; pattern is production-proven |
| EdhTop16 archetype label quality for primer matchup section | MEDIUM | `IEdhTop16Client` returns `maindeck{name type}` and tournament metadata but not pre-labelled archetype strings; the primer will likely pass raw entry data to the AI to derive labels — same approach `MetaGapService` uses successfully today |
| Combo data richness for step-by-step narration | MEDIUM | See Open Risk section above; stack is not in doubt, prompt framing is |
| `PacketSessionCache` cache-key discipline for KB excerpts | HIGH | Existing precedent in `DeckAnalysisPacketService.TryComputeCacheKeyAsync` — cache key is computed before any display-only data is appended; KB injection follows the same rule |

---

## Sources

All findings are from direct codebase inspection. No external library research performed
because no new libraries are being added.

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — packet service template
- `DeckFlow.Web/Services/PacketArtifactStore.cs` — zip artifact pattern
- `DeckFlow.Web/Services/PacketSessionCache.cs` — session cache pattern
- `DeckFlow.Web/Services/PromptBuilders/Analysis/` — prompt variant pattern
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — combo data shape
- `DeckFlow.Web/Services/EdhTop16Client.cs` — metagame data shape
- `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` — category data shape
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` + `ContentSiteIndexStore.cs` — KB index store
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ContentSiteIndexRow` fields
- `DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs` — artifact path resolution
- `DeckFlow.Web/Services/ContentArtifactParser.cs` — front-matter parsing
- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` — flag gate pattern
- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` — bracket catalog shape
- `DeckFlow.Web/Models/AiPlatform.cs` — AI platform value object
- `.planning/seeds/deck-primer-generator.md` — feature design decisions
- `.planning/notes/deck-primer-prompt-design.md` — 31-section catalog + preset decisions
- `.planning/PROJECT.md` — milestone scope, stack constraints, "no new packages without approval" rule

---
*Stack research for: DeckFlow v1.5 — Deck Primer Generator + Content KB Integration*
*Researched: 2026-06-03*
