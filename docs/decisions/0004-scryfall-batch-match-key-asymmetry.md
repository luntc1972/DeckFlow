# 0004 — Scryfall batch match-key asymmetry: additive punctuation-tolerant second pass

Date: 2026-07-31

## Context

`ScryfallReferenceResolver.ResolveBatchAsync` and `ScryfallCardResolver.ResolveSingleAsync`
resolve a card name against the same `cards/collection` endpoint, but they match the RETURNED
card back to the request with two different keys:

```csharp
// DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs:136 — BATCH, raw match
var matchingName = chunk.FirstOrDefault(name => string.Equals(name, card.Name, StringComparison.OrdinalIgnoreCase));

// DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:117-118 — SINGLE, CardNormalizer match
ScryfallCard? hit = response.Data.Data.FirstOrDefault(card =>
    string.Equals(CardNormalizer.Normalize(card.Name), CardNormalizer.Normalize(cardName), StringComparison.Ordinal));
```

The batch path compares the original request string to the returned card's `Name` with a raw
`OrdinalIgnoreCase` equality. The single-card path normalizes both sides first. This asymmetry
was flagged by research assumption A1 in `111.1-RESEARCH.md`: does the batch path's raw
comparison generate phantom misses (cards Scryfall already resolved, but which the client fails
to recognize as resolved), and if so, of what shape?

**Measured evidence — `111.1-REVIEWS.md` §0, live probes run by the orchestrator on 2026-07-31,
not inferences:**

| Input name | `POST /cards/collection` | `GET /cards/search` `!"…"` |
|---|---|---|
| `Smugglers Copter` (no apostrophe) | resolves -> `Smuggler's Copter` | resolves -> `Smuggler's Copter` |
| `Fire / Ice` (Archidekt single-slash) | `not_found` | resolves -> `Fire // Ice` |
| `Fire // Ice` (canonical) | `not_found` | resolves |

**A1 answer:** `/cards/collection`'s `name` identifier is punctuation-tolerant (it resolves a
name missing an apostrophe and returns the correctly-punctuated card), but it does NOT accept a
combined multiface name in EITHER slash form. `/cards/search` with the `!"…"` exact operator is
strictly more forgiving than `/cards/collection` for slash forms.

**Consequence:** the phantom-miss generator is PUNCTUATION, not DFC separators. When a name like
`"Smugglers Copter"` is submitted, Scryfall's `cards/collection` call already resolves and
returns the card (as `"Smuggler's Copter"`) in the same response the batch call made — but the
raw `OrdinalIgnoreCase` match-back at `ScryfallReferenceResolver.cs:136` compares the ORIGINAL
request string to the returned name and fails, because the two differ by punctuation. The name
is then treated as an unresolved miss and dispatched to the per-name fallback search
(`fallbackStrategy`), costing a full extra round-trip for a card the batch call had already
handed back. This is the burst this phase exists to fix, and it is pure waste: the answer was
already in hand.

DFC (double-faced card) names are a different and smaller case. The live probe shows
`cards/collection` genuinely cannot resolve EITHER slash form of a combined name — so a DFC name
falling through to the fallback strategy is legitimate work, not waste. Plan 111.1-01 already
halved the per-fallback-miss COST (see that plan's SUMMARY); it did not, and could not, reduce
the miss COUNT for the punctuation class, because the miss was never a real miss — it was a
client-side matching defect.

This behavior is explicitly marked LOAD-BEARING in the resolver's own xmldoc
(`ScryfallReferenceResolver.cs:52-61`, item 2), and `ResolveBatchAsync` is shared by five
services: `DeckComparisonService.cs:378`, `MetaGapService.cs:564`,
`DeckAnalysisPacketService.cs:1893` (delegate at `:1895`), `DeckHistoryPageService.cs:271`, and
`CutLabAnalysisContextBuilder.cs:410`. Any change to the match key is felt by all five at once,
which is why this is recorded as an ADR rather than made as a drive-by edit.

## Decision

### Options considered

**(a) Decline — leave the raw match as-is.** Zero risk, zero code change. Rejected: the measured
waste is large (every punctuation-drifted collection hit pays a redundant search-fallback
round-trip across all five consumers) and a fix exists at zero additional network cost.

**(b) `not_found`-driven miss detection.** Instead of matching returned cards back to request
names, treat only the identifiers Scryfall itself reports in `ScryfallCollectionResponse.NotFound`
(`ScryfallDtos.cs:32-34`) as misses, and assume everything else resolved by position/identifier.
There is direct precedent for using `NotFound` this way in `CardLookupService.cs:139-145`. This
would remove client-side matching drift entirely. Cost: it rewrites hit/miss semantics for all
five consuming services, and it requires rewriting the H2 lock test's fixture
(`ScryfallReferenceResolverTests.cs:27-52`), because that fixture supplies `NotFound: []` while
returning `"A // B"` for the request `"A / B"` — a `not_found`-driven matcher would call that a
HIT, which is exactly the behavior the H2 lock exists to pin as a MISS (a single-slash Archidekt
name must still be normalized-and-resubmitted through the fallback strategy, not silently
assigned the double-slash card's data). Not adopted in this phase. Recorded here as a possible
future consolidation, with this fixture-rewrite cost named up front so a future implementer does
not treat it as free.

**(c) Adopt `CardNormalizer.Normalize` as the batch match key, symmetric with
`ResolveSingleAsync`.** Rejected on two independently verified grounds
(`CardNormalizer.cs:15-33`):
  - It truncates at the DFC separator: `Normalize("A / B")` and `Normalize("A // B")` both
    collapse to `"a"` (the code lowercases, converts `" // "` to `" / "`, then slices at the
    first `" / "` and drops everything after it). Adopting this key would make the H2 lock's two
    forms COLLIDE, breaking that test, and would make two DIFFERENT DFCs sharing a front face
    resolve to the same key.
  - It does not even fix the punctuation class it would be adopted for: `Normalize("Smuggler's
    Copter")` = `"smuggler s copter"` (punctuation is replaced by a SPACE) while
    `Normalize("Smugglers Copter")` = `"smugglers copter"` — these are NOT equal, so the phantom
    miss this ADR targets would persist even under this option.

**(d) ADOPTED — an additive, punctuation-deleting, slash-preserving second pass over the
already-received batch response**, applied only to names the raw pass at `:136` left unmatched,
with an ambiguity guard. Zero extra network calls: it reads only `response.Data.Data`, which the
existing single `cards/collection` call for the chunk already returned. The raw pass and its
documented semantics (`ScryfallReferenceResolver.cs:52-61`, item 2) are untouched — it is still
the primary matcher and still runs first. Verified analytically (and locked by test, Task 2 of
plan 111.1-03) not to collide the DFC slash forms, so the H2 lock and both Deck Analysis golden
assertions containing `submitted_name:` survive unmodified.

### The key

`BatchMatchKey(string name)`:
1. Trim, then `ToLowerInvariant()`.
2. Delete (do not replace with a space) every character that is not a Unicode letter, digit,
   whitespace, or `/` — regex `[^\p{L}\p{N}\s/]` -> `string.Empty`.
3. Collapse `\s+` to a single space, trim.

Punctuation is DELETED rather than space-replaced so `"Smuggler's Copter"` and `"Smugglers
Copter"` key identically (`"smugglers copter"` both sides) — space-replacement, as
`CardNormalizer` does, would leave `"smuggler s copter"` vs `"smugglers copter"`, still distinct.
`/` is explicitly PRESERVED (not deleted, not collapsed with other punctuation) so `"a / b"` and
`"a // b"` remain distinct keys and the DFC fallback path is unchanged.

### The ambiguity rule

The second pass groups the still-unmatched request names and the still-unclaimed returned cards
(within the same chunk) by `BatchMatchKey`, and acts ONLY on a key that maps to exactly one
unmatched request name AND exactly one unclaimed card. Any key shared by two or more names, or
two or more cards, is skipped entirely on both sides and falls through unchanged to the fallback
strategy. A wrong match — silently assigning one deck slot another card's oracle text, mana cost,
and type line — is worse than paying for one extra search-fallback call.

### Implementation

Adopted and implemented in this same plan (111.1-03, Task 3). `ResolveSingleAsync` is NOT
touched by this decision; it already normalizes via `CardNormalizer`, which is a distinct code
path (single-card search) and out of scope here.

## Consequences

- **Deck Analysis prompt-artifact change.** `DeckAnalysisPacketService.cs:1920-1927` annotates a
  fallback-resolved name as `submitted_name: X | resolved_card: Y` only when
  `resolution.FromFallback` is true. A punctuation-drifted name that used to fall through to the
  fallback (and therefore get annotated) now resolves in the second pass with `FromFallback:
  false`, so the Deck Analysis packet will emit the bare resolved card name instead of the
  annotation for that class of name. This is a readability improvement in the emitted packet
  (fewer noisy annotations for what is, functionally, an exact hit), and it does not touch the
  two existing golden assertions (`AnalysisGoldens.cs:1798`,
  `DeckAnalysisPacketServiceTests.cs:1460`), because both exercise DFC slash-form names, which the
  second pass explicitly does not collide. Any FUTURE golden that covers a punctuation-drifted
  name (an apostrophe, a smart-quote, a special symbol) will differ from what it would have
  produced before this change — that is expected, not a regression.
- **What the next reader should do if they hit this asymmetry again:** read this ADR first. The
  raw pass at `ScryfallReferenceResolver.cs:136` is still the primary matcher and is still
  load-bearing; the second pass is a strictly additive supplement that never overrides a raw-pass
  result and never suppresses the fallback for a name it does not confidently match.
- **429 risk is reduced, not eliminated.** Plan 111.1-01's fix (halving the per-miss cost) plus
  this change (eliminating the punctuation-class misses entirely) together reduce the number of
  extra round-trips a very-high-miss-count pool generates, but DFC-shaped names still legitimately
  fall through to the fallback strategy, because the API cannot resolve them at `cards/collection`
  under any submission spelling. Unless plans 111.1-04/05 land the pacing change, `ScryfallThrottle`'s
  200ms interval remains 2.5x faster than Scryfall's documented 500ms per-endpoint ceiling for
  `/cards/collection`, `/cards/search`, `/cards/named`, and `/cards/random` — a pool with enough
  legitimate DFC misses can still burst past that ceiling.
- **Option (b) (`not_found`-driven miss detection) remains on the table** as a later, larger
  consolidation across all five `ResolveBatchAsync` consumers, with its H2-fixture rewrite cost
  named above so it is not mistaken for a free simplification.

## Addendum (2026-08-19): the match scope after partition-then-chunk

`ResolveBatchAsync` now partitions each original 75-name chunk into a warm set (collection-cache
hits, no POST) and a cold remainder, and re-chunks only the cold remainder for the POSTs. The two
passes above are unchanged, but the SCOPE they run over is no longer always the original chunk:

- The warm set of each original chunk is matched as its own pseudo-chunk, deliberately with BOTH
  passes. Giving warm names only the raw pass would send a punctuation-drifted warm name to the
  fallback strategy -- an extra Scryfall search on the path the cache exists to make free.
- Pooling every warm name across all chunks into ONE pseudo-chunk was tried and REJECTED (Codex
  review, round 1): it widened the ambiguity pool, so two cached cards sharing a match key that
  never shared a response collided and both names declined to fallback.
- The remaining, ACCEPTED divergence (Codex review, round 2): when two punctuation-colliding names
  sit in the SAME original chunk and one is warm while the other is cold, they no longer share a
  scope, so each can match where both previously declined as mutually ambiguous. This resolves MORE
  names, never fewer, and it is bounded to same-chunk collisions. Pinned by
  `ScryfallReferenceResolverTests.ResolveBatchAsync_WarmAndColdPunctuationCollisionInOneChunk_ResolvesBoth`.
- Preserving the pre-change scope exactly would mean decoupling transport from matching: batching
  the cold identifiers globally for the POSTs, then handing each ORIGINAL chunk back its own cards.
  That was weighed and deferred -- it needs an attribution rule for ambiguous leftover cards that no
  pairing pass can assign to a chunk. It belongs with the queued global pairing strategy (F-2/F-6).

## Extension: callers that need failure isolation (2026-08-19, UAT finding 2)

`CutLabAnalysisContextBuilder` cannot simply hand `ResolveBatchAsync` its whole missing-name list:
its resolve loop is also the method's failure isolation, so that one throwing call cannot discard
names that already resolved. Chunking the list itself, however, pinned the POST count at the
CALLER's chunk count -- the partition above could never collapse POSTs across those chunks, because
each kept a cold member and each still POSTed. Measured in UAT: a 111-name pool fully warmed by a
prior Manabase run still cost 2 POSTs through Cut Lab, where a single-chunk 60-name pool cost 0.

`ScryfallReferenceResolver.PlanBatchResolveGroups` resolves that tension. It returns the same
grouping `ResolveBatchAsync` would build internally for the whole list -- each original 75-name
chunk's warm set as its own group, then the pooled cold remainder re-chunked -- and the caller
resolves those groups one call at a time. The scope rules above are therefore unchanged: warm names
still keep their ORIGINAL chunk boundaries, and pooling is still confined to the cold remainder.

Two consequences worth stating:

- Warmth is a snapshot taken at plan time, and the cache is a DI singleton, so a concurrent request
  can warm part of a planned-cold group before it is resolved. That can only REMOVE POSTs -- the
  now-warm identifier drops out of the submitted set, because `ResolveBatchAsync` re-checks the
  cache itself. It is not result-neutral: the newly warm name lands in the warm match scope while
  its group-mates stay in the cold one, which is precisely the accepted divergence three bullets
  above -- it resolves MORE names, never fewer. No deterministic test pins the race (Codex stage-2
  review of `0e5a3c7f`, claim 5); it is documented rather than guarded because the outcome is
  bounded and benign.
- A caller-visible improvement falls out of it: a throwing fallback no longer discards cards the
  same request had already cached. They are re-planned into a warm group on the next resolve, which
  issues no POST and so cannot be aborted by a cold casualty. Pinned by
  `CutLabPageServiceTests.ProcessAsync_ScryfallRateLimitsDuringFallback_ImportSucceedsWithoutBanner`.
