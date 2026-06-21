# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — Polish & Quality

**Shipped:** 2026-05-02
**Phases:** 5 (1 abandoned mid-flight, rerouted to Phase 5) | **Plans:** 17 | **Commits:** 63

### What Was Built
- 6-step type scale + semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) propagated to all 25 guild themes — error vs link color can finally diverge per theme.
- Hub primary-CTA, inline-style cleanup, `<title>`/`<h1>` voice alignment, `/feedback` busy-state.
- Tech-debt sweep: test-only types out of prod assembly, single-ctor service standardization, generated JS untracked from git, `ForwardedHeadersOptions` Path B-rawpeer with `CF-Connecting-IP`.
- Scryfall Tagger restored for cEDH staples — auto-cookie revert + Cloudflare BIC browser-header bypass + `AutomaticDecompression` fix.
- Postgres-backed admin brute-force throttle (lazy expiry, monotonic `Retry-After`, CF-CIDR allow-list gate) + same partition fix on `/feedback` rate-limiter.
- Localhost `HttpListener` integration test for the Tagger cookie-replay path — closes the verification gap that let `4db8b8a` ship untested.

### What Worked
- **Phase 4 abandonment was the right call.** Both fixes passed static checks but failed live on prod. Naming it ABANDONED (not "wip") and writing a post-mortem (`04-ABANDONED.md`) before planning Phase 5 forced the surgical-revert mindset that surfaced the real Cloudflare BIC + decompression root causes.
- **Plan-checker → discuss-phase → plan loop on Phase 3** caught a 5-blocker plan twice and forced Phase 3 SC #1 to clarify "Null factories deleted, not migrated to TestDoubles" — saved a wrong-direction execution.
- **Live UAT before plan close** (Phase 5 Plan 02 admin throttle 11-burst with `Retry-After` 899→879) proved the throttle worked end-to-end before declaring done — much stronger than a passing unit test alone.
- **`gsd-verifier` 27/27 must-haves pass with grep + log evidence** caught the milestone in a defensible state — no soft "looks done" close.

### What Was Inefficient
- **Phase 4 ate ~6 hours producing dead code.** Iterate-printings + sort-by-released_at were the wrong fix; the bug was Cloudflare BIC, not Scryfall printings. Faster path: probe prod with a real curl from a Render shell first, then plan.
- **Multiple plans had blank `one_liner:` summary frontmatter** (Phase 02-01..03, Phase 04-01/03/04) so `gsd-sdk summary-extract` returned `null` or `"One-liner:"` in the milestone accomplishments aggregator — had to hand-rewrite MILESTONES.md.
- **REQUIREMENTS.md traceability table never auto-updated** as phases shipped — table still said "Pending" for 9 items at close even though work was done. Either keep it current per phase or stop maintaining a duplicate of the roadmap progress table.
- **STATE.md drift**: `status: executing` and "Phase 5 context gathered" stayed as the resume pointer through actual completion of Phase 5. The state file should auto-flip on phase verification.

### Patterns Established
- **Browser-shaped headers + `AutomaticDecompression` are required when egressing from Render to anything fronted by Cloudflare BIC** — bake into any new outbound HTTP client (not just Tagger). See `feedback_http_resilience_pattern.md` memory.
- **`CF-Connecting-IP` is the canonical client-IP source** for any rate-limit / throttle partition key — gated for spoof-resistance by Render Inbound IP Rules + Cloudflare CIDR allow-list, not header trust.
- **Real-handler integration tests for HTTP-facing services** (localhost `HttpListener` stub against the actual `SocketsHttpHandler`) are cheap and catch what mock-handler unit tests miss — adopt for any future cookie/redirect/decompression-sensitive path.
- **Phase abandonment with post-mortem doc** is a first-class outcome, not a failure — mark `[~]` in roadmap, write `NN-ABANDONED.md`, plan corrective phase from the post-mortem.

### Key Lessons
1. **Probe production reality before planning a fix.** Phase 4's design assumed Scryfall printing-resolution was the bug; a single curl from prod would have revealed Cloudflare BIC instantly.
2. **A passing static check is not a fix.** "Code compiles and tests pass" is necessary but not sufficient when the bug is environmental (TLS / cookies / compression / proxy behavior). Live UAT is the gate.
3. **Keep planning artifacts honest in real time.** REQUIREMENTS.md traceability and STATE.md status both drifted off reality during the milestone — fold those updates into the phase-completion ritual instead of saving them for milestone close.
4. **Surgical revert beats forward-fix when the prior code was working.** Phase 5's first move was reverting the `4db8b8a` HTTP-migration changes for Tagger specifically, then adding the new headers / decompression on top — much cleaner than trying to patch the manual cookie-replay path.

### Cost Observations
- Model mix: ~95% opus (planning, verification, complex reasoning), ~5% sonnet/haiku (small edits)
- Sessions: ~12 across 3 days
- Notable: Phase 4's abandoned work was ~6h of opus that produced no shipped code, but the post-mortem unlocked the actual Phase 5 fix in <1 day.

---

## Milestone: v1.2 — Multi-AI Prompts

**Shipped:** 2026-05-13
**Phases:** 2 | **Plans:** 8 | **Commits:** 30+ across 5 days

### What Was Built
- AI target selector (ChatGPT / Claude / Gemini) on all three ChatGPT analysis pages, defaulting to ChatGPT, with selection round-tripped through the session zip.
- Per-AI dispatch primitive at the top of every prompt builder, fanned across 15 variants (analysis, set-upgrade, comparison, follow-up, meta-gap × ChatGPT/Claude/Gemini).
- Claude-optimized prompt with XML-structured blocks; Gemini-optimized markdown variant with explicit `<result>` JSON mandate as the absolute final line.
- Unified `<result>...</result>` response wrapper extractor — single regex covers every paste-back path with the fenced ` ```json ` block preserved as fallback.
- Hybrid deck text storage: every session zip stores both the original (user-pasted) and the canonical (rebuilt-and-alphabetized) deck text. Original-prefers-canonical loader precedence.
- Archidekt parser state machine — section headers (Commander/Mainboard/Deck/Sideboard/Maybeboard/Possible Includes) now switch board state, matching Moxfield parser.
- cEDH meta-gap Step 1 round-trip — fetched EDH Top 16 entries + filter scalars + selected reference indexes preserved in zip, regenerate without re-hitting edhtop16.
- TargetCommanderBracket selector now in a `.bracket-callout` visual treatment on the Packets page.
- Filename sanitizer hardening + download Content-Type gate (no HTML-as-zip on validation errors).
- Step 2 display artifacts restored on Comparison + cEDH upload paths.
- Gemini AI target hidden behind `DECKFLOW_GEMINI_ENABLED` env flag at milestone close — full packet exceeds gemini.google.com paste cap, truncating instructions.

### What Worked
- **Plan + spec inserted mid-milestone for the cEDH zip Step 1 gap (10-05).** T3 retest surfaced a real round-trip gap; rather than hand-coding a fix, scoped it as a discrete sub-phase with spec + plan + 10 tasks. Closed the milestone-block cleanly with TDD-driven implementation (12 new unit tests) and one manual retest.
- **Manual integration tests T1-T8 covered cross-phase wiring** in lieu of a formal integration-checker pass. Every primary user flow (3 pages × 2-3 AIs × NEW/LEGACY paste-back paths) was exercised end-to-end with real round-trip zips.
- **`<result>` wrapper as a unifying seam** — single regex, three pages, no per-page parser logic. Far simpler than introducing AI-specific response parsers.
- **Hidden form field for cEDH state (vs server-side session)** — stateless, no session-affinity required on Render, sidesteps edhtop16 rate-limit on regenerate. ~50-200KB form post is acceptable.
- **Codex MCP for review-after-implement** when the user explicitly invoked it. Caught the filename sanitizer + download Content-Type gate as MEDs that became `7a54f50`.
- **Recognizing the Gemini paste-cap limit at close, not at design.** Three days of integration tests revealed Gemini-the-web-UI is a different beast than Gemini-the-model. Right call to flag-gate and ship rather than over-engineer a split-message prompt for v1.2.

### What Was Inefficient
- **HANDOFF.json went stale during multi-session work.** A prior session shipped 10-05 implementation, but this session's resume read an older HANDOFF that said Phase 10 was already done. Cost: a confused stash, an unnecessary rebase, and 30 minutes of state reconstruction.
- **Phase 9 SUMMARYs never declared `requirements-completed:` frontmatter.** Phase 10 SUMMARYs did. The inconsistency forced the milestone audit to mark BRKT-01 and AISEL-01 as `unsatisfied` (documentation-only — code shipped).
- **No `09-VERIFICATION.md` or `10-VERIFICATION.md` files ever authored.** Manual integration tests covered the behavior but the verification artifact gap forced an audit `gaps_found` verdict at milestone close.
- **Stale dev server hid verified-correct binary changes.** During a Gemini MANDATORY block test session, source + compiled DLL both contained the fix but the user's textarea showed the old prompt. Hard-restart of the dotnet process picked up the new build. Logged as `.continue-here.md` anti-pattern.
- **Multiple wip-commit pauses on 10-05.** Three commits over two sessions before the final implementation landed (`36a8828`, `1ee548e`, `7829c57`). Mid-implementation pause-and-resume churn is a smell.
- **EOL pollution.** Working tree showed 598 files modified — pure CRLF/LF flips from Windows-side tools touching files in WSL. Cost: a stash + drop ritual on every checkout/rebase.

### Patterns Established
- **Round-trippable browser-side workflow state** for any flow that depends on a rate-limited upstream API: store the response in the session zip + restore via a hidden form field so re-upload regenerates without re-fetching.
- **Loader-side `WorkflowStep` heuristic** — zip-load functions return enough restored state for the caller to derive the correct step, making the heuristic unit-testable rather than controller-bound.
- **Backwards-compatible zip schema** — new artifacts + new request-context scalars are additive; absence yields default values, never an exception path. Legacy zips load on first try.
- **Two-source AI selector default** — radio defaults to ChatGPT in markup; persisted "Gemini" value falls back to ChatGPT in the partial when the flag is off so a real radio always shows checked.
- **Feature-flag for "feature partially shipped"** — when a UI surface works but the user's tool ecosystem doesn't support it well (Gemini paste cap), hide-and-flag is cheaper than redesign or remove.
- **Magic-number lift with explanatory comment** when the literal carries a tradeoff worth explaining (response timing, regression mitigation). Not for every constant.
- **Auto-clear guard pattern** for transient UI state flags: `setTimeout` that deletes only if the flag is still the expected value, so other code-path overrides aren't clobbered.

### Key Lessons
1. **Suspect dev server staleness FIRST when UI contradicts verified source+binary.** Hard-restart the dotnet process before going deeper into code debugging.
2. **HANDOFF.json and STATE.md must reflect the latest pushed commit, not the session's last local commit.** When resuming, ALWAYS `git fetch` and compare `HEAD` vs `origin/<branch>` before reading any planning artifact.
3. **Verification artifacts are part of the phase, not a milestone-close afterthought.** Generate `NN-VERIFICATION.md` at phase completion or accept that the milestone audit will paint shipped work as `unsatisfied`.
4. **Web UIs have paste caps that the model's context window doesn't.** Gemini web UI accepts ~30K-100K chars depending on tier; the model's 1M token context is irrelevant if the input never reaches the model. Test with the user's actual paste destination, not the API.
5. **Sub-phases (10-05) are legitimate when a real gap surfaces mid-milestone.** Don't try to retrofit the gap into the existing plan numbering — insert with full spec + plan + tasks and treat as a first-class deliverable.
6. **EOL config matters when working cross-platform.** Without `.gitattributes`, Windows tools introduce CRLF and WSL sees the whole tree as modified. Either set `core.autocrlf=true` repo-wide or add `.gitattributes` with `* text=auto`.

### Cost Observations
- Model mix: ~85% opus (planning, complex Razor + service work, multi-session resume reconstruction), ~10% sonnet (small edits, doc cleanup), ~5% Codex MCP gpt-5.4 (review passes)
- Sessions: ~8 across 5 days
- Notable: 10-05 was scoped + spec'd + planned + implemented inside a single resume thread once the gap was identified. Mid-implementation pauses (3 wip commits) suggest the implementation could have been single-session if not interrupted.

---

## Milestone: v1.3 — Frontend Hardening + AI-Agnostic Rename + Code Hygiene

**Shipped:** 2026-05-23
**Phases:** 13 (5 production + 8 backlog/closure) | **Plans:** 51 | **Commits:** ~370 | **Timeline:** 10 days (2026-05-13 → 2026-05-23)

### What Was Built
- **Phase 11 (WDG-01..10):** 10 sweep PRs from 2026-05-13 Web Design Guidelines audit. site-common.css cross-cutting a11y primitives (color-scheme, reduced-motion, touch-action, tabular-nums, scroll-margin-top), admin focus-visible foundation, df-typeahead keyboard nav + ARIA combobox, ARIA tablist server-render, CSP inline-handler removal, URL/textarea autocomplete sweep.
- **Phase 12 (RENAME-01..03):** AI-agnostic URLs (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap` replacing chatgpt-* slugs) + H1/nav/hub labels + Mock A `.page-lede` explainer lines + 11 permanent 301 redirects (later retired in Phase 999.8).
- **Phase 13 (CLASSRENAME-01..03):** ChatGpt* C# types renamed to AI-agnostic names across request DTOs, services, view models, parsers, artifact stores. XML `<summary>` doc-comment backfill on every renamed class.
- **Phase 14 (AUDIT-01..03):** Broader codebase name-vs-behavior audit across 5 projects + missing doc-comment backfill. Release build clean.
- **Phase 15 (AIPLATFORM-01..03):** Sealed `AiPlatform` record value object replacing stringly-typed `TargetAiPlatform`. OCP forecast 3/10 → 8/10 — adding a 4th platform is one registry entry plus N variant classes.
- **Phase 999.1:** AI-agnostic prose adaptation across 3 workflow Razor views + C# exception messages + 3 Help markdown files. Hybrid pattern (universal noun above `_AiSelector`, `@aiPlatform.DisplayName` below).
- **Phase 999.2:** Claude `<result>` wrapper stripped from 5 prompt variants (direct JSON fenced-block output).
- **Phase 999.3:** Packet download session cache (per-request in-memory, TTL bounded) — eliminates full Scryfall pipeline replay on preview → download. Large Commander deck preview→download <2s on cache hit.
- **Phase 999.4:** Truncated-JSON response UX. `JsonReaderException` caught and translated to "wait for AI to finish generating" inline on workflow pages.
- **Phase 999.5/6:** Test-suite hardening (9→0 residual failures across 3 plans: F-ENV-COLLECTION serialization for env-mutating tests, F-PROD-CONTRACT `IHarvestRunStore.GetByIdAsync` production-bug fix surfaced inside the test cleanup).
- **Phase 999.7:** Audit cleanup (STATE arithmetic, REQUIREMENTS checkboxes, 5 stale Claude doc-comments, F-02 asymmetry comment, 999.5-UAT status finalization).
- **Phase 999.8:** Legacy chatgpt-* 301 redirects retired (22 lines deleted, 0 added; 11 URLs now 404). 2+ weeks live as graceful-deprecation tier was sufficient.

### What Worked
- **Cross-AI execution pattern (Codex codes, Claude reviews) established 2026-05-19.** Codex authored every production edit via `codex exec`; Claude orchestrated planning + verification + cross-AI peer review + commits + gate-running. Sustained zero friction across 6+ consecutive phase closures (999.5 → 999.8). Reduced single-model blind spots and kept Claude's leverage on review/orchestration, not implementation.
- **Cross-AI plan review caught 2 BLOCKER issues in 999.7 P01 before execution.** `/gsd-review` with Codex as reviewer found real planning bugs that Claude's plan-checker missed. Workflow: `/gsd-plan-phase` → `/gsd-review` → revise → `/gsd-execute-phase`.
- **Backlog-phase numbering 999.x.** Allowed v1.3 to absorb 8 quality-debt phases (999.1-999.8) without disrupting production phase numbering (11-15). Made `/gsd-audit-milestone` re-audit trivial — production reqs verified once at 22/22, backlog phases tracked separately.
- **`no-ship-failing-tests` rule (established 2026-05-22)** created Phase 999.6 specifically. Net delta 9→0 failures across 3 plans (stale test mechanical fixes + F-ENV-COLLECTION test-class race fix + F-PROD-CONTRACT real production bug surfaced inside the cleanup). Discipline pays off — prevented shipping with deferred test debt for the first time.
- **Atomic per-finding commits** through 999.7 + 999.8. Each audit finding got its own SUMMARY + commit, making the audit re-verification at HEAD trivial (one grep per finding).

### What Was Inefficient
- **STATE.md arithmetic drift accumulated across 4+ phase closures.** `completed_phases: 9 / total_phases: 11` (should be 11/11) and `completed_plans: 66 / total_plans: 46` (mathematically impossible: 66 > 46) shipped uncaught until milestone audit. Took Phase 999.7 P01 + 4 closure commits to reconcile. Need automatic STATE.md verification on phase close or audit-time arithmetic gate.
- **REQUIREMENTS.md WDG-01..10 checkboxes shipped unchecked despite Phase 11 closure 2026-05-13.** 10 days of audit drift before 999.7 P02 flipped them. Phase-close transition should auto-flip checkboxes by REQ-ID match.
- **Audit F-01 evidence inventory drift.** Audit listed 3 stale Claude doc-comments; repo grep at planning time confirmed 5. Codex review HIGH #1 caught the audit undercount; scope expanded to 5 files. Audit should run the grep gate it's claiming to verify, not just sample.
- **Planning-time grep miscount on 999.7-04 SC4** (`grep -c D-11 returns 1` but HEAD had 3). Verification gate format was too coarse — needed handler-anchored awk-grep. Logged as v1.4 follow-up.
- **999.3-01-PLAN.md has no SUMMARY** — rolled into 999.3-02-04 SUMMARY chain but the missing SUMMARY tripped the milestone.complete CLI counter (50/51). Need either a "rolled-into" SUMMARY pointer convention or explicit deletion of empty PLANs.

### Patterns Established
- **Codex-codes / Claude-reviews split** is now the global default (`~/.claude/CLAUDE.md` Workflow section).
- **Per-finding audit-closure commit pattern**: each milestone-audit finding → 1 SUMMARY + 1 commit. Re-audit gates verify per-finding grep, not aggregate diffs.
- **UAT.md as verification primitive for backlog phases** — `/gsd-verify-work` produces UAT.md instead of VERIFICATION.md for backlog phases that use D-XX decisions rather than formal REQ-IDs. Accepted as flow divergence in 2026-05-22 audit (F-03).
- **Machine-verified UAT for doc-only / deletion-only phases.** 999.7 + 999.8 UATs are 100% grep+build+test gates with no human-attestation step (zero behavioral change to verify). Pattern reusable for future doc-cleanup phases.

### Key Lessons
1. **Audit before complete-milestone is non-negotiable.** 2026-05-22 audit caught 7 findings that would have shipped uncaught; closing them took 5 commits across 999.7+999.8. Audit is cheap; deferred debt is expensive.
2. **STATE.md arithmetic needs automation.** Manual maintenance accumulates drift faster than human review can catch. Plan-close transition should compute counters, not trust them.
3. **Cross-AI peer review on plans is high-leverage.** Catches real BLOCKERs Claude misses. Cost is one Codex round-trip per plan; benefit is shipping the right thing the first time.
4. **`no-ship-failing-tests` discipline is foundational.** All prior milestones shipped with deferred failures; v1.3 was the first to enforce 0 before merge. Made the audit-passed → ship-ready transition smooth.
5. **Net-deletion phases (999.8) ship fast.** 22 lines deleted, 0 added; build clean; tests at baseline. When the work is "remove this," scope is bounded and verification is grep-trivial.

### Cost Observations
- Model mix: ~70% Codex (every production edit via cross-AI dispatch), ~25% Opus (planning, audit, verification, orchestration), ~5% Sonnet (small doc/state edits, gsd-plan-checker)
- Sessions: ~15 across 10 days (paused 2026-05-22 evening, resumed 2026-05-23 morning for verify→audit→complete→ship chain)
- Notable: Phase 999.8 (1 plan, 22-line deletion + closure + UAT) ran from concept to closed in ~10 minutes via Codex dispatch + Claude gate-running. Pattern scales to similar bounded-deletion work.

---

## Milestone: v1.5 — Deck Primer Generator + Content KB Integration + Housekeeping

**Shipped:** 2026-06-10
**Phases:** 6 (28-33) | **Plans:** 25 | **Commits:** 219 | **Timeline:** 2026-06-03 → 2026-06-09 (7 days)

### What Was Built

- Deck Primer Generator — fourth paste-ready workflow (`/deck-primer`): 31-section catalog, Spellbook combo grounding, bracket-routed matchups, per-AI artifacts (PRM-01..12).
- Content KB → analysis integration: Expert Context block in all 3 prompt variants + "What Experts Say" panel (KBI-01..06); pin/follow/evergreen expert selection over a 4-tier fill merge (SEL-01..06); admin curation-list filter + readability (KBUX-01/02).
- Housekeeping: DeckFlow.Core XML-doc backfill + doc-gate widen (HSK-01); VERIFICATION/artifact hygiene (HSK-03/04).
- Quality infra at close: Vitest+jsdom browser test runner + first GitHub Actions CI; expert-pin injection bug fixed; pin-id derivation unified.

### What Worked

- **Visual-verify caught real bugs unit tests missed** — Phase 31 visual-verify surfaced 3 production bugs; the pattern keeps paying off for UI phases.
- **Milestone audit caught the paper-trail gaps** — the first audit (`gaps_found`) correctly flagged missing VERIFICATION.md ×4, the SEL orphan, and the SEL-02 functional bug before close; backfill + fix → clean re-audit.
- **Adversarial diagnosis beat the consensus hypothesis** — the SEL-02 pin bug had a "known" cause (pin-id mismatch) repeated across memory + two audit agents; disciplined refutation (read-invariant → dead code) found the real cause (ParseRowsAsync dropping parse-failed pin rows). An unverified hypothesis repeated three times is still unverified.
- **Codex-codes / Claude-reviews held** — the SEL-02 TDD fix went RED→GREEN via Codex with Claude catching the auto-tier scoping gap in review before it landed.

### What Was Inefficient

- **Orphaned requirements** — Phase 32 (SEL) was inserted mid-milestone and its REQ-IDs were never added to REQUIREMENTS.md, only caught at milestone audit. Inserting a phase should add its requirements to the traceability table at insert time.
- **VERIFICATION.md skipped at phase close** — 4 of 6 phases shipped without one; backfilling retroactively is more work than writing at close.
- **Subagent final-message truncation** — the integration checker and debug-session-manager both truncated their synthesis; recovered via persisted observations + finishing the trace inline. Agents that WRITE their output (the VERIFICATION backfill) were unaffected.

### Patterns Established

- **Testable-TS seam under `module:none`** — extract pure logic into a global-assignment IIFE (`globalThis.X = {...}`, no export) so it compiles under the browser tsc AND imports under Vitest/esbuild; consumers reference via top-level `declare const`. Test files live outside `wwwroot/ts/`.
- **First real CI** — GitHub Actions: `npm ci` → `dotnet build` → `dotnet test` → `npm test`; npm precedes build because the C# build invokes local tsc.

### Key Lessons

- Refute the loudest hypothesis before building on it — a misdiagnosis repeated three times is still a misdiagnosis.
- Add inserted-phase requirements to the traceability table immediately, not at audit.
- Write VERIFICATION.md at phase close; retroactive backfill is the milestone-audit tax.

### Cost Observations

- Model mix: Opus orchestration (main thread) + Sonnet subagents (verifiers, integration checker, doc backfill) + Codex (gpt-5.4) for all implementation/fix code.
- Notable: parallel Sonnet agents for the 4 VERIFICATION.md backfills (independent files) — agent-writes-file sidesteps the subagent truncation issue.

---

## Milestone: v1.7 — Local Harvest & Publish Studio (CalVer `2026.06.3`)

**Shipped:** 2026-06-17 (merged to main, squash) | **Phases:** 10 (41-50) | **Plans:** 35 | **Requirements:** 23/23

### What Was Built
- **DeckFlow.Studio** — new standalone Blazor Server operator console: browse/paste YouTube → harvest captions → LLM-distill (spend dry-run gate) → review/approve queue → publish two ways (git commit-publish of the LF seed; direct SSH.NET SCP of artifacts to Render `/data` + content-columns-only Postgres upsert preserving admin fields). Prod secret in user-secrets only; presence-only `StudioConfig`.
- Orchestrator extraction CLI→Core `IContentKbOrchestrator` (42, closed the v1.6 god-class backlog item); approval-status + safe upsert (43); admin-grid AJAX lazy paging + `LOWER(commander_name)` index (44); Dapper across 13 dual-provider stores with Sqlite+Postgres parity (49); changed-lines `.editorconfig` format gate + CI (50).
- 6-pillar UI audit remediation of deployed deckflow.gg: **16/24 → 20/24** (48).

### What Worked
- **Inline gstack audit when the subagent couldn't.** The deployed re-score (UIR-01) needed real browser screenshots; the gsd-executor has no gstack, so the orchestrator ran Task 1/2 inline. Right call over forcing a blind subagent.
- **Backfilling verification caught drift, not bugs.** The milestone audit found 7 phases missing VERIFICATION.md and ~16 stale REQUIREMENTS rows — all tracking-artifact debt, confirmed functional via SUMMARY evidence + integration check (0 hard breaks). Backfilling 7 verifiers + the integration checker turned an honest `gaps_found` into `tech_debt`.
- **Per-phase security held up.** Retroactive secure-phase on 41/48/50 closed 0-open across all 10; the secrets-wiring phase (41) verified the prod conn-string never reaches logs/UI/DI across all 3 entry points.

### What Was Inefficient
- **Long-lived branch drift.** `v1.7` ran 286 commits ahead of `main` and was deployed to prod *off the feature branch*. Worse, `main` accumulated 11 hotfixes (deck-analysis, KB admin filters, checkbox/theming) that were never back-merged — surfaced as a merge conflict only at ship time. Cost a merge-reconciliation detour.
- **Milestone label == branch name == tag name.** `v1.7` as branch AND tag broke `git push origin v1.7` ("matches more than one") and forced explicit refspecs throughout. Directly motivated the CalVer switch.
- **Tracking artifacts not maintained during execution.** VERIFICATION.md / REQUIREMENTS status were skipped per-phase and had to be reconstructed at close.
- **API 529 storm** mid-close forced inline code-review + verification for Phase 48 (recovered later for the backfill).

### Patterns Established
- **CalVer + named milestones (ADR 0002).** Public release = `YYYY.MM`; planning milestones are named cycles, not version numbers; `2.0` reserved for a deliberate identity reset. Retroactive CalVer tags added for v1.0–v1.7 alongside the legacy `v1.x` tags. First named cycle: "Cycle 8 — Hardening & Backlog Burn-down."
- **Squash-merge milestone PR + re-point release tag onto the squash commit** (the branch-tip tag is unreachable from main otherwise).
- **Worktrees off when the build needs gitignored deps** — v1.7 phases serialized on the main tree because `dotnet build` runs the tsc target needing `node_modules` (absent in fresh worktrees).

### Key Lessons
- **Merge each milestone to `main` and deploy *from main* at (or before) ship — never run prod off a long-lived feature branch.** Cycle 8's short hardening phases are the habit reset.
- **Back-merge `main` hotfixes into the active dev branch promptly** so drift surfaces early, not at the merge gate.
- **A milestone audit that returns `gaps_found` is often tracking debt, not functional gaps — verify against SUMMARY/SECURITY/integration evidence before treating it as a blocker.**
- **Decouple the planning-milestone number from the public version** — conflating them manufactured false "approaching 2.0" pressure.

### Cost Observations
- Build 0/0; Core.Tests ~346, Web.Tests 622, DeckFlow.Studio.Tests (bUnit) 34. All 10 phases verified (4 passed, 6 human_needed operator-UAT) + secured (0 threats open). Audit: `tech_debt`. Squash merge: +50,412 / -4,516 across 446 files, 288 commits collapsed to one.
- Deferred at ship (operator-UAT, tracked): Studio runtime render, admin-grid browser, re-distill E2E + cap-persist + cancel-on-dispose, Review/Publish browser+git, **live SCP+prod-Postgres publish (needs operator prod secrets)**, Postgres parity (`DECKFLOW_POSTGRES_TESTS=1`) → scoped as Cycle 8.

---

## Milestone: Cycle 9 — Content Pipeline & Publish-Tracking (CalVer `2026.06.5`)

**Shipped:** 2026-06-19
**Phases:** 4 (55-58) | **Plans:** 11

### What Was Built
Publish-state tracking wired end-to-end: a `pushed_to_prod_utc` column + shared `PublishStateDeriver` (four states), Studio per-video status badges + multi-select harvest + Block/Blocked-list/unblock + single-URL add + publish-state on Review/Publish, a `/Admin/ContentKb` publish-state column, and a reworked distill prompt. Validated by a real dogfood run.

### What Worked
- **Dogfooding caught a real integration gap unit tests couldn't.** Phase 58 ran the actual pipeline on a new video and surfaced that DirectPush never set `is_visible` — Studio stayed Pushed-hidden while prod showed Published. The bug only existed at the cross-surface seam; the fix was clean (keyed `SetVisibilityAsync`, prod-then-local) and went through Claude-code → Codex-review → secure in one session.
- **Per-phase verify+secure** meant the milestone closed with zero open security threats and no separate audit needed.
- **CalVer + named-cycle + own-branch** flow held again (cycle9 branched from v1.7, carried cycle8 as commits, squashed to main).

### What Was Inefficient
- **Pre-close artifact cruft.** 23 "open" audit items, almost all stale (months-old completed quick-tasks the audit mis-parsed by filename/status-field, v1.3-era debug sessions). Resolving them was real time spent on bookkeeping, not product. The audit's quick-task parser keying on bare `SUMMARY.md` vs `<id>-SUMMARY.md` is a recurring false-positive.
- **Verification format drift:** Phase 57's `human_needed` gate (deferred to Phase 58) lingered as an "open" item until manually closed at milestone time.

### Patterns Established
- **DirectPush publishes visible** (operator decision): the surgical prod-write path also grants visibility, prod-then-local so local never over-reports prod. Harvest/seed-load stay ships-dark.
- **Mid-cycle bug-fix under a temp delegation rule** (Claude codes / Codex reviews) — recorded as a dated, expiring project memory.

### Key Lessons
- A validation/dogfood phase is worth keeping as the last phase of a build cycle — it pays for itself by catching seam bugs.
- Carry a lightweight `/gsd-cleanup`-style sweep into the milestone-close checklist so quick-task cruft doesn't accumulate across cycles.

### Cost Observations
- Model mix: ~all Opus (main loop) + 1 Codex gpt-5.4-low review dispatch + 1 sonnet security auditor.
- Notable: the SC2 fix (find → decide → code → review → secure) all landed in the close session without a separate plan cycle.

---

## Milestone: Cycle 10 — Studio Automation, Sync & Polish (CalVer `2026.06.6`)

**Shipped:** 2026-06-21
**Phases:** 5 (59-63) | **Plans:** 16

### What Was Built
- One-action harvest → auto-distill → auto-approve in Studio (swappable clip-count signal, default ON/5; spend gate + provider unchanged).
- Read-only Pull-from-Prod reconcile: SSH.NET SCP download + Postgres read into isolated staging, four-way diff classification, per-entry adopt-prod/keep-local, live sanitized progress panel. Prod never written.
- Persisted creator list + dropdown picker; unharvested-only default browse + show-all; lightweight skip/un-skip lane distinct from Block.
- Studio UI polish: shared `StatusBadge` + `VideoStatusResolver.FromContentRow` pure mapper, creator filtering on Harvest + Review, grouped Pipeline/Support nav, Review→Publish shortcut, About-link fix.
- Self-contained single-file win-x64 `DeckFlow.Studio.exe` (~116 MB) via re-runnable publish scripts — runs with no .NET install.

### What Worked
- Coarse-granularity phasing (automation / sync / selection / polish / packaging) held — each phase had a clean operator success gate, so no separate dogfood phase was needed.
- Pure-helper extraction (`VideoStatusResolver.FromContentRow`, `CreatorNameResolver`, `PublishStateDeriver` reuse) kept status/derivation logic in one testable place and out of the Razor views.
- bUnit + automated verification carried most of the UI proof, so deferring the live operator smoke at close was low-risk.

### What Was Inefficient
- Branch drift: the manabase public tool shipped on `main` in parallel while Cycle 10 lived on its own `cycle10` worktree, so close required a divergent squash (96 vs 36 commits) — though source overlap turned out to be only README + ROADMAP.
- A concurrent session briefly hijacked the working tree mid Phase-59, interleaving unrelated commits; resolved by re-homing onto a clean `cycle10` branch.
- The milestone-complete CLI auto-extracted noisy accomplishment bullets ("Plan:", stray rule text) — hand-rewritten at close.

### Patterns Established
- Read-only prod lane mirrors the write lane (Pull-from-Prod as the inverse of DirectPush) with the reader exposing no write method at all + sanitized progress copy — a reusable shape for safe prod reflection.
- Canonical visible projection (`GetVisibleChannelVideos`) as the single source for rendered rows + Select-All + action scope, so a filtered-out row can never be acted on.

### Key Lessons
- Keep parallel public-app work (manabase) and operator-tool milestone work on branches that rebase onto a shared base often, or accept a divergent squash at close — verify source overlap early to size the merge risk.
- A CalVer tag on `main` snapshots everything currently shipped (Studio + parallel manabase), which is correct for date-based releases even when the named milestone scopes only one body of work.

### Cost Observations
- Model mix: ~all Opus (main loop); Claude implemented under the temporary Codex-review-only override.
- Sessions: multi-session cycle (59-63 executed 2026-06-20→21); close folded UAT + archival + README + squash into one session.

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | ~12 | 5 (1 abandoned) | First milestone; established phase-abandonment + post-mortem pattern |
| v1.2 | ~8 | 2 | Mid-milestone sub-phase insertion (10-05); flag-gated partial-ship pattern for Gemini |
| v1.3 | ~15 | 13 (5 prod + 8 backlog) | Cross-AI execution split (Codex codes, Claude reviews); cross-AI plan review; `no-ship-failing-tests` rule; backlog-phase numbering 999.x; machine-verified UAT for doc-only phases |
| v1.7 | ~many | 10 (41-50) | New standalone Blazor app in-solution; CalVer + named-milestone switch (ADR 0002); squash-merge + tag-re-point release flow; verification/security backfilled at close; long-lived-branch drift identified as anti-pattern |

### Cumulative Quality

| Milestone | Tests Added | LOC Delta | Notable |
|-----------|-------------|-----------|---------|
| v1.0 | +1 integration test (Tagger cookie-replay), +N unit tests (TD-04 partition, throttle window) | +20,284 / -5,194 across 136 files | All 15 v1 reqs shipped, 27/27 must-haves verified |
| v1.2 | +~80 unit tests (63 from initial Phase 10 + 17 across hybrid storage / Archidekt parity / 10-05 round-trip) | ~480 total unit tests pass (Core 57 + Web 407 baseline, +12 from 10-05) | All 5 v1.2 reqs functionally satisfied via manual T1-T8; documentation gaps (no VERIFICATION.md, Phase 9 frontmatter) accepted as tech debt |
| v1.3 | Test baseline grew to 500 (Web 440 + Core 57 + 3 skipped Postgres integration). Net 9→0 failures resolved in Phase 999.6. New tests: AiPlatformExtensionTests 7 facts (4th-platform OCP proof), ResultContractTests (Claude direct-JSON divergence), AdminEnvCollection serialization scaffolding | +47,724 / -5,385 across 386 files | All 22 REQ-IDs SATISFIED (WDG-01..10, RENAME-01..03, CLASSRENAME-01..03, AUDIT-01..03, AIPLATFORM-01..03). 8/8 security threats CLOSED. First milestone with Failed:0 at ship. Audit re-passed 2026-05-23 after 7 findings closed via 999.7+999.8. |
| v1.7 | DeckFlow.Studio.Tests (bUnit) 34 added; Core.Tests ~346, Web.Tests 622. Dapper type-handler parity tests (SQLite-gated; PG behind `DECKFLOW_POSTGRES_TESTS`). | +50,412 / -4,516 across 446 files (squash) | 23/23 reqs satisfied; all 10 phases secured (0 threats open). Audit `tech_debt` — integration clean (0 hard breaks, 5 E2E flows wired), residue = operator-UAT. First CalVer-tagged release (`2026.06.3`). |
