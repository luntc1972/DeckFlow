# Phase 98: Card-Grounding Guard - Research

**Researched:** 2026-07-18
**Domain:** Scryfall-backed card validation / anti-hallucination guard (C#, ASP.NET Core Web host + pure-Core decision logic)
**Confidence:** HIGH (existing codebase substrate verified by direct source read; Scryfall fuzzy-404 error-body semantics AND `legalities`/`color_identity` JSON shape verified live against api.scryfall.com 2026-07-18 — see Pitfall 1 for captured bodies; discriminator is `type: "ambiguous"`, not `code`)

## Summary

Phase 98 does not need new HTTP plumbing. Every Scryfall access primitive the guard requires already
exists and is already throttled/resilient: `IScryfallCardResolver` (fuzzy `/cards/named`, `cards/search`,
batched `cards/collection`) wrapped in `ScryfallThrottle.ExecuteAsync` and the named `scryfall` Polly
pipeline. The work of this phase is almost entirely **new decision logic** — a strict-reject wrapper
around the existing resolver, a `legalities` DTO addition, a pure Core-side legality/identity/singleton/pip
checker, an `IMemoryCache`-backed verdict cache mirroring `ScryfallCardNameGrounder`'s proven 24h/1h TTL
pattern, and a whitelist builder that reads the already-cached P95 creator deck corpus
(`ICreatorDeckCacheStore`) with zero additional Scryfall traffic for pool assembly.

The one real external unknown — Scryfall's `/cards/named?fuzzy=` **ambiguous-vs-not-found** distinction —
was **resolved live 2026-07-18** (orchestrator curl, both bodies captured in Pitfall 1): both conditions
return HTTP 404 with `"code": "not_found"`; the ambiguous case is distinguished ONLY by an extra
`"type": "ambiguous"` field in the error body. This codebase does not yet parse that Error object
anywhere — every existing caller (`ScryfallCardResolver`, `CardLookupService`,
`ScryfallTaggerLookupService`) only checks the status code range and treats any non-2xx as a miss. CS-25's
`Ambiguous` vs `NotFound` reject-reason split (D-13) requires a **net-new** `ScryfallErrorResponse` DTO
(with nullable `type`) parse step that does not exist in any current caller — flag this for the planner as
new code, not a copy-paste of an existing pattern.

**Primary recommendation:** Build `ICardGroundingGuard`/`CardGroundingGuard` as a thin strict-mode
composition over the *existing* `IScryfallCardResolver`, add a `Legalities` field to `ScryfallCard`, add a
new `ScryfallErrorResponse` DTO to distinguish `ambiguous` from `not_found` on 404, write the
legality/identity/singleton/pip decision logic as static, pure, Core-side functions operating only on
already-fetched `ScryfallCard` fields (no new Scryfall calls, no dependency on `ManabaseAnalyzer`/
`CastabilitySimulator`), and build the whitelist purely from `ICreatorDeckCacheStore.GetByCreatorAsync`
(already-cached P95 corpus, zero new Scryfall traffic for pool assembly — validation of pool candidates
still goes through the guard once, on demand).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Strict fuzzy-name validation (CS-21) | API / Backend (`DeckFlow.Web/Services/Scryfall/`) | — | Needs `IScryfallCardResolver` + `ScryfallThrottle`, both Web-hosted; mirrors P96 grounder split (D-02) |
| Verdict caching (D-14) | API / Backend | — | `IMemoryCache` is a Web-host singleton; guard is itself a Web-host singleton like the grounder |
| Legality / color-identity / singleton / pip decision rules (CS-23) | Database/Storage-agnostic pure logic, physically in `DeckFlow.Core` | — | No HTTP, deterministic, unit-testable without a Web host — same split P96/P97 established |
| Whitelist pool assembly (CS-22, D-05/D-06/D-07) | API / Backend (`DeckFlow.Web/Services/CreatorStyle/` or new `Services/Scryfall/`) | Database/Storage (`ICreatorDeckCacheStore`) | Reads the already-crawled P95 corpus; ranking/capping is pure logic but the read is Web-hosted (store access) |
| Whitelist packet embedding + instruction pairing (D-08) | API / Backend (P99 `CreatorStylePacketService`, out of this phase's scope) | — | Phase 98 emits the ranked candidate list; P99 owns packet text assembly |
| Fail-closed / 503 escalation (D-12) | API / Backend | — | Reuses `UpstreamErrorMessageBuilder`, a Web-host static helper |

## Standard Stack

### Core
No new external packages. This phase composes exclusively existing in-repo services.

| Component | Version/Location | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `RestSharp` | 114.0.0 (already referenced) | HTTP calls to Scryfall via `IScryfallCardResolver` | Project-wide HTTP convention (CLAUDE.md: "do NOT migrate to standard handler") |
| `Polly` v8 | 8.x (already referenced) | Named `scryfall` resilience pipeline | Already wraps every Scryfall call site |
| `Microsoft.Extensions.Caching.Memory` | built-in (already referenced) | Verdict cache | Mirrors `ScryfallCardNameGrounder`'s existing 24h/1h TTL pattern `[VERIFIED: codebase]` |

### Supporting
None — no new NuGet packages needed. `[VERIFIED: codebase]`

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reusing `ManabaseAnalyzer`/`CastabilitySimulator` for the castability check | A dedicated, minimal pip-count function in the guard | CONTEXT D-11 explicitly forbids dragging the simulator into the guard's blast radius (perf + coupling); the simulator's hypergeometric consistency model is overkill for "does the deck have ≥1 source per required color" |
| A generic third-party MTG rules/validation library | None — hand-rolled, Core-side pure functions | No such package is needed; the logic is ~5 small deterministic checks over data already fetched from Scryfall |

**Installation:** None — no new packages.

**Version verification:** N/A — no new package versions to verify. Existing package versions (RestSharp 114.0.0, Polly 8.x) confirmed by direct read of `DeckFlow.Web/DeckFlow.Web.csproj` conventions already documented in project CLAUDE.md `[VERIFIED: codebase]`.

## Package Legitimacy Audit

**No external packages are installed by this phase.** The Package Legitimacy Gate protocol is not
applicable — the guard is built entirely from existing project dependencies (`RestSharp`, `Polly`,
`Microsoft.Extensions.Caching.Memory`), all already present in `DeckFlow.Web.csproj`/`DeckFlow.Core.csproj`
and already audited by prior phases.

**Packages removed due to slopcheck `[SLOP]` verdict:** none (no packages evaluated — none needed).
**Packages flagged as suspicious `[SUS]`:** none.

## Architecture Patterns

### System Architecture Diagram

```text
Submitted deck (DeckEntry[])          Whitelist candidate pool
        |                                       |
        v                                       v
  ICardGroundingGuard.ValidateAllAsync(names, deckContext)
        |
        |  1. cache lookup (IMemoryCache, normalized-name key)
        |         hit -> return cached CardGroundingVerdict
        |         miss -> continue
        v
  IScryfallCardResolver
        |
        |  batched cards/collection (<=75/call, exact ids)     <- D-04 batch surface
        |  per-miss fallback: cards/named?fuzzy=  (strict)      <- CS-21 single-card accept
        v
  ScryfallThrottle.ExecuteAsync (named "scryfall" Polly pipeline; ~5 req/s pacing, 429 backoff)
        |
        v
  ScryfallCard (Name, ColorIdentity, Legalities[NEW], ProducedMana, ManaCost, TypeLine)
        |
        v
  Pure Core decision functions (no HTTP):
    - LegalityCheck(card.Legalities["commander"] == "legal")
    - IdentityCheck(card.ColorIdentity subset-of commander identity)
    - SingletonCheck(name not already in submitted deck, basics exempt)
    - PipCastabilityCheck(deck's known color sources cover every colored pip the card needs)
        |
        v
  CardGroundingVerdict { Accepted, CanonicalName, RejectReason }   <- D-13 rich record
        |
        |-- cache verdict-relevant fields only (never full card JSON) --> IMemoryCache (24h/1h TTL)
        v
  Consumer (Phase 99 CreatorStylePacketService): assembly gate + whitelist embed
```

### Recommended Project Structure
```
DeckFlow.Core/
└── Knowledge/
    └── CardGrounding/                      # NEW — pure decision logic, no HTTP
        ├── ICardGroundingGuard.cs          # Core-facing seam (mirrors ICardNameGrounder pattern)
        ├── CardGroundingVerdict.cs         # sealed record: Accepted, CanonicalName, RejectReason
        ├── CardGroundingRejectReason.cs    # enum: NotFound/Ambiguous/NotLegal/IdentityViolation/
        │                                   #   SingletonDuplicate/Uncastable/UpstreamUnavailable
        ├── CardGroundingDeckContext.cs     # commander color identity, deck's cards+color sources,
        │                                   #   already-present card names (singleton check input)
        └── CardGroundingRules.cs           # static pure functions: legality/identity/singleton/pip

DeckFlow.Web/
└── Services/
    └── Scryfall/
        ├── CardGroundingGuard.cs           # implements ICardGroundingGuard; composes
        │                                   #   IScryfallCardResolver + IMemoryCache (D-14 cache)
        ├── ScryfallErrorResponse.cs        # NEW DTO: {object, code, type?, status, details} for 404 parse (branch on type=="ambiguous")
        └── ScryfallDtos.cs                 # MODIFIED: add Legalities to ScryfallCard (D-09)

DeckFlow.Web/
└── Services/
    └── CreatorStyle/
        └── CreatorWhitelistPoolBuilder.cs  # NEW — reads ICreatorDeckCacheStore, ranks by
                                             #   cross-deck frequency, caps, filters per submitted
                                             #   deck's context (D-05/D-06/D-07)
```

### Pattern 1: Core-seam / Web-impl split (mirrors P96 exactly, D-02)
**What:** Interface lives in `DeckFlow.Core` with no HTTP dependency; implementation lives in
`DeckFlow.Web/Services/Scryfall/` where the throttle/resolver already live.
**When to use:** Any time Core needs Scryfall-derived data but must stay HTTP-free for direct xUnit
testing (`DeckFlow.Core.Tests`).
**Example (existing P96 precedent this phase must replicate):**
```csharp
// Source: DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs
namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

public interface ICardNameGrounder
{
    Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default);
}
public sealed record CardGroundingResult(bool Resolved, string CanonicalName);
```
The new `ICardGroundingGuard` must NOT extend or modify this — D-01 requires a fully separate interface
and implementation (`ICardGroundingGuard`/`CardGroundingGuard`), sharing only the underlying
`IScryfallCardResolver`.

### Pattern 2: Verdict cache mirrors the grounder's proven TTL shape (D-14)
**What:** `IMemoryCache`, positive TTL 24h, negative TTL 1h, key = normalized lowercase trimmed name.
**Example (existing, to be mirrored — NOT extended):**
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs:11-12,22-26,41-44,49-50
private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromHours(24);
private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromHours(1);
// ...
var cacheKey = BuildCacheKey(candidateName);
if (cache.TryGetValue<CardGroundingResult>(cacheKey, out var cachedResult)) { return cachedResult!; }
// ...
cache.Set(cacheKey, result, result.Resolved ? PositiveCacheTtl : NegativeCacheTtl);
private static string BuildCacheKey(string candidateName)
    => "card-grounder:" + candidateName.Trim().ToLowerInvariant();
```
D-14 additionally requires caching **only verdict-relevant fields** (canonical name, color identity,
legality, mana cost) — never the full `ScryfallCard` JSON — to respect the 512MB web-tier RAM cap. Use a
small internal cache-record type distinct from `ScryfallCard`, not the raw DTO.

### Pattern 3: Batched Scryfall lookup, 75-per-call (D-04 batch surface — existing precedent)
**What:** `cards/collection` accepts up to 75 identifiers per POST; existing code already chunks at
exactly this size.
**Example:**
```csharp
// Source: DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs:22,344-347
private const int ScryfallBatchSize = 75;
// ...
for (int offset = 0; offset < identifiers.Count; offset += ScryfallBatchSize)
{
    object[] batch = identifiers.Skip(offset).Take(ScryfallBatchSize).ToArray();
    var request = new RestRequest("cards/collection", Method.Post);
    // ...
}
```
`ValidateAllAsync(names, deckContext)` should batch exact-name lookups this way, falling back to the
strict per-name fuzzy `/cards/named` call (CS-21) only for the `not_found` subset returned by the
collection batch — this matches the existing `ResolveSingleAsync` cascade
(`DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:103-126`) almost exactly, but the guard's version
must be **strict**: a batch-collection near-miss is NOT auto-accepted the way the lenient grounder auto-
accepts a fuzzy-corrected name — CS-21 requires the guard to reject anything that isn't an exact or
single-fuzzy-match, and must return the **canonical** Scryfall name (D-03) even on an exact hit so callers
never propagate the raw candidate string.

### Pattern 4: Internal test-seam constructor + `[InternalsVisibleTo]`
**What:** Public DI constructor + `internal` constructor taking injectable delegates, exposed to the test
project via assembly attribute.
**Example (existing, both assemblies already wired):**
```csharp
// Source: DeckFlow.Web/AssemblyInfo.cs:3
[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]
// Source: DeckFlow.Core/AssemblyInfo.cs:3
[assembly: InternalsVisibleTo("DeckFlow.Core.Tests")]
```
`CardGroundingGuard`'s tests should stub `IScryfallCardResolver` directly (a plain interface fake, as
`ScryfallCardNameGrounderTests.cs` already does with its `FakeResolver`) rather than adding a new internal
constructor — the resolver is already a seam; no HTTP mocking is needed.

### Anti-Patterns to Avoid
- **Extending `ScryfallCardNameGrounder` to add a strict mode:** D-01 explicitly forbids this — it would
  couple P96's lenient distill-time behavior (golden-tested) to P98's strict artifact-reject behavior.
  Two callers, two different behavioral contracts, two classes.
- **Pulling `ManabaseAnalyzer`/`CastabilitySimulator` into the guard:** D-11 explicitly forbids this. The
  guard's pip check is a simple "≥1 source per required color" count over already-fetched
  `ScryfallCard.ColorIdentity`/`ProducedMana` data, not a hypergeometric consistency simulation.
- **Caching full `ScryfallCard` JSON:** violates D-14 and the 512MB web-tier RAM cap (CLAUDE.md
  constraint) — cache only the 4-5 verdict-relevant fields.
- **Treating any non-2xx from `/cards/named?fuzzy=` as one undifferentiated "reject":** CS-25 fixtures
  require distinguishing `NotFound` from `Ambiguous` — this needs a genuinely new error-body parse step
  (see Common Pitfalls below), not reuse of any existing status-code-only check.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Scryfall pacing / 429 backoff | A new rate limiter | `ScryfallThrottle.ExecuteAsync` (static, process-wide) | Already tuned (~5 req/s, Retry-After honoring, 2-retry cap); a second throttle would double-pace or race the shared gate |
| Resilience (retry/circuit-break) around Scryfall HTTP | Custom retry loop | Named `"scryfall"` `ResiliencePipeline<RestResponse>` via `ResiliencePipelineProvider<string>` | Central registration in `ResiliencePipelineFactory`; CLAUDE.md forbids building pipelines per call |
| Card→color mapping / mana-cost pip parsing | A full mana-cost parser | Parse `ScryfallCard.ManaCost` pip letters directly (e.g. count `{U}` occurrences) for the guard's lightweight check; do not reuse `ManabaseClassifier.MapColors` (private, and tied to the heavier `CardFact`/`ManaSource` model) | The guard needs a tiny, independent parse — reusing the Manabase namespace's classifier reintroduces exactly the coupling D-11 forbids |
| Commander banlist cross-check | A second banlist HTTP client | `ScryfallCard.Legalities["commander"]` (D-09) — the ban-list `ICommanderBanListService` stays the project's SoT for its *existing* surfaces, but the guard does not take a dependency on it | Scryfall's `legalities` object also rejects never-legal cards (un-sets/acorn) that a banlist alone misses; avoids double Scryfall+HTML-scrape dependency inside one guard call |
| Whitelist candidate discovery | A live Scryfall search / EDHREC-style top-up query | `ICreatorDeckCacheStore.GetByCreatorAsync(creatorSlug)` — already-cached P95 corpus | D-05 locks pool source to the creator's own crawled decks only; zero extra Scryfall traffic for pool assembly |

**Key insight:** Nearly everything CS-21..CS-24 need already exists as infrastructure (throttle, resilience,
resolver, cache pattern, batch chunking). The actual net-new surface is small: one DTO field
(`Legalities`), one new error-parse DTO (`ScryfallErrorResponse`), one pure decision-rules file, one cache
wrapper, and one whitelist-pool reader. Resist the temptation to build anything more general — the
phase's entire purpose is a narrow, deterministic safety net, not a card-database abstraction layer.

## Common Pitfalls

### Pitfall 1: `/cards/named?fuzzy=` cannot distinguish "ambiguous" from "not found" by HTTP status alone
**What goes wrong:** Both conditions return HTTP 404. A naive strict validator (like every existing caller
in this codebase today) treats any non-2xx as "reject, no reason," which satisfies CS-21's literal
accept/reject behavior but NOT CS-25's requirement for a fixture asserting the specific `Ambiguous` reject
reason (D-13).
**Why it happens:** Scryfall's fuzzy endpoint always returns exactly one card (200) or a single Error
object (404); the distinguishing signal lives inside the JSON body — **verified live 2026-07-18 against
`api.scryfall.com` (orchestrator curl):** BOTH conditions return `"code": "not_found"`, so `code` alone
CANNOT distinguish them. The ambiguous case carries an ADDITIONAL `"type": "ambiguous"` field that the
plain not-found case omits entirely:

```json
// fuzzy=aust+com (ambiguous):
{ "object": "error", "code": "not_found", "type": "ambiguous", "status": 404,
  "details": "Too many cards match ambiguous name “aust com”. Add more words to refine your search." }
// fuzzy=zzzznotacardzzz (not found):
{ "object": "error", "code": "not_found", "status": 404,
  "details": "No cards found matching “zzzznotacardzzz”" }
```

None of the three existing call sites in this repo parse the error body today — they all just branch on
status code range.
**How to avoid:** Add a small `ScryfallErrorResponse` DTO (`{object, code, type, status, details}` — `type`
nullable) and, on a 404 from the strict fuzzy call specifically, deserialize `response.Content` (RestSharp
still exposes the raw body even when the typed `Data` is null) and branch on `type == "ambiguous"` →
`Ambiguous`, everything else → `NotFound`. Do NOT branch on `code` — it is `"not_found"` in both cases.
This is genuinely new code, not a copy of an existing pattern — flag as a specific planner task.
**Warning signs:** A CS-25 fixture for "two cards fuzzy-match ambiguously" (if one is chosen — e.g. a name
overlap edge case) returning `RejectReason.NotFound` instead of `RejectReason.Ambiguous` because the parse
step was skipped.
**Confidence:** `[VERIFIED: live curl against api.scryfall.com, 2026-07-18, both error bodies captured
verbatim above]`. The earlier assumption that `code` distinguishes the cases was WRONG — discriminator is
the presence of `"type": "ambiguous"`. Planner must encode the `type`-field branch, not a `code` branch.

### Pitfall 2: Singleton check needs a basic-land exemption list that does not exist in the codebase yet
**What goes wrong:** D-10 requires basics (including snow-covered) to be exempt from the "already in deck"
singleton reject, but "any number of copies" cards (Shadowborn Apostle, Persistent Petitioners, Dragon's
Approach, etc.) do NOT get this exemption and should still reject.
**Why it happens:** No existing constant/helper in this codebase enumerates basic land names (`Plains`,
`Island`, `Swamp`, `Mountain`, `Forest`, `Wastes`, plus `Snow-Covered` variants) — a grep for
`IsBasicLand`/`BasicLandNames` across `DeckFlow.Core` and `DeckFlow.Web` returns nothing `[VERIFIED:
codebase grep]`. This must be net-new, small, and Core-side (pure string set, no Scryfall call needed
since basic-ness is name-determinable, not type-line-determinable, to keep the check fast/offline —
though `ScryfallCard.TypeLine` containing `"Basic Land"` is also usable if the card was already fetched).
**How to avoid:** Prefer checking `TypeLine.Contains("Basic Land")` on the already-fetched `ScryfallCard`
over a hardcoded name list — it is authoritative and handles reprints/alternate names automatically. Only
fall back to a name-set if the guard needs to exempt basics before a Scryfall fetch happens (unlikely,
since the guard fetches every candidate anyway).
**Warning signs:** A cEDH deck with 40 Forests all individually rejected as "singleton duplicate" the
moment the second Forest suggestion is checked.

### Pitfall 3: The pip-castability check needs the submitted deck's OWN Scryfall data, which is a second batch fetch the guard's `ValidateAllAsync` does not automatically provide for the *deck's own cards* (only the candidate names passed in)
**What goes wrong:** D-11's castability check requires knowing "the submitted deck's manabase actually
produces every colored pip the candidate needs" — this requires the deck's own lands/rocks' produced-mana
data, not just the candidate's. If `ValidateAllAsync(names, deckContext)` only validates the *candidate*
names, the caller (P99) must separately hydrate the submitted deck's own `ScryfallCard` data and pass it in
via `deckContext` — this is a P99 integration contract detail that Phase 98 must design for even though
P99 is out of scope.
**Why it happens:** The batch surface (D-04) is framed around "every card in assembled content passed the
guard," which naturally reads as candidate-only; the deck's own color-source data is a separate input.
**How to avoid:** Design `CardGroundingDeckContext` (or whatever the planner names it) as an explicit input
parameter carrying: (a) commander color identity, (b) a precomputed set of colors the deck's lands/rocks
already produce (a `HashSet<char>` of WUBRG letters is sufficient for the "≥1 source per color" check — no
counts needed per D-11's "optional" framing on mana-value sanity), and (c) the set of card names already
in the submitted deck (for the singleton check). This keeps `CardGroundingRules` pure and testable with
plain data, and makes the P99 integration contract explicit in this phase's public API rather than an
implicit assumption.
**Warning signs:** Every candidate in every color rejects as `Uncastable` because `deckContext`'s produced-
color set was left empty/default by an integration that didn't realize it needed to be populated
separately from the candidate list.

### Pitfall 4: Whitelist pool is a raw crawl cache, not deck-context-filtered by default
**What goes wrong:** `ICreatorDeckCacheStore.GetByCreatorAsync` returns every cached deck's full `Entries`
list for a creator — off-color cards, cards already in the *submitted* deck, and non-Commander-legal cards
(rare, but a creator's own decks could include a since-banned card) are all present in the raw pool.
**Why it happens:** D-06 explicitly separates "cached per-creator raw pool" from "per-request filter" —
the raw pool is intentionally unfiltered at cache time (cheap, creator-scoped, reusable across many
requests); filtering against the submitted deck's specific color identity/singleton/legality state must
happen per-request, which means the whitelist pool builder needs to run every whitelist candidate through
the SAME guard used for the artifact's own cards (not a separate, lighter check) — otherwise a banned
staple could leak into the "safe to suggest" list.
**How to avoid:** The whitelist pool builder should call the same `ICardGroundingGuard.ValidateAllAsync`
(or an equivalent internal path) on its ranked/capped candidate set before embedding it in the packet, not
skip validation because "it came from a real deck already."
**Warning signs:** CS-25's "banned staple" fixture (e.g. a card banned after a creator's deck was crawled)
passes if it appears in the whitelist path but fails if fetched directly — inconsistent enforcement.

## Code Examples

### Batched exact-name lookup shape to mirror for `ValidateAllAsync`
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:103-126 (ResolveSingleAsync)
// The guard's batch path should mirror this exact-then-fallback shape, but the FALLBACK
// step must be the STRICT single-fuzzy-accept (CS-21), never the lenient
// SearchPrintingFallbackCardAsync cascade the P96 grounder uses.
var request = new RestRequest("cards/collection", Method.Post);
request.AddJsonBody(new { identifiers = new object[] { new { name = cardName } } });
RestResponse<ScryfallCollectionResponse> response =
    await ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
if (response.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices && response.Data?.Data.Count > 0)
{
    ScryfallCard? hit = response.Data.Data.FirstOrDefault(card =>
        string.Equals(CardNormalizer.Normalize(card.Name), CardNormalizer.Normalize(cardName), StringComparison.Ordinal));
    if (hit is not null) { return hit; }
}
```

### Existing strict fuzzy call already present in `SearchPrintingFallbackCardAsync` (last-resort step) — reuse the request shape, not the surrounding lenient cascade
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:195-204
var namedRequest = new RestRequest("cards/named", Method.Get);
namedRequest.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);
if ((int)namedResponse.StatusCode >= 200 && (int)namedResponse.StatusCode < 300 && namedResponse.Data is not null)
{
    return namedResponse.Data;
}
return null; // <- this is where the guard must branch 404 into NotFound vs Ambiguous (Pitfall 1)
```

### `ScryfallCard` DTO — current shape, needs `Legalities` added (D-09)
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs:39-57
public sealed record ScryfallCard(
    string Name,
    [property: JsonPropertyName("mana_cost")] string? ManaCost,
    [property: JsonPropertyName("type_line")] string TypeLine,
    // ... existing fields ...
    [property: JsonPropertyName("color_identity")] IReadOnlyList<string>? ColorIdentity,
    // ADD: [property: JsonPropertyName("legalities")] IReadOnlyDictionary<string, string>? Legalities = null
    [property: JsonPropertyName("produced_mana")] IReadOnlyList<string>? ProducedMana = null,
    [property: JsonPropertyName("rarity")] string? Rarity = null);
```
Scryfall's `legalities` object is keyed by format (`"standard"`, `"commander"`, `"duel"`, `"oathbreaker"`,
`"brawl"`, `"gladiator"`, ...) with string values `"legal" | "not_legal" | "restricted" | "banned"`.
`[VERIFIED: live curl `GET api.scryfall.com/cards/named?fuzzy=lightning+bolt` 2026-07-18 — response
carries `"legalities": {"standard": ..., "future": ..., "historic": ..., "timeless": ..., "gladiator": ...,
"pioneer": ..., "commander": "legal", ...}` (lowercase format keys, lowercase string values; `commander`
key present) plus `"color_identity": ["R"]`, `"type_line"`, `"mana_cost"`, `"cmc"`.]`

### Whitelist pool source — existing store call (D-05)
```csharp
// Source: DeckFlow.Core/Content/ICreatorDeckCacheStore.cs:26-27
Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default);
// CreatorDeckCacheEntry.Entries : IReadOnlyList<DeckEntry> — each has Name, Quantity, Board.
// Frequency ranking (D-07) = count of decks (not copies) each distinct card name appears in,
// across GetByCreatorAsync's full result set, sorted descending, capped.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| No card-name validation anywhere in the creator-style pipeline | P96's lenient distill-time grounder (fuzzy-correct + flag) | Phase 96 (this cycle) | Established the `ICardNameGrounder`/`ScryfallCardNameGrounder` split pattern this phase's `ICardGroundingGuard` deliberately does NOT extend |
| Free-generate LLM card suggestions | Constrained selection ("recommend only from this provided list") | Industry pattern cited from DeepMTG (Giles Strong, 2025) `[CITED: docs/research/creator-style-llm-system.md:104]` | Directly informs D-08 — the whitelist ships WITH an explicit "suggest swaps ONLY from this list" instruction, since there is no server-side LLM to constrain (P100 is a $0 paste tool) |

**Deprecated/outdated:** N/A — this is greenfield substrate within the project; no prior guard exists to
deprecate.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Scryfall's 404 Error object `code` field takes the literal string values `"ambiguous"` and `"not_found"` for the fuzzy-named-lookup 404 case | Common Pitfalls #1 | If the actual code strings differ, the `Ambiguous` vs `NotFound` reject-reason split (D-13, CS-25) silently defaults everything to one bucket; low blast radius (still fail-closed/reject) but breaks the specific CS-25 fixture assertion on reason text |
| A2 | `ScryfallCard.Legalities` should be typed `IReadOnlyDictionary<string,string>` keyed by lowercase format name with values `"legal"/"not_legal"/"restricted"/"banned"` | Code Examples (DTO addition) | If Scryfall's actual JSON shape differs (e.g. nested objects, different casing), the new DTO field fails to deserialize silently (nullable) and the legality check always sees `null`, which must fail-closed (D-12) rather than fail-open — planner should ensure the implementation treats a missing/null `Legalities["commander"]` as a reject, not an implicit pass |
| A3 | A basic land is best detected via `TypeLine.Contains("Basic Land")` on the already-fetched `ScryfallCard` rather than a hardcoded name list | Common Pitfalls #2 | If wrong (e.g. some basic-adjacent land like "Snow-Covered" DFCs don't carry the exact substring), the singleton exemption could over- or under-apply; low risk, easily unit-tested against real Scryfall fixture data at implementation time |

**Confirmation needed before locking:** A1 and A2 should be spot-checked with one live Scryfall API call
(`curl https://api.scryfall.com/cards/named?fuzzy=<ambiguous-test-string>` and
`curl https://api.scryfall.com/cards/named?exact=Sol%20Ring`) at implementation/plan-review time — this
research session's direct fetch attempts against `scryfall.com/docs/*` were blocked by Cloudflare (403),
but `api.scryfall.com` (the actual API host, as opposed to the docs host) was never itself blocked in this
session and is very likely reachable from the execution environment the same way every other Scryfall call
in this codebase already succeeds.

## Open Questions

1. **Exact whitelist cap value (D-07, Claude's Discretion)**
   - What we know: the packet must stay "paste-sized"; P95's `MeasuredStyleProfileBuilder` caps lift
     metrics at 25 (`MaxLiftMetrics = 25`), suggesting a similar order-of-magnitude cap is the house style
     for "bounded list embedded in a prompt artifact."
   - What's unclear: no phase-98-specific token budget has been computed against P99's actual packet
     shape (P99 doesn't exist yet).
   - Recommendation: planner should pick a cap in the same range (15-30 candidates) as a starting point
     and note it as adjustable once P99's actual packet token budget is measured.

2. **"Usable floor" threshold for the 503 escalation (D-12, Claude's Discretion)**
   - What we know: if per-card rejects push required content below a usable floor (e.g. the commander
     itself is unvalidatable), the guard's caller should surface `UpstreamErrorMessageBuilder`'s 503 copy
     instead of a degraded packet.
   - What's unclear: whether "required content" means just the commander, or also a minimum count of
     mainboard cards.
   - Recommendation: scope this decision to P99 (the actual packet assembler) rather than Phase 98 itself
     — the guard's job is to return accurate verdicts; the escalation threshold is a P99 policy decision
     that consumes the guard's aggregate result.

3. **Whether mana-value sanity is included in the pip check v1 (D-11, Claude's Discretion)**
   - What we know: D-11 frames it as optional; the core check (color identity ⊆ commander identity + ≥1
     source per required color) is mandatory.
   - What's unclear: whether skipping mana-value sanity in v1 creates a visible gap (e.g. suggesting a
     7-drop to a deck with only 30 lands, technically "castable" by the pip check but impractical).
   - Recommendation: defer to v1-without-mana-value-sanity per D-11's explicit "optional" framing; this
     matches the phase's "lightweight, not Karsten simulation" scope boundary.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `api.scryfall.com` (Scryfall REST API) | CS-21/CS-23 (all card validation) | Not directly probed this session (no live curl run); every existing caller in the codebase already depends on it successfully | — | D-12's fail-closed policy IS the fallback — a Scryfall outage causes rejects, not crashes |
| `.NET 10 SDK` / existing project build | Compiling the new files | ✓ (project builds today per prior phases) | net10.0 | — |
| `IMemoryCache` (DI-registered) | Verdict cache (D-14) | ✓ — already registered via `AddMemoryCache()` per `Program.cs` and consumed by `ScryfallCardNameGrounder`, `CommanderBanListService`, etc. | built-in | — |

**Missing dependencies with no fallback:** none identified — this phase adds no new external dependency
beyond the already-integrated Scryfall API.
**Missing dependencies with fallback:** Scryfall API availability itself — D-12 fail-closed is the designed
fallback (reject/omit rather than ship unvalidated).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) `[VERIFIED: codebase — csproj + existing test files]` |
| Config file | none — project-file-based (`.csproj` `PackageReference`s), no separate xunit.runner.json observed |
| Quick run command | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CardGrounding"` (pure logic, fast) |
| Full suite command | `dotnet build` clean, then `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj && dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` — per CLAUDE.md, VSTest is unreliable in WSL; rely on `dotnet build` clean + targeted `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CS-21 | Fuzzy validator: single match=ok, 404/ambiguous/none=reject | unit | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CardGroundingGuard"` | ❌ Wave 0 (new file: `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingGuardTests.cs`) |
| CS-22 | Whitelist builder assembles only legal/real candidates | unit | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~WhitelistPool"` | ❌ Wave 0 (new file, likely `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs`) |
| CS-23 | Singleton/color-identity/castability checks reject correctly | unit (pure Core) | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CardGroundingRules"` | ❌ Wave 0 (new file, `DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs`) |
| CS-24 | One reusable cached service consumed by Phase 99 | unit | Same as CS-21 test file — cache-hit/cache-miss assertions alongside the fuzzy-validator tests | ❌ Wave 0 (same new file as CS-21) |
| CS-25 | Known-hallucination fixtures (previously-would-have-shipped fakes/illegal cards) | unit | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~Hallucination"` | ❌ Wave 0 (new file, e.g. `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingHallucinationFixtureTests.cs`) |

### Sampling Rate
- **Per task commit:** targeted `dotnet test` filter for the file(s) just touched.
- **Per wave merge:** full `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests` suites.
- **Phase gate:** Full suite green (`dotnet build` clean, 0 warnings per CLAUDE.md gate) before
  `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs` — covers CS-23 (pure legality/
      identity/singleton/pip logic, no HTTP, plain-data inputs)
- [ ] `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingGuardTests.cs` — covers CS-21/CS-24 (strict fuzzy
      accept/reject, cache hit/miss, fake `IScryfallCardResolver` per the existing
      `ScryfallCardNameGrounderTests.cs` `FakeResolver` pattern)
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs` — covers CS-22 (pool
      assembly from a fake `ICreatorDeckCacheStore`, frequency ranking, capping)
- [ ] `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingHallucinationFixtureTests.cs` — covers CS-25
      (fixture list per Open Question/Discretion item on exact fake-card names; suggest reusing the
      "Dockside Extortonist" typo-heals-to-real-card fixture already proven in
      `ScryfallCardNameGrounderTests.cs`, PLUS a genuinely fake plausible name, PLUS the real-but-now-banned
      "Dockside Extortionist" (banned 2024-09-23, per `.planning`'s own
      `reference_dockside_banned.md` memory note) as the legality-reject fixture)
- No test framework install needed — xUnit already fully wired in both test projects.

## Security Domain

`security_enforcement` is not set to `false` in `.planning/config.json` (absent = enabled per protocol),
so this section is included even though the phase is cross-cutting substrate with no user-facing surface.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Guard is an internal service with no auth surface this phase |
| V3 Session Management | No | No session state introduced |
| V4 Access Control | No | No new endpoint/route this phase (no page, no controller) |
| V5 Input Validation | Yes | Card-name strings passed to Scryfall query parameters MUST go through `RestRequest.AddQueryParameter(...)` (RestSharp handles URL-encoding) — never string-concatenate a raw name into a URL, matching every existing Scryfall call site in this codebase `[VERIFIED: codebase — ScryfallCardResolver.cs, CardLookupService.cs both use AddQueryParameter]` |
| V6 Cryptography | No | No secrets/crypto touched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Query-parameter injection via a crafted "card name" containing control characters or Scryfall search-operator syntax (e.g. `name:"; DROP` style or Scryfall's own `q=` operator syntax abused to widen a search unexpectedly) | Tampering | Continue using `RestRequest.AddQueryParameter` (encodes automatically) for all guard-built requests; never build the fuzzy/collection request bodies by string interpolation |
| Denial of service via a submitted deck/whitelist candidate list large enough to generate excessive Scryfall batch calls | Denial of Service | `ScryfallThrottle`'s process-wide pacing gate already bounds total throughput; the whitelist cap (D-07, Open Question 1) additionally bounds per-request candidate count; the guard's cache (D-14) prevents repeat validation of the same name across requests |
| Cache poisoning via a crafted name that normalizes identically to a legitimate card but resolves differently over time (Scryfall data changes, e.g. a card gets banned) | Tampering / Information Disclosure (low severity) | D-14's short 1h negative TTL and D-12's fail-closed policy already bound staleness risk; no additional control needed this phase |

## Sources

### Primary (HIGH confidence)
- Direct source read: `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs`,
  `DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs`, `ScryfallCardResolver.cs`,
  `ScryfallDtos.cs`, `ScryfallThrottle.cs`, `ScryfallServiceCollectionExtensions.cs`,
  `CommanderBanListService.cs`, `UpstreamErrorMessageBuilder.cs`, `CardLookupService.cs`,
  `DeckFlow.Core/Manabase/KarstenManabase.cs`, `DeckFlow.Core/Content/ICreatorDeckCacheStore.cs`,
  `CreatorDeckCacheEntry.cs`, `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs`,
  `DeckFlow.Web.Tests/Services/Scryfall/ScryfallCardNameGrounderTests.cs`
- `.planning/phases/98-card-grounding-guard/98-CONTEXT.md` — locked D-01..D-14 decisions
- `.planning/REQUIREMENTS.md` §CS-21..CS-25 — locked requirement text
- `docs/research/creator-style-roadmap.md` §"P91 — Card-Grounding Guard" and requirement text
- `.planning/config.json` — confirms `nyquist_validation: true`, `security_enforcement` absent (enabled)

### Secondary (MEDIUM confidence)
- `docs/research/creator-style-llm-system.md:103` — "the `/cards/named?fuzzy=` endpoint returns exactly
  one card when unambiguous, a 404 `ambiguous` when many match, and 'No cards found' when none — a
  ready-made validator (verified live) [63]" — this is the project's own prior research, itself citing
  `https://api.scryfall.com/cards/named` as "verified live" at the time it was written
- WebSearch corroboration of Scryfall's documented Error object shape (`object`, `status`, `code`,
  `details`, optional `type`/`warnings`) and that `"ambiguous"` is a documented `code` value for the
  fuzzy-named 404 case

### Tertiary (LOW confidence)
- None retained as authoritative — the one LOW-confidence item (exact `code` string values, `Legalities`
  JSON key casing) is flagged explicitly in the Assumptions Log (A1, A2) rather than stated as fact.
  `scryfall.com/docs/api/errors` and `scryfall.com/docs/api/cards/named` both returned HTTP 403
  (Cloudflare) to direct `WebFetch` attempts this session and could not be read firsthand; `api.scryfall.com`
  itself (the actual API host used by all runtime code) was not blocked and was not independently tested
  live in this research session either — recommend the implementer run one live `curl`/`cards/named` call
  during implementation to lock the exact field shapes before finalizing `ScryfallErrorResponse` and the
  `Legalities` DTO field.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; every reused component read directly from source
- Architecture: HIGH — Core/Web split, cache pattern, batch pattern, test-seam pattern all directly
  observed in the P96/P95 precedent this phase is instructed to mirror
- Pitfalls: MEDIUM — the Scryfall ambiguous/not-found distinction and exact `legalities` JSON shape rely
  on WebSearch + the project's own prior (unverified-this-session) research note rather than a
  freshly-fetched official doc page (blocked by Cloudflare 403)

**Research date:** 2026-07-18
**Valid until:** 30 days (stable internal codebase substrate) for the architecture/pattern findings; the
Scryfall API-shape assumptions (A1/A2) should be re-verified live at implementation time regardless of
date, since they were never directly confirmed in this session
