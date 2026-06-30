# Project Research Summary

**Project:** DeckFlow Cycle 13 — Deck Evaluation & Creator Output
**Domain:** Commander/cEDH deck evaluation + AI paste-artifact engine extension
**Researched:** 2026-06-27
**Confidence:** HIGH (stack verified against live codebase and live Scryfall API; bracket definitions verified against WotC official announcements through Feb 2026)

## Executive Summary

Cycle 13 adds four deck-evaluation and creator-output features to an existing ASP.NET 10 paste-artifact engine. Every feature builds entirely on in-solution components — zero new NuGet packages, zero new npm packages. The Bracket Classifier detects which of the five WotC tiers a deck falls in; the Balancer generates a "cuts to reach bracket N" paste artifact (the only tool in the space to do this). Multi-Axis Deck Score (Power/Speed/Control/Consistency 0-5) replaces single-number scoring in the analysis packet. Auto-Refreshing Primer detects deck staleness against a stored canonical hash and flags the primer for regeneration. Tap Analyzer surfaces untapped-source frequency and turn-1 availability already computed inside the CastabilitySimulator but never previously reported.

The recommended approach is Core-first and additive. All four scoring and classification engines are pure functions of already-modeled deck facts and belong in `DeckFlow.Core`. The Web layer only hydrates Scryfall facts (reusing the existing `IScryfallCardResolver`/`ScryfallCardFactMapper` path), renders the three decoupled prompt variants per ADR-0001, and owns feature flags. The Tap Analyzer is the simplest and most independent — accumulate two counters inside the existing 20,000-trial Monte-Carlo loop, add additive fields to `ManabaseReport`, surface in the paste text. It has no dependency on any other Cycle 13 feature and should ship first.

The dominant risk across all four features is the correctness and honesty of the paste artifacts. A wrong bracket classification, an unjustifiable axis weight, a falsely-stale primer flag, or a tap metric that contradicts the sim's own cast-rate all erode trust in the artifact the user pastes into ChatGPT — the core product value. Prevention is uniform: externalize living data (Game Changers list must be a versioned data file, not a `.cs` constant), document every signal-to-score formula with `// Why:`, source each metric from a single place, and instrument every artifact with enough disclosed inputs that the AI can detect and correct DeckFlow's gaps.

## Key Findings

### Recommended Stack

All four features are pure builds on the existing stack. No new technology decisions are required. The in-solution components that enable each feature are: RestSharp + Polly `scryfall` pipeline + IMemoryCache (Bracket: reuse banlist pattern); `System.Security.Cryptography.SHA256` BCL already used in `PacketSessionCache` (Primer fingerprint); `RelationalDatabaseConnection` dual-dialect (one new `primer_snapshots` table); `DeckFlow.Core/Manabase` (Tap Analyzer: additive fields only). The existing `DeckStatClassifier`, `DeckStatAggregator`, `CardFact`, and `DeckStatSummary` types are the primary inputs to the bracket and scoring engines.

**Core technologies (all in-solution — no changes):**
- `DeckFlow.Core/Analysis/` — pure-CPU bracket classification + multi-axis scoring; pure-CPU locale for all new engines
- RestSharp `scryfall-rest` + Polly `scryfall` + IMemoryCache — Game Changers seed verification; 24h cache; reuse `ICommanderBanListService` pattern
- `System.Security.Cryptography.SHA256` (BCL) — deck fingerprint for primer staleness; already used in `PacketSessionCache.ComputeKey`
- `RelationalDatabaseConnection` (`IRelationalDialect`) — primer snapshot store; same pattern as `FeedbackStore`/`CategoryKnowledgeStore`
- `CastabilitySimulator` (existing, Core) — Tap Analyzer: add counters inside existing 20k-trial loop; zero new simulation

### Expected Features

**Must have (table stakes — launch with):**
- Bracket Classifier: Game Changers data table (versioned + dated) + hard-floor detection (mass land denial, extra-turn chains, 2-card combos via Spellbook) + bracket number with disclosed reasons
- Multi-Axis Score Speed + Consistency axes: ~80% reuse of existing manabase + ramp/draw + combo signals; ship in the analysis paste packet immediately
- Tap Analyzer surface: untapped land count/fraction + turn-1 untapped availability in report and paste text; the manabase engine already computes this, it just never surfaced it

**Should have (uncontested differentiators — complete in Cycle 13):**
- Bracket Balancer paste artifact: "cuts to hit target bracket N" with per-cut justification anchored to objective gates; no incumbent ships this
- Multi-Axis Score Control + Power axes: Control needs a new interaction classifier (board-wipe/removal/counter oracle text or category-knowledge); Power axis delegates card-strength judgment to the AI round-trip
- Auto-Refreshing Primer (flag-stale tier): store canonical deck fingerprint alongside primer artifact; show "Deck changed — regenerate?" banner naming the delta; reuses `DiffEngine` + `PacketSessionCache.ComputeKey`

**Defer (next cycle):**
- Auto-Refreshing Primer section-scoped regenerate: section to card dependency map + per-section prompt assembly; HIGH complexity; validate the flag-stale tier first
- Bracket Balancer fair-replacement automation: local replacement-suggestion engine (vs delegating fair-swap judgment to the AI round-trip); only if AI-delegated version proves insufficient

### Architecture Approach

The existing DeckFlow Core/Web split governs all four features. Pure scoring/classification logic goes in `DeckFlow.Core/Analysis/` alongside the existing `DeckStatClassifier`/`DeckStatAggregator`. Web services hydrate Scryfall facts (reusing `IScryfallCardResolver`), call Core, and render three decoupled prompt variant families per ADR-0001. Tap Analyzer is the exception: it stays entirely in `DeckFlow.Core/Manabase/` because it is literally an additional readout of `CastabilitySimulator` state. Primer staleness splits: thin `PrimerStalenessEvaluator` in Core (wraps `DiffEngine.Compare`) + `PrimerSnapshotStore` in Web (new dialect-pluggable persistence). A new `BracketController : DeckToolControllerBase` follows the established controller pattern with `[FeatureFlagGate]`.

**Major new components:**
1. `GameChangerCatalog` (Core/Analysis) — static embedded versioned card-name set with effective-date; loaded from a seed JSON file at startup, NOT a hardcoded `.cs` literal
2. `BracketClassifier` (Core/Analysis) — pure: `CardFact[]` + `DeckStatSummary` + `GameChangerCatalog` -> `BracketResult { Tier, Reasons[], GameChangerHits[] }`
3. `BracketBalancer` (Core/Analysis) — pure: deck + target tier -> ranked `IReadOnlyList<BracketCut>` anchored to objective gate violations
4. `DeckScorer` (Core/Analysis) — pure: `DeckStatSummary` + combo count + tutor count -> `DeckScore(Power, Speed, Control, Consistency)` clamped 0-5
5. `BracketAnalysisService` (Web/Services/Bracket/) — hydrate `CardFact[]` via existing Scryfall path; call Core; render 3 prompt variants; own flag `tool.bracket.enabled`
6. `PrimerStalenessEvaluator` (Core/Diffing) — thin wrapper over existing `DiffEngine.Compare`; returns stale-bool + change-count
7. `PrimerSnapshotStore` (Web/Services/Persistence/) — dialect-pluggable (`RelationalDatabaseConnection`); persists `{ deckKey, fingerprint, decklistText, generatedUtc }`
8. Tap accumulation (inline, Core/Manabase/CastabilitySimulator) — two `out int` counters added to existing `SimulateGame`; additive fields on `CardCastability`/`ManabaseReport`

### Critical Pitfalls

1. **Game Changers staleness treadmill** — A hardcoded `string[]` or `HashSet<string>` in `.cs` goes silently stale on every WotC update, producing wrong bracket classifications in the most trusted artifact. Prevention: externalize as a versioned seed JSON file (same pattern as `ContentKbSeedLoader`) with an `effective_date` field. Stamp every bracket classification artifact with the list date. The Scryfall `is:gamechanger` API can validate/update the seed out-of-band; the classifier itself reads only the local cached data.

2. **Tutor restrictions were REMOVED in October 2025** — The Oct 21 2025 WotC update explicitly dropped all tutor-count bracket gates. Classifier logic that hard-gates on tutor count is wrong. Tutors are a soft Consistency axis signal only. Any research or document predating October 2025 that gates brackets on tutors is stale.

3. **Mis-detecting gating mechanics (mass land denial, extra turns, 2-card combos)** — The existing `DeckStatClassifier` substring heuristics (`"extra turn"`, `"destroy all"`) are too coarse for bracket gating where a single Armageddon flips a bracket. Use exact name matching against a small curated named-card list for mass land denial and extra-turn chains. For 2-card combos, use the existing Commander Spellbook integration but treat a `null` return (API down) as "combo detection unavailable, disclosed in artifact," never as "zero combos."

4. **Primer auto-rebuild default triggers regeneration thrashing** — `DeckPrimerPacketService` is ~750 LOC and pulls category knowledge, Commander Spellbook combos, and EdhTop16 matchups per build. Auto-rebuilding on any deck-text difference (including whitespace/reorder changes) hammers upstreams and chews the 512MB RAM cap. Prevention: stale-FLAG is the default; rebuild on explicit user action only. Staleness key must be a canonical multiset hash (name+quantity, sorted) — the existing `TryComputeCacheKeyAsync` / `PacketSessionCache.ComputeKey` machinery is the correct primitive.

5. **3-variant triplication drift (ADR-0001)** — A developer adds a bracket block to the ChatGPT variant, forgets Claude and Gemini, ships a broken artifact for 2 of 3 platforms. Prevention: treat "new artifact section" as a mandatory 3-variant checklist item in every phase's success criteria. Add a parity test asserting the new section appears in all three variants (the codebase has `PrimerPromptVariantTests` as the model). Never extract a shared base class — that violates ADR-0001 and will be reverted.

## Implications for Roadmap

Based on combined research, the recommended phase structure is four phases ordered by dependency and risk. The Core-first build sequence from ARCHITECTURE.md is the canonical reference: pure-Core components land first within each feature, Web wiring follows.

### Phase 1: Tap Analyzer Surface

**Rationale:** Zero dependency on other Cycle 13 features. The manabase engine already models tapped/untapped per trial — this is a readout, not a new model. The additive-field discipline (MQ-02..05 precedent) makes it the lowest-risk change in the cycle and delivers an immediate visible win on the existing manabase page.
**Delivers:** `UntappedLandCount`, `TappedLandCount`, `UntappedLandFraction`, `UntappedSourcesByColor` on `ManabaseReport`; turn-1 untapped availability metric; new "Land Quality" section in the manabase report text (paste artifact) and in `Manabase.cshtml` view; flag `analysis.manabase.tap-analyzer`.
**Addresses:** Tap Analyzer surface feature (full scope); Salubrious Snail parity on the untapped-frequency metric.
**Avoids:** Pitfall 8 (misreading sim tapped state) by sourcing ALL dynamic metrics from inside the existing sim loop. Pitfall 11 (Monte-Carlo perf regression) by using only pre-allocated primitive counters in the hot loop — no LINQ, no allocation, no second pass.
**Research flag:** SKIP deeper research — the sim internals are fully verified; the additive-field pattern is established.

### Phase 2: Bracket Classifier + Balancer

**Rationale:** The classifier is the dependency gate for the balancer. Both share the same Core input model (`CardFact[]` + `DeckStatSummary`) and the same Web hydration path (reuse `IScryfallCardResolver`). Shipping classifier and balancer together avoids a mid-cycle integration seam. This is the headline differentiator of Cycle 13 — the balancer paste artifact has no incumbent. It is also the phase with the most domain-accuracy risk (Game Changers list versioning, gating mechanic detection) and must address those risks up front.
**Delivers:** `GameChangerCatalog` (versioned seed JSON + startup load + effective-date stamp); `BracketClassifier` + `BracketBalancer` in `DeckFlow.Core/Analysis/`; `BracketAnalysisService` in Web; `BracketController` (`DeckToolControllerBase` subclass); three bracket prompt variants (`ChatGpt/Claude/Gemini`) per ADR-0001; parity test; `/bracket` view; `tool.bracket.enabled` flag (seeded OFF). The balancer artifact frames cuts as "objective gate violations for AI evaluation," not as an authoritative cut list.
**Addresses:** Bracket Classifier (full), Bracket Balancer (full P1 scope, AI-delegated fair-swap suggestions).
**Avoids:** Pitfall 1 (staleness treadmill) via versioned seed file with effective-date stamp. Pitfall 2 (mis-detecting gating mechanics) via exact name matching for Game Changers + curated named-card list for mass-land-denial/extra-turns + Spellbook `null` disclosed. Pitfall 3 (over-claiming balancer authority) by framing the artifact as "here are the gate violations" not "cut these cards." CRITICAL: do NOT gate brackets on tutor count (Oct 2025 WotC change).
**Research flag:** NEEDS care on the Game Changers seed file format and the curated mass-land-denial + extra-turn named-card lists — verify these are complete and auditable before execution. The bracket rule thresholds (GC count per bracket, combo-timing definitions for B3) should be documented in the seed data with source citations.

### Phase 3: Multi-Axis Deck Score

**Rationale:** `DeckScorer` depends on `GameChangerCatalog` (Game Changer count feeds the Power axis) from Phase 2 and on `ManabaseReport` tap fields from Phase 1. Speed and Consistency axes are ~80% reuse of existing signals. Folding the score into the existing analysis packet variants (hand-edit all 3, ADR-0001) enriches every existing deck analysis without a new tool surface. Control and Power axes can follow if schedule allows; they are P2, not blockers.
**Delivers:** `DeckScorer` + `DeckScore` record in `DeckFlow.Core/Analysis/`; score block injected into all 3 existing `Analysis` prompt variants (Speed + Consistency axes in the MVP pass; Control + Power in a follow-on pass if schedule allows); parity test asserting all 3 variants contain the score block; optional standalone score display on the analysis page.
**Addresses:** Multi-Axis Deck Score (Speed + Consistency P1; Control + Power P2).
**Avoids:** Pitfall 4 (arbitrary weights) by computing every axis from documented signals with `// Why:` inline rationale and a bracket cross-check test (cEDH golden deck must score higher Power/Speed than a battlecruiser deck). Pitfall 5 (missing card-level data) by sharing the existing per-request enrichment cache and disclosing partial coverage in the artifact. Pitfall 13 (cross-tool number disagreement) by sharing `DeckStatSummary` tallies as the single signal source for bracket, score, and manabase budget.
**Research flag:** SKIP for Speed + Consistency axes (fully verified inputs). NEEDS one planning spike to decide the Control interaction classifier approach (oracle-text heuristics vs category-knowledge labels vs hybrid) before authoring that axis.

### Phase 4: Auto-Refreshing Primer (Flag-Stale Tier)

**Rationale:** Depends on no other Cycle 13 feature. The core primitive is already in `DeckPrimerPacketService` (`TryComputeCacheKeyAsync`). The `DiffEngine` (Core) is already the diff primitive. The new work is thin: a `PrimerStalenessEvaluator` wrapper, a `PrimerSnapshotStore` (dialect-pluggable), a staleness check in the primer service, and a "Regenerate?" banner in the primer view. This is DeckFlow's clearest creator-lane differentiator — no incumbent auto-flags primer staleness. Ship the flag-stale tier here; defer section-scoped regenerate to the next cycle.
**Delivers:** `PrimerStalenessEvaluator` (Core, wraps `DiffEngine.Compare`); `PrimerSnapshotStore` (Web, dialect-pluggable; stores `{ deckKey, fingerprint, decklistText, generatedUtc }`); `DeckPrimerPacketService` modification to store fingerprint on build + check on revisit; stale banner in `DeckPrimer.cshtml` naming the changed-card count; golden tests asserting the equivalence relation (reorder/printing = not stale; card swap/quantity change = stale).
**Addresses:** Auto-Refreshing Primer (flag-stale tier, full P2 scope).
**Avoids:** Pitfall 6 (regeneration thrashing) by defaulting to stale-FLAG + explicit user-initiated regenerate — never auto-rebuild. Pitfall 7 (false-positive/negative) by using the canonical multiset hash (name+quantity, sorted) as the staleness key, not raw textarea text, with golden tests asserting both directions.
**Research flag:** SKIP — established patterns throughout (DiffEngine, PacketSessionCache, RelationalDatabaseConnection, FeedbackStore as model).

### Phase Ordering Rationale

- Tap Analyzer first: independent, lowest risk, validates the additive-field discipline before heavier bracket work.
- Bracket before Score: `DeckScorer` consumes `GameChangerCatalog` (GC count feeds the Power axis); bracket service also completes the Scryfall hydration path the score needs.
- Score before Primer: Primer is fully independent but Score benefits from the full bracket signal. Either can swap without breaking the other.
- Primer last: most independent feature; its value is clearest once the bracket and score artifacts have demonstrated the paste-artifact thesis.
- Core-first discipline within each phase (models -> pure logic -> Web wiring -> view) enables unit testing of pure functions before any Web integration.

### Research Flags

Phases likely needing planning-time research attention:
- **Phase 2 (Bracket):** The Game Changers seed file format + startup loading mechanism needs a design decision before phase planning. The curated mass-land-denial and extra-turn card lists need to be authored and cited. The Spellbook-null disclosure copy needs to be agreed before prompt variant authoring.

Phases with standard patterns (no additional research needed):
- **Phase 1 (Tap Analyzer):** Additive-field pattern fully established by MQ-02..05; CastabilitySimulator internals verified directly.
- **Phase 3 (Multi-Axis Score) Speed+Consistency:** All input signals verified against live codebase.
- **Phase 4 (Auto-Refreshing Primer):** DiffEngine, PacketSessionCache, RelationalDatabaseConnection all verified; FeedbackStore is the persistence pattern model.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified directly against live codebase; zero new dependencies confirmed; Scryfall `is:gamechanger` API verified via live curl returning 53 cards |
| Features | HIGH | Bracket definitions verified against WotC official announcements through Feb 2026; Oct 2025 tutor-restriction removal confirmed; competitor landscape verified against live tools |
| Architecture | HIGH | All integration points traced to specific source files; no speculative claims |
| Pitfalls | HIGH (codebase); MEDIUM (GC domain) | Codebase-integration pitfalls traced to specific files; GC list update cadence is externally controlled by WotC |

**Overall confidence:** HIGH

### Gaps to Address

- **Game Changers list format decision:** Three approaches proposed across research files (static Core HashSet vs versioned seed JSON vs live Scryfall call). Recommended resolution: versioned seed JSON file loaded at startup into IMemoryCache (ContentKbSeedLoader pattern) with effective-date field. Lock this before Phase 2 planning.
- **Multi-axis Control axis classifier:** Exact approach (oracle-text keyword patterns vs category-knowledge labels vs hybrid) is unresolved. Decide in Phase 3 planning.
- **Bracket B3 "early-game combo" timing threshold:** WotC defines B3 as "no combos that reliably win before ~turn 6-7" — a prose definition, not a crisp turn number. The Spellbook data does not include a "fastest-win-turn" field. Resolution: disclose the approximation in the artifact and let the AI flag ambiguous cases. Confirm this is acceptable in Phase 2 planning.
- **Multi-axis score calibration golden decks:** No ground-truth dataset exists to validate axis weights. Define a set of golden decks (a known cEDH list, a known battlecruiser list) as the sanity anchor in Phase 3 planning.

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — verified `CardKind.{Untapped,Tapped}Land`, per-land `OnlineTurn` semantics, 20k-trial loop structure, existing `out int` param pattern
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — verified `ManaSource.EntersUntapped`, `CardCastability` additive-field discipline, `AvgOnCurvePercent` computed-getter rollup pattern
- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` + `DeckStatAggregator.cs` — confirmed reusable role predicates and `DeckStatSummary` as bracket/score input
- `DeckFlow.Core/Diffing/DiffEngine.cs` — confirmed `DeckDiff` add/remove/mismatch output for primer staleness
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` — confirmed `TryComputeCacheKeyAsync` + `PacketSessionCache.ComputeKey` canonical-deck fingerprint = the staleness signal
- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` — confirmed current brackets are hard-coded option records (staleness anti-pattern to NOT replicate for Game Changers)
- `docs/decisions/0001-prompt-variants-decoupled.md` — confirmed 3-variant decoupled pattern; no shared text; hand-edit all 3
- Scryfall `is:gamechanger` API (live curl) — verified `total_cards: 53`, confirmed JSON structure, June 2026
- WotC Commander Brackets Beta Update Oct 21 2025 — tutor restrictions REMOVED; 10 GCs removed; 48-card post-update list
- WotC Commander Brackets Beta Update Feb 9 2026 — +Farewell, +Biorhythm; total 53 cards confirmed

### Secondary (MEDIUM confidence)
- EDHRank (mtgmana.rocks) — 4-axis Power/Speed/Control/Consistency definitions and decimal example
- Spellweave bracket guide — "53 cards as of Feb 2026" cross-check; combo-timing table for B3
- ScrollVault bracket calculator — classifier pipeline; confirms no cut suggestions exist (DeckFlow balancer is uncontested)
- Salubrious Snail manabase tool — Tap Analyzer untapped-frequency + opening-turn sim; cast-rate/avg-delay benchmarks
- Rate My Decks — confirmed it does NOT split into 4 axes (confirms DeckFlow multi-axis decomposition is a differentiator)
- Moxfield help + BlazeHero primer guide — confirmed primer section structure is manual, no deck-link staleness detection
- `scratchpad-research/commander-feature-wants-report.md` — feature-gap basis for all four Cycle 13 features

### Tertiary (LOW confidence)
- MTGSalvation primer status thread — "manually updating to match changes" decay pattern confirms the pain point

---
*Research completed: 2026-06-27*
*Ready for roadmap: yes*
