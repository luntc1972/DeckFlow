# Phase 31: Deck Primer Generator — Pattern Map

**Mapped:** 2026-06-08
**Files analyzed:** 18 new/modified files
**Analogs found:** 18 / 18

---

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Services/DeckPrimerPacketService.cs` | service | request-response | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Primer/IPrimerPromptVariant.cs` | service (interface) | request-response | `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Primer/PrimerPromptVariantRegistry.cs` | service | request-response | `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs` | service | request-response | `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Primer/ClaudePrimerPromptVariant.cs` | service | request-response | `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs` | service | request-response | `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Models/PrimerSectionCatalog.cs` | model | transform | `DeckFlow.Web/Models/AnalysisQuestionCatalog.cs` | exact |
| `DeckFlow.Web/Models/DeckPrimerRequest.cs` | model | request-response | `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | exact |
| `DeckFlow.Web/Models/DeckPrimerViewModel.cs` | model | request-response | `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | exact |
| `DeckFlow.Web/Services/PacketArtifactStore.cs` (modified) | service (utility) | file-I/O | `DeckFlow.Web/Services/PacketArtifactStore.cs` — PrimerAllowedNames block | exact |
| `DeckFlow.Web/Models/DeckPageTab.cs` (modified) | model (enum) | N/A | `DeckFlow.Web/Models/DeckPageTab.cs` | exact |
| `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` (modified) | view (partial) | request-response | `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` | exact |
| `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` | view | request-response | `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | exact |
| `DeckFlow.Web/Controllers/DeckController.cs` (modified) | controller | request-response | `DeckFlow.Web/Controllers/DeckController.cs` — DeckAnalysis action group | exact |
| `DeckFlow.Web/wwwroot/ts/primer-selection.ts` | utility (TS) | event-driven | `DeckFlow.Web/wwwroot/ts/kb-selection.ts` | role-match |
| `DeckFlow.Web/Program.cs` (modified) | config | N/A | `DeckFlow.Web/Program.cs` lines 289-326 | exact |
| `DeckFlow.Web.Tests/PacketArtifactStorePrimerTests.cs` | test | CRUD | `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` | exact |
| `DeckFlow.Web.Tests/DeckPrimerRequestTests.cs` (or similar) | test | transform | `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` round-trip block | role-match |

---

## Pattern Assignments

### `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (service, request-response)

**Analog:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`

**Imports pattern** (lines 1-17):
```csharp
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Services.Http;
using Polly;
using Polly.Registry;
using RestSharp;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Primer;
```

**Interface + result record pattern** (lines 23-58):
```csharp
public interface IDeckPrimerPacketService
{
    Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request, CancellationToken cancellationToken = default);
    Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request, CancellationToken cancellationToken);
}

// Why: { get; init; } on every positional property is mandatory — System.Text.Json
// silently skips get-only properties in .NET 9+ (has broken EdhTop16Client deserialization before).
public sealed record DeckPrimerPacketResult(
    string InputSummary,
    string SuggestedChatTitle,
    string? PrimerPromptText,
    string? TimingSummary,
    string? ImportWarning = null,
    string? ResolvedCommanderName = null,
    string? DecklistText = null);
```

**Class declaration pattern** (line 63):
```csharp
public sealed partial class DeckPrimerPacketService : IDeckPrimerPacketService
```

**Constructor pattern — internal test seam + production ctor** (lines 88-191):
- Expose `internal DeckPrimerPacketService(... PrimerPromptVariantRegistry primerPromptRegistry, PacketSessionCache packetCache, ILogger<DeckPrimerPacketService>? logger = null, ...)` as the primary ctor with `ArgumentNullException.ThrowIfNull(...)` for every non-optional parameter.
- The logger parameter is optional and defaults to `NullLogger<DeckPrimerPacketService>.Instance` (line 177 pattern).
- Resolve Polly pipeline: `var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;` (line 164).
- `_executeCollectionAsync` delegate wired through `ScryfallThrottle.ExecuteAsync` (lines 179-190) — copy exactly if Scryfall card lookup is needed for the primer. If the primer does not need Scryfall hydration (relies on pre-parsed deck text only), omit those delegates but keep the ctor seam shape.

**BuildAsync top-level structure** (lines 391-783):
- `ArgumentNullException.ThrowIfNull(request)` — first line.
- `Stopwatch.StartNew()` + `List<(string Label, long Ms, string? Detail)> timings` — copy timing infrastructure verbatim.
- Cache-replay guard: call `TryComputeCacheKeyAsync`, then `_packetCache.TryGet<DeckPrimerPacketResult>(cacheKey, out var cached)` before running the full pipeline. Write back to cache at the end with `_packetCache.Set(cacheKey, result, PacketSizeEstimator.EstimateSizeBytes(result))`.
- `LoadDeckEntriesAsync` private helper (lines 799-833) — reuse pattern exactly: URI → Moxfield/Archidekt branch → MoxfieldParser → ArchidektParser → throw `InvalidOperationException`.
- `_lastImportNotice` mutable field pattern (line 791) — needed if Moxfield fallback notice surfaces.

**Null-Spellbook disclosure (D-2)** — modeled on combo result handling at lines 682-691:
```csharp
// Why: D-2 — when Spellbook returns null, emit explicit disclosure rather than
// silently omitting the combo block. Never treat null as a hard failure.
var comboResult = await _commanderSpellbookService.FindCombosAsync(deckEntries, cancellationToken)
    .ConfigureAwait(false);
// Pass comboResult (may be null) to the variant Build() — variant handles disclosure inline.
```

**Gotchas:**
- Do NOT call `ScryfallThrottle.ExecuteAsync` for non-Scryfall upstream calls (Spellbook, EdhTop16). Only Scryfall lookups route through the throttle gate.
- `PacketSessionCache` is injected as a dependency (not `new`-ed) — registered as Singleton in Program.cs, shared across all scoped services.
- `DeckAnalysisPacketService` is registered `AddScoped`, not `AddSingleton` — follow the same lifetime for `DeckPrimerPacketService` because it holds request-scoped state (`_lastImportNotice`).
- Namespace: `namespace DeckFlow.Web.Services;` (file-scoped).

---

### `DeckFlow.Web/Services/PromptBuilders/Primer/IPrimerPromptVariant.cs` (interface)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` (lines 1-38)

**Full pattern:**
```csharp
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Strategy interface for building a deck-primer prompt body targeting a specific AI platform.
/// </summary>
internal interface IPrimerPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the primer prompt text for the given request and pre-assembled data blocks.
    /// </summary>
    string Build(
        DeckPrimerRequest request,
        string decklistText,
        IReadOnlyList<PrimerSectionEntry> selectedSections,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        CategoryDistributionSummary? categoryDistribution,
        int bracketNumber,
        CancellationToken cancellationToken = default);
}
```

**Conventions to copy:**
- `internal interface` — not `public` (scoped to the assembly via DI enumeration, not called externally).
- Namespace mirrors folder: `DeckFlow.Web.Services.PromptBuilders.Primer`.
- XML doc on interface and every method parameter.
- No default implementations — each variant is standalone.

---

### `DeckFlow.Web/Services/PromptBuilders/Primer/PrimerPromptVariantRegistry.cs` (service)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` (lines 1-49)

**Full pattern:**
```csharp
namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Dispatches primer prompt construction to the registered <see cref="IPrimerPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/>
/// when an unrecognised platform is supplied.
/// </summary>
internal sealed class PrimerPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IPrimerPromptVariant> _variants;

    public PrimerPromptVariantRegistry(IEnumerable<IPrimerPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    public string Build(AiPlatform platform, DeckPrimerRequest request, ...)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(request, ...);
    }
}
```

**Conventions:**
- `internal sealed class` — same visibility as AnalysisPromptVariantRegistry.
- DI ctor takes `IEnumerable<IPrimerPromptVariant>` — all three variants auto-injected by `AddSingleton<IPrimerPromptVariant, ...>` registrations, then collected by `AddSingleton<PrimerPromptVariantRegistry>`.
- `AiPlatform.Default` fallback is defense-in-depth — `AiPlatform.Normalize` at the call site prevents unknown values from arriving here.

---

### `DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs` (service)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` (lines 1-60)

**Imports + class header pattern** (lines 1-18):
```csharp
using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

// Helpers used: NormalizeSingleLine [internal on DeckPrimerPacketService],
// BuildComboReferenceText [internal on DeckPrimerPacketService].
// CommanderBracketCatalog, PrimerSectionCatalog are public statics.

/// <summary>
/// Builds a deck-primer prompt body formatted for ChatGPT (markdown-headed, fenced JSON output).
/// </summary>
internal sealed class ChatGptPrimerPromptVariant : IPrimerPromptVariant
{
    public AiPlatform Platform => AiPlatform.ChatGpt;

    public string Build(DeckPrimerRequest request, ...) { ... }
}
```

**Combo injection pattern** — copy the fenced-block structure from `DeckAnalysisPacketService.BuildComboReferenceText` (lines 986-1029) but split into TWO structurally separated blocks per D-2:
```csharp
// Block 1: ground truth
builder.AppendLine("## Known Combos (ground truth — do not speculate)");
if (comboResult is null || comboResult.IncludedCombos.Count == 0)
{
    builder.AppendLine("No verified combos available — treat all synergies as speculative.");
}
else
{
    // emit combo lines ranked per D-1 spike verdict
}

// Block 2: speculative ask — always present, structurally separate from block 1
builder.AppendLine("## Speculative Synergies (you propose)");
builder.AppendLine("Based on the cards above, identify likely synergies that are NOT in the ground-truth block...");
```

**Conventions:**
- `internal sealed class` — no `public`.
- `AiPlatform.Platform => AiPlatform.ChatGpt` property (not a field).
- Comment at top citing which helpers are promoted to `internal` on the service (mirrors the pattern in existing variants).
- Decoupled from all other variants — no shared prose, no shared base class. ADR 0001 invariant.

---

### `DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs` (service)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` (lines 1-80)

**Defensive char-cap pattern** (line 17 — copy exactly):
```csharp
// Why: D-4 — defensive trim guard mirrors GeminiAnalysisPromptVariant.DefensivePromptCharCap.
// Exact threshold set by PRM-01 spike byte-size measurement; update after spike records verdict.
private const int DefensivePromptCharCap = 50000; // placeholder — replace with spike result

public AiPlatform Platform => AiPlatform.Gemini;
```

**Trim-to-fit pattern** — after building the full prompt string, apply:
```csharp
if (builder.Length > DefensivePromptCharCap)
{
    // Trim lowest-priority sections (trailing groups) until under cap.
    // Append disclosure: "Note: prompt trimmed to fit Gemini paste limit — N sections omitted."
    // Why: D-4 — never a hard disable; ChatGPT/Claude unaffected. Mirror GeminiAnalysisPromptVariant.
}
```

**Gotcha:** The Gemini variant MUST include the persona-scaffold header (`"You are an expert Magic: The Gathering analyst..."`) and `"Think carefully through the problem before responding."` — those are Gemini-specific; do not copy ChatGPT's markdown-only header. Check `GeminiAnalysisPromptVariant.cs` lines 49-52 for the exact persona text.

---

### `DeckFlow.Web/Services/PromptBuilders/Primer/ClaudePrimerPromptVariant.cs` (service)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs`

**Conventions:**
- Claude variant uses XML-tagged structure (`<deck_primer>...</deck_primer>` output wrapper) — check `ClaudeAnalysisPromptVariant.cs` for the exact tag names used in the analysis domain, then use equivalent primer tags.
- No `DefensivePromptCharCap` — Claude variant is unaffected by D-4.
- No `<result>` wrapper (was stripped in Phase 999.5-04 — see commit note at line 798 memory entry). Claude variant uses `<primer_output>` or equivalent, not `<result>`.
- MDFC-land guidance intentionally differs from ChatGPT/Gemini variants (the harmonization revert at memory entry 3186 is deliberate — do not try to unify).

---

### `DeckFlow.Web/Models/PrimerSectionCatalog.cs` (model, transform)

**Analog:** `DeckFlow.Web/Models/AnalysisQuestionCatalog.cs` (lines 1-287)

**Record types pattern** (lines 6-21):
```csharp
namespace DeckFlow.Web.Models;

/// <summary>Represents a single selectable primer section.</summary>
/// <param name="Id">Stable section identifier posted by the workflow form.</param>
/// <param name="Number">Section number (1–31) shown in the UI.</param>
/// <param name="Title">Display title shown to the user.</param>
/// <param name="HelpText">Explains what good AI output for this section looks like (PRM-12).</param>
/// <param name="Group">Group this section belongs to (one of the 5 collapsible groups).</param>
/// <param name="BracketGate">Null = available in all brackets; "cedh-only" = bracket 5 only;
///     "casual-only" = brackets 1–4 only.</param>
public sealed record PrimerSectionEntry(
    string Id,
    int Number,
    string Title,
    string HelpText,
    string Group,
    string? BracketGate = null);

/// <summary>Groups related primer sections under a shared collapsible heading.</summary>
/// <param name="Id">Stable group identifier.</param>
/// <param name="Label">Display label for the group.</param>
/// <param name="Sections">Sections included in the group.</param>
public sealed record PrimerSectionGroup(
    string Id,
    string Label,
    IReadOnlyList<PrimerSectionEntry> Sections);
```

**Static catalog class pattern** (lines 26-130):
```csharp
/// <summary>Provides the 31 primer sections, 5 collapsible groups, and bracket-preset helpers.</summary>
public static class PrimerSectionCatalog
{
    public static IReadOnlyList<PrimerSectionGroup> Groups { get; } = [ ... ];

    // Bracket-scoped gate sets (PRM-03)
    public static IReadOnlySet<string> CedhOnlySectionIds { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "section-24-id", "section-25-id" };

    public static IReadOnlySet<string> CasualOnlySectionIds { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "section-26-id" };

    // Preset helpers — return default-selected section IDs per bracket (PRM-03)
    public static IReadOnlyList<string> GetPresetForBracket(int bracketNumber) { ... }

    // Normalize submitted IDs — same pattern as AnalysisQuestionCatalog.NormalizeSelections
    public static IReadOnlyList<string> NormalizeSelections(IEnumerable<string>? selections) { ... }

    // Flatten for iteration
    public static IReadOnlyList<PrimerSectionEntry> AllSections { get; } =
        Groups.SelectMany(g => g.Sections).ToList();
}
```

**Conventions to copy:**
- `public static class` with `IReadOnlyList<T>` properties — same as `AnalysisQuestionCatalog`.
- `IReadOnlySet<string>` with `StringComparer.OrdinalIgnoreCase` for gated ID sets.
- `NormalizeSelections` validates, deduplicates, and sorts — copy lines 178-190 pattern exactly.
- `public static class` means no DI registration — consumed directly from views and the service.
- File-scoped namespace: `namespace DeckFlow.Web.Models;`.

---

### `DeckFlow.Web/Models/DeckPrimerRequest.cs` (model, request-response)

**Analog:** `DeckFlow.Web/Models/DeckAnalysisRequest.cs` (lines 1-306)

**Critical `{ get; init; }` guard:** `DeckAnalysisRequest` uses mutable setters with null-guard backing fields, NOT `init`. The request DTO is form-bound and mutated across the workflow — this is intentional. Copy the mutable-setter pattern with null-guard backing fields:
```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the deck-primer page.
/// </summary>
public sealed class DeckPrimerRequest
{
    private string _deckText = string.Empty;
    private string _deckUrl = string.Empty;
    private string _format = "Commander";
    private string _deckName = string.Empty;
    private string _targetCommanderBracket = string.Empty;
    private string _targetAiPlatform = "ChatGPT";
    private List<string> _selectedSectionIds = [];

    // DeckSource pattern — copied from DeckAnalysisRequest lines 59-74
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PasteText;
    public string DeckUrl { get => _deckUrl; set => _deckUrl = value ?? string.Empty; }
    public string DeckText { get => _deckText; set => _deckText = value ?? string.Empty; }
    public string DeckSource
    {
        get => DeckInputSource == DeckInputSource.PublicUrl ? _deckUrl : _deckText;
        set { /* route to DeckUrl or DeckText per mode */ }
    }

    public int WorkflowStep { get; set; } = 1;
    public string Format { get => _format; set => _format = value ?? "Commander"; }
    public string DeckName { get => _deckName; set => _deckName = value ?? string.Empty; }

    // Bracket: "Exhibition"|"Core"|"Upgraded"|"Optimized"|"cEDH" — matches CommanderBracketCatalog.Value
    public string TargetCommanderBracket { get => _targetCommanderBracket; set => _targetCommanderBracket = value ?? string.Empty; }

    // TargetAiPlatform uses AiPlatform.Normalize in setter — copy line 181 exactly
    public string TargetAiPlatform
    {
        get => _targetAiPlatform;
        set => _targetAiPlatform = AiPlatform.Normalize(value).Key;
    }

    public List<string> SelectedSectionIds { get => _selectedSectionIds; set => _selectedSectionIds = value ?? []; }
}
```

**Gotchas:**
- The `{ get; init; }` rule in CONTEXT.md applies to **result DTOs and round-tripped records** — the request DTO is form-bound and must use mutable setters to survive model binding and round-trip across workflow steps.
- `TargetAiPlatform` setter MUST call `AiPlatform.Normalize(value).Key` — this is the Phase 10 hardening contract (line 181 in DeckAnalysisRequest.cs). Prevents crafted form posts from leaving an unknown platform string.
- Null-guard pattern: every string property setter does `value ?? string.Empty`, every List setter does `value ?? []`.

---

### `DeckFlow.Web/Models/DeckPrimerViewModel.cs` (model, request-response)

**Analog:** `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` (lines 1-82)

**Pattern:**
```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Razor view model for the deck-primer page.
/// </summary>
public sealed class DeckPrimerViewModel
{
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.DeckPrimer;
    public DeckPrimerRequest Request { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public string? InputSummary { get; init; }
    public string? SuggestedChatTitle { get; init; }
    public string? PrimerPromptText { get; init; }
    public string? TimingSummary { get; init; }
    public string? ImportWarning { get; init; }
}
```

**Conventions:**
- `public sealed class` (NOT a record — view models are class, not record, per existing pattern).
- Every property `{ get; init; }` — view model is constructed once by the controller action and never mutated.
- `ActiveTab` defaults to the new `DeckPageTab.DeckPrimer` value.
- Namespace: `namespace DeckFlow.Web.Models;`.

---

### `DeckFlow.Web/Services/PacketArtifactStore.cs` (modified — add `PrimerAllowedNames`)

**Analog:** `DeckFlow.Web/Services/PacketArtifactStore.cs` lines 37-82 (three existing `HashSet<string>` allowlists)

**Pattern to add** (insert after `CedhAllowedNames` block, before the first `public static` method):
```csharp
private static readonly HashSet<string> PrimerAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-primer-input-summary.txt",
    "01-primer-request-context.txt",
    "10-primer-deck-list.txt",
    "10b-primer-deck-original.txt",
    "30-primer-chatgpt-prompt.txt",
    "30-primer-claude-prompt.txt",
    "30-primer-gemini-prompt.txt",
    "all-primer-prompts.txt"
};
```

**Add `BuildPrimerZip` and `LoadPrimerFromZip` methods** — mirror `BuildZip` / `LoadFromZip` (lines 105-348):
- `BuildPrimerZip(DeckPrimerRequest request, string inputSummary, string? requestContextText, string? chatGptPromptText, string? claudePromptText, string? geminiPromptText, string? canonicalDeckListText = null, string? originalDeckText = null)` 
- `LoadPrimerFromZip(Stream zipStream, DeckPrimerRequest request)` — reads against `PrimerAllowedNames`.

**`SuggestPrimerZipFileName`** — mirror pattern at line 570:
```csharp
public static string SuggestPrimerZipFileName(string? commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "deck-primer")}-primer-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
```

**Why `PrimerAllowedNames` must be added FIRST (per CONTEXT.md):** `ReadEntries` (line 625) throws `InvalidOperationException` on any entry name not in the passed allowlist. A primer zip written by `BuildPrimerZip` and immediately re-uploaded to `LoadPrimerFromZip` will fail if `PrimerAllowedNames` does not exist yet at the read side.

**Gotcha:** `PacketArtifactStore` is `internal static` — all new methods follow the same `public static` signature. `CreateSafePathSegment` and `NormalizeSections` are already private static helpers shared by all `Build*Zip` methods — the new method reuses them without duplication.

---

### `DeckFlow.Web/Models/DeckPageTab.cs` (modified)

**Analog:** `DeckFlow.Web/Models/DeckPageTab.cs` lines 6-40

**Add one enum value** (assign the next available integer — current max is 11):
```csharp
/// <summary>Deck-primer artifact generator page.</summary>
DeckPrimer = 12,
```

**Gotcha:** The existing enum has a gap at 6 (no value 6 defined) and skips 7 → Convert. Use 12 for DeckPrimer to stay out of any existing range. Verify no switch exhaustiveness analyzer will fire on existing switch expressions (the CLAUDE.md notes to preserve switch expressions — an `_ => ...` default arm already handles unknown values in consuming code).

---

### `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` (modified)

**Analog:** `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` lines 1-62

**Pattern to copy — add DeckPrimer link into the Analyze dropdown** (insert after line 20):
```cshtml
@{
    var analyzeActive = Model is DeckPageTab.DeckAnalysis or DeckPageTab.DeckComparison
        or DeckPageTab.CedhMetaGap or DeckPageTab.DeckPrimer;  // add DeckPrimer here
}
...
<a class="tool-nav__link @(Model == DeckPageTab.DeckPrimer ? "is-active" : string.Empty)"
   href="@Url.Content("~/deck-primer")">Deck Primer</a>
```

**Conventions:**
- The Analyze group already uses `is DeckPageTab.X or DeckPageTab.Y` pattern — extend it with `or DeckPageTab.DeckPrimer`.
- New link goes after the existing three Analyze links (DeckAnalysis, DeckComparison, CedhMetaGap) — Deck Primer is the 4th peer.
- No feature-flag gate needed unless the team decides to ship behind a flag — check with user before adding one.

---

### `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` (view)

**Analog:** `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (full file)

**View top pattern** (lines 1-47):
```cshtml
@model DeckFlow.Web.Models.DeckPrimerViewModel
@{
    ViewData["Title"] = "Deck Primer";
    var aiPlatform = AiPlatform.Normalize(Model.Request.TargetAiPlatform);
    var currentStep = Math.Clamp(Model.Request.WorkflowStep, 1, 3);
    var commanderBrackets = DeckFlow.Web.Models.CommanderBracketCatalog.Options;
    var sectionGroups = DeckFlow.Web.Models.PrimerSectionCatalog.Groups;
}

<section class="hero">
    <h1>Deck Primer</h1>
    <p class="page-lede">Generate a paste-ready deck primer prompt for ChatGPT, Claude, or Gemini.</p>
</section>

@await Html.PartialAsync("_BusyIndicator")
@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)
```

**WorkflowStepTabs pattern** (lines 120-134 in DeckAnalysis):
```cshtml
@{
    var packetTabs = new DeckFlow.Web.Models.WorkflowStepTabsModel(
        ariaLabel: "Deck primer steps",
        tabIdPrefix: "primer-step-tab",
        panelIdPrefix: "primer-step-panel",
        dataShowStepAttribute: "primer-step",
        steps:
        [
            new DeckFlow.Web.Models.WorkflowStepTab(1, "Step 1: Deck", !string.IsNullOrWhiteSpace(Model.Request.DeckSource)),
            new DeckFlow.Web.Models.WorkflowStepTab(2, "Step 2: Build Primer", !string.IsNullOrWhiteSpace(Model.PrimerPromptText)),
            new DeckFlow.Web.Models.WorkflowStepTab(3, "Step 3: Results", false)
        ]);
}
@await Html.PartialAsync("_WorkflowStepTabs", packetTabs)
```

**Section-group UI (PRM-04/11/12):** render as collapsible `<details>/<summary>` elements with badge counts. Each section gets a checkbox (id mapped to `SelectedSectionIds`) and a help-text toggle (PRM-12). Bracket-gated sections rendered with `disabled` + `aria-disabled="true"` when bracket doesn't match gate; the TS module (`primer-selection.ts`) enforces this client-side.

**PasteWarningBytes indicator** — copy the pattern from DeckAnalysis.cshtml lines 478-479:
```cshtml
@{
    var primerBytes = System.Text.Encoding.UTF8.GetByteCount(Model.PrimerPromptText ?? string.Empty);
    var pasteWarn = aiPlatform.PasteWarningBytes is int cap && primerBytes > cap;
}
```

**Download/upload actions:**
- `formaction="@Url.Content("~/deck-primer/download")"` — mirrors line 87 pattern.
- Upload: `data-upload-action="@Url.Content("~/deck-primer/upload")"` — mirrors line 106 pattern.
- The upload/download JS hooks (`data-chatgpt-download-submit`, `data-chatgpt-zip-upload`) are defined in the existing compiled JS — use the same `data-*` attributes (the TS compiles to `wwwroot/js/`).

---

### `DeckFlow.Web/Controllers/DeckController.cs` (modified)

**Analog:** `DeckFlow.Web/Controllers/DeckController.cs` — DeckAnalysis action group (lines 155-675)

**Constructor injection pattern** (lines 24-66):
- Add `IDeckPrimerPacketService _deckPrimerPacketService` field.
- Add `IDeckPrimerPacketService deckPrimerPacketService` parameter.
- Assign in ctor body.
- Keep `ArgumentNullException.ThrowIfNull` implicit via ASP.NET Core DI (not explicit in ctor — DI throws on null service).

**GET action pattern** (lines 158-166):
```csharp
/// <summary>
/// Renders the deck-primer artifact generator page.
/// </summary>
[HttpGet("/deck-primer")]
public IActionResult DeckPrimer()
{
    return View("DeckPrimer", new DeckPrimerViewModel
    {
        ActiveTab = DeckPageTab.DeckPrimer,
        Request = new DeckPrimerRequest(),
    });
}
```

**POST action pattern** (lines 461-508 in DeckAnalysis group):
```csharp
[HttpPost("/deck-primer")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeckPrimer(DeckPrimerRequest request)
{
    request ??= new DeckPrimerRequest();
    // Validation gates (mirror DeckAnalysis lines 604-628)
    try
    {
        var result = await _deckPrimerPacketService.BuildAsync(request, HttpContext.RequestAborted);
        return View("DeckPrimer", new DeckPrimerViewModel
        {
            ActiveTab = DeckPageTab.DeckPrimer,
            Request = request,
            InputSummary = result.InputSummary,
            SuggestedChatTitle = result.SuggestedChatTitle,
            PrimerPromptText = result.PrimerPromptText,
            TimingSummary = result.TimingSummary,
            ImportWarning = result.ImportWarning,
        });
    }
    catch (InvalidOperationException ex)
    {
        return View("DeckPrimer", new DeckPrimerViewModel
        {
            ActiveTab = DeckPageTab.DeckPrimer,
            Request = request,
            ErrorMessage = ex.Message,
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Deck primer build failed.");
        return View("DeckPrimer", new DeckPrimerViewModel
        {
            ActiveTab = DeckPageTab.DeckPrimer,
            Request = request,
            ErrorMessage = "An unexpected error occurred. Please try again.",
        });
    }
}
```

**Download action pattern** (lines 514-606): add `/deck-primer/download` POST mirroring `/deck-analysis/download`. Cache-hit path: `TryComputeCacheKeyAsync` → `_packetCache.TryGet<DeckPrimerPacketResult>` → `PacketArtifactStore.BuildPrimerZip` → `File(bytes, "application/zip", fileName)`.

**Upload action pattern** (lines 609-676): add `/deck-primer/upload` POST mirroring `/deck-analysis/upload`. Reads zip via `PacketArtifactStore.LoadPrimerFromZip`, returns view with restored state.

**Gotcha:** `DeckController` already has many actions — keep the primer action group (GET + POST + Download + Upload) together, placed after the existing CedhMetaGap group for organizational clarity.

---

### `DeckFlow.Web/wwwroot/ts/primer-selection.ts` (TypeScript, event-driven)

**Analog:** `DeckFlow.Web/wwwroot/ts/kb-selection.ts` (lines 1-728)

**IIFE wrapper pattern** (lines 1-3):
```typescript
((): void => {
  'use strict';
  // all logic inside IIFE — no module system, compiled to ES2017 global script
```

**localStorage key pattern** (lines 50-52):
```typescript
// One key per bracket — keyed by bracket value string from CommanderBracketCatalog
const PRIMER_SECTIONS_KEY_PREFIX = 'deckflow.primer.sections.';  // + bracketValue
```

**Load/save with try/catch pattern** (lines 92-134):
```typescript
const loadSectionsForBracket = (bracketValue: string): string[] => {
  try {
    const raw = window.localStorage.getItem(PRIMER_SECTIONS_KEY_PREFIX + bracketValue);
    if (!raw) { return []; }
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) { return []; }
    return parsed.filter((item): item is string => typeof item === 'string');
  } catch {
    return [];
  }
};

const saveSectionsForBracket = (bracketValue: string, ids: string[]): void => {
  try {
    if (ids.length === 0) {
      window.localStorage.removeItem(PRIMER_SECTIONS_KEY_PREFIX + bracketValue);
      return;
    }
    window.localStorage.setItem(PRIMER_SECTIONS_KEY_PREFIX + bracketValue, JSON.stringify(ids));
  } catch {
    return;
  }
};
```

**Hidden-field inject pattern** (lines 382-400):
```typescript
const injectHiddenInputs = (form: HTMLFormElement, selectedIds: string[]): void => {
  form.querySelectorAll<HTMLInputElement>('input[type="hidden"][name="SelectedSectionIds"]').forEach(i => i.remove());
  selectedIds.forEach(id => {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = 'SelectedSectionIds';
    input.value = id;
    form.appendChild(input);
  });
};
```

**DeckFlow namespace export** (lines 725-727):
```typescript
win.DeckFlow = win.DeckFlow ?? {};
win.DeckFlow.initPrimerSelection = initPrimerSelection;
document.addEventListener('DOMContentLoaded', initPrimerSelection);
```

**Conventions:**
- `strict: true` TypeScript — no implicit `any`. Use explicit type annotations everywhere.
- No module imports — `module: "none"` in `tsconfig.json`. Everything is a local `const`/`type`.
- Compiled output goes to `wwwroot/js/primer-selection.js` — add to `tsconfig.json` if needed and add a `<script>` include in `DeckPrimer.cshtml`.
- Progressive enhancement: the no-JS path loads the server-rendered default preset. JS enhances with localStorage restore.

**Gotcha — bracket-change applies preset but preserves custom toggles (D-3):** On bracket dropdown `change`, first check localStorage for a saved set. If found, restore it (user edits stick). If not found (first visit), apply the preset from `PrimerSectionCatalog.GetPresetForBracket(bracketNumber)` (embedded as a server-rendered `data-preset` attribute). Bracket-scoped gating (cEDH-only / casual-only) always enforced regardless of stored toggles.

---

### `DeckFlow.Web/Program.cs` (modified — DI registration)

**Analog:** `DeckFlow.Web/Program.cs` lines 289-326

**Registration block to add** (insert after the MetaGap variant block at line 308):
```csharp
// Primer prompt variants — same pattern as Analysis (lines 289-292)
builder.Services.AddSingleton<IPrimerPromptVariant, ChatGptPrimerPromptVariant>();
builder.Services.AddSingleton<IPrimerPromptVariant, ClaudePrimerPromptVariant>();
builder.Services.AddSingleton<IPrimerPromptVariant, GeminiPrimerPromptVariant>();
builder.Services.AddSingleton<PrimerPromptVariantRegistry>();

// DeckPrimerPacketService — Scoped (same lifetime as DeckAnalysisPacketService, line 310)
// Why: holds request-scoped state (_lastImportNotice). Shares PacketSessionCache singleton.
builder.Services.AddScoped<IDeckPrimerPacketService>(sp =>
    new DeckPrimerPacketService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetRequiredService<IEdhTop16Client>(),
        sp.GetRequiredService<ICategoryKnowledgeStore>(),
        sp.GetRequiredService<PrimerPromptVariantRegistry>(),
        sp.GetRequiredService<PacketSessionCache>(),
        sp.GetService<ILogger<DeckPrimerPacketService>>()));
```

**Conventions:**
- Primer variants: `AddSingleton<IPrimerPromptVariant, ...>` — three calls then `AddSingleton<PrimerPromptVariantRegistry>()`. DI collects all `IPrimerPromptVariant` as `IEnumerable<IPrimerPromptVariant>` in the registry ctor.
- Service itself: `AddScoped` — not `AddSingleton`. The `AddScoped` factory lambda pattern (with explicit `new ServiceType(sp.GetRequiredService<...>(), ...)`) is used because `DeckPrimerPacketService` uses an `internal` ctor (not visible to the default DI container's auto-resolve).
- `DeckController` auto-resolves `IDeckPrimerPacketService` from DI — no changes needed to the controller's DI registration.

---

### `DeckFlow.Web.Tests/PacketArtifactStorePrimerTests.cs` (test)

**Analog:** `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` (lines 1-132)

**Test class pattern** (lines 1-13):
```csharp
using System.IO.Compression;
using System.Text.Json;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for primer zip round-trip via <see cref="PacketArtifactStore"/>:
/// PRM-09 round-trip regression + allowlist rejection.
/// </summary>
public sealed class PacketArtifactStorePrimerTests
{
```

**Round-trip test pattern** (lines 16-41):
```csharp
[Fact]
public void BuildPrimerZip_then_LoadPrimerFromZip_round_trips_request_context()
{
    var request = new DeckPrimerRequest
    {
        TargetCommanderBracket = "cEDH",
        TargetAiPlatform = "ChatGPT",
        SelectedSectionIds = ["identity", "combos-ground-truth"]
    };

    var bytes = PacketArtifactStore.BuildPrimerZip(
        request,
        inputSummary: "Test Commander | cEDH",
        requestContextText: "target_ai_platform: ChatGPT\ntarget_bracket: cEDH",
        chatGptPromptText: "The primer prompt.",
        claudePromptText: null,
        geminiPromptText: null);

    var loaded = new DeckPrimerRequest();
    using var memoryStream = new MemoryStream(bytes);
    PacketArtifactStore.LoadPrimerFromZip(memoryStream, loaded);

    Assert.Equal("ChatGPT", loaded.TargetAiPlatform);
    // Verify prompt entry survived the round-trip
    using var verifyStream = new MemoryStream(bytes);
    using var archive = new ZipArchive(verifyStream, ZipArchiveMode.Read);
    Assert.Contains(archive.Entries, e => e.FullName == "30-primer-chatgpt-prompt.txt");
}
```

**Allowlist rejection test pattern** (mirrors lines 44-59):
```csharp
[Fact]
public void LoadPrimerFromZip_rejects_non_primer_entry_names()
{
    using var memStream = new MemoryStream();
    using (var arch = new ZipArchive(memStream, ZipArchiveMode.Create, leaveOpen: true))
    {
        var entry = arch.CreateEntry("31-analysis-prompt.txt"); // analysis entry, not primer
        using var w = new StreamWriter(entry.Open());
        w.Write("wrong workflow");
    }
    memStream.Position = 0;
    Assert.Throws<InvalidOperationException>(() =>
        PacketArtifactStore.LoadPrimerFromZip(memStream, new DeckPrimerRequest()));
}
```

**Conventions:**
- `public sealed class` + `[Fact]` — xUnit pattern (DeckFlow.Web.Tests uses xUnit, not NUnit).
- Test names: `Method_Scenario_ExpectedResult` style.
- No mocking needed for `PacketArtifactStore` tests — pure in-memory zip.
- Namespace: `DeckFlow.Web.Tests` (single namespace per test project, mirrors all other test files).

---

## Shared Patterns (cross-cutting, apply to all primer files)

### `{ get; init; }` on round-tripped records

**Source:** `DeckFlow.Web/Services/PacketArtifactStore.cs` lines 744-749 (`ExpertSelectionState`), plus CONTEXT.md "{ get; init; } guard" decision.

**Applies to:** `DeckPrimerPacketResult` and any new record that round-trips through the zip or `System.Text.Json`.

```csharp
// Why: System.Text.Json silently skips get-only properties in .NET 9+.
// Every record property that must survive JSON round-trip uses { get; init; }.
internal sealed record ExpertSelectionState
{
    public IReadOnlyList<string> PinnedVideoIds { get; init; } = [];
    public IReadOnlyList<string> FollowedCreators { get; init; } = [];
}
```

**Gotcha:** `DeckPrimerRequest` is NOT a record — it is a class with mutable setters (required for ASP.NET Core model binding across workflow steps). Only `DeckPrimerPacketResult` and any internal round-trip records use `{ get; init; }`.

---

### Error handling at controller boundary

**Source:** `DeckFlow.Web/Controllers/DeckController.cs` lines 461-510 (DeckAnalysis POST)

**Pattern:**
```csharp
catch (InvalidOperationException ex)
{
    // Domain validation failure — surface message to user, re-render form
    return View("DeckPrimer", new DeckPrimerViewModel { ..., ErrorMessage = ex.Message });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Deck primer build failed.");
    return View("DeckPrimer", new DeckPrimerViewModel { ..., ErrorMessage = "An unexpected error occurred." });
}
```

---

### Logging in services

**Source:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` lines 461, 669-671

```csharp
_logger.LogInformation("Deck Primer packet build completed in {ElapsedMs}ms. PrimerGenerated={PrimerGenerated}.",
    overallStopwatch.ElapsedMilliseconds,
    !string.IsNullOrWhiteSpace(primerPromptText));
```

- Use structured templates with named placeholders — never string interpolation.
- Logger is optional in the service ctor and defaults to `NullLogger<T>.Instance`.

---

### Namespace convention

**Source:** All existing service, model, and controller files.

| File location | Namespace |
|---|---|
| `DeckFlow.Web/Services/*.cs` | `namespace DeckFlow.Web.Services;` |
| `DeckFlow.Web/Services/PromptBuilders/Primer/*.cs` | `namespace DeckFlow.Web.Services.PromptBuilders.Primer;` |
| `DeckFlow.Web/Models/*.cs` | `namespace DeckFlow.Web.Models;` |
| `DeckFlow.Web/Controllers/*.cs` | `namespace DeckFlow.Web.Controllers;` |
| `DeckFlow.Web.Tests/*.cs` | `namespace DeckFlow.Web.Tests;` |

All namespaces are file-scoped (`namespace X;` not `namespace X { }`).

---

### `sealed` + `internal` visibility rules

**Source:** CLAUDE.md Naming Patterns + existing codebase pattern.

- Service implementations: `internal sealed class` (not consumed outside the assembly).
- Service interfaces: `internal interface` (DI registers via the interface type).
- Registry classes: `internal sealed class`.
- Variant classes: `internal sealed class`.
- Request DTOs: `public sealed class` (bound by ASP.NET Core model binder).
- View models: `public sealed class`.
- Result records: `public sealed record` (returned from the service interface).
- Catalog statics: `public static class`.
- Catalog entry records: `public sealed record`.

---

### Allman braces, 4-space indent, file-scoped namespace

**Source:** CLAUDE.md Code Style.

- Open brace on its own line for all C# blocks.
- 4-space indentation in C#.
- 2-space in `.json`.
- No format-document / code cleanup (CLAUDE.md: "touch only the lines that need touching").

---

### LF line endings for all new files

**Source:** CLAUDE.md Line Endings + `.gitattributes`.

DeckFlow is hosted in a public repo where `.gitattributes` enforces LF. New `.cs`, `.cshtml`, and `.ts` files must use LF. Do not let editors write CRLF. The CLAUDE.md note about "prefer CRLF for new files" applies to Windows `.NET` codebases; for this repo `.gitattributes` wins.

---

### Layout CSS in `site-common.css`, NOT `site.css`

**Source:** CLAUDE.md Constraints ("layout CSS must go in `site-common.css`").

Any new CSS for the primer page (section-group collapse, badge counters, section-help-text reveal) goes in `DeckFlow.Web/wwwroot/css/site-common.css`. Token additions (colors, spacing variables) go in `:root` of each guild theme file. Never edit `site.css` for layout changes.

---

### Compiled JS is gitignored — do NOT commit it

**Source (authoritative):** live `.gitignore` line 13 (`DeckFlow.Web/wwwroot/js/*.js`) + the fact that zero compiled `.js` is git-tracked in the repo + the Dockerfile rebuilds all TS at deploy (Node 20 + `CompileTypeScriptAssets` on `dotnet publish`). NOTE: the project CLAUDE.md still carries a STALE line claiming `wwwroot/js` is "git-tracked" — that is wrong for this repo and was corrected in practice in Phase 32 (commit a7efde6 untracked `kb-selection.js`). The `.gitignore` wins.

`primer-selection.ts` compiles to `wwwroot/js/primer-selection.js` during MSBuild (and again at Docker build). The compiled `.js` is **gitignored — do NOT stage or commit it**. The view's `<script src="~/js/primer-selection.js">` resolves at runtime because the deploy build emits it. Plan 31-06 correctly encodes this (do-not-commit). See [[feedback_verify_builds_test_project]]-adjacent convention notes.

---

## Data Source Analogs (PRM-05/06/07)

| Data needed by primer | Source service | How the analysis service uses it | Primer reuse pattern |
|---|---|---|---|
| Combo ground truth (PRM-05) | `ICommanderSpellbookService.FindCombosAsync` | `DeckAnalysisPacketService.cs` line 661 — fire as Task, await later | Same pattern; always fire; null result → D-2 disclosure |
| Bracket-5 archetypes (PRM-06) | `IEdhTop16Client.SearchCommanderEntriesAsync` | `MetaGapService` — not in DeckAnalysisPacketService | Inject `IEdhTop16Client` into `DeckPrimerPacketService`; call only when bracket == "cEDH" |
| Category distribution (PRM-07) | `ICategoryKnowledgeStore` → `CategoryKnowledgeRepository` | `CategorySuggestionService` — not in DeckAnalysisPacketService | Inject `ICategoryKnowledgeStore`; query ramp/draw/interaction/tutor counts for the resolved commander |

---

## No Analog Found

All files have analogs. No entries in this section.

---

## Metadata

**Analog search scope:** `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/PromptBuilders/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Views/`, `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core/Knowledge/`

**Files read for pattern extraction:** 20

**Pattern extraction date:** 2026-06-08
