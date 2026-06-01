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

> **Re-architected 2026-05-26** — pivoted from server-hosted harvest to a **local-harvester + file-artifact + slim-site-index** model. Harvest/transcribe/distill runs LOCALLY (CLI command or a standalone small app — packaging decided at plan time) against **local SQLite**, never on Render. Distilled output ships to the site as **AI-prompt artifact files** (committed to repo like `prompt-templates/` or uploaded to `/data`) PLUS a **slim index table** on Render Postgres for browse/filter. Rationale: avoids Render 512MB/Postgres limits, eliminates server-side spend-cap concurrency machinery (single-user local run), keeps expensive transcript/audio data off the host. See `.planning/STATE.md` pivot note. KB IDs preserved; meanings repurposed.

- [x] **KB-01**: The local harvester maintains a source list (YouTube channels + podcast RSS) in **local SQLite** (`content_sources`); sources can be added/edited/disabled (`is_enabled` flag) via the harvester (CLI/app) — NO Render-hosted CRUD UI. Soft-disable keeps prior harvested data
- [x] **KB-02**: The harvester runs end-to-end **locally** (single CLI/app invocation over the enabled source list) and records a local run summary (`content_harvest_runs`: sources processed, videos processed, transcripts fetched, Whisper calls, spend USD, abort reason if any) — NO server-hosted `POST .../Trigger` endpoint
- [ ] **KB-03**: The harvester fetches YouTube auto-captions for non-owned videos via YoutubeExplode 6.6.0 (NOT `Google.Apis.YouTube.v3.captions.download` — returns 403 on third-party); proven against 5 real cEDH/Commander channels (e.g., MTGGoldfish, Command Zone, EDHRECast, Tolarian Community College, Playing With Power) run from the local environment
- [x] **KB-04**: The harvester falls back to OpenAI Whisper API (via OpenAI 2.10.0 SDK + `HttpClientPipelineTransport` seam) for audio-only podcasts AND videos missing captions; transcripts persisted to local `content_transcripts` with `source` discriminator; per-call cost (seconds_billed + cost_usd) recorded in local `whisper_spend_ledger`
- [x] **KB-05**: A **plain local spend log** tracks cumulative Whisper cost; harvest skips a Whisper call when the projected monthly total would exceed config/env `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15.00) and marks the video `skipped_over_cap`. Single-user local run — NO TOCTOU advisory-lock, SERIALIZABLE wrapping, or kill-switch env var (server-concurrency machinery dropped with the pivot)
- [x] **KB-06**: Each harvested video is distilled into an **AI-prompt artifact file** (markdown/text) containing an LLM summary (≤200 words target) + 3-8 timestamped clip excerpts; OpenAI Structured Outputs (`strict: true`) used for parse reliability (<0.1% failure rate per PITFALLS.md P4). Artifacts land in a defined repo/`/data` location for the ChatGPT workflow (committed or uploaded)
- [x] **KB-07**: Each distilled artifact carries tags across 3 controlled-vocabulary dimensions: archetype/strategy (~15 community-standard values: voltron, aristocrats, stax, combo, control, tokens, spellslinger, reanimator, blink, …), format/bracket (Wizards Feb 2025 5-bracket system: Exhibition, Core, Upgraded, Optimized, cEDH), card_category (ramp, removal, draw, finishers, win-cons, …). Vocabulary enforced via `static class ContentTagVocabulary`; LLM-emitted tags outside the allowlist are rejected with WARN log. Tags persist locally AND on the slim site index
- [x] **KB-08**: A **slim index** on Render Postgres (source/title/url/tags → pointer to the prompt artifact) is browsable/filterable on the site so users can find relevant distilled advice (replaces the dropped admin spend dashboard). Heavy data (transcripts, audio, spend ledger) is NEVER uploaded to Render
- [ ] **KB-09**: The site-side Content KB display surface is gated behind a `content_kb_enabled` IFeatureFlagStore flag (default OFF until first UAT verifies browse + artifact rendering); any artifact-upload POST on the site is guarded by `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator`
- [x] **KB-10**: The local distill step routes its LLM extractions through a pluggable provider selected by `DECKFLOW_LLM_PROVIDER` (`openai` default | `claude`) via a factory mirroring `TranscriptProviderFactory`; OpenAI stays the default (no regression). A CLI-backed `ILlmDistillationService` shells to the `claude` CLI (`ProcessStartInfo.ArgumentList`, instruction-as-arg + transcript-on-stdin, `--allowedTools ""`) for all 3 extractions (summary/clips/tags) and parses+repairs best-effort JSON (no Structured-Outputs guarantee). The factory is extensible (codex value → clear NotSupportedException pointing to KB-12/Phase 21.3). (Added 2026-06-01 — unblocks Phase 21.1, which hit HTTP 429 insufficient_quota. Codex backend split to KB-12 per Codex round-2 plan review: unproven read-boundary for untrusted transcript input.)
- [x] **KB-11**: The CLI backend is cross-platform — env-configurable command (`DECKFLOW_LLM_CLI_COMMAND` with `{instruction}` placeholder) runs it from a WSL shell OR a Windows shell, both documented with exact commands incl. the Windows-`dotnet.exe`-from-WSL hard case; and when provider ≠ openai the `LlmSpendLedger` cap-gate + pricing math are bypassed (subscription = no per-token cost), run record written with spend=0, `DECKFLOW_LLM_MONTHLY_CAP_USD` governing only the openai backend. (Added 2026-06-01.)
- [ ] **KB-12** *(BACKLOG — low priority, demoted from Phase 21.3 on 2026-06-01)*: The `codex` distill backend is added to the KB-10 provider factory with a PROVEN tool/read-isolation boundary for untrusted transcript input (codex `exec` is an agent; `--sandbox read-only` blocks writes not reads). Ships only once an injection-style transcript is demonstrably unable to read/exfiltrate a sentinel file (verified codex no-tools mode OR stronger sandbox/container with only stdin visible). claude backend (KB-10/Phase 21.2) already covers the subscription-distill use case → codex is a nice-to-have, NOT required for v1.4. See ROADMAP `## Backlog`.

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
- **`IFeatureFlagStore` for Whisper monthly $ cap** — wrong tool for typed-decimal infra config; env/config value instead
- **Server-hosted content harvest on Render** — pivoted out 2026-05-26; harvest/transcribe/distill runs LOCALLY only. No `/Admin/ContentHarvest/Trigger` endpoint, no hosted orchestrator, no `content_*` harvest tables on Render (slim index table only)
- **TOCTOU spend cap-gate (`pg_try_advisory_lock` + SERIALIZABLE + kill-switch)** — dropped with the pivot; single-user local run uses a plain spend-log check
- **Widening v1.1 `harvest_runs.kind` CHECK constraint** — moot under the pivot (local `content_harvest_runs` lives in local SQLite, not Render); no collision with v1.1 `harvest_runs`
- **Scheduled / automated content harvest** — deferred to v1.5; v1.4 ships manual local runs only
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
| KB-01 | Local source list (`content_sources` SQLite) + harvester add/edit/disable | Phase 19 (local schema) + Phase 21 (source mgmt runtime) | [ ] |
| KB-02 | Local end-to-end harvest run + local run record | Phase 19 (`content_harvest_runs` SQLite schema) + Phase 21 (orchestration runtime) | [ ] |
| KB-03 | YouTube auto-caption fetch via YoutubeExplode (local) | Phase 20 | [ ] |
| KB-04 | Whisper fallback transcription + local transcript/ledger | Phase 19 (transcript + spend-log schema) + Phase 20 (Whisper runtime) | [ ] |
| KB-05 | Plain local spend-log cap check (no TOCTOU/kill-switch) | Phase 19 (spend-log schema) + Phase 20 (local cap-check runtime) | [ ] |
| KB-06 | LLM distill → AI-prompt artifact files (summary + clips) | Phase 19 (artifact file-format spec + distill models) + Phase 21 (distill + emit) | [ ] |
| KB-07 | Controlled-vocab tags on artifacts + slim index | Phase 19 (`ContentTagVocabulary` + tag schema) + Phase 21 (tag inference + emit) | [ ] |
| KB-08 | Slim site index on Render + browse/filter display | Phase 19 (slim-index schema contract) + Phase 22 (materialize + browse UI) | [ ] |
| KB-09 | `content_kb_enabled` display-gate flag + CSRF on upload POST | Phase 22 | [ ] |
| KB-10 | Pluggable LLM distill provider (openai\|claude via env; codex→KB-12) | Phase 21.2 | [x] |
| KB-11 | Cross-platform CLI invocation (WSL+Windows) + JSON hardening + ledger bypass | Phase 21.2 | [x] |
| KB-12 | Codex distill backend with proven untrusted-input read isolation | BACKLOG (low priority; was Phase 21.3) | [ ] |
| CAT-01 | Card category lookup fix (Sol Ring colorless staple returns empty) | Phase 24 | [ ] |
| AHD-01 | Admin harvested-decks paged grid (replaces top-10) | Phase 25 | [ ] |

**Coverage:** 20/20 active v1.4 REQ-IDs mapped (100%) + KB-12 in BACKLOG (low priority, not required for v1.4). KB-10/KB-11 added 2026-06-01 (Phase 21.2 pluggable LLM CLI backends — unblocks the Phase 21.1 OpenAI-billing dependency); KB-12 added 2026-06-01 then demoted to backlog (codex backend — claude already covers subscription-distill; codex's untrusted-input read boundary is unproven, so it's a nice-to-have). No orphans. KB cluster re-architected 2026-05-26 to the local-harvester + file-artifact + slim-index model (see KB section note) — IDs preserved, meanings repurposed; harvest now runs locally, only KB-08 (slim index) + KB-09 (display gate) land on Render. Multi-phase REQ-IDs (DOC-01, KB-01, KB-02, KB-04, KB-05, KB-06, KB-07, KB-08) split between the Phase 19 schema/contract foundation and the runtime/UI phase per layer-of-responsibility separation — each phase owns a distinct, verifiable portion; checkboxes flip when BOTH portions are complete. CAT-01 (bug) + AHD-01 (feature) added 2026-05-24 mid-milestone per user request.

---

## v1.5 Candidate Requirements (not yet phased)

> Captured via `/gsd-explore` 2026-05-29. **Not counted in v1.4 coverage above.** Promote to
> a phase during v1.5 planning. Design detail: `.planning/notes/deck-primer-prompt-design.md`;
> seed: `.planning/seeds/deck-primer-generator.md`.

### Deck Primer Generator (PRM)

- [ ] **PRM-01**: New paste-ready "Deck Primer" workflow (tab + `DeckPrimerPacketService`) that
  emits a ChatGPT-ready prompt producing a full Moxfield deck primer in one round-trip. Combo
  lines grounded by `CommanderSpellbookService` (ground truth) + AI speculative-extend (fenced);
  mulligan/engine/interaction sections grounded by category + tagger data.
- [ ] **PRM-02**: Primer section selection — bracket-preset defaults (cEDH / Casual-Upgraded)
  pre-check a sane set from the 31-section catalog, rendered as 5 collapsible groups (Identity /
  Combos / Gameplay / Matchups / Maintenance) with per-section on/off. Prompt emits only selected
  sections.
- [ ] **PRM-03**: Matchup section bracket-routing — bracket 5 → named meta archetypes via
  `EdhTop16Client`; brackets 1–4 → 5 generic strategy buckets (Aggro/Control/Midrange/Combo/
  Stax-Hate). cEDH-only sections (Must-Counter, Counter Cheat Sheet) and casual-only (Meta
  Positioning) gate on bracket. No EDHREC integration in v1.
