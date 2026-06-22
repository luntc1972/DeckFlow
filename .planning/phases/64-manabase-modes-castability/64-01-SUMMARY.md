# 64-01 Summary — Manabase Core: modes, castability sim, COLOR-AGG, commander importance

**Status:** SHIPPED (reconstructed on reconcile 2026-06-22) · **Date executed:** 2026-06-21

> Reconstructed during a main-branch planning reconcile (2026-06-22). The work was
> implemented and committed directly to `main` across several ad-hoc sessions, then
> deployed to prod, but the GSD SUMMARY was never written. This file closes the plan
> from git history + `64-VALIDATION.md`; it is not a live execution log.

## What shipped (requirements MODE-01/02/04, CAST-01/02/04, COLOR-AGG-01, REDUCE-01, GRANT-01, COMMANDER-01/02)

Core `DeckFlow.Core/Manabase/*`:

- **`ManabaseMode { Casual, Cedh }`** — `Analyze` defaults to Casual so existing callers stay
  byte-identical; cEDH lowers the land target (Brago: 32.3 < casual 35.8) and never below the 28 floor.
- **Castability list** — ascending-by-CastPercent list of the deck's colored payoff spells;
  mana sources (`IsManaSource` rocks/dorks) excluded from rows but still counted in the mana/color
  pools; non-source colorless spells (Ugin/Wurmcoil) included with `P_color = 1.0`.
- **COLOR-AGG-01** — color findings now aggregate every card of a color (pip-weighted required
  sources + under-supported count + mean castability); `WeakestColor` = lowest aggregate color
  castability, no longer a single-spell driver. Casual land target byte-identical to before.
- **Cost reducers (REDUCE-01)** — matching spell's effective cast turn shifts earlier
  (`OnCurveTurn < ManaValue`), raising its castability.
- **Mana-ability granters (GRANT-01)** — Relic of Legends / Cryptolith Rite add conditional
  weighted multi-color sources to the pools.
- **Commander importance (COMMANDER-01/02)** — `enum CommanderImportance { Central, Standard, Low }`
  (default Standard), orthogonal to Mode: it only adjusts commander-color evaluation + summary
  weighting, never the land target. `Casual+Central` headlines Brago (53%, pinned); `Standard/Low`
  headline the global worst — verified orthogonal (land target unchanged at 35.8).
- **Monte-Carlo `CastabilitySimulator`** — replaced the pessimistic `P_mana × P_color` independence
  product with a seeded joint-event sim (London mulligan, ramp deployed in-sim, ETB-tapped lands
  online next turn, conditional granted sources). Closed FINDING-3.

## Validation (VALIDATE-01 — cross-check vs Salubrious Snail)

Brago, King Eternal (WU, Central) real-Scryfall harness vs Salubrious Snail:
- Weakest color **Blue** = MATCH; best-add **+1 Island** = MATCH; per-card ordering = MATCH.
- Post-sim per-card cast% within **mean |Δ| 3.4 pts** of Snail (was ~30 pts low pre-sim).
- Full evidence: `64-VALIDATION.md`, `64-harness-brago-output.md`.

## Commits (on `main`)

`59798e20` (Casual/cEDH modes + castability sim + formula panels) · `88724d84`, `422dab0e`
(pill/toggle + zero-cost MV / etched fixes) · Codex-review fixes `82f6e197`, `7c408385`,
`9b98d8bf` (gold contention, sim-ranked driver, cap-guard + 3-color gold regression).
A subsequent accuracy pass (mulligan/verdict/delay) is tracked in `phases/manabase-accuracy/`.

## Notes / deviations

- ⚠ Implemented directly on `main`, not on a milestone branch (milestone-branch rule deviation —
  already shipped + deployed to prod, recorded here for honesty).
- Codex acted as reviewer only (per the active "Claude codes / Codex reviews" override).
- Core.Tests green at execution time; build clean.
