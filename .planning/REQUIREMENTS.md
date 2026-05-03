# Requirements: DeckFlow v1.1 Admin Console

**Defined:** 2026-05-02
**Core Value:** Every supported workflow must produce ChatGPT-paste-ready output in one round-trip — without the user reformatting anything. This milestone serves the operator running DeckFlow, not end users; admin tooling must keep the public site green while making harvest, flags, and usage observable from the browser.

## v1.1 Requirements

Requirements for the v1.1 milestone. Each maps to exactly one roadmap phase. Categories: ADMIN (shell + auth), HARV (harvest controls + stats), ANLY (page-usage analytics), FLAG (runtime feature flags).

### Admin Shell

- [x] **ADMIN-01**: Operator can navigate `/Admin` landing page with sidebar nav listing Feedback, Harvest, Analytics, Flags (and reserved future slots), with active-page indicator
- [x] **ADMIN-02**: All admin pages render through a dedicated `_AdminLayout.cshtml` that loads only neutral admin CSS — no guild theme leakage, no public-site nav
- [x] **ADMIN-03**: BasicAuth gate via existing `/Admin` path branch keeps protecting every admin page (Feedback, Harvest, Analytics, Flags) without per-controller `[Authorize]` drift
- [x] **ADMIN-04**: Existing `/Admin/feedback` page continues to work unchanged inside the new admin shell (no regression to inbox / mark-read flow)
- [x] **ADMIN-05**: All admin POST forms protected with `[ValidateAntiForgeryToken]` (Razor antiforgery, since `SameOriginRequestValidator` covers JSON APIs only)

### Harvest

- [ ] **HARV-01**: Operator can trigger an Archidekt harvest run-now with a duration cap chosen from a preset list (15 / 30 / 60 min)
- [ ] **HARV-02**: Operator can submit a single Archidekt deck URL and have it harvested on demand (independent of the bulk crawler)
- [ ] **HARV-03**: Operator can cancel a running harvest; cancellation is graceful (current deck completes, then stops — no torn-up DB writes)
- [ ] **HARV-04**: Operator can pause and resume the recurring harvest schedule (pause halts the schedule; in-flight run continues to graceful completion)
- [ ] **HARV-05**: Operator can configure a recurring harvest schedule via a friendly interval picker (Off / Every 2h / 4h / 8h / 24h), persisted in Postgres so it survives Render redeploy
- [ ] **HARV-06**: `/Admin/harvest` shows a stats panel: total decks harvested (lifetime + last 30 days), total observations / cards harvested, top-N commanders by deck count, recent runs log (last 10), Postgres storage size, last successful run timestamp + next scheduled run
- [ ] **HARV-07**: Harvest run history is persisted to Postgres (`harvest_runs` table) so the recent-runs log survives Render redeploys, not in-memory only

### Analytics

- [ ] **ANLY-01**: Per-request middleware records (route template, day, count, unique-IP, error-rate) into a Postgres `request_metrics` table, using route template (not raw path) to prevent high-cardinality blow-up
- [ ] **ANLY-02**: Middleware uses a write-behind buffer (bounded `Channel` + `BackgroundService` flusher) so hot-path requests do not pay synchronous DB I/O latency
- [ ] **ANLY-03**: Unique-IP count uses hashed CF-Connecting-IP (existing `FEEDBACK_IP_SALT`) so no raw IPs are stored
- [ ] **ANLY-04**: `/Admin/analytics` lists top routes by hit count for a chosen time window (today / 7d / 30d / all-time)
- [ ] **ANLY-05**: Each route row shows a daily sparkline rendered as inline SVG (no JS charting library, no external dependency) plus error-rate column
- [ ] **ANLY-06**: Static-asset routes (`/css/*`, `/js/*`, `/lib/*`, `/extensions/*`) are excluded from `request_metrics` to keep the table small and signal-rich

### Feature Flags

- [x] **FLAG-01**: Postgres `feature_flags` table seeded by `EnsureSchemaAsync` with default-on rows for shipped features (no default-off accidentally killing live behavior on fresh DB)
- [x] **FLAG-02**: Singleton `IFeatureFlagCache` holds the flag dict in-memory; refreshed by a 30s `BackgroundService` poller, plus explicit invalidation on admin write so toggle takes effect within seconds
- [x] **FLAG-03**: Operator can list all flags and toggle bool values from `/Admin/flags`; admin write triggers cache invalidation immediately
- [x] **FLAG-04**: `ScryfallTaggerService` consults `IFeatureFlagCache` and returns empty results (no upstream call) when its kill-switch flag is off
- [x] **FLAG-05**: Page kill-switch pattern: a chosen route returns 503 + maintenance copy when its flag is off, demonstrated end-to-end on at least one user-facing page

## v1.2+ Requirements

Deferred to future milestone. Tracked but not in v1.1 roadmap.

### Admin polish

- **POLISH-01**: Sidebar status badges (job running, unread feedback count) — low value vs. plumbing the four pages first
- **POLISH-02**: Feature-flag audit log (`flag_audit_log` table) — useful for "why is Tagger off?" archaeology, not blocking v1.1
- **POLISH-03**: Non-bool flag types (string / int / json) — bool-only is sufficient for v1.1 kill switches
- **POLISH-04**: Admin sidebar collapse / breadcrumb / mobile nav — single-operator desktop-only is fine for v1.1

### Harvest polish

- **HARV-NEXT-01**: Per-commander deck-count distribution chart on the harvest stats panel
- **HARV-NEXT-02**: Harvest velocity chart (decks-per-hour over time)
- **HARV-NEXT-03**: Free-form crontab string editor (in addition to the friendly picker)
- **HARV-NEXT-04**: Per-job error-rate trend on the recent-runs log

### Analytics polish

- **ANLY-NEXT-01**: Per-IP session drill-down (PII concern — defer with explicit privacy review)
- **ANLY-NEXT-02**: Per-route p95 / p99 response time tracking (use Render dashboard for now)
- **ANLY-NEXT-03**: Referer breakdown
- **ANLY-NEXT-04**: Outbound-API error-rate summary (Scryfall, Tagger, Spellbook) on the same page

## Out of Scope

Explicitly excluded. Documented to prevent scope creep mid-milestone.

| Feature | Reason |
|---------|--------|
| Multi-user admin auth (session cookie, RBAC) | Single-operator BasicAuth is sufficient for current ops volume; RBAC would dwarf the rest of v1.1 |
| Raw Serilog log tail / file viewer page | Render dashboard already streams stdout; usage analytics is the higher-leverage signal |
| Cache flush button, Postgres connection test, Render restart, manual artifact cleanup | Not blocking v1.1; can land as a future "ops actions" tile if a real need surfaces |
| Free-form crontab string editor | Friendly interval picker covers the actual operator need; crontab parsing edge cases are an injection / foot-gun risk |
| Mid-HTTP harvest cancel (kill in-flight requests) | Risks torn DB writes in `category_knowledge`; graceful "stop after current deck" gives the same outcome safely |
| Charting libraries (Chart.js, ApexCharts, etc.) | Inline SVG sparklines + Razor-rendered tables meet the need with zero new client dependency |
| OpenTelemetry / Prometheus exporter | No collector infrastructure; ops model is a single Render Starter web — analytics middleware writing to Postgres is the right altitude |
| External analytics beacons (GA, Plausible, etc.) | CSP + trust concerns; admin-only stats stay first-party |
| `Microsoft.FeatureManagement` / `Hangfire` / `Quartz.NET` | Each is heavier than the bespoke equivalent for our data shape; rationale captured in research/STACK.md |
| Mid-cycle migration framework (FluentMigrator etc.) | `EnsureSchemaAsync` + `CREATE TABLE IF NOT EXISTS` pattern still works for four new tables; revisit once the table count climbs |
| UI audit re-score (≥ 20/24) | Carried forward from v1.0 but split into its own UI-audit milestone — not coupled to admin tooling |

## Traceability

Which phases cover which requirements. Filled by gsd-roadmapper during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| ADMIN-01 | Phase 6 | Complete |
| ADMIN-02 | Phase 6 | Complete |
| ADMIN-03 | Phase 6 | Complete |
| ADMIN-04 | Phase 6 | Complete (06-03) |
| ADMIN-05 | Phase 6 | Complete (06-05) |
| HARV-01 | Phase 7 | Pending |
| HARV-02 | Phase 7 | Pending |
| HARV-03 | Phase 7 | Pending |
| HARV-04 | Phase 7 | Pending |
| HARV-05 | Phase 7 | Pending |
| HARV-06 | Phase 7 | Pending |
| HARV-07 | Phase 7 | Pending |
| ANLY-01 | Phase 8 | Pending |
| ANLY-02 | Phase 8 | Pending |
| ANLY-03 | Phase 8 | Pending |
| ANLY-04 | Phase 8 | Pending |
| ANLY-05 | Phase 8 | Pending |
| ANLY-06 | Phase 8 | Pending |
| FLAG-01 | Phase 6 | Complete (06-02) |
| FLAG-02 | Phase 6 | Complete |
| FLAG-03 | Phase 6 | Complete (06-05) |
| FLAG-04 | Phase 6 | Complete |
| FLAG-05 | Phase 6 | Complete |

**Coverage:**
- v1.1 requirements: 23 total
- Mapped to phases: 23
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-02*
*Last updated: 2026-05-02 — traceability filled by gsd-roadmapper*
