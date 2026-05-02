---
phase: 04-security-bug-fixes
plan: 01
subsystem: security
tags: [rate-limit, brute-force, basic-auth, admin, middleware]

requires:
  - phase: 03-tech-debt-cleanup
    provides: "Path B-rawpeer partition-key pattern (TD-04) — peer IP read directly from Connection.RemoteIpAddress, never X-Forwarded-For. Reused here as DerivePeerIpKey shared helper."
provides:
  - "AdminBruteForceTracker singleton — in-memory ConcurrentDictionary<string, BucketEntry>, 10 attempts / 15-minute fixed window per peer IP, lazy expiry on access."
  - "IAdminBruteForceTracker interface — IsThrottled(key, now) returns (Throttled, RetryAfterSeconds); RecordFailure(key, now) increments or resets bucket atomically."
  - "BasicAuthMiddleware throttle gate — 11th failed admin auth from one IP returns HTTP 429 + Retry-After; 401 challenge log preserved verbatim; successful auth never increments."
  - "DerivePeerIpKey shared helper extracted in Program.cs — used by both feedback rate limiter (peer:) and admin throttle (admin:) prefixes."
  - "AdminBruteForceTrackerTests — 5 unit tests + 2 middleware integration tests (xunit, NullLogger, DefaultHttpContext)."
  - "README admin throttle blurb — operations note covering 429 + Retry-After behavior."
affects: [phase-04-02, phase-05-anything-touching-admin-routes]

tech-stack:
  added: []
  patterns:
    - "Per-IP fixed-window throttle inside middleware boundary (D-02) — IRateLimiter cannot condition on auth outcome, so the gate lives in BasicAuthMiddleware itself."
    - "Lazy bucket expiry via ConcurrentDictionary.TryRemove on stale read (D-08) — no IHostedService timer, no LRU cap; bounded by active-IPs-in-15min."
    - "Shared partition-key helper pattern — DerivePeerIpKey(ctx, prefix) reused across feedback limiter and admin throttle."

key-files:
  created:
    - DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs
    - DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs
  modified:
    - DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs
    - README.md

key-decisions:
  - "D-01: Counter only increments on Challenge-emitted 401s — env-var-missing 503 path bypasses, success path never increments."
  - "D-02: In-middleware ConcurrentDictionary, NOT ASP.NET IRateLimiter (limiter cannot read auth outcome)."
  - "D-03: 429 response carries Retry-After header (seconds remaining in window, clamped ≥1)."
  - "D-04: Existing _logger.LogWarning('Admin basic-auth challenge issued: ...') line preserved VERBATIM."
  - "D-05: Partition key uses Connection.RemoteIpAddress (Path B-rawpeer, locked by Phase 03 TD-04) — forwarded headers cannot rotate buckets."
  - "D-06: PermitLimit = 10 attempts, Window = 15 minutes."
  - "D-07: BucketEntry(int Count, DateTimeOffset WindowStart); fixed-window semantics."
  - "D-08: Lazy expiry on dict access via TryRemove; no timer, no LRU."

patterns-established:
  - "Throttle-gate-then-auth ordering: gate runs at top of InvokeAsync BEFORE env-var check, so 503 path doesn't increment and successful auth still has counter context."
  - "RecordFailure scoped to Challenge() call site only — single emission point makes counter semantics auditable."
  - "EnvScope IDisposable test helper kept as local copy in security tests (not promoted) — Phase 03 precedent stands."

requirements-completed: [BUG-02]

duration: 25min
completed: 2026-05-01
---

# Phase 04 Plan 01: Admin Brute-Force Throttle Summary

**Per-IP brute-force protection on /Admin/* basic-auth — 11th failed attempt returns HTTP 429 + Retry-After while the existing per-challenge warn log keeps firing, closing BUG-02.**

## What Was Built

`AdminBruteForceTracker` is a sealed singleton wrapping `ConcurrentDictionary<string, BucketEntry>`. Each peer IP gets a fixed 15-minute window starting on first failure; attempts increment atomically via `AddOrUpdate`; reads lazy-expire stale buckets via `TryRemove`. `IsThrottled` returns `(true, retryAfterSeconds)` once Count ≥ 10 within the window, otherwise `(false, 0)`.

`BasicAuthMiddleware` was extended with an `IAdminBruteForceTracker` constructor dependency. `InvokeAsync` now opens with the throttle gate — partition key is `"admin:" + Connection.RemoteIpAddress` (Path B-rawpeer per Phase 03 TD-04, forwarded headers ignored), and a throttled request returns 429 with `Retry-After` and a fresh warn log without ever calling `Challenge` (so no spurious 401 or `WWW-Authenticate` header on the throttled response — Pitfall 3 invariant). `Challenge` itself preserves the existing `_logger.LogWarning("Admin basic-auth challenge issued: ...")` byte-for-byte (D-04) and ends with a single `_tracker.RecordFailure(...)` call so only Challenge-emitted 401s feed the counter (D-01: env-var-missing 503 path doesn't increment, success path never reaches Challenge).

`Program.cs` registers the tracker as a DI singleton next to `TaggerSessionCache` and extracts a shared `DerivePeerIpKey(ctx, prefix)` helper. Existing `DeriveFeedbackPartitionKey` now delegates to it, and a new `DeriveAdminPartitionKey` mirrors the pattern for future use.

## Tests

`DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` — 7 xunit tests:

| # | Method | Asserts |
|---|--------|---------|
| 1 | RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled | 10 failures → IsThrottled = true, retryAfter ∈ [1, 900] |
| 2 | IsThrottled_NinthFailure_StillNotThrottled | 9 failures → IsThrottled = false |
| 3 | IsThrottled_DifferentKeys_DoNotInterfere | 10.0.0.1 throttled, 10.0.0.2 not |
| 4 | RecordFailure_AfterWindowExpiry_ResetsBucket | t0+16min reset bucket, no longer throttled |
| 5 | IsThrottled_ReturnsRemainingSecondsInWindow | retryAfter ≈ 600s at t0+5min, ±1s tolerance |
| 6 | BasicAuthMiddleware_ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429 | full 11-attempt sequence: 10×401, 11th=429 with Retry-After, no WWW-Authenticate |
| 7 | BasicAuthMiddleware_SuccessfulAuthDoesNotCountTowardThrottle | 9 wrong + 1 right + 1 wrong → 11th still 401 (success bypassed counter) |

Existing `BasicAuthMiddlewareTests.cs` updated to pass the new ctor arg (real `AdminBruteForceTracker()`, not a stub — each test stays under the throttle threshold).

## Commits

| SHA | Subject |
|-----|---------|
| 7b3c1d6 | feat(04-01): add AdminBruteForceTracker singleton + extract DerivePeerIpKey helper (BUG-02) |
| 50849e9 | feat(04-01): wire AdminBruteForceTracker into BasicAuthMiddleware (BUG-02) |
| 7e08d8c | test(04-01): cover AdminBruteForceTracker + middleware throttle integration |
| aed9ead | docs(04-01): note admin throttle in README (BUG-02) |

## Build / Verification

`dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false` — clean, 0 warnings, 0 errors. Default parallel solution build path failed in this WSL/MSBuild environment with no compiler errors emitted; serialized path succeeded. (Same workaround applied across all 3 build runs in this plan.)

VSTest is unreliable in WSL per PROJECT.md, so the automated gate is `dotnet build` clean + the 7 new xunit tests are committed and ready for CI / push-and-watch on Render.

## Outstanding — Live UAT (Plan Task 4)

`04-HUMAN-UAT.md` will record the live curl loop after this commit batch reaches `main` and Render auto-deploys. Recipe:

```bash
for i in $(seq 1 11); do
  curl -sS -o /dev/null \
    -w "attempt=%{http_code} retry=%header{retry-after}\n" \
    -u admin:WRONGPASSWORD \
    https://www.deckflow.gg/Admin/Feedback
done
```

Expected: attempts 1–10 = 401, attempt 11 = 429 with non-empty `retry=` value. SC #1 of Phase 04 closes only when this PASS is recorded; document combined with Plan 04-02's HUMAN-UAT entries.

## Deviations

- Plan called for one `dotnet build DeckFlow.sln` invocation per task; environment forced `-m:1 -p:BuildInParallel=false` workaround on every run. No code or behavioral deviation; build correctness identical.
- No other deviations.
