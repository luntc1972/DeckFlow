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

### Wave 1 — adaptive pacing

- Default `MinInterval` returns to **200ms**.
- On an **observed** Scryfall 429, degrade to **500ms**.
- The degrade lasts **5 minutes since the most recent 429**, then reverts **automatically**. No manual
  intervention, no admin toggle.
- ⚠ **The trigger must fire where the 429 is OBSERVED, not where it is thrown.** Phase 111.1's B-1
  design deliberately **swallows** 429s in the Cut Lab fail-open path — they never reach
  `ScryfallThrottle.ThrowIfUpstreamUnavailable`. A degrade hook on the throw path alone would miss the
  exact scenario this phase exists for. Record at status inspection, **before** the fail-open branch.
- ⚠ `ScryfallThrottle` is a process-wide `static`. A mutable `MinInterval` is written from concurrent
  requests — use `Interlocked`/`volatile`, **not** a plain field.

### Wave 2 — batch the fallback

- `ScryfallReferenceResolver` currently does chunk(75) -> `POST cards/collection` -> match-back ->
  **one `GET cards/search?q=!"Name"` per miss**. Collapse that loop into a single OR query:
  `q=!"A" or !"B" or !"C"`.
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
| SCRY-01 | Scryfall pacing defaults to a 200ms minimum interval, and an observed 429 degrades it to 500ms for 5 minutes since the most recent 429, reverting automatically with no manual intervention. |
| SCRY-02 | The degrade triggers on a 429 that is **observed at status inspection**, including one swallowed by a fail-open path, not only on a 429 that is thrown. |
| SCRY-03 | N unresolved cards within one chunk cost **one** `cards/search` request, not N. |
| SCRY-04 | The batched path matches results back correctly for DFC (`Front // Back`) and curly-apostrophe names, falls back to per-card resolution on a 400 without losing any card the per-card path would have resolved, and treats a 404 as "every term missed". |

### Success Criteria (copied verbatim from ROADMAP Phase 6)

1. Steady state paces at 200ms; an observed 429 moves it to 500ms; it returns to 200ms 5 minutes
   after the last 429, with no manual intervention.
2. A swallowed (fail-open) 429 triggers the degrade — **proven by a test, not by inspection**.
3. N unresolved cards in one chunk cost ONE `cards/search` request, not N.
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

- Any admin-visible control over the pacing interval. The degrade is automatic and self-reverting by
  decision; an operator toggle is not in scope.
- Extending batching to `cards/collection` itself, which is already batched at 75.
- Re-tuning the 5-minute window or the 200/500ms pair based on production telemetry. Those numbers are
  locked for this phase; revisiting them is a later decision informed by whether the block recurs.

</deferred>

---

*Phase: 06-scryfall-throughput*
*Context gathered: 2026-08-01 from the operator's ROADMAP Phase 6 block, in place of discuss-phase*
