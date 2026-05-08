---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Admin Console
status: executing
stopped_at: Phase 8 deployed to prod; Wave 5 task 3 (live SC verification) on hold per operator
last_updated: "2026-05-07T23:10:00Z"
last_activity: 2026-05-07
progress:
  total_phases: 4
  completed_phases: 3
  total_plans: 21
  completed_plans: 20
  percent: 95
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-02 after v1.0 milestone)

**Core value:** Every supported workflow must produce ChatGPT-paste-ready output in one round-trip — without the user reformatting anything.
**Current focus:** Phase 08 — analytics

## Current Position

Phase: 08 (analytics) — EXECUTING (Wave 5 paused)
Plan: 5 of 5 — Waves 1-4 complete + deployed to prod (commits 3f6835f..33da0b9 pushed at 2026-05-03 ~21:30 UTC, Render deploy "Live" with build log clean)
Status: Wave 5 task 3 on hold — operator soak time before SC verification queries
Last activity: 2026-05-08 - Completed quick task 260507-o20: round-trip restore of user form state on ChatGPT saved-session zip import

### Phase 8 resume protocol

Remaining work in Wave 5 task 3:
- SC #1: `SELECT DISTINCT route_key FROM request_metrics ORDER BY route_key LIMIT 50` — controller/action templates only, no card/deck IDs, row count <100
- SC #2: `SELECT COUNT(1) FROM request_metrics WHERE route_key LIKE '/css/%' OR LIKE '/js/%' OR LIKE '/lib/%' OR LIKE '/extensions/%'` — must be 0
- SC #3: `SELECT column_name FROM information_schema.columns WHERE table_name='request_metrics'` — no ip_hash / ip_raw / ip column on aggregate table
- SC #4: browse `/Admin/Analytics` (BasicAuth), confirm range selector + sparkline; view-source must NOT contain `<script src=...chart...>`
- SC #5: capture post-deploy p95 from Render Metrics 24h for record (deferred — no pre-deploy baseline captured)
- Regression: toggle a non-analytics flag on `/Admin/Flags`, confirm save still works (DI side-effect smoke)

After verification: write `.planning/phases/08-analytics/08-05-SUMMARY.md`, update ROADMAP/REQUIREMENTS to flip Wave 5 + ANLY-04, ANLY-05 row checkboxes, commit as `docs(08-05): summary` and `docs(08): close phase 8`.

Resume command: continue this conversation OR start fresh and run `/gsd-execute-phase 8 --wave 5` (only task 3 remains).

Progress bar: `███████░░░` 75% (3/4 phases complete in v1.1) — Phase 8 remaining

## Performance Metrics

**Velocity:**

- Total plans completed: 0 (v1.1)
- Average duration: —
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 6 — Admin Shell + Flags | TBD | — | — |
| 7 — Harvest Controls + Stats | TBD | — | — |
| 8 — Analytics | TBD | — | — |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*
| Phase 06 P01 | 7min | 3 tasks | 15 files |
| Phase 06 P02 | 3min | 2 tasks | 3 files  |
| Phase 06 P03 | ~25min | 1 task done + 1 deferred-to-prod | 2 files (1 created, 1 modified — DEFER-06-01 fold) |
| Phase 06 P04 | 4min | 2 tasks | 4 files |
| Phase 06 P05 | 4min | 2 tasks done + 1 deferred-to-prod | 2 files (both created — AdminFlagsController.cs, Views/AdminFlags/Index.cshtml) |
| Phase 06 P06 | 2min | 1 tasks | 4 files |
| Phase 6 P7 | 5min | 2 tasks | 2 files |
| Phase 08-analytics P01 | 25 | 2 tasks | 5 files |
| Phase 08-analytics P02 | 10min | 2 tasks | 2 files |
| Phase 08-analytics P03 | 15min | 2 tasks | 4 files |
| Phase 08-analytics P04 | 10min | 3 tasks | 4 files |

## Accumulated Context

### Roadmap Evolution

- Phase 07.1 inserted after Phase 7: categories feature flag + SameOrigin AJAX fix (URGENT)

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Decisions affecting v1.1 work:

- Shell + Flags merged into Phase 6 (not split): kill-switch seed rows gate live Tagger before flags are user-facing; combining eliminates a phase boundary that would leave Harvest/Analytics with no flag support during Phase 6.
- Phase ordering: Shell (6) → Harvest (7) → Analytics (8). Analytics placed last because it captures Harvest job-trigger events as real signal data from day one.
- No Phase 9 Polish phase defined: all POLISH-01..04 and HARV-NEXT/ANLY-NEXT items explicitly deferred to v1.2+ in REQUIREMENTS.md.
- Granularity: coarse (from config.json) — 23 requirements → 3 phases. Research recommended 3 phases; structure matches exactly.
- Live verification mandatory every phase: Phase 4 trap (v1.0 post-mortem) applies unconditionally — every phase success criteria includes at least one criterion verifiable against deployed deckflow.gg.
- [Phase ?]: Phase 6 admin shell uses standalone admin.css with single-stylesheet wall (D-05); zero references to site-*.css guild themes
- [Phase ?]: Per-folder _ViewStart pattern adopted for admin: 5 folder-scoped 3-line files set Layout=_AdminLayout (Admin, AdminHarvest, AdminAnalytics, AdminFlags, AdminLanding); root Views/_ViewStart.cshtml untouched
- [Phase 6]: Feature-flag persistence uses dual-dialect IsPostgres branching (mirroring AdminBruteForceTrackerStore), not IRelationalDialect — IRelationalDialect stays feedback-specific until a third site demands the bump
- [Phase 6]: Default-on FLAG-01 contract enforced at the schema layer via ON CONFLICT (key) DO NOTHING seed (scryfall.tagger.enabled, page.help.enabled) — not just at the cache layer; fresh DB and re-bootstrap both end with both flags ON
- [Phase 6, Plan 03]: AdminFeedback layout swap landed via 3-line per-folder _ViewStart (D-15 layout-swap-only enforced — zero controller / view-body diff); Task 2 visual verification deferred-to-prod because local-dev has no FEEDBACK_ADMIN_USER/PASSWORD env vars (operator declined to add a dev-only BasicAuth fallback). DEFER-06-01 (`v@VersionService.GetVersion()` literal-text bug on _AdminLayout.cshtml:30) folded into the 06-03 closure commit (one-line `v@(...)` parens fix) so it rides the same post-merge prod verification gate.
- [Phase 6]: FeatureFlagCache uses BackgroundService.StartAsync override (not IHostApplicationLifetime.ApplicationStarted) for D-14 sync initial load — pattern is awaitable BEFORE base.StartAsync schedules ExecuteAsync, so host doesn't report ready until snapshot is hydrated. WARN-once dedupe via ConcurrentDictionary<string, byte> sentinel; T-06-D1 mitigated by try/catch preserving prior snapshot on PG failure.
- [Phase 6, Plan 05]: AdminFlagsController landed with sequential-await D-10 (`SetEnabledAsync` then `ReloadAsync` BEFORE redirect) and snapshot-allowlist key validation (T-06-E2 — unknown keys → 400 BadRequest, never reach store). View inherits _AdminLayout via plan 01's per-folder _ViewStart; per-row antiforgery POST forms match AdminFeedbackController pattern (ADMIN-05, FLAG-03 closed). Visual checkpoint deferred-to-prod under phase-wide standing decision (DEFER-06-01 precedent — local-dev has no FEEDBACK_ADMIN_USER/PASSWORD env). Production verification steps captured in 06-05-SUMMARY.md "Production verification steps".
- [Phase 06]: D-11 service-layer kill-switch gate at top of ScryfallTaggerService.LookupOracleTagsAsync — IFeatureFlagCache.IsEnabled("scryfall.tagger.enabled") short-circuits with Array.Empty<string>() when off; FLAG-04 satisfied.
- [Phase ?]: [Phase 6, Plan 07]: FeatureFlagGateAttribute is the canonical reusable page kill-switch — IAsyncActionFilter resolves IFeatureFlagCache from HttpContext.RequestServices per invocation (T-06-G3 mitigation by construction); flag-off short-circuits with 503 + Retry-After: 300 + _MaintenancePage ViewResult; future page kill-switches need only attribute + seed row (zero new infrastructure)
- [Phase ?]: [Phase 6 complete]: All 10 REQ-IDs (ADMIN-01..05 + FLAG-01..05) satisfied; FLAG-05 demo (page.help.enabled toggle on /help) verified locally via direct SQLite UPDATE + curl across 4 transitions (200 ON, 503 OFF, 200 restored, Topic ungated per D-16); production verification gate inherits the existing 06-03/06-05 deferred items (BasicAuth env-var presence)
- [Phase ?]: IpHasher extracted as single SHA-256+salt+CF-Connecting-IP site; FeedbackStore delegates to it
- [Phase ?]: RequestMetricsStore takes IServiceProvider? per D-14 to avoid circular DI with flusher/buffer
- [Phase ?]: ShutdownDrainCeiling=2s chosen for orderly restart flush without stalling Render/Fly graceful shutdown window
- [Phase ?]: MaybeLogDrops resets lastDropLog even when dropped==0 to advance the 60s window continuously and prevent spurious WARN bursts
- [Phase ?]: AnalyticsSaltAccessor: volatile-read singleton populated once at startup eliminates per-request DB I/O on analytics hot path
- [Phase ?]: Salt resolution try/catch at startup: SQLite feedback_meta missing logs WRN and continues with ip_hash null
- [Phase ?]: AdminAnalyticsController queries Postgres directly — IRequestMetricsStore stays write-only
- [Phase ?]: RenderSparkline: C# StringBuilder inline SVG — no JS chart library; color via .admin-sparkline { color: var(--muted) }

### Pending Todos

- Pre-condition for Phase 7: audit `ArchidektApiDeckImporter` cancellation token threading before designing harvest cancel UI (pitfall B3 from SUMMARY.md).
- Capture Render dashboard p95 baseline before deploying Phase 8 analytics middleware (SUMMARY.md gap).
- **After Phase 7:** Add `feature.categories.enabled` flag (Phase 6 IFeatureFlagCache pattern, default ON) gating two surfaces — (1) Suggest Categories nav menu entry; (2) Categories card/CTA on landing page. Ship as small interim phase (e.g., 7.5) so prod can hide the broken category flow via `/Admin/Flags`. Mirrors Phase 6 D-09 seed pattern + per-action gate. Captured 2026-05-03.
- **After Phase 7 (depends on flag above):** Fix categories endpoint regression — fetching categories returns "This endpoint only accepts same-origin browser requests." Same-origin gate is `SameOriginRequestValidator` in `DeckFlow.Web/Security/`. Used to work; regressed at some point. Likely cause: Origin/Referer header missing on the AJAX call OR forwarded-headers / scheme mismatch behind Render reverse proxy. Investigation: log request Origin / Referer / X-Forwarded-Proto at the validator on a category attempt; compare against working endpoints; check if `app.UseForwardedHeaders()` ordering shifted recently. Captured 2026-05-03.

### Blockers/Concerns

- Brownfield production site: every phase must keep deckflow.gg green; Render auto-deploys from `main`.
- VSTest unreliable in WSL2 — verification leans on `dotnet build` clean + manual harness + push-and-watch CI.
- SQL dialect divergence risk: every new SQL block (4 new tables across Phases 6-8) must be verified against Postgres before the phase closes.
- RAM cap: Render Starter 512MB web tier — analytics bounded Channel (2000 cap, DropOldest) and 30s flag poll are sized to stay well under budget.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260504-in1 | fix the remaining Phase 07.1 UI review issues: when AI Category Suggestions is feature-flagged off, update maintenance copy to say only AI category suggestions are unavailable, add a link/action to Category Reference or Home from the maintenance page, and add a small in-context explanation on the home/categories area or nav so returning users understand the feature is temporarily unavailable | 2026-05-04 | c3c7ee2 | [260504-in1-fix-the-remaining-phase-07-1-ui-review-i](./quick/260504-in1-fix-the-remaining-phase-07-1-ui-review-i/) |
| 260506-hgd | ChatGPT artifact local download/upload — replace server-side save and import (privacy restructure: zip download + zip upload on all three ChatGPT pages; supersedes stopgap commit 0021908; deletes IChatGptArtifactsDirectory + /api/saved-sessions; existing /data/ChatGPT Analysis/ files left untouched) | 2026-05-06 | 5f5764f | [260506-hgd-chatgpt-artifact-local-download-upload-r](./quick/260506-hgd-chatgpt-artifact-local-download-upload-r/) |
| 260506-kwt | Sticky prominent Download (.zip) bar on all three ChatGPT pages — always-available top-of-page CTA so user can save current session state at any step (per-step inline buttons retained as secondary). Layout CSS in site-common.css; no per-theme edits; no JS; reuses existing /chatgpt-*/download endpoints | 2026-05-06 | d44c7ab | [260506-kwt-make-chatgpt-zip-download-button-more-pr](./quick/260506-kwt-make-chatgpt-zip-download-button-more-pr/) |
| 260507-l7x | Fix saved-session upload on ChatGPT workflow pages so resume import bypasses browser-native required-field validation and the shared step validator. Addresses production `TargetCommanderBracket is not focusable` failure on `/chatgpt-packets` and applies the same upload-path guard to comparison and cEDH pages. | 2026-05-07 | 29e2733 | [260507-l7x-fix-chatgpt-packets-saved-session-upload](./quick/260507-l7x-fix-chatgpt-packets-saved-session-upload/) |
| 260507-m8k | Fix two /Admin/Harvest bugs: (B1) decks-imported counter stuck at 0 during running jobs because `decks_processed` was only written at terminal transitions — fixed by adding `IHarvestRunStore.UpdateProgressAsync` (counters-only, dialect-safe) and threading an optional `IProgress<int>` through `RunCacheSweepAsync` → `ArchidektDeckCacheSession.RunAsync`, with a throttled (≥10 decks OR ≥2s) progress sink in `ArchidektCacheJobService`; (B2) Recent Runs grid stale, required manual reload — fixed by adding `recentRunsRevision` token (`{startedTicks}\|{completedTicks}\|{count}`) to `/Admin/Harvest/status` and rewriting `admin-harvest.ts` to poll always (10s idle / 3s active) and `window.location.reload()` on revision change; noscript meta-refresh and terminal-state reload fallback preserved. Live B1/B2 verification gates require post-deploy on deckflow.gg per push-and-watch pattern. | 2026-05-07 | 9698551 | [260507-m8k-fix-admin-harvest-decks-counter-and-rece](./quick/260507-m8k-fix-admin-harvest-decks-counter-and-rece/) |
| 260507-ner | Add auto-refresh to /Admin/Analytics, mirroring the harvest pattern from 260507-m8k. Adds `[HttpGet("status")]` JSON endpoint on `AdminAnalyticsController` returning `{ metricsRevision: "{maxDay}\|{sumHits}" }` (request_metrics has no `updated_utc` column — token derives from `MAX(day_utc)` + `SUM(hit_count)`). 5s `IMemoryCache` TTL matches `RequestMetricsFlusher.FlushInterval`. SameOriginRequestValidator gate preserved. SQLite local-dev returns stable `"\|0"`; query failure returns stable `"\|err"` (no reload loop on either). New `admin-analytics.ts` polls every 15s, captures `lastRevision` baseline on first poll, `window.location.reload()` on revision change (URL + `?range=today\|7d\|30d\|all` survives by default). 60s `<noscript>` meta-refresh fallback. `IRequestMetricsStore` unchanged (write-only by design). Live verification deferred to operator post-deploy. | 2026-05-07 | b72f87a | [260507-ner-add-admin-analytics-auto-refresh-via-met](./quick/260507-ner-add-admin-analytics-auto-refresh-via-met/) |
| 260507-o20 | Round-trip restore of user form state on ChatGPT packet saved-session import. New `ChatGptRequestContextParser` parses the YAML-like `01-request-context.txt` export (scalars, `- item` lists, raw multi-line blocks for strategy/meta notes and deck source). `ChatGptPacketArtifactStore.LoadFromZip` now reads and applies all user-controlled fields: format, bracket, analysis questions, set codes, sideboard/maybeboard flags, budget, strategy notes, meta notes, deck source. Backwards-compatible — older zips without the context entry silently skip hydration. Adds xUnit fixture test against real Arna Kennerüd packet zip. | 2026-05-08 | d81acfc | [260507-o20-restore-full-round-trip-on-chatgpt-saved](./quick/260507-o20-restore-full-round-trip-on-chatgpt-saved/) |

## Deferred Items

Items acknowledged and deferred at v1.0 milestone close on 2026-05-02:

| Category | Item | Status | Notes |
|----------|------|--------|-------|
| uat_gap | 04-HUMAN-UAT.md | partial (5 pending scenarios) | Phase 04 ABANDONED — work re-shipped under Phase 05 with full live UAT (27/27 must-haves verified). Pending scenarios are stale; tracked by 04-ABANDONED.md. |
| verification_gap | 04-VERIFICATION.md | human_needed | Phase 04 ABANDONED — superseded by Phase 05 verification (passed, 7/7 SCs, 20/20 plan-frontmatter truths). |

## Session Continuity

Last session: 2026-05-03T21:25:58.761Z
Stopped at: Completed 08-04-PLAN.md
Resume: run `/gsd-execute-phase 6` for plan 06 (ScryfallTaggerService gate at top of LookupOracleTagsAsync — D-11 service-level kill switch, FLAG-04)
