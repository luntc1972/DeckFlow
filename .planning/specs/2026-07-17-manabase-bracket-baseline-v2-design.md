# Manabase Bracket Baseline — v2 Design (data-file first, EDHREC later)

**Date:** 2026-07-17
**Status:** Approved design (brainstorm), pre-planning
**Supersedes:** `2026-07-16-manabase-bracket-baseline-design.md` (self-corpus aggregation approach). This v2 keeps that spec's *premise* (bracket is the only driver; show an empirical community baseline beside Karsten) but changes the **delivery mechanism**, **granularity ordering**, and **data source** based on the 2026-07-17 brainstorm.

## Why v2 (what the brainstorm changed)

The v1 spec proposed a corpus-aggregation job (`ManabaseBaselineJobService`) computing per-commander-per-bracket averages from DeckFlow's own crawl corpus. Recon killed that path for now:

1. **The corpus can't feed it.** DeckFlow's crawl stores only aggregated card→category observations — no per-deck decklists, no per-deck lands/ramp/draw, no per-deck bracket. Producing any corpus baseline needs card-fact classification + a bracket per deck across the whole corpus (expensive, multi-phase).
2. **The pilot says per-commander barely matters.** 50-commander EDHREC sweep: between-commander SD ~1.4 lands; *"commander identity barely moves land count; bracket is the only driver."* So the expensive per-commander corpus build buys little accuracy — its value is UX/trust, not correctness.
3. **A proven pattern already exists.** `CedhLandBaselineProvider` loads an empirical per-commander land baseline from a **bundled JSON** (`Data/cedh-land-baseline/latest.json`), regenerated offline. Mirroring it delivers the feature with no hot-path DB and no live dependency.

**Result:** the corpus-aggregation job (v1 Component 2 / "Phase 3") is **dropped**. The baseline ships as a bundled data file + provider. Per-commander and ramp/draw come from EDHREC in a later, permission-gated increment.

## Premise (unchanged, earned by data)

Power bracket is the only driver of mana-base shape. Show the **empirical community baseline** for the deck's bracket **alongside** (not replacing) the existing Karsten/castability target, so the user sees "what real decks run" next to "what the formula prescribes."

## Increments

| Increment | Content | Gated on |
|---|---|---|
| **1 (now)** | Bundled bracket **land** baseline (B2–B5), shown beside Karsten; deck bracket auto-classified with a selector override; flag-gated. Lands only, no per-commander. | Nothing |
| **2 (later)** | Per-commander rows + ramp/draw, offline-generated from EDHREC into the same file; **on-the-fly fetch** for cache-miss commanders (write-through to the `manabase_baseline` DB table); P2 weighting blends commander→bracket-global. | **EDHREC written permission** (outreach: `Downloads/DeckFlow-EDHREC-Outreach.docx`) |

## Non-Goals

- No change to castability / colored-source math (prescriptive side stays; Karsten line unchanged).
- No commander-ability manabase modeling (proven pointless).
- No corpus-aggregation job.
- **Increment 1:** no live third-party dependency on the hot path (the deck-bracket classifier's Commander Spellbook call is existing, graceful, and optional to the bracket result).
- **Increment 2** intentionally amends the v1 "no live per-request third-party dependency" non-goal, but only for a **cache-miss** commander lookup, gated on permission, with graceful fallback to the bracket baseline.

---

## Increment 1 — Design (now, unblocked)

### Component A — bundled baseline data file
`DeckFlow.Web/Data/manabase-baseline/latest.json` (checked in; mirror `Data/cedh-land-baseline/` layout):
```json
{
  "schemaVersion": 1,
  "generatedUtc": "2026-07-17T00:00:00Z",
  "source": "edhrec-pilot-aggregate",
  "brackets": [
    { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
    { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
    { "bracket": 4, "avgLands": 34.5, "deckCount": 72399  },
    { "bracket": 5, "avgLands": 30.5, "deckCount": 4761,   "note": "genuine-cEDH mean; casual-favorite cEDH cells excluded" }
  ]
}
```
- **Provenance:** the per-bracket land means already extracted in `.planning/research/2026-07-16-edhrec-bracket-land-data.md` (50-commander sweep, ≥400-deck floor). These are aggregate statistics, not EDHREC decklists. `source` records their origin for attribution/audit.
- **Bracket 1 (Exhibition) is not supported** — uncommon/wacky decks, not worth a baseline. The file holds B2–B5 only; a deck that classifies as B1 falls back to the B2 (Core) baseline (see Component C).
- Ramp/draw are **absent** in Increment 1 (no clean per-bracket ramp/draw means exist; they arrive with EDHREC in Increment 2). The file schema allows adding `avgRamp`/`avgDraw`/per-commander rows later without a breaking change.

### Component B — provider (Core or Web, mirror `CedhLandBaselineProvider`)
`IManabaseBaselineProvider` + `ManabaseBaselineProvider`:
- Loads `latest.json` once at startup (cached), same shape as `CedhLandBaselineProvider.TryGetBaseline`.
- `TryGetBracketBaseline(int bracket) -> ManabaseBracketBaseline?` (avgLands, deckCount, source). Missing bracket → null (feature line omitted).
- No DB, no HTTP.

### Component C — deck-bracket determination (reuse existing)
- Auto-classify the submitted deck via the existing `IBracketClassificationService.ClassifyAsync(deckSource)` (Game Changers local + Commander Spellbook combos [graceful-null] + mass land denial) → B1–B5.
- **B1 (Exhibition) is not a supported baseline bracket** — a deck classified B1 defaults the selector to **B2 (Core)** (nearest supported; the pilot reads B1 ≈ B2 ≈ 36 anyway). The selector offers B2–B5 only.
- The (possibly B1→B2-mapped) result **defaults** the bracket selector; the user may override within B2–B5.
- Commander Spellbook unavailable → classifier still returns a bracket from local signals; never blocks the baseline.
- This is the same rubric the *baseline* is bracketed by → apples-to-apples.

### Component D — analyzer / result augment
- The manabase result gains a `ManabaseBaseline` block: `{ Bracket, AvgLands, DeckCount, Source, BracketSource = auto|override }`.
- Karsten land target and `ManabaseRampDrawBudget` advisory are **unchanged**; the baseline sits beside them.
- Flag `analysis.manabase.baseline` (seed **OFF**). Off or no data → block absent → **byte-identical** output.

### Component E — UI
- **Bracket selector (B2–B5)** on the manabase page (new control; reuse `CommanderBracketCatalog.Options` labels but **omit Exhibition** → Core/Upgraded/Optimized/cEDH), defaulted to the auto-classified bracket (B1→B2), with a subtle "auto-detected" hint and override affordance.
- Display line beside the Karsten result, e.g.:
  > **Community baseline · Upgraded (n ≈ 140k decks): ~35.5 lands.** Your deck: 33. *Karsten target: 35.*
- Themes + mobile; absent when flag off.
- Any bracket-selector TypeScript compiles to gitignored JS (never committed).

### Error Handling (Increment 1)
- Missing/ malformed data file → provider returns null → baseline line omitted; analysis proceeds. Log once.
- Bracket classification failure/timeout → fall back to a mode-derived default bracket (Casual→3, Focused→3, Cedh→5) and mark `BracketSource = fallback`; never throw.
- Flag OFF → byte-identical.

### Testing (Increment 1)
- **Provider tests:** loads the bundled file; `TryGetBracketBaseline` returns each bracket; unknown bracket → null; malformed file → null (no throw).
- **Analyzer/result:** flag OFF → no block, byte-identical; ON with data → block with correct bracket avg + sample + source; bracket override respected.
- **Bracket determination:** auto-classified value defaults the selector; override wins; Spellbook-down still yields a bracket.
- **UI:** baseline line renders (bracket/avg/sample + Karsten target) desktop + mobile, 2 themes; absent when flag off; selector defaults to auto bracket.
- Regression: full Core + Web suites green with flag OFF.

---

## Increment 2 — Map (EDHREC-permission-gated; not built until permission)

Uses everything already shipped (P1 table + P2 weighting) rather than discarding it.

- **Offline CLI generator** (`DeckFlow.CLI`): reads EDHREC `average-decks/<slug>/<bracket>.json` for a target commander list, writes per-commander rows (`avgLands/avgRamp/avgDraw/deckCount`) + refreshed global rows into `latest.json`. Attribution "Data from EDHREC" + backlinks per ToS.
- **Ramp/draw** arrive here (EDHREC average lists include them); the bundled global rows gain `avgRamp/avgDraw`.
- **On-the-fly per-commander** (cache-miss path): commander not in `latest.json` → live EDHREC `average-decks` fetch → **write-through into the `manabase_baseline` DB table (built in P1)** → serve; fetch fail/absent → bracket-global. Graceful, permission-gated, off the common hot path.
- **P2 `ManabaseBaselineWeighting`** blends the commander cell (file or DB cache) toward the bracket-global by sample confidence (LOW=100/HIGH=400) — its designed purpose.
- **Read precedence (per metric):** bundled per-commander → DB on-the-fly cache → bundled bracket-global. Weighted by deck_count.

## Disposition of already-shipped work

- **P2 `ManabaseBaselineWeighting`** (Core, tested, pushed): kept, **latent** in Increment 1 (no commander cell → always resolves to bracket-global), the **core of Increment 2**. Not wasted.
- **P1 `manabase_baseline` table + store** (tested, pushed): **parked** in Increment 1 (provider reads JSON, not DB); **repurposed** in Increment 2 as the on-the-fly write-through cache. Not wasted, not reverted.
- **v1 spec `2026-07-16-...`:** superseded by this file; corpus-aggregation job not built.

## Backward Compatibility

- Additive + flag-gated OFF → zero behavior change until the flag flips.
- New data file + provider + bracket selector + result block; Karsten + ramp/draw math untouched.
- Data-file schema is versioned (`schemaVersion`) and forward-compatible (adding ramp/draw/per-commander rows is non-breaking).
- Compiled bracket-selector JS never committed.

## Open Questions / Assumptions

- **ToS comfort on the pilot seed:** Increment 1 bakes 5 aggregate land means derived from EDHREC data. Treated as aggregate statistics (not decklist redistribution). Owner (user) accepts this for Increment 1; full EDHREC use (Increment 2) waits for written permission.
- **Bracket selector default when classifier is uncertain:** use the mode-derived fallback (Casual/Focused→3, Cedh→5) and flag `BracketSource=fallback`.
- **Commander Spellbook call on the manabase path:** new for that path but graceful; if latency is a concern, the classification can run only when the flag is ON.
- **B1 unsupported:** Exhibition dropped (uncommon/wacky decks); B1 classification maps to the B2 baseline. **B5 caveat:** uses the genuine-cEDH mean (30.5), not thin casual-favorite cEDH cells — encoded as `note` in the data file.
- **Ramp/draw are lands-only-deferred by data, not choice:** the pilot recorded only lands per bracket (50-cmdr sweep = `[slug,bracket,lands,deckCount]`); per-bracket ramp/draw means do not exist in the research and require a fresh EDHREC `average-decks` pull → Increment 2.
- **Provider home (Core vs Web):** mirror `CedhLandBaselineProvider` (currently `DeckFlow.Web/Services/Manabase/`); confirm at planning.
