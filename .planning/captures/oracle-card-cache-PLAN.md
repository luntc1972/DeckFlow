# Shared Oracle-Card Cache (former Tier 0 item 3) — Plan Draft v2

Date: 2026-08-17
Status: **DRAFT, round-1 review folded.** Awaiting re-review of the fold (§10) plus two open
decisions (§7).
Predecessors: `2026-08-17-upstream-cache-layer-research.md` (§3.1 rank 3, §6 item 3),
`2026-08-17-cache-tier0-implementation-map.md` (why this was split out of Tier 0)
Review: Codex `gpt-5.6-sol`, 4 BLOCK / 3 HIGH / 1 LOW, all folded here. Fold log at §10.

---

## 1. What this is, and what it is not

The research doc framed this as *"generalize `CutLabResolvedCardCache` into a shared oracle-card cache
consumed by all five `cards/collection` callers."* That framing had three errors, two found by reading
and one by review:

- `CutLabResolvedCardCache` is keyed on a **hash of the entire pool multiset**
  (`CutLabResolvedCardCache.cs:44`) — nothing per-card to generalize.
- The callers use **several different identity schemes**, and the repo holds **five** incompatible
  `Normalize` functions, none of which is the identifier builder.
- **There are ten production call sites, not five** (§2). Three are in `DeckFlow.CLI`.

**Reframe: this is not an identity-unification project.** Unifying the callers onto one convention
would change *which cards resolve* at several call sites, collide with a documented ADR, and convert a
caching task into a semantics migration.

**Design principle: a cache reproduces current behavior with fewer requests, never improves it.**
Anything that changes which cards resolve is separate, individually-verified work.

That principle yields the design: **cache at the wire boundary, keyed by the exact identifier
submitted to `cards/collection`.** No new normalization is invented; each caller keeps its own
pre-processing; callers that disagree on identity produce different keys, which is correct because they
receive different upstream answers today.

### What this dissolves, and the limit of that claim

`docs/decisions/0004-scryfall-batch-match-key-asymmetry.md` (with `ScryfallReferenceResolver.cs:57-64`
and `ScryfallCardResolver.cs:229-230`) makes the identifier/match-key mismatch **deliberate**:
normalization affects the submitted identifier only, never the match key, so `"A / B"` is *meant* to
miss and fall through to a fallback. `ScryfallReferenceResolverTests.cs:59` locks this.

A cache keyed on the submitted identifier sits strictly upstream of the match key. ⚠ Review
qualification: this holds **only while cached results still pass through the existing match-back and
fallback logic**. A cache that returns early and skips match-back would break the ADR just as surely
as a normalized key would. The cache must feed the existing pipeline, not bypass it.

**A cache keyed on a "normalized card name" would silently delete the asymmetry.** That is the largest
trap here, and an executor can point at five existing `Normalize` functions as justification.

---

## 2. Consumer census — ten production sites

Verified by `grep -rn 'cards/collection' --include=*.cs` across every project, excluding tests. The
round-1 draft listed five; it missed both `CardLookupService` sites and all three CLI runners.

### 2.1 In `DeckFlow.Web` — reachable by this design

| # | Site | Enclosing method | Identity submitted | Face split? | Batch | On collection non-2xx | Has a fallback a cached miss must not suppress? |
|---|---|---|---|---|---|---|---|
| 1 | `ScryfallCardResolver.cs:112` | `ResolveSingleAsync` (`:104`) | `ToFaceIdentifier(name)` | yes | 1 | **falls through** to `SearchFallbackCardAsync` (`:116-128`); fallback decides the outcome and may return a card normally | **yes** (`:128`) |
| 2 | `ScryfallReferenceResolver.cs:129` | `ResolveBatchAsync` (`:104-108`) | `ToFaceIdentifier` + `Distinct(OrdinalIgnoreCase)` | yes | 75 (`:83`) | **throws** `ScryfallReferenceCollectionException` (`:139-144`) | **yes** — per-miss fallback loop (`:178`) |
| 3 | `ManabaseAnalysisService.cs:1125` | `ResolveCardsAsync` (`:1100`) | `{set, collector}` when known, else **raw** name | **no** | 75 (`:179`) | **throws** `HttpRequestException` (`:1131-1137`) | no |
| 4 | `DeckAnalysisPacketService.cs:1793` | `LookupCommanderColorIdentityAsync` (`:1780`) | **raw** `commanderName.Trim()` | **no** | 1 | **never checks status** → `Array.Empty<string>()` (T0-D1) | no |
| 5 | `DeckConvertService.cs:132` | `NormalizeNamesAsync` (`:111`) | `ScryfallPrintingIdentifier(set, collector)` — never a name | n/a | 75 (`:34`) | non-2xx → `continue`, batch silently dropped (T0-D3) | no |
| 6 | `CardLookupService.cs:116` | batch path | name identifiers | see `:284` | 75 | **throws** | **yes** — falls back explicitly for `NotFound` (`:139`, `:156`) |
| 7 | `CardLookupService.cs:196` | single-card path | name identifier | see `:284` | 1 | **throws** | **yes** — falls back after matching `NotFound` (`:214`) |

### 2.2 In `DeckFlow.CLI` — architecturally out of reach, and why that matters

`RoleFloorResearchCommandRunner.cs:743`, `ManabaseCommandRunner.cs:181`,
`EdhrecRoleGridCommandRunner.cs:379`.

⭐ **A private `MemoryCache` in a `DeckFlow.Web` singleton is process-local, so it cannot serve a
separate CLI process — ever, regardless of how this plan is written.** These three sites are not
"deferred", they are **unreachable by an in-memory design** and stay so until a shared durable store
exists. This is the strongest available argument for the Tier 1 Postgres L2, and it should be recorded
as such rather than filed as scope.

**Decision (proposed): scope this phase to `DeckFlow.Web`.** Every "all callers" claim is struck.

### 2.3 Sites 6 and 7 are NOT already covered — question answered

Round-2 review settled this: **No.** `CardLookupCache` is consulted only *inside*
`SearchFallbackCardAsync`, i.e. **after** each collection POST has already gone out
(`CardLookupService.cs:196`, `:289`). It absorbs repeated **fallback search/named** traffic, not
repeated **collection** traffic. The collection POST at sites 6 and 7 is re-issued every time.

**Sites 6 and 7 therefore belong in the phase** (Wave 4). The hoped-for free scope reduction does not
exist.

### 2.4 Two identity spaces

Names (1, 2, 4, 6, 7 and part of 3) and printings (5 and part of 3). One namespace cannot hold both.
⚠ Precedent: the D-1 Scryfall cache-key collision needed **three** namespaces, not the two first
assumed. Review found no third shape in use today; budget for one surfacing anyway.

### 2.5 Value type — confirmed by review

`ScryfallCard` (`ScryfallDtos.cs:39`) carries every field the listed callers need and is the lossless
common denominator. **Cache `ScryfallCard`; callers map on read.** Do not cache `ScryfallCardData` as
`CutLabResolvedCardCache` does (`:21`) — it is the Manabase projection and is lossy.

---

## 3. Failure behavior, correctly stated

The round-1 draft claimed callers 1–3 "throw on non-2xx, so a failure can never reach a cache write,"
and built the wave order on it. **Caller 1 does not throw** — a collection non-2xx falls through to the
search fallback, which can return a card normally (`ScryfallCardResolverTests.cs:99` proves HTTP 500 →
successful search → card returned).

The write contract, not the throw behavior, is what provides safety: **never write on a non-2xx, an
exception, a partial batch, or an identifier's mere absence.** Under that contract every caller is
safe, including 4 and 5, because a caller-produced empty result never reaches the cache at all.

**Consequence (review HIGH-7): the T0-D1 / T0-D3 status-check fixes are NOT a prerequisite of this
work.** They are independent correctness bugs — real, worth fixing, and untested on their failure
branches — but they do not gate any wave. The round-1 wave order was over-constrained.

---

## 4. Design

### 4.1 The store

One service in `DeckFlow.Web/Services/Scryfall/`, registered **singleton**, injected — never
constructed at a call site (`CutLabPageService.cs:174` shows how that silently defeats a cache).
Two prefixed namespaces: name-space keyed by the submitted name identifier, printing-space keyed by
the submitted `(set, collector_number)`.

⛔ **Not a `DelegatingHandler`.** Research §4 rejected it with evidence: zero `DelegatingHandler`s are
registered in `DeckFlow.Web` and eight clients bypass `IHttpClientFactory`. The seam is the service
layer.

### 4.2 Negative caching — restated after review

The round-1 draft said `NotFound` means "upstream confirmed absent" and that no caller reads it. **Both
were wrong**, and the first was dangerous.

- **`NotFound` means "the collection endpoint did not match this identifier" — nothing more.** Callers
  1 and 2 deliberately proceed to a search fallback afterward (`ScryfallCardResolver.cs:128`,
  `ScryfallReferenceResolver.cs:178`). Model and name the cached entry as a **collection miss**, never
  as an absent card. **A cached collection miss must suppress only the collection POST and must still
  invoke the fallback.** Getting this wrong silently loses cards that resolve today.
- **`NotFound` is already read** — `CardLookupService.cs:139` and `:214`, plus `ManabaseCommandRunner.cs:196`
  in the CLI; the ADR records the precedent at `0004-...md:73`. Adopt the existing semantics and tests
  rather than introducing it as new behavior.
- **`ScryfallCollectionIdentifier` carries only `Name`** (`ScryfallDtos.cs:86`) — no set or collector
  number. So a printing-space miss **cannot be correlated** from the current DTO.
  **Proposed: name-space negatives only; printing-space is positive-only.** The alternative — extend
  the DTO with the printing fields and test that Scryfall echoes them — is more work for the caller
  that needs it least.

Write a collection miss only from an explicit 2xx whose `NotFound` names that identifier. Never from a
non-2xx, an exception, an absence from `Data`, or a partial/short-circuited batch.

Precedent to copy, not reinvent: `CutLabAnalysisContextBuilder.cs:342-372` already subtracts
429-casualty names before a cache write, and `:502-523` keeps rate-limited names out of the
known-missing set. Review confirmed a per-card cache beneath it does not disturb that guard.

### 4.3 Bounding and TTL

Bounded private `MemoryCache` with an explicit `SizeLimit` — never the shared `IMemoryCache`, which has
no `SizeLimit` and so can never evict on memory pressure. Pattern: `CutLabResolvedCardCache.cs:32-36`,
`PacketSessionCache.cs:44,106`.

Carry the Tier 0 review lesson (R-1): **`Size` must account for the stored payload, not the key alone,
and the capacity constant must state its unit in its name.** A `ScryfallCard` dwarfs the identifier
keying it.

**Settled numbers (measured, not assumed — see §7 for the method):**

- **Positive TTL: 24 hours.** Not a new number — `CardLookupCache.cs:73` already uses 24 h for exactly
  this data class (Scryfall card data). Adopt the existing convention.
- **Collection-miss TTL: 1 hour.** Matches `CardLookupCache.cs:88`.
- **Capacity: 10 M characters** (~17,000 cards at the measured mean of 588; ~13,000 at p95 776). In line
  with the neighbours: `PacketSessionCache.cs:28` 10 M, `CutLabDeltaCache.cs:13` 5 M,
  `CutLabResolvedCardCache.cs:15` 20 M.

⚠ Round-2 correction: an earlier draft argued 24 h and 7 days are "indistinguishable because
`autoDeploy: true` redeploys daily." **`autoDeploy: true` (`render.yaml:22`) only means deploys follow
qualifying repository changes — it establishes no cadence**, and deploy frequency has not been measured.
24 h is adopted because it is the established convention for this data class, full stop. The
uptime argument is withdrawn.

⚠ One semantic difference to respect: `CardLookupCache`'s 1 h negative represents **complete fallback
failure** (`CardLookupCache.cs:68,:83`), whereas this cache's negative is only a **collection miss**.
The number is borrowed; the meaning is not the same. Do not let the shared TTL imply shared semantics.

⚠ Naming: the existing caches call the constant `CacheCapacityBytes` but their size functions sum
string **lengths in chars** (`CutLabResolvedCardCache.cs:156`), so those names are a pre-existing
misnomer. Tier 0 shipped the accurate `...CapacityChars`; use that here and accept the divergence from
the older name rather than propagating the error.

### 4.4 `CutLabResolvedCardCache` — leave it alone

It answers *"has this exact pool been resolved?"* and stores known-missing names; the new cache answers
*"has this identifier been resolved?"* They compose, new cache beneath. **Do not merge them or migrate
Cut Lab in this phase** — its negative-caching tests (`CutLabAnalysisContextBuilderTests.cs:470`,
`CutLabPageServiceTests.cs:545`, `:2142`, including an explicit anti-poison test) are the best assets in
this area and multiplying blast radius buys no hits.

`CutLabPageService.cs:174` constructing its own instance instead of the singleton (T0-D4) is out of
scope — but it is the cautionary example for §4.1.

---

## 5. Proposed waves

**Wave 1 — the store plus sites 1 and 2.** Both submit `ToFaceIdentifier`. Both have fallbacks, so
this wave proves the hardest property first: a cached collection miss skips the POST and still runs the
fallback. Deliverable: repeated resolution of one card across a packet build and a single resolve
issues one upstream batch.

**Wave 2 — site 3.** First consumer of both namespaces (`:1116-1118`). After the key contract settles.

**Wave 3 — sites 4 and 5**, printing-space for 5. No longer blocked on anything (§3).

**Wave 4 — sites 6 and 7.** Confirmed in scope: `CardLookupCache` sits inside the fallback, after the
collection POST, so it does not absorb collection traffic (§2.3). Both sites already consume `NotFound`
and already fall back, so they are the best available check that the cached-miss contract matches
existing behavior rather than replacing it.

**Not in this phase:** the three CLI sites (§2.2 — impossible in-memory), T0-D1/T0-D2/T0-D3 (independent
correctness work), migrating Cut Lab, `IUpstreamHttpClient` (research §6 item 6), Postgres L2, bulk
ingest.

⚠ **This phase is the latency half, not the ban fix.** Memory-only plus `autoDeploy: true` means every
deploy empties the cache — the cold-burst shape behind the 2026-07-30 429 incident — and it cannot
touch the CLI at all. Tier 1 is the ban fix. Report it that way or the goal looks met when it isn't.

---

## 6. Tests required (explicit case list)

Each guard names its mutation; a test that passes with the guard removed is worthless.

1. Two resolutions of the same name identifier issue **one** upstream batch.
2. An identifier differing only by a caller's own pre-normalization gets a **separate** entry — proves
   the cache does not silently unify callers.
3. **ADR-0004 preserved**: `"A / B"` still misses and still reaches the fallback with the cache active.
   Mutation: switching the key to a normalized name must fail this. Guards
   `ScryfallReferenceResolverTests.cs:59`.
4. ⭐ **A cached collection miss still invokes the fallback** and skips only the collection POST.
   Mutation: making a cached miss return early must fail this. This is the review's HIGH-6 and the most
   important test in the set.
5. A 2xx naming an identifier in `NotFound` caches a collection miss; a second lookup does not re-POST.
6. **A 429 does not cache anything** — the next lookup retries. Mutation: deleting the status gate must
   turn this red.
7. A non-2xx mid-batch leaves **no** entry for any identifier in that batch.
8. A collection miss expires on its short TTL while a positive entry survives.
9. Name and printing namespaces cannot collide.
10. An oversized entry is not retained **and** a normal entry is retained (positive control — the Tier 0
    R-1 lesson, where the oversize test passed only because sizing was wrong).
11. DI-graph resolution still succeeds (`DiCompositionExtensionsTests`) — Tier 0 proved this is where an
    unregistered dependency surfaces, at startup rather than at build.

⚠ SQLite-backed tests cannot prove Postgres column types, and Docker is off in WSL so `[PostgresFact]`
cases silently **skip** — a false green. Irrelevant while memory-only; decisive the moment Tier 1 lands.

---

## 7. Decisions — all settled

**D1. Scope = `DeckFlow.Web` only. SETTLED.** A private `MemoryCache` inside a Web singleton is
process-local, so it cannot serve the three `DeckFlow.CLI` sites at any capacity or TTL. They are
excluded because they are unreachable from this design, not because they are unimportant.

⚠ Round-2 correction — an earlier draft claimed "all CLI Scryfall traffic is uncached" and that the CLI
runners are "the repo's highest-volume consumers, entirely unprotected." **Both are false.** The two
bulk runners already maintain **persistent JSON caches**, submit only `uncachedNames`, and write
resolved cards back (`RoleFloorResearchCommandRunner.cs:728`, `EdhrecRoleGridCommandRunner.cs:352,:440`);
`ManabaseCommandRunner` submits only one deck's distinct identifiers (`:74`). The accurate statement is
narrow: **the Web cache cannot serve CLI processes, so cold / newly-encountered-name research traffic
and the single-deck runner sit outside it.** Do not use CLI volume as an argument for Tier 1 — the CLI
already has disk-backed caching of its own.

**D2. Capacity and TTLs. SETTLED** — 24 h positive / 1 h miss / 10 M chars, per §4.3.

Method: `_calib/cards_full.json` (6,717 cards) projected down to the fields `ScryfallCard` declares,
**with `card_faces` recursively reduced to the six fields of `ScryfallCardFace`** (`ScryfallDtos.cs:39`,
`:75`), each projection then serialized:

| | chars |
|---|---|
| Full Scryfall object | mean 5,444 · median 5,310 · p95 6,072 |
| **`ScryfallCard` projection (what we cache)** | **mean 588 · median 571 · p95 776 · max 1,532** |

⚠ Round-2 corrections to this measurement, both material:

1. **The first attempt did not recurse into `card_faces`**, so raw face objects — `image_uris`, artist,
   colors — were counted. That inflated the mean to 634 and the max to 4,296. The nested projection above
   is the correct one. Publish the field list and script with any future re-measurement.
2. **`_calib/cards_full.json` is not an oracle-corpus sample.** It is a recent top-cEDH *workload* cache,
   populated from size-tiered EDHTop16 decks and holding only names occurring in them
   (`scripts/cedh-baseline/fetch.py:29,:88,:373`). All 6,717 entries are Commander-legal and biased toward
   recently-played cEDH cards; **the size bias relative to the full corpus cannot be determined from this
   repository.** Serialized length also excludes live CLR object/list/cache overhead.

⛔ **Therefore the "~30k corpus ≈ 36 MB, so memory is not the binding constraint" extrapolation is
withdrawn.** It was built on a workload sample treated as a corpus sample. Research §5's sizing stands
unrefuted; this measurement is valid **only** for sizing a demand-filled cache, which is exactly what is
being sized here — so the 10 M-char capacity survives, but the conclusion about Tier 1's rationale does
not. Re-deriving it needs an oracle bulk file and live-memory accounting.

⚠ Still not measured: *hit rate*. These numbers size the cache; they say nothing about how often it is
hit. Research §3.1's rankings remain underived, so this phase's latency benefit stays an estimate. One
instrumented packet-build run would settle it — worth doing before declaring the phase a success, not
before starting it.

*(Round-1 decision on two namespaces: settled, review found no third shape. Round-1 decision on
including the T0 defect fixes: dissolved, they were never a prerequisite.)*

## 8. Risks

- **Highest: a "tidy" key.** Replacing the submitted-identifier key with a normalized card name deletes
  ADR-0004's documented behavior, and five existing `Normalize` functions are available as
  justification. Test 3 catches it.
- **Second: treating a collection miss as an absent card.** Suppresses fallbacks that resolve cards
  today. Test 4 catches it.
- **Negative caching is the outage path.** Every rule in §4.2 must hold simultaneously.
- **Seven Web sites across six service classes** — larger than a Tier 0 item. Expect review to focus on
  §4.2.

## 9. Normalization census — five functions, none the identifier builder

`CardNormalizer.Normalize` (`CardNormalizer.cs:15`, truncates at front face, strips punctuation; wrapped
by `CutLabCardNames.Normalize` `:13`, which keys the Cut Lab cache); `ScryfallCardResolver.NormalizeLookupName`
(`:213`, keeps punctuation and slashes); `ScryfallCardNameIndex.Normalize` (`:115`, bare
`Trim().ToLowerInvariant()`); `ScryfallReferenceResolver.BatchMatchKey` (~`:230`, ADR-0004
slash-preserving); **`CardNameNormalizer.Normalize` (`CardLookupService.cs:491`, wrapped by
`NormalizeName` `:284`, used to match results *and* `NotFound` identifiers)** — the fifth, found by
review. Comparers disagree too: `Ordinal` over pre-lowercased values (`CutLabCardNames.cs:7`) vs
`OrdinalIgnoreCase` (`ScryfallReferenceResolver.cs:120,121,127,149`).

The identifier builder is separate: `ScryfallCollectionIdentifier.ToFaceIdentifier`.

Round-2 addition: a **sixth** named function exists, `ScryfallCardResolver.NormalizeForScryfall`
(`:241`), but it governs **search/named fallback submission**, not collection match-back, so it does not
affect the cache-key design. State the census as **five match/equality normalizers plus one
fallback-submission normalizer**.

## 10. Fold log — round 1 (Codex `gpt-5.6-sol`)

| # | Sev | Finding | Fold |
|---|---|---|---|
| 1 | BLOCK | Census claimed 5 callers; 10 production sites exist | §2 rebuilt: 7 Web + 3 CLI, all "all callers" claims struck, CLI declared unreachable in-memory (§2.2) |
| 2 | BLOCK | Caller 1 does not throw on non-2xx; falls to search fallback | §2 table column replaced; §3 rewritten — safety comes from the write contract, not throw behavior |
| 3 | BLOCK | `ScryfallCollectionIdentifier` has only `Name`, so printing-space negatives cannot be correlated | §4.2: name-space negatives only; printing-space positive-only |
| 4 | BLOCK | `NotFound` is already read (`CardLookupService.cs:139,:214`) | §4.2: "new behavior" claim struck, existing semantics adopted |
| 5 | HIGH | Fifth normalizer `CardNameNormalizer.Normalize` omitted | §9 census added, count corrected to five |
| 6 | HIGH | `NotFound` is a collection miss, not an absent card; fallback must still run | §4.2 renamed the concept; new test 4 with its mutation |
| 7 | HIGH | Wave 3 is not a cache-safety prerequisite | §3 + §5: T0-D1/D3 reclassified as independent work; wave order simplified |
| 8 | LOW | "both methods untested" → their failure branches are untested | §3 wording corrected |

Review also confirmed, and these are now load-bearing: `ScryfallCard` is the lossless denominator; only
two identifier shapes are in use; ADR-0004 survives identifier-keying **provided cached results still
pass through match-back/fallback**; per-card caching beneath Cut Lab does not disturb the
unattempted-name guard (`CutLabAnalysisContextBuilder.cs:362-372`).

## 10b. Fold log — round 2 (Codex `gpt-5.6-sol`) — **CONVERGED: 0 BLOCK, 0 HIGH**

| # | Sev | Finding | Fold |
|---|---|---|---|
| 1 | MED | Measurement did not recurse into `card_faces`, so face `image_uris`/artist/colors were counted; mean was inflated 634 (max 4,296) vs correct 588 (max 1,532) | §7 D2 re-measured with nested projection; both figures published |
| 2 | MED | `_calib/cards_full.json` is a top-cEDH **workload** cache, not an oracle-corpus sample (`fetch.py:29,:88,:373`); corpus size bias undeterminable from this repo | §7 D2: the 30k / "memory not binding" extrapolation **withdrawn**; research §5 stands unrefuted; sample retained only for demand-cache sizing |
| 3 | MED | "All CLI traffic uncached / highest-volume unprotected" false — bulk runners keep persistent JSON caches and submit only `uncachedNames` | §7 D1 corrected to the narrow true claim; CLI volume removed as a Tier 1 argument |
| 4 | MED | `autoDeploy: true` establishes no deploy cadence, so the 24 h ≈ 7 d argument is unsupported | §4.3: uptime argument withdrawn; 24 h kept purely as the established convention |
| 5 | LOW | Sites 6–7 uncharacterized | §2.1 rows completed: both throw, both fall back on `NotFound` |
| 6 | LOW | Sixth normalizer `NormalizeForScryfall` (`:241`) omitted | §9 addition; does not affect key design |
| 7 | LOW | "five services" → six service classes | §8 corrected |
| — | — | **§2.3 answered: NO.** `CardLookupCache` sits inside `SearchFallbackCardAsync`, after the collection POST (`:196`, `:289`) — absorbs fallback traffic, not collection traffic | §2.3 rewritten; sites 6–7 confirmed in Wave 4 |

Verified without findings in round 2: exactly ten production `cards/collection` POST sites, 7 Web / 3 CLI,
correctly attributed; §3's write contract is safe per site; §4.2's collection-miss semantics match
`CardLookupService`'s existing `NotFound` handling; the 24 h / 1 h TTL citations are correct; the
10 M / 5 M / 20 M neighbour constants and their chars-named-Bytes misnomer are confirmed; no section
retained the withdrawn size rationale.

⭐ Pattern worth carrying: three of the four MEDIUMs were **claims amplified beyond their evidence** — an
extrapolation from a biased sample, an unmeasured volume assertion, and an unmeasured cadence assertion.
None were coding errors; all were rhetorical strengthening of an argument that was already sufficient.

## 11. For the re-reviewer

**Re-review the FOLD (§10 and the sections it names), not the whole plan.** Verify against the repo:
the ten-site census is complete and correctly attributed; §3's failure-behavior restatement matches
each site; §4.2's collection-miss semantics match what `CardLookupService` already does with
`NotFound`; the §9 census is now complete at five; and §2.3's question — whether `CardLookupCache`
already absorbs sites 6–7 — is answered either way.

Return CONVERGED if there are zero BLOCK and zero HIGH findings. Do not withhold convergence over
LOW-severity wording; if the only remaining findings are cosmetic, say so plainly and converge.
