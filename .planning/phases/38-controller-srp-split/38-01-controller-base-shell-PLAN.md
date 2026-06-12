---
phase: 38-controller-srp-split
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Controllers/DeckToolControllerBase.cs
  - DeckFlow.Web/Controllers/ShellController.cs
  - DeckFlow.Web/Controllers/DeckController.cs
  - DeckFlow.Web/Views/Deck/Error.cshtml
autonomous: true
requirements: [SRP-01]
must_haves:
  truths:
    - "GET / still returns the Home view (200, DeckPageTab.Home)"
    - "GET /api/set-options still returns the Scryfall set catalog JSON"
    - "The unhandled-exception error view still renders and its 'Back to home' link resolves to /"
    - "Feature controllers can inherit a shared timeout-wrapper + timeout constants from DeckToolControllerBase"
  artifacts:
    - path: "DeckFlow.Web/Controllers/DeckToolControllerBase.cs"
      provides: "Abstract base: cancellation-token-timeout wrapper + LookupTimeout/SuggestionTimeout consts"
      contains: "abstract class DeckToolControllerBase"
    - path: "DeckFlow.Web/Controllers/ShellController.cs"
      provides: "Non-tool routes: GET / (Home), Error action, GET /api/set-options"
      contains: "class ShellController"
  key_links:
    - from: "DeckFlow.Web/Views/Deck/Error.cshtml"
      to: "ShellController.Home"
      via: "Url.Action Home Shell"
      pattern: "Url\\.Action\\(\"Home\", \"Shell\"\\)"
    - from: "DeckFlow.Web/Controllers/ShellController.cs"
      to: "IScryfallSetService"
      via: "GetSetOptions action"
      pattern: "_scryfallSetService\\.GetSetsAsync"
---

<objective>
Establish the SRP-split foundation: an abstract `DeckToolControllerBase` holding the genuinely cross-cutting bits (the cancellation-token-timeout wrapper and the `LookupTimeout`/`SuggestionTimeout` constants) that every feature controller will inherit, and a thin `ShellController` owning the three non-tool routes (`GET /` Home, the unhandled-exception Error action, `GET /api/set-options`). Move those three actions OUT of `DeckController` so the later feature extractions operate on a `DeckController` that no longer owns shell concerns.

Purpose: Per D-03 — a shared base + a shell controller are the spine of the decomposition. Building them first means feature controllers in Wave 2 inherit a stable base and there is exactly one owner of the timeout wrapper.

Output: Two new files (`DeckToolControllerBase.cs`, `ShellController.cs`), `DeckController.cs` slimmed by three actions + the `TryGetSetOptionsAsync` helper + the `SuggestionTimeout` const (relocated to base), and `Error.cshtml`'s link updated to target `ShellController`.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/38-controller-srp-split/38-CONTEXT.md

@DeckFlow.Web/Controllers/DeckController.cs
@DeckFlow.Web/Models/DeckPageTab.cs
@DeckFlow.Web/Views/Deck/Error.cshtml

<interfaces>
Shell actions to MOVE to ShellController (preserve attributes + bodies byte-for-byte):
- HttpGet "/" -> IActionResult Home()                                 // DeckController.cs ~L74-78, returns View("Home", DeckPageTab.Home)
- ResponseCache + IgnoreAntiforgeryToken -> IActionResult Error()     // DeckController.cs ~L80-86, returns View("Error") — NO route attribute (conventional)
- HttpGet "/api/set-options" -> async Task IActionResult GetSetOptions()  // DeckController.cs ~L217-222

Support that moves WITH GetSetOptions (its only consumer):
- private async Task<IReadOnlyList<ScryfallSetOption>> TryGetSetOptionsAsync()  // DeckController.cs ~L1464-1475
- Dependency consumed: IScryfallSetService _scryfallSetService

Cross-cutting bits to LIFT into DeckToolControllerBase (per D-03):
- private static readonly TimeSpan SuggestionTimeout = TimeSpan.FromSeconds(20);  // DeckController.cs L23 — relocate to base as protected
- The timeout-wrapper idiom at DeckController.cs L1596-1598 (CreateLinkedTokenSource + CancelAfter) — expose as a protected helper on the base.
- There is NO existing LookupTimeout const today; define it on the base for future Lookup-family use.

Error.cshtml current link (L10): an anchor with href Url.Action("Home", "Deck"). After the move it must read Url.Action("Home", "Shell").
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create DeckToolControllerBase (abstract cross-cutting base)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (lines 23, 1558-1598 — SuggestionTimeout const and the only live timeout-wrapper usage in SuggestCategories)
    - ./CLAUDE.md (LF endings on existing files; NEW files prefer CRLF; one-type-per-file; base is abstract, not sealed)
  </read_first>
  <action>
    Create new file DeckFlow.Web/Controllers/DeckToolControllerBase.cs (CRLF line endings — new file). Declare public abstract class DeckToolControllerBase : Controller in namespace DeckFlow.Web.Controllers with file-scoped namespace, Allman braces, XML summary doc.
    Add two protected timeout constants: protected static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(20); and protected static readonly TimeSpan SuggestionTimeout = TimeSpan.FromSeconds(20); — each with an XML doc and a Why-note that these are the per-request soft-timeout budgets lifted from DeckController per D-03. SuggestionTimeout value 20s is copied verbatim from DeckController.cs L23; LookupTimeout is introduced here at 20s for the Lookup family per D-03 timeout-constants phrasing. Do NOT wire LookupTimeout into any action in this plan.
    Add a protected helper CancellationTokenSource CreateTimeoutScope(TimeSpan timeout) returning CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted) with CancelAfter(timeout) applied, matching the idiom at DeckController.cs L1596-1598. Document that callers own disposal (using). Do NOT change any existing call site in this plan; SuggestCategories keeps its inline idiom until its move.
    Do NOT add an upstream-error funnel method — UpstreamErrorMessageBuilder.BuildScryfallMessage is already a static each action calls directly; lifting it adds no value (base stays minimal per SOLID). Note this in an inline Why-comment.
    Required usings: Microsoft.AspNetCore.Mvc and System.Threading (verify build for any implicit-using gaps).
  </action>
  <acceptance_criteria>
    - File exists, declares public abstract class DeckToolControllerBase : Controller.
    - Exposes protected static readonly TimeSpan LookupTimeout and protected static readonly TimeSpan SuggestionTimeout, both == 20 seconds.
    - Exposes protected CancellationTokenSource CreateTimeoutScope(TimeSpan timeout).
    - grep confirms NO sealed on the base and NO HttpGet/HttpPost attributes in the file.
    - Web csproj builds: 0 errors, 0 new warnings.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5</automated>
  </verify>
  <done>DeckToolControllerBase compiles, owns the two timeout consts + CreateTimeoutScope, owns zero routes, is abstract.</done>
</task>

<task type="auto">
  <name>Task 2: Create ShellController and move shell actions out of DeckController</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (lines 20-86 ctor + Home/Error; 214-222 GetSetOptions; 1461-1475 TryGetSetOptionsAsync; L23 SuggestionTimeout)
    - DeckFlow.Web/Models/DeckPageTab.cs (DeckPageTab.Home)
  </read_first>
  <action>
    Create new file DeckFlow.Web/Controllers/ShellController.cs (CRLF — new file). Declare public sealed class ShellController : Controller in namespace DeckFlow.Web.Controllers. Shell does NOT inherit DeckToolControllerBase — it has no tool-timeout logic (ISP: keep it a plain Controller). Constructor injects ONLY IScryfallSetService scryfallSetService and ILogger<ShellController> logger, with ArgumentNullException.ThrowIfNull guards, assigned to _scryfallSetService and _logger private readonly fields. XML doc on type + ctor.
    MOVE these three actions verbatim from DeckController into ShellController (carry attributes byte-for-byte; do NOT edit bodies): Home() (HttpGet "/"), Error() (ResponseCache + IgnoreAntiforgeryToken, no route attr), GetSetOptions() (HttpGet "/api/set-options"). MOVE the private helper TryGetSetOptionsAsync() with them (its only consumer; it calls _logger.LogWarning(...) and _scryfallSetService.GetSetsAsync(...)).
    Then DELETE those four members from DeckController.cs. Grep DeckController for _scryfallSetService after the move — it is used only by TryGetSetOptionsAsync, so remove the field + ctor param + assignment + the now-unused IScryfallSetService reference. Leave ALL other DeckController dependencies untouched.
    Touch only lines that move/delete — do NOT reformat surrounding code, do NOT reorder usings beyond removing a now-unused one, preserve LF endings in DeckController.cs.
    Required usings in ShellController.cs: Microsoft.AspNetCore.Mvc, DeckFlow.Web.Models, DeckFlow.Web.Services — confirm exact namespaces by grepping the moved code type references (IScryfallSetService, ScryfallSetOption).
  </action>
  <acceptance_criteria>
    - ShellController.cs declares public sealed class ShellController : Controller with ctor guarded by ArgumentNullException.ThrowIfNull.
    - ShellController contains exactly Home/Error/GetSetOptions + private TryGetSetOptionsAsync, preserving route attributes HttpGet "/" and HttpGet "/api/set-options".
    - grep -nE "GetSetOptions|TryGetSetOptionsAsync|IScryfallSetService|_scryfallSetService" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - DeckController.cs ctor parameter count dropped by exactly 1 (IScryfallSetService removed); no other dependency removed.
    - Web csproj builds: 0 errors, 0 new warnings. (The .Tests project will NOT build yet — DeckControllerTests still passes 12 ctor args; expected, fixed in Plan 05. Build only the Web csproj here.)
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; grep -cE "GetSetOptions|TryGetSetOptionsAsync|_scryfallSetService" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>ShellController owns the three shell routes + set-options helper; DeckController no longer references IScryfallSetService; Web project builds clean.</done>
</task>

<task type="auto">
  <name>Task 3: Re-point Error.cshtml back-to-home link to ShellController</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/Error.cshtml (line 10 — the Url.Action back-to-home link)
    - DeckFlow.Web/Controllers/ShellController.cs (the new Home action target)
  </read_first>
  <action>
    In DeckFlow.Web/Views/Deck/Error.cshtml line 10, change the anchor href from Url.Action("Home", "Deck") to Url.Action("Home", "Shell"). This is the ONLY line to touch in the file. Why: the Home action moved from DeckController to ShellController in Task 2; conventional link generation by controller name would otherwise 404 (the URL "/" still resolves via the HttpGet "/" attribute, but Url.Action resolves by controller+action name, so it must name "Shell"). Preserve LF line endings and the existing surrounding markup exactly. Do NOT touch any other view or any other link.
    Note: the Error VIEW itself stays at Views/Deck/Error.cshtml — ASP.NET resolves View("Error") returned by ShellController.Error() by view name through the shared view-location conventions, so the physical view file does NOT move and no view-path edit is needed. (If the build/runtime surfaces a view-not-found for "Error" from ShellController, that is a discovery to surface — but Views/Shared and Views/Deck are both on the default search path; do not pre-emptively move the file.)
  </action>
  <acceptance_criteria>
    - grep -n 'Url.Action("Home", "Shell")' DeckFlow.Web/Views/Deck/Error.cshtml returns exactly one match.
    - grep -n 'Url.Action("Home", "Deck")' DeckFlow.Web/Views/Deck/Error.cshtml returns NOTHING.
    - No other line in Error.cshtml changed (git diff shows a single-line change).
    - Web csproj builds: 0 errors, 0 new warnings.
  </acceptance_criteria>
  <verify>
    <automated>grep -c 'Url.Action("Home", "Shell")' DeckFlow.Web/Views/Deck/Error.cshtml; grep -c 'Url.Action("Home", "Deck")' DeckFlow.Web/Views/Deck/Error.cshtml</automated>
  </verify>
  <done>Error.cshtml back-to-home link targets ShellController.Home; no other line changed.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> MVC routes | Pre-existing; unchanged. Same routes, same inputs, same attributes after the move. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-01 | Tampering | Route surface (GET /, /api/set-options, Error) | accept | No new attack surface — pure move refactor; routes, inputs, antiforgery attributes (IgnoreAntiforgeryToken on Error, none added/removed) identical pre/post. Error.cshtml link is server-side generation only. |

No package installs in this plan; no new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- Web project builds clean (0 errors, 0 new warnings) after all three tasks.
- DeckController no longer references IScryfallSetService or the shell actions.
- ShellController owns GET /, Error, GET /api/set-options.
- Route-list capture for SC1 is performed at phase close (Plan 05 owns the full pre/post route diff); this plan's contribution is verified by: the moved actions retain their exact [HttpGet] attribute strings (grep), so the URL set is unchanged.
</verification>

<success_criteria>
- DeckToolControllerBase + ShellController exist and compile.
- The three shell routes are owned by ShellController with unchanged URLs.
- Error.cshtml link resolves to ShellController.Home.
- DeckController is slimmed by exactly these members with no collateral edits.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-01-SUMMARY.md` when done. Record: the exact members moved, the DeckController ctor arg count before/after, and confirmation that the Web build is clean.
</output>
