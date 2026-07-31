---
slug: cutlab-import-scryfall-429
status: root_cause_found
trigger: "work on last bug — Cut Lab \"Import pool\" shows an on-screen error banner in production; prior session found sustained Scryfall 429 rate limiting but never confirmed the user-visible failure path"
created: 2026-07-31
updated: 2026-07-31
---

# Debug: Cut Lab "Import pool" error banner + Scryfall 429s

## Symptoms

- **Expected:** Pasting a decklist and clicking **Import pool** on `/cut-lab` loads the deck,
  resolves cards via Scryfall, and rebuilds the protected pool.
- **Actual:** An **on-screen error banner** is rendered instead. Exact wording not yet captured.
- **Errors:** Production logs show sustained Scryfall `429 Too Many Requests`. No stack traces
  surfaced yet. Separately, two `libgssapi_krb5.so.2: cannot open shared object file` entries.
- **Timeline:** First 429 in logs at **2026-07-30 ~20:00Z**. A log search across
  2026-07-28 00:00Z → 2026-07-30 20:00Z returned **zero** 429s, so this is new behavior, not
  long-standing.
- **Reproduction:** **Production only, intermittent.** Not confirmed on localhost.

## Environment

- Prod: Render service `srv-d7gmufkp3tds73a29m30`, Oregon, Docker, port 10000.
- Postgres: `dpg-d7oj8iugvqtc73fso0g0-a` (read-only access via Render MCP; never write).
- Cut Lab feature flag `tool.cut-lab.enabled` is **ON in prod** — this bug is user-facing now.

## Evidence (carried over from the 2026-07-30 session — do NOT re-derive)

- timestamp: 2026-07-31T02:53:57Z–02:56:26Z
  finding: Eight consecutive Scryfall `429` responses in a ~2.5 min window, spaced 20–30s.
  Response times 8–9ms (one 33.7ms). All logged at **INFO**, not Error.
  note: The Cut Lab controller action **completed** at 02:56:26 after ~9.6s. The import did not
  hard-fail in the logs — this is the central contradiction with the user-reported banner.

- timestamp: 2026-07-31T02:56:16Z–02:56:26Z
  finding: A single import emits **21 HTTP requests in ~10s**, alternating
  `POST /cards/collection` and `GET /cards/search`, spaced 200–300ms. A 1.7s gap at 02:56:23.909
  is consistent with a Retry-After honoring path.

- timestamp: 2026-07-30 (code read)
  finding: `DeckFlow.Web/Services/Scryfall/ScryfallThrottle.cs` — `MinInterval` 200ms (5 req/s),
  `SemaphoreSlim Gate(1,1)` serializes all Scryfall traffic process-wide, `RetryAfterCap` 30s,
  `FallbackRetryDelay` 2s. Observed 200–300ms spacing confirms the throttle is **active and
  working as designed**; it is not being bypassed.

- timestamp: 2026-07-30 (live curl)
  finding: `GET /cards/search` from a residential WSL IP returns **200** in 207ms, second call
  `cf-cache-status: HIT`. No `Retry-After` or `X-RateLimit` headers on success. Same User-Agent.
  implication: Points at **IP-based limiting of Render's shared egress IP**, not at request rate.
  caveat: Not proven — a residential-vs-datacenter comparison does not isolate IP from volume.

- timestamp: 2026-07-30 (code read)
  finding: Scryfall call sites — `cards/collection` dominates (DeckConvertService:132,
  DeckAnalysisPacketService, ManabaseAnalysisService, CardLookupService).
  `CardLookupService:300` is the **only** `cards/search` call site, reached via
  `SearchFallbackCardAsync` — i.e. every `GET /cards/search` in the burst is a **per-card
  fallback for a name that `cards/collection` failed to resolve**.
  correction (2026-07-31): This claim is **incomplete for Cut Lab**. Cut Lab is wired to
  `IScryfallCardResolver` → `ScryfallCardResolver` (DI confirmed in
  `ScryfallServiceCollectionExtensions.cs:54`), a separate class from `CardLookupService` with
  its own independent `cards/search` sites (`ScryfallCardResolver.cs:137,174`) and its own
  redundant `cards/collection` retry (`ScryfallCardResolver.cs:110`). `CardLookupService` serves
  other flows (deck lookup/convert), not Cut Lab.

- timestamp: 2026-07-30 (code read)
  finding: `ScryfallReferenceResolver` chunks batch lookups at 75 cards per `cards/collection`
  call, then loops per-miss. `CutLabAnalysisContextBuilder.cs:400` instantiates it on demand
  (not DI-injected), so it is the single choke point for all Cut Lab card resolution.

- timestamp: 2026-07-30T17:53:18Z and 2026-07-30T20:07:35Z
  finding: `libgssapi_krb5.so.2: cannot open shared object file: No such file or directory`.
  Only 2 occurrences. Likely Npgsql probing GSSAPI auth in the aspnet:10.0 container.
  status: **Probably unrelated** — must be confirmed or eliminated, not assumed.

- timestamp: 2026-07-31 (code read, session 2)
  finding: `CutLabPageService.ResolveEntriesAsync` (`CutLabPageService.cs:498-501`) is the
  **only** Cut Lab Scryfall resolution call using `failOpenOnLookupErrors: false`; every other
  Cut Lab resolution (`CutLabAnalysisContextBuilder.BuildAsync`, `CutLabAnalysisContextBuilder.cs:179`)
  uses `failOpenOnLookupErrors: true`.

- timestamp: 2026-07-31 (code read, session 2)
  finding: `ScryfallCardResolver.ResolveSingleAsync` (`ScryfallCardResolver.cs:103-126`), used as
  the batch-miss fallback delegate inside `ScryfallReferenceResolver.ResolveBatchAsync`, issues a
  **redundant second single-name `cards/collection` POST** before falling through to
  `SearchFallbackCardAsync`'s `cards/search` GET — even though the identifier already failed once
  on that exact endpoint during the initial 75-card batch call. This explains the alternating
  POST/GET burst pattern and the ~21-request volume for what should be a ~2-call import
  (~10 misses × 2 extra calls each + 1-2 batch calls ≈ 21).

- timestamp: 2026-07-31 (code read, session 2)
  finding: `SearchFallbackCardAsync` throws `HttpRequestException` on any non-2xx/non-404,
  including 429 (`ScryfallCardResolver.cs:153-156`). Nothing catches it inside
  `ScryfallReferenceResolver.ResolveBatchAsync`. `CutLabAnalysisContextBuilder.ResolveCardsAsync`
  re-throws it because `failOpenOnLookupErrors == false` for the intake path
  (`CutLabAnalysisContextBuilder.cs:432-437`). `CutLabPageService.ProcessAsync:231` catches it and
  converts it to `CutLabProcessResult.ErrorMessage` via
  `UpstreamErrorMessageBuilder.BuildScryfallMessage`, producing the literal banner text
  **"Scryfall returned HTTP 429. Try again shortly."** `CutLabController.Process()` then renders
  `View("CutLab", ...)` as a normal 200 OK — this is why prod logs show the request completing
  normally (INFO, 200, ~9.6s, no stack trace) while the user simultaneously sees an error banner.
  The failure is caught and converted to a view-model field inside a successful HTTP response; it
  was never going to appear as a server error in the logs. **The "contradiction" is resolved: it
  was never a contradiction.**

## Eliminated

- hypothesis: ScryfallThrottle is being bypassed or misconfigured, causing the burst.
  evidence: Observed 200–300ms request spacing matches `MinInterval` exactly. Throttle is working.

- hypothesis: Render's shared egress IP is throttled independently of DeckFlow's request volume
  (as the sole/primary cause).
  evidence: The redundant collection-retry-then-search fallback fully explains the 21-request
  burst size and its intermittency (miss-rate dependent) without invoking IP-specific throttling.
  Render's IP tolerating a smaller burst than a residential IP before Cloudflare intervenes is not
  ruled out as a contributing *amplifier* of an already-inflated burst, but it is not the
  proximate trigger — demoted from leading to secondary/contributing.

## Current Focus

status: Root cause found and traced end-to-end through static code (DI-confirmed). No longer
investigating; awaiting user decision on fix path (Codex is out of credits, see Constraints).

## Resolution

root_cause: >
  Two independent defects compound in Cut Lab's Import-pool intake path only:
  (1) `CutLabPageService.ResolveEntriesAsync` (`CutLabPageService.cs:498-501`) is the only Cut Lab
  Scryfall resolution call using `failOpenOnLookupErrors: false` — every other Cut Lab path is
  fail-open. (2) `ScryfallCardResolver.ResolveSingleAsync` (`ScryfallCardResolver.cs:103-126`),
  used as the post-batch-miss fallback delegate, redundantly re-issues a second single-name
  `cards/collection` POST before falling through to `cards/search` — even though the identifier
  already failed on that exact endpoint in the initial batch call. This roughly doubles live
  Scryfall call volume per miss, turning what should be a ~2-3 call import into a ~21-request
  burst for a ~100-card pool with ~10 misses, which trips Scryfall/Cloudflare rate limiting.
  `SearchFallbackCardAsync` throws `HttpRequestException` on 429 (`ScryfallCardResolver.cs:153-156`),
  uncaught in `ScryfallReferenceResolver.ResolveBatchAsync`, re-thrown by
  `CutLabAnalysisContextBuilder.ResolveCardsAsync` because of defect (1)'s fail-closed policy
  (`CutLabAnalysisContextBuilder.cs:432-437`), caught by `CutLabPageService.ProcessAsync:231` and
  converted to a view-model `ErrorMessage` ("Scryfall returned HTTP 429. Try again shortly.") via
  `UpstreamErrorMessageBuilder.BuildScryfallMessage`. `CutLabController.Process()` renders a
  normal 200 OK regardless, which is why prod logs show a completed, INFO-level, no-stack-trace
  request while the user sees an error banner — the two observations were never in conflict.
  Render's shared egress IP being more sensitive to burst size than a residential IP is not ruled
  out as a secondary amplifier but is not the proximate cause; the redundant per-miss call pattern
  fully explains the burst size and intermittency on its own.
fix: >
  Not applied this session (diagnosis-only; Codex is out of credits, user authorization required
  before any production code is written). Suggested direction, in priority order:
  1. Remove the redundant single-name `cards/collection` POST inside `ResolveSingleAsync` when
     used purely as a post-batch-miss fallback — go straight to `SearchFallbackCardAsync`. Should
     roughly halve Cut Lab's worst-case Scryfall burst volume.
  2. Reconsider the fail-closed policy in `ResolveEntriesAsync`, or at minimum treat a 429
     (transient) differently from a 404 (permanent miss) — e.g. fail-open specifically on 429,
     since the analysis-context path already tolerates missing cards gracefully.
  3. Reassess whether `ScryfallThrottle`'s `MaxRetryAttempts=2` / `FallbackRetryDelay=2s` budget is
     generous enough for Cut Lab's larger per-request fan-out of near-simultaneous per-card
     fallbacks, compared to other single-lookup callers.
verification: Not yet performed — no fix applied.
files_changed: None (diagnosis-only session).

## Orchestrator verification + correction (2026-07-31, main context)

Spot-checked every load-bearing citation against source. `ScryfallCardResolver.cs:103-126`,
`:153-156`, `CutLabPageService.cs:498-501` and `ScryfallReferenceResolver.cs:146-156` all match
the report exactly. Two additions the investigation did not surface:

**A. The miss generator is documented and deliberately preserved.**
`ScryfallReferenceResolver` remarks (`:52-61`) are labelled **"LOAD-BEARING behaviors preserved
from the three current implementations (do not 'fix')"** and item 2 states outright that
collection hits are matched back by comparing the ORIGINAL request name to the RETURNED
`card.Name` (Ordinal-IgnoreCase), so an Archidekt single-slash DFC name `"A / B"` returned as
`"A // B"` **"will NOT match its original request and falls through to the fallback strategy,
exactly as today."** Match-back is `string.Equals(name, card.Name, OrdinalIgnoreCase)` on the raw
chunk name (`:136`) — no `CardNormalizer`. Meanwhile `ScryfallCardResolver.ResolveSingleAsync`
matches with `CardNormalizer.Normalize` on BOTH sides (`:117-118`). The batch match is therefore
strictly stricter than the single match: the batch manufactures phantom misses that the fallback
then resolves. That is why fallback volume is high AND why the fallbacks mostly succeed.

**B. Cut Lab does not pass `normalizeForScryfall`, and passing it would not fix this.**
`CutLabAnalysisContextBuilder.cs:408-411` calls `ResolveBatchAsync(requestNames, fallback,
cancellationToken:)` — the flag defaults to `false` (`:100`). DeckHistory (`:272`) and
DeckAnalysis (`:1896`) pass `true`; DeckComparison (`:379`) and MetaGap (`:565`) pass `false`.
⚠ **Two xmldocs contradict each other here.** `NormalizeForScryfall`'s own summary claims it
exists "so DFC cards resolve on the first /cards/collection attempt instead of cascading into
per-card fallbacks", but `ResolveBatchAsync`'s param doc says normalization **"Never affects the
match key"** and the remarks say the normalized name still falls through to fallback. Reading the
code, the remarks are right and `NormalizeForScryfall`'s summary is wrong (or describes
pre-refactor call sites). So flipping Cut Lab to `normalizeForScryfall: true` would change what
is SUBMITTED but not what MATCHES — the phantom miss survives.

**Routing consequence:** the genuinely high-leverage fix is normalizing the match KEY on both
sides, but that is exactly what the "do not fix" comment guards, and it changes behavior for four
other services. This is therefore **not** a safe single-file, tight-spec change and must not be
routed to a local model. The subagent's fix #1 (drop the redundant `cards/collection` POST in
`ResolveSingleAsync`) remains valid, safe, and single-file — but it only halves the cost per miss;
it does not reduce the miss count.

## Specialist Review

Specialist hint from investigation: `general` (C#/ASP.NET Core service-layer control-flow defect;
no framework-specific expertise needed beyond the trace already documented above).

Attempted to route the finding through the `engineering:debug` skill per specialist-dispatch
policy. That skill is **not available as an invokable agent/skill in this environment** — the
Agent tool's registered agent list does not include an `engineering:debug` entry, and no generic
skill-invocation tool is available in this session. Specialist review was **skipped**, not
silently substituted. The root cause above is traced directly through source (file:line citations
+ DI registration confirmation), not inferred, so confidence is high despite the missing second
opinion — but this gap should be surfaced to the user rather than assumed harmless.

## Constraints for this session

- **Diagnose first.** Per project convention Claude investigates and does not author the fix.
- **Codex is OUT OF CREDITS** (re-probed 2026-07-31, `ERROR: Your workspace is out of credits`).
  A fix therefore cannot be dispatched to Codex — surface to the user and get explicit
  authorization before Claude writes any production code.
- Prod database access is **read-only**, via Render MCP only.
- Do **not** hammer the live Import pool to reproduce — retrying feeds the very rate limit under
  investigation. Prefer log analysis and local repro.
- Local UI runs must use `scripts/run-web-test.sh` (`DECKFLOW_DISABLE_AUTO_BROWSER=true`);
  ask the user before opening any browser.
