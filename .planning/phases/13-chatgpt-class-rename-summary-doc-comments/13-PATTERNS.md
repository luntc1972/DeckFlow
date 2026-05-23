# Phase 13: ChatGpt* Class Rename + Summary Doc Comments - Pattern Map

**Mapped:** 2026-05-17
**Files analyzed:** 32 (10 model files renamed, 7 service files renamed, 1 enum file edited, 1 controller + 5 views/partials edited, 9 test files renamed, 2 test files edited, 1 Program.cs DI block edited, 1 README updated)
**Analogs found:** 32 / 32 (every renamed file has an existing analog inside the codebase)

**Phase nature:** This is a pure CLASS-RENAME phase. The executor MUST NOT introduce new patterns. Every renamed file already follows one of the seven patterns documented below. This document lists the canonical existing source for each pattern so that XML `<summary>` doc-comment tone, sealed record shape, interface co-location, static helper shape, nested response-shape layout, DI registration form, and enum syntax all match the file the executor opens for reference.

## File Classification

### Wave 1 — Models

| New/Modified File | Role | Data Flow | Pattern Family | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|----------------|---------------|
| `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | request DTO (form-bound) | request-response | sealed class request DTO | `DeckFlow.Web/Models/CategorySuggestionRequest.cs` | exact |
| `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | Razor view model | request-response | sealed class view model | `DeckFlow.Web/Models/CardLookupViewModel.cs` | exact |
| `DeckFlow.Web/Models/DeckAnalysisResponse.cs` (+ 3 nested shapes) | response DTO (JSON-bound) | transform | sealed class with nested response shapes | `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs` (pre-rename precedent, same file) | exact |
| `DeckFlow.Web/Models/SetUpgradeResponse.cs` (+ 4 nested shapes) | response DTO (JSON-bound) | transform | sealed class with nested response shapes | `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs` | exact |
| `DeckFlow.Web/Models/DeckComparisonRequest.cs` | request DTO | request-response | sealed class request DTO | `DeckFlow.Web/Models/CategorySuggestionRequest.cs` | exact |
| `DeckFlow.Web/Models/DeckComparisonViewModel.cs` | Razor view model | request-response | sealed class view model | `DeckFlow.Web/Models/CardLookupViewModel.cs` | exact |
| `DeckFlow.Web/Models/DeckComparisonResponse.cs` (+ 1 nested shape) | response DTO (JSON-bound) | transform | sealed class with nested response shapes | `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs` | exact |
| `DeckFlow.Web/Models/MetaGapRequest.cs` | request DTO | request-response | sealed class request DTO | `DeckFlow.Web/Models/CategorySuggestionRequest.cs` | exact |
| `DeckFlow.Web/Models/MetaGapViewModel.cs` | Razor view model | request-response | sealed class view model | `DeckFlow.Web/Models/CardLookupViewModel.cs` | exact |
| `DeckFlow.Web/Models/MetaGapResponse.cs` (+ 11 nested shapes) | response DTO (JSON-bound) | transform | sealed class with 12 nested response shapes | `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs` (this file before rename) | exact |
| `DeckFlow.Web/Models/DeckPageTab.cs` (edit only — values rename) | enum | n/a | flat enum, PascalCase values, explicit integer values | `DeckFlow.Web/Models/CategorySuggestionMode.cs` | exact |

### Wave 2 — Services

| New/Modified File | Role | Data Flow | Pattern Family | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|----------------|---------------|
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (interface + class + result record in one file) | service | request-response | `I*Service` + `sealed class *Service` + `sealed record *Result` co-located | `DeckFlow.Web/Services/CardLookupService.cs` | exact |
| `DeckFlow.Web/Services/DeckComparisonService.cs` | service | request-response | same triplet pattern | `DeckFlow.Web/Services/CardLookupService.cs` | exact |
| `DeckFlow.Web/Services/MetaGapService.cs` | service | request-response | same triplet pattern | `DeckFlow.Web/Services/CommanderSpellbookService.cs` (records-above-interface variant) | exact |
| `DeckFlow.Web/Services/PacketArtifactStore.cs` | static helper | file-I/O | `public static class` with terse `<summary>` | `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs` | exact |
| `DeckFlow.Web/Services/RequestContextParser.cs` | static helper (partial) | transform | `internal static partial class` with generated regex | `DeckFlow.Core/Normalization/CardNormalizer.cs` (partial+`[GeneratedRegex]` shape) | structural |
| `DeckFlow.Web/Services/ResponseParsers.cs` | static helper | transform | `internal static class` | `DeckFlow.Web/Services/CategorySuggestionMessageBuilder.cs` | exact |
| `DeckFlow.Web/Services/JsonTextFormatterService.cs` | static helper | transform | `public static class` | `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs` | exact |
| `DeckFlow.Web/Program.cs` (DI block L263-295) | DI registration | n/a | `AddScoped<IFoo>(sp => new Foo(...))` factory form | `DeckFlow.Web/Program.cs:300-308` (sibling registrations in the same file) | self |

### Wave 3 — Controller + Razor

| Modified File | Role | Data Flow | Pattern Family | Closest Analog |
|---------------|------|-----------|----------------|----------------|
| `DeckFlow.Web/Controllers/DeckController.cs` (12 action method renames + ~80 body refs) | controller | request-response | `[HttpGet("/slug")]` / `[HttpPost("/slug")]` action attrs (UNCHANGED) | sibling action methods in the same file (e.g., `CardLookup()`, `MechanicLookup()`) |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | Razor view | request-response | `@model DeckFlow.Web.Models.XViewModel` directive | sibling views in the same folder |
| `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` | Razor view | request-response | same | same |
| `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` | Razor view | request-response | same | same |
| `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` | Razor partial | n/a | enum-value reference | sibling enum branches in same partial |

### Wave 4 — Tests

| New/Modified File | Role | Data Flow | Pattern Family | Closest Analog |
|-------------------|------|-----------|----------------|----------------|
| `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` | xUnit test | n/a | `public sealed class *Tests` | sibling test files in the same folder (e.g., `DeckSyncServiceTests.cs`, `CardLookupServiceTests.cs`) |
| `DeckFlow.Web.Tests/DeckComparisonServiceTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/MetaGapServiceTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/JsonTextFormatterServiceTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/PacketArtifactStoreRoundTripTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/ResponseParsersTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/ResultContractTests.cs` | xUnit test | n/a | same | same |
| `DeckFlow.Web.Tests/DeckControllerTests.cs` (edit only, 6 inline test-double `private sealed class` renames) | xUnit test | n/a | inline `private sealed class Fake*` / `Throwing*` / `Configurable*` test doubles | within same file — preserved inline placement per Pitfall 3 |
| `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` | test factory | n/a | static factory methods returning real services | within same file — 3 method renames + 2 ILogger<T> generic-arg renames |

## Pattern Assignments

### Pattern 1 — Interface + sealed class + sealed record in one file (Wave 2 services)

**Applies to:** `DeckAnalysisPacketService.cs`, `DeckComparisonService.cs`, `MetaGapService.cs`

**Canonical analog:** `DeckFlow.Web/Services/CardLookupService.cs:13-42`

**Layout to mirror** (lines 13-42 of CardLookupService.cs):
```csharp
namespace DeckFlow.Web.Services;

/// <summary>
/// Looks up pasted card names against Scryfall and returns formatted outputs plus missing lines.
/// </summary>
public interface ICardLookupService
{
    /// <summary>
    /// Looks up the provided card list using Scryfall.
    /// </summary>
    Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the results of a card lookup.
/// </summary>
public sealed record CardLookupResult(IReadOnlyList<string> VerifiedOutputs, IReadOnlyList<string> MissingLines);

/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
{
    // ...
}
```

**Rules the executor MUST preserve:**
- Order: interface first, then result record, then implementing class (per CardLookupService).
- One `<summary>` block per public type.
- Public XML doc on every interface method signature; class methods may use `/// <inheritdoc/>` per `CommanderSpellbookService.cs:84`.
- Class is `public sealed class`; result is `public sealed record`; interface is `public interface I*Service`.
- File-scoped namespace `namespace DeckFlow.Web.Services;` on line 11 (after using block).

**Alternative records-first layout (for MetaGapService):** `DeckFlow.Web/Services/CommanderSpellbookService.cs:13-54` shows multiple `sealed record` types declared BEFORE the interface when records are conceptual inputs to the interface signature:
```csharp
/// <summary>
/// A single confirmed or almost-confirmed combo from Commander Spellbook.
/// </summary>
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions);

// ... more records ...

/// <summary>
/// Looks up combos for a deck using the Commander Spellbook API.
/// </summary>
public interface ICommanderSpellbookService
{
    /// <summary>
    /// Returns combos that are fully in the deck and combos that are one card away,
    /// within the deck's color identity. Returns null if the API call fails.
    /// </summary>
    Task<CommanderSpellbookResult?> FindCombosAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches and caches combo data from the Commander Spellbook backend API.
/// </summary>
public sealed class CommanderSpellbookService : ICommanderSpellbookService
{
    // ...
}
```

**Use whichever variant the existing pre-rename file uses** — the rename should not reshuffle declaration order.

---

### Pattern 2 — Sealed class request DTO with property-level `<summary>` (Wave 1 request DTOs)

**Applies to:** `DeckAnalysisRequest.cs`, `DeckComparisonRequest.cs`, `MetaGapRequest.cs`

**Canonical analog:** `DeckFlow.Web/Models/CategorySuggestionRequest.cs:1-33` (full file)

**Layout to mirror:**
```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Request payload for a single-card category suggestion lookup.
/// </summary>
public sealed class CategorySuggestionRequest
{
    /// <summary>
    /// Chooses whether the lookup should use only the local cache or also inspect a supplied Archidekt reference deck.
    /// </summary>
    public CategorySuggestionMode Mode { get; set; } = CategorySuggestionMode.All;

    /// <summary>
    /// Describes whether the optional reference deck will be provided as a public URL or pasted export text.
    /// </summary>
    public DeckInputSource ArchidektInputSource { get; set; } = DeckInputSource.PublicUrl;

    // ... etc.
}
```

**Simpler one-property variant:** `DeckFlow.Web/Models/CardLookupRequest.cs:1-12` (smaller surface).
```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Represents a pasted list of card names for Scryfall lookup.
/// </summary>
public sealed class CardLookupRequest
{
    /// <summary>
    /// Gets the pasted card list. One card per line; optional leading quantities are allowed.
    /// </summary>
    public string CardList { get; init; } = string.Empty;
}
```

**Rules the executor MUST preserve when renaming the existing `ChatGpt*Request` files:**
- Class-level `<summary>` is ONE sentence that anchors the request's role on the page ("Form-bound request for the deck-analysis page" or similar — read the existing properties to derive accurate wording per D-03).
- Property-level `<summary>` is ONE sentence (existing request DTOs already have some property summaries — preserve them; backfill any missing).
- `public sealed class` — never just `public class`.
- Property names DO NOT CHANGE (Phase 13 invariant — Phase 15 will handle property-level changes per D-07).
- Default values stay byte-identical (e.g., `_targetAiPlatform = "ChatGPT"` remains — D-07 #1).

---

### Pattern 3 — Sealed class Razor view model with property-level `<summary>` (Wave 1 view models)

**Applies to:** `DeckAnalysisViewModel.cs`, `DeckComparisonViewModel.cs`, `MetaGapViewModel.cs`

**Canonical analog:** `DeckFlow.Web/Models/CardLookupViewModel.cs:1-42` (full file)

**Layout to mirror:**
```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Represents the results of looking up a pasted card list.
/// </summary>
public sealed class CardLookupViewModel
{
    /// <summary>
    /// Gets the active tab for the shared deck tool navigation.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.CardLookup;

    /// <summary>
    /// Gets the original user request.
    /// </summary>
    public CardLookupRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message for form or upstream failures.
    /// </summary>
    public string? ErrorMessage { get; init; }

    // ... etc.
}
```

**Rules:**
- Class-level `<summary>` describes what the view renders.
- `ActiveTab` property MUST use the renamed `DeckPageTab` value (per Wave 1 enum rename — `DeckPageTab.DeckAnalysis`, `DeckPageTab.DeckComparison`, `DeckPageTab.CedhMetaGap`).
- Wrapped `Request { get; init; }` property uses renamed request DTO type (`DeckAnalysisRequest`, etc.).
- Property accessors use `init` (not `set`) where the existing file already uses `init`.

---

### Pattern 4 — Sealed class with nested response shapes in one file (Wave 1 response DTOs)

**Applies to:** `DeckAnalysisResponse.cs` (3 nested shapes), `SetUpgradeResponse.cs` (4 nested shapes), `DeckComparisonResponse.cs` (1 nested shape), `MetaGapResponse.cs` (11 nested shapes)

**Canonical analog (in-place precedent):** `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs:1-220` — this is the file being renamed to `MetaGapResponse.cs`. The 12-class nested layout is itself the established pattern.

**Layout to mirror (header + first 2 nested shapes shown for tone):**
```csharp
using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the cEDH meta-gap analysis prompt.
/// </summary>
public sealed class MetaGapResponse
{
    [JsonPropertyName("meta_gap")]
    public MetaGapData MetaGap { get; init; } = new();
}

/// <summary>
/// Body of the cEDH meta-gap analysis covering readiness, win lines, interaction, speed,
/// mana efficiency, core-convergence card list, missing staples, potential cuts, and top-10 lists.
/// </summary>
public sealed class MetaGapData
{
    [JsonPropertyName("commander")]
    public string Commander { get; init; } = string.Empty;

    [JsonPropertyName("color_id")]
    public string ColorId { get; init; } = string.Empty;

    // ... 14 more properties referencing the other 10 nested shape classes ...
}

/// <summary>
/// Pair of primary and backup win lines for a single deck.
/// </summary>
public sealed class WinLineSet
{
    [JsonPropertyName("primary")]
    public string Primary { get; init; } = string.Empty;

    [JsonPropertyName("backup")]
    public string Backup { get; init; } = string.Empty;
}
```

**Rules the executor MUST preserve:**
- `[JsonPropertyName("snake_case")]` attributes stay BYTE-IDENTICAL on every property (Anti-Pattern 4 in RESEARCH.md — touching these breaks T1-T8 zip round-trips).
- All nested classes remain `public sealed class` (no records — match the existing file).
- All properties use `init`-only setters with default values (`= string.Empty`, `= new()`, `= Array.Empty<...>()`).
- Each nested class gets a one-line `<summary>` describing the JSON sub-tree it maps to (D-03 "Nested response-shape classes get a one-line summary describing what JSON shape they map to").
- All 12 classes live in ONE file (do NOT split — RESEARCH.md anti-pattern: "splitting response shape files: defer to AUDIT-01 / Phase 14").

---

### Pattern 5 — Static helper class with terse `<summary>` (Wave 2 helpers)

**Applies to:** `PacketArtifactStore.cs`, `RequestContextParser.cs`, `ResponseParsers.cs`, `JsonTextFormatterService.cs`

**Canonical analog:** `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs:1-32` (header excerpt below)

**Layout to mirror:**
```csharp
using System.Net;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds user-facing error messages for failures coming from third-party upstream services.
/// </summary>
public static class UpstreamErrorMessageBuilder
{
    /// <summary>
    /// Builds a deck-sync error message that highlights the upstream site when possible.
    /// </summary>
    /// <param name="request">Original deck sync request.</param>
    /// <param name="exception">Failure to translate.</param>
    public static string BuildDeckSyncMessage(DeckDiffRequest request, Exception exception)
    {
        // ...
    }
}
```

**Smaller variant:** `DeckFlow.Web/Services/CategorySuggestionMessageBuilder.cs:1-26`:
```csharp
namespace DeckFlow.Web.Services;

/// <summary>
/// Builds user-facing messages for category suggestion lookups.
/// </summary>
public static class CategorySuggestionMessageBuilder
{
    private const string NoCachedDataMessage = "...";

    /// <summary>
    /// Builds the message that appears when no category suggestions were found.
    /// </summary>
    /// <param name="cardName">Card name that was looked up.</param>
    /// <param name="deckTotals">Deck totals for the card.</param>
    public static string BuildNoSuggestionsMessage(string cardName, CardDeckTotals deckTotals)
    {
        // ...
    }
}
```

**Rules:**
- `public static class` (or `internal static class` if the existing class is internal — preserve the access modifier).
- Class-level `<summary>` is ONE sentence stating the helper's verb-object purpose ("Builds...", "Parses...", "Formats...").
- Public methods get `<summary>` + `<param>` tags where parameters are non-obvious.
- `RequestContextParser.cs` ONLY: keep `partial` modifier — it uses `[GeneratedRegex(...)]` source generation (RESEARCH.md Anti-Pattern: "Removing the `partial` modifier from `ChatGptRequestContextParser`"). Mirror `DeckFlow.Core/Normalization/CardNormalizer.cs:5` which declares `public static partial class CardNormalizer` for the same reason.

---

### Pattern 6 — DI registration block (Wave 2 Program.cs edits)

**Applies to:** `DeckFlow.Web/Program.cs:263-295` (the three `AddScoped<IChatGptX>(sp => ...)` blocks)

**Canonical analog (in-place):** the current registration block itself — only the type identifiers change, the structural form is preserved.

**Current shape (`Program.cs:263-275` — to be renamed):**
```csharp
builder.Services.AddScoped<IChatGptDeckPacketService>(sp =>
    new ChatGptDeckPacketService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<IMechanicLookupService>(),
        sp.GetRequiredService<ICommanderBanListService>(),
        sp.GetRequiredService<IScryfallSetService>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetService<ILogger<ChatGptDeckPacketService>>()));
```

**Target shape after rename (identical structure, identifier-only changes):**
```csharp
builder.Services.AddScoped<IDeckAnalysisPacketService>(sp =>
    new DeckAnalysisPacketService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<IMechanicLookupService>(),
        sp.GetRequiredService<ICommanderBanListService>(),
        sp.GetRequiredService<IScryfallSetService>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetService<ILogger<DeckAnalysisPacketService>>()));
```

**Rules the executor MUST preserve:**
- Same lifetime (`AddScoped<>`, never change to Singleton).
- Same constructor-parameter ORDER (do not reorder — service ctor signature is unchanged per RESEARCH.md "service ctor signatures preserved across rename").
- `sp.GetRequiredService<T>()` for required deps; `sp.GetService<ILogger<T>>()` for the optional logger (per CONVENTIONS.md "Default `ILogger<T>` parameter to optional/nullable in services").
- Only THREE identifier surfaces change per block: the interface type arg, the implementation type in `new X(...)`, and the `ILogger<X>` generic argument.
- No new lines added; no lines removed. Identifier-only diff.

**Sibling reference for form check:** `Program.cs:300-308` shows simpler one-line `AddScoped<IFoo, Foo>()` registrations (`ICategorySuggestionService`, `ICommanderCategoryService`, `IDeckSyncService`). The renamed services keep the multi-line factory form because their constructors take many dependencies.

---

### Pattern 7 — Flat PascalCase enum with explicit integer values (Wave 1 enum edit)

**Applies to:** `DeckFlow.Web/Models/DeckPageTab.cs` (edit only — values renamed, integer values preserved)

**Canonical analog:** `DeckFlow.Web/Models/CategorySuggestionMode.cs:1-9` (full file)

**Layout to mirror:**
```csharp
namespace DeckFlow.Web.Models;

public enum CategorySuggestionMode
{
    CachedData = 0,
    ReferenceDeck = 1,
    ScryfallTagger = 2,
    All = 3,
}
```

**Current `DeckPageTab.cs` (to be edited):**
```csharp
namespace DeckFlow.Web.Models;

public enum DeckPageTab
{
    Sync = 0,
    SuggestCategories = 1,
    CommanderCategories = 2,
    CardLookup = 3,
    MechanicLookup = 4,
    ChatGptPackets = 5,
    Convert = 7,
    ChatGptDeckComparison = 8,
    ChatGptCedhMetaGap = 9,
    Home = 10,
    JudgeQuestions = 11,
}
```

**Target shape after rename:**
```csharp
namespace DeckFlow.Web.Models;

public enum DeckPageTab
{
    Sync = 0,
    SuggestCategories = 1,
    CommanderCategories = 2,
    CardLookup = 3,
    MechanicLookup = 4,
    DeckAnalysis = 5,
    Convert = 7,
    DeckComparison = 8,
    CedhMetaGap = 9,
    Home = 10,
    JudgeQuestions = 11,
}
```

**Rules:**
- Integer values 5, 8, 9 MUST be preserved (RESEARCH.md Wave 1: "keep enum integer values stable to avoid breaking any persisted serialization, even though `DeckPageTab` is not currently zip-stored").
- Member declaration order unchanged.
- Other (untouched) values stay byte-identical.
- This enum has NO doc comments today — RESEARCH.md D-03 covers public TYPES (classes, sealed classes, records, interfaces), and enums are listed alongside but the existing `DeckPageTab.cs` + `CategorySuggestionMode.cs` + `CedhMetaSortBy.cs` all currently ship without doc comments. **Recommendation:** match existing project tone — leave the enum without summary unless `NoWarn 1591` raises a warning specifically on the renamed enum values. (If `<summary>` IS added, it should match `CategorySuggestionMode` tone but no analog exists yet; defer this decision to discretion per D-03 wording.)

---

## Shared Patterns

### XML doc-comment tone (applies to every renamed type in Waves 1, 2, 4)

**Sources of truth (tone reference):**
- `DeckFlow.Web/Services/CardLookupService.cs:13-42` — terse single-sentence summaries
- `DeckFlow.Web/Services/CommanderSpellbookService.cs:13-54` — multiple records + interface + class, each with a one-line summary
- `DeckFlow.Web/Models/CardLookupViewModel.cs:1-42` — view-model property summaries
- `DeckFlow.Web/Models/CategorySuggestionRequest.cs:1-33` — request DTO with property summaries
- `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs:1-32` — static helper class summaries

**Tone rules (extracted from analogs):**
- ONE sentence per `<summary>`. No multi-line summaries on classes.
- Active voice. Verb-first ("Looks up...", "Returns...", "Builds...", "Represents...", "Gets...").
- For DTOs and view models, the class summary names the page/feature ("Represents the results of looking up a pasted card list.").
- For services, the class summary names the verb-object pair ("Looks up card lists via Scryfall's collection endpoint.").
- For result records, the summary states what's returned ("Returns the results of a card lookup.").
- For nested JSON response shapes, the summary states the JSON sub-tree the class maps to ("Pair of primary and backup win lines for a single deck.").
- Property summaries on view models start with "Gets" (because the property is `init`-only — see `CardLookupViewModel.cs:8-11`).
- Property summaries on request DTOs describe what the field controls ("Chooses whether...", "Describes whether...") — see `CategorySuggestionRequest.cs:8-31`.
- `<param>` and `<returns>` ONLY on non-trivial public methods where the parameter or return type is not self-explanatory (per CONVENTIONS.md "Use `<param>`/`<returns>` tags on non-trivial methods").
- "ChatGPT" as a narrative WORD inside a `<summary>` is PERMITTED (per CONTEXT.md D-07 #5: e.g., "Parses the ChatGPT-returned JSON payload into ...").
- Methods that override interface contracts use `/// <inheritdoc/>` instead of repeating the interface summary — see `CommanderSpellbookService.cs:84`.

**Anti-tone (do NOT produce):**
- ❌ "TODO add summary" placeholders (D-03 explicit prohibition)
- ❌ Vague generic text like "Helper class." or "Service implementation."
- ❌ Multi-sentence summaries
- ❌ Summaries that describe HOW (implementation) instead of WHAT (responsibility)
- ❌ Inserting `<remarks>` / `<example>` blocks (none exist in the analogs)

### Sealed-leaf rule (applies to every renamed class — Waves 1, 2, 4)

**Source:** `CONVENTIONS.md` "Classes: PascalCase, prefer `sealed` on leaf types" + every analog file checked above uses `sealed`.

**Rule:** Every renamed concrete class MUST be `public sealed class` or `public sealed record`. Test doubles inside `DeckControllerTests.cs` MUST be `private sealed class`. The existing pre-rename files already follow this — the rename preserves it.

### File-per-type rule with exception for service triplet and response-shape multi-class file (applies Waves 1, 2)

**Source:** `CONVENTIONS.md:7-9`:
> One public type per `.cs` file; file name matches the type name exactly. Interface and implementation often co-located in the same file (e.g., `ICardLookupService` + `ScryfallCardLookupService` + result records all live in `DeckFlow.Web/Services/CardLookupService.cs`).

**Rule:** After rename, the file name MUST match the LEAD public type name. Examples:
- `DeckAnalysisPacketService.cs` (lead type is the class `DeckAnalysisPacketService`; interface `IDeckAnalysisPacketService` and record `DeckAnalysisPacketResult` co-locate).
- `DeckAnalysisResponse.cs` (lead type is the class `DeckAnalysisResponse`; nested shapes `WeakSlot`, `QuestionAnswer`, `DeckVersion` co-locate).
- `MetaGapResponse.cs` (lead type is `MetaGapResponse`; 11 nested shape classes co-locate per existing precedent in pre-rename `ChatGptCedhMetaGapResponse.cs`).

**Pitfall 6 reminder (RESEARCH.md):** `ChatGptDeckAnalysisResponse.cs` filename already partially uses "DeckAnalysis" — the rename to `DeckAnalysisResponse.cs` drops BOTH the `ChatGpt` prefix AND the duplicate `Deck` qualifier ("DeckAnalysisResponse" is unambiguous).

### `[InternalsVisibleTo]` test seam (applies Wave 2 services, Wave 4 tests)

**Source:** `DeckFlow.Web/AssemblyInfo.cs:3` — `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]`

**Rule:** UNCHANGED — the renamed types may keep `internal` ctors (the test-seam delegate pattern per CONVENTIONS.md "Test seam pattern: optional `Func<...>` delegates injected via internal constructor"). The assembly attribute references the test PROJECT name, not any renamed class name. Do not touch `AssemblyInfo.cs`.

### Test-double naming (applies Wave 4 inline test classes in DeckControllerTests.cs)

**Source:** `CONVENTIONS.md:19` — "Test doubles: `Fake*` for stateful behavior fakes, `Stub*` for queue-driven stubs, `Throwing*` for exception injection."

**Rule:** The 6 inline private sealed classes inside `DeckControllerTests.cs` (lines 775-887 per RESEARCH.md Wave 4) rename in lockstep but PRESERVE prefixes:
- `FakeChatGptDeckPacketService` → `FakeDeckAnalysisPacketService` (Fake* preserved)
- `FakeChatGptDeckComparisonService` → `FakeDeckComparisonService`
- `FakeChatGptCedhMetaGapService` → `FakeMetaGapService`
- `ConfigurableChatGptCedhMetaGapService` → `ConfigurableMetaGapService` (Configurable* preserved — established stateful-config-fake variant)
- `ThrowingChatGptCedhMetaGapService` → `ThrowingMetaGapService` (Throwing* preserved)
- `ThrowingChatGptDeckPacketService` → `ThrowingDeckAnalysisPacketService`

These STAY inline as `private sealed class` declarations — do not promote to separate files (RESEARCH.md Anti-Pattern: "Renaming inline test doubles into separate files... out of scope for a rename").

---

## No Analog Found

**None.** Every renamed file follows an existing project pattern. Even the 12-nested-shape `MetaGapResponse.cs` precedent IS the very file being renamed (`ChatGptCedhMetaGapResponse.cs`); the layout is self-analogous.

The only "new style" decision the planner needs to flag is the **`ChatGptResultWrapInstruction` const inside `JsonTextFormatterService.cs`** (RESEARCH.md L573 allowlist row). This is Claude's Discretion per D-01 — the executor can either:
- Rename to `ResultWrapInstruction` (recommended — descriptive without prefix; matches `ApiUrl`, `MaxIncluded` static-readonly tone in `CommanderSpellbookService.cs:56-58`)
- Rename to `AiResultWrapInstruction` (more explicit)
- Leave as `ChatGptResultWrapInstruction` and add it to the verification grep allowlist

No analog in the codebase forces this choice. Document the decision in the wave 2 commit message either way.

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Services/` (CardLookupService.cs, CommanderSpellbookService.cs, UpstreamErrorMessageBuilder.cs, CategorySuggestionMessageBuilder.cs)
- `DeckFlow.Web/Models/` (CardLookupRequest.cs, CategorySuggestionRequest.cs, CardLookupViewModel.cs, CategorySuggestionMode.cs, CedhMetaSortBy.cs, ChatGptCedhMetaGapResponse.cs, DeckPageTab.cs)
- `DeckFlow.Core/Models/` (DeckEntry.cs — sealed record `init/required` reference)
- `DeckFlow.Core/Normalization/` (CardNormalizer.cs — `public static partial class` + `[GeneratedRegex]`)
- `DeckFlow.Core/Integration/` (MoxfieldApiUrl.cs, ArchidektApiUrl.cs — static helper reference; note these live in DeckFlow.Core which lacks `<GenerateDocumentationFile>true`, so they have NO summaries — use Web/Services UpstreamErrorMessageBuilder as the doc-tone analog instead)
- `DeckFlow.Web/Program.cs:263-308` (DI registration form)

**Files scanned:** 14
**Pattern extraction date:** 2026-05-17
**Patterns identified:** 7 file-level patterns + 5 shared cross-cutting rules
**New patterns introduced:** 0 (this is a pure rename phase)

## PATTERN MAPPING COMPLETE
