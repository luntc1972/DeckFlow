# Phase 96: Stated-Rules Distiller - Research

**Researched:** 2026-07-12
**Domain:** Multi-pass LLM claim extraction (Claimify) over MTG creator transcripts, extending the existing DeckFlow.Core Content-KB distill pipeline
**Confidence:** MEDIUM — the extension points (schemas/validation/writer/orchestrator/store) are HIGH confidence (read directly from shipped code); the Claimify method itself is MEDIUM (verified against the official Microsoft Research blog, not the raw arXiv PDF); the exact metric-allowlist join and content_type heuristic are the phase's own novel design and are presented as reasoned recommendations, not verified facts.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 (Claimify pipeline shape):** Literal multi-pass Claimify — 3 sub-calls per chunk (Select, Disambiguate, Decompose) + a reduce/dedupe pass across chunks. Chosen over a single-stage prompt for fidelity/falsifiability despite ~3x token cost.
- **D-01a:** Irreducibly ambiguous statements are DROPPED at Disambiguate/Decompose — a rule that cannot be made atomic + measurable does not ship.
- **D-02 (schema shape, locked now):** `{category, metric, value|band, comparator, condition, clip_ts}` plus `sourceClip`, `confidence`, `video_date`. `value` supports a band (min/max) so "37-42 lands" is ONE rule with `comparator: range`. `condition` carries per-archetype/per-curve conditionality (the P97 `applies_when` seed).
- **D-02a (metric allowlist, deferred to planner — THIS document supplies it):** The `metric` controlled vocabulary MUST be derived from the Phase 95 `MeasuredMetric` KEYS union the ~27 Snail prototype rules, so P97 fusion can join stated↔measured on the same key. This is a hard planner requirement.
- **D-03 (schema enforcement):** Every rule validates against a strict JSON schema via constrained decoding, extending `DistillationSchemas` (new `StatedRulesSchema` + system prompt) and `DistillationValidation` (new `ValidateStatedRules`/`SanitizeStatedRules`), mirroring the existing summary/clips/tags stages.
- **D-04 (recency/provenance):** Carry the source video's publish date on each rule; do NOT resolve superseding here — that is Phase 97's job. Honors the substrate-only boundary.
- **D-05a (content_type heuristic):** Derive `content_type ∈ {deckbuilding-theory, deck-tech, meta-commentary, gameplay}` from EXISTING signals (tags + keep/drop classifier verdict + clip density) — NO new LLM call.
- **D-05 (backfill scope):** Ship the pipeline re-distill-capable; run NO mass backfill this phase. New distills emit `stated_rules`/`content_type` going forward. Executing the sweep across ~106 existing artifacts is operator-driven, deferred.
- **D-05-DEP (downstream flag):** Because no backfill runs here, P97 fusion will have NO stated_rules input until a Snail re-distill is executed. P97 planning must run or gate on this.
- **D-07 (card grounding):** Fuzzy-correct then flag. Run a minimal Scryfall `fuzzy` lookup on any card name inside a distilled rule; confident single match → rewrite to canonical name + `card_grounded=true`; still unresolved → keep the rule, flag `card_grounded=false` (hard reject is Phase 98's job).
- **D-06 (golden regression):** Golden test runs the full multi-pass pipeline over a real Salubrious Snail transcript fixture and asserts emitted `stated_rules` validate against the new schema, using the existing UTF-8-safe harness (the `CliLlmDistillationService` CP437 lesson — mandatory, non-negotiable).

### Claude's Discretion

- Chunk size / map-reduce chunk boundaries for the hierarchical chunking (D-01).
- Exact dedupe key/threshold in the reduce pass (D-01) — likely `metric`+`condition`.
- Precise heuristic thresholds for `content_type` classification (D-05a).
- Concrete confidence scale/encoding for `confidence` (D-03).
- Exact `card_grounded` flag representation in the YAML block (D-07).

### Deferred Ideas (OUT OF SCOPE)

- Mass re-distill backfill of the ~106 existing artifacts — mechanism ships, execution deferred to an operator-run sweep (D-05). P97 needs at minimum a Snail re-distill first (D-05-DEP).
- Superseding / newer-wins conflict resolution across same-metric rules — explicitly Phase 97 (fusion), rejected here as scope creep (D-04).
- Multi-creator onboarding of stated rules beyond Snail — manual/deferred, mirroring the P95 creator-profile-source manual mapping.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CS-11 | Map-reduce hierarchical chunking over creator transcripts | See "Map-Reduce Chunking" below — no in-repo chunking precedent exists; recommendation is reasoned from `MaxTranscriptInputTokens`/`EstimateTokenCount` and transcript `[mm:ss]` markers already used by `ClipsSystemPrompt`. |
| CS-11a | `stated_rules:` YAML block + retrofit via one re-distill pass | See "ContentArtifactWriter Extension" — mechanism-only per D-05; concrete YAML/JSON-flow-mapping serialization pattern given, mirroring `SerializeTags`. |
| CS-11b | `content_type:` frontmatter, 4-value enum | See "content_type Heuristic" — concrete signal-to-bucket mapping derived from `ClassificationResult`/`TagsResult`/`ClipsResult` shapes already emitted by the pipeline. |
| CS-11c | Rule provenance/recency (video date) | See "Recency/Provenance" — `ContentVideo.PublishedUtc` is the existing source; already read in `ContentKbOrchestrator.DistillVideoAsync`. |
| CS-12 | Claimify-style Select→Disambiguate→Decompose; drop irreducibly ambiguous | See "Claimify Method" — stage-by-stage input/output contract verified against Microsoft Research's own description. |
| CS-13 | Strict JSON schema via constrained decoding | See "Constrained Decoding Mechanism" — the exact enforcement mechanism (JSON-Schema-in-prompt for the CLI path, `ChatResponseFormat.CreateJsonSchemaFormat` for OpenAI) is read directly from `CliLlmDistillationService`/`LlmDistillationService`. |
| CS-14 | Rule carries `sourceClip` + `confidence` | See "Schema Extension Points" — `DistillationSchemas`/`DistillationValidation` extension shape, `ClipItem` reuse. |
| CS-15 | UTF-8 harness reuse + golden regression + minimal Scryfall grounding | See "Golden Test Pattern" and "Card Grounding Seam" — exact harness mechanics (`BuildStartInfo`, `ExtractWithRetryAsync`, process-runner override test seam) and the exact Scryfall fuzzy contract (`cards/named?fuzzy=` via `IScryfallCardResolver`, Web-hosted, `internal` `ScryfallThrottle` blocks Core access). |
</phase_requirements>

## Summary

Phase 96 is a pure extension of an already-mature, well-tested distillation pipeline (`DistillationSchemas` → `DistillationValidation` → `ContentKbOrchestrator.DistillVideoAsync` → `ContentArtifactWriter.ToText` → `IContentVideoStore`/`IContentSiteIndexStore`). The existing pipeline runs exactly 3 constrained-decoding LLM calls per video (summary, clips, tags) plus a classification gate; Phase 96 adds a 4th "dimension" — stated rules — but that dimension itself fans out into `3 × chunks + 1` calls per video (Select/Disambiguate/Decompose per chunk, then one reduce), so this is meaningfully more expensive than any existing stage, consistent with the CONTEXT.md cost note.

The **Claimify method itself has no native cross-sentence merge step** — verified directly against Microsoft Research's own description of the pipeline (Selection → Disambiguation → Decomposition operate per-sentence with a local context window, and stop at decomposition). The **reduce/dedupe pass is a DeckFlow-specific addition** required because CS-11's map-reduce chunking runs Claimify independently per chunk; this is not part of literal Claimify and should be documented as such in the plan rather than attributed to the paper.

A significant, previously-undocumented finding: the **Phase 94 `StatedRule` record (`{Category, TargetMetric, TargetValue: double, Comparator, SourceClip, Confidence}`) cannot represent D-02's locked shape** — it has no band (min/max), no `condition`, no `clip_ts`, and no `video_date`. Re-reading Phase 96's own CONTEXT.md canonical_refs confirms this phase does **not** call `ICreatorStyleProfileStore`/`CreatorStyleProfileStore.UpsertAsync` at all (only Phase 95's `MeasuredStyleProfileBuilder` does that today) — Phase 96 emits a **per-video** YAML block + (recommended) DB rows, and it is Phase 97's job to aggregate across a creator's whole video corpus and populate `CreatorStyleProfile.StatedRules`. This means Phase 96 is free to define its **own** DTO for the distilled rule (with band support) rather than being constrained by the `double`-only P94 shape; the double-vs-band reconciliation is deferred to Phase 97's translation step. This should be called out explicitly to the planner and flagged in Phase 97's eventual research/context as a translation task.

A second load-bearing finding: `IContentVideoStore` currently persists three "distill dimensions" as dialect-guarded child tables (`content_summaries`, `content_clips`, `content_tags`), each cleared by `ClearDistillOutputAsync` before a re-distill. **Stated rules should almost certainly get a fourth table (`content_stated_rules`) following the identical pattern**, or P97 will have no queryable per-video rule source other than re-parsing committed markdown — a much more fragile design that breaks the established "structured DB row backs every rendered artifact section" invariant. This was not explicit in CONTEXT.md's canonical refs and should be flagged to the planner as a likely-required (not merely optional) extension.

**Primary recommendation:** Add a new `StatedRulesSchema`/system prompt set to `DistillationSchemas.cs` for 4 new LLM operations (Select, Disambiguate, Decompose, Reduce) gated to the subscription/CLI provider only (mirroring `ClassifyAsync`'s existing default-`NotSupportedException` pattern on `ILlmDistillationService`); introduce a new pure-Core namespace `DeckFlow.Core.Knowledge.StatedRulesExtraction` (mirroring the already-shipped `MeasuredStyleExtraction` namespace) holding the chunking, dedupe-key, and per-rule validation logic; add a `content_stated_rules` child table to `IContentVideoStore` mirroring `content_clips`/`content_tags`; extend `ContentArtifactMetadata`/`ContentArtifactWriter.ToText` with `ContentType` and `StatedRules` (both additive, single production call site, low blast radius); and implement card grounding via a narrow Core-facing interface (e.g. `ICardNameGrounder`) whose only Web-hosted implementation wraps the already-existing `IScryfallCardResolver` fuzzy-lookup call — this exactly mirrors the Phase 95 D-11 Core/Web layering precedent.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Transcript chunking + Select/Disambiguate/Decompose prompt construction | Core (`DeckFlow.Core.Knowledge`) | — | Pure prompt/schema construction, same tier as existing `DistillationSchemas`. |
| LLM call execution (CLI subprocess) | Core (`DeckFlow.Core.Integration`) | — | `CliLlmDistillationService` already lives in Core and owns the UTF-8 process harness; no HTTP framework dependency. |
| Reduce/dedupe across chunks | Core (`DeckFlow.Core.Knowledge`) | — | Pure data transform over in-memory rule candidates; no I/O. |
| Rule schema validation/sanitization | Core (`DeckFlow.Core.Knowledge`) | — | Mirrors `DistillationValidation`'s existing `internal static` surface. |
| Card-name fuzzy grounding (Scryfall) | **Web** (`DeckFlow.Web.Services.Scryfall`) | Core (narrow contract) | `IScryfallCardResolver` + `ScryfallThrottle` (internal, Web-only) + Polly `"scryfall"` pipeline are Web-hosted; Core cannot reach `internal static ScryfallThrottle`. Mirrors Phase 95 D-11. |
| Artifact rendering (`stated_rules:`/`content_type:` frontmatter) | Core (`DeckFlow.Core.Knowledge`) | — | `ContentArtifactWriter`/`ContentArtifactSpec` are Core, single call site in `ContentKbOrchestrator`. |
| Per-video structured persistence (new `content_stated_rules` table) | Core (`DeckFlow.Core.Content`) | — | Mirrors `IContentVideoStore`'s existing `content_clips`/`content_tags` dialect-guarded tables. |
| Orchestration sequencing (new stage after tags) | Core (`DeckFlow.Core.Orchestration`) | — | `ContentKbOrchestrator.DistillVideoAsync` is the single call site for all 3 existing stages; the new stage(s) slot in the same method. |
| `content_type` heuristic computation | Core (`DeckFlow.Core.Knowledge`) | — | Pure function over already-computed `ClassificationResult`/`TagsResult`/`ClipsResult` — no new I/O, no new LLM call (D-05a). |

## Standard Stack

No new external packages are required for this phase. All work extends already-referenced libraries:

| Library | Version (installed) | Purpose | Why Standard (this repo) |
|---------|---------|---------|--------------|
| `System.Text.Json` (BCL) | net10.0 | JSON payload (de)serialization for CLI/OpenAI extraction, YAML-frontmatter-embedded JSON flow mappings | Already the sole JSON library across `DistillationValidation`, `ContentArtifactSpec.SerializeTags` `[VERIFIED: repo]` |
| `OpenAI` SDK | per existing `LlmDistillationService.cs` | `ChatResponseFormat.CreateJsonSchemaFormat` structured output | Already wired for the metered provider path `[VERIFIED: repo]` |
| RestSharp 114.0.0 | per `CLAUDE.md` | Scryfall fuzzy lookup HTTP call (Web-hosted grounding seam) | Pinned project-wide HTTP abstraction `[CITED: CLAUDE.md]` |
| Polly 8.x | per `CLAUDE.md` | `"scryfall"` named resilience pipeline reused by the grounding call | Pinned pattern; do NOT build a new pipeline `[CITED: CLAUDE.md]` |
| xUnit 2.9.3 | per `CLAUDE.md`/`.csproj` | New tests for schema/validation/writer/store extension + golden regression | Matches `.NET Core` project test-framework convention `[CITED: CLAUDE.md]` |

**Version verification:** Not applicable — no new package references. If the planner determines a new package is genuinely needed (e.g., a dedicated YAML serializer instead of hand-rendering JSON-flow-mapping into the frontmatter, as the current code does for tags), that requires explicit user approval per `CLAUDE.md` "Dependency additions" and should be flagged as a plan-time question, not assumed.

## Package Legitimacy Audit

Not applicable — this phase installs no new external packages. All extension points reuse already-vetted, already-installed libraries (`System.Text.Json`, `OpenAI`, RestSharp, Polly, xUnit). If a plan later proposes a new package (e.g., a YAML library), the Package Legitimacy Gate must be run at that time and the package tagged `[ASSUMED]` pending `slopcheck` + registry verification, per this phase's package-legitimacy protocol.

## Architecture Patterns

### System Architecture Diagram

```
Transcript (already harvested, already gated by ClassifyAsync == "keep")
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│ NEW: StatedRulesExtraction (Core, pure)                      │
│                                                               │
│  1. Chunk(transcript) -> chunk[] (map-reduce, CS-11)          │
│         │                                                     │
│         ▼  (per chunk, 3 LLM calls)                          │
│  2. SelectAsync(chunk)        -> candidate sentences          │
│  3. DisambiguateAsync(cands)  -> resolved sentences            │
│                                   (irreducibly-ambiguous DROPPED)│
│  4. DecomposeAsync(resolved)  -> atomic rule candidates[]      │
│         │                                                     │
│         ▼ (across all chunks, 1 LLM call)                     │
│  5. ReduceAsync(all candidates) -> deduped StatedRuleCandidate[]│
│         │                                                     │
│         ▼ (pure, no LLM)                                     │
│  6. ValidateStatedRules / SanitizeStatedRules                  │
│         │                                                     │
│         ▼ (Web-hosted seam, D-07)                             │
│  7. ICardNameGrounder.TryGroundAsync(cardName)                 │
│       -> Web: IScryfallCardResolver fuzzy lookup                │
│       -> rewrite canonical name + card_grounded=true            │
│       -> OR keep + card_grounded=false                          │
└─────────────────────────────────────────────────────────────┘
      │
      ▼
ContentKbOrchestrator.DistillVideoAsync (existing, extended)
      │  ┌─ content_type heuristic (D-05a, pure, no LLM) ──────┐
      │  │  inputs: ClassificationResult + TagsResult +         │
      │  │          ClipsResult (already computed this call)   │
      │  └──────────────────────────────────────────────────────┘
      ▼
ContentArtifactWriter.ToText (existing, extended)
   -> stated_rules: [...] + content_type: "..." in frontmatter
      │
      ▼
IContentVideoStore (existing, extended: new content_stated_rules table)
      │
      ▼
Committed .md artifact (git) + SQLite/Postgres row
      │
      ▼ (Phase 97, OUT OF SCOPE this phase)
Fusion: aggregate stated rules across a creator's whole corpus,
translate band-shaped rules into P94 StatedRule[] entries
```

### Recommended Project Structure

```
DeckFlow.Core/
├── Knowledge/
│   ├── DistillationSchemas.cs          # ADD: StatedRulesSchema + 4 system prompts
│   ├── DistillationValidation.cs       # ADD: ValidateStatedRules/SanitizeStatedRules + payload records
│   ├── ContentArtifactWriter.cs        # EXTEND: ToText emits stated_rules:/content_type:
│   ├── ContentArtifactSpec.cs          # EXTEND: ArtifactFileFormat doc fixture, ContentArtifactMetadata
│   └── StatedRulesExtraction/          # NEW namespace, mirrors MeasuredStyleExtraction/
│       ├── TranscriptChunker.cs        # NEW: CS-11 map-reduce chunk boundaries
│       ├── StatedRuleCandidate.cs      # NEW: Phase-96-owned DTO (band-capable; distinct from P94 StatedRule)
│       ├── StatedRuleReducer.cs        # NEW: cross-chunk dedupe (D-01 reduce pass)
│       ├── ContentTypeHeuristic.cs     # NEW: D-05a pure classifier
│       └── ICardNameGrounder.cs        # NEW: narrow Core-facing grounding contract (D-07 seam)
├── Content/
│   ├── IContentVideoStore.cs           # EXTEND: InsertStatedRuleAsync, ClearDistillOutputAsync clears it too
│   └── ContentVideoStore.cs            # EXTEND: content_stated_rules table, both dialects
├── Integration/
│   ├── ILlmDistillationService.cs      # EXTEND: SelectAsync/DisambiguateAsync/DecomposeAsync/ReduceStatedRulesAsync
│   │                                    #         (default NotSupportedException, mirrors ClassifyAsync)
│   └── CliLlmDistillationService.cs    # IMPLEMENT: the 4 new methods (mandatory per D-06)
└── Orchestration/
    └── ContentKbOrchestrator.cs        # EXTEND: DistillVideoAsync gains the new stage(s)

DeckFlow.Web/
└── Services/Scryfall/
    └── ScryfallCardNameGrounder.cs     # NEW: implements ICardNameGrounder, wraps IScryfallCardResolver

DeckFlow.Core.Tests/
├── DistillationPromptRegressionTests.cs      # EXTEND: byte-exact new prompt/schema assertions
├── StatedRulesExtraction/                    # NEW test folder mirroring source
│   ├── TranscriptChunkerTests.cs
│   ├── StatedRuleReducerTests.cs
│   └── ContentTypeHeuristicTests.cs
├── ContentArtifactWriterTests.cs             # EXTEND: new frontmatter fields
├── ContentVideoStoreDistillTests.cs          # EXTEND: content_stated_rules round-trip, clear-on-redistill
└── CliLlmDistillationServiceTests.cs         # EXTEND: multi-call sequencing + D-06 golden fixture
```

### Pattern 1: Constrained-Decoding Extension (D-03, D-13)

**What:** Every existing distill dimension follows: a `const string XSchema` (strict `additionalProperties:false` JSON Schema) + a `static string XSystemPrompt` in `DistillationSchemas.cs`, an `internal sealed record XPayload` + `ValidateX`/`SanitizeX` in `DistillationValidation.cs`, and one method per dimension on `ILlmDistillationService` implemented identically by both `CliLlmDistillationService` (CLI/JSON-schema-in-prompt) and `LlmDistillationService` (OpenAI/`ChatResponseFormat.CreateJsonSchemaFormat`).

**When to use:** Any new distill dimension, including the 4 new Claimify stages.

**Exact mechanism (CS-13), verified from source, not assumed:**
- **CLI path (`CliLlmDistillationService`):** NOT a provider-native structured-output feature. The system prompt + JSON schema are concatenated into a single instruction string (`BuildInstruction`), the schema is enforced **post-hoc**: the CLI's raw stdout is parsed as a Claude JSON envelope (`{"result": "...", "is_error": false}`), fenced markdown is stripped (`FenceStrip`), a **balanced-brace scanner** (`ExtractBalancedJsonObject`) extracts the first complete `{...}` object even if the model wrapped it in prose, and the result is deserialized with `System.Text.Json` using snake_case property mapping. On any parse/deserialize failure, the whole call is retried up to `MaxRetries = 3` times before throwing. There is no schema-conformance check beyond "does it deserialize" — the schema text in the prompt is advisory to the model, not enforced by tooling.
- **OpenAI path (`LlmDistillationService`):** Uses the SDK's native **`ChatResponseFormat.CreateJsonSchemaFormat(jsonSchemaFormatName, jsonSchema, jsonSchemaIsStrict: true)`** — this IS a provider-enforced structured-output feature (OpenAI rejects/refuses non-conforming output rather than the caller post-hoc-parsing it). `Temperature = 0f` is set. No retry loop exists on this path; a refusal (`completion.Refusal`) or truncation (`FinishReason == Length`) throws immediately.
- **Both paths still call the shared `internal static Validate*/Sanitize*` in `DistillationValidation.cs`** after deserialization — this is the actual business-rule enforcement layer (word counts, clip count bounds, allowlist membership), independent of which structural mechanism produced the JSON.

**Example (existing, to mirror exactly for the new stages):**
```csharp
// Source: DeckFlow.Core/Integration/CliLlmDistillationService.cs:85-99 (ExtractClipsAsync)
public async Task<ClipsResult> ExtractClipsAsync(
    string transcript,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

    var payload = await ExtractWithRetryAsync<ClipsPayload>(
        BuildInstruction(DistillationSchemas.ClipsSystemPrompt, DistillationSchemas.ClipsSchema),
        transcript,
        cancellationToken).ConfigureAwait(false);

    return new ClipsResult(
        DistillationValidation.SanitizeClips(payload.Clips),
        new TokenUsage(0, 0));
}
```

### Pattern 2: Subscription-Only Capability Gate (precedent for Claimify stages)

**What:** `ILlmDistillationService.ClassifyAsync` has a **default interface method** body that throws `NotSupportedException("Classifier requires the subscription LLM CLI provider.")`. `LlmDistillationService` (OpenAI) does not override it; only `CliLlmDistillationService` implements it. `ContentKbOrchestrator.DistillAsync` explicitly refuses to run a live (non-dry-run) distill unless `isSubscriptionProvider` is true, specifically because the classifier is CLI-only.

**When to use:** The 4 new Claimify-stage methods (Select/Disambiguate/Decompose/Reduce) are prime candidates for the SAME gate, given the token-cost concern the CONTEXT.md "Cost note" explicitly raises (3 calls/chunk × N chunks + 1 reduce, on top of the existing 3 calls). Recommend the planner make this an explicit decision point rather than silently allowing the metered OpenAI path to attempt it.

**Example:**
```csharp
// Source: DeckFlow.Core/Integration/ILlmDistillationService.cs:24-26
Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
    => Task.FromException<ClassificationResult>(
        new NotSupportedException("Classifier requires the subscription LLM CLI provider."));
```

### Pattern 3: Core/Web Layering Seam (D-07, mirrors Phase 95 D-11)

**What:** Pure logic (chunking, reduce/dedupe, schema validation) lives in `DeckFlow.Core` with zero HTTP/AspNet dependency. Anything that must call an HTTP-touching, Web-hosted service (Scryfall, in this case) is injected through a **narrow interface** that Core defines and Web implements — Core never references `IHttpClientFactory`, RestSharp's Web-side named-client wiring, or the `internal` `ScryfallThrottle`.

**Verified constraint (not assumed):** `ScryfallThrottle` is declared `internal static class ScryfallThrottle` in namespace `DeckFlow.Web.Services` (`DeckFlow.Web/Services/Scryfall/ScryfallThrottle.cs:5,11`). It is NOT visible to `DeckFlow.Core` — there is no `InternalsVisibleTo` grant from `DeckFlow.Web` to `DeckFlow.Core` (only `DeckFlow.Web.Tests` is granted internals visibility into `DeckFlow.Web`, per `DeckFlow.Web/AssemblyInfo.cs`). This makes it **structurally impossible** for card-grounding HTTP logic to live in Core directly, unlike `ArchidektApiDeckImporter` (which is Core-hosted but self-manages its own `RestClient` + legacy `AsyncRetryPolicy`, bypassing the Web-side throttle/pipeline infrastructure entirely — that pattern is NOT appropriate to copy for Scryfall, since Scryfall calls MUST go through the shared throttle to respect the ~5 req/s global pacing invariant).

**Exact fuzzy-lookup contract, verified from source:**
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:195-202
var namedRequest = new RestRequest("cards/named", Method.Get);
namedRequest.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);
if ((int)namedResponse.StatusCode >= 200 && (int)namedResponse.StatusCode < 300 && namedResponse.Data is not null)
{
    return namedResponse.Data; // single confident match
}
return null; // 404 (not_found or ambiguous, per Scryfall's own /cards/named contract) -> unresolved
```
This is already wrapped by `ScryfallThrottle.ExecuteAsync` + the named Polly `"scryfall"` resilience pipeline inside `ScryfallCardResolver`'s constructor (`DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:75-92`). D-07's grounding call should reuse this exact code path (e.g., a thin new method on `IScryfallCardResolver` or direct reuse of `SearchPrintingFallbackCardAsync`'s named-fuzzy tail) rather than re-implementing the RestSharp/throttle wiring. `[VERIFIED: repo source]`

**Recommended seam shape:**
```csharp
// NEW, Core: DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs
public interface ICardNameGrounder
{
    Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default);
}

public sealed record CardGroundingResult(bool Resolved, string CanonicalName);

// NEW, Web: DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs
public sealed class ScryfallCardNameGrounder(IScryfallCardResolver resolver) : ICardNameGrounder
{
    public async Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken ct = default)
    {
        var card = await resolver.SearchPrintingFallbackCardAsync(candidateName, ct); // or a new fuzzy-only method
        return card is not null
            ? new CardGroundingResult(true, card.Name)
            : new CardGroundingResult(false, candidateName);
    }
}
```

### Pattern 4: Dialect-Guarded Child Table (recommended for stated rules persistence)

**What:** `IContentVideoStore` persists distill output as three child tables keyed by `video_id`, each with matching SQLite/Postgres DDL (`ContentVideoStore.cs:669-748`), each cleared in `ClearDistillOutputAsync` before re-distill (`ContentVideoStore.cs:604-611`).

**Recommended `content_stated_rules` DDL (mirrors `content_clips` exactly):**
```sql
-- Postgres
CREATE TABLE IF NOT EXISTS content_stated_rules (
  id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  video_id     BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
  category     TEXT NOT NULL,
  metric       TEXT NOT NULL,
  value_min    DOUBLE PRECISION NULL,
  value_max    DOUBLE PRECISION NULL,
  comparator   TEXT NOT NULL,
  condition    TEXT NULL,
  clip_ts      INT NULL,
  source_clip  TEXT NOT NULL,
  confidence   DOUBLE PRECISION NOT NULL,
  card_grounded BOOLEAN NULL,
  sort_order   INT NOT NULL DEFAULT 0
);
-- SQLite: INTEGER PRIMARY KEY AUTOINCREMENT in place of BIGINT GENERATED BY DEFAULT AS IDENTITY,
-- INTEGER in place of BIGINT/DOUBLE PRECISION/BOOLEAN, matching the existing content_clips split.
```
Then `ClearDistillOutputSql` gets a fourth `DELETE FROM content_stated_rules WHERE video_id = @videoId;` line.

**Why this matters (not optional busywork):** Without a structured per-video store, Phase 97 (fusion) has no queryable path to a creator's stated rules other than re-parsing every committed `.md` artifact's frontmatter YAML at fusion time — fragile, and inconsistent with how `summary`/`clips`/`tags` are already handled. This should be flagged to the planner as a likely-required task even though it was not named in CONTEXT.md's canonical_refs list.

### Anti-Patterns to Avoid

- **Attributing the reduce/dedupe pass to "Claimify" in docs/prompts.** Literal Claimify (per Microsoft Research) has no cross-sentence or cross-chunk merge step — it stops at Decomposition. The reduce pass is a DeckFlow-specific addition needed because of CS-11's map-reduce chunking. Mislabeling this in the plan or prompts risks confusing future maintainers about what the paper actually claims.
- **Building a new Scryfall RestClient/pipeline in Core**, copying `ArchidektApiDeckImporter`'s self-managed-`RestClient` pattern. That pattern exists for Archidekt specifically and bypasses the shared Scryfall throttle/Polly pipeline — reusing it for Scryfall would violate the "Calling Scryfall without `ScryfallThrottle`" anti-pattern already named in this repo's own architecture doc.
- **Assuming the P94 `StatedRule` record can hold the D-02 shape as-is.** It cannot (no band, no condition, no clip_ts, no video_date) — see Summary. Do not silently truncate a banded rule into `StatedRule.TargetValue` without a documented, deliberate translation rule (and ideally, defer that translation to Phase 97 where it belongs).
- **Running the multi-pass Claimify stages on the metered OpenAI provider without an explicit decision.** Given the ~3-4x call multiplier per video, treat this the same way `ClassifyAsync` already treats subscription-only gating, unless the planner deliberately decides otherwise.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON schema enforcement for LLM output | A custom JSON validator library | The existing `additionalProperties:false` schema-in-prompt (CLI) / `ChatResponseFormat.CreateJsonSchemaFormat` (OpenAI) + post-hoc `Validate*/Sanitize*` split already proven across 3 dimensions | Exact mechanism already exists and is tested (`DistillationPromptRegressionTests`); a new validator would duplicate `System.Text.Json` + add risk |
| CLI subprocess UTF-8 handling | A new process-launch helper | `CliLlmDistillationService.BuildStartInfo` (pins `StandardOutputEncoding`/`StandardErrorEncoding` to UTF-8) | This is the exact fix for a previously-shipped mojibake bug (CP437 misdecoding); D-06 explicitly mandates reusing it |
| Card name fuzzy matching | A local Levenshtein/fuzzy-match library against a cached card list | Scryfall's own `/cards/named?fuzzy=` endpoint via `IScryfallCardResolver` | Scryfall's fuzzy algorithm is the ecosystem-canonical source of truth for MTG card names and is already wrapped, throttled, and resilience-piped in this repo |
| YAML serialization | A new YAML library dependency (e.g., YamlDotNet) | Hand-rendered JSON-flow-mapping inside the `---`-delimited frontmatter block, exactly like `ContentArtifactSpec.SerializeTags` already does for `tags:` | JSON is a valid YAML subset; the existing artifact format already relies on this; adding a YAML library needs explicit user approval per `CLAUDE.md` and is very likely unnecessary here |
| Cross-chunk rule dedup | A generic string-similarity/clustering library | A deterministic dedupe key over `(metric, condition)` (or `(metric, condition, comparator)`), following the exact pattern of `TagsPayload`'s dedupe-by-canonical-value logic in `DistillationValidation.SanitizeTagDimension` | Deterministic, falsifiable, testable without any ML dependency; matches the codebase's existing preference for exact, allowlist-driven logic over fuzzy heuristics |

**Key insight:** Every "don't hand-roll" item above already has a working, tested analog somewhere in this exact codebase. The phase's job is disciplined extension of those analogs, not new infrastructure.

## Claimify Method (CS-12) — Verified Detail

Per Microsoft Research's own description of the Claimify pipeline (arXiv 2502.10855, "Towards Effective Extraction and Evaluation of Factual Claims"), verified via the official Microsoft Research blog post `[CITED: microsoft.com/en-us/research/blog/claimify-...]`:

| Stage | Input | Output | Notes |
|-------|-------|--------|-------|
| **Selection** | Individual sentences with configurable local context (surrounding sentences + structural metadata, e.g. headers) | Either the original sentence, a rewritten sentence containing only the verifiable components, or a "No verifiable claims" label | An LLM call identifies and strips unverifiable content (opinions, questions, instructions) |
| **Disambiguation** | Sentences that passed Selection, plus their context | Either a disambiguated sentence, or a "Cannot be disambiguated" label | Resolves pronouns/vague references/acronyms using ONLY local context; this is the stage Microsoft highlights as novel — it explicitly detects when ambiguity is unresolvable and drops rather than guesses |
| **Decomposition** | Unambiguous/disambiguated sentences, plus context | One or more standalone, decontextualized atomic claims, or "No verifiable claims" | Breaks a sentence into individual factual assertions while preserving necessary context |

**No native merge/reduce step exists in Claimify itself** — the method operates and terminates at the sentence level; claims are not aggregated or deduplicated across sentences or documents by the paper's own pipeline `[CITED: microsoft.com/en-us/research/blog/...]`. **This confirms D-01's "reduce" stage is a DeckFlow-specific addition**, not a documented part of Claimify, required because CS-11 chunks the transcript for map-reduce parallelism and independent per-chunk extraction will produce duplicate/near-duplicate rules across chunk boundaries (e.g., a land-count rule restated in two different segments of a long video).

**Mapping onto DeckFlow's existing per-dimension constrained-call pattern:** Each of Select/Disambiguate/Decompose becomes one more `ILlmDistillationService` method (one constrained JSON-schema call each), exactly like `SummarizeAsync`/`ExtractClipsAsync`/`InferTagsAsync` today — no new mechanism is needed beyond what already exists; only new schemas/prompts/payload records.

**Confidence:** MEDIUM. Verified against the official Microsoft Research blog's description (which is itself a secondary source summarizing the paper), not the raw arXiv PDF text. The stage names, input/output shapes, and "no merge step" conclusion are consistent across the blog and the search-result abstracts gathered, so this is presented as CITED rather than ASSUMED, but the planner should treat exact prompt wording/context-window sizing from the paper as out of scope — DeckFlow's system prompts should be written fresh (mirroring the tone/constraints of existing `DistillationSchemas` prompts) rather than transcribed from the paper.

## Map-Reduce Chunking (CS-11)

**No in-repo precedent exists for transcript chunking** — every existing distill call (`SummarizeAsync`, `ClassifyAsync`, `ExtractClipsAsync`, `InferTagsAsync`) sends the WHOLE transcript in one call, gated only by the overall `MaxTranscriptInputTokens = 120_000` cap (`DistillationValidation.cs:20`, enforced by `ValidateTranscriptLength`) and a `EstimateTokenCount` heuristic of `transcript.Length / 4`.

**Recommendation (Claude's Discretion per CONTEXT.md, presented as reasoned guidance, tagged `[ASSUMED]`):**
- Chunk on the transcript's existing `[mm:ss]` timestamp markers (already relied upon by `ClipsSystemPrompt`) rather than raw character counts, so chunk boundaries land on natural speech breaks.
- Target chunk size ~2,000-4,000 words (roughly 8,000-16,000 characters, i.e. ~2,000-4,000 estimated tokens via the existing `EstimateTokenCount` heuristic) — small enough that 3 calls/chunk stay well inside typical context windows and keep the per-call cost predictable, large enough that a typical 15-30 minute deck-tech video (roughly 2,500-5,000 spoken words) yields only 1-3 chunks, keeping the reduce pass's fan-in small.
- Use a small fixed overlap (e.g., the last 1-2 sentences of chunk N repeated as leading context for chunk N+1) so a rule stated right at a chunk boundary is not truncated mid-sentence for Disambiguation's local-context lookup.
- For a video under roughly one chunk's worth of content, running the chunker should be a no-op that returns a single chunk — do not force multiple chunks below the target size.

**Open question flagged below:** the exact word/token threshold is not verified against any production data in this repo (no MTG creator transcript token-length distribution was measured here); the planner should treat the numbers above as a reasonable starting point, not a locked constant, and confirm against a few real transcript lengths (e.g., the Snail corpus already harvested) before finalizing.

## Metric Allowlist (D-02a) — UNION Derivation

This is the single most safety-critical research finding for planning, because Phase 95 has **already shipped** and its metric key namespace cannot be changed without reopening that phase.

### Phase 95's ACTUAL emitted `MeasuredMetric.Metric` keys (read directly from `MeasuredStyleProfileBuilder.cs`, `[VERIFIED: repo source]`)

| Key pattern | Concrete values | Source line |
|---|---|---|
| `category_ratio:{category}` | One row per category found in the creator's crawled decks; `{category}` values come from `CreatorDeckCategoryResolver` output, which is seeded by `ContentTagVocabulary.CardCategories` = `{ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility}` (`ContentTagVocabulary.cs:41-55`) but is NOT strictly closed to that set — `CardCategoryRepository`/Scryfall Tagger oracle tags may surface other category strings at runtime. | `MeasuredStyleProfileBuilder.cs:175` |
| `lift:{CategoryA}\|{CategoryB}` | Dynamic pairs, top 25 by `MaxLiftMetrics`, open-ended (not a fixed enum) | `MeasuredStyleProfileBuilder.cs:196` |
| `combo_density:included_per_deck` | Single fixed key | `MeasuredStyleProfileBuilder.cs:214` |
| `karsten:land_delta` | Single fixed key — actual lands MINUS Karsten target, NOT a raw land count | `MeasuredStyleProfileBuilder.cs:233` |
| `karsten:target_lands` | Single fixed key — the Karsten-COMPUTED recommended target, NOT the deck's actual land count | `MeasuredStyleProfileBuilder.cs:234` |
| `karsten:health_score` | Single fixed key, 0-3 ordinal (Healthy=3…unhealthy=0) | `MeasuredStyleProfileBuilder.cs:235` |

### Snail prototype's stated-rule metrics (from `docs/research/p89-p90-prototype-snail.md`, `[CITED: docs/research/p89-p90-prototype-snail.md]`)

| Stated rule (prototype) | Value/band | Best available Phase-95 join key | Match quality |
|---|---|---|---|
| Land count | 37-42 (28 for low-curve/aggressive-mull decks) | `karsten:target_lands` / `karsten:land_delta` | **MISMATCH** — neither is a raw actual-land-count metric; `target_lands` is Karsten's recommended target, not what the creator actually plays. See Open Question below. |
| Ramp | 7-12 baseline | `category_ratio:ramp` | Direct match |
| Card draw | 13-18 | `category_ratio:draw` | Direct match |
| Removal | 8-14 (15-20 broad) | `category_ratio:removal` | Direct match |
| Interaction | ~20 slow / 5-8 proactive | none | No Phase-95 "interaction" category exists; would need to be a derived sum of removal+counter, or left stated-only |
| Board wipes | 3-5 max | `category_ratio:board-wipe` | Direct match |
| Counterspells | ≥8 in blue | `category_ratio:counter` | Direct match (color-conditionality is a `condition` field concern, not a metric-key concern) |
| Tutors | ~3 at Bracket 2 | `category_ratio:tutor` | Direct match (bracket-conditionality is a `condition` field concern) |
| Copies-to-see-in-opener | ≥10 | none | Hypergeometric/opener-probability concept, no Phase-95 counterpart |
| Colored-symbol shorthand | 30/25/20/15 | none | Pip-count concept; Karsten internals compute pip counts but do not expose them as a `MeasuredMetric` |
| Salt/power (anti-fast-mana) | qualitative | none | Not measurable by Phase 95 at all; likely a `condition`-only / philosophy-only stated rule with no measured counterpart (per CS-17's "stated-only for un-measurable philosophy") |

### Recommended allowlist for the planner to lock

1. **Closed sub-vocabulary for `category_ratio:` joins** = `ContentTagVocabulary.CardCategories` (`ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility`) — reuse this EXACT allowlist for the stated-rule `metric` field whenever the rule is category-count-shaped, so the string literally matches `category_ratio:{value}` once P97 strips the prefix.
2. **`karsten:target_lands`, `karsten:land_delta`, `karsten:health_score`, `combo_density:included_per_deck`** — reuse verbatim for any stated rule about land count/curve health/combo density, WITH the explicit caveat below.
3. **`lift:*` pairs are NOT a fixed vocabulary** — a stated rule should almost never target a `lift:` metric directly (creators state absolute counts, not statistical lift versus a global baseline); recommend excluding `lift:` from the stated-rule allowlist entirely.
4. **New stated-only metrics with no Phase-95 counterpart** (e.g., `land_count`, `interaction`, `opener_probability`, `pip_distribution`, `power_level_philosophy`) should be allowed to exist in the Phase-96 schema's allowlist even though Phase 97 will have nothing to fuse them against yet — CS-17 already anticipates "weight toward stated only for un-measurable philosophy." This is not a Phase 96 blocker.

**Open Question (flagged, not resolved here):** should Phase 96's allowlist include a literal `land_count` metric key distinct from `karsten:target_lands`, accepting that Phase 97 will have no measured counterpart for it (since Phase 95 never emits actual land count as its own metric)? OR should the planner treat "land count" stated rules as targeting `karsten:target_lands` even though that is a computed target, not an actual count, accepting the semantic mismatch? Recommend the former (introduce `land_count` as its own stated-only metric key) because conflating "what the creator says they play" with "what Karsten's algorithm recommends" would produce a confusing, semantically-wrong join in Phase 97. This should be surfaced to the user/discuss-phase for Phase 97, not silently decided here.

## `value`/Band Handling (D-02)

The locked shape wants "37-42 lands" to be ONE rule with `comparator: range`. Recommend a `StatedRuleCandidate` DTO (new, Phase-96-owned, NOT the P94 `StatedRule`) shaped like:

```csharp
public sealed record StatedRuleCandidate
{
    public required string Category { get; init; }
    public required string Metric { get; init; }
    public double? Value { get; init; }        // single-value comparators (gte/lte/eq)
    public double? ValueMin { get; init; }      // range comparator
    public double? ValueMax { get; init; }      // range comparator
    public required string Comparator { get; init; } // "gte" | "lte" | "eq" | "range"
    public string? Condition { get; init; }     // e.g. "archetype:control", "curve:low"
    public int? ClipTimestampSeconds { get; init; }
    public required string SourceClip { get; init; }
    public required double Confidence { get; init; }
    public bool? CardGrounded { get; init; }
    public required DateTimeOffset VideoDateUtc { get; init; }
}
```
`ValidateStatedRules` should enforce: `Comparator == "range"` requires both `ValueMin` and `ValueMax` non-null and `ValueMin <= ValueMax`; any other comparator requires `Value` non-null and both `ValueMin`/`ValueMax` null. This keeps the schema's `additionalProperties:false` JSON contract simple (emit all of `value`, `value_min`, `value_max` as nullable JSON fields; the LLM populates whichever the comparator needs) while giving `SanitizeStatedRules` a single, deterministic place to reject malformed combinations.

**This DTO is explicitly NOT required to satisfy the P94 `StatedRule.TargetValue: double` (non-nullable, single-value) constraint.** Per the Summary's finding, Phase 96 does not write into `CreatorStyleProfileStore`; the translation from a banded `StatedRuleCandidate` into whatever shape Phase 97 needs (possibly two `StatedRule` rows with `gte`/`lte` comparators, or a P94 schema extension via a nested extensible object mirroring `MetricDistribution`'s role) is Phase 97's concern.

## content_type Heuristic (D-05a) — No New LLM Call

Available signals at the point `content_type` would be computed (all ALREADY produced by the existing pipeline, inside `ContentKbOrchestrator.DistillVideoAsync`, before the artifact is written):

- `ClassificationResult` — only "keep" verdicts reach this point (a "drop" verdict short-circuits before summary/clips/tags run at all, per the existing `if (verdict == "drop") { ...filtered...; return; }` branch at `ContentKbOrchestrator.cs:1187-1204`). **This means the binary keep/drop verdict itself carries NO further discriminating signal for content_type** — every video reaching the content_type decision already passed "keep." The `reason` string might contain free-text hints but is not schema-validated or classified into a fixed vocabulary.
- `TagsResult` — `Archetype` (0-3 of 15 allowlisted values), `Bracket` (0-2 of 5), `CardCategory` (0-5 of 11) — all already allowlist-validated.
- `ClipsResult` — 3-8 clips, each with an excerpt and optional timestamp — "clip density" per CONTEXT.md's own phrasing.

**Recommended heuristic (Claude's Discretion; presented as a strong starting recommendation, `[ASSUMED]`, needs planner/discuss-phase confirmation):**

| content_type | Primary signal | Rationale |
|---|---|---|
| `meta-commentary` | `CardCategoryTags.Count == 0` (regardless of archetype/bracket tags) | Directly matches CONTEXT.md's own motivating stat: "~14% of artifacts have zero deckbuilding signal" — zero card-category tags is the cleanest available proxy for "no deckbuilding application," since `FilterTags` already only keeps allowlisted, dominant-topic tags (`ContentKbOrchestrator.cs:1417-1438`). |
| `deck-tech` | `CardCategoryTags.Count >= 1` AND `ArchetypeTags.Count >= 1` | A specific archetype + specific card-category tags together suggest a concrete deck being discussed, not abstract theory. |
| `deckbuilding-theory` | `CardCategoryTags.Count >= 1` AND `ArchetypeTags.Count == 0` | Card-category-level principles discussed without being anchored to one archetype — the closest available proxy for "general principle, not one deck." |
| `gameplay` | **No reliable signal from existing tags exists.** | `Archetype`/`Bracket`/`CardCategory` are all deckbuilding-oriented tag dimensions; none of them capture "this video is about in-game play sequencing/piloting," which is a materially different axis. This bucket is the WEAKEST-supported of the four and should be flagged as an open question rather than silently guessed at. |

**Open Question:** how should `gameplay` be detected without a new LLM call? Two options for the planner to weigh: (a) accept lower precision and fall back to it only when NONE of the other three heuristics fire (i.e., it becomes the default/residual bucket) — cheap, but likely over-broad; (b) add a lightweight keyword scan over the clip excerpts already extracted (e.g., looking for turn-sequencing language) — still "no new LLM call" (D-05a only forbids a new model call, not a deterministic string heuristic over already-extracted text) but adds bespoke keyword-list maintenance. Recommend (b) if the planner wants meaningfully better precision, but (a) is defensible given D-05a's cost constraint and this phase's substrate-only, non-user-facing scope (imprecision here is fixable later without breaking any consumer, since nothing reads `content_type` yet).

## Recency/Provenance (CS-11c)

`ContentVideo.PublishedUtc` (`DateTimeOffset?`) already flows into `ContentKbOrchestrator.DistillVideoAsync` (used today for `ContentArtifactMetadata`/`ContentSiteIndexRow` — see `video.PublishedUtc` at `ContentKbOrchestrator.cs:1366`). This is the exact, already-available source for `video_date` — no new metadata plumbing is required. Each stated rule should simply carry this same `DateTimeOffset?` value (or fail closed / omit the rule if null, since a rule with unknown provenance cannot be superseded correctly by Phase 97 — this should be an explicit validation rule in `ValidateStatedRules`, not a silent null-pass-through).

## Golden Test Pattern (D-06)

The existing "golden" test (`DistillationPromptRegressionTests.SystemPrompts_MatchShippedPhase21Fixtures`/`ResponseFormatSchemas_MatchShippedPhase21Fixtures`) is a **byte-exact string-constant assertion** against the shipped prompt/schema text — it does NOT invoke any LLM or CLI process. This is the pattern for guarding against accidental prompt/schema drift, and the new `StatedRulesSchema`/system prompts should get an equivalent byte-exact assertion added to this same file.

**D-06's "runs the full multi-pass pipeline over a real Salubrious Snail transcript fixture" is a DIFFERENT, complementary test** — it needs to actually exercise `CliLlmDistillationService`'s process-execution path end-to-end WITHOUT invoking a real Claude CLI subprocess (that would be slow, costly, and non-deterministic in CI). The existing seam for this is `CliLlmDistillationServiceTests`' `internal` constructor overload:
```csharp
internal CliLlmDistillationService(
    string provider,
    Func<CliCommandSpec, string, CancellationToken, Task<string>>? processRunnerOverride,
    TimeSpan? timeoutOverride = null)
```
which lets a test supply a **queue of canned stdout responses** (`ClaudeEnvelope("""{"summary":"..."}""")`) instead of actually shelling out. D-06's golden test should:
1. Load a REAL transcript excerpt (not synthetic deck-list data — contrast with Phase 95's `SnailSeedCorpusFixture`, which is a synthetic-but-representative deck corpus, not real transcript text) — likely a fixture file checked into `DeckFlow.Core.Tests` sourced from an actual Snail video transcript already harvested by the KB pipeline, or a hand-authored excerpt that faithfully mirrors the ~27-rule prototype's source language (lands 37-42, board wipes 3-5, etc., per `docs/research/p89-p90-prototype-snail.md`).
2. Queue canned CLI JSON responses for each expected Select/Disambiguate/Decompose call (one queue entry per chunk × 3 stages) plus one Reduce response, using the SAME `internal` process-runner-override seam, so the test is fully deterministic and fast (no real subprocess).
3. Assert the final `StatedRuleCandidate[]` both (a) passes `ValidateStatedRules` and (b) contains the expected representative rules (at minimum land-count band, board-wipe cap, and one dropped/ambiguous case) from the prototype.
4. This test belongs in `DeckFlow.Core.Tests` (new file, e.g. `StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs`), reusing `CliLlmDistillationServiceTests`' existing `ClaudeEnvelope`/`WithCommandOverrideAsync` helper pattern.

**The UTF-8/CP437 harness itself (`BuildStartInfo` pinning `StandardOutputEncoding`/`StandardErrorEncoding` to `Encoding.UTF8`) requires NO change** — it is provider-agnostic and already applies to every CLI call, including whatever new methods the Claimify stages add, as long as they go through the same `ExtractWithRetryAsync`/`RunProcessAsync` path. The planner's job is to route the new stages through that existing path, not to reimplement it.

## Card Grounding Seam (D-07)

Covered in detail under "Pattern 3" above. Summary of the exact contract:
- Endpoint: `GET https://api.scryfall.com/cards/named?fuzzy={name}` (Scryfall's own fuzzy-match algorithm; NOT a local fuzzy-string-match reimplementation).
- Already wrapped: `IScryfallCardResolver.SearchPrintingFallbackCardAsync` (or a new dedicated fuzzy-only method) → `ScryfallThrottle.ExecuteAsync` → named Polly `"scryfall"` pipeline → `RestClient` from `IScryfallRestClientFactory`.
- Response contract: HTTP 200 + single `ScryfallCard` = confident single match (Scryfall's own fuzzy algorithm already resolves ambiguity or 404s rather than returning a list) → rewrite + `card_grounded=true`. Any non-2xx (404 not_found/ambiguous, 429, 5xx) → unresolved → keep rule + `card_grounded=false`, per D-07 (never drop).
- Layering: MUST be called from `DeckFlow.Web` (via a new narrow `ICardNameGrounder` Core interface) — Core structurally cannot reach the `internal` `ScryfallThrottle`.
- Caching: CONTEXT.md's "code_context" section calls for a "cached Scryfall lookup" — no dedicated card-name-resolution cache currently exists in this exact shape; the planner should decide whether to reuse `IMemoryCache` (already used elsewhere per `AddMemoryCache()` DI registration) keyed by normalized candidate name, given repeated card names are likely across a creator's many videos.

## Common Pitfalls

### Pitfall 1: Treating Claimify's "no merge step" as a bug rather than by design
**What goes wrong:** A plan or prompt author assumes literal Claimify already handles cross-chunk/cross-sentence deduplication and skips designing the reduce pass carefully.
**Why it happens:** The paper's name and reputation ("high-quality claim extraction") implies end-to-end completeness; it is easy to miss that it operates purely at sentence granularity.
**How to avoid:** Explicitly document in the plan that the reduce/dedupe pass is a DeckFlow-specific addition, design its dedupe key (`metric`+`condition`, per CONTEXT.md's own discretion note) deliberately, and test it with genuinely duplicate/near-duplicate candidates across 2+ chunks.
**Warning signs:** The golden test (D-06) shows duplicate rules for the same metric surviving into the final artifact.

### Pitfall 2: Silently truncating a banded rule into the P94 `StatedRule.TargetValue` double
**What goes wrong:** A developer, trying to "integrate" with the existing `CreatorStyleProfile.StatedRule` record, picks e.g. the midpoint or the min of a 37-42 band and loses the band information entirely, defeating D-02's explicit purpose.
**Why it happens:** `StatedRule` already exists and looks like the "obvious" target type; the temptation to reuse it directly is strong.
**How to avoid:** Introduce the phase's OWN `StatedRuleCandidate` DTO (band-capable) as documented above; do not write into `CreatorStyleProfileStore` at all this phase (per the confirmed absence of any such canonical_ref in CONTEXT.md).
**Warning signs:** A new dependency from this phase onto `ICreatorStyleProfileStore`/`CreatorStyleProfileStore.UpsertAsync` appears in a plan — that is very likely scope creep into Phase 97.

### Pitfall 3: Re-distill leaving orphaned stated-rule rows
**What goes wrong:** `ClearDistillOutputAsync` is not updated to also clear `content_stated_rules`, so a re-distill (D-05's mechanism, exercised even without a mass backfill) leaves stale rules from a prior extraction alongside newly-inserted ones.
**Why it happens:** The three existing DELETE statements are easy to overlook when adding a fourth table; nothing fails loudly if the fourth DELETE is missing — it just silently accumulates duplicate/stale rows.
**How to avoid:** Add the fourth `DELETE FROM content_stated_rules WHERE video_id = @videoId;` line to `ClearDistillOutputSql` in the SAME commit that adds `InsertStatedRuleAsync`, and add a regression test mirroring the existing `T-45-16`-style "re-distill produces no orphaned rows" coverage already implied by `ContentVideoStoreDistillTests.cs`.
**Warning signs:** A round-trip test that inserts, clears, and re-inserts stated rules shows row-count growth instead of a clean replace.

### Pitfall 4: Forgetting the byte-stable artifact gate on unrelated existing artifacts
**What goes wrong:** Extending `ContentArtifactMetadata` with new fields (`ContentType`, `StatedRules`) accidentally changes the rendered output for artifacts that are NOT being re-distilled, e.g. by changing `ToText`'s handling of null/empty collections in a way that touches the tags/summary/clips sections too.
**Why it happens:** `ToText` is a single method; a careless refactor of shared formatting logic (e.g., the `builder.AppendLine` sequencing) can shift line counts or spacing even for the pre-existing sections.
**How to avoid:** Add the new frontmatter lines and the new `## Stated Rules` (or embedded frontmatter block) as strictly additive appends; do not touch the existing `## Summary`/`## Key Clips`/`## Tags` rendering code paths. Update `ContentArtifactSpec.ArtifactFileFormat` (the documentation fixture) and both `ContentArtifactWriterTests.cs`/`ContentArtifactSpecTests.cs` in the SAME commit — these are the only 2 test files plus 1 production call site (`ContentKbOrchestrator.cs`) that reference `ContentArtifactWriter.ToText`/`ContentArtifactMetadata`, so the blast radius is small and fully enumerable.
**Warning signs:** Any existing, previously-committed `.md` artifact (not touched by a re-distill) shows a diff after a build/test run — this would indicate the shared rendering path was touched, not just additive new lines.

### Pitfall 5: Content_type computed from the WRONG classification signal
**What goes wrong:** A plan assumes `ClassificationResult.Verdict` ("keep"/"drop") can discriminate among the 4 content_type buckets.
**Why it happens:** It's the only classifier-shaped signal in the pipeline, so it's tempting to lean on it.
**How to avoid:** Recognize that only "keep" videos ever reach the content_type decision point (see "content_type Heuristic" above) — the verdict itself carries zero further discriminating signal; use `TagsResult`/`ClipsResult` instead, per the heuristic table above.
**Warning signs:** content_type ends up nearly 100% one value, or the heuristic code references `classification.Verdict` at all past the initial keep/drop gate.

## Code Examples

### Multi-pass stage method addition to `ILlmDistillationService` (recommended shape)
```csharp
// NEW methods on ILlmDistillationService, mirroring ClassifyAsync's subscription-only default:
Task<SelectResult> SelectStatedClaimsAsync(string transcriptChunk, CancellationToken cancellationToken = default)
    => Task.FromException<SelectResult>(
        new NotSupportedException("Stated-rules extraction requires the subscription LLM CLI provider."));

Task<DisambiguateResult> DisambiguateStatedClaimsAsync(SelectResult selected, CancellationToken cancellationToken = default)
    => Task.FromException<DisambiguateResult>(
        new NotSupportedException("Stated-rules extraction requires the subscription LLM CLI provider."));

Task<DecomposeResult> DecomposeStatedClaimsAsync(DisambiguateResult disambiguated, CancellationToken cancellationToken = default)
    => Task.FromException<DecomposeResult>(
        new NotSupportedException("Stated-rules extraction requires the subscription LLM CLI provider."));

Task<ReduceResult> ReduceStatedRulesAsync(IReadOnlyList<DecomposeResult> allChunks, CancellationToken cancellationToken = default)
    => Task.FromException<ReduceResult>(
        new NotSupportedException("Stated-rules extraction requires the subscription LLM CLI provider."));
```
This mirrors the EXACT existing default-interface-method pattern at `DeckFlow.Core/Integration/ILlmDistillationService.cs:24-26`, so `LlmDistillationService` (OpenAI) needs zero changes unless the planner later decides to support it there too.

### `ClearDistillOutputAsync` extension (both dialects, single SQL constant)
```csharp
// Source pattern: DeckFlow.Core/Content/ContentVideoStore.cs:604-611 (existing)
private const string ClearDistillOutputSql = """
    DELETE FROM content_summaries
     WHERE video_id = @videoId;
    DELETE FROM content_clips
     WHERE video_id = @videoId;
    DELETE FROM content_tags
     WHERE video_id = @videoId;
    DELETE FROM content_stated_rules
     WHERE video_id = @videoId;
    """;
```

## State of the Art

| Old Approach (this repo, pre-Phase-96) | New Approach (Phase 96) | When Changed | Impact |
|--------------|------------------|--------------|--------|
| 3 constrained-decoding calls per video (summary, clips, tags), single-shot each | 3 constrained-decoding calls PLUS a multi-pass Claimify sub-pipeline (3 calls × chunks + 1 reduce) | This phase | Meaningfully higher token cost and latency per distilled video; must be gated to subscription/CLI provider per the existing `ClassifyAsync` precedent to avoid metered-provider cost surprises |
| Artifact frontmatter: `source/title/url/video_id/tags/generated_utc` only | Adds `content_type:` (single enum string) and `stated_rules:` (array of structured objects) | This phase | Additive frontmatter fields; byte-stability of PRE-EXISTING artifacts must be preserved (Pitfall 4) |
| 3 child tables (`content_summaries`, `content_clips`, `content_tags`) | Recommends a 4th (`content_stated_rules`) | This phase (recommended, not explicitly locked in CONTEXT.md) | Establishes the queryable per-video source Phase 97 will need; without it, Phase 97 must re-parse committed markdown |

**Deprecated/outdated:** Nothing in the existing pipeline is being deprecated by this phase — this is purely additive.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Recommended chunk size (~2,000-4,000 words, timestamp-boundary-aligned, small fixed overlap) | Map-Reduce Chunking | If too large, defeats the token/cost benefit of chunking and risks context-window issues; if too small, multiplies the already-high 3-calls-per-chunk cost further. No production transcript-length data was measured in this research session. |
| A2 | content_type heuristic mapping table (tag-presence-based rules for meta-commentary/deck-tech/deckbuilding-theory; unresolved for gameplay) | content_type Heuristic | If wrong, `content_type` mis-labels artifacts, degrading Phase 97's "clean coverage denominator" motivation (CONTEXT.md's own stated purpose for this field) — but low blast radius since nothing consumes `content_type` yet this phase. |
| A3 | Recommendation to introduce a NEW `land_count` stated-only metric key distinct from `karsten:target_lands`/`karsten:land_delta` | Metric Allowlist / Open Question | If the planner instead conflates land-count stated rules onto `karsten:target_lands`, Phase 97 fusion would compare "what the creator says he plays" against "what an algorithm recommends," which is a different question — likely produces a misleading conflict signal downstream. |
| A4 | Recommendation to gate the new Claimify-stage methods to the subscription/CLI provider only (mirroring `ClassifyAsync`) | Pattern 2 / Code Examples | If not gated, an operator running the metered OpenAI provider could incur substantial unplanned spend from the 3x-per-chunk call multiplier; conversely if the planner has a different cost-control mechanism in mind, this recommendation may be redundant, not wrong. |
| A5 | Recommendation to add a `content_stated_rules` child table (not explicitly named in CONTEXT.md's canonical_refs) | Pattern 4 / Don't Hand-Roll | If omitted, Phase 97 has no structured per-video rule source and must re-parse markdown frontmatter at fusion time — more fragile, inconsistent with the established store pattern, but not strictly a Phase-96 test failure since CS-11a only requires the YAML block to exist. |
| A6 | The Microsoft Research blog's description of Claimify's 3 stages (input/output shapes, "no merge step") is an accurate summary of the arXiv paper | Claimify Method | The blog is Microsoft's own secondary description of their own paper, not independently verified against the raw PDF text in this session; if the blog oversimplifies, DeckFlow's stage-by-stage prompt design could miss a nuance (e.g., the paper may describe a "5-sentence context window" or similar specific parameter not captured by the blog summary). |

## Open Questions

1. **Should `land_count` be its own stated-only metric key, or should land-count stated rules target `karsten:target_lands`?**
   - What we know: Phase 95 emits `karsten:target_lands`/`karsten:land_delta`, neither of which is a raw actual-land-count metric; the Snail prototype's single strongest-agreement stated rule is specifically about land COUNT (37-42).
   - What's unclear: Whether Phase 97 (not yet planned) will want to introduce a genuinely new measured "actual land count" metric of its own (which would require touching already-shipped Phase 95 code), or whether it will accept comparing stated land-count rules only against `karsten:target_lands` with a documented semantic caveat.
   - Recommendation: Introduce `land_count` as its own stated-only metric key in Phase 96's allowlist (no Phase-95 counterpart needed yet); flag this explicitly for Phase 97's own research/discuss-phase rather than resolving it here, since resolving it might require reopening already-shipped Phase 95 code, which is out of this phase's scope.

2. **How should `gameplay` content_type be detected given no new LLM call is allowed and no existing tag dimension captures it?**
   - What we know: `Archetype`/`Bracket`/`CardCategory` tags are all deckbuilding-oriented; none capture "this is a play-by-play/gameplay video."
   - What's unclear: Whether a lightweight keyword heuristic over already-extracted clip excerpts is acceptable "no new LLM call" scope, or whether `gameplay` should just be the residual/default bucket.
   - Recommendation: Treat `gameplay` as the residual bucket (fires only when none of the other three heuristics match) unless the planner/user wants to invest in a keyword-list heuristic; either way, this needs an explicit decision recorded in the plan, not a silent default.

3. **Should the new Claimify-stage LLM methods be gated to the subscription/CLI provider only, and if so, should `DistillAsync`'s existing `isSubscriptionProvider` gate be extended to cover them explicitly (the way it already does for `ClassifyAsync`)?**
   - What we know: The existing precedent (`ClassifyAsync`) is CLI-only and the orchestrator explicitly refuses non-dry-run distills on a metered provider because of it.
   - What's unclear: Whether the planner wants the SAME hard refusal for stated-rules extraction, or a softer degrade (e.g., skip stated-rules extraction on OpenAI but still run summary/clips/tags).
   - Recommendation: Hard-gate identically to `ClassifyAsync`, given CONTEXT.md's own explicit "cost note" flagging this as the most token-heavy part of the phase; a partial degrade adds branching complexity without a clearly stated user need.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Claude CLI (`claude` on PATH, or `DECKFLOW_LLM_CLI_COMMAND` override) | D-06 golden test relies on the process-runner-override test seam, NOT a live CLI call, so the real CLI binary is NOT required for tests | N/A for tests | — | Tests use `processRunnerOverride`; only a live (non-test) distill run needs the real binary, unchanged from today |
| Scryfall API reachability (`api.scryfall.com`) | D-07 card grounding (production runtime only) | N/A for unit tests (mocked via `IScryfallCardResolver` test seam, matching existing `ScryfallCardResolverTests` patterns) | — | Existing `ScryfallThrottle.ThrowIfUpstreamUnavailable` already handles 429/5xx; D-07 keeps the rule + flags `card_grounded=false` on any failure, so no new fallback design is needed |
| .NET 10 SDK, xUnit | All new tests | ✓ (already the project baseline per `CLAUDE.md`) | net10.0 / xUnit 2.9.3 | — |

**Missing dependencies with no fallback:** None identified.

**Missing dependencies with fallback:** None beyond the already-existing Scryfall-unavailable handling described above.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`), matching existing `.csproj` |
| Config file | None dedicated — standard `dotnet test` discovery, mirrors every existing Core test file |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~StatedRules` |
| Full suite command | `dotnet build && dotnet test DeckFlow.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CS-11 | Transcript chunking produces expected chunk boundaries/count for representative lengths | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~TranscriptChunkerTests` | ❌ Wave 0 |
| CS-11a | `ContentArtifactWriter.ToText` emits a `stated_rules:` block matching the locked shape | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ContentArtifactWriterTests` | ✅ file exists, extend |
| CS-11b | content_type heuristic returns the correct bucket for representative tag/clip combinations | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ContentTypeHeuristicTests` | ❌ Wave 0 |
| CS-11c | Each persisted/rendered rule carries `video_date` sourced from `ContentVideo.PublishedUtc` | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ContentVideoStoreDistillTests` | ✅ file exists, extend |
| CS-12 | Ambiguous candidate sentences are dropped, never reach Decompose/output | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~StatedRuleReducerTests` (or a dedicated Disambiguate-stage test) | ❌ Wave 0 |
| CS-13 | New `StatedRulesSchema`/system prompt match byte-exact fixtures (regression) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~DistillationPromptRegressionTests` | ✅ file exists, extend |
| CS-14 | Every emitted rule carries non-empty `sourceClip` and a `confidence` in the valid range | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ValidateStatedRulesTests` (new, mirrors `DistillationValidationTests` pattern if one exists, else co-located) | ❌ Wave 0 |
| CS-15 | Golden regression over a real Snail transcript fixture, using the UTF-8 CLI harness + canned process-runner responses; card grounding rewrites/flags correctly | integration (deterministic, no live network/process) | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CliLlmDistillationStatedRulesGoldenTests` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln` (0 new warnings, per `CLAUDE.md`'s Definition of Done) + the quick filtered test run above scoped to whichever file was just touched.
- **Per wave merge:** `dotnet test DeckFlow.sln` (full solution, both Core and Web test projects, since the Web-hosted `ICardNameGrounder` implementation and its tests live in `DeckFlow.Web.Tests`).
- **Phase gate:** Full suite green before `/gsd:verify-work`, per this repo's established `Phase 94/95` precedent (both closed with a green full-suite run per `STATE.md`'s velocity log).

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/StatedRulesExtraction/TranscriptChunkerTests.cs` — covers CS-11
- [ ] `DeckFlow.Core.Tests/StatedRulesExtraction/ContentTypeHeuristicTests.cs` — covers CS-11b
- [ ] `DeckFlow.Core.Tests/StatedRulesExtraction/StatedRuleReducerTests.cs` — covers CS-12 (ambiguity-drop + dedupe)
- [ ] `DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs` — covers CS-15 (D-06 golden test); needs a real-transcript-derived fixture (Snail excerpt) checked into the test project
- [ ] `DeckFlow.Web.Tests/Services/Scryfall/ScryfallCardNameGrounderTests.cs` — covers CS-15's grounding pass, mirroring existing `ScryfallCardResolver` test patterns
- Framework install: none — xUnit is already fully wired for both test projects.

## Security Domain

`security_enforcement` is absent from `.planning/config.json`, so treated as enabled per instructions; however, this phase has NO user-facing surface, NO new authentication/session/access-control boundary, and NO new external package. The relevant ASVS categories are narrow:

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No new auth surface this phase |
| V3 Session Management | No | No new session surface this phase |
| V4 Access Control | No | No new access-controlled endpoint this phase |
| V5 Input Validation | Yes | LLM-produced JSON is untrusted output from an external process/API and MUST pass through `ValidateStatedRules`/`SanitizeStatedRules` (allowlist-driven, mirroring existing `ValidateTags`/`SanitizeTags`) before being persisted or rendered — never trust the model's JSON as pre-validated just because it matched the requested schema shape (the CLI path in particular has NO tooling-enforced schema conformance, only post-hoc parsing, per "Constrained Decoding Mechanism" above). |
| V6 Cryptography | No | No new cryptographic operation this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| LLM-injected path traversal / control characters inside a "card name" or "source clip" string later written into a file path or DB query | Tampering | `ContentArtifactWriter`'s existing `SanitizePathSegment` already guards path construction; the new `stated_rules` fields are rendered as JSON-string content within the artifact body, never as path segments, so no NEW path-construction code should be introduced for this phase — reuse the existing safe rendering path (`Quote()`/`JsonSerializer.Serialize`) rather than hand-concatenating any LLM-produced string into a file path or raw SQL string. |
| Prompt injection via transcript content attempting to make the model emit out-of-schema or malicious JSON | Tampering | Existing `additionalProperties:false` schemas + post-hoc `Validate*/Sanitize*` allowlist enforcement (for `category`/`comparator`, treat these as closed allowlists exactly like `ContentTagVocabulary`, not free-text) is the standing mitigation; extend the SAME pattern for the new stated-rules payload rather than trusting the LLM's own schema adherence. |
| Unbounded LLM cost from an adversarially long or repetitive transcript defeating the chunking cost model | Denial of Service (cost) | The existing `MaxTranscriptInputTokens = 120_000` cap (`ValidateTranscriptLength`) already bounds the input; chunking must not bypass this cap, and the reduce pass's fan-in (bounded by chunk count) should have a sane upper bound asserted in code, not left unbounded. |

## Sources

### Primary (HIGH confidence)
- Direct source reads: `DeckFlow.Core/Knowledge/DistillationSchemas.cs`, `DistillationValidation.cs`, `ContentArtifactWriter.cs`, `ContentArtifactSpec.cs`; `DeckFlow.Core/Integration/CliLlmDistillationService.cs`, `LlmDistillationService.cs`, `LlmDistillationProviderFactory.cs`, `ILlmDistillationService.cs`, `CliCommandSpec.cs`; `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs`; `DeckFlow.Core/Content/ContentVideoStore.cs`, `IContentVideoStore.cs`; `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs`; `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs`; `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs`; `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs`, `ScryfallThrottle.cs`; `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs`, `CliLlmDistillationServiceTests.cs`; `DeckFlow.Web.Tests/Services/CreatorStyle/SnailSeedCorpusFixture.cs`.

### Secondary (MEDIUM confidence)
- [Claimify: Extracting high-quality claims from language model outputs — Microsoft Research blog](https://www.microsoft.com/en-us/research/blog/claimify-extracting-high-quality-claims-from-language-model-outputs/) — stage-by-stage input/output description, verified via `WebFetch` against the live page.
- `docs/research/p89-p90-prototype-snail.md`, `docs/research/creator-style-llm-system.md`, `docs/research/creator-style-roadmap.md` — internal prototype/design docs, treated as CITED (authored by this project's own prior research phases, not independently re-verified against external sources).

### Tertiary (LOW confidence)
- [arXiv:2502.10855 abstract/search snippets](https://arxiv.org/abs/2502.10855) — surfaced via `WebSearch`, not independently fetched in full; used only to corroborate the blog's stage names, not as the primary source for stage detail.

## Metadata

**Confidence breakdown:**
- Standard stack / architecture extension points: HIGH — every claim in "Architecture Patterns," the metric allowlist tables, and the store/writer extension recommendations is read directly from shipped source in this repo.
- Claimify method description: MEDIUM — verified against Microsoft's own blog post (a secondary summary of the primary paper), not the raw arXiv PDF.
- content_type heuristic, chunk sizing, `land_count` metric-key recommendation, subscription-gating recommendation: MEDIUM-LOW — these are reasoned design recommendations for genuinely novel decisions this phase must make; each is logged in the Assumptions table and should be confirmed by the planner/user, not treated as settled fact.

**Research date:** 2026-07-12
**Valid until:** 30 days (stable, in-repo-code-grounded; the external Claimify citation is unlikely to change, but the in-repo extension points should be re-verified if Phase 95/97 planning or execution touches the same files before Phase 96 executes).
