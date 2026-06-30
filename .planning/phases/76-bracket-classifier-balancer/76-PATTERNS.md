# Phase 76: Bracket Classifier + Balancer — Pattern Map

**Mapped:** 2026-06-28
**Files analyzed:** 28 new/modified files
**Analogs found:** 27 / 28 (bracket-data.json has no analog)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Bracket/GameChangerCatalog.cs` | model | transform | `DeckFlow.Core/Manabase/ManabaseReport.cs` | role-match (immutable sealed record) |
| `DeckFlow.Core/Bracket/BracketClassification.cs` | model | transform | `DeckFlow.Core/Manabase/ManabaseReport.cs` | role-match (result record) |
| `DeckFlow.Core/Bracket/BracketClassifier.cs` | utility | transform | `DeckFlow.Core/Manabase/ManabaseClassifier.cs` | exact (pure static classifier) |
| `DeckFlow.Web/Data/bracket-data.json` | config | file-I/O | — | no analog (first JSON seed file of this type) |
| `DeckFlow.Web/Services/Bracket/IGameChangerCatalogService.cs` | service | file-I/O | `DeckFlow.Web/Services/IHelpContentService.cs` | role-match |
| `DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs` | service | file-I/O | `DeckFlow.Web/Services/HelpContentService.cs` | exact (lazy file-load singleton) |
| `DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs` | service | request-response | `DeckFlow.Web/Services/Manabase/IManabaseAnalysisService.cs` | role-match |
| `DeckFlow.Web/Services/Bracket/BracketClassificationService.cs` | service | request-response | `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` | role-match (orchestrating service) |
| `DeckFlow.Web/Services/PromptBuilders/Bracket/IBracketPromptVariant.cs` | service | transform | `DeckFlow.Web/Services/PromptBuilders/Primer/IPrimerPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Bracket/ChatGptBracketPromptVariant.cs` | service | transform | `DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Bracket/ClaudeBracketPromptVariant.cs` | service | transform | `DeckFlow.Web/Services/PromptBuilders/Primer/ClaudePrimerPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Bracket/GeminiBracketPromptVariant.cs` | service | transform | `DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Bracket/BracketPromptVariantRegistry.cs` | service | request-response | `DeckFlow.Web/Services/PromptBuilders/Primer/PrimerPromptVariantRegistry.cs` | exact |
| `DeckFlow.Web/Controllers/BracketController.cs` | controller | request-response | `DeckFlow.Web/Controllers/ManabaseController.cs` | exact |
| `DeckFlow.Web/Models/BracketViewModel.cs` | model | request-response | `DeckFlow.Web/Models/ManabaseViewModel.cs` | exact |
| `DeckFlow.Web/Models/BracketRequest.cs` | model | request-response | `DeckFlow.Web/Models/ManabaseRequest.cs` | role-match |
| `DeckFlow.Web/Views/Deck/Bracket.cshtml` | component | request-response | `DeckFlow.Web/Views/Deck/Manabase.cshtml` | exact |
| `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs` | test | transform | `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` | exact (pure static classifier test) |
| `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs` | test | request-response | `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` | exact |
| `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` | test | transform | `DeckFlow.Web.Tests/ResultContractTests.cs` | exact (3-platform parity) |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (modified) | config | CRUD | itself (add seed row) | n/a |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (modified) | config | CRUD | itself (add description entry) | n/a |
| `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (modified) | config | request-response | itself (add Create() call) | n/a |
| `DeckFlow.Web/Models/DeckPageTab.cs` (modified) | model | — | itself (add enum value) | n/a |
| `DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml` (modified) | component | — | itself (add case) | n/a |
| `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` (modified) | test | — | itself (add InlineData) | n/a |
| `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` (modified) | test | — | itself (add InlineData) | n/a |
| `DeckFlow.Web/Program.cs` (modified) | config | — | itself (add DI registrations) | n/a |

---

## Pattern Assignments

### `DeckFlow.Core/Bracket/GameChangerCatalog.cs` + `BracketClassification.cs` (model, transform)

**Analog:** `DeckFlow.Core/Manabase/ManabaseReport.cs` for the sealed-record result shape.

**Imports pattern** (copy these namespace declarations):
```csharp
namespace DeckFlow.Core.Bracket;
```

**Core record pattern** — sealed record with `IReadOnlyList<T>` properties and `{ get; init; }` on every property. Never use `{ get; }` alone (breaks System.Text.Json deserialization in .NET 10):
```csharp
// GameChangerCatalog.cs
public sealed record GameChangerCatalog(
    DateOnly EffectiveDate,
    IReadOnlyList<string> GameChangers,
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

// BracketClassification.cs
public sealed record BracketClassification(
    int BracketNumber,
    IReadOnlyList<string> DetectedGameChangers,
    IReadOnlyList<string> DetectedMassLandDenial,
    IReadOnlyList<string> DetectedExtraTurnCards,
    IReadOnlyList<TwoCardCombo>? TwoCardCombos,
    bool ComboDetectionAvailable,
    string EffectiveDate);
```

**Project reference note:** `BracketClassification` references the Core-local `TwoCardCombo` record (`DeckFlow.Core/Bracket/TwoCardCombo.cs`), NOT the Web `SpellbookCombo` type — keeping `DeckFlow.Core` free of any `DeckFlow.Web` reference. Per 76-01, the Web orchestrator (76-04) maps `SpellbookCombo` -> `TwoCardCombo` before calling `BracketClassifier.Classify`. This enables Phase 77 Power-axis reuse without inverting the project dependency.

---

### `DeckFlow.Core/Bracket/BracketClassifier.cs` (utility, transform)

**Analog:** `DeckFlow.Core/Manabase/ManabaseClassifier.cs` (lines 1–70 for static class shape); `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` line 23 for the `public static ... Classify(...)` entry-point pattern.

**Imports pattern** (lines 1–4 of ManabaseClassifier.cs):
```csharp
using System.Globalization;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Bracket;
```

**Core static-classifier pattern** — pure `static` class, single public entry point `Classify(...)`, private helpers below:
```csharp
// Source: DeckFlow.Core/Content/ContentSyncDiffClassifier.cs:23
// and DeckFlow.Core/Manabase/ManabaseClassifier.cs:64
public static class BracketClassifier
{
    public static BracketClassification Classify(
        IReadOnlyList<DeckEntry> entries,
        GameChangerCatalog catalog,
        IReadOnlyList<TwoCardCombo>? twoCardCombos)
    {
        var deckNames = entries
            .Where(e => e.Board is "mainboard" or "commander")
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var detectedGCs = catalog.GameChangers
            .Where(gc => deckNames.Contains(gc))
            .ToList();
        // ... rest of logic per RESEARCH.md §2 Example 1
    }
}
```

**Gating logic to copy exactly** (from RESEARCH.md §1.1):
```csharp
int bracketNumber;
if (detectedMld.Count > 0 || twoCardCombos.Count > 0 || detectedGCs.Count >= 4)
    bracketNumber = 4;
else if (detectedGCs.Count >= 1)
    bracketNumber = 3;
else
    bracketNumber = 2;   // B2 default for zero-signal decks (B1 requires self-declaration)
```

**Effective-date format** — always use `InvariantCulture` (Pitfall 4):
```csharp
catalog.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
```

---

### `DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs` (service, file-I/O)

**Analog:** `DeckFlow.Web/Services/HelpContentService.cs`

**Imports pattern** (HelpContentService.cs lines 1–7):
```csharp
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace DeckFlow.Web.Services.Bracket;
```

**Lazy file-load singleton pattern** (HelpContentService.cs lines 16–37):
```csharp
// HelpContentService ctor — copy this pattern:
public HelpContentService(IWebHostEnvironment environment)
    : this(Path.Combine(environment.ContentRootPath, "Help"))
{
}

public HelpContentService(string rootPath)
{
    _root = rootPath;
    _all = new Lazy<IReadOnlyList<HelpTopic>>(LoadAll);
}
```

**Adapted for GameChangerCatalogService** (use IMemoryCache instead of ConcurrentDictionary since we have a single file, not a directory scan):
```csharp
public sealed class GameChangerCatalogService : IGameChangerCatalogService
{
    private const string CacheKey = "bracket:game-changer-catalog";
    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;

    // DI ctor
    public GameChangerCatalogService(IWebHostEnvironment env, IMemoryCache cache)
    {
        _dataFilePath = Path.Combine(env.ContentRootPath, "Data", "bracket-data.json");
        _cache = cache;
    }

    // Test-seam ctor (internal, per DeckFlow convention)
    internal GameChangerCatalogService(string dataFilePath, IMemoryCache cache)
    {
        _dataFilePath = dataFilePath;
        _cache = cache;
    }

    public GameChangerCatalog GetCatalog()
    {
        if (_cache.TryGetValue<GameChangerCatalog>(CacheKey, out var cached) && cached is not null)
            return cached;
        var json = File.ReadAllText(_dataFilePath);
        var catalog = JsonSerializer.Deserialize<GameChangerCatalog>(json, _jsonOptions)!;
        _cache.Set(CacheKey, catalog, TimeSpan.FromHours(24));
        return catalog;
    }
}
```

**DI registration in Program.cs** (mirror HelpContentService registration at line 89):
```csharp
// Program.cs line 89 pattern:
builder.Services.AddSingleton<IHelpContentService, HelpContentService>();
// → for bracket:
builder.Services.AddSingleton<IGameChangerCatalogService, GameChangerCatalogService>();
```

---

### `DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs` + `BracketClassificationService.cs` (service, request-response)

**Analog:** `DeckFlow.Web/Services/Manabase/IManabaseAnalysisService.cs` + `ManabaseAnalysisService.cs` for the orchestrating-service pattern.

**Constructor pattern** — argument-null guards, multiple injected dependencies, logger optional (NullLogger fallback):
```csharp
// Pattern: CommanderSpellbookService.cs:77-78
ArgumentNullException.ThrowIfNull(gameChangerCatalogService);
ArgumentNullException.ThrowIfNull(deckEntryLoader);
ArgumentNullException.ThrowIfNull(spellbookService);
```

**Combo null handling** (BRACKET-03 critical — from RESEARCH.md §3.2):
```csharp
// ComboResult == null means "unavailable", NOT "no combos"
bool comboAvailable = comboResult != null;
var twoCardCombos = comboResult?.IncludedCombos
    .Where(c => c.CardNames.Count == 2)
    .ToList() ?? [];
// Never: var twoCardCombos = comboResult?.IncludedCombos ?? [] with hasTwoCardCombo = false
```

**DI registration in Program.cs** (mirror scoped service pattern):
```csharp
builder.Services.AddScoped<IBracketClassificationService, BracketClassificationService>();
```

---

### `DeckFlow.Web/Services/PromptBuilders/Bracket/IBracketPromptVariant.cs` (service, transform)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Primer/IPrimerPromptVariant.cs` (exact match)

**Full file to copy** (IPrimerPromptVariant.cs lines 1–48, substitute types):
```csharp
using DeckFlow.Web.Models;
using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

internal interface IBracketPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the bracket classification + optional balancer prompt.
    /// </summary>
    string Build(
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default);
}
```

**Note:** Two output blocks, both from a single `Build()` call:
1. Classification block (always present)
2. Balancer block (only when `targetBracketNumber != null` AND deck is over target)

---

### `DeckFlow.Web/Services/PromptBuilders/Bracket/BracketPromptVariantRegistry.cs` (service, request-response)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Primer/PrimerPromptVariantRegistry.cs` (exact copy pattern)

**Full shape to copy** (PrimerPromptVariantRegistry.cs lines 1–63, substitute types):
```csharp
namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

internal sealed class BracketPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IBracketPromptVariant> _variants;

    public BracketPromptVariantRegistry(IEnumerable<IBracketPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    public string Build(
        AiPlatform platform,
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(classification, targetBracketNumber, deckName, tiers, catalog, cancellationToken);
    }
}
```

**DI registration** — register each variant as the interface, then the registry gets them via `IEnumerable<IBracketPromptVariant>`:
```csharp
// Mirror pattern from AnalysisPromptVariantRegistry wiring in Program.cs
builder.Services.AddSingleton<IBracketPromptVariant, ChatGptBracketPromptVariant>();
builder.Services.AddSingleton<IBracketPromptVariant, ClaudeBracketPromptVariant>();
builder.Services.AddSingleton<IBracketPromptVariant, GeminiBracketPromptVariant>();
builder.Services.AddSingleton<BracketPromptVariantRegistry>();
```

---

### `DeckFlow.Web/Services/PromptBuilders/Bracket/ChatGptBracketPromptVariant.cs` (and Claude/Gemini variants) (service, transform)

**Analog:** `DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs` (exact shape)

**Imports + class declaration pattern** (ChatGptPrimerPromptVariant.cs lines 1–14):
```csharp
using System.Text;
using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

internal sealed class ChatGptBracketPromptVariant : IBracketPromptVariant
{
    public AiPlatform Platform => AiPlatform.ChatGpt;

    public string Build(
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(catalog);
        var builder = new StringBuilder();
        // ... classification block, then conditional balancer block
    }
}
```

**ADR-0001 constraint** — no shared base class or shared helper method across the three variants. Each file is self-contained. The effective-date stamp line is hand-coded into each:
```csharp
// Each variant independently writes this line (do NOT extract to a shared method):
builder.AppendLine($"Game Changers list effective {classification.EffectiveDate}. " +
    "Re-confirm Game Changers membership before suggesting swaps.");
```

**Combo-unavailable disclosure** (BRACKET-03) — each variant independently writes:
```csharp
if (!classification.ComboDetectionAvailable)
    builder.AppendLine("Note: combo detection was temporarily unavailable. " +
        "A two-card win combo could place this deck a bracket higher than shown — " +
        "please double-check for combos.");
```

---

### `DeckFlow.Web/Controllers/BracketController.cs` (controller, request-response)

**Analog:** `DeckFlow.Web/Controllers/ManabaseController.cs` (exact match — inherits DeckToolControllerBase, uses FeatureFlagGate, RunGuardedAsync)

**Imports pattern** (ManabaseController.cs lines 1–9):
```csharp
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Bracket;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;
```

**Class declaration + ctor pattern** (ManabaseController.cs lines 16–31):
```csharp
public sealed class BracketController : DeckToolControllerBase
{
    private readonly IBracketClassificationService _bracketService;
    private readonly ILogger<BracketController> _logger;

    public BracketController(
        IBracketClassificationService bracketService,
        ILogger<BracketController> logger)
    {
        ArgumentNullException.ThrowIfNull(bracketService);
        ArgumentNullException.ThrowIfNull(logger);
        _bracketService = bracketService;
        _logger = logger;
    }
```

**GET action pattern** (ManabaseController.cs lines 34–39):
```csharp
[HttpGet("/bracket")]
[FeatureFlagGate("tool.bracket.enabled")]
public IActionResult Bracket() => View("Bracket", new BracketViewModel());
```

**POST action — collapsed (no /bracket/load step unlike Manabase)**:
```csharp
[HttpPost("/bracket")]
[ValidateAntiForgeryToken]
[FeatureFlagGate("tool.bracket.enabled")]
public async Task<IActionResult> Bracket(BracketRequest request)
{
    request ??= new BracketRequest();
    // validate targetBracketNumber is 1-5 if set
    return await RunGuardedAsync(request, "classify",
        "Something went wrong classifying that deck. Please try again.",
        async token =>
        {
            var result = await _bracketService.ClassifyAsync(request.DeckSource, ...token);
            return View("Bracket", new BracketViewModel { ... });
        });
}
```

**RunGuardedAsync pattern** (ManabaseController.cs lines 172–222) — copy this structure exactly, substitute "Bracket" for "Manabase" and "classify" for the operation name:
```csharp
private async Task<IActionResult> RunGuardedAsync(
    BracketRequest request,
    string operation,
    string unexpectedMessage,
    Func<CancellationToken, Task<IActionResult>> body)
{
    using var timeoutScope = CreateTimeoutScope(LookupTimeout);
    try
    {
        return await body(timeoutScope.Token);
    }
    catch (OperationCanceledException) when (timeoutScope.IsCancellationRequested)
    {
        _logger.LogInformation("Bracket {Operation} timed out.", operation);
        return View("Bracket", new BracketViewModel
        {
            Request = request,
            ErrorMessage = "The deck took too long to load. Try again in a moment.",
        });
    }
    catch (InvalidOperationException exception)
    {
        _logger.LogInformation(exception, "Bracket {Operation} failed validation.", operation);
        return View("Bracket", new BracketViewModel
        {
            Request = request,
            ErrorMessage = exception.Message,
        });
    }
    catch (HttpRequestException exception)
    {
        _logger.LogWarning(exception, "Bracket {Operation} hit an upstream dependency.", operation);
        return View("Bracket", new BracketViewModel
        {
            Request = request,
            ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
        });
    }
    catch (Exception exception)
    {
        _logger.LogError(exception, "Bracket {Operation} failed unexpectedly.", operation);
        return View("Bracket", new BracketViewModel
        {
            Request = request,
            ErrorMessage = unexpectedMessage,
        });
    }
}
```

---

### `DeckFlow.Web/Models/BracketViewModel.cs` (model, request-response)

**Analog:** `DeckFlow.Web/Models/ManabaseViewModel.cs` (exact shape)

**Pattern** (ManabaseViewModel.cs lines 1–86):
```csharp
namespace DeckFlow.Web.Models;

public sealed class BracketViewModel
{
    /// <summary>The active deck-tool tab (always <see cref="DeckPageTab.Bracket"/>).</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Bracket;

    /// <summary>The form-bound request, re-rendered so inputs persist across the postback.</summary>
    public BracketRequest Request { get; init; } = new();

    /// <summary>User-facing error message, or null when the request succeeded.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The bracket classification result, or null before a successful submit.</summary>
    public BracketClassification? Classification { get; init; }

    /// <summary>The selected target bracket number (1-5), or null if not selected.</summary>
    public int? TargetBracketNumber { get; init; }

    /// <summary>The paste artifact for the selected AI platform, or null.</summary>
    public string? PromptArtifact { get; init; }

    // computed helpers
    public bool HasResult => Classification is not null;
    public bool HasTarget => TargetBracketNumber.HasValue;
    public bool IsOverTarget => HasResult && HasTarget &&
        Classification!.BracketNumber > TargetBracketNumber;
}
```

---

### `DeckFlow.Web/Models/BracketRequest.cs` (model, request-response)

**Analog:** `DeckFlow.Web/Models/ManabaseRequest.cs` (role-match — form-bound request with DeckSource)

**Pattern** — minimal request; copy `DeckInputSource` property pattern from ManabaseRequest:
```csharp
namespace DeckFlow.Web.Models;

public sealed class BracketRequest
{
    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>Unified deck source (URL or pasted text) resolved by the input method.</summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;

    public string DeckUrl { get; set; } = string.Empty;
    public string DeckText { get; set; } = string.Empty;
    public string? DeckName { get; set; }

    /// <summary>Target bracket number (1-5), or null if the user chose classify-only.</summary>
    public int? TargetBracketNumber { get; set; }

    /// <summary>AI platform for the paste artifact.</summary>
    public string TargetAiPlatform { get; set; } = "ChatGPT";
}
```

---

### `DeckFlow.Web/Views/Deck/Bracket.cshtml` (component, request-response)

**Analog:** `DeckFlow.Web/Views/Deck/Manabase.cshtml` (exact layout structure)

**Page skeleton pattern** (Manabase.cshtml lines 1–24 — copy exactly, substitute names):
```razor
@model DeckFlow.Web.Models.BracketViewModel
@{
    ViewData["Title"] = "Bracket Check";
}

<section class="hero">
    <h1>Bracket Check</h1>
    <p class="lede">Classify a Commander deck into its official 1-5 bracket and balance it toward a target
        — the classification is computed locally, no AI needed.</p>
    <details class="hero-detail">
        <summary>How it works</summary>
        <p>...</p>
    </details>
</section>

@await Html.PartialAsync("_BusyIndicator")
@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)

<div class="error-banner @(string.IsNullOrWhiteSpace(Model.ErrorMessage) ? "hidden" : string.Empty)" role="alert">
    @Model.ErrorMessage
</div>
```

**Target-bracket pills** — copy `fieldset.manabase-segmented` pattern from Manabase.cshtml:
```razor
<fieldset class="manabase-segmented">
    <legend>Target bracket (optional)</legend>
    <div class="manabase-pills">
        @foreach (var tier in /* BracketTier list from model */)
        {
            <label class="manabase-pill">
                <input type="radio" name="TargetBracketNumber" value="@tier.Number"
                       checked="@(Model.Request.TargetBracketNumber == tier.Number ? "checked" : null)" />
                <span>B@tier.Number @tier.Name</span>
            </label>
        }
    </div>
    <p class="manabase-help">Leave unset to just classify. Pick a target to get the cards that exceed it plus suggested cuts.</p>
</fieldset>
```

**Result panel with copy-prompt collapsible** — copy `details.result-panel.nested-panel` from Manabase.cshtml swap-prompt block:
```razor
<details class="result-panel nested-panel">
    <summary>
        <span class="panel-heading">Want fair swaps? Copy this prompt for ChatGPT / Claude / Gemini</span>
    </summary>
    <button type="button" class="copy-button" data-copy-target="#bracket-prompt">Copy</button>
    <textarea id="bracket-prompt" readonly>@Model.PromptArtifact</textarea>
</details>
```

---

### `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs` (test, transform)

**Analog:** `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` (exact — pure xUnit Theory/Fact tests on a static class)

**File header + test class pattern** (DeckStatClassifierTests.cs lines 1–11):
```csharp
using DeckFlow.Core.Bracket;
using DeckFlow.Core.Models;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class BracketClassifierTests
{
    // one section per logical gate (GC threshold, MLD, combo, extra-turns, B1/B2)
```

**Theory with InlineData pattern** (DeckStatClassifierTests.cs):
```csharp
[Theory]
[InlineData(4, false, false, 4)]   // 4 GCs → B4
[InlineData(3, false, false, 3)]   // 3 GCs → B3
[InlineData(0, true,  false, 4)]   // combo → B4
[InlineData(0, false, true,  4)]   // MLD   → B4
[InlineData(0, false, false, 2)]   // nothing → B2
public void Classify_BracketNumber_FromCombination(
    int gcCount, bool hasCombo, bool hasMld, int expectedBracket)
{
    var catalog = BuildCatalog(gcCount, hasMld ? new[] { "Armageddon" } : Array.Empty<string>());
    var twoCardCombos = hasCombo ? BuildCombos(twoCardCount: 1) : Array.Empty<TwoCardCombo>();
    var result = BracketClassifier.Classify(BuildEntries(catalog, hasMld), catalog, twoCardCombos);
    Assert.Equal(expectedBracket, result.BracketNumber);
}

[Fact]
public void Classify_NullComboResult_SetsComboDetectionAvailableFalse()
{
    var result = BracketClassifier.Classify([], BuildCatalog(0), null);
    Assert.False(result.ComboDetectionAvailable);
}
```

**Critical null-combo test** (BRACKET-03 / Pitfall 1):
```csharp
[Fact]
public void Classify_NullComboResult_DoesNotClaimZeroCombos_InBracketNumber()
{
    // A deck with 0 GCs and null combo service must NOT assert "no combos found"
    // by using that absence to prevent B4 classification of a combo deck.
    // Verify: bracketNumber stays ≤3 (correct — we can't know) AND
    // ComboDetectionAvailable is false.
    var result = BracketClassifier.Classify([], BuildCatalog(0), twoCardCombos: null);
    Assert.False(result.ComboDetectionAvailable);
    // No assertion on BracketNumber here — when unavailable, classifier cannot gate B4.
}
```

---

### `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs` (test, request-response)

**Analog:** `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` (exact copy — substitute types and assertion strings)

**Full render-test scaffold** (ManabaseViewRenderTests.cs lines 182–260) — copy and adapt:

```csharp
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BracketViewRenderTests
{
    [Fact]
    public async Task OffState_FlagFalse_RendersNoBracketBadgeMarkup()
    {
        var model = new BracketViewModel();  // no classification = flag OFF path

        string html = await RenderBracketViewAsync(model);

        Assert.DoesNotContain("bracket-badge", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_WithClassification_RendersBracketBadge()
    {
        var model = BuildClassifiedModel(bracketNumber: 4);

        string html = await RenderBracketViewAsync(model);

        Assert.Contains("bracket-badge", html, StringComparison.Ordinal);
        Assert.Contains("bracket-badge--b4", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderBracketViewAsync(BracketViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry,
            DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(
            new FakeFeatureFlagCache());
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(BracketController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(
                new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());

        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "Bracket", isMainPage: false);
        Assert.True(viewResult.Success,
            $"View 'Bracket' not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(
            new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer, new HtmlHelperOptions());
        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }
    // StubTempDataProvider + TestWebHostEnvironment + CreateHostingEnvironment():
    // copy verbatim from ManabaseViewRenderTests.cs lines 229-260
}
```

---

### `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` (test, transform)

**Analog:** `DeckFlow.Web.Tests/ResultContractTests.cs` (exact pattern — 3-platform Theory with inline registry)

**Registry builder pattern** (ResultContractTests.cs lines 29–35):
```csharp
using DeckFlow.Core.Bracket;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BracketPromptVariantParityTests
{
    private static BracketPromptVariantRegistry BuildRegistry() =>
        new(new IBracketPromptVariant[]
        {
            new ChatGptBracketPromptVariant(),
            new ClaudeBracketPromptVariant(),
            new GeminiBracketPromptVariant(),
        });

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_ClassificationBlock_AppearsInAllThreeVariants(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        var result = registry.Build(platform, BuildClassification(), null, null,
            BuildTiers(), BuildCatalog());

        Assert.Contains("Game Changers list effective", result, StringComparison.Ordinal);
        Assert.Contains("WHY THIS BRACKET", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_BalancerBlock_AppearsInAllThreeVariants_WhenTargetSelected(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        // B4 classification with B2 target → over-target → balancer block
        var result = registry.Build(platform, BuildClassification(bracketNumber: 4),
            targetBracketNumber: 2, null, BuildTiers(), BuildCatalog());

        Assert.Contains("FLOOR VIOLATIONS", result, StringComparison.Ordinal);
        Assert.Contains("STARTER CUTS", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_EffectiveDateStamp_AppearsInAllThreeVariants(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        var result = registry.Build(platform, BuildClassification(), null, null,
            BuildTiers(), BuildCatalog());

        Assert.Contains("2026-02-09", result, StringComparison.Ordinal);
    }
}
```

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (modified)

**Add to `PostgresSeedSql`** (after the last entry `'analysis.command-zone-awareness', FALSE`):
```sql
  ('tool.bracket.enabled', FALSE),
```

**Add to `SqliteSeedSql`** (after the last entry `'analysis.command-zone-awareness', 0`):
```sql
  ('tool.bracket.enabled', 0),
```

Both maintain the `ON CONFLICT (key) DO NOTHING` idiom (FeatureFlagStore.cs lines 196–228).

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (modified)

**Add to `Descriptions`** (after `"analysis.command-zone-awareness"` entry):
```csharp
["tool.bracket.enabled"] =
    "Enable the Bracket Check tool — auto-classify a Commander deck into its official 1-5 bracket " +
    "and generate a balancer prompt. Off = byte-identical to pre-Phase-76.",
```

---

### `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (modified)

**Add to `Definitions`** (after the `manabase` entry, both are in `ToolNavSection.Analyze`):
```csharp
Create("bracket", "Bracket", "/bracket", ToolNavSection.Analyze,
    "tool.bracket.enabled", false /*core*/,
    "Bracket Check",
    "Classify a Commander deck into its official 1-5 bracket from Game Changers, two-card combos, and mass-land-denial — then generate a balancer prompt to hit a target bracket. No tutor-counting.",
    "bracket", DeckPageTab.Bracket, false /*isPrimaryTile*/),
```

---

### `DeckFlow.Web/Models/DeckPageTab.cs` (modified)

**Add after `Manabase = 14`** (ToolRegistry.cs pattern — never renumber existing members):
```csharp
/// <summary>Bracket classifier and balancer page.</summary>
Bracket = 15,
```

---

### `DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml` (modified)

**Add before the closing `default:` case** (after the last `break;` in the switch):
```razor
case "bracket":
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor"
         stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"
         aria-hidden="true" focusable="false">
        <line x1="4" y1="16" x2="6" y2="16"/>
        <line x1="8" y1="13" x2="10" y2="13"/>
        <line x1="12" y1="10" x2="14" y2="10"/>
        <line x1="16" y1="7" x2="16" y2="7"/>
        <polyline points="4,16 6,16 6,13 10,13 10,10 14,10 14,7 17,7"/>
    </svg>
    break;
```

---

### `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` (modified)

**Add to the `[Theory]` block** (after `[InlineData("analysis.command-zone-awareness")]`):
```csharp
[InlineData("tool.bracket.enabled")]
```

---

### `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` (modified)

**Add to the `[Theory]` block** (after `[InlineData("analysis.command-zone-awareness", false)]`):
```csharp
[InlineData("tool.bracket.enabled", false)] // BRACKET-05: seeded OFF
```

---

## Shared Patterns

### Feature Flag Gate Attribute
**Source:** `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs`
**Apply to:** All three `BracketController` actions (GET + POST)
```csharp
[FeatureFlagGate("tool.bracket.enabled")]
```
This attribute returns 404 when the flag is OFF — the byte-identity guarantee for disabled tools.

### Argument-Null Guard (constructor)
**Source:** `DeckFlow.Web/Services/CommanderSpellbookService.cs` lines 77–78
**Apply to:** All new service constructors and controller constructors
```csharp
ArgumentNullException.ThrowIfNull(dependency);
```

### IReadOnlyList<T> on public surfaces
**Source:** Throughout `DeckFlow.Core/Models/`, `DeckFlow.Web/Services/`
**Apply to:** All new record properties and method return types that expose collections
```csharp
// Yes: IReadOnlyList<string> DetectedGameChangers
// No:  List<string> DetectedGameChangers
```

### NullLogger fallback on optional logger
**Source:** `DeckFlow.Web/Services/CommanderSpellbookService.cs` line 82
**Apply to:** `BracketClassificationService` if logger is injected as optional
```csharp
_logger = logger ?? NullLogger<BracketClassificationService>.Instance;
```

### get; init; on all record properties (never get; only)
**Source:** `.editorconfig` + `CarveOutGuardTests.cs` carve-out description
**Apply to:** All new `sealed record` types in Core and Web
```csharp
// Yes: public string EffectiveDate { get; init; }
// No:  public string EffectiveDate { get; }   ← breaks System.Text.Json deserialization
```

### Structured logging (no string interpolation)
**Source:** Throughout all controllers and services
**Apply to:** All new log statements
```csharp
// Yes: _logger.LogInformation("Bracket {Operation} timed out.", operation);
// No:  _logger.LogInformation($"Bracket {operation} timed out.");
```

### `ON CONFLICT (key) DO NOTHING` seed idiom
**Source:** `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` lines 196–228
**Apply to:** New `tool.bracket.enabled` seed row in both PostgresSeedSql and SqliteSeedSql
This preserves operator-set values across restarts (FLAG-01 contract).

### FakeFeatureFlagCache for view render tests
**Source:** `DeckFlow.Web.Tests/TestDoubles/FakeFeatureFlagCache.cs`
**Apply to:** `BracketViewRenderTests.RenderBracketViewAsync()` — register it as `IFeatureFlagCache` in the test ServiceCollection (same as ManabaseViewRenderTests.cs line 194)

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `DeckFlow.Web/Data/bracket-data.json` | config | file-I/O | First versioned JSON seed file of this type in the codebase. No existing `Data/` directory or JSON seed pattern exists. Use the schema from RESEARCH.md §2.2 directly. Mark with `<Content CopyToOutputDirectory="Always" />` in `DeckFlow.Web.csproj`. |

---

## Build Coupling Note

`DeckFlow.Web.csproj` must include the new seed file as a content item. Find the existing `<Content>` item pattern in the csproj (used for Help markdown files) and mirror it:

```xml
<Content Include="Data\bracket-data.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
```

---

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Views/`, `DeckFlow.Core/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core.Tests/`
**Files scanned:** 22 files read directly; 6 grep searches
**Pattern extraction date:** 2026-06-28
