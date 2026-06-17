# Project Research Summary

**Project:** DeckFlow v1.7 — Local Harvest & Publish Studio
**Domain:** Standalone local Blazor Server curation tool + admin grid perf fix
**Researched:** 2026-06-13
**Confidence:** HIGH

## Executive Summary

DeckFlow v1.7 adds a standalone local tool (DeckFlow.Studio) that gives the operator a browser-based UI for the entire content-KB pipeline: discover YouTube videos, harvest+distill them, review and approve in a queue, and publish to deckflow.gg via either a git commit-then-deploy path (primary) or a direct prod-Postgres + SCP write (secondary). The milestone also fixes the admin commander grid's synchronous count+aggregate query with lazy AJAX paging. The most important finding from all four research files is that no new NuGet packages are needed: YoutubeExplode 6.6.0 and Npgsql 10.0.0 are already in DeckFlow.Core, and all other operations use the existing RestSharp/Polly pattern, shell-out git/scp, and dotnet user-secrets.

The single biggest architectural decision — and a prerequisite to everything else — is extracting the harvest/distill orchestration out of DeckFlow.CLI into DeckFlow.Core as IContentKbOrchestrator. This is blocked by the fact that CLI is an executable project; Studio cannot reference it as a library. The extraction simultaneously closes the v1.6 backlog item "ContentKbCommandRunners god-class split." Once IContentKbOrchestrator lives in Core, the CLI's public Run*Async methods become thin path-resolver adapters, and Studio's Blazor services call the same domain logic directly via DI.

Three pitfalls impose hard ordering constraints on the build sequence. First, a safe content-columns-only upsert overload (UpsertContentColumnsOnlyAsync) must exist before any direct prod-write phase ships, because the two existing overloads both clobber is_visible/is_evergreen on UPDATE — silently erasing admin curation. Second, the approval_status column and filtered export (GetApprovedRowsAsync) must land before the commit-publish phase, otherwise rejected content enters the public repo's seed JSON and prod Postgres. Third, the Studio project scaffold with gitignore entries and user-secrets wiring must be the very first phase, because the prod connection string has no safe home until that foundation exists.

## Key Findings

### Recommended Stack

No new NuGet packages are required. YoutubeExplode 6.6.0 (already in DeckFlow.Core) covers all YouTube operations — channel listing, video metadata, and keyword search — with no API key and no quota ceiling. Npgsql 10.0.0 (already in Core) handles prod Postgres writes via the existing RelationalDatabaseConnection pattern. Git automation shells out to the system git binary using the existing ProcessOutput helper from FfmpegAudioChunker.cs; this inherits the developer's SSH auth automatically and avoids LibGit2Sharp's credential-resolution complexity. Secrets live in dotnet user-secrets (UserSecretsId GUID in the csproj; actual values outside the repo tree), consistent with Render's env-var pattern for the deployed app.

The Studio UI is ASP.NET Core + Blazor Server running on localhost via dotnet run. Blazor Server's SignalR connection handles real-time progress updates from the long-running harvest/distill pipelines via StateHasChanged(), and the async UI model is familiar from DeckFlow.Web's MVC/Razor skill. The Studio project is added to DeckFlow.sln but excluded from the Dockerfile restore (which stays scoped to DeckFlow.Web/DeckFlow.Web.csproj — do not change this to a solution-level restore).

**Core technologies:**
- ASP.NET Core + Blazor Server 10.0: Studio UI host — dotnet run on localhost, real-time progress via SignalR, ProjectReference to Core
- YoutubeExplode 6.6.0 (already in Core): channel listing + video metadata + keyword search — no API key, no quota
- Npgsql 10.0.0 (already in Core): direct prod Postgres writes via existing RelationalDatabaseConnection
- git CLI (shelled out): commit-then-deploy publish path — inherits SSH auth, reuses ProcessOutput pattern
- dotnet user-secrets: prod connection string + Render SSH address — stays outside the public repo tree
- scp CLI (shelled out): markdown artifact files to Render /data disk — secondary path only; requires one-time SSH key setup in Render dashboard

### Expected Features

**Must have (table stakes — v1.7 core):**
- Paste URL/ID → harvest+distill with LLM spend preview (dry-run gate before every distill)
- Distill-status tracking and per-item review queue (summary + clips + tags)
- Approve / reject individual items (approval_status column: pending / approved / rejected)
- Blocked-video management in UI (parity with existing CLI block/unblock/list)
- Seed-export + git commit-then-deploy publish path (proven mechanism, primary path)
- Lazy AJAX numbered paging for AdminHarvestController commander grid (independent, low-risk)

**Should have (v1.7 complete):**
- Channel browse UI — list recent videos by known handle/URL with harvested-status badge
- Multi-select batch harvest from browse results
- "What will change" diff before publish (git diff for commit path; prod-DB query for direct path)
- Direct prod-DB + SCP publish path (secondary; only after commit path is proven)
- Inline tag editing before publish

**Defer (v1.8+):**
- Real-time SSE/polling progress during harvest+distill (high complexity)
- YouTube Data API v3 creator search by name (quota risk — 100 units/call; handle browse covers the core case)
- Post-publish verification via HTTP scrape or prod-DB read
- Scheduled/cron harvest (local tool is not always running; defeats review-before-publish guarantee)

### Architecture Approach

The solution gains one new project (DeckFlow.Studio) and two new files in DeckFlow.Core (IContentKbOrchestrator, ContentKbOrchestrator). DeckFlow.CLI's ContentKbCommandRunners is thinned to path-resolver adapters. DeckFlow.Web is unchanged except for the lazy-paging partial endpoint on AdminHarvestController. The approval_status column is local-SQLite-only — it is never propagated to prod Postgres by either publish path. Prod Postgres has no concept of it.

**Major components:**
1. IContentKbOrchestrator / ContentKbOrchestrator (Core, NEW) — extracted domain logic for harvest, distill, block/unblock, corpus-reset, export; accepts interfaces, returns result records (not int exit codes); both CLI and Studio call it
2. DeckFlow.Studio Blazor Server app (NEW) — five Studio services (StudioHarvestService, StudioDistillService, StudioDiscoveryService, StudioReviewService, StudioPublishService) backed by Core interfaces; Blazor pages for Discovery, Queue, Publish, Admin
3. ContentSiteIndexStore + IContentSiteIndexStore (Core, MODIFIED) — add approval_status self-healing ALTER migration; add GetPendingApprovalAsync, SetApprovalStatusAsync, GetApprovedRowsAsync; add UpsertContentColumnsOnlyAsync overload that preserves is_visible/is_evergreen on UPDATE
4. ContentKbCommandRunners (CLI, MODIFIED) — public Run*Async become thin adapters; internal static domain methods move to Core
5. AdminHarvestController (Web, MODIFIED) — add GET /Admin/Harvest/commanders partial endpoint; initial Index renders skeleton, JS replaces grid on DOMContentLoaded

### Critical Pitfalls

1. **Direct prod-write clobbers is_visible/is_evergreen** — UpsertRowAsync overwrites every column including admin curation; UpsertRowPreservingVisibilityAsync hardcodes FALSE on INSERT and ignores visibility on UPDATE. A third overload (UpsertContentColumnsOnlyAsync) whose DO UPDATE SET clause explicitly excludes is_visible, is_evergreen, and approval_status is required before any direct prod-write phase. Add an integration test: upsert a row with is_visible=TRUE, call the new method, assert is_visible remains TRUE.

2. **Export includes unapproved entries in seed commit** — RunContentIndexExportAsync calls GetAllRowsAsync() with no filter. When the Studio commits the seed JSON, rejected/pending content enters the public repo and prod Postgres. Prevention: Studio publish must call a filtered GetApprovedRowsAsync() export; add --approved-only flag to CLI export command. The approval_status column must exist before the commit-publish phase ships.

3. **Secret leakage to public repo** — three sub-modes: appsettings file committed, connection string in Serilog structured logs, or secrets.json inside the Studio project directory. Prevention: dotnet user-secrets exclusively; DeckFlow.Studio/appsettings*.local.json and secrets.json added to .gitignore on project creation; never pass the connection string to any ILogger call (log "configured" / "not configured" only).

4. **Partial write — DB row written but SCP failed** — direct push path creates a content_site_index row with an artifact_path referencing a file that never arrived on /data. Prevention: implement as two explicit sequential UI steps — Step 1 SCP artifacts, Step 2 push DB rows. Step 2 is blocked if Step 1 fails. File-first ordering must be in the plan's success criteria.

5. **YoutubeExplode AngleSharp concurrency** — concurrent calls to IYouTubeChannelVideoLister corrupt each other's parse output (same bug hit in v1.6, resolved by serializing to concurrency=1). Prevention: SemaphoreSlim(1) in StudioDiscoveryService; no Task.WhenAll across lister invocations.

## Implications for Roadmap

Based on research, the recommended build sequence is 8 phases. Phases 1-5 are strictly ordered by dependency. Phase 6 (admin grid fix) is fully independent and can be scheduled anywhere. Phases 7-8 depend on Phase 5 and Phase 4 respectively.

---

### Phase 1: Studio Scaffold + Secrets Wiring
**Rationale:** The prod connection string has no safe home until this project exists with .gitignore entries and user-secrets configured. All subsequent Studio phases depend on this foundation. Must come first — Pitfall 3 (secret leakage) is a permanent risk if any Studio config file is committed before .gitignore is wired.
**Delivers:** DeckFlow.Studio project in solution; ProjectReference to Core; dotnet user-secrets init; appsettings*.local.json + secrets.json added to .gitignore; Program.cs DI scaffold; first Blazor page renders at http://localhost:<port>; Dockerfile constraint documented (restore stays scoped to Web project)
**Avoids:** Pitfall 3 (secret leakage via appsettings commit)
**Research flag:** Standard patterns — Blazor Server scaffold is well-documented; no deeper research needed

---

### Phase 2: Extract IContentKbOrchestrator to Core
**Rationale:** The architectural blocker. CLI is an executable; Studio cannot reference it. This extraction unlocks all subsequent harvest/distill/export work in Studio. Also closes the v1.6 backlog god-class split. Must precede any phase that invokes pipeline logic from Studio.
**Delivers:** IContentKbOrchestrator + ContentKbOrchestrator in DeckFlow.Core/Content/; ContentKbCommandRunners public Run*Async thinned to adapters; all existing CLI tests continue to pass (internal test surface moves to Core with InternalsVisibleTo); ILogger dependency changed from Serilog.ILogger to ILogger<ContentKbOrchestrator> (MEL abstraction — Core convention is Microsoft.Extensions.Logging.Abstractions)
**Avoids:** Anti-pattern of duplicating distill pipeline in Studio; god-class accumulation
**Research flag:** Standard patterns — the extraction is mechanical (internal static methods move to Core). Open question: confirm whether Core already has a transitive Serilog reference (see Gaps section).

---

### Phase 3: approval_status Column + Safe Upsert Overload
**Rationale:** Two of the three hard ordering constraints from PITFALLS converge here. The safe UpsertContentColumnsOnlyAsync overload must exist before any direct prod-write phase. The approval_status column + filtered export must exist before the commit-publish phase. Both constraints are satisfied by this single phase. This phase has no Studio UI dependency — it is pure Core + schema work.
**Delivers:** approval_status column on content_site_index via self-healing ALTER migration (same pattern as is_evergreen); GetPendingApprovalAsync, SetApprovalStatusAsync, GetApprovedRowsAsync on IContentSiteIndexStore; UpsertContentColumnsOnlyAsync overload whose DO UPDATE excludes is_visible, is_evergreen, approval_status; integration test: set is_visible=TRUE, call new overload, assert unchanged; filtered export (Studio calls GetApprovedRowsAsync, not GetAllRowsAsync)
**Avoids:** Pitfall 1 (is_visible/is_evergreen clobbered on prod write); Pitfall 4 (unapproved entries in seed commit)
**Research flag:** Standard patterns — self-healing ALTER is established in this codebase; no research needed

---

### Phase 4: Harvest + Distill UI (Studio core pipeline)
**Rationale:** First Studio phase that delivers end-user value. Depends on Phase 2 (orchestrator in Core) and Phase 1 (Studio scaffold). The distill dry-run gate (Pitfall 5) and Blazor background-task pattern (Pitfall 7) must be requirements in the plan, not enhancements.
**Delivers:** Paste URL/ID form → harvest via IContentKbOrchestrator.HarvestAsync; LLM spend dry-run shown before distill; distill via IContentKbOrchestrator.DistillAsync; dedup surfacing; background Task.Run + InvokeAsync(StateHasChanged) pattern with CTS tied to component disposal; blocked-video management page (100% reuse of existing CLI block/unblock/list)
**Avoids:** Pitfall 5 (re-distill LLM spend surprise — dry-run gate required); Pitfall 7 (Blazor circuit blocking — background Task pattern required)
**Research flag:** Needs plan-time design decision: specify the exact IAsyncDisposable CTS teardown pattern to avoid orphaned operations after circuit close

---

### Phase 5: Review Queue + Commit-Publish Path
**Rationale:** The review queue (approve/reject) and the commit-publish path are tightly coupled — you cannot publish without approved entries, and the export filter from Phase 3 gates what enters the seed. The two-stage commit/push separation (Pitfall 8) must be a plan requirement.
**Delivers:** Review queue page showing approval_status='pending' entries with summary + clips + tags preview; approve/reject actions per item; seed export filtered to approval_status='approved' rows; two-stage publish: Stage 1 = git commit (shows diff first, reversible); Stage 2 = git push (separate button, requires checkbox acknowledge); CRLF prevention in exported JSON (LF forced in write step, not relying on gitattributes normalization)
**Avoids:** Pitfall 4 (unapproved entries in seed commit); Pitfall 8 (accidental push to main without review); Pitfall 10 (CRLF in seed JSON)
**Research flag:** Standard patterns — git shell-out and two-stage UI are well-understood; no research needed

---

### Phase 6: Admin Commander Grid Lazy AJAX Paging (independent)
**Rationale:** Fully independent of the Studio pipeline. Can be scheduled at any point. Fixes the existing /Admin/Harvest slow initial load caused by GetDistinctProcessedCommanderCountAsync + GetPagedProcessedCommandersAsync running synchronously on every page request. The missing partial expression index on LOWER(commander_name) WHERE processed=1 should ship with this phase.
**Delivers:** New GET /Admin/Harvest/commanders partial endpoint on AdminHarvestController; _CommanderGrid.cshtml Razor partial; JS/TS page-click handler (replaces only the grid section via fetch + DOM replace, matching existing AdminHarvest/Status polling pattern); SameOriginRequestValidator on the new endpoint; partial expression index on LOWER(commander_name) WHERE processed=1 in CategoryKnowledgeRepository; initial Index renders skeleton (no count/aggregate queries on first load)
**Uses:** Existing GetPagedProcessedCommandersAsync and GetDistinctProcessedCommanderCountAsync — no store changes needed
**Avoids:** Performance trap: count aggregate query on every page load
**Research flag:** Standard patterns — AJAX partial replacement is established in this codebase; no research needed

---

### Phase 7: Channel Browse UI + Discovery
**Rationale:** Depends on Phase 4 (Studio pipeline running). Adds the discovery workflow so the operator can browse channels without copy-pasting video IDs. AngleSharp SemaphoreSlim(1) constraint must be in the plan.
**Delivers:** Channel handle/URL input → IYouTubeChannelVideoLister.ListRecentAsync → video grid with thumbnail (img src to i.ytimg.com), duration, published date, harvested-status badge (batch GetVideoByYoutubeIdAsync overlay); multi-select → batch harvest; SemaphoreSlim(1) in StudioDiscoveryService enforced
**Avoids:** Pitfall 6 (AngleSharp concurrency — SemaphoreSlim required)
**Research flag:** Standard patterns — channel listing via existing interface; no research needed

---

### Phase 8: Direct Prod-DB + SCP Publish Path
**Rationale:** Depends on Phase 3 (safe upsert overload exists) and Phase 5 (commit path proven). Secondary publish path — skips the 2-4 min Render deploy cycle. The file-first ordering (SCP before DB push) and two-stage UI confirmation are non-negotiable requirements from Pitfall 2. Render SSH key setup is a one-time manual gate.
**Delivers:** StudioPublishService.PublishViaDirectPushAsync() — Step 1: SCP all approved artifact .md files to Render /data (tar bundle for >20 files); Step 2: construct prod Postgres ContentSiteIndexStore from user-secrets connection string, call UpsertContentColumnsOnlyAsync per approved row; StudioPublishService.GetProdDiffAsync() — query prod Postgres for existing natural keys, diff against local approved rows; schema migration runs via EnsureSchemaAsync on first prod connect; post-push verification query for dangling artifact_path rows
**Avoids:** Pitfall 1 (is_visible clobber — uses safe overload from Phase 3); Pitfall 2 (partial write — SCP before DB push, Step 2 blocked if Step 1 fails); Pitfall 9 (schema drift — EnsureSchemaAsync runs on prod connect)
**Research flag:** SCP bundling approach (tar + single handshake for large artifact sets) and Render SSH key registration are operationally new — plan should include a manual setup checklist

---

### Phase Ordering Rationale

- Phase 1 before everything: secrets have no safe home until .gitignore + user-secrets are wired
- Phase 2 before Phases 4-5-7-8: CLI cannot be referenced as a library; orchestration must be in Core first
- Phase 3 before Phase 5: export filter blocks on approval_status column; safe upsert blocks on Phase 8
- Phase 4 before Phase 5: queue cannot show reviewed items until harvest+distill pipeline runs
- Phase 5 before Phase 8: direct push is secondary; commit path must be proven first
- Phase 4 before Phase 7: channel browse is a discovery-to-harvest flow; harvest UI must exist first
- Phase 6 is independent: no dependency on Studio pipeline; schedule at any point (good early win)

### Research Flags

Phases needing specific design decisions during planning:
- **Phase 4:** Blazor Server background-task + IAsyncDisposable CTS teardown pattern — plan must specify the exact lifecycle approach to avoid orphaned operations after circuit close
- **Phase 8:** Render SSH key setup procedure and SCP tar-bundle strategy for large artifact sets — plan should include a manual ops checklist

Phases with standard, well-documented patterns (no research phase needed):
- **Phase 1:** Blazor Server scaffold + user-secrets — standard .NET 10 CLI commands
- **Phase 2:** Mechanical code extraction — internal static methods moved to Core; no new patterns
- **Phase 3:** Self-healing ALTER migration — established pattern in this codebase (is_evergreen precedent)
- **Phase 5:** Git shell-out + two-stage commit/push — ProcessOutput pattern already in Core
- **Phase 6:** AJAX partial replacement — existing pattern in AdminHarvest/Status polling
- **Phase 7:** Channel browse via IYouTubeChannelVideoLister — existing interface, no new dependencies

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All key deps verified directly in csproj files; Render SCP verified against official docs; LibGit2Sharp tradeoffs verified against NuGet |
| Features | HIGH | Grounded in existing codebase; every feature traced to a specific existing method or a bounded new addition |
| Architecture | HIGH | All component boundaries verified by reading the actual files named; no speculative claims |
| Pitfalls | HIGH | Every pitfall traced to a specific file, SQL statement, or documented incident (MEMORY: harvest_lister_concurrency_crash, Phase 20 CR-01 spend-recording order) |

**Overall confidence:** HIGH

### Gaps to Address

These are open questions that need a human decision before or during planning — not research gaps:

- **Studio in DeckFlow.sln vs separate solution:** Architecture research recommends adding to DeckFlow.sln (one workspace, one build, no cross-solution reference friction). The Dockerfile constraint (restore stays scoped to DeckFlow.Web/DeckFlow.Web.csproj) is already identified and safe. Recommended path unless the user prefers a clean separation. **Decision needed from user before Phase 1 plan.**

- **ILogger abstraction in Core orchestrator:** ContentKbCommandRunners.cs currently passes Serilog.ILogger to its internal methods. Moving them to Core should switch to ILogger<ContentKbOrchestrator> (MEL abstraction — Core convention is Microsoft.Extensions.Logging.Abstractions). Verify whether Core already takes a transitive Serilog reference that would make this a no-op concern. **Confirm during Phase 2 plan.**

- **Prod connection string storage location:** Research recommends dotnet user-secrets. The alternative (env var STUDIO_PROD_CONNECTION_STRING set in terminal before dotnet run) is also valid and avoids the user-secrets init step. Both are safe for the public repo. **Decision needed before Phase 1 plan.**

- **approval_status scope:** Architecture research recommends the column is local-SQLite-only and never propagated to prod. If the admin ever wants to audit which videos were approved vs rejected, the history would be local-only and lost on a DB reset. **Confirm this is acceptable during Phase 3 plan.**

## Sources

### Primary (HIGH confidence — verified directly against codebase)
- DeckFlow.Core/DeckFlow.Core.csproj — YoutubeExplode 6.6.0, Npgsql 10.0.0 confirmed as existing deps
- DeckFlow.Core/Content/ContentSiteIndexStore.cs — UpsertSql behavior, UpsertPreservingVisibilitySql behavior, self-healing ALTER pattern
- DeckFlow.CLI/ContentKbCommandRunners.cs — internal static method signatures, GetAllRowsAsync export, video-IDs distill bypass, spend-recording order
- DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs — AngleSharp concurrency constraint, concurrency=1 fix
- DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs — synchronous count+page queries on Index; SameOriginRequestValidator pattern
- DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs — COUNT(DISTINCT LOWER(commander_name)) full table scan; no index on LOWER(commander_name)
- DeckFlow.Web/Services/ContentKbSeedLoader.cs — UpsertRowPreservingVisibilityAsync upsert behavior on deploy
- Dockerfile — restore scoped to DeckFlow.Web/DeckFlow.Web.csproj (line 29)
- render.yaml — autoDeploy:true, /data disk mount, Postgres provider env var

### Primary (HIGH confidence — official docs)
- Render Persistent Disks docs (https://render.com/docs/disks) — SCP is the only file-write mechanism; no REST API for /data; Starter plan supported
- Render SSH docs (https://render.com/docs/ssh) — SSH available on Starter+; scp -s supported
- YouTube Data API v3 quota table (https://developers.google.com/youtube/v3/determine_quota_cost) — search.list = 100 units/call, 100-calls/day dedicated bucket
- Microsoft user-secrets docs (https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — works in non-ASP.NET projects via ConfigurationBuilder.AddUserSecrets<Program>()
- NuGet LibGit2Sharp 0.31.0 (https://www.nuget.org/packages/LibGit2Sharp/) — .NET 10 compatible; SSH credential-resolution complexity documented

### Secondary (MEDIUM confidence — inference from codebase patterns)
- ProcessOutput pattern from FfmpegAudioChunker.cs — assumed to generalize to git/scp shell-out; not directly verified for git use case
- Blazor Server SignalR circuit timeout (~3 minutes default) — standard framework behavior; not verified against a specific .NET 10 changelog entry

---
*Research completed: 2026-06-13*
*Ready for roadmap: yes*
