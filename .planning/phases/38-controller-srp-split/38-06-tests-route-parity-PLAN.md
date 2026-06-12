---
phase: 38-controller-srp-split
plan: 06
type: execute
wave: 5
depends_on: ["38-01", "38-02", "38-03", "38-04"]
files_modified:
  - DeckFlow.Web.Tests/DeckControllerTestFakes.cs
  - DeckFlow.Web.Tests/DeckLookupControllerTests.cs
  - DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs
  - DeckFlow.Web.Tests/DeckPacketControllerTests.cs
  - DeckFlow.Web.Tests/DeckControllerTests.cs
autonomous: true
requirements: [SRP-03, SRP-01]
must_haves:
  truths:
    - "Every existing DeckControllerTests test case still exists, now constructing the correct new controller, and the .Tests project builds clean"
    - "The pre-split route list equals the post-split route list (zero add/remove/change) — SC1 proven mechanically"
    - "Shared test fakes are accessible to all new per-controller test files"
  artifacts:
    - path: "DeckFlow.Web.Tests/DeckControllerTestFakes.cs"
      provides: "Relocated shared Fake/Stub/Throwing service doubles, internal so all new test files can use them"
      contains: "class FakeDeckSyncService"
    - path: "DeckFlow.Web.Tests/DeckLookupControllerTests.cs"
      provides: "card+mechanic lookup tests targeting DeckLookupController"
      contains: "class DeckLookupControllerTests"
    - path: "DeckFlow.Web.Tests/DeckPacketControllerTests.cs"
      provides: "analysis/comparison/meta-gap tests targeting DeckPacketController"
      contains: "class DeckPacketControllerTests"
  key_links:
    - from: "DeckFlow.Web.Tests/DeckLookupControllerTests.cs"
      to: "DeckLookupController"
      via: "ILogger<DeckLookupController> + narrowed ctor"
      pattern: "new DeckLookupController\\("
    - from: "DeckFlow.Web.Tests/DeckPacketControllerTests.cs"
      to: "DeckPacketController"
      via: "ILogger<DeckPacketController> + narrowed ctor"
      pattern: "new DeckPacketController\\("
---

<objective>
Mirror-split `DeckFlow.Web.Tests/DeckControllerTests.cs` into per-new-controller test files (D-05), updating each test to construct its new controller with the narrowed ctor and the correct `ILogger<NewController>` generic, and relocate the shared test fakes so every new test file can use them. Then run the SC1 route-parity gate: capture the route list before vs after the split and prove zero adds/removes/changes. This is the phase's verification close — after it, the whole solution builds clean and the URL set is provably unchanged.

Purpose: SRP-03 (tests pass against the split with only logger-generic refs changed, no new warnings) + SRP-01's SC1 (route-list parity).

Output: New `DeckControllerTestFakes.cs` (relocated internal fakes), three new per-controller test files (`DeckLookupControllerTests`, `DeckCategoriesControllerTests`, `DeckPacketControllerTests`), `DeckControllerTests.cs` deleted; the full solution builds clean; a captured route-diff proving SC1.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/38-controller-srp-split/38-CONTEXT.md

@DeckFlow.Web.Tests/DeckControllerTests.cs
@DeckFlow.Web/Controllers/DeckLookupController.cs
@DeckFlow.Web/Controllers/DeckCategoriesController.cs
@DeckFlow.Web/Controllers/DeckPacketController.cs

<interfaces>
Current DeckControllerTests.cs constructs DeckController with the 12-arg ctor in EVERY test. After the split each test must construct its new controller with that controller's narrowed ctor (services + ILogger<NewController>). Test-to-controller map (confirmed by which controller.X each test invokes):

-> DeckLookupController (ctor: ICardLookupService, IMechanicLookupService, ILogger<DeckLookupController>):
   CardLookup_ReturnsValidationError_WhenCardListMissing, CardLookup_ReturnsUserFacingError_WhenScryfallFails,
   CardLookup_ReturnsValidationMessage_WhenTooManyLinesSubmitted, DownloadCardLookup_ReturnsTextFile_WhenVerificationSucceeds,
   SingleCardLookup_* (5 tests), MechanicLookup_ReturnsValidationError_WhenMechanicMissing, MechanicLookup_ReturnsRules_WhenMechanicFound

-> DeckCategoriesController (ctor: ICategorySuggestionService, ICardSearchService, ILogger<DeckCategoriesController>):
   CardSearch_ReturnsServiceUnavailable_WhenScryfallFails,
   BuildNoSuggestionsMessage_UsesCachedDataNotice_WhenNoDecks, BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist
   (the two BuildNoSuggestionsMessage tests call the static CategorySuggestionMessageBuilder directly — no controller; group them here as the closest family.)

-> DeckPacketController (ctor: IDeckAnalysisPacketService, IDeckComparisonService, IMetaGapService, PacketSessionCache, ILogger<DeckPacketController>):
   CedhMetaGap_Get_ReturnsExpectedViewModel, CedhMetaGap_Post_AdvancesToStep2WhenReferenceDecksAreFetched, CedhMetaGap_Post_ReturnsRateLimitMessage,
   DeckAnalysis_* (4 tests), DeckComparison_Get_RendersPage, DeckComparison_Post_ReturnsExpectedResultModel, DeckComparison_Post_ReturnsViewWithError_WhenModelStateInvalid

Shared fakes currently nested private in DeckControllerTests.cs (L811-1110): FakeDeckSyncService, FakeDeckConvertService, StubDeckAnalysisPacketService, StubDeckPrimerPacketService, FakeDeckComparisonService, StubMetaGapService, FakeMetaGapService, ThrowingMetaGapService, ThrowingDeckAnalysisPacketService, FakeDeckAnalysisPacketService, FakeScryfallSetService, ThrowingCardSearchService, FakeCardLookupService, ThrowingCardLookupService, StubSuccessfulCardLookupService, StubSuccessfulSingleCardLookupService, FakeCategorySuggestionService, FakeMechanicLookupService, StubSuccessfulMechanicLookupService.
NOTE: DeckSyncApiControllerTests.cs defines its OWN FakeDeckSyncService (different file, private) — leave it; do NOT touch DeckSyncApiControllerTests.cs. The relocated fakes here are INTERNAL (not private nested) so multiple test files share them; name-collision with DeckSyncApiControllerTests' private FakeDeckSyncService is fine (private wins inside that file).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Relocate shared test fakes to DeckControllerTestFakes.cs</name>
  <read_first>
    - DeckFlow.Web.Tests/DeckControllerTests.cs (L811-1110 — all 19 Fake/Stub/Throwing nested classes + their using set L1-14)
  </read_first>
  <action>
    Create DeckFlow.Web.Tests/DeckControllerTestFakes.cs (CRLF — new file) in namespace DeckFlow.Web.Tests. Move all 19 service-double classes (FakeDeckSyncService ... StubSuccessfulMechanicLookupService, full list in interfaces) verbatim out of DeckControllerTests.cs into this file, changing each from `private sealed class` to `internal sealed class` so the new per-controller test files can reference them. Carry their XML docs + bodies byte-for-byte. Add the usings these doubles need (System, System.Collections.Generic, System.Net, System.Threading, System.Threading.Tasks, DeckFlow.Core.Integration, DeckFlow.Core.Reporting, DeckFlow.Web.Services, DeckFlow.Web.Models — verify by build).
    Do NOT yet delete DeckControllerTests.cs — Task 2-4 carve its test cases out; this task only relocates the fakes (remove the 19 nested classes from DeckControllerTests.cs, leaving the test methods temporarily referencing the now-internal fakes in the same namespace — they still resolve).
    After this task DeckControllerTests.cs still compiles (its tests reference the relocated internal fakes by simple name, same namespace). Build .Tests to confirm.
  </action>
  <acceptance_criteria>
    - DeckControllerTestFakes.cs contains all 19 doubles as `internal sealed class`.
    - grep -cE "private sealed class (Fake|Stub|Throwing)" DeckFlow.Web.Tests/DeckControllerTests.cs == 0 (all moved out).
    - DeckFlow.Web.Tests builds clean: 0 errors, 0 new warnings. (DeckControllerTests.cs still present + green at this step.)
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; grep -cE "private sealed class (Fake|Stub|Throwing)" DeckFlow.Web.Tests/DeckControllerTests.cs</automated>
  </verify>
  <done>Shared fakes live in DeckControllerTestFakes.cs as internal classes; .Tests builds clean.</done>
</task>

<task type="auto">
  <name>Task 2: Carve out DeckLookupControllerTests + DeckPacketControllerTests + DeckCategoriesControllerTests, delete DeckControllerTests.cs</name>
  <read_first>
    - DeckFlow.Web.Tests/DeckControllerTests.cs (all test methods L24-809; the per-test ctor construction)
    - DeckFlow.Web/Controllers/DeckLookupController.cs, DeckCategoriesController.cs, DeckPacketController.cs (the narrowed ctor signatures to call)
  </read_first>
  <action>
    Create three new test files (CRLF), each `public sealed class XxxControllerTests` in namespace DeckFlow.Web.Tests, distributing the test methods per the map in the interfaces block:
    - DeckLookupControllerTests.cs: the CardLookup/DownloadCardLookup/SingleCardLookup/MechanicLookup tests.
    - DeckCategoriesControllerTests.cs: CardSearch_ReturnsServiceUnavailable + the two BuildNoSuggestionsMessage tests.
    - DeckPacketControllerTests.cs: the CedhMetaGap/DeckAnalysis/DeckComparison tests.
    In EACH carried-over test, replace the 12-arg `new DeckController(...)` construction with the new controller's narrowed ctor, passing ONLY the doubles that controller needs + `NullLogger<NewController>.Instance` (the ONLY generic-type change permitted per SRP-03). Keep every assertion + ControllerContext/HttpContext setup + the action invocation (controller.X(...)) otherwise byte-for-byte. Example: a SingleCardLookup test changes from `new DeckController(... 12 args ..., NullLogger<DeckController>.Instance)` to `new DeckLookupController(new FakeCardLookupService(), new FakeMechanicLookupService(), NullLogger<DeckLookupController>.Instance)` (pick the specific fakes the test originally passed for those two services — e.g. the throwing/stub variant the test used). The BuildNoSuggestionsMessage tests construct NO controller (call the static directly) — carry them verbatim, just into the Categories file.
    Add per-file usings mirroring the original test file's using set (System, System.Net, System.Threading[.Tasks], DeckFlow.Web.Controllers, DeckFlow.Web.Models, DeckFlow.Core.Reporting, DeckFlow.Web.Services, Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Mvc, Microsoft.Extensions.Logging.Abstractions, Xunit) — trim to what each file references; verify by build.
    DELETE DeckFlow.Web.Tests/DeckControllerTests.cs (git rm) — every test case now lives in a per-controller file and the DeckController type no longer exists (deleted in Plan 04). Confirm via grep that the total carried test-method count equals the original 24 (1 CardSearch + 2 BuildNoSuggestions + 7 lookup + 4 analysis + 3 comparison + 3 metagap + ... — count from the original file and assert equality so no test is silently dropped).
    Build the full solution clean.
  </action>
  <acceptance_criteria>
    - DeckControllerTests.cs is deleted (git status shows removal); no file references `new DeckController(` or `NullLogger<DeckController>` anywhere in DeckFlow.Web.Tests.
    - The three new files together contain the SAME number of [Fact]/[Theory] methods as the original DeckControllerTests.cs (24). grep -c "[Fact]" summed across the 3 new files == original count.
    - Each test constructs its new controller via the narrowed ctor with NullLogger<NewController>.Instance; grep finds new DeckLookupController(, new DeckPacketController(, new DeckCategoriesController( and zero new DeckController(.
    - Full solution builds clean: 0 errors, 0 new warnings (DeckFlow.Web.Tests + DeckFlow.Core.Tests + DeckFlow.Web + DeckFlow.CLI).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|Build succeeded|Warning" | tail -8; grep -rc "new DeckController(" DeckFlow.Web.Tests 2>/dev/null | grep -v ':0'; test -f DeckFlow.Web.Tests/DeckControllerTests.cs && echo "STILL-EXISTS-FAIL" || echo "deleted-ok"</automated>
  </verify>
  <done>Tests mirror-split into 3 per-controller files; DeckControllerTests.cs deleted; only logger-generic refs changed; full solution builds clean with no new warnings.</done>
</task>

<task type="auto">
  <name>Task 3: SC1 route-parity gate — prove pre==post URL set</name>
  <read_first>
    - DeckFlow.Web/Controllers/ (all new controllers: Shell, DeckSync, DeckConvert, DeckLookup, DeckCategories, DeckPacket, DeckPrimer, JudgeQuestions)
    - .planning/phases/38-controller-srp-split/38-01-SUMMARY.md (the recorded pre-split baseline git SHA — see step (a) below)
    - .planning/phases/38-controller-srp-split/38-CONTEXT.md (D-02 + the SC1 capture-method requirement)
  </read_first>
  <action>
    Mechanically prove route parity (SC1). Capture the POST/GET attribute-route set BEFORE and AFTER the split and diff them:
    (a) PRE list: read the pre-split baseline git SHA recorded in 38-01-SUMMARY.md (Plan 01 captured `git rev-parse HEAD` BEFORE any split edits; expected value ~c315a94, but use whatever SHA the SUMMARY records). Do NOT derive HEAD~N by counting commits — it is error-prone across this multi-commit refactor and a wrong N silently invalidates the SC1 proof. Extract the original DeckController's route attribute strings from that exact commit: git show <baseline-sha>:DeckFlow.Web/Controllers/DeckController.cs piped through grep -oE 'Http(Get|Post)\("[^"]+"\)' | sort -u. Save to a temp file pre-routes.txt. If 38-01-SUMMARY.md is missing the baseline SHA, STOP and report it (do NOT fall back to HEAD~N guessing).
    ALSO handle the Error action (no route attr): the original DeckController.Error() was conventional /Deck/Error; after the split Error() lives on ShellController and resolves conventionally as /Shell/Error, and Plan 01 re-pointed app.UseExceptionHandler("/Deck/Error") -> app.UseExceptionHandler("/Shell/Error") to match. So the conventional error route changed controller NAME only (Deck -> Shell), with the handler re-pointed accordingly — user-visible error behavior is unchanged, but the route string is NOT "unchanged". Record this Deck->Shell conventional-error-route move explicitly in the SUMMARY; do NOT claim "/Deck/Error survives".
    (b) POST list: grep -rhoE 'Http(Get|Post)\("[^"]+"\)' across ALL DeckFlow.Web/Controllers/*.cs (the 8 new controllers) | sort -u. Save to post-routes.txt.
    (c) diff pre-routes.txt post-routes.txt — MUST be empty (zero adds/removes/changes) for the attribute-routed set. The attribute strings are the URLs (attribute routing per D-02), so identical attribute-string sets == identical URL set.
    Record both lists + the empty diff + the baseline SHA used + the Deck->Shell conventional-error-route note verbatim in the SUMMARY as the SC1 proof artifact. If the diff is non-empty, STOP and report the discrepancy (a route was dropped or altered during a move — a defect to fix before phase close, not to accept).
    Do NOT modify any production code in this task — it is verification only. If a parity gap is found, surface it; the fix belongs in whichever feature plan dropped the route.
    Why no EndpointDataSource runtime dump: the app cannot be reliably booted headless in WSL (project Constraints: push-and-watch CI / manual harness). The attribute-string set IS the authoritative URL source for attribute-routed actions, so the static grep-diff is a sound, deterministic SC1 proof.
  </action>
  <acceptance_criteria>
    - A pre-routes.txt (from the baseline-SHA DeckController.cs, SHA read from 38-01-SUMMARY.md) and post-routes.txt (from all new controllers) are captured.
    - diff of the two sorted unique attribute-route lists is EMPTY.
    - The conventional error route is recorded as moving /Deck/Error -> /Shell/Error (controller-name change only; UseExceptionHandler re-pointed in Plan 01), NOT claimed as "unchanged".
    - The route count matches the known inventory: GET /, /api/set-options, /sync, /convert, /convert/commander-search, /card-lookup (+/download,/download-json,/single), /mechanic-lookup, /suggest-categories (+/card-search), /judge-questions, /deck-analysis (+/download,/upload), /deck-comparison (+/download,/upload), /cedh-meta-gap (+/download,/upload), /deck-primer (+/download,/upload), /resolve. (POST /sync and POST /resolve etc. are distinct verb+path entries.)
    - SUMMARY contains the verbatim pre/post lists + empty diff + baseline SHA + Deck->Shell error-route note.
  </acceptance_criteria>
  <verify>
    <automated>grep -rhoE 'Http(Get|Post)\("[^"]+"\)' DeckFlow.Web/Controllers/*.cs | sort -u > /tmp/post-routes.txt; wc -l /tmp/post-routes.txt; echo "--- post route set ---"; cat /tmp/post-routes.txt</automated>
  </verify>
  <done>SC1 proven: pre and post attribute-route sets are identical (empty diff); baseline SHA sourced from 38-01-SUMMARY.md; Deck->Shell conventional error-route move recorded; proof in SUMMARY.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| n/a (test + verification only) | This plan changes only test code + runs a route-diff. No production route/input/auth change. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-08 | n/a | test fakes + route-diff | accept | No new attack surface — test relocation + a static verification gate; no production code, inputs, or auth touched. |

No package installs; no new inputs. No HIGH-severity threats.
</threat_model>

<verification>
- Full solution (DeckFlow.sln) builds clean: 0 errors, 0 new warnings (SRP-03).
- Every original DeckControllerTests case is preserved in a per-controller file with only the logger-generic + ctor-arity change (SRP-03).
- Pre==post attribute-route set with empty diff (SC1), using the baseline SHA from 38-01-SUMMARY.md.
- The /Deck/Error -> /Shell/Error conventional error-route move is documented (not mis-recorded as unchanged).
- Shared fakes relocated to internal classes; DeckControllerTests.cs deleted.
</verification>

<success_criteria>
- Tests mirror-split per D-05; .Tests + full solution build clean with no new warnings.
- SC1 route parity mechanically proven (empty pre/post diff) against the recorded baseline SHA.
- No test case silently dropped (count preserved).
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-06-SUMMARY.md` when done. Record: the test-to-controller distribution, the preserved test count, confirmation only logger-generics + ctor changed, the relocated-fakes file, DeckControllerTests.cs deletion, the baseline SHA read from 38-01-SUMMARY.md, the verbatim SC1 pre/post route lists + empty diff, and the /Deck/Error -> /Shell/Error conventional error-route note. Confirm full-solution build clean.
</output>
