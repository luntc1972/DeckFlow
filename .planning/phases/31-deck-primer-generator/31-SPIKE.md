# 31-SPIKE.md — PRM-01 Combo-Data Spike Verdicts

**Date:** 2026-06-09
**Plan:** 31-01 (Wave 1, first execution unit of Phase 31 per D-1)
**Operator probe only — no production code shipped.** All probing done via throwaway
`curl` + Python scratch scripts under `/tmp`; `git status` shows only this file added.

## Provenance / Fixtures (valid-until ~2026-07-08 per RESEARCH cadence)

| Probe | Endpoint | Request | HTTP |
|-------|----------|---------|------|
| (a) Spellbook richness | `POST https://backend.commanderspellbook.com/find-my-combos` | Thrasios + Tymna commanders; main = Thassa's Oracle, Demonic Consultation, Isochron Scepter, Dramatic Reversal, Sol Ring, Mana Vault, Basalt Monolith, Kinnan | 200 (160 KB) |
| (c) EdhTop16 schema | `POST https://edhtop16.com/api/graphql` | `__schema` introspection + live `commanders(...)` query | 200 |

Live response captured 3 `included` + 37 `almostIncluded` variants. Byte-size model
re-used those real variant objects (padded to the production caps of 20 verified + 15
near) so the byte count reflects an actual combo-dense cEDH deck.

---

## Verdict 1 — Combo Ranking: **`sufficient`**

The raw `find-my-combos` variant objects carry machine-readable ranking fields well beyond
`description`. Top-level keys present on every variant:

```
bracketTag, description, easyPrerequisites, id, identity, includes, legalities,
manaNeeded, manaValueNeeded, notablePrerequisites, notes, of, popularity, prices,
produces, requires, spoiler, status, uses, variantCount
```

Observed values (live `included` variants):

| Combo | `uses` (piece count) | `manaValueNeeded` | `manaNeeded` | `popularity` | `produces` (immediacy) |
|-------|---------------------|-------------------|--------------|--------------|------------------------|
| Demonic Consultation + Thassa's Oracle | 2 | 3 | `{U}{U}{B}` | 142518 | "Win the game" |
| Dramatic Reversal + Isochron Scepter | 2 | 0 | `` | 99907 | "Infinite mana / storm / untap" |
| Kinnan + Basalt Monolith | 2 | 0 | `` | 44905 | "Infinite colorless mana" |

This gives a **structured** piece count (`uses.length`), an integer assembly-cost signal
(`manaValueNeeded`), an immediacy signal (`produces` — game-winning vs infinite-mana vs
value), a prevalence signal (`popularity`), and setup-complexity text
(`easyPrerequisites` / `notablePrerequisites`). No heuristic description-parsing needed.

### Downstream consequence (→ 31-04)

**31-04 selects the priority-ranked combo branch (PRM-08), NOT the AI-ranked fallback.**
Recommended ranking key for the "Combo Prioritization" section / combo-block ordering:

1. `produces` immediacy class — game-winning (`Win the game` / `Win`) > infinite-mana >
   other value (descending priority)
2. `uses.length` ascending (fewer pieces = easier assembly)
3. `manaValueNeeded` ascending (cheaper to fire)
4. `popularity` descending (tie-break toward the established line)

### Required production change for 31-03/31-04 (record — NOT done in this spike)

The current parser **drops** the ranking fields. `SpellbookCombo`
(`CommanderSpellbookService.cs:16-19`) is `(CardNames, Results, Instructions)` and
`ParseVariants` / `ExtractInstructions` (lines 180-276) read only `uses`, `produces`,
`description`. To use the `sufficient` branch, 31-03/04 must extend `SpellbookCombo` (and
`SpellbookAlmostCombo`) with `PieceCount` (= `uses.length`), `ManaValueNeeded` (int),
and `Popularity` (int), and capture them in `ParseVariants` / `ParseAlmostVariants`.
Preserve `{ get; init; }` accessors + add a serialization round-trip test per the
record-guard decision in STATE.md.

---

## Verdict 2 — Primer Byte-Size + Gemini Cap

Synthetic full-section cEDH primer modeled from: real catalog HelpText (all 31 sections,
`PrimerSectionCatalog.cs`), the real Spellbook combo block padded to the production caps
(20 verified + 15 near), a 100-card decklist, grounded role counts, the named-archetype
matchup block (Verdict 3), preamble + output-format framing, and conservative per-section
guidance prose.

| Scenario | Chars | UTF-8 Bytes | vs Gemini warning (32,768 B) |
|----------|-------|-------------|------------------------------|
| Full-31 sections, **max combos** (20+15) | 30,929 | **30,931** | ~94% — just under |
| cEDH-30 sections, max combos | 30,476 | 30,478 | ~93% |
| Full-31 sections, typical combos (3+15) | 25,019 | 25,023 | ~76% |
| Typical 15-section primer | 18,028 | 18,032 | ~55% |
| Combo block alone, max (20+15) | — | 13,462 | — |

Primer prose is overwhelmingly ASCII, so **chars ≈ bytes** (30,929 chars ↔ 30,931 bytes).
The worst-case full primer sits at ~94% of Gemini's 32,768-byte paste warning. Headroom is
thin: the production Gemini variant's real per-section prose (likely richer than the
conservative framing modeled here), longer commander names, or verbose combo descriptions
push a combo-dense full-31 cEDH primer **over** 32,768 bytes.

### Recommended `GeminiPrimerPromptVariant.DefensivePromptCharCap = 32000` (chars)

- The RESEARCH placeholder of `50000` (mirroring `GeminiAnalysisPromptVariant`) would
  **never** trim a primer — the full-31 max primer is only ~30.9K chars — making the
  D-4 graceful-trim feature dead code.
- Setting the cap to **32,000 chars** aligns the defensive trim with Gemini's 32,768-byte
  paste warning (chars ≈ bytes for this near-ASCII content), so the largest full-31 cEDH
  primers trim lowest-priority sections instead of silently exceeding Gemini's paste limit.
- Trim order (lowest priority first), with the disclosure line
  `[N section(s) omitted — Gemini paste limit]`: Maintenance group
  (`version-history-and-change-log`, `meta-shift-adjustments`, `upgrade-paths`,
  `budget-cut-ladder`), then `speculative-synergies`.
- **ChatGPT and Claude primer variants stay uncapped** (no trim) — practical ChatGPT web
  paste ~100K chars, Claude no hard limit.

### Pitfall 5 note (bytes vs chars — D-4)

`DefensivePromptCharCap` is a **CHAR** check (`string.Length`).
`AiPlatform.Gemini.PasteWarningBytes = 32_768` (`AiPlatform.cs:30`) is a separate **BYTE**
check driving the UI paste-warning indicator. They are intentionally near-equal here only
because primer prose is near-ASCII; they remain two distinct controls. Do not conflate.

### Re-confirm in 31-04

These numbers are a faithful model, not the final assembled prompt (variants not yet
built). Re-run `Encoding.UTF8.GetByteCount(promptText)` against the real
`GeminiPrimerPromptVariant` output at the end of 31-04 and adjust the cap if the real
per-section prose materially exceeds the model.

---

## Verdict 3 — EdhTop16 Named-Archetype Matchup Routing (OQ-2): **`meta-query-available`**

The existing `EdhTop16Client` exposes ONLY the per-commander
`commander(name:$name)` query (`EdhTop16Client.cs:37-58`) — confirmed from source. BUT the
EdhTop16 GraphQL schema **does** expose a meta-wide, name-filter-free root query yielding
NAMED archetypes. Root query fields from `__schema` introspection include:

```
commanders(after, colorId, first, minEntries, minTournamentSize, sortBy, timePeriod)
```

Live query (run successfully, HTTP 200) — **record this verbatim for 31-03**:

```graphql
query($first:Int!,$sortBy:CommandersSortBy!,$timePeriod:TimePeriod!){
  commanders(first:$first,sortBy:$sortBy,timePeriod:$timePeriod){
    edges{ node{ name colorId } }
  }
}
```

- **Response field path:** `data.commanders.edges[].node.name` (named archetype) + `.node.colorId`
- **`CommandersSortBy` enum:** `CONVERSION | POPULARITY | TOP_CUTS | WINRATE`
- **`TimePeriod` enum:** `ALL_TIME | ONE_MONTH | THREE_MONTHS | SIX_MONTHS | ONE_YEAR | POST_BAN`
- Optional filter args available: `colorId`, `minEntries`, `minTournamentSize`, `after` (pagination cursor).

Sample result (`first:8, sortBy:POPULARITY, timePeriod:SIX_MONTHS`):
`Kraum / Tymna`, `Kinnan`, `Rograkh / Thrasios`, `Rograkh / Silas Renn`, `Sisay`,
`Etali`, `Thrasios / Tymna`, `Ral`.

**Note on "bracket 5":** edhtop16.com is a cEDH-only dataset — every entry is bracket-5
competitive. There is no `bracket` argument because the whole corpus IS bracket 5. The
`commanders(...)` result therefore IS the bracket-5 named meta. No invented API; the spike
verified an existing one.

### Downstream consequence (→ 31-03)

**31-03 adds ONE meta-wide client method** (e.g. `SearchTopCommandersAsync(first, sortBy,
timePeriod, ct)`) on `IEdhTop16Client` using the verbatim query above, mapping
`data.commanders.edges[].node.name`. **PRM-06 bracket-5 matchups inject these named
archetypes** into the `cedh-meta-macro-matchups` / `matchup-archetype-plan` sections.
Brackets 1-4 continue to use the 5 generic strategy buckets
(Aggro / Control / Midrange / Combo / Stax-Hate) — no EdhTop16 call for casual brackets.
Apply graceful degradation: if the meta query fails at runtime, bracket 5 falls back to the
same 5 generic buckets (mirror the `FindCombosAsync` null contract, D-2).

---

## Acceptance Self-Check

- [x] Exactly one combo-ranking verdict token: **`sufficient`**
- [x] Numeric full-31 byte count (30,931 B) + recommended Gemini `DefensivePromptCharCap`
      (32,000 chars) + bytes-vs-chars note (Pitfall 5)
- [x] Exactly one EdhTop16 verdict token: **`meta-query-available`** + verbatim query +
      response field path recorded
- [x] Each verdict states the 31-03/31-04 branch it activates
- [x] No production code committed — probe reverted; only `31-SPIKE.md` added
