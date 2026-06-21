# Phase 61: Creator Sources & Selection - Context

Cycle 10 — Studio Automation, Sync & Polish. Phase 61 (after 59 + 60 + 63 complete).
Authored manually 2026-06-21 (operator chose manual planning in the cycle10 worktree; Codex to
peer-review before execute, per project rules).

## Phase Boundary

**In:** Operator manages a persisted curated creator/channel list and harvests *from* it — picks a
creator from a dropdown instead of pasting a URL each time, sees only not-yet-harvested videos by
default, and can quietly *skip* candidates (lighter than Block) and un-skip them.

**Out:** Status-badge consistency, multi-select ergonomics, creator FILTERING of review/publish/admin
lists, layout/nav polish, and the MainLayout About-link fix — all of that is Phase 62 (Studio UI
Polish). Phase 61 is the data-and-behavior pass over the harvest *selection* surface only.

**Requirements:** SRC-01, SRC-02, HSEL-01, HSEL-02, HSEL-03.

## Implementation Decisions (LOCKED)

### Persistence mirrors BlockedVideoStore exactly (SQLite in content-kb.db)
Two new Core stores, same shape/conventions as `DeckFlow.Core/Content/BlockedVideoStore.cs`
(constructor over a db path + a `RelationalDatabaseConnection` overload, `EnsureSchemaAsync`,
Dapper, no secrets):
- `CreatorSourceStore` → table `creator_sources` (SRC-01). Columns: surrogate id, `display_name`,
  `channel_ref` (the URL/handle/channel-id the operator pastes today), `added_utc`. Methods:
  `AddAsync`, `RemoveAsync(id)`, `ListAsync`, `EnsureSchemaAsync`. Dedupe on normalized `channel_ref`.
- `SkippedVideoStore` → table `skipped_videos` (HSEL-02/03). Columns: `youtube_video_id` (PK),
  `reason?`, `skipped_utc`. Methods: `AddSkipAsync`, `RemoveSkipAsync`, `IsSkippedAsync`,
  `ListSkippedAsync`, `EnsureSchemaAsync`. A SEPARATE table from `blocked_videos` — skip ≠ block.

Rationale: these are queryable lists with membership checks (exclude-from-selection), so SQLite (like
Block) fits better than the JSON `AutoApproveSettingsStore` pattern. content-kb.db is the existing
local store; both are registered as singletons in `Program.cs` over `contentKbDatabasePath`, exactly
like `BlockedVideoStore`. EnsureSchema is called at startup beside the other stores.

### Skip is distinct from Block (HSEL-02)
Skip = "don't surface this candidate in selection again." NO artifact hard-delete, NO
`blocked_videos` entry, NO harvest-blocklist semantics. Block stays exactly as is. The two lists are
independent; a video can be skipped without being blocked and vice-versa.

### Unharvested-only default reuses existing VideoStatus (HSEL-01)
The channel browse already computes per-video `VideoStatus` (`DeckFlow.Core/Content/VideoStatus.cs`:
NotHarvested/Harvested/Distilled/Approved/Published/Blocked/Duplicate). HSEL-01 default-filters the
browsed list to `NotHarvested` (and excludes Skipped), with a "Show all" toggle that reveals every
status. NO new status logic — reuse the Cycle 56/59 status the browse already attaches.

### Pages: dropdown on Harvest, management + skipped on their own pages
- SRC-02 creator dropdown: a `<select>` of saved creators added ABOVE the existing channel-URL input
  on `Harvest.razor`; selecting one populates the browse target. The paste-URL input REMAINS as the
  one-off fallback (SRC-02 explicitly keeps it).
- SRC-01 management (add/view/remove): a dedicated `CreatorSources.razor` page so `Harvest.razor`
  (already ~1970 lines) is not further bloated.
- HSEL-03 skipped list + un-skip: a dedicated `Skipped.razor` page mirroring the existing
  `Blocked.razor` (`/blocked`, 165 lines), reachable from the nav, with an Un-skip action per row.

### Skip/un-skip used directly via the store (not routed through the maintenance orchestrator)
Block/Unblock go through `IContentMaintenanceOrchestrator`; skip is simpler (a local list with no
artifact side effects), so the pages call `SkippedVideoStore` directly via DI. Documented divergence
to keep blast radius small; revisit only if skip ever needs orchestration.

## Canonical References (read in full before executing)

### Patterns to mirror
- `DeckFlow.Core/Content/BlockedVideoStore.cs` + `IBlockedVideoStore` — store shape for BOTH new stores.
- `DeckFlow.Core.Tests/Content/BlockedVideoStoreTests.cs` (if present) — test shape for the new stores.
- `DeckFlow.Studio/Pages/Blocked.razor` — page shape for `Skipped.razor`.
- `DeckFlow.Studio/Pages/Harvest.razor` §1 "Browse Channel or Playlist" (`_channelInput`,
  `_channelVideos`, `BrowseChannelAsync`, the per-row Block action, `VideoStatus`) — the dropdown,
  the unharvested filter/toggle, and the Skip action all attach here.
- `DeckFlow.Studio/AutoApproveSettingsStore.cs` — only as the "small persisted local state" reference;
  NOT the chosen mechanism for the lists.

### Project rules
- `CLAUDE.md`: Codex reviews plans + implements (cross-AI), Claude reviews; one type per file;
  `{ get; init; }` carve-out; LF endings; changed-lines format gate; xUnit for .NET Core test project;
  new tests required for new Core logic; README updated when behavior changes; no new packages.
- Build with Windows `dotnet.exe` in the worktree (Linux dotnet absent; Web .sln fails on tsc — build
  the Studio/Core projects individually). Studio tests: `DeckFlow.Studio.Tests` (bUnit). Note a known
  pre-existing parallel-isolation flake in `BlockedPageTests` (passes in isolation).

## Threat / safety notes (for per-plan STRIDE)
- `creator_sources.channel_ref` and `skipped_videos.youtube_video_id` are operator-entered and
  flow into YouTube browse + SQL — parameterize all SQL (Dapper) and treat values as untrusted
  (no string-concatenated SQL; validate/normalize the channel_ref before use).
- Local single-operator tool; stores hold no secrets (T-parity with AutoApproveSettings / Blocked).
- Skip must never delete artifacts or write `blocked_videos` (HSEL-02 invariant).

## Plan map
- 61-01 (wave 1, Core, autonomous): `CreatorSourceStore` + `SkippedVideoStore` + models + interfaces
  + EnsureSchema + xUnit tests. Foundation; no UI.
- 61-02 (wave 2, depends 61-01): SRC-01 `CreatorSources.razor` management page (add/view/remove) +
  SRC-02 creator dropdown on `Harvest.razor` + DI wiring + nav link + bUnit.
- 61-03 (wave 2, depends 61-02 — serializes on Harvest.razor): HSEL-01 unharvested-only default +
  "Show all" toggle + HSEL-02 per-row Skip action (excludes skipped from selection) + bUnit.
- 61-04 (wave 3, depends 61-01 + 61-03): HSEL-03 `Skipped.razor` list page + un-skip + nav + bUnit.
