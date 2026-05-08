# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- 🔵 **v1.2 Multi-AI Prompts** — Phases 9-10 (active)

## Phases

<details>
<summary>✅ v1.0 Polish & Quality (Phases 1-5) — SHIPPED 2026-05-02</summary>

- [x] Phase 1: Visual System Tokens — 3/3 plans (UI-VS-01..04)
- [x] Phase 2: Layout, Hierarchy & UX Copy — 3/3 plans (UI-LH-01..02, UX-01..03)
- [x] Phase 3: Tech-Debt Cleanup — 4/4 plans (TD-01..04)
- [~] Phase 4: Security & Bug Fixes — 4/4 plans, ABANDONED 2026-05-02 (rerouted to Phase 5)
- [x] Phase 5: Security & Bug Fixes v2 — 3/3 plans (BUG-01, BUG-02, TD-04 patch + integration test)

Verification: 27/27 must-haves passed. 15/15 v1 requirements shipped.
Full archive: `.planning/milestones/v1.0-ROADMAP.md`

</details>

### v1.1 Admin Console

- [x] **Phase 6: Admin Shell + Flags Foundation** — Layout shell, sidebar nav, antiforgery baseline, feature-flag infrastructure (completed 2026-05-03)
- [x] **Phase 7: Harvest Controls + Stats** — Run-now, cancel, pause/resume, cron schedule, stats panel, run history (completed 2026-05-03; circular-DI errata fixed in dc66a38)
- [x] **Phase 7.1: Categories feature flag + SameOrigin AJAX fix** — kill-switch flag default-on; X-Forwarded-Proto honored in same-origin validator (completed 2026-05-03)
- [x] **Phase 8: Analytics** — Request metrics middleware, write-behind buffer, top-routes page, inline SVG sparklines (completed 2026-05-08)

## Phase Details

### Phase 6: Admin Shell + Flags Foundation

**Goal**: Operator can reach all admin sections through a neutral-themed shell and toggle feature flags from the browser — live features are protected by default-on seed rows before any user-facing flag work ships.

**Depends on**: Nothing (first v1.1 phase; builds on v1.0 BasicAuth gate already in production)

**Requirements**: ADMIN-01, ADMIN-02, ADMIN-03, ADMIN-04, ADMIN-05, FLAG-01, FLAG-02, FLAG-03, FLAG-04, FLAG-05

**Success Criteria** (what must be TRUE):

1. Visiting `https://www.deckflow.gg/Admin` prompts BasicAuth; after auth, a sidebar listing Feedback / Harvest / Analytics / Flags renders with the active page highlighted — no guild theme colors, fonts, or nav chrome visible.
2. Clicking each sidebar link in all three major guild themes returns HTTP 200 and shows only neutral admin CSS (verified: no `--accent-strong` guild hue visible on the page).
3. `curl https://www.deckflow.gg/Admin` without credentials returns 401 — no page content leaks.
4. `/Admin/feedback` loads inside the new admin shell with its existing inbox and mark-read flow fully intact (no regression).
5. Operator can disable the Tagger kill-switch flag from `/Admin/flags`, reload a card lookup page within 2 seconds, and observe that Tagger tags are absent — demonstrating hot-reload invalidation, not TTL expiry.

**Plans**: 7 plans across 3 waves

Plans:
- [x] 06-01-PLAN.md — Admin shell layout + CSS + sidebar nav + 3 placeholder controllers + MaintenanceViewModel/_MaintenancePage view (ADMIN-01, ADMIN-02)
- [x] 06-02-PLAN.md — feature_flags schema + IFeatureFlagStore (Postgres + SQLite) with EnsureSchemaAsync seed (FLAG-01)
- [x] 06-03-PLAN.md — AdminFeedback layout-swap to _AdminLayout (D-15 zero controller / view-body churn) (ADMIN-03, ADMIN-04) — Task 2 deferred-to-prod, DEFER-06-01 folded
- [x] 06-04-PLAN.md — IFeatureFlagCache singleton + IHostedService (sync StartAsync load D-14, 30s poller, WARN-once dedupe D-13) + AddDeckFlowFeatureFlags() extension (FLAG-02)
- [x] 06-05-PLAN.md — AdminFlagsController + view + POST toggle (antiforgery + sync cache reload D-10 + key-allowlist) (ADMIN-05, FLAG-03) — visual checkpoint deferred-to-prod (no local BasicAuth)
- [x] 06-06-PLAN.md — ScryfallTaggerService gate at top of LookupOracleTagsAsync (D-11 service-level kill switch) (FLAG-04)
- [x] 06-07-PLAN.md — FeatureFlagGateAttribute action filter + apply to /help index (D-16 demo target, D-17 503 + Retry-After 300, D-18 attribute wiring) (ADMIN-03, FLAG-05)

**UI hint**: yes

---

### Phase 7: Harvest Controls + Stats

**Goal**: Operator can start, cancel, and schedule Archidekt harvest runs from the browser, and see current knowledge-base coverage stats — all state surviving Render redeploys.

**Depends on**: Phase 6 (admin shell + antiforgery pattern established)

**Requirements**: HARV-01, HARV-02, HARV-03, HARV-04, HARV-05, HARV-06, HARV-07

**Success Criteria** (what must be TRUE):

1. Operator clicks "Run Now" with a 15-minute cap, the page shows live job status (state, decks processed, elapsed), and Postgres `harvest_runs` shows a completed row after the run finishes — row survives a Render redeploy.
2. Operator submits a single Archidekt deck URL; the deck is harvested and its commander appears in the top-commanders list on the stats panel.
3. Operator clicks "Cancel" on a running job; the harvest page transitions to "Stopping" then "Failed/Cancelled" within one HTTP timeout (30s), and no torn rows appear in `category_knowledge`.
4. Operator sets "Pause schedule" and confirms the recurring harvest does not fire at its next scheduled slot; operator resumes, and the next slot fires correctly.
5. Stats panel at `https://www.deckflow.gg/Admin/harvest` shows: total decks (lifetime), total observations, Postgres storage size, last run timestamp, next scheduled run — all drawn from live Postgres, not in-memory.

**Plans**: 7 plans across 5 waves

Plans:
- [x] 07-01-PLAN.md — Harvest run/schedule stores + schemas + D-02 startup reaper + D-17 deck_queue.commander_name additive migration (HARV-07) [Wave 1]
- [x] 07-02-PLAN.md — ArchidektCacheJobService PG migration (drop _jobs dict) + _activeJobCts cancel plumbing + commander capture at MarkProcessed UPDATE site (HARV-01, HARV-03) [Wave 2]
- [x] 07-03-PLAN.md — IHarvestScheduleCache (BackgroundService + sync StartAsync + 30s poller) + HarvestScheduleService 60s tick gated by harvest.cron.enabled (HARV-04, HARV-05) [Wave 2]
- [x] 07-04-PLAN.md — AdminHarvestController (5 antiforgery POSTs) + AdminHarvestViewModel + Index.cshtml four panels per D-11 (HARV-01, HARV-02, HARV-04, HARV-05) [Wave 3]
- [x] 07-05-PLAN.md — GET /Admin/Harvest/status JSON (same-origin gated, 1s IMemoryCache) + admin-harvest.ts 3s setTimeout poll (HARV-01, HARV-03) [Wave 4]
- [x] 07-06-PLAN.md — IHarvestStatsAggregator 60s cache (admin.harvest.stats.v1) + GetTopCommandersAsync + pg_database_size PG-only branch + stats panel Razor (HARV-06) [Wave 5]
- [x] 07-07-PLAN.md — AddDeckFlowHarvest() DI extension + Program.cs wiring + startup IHarvestRunStore.EnsureSchemaAsync awaited before app.RunAsync (cross-cutting) [Wave 4]

---

### Phase 07.1: categories feature flag + SameOrigin AJAX fix (INSERTED)

**Goal:** Hide the broken Categories flow from production users behind a default-on `feature.categories.enabled` flag (operator-toggleable from /Admin/Flags), and fix the regression where the Categories AJAX endpoint returns "This endpoint only accepts same-origin browser requests."

**Depends on:** Phase 7

**Requirements**: CATFLAG-01, CATFLAG-02, CATFLAG-03, CAT-FIX-01

**Success Criteria** (what must be TRUE):

1. Fresh DB after `EnsureSchemaAsync`: `SELECT enabled FROM feature_flags WHERE key = 'feature.categories.enabled'` returns `true` (default-on).
2. With flag ON: Suggest Categories nav entry is rendered, landing-page Categories CTA is rendered, `GET /Deck/SuggestCategories` returns 200.
3. With flag OFF (toggled via /Admin/Flags): nav entry hidden, landing CTA hidden, page route returns 503 + maintenance copy (mirrors FLAG-05 pattern).
4. After toggle, change is visible to users within ~30s without app restart (existing IFeatureFlagCache poll cadence).
5. Categories AJAX endpoint accepts a legitimate same-origin request from the running site without returning the SameOrigin rejection message — investigated against logged Origin / Referer / X-Forwarded-Proto values.

Plans:
- [x] 07.1-01-PLAN.md — feature.categories.enabled seed row + nav/landing gates + page route gate (CATFLAG-01, CATFLAG-02, CATFLAG-03) [Wave 1]
- [x] 07.1-02-PLAN.md — diagnose + fix SameOriginRequestValidator regression on categories AJAX endpoint (CAT-FIX-01) [Wave 1]

### Phase 8: Analytics

**Goal**: Operator can see which pages are being used, how often, and whether errors are spiking — using signal-rich, low-cardinality data drawn from live traffic, with no raw IPs stored.

**Depends on**: Phase 6 (admin shell); Phase 7 not required (analytics is independent of harvest)

**Requirements**: ANLY-01, ANLY-02, ANLY-03, ANLY-04, ANLY-05, ANLY-06

**Success Criteria** (what must be TRUE):

1. After 5 minutes of live traffic, `SELECT DISTINCT route_key FROM request_metrics` returns template strings (e.g. `Deck/Index`) — not literal paths with card names or IDs — confirming no high-cardinality blow-up.
2. `SELECT COUNT(1) FROM request_metrics WHERE route_key LIKE '/css/%' OR route_key LIKE '/js/%'` returns 0 — static assets excluded.
3. `SELECT ip_hash FROM request_metrics LIMIT 1` returns a hash string; no `ip_raw` column exists — confirmed no PII stored.
4. `/Admin/analytics` renders a top-routes table filterable by today / 7d / 30d, each row showing hit count, unique-IP count, error rate, and an inline SVG sparkline — no JavaScript charting library loaded.
5. Render dashboard p95 response time does not regress vs pre-analytics baseline after the middleware deploys (write-behind channel absorbs DB I/O off the hot path).

**Plans**: 5 plans across 5 waves

Plans:
- [x] 08-01-PLAN.md — IpHasher extraction + RequestMetricsStore (schema + bulk UPSERT) + RequestMetricEvent record (ANLY-01, ANLY-03) [Wave 1]
- [x] 08-02-PLAN.md — RequestMetricsBuffer (BoundedChannel DropOldest) + RequestMetricsFlusher (BackgroundService whichever-fires-first) (ANLY-02) [Wave 2]
- [x] 08-03-PLAN.md — AnalyticsMiddleware + AddDeckFlowAnalytics() DI extension + Program.cs wiring (D-12 position, D-15 startup smoke-test) (ANLY-01, ANLY-02, ANLY-03, ANLY-06) [Wave 3]
- [x] 08-04-PLAN.md — AdminAnalyticsController + ViewModel + Index.cshtml + sparkline helper + admin.css (ANLY-04, ANLY-05) [Wave 4]
- [x] 08-05-PLAN.md — Live-traffic SC verification + Render p95 baseline delta capture (all 5 SCs) [Wave 5]

---

### v1.2 Multi-AI Prompts

- [ ] **Phase 9: Bracket UX + AI Selector Foundation** — Bracket visibility fix, AI target picker (ChatGPT/Claude/Gemini) on all three analysis pages, zip round-trip for AI selection
- [ ] **Phase 10: Claude + Gemini Artifact Optimization** — Per-AI artifact format and instructions for Claude and Gemini

---

### Phase 9: Bracket UX + AI Selector Foundation

**Goal**: Users cannot miss the TargetCommanderBracket selector, and all three ChatGPT analysis pages surface an AI target picker (defaulting to ChatGPT) whose selection round-trips through the zip export.

**Depends on**: v1.1 (ChatGPT packet artifacts + zip round-trip infrastructure already in place)

**Requirements**: BRKT-01, AISEL-01, AISEL-04

**Success Criteria** (what must be TRUE):

1. Opening `/chatgpt-packets` shows the TargetCommanderBracket selector with a visual treatment (label, callout, or banner) that makes it impossible to overlook before submitting Step 1.
2. All three ChatGPT analysis pages (Packets, Deck Comparison, CEDH Meta Gap) show an AI target selector with options ChatGPT / Claude / Gemini; ChatGPT is selected by default.
3. Selecting Claude or Gemini and downloading a session zip, then re-uploading the zip, restores the same AI target selection.
4. Selecting Claude or Gemini and running analysis produces a valid artifact (content may be ChatGPT-format temporarily — Phase 10 adds optimization); no errors, no missing files.

**Plans**: 3 plans across 2 waves

Plans:
- [ ] 09-01-PLAN.md — CSS blocks + _AiSelector.cshtml + _BracketCallout.cshtml shared partials (BRKT-01, AISEL-01) [Wave 1]
- [ ] 09-02-PLAN.md — TargetAiPlatform model properties (all 3 requests) + zip round-trip writer/parser/loader (AISEL-01, AISEL-04) [Wave 1]
- [ ] 09-03-PLAN.md — Razor view insertions: _AiSelector in all 3 pages, bracket callout in Packets; visual checkpoint (BRKT-01, AISEL-01, AISEL-04) [Wave 2]

---

### Phase 10: Claude + Gemini Artifact Optimization

**Goal**: When Claude or Gemini is selected, the exported artifact differs from ChatGPT in both file structure and the instructions/system-prompt section, tuned to each AI's strengths.

**Depends on**: Phase 9 (AI selector UI and routing in place)

**Requirements**: AISEL-02, AISEL-03

**Success Criteria** (what must be TRUE):

1. Claude artifact: exported zip contains XML-structured prompt blocks (not markdown fenced code) and a system prompt section that leverages Claude's XML-tag instruction format.
2. Gemini artifact: exported zip uses a layout and instruction style distinct from both ChatGPT and Claude formats, reflecting Gemini's prompt best practices.
3. Pasting a Claude artifact into Claude.ai and a Gemini artifact into Gemini.google.com each produce a relevant, well-formed response without the user reformatting anything.
4. ChatGPT artifact (existing format) is unchanged — Claude/Gemini paths are additive, zero regression on the default flow.

---

## Progress

| Phase | Milestone | Plans | Status | Completed |
|-------|-----------|-------|--------|-----------|
| 1. Visual System Tokens | v1.0 | 3/3 | Complete | 2026-04-30 |
| 2. Layout, Hierarchy & UX Copy | v1.0 | 3/3 | Complete | 2026-04-30 |
| 3. Tech-Debt Cleanup | v1.0 | 4/4 | Complete | 2026-05-01 |
| 4. Security & Bug Fixes | v1.0 | 4/4 | Abandoned (rerouted to Ph. 5) | 2026-05-02 |
| 5. Security & Bug Fixes v2 | v1.0 | 3/3 | Complete | 2026-05-02 |
| 6. Admin Shell + Flags Foundation | v1.1 | 7/7 | Complete | 2026-05-03 |
| 7. Harvest Controls + Stats | v1.1 | 7/7 | Complete | 2026-05-03 |
| 7.1 Categories Flag + SameOrigin Fix | v1.1 | 2/2 | Complete | 2026-05-03 |
| 8. Analytics | v1.1 | 5/5 | Complete | 2026-05-08 |
| 9. Bracket UX + AI Selector Foundation | v1.2 | 0/3 | Not started | — |
| 10. Claude + Gemini Artifact Optimization | v1.2 | 0/? | Not started | — |

---

*v1.1 roadmap created: 2026-05-02 | v1.2 roadmap created: 2026-05-08*
