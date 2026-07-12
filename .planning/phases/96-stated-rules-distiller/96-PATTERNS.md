# Phase 96: Stated-Rules Distiller - Pattern Map

**Mapped:** 2026-07-12
**Files analyzed:** 20 (new + modified)
**Analogs found:** 20 / 20 (all HIGH-confidence, read directly from shipped source)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleCandidate.cs` | model (DTO) | transform | `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/LiftCalculator.cs` (`CategoryLift` record) + `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (`StatedRule` record) | role-match |
| `DeckFlow.Core/Knowledge/StatedRulesExtraction/TranscriptChunker.cs` | utility (pure static helper) | transform | `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs` | exact (namespace + style twin) |
| `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleReducer.cs` | utility (pure static helper) | transform (dedupe) | `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/LiftCalculator.cs` | exact (namespace + style twin) |
| `DeckFlow.Core/Knowledge/StatedRulesExtraction/ContentTypeHeuristic.cs` | utility (pure static helper) | transform (classifier) | `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs` | exact (namespace + style twin) |
| `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs` | service interface (Core-facing seam) | request-response | Phase 95 D-11 Core/Web seam precedent (no single file — see "Card Grounding Seam" pattern below) | role-match |
| `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (EXTEND) | config (const schemas + prompts) | transform | itself, existing `ClipsSchema`/`ClipsSystemPrompt` block | exact |
| `DeckFlow.Core/Knowledge/DistillationValidation.cs` (EXTEND) | utility (Validate*/Sanitize*) | transform | itself, existing `ValidateTags`/`SanitizeTags`/`TagsPayload` block | exact |
| `DeckFlow.Core/Integration/ILlmDistillationService.cs` (EXTEND) | service interface | request-response | itself, existing `ClassifyAsync` default-interface-method gate | exact |
| `DeckFlow.Core/Integration/CliLlmDistillationService.cs` (EXTEND) | service (CLI adapter) | request-response | itself, existing `ExtractClipsAsync`/`ClassifyAsync` methods | exact |
| `DeckFlow.Core/Integration/LlmDistillationService.cs` (NOT extended — gated CLI-only per Pattern 2) | service (OpenAI adapter) | request-response | itself (no change expected; document why) | exact |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (EXTEND `DistillVideoAsync`) | controller (orchestrator) | pipeline / event-driven | itself, existing tags-stage call+cost-ledger+insert sequence (lines 1263-1322) | exact |
| `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` (EXTEND `ToText`) | utility (renderer) | transform | itself, existing frontmatter `tags:`/`## Key Clips` block | exact |
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (EXTEND `ContentArtifactMetadata`, `ArtifactFileFormat`) | model + doc-fixture | transform | itself | exact |
| `DeckFlow.Core/Content/IContentVideoStore.cs` (EXTEND: `InsertStatedRuleAsync`, `ClearDistillOutputAsync` doc) | store interface | CRUD | itself, existing `InsertClipAsync`/`InsertTagAsync` | exact |
| `DeckFlow.Core/Content/ContentVideoStore.cs` (EXTEND: DDL + insert + clear, both dialects) | store implementation | CRUD | itself, existing `content_clips` table + `InsertClipSql`/`ClearDistillOutputSql` | exact |
| `DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs` | service (Web-hosted adapter) | request-response | `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` (`SearchPrintingFallbackCardAsync` + ctor DI/test-seam split) | exact |
| `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs` (EXTEND) | test (byte-exact regression) | transform | itself, existing `SystemPrompts_MatchShippedPhase21Fixtures` | exact |
| `DeckFlow.Core.Tests/StatedRulesExtraction/*Tests.cs` (NEW) | test (unit) | transform | `DeckFlow.Core.Tests` sibling unit-test style (xUnit `[Fact]`) | role-match |
| `DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs` (NEW) | test (golden/integration, deterministic) | pipeline | `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` (`ClaudeEnvelope`/`WithCommandOverrideAsync`/`CreateService` seam) | exact |
| `DeckFlow.Core.Tests/ContentVideoStoreDistillTests.cs` (EXTEND) | test (store round-trip) | CRUD | itself, existing `ClearDistillOutputAsync_RemovesPriorSummaryClipAndTagRowsOnly` | exact |

## Pattern Assignments

### `DeckFlow.Core/Knowledge/StatedRulesExtraction/*` (pure-Core extraction logic)

**Analog:** `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs` and `LiftCalculator.cs` (Phase 95, already shipped)

**Namespace + file-header pattern** (`CategoryCounter.cs:1-9`):
```csharp
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Pure helper for multi-bucket category counting across creator deck samples.
/// </summary>
public static class CategoryCounter
{
```
Mirror exactly for the new `DeckFlow.Core.Knowledge.StatedRulesExtraction` namespace: `public static class TranscriptChunker`, `public static class StatedRuleReducer`, `public static class ContentTypeHeuristic`. No `HttpClient`, no `Microsoft.AspNetCore.*`, no `RestSharp` usings anywhere in this folder — that is the entire point of the Core/Web split (verified: neither `CategoryCounter.cs` nor `LiftCalculator.cs` references anything outside `DeckFlow.Core.Models`/`DeckFlow.Core.Reporting`).

**Argument-guard style** (`CategoryCounter.cs:21-22`, `LiftCalculator.cs:20-22`):
```csharp
ArgumentNullException.ThrowIfNull(sample);
ArgumentNullException.ThrowIfNull(cardCategories);
```
Every public static method on the new helpers should open with `ArgumentNullException.ThrowIfNull(...)`/`ArgumentException.ThrowIfNullOrWhiteSpace(...)` guards, exactly like this.

**Pure static computation + a `sealed record` result type, both in the SAME file** (`LiftCalculator.cs:114-130`):
```csharp
/// <summary>
/// Category-pair lift result emitted by <see cref="LiftCalculator"/>.
/// </summary>
public sealed record CategoryLift
{
    /// <summary>The first category in canonical sorted order.</summary>
    public required string CategoryA { get; init; }
    ...
}
```
`StatedRuleReducer.cs` should follow this exact shape: the reducer class plus any small result wrapper record co-located, IF the record is reducer-specific. The `StatedRuleCandidate` DTO itself (used by ALL of Select/Disambiguate/Decompose/Reduce/Validate) should be its OWN file (`StatedRuleCandidate.cs`) since it's shared, not reducer-private — see next section.

**Documented rationale via `// Why:` comments for non-obvious business rules** (`LiftCalculator.cs:63,70`):
```csharp
// Why: omitting pairs with missing baseline marginals keeps downstream consumers free of
// NaN/Infinity while still signaling that the shared corpus has no usable denominator for this pair.
continue;
```
Use this same `// Why:` inline-comment convention for the dedupe-key decision in `StatedRuleReducer` (per CLAUDE.md's own "Document *why*" convention) and for the ambiguity-drop behavior in the Disambiguate-stage validation.

---

### `StatedRuleCandidate` DTO (band-capable, Phase-96-owned)

**Analog A — sibling record shape idiom:** `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs:39-58` (`StatedRule`) and `:102-118` (`MetricDistribution`)
```csharp
/// <summary>
/// Immutable stated creator rule captured from a source clip.
/// </summary>
public sealed record StatedRule
{
    /// <summary>Rule category used to group related creator guidance.</summary>
    public required string Category { get; init; }

    /// <summary>Metric targeted by the stated rule.</summary>
    public required string TargetMetric { get; init; }

    /// <summary>Target metric value expressed by the creator.</summary>
    public required double TargetValue { get; init; }

    /// <summary>Comparator describing how the target value should be interpreted.</summary>
    public required string Comparator { get; init; }

    /// <summary>Source clip excerpt supporting the stated rule.</summary>
    public required string SourceClip { get; init; }

    /// <summary>Confidence assigned to the extracted rule.</summary>
    public required double Confidence { get; init; }
}
```
**DO NOT reuse this record directly** (per RESEARCH.md's Pitfall 2 — no band/condition/clip_ts/video_date; Phase 96 does not write `ICreatorStyleProfileStore`). Instead, mirror ITS property idiom (`required string`, `required double`, XML doc per property, `sealed record`) into a NEW, phase-96-owned record with band support — RESEARCH.md's own recommended shape (verbatim, already vetted against this exact codebase's conventions):
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
Put this in `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleCandidate.cs` with the `DeckFlow.Core.Knowledge.StatedRulesExtraction` namespace (NOT bare `DeckFlow.Core.Knowledge` — this record is Phase-96-private, unlike `StatedRule` which lives at the `CreatorStyleProfile.cs` top level because Phase 97 consumes it).

**Analog B — nullable-distribution-field idiom for optional sub-shape:** `CreatorStyleProfile.cs:74-76` (`MeasuredMetric.Distribution`)
```csharp
/// <summary>Optional distribution details for the measured metric.</summary>
public MetricDistribution? Distribution { get; init; }
```
Same nullable-optional-property idiom applies to `Condition`, `ClipTimestampSeconds`, `CardGrounded` on `StatedRuleCandidate` above.

---

### `DistillationSchemas.cs` — new `StatedRulesSchema` + 4 system prompts (Select/Disambiguate/Decompose/Reduce)

**Analog:** `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (entire file, 99 lines — already fully read)

**Schema-const idiom** (`DistillationSchemas.cs:28-40`, `ClipsSchema`):
```csharp
/// <summary>
/// Strict schema for key clip extraction.
/// </summary>
public const string ClipsSchema = """
    {"type":"object","additionalProperties":false,
     "properties":{"clips":{"type":"array","items":{
        "type":"object","additionalProperties":false,
        "properties":{
            "timestamp_seconds":{"type":["integer","null"]},
            "excerpt":{"type":"string"}},
        "required":["timestamp_seconds","excerpt"]}}},
     "required":["clips"]}
    """;
```
Copy this EXACT `const string XSchema = """...""";` raw-string-literal shape for `StatedRulesSelectSchema`/`StatedRulesDisambiguateSchema`/`StatedRulesDecomposeSchema`/`StatedRulesReduceSchema` (or a single combined `StatedRulesSchema` if the planner collapses stages into fewer schemas — CONTEXT.md D-01 implies 4 distinct calls, so 4 distinct schemas is the safer default to mirror the existing 1-schema-per-call convention). **CRITICAL per CLAUDE.md carve-out: raw string literals must NOT be re-indented — the exact whitespace inside `"""..."""` ships to the LLM.**

**System-prompt-const idiom** (`DistillationSchemas.cs:74-82`, `ClipsSystemPrompt`):
```csharp
/// <summary>System prompt for key clip extraction.</summary>
public static string ClipsSystemPrompt { get; } = """
    You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
    Output only JSON matching the supplied schema.
    Use timestamp_seconds only from an explicit [mm:ss] marker present in the transcript at or just before the advice moment. If no marker is nearby, still return the clip but set its timestamp_seconds to null rather than estimating; never invent or interpolate a time.
    ...
    """;
```
Mirror this `public static string XSystemPrompt { get; } = """...""";` shape for each of the 4 new prompts. Per RESEARCH.md's "Anti-Patterns" section: **write these prompts fresh** (matching the tone/constraint style of the existing prompts above), do NOT transcribe wording from the Claimify paper/blog.

**Allowlist-interpolation idiom** (`DistillationSchemas.cs:84-98`, `TagsSystemPrompt`):
```csharp
public static string TagsSystemPrompt
{ get; } =
    "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
    + "Output only JSON matching the supplied schema. "
    + "Choose ONLY from these allowlists; do not invent new values. "
    ...
    + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
    ...

private static string FormatAllowlist(IReadOnlySet<string> values)
    => string.Join(", ", values);
```
If the planner locks a `metric` controlled-vocabulary allowlist (D-02a), interpolate it into the Decompose/Reduce system prompts using this SAME `FormatAllowlist` helper (already private in this file — reuse it, don't duplicate).

---

### `DistillationValidation.cs` — new `ValidateStatedRules`/`SanitizeStatedRules`

**Analog:** `DeckFlow.Core/Knowledge/DistillationValidation.cs` (entire file, 197 lines — already fully read)

**Validate* idiom — throws `InvalidOperationException` on any contract violation** (`DistillationValidation.cs:61-66`, `ValidateTags`):
```csharp
internal static void ValidateTags(TagsPayload payload)
{
    ValidateTagDimension("archetype", payload.Archetype, ContentTagVocabulary.Archetypes);
    ValidateTagDimension("bracket", payload.Bracket, ContentTagVocabulary.Brackets);
    ValidateTagDimension("card_category", payload.CardCategory, ContentTagVocabulary.CardCategories);
}
```
and the per-dimension detail (`:158-181`, `ValidateTagDimension`):
```csharp
private static void ValidateTagDimension(
    string dimension,
    IReadOnlyList<string> values,
    IReadOnlySet<string> allowlist)
{
    if (values is null)
    {
        throw new InvalidOperationException($"{dimension} tags cannot be null.");
    }

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var value in values)
    {
        if (string.IsNullOrWhiteSpace(value) || !allowlist.Contains(value))
        {
            throw new InvalidOperationException($"{dimension} tag '{value}' is not in the content tag vocabulary.");
        }

        if (!seen.Add(value))
        {
            throw new InvalidOperationException($"{dimension} tag '{value}' is duplicated.");
        }
    }
}
```
`ValidateStatedRules` should mirror this shape: throw `InvalidOperationException` (not a custom exception type) for each locked-shape rule violation — per RESEARCH.md's exact recommended checks: `Comparator == "range"` requires both `ValueMin`/`ValueMax` non-null and `ValueMin <= ValueMax`; any other comparator requires `Value` non-null with both `ValueMin`/`ValueMax` null; `metric` must be in the (planner-derived) allowlist (same `allowlist.Contains(value)` pattern as `ValidateTagDimension`); `VideoDateUtc` must be non-default (RESEARCH.md's "Recency/Provenance" section — fail closed on unknown provenance, do not silently null-pass).

**Sanitize* idiom — never throws, defensively drops/normalizes** (`DistillationValidation.cs:77-83`, `SanitizeClips`):
```csharp
internal static IReadOnlyList<ClipItem> SanitizeClips(IReadOnlyList<ClipItem>? clips)
{
    return (clips ?? [])
        .Where(clip => clip.TimestampSeconds is null or >= 0)
        .Take(MaxClipCount)
        .ToArray();
}
```
`SanitizeStatedRules` should follow this null-coalesce + `Where`/`Take` LINQ-chain idiom — filter out malformed candidates (rather than throwing) before they reach `ValidateStatedRules`, matching the existing two-tier Sanitize-then-Validate flow already used for tags/clips.

**Payload record idiom, colocated at file bottom** (`DistillationValidation.cs:184-197`):
```csharp
/// <summary>JSON payload shape for the summary extraction call.</summary>
internal sealed record SummaryPayload(string Summary);

/// <summary>JSON payload shape for the clip extraction call.</summary>
internal sealed record ClipsPayload(IReadOnlyList<ClipItem> Clips);
```
New `internal sealed record SelectPayload(...)`, `DisambiguatePayload(...)`, `DecomposePayload(...)`, `ReducePayload(...)` (raw LLM JSON shapes, snake_case-mapped via the shared `JsonOpts`) go here, distinct from the public `StatedRuleCandidate` DTO used internally by the pipeline — mirrors the existing `TagsPayload` (raw JSON shape) vs. the eventual sanitized/validated in-memory result split.

**Shared constant idiom** (`DistillationValidation.cs:14-24`): add new `internal const int` bounds (e.g. a max-rules-per-video cap, matching `MinClipCount`/`MaxClipCount`) alongside the existing constants, in the SAME class, not a new one.

---

### `ILlmDistillationService` + `CliLlmDistillationService` + `LlmDistillationService` — new Select/Disambiguate/Decompose/Reduce methods

**Analog 1 — subscription-only default-interface-method gate** (`DeckFlow.Core/Integration/ILlmDistillationService.cs:24-26`, `ClassifyAsync`):
```csharp
Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
    => Task.FromException<ClassificationResult>(
        new NotSupportedException("Classifier requires the subscription LLM CLI provider."));
```
Add the 4 new methods to `ILlmDistillationService` with this EXACT default-interface-method shape (RESEARCH.md's own recommended code, verified consistent with this precedent):
```csharp
Task<SelectResult> SelectStatedClaimsAsync(string transcriptChunk, CancellationToken cancellationToken = default)
    => Task.FromException<SelectResult>(
        new NotSupportedException("Stated-rules extraction requires the subscription LLM CLI provider."));
```
`LlmDistillationService` (OpenAI) needs ZERO changes — it inherits the `NotSupportedException` default, exactly like it does today for `ClassifyAsync` (confirmed: `LlmDistillationService.cs` has no `ClassifyAsync` override).

**Analog 2 — CLI implementation shape, retry+schema+sanitize** (`CliLlmDistillationService.cs:85-99`, `ExtractClipsAsync`):
```csharp
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
Each of the 4 new `CliLlmDistillationService` methods follows this exact 3-line body: build instruction from the matching schema/prompt pair, call the SHARED `ExtractWithRetryAsync<T>` (already handles the CLI-JSON-envelope unwrap + balanced-brace JSON scan + retry-3 loop — reuse it verbatim, do not reimplement), then sanitize via the new `DistillationValidation.Sanitize*` and return `new TokenUsage(0, 0)` (CLI subscription calls are always $0-metered, matching every existing CLI method).

**Analog 3 — the UTF-8 process harness is provider-agnostic; zero changes needed** (`CliLlmDistillationService.cs:242-260`, `BuildStartInfo`):
```csharp
internal static ProcessStartInfo BuildStartInfo(CliCommandSpec spec)
{
    var startInfo = new ProcessStartInfo(spec.FileName)
    {
        ...
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };
    ...
}
```
The new Select/Disambiguate/Decompose/Reduce calls automatically get this UTF-8 fix for free as long as they route through the existing `ExtractWithRetryAsync` → `_runProcess` → `RunProcessAsync`/`BuildStartInfo` path — confirming D-06's mandate is satisfied by construction, not by new code.

---

### `ContentKbOrchestrator.DistillVideoAsync` — sequencing the new stage(s)

**Analog:** `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs:1167-1400` (`DistillVideoAsync`, already fully read)

**Per-call cost-gate + cost-record + counter-increment idiom, repeated 3x today** (`ContentKbOrchestrator.cs:1263-1288`, the tags-stage call — closest analog since it's the LAST stage before persistence, i.e. where a 4th stage slots in):
```csharp
if (!isSubscriptionProvider && await _llmLedger.WouldExceedCapAsync(
    DistillationValidation.ComputeProjectedCallCostUsd(transcript.Body, DistillationValidation.TagsMaxOutputTokens),
    monthKey,
    cancellationToken).ConfigureAwait(false))
{
    return await MarkSkippedOverCapAsync(
        video.Id, naturalKey,
        "llm monthly cap would be exceeded before tags for " + naturalKey,
        llmCalls, llmSpend, progress, cancellationToken).ConfigureAwait(false);
}

var tags = await _distiller.InferTagsAsync(transcript.Body, cancellationToken).ConfigureAwait(false);
var tagsCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(tags.Usage.InputTokens, tags.Usage.OutputTokens);
await _llmLedger.RecordCallAsync(
    video.Id, tags.Usage.InputTokens, tags.Usage.OutputTokens, tagsCost, monthKey,
    cancellationToken).ConfigureAwait(false);
llmCalls++;
llmSpend += tagsCost;
```
The new stated-rules stage (chunk → Select → Disambiguate → Decompose per chunk, then one Reduce) slots in AFTER this block, BEFORE the `DistillationValidation.ValidateSummary(...)`/insert-rows block at line 1290. Given the ~3-4x call multiplier (CONTEXT.md's cost note + RESEARCH.md Pattern 2), and since these new methods are subscription-CLI-only via the `NotSupportedException` default, the `!isSubscriptionProvider && ...` cap-check guard should almost certainly wrap a HARD REFUSAL for non-subscription providers (mirroring how `isSubscriptionProvider` already gates whether `ClassifyAsync`-requiring non-dry-run distills are allowed at all, per RESEARCH.md's Pattern 2/Open-Question-3 recommendation) — this is a planner decision point, not silently assumed here.

**Insert-child-rows-after-validate idiom** (`ContentKbOrchestrator.cs:1290-1322`):
```csharp
DistillationValidation.ValidateSummary(summary.Summary);
DistillationValidation.ValidateClips(clips.Clips);
var archetypeTags = FilterTags(ContentTagDimension.Archetype, tags.Archetype);
...
await _videoStore.InsertSummaryAsync(video.Id, summary.Summary, cancellationToken).ConfigureAwait(false);
var sortOrder = 0;
foreach (var clip in clips.Clips)
{
    await _videoStore.InsertClipAsync(video.Id, clip.TimestampSeconds ?? 0, clip.Excerpt, sortOrder++, cancellationToken).ConfigureAwait(false);
}
foreach (var tag in archetypeTags)
{
    await _videoStore.InsertTagAsync(video.Id, ContentTagDimension.Archetype, tag, cancellationToken).ConfigureAwait(false);
}
```
The new `foreach (var rule in statedRules) { await _videoStore.InsertStatedRuleAsync(video.Id, rule, sortOrder++, cancellationToken)...; }` loop goes here, after `DistillationValidation.ValidateStatedRules(...)`, following the exact same call-then-loop-insert shape as clips/tags.

**`ContentArtifactMetadata` construction site — additive-only extension point** (`ContentKbOrchestrator.cs:1324-1339`):
```csharp
var metadata = new ContentArtifactMetadata
{
    Source = source.DisplayName,
    Title = video.Title,
    Url = video.VideoUrl,
    YoutubeVideoId = video.YoutubeVideoId,
    RssGuid = video.RssGuid,
    ArchetypeTags = archetypeTags,
    BracketTags = bracketTags,
    CardCategoryTags = cardCategoryTags,
    GeneratedUtc = generatedUtc,
};
var artifactText = ContentArtifactWriter.ToText(
    metadata,
    summary.Summary,
    clips.Clips.Select(clip => (clip.TimestampSeconds, clip.Excerpt)).ToArray());
```
Add `ContentType = contentType` and `StatedRules = statedRules` (or similar) as NEW init-properties on this SAME object-initializer block; `video.PublishedUtc` (already read at line 1366 for the site-index row) is the exact same value to stamp `VideoDateUtc` onto each `StatedRuleCandidate` per D-04/CS-11c — no new metadata plumbing needed, per RESEARCH.md's "Recency/Provenance" finding.

**This is the ONE call site for `ContentArtifactWriter.ToText`/`ContentArtifactMetadata` in production code** — confirmed by RESEARCH.md's Pitfall 4 ("only 2 test files plus 1 production call site... reference `ContentArtifactWriter.ToText`/`ContentArtifactMetadata`"), so the blast radius of extending both is small and fully enumerable.

---

### `ContentArtifactWriter.ToText` — additive `content_type:` field + `stated_rules:` block

**Analog:** `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs:43-102` (`ToText`, already fully read)

**Frontmatter-field-append idiom** (`ContentArtifactWriter.cs:58-68`):
```csharp
builder.AppendLine("---");
builder.Append("source: ").AppendLine(Quote(metadata.Source));
builder.Append("title: ").AppendLine(Quote(metadata.Title));
builder.Append("url: ").AppendLine(Quote(metadata.Url));
builder.Append("video_id: ").AppendLine(Quote(videoId));
builder.AppendLine("tags:");
builder.Append("  archetype: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.ArchetypeTags));
builder.Append("  bracket: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.BracketTags));
builder.Append("  card_category: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.CardCategoryTags));
builder.Append("generated_utc: ").AppendLine(Quote(FormatGeneratedUtc(metadata.GeneratedUtc)));
builder.AppendLine("---");
```
Per RESEARCH.md's Pitfall 4 (byte-stable gate): add `builder.Append("content_type: ").AppendLine(Quote(metadata.ContentType));` and a `stated_rules:` JSON-flow-mapping block as STRICTLY ADDITIVE new lines inside this SAME `---`-delimited block (e.g., right after `generated_utc:`, before the closing `---`) — do NOT touch the existing `source`/`title`/`url`/`video_id`/`tags`/`generated_utc` lines or their ordering. The `## Summary`/`## Key Clips`/`## Tags` body sections (`:70-99`) must likewise remain byte-for-byte untouched; if the plan adds a `## Stated Rules` body section, append it strictly AFTER the existing `## Tags` section, never interleaved.

**Tag-serialization idiom to mirror for `stated_rules:`** (`ContentArtifactSpec.cs:48-53`, `SerializeTags`):
```csharp
public static string SerializeTags(IReadOnlyList<string> tags)
{
    ArgumentNullException.ThrowIfNull(tags);
    return JsonSerializer.Serialize(tags);
}
```
A new `ContentArtifactSpec.SerializeStatedRules(IReadOnlyList<StatedRuleCandidate> rules)` should follow this exact `JsonSerializer.Serialize(...)` one-liner shape — JSON is valid YAML flow-mapping, so no new YAML library is needed (per RESEARCH.md's "Don't Hand-Roll" table and CLAUDE.md's "no new packages without asking" rule).

**Existing safe-quoting helper to reuse, never hand-concatenate** (`ContentArtifactWriter.cs:191-195`, `Quote`):
```csharp
private static string Quote(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return JsonSerializer.Serialize(value);
}
```
Use `Quote(...)` for `content_type:`; per RESEARCH.md's security section, never hand-concatenate an LLM-produced string (card name, source clip, condition) into the artifact text outside this existing `Quote()`/`JsonSerializer.Serialize` path.

**`ArtifactFileFormat` doc-fixture idiom to extend in the SAME commit** (`ContentArtifactSpec.cs:13-41`, the full fixture string) — add `content_type: "deck-tech"` and a `stated_rules: [...]` example line to this fixture, matching the existing `tags:`/`generated_utc:` line style, so the documented contract stays in sync with the renderer (per RESEARCH.md's Pitfall 4 remediation: update `ContentArtifactSpec.ArtifactFileFormat` + both `ContentArtifactWriterTests.cs`/`ContentArtifactSpecTests.cs` in the same commit).

**`ContentArtifactMetadata` new-property idiom** (`ContentArtifactSpec.cs:91-98`):
```csharp
/// <summary>Allowlisted archetype tags serialized into artifact front matter.</summary>
public required IReadOnlyList<string> ArchetypeTags { get; init; }
...
/// <summary>Allowlisted card category tags serialized into artifact front matter.</summary>
public required IReadOnlyList<string> CardCategoryTags { get; init; }
```
Add `public required string ContentType { get; init; }` and `public IReadOnlyList<StatedRuleCandidate> StatedRules { get; init; } = Array.Empty<StatedRuleCandidate>();` (non-required, defaulting to empty — matching how `ArchetypeTags` etc. default via the null-guard pattern at the top of `ToText`, but note StatedRules is genuinely optional per-video unlike the required tag lists).

---

### `content_stated_rules` new store table — `IContentVideoStore` / `ContentVideoStore`

**Analog:** `content_clips` table + `InsertClipAsync`/`InsertClipSql` (both dialects), `DeckFlow.Core/Content/ContentVideoStore.cs` (already fully read: lines 204-219, 568-572, 675-694, 729-748) and `DeckFlow.Core/Content/IContentVideoStore.cs:104-118`

**Interface method idiom** (`IContentVideoStore.cs:104-118`, `InsertClipAsync`):
```csharp
/// <summary>
/// Inserts a timestamped clip excerpt for a video.
/// </summary>
/// <param name="videoId">Identifier of the owning video.</param>
/// <param name="timestampS">Timestamp in seconds from the start of the content item.</param>
/// <param name="excerpt">Clip excerpt text.</param>
/// <param name="sortOrder">Stable sort order for clips under the same video.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The inserted clip identifier.</returns>
Task<long> InsertClipAsync(
    long videoId,
    int timestampS,
    string excerpt,
    int sortOrder,
    CancellationToken cancellationToken = default);
```
New `Task<long> InsertStatedRuleAsync(long videoId, StatedRuleCandidate rule, int sortOrder, CancellationToken cancellationToken = default);` mirrors this exact XML-doc + parameter-list shape (pass the whole `StatedRuleCandidate` rather than exploding every field as a parameter, since it has 12 fields — a deliberate, justified deviation from the flat-parameter style, matching how `InsertTagAsync` also stays flat only because it has just 3 fields).

**Implementation idiom** (`ContentVideoStore.cs:204-219`, `InsertClipAsync`):
```csharp
public async Task<long> InsertClipAsync(
    long videoId, int timestampS, string excerpt, int sortOrder,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(excerpt);
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
        InsertClipSql,
        new { videoId, timestampS, excerpt, sortOrder },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```
Mirror exactly: `EnsureSchemaAsync` → `OpenConnectionAsync` → `ExecuteScalarAsync<long>` with a `RETURNING id;`-suffixed SQL const, using Dapper anonymous-object parameter binding.

**Insert SQL const idiom** (`ContentVideoStore.cs:568-572`, `InsertClipSql`):
```csharp
private const string InsertClipSql = """
    INSERT INTO content_clips (video_id, timestamp_s, excerpt, sort_order)
    VALUES (@videoId, @timestampS, @excerpt, @sortOrder)
    RETURNING id;
    """;
```
New `InsertStatedRuleSql` follows this exact `INSERT INTO ... VALUES (...) RETURNING id;` shape — same for both Postgres and SQLite (SQLite also supports `RETURNING` per the existing single-shared-SQL-const pattern; no dialect branch is needed at the SQL-text level for inserts, only for DDL).

**Postgres DDL idiom** (`ContentVideoStore.cs:675-681`, `content_clips` table):
```sql
CREATE TABLE IF NOT EXISTS content_clips (
  id          BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  video_id    BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
  timestamp_s INT NOT NULL,
  excerpt     TEXT NOT NULL,
  sort_order  INT NOT NULL DEFAULT 0
);
```
RESEARCH.md's recommended `content_stated_rules` DDL (verbatim, already codebase-consistent):
```sql
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
```
Add a matching `CREATE INDEX IF NOT EXISTS ix_content_stated_rules_video_id ON content_stated_rules(video_id);` line right after it, mirroring the existing `ix_content_clips_video_id` index line (`:692`).

**SQLite DDL idiom** (`ContentVideoStore.cs:729-735`, the SQLite twin of `content_clips`):
```sql
CREATE TABLE IF NOT EXISTS content_clips (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  video_id    INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
  timestamp_s INTEGER NOT NULL,
  excerpt     TEXT NOT NULL,
  sort_order  INTEGER NOT NULL DEFAULT 0
);
```
Apply the SAME `BIGINT→INTEGER`/`DOUBLE PRECISION→REAL`/`BOOLEAN→INTEGER`/`GENERATED BY DEFAULT AS IDENTITY→AUTOINCREMENT` substitution already used consistently across every other table's Postgres/SQLite pair in this file.

**Clear-on-redistill idiom — MUST add the 4th DELETE in the SAME commit** (`ContentVideoStore.cs:604-611`, `ClearDistillOutputSql`):
```csharp
private const string ClearDistillOutputSql = """
    DELETE FROM content_summaries
     WHERE video_id = @videoId;
    DELETE FROM content_clips
     WHERE video_id = @videoId;
    DELETE FROM content_tags
     WHERE video_id = @videoId;
    """;
```
New line: `DELETE FROM content_stated_rules\n WHERE video_id = @videoId;` appended — per RESEARCH.md's Pitfall 3, forgetting this line silently accumulates orphaned rows on re-distill with nothing failing loudly.

---

### Card Grounding Seam (Core interface + Web implementation, D-07)

**Analog — Core-facing interface + Web DI/test-seam split, mirrors `IScryfallCardResolver`'s own constructor split:** `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:50-93`
```csharp
public ScryfallCardResolver(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider)
    : this(scryfallRestClientFactory, pipelineProvider, null, null, null, null)
{
}

internal ScryfallCardResolver(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    RestClient? restClientOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null,
    ...)
```
`ScryfallCardNameGrounder` (the NEW Web-hosted `ICardNameGrounder` implementation) should take the already-DI-registered `IScryfallCardResolver` as its only constructor dependency (simple public DI ctor, no internal test-seam needed on the grounder itself since `IScryfallCardResolver` is already independently mockable in tests) — see RESEARCH.md's exact recommended shape:
```csharp
public sealed class ScryfallCardNameGrounder(IScryfallCardResolver resolver) : ICardNameGrounder
{
    public async Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken ct = default)
    {
        var card = await resolver.SearchPrintingFallbackCardAsync(candidateName, ct);
        return card is not null
            ? new CardGroundingResult(true, card.Name)
            : new CardGroundingResult(false, candidateName);
    }
}
```

**Exact fuzzy-lookup contract being wrapped** (`ScryfallCardResolver.cs:195-202` per RESEARCH.md, verified):
```csharp
var namedRequest = new RestRequest("cards/named", Method.Get);
namedRequest.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);
if ((int)namedResponse.StatusCode is >= 200 and < 300 && namedResponse.Data is not null)
{
    return namedResponse.Data; // single confident match
}
return null; // 404 -> unresolved
```
**Structural constraint (verified, non-negotiable):** `ScryfallThrottle` is `internal static class ScryfallThrottle` in `DeckFlow.Web.Services` (`ScryfallThrottle.cs:11`) with NO `InternalsVisibleTo` grant to `DeckFlow.Core` — Core CANNOT reach it directly. This is WHY the grounder must live in `DeckFlow.Web` behind a narrow Core-owned interface, per the exact same Phase-95 D-11 precedent already applied elsewhere in this codebase (measured-style Karsten/lift computation stays pure-Core; only the Scryfall HTTP call itself is Web-hosted).

**Core-side interface shape (new file):**
```csharp
// DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs
public interface ICardNameGrounder
{
    Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default);
}

public sealed record CardGroundingResult(bool Resolved, string CanonicalName);
```

---

### Golden Regression Test (D-06)

**Analog 1 — byte-exact prompt/schema regression, extend the SAME file:** `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs:6-53` (`SystemPrompts_MatchShippedPhase21Fixtures`, `ResponseFormatSchemas_MatchShippedPhase21Fixtures`)
```csharp
public sealed class DistillationPromptRegressionTests
{
    [Fact]
    public void SystemPrompts_MatchShippedPhase21Fixtures()
    {
        const string expectedSummaryPrompt = """
            You extract paste-ready deckbuilding summaries from Magic: The Gathering video transcripts...
            """;
        ...
        Assert.Equal(expectedSummaryPrompt, DistillationSchemas.SummarySystemPrompt);
    }
```
Add a `StatedRulesPrompts_MatchShippedFixtures` (or similar) `[Fact]` to this SAME test class asserting each of the 4 new system prompts + `StatedRulesSchema`(s) byte-exact, following the identical `const string expectedX = """...""";` + `Assert.Equal(expectedX, DistillationSchemas.X)` shape.

**Analog 2 — canned-response process-runner-override seam for the full-pipeline golden test:** `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` (helper methods at file bottom, already read)
```csharp
private static CliLlmDistillationService CreateService(Queue<string> stdoutQueue, TimeSpan? timeout = null)
    => new(
        "claude",
        (_, _, _) => Task.FromResult(stdoutQueue.Dequeue()),
        timeout);

private static string ClaudeEnvelope(string result, bool isError = false)
    => JsonSerializer.Serialize(new { type = "result", is_error = isError, result });
```
and a representative call-site:
```csharp
var stdout = new Queue<string>([ClaudeEnvelope("""{"summary":"Build around sacrifice payoffs."}""")]);
var service = CreateService(stdout);
var result = await WithCommandOverrideAsync(ValidOverride, () => service.SummarizeAsync("transcript"));
```
The new `DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs` should reuse this EXACT `Queue<string>` + `CreateService`-style helper (recreate the `(_, _, _) => Task.FromResult(stdoutQueue.Dequeue())` processRunnerOverride pattern; either copy the private helpers or make them `internal` and share via `[InternalsVisibleTo]` if the planner prefers not to duplicate) — queue one `ClaudeEnvelope(...)` response per expected Select/Disambiguate/Decompose call (one per chunk × 3 stages) plus one Reduce response, then assert the final `StatedRuleCandidate[]` passes `ValidateStatedRules` and contains the expected representative Snail-prototype rules (land-count band, board-wipe cap, one dropped/ambiguous case). This is fully deterministic — no real subprocess, no live network — satisfying D-06 without live-CLI cost/flakiness.

---

### `ContentVideoStoreDistillTests.cs` — extend for `content_stated_rules` round-trip + clear-on-redistill

**Analog:** `DeckFlow.Core.Tests/ContentVideoStoreDistillTests.cs:141-151` (`ClearDistillOutputAsync_RemovesPriorSummaryClipAndTagRowsOnly`)
```csharp
[Fact]
public async Task ClearDistillOutputAsync_RemovesPriorSummaryClipAndTagRowsOnly()
{
    ...
    await _videoStore.InsertClipAsync(videoId, 42, "clip", 1);
    await _videoStore.InsertTagAsync(videoId, ContentTagDimension.Archetype, "combo");
    ...
    await _videoStore.ClearDistillOutputAsync(videoId);
    ...
}
```
Add `await _videoStore.InsertStatedRuleAsync(videoId, rule, 1);` to this SAME test (asserting the stated-rule row is ALSO cleared) plus a new dedicated round-trip test (`InsertStatedRuleAsync_ThenClear_RemovesRow` or similar) mirroring the existing insert-then-count-then-clear-then-recount shape already used for clips/tags in this file — this is the regression guard for RESEARCH.md's Pitfall 3.

---

## Shared Patterns

### Constrained-decoding extension (schema + prompt + payload + Validate*/Sanitize*)
**Source:** `DeckFlow.Core/Knowledge/DistillationSchemas.cs` + `DeckFlow.Core/Knowledge/DistillationValidation.cs` (both files, fully read above)
**Apply to:** All 4 new Select/Disambiguate/Decompose/Reduce dimensions — same 4-piece shape every existing dimension (summary/classification/clips/tags) already uses: `const string XSchema` + `static string XSystemPrompt` in `DistillationSchemas.cs`; `internal sealed record XPayload` + `internal static void ValidateX(...)`/`internal static X SanitizeX(...)` in `DistillationValidation.cs`.

### Subscription-only capability gate (default-interface-method throw)
**Source:** `DeckFlow.Core/Integration/ILlmDistillationService.cs:24-26` (`ClassifyAsync`)
**Apply to:** All 4 new `ILlmDistillationService` methods — `LlmDistillationService` (OpenAI) needs zero code changes; only `CliLlmDistillationService` implements them.

### Core/Web layering seam (pure logic in Core, HTTP-touching adapter in Web)
**Source:** Phase 95 D-11 precedent (measured-style Karsten computation stays Core; only the Scryfall lookup itself, via `IScryfallCardResolver`, is Web-hosted) + `ScryfallThrottle.cs:11` (`internal static`, no `InternalsVisibleTo` to Core)
**Apply to:** `ICardNameGrounder` (Core interface) / `ScryfallCardNameGrounder` (Web implementation) — the ONLY new file in this phase that must live in `DeckFlow.Web` rather than `DeckFlow.Core`.

### Dialect-guarded child table (Postgres + SQLite DDL pair, insert, clear)
**Source:** `DeckFlow.Core/Content/ContentVideoStore.cs:568-748` (`content_clips`/`content_tags` DDL, `InsertClipSql`/`InsertTagSql`, `ClearDistillOutputSql`)
**Apply to:** New `content_stated_rules` table — same `BIGINT GENERATED BY DEFAULT AS IDENTITY`/`INTEGER PRIMARY KEY AUTOINCREMENT` dialect split, `RETURNING id;` insert, and MANDATORY 4th line in `ClearDistillOutputSql`.

### Byte-stable additive artifact rendering
**Source:** `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs:43-102` (`ToText`) + `ContentArtifactSpec.cs:13-41` (`ArtifactFileFormat` doc fixture)
**Apply to:** `content_type:`/`stated_rules:` frontmatter additions — append-only; never reorder or touch existing `source`/`title`/`url`/`video_id`/`tags`/`generated_utc`/`## Summary`/`## Key Clips`/`## Tags` lines. Update the doc fixture + both writer/spec test files in the SAME commit.

### Deterministic test seam over live CLI subprocess
**Source:** `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` (`CreateService`, `ClaudeEnvelope`, `WithCommandOverrideAsync`)
**Apply to:** All new `StatedRulesExtraction` unit tests that exercise `CliLlmDistillationService`, and the D-06 golden test specifically — never shells out to a real `claude` process in CI.

## No Analog Found

None. Every file in scope has a HIGH-confidence, directly-read shipped analog in this exact codebase (RESEARCH.md's own confidence assessment: "the extension points (schemas/validation/writer/orchestrator/store) are HIGH confidence"). The only genuinely novel design surfaces (Claimify stage prompts themselves, the content_type heuristic thresholds, the metric allowlist) are NOT "no analog" cases in the pattern-mapping sense — they reuse the exact SAME mechanical patterns above; only their prompt/business-rule CONTENT is new, not their code shape.

## Metadata

**Analog search scope:** `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/`, `DeckFlow.Core/Integration/`, `DeckFlow.Core/Orchestration/`, `DeckFlow.Core/Content/`, `DeckFlow.Web/Services/Scryfall/`, `DeckFlow.Core.Tests/`
**Files scanned:** 20 read in full or targeted-section (all listed in File Classification table above)
**Pattern extraction date:** 2026-07-12
