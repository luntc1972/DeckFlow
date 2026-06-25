---
phase: quick-260624-opb
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
  - DeckFlow.Web/Controllers/ManabaseController.cs
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs
  - DeckFlow.Web/e2e/manabase-download.spec.ts
autonomous: true
requirements: [QUICK-260624-opb]
must_haves:
  truths:
    - "After analyzing a deck, the user sees a Download button on the result panel."
    - "Clicking Download returns a text file (Content-Disposition attachment) containing the full mana-base verdict the page shows."
    - "The downloaded report is paste-ready into ChatGPT/Claude without reformatting."
    - "The download re-uses the existing analysis service, not a duplicated pipeline."
    - "The download button does not overflow on desktop or mobile across themes."
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs"
      provides: "Pure ManabaseReport -> paste-ready plain-text/markdown report builder"
      contains: "ManabaseReportTextBuilder"
    - path: "DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs"
      provides: "Unit coverage for the report-text builder"
    - path: "DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs"
      provides: "Controller download action coverage (File result, headers, validation)"
    - path: "DeckFlow.Web/e2e/manabase-download.spec.ts"
      provides: "Playwright smoke: download control present + wired, no overflow"
  key_links:
    - from: "DeckFlow.Web/Controllers/ManabaseController.cs"
      to: "ManabaseReportTextBuilder.Build"
      via: "download action calls the Core builder"
      pattern: "ManabaseReportTextBuilder\\.Build"
    - from: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      to: "/manabase/download"
      via: "formaction submit button in the result panel"
      pattern: "manabase/download"
---

<objective>
Let a user download the mana-base analysis they just ran as a paste-ready text file.

The /manabase result panel currently renders a deterministic Karsten §6 report (land target, ramp,
per-color sources, health verdict, biggest fix, optional castability table, "this deck's numbers"),
but there is no way to save it. The deck source is in the form, but the computed `ManabaseReport` is
NOT persisted server-side — so the download path re-submits the same form and re-runs the existing
`IManabaseAnalysisService.AnalyzeAsync`, then serializes the report to a downloadable file. This
exactly mirrors the established sibling pattern in `DeckLookupController` (`/card-lookup/download`):
a `formaction` POST that returns `File(Encoding.UTF8.GetBytes(...), "text/plain; charset=utf-8",
"...-{timestamp}.txt")`.

Per DeckFlow's core value, the file must be paste-ready into ChatGPT/Claude without reformatting.

Purpose: close the "I want to keep / share / paste this analysis" gap on the manabase tool.
Output: a Core report-text builder (+ tests), a controller download action (+ tests), a Download
button in the result panel, and a Playwright smoke test (desktop + mobile).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<interfaces>
<!-- Contracts the executor needs. Extracted from the codebase — do NOT re-explore to find these. -->

The report the download must serialize (DeckFlow.Core/Manabase/ManabaseModels.cs):

  public sealed record ManabaseReport
  {
      public required int ActualLands { get; init; }
      public required double TargetLands { get; init; }
      public double LandDelta { get; }                       // ActualLands - TargetLands
      public required IReadOnlyList<ColorSourceFinding> ColorFindings { get; init; }
      public ColorSourceFinding? WeakestColor { get; }
      public ManabaseHealth Health { get; }                  // Healthy/Functional/Workable/NeedsWork
      public bool LandShortfallCoveredByRamp { get; }
      public IReadOnlyList<DemandingCard> DemandingCards { get; init; }   // Name, CastPercent
      public ManabaseMode Mode { get; init; }                // Casual | Cedh
      public IReadOnlyList<CardCastability> Castability { get; init; }    // Name, ManaValue, OnCurveTurn, CastPercent, LimitingFactor, IsCommander, IsCostOverridden, AverageDelay
      public IReadOnlyDictionary<ManaColor,int> ColorSpellCounts { get; init; }
      public IReadOnlyList<ManaColor> CommanderColors { get; init; }
      public ManabaseLandTargetBreakdown? LandTarget { get; init; }
      public int RampSourceCount { get; init; }
      public IReadOnlyList<string> RampSourceNames { get; init; }
      public IReadOnlyList<string> RampAndDrawNames { get; init; }
      public IReadOnlyList<UnsupportedInteraction> UnsupportedInteractions { get; init; }  // Name, Reason
      public required string Summary { get; init; }
      public ManabasePrimaryFix PrimaryFix { get; }          // Kind, Color?, Amount, ActualSources, RequiredSources, Spell, DemandingCount
  }

  ColorSourceFinding: Color, ActualSources(double), RequiredSources(int), DrivingSpell,
      UnderSupportedCount, DirectSources, SharedSources, ConditionalSources,
      Deficit(=>Required-Actual), IsAdequate, NeedsMoreSources.

  ManabaseFixKind: None | ColorSources | Lands | DemandingCards.
  ManabaseHealth labels (display): Healthy="Excellent", Functional="Solid", Workable="Workable",
      NeedsWork="Needs work" (see ManabaseDisplay.HealthLabel — Web side; the Core builder must
      hard-code the equivalent labels since ManabaseDisplay lives in DeckFlow.Web.Models and Core
      cannot reference Web).

The analysis service the download action re-runs (DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs):

  Task<ManabaseAnalysisResult> AnalyzeAsync(string deckSource, string? deckName,
      ManabaseAnalysisOptions? options = null, CancellationToken ct = default);
  // ManabaseAnalysisResult(ManabaseReport Report, string InputSummary,
  //   IReadOnlyList<string> Unresolved, string? ImportWarning, string ChatGptSwapPrompt,
  //   IReadOnlyList<CostSuggestion> Suggestions)
  // ManabaseAnalysisOptions { ManabaseMode Mode; CommanderImportance CommanderImportance;
  //   IReadOnlyDictionary<string,string>? CostOverrides }

The canonical sibling download pattern (DeckLookupController.cs:212-280) — copy its shape:
  - validate request (re-render the view with ErrorMessage when input missing),
  - call the service, build the string, then
    return File(Encoding.UTF8.GetBytes(output), "text/plain; charset=utf-8", $"manabase-analysis-{timestamp}.txt");
    timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss").

The controller's existing analyze action (ManabaseController.cs:118-201) shows the exact request
normalization to mirror: coerce request.Mode / request.CommanderImportance via Enum.IsDefined,
CreateTimeoutScope(LookupTimeout), ManabaseCostOverrideParser.Parse(request.CostOverridesText),
and the [ValidateAntiForgeryToken] + [FeatureFlagGate("feature.manabase.enabled", ...)] attributes.

ManabaseRequest binds: DeckInputSource, DeckUrl, DeckText, DeckName, Mode, CommanderImportance,
CostOverridesText, plus a DeckSource property the service consumes.

The view's existing form (Manabase.cshtml:28-146) posts to ~/manabase with @Html.AntiForgeryToken().
The result panel is the `<section class="result-panel" data-scroll-on-load>` block at line 150.
The existing action toolbar uses: <div class="toolbar manabase-actions"> with
<button type="submit" class="run-button" formaction="...">. The ChatGPT swap-prompt block already
demonstrates an in-result control (a copy-button).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Core ManabaseReportTextBuilder + unit tests</name>
  <files>DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs, DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs</files>
  <behavior>
    - Build(report, deckName, decklistText, mode) returns a non-empty paste-ready string that
      contains: a title line, the deck name when supplied, the mode label ("Casual"/"cEDH"), the
      land line ("Lands: {ActualLands} vs ~{TargetLands:F1}"), the health verdict label
      ("Excellent"/"Solid"/"Workable"/"Needs work"), and the Summary.
    - Includes a per-color source table/section: each ColorFinding's Color, ActualSources (F1),
      RequiredSources, Deficit/"OK", and DrivingSpell.
    - Includes the "biggest fix" line derived from report.PrimaryFix.Kind (ColorSources / Lands /
      DemandingCards / None) mirroring the view's wording, and never emits a negative "add ~-N"
      (PrimaryFix already guards this; assert the None and Lands branches).
    - When report.Castability is non-empty AND mode == Casual, includes a castability section
      (Card, MV, CastPercent%, limiting factor); when mode == Cedh or empty, omits it.
    - Lists RampSourceNames / RampAndDrawNames when present, and UnsupportedInteractions when present.
    - Appends the decklist text at the end when supplied (mirrors the swap-prompt builder).
    - All numeric formatting uses CultureInfo.InvariantCulture (match ManabaseSwapPromptBuilder).
    - Test cases (xUnit, DeckFlow.Core.Tests, mirror ManabaseSwapPromptBuilderTests style):
      * Build_HealthyCasualReport_ContainsLandsHealthSummaryAndColorRows
      * Build_CedhReport_OmitsCastabilitySection
      * Build_CasualReportWithCastability_IncludesCastabilityRows
      * Build_PrimaryFixLands_EmitsAddLandsLineNotNegative
      * Build_PrimaryFixNone_EmitsEveryColorAdequate
      * Build_WithRampAndUnsupportedInteractions_ListsThem
      * Build_WithDeckName_IncludesName / Build_BlankDeckName_OmitsNameDecoration
      * Build_NullReport_Throws (ArgumentNullException.ThrowIfNull, like the swap builder)
  </behavior>
  <action>
    Create `public static class ManabaseReportTextBuilder` in DeckFlow.Core/Manabase with a single
    public `Build(ManabaseReport report, string? deckName, string? decklistText, ManabaseMode mode = ManabaseMode.Casual)`
    returning a paste-ready plain-text report. Model the structure and culture handling on
    ManabaseSwapPromptBuilder (same namespace, same StringBuilder + CultureInfo.InvariantCulture
    discipline). Hard-code the four health labels in Core (Core cannot reference ManabaseDisplay,
    which lives in DeckFlow.Web.Models) — keep them identical to ManabaseDisplay.HealthLabel:
    Healthy->"Excellent", Functional->"Solid", Workable->"Workable", NeedsWork->"Needs work".
    Reproduce the view's "biggest fix" wording per PrimaryFix.Kind. Use XML doc comments on the
    public type and method per project convention. Do NOT new up or call any HTTP/service — this is
    pure formatting of an already-computed report. Write the failing tests first (RED), then implement.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.Core/DeckFlow.Core.csproj -warnaserror; dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ManabaseReportTextBuilderTests"</automated>
  </verify>
  <done>ManabaseReportTextBuilder.Build returns the paste-ready report; all new Core unit tests pass; Core builds clean (0 warnings on changed lines).</done>
</task>

<task type="auto">
  <name>Task 2: Controller download action + view button + CSS + Web tests</name>
  <files>DeckFlow.Web/Controllers/ManabaseController.cs, DeckFlow.Web/Views/Deck/Manabase.cshtml, DeckFlow.Web/wwwroot/css/site-common.css, DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs</files>
  <action>
    Controller: add `[HttpPost("/manabase/download")]` action `Download(ManabaseRequest request)`
    with the SAME attributes as the analyze action ([ValidateAntiForgeryToken] +
    [FeatureFlagGate("feature.manabase.enabled", Title=..., Message=...)]). Mirror the analyze
    action body exactly (ManabaseController.cs:123-201): null-coalesce the request, coerce
    request.Mode / request.CommanderImportance via Enum.IsDefined, open a CreateTimeoutScope(LookupTimeout),
    call `_manabaseAnalysisService.AnalyzeAsync(request.DeckSource, request.DeckName, new ManabaseAnalysisOptions { Mode=..., CommanderImportance=..., CostOverrides = ManabaseCostOverrideParser.Parse(request.CostOverridesText) }, token)`.
    On success, build text via `ManabaseReportTextBuilder.Build(result.Report, request.DeckName, decklistText: null, request.Mode)`
    and `return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", $"manabase-analysis-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt")`.
    On the same exception families the analyze action catches (OperationCanceled timeout,
    InvalidOperationException, HttpRequestException, Exception), re-render `View("Manabase", new ManabaseViewModel { Request = request, ErrorMessage = ... })`
    with the identical user-facing messages (so a download failure looks like an analyze failure, not a 500).
    Add `using System.Text;` only if not already implied by ImplicitUsings (System.Text.Encoding is
    available via using System.Text — add the directive, sorted System.* first).

    View (Manabase.cshtml): inside the result panel `<section class="result-panel" ...>` (after the
    castability/unsupported blocks, before the closing of the result section near line 417), add a
    download control as a submit button that re-posts the SAME form via formaction. The cleanest
    wiring that reuses the already-bound deck inputs is to render the button INSIDE the existing
    analyze `<form method="post" action="~/manabase">` is NOT possible (the result panel is outside
    the form). Instead add a small dedicated mini-form in the result panel that re-posts the deck
    source: render a `<form method="post" action="@Url.Content("~/manabase/download")" class="toolbar manabase-download">`
    containing `@Html.AntiForgeryToken()` and hidden inputs carrying the current request so the
    re-run is identical: DeckInputSource, DeckUrl, DeckText, DeckName, Mode, CommanderImportance,
    CostOverridesText (bind each from Model.Request, matching the form field names exactly), plus a
    `<button type="submit" class="run-button manabase-download-button" data-no-busy>Download analysis (.txt)</button>`.
    Only render this block when `Model.HasResult`. Keep wording paste-ready-oriented; place it near
    the ChatGPT swap-prompt disclosure so "save / paste" actions are grouped.

    CSS (site-common.css — NOT site.css): add a small rule block `.toolbar.manabase-download { ... }`
    if spacing/margins are needed (mirror the existing `.toolbar.manabase-actions` block at line 2263:
    justify-content: flex-start; flex-wrap: wrap; gap). Do NOT add layout to site.css or any theme
    fork. Reuse the existing `.run-button` styling (already theme-aware) — add no new color tokens.

    Web tests (DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs, mirror
    ManabaseControllerModeTests for the fake-service test-seam pattern):
      * Download_ValidDeck_ReturnsFileResultWithTextContentTypeAndTimestampedName
        (assert FileContentResult, ContentType "text/plain; charset=utf-8",
         FileDownloadName matches /^manabase-analysis-\d{8}-\d{6}\.txt$/, and the bytes decode to a
         string containing the report Summary).
      * Download_InvalidEnumValues_CoercedToDefaults (out-of-range Mode/CommanderImportance still
        produce a file, not a 500 — mirrors the analyze action's MEDIUM-1 guard).
      * Download_ServiceThrowsInvalidOperation_RendersViewWithErrorMessage (returns ViewResult with
        ManabaseViewModel.ErrorMessage set, not a File).
      * Download_ServiceThrowsHttpRequestException_RendersUpstreamErrorView.
    Use the existing fake/stub analysis-service seam these sibling tests already use; do not add a
    mocking library.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.Web/DeckFlow.Web.csproj -warnaserror; dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~ManabaseControllerDownloadTests"</automated>
  </verify>
  <done>POST /manabase/download returns a timestamped text-file attachment of the report; failures re-render the page with a friendly error; the Download button renders only with a result; new Web tests pass; build clean on changed lines; no layout CSS added to site.css.</done>
</task>

<task type="auto">
  <name>Task 3: Playwright download smoke + theme/mobile overflow check</name>
  <files>DeckFlow.Web/e2e/manabase-download.spec.ts</files>
  <action>
    Create an e2e spec (mirror DeckFlow.Web/e2e/manabase.spec.ts conventions: import { expect, test }
    from '@playwright/test'; assert no console errors). Because the result panel only appears after a
    real analysis (which calls Scryfall and is covered by xUnit), do NOT submit a real deck here.
    Instead cover the wiring that does not need a live analysis:
      * The download mini-form is absent on a fresh GET /manabase (no result yet) — assert
        `page.locator('form[action="/manabase/download"]')` is not attached.
      * Assert the download endpoint contract on the page when a result is simulated is out of scope
        for a no-Scryfall smoke; instead assert the button's static markup contract via a focused DOM
        check IF a lightweight result fixture exists, otherwise keep the smoke to: (a) page renders,
        (b) the analyze form still posts to /manabase, (c) no console errors — and add an explicit
        comment that the File-result behavior is covered by ManabaseControllerDownloadTests.
      * Overflow/theme guard: reuse the project's responsive pattern (see e2e/ui-responsive.spec.ts)
        — load /manabase, and on both the chromium-desktop and chromium-mobile projects assert no
        horizontal scroll (document.scrollingElement.scrollWidth <= clientWidth + 1). The download
        button reuses .run-button, so this guards that the new control does not widen the page.
    Keep the spec runnable under both projects in playwright.config.ts (chromium-desktop,
    chromium-mobile); do not hard-code a viewport.
  </action>
  <verify>
    <automated>cd DeckFlow.Web &amp;&amp; npx --no-install playwright test e2e/manabase-download.spec.ts --project=chromium-desktop --project=chromium-mobile</automated>
  </verify>
  <done>manabase-download.spec.ts passes on chromium-desktop and chromium-mobile: GET /manabase has no download form, the analyze form still posts to /manabase, no console errors, and the page has no horizontal overflow on either viewport.</done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln` is clean (0 errors; 0 new warnings on changed lines per the format gate).
- Core: ManabaseReportTextBuilderTests all green.
- Web: ManabaseControllerDownloadTests all green; existing Manabase tests unaffected.
- e2e: manabase-download.spec.ts green on chromium-desktop + chromium-mobile (per the project rule:
  run the Playwright suite, start the server with dotnet.exe + DECKFLOW_DISABLE_AUTO_BROWSER=true +
  admin creds, reuseExistingServer attaches).
- Manual (operator, optional): run a real deck on /manabase, click "Download analysis (.txt)", confirm
  the file downloads with a manabase-analysis-*.txt name and pastes cleanly into ChatGPT.
- Changed-lines format gate: `scripts/format-check-changed.sh staged` passes; raw-string literals and
  `{ get; init; }` untouched (no carve-out violations).
</verification>

<success_criteria>
- A Download button appears on the /manabase result panel only after an analysis.
- Clicking it returns a paste-ready `manabase-analysis-{timestamp}.txt` attachment containing the full
  verdict (lands, health, per-color sources, biggest fix, castability when Casual, ramp, summary).
- The download re-uses `IManabaseAnalysisService.AnalyzeAsync` (no duplicated analysis pipeline) and a
  pure, unit-tested `ManabaseReportTextBuilder` in DeckFlow.Core.
- Tests ship in the same change: Core unit tests + Web controller tests + Playwright smoke.
- No overflow on desktop or mobile; layout CSS lives only in site-common.css; no new NuGet packages.
</success_criteria>

<output>
Create `.planning/quick/260624-opb-be-able-to-download-the-manabase-analysi/260624-opb-SUMMARY.md` when done.
</output>
