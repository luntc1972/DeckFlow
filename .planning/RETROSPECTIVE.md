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

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | ~12 | 5 (1 abandoned) | First milestone; established phase-abandonment + post-mortem pattern |

### Cumulative Quality

| Milestone | Tests Added | LOC Delta | Notable |
|-----------|-------------|-----------|---------|
| v1.0 | +1 integration test (Tagger cookie-replay), +N unit tests (TD-04 partition, throttle window) | +20,284 / -5,194 across 136 files | All 15 v1 reqs shipped, 27/27 must-haves verified |
