# Requirements: Cycle 13 — Deck Evaluation & Creator Output

**Milestone goal:** Extend the paste-artifact engine into deck *evaluation* (official bracket classification + balancer, multi-axis score) and *creator output* (auto-refreshing primer), plus surface the manabase tap-analysis DeckFlow already computes — closing the top uncontested gaps from the 2026-06-27 commander-feature-wants research.

**Core value alignment:** Every requirement produces or strengthens output the user can paste into ChatGPT / Claude / Gemini and get a useful answer in one round-trip. Bracket + score + tap metrics fold into existing paste artifacts; the balancer is itself a paste artifact; the primer stale-flag protects the creator artifact's correctness. No requirement adds a non-paste surface.

**Scope decisions (2026-06-27):** Balancer = hybrid (local floor-violation + starter cuts, AI refines) · Primer = stale-FLAG only (no auto-rebuild) · **Bracket = its own flag-gated tool tile** (`tool.bracket.enabled`, full registry entry) · **Multi-axis score folds into the existing `/deck-analysis` packet** (no new tile) · Power axis = proxy signals (Game Changers + combo density + fast mana), no card-quality DB.

---

## Cycle 13 Requirements

### Deck Bracket Classifier + Balancer (BRACKET)

- [x] **BRACKET-01**: User sees their deck auto-classified into the official 5-tier Commander bracket (B1 Exhibition … B5 cEDH), derived from Game Changers count, two-card-combo presence, and mass-land-denial / extra-turns / extra-cards detection — explicitly NOT tutor count (removed from the official rubric Oct-2025).
- [x] **BRACKET-02**: The Game Changers list and bracket rubric live as versioned data (a seed file stamped with an effective-date, loaded at startup and cached) — not a `.cs` literal — so a WotC update is a data change; the existing `DeckFlow.Web/Models/CommanderBracketCatalog.cs` hardcoded brackets are migrated into this model in `DeckFlow.Core`.
- [ ] **BRACKET-03**: User selects a target bracket and gets a paste artifact listing the deck's floor-violations (the specific cards/combos that exceed the target) plus a starter set of suggested cuts, framed so the AI refines them into fair swaps in one round-trip.
- [ ] **BRACKET-04**: The bracket classification and balancer output render in all three prompt variants (ChatGpt/Claude/Gemini) with no shared helper (ADR-0001), guarded by a parity test.
- [ ] **BRACKET-05**: The classification artifact is stamped with the Game Changers list effective-date and instructs the AI to re-confirm membership, so a stale list degrades gracefully rather than misclassifying silently.

### Multi-Axis Deck Score (SCORE)

- [x] **SCORE-01**: User sees their deck scored on four axes — Power, Speed, Control, Consistency — each a coarse 0-5 labeled band (no false-decimal precision).
- [x] **SCORE-02**: Speed and Consistency derive from signals DeckFlow already computes (avg MV, fast mana, ramp/draw-under-three, combo density, tutor count); Power derives from proxy signals (Game Changers count + combo density + fast mana); Control derives from a new interaction/removal classifier over deck categories.
- [x] **SCORE-03**: Each axis reports the signals that produced its band (inline rationale), and the score is cross-checked against the bracket classification for consistency.
- [x] **SCORE-04**: The multi-axis score block folds into the existing `/deck-analysis` paste artifact across all three prompt variants (ADR-0001, parity test) — no new tool tile.

### Auto-Refreshing Primer (PRIMER)

- [x] **PRIMER-01**: When a generated Deck Primer's source deck has changed since the primer was produced, the user sees a clear "deck changed — regenerate?" stale indicator.
- [x] **PRIMER-02**: Staleness is detected via a canonical card-name + quantity multiset hash (reusing the primer's existing cache-key computation); reordering cards or swapping printings does NOT flag stale, while adding/removing a card or changing a quantity DOES.
- [x] **PRIMER-03**: Regeneration is the existing explicit user action — no silent auto-rebuild, no upstream re-fetch hammering; the stale flag never clobbers a generated primer on its own.
- [x] **PRIMER-04**: Golden tests lock the staleness semantics (reorder / printing-swap = fresh; card add/remove / quantity change = stale).

### Tap Analyzer Surface (TAP)

- [x] **TAP-01**: The manabase report surfaces untapped-source frequency — how many of the deck's mana sources enter untapped, overall and per color — as a discrete metric.
- [x] **TAP-02**: The manabase report surfaces opening-turn (turn-1) untapped availability — the chance of having untapped mana of the needed colors on turn 1.
- [x] **TAP-03**: These metrics are read out of the existing `CastabilitySimulator` tapped/untapped state within its single simulation pass (no second pass, no new sim, additive `{ get; init; }` fields only), with a single source of truth per metric (no two contradictory untapped numbers in one report).
- [x] **TAP-04**: The tap metrics appear in both the `/manabase` page and its paste artifact, behind a namespaced feature flag seeded OFF (flag OFF = prod byte-identical).

---

## Future Requirements (deferred)

- **Section-scoped primer regeneration** — regenerate only the changed sections; Cycle 13 ships stale-flag only.
- **cEDH meta-gap deepening** — tie deck → EDHTop16 tournament cut/add + missing-staple flags (research gap #3-meta); separate milestone.
- **Standalone bracket/score tool tile** — if folding into `/deck-analysis` proves cramped, promote to its own flag-gated tool later.

## Out of Scope (explicit)

- **Folder-level deck sharing** — off-thesis (produces no paste artifact).
- **Live stream / Twitch deck overlays** — Arena/Twitch-bound, not paper-pod; outside DeckFlow's lane.
- **Rebuilding the manabase castability ENGINE** — already shipped P70-72; Cycle 13 only *surfaces* existing tap state.
- **A card-quality / card-strength database** — Power axis uses proxy signals + AI delegation instead.
- **Live Scryfall call in the bracket classification hot path** — Game Changers list is preloaded versioned data; Scryfall touches it only out-of-band to refresh the seed.
- **New NuGet / npm dependencies** — every feature builds on in-solution tech.

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| TAP-01 | Phase 75 | Complete |
| TAP-02 | Phase 75 | Complete |
| TAP-03 | Phase 75 | Complete |
| TAP-04 | Phase 75 | Complete |
| BRACKET-01 | Phase 76 | Complete |
| BRACKET-02 | Phase 76 | Complete |
| BRACKET-03 | Phase 76 | Pending |
| BRACKET-04 | Phase 76 | Pending |
| BRACKET-05 | Phase 76 | Pending |
| SCORE-01 | Phase 77 | Complete |
| SCORE-02 | Phase 77 | Complete |
| SCORE-03 | Phase 77 | Complete |
| SCORE-04 | Phase 77 | Complete |
| PRIMER-01 | Phase 78 | Complete |
| PRIMER-02 | Phase 78 | Complete |
| PRIMER-03 | Phase 78 | Complete |
| PRIMER-04 | Phase 78 | Complete |
