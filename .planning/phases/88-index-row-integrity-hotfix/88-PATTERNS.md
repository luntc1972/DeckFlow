# Phase 88: Index-Row Integrity Hotfix - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 7 (all modified, no new files)
**Analogs found:** 7 / 7 (all in-place — this phase edits existing files, so each file is its own primary analog; cross-references are to sibling methods/tests in the same or adjacent files)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (`UpsertContentColumnsOnlyBatchAsync` + SQL const + ctor) | service/model (persistence) | CRUD (batch upsert) | itself — sibling `SetApprovalStatusAsync`/`SetVisibilityAsync` methods in same file for the mirrored-column write shape | exact |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | service interface | CRUD | itself — existing XML-doc convention on every method | exact |
| `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` (`IndexByNaturalKey`, `BuildEntry`) | utility (pure classifier) | transform | `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` `ClassifyDiff`/`DeriveNaturalKey` (the composite-key pattern to extract FROM) | exact — donor pattern already proven in sibling file |
| `DeckFlow.Core/Content/SyncDiffEntry.cs` (`NaturalKeyType` xmldoc + vocabulary) | model (DTO/record) | transform | itself — `ContentSourceType` constants class is the vocabulary source of truth | exact |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (`ClassifyDiff`, `DeriveNaturalKey` deletion, `ComputeDiffAsync` comment, `CreateProdStore`) | service/coordinator | CRUD + transform | `DeckFlow.Studio/Services/IProdStoreFactory.cs` (`ProdStoreFactory.Create`) for the schema-ensure-switch plumbing point | exact |
| `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` (vocabulary sweep, `keyType` derivation) | service/coordinator | CRUD | itself — already derives `ContentSourceType.Youtube`/`Podcast` correctly at line ~158; the classifier/`SyncDiffEntry` side needs to catch up to this file's existing correct pattern | exact (this file is already right — it's the reference for D-07) |
| `DeckFlow.Web/Controllers/ContentKbController.cs` → really `ContentSiteIndexStore.GetPublishedRowsAsync` SQL | service (persistence, read) | CRUD (read filter) | itself — sibling `GetApprovedRowsAsync` SQL in the same file already has the `WHERE approval_status = 'approved'` shape to copy | exact |

## Pattern Assignments

### `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — D-01/D-02 approved-write mirroring

**Analog:** `SetVisibilityAsync(IReadOnlyList<(string,string)> keys, bool visible, ...)` in the same file (lines 650-687) — the existing "batch write that mutates one admin-owned column across natural keys inside one transaction" shape.

**Constructor / ctor pattern to extend for D-09 schema-ensure switch** (lines 13-42):
```csharp
public sealed class ContentSiteIndexStore : IContentSiteIndexStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public ContentSiteIndexStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    public ContentSiteIndexStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        ...
    }
```
D-09's smallest-surface option: add a third ctor parameter (e.g. `bool ensureSchemaEnabled = true`) defaulted to preserve every existing call site, stored as a new `_ensureSchemaEnabled` readonly field. `EnsureSchemaAsync` becomes a no-op when the flag is off (see below) — this satisfies "one mechanism covers all ~20 auto-ensure call sites" without touching any of the ~20 call sites themselves.

**`EnsureSchemaAsync` entry guard to add** (lines 44-52, current):
```csharp
public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
{
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (_schemaReady) return;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Why: schema creation, ALTER backfills, and schema introspection are intentional raw ADO.NET carve-outs for this phase.
        await using var create = connection.CreateCommand();
        create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        ...
```
Add an early `if (!_ensureSchemaEnabled) return;` at the very top (before the `_schemaReady` fast-path) so a prod-mode store with the switch off never issues `CREATE TABLE`/`ALTER TABLE` and the ~20 `await EnsureSchemaAsync(...)` call sites throughout the file are untouched.

**Hardcoded-`'pending'` insert to change (D-01)** — `UpsertContentColumnsOnlySql` (lines 955-993):
```csharp
private const string UpsertContentColumnsOnlySql = """
    INSERT INTO content_site_index (
      source, title, video_url, artifact_path, published_utc, indexed_utc,
      archetype_tags, bracket_tags, card_category_tags,
      natural_key_type, natural_key_value,
      approval_status)
    VALUES (
      @source, @title, @videoUrl, @artifactPath, @publishedUtc, @indexedUtc,
      @archetypeTags, @bracketTags, @cardCategoryTags,
      @naturalKeyType, @naturalKeyValue,
      'pending')
    ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
      source             = EXCLUDED.source,
      title              = EXCLUDED.title,
      video_url          = EXCLUDED.video_url,
      artifact_path      = EXCLUDED.artifact_path,
      published_utc      = EXCLUDED.published_utc,
      indexed_utc        = EXCLUDED.indexed_utc,
      archetype_tags     = EXCLUDED.archetype_tags,
      bracket_tags       = EXCLUDED.bracket_tags,
      card_category_tags = EXCLUDED.card_category_tags;
    -- is_visible, is_hidden, is_evergreen, approval_status are intentionally absent here.
    """;
```
D-01/D-02 change: replace the literal `'pending'` with an `@approvalStatus` parameter (bound from `row.ApprovalStatus` in `BuildUpsertParameters`, lines 713-728, which already binds every other column the same way — add one more `parameters.Add("approvalStatus", row.ApprovalStatus);` line there), and add `approval_status = EXCLUDED.approval_status` to the `ON CONFLICT DO UPDATE SET` list. Delete/rewrite the trailing comment (it currently claims approval_status is "intentionally absent" — that claim becomes false and must be corrected per D-12).

**`BuildUpsertParameters` shared binder (copy this binding style for the new column)** (lines 713-728):
```csharp
private static DynamicParameters BuildUpsertParameters(ContentSiteIndexRow row, (string Type, string Value) naturalKey)
{
    var parameters = new DynamicParameters();
    parameters.Add("source", row.Source);
    parameters.Add("title", row.Title);
    ...
    parameters.Add("naturalKeyType", naturalKey.Type);
    parameters.Add("naturalKeyValue", naturalKey.Value);
    return parameters;
}
```

**Read-filter analog for D-04 (copy this WHERE-clause shape into `GetPublishedRowsAsync`)** — `GetApprovedRowsAsync` (lines 254-285) already has the exact filter shape needed:
```csharp
public async Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
{
    ...
    var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
        """
        SELECT id, source, title, video_url, artifact_path, published_utc,
               pushed_to_prod_utc, indexed_utc, archetype_tags, bracket_tags,
               card_category_tags, natural_key_type, natural_key_value,
               is_visible, is_hidden, is_evergreen, approval_status
          FROM content_site_index
         WHERE approval_status = 'approved'
         ORDER BY source, title, id;
        """,
        cancellationToken: cancellationToken)).ConfigureAwait(false);
    return rows.Select(ToContentSiteIndexRow).ToList();
}
```
`GetPublishedRowsAsync` (lines 220-252) currently filters ONLY on `is_visible = @visible`:
```csharp
             FROM content_site_index
             WHERE is_visible = @visible
             ORDER BY source, title, id;
```
D-04 change: `WHERE is_visible = @visible AND approval_status = 'approved'` — the serve-side defense-in-depth filter, copying the `approval_status = 'approved'` literal exactly as it appears in `GetApprovedRowsAsync`.

---

### `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` — D-05/D-06/D-07/D-08 shared natural-key helper

**Analog to extract FROM (the proven composite-key donor):** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` lines 362-373:
```csharp
// Why: the display natural key used by the content diff (ClassifyDiff). KeyType is the local diff
// label ("youtube"/"podcast"), intentionally NOT the store's youtube_channel/podcast_rss
// discriminator — matching is on the key value, not the type. The write path keys instead via
// ContentIndexExportRow.From (the store discriminator). KeyValue reuses ContentSiteIndexRow.PinId.
private static (string KeyType, string KeyValue) DeriveNaturalKey(ContentSiteIndexRow row)
    => (row.YoutubeVideoId is not null ? "youtube" : "podcast",
        row.PinId ?? string.Empty);
```
and the composite-key join pattern already used in `ClassifyDiff` (lines 116-124, 131-132):
```csharp
var prodByKey = new Dictionary<string, ContentSiteIndexRow>(prodRows.Count, StringComparer.Ordinal);
foreach (var r in prodRows)
{
    var (prodKeyType, prodKeyValue) = DeriveNaturalKey(r);
    if (!string.IsNullOrEmpty(prodKeyValue))
    {
        prodByKey[$"{prodKeyType}\u0000{prodKeyValue}"] = r;
    }
}
...
var (keyType, key) = DeriveNaturalKey(row);
if (!prodByKey.TryGetValue($"{keyType}\u0000{key}", out var prodRow))
```
D-05 shape: extract a shared static helper (Claude's discretion on exact home — a static class in `DeckFlow.Core/Content/` alongside `ContentSyncDiffClassifier`, or a method on `ContentSiteIndexRow` itself) that both `ContentSyncDiffClassifier.IndexByNaturalKey` (below) and `DirectPushCoordinator.ClassifyDiff` call — deleting `DirectPushCoordinator`'s private `DeriveNaturalKey` entirely per D-05.

**Current bare-PinId keying to replace** — `ContentSyncDiffClassifier.IndexByNaturalKey` (lines 76-93):
```csharp
private static Dictionary<string, ContentSiteIndexRow> IndexByNaturalKey(IReadOnlyList<ContentSiteIndexRow> rows)
{
    var map = new Dictionary<string, ContentSiteIndexRow>(StringComparer.Ordinal);
    foreach (var row in rows)
    {
        var key = row.PinId;
        if (string.IsNullOrEmpty(key))
        {
            // A row with neither a YouTube id nor an RSS guid has no natural key to reconcile on; skip it.
            continue;
        }

        // First occurrence wins; the store does not emit duplicate natural keys.
        map.TryAdd(key, row);
    }

    return map;
}
```
D-06/D-08 change: key by the shared helper's composite `type\u0000value` (or a `(string Type, string Value)` tuple key, since `Dictionary<string,...>` requires the `\u0000`-join trick used by `DirectPushCoordinator` — reuse that exact separator so both diff paths produce identical key strings), sourced from the stored `NaturalKeyType`/`NaturalKeyValue` columns per D-06 (NOT `row.PinId`/`YoutubeVideoId is not null` heuristic), with fallback derivation only for legacy in-memory rows lacking those fields. Add a structured `_logger`-style warning on skip (D-08) — but note this is a `static` pure classifier with no injected logger today; check whether D-08's "structured log warning" needs a logger parameter threading change or can go through a static `ILogger`-free mechanism (e.g. return a skip-count/list alongside the diff, or accept an optional `ILogger` — this is a discretion point the plan must resolve, since the class is currently 100% pure/static with zero DI).

**`BuildEntry`'s heuristic key-type derivation to replace (D-07)** — lines 95-117, specifically line 103:
```csharp
var keyType = source.YoutubeVideoId is not null ? "youtube" : "podcast";

return new SyncDiffEntry
{
    NaturalKeyType = keyType,
    NaturalKeyValue = key,
    ...
```
D-07 change: `keyType` comes from the shared helper (stored `NaturalKeyType` column, emitting `ContentSourceType.Youtube`/`ContentSourceType.Podcast` i.e. `"youtube_channel"`/`"podcast_rss"`) instead of the local heuristic string literal.

**Vocabulary source of truth** — `DeckFlow.Core/Knowledge/ContentModels.cs` lines 180-187:
```csharp
public static class ContentSourceType
{
    /// <summary>YouTube channel source type stored in content source rows.</summary>
    public const string Youtube = "youtube_channel";

    /// <summary>Podcast RSS source type stored in content source rows.</summary>
    public const string Podcast = "podcast_rss";
}
```

**Consumer already doing this correctly (reference implementation for D-07's ripple)** — `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` lines 154-161:
```csharp
var prodRow = entry.ProdRow;
// The store + SetApprovalStatusAsync key on the ContentSourceType discriminator
// ("youtube_channel"/"podcast_rss"), NOT the classifier's short "youtube"/"podcast";
// derive it from the row so the approval mirror matches the right row.
var keyType = prodRow.YoutubeVideoId is not null
    ? ContentSourceType.Youtube
    : ContentSourceType.Podcast;
var keyValue = entry.NaturalKeyValue;
```
Once `SyncDiffEntry.NaturalKeyType` itself emits the stored vocabulary (D-07), this local re-derivation + comment in `PullFromProdCoordinator` becomes redundant and should simplify to `entry.NaturalKeyType` directly — this is one of the "sweep target" ripple points named in canonical refs.

---

### `DeckFlow.Core/Content/SyncDiffEntry.cs` — D-07 xmldoc correction

**Current (line 37, to correct):**
```csharp
/// <summary>The natural-key type: "youtube" when the row carries a YouTube id, else "podcast".</summary>
public required string NaturalKeyType { get; init; }
```
Update the doc comment to state the stored-vocabulary values (`ContentSourceType.Youtube` / `ContentSourceType.Podcast`, i.e. `"youtube_channel"`/`"podcast_rss"`) once D-07 lands.

---

### `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — D-05 dedup, D-09/D-10 schema-ensure switch integration, D-12 comment sweep

**False "no DDL" comment to correct (D-12)** — `ComputeDiffAsync` xmldoc (lines 87-91):
```csharp
/// <summary>
/// Reads local approved rows and all prod rows, then runs the content-aware classification
/// (M2). The prod store is built on demand from the ephemeral connection string (D-03) and
/// the read issues no DDL against prod (H3).
/// </summary>
```
This is the comment cited in canonical refs (`docs/research/kb-prod-sync-fix-design.md` C4 §72). Make it true: once D-09/D-10's schema-ensure switch is wired through `CreateProdStore()`, this comment becomes accurate — until then it's a false claim (the store's `EnsureSchemaAsync` DOES currently run `CREATE TABLE IF NOT EXISTS` + `ALTER TABLE` on every prod read via `GetAllRowsAsync`).

**Prod-store construction choke point (D-10 integration site)** — `CreateProdStore()` (lines 362-365):
```csharp
// Builds the on-demand prod store from the ephemeral connection string (D-03) — never at DI
// startup. Shared by the diff read and the publish write so the config key lives in one place.
private IContentSiteIndexStore CreateProdStore()
    => _prodStoreFactory.Create(_configuration["Studio:ProdConnectionString"] ?? string.Empty);
```
This delegates to `IProdStoreFactory.Create(string)` (`DeckFlow.Studio/Services/IProdStoreFactory.cs`, full file below) — the single choke point per D-10. The factory interface signature likely needs to grow (e.g. an overload or an options object) to plumb the schema-ensure-off flag into the `ContentSiteIndexStore` ctor `ProdStoreFactory.Create` builds.

**`IProdStoreFactory` + `ProdStoreFactory` (full file, the plumbing target)**:
```csharp
public interface IProdStoreFactory
{
    /// <summary>Builds a Postgres-backed store from <paramref name="connectionString"/>.</summary>
    IContentSiteIndexStore Create(string connectionString);
}

public sealed class ProdStoreFactory : IProdStoreFactory
{
    public IContentSiteIndexStore Create(string connectionString)
    {
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, normalized);
        return new ContentSiteIndexStore(conn);
    }
}
```
D-10 says "ALL prod-pointed stores, always" disable schema-ensure — since `ProdStoreFactory.Create` is the ONLY place a prod `ContentSiteIndexStore` is constructed (grep confirms `IProdStoreFactory` usage is limited to `Program.cs` DI registration, `DirectPushCoordinator.cs`, and the two test doubles), the smallest-surface fix is inside `ProdStoreFactory.Create` itself: pass the new schema-ensure-off ctor argument unconditionally, with NO interface signature change needed at all — `Create(string connectionString)` stays as-is; only the ctor call inside changes to `new ContentSiteIndexStore(conn, ensureSchemaEnabled: false)`. This is the cleanest of the three discretion options listed in CONTEXT.md.

**`ClassifyDiff`/`DeriveNaturalKey` to dedupe (D-05)** — lines 102-117, 362-373 (already excerpted above under the classifier section) — delete the private `DeriveNaturalKey` method here and call the shared `DeckFlow.Core` helper instead. `ClassifyDiff` itself (lines 109-152) keeps its `\u0000`-joined `Dictionary<string, ContentSiteIndexRow>` structure — only the per-row key-derivation call changes from local `DeriveNaturalKey(row)` to the shared helper.

---

### Test Patterns

**xUnit SQLite-integration fixture convention** (copy this ctor/dispose shape for D-01/D-02/D-04 tests) — `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs` lines 15-35:
```csharp
public sealed class ContentSiteIndexStoreApprovalTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreApprovalTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-approval-{Guid.NewGuid():N}.db");
        _store = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }
    ...
```
Same convention repeats in `ContentSiteIndexStoreBatchUpsertTests.cs` (lines 19-41) — use whichever test file is the natural home (`ContentSiteIndexStoreApprovalTests` for the D-01/D-02 mirror-on-insert-and-update cases, since it already tests approval semantics).

**Pure-classifier test convention** (copy this `Row(...)` builder + fact shape for the D-06 composite-key-collision regression test) — `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs` lines 12-36, 156-175:
```csharp
private static ContentSiteIndexRow Row(
    string? youtubeId = "yt-1",
    string? rssGuid = null,
    ...
    string approvalStatus = "approved") =>
    new() { Id = 1, Source = "youtube", Title = title, ... YoutubeVideoId = youtubeId, RssGuid = rssGuid };

[Fact]
public void Classify_PodcastAndYoutubeKeys_ClassifiedIndependently()
{
    var prod = new[]
    {
        Row(youtubeId: "yt-1", rssGuid: null),
        Row(youtubeId: null, rssGuid: "rss-only-prod")
    };
    var local = new[] { Row(youtubeId: "yt-1", rssGuid: null) };

    var result = ContentSyncDiffClassifier.Classify(prod, local);

    var entry = Assert.Single(result);
    Assert.Equal("rss-only-prod", entry.NaturalKeyValue);
    Assert.Equal("podcast", entry.NaturalKeyType);
    Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
}
```
For the D-06 collision regression test (a YouTube id string that equals a podcast RSS guid string must NOT cross-match), add a new `[Fact]` in this file following this exact builder/assert shape, constructing one row with `youtubeId: "COLLIDE"` and another with `rssGuid: "COLLIDE"` and asserting BOTH appear as separate diff entries (not merged/dropped).

**`DirectPushCoordinator.ClassifyDiff` collision test already exists — extend, don't duplicate** — `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` line 120: `ClassifyDiff_YoutubeAndPodcastShareKeyValue_DoNotCollide` is the exact proven pattern for the `DirectPushCoordinator` side of the same collision fix; mirror its structure for the shared-helper extraction so both suites still pass unchanged in shape (only the call target moves).

**Batch-upsert transactional-rollback test convention** (for the D-11 no-DDL recording-connection test, follow this per-fact structuring) — `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs` lines 45-78 shows the "construct store against temp SQLite file, exercise one write path, assert on `GetAllRowsAsync()`" idiom; no existing "recording connection" or command-interceptor fake exists in the codebase yet (`grep -rn "RecordingConnection\|DbCommandInterceptor"` returns nothing) — D-11's mechanics are genuinely new and left to plan-time discretion. The closest existing "recording" convention in spirit is `DeckFlow.Core.Tests/Orchestration/RecordingOrchestratorDoubles.cs`, which records invoked operations on a fake rather than a real ADO.NET connection; that file is worth reading at plan time for the general "recording double" naming/shape convention (`Recording*` prefix, a `IReadOnlyList<...>` of recorded calls exposed for assertion) even though it isn't DB-specific.

## Shared Patterns

### Dapper `DynamicParameters` binding for new SQL columns
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:713-728` (`BuildUpsertParameters`)
**Apply to:** The D-01 `approval_status` parameter addition to the batch/single upsert SQL.
```csharp
parameters.Add("naturalKeyType", naturalKey.Type);
parameters.Add("naturalKeyValue", naturalKey.Value);
return parameters;
```

### `EnsureSchemaAsync` call-site convention (untouched by the switch)
**Source:** every public method in `ContentSiteIndexStore.cs` opens with `await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);` (~20 occurrences, e.g. lines 131, 150, 170, 223, 257, 290...).
**Apply to:** D-09 — the switch must live INSIDE `EnsureSchemaAsync` itself (early return when disabled) so none of these ~20 call sites need editing.

### Natural-key composite join (`\u0000`-separated — U+0000 NULL, NOT a space)
**Source:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs:116-124, 131-132` — the separator is the U+0000 NULL character (a shipped Codex MED anti-collision fix: NULL cannot appear in either key component, unlike a space). Do NOT replace it with a space; doing so would revert the hardening.
**Apply to:** Both `ContentSyncDiffClassifier.IndexByNaturalKey` and the extracted shared helper's Dictionary-key construction (D-05/D-06) — reuse the exact `$"{type}\u0000{value}"` format so both diff paths key identically and can never silently diverge again.

### `ContentSourceType` vocabulary constants
**Source:** `DeckFlow.Core/Knowledge/ContentModels.cs:180-187`
**Apply to:** `SyncDiffEntry.NaturalKeyType`, the shared natural-key helper's return type, `ContentSyncDiffClassifier.BuildEntry`, and any remaining `"youtube"`/`"podcast"` string literals found by the D-07 grep sweep (`PullFromProdCoordinator.cs`, `DirectPushCoordinatorTests.cs`, `ContentSyncDiffClassifierTests.cs`, `PullFromProdCoordinatorTests.cs`).

### Structured logging via injected `ILogger`
**Source:** `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs:209` — `_logger.LogError(ex, "Local apply failed for pull-from-prod entry {KeyType}:{KeyValue}.", keyType, keyValue);`
**Apply to:** D-08's "structured log warning naming the row" for skipped missing-key rows — BUT note `ContentSyncDiffClassifier` is currently a pure `static` class with no `ILogger` dependency; the plan must decide whether to (a) thread an optional `ILogger` parameter into `Classify(...)`, (b) return skip diagnostics in the result for the caller to log, or (c) accept the class becoming non-static. Flag this as an open design question for the planner, not a copy-paste pattern.

## No Analog Found

| File/Concern | Role | Data Flow | Reason |
|---|---|---|---|
| D-11 recording-connection / command-interceptor test double | test infrastructure | event-driven (SQL-issued assertion) | No existing fake `IRelationalDialect`/`DbConnection` wrapper or SQL-recording test double exists in the codebase (`RecordingOrchestratorDoubles.cs` is the nearest naming convention but is not DB-related). This is genuinely new test infrastructure — CONTEXT.md explicitly leaves the mechanics to Claude's discretion at plan time. Two viable approaches: (1) a `DbConnection`/`DbCommand` wrapper that records `CommandText` before delegating to a real SQLite connection, or (2) a lightweight SQLite-backed integration test that runs the prod-mode store's read+write paths against a schema-less (or intentionally-wrong-schema) SQLite file and asserts the operation throws a schema-missing error rather than silently auto-creating the table — this option reuses the existing `ContentSiteIndexStore(string databasePath)` ctor + temp-file idiom with zero new infra. |
| D-08 logger threading into a pure static classifier | architectural | transform | See "Structured logging" shared-pattern note above — resolving this changes `ContentSyncDiffClassifier`'s purity contract (currently explicitly documented as "No I/O, no DI, no exceptions for valid input" in its class-level xmldoc, lines 5-10) and needs a planner decision, not a code pattern to copy. |

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Studio/ViewModels/`, `DeckFlow.Studio/Services/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Core.Tests/Content/`, `DeckFlow.Studio.Tests/ViewModels/`
**Files scanned:** 7 primary files (all listed in canonical_refs) + `DeckFlow.Core/Knowledge/ContentModels.cs`, `DeckFlow.Studio/Services/IProdStoreFactory.cs`, `DeckFlow.Studio/Program.cs`, plus 5 existing test files for pattern extraction
**Pattern extraction date:** 2026-07-06
