# 31-01 SUMMARY — PRM-01 Combo-Data Spike

**Status:** COMPLETE (Claude investigation — read-only probes) — 2026-06-09
**Requirements:** PRM-01
**Wave:** 1 (first execution unit per D-1; gates 31-03/31-04)
**Human-verify checkpoint:** APPROVED 2026-06-09

## What shipped

`31-SPIKE.md` recording three gating verdicts from live read-only probes of Commander
Spellbook + EdhTop16. No production code (disposable `curl` + Python scratch; reverted).

## Verdicts

1. **Combo Ranking = `sufficient`** → 31-04 uses the **priority-ranked** branch (PRM-08),
   not AI-ranked fallback. Raw `find-my-combos` variants carry `uses` (piece count),
   `manaValueNeeded` (int assembly cost), `popularity` (int), `produces` (immediacy),
   `easyPrerequisites`/`notablePrerequisites`. Live proof: Consultation+Oracle →
   pieces=2, mv=3, pop=142518, "Win the game".
   - **Production change recorded for 31-03/04:** current parser
     (`CommanderSpellbookService.cs:16-19, 180-276`) drops these fields. `SpellbookCombo`
     (+ `SpellbookAlmostCombo`) must gain `PieceCount` / `ManaValueNeeded` / `Popularity`
     with `{ get; init; }` + serialization round-trip test.

2. **Primer byte-size → recommend `GeminiPrimerPromptVariant.DefensivePromptCharCap = 32000`**
   (chars), lowering the RESEARCH placeholder of 50000. Worst-case full-31 cEDH primer
   (20 verified + 15 near combos, 100-card deck, named archetypes) = **30,931 bytes /
   30,929 chars** ≈ 94% of Gemini's 32,768-byte paste warning. 50000 would never trim
   (dead feature); 32000 engages the D-4 graceful trim at the Gemini paste boundary.
   ChatGPT/Claude variants uncapped. Cap = CHAR check; `AiPlatform.Gemini.PasteWarningBytes`
   = BYTE check — distinct controls (Pitfall 5). Re-confirm against the real variant in 31-04.

3. **EdhTop16 = `meta-query-available`** → 31-03 adds ONE meta-wide client method. Schema
   exposes the name-filter-free root query `commanders(first,sortBy,timePeriod,...)` →
   `data.commanders.edges[].node.name`. Verbatim query + `CommandersSortBy`
   (`CONVERSION|POPULARITY|TOP_CUTS|WINRATE`) + `TimePeriod`
   (`ALL_TIME|ONE_MONTH|THREE_MONTHS|SIX_MONTHS|ONE_YEAR|POST_BAN`) enums recorded.
   edhtop16 is cEDH-only → this IS the bracket-5 named meta. Brackets 1-4 keep the 5
   generic strategy buckets. Runtime failure degrades bracket 5 to the generic buckets (D-2).

## Verification

- Automated grep gate (plan): `SPIKE-OK` (combo token + cap + EdhTop16 token all present).
- `git status` — only `31-SPIKE.md` added; no production code touched; scratch reverted.
- Human-verify checkpoint approved.

## Notes / next

- Unblocks Wave 2: **31-03** (service — priority-rank parser extension + EdhTop16 meta
  method) and **31-04** (prompt variants — Gemini cap 32000). Both read `31-SPIKE.md` at
  execution time; no replan (D-1 satisfied).
- Fixture validity ~2026-07-08; verdicts re-checkable.
