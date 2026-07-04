# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`
- ✅ **Cycle 11 — Security, Visibility Control & Creator-Lens** — Phases 64-69 (shipped 2026-06-25, `2026.06.8`) — see `.planning/milestones/cycle11-ROADMAP.md`
- ✅ **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** — Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- ✅ **Cycle 13 — Deck Evaluation & Creator Output** — Phases 75-78 (shipped 2026-06-30, `2026.06.10`) — see `.planning/milestones/cycle13-ROADMAP.md`
- 🚧 **Cycle 14 — Deeper Deck Evaluation** — Phases 79-81 (executing; Phase 79 done 2026-07-01) — see below

---

# Cycle 14 — Deeper Deck Evaluation

**Goal:** Extend the deck-analysis paste-artifact engine with three deeper read dimensions — an interaction & answers audit, a win-condition & combo map, and an opening-hand / mulligan evaluator — each layered on the already-shipped engine (`DeckStatClassifier`, Commander Spellbook, the Monte-Carlo castability simulation, multi-axis score) with zero new dependencies, flag-gated and byte-identical when OFF.

**Granularity:** coarse · **Phases:** 3 (79-81) · **Coverage:** 13/13 requirements mapped ✓

**Cross-cutting gate conditions (apply to EVERY phase — Pitfalls 4-8):**

- **Flag-OFF byte-identity** — with the flag OFF, the rendered page, all three paste artifacts, AND the zip round-trip are byte-for-byte the pre-feature baseline (per-surface byte-identity test, Phase-77 contiguous-suppressible-block pattern). The flag is seeded OFF in BOTH SQLite and Postgres seed SQL with a catalog description; seed-consistency tests cover it in each dialect.
- **ADR-0001 variant parity** — each paste-artifact section is hand-edited into all three decoupled prompt variants (ChatGpt/Claude/Gemini) with NO shared helper; a 3-platform parity test asserts presence + figures in each (Gemini is the classic omission). `/simplify`/review must not "fix" the intentional duplication.
- **Reuse, not rebuild** — no second Monte-Carlo `Simulate` pass and no second Commander Spellbook / Scryfall fetch; thread the single existing 20k-trial pass and the cached `CommanderSpellbookResult` (512MB web / 256mb PG tier).
- **Heuristic honesty** — every count/band/percentage is framed as DeckFlow's automated first-pass read the AI re-checks, with the cards/bands shown behind it — never presented as authoritative.
- **CI is the authoritative test gate** — a green-looking WSL local `dotnet test` masked Cycle 13's 2 CI failures; build the test projects, run targeted `--filter`, and confirm GitHub Actions green (push-and-watch) before close. Honor the changed-lines format gate / `.editorconfig` carve-outs (never re-indent raw-string prompt literals, never convert `{ get; init; }`→get-only).

**Resolved decisions (do NOT re-open during planning):**

- **Mulligan routing = 3a** — the mulligan evaluator surfaces on **`/manabase`** (`Manabase.cshtml` + its paste artifact, mirroring TAP-01/02), where the London-mulligan sim already runs. Cheap, no per-request sim cost on the deck-analysis path, no cross-pipeline bridge. (Phase 81.)
- **`manaValueNeeded` capture = Spellbook-grounded** — the win-con assembly band is grounded in the already-parsed-but-dropped `SpellbookCombo.ManaValueNeeded` (and `Popularity`); the parser is updated to capture them, which also satisfies the combo-ranking pitfall. (Phase 80.)

## Phases

- [x] **Phase 79: Interaction & Answers Audit** — bucketed, card-backed interaction counts + coverage-gap advisories in `/deck-analysis`, framed as an AI-rechecked first pass (EXECUTED + VERIFIED 2026-07-01; commits 3f063a06..cc061f26; ⚠ push/CI + live smoke owed)
- [x] **Phase 80: Win-Condition & Combo Map** — ranked combo / near-combo enumeration with redundancy + a coarse assembly band, grounded in the cached Spellbook data (completed 2026-07-02)
- [x] **Phase 81: Opening-Hand / Mulligan Evaluator** — keepable-hand probability + color/curve / process / on-curve / has-a-plan reads on `/manabase`, off the single existing sim pass (completed 2026-07-03)

## Phase Details

### Phase 79: Interaction & Answers Audit
**Goal**: A Commander/cEDH player running `/deck-analysis` sees their deck's interaction counted, bucketed, and gap-flagged as a heuristic first-pass the AI re-checks — pasteable in one round-trip, with zero behavior change when the flag is OFF.
**Depends on**: Nothing new (first Cycle 14 phase; builds on the shipped `DeckStatClassifier`/`DeckStatSummary`/`DeckStatAggregator` + analysis-packet groove). Establishes the repeatable "new block through 3 variants + new flag" recipe.
**Requirements**: INTERACT-01, INTERACT-02, INTERACT-03
**Success Criteria** (what must be TRUE):
  1. With `analysis.interaction-audit` ON, the `/deck-analysis` paste artifact AND on-page readout show interaction bucketed into targeted removal, board wipes, counterspells, protection/recursion, and stax/taxation — each count backed by the actual card list (no bare numbers), with stax/protection as coarse presence only (curated in-repo static list + golden tests, not exhaustive oracle-text heuristics).
  2. The audit emits short coverage-gap advisories ("0 counterspells", "no graveyard hate") explicitly framed as DeckFlow's automated first-pass read the AI must re-verify against the cards — prose says "approximately / verify," never "the deck has N"; a borderline/"review" confidence tier holds weak-signal matches.
  3. The interaction block renders in all three prompt variants (ChatGpt/Claude/Gemini) with NO shared helper, proven by a 3-platform parity test (ADR-0001); Gemini is not omitted.
  4. With the flag OFF, the page, all three paste artifacts, AND the zip round-trip are byte-identical to the pre-feature baseline (per-surface byte-identity test); the flag is seeded OFF in BOTH SQLite and Postgres seed SQL with a catalog description and is covered by the seed-consistency tests.
  5. Pure classification logic extends the shared `DeckStatClassifier` in `DeckFlow.Core` (not a forked `Contains` chain), with fixtures exercising pseudo-removal / modal-MDFC / self-target ("you control") cases; CI (not the WSL local run) is green and the changed-lines format gate / carve-outs are clean before close.
**Plans**: 3 plans
  - [x] 79-01-PLAN.md — Core: interaction classification + curated stax/protection catalog + InteractionAudit model/aggregator (INTERACT-01/02)
  - [x] 79-02-PLAN.md — Paste artifacts: flag + seed both dialects + hedged card-backed block through all 3 prompt variants + parity test (INTERACT-01/02/03)
  - [x] 79-03-PLAN.md — On-page readout + hardened round-trip + per-surface flag-OFF byte-identity test (INTERACT-01/03)
**UI hint**: yes

### Phase 80: Win-Condition & Combo Map
**Goal**: The player sees an enumerated, ranked win-condition / combo map — how the deck wins, with redundancy and a coarse assembly band — grounded in the already-fetched Commander Spellbook data, gracefully disclosing data-unavailable, and byte-identical when OFF.
**Depends on**: Phase 79 (reuses the block-through-3-variants recipe) + the already-wired, already-widened analysis-path combo fetch. Independent of Phase 79's interaction output.
**Requirements**: WINCON-01, WINCON-02, WINCON-03, WINCON-04
**Success Criteria** (what must be TRUE):
  1. With `analysis.wincon-map` ON, the `/deck-analysis` artifact + readout enumerates the deck's combos (`IncludedCombos`) plus one-card-away near-combos (`AlmostIncludedCombos`) — strictly separated, almost-combos labeled "one card away (not currently a win line)" — and states how many assembly paths exist.
  2. A coarse assembly-band read ("comes online early / mid / late" — bands, NEVER hard turn numbers) grounded in the combos' `manaValueNeeded`; the parser is updated to capture the already-parsed-but-dropped `manaValueNeeded` and `popularity`, which also rank the included combos (low MV-needed + high popularity first) so the truncated-at-20 list is no longer unranked noise. (Decision RESOLVED: Spellbook-grounded capture.)
  3. Commander Spellbook failure is disclosed as "combo data unavailable" (distinguished from "no win conditions"); non-combo closers (closing-power cards) are noted so a combo-less deck still gets a win-condition read.
  4. The combo map reuses the single already-wired, 30-min-cached `CommanderSpellbookResult` from the analysis path (no second `find-my-combos` fetch) and frames combos as candidate win lines the AI confirms for castability/board/color — never "the deck wins via X."
  5. The block renders in all three variants with no shared helper (3-platform parity test, ADR-0001, Gemini included); with the flag OFF the page + 3 artifacts + zip are byte-identical to baseline, seeded OFF in both dialects with a catalog description; CI green and format gate clean before close.
**Plans**: 3 plans
  - [x] 80-01-PLAN.md — Core: WinConMap model + WinConMapAggregator (rank/band combos, separate near-combos, closing cards, data-unavailable sentinel) + golden tests (WINCON-01/02/03)
  - [x] 80-02-PLAN.md — Paste artifacts: flag + seed both dialects + reuse the single combo fetch (gate widened) + hedged win-con block through all 3 prompt variants + parity test (WINCON-01/02/03/04)
  - [x] 80-03-PLAN.md — On-page readout + hardened WinConMapJson round-trip + 61-wincon-map.json zip + dual-layer flag-OFF byte-identity (Razor render + surface/zip contract) (WINCON-01/03/04)
**UI hint**: yes

### Phase 81: Opening-Hand / Mulligan Evaluator
**Goal**: On the `/manabase` tool, the player sees a keepable opening-hand probability plus color/curve, mulligan-process, on-curve-castability, and "has-a-plan" reads — all read off the single existing Monte-Carlo London-mulligan pass, never contradicting the manabase tool's own numbers, framed as a consistency signal not advice.
**Depends on**: Phase 79 (block-through-variants / flag recipe). Independent of Phase 80. Sequenced last so the (now-resolved) routing choice lands deliberately. **Planning risk:** may need to expose currently-private London-mulligan internals (`LondonMulligan`/`ColorKeepCap`) out of `CastabilitySimulator` — keep any newly-exposed seam inside Core.
**Requirements**: MULLIGAN-01, MULLIGAN-02, MULLIGAN-03, MULLIGAN-04, MULLIGAN-05, MULLIGAN-06
**Success Criteria** (what must be TRUE):
  1. With `analysis.mulligan-eval` ON, the `/manabase` tool (page + paste artifact, mirroring TAP-01/02) shows a keepable opening-hand probability as a discrete metric plus a color/curve read. (Decision RESOLVED: routing = 3a, `/manabase`.)
  2. The evaluator shows the London-mulligan PROCESS — representative openers with the keep / mull-to-6 / bottom decisions the sim already makes — plus a per-opener ON-CURVE CASTABILITY read (spells castable on-curve turn-by-turn using the hand's lands, expected draws, and ramp timing the sim already models) and a "has a plan" hand-quality flag ("workable line / no clear line"). This is opening-hand EVALUATION, NOT a keep/mulligan-decision or turn-by-turn play advisor (no prescriptive "keep this hand").
  3. All reads reuse the existing single Monte-Carlo pass (no second `Simulate`, no upstream re-fetch) and reuse the sim's existing `LondonMulligan` + `ColorKeepCap` as the single definition of "keepable" — surfacing a band, not a false-precision %, and never reporting a figure that contradicts the manabase tool's own keep/cast numbers.
  4. Heuristic reads are framed as a consistency signal feeding the AI, with the keep criterion stated narrowly next to the number ("keepable = 2-5 lands with an early play, on the London mulligan; not a strategic keep judgment") — never an authoritative verdict.
  5. Flag `analysis.mulligan-eval` is seeded OFF in both dialects with a catalog description; with the flag OFF the `/manabase` page AND its paste artifact (and zip where applicable) are byte-identical to baseline (per-surface test); any newly-exposed sim internals stay within Core; CI green and format gate clean before close.
**Plans**: 3 plans
  - [x] 81-01-PLAN.md — Core: LondonMulligan keep-depth exposure + pure-observation opening-hand instrumentation on the single existing sim pass + ManabaseMulliganEvaluation model/aggregator + golden tests (MULLIGAN-01/02/03/04/05)
  - [x] 81-02-PLAN.md — Paste artifact: flag analysis.mulligan-eval + seed both dialects + hedged opening-hand block through ManabaseReportTextBuilder (single artifact, TAP precedent) + service gate + null-byte-identity test (MULLIGAN-01/04/06)
  - [x] 81-03-PLAN.md — On-page /manabase lens card + ShowMulliganEval + per-surface flag-OFF byte-identity render test + desktop/mobile smoke (MULLIGAN-01/02/06)
**UI hint**: yes

## Phase Ordering Rationale

- **79 first** — strongest precedent (exact Phase-77 files, purely additive over the most-exercised groove), no external call, lowest risk. Locks the repeatable "new block param through 3 variants + new flag + per-surface byte-identity test" recipe on the safest surface.
- **80 second** — reuses Phase 79's block recipe and the already-wired/already-widened combo fetch; the only nuance is combo-null handling (precedent exists) plus the small Spellbook-grounded parser capture.
- **81 last** — the additive-sim half is a TAP-02 clone, but it lives on the *other* pipeline (`/manabase`, routing decision 3a) and may expose private sim internals; sequenced last so that cross-surface work is done deliberately. No feature consumes another's output, so all three are independent at the Core layer; all three are gated OFF and byte-identical, so order does not affect the OFF-state contract.

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 79. Interaction & Answers Audit | 3/3 | Executed + Verified | 2026-07-01 |
| 80. Win-Condition & Combo Map | 3/3 | Complete   | 2026-07-02 |
| 81. Opening-Hand / Mulligan Evaluator | 3/3 | Complete   | 2026-07-03 |

## Carry-forward backlog (not in Cycle 14)

- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred — deepens cedh-meta-gap, a separate lane)
- Operator owed: manual prod deploy of Cycle 13 (autodeploy OFF) + flip the four Cycle 13 flags + any owed Cycle 12 flag flips
</content>
