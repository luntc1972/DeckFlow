# DeckFlow v1.4 Requirements

**Milestone:** v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup
**Defined:** 2026-05-23
**Source:** `.planning/research/SUMMARY.md` (synthesized from STACK + FEATURES + ARCHITECTURE + PITFALLS); `.planning/PROJECT.md` Current Milestone section.

## v1.4 Requirements

### Admin Focus-Trapped Modal (MODAL)

- [ ] **MODAL-01**: Admin can confirm destructive feedback actions via a styled focus-trapped native `<dialog>` modal (replaces deferred inline `onsubmit` confirm in `AdminFeedback/Detail.cshtml`; closes v1.3 WDG-04 override 2026-05-16)

### Doc-Comment NoWarn Backlog (DOC)

- [ ] **DOC-01**: All public types in `DeckFlow.Web/{Controllers,Services,Models,Models/Api,Infrastructure,Security,ViewModels}/` carry XML `<summary>` doc-comments (~88 v1.1-era types backfilled across 2 phases)
- [ ] **DOC-02**: `DeckFlow.Web.csproj` no longer suppresses CS1591/CS1573/CS1587 globally (scoped 1591 retention permitted only for compiler-generated Razor partials if needed); `dotnet build -warnaserror:CS1591` succeeds from clean `obj/`

### Admin Mobile-Responsive Sweep (AMOB)

- [ ] **AMOB-01**: Admin shell renders correctly on viewports ≥320px wide; sidebar collapses to disclosure (`<details>`/`<summary>`) below the 768px breakpoint with no-JS fallback
- [ ] **AMOB-02**: Admin tables remain usable on narrow viewports — per-table choice of `overflow-x: auto` (Analytics, HarvestRuns, ContentHarvest) or card-stack pattern (Feedback list, ContentSources list)
- [ ] **AMOB-03**: Admin forms render single-column on narrow viewports; all interactive elements meet ≥44×44px touch-target floor (extends v1.3 WDG-08 site-common.css primitives to admin shell)
- [ ] **AMOB-04**: `admin.css` factored into `admin-common.css` (layout primitives, mirrors `site-common.css` role) + `admin-mobile.css` (`@media` rules) + `admin.css` import shim; CSS scoped to `.admin-shell` parent class — zero bleed into 22 guild themes

### Content Knowledge Base Foundation (KB)

- [ ] **KB-01**: Admin can create/edit/disable YouTube channel + podcast RSS sources via `/Admin/ContentSources` CRUD UI; data persists to `content_sources` Postgres table via existing `IRelationalDialect` + per-store `EnsureSchemaAsync` pattern
- [ ] **KB-02**: Admin can trigger manual content harvest via `POST /Admin/ContentHarvest/Trigger` (returns 202 with run id); harvest history visible at `GET /Admin/ContentHarvest`; per-run drill-down at `GET /Admin/ContentHarvest/{id}` (sources processed, videos processed, transcripts fetched, Whisper calls, spend, abort reason if any)
- [ ] **KB-03**: Content harvest fetches YouTube auto-captions for non-owned videos via YoutubeExplode 6.6.0 (NOT `Google.Apis.YouTube.v3.captions.download` — returns 403 on third-party); proven against 5 real cEDH/Commander channels (e.g., MTGGoldfish, Command Zone, EDHRECast, Tolarian Community College, Playing With Power) from deployed Render environment
- [ ] **KB-04**: Content harvest falls back to OpenAI Whisper API (via OpenAI 2.10.0 SDK + `HttpClientPipelineTransport` seam) for audio-only podcasts AND videos missing captions; transcripts persisted to `content_transcripts` with `source` discriminator; per-call cost (seconds_billed + cost_usd) recorded in `whisper_spend_ledger`
- [ ] **KB-05**: Whisper spend gate aborts harvest when projected monthly cost would exceed env-var `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15.00); no Whisper API call made when cap would be exceeded; TOCTOU-safe under concurrent admin triggers (Postgres `pg_try_advisory_lock` per YYYY-MM month key + SERIALIZABLE transaction wrapping the check-and-insert); video.transcript_status set to `skipped_over_cap`; `DECKFLOW_WHISPER_KILL_SWITCH=true` env var aborts harvest immediately
- [ ] **KB-06**: Each harvested video has an LLM-generated summary (≤200 words target) + 3-8 timestamped clip excerpts persisted to `content_summaries` + `content_clips`; OpenAI Structured Outputs (`strict: true`) used for parse reliability (<0.1% failure rate per PITFALLS.md P4)
- [ ] **KB-07**: Each harvested video has tags persisted to `content_tags` covering 3 controlled-vocabulary dimensions: archetype/strategy (~15 community-standard values: voltron, aristocrats, stax, combo, control, tokens, spellslinger, reanimator, blink, …), format/bracket (Wizards Feb 2025 5-bracket system: Exhibition, Core, Upgraded, Optimized, cEDH), card_category (ramp, removal, draw, finishers, win-cons, …). Vocabulary enforced via `static class ContentTagVocabulary`; LLM-emitted tags outside the allowlist are rejected with WARN log
- [ ] **KB-08**: Admin can view spend dashboard at `/Admin/ContentSpend` showing current month + last 6 months Whisper + LLM aggregate spend (per-provider breakdown); warns inline when current month consumed >80% of cap
- [ ] **KB-09**: Content KB feature is gated behind `content_kb_enabled` IFeatureFlagStore flag (default OFF until first admin UAT verifies end-to-end harvest); all `/Admin/Content*` POSTs guarded by `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator`

### Card Category Lookup Bug Fix (CAT)

- [ ] **CAT-01**: Card category suggestion returns correct, non-empty categories for staple cards that must always resolve. Regression repro: **Sol Ring** (colorless artifact ramp staple) currently returns no categories. Root cause unknown and MUST be investigated in BOTH states — while the Archidekt harvest/cache job is running AND while it is stopped (the running service is a suspected cause). Fix restores category results for Sol Ring and similar colorless/staple cards without regressing existing category coverage. (Added 2026-05-24 — captured for investigation-later.)

### Admin Harvested-Decks Grid (AHD)

- [x] **AHD-01**: The admin harvested-decks view replaces the current top-ten-decks list with a paged grid showing ALL harvested decks — server-side paging (page size + total count), scannable rows. Reuses the Phase 18 responsive admin shell + existing admin table/card patterns; must not load all rows into memory (Render 512MB cap). (Added 2026-05-24.)

## Future Requirements (Deferred to v1.5+)

**Content Knowledge Base — Integration into deck analysis (DEFERRED to v1.5):**
- Prompt-injection: relevant clip excerpts + source citations inserted into AI prompt artifact for user's deck analysis
- DeckFlow UI "What experts say" panel surfacing ranked clips/quotes for user's commander/archetype on deck-analysis page
- New-deck-building interactive guide (wizard) leveraging Content KB tags

**Content Knowledge Base — Operational enhancements (DEFERRED to v1.5+):**
- Scheduled (cron) harvest cadence (daily/weekly background harvest)
- Multi-tenancy / public-user-submitted sources
- Cost-per-tag insights, predicted-spend forecasting

**Other v1.3 carry-over items (deferred to v1.5+):**
- Gemini paste-limit workaround (flag-gated DECKFLOW_GEMINI_ENABLED; needs split-message strategy or direct API integration) — moved out of v1.4 scope 2026-05-23
- IN-01 `_AiSelector` vs view-level Normalize Gemini-flag fallback divergence
- v1.1 phase-dir archive move (06, 07, 07.1, 08 → `.planning/milestones/v1.1-phases/`)
- CSS-class / data-attribute / TS-constant `chatgpt-*` cleanup
- v13-harvest-worker-stalled debug follow-up
- edhtop16 filter-defaults mismatch (Plagon, Lord of the Beach 0-entries case)
- audit-open scanner vocabulary alignment

## Out of Scope (v1.4 explicit exclusions)

- **YouTube Data API v3 with OAuth** — captions endpoint requires content owner; non-viable for third-party MTG content (PITFALLS.md P1)
- **Self-hosted local Whisper model** — Render Starter 512MB RAM cap forbids in-memory ML inference (STACK.md hard reject)
- **`Google.GenAI` 1.7.0 / `Google_GenerativeAI` 3.6.6 SDKs** — transitive `Microsoft.Extensions.AI` / Newtonsoft baggage conflicts with single-RestSharp + named-Polly-pipeline convention (STACK.md reject list)
- **Bootstrap / Tailwind / Fluent UI for admin mobile** — fights the 25-guild-theme system (FEATURES.md reject)
- **Multi-AI registry for admin-side LLM summarization** — AiPlatform serves user-facing multi-AI paste dispatch; admin ingestion uses single dedicated provider (OpenAI 2.10.0). AiPlatform variant added ONLY if Gemini Path B chosen
- **`IFeatureFlagStore` for Whisper monthly $ cap** — wrong tool for typed-decimal infra config; env var instead
- **Widening v1.1 `harvest_runs.kind` CHECK constraint** — fork to parallel `ContentHarvestRunStore` on new `content_harvest_runs` table (PITFALLS.md P12)
- **Scheduled background content harvest** — deferred to v1.5; v1.4 ships manual admin-triggered only
- **Deck-analysis integration of Content KB** — deferred to v1.5 per scope decision 2026-05-23
- **New-deck-building interactive guide** — deferred to v1.5 per scope decision 2026-05-23

## Traceability

| REQ-ID | Description | Phase | Status |
|--------|-------------|-------|--------|
| MODAL-01 | Admin focus-trapped modal | Phase 16 | [ ] |
| DOC-01 | XML `<summary>` doc-comments on ~88 Web types | Phase 17 (Part 1: Controllers + Services) + Phase 23 (Part 2: remaining + v1.4 new types) | [ ] |
| DOC-02 | Strip `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj` | Phase 23 | [ ] |
| AMOB-01 | Admin shell renders ≥320px viewport (sidebar disclosure) | Phase 18 | [ ] |
| AMOB-02 | Admin tables usable on narrow viewports | Phase 18 | [ ] |
| AMOB-03 | Admin forms single-column + ≥44×44px touch targets | Phase 18 | [ ] |
| AMOB-04 | `admin.css` factored into common+mobile+shim | Phase 18 | [ ] |
| KB-01 | Admin source CRUD UI + `content_sources` table | Phase 19 (table) + Phase 22 (CRUD UI) | [ ] |
| KB-02 | Admin manual harvest trigger + run history UI | Phase 21 (orchestrator + trigger runtime) + Phase 22 (history UI) | [ ] |
| KB-03 | YouTube auto-caption fetch via YoutubeExplode | Phase 20 | [ ] |
| KB-04 | Whisper fallback transcription + spend ledger | Phase 20 | [ ] |
| KB-05 | Whisper spend cap-gate (TOCTOU-safe + kill-switch) | Phase 19 (ledger schema + WouldExceedCapAsync stub) + Phase 21 (advisory lock + kill-switch runtime) | [ ] |
| KB-06 | LLM summary + clip-excerpt extraction | Phase 20 | [ ] |
| KB-07 | Tag inference (controlled vocab: archetype + bracket + category) | Phase 20 | [ ] |
| KB-08 | Admin spend dashboard at `/Admin/ContentSpend` | Phase 22 | [ ] |
| KB-09 | `content_kb_enabled` feature flag gate + CSRF guards | Phase 21 (orchestrator-boundary flag gate) + Phase 22 (UI-surface CSRF tokens + flag check) | [ ] |
| CAT-01 | Card category lookup fix (Sol Ring colorless staple returns empty) | Phase 24 | [ ] |
| AHD-01 | Admin harvested-decks paged grid (replaces top-10) | Phase 25 | [ ] |

**Coverage:** 18/18 v1.4 REQ-IDs mapped (100%). No orphans. Multi-phase REQ-IDs (DOC-01, KB-01, KB-02, KB-05, KB-09) split between schema/foundation phase and UI/runtime phase per layer-of-responsibility separation — each phase owns a distinct, verifiable portion of the requirement; checkboxes flip when BOTH portions are complete. CAT-01 (bug) + AHD-01 (feature) added 2026-05-24 mid-milestone per user request.
