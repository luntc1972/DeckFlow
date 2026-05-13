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

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | ~12 | 5 (1 abandoned) | First milestone; established phase-abandonment + post-mortem pattern |
| v1.2 | ~8 | 2 | Mid-milestone sub-phase insertion (10-05); flag-gated partial-ship pattern for Gemini |

### Cumulative Quality

| Milestone | Tests Added | LOC Delta | Notable |
|-----------|-------------|-----------|---------|
| v1.0 | +1 integration test (Tagger cookie-replay), +N unit tests (TD-04 partition, throttle window) | +20,284 / -5,194 across 136 files | All 15 v1 reqs shipped, 27/27 must-haves verified |
| v1.2 | +~80 unit tests (63 from initial Phase 10 + 17 across hybrid storage / Archidekt parity / 10-05 round-trip) | ~480 total unit tests pass (Core 57 + Web 407 baseline, +12 from 10-05) | All 5 v1.2 reqs functionally satisfied via manual T1-T8; documentation gaps (no VERIFICATION.md, Phase 9 frontmatter) accepted as tech debt |
