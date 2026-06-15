---
gsd_state_version: 1.0
milestone: v1.7
milestone_name: Local Harvest & Publish Studio
status: executing
stopped_at: Phase 45 context gathered
last_updated: "2026-06-15T19:12:24.989Z"
last_activity: 2026-06-15
progress:
  total_phases: 10
  completed_phases: 6
  total_plans: 24
  completed_plans: 21
  percent: 60
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 45 — harvest-distill-ui

## Current Position

Phase: 45 (harvest-distill-ui) — EXECUTING
Plan: 2 of 4
Status: Ready to execute
Execution order: 49 → 44 → 45 → 46 → 47 (48 independent; 50 after 44+49)
Last activity: 2026-06-15

```
Progress: [█████████░] 88%
```

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 41 | Studio Scaffold + Secrets Wiring | STU-01, STU-02, STU-03 | Complete |
| 42 | Orchestrator Extraction | ORCH-01, ORCH-02 | Not started |
| 43 | Approval Status + Safe Upsert | REVQ-01, PUB-01, PUB-02 | Not started |
| 44 | Admin Grid Lazy Paging | GRID-01, GRID-02 | Not started |
| 45 | Harvest + Distill UI | HARV-01..05 | Not started |
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

## Session Continuity

Last session: 2026-06-15T19:12:24.959Z
Stopped at: Phase 45 context gathered
Resume: Start Phase 41 with `/gsd:plan-phase 41`.
