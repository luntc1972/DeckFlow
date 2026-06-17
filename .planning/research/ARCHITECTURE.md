# Architecture Research

**Domain:** Local harvest-and-publish studio integrated into an existing .NET 10 solution
**Researched:** 2026-06-13
**Confidence:** HIGH — grounded in direct code reading of all named files; no speculative claims

---

## The Single Biggest Architectural Decision: Where Does Orchestration Live?

**Decision: Extract the internal `Run*Async` methods from `DeckFlow.CLI` into `DeckFlow.Core`.**

The reason this matters: `ContentKbCommandRunners` in `DeckFlow.CLI` has two layers. The
public static entry points (`RunHarvestAsync(FileInfo? db, ...)`) are CLI-specific — they
resolve paths, construct concrete store instances from paths, and return `int` exit codes.
The `internal static` overloads (`RunHarvestAsync(IContentSourceStore, IContentVideoStore, ...)`)
are pure domain logic — they accept interfaces, are already tested via `[InternalsVisibleTo]`,
and carry no CLI dependency. The Studio needs the domain logic layer, not the path-resolution
layer.

**Three options evaluated:**

**Option A — Studio references DeckFlow.CLI.** Rejected. CLI is an executable project
(`Sdk="Microsoft.NET.Sdk"`, outputs a `.exe`). You cannot add a `<ProjectReference>` to an
executable from another project without either making it a library or creating a circular
dependency. Even if you restructured it as a library, Studio would inherit `System.CommandLine`
and `Serilog.Sinks.File` as transitive deps it doesn't need. The CLI namespace
(`DeckFlow.CLI`) would pollute Core. This path is blocked.

**Option B — Duplicate the orchestration in Studio.** Rejected. Two implementations of the
same harvest/distill loop is the definition of the v1.6 arch-review backlog item (Finding B:
ContentKbCommandRunners god-class split). Duplication creates two places to fix spend-cap
logic, tag filtering, and the `DistillVideoAsync` pipeline. Already rejected in the v1.6
backlog.

**Option C (Recommended) — Extract to `DeckFlow.Core` as `IContentKbOrchestrator`.** Extract
the domain-logic internals from `ContentKbCommandRunners` into a new class in
`DeckFlow.Core/Content/` (e.g., `ContentKbOrchestrator`), expose it behind an interface, and
have both CLI and Studio call it. The CLI's public static entry points become thin adapters
that construct the stores, resolve paths, and call the Core orchestrator. The Studio's Blazor
services call the same orchestrator directly via DI.

This also closes the v1.6 backlog item "ContentKbCommandRunners god-class split" as a
side-effect of building v1.7.

**Concrete evidence supporting Option C:**

The `internal static RunDistillAsync(IContentSourceStore, IContentVideoStore, ...)` overload
at line 384 of `ContentKbCommandRunners.cs` already takes pure interfaces. Its only non-Core
dependency is `Serilog.ILogger` — which `DeckFlow.Core` already references
(`Microsoft.Extensions.Logging.Abstractions` is in Core; Serilog is also already in Core as
a transitive dep via CLI patterns). The `DistillVideoAsync`, `HarvestVideoAsync`,
`HarvestSourceAsync`, and `HarvestExplicitVideoIdsAsync` private helpers have no CLI surface
area. They can move to Core as-is.

---

## System Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                    DeckFlow.Studio (NEW — local only)              │
│  Blazor Server, runs localhost, never deployed to Render           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │DiscoveryPages│  │  ReviewQueue │  │ PublishPages  │             │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘             │
│         └─────────────────┴──────────────────┘                     │
│                           │ Blazor DI                              │
│         ┌─────────────────┴──────────────────┐                    │
│         │       IContentKbOrchestrator        │  (NEW in Core)     │
│         │       IStudioPublishService         │  (NEW in Studio)   │
│         └─────────────────┬──────────────────┘                    │
└───────────────────────────┼────────────────────────────────────────┘
                            │ ProjectReference
┌───────────────────────────┼────────────────────────────────────────┐
│                    DeckFlow.Core (MODIFIED)                        │
│                                                                    │
│  Content/                                                          │
│  ┌──────────────────────────────────────────────────────────┐     │
│  │  ContentKbOrchestrator  (EXTRACTED from CLI)             │     │
│  │  + approval_status migration in ContentSiteIndexStore    │     │
│  └─────────────────────────────┬────────────────────────────┘     │
│                                │                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │ContentSource │  │ContentVideo  │  │ContentSiteIndex│            │
│  │Store         │  │Store         │  │Store (MODIFIED)│            │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
│                                                                    │
│  Storage/  RelationalDatabaseConnection (UNCHANGED)                │
│            SqliteRelationalDialect / PostgresRelationalDialect     │
└────────────────────────────────────────────────────────────────────┘
                            │ ProjectReference
┌───────────────────────────┼────────────────────────────────────────┐
│                    DeckFlow.CLI (MODIFIED — thinned)               │
│  ContentKbCommandRunners: public Run*Async = thin adapters         │
│  (construct stores from paths, call IContentKbOrchestrator)        │
└────────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                    DeckFlow.Web (UNMODIFIED for Studio)            │
│                                                                    │
│  ContentKbSeedLoader: reads index-seed.json, upserts to Postgres  │
│  AdminHarvestController: Archidekt category KB (deck harvest)      │
│  AdminContentKbController: Content KB curation (visibility, pins) │
│  ← The ONLY change to Web in v1.7 is the lazy-AJAX grid paging    │
│    on AdminHarvestController.Index                                 │
└───────────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

| Component | Project | Status | Responsibility |
|-----------|---------|--------|----------------|
| `ContentKbOrchestrator` | Core | NEW (extracted) | Domain logic for harvest, distill, block/unblock, corpus-reset, export. Accepts interfaces, returns result records (not `int` exit codes). |
| `IContentKbOrchestrator` | Core | NEW | Interface seam consumed by CLI and Studio |
| `ContentSiteIndexStore` | Core | MODIFIED | Add `approval_status` column via self-healing ALTER; add `GetPendingApprovalAsync`, `SetApprovalStatusAsync` methods |
| `IContentSiteIndexStore` | Core | MODIFIED | Add approval-status query/mutation methods |
| `ContentKbCommandRunners` | CLI | MODIFIED | Public `Run*Async` methods become thin path-resolver + store-constructor adapters that delegate to `IContentKbOrchestrator` |
| `StudioHarvestService` | Studio | NEW | Blazor-scoped service: wires `IContentKbOrchestrator` with local DB path from config; exposes progress event for UI |
| `StudioPublishService` | Studio | NEW | Blazor-scoped service: seed-export + git shell-out for commit path; Npgsql direct write + SCP shell-out for direct path |
| `StudioDiscoveryService` | Studio | NEW | Thin wrapper over `IYouTubeChannelVideoLister`; returns discovery results + harvested-status overlay |
| `StudioReviewService` | Studio | NEW | Assembles per-video review payload (summary + clips + tags) from existing Core stores; sets approval_status |
| Blazor Pages/Components | Studio | NEW | Discovery, Queue, Publish, Blocked-Video management pages |
| `AdminHarvestController` | Web | MODIFIED (lazy-paging only) | New `GET /Admin/Harvest/commanders` partial endpoint; existing Index unchanged for full-page fallback |

---

## Recommended Project Structure

```
DeckFlow.Studio/
  DeckFlow.Studio.csproj          ← net10.0, Sdk="Microsoft.NET.Sdk.Web", refs Core
  Program.cs                      ← DI wiring, user-secrets, app.Run()
  Components/
    Layout/
      MainLayout.razor
      NavMenu.razor
    Discovery/
      ChannelBrowsePage.razor      ← handle/URL input → video list
      VideoCard.razor              ← thumbnail + status badge
    Queue/
      ReviewQueuePage.razor        ← pending-approval list
      VideoReviewDetail.razor      ← summary + clips + tags + approve/reject
    Publish/
      PublishPage.razor            ← diff preview + publish action
    Admin/
      BlockedVideosPage.razor
  Services/
    StudioHarvestService.cs
    StudioDistillService.cs        ← wraps IContentKbOrchestrator.DistillAsync
    StudioPublishService.cs        ← git path + direct-push path
    StudioDiscoveryService.cs
    StudioReviewService.cs
  appsettings.json                 ← no secrets; just Kestrel/logging config
```

The `DeckFlow.Core/Content/` directory gains:
```
DeckFlow.Core/Content/
  ContentKbOrchestrator.cs        ← NEW: extracted domain logic
  IContentKbOrchestrator.cs       ← NEW: interface
  (all existing *Store.cs files unchanged except ContentSiteIndexStore.cs)
```

---

## Question 1: Where Does Shared Orchestration Live?

**Answer: Extract to `DeckFlow.Core` as `IContentKbOrchestrator`.**

**What moves:**

The following `internal static` methods from `ContentKbCommandRunners.cs` move to
`ContentKbOrchestrator` in Core, preserving their interface-based signatures:

- `RunHarvestAsync(IContentSourceStore, IContentVideoStore, IBlockedVideoStore, ...)` → `HarvestAsync(...)`
- `RunDistillAsync(IContentSourceStore, IContentVideoStore, IContentSiteIndexStore, ...)` → `DistillAsync(...)`
- `RunBlockVideoAsync(string youtubeVideoId, ..., IBlockedVideoStore, ...)` → `BlockVideoAsync(...)`
- `RunUnblockVideoAsync(string, IBlockedVideoStore, ...)` → `UnblockVideoAsync(...)`
- `RunCorpusResetAsync(IContentVideoStore, IContentSiteIndexStore, ...)` → `CorpusResetAsync(...)`
- `RunListBlockedAsync(IBlockedVideoStore, TextWriter, ...)` → `ListBlockedAsync(...)`
- All private helpers (`HarvestVideoAsync`, `DistillVideoAsync`, `HarvestSourceAsync`, etc.)
- All private constants (`ShortVideoMaxDuration`, `DistillationCallCount`, spend constants, etc.)
- `ParseVideoIds` (already `internal static`, has tests)

**What stays in CLI:**

- `ContentKbCliPaths` (path resolution from `--db` FileInfo args)
- `SlugifySourceName.Slugify` (already in Core, just the call stays)
- The public `Run*Async(FileInfo? db, ...)` entry points — these become 5-10 line adapters:
  ```csharp
  public static async Task<int> RunHarvestAsync(FileInfo? db, int limit, ...)
  {
      var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
      var sourceStore = new ContentSourceStore(dbPath);
      var videoStore = new ContentVideoStore(dbPath);
      // ... construct stores ...
      var orchestrator = new ContentKbOrchestrator();
      return await orchestrator.HarvestAsync(sourceStore, videoStore, ...) ? 0 : 1;
  }
  ```

**Return type change:** The orchestrator methods return result records (e.g., `HarvestResult`,
`DistillResult`) rather than `int` exit codes. The CLI adapters convert results to exit codes.
The Studio services use the result records directly for UI feedback.

**Serilog dependency in Core:** `ContentKbCommandRunners` already passes `Serilog.ILogger`
to its internal methods. Core already has `Microsoft.Extensions.Logging.Abstractions`. The
orchestrator should use `ILogger<ContentKbOrchestrator>` (MEL abstraction) rather than
`Serilog.ILogger`. The CLI adapters pass `NullLogger` or a Serilog-wrapped MEL adapter.
This is a small but clean improvement — Core should not depend on Serilog directly.

**Build order:**

1. Add `IContentKbOrchestrator` + `ContentKbOrchestrator` to `DeckFlow.Core` (no other
   changes; all dependencies already in Core)
2. Add `approval_status` migration to `ContentSiteIndexStore.EnsureSchemaAsync` + new
   `GetPendingApprovalAsync` / `SetApprovalStatusAsync` on the interface and store
3. Update `ContentKbCommandRunners` public entry points to delegate to Core orchestrator
   (thin adapters); all existing CLI tests continue to pass because the `internal static`
   test surface moves to Core and gets `[InternalsVisibleTo("DeckFlow.Core.Tests")]`
4. Create `DeckFlow.Studio` project with `<ProjectReference>` to Core; wire DI; first
   Studio page can render
5. Implement Studio services and Blazor pages against the extracted Core interfaces

This order means step 1-3 can be built and verified (CLI still works, tests still pass)
before the Studio project exists.

---

## Question 2: Local Data — One DB or Two?

**Answer: Studio operates on the SAME local `content-kb.db` the CLI uses.**

`ContentKbCliPaths.ResolveDatabasePath(null)` resolves to
`artifacts/uat-content-kb.db` (relative to CWD, which is the repo root when run from
the solution). The Studio's `Program.cs` configures the same path via user-secrets or
appsettings:

```json
// appsettings.json (non-secret, committed)
{
  "Studio": {
    "LocalDbPath": "artifacts/uat-content-kb.db",
    "ArtifactRoot": "content-kb/artifacts"
  }
}
```

**Why one DB:** The approve/reject workflow requires reading distilled content (in
`content_videos`) and writing approval status (in `content_site_index`). These tables only
exist in the local KB DB. Running a second DB would require a sync step. No benefit for a
single-operator tool.

**`approval_status` column flow:**

The `approval_status` column lives ONLY in the local SQLite DB. It is a local curation
signal, not a published field. The column does NOT appear in `index-seed.json` (the seed
export strips it — `ContentIndexExportRow.From()` only serializes display/navigation fields).
The column does NOT appear in prod Postgres `content_site_index` (only commit-path row
upserts happen via `ContentKbSeedLoader.UpsertRowPreservingVisibilityAsync`, which does not
set approval_status).

**State machine:**

```
[distilled] → approval_status = 'pending'   (default after distill completes)
    ↓ operator approves
approval_status = 'approved'
    ↓ operator publishes (either path)
is_visible = true  (set on the local row; carried into seed export / direct push)
    ↓
approval_status = 'approved' stays; is_visible = true is the "published" marker
```

`is_visible` on the local row controls whether the row appears in `GetAllRowsAsync()` for
the seed export. Approved + visible = exported. The seed export in
`RunContentIndexExportAsync` currently calls `GetAllRowsAsync()` — it should be modified to
call a new `GetApprovedVisibleRowsAsync()` or the existing call scoped with an
`is_visible=true` filter. The Studio's publish action sets `is_visible=true` on approved rows
before triggering the export.

**`is_visible` vs `is_evergreen` vs `approval_status` — column semantics:**

| Column | Where | Meaning |
|--------|-------|---------|
| `approval_status` | Local SQLite only | Curation decision: `pending` / `approved` / `rejected` |
| `is_visible` | Local SQLite + Prod Postgres | Published to end-users on deckflow.gg |
| `is_evergreen` | Local SQLite + Prod Postgres | Tier-1 expert fill (clip injection) |
| `pin_id` | Local SQLite + Prod Postgres | Explicit pin for a specific expert |

The `approval_status` column is NOT propagated to prod by either publish path. It is a local
operator decision log only. The prod DB has no concept of it.

---

## Question 3: The Two Publish Paths as Components

### Path A — Commit-Then-Deploy (Primary)

**What it mutates:**

1. Local SQLite: sets `is_visible=true` on all approved rows (via
   `ContentSiteIndexStore.SetVisibleAsync` called per approved row, or a batch update)
2. Local filesystem: `content-kb/seed/index-seed.json` rewritten by
   `RunContentIndexExportAsync` (scoped to visible rows)
3. Local filesystem: `content-kb/artifacts/<source-slug>/<video-id>.md` already written
   by `ContentKbOrchestrator.DistillAsync` during distillation
4. Git: `git add content-kb/seed/index-seed.json content-kb/artifacts/` →
   `git commit -m "content(kb): publish approved entries"` → `git push origin main`
5. Render: auto-deploy triggers from `main` branch push (autoDeploy: true in render.yaml);
   `ContentKbSeedLoader.LoadIfPresentAsync` runs at startup and upserts all rows from the
   seed file into Postgres via `UpsertRowPreservingVisibilityAsync`

**Integration points:**

- `StudioPublishService.PublishViaCommitAsync()`:
  1. Call `ContentSiteIndexStore.GetApprovedRowsAsync()` (new method)
  2. Call `ContentSiteIndexStore.SetVisibleAsync(id, true)` for each
  3. Call `ContentKbOrchestrator.ExportIndexAsync(outputPath)` (extracted from
     `RunContentIndexExportAsync`)
  4. Shell-out: `git add content-kb/seed/index-seed.json content-kb/artifacts/`
  5. Shell-out: `git commit -m "content(kb): publish N approved entries"`
  6. Shell-out: `git push origin main`
  7. Return `CommitPublishResult` with commit SHA + row count

**What visibility/approval/pin/evergreen carry across:**

The seed export serializes: `source`, `title`, `videoUrl`, `artifactPath`, `publishedUtc`,
`indexedUtc`, `archetypeTags`, `bracketTags`, `cardCategoryTags`, `naturalKeyType`,
`naturalKeyValue`. On load, `UpsertRowPreservingVisibilityAsync` upserts WITHOUT overwriting
`is_visible` or `is_evergreen` if a row with the same natural key already exists in Postgres.
So: visibility set by the admin in the deployed site is preserved across re-deploys. The
Studio's `is_visible=true` flag in the seed JSON is applied only to NEW rows (first upsert).
Pins are not in the seed — they exist only in the deployed site's `content_pins` table,
managed via the deployed admin UI.

### Path B — Direct Prod Push (Secondary)

**What it mutates:**

1. Local SQLite: same as Path A steps 1 (sets `is_visible=true` on approved rows)
2. Prod Postgres: calls `ContentSiteIndexStore` (constructed with the Render Postgres
   connection string from user-secrets) and calls `UpsertRowAsync` for each approved row
   with `is_visible=true`
3. Render `/data` disk: SCP each markdown artifact file from
   `content-kb/artifacts/<source-slug>/<video-id>.md` to
   `<service-id>@ssh.oregon.render.com:/data/content-kb/artifacts/<source-slug>/<video-id>.md`

**Integration points:**

- `StudioPublishService.PublishViaDirectPushAsync()`:
  1. Call `ContentSiteIndexStore.GetApprovedRowsAsync()` locally
  2. Construct a Postgres-connected `ContentSiteIndexStore` using prod connection string
     from `IConfiguration["Studio:ProdConnectionString"]`
  3. Call prod store `UpsertRowAsync(row with is_visible=true)` for each approved row
  4. Collect `row.ArtifactPath` values; shell-out `scp` for each artifact file (or bundle
     as tar for a single SCP call)
  5. Return `DirectPushResult` with rows-written count + files-scped count

**Critically: there is NO Render REST API for /data.** The SCP shell-out is the only
mechanism. This path requires the Render SSH key to be pre-configured in the Render
dashboard (one-time setup), and the service SSH address stored in user-secrets.

**What visibility/approval/pin/evergreen carry across:**

Direct push writes `is_visible=true` rows to prod Postgres directly, bypassing the seed
loader's "preserve existing visibility" behavior. This means direct push CAN overwrite
visibility settings made in the deployed admin UI. This is acceptable for the Studio
(operator knows they are doing a direct write) but should be called out in the UI. Pins
(`content_pins` table) are NOT touched by either publish path — they exist only in prod and
are managed only via the deployed admin UI.

**Diff before publish (both paths):**

For Path A: `git diff HEAD -- content-kb/seed/index-seed.json` before commit shows the exact
JSON delta. This is the natural diff signal — cheapest to implement, already works.

For Path B: query prod Postgres `SELECT youtube_video_id FROM content_site_index WHERE
is_visible=true` and compare against local approved rows. New rows are the delta. Implement
as a `StudioPublishService.GetProdDiffAsync()` that returns added/updated/removed counts.

---

## Question 4: New vs Reused Components

### New Components (DeckFlow.Studio)

| Component | Description | Reuses from Core |
|-----------|-------------|------------------|
| `StudioHarvestService` | Wires `IContentKbOrchestrator.HarvestAsync` with local DB + lister; exposes `ProgressChanged` event for Blazor `StateHasChanged` | `IContentKbOrchestrator`, `IYouTubeChannelVideoLister` |
| `StudioDistillService` | Wires `IContentKbOrchestrator.DistillAsync`; dry-run for spend preview | `IContentKbOrchestrator`, `LlmDistillationProviderFactory` |
| `StudioDiscoveryService` | Calls `IYouTubeChannelVideoLister.ListRecentAsync`; overlays harvested-status by calling `IContentVideoStore.GetVideoByYoutubeIdAsync` batch | `IYouTubeChannelVideoLister`, `IContentVideoStore` |
| `StudioReviewService` | Reads `IContentVideoStore` for summary/clips/tags; calls `IContentSiteIndexStore.SetApprovalStatusAsync` | `IContentVideoStore`, `IContentSiteIndexStore` |
| `StudioPublishService` | Path A: export + git shell-out; Path B: Npgsql direct write + SCP shell-out | `IContentKbOrchestrator.ExportIndexAsync`, `ContentSiteIndexStore` |
| Blazor pages/components | 5-8 `.razor` files for discovery, queue, publish, admin | All via service DI |

### New Components (DeckFlow.Core)

| Component | Description |
|-----------|-------------|
| `IContentKbOrchestrator` | Interface with `HarvestAsync`, `DistillAsync`, `BlockVideoAsync`, `UnblockVideoAsync`, `CorpusResetAsync`, `ExportIndexAsync`, `ListBlockedAsync` |
| `ContentKbOrchestrator` | Implementation: domain logic moved from CLI `internal static` methods |
| `approval_status` column + methods on `IContentSiteIndexStore` / `ContentSiteIndexStore` | Self-healing ALTER migration; `GetPendingApprovalAsync`, `SetApprovalStatusAsync`, `GetApprovedRowsAsync` |

### Modified Components

| Component | What Changes |
|-----------|-------------|
| `ContentKbCommandRunners` (CLI) | Public `Run*Async` entry points become thin adapters; `internal static` domain methods removed (moved to Core) |
| `ContentSiteIndexStore` (Core) | Add `approval_status` column in `EnsureSchemaAsync` self-healing block; add 3 new methods |
| `IContentSiteIndexStore` (Core) | Add 3 new method signatures |
| `AdminHarvestController` (Web) | Add `GET /Admin/Harvest/commanders` partial endpoint for lazy AJAX paging |
| `AdminHarvestViewModel` (Web) | No change to the model; the partial returns the same data already used for `HarvestedCommanders` |

### Unchanged (Reused Directly)

| Component | Used By |
|-----------|---------|
| `ContentSourceStore`, `ContentVideoStore`, `BlockedVideoStore`, `ContentHarvestRunStore`, `LlmSpendLedger`, `WhisperSpendLedger` | Studio via DI |
| `IYouTubeChannelVideoLister` / `YouTubeChannelVideoLister` | `StudioDiscoveryService` |
| `RelationalDatabaseConnection` dual-dialect | Studio constructs local SQLite connection + optionally a Postgres prod connection |
| `ContentArtifactWriter` | `ContentKbOrchestrator.DistillAsync` (already calls it) |
| `LlmDistillationProviderFactory` | `StudioDistillService` |
| `ContentTagVocabulary` | Stays in orchestrator |

---

## Data Flow Diagrams

### Flow 1: Operator Searches YouTube → Approved Row Live on deckflow.gg (Path A — Commit-Deploy)

```
[Studio UI: channel handle input]
    ↓
StudioDiscoveryService.BrowseChannelAsync(handle)
    ↓
IYouTubeChannelVideoLister.ListRecentAsync(sourceUrl, limit)
    ↓ (serialized; AngleSharp concurrency=1)
IContentVideoStore.GetVideoByYoutubeIdAsync per video  ← overlay "already harvested" badge
    ↓
[Studio UI: video grid with checkboxes]
    ↓ operator selects videos + clicks "Harvest"
StudioHarvestService.HarvestAsync(videoIds: [...])
    ↓
IContentKbOrchestrator.HarvestAsync(sourceStore, videoStore, blockedVideoStore,
    ledger, lister, transcriptSource, chunker, limit=0, videoIds=[...])
    ↓
content_videos rows inserted; transcript_status = 'captions' or 'whisper' or 'failed'
    ↓
[Studio UI: Queue page]
StudioDistillService.DryRunAsync(videoIds: [...])    ← shows projected spend
    ↓ operator confirms cost + clicks "Distill"
IContentKbOrchestrator.DistillAsync(sourceStore, videoStore, indexStore,
    runStore, ledger, distiller, artifactRoot, videoIds=[...])
    ↓
content_videos: summary + clips + tags inserted; distill_status = 'distilled'
content_site_index: UpsertRowAsync (is_visible=false, approval_status='pending')
content-kb/artifacts/<slug>/<video-id>.md written to local filesystem
    ↓
[Studio UI: Review Queue shows new entry]
StudioReviewService.GetPendingAsync()
    ↓ reads content_videos + content_site_index WHERE approval_status='pending'
[Operator reviews summary + clips + tags; clicks Approve]
StudioReviewService.ApproveAsync(siteIndexId)
    ↓
IContentSiteIndexStore.SetApprovalStatusAsync(id, 'approved')
    ↓
[Publish page: diff shows N new rows vs last commit]
StudioPublishService.GetCommitDiffAsync()   ← git diff HEAD -- index-seed.json
    ↓ operator reviews diff + clicks "Publish via Commit"
StudioPublishService.PublishViaCommitAsync()
    1. SetVisibleAsync(all approved ids, true)         ← local SQLite
    2. IContentKbOrchestrator.ExportIndexAsync(path)   ← rewrites index-seed.json
    3. git add content-kb/seed/index-seed.json content-kb/artifacts/
    4. git commit -m "content(kb): publish N approved entries"
    5. git push origin main
    ↓
Render auto-deploy triggered (autoDeploy: true)
ContentKbSeedLoader.LoadIfPresentAsync()
    ↓
IContentSiteIndexStore.UpsertRowPreservingVisibilityAsync(each row)
    ↓ (new rows get is_visible=true from seed; existing rows preserve admin-set visibility)
[Row live on deckflow.gg /content-kb]
```

### Flow 2: Operator Searches YouTube → Approved Row Live on deckflow.gg (Path B — Direct Push)

```
[Same discovery → harvest → distill → review → approve steps as Path A]
    ↓
[Publish page: diff shows N new rows vs prod Postgres]
StudioPublishService.GetProdDiffAsync()
    ↓ queries prod Postgres content_site_index for existing natural keys
    ↓ diffs against local approved rows
[Operator reviews diff + clicks "Publish Direct"]
StudioPublishService.PublishViaDirectPushAsync()
    1. SetVisibleAsync(all approved ids, true)          ← local SQLite
    2. Construct prod Postgres ContentSiteIndexStore
       (connection string from IConfiguration["Studio:ProdConnectionString"])
    3. UpsertRowAsync(row with is_visible=true) per approved row  ← prod Postgres
    4. Collect artifact paths; scp content-kb/artifacts/<slug>/<id>.md
       → <service-id>@ssh.oregon.render.com:/data/content-kb/artifacts/<slug>/<id>.md
    ↓
[Row immediately visible in prod Postgres; artifact file on /data]
[No Render restart needed; DeckFlow.Web reads from Postgres (not seed file) in prod]
```

**Key difference:** In prod, `DeckFlow.Web` uses Postgres (not the seed file). The seed file
is the bootstrap mechanism for new deploys. Once a row is in Postgres, it is served from
Postgres. Path B writes directly to Postgres; Path A writes via seed → deploy →
`SeedLoader.UpsertRowPreservingVisibilityAsync`. Both end at the same destination.

---

## Question 5: Admin Grid Lazy-Paging Fix

**Where the new partial endpoint sits:**

Add `GET /Admin/Harvest/commanders` to the existing `AdminHarvestController`. This endpoint
returns a Razor partial view (`_CommanderGrid.cshtml`) containing only the paged table and
pagination controls, not the full page layout. The existing `Index` action remains unchanged
for full-page loads and non-JS fallback.

**The fix:**

`AdminHarvestController.Index` currently calls both `GetDistinctProcessedCommanderCountAsync`
and `GetPagedProcessedCommandersAsync` on every page load, regardless of whether the
commander grid section is actually needed on the initial render. The slow initial load is the
count aggregate query running before the page responds.

**Recommended approach:**

- On initial `GET /Admin/Harvest`, render the page skeleton with the stats, recent runs, and
  schedule sections (fast). Render the commander grid as an empty placeholder with
  `data-page="1"`.
- The page's existing JS (or new lightweight TS) immediately fires
  `GET /Admin/Harvest/commanders?page=1` on `DOMContentLoaded`.
- The partial endpoint runs only the count + page queries, returns the partial HTML, and the
  JS replaces the placeholder.
- Pagination clicks fire new AJAX requests to the same endpoint.

**Whether it touches Core stores:** Yes, minimally. It calls the same
`_categoryStore.GetDistinctProcessedCommanderCountAsync` and
`_categoryStore.GetPagedProcessedCommandersAsync` that `Index` already calls. No new store
methods needed. The partial endpoint is a pure controller-layer change.

```csharp
// New action in AdminHarvestController
[HttpGet("commanders")]
public async Task<IActionResult> CommandersPartial(int page = 1, CancellationToken cancellationToken = default)
{
    if (!SameOriginRequestValidator.IsValid(Request))
        return StatusCode(403, new { Message = "Same-origin required." });

    page = Math.Max(page, 1);
    const int pageSize = AdminHarvestViewModel.DefaultDeckPageSize;
    var deckTotal = await _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken);
    var deckTotalPages = (int)Math.Ceiling((double)Math.Max(deckTotal, 1) / pageSize);
    page = Math.Min(page, deckTotalPages);
    var pagedCommanders = await _categoryStore.GetPagedProcessedCommandersAsync(page, pageSize, cancellationToken);

    return PartialView("_CommanderGrid", new CommanderGridViewModel
    {
        HarvestedCommanders = pagedCommanders,
        DeckPage = page,
        DeckPageSize = pageSize,
        DeckTotalCount = deckTotal,
    });
}
```

`SameOriginRequestValidator.IsValid` is already used on existing API endpoints in this
codebase — same pattern here.

---

## Question 6: Solution Layout and Dockerfile Implication

**Recommendation: Add `DeckFlow.Studio` to the existing `DeckFlow.sln`.**

**Rationale:**

- One solution, one `dotnet build DeckFlow.sln` command, one IDE workspace.
- Studio has a `<ProjectReference>` to Core. Core is in this solution. A separate `.sln`
  would require either a local NuGet feed or a relative path reference across solution
  boundaries — both add friction.
- VS Code / Rider / Visual Studio all handle multi-project solutions fine.
- No namespace collision: `DeckFlow.Studio.*` is distinct from `DeckFlow.Web.*`.

**The Dockerfile implication:**

The current `Dockerfile` copies only `DeckFlow.Core`, `DeckFlow.Web`, and `DeckFlow.CLI`
csproj files before `dotnet restore`. When `DeckFlow.Studio` is added to the solution,
`dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` continues to work without changes because
`DeckFlow.Studio` is NOT a transitive dependency of `DeckFlow.Web`. The solution file
references all projects, but the Dockerfile restores by project path, not by solution.

**No Dockerfile change needed** as long as the restore command stays
`dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` (current line 29). If the Dockerfile is
ever changed to `dotnet restore DeckFlow.sln`, it would attempt to restore Studio and
potentially pull in Blazor workload packages not present in the SDK image. The correct
mitigation is to add Studio to a `.slnf` (solution filter) that excludes it from CI/build
pipeline contexts, or keep the Dockerfile restore scoped to the Web project (recommended,
already the case).

**For `dotnet test` in CI (GitHub Actions):** The existing workflow runs
`dotnet test DeckFlow.Core.Tests` and `DeckFlow.Web.Tests`. Studio tests (if any) would be
in `DeckFlow.Studio.Tests` — add separately to the CI step. Studio is not tested in
the container build context.

**Summary of the Dockerfile rule:**

> Keep `Dockerfile` restore scoped to `DeckFlow.Web/DeckFlow.Web.csproj`. Do not change this
> to a solution-level restore. Document this constraint in a comment in the Dockerfile.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Studio Referencing CLI

Studio cannot reference CLI (executable project). Even if CLI were restructured as a library,
the namespace contamination and `System.CommandLine` transitive dependency would be
unnecessary. Extract to Core instead (Option C above).

### Anti-Pattern 2: Duplicating `DistillVideoAsync` in Studio

The distill pipeline has non-trivial LLM cost-safety logic: the `RecordCallAsync`-before-next-call
ordering (HIGH-1/FIX-1 from Phase 20 CR-01), the per-call cap checks, and the
`ValidateClips`/`ValidateSummary` guards. Duplicating this creates a second place for spend
overruns. Always route through the extracted `ContentKbOrchestrator`.

### Anti-Pattern 3: Writing `approval_status` to Prod Postgres

`approval_status` is a local curation column. Prod Postgres does not need it. If it were
added to prod, `UpsertRowPreservingVisibilityAsync` would need to preserve it, and the seed
loader would need to carry it — adding complexity for zero value. Keep it local-only.

### Anti-Pattern 4: Using Direct-Push Path for the Seed File

The commit path handles the seed JSON file. The direct-push path handles Postgres rows and
artifact files. Never SCP the `index-seed.json` to `/data` — that file is on the `/app`
filesystem (copied at Docker build time from the repo via `COPY content-kb/ ./content-kb/`).
SCP targets the `/data` volume. These are different filesystem locations. Mixing them would
create split-brain state.

### Anti-Pattern 5: Launching a Browser in `dotnet run` for Studio

The existing `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER` env var on `DeckFlow.Web`'s
`DevelopmentBrowserLauncher` exists because auto-launch was problematic. Studio should not
auto-launch a browser either — just log the localhost URL and let the operator open it. The
MEMORY note "never auto-launch DeckFlow web; ask user to start" applies equally to Studio.

---

## Integration Points Summary

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Studio ↔ Core orchestrator | Direct method call via `IContentKbOrchestrator` | DI-injected; no process boundary |
| Studio ↔ Local SQLite | `RelationalDatabaseConnection.FromSqlitePath(localDbPath)` | Same path as CLI; operator must not run CLI + Studio simultaneously on same DB |
| Studio ↔ Prod Postgres | `new RelationalDatabaseConnection(Postgres, prodConnStr)` | Connection string from user-secrets; only `ContentSiteIndexStore` writes; read-only for diff |
| Studio ↔ GitHub (commit path) | Shell-out to `git` (Process.Start) | Inherits SSH auth; reuses `ProcessOutput` pattern from `FfmpegAudioChunker` |
| Studio ↔ Render /data (artifact path) | Shell-out to `scp` | Requires one-time SSH key setup in Render dashboard; `scp -s` for SFTP mode |
| CLI ↔ Core orchestrator | Direct call (thin adapter in `ContentKbCommandRunners`) | No change to CLI's external interface (same commands, same flags) |
| Web ↔ Local seed file | `ContentKbSeedLoader.LoadIfPresentAsync` at startup | Unchanged; seed file written by Studio publish → commit → deploy triggers loader |
| Web AdminHarvestController ↔ CategoryKnowledgeStore | Existing + new `GET /Admin/Harvest/commanders` partial endpoint | No Core store changes for this feature |

---

## Sources

- `DeckFlow.CLI/ContentKbCommandRunners.cs` — verified all internal method signatures,
  interface dependencies, and Serilog coupling
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — verified self-healing ALTER pattern
  (`is_visible`, `is_evergreen`), `UpsertRowPreservingVisibilityAsync`, schema method
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — verified dual-dialect constructor
  pattern; confirmed Postgres connection string constructor path
- `DeckFlow.Web/Services/ContentKbSeedLoader.cs` — verified `UpsertRowPreservingVisibilityAsync`
  upsert behavior; confirmed seed entry schema (no `is_visible`, no `approval_status`)
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — verified existing
  `GetPagedProcessedCommandersAsync` call site; confirmed `SameOriginRequestValidator`
  usage pattern for AJAX endpoints; confirmed `StatusCacheKey` polling pattern
- `Dockerfile` — verified restore is scoped to `DeckFlow.Web/DeckFlow.Web.csproj` (line 29),
  not solution-level; confirmed `content-kb/` copied to runtime image
- `render.yaml` — confirmed `autoDeploy: true` on main branch; confirmed Postgres provider
  env var; confirmed `/data` disk mount at `/data`
- `DeckFlow.sln` — confirmed 5 projects; verified no `DeckFlow.Studio` entry exists yet
- `STACK.md` (sibling research) — confirmed Blazor Server recommendation, shell-out git
  pattern, Npgsql direct write, SCP for /data
- `FEATURES.md` (sibling research) — confirmed Option B (approval_status column) recommended
  over Option A (reuse is_visible); confirmed lazy-AJAX numbered pagination recommendation

---
*Architecture research for: DeckFlow v1.7 Local Harvest & Publish Studio*
*Researched: 2026-06-13*
