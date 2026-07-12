# Phase 97: Profile Fusion + Conflict Ledger - Research

**Researched:** 2026-07-12
**Domain:** Deterministic C# rubric fusion (Core-only) + read-only Studio Blazor ledger view
**Confidence:** MEDIUM — the additive-extension shape and Studio pattern are HIGH confidence
(read directly from shipped code); the exact join mechanics and coverage-floor semantics
surfaced a real gap between CONTEXT.md's assumption and the actual P95 output shape (see
"Key Findings" and "Common Pitfalls" — MEDIUM/LOW confidence there, flagged for the planner).

## Summary

Phase 97 is a pure-`DeckFlow.Core` reconciliation step: read the P95 measured leg
(`CreatorStyleProfile.MeasuredMetrics`, persisted via `CreatorStyleProfileStore`) and the P96
stated leg (`content_stated_rules` rows / `StatedRuleCandidate`), join them, resolve a winner
per metric, and write the result additively onto `CreatorStyleProfile.FusedTargets`. A new
read-only Studio Blazor page renders the result.

Three load-bearing facts emerged from reading the actual shipped code (not just CONTEXT.md's
paraphrase) that change how the planner should scope this phase:

1. **The stated↔measured metric-key join is NOT a literal string match**, despite CONTEXT.md's
   D-08 claim ("join on those exact keys"). Measured emits `category_ratio:{category}` for the
   11 card categories but stated emits the bare category name (`ramp`, not `category_ratio:ramp`).
   Only `karsten:target_lands`, `karsten:land_delta`, `karsten:health_score`, and
   `combo_density:included_per_deck` match as exact strings. A normalization/mapping function is
   required before any join — see "Key Findings" #1.
2. **There is no read path for stated rules yet.** `ContentArtifactSpec.SerializeStatedRules`
   and `IContentVideoStore.InsertStatedRuleAsync` are write-only; nothing queries
   `content_stated_rules` by creator slug. The planner must add this (a DB query, not a markdown
   re-parse — the DB table already holds the structured fields).
3. **`MetricDistribution.EffectiveSampleSize` (and `MeasuredMetric.NumDecks`) are per-PROFILE
   scalars, not per-METRIC coverage fractions.** Every metric in a given creator's profile gets
   the *same* `EffectiveSampleSize` value (computed once from the whole weighted deck sample).
   D-05's premise — "draw labeled on ~28% of decks, wipes ~3%" as a per-metric gate — cannot be
   reproduced from the current P95 output as literally worded. See "Key Findings" #3 for the two
   viable interpretations and a recommendation.

None of this blocks the phase — all three are resolvable with either a translation layer (finding
1), a new but small additive read method (finding 2), or a documented scope decision (finding 3).
But a planner who takes CONTEXT.md's D-08/D-05 wording at face value and writes tasks assuming a
literal string join and a real per-metric coverage percentage will hit avoidable rework mid-plan.

**Primary recommendation:** Build fusion as `DeckFlow.Core.Knowledge.ProfileFusion` (new,
static/pure), consuming `IReadOnlyList<MeasuredMetric>` + `IReadOnlyList<StatedRuleCandidate>` and
producing `IReadOnlyList<FusedTarget>`; wire a **stated-metric-to-measured-key mapper** (finding
1) and a **profile-level coverage gate** (finding 3, option (a) below) as two small, independently
testable pure functions inside it. Add a `GetStatedRulesBySourceSlugAsync`-shaped read method to
`IContentVideoStore`/`ContentVideoStore` (finding 2). Extend `FusedTarget`/`FusedConflict`
additively per the exact shape in "Code Examples". Add the Studio page mirroring
`CreatorSources.razor`, and register `ICreatorStyleProfileStore` in `DeckFlow.Studio/Program.cs`
(currently NOT registered there — see "Common Pitfalls"). D-03's isolated harvest+distill
confirmation run is **not executable in this environment** (yt-dlp/ffmpeg/whisper absent from
PATH, verified live) — see "Key Questions #7" for how to scope this honestly in the plan.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Stated-rule recency collapse (D-09) | Database/Storage (read) + Core (logic) | — | Reads `content_stated_rules` (DB), collapses in pure Core code — no Web/HTTP involved |
| Metric-key join + classification (D-06/D-08) | Core (pure logic) | — | Deterministic, no I/O; must be unit-testable in isolation |
| Conflict math + coverage gate (D-04/D-05) | Core (pure logic) | — | Same — falsifiable rubric, CS-20 |
| Fused profile persistence | Database/Storage | Core (call site) | `CreatorStyleProfileStore.UpsertAsync`, already dialect-guarded |
| Fusion trigger/orchestration (who calls the pure function and persists) | Not yet assigned — planner must pick | CLI (recommended) or Studio button | No existing wiring calls `ProfileFusion`; see "Key Findings" #4 |
| Say-vs-do ledger view | Frontend Server (Studio Blazor, server-rendered, loopback-only) | — | Studio is not a browser/CDN/public-API tier — it is a local operator tool; D-11 explicitly excludes public/theme/mobile obligations |

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** A live Snail re-distill is NOT feasible cheaply this session — confirmed independently
  in this research (see Key Questions #7): yt-dlp, ffmpeg, whisper, ffprobe are all absent from
  PATH in this environment.
- **D-02:** Ground fusion thresholds/weighting on `docs/research/p89-p90-prototype-snail.md` (39
  decks, 27 stated rules) — the real say-vs-do table (land 37-42 vs avg 37.4 ✅; ramp 7-12 vs avg
  12.0 ✅; draw 13-18 vs avg 11.1 ⚠; wipes 3-5 vs ~1.2/deck ✅-philosophy; counters ≥8 vs
  control-only ⚠).
- **D-03 (⚠ plan-phase gate — MANDATORY):** Plan-phase MUST add an isolated harvest+distill
  pre-step (temp DB, `DECKFLOW_LLM_PROVIDER=claude`) to confirm the shipped P96 prompts reproduce
  the ~27 prototype rules before the executor locks final fusion numbers. Do NOT mutate the live
  studio corpus. **Research finding: the harvest tooling this pre-step depends on is not present
  in this session's PATH — see Key Questions #7 for how the plan should scope this honestly.**
- **D-04:** Conflict form = band-relative % beyond the stated band edge (scale-free across
  metric magnitudes: lands ~40, tutors ~3). Exact X locked at plan/executor time.
- **D-05:** A coverage floor gates whether a conflict may fire at all — reuse
  `MetricDistribution.EffectiveSampleSize`. Below floor → `insufficient-measured`, never a
  conflict. **Research finding: `EffectiveSampleSize` is currently a per-profile, not per-metric,
  scalar — see Key Findings #3 before implementing this literally.**
- **D-06:** Weight assignment = hard partition by metric key (CS-17). Observable → resolved =
  measured (stated band kept as guard/ledger reference). Philosophy/stated-only (no P95
  counterpart) → resolved = stated, never a conflict. No blended numbers.
- **D-07:** `FusedTarget` retains everything, additively (CI-1): stated band, measured value +
  numDecks/coverage/distribution, resolved target, weight, source, populated conflict payload.
  Additive-only — do not break the P94 round-trip tests (named explicitly in "Key Findings" #2 /
  "Code Examples" below).
- **D-08:** Fusion join keys on `(metric, condition)`, never `metric` alone (CS-16a, "highest-risk
  modeling decision this cycle"). **Research finding: the metric half of the key requires a
  translation layer, not a literal string match — see Key Findings #1.**
- **D-09:** Recency-collapse stated rules BEFORE fusion — same `(metric, condition)` keeps newest
  by `video_date`, superseded rule stays visible in the ledger as history (not an active target).
- **D-10:** `confidence` is a coarse band (low/med/high), informational only, does not scale the
  fused number.
- **D-11:** CS-19 ledger renders as a new read-only Studio Blazor page, neighbor to
  `CreatorSources.razor`/`Harvest`/`Publish`. Studio is loopback-only — no public/theme/mobile
  surface obligations. Fused-profile data is local tooling data, not synced to the deployed app
  this phase.
- **D-12:** Each ledger row = full say-vs-do row per `(metric, condition)`: stated band · measured
  value + numDecks/coverage · resolved target · verdict badge (`agree` / `conflict` /
  `insufficient-measured` / `philosophy-stated-only`) · source-clip link + `video_date`.

### Claude's Discretion

- Exact numeric X for the band-relative % (D-04) and the coverage-floor value (D-05) — empirical,
  locked at plan/executor time against the D-02 prototype table + D-03 confirmation run (or its
  documented-manual-verification substitute — see Key Questions #7).
- The precise additive field names/shape on `FusedTarget` (D-07) — planner's call, subject to the
  additive-only / round-trip-preserving constraint. A concrete starting shape is proposed in "Code
  Examples" below.
- Studio page layout details beyond the D-12 row contract.

### Deferred Ideas (OUT OF SCOPE)

- Mass corpus backfill (re-distill all ~106 artifacts to populate `stated_rules:`) — operator-
  driven, deferred per P96 D-05; P97 uses the single confirmed Snail profile.
- Syncing fused profiles to the deployed app — no consumer until P99/P100; ledger stays
  Studio-local this phase.
- Card-level grounding of stated rules — Phase 98's guard, not fusion's job.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CS-16 | For each metric, compute conflict = measured outside stated band by threshold; record both numbers | "Code Examples" additive `FusedConflict` shape; D-02 prototype table gives calibration targets |
| CS-16a | Conditionality first-class — rules per-archetype/curve; ledger carries `applies_when`/join must not emit false deltas | `StatedRuleCandidate.Condition` already exists (P96); Key Findings #1 covers the metric-half of the join key that must accompany it |
| CS-17 | Weight toward measured for observables, toward stated only for un-measurable philosophy | `StatedRulesMetricVocabulary.Metrics` already partitions cleanly: 11 `CardCategories` + `karsten:*`/`combo_density:*` (observable) vs `land_count`/`interaction`/`opener_probability`/`pip_distribution`/`power_level_philosophy` (stated-only, no P95 counterpart) — see Key Questions #3 |
| CS-18 | Encode fused profile as weighted numeric targets, not prose | `FusedTarget.Value`/`Weight` already numeric; no prose field exists or should be added |
| CS-19 | Conflict ledger surfaced in Studio/admin | `CreatorSources.razor` pattern is the concrete template (Code Examples) |
| CS-20 | Pure-Core, fully unit-tested | `ProfileFusion` must live in `DeckFlow.Core/Knowledge/`, zero Web/HTTP refs, xUnit tests (project convention) |

## Standard Stack

No new packages required. This phase composes exclusively existing in-repo primitives:

| Component | Location | Purpose |
|-----------|----------|---------|
| `CreatorStyleProfile` / `FusedTarget` / `FusedConflict` / `MeasuredMetric` / `MetricDistribution` / `StatedRule` | `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` | P94 substrate to extend additively |
| `StatedRuleCandidate` | `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleCandidate.cs` | P96 stated-rule shape fusion consumes |
| `StatedRulesMetricVocabulary` | `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesMetricVocabulary.cs` | Closed 20-key stated metric vocabulary + comparator vocabulary |
| `ContentTagVocabulary.CardCategories` | `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` | The 11 observable category names (`ramp`, `removal`, `draw`, `finishers`, `win-cons`, `counter`, `protection`, `board-wipe`, `tutor`, `recursion`, `utility`) |
| `CreatorStyleProfileStore` | `DeckFlow.Core/Content/CreatorStyleProfileStore.cs` | Dialect-guarded persistence, JSON-section columns |
| `IContentVideoStore` / `ContentVideoStore` | `DeckFlow.Core/Content/{IContentVideoStore,ContentVideoStore}.cs` | Owns `content_stated_rules` (insert-only today — needs a read method added) |
| `MeasuredStyleProfileBuilder` | `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` | Emits the actual `MeasuredMetric[]` vocabulary fusion joins against |
| `CreatorSources.razor` + `StudioCancellableComponentBase` | `DeckFlow.Studio/Pages/`, `DeckFlow.Studio/StudioCancellableComponentBase.cs` | Studio page pattern template for D-11 |

**Version verification:** N/A — no external package versions involved; every dependency above is
already present and compiling in this solution (verified by direct file read, not `npm view`/`pip
index`-style registry check, since none applies here).

## Package Legitimacy Audit

**Not applicable this phase.** No new external packages (NuGet, npm, or otherwise) are needed —
fusion composes only existing in-repo Core types and existing Dapper/ADO.NET storage patterns.
Per CLAUDE.md, any new dependency the planner later discovers a need for requires explicit user
approval before being added; none is anticipated here.

## Architecture Patterns

### System Architecture Diagram

```
 P95 (measured)                    P96 (stated)
 CreatorStyleProfile.MeasuredMetrics    content_stated_rules (DB, by video_id -> source_id)
   [category_ratio:*, lift:*,              [category, metric, value/min/max, comparator,
    combo_density:*, karsten:*]              condition, clip_ts, source_clip, confidence,
        |  (already persisted,               card_reference, card_grounded, video_date_utc]
        |   read via                                |
        |   CreatorStyleProfileStore                |  NEW: read method needed
        |   .GetBySlugAsync(slug))                   |  (join content_videos -> content_sources
        |                                             |   by source_slug == creator slug)
        v                                             v
   +----------------------------------------------------------------+
   |               ProfileFusion (NEW, DeckFlow.Core, pure)          |
   |  1. Recency-collapse stated rules by (metric, condition),       |
   |     newest video_date wins; superseded kept as ledger history   |
   |     (D-09)                                                      |
   |  2. Map each stated Metric -> measured MeasuredMetric.Metric    |
   |     key (translation layer, NOT literal match -- Key Finding 1) |
   |  3. Classify metric key: observable vs philosophy (D-06/CS-17)  |
   |  4. Join on (mapped-metric, condition) (D-08/CS-16a)            |
   |  5. Compute band-relative-% conflict + coverage gate (D-04/D-05)|
   |  6. Resolve target: measured (observable) or stated (philosophy)|
   |  7. Emit verdict: agree | conflict | insufficient-measured |    |
   |     philosophy-stated-only (D-12)                               |
   +----------------------------------------------------------------+
        |
        v  FusedTarget[] (additively extended, D-07)
   CreatorStyleProfileStore.UpsertAsync(profile with FusedTargets=...)
        |
        v
   Studio ledger page (NEW, read-only)
   CreatorStyleProfileStore.GetBySlugAsync(slug) -> render FusedTargets as D-12 rows
```

A reader can trace: measured metrics (already in the DB) + stated rules (DB, once a read method
exists) enter `ProfileFusion`, get joined/classified/scored, and the result persists back onto the
same `CreatorStyleProfile` row the Studio page reads.

### Recommended Project Structure

```
DeckFlow.Core/Knowledge/
├── CreatorStyleProfile.cs           # EXTEND: FusedTarget/FusedConflict additive fields
├── ProfileFusion/                   # NEW namespace, mirrors StatedRulesExtraction/ sibling style
│   ├── ProfileFusionEngine.cs       # pure Fuse(measured, statedRules) -> FusedTarget[]
│   ├── StatedMetricKeyMapper.cs     # pure: stated Metric -> measured MeasuredMetric.Metric
│   ├── MetricClassification.cs      # pure: observable vs philosophy static map
│   ├── ConflictCalculator.cs        # pure: band-relative-% + coverage gate -> verdict
│   └── StatedRuleRecencyCollapser.cs # pure: D-09 collapse, keeps superseded as history
DeckFlow.Core/Content/
├── ContentVideoStore.cs             # EXTEND: add GetStatedRulesBySourceSlugAsync (or similar)
├── IContentVideoStore.cs            # EXTEND: interface addition, mirrors existing pattern
DeckFlow.Studio/
├── Program.cs                       # ADD: ICreatorStyleProfileStore registration (currently ABSENT)
├── Pages/CreatorStyleLedger.razor   # NEW: read-only page, mirrors CreatorSources.razor
├── Shared/NavMenu.razor             # ADD: nav link (mirrors existing NavLink pattern)
DeckFlow.CLI/                        # (recommended trigger point, see Key Findings #4)
├── Program.cs                       # possible new "fuse-profile" command, mirrors "distill" pattern
```

### Pattern 1: Additive record extension preserving record equality round-trips

**What:** Add new `init`-only properties (nullable, defaulted) to an existing `sealed record` that
is (a) persisted as a JSON-serialized list column via `System.Text.Json`, and (b) asserted for
equality via `Assert.Equal(expected, actual)` (full record equality) in existing tests.

**When to use:** Exactly the P94 `FusedTarget`/`FusedConflict` situation — the store round-trips
whole objects through `JsonSerializer.Serialize`/`Deserialize<T[]>` with no custom converters
(`CreatorStyleProfileSections.SerializeSection<T>`/`DeserializeSection<T>` in
`DeckFlow.Core/Knowledge/CreatorStyleProfileSections.cs`), and the test data builder
(`CreatorStyleProfileTestData.CreateFullProfile`) never sets the new properties — so both
`expected` and `actual` get the same default (`null`) on both sides of every existing assertion.
This is why the extension is safe: no test needs to change, because `System.Text.Json` handles
new nullable properties by round-tripping `null` transparently.

**Example:**
```csharp
// Source: DeckFlow.Core/Knowledge/CreatorStyleProfile.cs (existing shape, lines 81-97)
public sealed record FusedTarget
{
    public required string Metric { get; init; }
    public required double Value { get; init; }
    public required double Weight { get; init; }
    public required string Source { get; init; }
    public FusedConflict? Conflict { get; init; }

    // --- ADDITIVE (D-07) — proposed starting shape, planner's call on exact names ---
    /// <summary>Optional conditional scope this fused target applies under (D-08 join key half 2).</summary>
    public string? Condition { get; init; }

    /// <summary>Stated band lower bound, or null when the stated rule is a single-value/one-sided comparator.</summary>
    public double? StatedMin { get; init; }

    /// <summary>Stated band upper bound, or null when the stated rule is a single-value/one-sided comparator.</summary>
    public double? StatedMax { get; init; }

    /// <summary>Raw measured value before D-06 resolution (kept for the ledger even when Source == "stated").</summary>
    public double? MeasuredValue { get; init; }

    /// <summary>Measured leg's raw deck count (profile-level today — see research Key Findings #3).</summary>
    public int? NumDecks { get; init; }

    /// <summary>Measured leg's coverage/effective-sample-size signal reused for the D-05 floor.</summary>
    public double? EffectiveSampleSize { get; init; }

    /// <summary>D-12 verdict badge: agree | conflict | insufficient-measured | philosophy-stated-only.</summary>
    public string? Verdict { get; init; }

    /// <summary>Stated rule provenance for the ledger's source-clip link.</summary>
    public string? SourceClip { get; init; }

    /// <summary>Stated rule's source video publish date (D-09 recency).</summary>
    public DateTimeOffset? VideoDateUtc { get; init; }

    /// <summary>Coarse confidence band (D-10) — informational, never scales Value.</summary>
    public string? Confidence { get; init; }
}

// FusedConflict additive extension
public sealed record FusedConflict
{
    public required double StatedValue { get; init; }
    public required double MeasuredValue { get; init; }
    public required double Delta { get; init; }

    // --- ADDITIVE ---
    /// <summary>The computed band-relative-% (D-04) that triggered this conflict.</summary>
    public double? BandRelativePercent { get; init; }

    /// <summary>Which leg "won" the resolution: "measured" | "stated" (D-06).</summary>
    public string? Winner { get; init; }
}
```

**Tests that lock the current shape and MUST stay green (all in `DeckFlow.Core.Tests`):**
- `CreatorStyleProfileStoreTests.UpsertAsync_ThenGetBySlug_RoundTripsFullShape` (SQLite)
- `CreatorStyleProfileStorePostgresTests.UpsertAsync_ThenGetBySlug_RoundTripsFullShape_OnPostgres` (Postgres, `[PostgresFact]`-gated)
- `CreatorStyleProfileStoreTests.UpsertAsync_FusedOnly_EmptySectionsReadBackEmptyNotNull` /
  `UpsertAsync_MeasuredOnly_...` / `UpsertAsync_StatedOnly_...`
- `CreatorStyleProfileTestData.AssertProfilesEqual` (the shared helper doing
  `Assert.Equal(expected.FusedTargets[0], actual.FusedTargets[0])` — full record equality)

All of these construct `FusedTarget`/`FusedConflict` via `CreatorStyleProfileTestData.CreateFullProfile`
without setting any new field, so both sides default to `null` and the additive fields never
break equality.

### Pattern 2: Studio read-only page (D-11)

**What:** A Blazor Server page in `DeckFlow.Studio/Pages/` that injects a store, loads data in
`OnInitializedAsync`, and renders a table — no write path.

**Example (concrete template, based on `CreatorSources.razor` lines 1-6, 101-144):**
```razor
@page "/creator-style-ledger"
@inherits StudioCancellableComponentBase
@using DeckFlow.Core.Content
@using DeckFlow.Core.Knowledge

<PageTitle>Creator Style Ledger</PageTitle>
<h1 class="h4 fw-semibold">Creator Style Ledger</h1>

@code {
    [Inject]
    private ICreatorStyleProfileStore ProfileStore { get; set; } = default!;

    private CreatorStyleProfile? _profile;
    private bool _loading = true;
    private string _error = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Why: Task.Run moves the store call off the Blazor sync context (existing convention).
            _profile = await Task.Run(() => ProfileStore.GetBySlugAsync("salubrioussnail", Cts.Token), Cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            _error = "Could not load the fused profile. Try again.";
        }
        finally
        {
            _loading = false;
            await SafeStateHasChangedAsync();
        }
    }
}
```

`ICreatorStyleProfileStore` is **not currently registered** in `DeckFlow.Studio/Program.cs` — the
planner must add `builder.Services.AddSingleton<ICreatorStyleProfileStore>(_ => new
CreatorStyleProfileStore(contentKbDatabasePath));` alongside the existing `IContentVideoStore` /
`IContentSourceStore` registrations at lines 90-91.

Studio pages register navigation in `DeckFlow.Studio/Shared/NavMenu.razor` — a new `<NavLink>`
entry following the existing 10-item list pattern is needed (D-11 mentions no specific nav
section; "Pipeline" or a new "Creator Style" section both fit the existing header convention).

**Loopback-only confirmation:** Studio is a local Blazor Server app run via `dotnet run
--project DeckFlow.Studio` with no theme system, no mobile viewport testing, and no public route —
it is not part of `DeckFlow.Web`'s deployed surface. The "web-page change -> tests+themes+mobile"
project rule does not apply to Studio pages (consistent with how `Reconcile.razor`, `DirectPush.razor`,
etc. were built in prior phases with only xUnit coverage on their view-model coordinators, not
Playwright).

### Anti-Patterns to Avoid

- **Parsing the `stated_rules:` YAML/JSON frontmatter out of the markdown artifact file at fusion
  time.** The structured data already lives in `content_stated_rules` (DB), inserted by
  `IContentVideoStore.InsertStatedRuleAsync` during distill. Re-parsing markdown would duplicate a
  parser that doesn't exist yet (`ContentArtifactSpec` only has `SerializeStatedRules`, never a
  matching deserializer) and would be a second, drifting source of truth. Add a DB read method
  instead.
- **Assuming `EffectiveSampleSize` differs per metric within one profile.** It does not, today
  (see Key Findings #3). Do not write a conflict-detection algorithm that assumes wipe-specific
  vs. land-specific coverage percentages exist in the current data — they don't, unless P95's
  `MeasuredStyleProfileBuilder` is also extended (out of this phase's stated blast radius).
- **Literal string-matching `StatedRuleCandidate.Metric` against `MeasuredMetric.Metric`.** Only
  4 of 20 stated vocabulary keys match a measured key verbatim (see Key Findings #1). A naive
  `.Equals()` join will silently produce zero fused rows for the 11 category-based metrics — the
  most important half of the ledger (ramp/removal/draw/wipes/counters/etc.).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dialect-guarded persistence | A new store/table for fused targets | `CreatorStyleProfileStore.UpsertAsync` (already stores `FusedTargets` as a JSON column) | Already exists, already dialect-guarded (SQLite + Postgres), already round-trip tested |
| Card-name validation inside fusion | A second Scryfall-fuzzy-lookup call in fusion | Nothing — P96 already ground card names inside stated rules (`card_grounded`); P98 owns the full guard for downstream artifact use | Fusion should stay a pure numeric join; grounding is explicitly out of scope (Deferred Ideas) |
| Coarse confidence scale | A new float→enum mapping utility | Treat `StatedRuleCandidate.Confidence` (0.0-1.0 double) as an input to a simple 3-bucket static classifier (low/med/high) if D-10's "coarse band" needs a discrete representation | D-10 says informational only — no need for a dedicated library, a switch expression suffices |

**Key insight:** almost everything this phase needs already exists in the codebase in a
compatible shape (record types, storage, Studio page pattern). The actual net-new engineering
surface is small: two pure-Core join/classification functions, one new store read method, and one
new Blazor page. Resist the temptation to build new infrastructure (a new store, a new markdown
parser, a new confidence library) when an existing seam already covers the need.

## Common Pitfalls

### Pitfall 1: Taking D-08's "join on those exact keys" literally

**What goes wrong:** A fusion join implemented as `measured.Metric == stated.Metric` silently
produces zero matches for `ramp`, `removal`, `draw`, `finishers`, `win-cons`, `counter`,
`protection`, `board-wipe`, `tutor`, `recursion`, `utility` (11 of the 20 closed stated-vocabulary
keys) because measured emits `category_ratio:{category}` for these, not the bare name.

**Why it happens:** CONTEXT.md D-08 asserts "Stated metric vocabulary was aligned to the P95
MeasuredMetric keys on purpose" — true in spirit (P96 D-02a intentionally derived the allowlist
from the P95 keys), but the alignment is a *prefix relationship*, not identity, for the category
metrics. Verified directly: `StatedRulesMetricVocabulary.Metrics` (20 keys) vs. the four
`MeasuredMetric.Metric` prefixes emitted by `MeasuredStyleProfileBuilder`
(`category_ratio:*`, `lift:*` [explicitly excluded from stated vocab], `combo_density:included_per_deck`,
`karsten:target_lands`/`karsten:land_delta`/`karsten:health_score`).

**How to avoid:** Build an explicit `StatedMetricKeyMapper`:
```
stated "ramp"                          -> measured "category_ratio:ramp"   (11 category keys, prefix-mapped)
stated "combo_density:included_per_deck" -> measured "combo_density:included_per_deck" (exact)
stated "karsten:target_lands"/"karsten:land_delta"/"karsten:health_score" -> exact match
stated "land_count"                    -> DERIVED measured value: karsten:target_lands + karsten:land_delta
                                           (= ManabaseReport.ActualLands average; see ManabaseModels.cs:660
                                           LandDelta => ActualLands - TargetLands)
stated "interaction"/"opener_probability"/"pip_distribution"/"power_level_philosophy" -> NO measured
                                           counterpart -> philosophy/stated-only (D-06), never joins
```
This mapping table is itself the concrete, testable artifact CS-16a's "highest-risk modeling
decision" is asking for — write unit tests asserting each of the 20 keys maps (or explicitly does
not map) as above.

**Warning signs:** A fusion test corpus where every category metric (ramp/removal/draw/wipes/
counters — the flagship prototype metrics) comes back `philosophy-stated-only` or simply absent
from `FusedTargets`, while only `karsten:*`/`combo_density:*` fuse correctly.

### Pitfall 2: Treating `EffectiveSampleSize` as a per-metric label-coverage percentage

**What goes wrong:** D-05 says "reuse `MetricDistribution.EffectiveSampleSize`... draw labeled on
~28% of decks, wipes ~3%" as if the field already varies per metric to reflect exactly this. It
does not. `MeasuredStyleProfileBuilder.BuildAsync` computes `effectiveSampleSize` **once** (from
`FolderWeighting.EffectiveSampleSize(weightedSamples)`) and passes the *same scalar* into every
call to `BuildDistribution`/`AverageMetric` for every metric in the profile — category ratios,
lift metrics, combo density, and Karsten metrics alike (see `MeasuredStyleProfileBuilder.cs` lines
122-251). Likewise `MeasuredMetric.NumDecks` is `rawDeckCount`, also a single profile-wide number.

**Why it happens:** the prototype's original framing (`docs/research/p89-p90-prototype-snail.md`,
"Measured side is the weaker leg... Archidekt labels sparse (ramp 44%, draw 28%, wipes 3% of
decks)") describes a genuinely per-metric sparsity signal that P95 does not currently compute or
persist as a distinct field. P95's `BuildCategoryMetrics` already zero-fills (a deck with zero
wipes contributes a `0` to the average, not an exclusion), which is *why* the ~1.2/deck wipes
average is correct in the first place — but there is no companion "fraction of decks with a
nonzero value for this metric" field alongside it.

**How to avoid — two viable interpretations, pick one explicitly in the plan:**
1. **(Recommended, no P95 changes needed)** Treat the coverage floor as a *global profile-level*
   gate: require `EffectiveSampleSize >= CreatorStyleProfile.MinDeckFloor` (reusing the existing
   constant, 5) before ANY conflict may fire for that profile at all. This does not distinguish
   wipes from land count, but it doesn't need to — P95's zero-fill averaging already makes the
   wipes ~1.2/deck number *correct and honest* (it is not an artifact of sparse labeling that
   needs suppressing; it is the true average across all decks including zeros). The "insufficient-
   measured" verdict then only fires for a whole creator profile below the floor, not per-metric.
2. **(Larger blast radius)** Add a genuinely new per-metric coverage field to `MeasuredMetric`
   (e.g., `NonZeroDeckFraction`) — this requires touching the already-shipped/complete P95
   `MeasuredStyleProfileBuilder`, which CONTEXT.md's "Upstream inputs (COMPLETE)" framing does not
   anticipate reopening. Only do this if the plan explicitly re-scopes P95 as touched-again.

**Warning signs:** a conflict-detection implementation that reads `EffectiveSampleSize` per
`MeasuredMetric` expecting it to vary within a single profile and finds every metric reporting the
identical number.

### Pitfall 3: `ICreatorStyleProfileStore` is not registered in Studio's (or Web's) DI container yet

**What goes wrong:** `grep` across `DeckFlow.Studio/Program.cs` and `DeckFlow.Web/Program.cs`
finds **zero** registrations of `ICreatorStyleProfileStore`. It is only ever constructed directly
in tests (`new CreatorStyleProfileStore(...)`). `MeasuredStyleProfileBuilder` (Web, `AddScoped`)
takes it as a constructor dependency but nothing in Web's DI graph supplies it — meaning
`MeasuredStyleProfileBuilder` would throw at first resolution today if anything tried to construct
it (apparently nothing does yet, consistent with "P95 substrate only, no user-visible surface").

**How to avoid:** the plan must explicitly add
`builder.Services.AddSingleton<ICreatorStyleProfileStore>(_ => new
CreatorStyleProfileStore(contentKbDatabasePath));` to `DeckFlow.Studio/Program.cs` (mirroring the
existing `IContentSourceStore`/`IContentVideoStore` registrations at lines 90-91) so the new ledger
page can inject it. Whether Web also needs this registration depends on where the plan puts the
fusion *trigger* (see Key Findings #4) — if fusion runs via the CLI only, Web never needs it this
phase.

### Pitfall 4: `content_stated_rules` has no query method — only `InsertStatedRuleAsync` and a
cascade delete

**What goes wrong:** assuming a `GetStatedRulesAsync`-shaped method already exists because the
table and the insert path do. It doesn't. `IContentVideoStore` (verified by full-file grep) only
exposes `InsertStatedRuleAsync` (write) and the rows are deleted via `ClearDistillOutputAsync`
(part of a `DELETE FROM content_stated_rules WHERE video_id = @videoId` cascade). There is no read
API by video, by source, or by creator slug.

**How to avoid:** add a new read method, e.g. `Task<IReadOnlyList<StatedRuleCandidate>>
GetStatedRulesBySourceSlugAsync(string sourceSlug, CancellationToken)`, joining
`content_stated_rules.video_id -> content_videos.id` and `content_videos.source_id ->
content_sources.id` filtered on `content_sources.source_slug = @sourceSlug` (the same slug
`CreatorStyleProfile.Slug` and `CreatorProfileSourceStore` already use — confirmed both P95's
`CreatorProfileSourceStore.GetBySlugAsync` and `ContentSourceStore`'s `source_slug` column exist
and are the established creator-identity key across this cycle).

## Code Examples

### Deriving "average actual lands" from the P95 Karsten metrics (Key Finding #1's `land_count` mapping)

```csharp
// Source: DeckFlow.Core/Manabase/ManabaseModels.cs:654-660 (existing, unmodified)
public sealed record ManabaseReport
{
    public required int ActualLands { get; init; }
    public required double TargetLands { get; init; }
    public double LandDelta => ActualLands - TargetLands;
    // ...
}
```
`MeasuredStyleProfileBuilder.BuildKarstenMetricsAsync` averages `LandDelta` and `TargetLands`
separately across decks (`karsten:land_delta`, `karsten:target_lands`) but never emits a direct
"average lands played" metric. Since `LandDelta = ActualLands - TargetLands` per deck, the
fusion-time derivation `avgActualLands ≈ karsten:target_lands.Value + karsten:land_delta.Value` is
the closest reconstruction available without re-touching P95 — an approximation (summing two
separately-averaged quantities is not exactly the average of the per-deck sum, but the discrepancy
is second-order for a reasonably-sized sample) that should be documented as such wherever it is
used to reproduce the D-02 prototype's "avg 37.4 lands" comparison.

### The full 20-key stated vocabulary and 4 P95 measured-metric prefixes (ground truth for Key Finding #1)

```csharp
// Source: DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesMetricVocabulary.cs
public static readonly IReadOnlySet<string> Metrics = new HashSet<string>(
    ContentTagVocabulary.CardCategories, StringComparer.OrdinalIgnoreCase)
{
    "karsten:target_lands", "karsten:land_delta", "karsten:health_score",
    "combo_density:included_per_deck",
    "land_count", "interaction", "opener_probability", "pip_distribution",
    "power_level_philosophy",
    // lift:* deliberately excluded — creators state absolute counts, not statistical lift
};

// Source: DeckFlow.Core/Knowledge/ContentTagVocabulary.cs:41-55
public static readonly IReadOnlySet<string> CardCategories = new HashSet<string>(...)
{
    "ramp", "removal", "draw", "finishers", "win-cons", "counter",
    "protection", "board-wipe", "tutor", "recursion", "utility"
};

// Source: DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs (metric-key emission sites)
// BuildCategoryMetrics:  Metric = $"category_ratio:{category}"       (category from Scryfall/repo tags,
//                                                                      NOT strictly limited to CardCategories —
//                                                                      verify at plan time whether
//                                                                      CategoryKnowledgeRepository/Tagger
//                                                                      output is filtered to this closed set)
// BuildLiftMetrics:      Metric = $"lift:{item.CategoryA}|{item.CategoryB}"   (excluded from stated vocab)
// BuildComboDensityMetricAsync: Metric = "combo_density:included_per_deck"    (exact match)
// BuildKarstenMetricsAsync:     Metric = "karsten:land_delta" | "karsten:target_lands" | "karsten:health_score" (exact match)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Fusion is a research prototype (Fable, ad hoc) | Fusion is a locked, deterministic Core algorithm consuming persisted P94/P95/P96 schemas | This phase (P97) | The prototype's discriminating verdicts (agree/conflict/philosophy) become a first-class ledger, not a one-off report |
| Stated rules only existed as free-text summary/clips | Stated rules are structured (`StatedRuleCandidate`) and persisted in `content_stated_rules` | P96 (shipped 2026-07-12) | Fusion can aggregate rather than re-read prose, once a query method is added |

**Deprecated/outdated:** none — this is a brand-new capability with no prior in-repo
implementation to supersede.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `CategoryKnowledgeRepository`/Scryfall-Tagger category labels used by `category_ratio:{category}` are effectively limited to (or should be normalized to) `ContentTagVocabulary.CardCategories`'s 11 names | Code Examples, Pitfall 1 | If the repository/tagger can emit categories outside this closed set, the stated↔measured join needs a broader or fuzzy mapping, not the clean 11-item table shown |
| A2 | Deriving "average actual lands" as `karsten:target_lands + karsten:land_delta` is an acceptable approximation for the `land_count` stated metric | Code Examples | If the approximation drifts meaningfully from a true per-deck average of `ActualLands`, the flagship land-count row in the ledger (the prototype's strongest ✅ case) could show a spurious small conflict; consider exposing `ActualLands` as its own P95 metric if this matters at executor time |
| A3 | Interpretation (1) of Pitfall 2 (global `EffectiveSampleSize`/`MinDeckFloor` gate, not per-metric) is the intended and sufficient reading of D-05 for this phase | Common Pitfalls #2 | If the user actually wants true per-metric label-coverage (matching the prototype's "wipes 3%" framing literally), P95 needs reopening — a larger blast radius than CONTEXT.md's phase boundary implies; surface this explicitly to the user/executor rather than silently picking interpretation (1) |
| A4 | Fusion's trigger/orchestration point (who calls the pure fusion function + persists) is best placed in `DeckFlow.CLI` as a new command, mirroring `distill`/`archidekt-categories` | Key Findings #4 / Architecture | If the user wants a Studio "Recompute" button instead, DI wiring differs (Studio needs a coordinator + button, not just a read-only page) — this is an open question, not a locked decision |

## Open Questions

1. **Where does fusion actually run?**
   - What we know: `ProfileFusion`/equivalent must be pure Core (CS-20). `CreatorStyleProfileStore.UpsertAsync`
     already persists `FusedTargets`. `MeasuredStyleProfileBuilder` (Web) already builds+persists the
     measured leg via the same store. No existing code calls a fusion function or triggers a
     recompute anywhere (CLI, Web, or Studio).
   - What's unclear: whether the plan should add a CLI command (`fuse-profile <slug>`, mirroring
     `distill`), a Studio "Recompute Ledger" button (write action on an otherwise read-only page —
     tension with D-11's "read-only" framing), or treat this as out of scope entirely and assume an
     already-fused row is seeded by a test/manual step for the Studio page to display.
   - Recommendation: a CLI command is the smallest, most convention-consistent addition (matches
     `distill`, `content-index-export`, `harvest` as one-shot operator-triggered commands) and keeps
     the Studio page truly read-only per D-11. Flag this explicitly for user/planner confirmation
     since CONTEXT.md doesn't address it.

2. **Does `category_ratio:{category}` ever emit a label outside the 11 `ContentTagVocabulary.CardCategories`
   names?**
   - What we know: `CreatorDeckCategoryResolver.ResolveAsync` sources categories from
     `CategoryKnowledgeRepository.GetCategoriesAsync` (harvested repository) falling back to
     `IScryfallTaggerLookupService.LookupOracleTagsAsync` (raw Scryfall oracle tags) — neither call
     site visibly filters to the closed 11-item vocabulary in the code read during this research.
   - What's unclear: whether an oracle tag like `"stax"` or `"reanimator"` could produce a
     `category_ratio:stax` measured metric with no stated-vocabulary counterpart at all (silently
     dropped from fusion, which is fine) versus subtly polluting the join if such a tag happens to
     collide with a stated category name in an unexpected way.
   - Recommendation: at implementation time, log/audit the actual distinct `category_ratio:*` keys
     produced for the Snail profile once P95 has run against real data, and confirm they're a subset
     of (or safely superset — extras just don't join — of) the 11-item vocabulary before finalizing
     the mapping table.

3. **Is the D-03 pre-step feasible on ANY machine this team uses, or only unavailable in this
   sandboxed session?**
   - What we know: yt-dlp, ffmpeg, whisper, ffprobe are absent from PATH in this research session
     (verified live via `command -v`). D-01's own reasoning (live studio DB has no Snail transcript,
     in-repo transcript is a 6-line synthetic stub, tooling not on PATH) matches this finding exactly.
   - What's unclear: whether the operator's actual development machine (outside this sandboxed
     research/planning session) has these tools installed — this research cannot determine that.
   - Recommendation: the plan should NOT assume the D-03 confirmation run is executable by an
     agent in this kind of session. Frame it as either (a) a documented manual verification
     checklist the operator runs themselves before the executor locks final numbers, or (b) an
     explicit environment-setup task (install yt-dlp + ffmpeg + confirm `DECKFLOW_LLM_PROVIDER=claude`
     works) gated behind human confirmation, not a task the executor silently attempts and might
     fail on. See Key Questions #7 in the return summary for the full reasoning.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| yt-dlp | D-03 harvest pre-step (transcript download) | ✗ | — | None in this session — D-02 prototype grounding substitutes; D-03 must become a documented manual/operator step |
| ffmpeg | D-03 harvest pre-step (audio extraction for Whisper fallback) | ✗ | — | Same as above |
| ffprobe | D-03 harvest pre-step (media probing) | ✗ | — | Same as above |
| whisper (openai-whisper) | D-03 harvest pre-step (transcript fallback when no captions) | ✗ | — | Same as above |
| .NET 10 SDK / xUnit / Dapper / Npgsql / Microsoft.Data.Sqlite | Fusion logic + persistence + tests | ✓ | per `.csproj` (already used throughout Core) | — |
| Blazor Server (Studio host) | D-11 ledger page | ✓ | already running (`DeckFlow.Studio`) | — |

**Missing dependencies with no fallback:**
- yt-dlp / ffmpeg / whisper / ffprobe — block D-03's automated confirmation run in an agent
  session. The phase itself (fusion logic + Studio page) does NOT depend on these; only the
  optional D-03 calibration-confirmation pre-step does. See Key Questions #7 for the recommended
  scoping (documented manual step, not a plan task the executor attempts to run).

**Missing dependencies with fallback:**
- None — D-02's prototype-grounded numbers are the only fallback for D-03, already locked in
  CONTEXT.md as acceptable interim grounding.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`) |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` (standard xUnit + `xunit.runner.visualstudio` + `coverlet.collector`) |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ProfileFusion"` (once tests exist) |
| Full suite command | `dotnet test DeckFlow.Core.Tests` (SQLite unconditional; `[PostgresFact]`-gated tests skip without a live container) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CS-16 | Conflict computed when measured outside stated band by threshold, both numbers recorded | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ConflictCalculator` | ❌ Wave 0 |
| CS-16a | `(metric, condition)` join never produces a false delta for a conditional rule vs. an unconditional aggregate | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ProfileFusionEngine` | ❌ Wave 0 |
| CS-17 | Observable metrics resolve to measured; philosophy metrics resolve to stated, never conflict | unit | same as above | ❌ Wave 0 |
| CS-18 | Fused profile stays numeric (`Value`/`Weight`), no prose field added | unit (structural assertion) | same as above | ❌ Wave 0 |
| CS-19 | Studio page renders the ledger from a persisted profile | manual / smoke (Blazor page, no Playwright per D-11 loopback-only) | manual verification in running `dotnet run --project DeckFlow.Studio` | ❌ Wave 0 |
| CS-20 | Fusion has zero Web/HTTP references | unit (compile-time / project-reference check) | `dotnet build DeckFlow.Core` with no `DeckFlow.Web`/`RestSharp`/ASP.NET references in the new files | N/A — enforced by project structure, not a runtime test |

### Sampling Rate

- **Per task commit:** `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ProfileFusion|FullyQualifiedName~CreatorStyleProfile"`
- **Per wave merge:** `dotnet test DeckFlow.Core.Tests` (full Core suite; Postgres tests auto-skip without Testcontainers)
- **Phase gate:** Full suite green before `/gsd:verify-work`; additionally confirm `dotnet build` across the whole solution (`DeckFlow.sln`) to catch any Studio DI-registration compile break.

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/ProfileFusion/StatedMetricKeyMapperTests.cs` — covers CS-16a's 20-key mapping table (Key Finding #1)
- [ ] `DeckFlow.Core.Tests/ProfileFusion/ConflictCalculatorTests.cs` — covers CS-16, calibrated against the D-02 prototype table (land/ramp/draw/wipes/counters)
- [ ] `DeckFlow.Core.Tests/ProfileFusion/ProfileFusionEngineTests.cs` — covers CS-16a conditionality + CS-17 partition + CS-20 determinism
- [ ] `DeckFlow.Core.Tests/ProfileFusion/StatedRuleRecencyCollapserTests.cs` — covers D-09
- [ ] `DeckFlow.Core.Tests/ContentVideoStoreStatedRulesReadTests.cs` (or extend existing `ContentVideoStoreDistillTests.cs`) — covers the new read method (Pitfall 4)
- [ ] Framework install: none — xUnit/Dapper/Sqlite/Npgsql already referenced by `DeckFlow.Core.Tests.csproj`

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Studio is loopback-only, no auth surface added or touched this phase |
| V3 Session Management | No | Blazor Server circuit, no new session concept |
| V4 Access Control | No | No new public endpoint; Studio has no BasicAuth gate (unlike `/Admin/*` in Web) because it is not internet-exposed |
| V5 Input Validation | Marginal | The new `GetStatedRulesBySourceSlugAsync`-style method must parameterize the slug (Dapper `DynamicParameters`, matching every existing store method) — no raw SQL string concatenation |
| V6 Cryptography | No | No secrets, no crypto surface |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via creator slug parameter | Tampering | Parameterized Dapper queries (`new { slug }`), matching every existing store in this codebase — never string-concatenate the slug into SQL |

No other threat patterns apply — this phase adds no HTTP endpoint, no user-submitted content
processing, and no cross-boundary data flow beyond a local SQLite/Postgres read/write already
protected by the existing dialect-guarded store pattern.

## Sources

### Primary (HIGH confidence — direct file reads this session)
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — current `FusedTarget`/`FusedConflict`/`MeasuredMetric`/`MetricDistribution`/`StatedRule` shapes
- `DeckFlow.Core/Knowledge/CreatorStyleProfileSections.cs` — JSON section serialize/deserialize helpers proving additive-safety
- `DeckFlow.Core/Content/{CreatorStyleProfileStore,CreatorStyleProfileReadModel,ContentSourceStore,ContentVideoStore,IContentVideoStore}.cs` — persistence + `content_stated_rules` DDL (insert-only confirmed)
- `DeckFlow.Core/Knowledge/StatedRulesExtraction/{StatedRuleCandidate,StatedRulesMetricVocabulary}.cs` — P96 stated-rule shape + closed 20-key vocabulary
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — the 11 `CardCategories` names
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `SerializeStatedRules` (write-only, confirmed no deserializer)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` (lines 654-660) — `ManabaseReport.LandDelta` definition enabling the land-count derivation
- `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` — exact `MeasuredMetric.Metric` key-emission sites, confirming `EffectiveSampleSize`/`NumDecks` are per-profile scalars
- `DeckFlow.Core.Tests/{CreatorStyleProfileTestData,CreatorStyleProfileStoreTests}.cs` + `DeckFlow.Core.Tests/Integration/CreatorStyleProfileStorePostgresTests.cs` — the exact round-trip tests that must stay green
- `DeckFlow.Studio/{Program.cs,StudioComponentBase.cs,StudioCancellableComponentBase.cs,Pages/CreatorSources.razor,Shared/NavMenu.razor}` — Studio page pattern + confirmed absence of `ICreatorStyleProfileStore` registration
- `DeckFlow.CLI/{Program.cs,ContentKbCommandRunners.cs}` — CLI command pattern + `ThrowingTranscriptSource` confirming `distill` requires a pre-harvested transcript
- Live shell check (`command -v yt-dlp/ffmpeg/whisper/ffprobe`) — confirmed all four absent from PATH this session

### Secondary (MEDIUM confidence)
- `docs/research/p89-p90-prototype-snail.md` — the empirical calibration table (39 decks, 27 rules); numbers are real but were produced by a Fable prototype, not this codebase's shipped algorithm
- `.planning/phases/96-stated-rules-distiller/96-CONTEXT.md` — D-02a's "aligned to P95 keys" claim, verified partially true (4/20 exact, 11/20 prefix-mappable, 5/20 stated-only by design)

### Tertiary (LOW confidence)
- None — no unverified web search was needed for this phase; everything required was in-repo.

## Metadata

**Confidence breakdown:**
- Standard stack / additive extension: HIGH — read directly from shipped, tested code
- Architecture / Studio pattern: HIGH — `CreatorSources.razor` is a complete, working template
- Join/classification mechanics (D-08/D-06): MEDIUM — the mapping table is derived correctly from
  code but not yet validated against a real Snail measured-profile run (P95 has shipped code but
  this research did not execute it against live Archidekt data)
- Coverage-floor semantics (D-05): LOW-MEDIUM — genuine ambiguity surfaced between CONTEXT.md's
  wording and the current P95 implementation; flagged as Assumption A3 for explicit user/executor
  confirmation
- D-03 feasibility: HIGH — verified live in this session (tooling absent from PATH)

**Research date:** 2026-07-12
**Valid until:** 14 days (fast-moving milestone; P95/P96 code could still receive follow-up fixes before P97 executes)
