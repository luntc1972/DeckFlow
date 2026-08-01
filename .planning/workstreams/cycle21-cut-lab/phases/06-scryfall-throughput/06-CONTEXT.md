# Phase 6: Scryfall Throughput - Context

**Gathered:** 2026-08-01
**Status:** Ready for planning
**Source:** ROADMAP Phase 6 block (operator decision, 2026-08-01), by operator choice in place of discuss-phase

<domain>
## Phase Boundary

Restore the Scryfall pacing floor to 200ms without re-earning the Cloudflare IP block, and stop
paying one throttled request per unresolved card.

**In scope:** `ScryfallThrottle` pacing policy (wave 1) and `ScryfallReferenceResolver`'s per-miss
fallback (wave 2).

**Out of scope:** any change to Cut Lab behavior, any new feature flag, any change to the five other
Scryfall consumers' own logic. This phase changes shared infrastructure *underneath* them; it must not
change what they do.

**Depends on:** nothing. Gates nothing. Runs in parallel with Phases 4 and 5.

**Why it exists:** Phase 111.1 raised `ScryfallThrottle.MinInterval` from 200ms to 500ms to stop a
Cloudflare IP block. That fix was correct but blunt, and it applies **process-wide to every Scryfall
consumer** — Comparison, Meta-Gap, Analysis, Manabase, Deck History — not just Cut Lab, which is
flag-gated. It is affecting production users today, outside any flag.
</domain>

<decisions>
## Implementation Decisions

Every decision below is **locked** — taken from the operator's own ROADMAP Phase 6 block, written
2026-08-01. Do not re-open them during planning.

### Wave 1 — per-endpoint gating (REPLACES the roadmap's "restore 200ms" plan)

⚠ **SUPERSEDED 2026-08-01, by operator decision, after research contradicted the roadmap's premise.**
The ROADMAP Phase 6 block says wave 1 should "restore the 200ms pacing floor behind an adaptive
degrade to 500ms on observed rate limiting". **Do not plan that.** Three independent pieces of
evidence say the 200ms figure was itself the defect:

1. `ScryfallThrottle.cs:14-21` — "Scryfall publishes a hard 2 requests/second (500ms) limit for
   `/cards/collection`, `/cards/search`, `/cards/named`, and `/cards/random` — the four endpoints
   every flow behind this throttle calls. **The previous 200ms figure was derived from that page's
   'all other methods' row (10 req/sec), which does not apply to these endpoints.**"
2. `ScryfallThrottleTests.cs:165-174` — `ExecuteAsync_SpacesConsecutiveCallsByAtLeastTheDocumentedPerEndpointLimit`
   asserts **>= 450ms**, written specifically to pin the documented limit. Restoring 200ms would
   require deleting or inverting it.
3. `111.1-PACING-MEASUREMENT.md` — "pacing conservatively *under* the documented limit is the desired
   behavior. This is a **documentation correction, not a defect to fix**."

Restoring 200ms would mean running above Scryfall's documented per-endpoint rate and backing off only
*after* being caught — a design that re-earns the Cloudflare block rather than avoiding it.

**The concern behind wave 1 is nonetheless real.** The measurement's own correction records true
achieved throughput as `1 / (MinInterval + s)`, i.e. roughly **2.2 req/s before, 1.33 req/s after** —
not the 5 and 2 quoted in the summary tables. Process-wide aggregate throughput genuinely fell.

**Decision: recover the throughput legitimately, by making the gate per-endpoint.**

- Scryfall's 2 req/s limit is documented **per endpoint**; `ScryfallThrottle` currently enforces a
  **single process-wide gate** shared across all four. That is stricter than required and is the
  actual source of the aggregate-throughput loss.
- Keep the **500ms floor per endpoint**. Do not lower it. The SC-7 test stays and must keep passing.
- Replace the single `Gate` + `_lastCallUtc` pair with per-endpoint pacing state, keyed by endpoint.
- ⚠ `ScryfallThrottle.ExecuteAsync` currently takes only a `Func<...>` — **it cannot see which
  endpoint it is pacing.** An endpoint key must be threaded through, and there are **14 call sites
  across 8 services** (`DeckConvertService`, `ScryfallCommanderSearchService`, `CardLookupService` x4,
  `CardSearchService`, `ScryfallCardResolver` x3, `ScryfallTaggerLookupService`, `ScryfallSetService`
  x2). That enumeration is the wave's real blast radius.
- ⚠ `ScryfallThrottle` is a process-wide `static` and its state is written from concurrent requests.
  Per-endpoint state must be concurrency-safe — a `ConcurrentDictionary` of per-endpoint gates, or
  equivalent. Not a plain field, and not a non-thread-safe dictionary.
- The adaptive degrade-on-429 idea is **deferred, not rejected** — see `<deferred>`. It becomes
  coherent only once the floor is no longer the thing being violated.

### Wave 2 — batch the fallback (SCOPED: one of the two strategies only)

- `ScryfallReferenceResolver` currently does chunk(75) -> `POST cards/collection` -> match-back ->
  **one `GET cards/search?q=!"Name"` per miss**. Collapse that loop into a single OR query:
  `q=!"A" or !"B" or !"C"`.

⚠ **Scope correction, 2026-08-01, from research.** The roadmap describes "the loop" as if there were
one. There are **two** per-caller fallback strategies, passed into `ResolveAsync` as a required
`Func<string, CancellationToken, Task<ScryfallCard?>>` delegate — the resolver does **not** own the
fallback query. Only one of the two is expressible as an OR query:

| Strategy | Callers | Shape | Batchable |
|---|---|---|---|
| `SearchFallbackCardAsync` (`ScryfallCardResolver.cs:140-159`) | Comparison, Meta-Gap, Cut Lab, Manabase | ONE request: `q=!"Name"`, `unique=cards`, `order=name` | **YES — this is wave 2's target** |
| `SearchPrintingFallbackCardAsync` (`ScryfallCardResolver.cs:162-205`) | Analysis, Deck History | up to THREE sequential requests: `(printed:"X" OR name:"X")` -> bare `X` -> `cards/named?fuzzy=X`, all `unique=prints` | **NO — out of scope, see below** |

**Why the printing strategy is excluded, stated so nobody "completes" it later without deciding to.**
It is a progressive per-name escalation: each stage runs only if the previous found nothing, and the
match-back picks a per-name best match
(`FirstOrDefault(card => NormalizeLookupName(card.Name) == normalizedCardName) ?? FirstOrDefault()`).
`unique=prints` returns many printings per card, so a batched flat list cannot be attributed back to
the term that produced it, and stages 2 and 3 are per-name by construction. Batching it is a redesign
of its matching semantics, not a request-count optimization. **Out of scope for this phase; record it
as a follow-up.**

**Consequence to state honestly in the plan:** wave 2 improves the four consumers on the exact-name
strategy. Analysis and Deck History — which use the printing strategy — see **no** request-count
change. If the 39-call miss-heavy scenario in `111.1-PACING-MEASUREMENT.md` came from one of those two
flows, this wave does not address that specific number. Verify which flow it was during planning and
say so plainly rather than implying phase-wide coverage.
- Chunk at roughly **60 names** — smaller than collection's 75, because `cards/search` is a GET and URL
  length bounds the batch at ~30 chars per term.
- ⚠ **Match-back is the risk.** Today it is 1 name -> 1 result; a batch returns a flat list. **This is
  the same seam that produced BOTH combo-seam MEDs on 2026-08-01** (DFC front-face, curly apostrophe).
  Normalize both sides through `CutLabCardNames.Normalize` from the start, and pin those two vectors
  in tests.
- One malformed term can 400 the whole chunk. **Degrade to the existing per-card path on 400** so nine
  good resolutions are not lost to one bad name.
- 404 changes meaning: a search 404s **only when EVERY term misses**. "Which missed" becomes set
  subtraction, which the existing `resolvedRequestNames` step already computes.

### Claude's Discretion

- Where the observation hook physically lives, and its exact shape, provided it satisfies the
  observed-not-thrown rule above.
- The concurrency primitive chosen, provided it is not a plain field.
- Test structure and fixture design, subject to the mutation expectations below.
- Whether waves 1 and 2 are one plan or several.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The measurement that motivates both waves
- `.planning/phases/111.1-cutlab-scryfall-burst-hotfix/111.1-PACING-MEASUREMENT.md` — the cost model.
  ⚠ **This lives in the ROOT workspace, not this workstream.** A normal 0-miss flow makes 2 throttled
  calls and pays one extra gap (200-300ms, barely perceptible); the damage concentrates in miss-heavy
  flows, modelled at `39 x 500ms ~ 19.5s` of serialized throttle. Aggregate process-wide ceiling falls
  from roughly 5 req/s to 2 req/s.

### Phase 6's own specification
- `.planning/workstreams/cycle21-cut-lab/ROADMAP.md` — the Phase 6 block is the source of every locked
  decision above, and the Success Criteria below are copied from it verbatim.

### Code under change
- `DeckFlow.Web/Services/ScryfallThrottle.cs` — the process-wide static gate. Wave 1.
- `DeckFlow.Web/Services/*ScryfallReferenceResolver*` — chunking, `cards/collection`, match-back and
  the per-miss fallback loop. Wave 2.

### The normalization seam this phase must not re-break
- `CutLabCardNames.Normalize` / `.Comparer` — the canonical name normalizer. Both sides of any batched
  match-back go through it.

</canonical_refs>

<specifics>
## Specific Ideas

### Proposed requirement IDs — NOT YET RATIFIED

⚠ `SCRY-01` through `SCRY-04` are referenced by the ROADMAP (`:367`, `:436`, `:441`, `:446`) but are
**defined nowhere**, and are **not** in `REQUIREMENTS.md`. The ROADMAP itself records this as a known
traceability gap to close before milestone closeout. The definitions below are **proposed** so planning
has something concrete to map against; they must be ratified into `REQUIREMENTS.md` before closeout,
and the operator may reword them.

| ID | Proposed text |
|----|----------------|
| SCRY-01 | Scryfall pacing is enforced **per endpoint** at the documented 500ms minimum interval, rather than by a single process-wide gate shared across all endpoints. |
| SCRY-02 | Every one of the 14 `ScryfallThrottle.ExecuteAsync` call sites supplies an endpoint key, and no call site can pace against the wrong endpoint's state or bypass pacing entirely. |
| SCRY-03 | N unresolved cards within one chunk cost **one** `cards/search` request, not N, for callers using the exact-name fallback strategy. |
| SCRY-04 | The batched path matches results back correctly for DFC (`Front // Back`) and curly-apostrophe names, falls back to per-card resolution on a 400 without losing any card the per-card path would have resolved, and treats a 404 as "every term missed". |

⚠ SCRY-01 and SCRY-02 are **rewritten** from the versions implied by the ROADMAP, which described the
superseded 200ms-restore design. SCRY-03 is **narrowed** to the exact-name strategy.

### Success Criteria (AMENDED — 1 and 2 replaced, 3 narrowed; 4-6 verbatim from ROADMAP Phase 6)

1. **(replaced)** Pacing is enforced per endpoint at >= 500ms each. Two calls to *different* endpoints
   are no longer serialized behind one another; two calls to the *same* endpoint still are. The
   existing SC-7 test (`ScryfallThrottleTests.cs:174`) still passes **unmodified**.
2. **(replaced)** All 14 call sites across 8 services supply an endpoint key, verified by enumeration
   rather than by grep alone — and a call site that omits one fails to compile rather than silently
   pacing against a default bucket.
3. **(narrowed)** N unresolved cards in one chunk cost ONE `cards/search` request, not N, **for the
   exact-name fallback strategy**. The printing-fallback strategy is explicitly unchanged.
4. A 400 on the batch query falls back to per-card resolution and loses no card that the per-card path
   would have resolved.
5. DFC (`Front // Back`) and curly-apostrophe names match back correctly in the batched path.
6. No regression for the other five Scryfall consumers — this is shared infrastructure, and Cut Lab's
   flag does **not** gate it.

### Release posture

This phase ships **unflagged**. `tool.cut-lab.enabled` does not gate `ScryfallThrottle` or the
resolver, and `main` auto-deploys to Render — so a defect here reaches every user of Comparison,
Meta-Gap, Analysis, Manabase and Deck History immediately. Plan the verification accordingly:
criterion 6 is not a formality.

</specifics>

<deferred>
## Deferred Ideas

- **Adaptive degrade-on-observed-429 (the roadmap's original wave 1).** Deferred, not rejected. It is
  coherent only *above* a floor that already respects the documented limit — as a degrade from 500ms
  to something slower under sustained pressure, never as justification for a faster steady state. If
  it is revived, the observed-not-thrown rule still holds: Phase 111.1's fail-open path **swallows**
  429s so they never reach `ScryfallThrottle.ThrowIfUpstreamUnavailable`, and a hook on the throw path
  alone would miss them.
- **Batching `SearchPrintingFallbackCardAsync`.** Out of scope with a stated reason (see the wave 2
  block). Revisiting it means redesigning its progressive-escalation matching semantics, which is its
  own decision. Record as a follow-up so Analysis and Deck History are not assumed covered.
- Any admin-visible control over the pacing interval.
- Extending batching to `cards/collection` itself, which is already batched at 75.
- Re-tuning the 500ms floor based on production telemetry. It is the documented per-endpoint limit and
  is locked for this phase.
- Per-endpoint intervals that differ by endpoint — e.g. giving non-card endpoints the docs' faster
  "all other methods" rate. Possible once gating is per-endpoint, but it is a second decision and this
  phase deliberately applies one uniform 500ms to every bucket.

</deferred>

---

*Phase: 06-scryfall-throughput*
*Context gathered: 2026-08-01 from the operator's ROADMAP Phase 6 block, in place of discuss-phase*
