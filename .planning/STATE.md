---
gsd_state_version: 1.0
milestone: v1.7
milestone_name: Local Harvest & Publish Studio
status: executing
stopped_at: Phase 47 Plan 03 Task 2 complete — awaiting Task 3 blocking-human checkpoint
last_updated: "2026-06-16T21:29:43.986Z"
last_activity: 2026-06-16 -- Phase 47 Plan 03 Tasks 1-2 executed (DirectPush page + 11 bUnit tests)
progress:
  total_phases: 10
  completed_phases: 8
  total_plans: 32
  completed_plans: 31
  percent: 84
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 47 — Direct Prod-DB + SCP Publish Path

## Current Position

Phase: 47 (Direct Prod-DB + SCP Publish Path) — EXECUTING (Plan 03 awaiting blocking-human checkpoint)
Plan: 3 of 3 (Plans 01-02 complete; Plan 03 Tasks 1-2 done, Task 3 checkpoint pending)
Status: Executing Phase 47 — NOT complete (Task 3 blocking-human gate outstanding)
Execution order: 49 → 44 → 45 → 46 → 47 (48 independent; 50 after 44+49)
Last activity: 2026-06-16 -- Phase 47 Plan 03 Tasks 1-2 executed (DirectPush page + 11 bUnit tests)

```
Progress: [██████████] 100%
```

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 41 | Studio Scaffold + Secrets Wiring | STU-01, STU-02, STU-03 | Complete |
| 42 | Orchestrator Extraction | ORCH-01, ORCH-02 | Not started |
| 43 | Approval Status + Safe Upsert | REVQ-01, PUB-01, PUB-02 | Not started |
| 44 | Admin Grid Lazy Paging | GRID-01, GRID-02 | Not started |
| 45 | Harvest + Distill UI | HARV-01..05 | Complete |
| 46 | Review Queue + Commit-Publish Path | REVQ-02, REVQ-03, PUB-03 | Not started |
| 47 | Direct Prod-DB + SCP Publish Path | PUB-04, PUB-05 | Not started |
| 48 | UI Audit + Remediation | UIR-01, UIR-02, UIR-03 | Not started |

**Phase ordering notes:**

- 41 before everything: secrets have no safe home until .gitignore + user-secrets are wired
- 42 before 45/46/47: CLI is an executable; orchestration must be in Core before Studio can call it
- 43 before 46 (approval_status column + filtered export prerequisite) and before 47 (safe upsert prerequisite)
- 45 before 46: review queue has nothing to show until harvest+distill pipeline runs
- 46 before 47: direct push is secondary; commit path must be proven first
- 44 and 48 are independent: no Studio dependency; can run in any order relative to other phases

## Performance Metrics

**Velocity (v1.6 reference — most recent shipped):**

- ~122 commits across 8 phases (2026-06-10 → 2026-06-12, 3 days)
- Cross-AI execution pattern: Codex codes, Claude reviews
- Final test gate: Core 320/0, Web 593/0/5-skip; build 0/0

**v1.7 Phase Forecast:**

| Phase | Req-IDs | Key Constraint |
|-------|---------|---------------|
| 41 — Studio Scaffold | STU-01..03 | Must be first; Pitfall 3 (secret leakage) is permanent risk if gitignore not wired first |
| 42 — Orchestrator Extraction | ORCH-01..02 | Architectural blocker; CLI executable cannot be referenced by Studio |
| 43 — Approval Status + Safe Upsert | REVQ-01, PUB-01..02 | Pitfall 1 (is_visible clobber) + Pitfall 4 (unapproved in seed) require this before publish |
| 44 — Admin Grid Lazy Paging | GRID-01..02 | Independent quick-win; Pitfall (count aggregate on every page load) |
| 45 — Harvest + Distill UI | HARV-01..05 | Blazor background-task pattern required (Pitfall 7: circuit blocking); SemaphoreSlim(1) on lister (Pitfall 6) |
| 46 — Review Queue + Commit-Publish | REVQ-02..03, PUB-03 | Two-stage commit/push (Pitfall 8); LF normalization (Pitfall 10) |
| 47 — Direct Prod-DB + SCP Publish | PUB-04..05 | File-first ordering: SCP before DB push (Pitfall 2); plan needs Render SSH key setup checklist |
| 48 — UI Audit + Remediation | UIR-01..03 | Browser screenshots at ≥2 viewports required; grep-only insufficient |
| Phase 45 P01 | 25m | 3 tasks | 9 files |
| Phase 45 P02 | ~25m | 2 tasks | 6 files |
| Phase 46-review-queue-commit-publish-path P05 | 15 | 2 tasks | 1 files |
| Phase 47-direct-prod-db-scp-publish-path P02 | ~10m | 2 tasks | 2 files |

## Accumulated Context

### Decisions

- **v1.7 roadmap created 2026-06-13:** 8 phases (41-48), 23/23 requirements mapped.
- **Granularity:** Config = coarse, but hard ordering constraints from research PITFALLS.md require 8 phases. Compressing below this would merge safety-critical boundaries (secret leakage, is_visible clobber, unapproved entries in seed).
- **Phase 44 scheduled as independent quick-win:** GRID-01/02 have no Studio dependency and fix an existing live performance issue on /Admin/Harvest. Can be executed at any point.
- **Phase 48 scheduled last and independent:** UIR-01/02/03 are fully independent of the Studio track and can run in parallel with or after any other phase.
- **ORCH-01 (Phase 42) closes arch-review Finding C** from the v1.6 backlog (ContentKbCommandRunners god-class split) as a side-effect of v1.7 Studio work.
- **approval_status column is LOCAL-ONLY:** The column lives only in the local SQLite DB. It is not propagated to prod Postgres by either publish path. Prod DB has no concept of it (confirmed: ARCHITECTURE.md).
- **No new NuGet packages:** YoutubeExplode 6.6.0 and Npgsql 10.0.0 already in DeckFlow.Core. Git/SCP shell-out via ProcessOutput pattern. No LibGit2Sharp.
- **Dockerfile stays project-scoped:** `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` is the restore command in the Dockerfile. Adding Studio to the solution does NOT change this. Adding Studio to .sln is correct; changing restore to solution-level is a constraint violation.
- **Studio binds to localhost only:** `applicationUrl` in Studio launchSettings.json must be `http://localhost:{port}` only; no LAN exposure.
- **Corpus-reset not exposed in Studio UI:** `RunCorpusResetAsync` is CLI-only. Emergency operation; exposing it in a UI without typing confirmation is a security mistake (PITFALLS.md).
- **Phase 45-02: Single providerEnv read closes HIGH-1:** `DECKFLOW_LLM_PROVIDER` read once in Program.cs; both `LlmDistillationProviderFactory.Resolve` and `isSubscriptionProvider` derive from the same var — distiller and spend flag cannot disagree.
- **Phase 45-02: VideoStatusResolver in Core (not Studio):** Pure store-query badge-resolution logic lives in DeckFlow.Core so Core.Tests can unit-test it without inverting project dependencies (HIGH-2).
- **Phase 45-02: Override-aware ledger is a single shared singleton:** `SessionCapOverride` captured in resolver closure; same ledger instance injected into both Harvest page and orchestrator so `WouldExceedCapAsync` sees the override (T-45-04 / Pitfall 6).
- **Phase 45-04: Distill spend gate COMPLETE + human-verify PASS (2026-06-15):** Tasks 1+2 committed (4f3c2df). Two-stage spend gate with dry-run projection, re-distill double-confirm (redistillConfirmed gates both distillIds and redistill: named arg, HIGH-4), monthly cap display + session override (D-03), cap-exceeded block, Stage B reviewed-spend confirm, actual spend + failure reporting, badge + cap refresh after distill. Task 3 human-verify PASS: cap display, HIGH-1 provider→badge (Metered vs Subscription $0) verified live, dry-run/Stage-B gating, re-distill amber banner + session cap-raise, no secrets in page/logs, 0 ObjectDisposedException/unobserved exceptions, and a live $0 claude-CLI distill confirmed end-to-end (badges flip to Distilled, per-video + total timing, cancel works). Phase 45 all 4 plans complete. Related dogfood follow-up quick tasks: 260615-h2v, k8o, p4d, c9e, t7m, q3n (+ skip-estimate-on-subscription).
- **Phase 47-01: Wave-0 scaffold complete (2026-06-16):** Commits a1d14ed/a9f272d/e687b9b. SSH.NET 2025.0.0 added to DeckFlow.Studio ONLY (D-01 approved exception; absent from Tests + Core). `ISshArtifactUploader` takes `SshUploadRequest(LocalPath, RemoteRelativePath)` NOT `IReadOnlyList<string>` (Codex HIGH-1 path-traversal guard) — this supersedes the string-list shape shown in 47-PATTERNS.md/47-RESEARCH.md; `SshUploadResult` also carries `RemoteRelativePath` for the SC4 per-file reconcile key. `ProdStoreFactory.Create` builds on-demand Postgres `ContentSiteIndexStore` via `PostgresConnectionStringNormalizer.Normalize` + `RelationalDatabaseConnection(Postgres,...)` (D-03; no Core change). `StudioConfig` gained `IsScpConfigured`; Program.cs:47 temp 2-arg `new StudioConfig(isProdConfigured, false)` with `// TODO(47-02)` — Plan 02 wires real SCP detection. `FakeContentSiteIndexStore.UpsertMethodCalls` records method names so SC3/D-08 (only `UpsertContentColumnsOnlyAsync` on prod) is assertable. `DirectPushPageTests` stubs 8 named facts (Render<DirectPush> commented, TODO 47-03). Build 0/0 (1 pre-existing Core CS1574 cref warning, out of scope); Studio suite 29/29 green; --filter DirectPush 8/8.
- **Phase 47-03: DirectPush page + 11 bUnit tests complete; Task 3 checkpoint PENDING (2026-06-16):** Commits 6026419 (Task 1, pre-existing — DirectPush.razor 3-stage gated PROD page @page /direct-push + NavMenu "Direct Push" entry) / ca9d824 (Task 2, this resume — 11 named bUnit tests). The page calls `UpsertContentColumnsOnlyAsync` ONLY (SC3/D-08; no full-row upsert in the write path), the WriteRowsAsync hard-guard `if (!_scpSuccess || _operationInFlight || !_diffReady) return;` (MEDIUM-1), and sanitized-literal catches for diff-read + DB-write (never ex.Message, HIGH-2). Tests: artifact-first gating, safe-upsert-only, SCP/DB partial-failure reconcile, presence-only secrets proven via sentinel-bearing exception messages ("Host=...;Password=hunter2") driven through both failure catches, and the not-configured warning/disabled gate. FakeContentSiteIndexStore gained KeysToFailOnUpsert/UpsertFailureMessage/ReadFailureMessage fault-injection + full-row-upsert throw-guard (MEDIUM-4). One Rule-1 test bug fixed on resume: `DirectPush_NotConfigured_ButtonsDisabled` rendered 3 components in one BunitContext (illegal after first service-resolve) → converted to a `[Theory]` (3 InlineData variants, each its own context). InternalsVisibleTo(DeckFlow.Studio.Tests) + DirectPush.InvokeWriteRowsForTest seam committed with Task 2 (exist solely to enable the MEDIUM-1 hard-guard test). Build 0/0; `--filter DirectPush` 13/13 (11 facts; not-configured expands to 3 theory cases); full Studio suite 34/34. LF-clean; format gate exit 0. **Task 3 (SSH.NET 2025.0.0 supply-chain confirmation + PROD-write UI human-verify) is a `gate="blocking-human"` checkpoint, NOT auto-approvable (workflow.auto_advance ignored, T-47-SC) — left untouched for the orchestrator. Phase 47 is NOT complete until that gate is approved.**
- **Phase 47-02: SFTP transport + Studio wiring complete (2026-06-16):** Commits 9d45dc4/e85312b. `SftpArtifactUploader` (SSH.NET `SftpClient`) implements the Wave-1 request-based `ISshArtifactUploader` (`IReadOnlyList<SshUploadRequest>` -> `SshUploadResult{LocalPath,RemoteRelativePath,Success,FailureReason}`) — NOT the `IReadOnlyList<string>` shape in 47-PATTERNS/47-RESEARCH. Per-file results, never throws on single-file failure; one client per call + sequential uploads (Pitfall 5); `EnsureRemoteDirectory` walks each `/`-segment under root (Pitfall 6/MEDIUM-3); `TryBuildRemotePath` rejects rooted/`..`/out-of-root paths (T-47-02c/V5); only the sanitized literal `"SSH upload failed — check SCP configuration and Render SSH access."` is ever surfaced — never `ex.Message` (D-07/Pitfall 3). Program.cs: real presence-only `isScpConfigured` (Host+Username+KeyFile+RemoteArtifactRoot; KeyPassphrase excluded), `new StudioConfig(isProdConfigured, isScpConfigured)` replaces the `false`/`TODO(47-02)` stub, registers `ISshArtifactUploader`->`SftpArtifactUploader` + `IProdStoreFactory`->`ProdStoreFactory`, presence-only `"Studio SCP: configured/not configured"` log; prod conn string still on-demand only (D-03, never a DI singleton). Build 0/0 (1 pre-existing Core CS1574, out of scope); --filter DirectPush 8/8 (no regression). Plan 03 (DirectPush.razor page + tests) remains.
- **Phase 45-03: Harvest page complete + human-verify PASS (2026-06-15):** Playwright smoke on `:5271` — HARV-01..04 + cancel-on-dispose + no-secrets all PASS; 0 ObjectDisposedException, 0 unobserved exceptions; circuit stayed responsive. CAVEAT (non-blocking): full success-stream + mid-flight cancel not exercised (local data dir had 0 enabled YouTube sources → orchestrator "0 sources enabled" guard hit immediately); environment-data condition, not a code defect. Re-exercise full success-stream + mid-flight cancel once a local data dir with an enabled source exists.

### Key Pitfalls to Watch (from research/PITFALLS.md)

- **Pitfall 1 (Phase 47):** `UpsertRowAsync` overwrites `is_visible`/`is_evergreen`. Use only `UpsertContentColumnsOnlyAsync` for prod writes. Integration test: set is_visible=TRUE, call new overload, assert unchanged.
- **Pitfall 2 (Phase 47):** SCP before DB push. Step 2 unreachable if Step 1 failed. File-first ordering enforced in code.
- **Pitfall 3 (Phase 41):** Secret leakage via appsettings. Gitignore entries before any config file is created. Never log the connection string.
- **Pitfall 4 (Phase 43/46):** Export-all includes unapproved entries. Filter at query level via GetApprovedRowsAsync.
- **Pitfall 5 (Phase 45):** Re-distill LLM spend. Dry-run gate required before every distill. Re-distill of known-distilled video shows explicit warning + secondary confirm.
- **Pitfall 6 (Phase 45):** AngleSharp concurrency. SemaphoreSlim(1) on all IYouTubeChannelVideoLister calls. No Task.WhenAll over lister.
- **Pitfall 7 (Phase 45):** Blazor circuit blocking on long-running harvest/distill. Use `Task.Run` + `InvokeAsync(StateHasChanged)` + CTS tied to component IDisposable.
- **Pitfall 8 (Phase 46):** Accidental git push before diff review. Two-stage commit/push with checkbox acknowledge.
- **Pitfall 9 (Phase 43):** Schema drift local vs prod. EnsureSchemaAsync called at Studio startup on both connections before UI is enabled.
- **Pitfall 10 (Phase 46):** CRLF in index-seed.json on Windows. Force LF in write step via JsonWriterOptions.NewLine or .Replace.

### Open Research Questions (to resolve during planning)

- **Phase 41:** `dotnet user-secrets` vs env var (`STUDIO_PROD_CONNECTION_STRING`) for prod connection string. Both safe for public repo. Decision needed before Phase 41 plan.
- **Phase 47:** Render SSH key registration (one-time manual gate) + SCP tar-bundle strategy for >20 files. Plan should include ops checklist.

### Pending Todos

- 15 pre-v1.5 open artifacts (stale 999.x/v13 debug sessions, May quick-task refs, empty todos) — acknowledged cruft; clean via `/gsd-cleanup` when convenient.

### Blockers/Concerns

- None at roadmap creation.

### Quick Tasks Completed (v1.6 era, carried for context)

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260611-p9x | Fix Primer copy buttons (port data-copy-target wiring into primer-selection.ts + Vitest test) | 2026-06-12 | 29cf22e | [260611-p9x](./quick/260611-p9x-fix-primer-copy-buttons-port-data-copy-t/) |
| 260612-kb1 | Admin KB per-entry Delete + restore Phase-37-broken page behaviors (filter/confirms/toast) | 2026-06-12 | e3b6ea4 | [260612-kb1](./quick/260612-kb1-admin-kb-delete-entry/) |
| 260612-e2e | Playwright E2E smoke suite (15 routes × desktop+mobile) + CI stage; 68/68 green | 2026-06-12 | 6e8ddff | [260612-e2e](./quick/260612-e2e-playwright-smoke/) |
| 260615-k8o | Add skip/offset to channel browse: IYouTubeChannelVideoLister + 5 implementers + Studio Skip input | 2026-06-15 | a018684 | [260615-k8o](./quick/260615-k8o-channel-browse-skip/) |
| 260615-h2v | Auto-ensure YouTube source from browsed channel before harvest: GetSourceByUrlAsync + EnsureYoutubeSourceAsync + Harvest.razor wire-up | 2026-06-15 | 2db4513 | [260615-h2v](./quick/260615-h2v-harvest-source-autoensure/) |
| 260615-p4d | DB-backed "Load harvested (pending distill)" loader: PendingDistillVideo + IDistillOrchestrator.ListPendingDistillAsync (union/dedup/null-skip) + Harvest.razor loader table | 2026-06-15 | 83ecaa5 | [260615-p4d](./quick/260615-p4d-pending-distill-loader/) |
| 260615-c9e | Typed LlmCliConfigurationException + orchestrator config-abort: one clear "Distiller CLI not configured" abort instead of N silent "distill failed" lines when DECKFLOW_LLM_CLI_COMMAND is missing/invalid | 2026-06-15 | 094c5a8 | [260615-c9e](./quick/260615-c9e-distill-config-error/) |
| 260615-t7m | Per-video elapsed time (N.Ns) in Core distill progress lines + live Elapsed counter and Total time display in Studio Harvest page via PeriodicTimer ticker | 2026-06-15 | d6f58e7 | [260615-t7m](./quick/260615-t7m-distill-timing/) |
| 260615-q3n | ChannelId/ChannelTitle on YouTubeChannelVideo; ListPlaylistAsync (default-throw on interface, real impl); Studio per-channel harvest grouping + playlist browse; remove Browse-first gate | 2026-06-15 | 7661107 | [260615-q3n](./quick/260615-q3n-queue-harvest-channel/) |

## Deferred Items

**Resolved in v1.6:**

- ✅ DeckController + CommandRunners SRP split (Phase 38, SRP-01..03; route-parity + live smoke)
- ✅ IDeckEntryLoader.LoadFromSourceAsync + IScryfallCardResolver extraction (Phase 39, Finding A)
- ✅ Core.Tests deterministic: 320/0 (Phase 40)
- ✅ Retire KB clip-injection (Phase 37, RET-01..05)
- ✅ KB corpus rebuild high-signal (Phase 37.5)
- ✅ Harvest video block + hard-delete (Phase 37.6, VBLK-01..04)

**Open / carried forward:**

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED (flag-gated `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping |
| tech_debt | SpellbookCombo ranking fields (PRM-08) | DEFERRED to v1.7+ | v1.5 Phase 31 |
| arch | Finding B: Split CategoryKnowledgeRepository | DEFERRED (backlog) | v1.6 Phase 39 |
| arch | Finding C: Split ContentKbCommandRunners | PARTIALLY ADDRESSED by v1.7 Phase 42 (ORCH-01) | v1.6 Phase 39 |
| arch | Findings D-K | DEFERRED (backlog) | v1.6 Phase 39 |
| housekeeping | 15 pre-v1.5 open artifacts | ACKNOWLEDGED — clean via `/gsd-cleanup` | v1.5 close 2026-06-10 |
| ops | SEL-02 expert-pin live-pin re-confirm | PENDING — needs KB-enable window | v1.5 close |
| ui | Studio "About" link is the Blazor scaffold placeholder (points at ASP.NET docs) — needs a real value (Studio About/version or deckflow.gg). `MainLayout.razor:12`, TODO inline | TODO — Phase 48 UI audit | v1.7 Phase 45 dogfood 2026-06-15 |

## Session Continuity

Last session: 2026-06-16T21:25:00.000Z
Stopped at: Phase 47 Plan 03 Task 2 complete — awaiting Task 3 blocking-human checkpoint
Resume: Phase 47 Plan 03 Tasks 1-2 done (DirectPush.razor page + nav at 6026419; 11 bUnit tests at ca9d824; build 0/0, Studio 34/34). Task 3 is a blocking-human checkpoint (SSH.NET 2025.0.0 supply-chain verify + PROD-write UI gate verify) — NOT auto-approvable. The orchestrator must present that checkpoint and collect the operator's "approved" before Phase 47 can close.
