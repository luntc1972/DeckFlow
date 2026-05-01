# Phase 04: Security & Bug Fixes - Context

**Gathered:** 2026-05-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Close the two concrete bugs that prompted this milestone's quality bar — without regressing the live ChatGPT-paste, deck reconcile, and category suggestion pipelines on deckflow.gg.

In scope (from ROADMAP.md §"Phase 4: Security & Bug Fixes"):
- **BUG-02:** Per-IP rate-limit on `/Admin/*` so repeated failed basic-auth attempts from a single IP get throttled by ASP.NET Core middleware. Existing per-challenge warning log still fires; legitimate admin sessions unaffected.
- **BUG-01:** Fix the Scryfall Tagger 404 in `ScryfallTagger`-mode category suggestions so a known card (e.g. "Sol Ring") returns real Tagger data instead of a silent HTTP 200 with `[]`. Either real data or a clear graceful-fallback message — not silent empty.
- **SC #3 regression bar:** ChatGPT-paste workflow, deck reconcile (`/sync`), and category suggestion (`/suggest-categories`) keep producing the same prompt artifacts after both fixes deploy.

Out of scope: any non-`/Admin` rate-limit changes (the feedback rate-limit was already hardened in Phase 03 TD-04 / Path B-rawpeer); refactoring `BasicAuthMiddleware` beyond what BUG-02 requires; adding rate-limit to `/api/*` (separate concern); EDHREC or cached-store path in `CategorySuggestionService` (only the Tagger leg is broken); a Tagger upstream switch (sticking with `tagger.scryfall.com` GraphQL — well understood after Phase 1's CSRF rework). UI-surface changes are out of scope; SC #2 explicitly allows "real Tagger data OR fallback message" — we picked the data path so no UI copy work is needed.

</domain>

<decisions>
## Implementation Decisions

### BUG-02 — Admin throttle scope (what counts toward the bucket)

- **D-01:** Count only **401 challenges from `BasicAuthMiddleware`**, not all `/Admin/*` requests, not all 4xx responses. Successful auth (existing browser sending a valid cached `Authorization` header on subsequent requests) never increments the counter. SC #1's "legitimate admin sessions are unaffected" is structurally guaranteed, not dependent on threshold tuning.
- **D-02:** The counter lives **in-memory inside the middleware**: a static `ConcurrentDictionary<string, BucketEntry>` (planner picks whether to wrap in a small `AdminBruteForceTracker` singleton or keep it inline in `BasicAuthMiddleware`). No ASP.NET `IRateLimiter` API (the failed-only requirement breaks the standard model — limiter runs before the controller and can't condition on auth outcome). No distributed (Postgres/Redis) store — Render runs one Starter web instance and admin volume is sub-1-req/min globally.
- **D-03:** When a bucket overflows the threshold, return **HTTP 429 + `Retry-After` header** (seconds until window reset). Matches the rejection-status-code convention already used by the `/feedback` rate limiter (`Program.cs:148`). Browser/curl-friendly; basic-auth dialog reappears after window resets.
- **D-04:** The existing `_logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", ...)` (`BasicAuthMiddleware.cs:73`) **fires on every 401** including when throttled. SC #1 is explicit: "the existing warning log on each challenge still fires." Throttle does not silence per-challenge logs. Discretion: planner may add a one-shot warn on threshold-trip for forensic clarity, but the per-challenge warn must remain.

### BUG-02 — Throttle identity & limits (how many, by whom)

- **D-05:** Partition key = **TCP peer IP** via `context.Connection.RemoteIpAddress` — the same Path B-rawpeer pattern locked in Phase 03 TD-04 (`Program.cs:349-350` `DeriveFeedbackPartitionKey`). Render's edge collapses prod traffic to a single partition, which is exactly what we want here: brute-force from a single attacker (or attacker-controlled set) is the threat; legitimate admin = single human at a single IP. **Refactor opportunity (Claude's discretion during planning):** extract a shared `DerivePeerIpKey(HttpContext, string prefix)` helper used by both `DeriveFeedbackPartitionKey` ("peer:") and the new admin tracker (e.g., "admin:"). Helper lives next to existing `DeriveFeedbackPartitionKey` in `Program.cs`.
- **D-06:** **Permit = 10 failed attempts, window = 15 minutes.** Tight enough to throttle a brute-force script (10/15min ≈ 40/hr cap, useless for credential-stuffing); loose enough that a human admin who fat-fingers the password 3-4 times has headroom. Derived from "single human admin operator" assumption — not many concurrent admins.
- **D-07:** **Fixed-window** semantics. Each `BucketEntry` = `(int count, DateTimeOffset windowStart)`. First failed attempt for an IP starts a fresh 15-min window; subsequent failures within the window increment until permit hit; after `windowStart + 15 min`, the next failure resets the bucket. Same shape as `FixedWindowRateLimiter` used by `/feedback`. Sliding window / token-bucket variants rejected as overkill at this scale.
- **D-08:** **Lazy expiry on dict access** for memory hygiene. When checking an IP's bucket, drop entries whose `windowStart` is older than 15 minutes (in the same code path that increments). No background `IHostedService` sweep timer. No hard LRU cap. Active-IPs-in-last-15-min is naturally tiny (an attacker is one IP burning 10 attempts; aggregate is bounded by attack rate). Render container restart sweeps any drift.

### BUG-01 — Tagger 404 fix path (the printing-set mismatch)

- **D-09:** **Iterate-printings strategy.** Replace `ScryfallTaggerService.ResolveCardPrintingAsync` (`ScryfallTaggerService.cs:123-148`) which currently calls `/cards/named?exact=X` and returns the first printing — that printing is often Tagger-unindexed (per prior investigation: SOC = Shadows Over Innistrad Remastered for Sol Ring, DSC for Counterspell — both 404 on `tagger.scryfall.com/card/{set}/{number}`). New flow: call `/cards/search?q=!"<name>"&unique=prints` (returns all printings); for each printing in returned order, probe the Tagger card page; first **HTTP 200** wins. Then proceed to existing CSRF/cookie/GraphQL flow with that `(set, number)`. Real fix for SC #2 — Sol Ring will return real Tagger data.
- **D-10:** **Probe order = Scryfall default** (release-date descending as Scryfall returns from `/cards/search?unique=prints`). No custom set whitelist (brittle, goes stale), no `is:firstprint` filter (older printings have less Tagger coverage than recent), no oracle-id rewriting. Most-recent printings tend to be Tagger-indexed; this matches what a human would do in the Tagger UI when probing a card.
- **D-11:** **Probe ceiling = 5 printings.** If none of the first 5 most-recent printings have a Tagger page (all 404), return `[]` and `_logger.LogWarning("Tagger has no indexed printing for {CardName} after {N} probes", cardName, 5)`. Worst-case latency for a cold lookup = 1 Scryfall search + 5 Tagger HEAD/GET probes. Bounded; tolerable. Cards-with-no-Tagger-coverage are rare but real (very recent prints, alchemy-only, etc.) — the empty result + log is the honest answer.
- **D-12:** **Cache the winning tuple in `IMemoryCache` with 24hr TTL.** Cache key shape `tagger-printing:{normalized-card-name}` → `(string set, string collectorNumber)`. Steady-state lookup drops to 1 Scryfall named-call + 1 Tagger probe (the cached one). Survives within the Render container; resets on deploy/restart (~daily). 24hr TTL aligns with Scryfall's recommended cache horizon and the existing `IMemoryCache` usage in `CommanderSpellbookService` / `ScryfallSetService`. Cache **misses** also cache (negative result) for a shorter TTL (e.g., 1hr) so we don't re-iterate 5 printings on every empty-result repeat — planner decides exact negative-cache TTL, but include negative caching.

### Verification approach

- **D-13:** **BUG-02 verification** = unit test + live UAT curl loop. Unit test: `AdminBruteForceTracker` (or middleware) test in `DeckFlow.Web.Tests/Security/` — fake N+1 401s from same simulated `RemoteIpAddress` and assert middleware writes 429 with `Retry-After` and the existing warning log fired N+1 times. Live UAT after Render deploys: curl loop with bad creds against `/Admin/Feedback` (11 attempts) — expect first 10 = 401, 11th = 429 with `Retry-After`. Document in `04-HUMAN-UAT.md` (Phase 03 template). WSL VSTest unreliable per PROJECT.md so unit tests verified via `dotnet test` on Render CI or `dotnet build` clean + push-and-watch.
- **D-14:** **BUG-01 verification** = unit test + live UAT browser walk. Unit test: orchestration test on the new printings-iteration logic using `RichardSzalay.MockHttp` (already in `DeckFlow.Web.Tests`) — Scryfall search returns 3 fake printings, first 2 Tagger probes 404, 3rd returns 200 with valid Tagger HTML, assert tags returned. Live UAT: visit `https://www.deckflow.gg/suggest-categories`, select `ScryfallTagger` mode, enter "Sol Ring" — expect non-empty tag list. Document in `04-HUMAN-UAT.md`.
- **D-15:** **SC #3 regression** = manual UAT walk. After Render deploys both fixes, walk: (1) `/sync` deck reconcile with two known Moxfield decks → `DeckSync` page renders diff. (2) `/chatgpt-packets` → produces ChatGPT-paste artifact, sniff-test that header/format unchanged. (3) `/suggest-categories` mode=All for "Sol Ring" → returns cached + EDHREC + (now non-empty) Tagger. Each documented in `04-HUMAN-UAT.md` as PASS/FAIL with evidence (curl output, screenshot, or paste-into-ChatGPT round-trip success).
- **D-16:** **Plan grouping = 2 plans, one per bug.**
  - `04-01-PLAN.md` = BUG-02 (admin throttle): `BasicAuthMiddleware` hook + `AdminBruteForceTracker` (or inline dict) + `DerivePeerIpKey` shared helper extraction + 429/`Retry-After` response shape + unit test under `DeckFlow.Web.Tests/Security/`.
  - `04-02-PLAN.md` = BUG-01 (Tagger printings iteration): replace `ResolveCardPrintingAsync` with `/cards/search?prints` + 5-cap probe loop + `IMemoryCache` (`tagger-printing:` key, 24hr positive / shorter negative TTL) + unit test using `RichardSzalay.MockHttp`.
  - Plans are **independent** — can ship in either order or in parallel. Sequence recommendation: 04-01 first (security item, smaller surface), then 04-02 (touches the Tagger flow that Phase 1 reworked).

### Claude's Discretion

- **Tracker class shape:** whether to wrap the dict in a small `AdminBruteForceTracker` singleton (DI-registered, easier to unit-test in isolation) or keep it as a private static field on `BasicAuthMiddleware` itself. Planner picks based on testability vs. surface size.
- **DerivePeerIpKey extraction location:** inline as a private static helper in `Program.cs` next to the existing `DeriveFeedbackPartitionKey`, or as a new internal helper class in `DeckFlow.Web/Infrastructure/`. Planner's call.
- **Negative-cache TTL** on Tagger probe miss: 1hr is a starting suggestion; planner can refine based on how often a "no Tagger page" result is expected to flip.
- **Probe HTTP method** for the Tagger page check: `HEAD` (cheap, server-implementation-dependent — Tagger may not implement it) vs. `GET` (always works, downloads the HTML). Planner verifies via spike during research; default to `GET` if `HEAD` is uncertain.
- **Retry-After value:** seconds remaining in the current 15-min window (so retries-immediately-after-window flow naturally) vs. fixed `Retry-After: 900`. First option is more honest; planner picks.
- **Cache key normalization** for `tagger-printing:` keys: `cardName.Trim().ToLowerInvariant()` is the obvious starting rule; planner verifies it matches existing normalization conventions in `CardNormalizer` (referenced in CONVENTIONS.md).

</decisions>

<specifics>
## Specific Ideas

- "Reuse Phase 03's Path B-rawpeer pattern — `context.Connection.RemoteIpAddress` is what `DeriveFeedbackPartitionKey` already uses. Don't reinvent."
- "SC #1 wording is structural: 'legitimate admin sessions unaffected.' Counting only 401s nails that without threshold tuning."
- "SC #2 says either-or. We're picking 'real Tagger data' (the iterate-printings path) so no UI copy / view changes — keeps the surface smaller."
- "Phase 03 verification template (`03-HUMAN-UAT.md` with PASS/FAIL evidence rows + curl recipes) is the right shape for `04-HUMAN-UAT.md`. Reuse it."
- "Plain commits, no Co-Authored-By trailer." (PROJECT.md / CLAUDE.md)
- "VSTest unreliable in WSL — verification leans on `dotnet build` clean + Render CI or push-and-watch + manual UAT." (PROJECT.md)
- "Brownfield: every plan must keep deckflow.gg green; commit-per-logical-change so revert blast radius stays small."
- "Render container restart auto-clears the in-memory throttle dict — that's a feature, not a bug. Reset window on deploy is fine for an ops endpoint."

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before research/planning.**

### Roadmap and requirements
- `.planning/ROADMAP.md` §"Phase 4: Security & Bug Fixes" lines 84-95 — Goal, Depends-on, Requirements (BUG-01, BUG-02), Success Criteria 1-3.
- `.planning/REQUIREMENTS.md` §"Quality Bug Fixes (BUG)" lines 44-49 — BUG-01 and BUG-02 verbatim wording.
- `.planning/PROJECT.md` §"Constraints" — public-repo discipline, plain-author commits, README-current-with-commits, VSTest unreliable in WSL.
- `CLAUDE.md` §"Constraints" — RestSharp + direct Polly v8 HTTP layer, no framework migration, Render hosting + 512MB RAM cap.

### Phase 03 carry-forward (rate-limit precedent)
- `.planning/phases/03-tech-debt-cleanup/03-CONTEXT.md` §"TD-04: ForwardedHeadersOptions CIDR tightening" — D-11/D-12 explain why Path B-rawpeer was chosen over CIDR allowlist (Render does not publish inbound CIDR, verified at https://render.com/docs/inbound-ip-rules retrieval 2026-04-30).
- `.planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md` — TD-04 ship summary, includes the live curl spoof-resistance test recipe and result evidence (commit 70e01d2).
- `.planning/phases/03-tech-debt-cleanup/03-HUMAN-UAT.md` — template + live UAT format (PASS/FAIL rows, curl recipe, evidence). Mirror this for `04-HUMAN-UAT.md`.

### Codebase intel
- `.planning/codebase/CONCERNS.md` — original audit items; BUG-01 and BUG-02 origin context.
- `.planning/codebase/CONVENTIONS.md` — naming (`Fake*` / `Stub*` test doubles), DI conventions (factory delegate registration after Phase 03 single-ctor collapse), error handling (catch-broad-and-translate at controller boundary), test-seam pattern (now via `[InternalsVisibleTo("DeckFlow.Web.Tests")]` after Phase 03's TD-02).
- `.planning/codebase/STRUCTURE.md` — `DeckFlow.Web/Infrastructure/` (where `BasicAuthMiddleware` lives), `DeckFlow.Web.Tests/Security/` (where `ForwardedHeadersOptionsTests.cs` already exists from Phase 03; `04-01` test lands here too).
- `.planning/codebase/INTEGRATIONS.md` — upstream integration map (`tagger.scryfall.com`, `api.scryfall.com`, named HttpClient + Polly pipeline registrations).
- `.planning/codebase/TESTING.md` — `RichardSzalay.MockHttp` 7.0.0 already in `DeckFlow.Web.Tests`; `Fake*` family in `TestDoubles/`; `TestServiceFactory.cs` from Phase 03 TD-02 for any service that needs construction in tests.

### Code reference points (BUG-02)
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` lines 1-90 — entire current middleware. The `Challenge(...)` method at lines 70-76 is the single 401-emission site; throttle hook lands here.
- `DeckFlow.Web/Program.cs` lines 286-290 — `app.UseRateLimiter()` placement and the `app.UseWhen(... StartsWithSegments("/Admin"))` branch invoking `BasicAuthMiddleware`. Throttle is intra-middleware (D-01 says count only 401s) so the existing branch shape is unchanged; we don't add another `UseRateLimiter` policy.
- `DeckFlow.Web/Program.cs` lines 341-350 — `DeriveFeedbackPartitionKey` (Path B-rawpeer). The pattern to reuse / extract.
- `DeckFlow.Web/Program.cs` lines 136-149 — current `AddRateLimiter` block ("feedback-submit" policy). Reference shape only; admin throttle does NOT add a parallel policy here (D-02 says in-middleware).
- `DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs` — Phase 03 test sibling; `04-01` test lands in the same folder following the same shape.

### Code reference points (BUG-01)
- `DeckFlow.Web/Services/ScryfallTaggerService.cs` lines 73-118 — `LookupOracleTagsAsync` orchestration. `ResolveCardPrintingAsync` at lines 123-148 is the file/lines being replaced. Observation 2725 + 2966 record the bug evidence (SOC for Sol Ring, DSC for Counterspell, both 404).
- `DeckFlow.Web/Services/ScryfallTaggerService.cs` lines 155-177 — `FetchTaggerSessionAsync` does the Tagger card page GET. The new printings loop will call this (or equivalent) once per probe; planner decides whether to factor a lighter "is-this-printing-Tagger-indexed" check vs. driving the full session fetch each time.
- `DeckFlow.Web/Services/CategorySuggestionService.cs` lines 118-120 — caller of `_taggerService.LookupOracleTagsAsync`. After fix, this returns real tags for cards like Sol Ring; no caller-side change needed.
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` — named pipelines `tagger`, `tagger-post`, `scryfall`. New search call uses `scryfall` pipeline (already wired). New per-printing probes use `tagger` pipeline.
- `DeckFlow.Web/Services/CardLookupService.cs` (`ScryfallCardLookupService`) — reference for how Scryfall search calls are shaped (`/cards/search`, `unique=prints`). Pattern to copy when authoring the new lookup helper.
- `DeckFlow.Web/Services/CardNormalizer.cs` (referenced in CONVENTIONS.md) — normalization rules for the cache-key suffix. Verify that whatever `tagger-printing:{key}` uses matches the normalization other services apply, so cache-hit rate stays high.

### External (research targets for the planner)
- Scryfall API `/cards/search` parameters — `q=!"<name>"` (exact match), `unique=prints`, ordering. Researcher confirms ordering field name and default direction.
- `tagger.scryfall.com/card/{set}/{number}` — confirm HEAD support; if not, the probe must use GET. This is the discretion item D-probe-method.
- ASP.NET Core BasicAuth + custom rate-limit patterns — for the `04-01` design (planner verifies that `IRateLimiter` API truly cannot condition on auth-outcome cleanly, validating D-02).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`context.Connection.RemoteIpAddress`** — Phase 03 TD-04 confirmed Render forwards the immediate-peer (Render edge) IP cleanly; same value works for the admin throttle partition key.
- **`DeriveFeedbackPartitionKey` (`Program.cs:349-350`)** — already-shipped Path B-rawpeer helper. Either extract to a shared `DerivePeerIpKey(ctx, prefix)` or copy-the-shape; pattern is proven.
- **`BasicAuthMiddleware.Challenge(...)` (`BasicAuthMiddleware.cs:70-76`)** — existing single 401-emission site. The throttle hook is a localized add at this method; no fan-out across the pipeline.
- **`IMemoryCache`** — already DI-registered, used by `CommanderSpellbookService`, `ScryfallSetService`, `CommanderBanListService`. The Tagger printing cache piggybacks on the same registration; no new DI plumbing.
- **`RichardSzalay.MockHttp` 7.0.0** — already in `DeckFlow.Web.Tests.csproj` (used by `CommanderSpellbookServiceTests`). The BUG-01 unit test mocks Scryfall + Tagger HTTP via the existing test infrastructure.
- **`TestServiceFactory.cs`** (Phase 03 TD-02 output, `DeckFlow.Web.Tests/TestDoubles/`) — if the BUG-01 test needs to construct `ScryfallTaggerService`, route through `TestServiceFactory.CreateScryfallTaggerService(...)` per the Phase 03 single-ctor pattern.
- **`Fake*` test doubles in `DeckFlow.Web.Tests/TestDoubles/`** — `FakeScryfallRestClientFactory`, `FakeResiliencePipelineProvider` already exist; reuse for both unit tests.

### Established Patterns
- **Path B-rawpeer rate-limit identity** (locked Phase 03 TD-04): partition by `RemoteIpAddress`, accept Render edge collapse, document with a code comment citing the source. Apply same pattern to admin throttle.
- **Plain commit / commit-per-logical-change** (PROJECT.md, CLAUDE.md): each logical step (helper extract, throttle apply, unit test, README) is its own commit so revert blast radius stays small.
- **Live UAT after Render deploy** (Phase 03 pattern): build + commit + push to `main` (Render auto-deploys); wait ~17min for deploy; run UAT curl/walkthrough; document in `XX-HUMAN-UAT.md` with PASS/FAIL evidence rows.
- **Service test seams via `[InternalsVisibleTo]`** (Phase 03 TD-02): single internal ctor, test calls via `TestServiceFactory.Create*`.
- **RestSharp + direct Polly v8 named pipelines** (CLAUDE.md): all upstream HTTP — including the new Scryfall search and Tagger probe calls — go through the existing `scryfall` / `tagger` named pipelines.
- **In-memory cache with TTL for upstream lookups** (`CommanderSpellbookService`, `ScryfallSetService`): same pattern for the Tagger printing cache.

### Integration Points
- **`BasicAuthMiddleware.InvokeAsync` (`Infrastructure/BasicAuthMiddleware.cs:21-68`)** — the entry path for every `/Admin/*` request after `app.UseWhen(...)` matches. Throttle check lands at the very top (before any 401 path) and intercepts to emit 429.
- **`Program.cs:288-290`** — the `app.UseWhen(... "/Admin")` branch wiring `BasicAuthMiddleware`. Unchanged by Phase 04 (D-02 keeps the throttle inside the middleware).
- **`Program.cs:341-350`** — existing `DeriveFeedbackPartitionKey`. If we extract the shared helper, edits land here.
- **`ScryfallTaggerService.ResolveCardPrintingAsync` (`Services/ScryfallTaggerService.cs:123-148`)** — the BUG-01 surgical site. Replace; the rest of `LookupOracleTagsAsync` is unchanged (probe returns `(set, number)`, then existing CSRF/cookie/GraphQL flow runs on those values).
- **`CategorySuggestionService` line 118** — caller of `LookupOracleTagsAsync`. Unchanged after fix; just receives a non-empty list for cards that previously came back empty.
- **`DeckFlow.Web.Tests/Security/`** — directory exists from Phase 03 (`ForwardedHeadersOptionsTests.cs`); `04-01` admin-throttle test lands here.
- **`DeckFlow.Web.Tests/<Service>Tests.cs`** — `04-02` Tagger printings unit test lands as a new test class (e.g., `ScryfallTaggerServicePrintingsTests.cs`) or as new methods on existing `ScryfallTaggerServiceTests` if one exists; planner picks.

### Constraints carried from earlier phases
- **Phase 01 (Visual System Tokens):** No carry-forward — different surface (CSS).
- **Phase 02 (Layout / Hierarchy / UX Copy):** No carry-forward — different surface (Razor / CSS).
- **Phase 03 (Tech-Debt Cleanup):** Path B-rawpeer rate-limit pattern (TD-04) — REUSED for BUG-02 admin throttle. Single-ctor service pattern (TD-02) — applies if BUG-01 test needs to construct `ScryfallTaggerService` (route through `TestServiceFactory.CreateScryfallTaggerService(...)`).
- **PROJECT.md global:** RestSharp + direct Polly v8 HTTP pattern; testing leans on `dotnet build` clean + Render CI / push-and-watch; commits plain-author no Co-Authored-By; brownfield discipline (each commit must keep deckflow.gg green); README updated when behavior changes.

</code_context>

<deferred>
## Deferred Ideas

- **CI-side smoke test for SC #3** — GitHub Actions or Render post-deploy hook hits `/sync`, `/chatgpt-packets`, `/suggest-categories` and grep-asserts known tokens. Larger lift than Phase 04 should carry; capture as future polish item. Useful for any future phase that touches the prompt artifact pipeline.
- **Per-route rate-limit policy for `/api/*` JSON endpoints** — `DeckSyncApiController`, `SuggestionsApiController` are currently unthrottled. Out of Phase 04 scope (BUG-02 is admin-specific). Backlog candidate.
- **Persistent throttle store** — if DeckFlow ever runs multiple Render web instances, the in-memory dict becomes per-instance and an attacker can rotate across instances. At current scale (1 web tier), not a concern. Capture for the day we scale horizontally.
- **Tagger upstream switch / replacement** — `tagger.scryfall.com` is community-curated and intermittently flaky (CSRF rotation, missing printings). A dedicated tag source (or a snapshot of Tagger data) would remove the upstream-availability dependency. Out of milestone scope; Tagger is "best effort" by design.
- **Negative-cache TTL standardization across all upstream services** — BUG-01's "cache empty result for shorter TTL" pattern could be standardized into a small `IMemoryCache` extension method used by `CommanderSpellbookService`, `ScryfallSetService`, etc. Refactor candidate, not Phase 04 scope.

</deferred>

---

*Phase: 04-security-bug-fixes*
*Context gathered: 2026-05-01*
