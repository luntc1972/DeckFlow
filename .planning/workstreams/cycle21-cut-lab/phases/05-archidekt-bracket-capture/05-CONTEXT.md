# Phase 5: Archidekt Bracket Capture - Context

**Gathered:** 2026-07-29
**Status:** Ready for planning
**Workstream:** `cycle21-cut-lab`

<domain>
## Phase Boundary

Phase 5 captures deck-level metadata already present on the Archidekt deck payload during category harvest. It is a storage and propagation phase: no extra request per deck, no backfill requirement, no commander x bracket floor derivation, and no user-facing bracket analysis in this phase.

The purpose is to make future commander x bracket analysis possible by preserving the data while the deck payload is in hand. Coverage starts from deploy time and grows over future harvests.

Locked scope:
- Capture Archidekt bracket metadata from the already-fetched deck payload.
- Persist metadata on `deck_queue`, because that table already owns per-deck harvest state (`processed`, `skipped`, `last_checked_utc`, `commander_name`, `content_hash`).
- Distinguish rows harvested before Phase 5 from rows harvested after Phase 5 where Archidekt did not declare a bracket.
- Keep Phase 5 non-gating for Cycle 21 Phase 2/3 outcomes.

</domain>

<decisions>
## Implementation Decisions

### Metadata Scope

- **D-01: Persist curated deck-level metadata columns, not raw payload JSON.** The user requested "all available fields" and then selected "Curated columns" as the persistence shape. Planning should identify stable, deck-level Archidekt payload fields that are useful for future corpus hygiene and bracket analysis, then persist them as named columns with explicit tests.
  - Must include the bracket field already identified by Phase 2 research as `edhBracket`.
  - Should include other stable top-level deck metadata when directly useful for future filtering or provenance, such as deck format, theorycrafted status, and created/updated timestamps if present on the payload.
  - Do not persist the full raw payload or arbitrary top-level JSON in this phase. Raw JSON is a larger storage/privacy/review question and is deferred.

- **D-02: `edhBracket` is captured as data, not inferred.** If the payload does not carry a bracket, do not classify the deck locally and write a bracket. Local bracket classification is a separate product capability. Phase 5 records what Archidekt declared.

### Captured vs. Absent Semantics

- **D-03: Use a captured timestamp to distinguish "not captured" from "captured absent."** Add an audit column such as `archidekt_metadata_captured_utc`.
  - `archidekt_metadata_captured_utc IS NULL` means the row predates Phase 5 or has not been metadata-captured yet.
  - `archidekt_metadata_captured_utc IS NOT NULL AND archidekt_edh_bracket IS NULL` means Phase 5 saw the payload and Archidekt did not provide a bracket.
  - This satisfies BRKT-03 without sentinel bracket values.

- **D-04: Nullable metadata columns are the compatibility mechanism.** Existing rows must remain valid after schema migration. No backfill is required for Phase 5 to pass. Any future analysis must explicitly filter to rows with `archidekt_metadata_captured_utc IS NOT NULL` when it needs known capture coverage.

### Write Paths

- **D-05: Both bulk harvest and one-off admin URL import write the same deck metadata.** The user selected "Both paths." Any successful Archidekt deck import that marks a deck processed in `deck_queue` should also persist the curated metadata captured from that same payload.
  - Bulk path: `ArchidektDeckCacheSession.PersistDeckAsync` imports the deck, computes the content hash, persists category rows, and calls `MarkDeckProcessedAsync`.
  - URL path: `AdminHarvestController.SubmitUrl(string url, CancellationToken cancellationToken)` (`[HttpPost("url")]`, `AdminHarvestController.cs:229-231`) imports the deck, persists category rows, and calls `MarkUrlDeckProcessedAsync`.
  - Planning should avoid two independent metadata implementations. Prefer one returned metadata shape from the importer and one repository update surface shared by both paths.

- **D-06: Skip/failure rows do not fabricate metadata.** If deck import fails and the deck is marked skipped, metadata columns stay null unless a payload was successfully parsed before the failure. Failed imports must not write a bracket from partial or guessed state.

### Data Shape and Compatibility

- **D-07: Extend the Archidekt importer with a metadata-bearing result rather than overloading `DeckEntry`.** `DeckEntry` is card-level data. Deck-level metadata belongs in a separate result/record, with the existing `ImportAsync` either preserved for compatibility or adapted through a wrapper.

- **D-08: Content hash remains card-list based.** Capturing deck metadata must not cause category cache rewrites when only metadata changes unless the planner deliberately adds a metadata-only update path. The existing hash is about deck entries/categories, not top-level deck metadata.

### URL-Import Commander Attribution

- **D-09: The URL-import commander-extraction fix is ratified into Phase 5.** `AdminHarvestController.SubmitUrl` selects the commander with `string.Equals(entry.Category, "Commander", ...)` (`AdminHarvestController.cs:269`). That predicate is **unreachable for Archidekt payloads**: `IsBoardCategory` (`AdminHarvestController.cs:152`, applied at `:79`) strips `Commander` out of `Category`, and `ArchidektApiDeckImporter.DetermineBoard` records the commander as `Board = "commander"` (`ArchidektApiDeckImporter.cs:130`). Consequently every URL-imported `deck_queue` row persists `commander_name = NULL` **always**, today. Phase 5 changes the predicate to `entry.Board == "commander"` with ordinal-ignore-case comparison, matching the already-correct bulk path (`ArchidektDeckCacheSession.cs:185-188`). This is a decision, not a suggestion: plan 05-03 implements it and tests it.
  - **No backfill.** Pre-existing URL-imported rows are **not** backfilled and keep `commander_name IS NULL`, mirroring D-04's no-backfill posture. Correct commander attribution for the URL subset begins at Phase 5 deploy time.
  - **Corpus consequence.** Any future commander-grouped corpus query must **not** read the URL-imported subset as a time series: those aggregates filter `commander_name IS NOT NULL` (`DeckQueueRepository.cs:74`, `:101`), so URL-imported decks appear to begin existing at the Phase 5 deploy boundary even though the rows are older.
  - **User-visible effect.** The admin success banner's rendered text changes from `"Harvested deck: N new observations."` to `"Harvested <Commander>: N new observations."` (`AdminHarvestController.cs:286`). The interpolated format string itself is unchanged; only the previously-always-null `commanderName` now resolves.

### Claude's Discretion

- Exact curated column names, provided they are clear and prefixed enough to avoid ambiguity, for example `archidekt_edh_bracket`, `archidekt_deck_format`, `archidekt_theorycrafted`, `archidekt_created_utc`, `archidekt_updated_utc`, and `archidekt_metadata_captured_utc`.
- Whether the repository API takes a dedicated metadata record or individual optional parameters. Prefer the record if more than two fields survive research.
- Whether to add read APIs now. Only add them if needed for verification or existing admin diagnostics; Phase 5 does not need a UI.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/workstreams/cycle21-cut-lab/ROADMAP.md` - Phase 5 "Archidekt Bracket Capture" block; establishes independent, non-gating posture and no-backfill release shape.
- `.planning/workstreams/cycle21-cut-lab/REQUIREMENTS.md` - BRKT-01 through BRKT-03.
- `.planning/workstreams/cycle21-cut-lab/PROJECT.md` - Cycle 21 decision log; Phase 5 is non-gating and exists to preserve future analysis data.

### Research and prior findings
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-08-SUMMARY.md` - Corpus hygiene and Phase 5 rationale; Archidekt bracket capture cannot fill commander x bracket cells this cycle.
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md` - Human-readable research output mentioning `deckFormat`, `edhBracket`, and Phase 5 non-gating arithmetic.
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.json` - Machine-readable run metadata and payload-field observations.

### Code this phase changes or mirrors
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` - Fetches `api/decks/{deckId}/`, parses `cards[]`, currently ignores top-level payload metadata.
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` - Bulk harvest flow; imports decks, writes category observations, content hash, commander name, and processed state.
- `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` - Owns `deck_queue` schema creation/migration across SQLite and Postgres.
- `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` - Owns `deck_queue` writes: `MarkDeckProcessedAsync`, `MarkUrlDeckProcessedAsync`, `SetContentHashAsync`.
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` - Public repository facade around deck queue operations.
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` - One-off admin URL import path that should write the same metadata as bulk harvest.
- `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` and `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs` - Web persistence interface and adapter for URL import metadata propagation.
- `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` and `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` - Schema/index/parity test patterns for SQLite/Postgres.
- `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` - Postgres storage integration coverage patterns, if planner decides metadata needs dialect-specific verification.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ArchidektApiDeckImporter.ImportAsync` already fetches the exact deck payload needed for BRKT-01. It parses only `cards[]`; top-level metadata can be extracted from the same `JsonDocument`.
- `CategoryCacheSchema.EnsureSchemaAsync` already performs additive `ALTER TABLE deck_queue ADD COLUMN ...` migrations after reading table columns.
- `DeckQueueRepository.MarkDeckProcessedAsync` and `MarkUrlDeckProcessedAsync` are the existing single points where processed deck metadata is written.
- `ArchidektDeckCacheSession.PersistDeckAsync` already returns `(DeckCacheWriteResult Result, string? CommanderName)`, making it a natural place to add a metadata result.

### Established Patterns
- Nullable `deck_queue` columns are used for optional, gradually populated deck-level state (`commander_name`, `content_hash`).
- Existing comments document why per-deck metadata is written in the same update that flips `processed = 1`; preserve that style for bracket metadata.
- The category cache supports SQLite and Postgres through one schema class and one repository class; migrations must work for both.

### Integration Points
- Bulk harvest: `RunAsync` -> `PersistDeckAsync` -> `_repository.MarkDeckProcessedAsync(...)`.
- URL import: `AdminHarvestController.SubmitUrl` (`AdminHarvestController.cs:229-231`) -> `_deckImporter.ImportAsync(url)` -> `PersistImportedDeckEntriesAsync` -> `_categoryStore.MarkUrlDeckProcessedAsync(...)`.
- Any importer API change must preserve existing deck loading callers that only need `List<DeckEntry>`, such as `DeckEntryLoader`.

</code_context>

<specifics>
## Specific Ideas

- Candidate metadata record:

  ```csharp
  public sealed record ArchidektDeckMetadata(
      int? EdhBracket,
      int? DeckFormat,
      bool? Theorycrafted,
      DateTimeOffset? CreatedUtc,
      DateTimeOffset? UpdatedUtc,
      DateTimeOffset CapturedUtc);
  ```

- Candidate capture rule:
  - Capture timestamp is set only after a successful payload parse.
  - Bracket nullable value is copied from `edhBracket` when it is numeric or numeric-string; malformed values become null but still count as captured.
  - Do not infer bracket from card contents.

- Candidate DB semantics:
  - `archidekt_metadata_captured_utc` nullable text/timestamptz-compatible value.
  - `archidekt_edh_bracket` nullable integer.
  - Other curated metadata columns nullable.

</specifics>

<deferred>
## Deferred Ideas

- Raw top-level Archidekt payload metadata JSON storage. Useful for future forensic analysis, but it needs a separate storage/privacy review and is outside BRKT-01..03.
- Backfilling existing `deck_queue` rows. Phase 5 explicitly does not require backfill.
- Commander x bracket role-floor derivation. This remains out of scope for Cycle 21 because coverage is too thin and bracket capture only builds prospectively.
- Any user-facing admin report or UI for bracket coverage, unless needed only as lightweight verification.

</deferred>

---

*Phase: 05-archidekt-bracket-capture*
*Context gathered: 2026-07-29*
