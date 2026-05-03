---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Admin Console
status: executing
stopped_at: Phase 8 context gathered
last_updated: "2026-05-03T20:48:48.400Z"
last_activity: 2026-05-03
progress:
  total_phases: 4
  completed_phases: 3
  total_plans: 21
  completed_plans: 17
  percent: 81
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-02 after v1.0 milestone)

**Core value:** Every supported workflow must produce ChatGPT-paste-ready output in one round-trip — without the user reformatting anything.
**Current focus:** Phase 08 — analytics

## Current Position

Phase: 08 (analytics) — EXECUTING
Plan: 2 of 5
Status: Ready to execute
Last activity: 2026-05-03

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

## Deferred Items

Items acknowledged and deferred at v1.0 milestone close on 2026-05-02:

| Category | Item | Status | Notes |
|----------|------|--------|-------|
| uat_gap | 04-HUMAN-UAT.md | partial (5 pending scenarios) | Phase 04 ABANDONED — work re-shipped under Phase 05 with full live UAT (27/27 must-haves verified). Pending scenarios are stale; tracked by 04-ABANDONED.md. |
| verification_gap | 04-VERIFICATION.md | human_needed | Phase 04 ABANDONED — superseded by Phase 05 verification (passed, 7/7 SCs, 20/20 plan-frontmatter truths). |

## Session Continuity

Last session: 2026-05-03T20:48:35.890Z
Stopped at: Phase 8 context gathered
Resume: run `/gsd-execute-phase 6` for plan 06 (ScryfallTaggerService gate at top of LookupOracleTagsAsync — D-11 service-level kill switch, FLAG-04)
