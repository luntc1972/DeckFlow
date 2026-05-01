# Phase 04: Security & Bug Fixes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-01
**Phase:** 04-security-bug-fixes
**Areas discussed:** Admin throttle scope, Throttle identity & limits, Tagger 404 fix path, Verification approach

---

## Admin throttle scope

| Option | Description | Selected |
|--------|-------------|----------|
| Only 401 challenges | Hook `BasicAuthMiddleware`: increment partition only when `Challenge()` fires. Successful auth never counts — SC #1 'legitimate sessions unaffected' is structurally guaranteed. | ✓ |
| All /Admin requests pre-auth | Standard ASP.NET RateLimiter policy on /Admin/* branch like /feedback. Risk: legit admin browsing burns the bucket. | |
| Only failed responses (401/403/4xx) | Throttle by response status; requires response-side middleware. Awkward fit. | |

**User's choice:** Only 401 challenges
**Notes:** Cleanest semantics — SC #1 met without threshold tuning.

---

| Option | Description | Selected |
|--------|-------------|----------|
| In-memory dict in middleware | Static `ConcurrentDictionary` inside `BasicAuthMiddleware` (or `AdminBruteForceTracker` singleton). Self-contained; Render restart auto-clears. | ✓ |
| ASP.NET RateLimiter API | Use `AddPolicy("admin-auth")` + `EnableRateLimiting`. Failed-attempt-only requirement breaks the standard model. | |
| Distributed (Postgres/Redis) | Multiple Render instances share state. Overkill at DeckFlow scale (1 web tier). | |

**User's choice:** In-memory dict in middleware
**Notes:** No DI plumbing for `IRateLimiter`; throttle stays local to where the 401 emits.

---

| Option | Description | Selected |
|--------|-------------|----------|
| 429 + Retry-After | Standard semantics, browser/curl-friendly, matches existing /feedback rate-limit response. | ✓ |
| 401 (silent) | Browser keeps prompting indefinitely — bad UX for fat-finger user. | |
| 503 Service Unavailable | Misleading semantics; service is up, IP is throttled. | |

**User's choice:** 429 + Retry-After

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, every challenge | SC #1 explicit: 'each challenge' log fires. Keeps brute-force-in-flight visibility. | ✓ |
| Only on 429 trigger | Quieter but loses ramp-up visibility; arguably violates SC. | |
| Log challenges + add separate warn on throttle | Two log events; best forensics, tiny extra code. | |

**User's choice:** Yes, every challenge
**Notes:** SC #1 wording is binding.

---

## Throttle identity & limits

| Option | Description | Selected |
|--------|-------------|----------|
| TCP peer IP (Path B-rawpeer) | `context.Connection.RemoteIpAddress` — same shape as Phase 03's `DeriveFeedbackPartitionKey`. Render edge collapses prod traffic to one partition. | ✓ |
| X-Forwarded-For first hop | Phase 03 explicitly rejected this for /feedback because Render does not publish CIDR. Spoofable. | |
| User-Agent + IP composite | Marginal benefit; attacker rotates UA cheaply. | |

**User's choice:** TCP peer IP (Path B-rawpeer)
**Notes:** Refactor opportunity — extract `DerivePeerIpKey(HttpContext, string prefix)` helper.

---

| Option | Description | Selected |
|--------|-------------|----------|
| 10 / 15 min | Tight enough to throttle a script; loose enough for human retries. | ✓ |
| 5 / 1 hr (mirror feedback) | Identical shape but locks out forgotten-password retries. | |
| 30 / 5 min | Looser, matches some upstream best practices. | |
| 20 / 10 min | Middle ground. | |

**User's choice:** 10 / 15 min

---

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed window | `windowStart` + `count`, reset on first attempt after expiry. Matches `FixedWindowRateLimiter` used by /feedback. | ✓ |
| Sliding window | Smoother at boundaries; more memory + compute. | |
| Token bucket / leaky bucket | Continuous refill; overkill. | |

**User's choice:** Fixed window

---

| Option | Description | Selected |
|--------|-------------|----------|
| Lazy expiry on access | Drop expired entries when an IP's bucket is checked. No background timer. | ✓ |
| Background sweep timer | `IHostedService` removes expired entries. DI plumbing for one purpose. | |
| Hard cap (LRU) | Cap dict at e.g. 1000 entries; eviction belt-and-suspenders. | |

**User's choice:** Lazy expiry on access

---

## Tagger 404 fix path

| Option | Description | Selected |
|--------|-------------|----------|
| Iterate printings until 200 | Replace `/cards/named?exact=X` with `/cards/search?prints`; probe each printing; first 200 wins. Real fix; cache the winning tuple. | ✓ |
| Graceful fallback only | Keep `/cards/named`, surface UI 'Tagger had no data' message. SC met but Sol Ring still empty. | |
| Hybrid: try named, fall back to printings on 404 | Best of both, slightly more code. | |

**User's choice:** Iterate printings until 200
**Notes:** SC #2 'returns real Tagger data for Sol Ring' satisfied — we picked the data path so no UI copy work needed.

---

| Option | Description | Selected |
|--------|-------------|----------|
| Scryfall default order | `/cards/search?unique=prints` returns release-date-desc; take first N. Matches what a human does in Tagger UI. | ✓ |
| Prefer 'older' / canonical printings | `is:firstprint` or asc by released_at. Old printings have less Tagger coverage. | |
| Try /cards/named first, then printings on 404 | Hybrid — already rejected in prior question. | |
| Custom curated set whitelist | Brittle; goes stale. | |

**User's choice:** Scryfall default order

---

| Option | Description | Selected |
|--------|-------------|----------|
| First 5 printings | Cap at 5; if all 404, return [] + log warn. Worst case 1 search + 5 probes. | ✓ |
| First 3 | Tighter latency; risks missing hits. | |
| First 10 | More tolerant but doubles worst-case latency. | |
| All printings (no cap) | 30+ printings possible; latency + Scryfall load risk. | |

**User's choice:** First 5 printings

---

| Option | Description | Selected |
|--------|-------------|----------|
| IMemoryCache 24hr TTL | Key `tagger-printing:{normalized-card-name}` → `(set, number)`. Resets on Render restart (~daily). | ✓ |
| IMemoryCache, never expire | Cache forever in-process; container restart invalidates. Marginal difference vs 24hr TTL. | |
| No cache — probe every time | Cleanest correctness; worst latency. | |
| Persistent cache (SQLite/Postgres) | Survives deploys; overkill. | |

**User's choice:** IMemoryCache 24hr TTL
**Notes:** Negative-cache TTL on miss (e.g., 1hr) included so we don't re-iterate 5 printings on every empty repeat.

---

## Verification approach

| Option | Description | Selected |
|--------|-------------|----------|
| Unit test + live UAT curl | Unit test on tracker; live-Render-independent. Live UAT: curl loop with bad creds against /Admin/Feedback (10x → 401, 11th → 429). | ✓ |
| Live UAT only | Skip unit test; rely entirely on prod testing. Risk: regression sneaks past. | |
| Integration test in DeckFlow.Web.Tests | `WebApplicationFactory` test boots middleware pipeline. WSL VSTest unreliable. | |

**User's choice:** Unit test + live UAT curl

---

| Option | Description | Selected |
|--------|-------------|----------|
| Unit test + live UAT browser walk | Unit test with `MockHttp`: 3 fake printings, first 2 Tagger 404, 3rd 200. Live UAT: deckflow.gg/suggest-categories ScryfallTagger mode + Sol Ring → non-empty. | ✓ |
| Live UAT only | Cheap; brittle to regression. | |
| Integration test against real Scryfall + Tagger | Network-dependent; hits upstream rate limits; can't run in CI without secrets. | |

**User's choice:** Unit test + live UAT browser walk

---

| Option | Description | Selected |
|--------|-------------|----------|
| Manual UAT walk | After deploy, walk /sync, /chatgpt-packets, /suggest-categories All-mode for Sol Ring. PASS/FAIL with evidence. | ✓ |
| Build-clean gate only | `dotnet build` clean = SC met. Too weak; behavioral check necessary. | |
| Add CI smoke test | GitHub Actions or post-deploy hook. Bigger lift; out of Phase 04 scope. | |

**User's choice:** Manual UAT walk
**Notes:** 'Add CI smoke test' captured as deferred idea.

---

| Option | Description | Selected |
|--------|-------------|----------|
| 2 plans (one per bug) | 04-01 = BUG-02 admin throttle. 04-02 = BUG-01 Tagger fix. Independent; mirror Phase 03's per-requirement grouping. | ✓ |
| 3 plans (split BUG-02 prep + apply) | Add 04-00 = extract `DerivePeerIpKey` helper. Cleanest dependency tree; smallest blast radius. | |
| 1 plan | Combined; harder to revert one without the other. | |

**User's choice:** 2 plans (one per bug)
**Notes:** `DerivePeerIpKey` extraction folded into 04-01 as Claude's discretion (not its own plan).

---

## Claude's Discretion

- **Tracker class shape:** wrap dict in `AdminBruteForceTracker` singleton (DI-registered, easier to unit-test) vs. private static field on `BasicAuthMiddleware`. Planner picks based on testability vs. surface size.
- **DerivePeerIpKey extraction location:** inline as private static helper in `Program.cs` next to `DeriveFeedbackPartitionKey`, or as new internal helper class in `DeckFlow.Web/Infrastructure/`. Planner's call.
- **Negative-cache TTL** on Tagger probe miss: 1hr starting suggestion; planner can refine.
- **Probe HTTP method:** `HEAD` (cheap, server-implementation-dependent) vs. `GET` (always works). Planner verifies via spike during research; default to `GET` if `HEAD` is uncertain.
- **Retry-After value:** seconds remaining in current 15-min window vs. fixed `Retry-After: 900`. First option more honest.
- **Cache key normalization** for `tagger-printing:` keys — verify against existing `CardNormalizer` rules.

## Deferred Ideas

- CI-side smoke test for SC #3 (GitHub Actions or Render post-deploy hook hits 5 endpoints + grep-asserts known tokens). Out of Phase 04 scope.
- Per-route rate-limit policy for `/api/*` JSON endpoints (`DeckSyncApiController`, `SuggestionsApiController` currently unthrottled). Backlog candidate.
- Persistent throttle store — irrelevant at current single-Render-instance scale; capture for horizontal-scale day.
- Tagger upstream switch / replacement (community-curated, intermittently flaky). Out of milestone scope; Tagger is "best effort" by design.
- Negative-cache TTL standardization across all upstream services — could extract a small `IMemoryCache` extension method used by `CommanderSpellbookService`, `ScryfallSetService`, etc. Refactor candidate.
