# Roadmap: DeckFlow — Polish & Quality Milestone

## Overview

Lift DeckFlow's UI audit score from 16/24 to ≥ 20/24 while shipping tractable
tech-debt and security fixes — without breaking the live deckflow.gg
ChatGPT-paste pipeline. The heaviest lift is the visual-system token migration
(Phase 1) which touches `site.css :root` and all 25 guild theme forks; UI
hierarchy and inline-style cleanup (Phase 2) build on those tokens. UX copy,
tech-debt, and security/bug work are independent of the CSS surface and
parallelize freely.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

- [x] **Phase 1: Visual System Tokens** — Type scale, semantic color tokens, hex literal hoist, propagate across all 25 guild themes
- [ ] **Phase 2: Layout, Hierarchy & UX Copy** — Promote primary hub CTA, kill inline styles, fix copy/voice mismatches and feedback busy-state
- [ ] **Phase 3: Tech-Debt Cleanup** — Move test-only types out of prod assembly, single-ctor services, drop generated JS from git, tighten forwarded-headers CIDR
- [ ] **Phase 4: Security & Bug Fixes** — Per-IP rate-limit on /Admin/*, fix Scryfall Tagger 404

## Phase Details

### Phase 1: Visual System Tokens
**Goal**: A single semantic-token layer drives typography and color across the
classic theme and all 25 guild themes, eliminating the `--accent-strong`
overload and the 18-value font-size sprawl that drove Color (2/4) and
Typography (2/4) audit scores.
**Depends on**: Nothing (foundation phase)
**Requirements**: UI-VS-01, UI-VS-02, UI-VS-03, UI-VS-04
**Success Criteria** (what must be TRUE):
  1. `site.css :root` exposes a 6-step type scale (`--fs-xs/sm/base/lg/xl/2xl`) and every literal `font-size` in `site.css` and `site-common.css` references one of those tokens (or `em`/`%` derivative).
  2. `site.css :root` defines `--link`, `--danger`, `--cta-border`, and `--focus` tokens; `.feedback-error`, `.admin-feedback-filter.active`, link selectors, and CTA borders consume them — and on Rakdos (red theme), the feedback error message is visually distinct from a body link.
  3. No standalone hex literal (`#fff`, `#3a82f7`, `#c53030`, `#2f855a`, `#2b6cb0`, `#b83a2e`) survives outside a `:root` declaration in `site.css` or `site-common.css`; each is reachable as a named token from guild themes.
  4. All 25 guild theme files declare or inherit the new tokens; spot-checking three contrasting themes (Rakdos red, Selesnya green-white, Dimir blue-black) shows tokens resolve cleanly with no fallback bleed.
  5. Live deckflow.gg classic theme renders identically to pre-migration on the home, /feedback, /help, /about, and DeckSync pages — token migration is invisible to a casual user.
**Plans**: 3 plans
Plans:
- [x] 01-01-PLAN.md — Type scale tokens in site.css :root + replace font-size literals across site.css and site-common.css (UI-VS-01)
- [x] 01-02-PLAN.md — Semantic color tokens (--link, --danger, --cta-border, --focus) + hex literal hoist + rewire .feedback-error / .admin-feedback-filter / focus / CTA / link consumers (UI-VS-02, UI-VS-03)
- [x] 01-03-PLAN.md — Propagate tokens to all 25 :root files: 11 non-importer forks get explicit token block, importers inherit, Rakdos --link override for error-vs-link disambiguation, manual smoke-check checkpoint (UI-VS-04)

### Phase 2: Layout, Hierarchy & UX Copy
**Goal**: Home hub has an unmistakable headline action, all flagged inline
`style=` attributes live in CSS classes, and copy + voice + feedback busy-state
gaps from the audit are closed — lifting Visuals (3/4), Copywriting (3/4), and
Experience Design (3/4) pillars.
**Depends on**: Phase 1 (UI-LH-01's `.hub-card--primary` accent border consumes the new `--cta-border` / `--link` tokens; UI-LH-02's `.feedback-panel` and `.admin-feedback-detail` classes use the new color tokens)
**Requirements**: UI-LH-01, UI-LH-02, UX-01, UX-02, UX-03
**Success Criteria** (what must be TRUE):
  1. On the home hub, exactly one card per group (or one hero CTA above the grid) is visually promoted as primary — a first-time user can answer "what do I do?" without scanning all 11 cards. ChatGPT Analysis is the headline workflow.
  2. `Feedback/Index.cshtml` and `AdminFeedback/{Index,Detail}.cshtml` contain zero `style=` attributes; equivalent rules live in named CSS classes (`.feedback-panel`, `.admin-feedback-detail`, `.admin-action-form`) in `site-common.css`.
  3. The Moxfield bulk-edit hint copy (`_MoxfieldBulkEditHint.cshtml`) uses an action-specific verb that matches the form's actual submit button label — no generic "Submit".
  4. Submitting the public `/feedback` form on a slow connection visibly disables the button and shows a spinner/busy indicator until the server responds; double-submit is prevented.
  5. The Feedback page `<title>` and `<h1>` use the same voice convention (verb-noun OR noun-only) as the rest of the site.
**Plans**: 3 plans
**UI hint**: yes
Plans:
- [x] 02-01-PLAN.md — site-common.css: hub hero + .hub-card--primary + amend .feedback-panel + new .admin-feedback-detail + new .admin-action-form + .feedback-submit--busy spinner (UI-LH-01, UI-LH-02, UX-02)
- [x] 02-02-PLAN.md — Razor markup: Home.cshtml hero + 3 .hub-card--primary; Feedback/Index.cshtml voice fix + inline-style removal; AdminFeedback Index/Detail inline-style removal; _MoxfieldBulkEditHint verb param + 6 call-site updates (UI-LH-01, UI-LH-02, UX-01, UX-03)
- [x] 02-03-PLAN.md — feedback.ts new module + @section Scripts wiring + manual checkpoint for slow-network busy state (UX-02)

### Phase 3: Tech-Debt Cleanup
**Goal**: Remove test-only types from the production assembly, standardize
service constructors, stop tracking generated JS, and tighten the trusted
forwarded-headers surface — closing four CONCERNS.md items that don't depend
on UI work.
**Depends on**: Nothing (independent of CSS/UI; can run in parallel with Phase 1 or Phase 2)
**Requirements**: TD-01, TD-02, TD-03, TD-04
**Success Criteria** (what must be TRUE):
  1. `NullHttpClientFactory` and `NullScryfallRestClientFactory` no longer exist in the production assembly (deletion, not migration — per CONTEXT D-01: the existing `Fake*` family in `DeckFlow.Web.Tests/TestDoubles/` already serves actual test scenarios; the `Null*` factories are pure orphans once TD-02 collapses the test-compat ctors that referenced them). The production assembly's public surface no longer exposes test-only types.
  2. Services under `DeckFlow.Web/Services/` expose exactly one constructor each; tests that previously required a "test-compat" ctor route through a named test-helper factory in the test project. `dotnet build` is clean and existing test suite (where runnable) passes.
  3. `DeckFlow.Web/wwwroot/js/*.js` files are no longer tracked in git; `.gitignore` excludes them; the existing `tsc` MSBuild step still produces them at build time and the deployed site continues to load JS correctly.
  4. `ForwardedHeadersOptions.KnownIPNetworks` in `Program.cs` is restricted to Render's documented CIDR ranges (with a code comment citing the source); a request from a non-Render upstream cannot spoof `X-Forwarded-For` to dodge the feedback rate limiter.
**Plans**: 4 plans
Plans:
- [x] 03-01-PLAN.md — TD-02 single-ctor collapse: 10 services + new TestServiceFactory in DeckFlow.Web.Tests/TestDoubles + Program.cs DI factory delegates (Wave 1)
- [ ] 03-02-PLAN.md — TD-01 delete NullHttpClientFactory.cs and NullScryfallRestClientFactory.cs orphans (Wave 2, depends on 03-01)
- [x] 03-03-PLAN.md — TD-03 untrack wwwroot/js/*.js + .gitignore glob + README local-dev TS toolchain section (Wave 1)
- [ ] 03-04-PLAN.md — TD-04 ForwardedHeadersOptions Render CIDR research + Production-only restriction with cited source (Wave 1)

### Phase 4: Security & Bug Fixes
**Goal**: Per-IP rate-limit protects `/Admin/*` from basic-auth brute-force,
and Scryfall Tagger lookups either succeed or fall back gracefully instead of
returning empty 404 responses — closing the two concrete bugs that prompted
this milestone's quality bar.
**Depends on**: Nothing (independent of UI and tech-debt work; smallest phase, can ship first if desired)
**Requirements**: BUG-01, BUG-02
**Success Criteria** (what must be TRUE):
  1. Repeated failed basic-auth attempts against `/Admin/*` from a single IP are throttled by ASP.NET Core rate-limiting middleware; the existing warning log on each challenge still fires, and legitimate admin sessions are unaffected.
  2. The AI Category Suggestions page in `ScryfallTagger` mode either returns real tagger data for a known card (e.g. "Sol Ring") or surfaces a clear graceful-fallback message — it no longer silently returns HTTP 200 with empty suggestions.
  3. ChatGPT-paste workflow, deck reconcile, and category suggestion flows produce the same prompt artifacts on deckflow.gg as before — the security and bug fixes do not regress the core value pipeline.
**Plans**: TBD

## Progress

**Execution Order:**
Phases 1 → 2 are sequenced (Phase 2 consumes Phase 1 tokens). Phases 3 and 4
are independent and can run in parallel with Phase 1/2 or with each other.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Visual System Tokens | 3/3 | Complete | 2026-04-30 |
| 2. Layout, Hierarchy & UX Copy | 3/3 | Code-complete (awaiting verifier) | - |
| 3. Tech-Debt Cleanup | 0/TBD | Not started | - |
| 4. Security & Bug Fixes | 0/TBD | Not started | - |

## Coverage

| Requirement | Phase |
|-------------|-------|
| UI-VS-01 | Phase 1 |
| UI-VS-02 | Phase 1 |
| UI-VS-03 | Phase 1 |
| UI-VS-04 | Phase 1 |
| UI-LH-01 | Phase 2 |
| UI-LH-02 | Phase 2 |
| UX-01 | Phase 2 |
| UX-02 | Phase 2 |
| UX-03 | Phase 2 |
| TD-01 | Phase 3 |
| TD-02 | Phase 3 |
| TD-03 | Phase 3 |
| TD-04 | Phase 3 |
| BUG-01 | Phase 4 |
| BUG-02 | Phase 4 |

**Coverage:** 15/15 v1 requirements mapped. No orphans. No duplicates.

## Milestone Success Bar

UI audit re-score ≥ 20/24 against `tasks/UI-REVIEW.md` rubric (currently
16/24). Phase 1 alone targets the two lowest pillars (Color 2/4, Typography
2/4); Phase 2 targets Visuals (3/4), Copywriting (3/4), and Experience Design
(3/4). Phases 3 and 4 do not move the audit score directly but are required
for milestone completion.

---
*Roadmap created: 2026-04-30*
