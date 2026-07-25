# EDHREC empirical land counts — top-10 commanders × bracket

**Date:** 2026-07-16
**Source:** EDHREC average-deck JSON API (`https://json.edhrec.com/pages/average-decks/<slug>/<bracket>.json`; bracket slugs exhibition/core/upgraded/optimized/cedh = B1..B5). Each value is EDHREC's average over thousands of real decklists.
**Purpose:** empirically test whether the commander's cost/abilities drive the manabase (they mostly don't — bracket does).

## Lands by bracket + overall

| Commander (CMC) | ability | B1 | B2 | B3 | B4 | B5 | ALL | EDHREC ramp (ALL) |
|---|---|--:|--:|--:|--:|--:|--:|--:|
| The Ur-Dragon (9) | draw + cost-reduce | 35 | 37 | 36 | 35 | 34 | 35 | ~13 |
| Edgar Markov (6) | none (tokens) | 36 | 35 | 35 | 34 | 31 | 36 | 7 |
| Y'shtola (4) | draw (conditional) | 36 | 37 | 35 | 33 | 27 | 35 | 10 |
| Atraxa (4) | none | 36 | 35 | 36 | 36 | 32 | 35 | 11 |
| Krenko (4) | none (tokens) | 35 | 35 | 35 | 33 | 30 | 34 | 8 |
| Kaalia (4) | cheat creatures | 36 | 36 | 35 | 36 | 30 | 36 | 11 |
| Ms. Bumbleflower (4) | draw (conditional) | 36 | 36 | 35 | 34 | 30 | 35 | ~13 |
| Vivi Ornitier (3) | **taps for mana** | 33 | 33 | 32 | 33 | 26 | 31 | 9 |
| Sauron (6) | draw (wheel) | 38 | 37 | 37 | 35 | 30 | 36 | 9 |
| Teval (4) | land-fetch (grave) | 42* | 36 | 36 | 36 | 30 | 36 | 9 |
| **bracket mean** | | 36.3 | 35.7 | 35.2 | 34.5 | 30.0 | 34.9 | |
| **bracket SD** | | 2.24* | 1.19 | 1.25 | 1.20 | 2.14 | 1.45 | |

\* Teval B1 = 42 is a tiny-sample outlier (B1 has ~134 decks). cEDH (B5) excluding fast-mana/storm decks (Vivi 26, Y'shtola 27): mean 30.9, SD 1.36.

## Findings

1. **Power level (bracket) is THE driver.** Monotonic: 36 → 36 → 35 → 34.5 → **30** (cEDH −5 to −6). Confirms general research (cEDH 28–31, casual 36–37) with hard per-commander data.
2. **Commander CMC ≈ no effect on land count.** Ur-Dragon (9 CMC) = 34–37 across brackets, same band as 4-CMC commanders. The Burgess land-floor (31 + colors + CMC) over-predicts by ~4 lands (Ur-Dragon: 45 predicted vs 35 real) → **rejected**.
3. **Commander abilities → little land effect, with ONE exception.** Draw/cheat/land-fetch commanders sit in the same ~35 band as vanilla ones. **Vivi (commander that taps for mana) is consistently ~2–3 lands lower in every bracket** — the only real "commander engine" land credit, and it's small + specific to mana-producing commanders.
4. **Expensive commanders absorb cost via RAMP, not lands** (Ur-Dragon 35 lands + 13 ramp = 48 sources). Ramp-by-commander-cost is already modeled in DeckFlow (`ManabaseRampDrawBudget`).

## Sample size & data quality (per-cell deck counts)

Each EDHREC cell is itself an average over N decklists. Underlying deck counts for these 10 commanders (total 98,202 decks):

| Bracket | per-commander range | total | meaningful? |
|---|---|--:|---|
| B1 exhibition | 50–136 | 889 | NO — too thin per commander (Teval's 42-land outlier came from 50 decks) |
| B2 core | 696–4,222 | 26,058 | YES |
| B3 upgraded | 2,625–5,580 | 40,175 | YES (strongest) |
| B4 optimized | 1,369–3,725 | 26,319 | YES |
| B5 cEDH | 118–838 (Vivi 2,437) | 4,761 | ONLY for genuinely-cEDH commanders (Vivi, Y'shtola); thin for the casual-favorite 8 |

**Rule: treat a per-commander-per-bracket cell as meaningful only at ≥ ~400 decks.** Flag/exclude thinner cells.

**Between-commander sample size** (how many commanders to estimate a bracket's population mean, using the between-commander SD):
- ±1.0 land (adequate for a land recommendation): ~10–15 commanders (tight brackets B2–B4), ~20–25 (wide).
- ±0.5 land: ~25–30 (tight), ~50–60 (wide).
- Regression-grade (resolves secondary effects — mana-producer credit, colors, archetype): ~50 commanders × B2–B4.

**Practical study design:**
- **Casual/mid/optimized baseline (B2–B4): already meaningful at n=10** (thousands of decks/cell). Expand to ~30–50 commanders for ±0.5-land precision + secondary effects.
- **cEDH baseline (B5): sample ~20–30 commanders that are ACTUALLY cEDH-tier** (Kinnan, Thrasios/Tymna, Najeela, Rograkh/Silas, etc.) so their B5 cells hold thousands of decks — do NOT average thin cEDH cells of casual-favorite commanders.
- **Drop B1** as a per-commander metric (read "casual ≈ 36" from B2/Core).

## Large-sample confirmation (50 commanders, B1–B4, ≥400-deck floor, cEDH cut)

Swept the EDHREC top-50 commanders; 148 cells cleared the ≥400-deck floor (~337k decks). Raw: `2026-07-16-edhrec-50commander-B1-B4-rows.json`.

| Bracket | n | mean lands | SD | 95% CI | decks |
|---|--:|--:|--:|--:|--:|
| B2 core | 49 | 35.9 | 1.46 | ±0.4 | 124,221 |
| B3 upgraded | 50 | 35.5 | 1.39 | ±0.4 | 140,632 |
| B4 optimized | 48 | 34.5 | 1.57 | ±0.4 | 72,399 |
| overall (all-bracket) | 50 | 35.1 | 1.68 | ±0.5 | — |

B1 exhibition: only 1 commander cleared the floor → excluded (too niche).

- **Baseline is nailed:** SE ≈ 0.2 land/bracket; each mean solid to ±0.4 at 95%. Curve **36 → 35.5 → 34.5** (core→optimized).
- **Between-commander SD only ~1.4–1.6** → commander identity barely moves land count; confirms (n=50) that CMC/abilities don't drive it. Burgess floor + draw/ramp engine-credit remain rejected.
- **Data floor answer:** with SD ≈ 1.5, ~20–30 commanders gives ±0.3–0.4 land; 50 → ±0.2. Past ~50 = negligible for the baseline.

## DEFINITIVE — 100 commanders, abilities classified + joined to land counts

Classified all top-100 commanders' oracle text (draw / cost-reduce / mana-produce / land-fetch, heuristic regex, spot-validated) and joined to EDHREC land counts. Raw: `2026-07-16-edhrec-100commander-classified-rows.json`. Counts: draw 27, cost-reduce 6, mana-produce 11, land-fetch 2.

| Ability group | n | overall lands | B3 (upgraded, cleanest) |
|---|--:|--:|--:|
| draw only | 23 | 35.5 | 35.7 |
| cost-reduce | 6 | 35.0 | 35.3 |
| mana-produce / land-fetch | 13 | 33.9 (SD 2.81) | 35.0 |
| none | 60 | 35.2 | 35.5 |
| all | 100 | 35.1 | 35.5 |

**Verdict (n=100): NO commander ability reduces land count once power level is controlled.**
- Draw → no effect (35.7 vs 35.5). Cost-reduce → no effect (35.3).
- Mana/land-fetch → the −1.2 "overall" signal is a mirage: the group is bimodal — low values are cEDH combo commanders (Kinnan 27, Urza 31, Vivi 31), high are casual (Choco 38, Omnath 37, Zaxara 36). In B3 (no cEDH) it collapses to 35.0 = baseline. Sméagol/Vivi were archetype/bracket confounds, not a mana-ability effect.

**Every commander-ability manabase adjustment (draw/ramp engine-credit, cost floor, mana-producer credit) is REJECTED by data. Bracket is the only driver.** Only data-backed feature = bracket-graded land target (core 36 / upgraded 35.5 / optimized 34.5).

## Tool implications

- **Land target should key off bracket/power level, not commander cost.** DeckFlow already has Standard vs cEDH modes; data says cEDH target ≈ **30**, casual ≈ **36**. Candidate feature: a 5-band bracket-graded target (~36/36/35/34/30), extending the recent focused-tier work. Verify `CedhLandBaseline` ≈ 30.
- **Narrow, earned engine-credit:** commander that *taps for mana* → ~−2 lands. (Generic draw/ramp engine-credit and the Burgess cost-floor both rejected by data.)
