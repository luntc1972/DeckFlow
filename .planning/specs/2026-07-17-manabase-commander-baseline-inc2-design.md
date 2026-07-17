# Manabase Community Baseline — Increment 2: Per-Commander from EDHREC averages dump

**Date:** 2026-07-17
**Status:** APPROVED-PENDING-REVIEW (user decisions locked 2026-07-17; Codex plan-review pending)
**Branch:** `feat/edhrec-bracket-land-target` (continues Increment 1a/1b)
**Amends:** `.planning/specs/2026-07-17-manabase-bracket-baseline-v2-design.md` — this file **supersedes that spec's "Increment 2 — Map" section only**. Increment 1 content and disposition notes stand.

## Context — what changed

EDHREC developer (keattz, Discord, 2026-07-17) responded to our data request by publishing a **new sanctioned daily dump**: `https://edhrec.com/data/averages.tgz` — CSV of every commander (and partner pair) with average card-type counts, average land counts (total/basic/nonbasic), and deck count. Bundled `LICENSE.txt`: community use encouraged, **commercial use not permitted**, email edhrec@edhrec.com when live.

keattz explicitly: per-**bracket** and ramp/draw dumps are **not possible** on their side. Consequences:

- The old Increment 2 mechanism (scrape `average-decks/<slug>/<bracket>.json`, on-the-fly cache-miss fetch, write-through to the P1 `manabase_baseline` table) is **DROPPED** — replaced by the sanctioned dump.
- **Ramp/draw remain lands-only-deferred indefinitely** (no data source). The v2 spec's "ramp/draw arrive in Increment 2" is void.
- **No per-commander bracket dimension exists.** Commander means are bracket-agnostic aggregates over EDHREC's population, which is overwhelmingly casual (bracket 2–3).

### Dump facts (verified 2026-07-17)

- 6,553 rows; 3,211 partner pairs; 9,386,908 decks total; daily refresh; ~250 KB tgz.
- Columns: `commander, commander2, oracle_id, oracle_id2, avg_* (7 card types), avg_nonbasicland, avg_basicland, avg_land, number_decks`.
- Averages are **integer-rounded**. Acceptable: pilot showed commander-level SD ≈ 1.4 lands.
- Deck-weighted global `avg_land` = **35.13** — sanity-matches the Increment 1 B2 pilot mean (35.9). ✓
- Deck-count distribution: 3,179 commanders ≥ 100 decks; 2,216 ≥ 400; median 86. The P2 weighting thresholds (LOW=100 / HIGH=400) were chosen independently and map cleanly onto this distribution.

## User decisions (locked 2026-07-17)

1. **Bundled snapshot** — offline generator converts the dump into the committed data file (mirrors Increment 1 / `CedhLandBaselineProvider` precedent). No runtime fetch, no background job. Refresh at release time; a refresh job is a captured follow-up only if staleness ever matters (land averages move slowly).
2. **≥ 100-deck cutoff** — 3,179 commanders bundled. Below 100, P2 weighting ignores the commander cell anyway (LOW threshold); the sub-100 tail contains junk rows (`avg_land` 0–97).
3. **License accepted** — DeckFlow is free; non-commercial use is compatible today. **This data blocks future monetization of this feature** unless renegotiated. Attribution "Data from EDHREC" ships in the UI. Email edhrec@edhrec.com when live (owner action).

## Design

### Data file (additive, schemaVersion stays 1)

`DeckFlow.Web/Data/manabase-baseline/latest.json` gains a `commanders` array alongside the existing `brackets` array. Existing bracket rows (pilot values, source `edhrec-pilot-aggregate`) are **unchanged** — bracket baselines remain our own study's per-bracket means; the dump cannot replace them (no bracket dimension). Additive property ⇒ no schema bump. Compat claim, stated narrowly: the shipped Increment 1 provider **tolerates** the new file (`System.Text.Json` ignores unknown properties, so bracket lookups keep working) but cannot **serve** commander lookups until this increment extends the snapshot DTO and `IManabaseBaselineProvider` — that extension is the Inc2 work itself, not a compat risk.

```json
{
  "schemaVersion": 1,
  "generatedUtc": "...",
  "source": "edhrec-pilot-aggregate",
  "brackets": [ ...unchanged pilot rows... ],
  "commandersSource": "edhrec-averages",
  "commandersGeneratedFromDump": "jul26",
  "commanders": [
    { "name": "The Ur-Dragon", "avgLands": 35, "deckCount": 48802 },
    { "name": "Halana, Kessig Ranger", "partnerName": "Alena, Kessig Trapper", "avgLands": 36, "deckCount": 1234 }
  ]
}
```

- Fields per commander row: `name`, optional `partnerName`, `avgLands` (double — integers today, forward-safe), `deckCount`. Basic/nonbasic split **not bundled** (YAGNI; generator can re-emit later).
- `commandersSource` = `edhrec-averages` is snapshot-level for the commanders block; the UI derives the "Data from EDHREC" label from it.

### Offline generator (DeckFlow.CLI)

New CLI command (name at planning, e.g. `edhrec-averages`): input = path to extracted `averages.csv` (+ optional output path), output = `latest.json` rewritten with refreshed `commanders` array while **preserving the pilot `brackets` array**.

- Filter `number_decks >= 100`.
- Skip malformed rows (bad numerics, empty name) with a logged count; never throw on a single row.
- CSV parsing must handle quoted names with commas/apostrophes (`"Y'shtola, Night's Blessed"`).
- Deterministic ordering (by deckCount desc, then name) for stable diffs.
- Testable core: parsing/filtering/serialization extracted to `DeckFlow.Core` (e.g. `Manabase/EdhrecAveragesParser` or similar) so xUnit covers it without the CLI harness — CLI command is a thin wrapper (matches existing CLI/Core split).
- The raw dump/CSV is **not** committed; only the generated `latest.json`.

### Provider

`IManabaseBaselineProvider` gains:

```csharp
ManabaseCommanderBaseline? TryGetCommanderBaseline(string commanderName, string? partnerName = null);
```

- **Lookup key algorithm (exact, both generator and provider use the same helper):**
  1. Normalize **each commander name separately** with `CardNormalizer.Normalize` (never after joining — `CardNormalizer` rewrites `" // "` to `" / "` and truncates at the first `" / "`, which would destroy a joined pair key; per-name it acceptably collapses an MDFC commander to its front face, and both the dump side and the deck side collapse identically).
  2. For a pair, sort the two normalized components ordinally and join with `"||"` (cannot occur in a normalized card name).
  3. Key comparison is the normalized string, ordinal.
- Partner handling: a pair matches only the pair row (order-insensitive via the canonical sort); a lone commander matches only the lone row.
- **Duplicate keys after normalization** (e.g. two dump rows collapsing to one key): keep the higher `deckCount` row; generator logs the collision count. Deterministic, tested.
- Same fail-open snapshot cache as Increment 1 (one load, 24 h memory-cache, missing/corrupt ⇒ null).
- Snapshot dictionary built once at load (normalized key → row); 3,179 entries is trivial for the 512 MB tier.

### Weighting integration (ManabaseAnalysisService.ResolveBaseline)

- **Commander cell participates only when the effective bracket is 2 or 3.** Rationale: dump means are bracket-agnostic and the EDHREC population is dominated by casual decks; applying a 48k-deck commander mean under bracket 4/5 would drown the optimized/cEDH signal (e.g. Ur-Dragon 35 vs genuine-cEDH 30.5). Bracket 4/5 keep the Increment 1 bracket-global row.
- **cEDH display precedence (closes an Inc1 latent overlap):** when the result already carries the commander-keyed cEDH meta range from `CedhLandBaselineProvider` (Cedh mode, commander present in cEDH data), the community-baseline line is **suppressed** — the genuine commander-specific cEDH range supersedes the thin B5 global mean, and users must never see two differently-sourced "community" land baselines at once. When the cEDH range is absent (commander not in cEDH data, or non-Cedh mode at bracket 5), the B5 global community line renders as the fallback. Flag is still OFF in prod, so refining Inc1's shipped render rule here costs nothing.
- For brackets 2–3: `ManabaseBaselineWeighting.Compute(commanderLands, null, null, deckCount, globalLands, null, null)` — the shipped P2 logic used exactly as designed (Commander ≥ 400 / Blended 100–399 / Global < 100 or missing). Ramp/draw pass null (no data).
- Result block (`ManabaseCommunityBaseline`) gains the weighting outcome: value, `ManabaseBaselineSource` (Commander/Blended/Global), commander deck count when the commander cell contributed.

### UI (Manabase.cshtml baseline line)

- Line copy varies by source:
  - Commander: `EDHREC decks for {commander} average {n} lands ({deckCount:N0} decks)`
  - Blended: commander phrasing with combined framing decided at planning (keep one line, no stats jargon).
  - Global (unchanged Inc1 line): `Bracket {b} decks average {n} lands`.
- **Attribution label returns**: muted `Data from EDHREC` span when the displayed value came from (or blended with) the EDHREC commanders block. This also settles the two owed Increment 1b cosmetics: give `.manabase-baseline-source` explicit muted CSS (token-based, in `site-common.css` per theme rules — layout/common CSS never in `site.css`), and map raw source tokens to human copy (`edhrec-pilot-aggregate` no longer leaks to users).
- No new inputs; the bracket selector from 1b is unchanged.

## Non-goals

- Live/runtime EDHREC fetch of any kind (dropped with the old Inc2 map).
- Ramp/draw baselines (no data source exists; revisit only if EDHREC ships a new dump).
- Per-commander-per-bracket cells (impossible upstream).
- Touching the P1 `manabase_baseline` table — it stays **parked** (built, tested, unused). Its planned Inc2 role (write-through cache) died with the live-fetch path. Removal is a separate decision, not this increment.
- Replacing the pilot bracket rows with dump-derived values (dump has no bracket axis).

## Testing

- **Core generator**: header/quoting/partner parse, ≥100 filter, malformed-row skip+count, deterministic order, integer means round-trip as doubles.
- **Provider**: commander hit, partner-pair hit (both orders), lone-vs-pair non-collision, unknown ⇒ null, normalization (case, ASCII + Unicode punctuation/apostrophes, accents, MDFC front-face collapse), post-normalization duplicate-key collision ⇒ higher-deckCount row wins deterministically (+ generator logs count), corrupt file ⇒ null, old-schema file (no `commanders`) ⇒ bracket lookups still work + commander lookups null.
- **Weighting integration**: bracket 2/3 with ≥400-deck commander ⇒ Commander source; 100–399 ⇒ Blended value matches linear formula; <100 ⇒ Global; bracket 4/5 ⇒ Global regardless of commander cell; no commander identified ⇒ Global.
- **UI**: line copy per source; attribution span present only for EDHREC-sourced values; muted CSS class applied; flag OFF ⇒ byte-identical (regression).
- Full Core + Web suites green; live-e2e spec (`bbc292ed` pattern) extended only if selectors change (they shouldn't).

## Rollout

- Same flag `analysis.manabase.baseline` (OFF in prod) gates everything; Inc2 ships dark alongside Inc1.
- Pre-flip gate (unchanged from Inc1, now includes Inc2): pilot-number + commander-number eyeball, visual sweep, UAT, `[PostgresFact]` note from P1 review (unaffected by this increment but still owed before any DB-touching flip — P1 remains parked so not blocking).
- Owner actions at flip: email edhrec@edhrec.com; verify attribution renders.

## Open questions (for plan-review)

1. Blended-line copy wording (one line, human, no "blended" jargon).
2. Generator command name + whether it also refreshes `generatedUtc` on the snapshot root or only the commanders block.
3. Whether `commandersGeneratedFromDump` (dump folder tag, e.g. `jul26`) is worth keeping vs just `generatedUtc`.
