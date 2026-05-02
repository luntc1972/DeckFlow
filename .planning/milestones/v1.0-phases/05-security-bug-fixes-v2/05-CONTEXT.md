# Phase 5: Security & Bug Fixes v2 — Surgical Revert + Corrective Throttle - Context

**Gathered:** 2026-05-02
**Status:** Ready for planning
**Source:** /gsd-discuss-phase 5 (default mode, 4 gray areas selected, all 4 decided)

<domain>
## Phase Boundary

Restore Tagger to its pre-`4db8b8a` working state for the cEDH-staple lookup path, and ship a *real* working admin brute-force throttle (persistent state, correct partition key). Drop the iterate-printings + sort-ASC dead code that came from Phase 4-02/4-03 — Phase 4's investigation found those addressed the wrong layer.

Propagate the corrective IP-derivation to Phase 03 TD-04's feedback rate-limiter (same multi-proxy fragmentation defect is latent there).

Add minimal step-level logging to ScryfallTaggerService so future regressions can be diagnosed from Render logs without git archaeology.

**Not in scope:** integration test framework changes, additional admin endpoints, any UI work, AI-SPEC review. Those are future phases.

</domain>

<pre_phase_findings>
## Pre-Phase Findings (verified in session 2026-05-02)

These are not assumptions — they are direct-probe verified:

1. `cards/named?exact=Sol Ring` returns `set=soc, num=128, released_at=2026-04-24` (Scryfall's chosen default printing for that card today).
2. `tagger.scryfall.com/card/soc/128` HEAD probe returns 200.
3. `tagger.scryfall.com/graphql` POST with proper CSRF + cookies for soc/128 returns **15 oracle tags** (`star arch`, `strixhaven`, `pond`, `dutch angle`, `adds multiple mana`, …).
4. Tagger sets exactly **one** Set-Cookie header: `_scryfall_tagger_session=...;path=/;secure;HttpOnly;SameSite=Lax`.
5. Pre-migration code (file at `4db8b8a^:DeckFlow.Web/Services/ScryfallTaggerService.cs`) used `HttpClientHandler{UseCookies=true}` + `cards/named?exact=` — this shape provably worked.
6. Phase 4-02's iterate-printings approach (`cards/search?unique=prints` + 5-cap HEAD probe) and Phase 4-03's sort-ASC by `released_at` solved a problem that didn't exist — every Sol Ring printing including the newest `soc/128` returns 15 tags via GraphQL when called with proper cookies.
7. Cloudflare Free tier does NOT throttle per-endpoint brute force (verified: 30/sec hammering passes through unblocked, no `cf-ratelimit-*` headers, `cf-cache-status: DYNAMIC` confirms passthrough). Application-layer throttle is required.
8. Render Starter + persistent disk = guaranteed single instance. Render's edge LB fans across multiple internal proxy IPs (RFC1918 10.x.x.x range) → `Connection.RemoteIpAddress` varies per request → fragmented bucket. Verified by 11-burst returning non-monotonic Retry-After (251 → 252) within 1-second.
9. Real proxy chain is `Cloudflare → Render edge → container`. Phase 4-04's `ForwardLimit=1` + RFC1918 trust list peeled only the Render hop, landing on Cloudflare edge IP (still fragmented).
10. Pre-Phase-4 code state (commit `bcc1693`) is the current baseline after revert commit `b3a8a5b`. Tagger broken since `4db8b8a` (2026-04-27); admin throttle never existed pre-Phase-4.

</pre_phase_findings>

<decisions>
## Implementation Decisions

### BUG-01: Tagger Code Shape

**Cookie handling — DECISION:** Keep the typed `ScryfallTaggerHttpClient`, flip `UseCookies = false` → `UseCookies = true` on the SocketsHttpHandler in Program.cs. Delete the `BuildCookieHeader` / `StripCookieAttributes` / manual `AddHeader("Cookie", ...)` code in `ScryfallTaggerService`. Let .NET handle cookies automatically. Re-enable `AllowAutoRedirect` (default true) so the page-GET can follow redirects if Tagger ever issues one.

**Why this choice:** Smallest diff. Preserves the typed-client design (Polly pipelines, IMemoryCache, ScryfallThrottle, named pipelines) that works correctly for the OTHER eight HTTP-dependent services. Targets only the Tagger-specific cookie regression.

**Printing resolution — DECISION:** Revert `ResolveCardPrintingAsync` to pre-migration shape: `cards/named?exact=<cardName>` → returns `(set, collector_number)` from Scryfall's chosen default printing. Drop the iterate-up-to-5-printings loop. Drop the sort-ASC by `released_at` from Plan 04-03. Drop the negative `IMemoryCache` "tagger has no indexed printing" entry — every printing has tags, the negative cache was caching a phantom problem.

**Keep:** `IMemoryCache` for the (set, num) tuple result (positive cache 24hr) — single-printing lookup is also worth caching.

### BUG-02: Admin Brute-Force Throttle

**Partition key source — DECISION:** Read the `CF-Connecting-IP` request header. Cloudflare always sets this to the real client IP (single value, no chain). Cannot be spoofed past Cloudflare's edge. The throttle's partition key is `"admin:" + CF-Connecting-IP`.

**Spoof prevention requirement (HUMAN ACTION):** Configure Render Inbound IP Rules in the Render dashboard to allow only Cloudflare's published CIDR ranges (https://www.cloudflare.com/ips-v4/ and https://www.cloudflare.com/ips-v6/). Without this gate, an attacker reaching Render's container IP directly could bypass Cloudflare and supply a fake CF-Connecting-IP header.

**Fallback if CF-Connecting-IP missing:** If the header is missing (e.g., direct hit before inbound rules are configured), fall back to `"admin:unknown"` — single bucket, all unidentifiable traffic shares one bucket. Conservative fail-closed posture; protects against attacker probing without the right header. Log a warning so misconfig surfaces.

**Throttle persistence — DECISION:** Postgres-backed. New table `admin_brute_force_buckets(partition_key TEXT PRIMARY KEY, count INT NOT NULL, window_start TIMESTAMPTZ NOT NULL)`. Repository class `AdminBruteForceTrackerStore` with the same `(bool, int) IsThrottled(key, now)` and `void RecordFailure(key, now)` contract used by Phase 4-01. Reuse `RelationalDatabaseConnection` + `IRelationalDialect` pattern from `FeedbackStore` and `CategoryKnowledgeRepository`.

**Schema migration:** `CREATE TABLE IF NOT EXISTS` on first call (matches existing pattern for feedback + category-knowledge tables). No explicit migration framework.

**Window logic:** Same 10/15min as Phase 4-01 (`PermitLimit=10`, `Window=15min`). Lazy expiry on access — when `IsThrottled` is called and `now - window_start >= 15min`, treat the bucket as expired and let `RecordFailure` reset it.

**TD-04 propagation — DECISION:** Update `DeriveFeedbackPartitionKey` (Program.cs) to use the same CF-Connecting-IP read as the admin partition key. Both partition functions go through a shared helper `DeriveCloudflareClientIp(HttpContext)`. Phase 03's spoof-resistance test still passes because `DeriveFeedbackPartitionKey` still does NOT read X-Forwarded-For — but its underlying IP source is now the CF header instead of the proxy peer IP.

### Observability

**Logging shape — DECISION:** Minimal step-level structured logging. Five distinct LogWarning/LogInformation templates in ScryfallTaggerService:

```
LogWarning("Tagger.Resolve failed for {CardName}: HTTP {StatusCode} in {ElapsedMs}ms", cardName, status, ms)
LogWarning("Tagger.SessionFetch failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms; csrf={CsrfPresent} cookies={CookieCount}", ...)
LogWarning("Tagger.GraphQlPost failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms", ...)
LogWarning("Tagger.Parse failed for {CardName}: {Reason}", cardName, reason)
LogInformation("Tagger.Lookup succeeded for {CardName} in {ElapsedMs}ms returning {TagCount} tags", cardName, ms, tagCount)
```

Replace existing `LogWarning("Unable to obtain Tagger session for {CardName}.")` (which lumps three failure modes) with the distinct templates above. Existing 403→retry path (`RefreshSessionAndRetryAsync`) gets its own template too.

**README operations note — DECISION:** Restore the BUG-02 throttle blurb that was reverted with Phase 4. Format follows Phase 4's plan template: lockout window (10 attempts / 15min), Retry-After behavior, the Cloudflare inbound-rules requirement noted explicitly so an operator running this elsewhere knows the spoof prerequisite.

### Phase 4 Dead Code to Remove

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` — drop iterate-printings loop + sort key logic + negative-cache "no printing found" path. Drop the `BuildCookieHeader`/`StripCookieAttributes` helpers and the manual cookie/CSRF AddHeader replay (the new auto-cookies path makes them unnecessary).
- `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` — drop tests that asserted iterate-printings behavior (5-cap probe, sort ASC, mixed-age fixture). Replace with single-printing-lookup tests against the cards/named flow.

### Claude's Discretion

- Postgres dialect SQL statements (parameter prefix differences SQLite-vs-Postgres; UPSERT shape; window-expiry SQL pattern).
- Test infrastructure choice (Testcontainers.PostgreSql vs in-memory SQLite vs in-process FakeStore) — planner picks based on existing patterns in `FeedbackStoreTests` / `CategoryKnowledgeStoreTests`.
- Cloudflare CIDR list snapshot — do we hardcode it, fetch it at startup, or document it in the README and let the operator supply it?
- ScryfallTaggerService method shape after dead-code removal: keep the same overall flow (Resolve → Session → POST) or simplify into one method?
- Whether to add an integration test that exercises the full Tagger flow against a stub HTTP server — was an optional Plan 05-03 in the roadmap, planner decides whether to include.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 4 history (root causes + reverted approach)

- `.planning/phases/04-security-bug-fixes/04-ABANDONED.md` — Full post-mortem with corrective guidance. **Required reading**.
- `.planning/phases/04-security-bug-fixes/04-CONTEXT.md` — Phase 4 design decisions (helpful for understanding what was tried).
- `.planning/phases/04-security-bug-fixes/04-RESEARCH.md` — Phase 4 research domain (BUG-01 + BUG-02). Note: research was correct on BUG-02 but missed S3804 (GraphQL-layer root cause for BUG-01).

### Phase 3 latent defect

- `.planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md` — Path-B-rawpeer feedback rate-limit hardening + the Phase 04 addendum noting multi-proxy fragmentation defect.

### Project-level

- `CLAUDE.md` (project root) — Constraints: ASP.NET 10, Render Starter, public repo (no secrets), plain commit author, README updated when behavior changes, Codex MCP routing for code edits, mandatory twice-QA per Codex prompt.
- `.planning/PROJECT.md` — Core value: ChatGPT-paste-ready output in one round-trip.
- `.planning/REQUIREMENTS.md` — BUG-01, BUG-02, TD-04.
- `.planning/ROADMAP.md` — Phase 5 entry (just-updated): Goal, Pre-Phase Findings, 7 SCs, projected 3-plan structure.

### External docs (verified during pre-phase research)

- https://render.com/docs/inbound-ip-rules — Configuring inbound IP allow-lists. Required for the CF-Connecting-IP spoof prevention.
- https://www.cloudflare.com/ips-v4/ and https://www.cloudflare.com/ips-v6/ — Cloudflare's published CIDRs (the allow-list for Render Inbound IP Rules).
- https://render.com/docs/scaling — Confirms Starter+disk = single instance (no horizontal scaling).
- https://render.com/articles/how-render-handles-zero-downtime-deploys — Zero-downtime rolling cutover (relevant for verifying Postgres state survives restart).

### Code (current baseline state after revert)

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` (at `bcc1693` shape; see `4db8b8a^:` for the pre-migration working version Plan 05-01 will partially restore).
- `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` — typed client wrapper; will be modified to flip UseCookies.
- `DeckFlow.Web/Program.cs` — Tagger client registration block (~190-209 area); `DeriveFeedbackPartitionKey` (~360); `BasicAuthMiddleware` UseWhen (~297).
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` — current state has NO tracker/throttle (reverted).
- `DeckFlow.Web/Services/FeedbackStore.cs` and `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — patterns to mirror for the new `AdminBruteForceTrackerStore` (relational dialect, parameter prefix, UPSERT-style).

</canonical_refs>

<code_context>
## Reusable Assets / Patterns

**For BUG-01 fix (Plan 05-01):**
- `IScryfallTaggerHttpClient` typed wrapper exists; just flip handler config.
- Polly pipelines `tagger`, `tagger-post`, `scryfall` already configured in `ResiliencePipelineFactory` — keep as-is.
- `IMemoryCache` registered in DI — keep using.
- `ScryfallThrottle.ExecuteAsync` static gate — keep wrapping Scryfall calls.
- `IScryfallRestClientFactory` — produces RestClient for Scryfall calls.
- Pre-migration `ScryfallTaggerParsers.TryExtractCsrfToken` and `ParseOracleTagsFromJson` — both still in use, no changes needed.
- Pre-migration code at `git show 4db8b8a^:DeckFlow.Web/Services/ScryfallTaggerService.cs` — reference for the working cookie shape.

**For BUG-02 fix (Plan 05-02):**
- `RelationalDatabaseConnection` in `DeckFlow.Core/Storage/` — handles SQLite/Postgres dialect switching.
- `IRelationalDialect` with `SqliteRelationalDialect` and `PostgresRelationalDialect` — parameter prefixes, UPSERT shape, type mappings.
- `FeedbackStore` (DeckFlow.Web/Services/FeedbackStore.cs) — closest template: uses `RelationalDatabaseConnection`, defines schema with CREATE TABLE IF NOT EXISTS on first call, parameterized queries via dialect helpers.
- `CategoryKnowledgeRepository` (DeckFlow.Core/Knowledge/) — alternate template; less ASP.NET-y, more pure-domain.
- `Program.cs:354-367` — existing `DerivePeerIpKey` / `DeriveFeedbackPartitionKey` / `DeriveAdminPartitionKey` helper hierarchy. Will mostly be deleted/replaced by `DeriveCloudflareClientIp(ctx)`.
- Existing `BasicAuthMiddleware` (post-revert state, no tracker) — needs the throttle gate + RecordFailure wiring like Phase 4-01 had, but reading from Postgres-backed store instead of in-memory.
- Render dashboard (manual): Inbound IP Rules section, has "Add allow rule" UI accepting CIDR. Cloudflare publishes ~22 IPv4 + ~7 IPv6 CIDRs.

**For observability (Plan 05-01 piggyback):**
- Existing Serilog config in `Program.cs:34-47` — JSON console + daily-rolling file sink. Structured-property template syntax already used throughout.

</code_context>

<deferred>
## Deferred Ideas

- **Integration test against a stub Tagger** — listed as optional Plan 05-03 in roadmap. Discussed lightly here; not a locked decision. Planner may choose to include or skip based on test infrastructure cost vs value.
- **Distributed tracing / correlation IDs** — discussed as observability option but rejected in favor of minimal step-level logging. Could revisit if Render logs become hard to parse for concurrent requests.
- **Hybrid in-memory+Postgres write-through cache** — discussed for throttle persistence but rejected as overkill for /Admin/* low-traffic. Could revisit if perf issues observed.
- **iterate-printings as fallback** — discussed for BUG-01 but rejected (unnecessary; cards/named already returns a tagged printing). Code goes; comments don't preserve the rejected approach.
- **Ramp into multi-instance Render Pro** — out of scope for this milestone; if user upgrades, the Postgres-backed throttle is already cross-instance-safe (Plan 05-02's design accommodates this without rework).
- **Apply same observability pattern to other services (BanList, Spellbook, Scryfall)** — not in scope; would be a separate tech-debt phase.
- **Cloudflare CIDR auto-refresh** — Cloudflare publishes a small set of CIDRs that change rarely; can be hardcoded in README for now and refreshed manually if Cloudflare announces a change.

</deferred>

---

*Phase: 05-security-bug-fixes-v2*
*Context gathered: 2026-05-02 via /gsd-discuss-phase 5*
