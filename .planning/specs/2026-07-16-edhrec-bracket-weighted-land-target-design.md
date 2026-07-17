# Manabase — EDHREC Bracket-Weighted Land Target

**Date:** 2026-07-16
**Status:** Approved design (scoping), pre-implementation
**Supersedes:** commander-cost-floor + commander-engine-credit (both rejected by data — see `../research/2026-07-16-edhrec-bracket-land-data.md`).

## Premise (earned by data)

100-commander EDHREC study: no commander *ability* (draw/ramp/cost-reduce/mana) moves the land count once power level is controlled; **bracket is the only driver**, and each commander's own EDHREC per-bracket average already bakes in its archetype/abilities/ramp. So instead of a formula that tries (and fails) to model the commander, **use the observed EDHREC average land count for this exact commander at the selected bracket as an empirical baseline, shown alongside the existing castability math.**

## Goal

Given a commander + a target bracket, surface a **bracket-weighted land target**: the EDHREC per-commander-per-bracket average land count (weighted toward a global bracket baseline when the commander's sample is thin), displayed **alongside** — not replacing — the current Karsten/castability target. Cached in the DB via a scheduled crawl; flag-gated OFF until validated.

## Non-Goals

- No change to castability / colored-source / ramp math (the prescriptive side stays).
- No attempt to model commander abilities (proven pointless).
- No live per-request EDHREC dependency on the analysis hot path.
- No new NuGet dependency (reuse RestSharp + Polly like existing crawlers).

## ⚠️ Prerequisite gate (blocking — before any implementation)

**Verify EDHREC's Terms of Service / data-use policy permits caching their average-deck JSON for a public deployed tool, with attribution.** Reach out to EDHREC if unclear. Implementation MUST NOT begin until this is confirmed. Bake clear "Data from EDHREC" attribution into the UI regardless.

## Design

### Data source (proven)

`https://json.edhrec.com/pages/average-decks/<slug>/<bracket>.json` → `land` (int) + base JSON `bracket_counts` (deck count per bracket 1–5). Bracket slugs: exhibition/core/upgraded/optimized/cedh = 1–5.

### Component 1 — storage

New table (via `RelationalDatabaseConnection`, SQLite + Postgres dialects) `edhrec_land_baseline`:

| column | type | notes |
|---|---|---|
| commander_slug | text | EDHREC slug; `*` reserved for the global-per-bracket fallback row |
| bracket | int | 1–5 |
| avg_lands | real | EDHREC `land` for that cell |
| deck_count | int | sample size (for weighting + display) |
| fetched_utc | timestamp | dialect-guarded (`::timestamptz` on PG — see prior F-51-PG-01 fix) |

Primary key (commander_slug, bracket). Follows the existing store pattern (`ICategoryKnowledgeStore` / `FeedbackStore`).

### Component 2 — crawl job

New hosted service `EdhrecLandBaselineJobService` (registered `Singleton` + `HostedService`, mirroring `ArchidektCacheJobService`):
- Weekly (configurable), fetches the EDHREC top-N commanders (and/or the union of commanders already in the user's crawled deck set) average-deck JSON per bracket.
- Upserts `edhrec_land_baseline` rows (only cells with deck_count ≥ a floor, default 400).
- Recomputes + stores the **global-per-bracket baseline** (`commander_slug = '*'`) = mean avg_lands across commanders with deck_count ≥ floor (current values: core 35.9 / upgraded 35.5 / optimized 34.5). This is the fallback anchor.
- Uses RestSharp + a named Polly pipeline (like existing upstreams); polite rate limiting; a descriptive User-Agent.

### Component 3 — bracket-weighted lookup (Core)

New pure helper `EdhrecBracketBaseline` (Core, no I/O — takes the looked-up rows as input) implementing the weighting:

```
Given commanderAvg (or null), commanderDeckCount, globalBracketAvg for the selected bracket:
  HIGH = 400, LOW = 100
  if commanderAvg == null || commanderDeckCount < LOW:  target = globalBracketAvg ; source = "global bracket"
  elif commanderDeckCount >= HIGH:                       target = commanderAvg    ; source = "commander"
  else:  w = (commanderDeckCount - LOW) / (HIGH - LOW)
         target = w*commanderAvg + (1-w)*globalBracketAvg ; source = "blended"
```

Returns `{ Target, Source, CommanderAvg, CommanderDeckCount, GlobalAvg }`. This is the **bracket-weighted target** — commander-specific when the sample is solid, blended toward the bracket norm as it thins, pure bracket baseline when absent. Fully unit-testable.

### Component 4 — analyzer / result plumbing (augment)

- The manabase result gains an `EdhrecBaseline` block: the weighted target + source + commander sample size + global bracket value.
- The existing Karsten/castability land target is **unchanged**; the EDHREC baseline sits beside it.
- When the flag is off or no data exists, the block is absent → byte-identical output.

### Component 5 — bracket input (UI)

- Add a **bracket selector (1–5)** to the manabase page (labels: Exhibition/Core/Upgraded/Optimized/cEDH). Default to Core/Upgraded (most common) or infer from the existing mode (Standard→~Core/Upgraded, cEDH→cEDH). Existing Bracket tool / focused-tier can inform the default later.
- The selected bracket keys the lookup.

### Component 6 — display

Manabase result shows, e.g.:

> **EDHREC baseline** — Sméagol, Helpful Guide · Upgraded: **34 lands** (657 decks). Your deck: 24. *Karsten castability target: 35.*
> _Data from EDHREC._

- Show the sample size (a 51-deck average must read as low-confidence).
- Show the source label (commander / blended / global bracket).
- Attribution line required.

### Flag

`analysis.manabase.edhrec-baseline`, seeded **OFF** until the crawl runs and the numbers are validated against the pilot (core 35.9 / upgraded 35.5 / optimized 34.5). Read fail-safe OFF; off = byte-identical.

## Data Flow

Weekly job → `edhrec_land_baseline` (per-commander + `*` global rows). Per analysis: resolve commander → slug; read commander row + `*` row for the selected bracket; `EdhrecBracketBaseline` computes the weighted target + source; result carries the `EdhrecBaseline` block; view renders it beside the Karsten target with attribution. No per-request EDHREC call.

## Fallback chain

commander cell (deck_count ≥ 400) → blended (100–400) → global bracket baseline (`*` row) → if even `*` missing (crawl never ran), omit the block entirely (formula target still shown). Never blocks or errors the analysis.

## Error Handling

- Commander slug unresolved (obscure/new/misspelled) → no commander row → global bracket baseline (or omit). No throw.
- Crawl upstream failure → stale-but-valid cached rows keep serving; job retries next cycle (log, don't crash — `ArchidektCacheJobService` pattern).
- DB provider parity: timestamp column dialect-guarded (SQLite vs Postgres), per the prior harvest-stats fix.

## Testing

- **`EdhrecBracketBaselineTests`** (Core, xUnit): solid commander sample → uses commanderAvg; thin (<100) → global; mid (100–400) → correct linear blend weight; missing commander → global; missing global too → null/omit.
- **Store tests**: upsert + read per (slug, bracket) on both SQLite and Postgres dialects (mirror `CategoryKnowledgeStore` tests); timestamp round-trips on PG.
- **Crawl job**: parse EDHREC average-deck JSON fixture → correct land + deck_count extraction; floor filter; global-baseline recompute.
- **Analyzer/result**: flag OFF → no `EdhrecBaseline` block, byte-identical; flag ON with seeded rows → block present with correct weighted target + sample.
- **UI**: manabase page renders the EDHREC line + attribution + sample size; desktop+mobile, 2 themes; absent when flag off.
- Regression: full Core + Web suites green with flag OFF.

## Backward Compatibility

- Additive + flag-gated OFF → zero behavior change until crawl runs + flag flipped.
- New table + hosted service + UI control; the Karsten target and all existing math untouched.
- Compiled JS (if any bracket-selector TS) never committed.

## Scope / phasing (this is a milestone, not a slice)

Bigger than a single plan — break into phases, each its own plan:
1. **Storage + store** (`edhrec_land_baseline` table, dialect-guarded store + tests).
2. **Crawl job** (`EdhrecLandBaselineJobService`, RestSharp+Polly, upsert, global-baseline recompute) — **after the ToS gate clears**.
3. **Weighted lookup** (`EdhrecBracketBaseline` Core helper + tests).
4. **Analyzer/result + report augmentation** (EdhrecBaseline block, flag).
5. **UI** (bracket selector + display + attribution, themes/mobile).
6. **Validate + flag-flip** (crawl real data, confirm vs pilot baselines, UAT, seed ON).

## Open Questions / Assumptions

- **ToS** — the hard prerequisite (Component gate). Assumption: cache-with-attribution is acceptable; confirm before Phase 2.
- **Which commanders to crawl** — top-N (e.g. 500–1000) covers the vast majority of usage; obscure commanders fall back to the global bracket baseline. Assumption: top-N + on-demand backfill for analyzed commanders not yet cached (a lazy fetch that populates the cache, still not on the hot path if done async).
- **Bracket default** — infer from mode initially (Standard→Upgraded, cEDH→cEDH); a real 1–5 selector is the fuller version.
- **Weighting thresholds** (LOW 100 / HIGH 400) — tunable consts; validate against real crawl coverage.
- **Ramp baseline** — could store avg_ramp too (same source) for a future ramp-target augmentation; out of scope here, but the table can carry the column.
