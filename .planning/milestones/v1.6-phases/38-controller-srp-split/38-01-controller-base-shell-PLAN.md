---
phase: 38-controller-srp-split
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Controllers/DeckToolControllerBase.cs
  - DeckFlow.Web/Controllers/ShellController.cs
  - DeckFlow.Web/Controllers/DeckViewLocationExpander.cs
  - DeckFlow.Web/Controllers/DeckController.cs
  - DeckFlow.Web/Views/Deck/Error.cshtml
  - DeckFlow.Web/Program.cs
autonomous: true
requirements: [SRP-01]
must_haves:
  truths:
    - "GET / still returns the Home view (200, DeckPageTab.Home)"
    - "GET /api/set-options still returns the Scryfall set catalog JSON"
    - "Every split feature controller resolves its View(\"X\") to the existing /Views/Deck/X.cshtml (no ViewNotFoundException at runtime)"
    - "The unhandled-exception error view still renders and its 'Back to home' link resolves to /"
    - "An unhandled exception is routed to the error view via UseExceptionHandler(\"/Deck/Error\") (unchanged), and /Deck/Error still resolves — now to ShellController.Error via an explicit [Route(\"Deck/Error\")] attribute"
    - "Feature controllers can inherit a shared timeout-wrapper + timeout constants from DeckToolControllerBase"
  artifacts:
    - path: "DeckFlow.Web/Controllers/DeckToolControllerBase.cs"
      provides: "Abstract base: cancellation-token-timeout wrapper + LookupTimeout/SuggestionTimeout consts"
      contains: "abstract class DeckToolControllerBase"
    - path: "DeckFlow.Web/Controllers/ShellController.cs"
      provides: "Non-tool routes: GET / (Home), Error action (routed at /Deck/Error), GET /api/set-options"
      contains: "class ShellController"
    - path: "DeckFlow.Web/Controllers/DeckViewLocationExpander.cs"
      provides: "IViewLocationExpander that appends /Views/Deck/{0}.cshtml so split controllers find the shared Deck views"
      contains: "class DeckViewLocationExpander"
  key_links:
    - from: "DeckFlow.Web/Views/Deck/Error.cshtml"
      to: "ShellController.Home"
      via: "Url.Action Home Shell"
      pattern: "Url\\.Action\\(\"Home\", \"Shell\"\\)"
    - from: "DeckFlow.Web/Controllers/ShellController.cs"
      to: "IScryfallSetService"
      via: "GetSetOptions action"
      pattern: "_scryfallSetService\\.GetSetsAsync"
    - from: "DeckFlow.Web/Controllers/ShellController.cs"
      to: "/Deck/Error route"
      via: "explicit Route attribute on Error"
      pattern: "\\[Route\\(\"Deck/Error\"\\)\\]"
    - from: "DeckFlow.Web/Program.cs"
      to: "DeckViewLocationExpander"
      via: "RazorViewEngineOptions.ViewLocationExpanders.Add"
      pattern: "ViewLocationExpanders\\.Add\\(new DeckFlow\\.Web\\.Controllers\\.DeckViewLocationExpander"
---

<objective>
Establish the SRP-split foundation: an abstract `DeckToolControllerBase` holding the genuinely cross-cutting bits (the cancellation-token-timeout wrapper and the `LookupTimeout`/`SuggestionTimeout` constants) that every feature controller will inherit, and a thin `ShellController` owning the three non-tool routes (`GET /` Home, the unhandled-exception Error action, `GET /api/set-options`). Move those three actions OUT of `DeckController` so the later feature extractions operate on a `DeckController` that no longer owns shell concerns.

This plan also wires the runtime plumbing that makes the whole split safe: a `DeckViewLocationExpander` (registered in `Program.cs`) so that every split controller's `return View("X")` still resolves to the existing `/Views/Deck/X.cshtml` instead of throwing `ViewNotFoundException` at render time. The `Error()` action moves to `ShellController` but KEEPS its `/Deck/Error` URL via an explicit `[Route("Deck/Error")]` attribute, so `UseExceptionHandler("/Deck/Error")` stays UNCHANGED and the SC1 URL set is preserved literally (the URL `/Deck/Error` exists pre- and post-split).

Purpose: Per D-03 — a shared base + a shell controller are the spine of the decomposition. Building them first means feature controllers in Wave 2 inherit a stable base and there is exactly one owner of the timeout wrapper. The view-location expander is a single-point fix that keeps all later feature plans free of any view-file moves or per-action edits. Preserving `/Deck/Error` (rather than repointing the handler to `/Shell/Error`) satisfies SC1 literally AND keeps the exception handler working with zero Program.cs handler edits — the simpler, parity-preserving choice (codex review finding #5).

Output: Three new files (`DeckToolControllerBase.cs`, `ShellController.cs`, `DeckViewLocationExpander.cs`), `DeckController.cs` slimmed by three actions + the `TryGetSetOptionsAsync` helper + the `SuggestionTimeout` const (relocated to base), `Error.cshtml`'s link updated to target `ShellController`, and `Program.cs` updated to register the expander on `RazorViewEngineOptions` (the ONLY Program.cs edit in this plan — `UseExceptionHandler` is left untouched).
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
@DeckFlow.Web/Program.cs

<interfaces>
Shell actions to MOVE to ShellController (preserve bodies byte-for-byte; ADD the route attribute noted on Error):
- HttpGet "/" -> IActionResult Home()                                 // DeckController.cs ~L74-78, returns View("Home", DeckPageTab.Home)
- ResponseCache + IgnoreAntiforgeryToken -> IActionResult Error()     // DeckController.cs ~L80-86, returns View("Error") — originally NO route attribute (conventional /Deck/Error). After the move it MUST carry an explicit [Route("Deck/Error")] so its URL stays /Deck/Error (see Task 2). Keep the existing [ResponseCache] + [IgnoreAntiforgeryToken] attributes.
- HttpGet "/api/set-options" -> async Task IActionResult GetSetOptions()  // DeckController.cs ~L217-222

Support that moves WITH GetSetOptions (its only consumer):
- private async Task<IReadOnlyList<ScryfallSetOption>> TryGetSetOptionsAsync()  // DeckController.cs ~L1464-1475
- Dependency consumed: IScryfallSetService _scryfallSetService

Cross-cutting bits to LIFT into DeckToolControllerBase (per D-03):
- private static readonly TimeSpan SuggestionTimeout = TimeSpan.FromSeconds(20);  // DeckController.cs L23 — relocate to base as protected
- The timeout-wrapper idiom at DeckController.cs L1596-1598 (CreateLinkedTokenSource + CancelAfter) — expose as a protected helper on the base.
- There is NO existing LookupTimeout const today; define it on the base for future Lookup-family use.

Error.cshtml current link (L10): an anchor with href Url.Action("Home", "Deck"). After the move it must read Url.Action("Home", "Shell"). (The Error VIEW file itself does not move; it stays at /Views/Deck/Error.cshtml and resolves via the expander.)

VIEW DISCOVERY (critical — runtime, not build-time): All ~12 feature views live in /Views/Deck/*.cshtml (CardLookup, CedhMetaGap, DeckAnalysis, DeckComparison, DeckConvert, DeckPrimer, DeckSync, Error, Home, JudgeQuestions, MechanicLookup, SuggestCategories). The default Razor view engine resolves View("DeckSync") from a controller named DeckSyncController to /Views/DeckSync/DeckSync.cshtml and /Views/Shared/DeckSync.cshtml — NEITHER exists, so every split-controller page would throw ViewNotFoundException at render. DeckFlow.Web.csproj has NO precompile/runtime-view-compile setting (views compile at runtime), so `dotnet build` does NOT catch this. The fix is one IViewLocationExpander that appends /Views/Deck/{0}.cshtml (and /Views/Deck/{0}{1}.cshtml) to the search locations for the split controllers — zero view-file edits, every action's return View("X") unchanged.

NAMESPACE / REGISTRATION NOTE (codex review finding #4): DeckViewLocationExpander is declared in namespace DeckFlow.Web.Controllers (consistent with the other new controller files). Program.cs is namespace DeckFlow.Web and has NO `using DeckFlow.Web.Controllers` (confirmed: Program.cs usings cover DeckFlow.Web.Configuration/Extensions/Infrastructure/Security/Services/... but NOT .Controllers). To make the registration compile WITHOUT adding a using (minimizing the Program.cs diff, matching how Program.cs already references RazorViewEngineOptions fully-qualified inline), instantiate the expander FULLY-QUALIFIED: `new DeckFlow.Web.Controllers.DeckViewLocationExpander()`. Do NOT add a `using DeckFlow.Web.Controllers;` line.

Program.cs anchors:
- MVC registration: `.AddControllersWithViews()` at Program.cs L66 — chain the `services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(...)` registration here (or immediately after the AddControllersWithViews() call).
- Exception handler: `app.UseExceptionHandler("/Deck/Error")` at Program.cs L389 — LEAVE UNCHANGED. Because Error() carries [Route("Deck/Error")] on ShellController, /Deck/Error still resolves and the handler keeps working. This plan makes NO edit to the UseExceptionHandler line.
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
    BASELINE CAPTURE (first action in the whole phase): before editing, run a clean `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` of the current HEAD and record the warning count (`grep -c ': warning '`) plus `git rev-parse HEAD`. These go into 38-01-SUMMARY as the warning baseline + pre-split baseline SHA that Plans 02-04 and Plan 06 read.
  </action>
  <acceptance_criteria>
    - File exists, declares public abstract class DeckToolControllerBase : Controller.
    - Exposes protected static readonly TimeSpan LookupTimeout and protected static readonly TimeSpan SuggestionTimeout, both == 20 seconds.
    - Exposes protected CancellationTokenSource CreateTimeoutScope(TimeSpan timeout).
    - grep confirms NO sealed on the base and NO HttpGet/HttpPost attributes in the file.
    - Web csproj builds: 0 errors; warning count (grep -c ': warning ') does not exceed the baseline recorded in 38-01-SUMMARY.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-01-build.log | grep -E "error|Build succeeded" | tail -5; echo "warning-count:"; grep -c ': warning ' /tmp/38-01-build.log</automated>
  </verify>
  <done>DeckToolControllerBase compiles, owns the two timeout consts + CreateTimeoutScope, owns zero routes, is abstract. Warning baseline + pre-split SHA captured in SUMMARY.</done>
</task>

<task type="auto">
  <name>Task 2: Create ShellController, move shell actions out of DeckController (preserving /Deck/Error)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (lines 20-86 ctor + Home/Error; 214-222 GetSetOptions; 1461-1475 TryGetSetOptionsAsync; L23 SuggestionTimeout)
    - DeckFlow.Web/Models/DeckPageTab.cs (DeckPageTab.Home)
    - DeckFlow.Web/Program.cs (L389 — app.UseExceptionHandler("/Deck/Error") — confirm it stays UNCHANGED)
  </read_first>
  <action>
    Create new file DeckFlow.Web/Controllers/ShellController.cs (CRLF — new file). Declare public sealed class ShellController : Controller in namespace DeckFlow.Web.Controllers. Shell does NOT inherit DeckToolControllerBase — it has no tool-timeout logic (ISP: keep it a plain Controller). Constructor injects ONLY IScryfallSetService scryfallSetService and ILogger<ShellController> logger, with ArgumentNullException.ThrowIfNull guards, assigned to _scryfallSetService and _logger private readonly fields. XML doc on type + ctor.
    MOVE these three actions from DeckController into ShellController (carry the [ResponseCache]/[IgnoreAntiforgeryToken]/[HttpGet] attributes byte-for-byte; do NOT edit bodies): Home() (HttpGet "/"), Error() (ResponseCache + IgnoreAntiforgeryToken), GetSetOptions() (HttpGet "/api/set-options"). MOVE the private helper TryGetSetOptionsAsync() with them (its only consumer; it calls _logger.LogWarning(...) and _scryfallSetService.GetSetsAsync(...)).
    CRITICAL — PRESERVE /Deck/Error (codex review finding #5): the original Error() had NO route attribute and resolved conventionally as /Deck/Error. To keep that exact URL after moving to ShellController, ADD an explicit attribute [Route("Deck/Error")] to ShellController.Error() (in addition to its existing [ResponseCache] + [IgnoreAntiforgeryToken]). This makes /Deck/Error resolve to ShellController.Error, so UseExceptionHandler("/Deck/Error") in Program.cs needs NO change and the SC1 URL set is preserved literally (/Deck/Error exists pre- and post-split). Do NOT use /Shell/Error; do NOT add [HttpGet] (a Route attribute with no verb constraint matches the re-executed error pipeline request, matching the original conventional behavior which also had no verb constraint).
    Then DELETE those four members from DeckController.cs. Grep DeckController for _scryfallSetService after the move — it is used only by TryGetSetOptionsAsync, so remove the field + ctor param + assignment + the now-unused IScryfallSetService reference. Leave ALL other DeckController dependencies untouched.
    DO NOT TOUCH Program.cs in this task. The UseExceptionHandler("/Deck/Error") line at L389 stays exactly as-is (the route attribute above keeps it valid). Program.cs is edited ONLY in Task 3 (the view-expander registration).
    Touch only lines that move/delete — do NOT reformat surrounding code, do NOT reorder usings beyond removing a now-unused one, preserve LF endings in DeckController.cs.
    Required usings in ShellController.cs: Microsoft.AspNetCore.Mvc, DeckFlow.Web.Models, DeckFlow.Web.Services — confirm exact namespaces by grepping the moved code type references (IScryfallSetService, ScryfallSetOption).
  </action>
  <acceptance_criteria>
    - ShellController.cs declares public sealed class ShellController : Controller with ctor guarded by ArgumentNullException.ThrowIfNull.
    - ShellController contains exactly Home/Error/GetSetOptions + private TryGetSetOptionsAsync, preserving route attributes HttpGet "/" and HttpGet "/api/set-options", and Error() carries [Route("Deck/Error")] (plus its original [ResponseCache] + [IgnoreAntiforgeryToken]).
    - grep -c '\[Route("Deck/Error")\]' DeckFlow.Web/Controllers/ShellController.cs == 1.
    - grep -nE "GetSetOptions|TryGetSetOptionsAsync|IScryfallSetService|_scryfallSetService" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - DeckController.cs ctor parameter count dropped by exactly 1 (IScryfallSetService removed); no other dependency removed.
    - Program.cs UNCHANGED in this task: grep -c '"/Deck/Error"' DeckFlow.Web/Program.cs == 1 AND grep -c '"/Shell/Error"' DeckFlow.Web/Program.cs == 0 (handler still targets /Deck/Error).
    - Web csproj builds: 0 errors; warning count does not exceed the 38-01 baseline. (The .Tests project will NOT build yet — DeckControllerTests still passes 12 ctor args; expected, fixed in Plan 06. Build only the Web csproj here.)
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-01-build.log | grep -E "error|Build succeeded" | tail -5; echo "warning-count:"; grep -c ': warning ' /tmp/38-01-build.log; echo "shell Route(Deck/Error) count:"; grep -c 'Route("Deck/Error")' DeckFlow.Web/Controllers/ShellController.cs; echo "DeckController shell-refs (must be 0):"; grep -cE "GetSetOptions|TryGetSetOptionsAsync|_scryfallSetService" DeckFlow.Web/Controllers/DeckController.cs; echo "Program /Deck/Error (must be 1):"; grep -c '/Deck/Error' DeckFlow.Web/Program.cs; echo "Program /Shell/Error (must be 0):"; grep -c '/Shell/Error' DeckFlow.Web/Program.cs</automated>
  </verify>
  <done>ShellController owns the three shell routes + set-options helper; Error() keeps the /Deck/Error URL via [Route("Deck/Error")]; DeckController no longer references IScryfallSetService; Program.cs UseExceptionHandler is untouched (still /Deck/Error); Web project builds clean (warnings <= baseline).</done>
</task>

<task type="auto">
  <name>Task 3: Add DeckViewLocationExpander and register it so split controllers find /Views/Deck/*</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (the View("X") usages — CardLookup, CedhMetaGap, DeckAnalysis, DeckComparison, DeckConvert, DeckPrimer, DeckSync, Error, Home, JudgeQuestions, MechanicLookup, SuggestCategories — all 12 views live in /Views/Deck/)
    - DeckFlow.Web/Program.cs (L66 — `.AddControllersWithViews()` is the MVC registration anchor; add the RazorViewEngineOptions config here; confirm there is NO `using DeckFlow.Web.Controllers`)
    - ./CLAUDE.md (NEW files prefer CRLF; existing Program.cs is LF — preserve; one-type-per-file)
  </read_first>
  <action>
    Create new file DeckFlow.Web/Controllers/DeckViewLocationExpander.cs (CRLF — new file). Declare public sealed class DeckViewLocationExpander : IViewLocationExpander in namespace DeckFlow.Web.Controllers (Microsoft.AspNetCore.Mvc.Razor). XML summary doc explaining: all DeckFlow tool views physically live in /Views/Deck/, but after the SRP split the controllers are named DeckSyncController/DeckConvertController/DeckLookupController/DeckCategoriesController/DeckPacketController/DeckPrimerController/JudgeQuestionsController/ShellController; the default view engine searches /Views/{ControllerName}/ which no longer matches, so this expander appends /Views/Deck/ as a fallback search location.
    Implement IViewLocationExpander:
    - PopulateValues(ViewLocationExpanderContext context): no-op (no view-key contribution needed; leave body empty with an XML/inline Why-note that view selection does not vary by any populated value).
    - ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations): return the original viewLocations followed by the appended Deck locations "/Views/Deck/{0}.cshtml" and "/Views/Deck/{0}{1}.cshtml" (use viewLocations.Concat(new[] { "/Views/Deck/{0}.cshtml", "/Views/Deck/{0}{1}.cshtml" })). Append (do NOT prepend) so any future controller-specific view still wins; the Deck folder is a fallback. The {0} token is the view name, {1} the controller name — both Razor-standard.
    Scope note: appending unconditionally for ALL controllers is acceptable and simplest (the {0} fallback only ever resolves a file that exists in /Views/Deck/, and existing controllers — Commander, Feedback, Help, About, Admin, Api — already find their own views first since this is appended last). Do NOT gate on controller base type; keep the expander dependency-free and trivial. Add an inline Why-comment recording this decision.
    Register it in Program.cs: immediately after the `.AddControllersWithViews()` call at L66 (preserving the existing builder chain and LF endings), add services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options => options.ViewLocationExpanders.Add(new DeckFlow.Web.Controllers.DeckViewLocationExpander()));
    USE THE FULLY-QUALIFIED TYPE NAME `new DeckFlow.Web.Controllers.DeckViewLocationExpander()` (codex review finding #4): Program.cs is namespace DeckFlow.Web with no `using DeckFlow.Web.Controllers`, so a bare `new DeckViewLocationExpander()` would NOT compile. Do NOT add a `using DeckFlow.Web.Controllers;` — instead fully-qualify the type inline (matching the fully-qualified RazorViewEngineOptions already used on the same line), keeping the Program.cs using-block diff at zero. Touch only the lines needed for the registration; do NOT reformat the surrounding DI chain.
    Why no view-file moves: this single registration makes every split controller's existing return View("X") resolve to /Views/Deck/X.cshtml with zero edits to the ~12 view files or any action body — the cheapest correct fix.
  </action>
  <acceptance_criteria>
    - DeckViewLocationExpander.cs exists, declares public sealed class DeckViewLocationExpander : IViewLocationExpander in namespace DeckFlow.Web.Controllers, and ExpandViewLocations appends "/Views/Deck/{0}.cshtml" and "/Views/Deck/{0}{1}.cshtml".
    - The registration uses the FULLY-QUALIFIED type and compiles against the chosen namespace: grep -c "ViewLocationExpanders.Add(new DeckFlow.Web.Controllers.DeckViewLocationExpander" DeckFlow.Web/Program.cs == 1 AND grep -c "using DeckFlow.Web.Controllers;" DeckFlow.Web/Program.cs == 0 (no using added). The Web build compiling clean is the authoritative proof the namespace/instantiation match.
    - Web csproj builds: 0 errors; warning count does not exceed the 38-01 baseline.
    - MANUAL (WSL — VSTest unreliable, runtime-compiled views): after Plan 04 completes the split, a human/curl smoke confirms at least GET /sync renders its view (HTTP 200, body contains the DeckSync page markup) rather than a ViewNotFoundException/500. State this as a manual verification step; it is the runtime proof the expander works and is re-asserted in the phase verification (one route per new controller).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tee /tmp/38-01-build.log | grep -E "error|Build succeeded" | tail -5; echo "warning-count:"; grep -c ': warning ' /tmp/38-01-build.log; echo "fully-qualified registration (must be 1):"; grep -c "ViewLocationExpanders.Add(new DeckFlow.Web.Controllers.DeckViewLocationExpander" DeckFlow.Web/Program.cs; echo "using added (must be 0):"; grep -c "using DeckFlow.Web.Controllers;" DeckFlow.Web/Program.cs</automated>
  </verify>
  <done>DeckViewLocationExpander exists in DeckFlow.Web.Controllers and is registered fully-qualified on RazorViewEngineOptions (no using added, build clean); split controllers resolve View("X") to /Views/Deck/X.cshtml; Web builds clean (warnings <= baseline). Runtime page-render confirmed manually (deferred to phase verification once the split lands in Plan 04).</done>
</task>

<task type="auto">
  <name>Task 4: Re-point Error.cshtml back-to-home link to ShellController</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/Error.cshtml (line 10 — the Url.Action back-to-home link)
    - DeckFlow.Web/Controllers/ShellController.cs (the new Home action target)
  </read_first>
  <action>
    In DeckFlow.Web/Views/Deck/Error.cshtml line 10, change the anchor href from Url.Action("Home", "Deck") to Url.Action("Home", "Shell"). This is the ONLY line to touch in the file. Why: the Home action moved from DeckController to ShellController in Task 2; conventional link generation by controller name would otherwise 404 (the URL "/" still resolves via the HttpGet "/" attribute, but Url.Action resolves by controller+action name, so it must name "Shell"). Preserve LF line endings and the existing surrounding markup exactly. Do NOT touch any other view or any other link.
    Note: the Error VIEW itself stays at Views/Deck/Error.cshtml — ShellController.Error() returns View("Error"), which now resolves via the DeckViewLocationExpander registered in Task 3 (appends /Views/Deck/Error.cshtml). So the physical view file does NOT move and no view-path edit is needed.
  </action>
  <acceptance_criteria>
    - grep -n 'Url.Action("Home", "Shell")' DeckFlow.Web/Views/Deck/Error.cshtml returns exactly one match.
    - grep -n 'Url.Action("Home", "Deck")' DeckFlow.Web/Views/Deck/Error.cshtml returns NOTHING.
    - No other line in Error.cshtml changed (git diff shows a single-line change).
    - Web csproj builds: 0 errors; warning count does not exceed the 38-01 baseline.
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
| unhandled exception -> error pipeline | UseExceptionHandler re-executes against /Deck/Error (unchanged); the path string is server-controlled (literal), not user input. /Deck/Error now resolves to ShellController.Error via an explicit Route attribute. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-01 | Tampering | Route surface (GET /, /api/set-options, /Deck/Error) | accept | No new attack surface — pure move refactor; routes, inputs, antiforgery attributes (IgnoreAntiforgeryToken on Error, none added/removed) identical pre/post. The added [Route("Deck/Error")] re-asserts the SAME URL the action had conventionally. Error.cshtml link is server-side generation only. |
| T-38-01b | Denial of Service | View resolution / error pipeline | mitigate | DeckViewLocationExpander appends a fallback search path so split controllers render real views (prevents ViewNotFoundException 500s); the error pipeline keeps targeting /Deck/Error (now backed by ShellController.Error via [Route]) so unhandled exceptions still render the friendly error view (prevents bare 404s). Both are literal, server-controlled values — no user-tainted input reaches the view path or handler path. |

No package installs in this plan; no new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- Web project builds clean (0 errors; warning count <= captured baseline) after all four tasks.
- DeckController no longer references IScryfallSetService or the shell actions.
- ShellController owns GET /, Error (routed at /Deck/Error), GET /api/set-options.
- DeckViewLocationExpander is registered fully-qualified on RazorViewEngineOptions (grep proof, no using added); split controllers resolve View("X") to /Views/Deck/X.cshtml.
- Program.cs UseExceptionHandler still targets /Deck/Error (grep == 1) and /Shell/Error is absent (grep == 0) — handler unchanged, URL preserved.
- MANUAL page-render smoke (WSL, runtime-compiled views, VSTest unreliable): once Plan 04 lands the full split, confirm one route per new controller renders its view (HTTP 200, not 404/500): GET /sync, /convert, /card-lookup, /suggest-categories, /deck-analysis, /deck-primer, /judge-questions, / (home), and a forced unhandled exception routes to the error view via /Deck/Error. This is the real SC2 "behavior unchanged" proof beyond build.
- Route-list capture for SC1 is performed at phase close (Plan 06 owns the full pre/post route diff); this plan's contribution is verified by: the moved actions retain their exact [HttpGet] attribute strings (grep), and Error()'s /Deck/Error URL is preserved via [Route("Deck/Error")], so the URL set is unchanged.
</verification>

<success_criteria>
- DeckToolControllerBase + ShellController + DeckViewLocationExpander exist and compile.
- The three shell routes are owned by ShellController with unchanged URLs (including /Deck/Error preserved via [Route]).
- Every split controller's View("X") resolves to /Views/Deck/X.cshtml at runtime (no ViewNotFoundException).
- UseExceptionHandler still routes unhandled exceptions to /Deck/Error (unchanged), now backed by ShellController.Error.
- Error.cshtml link resolves to ShellController.Home.
- DeckController is slimmed by exactly these members with no collateral edits.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-01-SUMMARY.md` when done. Record:
- The pre-split baseline git SHA — the current HEAD BEFORE any edits in this plan (capture with `git rev-parse HEAD` before Task 1; re-capture at execution time in case of intervening commits). Plan 06's SC1 route-parity gate reads this SHA from the SUMMARY to extract the pre-split DeckController.cs, instead of guessing HEAD~N.
- The WARNING BASELINE — the `grep -c ': warning '` count from a clean Web build of that baseline SHA. Plans 02-04 and Plan 06 assert their post-edit warning count does not exceed this baseline (SRP-03 no-new-warnings).
- The exact members moved (Home/Error/GetSetOptions/TryGetSetOptionsAsync), the DeckController ctor arg count before/after.
- Confirmation that Error() carries [Route("Deck/Error")] so /Deck/Error is preserved and UseExceptionHandler is UNCHANGED.
- Confirmation that DeckViewLocationExpander is registered (fully-qualified, no using added) and the Web build is clean.
</output>
