---
phase: 38-controller-srp-split
plan: 02
type: execute
wave: 2
depends_on: ["38-01"]
files_modified:
  - DeckFlow.Web/Controllers/DeckSyncController.cs
  - DeckFlow.Web/Controllers/DeckConvertController.cs
  - DeckFlow.Web/Controllers/JudgeQuestionsController.cs
  - DeckFlow.Web/Controllers/DeckController.cs
autonomous: true
requirements: [SRP-01]
must_haves:
  truths:
    - "GET/POST /sync and POST /resolve render the DeckSync view exactly as before"
    - "GET/POST /convert and GET /convert/commander-search behave exactly as before"
    - "GET /judge-questions renders the JudgeQuestions view with optional ?card pre-fill"
  artifacts:
    - path: "DeckFlow.Web/Controllers/DeckSyncController.cs"
      provides: "Sync family: GET/POST /sync, POST /resolve + sync helpers"
      contains: "class DeckSyncController"
    - path: "DeckFlow.Web/Controllers/DeckConvertController.cs"
      provides: "Convert family: GET/POST /convert, GET /convert/commander-search"
      contains: "class DeckConvertController"
    - path: "DeckFlow.Web/Controllers/JudgeQuestionsController.cs"
      provides: "GET /judge-questions"
      contains: "class JudgeQuestionsController"
  key_links:
    - from: "DeckFlow.Web/Controllers/DeckSyncController.cs"
      to: "IDeckSyncService"
      via: "CompareDecksAsync"
      pattern: "_deckSyncService\\.CompareDecksAsync"
    - from: "DeckFlow.Web/Controllers/DeckConvertController.cs"
      to: "IDeckConvertService"
      via: "ConvertAsync"
      pattern: "_deckConvertService\\.ConvertAsync"
---

<objective>
Extract three feature controllers out of `DeckController`: `DeckSyncController` (sync + resolve), `DeckConvertController` (convert + commander typeahead), and `JudgeQuestionsController` (the standalone judge-questions GET). Each is a `sealed` controller inheriting `DeckToolControllerBase` (from Plan 01) and injects ONLY the services its actions use. All routes, view names, and active-tab values move unchanged.

Purpose: SRP-01 — split DeckController by tool family. Sync, Convert, and JudgeQuestions are the cleanest families (no packet/cache/timeout complexity), so they extract first with the lowest risk.

Output: Three new controller files; `DeckController.cs` slimmed by the moved actions + sync-only private helpers; the sync helpers (`BuildViewModel`, `BuildUserFacingErrorMessage`, `IsMoxfieldForbidden`, `HasMoxfieldInput`, `HasArchidektInput`, `RenderDiffAsync`) move WITH DeckSyncController.
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
DeckSyncController owns (move verbatim, preserve attributes + bodies):
- HttpGet "/sync"  -> IActionResult Index()                          // DeckController.cs ~L91-98 (ActiveTab=Sync)
- HttpPost "/sync" + ValidateAntiForgeryToken -> Index(DeckDiffRequest)  // ~L323-328 -> calls RenderDiffAsync
- HttpPost "/resolve" + ValidateAntiForgeryToken -> Resolve(DeckDiffRequest)  // ~L1649-1678
- private async RenderDiffAsync(DeckDiffRequest)                     // ~L1684-1738
- private ViewResult BuildViewModel(...)                             // ~L1747-1765
- private static string BuildUserFacingErrorMessage(...)            // ~L1772-1780
- private static bool IsMoxfieldForbidden(...)                       // ~L1787-1793
- private static bool HasMoxfieldInput(...)                          // ~L1799-1802
- private static bool HasArchidektInput(...)                         // ~L1808-1811
- Dependency: IDeckSyncService _deckSyncService
- Static helpers used (stay in their existing static classes; just call them): DeckSyncSupport, DeltaExporter, FullImportExporter, ReconciliationReporter, PrintingChoice

DeckConvertController owns:
- HttpGet "/convert"  -> IActionResult Convert()                     // ~L227-231
- HttpPost "/convert" + ValidateAntiForgeryToken -> async Convert(DeckConvertRequest)  // ~L237-274
- HttpGet "/convert/commander-search" -> async ConvertCommanderSearch(string q)  // ~L279-295
- Dependencies: IDeckConvertService _deckConvertService, ICardSearchService _cardSearchService
- Static helper used: UpstreamErrorMessageBuilder.BuildScryfallMessage

JudgeQuestionsController owns:
- HttpGet "/judge-questions" -> IActionResult JudgeQuestions(string? card)  // ~L148-156
- NO injected services AND NO logger. The live JudgeQuestions action body (DeckController.cs ~L149-156) references ONLY View(...), JudgeQuestionViewModel, DeckPageTab.JudgeQuestions, and the `card` param — it touches no `_logger`, no service field. So the controller is PARAMETERLESS (no ctor injection). Adding an ILogger<JudgeQuestionsController> field "for symmetry" would be an assigned-but-unused field => a new compiler warning => SRP-03 violation. Per D-01 discretion this stays its own thin controller (NOT folded into Lookup) to keep the URL + DeckPageTab.JudgeQuestions tab obvious.

All three controllers: ActiveTab values are set inside the action bodies (DeckPageTab.Sync / .Convert is set via the DeckConvertViewModel default / .JudgeQuestions) — moving the body carries the tab automatically (D-02).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create DeckSyncController (sync + resolve + sync helpers)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L91-98, 319-328, 1645-1811 — sync actions + every sync private helper)
    - DeckFlow.Web/Controllers/DeckToolControllerBase.cs (base to inherit)
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckSyncController.cs (CRLF — new file). Declare public sealed class DeckSyncController : DeckToolControllerBase in namespace DeckFlow.Web.Controllers. Ctor injects IDeckSyncService deckSyncService and ILogger<DeckSyncController> logger, both ArgumentNullException.ThrowIfNull-guarded, assigned to private readonly fields. XML doc on type + ctor.
    MOVE verbatim from DeckController (carry attributes + bodies byte-for-byte): the GET Index (HttpGet "/sync"), POST Index (HttpPost "/sync" + ValidateAntiForgeryToken), Resolve (HttpPost "/resolve" + ValidateAntiForgeryToken), and the private members RenderDiffAsync, BuildViewModel, BuildUserFacingErrorMessage, IsMoxfieldForbidden, HasMoxfieldInput, HasArchidektInput. The POST Index body is just `return await RenderDiffAsync(request);` — keep it.
    DELETE those members from DeckController.cs. Then remove the IDeckSyncService _deckSyncService field + ctor param + assignment from DeckController ONLY if no remaining DeckController action uses it (grep _deckSyncService after the move — Resolve and RenderDiffAsync are its only users, both moved, so remove it).
    Add usings in DeckSyncController.cs by grepping moved-code type references: Microsoft.AspNetCore.Mvc, System.Net (HttpStatusCode), DeckFlow.Core.Diffing, DeckFlow.Core.Exporting, DeckFlow.Core.Models, DeckFlow.Core.Parsing (DeckParseException), DeckFlow.Core.Reporting, DeckFlow.Web.Infrastructure (UpstreamErrorMessageBuilder), DeckFlow.Web.Models, DeckFlow.Web.Services. Include only those actually referenced; verify by build.
    Preserve LF in DeckController.cs; touch only moved/deleted lines; do not reformat.
  </action>
  <acceptance_criteria>
    - DeckSyncController.cs declares public sealed class DeckSyncController : DeckToolControllerBase, ctor guarded.
    - grep -nE "RenderDiffAsync|BuildUserFacingErrorMessage|IsMoxfieldForbidden|HasMoxfieldInput|HasArchidektInput|_deckSyncService|\"/resolve\"" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - DeckSyncController retains the exact attribute strings HttpGet "/sync", HttpPost "/sync", HttpPost "/resolve".
    - Web csproj builds: 0 errors, 0 new warnings (.Tests builds later in Plan 05). Confirm zero new warnings by comparing the warning count to the baseline recorded in 38-01-SUMMARY.md (grep `: warning ` in the build output; the count must not exceed the baseline).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-02-build.log | grep -E "error|Build succeeded" | tail -5; echo "new-warning-count:"; grep -c ': warning ' /tmp/38-02-build.log; grep -cE "RenderDiffAsync|_deckSyncService|/resolve" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>DeckSyncController owns /sync (GET+POST), /resolve, and all sync helpers; DeckController no longer references IDeckSyncService; Web build clean (warning count <= baseline).</done>
</task>

<task type="auto">
  <name>Task 2: Create DeckConvertController (convert + commander typeahead)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L224-295 — convert GET/POST + commander-search)
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckConvertController.cs (CRLF — new file). Declare public sealed class DeckConvertController : DeckToolControllerBase. Ctor injects IDeckConvertService deckConvertService, ICardSearchService cardSearchService, ILogger<DeckConvertController> logger — all ThrowIfNull-guarded, private readonly fields. XML doc.
    MOVE verbatim: Convert() (HttpGet "/convert"), Convert(DeckConvertRequest) (HttpPost "/convert" + ValidateAntiForgeryToken), ConvertCommanderSearch(string q) (HttpGet "/convert/commander-search"). Carry attributes + bodies byte-for-byte (the POST body uses _deckConvertService.ConvertAsync and the typeahead uses _cardSearchService.SearchCommandersAsync + UpstreamErrorMessageBuilder.BuildScryfallMessage).
    DELETE those three actions from DeckController.cs. Remove the IDeckConvertService _deckConvertService field/param/assignment (its only user is the moved POST Convert — grep to confirm, then remove). Do NOT remove _cardSearchService from DeckController yet — it is ALSO used by CardSearch (/suggest-categories/card-search), which moves in Plan 03; leave _cardSearchService in DeckController until then. (DeckConvertController gets its own injected ICardSearchService instance — DI provides one per controller; this is fine.)
    Add usings by grepping references: Microsoft.AspNetCore.Mvc, System.Net, DeckFlow.Core.Models (DeckInputSource), DeckFlow.Web.Infrastructure, DeckFlow.Web.Models, DeckFlow.Web.Services. Verify by build.
  </action>
  <acceptance_criteria>
    - DeckConvertController.cs declares public sealed class DeckConvertController : DeckToolControllerBase, ctor guarded.
    - grep -nE "\"/convert\"|ConvertCommanderSearch|_deckConvertService" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - _cardSearchService STILL present in DeckController.cs (grep finds it — used by CardSearch, moved in Plan 03).
    - Retains attribute strings HttpGet "/convert", HttpPost "/convert", HttpGet "/convert/commander-search".
    - Web csproj builds: 0 errors, 0 new warnings (warning count from `grep -c ': warning '` must not exceed the 38-01-SUMMARY.md baseline).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-02-build.log | grep -E "error|Build succeeded" | tail -5; echo "new-warning-count:"; grep -c ': warning ' /tmp/38-02-build.log; grep -cE "_deckConvertService|ConvertCommanderSearch" DeckFlow.Web/Controllers/DeckController.cs; grep -c "_cardSearchService" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>DeckConvertController owns the convert family; DeckController no longer references IDeckConvertService but retains _cardSearchService for Plan 03; Web build clean (warning count <= baseline).</done>
</task>

<task type="auto">
  <name>Task 3: Create JudgeQuestionsController (standalone judge-questions GET)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L148-156 — JudgeQuestions action; confirm the body references no `_logger` and no service field before deciding the ctor shape)
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/JudgeQuestionsController.cs (CRLF — new file). Declare public sealed class JudgeQuestionsController : DeckToolControllerBase.
    PARAMETERLESS controller — do NOT declare a constructor, do NOT inject ILogger<JudgeQuestionsController>, do NOT add any private field. Rationale: the live JudgeQuestions action body (DeckController.cs ~L149-156) references ONLY View(...), JudgeQuestionViewModel, DeckPageTab.JudgeQuestions, and the `card` param — it consumes no logger and no service. An injected-but-unused `_logger` (or any unused field) emits a new compiler warning, which violates SRP-03 (no new warnings). The base DeckToolControllerBase supplies the default Controller ctor; no JudgeQuestionsController ctor is needed. Confirm by reading the action body first: if (and only if) the live body actually references an injected member, route that member in — but the source shows it does not.
    MOVE verbatim: JudgeQuestions(string? card) (HttpGet "/judge-questions"), carrying its XML doc + attribute + body byte-for-byte (sets ActiveTab=DeckPageTab.JudgeQuestions and trims the optional card pre-fill).
    DELETE that action from DeckController.cs.
    Add usings: Microsoft.AspNetCore.Mvc, DeckFlow.Web.Models. Verify by build.
    Per D-01 discretion: JudgeQuestions stays standalone (not folded into Lookup) — it is a distinct tool family with its own tab and URL, and folding it would dilute Lookup's SRP. Record this choice in the SUMMARY.
  </action>
  <acceptance_criteria>
    - JudgeQuestionsController.cs declares public sealed class JudgeQuestionsController : DeckToolControllerBase.
    - The controller is PARAMETERLESS: grep finds NO constructor, NO `ILogger`, NO private field in JudgeQuestionsController.cs (grep -cE "ILogger|private readonly|public JudgeQuestionsController\\(" == 0).
    - Retains attribute string HttpGet "/judge-questions" and sets ActiveTab=DeckPageTab.JudgeQuestions.
    - grep -n "JudgeQuestions" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - Web csproj builds: 0 errors, 0 new warnings (warning count from `grep -c ': warning '` must not exceed the 38-01-SUMMARY.md baseline — proves the dropped logger introduced no unused-field warning).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-02-build.log | grep -E "error|Build succeeded" | tail -5; echo "new-warning-count:"; grep -c ': warning ' /tmp/38-02-build.log; echo "judge-ctor/logger count (must be 0):"; grep -cE "ILogger|private readonly|public JudgeQuestionsController\(" DeckFlow.Web/Controllers/JudgeQuestionsController.cs; echo "judge in DeckController (must be 0):"; grep -c "JudgeQuestions" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>JudgeQuestionsController owns /judge-questions as a parameterless controller (no unused logger); DeckController no longer contains it; Web build clean with no new warnings.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> sync/convert/judge routes | Pre-existing; unchanged. Same routes, same antiforgery tokens, same inputs after the move. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-02 | Tampering | /sync, /resolve, /convert POST endpoints | accept | No new attack surface — pure move refactor; the ValidateAntiForgeryToken attributes move verbatim with each POST action; inputs/auth identical pre/post. |

No package installs; no new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- Web project builds clean after all three tasks (0 errors; warning count <= 38-01-SUMMARY.md baseline — SRP-03 no-new-warnings).
- DeckController no longer references IDeckSyncService or IDeckConvertService and no longer contains the sync/convert/judge actions or sync helpers.
- JudgeQuestionsController is parameterless (no unused logger/field) — proves the dropped-logger fix.
- _cardSearchService remains in DeckController (consumed by Plan 03's CardSearch).
- Moved actions retain exact [HttpGet]/[HttpPost] attribute strings (URL set unchanged for SC1).
</verification>

<success_criteria>
- Three new sealed controllers inheriting DeckToolControllerBase; DeckSync/DeckConvert inject only their own services, JudgeQuestions is parameterless.
- All sync/convert/judge URLs + tabs preserved.
- DeckController slimmed accordingly with no collateral edits.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-02-SUMMARY.md` when done. Record moved members per controller, that JudgeQuestionsController is parameterless (no logger), which DeckController dependencies were removed vs retained (note _cardSearchService retained for Plan 03), and Web build status (warning count vs baseline).
</output>
