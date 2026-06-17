# Architecture Patterns — v1.6 Integration

**Domain:** DeckFlow v1.6 — Content KB Retrieval Fix + Value Re-Validation + Conditional Philosophy-Profile + SRP Split
**Researched:** 2026-06-10
**Source files verified:**
  ContentKbRelevanceService.cs, ContentKbClipParser.cs, DeckController.cs (1841 lines),
  DeckAnalysisPacketService.cs (1755 lines), CommandRunners.cs (1902 lines),
  ContentSiteIndexStore.cs (schema/migration patterns), creator-philosophy-profile.md (seed),
  spikes/001-kb-value-ab/VERDICT.md, .planning/PROJECT.md

---

## 1. Retrieval Fix — Where the Logic Lives and What Changes

### Current pipeline (as built)

```
GetPublishedRowsAsync()           -- IContentSiteIndexStore → SQLite/Postgres
  └─ ParseRowsAsync()             -- reads artifact markdown, builds ScoreInput per row
       └─ ScoreArtifact()         -- bracket + archetype + commander free-text → gated score
SelectTopClips()                  -- iterate rows ordered by score; fill MaxClips (5) top-to-bottom
EstimateRenderedChars() / trim    -- budget gate
```

`SelectTopClips` has no per-video cap. The highest-scoring row fills all 5 slots before any other
row is considered. This is the defect.

### Integration point: refactor `SelectTopClips` in-place; extract scorer separately

**Do not split `ContentKbRelevanceService` into a new class.** The existing internal test ctor
(`readArtifactAsync` seam + `resolveArtifactPath` seam) already isolates the scorer for
deterministic unit tests without I/O. Adding a new abstraction boundary around the scorer adds DI
surface and breaks test seam symmetry without benefit.

**Two surgical changes:**

(a) `SelectTopClips` — add a per-video cap (e.g. `MaxClipsPerVideo = 2`). Change: iterate
rows in descending score order, track clips-contributed per `row.Row.Id`, skip a row once its
per-video cap is hit. Total `MaxClips` budget unchanged.

```
// Before: inner loop exhausts all clips from the top-scoring row
// After:  skip clip if clipsPerVideo[row.Row.Id] >= MaxClipsPerVideo
```

(b) Scoring function — replace tag-overlap as the primary signal with topical-fit filtering.
The spike identified the root cause: broad tag sets on tangential videos (e.g., "Glass Cannon
Commanders" tags: midrange/combo/value-engine/ramp/aggro + Upgraded/Optimized/cEDH) outscore
narrowly-relevant videos because archetype overlap counts raw hits. Fix options in order of
preference:

  - Commander-name hit as a hard **bonus multiplier** (not an additive dimension), so a video
    that mentions the commander by name is ranked strictly above one that only shares tags.
  - Content-text semantic overlap: check if clip excerpts + summary reference the deck's
    primary archetypes as concepts, not just tag labels (light TF-IDF or word-overlap on
    `SearchText` vs. a derived deck description string).

The `ScoreArtifact` static method is `internal` and directly tested in
`ContentKbRelevanceServiceTests`. Changing its weight constants / logic is safe; just update the
calibration tests alongside.

**What is NOT modified:** `GetMergedClipsAsync` 4-tier merge (pinned/followed/auto/evergreen),
`ContentKbClipParser`, `BuildScoreInputAsync`, the `internal` test ctor signature, or the
`IContentKbRelevanceService` interface surface. The interface is consumed by
`DeckAnalysisPacketService` (optional injection, `IContentKbRelevanceService?`) and the
`AdminContentKbController` (score preview). Neither consumer needs to change.

**SQLite/Postgres parity:** `SelectTopClips` and `ScoreArtifact` are pure in-process operations
over already-fetched rows. They never issue SQL. No parity concern here.

---

## 2. Value Re-Validation Gate

After the retrieval fix ships, re-run `Spike001KbValueAbHarness` (the gold A/B harness already
wired in `DeckFlow.Web.Tests`). The harness forces `content.kb.enabled` on in-process and uses the
real `ContentKbRelevanceService`. This is a test-only execution path; no production code changes
for the gate itself.

Gate outcome drives the conditional branch:
- **Lift confirmed** → build philosophy-profile (Phase 3 below), flip `content.kb.enabled` ON,
  close SEL-02 expert-pin re-confirm in the same window.
- **Still marginal/negative** → retire or pivot the feature; SRP split still runs (Phase 4) as it
  is independent.

---

## 3. Creator Philosophy-Profile (CONDITIONAL on gate clearing)

### Where the profile lives

**Offline synthesis (CLI):** Reuse the existing `RunDistillAsync` pipeline in `CommandRunners`
(`DeckFlow.CLI`). The distill pipeline already: reads transcript text from `ContentVideoStore`,
calls the pluggable `ILlmDistillationService` (openai/claude CLI backends), writes a markdown
artifact, and upserts to `IContentSiteIndexStore`. A new `RunSynthesizePhilosophyAsync` runner
follows the same pattern: reads all published artifacts for a given source slug, calls the LLM
with a profile-synthesis prompt, writes a per-creator profile artifact to disk.

**Storage:** Two options; the simpler one wins.

  - Option A (preferred): Per-creator profile as a markdown artifact on disk
    (`artifacts/content-kb-profiles/<source-slug>.md`) + a single new table
    `creator_philosophy_profiles` with columns `(source TEXT PK, artifact_path TEXT,
    synthesized_utc TIMESTAMPTZ, content_hash TEXT, principles_json JSONB/TEXT)`.
    The `principles_json` column stores the structured principle-list with provenance (video id +
    date per principle) so retrieval can filter by recency or video scope without re-parsing the
    markdown.
  - Option B: Store the whole profile as a JSONB column, no artifact file. Harder to debug and
    doesn't reuse the existing `ContentKbArtifactPathResolver` + artifact-read seam.

Use Option A.

**Schema migration pattern:** Follow `is_evergreen` precedent. `EnsureSchemaAsync` in
`ContentSiteIndexStore`-style class with `CREATE TABLE IF NOT EXISTS` + column-presence check for
forward-compat. Use `IRelationalDialect` for SQLite vs. Postgres syntax (`BOOLEAN NOT NULL DEFAULT
FALSE` vs. `INTEGER NOT NULL DEFAULT 0`; `RETURNING id` clause vs. `SELECT last_insert_rowid()`).
New store: `ICreatorPhilosophyProfileStore` in `DeckFlow.Core/Content/` implementing
`EnsureSchemaAsync`, `UpsertProfileAsync`, `GetProfileAsync(string sourceSlug)`,
`GetAllProfilesAsync`. Wire into `Program.cs` beside the other `IContent*Store` registrations.

**Retrieval and injection at analysis time:**

At `DeckAnalysisPacketService.BuildAsync` time, after the existing Expert Context clips are
assembled, a new optional dependency `ICreatorPhilosophyProfileService?` is resolved. This follows
the exact same optional-injection pattern as `IContentKbRelevanceService?` (null when flag is off
or profile store is empty). The service:

1. Receives the set of source slugs represented in the selected clips (available from
   `clip.Source` on each `ContentKbExcerpt`).
2. Loads the profile artifact(s) for those sources via `ICreatorPhilosophyProfileStore`.
3. Retrieves the most relevant principles for this deck's archetypes using the stored
   `principles_json` (filter by archetype overlap + recency-weight).
4. Returns a `CreatorPhilosophyContext` record: `IReadOnlyList<PhilosophyPrinciple>` where each
   principle carries `SourceSlug`, `Principle` text, `SourceVideoId`, and `SourceDate`.

`DeckAnalysisPacketService.BuildAnalysisPrompt` already accepts `IReadOnlyList<ContentKbExcerpt>?
kbExcerpts`. It will accept an additional optional `CreatorPhilosophyContext? philosophyContext`.
This injects into the `## Expert Context` block as a second sub-section ("Creator Heuristics")
beneath the existing clip pull-quotes. The prompt variant registry (`AnalysisPromptVariantRegistry`
/ `IAnalysisPromptVariant.BuildPrompt`) passes the context through. All three AI variants
(ChatGPT / Claude / Gemini) receive it — consistent with the existing per-AI but same-structure
pattern.

**Provenance requirement (from seed):** Each principle carries `SourceVideoId + SourceDate`.
The prompt renders them as attributed statements: `"[Creator] (from [VideoTitle], [Date]):
[principle text]"`. This satisfies the "hallucination gate" requirement in the seed — no
free-floating assertions.

**Offline synthesis wiring in CLI:**

```
dotnet run --project DeckFlow.CLI -- synthesize-philosophy --source <slug> [--db <path>]
```

New CLI command `synthesize-philosophy` registered in `DeckFlow.CLI/Program.cs` alongside
`distill`. `CommandRunners.RunSynthesizePhilosophyAsync` reads published artifacts for the source,
calls `ILlmDistillationService.SynthesizePhilosophyAsync` (new method on the existing interface,
or a parallel interface `ILlmPhilosophySynthesizer` — prefer adding to the existing interface to
avoid a second factory).

**What is NOT modified:** `ContentKbClipParser`, the harvest pipeline, `ContentVideoStore`,
existing `ContentSiteIndexStore` columns, or the `ExpertSelection` / pin/follow/evergreen tiers.
The philosophy-profile is an additive injection path alongside the clip-excerpt path.

---

## 4. DeckController / CommandRunners SRP Split

### DeckController: current state

1841 lines, 13 injected service dependencies, routing surfaces:
- Utility tools: Sync, Convert, CardLookup, MechanicLookup, JudgeQuestions, SuggestCategories
- AI packet workflows: DeckAnalysis (GET/POST/download/upload), DeckComparison (GET/POST/download/upload), CedhMetaGap (GET/POST/download/upload), DeckPrimer (GET/POST/download/upload)
- API: GetSetOptions, ConvertCommanderSearch, CardSearch, SingleCardLookup
- Infrastructure: Home, Error, Resolve

Each packet workflow (analysis/comparison/cedh/primer) has a symmetrical 4-action pattern:
`GET` (fresh state), `POST` (build), `POST /download` (zip), `POST /upload` (restore from zip).

### Recommended split: 3 new controllers

All controllers remain in `DeckFlow.Web/Controllers/`. Routes do not change (route attributes are
per-action, not per-controller class in MVC). This is a rename/move, not a URL change.

| New Controller | Actions | Services Retained |
|----------------|---------|-------------------|
| `DeckToolsController` | Sync (GET+POST+Resolve), Convert (GET+POST+CommanderSearch), CardLookup (GET+download+download-json+single), MechanicLookup (GET+POST), SuggestCategories (GET+POST+CardSearch), JudgeQuestions (GET) | IDeckSyncService, IDeckConvertService, ICardSearchService, ICardLookupService, IMechanicLookupService, ICategorySuggestionService |
| `DeckPacketController` | DeckAnalysis (GET/POST/download/upload), DeckComparison (GET/POST/download/upload), CedhMetaGap (GET/POST/download/upload) | IDeckAnalysisPacketService, IDeckComparisonService, IMetaGapService, PacketSessionCache, IScryfallSetService |
| `DeckPrimerController` | DeckPrimer (GET/POST/download/upload) | IDeckPrimerPacketService, PacketSessionCache |

`DeckController` retains: `Home`, `Error`, `GetSetOptions` (shared API endpoint) — or `GetSetOptions` moves to `DeckPacketController` since it serves the analysis page.

**Why this split:** The packet workflows (analysis/comparison/cedh/primer) share the cache pattern, zip artifact pattern, and `PacketSessionCache`. The utility tools (sync, convert, lookup, suggest) share no state with the packet workflows. DeckPrimer is isolated enough (one service, one workflow) to justify its own controller.

**Regression risk:** Low. The split is mechanical — cut action methods, update constructor, update DI (all registrations are already in `Program.cs` with interface keys; no new registrations needed). The key risk is the shared `BuildViewModel` and `TryGetSetOptionsAsync` helpers — check each and decide which controller owns them (BuildViewModel → DeckToolsController; TryGetSetOptionsAsync → DeckPacketController).

**Test impact:** Existing controller tests use `NullLogger<DeckController>.Instance`; they will need to reference the new controller type name. That's the entire test-side change.

### CommandRunners: current state

1902 lines, containing two completely separate domains in one static class:
- **Deck domain runners:** `RunCompareAsync`, `RunProbeAsync`, `RunExportMoxfieldAsync`, `RunArchidektCategoriesAsync`, `RunArchidektCategoryCardsAsync`, `RunArchidektHarvestRecentAsync`, `RunArchidektCacheAsync`, `RunCategoryFindAsync`, `RunCardLookupAsync`, `RunScryfallProbeAsync`
- **Content KB runners:** `RunContentSourceAddAsync`, `RunDistillAsync`, `RunHarvestAsync`, `RunContentIndexExportAsync`, plus ~20 private helpers for harvest/distill pipelines

### Recommended split: extract `ContentKbCommandRunners`

Pull all `RunContent*Async` and `RunHarvestAsync` (plus their private helpers) into
`DeckFlow.CLI/ContentKbCommandRunners.cs`. The deck-domain runners stay in
`CommandRunners.cs`. New `RunSynthesizePhilosophyAsync` goes into `ContentKbCommandRunners`.

`DeckFlow.CLI/Program.cs` registers commands from both runner classes. No DI needed (these are
static runners). The split reduces `CommandRunners.cs` to ~800 lines (deck domain) and isolates
the 1,100-line content KB pipeline into its own file.

**Regression risk:** Very low. These are static methods; refactoring is cut-and-paste. The only
coupling is `ResolveContentKbDatabasePath` / `ResolveContentKbArtifactRoot` helpers which move
to `ContentKbCommandRunners`. `Program.cs` command registrations update to reference the new
class.

---

## 5. Component Map: New vs. Modified

### Modified components (in-place changes)

| Component | File | What Changes |
|-----------|------|--------------|
| `ContentKbRelevanceService` | `DeckFlow.Web/Services/ContentKbRelevanceService.cs` | `SelectTopClips`: add per-video cap. `ScoreArtifact`: reweight scorer (commander bonus multiplier, content-text topical filter). Update calibration constants. Tests update alongside. |
| `DeckAnalysisPacketService` | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | Add optional `ICreatorPhilosophyProfileService?` dependency (Phase 3 only). Pass `CreatorPhilosophyContext?` into `BuildAnalysisPrompt`. Update `DeckAnalysisPacketResult` record to carry `CreatorPhilosophyContext?`. |
| `AnalysisPromptVariant` (all 3) | `DeckFlow.Web/Services/PromptBuilders/Analysis/` | Add philosophy-context injection in the `## Expert Context` block (Phase 3 only). |
| `DeckController` | `DeckFlow.Web/Controllers/DeckController.cs` | Removed in Phase 4 (replaced by 3 new controllers). Existing action methods migrate verbatim — no logic change. |
| `CommandRunners` | `DeckFlow.CLI/CommandRunners.cs` | Content KB runners extracted to `ContentKbCommandRunners.cs`. Deck-domain runners stay. `Program.cs` command registrations updated. |
| `DeckFlow.CLI/Program.cs` | `DeckFlow.CLI/Program.cs` | Register `synthesize-philosophy` command (Phase 3). Update command-runner class references (Phase 4 split). |

### New components

| Component | File | Purpose |
|-----------|------|---------|
| `ICreatorPhilosophyProfileStore` | `DeckFlow.Core/Content/ICreatorPhilosophyProfileStore.cs` | CRUD over `creator_philosophy_profiles` table. |
| `CreatorPhilosophyProfileStore` | `DeckFlow.Core/Content/CreatorPhilosophyProfileStore.cs` | SQLite+Postgres implementation via `IRelationalDialect`. `EnsureSchemaAsync` + upsert + fetch by source slug. |
| `ICreatorPhilosophyProfileService` | `DeckFlow.Web/Services/CreatorPhilosophyProfileService.cs` | Resolves profile for a set of source slugs; scores and returns relevant principles for the deck's archetypes. Flag-gated via `content.kb.profiles.enabled`. |
| `CreatorPhilosophyContext` | `DeckFlow.Web/Models/` or inline in service file | `sealed record` carrying `IReadOnlyList<PhilosophyPrinciple>`. |
| `PhilosophyPrinciple` | same file | `sealed record(string SourceSlug, string Principle, string SourceVideoId, DateTimeOffset SourceDate)`. |
| `ContentKbCommandRunners` | `DeckFlow.CLI/ContentKbCommandRunners.cs` | All content KB CLI runners extracted from `CommandRunners.cs`. Includes `RunSynthesizePhilosophyAsync`. |
| `DeckToolsController` | `DeckFlow.Web/Controllers/DeckToolsController.cs` | Sync, Convert, Lookup, Mechanic, SuggestCategories, JudgeQuestions actions. |
| `DeckPacketController` | `DeckFlow.Web/Controllers/DeckPacketController.cs` | Analysis, Comparison, CedhMetaGap packet workflows. |
| `DeckPrimerController` | `DeckFlow.Web/Controllers/DeckPrimerController.cs` | Primer workflow. |

---

## 6. Data Flow Diagrams

### Phase 1: Fixed retrieval path (GetRelevantClipsAsync)

```
DeckAnalysisPacketService.BuildAsync()
  └─ IContentKbRelevanceService.GetMergedClipsAsync()
       └─ IContentSiteIndexStore.GetPublishedRowsAsync()   [SQLite or Postgres]
       └─ ParseRowsAsync()                                  [reads artifact .md files]
            └─ ScoreArtifact() [MODIFIED: topical-fit scorer]
       └─ SelectTopClips() [MODIFIED: per-video cap]
       └─ budget trim
  → IReadOnlyList<ContentKbExcerpt>?
       → DeckAnalysisPacketResult.ExpertContextClips
       → AnalysisPromptVariant.BuildPrompt() → ## Expert Context block
```

### Phase 3: Philosophy-profile injection path (conditional)

```
DeckAnalysisPacketService.BuildAsync()
  └─ [existing] IContentKbRelevanceService → ExpertContextClips
  └─ [NEW] ICreatorPhilosophyProfileService?.GetProfileContextAsync(
               sourceSlugSet: clips.Select(c => c.Source).Distinct(),
               deckArchetypes)
       └─ ICreatorPhilosophyProfileStore.GetProfileAsync(slug)   [DB read]
       └─ Filter/rank principles by archetype overlap + recency
       → CreatorPhilosophyContext (principles with provenance)
  → DeckAnalysisPacketResult.PhilosophyContext  [new field]
       → AnalysisPromptVariant.BuildPrompt()
            → ## Expert Context → "### Creator Heuristics" sub-section
```

### Phase 3: Offline synthesis path (CLI)

```
dotnet DeckFlow.CLI synthesize-philosophy --source <slug>
  └─ ContentKbCommandRunners.RunSynthesizePhilosophyAsync()
       └─ IContentSiteIndexStore.GetPublishedRowsAsync() [filter by source]
       └─ reads artifact .md files for that source
       └─ ILlmDistillationService.SynthesizePhilosophyAsync(transcripts)
            → LLM call → structured principles JSON + summary
       └─ writes profile artifact to artifacts/content-kb-profiles/<slug>.md
       └─ ICreatorPhilosophyProfileStore.UpsertProfileAsync(...)
```

---

## 7. Dependency-Ordered Build Sequence

```
Phase 1: Retrieval fix (no new components)
  ├─ Modify SelectTopClips — per-video cap
  ├─ Modify ScoreArtifact — topical-fit scorer
  └─ Update ContentKbRelevanceServiceTests calibration tests

Phase 2: Re-validation gate (test-only)
  ├─ Re-run Spike001KbValueAbHarness against fixed retriever
  └─ Gate decision: proceed to Phase 3 or route to retire/pivot

Phase 3: Creator Philosophy-Profile [CONDITIONAL on Phase 2 gate]
  ├─ New: ICreatorPhilosophyProfileStore + CreatorPhilosophyProfileStore (Core)
  │    └─ EnsureSchemaAsync wired in Program.cs startup alongside other Content* stores
  ├─ New: ICreatorPhilosophyProfileService + CreatorPhilosophyProfileService (Web)
  │    └─ Optional DI injection (nullable, same pattern as IContentKbRelevanceService?)
  ├─ New: CreatorPhilosophyContext + PhilosophyPrinciple records
  ├─ Modify: DeckAnalysisPacketService — add optional profile service, pass context to prompt
  ├─ Modify: AnalysisPromptVariant (all 3) — inject philosophy sub-section
  ├─ New: CLI synthesize-philosophy command + ContentKbCommandRunners.RunSynthesizePhilosophyAsync
  └─ Modify: DeckFlow.CLI/Program.cs — register command

  [Also in Phase 3 window if gate clears]
  ├─ Flip content.kb.enabled ON
  └─ SEL-02 expert-pin live re-confirm

Phase 4: SRP split (independent of Phase 2 gate)
  ├─ Extract ContentKbCommandRunners from CommandRunners (CLI)
  ├─ New: DeckToolsController, DeckPacketController, DeckPrimerController
  ├─ Delete: DeckController (or retain as empty stub forwarding to new controllers)
  └─ Update controller test class references
```

**Why this ordering:**
- Phase 1 is a prerequisite for Phase 2 (you need the fixed retriever to re-validate).
- Phase 3 is gated on Phase 2 outcome — it must not start before the gate is evaluated.
- Phase 4 is fully independent of Phases 1-3 (no shared code paths). It runs last to minimize blast radius during the KB work.
- `ICreatorPhilosophyProfileStore` (Core) must be built before `ICreatorPhilosophyProfileService` (Web) because the service depends on the store.
- `CreatorPhilosophyContext` record must exist before `DeckAnalysisPacketService` changes compile.

---

## 8. Architectural Constraints to Respect

| Constraint | Where it applies in v1.6 |
|------------|--------------------------|
| No `new HttpClient()` in services | `ICreatorPhilosophyProfileService` has no HTTP; CLI synthesis calls through `ILlmDistillationService` which already has the HTTP seam. |
| `IRelationalDialect` for SQLite/Postgres SQL differences | `CreatorPhilosophyProfileStore.EnsureSchemaAsync` must branch on dialect for `BOOLEAN` vs `INTEGER`, `RETURNING` vs `SELECT last_insert_rowid()`. Follow `ContentSiteIndexStore` line 66-71 pattern exactly. |
| Internal test ctor seam | Any new service that reads from disk (e.g. profile artifact reader) must expose an `internal Func<string, CancellationToken, Task<string>>? readArtifactAsync` override, same as `ContentKbRelevanceService`. |
| `content.kb.enabled` flag gate | `ICreatorPhilosophyProfileService` must check `IFeatureFlagCache.IsEnabled("content.kb.enabled")` (or a new subordinate flag `content.kb.profiles.enabled`) before doing any work. Return null when disabled — consistent with `GetRelevantClipsAsync` returning null. |
| No DeckController-level logic changes | The SRP split is mechanical extraction only. No behavior changes, no new routes, no new error handling patterns. Copy-paste the action bodies verbatim. |
| `sealed class` on leaf types, `sealed record` for DTOs | `CreatorPhilosophyContext`, `PhilosophyPrinciple`, `CreatorPhilosophyProfileStore` follow existing conventions. |
| Prompt variant content is intentionally decoupled | The philosophy sub-section prose will be duplicated across ChatGPT/Claude/Gemini variants. Do not extract shared guidance. |
| Artifact file naming under MTG_DATA_DIR | Profile artifacts go under `artifacts/content-kb-profiles/<slug>.md`. Resolve via `ContentKbArtifactPathResolver` (or a new `ContentKbProfileArtifactPathResolver` following the same class shape). |

---

## 9. Test Seam Inventory

| Component | Existing seam | v1.6 change needed |
|-----------|---------------|--------------------|
| `ContentKbRelevanceService` | `internal` ctor with `readArtifactAsync`, `resolveArtifactPath` | No seam change. Scoring logic tests (`ScoreArtifact` is `internal static`) update calibration constants and add per-video-cap assertions. |
| `DeckAnalysisPacketService` | `internal` ctor with 3 `Func<RestRequest,...>` overrides | Add `ICreatorPhilosophyProfileService? philosophyProfileService` param to existing internal ctor (Phase 3 only). Tests pass `null` to opt out. |
| `CreatorPhilosophyProfileService` (new) | Needs: `internal` ctor accepting `Func<string, Task<string>>? readProfileArtifactAsync` | Mirrors `ContentKbRelevanceService` pattern. |
| `CreatorPhilosophyProfileStore` (new) | `RelationalDatabaseConnection` dependency → use `FakeRelationalDatabaseConnection` or in-memory SQLite (existing pattern in `FeedbackStoreTests`) | No new test double needed if in-memory SQLite already works. |
| Controller split (Phase 4) | Tests reference `DeckController` type | Update test class `ILogger<DeckController>` → `ILogger<DeckToolsController>` etc. Behavior unchanged. |
