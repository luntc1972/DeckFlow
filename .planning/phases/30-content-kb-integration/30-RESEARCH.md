# Phase 30: Content KB Integration - Research

**Researched:** 2026-06-05
**Domain:** Content KB relevance scoring, prompt injection, "What Experts Say" panel, admin score preview
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** `is_kept` = `ContentSiteIndexRow.IsVisible`. No new per-clip schema. Injection parses `## Key Clips` from visible artifacts' markdown at prompt-build time.
- **D-02:** Inject clips only (never the Summary), in document order within an artifact. K=5 total across matched artifacts, filling from best-scoring artifact first. Deterministic selection.
- **D-03:** Injected clip set (source, title, timestamp, excerpt, harvest date, score) persisted in the packet session + zip artifact. Requires a zip allowlist entry + round-trip regression test.
- **D-04:** Clips exceeding the 150-word cap truncated at the last full sentence under the cap with an ellipsis.
- **D-05:** Commander relevance is free-text matching: commander name(s) searched against artifact title + summary + clip text (normalized, partner-aware). No new tag dimension.
- **D-06:** A commander-name hit counts as a relevance dimension in the ≥2-dimension AND gate: commander hit + bracket match qualifies even when archetype tags don't align.
- **D-07:** Deck-side archetype signal derived from existing deck data — category-knowledge distribution (tutor/counter-heavy → combo/control) plus commander free-text hit.
- **D-08:** Admin KBI-06 score = live test-input preview: admin sources view gets a small commander + bracket input; per-clip scores compute on demand through the exact production scoring path.
- **D-09:** Flip early — flag flip is the first execution unit: incremental harvest → commit artifacts → deploy → admin curates visible → flip `content.kb.enabled` via live /Admin/Flags → verify browse page.
- **D-10:** Injection + panel gated by same `content.kb.enabled` flag. Flag OFF = Expert Context block absent and panel hidden, reusing the KBI-05 empty-state code path. No second flag.
- **D-11:** "Fresh harvest" = incremental top-up. User runs local CLI harvest+distill over existing 5 channels. User-manual step; never auto-launched.

### Claude's Discretion

- Score weights, dimension weighting, injection threshold values — calibrated against the mandatory live tag-distribution audit.
- Commander-name normalization and partner/background handling details.
- Expert Context block placement within the three decoupled AI prompt variants (hand-edit all three; never extract shared guidance).
- Panel markup/CSS specifics (layout CSS in `site-common.css`; UI hint = yes).
- Plan split and sequencing beyond D-09's "flip first" ordering.

### Deferred Ideas (OUT OF SCOPE)

- Expert panel on DeckComparison / CedhMetaGap / DeckPrimer — KBI-F01 (v1.6+)
- Expert Context injection into other four prompt builders — KBI-F02 (v1.6+)
- Embedding-based semantic clip retrieval — KBI-F03
- Scheduled (cron) KB harvest cadence — KBI-F04
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| KBI-01 | `content.kb.enabled` flipped ON in prod with published KB content verified live | D-09 flip-first sequence; incremental harvest prerequisite; admin curation; /Admin/Flags toggle verified in AdminContentKbController |
| KBI-02 | Deck-analysis prompt artifact includes Expert Context block with top-K relevant curated clips | Tag-based relevance scoring design; `IAnalysisPromptVariant.Build` extension point identified; prompt budget hierarchy |
| KBI-03 | Injected clips formatted as block-quote pull-quotes with attribution | Artifact `## Key Clips` shape verified; clip attribution format `— Source, *Title* [MM:SS]` |
| KBI-04 | "What Experts Say" panel on DeckAnalysis result page with attribution, deep-link, harvest date | `DeckAnalysisViewModel` extension needed; new `ContentKbExcerpt` record; partial view pattern |
| KBI-05 | Graceful empty state — prompt omits Expert Context block; panel shows friendly message | Flag gate check per-request via `IFeatureFlagCache.IsEnabled`; null/empty return path; no empty section header |
| KBI-06 | Admin sources view shows per-clip relevance match score for curation tuning | `AdminContentKbController.Index` + `Index.cshtml` extension; D-08 live preview input |
</phase_requirements>

---

## Summary

Phase 30 wires the v1.4 Content KB into deck-analysis prompts and takes the KB live in production. Every building block is already deployed and proven: the site-index store, artifact resolver, front-matter parser, feature-flag cache, and analysis prompt variant registry all exist. The work is four integration seams — (1) a new `ContentKbRelevanceService` that scores artifacts against the deck context, (2) a new `ContentKbExcerpt` record persisted into the analysis zip, (3) a `## Expert Context` block inserted into each of the three analysis prompt variants, and (4) a "_ContentKbPanel.cshtml" partial added to the DeckAnalysis result page. A mandatory tag-distribution audit runs as the first executable task in the technical work — relevance thresholds and dimension weights are calibrated from its output, not hard-coded at research time.

The live artifact corpus (10 `.md` files committed to `content-kb/`) has been inspected during this research. The tag distribution is highly skewed: 6/10 artifacts have no bracket tag, 1/10 has no archetype tag. The D-09 incremental harvest will expand this corpus before thresholds are set. Scores and thresholds in the plan must be expressed as calibration placeholders that the tag-distribution audit task fills in — they must NOT be hard-coded constants before that audit runs.

One important discrepancy from the CONTEXT.md description of `PacketArtifactStore`: the code at `PacketArtifactStore.cs:598-601` does NOT silently drop unknown zip entries — it throws `InvalidOperationException`. This means a zip with an unlisted `expert-context.json` entry will fail load rather than silently discard it. The planner must model the allowlist addition as the prerequisite for D-03 zip persistence, exactly as with the `PrimerAllowedNames` pattern.

**Primary recommendation:** Plan the phase as three waves — (Wave 0) D-09 flag flip + incremental harvest + tag-distribution audit, (Wave 1) `ContentKbRelevanceService` + zip allowlist + `ContentKbExcerpt` record, (Wave 2) prompt-variant injection + DeckAnalysis panel + admin KBI-06 preview.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Clip relevance scoring | API / Backend (Web service) | — | Reads index + artifacts from disk; in-memory scoring; no new DB queries beyond `GetPublishedRowsAsync` |
| Expert Context prompt injection | API / Backend (Web service) | — | Each `IAnalysisPromptVariant.Build` appends independently; variants intentionally decoupled |
| Zip persistence of injected clips | API / Backend (Web service) | — | `PacketArtifactStore.BuildZip` extended with new allowlisted entry `32-expert-context.json` |
| "What Experts Say" panel | Frontend Server (SSR Razor) | — | `_ContentKbPanel.cshtml` partial; model data from `DeckAnalysisViewModel.ExpertContextClips` |
| Admin KBI-06 score preview | API / Backend (Web service) + SSR | — | `AdminContentKbController.Index` POST action + view extension; calls production scoring path |
| Feature-flag gating | API / Backend (Web service) | Frontend (panel hide) | `IFeatureFlagCache.IsEnabled("content.kb.enabled")` checked per-request in service and view |
| `content.kb.enabled` flag flip | User-manual ops | — | `/Admin/Flags` toggle; already wired in `AdminContentKbController` via `IFeatureFlagCache` |

---

## Standard Stack

### Core (all existing — zero new packages)

| Component | Where | Purpose | Phase 30 use |
|-----------|-------|---------|--------------|
| `IContentSiteIndexStore.GetPublishedRowsAsync` | `DeckFlow.Core/Content/` | Returns all `IsVisible=true` index rows | Load artifact catalog for scoring |
| `ContentKbArtifactPathResolver.ResolveArtifactFullPath` | `DeckFlow.Web/Services/` | Traversal-guarded artifact path | Read `.md` files for clip parsing |
| `ContentArtifactParser.SplitHeader` | `DeckFlow.Web/Services/` | Front-matter / body splitter | Extract tags + body for scoring; clip parsing extends this |
| `ContentTagVocabulary.Archetypes / Brackets` | `DeckFlow.Core/Knowledge/` | Tag allowlists | Dimension validation during scoring |
| `ContentSiteIndexRow` | `DeckFlow.Core/Knowledge/` | Slim index row with `IsVisible`, tag lists, `ArtifactPath` | Scoring input |
| `IFeatureFlagCache.IsEnabled("content.kb.enabled")` | `DeckFlow.Web/Services/FeatureFlags/` | Per-request flag check | Gate injection + panel; default-on when key missing |
| `AnalysisPromptVariantRegistry` + `IAnalysisPromptVariant` | `DeckFlow.Web/Services/PromptBuilders/Analysis/` | Registry dispatches by `AiPlatform` | Expert Context block appended in each variant's `Build` method |
| `PacketArtifactStore` allowlist + `BuildZip` | `DeckFlow.Web/Services/` | Zip artifact persistence | New `32-expert-context.json` allowlist entry + `BuildZip` parameter |
| `DeckAnalysisPacketService` | `DeckFlow.Web/Services/` | Packet orchestrator | Injects `ContentKbRelevanceService` result before `BuildAnalysisPrompt` call |
| `AdminContentKbController` + `Views/AdminContentKb/Index.cshtml` | `DeckFlow.Web/Controllers/Admin/` | Admin KB curation UI | Extended with preview input + per-row score column (KBI-06) |

### No New Packages

Per project CLAUDE.md and v1.5 research: no new NuGet dependencies. All capability is composition of existing services.

---

## Package Legitimacy Audit

> Phase installs no external packages. Section not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
DeckAnalysis Request (POST /Deck/DeckAnalysis, WorkflowStep=2)
        |
        v
DeckAnalysisPacketService.BuildAsync
        |
        |--- [existing] ComboTask, Scryfall lookups, BanList, ReferenceText build
        |
        |--- [NEW Phase 30] IFeatureFlagCache.IsEnabled("content.kb.enabled")
        |        |
        |        +-- OFF --> kbExcerpts = null  (skip all KB work)
        |        |
        |        +-- ON  --> ContentKbRelevanceService.GetRelevantClipsAsync(commanderName, bracket, deckCategories)
        |                         |
        |                         v
        |                 IContentSiteIndexStore.GetPublishedRowsAsync()
        |                         |
        |                         v
        |                 In-memory scoring loop (tag overlap + commander free-text)
        |                 ≥ 2-dimension AND gate → score threshold filter → K=5 selection
        |                         |
        |                         v
        |                 ContentKbArtifactPathResolver.ResolveArtifactFullPath(row.ArtifactPath)
        |                         |
        |                         v
        |                 File.ReadAllTextAsync → ContentArtifactParser + ## Key Clips parser
        |                         |
        |                         v
        |                 IReadOnlyList<ContentKbExcerpt> (≤5 clips, 150-word cap each)
        |
        v
BuildAnalysisPrompt(request, ..., kbExcerpts)
        |
        v
AnalysisPromptVariantRegistry.Build(platform, ..., kbExcerpts)
        |
    [ChatGPT] [Claude] [Gemini] — each independently appends ## Expert Context block
        |
        v
PacketArtifactStore.BuildZip(..., expertContextJson: Serialize(kbExcerpts))
    adds "32-expert-context.json" to allowlist → zip
        |
        v
DeckAnalysisPacketResult (ExpertContextClips added as new field)
        |
        v
DeckAnalysisViewModel (ExpertContextClips propagated)
        |
        v
DeckAnalysis.cshtml → @Html.PartialAsync("_ContentKbPanel", Model.ExpertContextClips)
        |
    [panel visible when clips != null && clips.Count > 0]
    [panel hidden entirely when clips null/empty OR flag OFF]
```

### Recommended Project Structure

```
DeckFlow.Web/Services/
├── ContentKbRelevanceService.cs        # NEW: IContentKbRelevanceService + sealed impl
DeckFlow.Web/Models/
├── ContentKbExcerpt.cs                 # NEW: sealed record — Source, Title, VideoUrl,
│                                       #       TimestampLabel, Excerpt, HarvestDate, Score
DeckFlow.Web/Views/Deck/
├── _ContentKbPanel.cshtml              # NEW: "What Experts Say" partial
DeckFlow.Web/Services/PromptBuilders/Analysis/
├── ChatGptAnalysisPromptVariant.cs     # MODIFIED: append ## Expert Context block
├── ClaudeAnalysisPromptVariant.cs      # MODIFIED: append ## Expert Context block
├── GeminiAnalysisPromptVariant.cs      # MODIFIED: append ## Expert Context block
DeckFlow.Web/Services/
├── DeckAnalysisPacketService.cs        # MODIFIED: inject IContentKbRelevanceService
├── PacketArtifactStore.cs              # MODIFIED: add "32-expert-context.json" to
│                                       #   PacketAllowedNames + new BuildZip parameter
DeckFlow.Web/Models/
├── DeckAnalysisViewModel.cs            # MODIFIED: add ExpertContextClips property
├── DeckAnalysisPacketResult (record)   # MODIFIED: add ExpertContextClips field
DeckFlow.Web/Controllers/Admin/
├── AdminContentKbController.cs         # MODIFIED: add Preview action (D-08)
DeckFlow.Web/Views/AdminContentKb/
├── Index.cshtml                        # MODIFIED: add commander+bracket input + score column
```

### Pattern 1: IAnalysisPromptVariant.Build signature extension

The `Build` method signature in `IAnalysisPromptVariant` currently accepts 9 parameters (lines 17-27 of `IAnalysisPromptVariant.cs`). Adding a `IReadOnlyList<ContentKbExcerpt>?` parameter as the last parameter is the cleanest extension — the three concrete implementations (ChatGPT, Claude, Gemini) each append the Expert Context block independently at the end of their `Build` body, after `## REFERENCE DATA` and `## DECKLIST`.

**Current prompt section order (ChatGPT variant, verified):**
1. Title line
2. Role instruction
3. `## DECK CONTEXT`
4. `## EVIDENCE RULES`
5. `## BRACKET GUIDANCE`
6. `## ANALYSIS QUESTIONS`
7. `## OUTPUT FORMAT` (with deck_profile schema)
8. Combo reference (if available)
9. `## REFERENCE DATA`
10. `## DECKLIST`

**Expert Context placement:** After `## DECKLIST` (last item). This respects the prompt budget hierarchy from Pitfall 4 — deck context → combo reference → questions → KB injection last. KB content is appended only after the core prompt is complete.

**Expert Context markdown format** (per KBI-03, D-02):
```
## Expert Context

The following clips were harvested [generated_utc date]; content may not reflect current meta.

> "[clip excerpt]"
> — Source Channel, *Video Title* [MM:SS]

> "[clip excerpt]"
> — Source Channel, *Video Title* [MM:SS]
```

When `kbExcerpts` is null or empty: no `## Expert Context` section header is emitted (Pitfall 3 guard). The prompt ends at `## DECKLIST` unchanged.

### Pattern 2: PacketArtifactStore allowlist addition

`ReadEntries` at line 585-619 of `PacketArtifactStore.cs` THROWS `InvalidOperationException` (not silent drop) when an entry name is not in the allowlist (`if (!allowedNames.Contains(entry.FullName)) throw ...`).

**Action required:** Add `"32-expert-context.json"` to `PacketAllowedNames` BEFORE any zip is built containing that entry. Extend `BuildZip` to accept an optional `string? expertContextJson` parameter. `LoadFromZip` reads it into a new `DeckAnalysisRequest` field or populates the `ExpertContextClips` directly on the result.

**IMPORTANT CORRECTION from CONTEXT.md:** CONTEXT.md states `PacketArtifactStore` "silently drops non-allowlisted names." The verified source code (line 598-601) THROWS. This is safer behavior than described — but means the allowlist addition is even more critical: any zip built before the allowlist entry is added AND loaded by code that already has the new entry will throw (version mismatch scenario). The planner must make the allowlist addition part of the same commit as the new BuildZip parameter.

**Round-trip regression test pattern** (from `PacketArtifactStoreTests.cs`):
```csharp
// Source: DeckFlow.Web.Tests/PacketArtifactStoreTests.cs
[Fact]
public void BuildZip_then_LoadFromZip_round_trips_response_json()
{
    var request = new DeckAnalysisRequest { DeckProfileJson = "..." };
    var bytes = PacketArtifactStore.BuildZip(request, ...);
    var loaded = new DeckAnalysisRequest();
    PacketArtifactStore.LoadFromZip(new MemoryStream(bytes), loaded);
    Assert.Contains("deck_profile", loaded.DeckProfileJson);
}
```
New test: `BuildZip_with_expert_context_round_trips_clips` — build a zip with `expertContextJson` populated; load it; assert all clip fields present with correct values.

### Pattern 3: ContentKbExcerpt record — { get; init; } requirement

New record for Phase 30 zip artifact serialization:
```csharp
// DeckFlow.Web/Models/ContentKbExcerpt.cs
public sealed record ContentKbExcerpt
{
    public required string Source { get; init; }        // NEVER { get; } — System.Text.Json skips it
    public required string Title { get; init; }
    public required string VideoUrl { get; init; }
    public required string TimestampLabel { get; init; } // e.g. "02:14"
    public required string Excerpt { get; init; }        // ≤150 words, truncated at sentence boundary
    public required DateTimeOffset HarvestDate { get; init; }  // from artifact generated_utc
    public double Score { get; init; }                   // relevance score for admin KBI-06
}
```

**Every property must use `{ get; init; }`** — the `{ get; init; }` → `{ get; }` regression has broken `EdhTop16Client` before (per CLAUDE.md). Include a serialization round-trip test:
```csharp
[Fact]
public void ContentKbExcerpt_roundtrips_through_json()
{
    var excerpt = new ContentKbExcerpt { Source = "TCG", Title = "Test", VideoUrl = "https://...",
        TimestampLabel = "01:23", Excerpt = "text", HarvestDate = DateTimeOffset.UtcNow, Score = 0.8 };
    var json = JsonSerializer.Serialize(excerpt);
    var loaded = JsonSerializer.Deserialize<ContentKbExcerpt>(json);
    Assert.Equal(excerpt.Source, loaded!.Source);
    Assert.Equal(excerpt.Score, loaded.Score);
}
```

### Pattern 4: FeatureFlagGate for injection (D-10)

`IFeatureFlagCache.IsEnabled("content.kb.enabled")` returns `true` (default-on) when the key is missing from the snapshot. This means:
- In dev/test environments where the flag has never been set: injection IS attempted.
- In production before D-09 flip: the flag row exists with value `false` → injection skipped.
- After D-09 flip: flag = `true` → injection enabled.

The flag check must be the FIRST statement in `ContentKbRelevanceService.GetRelevantClipsAsync` — before any DB or filesystem access. Return `null` immediately when flag is off.

Pattern from `ContentKbController.cs` line 51-53 (for route-level gating):
```csharp
[FeatureFlagGate("content.kb.enabled", Title = "Knowledge Base unavailable",
    Message = "The Knowledge Base is not currently available.")]
```

For the injection service (not a controller action), use `IFeatureFlagCache` directly per-call — not the attribute. The attribute is for controller routes only.

### Pattern 5: ContentArtifactParser + ## Key Clips parsing

`ContentArtifactParser.SplitHeader` returns `(Header: dict, Body: string)`. The `Body` starts after the closing `---` delimiter.

**Verified body structure** (from real artifacts):
```
## Summary
[prose]

## Key Clips
- **[02:14]** [timestamped excerpt text]
- **[08:47]** [timestamped excerpt text]

## Tags
[tag lines]
```

**Clip parsing algorithm:**
1. Locate `## Key Clips` section in body (find line `"## Key Clips"`)
2. Read bullet lines (`- **[MM:SS]** text`) until `## Tags` or end-of-body
3. For each bullet: extract timestamp label from `**[MM:SS]**` pattern; extract excerpt text (remainder of line after `**`)
4. Truncate at 150 words at last full sentence (D-04)

`SplitHeader` returns an empty `Header` dict (not exception) when `---` delimiter is missing — clip parser must handle empty-header artifacts gracefully (return no clips, no exception).

**Tag access from SplitHeader:** The current `SplitHeader` implementation only handles simple `key: value` pairs, not nested YAML. The `tags:` block (with sub-keys `archetype:`, `bracket:`, `card_category:`) is NOT parsed by `SplitHeader` — the values come from `ContentSiteIndexRow` (already stored as deserialized lists in the DB). Use `row.ArchetypeTags` and `row.BracketTags` from the index row — DO NOT re-parse front matter for tag data.

### Pattern 6: D-07 archetype derivation from deck category data

`ICategoryKnowledgeStore` is NOT injected into `DeckAnalysisPacketService` — confirmed by inspecting the service's constructor and grep for `CategoryKnowledge` usages (none found there). The category distribution is available only if the deck was submitted with inline categories (Moxfield/Archidekt format).

**Available at analysis time:**
- `request.TargetCommanderBracket` — the bracket string the user selected (e.g., `"cEDH"`, `"Optimized"`)
- `commanderName` — resolved oracle name
- Deck entries with category labels IF the import format included them (not guaranteed)
- `referenceText` already contains card data for the deck

**Practical D-07 implementation:** The `ContentKbRelevanceService` receives `commanderName` and `bracket` as direct inputs (both available at call time in `DeckAnalysisPacketService`). Archetype derivation from category distribution is an enhancement that requires the deck entries AND category knowledge lookup — it is NOT available from `DeckAnalysisPacketService` without additional DI wiring. The planner should design the service signature to accept `string? commanderName`, `string? bracket`, and optionally `IReadOnlyList<DeckEntry>?` entries for future archetype inference. For v1.5, derive archetype signal from:
1. Commander name free-text hit (D-05/D-06) — sufficient for a commander-specific match
2. Bracket match — `request.TargetCommanderBracket` maps directly to `ContentTagVocabulary.Brackets`
3. Archetype inference from commander name keywords (e.g., "Atraxa" → "combo/control") as a best-effort fallback — calibrated against the tag audit

### Pattern 7: Admin KBI-06 live preview (D-08)

`AdminContentKbController` currently takes `IContentSiteIndexStore`, `IContentKbSeedLoader`, `IFeatureFlagCache`, and `ILogger` in its constructor. Adding `IContentKbRelevanceService` to the constructor enables the preview action.

**Preview flow:**
- GET `/Admin/ContentKb` renders the index page (existing)
- Add a small form to `Index.cshtml` with commander name text input + bracket dropdown
- POST `/Admin/ContentKb/Preview?commanderName=X&bracket=Y` OR handle as query params in the Index GET
- Controller calls `ContentKbRelevanceService.ScoreAllAsync(commanderName, bracket)` which returns `IReadOnlyList<(ContentSiteIndexRow Row, double Score)>` — all rows with their scores, not just the top-K
- View renders a score column next to each entry's title/tags/status

The existing entries table in `Index.cshtml` (lines 82-end) already iterates `Model.Entries` — add a `RelevanceScore` column that shows the computed score when a preview input was submitted, or `—` when no preview is active.

### Anti-Patterns to Avoid

- **Emit `## Expert Context` header with no clips:** Confuses the AI with an empty section. The header is ONLY emitted when clips were found and selected. (Pitfall 3)
- **Re-parse front-matter for tags:** `SplitHeader` only handles flat key:value; nested YAML is not parsed. Use `ContentSiteIndexRow.ArchetypeTags` / `BracketTags` from the DB row.
- **Check feature flag at DI construction time:** Flag is designed for per-request runtime toggle. Always call `IFeatureFlagCache.IsEnabled(...)` inside the method, never in constructor.
- **Add `32-expert-context.json` to `PacketAllowedNames` after any zip is built containing it:** Causes `InvalidOperationException` on `ReadEntries` for all existing sessions. Same commit.
- **Use `{ get; }` on `ContentKbExcerpt` properties:** System.Text.Json silently skips them in .NET 9+. Must be `{ get; init; }`.
- **Hard-code relevance thresholds at plan time:** Thresholds must be placeholders calibrated by the tag-distribution audit task. Never `score >= 2.0` as a hard constant in the plan.
- **Inject KB content even when prompt budget is tight:** Measure total prompt length before appending Expert Context; skip injection if remaining budget < the Expert Context block size. The ~4KB cap from Pitfall 4 applies.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Front matter parsing | Custom YAML parser | `ContentArtifactParser.SplitHeader` | Already exists, tested, handles edge cases |
| Artifact path resolution | String concatenation | `ContentKbArtifactPathResolver.ResolveArtifactFullPath` | Has path-traversal guard; falls back gracefully when content-kb dir absent |
| Feature flag check | Custom DB read | `IFeatureFlagCache.IsEnabled(...)` | Already registered in DI; cached snapshot; per-request safe |
| Bracket string mapping | Custom enum | `ContentTagVocabulary.Brackets` (HashSet, OrdinalIgnoreCase) | Allowlist already exists |
| Tag normalization | `string.ToLower()` | `ContentTagVocabulary.IsValid(dimension, value)` + OrdinalIgnoreCase | Normalizes and validates in one call |

**Key insight:** The entire KB retrieval pipeline is already deployed and proven in Phase 22. Phase 30 adds a scoring layer on top of `GetPublishedRowsAsync` — it does not rebuild any retrieval infrastructure.

---

## Runtime State Inventory

> Phase 30 is NOT a rename/refactor phase. Standard omit rule applies. The one ops item is noted here for the planner's sequencing awareness, not as a data migration item.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | `content.kb.enabled` flag row in `feature_flags` table (Postgres prod): value = `false` | D-09: user flips via `/Admin/Flags` toggle UI — no code change, no migration |
| Live service config | KB content in `content-kb/` directory under `MTG_DATA_DIR=/data` on Render (10 artifacts committed to git; incremental harvest adds more) | D-11: user runs local CLI harvest + commits; deploy picks up new artifacts |
| OS-registered state | None | None |
| Secrets/env vars | None — no new secrets | None |
| Build artifacts | None | None |

---

## Common Pitfalls

### Pitfall 1: PacketArtifactStore throws on unknown zip entry (not silent drop)

**What goes wrong:** CONTEXT.md describes `PacketArtifactStore` as "silently drops non-allowlisted names." The actual code at line 598 throws `InvalidOperationException`. A deployed version that builds zips with `32-expert-context.json` BUT does NOT yet have it in `PacketAllowedNames` will cause every subsequent zip upload to throw.

**How to avoid:** The zip allowlist entry `"32-expert-context.json"` added to `PacketAllowedNames` MUST be in the same commit as the first `BuildZip` call that writes it. Never add the BuildZip parameter first and the allowlist later.

**Warning signs:** `InvalidOperationException: Imported zip contains an unsupported entry: 32-expert-context.json` in the application logs.

### Pitfall 2: Tag distribution is sparse at research time — thresholds cannot be hard-coded

**What goes wrong:** The live corpus has 10 artifacts. Tag distribution inspection reveals:
- 6/10 have no bracket tag (`bracket: []`)
- 5/10 are tagged cEDH (bracket)
- 1/10 is tagged Exhibition
- Most archetype tags lean toward "combo", "ramp", "value-engine"
- 1/10 has no archetype tag at all
- 1/10 has no archetype AND no bracket tag

A hard-coded `≥2 dimensions` AND gate with this corpus would return 0 results for most non-cEDH decks, because bracket tags are sparsely populated. The D-09 incremental harvest and admin curation must run BEFORE thresholds are set.

**How to avoid:** Express all score thresholds as named constants with calibration-placeholder comments in the plan. The tag-distribution audit task (Wave 0) produces a query result that the implementing agent uses to set the actual values.

**Warning signs:** Integration test for a non-cEDH deck returns 0 clips when the corpus has non-cEDH artifacts.

### Pitfall 3: SplitHeader flat-key-only parser cannot extract tags from front matter

**What goes wrong:** `SplitHeader` parses only flat `key: value` pairs. The artifact front matter uses nested YAML (`tags:\n  archetype: [...]`). Attempting to use `SplitHeader` to extract tag data returns the literal `tags:` key with an empty value.

**How to avoid:** Use `ContentSiteIndexRow.ArchetypeTags` / `BracketTags` / `CardCategoryTags` from the DB row. These are pre-deserialized from the JSON array stored in the database. Never re-parse the `.md` file for tag data — only parse it for clip text.

### Pitfall 4: `{ get; init; }` regression on ContentKbExcerpt (known regression class)

**What goes wrong:** IDE or Codex formatting pass converts `{ get; init; }` → `{ get; }` on `ContentKbExcerpt` properties. `System.Text.Json` silently drops get-only properties in .NET 9+. The JSON in `32-expert-context.json` contains `{}` or partial objects. Panel shows no data after zip reload.

**How to avoid:** Every `ContentKbExcerpt` property must use `{ get; init; }`. Include a serialization round-trip test (see Pattern 3 above). The plan's CONTEXT.md must contain the explicit constraint verbatim.

### Pitfall 5: Prompt budget — Expert Context block can push Gemini over paste cap

**What goes wrong:** Gemini web UI paste cap is ~30-60KB. Existing analysis prompt for a large deck is ~35-50KB. Adding 5 clips × ~150 words × ~6 chars/word = ~4,500 chars (~4.4KB) pushes the total toward 54KB, into the Gemini risk zone.

**How to avoid:** Measure total prompt length after assembling the core prompt; inject Expert Context only if `totalLength + expertContextLength <= 50,000` chars. Skip injection for Gemini if the prompt is already large. The budget cap is already an established constraint in the codebase (Pitfall 4 in PITFALLS.md).

---

## Code Examples

### Clip parsing from `## Key Clips` section

```csharp
// [VERIFIED: direct inspection of content-kb/*.md artifacts]
// Bullet format: "- **[02:14]** excerpt text here"
private static readonly Regex ClipBulletRegex = new(
    @"^\s*-\s*\*\*\[(?<ts>[^\]]+)\]\*\*\s*(?<text>.+)$",
    RegexOptions.Compiled | RegexOptions.Multiline);

// Parse ## Key Clips section from artifact body:
// 1. Find "## Key Clips" line
// 2. Read until "## Tags" or end of body
// 3. Match each line against ClipBulletRegex
```

### Relevance scoring dimensions

```csharp
// [ASSUMED] — exact weights are calibration placeholders; tag audit sets actual values
internal static double ScoreArtifact(
    ContentSiteIndexRow row,
    string? normalizedCommanderName,   // D-05 free-text match input
    string? deckBracket,               // from request.TargetCommanderBracket
    IReadOnlySet<string> deckArchetypes) // D-07 derived from deck data
{
    double score = 0.0;
    int dimensionsHit = 0;

    // Dimension 1: bracket match
    if (!string.IsNullOrEmpty(deckBracket)
        && row.BracketTags.Any(t => string.Equals(t, deckBracket, StringComparison.OrdinalIgnoreCase)))
    {
        score += /* CALIBRATE_BRACKET_WEIGHT */ 1.0;
        dimensionsHit++;
    }

    // Dimension 2: archetype overlap (D-07)
    var archetypeOverlap = row.ArchetypeTags.Count(t =>
        deckArchetypes.Contains(t, StringComparer.OrdinalIgnoreCase));
    if (archetypeOverlap > 0)
    {
        score += archetypeOverlap * /* CALIBRATE_ARCHETYPE_WEIGHT */ 0.5;
        dimensionsHit++;
    }

    // Dimension 3: commander free-text hit (D-05/D-06)
    if (!string.IsNullOrEmpty(normalizedCommanderName)
        && ContainsCommanderName(row, normalizedCommanderName))
    {
        score += /* CALIBRATE_COMMANDER_WEIGHT */ 2.0;
        dimensionsHit++;
    }

    // ≥ 2-dimension AND gate (D-06)
    return dimensionsHit >= 2 ? score : 0.0;
}
```

### IAnalysisPromptVariant signature extension

```csharp
// [VERIFIED: DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs]
// Add optional kbExcerpts parameter as last argument:
internal interface IAnalysisPromptVariant
{
    AiPlatform Platform { get; }
    string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions,
        IReadOnlyList<ContentKbExcerpt>? kbExcerpts = null); // NEW — optional, default null
}
```

### Expert Context block appended in ChatGPT variant (after ## DECKLIST)

```csharp
// [VERIFIED: ChatGptAnalysisPromptVariant.cs ends at line 249 with builder.ToString().TrimEnd()]
// Expert Context is appended immediately before TrimEnd():
if (kbExcerpts is { Count: > 0 })
{
    var harvestDate = kbExcerpts[0].HarvestDate.ToString("yyyy-MM-dd");
    builder.AppendLine();
    builder.AppendLine("## Expert Context");
    builder.AppendLine($"The following clips were harvested {harvestDate}; content may not reflect current meta.");
    builder.AppendLine();
    foreach (var clip in kbExcerpts)
    {
        builder.AppendLine($"> \"{clip.Excerpt}\"");
        builder.AppendLine($"> — {clip.Source}, *{clip.Title}* [{clip.TimestampLabel}]");
        builder.AppendLine();
    }
}
// Claude + Gemini variants: same Expert Context prose block, hand-edited independently.
// NEVER extract shared Expert Context prose — prompt variants are intentionally decoupled.
```

---

## Live Tag-Distribution Audit (Pre-Implementation Mandatory Step)

The following query must run against the prod DB as the first technical task (Wave 0). Until it runs, all threshold values in the plan remain placeholders.

**What to query:**
```sql
-- Bracket tag distribution across visible rows
SELECT tags_bracket, COUNT(*) as cnt
FROM content_site_index
WHERE is_visible = true
GROUP BY tags_bracket ORDER BY cnt DESC;

-- Archetype tag distribution (normalized JSON array)
SELECT tags_archetype, COUNT(*) as cnt
FROM content_site_index
WHERE is_visible = true
GROUP BY tags_archetype ORDER BY cnt DESC;

-- Rows with empty bracket tag
SELECT COUNT(*) FROM content_site_index
WHERE is_visible = true AND (tags_bracket = '[]' OR tags_bracket IS NULL);
```

**Research-time observation (dev corpus — 10 artifacts only, pre-harvest):**

| Bracket | Count (10 total) |
|---------|-----------------|
| cEDH | 5 |
| Exhibition | 1 |
| (empty) | 6 |

| Archetype | Occurrence count (multi-tag, overlapping) |
|-----------|------------------------------------------|
| combo | 5 |
| ramp | 5 |
| value-engine | 4 |
| control | 3 |
| reanimator | 3 |
| lands | 2 |
| tribal | 2 |
| spellslinger | 2 |
| aristocrats | 2 |
| midrange | 1 |
| stax | 1 |
| aggro | 1 |
| voltron | 1 |
| (empty) | 1 |

**Planning implication:** With 60% of artifacts having no bracket tag, a strict bracket-required gate would match only cEDH/Exhibition decks against the current corpus. The planner should model the scoring so that bracket match is a score bonus rather than a hard filter — or use commander free-text hit as a qualifying dimension when bracket is empty. The tag audit on the expanded prod corpus (post D-11 harvest) will determine which approach is viable.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `PacketAllowedNames` (analysis only) | Three separate HashSets: `PacketAllowedNames`, `ComparisonAllowedNames`, `CedhAllowedNames` | v1.2 (Phase 10) | Phase 30 adds a new entry to `PacketAllowedNames` only (analysis workflow) |
| `IAnalysisPromptVariant.Build` without KB excerpts | Same signature, new optional `kbExcerpts` parameter | Phase 30 (new) | All three variants updated independently — no shared prose |
| `DeckAnalysisPacketResult` record | 13-parameter positional record | Phase 15 refactor | Phase 30 adds `ExpertContextClips` via optional named parameter (last position) |

**Deprecated/outdated:**
- `PacketArtifactStore` "silently drops" description in CONTEXT.md: the actual code THROWS. Plan against the throwing behavior.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Archetype derivation from deck entries is NOT available in `DeckAnalysisPacketService` without additional DI wiring (no `ICategoryKnowledgeStore` injected) | Architecture Patterns, Pattern 6 | Low — the constructor code was inspected and no category store injection was found; if wrong, archetype derivation could be richer |
| A2 | Adding an optional `kbExcerpts` parameter to `IAnalysisPromptVariant.Build` at the tail position is the correct extension point (vs. passing through a container object) | Pattern 1 | Medium — alternative is a new `AnalysisPromptContext` record that combines all inputs; either works but context object would require more refactoring |
| A3 | The incremental CLI harvest will expand the corpus enough to calibrate thresholds meaningfully; with ≥50 visible artifacts the bracket sparse-tag problem may be materially different | Tag-Distribution Audit section | Medium — if bracket tags remain sparse after harvest, the ≥2-dimension gate design needs adjustment (e.g., commander free-text + archetype may be the primary dimensions) |
| A4 | `ContentKbExcerpt` is serialized to JSON for the zip entry using `System.Text.Json` (project standard) | Pattern 3 | Low — all other zip artifact serialization uses System.Text.Json |

**If this table is empty:** All other claims in this research were verified by direct codebase inspection.

---

## Open Questions (RESOLVED)

1. **CategoryKnowledgeStore availability in DeckAnalysisPacketService context** — RESOLVED: D-07 is a locked user decision; plan 30-02 wires `ICategoryKnowledgeStore` into `ContentKbArchetypeDeriver` via DI (the recommendation below is overruled; keyword fallback is last-resort only when the commander has no category rows).
   - What we know: `ICategoryKnowledgeStore` is NOT currently injected into `DeckAnalysisPacketService` (constructor grep returned no matches)
   - What's unclear: whether the planner should wire it in for Phase 30's D-07 archetype derivation, or accept commander-name + bracket as sufficient inputs for v1.5
   - Recommendation: For v1.5, accept `commanderName` + `bracket` + optional `IReadOnlyList<DeckEntry>?` in `ContentKbRelevanceService` — derive archetype from keyword matching on commander name. Adding `ICategoryKnowledgeStore` to `DeckAnalysisPacketService` is scope expansion; defer to v1.6 or make it a discretion-area decision in the plan.

2. **`DeckAnalysisPacketResult` record extension (positional record)** — RESOLVED: per the recommendation; plan 30-03 adds `ExpertContextClips` as the last optional parameter.
   - What we know: `DeckAnalysisPacketResult` is a positional record with 13 named optional parameters (lines 43-56)
   - What's unclear: whether `ExpertContextClips` should be a new optional parameter on the record or surfaced only in `DeckAnalysisViewModel`
   - Recommendation: Add `IReadOnlyList<ContentKbExcerpt>? ExpertContextClips = null` as the last parameter. This propagates clips through the packet→zip→view pipeline naturally. The packet service sets it; the controller reads it into the view model.

3. **D-08 admin preview — GET with query params vs. POST action** — RESOLVED: per the recommendation; plan 30-04 uses GET query params `previewCommander`/`previewBracket` on Index.
   - What we know: The current `Index` action is a GET with no parameters; `SetVisibility` and `BulkSetVisibility` are POST
   - What's unclear: whether D-08 preview should be a GET (commander + bracket in query string, re-renders Index with scores) or a separate POST endpoint
   - Recommendation: GET with query params is simplest — `?previewCommander=X&previewBracket=Y` appended to the Index action renders the score column. No additional round-trip; same CSRF model as existing admin page.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `content-kb/` artifact tree | Clip parsing | ✓ (10 artifacts committed) | Varies by artifact | — |
| Postgres (prod) | D-09 flag flip, tag audit | ✓ (Render prod) | 15.x | — |
| CLI harvest tool (`DeckFlow.CLI`) | D-11 incremental harvest | ✓ (existing) | current | — |
| `dotnet` (.NET 10 SDK) | Build + test | ✓ | .NET 10 | — |

**Missing dependencies with no fallback:** None.

---

## Security Domain

> `security_enforcement` is not explicitly `false` in config.json; section required.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Admin section already behind `BasicAuthMiddleware` |
| V3 Session Management | No | No new session state |
| V4 Access Control | Yes (KBI-06) | Admin preview behind existing `/Admin` BasicAuth branch; `SameOriginRequestValidator` on any mutating POST |
| V5 Input Validation | Yes (D-08 inputs) | Commander name and bracket inputs from admin preview form: normalize/sanitize before passing to scoring service; bracket validated against `ContentTagVocabulary.Brackets` allowlist |
| V6 Cryptography | No | No new cryptography |

### Known Threat Patterns for this Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| KB content contains prompt-injection text (e.g., "Ignore previous instructions") | Tampering | KB content goes through `Markdig.DisableHtml()` before injection (per existing `ContentKbController` pipeline); KB is admin-curated so threat surface is lower |
| Path traversal via `ArtifactPath` in `ContentSiteIndexRow` | Elevation of Privilege | `ContentKbArtifactPathResolver.ResolveArtifactFullPath` uses `Path.GetFullPath` + `Path.Combine` with the ContentBase anchor |
| D-08 preview input used for injection | Tampering | Validate `previewBracket` against `ContentTagVocabulary.Brackets`; normalize `previewCommander` via existing `NormalizeSingleLine` pattern |
| `content.kb.enabled` flag bypassed by direct service call | Elevation of Privilege | Flag check must be the FIRST statement in `ContentKbRelevanceService.GetRelevantClipsAsync` |

---

## Sources

### Primary (HIGH confidence — direct codebase inspection)

- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ArtifactFileFormat`, `ContentSiteIndexRow` fields, tag serialization helpers
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — Archetypes (15), Brackets (5: Exhibition/Core/Upgraded/Optimized/cEDH), CardCategories (11)
- `DeckFlow.Core/Knowledge/ContentModels.cs` lines 192-202 — `ContentTagDimension` constants
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — `GetPublishedRowsAsync`, `GetAllRowsAsync`, visibility setters
- `DeckFlow.Web/Services/ContentArtifactParser.cs` — `SplitHeader` implementation (flat-key only, nested YAML not parsed)
- `DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs` — traversal-guarded path resolution, `ContentBase`, fallback behavior
- `DeckFlow.Web/Services/PacketArtifactStore.cs` lines 27-70, 585-619 — three HashSet allowlists (PacketAllowedNames/ComparisonAllowedNames/CedhAllowedNames); `ReadEntries` THROWS on unknown entry (not silent drop)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` lines 562-605, 1031-1038 — combo null-handling precedent; `BuildAnalysisPrompt` delegates to registry; no `ICategoryKnowledgeStore` injection
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` — 9-param `Build` signature
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` lines 1-251 — prompt section order (DECK CONTEXT → EVIDENCE RULES → BRACKET GUIDANCE → ANALYSIS QUESTIONS → OUTPUT FORMAT → combo ref → REFERENCE DATA → DECKLIST)
- `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` — AiPlatform dispatch, Default fallback
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — resolves `IFeatureFlagCache` from `RequestServices` per-invocation
- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` — `IsEnabled` returns `true` when key is missing (default-on)
- `DeckFlow.Web/Controllers/ContentKbController.cs` lines 51-53 — `[FeatureFlagGate("content.kb.enabled", ...)]` usage pattern
- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` lines 1-160 — constructor, `Index` action, `SetVisibility`, `BulkSetVisibility`, `IFeatureFlagCache` usage
- `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` — existing table structure (Title/Source/Tags/Status/Action columns)
- `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` — existing fields; `ExpertContextClips` not yet present
- `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` — round-trip test pattern (build → load → assert)
- `content-kb/**/*.md` (10 artifacts inspected) — live `## Key Clips` format; tag distribution; bracket sparsity

### Secondary (MEDIUM confidence)

- `.planning/research/PITFALLS.md` Pitfall 3 (tag-mismatch AND gate), Pitfall 4 (prompt budget ~4KB cap), Pitfall 7 (stale content freshness)
- `.planning/research/SUMMARY.md` Track B design, `IContentKbRelevanceService` new component description
- `.planning/phases/30-content-kb-integration/30-CONTEXT.md` — 11 locked decisions (D-01..D-11)

---

## Metadata

**Confidence breakdown:**
- Artifact format and clip shape: HIGH — 10 real artifacts inspected, format confirmed
- PacketArtifactStore mechanics: HIGH — source code directly inspected; THROWS correction documented
- Prompt variant extension point: HIGH — all three variant files and registry inspected
- Tag distribution: MEDIUM — dev corpus only (10 artifacts); prod corpus after harvest may differ materially
- D-07 archetype derivation: MEDIUM — `ICategoryKnowledgeStore` not available in `DeckAnalysisPacketService` context; derivation fallback via commander name keyword matching is [ASSUMED] as sufficient for v1.5
- Relevance scoring thresholds/weights: LOW (intentional) — these are calibration placeholders; the tag-distribution audit (Wave 0) produces the actual values

**Research date:** 2026-06-05
**Valid until:** 2026-06-19 (stable codebase; tag-distribution changes daily with harvest)
