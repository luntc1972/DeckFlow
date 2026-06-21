# Phase SPEC — Manabase: Casual/cEDH Modes + Per-Card Castability List

**Status:** Draft spec (pre-plan). Research backing: `.planning/manabase-mode-research.md`.
**Feature flag:** ships under existing `feature.manabase.enabled` (no new flag).

---

## Goal

1. Add a **mode toggle** to the Mana Base analyzer: **Casual** (default) and **cEDH**. Mode changes the land-count target and the colored-source consistency targets per the research.
2. In **Casual** mode, add a **Castability** section: a table of the deck's spells showing **% chance each card is castable on its mana-value turn**, sorted **lowest chance → highest**.
   - The probability **includes mana rocks and dorks** in the math (they accelerate / supply color).
   - Rocks and dorks are **NOT shown as rows** in the table — only real spells appear.

---

## Scope decisions (locked)

- **Default mode = Casual** so existing behavior/output is unchanged for users who don't pick cEDH.
- **Castability section is Casual-only for v1.** (The math runs in both modes; we surface it in Casual now, can expose in cEDH later.)
- **No new feature flag, no new NuGet, no schema change.** Pure compute + view additions.
- **Rocks/dorks**: counted in BOTH the mana-quantity pool and the color-source pool for the probability; excluded from the displayed list.

---

## Castability model (v1)

For each displayed spell, **Castability = P(enough mana by turn T) × P(enough colored sources by turn T)**, where **T = spell.ManaValue** (the on-curve turn), on the play, 7-card opener.

The codebase already computes the joint of these in `KarstenManabase.CastConsistency` (the `joint` value before its conditional division is exactly `P(≥pips colored sources AND ≥M lands by turn M)`). We extend that to an **unconditional, ramp-aware castability** number:

### Color part — reuse, with rocks/dorks included
- Colored sources already sum **all** sources producing the color (lands + non-land), via `ManabaseAnalyzer.EffectiveSources` (the `IsLand` flag only gates the land-drop count, not color supply). So "include rocks and dorks in the color calc" is already the behavior — keep it.
- Turn-1 (MV ≤ 1) spells use **untapped-only** sources (existing rule). MV ≥ 2 uses all sources.

### Mana-quantity part — include ramp via an effective-turn heuristic (v1 approximation)
Exact ramp timing (deploy cost + summoning sickness) is out of scope for v1. Use a defensible heuristic, flagged in the UI as an estimate:

- **MV ≤ 2 spells:** lands only. `P(≥MV lands by turn MV)` = `Hypergeometric.AtLeast(deckSize, lands, cardsSeen(MV), MV)`. (Ramp can't realistically come online before turn 2–3, so it doesn't help the 1- and 2-drops on curve.)
- **MV ≥ 3 spells:** credit ramp that has time to come online. Effective mana pool = `lands + rampSources` where `rampSources` = mana rocks/dorks of MV ≤ (T − 1). Compute `P(≥(MV − expectedRampMana) lands ...)` OR, simpler and recommended for v1: `P(≥MV mana-sources by turn MV)` against the combined `lands + weightedRamp` pool, treating each ramp piece as a fractional source (rock/dork weight already on `ManaSource.Weight`, e.g. 0.5–0.75).
- This reuses `Hypergeometric.AtLeast`; the only new input is the ramp-augmented source count and a per-spell turn check.

> **Design note for the plan phase:** decide between (a) "net the requirement down by expected ramp mana" vs (b) "add weighted ramp to the source pool." Pick one, unit-test against a hand-checked example, and document the choice inline. Both are approximations; (b) is closer to the existing source-counting idiom.

### Output per card
`CardCastability { Name, ManaValue, OnCurveTurn (=MV), CastPercent (0–100), LimitingFactor ("mana" | "color:<X>" | "both") }`

The `LimitingFactor` tells the user whether mana quantity or a specific color is the bottleneck (compute by comparing the mana-part and color-part probabilities).

---

## Mode parameters (from research)

| Parameter | Casual (default = today) | cEDH |
|---|---|---|
| Land target | Karsten singleton regression (unchanged) | Lower: apply competitive adjustment, **floor ~28**, target band **28–32**. Plan picks: flat −3 to −4 vs casual output **clamped to ≥28**, with fast-mana/rocks fully credited. |
| Consistency threshold | (89+M)% (unchanged) | Keep high color-access consistency, but **evaluate cheap-interaction color access at turns 1–3**, not at the spell's MV. |
| Tapped lands | tolerated | weight untapped-only heavily for early turns |
| Land floor for low curve | add a low-MV floor so a <2-MV deck isn't told to run 38 | inherent (low target) |

cEDH mode is primarily a **land-target + early-color emphasis** change in v1. The detailed cEDH castability surface can follow.

---

## Requirements

- **MODE-01** — `ManabaseMode { Casual, Cedh }` enum in Core. `ManabaseDeck` (or analyzer call) carries the mode. Default Casual.
- **MODE-02** — Land-target path branches on mode: Casual = current; cEDH = competitive-adjusted target with ≥28 floor. `KarstenManabase` gains a cEDH variant or a mode parameter (no change to existing casual signature behavior).
- **MODE-03** — Form gains a Casual/cEDH selector (radio). `ManabaseRequest.Mode` (default Casual) threads controller → service → classifier/analyzer. Re-renders selected mode on postback.
- **MODE-04** — `ManabaseReport.Summary` and the swap prompt mention the mode used.
- **CAST-01** — Core computes `IReadOnlyList<CardCastability>` for the deck (all non-land, non-ramp spells), each with on-curve cast %, ramp+color included in the math, sorted ascending by `CastPercent`.
- **CAST-02** — Rocks/dorks/lands excluded from the `CardCastability` list rows; still counted in the probability pools.
- **CAST-03** — Casual report renders a Castability table (worst-first), with a one-line "estimate, on the play, on-curve" caveat and the `LimitingFactor` per row.
- **COLOR-AGG-01** — Color findings must reflect **all** cards needing a color (mean castability + count under-supported), so a single easy card no longer makes a strained color look fine and a single bomb no longer is the only thing considered. BUT the verdict must still surface a lone unsupported bomb: keep the worst-driver spell + its required sources, and rank `WeakestColor` by a tail-risk-first composite (any under-supported → worst-spell cast% → mean cast% → deficit). Land target stays unchanged; casual color-findings output changes.
- **REDUCE-01** — Always-on static cost reducers ("<Type>? spells **you cast** cost {1} less", e.g. Goblin Electromancer; type-scoped) shift the **effective cast turn** of matching spells earlier. `OnCurveTurn` becomes `< ManaValue` for affected spells and drives the castability math. v1 = best-effort oracle-text heuristic, optimistic (assumes the reducer is on board), flagged. **Excluded in v1:** "for each", affinity/improvise/convoke/delve, one-shot/ritual discounts, opponent-symmetric/opponent-only text.
- **GRANT-01** — Mana-ability **granters** — Relic of Legends (legendary creatures tap for any color), Cryptolith Rite (creatures tap for any color), Paradise Mantle, etc. — turn otherwise-non-mana permanents into conditional color sources. When present, count the enabled permanents as additional weighted, multi-color sources in both the mana and color pools. v1 = best-effort heuristic, low weight, flagged.
- **COMMANDER-01** — The commander is the most important spell to support: it starts in the command zone, is available **every** game, and is typically recast on or near curve, repeatedly. Its colors therefore demand the highest-priority support. The analyzer must: (a) mark the commander's color demand as elevated so it can never be averaged away in COLOR-AGG (a commander color that is under-supported always surfaces as the/a weakest color, and its required-sources use the worst-driver, not the mean); (b) include the commander(s) in the castability list, flagged/pinned, since "can I cast my commander on curve" is the headline question; (c) keep crediting `commanderCount` in the land target (already done). For a partner/background pair, treat each commander's colors with the same elevated priority. The commander's own colors also define which colors "count" as the deck's identity for fixing heuristics.
- **FORMULA-01** — Two expandable panels on the page (collapsed by default): (1) **"How the analysis works"** — the methodology/formula (Karsten regression + Monte-Carlo castability model + aggregation + commander weighting), shown even before a deck is entered; (2) **"This deck's numbers"** — the formula evaluated for the entered deck (land-target terms plugged in, per-color source tally incl. dual/any/fetch crediting, sim parameters, effective turns). "Show the work" so a verdict is auditable. May require the analyzer to surface a small additive breakdown (regression term values + sim params) for panel (2). Native `<details>`; styles in `site-common.css`; mobile-safe.
- **COMMANDER-02** — **Commander importance is a user input that scales the formula**, because it varies by deck: Brago wants to be cast as early and as every-game as possible (the manabase should bend to it), while a value/late commander barely affects the base. Add a user-selected `CommanderImportance`:
  - **Central** ("must cast ASAP, every game" — Brago, voltron, combo-piece commander): the commander's on-curve castability and color access dominate. Target a stricter consistency for its colors (e.g. require its colors at the higher cEDH-style threshold and prioritize **untapped** early sources), force its colors to the top of the weakest-color ranking, and weight its castability heaviest in the summary verdict.
  - **Standard** (default): COMMANDER-01 elevated-but-not-dominant behavior.
  - **Low** ("optional / situational / late value"): treat the commander roughly like a normal spell — no special elevation, normal threshold.
  The level threads from the form through to the analyzer and scales the COMMANDER-01 weighting (and the commander's effective consistency target) accordingly.
- **CAST-04** — Colorless spells that are NOT mana sources (Ugin, Wurmcoil, Karn) appear in the list with castability = mana-quantity probability only (P_color = 1.0, limiting factor "mana"). Only mana-producing rocks/dorks are hidden from rows. Variable-cost (X) spells remain excluded.

## Success criteria

- **SC1** — Switching Casual→cEDH on the same deck lowers the land target into ~28–32 and the page shows the mode.
- **SC2** — Casual Castability table lists only real spells (no Sol Ring / Birds of Paradise / lands as rows), sorted lowest % first.
- **SC3** — A high-MV double-pip spell shows a lower % than a 1-mana single-pip spell on the same deck (sanity of ordering).
- **SC4** — Hand-checked example: a known deck/land/source config matches a manually computed castability within ~1–2 pts (unit test with fixed numbers).
- **SC5** — Build clean; Core + Web unit tests added; Playwright smoke (desktop + mobile) across themes shows the new section without overflow.
- **SC6 (VALIDATE-01)** — Per-color **raw source counts** cross-checked against the **Salubrious Snail mana calculator** for 2–3 reference decks, deltas logged in `64-VALIDATION.md`. Blocker = an unexplained > 2-source divergence on a problem color (counting bug). A weakest-color flip is **investigate-required, not an automatic blocker** (our COLOR-AGG composite scores differently than their model).
- **SC7** — Color findings aggregate all cards (COLOR-AGG-01): a single greedy multi-pip bomb no longer alone determines a color's verdict.
- **SC8** — A cost reducer raises a matching spell's cast% (effective turn earlier); a mana-granter (Relic of Legends / Cryptolith Rite) raises affected color source counts.

---

## Blast radius (side-effects)

**Direct:**
- `DeckFlow.Core/Manabase/`: `ManabaseModels.cs` (add `ManabaseMode`, `CardCastability`, extend `ManabaseDeck`/`ManabaseReport`), `KarstenManabase.cs` (cEDH land target; expose unconditional castability helper), `ManabaseAnalyzer.cs` (build castability list; branch land target on mode), `ManabaseClassifier.cs` (flag ramp sources / pass mode), `ManabaseSwapPromptBuilder.cs` (mention mode).
- `DeckFlow.Web`: `Models/ManabaseRequest.cs` (+Mode), `Models/ManabaseViewModel.cs` (expose castability), `Controllers/ManabaseController.cs` (pass mode), `Services/Manabase/ManabaseAnalysisService.cs` (thread mode), `Views/Deck/Manabase.cshtml` (mode radio + castability table).

**Transitive:** existing manabase tests (`ManabaseAnalyzerTests`, `ManabaseClassifierTests`, `KarstenManabaseTests` if present, `ManabaseAnalysisServiceTests`, controller flag-gate test) — extend, don't rewrite. Default-Casual keeps current assertions valid.

**Contract changes:** `ManabaseReport` gains a property (additive, non-breaking). New enum defaults to Casual so existing callers/serialization unaffected. No DB, no API, no config.

**Shared state / external:** none. Pure CPU + Scryfall (unchanged).

**Backward compat:** Casual default = byte-identical land math to today. cEDH is opt-in. No persisted data touched.

**Tests to add:** castability ordering + value (CAST-01/02/04, SC3/SC4), cEDH land floor (MODE-02/SC1), classifier ramp-exclusion-from-rows, view smoke (SC5).

**Open questions for plan phase:**
1. Ramp-inclusion method (a) net-down requirement vs (b) weighted pool — pick + test.
2. cEDH land target: flat −3/−4 vs a re-fit competitive coefficient — pick the simpler that hits 28–32 on real lists.
3. Should castability show on-the-play only, or play+draw average? (v1: on the play, matches existing model.)
4. Mobile layout for a multi-row table across 24 themes (no horizontal overflow).

---

## Suggested phase breakdown (for /gsd-plan-phase)

- **Wave 1 (Core math):** MODE-01/02, CAST-01/02/04 + unit tests. Pure, no UI.
- **Wave 2 (Web wiring + view):** MODE-03/04, CAST-03 + Web tests + Playwright smoke.

Per project rule, a web-page change ships with xUnit + Playwright (smoke/functional) and desktop+mobile verification across themes in the same change.
