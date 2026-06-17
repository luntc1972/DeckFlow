# Phase 43: Approval Status + Safe Upsert - Pattern Map

**Mapped:** 2026-06-13
**Files analyzed:** 6 (4 modified, 2 new test files)
**Analogs found:** 6 / 6

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — EnsureSchemaAsync ALTER block | migration | CRUD | same file, lines 57-82 (is_visible/is_evergreen/is_hidden ALTER blocks) | exact |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — UpsertContentColumnsOnlyAsync | service | CRUD | same file, `UpsertRowPreservingVisibilityAsync` lines 128-161 + `UpsertPreservingVisibilitySql` lines 658-702 | exact |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — GetApprovedRowsAsync | service | CRUD | same file, `GetPublishedRowsAsync` lines 208-237 | exact |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | service | request-response | same file, existing `UpsertRowPreservingVisibilityAsync` + `GetPublishedRowsAsync` signatures | exact |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` — two call-site switches | service | CRUD | same file, lines 610 and 1052-1068 | exact |
| `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs` (new) | test | CRUD | `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreVisibilityTests.cs` (entire file) | exact |

---

## Pattern Assignments

### 1. `EnsureSchemaAsync` — approval_status ALTER block

**Analog:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 57-82

**Verified line numbers:** is_visible block = 57-64; is_evergreen block = 66-73; is_hidden block = 75-82.
The new `approval_status` block goes after line 82 (immediately before `_schemaReady = true;` on line 84).

**Existing ALTER pattern to copy** (lines 75-82):
```csharp
if (!columns.Contains("is_hidden"))
{
    await using var addHidden = connection.CreateCommand();
    addHidden.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_hidden BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_hidden INTEGER NOT NULL DEFAULT 0;";
    await addHidden.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

**New block shape for approval_status:**
- Column type: `TEXT NOT NULL DEFAULT 'pending'` on both dialects (TEXT is universal; no BOOLEAN/INTEGER branch needed).
- After the ALTER succeeds, run a one-time grandfather UPDATE **in the same `if` branch** (so it only fires when the column is newly added, not on re-runs):
  ```sql
  UPDATE content_site_index
     SET approval_status = CASE WHEN is_visible THEN 'approved' ELSE 'pending' END
   WHERE approval_status = 'pending';
  ```
  On SQLite `is_visible` stores 0/1 integers — the CASE expression works the same way across dialects.

**Full new block shape:**
```csharp
if (!columns.Contains("approval_status"))
{
    await using var addStatus = connection.CreateCommand();
    addStatus.CommandText =
        "ALTER TABLE content_site_index ADD COLUMN approval_status TEXT NOT NULL DEFAULT 'pending';";
    await addStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

    // Why: grandfather existing rows — visible rows become approved so the next seed export
    // does not silently drop already-published content (D-01).
    await using var backfill = connection.CreateCommand();
    backfill.CommandText = """
        UPDATE content_site_index
           SET approval_status = CASE WHEN is_visible THEN 'approved' ELSE 'pending' END
         WHERE approval_status = 'pending';
        """;
    await backfill.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

---

### 2. `UpsertContentColumnsOnlyAsync` — new method on ContentSiteIndexStore

**Analog:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 128-161 (method body) + lines 658-702 (SQL constant `UpsertPreservingVisibilitySql`)

**Existing method shape to copy** (lines 128-161, slightly condensed):
```csharp
public async Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(row);
    ArgumentException.ThrowIfNullOrWhiteSpace(row.Source);
    ArgumentException.ThrowIfNullOrWhiteSpace(row.Title);
    ArgumentException.ThrowIfNullOrWhiteSpace(row.VideoUrl);
    ArgumentException.ThrowIfNullOrWhiteSpace(row.ArtifactPath);
    ArgumentNullException.ThrowIfNull(row.ArchetypeTags);
    ArgumentNullException.ThrowIfNull(row.BracketTags);
    ArgumentNullException.ThrowIfNull(row.CardCategoryTags);

    var naturalKey = GetNaturalKey(row);
    ValidateArtifactPath(row.ArtifactPath);
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    await using var command = connection.CreateCommand();
    command.CommandText = UpsertPreservingVisibilitySql;
    RelationalDatabaseConnection.AddParameter(command, "@source", row.Source);
    // ... (one AddParameter per bound column)
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

**Existing SQL constant to copy** (`UpsertPreservingVisibilitySql`, lines 658-702):

Key insight from line 699-701 — the existing "preserve" pattern uses `content_site_index.<col>` on the right-hand side of `DO UPDATE SET` to keep the stored value:
```sql
is_visible         = content_site_index.is_visible,
is_hidden          = content_site_index.is_hidden,
is_evergreen       = content_site_index.is_evergreen;
```

**New SQL constant shape — `UpsertContentColumnsOnlySql`:**

INSERT side: same content columns as `UpsertPreservingVisibilitySql`, but do NOT include `is_visible`, `is_hidden`, `is_evergreen` in the column list (rely on column defaults). DO include `approval_status = 'pending'` explicitly (so a brand-new row always starts pending regardless of the column default):
```sql
INSERT INTO content_site_index (
  source, title, video_url, artifact_path,
  published_utc, indexed_utc,
  archetype_tags, bracket_tags, card_category_tags,
  natural_key_type, natural_key_value,
  approval_status)
VALUES (
  @source, @title, @videoUrl, @artifactPath,
  @publishedUtc, @indexedUtc,
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
  -- is_visible, is_hidden, is_evergreen, approval_status are NOT listed here
  -- so they are never touched on UPDATE (D-03, D-04)
```

**Method parameters:** identical to `UpsertRowPreservingVisibilityAsync` — only `row` and `cancellationToken`. No `@isVisible`, `@isHidden`, `@isEvergreen` parameters needed (they are omitted from the INSERT column list and omitted from DO UPDATE SET).

---

### 3. `GetApprovedRowsAsync` — new filtered read method

**Analog:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 208-237 (`GetPublishedRowsAsync`)

**Existing method to copy verbatim, changing only the WHERE clause** (lines 208-237):
```csharp
public async Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
{
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT id,
               source,
               title,
               video_url,
               artifact_path,
               published_utc,
               indexed_utc,
               archetype_tags,
               bracket_tags,
               card_category_tags,
               natural_key_type,
               natural_key_value,
               is_visible,
               is_hidden,
               is_evergreen
          FROM content_site_index
         WHERE is_visible = @visible
         ORDER BY source, title, id;
        """;
    RelationalDatabaseConnection.AddParameter(command, "@visible", FormatVisibility(true));

    return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
}
```

**New method shape — `GetApprovedRowsAsync`:**
- Replace `WHERE is_visible = @visible` with `WHERE approval_status = 'approved'`
- Remove the `@visible` parameter bind
- Keep the SELECT column list identical (the `approval_status` column is a filter concern, not a returned projection — see D-07 and the note on `ContentSiteIndexRow` below)
- Keep `ORDER BY source, title, id` (D-07: preserve row order for seed JSON stability)

Note: `ReadRow()` at line 557-587 reads columns by ordinal (0-14). Adding `approval_status` to the SELECT list would shift ordinals and break `ReadRow()`. Since `ContentSiteIndexRow` gains an `ApprovalStatus` property, the full SELECT list and `ReadRow()` must both be updated consistently across all SELECT queries. See the `ContentSiteIndexRow` note below.

---

### 4. Interface extensions on `IContentSiteIndexStore`

**Analog:** `DeckFlow.Core/Content/IContentSiteIndexStore.cs` lines 26-28 and 43-47

**Existing signatures to mirror:**
```csharp
// line 26-28
Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

// line 43-47
Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default);
```

**New signatures to add (same shape):**
```csharp
/// <summary>
/// Inserts or updates content/nav columns only, never touching admin-set fields
/// (is_visible, is_hidden, is_evergreen, approval_status) on existing rows.
/// New rows are inserted with approval_status='pending'.
/// </summary>
Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

/// <summary>
/// Gets site-index rows where approval_status='approved', ordered for deterministic export.
/// </summary>
Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default);
```

Place `UpsertContentColumnsOnlyAsync` after `UpsertRowPreservingVisibilityAsync` (line 28).
Place `GetApprovedRowsAsync` after `GetPublishedRowsAsync` (line 47).

---

### 5. `ContentSiteIndexRow` — ApprovalStatus property

**Analog:** `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` lines 136-142 (the three bool admin fields)

**Existing admin field pattern to follow:**
```csharp
// line 136-138
public bool IsVisible { get; init; }

// line 139-140
public bool IsHidden { get; init; }

// line 141-142
public bool IsEvergreen { get; init; }
```

**New property shape:**
```csharp
/// <summary>
/// Approval workflow status: 'pending' (awaiting review), 'approved' (publish-eligible),
/// or 'rejected' (suppressed from export). Defaults to 'pending' for new distilled rows.
/// </summary>
public string ApprovalStatus { get; init; } = "pending";
```

Place after `IsEvergreen` (line 142). Not `required` — the default `"pending"` keeps existing test row factories compiling without modification.

**CRITICAL: do not add `ApprovalStatus` to `ContentIndexExportRow`.** That class is a separate projection (CONTEXT D-07 + scope fence). The Phase 42 golden test pins the export JSON byte-shape; `approval_status` must NOT appear in the exported JSON.

**ReadRow() update:** `ReadRow()` at lines 557-587 reads by ordinal. When `approval_status` is added to every SELECT list at ordinal 15, `ReadRow()` must read `reader.GetString(15)` and assign to `ApprovalStatus`. The existing ordinals 0-14 are unchanged.

---

### 6. Orchestrator call-site switches in `ContentKbOrchestrator.cs`

**Analog call sites — exact current code:**

**Export read** (line 610):
```csharp
var rows = await _indexStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
```
Switch to:
```csharp
var rows = await _indexStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
```

**Distill upsert** (lines 1052-1068):
```csharp
await _indexStore.UpsertRowAsync(
    new ContentSiteIndexRow
    {
        Id = 0,
        Source = source.DisplayName,
        Title = video.Title,
        VideoUrl = video.VideoUrl,
        ArtifactPath = ContentArtifactWriter.ComputeRelativeArtifactPath(source.SourceSlug, naturalKey),
        PublishedUtc = video.PublishedUtc,
        IndexedUtc = generatedUtc,
        ArchetypeTags = archetypeTags,
        BracketTags = bracketTags,
        CardCategoryTags = cardCategoryTags,
        YoutubeVideoId = video.YoutubeVideoId,
        RssGuid = video.RssGuid,
    },
    cancellationToken).ConfigureAwait(false);
```
Switch `UpsertRowAsync` to `UpsertContentColumnsOnlyAsync`. No other change to the row constructor. `ApprovalStatus` does not need to be set explicitly — the new SQL inserts `'pending'` for new rows and the method never writes `approval_status` on UPDATE.

---

## Shared Patterns

### Argument validation on upsert methods
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 95-103
**Apply to:** `UpsertContentColumnsOnlyAsync`
```csharp
ArgumentNullException.ThrowIfNull(row);
ArgumentException.ThrowIfNullOrWhiteSpace(row.Source);
ArgumentException.ThrowIfNullOrWhiteSpace(row.Title);
ArgumentException.ThrowIfNullOrWhiteSpace(row.VideoUrl);
ArgumentException.ThrowIfNullOrWhiteSpace(row.ArtifactPath);
ArgumentNullException.ThrowIfNull(row.ArchetypeTags);
ArgumentNullException.ThrowIfNull(row.BracketTags);
ArgumentNullException.ThrowIfNull(row.CardCategoryTags);
```

### EnsureSchemaAsync + OpenConnectionAsync guard
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 106-109
**Apply to:** all new public methods
```csharp
await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
```

### ConfigureAwait(false) on every await
**Source:** entire `ContentSiteIndexStore.cs` — no exceptions
**Apply to:** all new async code in store and orchestrator

### ReadRowsAsync helper
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — used by `GetPublishedRowsAsync` (line 236) and `GetAllRowsAsync` (line 266)
**Apply to:** `GetApprovedRowsAsync`
```csharp
return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
```

---

### Test fixture pattern
**Source:** `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreVisibilityTests.cs` (entire file, 324 lines)

**Setup pattern** (lines 14-33):
```csharp
public sealed class ContentSiteIndexStoreVisibilityTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreVisibilityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-visibility-{Guid.NewGuid():N}.db");
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
```

**Legacy schema test pattern** (lines 166-197 + 211-274) — critical for the column-present migration test:
- `CreateLegacySchemaAsync()` hand-builds the old DDL (without the new column) and inserts a seed row
- Calls `EnsureSchemaAsync()` to trigger migration
- Asserts `ColumnExistsAsync("approval_status")` is now true
- Asserts seed row has the grandfathered value (visible → `'approved'`, not-visible → `'pending'`)

**`ColumnExistsAsync` helper** (lines 277-293) — PRAGMA table_info:
```csharp
private async Task<bool> ColumnExistsAsync(string columnName)
{
    await using var connection = await RelationalDatabaseConnection
        .FromSqlitePath(_dbPath)
        .OpenConnectionAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA table_info(content_site_index);";
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

**`CreateYoutubeRow` factory** (lines 303-323) — the new test class needs its own copy of this static helper, matching `ContentSiteIndexStoreVisibilityTests.CreateYoutubeRow`.

**Private SQL reflection helper** (lines 296-301) — for DDL shape assertions:
```csharp
private static string GetPrivateSql(string fieldName)
{
    var field = typeof(ContentSiteIndexStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(field);
    return Assert.IsType<string>(field!.GetRawConstantValue());
}
```

**New test file to create:** `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs`

Required tests (mirror the visibility test pattern for each new behavior):
1. `EnsureSchemaAsync_AddsApprovalStatusColumn_ToLegacySchema` — uses `CreateLegacySchemaAsync` (without approval_status), calls `EnsureSchemaAsync`, asserts `ColumnExistsAsync("approval_status")` is true.
2. `EnsureSchemaAsync_Grandfather_SetsApprovedForVisibleRows_PendingForOthers` — insert one visible row + one invisible row via legacy schema, migrate, assert `approval_status` values.
3. `UpsertContentColumnsOnlyAsync_NewRow_LandsAsPending` — upsert a new row, assert `ApprovalStatus == "pending"`.
4. `UpsertContentColumnsOnlyAsync_ExistingRow_PreservesApprovalStatus` — upsert, set status to `'approved'` via direct SQL, re-upsert with changed title, assert status still `'approved'` and title updated.
5. `UpsertContentColumnsOnlyAsync_PreservesAllAdminFields` — upsert, set `is_visible=true`, `is_hidden=false`, `is_evergreen=true`, `approval_status='approved'`, re-upsert, assert all four preserved.
6. `GetApprovedRowsAsync_ReturnsOnlyApprovedRows` — insert `approved`, `pending`, `rejected` rows, assert only `approved` in result; assert count and natural key match.
7. `CreateTableDdl_IncludesApprovalStatusDefault` — reflection-based check that both DDL strings contain `approval_status TEXT NOT NULL DEFAULT 'pending'`.

Postgres column-presence test: skip (no PG test harness in CI — note explicitly in test class XML doc or `[Fact(Skip = "...")]`).

---

## No Analog Found

None. All new files/methods have direct analogs in the same store/test files.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Orchestration/`, `DeckFlow.Core.Tests/`, `DeckFlow.Core.Tests/Content/`
**Files scanned:** 7
**Pattern extraction date:** 2026-06-13

### Line number verification (CONTEXT.md vs actual)

| CONTEXT.md citation | Actual lines |
|---|---|
| is_visible ALTER @57-64 | 57-64 — CONFIRMED |
| is_evergreen ALTER @66-73 | 66-73 — CONFIRMED |
| is_hidden ALTER @75-82 | 75-82 — CONFIRMED |
| GetPublishedRowsAsync @47 | 208 — DRIFTED (CONTEXT cited interface line 47; implementation is lines 208-237) |
| GetAllRowsAsync @240 | 240 — CONFIRMED |
| UpsertRowAsync @1052 | 1052 — CONFIRMED |
| ExportIndexAsync GetAllRowsAsync @610 | 610 — CONFIRMED |
