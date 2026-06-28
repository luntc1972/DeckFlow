---
phase: 260627-qyc
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Content/IContentSiteIndexStore.cs
  - DeckFlow.Core/Content/ContentSiteIndexStore.cs
  - DeckFlow.Core/Content/ContentSiteIndexBatchUpsertException.cs
  - DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs
  - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs
  - DeckFlow.Studio/Pages/DirectPush.razor
  - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
  - DeckFlow.Studio.Tests/DirectPushPageTests.cs
autonomous: true
requirements: [H4, M2]

must_haves:
  truths:
    - "A mid-batch prod content upsert failure leaves PROD with zero rows from that batch written (all-or-nothing)."
    - "On full success, every approved publish-set row is content-upserted to PROD in one transaction, then stamped + published visible."
    - "DirectPush calls the single transactional batch method, never the per-row upsert loop."
    - "ComputeDiffAsync classifies an already-present row as Unchanged when its content columns match prod, and excludes it from the publish set (no SCP, no DB write)."
    - "ComputeDiffAsync classifies a changed row as Updated and a missing row as New, and includes both in the publish set."
    - "The UI shows accurate New / Updated / Unchanged counts."
    - "On rollback, the UI states nothing was written and names the row that aborted, without leaking any secret from the underlying DB exception."
    - "DeckFlow.Core, DeckFlow.Studio, DeckFlow.Core.Tests, and DeckFlow.Studio.Tests all compile."
  artifacts:
    - path: "DeckFlow.Core/Content/ContentSiteIndexStore.cs"
      provides: "Transactional UpsertContentColumnsOnlyBatchAsync (one connection, one DbTransaction, content-columns-only SQL, commit-at-end, rollback-on-any-failure)"
      contains: "UpsertContentColumnsOnlyBatchAsync"
    - path: "DeckFlow.Core/Content/IContentSiteIndexStore.cs"
      provides: "Batch upsert contract with default-throwing implementation (DeleteAllRowsAsync precedent)"
      contains: "UpsertContentColumnsOnlyBatchAsync"
    - path: "DeckFlow.Core/Content/ContentSiteIndexBatchUpsertException.cs"
      provides: "Typed batch-abort exception carrying the failing row's title + natural key (non-secret) with the DB exception as InnerException"
      contains: "class ContentSiteIndexBatchUpsertException"
    - path: "DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs"
      provides: "Stable content signature over the exact UpsertContentColumnsOnly column set for cross-dialect equality"
      contains: "BuildSignature"
    - path: "DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs"
      provides: "SQLite integration tests for batch commit + rollback and signature comparer unit tests"
      contains: "UpsertContentColumnsOnlyBatchAsync"
    - path: "DeckFlow.Studio/Pages/DirectPush.razor"
      provides: "Content-aware diff (New/Updated/Unchanged) + single atomic batch write call"
      contains: "UpsertContentColumnsOnlyBatchAsync"
    - path: "DeckFlow.Studio.Tests/DirectPushPageTests.cs"
      provides: "bUnit tests for content-aware diff classification + atomic batch commit/rollback UX"
      contains: "Unchanged"
  key_links:
    - from: "DeckFlow.Studio/Pages/DirectPush.razor"
      to: "ContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync"
      via: "prodStore batch call in WriteRowsAsync"
      pattern: "UpsertContentColumnsOnlyBatchAsync"
    - from: "DeckFlow.Studio/Pages/DirectPush.razor"
      to: "ContentSiteIndexContentSignature.BuildSignature"
      via: "ComputeDiffAsync content comparison"
      pattern: "ContentSiteIndexContentSignature"
    - from: "DeckFlow.Core/Content/ContentSiteIndexStore.cs"
      to: "UpsertContentColumnsOnlySql"
      via: "batch loop reuses the existing content-columns-only SQL inside one transaction"
      pattern: "UpsertContentColumnsOnlySql"
---

<objective>
Two coupled prod-write-integrity fixes to DeckFlow.Studio's DirectPush path, from the
Codex-reviewed Studio assessment.

- **H4 — Transactional batch prod upsert.** Today DirectPush upserts each approved row via a
  separate `UpsertContentColumnsOnlyAsync` call (each opens its own connection and autocommits);
  a mid-batch failure leaves PROD partially written. Add a batch method that opens ONE
  connection, begins ONE `DbTransaction`, upserts ALL rows with the existing
  content-columns-only SQL, and commits at the end — rolling back on any row failure so the prod
  write is all-or-nothing on BOTH dialects.
- **M2 — Content-aware diff.** Today `ComputeDiffAsync` only checks natural-key presence, so every
  existing key is labeled "Updated" and re-upserted. Compare the actual content columns and
  classify rows New / Updated / Unchanged; exclude Unchanged rows from the publish set (skip
  no-op upserts) and surface accurate counts.

Purpose: protect the live deckflow.gg database from partial/inconsistent state and stop
re-writing rows that did not change.
Output: a transactional Core batch method + a stable content-signature comparer, wired into
DirectPush, with full test coverage.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md
@scratchpad-research/studio-improvement-best-practice-report.md

@DeckFlow.Core/Content/IContentSiteIndexStore.cs
@DeckFlow.Core/Content/ContentSiteIndexStore.cs
@DeckFlow.Core/Storage/RelationalDatabaseConnection.cs
@DeckFlow.Core/Orchestration/ContentIndexExportRow.cs
@DeckFlow.Studio/Pages/DirectPush.razor
@DeckFlow.Studio/Services/IProdStoreFactory.cs
@DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
@DeckFlow.Studio.Tests/DirectPushPageTests.cs
@DeckFlow.Core.Tests/Content/ContentSiteIndexStorePushedToProdTests.cs

<interfaces>
<!-- Key contracts the executor needs; extracted from the codebase. Use directly — no exploration. -->

ContentSiteIndexRow (DeckFlow.Core/Knowledge/ContentArtifactSpec.cs, `sealed record`):
  Id (long, required), Source, Title, VideoUrl, ArtifactPath (all required string),
  PublishedUtc (DateTimeOffset?), PushedToProdUtc (DateTimeOffset?), IndexedUtc (DateTimeOffset, required),
  IsVisible/IsHidden/IsEvergreen (bool), ApprovalStatus (string = "pending"),
  ArchetypeTags/BracketTags/CardCategoryTags (IReadOnlyList<string>, required),
  YoutubeVideoId (string?), RssGuid (string?), PinId => YoutubeVideoId ?? RssGuid.
  Carve-out: all members are `{ get; init; }` — DO NOT convert to get-only.

ContentSourceType (DeckFlow.Core/Knowledge/ContentModels.cs):
  const Youtube = "youtube_channel"; const Podcast = "podcast_rss".

ContentIndexExportRow.From(row) -> { NaturalKeyType, NaturalKeyValue, ... } (Orchestration namespace) —
  the canonical (Type, Value) natural-key projection used by Stamp/SetVisibility batch calls.

ContentArtifactSpec.SerializeTags(IReadOnlyList<string>) -> string — the EXACT serializer the
  upsert uses for archetype/bracket/card-category tags. Reuse it for signature stability.

RelationalDatabaseConnection: OpenConnectionAsync(ct) -> DbConnection (Dapper-compatible),
  IsSqlite / IsPostgres, Dialect. Transactions via DbConnection.BeginTransactionAsync(ct);
  pass `transaction:` into Dapper CommandDefinition (see existing StampPushedToProdAsync,
  ContentSiteIndexStore.cs:592-625, for the exact one-connection/one-transaction template).

ContentSiteIndexStore.UpsertContentColumnsOnlySql (private const) — the content-columns-only
  INSERT ... ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE that EXCLUDES
  is_visible / is_hidden / is_evergreen / approval_status. The batch MUST reuse this exact SQL.

Existing implementers of IContentSiteIndexStore (blast radius):
  1. DeckFlow.Core/Content/ContentSiteIndexStore.cs        (real — implements batch transactionally)
  2. DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs (implements batch — exercised by DirectPush tests)
  3. DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs    (inherits default-throwing — no edit needed)
  4. DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs    (inherits default-throwing — no edit needed)
  5. DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs (inherits default-throwing — no edit needed)
</interfaces>
</context>

<side_effects_note>
**Blast radius of the interface change (per side-effects discipline):**
- `IContentSiteIndexStore` gains ONE method, `UpsertContentColumnsOnlyBatchAsync`, with a
  **default interface implementation that throws NotSupportedException** — mirroring the existing
  `DeleteAllRowsAsync` default (IContentSiteIndexStore.cs:111). This keeps the 3 unrelated
  implementers (Web.Tests fake, Core.Tests orchestration fake, Core.Tests throwing store)
  compiling unchanged; none of their consuming paths call the batch method (only DirectPush does).
- Two implementers override it: the real `ContentSiteIndexStore` (transactional) and the
  Studio.Tests `FakeContentSiteIndexStore` (in-memory atomic, for the rollback test).
- New public types `ContentSiteIndexBatchUpsertException` and `ContentSiteIndexContentSignature`
  in `DeckFlow.Core/Content` — additive only.
- DirectPush behavior change: Unchanged rows are excluded from BOTH SCP upload and DB write
  (intentional no-op skip; their artifacts were uploaded on a prior push and content is identical).
- Shared/persisted state: prod Postgres + local SQLite content_site_index table. No schema change.
  The batch reuses the existing content-columns-only SQL, so is_visible / is_hidden /
  is_evergreen / approval_status remain untouched on existing prod rows (D-08 preserved).
- Dual-dialect: the transaction wrapper is `DbConnection.BeginTransactionAsync` (dialect-agnostic);
  the Postgres `ON CONFLICT` clause is preserved verbatim by reusing `UpsertContentColumnsOnlySql`.
</side_effects_note>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Core — transactional batch upsert + stable content signature</name>
  <files>DeckFlow.Core/Content/IContentSiteIndexStore.cs, DeckFlow.Core/Content/ContentSiteIndexStore.cs, DeckFlow.Core/Content/ContentSiteIndexBatchUpsertException.cs, DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs, DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs</files>
  <behavior>
    - Batch commit: upserting [validRowA, validRowB] in one call persists BOTH rows; GetAllRowsAsync returns 2.
    - Batch rollback: upserting [validRowA, badRow] (badRow fails validation mid-loop) throws ContentSiteIndexBatchUpsertException; GetAllRowsAsync returns 0 — validRowA was rolled back (true all-or-nothing, not just "skip the bad row").
    - The thrown exception's FailedRowTitle equals badRow.Title and its InnerException is the underlying failure (so the page can name the row without re-deriving it).
    - Content-columns-only semantics preserved: pre-seed an existing row with IsVisible=true/IsEvergreen=true, batch-upsert a same-key row with changed Title; after commit the row's Title is updated but IsVisible and IsEvergreen are unchanged.
    - Empty input: batch of 0 rows is a no-op returning normally (mirrors StampPushedToProdAsync count==0 guard).
    - Signature equality: BuildSignature returns equal strings for two rows whose in-scope content columns match, and different strings when Title (or any in-scope column) differs.
    - Signature date stability: two rows differing only by sub-second time on IndexedUtc/PublishedUtc produce EQUAL signatures (normalized to UTC, truncated to whole seconds — SQLite text default has 1-second precision; Postgres has microsecond; must not false-positive across dialects).
    - Signature tag stability: rows with the same tags produce equal signatures via ContentArtifactSpec.SerializeTags (not list reference equality).
  </behavior>
  <action>
Add `Task UpsertContentColumnsOnlyBatchAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken cancellationToken = default)` to `IContentSiteIndexStore` with a **default interface implementation that throws NotSupportedException**, exactly following the `DeleteAllRowsAsync` default-method precedent at IContentSiteIndexStore.cs:111. XML-doc it: one transaction, all-or-nothing, content-columns-only (never touches is_visible / is_hidden / is_evergreen / approval_status on existing rows), throws ContentSiteIndexBatchUpsertException on any row failure after rolling back.

Implement the override in `ContentSiteIndexStore`. Model it on the existing transactional `StampPushedToProdAsync` (ContentSiteIndexStore.cs:592-625): guard `rows.Count == 0` -> return; `await EnsureSchemaAsync`; open ONE connection; `await using var transaction = await connection.BeginTransactionAsync(ct)`; loop the rows; for EACH row inside the loop validate it the same way `UpsertContentColumnsOnlyAsync` does (ArgumentNullException.ThrowIfNull + the ThrowIfNullOrWhiteSpace guards + ValidateArtifactPath + GetNaturalKey) and then run `connection.ExecuteAsync(new CommandDefinition(UpsertContentColumnsOnlySql, <same anonymous param object as UpsertContentColumnsOnlyAsync>, transaction: transaction, cancellationToken: ct))`. Why (comment): validation runs inside the transaction loop (not all up-front) so a bad row aborts AFTER prior rows wrote, proving real rollback. Wrap the loop body so that any exception (other than OperationCanceledException, which must propagate) triggers `await transaction.RollbackAsync(ct)` and is rethrown wrapped in ContentSiteIndexBatchUpsertException carrying the current row's Title and its (Type, Value) natural key, with the caught exception as InnerException. On clean completion `await transaction.CommitAsync(ct)`. Reuse the EXACT private `UpsertContentColumnsOnlySql` const — do not duplicate or alter the SQL (preserves the Postgres ON CONFLICT clause and dual-dialect behavior). Preserve the `{ get; init; }` carve-out and the raw-string-literal SQL carve-out (do not reflow).

Create `ContentSiteIndexBatchUpsertException : Exception` (public sealed) in DeckFlow.Core/Content with init properties FailedRowTitle (string), FailedKeyType (string), FailedKeyValue (string), a constructor taking those + message + innerException, and XML docs. Note in a Why comment: only the non-secret row identity is carried; the secret-bearing DB exception stays in InnerException for the sink, never the UI.

Create `ContentSiteIndexContentSignature` (public static) in DeckFlow.Core/Content with `BuildSignature(ContentSiteIndexRow row)` and `AreContentEqual(ContentSiteIndexRow a, ContentSiteIndexRow b)`. The signature MUST cover the exact column set written by UpsertContentColumnsOnlySql: source, title, video_url, artifact_path, published_utc, indexed_utc, archetype_tags, bracket_tags, card_category_tags. Serialize tags via `ContentArtifactSpec.SerializeTags` (identical to the upsert) for stability. Normalize dates to UTC truncated to whole seconds (`.UtcDateTime` then strip sub-second ticks) formatted with InvariantCulture; treat null PublishedUtc as a fixed sentinel token. Join fields with a delimiter that cannot occur in the values (e.g. ""). Document the indexed_utc inclusion (it changes only when the artifact is re-distilled, which IS a real content change worth pushing) and the 1-second truncation (cross-dialect precision). Do NOT include is_visible / is_hidden / is_evergreen / approval_status / pushed_to_prod_utc — those are not content and not written by the upsert.

Add `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs` following the per-fact SQLite-file pattern of ContentSiteIndexStorePushedToProdTests.cs (temp .db, IDisposable cleanup with ClearAllPools/GC, CreateYoutubeRow helper). Cover every bullet in <behavior>. Force the mid-batch failure deterministically by feeding a second row whose ArtifactPath is a `..` traversal or rooted path (ValidateArtifactPath rejects it). Postgres parity note in the test-class summary: rollback/transaction verified on SQLite (Postgres cannot run in WSL); the transaction wrapper is dialect-agnostic and the SQL is shared, so behavior is asserted equivalent — do NOT claim PG was executed.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -warnaserror && "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ContentSiteIndexStoreBatchUpsert"</automated>
  </verify>
  <done>Core + Core.Tests build clean (no new warnings); new batch/signature tests pass; commit success persists all rows, mid-batch failure rolls back ALL rows, signature is stable across sub-second/tag differences; is_visible/is_evergreen preserved on existing rows.</done>
</task>

<task type="auto">
  <name>Task 2: Studio — wire DirectPush to atomic batch (H4) + content-aware diff (M2)</name>
  <files>DeckFlow.Studio/Pages/DirectPush.razor</files>
  <action>
**M2 — ComputeDiffAsync (DirectPush.razor ~528-564).** Replace the presence-only `HashSet` with a `Dictionary<string, ContentSiteIndexRow>` of prod rows keyed by `YoutubeVideoId ?? RssGuid ?? string.Empty` (the same value DeriveNaturalKey yields). For each local approved row: if its key is absent -> New (include in publish set); else compare with `ContentSiteIndexContentSignature.AreContentEqual(localRow, prodRow)` — equal -> Unchanged (EXCLUDE from publish set), different -> Updated (include). Track three counts (_newCount, _updatedCount, add `_unchangedCount`) and build a `_publishRows` list = the New + Updated local rows only (preserve input order). Keep `_approvedRows = localRows` for reference but make Stage 2 (SCP) and Stage 3 (DB write) operate on `_publishRows`. Add `Unchanged` to the DiffRow record (or add an `IsUnchanged`/change-kind field) and continue to render New + Updated in the per-row table; you may render Unchanged rows greyed/labeled but they MUST NOT be in `_publishRows`. Why comment: Unchanged rows are skipped entirely (no SCP, no DB) — their artifacts were uploaded on a prior push and their content signature is identical.

**Markup (Diff Preview ~141-156).** Add an `Unchanged: @_unchangedCount` badge (e.g. `bg-secondary`) beside New/Updated. Keep the "Production is already up to date" message but gate it on `_newCount == 0 && _updatedCount == 0` (now correct because Unchanged no longer inflates Updated). Stage 2 / Stage 3 cards already gate on `_diffReady && (_newCount > 0 || _updatedCount > 0)` — leave that condition; it now correctly hides the publish stages when only Unchanged rows exist.

**Stage 2 — UploadArtifactsAsync (~619).** Change the request projection source from `_approvedRows` to `_publishRows` so only New/Updated artifacts are uploaded.

**H4 — WriteRowsAsync (~676-786).** Keep the `if (!_scpSuccess || _operationInFlight || !_diffReady) return;` hard-guard. Replace the per-row `foreach` loop that calls `prodStore.UpsertContentColumnsOnlyAsync(row)` with a SINGLE `await prodStore.UpsertContentColumnsOnlyBatchAsync(_publishRows, Cts.Token)`. On success: build `_rowResults` as one `RowResult(..., Success: true, null)` per publish row (so the per-row reconcile table still renders "Written"); then run the existing post-write stamping/visibility block (StampPushedToProdAsync + SetVisibilityAsync on prodStore then IndexStore) using keys derived from `_publishRows` (was `_approvedRows`). Catch `ContentSiteIndexBatchUpsertException` specifically BEFORE the generic catch: `Logger.LogError(ex, ...)` (logs InnerException to the sink), set `_dbError` to an all-or-nothing message naming `ex.FailedRowTitle` only — e.g. "Row '{title}' failed — the entire batch was rolled back. NOTHING was written to production. See logs." — and mark every `_rowResults` entry Failed/"Rolled back — not written". Keep `OperationCanceledException` rethrow/propagation and the existing generic `catch (Exception ex)` that logs and surfaces sanitized copy. Under NO path surface `ex.Message` / `ex.InnerException.Message` to the UI (D-07 / SC5): only `ex.FailedRowTitle` (a non-secret) and static copy. On any failure, the stamp/visibility block MUST NOT run (so local never over-reports prod — PUB-01).

Follow side-effects-check.md, the five .editorconfig carve-outs (preserve raw-string literals, switch expressions, `{ get; init; }`), LF endings, and the changed-lines format gate. No new packages.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Studio/DeckFlow.Studio.csproj -warnaserror && bash scripts/format-check-changed.sh staged 2>/dev/null || "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Studio/DeckFlow.Studio.csproj -warnaserror</automated>
  </verify>
  <done>DeckFlow.Studio builds clean; ComputeDiffAsync produces New/Updated/Unchanged with Unchanged excluded from _publishRows; WriteRowsAsync calls the single batch method and, on ContentSiteIndexBatchUpsertException, reports all-or-nothing with the failing row title and runs no stamp/visibility; SCP + DB operate on _publishRows; no secret reaches the UI.</done>
</task>

<task type="auto">
  <name>Task 3: Studio tests — atomic batch fake + DirectPush bUnit for H4 + M2</name>
  <files>DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs, DeckFlow.Studio.Tests/DirectPushPageTests.cs</files>
  <action>
**Fake (FakeContentSiteIndexStore.cs).** Implement `UpsertContentColumnsOnlyBatchAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken)`. Record the call (add a `List<IReadOnlyList<ContentSiteIndexRow>> BatchUpsertCalls` and append "UpsertContentColumnsOnlyBatchAsync" to `UpsertMethodCalls`). Implement true all-or-nothing in-memory: first scan all rows for a key in `KeysToFailOnUpsert`; if any matches, throw `ContentSiteIndexBatchUpsertException` (FailedRowTitle = that row's Title, key from YoutubeVideoId/RssGuid, InnerException = new InvalidOperationException(UpsertFailureMessage)) and add NOTHING to `Rows` (no partial state). Otherwise add all rows to `Rows`. Keep the existing per-row `UpsertContentColumnsOnlyAsync` and the full-row-upsert "forbidden" guards intact.

**bUnit tests (DirectPushPageTests.cs).** Reuse the file's existing harness (BunitContext, FakeProdStoreFactory, FakeContentSiteIndexStore, CapturingLoggerProvider, StudioConfig, the established render+click helpers). Add:
  - M2 classification: seed the prod fake with a row whose content signature matches a local approved row (Unchanged), a second prod row with a different Title for another local key (Updated), and a third local key absent from prod (New). After clicking Compute Prod Diff, assert New=1, Updated=1, Unchanged=1 and that the Unchanged badge renders. Then drive through to the DB write and assert the prod fake's BatchUpsertCalls received exactly the New+Updated rows (Unchanged excluded).
  - M2 all-unchanged: when every approved row matches prod by signature, Stage 2/3 cards do not render and the "already up to date" copy shows; no batch call occurs.
  - H4 success: New/Updated publish set written via the batch method; assert one batch call with all publish rows, all rows show "Written", and StampPushedToProdAsync + SetVisibilityAsync ran on both prod and local stores.
  - H4 rollback: set `KeysToFailOnUpsert` for one publish row so the batch throws ContentSiteIndexBatchUpsertException; assert the prod fake committed ZERO rows, the failing row's Title appears in the UI, the UI states nothing was written, the SentinelSecret substrings do NOT appear in the rendered markup (reuse the existing SentinelSubstrings check), the exception reached the CapturingLogger, and StampPushedToProdAsync / SetVisibilityAsync were NOT called.

Match xUnit + bUnit conventions already in the file; all fakes, no live SSH/Postgres.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --filter "FullyQualifiedName~DirectPush"</automated>
  </verify>
  <done>DeckFlow.Studio.Tests builds and the DirectPush tests pass: content-aware classification (New/Updated/Unchanged) with Unchanged excluded from the batch; atomic commit on success with stamp+visibility; atomic rollback on failure with zero committed rows, failing-row title surfaced, no secret leak, exception logged, and no stamp/visibility.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Studio (operator laptop) -> prod Postgres | Direct prod DB writes; the highest-value, no-undo surface |
| DB exception -> Blazor UI | Npgsql/SQLite exception text can carry host/db/user/password |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-qyc-01 | Tampering | DirectPush prod content write | mitigate | Single DbTransaction; rollback on any row failure so prod is all-or-nothing (H4). SQLite integration test asserts zero rows after mid-batch abort. |
| T-qyc-02 | Information disclosure | WriteRowsAsync / batch exception | mitigate | ContentSiteIndexBatchUpsertException carries only non-secret row identity (Title/key); DB exception kept in InnerException, logged to sink, never surfaced. bUnit SentinelSecret assertion guards the catch path. |
| T-qyc-03 | Tampering | content-columns-only semantics on existing prod rows | mitigate | Batch reuses UpsertContentColumnsOnlySql verbatim; is_visible/is_hidden/is_evergreen/approval_status untouched (D-08). Core test asserts preservation. |
| T-qyc-04 | Repudiation/Integrity | local-vs-prod publish state drift | mitigate | Stamp/visibility runs only on full batch success; on rollback local stays behind (PUB-01) so local never over-reports prod. |
| T-qyc-SC | Tampering | npm/NuGet installs | accept | No new packages added (CLAUDE.md). No package-manager install tasks in this plan. |
</threat_model>

<verification>
- Build all four affected projects clean with -warnaserror: DeckFlow.Core, DeckFlow.Studio, DeckFlow.Core.Tests, DeckFlow.Studio.Tests.
- `dotnet test DeckFlow.Core.Tests --filter ContentSiteIndexStoreBatchUpsert` passes (commit, rollback, preservation, signature).
- `dotnet test DeckFlow.Studio.Tests --filter DirectPush` passes (classification, atomic commit, atomic rollback, no-leak).
- Confirm the Postgres `ON CONFLICT (natural_key_type, natural_key_value)` clause is unchanged (the batch reuses the shared SQL const) — assert via the existing GetPrivateSql pattern if useful.
- Run the broader suite before merge per CLAUDE.md (no ship with failing tests). VSTest is unreliable in WSL — use dotnet.exe; if the full run is flaky, scope to the affected filters above and note it.
- LF endings preserved; changed-lines format gate green; the five carve-outs intact.
</verification>

<success_criteria>
- H4: a mid-batch prod upsert failure leaves PROD with zero rows from the batch (all-or-nothing), proven by a SQLite rollback test and a bUnit rollback test.
- H4: DirectPush calls the single transactional batch method; on success all rows are written then stamped + published visible; on failure nothing is written and the failing row is named without leaking secrets.
- M2: ComputeDiffAsync classifies New / Updated / Unchanged from real content comparison; Unchanged rows are excluded from SCP and DB; the UI shows accurate counts.
- Content-columns-only semantics preserved (is_visible / is_evergreen not clobbered).
- All four projects compile with no new warnings; no new NuGet packages; carve-outs and LF respected.
</success_criteria>

<output>
Create `.planning/quick/260627-qyc-deckflow-studio-directpush-prod-write-in/260627-qyc-01-SUMMARY.md` when done.
</output>
