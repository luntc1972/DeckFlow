---
phase: quick-260506-hgd
plan: 01
type: execute
wave: 1
depends_on: []
autonomous: false
mode: quick
files_modified:
  - DeckFlow.Web/Models/ChatGptDeckRequest.cs
  - DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs
  - DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs
  - DeckFlow.Web/Models/ChatGptDeckViewModel.cs
  - DeckFlow.Web/Models/ChatGptDeckComparisonViewModel.cs
  - DeckFlow.Web/Models/ChatGptCedhMetaGapViewModel.cs
  - DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs              # delete
  - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs              # rewrite as zip producer
  - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
  - DeckFlow.Web/Services/ChatGptDeckComparisonService.cs
  - DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs
  - DeckFlow.Web/Controllers/DeckController.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
  - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
  - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
  - DeckFlow.Web/wwwroot/ts/deck-sync.ts
  - DeckFlow.Web.Tests/DeckControllerTests.cs
  - DeckFlow.Web.Tests/ChatGptDeckPacketServiceTests.cs
  - DeckFlow.Web.Tests/ChatGptDeckComparisonServiceTests.cs
  - README.md
requirements: []
must_haves:
  truths:
    - "User clicks Download on Step 3 (analysis) or Step 5 (set upgrade) of /chatgpt-packets and receives a single .zip containing every artifact with content."
    - "User clicks Download on Step 3 of /chatgpt-deck-comparison and receives a single .zip with comparison artifacts + response."
    - "User clicks Download on Step 3 of /chatgpt-cedh-meta-gap and receives a single .zip with meta-gap artifacts + response."
    - "User selects a previously downloaded .zip via the Upload control on any of the three pages, posts it, and the page rehydrates DeckProfileJson / ComparisonResponseJson / MetaGapResponseJson and lands on the matching results step."
    - "No code path under /data/ChatGPT Analysis writes new files; existing files on disk are untouched."
    - "/api/saved-sessions endpoint is removed (404, not [])."
    - "dotnet build of DeckFlow.sln succeeds with zero warnings introduced; existing tests build and pass."
    - "README.md sections at lines 272, 352, 417 describe the local download/upload flow (no 'temporarily disabled' language remains)."
  artifacts:
    - path: "DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs"
      provides: "In-memory zip producer (no filesystem writes, no LoadInto from disk)"
      exports: ["BuildZip(...)", "LoadFromZip(Stream zipStream, ChatGptDeckRequest request)"]
    - path: "DeckFlow.Web/Controllers/DeckController.cs"
      provides: "Six new actions: ChatGptPacketsDownload(POST), ChatGptPacketsUpload(POST), ChatGptDeckComparisonDownload(POST), ChatGptDeckComparisonUpload(POST), ChatGptCedhMetaGapDownload(POST), ChatGptCedhMetaGapUpload(POST)"
    - path: "DeckFlow.Web/wwwroot/ts/deck-sync.ts"
      provides: "Upload <input type=file> change handler that submits the form to the page's /upload endpoint; loadSavedSessionsAsync removed."
  key_links:
    - from: "Three Razor views"
      to: "DeckController download/upload actions"
      via: "buttons inside Step 3/5 result panels post to /<page>/download (form action override) and /<page>/upload (multipart form action override)"
    - from: "DeckController upload actions"
      to: "ChatGptPacketArtifactStore.LoadFromZip"
      via: "IFormFile.OpenReadStream() into ZipArchive(ZipArchiveMode.Read), populate request, return same View() the GET-with-data path returns"
---

<objective>
Replace shared server-side `/data/ChatGPT Analysis/` storage with a local browser download/upload flow on the three ChatGPT-paste workflow pages: `/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`. Stopgap commit `0021908` already disabled the read/write paths and removed the UI; this plan finishes the restructure by deleting dead infrastructure and shipping the new download/upload flow.

Purpose: privacy fix (no shared `/data` volume across users) and durability (user owns their session artifacts on their own machine, can re-import any time without server retention).

Output: Working Download (.zip) and Upload (.zip) buttons on each of the three pages; all `IChatGptArtifactsDirectory` / `SaveArtifactsToDisk` / `ImportArtifactsPath` infrastructure removed; `/api/saved-sessions` route removed; tests rewritten to cover the zip seam; README updated to describe the new flow.

Implementation routes through Codex MCP (gpt-5.4 full — multi-file scope spans services, controller, views, TS, tests, README).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/PROJECT.md
@./CLAUDE.md
@.planning/quick/260506-hgd-chatgpt-artifact-local-download-upload-r/260506-hgd-CONTEXT.md

<interfaces>
<!-- Existing surface the executor needs. Extracted at planning time. -->

ChatGptDeckRequest (DeckFlow.Web/Models/ChatGptDeckRequest.cs):
  string DeckProfileJson { get; set; }
  string SetUpgradeResponseJson { get; set; }
  int WorkflowStep { get; set; }
  string DeckUrl, DeckText, DeckSource (routes by DeckInputSource)
  // To DELETE in this plan:
  bool SaveArtifactsToDisk { get; set; }                 // line 61
  string ImportArtifactsPath { get; set; }               // lines 186-196

ChatGptDeckComparisonRequest (DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs):
  int WorkflowStep { get; set; }
  string DeckASource, DeckBSource, DeckAName, DeckBName, DeckABracket, DeckBBracket
  string ComparisonResponseJson { get; set; }
  // To DELETE: bool SaveArtifactsToDisk { get; set; }   // line 15

ChatGptCedhMetaGapRequest (DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs):
  int WorkflowStep { get; set; }
  string CommanderName, DeckSource, MetaGapResponseJson
  CedhMetaTimePeriod TimePeriod; CedhMetaSortBy SortBy; int MinEventSize; int? MaxStanding; List<int> SelectedReferenceIndexes
  // To DELETE: bool SaveArtifactsToDisk { get; set; }   // line 11

DeckController (DeckFlow.Web/Controllers/DeckController.cs):
  Existing routes:
    [HttpGet ("/chatgpt-packets")] / [HttpPost ("/chatgpt-packets")]            // 146, 458
    [HttpGet ("/chatgpt-deck-comparison")] / [HttpPost ("/chatgpt-deck-comparison")]  // 159, 510
    [HttpGet ("/chatgpt-cedh-meta-gap")] / [HttpPost ("/chatgpt-cedh-meta-gap")]      // 172, 577
    [HttpGet ("/api/saved-sessions")] -> Json([])                               // 195   (DELETE entire action)
  Field/ctor to remove: _chatGptArtifactsDirectory + IChatGptArtifactsDirectory parameter (lines 33, 50, 63)

ChatGptPacketArtifactStore (DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs):
  Current public surface (rewritten by this plan):
    SaveAsync(ChatGptDeckRequest request, string? commanderName, string inputSummary, string? requestContextText,
              string? referenceText, string? analysisPromptText, string deckProfileSchemaJson,
              string? setUpgradePromptText, CancellationToken ct) -> Task<string outputDirectory>
    LoadInto(ChatGptDeckRequest request)                                        // disk-based, throws if not under root
  Existing helpers to keep & refactor as private methods on the new zip producer:
    BuildCombinedArtifactText, ExtractJsonObject, CreateSafePathSegment

IChatGptArtifactsDirectory + ChatGptArtifactsDirectory + SavedSession record (DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs):
  Entire file is dead code post-restructure -> DELETE.

Service surface (unchanged contracts; only their private SaveArtifactsAsync paths and force-clearing stopgap go away):
  IChatGptDeckPacketService.BuildAsync(ChatGptDeckRequest, CancellationToken) -> Task<ChatGptDeckPacketResult>
  IChatGptDeckComparisonService.BuildAsync(ChatGptDeckComparisonRequest, CancellationToken) -> Task<ChatGptDeckComparisonResult>
  IChatGptCedhMetaGapService.BuildAsync(ChatGptCedhMetaGapRequest, CancellationToken) -> Task<ChatGptCedhMetaGapResult>

Result records — drop the `SavedArtifactsDirectory` member (no longer meaningful):
  ChatGptDeckPacketResult                     (DeckFlow.Web/Services/ChatGptDeckPacketService.cs lines 34-45)
  ChatGptDeckComparisonResult                 (DeckFlow.Web/Services/ChatGptDeckComparisonService.cs lines 24-36)
  ChatGptCedhMetaGapResult                    (DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs)

ViewModels — drop the `SavedArtifactsDirectory` property:
  ChatGptDeckViewModel
  ChatGptDeckComparisonViewModel
  ChatGptCedhMetaGapViewModel

Existing form anchors in views (form action defaults to the page URL):
  ChatGptPackets.cshtml:78           <form method="post" class="deck-form chatgpt-packets-form" data-cache-key="chatgpt-packets" ...>
  ChatGptDeckComparison.cshtml:177   <form method="post" class="deck-form" data-cache-key="chatgpt-deck-comparison" ...>
  ChatGptCedhMetaGap.cshtml:46       <form method="post" class="deck-form" data-cache-key="chatgpt-cedh-meta-gap" ...>
  Step-3 / Step-5 result panels are already marked with `data-chatgpt-result-anchor`. New Download/Upload toolbars sit inside those panels, gated by the same `Model.AnalysisResponse is not null` / `Model.SetUpgradeResponse is not null` / `Model.ComparisonResponse is not null` / `Model.AnalysisResponse is not null` (cEDH) checks.

Existing `SavedArtifactsDirectory` banner blocks to DELETE from views:
  ChatGptPackets.cshtml lines 50-54
  ChatGptDeckComparison.cshtml lines 160-164
  ChatGptCedhMetaGap.cshtml lines 37-41

DI registrations (DeckFlow.Web/Program.cs):
  Line 252  AddSingleton<IChatGptArtifactsDirectory, ChatGptArtifactsDirectory>()                  -> DELETE
  Line 253  AddScoped<IChatGptDeckPacketService>(...)        -- keep, drop chatGptArtifactsPath param
  Line 266  AddScoped<IChatGptDeckComparisonService>(...)    -- keep, drop artifactsPath param
  Line 277  AddScoped<IChatGptCedhMetaGapService>(...)       -- keep, drop artifactsPath param

TypeScript handlers in DeckFlow.Web/wwwroot/ts/deck-sync.ts:
  Lines 2380, 2391-2423   loadSavedSessionsAsync + [data-saved-sessions-*] / [data-chatgpt-import-path] wiring
                          -> DELETE in full. New upload handler wires `<input type="file" data-chatgpt-zip-upload>` change events.

Tests (DeckFlow.Web.Tests/) referencing the old surface:
  DeckControllerTests.cs:923                FakeChatGptArtifactsDirectory                 -> DELETE
  ChatGptDeckPacketServiceTests.cs:1290,1333  request.SaveArtifactsToDisk = true          -> rewrite as BuildZip-based assertions or DELETE if no longer reachable
  ChatGptDeckComparisonServiceTests.cs:26    SaveArtifactsToDisk = true                   -> rewrite or DELETE
  Any DeckControllerTests ctor that passes IChatGptArtifactsDirectory                     -> drop the parameter
</interfaces>

<canonical_artifact_inventory>
The zip produced by the BuildZip method MUST contain every entry below for which content is non-empty. File name + label list is the canonical contract — do NOT rename or reorder. Naming follows existing on-disk numbering so existing operator backups remain compatible.

Packets zip (per /chatgpt-packets request):
  00-input-summary.txt              "INPUT SUMMARY"
  01-request-context.txt            "REQUEST CONTEXT"
  30-reference.txt                  "REFERENCE TEXT"
  31-analysis-prompt.txt            "ANALYSIS PROMPT"
  41-deck-profile-schema.json       "DECK PROFILE JSON SCHEMA"
  50-set-upgrade-prompt.txt         "SET UPGRADE PROMPT"
  40-deck-profile.json              "DECK PROFILE JSON"          (extracted via ExtractJsonObject)
  51-set-upgrade-response.json      "SET UPGRADE RESPONSE JSON"  (extracted via ExtractJsonObject)
  all-prompts.txt                   combined text of the prompt sections present
  all-responses.txt                 combined text of the response sections present

Comparison zip (per /chatgpt-deck-comparison request):
  00-comparison-input-summary.txt
  10-deck-a-list.txt
  11-deck-b-list.txt
  12-deck-a-combos.txt
  13-deck-b-combos.txt
  20-comparison-context.txt
  30-comparison-prompt.txt
  31-comparison-schema.json
  32-comparison-follow-up-prompt.txt
  40-deck-comparison-response.json  (ExtractJsonPayload)

cEDH meta-gap zip (per /chatgpt-cedh-meta-gap request):
  00-input-summary.txt
  30-meta-gap-prompt.txt
  31-meta-gap-schema.json
  40-meta-gap-response.json         (ExtractJsonPayload)

Re-import contract (server reads only the *response* JSON entries):
  Packets:    40-deck-profile.json -> request.DeckProfileJson;
              51-set-upgrade-response.json -> request.SetUpgradeResponseJson;
              WorkflowStep = (loaded51 ? 5 : 3); else throw "no recognized response JSON in zip".
  Comparison: 40-deck-comparison-response.json -> request.ComparisonResponseJson;
              WorkflowStep = 3; throw if missing.
  cEDH:       40-meta-gap-response.json -> request.MetaGapResponseJson;
              WorkflowStep = 3; throw if missing.

Allow-list filter on import (defense against malicious zip):
  Reject any entry whose name contains '/' or '\\' (no nested directories — flat zip).
  Reject any entry not in the matching page's name allow-list above.
  Cap individual entry uncompressed length at 2 MB; cap zip total uncompressed at 10 MB.
</canonical_artifact_inventory>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Rewrite ChatGptPacketArtifactStore as a pure in-memory zip producer/consumer; delete ChatGptArtifactsDirectory.cs</name>
  <files>
    DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs (rewrite),
    DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs (delete)
  </files>
  <action>
    Delete `DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs` entirely (interface, class, SavedSession record). It is dead code after the saved-sessions endpoint is removed.

    Rewrite `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` as a stateless static helper class (drop the `_rootPath` field, drop the constructor, drop `SaveAsync`, drop `LoadInto`):

    ```csharp
    namespace DeckFlow.Web.Services;

    /// <summary>
    /// Builds a single in-memory .zip of every ChatGPT analysis artifact for the current
    /// request, and rehydrates a saved zip back into a request. Pure CPU work, no
    /// filesystem access. Caller streams the zip back as application/zip via FileContentResult.
    /// </summary>
    internal static class ChatGptPacketArtifactStore
    {
        // Hard caps for re-import defence. Single zip stays well under Render Starter's 512MB.
        private const int MaxEntryUncompressedBytes = 2 * 1024 * 1024;
        private const int MaxTotalUncompressedBytes = 10 * 1024 * 1024;

        public static byte[] BuildZip(
            ChatGptDeckRequest request,
            string? commanderName,
            string inputSummary,
            string? requestContextText,
            string? referenceText,
            string? analysisPromptText,
            string deckProfileSchemaJson,
            string? setUpgradePromptText)
        {
            // promptSections (FileName, Label, Content?):
            //   ("01-request-context.txt", "REQUEST CONTEXT", requestContextText)
            //   ("00-input-summary.txt", "INPUT SUMMARY", inputSummary)
            //   ("30-reference.txt", "REFERENCE TEXT", referenceText)
            //   ("31-analysis-prompt.txt", "ANALYSIS PROMPT", analysisPromptText)
            //   ("41-deck-profile-schema.json", "DECK PROFILE JSON SCHEMA", deckProfileSchemaJson)
            //   ("50-set-upgrade-prompt.txt", "SET UPGRADE PROMPT", setUpgradePromptText)
            // responseSections:
            //   ("40-deck-profile.json", "DECK PROFILE JSON", request.DeckProfileJson)        <- ExtractJsonObject(...)
            //   ("51-set-upgrade-response.json", "SET UPGRADE RESPONSE JSON", request.SetUpgradeResponseJson) <- ExtractJsonObject(...)
            // Then add "all-prompts.txt" / "all-responses.txt" via existing BuildCombinedArtifactText.
            //
            // Use System.IO.Compression.ZipArchive on a MemoryStream with leaveOpen:false.
            // Encoding: UTF-8 (no BOM). Content uses StreamWriter; trim trailing whitespace and
            // append Environment.NewLine to match the previous on-disk format.
            // Only write entries where !string.IsNullOrWhiteSpace(Content).
            // Return ms.ToArray().
        }

        /// <summary>
        /// Reads a zip stream and populates DeckProfileJson and/or SetUpgradeResponseJson on
        /// the request. Sets WorkflowStep so the existing standalone short-circuit fires:
        /// only 51 -> 5; else only 40 -> 3; throws if neither is present.
        /// Validates against PacketAllowedNames and the 2MB/10MB caps.
        /// Resets request.DeckUrl and request.DeckText to empty (mirrors old LoadInto).
        /// </summary>
        public static void LoadFromZip(Stream zipStream, ChatGptDeckRequest request)
        {
            // Use ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen:false).
            // For each entry: reject if FullName contains '/' or '\\', or if not in
            // PacketAllowedNames. Enforce entry.Length <= MaxEntryUncompressedBytes and
            // running total <= MaxTotalUncompressedBytes. Read via entry.Open() into a
            // string with StreamReader(UTF-8).
            //
            // Capture deckProfile = content of "40-deck-profile.json" (if present, non-empty)
            //         setUpgrade  = content of "51-set-upgrade-response.json" (if present, non-empty)
            // If both null/empty: throw new InvalidOperationException(
            //   "Imported zip did not contain 40-deck-profile.json or 51-set-upgrade-response.json.");
            // Else assign to request.DeckProfileJson / request.SetUpgradeResponseJson
            // and set request.WorkflowStep = setUpgrade is not null ? 5 : 3;
            // request.DeckUrl = string.Empty; request.DeckText = string.Empty;
        }

        public static void LoadComparisonFromZip(Stream zipStream, ChatGptDeckComparisonRequest request)
        {
            // Same shape; allow-list = ComparisonAllowedNames; reads "40-deck-comparison-response.json";
            // assigns to request.ComparisonResponseJson; sets request.WorkflowStep = 3;
            // throws if entry missing or empty.
        }

        public static void LoadCedhMetaGapFromZip(Stream zipStream, ChatGptCedhMetaGapRequest request)
        {
            // Same shape; allow-list = CedhAllowedNames; reads "40-meta-gap-response.json";
            // assigns to request.MetaGapResponseJson; sets request.WorkflowStep = 3;
            // throws if entry missing or empty.
        }

        // Equivalents for the two new BuildZip overloads:
        public static byte[] BuildComparisonZip(ChatGptDeckComparisonRequest request,
            string inputSummary, string deckAListText, string deckBListText,
            string deckAComboText, string deckBComboText, string comparisonContextText,
            string comparisonPromptText, string followUpPromptText, string comparisonSchemaJson)
        { /* see Comparison zip inventory above */ }

        public static byte[] BuildCedhMetaGapZip(ChatGptCedhMetaGapRequest request,
            string inputSummary, string promptText, string schemaJson)
        { /* see cEDH zip inventory above */ }

        // Keep ExtractJsonObject + BuildCombinedArtifactText + CreateSafePathSegment as private static
        // helpers in this file. CreateSafePathSegment is reused for the suggested zip filename
        // (returned via a separate helper, e.g. SuggestPacketZipFileName(commanderName)).

        public static string SuggestPacketZipFileName(string? commanderName)
            => $"{CreateSafePathSegment(commanderName, "deckflow-packet")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

        public static string SuggestComparisonZipFileName(string deckAName, string deckBName)
            => $"{CreateSafePathSegment($"{deckAName}-vs-{deckBName}", "deck-comparison")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

        public static string SuggestCedhMetaGapZipFileName(string commanderName)
            => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

        // Hard-coded allow-lists (HashSet<string> with OrdinalIgnoreCase) sourced from the
        // canonical_artifact_inventory block in this PLAN.md.
    }
    ```

    Add a single `using System.IO.Compression;` to the top of the file. Class becomes `internal static`. Update `[InternalsVisibleTo("DeckFlow.Web.Tests")]` is already in `DeckFlow.Web/AssemblyInfo.cs:3`, so the rewrite remains test-visible.

    Per CONTEXT D: "Reuse `ChatGptPacketArtifactStore.SaveAsync` logic but stream into an in-memory `ZipArchive`". This task delivers exactly that.

    QA gate: Codex must confirm twice that (a) every artifact filename in `canonical_artifact_inventory` appears verbatim in the corresponding BuildZip method, and (b) no `File.WriteAllTextAsync`, `Directory.CreateDirectory`, `Path.Combine(_rootPath, ...)`, or other filesystem APIs remain in this file.
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.sln -nologo 2>&1 | tail -20</automated>
    Build succeeds. `grep -c 'File\.\|Directory\.' DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` returns 0. `ls DeckFlow.Web/Services/ChatGptArtifactsDirectory.cs` returns "No such file".
  </verify>
  <done>
    `ChatGptArtifactsDirectory.cs` deleted. `ChatGptPacketArtifactStore.cs` is a stateless `internal static` class exposing `BuildZip(...)`, `BuildComparisonZip(...)`, `BuildCedhMetaGapZip(...)`, `LoadFromZip(...)`, `LoadComparisonFromZip(...)`, `LoadCedhMetaGapFromZip(...)`, plus three `Suggest*ZipFileName(...)` helpers. No filesystem APIs, no instance state. dotnet build still passes (next tasks fix the call sites).
  </done>
</task>

<task type="auto">
  <name>Task 2: Update three ChatGPT services to drop on-disk save paths and the SaveArtifactsToDisk stopgap</name>
  <files>
    DeckFlow.Web/Services/ChatGptDeckPacketService.cs,
    DeckFlow.Web/Services/ChatGptDeckComparisonService.cs,
    DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs
  </files>
  <action>
    For all three services:

    1. Remove the `string? chatGptArtifactsPath = null` / `string? artifactsPath = null` constructor parameter and the matching field (`_artifactStore`, `_artifactsPath`). Drop `ResolveChatGptArtifactsPath` (Packet service) and the inline `_artifactsPath = ... MyDocuments ...` block (Comparison + cEDH services). Drop `using Microsoft.AspNetCore.Hosting;` from `ChatGptDeckComparisonService.cs` and the `IWebHostEnvironment environment` ctor param + `ArgumentNullException.ThrowIfNull(environment)` (it was only used for the `_artifactsPath`).

    2. In each `BuildAsync`, delete:
       - The `// Server-side artifact ... disabled — pending local download/upload restructure` comment block.
       - `request.ImportArtifactsPath = string.Empty;` (Packet only)
       - `request.SaveArtifactsToDisk = false;` (all three)
       - The `if (request.SaveArtifactsToDisk) { savedArtifactsDirectory = await SaveArtifactsAsync(...); }` block (all three; both step-3 and step-5 instances in Packet service at lines 172-183 and 216-227, plus the shared one at lines 497-511).
       - The `private (Task<string>|async Task<string>) SaveArtifactsAsync(...)` method itself in each service (Packet line 1721, Comparison line 849, cEDH line 829) and any private helpers used only by it (`CreateSafePathSegment` in cEDH line 862; `BuildRequestContextText` in Packet line 1741 — KEEP this one, repurposed below).
       - Local `savedArtifactsDirectory` variable and its usage in the result record.
       - `BuildRequestContextText` in Packet service: KEEP the method (zip download still needs it), but drop the `builder.AppendLine($"save_artifacts_to_disk: {request.SaveArtifactsToDisk}");` line (line 1745) — now meaningless.

    3. Drop `SavedArtifactsDirectory` from the result records:
       ```csharp
       // Before (Packet, lines 34-45):
       public sealed record ChatGptDeckPacketResult(string InputSummary, ..., string? SavedArtifactsDirectory, string? TimingSummary, ...)
       // After:
       public sealed record ChatGptDeckPacketResult(string InputSummary, ..., string? TimingSummary, ChatGptDeckAnalysisResponse? AnalysisResponse = null, ChatGptSetUpgradeResponse? SetUpgradeResponse = null, string? ImportWarning = null);
       ```
       Same surgery on `ChatGptDeckComparisonResult` (drop `string? SavedArtifactsDirectory`) and `ChatGptCedhMetaGapResult` (drop `string? SavedArtifactsDirectory`).

    4. Update each `BuildAsync` return to omit the dropped argument.

    5. The `_artifactsPath` field in Comparison + cEDH was used only by the deleted SaveArtifactsAsync. After deletion they can be removed wholesale.

    Do NOT touch the BuildRequestContextText, BuildAnalysisPrompt, BuildSetUpgradePrompt, BuildComparisonPrompt, BuildFollowUpPrompt, BuildInputSummary, or BuildTimingSummary helpers. They produce content used by downstream views AND by the new Download endpoint.

    QA gate: Codex confirms twice that (a) zero references to `SaveArtifactsToDisk`, `ImportArtifactsPath`, `_artifactStore`, `_artifactsPath`, `SaveArtifactsAsync`, or `IChatGptArtifactsDirectory` remain in the three service files; (b) `dotnet build` is clean; (c) the existing service unit tests still build (their assertions on `SavedArtifactsDirectory` / `SaveArtifactsToDisk` will be fixed in Task 6, but the surface mutation must still compile against them at this stage by leaving the test files broken intentionally — flag this in the commit body).
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.Web/DeckFlow.Web.csproj -nologo 2>&1 | tail -20</automated>
    `DeckFlow.Web` builds clean. `grep -rn 'SaveArtifactsToDisk\|ImportArtifactsPath\|_artifactStore\|_artifactsPath\|SaveArtifactsAsync\|IChatGptArtifactsDirectory' DeckFlow.Web/Services/ChatGpt*.cs` returns no matches.
  </verify>
  <done>
    Three services no longer accept artifacts-path constructor params, no longer reference `SaveArtifactsToDisk`/`ImportArtifactsPath`, no longer write to `/data/ChatGPT Analysis/`. Result records lost their `SavedArtifactsDirectory` field. `BuildAsync` keeps producing all the prompt/schema text the new download endpoint will package into a zip. `DeckFlow.Web` builds. (`DeckFlow.Web.Tests` may fail to compile — Task 6 fixes that.)
  </done>
</task>

<task type="auto">
  <name>Task 3: Add Download/Upload controller actions; remove IChatGptArtifactsDirectory and /api/saved-sessions</name>
  <files>
    DeckFlow.Web/Controllers/DeckController.cs,
    DeckFlow.Web/Program.cs,
    DeckFlow.Web/Models/ChatGptDeckRequest.cs,
    DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs,
    DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs,
    DeckFlow.Web/Models/ChatGptDeckViewModel.cs,
    DeckFlow.Web/Models/ChatGptDeckComparisonViewModel.cs,
    DeckFlow.Web/Models/ChatGptCedhMetaGapViewModel.cs
  </files>
  <action>
    Model surgery first — clean inputs make the controller actions trivial:

    - In `ChatGptDeckRequest.cs`: delete `public bool SaveArtifactsToDisk { get; set; }` (line 61) and the entire `ImportArtifactsPath` property block + its backing field (lines 186-196). Keep everything else.
    - In `ChatGptDeckComparisonRequest.cs`: delete `public bool SaveArtifactsToDisk { get; set; }` (line 15).
    - In `ChatGptCedhMetaGapRequest.cs`: delete `public bool SaveArtifactsToDisk { get; set; }` (line 11).
    - In all three `*ViewModel.cs` files (`ChatGptDeckViewModel`, `ChatGptDeckComparisonViewModel`, `ChatGptCedhMetaGapViewModel`): delete the `string? SavedArtifactsDirectory { get; init; }` property and its assignment in any `with` expressions inside the controller (Task 3 step below).

    `DeckFlow.Web/Controllers/DeckController.cs` surgery:

    1. Drop the `_chatGptArtifactsDirectory` field (line 33), the `IChatGptArtifactsDirectory chatGptArtifactsDirectory` ctor param (line 50), and the matching assignment (line 63).
    2. Drop the `using DeckFlow.Web.Services;` reference to the deleted interface — already covered by namespace usage; just leave imports clean.
    3. Delete the entire `[HttpGet("/api/saved-sessions")] public IActionResult GetSavedSessions()` action (lines 195-203).
    4. In the existing three `HttpPost` actions for `/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`, drop the `SavedArtifactsDirectory = result.SavedArtifactsDirectory` line from the `View(...)` expressions.

    Add six new actions, three pairs (Download / Upload), each `[ValidateAntiForgeryToken]`. They live alongside the existing POST handlers.

    Pattern A — Download (POST returning `FileContentResult application/zip`):

    ```csharp
    [HttpPost("/chatgpt-packets/download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChatGptPacketsDownload(ChatGptDeckRequest request)
    {
        request ??= new ChatGptDeckRequest();
        try
        {
            var result = await _chatGptDeckPacketService.BuildAsync(request, HttpContext.RequestAborted);
            // Reconstruct the request-context text the same way the service did pre-stopgap.
            // The service's `BuildRequestContextText(request, commanderName)` is private — promote it
            // to internal (or expose via a small helper on the service) so the controller can
            // call it. Alternative: keep the helper private and instead extend the result record
            // with `RequestContextText` so the zip producer doesn't need controller-side wiring.
            // ** Recommended: extend ChatGptDeckPacketResult with `string? RequestContextText` **
            // populated inside BuildAsync (single-line addition next to existing fields).
            var commanderName = result.AnalysisResponse?.Commander
                                ?? request.DeckName;     // matches existing SuggestedChatTitle fallback
            var bytes = ChatGptPacketArtifactStore.BuildZip(
                request,
                commanderName,
                result.InputSummary,
                result.RequestContextText,
                result.ReferenceText,
                result.AnalysisPromptText,
                result.DeckProfileSchemaJson,
                result.SetUpgradePromptText);
            var fileName = ChatGptPacketArtifactStore.SuggestPacketZipFileName(commanderName);
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException ex) { /* same view-with-error pattern as the existing POST */ }
        catch (HttpRequestException ex)      { /* same view-with-error pattern */ }
    }
    ```

    Pattern B — Upload (POST `IFormFile zipFile`):

    ```csharp
    [HttpPost("/chatgpt-packets/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]            // 11 MB hard cap (1 MB headroom over the
                                                    // 10 MB BuildZip cap)
    public async Task<IActionResult> ChatGptPacketsUpload(IFormFile zipFile)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return View("ChatGptPackets", new ChatGptDeckViewModel
            {
                ActiveTab = DeckPageTab.ChatGptPackets,
                Request = new ChatGptDeckRequest(),
                ErrorMessage = "Choose a .zip file produced by Download to import."
            });
        }
        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return View("ChatGptPackets", new ChatGptDeckViewModel
            {
                ActiveTab = DeckPageTab.ChatGptPackets,
                Request = new ChatGptDeckRequest(),
                ErrorMessage = "Only .zip files produced by Download are accepted."
            });
        }
        var request = new ChatGptDeckRequest();
        try
        {
            await using var stream = zipFile.OpenReadStream();
            ChatGptPacketArtifactStore.LoadFromZip(stream, request);
            // Re-render via the existing POST handler's view-builder by re-calling BuildAsync,
            // which already short-circuits when DeckSource is empty and DeckProfileJson or
            // SetUpgradeResponseJson is set (see ChatGptDeckPacketService.cs:162-239 standalone branches).
            var result = await _chatGptDeckPacketService.BuildAsync(request, HttpContext.RequestAborted);
            return View("ChatGptPackets", new ChatGptDeckViewModel
            {
                ActiveTab = DeckPageTab.ChatGptPackets,
                Request = request,
                InputSummary = result.InputSummary,
                SuggestedChatTitle = result.SuggestedChatTitle,
                ReferenceText = result.ReferenceText,
                AnalysisPromptText = result.AnalysisPromptText,
                DeckProfileSchemaJson = result.DeckProfileSchemaJson,
                SetUpgradePromptText = result.SetUpgradePromptText,
                TimingSummary = result.TimingSummary,
                AnalysisResponse = result.AnalysisResponse,
                SetUpgradeResponse = result.SetUpgradeResponse,
                ImportWarning = result.ImportWarning,
            });
        }
        catch (InvalidOperationException ex)
        {
            return View("ChatGptPackets", new ChatGptDeckViewModel
            {
                ActiveTab = DeckPageTab.ChatGptPackets,
                Request = new ChatGptDeckRequest(),
                ErrorMessage = ex.Message
            });
        }
        catch (InvalidDataException)
        {
            return View("ChatGptPackets", new ChatGptDeckViewModel
            {
                ActiveTab = DeckPageTab.ChatGptPackets,
                Request = new ChatGptDeckRequest(),
                ErrorMessage = "The uploaded file is not a valid .zip archive."
            });
        }
    }
    ```

    Implement the parallel pair on `/chatgpt-deck-comparison/download` + `/chatgpt-deck-comparison/upload` and `/chatgpt-cedh-meta-gap/download` + `/chatgpt-cedh-meta-gap/upload`. All six actions sit immediately after their existing GET/POST counterparts in the controller. Use the same try/catch pattern as the existing `[HttpPost("/chatgpt-deck-comparison")]` and `[HttpPost("/chatgpt-cedh-meta-gap")]` handlers (which catch `InvalidOperationException` + `HttpRequestException` and re-render the page with `ErrorMessage`).

    `DeckFlow.Web/Program.cs` surgery:
    - Delete `builder.Services.AddSingleton<IChatGptArtifactsDirectory, ChatGptArtifactsDirectory>();` (line 252).
    - In the three `AddScoped<IChatGpt*Service>(sp => new ...(...))` registrations (lines 253, 266, 277), drop the trailing `chatGptArtifactsPath`/`artifactsPath` argument so the call matches the trimmed constructor signatures from Task 2.
    - Drop the `IWebHostEnvironment` argument from the comparison service factory (also fall-out of Task 2).

    Same-Origin / antiforgery: `[ValidateAntiForgeryToken]` is applied to every new POST, matching the existing handlers. The form already renders the antiforgery token via the existing form (no change needed). `SameOriginRequestValidator` is configured in `Program.cs` against API endpoints and is unchanged — these new MVC endpoints inherit the same protection chain because they post under the same Origin as the form.

    QA gate: Codex confirms twice (a) every download action returns `File(bytes, "application/zip", filename)`, (b) every upload action gates by content-length and `.zip` extension, (c) `SuggestedSetUpgradeResponseJson` and `DeckProfileJson` paths from the existing standalone short-circuit branches in `ChatGptDeckPacketService.BuildAsync` still fire correctly when called post-upload (see lines 162-239 and 197-239), (d) `_chatGptArtifactsDirectory` is gone from the controller, (e) `/api/saved-sessions` returns 404 (route deleted, not the empty-array stopgap).
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.Web/DeckFlow.Web.csproj -nologo 2>&1 | tail -10</automated>
    `DeckFlow.Web` builds. `grep -c 'IChatGptArtifactsDirectory\|ChatGptArtifactsDirectory\|api/saved-sessions' DeckFlow.Web/Controllers/DeckController.cs DeckFlow.Web/Program.cs` returns 0. Six new `[HttpPost]` actions present. App starts without DI errors: `dotnet run --project DeckFlow.Web --no-build &` then `curl -s -o /dev/null -w '%{http_code}' http://localhost:5173/api/saved-sessions` returns `404`.
  </verify>
  <done>
    Controller has six new `*Download` / `*Upload` actions and no longer holds `IChatGptArtifactsDirectory`. `/api/saved-sessions` route is gone (404, not `[]`). `Program.cs` no longer registers `IChatGptArtifactsDirectory`. Three `*Request` models lost `SaveArtifactsToDisk` (and `ImportArtifactsPath` on the packet model). Three `*ViewModel` classes lost `SavedArtifactsDirectory`. Build is green.
  </done>
</task>

<task type="auto">
  <name>Task 4: Razor view changes — Download buttons in result panels, Upload control near top, remove dead artifact banners</name>
  <files>
    DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml,
    DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml,
    DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
  </files>
  <action>
    Per CONTEXT D — manual Download button in each results panel; single zip; same picker for upload.

    For ALL three views:

    1. Delete the `@if (!string.IsNullOrWhiteSpace(Model.SavedArtifactsDirectory)) { ... "Saved ... artifacts to" ... }` banner block (Packets:50-54, Comparison:160-164, cEDH:37-41).

    2. Add an Upload control inside the existing `<form>` immediately above the workflow-step tab strip (the `_WorkflowStepTabs` partial). One `<input type="file">` is enough — visually styled as a small "Resume from saved session (.zip)" details/summary block to keep the top of the form quiet. Markup pattern (use `formaction`+`formenctype` so the existing top-level form is reused without changing the default action):

       ```cshtml
       <details class="chatgpt-resume" data-chatgpt-resume>
         <summary>Resume from a saved session (.zip)</summary>
         <p class="sync-column__hint">
           Choose a <code>.zip</code> file you previously downloaded from Step 3 (or Step 5).
           This rehydrates the response JSON and jumps to the matching results step.
         </p>
         <label class="field">
           <span>Saved session file</span>
           <input type="file" name="zipFile" accept=".zip,application/zip"
                  data-chatgpt-zip-upload
                  data-upload-action="@Url.Content("~/chatgpt-packets/upload")" />
         </label>
         <button type="submit"
                 class="run-button"
                 formaction="@Url.Content("~/chatgpt-packets/upload")"
                 formenctype="multipart/form-data"
                 formmethod="post">
           Upload &amp; Resume
         </button>
       </details>
       ```

       (For the Comparison view use `~/chatgpt-deck-comparison/upload`; for the cEDH view use `~/chatgpt-cedh-meta-gap/upload`.)

    3. Add a Download button **inside each existing results panel** (gated by the same `@if (Model.AnalysisResponse is not null)` etc. that already gates the result render):

       Packets — TWO download buttons, one per results step:
       - Step 3 panel (line 436+, inside `@if (Model.AnalysisResponse is not null) { ... }`): add a small toolbar above the existing summary panel:
         ```cshtml
         <div class="toolbar chatgpt-step-actions">
           <button type="submit" class="run-button"
                   formaction="@Url.Content("~/chatgpt-packets/download")"
                   formmethod="post">
             Download session (.zip)
           </button>
         </div>
         ```
       - Step 5 panel (line 669+, inside `@if (Model.SetUpgradeResponse is not null) { ... }`): same button, same `formaction`. Both buttons re-submit the full form so the server re-runs `BuildAsync` and packages the current state — this is the simplest path and avoids a second hidden state-mirror endpoint.

       Comparison — Step 3 panel (line 498+, inside `@if (Model.ComparisonResponse is not null) { ... }`):
         ```cshtml
         <button type="submit" class="run-button"
                 formaction="@Url.Content("~/chatgpt-deck-comparison/download")"
                 formmethod="post">
           Download comparison session (.zip)
         </button>
         ```

       cEDH — Step 3 panel (line 299+, inside `@if (Model.AnalysisResponse is not null) { ... }`):
         ```cshtml
         <button type="submit" class="run-button"
                 formaction="@Url.Content("~/chatgpt-cedh-meta-gap/download")"
                 formmethod="post">
           Download meta-gap session (.zip)
         </button>
         ```

    4. Re-running BuildAsync on download is fine *if* the form already carries enough state to short-circuit. The Packets `BuildAsync` already short-circuits at lines 162-239 when `DeckSource` is empty and `DeckProfileJson` (or `SetUpgradeResponseJson`) is set — so downloading from Step 3/Step 5 after pasting the JSON will NOT re-run Scryfall lookups. Comparison and cEDH services run their full pipelines every POST regardless; that is acceptable here (CONTEXT D-script: "pick whichever avoids re-running expensive Scryfall/banlist work" — for Comparison/cEDH the data is already cached via existing `IMemoryCache` paths and the user clicked Download intentionally). Note this in the commit body.

    5. Visual polish: any new layout CSS goes in `DeckFlow.Web/wwwroot/css/site-common.css` per CLAUDE.md theme-architecture rule. If purely toolbar-button placement, no new CSS needed (`.toolbar` and `.run-button` already exist).

    QA gate: Codex confirms twice (a) every Download/Upload control sits inside the existing `<form>`, (b) `formaction` and `formmethod="post"` are set on every submit override, (c) `enctype` is `multipart/form-data` only on the Upload submit (via `formenctype`), (d) all three "Saved artifacts to ..." banner `@if` blocks are gone.
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.Web/DeckFlow.Web.csproj -nologo 2>&1 | tail -5</automated>
    Razor compilation passes (no warnings about missing `SavedArtifactsDirectory`/`SaveArtifactsToDisk`/`ImportArtifactsPath`). `grep -c 'SavedArtifactsDirectory\|SaveArtifactsToDisk\|ImportArtifactsPath\|saved-sessions' DeckFlow.Web/Views/Deck/ChatGpt*.cshtml` returns 0. Three `formaction=".../upload"` and three `formaction=".../download"` (four for packets) markers present.
  </verify>
  <done>
    All three views render Download buttons inside their results panels and an Upload `<details>` block near the top of their form. No "Saved artifacts to /data/..." banner remains. Razor builds clean.
  </done>
</task>

<task type="auto">
  <name>Task 5: TypeScript wiring — remove saved-sessions handlers, add upload submit handler</name>
  <files>
    DeckFlow.Web/wwwroot/ts/deck-sync.ts
  </files>
  <action>
    1. Delete `loadSavedSessionsAsync` invocation (line ~2380) and the full function definition (lines ~2391-2423-ish). Delete every reference to `[data-saved-sessions-url]`, `[data-saved-sessions-select]`, `[data-saved-sessions-empty]`, `[data-chatgpt-import-path]` selectors.

    2. Add a small change handler that auto-submits the form when a user picks a zip:

       ```ts
       const wireChatGptZipUpload = (): void => {
         document.querySelectorAll<HTMLInputElement>('[data-chatgpt-zip-upload]').forEach((input) => {
           input.addEventListener('change', () => {
             const file = input.files?.[0];
             if (!file) return;
             // Defer to the existing busy-indicator pattern by submitting the form
             // via the dedicated upload button so the form's `formaction` overrides apply.
             const wrapper = input.closest('details');
             const submit = wrapper?.querySelector<HTMLButtonElement>('button[formaction$="/upload"]');
             submit?.click();
           });
         });
       };
       ```

       Call `wireChatGptZipUpload()` from the same DOMContentLoaded init block that previously called `loadSavedSessionsAsync()`.

    3. Do NOT add any new download-side TS — the Razor `<button type="submit" formaction=".../download">` handles it natively (the browser's default form-submit behavior is exactly what's needed; the response is `application/zip` with `Content-Disposition: attachment` and the browser downloads it).

    4. The MSBuild `CompileTypeScriptAssets` target compiles `wwwroot/ts/*.ts` -> `wwwroot/js/*.js` on every build. No package.json or tsconfig changes.

    QA gate: Codex confirms twice (a) zero remaining references in `deck-sync.ts` to `loadSavedSessionsAsync`, `data-saved-sessions-`, or `data-chatgpt-import-path`; (b) the new `wireChatGptZipUpload` is wired into the same init block the old function was in; (c) `tsc --noEmit -p DeckFlow.Web/tsconfig.json` passes (or, equivalently, `dotnet build DeckFlow.Web` passes since the MSBuild task runs `tsc`).
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.Web/DeckFlow.Web.csproj -nologo 2>&1 | tail -5</automated>
    Build green. `grep -c 'loadSavedSessionsAsync\|data-saved-sessions-\|data-chatgpt-import-path' DeckFlow.Web/wwwroot/ts/deck-sync.ts` returns 0. `grep -c 'wireChatGptZipUpload\|data-chatgpt-zip-upload' DeckFlow.Web/wwwroot/ts/deck-sync.ts` returns >= 2.
  </verify>
  <done>
    Saved-sessions polling code deleted. New zip-upload change handler auto-clicks the Upload submit so the user gets the existing busy indicator + page rerender for free. tsc compile is clean.
  </done>
</task>

<task type="auto">
  <name>Task 6: Test cleanup — remove FakeChatGptArtifactsDirectory + SaveArtifactsToDisk assertions; add zip round-trip test</name>
  <files>
    DeckFlow.Web.Tests/DeckControllerTests.cs,
    DeckFlow.Web.Tests/ChatGptDeckPacketServiceTests.cs,
    DeckFlow.Web.Tests/ChatGptDeckComparisonServiceTests.cs,
    DeckFlow.Web.Tests/ChatGptPacketArtifactStoreTests.cs (new)
  </files>
  <action>
    1. `DeckControllerTests.cs`: delete `FakeChatGptArtifactsDirectory` (line 923) and remove the parameter from every `new DeckController(...)` constructor call (it's currently `IChatGptArtifactsDirectory` — drop it). If the helper that builds the controller (likely a private `BuildController` factory near the top of the test class) references the fake, drop both.

    2. `ChatGptDeckPacketServiceTests.cs` (lines 1290, 1333): the two tests assert the `SaveArtifactsToDisk = true` path. Delete those tests outright — the path no longer exists. (Caveman rule per CLAUDE.md: don't keep dead test scaffolding.)

    3. `ChatGptDeckComparisonServiceTests.cs` (line 26): same. Delete the test asserting `SaveArtifactsToDisk = true`. If the `request` builder was shared across tests, replace with `new ChatGptDeckComparisonRequest()` literal where each test still references it.

    4. Create `DeckFlow.Web.Tests/ChatGptPacketArtifactStoreTests.cs` with a round-trip test:

       ```csharp
       public sealed class ChatGptPacketArtifactStoreTests
       {
           [Fact]
           public void BuildZip_then_LoadFromZip_round_trips_response_json()
           {
               var request = new ChatGptDeckRequest
               {
                   DeckProfileJson = "{\"deck_profile\":{\"format\":\"Commander\"}}",
                   SetUpgradeResponseJson = "{\"set_upgrade_report\":{\"sets\":[]}}"
               };
               var bytes = ChatGptPacketArtifactStore.BuildZip(
                   request,
                   commanderName: "Atraxa",
                   inputSummary: "summary",
                   requestContextText: "context",
                   referenceText: null,
                   analysisPromptText: "analysis prompt",
                   deckProfileSchemaJson: "{}",
                   setUpgradePromptText: "upgrade prompt");

               var loaded = new ChatGptDeckRequest();
               using var ms = new MemoryStream(bytes);
               ChatGptPacketArtifactStore.LoadFromZip(ms, loaded);

               Assert.Contains("deck_profile", loaded.DeckProfileJson);
               Assert.Contains("set_upgrade_report", loaded.SetUpgradeResponseJson);
               Assert.Equal(5, loaded.WorkflowStep);     // both present -> step 5
           }

           [Fact]
           public void LoadFromZip_throws_when_no_response_json_present()
           {
               using var ms = new MemoryStream();
               using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
               {
                   var entry = archive.CreateEntry("00-input-summary.txt");
                   using var sw = new StreamWriter(entry.Open());
                   sw.Write("noise only");
               }
               ms.Position = 0;

               Assert.Throws<InvalidOperationException>(() =>
                   ChatGptPacketArtifactStore.LoadFromZip(ms, new ChatGptDeckRequest()));
           }

           [Fact]
           public void LoadFromZip_rejects_directory_traversal_entries()
           {
               using var ms = new MemoryStream();
               using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
               {
                   var entry = archive.CreateEntry("../escape.json");
                   using var sw = new StreamWriter(entry.Open());
                   sw.Write("{}");
               }
               ms.Position = 0;

               // No matching response JSON, so the malicious entry is silently skipped and the
               // load throws InvalidOperationException for "no recognized response JSON".
               Assert.Throws<InvalidOperationException>(() =>
                   ChatGptPacketArtifactStore.LoadFromZip(ms, new ChatGptDeckRequest()));
           }
       }
       ```

       Add equivalents for comparison + cEDH if time permits — minimum bar is the three packet tests above.

    QA gate: Codex confirms twice (a) zero remaining references to `FakeChatGptArtifactsDirectory`, `IChatGptArtifactsDirectory`, `SaveArtifactsToDisk`, or `ImportArtifactsPath` in `DeckFlow.Web.Tests/`; (b) `dotnet build DeckFlow.sln` passes; (c) the round-trip test passes (run via `dotnet test --filter ChatGptPacketArtifactStoreTests` if VSTest is reachable; otherwise note the WSL VSTest limitation in the commit body and rely on the CI gate after push).
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && dotnet build DeckFlow.sln -nologo 2>&1 | tail -10</automated>
    Whole solution builds clean. `grep -rn 'FakeChatGptArtifactsDirectory\|IChatGptArtifactsDirectory\|SaveArtifactsToDisk\|ImportArtifactsPath' DeckFlow.Web.Tests/ DeckFlow.Core.Tests/` returns no matches in source files (build artifacts under `bin/` are ignored). New `ChatGptPacketArtifactStoreTests.cs` exists with at least three `[Fact]` methods.
  </verify>
  <done>
    Test project no longer references the deleted artifact infrastructure. New round-trip + traversal-rejection tests exercise the BuildZip + LoadFromZip seam. Solution builds. (VSTest may still be unreliable in WSL — operator pushes to CI for the actual run.)
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 7: Local smoke test on the live form</name>
  <what-built>
    Six new endpoints (`/chatgpt-packets/{download,upload}`, `/chatgpt-deck-comparison/{download,upload}`, `/chatgpt-cedh-meta-gap/{download,upload}`), Download buttons on Step 3/5 result panels of all three pages, an Upload `<details>` block at the top of each form, full removal of `IChatGptArtifactsDirectory` / `SaveArtifactsToDisk` / `ImportArtifactsPath` / `/api/saved-sessions` / on-disk save paths.
  </what-built>
  <how-to-verify>
    1. `dotnet run --project DeckFlow.Web` (or the VS launchSettings profile). Open `http://localhost:5173/chatgpt-packets`.
    2. Paste a known `deck_profile` JSON into Step 3 (use a previously saved one from `/data/ChatGPT Analysis/.../40-deck-profile.json` if available). Click "Render Analysis Summary" — the summary panel renders. Confirm a "Download session (.zip)" button is visible inside that panel.
    3. Click Download. Browser saves a `<commander>-<timestamp>.zip` file. Open the zip — confirm it contains every file listed in `<canonical_artifact_inventory>` for the Packets section that has content (00, 01, 30 if applicable, 31, 40, 41, 50 if applicable, 51 if applicable, all-prompts.txt, all-responses.txt).
    4. Refresh the page (state cleared). Open the "Resume from a saved session (.zip)" `<details>` block. Pick the zip you just downloaded. The form should auto-submit and re-render Step 3 (or Step 5 if the zip had a 51 entry) populated with the same JSON.
    5. Repeat the round-trip on `/chatgpt-deck-comparison` (Step 3 only) and `/chatgpt-cedh-meta-gap` (Step 3 only).
    6. `curl -i http://localhost:5173/api/saved-sessions` -> expect HTTP 404 (route is gone, NOT `[]`).
    7. Optional: run `ls /data/ChatGPT\ Analysis/<commander>/` on Render shell after downloading and confirm no new timestamped directories were created. (Existing directories remain — CONTEXT D-cleanup says leave them alone.)
    8. Negative case: try uploading a non-zip file. Page rerenders with the friendly error "Only .zip files produced by Download are accepted."
  </how-to-verify>
  <resume-signal>Type "approved" if all 8 checks pass, or describe what failed. Codex returns to fix any failure before continuing.</resume-signal>
</task>

<task type="auto">
  <name>Task 8: README rewrite + commit</name>
  <files>
    README.md
  </files>
  <action>
    Rewrite the three "Artifact saving (temporarily disabled)" sections at lines ~272, ~352, ~417 to describe the local download/upload flow. Each section becomes:

    ```markdown
    ### Artifact saving (local download / upload)

    On the **<page-name>** page, the Step 3 (and Step 5 on /chatgpt-packets) result panel
    includes a **Download session (.zip)** button. The zip contains every artifact for the
    current run — the input summary, request context, prompts, schemas, and response JSON
    blobs. Files are stored only on your machine; no copy is retained server-side.

    To resume a saved run later, expand **Resume from a saved session (.zip)** at the top
    of the form, choose the previously downloaded zip, and the page will rehydrate the
    response JSON into Step 3 (or Step 5 for /chatgpt-packets if the zip carries a
    set-upgrade response). The browser's busy indicator runs while the upload is processed.

    Zip contents (per page):
    - **/chatgpt-packets**: 00-input-summary, 01-request-context, 30-reference,
      31-analysis-prompt, 41-deck-profile-schema, 50-set-upgrade-prompt,
      40-deck-profile, 51-set-upgrade-response, all-prompts, all-responses.
    - **/chatgpt-deck-comparison**: 00-comparison-input-summary, 10-deck-a-list,
      11-deck-b-list, 12-deck-a-combos, 13-deck-b-combos, 20-comparison-context,
      30-comparison-prompt, 31-comparison-schema, 32-comparison-follow-up-prompt,
      40-deck-comparison-response.
    - **/chatgpt-cedh-meta-gap**: 00-input-summary, 30-meta-gap-prompt,
      31-meta-gap-schema, 40-meta-gap-response.

    Re-import only consumes the `40-*` (and `51-*` on Packets) response JSON; the rest
    rides along for your records or future ChatGPT context.
    ```

    Adapt the copy per page: replace `/chatgpt-packets` and the artifact list per the canonical inventory above. No per-page section should still contain "temporarily disabled" or reference `/data/ChatGPT Analysis/`.

    Then commit. Per CLAUDE.md: plain default-author, no Co-Authored-By trailer, README updated in the same commit as the behavior change. Suggested message:

    ```
    feat(chatgpt): local zip download/upload replaces server-side artifact save

    - Three pages (chatgpt-packets, chatgpt-deck-comparison, chatgpt-cedh-meta-gap)
      now produce a single .zip per session via Download buttons in their result
      panels and accept the same .zip back via an Upload picker at the top of
      the form. Server holds no per-user artifact state.
    - ChatGptArtifactsDirectory + IChatGptArtifactsDirectory + /api/saved-sessions
      removed. ChatGptPacketArtifactStore rewritten as a stateless in-memory
      ZipArchive producer/consumer.
    - SaveArtifactsToDisk and ImportArtifactsPath dropped from request models;
      SavedArtifactsDirectory dropped from result records and view models.
    - DeckController gains six [HttpPost] actions: {page}/{download,upload}.
    - TypeScript: loadSavedSessionsAsync removed, replaced by a small change
      handler that auto-submits the upload form.
    - Tests rewritten: FakeChatGptArtifactsDirectory deleted; new
      ChatGptPacketArtifactStoreTests covers BuildZip + LoadFromZip round-trip
      and traversal-rejection.
    - README artifact-saving sections rewritten to describe the local flow.

    Existing /data/ChatGPT Analysis/ files left untouched per CONTEXT.md
    (operator deletes via Render shell separately).

    Closes the privacy stopgap chain that started with 0021908.
    ```

    Push to `main`. Render auto-deploys; watch the build log per project convention.

    QA gate: Codex confirms twice (a) every "temporarily disabled" or `/data/ChatGPT Analysis/` reference in README.md is gone (`grep -c 'temporarily disabled\|/data/ChatGPT Analysis' README.md` returns 0), (b) git status is clean after the commit, (c) the commit author is the default repo user with no Co-Authored-By trailer.
  </action>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench && grep -c 'temporarily disabled\|/data/ChatGPT Analysis' README.md && git log -1 --format='%an <%ae>%n%B'</automated>
    First grep reports 0. git log shows default author, no Co-Authored-By trailer, message body matches the suggested form (or close).
  </verify>
  <done>
    README describes the live local download/upload flow. Single commit on `main` lands the full restructure; CI build log on Render is green.
  </done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln -nologo` exits 0 with no new warnings.
- `grep -rn 'IChatGptArtifactsDirectory\|SaveArtifactsToDisk\|ImportArtifactsPath\|api/saved-sessions' DeckFlow.Web/ DeckFlow.Web.Tests/ --include='*.cs' --include='*.cshtml' --include='*.ts' --include='*.json'` returns zero matches in source files (bin/obj XML doc artifacts are not source).
- `grep -c 'temporarily disabled\|/data/ChatGPT Analysis' README.md` returns 0.
- `curl -i http://localhost:5173/api/saved-sessions` returns 404.
- Local round-trip in Task 7 (download zip, refresh page, upload zip, results re-render) passes on all three pages.
- Render production deploy stays green after push.
</verification>

<success_criteria>
- Each of the three ChatGPT pages exposes a working Download button inside its results panel that returns `application/zip` with `Content-Disposition: attachment; filename=...`.
- Each of the three pages exposes a working Upload control that accepts the same .zip back and rerenders the page on the matching results step.
- No code path under `DeckFlow.Web/` writes to `/data/ChatGPT Analysis/` or `~/Documents/DeckFlow/ChatGPT*`.
- `IChatGptArtifactsDirectory`, `ChatGptArtifactsDirectory`, `SavedSession`, `/api/saved-sessions`, `SaveArtifactsToDisk`, `ImportArtifactsPath`, and `SavedArtifactsDirectory` are gone from the codebase (source files only — bin/obj XML doc artifacts are stale and refresh on next build).
- `dotnet build DeckFlow.sln` is clean.
- README documents the new local flow on all three pages.
- Single commit on `main`, plain author, no Co-Authored-By trailer.
- Render auto-deploy succeeds.
</success_criteria>

<output>
After completion, append a one-line entry to `.planning/STATE.md` under "Quick Tasks Completed":

```
| 260506-hgd | ChatGPT artifact local download/upload — replace server-side save and import | 2026-05-06 | <commit> | [260506-hgd-chatgpt-artifact-local-download-upload-r](./quick/260506-hgd-chatgpt-artifact-local-download-upload-r/) |
```

No phase SUMMARY.md required (this is a /gsd-quick task, not a phase plan).
</output>
