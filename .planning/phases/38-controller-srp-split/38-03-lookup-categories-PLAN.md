---
phase: 38-controller-srp-split
plan: 03
type: execute
wave: 3
depends_on: ["38-01", "38-02"]
files_modified:
  - DeckFlow.Web/Controllers/DeckLookupController.cs
  - DeckFlow.Web/Controllers/DeckCategoriesController.cs
  - DeckFlow.Web/Controllers/DeckController.cs
autonomous: true
requirements: [SRP-01]
must_haves:
  truths:
    - "GET /card-lookup, POST /card-lookup/download(-json), GET /card-lookup/single render/behave as before"
    - "GET/POST /mechanic-lookup render/behave as before"
    - "GET/POST /suggest-categories and GET /suggest-categories/card-search render/behave as before, including the 20s suggestion timeout and feature-flag gate"
  artifacts:
    - path: "DeckFlow.Web/Controllers/DeckLookupController.cs"
      provides: "Lookup family: card-lookup (page/download/download-json/single), mechanic-lookup (GET/POST)"
      contains: "class DeckLookupController"
    - path: "DeckFlow.Web/Controllers/DeckCategoriesController.cs"
      provides: "Categories: GET/POST /suggest-categories, GET /suggest-categories/card-search"
      contains: "class DeckCategoriesController"
  key_links:
    - from: "DeckFlow.Web/Controllers/DeckLookupController.cs"
      to: "ICardLookupService"
      via: "LookupAsync / LookupSingleAsync"
      pattern: "_cardLookupService\\.Lookup"
    - from: "DeckFlow.Web/Controllers/DeckCategoriesController.cs"
      to: "ICategorySuggestionService"
      via: "SuggestAsync with timeout scope"
      pattern: "_categorySuggestionService\\.SuggestAsync"
---

<objective>
Extract the remaining two simple feature families out of `DeckController`: `DeckLookupController` (card-lookup page + downloads + single-card JSON + mechanic-lookup) and `DeckCategoriesController` (suggest-categories page/POST + card-search typeahead). Both inherit `DeckToolControllerBase`. DeckCategoriesController adopts the base's timeout idiom for the 20s suggestion budget. After this plan, the only family left in DeckController is the ChatGPT-packet group (handled in Plan 04).

Purpose: SRP-01 — continue the by-family split. Lookup owns `BuildVerificationFile` + the `CardLookupDownloadFormat` enum + `DownloadCardLookupAsync`; Categories owns `HasSuggestionInput` and is the sole remaining user of the suggestion timeout.

Output: Two new controller files; `DeckController.cs` slimmed; `_cardSearchService`, `_cardLookupService`, `_mechanicLookupService`, `_categorySuggestionService` and the `SuggestionTimeout` const removed from DeckController (the const now lives on the base from Plan 01).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/38-controller-srp-split/38-CONTEXT.md

@DeckFlow.Web/Controllers/DeckController.cs
@DeckFlow.Web/Controllers/DeckToolControllerBase.cs
@DeckFlow.Web/Models/DeckPageTab.cs

<interfaces>
DeckLookupController owns (move verbatim, preserve attributes + bodies):
- HttpGet "/card-lookup" -> CardLookup()                                  // DeckController.cs ~L121-128
- HttpPost "/card-lookup/download" + ValidateAntiForgeryToken -> DownloadCardLookup(CardLookupRequest)  // ~L334-339
- HttpPost "/card-lookup/download-json" + ValidateAntiForgeryToken -> DownloadCardLookupJson(CardLookupRequest)  // ~L345-350
- HttpGet "/card-lookup/single" -> async SingleCardLookup(string? name)   // ~L356-416
- HttpGet "/mechanic-lookup" -> MechanicLookup()                          // ~L133-140
- HttpPost "/mechanic-lookup" + ValidateAntiForgeryToken -> async MechanicLookup(MechanicLookupRequest)  // ~L422-475
- private async DownloadCardLookupAsync(CardLookupRequest, CardLookupDownloadFormat)  // ~L1482-1532
- private static string BuildVerificationFile(CardLookupResult)          // ~L1538-1550
- private enum CardLookupDownloadFormat { Text, Json }                    // ~L1552-1556
- Dependencies: ICardLookupService _cardLookupService, IMechanicLookupService _mechanicLookupService
- Note: SingleCardLookup also calls _mechanicLookupService.LookupAsync for detected mechanics — both services belong to this controller.
- Static helper: UpstreamErrorMessageBuilder.BuildScryfallMessage

DeckCategoriesController owns:
- HttpGet "/suggest-categories" + FeatureFlagGate(...) -> SuggestCategories()  // ~L103-116 (carry the full [FeatureFlagGate] attribute verbatim)
- HttpPost "/suggest-categories" + FeatureFlagGate(...) + ValidateAntiForgeryToken -> async SuggestCategories(CategorySuggestionRequest)  // ~L1562-1643
- HttpGet "/suggest-categories/card-search" -> async CardSearch(string query)  // ~L301-317
- private static bool HasSuggestionInput(CategorySuggestionRequest)       // ~L1817-1820
- Dependencies: ICategorySuggestionService _categorySuggestionService, ICardSearchService _cardSearchService
- The POST SuggestCategories uses the 20s timeout idiom (DeckController.cs L1596-1598). Replace the inline CreateLinkedTokenSource(...).CancelAfter(SuggestionTimeout) with the base helper: `using var timeoutCts = CreateTimeoutScope(SuggestionTimeout);` then `var cancellationToken = timeoutCts.Token;`. SuggestionTimeout now resolves to the protected base const (Plan 01). Behavior is identical (same 20s, same linked token).
- Static helpers used (stay put, just call): CategorySuggestionMessageBuilder, CategorySuggestionReporter, UpstreamErrorMessageBuilder.

After both moves, DeckController retains ONLY the packet-family actions + the 4 packet services + PacketSessionCache (Plan 04 finishes the job).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create DeckLookupController (card + mechanic lookup family)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L118-140, 330-475, 1477-1556 — lookup actions + DownloadCardLookupAsync + BuildVerificationFile + CardLookupDownloadFormat enum)
    - DeckFlow.Web/Controllers/DeckToolControllerBase.cs
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckLookupController.cs (CRLF — new file). Declare public sealed class DeckLookupController : DeckToolControllerBase. Ctor injects ICardLookupService cardLookupService, IMechanicLookupService mechanicLookupService, ILogger<DeckLookupController> logger — all ThrowIfNull-guarded, private readonly fields. XML doc.
    MOVE verbatim (attributes + bodies byte-for-byte): CardLookup() (HttpGet "/card-lookup"), DownloadCardLookup (HttpPost "/card-lookup/download"), DownloadCardLookupJson (HttpPost "/card-lookup/download-json"), SingleCardLookup (HttpGet "/card-lookup/single"), MechanicLookup() (HttpGet "/mechanic-lookup"), MechanicLookup(MechanicLookupRequest) (HttpPost "/mechanic-lookup"), plus the private members DownloadCardLookupAsync, BuildVerificationFile, and the private enum CardLookupDownloadFormat.
    DELETE those members from DeckController.cs. Remove _cardLookupService and _mechanicLookupService field/param/assignment from DeckController (grep both after the move; their only users are the moved actions, so remove both).
    Add usings by grepping references: Microsoft.AspNetCore.Mvc, System.Net, System.Text (Encoding), System.Text.Json, DeckFlow.Web.Infrastructure, DeckFlow.Web.Models, DeckFlow.Web.Services. Verify by build.
    Preserve LF in DeckController.cs; touch only moved/deleted lines.
  </action>
  <acceptance_criteria>
    - DeckLookupController.cs declares public sealed class DeckLookupController : DeckToolControllerBase, ctor guarded.
    - grep -nE "_cardLookupService|_mechanicLookupService|BuildVerificationFile|CardLookupDownloadFormat|\"/card-lookup|\"/mechanic-lookup" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - Retains attribute strings: HttpGet "/card-lookup", HttpPost "/card-lookup/download", HttpPost "/card-lookup/download-json", HttpGet "/card-lookup/single", HttpGet "/mechanic-lookup", HttpPost "/mechanic-lookup".
    - Web csproj builds: 0 errors, 0 new warnings.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; grep -cE "_cardLookupService|_mechanicLookupService|BuildVerificationFile|/card-lookup|/mechanic-lookup" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>DeckLookupController owns the full card+mechanic lookup family; DeckController no longer references those services or actions; Web build clean.</done>
</task>

<task type="auto">
  <name>Task 2: Create DeckCategoriesController (suggest-categories + card-search), adopt base timeout helper</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L100-116, 297-317, 1558-1643, 1813-1820 — category actions + HasSuggestionInput + the SuggestionTimeout usage)
    - DeckFlow.Web/Controllers/DeckToolControllerBase.cs (CreateTimeoutScope + SuggestionTimeout)
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckCategoriesController.cs (CRLF — new file). Declare public sealed class DeckCategoriesController : DeckToolControllerBase. Ctor injects ICategorySuggestionService categorySuggestionService, ICardSearchService cardSearchService, ILogger<DeckCategoriesController> logger — all ThrowIfNull-guarded, private readonly fields. XML doc.
    MOVE verbatim (preserve the full [FeatureFlagGate(...)] attribute on both GET and POST SuggestCategories, plus [ValidateAntiForgeryToken] on POST): SuggestCategories() (HttpGet "/suggest-categories"), SuggestCategories(CategorySuggestionRequest) (HttpPost "/suggest-categories"), CardSearch(string query) (HttpGet "/suggest-categories/card-search"), and private static bool HasSuggestionInput.
    In the moved POST SuggestCategories body, replace the inline timeout idiom (using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted); timeoutCts.CancelAfter(SuggestionTimeout);) with the base helper call: using var timeoutCts = CreateTimeoutScope(SuggestionTimeout); — keep the subsequent `var cancellationToken = timeoutCts.Token;` line unchanged. SuggestionTimeout resolves to the protected base const. This is the ONLY body edit permitted; everything else moves byte-for-byte. Why: D-03 — the timeout wrapper is the cross-cutting bit the base owns; the OperationCanceledException catch + 20s behavior are unchanged.
    DELETE those members from DeckController.cs. After the move, DeckController no longer uses _categorySuggestionService (remove field/param/assignment) nor _cardSearchService (remove field/param/assignment — Plan 02 deliberately retained it for this plan; now both its users, ConvertCommanderSearch already moved and CardSearch moving here, are gone — confirm by grep then remove). Also remove the now-orphaned `private static readonly TimeSpan SuggestionTimeout` from DeckController if still present (it should have been the base's; confirm it is no longer referenced anywhere in DeckController and delete the field).
    Add usings by grepping references: Microsoft.AspNetCore.Mvc, System.Net, DeckFlow.Core.Models (DeckInputSource, CategorySuggestionMode), DeckFlow.Core.Parsing (DeckParseException), DeckFlow.Web.Infrastructure (FeatureFlagGate, UpstreamErrorMessageBuilder), DeckFlow.Web.Models, DeckFlow.Web.Services. Verify by build.
  </action>
  <acceptance_criteria>
    - DeckCategoriesController.cs declares public sealed class DeckCategoriesController : DeckToolControllerBase, ctor guarded, with the [FeatureFlagGate(...)] attribute present on both SuggestCategories overloads (grep "FeatureFlagGate" returns 2 in the new file).
    - The POST SuggestCategories uses CreateTimeoutScope(SuggestionTimeout) (grep finds it) and no longer contains CreateLinkedTokenSource in this controller's body.
    - grep -nE "_categorySuggestionService|_cardSearchService|HasSuggestionInput|SuggestionTimeout|\"/suggest-categories" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - Retains attribute strings: HttpGet "/suggest-categories", HttpPost "/suggest-categories", HttpGet "/suggest-categories/card-search".
    - Web csproj builds: 0 errors, 0 new warnings.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; grep -cE "_categorySuggestionService|_cardSearchService|SuggestionTimeout|/suggest-categories" DeckFlow.Web/Controllers/DeckController.cs; grep -c "CreateTimeoutScope" DeckFlow.Web/Controllers/DeckCategoriesController.cs</automated>
  </verify>
  <done>DeckCategoriesController owns the categories family, uses the base timeout helper, preserves the feature-flag gate; DeckController retains only packet-family services; Web build clean.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> lookup/category routes | Pre-existing; unchanged. Same routes, same feature-flag gate, same antiforgery tokens, same 20s suggestion timeout. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-03 | Denial of Service | /suggest-categories POST (long upstream calls) | accept | Existing 20s linked-token timeout is preserved verbatim via CreateTimeoutScope; no change to the protection. No new attack surface — pure move refactor. |
| T-38-04 | Tampering | /suggest-categories feature-flag gate | accept | [FeatureFlagGate] attribute moves byte-for-byte with both action overloads; gating behavior identical pre/post. |

No package installs; no new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- Web project builds clean after both tasks.
- DeckController retains ONLY the packet-family actions + IDeckAnalysisPacketService, IDeckPrimerPacketService, IDeckComparisonService, IMetaGapService, PacketSessionCache (verified by grep — no lookup/category/sync/convert/shell services remain).
- Moved actions retain exact attribute strings; the SuggestionTimeout const lives only on the base now.
</verification>

<success_criteria>
- DeckLookupController and DeckCategoriesController exist as sealed controllers inheriting the base, each injecting only its own services.
- All lookup + category URLs, tabs, feature-flag gate, and timeout behavior preserved.
- DeckController reduced to the packet family only.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-03-SUMMARY.md` when done. Record moved members, that _cardSearchService + SuggestionTimeout were removed from DeckController, and that the POST SuggestCategories adopted CreateTimeoutScope. Confirm Web build clean.
</output>
