---
phase: 04-security-bug-fixes
status: abandoned
abandoned: 2026-05-02
revert_commit: pending
baseline_restored: bcc1693 (Phase 03 close)
---

# Phase 04 Abandoned

**Status:** ABANDONED. Both BUG-01 and BUG-02 fixes were ineffective on production despite passing static verification (13/13 must-haves) and unit tests (12 new + all existing). Code reverted to pre-Phase-04 baseline (bcc1693).

## What Happened

Plans 04-01 / 04-02 / 04-03 / 04-04 all shipped. Code committed, pushed, deployed live on Render. Live UAT exposed three concurrent issues that meant neither bug was actually fixed:

### BUG-01 (ScryfallTagger 404 / empty tags) — wrong root cause

The implemented fix in 04-02 (iterate up to 5 printings, HEAD-probe each, return first 200) addressed **page existence**. Live probing revealed:
- **All printings of cEDH staples return HEAD 200** from `tagger.scryfall.com/card/{set}/{number}` — confirming memory observation S3804 ("Bug Is at GraphQL Layer, Not Page-Existence Layer") that was made BEFORE plans were written but missed during plan synthesis.
- The actual failure is at the **GraphQL POST layer** in `QueryTaggerGraphQlAsync` (or the upstream `FetchTaggerSessionAsync` CSRF/cookie step). From an external IP, GraphQL returns 15-16 oracle tags for ALL Sol Ring printings probed (LEA through SOC). From production Render, the same flow returns empty `taggerCategoriesText`.
- Plan 04-03's "sort ASC by released_at" doubled down on the wrong-layer hypothesis. Sort is harmless but provides no fix.
- Without Render logs showing **which step** of Resolve→Session→GraphQL fails, no targeted fix is possible.

### BUG-02 (admin basic-auth brute-force) — wrong partition assumption

The implemented fix in 04-01 (in-memory `AdminBruteForceTracker` singleton keyed by `Connection.RemoteIpAddress`) assumed the peer IP would be stable per client. Live UAT 11-bursts on production returned 5×401 + 6×429 with **non-monotonic Retry-After** (251 → 252 ascending within 1-second burst), proving multiple buckets keyed by varying IPs.

Render's edge load balancer fronts the single Starter instance with multiple proxy IPs in the RFC1918 10.x.x.x range, fragmenting partition keys. Plan 04-04 attempted to fix this by adding RFC1918 `KnownIPNetworks` + `ForwardLimit=1`, but post-deploy 1/50 burst still tripped 429 — suggesting the architecture is **Cloudflare → Render → container**, not just **Render → container**, so `ForwardLimit=1` peels Render but lands on Cloudflare edge IPs (which also fan out across multiple datacenters).

### Phase 03 TD-04 carry-over defect

The same multi-proxy-IP fragmentation affects Phase 03's feedback-submit rate-limiter (`DeriveFeedbackPartitionKey`). It was load-tested with a single bucket and passed coincidentally; under multi-burst load the same fragmentation likely degrades it. This was not corrected by reverting Phase 04 — it remains a known latent defect for Phase 05.

## Why Static Verification Passed

`gsd-verifier` checked:
- Code patterns match plan specs (literals, identifier names, file shapes) — all PASS
- Unit tests compile and assert their stated behaviors — all PASS

It did NOT (and could not, by design) check:
- Whether the partition key assumption holds in the deployed environment (multi-proxy-IP fan-out)
- Whether the fix targets the actual user-observable failure layer (GraphQL vs. page existence)
- Whether ASP.NET pipeline middleware order produces the expected `RemoteIpAddress` rewrite under Cloudflare-fronted Render

The verification gap is the same as Phase 03 SC #4's: live integration behavior cannot be exercised against the WSL build, and unit tests with `TestServer` use loopback, masking real-world proxy chain effects.

## Code Reverted

Single commit `revert(04): roll back Phase 04 code — BUG-01 + BUG-02 fixes ineffective on prod`:

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` → bcc1693 (single-printing fixed lookup)
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` → bcc1693 (no tracker, no throttle)
- `DeckFlow.Web/Program.cs` → bcc1693 (no AdminBruteForceTracker DI, no RFC1918 KnownNetworks, no IMemoryCache factory closure for Tagger)
- `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` → bcc1693
- `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs` → bcc1693
- `README.md` → bcc1693 (no admin throttle blurb)
- `DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs` → DELETED
- `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` → DELETED

## Plan Documents Retained (Audit Trail)

The following stay in `.planning/phases/04-security-bug-fixes/` as historical record:

- `04-CONTEXT.md`, `04-DISCUSSION-LOG.md`, `04-RESEARCH.md`, `04-PATTERNS.md`
- `04-01-PLAN.md`, `04-01-SUMMARY.md`
- `04-02-PLAN.md`, `04-02-SUMMARY.md`
- `04-03-PLAN.md`, `04-03-SUMMARY.md`
- `04-04-PLAN.md`, `04-04-SUMMARY.md`
- `04-VERIFICATION.md` (note: VERIFIED 13/13 statically, but live UAT failed)
- `04-HUMAN-UAT.md` (records partial: live UAT failures)
- `.continue-here.md`

## What Phase 05 Must Do Differently

### For BUG-01

1. **Get Render logs** for a triggered Sol Ring `/suggest-categories` mode=ScryfallTagger call. Filter for `Tagger`, `tagger`, `GraphQL`. Identify which of {ResolveCardPrintingAsync, FetchTaggerSessionAsync, QueryTaggerGraphQlAsync} fails.
2. **Reproduce locally** with Render-equivalent egress (or run from a Render shell if available). The failure may be Render-egress-specific (User-Agent blocking, IP-based rate limiting, geographic routing).
3. **Add observability first**, fix second. The current `LogWarning` calls leave too many failure modes ambiguous — distinguish session-fetch-failed vs CSRF-empty vs cookie-build-empty vs GraphQL-403 vs GraphQL-empty in distinct log templates.
4. **Test against live Tagger before claiming fix** — unit tests with mocked HTTP cannot exercise the real CSRF+cookie+GraphQL handshake.

### For BUG-02

1. **Confirm the actual proxy chain** in front of Render: is Cloudflare configured as a proxy (orange-cloud) or just DNS (gray-cloud)? Does Render-edge sit between Cloudflare and container, or directly?
2. **Use `CF-Connecting-IP` header** if Cloudflare is in proxy mode — it's set by Cloudflare to the real client IP and not spoofable past the Cloudflare edge (assuming Render inbound IP rules gate to Cloudflare-only).
3. **Configure Render Inbound IP Rules** to block direct access (not via Cloudflare). Without this, `CF-Connecting-IP` is spoofable.
4. **Persist throttle state cross-restart** — even a single instance loses bucket on deploy, so a deployment cycle becomes a brute-force amnesty window. Postgres-backed (already available) is the simplest persistent option.
5. **Apply the same correction to Phase 03 TD-04 feedback rate limit** — same multi-proxy-IP fragmentation affects it.

### Common

- **Live UAT must precede phase-close**, not just be queued for after. The verifier's `human_needed` outcome should block phase completion until live UAT is recorded PASS.
- **Verification must include a live run on production**, not just static code audit. A 1-curl + 1-tagger-probe smoke test would have caught BUG-01 immediately and BUG-02 in 1 burst.

## Lessons (for tasks/lessons.md)

1. **Static verification ≠ live verification.** A 13/13 static PASS is necessary but not sufficient. Live exercise on the deployed environment is the gate.
2. **Memory observations made DURING research can be missed by plan synthesis.** S3804 (the GraphQL-layer root cause) was logged at 11:47a; plans were written at 12:14p-12:31p; the observation was not surfaced into the plan. Build a step that re-checks recent observations against draft plan must_haves.
3. **Multi-proxy fan-out is invisible in unit tests.** Any partition key derived from `RemoteIpAddress` should be live-exercised with multi-burst before claim.
4. **Don't rely on dashboard grace.** "Last deploy succeeded" doesn't mean "the deploy contains the change you intended" — verify behavior, not deploy status.

## Sign-Off

Phase 04 closed as ABANDONED. Both bugs deferred to Phase 05 with the corrective guidance above.
