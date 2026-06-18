# Milestones

## Cycle 8 — Hardening & Backlog Burn-down (Shipped: 2026-06-17, `2026.06.4`)

**Phases completed:** 4 phases (51-54), 11 plans
**Git range:** 46 commits, 184 files (+7,627/−1,656), all 2026-06-17
**Requirements:** 8/8 satisfied (FEAT-01 PASS-WITH-NOTES). Audit status: tech-debt — no milestone audit run; every phase individually verified (P51 PASS, P52 PASS, P53 PASS 8/8 + SECURED 4/4, P54 PASS-with-notes). First CalVer release.

**Key accomplishments:**

- **Verified the shipped v1.7 Studio/publish pipeline end-to-end (P51/P52):** deferred non-prod operator-UAT smokes (Studio runtime render, `/Admin/Harvest` no-jump grid, re-distill/cap/cancel, Review/Publish git+LF) plus a **live prod publish run** — DirectPush SCP'd artifacts to the Render `/data` disk and ran a content-columns-only Postgres upsert, proving admin flags (`is_visible`/`is_evergreen`/`is_hidden`/`approval_status`) on all 86 pre-existing rows were preserved while 8 new rows landed `pending`/not-visible.
- **Fixed F-51-PG-01:** `AddDeckIdsAsync` compared TEXT `last_checked_utc` to a `timestamptz` param → Npgsql `42883` on Postgres (SQLite tolerated). Dialect-guarded `::timestamptz` cast (PG-only, no migration); PG 19/19 + SQLite 20/20. Surfaced by the `DECKFLOW_POSTGRES_TESTS=1` gate (HARD-03).
- **Burned down the Phase 39 architecture backlog (Phase 53, ARCH-01/02):** facade-then-extract split of the `CategoryKnowledgeRepository` god-file (1272→274 LOC + Schema/DeckQueue/CardCategory collaborators); `Program.cs` DI extracted into `AddDeckFlowXxx()` (553→354 LOC) + finished `Services/` foldering (Scryfall/Persistence/Content); deck-stat classifiers relocated to `DeckFlow.Core.Analysis` (+64 tests); `Feedback*` layering leak removed from the Core `IRelationalDialect`. Zero user-visible change; verifier PASS 8/8; SECURED 4/4. Finding C dropped (already addressed by the Core orchestrator slices); full dialect-branch collapse deferred (PG DDL parity prereq). The DI ValidateOnBuild smoke test caught a latent missing `IFeatureFlagCache` registration.
- **Resolved feature debt (Phase 54, FEAT-01/02):** captured the `SpellbookCombo` ranking fields (`popularity`/`manaValueNeeded`/`uses`) the parser previously dropped + priority-ranked Deck Primer combos (popularity DESC, manaValueNeeded ASC); verified Gemini artifacts fit the ~30,000-char paste ceiling across all 4 workflows (analysis 24,994 / comparison 23,830 / meta-gap 18,026 / primer 5,553) — flag stays default-off.
- **Merged v1.7 to `main` + confirmed Render deploys from `main` (Phase 51, OPS-01).**

**Quality:** build 0 errors; Core 447/447, Web 633/644 (11 PG-skip).

**Known carry-forward at close:** `deckflow_admin` credential deletion (P52 — password already rotated, deletion owed by operator); operator live Gemini paste before flipping `DECKFLOW_GEMINI_ENABLED` in prod (P54); full dual-dialect branch collapse (gated on a Postgres DDL parity test); Cycle 9 = Studio/content-pipeline expansion + SEO/growth + SEED-001 (KB add/remove + publish-tracking).

Full archive: `.planning/milestones/cycle8-ROADMAP.md` · `.planning/milestones/cycle8-REQUIREMENTS.md`

---

## v1.7 Local Harvest & Publish Studio (Shipped: 2026-06-17)

**Phases completed:** 10 phases (41-50), 35 plans, 36 tasks
**Requirements:** 23/23 satisfied (STU, ORCH, REVQ, PUB, GRID, HARV, UIR). Audit status: tech_debt — integration clean, deferred operator-UAT tracked (see `.planning/milestones/v1.7-MILESTONE-AUDIT.md`).

**Key accomplishments:**

- **DeckFlow.Studio (new standalone Blazor Server app, Phases 41/45/46/47):** local operator console to harvest YouTube captions → distill to KB entries via LLM (with a spend dry-run gate) → review/approve in a queue → publish to production via TWO paths: git commit-publish of the LF-normalized seed, and direct prod-DB push (SSH.NET SCP of artifacts to Render `/data` then a safe content-columns-only Postgres upsert). Prod connection lives in user-secrets only; StudioConfig is presence-only (never carries the secret).
- **Orchestrator extraction (Phase 42, ORCH-01/02):** harvest/distill/export domain logic moved DeckFlow.CLI → DeckFlow.Core as `IContentKbOrchestrator` (facade over 5 slice interfaces); CLI reduced to thin adapters; closes the v1.6 god-class backlog item. Pure refactor, golden-fixture pinned.
- **Approval status + safe upsert (Phase 43, REVQ-01/PUB-01/02):** `approval_status` self-healing migration + `UpsertContentColumnsOnlyAsync` (preserves admin `is_visible`/`is_evergreen`) + approved-only filtered export — the prerequisite both publish paths depend on.
- **Admin grid lazy paging (Phase 44, GRID-01/02):** `/Admin/Harvest` initial load goes synchronous-count → AJAX on-demand; `LOWER(commander_name)` partial index fixes the slow query at the source.
- **Dapper data-access adoption (Phase 49):** raw ADO.NET boilerplate in 13 dual-provider stores replaced with Dapper behind the existing `IRelationalDialect`/`RelationalDatabaseConnection` abstraction; 5 provider-aware type handlers preserve Sqlite+Postgres parity; DDL/introspection + unnest-batch stay raw.
- **Code-style enforcement (Phase 50):** operator ReSharper style reconciled into `.editorconfig` (5 bug-driven carve-outs win); changed-lines-only pre-commit hook + CI `format-gate`; existing files never reflowed; CLAUDE.md made the source of truth. CI behavioral proof both directions.
- **UI audit remediation (Phase 48, UIR-01/02/03):** 6-pillar visual audit of the deployed deckflow.gg re-scored from v1.0 16/24 → **20/24** — inline-SVG iconography + resting elevation, widened surface/bg delta, darker muted, raised `--fs-xs` floor, typography hierarchy, short-form empty-state panels; every finding browser-verified at mobile + desktop across light/dark/commander-table themes.

**Quality:** DeckFlow.sln builds 0/0 (Studio included); Core.Tests ~346, Web.Tests 622 + DeckFlow.Studio.Tests (bUnit 34); cross-phase integration check clean (0 hard breaks, all 5 E2E flows wired); changed-lines format gate green.

**Known deferred at close (operator-UAT, non-blocking, tracked in v1.7-MILESTONE-AUDIT.md):** Studio runtime render smoke (P41), admin grid no-jump browser smoke (P44), re-distill E2E + cap-persist + cancel-on-dispose (P45), Review/Publish browser + real-git smoke (P46), live SCP+prod-Postgres publish (P47, needs operator secrets), Postgres parity tests (P49, `DECKFLOW_POSTGRES_TESTS=1`). `/gsd-secure-phase` not run for 48 + 50.

---

## v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (Shipped: 2026-06-10)

**Phases completed:** 6 phases (28-33), 25 plans, 29 tasks
**Git range:** 219 commits, 781 files (+56,893/-2,108), 2026-06-03 → 2026-06-09
**Requirements:** 30/30 satisfied (HSK-02 descoped to backlog by design)

**Key accomplishments:**

- **Deck Primer Generator (Phase 31, PRM-01..12):** fourth paste-ready workflow at `/deck-primer` — 31-section catalog in 5 collapsible groups, Commander Spellbook combo grounding (null-graceful), bracket routing (EdhTop16 archetypes / generic fallback), category-knowledge distribution, per-AI artifact variants (ChatGPT/Claude/Gemini) via PacketArtifactStore zip round-trip.
- **Content KB Integration (Phase 30, KBI-01..06):** Expert Context block of top-K relevant curated clips injected into all three analysis prompt variants as attributed pull-quotes; "What Experts Say" panel on the DeckAnalysis result page; admin per-clip relevance score preview. Prod UAT passed 2026-06-07 (ships flag-gated/dark).
- **Expert Context Selection (Phase 32, SEL-01..06):** pin videos / follow creators / evergreen flag layered over auto-relevance via a 4-tier fill merge (pinned→followed→auto→evergreen); `is_evergreen` self-healing SQLite+Postgres migration; typeahead selection endpoints.
- **Admin Content KB Curation UX (Phase 33, KBUX-01..02):** instant client-side filter/search over the entries list + readability sweep (zebra, sticky header, hover/focus, mobile cards).
- **Housekeeping (Phases 28-29, HSK-01/03/04):** DeckFlow.Core XML-doc backfill + compiler doc-gate widened to `[DeckFlow.Core/**.cs]`; retroactive VERIFICATION.md + artifact hygiene.
- **Quality infra (milestone close):** added Vitest+jsdom browser test runner + first GitHub Actions CI (build + xUnit + Vitest); diagnosed and fixed the expert-pin injection bug (ParseRowsAsync dropped parse-failed pin rows — `a106c6a`) and unified pin-id derivation (`bfe16b1`).

**Known deferred items at close:** 15 (pre-v1.5 carryover — stale 999.x/v13 debug sessions, May quick-task references, empty todos; see STATE.md Deferred Items). SEL-02 live-pin re-confirm pending next KB-enable window.

---

## v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (Shipped: 2026-06-03)

**Phases completed:** 14 phases (16-27 + inserted 21.1/21.2), 31 plans

**Stats:**

- Commits: 343 (`v1.3..HEAD` on `v1.4` branch)
- Files: 638 changed, +54,651 / −4,726 LOC
- Timeline: 2026-05-23 → 2026-06-03 (11 days)
- Requirements: 20/20 active v1.4 REQ-IDs shipped (KB-12 → backlog; DBO-01/CAT-02 ROADMAP-tracked inserts also shipped)
- Final tests: Core 257/257, Web 528 pass / 5 PG-integration skips

**Key accomplishments:**

1. **Content Knowledge Base end-to-end (KB-01..09, Phases 19-22)** — local CLI pipeline (YouTube captions via YoutubeExplode + Whisper fallback with monthly spend caps → LLM distillation into ≤200-word summaries, 3-8 timestamped clips, controlled-vocabulary tags → markdown prompt artifacts) feeding a slim Postgres site index with flag-gated public browse/filter, per-entry admin publish curation, and CSRF+SameOrigin-guarded mutations. Mid-milestone re-architecture from server-harvest to local-CLI model (2026-05-26) preserved all REQ-IDs.
2. **Pluggable LLM distill backends (KB-10/11, Phase 21.2)** — `LlmDistillationProviderFactory` selects openai|claude via env; claude-CLI backend runs the full 10-video distill at $0 subscription cost (cleared the Phase 21.1 gate after OpenAI 429 insufficient_quota), cross-platform WSL/Windows incl. the dotnet.exe-from-WSL hard case; codex backend deferred to backlog with documented untrusted-input read-boundary rationale.
3. **Category cache rebuilt (DBO-01/CAT-01/CAT-02, Phases 26/24/27)** — integer-keyed star schema with prod full-reset + re-harvest (hot commander query 69s timeout → 0.66ms index-only), Sol Ring/colorless-staple empty-categories fixed via read-time `CategoryFilter`, and content-hash dedup + 5-day refresh on deck writes.
4. **Admin mobile + tooling (AMOB-01..04/AHD-01/MODAL-01, Phases 18/25/16)** — `admin.css` factored into `admin-common.css` + `admin-mobile.css` scoped to `.admin-shell` (≥320px viewports, ≥44px touch targets), server-side paged harvested-decks commander grid, native `<dialog>` focus-trapped confirm modal reused across admin pages.
5. **Doc-warning gate live (DOC-01/02, Phases 17+23)** — every public type AND member in DeckFlow.Web XML-documented (475 compiler-derived sites in Phase 23 alone); `NoWarn 1591;1573;1587` stripped from the csproj and the editorconfig gate flipped to warning severity scoped to `[DeckFlow.Web/**.cs]`, probe-proven real; DeckFlow.Core (186 sites) deliberately deferred.
6. **Cross-AI delivery pattern sustained** — Codex implemented / Claude planned+reviewed across the milestone, with scope-fenced dispatches; plus a post-ship /simplify pass (SpendLedgerBase extraction, distillation validator dedup, dialect collapse) and ADR 0001 (prompt variants intentionally decoupled).

**Verification:**

- Milestone audit: `tech_debt` — 20/20 requirements satisfied, 4/4 E2E flows wired (integration-checker), 0 critical gaps (`.planning/milestones/v1.4-MILESTONE-AUDIT.md`)
- Live UAT: Phase 20 5-channel harvest (10/10 captions), Phase 21.2 10/10 claude distill at $0, Phase 22 both checkpoints, Phase 24 live smoke, Phase 26 prod reset verified via information_schema/pg_indexes

**Known deferred items at close:** 18 audit-open items acknowledged (11 stale scanner re-flags; see STATE.md Deferred Items) + audit tech debt: 7 phases missing VERIFICATION.md, P26 SUMMARYs, prod flag `content.kb.enabled` OFF (user flip pending), dual artifact trees, Core doc backfill, KB-12.

---

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
