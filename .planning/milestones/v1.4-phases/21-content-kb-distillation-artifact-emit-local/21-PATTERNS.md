# Phase 21: Content KB Distillation + Artifact Emit (local) - Pattern Map

**Mapped:** 2026-05-27
**Files analyzed:** 13 (5 NEW Core, 1 NEW schema-consts, 1 NEW writer, 2 EDIT Core stores + interfaces, 2 EDIT CLI, 4 NEW tests)
**Analogs found:** 13 / 13 (every new/changed file has an in-repo analog — this is a greenfield slice over a fully-built Phase 19/20 foundation)

> **Read order for the executor:** every analog excerpt below is load-bearing. Copy the *structure* (ctor seam, Allman braces, `ConfigureAwait(false)`, `IReadOnlyList<T>` surface, `// Why:` comments, `CREATE TABLE IF NOT EXISTS` dual-dialect SQL) exactly. AI-SDK call shapes (`ChatClient`, strict `json_schema`, `sealed record` result types) are NOT re-derived here — they are fully specified in `21-AI-SPEC.md` §3 and §4. Reference those verbatim.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Integration/LlmDistillationService.cs` (NEW) | service (pure OpenAI adapter) | request-response (LLM) | `DeckFlow.Core/Integration/WhisperTranscriptionService.cs` | exact |
| `DeckFlow.Core/Integration/ILlmDistillationService.cs` (NEW) | interface | request-response | `DeckFlow.Core/Integration/IWhisperTranscriptionService.cs` | exact |
| `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (NEW) | config/consts | transform | `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (const-string pattern) | role-match |
| `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` (NEW) | utility (file emit) | file-I/O | `DeckFlow.Core/Exporting/DeltaExporter.cs` | exact |
| `DeckFlow.Core/Content/LlmSpendLedger.cs` (NEW) | store (ledger) | CRUD | `DeckFlow.Core/Content/WhisperSpendLedger.cs` | exact |
| `DeckFlow.Core/Content/ILlmSpendLedger.cs` (NEW) | interface | CRUD | `DeckFlow.Core/Content/IWhisperSpendLedger.cs` | exact |
| `DeckFlow.Core/Content/ContentVideoStore.cs` (EDIT) | store | CRUD/query | self (existing query + count helpers) | exact (additive) |
| `DeckFlow.Core/Content/IContentVideoStore.cs` (EDIT) | interface | CRUD/query | self | exact (additive) |
| `DeckFlow.Core/Content/ContentSourceStore.cs` (EDIT) | store | CRUD | self (`ListEnabledSourcesAsync` bool-adapt) | exact (additive) |
| `DeckFlow.Core/Content/IContentSourceStore.cs` (EDIT) | interface | CRUD | self | exact (additive) |
| `DeckFlow.CLI/CommandRunners.cs` (EDIT) | orchestrator (CLI runner) | batch + event-driven | `RunHarvestAsync` two-layer (lines 446-534) + `HarvestVideoAsync` (558-593) | exact |
| `DeckFlow.CLI/Program.cs` (EDIT) | composition root (verb registration) | n/a | `harvest` / `content-source-add` registration (lines 64-67, 102-104, 152, 200-208) | exact |
| `DeckFlow.Core.Tests/*Tests.cs` (NEW x4) | test | n/a | existing store/runner tests (inject-delegate / inject-fakes seam) | role-match |

**Wire-in only (no new file, call existing store methods from the orchestrator):**
`ContentSiteIndexStore.UpsertRowAsync` + `.GetByNaturalKeyAsync` (analog `ContentSiteIndexStore.cs:65,98`), `ContentHarvestRunStore.StartRunAsync`/`CompleteRunAsync` (analog `IContentHarvestRunStore.cs:21,34`), `ContentVideoStore.InsertSummary/Clip/TagAsync` (analog `ContentVideoStore.cs:178,194,217`).

---

## Pattern Assignments

### `DeckFlow.Core/Integration/LlmDistillationService.cs` (NEW) — service, request-response

**Analog:** `DeckFlow.Core/Integration/WhisperTranscriptionService.cs`

This is the single most important analog. Copy the construction seam, the dual ctor (public DI + `internal` test-seam injecting a delegate), the `OpenAIClientOptions` shape, and the `ReadApiKey` env-var pattern exactly. Swap `AudioClient`→`ChatClient` and Whisper-specific result types for the AI-SPEC §4 `SummaryResult`/`ClipsResult`/`TagsResult` records.

**Imports + class shape** (`WhisperTranscriptionService.cs:1-13`):
```csharp
using System.ClientModel;             // ApiKeyCredential
using System.ClientModel.Primitives;  // HttpClientPipelineTransport, ClientRetryPolicy
using DeckFlow.Core.Content;
using OpenAI;                         // OpenAIClientOptions
using OpenAI.Audio;                   // -> SWAP to OpenAI.Chat for Phase 21
using Polly;

namespace DeckFlow.Core.Integration;

public sealed class WhisperTranscriptionService : IWhisperTranscriptionService
```

**Dual-ctor test seam** (`WhisperTranscriptionService.cs:30-51`) — replicate this exactly: public ctor delegates to `internal` ctor; production transcriber is `CreateProductionTranscriber(httpClient)` when the override is null. For Phase 21 the injected delegate type is the canonical seam for E1-E4 unit tests (feed canned good/refusal/truncated/garbage `ChatCompletion`):
```csharp
public WhisperTranscriptionService(IWhisperSpendLedger ledger, IFfmpegAudioChunker chunker, HttpClient httpClient)
    : this(ledger, chunker, httpClient, transcribeAsyncOverride: null) { }

internal WhisperTranscriptionService(
    IWhisperSpendLedger ledger, IFfmpegAudioChunker chunker, HttpClient httpClient,
    Func<Stream, string, CancellationToken, Task<(string Body, int BilledSeconds)>>? transcribeAsyncOverride)
{
    ArgumentNullException.ThrowIfNull(ledger);
    // ...
    _transcribeAsync = transcribeAsyncOverride ?? CreateProductionTranscriber(httpClient);
}
```
> Phase 21 service stays PURE — NO ledger/store args in the ctor (Phase 20 D-11: services pure, orchestrator owns persistence). The only injected dependency is the `HttpClient` + (test) the completion delegate. The ledger lives in the orchestrator, not here.

**OpenAI client construction — copy verbatim, swap the client type** (`WhisperTranscriptionService.cs:179-189`):
```csharp
private static AudioClient CreateAudioClient(HttpClient httpClient, string apiKey)
{
    var options = new OpenAIClientOptions
    {
        Transport = new HttpClientPipelineTransport(httpClient),
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),   // Polly owns retry, not the SDK (D-12)
        NetworkTimeout = Timeout.InfiniteTimeSpan,            // CancellationToken governs timeout
    };
    return new AudioClient("whisper-1", new ApiKeyCredential(apiKey), options);
}
```
Phase 21: `return new ChatClient("gpt-4o-mini", new ApiKeyCredential(apiKey), options);` (AI-SPEC §3 `CreateChatClient`).

**API key env-var guard — copy verbatim** (`WhisperTranscriptionService.cs:202-211`); reuse the SAME `OPENAI_API_KEY` constant:
```csharp
private const string OpenAiApiKeyEnvironmentKey = "OPENAI_API_KEY";
private static string ReadApiKey()
{
    var apiKey = Environment.GetEnvironmentVariable(OpenAiApiKeyEnvironmentKey);
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException($"{OpenAiApiKeyEnvironmentKey} is not set.");
    return apiKey;
}
```

**Result-record pattern** (`WhisperTranscriptionService.cs:258`, `sealed record` for multi-value results) — Phase 21 result records (`SummaryResult`, `ClipItem`/`ClipsResult`, `TagsResult`) are specified in AI-SPEC §4. Keep them `public sealed record` with `IReadOnlyList<T>` collections (CLAUDE.md surface convention).

**Polly pipeline** — `WhisperTranscriptionService.cs:22,50,155` wraps the call in `_pipeline.ExecuteAsync(...)`. AI-SPEC §4 / D-12 says: a Polly pipeline is fine but must NOT convert a transient failure into a recorded success. Mirror the `WhisperResiliencePipeline.Build()` pattern for a `DistillationResiliencePipeline` only if needed; do not add masking retry.

---

### `DeckFlow.Core/Content/LlmSpendLedger.cs` + `ILlmSpendLedger.cs` (NEW) — store, CRUD

**Analog:** `DeckFlow.Core/Content/WhisperSpendLedger.cs` + `IWhisperSpendLedger.cs`

Mirror, do NOT generalize (D-05 says build a separate parallel ledger). Swap `seconds_billed` → `input_tokens`/`output_tokens`, table name `whisper_spend_ledger` → `llm_spend_ledger`, cap key → `DECKFLOW_LLM_MONTHLY_CAP_USD`.

**Dual ctor + SQLite dir creation** (`WhisperSpendLedger.cs:25-46`) — copy exactly:
```csharp
public WhisperSpendLedger(string databasePath, Func<string, string?>? configurationValueResolver = null)
    : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), configurationValueResolver) { }

public WhisperSpendLedger(RelationalDatabaseConnection connectionInfo, Func<string, string?>? configurationValueResolver = null)
{
    ArgumentNullException.ThrowIfNull(connectionInfo);
    _connectionInfo = connectionInfo;
    _configurationValueResolver = configurationValueResolver;
    if (_connectionInfo.IsSqlite) { /* Directory.CreateDirectory(dir of ExtractSqlitePath) */ }
}
```

**FK-parent schema ordering — CRITICAL, copy the `// Why:` comment** (`WhisperSpendLedger.cs:60-68`):
```csharp
// Why: REVIEW #1 requires content_videos to exist before the spend ledger
// declares its FK parent, and Postgres rejects FKs to missing parent tables.
var videoStore = new ContentVideoStore(_connectionInfo);
await videoStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
// ... then CREATE TABLE IF NOT EXISTS llm_spend_ledger
```

**Method signatures to mirror** (`IWhisperSpendLedger.cs:16-42`) — adapt arg types for tokens:
```csharp
Task RecordCallAsync(long videoId, int inputTokens, int outputTokens, decimal costUsd, string monthKey, CancellationToken ct = default);
Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken ct = default);
Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken ct = default);
```

**Cap resolver** (`WhisperSpendLedger.cs:12-13,166-182`) — copy `ReadMonthlyCapUsd`; change the const key and default:
```csharp
private const string MonthlyCapConfigurationKey = "DECKFLOW_LLM_MONTHLY_CAP_USD"; // [ASSUMED name+default — confirm with user, A2]
private static readonly decimal DefaultMonthlyCapUsd = 5.00m;                     // ~$1.10 for 200 videos (AI-SPEC §4)
```

**Cost constants** — store gpt-4o-mini prices as `decimal` consts (mirror `WhisperUsdPerMinute = 0.006m` at line 16). AI-SPEC §4: ≈ $0.15/1M input, $0.60/1M output. Tag `[ASSUMED]` (A1 — verify before locking). Compute EXACT per-call cost from `completion.Usage.InputTokenCount`/`OutputTokenCount` AFTER the call (do not estimate at ledger time).

**Dual-dialect DDL** (`WhisperSpendLedger.cs:197-219`) — copy both `PostgresCreateTableSql` and `SqliteCreateTableSql` raw-string blocks, swapping `seconds_billed INT` → `input_tokens INT NOT NULL, output_tokens INT NOT NULL`, table name, and index name `ix_spend_month` → `ix_llm_spend_month`. Keep `cost_usd DECIMAL(10,6)` (Postgres) / `TEXT` (SQLite) + `FormatDecimal` invariant-culture handling (`WhisperSpendLedger.cs:158-159`).

---

### `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` (NEW) — utility, file-I/O

**Analog:** `DeckFlow.Core/Exporting/DeltaExporter.cs` (static `ToText` + `WriteFile` pair)

**Static pure pair** (`DeltaExporter.cs:9-19`):
```csharp
public static class DeltaExporter
{
    public static void WriteFile(List<DeckEntry> toAdd, string outputPath, string targetSystem)
    {
        ArgumentNullException.ThrowIfNull(toAdd);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        File.WriteAllText(outputPath, ToText(toAdd, targetSystem));   // build-then-write
    }
    public static string ToText(List<DeckEntry> toAdd, string targetSystem) { /* StringBuilder */ }
}
```

**Phase 21 shape (Q7 recommendation):**
- `static string ToText(ContentArtifactMetadata metadata, string summary, IReadOnlyList<(int? TimestampSeconds, string Excerpt)> clips)` — builds the LOCKED `ContentArtifactSpec.ArtifactFileFormat` layout: YAML frontmatter + `## Summary` + `## Key Clips` + `## Tags`. Use `ContentArtifactSpec.SerializeTags` and the `ContentArtifactMetadata` tag lists for the frontmatter arrays. Do NOT invent a layout (D-07).
- `static string WriteFile(string artifactRoot, string sourceSlug, string videoId, string text)` — `Directory.CreateDirectory(parent)` then `File.WriteAllText`. The CLI caller already does `Directory.CreateDirectory(output.DirectoryName)` before write (`CommandRunners.cs` RunExportMoxfieldAsync ~line 270) — keep that idiom.
- **Null-timestamp rendering (Q1/D-08):** when `TimestampSeconds is null`, OMIT the `[mm:ss]` prefix → render `- [excerpt]`. Never emit a confidently-wrong timestamp. This is the E4 test target.

**Locked artifact format — emit EXACTLY this** (`ContentArtifactSpec.cs:13-41`): YAML frontmatter keys `source/title/url/video_id/tags{archetype,bracket,card_category}/generated_utc`, then `## Summary`, `## Key Clips` (bulleted `- **[mm:ss]** excerpt`), `## Tags` (three bold lines). The `ContentArtifactMetadata` record (`ContentArtifactSpec.cs:74-102`) carries the frontmatter inputs.

**Relative-path safety:** compute the relative path `content-kb/{source-slug}/{video_id}.md` separately for the slim-index row's `ArtifactPath` — it MUST pass `ContentSiteIndexStore.ValidateArtifactPath` (`ContentSiteIndexStore.cs:169-187`, rejects rooted + `..`). Sanitize slug/video_id before path use even though the store guards.

---

### `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (NEW) — config/consts, transform

**Analog:** `ContentArtifactSpec.cs:13` (`public const string ... = """ ... """;` raw-string-literal const pattern in `DeckFlow.Core/Knowledge`).

Hold the three strict `json_schema` documents as `const string` (or `internal const string`) raw-string literals, exposed as `BinaryData` via `BinaryData.FromString(...)` at call time. Exact schema text is in AI-SPEC §4 (`SummarySchema`, `ClipsSchema`, `TagsSchema`). PITFALLS in AI-SPEC §3: every object needs `additionalProperties:false`, every property in `required`, nullable modeled as `["integer","null"]` + required. Do NOT re-indent raw-string literals (CLAUDE.md — re-indent changes the shipped value).

---

### `DeckFlow.Core/Content/ContentVideoStore.cs` (+ `IContentVideoStore.cs`) (EDIT) — store, query

**Analog:** self. Three additive methods (RESEARCH Q5/Q1). Mirror existing method shape: `ArgumentException.ThrowIfNullOrWhiteSpace` guards, `EnsureSchemaAsync` first, `await using` connection/command, `RelationalDatabaseConnection.AddParameter`, `ReadVideo(reader)` mapper, `ConfigureAwait(false)`.

1. **`ListVideosPendingDistillAsync(CancellationToken)`** → `IReadOnlyList<ContentVideo>`. Query videos whose `transcript_status IN ('captions','whisper')` (terminal-success per `CommandRunners.cs:709 IsTerminalSuccess`) AND have ≥1 `content_transcripts` row. Mirror `GetVideoByYoutubeIdAsync` (`ContentVideoStore.cs:110-131`) reader loop + the `List<ContentSource>` accumulation pattern in `ContentSourceStore.ListEnabledSourcesAsync` (`ContentSourceStore.cs:131-138`). The "not yet distilled" filter is applied by the ORCHESTRATOR via the derived check (artifact file + index row), NOT in SQL.

2. **`GetLatestTranscriptAsync(long videoId)`** → transcript body (+ source). GAP: only `InsertTranscriptAsync` + `CountTranscriptsByVideoAsync` exist today. Add a `SELECT body, source FROM content_transcripts WHERE video_id=@videoId ORDER BY id DESC LIMIT 1` reader following the `GetVideoByYoutubeIdAsync` single-row pattern.

3. **`ClearDistillOutputAsync(long videoId)`** → deletes summary/clip/tag rows for clean re-distill (Q2 ordering invariant — avoids duplicate child rows + `content_tags` UNIQUE violation on re-run). Mirror `DeleteVideoAsync` (`ContentVideoStore.cs:239-251`) — three `DELETE FROM content_{summaries,clips,tags} WHERE video_id=@videoId` statements.

Add the matching XML-doc'd signatures to `IContentVideoStore.cs` (existing doc style at lines 38-48). Update the `DeckFlow.Core.Tests` fake `IContentVideoStore` implementation (RESEARCH Wave 0 gap).

**Clip insert with null-timestamp sentinel (Q1/A5):** `content_clips.timestamp_s` is `INT NOT NULL`. When the LLM `ClipItem.TimestampSeconds` is null, the orchestrator stores sentinel `0` and relies on the existing `sortOrder` arg of `InsertClipAsync` (`ContentVideoStore.cs:194-214`) for ordering. Document the `0`-sentinel with a `// Why:` comment at the call site.

---

### `DeckFlow.Core/Content/ContentSourceStore.cs` (+ `IContentSourceStore.cs`) (EDIT) — store, CRUD

**Analog:** self — `ListEnabledSourcesAsync` (`ContentSourceStore.cs:114-139`) for the boolean dialect-adaptation idiom.

**`SetEnabledAsync(long id, bool isEnabled, CancellationToken)`** (D-13/KB-01). Plain `UPDATE content_sources SET is_enabled = @isEnabled WHERE id = @id;`. Reuse the EXACT bool-adapt pattern (`ContentSourceStore.cs:126-129`):
```csharp
RelationalDatabaseConnection.AddParameter(
    command, "@isEnabled",
    _connectionInfo.IsPostgres ? (object)true : 1);   // SQLite stores INTEGER 1/0
```
Soft-disable touches no child data (D-13 keeps prior harvested data — no cascade). Add the XML-doc'd signature to the interface. Optional `GetSourceBySlugAsync` (disabled sources won't appear in `ListEnabledSourcesAsync`) only if the verb accepts `--slug`; MVP can take `--id` to skip the lookup (A3).

---

### `DeckFlow.CLI/CommandRunners.cs` (EDIT) — orchestrator, batch + event-driven

**Analog:** `RunHarvestAsync` two-layer (`CommandRunners.cs:446-534`) + `HarvestVideoAsync` per-video isolation (`CommandRunners.cs:558-593`). The new `RunDistillAsync` is a SIBLING of `RunHarvestAsync` — do NOT edit harvest (D-09; the marker at line 532 means "do it elsewhere").

**Two-layer runner — public concrete builds dependencies, internal interface overload for test fakes** (`CommandRunners.cs:446-491` public → `493-534` internal):
```csharp
public static async Task<int> RunDistillAsync(FileInfo? db, int limit, Serilog.ILogger logger, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(logger);
    try
    {
        var dbPath = ResolveContentKbDatabasePath(db);                       // CommandRunners.cs:963
        var artifactRoot = ResolveContentKbArtifactRoot(db);                 // NEW helper (Q7), mirror line 963
        using var llmHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) }; // mirror whisperHttpClient:458
        var sourceStore = new ContentSourceStore(dbPath);
        var videoStore  = new ContentVideoStore(dbPath);
        var indexStore  = new ContentSiteIndexStore(dbPath);
        var runStore    = new ContentHarvestRunStore(dbPath);
        var ledger      = new LlmSpendLedger(dbPath);                        // NEW
        var distiller   = new LlmDistillationService(llmHttpClient);        // NEW (pure)
        return await RunDistillAsync(sourceStore, videoStore, indexStore, runStore, ledger,
            distiller, artifactRoot, limit, logger, () => DateTimeOffset.UtcNow, ct);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.Error(exception, "Content KB distill failed.");
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

internal static async Task<int> RunDistillAsync(
    IContentSourceStore sourceStore, IContentVideoStore videoStore, IContentSiteIndexStore indexStore,
    IContentHarvestRunStore runStore, ILlmSpendLedger ledger, ILlmDistillationService distiller,
    string artifactRoot, int limit, Serilog.ILogger logger, Func<DateTimeOffset> utcNow, CancellationToken ct)
{ /* StartRunAsync -> loop -> CompleteRunAsync */ }
```

**Run-record wiring (D-11/Q4):** `var runId = await runStore.StartRunAsync(ct);` at top; `await runStore.CompleteRunAsync(runId, sourcesProcessed, videosDistilled, transcriptsFetched:0, whisperCalls: llmCalls, spendUsd: llmSpend, abortedReason, ct);` at end — in a `finally`/catch so a crashed run still records. Document the column overload with a `// Why:` comment: on a distill run `whisper_calls` counts LLM calls and `spend_usd` is LLM spend (interface at `IContentHarvestRunStore.cs:34-42`). Surface distill-failed count in the Serilog completion log (mirror `LogFallbackRatio` `CommandRunners.cs:701-707`), not a column.

**Per-video failure isolation (D-12)** — copy `HarvestVideoAsync` (`CommandRunners.cs:558-593`):
```csharp
try { /* cap-check -> 3 LLM calls -> validate -> ClearDistillOutput -> Insert child rows -> WriteFile -> UpsertRow */ }
catch (Exception exception) when (exception is not OperationCanceledException)
{
    logger.Error(exception, "distill failed {VideoId}", video.YoutubeVideoId);   // mark-failed + continue, NO retry
}
```

**Skip-completed / idempotent resume (D-10/Q2)** — at the START of each video, mirror the `ResolveHarvestVideoIdAsync` already-harvested early-return (`CommandRunners.cs:602-619`): if the artifact file exists AND `indexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoId)` returns non-null → log "already distilled" and skip (never call the LLM).

**Cap-check ordering** — `ledger.WouldExceedCapAsync(projectedCost, monthKey, ct)` BEFORE the paid calls; over-cap → skip + record `aborted_reason`, stop further videos (hard stop). Mirror `WhisperTranscriptionService.cs:71-74` ordering and `PersistTranscriptResultAsync` "record spend first" comment (`CommandRunners.cs:648-650`).

**New helper `ResolveContentKbArtifactRoot(FileInfo? db)`** — mirror `ResolveContentKbDatabasePath` (`CommandRunners.cs:963-964`): `MTG_DATA_DIR` env if set → `Path.Combine(MTG_DATA_DIR, "content-kb")`; else `Path.Combine(<db-dir>, "content-kb")` (artifacts beside the db, D-06).

**`RunContentSourceSetEnabledAsync(long id, bool enabled, FileInfo? db)`** — mirror `RunContentSourceAddAsync` (`CommandRunners.cs:416-444`): resolve db path, `new ContentSourceStore(dbPath)`, call `SetEnabledAsync`, Console.WriteLine result, `catch (Exception) when (not OperationCanceledException)` → write message + return 1.

---

### `DeckFlow.CLI/Program.cs` (EDIT) — composition root, verb registration

**Analog:** `harvest` + `content-source-add` registration (`Program.cs:59-67, 98-104, 151-152, 200-208`).

**Declare command + options** (mirror `Program.cs:64-67`):
```csharp
var distillCommand = new Command("distill", "Distill harvested transcripts into Content KB artifacts.");
var distillDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var distillLimitOption = new Option<int>("--limit", () => 5) { Description = "Videos to distill per run." };
```
`distillCommand.AddOption(...)` for each (mirror `:102-104`); `rootCommand.AddCommand(distillCommand);` (mirror `:152`).

**Handler — `GetAwaiter().GetResult()` ONLY at the SetHandler boundary** (mirror `Program.cs:205-208`); keep everything below `async`/`await` (AI-SPEC §4b — `.Result` deeper deadlocks + swallows the exception type D-12 needs):
```csharp
distillCommand.SetHandler((FileInfo? db, int limit) =>
{
    Environment.ExitCode = CommandRunners.RunDistillAsync(db, limit, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, distillDbOption, distillLimitOption);
```

**`content-source-set-enabled` verb** — mirror `content-source-add` (`Program.cs:59-63, 98-101, 151, 200-203`) with `--id` (or `--slug`) + `--enabled <bool>` + `--db` options dispatching to `RunContentSourceSetEnabledAsync`.

---

### `DeckFlow.Core.Tests/*Tests.cs` (NEW x4) — test

**Analogs:** existing `WhisperTranscriptionService`/`WhisperSpendLedger`/store/runner tests using the inject-delegate (Core service) and inject-fakes (`internal RunHarvestAsync` overload) seams. xUnit 2.9.3 already referenced — zero new deps.

- `LlmDistillationServiceTests.cs` — inject the completion delegate via the `internal` test ctor; feed canned good/refusal/`FinishReason.Length`/garbage payloads; assert deserialize-or-mark-failed (E1). Mirror the `transcribeAsyncOverride` seam (`WhisperTranscriptionService.cs:38-49`).
- `LlmSpendLedgerTests.cs` — record/total/cap against a temp SQLite path (D-05). Mirror existing `WhisperSpendLedger` tests; use `configurationValueResolver` to set the cap deterministically (`WhisperSpendLedger.cs:25`).
- `ContentArtifactWriterTests.cs` — `ToText` format compliance (D-07), summary ≤200 words (E3), clip count 3-8 + null-timestamp omission (E4/Q1).
- `RunDistillAsyncTests.cs` — inject fakes via the `internal RunDistillAsync` overload (mirror `RunHarvestAsync` test pattern): out-of-vocab tag dropped + WARN (E2), skip-already-distilled (D-10), per-video failure isolation/batch-continues (D-12).
- Update the `DeckFlow.Core.Tests` fake `IContentVideoStore`/`IContentSourceStore` with the new method stubs (Wave 0 gap).

---

## Shared Patterns

### OpenAI client construction (cross-cutting for the LLM service)
**Source:** `WhisperTranscriptionService.cs:179-189` (`CreateAudioClient`) + `:202-211` (`ReadApiKey`)
**Apply to:** `LlmDistillationService` only.
`OpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient), RetryPolicy = new ClientRetryPolicy(maxRetries: 0), NetworkTimeout = Timeout.InfiniteTimeSpan }`; reuse `OPENAI_API_KEY` env var; throw `InvalidOperationException` when unset.

### Dual-dialect SQLite/Postgres store skeleton
**Source:** `WhisperSpendLedger.cs:33-76,155-219` and `ContentSourceStore.cs:28-62,141-214`
**Apply to:** `LlmSpendLedger` (new) + the new query methods on `ContentVideoStore`/`ContentSourceStore`.
Pattern: ctor takes `string databasePath` → `RelationalDatabaseConnection.FromSqlitePath`; `_schemaGate` + `_schemaReady` double-checked `EnsureSchemaAsync`; `_connectionInfo.IsPostgres ? PostgresSql : SqliteSql`; `RelationalDatabaseConnection.AddParameter`; `_connectionInfo.IsPostgres ? (object)true : 1` for booleans; invariant-culture `FormatDecimal`/`FormatTimestamp`; `await using` + `ConfigureAwait(false)` throughout.

### Spend-cap ordering (cap-check before paid call)
**Source:** `WhisperTranscriptionService.cs:70-76` + `CommandRunners.cs:648-650` ("record spend first")
**Apply to:** the distill orchestrator's per-video loop.
`WouldExceedCapAsync` BEFORE the LLM calls; `RecordCallAsync` AFTER with exact `completion.Usage` cost; over-cap → skip + `aborted_reason` (D-05/SC5, Phase 20 CR-01 ordering).

### Two-layer CLI runner + per-item failure isolation
**Source:** `CommandRunners.cs:446-534` (public concrete + internal interface overload) + `:558-593` (`HarvestVideoAsync` try/catch-continue)
**Apply to:** `RunDistillAsync`.
Public method news up concretes and delegates to an `internal` overload taking interfaces (test-fake seam). Per-item `catch (Exception) when (exception is not OperationCanceledException)` → log + continue; NO retry that masks failure.

### File emit (build-then-write)
**Source:** `DeltaExporter.cs:9-19`
**Apply to:** `ContentArtifactWriter`.
Static `ToText(...)` pure builder + `WriteFile(...)` that arg-guards then `Directory.CreateDirectory(parent)` + `File.WriteAllText`.

### Locked Phase 19 contracts (consume, never redesign)
**Source:** `ContentArtifactSpec.cs:13-68` (format + `SerializeTags`), `ContentTagVocabulary.cs:63-72` (`IsValid`), `ContentSiteIndexStore.cs:169-187` (`ValidateArtifactPath`)
**Apply to:** writer (format), orchestrator (tag filter — drop out-of-vocab with WARN per D-04), index-row upsert (relative path).

---

## No Analog Found

None. Every file has a direct in-repo analog. The only genuinely novel surface is the OpenAI `ChatClient` + strict `json_schema` *call shape*, which is intentionally NOT a codebase analog — it is fully specified in `21-AI-SPEC.md` §3 (Entry Point Pattern) and §4 (Core Pattern). Reference those verbatim; do not re-derive.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Integration/`, `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Exporting/`, `DeckFlow.CLI/`
**Files scanned (read in full or targeted):** WhisperTranscriptionService.cs, WhisperSpendLedger.cs, IWhisperSpendLedger.cs, ContentArtifactSpec.cs, ContentTagVocabulary.cs, DeltaExporter.cs, ContentSourceStore.cs, IContentSourceStore.cs, IContentVideoStore.cs, ContentVideoStore.cs (targeted), IContentHarvestRunStore.cs, ContentSiteIndexStore.cs (targeted), CommandRunners.cs (targeted), Program.cs (targeted), ContentModels.cs (targeted)
**Pattern extraction date:** 2026-05-27
**Cross-references:** 21-CONTEXT.md (D-01..D-13), 21-AI-SPEC.md (§3/§4 SDK depth), 21-RESEARCH.md (Q1-Q7 + Assumptions A1-A5)
