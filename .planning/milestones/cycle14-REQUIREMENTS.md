# Requirements Archive: cycle14 Cycle 14 — Deeper Deck Evaluation

**Archived:** 2026-07-04
**Status:** SHIPPED

For current requirements, see `.planning/REQUIREMENTS.md`.

---

# Requirements: Cycle 14 — Deeper Deck Evaluation

**Milestone goal:** Extend the deck-analysis paste-artifact engine with three deeper read dimensions — an interaction & answers audit, a win-condition & combo map, and an opening-hand / mulligan evaluator — each building on the already-shipped engine (DeckStatClassifier, Commander Spellbook, the Monte-Carlo castability simulation, multi-axis score) with zero new dependencies, flag-gated and byte-identical when OFF.

**Core value alignment:** Every requirement produces or strengthens output the user can paste into ChatGPT / Claude / Gemini and get a useful answer in one round-trip. Interaction audit and win-con map fold into existing paste artifacts; the mulligan evaluator surfaces on the manabase tool where the simulation already runs. Heuristic reads are framed as a first-pass the AI re-checks — never presented as authoritative — preserving the one-round-trip trust.

**Scope decisions (2026-06-30, research-backed):** Zero new deps (every input already hydrated). Mulligan routing = **3a, surface on `/manabase`** (the London-mulligan sim already lives there; cheap; no contradiction with the manabase tool's own numbers; avoids per-request sim cost on the 512MB tier). Combo assembly-band = **Spellbook-grounded** — capture the already-parsed-but-dropped `manaValueNeeded` (and `popularity`) field. Stax/protection classification = **coarse presence only**, backed by a curated in-repo static list + golden tests (not exhaustive substring heuristics). Build order (roadmapper confirmed): interaction → win-con → mulligan.

---

## Cycle 14 Requirements

### Interaction & Answers Audit (INTERACT)

- [x] **INTERACT-01**: In the `/deck-analysis` output (paste artifact + on-page readout), the user sees the deck's interaction counted and bucketed — targeted removal, board wipes, counterspells, protection/recursion, and stax/taxation (coarse presence) — with the cards behind each count shown.
- [x] **INTERACT-02**: The audit flags coverage GAPS as a short advisory (e.g. "0 counterspells", "no graveyard hate"), framed as a heuristic first-pass the AI re-checks — explicitly not authoritative counts.
- [x] **INTERACT-03**: The interaction block is flag-gated (`analysis.interaction-audit`, seeded OFF in both dialects with a catalog description), byte-identical when OFF (pages AND zips), and renders in all three prompt variants (ChatGpt/Claude/Gemini) with no shared helper (ADR-0001, parity test).

### Win-Condition & Combo Map (WINCON)

- [x] **WINCON-01**: The user sees an enumerated win-condition / combo map — the deck's combos (Commander Spellbook `IncludedCombos`) plus near-combos (`AlmostIncludedCombos`, the one-card-away redundancy signal) — with how many assembly paths exist.
- [x] **WINCON-02**: A coarse assembly-band read ("comes online early / mid / late" — bands, NOT hard turn numbers), grounded in the combos' `manaValueNeeded`; the parser is updated to capture that already-parsed-but-dropped field (and `popularity`).
- [x] **WINCON-03**: Commander Spellbook failure is disclosed ("combo data unavailable") rather than implied as "no win conditions"; non-combo closers (closing-power cards) are noted so a combo-less deck still gets a win-condition read.
- [x] **WINCON-04**: The win-con/combo block is flag-gated (`analysis.wincon-map`, seeded OFF both dialects + catalog description), byte-identical when OFF (pages AND zips), and renders in all three prompt variants with no shared helper (ADR-0001, parity test).

### Opening-Hand / Mulligan Evaluator (MULLIGAN)

- [x] **MULLIGAN-01**: On the manabase tool, the user sees a keepable opening-hand probability for the deck as a discrete metric, plus a color/curve read.
- [x] **MULLIGAN-02**: The evaluator shows the London mulligan PROCESS — representative openers with the keep / mulligan-to-6 / bottom decisions the simulation makes — so the user sees how the deck's hands actually resolve, not only an aggregate percentage.
- [x] **MULLIGAN-03**: Hand quality is judged by ON-CURVE CASTABILITY — whether a kept hand's spells can actually be cast on-curve turn-by-turn (using the hand's lands, expected draws, and ramp timing the simulation already models), not merely whether total mana is sufficient.
- [x] **MULLIGAN-04**: A "has a plan" read — each kept opener is classified for whether it supports a coherent opening line (lands + color access + an early play + a payoff/path), surfaced as a hand-quality flag. This is opening-hand EVALUATION ("workable line / no clear line"), NOT a turn-by-turn play advisor.
- [x] **MULLIGAN-05**: All mulligan reads reuse the existing single Monte-Carlo simulation pass (no second simulation, no upstream re-fetch) and never report a figure that contradicts the manabase tool's own keep/cast numbers.
- [x] **MULLIGAN-06**: The mulligan evaluator is flag-gated (`analysis.mulligan-eval`, seeded OFF both dialects + catalog description), byte-identical when OFF.

---

## Out of Scope (anti-features)

Deliberately excluded this cycle (research-identified noise / false precision / off-thesis):

- **Exhaustive stax/protection classification** — coarse presence only; chasing 100% oracle-text accuracy adds noise.
- **Hard assembly-turn numbers** — bands only; precise turns are false precision.
- **Per-card grading** — no card-by-card scores.
- **Mulligan DECISION advisor** — the evaluator rates an opener; it does NOT recommend keep-or-mull or tell the user what to play turn by turn.
- **Win-rate / win-percentage** — no simulated match outcomes.
- **"Fix my deck" auto-suggestions** — the AI does that in the round-trip; DeckFlow supplies the read, not the rebuild.
- **Matchup / meta-threat read** — deferred (deepens cedh-meta-gap, a separate lane).

---

## Traceability

Phase mapping confirmed by the roadmapper: 79 INTERACT · 80 WINCON · 81 MULLIGAN. All 13 requirements mapped to exactly one phase; no orphans.

| Requirement | Phase | Status |
|-------------|-------|--------|
| INTERACT-01 | Phase 79 | Complete |
| INTERACT-02 | Phase 79 | Complete |
| INTERACT-03 | Phase 79 | Complete |
| WINCON-01 | Phase 80 | Complete |
| WINCON-02 | Phase 80 | Complete |
| WINCON-03 | Phase 80 | Complete |
| WINCON-04 | Phase 80 | Complete |
| MULLIGAN-01 | Phase 81 | Complete |
| MULLIGAN-02 | Phase 81 | Complete |
| MULLIGAN-03 | Phase 81 | Complete |
| MULLIGAN-04 | Phase 81 | Complete |
| MULLIGAN-05 | Phase 81 | Complete |
| MULLIGAN-06 | Phase 81 | Complete |
