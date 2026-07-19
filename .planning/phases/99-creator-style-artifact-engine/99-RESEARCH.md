# Phase 99: Creator-Style Artifact Engine - Research

**Researched:** 2026-07-19
**Domain:** Deterministic C# scoring/diff engine + prompt-artifact assembly (no LLM), reusing Phase 94-98 substrate
**Confidence:** HIGH (this phase is almost entirely composition of existing, already-shipped in-repo services; very little net-new algorithmic risk)

## Summary

Phase 99 does not introduce a new technology or external dependency. It is a
composition phase: wire together five pieces of substrate that Phases 94-98
already shipped and committed in this branch, plus one reference implementation
(`DeckAnalysisPacketService`) to mirror structurally. No package installs, no
new HTTP surface, no LLM calls.

The five substrate pieces, all confirmed present in the working tree:
1. **`ICreatorStyleProfileStore`** (`DeckFlow.Core/Content/ICreatorStyleProfileStore.cs`) — returns a `CreatorStyleProfile` with `FusedTargets: IReadOnlyList<FusedTarget>` keyed by creator slug. This is the "fused profile as weighted numeric targets" (CS-28a).
2. **`ICardGroundingGuard`** (`DeckFlow.Core/Knowledge/CardGrounding/ICardGroundingGuard.cs`, impl `DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs`) — the Phase 98 guard. Its doc comment literally says "the P99 caller" for `CardGroundingDeckContext.DeckCardNames`. This is CS-29.
3. **`CreatorWhitelistPoolBuilder`** (`DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs`) — already produces a guard-validated, frequency-ranked candidate pool (cap 25) from `ICreatorDeckCacheStore`. Its own code comment says the cap "stays adjustable once Phase 99 token budget is measured" — i.e. Phase 98 was built anticipating this exact consumer.
4. **`ICreatorDeckCacheStore`** (`DeckFlow.Core/Content/ICreatorDeckCacheStore.cs`) — `GetByCreatorAsync` returns `CreatorDeckCacheEntry { FolderName, Size, ConfidenceMarker, Entries: IReadOnlyList<DeckEntry> }`. This is the direct source for "2-3 real creator-deck exemplars" (CS-28b) — no new fetch/crawl needed, it's already cached.
5. **`KarstenManabase`** (`DeckFlow.Core/Manabase/KarstenManabase.cs`) — pure static deterministic math (`SingletonLandTarget`, `CedhLandTarget`, `CastConsistency`, `SourcesNeeded`). Already the source of the `karsten:target_lands` / `karsten:land_delta` / `karsten:health_score` measured-metric keys that Phase 95/97 fused into `FusedTarget.Metric`.

The rubric (CS-27) is a diff, not a new scoring theory: for each `FusedTarget` in the creator's profile, compute the equivalent statistic on the *submitted* deck (category ratios via `CategoryKnowledgeRepository.GetCategoriesAsync`, land count via `KarstenManabase`, combo density via `ICommanderSpellbookService.FindCombosAsync` — all already-wired services) and produce a deterministic delta/verdict per metric, reusing the same `Verdict`/`Confidence` vocabulary `ProfileFusionEngine` already established (`"high"/"med"/"low"`, `"insufficient-measured"`, etc.) so the two ledgers (fusion ledger, rubric ledger) read consistently.

**Primary recommendation:** Build `CreatorStylePacketService` as a new scoped service in `DeckFlow.Web/Services/CreatorStyle/`, structurally mirroring `DeckAnalysisPacketService`'s constructor-injection / `BuildAsync(request, ct)` / `PacketSessionCache` shape, but keep it far smaller — do not replicate `DeckAnalysisPacketService`'s ~2250-line, 12-flag complexity. Put the pure diff/scoring math in `DeckFlow.Core/Knowledge/CreatorStyleRubric/` (mirroring `ProfileFusion`'s Core-only, fully-unit-testable placement) and keep only I/O orchestration (deck loading, guard calls, exemplar selection) in the `DeckFlow.Web` service, exactly as `ProfileFusionEngine` (Core, pure) + `MeasuredStyleProfileBuilder` (Web, orchestration) already split in Phase 95/97.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Rubric scoring (diff submitted-deck stats vs. `FusedTarget[]`) | API/Backend (`DeckFlow.Core`) | — | Pure math, no I/O; must be unit-testable in isolation per SC #4. Mirrors `ProfileFusionEngine`'s Core-only placement. |
| Karsten land-target computation for submitted deck | API/Backend (`DeckFlow.Core`) | — | `KarstenManabase` is already pure Core; reuse directly, do not re-derive. |
| Submitted-deck category-ratio computation | API/Backend (`DeckFlow.Web` orchestration → `DeckFlow.Core` repository) | — | `CategoryKnowledgeRepository` is a Web-registered singleton wrapping a SQLite/PG store; the classification call itself is I/O (DB read per card name), so it lives in the Web orchestrator, but the ratio *math* should be a pure Core helper fed the classified counts. |
| Exemplar deck selection (2-3 real decks) | API/Backend (`DeckFlow.Web`) | — | Reads `ICreatorDeckCacheStore` (I/O), needs ranking logic; the ranking/selection *rule* (e.g. "closest Size to submitted deck", "highest ConfidenceMarker") should be a pure, testable helper even though the store read is I/O. |
| Card-grounding validation of every referenced card | API/Backend (`DeckFlow.Web` → `ICardGroundingGuard`) | — | Guard is Web-hosted (needs Scryfall HTTP); Core only sees the seam interface. Already established in Phase 98. |
| Combo/synergy context | API/Backend (`DeckFlow.Web` → `ICommanderSpellbookService`) | — | Existing HTTP-backed service, same pattern `DeckAnalysisPacketService` already uses for combo lookups. |
| Artifact text assembly (final prompt string) | API/Backend (`DeckFlow.Web`) | — | String composition from the above pieces; no page/controller exists yet per SC #4 — this phase stops at the assembled string + result record, consumed later by Phase 100's controller. |
| Session/packet caching | API/Backend (`DeckFlow.Web`, `PacketSessionCache`) | — | Optional this phase (no page = no request cycle to cache yet), but if `BuildAsync` is exposed publicly now, follow the existing `PacketSessionCache` + `TryComputeCacheKeyAsync` pattern so Phase 100 does not have to retrofit it. |

## User Constraints

No `99-CONTEXT.md` exists yet in `.planning/phases/99-creator-style-artifact-engine/` — `/gsd:discuss-phase 99` has not been run. This research was requested standalone/ahead of that step. There are therefore no locked user decisions to copy verbatim. The planner should treat the "Assumptions Log" and "Open Questions" below as the candidate discussion topics for `/gsd:discuss-phase 99` if that step runs before planning.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CS-26 | `CreatorStylePacketService` mirroring `DeckAnalysisPacketService`'s shape (the "new tool page" clause of CS-26's text is explicitly OUT of scope for Phase 99 — see Assumptions Log A1) | See "Architecture Patterns" — constructor-injection scoped service, `BuildAsync(request, ct)`, optional `PacketSessionCache` wiring; DI registration pattern in `PacketServiceCollectionExtensions.cs` |
| CS-27 | Deterministic C# rubric scoring — diff submitted deck vs. fused targets + Karsten math, no LLM | `KarstenManabase` (pure static, Core), `ProfileFusionEngine`'s `FusedTarget` vocabulary, `CategoryKnowledgeRepository` for category-ratio counts on the submitted deck |
| CS-28 | Artifact injects: (a) fused targets, (b) 2-3 exemplars, (c) validated synergy/combo, (d) rubric scores, (e) "critique only with provided cards" instruction | (a) `ICreatorStyleProfileStore.GetBySlugAsync`; (b) `ICreatorDeckCacheStore.GetByCreatorAsync`; (c) `ICommanderSpellbookService.FindCombosAsync` + `CreatorWhitelistPoolBuilder`; (d) the new rubric from CS-27; (e) a fixed instruction string, same pattern as `DeckAnalysisPacketService`'s prompt-builder text blocks |
| CS-29 | Every referenced card passes the Phase 98 guard pre-ship | `ICardGroundingGuard.ValidateAllAsync` (batch) — call once over the union of {exemplar deck cards, whitelist cards, any explicitly-named combo pieces} before assembly returns; `CardGroundingBatchResult.HasUpstreamFailure` must gate what ships (see Pitfall 2) |

## Standard Stack

No new packages. This phase composes existing DeckFlow.Core / DeckFlow.Web types only.

### Core (existing, reused)
| Type | Location | Purpose |
|------|----------|---------|
| `CreatorStyleProfile`, `FusedTarget`, `MeasuredMetric` | `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` | Fused profile shape to diff against |
| `KarstenManabase` | `DeckFlow.Core/Manabase/KarstenManabase.cs` | Deterministic land-target/consistency math |
| `ICardGroundingGuard`, `CardGroundingDeckContext`, `CardGroundingBatchResult`, `CardGroundingVerdict` | `DeckFlow.Core/Knowledge/CardGrounding/*.cs` | Card-grounding seam (Core-facing contract; Web-hosted impl) |
| `CategoryKnowledgeRepository` / `CardCategoryRepository` | `DeckFlow.Core/Knowledge/*.cs` | Category classification (ramp/removal/draw/etc.) for arbitrary card names |
| `ContentTagVocabulary.CardCategories` | `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` | The closed 11-category vocabulary (`ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility`) — must match exactly for the rubric to join `category_ratio:*` fused metrics |
| `StatedMetricKeyMapper` | `DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs` | Canonical metric-key vocabulary: `category_ratio:{category}`, `karsten:target_lands`, `karsten:land_delta`, `karsten:health_score`, `combo_density:included_per_deck` — the rubric should key off these exact strings, not invent new ones |

### Supporting (existing Web services, reused)
| Service | Location | When to Use |
|---------|----------|-------------|
| `ICreatorStyleProfileStore` | `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs`, impl `CreatorStyleProfileStore.cs` | Load the creator's fused profile by slug |
| `ICreatorDeckCacheStore` | `DeckFlow.Core/Content/ICreatorDeckCacheStore.cs`, impl `CreatorDeckCacheStore.cs` | Load cached creator decks for exemplar selection |
| `CreatorWhitelistPoolBuilder` | `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` | Guard-validated, frequency-ranked candidate card pool per creator |
| `ICommanderSpellbookService` | `DeckFlow.Web/Services/CommanderSpellbookService.cs` | Combo/synergy context (`FindCombosAsync`) — already used identically by `DeckAnalysisPacketService` |
| `IDeckEntryLoader` | `DeckFlow.Core/Loading/*` | Load the submitted deck (URL or paste) — same loader every other packet service uses |
| `PacketSessionCache` | `DeckFlow.Web/Services/Packets/PacketSessionCache.cs` | Session-scoped result caching, if `BuildAsync` gets an externally-callable cache key this phase |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reusing `CategoryKnowledgeRepository` for submitted-deck category counts | Re-deriving categories from Scryfall oracle tags directly in Phase 99 | Rejected — duplicates Phase 95's exact classification logic; the fused profile's `category_ratio:*` values were computed via this same repository, so using a different classifier here would silently break the apples-to-apples diff (see Pitfall 1). |
| Full `ManabaseAnalyzer.Analyze(ManabaseDeck)` simulation for the submitted deck's land math | Calling `KarstenManabase.SingletonLandTarget`/`CedhLandTarget` directly with a few scalar deck stats | `ManabaseAnalyzer` requires building a full `ManabaseDeck` (mana sources, weights, castability simulation) via Scryfall-resolved card data — heavier than needed for a target/actual land-count comparison. Prefer the lighter, purely-scalar `KarstenManabase` call unless the rubric explicitly needs full castability simulation (open question — see below). |

**Installation:** none — no new packages for this phase.

## Package Legitimacy Audit

Not applicable. Phase 99 introduces zero new external packages, NuGet or otherwise. `slopcheck` / registry verification is skipped per the protocol's scope (only required "whenever this phase installs external packages").

## Architecture Patterns

### System Architecture Diagram

```text
Submitted deck (URL/paste text)
        │
        ▼
IDeckEntryLoader.LoadFromSourceAsync ──► List<DeckEntry> (submitted deck)
        │
        ├──► CategoryKnowledgeRepository.GetCategoriesAsync (per card)
        │        └──► submitted-deck category_ratio:* counts
        │
        ├──► KarstenManabase.SingletonLandTarget/CedhLandTarget
        │        └──► submitted-deck karsten:target_lands / land_delta
        │
        └──► ICommanderSpellbookService.FindCombosAsync
                 └──► submitted-deck combo_density:included_per_deck + combo names

ICreatorStyleProfileStore.GetBySlugAsync(creatorSlug)
        └──► CreatorStyleProfile.FusedTargets (creator's weighted numeric targets)

                    │                              │
                    ▼                              ▼
        ┌───────────────────────────────────────────────────┐
        │   CreatorStyleRubric.Score (DeckFlow.Core, PURE)   │
        │   for each FusedTarget: compute submitted-deck     │
        │   equivalent, delta, verdict — NO I/O, NO LLM      │
        └───────────────────────────────────────────────────┘
                    │
                    ▼
        RubricScoreResult (per-metric verdicts)

ICreatorDeckCacheStore.GetByCreatorAsync(creatorSlug)
        └──► candidate exemplar decks ──► pick 2-3 (selection rule, pure/testable)

CreatorWhitelistPoolBuilder.BuildAsync(creatorSlug, deckContext)
        └──► guard-validated ranked candidate names

        │  (exemplar card names + whitelist names + any named combo pieces)
        ▼
ICardGroundingGuard.ValidateAllAsync(allReferencedCardNames, deckContext)
        └──► CardGroundingBatchResult — MUST gate assembly (CS-29)
                    │
                    ▼
        ┌───────────────────────────────────────────────────┐
        │  CreatorStylePacketService.BuildAsync (Web)        │
        │  assembles final artifact text:                    │
        │   1. fused profile as weighted numeric targets      │
        │   2. 2-3 real exemplar decks                        │
        │   3. validated synergy/combo context                │
        │   4. rubric scores                                  │
        │   5. "critique only with the provided cards" text   │
        └───────────────────────────────────────────────────┘
                    │
                    ▼
        CreatorStylePacketResult (returned; NO controller/page consumes it yet)
```

### Recommended Project Structure
```
DeckFlow.Core/Knowledge/CreatorStyleRubric/          # NEW — pure diff/scoring math
├── CreatorStyleRubricScorer.cs                       # static class, mirrors ProfileFusionEngine's shape
├── RubricMetricScore.cs                               # per-metric verdict record
└── RubricScoreResult.cs                               # aggregate result record

DeckFlow.Web/Services/CreatorStyle/                   # EXISTING folder — add to it
├── CreatorStylePacketService.cs                        # NEW — orchestrator, mirrors DeckAnalysisPacketService shape
├── CreatorDeckExemplarSelector.cs                      # NEW — pure selection rule (given decks, pick 2-3)
├── CreatorWhitelistPoolBuilder.cs                      # EXISTING — reuse as-is
├── MeasuredStyleProfileBuilder.cs                      # EXISTING — do not modify (Phase 95/97 owns it)
└── CreatorProfileDeckCrawler.cs                        # EXISTING — do not modify

DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/       # NEW — pure unit tests, no controller/page dep (SC #4)
DeckFlow.Web.Tests/Services/CreatorStyle/               # EXISTING folder — add CreatorStylePacketServiceTests.cs
```

### Pattern 1: Core-pure rubric, Web-only orchestration (mirrors Phase 97's split)
**What:** `ProfileFusionEngine.Fuse(measured, statedRules)` in `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` is a `static` method taking only plain data (`IReadOnlyList<MeasuredMetric>`, `IReadOnlyList<StatedRuleCandidate>`) and returning `IReadOnlyList<FusedTarget>` — zero I/O, zero DI, fully constructible in a test with in-memory lists.
**When to use:** The CS-27 rubric must follow the identical shape: a static `Score(IReadOnlyList<FusedTarget> creatorTargets, SubmittedDeckStats submittedStats)` method in Core, callable from a unit test with hand-built records — no mock HTTP client, no mock store needed for the scoring logic itself.
**Example:**
```csharp
// Source: DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs:20 (existing shipped code)
public static IReadOnlyList<FusedTarget> Fuse(
    IReadOnlyList<MeasuredMetric> measured,
    IReadOnlyList<StatedRuleCandidate> statedRules)
{
    ArgumentNullException.ThrowIfNull(measured);
    ArgumentNullException.ThrowIfNull(statedRules);
    // ... pure computation, no awaits, no I/O ...
}
```
Phase 99's rubric scorer should be structurally identical: a static entry point in `DeckFlow.Core/Knowledge/CreatorStyleRubric/`, taking the already-fetched `FusedTarget[]` plus already-computed submitted-deck stat inputs (both fetched by the Web orchestrator beforehand), returning a plain result record.

### Pattern 2: Guard-gate before assembly returns, with `HasUpstreamFailure` short-circuit
**What:** `CreatorWhitelistPoolBuilder.BuildAsync` (existing, `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs:68-77`) calls `ICardGroundingGuard.ValidateAllAsync` once over the full raw candidate list, logs a warning if `HasUpstreamFailure`, and returns only the `Accepted` subset.
**When to use:** CS-29 requires this exact pattern applied to *every* card name that ends up anywhere in the assembled artifact (exemplar decklists, whitelist, any named combo cards) — not just the whitelist. Batch all candidate names into a single `ValidateAllAsync` call (never loop `TryValidateAsync` per card — that defeats the batch-cache/Scryfall-collection-endpoint optimization already built into `CardGroundingGuard`).
**Example:**
```csharp
// Source: DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs:68-77 (existing shipped code)
CardGroundingBatchResult validation = await _cardGroundingGuard
    .ValidateAllAsync(rawPool, deckContext, cancellationToken)
    .ConfigureAwait(false);

if (validation.HasUpstreamFailure)
{
    _logger.LogWarning(
        "Creator whitelist validation saw upstream failures for creator {CreatorSlug}; returning accepted subset only.",
        creatorSlug);
}

return validation.Verdicts
    .Where(verdict => verdict.Accepted)
    .Select(verdict => verdict.CanonicalName)
    .ToArray();
```

### Pattern 3: DI registration — factory-lambda scoped service resolving from `sp`
**What:** All four existing packet services are registered in one place, `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs`, via `services.AddScoped<IXyzService>(sp => new XyzService(sp.GetRequiredService<...>(), ...))`.
**When to use:** Register `CreatorStylePacketService` the same way, adding a new `services.AddScoped<ICreatorStylePacketService>(sp => new CreatorStylePacketService(...))` block inside `AddDeckFlowPacketServices()` (or a new sibling extension method if the planner prefers separating creator-style registrations — either is consistent with existing conventions, since `CreatorWhitelistPoolBuilder` is already registered directly in `Program.cs` rather than in the extension file).
**Example:**
```csharp
// Source: DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:49-62 (existing shipped code)
services.AddScoped<IDeckAnalysisPacketService>(sp =>
    new DeckAnalysisPacketService(
        sp.GetRequiredService<IScryfallCardResolver>(),
        sp.GetRequiredService<IDeckEntryLoader>(),
        // ... 8 more required deps ...
        sp.GetRequiredService<PacketSessionCache>(),
        sp.GetService<IFeatureFlagCache>(),      // optional, nullable
        sp.GetService<ILogger<DeckAnalysisPacketService>>()));
```

### Pattern 4: DI-registration tripwire test (mandatory precedent, not optional)
**What:** Phase 98's gap-closure plan (98-05) shipped `CreatorStyleDiRegistrationTests.cs` specifically because a new scoped Web service's *transitive* store dependency graph was missing three registrations and broke `dotnet run` cold start in Development. The test uses `ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }` to catch this at test time instead of at manual cold-start time.
**When to use:** Phase 99 MUST add an equivalent DI-graph validation test (or extend the existing `CreatorStyleDiRegistrationTests.cs`) covering `CreatorStylePacketService`'s full constructor graph before merging, given this exact failure mode already bit this codebase once in this same cycle.
**Example:**
```csharp
// Source: DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs (existing shipped test, pattern to extend)
using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true,
});
Assert.NotNull(provider.GetRequiredService<CreatorWhitelistPoolBuilder>());
```

### Anti-Patterns to Avoid
- **Re-deriving category classification instead of reusing `CategoryKnowledgeRepository`:** the fused profile's `category_ratio:*` values were computed from this repository's classification during Phase 95's measured extraction. Using a different classifier (e.g. raw Scryfall oracle-tag string matching) for the submitted deck would compare two different vocabularies and silently produce meaningless deltas.
- **Looping `ICardGroundingGuard.TryValidateAsync` per card** instead of one `ValidateAllAsync` batch call — defeats the Scryfall `cards/collection` batch endpoint (75-card chunks) already built into `CardGroundingGuard`, and multiplies HTTP round-trips under the 512MB Render Starter tier's constrained resources.
- **Copying `DeckAnalysisPacketService`'s full ~2250-line complexity wholesale** (12 feature flags, multiple workflow-step branches, saved-JSON round-trip paths). Phase 99 has zero flags, zero workflow steps, and no page yet — mirror the *shape* (constructor injection, `BuildAsync`, result record, optional cache-key method) not the *size*.
- **Building a full `ManabaseDeck`/`ManabaseAnalyzer.Analyze` simulation** for the submitted deck's Karsten comparison when the rubric only needs a target-vs-actual land count and a curve-consistency check — that full simulation exists for the manabase tool's own castability *report*, which is a materially larger feature than what CS-27 asks for. Confirm scope with the planner (see Open Questions).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Karsten land-count math | A new regression/hypergeometric formula | `DeckFlow.Core/Manabase/KarstenManabase.cs` (`SingletonLandTarget`, `CedhLandTarget`, `ConsistencyThreshold`, `CastConsistency`, `SourcesNeeded`) | Already implemented, already unit-tested, already the source of the `karsten:*` fused-metric values this phase must diff against. Re-deriving would risk numeric drift from the values already stored in `FusedTarget`. |
| Card legality/color-identity/singleton/castability validation | New Scryfall-lookup + rule checks | `ICardGroundingGuard` (Phase 98) | Exactly what CS-29 requires; hand-rolling would duplicate Phase 98's fixture-tested hallucination-rejection logic and risk missing a rejection class the guard already covers. |
| Frequency-ranked candidate card pool from a creator's decks | New corpus-scanning logic | `CreatorWhitelistPoolBuilder` | Already built, already guard-validated, already capped, explicitly built "for Phase 99" per its own code comment. |
| Category (ramp/removal/draw/...) classification of arbitrary cards | New oracle-text keyword matching | `CategoryKnowledgeRepository.GetCategoriesAsync` / `CardCategoryRepository` | This is the exact classifier Phase 95's measured extractor used to produce the `category_ratio:*` fused metrics; using anything else breaks apples-to-apples comparison (see Anti-Patterns). |
| Combo/synergy detection | New combo-graph logic | `ICommanderSpellbookService.FindCombosAsync` | Already the single canonical combo source used by every other packet service (`DeckAnalysisPacketService`, `DeckPrimerPacketService`). |
| Deck loading (URL or paste, Moxfield/Archidekt) | A new parser/importer | `IDeckEntryLoader` | Shared by every existing packet service; handles Moxfield-fallback notices, DFC normalization, etc. |

**Key insight:** This phase's entire value is in *composition*, not new algorithms. Every "don't hand-roll" item above already exists, has existing tests, and (for the guard and whitelist builder) was explicitly built in Phase 98 anticipating this exact phase as its consumer. The main planning risk is scope creep — building something Phase 94-98 already built — not missing capability.

## Common Pitfalls

### Pitfall 1: Metric-key vocabulary drift between fusion and rubric
**What goes wrong:** The rubric computes a submitted-deck stat under a metric name/shape that doesn't match `FusedTarget.Metric` exactly (e.g. computing `"ramp_count"` when the fused profile stores `"category_ratio:ramp"`), producing silent no-match rows instead of a computed delta.
**Why it happens:** `StatedMetricKeyMapper` (Phase 97) already hit this exact class of bug — the RESEARCH.md for Phase 97 notes a prior `.Equals()` join risk that "produces zero matches for ramp, removal, draw, finishers, win-cons, counter..." when the vocabulary isn't reused verbatim.
**How to avoid:** Import and key off `StatedMetricKeyMapper`'s canonical strings (`category_ratio:{category}` for the 11 `ContentTagVocabulary.CardCategories`, plus `karsten:target_lands`, `karsten:land_delta`, `karsten:health_score`, `combo_density:included_per_deck`) rather than inventing new metric-name strings in the rubric.
**Warning signs:** A rubric test where every fused target comes back "no comparable data" despite the fixture profile having real values.

### Pitfall 2: `HasUpstreamFailure` silently shipping an artifact with missing validation
**What goes wrong:** If Scryfall is down mid-request, `ICardGroundingGuard.ValidateAllAsync` returns a batch where some verdicts are `RejectReason.UpstreamUnavailable` (rejected, not accepted) — but `CardGroundingBatchResult.HasUpstreamFailure` is just a boolean flag; nothing *forces* the caller to abort. `CreatorWhitelistPoolBuilder` only logs a warning and silently returns the accepted subset.
**Why it happens:** The guard is fail-safe (never lets an unvalidated card through — a rejected verdict never gets accepted), but the *caller* still decides whether "artifact with a smaller-than-requested exemplar/combo set" is an acceptable degraded response versus a hard failure.
**How to avoid:** Decide explicitly (and get this into CONTEXT.md / plan) whether `CreatorStylePacketService.BuildAsync` should (a) proceed with whatever subset of cards passed, silently smaller, mirroring `CreatorWhitelistPoolBuilder`'s existing behavior, or (b) throw/return a distinct failure result when `HasUpstreamFailure` is true for anything that would otherwise appear in the artifact. CS-29's "pre-ship" wording suggests (b) is closer to the requirement's intent for at least the exemplar decklists (an exemplar deck missing half its cards because Scryfall hiccuped is a worse artifact than no exemplar at all).

### Pitfall 3: `ICreatorStyleProfileStore` is bound to a local-only SQLite file (D-14), not Render-shipped
**What goes wrong:** `DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection` is documented in-code as "always-SQLite... local-only and must never be uploaded to Render (D-14)" and `Program.cs:108-111` binds `ICreatorStyleProfileStore` through exactly that connection factory. Any code path that calls `ICreatorStyleProfileStore.GetBySlugAsync` in the deployed Render container will hit an empty/nonexistent local `content-kb.db`, not the creator's actual fused profile.
**Why it happens:** Content-KB (transcripts, distilled rules, and now creator-style profiles) was deliberately kept off Render for cost/scope reasons in prior cycles (D-14), predating this cycle's decision to make a *runtime* feature (the artifact engine) depend on that same store.
**How to avoid:** This doesn't block Phase 99 itself (no page/controller exists yet, so nothing calls this in production this phase), but it is a load-bearing fact the planner and Phase 100 must know before wiring a controller: either (a) a KB-sync mechanism analogous to `ContentSiteIndexStore`'s prod-bound slim index needs to ship fused profiles to a Render-reachable store, or (b) `ICreatorStyleProfileStore`'s connection factory needs to change for this table specifically. Flag this loudly for discuss-phase / Phase 100 planning rather than silently assuming "it'll just work" once a page exists.
**Warning signs:** A locally-passing xUnit test suite (SQLite fixture, in-memory or temp file) gives zero signal on this — the gap is dev/prod topology, not code correctness, so tests alone will not catch it.

### Pitfall 4: Naming/shape confusion — `ChatGptDeckPacketService` does not exist under that name anymore
**What goes wrong:** The task's own "additional context" references `ChatGptDeckPacketService` as an example to inspect. That type was renamed during Cycle 15's Phase 85 "`chatgpt-*` naming cleanup" (per project MEMORY.md and `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`'s current name/namespace). A search for `ChatGptDeckPacketService` in the current tree returns no matches.
**Why it happens:** Stale naming reference in planning documents predating the Phase 85 rename.
**How to avoid:** Treat `DeckAnalysisPacketService` (and, for a smaller/simpler reference, `DeckPrimerPacketService`, ~866 lines vs. ~2250) as the two mirror candidates. `DeckPrimerPacketService` is structurally closer to what Phase 99 needs (no feature flags, straightforward `BuildAsync`/`TryComputeCacheKeyAsync`/staleness-check triad) and is the better shape template; `DeckAnalysisPacketService` is useful only for the DI-registration and combo-lookup-reuse patterns, not as a line-count target.

### Pitfall 5: Confusing "2-3 real creator-deck exemplars" with the whitelist pool
**What goes wrong:** `CreatorWhitelistPoolBuilder` produces a flat list of up to 25 *card names* (frequency-ranked across all the creator's decks) — this is NOT the same thing as "2-3 real creator-deck exemplars" (CS-28b), which means 2-3 *whole decklists* shown as concrete examples, sourced from `ICreatorDeckCacheStore.GetByCreatorAsync`'s `CreatorDeckCacheEntry.Entries` directly.
**Why it happens:** Both features draw from the same underlying `ICreatorDeckCacheStore` corpus, so it's easy to conflate "ranked card pool" with "example decks."
**How to avoid:** Plan for two distinct pieces of assembled content: (1) the whitelist (guard-validated ranked card names, reused as-is from Phase 98) feeding the "critique only with provided cards" constraint, and (2) a small, separately-selected set of 2-3 full `CreatorDeckCacheEntry` decklists (selection rule TBD — see Open Questions) shown verbatim as style exemplars.

## Code Examples

### Loading the creator's fused profile
```csharp
// Composition pattern based on DeckFlow.Core/Content/ICreatorStyleProfileStore.cs (existing interface)
CreatorStyleProfile? profile = await _profileStore.GetBySlugAsync(creatorSlug, cancellationToken);
if (profile is null || profile.InsufficientSample)
{
    // CS-03 floor already enforced upstream at profile-build time (Phase 94/97);
    // decide here whether an insufficient-sample profile should still produce
    // a degraded artifact or a hard "not enough data" result.
}
IReadOnlyList<FusedTarget> targets = profile?.FusedTargets ?? Array.Empty<FusedTarget>();
```

### Selecting exemplar decks (new pure helper — no existing precedent, propose this shape)
```csharp
// Proposed shape, mirroring the RankedCandidate pattern already used in
// CreatorWhitelistPoolBuilder.cs:178-192 for a similarly-shaped "pick best N" problem.
internal static IReadOnlyList<CreatorDeckCacheEntry> SelectExemplars(
    IReadOnlyList<CreatorDeckCacheEntry> creatorDecks,
    int submittedDeckSize,
    int maxExemplars = 3)
{
    // Candidate rule (confirm with planner/discuss-phase): prefer decks whose
    // ConfidenceMarker is highest and whose Size is closest to submittedDeckSize,
    // deterministic tie-break by DeckId (Ordinal) for reproducible output.
    return creatorDecks
        .OrderByDescending(deck => deck.ConfidenceMarker, StringComparer.Ordinal)
        .ThenBy(deck => Math.Abs(deck.Size - submittedDeckSize))
        .ThenBy(deck => deck.DeckId, StringComparer.Ordinal)
        .Take(maxExemplars)
        .ToArray();
}
```

### Batch card-grounding gate before returning the assembled artifact
```csharp
// Source pattern: DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs:68-83
IReadOnlyList<string> allReferencedNames = exemplarCardNames
    .Concat(whitelistNames)
    .Concat(comboCardNames)
    .Distinct(StringComparer.Ordinal)
    .ToArray();

CardGroundingBatchResult validation = await _cardGroundingGuard
    .ValidateAllAsync(allReferencedNames, deckContext, cancellationToken)
    .ConfigureAwait(false);

// CS-29: decide fail-open vs fail-closed here (see Pitfall 2) before assembling text.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — no prior creator-style artifact existed | Deterministic C# rubric + assembled artifact (this phase) | Phase 99 (this cycle) | First implementation; no legacy behavior to preserve, no byte-identical gate applies yet (that gate is a Phase 100 concern per CS-31, since no page/controller exists to compare against). |

**Deprecated/outdated:** None relevant — this is greenfield composition, not a refactor.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | CS-26's "new tool page" clause is deferred to Phase 100; Phase 99 builds only `CreatorStylePacketService` with no controller/page, per the phase description's explicit SC #4 ("no controller or page dependency") and the ROADMAP's "Codex-mandated 2-phase split" note | Summary, Phase Requirements | If wrong, Phase 99's scope is larger than assumed (would need a page too) — but the phase's own success criteria explicitly contradict a page being in scope, so this is a documentation-text-vs-phase-description mismatch, not a real ambiguity. Low risk; flag to planner for one-line confirmation. |
| A2 | The rubric should reuse `CategoryKnowledgeRepository`'s classification for submitted-deck category ratios rather than a new classifier, to keep metric keys joinable against `FusedTarget.Metric` | Standard Stack, Pitfall 1 | If wrong (e.g. the planner wants a cheaper/simpler classifier), the rubric's category-based rows will never match against real fused profiles — moderate risk, should be confirmed in discuss-phase or plan review. |
| A3 | The submitted-deck side of the Karsten diff needs only scalar `KarstenManabase` calls (land target, consistency), not a full `ManabaseAnalyzer`/`ManabaseDeck` castability simulation | Anti-Patterns, Alternatives Considered | If wrong, the rubric under-delivers on "Karsten math" depth (e.g. missing per-color castability %, not just aggregate land count) — moderate risk, this is a legitimate open scope question, not a research gap; see Open Questions #1. |
| A4 | `ICardGroundingGuard.ValidateAllAsync`'s `HasUpstreamFailure` behavior should be treated as a hard-fail gate for shipped artifact content (fail-closed) rather than mirroring `CreatorWhitelistPoolBuilder`'s fail-open/log-warning precedent | Pitfall 2 | If wrong, either (a) artifacts silently ship with fewer validated cards than intended when Scryfall hiccups (current whitelist precedent), or (b) the service throws too aggressively on transient Scryfall issues — this is a real product decision, not something research can resolve; flag for discuss-phase. |
| A5 | Exemplar-deck selection rule (confidence-marker + size-proximity) is a reasonable default, not something already specified anywhere in the shipped Phase 94-98 code | Code Examples | No prior code establishes a selection rule — this is genuinely new logic for Phase 99. Low risk (it's a UX/quality tuning question, easily adjusted later), but the planner should treat it as net-new, not "already exists." |

**If this table is empty:** N/A — populated above.

## Open Questions

1. **How deep should the "Karsten math" half of the rubric go?**
   - What we know: `KarstenManabase` exposes scalar land-target formulas (`SingletonLandTarget`, `CedhLandTarget`) and a per-spell consistency/sources-needed pair (`ConsistencyThreshold`, `CastConsistency`, `SourcesNeeded`). The fused profile already stores `karsten:target_lands`/`karsten:land_delta`/`karsten:health_score` as scalar `MeasuredMetric`/`FusedTarget` values from the creator's own decks.
   - What's unclear: whether CS-27's "Karsten math" means (a) comparing the submitted deck's own computed land target against the creator's fused target-land value (cheap, scalar), or (b) running a fuller per-color castability check against the submitted deck's actual mana sources (needs Scryfall-resolved card data, closer to what `ManabaseAnalysisService` already does for the manabase tool).
   - Recommendation: default to (a) for Phase 99 scope — it's a direct, cheap reuse of already-computed fused values and matches the phase's "no LLM, deterministic diff" framing without duplicating the manabase tool's heavier simulation. Confirm with the user at discuss-phase; this is a scope-sizing question, not a technical unknown.

2. **Should `CreatorStylePacketService.BuildAsync` be publicly callable/exposed at all in Phase 99, given there's no controller yet?**
   - What we know: SC #4 requires "xUnit tests cover rubric scoring and artifact assembly in isolation, with no controller or page dependency" — this is satisfiable whether or not the service method is `public`.
   - What's unclear: whether the planner should still wire the DI registration (`PacketServiceCollectionExtensions.cs` or equivalent) this phase, or defer registration entirely to Phase 100 alongside the controller.
   - Recommendation: register the service in DI this phase (with the mandatory tripwire test per Pattern 4) so Phase 100 only has to add a controller/page, not debug a fresh DI graph under time pressure — this mirrors how Phase 98's `ICardGroundingGuard` was registered a full phase before Phase 99 (its only consumer) needed it.

3. **Fail-open vs. fail-closed on upstream Scryfall failure during card-grounding validation (CS-29)?**
   - See Assumptions Log A4 / Pitfall 2. Needs an explicit product decision, not a research answer.

## Environment Availability

Skipped — this phase has no new external dependencies. All required services (Scryfall via existing `IScryfallCardResolver`/`ICardGroundingGuard`, Commander Spellbook via `ICommanderSpellbookService`, SQLite/Postgres via existing `RelationalDatabaseConnection`) are already wired, tested, and running in this codebase from Phases 1-98.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| Config file | None dedicated — standard `dotnet test` via each `.csproj` |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CreatorStyleRubric` (once created) |
| Full suite command | `dotnet build` (clean, per project convention — VSTest unreliable in WSL) then `dotnet test DeckFlow.Core.Tests DeckFlow.Web.Tests` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CS-27 | Rubric scoring produces correct per-metric verdicts from hand-built `FusedTarget`/deck-stat fixtures | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CreatorStyleRubricScorerTests` | ❌ Wave 0 |
| CS-27 | Metric-key join uses the same vocabulary as `StatedMetricKeyMapper`/`ContentTagVocabulary` (Pitfall 1 regression guard) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CreatorStyleRubricScorerTests` | ❌ Wave 0 |
| CS-28 | Artifact assembly includes all 5 required elements given fixture inputs | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStylePacketServiceTests` | ❌ Wave 0 |
| CS-28 | Exemplar selection picks 2-3 decks deterministically from a fixture corpus | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorDeckExemplarSelectorTests` | ❌ Wave 0 |
| CS-29 | No card reaches assembled artifact content without passing `ICardGroundingGuard`; a rejected/upstream-unavailable card is excluded or hard-fails per the chosen policy (Open Question #3) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStylePacketServiceTests` (fake guard returning mixed accept/reject/upstream-failure verdicts) | ❌ Wave 0 |
| SC #4 | No controller/page dependency — tests construct the service directly with fakes, no `WebApplicationFactory`/MVC test host | unit | Same as above | ❌ Wave 0 |
| (regression) | DI graph resolves cleanly (mirrors `CreatorStyleDiRegistrationTests`, Phase 98 precedent) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStyleDiRegistrationTests` | ✅ existing file, needs extending |

### Sampling Rate
- **Per task commit:** targeted `--filter` run on the touched test class(es)
- **Per wave merge:** `dotnet test DeckFlow.Core.Tests DeckFlow.Web.Tests` (full suite; ~1300+ existing Web tests plus new Core tests)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorerTests.cs` — covers CS-27
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs` — covers CS-26, CS-28, CS-29, SC #4
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs` — covers CS-28b
- [ ] Extend existing `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs` to include `CreatorStylePacketService`'s constructor graph
- [ ] No new test framework/config needed — xUnit already fully configured in both test projects

## Security Domain

`security_enforcement` is absent from `.planning/config.json` (treated as enabled per protocol).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | This phase has no controller/endpoint; nothing to authenticate against yet. |
| V3 Session Management | No | No page/session surface this phase. |
| V4 Access Control | No | No endpoint this phase. |
| V5 Input Validation | Yes | Submitted deck text still flows through `IDeckEntryLoader` (existing, already-hardened parser path shared with every other packet tool) — no new untrusted-input surface is introduced, but the rubric/artifact-assembly code must not trust card *names* extracted from the deck as safe for direct string interpolation into the artifact without the same normalization/length-capping conventions `DeckAnalysisPacketService` already applies (e.g. `MaxCompanionNameLength` pattern) if any user-controlled string (deck name, companion name) is echoed into assembled text. |
| V6 Cryptography | No | No new secrets, tokens, or crypto in this phase. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prompt-injection via crafted deck-entry names flowing unescaped into the assembled artifact text | Tampering | Already-established convention: cap/normalize free-text fields before they reach any prompt text (mirrors `MaxCompanionNameLength` + single-line collapse in `DeckAnalysisPacketService`). Apply the same discipline to any user-controlled string this phase touches (deck name, if surfaced) even though there is no page yet — the service method itself should not trust its `request` DTO's string fields as pre-sanitized. |
| Hallucinated/illegal card names shipped in a critique artifact | Spoofing (of a "real" card) | `ICardGroundingGuard` (Phase 98) — this is the entire purpose of CS-29; do not bypass it for "obviously real" cards (e.g. exemplar-deck cards that came from a cached Archidekt import) — cache staleness or a since-banned/rotated card is exactly the class of bug the guard exists to catch. |

## Sources

### Primary (HIGH confidence — read directly from the working tree in this session)
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — `FusedTarget`/`CreatorStyleProfile` shape
- `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` — pure-Core fusion pattern to mirror
- `DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs` — canonical metric-key vocabulary
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — the 11-category vocabulary
- `DeckFlow.Core/Knowledge/CardGrounding/{ICardGroundingGuard,CardGroundingVerdict,CardGroundingDeckContext,CardGroundingBatchResult}.cs` — Phase 98 guard seam
- `DeckFlow.Core/Manabase/KarstenManabase.cs` — deterministic Karsten math
- `DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs` — Web-hosted guard implementation
- `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` — whitelist builder, explicit "Phase 99" code comment
- `DeckFlow.Core/Content/{ICreatorStyleProfileStore,ICreatorDeckCacheStore,CreatorDeckCacheEntry}.cs` — store contracts
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` and `DeckFlow.Web/Services/DeckPrimerPacketService.cs` — mirror-shape reference services
- `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs` — DI registration pattern
- `DeckFlow.Web/Program.cs` (lines ~95-121) — existing store DI registrations, D-14 comment
- `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` — confirms D-14 local-only content-kb.db binding
- `DeckFlow.Web.Tests/Services/CreatorStyle/{CreatorWhitelistPoolBuilderTests,CreatorStyleDiRegistrationTests}.cs` — existing test-double and DI-tripwire patterns
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md` — phase scope, traceability, decision log
- `.planning/phases/97-profile-fusion-conflict-ledger/97-*-SUMMARY.md`, `.planning/phases/98-card-grounding-guard/98-*-SUMMARY.md` — prior-phase completion state and decisions

### Secondary (MEDIUM confidence)
- None — all findings this phase were directly verifiable in the working tree; no WebSearch was needed since every dependency is already implemented in-repo.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every dependency is existing, committed, already-tested code read directly from the working tree.
- Architecture: HIGH — the composition pattern (Core-pure scorer + Web orchestrator) directly mirrors Phase 97's already-shipped `ProfileFusionEngine`/`MeasuredStyleProfileBuilder` split; the packet-service shape directly mirrors `DeckAnalysisPacketService`/`DeckPrimerPacketService`.
- Pitfalls: HIGH for Pitfalls 1, 3, 4, 5 (all directly evidenced in the codebase/docs); MEDIUM for Pitfall 2 (the guard's fail-open precedent is evidenced, but the "correct" policy for Phase 99 is a product decision, not a fact).

**Research date:** 2026-07-19
**Valid until:** No expiry pressure — this phase depends entirely on in-repo code from the same branch, not external/versioned dependencies. Re-verify only if Phase 97/98 code changes before Phase 99 executes.
