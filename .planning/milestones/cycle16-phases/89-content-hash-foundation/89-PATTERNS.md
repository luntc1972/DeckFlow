# Phase 89: Content-Hash Foundation - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 9 (7 modified, 1 new helper location decision, 1 startup wiring change)
**Analogs found:** 9 / 9 — every file in this phase is itself the closest analog to its own extension point; no cross-domain substitution was needed.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` | utility (pure transform) | transform | itself — extend `BuildSignature` | exact |
| `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` | service (pure classifier) | transform/CRUD-diff | itself — replace `Fingerprint` w/ unified signature | exact |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | model + store (persistence) | CRUD | itself — `EnsureSchemaAsync` ALTER-backfill block (lines 87-130) is the exact DDL pattern to mirror for `body_sha256` | exact |
| `DeckFlow.Web/Controllers/ContentKbController.cs` | controller | request-response | itself — `Detail` action's `SplitHeader` call at line 119 | exact |
| `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` / publish-compute call site (`ContentKbOrchestrator.cs` ~1330-1355) | service (file I/O + CRUD) | file-I/O → CRUD | `ContentKbOrchestrator.DistillVideoAsync` body around `WriteFile`/`WritePromptFile`/`UpsertContentColumnsOnlyAsync` | exact |
| New shared body-hash helper (Claude's discretion — recommend folding into `ContentSiteIndexContentSignature`) | utility (pure transform) | transform | `ContentArtifactParser.SplitHeader` (existing pure static helper) as the structural template | role-match |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` | service (consumer of signature) | CRUD-diff | itself — `ClassifyDiff` at line 166 (`ContentSiteIndexContentSignature.AreContentEqual`) | exact |
| `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` | service (consumer of classifier) | CRUD-diff | itself — line 119 (`ContentSyncDiffClassifier.Classify`) | exact |
| `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` + seed export path (`ContentKbCommandRunners.cs`/CLI export, `PublishCoordinator.cs`) | service (file I/O, seed JSON) | file-I/O / CRUD | itself — `ContentKbSeedEntry` record + `BuildRow` (lines 68-113) | exact |
| Web startup backfill wiring (`DeckFlow.Web/Program.cs` ~264-265) | config/composition-root | batch | itself — the existing `EnsureSchemaAsync()` → `LoadIfPresentAsync()` sequence | exact |

## Pattern Assignments

### `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` (utility, transform) — D-03

**Analog:** itself (extend in place)

**Current signature-building pattern** (lines 52-93, full file already read):
```csharp
public static string BuildSignature(ContentSiteIndexRow row)
{
    ArgumentNullException.ThrowIfNull(row);

    var sb = new StringBuilder();

    // source
    sb.Append(row.Source);
    sb.Append(FieldDelimiter);
    // ... title, video_url, artifact_path, published_utc, indexed_utc ...
    sb.Append(ContentArtifactSpec.SerializeTags(row.ArchetypeTags));
    sb.Append(FieldDelimiter);
    sb.Append(ContentArtifactSpec.SerializeTags(row.BracketTags));
    sb.Append(FieldDelimiter);
    sb.Append(ContentArtifactSpec.SerializeTags(row.CardCategoryTags));

    return sb.ToString();
}
```

**Extension point (D-03):** Append one more `sb.Append(FieldDelimiter); sb.Append(row.BodySha256 ?? NullShaSentinel);` field at the end of `BuildSignature`, after the `card_category_tags` append, following the exact `FieldDelimiter`-then-value idiom already used for every other field. Add a new sentinel constant analogous to `NullDateSentinel` (line 43) for a null/legacy hash, e.g. `NullShaSentinelConstant = "(nohash)"` — must not collide with a real 64-hex-char SHA-256 string. `AreContentEqual` (lines 102-103) needs no change; it already just compares two `BuildSignature` outputs by ordinal string equality.

**Naming convention to follow:** the row property should be `BodySha256` (PascalCase, matches `PublishedUtc`/`IndexedUtc` neighbors) on `ContentSiteIndexRow` in `ContentArtifactSpec.cs` (record properties block, lines 107-167) — add it as `public string? BodySha256 { get; init; }` next to `ApprovalStatus`, following the `sealed record` + nullable-init convention already used throughout that file (e.g. line 130 `PublishedUtc`, line 133 `PushedToProdUtc`).

---

### `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` (service, pure classifier) — D-03/D-04

**Analog:** itself

**Fingerprint to delete** (lines 136-143):
```csharp
private static string Fingerprint(ContentSiteIndexRow row) =>
    string.Join(
        '',
        row.Title,
        row.ArtifactPath,
        string.Join(',', row.ArchetypeTags),
        string.Join(',', row.BracketTags),
        string.Join(',', row.CardCategoryTags));
```

**Call site to switch onto the unified signature** (equal-timestamp branch, lines 59-67):
```csharp
else if (!string.Equals(Fingerprint(prod), Fingerprint(local), StringComparison.Ordinal))
{
    // Equal timestamps but different content — diverged without a clear direction.
    entries.Add(BuildEntry(SyncDiffKind.Diverged, prod, local, localIsNewer: false));
}
```
Replace `Fingerprint(prod)`/`Fingerprint(local)` compare with `!ContentSiteIndexContentSignature.AreContentEqual(prod, local)` — this is the exact call `DirectPushCoordinator.ClassifyDiff` (line 166) already makes, so after this change both classifiers call the identical comparator (SYNC-02 invariant: one signature, one home). D-04 requires the surrounding UTC-direction branches (`prodUtc > localUtc`, `localUtc > prodUtc`, lines 55-61) to stay untouched — only the equal-timestamp tie-breaker (line 63) changes.

---

### `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (model + store, CRUD) — D-09

**Analog:** itself — the `is_hidden`/`approval_status`/`pushed_to_prod_utc` ALTER-backfill blocks are the exact template

**Dialect-guarded idempotent ALTER pattern to mirror** (lines 106-113, `is_hidden` example — shortest/cleanest of the five):
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
For `body_sha256` (nullable TEXT, no dialect-specific type divergence needed since both SQLite and Postgres use `TEXT NULL`):
```csharp
if (!columns.Contains("body_sha256"))
{
    await using var addBodySha256 = connection.CreateCommand();
    addBodySha256.CommandText = "ALTER TABLE content_site_index ADD COLUMN body_sha256 TEXT NULL;";
    await addBodySha256.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```
Placed inside the same `if (!_ensureSchemaEnabled) return;` / `_schemaReady` gate at lines 71-80, alongside the other four column-backfill blocks (after line 130, before the `_schemaReady = true;` at line 146). **This is the ONLY place the ALTER should be issued (D-09)** — `IProdStoreFactory.Create` (`DeckFlow.Studio/Services/IProdStoreFactory.cs:32`) constructs prod stores with `ensureSchemaEnabled: false`, so Studio never runs this code path against prod; the web app's own `EnsureSchemaAsync()` call at `Program.cs:264` is what actually issues it.

**`CREATE TABLE` DDL to extend** (both dialects, lines 1071-1114) — add `body_sha256 TEXT NULL,` to both `PostgresCreateTableSql` and `SqliteCreateTableSql` (fresh-DB path; the ALTER above is the existing-DB backfill path, both must agree on the final schema):
```csharp
private const string SqliteCreateTableSql = """
    CREATE TABLE IF NOT EXISTS content_site_index (
      id                 INTEGER PRIMARY KEY AUTOINCREMENT,
      ...
      approval_status    TEXT NOT NULL DEFAULT 'pending',
      UNIQUE (natural_key_type, natural_key_value)
    );
    """;
```

**`ContentSiteIndexRowData` internal DTO to extend** (lines 1116-1135):
```csharp
private sealed class ContentSiteIndexRowData
{
    public long Id { get; init; }
    public required string Source { get; init; }
    ...
    public required string ApprovalStatus { get; init; }
}
```
Add `public string? BodySha256 { get; init; }` following the nullable-optional-column convention already used for `PublishedUtc`/`PushedToProdUtc` (not `required`, since legacy rows are null pre-backfill).

**Read/write plumbing to touch (mechanical, many call sites — same shape at each):**
- Every `SELECT ... FROM content_site_index` (7 occurrences: `GetByNaturalKeyAsync` ~223-243, `GetPublishedRowsAsync` ~257-278, `GetApprovedRowsAsync` ~292-311, `GetAllRowsAsync` ~325-343, `GetByIdAsync` ~357-376, `GetPublishedByIdAsync`, plus one more) needs `body_sha256` added to the column list, using the exact same one-column-per-line style already there.
- `ToContentSiteIndexRow(ContentSiteIndexRowData row)` mapper (lines 905-937) needs `BodySha256 = row.BodySha256,` added, following the same 1:1 property-copy convention as every other field.
- `BuildUpsertParameters` (lines 780-798) needs `parameters.Add("bodySha256", row.BodySha256);` following the exact `parameters.Add("approvalStatus", row.ApprovalStatus);` idiom at line ~797 (same comment-then-add shape).
- `UpsertContentColumnsOnlySql` (lines 1028-1068, the sql the publish path calls) needs `body_sha256` added to both the INSERT column list and the `ON CONFLICT ... DO UPDATE SET` clause (mirroring `approval_status = EXCLUDED.approval_status;` at line 1065) — this is the one upsert variant D-01's publish-compute actually calls (`ContentKbOrchestrator.cs` ~1350), so it is the highest-priority SQL text to update; `UpsertSql` and `UpsertPreservingVisibilitySql` are lower priority (used by the seed loader / legacy paths) but should stay consistent for schema symmetry.

---

### `DeckFlow.Web/Controllers/ContentKbController.cs` (controller, request-response) — D-05/D-07

**Analog:** itself — the render path already does the exact split the guard needs to reuse

**Existing split-then-render pattern** (lines 118-126):
```csharp
var raw = await System.IO.File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);
var (_, body) = ContentArtifactParser.SplitHeader(raw);
var renderedHtml = new HtmlString(Markdown.ToHtml(body, Pipeline));

// Prefer the baked sibling prompt (written at distill time) when present; otherwise
// reconstruct it from the notes so pre-bake artifacts still copy a framed, paste-ready
// prompt. Both paths yield identical output for the same notes. See ContentKbPromptResolver.
var copyPrompt = await ResolveCopyPromptAsync(row, raw, cancellationToken).ConfigureAwait(false);
return View("Detail", BuildDetailModel(row, renderedHtml, copyPrompt, artifactUnavailable: false));
```

**Insertion point (D-05/D-07):** immediately after `var (_, body) = ContentArtifactParser.SplitHeader(raw);` — compute the on-disk hash from `body` (normalize per D-02: decode UTF-8, normalize `\r\n`→`\n`, then SHA-256), compare against `row.BodySha256`. On mismatch OR null/legacy `row.BodySha256`, log a structured warning and continue serving (fail-open, matching the `MissingFile` branch's existing style at lines 112-116):
```csharp
if (resolution == ContentKbArtifactResolution.MissingFile)
{
    _logger.LogWarning("Content KB artifact file was unavailable for row {ContentKbRowId}.", row.Id);
    return View("Detail", BuildDetailModel(row, new HtmlString(string.Empty), string.Empty, artifactUnavailable: true));
}
```
Use the identical `_logger.LogWarning("...", row.Id)` structured-template idiom (never string interpolation, per project Logging convention) for the hash-mismatch warning, e.g. `_logger.LogWarning("Content KB body hash mismatch for row {ContentKbRowId}: stored={StoredHash} computed={ComputedHash}", row.Id, row.BodySha256 ?? "(none)", computedHash);`. No new dependency needed — `System.Security.Cryptography.SHA256` is BCL. The guard MUST call `ContentArtifactParser.SplitHeader` — never re-implement frontmatter stripping — so the render-side hash and the publish-side hash are provably computed over the same bytes (D-01).

**Constructor / DI shape to preserve** (lines 32-44) — `ArgumentNullException.ThrowIfNull` guards on every ctor param, `ILogger<ContentKbController>` injected — no new dependency required for the guard since hashing is pure BCL.

---

### Publish-time hash compute — `ContentArtifactWriter.cs` / `ContentArtifactSpec.cs` + call site `ContentKbOrchestrator.cs` (service, file-I/O → CRUD) — D-01/D-02

**Analog:** the existing distill call site is the exact insertion point, not a different file

**Existing publish-write-then-split-then-upsert sequence** (`DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs`, ~lines 1330-1362):
```csharp
var artifactText = ContentArtifactWriter.ToText(
    metadata,
    summary.Summary,
    clips.Clips.Select(clip => (clip.TimestampSeconds, clip.Excerpt)).ToArray());
ContentArtifactWriter.WriteFile(_artifactRoot, source.SourceSlug, naturalKey, artifactText);

// Bake the paste-ready AI prompt into a sibling {id}.prompt.md at distill time so the
// Studio review queue and the public copy button serve the exact shipped prompt without
// re-framing. Reconstruct the body from the just-written artifact text (same SplitHeader
// path as the serve-time fallback) so a baked prompt is byte-identical to a reconstructed
// one for the same notes.
var (_, promptBody) = ContentArtifactParser.SplitHeader(artifactText);
var promptText = ContentKbPromptWrapper.Wrap(video.Title, source.DisplayName, video.VideoUrl, promptBody);
if (!string.IsNullOrWhiteSpace(promptText))
{
    ContentArtifactWriter.WritePromptFile(_artifactRoot, source.SourceSlug, naturalKey, promptText);
}
await _indexStore.UpsertContentColumnsOnlyAsync(
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

**Why this is THE insertion point:** `ContentArtifactParser.SplitHeader(artifactText)` is already called here (`var (_, promptBody) = ...`) for the prompt-baking purpose — the comment even says "same SplitHeader path as the serve-time fallback." D-01 requires publish-compute and render-guard to call the identical split; `promptBody` at this call site IS that identical body. Compute the hash from `promptBody` (or re-derive via the same call if the planner prefers not to couple to the prompt-bake variable name), normalize per D-02, and add `BodySha256 = computedHash,` to the `new ContentSiteIndexRow { ... }` literal passed to `UpsertContentColumnsOnlyAsync`.

**Recommended home for the shared hash helper** (Claude's discretion, CONTEXT.md line 42): a static method on `ContentSiteIndexContentSignature`, e.g. `public static string ComputeBodySha256(string rawArtifactText)` that internally calls `ContentArtifactParser.SplitHeader` + normalizes + hashes — so both the controller's render-guard and the orchestrator's publish-compute call ONE method instead of duplicating the normalize-then-hash sequence. This keeps "one signature surface" (D-03's stated invariant) extended to "one hash surface." Structural template for a new pure static helper method: `ContentArtifactParser.SplitHeader` itself (lines 13-46) — single static method, `ArgumentNullException.ThrowIfNull` guard at top, pure string manipulation, XML doc with `<param>`/`<returns>`.

---

### `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (service, CRUD-diff consumer) — D-03

**Analog:** itself — no logic change needed, only benefits automatically once the signature is extended

**Current call site** (line 166):
```csharp
else if (!ContentSiteIndexContentSignature.AreContentEqual(row, prodRow))
{
    updatedCount++;
    publishRows.Add(row);
    diffRows.Add(new DirectPushDiffRow(row.Title, keyType, key, false, Path.GetFileName(row.ArtifactPath)));
}
```
No code change required here — once `BuildSignature`/`AreContentEqual` include `body_sha256` (D-03), this comparison automatically becomes body-hash-aware. Confirms the "one signature, one home" design: DirectPush already depends on the extension point, so extending it there is sufficient.

---

### `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` (service, CRUD-diff consumer) — D-03/D-04

**Analog:** itself

**Current call site** (line 119):
```csharp
var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows, _logger)
    .Select(e => e with { ArtifactDownloaded = availableSet.Contains(e.ArtifactPath) })
    .ToList();
```
No code change required — once `ContentSyncDiffClassifier.Classify`'s equal-timestamp branch switches to `ContentSiteIndexContentSignature.AreContentEqual` (D-03/D-04), Pull's diff automatically becomes body-hash-aware too. Same "one signature, one home" pay-off as DirectPush.

---

### Seed JSON (`index-seed.json`) export/load path (service, file-I/O) — D-09

**Analog:** `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` (load side, full file already read) — the private `ContentKbSeedEntry` record is the exact shape to extend

**Load-side record + mapper to extend** (lines 68-113):
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
    };
}

private sealed record ContentKbSeedEntry
{
    public required string NaturalKeyType { get; init; }
    ...
    public required IReadOnlyList<string> CardCategoryTags { get; init; }
}
```
Add `public string? BodySha256 { get; init; }` to `ContentKbSeedEntry` (nullable — legacy seed entries predating this phase won't have it) and `BodySha256 = entry.BodySha256,` to `BuildRow`'s `new ContentSiteIndexRow { ... }` literal. `JsonSerializerOptions` at lines 12-16 already use `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` + `PropertyNameCaseInsensitive = true`, so the JSON field auto-serializes as `bodySha256` — no explicit `[JsonPropertyName]` attribute needed, consistent with every other property in the record.

**Export side (write path):** the exporter lives in `DeckFlow.CLI/ContentKbCommandRunners.cs` (`--output` defaults to `content-kb/seed/index-seed.json` per `DeckFlow.CLI/Program.cs:100`) and `DeckFlow.Studio/ViewModels/PublishCoordinator.cs` (`SeedRelative = "content-kb/seed/index-seed.json"`, line 26). Both read `ContentSiteIndexRow` rows and serialize to the seed JSON shape — once `ContentSiteIndexRow.BodySha256` exists (from the store-side extension above) and the exporter's own DTO/anonymous-object projection includes it, the field round-trips automatically. Locate the exact exporter projection via the golden test `DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs` (asserts against `Fixtures/index-seed.golden.json`) — that golden fixture will need `body_sha256`/`bodySha256` added alongside the update, and `ContentIndexSeedWriteTests.cs` covers the writer itself.

---

### Web startup backfill wiring — `DeckFlow.Web/Program.cs` (composition root, batch) — D-08

**Analog:** itself — the existing schema-ensure + seed-load startup sequence is the exact place a backfill pass slots in

**Existing sequence** (lines 264-265):
```csharp
await app.Services.GetRequiredService<DeckFlow.Core.Content.IContentSiteIndexStore>().EnsureSchemaAsync();
await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
```
D-08's one-time deterministic backfill (web-app side, prod) should run as a third step immediately after these two — schema (including the new `body_sha256` column) must exist, and the seed load must have populated/refreshed rows, before backfill can safely `UPDATE ... WHERE body_sha256 IS NULL` against every row's on-disk `.md`. Enumerate rows via `IContentSiteIndexStore.GetAllRowsAsync` (already exists, unfiltered — same method Studio's admin/Pull paths use), resolve each `ArtifactPath` via `ContentKbArtifactPathResolver.TryResolveExistingArtifact` (same resolver `ContentKbController` already injects), read + `SplitHeader` + hash (reusing the same shared helper as D-01/D-02), and persist via a narrow new store method or by reusing `UpsertContentColumnsOnlyAsync` guarded to skip when `BodySha256` is already non-null (idempotent UPDATE-where-null, per CONTEXT.md's stated preference for "the smaller-surface option that stays safe on re-run").

**Studio/local backfill side (D-08, Studio/publish path):** per CONTEXT.md discretion, piggyback the existing publish/upsert path (`ContentKbOrchestrator.DistillVideoAsync` already computes+stores the hash for newly-distilled videos going forward per D-01) rather than a discrete one-shot command, UNLESS existing pre-phase-89 local rows need a one-time catch-up pass — in which case a small CLI command analogous to the existing `ContentKbCommandRunners` command shapes (e.g. the seed-export runner at `ContentKbCommandRunners.cs:338`) is the structural template to follow.

## Shared Patterns

### Dialect-guarded idempotent ALTER (SQLite + Postgres)
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 87-130 (`EnsureSchemaAsync`)
**Apply to:** the `body_sha256` column addition (D-09)
```csharp
var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken).ConfigureAwait(false);
if (!columns.Contains("<column_name>"))
{
    await using var addColumn = connection.CreateCommand();
    addColumn.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN <column_name> <PG_TYPE> NULL;"
        : "ALTER TABLE content_site_index ADD COLUMN <column_name> <SQLITE_TYPE> NULL;";
    await addColumn.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```
Gate every ALTER behind `if (!_ensureSchemaEnabled) return;` (line 75) — this is what keeps Studio's prod-pointed stores DDL-free (P88 D-10, `IProdStoreFactory.Create` at `DeckFlow.Studio/Services/IProdStoreFactory.cs:32`).

### Structured logging, never string interpolation
**Source:** `DeckFlow.Web/Controllers/ContentKbController.cs:114` and project CONVENTIONS.md `## Logging`
**Apply to:** the render-guard's hash-mismatch/missing-hash warning (D-05)
```csharp
_logger.LogWarning("Content KB artifact file was unavailable for row {ContentKbRowId}.", row.Id);
```
Use named placeholders (`{ContentKbRowId}`, `{StoredHash}`, `{ComputedHash}`) — never `$"..."` interpolation — matching every other logger call site in this controller and across the codebase.

### One signature, one home (SYNC-02 invariant)
**Source:** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` (canonical) vs. the now-deleted `ContentSyncDiffClassifier.Fingerprint`
**Apply to:** `ContentSyncDiffClassifier.Classify`, `DirectPushCoordinator.ClassifyDiff`, `PullFromProdCoordinator` — all three MUST call `ContentSiteIndexContentSignature.BuildSignature`/`AreContentEqual` and nothing else. A test should assert (via reflection or a simple `grep`-style source scan, matching the project's existing `CarveOutGuard`-style invariant tests) that no second signature-building method exists in `DeckFlow.Core.Content`.

### Fail-open + structured log on data-integrity guard
**Source:** P88's serve-side approval filter (`ContentKbController.Detail`, `GetPublishedByIdAsync` at `ContentSiteIndexStore.cs:383-403`) — same defense-in-depth posture, applied to a different guard
**Apply to:** the D-05 body-hash render guard — never throw, never 404, never blank the body on mismatch; log and continue serving.

## No Analog Found

None. Every file in scope for Phase 89 is itself the closest and only meaningful analog for its own extension — this phase is entirely in-place extension of existing, single-purpose files, not new architectural surface.

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Services/Content/`, `DeckFlow.Web/Program.cs`, `DeckFlow.Studio/ViewModels/`, `DeckFlow.Studio/Services/`, `DeckFlow.CLI/`
**Files scanned:** `ContentSiteIndexContentSignature.cs`, `ContentSyncDiffClassifier.cs`, `ContentNaturalKey.cs`, `ContentSiteIndexStore.cs` (targeted ranges: 1-150, 160-400, 780-1136), `ContentArtifactSpec.cs`, `ContentArtifactParser.cs`, `ContentArtifactWriter.cs`, `ContentKbController.cs`, `ContentKbSeedLoader.cs`, `ContentKbArtifactPathResolver.cs` (1-90), `ContentKbOrchestrator.cs` (1300-1400), `DirectPushCoordinator.cs` (100-200), `PullFromProdCoordinator.cs` (90-150), `IProdStoreFactory.cs`, `Program.cs` (grep only, lines 95-99, 264-274)
**Pattern extraction date:** 2026-07-06
