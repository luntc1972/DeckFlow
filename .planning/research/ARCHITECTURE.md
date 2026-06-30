# Architecture Research

**Domain:** Deck evaluation + creator output features integrated into the existing DeckFlow .NET 10 paste-artifact engine
**Researched:** 2026-06-27
**Confidence:** HIGH — grounded in direct reads of `CastabilitySimulator.cs`, `ManabaseModels.cs`, `ManabaseAnalyzer.cs`, `DeckStatClassifier.cs`/`DeckStatAggregator.cs`, `DiffEngine.cs`, `DeckPrimerPacketService.cs`, the Analysis/Primer prompt-variant trees, `FeatureFlagCatalog.cs`, and the controllers. No speculative claims.

---

## The Core-vs-Web Split Decision (per feature)

DeckFlow already enforces a hard boundary: **`DeckFlow.Core` is pure CPU domain logic (no `HttpClient`, no ASP.NET); `DeckFlow.Web` owns HTTP egress, Scryfall hydration, paste-artifact rendering, controllers, views, and feature flags.** Every new feature splits along this exact seam, mirroring how `ManabaseAnalyzer` (Core) is driven by `ManabaseAnalysisService` (Web).

| Feature | DeckFlow.Core (pure logic) | DeckFlow.Web (hydration + render + flags) |
|---------|----------------------------|-------------------------------------------|
| **1. Bracket Classifier + Balancer** | `BracketClassifier` (deck facts → bracket 1-5 + reasons), `BracketBalancer` (cuts to hit target bracket). Consumes `CardFact` + `DeckStatSummary`. | `BracketAnalysisService` (Scryfall hydration → `CardFact[]`), the 3 paste-variant builders, controller/view, flag `tool.bracket.enabled` (or `analysis.bracket.*`). |
| **2. Multi-Axis Deck Score** | `DeckScorer` (Power/Speed/Control/Consistency 0-5) over `DeckStatSummary` + `ManabaseReport` + castability. | Score rendered **into the existing analysis packet** + optional standalone surface; no new HTTP. |
| **3. Auto-Refreshing Primer** | Reuse `DiffEngine` (already Core) for staleness; a `PrimerStalenessEvaluator` wrapper. | Persist primer-snapshot fingerprint; `DeckPrimerPacketService` gains a "stale?" check + regenerate trigger; staleness store. |
| **4. Tap Analyzer surface** | **Minimal accumulator inside `CastabilitySimulator` + new fields on `CardCastability`/`ManabaseReport`.** | Surface the new metrics in `ManabaseReportTextBuilder` (paste) + `Manabase.cshtml` (view). |

**Opinionated rule:** all four scoring/classification engines are pure functions of already-modeled deck facts. They belong in Core. The Web layer only hydrates facts from Scryfall (which it already does for manabase + analysis), renders the 3 decoupled paste variants, and owns the flags.

---

## System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                    DeckFlow.Web  (HTTP + render + flags)              │
│                                                                       │
│  Controllers/                                                         │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌─────────────┐  │
│  │ManabaseCtrl  │ │DeckPacketCtrl│ │DeckPrimerCtrl│ │BracketCtrl   │  │
│  │ (Tap surface)│ │(Multi-Axis)  │ │(Auto-refresh)│ │(NEW)         │  │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬──────┘  │
│         │                │                │                │         │
│  Services/ (hydrate Scryfall → CardFact, render 3 variants, flags)   │
│  ┌──────────────┐ ┌──────────────────┐ ┌──────────────────────────┐  │
│  │ManabaseAnaly-│ │DeckAnalysisPacket│ │BracketAnalysisService NEW│  │
│  │sisService    │ │Service           │ │ScoreInjection (into pkt) │  │
│  └──────┬───────┘ └────────┬─────────┘ └──────────┬───────────────┘  │
│         │                  │                      │                  │
│  PromptBuilders/{Analysis,Primer,Bracket NEW}/{ChatGpt,Claude,Gemini}│
│         │   (ADR-0001: 3 fully-decoupled variants per family)        │
└─────────┼──────────────────┼──────────────────────┼──────────────────┘
          │ ProjectReference  │ (pure facts in/out)  │
┌─────────┼──────────────────┼──────────────────────┼──────────────────┐
│                    DeckFlow.Core  (pure CPU domain)                  │
│                                                                      │
│  Manabase/                          Analysis/                        │
│  ┌────────────────────────────┐    ┌──────────────────────────────┐  │
│  │CastabilitySimulator        │    │DeckStatClassifier            │  │
│  │ + TapAccumulator (MINIMAL) │    │DeckStatAggregator            │  │
│  │ManabaseAnalyzer            │    │BracketClassifier      (NEW)  │  │
│  │ManabaseReport              │    │BracketBalancer        (NEW)  │  │
│  │ +UntappedFreq/Turn1 (NEW)  │    │DeckScorer (multi-axis)(NEW)  │  │
│  │CardFact / ManabaseModels   │    │GameChangerCatalog     (NEW)  │  │
│  └────────────────────────────┘    └──────────────────────────────┘  │
│  Diffing/DiffEngine  ← REUSED by Auto-Refreshing Primer staleness    │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

| Component | Project | Status | Responsibility |
|-----------|---------|--------|----------------|
| `BracketClassifier` | Core/Analysis | NEW | Pure: `CardFact[]` + `DeckStatSummary` → bracket 1-5 + per-rule reasons. Game-changer count, tutor density, combo presence, fast-mana, mass land denial drive the WotC 5-tier rules. |
| `BracketBalancer` | Core/Analysis | NEW | Pure: deck + target bracket → ranked list of cuts (and the rule each cut relieves). The uncontested differentiator per research. |
| `GameChangerCatalog` | Core/Analysis | NEW | Static embedded list of WotC "Game Changers" card names (the official bracket gate). Lives in Core as a `static readonly HashSet<string>` resource — no DB, no HTTP. |
| `DeckScorer` | Core/Analysis | NEW | Pure: `DeckStatSummary` + `ManabaseReport` + castability rows → `DeckScore(Power, Speed, Control, Consistency)` each 0-5. |
| `TapAccumulator` (inline) | Core/Manabase | MODIFIED | Accumulate untapped-source frequency + turn-1 untapped availability **inside the existing Monte-Carlo loop** in `CastabilitySimulator.Simulate`. No new sim. |
| `ManabaseReport` / `CardCastability` | Core/Manabase | MODIFIED | Add `UntappedFrequency`, `Turn1UntappedRate` (and per-color variants) fields — additive, default-null/0 so serialization + existing tests are unaffected (same pattern as `AverageDelay`, `LandTarget`). |
| `PrimerStalenessEvaluator` | Core | NEW (thin) | Pure: prior decklist fingerprint vs current → stale? Wraps `DiffEngine.Compare` (already Core) and counts changed cards. |
| `BracketAnalysisService` | Web/Services | NEW | Hydrates Scryfall → `CardFact[]` (reuse `IScryfallCardResolver`), runs Core `BracketClassifier`/`BracketBalancer`, renders 3 variants. Mirrors `ManabaseAnalysisService`. |
| `BracketController` | Web/Controllers | NEW | `DeckToolControllerBase` subclass; `[FeatureFlagGate("tool.bracket.enabled")]`; GET form + POST analyze. |
| Bracket prompt variants | Web/PromptBuilders/Bracket | NEW (×3) | `ChatGpt/Claude/Gemini` per ADR-0001; each owns its full prompt text. |
| `ManabaseReportTextBuilder` | Core/Manabase | MODIFIED | Append Tap-Analyzer lines to the paste-ready report. |
| `DeckScore` injection | Web/Services + PromptBuilders/Analysis | MODIFIED | Multi-axis score block added to all 3 analysis variants (hand-edited, ADR-0001). |
| `DeckPrimerPacketService` | Web/Services | MODIFIED | Compute + persist primer fingerprint; expose `IsStale`/regenerate trigger. |

---

## Recommended Project Structure

```
DeckFlow.Core/
  Analysis/
    DeckStatClassifier.cs          (EXISTING — reused for role tallies)
    DeckStatAggregator.cs          (EXISTING — DeckStatSummary supplier)
    BracketClassifier.cs           (NEW)
    BracketBalancer.cs             (NEW)
    BracketModels.cs               (NEW: BracketResult, BracketCut, BracketTier)
    GameChangerCatalog.cs          (NEW: static WotC game-changer name set)
    DeckScorer.cs                  (NEW)
    DeckScoreModels.cs             (NEW: DeckScore(Power,Speed,Control,Consistency))
  Manabase/
    CastabilitySimulator.cs        (MODIFIED: tap/turn-1 accumulation)
    ManabaseModels.cs              (MODIFIED: additive tap fields on report/row)
    ManabaseReportTextBuilder.cs   (MODIFIED: tap lines in paste text)
  Diffing/
    DiffEngine.cs                  (EXISTING — reused for primer staleness)
    PrimerStalenessEvaluator.cs    (NEW: thin DiffEngine wrapper)

DeckFlow.Web/
  Controllers/
    BracketController.cs           (NEW)
    ManabaseController.cs          (MODIFIED: pass tap surface to view)
    DeckPrimerController.cs        (MODIFIED: stale check / regenerate)
  Services/
    Bracket/BracketAnalysisService.cs   (NEW)
    DeckScoreService.cs            (NEW — or fold into DeckAnalysisPacketService)
    DeckPrimerPacketService.cs     (MODIFIED: fingerprint + staleness)
    Persistence/PrimerSnapshotStore.cs  (NEW: dialect-pluggable fingerprint store)
    FeatureFlags/FeatureFlagCatalog.cs  (MODIFIED: new flag descriptions)
  Services/PromptBuilders/
    Bracket/{IBracketPromptVariant, ChatGpt, Claude, Gemini, Registry}.cs (NEW ×5)
    Analysis/{ChatGpt,Claude,Gemini}AnalysisPromptVariant.cs (MODIFIED: score block)
    Primer/{...}                   (MODIFIED only if stale-banner copy in prompt)
  Views/Deck/
    Bracket.cshtml                 (NEW)
    Manabase.cshtml                (MODIFIED: tap metrics)
    DeckPrimer.cshtml              (MODIFIED: stale banner)
```

### Structure Rationale

- **`Core/Analysis/`** already holds the pure deck-role classifiers (`DeckStatClassifier`, `DeckStatAggregator`). Bracket + multi-axis scorers are the same kind of pure-CPU classifier over the same inputs (typeline/oracle/manacost/quantity), so they belong beside them — not in `Manabase/` (that namespace is mana-specific).
- **Tap Analyzer stays in `Manabase/`** because it is literally an extra read-out of the existing `CastabilitySimulator` state — colocating keeps the invariant-heavy sim file the single source of truth.
- **Bracket gets its own `PromptBuilders/Bracket/` family** mirroring `Analysis/`, `Primer/`, `Comparison/`, `MetaGap/`, `SetUpgrade/`, `FollowUp/` — the established per-workflow variant-registry pattern.

---

## Feature 1 — Bracket Classifier + Balancer

### What it consumes (real models)
- `CardFact` (Core/Manabase): name, quantity, manaCost, manaValue, typeLine, oracleText, producedMana, layout, `HasLandFace`, `IsCommander`, `Power`. **This is the richest already-modeled per-card fact record and the Web layer already hydrates it from Scryfall for manabase.** Bracket classification reuses it directly.
- `DeckStatSummary` (Core/Analysis): lands, creatures, avgMV, curve buckets, ramp/draw/interaction/wipes/recursion/closingPower counts — already computed by `DeckStatAggregator`.
- Role predicates in `DeckStatClassifier` (`IsRampCard`, `IsDrawCard`, `IsInteractionCard`, `IsClosingPowerCard`, etc.) — reusable as-is for "tutor density", "fast mana", "combo-ish" heuristics.

### Where "Game Changers" data lives
**Decision: a static embedded `GameChangerCatalog` in `DeckFlow.Core/Analysis/`** — a `static readonly HashSet<string>` of the official WotC Game Changers card names (normalized via the existing `CardNormalizer`). Rationale:
- It is a small (~40-card), slowly-changing official list — the same shape as the embedded bracket presets already in `CommanderBracketCatalog` and the static role-keyword lists in `DeckStatClassifier`. No HTTP, no DB row, no Scryfall round-trip needed (the deck's card names are already known after hydration).
- Keeping it in Core makes `BracketClassifier` a pure, fully-unit-testable function. The bracket rules (game-changer count thresholds, mass-land-denial, extra-turn chaining, two-card infinite combos) all reduce to set-membership + the `DeckStatClassifier` predicates.
- **Pitfall flag for roadmap:** the WotC list updates periodically. Ship it as a Core constant now; if update cadence becomes a burden, a later phase can move it behind `ICategoryKnowledgeStore` — but do NOT over-engineer that in this cycle.

### Core vs Web
- **Core:** `BracketClassifier.Classify(IReadOnlyList<CardFact>, DeckStatSummary) → BracketResult { Tier, Reasons[], GameChangerHits[] }`. `BracketBalancer.PlanCuts(deck, targetTier) → IReadOnlyList<BracketCut>`.
- **Web:** `BracketAnalysisService` hydrates `CardFact[]` (reuse `IScryfallCardResolver`/`ScryfallCardFactMapper` — already the manabase hydration path), calls Core, then renders the 3 paste variants. New `BracketController : DeckToolControllerBase` + `[FeatureFlagGate]`. New flag `tool.bracket.enabled` registered in the tool registry + `FeatureFlagCatalog`.

---

## Feature 2 — Multi-Axis Deck Score

### What it consumes
- `DeckStatSummary` (curve, ramp, draw, interaction, wipes, closingPower) → **Power** + **Control**.
- `ManabaseReport.AvgOnCurvePercent` + per-card `CardCastability` + curve front-loading → **Speed**.
- Combo density (`ICommanderSpellbookService` result, already wired into analysis/primer) + tutor count + `ManabaseReport` health → **Consistency**.

### Core vs Web
- **Core:** `DeckScorer.Score(DeckStatSummary, ManabaseReport?, comboCount, tutorCount) → DeckScore(Power, Speed, Control, Consistency)` each clamped 0-5. Pure; deterministic; unit-testable with no Monte-Carlo.
- **Web:** the score is **rolled into the existing analysis paste packet** (the project goal says "replacing single-number scoring"). No new HTTP. The block is injected into all 3 `Analysis` prompt variants by hand per ADR-0001. Optionally surfaced on the page too.

**Dependency note:** Speed/Consistency want the `ManabaseReport`. The analysis packet path does not currently run the manabase sim. Two options for the roadmap: (a) compute a lightweight curve/castability proxy from `DeckStatSummary` alone (cheaper, no sim), or (b) invoke the manabase analyzer inside the analysis packet (heavier, 20k-trial sim per spell). **Recommend (a) for the first ship** — Power/Speed/Control/Consistency from `DeckStatSummary` + combo/tutor counts keeps it fast and on the 512MB tier; deepen with sim later if axes feel coarse.

---

## Feature 3 — Auto-Refreshing Primer

### Where the generator lives today
`DeckFlow.Web/Services/DeckPrimerPacketService.cs` (`IDeckPrimerPacketService.BuildAsync`). It loads the deck via `IDeckEntryLoader`, grounds combos (`ICommanderSpellbookService`), archetypes (`IEdhTop16Client`), and category distribution (`ICategoryKnowledgeStore`), then renders 3 variants via `PrimerPromptVariantRegistry`. It **already computes a content fingerprint**: `TryComputeCacheKeyAsync` builds `PrimerCacheInputs` (commander, `BuildCanonicalDeckSourceText(entries)`, target bracket, style, section ids, gemini-enabled) and hashes it with `PacketSessionCache.ComputeKey`. **This canonical-deck-source hash is exactly the staleness signal.**

### How "deck changed / stale" is detected (grounded in existing infra)
Two complementary, already-present mechanisms:
1. **Canonical fingerprint diff (cheapest):** persist the `PacketSessionCache.ComputeKey(PrimerCacheInputs)` value alongside the generated primer. On next view, recompute from the live deck URL; if the key differs, the primer is stale. This reuses the EXISTING cache-key machinery — no new diff logic.
2. **Card-level diff (richer "what changed"):** `DeckFlow.Core/Diffing/DiffEngine.Compare(oldEntries, newEntries)` already produces a `DeckDiff` (added / count-mismatch / only-in-X / printing conflicts). A thin `PrimerStalenessEvaluator` in Core wraps it to answer "N cards changed since this primer" and can drive a "regenerate?" banner that names the changes.

### Core vs Web
- **Core:** `PrimerStalenessEvaluator` (wraps `DiffEngine`) — pure.
- **Web:** new `PrimerSnapshotStore` (dialect-pluggable SQLite/Postgres via `RelationalDatabaseConnection`, same pattern as `FeedbackStore`/`CategoryKnowledgeStore`) persisting `{ deckKey, fingerprint, decklistText, generatedUtc }`. `DeckPrimerPacketService` gains: store the fingerprint on build; on revisit, compare and set `IsStale` + changed-card summary on the view model. The 3 primer variants change only if the stale banner text lives inside the prompt (hand-edit all 3 if so, ADR-0001).

**Trigger model:** there is no background scheduler for public deck state (the only hosted job is `ArchidektCacheJobService` for the knowledge cache). Staleness is **detected on-demand** when the user re-opens the primer tool for a known deck — re-fetch deck, recompute fingerprint, compare. This fits the stateless, anonymous, 512MB web tier and needs no cron.

---

## Feature 4 — Tap Analyzer surface (minimal change to CastabilitySimulator)

### What the sim already models
`CastabilitySimulator.SimulateGame` (Core/Manabase/CastabilitySimulator.cs) already tracks, per turn, `landsOnBoard` as `(int Mask, int OnlineTurn, int Amount)` where **an untapped land has `OnlineTurn == currentTurn`** and **an ETB-tapped land has `OnlineTurn == currentTurn + 1`** (see `PlayOneLand`, line ~727). `CardKind` distinguishes `UntappedLand` vs `TappedLand`. `ManaSource.EntersUntapped` is already a modeled field. So tapped-vs-untapped state is fully present inside the loop — it is simply **not currently accumulated or surfaced**.

### The minimal change (verified against the source)
1. **Accumulate inside the existing trial loop** in `Simulate` (around the `for (int t = 0; t < trials; t++)` block, lines 228-262): add two counters —
   - `untappedSourceTrials`: increment when the land played on/by the spell's turn entered untapped (read `CardKind.UntappedLand` in `PlayOneLand`, or compare `OnlineTurn <= currentTurn` at play time).
   - `turn1UntappedTrials`: increment when, on `currentTurn == 1`, an untapped land was available to play (the `bestUntappedAny >= 0` branch already exists in `PlayOneLand`).
   These are pure counters threaded out of `SimulateGame` via two new `out` ints (the method already uses `out bool manaShort, out bool colorShort, out int firstCastableTurn`).
2. **Emit on `CardCastability`** (ManabaseModels.cs) as additive fields — `double Turn1UntappedRate`, `double UntappedFrequency` — defaulted like the existing additive `AverageDelay`. **No required-property change, so serialization + every existing test stays byte-compatible.**
3. **Aggregate on `ManabaseReport`** as deck-level rollups (mirror the existing computed `AvgOnCurvePercent` getter pattern) so the report exposes a single untapped-frequency + turn-1 number.
4. **Surface:** `ManabaseReportTextBuilder.Build` appends a "Tap Analyzer" section to the paste text; `Manabase.cshtml` renders the metric. No prompt-variant fan-out needed unless a dedicated tap paste-artifact is wanted.

**Why this is minimal and safe:** it reads state the sim already holds, adds only out-params + additive record fields, and follows the established additive-flag discipline (every manabase enhancement MQ-02..05 / health-band shipped behind additive fields with byte-identical off-paths). **No new simulation, no second pass, no change to cast-% math.** Put the new accumulation behind a flag (`analysis.manabase.tap-analyzer`) only if you want a dark-ship gate — the counters themselves are free (already iterating).

---

## ADR-0001 — 3-variant rendering pattern (confirmed)

`docs/decisions/0001-prompt-variants-decoupled.md` is authoritative and current: **per-platform prompt variants (`ChatGpt`/`Claude`/`Gemini`) are intentionally fully decoupled — no shared guidance text, no base builder, no constants holder.** Each variant owns its complete prompt bytes.

Confirmed structure in the worktree: each workflow family is a folder under `Services/PromptBuilders/<Family>/` with `I<Family>PromptVariant.cs` (strategy interface), three `{ChatGpt,Claude,Gemini}<Family>PromptVariant.cs` implementations, and a `<Family>PromptVariantRegistry.cs`. Families present: `Analysis`, `Comparison`, `FollowUp`, `MetaGap`, `Primer`, `SetUpgrade`.

**Where new sections insert:**
- **Bracket:** add a NEW family `PromptBuilders/Bracket/` with the same 5 files (interface + 3 variants + registry). Each variant hand-writes its bracket-classification + balancer prompt.
- **Multi-Axis Score:** insert a score block into each of the 3 existing `Analysis` variants (e.g., after `## DECK CONTEXT` in `ChatGptAnalysisPromptVariant.Build`). **Hand-apply to all 3** — reviewers must verify all three received the change (ADR-0001 consequence). Wording may differ per platform format (Claude prose-without-bullets vs ChatGPT/Gemini markdown bullets); only semantic divergence is a defect.
- **Tap Analyzer:** lives in `ManabaseReportTextBuilder` (Core paste text), not a prompt variant — manabase has no per-AI prompt family (it emits a deterministic plain-text report + a single swap prompt), so no 3-way fan-out.
- **Primer stale banner:** if banner copy is inside the prompt, hand-edit all 3 `Primer` variants; if it is view-only, no prompt change.

---

## Data Flow

### Bracket analyze (new)
```
[/bracket form POST: deck URL + target bracket]
    ↓
BracketController (DeckToolControllerBase, FeatureFlagGate)
    ↓
BracketAnalysisService.AnalyzeAsync
    ↓ IDeckEntryLoader.LoadFromSourceAsync → entries
    ↓ IScryfallCardResolver / ScryfallCardFactMapper → CardFact[]   (HTTP stays in Web)
    ↓ DeckStatAggregator.Compute → DeckStatSummary                  (Core)
    ↓ BracketClassifier.Classify(facts, summary) → BracketResult     (Core, GameChangerCatalog)
    ↓ BracketBalancer.PlanCuts(deck, target) → BracketCut[]          (Core)
    ↓ {ChatGpt,Claude,Gemini}BracketPromptVariant.Build (×3)         (Web, ADR-0001)
    ↓
[View + zip artifacts via PacketArtifactStore]
```

### Tap Analyzer (read-out of existing sim)
```
ManabaseAnalysisService.AnalyzeAsync → ManabaseAnalyzer.Analyze
    ↓ CastabilitySimulator.Simulate  (EXISTING 20k-trial loop)
    ↓   + untappedSourceTrials / turn1UntappedTrials counters (NEW, free)
    ↓ CardCastability { ..., UntappedFrequency, Turn1UntappedRate } (additive)
    ↓ ManabaseReport rollup getters
    ↓ ManabaseReportTextBuilder (paste) + Manabase.cshtml (view)
```

### Auto-refreshing primer staleness
```
[/deck-primer revisit for known deck]
    ↓ DeckPrimerPacketService: re-load deck → recompute PacketSessionCache key
    ↓ PrimerSnapshotStore.Get(deckKey) → stored fingerprint
    ↓ key != stored?  → IsStale = true
    ↓   DiffEngine.Compare(storedEntries, liveEntries) → DeckDiff (what changed)
    ↓ view shows "Deck changed — regenerate" banner naming the changes
```

---

## Dependency-Ordered Build Sequence

Ordered so each step is independently buildable + testable (Core-first, Web-after), and shared facts land before consumers.

1. **Tap Analyzer (Core)** — add counters in `CastabilitySimulator` + additive fields on `CardCastability`/`ManabaseReport`. Lowest risk (additive, byte-identical off-path), unblocks an early visible win. Surface in `ManabaseReportTextBuilder` + `Manabase.cshtml`. *No dependency on other new work.*
2. **`GameChangerCatalog` + `BracketModels` (Core)** — static data + record shapes. Pure, trivially testable. Prereq for bracket + (game-changer count helps) scoring.
3. **`BracketClassifier` (Core)** — consumes `CardFact` + `DeckStatSummary` + `GameChangerCatalog`. Reuses `DeckStatClassifier` predicates. Full unit coverage.
4. **`BracketBalancer` (Core)** — depends on `BracketClassifier` (needs to know which rules a cut relieves). The uncontested differentiator.
5. **`DeckScorer` + `DeckScoreModels` (Core)** — consumes `DeckStatSummary` (+ optional `ManabaseReport`/combo/tutor counts). Can land in parallel with 3-4; only depends on step 2 if it reuses the game-changer signal.
6. **`PrimerStalenessEvaluator` (Core)** — thin `DiffEngine` wrapper. Independent; can land any time after step 1.
7. **`BracketAnalysisService` + `BracketController` + 3 Bracket prompt variants + registry + view + flag (Web)** — wires step 3-4 to Scryfall hydration (reuse manabase path) and the paste engine. Register tool in `ToolRegistry` + `FeatureFlagCatalog`.
8. **Multi-Axis Score injection (Web)** — fold `DeckScorer` output into `DeckAnalysisPacketService` and hand-edit all 3 `Analysis` variants (ADR-0001). Depends on step 5.
9. **Auto-Refreshing Primer (Web)** — `PrimerSnapshotStore` (dialect-pluggable) + `DeckPrimerPacketService` staleness check + view banner. Depends on step 6.

**Parallelizable:** {1}, {2→3→4}, {5}, {6} are independent Core tracks. Web steps 7-9 each depend only on their Core counterpart, so they can be staged as those land. Bracket (7) is the largest net-new surface (new controller + family + view + flag); Tap Analyzer (1) is the smallest.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Rebuilding the castability sim for Tap Analyzer
The sim already tracks tapped/untapped via `CardKind` + `OnlineTurn`. Do NOT add a second simulation pass or a parallel tap-only sim. Accumulate counters in the existing loop and emit additive fields. (Project scope explicitly says: "Engine already models tapped state (P70-72); this exposes it.")

### Anti-Pattern 2: Extracting shared prompt text for the new Bracket/Score blocks
ADR-0001 forbids cross-variant shared guidance. The new bracket family and the score block must each be hand-written per platform. A consolidation will be reverted (it has been before: `a1fa5ad`→`b2ffba7`).

### Anti-Pattern 3: Putting scoring/classification logic in Web services
`BracketClassifier`, `BracketBalancer`, `DeckScorer`, `PrimerStalenessEvaluator` are pure functions of already-modeled deck facts. Putting them in `Services/` (next to HTTP) breaks the Core purity invariant and makes them un-unit-testable in WSL (where VSTest is flaky and Core tests are the reliable gate). Keep logic in `Core/Analysis`; keep only hydration + render + flags in Web.

### Anti-Pattern 4: A background scheduler for primer staleness
There is no cron for public deck state (only `ArchidektCacheJobService` for the knowledge cache). Detect staleness on-demand when the user re-opens the tool. A scheduler would need to track every deck URL ever primed — wrong for the stateless, anonymous, 512MB tier.

### Anti-Pattern 5: Making the Game Changers list a runtime DB/HTTP dependency
The list is ~40 official card names, slow-changing. Ship it as a Core `static readonly HashSet<string>` (normalized via `CardNormalizer`). A DB row or Scryfall call adds latency + failure modes for data the deck already has in hand. Revisit only if update cadence becomes painful — not this cycle.

### Anti-Pattern 6: Changing `required`/get-only on the modified records
`ManabaseModels` records are JSON-round-tripped; the CLAUDE.md carve-out warns System.Text.Json silently drops get-only positional members. New tap/score fields must be `{ get; init; }` with safe defaults, added additively — never as `required` (would break existing constructors/tests/zip round-trips).

---

## Integration Points Summary

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Web `BracketAnalysisService` ↔ Core `BracketClassifier`/`Balancer` | Direct method call (pure facts in/out) | Mirrors `ManabaseAnalysisService` ↔ `ManabaseAnalyzer` |
| Web hydration ↔ Scryfall | `IScryfallCardResolver` + `ScryfallCardFactMapper` (RestSharp+Polly) | Reuse the EXISTING manabase `CardFact` hydration path; no new HTTP client |
| Core `CastabilitySimulator` ↔ `ManabaseReport` | Additive fields on existing records | Byte-identical off-path; same discipline as MQ-02..05 |
| Web `DeckPrimerPacketService` ↔ Core `DiffEngine` | Direct call via `PrimerStalenessEvaluator` | `DiffEngine` already Core; `DeckDiff` already models add/remove/mismatch |
| Web `PrimerSnapshotStore` ↔ SQLite/Postgres | `RelationalDatabaseConnection` dual-dialect | Same pattern as `FeedbackStore`/`CategoryKnowledgeStore` |
| Web `DeckScorer` block ↔ 3 Analysis variants | Hand-edited per platform | ADR-0001; reviewers verify all 3 |
| New tools ↔ visibility | `ToolRegistry` + `FeatureFlagCatalog` + `[FeatureFlagGate]` | Bracket needs a tile/nav/help entry + flag, like every existing tool |

---

## Sources

- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — verified tapped/untapped modeling (`CardKind`, `OnlineTurn`, `PlayOneLand`), the trial loop, and the existing `out` param + additive-field pattern (`AverageDelay`); confirmed the minimal tap-accumulation seam.
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — verified `CardFact` consumer shape, `CardCastability`/`ManabaseReport` additive-field discipline, computed-getter rollup pattern (`AvgOnCurvePercent`).
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — confirmed pure-Core analyzer driven by Web service; flag threading pattern.
- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` + `DeckStatAggregator.cs` — confirmed reusable role predicates + `DeckStatSummary` as the bracket/score input.
- `DeckFlow.Core/Diffing/DiffEngine.cs` — confirmed `DeckDiff` add/remove/mismatch output for primer staleness.
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` — confirmed existing `PrimerCacheInputs` + `PacketSessionCache.ComputeKey` canonical-deck fingerprint = the staleness signal.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — confirmed the Scryfall→`CardFact` hydration path the bracket service should reuse.
- `DeckFlow.Web/Services/PromptBuilders/{Analysis,Primer}/*` + `docs/decisions/0001-prompt-variants-decoupled.md` — confirmed the per-family interface+3-variant+registry pattern and the no-shared-text rule.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — confirmed dotted-namespace flag + description-catalog pattern for new tools.
- `DeckFlow.Web/Controllers/{Manabase,DeckPrimer}Controller.cs` — confirmed `DeckToolControllerBase` + `[FeatureFlagGate]` controller pattern.
- `.planning/PROJECT.md` + `scratchpad-research/commander-feature-wants-report.md` — confirmed feature scope and the uncontested-gap framing.

---
*Architecture research for: DeckFlow Cycle 13 — Deck Evaluation & Creator Output*
*Researched: 2026-06-27*
