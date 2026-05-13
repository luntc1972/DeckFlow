# Milestones

## v1.2 Multi-AI Prompts (Shipped: 2026-05-13)

**Phases completed:** 2 phases, 8 plans, 22 tasks

**Key accomplishments:**

- One-liner:
- [Rule 3 - Blocking] Symlinked node_modules to unblock TypeScript build in worktree
- One-liner:
- Per-AI dispatch on request.TargetAiPlatform proven on BuildAnalysisPrompt with full Claude XML skeleton and full Gemini markdown+tweaks variants; ChatGPT path unchanged except for the new <result> wrap directive.
- Per-AI dispatch primitive proven in 10-01 fanned out across all four remaining prompt builders. AISEL-02 and AISEL-03 content-complete; every prompt the user generates is now keyed off request.TargetAiPlatform.
- AISEL-04 fully closed across all three ChatGPT pages. The unified <result>...</result> response extractor lives at the top of ExtractJsonPayload — one regex covers every response parser path with the existing fenced-JSON detection preserved as fallback.
- Magic 3000ms literal lifted to a documented module-scope constant; skipPersistence flag now self-clears 30s after set so a transient upload failure cannot silently disable form-state persistence for the rest of the page lifetime.
- Session zip for /chatgpt-cedh-meta-gap now round-trips full Step 1 state — fetched EDH Top 16 entries, selected reference indexes, and all four filter scalars — so a re-uploaded session regenerates the prompt without re-hitting edhtop16. Closes the v1.2 milestone-close blocker surfaced by integration test T3.

---

## v1.0 Polish & Quality (Shipped: 2026-05-02)

**Delivered:** Lifted DeckFlow's design-token layer, hardened tech-debt and security surfaces, and fixed the Scryfall Tagger empty-tag bug for cEDH staples — without breaking the live deckflow.gg ChatGPT-paste pipeline.

**Stats:**

- Phases: 5 (Phase 4 abandoned 2026-05-02; rerouted to Phase 5 — see `04-ABANDONED.md` in the archived phase dir)
- Plans: 17 total summaries
- Files modified: 136 | LOC: +20,284 / -5,194
- Commits: 63 (`5c11b00..5a36d42`)
- Timeline: 2026-04-30 → 2026-05-02 (3 days)
- Requirements: 15/15 v1 requirements shipped

**Key accomplishments:**

1. **Visual-system token layer (Phase 1, UI-VS-01..04)** — 6-step type scale (`--fs-xs/sm/base/lg/xl/2xl`) and semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) added to `site.css` `:root`; standalone hex literals hoisted to named tokens; token block propagated to all 25 guild themes with per-theme overrides (e.g. Rakdos `--link` `#ff9ea4`) so error vs link color can finally diverge.

2. **Layout, hierarchy & UX copy (Phase 2, UI-LH-01..02 / UX-01..03)** — Hub primary-CTA promoted, all inline `style=` attributes in Feedback / AdminFeedback views moved to named CSS classes, page `<title>` / `<h1>` voice aligned, and a non-blocking submit busy-state shipped on `/feedback`.

3. **Tech-debt cleanup (Phase 3, TD-01..04)** — Test-only `Null*` factories relocated to the test assembly via `[InternalsVisibleTo]`, services collapsed to a single ctor with test-helper factories, generated browser JS untracked from git (TypeScript-only source of truth), and `ForwardedHeadersOptions` tightened to Path B-rawpeer with `CF-Connecting-IP` + Render Inbound IP Rules.

4. **Scryfall Tagger restored for cEDH staples (Phase 5, BUG-01)** — Surgical revert of manual cookie replay back to auto-cookie management, plus two follow-up root causes that Phase 4 had masked: Cloudflare BIC blocks Render egress IPs without browser-shaped headers, and `AutomaticDecompression` must be enabled when advertising `Accept-Encoding`. Sol Ring / Counterspell / Mana Crypt all return 5+ oracle tags from production.

5. **Postgres-backed admin brute-force throttle (Phase 5, BUG-02 + TD-04 patch)** — `admin_brute_force_buckets` table with lazy expiry, 10/15-min window, monotonic `Retry-After`; gated on Render Inbound IP Rules with Cloudflare CIDR allow-list; same `CF-Connecting-IP` partition fix propagated to the `/feedback` rate-limiter so multi-proxy fragmentation can't dilute the bucket.

6. **Tagger cookie-replay regression guard (Phase 5)** — In-process integration test runs the full Tagger flow against a real `SocketsHttpHandler` via a localhost `HttpListener` stub; would catch a regression to manual cookie replay or `UseCookies=false` before it ships, closing the verification gap that let commit `4db8b8a` reach production untested.

**Verification:**

- Phase 5 verifier: passed 27/27 must-haves (7 ROADMAP SCs + 20 plan-frontmatter truths)
- Live UAT: Sol Ring 7 tags, Counterspell 5 tags, Mana Crypt 9 tags; admin throttle 10×401 + 1×429 with `Retry-After` 899→879 monotonic decrement
- UI audit re-score against `tasks/UI-REVIEW.md`: not yet measured (deferred to v1.1)

**Known deferred items at close:** 2 (see STATE.md Deferred Items)

- Phase 04 UAT: 5 stale pending scenarios (phase abandoned; work re-shipped under Phase 5)
- Phase 04 VERIFICATION: human_needed (phase abandoned; superseded by Phase 5 verification)

---
