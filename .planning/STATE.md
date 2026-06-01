---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup
status: executing
stopped_at: Phase 22 UI-SPEC approved (force-approved over generic-rule blocks; reuses pinned design system)
last_updated: "2026-06-01T21:14:09.100Z"
last_activity: 2026-05-27 -- Phase 20 execution started
progress:
  total_phases: 12
  completed_phases: 8
  total_plans: 22
  completed_plans: 21
  percent: 67
---

## Deferred Items

Reviewed 2026-05-13 via `/gsd-review-backlog`. Promoted to v1.3 candidates: harvest-killed-by-suggestion (debug), AiPlatform value object refactor. Removed 7 quick_tasks (all complete per SUMMARY, stale "missing" label). Closed 07-VERIFICATION as satisfied (v1.1 shipped 2026-05-08, no harvest crash incidents). Backfilled 09/10-VERIFICATION.md and Phase 9 SUMMARY frontmatter same session.

| Category | Item | Status |
|----------|------|--------|
| tech_debt | Gemini paste-limit workaround | DEFERRED to v1.5 (cluster D dropped from v1.4 scope 2026-05-23) |
| archive | v1.1 phase dirs (`06-admin-shell-flags-foundation`, `07-harvest-controls-stats`, `07.1-categories-feature-flag-sameorigin-ajax-fix`, `08-analytics`) | DONE 2026-05-29 — moved from `v1.3-phases/` to `.planning/milestones/v1.1-phases/` |
| tech_debt | Semantic-completeness guards for `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` | CLOSED v1.3 Phase 999.5 P02 |

### Acknowledged at v1.3 close (2026-05-23) — carried to v1.5 housekeeping

Audit-open scan surfaced 21 items at `/gsd-complete-milestone v1.3` pre-flight. All triaged and acknowledged for deferral per user `[A]` choice 2026-05-23. Items marked v1.4 backlog in original STATE were reassessed at v1.4 scoping and deferred to v1.5 (v1.4 scope is fixed at 4 clusters / 16 REQ-IDs).

| Category | Item | Status | Disposition |
|----------|------|--------|-------------|
| debug | 999.6-archidekt-cache-job | closed | CLOSED v1.3 999.6 P03 (commit d758609); status flipped 2026-05-29 (was stale "unknown" from audit scanner) |
| debug | 999.6-basicauth-flaky | closed | CLOSED v1.3 999.6 P02 (commit a62f608); status flipped 2026-05-29 (was stale "unknown") |
| debug | v13-harvest-worker-stalled | resolved_not_reproduced | CLOSED 2026-05-29 — fix+diagnostics live on prod; Render logs 05-23..05-29 show all harvest cycles healthy (matched Enqueue/Dequeue, all Running→Succeeded, 0 TerminalWriteFailed, 0 stalls). One-off v1.3 incident never recurred. |
| uat-gap | Phase 11/13/15/999.1-999.8 UAT files (10 phases) | false_positive | FALSE POSITIVE — audit-scanner vocabulary drift, not a real gap; acknowledged 2026-05-29 |
| quick_task | 260504-in1-fix-the-remaining-phase-07-1-ui-review-i | complete | Verified complete (SUMMARY status:complete); "missing" was scanner false-positive, flipped 2026-05-29 |
| quick_task | 260506-hgd-chatgpt-artifact-local-download-upload-r | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260506-kwt-make-chatgpt-zip-download-button-more-pr | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260507-l7x-fix-chatgpt-packets-saved-session-upload | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260507-m8k-fix-admin-harvest-decks-counter-and-rece | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260507-ner-add-admin-analytics-auto-refresh-via-met | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260507-o20-restore-full-round-trip-on-chatgpt-saved | complete | Verified complete (SUMMARY status:complete); flipped 2026-05-29 |
| quick_task | 260513-wdg-web-design-guidelines-audit-findings | complete | CLOSED v1.3 Phase 11 (SUMMARY status:complete); flipped 2026-05-29 |

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-23 for v1.4 milestone start)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 20 — content-kb-ingestion-transcription-local

## Current Position

Phase: 20 (content-kb-ingestion-transcription-local) — EXECUTING
Plan: 1 of 4
Status: Executing Phase 20
Last activity: 2026-05-27 -- Phase 20 execution started

## Performance Metrics

**Velocity (v1.3 reference — most recent shipped):**

- Total plans completed in v1.3: 51 (13 phases over 10 days, 2026-05-13 → 2026-05-23)
- Cross-AI execution pattern established (Codex codes, Claude reviews) — sustained zero friction across 6+ consecutive phase closures
- Final test gate: `Failed: 0, Passed: 497, Skipped: 3, Total: 500`

**v1.4 Forecast (per research SUMMARY.md):**

| Phase | Plans (est.) | Notes |
|-------|--------------|-------|
| 1 — WDG-04 Modal | 1-2 | Tiny: 1 TS + 1 view + small CSS; zero coupling |
| 2 — Doc-Comment Backfill Part 1 | 2-3 | ~50 of 88 types; mechanical (Controllers + Services) |
| 3 — Admin Mobile Sweep | 2-4 | CSS factoring + table strategy decisions + form audit + 22-theme visual regression |
| 4 — Content KB Stores + Schema | 3-5 | 8 tables, per-store EnsureSchemaAsync, dual-dialect SQLite+Postgres tests |
| 5 — Content KB Outbound HTTP | 4-6 | YouTube + Podcast + Whisper + LLM; 5 new named HttpClients + Polly pipelines |
| 6 — Content KB Orchestrator | 3-4 | TOCTOU advisory lock, cap-gate, kill-switch, single-worker; MVP mode |
| 7 — Content KB Admin UI | 3-5 | 3 controllers + 7 Razor views + sidebar + CSRF wiring |
| 8 — Doc-Comment Part 2 + Strip NoWarn | 2-3 | Remaining ~38 + all v1.4 new types; csproj edit LAST |
| **Total estimate** | **~20-32 plans** | SUMMARY range was 25-35; coarse granularity tightens to ~20-32 |

## Accumulated Context

### Roadmap Evolution

- v1.4 roadmap created 2026-05-23 — 8 phases, 16/16 REQ-IDs mapped. Phase numbering RESET to 1 (--reset-phase-numbers active). v1.3 phase dirs already archived to `.planning/milestones/v1.3-phases/`.
- Gemini cluster D (GEM-01/02) DROPPED from v1.4 per user decision 2026-05-23 → v1.5. SUMMARY.md scope-update note authoritative.
- Phase 20.1 inserted after Phase 20: Phase 21 live-UAT gate (URGENT) — superseded: renumbered to 21.1
- Phase 21.1 inserted after Phase 21: Phase 21 live-UAT gate — renumbered from 20.1 (gate verifies/depends-on Phase 21, must sort after it) (URGENT)

### v1.0-v1.3 Shipped

v1.0 (15/15 reqs, 2026-05-02) | v1.1 (27/27 reqs, 2026-05-08) | v1.2 (5/5 reqs, 2026-05-13) | v1.3 (22/22 reqs, 2026-05-23).

### v1.4 Decisions

- **Critical path (dependencies):** Phase 16 → 18 → 19 → 20 → 21 → 22. Phase 17 parallelizable (off critical path). Phase 23 lands last (so v1.4 new types are documented before NoWarn gate flips per Pitfall 8).
- **Execution order (user decision 2026-05-24) — Content KB last:** run remaining phases as `25 → 24 → 19 → 20 → 21 → 22 → 23`. Grid (25, Codex-approved) + bug (24) are independent of Content KB and lead; KB block (19-22) runs last in dependency order; 23 (strip NoWarn) stays final. Phase numbers NOT renumbered (stable IDs). **Auto next-phase detection picks lowest unplanned number (19) — it will NOT follow this order; pass explicit phase numbers to `/gsd-execute-phase`.**
- **Phase 16 before Phase 18:** Modal CSS lands in new `admin-common.css` factoring; doing modal after the split forces touching two files (per ARCHITECTURE.md build order rationale).
- **Phase 19 before Phase 20 before Phase 21 before Phase 22:** Each Content KB layer's tests need the prior layer's seam (stores → HTTP → orchestrator → UI).
- **Phase 21 mode = mvp:** Orchestrator phase delivers the headline user story "admin can trigger end-to-end harvest"; mvp mode enforces user-story-first verification with the TOCTOU + kill-switch invariants as hard gates.
- **`content_kb_enabled` flag default OFF:** flip only after first admin UAT verifies end-to-end harvest from deployed Render env (per Pitfall 1+2 P1+P2 mitigation).
- **Whisper monthly cap default `$15.00`:** per STACK.md expected $13.32 run-rate + 12% headroom. Env var `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (sync:false on Render).
- **YouTube transcript via YoutubeExplode 6.6.0 NOT Google.Apis.YouTube.v3** — captions.download returns 403 on third-party content per Google Issue Tracker 241669016 (Pitfall 1).
- **OpenAI 2.10.0 single SDK** for Whisper + chat-completion + Structured Outputs — minimizes cap-tracking complexity. AiPlatform value object UNTOUCHED for admin-side ingestion (registry serves user-facing multi-AI paste only).
- **ContentHarvestRunStore is parallel to HarvestRunStore (NOT subclass)** — `harvest_runs.kind` CHECK never widened; strict `content_*` table namespace (Pitfall 12).
- **`pg_try_advisory_lock(hashtext('whisper-cap-' || YYYY-MM))`** acquired BEFORE any Whisper call; SERIALIZABLE txn wraps check-and-insert; `DECKFLOW_WHISPER_KILL_SWITCH=true` env var evaluated first (Pitfall 3 P3).

### Cross-Cutting Invariants (15 MUST/MUST NOT — from SUMMARY.md §5)

1. All outbound HTTP via `IHttpClientFactory` named clients + named Polly pipelines via `ResiliencePipelineProvider<string>`. NEVER `new HttpClient()`. NEVER migrate to `Microsoft.Extensions.Http.Resilience` standard handler.
2. AiPlatform value object UNTOUCHED for admin-side LLM summarization.
3. NEVER widen v1.1 `harvest_runs.kind` CHECK; fork to parallel `ContentHarvestRunStore`.
4. All new tables namespaced `content_*` (except `whisper_spend_ledger`).
5. `IWhisperSpendLedger.WouldExceedCapAsync(estimate)` BEFORE every Whisper API call; ledger row on success only.
6. Env var `DECKFLOW_WHISPER_MONTHLY_CAP_USD` typed decimal; NOT routed through `IFeatureFlagStore`.
7. `SameOriginRequestValidator` on every `/api/*` POST AND `[ValidateAntiForgeryToken]` on every `/Admin/*` POST — two separate CSRF mechanisms.
8. `{ get; init; }` preserved on every new record type.
9. C# raw-string literals byte-preserved in prompts + DDL constants.
10. Native HTML `<dialog>` element + `showModal()`; NO focus-trap npm dependency.
11. New admin CSS scoped to `.admin-shell` parent class; `@layer admin { ... }` for cascade discipline; ZERO unscoped element selectors.
12. Layout CSS in `site-common.css` / new `admin-common.css`, NOT in `site.css` or `admin.css` directly.
13. Store-test isolation per F-PROD-CONTRACT 999.6 lesson (own SQLite file or `:memory:` per-fact scope).
14. All API keys in Render env vars with `sync: false`; Gitleaks pre-commit hook recommended.
15. Every plan through Codex peer review (`/gsd-review`) before execute-phase dispatch.

### Recurring v1.3 Patterns to AVOID (R-1..R-7 from SUMMARY.md §6)

- **R-1** STATE.md arithmetic drift → auto-compute counters on phase close; CI gate `gsd-sdk verify-state`.
- **R-2** REQUIREMENTS.md checkbox drift → auto-flip `[x]` from SUMMARY frontmatter `requirements-completed:` field; reject SUMMARYs missing it.
- **R-3** Planning-time grep miscounts → every SC grep MUST be anchored (e.g. `grep -cE '^[[:space:]]*\[HttpPost'`, NOT `grep -c HttpPost`).
- **R-4** Cross-AI plan review (Codex reviews Claude's plans) — NO exceptions for "small" plans.
- **R-5** `no-ship-failing-tests` — Failed:0 mandatory before milestone PR. Pre-allocate `999.x` test-hardening phase before ship if needed.
- **R-6** Formatting paranoia — no Format Document; no `{ get; init; }` → `{ get; }`; no inline `[Attribute]`; no raw-string re-indent; touch only lines that need touching.
- **R-7** HANDOFF.json / origin staleness on resume — every session resume `git fetch` + compare `HEAD` vs `origin/<branch>` BEFORE reading planning artifacts.

### Constraints Carried Forward (per PROJECT.md + CLAUDE.md)

- ASP.NET 10 + Razor pinned — no framework migration in v1.4.
- 22 guild theme stylesheets are full forks — admin CSS sweep MUST be `.admin-shell`-scoped to avoid 22× theme regression.
- VSTest unreliable in WSL — rely on `dotnet build` clean + push-and-watch CI on `v1.4` branch + targeted manual UAT.
- Plain default-author commits, no `Co-Authored-By` trailer across all v1.4.
- Public repo `luntc1972/DeckFlow` — no secrets in commits; Render dashboard with `sync: false` for new keys (`OPENAI_API_KEY`, `DECKFLOW_WHISPER_MONTHLY_CAP_USD`, optional `DECKFLOW_WHISPER_KILL_SWITCH`, `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER`).
- 512MB RAM cap on Render Starter — NO local Whisper inference; chunk audio via ffmpeg client-side; stream audio to `/data/whisper-tmp/` not in-memory.
- Postgres connection pool: cap explicit at 10-15 (Pitfall 6); never hold connection across `await` HTTP call.

### Content KB Pivot (2026-05-26)

- During `/gsd-discuss-phase 19`, the Content KB milestone was **re-architected** from server-hosted harvest to a **local-harvester + file-artifact + slim-site-index** model. Harvest/transcribe/distill runs LOCALLY (CLI command or standalone small app — packaging decided at plan time) against **local SQLite**; never on Render.
- Output = **AI-prompt artifact files** (committed to repo like `prompt-templates/` or uploaded to `/data`) + a **slim index table** on Render Postgres for browse/filter. Only KB-08 (index) + KB-09 (display gate) touch Render.
- **Dropped:** server harvest endpoint/orchestrator, `pg_try_advisory_lock` TOCTOU cap-gate, `DECKFLOW_WHISPER_KILL_SWITCH`, admin spend dashboard. Spend control = plain local spend-log check vs `DECKFLOW_WHISPER_MONTHLY_CAP_USD`.
- ROADMAP.md phases 19-22 + REQUIREMENTS.md KB-01..KB-09 rewritten to match (IDs preserved, repurposed). Phase 19 discuss decisions (integer PKs, CASCADE FKs, soft-disable, ~4 aggregate stores) captured in the Phase 19 detail block. Numbers NOT renumbered.

### Blockers/Concerns

- None at roadmap creation. v1.4 work continues on branch `v1.3` (per current checkout); branch cutover to `v1.4` per operator decision at first execute-phase.
- **SQLite CASCADE landmine** (Phase 19/20): SQLite enforces FK `ON DELETE CASCADE` only with `PRAGMA foreign_keys=ON` per connection — verify the connection factory sets it or cascades silently no-op while Postgres enforces.
- Dockerfile `apt-get install -y ffmpeg` verification needed at Phase 20/21 start if podcasts >25MB will need chunking (per Pitfall 7).

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260530-c72 | cEDH meta-gap: remove Input Summary panel + mobile-friendly reference table | 2026-05-30 | 1133151 | [260530-c72-cedh-meta-gap-remove-input-summary-panel](./quick/260530-c72-cedh-meta-gap-remove-input-summary-panel/) |

## Session Continuity

Last session: 2026-06-01T21:14:09.062Z

Stopped at: Phase 22 UI-SPEC approved (force-approved over generic-rule blocks; reuses pinned design system)

Phase 21 (Content KB Distillation + Artifact Emit, local) — all 4 plans implemented by Codex (cross-AI per CLAUDE.md), reviewed by Claude, verified green:

- 21-01 LlmDistillationService (3 strict-json gpt-4o-mini calls) + schemas/results — 6 tests.
- 21-02 LlmSpendLedger (separate token-based ledger, cap $15, gpt-4o-mini $0.15/$0.60 per 1M) — 6 tests.
- 21-03 ContentArtifactWriter + `content_distill_status` table (CREATE IF NOT EXISTS, no ALTER) + source-scoped store methods + `content-kb/` gitignore — 12 tests.
- 21-04 distill orchestrator + `distill`/`content-source-set-enabled` CLI verbs — HIGH-1 per-call ledgering, HIGH-2 source-scoped, HIGH-3 durable status, --dry-run no-mutation — 9 tests.
- Full suite green: solution build 0/0; Core.Tests 217/217; Web.Tests 486 pass / 5 skipped / 0 fail.
- Codex injected an out-of-scope prompt-dedup refactor during 21-01; reverted (a1fa5ad/774aa1a → reverts 6da70da/b2ffba7). Dedup work preserved in git history for a future authorized /gsd-quick.

Next action on resume: run the 21-04 live-UAT human-verify checkpoint (needs `OPENAI_API_KEY`; ~$1 spend on `artifacts/uat-content-kb.db` 10-video set; E5/E6 archetype/bracket/thesis sampling). On UAT pass: `/gsd-verify-work 21`, flip ROADMAP Phase 21 `[x]`, then commit. ROADMAP stays `[ ]` until UAT passes.

**Branch**: `v1.4`. Commits 603d202→721c296 (+SUMMARY/revert commits); NOT pushed.
