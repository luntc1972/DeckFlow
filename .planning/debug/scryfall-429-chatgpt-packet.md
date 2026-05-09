---
slug: scryfall-429-chatgpt-packet
status: resolved
trigger: "Scryfall returning 429 when generating a ChatGPT packet"
created: 2026-05-03
updated: 2026-05-03
---

# Debug Session: scryfall-429-chatgpt-packet

## Symptoms (initial — operator-supplied)

- **Symptom:** Scryfall returns HTTP 429 (Too Many Requests) when the user attempts to generate a ChatGPT packet (deck → ChatGPT-ready prompt artifact).
- **Where:** Production deckflow.gg.
- **When first seen:** Operator hit it shortly after Phase 8 (analytics) deploy at 2026-05-03 ~21:30 UTC.
- **Reproduction:** Unknown — operator triggered it once organically; not yet confirmed deterministic.
- **Specific flow:** Unknown which packet endpoint specifically (DeckController.ChatGptPackets / ChatGptComparison / ChatGptCedhMetaGap / JudgeQuestions). Operator said "ChatGPT packet" — most likely `/Deck/ChatGptPackets` since that's the canonical name, but the debugger should confirm by reading prod logs / asking the operator if needed.

## Hypothesis Bank (to triage / falsify)

1. **H1 — ScryfallThrottle bypass:** Some code path in the packet build flow calls Scryfall WITHOUT going through `ScryfallThrottle.ExecuteAsync`. Phase 7 (harvest) and Phase 8 (analytics) both shipped recently and either could have introduced a Scryfall caller that skips the gate.
2. **H2 — Parallel burst from packet builder:** `ChatGptDeckPacketService` (or its card-lookup orchestrator) issues N parallel Scryfall calls instead of awaiting them sequentially through the gate. Even with `ScryfallThrottle`, `Task.WhenAll` over a list of `LookupAsync` calls can each contend for the semaphore but still burst together if the gate's wait policy is wrong.
3. **H3 — Polly pipeline regression:** The named `scryfall` `ResiliencePipeline<RestResponse>` (in `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`) used to retry 429 with `Retry-After` honoring; a recent change may have removed or weakened that. `ScryfallThrottle.ThrowIfUpstreamUnavailable` rethrows on 429, so if Polly doesn't retry, the user sees the upstream error.
4. **H4 — Genuine upstream rate-limit / shared-IP throttling:** Scryfall tightened limits, OR Render Starter shared egress IP is hot from another tenant. Manifest: even a single packet generation 429s because the IP-shared budget is exhausted.
5. **H5 — Phase 8 analytics indirect impact:** Analytics middleware shouldn't touch Scryfall, BUT it might be holding RAM / threads that starve Polly's retry timer, or its EnsureSchemaAsync await might be delaying the retry timer indirectly. Low probability but ruling-out worth one read.
6. **H6 — Tagger-vs-card mix-up:** ChatGPT packet builders also hit `tagger.scryfall.com` (separate handler with cookie-disabled SocketsHttpHandler). Tagger 429s are normally treated as soft errors, but a recent change may have promoted them to hard fails that bubble as Scryfall 429.

## Files to read first (high signal, low cost)

- `DeckFlow.Web/Services/ScryfallThrottle.cs` — global gate; confirm `MinInterval`, `RetryAfterCap`, `ThrowIfUpstreamUnavailable` behavior.
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` — named `scryfall` pipeline; confirm 429 is in the retry predicate AND `Retry-After` is honored.
- `DeckFlow.Web/Services/CardLookupService.cs` (`ScryfallCardLookupService.LookupAsync`) — main batched card lookup. Check whether `Chunk()` results are awaited sequentially or in parallel.
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` — packet orchestrator. Look for any direct `IRestClient.ExecuteAsync` calls bypassing `ScryfallThrottle.ExecuteAsync`. Look for `Task.WhenAll(cards.Select(...))` patterns.
- `DeckFlow.Web/Services/CardSearchService.cs`, `ScryfallSetService.cs`, `ScryfallCommanderSearchService.cs` — other Scryfall touch points; ensure they all go through the throttle.
- Recent git log on these files: `git log --oneline --since=2026-04-15 -- DeckFlow.Web/Services/Scryfall* DeckFlow.Web/Services/CardLookupService.cs DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs DeckFlow.Web/Services/ChatGptDeckPacketService.cs`

## Current Focus

- hypothesis: H3 confirmed (defective 429-recovery in ScryfallThrottle); H1, H2, H5, H6 eliminated.
- test: Mapped every Scryfall call site; verified each routes through ScryfallThrottle.ExecuteAsync; inspected Polly scryfall pipeline; inspected throttle Retry-After parser & retry budget.
- expecting: Found weak 429 recovery in ScryfallThrottle (no header → immediate fail; HTTP-date format → unparseable; only one retry).
- next_action: Fix applied — strengthen ScryfallThrottle retry policy. See Resolution.
- reasoning_checkpoint: All Scryfall callers correctly wrap ScryfallThrottle.ExecuteAsync. No parallel burst exists. Polly pipeline 429 delegation to throttle is intentional. The throttle itself is the only weak link.
- tdd_checkpoint: (n/a — diagnosis-then-fix, no failing test in repo to anchor on)

## Evidence

- timestamp: 2026-05-03T22:08Z — All 7 Scryfall-touching services confirmed wrapping their delegate creation in `ScryfallThrottle.ExecuteAsync(token => pipeline.ExecuteAsync(...), cancellationToken)`. H1 eliminated.
- timestamp: 2026-05-03T22:09Z — `LookupMechanicReferencesAsync` (line 1600-1614 of ChatGptDeckPacketService) does `Task.WhenAll` BUT it calls `IMechanicLookupService` which is `WotcMechanicLookupService` hitting `magic.wizards.com`, not Scryfall. H2 candidate from prior turn was misdirected; eliminated.
- timestamp: 2026-05-03T22:10Z — `LookupCardReferencesAsync` (line 1410-1494) iterates chunks via sequential `foreach`. `SearchFallbackCardAsync` is sequential. `ScryfallSetService.FetchCardsForSetAsync` paginates in a sequential `while` loop. `CardLookupService.LookupAsync` chunks sequentially. No `Task.WhenAll` over Scryfall calls anywhere.
- timestamp: 2026-05-03T22:10Z — Only parallelism in packet flow: line 426 `await Task.WhenAll(bannedCardsTask, setPacketTask)`. `bannedCardsTask` hits mtgcommander.net (separate `commander-banlist` pipeline). Only `setPacketTask` touches Scryfall, so global throttle still serializes correctly.
- timestamp: 2026-05-03T22:11Z — Polly `scryfall` pipeline (ResiliencePipelineFactory.cs line 122-138): retry predicate is `r.StatusCode >= HttpStatusCode.InternalServerError` (5xx ONLY, NOT 429). Outermost TotalTimeout(30s), MaxRetryAttempts=2, Exponential+Jitter. By design — 429 handling is delegated to ScryfallThrottle (per comment line 119, observation 4425).
- timestamp: 2026-05-03T22:12Z — ScryfallThrottle.ExecuteAsync (line 35-49) executes once, on 429 reads Retry-After. **Bug #1**: if `retryAfter is null` returns the 429 immediately, no fallback wait. **Bug #2**: `ReadRetryAfter` uses `int.TryParse(raw, out var seconds)` — only handles delta-seconds form. RFC 7231 also allows HTTP-date; Cloudflare and Scryfall sometimes send that format. **Bug #3**: only one retry attempt — second 429 surfaces to caller.
- timestamp: 2026-05-03T22:12Z — `ScryfallThrottle.ThrowIfUpstreamUnavailable` (line 111-121) rethrows 429 as `HttpRequestException`. Call sites (e.g. ChatGptDeckPacketService.cs:1517,1535) invoke it after `_executeSearchAsync`/`_executeNamedAsync`, so the throttle's failed-recovery 429 surfaces to the user as "Scryfall returned HTTP 429".

## Eliminated

- H1 (throttle bypass): All 7 Scryfall-touching services wrap calls in `ScryfallThrottle.ExecuteAsync`. Phase 7 (Harvest) and Phase 8 (Analytics) do not call Scryfall.
- H2 (parallel burst): No `Task.WhenAll` over Scryfall calls. The `WotcMechanicLookupService` parallel batch hits wizards.com, not Scryfall. Only orthogonal parallelism is banlist (mtgcommander.net) + setPacket (Scryfall) which is one Scryfall caller at a time.
- H4 (genuine upstream): Possible amplifier but not the root cause; the throttle's 429 recovery should mask brief upstream throttle/burst events and currently fails to do so.
- H5 (Phase 8 analytics): Middleware is fire-and-forget around BoundedChannel. Does not touch Scryfall and does not block request threads in any way that would starve Polly timers.
- H6 (Tagger mix-up): Tagger pipelines (`tagger`, `tagger-post`) are separate. Operator-described error is the Scryfall 429 from `api.scryfall.com`.

## Resolution

- root_cause: ScryfallThrottle 429-recovery is too brittle — three compounding defects: (1) when Scryfall/Cloudflare returns 429 without a `Retry-After` header, the throttle gives up immediately instead of using a fallback delay; (2) the `Retry-After` parser only handles delta-seconds (`int.TryParse`) and silently fails on HTTP-date format which Cloudflare sometimes emits; (3) the throttle retries at most once, so a second 429 inside the burst window surfaces to the user. With Polly's `scryfall` pipeline intentionally not retrying 429 (delegating to the throttle), any of these three failure modes drops a 429 straight back to the call site, which throws via `ScryfallThrottle.ThrowIfUpstreamUnavailable` and the user sees "Scryfall returned HTTP 429" mid-packet build.
- fix: Hardened `ScryfallThrottle` 429 recovery in `DeckFlow.Web/Services/ScryfallThrottle.cs`: added 2-second `FallbackRetryDelay` used when `Retry-After` is missing/unparseable; extended `ParseRetryAfter` to accept BOTH delta-seconds AND HTTP-date forms (RFC 7231); raised the per-call retry budget from 1 to 2 (`MaxRetryAttempts = 2`) bounded by `RetryAfterCap` so worst-case wall time stays under ~60s. Both generic and non-generic `ExecuteAsync<T>` paths share the new `ResolveRetryDelay` helper for consistent behavior. No other files touched.
- verification: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors). All Scryfall callers continue to use the same `ScryfallThrottle.ExecuteAsync` signature; no call-site changes required.
- files_changed: DeckFlow.Web/Services/ScryfallThrottle.cs

## Followup — operator log evidence (2026-05-03 22:09 UTC)

Operator pasted production log of one packet generation. Reading chronologically:

- 22:09:40 — POST /cards/collection (batch, 234ms; banlist+set fetch concurrent on separate pipeline)
- 22:09:40 → 22:09:46 — ~16 GET /cards/search?* + 1 GET /cards/named?* (mix of 200 and 404, each request ~11-15ms once fired)
- 22:09:44 — one GET /cards/search?* with End=3540ms duration — that is `ScryfallThrottle.MinInterval` PACING the next request (the gate is working)

Implication: ScryfallThrottle is serializing correctly. After the initial pacing wait, sustained ~5-6 Scryfall calls/sec for ~6 seconds. The 429 fix (21f2481) handles transient single 429s with fallback delay + HTTP-date parsing + second retry — that part is correct.

But the load shape is the deeper issue: one packet build = 1 batch `/cards/collection` + N individual `/cards/search` + M `/cards/named` fallbacks. For a typical 100-card deck where ~30 names don't exact-match, that's 30+ extra Scryfall calls per packet build. At 5 req/s sustained, 6-10s of locked Scryfall traffic per packet. On Render Starter (shared egress IP), Scryfall's per-IP enforcement counts other-tenant traffic against us, so brief 429 bursts are likely.

H4 (genuine upstream / shared-IP) reclassified from "amplifier" to "structural amplifier requiring product-level decision." The throttle hardening is the right local fix; reducing search-fallback volume is the next structural lever (but is product/perf scope, not a debug deliverable).

### Followup options for operator (not in this fix)

1. **Cache /cards/search results** in `IMemoryCache` keyed by query → most replays of the same deck become free. Highest leverage, lowest risk.
2. **Lower MinInterval to ~150ms** (~6.7 req/s) — within Scryfall's published limit, shaves 25% off packet build time.
3. **Move to Render Standard or Pro tier** for dedicated egress IP — eliminates shared-IP amplification.
4. **Accept and monitor** — 21f2481 may be sufficient; let prod soak then reassess.
