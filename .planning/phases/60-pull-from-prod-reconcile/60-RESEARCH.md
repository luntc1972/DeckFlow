# Phase 60: Pull-from-Prod Reconcile - Research

**Researched:** 2026-06-20
**Domain:** Blazor Server / SSH.NET SftpClient / Npgsql / DeckFlow.Core diff classifier
**Confidence:** HIGH

---

## Summary

Phase 60 is the inverse of DirectPush: where DirectPush writes local→prod, this phase reads prod→local. The existing plumbing (`IProdStoreFactory`, `ISshArtifactUploader`, `StudioConfig`, `IContentSiteIndexStore`) covers almost everything needed — but none of it goes in the download direction yet. Three new pieces must be built:

1. **SCP download** — a `DownloadArtifactsAsync` counterpart on a new `ISshArtifactDownloader` interface (mirroring `ISshArtifactUploader`) backed by `SftpClient.DownloadFile`. Downloaded files land in a staging area (a `pull-staging/` subdirectory under the Studio data dir) and are NOT promoted into the live `content-kb/` folder until the operator resolves each entry.

2. **Diff classifier** — a pure `ContentSyncDiffClassifier` class in `DeckFlow.Core/Content/`, comparing prod rows vs local rows by natural key, returning per-entry `SyncDiffEntry` records tagged with `SyncDiffKind` (ProdNewer / MissingLocally / LocalOnly / Diverged). This is a stateless pure function that belongs in Core exactly like `PublishStateDeriver`.

3. **PullFromProd.razor page** — 3-stage gated UI mirroring DirectPush.razor's pattern: Stage 1 fetches prod rows + downloads artifacts to staging, Stage 2 runs the diff classifier and surfaces the per-entry table, Stage 3 lets the operator pick adopt-prod / keep-local per row and applies local writes only. Prod is never written by this page.

The prod read uses `IProdStoreFactory.Create(rawConnStr)` (already registered as a singleton) with `prodStore.GetAllRowsAsync()` — exactly what DirectPush Stage 1 does for its diff computation. Zero new packages; SSH.NET 2025.1.0 (already in DeckFlow.Studio.csproj) provides `SftpClient.DownloadFile(string remotePath, Stream output)`.

**Primary recommendation:** Build the downloader as a new `ISshArtifactDownloader` interface + `SftpArtifactDownloader` implementation in `DeckFlow.Studio/Services/` (mirroring the existing upload pair exactly), put `ContentSyncDiffClassifier` + `SyncDiffEntry` + `SyncDiffKind` in `DeckFlow.Core/Content/`, and wire `PullFromProd.razor` as the 3-stage page using both. Never expose ex.Message in the UI (D-07 is a hard project rule); never call any write method on the prod store.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Prod DB read (content_site_index SELECT) | Studio Services (on-demand prod store) | — | Mirrors DirectPush Stage 1: IProdStoreFactory.Create + prodStore.GetAllRowsAsync — read-only SELECT only |
| SCP artifact download from Render /data | Studio Services (new ISshArtifactDownloader) | — | Symmetric to ISshArtifactUploader; SFTP GET direction using same SftpClient |
| Diff classification (per-entry) | DeckFlow.Core/Content (pure logic) | — | Stateless pure comparison; no I/O; must be unit-testable in Core.Tests |
| Staging area management (local FS) | Studio Services / PullFromProd.razor | — | Downloaded files land in pull-staging/ sub-dir under Studio data dir |
| Resolution apply — adopt-prod | Studio Services / PullFromProd.razor | DeckFlow.Core (UpsertContentColumnsOnlyAsync) | Local-only write: UpsertContentColumnsOnlyAsync on local IndexStore, then copy staged artifact to live content-kb/ |
| Resolution apply — keep-local | PullFromProd.razor (no-op) | — | Just marks the entry resolved; no store call needed |
| Config gate (presence check) | PullFromProd.razor (UI) | Program.cs (StudioConfig) | IsProdConfigured && IsScpConfigured — same check DirectPush uses |
| Secret handling | SftpArtifactDownloader + PullFromProd.razor | — | Same D-07 rule: never log or surface ex.Message; connection string never logged at DI startup |

---

## Standard Stack

### Core — Zero New Packages

All capabilities are covered by packages already in the solution.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| SSH.NET (`Renci.SshNet`) | 2025.1.0 | SftpClient download direction | Already in DeckFlow.Studio.csproj; `SftpClient.DownloadFile(string, Stream)` is the symmetric download API to `UploadFile` |
| Npgsql | 10.0.0 | Prod Postgres read via IProdStoreFactory | Already in DeckFlow.Core; ProdStoreFactory already creates a Postgres-backed ContentSiteIndexStore |
| Dapper | in-solution | ContentSiteIndexStore query layer | Already used for all store queries |
| bUnit 2.7.2 | 2.7.2 | Blazor component tests | Already in DeckFlow.Studio.Tests.csproj |
| xUnit 2.9.3 | 2.9.3 | Core unit tests | Already in DeckFlow.Core.Tests.csproj |

**Installation:** No new packages required. Zero new NuGet dependencies.

### API Confirmed in Documentation

`SftpClient.DownloadFile(string path, Stream output, Action<ulong>? downloadCallback = null)` — confirmed in SSH.NET 2025.1.0 official API docs at https://sshnet.github.io/SSH.NET/api/Renci.SshNet.SftpClient.html [VERIFIED: official SSH.NET docs]. Async variant: `DownloadFileAsync(string path, Stream output, CancellationToken)`.

---

## Package Legitimacy Audit

> **No new packages are introduced by this phase.** All dependencies are already present in the solution.

| Package | Registry | Age | Status | slopcheck | Disposition |
|---------|----------|-----|--------|-----------|-------------|
| SSH.NET | NuGet | ~10+ yrs | 288.7M total downloads; maintained by Renci/sshnet org | N/A (NuGet, not pip/npm) | Approved — well-established |
| Npgsql | NuGet | ~14 yrs | Industry-standard .NET Postgres driver | N/A (NuGet, not pip/npm) | Approved — well-established |
| bUnit | NuGet | ~5 yrs | Standard Blazor unit testing library | N/A (NuGet) | Approved — already in solution |

**Packages removed due to slopcheck [SLOP] verdict:** none (slopcheck is Python/npm only; NuGet packages verified against nuget.org directly — SSH.NET at 288.7M downloads [VERIFIED: nuget.org], Npgsql is the canonical .NET Postgres driver [ASSUMED: training knowledge, not fetched from nuget.org during this session]).

---

## Architecture Patterns

### System Architecture Diagram

```
Operator clicks "Pull from Prod"
        │
        ▼
[PullFromProd.razor Stage 1]
        │
        ├──→ IProdStoreFactory.Create(connStr) → prodStore.GetAllRowsAsync()
        │           (PROD Postgres READ — no write)
        │
        └──→ ISshArtifactDownloader.DownloadArtifactsAsync(requests, staging, progress, ct)
                    │
                    └──→ SftpClient.DownloadFile(remotePath, localFileStream)
                              (SFTP GET from Render /data → local pull-staging/)
        │
        ▼
[PullFromProd.razor Stage 2]
        │
        └──→ ContentSyncDiffClassifier.Classify(prodRows, localRows)
                    │
                    └──→ IReadOnlyList<SyncDiffEntry> with SyncDiffKind per entry
                              (pure Core function, no I/O)
        │
        ▼ (per-entry table with adopt-prod / keep-local radio)
        │
[PullFromProd.razor Stage 3 — Apply Resolutions]
        │
        ├──→ adopt-prod:
        │         localStore.UpsertContentColumnsOnlyAsync(prodRow, ct)
        │         File.Copy(stagingPath → live content-kb/ path)
        │
        └──→ keep-local:
                  (no store call; mark entry as resolved in UI state)
```

### Recommended New File Layout

```
DeckFlow.Core/Content/
├── SyncDiffEntry.cs          # sealed record + SyncDiffKind enum (new)
└── ContentSyncDiffClassifier.cs   # pure static/sealed class (new)

DeckFlow.Studio/Services/
├── ISshArtifactDownloader.cs  # new interface (mirrors ISshArtifactUploader)
└── SftpArtifactDownloader.cs  # new implementation (mirrors SftpArtifactUploader)

DeckFlow.Studio/Pages/
└── PullFromProd.razor         # new 3-stage page

DeckFlow.Core.Tests/Content/
└── ContentSyncDiffClassifierTests.cs   # xUnit tests (new)

DeckFlow.Studio.Tests/
├── TestDoubles/
│   └── FakeSshArtifactDownloader.cs    # mirrors FakeSshArtifactUploader
└── PullFromProdPageTests.cs            # bUnit behavioral tests (new)
```

---

## Detailed Design: Each New Piece

### 1. SyncDiffKind Enum and SyncDiffEntry Record (DeckFlow.Core/Content/SyncDiffEntry.cs)

```csharp
// Source: derived from ROADMAP.md Phase 60 success criterion 2 and DirectPush.razor:449
namespace DeckFlow.Core.Content;

/// <summary>Classification of a prod↔local content entry sync diff.</summary>
public enum SyncDiffKind
{
    /// <summary>Entry exists in both prod and local; prod's indexed_utc is newer.</summary>
    ProdNewer,

    /// <summary>Entry exists in prod but not in the local store.</summary>
    MissingLocally,

    /// <summary>Entry exists in local but not in prod (local-only draft).</summary>
    LocalOnly,

    /// <summary>Entry exists in both; content columns differ but neither is strictly newer (equal timestamps or both null).</summary>
    Diverged,
}

/// <summary>Per-entry result from <see cref="ContentSyncDiffClassifier.Classify"/>.</summary>
public sealed record SyncDiffEntry
{
    public required string NaturalKeyType { get; init; }
    public required string NaturalKeyValue { get; init; }
    public required SyncDiffKind Kind { get; init; }
    public required string Title { get; init; }
    /// <summary>Prod row; null for <see cref="SyncDiffKind.LocalOnly"/> entries.</summary>
    public ContentSiteIndexRow? ProdRow { get; init; }
    /// <summary>Local row; null for <see cref="SyncDiffKind.MissingLocally"/> entries.</summary>
    public ContentSiteIndexRow? LocalRow { get; init; }
    /// <summary>Relative artifact path (from prod row when available, else local row).</summary>
    public required string ArtifactPath { get; init; }
}
```

### 2. ContentSyncDiffClassifier (DeckFlow.Core/Content/ContentSyncDiffClassifier.cs)

Pure static function — no constructor, no I/O, no DI. Pattern mirrors `PublishStateDeriver` which is also a pure sealed class in `DeckFlow.Core/Content/` [VERIFIED: codebase].

**"Newer" field:** Use `IndexedUtc` (the distill/index timestamp — present on every row, non-nullable). This is the most reliable "content freshness" signal because `PublishedUtc` is the YouTube publication timestamp (content metadata, not our distill recency) and `PushedToProdUtc` is a local fact that prod doesn't stamp back. `IndexedUtc` is written by the orchestrator at distill time and represents when the content artifact was generated — if prod's `IndexedUtc` is newer, the operator re-distilled on prod (unlikely but possible via seed update); if local's is newer, the operator re-distilled locally.

**"Content changed" field (Diverged):** Compare `ArchetypeTags + BracketTags + CardCategoryTags + Title + ArtifactPath` as a combined fingerprint when timestamps are equal or both null.

```csharp
// Source: derived from ContentSiteIndexRow fields in DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:107
namespace DeckFlow.Core.Content;

public static class ContentSyncDiffClassifier
{
    public static IReadOnlyList<SyncDiffEntry> Classify(
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        IReadOnlyList<ContentSiteIndexRow> localRows)
    {
        // Build lookup by natural key
        var prodByKey = prodRows.ToDictionary(r => NaturalKey(r));
        var localByKey = localRows.ToDictionary(r => NaturalKey(r));

        var result = new List<SyncDiffEntry>();

        // prod entries
        foreach (var (key, prodRow) in prodByKey)
        {
            if (!localByKey.TryGetValue(key, out var localRow))
            {
                result.Add(MakeEntry(prodRow, null, SyncDiffKind.MissingLocally, key));
                continue;
            }
            var kind = ClassifyPair(prodRow, localRow);
            result.Add(MakeEntry(prodRow, localRow, kind, key));
        }

        // local-only entries
        foreach (var (key, localRow) in localByKey)
        {
            if (!prodByKey.ContainsKey(key))
            {
                result.Add(MakeEntry(null, localRow, SyncDiffKind.LocalOnly, key));
            }
        }

        return result;
    }

    private static SyncDiffKind ClassifyPair(ContentSiteIndexRow prod, ContentSiteIndexRow local)
    {
        var prodUtc = prod.IndexedUtc.ToUniversalTime().UtcDateTime;
        var localUtc = local.IndexedUtc.ToUniversalTime().UtcDateTime;

        if (prodUtc > localUtc) return SyncDiffKind.ProdNewer;
        if (localUtc > prodUtc) return SyncDiffKind.Diverged; // local is newer — it may have been re-distilled locally
        // timestamps equal — check content fingerprint
        return ContentFingerprint(prod) == ContentFingerprint(local)
            ? SyncDiffKind.Diverged  // same time but content differs (edge case: re-distill same second)
            : SyncDiffKind.Diverged; // safe: always surface equal-time differing content
        // Note: if timestamps equal AND content identical, we could skip but surfacing is safer.
    }
    // ... helpers: NaturalKey, MakeEntry, ContentFingerprint
}
```

> **Design note:** When `localUtc > prodUtc` the local version is newer — this surfaces as `Diverged` (not a new classification) because from the operator's perspective, "local has been re-distilled since the prod version" is also a situation to review, not just silently adopt. Planner should confirm whether to add a `LocalNewer` classification (SYNC-02 only specifies the four listed kinds: prod-newer / missing-locally / local-only / diverged). [ASSUMED: treating local-newer as a subcase of Diverged; could be its own kind — planner to decide.]

### 3. ISshArtifactDownloader + SftpArtifactDownloader (DeckFlow.Studio/Services/)

**Interface** — mirrors `ISshArtifactUploader` exactly:

```csharp
// Source: ISshArtifactUploader.cs:1 pattern
namespace DeckFlow.Studio.Services;

public interface ISshArtifactDownloader
{
    Task<IReadOnlyList<SshDownloadResult>> DownloadArtifactsAsync(
        IReadOnlyList<SshDownloadRequest> downloads,
        string localStagingRoot,
        IProgress<SshDownloadResult>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record SshDownloadRequest(
    string RemoteRelativePath,   // relative to RemoteArtifactRoot (same as SshUploadRequest.RemoteRelativePath)
    string LocalRelativePath);   // same relative path — combined with localStagingRoot to form local dest

public sealed record SshDownloadResult(
    string RemoteRelativePath,
    string LocalPath,
    bool Success,
    string? FailureReason);
```

**Implementation** — `SftpArtifactDownloader` reads the same `Studio:Scp:*` keys and uses `SftpClient.DownloadFile`:

```csharp
// Source: SftpArtifactUploader.cs:36 config-reading pattern; SSH.NET docs DownloadFile API
public sealed class SftpArtifactDownloader : ISshArtifactDownloader
{
    // Same Studio:Scp:* config keys as SftpArtifactUploader
    // SftpClient — NOT ScpClient (existing uploader uses SftpClient.UploadFile, download is symmetric)
    // D-07: never log _host, _username, _keyFile values; never surface ex.Message
    // Path-traversal guard identical to TryBuildRemotePath in uploader

    private DownloadOne(SftpClient client, SshDownloadRequest request, string localStagingRoot):
        // 1. TryBuildRemotePath (same logic as uploader) for the remote side
        // 2. Build local dest: Path.Combine(localStagingRoot, request.LocalRelativePath) with traversal check
        // 3. Directory.CreateDirectory(Path.GetDirectoryName(localDest))
        // 4. using var fs = File.Create(localDest); client.DownloadFile(remotePath, fs);
        // 5. Return SshDownloadResult
}
```

Key constraint: open ONE `SftpClient` per `DownloadArtifactsAsync` invocation (same rationale as uploader comment at `SftpArtifactUploader.cs:59`: SftpClient is not thread-safe across concurrent calls).

**Staging directory:** `Path.Combine(studioDataDirectory, "pull-staging")` — alongside `content-kb/` and `content-kb.db`. Created on first use (not at DI startup). On `adopt-prod` for an entry, the staged file is `File.Move` (or `File.Copy+Delete`) into the live `contentKbArtifactRoot`. On `keep-local`, the staged file is left for cleanup or discarded at session end.

### 4. PullFromProd.razor — 3-Stage Gated Page

**Config gate** — same `IsProdConfigured && IsScpConfigured` check as DirectPush.razor:38. If either is false, show warning banners; disable the Stage 1 button.

**Stage 1 — Fetch From Prod (combined DB + SCP):**
- `prodStore = ProdStoreFactory.Create(Configuration["Studio:ProdConnectionString"] ?? "")`
- `await prodStore.EnsureSchemaAsync(ct)` — run every time (idempotent; same pattern as DirectPush:516)
- `prodRows = await prodStore.GetAllRowsAsync(ct)` — read all prod rows
- `requests = prodRows.Select(r => new SshDownloadRequest(r.ArtifactPath, r.ArtifactPath)).ToList()`
- `await SshDownloader.DownloadArtifactsAsync(requests, stagingRoot, progress, ct)` — pull artifacts to staging
- If any download fails, mark the corresponding diff entry with a download-failed flag (don't abort; let the diff run over what succeeded)
- Stage 1 result: `_prodRows` cached, `_downloadResults` per-file

**Stage 2 — Classify Diffs:**
- `localRows = await IndexStore.GetAllRowsAsync(ct)` — read all local rows
- `_diffEntries = ContentSyncDiffClassifier.Classify(_prodRows, localRows)` — pure function
- Show per-entry table: Title | Natural key | Kind badge | Download status | Resolution (radio: adopt-prod / keep-local / skip)
- LocalOnly entries are advisory (they won't be overwritten by adopt-prod); still show them so the operator sees the full picture

**Stage 3 — Apply Resolutions:**
- Iterate `_diffEntries` where resolution == adopt-prod:
  - `await IndexStore.UpsertContentColumnsOnlyAsync(entry.ProdRow!, ct)` — writes local only
  - `File.Move(staged path → live content-kb/ path, overwrite: true)`
- Iterate entries where resolution == keep-local: no-op
- Track per-entry result (RowResult pattern from DirectPush)
- Never call any method on the `prodStore` during Stage 3 — only `IndexStore` (local)

**Hard guard on Stage 3** (mirrors DirectPush:668):
```csharp
if (!_diffReady || _operationInFlight) return;
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Prod Postgres connection | Custom Npgsql open | `IProdStoreFactory.Create(connStr)` → `IContentSiteIndexStore` | Already exists; `ContentSiteIndexStore.cs:30` accepts any `RelationalDatabaseConnection`; ProdStoreFactory normalizes URL form |
| SCP download to local | Custom SFTP client | `SftpClient.DownloadFile(remotePath, stream)` from SSH.NET 2025.1.0 | Built-in; same client already used for upload |
| Content column local upsert | Manual SQL | `IContentSiteIndexStore.UpsertContentColumnsOnlyAsync` | Existing method; specifically designed to NOT clobber admin fields (is_visible / is_hidden / is_evergreen / approval_status) |
| Natural key extraction | Custom logic | `ContentIndexExportRow.From(row)` or inline `row.YoutubeVideoId ?? row.RssGuid` | Pattern established in DirectPush.razor:524–527 and ContentIndexExportRow.cs:72 |
| Prod connection string normalization | Custom parser | `PostgresConnectionStringNormalizer.Normalize(connStr)` | Already used in ProdStoreFactory.cs:27; handles the `postgresql://` URL form from Render DATABASE_URL |

**Key insight:** The entire prod read plumbing (DB connection, schema check, row query) is already wired and battle-tested in DirectPush Stage 1. PullFromProd Stage 1 reuses it verbatim — the only new code is the download direction.

---

## Common Pitfalls

### Pitfall 1: Writing to Prod During Stage 3
**What goes wrong:** Calling any mutating method (`UpsertContentColumnsOnlyAsync`, `StampPushedToProdAsync`, `SetVisibilityAsync`) on the `prodStore` reference during resolution apply.
**Why it happens:** Copy-paste from DirectPush Stage 3 without realizing this page goes the other direction.
**How to avoid:** The `prodStore` reference is created in Stage 1 and MUST NOT be captured into a field. Stage 3 calls only on `IndexStore` (the injected local store). A code comment at the top of Stage 3 and a separate method signature that only receives `IndexStore` will make this clear.
**Warning signs:** Any call to `prodStore.UpsertXxx`, `prodStore.StampXxx`, `prodStore.SetVisibilityAsync` in the Stage 3 method.

### Pitfall 2: Npgsql timestamptz-vs-text (F-51-PG-01 recurrence)
**What goes wrong:** If a WHERE clause on `pushed_to_prod_utc` or `indexed_utc` compares a TEXT literal against a TIMESTAMPTZ column on Postgres, Npgsql throws `42883` (operator not found). SQLite is tolerant.
**Why it happens:** Dapper parameter type inference sends `DateTimeOffset` as a string on some paths.
**How to avoid:** The read path (`GetAllRowsAsync`) does no timestamp filtering, so F-51-PG-01 does not apply here. However, `UpsertContentColumnsOnlyAsync` also has no timestamp-typed WHERE clause. The risk is zero for the read path; the only write is local SQLite. Document this explicitly as a non-issue for this phase: the prod store is read-only, and the local write is SQLite via the existing `UpsertContentColumnsOnlyAsync` which has no timestamptz comparisons. [VERIFIED: codebase — UpsertContentColumnsOnlySql at ContentSiteIndexStore.cs:891 has no timestamp WHERE]

### Pitfall 3: Clobbering Local Admin State on adopt-prod
**What goes wrong:** Using `UpsertRowAsync` instead of `UpsertContentColumnsOnlyAsync` when applying adopt-prod resolution — this overwrites `is_visible`, `is_hidden`, `is_evergreen`, and `approval_status` on existing local rows.
**Why it happens:** `UpsertRowAsync` is the "full upsert" and is easy to reach first.
**How to avoid:** The same constraint applies here as in DirectPush Stage 3 (comment at DirectPush.razor:694): only `UpsertContentColumnsOnlyAsync` may run. The `FakeContentSiteIndexStore` already throws `InvalidOperationException` from `UpsertRowAsync` and `UpsertRowPreservingVisibilityAsync` as a test-time guard — the pull page tests will catch this automatically. [VERIFIED: FakeContentSiteIndexStore.cs:28-35]

### Pitfall 4: Staging File Collision / Incomplete Pull
**What goes wrong:** Stage 3 apply tries to promote a staged file that wasn't actually downloaded (download failed in Stage 1).
**How to avoid:** Tag `SyncDiffEntry` with a `bool ArtifactDownloaded` field. The Stage 3 adopt-prod path must check this flag and skip the `File.Move` if the artifact wasn't staged. The operator sees a per-entry "artifact not downloaded" warning. Do NOT abort the entire Stage 3 if one artifact is missing — continue applying other resolved rows.

### Pitfall 5: Path Traversal on Download Target
**What goes wrong:** Accepting a `RemoteRelativePath` from the prod DB row without re-validating it before building the local staging path.
**Why it happens:** The path was validated at write time by `ContentSiteIndexStore.ValidateArtifactPath`, but a compromised or manipulated prod DB could contain a `..` traversal.
**How to avoid:** `SftpArtifactDownloader` must validate the `LocalRelativePath` (reject `..`, reject rooted paths) before `Path.Combine(stagingRoot, localRelativePath)`, and confirm the resolved path stays under `stagingRoot` — exact same logic as `TryBuildRemotePath` in `SftpArtifactUploader.cs:151`. This is defense in depth. [VERIFIED: SftpArtifactUploader.cs:151-188]

### Pitfall 6: Secret Leak via ex.Message
**What goes wrong:** In catch blocks for SFTP or Npgsql exceptions, surfacing `ex.Message` to the Blazor UI or logs can leak the host, username, remote path, or connection string.
**Why it happens:** The D-07 rule is in `DirectPush.razor` comments but a new page can miss it.
**How to avoid:** All catch blocks in `PullFromProd.razor` and `SftpArtifactDownloader` must use sanitized literal strings only — never `ex.Message`. Follow the exact pattern at `DirectPush.razor:574-576` and `SftpArtifactUploader.cs:19-20`.

### Pitfall 7: Blazor Sync Context Deadlock (Task.Run pattern)
**What goes wrong:** Awaiting store calls or SFTP operations directly on the Blazor sync context blocks the SignalR rendering pipeline.
**How to avoid:** Wrap each stage's async work in `Task.Run(async () => { ... }, ct)` exactly as DirectPush.razor does (lines 509, 599, 680). Call `await InvokeAsync(() => { /* state mutation + StateHasChanged */ })` from inside the Task.Run to post UI updates back to the Blazor context. [VERIFIED: DirectPush.razor:458-463 pattern]

### Pitfall 8: LocalOnly Entries Shown as Adopt-Prod Candidates
**What goes wrong:** Offering "adopt-prod" as a resolution for a `LocalOnly` entry makes no sense (there is no prod row to adopt).
**How to avoid:** LocalOnly entries are display-only in the diff table (the operator is being informed "this is in your local store but not in prod"). The UI should show no resolution radio for LocalOnly entries — just an informational badge. Stage 3 skips these entirely.

---

## Runtime State Inventory

> This phase is not a rename/refactor phase. Omit detailed runtime state section. Brief note: no string renaming occurs; no stored keys change; the only new runtime state is the `pull-staging/` directory created on first pull.

None — no rename/refactor. The `pull-staging/` subdirectory is transient (created in Stage 1, consumed in Stage 3). No migrations.

---

## Code Examples

### Reading All Prod Rows (Stage 1 — pattern from DirectPush.razor:514-519)
```csharp
// Source: DirectPush.razor:514-519 (existing, verified)
var rawConnStr = Configuration["Studio:ProdConnectionString"] ?? string.Empty;
var prodStore = ProdStoreFactory.Create(rawConnStr);
await prodStore.EnsureSchemaAsync(_cts.Token).ConfigureAwait(false);
var prodRows = await prodStore.GetAllRowsAsync(_cts.Token).ConfigureAwait(false);
// prodStore is never written to — only read here.
```

### SftpClient Download (NEW — mirrors upload at SftpArtifactUploader.cs:128-133)
```csharp
// Source: SSH.NET 2025.1.0 official docs + SftpArtifactUploader.cs upload pattern
// Upload: client.UploadFile(fileStream, remotePath);
// Download (symmetric):
using var fileStream = File.Create(localDest);
client.DownloadFile(remotePath, fileStream);
// async form: await client.DownloadFileAsync(remotePath, fileStream, ct)
```

### UpsertContentColumnsOnlyAsync local-only write (Stage 3 adopt-prod)
```csharp
// Source: DirectPush.razor:695 (existing, verified) — same call but on LOCAL IndexStore
await IndexStore.UpsertContentColumnsOnlyAsync(entry.ProdRow!, _cts.Token).ConfigureAwait(false);
// Then promote staged artifact:
var localDest = Path.Combine(_artifactRoot, entry.ArtifactPath);
Directory.CreateDirectory(Path.GetDirectoryName(localDest)!);
File.Move(stagedPath, localDest, overwrite: true);
```

### Diff Classification (pure)
```csharp
// Source: ContentSyncDiffClassifier (NEW in DeckFlow.Core/Content/)
// Equivalent pattern: PublishStateDeriver.Derive(...) in PublishStateDeriver.cs:15
var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows);
// entries is IReadOnlyList<SyncDiffEntry>; no I/O, no exceptions (pure)
```

### Config Gate (mirrors DirectPush.razor:38-53)
```csharp
// Source: DirectPush.razor:38-53 (existing, verified)
@if (!Config.IsProdConfigured || !Config.IsScpConfigured)
{
    // show warning banners
}
// Stage 1 button disabled when: _operationInFlight || !Config.IsProdConfigured || !Config.IsScpConfigured
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| ScpClient (legacy SSH.NET) | SftpClient (preferred; used by existing uploader) | SftpClient.DownloadFile / UploadFile are the same client — consistent |
| DI-registered prod store (live at startup) | On-demand via IProdStoreFactory (built inside the action) | D-03: minimizes always-live accidental-write surface |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `SyncDiffKind.Diverged` covers the local-is-newer case (i.e., no separate `LocalNewer` kind needed) | Detailed Design §2 | SYNC-02 requires 4 specific labels; if the spec meant local-newer as distinct from diverged, the enum needs a 5th value — low risk as the planner can confirm |
| A2 | `IndexedUtc` is the right "content freshness" field for ProdNewer classification | Detailed Design §2 | If `PublishedUtc` or `PushedToProdUtc` was intended, classification output differs — but `IndexedUtc` is the distill timestamp and is non-nullable, making it the most reliable comparison axis |
| A3 | Staging area: `pull-staging/` sub-dir under Studio data dir (not under `content-kb/`) | Detailed Design §3 | If the planner wants staging inside `content-kb/` with a `_staged/` prefix, paths would differ slightly — no behavioral risk |
| A4 | Stage 1 combines DB fetch and artifact download (single combined stage) | Architecture Patterns | Alternatively, these could be two separate stages with their own progress reporting; keeping them combined is simpler and mirrors Stage 1+2 of DirectPush logically |
| A5 | Npgsql on NuGet is the canonical .NET Postgres driver (training knowledge) | Package Legitimacy Audit | Extremely low risk — Npgsql.org is well-known; version already in use in this codebase |

---

## Open Questions (RESOLVED — see 60-CONTEXT.md Q1/Q2/Q3)

1. **LocalNewer as its own SyncDiffKind?**
   - What we know: SYNC-02 specifies exactly prod-newer / missing-locally / local-only / diverged
   - What's unclear: does "diverged" cover "local is newer" or is that a 5th kind?
   - Recommendation: implement Diverged to cover all "same key, content differs or local is newer" cases; the planner should confirm with the operator

2. **LocalOnly entries: downloadable?**
   - What we know: LocalOnly entries exist only locally — there is no prod artifact to download
   - What's unclear: should Stage 2 surface a LocalOnly entry's local artifact path as informational?
   - Recommendation: show LocalOnly as display-only in the diff table (no adopt/keep radio); no action required

3. **Partial download failure handling:**
   - What we know: DirectPush treats any SCP failure as a hard block (Stage 3 locked until Stage 2 fully succeeds)
   - What's unclear: for the pull direction, should one missing artifact block the entire resolution or only the adopt-prod action for that specific entry?
   - Recommendation: per-entry: mark the entry's ArtifactDownloaded=false; still allow adopt-prod on entries that did download; block adopt-prod on entries that didn't

4. **approval_status on adopt-prod:**
   - What we know: `UpsertContentColumnsOnlyAsync` does NOT touch `approval_status` on existing rows (ContentSiteIndexStore.cs:891-928); for NEW rows it inserts `approval_status='pending'`
   - What's unclear: should an adopt-prod for a MissingLocally entry result in `pending` (needs local review) or `approved`?
   - Recommendation: leave `pending` for new entries (the prod row was presumably reviewed before being pushed; but the local store convention is that approval happens locally). Operator re-approves before next DirectPush. This is safe and consistent with the existing upsert contract.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build | ✓ (Windows, used by other phases) | 10.x | — |
| SSH.NET SftpClient | SftpArtifactDownloader | ✓ | 2025.1.0 (in DeckFlow.Studio.csproj) | — |
| Npgsql / Postgres | IProdStoreFactory | ✓ (configured on operator machine) | 10.0.0 | Page shows "prod not configured" banner |
| Render SSH access | Artifact download | ✗ (not verifiable in research) | — | Page shows "SCP not configured" banner |
| Studio user-secrets (Studio:ProdConnectionString, Studio:Scp:*) | Prod read | ✗ (operator-configured; not in repo) | — | StudioConfig gates the page |

**Missing dependencies with no fallback:** None — the page gracefully degrades when secrets are not configured (same gate as DirectPush). All code builds locally without live Postgres or SSH access.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (DeckFlow.Core.Tests) + bUnit 2.7.2 (DeckFlow.Studio.Tests) |
| Config file | DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj + DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj (existing) |
| Quick run command (Core) | `dotnet test DeckFlow.Core.Tests` |
| Quick run command (Studio) | `dotnet test DeckFlow.Studio.Tests` |
| Full suite command | `dotnet test` (solution root) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-01 | Prod rows fetched + artifacts downloaded to staging | Integration (operator-verify) | Manual operator smoke on configured machine | — |
| SYNC-01 | IProdStoreFactory + GetAllRowsAsync invoked in Stage 1 | unit (bUnit) | `dotnet test DeckFlow.Studio.Tests --filter PullFromProd` | ❌ Wave 0 |
| SYNC-01 | ISshArtifactDownloader invoked with correct requests | unit (bUnit) | `dotnet test DeckFlow.Studio.Tests --filter PullFromProd` | ❌ Wave 0 |
| SYNC-02 | ContentSyncDiffClassifier.Classify: ProdNewer case | unit (xUnit) | `dotnet test DeckFlow.Core.Tests --filter ContentSyncDiffClassifier` | ❌ Wave 0 |
| SYNC-02 | ContentSyncDiffClassifier.Classify: MissingLocally case | unit (xUnit) | same | ❌ Wave 0 |
| SYNC-02 | ContentSyncDiffClassifier.Classify: LocalOnly case | unit (xUnit) | same | ❌ Wave 0 |
| SYNC-02 | ContentSyncDiffClassifier.Classify: Diverged case | unit (xUnit) | same | ❌ Wave 0 |
| SYNC-03 | adopt-prod calls UpsertContentColumnsOnlyAsync on LOCAL store only | unit (bUnit) | `dotnet test DeckFlow.Studio.Tests --filter PullFromProd` | ❌ Wave 0 |
| SYNC-03 | adopt-prod never calls any mutating method on prod store | unit (bUnit) | same — FakeContentSiteIndexStore.UpsertRowAsync throws | ❌ Wave 0 |
| SYNC-03 | keep-local makes no store calls | unit (bUnit) | same | ❌ Wave 0 |
| SYNC-03 | Secret leak: SFTP ex.Message never surfaces in UI | unit (bUnit) | same — FakeSshArtifactDownloader throws with sentinel-bearing message | ❌ Wave 0 |
| SYNC-03 | Secret leak: Npgsql ex.Message never surfaces in UI | unit (bUnit) | same | ❌ Wave 0 |
| SC4 (prod read-only) | Stage 3 hard-guard: assert no prod write even if button re-invoked | unit (bUnit) | same — test seam pattern InvokePullApplyForTest() | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Core.Tests --filter ContentSyncDiffClassifier` (new) + `dotnet test DeckFlow.Studio.Tests --filter PullFromProd` (new)
- **Per wave merge:** `dotnet test` (full solution)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Core/Content/SyncDiffEntry.cs` — new type
- [ ] `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` — new classifier
- [ ] `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs` — covers SYNC-02 (4 SyncDiffKind values)
- [ ] `DeckFlow.Studio/Services/ISshArtifactDownloader.cs` — new interface
- [ ] `DeckFlow.Studio/Services/SftpArtifactDownloader.cs` — new implementation
- [ ] `DeckFlow.Studio/Pages/PullFromProd.razor` — new 3-stage page
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactDownloader.cs` — mirrors FakeSshArtifactUploader
- [ ] `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` — bUnit behavioral + secret-leak tests

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — Studio is local operator tool; no user auth |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A — local operator tool |
| V5 Input Validation | yes | Path-traversal guard on `LocalRelativePath` in `SftpArtifactDownloader` (mirror of `TryBuildRemotePath` in uploader); `ValidateArtifactPath` already in `ContentSiteIndexStore`; prod DB values are untrusted inputs — validate ArtifactPath before using as local file path |
| V6 Cryptography | no | SSH key auth handled by SSH.NET; no custom crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret leak via ex.Message (Npgsql conn str, SSH hostname) | Information Disclosure | Sanitized literal strings in all catch blocks; never surface ex.Message (D-07 / established in DirectPush) |
| Path traversal in ArtifactPath from prod DB | Tampering / Elevation | Validate every `ArtifactPath` before `Path.Combine(stagingRoot, …)`; reject `..` and rooted paths; confirm resolved path stays under stagingRoot |
| Accidental prod write in Stage 3 | Tampering | Hard-guard at top of Stage 3 handler + only reference `IndexStore` (local); never capture `prodStore` into a field; FakeProdStore.UpsertRowAsync throws in tests |
| Staging artifact promotion without download | Tampering (data integrity) | `ArtifactDownloaded` flag on `SyncDiffEntry`; Stage 3 adopt-prod skips file promotion if flag is false |
| Operator triggered prod connection at DI startup | Tampering / latent write | Follow D-03: build prodStore on-demand inside Stage 1 action, never at DI startup; same pattern as ProdStoreFactory.cs:26 |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Studio/Pages/DirectPush.razor` — complete write path; mirror exactly for read path; all stage patterns, config gate, D-07 conventions verified [VERIFIED: codebase]
- `DeckFlow.Studio/Services/ISshArtifactUploader.cs` + `SftpArtifactUploader.cs` — SSH.NET SftpClient pattern; path-traversal guard; D-07 sanitized failure; one-client-per-call idiom [VERIFIED: codebase]
- `DeckFlow.Studio/Services/IProdStoreFactory.cs` — prod store on-demand pattern; config key `Studio:ProdConnectionString` [VERIFIED: codebase]
- `DeckFlow.Studio/Program.cs` — `StudioConfig` presence flags, `Studio:Scp:*` keys, singleton registrations [VERIFIED: codebase]
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — all store methods; `UpsertContentColumnsOnlyAsync` SQL (never writes admin fields); `GetAllRowsAsync`; `ValidateArtifactPath` [VERIFIED: codebase]
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:107` — `ContentSiteIndexRow` fields including `IndexedUtc`, `PushedToProdUtc`, `ApprovalStatus` [VERIFIED: codebase]
- `DeckFlow.Core/Content/PublishStateDeriver.cs` — pattern for a pure sealed classifier in DeckFlow.Core/Content/ [VERIFIED: codebase]
- `DeckFlow.Core.Tests/Content/PublishStateDeriverTests.cs` — test pattern for a pure classifier [VERIFIED: codebase]
- `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` — test double pattern to mirror for downloader [VERIFIED: codebase]
- `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` — test double for prod factory [VERIFIED: codebase]
- SSH.NET SftpClient official API docs — `DownloadFile(string path, Stream output)` confirmed [VERIFIED: https://sshnet.github.io/SSH.NET/api/Renci.SshNet.SftpClient.html]
- SSH.NET NuGet package — 288.7M downloads, established package, MIT license [VERIFIED: nuget.org]
- `.planning/ROADMAP.md` Phase 60 section — success criteria, design questions, backlog note [VERIFIED: codebase]
- `.planning/REQUIREMENTS.md` — SYNC-01/02/03 wording [VERIFIED: codebase]

### Secondary (MEDIUM confidence)
- `ContentSiteIndexStore.cs:891-928` — `UpsertContentColumnsOnlySql` text confirms absence of timestamp WHERE clause (no F-51-PG-01 risk for the read path) [VERIFIED: codebase]

### Tertiary (LOW confidence)
- Npgsql is the canonical .NET Postgres driver [ASSUMED: training knowledge; version already in-solution and known to work from prior phases]

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — zero new packages; all dependencies confirmed in codebase
- Architecture: HIGH — write path (DirectPush) is fully read and the read path mirrors it exactly
- Pitfalls: HIGH — D-07, F-51-PG-01, UpsertContentColumnsOnly restriction all confirmed from codebase comments and prior bug history
- Diff Classification: HIGH — pure function; design is straightforward; single open question (LocalNewer as own kind) is flagged

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 (stable stack; 30 days)
