# Phase 103: Simulation Engine & Guided Cut Rounds - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-19
**Phase:** 103-simulation-engine-guided-cut-rounds
**Areas discussed:** Cut round mechanics, Tradeoff delta display, Recalc timing & perf, Baseline compare + defer UX

---

## Cut round mechanics

| Option | Description | Selected |
|--------|-------------|----------|
| Multi-finding overlap | Card flagged by 2+ Phase-102 structural findings; "flagged by N findings" framing | ✓ |
| Surplus-role weakest | Cut weakest per surplus role until floor met; single-metric ranking risks verdict framing | |
| You decide | Claude picks during planning | |

**User's choice:** Multi-finding overlap (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| One at a time | Single proposed cut with deltas; accept/reject/defer, then next | ✓ |
| Round list + drill-in | Full candidate queue up front; open each for deltas | |
| You decide | Claude picks during planning | |

**User's choice:** One at a time (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-advance + banner | Next round starts automatically when queue exhausts, with round banner | ✓ |
| Explicit round gate | Round-summary screen between rounds; user clicks to continue | |
| Free navigation | Rounds as tabs; weakens CUT-01 fixed order | |

**User's choice:** Auto-advance + banner (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Single-flag → rest; loop rounds | Round 2 = exactly-one-finding cards; round 3 = remaining unlocked weakest-first as preference; loop over deferred/rejected until 100 | ✓ |
| Role-quota rounds | Round 2 purely floor-driven; round 3 free-choice unordered | |
| You decide | Claude designs population + overflow during planning | |

**User's choice:** Single-flag → rest; loop rounds (recommended)

---

## Tradeoff delta display

| Option | Description | Selected |
|--------|-------------|----------|
| Changed-only compact | Only meaningfully-moved metrics + "no meaningful change" line + full-table expander | ✓ |
| Full panel always | All 7 families every proposal | |
| Role-relevant subset | Metrics tied to card's role(s); risks hiding cross-effects | |

**User's choice:** Changed-only compact (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Directional colors OK | Down=red, up=green; neutrality in copy, not hidden direction | ✓ |
| Neutral palette | Same color, arrows only | |
| You decide | Claude picks with theme tokens in mind | |

**User's choice:** Directional colors OK (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Current-only per proposal | Delta vs current working list; baseline drift only in SIM-02 view | ✓ |
| Both per proposal | Delta vs current AND cumulative vs baseline on every card | |
| You decide | Claude picks during UI planning | |

**User's choice:** Current-only per proposal (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed seed + noise floor | Deterministic seed per (decklist, iterations); sub-threshold deltas = "no meaningful change" | ✓ |
| Fixed seed only | Stable numbers but jitter deltas still shown | |
| You decide | Claude sets policy; researcher verifies seed support | |

**User's choice:** Fixed seed + noise floor (recommended)

---

## Recalc timing & perf

| Option | Description | Selected |
|--------|-------------|----------|
| Async fetch + partial update | TS posts to JSON endpoint (SameOriginRequestValidator, DeckSyncApiController pattern); form-POST no-JS fallback | ✓ |
| Form-POST reload only | Current Cut Lab pattern; full render per decision | |
| You decide | Claude weighs after latency measurements | |

**User's choice:** Async fetch + partial update (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| On-demand + cache | Compute presented card only; cache by (working-list hash, card) | ✓ |
| Precompute round queue | Sim every candidate at round start; N× burst, staleness risk | |
| On-demand + prefetch next | Compute shown card, prefetch next while deciding | |

**User's choice:** On-demand + cache (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| ~1s target, 3s cap | Tune iterations for ~1s; spinner-capped 3s; full fidelity for summaries | ✓ |
| Full fidelity always | Keep engine defaults everywhere, accept latency | |
| You decide | Researcher measures first, Claude sets budget | |

**User's choice:** ~1s target, 3s cap (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Snapshot in state, cache in memory | Baseline snapshot in CutLabState; delta cache in IMemoryCache with TTL | ✓ |
| Everything in IMemoryCache | Smallest state but baseline lost on restart/eviction | |
| Everything in state | Self-contained but unbounded form payload growth | |

**User's choice:** Snapshot in state, cache in memory (recommended)

---

## Baseline compare + defer UX

| Option | Description | Selected |
|--------|-------------|----------|
| Collapsible panel, same page | Three-column baseline/current/delta table in accordion section | ✓ |
| Separate compare view | Dedicated tab/route | |
| You decide | Claude picks during UI planning | |

**User's choice:** Collapsible panel, same page (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Sticky bar w/ round + count | Slim sticky bar: round name + cards-to-cut + accepted count; 44px touch-safe | ✓ |
| Counter in proposal card | Count inside proposal card; no sticky chrome | |
| You decide | Claude picks with mobile screenshots as gate | |

**User's choice:** Sticky bar w/ round + count (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Parked until loop-around | Defer sits out until rounds exhaust; loop-around re-proposes deferred then rejected | ✓ |
| Requeue end of round | Deferred card returns at end of current round queue | |
| You decide | Claude picks during planning | |

**User's choice:** Parked until loop-around (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Cuts-made list, restore any | Any accepted cut individually restorable; metrics recalc on restore | ✓ |
| Undo last only | Single-step undo of most recent decision | |
| No undo | Decisions final until restart | |

**User's choice:** Cuts-made list, restore any (recommended)

---

## Claude's Discretion

- Noise-floor threshold values and per-metric formatting precision
- In-loop vs full-fidelity iteration counts (set from measured engine cost)
- Delta cache TTL / eviction policy
- Sticky-bar exact layout and markup

## Deferred Ideas

None — discussion stayed within phase scope.
