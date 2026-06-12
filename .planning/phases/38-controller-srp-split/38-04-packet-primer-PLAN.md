---
phase: 38-controller-srp-split
plan: 04
type: execute
wave: 4
depends_on: ["38-01", "38-02", "38-03"]
files_modified:
  - DeckFlow.Web/Controllers/DeckPacketController.cs
  - DeckFlow.Web/Controllers/DeckPrimerController.cs
  - DeckFlow.Web/Controllers/DeckController.cs
autonomous: true
requirements: [SRP-01]
must_haves:
  truths:
    - "GET/POST /deck-analysis + /deck-analysis/download + /deck-analysis/upload behave exactly as before"
    - "GET/POST /deck-comparison + /download + /upload behave exactly as before"
    - "GET/POST /cedh-meta-gap + /download + /upload behave exactly as before"
    - "GET/POST /deck-primer + /download + /upload behave exactly as before"
  artifacts:
    - path: "DeckFlow.Web/Controllers/DeckPacketController.cs"
      provides: "ChatGPT-packet family: deck-analysis, deck-comparison, cedh-meta-gap (GET/POST/download/upload each)"
      contains: "class DeckPacketController"
    - path: "DeckFlow.Web/Controllers/DeckPrimerController.cs"
      provides: "Primer family: deck-primer GET/POST/download/upload"
      contains: "class DeckPrimerController"
  key_links:
    - from: "DeckFlow.Web/Controllers/DeckPacketController.cs"
      to: "PacketSessionCache"
      via: "cache-key short-circuit on downloads"
      pattern: "_packetCache\\.TryGet"
    - from: "DeckFlow.Web/Controllers/DeckPrimerController.cs"
      to: "IDeckPrimerPacketService"
      via: "BuildAsync"
      pattern: "_deckPrimerPacketService\\.BuildAsync"
---

<objective>
Extract the final family out of `DeckController`: the ChatGPT-packet workflows. Per D-01 discretion (Primer MAY split out if it stays SRP and preserves URLs/tabs), this plan creates TWO controllers — `DeckPacketController` for the three packet generators (deck-analysis, deck-comparison, cedh-meta-gap, each with GET/POST/download/upload) and `DeckPrimerController` for the deck-primer workflow. Splitting Primer keeps each controller within SRP and within executor context budget (the packet group alone is 12 actions). After this plan, `DeckController` is fully emptied; the final task deletes the now-empty `DeckController.cs`.

Purpose: SRP-01 — complete the by-family decomposition. DeckPacketController injects the analysis/comparison/meta-gap services + PacketSessionCache; DeckPrimerController injects only the primer service + cache.

Output: Two new controller files; `DeckController.cs` deleted (its last members moved out). All packet/primer URLs, tabs, cache short-circuits, upload size limits, and antiforgery tokens preserved verbatim.
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
DeckPacketController owns (move verbatim — attributes, [RequestSizeLimit], [ValidateAntiForgeryToken], cache short-circuits, comments byte-for-byte):
ANALYSIS:
- HttpGet "/deck-analysis" -> DeckAnalysis()                              // DeckController.cs ~L161-169
- HttpPost "/deck-analysis" + ValidateAntiForgeryToken -> async DeckAnalysis(DeckAnalysisRequest)  // ~L481-526
- HttpPost "/deck-analysis/download" + ValidateAntiForgeryToken -> async DeckAnalysisDownload(DeckAnalysisRequest)  // ~L760-836
- HttpPost "/deck-analysis/upload" + ValidateAntiForgeryToken + RequestSizeLimit(11MB) -> async DeckAnalysisUpload(IFormFile)  // ~L842-909
COMPARISON:
- HttpGet "/deck-comparison" -> DeckComparison()                          // ~L174-182
- HttpPost "/deck-comparison" + ValidateAntiForgeryToken -> async DeckComparison(DeckComparisonRequest)  // ~L915-975
- HttpPost "/deck-comparison/download" + ValidateAntiForgeryToken -> async DeckComparisonDownload(DeckComparisonRequest)  // ~L981-1099
- HttpPost "/deck-comparison/upload" + ValidateAntiForgeryToken + RequestSizeLimit(11MB) -> DeckComparisonUpload(IFormFile)  // ~L1105-1199
META-GAP:
- HttpGet "/cedh-meta-gap" -> CedhMetaGap()                               // ~L187-195
- HttpPost "/cedh-meta-gap" + ValidateAntiForgeryToken -> async CedhMetaGap(MetaGapRequest)  // ~L1205-1265
- HttpPost "/cedh-meta-gap/download" + ValidateAntiForgeryToken -> async CedhMetaGapDownload(MetaGapRequest)  // ~L1271-1361
- HttpPost "/cedh-meta-gap/upload" + ValidateAntiForgeryToken + RequestSizeLimit(11MB) -> CedhMetaGapUpload(IFormFile)  // ~L1367-1459
- Dependencies: IDeckAnalysisPacketService, IDeckComparisonService, IMetaGapService, PacketSessionCache
- Const moved with it: CorruptedZipMessage (DeckController.cs L22) — used by all three upload paths (analysis, comparison, meta-gap, AND primer). See note below.
- Static helpers used (stay put): PacketArtifactStore, ResponseParsers (TruncatedResponseMessage), DeckComparisonService (BuildRequestContextText / ParseComparisonResponse), MetaGapService (BuildRequestContextText / ParseResponse), DeckAnalysisPacketService (BuildRequestContextText), UpstreamErrorMessageBuilder, AiPlatform, CommanderBracketCatalog.

DeckPrimerController owns:
- HttpGet "/deck-primer" -> DeckPrimer()                                  // ~L200-212
- HttpPost "/deck-primer" + ValidateAntiForgeryToken -> async DeckPrimer(DeckPrimerRequest)  // ~L532-583
- HttpPost "/deck-primer/download" + ValidateAntiForgeryToken -> async DeckPrimerDownload(DeckPrimerRequest)  // ~L589-650
- HttpPost "/deck-primer/upload" + ValidateAntiForgeryToken + RequestSizeLimit(11MB) -> async DeckPrimerUpload(IFormFile)  // ~L656-754
- Dependencies: IDeckPrimerPacketService, PacketSessionCache
- Also uses CorruptedZipMessage and CommanderBracketCatalog.

CorruptedZipMessage handling: the const string "The uploaded zip contains an incomplete response payload..." is used by upload paths in BOTH controllers. Duplicate it as a private const in EACH controller (DeckPacketController + DeckPrimerController) — do NOT extract to a shared static (project convention: prompt/copy strings are intentionally duplicated, not centralized; see reference_prompt_variants_intentionally_decoupled). Both copies must be byte-identical to the original.

Both controllers inherit DeckToolControllerBase. ActiveTab values (DeckAnalysis/DeckComparison/CedhMetaGap/DeckPrimer) are set in action bodies — carried automatically.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create DeckPacketController (analysis + comparison + meta-gap families)</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L22 CorruptedZipMessage; L158-195 GETs; L477-526, 756-1199, 1201-1459 — all packet POST/download/upload actions for the three families)
    - DeckFlow.Web/Controllers/DeckToolControllerBase.cs
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckPacketController.cs (CRLF — new file). Declare public sealed class DeckPacketController : DeckToolControllerBase. Ctor injects IDeckAnalysisPacketService deckAnalysisPacketService, IDeckComparisonService deckComparisonService, IMetaGapService metaGapService, PacketSessionCache packetCache, ILogger<DeckPacketController> logger — all ThrowIfNull-guarded, private readonly fields. XML doc.
    Add a private const string CorruptedZipMessage byte-identical to DeckController.cs L22.
    MOVE verbatim (attributes, [RequestSizeLimit], [ValidateAntiForgeryToken], all inline comments, cache short-circuit logic byte-for-byte) the TWELVE packet actions for the three families: the three GETs (DeckAnalysis, DeckComparison, CedhMetaGap) and the nine POST/download/upload actions listed in the interfaces block. Do NOT touch the bodies — these contain audited cache-key parity logic (Phase 999.3 D-10/D-11) and the analysis/download asymmetry comment; carry them exactly.
    DELETE all twelve actions from DeckController.cs. Remove the IDeckAnalysisPacketService, IDeckComparisonService, IMetaGapService field/param/assignment from DeckController (grep each; their only users are the moved actions). Do NOT remove PacketSessionCache or IDeckPrimerPacketService from DeckController yet — Primer (Task 2) still lives there and uses both. Leave the CorruptedZipMessage const in DeckController for now (Primer's upload uses it; Task 2 removes the DeckController copy when it deletes the file).
    Add usings by grepping references: Microsoft.AspNetCore.Mvc, System.Net, System.Text.Json, DeckFlow.Core.Integration (EdhTop16Entry / EdhTop16Card / EdhTop16 types), DeckFlow.Core.Reporting, DeckFlow.Web.Infrastructure, DeckFlow.Web.Models, DeckFlow.Web.Services. Verify exact namespaces by build (EdhTop16Entry/EdhTop16Card namespace must be confirmed from the source file's existing usings).
    Preserve LF in DeckController.cs; touch only moved/deleted lines.
  </action>
  <acceptance_criteria>
    - DeckPacketController.cs declares public sealed class DeckPacketController : DeckToolControllerBase, ctor guarded.
    - Contains all 12 packet actions with attribute strings intact: HttpGet/HttpPost "/deck-analysis", "/deck-analysis/download", "/deck-analysis/upload", "/deck-comparison" (+download/upload), "/cedh-meta-gap" (+download/upload). grep counts: 3 RequestSizeLimit attributes present (the three upload actions).
    - grep -nE "_deckAnalysisPacketService|_deckComparisonService|_metaGapService|\"/deck-analysis|\"/deck-comparison|\"/cedh-meta-gap" DeckFlow.Web/Controllers/DeckController.cs returns NOTHING.
    - PacketSessionCache + IDeckPrimerPacketService STILL present in DeckController (grep finds both — used by Primer until Task 2).
    - Web csproj builds: 0 errors, 0 new warnings.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; grep -cE "_deckAnalysisPacketService|_deckComparisonService|_metaGapService|/deck-analysis|/deck-comparison|/cedh-meta-gap" DeckFlow.Web/Controllers/DeckController.cs; grep -cE "_deckPrimerPacketService|_packetCache" DeckFlow.Web/Controllers/DeckController.cs</automated>
  </verify>
  <done>DeckPacketController owns the three packet families with all cache/upload logic intact; DeckController retains only primer members; Web build clean.</done>
</task>

<task type="auto">
  <name>Task 2: Create DeckPrimerController and delete the emptied DeckController</name>
  <read_first>
    - DeckFlow.Web/Controllers/DeckController.cs (L22 CorruptedZipMessage; L197-212 GET; L528-754 — primer POST/download/upload; confirm what remains after Task 1)
    - DeckFlow.Web/Controllers/DeckToolControllerBase.cs
  </read_first>
  <action>
    Create DeckFlow.Web/Controllers/DeckPrimerController.cs (CRLF — new file). Declare public sealed class DeckPrimerController : DeckToolControllerBase. Ctor injects IDeckPrimerPacketService deckPrimerPacketService, PacketSessionCache packetCache, ILogger<DeckPrimerController> logger — all ThrowIfNull-guarded, private readonly fields. XML doc.
    Add a private const string CorruptedZipMessage byte-identical to the original (DeckController.cs L22).
    MOVE verbatim (attributes, [RequestSizeLimit(11MB)], [ValidateAntiForgeryToken], bodies byte-for-byte): DeckPrimer() (HttpGet "/deck-primer"), DeckPrimer(DeckPrimerRequest) (HttpPost "/deck-primer"), DeckPrimerDownload (HttpPost "/deck-primer/download"), DeckPrimerUpload (HttpPost "/deck-primer/upload"). These bodies use _deckPrimerPacketService, _packetCache, PacketArtifactStore, AiPlatform, CommanderBracketCatalog, ResponseParsers.TruncatedResponseMessage, UpstreamErrorMessageBuilder.
    After moving, DeckController.cs is EMPTY of actions and of all dependencies. Verify by grep that DeckController.cs now contains only the class shell (ctor + fields + usings, no [Http*] actions). Then DELETE the file DeckFlow.Web/Controllers/DeckController.cs entirely (git rm). Why: every action and helper has been redistributed; an empty DeckController violates one-type-per-purpose and would leave a dead 12-arg ctor. Removing it is the clean SRP endpoint. (Confirm no code references the DeckController TYPE before deleting — grep the whole Web project + Program.cs; AddControllersWithViews discovers controllers by convention, so there is no manual registration to update. The only known type reference is in DeckControllerTests.cs, which Plan 05 rewrites — that is expected and the .Tests build stays red until Plan 05.)
    Add usings in DeckPrimerController.cs by grepping references: Microsoft.AspNetCore.Mvc, System.Net, DeckFlow.Web.Infrastructure, DeckFlow.Web.Models, DeckFlow.Web.Services. Verify by build.
  </action>
  <acceptance_criteria>
    - DeckPrimerController.cs declares public sealed class DeckPrimerController : DeckToolControllerBase, ctor guarded, with the four primer actions and attribute strings HttpGet "/deck-primer", HttpPost "/deck-primer", HttpPost "/deck-primer/download", HttpPost "/deck-primer/upload" (1 RequestSizeLimit present on the upload).
    - DeckFlow.Web/Controllers/DeckController.cs no longer exists (git status shows it deleted).
    - grep -rnE "new DeckController\(|: DeckController|DeckController " DeckFlow.Web/ Program.cs returns NOTHING (no production reference to the deleted type).
    - Web csproj builds: 0 errors, 0 new warnings. (.Tests still red — Plan 05.)
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | grep -E "error|Build succeeded" | tail -5; test -f DeckFlow.Web/Controllers/DeckController.cs && echo "STILL EXISTS - FAIL" || echo "deleted-ok"; grep -rcE "new DeckController\(" DeckFlow.Web/ 2>/dev/null | grep -v ':0' | head</automated>
  </verify>
  <done>DeckPrimerController owns the primer family; DeckController.cs is deleted; no production code references the old type; Web build clean.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> packet/primer upload + download routes | Pre-existing; unchanged. Same routes, same 11MB RequestSizeLimit, same antiforgery tokens, same zip-parse error handling. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-05 | Denial of Service | upload endpoints (analysis/comparison/meta-gap/primer) | accept | The [RequestSizeLimit(11MB)] attributes move byte-for-byte with each upload action; zip-bomb / oversize protections unchanged. No new attack surface — pure move refactor. |
| T-38-06 | Tampering | uploaded-zip deserialization (PacketArtifactStore.Load*FromZip) | accept | Untrusted-zip handling (InvalidDataException catch, TruncatedResponseMessage->CorruptedZipMessage) moves verbatim; same validation pre/post. |

No package installs; no new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- Web project builds clean after both tasks.
- DeckController.cs deleted; no production reference to the DeckController type remains.
- All 16 packet+primer URLs retain exact attribute strings (URL set unchanged for SC1).
- The three upload-RequestSizeLimit attributes are preserved (one per upload action: 4 total across the two new controllers).
</verification>

<success_criteria>
- DeckPacketController + DeckPrimerController exist as sealed controllers inheriting the base, each injecting only its own services.
- All packet + primer URLs, tabs, cache short-circuits, upload limits, and error handling preserved.
- DeckController fully decomposed and removed.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-04-SUMMARY.md` when done. Record the action distribution across the two controllers, confirm DeckController.cs deletion, list the per-controller injected services, and confirm Web build clean. Note the CorruptedZipMessage was duplicated (not centralized) per project convention.
</output>
