# Phase 5 Discussion Log

**Date:** 2026-05-02
**Mode:** /gsd-discuss-phase 5 (default mode)
**Outcome:** CONTEXT.md committed; 4/4 gray areas decided.

## Gray-Area Selection

User selected ALL 4 of 4 gray areas:
- ☑ BUG-01 Tagger code shape
- ☑ BUG-02 partition key source
- ☑ BUG-02 throttle persistence
- ☑ Observability scope

## Discussion

### Q1: BUG-01 cookie handling shape

**Options presented:**
1. Typed client + UseCookies=true (Recommended) — flip the handler config; smallest diff; preserves typed-client+Polly+IMemoryCache infrastructure
2. Per-call HttpClientHandler (full pre-migration revert) — drops typed client; sacrifices connection pooling
3. RestSharp CookieJar on existing handler — adds in-app cookie management; new dependency surface

**User selection:** #1 — Typed client + UseCookies=true

**Notes:** Smallest diff preserves the well-tested infrastructure (Polly pipelines, IMemoryCache, ScryfallThrottle) for the eight other HTTP-dependent services. Targets only the Tagger-specific cookie regression.

### Q2: BUG-01 printing-resolution shape

**Options presented:**
1. Drop iterate-printings entirely (Recommended) — revert to `cards/named?exact=`; cleanest code
2. Keep iterate-printings as fallback — hedges if Scryfall default is Tagger-untagged
3. Keep cards/search but stop iterating — halfway change, no clear advantage

**User selection:** #1 — Drop iterate-printings entirely

**Notes:** Direct probe verified `cards/named?exact=Sol Ring` returns `soc/128` and Tagger has 15 oracle tags for that printing. Phase 4-02 + 4-03 are dead code.

### Q3: BUG-02 partition key source

**Options presented:**
1. CF-Connecting-IP + Render Inbound IP Rules (Recommended) — most secure; requires manual Render dashboard step
2. X-Forwarded-For + ForwardLimit=2 — no inbound rules; brittle CIDR-drift risk
3. Stay with RemoteIpAddress, accept fragmentation — weakest protection

**User selection:** #1 — CF-Connecting-IP + Render Inbound IP Rules

**Notes:** Locks the spoofing path closed by gating Render inbound to Cloudflare-only. Requires user to configure Render dashboard manually.

### Q4: BUG-02 throttle state persistence

**Options presented:**
1. Postgres-backed (Recommended) — durable across restart; ~80 LOC + tests
2. In-memory only — deploy = amnesty window
3. Hybrid in-memory + Postgres write-through — overkill for /Admin/*

**User selection:** #1 — Postgres-backed

**Notes:** Reuses existing RelationalDatabaseConnection + dialect pattern. New table `admin_brute_force_buckets`. Tests use existing in-memory SQLite pattern.

### Q5: Observability scope

**Options presented:**
1. Minimal step-level (Recommended) — distinct LogWarning per step; ~15 lines new code
2. Rich w/ correlation IDs — Activity instrumentation; ~30 LOC
3. Skip observability — counter to Phase 4 lessons

**User selection:** #1 — Minimal step-level

**Notes:** Five distinct templates: Tagger.Resolve, Tagger.SessionFetch, Tagger.GraphQlPost, Tagger.Parse, Tagger.Lookup-success. Replaces existing single LogWarning that lumps three failure modes.

## Deferred Items

Captured in CONTEXT.md `<deferred>`:
- Integration test against stub Tagger (Plan 05-03 optional)
- Distributed tracing / correlation IDs
- Hybrid in-memory + Postgres write-through cache
- iterate-printings fallback
- Multi-instance Render Pro upgrade
- Apply observability pattern to other services
- Cloudflare CIDR auto-refresh

## Claude's Discretion (left for planner)

- Postgres dialect SQL specifics (parameter prefix, UPSERT shape)
- Test infrastructure choice (Testcontainers vs in-memory SQLite vs FakeStore)
- Cloudflare CIDR list management approach
- ScryfallTaggerService method shape after dead-code removal
- Whether to include Plan 05-03 integration test

## Scope-Creep Redirects

None — user stayed within phase boundaries.

## Anti-Pattern Check

`.planning/phases/04-security-bug-fixes/.continue-here.md` notes the WSL/MSBuild parallel build silently-fails issue (severity: advisory, not blocking). Already known; carries forward to Phase 5 execution.

---

*Discussion completed: 2026-05-02 via /gsd-discuss-phase 5*
