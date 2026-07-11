# Manabase Prior-Art & Community-Math Survey

Reconstructed from the deep-research fan-out run 2026-07-10 (workflow
`wf_f7a44f1e-592`, ~100 subagents: 21 source-claim sets + 75 adversarial
verify verdicts). The run was stopped before it wrote a synthesis, so this file
is the persisted digest. Primary sources are Frank Karsten's Channel
Fireball / TCGplayer articles plus community tools; only the minor refutals
noted at the end failed verification.

Purpose: the reference the DeckFlow manabase analyzer is validated against. When
this disagrees with `docs/manabase-analysis-rules.md`, the code is the source of
truth — see that doc's `file:line` citations.

---

## 1. Canonical Karsten numbers (all confirmed verbatim)

### Land-count regressions
- **60-card (2022):** `lands = 19.59 + 1.90·avgMV − 0.28·(cheap draw/ramp spells) + 0.27·companion`.
  At avgMV 3 → ~25–26 lands, cut one land per 3–4 cheap draw/ramp pieces.
  Fitted by least-squares on 95,143 winning tournament decks (MTG Melee + MTGO,
  Jul 2020–Jul 2022), R² 0.395, RMSE 2.75 lands.
- **99-card port (2022):** `31.42 + 3.13·avgMV − 0.28·(cheap draw/ramp)` → ~40–41 lands at avgMV 3.
- **Port method:** scale a 60-card figure by `99/60`, then **subtract 1.35 lands**
  for Commander's free mulligan + guaranteed turn-1 draw (calibrated: 26-land
  60-card optimum → 43.35 naïve, but optimal cheap-commander decks ran 42).
- **2017 rule of thumb:** `lands = 16 + 3.14·avgMV` (R² 0.614, 110 top decks).
  Scale to Commander ×`99/60`, to Limited ×`40/60`.

### Colored-source thresholds (2022 update)
- Consistency benchmark: **`(89 + M)%`** probability of drawing the needed N
  colored sources by turn M (≈90% one-drops, rising with mana value), conditional
  on hitting land drops.
- **99-card table** (assumes 41 lands): **19** sources for a T1 `C`, **30** for a
  T2 `CC`, **36** for a T3 `CCC`. Free mulligan + T1 draw lowered these vs the
  prior edition.
- **60-card table** (assumes 25 lands): **14** for T1 `C`, **21** for T2 `CC`,
  **23** for T3 `CCC`.
- Assumed land counts by deck size: 40→17, 60→25, 80→35, 99→41.
- Cards seen by turn: play `7 + turn − 1`, draw `7 + turn`.
- **On the play needs ≈ +1 source** vs on the draw for the same consistency.

### Fractional source weights (Karsten)
- **Fetchland:** full source for any color it can fetch.
- **Mana rock (Signet/Arcane Signet):** **0.75** source per color.
- **Mana creatures / cheap cantrips:** *not* counted as lands in his land-count math.
- **Land/spell MDFC (land-count credit):** **non-mythic = 0.38 land, mythic = 0.74 land**
  (zero-intercept regression on MDFC decks). ⚠ Note direction — see §5.

### cEDH / game-length curves (2023)
- Optimal 99-card lands depend on commander MV and assumed game length:
  turn-9 (long casual) → 38–39 lands + 13–14 Signets; turn-7 baseline → 38–42;
  **turn-5 (fast/cEDH) → 35–38 lands, 0 Signets.**
- Practical ritual-heavy cEDH: **29–33 lands**; Karsten explicitly *"wouldn't drop
  to 24–28."* A hit land drop ≈ a free Mox; a missed one ≈ a wasted ritual.
- Method: Monte Carlo + local-search optimizing **expected compounded mana over
  the first N turns** (not hypergeometric). 10k sims/deck, +1k per iteration, stop
  when best deck exceeds 200k sims.
- If a commander costs N, the optimal 99 contains **zero N-drops** (commander
  already fills that slot for free). Three-mana rocks are rejected in favor of
  two-mana rocks; one-mana dorks are very strong.
- Commander rules that must be modeled: free first mulligan (CR 103.4c) and the
  starting player **not** skipping their first draw in multiplayer (CR 800.7).

## 2. Probability model consensus
- **Hypergeometric distribution** = standard for single-category opening-hand draws
  (sampling without replacement). ~39.9% to see a 4-of in a 60-card opener.
- **Multivariate / Monte Carlo required** once you track 2 basics + duals or 9 card
  types — exact multivariate hypergeometric is "not feasible," so Karsten and most
  serious tools use Monte Carlo.
- Raw land count overstates color: a 19-land RW base is 72% to have 2 lands but
  only 59% to have both an R and a W — motivating per-color analysis.
- **Consistency tiers (community):** 75% = okay/non-critical, 85% = reliable,
  90% = very reliable/critical. Karsten calls the exact thresholds subjective.
- **Fetch deck-thinning is negligible** for drawing a specific card (~0.1 pt).

## 3. Simulation-methodology reference points
- Trial counts in the wild: landlord & ManaTuner Pro 10k–50k; a blog 100k
  ("nice round number"); Karsten 200k local-search cap; MTG-Mana-Simulator 10k.
  → DeckFlow's 5k-search / 20k-confirm sits inside the community band.
- **London mulligan** is modeled by the best tools (landlord's `london.rs`,
  MTG-Manacurve's hand-enumeration). Older sims (py `Manabase`, 2015) use fixed
  Vancouver-era keep heuristics.
- Cards-seen / on-curve-% is the shared output metric across tools.

## 4. Prior-art tools surveyed
| Tool | Kind | Notes |
|---|---|---|
| **landlord** (Rust→WASM, `mtgoncurve.com`) | sim | London + Never mulligan; **Vancouver is a stub**; Scryfall data; MIT; archived Dec 2025 |
| **ManaTuner Pro** (React/TS) | hypergeometric + MC | encodes Karsten's 90% table; 10k–50k trials; 0 stars (not a "community standard") |
| **ScrollVault calc** | hypergeometric | restates Karsten thresholds (some numbers lossy); claims 3.75M MC games, unverifiable |
| **MTG-Mana-Simulator** (Python/PyPI) | turn-by-turn sim | AI play decisions; models rocks/rituals/treasure/draw; **no mulligan** |
| **py `Manabase`** (2015) | exhaustive play-line search | 24 basics → 4-drops on curve only 65.5%; models fetch/scry/check lands + dorks |
| **MTG-Manacurve** (Java) | MC curve-opt | London via hand-enumeration maximizing mana spent; excludes ramp/draw |
| **AetherHub calc** | univariate hypergeometric | 4-input calculator; no multivariate/mulligan |
| **Command Zone template** | deckbuilding heuristic | 10–12 ramp, 10 draw, 10–12 removal; no land count |

## 5. Refutals & cautions from the verify pass
- **landlord Vancouver mulligan is NOT implemented** (source stub says "TODO
  unimplemented") — the claim that it has three working strategies is false; only
  London + Never work.
- **ManaTuner Pro "community reference point"** overreaches — repo has 0 stars/forks,
  created 2025-06-15.
- The turn-9 Karsten optimizer detail ("eagerly added banned fast mana as a sanity
  check") is contradicted by the primary text in one spot; numeric core holds.
- ⚠ **MDFC direction (historical):** the verified Karsten quote is **non-mythic 0.38 /
  mythic 0.74**. DeckFlow's old land-count credit used the reverse
  (`0.74·non-mythic / 0.38·mythic`) — inverted vs Karsten. This was moot in prod (MDFC
  backs were already real lands there) and the whole legacy credit path has since been
  **removed** (2026-07-10): MDFCs now count as real lands unconditionally, so the
  fractional credit — and its inversion — no longer exist.

## 6. How DeckFlow already compares
DeckFlow matches every core Karsten number (formula constants, `(89+M)%` threshold,
1.35 Commander reduction, cards-seen), uses the same Monte-Carlo choice Karsten
endorses, and is **more granular than any surveyed tool** on: a joint mana+color
castability event with real land-sequencing, the "with a plan" win-condition opener
stat (no prior art), conditional-untapped (bond/check/Snarl) land modeling,
deploy-friction ramp gating, and structural-vs-source-fixable demanding-card triage.

**Open follow-ups surfaced by the survey:** rituals/one-shot fast mana get zero
credit (a real cEDH hole); the cEDH land floor (28) sits under Karsten's stated
29–33 band.
