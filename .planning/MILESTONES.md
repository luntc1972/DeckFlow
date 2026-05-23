# Milestones

## v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Shipped: 2026-05-23)

**Phases completed:** 13 phases, 51 plans, 37 tasks

**Key accomplishments:**

- Five accessibility primitives (color-scheme, reduced-motion gate, touch-action, tabular-nums utility, scroll-margin-top) added to site-common.css so all 22 guild theme forks inherit them via cascade without per-fork edit.
- Universal keyboard-focus indicator + color-scheme + tabular-nums added to admin.css, mirroring site.css:109-118 so the admin shell renders the same visible focus ring as the main shell.
- One-liner:
- One-liner:
- 1. [Rule 3 - Blocking] Plan listed `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`; the file on disk is `ChatGptCedhMetaGap.cshtml`
- Sweep-applied autocomplete="url" + inputmode="url" + ellipsis placeholder to 6 URL inputs and autocomplete="off" to 49 user-paste textareas across 8 Razor views — WDG-09 closed.
- One-liner:
- Renamed the three Razor view files (`ChatGptPackets.cshtml` / `ChatGptDeckComparison.cshtml` / `ChatGptCedhMetaGap.cshtml`) to AI-agnostic names (`DeckAnalysis.cshtml` / `DeckComparison.cshtml` / `CedhMetaGap.cshtml`) using `git mv` for history preservation, and updated all 39 `return View("ChatGpt…", …)` literal strings in DeckController.cs so view lookup resolves to the renamed files. Closes the Phase 11 verification note flagging the CedhMetaGap.cshtml filename mismatch.
- Closed the user-visible label half of RENAME-02 — added the Mock A `<p class="page-lede">` explainer paragraph under the H1 on all three AI workflow pages with exact D-07 copy, rebranded Page 1's H1 + browser title + nav label + hub-card title from `ChatGPT Analysis` to `Deck Analysis` per D-06 + D-09, swung all six remaining `~/chatgpt-` hrefs in `_DeckToolTabs.cshtml` (3) and `Home.cshtml` (4 including the hub-hero) to the new slugs, and added a cross-cutting `.page-lede` CSS rule to `site-common.css` (CLAUDE.md D-07 invariant — single source, NOT forked across any of the 22 guild themes). Pages 2 and 3 H1s left unchanged per D-06 (already AI-agnostic). Build clean against both `DeckFlow.Web.csproj` (Debug) and `DeckFlow.sln` (Release) with 0 warnings, 0 errors.
- Ctor parameters + private fields (3 of each):
- DeckControllerTests.cs (d7510da):
- One-liner:
- One-liner:
- Commit 1
- Gate 1 (warning count):
- 1. [Rule 1 - Bug] Missing `using DeckFlow.Web.Services;` in Analysis family
- AllForTesting internal seam added to AiPlatform.cs + 7-fact AiPlatformExtensionTests proving 4th-platform OCP extension with zero production edits (SC5 diff gate: PASS)
- 1. chatgpt-step-eyebrow baseline count
- One-liner:
- Hybrid prose pattern applied to CedhMetaGap.cshtml (6 ChatGPT hits rewritten) and DeckComparison hub-card in Home.cshtml generalized to "AI-authored breakdown"
- 10 InvalidOperationException strings across 3 C# service files rewritten to AI-agnostic phrasing, blocking page error banner from naming ChatGPT for Claude/Gemini users
- One-liner:
- README brand-neutralized (29 ChatGPT → AI generalizations), full-phase invariants grep gate PASS, `dotnet build Release` clean, human UAT approved.
- Found during:
- One-liner:
- One-liner:
- One-liner:
- STATE.md progress frontmatter now reports the SC1 literal v1.3 post-completion target: 11/11 phases, 46/46 plans, 100 percent.

---

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
