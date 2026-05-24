# Phase 2: Doc-Comment Backfill — Part 1 (Controllers + Services) - Context

**Gathered:** 2026-05-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Backfill XML `<summary>` doc-comments on every undocumented public type under
`DeckFlow.Web/Controllers/` (incl. `Controllers/Admin/`, `Controllers/Api/`),
`DeckFlow.Web/Services/` (incl. all subfolders), and `DeckFlow.Web/Services/Http/`.

**Measured scope (2026-05-24 codebase scan):** 118 public types in Controllers +
Services; 73 already documented; **45 undocumented** types are the work.
ROADMAP SC1's anchored grep covers the whole directory tree, so ALL 45 are in
scope regardless of era — the v1.3-era `Harvest/`, `FeatureFlags/`, and
`Analytics/` types are included, not just the original v1.1-era subset.

**NoWarn stays:** `NoWarn 1591;1573;1587` REMAINS in `DeckFlow.Web.csproj`.
The gate is NOT flipped this phase — Phase 8 strips it after the rest of the
v1.4 surface is documented (Pitfall 8 sequencing).

**Out of scope:** `Models/`, `Models/Api/`, `Infrastructure/`, `Security/`,
`ViewModels/` (Phase 8 — Part 2). No `NoWarn` strip. No runtime/behavior change.
No Format Document / reformatting (CLAUDE.md R-6).

</domain>

<decisions>
## Implementation Decisions

### inheritdoc policy (interface/impl pairs)
- **D-01:** Interface owns the prose. The interface type carries full
  `<summary>` (+ `<param>`/`<returns>` per D-02); the implementing class and its
  public members use `<inheritdoc/>`. DRY, .NET-standard, matches the project's
  co-located interface+impl convention. ~18 of the 45 are interface/impl pairs.
- **D-01a:** Standalone classes/records with no interface get full summaries
  written directly (no inheritdoc target exists).

### Method-level depth threshold
- **D-02:** `<param>`/`<returns>` tags are added when:
  - **≥2 *real* params** — a trailing `CancellationToken cancellationToken = default`
    does NOT count toward the "multi-arg" trigger (it is conventional boilerplate;
    documenting it everywhere is the noise CLAUDE.md discourages).
  - **non-obvious return** — add `<returns>` when the return's meaning is not
    self-evident from the method name.
  - Single-arg, self-evident methods get `<summary>` only.
- **D-02a:** This satisfies ROADMAP SC2 (`<param>`+`<returns>` on non-trivial
  public methods) without the literal-2+-args boilerplate explosion.

### Summary source & voice
- **D-03:** Hybrid sourcing. Seed summaries from the existing one-liner
  descriptions in CLAUDE.md's architecture Component-Responsibilities table where
  a type is listed (consistent vocabulary with shipped docs), VERIFY each against
  the actual current code, and write fresh for the many types not in the table
  (records, DTOs, caches, background services). Inline comments explain WHY, not
  what (CLAUDE.md Comments convention).

### Record `<param>` docs (positional DTO records)
- **D-04:** Every record gets a type-level `<summary>`. Add `<param>` tags on the
  record declaration ONLY where a positional field's meaning is not obvious from
  its name (cryptic abbreviations, units, nullable semantics). Self-documenting
  fields (e.g. `Name`, `OracleText`) skip `<param>`. ~9 positional records in scope.

### Claude's Discretion
- Plan splitting / batching of the 45 types (by directory, by count, by
  interface-pair grouping) is left to the planner.
- Exact wording of each summary, within the D-03 sourcing rule.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition & requirements
- `.planning/ROADMAP.md` §"Phase 2" (lines ~107–116) — Goal + 4 Success Criteria,
  including the SC1 anchored grep that must return empty for Controllers+Services.
- `.planning/REQUIREMENTS.md` DOC-01 (line 15) — partial coverage (Controllers +
  Services subset); DOC-02/Phase 8 owns the NoWarn strip.

### Project conventions (binding constraints)
- `CLAUDE.md` §Comments — XML doc-comment style; "explain why, not what"; the
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `NoWarn`
  1591/1573/1587 context.
- `CLAUDE.md` §Architecture → Component Responsibilities table — source for D-03
  summary seeding (one-liner descriptions of many of the in-scope services).
- `CLAUDE.md` §Constraints "Formatting" + R-6 — touch-only-what-you-touch; NO
  Format Document, NO `{ get; init; }`→`{ get; }` mutation, NO `[Attribute]`
  inlining, NO raw-string re-indent, preserve LF endings.

### Cross-phase dependency
- Phase 8 (`.planning/ROADMAP.md` §"Phase 8") — Part 2 + NoWarn strip. Part 1
  must NOT strip NoWarn; Part 8's anchored grep covers the whole Web project.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **CLAUDE.md Component-Responsibilities table** — pre-written one-liners for
  many in-scope services (`AdminFeedbackController`, `FeedbackStore`,
  `EdhTop16Client`, `ResiliencePipelineFactory`, etc.); seed text per D-03.
- **73 already-documented types** in the same dirs — exemplars for house style
  (e.g. `CardLookupService.cs`, `CommanderSpellbookService.cs` summaries).

### Established Patterns
- Interface + implementation + result records co-located in one `.cs` file
  (CLAUDE.md Naming) — drives the D-01 inheritdoc split (prose on interface,
  inheritdoc on co-located impl).
- Records used for immutable DTOs/results with `{ get; init; }` / positional
  params — D-04 governs their param docs; formatting rule forbids mutating
  `{ get; init; }`.

### Integration Points
- `DeckFlow.Web.csproj` `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` — must stay
  untouched this phase (verified by SC3; build stays 0 warn / 0 err Release).

### Undocumented type inventory (45, from 2026-05-24 scan)
Controllers (6): `CommanderController`, `FeedbackController`,
`Admin/AdminFeedbackController` (+`AdminFeedbackListViewModel`),
`Admin/AdminFlagsController`, `Admin/AdminAnalyticsController`,
`Api/SuggestionsApiController`, `Api/ArchidektCacheJobsController`.
Services + subfolders (39): `ArchidektCacheJobService` (interface + 2 records +
class), `CategoryKnowledgeStore` (+`ICategoryKnowledgeStore`), `FeedbackStore`
(+`IFeedbackStore` + `FeedbackRequestContext`), `EdhTop16Client` (interface +
class), `ScryfallDtos.ScryfallCardFace`, `ScryfallRestClientFactory`,
`MechanicLookupResult`, `PacketSessionCache`, `ScryfallSetService` (interface),
`ScryfallTaggerHttpClient` (interface), `ScryfallTaggerLookupService`,
`TaggerSessionCache` (interface), `DeckFlowDatabaseConnectionFactory`,
`Analytics/` (`RequestMetricsBuffer`, `IRequestMetricsStore`,
`RequestMetricsStore`, `RequestMetricsFlusher`, `RequestMetricEvent`),
`FeatureFlags/` (`FeatureFlagStore`, `IFeatureFlagCache`),
`Harvest/` (`HarvestRunStore` + `IHarvestRunStore`, `HarvestRunRow`,
`HarvestScheduleSnapshot`, `HarvestScheduleService`, `HarvestScheduleCache` +
`IHarvestScheduleCache`, `HarvestScheduleStore` + `IHarvestScheduleStore`),
`Http/ResiliencePipelineFactory`.
> Re-run the SC1 anchored grep at plan/execute time — counts are a snapshot.

</code_context>

<specifics>
## Specific Ideas

- Delegation: per CLAUDE.md, Codex writes the doc-comments; Claude reviews.
  PLAN.md should be Codex-executable and route through `/gsd-review` before execute.
- Verification is grep-based + build-based, not behavioral — there is no runtime
  change to UAT. SC1 anchored grep empty + `dotnet build -c Release` 0/0 + test
  suite `Failed:0` is the gate.

</specifics>

<deferred>
## Deferred Ideas

- `Models/`, `Models/Api/`, `Infrastructure/`, `Security/`, `ViewModels/`
  doc-comments + the `NoWarn` strip — Phase 8 (Part 2). Explicitly out of Part 1.

</deferred>

---

*Phase: 2-doc-comment-backfill-part-1-controllers-services*
*Context gathered: 2026-05-24*
