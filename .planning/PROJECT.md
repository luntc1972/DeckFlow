# DeckFlow

## What This Is

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH and Commander players, deployed live at https://www.deckflow.gg. It pulls deck data from Archidekt and Moxfield, generates ChatGPT-ready prompt artifacts for deck analysis, and provides synergy/category knowledge derived from the user's own crawled deck history. Audience: serious deck-builders who want a structured "compare, analyze, decide" workflow rather than a one-click recommender.

## Core Value

**Every supported workflow must produce output the user can paste into ChatGPT and get back a useful answer in one round-trip — without the user reformatting anything.** Visual polish, theme variety, and admin tooling all serve that core. If the prompt artifacts are wrong or missing, nothing else matters.

## Current State

**Shipped:** v1.2 Multi-AI Prompts (2026-05-13) — AI target selector (ChatGPT / Claude / Gemini) live on all three ChatGPT analysis pages with zip round-trip; Claude-optimized XML prompt structure; cEDH meta-gap Step 1 state round-trip; Gemini gated behind `DECKFLOW_GEMINI_ENABLED` env flag because the full packet exceeds gemini.google.com's paste cap.

**Active:** v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (started 2026-05-13 on `v1.3` branch).

## Current Milestone: v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene

**Goal:** Ship audit-driven a11y/quality fixes, drop "ChatGPT" branding from AI-target-agnostic surfaces (URLs + classes), bring class names in line with behavior across the codebase, refactor `AiPlatform` string to value object.

**Target features (in execution order):**

1. Web Design Guidelines audit fixes — 10 sweep PRs from `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/FINDINGS.md` (P1 a11y bugs first: admin focus-visible, df-typeahead keyboard nav, ARIA tablist server-render, CSP inline-handler removal, info-tooltip a11y, then P2 guideline violations).
2. AI-agnostic rename — URL + page layer (`/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap` → AI-agnostic URLs; H1/nav/hub labels updated; permanent redirects from old URLs; explainer lines preserve "this is for an AI" cue). Source: `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` (Option A recommended).
3. ChatGpt* class rename — code layer (`ChatGptDeckRequest`, `ChatGptDeckPacketService`, `ChatGptRequestContextParser`, `ChatGptPacketArtifactStore`, `ChatGptDeckComparisonService`, `ChatGptCedhMetaGapService`, etc.) renamed to AI-agnostic terms; XML `<summary>` doc comments added on every renamed class; DI registrations + `InternalsVisibleTo` updated.
4. Broader codebase audit — name-vs-behavior pass. Scan all classes (services, models, controllers, helpers). Flag and rename any whose name doesn't match current responsibility; add `<summary>` doc comments where missing.
5. `AiPlatform` value object refactor — replace string `TargetAiPlatform` with sealed record value object per `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` design. OCP forecast 3/10 → 8/10. Zero user-visible behavior change.

**Key context:**

- Branch: `v1.3` (created from `main` at commit `7ed0cde` on 2026-05-13).
- Phase numbering continues from v1.2 — starts at Phase 11.
- Order rationale: #1 first (independent, low-risk, visible quality wins); #2+#3 paired (URL + class rename in single conceptual unit); #4 broader audit uses #3 as template; #5 last (refactor sits on clean class names).
- Deferred (NOT in v1.3 scope): `harvest-killed-by-suggestion` debug (parked at H1 hypothesis in `.planning/debug/`), Gemini paste-limit workaround (kept flag-gated via `DECKFLOW_GEMINI_ENABLED`).

## Requirements

### Validated

<!-- Shipped and confirmed working in production at deckflow.gg. -->

- ✓ Archidekt and Moxfield deck import (multi-format URL parsing) — production
- ✓ ChatGPT analysis prompt generator with multi-card picker and saved sessions — production
- ✓ Deck-vs-deck reconcile (Moxfield ↔ Archidekt, either direction) — production
- ✓ Category knowledge crawl + observations DB (PostgreSQL on Render) — production
- ✓ Card lookup with Scryfall integration + RestSharp/Polly resilience pipeline — production
- ✓ 25 guild-themed Razor views with single shell-max-width token — production
- ✓ Public feedback form + basic-auth-protected `/Admin/feedback` — production
- ✓ ARIA-1.2 `df-select` combobox rolled across single-select form controls — production
- ✓ Mobile responsive layout via `site-mobile.css` (≤ 50 lines) — production
- ✓ Help center `/help` and `/about` with Markdig content pipeline (DisableHtml hardening) — production
- ✓ Browser extension package (`deckflow-bridge.zip`) served from `/extensions/` — production
- ✓ Pluggable SQLite/Postgres storage with auto-creating schema (`EnsureSchemaAsync`) — production
- ✓ Skip-link, ARIA labelled-by, copy announcer accessibility baseline — production
- ✓ 6-step type scale + semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) propagated to all 25 guild themes — v1.0 (UI-VS-01..04)
- ✓ Hub primary-CTA + inline-style cleanup + voice-aligned page titles + `/feedback` busy-state — v1.0 (UI-LH-01..02, UX-01..03)
- ✓ Test-only factories moved out of prod assembly + single-ctor service standardization + generated JS untracked + `ForwardedHeadersOptions` Path B-rawpeer with `CF-Connecting-IP` — v1.0 (TD-01..04)
- ✓ Scryfall Tagger restored for cEDH staples (auto-cookie revert + Cloudflare BIC headers + `AutomaticDecompression`) — v1.0 (BUG-01)
- ✓ Postgres-backed admin brute-force throttle with `CF-Connecting-IP` partition + same fix on `/feedback` rate-limiter — v1.0 (BUG-02 + TD-04 patch)
- ✓ Localhost integration test regression guard for Tagger cookie-replay path — v1.0
- ✓ TargetCommanderBracket selector visually prominent on ChatGPT Packets page — v1.2 (BRKT-01)
- ✓ AI target selector (ChatGPT / Claude / Gemini) on all three ChatGPT analysis pages — v1.2 (AISEL-01)
- ✓ Claude-optimized artifact format + instructions — v1.2 (AISEL-02)
- ✓ Gemini-optimized artifact format + instructions — v1.2 (AISEL-03, flag-gated since 2026-05-13)
- ✓ AI selection preserved in zip round-trip — v1.2 (AISEL-04)
- ✓ cEDH meta-gap Step 1 state preserved in zip round-trip (fetched entries + filters + selections, regenerate without re-fetching edhtop16) — v1.2 (AISEL-04 closeout, 10-05)

### Active

<!-- v1.3 not yet scoped — run /gsd-new-milestone to define. -->

(none — v1.3 pending definition)

### Out of Scope

<!-- Deferred to next milestones, with reasoning. -->

**Deferred from v1.1 (Admin Console):**

- UI audit re-score (`tasks/UI-REVIEW.md`, 16/24 → ≥ 20/24) — split into its own UI-audit milestone; not coupled to admin tooling
- Raw Serilog log tail / file viewer — Render dashboard already streams stdout; usage analytics gives more triage value than tail-follow
- Multi-user admin auth (session cookie, role split) — single-operator BasicAuth is sufficient for current ops volume
- Admin alerts / notifications — Render dashboard + manual stat checks cover ops; not blocking
- Cache flush button, Postgres connection test, Render restart, manual artifact cleanup — not required for v1.1; future "ops actions" tile if demand surfaces

**Carried from v1.0:**

- DeckController god-class split — too large for a polish milestone; warrants its own dedicated refactor milestone
- ChatGPT services extraction (`PromptBuilder`, `ScryfallReferenceResolver`, etc.) — too large; own refactor milestone
- DeckController test coverage uplift — own milestone (depends on the split)
- Visual regression harness for 25 themes — separate testing-infra milestone; nice-to-have, not table stakes
- Mobile polish beyond what UI-REVIEW.md flagged — current responsive layout is adequate
- ScryfallThrottle → Polly rate limiter migration — already in flight under prior plan; out of this milestone's scope
- Per-route Scryfall fairness queue — own resilience milestone
- Disk-backed Scryfall set cache — own caching milestone
- Tagger refresh `IHostedService` — own cron/jobs milestone
- DB-backed Archidekt cache job persistence — own jobs milestone
- ChatGPT artifact retention sweep — own cleanup milestone
- RestSharp abstraction (`IUpstreamHttpClient`) — own milestone
- Health/ready endpoint + correlation ID middleware — own observability milestone
- Structured API error envelope — own milestone
- Resilience pipeline behavior tests — own testing milestone
- Cloudflare upstream snapshot harness — own testing milestone
- PWA / offline-first — UX nice-to-have, not blocking
- Browser-extension test coverage gap — manifest-version protocol bumps are documented; deferred

## Context

**Technical environment**

- ASP.NET 10 (Razor Views + MVC controllers), TypeScript via `tsc` in MSBuild
- PostgreSQL 16 on Render (Basic-256mb), `dpg-d7oj8iugvqtc73fso0g0-a` instance "deckflow"
- Render web service (`srv-d7gmufkp3tds73a29m30`) on Starter plan with `/data` persistent disk for ChatGPT artifacts
- Custom domain `www.deckflow.gg`; auto-deploys from `main` on `luntc1972/DeckFlow`
- Local dev: WSL2 + .NET 10 SDK; VSTest currently broken in WSL (socket permission issue — known limitation)
- HTTP layer: RestSharp wrapping `IHttpClientFactory` HttpClient + per-call `ResiliencePipelineBuilder<RestResponse>` (NOT MS standard handler)
- Markdig pipeline hardened with `DisableHtml()` defense-in-depth XSS

**Known UI debt (basis for this milestone)**

- Recent UI audit (`tasks/UI-REVIEW.md`, 2026-04-30) scored 16/24 across 6 pillars
- Color (2/4) and Typography (2/4) are the lowest pillars and the biggest leverage points
- Real bug: `--accent-strong` is overloaded (links + brand + focus + error + CTA) — error text reads as link in red guild themes

**Recent project state**

- v1.0 Polish & Quality milestone shipped 2026-05-02 (5 phases, 17 plans, 63 commits, +20,284 / -5,194 LOC)
- Phase 4 abandoned mid-milestone after both fixes proved ineffective on prod despite passing static checks; rerouted to Phase 5 with surgical revert + corrective Postgres-backed throttle (see `04-ABANDONED.md` post-mortem)
- Two latent root causes surfaced during Phase 5 BUG-01: Cloudflare BIC blocks Render egress IPs without browser-shaped headers, and `AutomaticDecompression` must be enabled when advertising `Accept-Encoding`
- All 15 v1 requirements shipped; UI audit re-score against `tasks/UI-REVIEW.md` deferred to v1.1
- Tech stack pinned: ASP.NET 10 + Razor + RestSharp/Polly v8 + Postgres on Render

**User profile**

- Solo developer (Chris Lunt) with deep MTG/cEDH domain knowledge
- Communication style: terse, technical, demanding ("caveman mode" preferred)
- Prefers explainer text (`.mode-note`) over relabeling unclear UI controls
- Prefers bundled PRs to many small ones for related refactors
- README must be kept current with each commit

## Constraints

- **Tech stack**: ASP.NET 10 + Razor — pinned by deployed app; no framework migration in this milestone
- **Hosting**: Render Starter web + Basic-256mb Postgres — 512MB RAM cap on web tier, mind allocations
- **Theme system**: Guild themes are full standalone CSS forks; layout CSS must go in `site-common.css`, not `site.css` — token additions go in `:root` of each theme file
- **HTTP resilience**: Use existing RestSharp + direct Polly v8 pattern — do NOT migrate to standard handler
- **Public repo**: `luntc1972/DeckFlow` is public — no secrets in commits ever; secrets live in Render dashboard with `sync: false`
- **Testing**: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI
- **Commits**: Plain default-author commits, no Co-Authored-By trailer; README updated when behavior changes; commit per logical change

## Key Decisions

<!-- Decisions that constrain future work in this milestone and beyond. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Polish & quality before refactor | Visible improvements ship faster, sustain user momentum, and the UI audit gave a concrete punch list. Refactors (DeckController, ChatGPT services) are larger and benefit from a clean baseline. | ✓ Good (v1.0 shipped on schedule, 15/15 reqs) |
| Audit re-score ≥ 20/24 as success bar | Concrete, measurable, ties milestone completion back to the same evidence that started it. Avoids "feels nicer" subjective drift. | ⚠️ Revisit (re-score not measured at close; carry to v1.1) |
| Keep `--accent-strong` for backward compat; layer new semantic tokens on top | Mass-renaming `--accent-strong` would touch 25 theme files; aliases preserve all themes while fixing the semantic collision. | ✓ Good (token block landed on all 25 themes; Rakdos `--link` override proves the seam) |
| Postgres on Render (single managed instance) | Free SQLite + persistent disk works but is fragile across deploys; managed Postgres is durable, cheap (Basic-256mb $7/mo), and the storage layer was already pluggable. | ✓ Good (deployed 2026-04-30) |
| Render Starter web tier ($7/mo) over Free | Free tier sleeps after 15min, .NET cold start ~30s gave bad UX; Starter is always-on for ~$84/yr. | ✓ Good (no cold-start UX complaints during milestone) |
| Phase 4 abandonment + rerouting BUG-01/BUG-02 to Phase 5 | Both Phase 4 fixes passed static checks but failed live on prod (Tagger still empty for cEDH staples, throttle still ineffective). Pressing forward would have buried the root causes. | ✓ Good (Phase 5 surfaced Cloudflare BIC + AutomaticDecompression as the actual blockers) |
| `CF-Connecting-IP` as the partition key for both admin throttle AND `/feedback` rate-limiter | `X-Forwarded-For` was being fragmented by multi-proxy chain; spoof-resistance comes from Render Inbound IP Rules + Cloudflare CIDR allow-list, not from header trust. | ✓ Good (live UAT 10×401 + 1×429 with monotonic Retry-After 899→879) |
| Localhost `HttpListener` integration test for Tagger cookie-replay | Static checks let `4db8b8a` ship to prod without exercising the GraphQL POST leg. Real `SocketsHttpHandler` against a stub server catches future regressions to manual cookie replay or `UseCookies=false`. | ✓ Good (2/2 pass; closes the verification gap) |
| Per-AI dispatch primitive on prompt builders (v1.2 Phase 10-01) | Single `request.TargetAiPlatform` branch at the top of every prompt builder, fanned out across all 5 builders (analysis, set-upgrade, comparison, follow-up, meta-gap). Keeps ChatGPT path unchanged; Claude/Gemini are additive. | ✓ Good (15 variants shipped 2026-05-09; AISEL-02/03 satisfied) |
| Unified `<result>...</result>` wrapper for AI responses (v1.2 Phase 10-03) | One regex extracts JSON from any AI's response with the fenced ` ```json ` block preserved as fallback. Closes AISEL-04 in a single seam rather than 3 page-specific parsers. | ✓ Good (one extractor, 3 pages, no per-page paste logic) |
| Hybrid deck text storage in session zips (v1.2 Phase 10 hardening) | Store both original (user-pasted) and canonical (BuildDecklistText output) in every zip. Original-prefers-canonical loader precedence handles the alphabetize-vs-preserve mismatch on re-upload. | ✓ Good (62ee45b; 11 new tests) |
| Hidden form field carries cEDH Step 1 state between Step 2 submits (v1.2 Phase 10-05) | Stateless server, no session-affinity required, sidesteps edhtop16 rate-limit on regenerate. ~50-200KB per form post is acceptable. | ✓ Good (T3 retest passed 2026-05-13) |
| Gemini hidden behind `DECKFLOW_GEMINI_ENABLED` flag at v1.2 close | Full packet exceeds gemini.google.com paste cap, truncating instructions. Server logic preserved; flip env var to re-enable. | ⚠️ Revisit in v1.3 (needs split-message prompt or direct API integration) |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

## Shipped History

**Shipped:** v1.0 Polish & Quality (2026-05-02) — all 15 v1 requirements landed across 5 phases, 17 plans, 63 commits.
**Shipped:** v1.1 Admin Console (2026-05-08) — all 27 requirements landed across Phases 6–8 + Phase 7.1 insert.
**Shipped:** v1.2 Multi-AI Prompts (2026-05-13) — 5 requirements across Phases 9-10 (8 plans). AI target selector + Claude-optimized artifacts + cEDH Step 1 round-trip live. Gemini flag-gated.

**Active:** v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (started 2026-05-13 on `v1.3` branch).

---
*Last updated: 2026-05-13 — v1.3 milestone started*
