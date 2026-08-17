# Cache Tier 0 — Implementation Map and Side Effects Report

Date: 2026-08-17
Branch: `feat/cache-tier0` (worktree `../deckflow-cache-tier0`), based on `main` @ `e3718f97`
Source spec: `2026-08-17-upstream-cache-layer-research.md` §6 Tier 0
Scope approved by user: items 1–3, no measurement pass first.

Build baseline in this worktree: **0 errors, 15 warnings** (9× `CS8629` nullable, plus `NU1903`
package-advisory). Both pre-existing; any new code must not add to either count.

---

## Headline: the research doc was wrong about item 3

Tier 0 item 3 said "generalize `CutLabResolvedCardCache` into a shared oracle-card cache consumed by
all five `cards/collection` callers", and rated it localized, no-new-infrastructure work.

Reading the code, it is none of those things:

1. **`CutLabResolvedCardCache` is not a per-card cache.** Its key is a hash of the *entire pool
   multiset* (`CutLabResolvedCardCache.cs:44` `ComputePoolKey`, hashed via
   `PacketSessionCache.ComputeKey` at `:54`) and its value is the whole resolved list for that exact
   multiset (`:21` `CachedEntry(IReadOnlyList<ScryfallCardData> Cards, IReadOnlySet<string>
   MissingCardNames, int SizeBytes)`). Generalizing it to per-oracle-card keys is a key-convention
   redesign, not a lift-and-shift.
2. **The five callers share no identity convention.** Four different schemes:

   | # | Caller | Identity sent | Face split? |
   |---|---|---|---|
   | 1 | `ScryfallCardResolver.cs:112` | `ToFaceIdentifier(cardName)` | yes |
   | 2 | `ScryfallReferenceResolver.cs:129` | `ToFaceIdentifier` + `Distinct(OrdinalIgnoreCase)` | yes |
   | 3 | `ManabaseAnalysisService.cs:1125` | `{set, collector_number}` when known, else **raw** `entry.Name` | **no** |
   | 4 | `DeckAnalysisPacketService.cs:1793` | **raw** `commanderName.Trim()` | **no** |
   | 5 | `DeckConvertService.cs:132` | `ScryfallPrintingIdentifier(set, collector)` — **never a name** | n/a |

3. **Three-to-four mutually incompatible "normalize" functions coexist**, and none of them is the
   identifier builder: `CardNormalizer.Normalize` (`CardNormalizer.cs:15`, truncates at front face,
   strips punctuation) used by the cache key itself via `CutLabCardNames.Normalize`
   (`CutLabCardNames.cs:13`); `ScryfallCardResolver.NormalizeLookupName` (`:213`, keeps punctuation
   and slashes); `ScryfallCardNameIndex.Normalize` (`:115`, bare `Trim().ToLowerInvariant()`);
   `ScryfallReferenceResolver.BatchMatchKey` (~`:230`, slash-preserving, ADR-0004).
   Comparers disagree too — cache is `Ordinal` over pre-lowercased values (`CutLabCardNames.cs:7`),
   `ScryfallReferenceResolver` is `OrdinalIgnoreCase` throughout (`:120,121,127,149`).
4. **The asymmetry is load-bearing and deliberate.** `docs/decisions/0004-scryfall-batch-match-key-asymmetry.md`
   plus `ScryfallReferenceResolver.cs:57-64` and `ScryfallCardResolver.cs:229-230`: normalization
   affects the submitted identifier only, never the match key, so `"A / B"` is *meant* to miss and
   fall through to the fallback. A shared cache key that collapses this silently deletes a
   documented behavior, and `ScryfallReferenceResolverTests.cs:59` locks it.
5. **Caller 5 cannot participate in a name-keyed cache at all** — it is printing-keyed only and skips
   entries lacking set+collector (`DeckConvertService.cs:113-117`).
6. **No shared resolved-card type.** `ScryfallCard` (`ScryfallDtos.cs:39`) is the only common
   denominator; the existing cache stores `ScryfallCardData` (the *Manabase* shape), which is lossy —
   e.g. `ColorIdentity`, the single field caller 4 needs, is absent from `EstimateSizeBytes`'s field
   list (`CutLabResolvedCardCache.cs:161-181`).

**Conclusion: item 3 is a normalization-unification project, not a cache.** It needs its own plan,
and its real prerequisite is picking one card-identity convention across five services. Recommend
splitting it out of Tier 0. Items 1 and 2 are genuinely localized and unaffected.

---

## Latent defects found while mapping (independent of caching)

- **T0-D1 — `DeckAnalysisPacketService.cs:1793-1806` never checks the status code.** A 429 yields
  `response.Data == null`, which returns `Array.Empty<string>()` — reported as "commander has no
  color identity". A silent, 429-derived wrong answer. No test covers this method.
- **T0-D2 — same site sends `commanderName.Trim()` with no face split.** A DFC or partner commander
  written `"A // B"` returns `not_found`, contradicting the rule documented at
  `ScryfallCollectionIdentifier.cs:5`.
- **T0-D3 — `DeckConvertService.cs:136-139` swallows non-2xx with `continue`.** The whole batch's
  canonical names are silently dropped and entry names left unrewritten. No test covers this path.
- **T0-D4 — `CutLabPageService.cs:174` bypasses DI**: `CutLabResolvedCardCache sharedResolvedCardCache
  = new();` constructs a per-call instance instead of the registered singleton
  (`CutLabServiceCollectionExtensions.cs:20`), so that call site gets no cross-request cache hits at
  all. Possibly deliberate; unverified either way.
- **T0-D5 — `TaggerSessionCache.cs:92-99` writes to the app-wide shared `IMemoryCache` with no
  `Size`**, which is the Tier 0 item 4 exposure in miniature.

None of these are in the approved scope. Recorded here so they are not rediscovered.

---

## Item 1 — Tagger name → (set, collector number) cache

Correction to the research doc: it cited "`cards/named` then `POST graphql`, **twice per card**" at
`:154` and `:297`. In fact **`:297` is the GraphQL POST**, not a second `cards/named`. There is
exactly one `cards/named` call in the Tagger path (`ScryfallTaggerLookupService.cs:154`). The win is
real but singular.

**Target:** `ScryfallTaggerLookupService.cs:150`

```csharp
private async Task<(string Set, string CollectorNumber)> ResolveCardPrintingAsync(string cardName, CancellationToken cancellationToken)
```

- Extraction: `"set"` at `:174`, `"collector_number"` at `:175`.
- Sole call site: `:105`. Public entry `LookupOracleTagsAsync` at `:92`; single production consumer
  `CategorySuggestionService.cs:110`.
- Throttle: `ScryfallThrottle.ExecuteAsync` wraps **only** this call (`:157-161`), so a cache hit
  bypasses the process-wide 500 ms gate. That is the latency win.
- Host lifetime: **singleton** (`ScryfallServiceCollectionExtensions.cs:77`), so a private
  `MemoryCache` field is viable with no DI change.

**Guard rail (mandatory):** every failure path returns `(string.Empty, string.Empty)` — non-success
HTTP at `:163-169`, missing JSON property at `:174-175` — so a 429 is *indistinguishable* from a
genuine miss. **Cache only when both values are non-empty.** This is the Phase 111.1 B-1 blocker
arriving through a different door.

**Pattern to copy:** bounded private `MemoryCache` with an explicit `SizeLimit`, as in
`CutLabResolvedCardCache.cs:32-36` and `PacketSessionCache.cs:44,106`. Do **not** add to the shared
`IMemoryCache` — it has no `SizeLimit`, so it can never evict on memory pressure.

## Item 2 — EDHREC per-card cache + one shared `RestClient`

**Blocking constraint:** `EdhrecCardLookup` lives in `DeckFlow.Core/Integration/EdhrecCardLookup.cs`,
and `DeckFlow.Core.csproj` has **no `Microsoft.Extensions.Caching.Memory` reference**. The design
doc's prescription — "`IMemoryCache` keyed by commander slug, long TTL"
(`.planning/captures/cutlab-add-edhrec-role-card-design.md:17`) — therefore cannot be applied in
place without a new package, which needs explicit approval.

**Chosen shape (no new package):** extract an interface, cache in the Web layer.

- `IEdhrecCardLookup` alongside the Core class; `EdhrecCardLookup` (`:11`, `sealed`, currently
  implements nothing) implements it. Its only public instance method is
  `:33 public async Task<IReadOnlyList<string>> LookupCategoriesAsync(string cardName, CancellationToken cancellationToken = default)`.
- A caching decorator in `DeckFlow.Web`, where `AddMemoryCache()` is already registered
  (`Program.cs:71`) — but per item 1, use a **bounded private** `MemoryCache`, not the shared one.
- **Cache key = `EdhrecCardLookup.Slugify(cardName)`** (`:87-97`) — already lowercase-canonical.
  Not the raw name, which arrives case-preserved from `CategorySuggestionService.cs:99`.
- Register the transport as a **singleton** so the `RestClient` built at `:21-25` is created once
  instead of per call, replacing `new EdhrecCardLookup()` at `CategorySuggestionService.cs:128`.
  `CategorySuggestionService` is `AddScoped` (`Program.cs:180`).

**Guard rail (mandatory):** `LookupCategoriesAsync` returns `[]` for *everything* — 404, 429, 500,
timeout (`:44-47`, no status inspection anywhere), missing `panels` (`:52-55`), and genuine
no-tags (`:70`). There is **no 429 handling in this file at all**. So: **cache only non-empty
results.** Never store `[]`. This matches the existing downstream contract, which persists only when
`Count > 0` (`CategorySuggestionService.cs:131`).

**Bonus this unlocks:** there is currently **no test double for EDHREC anywhere**, because
`new EdhrecCardLookup()` is hardcoded. `LookupCategoriesAsync` has never been exercised by a test;
existing tests avoid the network only by keeping the `:127` guard false. The interface creates the
seam.

**Out of scope, flag as follow-up:** routing EDHREC through the RestSharp + Polly named-pipeline
pattern and adding a contact User-Agent, both also called for at
`cutlab-add-edhrec-role-card-design.md:17`. Caching is the approved ask; pipeline work is separate.

---

## Side Effects Report (items 1 and 2 only)

**Files/modules affected (direct):**
- `DeckFlow.Web/Services/Scryfall/ScryfallTaggerLookupService.cs` — private cache field + hit/store
  around `ResolveCardPrintingAsync`.
- `DeckFlow.Core/Integration/EdhrecCardLookup.cs` — implement new interface (no behavior change).
- New: `IEdhrecCardLookup` (Core), caching decorator (Web).
- `DeckFlow.Web/Services/CategorySuggestionService.cs` — ctor gains `IEdhrecCardLookup`; `:128`
  `new EdhrecCardLookup()` removed.
- DI registration for both EDHREC types (`Program.cs` or a `*ServiceCollectionExtensions.cs`).

**Files/modules affected (transitive):**
- `DeckFlow.Web/Controllers/DeckCategoriesController.cs:111` and
  `Controllers/Api/SuggestionsApiController.cs:82` — the two `CategorySuggestionService` consumers.
  Resolved by DI, so no source change expected, but they are the live blast radius.
- `DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs:35` resolves the real DI graph — any
  unregistered new dependency fails **here** first.
- `DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs:23` constructs the service directly (ctor at
  `:70-80`), so the signature change breaks it until a fake is added.

**Shared state touched:**
- Two **new** private bounded `MemoryCache` instances, both process-local, both inside singletons.
- The app-wide shared `IMemoryCache` is **deliberately not touched** (no `SizeLimit`, cannot evict on
  pressure — see T0-D5).
- `ScryfallThrottle`'s static gate is not modified; cache hits simply no longer reach it.

**External surfaces (DB / API / FS / config):**
- No schema, no migration, no new config key, no new env var. Fewer outbound requests to
  `api.scryfall.com` and `json.edhrec.com`. No new package, so no lockfile change.

**Contract changes (signatures, errors, perf, ordering):**
- `CategorySuggestionService` constructor gains a parameter — internal composition only, not a public
  API of the app.
- `EdhrecCardLookup` gains an interface; existing signatures unchanged.
- Error shape unchanged: failures still degrade to `[]` / `(empty, empty)` and are now explicitly
  *not* cached, so behavior on failure is identical to today, just not persisted.
- Ordering: `LookupCategoriesAsync` returns a list; the cache must return an equivalent list, and
  must not hand out a mutable reference that a caller could mutate into the cached entry.

**Tests requiring updates or additions:**
- Update `CategorySuggestionServiceTests.cs` for the new ctor param + add a `FakeEdhrecCardLookup`
  (naming per convention: `Fake*` stateful).
- New: Tagger cache hit avoids a second `cards/named` request; failure tuple is **not** cached and is
  retried on the next call (mutation-provable).
- New: EDHREC decorator caches non-empty by slug; `[]` is never cached; two differently-cased names
  with the same slug share one entry.
- New: size-limit behavior for both, mirroring `CutLabResolvedCardCacheTests.cs:80,104`.
- Existing `ScryfallTaggerLookupServiceTests.cs:93`
  (`LookupOracleTagsAsync_WarmCache_SkipsCsrfLeg_RefetchesRestAndGraphQL`) **asserts the REST leg is
  re-fetched** — that is precisely what item 1 changes. This test must be revisited deliberately, not
  quietly relaxed.

**Backward compatibility risks:**
- Stale printing data if a card's set/collector mapping changed upstream. Immutable in practice for a
  given name, but a TTL bounds the exposure rather than relying on that.
- No persisted state, so nothing to migrate and a rollback is a code revert.

**Open questions / assumptions:**
- TTL and size cap for both caches: assuming a long TTL (hours) and a small explicit `SizeLimit`,
  following the `CutLabResolvedCardCache` precedent, since both payloads are tiny relative to a
  resolved card list.
- Rankings in the source research were derived from call shape, not measured; user accepted skipping
  an instrumented run.

---

## Decisions taken (user, 2026-08-17)

1. **Scope = items 1 and 2.** No instrumented measurement pass first.
2. **Item 3 split out of Tier 0.** Its real deliverable is choosing one card-identity convention
   across five services and reconciling four `Normalize` functions against ADR-0004 — a planned
   phase, not a cache task. Do not re-attempt it as "generalize the Cut Lab cache".
3. **T0-D1 … T0-D5 logged, not fixed.** Recorded above so they are not rediscovered.

---

## Round 1 implementation (Codex `gpt-5.6-terra`, uncommitted on `feat/cache-tier0`)

Files: `ScryfallTaggerLookupService.cs` (printing cache), `IEdhrecCardLookup.cs` (new, Core),
`EdhrecCardLookup.cs` (implements it), `Services/Edhrec/CachingEdhrecCardLookup.cs` (new decorator),
`CategorySuggestionService.cs` (ctor injection, `new EdhrecCardLookup()` removed), `Program.cs:180-181`
(singleton concrete + decorator factory), plus three test files.

Verified independently by the lead, not taken on the dispatch's word:

- **Fence held** — exactly the permitted files, nothing else.
- **Zero EOL churn** — `git diff --stat` byte-identical to `git diff --ignore-all-space --stat`.
- **No new `PackageReference`** — no `.csproj` in the diff. `DeckFlow.Core` stayed free of
  `Microsoft.Extensions.Caching.Memory`, which was the constraint that dictated the decorator shape.
- **Build 0 errors / 15 warnings** — baseline exactly, nothing new.
- **Full suite 4778 passed / 0 failed / 20 skipped**, up from 4768. Codex had run only 18 focused
  tests; the full run was the lead's.
- **`DiCompositionExtensionsTests` confirmed executed** (filtered run, 1 passed) — not one of the 20
  skips. This clears the live risk that `AddSingleton<EdhrecCardLookup>()` could not resolve a type
  whose only ctor is `EdhrecCardLookup(RestClient? restClient = null)` with `RestClient` unregistered;
  `ActivatorUtilities` does honour the default parameter value. Had it not, the failure would have
  been at **app startup**, invisible to both the build and the focused tests.
- **Guard rails present and correct** — Tagger caches only when set *and* collector number are
  non-empty (`:187`); EDHREC returns early without caching on `Count == 0` (`:36`); both hand out
  defensive copies so a caller cannot mutate a cached entry.

## Review findings (lead, round 1)

- **R-1 (MEDIUM, real defect).** Both caches set `SizeLimit = 256` and `Size = cacheKey.Length`. The
  size metric counts only the **key** and ignores the value, and at a 256 budget with ~15–25-char card
  names each cache holds only ~10–17 entries. A cache whose purpose is accumulating immutable data
  across requests, capped at ~13 entries, forfeits most of the win. Compare
  `CutLabResolvedCardCache.cs:15` (`20_000_000`) and `:156` (`EstimateSizeBytes` sums the payload).
  Aggravating: both `..._LargerThanCacheLimit_IsNotRetained` tests overflow the cache with an absurdly
  long card **name**, so they pass *because* of the defect — they assert the bad metric rather than
  real overflow behavior, and have no positive control proving a normal entry is retained.
- **R-2 (LOW).** Tagger key is `cardName.ToUpperInvariant()` with no `Trim()`, so `" Sol Ring"` and
  `"Sol Ring"` occupy separate entries.
- **R-3 (LOW, style).** `Program.cs:181` constructs the decorator with a fully-qualified inline type
  instead of a `using`.

Round 2 dispatched fresh (not `codex exec resume` — resume silently drops the `-m` seat, and round 1
had already run 38 turns against the 40-turn cap).

## Round 2 — all three findings discharged

- **R-1 fixed.** Constants renamed to state their unit: `PrintingCacheCapacityChars = 200_000`
  (`ScryfallTaggerLookupService.cs:43`), `CacheCapacityChars = 1_000_000`
  (`CachingEdhrecCardLookup.cs:13`). `Size` now covers the whole entry with a floor of 1 —
  `Math.Max(cacheKey.Length + set.Length + number.Length, 1)` (Tagger `:193`) and
  `Math.Max(cacheKey.Length + cachedCategories.Sum(c => c.Length), 1)` (EDHREC `:46`).
- **R-1 tests rewritten.** Both oversize tests now use a normal card name and overflow on the
  **payload** (an oversized `set` value / an oversized category string sized off the capacity
  constant). Retention counterparts (`LookupCategoriesAsync_WarmCache_UsesOneUpstreamRequest`,
  `LookupOracleTagsAsync_CasingVariant_ReusesPrintingResolution`) mean neither can pass by the cache
  storing nothing at all.
- **R-2 fixed.** `cardName.Trim().ToUpperInvariant()` (`:157`); casing test extended to whitespace.
- **R-3 fixed.** `using DeckFlow.Web.Services.Edhrec;` (`Program.cs:23`), qualification dropped at
  `:182`; lifetimes unchanged.

### Verification (lead)

- Build **0 errors / 15 warnings** — baseline held.
- Full suite **4778 passed / 0 failed / 20 skipped**, unchanged from round 1.
  ⚠ Codex reported "full-suite verification could not complete: runs hung after test discovery".
  That is the **known false WSL runner artifact**, not a real stall — the lead's own run completed
  normally. Do not chase it.
- Fence still exact; **zero EOL churn** (`--stat` identical to `--ignore-all-space --stat`); no
  `.csproj` change.
- ⭐**Mutation-proved the load-bearing guard rail.** Deleting the `categories.Count == 0` early return
  at `CachingEdhrecCardLookup.cs:37-40` turned the suite red on exactly one test
  (`LookupCategoriesAsync_EmptyResult_IsRetried`, 1 failed / 3 passed); restoring it returned 13/13
  green. The guard that stops a 429 being cached as "this card has no categories" is genuinely
  protected by a test, not merely accompanied by one.

### Not done

- `/simplify` was not run as a command; the lead reviewed the diff for simplification by hand. One
  deliberate redundancy remains: `CachingEdhrecCardLookup` copies the array on store **and** on every
  read (`:42`, `:48`, `:33`). That is intentional isolation — returning the cached array directly
  would let a caller mutate the cache — and should not be "simplified" away.
- No UAT. Both caches are invisible in the UI (same output, fewer upstream calls), so the observable
  check is request counts under a repeated Suggest Categories run, not a visual diff.
