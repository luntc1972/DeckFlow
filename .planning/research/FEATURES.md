# Feature Research — v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup

**Domain:** MTG cEDH/Commander deck-analysis tool; admin-curated content ingestion (YouTube + podcast) → LLM-tagged clip store; admin mobile-responsive sweep; Gemini paste-limit unblock; doc-comment + modal v1.3 backlog
**Researched:** 2026-05-23
**Confidence:** HIGH on existing-system reuse (codebase grounded); MEDIUM on YouTube caption legality + Whisper $/min figures (verified via current vendor docs but figures shift quarterly); HIGH on MTG taxonomy (Wizards official bracket system Feb 2025)
**Audience:** `gsd-roadmapper` — one phase per feature category, sequenced by dependencies

> **Scope note:** This file scopes **only** the 7 NEW v1.4 features. Existing features (deck import, AI-agnostic prompt artifacts, admin shell + flags, harvest stats, packet session cache, category-knowledge crawl) are out-of-scope and not re-researched. Deck-analysis integration of content ("What experts say" panel + prompt injection) and new-deck wizard are explicitly DEFERRED to v1.5 — flagged as anti-features for v1.4 below.

---

## Feature 1: Content Knowledge Base — Ingestion Pipeline (YouTube + Podcast → Transcript → LLM Tags)

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Idempotent harvest (re-running same source does not duplicate videos/episodes) | Any ingestion pipeline must survive crashes + retries without double-billing transcription. Mirrors v1.1 `HarvestRunStore` invariant. | Dedupe on `(source_id, external_id)` unique key; YouTube video ID + podcast `<guid>` are stable identifiers. |
| YouTube auto-captions tried first, Whisper fallback only when missing/empty | Captions are free + instant; Whisper is paid + slow. Reversing this order destroys the cost model. | Captions empty/404 ⇒ pull audio stream ⇒ Whisper. |
| Per-clip start/end timestamps preserved | Users must be able to deep-link to the moment in source. Mirrors industry SRT/VTT segment format (every segment has start + duration in seconds). | Persist as `(start_seconds, end_seconds)` on `clips` table; emit `youtube.com/watch?v=ID&t=NNs` deep-link for YouTube. |
| Per-video LLM summary (≤ 500 words) AND per-clip excerpts | Two-tier granularity — summary for browse, clips for retrieval/citation. Industry chunking pattern. | One LLM call per video for summary; clips emitted as side-effect of the same call OR a second clip-extraction pass. |
| Source language tracking | Multi-lingual content (Spanish/Japanese cEDH channels exist) needs language tag for filtering + downstream prompt-builder. | Persist `language` (BCP-47, e.g. `en`, `es-MX`) on `transcripts` table; default `en` if YouTube doesn't expose it. |
| Per-call cost row written to spend ledger (idempotent) | Without it the monthly cap (Feature 3) cannot be computed accurately. Critical for the cost-cap contract. | Spend row keyed on `(harvest_run_id, asset_id, kind)`; re-runs of same audio are zero-add. |
| Visible status per source: last-harvested, next-eligible, error-state | Without status, operator has no signal whether to re-run. Mirrors v1.1 harvest controls UX. | Reuse `HarvestRunStore` pattern: store `last_run_id`, `last_run_at`, `last_status` on `sources`. |
| Admin-triggered manual run (no scheduler) | v1.4 milestone defines this explicitly — scheduled cron deferred to v1.5. | Single "Run harvest now" button per source row. |
| Hard transcript-size cap per video | Long videos (3+ hour podcast episodes are common in MTG content) blow up Whisper bills + LLM token costs. Industry convention: chunk transcripts ≥ 5000 chars before LLM. | Reject videos > N minutes OR truncate audio at N minutes with operator-visible flag; surface in source detail. |
| Cancelable harvest job | Long pipelines need a kill switch — operator may notice runaway spend mid-run. | Cancellation token threaded through harvest service; on cancel, mark run `aborted`, partial transcripts persisted. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Re-tag existing transcripts without re-transcribing | Lets operator iterate on taxonomy (Feature 2) without paying Whisper again. | Separate `re-tag` action: re-runs LLM step against stored transcript only. |
| Per-clip confidence score from LLM (or word-error-rate from Whisper) | Helps operator triage low-quality clips before they pollute the knowledge base. | Whisper `avg_logprob` per segment; LLM self-report `confidence: 0.0-1.0` on each tag. |
| Source health: "last 3 harvests succeeded / failed" miniature trend on source row | Spots channels that have changed format/started gating captions. | Postgres CTE over `harvest_runs` last N rows. |
| Per-clip "kept" / "rejected" admin flag | Lets operator manually curate noise out before it reaches v1.5 deck-analysis injection. | `clips.is_kept` boolean default true; admin can flip; v1.5 panel queries `WHERE is_kept`. |
| Podcasting 2.0 `<podcast:transcript>` element detection | When podcast feed exposes pre-built transcript, skip Whisper entirely. ~5-10% of MTG podcasts opt in; saves $ when available. | Parse `<podcast:transcript>` namespace element; if URL is text/SRT/VTT, fetch + skip Whisper. |
| Spot-check sample-replay link in admin | Click → opens YouTube at the clip's start timestamp; verifies the clip excerpt matches the actual video moment. | Trivial: link emit only. High operator confidence return. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Deck-analysis prompt injection** ("Inject expert clips into AnalysisPrompt for matching commander") | Explicit v1.4 scope boundary per PROJECT.md. v1.5 generalizes integration model (commander/color join surface doesn't exist in v1.4 tag schema). | v1.5 |
| **"What experts say" UI panel on deck-analysis pages** | Same boundary — v1.4 stops at storage. | v1.5 |
| **Public-user content submission** | Single-operator admin model. Public submission opens spam, moderation, legal-DMCA surface. CLAUDE.md "admin-only single-operator" constraint. | Never (or own milestone with full content-moderation workflow) |
| **Scheduled (cron / IHostedService) harvest** | PROJECT.md explicit: "Manual admin-triggered harvest (no scheduler in v1.4)". | v1.5 |
| **Full-text search across clip excerpts** | Storage schema must permit it later, but the search UI itself is not v1.4 scope. Postgres `tsvector` column can be added without breaking changes. | v1.5+ |
| **Multi-language transcription / translation** | Single-operator volume + English-first MTG content scene means EN-only is sufficient for v1.4 first cut. Schema preserves `language` for future. | v1.5+ |
| **Whisper diarization (per-speaker labels)** | $0.006/min flat vs $0.006/min with diarization; v1.4 doesn't need "who said what" for tag extraction. | v1.5+ if podcast multi-host attribution surfaces as need |
| **Auto-detection of new videos in already-curated channel** | Crosses into "scheduler" territory. v1.4 = admin clicks "Run harvest" → pipeline pulls latest N videos. | v1.5 |
| **OAuth-authenticated YouTube captions API** | Official Google.Apis.YouTube.v3 `captions.download` REQUIRES OAuth and ONLY works for videos you own. Channel-creator captions are NOT downloadable via the official API for third parties. Use scraping library (YoutubeExplode) instead — its `Videos.ClosedCaptions.DownloadAsync` works without API key and returns SRT. | Never (architectural — official API cannot satisfy requirement) |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| `HarvestRunStore` (v1.1 `IHarvestRunStore`) | Source-of-truth pattern for harvest run lifecycle: `running` / `complete` / `error` / `aborted`. v1.4 either reuses table directly with new `kind` discriminator OR adds parallel `content_harvest_runs` table mirroring the schema. **Recommend: parallel table** because clip+spend joins don't fit category-harvest schema and Phase 999.6's `GetByIdAsync` production-bug fix proved that table is tightly coupled to category-harvest flow. |
| `IFeatureFlagStore` / `IFeatureFlagCache` (v1.1 ADMIN-04/FLAG-01..05) | Flag-gate content KB Phase 1 behind `content_kb_ingestion_enabled` so it can ship dark and be flipped on per-environment. Mirrors `DECKFLOW_GEMINI_ENABLED` pattern. |
| `RelationalDatabaseConnection` + `IRelationalDialect` (Sqlite + Postgres) | New `sources`, `videos`, `transcripts`, `summaries`, `clips`, `content_tags`, `content_spend_ledger` tables added via existing `EnsureSchemaAsync` migration seam. Schema MUST work on both SQLite (local dev) and Postgres (prod). |
| `BasicAuthMiddleware` on `/Admin/*` | CRUD pages live at `/Admin/Sources` — already auth-gated for free. |
| `SameOriginRequestValidator` | Any JSON POST endpoints (run-harvest action) reuse the existing CSRF guard. |
| `IHttpClientFactory` + `ResiliencePipelineProvider<string>` | YouTube + podcast HTTP egress goes through new named clients (`youtube-captions`, `podcast-rss`, `whisper-api`, `openai-llm`) with Polly pipelines. **MUST NOT** introduce new HTTP plumbing — see ARCHITECTURE.md anti-patterns. |
| Serilog structured logging | Each harvest step logs `{HarvestRunId} {SourceId} {VideoId} {Step}` for triage; cost-ledger writes log `{Cost} {Provider} {Asset}`. |
| `UpstreamErrorMessageBuilder` | New `BuildWhisperMessage` / `BuildYouTubeMessage` helpers for operator-facing error strings. |

### Complexity: **LARGE** (6+ plans)

Phase suggestion:
1. Schema + migrations (sources, videos, transcripts, summaries, clips, content_tags, spend_ledger) on both SQLite + Postgres dialects
2. Source CRUD + admin UI (split out as Feature 2 below — could be its own phase)
3. YouTube ingest adapter (YoutubeExplode caption fetch + audio stream extraction for Whisper fallback)
4. Podcast RSS adapter (`System.ServiceModel.Syndication` or `FeedReader` + `<podcast:transcript>` detection + audio enclosure pull)
5. Whisper API service (with idempotent cost-ledger write — see Feature 3)
6. LLM tagging service (chunk → JSON-structured-output → per-clip rows)
7. Harvest orchestrator + admin "Run Now" action + cancellation
8. End-to-end smoke test on a real source (single video harvest from a real cEDH channel)

### Notes

- **Critical legal/TOS callout:** Scraping YouTube captions via YoutubeExplode is a gray area (YouTube ToS prohibits scraping; library author explicitly notes "raw page data and exploiting reverse-engineered internal endpoints"). Risk is operational (YT changes internal API → library breaks → harvest fails until library updates), not legal at single-operator harvest cadence. Mitigation: pin YoutubeExplode version + admin-visible "library outdated" error message; Whisper-only fallback path covers it when scraping breaks.
- **Timestamp format:** Persist as numeric `start_seconds` / `end_seconds` (industry standard, SRT/VTT use `HH:MM:SS,mmm` only as display). Compute `&t=NNs` YouTube deep-link at render time.
- **Clip granularity:** Industry convention from RAG/video-chaptering practice: target ~30-90 second clips, ~3-7 sentences per clip. LLM-driven (let the model pick semantically-coherent breaks) beats fixed-window chunking for retrieval quality.
- **LLM structured output:** Use OpenAI Structured Outputs (`response_format: json_schema`) or Gemini structured-mode for reliable tag extraction. Don't regex-parse JSON from a prose response — known anti-pattern that produces silent breakage.

---

## Feature 2: Admin Source CRUD UI (List / Add / Edit / Delete, Harvest History, Spend Dashboard)

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| List page: sources table with name, type (YouTube/Podcast), last-harvest, next-eligible, current month spend | Standard admin "index" view. Mirrors v1.1 `/Admin/Feedback` list pattern. | Reuse admin table semantics from v1.1 Phase 8 analytics. |
| Add: form with kind (YouTube channel URL / podcast RSS URL), display name, optional default tag-set | Bare minimum CRUD create. | Form validates URL format + (for YouTube) channel-ID extraction. |
| Edit: same form pre-populated | Operator inevitably needs to rename / re-tag. | Standard MVC PRG (post-redirect-get). |
| Delete (soft delete; cascade-block if harvest runs exist OR cascade-delete with confirm) | Hard delete loses audit trail. Soft delete preserves cost-ledger references. | Recommend soft delete (`is_archived` flag) + filter by default; provide "show archived" toggle. |
| Confirm dialog on Delete (focus-trapped — see Feature 6) | WCAG 2.2 + existing v1.3 WDG-04 alignment. | Native `<dialog>` with `showModal()` per current research; auto-traps focus + Escape handler. |
| Per-source detail page: harvest history table (run, started, duration, videos processed, spend, status) | Drill-down for triage. | Paginate harvest_runs query. |
| Per-source detail page: "Run harvest now" button | The only way to trigger ingestion in v1.4 (no scheduler). | POSTs to API endpoint guarded by `SameOriginRequestValidator`; live-region announces start/complete. |
| Spend dashboard: this-month total, projected EOM, per-source breakdown | Without it, the cost cap is invisible. Mirrors Cloudflare/Azure budget-alert dashboards. | Single page or admin landing tile; numeric + progress-bar against cap. |
| Audit trail: who-edited-when (single-operator → just timestamps) | Even single-operator deployments benefit from "I changed this 3 days ago and forgot" recovery. | `updated_at` column + `last_change_note` optional text. |
| Empty states for first-time-admin (zero sources) | "Add your first YouTube channel" CTA instead of empty grey table. | Standard `[!empty]` partial pattern. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Bulk import sources via CSV/JSON paste | Operator may want to seed 20 channels at once. Cheap to add. | Textarea + parse + per-row validation. |
| Source-level cost cap (in addition to global monthly cap) | Lets operator say "no more than $5/mo on this one channel" — guards against one runaway feed. | Optional column `per_source_monthly_cap_usd`; enforce in harvest pre-flight. |
| Duplicate-source detection on Add | "You've already added this channel" check at form time. | Hash on canonical channel ID / RSS URL. |
| "Preview" action on Add: pull last video metadata WITHOUT harvesting | Confirms URL is parseable + reachable before committing operator to a cost run. | Read-only call to YouTube/RSS adapter that returns 1-row preview. |
| Tag pre-selection per source | "All clips from this channel are cEDH-focused" — pre-seeds bracket=5 tag on all clips from this source. | Persist as JSON column `default_tags`; merge with LLM-extracted tags. |
| Inline status pill (running / idle / errored / capped) per source row | At-a-glance triage on the list page. | Color-coded badge using existing site-common.css token system. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Public-facing source-list page** | Admin-only operator surface. Public exposure crosses into v1.5 content-integration scope + likely needs licensing/attribution review. | v1.5+ (only if v1.5 deck-analysis panel exposes "source attribution") |
| **Multi-tenant source ownership** | Single-operator deployment. "Owner" column would be dead weight. | Never (or full multi-user rewrite) |
| **Public-user upvote/downvote on clips** | Same multi-tenant boundary. | Never in current product shape |
| **Rich-text editor for source description** | Description column is plain-text label only. Anything richer is yak-shaving. | Never |
| **Webhook fire on harvest complete** | No webhook consumer exists. YAGNI. | When v1.5 prompt-injection wants reactive refresh |
| **Drag-reorder source priority** | No "priority" semantic in v1.4 — harvest is manual one-source-at-a-time. | If v1.5 scheduler introduces priority queue |
| **Channel/podcast discovery (search YouTube from within admin)** | Operator already has a URL in hand — discovery flow is product-adjacent feature creep. | Never (or own discovery milestone) |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| Admin shell `_AdminLayout` (v1.1 ADMIN-01) | New `/Admin/Sources/*` pages slot into existing sidebar nav. Add 1-2 new sidebar items. |
| Admin table CSS (v1.1 Phase 8) | Reuse existing `<table class="admin-table">` styles; do NOT fork. |
| `BasicAuthMiddleware` | `/Admin/Sources/*` routes auto-gated. |
| Live-region announcer (v1.1 HARV-07 → v1.3 WDG-10) | "Harvest started" / "Cap reached" announcements use existing `<div aria-live="polite">` pattern. |
| `IFeedbackStore` BasicAuth + Postgres-throttle (v1.0 Phase 5) | Same admin-auth perimeter; nothing new needed. |
| `df-select` ARIA combobox (v1.0/v1.3 WDG-02) | Source-type dropdown (YouTube / Podcast) reuses. |

### Complexity: **MEDIUM** (3-5 plans)

Phase suggestion:
1. List + add + edit + delete forms (CRUD + Razor views + view models)
2. Per-source detail page + harvest history list
3. "Run harvest now" action wired to Feature 1 orchestrator + live-region
4. Spend dashboard (depends on Feature 3's ledger schema landing first)
5. Empty states + accessibility (focus-trapped delete confirm — see Feature 6)

### Notes

- **Strong dependency on Feature 1 schema** — CRUD UI cannot be built before `sources` / `harvest_runs` / `spend_ledger` tables exist. Sequence: Feature 1 schema plan → Feature 2 CRUD.
- **Soft dependency on Feature 6 (focus-trapped modal)** — Delete-confirm dialog SHOULD use the same modal primitive as v1.4-modal-replacement to avoid two patterns. Sequence: ship Feature 6 first, then Feature 2 deletion flow reuses it.

---

## Feature 3: Whisper Cost Cap + Spend Tracking

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Hard monthly $ cap, configurable per environment via env var | The contract: "abort harvest when cap hit". Without cap = uncapped Whisper bill = $$$. | `WHISPER_MONTHLY_CAP_USD` env var; default e.g. $10; admin UI reads it but cannot edit (env-config only — secrets-style). |
| Current-month spend total visible on admin landing | First-glance ops signal. | Admin landing tile showing "$X.XX / $Y.YY this month". |
| Per-call cost row written to ledger (idempotent on `(harvest_run_id, asset_id, kind)`) | Without per-call rows, you cannot audit / debug / forecast. Idempotency prevents double-billing on retry. | `content_spend_ledger` table: `(id, harvest_run_id, asset_id, kind, provider, units, unit_cost_usd, total_usd, created_at)`. |
| Pre-flight check: estimate cost before transcribing (audio duration × $/min) | Lets the harvester abort BEFORE making the spend, not after. Critical for cap enforcement. | YoutubeExplode exposes stream duration; podcast RSS often has `<itunes:duration>`; estimate = `duration_min * cost_per_min`. |
| Threshold alert before hard cap (e.g. 80% warning in logs + admin badge) | Stops surprises. Industry: Cloudflare / GCP / Azure all do soft + hard thresholds. | Configurable threshold (default 80%); emit `Warning` log + admin "approaching cap" badge. |
| Cap is per-calendar-month (resets 1st of month UTC) | Operator can budget month-over-month. Avoids rolling-window confusion. | `WHERE created_at >= date_trunc('month', now() at time zone 'UTC')`. |
| Pricing constants in code (e.g. `WhisperPricing.PerMinuteUsd = 0.006m`) NOT in env var | Vendor pricing changes are deployment events, not config events. Keep in source so update is reviewable. | `static class WhisperPricing { public const decimal PerMinuteUsd = 0.006m; }`; gets bumped via PR when OpenAI changes pricing. |
| Cap reached → harvest aborts mid-run with operator-visible reason | Silent abort = mystery. | Mark harvest run `aborted_cap_reached`; admin row shows "Stopped: monthly cap reached"; surface in live-region. |
| Spend logged to Serilog with structured fields | Enables external triage via Render log search. | `_logger.LogInformation("Whisper spend {Provider} {Asset} {Minutes} {CostUsd}", ...)`. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Per-source breakdown on dashboard | Which channel/podcast is burning the budget? | Postgres GROUP BY source_id over current month. |
| Per-kind breakdown (Whisper vs LLM-tagging) | LLM tagging is the other paid step; both contribute to TCO. Splitting helps the operator decide where to optimize. | Add `LlmPricing.Per1MTokensUsd` constant; LLM cost ledger rows share table with Whisper rows via `kind` column. |
| 7-day trend sparkline on dashboard | Detects spikes early. | Tiny inline SVG generated server-side; no JS chart lib needed. |
| Forecast: "at current burn rate, monthly cap hit on day N" | Helps operator decide whether to slow harvest cadence. | Linear extrapolation from current month spend / days elapsed. |
| Export ledger as CSV | Audit / tax purposes for serious operators. | Single `?export=csv` endpoint. |
| Soft-cap (warn-only) vs hard-cap (abort) mode | Some operators want "tell me but don't stop me". | `WHISPER_CAP_MODE=hard|soft` env var (default `hard` for safety). |
| Dry-run mode | Operator can do `--dry-run` to estimate harvest cost without paying. | New admin checkbox on Run-Harvest action; pipeline runs through duration estimation only and reports total. |
| Real-time cap-remaining indicator while harvest is running | Inline live-region update as ledger grows. | Polling endpoint or SSE; simplest = poll every 10s during run. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Edit cap from admin UI** | Cap is secrets-tier config; UI editing means race conditions + accidental zeroing + audit-trail gap. Env-var-only is safer for single-operator. | Never (architectural choice) — if needed, build a "config override" feature with full audit log |
| **Automatic email/SMS alerts** | No outbound-comms infra in DeckFlow today. Adding SMTP/Twilio is its own milestone. Render dashboard + Serilog logs cover triage. | When ops scale demands it |
| **Multi-currency support** | Single-operator USD-only billing reality (OpenAI bills USD). | When non-USD usage surfaces |
| **Per-user spend attribution** | Single-operator deployment — no users to attribute to. | If multi-tenant rewrite ever happens |
| **Automatic Whisper-model downgrade on cap approach** (e.g. fall back to GPT-4o-mini-transcribe at $0.003/min) | Adds quality-vs-cost branching complexity that v1.4 doesn't need. | v1.5+ if cost pressure justifies it |
| **Stripe/payment-provider integration** | DeckFlow doesn't bill anyone; this is OUR spend, not user spend. | Never |
| **Automatic cap-bump-on-overrun** | Defeats the purpose of a cap. | Never |
| **Predictive ML cost models** | Linear projection is enough at v1.4 volume. Anything ML is overkill. | Never |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| `RelationalDatabaseConnection` + `IRelationalDialect` | New `content_spend_ledger` table via `EnsureSchemaAsync`. |
| Serilog structured logging | Per-call cost log lines for external triage. |
| Admin shell + landing page | Spend tile + dashboard slot into existing admin chrome. |
| Env var config pattern (`DECKFLOW_*` prefix per CLAUDE.md) | New `WHISPER_MONTHLY_CAP_USD`, `WHISPER_CAP_THRESHOLD_PCT`, `WHISPER_CAP_MODE` env vars follow same pattern; not committed (Render dashboard with `sync: false`). |
| `BasicAuthMiddleware` | Dashboard + ledger CSV export auto-gated. |
| Live-region announcer | "Cap reached, harvest stopped" announcement. |

### Complexity: **MEDIUM** (3-5 plans)

Phase suggestion:
1. Spend ledger table + idempotent write seam
2. Pre-flight cost estimator + hard-cap enforcement in harvest orchestrator
3. Admin dashboard page (current month total, per-source breakdown, projected EOM)
4. Threshold alert + admin badge + Serilog warning
5. CSV export + dry-run mode (could be combined with Feature 2 enhancements)

### Notes

- **Pricing reference (verify at implementation time):** Whisper-1 = $0.006/min flat; GPT-4o-mini-transcribe = $0.003/min. Source: openai.com/api/pricing (May 2026). Pricing has shifted twice in 2025 — bake an assertion-test that hard-codes the constant value matches a comment with the date verified.
- **Real-world overhead:** Reports indicate effective cost is ~$0.010/min after retries + rounding. Build the cap with a 20% safety margin (e.g. if user sets `WHISPER_MONTHLY_CAP_USD=10`, abort at $8 effective spend) OR bake the safety margin into the constant. Operator-tunable via threshold % is cleaner.
- **LLM cost also matters:** GPT-4o input ~$2.50/M tokens, output ~$10/M tokens; Gemini Flash much cheaper. Ledger should be provider-agnostic (`provider` column = `"openai-whisper"` / `"openai-gpt-4o"` / `"google-gemini-flash"`). Build the schema right the first time; capping non-Whisper providers is a future drop-in.

---

## Feature 4: Admin Mobile-Responsive Sweep

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Sidebar collapses to hamburger / off-canvas on narrow viewports | Standard admin-panel responsive pattern. Bootstrap, AdminLTE, every SaaS admin template does this. | Breakpoint at ~768px; toggle button stays visible; sidebar slides in via CSS transform. |
| Admin tables either `overflow-x: auto` OR card-stack layout on narrow | Tables are the dominant admin chrome. Without responsive handling, columns clip silently. | Recommend `overflow-x: auto` for data-dense tables (preserves column relationships); card-stack for ≤4-column tables. Both patterns are industry-standard. |
| Forms reflow single-column on narrow | Multi-column forms collapse to one column under ~500px. | CSS grid auto-fit or single media query. |
| All touch targets ≥44×44 px | WCAG 2.5.5 + Apple HIG + Material guidelines all converge on 44px. Extends v1.3 WDG-04 a11y primitives sweep to admin shell. | Audit all admin buttons / links / icon-actions; bump small ones via padding. |
| `touch-action: manipulation` on tappable elements | Prevents 300ms double-tap zoom delay on mobile. Already established in v1.3 WDG-04 site-common.css primitive. | Extend the existing primitive to cover admin selectors. |
| `:focus-visible` admin sweep | Already in site-common.css per v1.3 WDG audit. Confirm admin elements inherit / aren't overridden by admin.css. | Audit; add missing where overridden. |
| Mobile-tested at one realistic small viewport (375px iPhone SE) | Industry-standard mobile baseline. | Manual UAT against 375px. |
| No horizontal-scroll on the page-as-whole | Tables can scroll horizontally inside their wrapper; the page must not. Common regression. | Audit + CSS `overflow-x: hidden` on `<body>` if needed. |
| Hamburger button is keyboard-accessible + screen-reader-labelled | A11y baseline. | `<button aria-label="Toggle navigation" aria-expanded="...">` standard pattern. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Sidebar state persists in `localStorage` (collapsed/expanded preference) | Operator who prefers collapsed sidebar isn't re-collapsing on every page load. | Single TS module, ~20 lines. |
| Sticky table headers on long scrollable tables | Preserves column context as operator scrolls. | `position: sticky` CSS; one rule, zero JS. |
| Card-stack pattern for harvest-history detail rows on narrow | More mobile-readable than horizontal-scroll for nested data. | CSS media query; same data, different layout. |
| Mobile-first nav badge counts (e.g. "Errors: 3" next to nav item) | At-a-glance triage on tiny screens where the dashboard tile is below the fold. | Reuse existing badge styling. |
| Swipe gesture to open sidebar | Native-app polish. Low ROI in v1.4 single-operator context; skip. | Only if operator explicitly asks |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **PWA install / offline-first admin** | Out-of-scope per PROJECT.md (deferred from v1.0). Admin doesn't need offline mode. | Own milestone |
| **Native mobile app** | Web admin is sufficient. | Never |
| **Tablet-specific layout breakpoint** | Two breakpoints (mobile + desktop) is enough at v1.4 scale. Three breakpoints means triple QA surface. | If admin users emerge who actively use tablets |
| **Dark mode toggle on admin** | Admin shell already uses neutral / minimal theming (separate from public guild themes). Adding dark-mode is its own design pass. | Own design milestone |
| **Reordering / customizing sidebar** | Single-operator; no need. | Never |
| **Full redesign of admin chrome** | Scope is "sweep" not "redesign". Touch the responsive seams, leave structure intact. | Own redesign milestone |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses / Touches It |
|---|---|
| `_AdminLayout.cshtml` (v1.1 ADMIN-01) | Touched: add hamburger toggle markup + sidebar wrapper. |
| `admin.css` (v1.1) | Touched: add `@media (max-width: 768px)` blocks. |
| `site-common.css` a11y primitives (v1.3 WDG-04) | Reused: `touch-action: manipulation`, `:focus-visible` rings, ≥44px touch targets. Per CLAUDE.md, layout CSS lives in site-common.css NOT site.css. |
| Existing admin views (Feedback, Harvest, Analytics + new Sources views) | Touched: form widths, table wrappers; ideally driven by class-level changes not per-page edits. |
| TypeScript build pipeline (`wwwroot/ts/*.ts` → `wwwroot/js/*.js`) | New tiny `admin-nav.ts` module for sidebar toggle + localStorage persistence. |

### Complexity: **SMALL-MEDIUM** (2-4 plans)

Phase suggestion:
1. Sidebar collapse + hamburger + localStorage persistence (TS + CSS)
2. Admin tables responsive (overflow-x wrapper OR card-stack — decide per-table during plan)
3. Forms single-column + touch-target audit + form widths
4. Manual UAT at 375px on all admin pages (Feedback list/detail, Harvest controls, Analytics, Sources list/detail, Spend dashboard)

### Notes

- **Hard sequencing dependency on Feature 6 (modal):** Both v1.4 admin pages and the modal will touch `admin.css`. Sequencing matters because the modal's `<dialog>` styling is mobile-relevant (full-screen-on-mobile vs centered-on-desktop). **Recommend: Feature 6 modal lands FIRST, mobile-sweep audits against its final markup.** Otherwise mobile-sweep ships, then modal lands and breaks the mobile audit invariant.
- **No CSS framework adoption:** DeckFlow uses hand-rolled CSS (25 guild themes + admin.css). Do NOT introduce Bootstrap / Tailwind for the admin sweep — alien to the codebase and adds 50KB+. Hand-roll the media queries.

---

## Feature 5: Gemini Paste-Limit Workaround

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Gemini selector option enabled in production (flag flips on) | This is the ship gate. Currently flag-gated since 2026-05-13 (v1.2 close). | Flip `DECKFLOW_GEMINI_ENABLED=true` on Render after workaround lands. |
| Single-paste workflow OR split-message workflow is functional end-to-end | User must successfully get a Gemini analysis from a normal-sized deck packet (~99 cards + commander). | Manual UAT against gemini.google.com web UI. |
| Approach documented in /help | When user pastes and it doesn't work, they look for help. | Update existing help markdown topics. |
| Behavior is deterministic per AI selection — Gemini selection always uses the workaround; ChatGPT/Claude unchanged | Don't regress the other two AIs. v1.3 AiPlatform value object (sealed record) gives the dispatch primitive. | Per-AI branch in prompt builder (already established v1.2 Phase 10 pattern). |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Split-message UX: numbered output ("Part 1 of 3") + clear acknowledgement copy | Industry workaround pattern: tell Gemini "I'll send N parts, acknowledge only until done." Documented technique. | Generate N artifacts in download zip, each with header `// PART 1/3 — paste this first, wait for Gemini to acknowledge, then paste PART 2/3`. |
| Adaptive sizing: artifact stays single-paste if under threshold, splits only when needed | Avoids forcing users into multi-paste for small decks. | Compute byte/char size; threshold ≈ 28,000 chars (safety margin under Gemini ~30K web cap). |
| Direct Gemini API integration (alternative path) | Eliminates paste step entirely. Requires user to supply API key OR DeckFlow to provide hosted Gemini access. | Out of scope for v1.4 single-operator hobby project unless DeckFlow eats the cost. Free-tier Gemini API has 1500 RPD limit which might cover single-operator demand. |
| Per-AI artifact preview showing user "this will be N pastes" before they download | Sets expectation up-front. | Render preview count on the packet page. |
| Auto-copy first part to clipboard on download | Saves a step in the multi-paste flow. | TS module + existing copy-announcer pattern from v1.0. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Hosted Gemini account / API-key proxy for users** | DeckFlow does not handle user auth; storing API keys = secrets-management surface DeckFlow doesn't have. | Never (architectural — DeckFlow is anonymous, no user accounts) |
| **Browser-extension auto-paste into Gemini web UI** | Cross-site script injection territory; brittle (Gemini DOM changes); v1.4 has no extension scope. | Browser-extension milestone if ever |
| **Gemini Pro premium paid integration** | DeckFlow doesn't bill. | Never |
| **Auto-switching between split-message and direct-API based on content size** | Two-mode complexity for marginal benefit. Pick one approach for v1.4. | v1.5+ if both modes ship |
| **Server-side Gemini API call returning analysis directly (no paste at all)** | Violates Core Value: "every workflow produces output the user can paste into ChatGPT/Claude/Gemini" — DeckFlow is a prompt-artifact tool, NOT an analysis service. Changing that is a strategic pivot. | Never under current Core Value statement |
| **Token-level optimization on prompt text** (e.g. compress instructions) | Marginal byte savings; readability cost. Threshold-based splitting wins. | If split-message proves insufficient |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| `AiPlatform` sealed record (v1.3 Phase 15 AIPLATFORM-01..03) | Per-AI dispatch primitive in prompt builders. Gemini branch is already a first-class enum entry; just needs to switch from "skip" to "split-message build". |
| Prompt builders (all 5: analysis, set-upgrade, comparison, follow-up, meta-gap) | Each needs Gemini branch updated. Mirrors v1.2 Phase 10 multi-AI dispatch pattern. |
| Packet download session cache (v1.3 Phase 999.3) | Multi-part artifact build uses same cache path; preview→download invariant preserved. |
| Zip artifact filename convention (v1.3 RENAME-03) | Multi-part filenames preserve AI-segment invariant: e.g. `deck-analysis-gemini-part1of3.txt`. |
| `IN-01 _AiSelector vs view-level Normalize Gemini-flag fallback divergence` (v1.3 audit tech-debt) | **MUST resolve as part of this work** — divergence currently silently disagrees on whether Gemini is shown. v1.4 unblock surfaces this. |
| Help center (Markdig pipeline) | Update help topics for Gemini multi-paste workflow. |
| `_AiSelector` partial | Remove conditional that hides Gemini under flag (after flag flipped). |

### Complexity: **SMALL-MEDIUM** (2-4 plans)

Phase suggestion:
1. Decide approach (split-message vs direct-API) — design / spike plan
2. Implement split-message build in all 5 prompt builders (per-AI dispatch fan-out, mirroring v1.2 Phase 10 pattern)
3. Resolve IN-01 fallback divergence (flag-disabled UI consistency)
4. Manual UAT against gemini.google.com (paste 3-part flow, verify analysis returned correctly) + help center update + flip flag in Render

### Notes

- **Strong recommendation: split-message over direct-API** for v1.4 — preserves DeckFlow's paste-artifact Core Value, avoids API-key secrets management, ships fast.
- **Gemini web UI character cap:** ~30,000 chars per message based on community reports (no Google-published number). Threshold at 28K conservatively.
- **Gemini API option viable as fallback** if split-message fails UAT: Gemini API free tier post-April-2026 = 1500 RPD on Flash models, sufficient for single-operator project. API key would go in Render env var with `sync: false`. But splits the prompt-artifact paradigm. Defer.
- **Gemini-specific instructions copy:** Need to add "Paste the parts in order, wait for Gemini to acknowledge each" line in Gemini-targeted artifact. Mirrors v1.2 Claude-vs-ChatGPT copy divergence pattern.

---

## Feature 6: WDG-04 Modal (Replace `onsubmit` Confirm with Styled Focus-Trapped Modal)

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Native `<dialog>` element used (NOT custom div + JS focus library) | WHATWG `<dialog>` with `showModal()` handles focus trap, Escape, ARIA role natively. Industry recommendation as of 2026. Zero added JS dependencies. | Single Razor partial + minimal TS. |
| Focus moves to first interactive element on open | WCAG 2.4.3 (Focus Order). | Native `<dialog>` does this automatically with `autofocus` attribute. |
| Escape key closes modal AND returns focus to trigger element | WCAG 2.1.2 (No Keyboard Trap). | Native `<dialog>` handles Escape; trigger-return is one line of TS. |
| Focus is trapped: Tab/Shift+Tab cycles only inside modal | WCAG 2.1.2. | Native `<dialog>` traps focus when opened via `showModal()`. |
| Click-outside-to-close (with confirm-pattern destructive-action exception) | Standard non-destructive modal pattern. Destructive actions (Delete confirm) should require explicit Cancel click. | Conditional via attribute / data-flag. |
| Replaces `onsubmit` in AdminFeedback/Detail.cshtml:41 | This is the v1.3 deferred override. The literal item to close. | Per `v1.3-MILESTONE-AUDIT.md` tech_debt list. |
| Styled to match site-common.css token system (background overlay, border-radius, focus rings) | Visual consistency with rest of admin. | Use existing CSS variables from `:root` token block. |
| Works in all evergreen browsers (Chromium, Firefox, Safari) | `<dialog>` is now supported across all evergreen browsers (Safari 15.4+, Chrome 37+, Firefox 98+). | No polyfill needed at current browser baseline. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Reusable Razor partial `_ConfirmDialog.cshtml` | One pattern, multiple usage sites (delete-source from Feature 2, delete-feedback, future destructive actions). DRY. | View-component or partial with named slots. |
| Configurable confirm-button label per call site | "Delete Source" vs "Delete Feedback" — site-specific copy. | Parameterize. |
| Server-side fallback if `<dialog>` unsupported (graceful degradation to non-modal confirm page) | Defense in depth for legacy admin browsers. | Probably overkill — admin baseline is evergreen Chrome/Edge/Firefox. Skip in v1.4. |
| Animation on open/close (subtle fade) | Polish. Native `<dialog>` supports `::backdrop` + `transition`. | Optional; ~5 lines of CSS. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Toast/snackbar notification system** | Modals only — toasts are a separate primitive. Out of scope. | Own UX milestone |
| **Full modal library / framework adoption** | Native `<dialog>` is sufficient. | Never |
| **Stacked modals** | YAGNI in v1.4 admin context. | If/when needed |
| **Modal on public-facing pages** | Scope is admin (`AdminFeedback/Detail.cshtml`). Public pages don't have the WDG-04 deferred override. | Future if pattern emerges |
| **Custom focus-trap JS library** (e.g. focus-trap-react port) | Native `<dialog>` makes this obsolete. | Never |
| **Confirm-with-text-typed-input** ("type DELETE to confirm") | Overkill for single-operator admin. | If destructive scope grows |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| `site-common.css` a11y primitives (v1.3 WDG-04) | Reuse `:focus-visible` ring + tokens. Per CLAUDE.md, layout CSS in site-common.css. |
| `admin.css` | Touched: add modal styling. **MUST sequence before Feature 4 mobile sweep** (mobile-sweep audits against admin.css). |
| TypeScript build (`wwwroot/ts/*.ts`) | New tiny `confirm-dialog.ts` (~20 lines: open/close, trigger-element-return). |
| Existing `AdminFeedback/Detail.cshtml:41` `onsubmit` block | Replaced by trigger button → `dialog.showModal()`. |
| Razor partial / view-component convention | New `_ConfirmDialog.cshtml` partial. |

### Complexity: **SMALL** (1-2 plans)

Phase suggestion:
1. Reusable `_ConfirmDialog.cshtml` partial + `confirm-dialog.ts` + admin.css styling
2. Wire AdminFeedback/Detail.cshtml to use partial (close WDG-04 deferred override)
3. (Optional) Document pattern for Feature 2 Delete-Source action to reuse

### Notes

- **Sequencing hard-dependency:** Ship Feature 6 BEFORE Feature 4 (mobile sweep) AND BEFORE Feature 2 (CRUD UI). Otherwise the admin.css surface changes twice and mobile-sweep audits against an interim state.
- **`<dialog>` element status:** Per current WCAG 2.2 guidance + 2026 browser-support reality, the native `<dialog>` element is the preferred primitive over div-based custom modals. DeckFlow's evergreen-browser baseline supports it everywhere; no polyfill needed.

---

## Feature 7: Doc-Comment NoWarn Backlog (Backfill ~88 v1.1-era Web Types)

### Table Stakes

| Capability | Why Expected | Notes |
|---|---|---|
| Every public Web type gets XML `<summary>` doc-comment | Convention established v1.3 Phase 13 + 14 audit. The remaining ~88 are the v1.1 backlog. | Mirrors CLAUDE.md "XML doc comments on every public type" convention. |
| `NoWarn 1591;1573;1587` removed from `DeckFlow.Web.csproj` after coverage achieved | The literal csproj line is the ship gate. Build must stay clean (0 warnings) after removal. | Per CLAUDE.md: "compiler warnings + nullable diagnostics are the gate." |
| Build clean (0 Warnings, 0 Errors) on Release | Mirrors v1.3 close-gate: `0 Warning(s), 0 Error(s)` at HEAD. | Verify via `dotnet build -c Release`. |
| Test suite still 0-fail after backfill | Doc-comment edits cannot regress runtime — but verify the gate anyway. | `Failed: 0, Passed: 497, Skipped: 3, Total: 500` preserved. |
| `<param>` + `<returns>` on non-trivial methods (multi-arg + non-void) | Mirrors v1.3 backfill convention. | Auto-generated stubs from Visual Studio / ReSharper are fine starting point but MUST be reviewed for accuracy. |
| Controller action methods documented with what they return + HTTP semantics | MVC convention: doc-comment includes return-type semantics (View / JsonResult / Redirect). | Single-sentence summary minimum. |
| View models documented with what view consumes them | View models are RAZOR-page-coupled; the doc-comment establishes that coupling for readers. | `<summary>View model bound to /Admin/Feedback list page.</summary>` |
| Razor `.cshtml`-generated CS1591 noise NOT reintroduced | Known gotcha: enabling xmlDoc emits CS1591 against `.cshtml`-generated partial classes. Pre-existing `NoWarn 1573 + 1587` covers some — must verify after backfill. | Per Microsoft docs CS1591 + GitHub issue: Razor-generated classes legitimately need 1591 suppression OR explicit `<inheritdoc/>` stub on partials. Recommend keeping 1591 suppression scoped to generated Razor only (via filename-pattern NoWarn) and removing it elsewhere. |

### Differentiators

| Capability | Value | Notes |
|---|---|---|
| Doc-comment seeding from method-body comments where present | If `// Builds the analysis prompt` exists, lift it into `<summary>Builds the analysis prompt.</summary>`. | Manual pass; faster than writing from scratch. |
| Auto-generation pass via Roslyn analyzer + then human review | Speeds up the 88-type backfill. Industry: use IDE template generation + then audit. | One-shot tool run; humans curate. |
| `<remarks>` blocks pointing to phase/REQ markers where relevant | Mirrors v1.3 Phase 13 + 14 pattern (`D-01`, `WDG-06` markers in inline comments). | Optional polish. |
| `<inheritdoc/>` on interface implementations | Standard .NET pattern: `<inheritdoc cref="IFoo.Bar"/>` instead of duplicating prose. | Saves time + keeps single source of truth on interfaces. |

### Anti-Features (DO NOT BUILD IN v1.4)

| Anti-Feature | Why | Defer To |
|---|---|---|
| **Auto-generate doc-comments via LLM without review** | High risk of inaccurate / hallucinated descriptions. Doc-comments are contract documentation. | Never (always human-review LLM-suggested doc-comments) |
| **DocFX site generation / API reference site** | Out of scope. v1.4 ships doc-comments in source; DocFX is a separate doc-site milestone. | Own milestone |
| **External Markdown doc-site generation** | Same boundary. | Own milestone |
| **Re-format / re-indent existing files** | EXPLICITLY forbidden per CLAUDE.md: "Touch only the lines that need touching." Doc-comment additions are line-additions only. | Never — CLAUDE.md constraint |
| **Backfill DeckFlow.Core / DeckFlow.CLI doc-comments** | v1.3 already swept Core + CLI in Phase 14 AUDIT. v1.4 scope is the v1.1-era Web backlog only. | Only if Core/CLI surface drift surfaces |
| **Enforce doc-comments via Roslyn analyzer in CI** | Build-warning gate is sufficient. Analyzer infra is its own milestone. | Future hardening milestone |

### Dependencies on Existing v1.1/v1.3 Infrastructure

| Existing System | How v1.4 Reuses It |
|---|---|
| `DeckFlow.Web.csproj` `GenerateDocumentationFile=true` (existing) | Already enabled. Edit removes `NoWarn 1591;1573;1587`. |
| v1.3 Phase 13 / 14 doc-comment style convention (established CLASSRENAME-01..03 + AUDIT-01..03) | Same prose pattern: `<summary>One-sentence-imperative. Optional context paragraph.</summary>`. |
| Test suite | Doc-comments cannot break tests but suite confirms invariance. |

### Complexity: **MEDIUM** (2-4 plans, ~88 files but mechanical)

Phase suggestion:
1. Inventory: `grep` for public types in `DeckFlow.Web` without `<summary>`; confirm count (~88) and group by directory (Controllers, Services, Models, ViewModels, Infrastructure, Security).
2. Backfill batch 1: Controllers + ViewModels (~30 types) — highest-traffic surface; commit per controller.
3. Backfill batch 2: Services + Models + Infrastructure + Security (~58 types) — commit per service-area.
4. Strip `NoWarn 1591;1573;1587` from csproj → confirm 0-warning Release build → handle any residual Razor-generated CS1591 (likely needs scoped re-suppression).

### Notes

- **Razor `.cshtml` CS1591 caveat:** Removing `1591` from `NoWarn` may resurface warnings against `.cshtml`-generated partial classes. Two known mitigations:
  1. Filename-scoped `NoWarn` via `<Compile Remove>` / `<NoWarn Condition>` for `**/*.g.cs`
  2. Keep `1591` in `NoWarn` permanently scoped only to generated Razor; remove `1573` + `1587` outright; let `1591` continue covering generated classes
- **Recommendation:** Approach 2 (keep `1591` scoped to generated, remove `1573` + `1587`) — simpler, mirrors pragmatic .NET community pattern.
- **Mechanical nature:** Most of these doc-comments are template fills. Recommend using `mcp__codex__codex` per CLAUDE.md "Codex codes, Claude reviews" pattern with explicit instruction to NOT reformat existing files.
- **Hard-coded count ~88:** From v1.3 audit tech-debt entry. Confirm via grep before execution; could be slightly higher/lower.

---

## Cross-Cutting Patterns

### Schema Pattern (Feature 1 + 2 + 3)
New tables added via `EnsureSchemaAsync` migration seam (pre-existing pattern). MUST work on both `SqliteRelationalDialect` (local dev) AND `PostgresRelationalDialect` (Render prod). Schema candidates:
- `sources` — id, kind (youtube/podcast), display_name, external_id (channel ID / RSS URL), default_tags JSON, per_source_monthly_cap_usd nullable, is_archived, created_at, updated_at
- `content_harvest_runs` — id, source_id, started_at, completed_at, status, videos_processed, total_spend_usd, error_message
- `videos` — id, source_id, external_id (YT video ID / podcast guid), title, published_at, duration_seconds, harvested_at
- `transcripts` — id, video_id, source (youtube_captions / whisper / podcast_2_0), language, full_text, created_at
- `summaries` — id, video_id, llm_model, prompt_version, summary_text, created_at
- `clips` — id, video_id, start_seconds, end_seconds, excerpt_text, is_kept, created_at
- `content_tags` — id, clip_id, tag_kind (archetype/format/bracket/card_category), tag_value, confidence, created_at
- `content_spend_ledger` — id, harvest_run_id, asset_id (video_id), kind (whisper/llm), provider, units, unit_cost_usd, total_usd, created_at

### Tag Taxonomy (Feature 1 LLM extraction)
Per MTG community + Wizards-official sources, recommended initial taxonomy:
- **Archetype** (controlled vocabulary, ~15 values): `voltron`, `aristocrats`, `stax`, `combo`, `control`, `tokens`, `spellslinger`, `reanimator`, `blink`, `lands-matter`, `tribal`, `big-mana`, `artifacts`, `group-hug`, `politics`
- **Format/Bracket** (controlled vocabulary, 6 values matching Wizards official Feb 2025 system): `bracket-1-exhibition`, `bracket-2-core`, `bracket-3-upgraded`, `bracket-4-optimized`, `bracket-5-cedh`, `casual-edh` (legacy "not bracketed" content)
- **Card category** (controlled vocabulary tied to Scryfall card-type taxonomy): `ramp`, `removal`, `card-draw`, `tutors`, `interaction`, `wincon`, `mana-base`, `protection`, `recursion`, `counterspells`, `boardwipe`, `combo-piece`
- **Free-text** (for content that doesn't fit controlled vocab — e.g. specific card names mentioned, commander names): persisted but not indexed for v1.4

Controlled vocabulary in code (not DB) — `static class ContentTagVocabulary` exposes valid values; LLM prompt enumerates choices. Mirrors v1.3 AiPlatform sealed-record pattern (DI-1 dependency-inversion).

### Reused HTTP/Resilience Pattern
All 4 new external HTTP egress points (YouTube scraping, podcast RSS, Whisper API, LLM API):
1. Named `IHttpClientFactory` clients in Program.cs (e.g. `youtube-captions`, `podcast-rss`, `openai-whisper`, `openai-llm`, `gemini-api` if Feature 5 picks API path)
2. Polly `ResiliencePipelineProvider<string>` named pipelines (timeout + retry + circuit-breaker per endpoint)
3. RestSharp `RestClient` wrapping the named HttpClient
4. Per-service `internal` test-seam constructor accepting `Func<RestRequest, CancellationToken, Task<RestResponse<T>>>`
5. NO direct `new HttpClient()` — per ARCHITECTURE.md anti-pattern list
6. NO `Microsoft.Extensions.Http.Resilience` standard handler — per CLAUDE.md HTTP-resilience constraint

### Reused Admin UI Pattern
- New views slot into `_AdminLayout.cshtml` sidebar
- New CSS rules go in `admin.css` (not `site.css`)
- Layout primitives in `site-common.css` (per CLAUDE.md)
- Live-region announcer reused for status messages
- `df-select` ARIA combobox for dropdowns (per v1.3 WDG-02)
- Forms use existing `.form-group` / button styles

### Reused Cost/Logging Pattern
- Every spend write → Serilog structured log line for external triage
- Every admin action → log line with `{User}` (BasicAuth user) + `{Action}` + `{Target}`
- Cap-reached event → Serilog `Warning` level + admin badge + live-region

---

## Sequencing Recommendations (for gsd-roadmapper)

**Strong dependencies (sequence is mandatory):**

```
Feature 6 (Modal)  ──────────────►  Feature 2 (CRUD UI, Delete-Confirm)
       │                                  ▲
       │                                  │
       └────────────►  Feature 4 (Mobile Sweep, admin.css audit)
                              ▲
                              │
Feature 7 (Doc-Comment)  ─────┘  (no hard dep; can parallel)

Feature 1 (Ingestion Schema)  ────►  Feature 2 (CRUD UI needs tables)
                              ───►  Feature 3 (Spend Cap needs ledger table)
                              ───►  Feature 1 (Adapters + Orchestrator)

Feature 5 (Gemini Unblock)  ──── independent ────►  (can ship parallel)
```

**Suggested phase ordering for the roadmap:**

1. **Phase A: WDG-04 Modal Primitive** (Feature 6 — SMALL) — unblocks Phases B + E admin.css edits
2. **Phase B: Doc-Comment NoWarn Backlog** (Feature 7 — MEDIUM, mechanical) — independent of everything else; can run in parallel with A; ideal for codex-codes-claude-reviews delegation per CLAUDE.md
3. **Phase C: Content KB Schema + Migrations** (Feature 1 Part 1 — SMALL) — unblocks Feature 2 + 3
4. **Phase D: Spend Ledger + Cost Cap Enforcement** (Feature 3 — MEDIUM) — needs Phase C schema; sequence before adapters so the cap exists when adapters first run
5. **Phase E: Admin Source CRUD + Spend Dashboard** (Feature 2 — MEDIUM) — needs Phase A modal + Phase C schema + Phase D ledger
6. **Phase F: Content KB Ingestion Adapters + Orchestrator** (Feature 1 Part 2 — LARGE) — needs Phase D cap-enforcement online; can be split into 2-3 sub-phases per adapter
7. **Phase G: Admin Mobile-Responsive Sweep** (Feature 4 — SMALL-MEDIUM) — needs Phase A modal + Phase E CRUD UI to be in final form before audit
8. **Phase H: Gemini Paste-Limit Unblock** (Feature 5 — SMALL-MEDIUM) — fully independent; can interleave anywhere

**Recommended bundles:** Phases A + B can be one numbered phase ("v1.4 admin foundation + doc backlog"). Phases C + D + E can be one numbered phase ("content KB control plane"). Phase F as a dedicated phase ("content KB ingestion pipeline"). Phases G + H as a closing phase ("UI polish + Gemini unblock").

**Critical path:** A → C → D → E → F. This is the longest chain (~5 phases). Phases B, G, H are off-critical-path and can absorb schedule risk.

**Total scope estimate:** 5-8 numbered phases; ~25-35 plans total. Approximately matches v1.1 (3-phase, 21-plan) plus v1.2 (2-phase, 8-plan) combined.

---

## Sources

### MTG Domain
- [Wizards of the Coast — Introducing Commander Brackets Beta](https://magic.wizards.com/en/news/announcements/introducing-commander-brackets-beta) — HIGH confidence, official 5-bracket taxonomy
- [EDHREC Guide to Commander Brackets](https://edhrec.com/guides/edhrec-guide-to-commander-brackets) — HIGH confidence community ratification
- [Spellweave EDH Archetypes Guide](https://spellweave.app/guides/edh-archetypes) — MEDIUM confidence archetype taxonomy
- [Draftsim — 27 EDH Archetypes](https://draftsim.com/edh-archetypes/) — MEDIUM confidence
- [TheGamer — Commander Bracket System Explained](https://www.thegamer.com/magic-the-gathering-mtg-commander-brackets-explained/) — MEDIUM confidence

### YouTube Caption Extraction
- [YoutubeExplode on GitHub](https://github.com/Tyrrrz/YoutubeExplode) — HIGH confidence (active library, exposes `ClosedCaptions.DownloadAsync` to SRT, no API key required, MIT licensed)
- [Google YouTube Data API v3 Captions Reference](https://developers.google.com/youtube/v3/docs/captions) — HIGH confidence: official API requires OAuth + only works for video-owner; not viable for third-party harvest of others' channels
- [Truelogic — YouTube Captions Download Function](https://truelogic.org/wordpress/2017/07/04/13-youtube-data-api-captions-download-function/) — MEDIUM confidence on third-party limitation

### Whisper / OpenAI Pricing
- [OpenAI API Pricing](https://openai.com/api/pricing/) — HIGH confidence vendor official
- [TokenMix — Whisper API Pricing 2026](https://tokenmix.ai/blog/whisper-api-pricing) — MEDIUM confidence ($0.006/min flat for Whisper-1, $0.003/min for GPT-4o-mini-transcribe)
- [DIYAI — OpenAI Whisper Pricing 2026](https://diyai.io/ai-tools/speech-to-text/openai-whisper-api-pricing-2026/) — MEDIUM confidence cross-reference
- [CostGoat — OpenAI Transcription Pricing](https://costgoat.com/pricing/openai-transcription) — MEDIUM confidence ("real-world ~$0.010/min" overhead note)

### Spend Dashboard Patterns
- [Kinde — Integrating Usage Caps in Billing UX](https://www.kinde.com/learn/billing/pricing/integrating-usage-caps-alerts-and-spend-limits-in-billing-ux/) — MEDIUM confidence design-pattern overview
- [Cloudflare — Billable Usage Dashboard + Budget Alerts](https://developers.cloudflare.com/changelog/post/2026-04-13-billable-usage-dashboard-and-budget-alerts/) — HIGH confidence real-world reference
- [Microsoft Azure — Cost Alerts](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/cost-mgt-alerts-monitor-usage-spending) — HIGH confidence (cloud-vendor pattern)
- [Google Cloud — Budgets and Alerts](https://cloud.google.com/billing/docs/how-to/budgets) — HIGH confidence

### Gemini Paste Limits
- [Text-Splitter — Gemini Prompt Splitter Guide](https://www.text-splitter.com/blog/gemini-prompt-splitter-guide) — MEDIUM confidence (community workaround pattern)
- [Gemini API — Understand and Count Tokens](https://ai.google.dev/gemini-api/docs/tokens) — HIGH confidence official
- [Gemini API — Pricing](https://ai.google.dev/gemini-api/docs/pricing) — HIGH confidence vendor official
- [MetaCTO — Gemini API Pricing 2026](https://www.metacto.com/blogs/the-true-cost-of-google-gemini-a-guide-to-api-pricing-and-integration) — MEDIUM confidence pricing cross-ref
- [YingTu — Gemini API Free Tier Guide 2026](https://yingtu.ai/en/blog/gemini-api-free-tier) — MEDIUM confidence (1500 RPD Flash post-April-2026)

### Admin Mobile Patterns
- [Syncfusion ASP.NET Core Sidebar Control](https://www.syncfusion.com/aspnet-core-ui-controls/sidebar) — MEDIUM confidence (commercial component, but documents standard patterns)
- [DEV.to — Collapse/Expand Sidebar Menu Tutorial](https://dev.to/thedevdrawer/collapse-expand-sidebar-menu-using-javascript-html-css-3i5i) — MEDIUM confidence community pattern reference
- [ASP.NET Core Navigation Menus Tutorial](https://dotnettutorials.net/lesson/navigation-menus-asp-net-core/) — MEDIUM confidence

### Modal / Accessibility
- [UXPin — How to Build Accessible Modals with Focus Traps (2026)](https://www.uxpin.com/studio/blog/how-to-build-accessible-modals-with-focus-traps/) — MEDIUM confidence
- [TheWCAG — Accessible Modals & Dialogs WCAG 2.2 Examples](https://www.thewcag.com/examples/modals-dialogs) — MEDIUM confidence WCAG-grounded
- [W3C — Understanding WCAG 2.1.2 (No Keyboard Trap)](https://www.w3.org/TR/UNDERSTANDING-WCAG20/keyboard-operation-trapping.html) — HIGH confidence W3C official
- [TestParty — Accessible Modal Dialogs: Focus Trapping](https://testparty.ai/blog/modal-dialog-accessibility) — MEDIUM confidence

### Doc-Comments / CS1591
- [Microsoft Learn — XML Documentation Comments (C# Reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/) — HIGH confidence official
- [Microsoft Learn — Compiler Warning CS1591](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1591) — HIGH confidence official
- [GitHub aspnet/Mvc Issue #4653 — CS1591 with Razor xmlDoc](https://github.com/aspnet/Mvc/issues/4653) — HIGH confidence (documents the Razor-generated-class noise)
- [Microsoft Learn — Generate XML Documentation from Source](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/xml-documentation) — HIGH confidence

### LLM Structured Output / Chunking
- [Towards Data Science — Automate Video Chaptering with LLMs and TF-IDF](https://towardsdatascience.com/automate-video-chaptering-with-llms-and-tf-idf-f6569fd4d32b/) — MEDIUM confidence (transcript-chunking specifics, GPT-4o-mini handles ~5000 chars/chunk)
- [Pockit Blog — LLM Structured Output in 2026](https://pockit.tools/blog/llm-structured-output-complete-guide/) — MEDIUM confidence (don't regex-parse JSON pattern)
- [Simon Willison — Structured Data Extraction with LLM Schemas](https://simonw.substack.com/p/structured-data-extraction-from-unstructured) — MEDIUM confidence

### Podcast RSS Parsing
- [arminreiter/FeedReader on GitHub](https://github.com/arminreiter/FeedReader) — MEDIUM confidence (C# RSS/Atom library, multi-language tested)
- [CodeProject — How to Parse RSS Feeds in .NET](https://www.codeproject.com/Articles/820669/How-to-Parse-RSS-Feeds-in-NET) — MEDIUM confidence (covers `System.ServiceModel.Syndication` built-in)
- [RSS.com — Podcast Transcript Download](https://help.rss.com/en/support/solutions/articles/44002543594-how-to-download-a-transcript-of-your-podcast-episode-) — MEDIUM confidence (Podcasting 2.0 `<podcast:transcript>` namespace exists)
