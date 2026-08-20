# Scryfall collection lookup — protocol consolidation (design)

**Status:** design, awaiting review. No code written.
**Branch base:** `feat/cache-tier0`.
**Evidence base:** `/mnt/d/claude_doc/deckflow/cache-tier0/2026-08-20-phase5-census.md` — a 15-implementation
census plus two read-only traces, all claims carrying `file:line`. **This document does not restate the
census.** Where a fact is cited here without evidence, the census section is named instead.

This supersedes the "Phase 5" sketch in
`/mnt/d/claude_doc/deckflow/cache-tier0/2026-08-18-cache-tier0-backlog-closeout.md:402-424`, whose two
premises the census disproved: F-2 is 15 implementations rather than three, and F-6 is one file rather
than a subsystem.

---

## 1. Problem

Four production code paths probe and populate one process-wide `ScryfallCollectionCardCache`
(census §A). They agree on the rule **"key the cache by the identifier you actually submitted"** — stated
in `b76f88ad`'s commit body and deliberate — but they disagree on *what they submit*, on how they match
responses back, and on where the chunk boundary sits relative to the cache probe.

One of those disagreements costs real work on every request. `ManabaseAnalysisService` submits the raw
deck-entry name, so a double-faced card is submitted as `A // B`. ADR-0004:35-38 measured that
`cards/collection` accepts neither slash form and echoes the identifier in `not_found`. The card is then
recovered by a `cards/search` repair that **writes nothing to any cache** (census §G.1), so that search is
re-paid on every analysis, forever, and the entry can never be shared with the two resolvers that key the
same cache by front face.

Reachability is not marginal. `MoxfieldApiDeckImporter.cs:160-161` nulls set code and collector number for
every entry on the Commander Spellbook fallback — taken on any Moxfield 401/403/429/451/5xx — which routes
an entire deck down the name branch. Plain `N Card Name` pastes do the same.

## 2. What this is not

Confirmed by trace, so that no plan re-litigates them:

- **Not a correctness defect.** The card resolves, via the repair search at `ManabaseAnalysisService.cs:750`,
  indexed at `SearchFallbackPriority`. Analysis output is correct today.
- **Not warmth-dependent.** A stored collection-miss makes `TryGetName` return `true` with a null card, which
  drives `continue`, leaving the index in exactly the state a cold `not_found` leaves it. Cold and warm runs
  produce identical output. Evidence and the pinning test: census §G.2.
- **Not a cache-keying bug.** Keying by the submitted identifier is a deliberate invariant with a stated
  rationale. This design preserves it; the key agreement it produces is a *consequence* of submitting a
  better identifier, never an override of the rule.

The defect is therefore **recurring wasted upstream work**, on the endpoint whose rate limit has already
caused live Cloudflare IP blocks.

## 3. Decisions taken

| # | Decision | Taken |
| --- | --- | --- |
| D1 | Split the work into three independently reviewable pieces rather than one bundle | user, 2026-08-20 |
| D2 | Route **all five** throttle-bypass sites through `ScryfallThrottle`, tests included | user, 2026-08-20 |
| D3 | The F-2 extraction preserves today's behavior **exactly**; ADR-0004:184-187 stays deferred | user, 2026-08-20 |
| D4 | Spec and plans live in `.planning/`; the census stays in `/mnt/d/claude_doc/` | user, 2026-08-20 |

D3 is load-bearing for scope: a refactor that also changes behavior is two changes wearing one commit, and
the deferred item needs an attribution rule for ambiguous leftover cards that nobody has designed.

---

## 4. Piece 1 — submission shape, and the throttle

### 4.1 The three coupled changes

Submitting a better identifier is **not** a one-line change, because the response pairing is keyed off the
submitted name. All three must land together or the card silently keeps falling back:

1. **Submit the front-face identifier.** The name branch at `ManabaseAnalysisService.cs:1127-1136` submits
   `ScryfallCollectionIdentifier.ToFaceIdentifier(entry.Name)` instead of `entry.Name`. The printing branch
   is untouched.
2. **Pair on the same footing.** The name-pairing comparison at `:1196-1199` currently compares
   `card.Name` to the submitted name. Scryfall returns a double-faced card under its **combined** name, so
   once the submission is a front face those two can never be equal. Pairing must instead compare
   `ToFaceIdentifier(card.Name)` against the submitted face identifier — the same footing
   `ScryfallReferenceResolver` already uses for its exact-identifier pass, including that pass's
   *exactly-one-on-both-sides* ambiguity guard.
3. **The cache key follows the submission, unchanged in rule.** Because the key is whatever was sent, it
   becomes the face identifier automatically, and therefore agrees with `ScryfallReferenceResolver` and
   `ScryfallCardResolver`. No keying logic is edited.

Index population is **not** changed: entries continue to enter `ScryfallCardNameIndex` under the original
`entry.Name`, because `TryResolve` is called with the deck entry's own name.

### 4.2 The ambiguity this opens, and how it is handled

Two distinct deck entries can share a front face — `A // B` and `A // C` both reduce to `A`. The dedupe key
at `:1129` stays on the raw entry name, so both are submitted, and the exactly-one guard from 4.1(2) then
declines to pair either. Declining routes both to the existing repair search, i.e. **today's behavior for
that shape, preserved**. Deduplicating on the face identifier instead was considered and rejected: it would
silently collapse two different cards into one deck slot.

### 4.3 Throttle (D2)

Five sites issue Scryfall requests outside `ScryfallThrottle` — one production CLI, four test harnesses;
enumerated with `file:line` in census §C. All five move onto the throttled call path, their hand-rolled
`Task.Delay` pacing is deleted as now-redundant, and a guard test asserts no raw Scryfall POST exists
outside the throttle. The guard is the durable half: the five fixes decay, the guard does not.

### 4.4 Behavioral contract

- Analysis output is **expected** byte-identical before and after, for every deck shape — but see §10:
  the card now arrives paired rather than search-repaired, which moves it *up* the priority ladder, and
  that is only inert if no name collision is in play. This is an assertion the plan must prove on a
  collision deck, not an assumption. The flag-free, UAT-free shape of piece 1 depends on it holding.
- `cards/search` calls fall by one per double-faced name on the name branch, per analysis.
- `cards/collection` identifier slots fall by the same count.
- Cross-tool sharing begins working for those names, in both directions.

---

## 5. Piece 2 — retire the tuples in `ResolveCardsAsync`

`ManabaseAnalysisService.ResolveCardsAsync` carries a 4-tuple and a 5-tuple as local variables. Full shapes,
the six null-forgiving operators they force, and the call-site count are in census §D. The salient facts: it
is **one private method, 20 call sites, zero test call sites**, and the tests that exercise the method reach
it by reflection on a method-name string, so they are name-coupled but not shape-coupled.

A record replaces both tuples, absorbing the position field. `DeckFlow.CLI/ManabaseCommandRunner.cs:296`
already carries a proven instance of this shape, including the `JsonIgnoreCondition.WhenWritingNull`
treatment that lets the untyped request-body slot disappear entirely; the new type follows it rather than
inventing a shape.

**Naming is a real decision, not a detail.** Three similarly-named types already exist (census §D); a fourth
called `CollectionIdentifier` would be actively confusing. The plan must choose a name that cannot be
mistaken for the Core normalization helper, the wire DTO, or the printing DTO, and must state the chosen
name before any edit.

This piece is behavior-preserving by construction and lands independently of pieces 1 and 3.

---

## 6. Piece 3 — collapse the four cache-using copies

**Goal:** one lookup component owning probe, submit, write-back and match, with the four current copies
becoming callers of it. **Zero behavior change** (D3).

Preserved exactly, because each is a decided rule with a pinning test (census §E):

- Ambiguity is scoped per response — never per call, never run-wide. Run-wide matching is a rejected option,
  not an unexplored one.
- Warm sets keep their original chunk windows and receive **both** match passes; only the cold remainder is
  pooled and re-chunked.
- The raw pass stays primary; the punctuation-tolerant pass stays additive and never overrides it.
- The accepted same-chunk warm/cold divergence stays accepted.
- `CardNormalizer.Normalize` is not adopted as a batch match key; ADR-0004:86-97 rejected it on two verified
  grounds.

**Sequencing:** piece 3 lands after piece 1, so the extraction happens on a cache whose four participants
already agree on submission shape. Extracting first would freeze the disagreement into the shared component
and make it harder to remove.

**Out of scope, explicitly:** the seven cacheless `cards/collection` callers, the two duplicated CLI runners,
and the test harnesses (census §B). They are named here so that "we covered everything" is never inferred.

---

## 7. Testing

Bug fixes are TDD, so piece 1 opens with a **characterization test that pins today's behavior before
anything moves** — two runs against one service and cache, asserting the collection call count, the fallback
call count on both runs, the stored miss, and output equality between runs. Its full shape and its mutation
check are in census §G.6. It must go red on the fix in a way that reads as the improvement (fallback calls
drop), not as breakage.

Required beyond it:

- A double-faced entry on the name branch resolves **from the collection response**, with zero repair
  searches, cold.
- The same entry, warmed by `ScryfallReferenceResolver`, issues **no** collection POST — the cross-tool
  sharing the change exists to deliver, asserted in both directions.
- Two entries sharing a front face decline to pair and both reach the repair — 4.2's contract.
- The printing branch is unaffected: an Archidekt-shaped deck produces identical calls and output.
- Output equality across cold and warm runs survives the change.
- The throttle guard test from 4.3.

`ManabaseAnalysisServiceTests.cs:2535` stubs the collection endpoint **returning a card named `A // B`**,
which ADR-0004:33 measured Scryfall never does. It pins the key namespace on an impossible response and
nothing about real behavior. It is corrected to the real `not_found` shape as part of piece 1 — corrected,
not deleted, because the key-namespace question it asks is still worth pinning.

**Every guard added here is mutation-proved**, restoring from a scratchpad copy rather than the index, and
the restore is compared byte-for-byte. A locked `DeckFlow.Web.exe` produces a build failure that reads
exactly like a caught mutation, so `error MSB` is checked for before any kill is believed.

## 8. Risks

| Risk | Handling |
| --- | --- |
| The pairing change (4.1(2)) is missed and only the submission changes | The cross-tool sharing test fails loudly; it cannot pass without the pairing change |
| SQLite-backed tests cannot prove Postgres behavior | Not applicable — no schema or Dapper mapping is touched |
| Codex review is owed but its plan window is at 92% DENY until 2026-08-25 11:53 MDT | Pieces may be written and self-reviewed, but **no piece merges before its Codex review**; the gate is deferred, not waived |
| `.planning/config.json` on `feat/cache-tier0` has `cross_ai_execution: true` while the role swap requires `false` | Flagged to the user, uncorrected — it is a tracked file, and nothing in this design runs a GSD execute command |

## 9. Follow-ups recorded, deliberately not scheduled

Both found while tracing, both real, neither belonging to this work:

1. **Front-face collision in `ScryfallCardNameIndex.TryResolve`** — an entry written `A // B` can silently
   resolve to a *different* card genuinely named `A` when that card is in the same deck, never reaching the
   repair. Candidate correctness defect, independent of caching. Census §G.5.1. Needs its own verification
   before it is called a bug.
2. **Chunk-boundary shift may move a card between priority bands** — unresolved, marked CANNOT-DETERMINE.
   ADR-0005:78-82 asserts warmth-invariance but reasons only about index precedence, not band assignment,
   and no test exercises the shape on this path. Census §G.5.2. Adjacent to piece 3; not to be fixed blind.

Also carried forward from the census and unrelated to this design: ADR-0004's throttle interval text is
stale against the shipped 500 ms value, and three of its `file:line` citations have drifted.

## 10. Open question for review

Piece 1's contract claims byte-identical output. The mechanism supports it — the same card is indexed at a
different priority band than before, since it now arrives paired rather than search-repaired, and
ADR-0005's bands rank paired above search-repair. **A card moving *up* the ladder can change which entry
wins a name collision.** The plan must therefore prove output equality on a deck containing a name collision,
not only on a clean deck, before the flag-free claim is accepted.

## 11. Review termination condition

Return CONVERGED if there are zero BLOCK and zero HIGH findings. Do not withhold convergence over
LOW-severity wording; if the only remaining findings are cosmetic, say so plainly and converge.
