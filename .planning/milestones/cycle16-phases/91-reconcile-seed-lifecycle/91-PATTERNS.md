# Phase 91: Reconcile + Seed Lifecycle - Pattern Map

**Mapped:** 2026-07-08
**Files analyzed:** 15 (7 modified, 8 new)
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (MOD: `seed_managed` DDL + column threading) | model/store | CRUD | itself — `body_sha256` rollout in the SAME file | exact (self-precedent) |
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (MOD: `ContentSiteIndexRow.SeedManaged`) | model | CRUD | itself — `BodySha256`/`AwaitingConfirmUtc` props in the SAME file | exact (self-precedent) |
| `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs` (NEW) | service (pure classifier) | transform | `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` | exact |
| `DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs` (NEW) | model (records) | transform | `DeckFlow.Core/Content/ContentKbOrphanScanner.cs` (`ContentKbRowCheck`/`ContentKbOrphanScanResult` records) | exact |
| `DeckFlow.Core/Content/SeedManagedBackfill.cs` (NEW) | service (host-agnostic backfill) | batch | `DeckFlow.Core/Content/ContentBodyHashBackfill.cs` | exact |
| `DeckFlow.Studio/Services/ProdContentReader.cs` (MOD: add `body_sha256`/`seed_managed` to select+map) | service (read-only prod reader) | request-response | itself — existing `SelectAllSql`/`ContentSiteIndexRowData` in the SAME file | exact (self-precedent) |
| `DeckFlow.Studio/Services/IContentKbReconcileStore.cs` + `ContentKbReconcileStore.cs` (NEW) | store (local SQLite) | CRUD + idempotent upsert | `DeckFlow.Core/Content/ContentHarvestRunStore.cs` | exact |
| `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs` (NEW) | service (I/O orchestrator) | batch / file-I/O + event-driven | `DeckFlow.Studio/Services/GitBodyCoverageAudit.cs` | exact |
| `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` (NEW) | controller/coordinator (Studio operator action) | request-response | `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (+ `PublishCoordinator.cs` for the export/commit shape) | exact |
| `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` (MOD: stamp `SeedManaged = true`) | service (seed loader) | batch | itself — `BuildRow` in the SAME file | exact (self-precedent) |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (MOD: register `sync.reconcile`) | config | CRUD | itself — `sync.directpush-gitbody` entry in the SAME file | exact (self-precedent) |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (MOD: seed `sync.reconcile` OFF, both dialects) | config/store | CRUD | itself — `sync.directpush-gitbody` seed rows in the SAME file | exact (self-precedent) |
| `DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs` (NEW) | test | transform | `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs` | exact |
| `DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs` (NEW) | test | batch | `DeckFlow.Core.Tests/Content/ContentBodyHashBackfillTests.cs` | exact |
| `DeckFlow.Studio.Tests/Services/ContentKbReconcileStoreTests.cs`, `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs`, extended `ProdContentReaderTests.cs`/`ContentSiteIndexStoreTests.cs`/`ContentKbSeedLoaderTests.cs` (NEW/MOD) | test | CRUD / request-response | `DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs` + `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` | exact |

## Pattern Assignments

### `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (store, CRUD) — `seed_managed` column

**Analog:** itself, the existing `body_sha256`/`awaiting_confirm_utc` rollout in the same file (D-01 explicitly says "exactly like SYNC-01's body_sha256 rollout").

**Dialect-guarded additive DDL pattern** (`ContentSiteIndexStore.cs:132-150`, template to replicate for `seed_managed`):
```csharp
if (!columns.Contains("body_sha256"))
{
    // Why: TEXT NULL is valid in both dialects — no IsPostgres branch needed (D-09).
    await using var addBodySha256 = connection.CreateCommand();
    addBodySha256.CommandText = "ALTER TABLE content_site_index ADD COLUMN body_sha256 TEXT NULL;";
    await addBodySha256.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

if (!columns.Contains("awaiting_confirm_utc"))
{
    // Why: durable "pushed, awaiting deploy-confirm" marker (D-10); dialect-guarded like
    // pushed_to_prod_utc since it is a genuine TIMESTAMPTZ on Postgres. Never filtered on
    // in a WHERE clause (F-51-PG-01 avoided) — only ever set/cleared keyed on natural key.
    await using var addAwaitingConfirmUtc = connection.CreateCommand();
    addAwaitingConfirmUtc.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN awaiting_confirm_utc TIMESTAMPTZ NULL;"
        : "ALTER TABLE content_site_index ADD COLUMN awaiting_confirm_utc TEXT NULL;";
    await addAwaitingConfirmUtc.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```
`seed_managed` follows the `awaiting_confirm_utc` shape (dialect-guarded, both branches present) rather than the `body_sha256` shape (single TEXT-works-everywhere line) — per RESEARCH Pattern 2 / Pitfall 1, it MUST be `BOOLEAN NULL` (Postgres) / `INTEGER NULL` (SQLite), never a non-nullable bool with a `DEFAULT`, so `NULL` can mean "not yet classified" (distinct from `false` = "classified prod-owned"). Add it to `PostgresCreateTableSql`/`SqliteCreateTableSql` (`ContentSiteIndexStore.cs:1210-1258`) as `seed_managed BOOLEAN NULL,` / `seed_managed INTEGER NULL,` alongside the existing `body_sha256`/`awaiting_confirm_utc` lines. Add `seed_managed` to all 5 `SELECT` column lists (`GetByNaturalKeyAsync`, `GetPublishedRowsAsync`, `GetApprovedRowsAsync`, `GetAllRowsAsync`, `GetByIdAsync` — `ContentSiteIndexStore.cs:242-448`) and the `ContentSiteIndexRowData`/`ToContentSiteIndexRow` mapping (`ContentSiteIndexStore.cs:1031-1065, 1260-1281`).

**Per-row bound parameter, NOT a SQL literal** (`ContentSiteIndexStore.cs:896-925`, `BuildUpsertParameters` — the exact template to extend):
```csharp
private static DynamicParameters BuildUpsertParameters(ContentSiteIndexRow row, (string Type, string Value) naturalKey)
{
    var parameters = new DynamicParameters();
    // ... existing bindings ...
    // Why: approval_status (D-01) so the content-columns-only upsert carries approval on insert
    // AND heals a drifted prod row on update; other upsert variants ignore this unbound-to-their-SQL
    // parameter harmlessly.
    parameters.Add("approvalStatus", row.ApprovalStatus);
    // Why: body_sha256 (D-01/D-09) is bound here so all three upsert variants can bind it;
    // variants whose SQL doesn't reference @bodySha256 ignore this parameter harmlessly.
    parameters.Add("bodySha256", row.BodySha256);
    // NEW, same shape: parameters.Add("seedManaged", row.SeedManaged);
    return parameters;
}
```
Add `seed_managed` to the `UpsertContentColumnsOnlySql` and `UpsertPreservingVisibilitySql` column/VALUES/`ON CONFLICT ... DO UPDATE` lists (`ContentSiteIndexStore.cs:1113-1208`), with `seed_managed = EXCLUDED.seed_managed` in both — but see the comment-discipline note in Pitfall 4 below; `UpsertPreservingVisibilitySql` needs an explicit "why always TRUE here" comment on `seed_managed` because that statement has THREE existing override strategies (preserved / overwritten-from-EXCLUDED / operator-owned) that a fourth column must be deliberately slotted into, following the `body_sha256` comment already at `ContentSiteIndexStore.cs:1159-1161`:
```csharp
// body_sha256 is OVERWRITTEN from EXCLUDED (like indexed_utc), NOT preserved (WARNING 1):
// a corrected seed hash must propagate on reseed, protecting D-08's one-time backfill intent.
body_sha256        = EXCLUDED.body_sha256;
```
**Do NOT** add `seed_managed` to the plain `UpsertSql` (`ContentSiteIndexStore.cs:1067-1111`, the local-distill write path via `UpsertRowAsync`) unless a caller explicitly wants to stamp it there — the local-distill write must leave `seed_managed` unset (NULL/unclassified or false), never `true`.

**Backfill write template** (`SetBodySha256IfNullAsync`, `ContentSiteIndexStore.cs:486-501` — exact template for a new `SetSeedManagedIfNullAsync`):
```csharp
public async Task<int> SetBodySha256IfNullAsync(long id, string bodySha256, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(bodySha256);
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    return await connection.ExecuteAsync(new CommandDefinition(
        """
        UPDATE content_site_index
           SET body_sha256 = @bodySha256
         WHERE id = @id
           AND body_sha256 IS NULL;
        """,
        new { bodySha256, id },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```
A new `SetSeedManagedIfNullAsync(long id, bool seedManaged, ...)` follows this exact shape (`WHERE id = @id AND seed_managed IS NULL`) — idempotent, never overwrites a row already classified.

---

### `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (model) — `ContentSiteIndexRow.SeedManaged`

**Analog:** the existing `BodySha256`/`AwaitingConfirmUtc` properties in the same record (`ContentArtifactSpec.cs:148-164`):
```csharp
public string ApprovalStatus { get; init; } = "pending";
// ...
public string? BodySha256 { get; init; }
// ...
public DateTimeOffset? AwaitingConfirmUtc { get; init; }
```
Add `public bool? SeedManaged { get; init; }` alongside these — nullable `bool?`, matching the `BOOLEAN NULL` DDL (Pitfall 1: non-nullable would collapse "unclassified" into "prod-owned").

---

### `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs` (NEW — pure classifier, transform)

**Analog:** `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` (full file, 137 lines).

**Class shape** (`ContentSyncDiffClassifier.cs:1-38`):
```csharp
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// Pure classifier that compares production and local <see cref="ContentSiteIndexRow"/> sets by natural
/// key and labels each *differing* entry ... No I/O, no DI; the only side channel is an OPTIONAL
/// <see cref="ILogger"/> used to warn on rows that have no natural key to reconcile on (D-08).
/// </summary>
public static class ContentSyncDiffClassifier
{
    public static IReadOnlyList<SyncDiffEntry> Classify(
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        IReadOnlyList<ContentSiteIndexRow> localRows,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(prodRows);
        ArgumentNullException.ThrowIfNull(localRows);
        // ...
    }
}
```
`ContentKbReconcileClassifier` mirrors this exactly: `static class`, a single entry-point method taking 3 already-loaded in-memory collections (prod rows, git-tree file-path set, seed entries) + optional `ILogger?`, returning discrepancy records. NO DI, NO I/O.

**Natural-key indexing — the reusable piece** (`ContentSyncDiffClassifier.cs:85-110`):
```csharp
private static Dictionary<string, ContentSiteIndexRow> IndexByNaturalKey(
    IReadOnlyList<ContentSiteIndexRow> rows,
    ILogger? logger)
{
    var map = new Dictionary<string, ContentSiteIndexRow>(StringComparer.Ordinal);
    foreach (var row in rows)
    {
        if (!ContentNaturalKey.TryDerive(row, out var nk))
        {
            logger?.LogWarning(
                "Skipping content row with no natural key (neither YouTube id nor RSS guid): {Title} [{Source}]",
                row.Title,
                row.Source);
            continue;
        }

        // Composite key uses the U+0000 NULL separator ... identical to the DirectPushCoordinator
        // key format (SYNC-05). First occurrence wins.
        map.TryAdd($"{nk.Type}{nk.Value}", row);
    }

    return map;
}
```
Reuse this shape for seed-drift (index `seed_managed=true` prod rows AND parsed `index-seed.json` entries by the SAME ``-joined key), keyed via `DeckFlow.Core.Content.ContentNaturalKey.TryDerive` — the ONE shared natural-key helper (`ContentNaturalKey.cs:35-53`). **Do NOT** import `SyncDiffKind`'s timestamp-direction branching (`ContentSyncDiffClassifier.cs:50-69`, ProdNewer/Diverged logic) — per RESEARCH Pitfall 3, seed-drift is a plain SET DIFFERENCE by natural key, not a timestamp comparison; the git seed JSON has no meaningful `IndexedUtc` direction to compare.

**Published-orphan loop shape** (`GitBodyCoverageAudit.cs:36-62`, to be lifted into the pure classifier — the orchestrator builds the `HashSet<string>` of existing file paths and passes it in, per RESEARCH Open Question 2):
```csharp
foreach (var row in prodRows)
{
    if (!string.Equals(row.ApprovalStatus, "approved", StringComparison.Ordinal) || !row.IsVisible)
    {
        continue;
    }

    var isPresent = ArtifactPathSafety.TryBuildContainedPath(repoRoot, row.ArtifactPath, out var fullPath)
        && File.Exists(fullPath);

    if (!isPresent)
    {
        missing.Add(new GitBodyCoverageMissingRow(keyType, keyValue, row.Title, row.ArtifactPath));
    }
}
```
The classifier's published-orphan class takes a pre-built `HashSet<string>` (or `IReadOnlySet<string>`) of existing relative file paths instead of doing `File.Exists` itself — I/O stays in the Studio orchestrator (`ArtifactPathSafety.TryBuildContainedPath` + `File.Exists`); the classifier only does set membership.

**Mine, do NOT extend, `ContentKbOrphanScanner`** (`ContentKbOrphanScanner.cs:14-37`, record shapes to mirror for `ContentKbReconcileDiscrepancy.cs`):
```csharp
public sealed record ContentKbRowCheck(
    string ArtifactPath,
    bool Exists,
    bool IsVisible,
    bool IsHidden,
    string ApprovalStatus,
    bool IsPublishedOrphan);

public sealed record ContentKbOrphanScanResult(
    int TotalRows,
    int RowsWithArtifact,
    int MissingCount,
    int PublishedOrphanCount,
    int HiddenOrphanCount,
    IReadOnlyList<ContentKbRowCheck> Rows);
```
`ContentKbReconcileDiscrepancy` follows this record-with-derived-flags shape (D-07: mine the shape, do not subclass/extend the scanner itself — it is local-only and covers only one of the four classes).

---

### `DeckFlow.Core/Content/SeedManagedBackfill.cs` (NEW — host-agnostic backfill, batch)

**Analog:** `DeckFlow.Core/Content/ContentBodyHashBackfill.cs` (full file, 115 lines) — same host-agnostic, idempotent, cannot-crash-startup shape.

**Full pattern to mirror** (`ContentBodyHashBackfill.cs:16-114`):
```csharp
public sealed class ContentBodyHashBackfill
{
    private readonly IContentSiteIndexStore _store;
    private readonly IContentArtifactBodyResolver _resolver;
    private readonly ILogger<ContentBodyHashBackfill> _logger;

    public ContentBodyHashBackfill(
        IContentSiteIndexStore store,
        IContentArtifactBodyResolver resolver,
        ILogger<ContentBodyHashBackfill> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _store.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
        // ... per-row: skip if already classified; catch OperationCanceledException and rethrow;
        // catch other exceptions, log warning, skip row (never crash host startup); write via the
        // null-only store method; log a structured summary at the end.
    }
}
```
`SeedManagedBackfill` mirrors this shape exactly, but its "resolver" input is the PARSED seed file's natural-key set (not an artifact-body resolver) — `RunAsync` calls `IContentSiteIndexStore.GetAllRowsAsync`, computes membership by `ContentNaturalKey.TryDerive(row)` against the seed's key set (D-02: present → `true`, absent → `false`), then calls a new `SetSeedManagedIfNullAsync(row.Id, classified, ...)` per un-classified (`SeedManaged is null`) row — reusing the exact `WHERE ... IS NULL` idempotent-write idiom.

**Cancellation/error-swallow discipline** (`ContentBodyHashBackfill.cs:70-93`, the exact try/catch shape to copy):
```csharp
try
{
    rawArtifactText = await _resolver.TryReadArtifactTextAsync(row.ArtifactPath, cancellationToken).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    // Cancellation ... always propagate it rather than skipping the row.
    throw;
}
catch (Exception ex)
{
    // ... this backfill runs during host startup, so a single unreadable artifact
    // ... must never crash the host ... Skip the row and continue.
    skippedCount++;
    _logger.LogWarning(ex, "Content KB body-hash backfill skipped row {ContentKbRowId}: artifact read failed.", row.Id);
    continue;
}
```

---

### `DeckFlow.Studio/Services/ProdContentReader.cs` (MOD — extend `SelectAllSql` + mapping)

**Analog:** itself (`ProdContentReader.cs:21-26, 153-207`) — required extension, not a nice-to-have (RESEARCH Pitfall 2, "the reconciler's body-hash-mismatch class is unbuildable without it").

**Current (missing `body_sha256`/`seed_managed`)**:
```csharp
private const string SelectAllSql = """
    SELECT id, source, title, video_url, artifact_path, published_utc, pushed_to_prod_utc,
           indexed_utc, archetype_tags, bracket_tags, card_category_tags, natural_key_type,
           natural_key_value, is_visible, is_hidden, is_evergreen, approval_status
      FROM content_site_index;
    """;
```
Add `body_sha256, seed_managed` to the column list, mirroring `ContentSiteIndexStore.GetAllRowsAsync`'s SELECT shape exactly (`ContentSiteIndexStore.cs:349-375`). Extend the private `ContentSiteIndexRowData` class (`ProdContentReader.cs:187-207`) with `public string? BodySha256 { get; init; }` and `public bool? SeedManaged { get; init; }`, and `ToContentSiteIndexRow` (`ProdContentReader.cs:153-185`) to map them onto `ContentSiteIndexRow.BodySha256`/`SeedManaged`. This reader issues NO `EnsureSchemaAsync`/DDL (structurally read-only, R1) — do not add any schema-ensure call when extending.

**Tri-state flag read** (`ProdContentReader.cs:110-149`, reused verbatim — no changes needed, `sync.reconcile` flows through the SAME `TryReadFlagAsync`):
```csharp
public async Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
{
    // ...
    var enabled = await connection.QuerySingleOrDefaultAsync<bool?>(
        new CommandDefinition(SelectFlagSql, new { key }, cancellationToken: cancellationToken));
    // A missing row / null enabled is a DEFINITIVE OFF (false), NOT indeterminate — only a
    // caught read failure below returns null.
    return enabled ?? false;
    // ... catch (Exception) return null;  // indeterminate
}
```

---

### `DeckFlow.Studio/Services/IContentKbReconcileStore.cs` + `ContentKbReconcileStore.cs` (NEW — local store, CRUD + idempotent upsert)

**Analog:** `DeckFlow.Core/Content/ContentHarvestRunStore.cs` (full file, 185 lines) — 9-sibling `content-kb.db` schema-ensure pattern.

**Constructor + schema-ensure shape** (`ContentHarvestRunStore.cs:11-62`):
```csharp
public sealed class ContentHarvestRunStore : IContentHarvestRunStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public ContentHarvestRunStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    public ContentHarvestRunStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }
    // ...
    private const string PostgresCreateTableSql = """CREATE TABLE IF NOT EXISTS content_harvest_runs ( ... );""";
    private const string SqliteCreateTableSql = """CREATE TABLE IF NOT EXISTS content_harvest_runs ( ... );""";
}
```
`ContentKbReconcileStore` mirrors this exactly (dialect-capable-but-SQLite-constructed, per RESEARCH Open Question 3): SemaphoreSlim-gated `EnsureSchemaAsync`, `CREATE TABLE IF NOT EXISTS content_kb_reconcile_discrepancy (...)` (schema recommended in RESEARCH.md Pattern 3), `RETURNING id`/`ExecuteScalarAsync<long>` for a `StartRunAsync`-equivalent if a run-scoping concept is needed. Registration mirrors `Program.cs:103` (9th sibling → 10th):
```csharp
builder.Services.AddSingleton<IContentHarvestRunStore>(_ => new ContentHarvestRunStore(contentKbDatabasePath));
// NEW, same shape:
builder.Services.AddSingleton<IContentKbReconcileStore>(_ => new ContentKbReconcileStore(contentKbDatabasePath));
```

**Idempotent upsert + resolution-by-absence SQL shape** (RESEARCH Pattern 4, to author fresh — no direct existing analog for resolution-by-absence, but the upsert shape mirrors `FeatureFlagStore`'s `ON CONFLICT (key) DO UPDATE` at `FeatureFlagStore.cs:280-294`):
```sql
INSERT INTO feature_flags (key, enabled, updated_at)
VALUES (@key, @enabled, @now)
ON CONFLICT (key) DO UPDATE SET
  enabled    = EXCLUDED.enabled,
  updated_at = EXCLUDED.updated_at;
```
Discrepancy upsert follows this `ON CONFLICT (discrepancy_id) DO UPDATE SET last_seen_utc = EXCLUDED.last_seen_utc` shape; resolution-by-absence is a separate `UPDATE ... SET resolved_utc = @now WHERE scope_tag = @scopeTag AND resolved_utc IS NULL AND discrepancy_id NOT IN @currentlySeenIds` pass run once per reconcile.

---

### `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs` (NEW — I/O orchestrator, batch/file-I/O)

**Analog:** `DeckFlow.Studio/Services/GitBodyCoverageAudit.cs` (full file, 66 lines).

**Full shape to mirror** (`GitBodyCoverageAudit.cs:13-65`):
```csharp
public sealed class GitBodyCoverageAudit : IGitBodyCoverageAudit
{
    private readonly IProdContentReader _prodReader;

    public GitBodyCoverageAudit(IProdContentReader prodReader)
    {
        ArgumentNullException.ThrowIfNull(prodReader);
        _prodReader = prodReader;
    }

    public async Task<GitBodyCoverageReport> RunAsync(
        string prodConnectionString,
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var prodRows = await _prodReader
            .ReadAllAsync(prodConnectionString ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        // ... loop, ArtifactPathSafety-guarded File.Exists, build report
        return new GitBodyCoverageReport(missing);
    }
}
```
`ContentKbReconcileOrchestrator` takes the SAME constructor dependency (`IProdContentReader`), reads prod ONCE per run (RESEARCH Security Domain: "call it ONCE per run, not per-row"), resolves `repoRoot` via `IGitRepository.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory(), ...)` (same as `DirectPushCoordinator.CommitAndPushBodiesAsync:410`), enumerates `Directory.EnumerateFiles(Path.Combine(repoRoot, "content-kb"), "*.md", SearchOption.AllDirectories)` for the file-orphan class (net-new capability — RESEARCH Pitfall 6), parses `index-seed.json` via the SAME `ContentKbSeedLoader`-shaped `JsonSerializerOptions` (camelCase, case-insensitive), calls `ContentKbReconcileClassifier` (pure), then persists to `IContentKbReconcileStore` and writes the D-06 report file.

**Path-safety guard — the ONE sanctioned routine, reuse verbatim** (`ArtifactPathSafety.cs:22-42`):
```csharp
public static bool TryBuildContainedPath(string root, string artifactPath, out string resolvedPath)
{
    resolvedPath = string.Empty;
    if (!IsSafeArtifactPath(artifactPath)) return false;

    var rootFull = Path.GetFullPath(root);
    var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
    var candidate = Path.GetFullPath(Path.Combine(rootFull, artifactPath));
    if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;

    resolvedPath = candidate;
    return true;
}
```
Use this for BOTH directions (row→file for published-orphan, file→row for file-orphan — the file-orphan direction needs the inverse: build the file's path relative to `repoRoot`, verify it starts with `content-kb/`, matching `IsSafeArtifactPath`'s prefix check at `ArtifactPathSafety.cs:64`).

---

### `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` (NEW — Studio operator-action coordinator, request-response)

**Analog:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (full file, 716 lines) for the flag-gated destructive-write pattern; `PublishCoordinator.cs` (full file, 337 lines) for the export/diff/commit staging shape.

**Constructor + optional-logger convention** (`DirectPushCoordinator.cs:72-106`):
```csharp
public DirectPushCoordinator(
    IContentSiteIndexStore localStore,
    ISshArtifactUploader uploader,
    IProdStoreFactory prodStoreFactory,
    IConfiguration configuration,
    ContentKbOrchestratorOptions options,
    IGitRepository git,
    IContentKbOrchestrator orchestrator,
    IProdContentReader prodReader,
    IDeployedBodyConfirmer confirmer,
    ILogger<DirectPushCoordinator>? logger = null)
{
    ArgumentNullException.ThrowIfNull(localStore);
    // ... (ArgumentNullException.ThrowIfNull for every required dependency)
    _logger = logger ?? NullLogger<DirectPushCoordinator>.Instance;
}
```

**Flag-gated destructive apply — the EXACT tri-state pattern to copy for `sync.reconcile`** (`DirectPushCoordinator.cs:296-352`, `VerifyAndPublishAsync`):
```csharp
var directPushGitBodyFlag = await TryReadDirectPushGitBodyFlagAsync(cancellationToken).ConfigureAwait(false);
if (directPushGitBodyFlag == false)
{
    // definitive OFF path
}
// null (indeterminate) falls through to the SAFE branch — never treated as license to proceed
// with the destructive action.
```
```csharp
private Task<bool?> TryReadDirectPushGitBodyFlagAsync(CancellationToken cancellationToken)
    => _prodReader.TryReadFlagAsync(
        _configuration["Studio:ProdConnectionString"] ?? string.Empty,
        DirectPushGitBodyFlagKey,
        cancellationToken);
```
`ReconcileCoordinator`'s Apply action reads `sync.reconcile` via the identical `IProdContentReader.TryReadFlagAsync` call, using a `ReconcileFlagKey = "sync.reconcile"` const (mirroring `DirectPushGitBodyFlagKey`), and — per D-09/RESEARCH Anti-Pattern — treats BOTH `false` and `null` as "refuse to apply" (unlike `VerifyAndPublishAsync`, whose `false` path proceeds; here the flag gates a purely destructive write, so only a confirmed `true` may proceed). Prod store construction reuses the SAME on-demand factory pattern (`DirectPushCoordinator.cs:579-580`):
```csharp
private IContentSiteIndexStore CreateProdStore()
    => _prodStoreFactory.Create(_configuration["Studio:ProdConnectionString"] ?? string.Empty);
```

**Two-step re-validated apply (D-08)** — no direct single-method analog exists, but `ComputeDiffAsync` (`DirectPushCoordinator.cs:119-133`) is the "compute a fresh diff on demand" shape to call TWICE (once for dry-run, once inside Apply immediately before the write):
```csharp
public async Task<DirectPushDiff> ComputeDiffAsync(CancellationToken cancellationToken)
{
    var localRows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
    var prodStore = CreateProdStore();
    var prodRows = await prodStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
    return ClassifyDiff(localRows, prodRows, _logger);
}
```
`ReconcileCoordinator.ApplyRemovalsAsync` re-runs the seed-drift classification fresh (not from the persisted dry-run store) immediately before soft-hiding, exactly matching this "compute fresh, then act" shape.

**Soft-hide write — reuse the visibility-write surface, do NOT hand-roll** (`ContentSiteIndexStore.cs:841-877`, `SetVisibilityAsync(IReadOnlyList<(string,string)> keys, bool visible, ...)` — the natural-key-batch variant, since Apply operates on a set of discrepant rows):
```csharp
public async Task<int> SetVisibilityAsync(
    IReadOnlyList<(string Type, string Value)> keys,
    bool visible,
    CancellationToken cancellationToken = default)
{
    // ... one transaction, is_hidden cleared unconditionally, keyed by natural_key_type/value
    const string sql = """
        UPDATE content_site_index
           SET is_visible = @visible,
               is_hidden = FALSE
         WHERE natural_key_type = @type
           AND natural_key_value = @value;
        """;
    // ...
}
```
Call this against the PROD store (via `CreateProdStore()`) with `visible: false` for the re-validated set of `seed_managed=true` discrepant natural keys. Do NOT touch any timestamp column on this write (RESEARCH Pitfall 5 / F-51-PG-01) — D-03 says retain existing timestamps, not add a new one.

**Seed-export reuse — the ONE shared factory, no forked writer** (`PublishCoordinator.cs:96-101`, `DirectPushCoordinator.cs:478-490` both call the SAME method):
```csharp
var exportResult = await _orchestrator.ExportIndexToFileAsync(seedAbsPath, progress, cancellationToken).ConfigureAwait(false);
if (!exportResult.Success)
{
    return PublishExportResult.SeedExportFailure(exportResult.Message ?? string.Empty);
}
```
Any seed (re)write in the reconciler MUST go through `IContentKbOrchestrator.ExportIndexToFileAsync` / `ContentIndexExportRow.From()` — never a second seed serializer.

---

### `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` (MOD — stamp `SeedManaged = true`)

**Analog:** itself, `BuildRow` (`ContentKbSeedLoader.cs:68-89`):
```csharp
private static ContentSiteIndexRow BuildRow(ContentKbSeedEntry entry)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(entry.NaturalKeyType);
    ArgumentException.ThrowIfNullOrWhiteSpace(entry.NaturalKeyValue);

    return new ContentSiteIndexRow
    {
        Id = 0,
        Source = entry.Source,
        Title = entry.Title,
        VideoUrl = entry.VideoUrl,
        ArtifactPath = entry.ArtifactPath,
        PublishedUtc = entry.PublishedUtc,
        IndexedUtc = entry.IndexedUtc,
        ArchetypeTags = entry.ArchetypeTags,
        BracketTags = entry.BracketTags,
        CardCategoryTags = entry.CardCategoryTags,
        YoutubeVideoId = entry.NaturalKeyType == ContentSourceType.Youtube ? entry.NaturalKeyValue : null,
        RssGuid = entry.NaturalKeyType == ContentSourceType.Podcast ? entry.NaturalKeyValue : null,
        BodySha256 = entry.BodySha256,
    };
}
```
Add `SeedManaged = true,` (hardcoded literal in the C# caller, NOT derived from a `seedManaged` JSON field even though D-01 says the seed JSON also carries the field — per RESEARCH Pitfall 4, "this row is in the file we just loaded" already proves seed-managed regardless of what the JSON says). This call site feeds `UpsertRowPreservingVisibilityAsync` (`ContentKbSeedLoader.cs:61`) — confirm `seed_managed = EXCLUDED.seed_managed` (always-true-on-this-path) is documented in `UpsertPreservingVisibilitySql`'s SQL comment per the Pitfall-4 guidance above.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (MOD — register `sync.reconcile`)

**Analog:** itself, the `sync.directpush-gitbody` entry (`FeatureFlagCatalog.cs:97-99`):
```csharp
["sync.directpush-gitbody"] =
    "Serve a Content-KB body exclusively from the git-shipped /app tree, dropping the legacy " +
    "/data-SFTP-first overlay fallback. Off = today's byte-identical git-then-overlay serving.",
```
Add `["sync.reconcile"] = "..."` immediately after this entry, describing that it gates ONLY the destructive soft-hide Apply (per D-09) while detection/dry-run stay always-available.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (MOD — seed `sync.reconcile` OFF, both dialects)

**Analog:** itself, the `sync.directpush-gitbody` seed rows (`FeatureFlagStore.cs:198-236` Postgres, `238-276` SQLite):
```csharp
// Postgres (FeatureFlagStore.cs:234):
  ('sync.directpush-gitbody', FALSE)
ON CONFLICT (key) DO NOTHING;

// SQLite (FeatureFlagStore.cs:274):
  ('sync.directpush-gitbody', 0)
ON CONFLICT (key) DO NOTHING;
```
Add `('sync.reconcile', FALSE),` / `('sync.reconcile', 0),` as new rows immediately before the `sync.directpush-gitbody` line in each block (`ON CONFLICT (key) DO NOTHING` preserves any operator-set value on re-bootstrap — FLAG-01 contract, unchanged).

---

### Test files

**`ContentKbReconcileClassifierTests.cs`** — analog `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs` (full pattern read). Row-builder-with-named-optional-params fixture shape (`ContentSyncDiffClassifierTests.cs:12-38`):
```csharp
private static ContentSiteIndexRow Row(
    string? youtubeId = "yt-1",
    string? rssGuid = null,
    string title = "Title",
    string artifactPath = "content-kb/slug/yt-1.md",
    DateTimeOffset? indexedUtc = null,
    IReadOnlyList<string>? archetypeTags = null,
    IReadOnlyList<string>? bracketTags = null,
    IReadOnlyList<string>? cardCategoryTags = null,
    string approvalStatus = "approved",
    string? bodySha256 = null) =>
    new() { Id = 1, Source = "youtube", Title = title, /* ... */ };
```
`[Fact]` per discrepancy class, e.g. `Classify_KeyInProdOnly_IsMissingLocally` style naming (`ContentSyncDiffClassifierTests.cs:47-59`) — mirror for each of the 4 classes plus idempotency/determinism cases.

**`ProdContentReaderTests.cs` extension** — analog itself (`ProdContentReaderTests.cs:1-80`). `[PostgresFact]`-gated live round-trip tests (`ProdContentReaderTests.cs:54-78`) are the template for asserting `row.BodySha256`/`row.SeedManaged` round-trip through the extended `SelectAllSql`; the always-runs fail-closed connection-failure test (`ProdContentReaderTests.cs:35-50`) is the template for any new failure-mode assertion.

**`ContentKbReconcileStoreTests.cs`** — analog `ContentHarvestRunStore`'s existing test file's shape (schema-ensure + round-trip), covering idempotent upsert (re-run twice, assert row count unchanged), resolution-by-absence (seed a discrepancy, re-run reconcile without it present, assert `resolved_utc` set not null and row not deleted), and scope-tag isolation (two scope tags, resolve one scope, assert the other untouched).

**`ReconcileCoordinatorTests.cs`** — analog `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` for coordinator test shape (fakes for `IProdContentReader`/`IProdStoreFactory`, asserting `TryReadFlagAsync` tri-state drives Apply gating exactly as `VerifyAndPublishAsync` does).

## Shared Patterns

### Natural-key derivation (single source of truth)
**Source:** `DeckFlow.Core/Content/ContentNaturalKey.cs:35-53`
**Apply to:** `ContentKbReconcileClassifier` (seed-drift, seed-file parsing), `ContentKbReconcileOrchestrator` (prod-row indexing)
```csharp
public static bool TryDerive(ContentSiteIndexRow row, out (string Type, string Value) key)
{
    ArgumentNullException.ThrowIfNull(row);
    if (!string.IsNullOrWhiteSpace(row.YoutubeVideoId))
    {
        key = (ContentSourceType.Youtube, row.YoutubeVideoId!);
        return true;
    }
    if (!string.IsNullOrWhiteSpace(row.RssGuid))
    {
        key = (ContentSourceType.Podcast, row.RssGuid!);
        return true;
    }
    key = default;
    return false;
}
```
Composite dictionary keys MUST join with the U+0000 NULL separator (`$"{nk.Type}{nk.Value}"`), never a printable separator — the shipped anti-collision format (SYNC-05).

### Body hashing (single source of truth)
**Source:** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` (`ComputeBodySha256`, `AreContentEqual`)
**Apply to:** body-hash-mismatch discrepancy class in `ContentKbReconcileClassifier`
No second hash path — reuse verbatim per P89 D-01/D-02 and RESEARCH's "Don't Hand-Roll" table.

### Path-safety guard (single source of truth)
**Source:** `DeckFlow.Studio/Services/ArtifactPathSafety.cs:22-79` (`TryBuildContainedPath` / `IsSafeArtifactPath`)
**Apply to:** `ContentKbReconcileOrchestrator`'s published-orphan (row→file) AND file-orphan (file→row) checks
```csharp
public static bool TryBuildContainedPath(string root, string artifactPath, out string resolvedPath) { /* ... */ }
public static bool IsSafeArtifactPath(string artifactPath) { /* rejects rooted, requires content-kb/ prefix, rejects .. */ }
```

### Seed-export factory (single source of truth)
**Source:** `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs:53-90` (`From()`), consumed via `IContentKbOrchestrator.ExportIndexToFileAsync`
**Apply to:** Any seed (re)write in the reconciler/coordinator
```csharp
public static ContentIndexExportRow From(ContentSiteIndexRow row) { /* ... */ }
```

### Tri-state prod-flag read + fail-safe-to-refuse for destructive writes
**Source:** `DeckFlow.Studio/Services/ProdContentReader.cs:110-149` (`TryReadFlagAsync`), consumption pattern at `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs:296-352`
**Apply to:** `ReconcileCoordinator`'s Apply gating for `sync.reconcile`
```csharp
var flag = await _prodReader.TryReadFlagAsync(connectionString, "sync.reconcile", cancellationToken);
// D-09: only a confirmed `true` may proceed with the destructive soft-hide.
// Both `false` (definitive OFF) and `null` (indeterminate) refuse to apply.
if (flag != true) { /* refuse */ }
```

### Dialect-guarded additive DDL + idempotent NULL-gated backfill
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:132-150` (DDL), `:486-501` (backfill write template)
**Apply to:** `seed_managed` column + `SeedManagedBackfill`

### Local schema-ensure store (content-kb.db, 9-sibling pattern)
**Source:** `DeckFlow.Core/Content/ContentHarvestRunStore.cs` (full file), registration at `DeckFlow.Studio/Program.cs:103`
**Apply to:** `ContentKbReconcileStore`

### Optional-logger / NullLogger default convention
**Source:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs:104-105`
```csharp
_logger = logger ?? NullLogger<DirectPushCoordinator>.Instance;
```
**Apply to:** `ReconcileCoordinator`, `ContentKbReconcileOrchestrator`, `SeedManagedBackfill` (all take `ILogger<T>` as a required DI param per house convention except where a static pure class takes `ILogger?` optional, per `ContentSyncDiffClassifier`)

## No Analog Found

None — every file this phase creates or modifies has a direct, high-quality analog already in the codebase (RESEARCH.md: "every seam this phase needs ... already exists in the codebase from Phases 88-90"). The two genuinely NEW capabilities (full git-tree file enumeration for file-orphan detection, and the discrepancy-store resolution-by-absence query) have no prior Studio component to copy verbatim but are composed from directly-analogous existing pieces (`GitBodyCoverageAudit`'s prod-read+path-check loop; `FeatureFlagStore`'s `ON CONFLICT ... DO UPDATE` upsert shape) as documented above.

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Orchestration/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Studio/Services/`, `DeckFlow.Studio/ViewModels/`, `DeckFlow.Web/Services/Content/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Core.Tests/Content/`, `DeckFlow.Studio.Tests/Services/`, `DeckFlow.Studio.Tests/ViewModels/`
**Files scanned (full read):** `ContentSiteIndexStore.cs`, `ContentSyncDiffClassifier.cs`, `ContentKbOrphanScanner.cs`, `ContentHarvestRunStore.cs`, `ContentBodyHashBackfill.cs`, `ContentNaturalKey.cs`, `ContentIndexExportRow.cs`, `ArtifactPathSafety.cs`, `ProdContentReader.cs`, `GitBodyCoverageAudit.cs`, `ContentKbSeedLoader.cs`, `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `DirectPushCoordinator.cs`, `PublishCoordinator.cs` (15 files, ~4,600 lines) + partial reads of `ContentArtifactSpec.cs`, `ContentSyncDiffClassifierTests.cs`, `ProdContentReaderTests.cs`
**Pattern extraction date:** 2026-07-08
