# Phase 30: Content KB Integration - Pattern Map

**Mapped:** 2026-06-05
**Files analyzed:** 12 new/modified files
**Analogs found:** 12 / 12

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Services/ContentKbRelevanceService.cs` | service | request-response | `DeckFlow.Web/Services/CommanderSpellbookService.cs` | exact |
| `DeckFlow.Web/Models/ContentKbExcerpt.cs` | model | transform | `DeckFlow.Web/Models/AdminContentKbViewModel.cs` (`KbEntryRow`) | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` | service interface | request-response | self (extend in place) | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` | service | transform | self (extend in place) | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs` | service | transform | `ChatGptAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` | service | transform | `ChatGptAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | service | request-response | self (extend in place) | exact |
| `DeckFlow.Web/Services/PacketArtifactStore.cs` | utility | file-I/O | self (extend in place) | exact |
| `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | model | transform | self (extend in place) | exact |
| `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` | component | request-response | `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (`<details>` block lines 217-247) | role-match |
| `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` | controller | CRUD | self (extend in place) | exact |
| `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` | component | CRUD | self (extend in place) | exact |

---

## Pattern Assignments

### `DeckFlow.Web/Services/ContentKbRelevanceService.cs` (service, request-response)

**Analog:** `DeckFlow.Web/Services/CommanderSpellbookService.cs`

**Imports pattern** (lines 1-11):
```csharp
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.FeatureFlags;
```

**Interface + class declaration pattern** (lines 40-54 of `CommanderSpellbookService.cs`):
```csharp
// ICommanderSpellbookService shows the interface-and-implementation in one file convention.
// ContentKbRelevanceService follows the same file structure:
//   1. Interface (public) with XML doc
//   2. Sealed implementation (public) with XML doc
//   3. Internal test ctor with optional override delegate
//   4. Public DI ctor with ArgumentNullException.ThrowIfNull guards

public interface IContentKbRelevanceService
{
    /// <summary>
    /// Returns up to K=5 relevant curated clips for the given deck context,
    /// or null when the content.kb.enabled flag is off or no clips match.
    /// </summary>
    Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
        string? commanderName,
        string? bracket,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all visible rows with their relevance scores (for admin KBI-06 preview).
    /// </summary>
    Task<IReadOnlyList<(ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(
        string? commanderName,
        string? bracket,
        CancellationToken cancellationToken = default);
}

public sealed class ContentKbRelevanceService : IContentKbRelevanceService
{
    private const int MaxClips = 5;
    private const int MaxExcerptWords = 150;

    private readonly IContentSiteIndexStore _store;
    private readonly ContentKbArtifactPathResolver _pathResolver;
    private readonly IFeatureFlagCache _flagCache;
    private readonly ILogger<ContentKbRelevanceService> _logger;
```

**Internal test ctor pattern** (lines 67-82 of `CommanderSpellbookService.cs`):
```csharp
// CommanderSpellbookService internal test ctor pattern — inject optional delegate to bypass HTTP:
internal CommanderSpellbookService(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    IMemoryCache memoryCache,
    ILogger<CommanderSpellbookService>? logger = null,
    Func<string, CancellationToken, Task<string?>>? postJsonAsync = null)
{
    ArgumentNullException.ThrowIfNull(httpClientFactory);
    // ...
    _logger = logger ?? NullLogger<CommanderSpellbookService>.Instance;
    _postJsonAsync = postJsonAsync ?? PostJsonAsync;
}
```
ContentKbRelevanceService public DI ctor follows the same guard pattern:
```csharp
public ContentKbRelevanceService(
    IContentSiteIndexStore store,
    ContentKbArtifactPathResolver pathResolver,
    IFeatureFlagCache flagCache,
    ILogger<ContentKbRelevanceService>? logger = null)
{
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(pathResolver);
    ArgumentNullException.ThrowIfNull(flagCache);
    _store = store;
    _pathResolver = pathResolver;
    _flagCache = flagCache;
    _logger = logger ?? NullLogger<ContentKbRelevanceService>.Instance;
}
```

**Feature-flag first-check pattern** (from `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` + `AdminContentKbController.cs` line 84):
```csharp
// Flag check MUST be the first statement — before any DB or filesystem access.
// IFeatureFlagCache.IsEnabled returns true (default-on) when key is missing.
public async Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
    string? commanderName,
    string? bracket,
    CancellationToken cancellationToken = default)
{
    if (!_flagCache.IsEnabled("content.kb.enabled"))
    {
        return null;  // flag OFF: skip all KB work; panel hidden by caller
    }
    // ... rest of implementation
}
```

**Graceful null-return on no-match** (combo null-handling precedent at `DeckAnalysisPacketService.cs` line 562-564):
```csharp
// ComboTask returns Task<CommanderSpellbookResult?>(null) when not required.
// ContentKbRelevanceService returns null when flag off OR no clips match.
// Callers use ?. / null-coalescing — no exception path for "no data" case.
var comboTask = AnalysisQuestionCatalog.RequiresComboLookup(selectedQuestions)
    ? _commanderSpellbookService.FindCombosAsync(deckEntries, cancellationToken)
    : Task.FromResult<CommanderSpellbookResult?>(null);
```

**Error handling pattern** (from `CommanderSpellbookService.cs` — returns null on API failure):
```csharp
// Services return null on upstream failure, not exception.
// ContentKbRelevanceService returns null/empty on file-read failure (not throw).
try { ... }
catch (Exception ex)
{
    _logger.LogWarning(ex, "Content KB clip read failed for artifact {Path}.", artifactPath);
    return null;
}
```

---

### `DeckFlow.Web/Models/ContentKbExcerpt.cs` (model, transform)

**Analog:** `DeckFlow.Web/Models/AdminContentKbViewModel.cs` — `KbEntryRow` sealed record (lines 55-71)

**{ get; init; } record pattern** (lines 55-71 of `AdminContentKbViewModel.cs`):
```csharp
// CRITICAL: every property must be { get; init; } — never { get; }.
// System.Text.Json silently skips get-only properties in .NET 9+.
// This has broken EdhTop16Client deserialization before (per CLAUDE.md).
public sealed record KbEntryRow
{
    /// <summary>Surrogate row id (the SetVisibility key).</summary>
    public required long Id { get; init; }

    /// <summary>Entry title.</summary>
    public required string Title { get; init; }

    /// <summary>Source display name.</summary>
    public required string Source { get; init; }

    /// <summary>Archetype + bracket tag chips for display.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Whether this entry is currently published to the public surface.</summary>
    public required bool IsVisible { get; init; }
}
```
ContentKbExcerpt follows the same `sealed record` + `required ... { get; init; }` shape:
```csharp
// DeckFlow.Web/Models/ContentKbExcerpt.cs
// Why: { get; init; } is required — System.Text.Json skips get-only properties.
// This record is serialized into 32-expert-context.json in the packet zip.
public sealed record ContentKbExcerpt
{
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string VideoUrl { get; init; }
    public required string TimestampLabel { get; init; }
    public required string Excerpt { get; init; }
    public required DateTimeOffset HarvestDate { get; init; }
    public double Score { get; init; }
}
```

**Namespace/file-scope pattern** (`DeckFlow.Web/Models/AdminContentKbViewModel.cs` line 1):
```csharp
namespace DeckFlow.Web.Models;
// No using block needed for a plain record with only BCL types + project-local types.
```

---

### `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` (interface, request-response)

**Current full file** (lines 1-27 — verified):
```csharp
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

/// <summary>
/// Strategy interface for building a deck-analysis prompt body targeting a specific AI platform.
/// </summary>
internal interface IAnalysisPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the analysis prompt text for the given request and pre-assembled text blocks.
    /// </summary>
    string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions);
}
```
**Extension:** Add `IReadOnlyList<ContentKbExcerpt>? kbExcerpts = null` as the final parameter, after `bool includeCardVersions`.

---

### `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` (service, transform) — MODIFIED

**Analog:** self (extend in place); `ClaudeAnalysisPromptVariant.cs` and `GeminiAnalysisPromptVariant.cs` follow same pattern independently.

**Existing Build signature** (lines 24-33):
```csharp
public string Build(
    DeckAnalysisRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult,
    bool includeCardVersions)
```

**Existing tail pattern to insert after** (lines 232-249):
```csharp
// --- Combo reference (if available) ---
var comboReferenceText = DeckAnalysisPacketService.BuildComboReferenceText(comboResult);
if (!string.IsNullOrWhiteSpace(comboReferenceText))
{
    builder.AppendLine();
    builder.AppendLine(comboReferenceText);
}

// --- Reference data ---
builder.AppendLine();
builder.AppendLine("## REFERENCE DATA");
builder.AppendLine(referenceText);

// --- Decklist ---
builder.AppendLine();
builder.AppendLine("## DECKLIST");
builder.AppendLine(decklistText);
return builder.ToString().TrimEnd();
```

**Expert Context injection pattern** (insert before `return builder.ToString().TrimEnd()`):
```csharp
// --- Expert Context (KB clips, if available and within budget) ---
// Why: Pitfall 3 guard — NEVER emit the header when clips is null/empty.
// Why: Pitfall 5 guard — skip injection for Gemini when prompt already large.
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
return builder.ToString().TrimEnd();
```

**IMPORTANT:** Claude and Gemini variants get identical Expert Context prose block, hand-edited independently. Do NOT extract shared prose — prompt variants are intentionally decoupled (see CLAUDE.md `reference_prompt_variants_intentionally_decoupled.md`).

---

### `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (service, request-response) — MODIFIED

**Analog:** self (extend in place)

**Combo null-handling precedent** (lines 562-564) — pattern for KB service call:
```csharp
// Start combo lookup immediately — only needs deckEntries, independent of Scryfall lookups.
var comboStopwatch = Stopwatch.StartNew();
var comboTask = AnalysisQuestionCatalog.RequiresComboLookup(selectedQuestions)
    ? _commanderSpellbookService.FindCombosAsync(deckEntries, cancellationToken)
    : Task.FromResult<CommanderSpellbookResult?>(null);
```
KB service call follows same pattern: flag-gated optional async call, null result = no injection.

**BuildAnalysisPrompt call site** (line 605) — where `kbExcerpts` threads through:
```csharp
analysisPromptText = BuildAnalysisPrompt(request, analysisDecklistText, referenceText,
    deckProfileSchemaJson, commanderName, selectedQuestions, bannedCards, comboResult,
    includeCardVersions);
// Phase 30: add kbExcerpts as final argument:
analysisPromptText = BuildAnalysisPrompt(request, analysisDecklistText, referenceText,
    deckProfileSchemaJson, commanderName, selectedQuestions, bannedCards, comboResult,
    includeCardVersions, kbExcerpts);
```

**DeckAnalysisPacketResult record** (lines 43-56) — add `ExpertContextClips` as last optional parameter:
```csharp
public sealed record DeckAnalysisPacketResult(
    string InputSummary,
    string SuggestedChatTitle,
    string DeckProfileSchemaJson,
    string? ReferenceText,
    string? AnalysisPromptText,
    string? SetUpgradePromptText,
    string? RequestContextText,
    string? TimingSummary,
    DeckAnalysisResponse? AnalysisResponse = null,
    SetUpgradeResponse? SetUpgradeResponse = null,
    string? ImportWarning = null,
    string? ResolvedCommanderName = null,
    string? DecklistText = null,
    IReadOnlyList<ContentKbExcerpt>? ExpertContextClips = null); // Phase 30 — LAST
```

---

### `DeckFlow.Web/Services/PacketArtifactStore.cs` (utility, file-I/O) — MODIFIED

**Analog:** self (extend in place)

**PacketAllowedNames HashSet** (lines 27-41) — add new entry:
```csharp
private static readonly HashSet<string> PacketAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-input-summary.txt",
    "01-request-context.txt",
    "10-deck-list.txt",
    "10b-deck-original.txt",
    "30-reference.txt",
    "31-analysis-prompt.txt",
    "40-deck-profile.json",
    "41-deck-profile-schema.json",
    "50-set-upgrade-prompt.txt",
    "51-set-upgrade-response.json",
    "all-prompts.txt",
    "all-responses.txt",
    "32-expert-context.json"  // Phase 30 — MUST be in same commit as BuildZip parameter
};
```

**CRITICAL — throw behavior** (lines 598-601 — verified):
```csharp
// ReadEntries THROWS (not silent drop) on unknown entry names.
// "32-expert-context.json" MUST be in PacketAllowedNames before ANY zip is built containing it.
if (!allowedNames.Contains(entry.FullName))
{
    throw new InvalidOperationException($"Imported zip contains an unsupported entry: {entry.FullName}");
}
```

**BuildZip signature extension pattern** (lines 93-103) — add optional `string? expertContextJson` as final parameter:
```csharp
public static byte[] BuildZip(
    DeckAnalysisRequest request,
    string? commanderName,
    string inputSummary,
    string? requestContextText,
    string? referenceText,
    string? analysisPromptText,
    string deckProfileSchemaJson,
    string? setUpgradePromptText,
    string? canonicalDeckListText = null,
    string? originalDeckText = null,
    string? expertContextJson = null)  // Phase 30 — LAST optional
```

**NormalizeSections tuple pattern** (lines 107-126) — add entry for new artifact:
```csharp
// Add to the promptSections array:
("32-expert-context.json", "EXPERT CONTEXT JSON", expertContextJson),
```

**LoadFromZip read pattern** (lines 207-212) — add entry read after existing reads:
```csharp
entries.TryGetValue("32-expert-context.json", out var expertContextJson);
// Deserialize into IReadOnlyList<ContentKbExcerpt> and store on request or pass to result.
```

---

### `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` (model, transform) — MODIFIED

**Analog:** self (extend in place)

**Existing pattern** (lines 60-72):
```csharp
/// <summary>
/// Gets the parsed deck-analysis JSON response from the AI, when available.
/// </summary>
public DeckAnalysisResponse? AnalysisResponse { get; init; }

/// <summary>
/// Gets the parsed set-upgrade JSON response from the AI, when available.
/// </summary>
public SetUpgradeResponse? SetUpgradeResponse { get; init; }

/// <summary>
/// Gets a warning surfaced when the user's deck import succeeded but with caveats worth flagging.
/// </summary>
public string? ImportWarning { get; init; }
```
Add after `ImportWarning`:
```csharp
/// <summary>
/// Gets the curated expert-context clips injected into the analysis prompt, or
/// <see langword="null"/> when the content.kb.enabled flag is off or no clips matched.
/// </summary>
public IReadOnlyList<ContentKbExcerpt>? ExpertContextClips { get; init; }
```

---

### `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` (component, request-response) — NEW

**Analog:** `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` collapsed `<details>` block (lines 217-247)

**Collapsed optional panel pattern** (lines 217-247 of `DeckAnalysis.cshtml`):
```cshtml
<details class="result-panel nested-panel">
    <summary>Analysis context</summary>
    <div class="chatgpt-context-note">
        <p>These fields do not change the deck parser. ...</p>
    </div>
    ...
</details>
```

**_ContentKbPanel.cshtml structure** — follow the same `<details>` collapsed-section pattern; hide panel entirely when model is null/empty; layout CSS in `site-common.css` only:
```cshtml
@model IReadOnlyList<DeckFlow.Web.Models.ContentKbExcerpt>?
@* "What Experts Say" panel — KBI-04/05.
   Hidden entirely when Model is null or empty (flag OFF or no clips matched). *@
@if (Model is { Count: > 0 })
{
    <details class="result-panel nested-panel kb-expert-panel">
        <summary>What Experts Say</summary>
        <div class="kb-expert-clips">
            @foreach (var clip in Model)
            {
                <article class="kb-expert-clip">
                    <blockquote>@clip.Excerpt</blockquote>
                    <footer>
                        — @clip.Source,
                        <a href="@clip.VideoUrl" target="_blank" rel="noopener noreferrer">@clip.Title</a>
                        [@clip.TimestampLabel] |
                        Harvested @clip.HarvestDate.UtcDateTime.ToString("yyyy-MM-dd")
                    </footer>
                </article>
            }
        </div>
    </details>
}
```

**Invocation pattern** in `DeckAnalysis.cshtml` (copy from line 35 partial pattern):
```cshtml
@await Html.PartialAsync("_ContentKbPanel", Model.ExpertContextClips)
```

**CSS:** Add `.kb-expert-panel`, `.kb-expert-clips`, `.kb-expert-clip` selectors to `DeckFlow.Web/wwwroot/css/site-common.css` — never to `site.css` or any guild theme file.

---

### `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` (controller, CRUD) — MODIFIED

**Analog:** self (extend in place)

**Constructor extension pattern** (lines 32-46 of `AdminContentKbController.cs`):
```csharp
// Existing ctor: store, seedLoader, flagCache, logger.
// Phase 30: add IContentKbRelevanceService as 5th parameter (after flagCache, before logger):
public AdminContentKbController(
    IContentSiteIndexStore store,
    IContentKbSeedLoader seedLoader,
    IFeatureFlagCache flagCache,
    IContentKbRelevanceService relevanceService,  // Phase 30
    ILogger<AdminContentKbController> logger)
{
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(seedLoader);
    ArgumentNullException.ThrowIfNull(flagCache);
    ArgumentNullException.ThrowIfNull(relevanceService);  // Phase 30
    ArgumentNullException.ThrowIfNull(logger);
    _store = store;
    _seedLoader = seedLoader;
    _flagCache = flagCache;
    _relevanceService = relevanceService;           // Phase 30
    _logger = logger;
}
```

**GET with query params pattern** (Index action, lines 54-96) — extend to accept preview params:
```csharp
[HttpGet("")]
[HttpGet("Index")]
public async Task<IActionResult> Index(
    string? previewCommander = null,
    string? previewBracket = null,
    CancellationToken cancellationToken = default)
{
    // ... existing rows/sources/status build ...
    IReadOnlyList<(ContentSiteIndexRow Row, double Score)>? previewScores = null;
    if (!string.IsNullOrWhiteSpace(previewCommander) || !string.IsNullOrWhiteSpace(previewBracket))
    {
        previewScores = await _relevanceService
            .ScoreAllAsync(previewCommander, previewBracket, cancellationToken)
            .ConfigureAwait(false);
    }
    // pass previewScores into view model
}
```

**Same-origin guard pattern** (lines 108-111, 128-131 — every mutating POST):
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
}
```
Preview is a GET (no mutation) — no CSRF guard needed on the preview read path.

---

### `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` (component, CRUD) — MODIFIED

**Analog:** self (extend in place)

**Existing table header pattern** (lines 87-94):
```cshtml
<thead>
    <tr>
        <th scope="col">Title</th>
        <th scope="col">Source</th>
        <th scope="col">Tags</th>
        <th scope="col">Status</th>
        <th scope="col">Action</th>
    </tr>
</thead>
```
Add `<th scope="col">Score</th>` after Status when a preview is active.

**Existing table row pattern** (lines 96-130):
```cshtml
@foreach (var entry in Model.Entries)
{
    <tr>
        <td data-label="Title" class="admin-kb-title">@entry.Title</td>
        ...
    </tr>
}
```
Add `<td data-label="Score">@(scoreForEntry?.ToString("F2") ?? "—")</td>` after the Status cell.

**Preview form pattern** (modeled after existing `BulkSetVisibility` form, lines 61-76):
```cshtml
<form method="get" class="admin-action-form admin-preview-form">
    <label>
        Commander
        <input type="text" name="previewCommander" value="@Model.PreviewCommander" />
    </label>
    <label>
        Bracket
        <select name="previewBracket">
            <option value="">(any)</option>
            @foreach (var b in Model.BracketOptions)
            {
                <option value="@b" selected="@(b == Model.PreviewBracket)">@b</option>
            }
        </select>
    </label>
    <button type="submit">Preview Scores</button>
</form>
```

---

## Shared Patterns

### Feature Flag Per-Request Check
**Source:** `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` + `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` line 84
**Apply to:** `ContentKbRelevanceService.GetRelevantClipsAsync` (first statement), `DeckAnalysis.cshtml` panel display guard
```csharp
// Per-request call — never check at DI construction time.
// Returns true (default-on) when key is missing from snapshot.
bool enabled = _flagCache.IsEnabled("content.kb.enabled");
```

### ArgumentNullException.ThrowIfNull Constructor Guards
**Source:** `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` lines 38-44
**Apply to:** `ContentKbRelevanceService` constructor, any new controller constructor
```csharp
ArgumentNullException.ThrowIfNull(store);
ArgumentNullException.ThrowIfNull(seedLoader);
ArgumentNullException.ThrowIfNull(flagCache);
ArgumentNullException.ThrowIfNull(logger);
```

### NullLogger Fallback for Optional Logger
**Source:** `DeckFlow.Web/Services/CommanderSpellbookService.cs` line 80
**Apply to:** `ContentKbRelevanceService` constructor
```csharp
_logger = logger ?? NullLogger<CommanderSpellbookService>.Instance;
```

### Same-Origin Guard on Mutating POSTs
**Source:** `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` lines 108-111
**Apply to:** any new `[HttpPost]` action in `AdminContentKbController`
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
}
```

### { get; init; } on All Serialized Record Properties
**Source:** `DeckFlow.Web/Models/AdminContentKbViewModel.cs` lines 31-70
**Apply to:** `ContentKbExcerpt.cs` — every property
```csharp
// NEVER use { get; } on properties that must round-trip through System.Text.Json.
// System.Text.Json silently skips get-only properties in .NET 9+.
public required string Source { get; init; }   // correct
// public required string Source { get; }      // WRONG — breaks deserialization
```

### Graceful Null/Empty Return (No Exception for No-Data)
**Source:** `DeckFlow.Web/Services/CommanderSpellbookService.cs` — `FindCombosAsync` returns `null` on API failure
**Apply to:** `ContentKbRelevanceService.GetRelevantClipsAsync` (null when flag off OR no match)
```csharp
// Services return null/empty on no-match; never throw for "no data" case.
// Callers guard with ?. or null-check before injecting into prompt.
```

### PacketAllowedNames Allowlist + Same-Commit Rule
**Source:** `DeckFlow.Web/Services/PacketArtifactStore.cs` lines 27-41, 598-601
**Apply to:** `PacketArtifactStore.cs` Phase 30 extension
```csharp
// ReadEntries THROWS (not silent drop) on unknown zip entries.
// "32-expert-context.json" MUST be added to PacketAllowedNames in the SAME commit
// as the BuildZip parameter that writes it. Never add the parameter first.
```

---

## Test Patterns

### xUnit + FakeFeatureFlagCache + FakeContentSiteIndexStore
**Source:** `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs` + `TestDoubles/FakeFeatureFlagCache.cs` + `TestDoubles/FakeContentSiteIndexStore.cs`

**Test class structure:**
```csharp
// DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ContentKbRelevanceServiceTests
{
    [Fact]
    public async Task GetRelevantClipsAsync_FlagOff_ReturnsNull()
    {
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
            { ["content.kb.enabled"] = false });
        // ...
        var result = await service.GetRelevantClipsAsync("Atraxa", "cEDH");
        Assert.Null(result);
    }
}
```

**Fake* test double pattern** (from `FakeFeatureFlagCache.cs` lines 13-29):
```csharp
// internal sealed class FakeXxx : IXxx — matches interface, minimal state for assertions.
// Stateful mutation (Flags dict) is public so tests can set up scenarios.
internal sealed class FakeFeatureFlagCache : IFeatureFlagCache
{
    public Dictionary<string, bool> Flags { get; }
    public FakeFeatureFlagCache(IDictionary<string, bool>? initial = null) { ... }
    public bool IsEnabled(string key) => !Flags.TryGetValue(key, out var enabled) || enabled;
    // default-on contract: missing key => true
}
```

**Round-trip zip test pattern** (`DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` lines 14-40):
```csharp
[Fact]
public void BuildZip_then_LoadFromZip_round_trips_response_json()
{
    var request = new DeckAnalysisRequest { DeckProfileJson = "..." };
    var bytes = PacketArtifactStore.BuildZip(request, commanderName: "Atraxa",
        inputSummary: "summary", requestContextText: "context", ...);
    var loaded = new DeckAnalysisRequest();
    using var memoryStream = new MemoryStream(bytes);
    PacketArtifactStore.LoadFromZip(memoryStream, loaded);
    Assert.Contains("deck_profile", loaded.DeckProfileJson);
}
// New Phase 30 test: BuildZip_with_expert_context_round_trips_clips
// — build zip with expertContextJson = serialized clips; load; assert all clip fields present.
```

**AdminContentKbController test structure** (lines 130-152):
```csharp
private static AdminContentKbController Build(
    FakeContentSiteIndexStore store,
    FakeContentKbSeedLoader loader,
    out FakeContentKbSeedLoader loaderOut,
    bool crossOrigin)
{
    loaderOut = loader;
    var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        { ["content.kb.enabled"] = false });
    var controller = new AdminContentKbController(store, loader, flagCache,
        NullLogger<AdminContentKbController>.Instance);

    var httpContext = new DefaultHttpContext();
    httpContext.Request.Scheme = "https";
    httpContext.Request.Host = new HostString("deckflow.test");
    httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

    controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
    return controller;
}
```
Phase 30 tests for `AdminContentKbController` extend `Build` to inject a fake `IContentKbRelevanceService`.

### ContentArtifactParser.SplitHeader Extension
**Source:** `DeckFlow.Web/Services/ContentArtifactParser.cs` (full file — 47 lines)

**Existing SplitHeader pattern** (lines 13-46):
```csharp
// SplitHeader: parses flat key: value front matter only.
// Returns (empty dict, raw) when --- delimiter is missing — graceful.
// Use this to get the body; get tags from ContentSiteIndexRow.ArchetypeTags/BracketTags.
// DO NOT re-parse front matter for tag data — SplitHeader cannot parse nested YAML.
public static (IReadOnlyDictionary<string, string> Header, string Body) SplitHeader(string raw)
{
    ArgumentNullException.ThrowIfNull(raw);
    // ... finds second --- delimiter; joins lines[end+1..] as Body
    var body = string.Join('\n', lines.Skip(end + 1));
    return (header, body);
}
```

**Key Clips parser builds on Body** — locate `## Key Clips`, read until `## Tags` or end:
```csharp
// Clip bullet format (verified from 10 live artifacts):
// "- **[02:14]** excerpt text here"
private static readonly Regex ClipBulletRegex = new(
    @"^\s*-\s*\*\*\[(?<ts>[^\]]+)\]\*\*\s*(?<text>.+)$",
    RegexOptions.Compiled | RegexOptions.Multiline);
```

---

## No Analog Found

All files have close analogs in the existing codebase. No new architectural patterns required.

---

## Metadata

**Analog search scope:** `DeckFlow.Web/Services/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Controllers/Admin/`, `DeckFlow.Web/Views/Deck/`, `DeckFlow.Web/Views/AdminContentKb/`, `DeckFlow.Web.Tests/`, `DeckFlow.Web.Tests/TestDoubles/`
**Files scanned:** 18 source files read directly; pattern confirms match for all 12 target files
**Pattern extraction date:** 2026-06-05
