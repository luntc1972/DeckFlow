# Requirements: DeckFlow — Polish & Quality Milestone

**Defined:** 2026-04-30
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT and get back a useful answer in one round-trip — without the user reformatting anything.
**Success bar:** UI audit re-score ≥ 20/24 (currently 16/24 per `tasks/UI-REVIEW.md`).

## v1 Requirements

Requirements for this milestone. Each maps to a roadmap phase.

### UI Visual System (UI-VS)

Address the lowest-scoring audit pillars (Color 2/4, Typography 2/4) by hardening the design-token layer.

- [ ] **UI-VS-01**: Define a 6-step type scale via `--fs-xs/sm/base/lg/xl/2xl` tokens in `site.css` `:root` and replace the 18 distinct font-size literals across core CSS
- [ ] **UI-VS-02**: Add semantic color token aliases (`--link`, `--danger`, `--cta-border`, `--focus`) so error text, link color, focus ring, and CTA border can diverge from `--accent-strong` per theme — fixes red-theme error-as-link bug
- [ ] **UI-VS-03**: Hoist standalone hex literals (`#fff`, `#3a82f7`, `#c53030`, `#2f855a`, `#2b6cb0`, `#b83a2e`) in `site.css` into named `:root` tokens reachable by guild themes
- [ ] **UI-VS-04**: Verify token migration applied to all 25 guild theme files (each theme can override the new tokens; classic theme remains the default)

### UI Layout & Hierarchy (UI-LH)

Address Visual Hierarchy gap on the home hub (Visuals 3/4) and inline-style usage flagged by audit.

- [ ] **UI-LH-01**: Promote a primary focal action on the home hub — single hero CTA above the grid OR `.hub-card--primary` modifier on one card per group, chosen to drive ChatGPT Analysis as the headline workflow
- [ ] **UI-LH-02**: Move inline `style=` attributes from `Feedback/Index.cshtml` and `AdminFeedback/{Index,Detail}.cshtml` into named CSS classes (`.feedback-panel`, `.admin-feedback-detail`, `.admin-action-form`)

### Copy & UX (UX)

Wins flagged in Copywriting (3/4) and Experience Design (3/4) pillars.

- [ ] **UX-01**: Replace generic "Submit" verb in `_MoxfieldBulkEditHint.cshtml` with action-specific copy that mirrors the actual button label ("Run Compare" or "Look Up")
- [ ] **UX-02**: Add submit busy-state to `/feedback` form — disable button + spinner while POSTing so users know the click registered
- [ ] **UX-03**: Reconcile voice mismatch between page `<title>` (verb-noun) and `<h1>` (noun-only) on the Feedback page — pick one convention and align

### Tech-Debt Cleanup (TD)

Tractable items from `.planning/codebase/CONCERNS.md` that don't require coordinated refactors.

- [ ] **TD-01**: Move `NullHttpClientFactory` and `NullScryfallRestClientFactory` from `DeckFlow.Web` to `DeckFlow.Web.Tests` and add `[InternalsVisibleTo]` so test-only types stop leaking into production assembly
- [ ] **TD-02**: Standardize on one constructor per service across `DeckFlow.Web/Services/` and adopt named test-helper factories where multiple constructors exist for testability
- [ ] **TD-03**: Remove generated `*.js` files from git tracking (TypeScript is source of truth); rely on the existing `tsc` MSBuild step to produce them at build time
- [ ] **TD-04**: Tighten `ForwardedHeadersOptions.KnownIPNetworks` to Render's documented CIDR ranges instead of accepting any upstream proxy

### Quality Bug Fixes (BUG)

Concrete bugs surfaced in CONCERNS.md or recent ops experience.

- [ ] **BUG-01**: Fix Scryfall Tagger 404 — investigate the deck-tagger refresh path that returns 404 for some deck IDs and either correct the URL pattern or fall back gracefully
- [ ] **BUG-02**: Per-IP rate-limit on `/Admin/*` routes — add ASP.NET Core rate limiting middleware to throttle basic-auth brute-force attempts (currently mitigated only by warning logs)

## v2 Requirements

Deferred to future Polish & Quality cycles or other milestones. Acknowledged but not in this roadmap.

### Visual & Accessibility

- **UI-VS-V2-01**: Visual regression harness for 25 themes (Playwright snapshots) — own testing-infra milestone
- **UI-VS-V2-02**: Mobile polish beyond audit-flagged items
- **UX-V2-01**: PWA / offline-first

### Refactor

- **REF-V2-01**: DeckController god-class split — own refactor milestone
- **REF-V2-02**: ChatGPT services extraction (`PromptBuilder`, `ScryfallReferenceResolver`, etc.) — own refactor milestone
- **REF-V2-03**: DeckController test coverage uplift — depends on REF-V2-01

### Operational

- **OPS-V2-01**: Health/ready endpoint + correlation ID middleware — own observability milestone
- **OPS-V2-02**: Structured API error envelope
- **OPS-V2-03**: ChatGPT artifact retention sweep
- **OPS-V2-04**: Tagger refresh `IHostedService`
- **OPS-V2-05**: DB-backed Archidekt cache job persistence

### Scryfall / HTTP layer

- **NET-V2-01**: ScryfallThrottle → Polly rate limiter migration (started under prior plan)
- **NET-V2-02**: Per-route Scryfall fairness queue
- **NET-V2-03**: Disk-backed Scryfall set cache
- **NET-V2-04**: RestSharp abstraction (`IUpstreamHttpClient`)
- **NET-V2-05**: Cloudflare upstream snapshot harness
- **NET-V2-06**: Resilience pipeline behavior tests

## Out of Scope

Explicitly excluded from this milestone with reasoning.

| Feature | Reason |
|---------|--------|
| Browser-extension test coverage gap | Manifest-version protocol bumps already documented; protocol is stable; defer until protocol changes |
| Path-base `~/...` view discipline (every Razor view) | Manual review acceptable; mass enforcement is high-effort for low-risk surfaces |
| Framework migration (e.g. .NET 11 / Razor Components) | Pinned by deployed app; not in polish scope |
| Render → other host migration | Render works fine; speculative |
| Theme additions beyond the existing 25 | Scope creep; theme system is feature-complete |

## Traceability

Mapping from REQ-ID to roadmap phase. Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| UI-VS-01 | Phase 1 | Pending |
| UI-VS-02 | Phase 1 | Pending |
| UI-VS-03 | Phase 1 | Pending |
| UI-VS-04 | Phase 1 | Pending |
| UI-LH-01 | Phase 2 | Pending |
| UI-LH-02 | Phase 2 | Pending |
| UX-01 | Phase 2 | Pending |
| UX-02 | Phase 2 | Pending |
| UX-03 | Phase 2 | Pending |
| TD-01 | Phase 3 | Pending |
| TD-02 | Phase 3 | Pending |
| TD-03 | Phase 3 | Pending |
| TD-04 | Phase 3 | Pending |
| BUG-01 | Phase 4 | Pending |
| BUG-02 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 15 total
- Mapped to phases: 15
- Unmapped: 0 ✓

---
*Requirements defined: 2026-04-30*
*Last updated: 2026-04-30 — traceability mapped to 4-phase roadmap*
