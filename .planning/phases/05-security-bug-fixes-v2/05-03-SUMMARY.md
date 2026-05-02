---
phase: 05-security-bug-fixes-v2
plan: 03
subsystem: testing
tags: [tagger, integration-test, cookies, regression-guard, http-listener]

requires:
  - phase: 05-security-bug-fixes-v2
    provides: Plan 05-01 — auto-cookie ScryfallTaggerHttpClient + reduced TaggerSession + structured logging
provides:
  - In-process integration test for the full ScryfallTaggerService cookie-replay path against a localhost HttpListener stub
  - Regression guard against commit 4db8b8a's failure shape (UseCookies=false + manual cookie replay)
  - Meta-test that proves the cookie-presence assertion is meaningful (would fail if production regressed to UseCookies=false)
affects: [future-tagger-work]

tech-stack:
  added: []
  patterns:
    - HttpListener-bound localhost stub with TcpListener-grabbed free port for test isolation
    - Cross-thread cookie capture via Volatile.Write between server task and assertion thread
    - Single stub serves both Scryfall and Tagger endpoints (path-routed) — both BaseAddresses point at same _baseUrl

key-files:
  created:
    - DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs
  modified: []

key-decisions:
  - "Use HttpListener over Kestrel TestServer — simpler for single-test-class needs; no extra package; binds cleanly on WSL2."
  - "Free port acquired via TcpListener probe-and-release pattern instead of HttpListener's port=0 (which is not reliably supported across runtimes)."
  - "Stub serves both Scryfall and Tagger paths from one listener — collapses two services into one localhost target via path routing."

patterns-established:
  - "Localhost-stub pattern for verifying handler-level behavior (cookies, redirects, decompression) that MockHttp cannot reach"
  - "Meta-test alongside happy-path test — proves the assertion has discriminating power, not just a tautology"

requirements-completed:
  - BUG-01 (regression guard; primary fix landed in 05-01)

duration: 15min
completed: 2026-05-02
---

# Phase 05-03: Tagger Cookie-Replay Integration Test Summary

**Adds an in-process integration test that exercises the full Tagger flow against a real SocketsHttpHandler — closes the verification gap that let commit `4db8b8a` ship without testing the GraphQL POST leg, and would catch a regression to manual cookie replay or UseCookies=false before reaching production.**

## Performance

- **Duration:** ~15 min (1 task, 1 file, single Codex dispatch)
- **Started:** 2026-05-02T15:48 MDT (after Plan 05-02 close)
- **Completed:** 2026-05-02T15:51 MDT
- **Tasks:** 1
- **Files created:** 1

## Accomplishments

- Localhost HttpListener stub serves both Scryfall (`/cards/named*`) and Tagger (`/card/lea/161`, `/graphql`) endpoints with realistic response shapes including `Set-Cookie: _scryfall_tagger_session=test-session-cookie` on the page GET.
- Happy-path test (`RepliesWithCookieAutomatically`) asserts the GraphQL POST request the stub received contains the session cookie — proving auto-cookie replay works through the post-Phase-5 SocketsHttpHandler.CookieContainer wiring.
- Meta-test (`PostMissingCookieWhenUseCookiesFalse`) flips the handler back to `UseCookies = false` and confirms the POST arrives without a Cookie header — demonstrating the assertion would actually fail under the pre-Phase-5 broken shape (4db8b8a's failure mode). This proves the test has discriminating power.
- Test runs in 1s on WSL2 — no external network, no skip needed (HttpListener works cleanly on `http://127.0.0.1:<port>/`).

## Task Commits

1. **Task 1: ScryfallTaggerCookieReplayTests** — `a38ad90` (test)

## Files Created/Modified

- `DeckFlow.Web.Tests/Integration/ScryfallTaggerCookieReplayTests.cs` (NEW, 199 lines) — single test class with two `[Fact]` methods + IDisposable cleanup of the listener. Uses real `ScryfallTaggerHttpClient` + real `SocketsHttpHandler`; reuses existing test doubles (`FakeScryfallRestClientFactory`, `FakeResiliencePipelineProvider`) and a real `TaggerSessionCache` over an in-memory `MemoryCache`.

## Test Execution Results

```
$ dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ScryfallTaggerCookieReplayTests"
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 1 s
```

Both tests pass on WSL2. No CI/Docker fallback needed.

### Regression simulation (sanity check)

The Phase 4 abandonment regression was commit `4db8b8a`'s flip from `UseCookies = true` to `UseCookies = false` plus a manual `AddHeader("Cookie", session.CookieHeader)` replay. The meta-test (`PostMissingCookieWhenUseCookiesFalse`) **directly simulates one half of that regression** (`UseCookies = false`) and confirms the POST arrives without a cookie. If the happy-path assertion were a tautology, both tests would either both pass or both fail; instead, they assert opposite cookie states — proving the assertion is meaningful.

A future regression to either:
1. `UseCookies = false` with no manual replay, OR
2. Removing the `Set-Cookie` capture wiring on the SocketsHttpHandler (e.g., breaking the shared `CookieContainer` between handler and typed wrapper),

…would cause `RepliesWithCookieAutomatically` to fail before any code reached production.

## Deviations from Plan

None significant. One minor:

- **Plan flagged optional** ("Claude's Discretion" per CONTEXT.md). Included anyway because Phase 4 abandonment cost roughly 2 weeks of wall time on a regression a 5-second integration test would have caught. Cost-vs-value strongly favored inclusion.
- **CI/Docker fallback was specified for WSL `HttpListenerException: Access is denied` cases** — not needed; HttpListener bound cleanly on `http://127.0.0.1:<dynamic-port>/` in WSL2 via `TcpListener` probe-and-release.

## What's Unblocked

- **Future Tagger work:** any change to the cookie-handling shape (handler config, typed wrapper, service code) is now guarded by both happy-path and meta-test assertions.
- **Future integration tests** can adopt the same HttpListener stub pattern — the `GrabFreePort` + listener wiring is reusable boilerplate.

## Verification

- `dotnet build DeckFlow.sln /p:NuGetAudit=false` — clean (0 errors, 0 warnings)
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ScryfallTaggerCookieReplayTests"` — 2/2 pass in 1s
- All grep gates from Task 1 acceptance criteria met
