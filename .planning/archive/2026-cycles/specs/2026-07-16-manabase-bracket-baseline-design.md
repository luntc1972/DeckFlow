# Manabase — Bracket-Weighted Empirical Baseline (lands + ramp + draw)

**Date:** 2026-07-16 (rev 2 — self-corpus primary, lands/ramp/draw, EDHREC optional)
**Status:** Approved design (scoping), pre-implementation
**Supersedes:** commander-cost-floor + commander-engine-credit (rejected by data — see `../research/2026-07-16-edhrec-bracket-land-data.md`).

## Premise (earned by data)

100-commander EDHREC study: no commander *ability* moves the land count once power level is controlled; **bracket is the only driver**, and a commander's own per-bracket average already bakes in its archetype/abilities/ramp. So instead of a formula, **use the observed average mana-base composition for this commander at the selected bracket** — and land count alone is misleading, so the baseline covers **lands + ramp + draw** (total mana sources is the metric that matters; high-cost commanders hold lands ~35 and scale ramp).

## Goal

Given a commander + a target bracket, surface a **bracket-weighted empirical baseline** — average **lands / ramp / draw** (and total sources) that real decks for that commander at that bracket run — weighted toward a global bracket baseline when the sample is thin, displayed **alongside** (not replacing) the existing Karsten/castability target. Cached in the DB; flag-gated OFF until validated.

## Data sources (two, self-corpus primary)

1. **PRIMARY — DeckFlow's own crawl corpus (no ToS gate).** DeckFlow already crawls decks for its knowledge base and its `ManabaseClassifier` already classifies lands, ramp (`IsRampPieceForBudget`), and draw (`IsDrawPieceForBudget`) per deck. Compute per-commander-per-bracket average lands/ramp/draw from the classified corpus — **counted the same way the analyzer evaluates them.** No third party, data DeckFlow owns.
2. **OPTIONAL SECONDARY — EDHREC (pending permission).** EDHREC has broader per-commander coverage + the cleanest land number. Use it only to (a) backfill commanders the corpus is thin on and (b) cross-validate. **Gated on EDHREC granting permission** (see gate) — the feature ships fully without it.

Fallback chain (per metric): commander corpus cell (≥ HIGH decks) → blended (LOW–HIGH) → global bracket baseline → existing Karsten formula (lands) / `ManabaseRampDrawBudget` formula (ramp/draw).

## Non-Goals

- No change to castability / colored-source math (prescriptive side stays).
- No attempt to model commander abilities (proven pointless).
- No live per-request third-party dependency on the hot path.
- No new NuGet dependency.

## EDHREC gate (applies ONLY to the optional secondary source)

Caching EDHREC data violates their ToS as written (no API/license; prohibits automated queries + redistribution). The **optional EDHREC source MUST NOT be built until EDHREC grants written permission** (outreach drafted: `Downloads/DeckFlow-EDHREC-Outreach.docx`). Attribution "Data from EDHREC" + backlinks required if granted. **The primary self-corpus path has no such gate and is the shipping foundation.**

## Design

### Component 1 — storage

New table `manabase_baseline` (via `RelationalDatabaseConnection`, SQLite + Postgres dialects):

| column | type | notes |
|---|---|---|
| commander_slug | text | canonical key; `*` = global-per-bracket fallback row |
| bracket | int | 1–5 |
| source | text | `corpus` \| `edhrec` |
| avg_lands | real | |
| avg_ramp | real | classified same as the analyzer's ramp budget |
| avg_draw | real | classified same as the analyzer's draw budget |
| deck_count | int | sample size (weighting + display) |
| computed_utc | timestamp | dialect-guarded (`::timestamptz` on PG — prior F-51-PG-01 fix) |

PK (commander_slug, bracket, source). Mirrors the existing `ICategoryKnowledgeStore` / `FeedbackStore` pattern.

### Component 2 — corpus aggregation job

New hosted service `ManabaseBaselineJobService` (`Singleton` + `HostedService`, mirroring `ArchidektCacheJobService`):
- Periodically re-aggregates the classified crawl corpus into `manabase_baseline` (source `corpus`): for each (commander, bracket), average lands/ramp/draw over decks meeting a per-cell floor (default 400; configurable). Bracket per deck comes from DeckFlow's own bracket signal (the Bracket tool / a stored bracket tag) — or, until per-deck bracket is available, aggregate by mode/power proxy and refine later.
- Recompute + store the **global-per-bracket baseline** (`commander_slug = '*'`, source `corpus`) = mean across commanders with ≥ floor decks (validation anchor: lands core 35.9 / upgraded 35.5 / optimized 34.5 from the EDHREC pilot — the corpus should land near these).
- (Optional, post-permission) a separate `EdhrecBaselineLoader` upserts source `edhrec` rows the same way.

### Component 3 — bracket-weighted lookup (Core, pure)

New pure helper `ManabaseBaselineWeighting` (Core, no I/O — takes looked-up rows as input). For each metric (lands, ramp, draw) independently:

```
HIGH = 400, LOW = 100
if commanderAvg == null || commanderDeckCount < LOW:  value = globalBracketAvg ; source = "global"
elif commanderDeckCount >= HIGH:                      value = commanderAvg     ; source = "commander"
else: w = (commanderDeckCount - LOW)/(HIGH - LOW)
      value = w*commanderAvg + (1-w)*globalBracketAvg ; source = "blended"
```

Returns `{ Lands, Ramp, Draw, TotalSources = Lands+Ramp, Source, CommanderDeckCount }`. Prefer `corpus` rows; use `edhrec` rows only to backfill a metric the corpus lacks for that cell (config-controlled). Fully unit-testable.

### Component 4 — analyzer / result plumbing (augment)

- The manabase result gains a `ManabaseBaseline` block: weighted lands/ramp/draw + total sources + source label + commander sample size + which data source (corpus/edhrec).
- The existing Karsten land target AND `ManabaseRampDrawBudget` advisory are **unchanged**; the empirical baseline sits beside them (and can later re-anchor the ramp/draw budget once validated).
- Flag off or no data → block absent → byte-identical output.

### Component 5 — bracket input (UI)

Bracket selector (1–5: Exhibition/Core/Upgraded/Optimized/cEDH) on the manabase page; default inferred from the existing mode (Standard→Upgraded, cEDH→cEDH). Keys the lookup.

### Component 6 — display

> **Community baseline** — Sméagol, Helpful Guide · Upgraded (657 decks): **34 lands · 10 ramp · 9 draw** (~44 sources). Your deck: **24 · 15 · 6** (~39). *Karsten castability target: 35 lands.*
> _Source: DeckFlow crawl corpus_ (or _Data from EDHREC_ when that source is used).

Show sample size, source label, and attribution when EDHREC-sourced.

### Flag

`analysis.manabase.baseline`, seeded **OFF** until the corpus aggregation runs and the numbers validate against the pilot. Read fail-safe OFF; off = byte-identical.

## Error Handling

- Commander slug unresolved → no commander row → global bracket baseline (or omit). No throw.
- Job upstream/aggregation failure → stale-but-valid cached rows keep serving; retry next cycle (log, don't crash — `ArchidektCacheJobService` pattern).
- DB provider parity: timestamp column dialect-guarded (SQLite vs Postgres).
- Thin corpus for a metric → blend/global fallback; never blocks analysis.

## Testing

- **`ManabaseBaselineWeightingTests`** (Core): per metric — solid sample → commander value; thin → global; mid → correct linear blend; missing → global; missing global → omit. Total-sources = lands+ramp.
- **Store tests**: upsert + read per (slug, bracket, source) on SQLite + Postgres; timestamp round-trips on PG. Mirror `CategoryKnowledgeStore` tests.
- **Aggregation job**: classified-corpus fixture → correct per-cell avg lands/ramp/draw + deck_count; floor filter; global recompute; ramp/draw counted identically to `ManabaseRampDrawBudget`'s classification.
- **Analyzer/result**: flag OFF → no block, byte-identical; ON with seeded rows → block with correct weighted lands/ramp/draw + sample + source.
- **UI**: baseline line renders (lands/ramp/draw/sources + attribution + sample); desktop+mobile, 2 themes; absent when flag off.
- Regression: full Core + Web suites green with flag OFF.

## Backward Compatibility

- Additive + flag-gated OFF → zero behavior change until aggregation runs + flag flipped.
- New table + hosted service + UI control; Karsten + ramp/draw math untouched.
- Compiled JS (bracket selector TS, if any) never committed.

## Scope / phasing (milestone — each phase its own plan)

**ToS-free (build now):**
1. **Storage + store** (`manabase_baseline` table, dialect-guarded, tests).
2. **Weighted lookup** (`ManabaseBaselineWeighting` Core helper + tests) — heart of the feature, zero external dependency.
3. **Corpus aggregation job** (`ManabaseBaselineJobService` from classified corpus; global recompute).
4. **Analyzer/result augment** (`ManabaseBaseline` block, flag).
5. **UI** (bracket selector + lands/ramp/draw display + attribution; themes/mobile).
6. **Validate + flag-flip** (aggregate real corpus, confirm vs pilot baselines, UAT, seed ON).

**Gated on EDHREC permission (optional, later):**
7. **`EdhrecBaselineLoader`** (source `edhrec` backfill + cross-validation) — only after written permission.

## Open Questions / Assumptions

- **Per-deck bracket signal in the corpus** — does DeckFlow store/derive a bracket per crawled deck? If not, Phase 3 first needs a bracket tag (reuse the Bracket classifier) or aggregates by mode/power proxy initially. **Confirm at Phase 1/3.**
- **Corpus size / coverage** — thinner than EDHREC; global bracket baseline is robust (SD ~1.5), popular commanders fine, long tail falls back to global. Validate coverage after first aggregation.
- **Weighting thresholds** (LOW 100 / HIGH 400) — tunable consts; validate against corpus coverage.
- **Ramp/draw classification parity** — the aggregation MUST use the same `IsRampPieceForBudget` / `IsDrawPieceForBudget` predicates the analyzer uses, so the baseline and the user's deck are counted identically.
- **EDHREC** — optional; gated on Discord/email permission (`Downloads/DeckFlow-EDHREC-Outreach.docx`). Feature ships without it.
