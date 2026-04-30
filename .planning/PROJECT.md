# DeckFlow

## What This Is

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH and Commander players, deployed live at https://www.deckflow.gg. It pulls deck data from Archidekt and Moxfield, generates ChatGPT-ready prompt artifacts for deck analysis, and provides synergy/category knowledge derived from the user's own crawled deck history. Audience: serious deck-builders who want a structured "compare, analyze, decide" workflow rather than a one-click recommender.

## Core Value

**Every supported workflow must produce output the user can paste into ChatGPT and get back a useful answer in one round-trip — without the user reformatting anything.** Visual polish, theme variety, and admin tooling all serve that core. If the prompt artifacts are wrong or missing, nothing else matters.

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

### Active

<!-- Polish & Quality milestone scope. -->

**Visible UI improvements (from UI-REVIEW.md, 2026-04-30 audit, score 16/24)**

- [ ] Define a 6-step type scale (`--fs-xs/sm/base/lg/xl/2xl`); replace 18 literal font-sizes in `site.css`
- [ ] Split semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) from `--accent-strong` to fix error-text-as-link bug on red guild themes (Rakdos, Boros, Jund)
- [ ] Pick a primary focal action on the home hub (single hero CTA above the grid OR per-group `.hub-card--primary`)
- [ ] Move inline `style=` attributes from `Feedback/Index.cshtml` and `AdminFeedback/{Index,Detail}.cshtml` into CSS classes
- [ ] Hoist 14+ hardcoded color literals (`#fff`, `#3a82f7`, `#c53030`, `#2f855a`, `#2b6cb0`, `#b83a2e`) in `site.css` into `:root` tokens

**Copy & UX small wins**

- [ ] `_MoxfieldBulkEditHint.cshtml` "Submit" → action-specific verb ("Run Compare" or "Look Up")
- [ ] Feedback page submit busy-state (spinner/disabled while POSTing)
- [ ] Voice consistency: page `<title>` vs `<h1>` (verb-noun vs noun-only — pick one)

**Tractable code-quality cleanup (from CONCERNS.md)**

- [ ] Move `NullHttpClientFactory` and `NullScryfallRestClientFactory` to test project + `[InternalsVisibleTo]`
- [ ] Standardize on one constructor per service + named test-helper factory
- [ ] Drop generated `*.js` from git tracking (TS-source-of-truth)
- [ ] Tighten `ForwardedHeadersOptions.KnownIPNetworks` to Render's known CIDR

**Quality bug fixes**

- [ ] Scryfall Tagger 404 bug
- [ ] Per-IP rate-limit on `/Admin/*` (defense against brute-force)

### Out of Scope

<!-- Deferred to next milestones, with reasoning. -->

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

- Storage code review remediation completed (commits `526690d` and earlier)
- Postgres URI parsing fix shipped today (commit `6bd3117`) after libpq URI vs Npgsql key=value mismatch broke first deploy
- AdminFeedback view path fix shipped today (commit `a225e89`) — views were nested at `Views/Admin/Feedback/` not the convention `Views/AdminFeedback/`
- 8 quick-win CONCERNS items already shipped before this milestone

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
| Polish & quality before refactor | Visible improvements ship faster, sustain user momentum, and the UI audit gave a concrete punch list. Refactors (DeckController, ChatGPT services) are larger and benefit from a clean baseline. | — Pending |
| Audit re-score ≥ 20/24 as success bar | Concrete, measurable, ties milestone completion back to the same evidence that started it. Avoids "feels nicer" subjective drift. | — Pending |
| Keep `--accent-strong` for backward compat; layer new semantic tokens on top | Mass-renaming `--accent-strong` would touch 25 theme files; aliases preserve all themes while fixing the semantic collision. | — Pending |
| Postgres on Render (single managed instance) | Free SQLite + persistent disk works but is fragile across deploys; managed Postgres is durable, cheap (Basic-256mb $7/mo), and the storage layer was already pluggable. | ✓ Good (deployed 2026-04-30) |
| Render Starter web tier ($7/mo) over Free | Free tier sleeps after 15min, .NET cold start ~30s gave bad UX; Starter is always-on for ~$84/yr. | — Pending (just upgraded) |

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

---
*Last updated: 2026-04-30 after initialization (Polish & Quality milestone)*
