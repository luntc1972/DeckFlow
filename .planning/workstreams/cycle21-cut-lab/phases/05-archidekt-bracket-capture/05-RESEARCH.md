# Phase 05: Archidekt Bracket Capture - Research

**Researched:** 2026-07-29  
**Domain:** Archidekt payload parsing, category-harvest persistence, SQLite/Postgres additive schema migration  
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Capture Archidekt bracket metadata from the already-fetched deck payload. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Persist metadata on `deck_queue`, because that table already owns per-deck harvest state (`processed`, `skipped`, `last_checked_utc`, `commander_name`, `content_hash`). [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Distinguish rows harvested before Phase 5 from rows harvested after Phase 5 where Archidekt did not declare a bracket. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Persist curated deck-level metadata columns, not raw payload JSON. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- `edhBracket` is captured as data, not inferred. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Use a captured timestamp such as `archidekt_metadata_captured_utc` to distinguish "not captured" from "captured absent." [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Existing rows must remain valid after schema migration; no backfill is required. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Both bulk harvest and one-off admin URL import write the same deck metadata. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Skip/failure rows do not fabricate metadata. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Extend the Archidekt importer with a metadata-bearing result rather than overloading `DeckEntry`. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Content hash remains card-list based. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]

### the agent's Discretion
- Exact curated column names, provided they are clear and prefixed enough to avoid ambiguity, for example `archidekt_edh_bracket`, `archidekt_deck_format`, `archidekt_theorycrafted`, `archidekt_created_utc`, `archidekt_updated_utc`, and `archidekt_metadata_captured_utc`. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Whether the repository API takes a dedicated metadata record or individual optional parameters. Prefer the record if more than two fields survive research. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Whether to add read APIs now. Only add them if needed for verification or existing admin diagnostics; Phase 5 does not need a UI. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]

### Deferred Ideas (OUT OF SCOPE)
- Raw top-level Archidekt payload metadata JSON storage. Useful for future forensic analysis, but it needs a separate storage/privacy review and is outside BRKT-01..03. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Backfilling existing `deck_queue` rows. Phase 5 explicitly does not require backfill. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Commander x bracket role-floor derivation. This remains out of scope for Cycle 21 because coverage is too thin and bracket capture only builds prospectively. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- Any user-facing admin report or UI for bracket coverage, unless needed only as lightweight verification. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BRKT-01 | Category harvest parses the bracket field from the Archidekt deck payload already being fetched, with no additional request per deck. | `ArchidektApiDeckImporter.ImportAsync` already fetches `api/decks/{deckId}/` and parses `cards[]`; add metadata parsing against the same `JsonDocument` and expose it through a new metadata-bearing result. [VERIFIED: codebase grep] |
| BRKT-02 | Bracket is persisted as a nullable column so existing harvested rows are unaffected and no backfill is required. | `CategoryCacheSchema.EnsureSchemaAsync` already uses additive `ALTER TABLE deck_queue ADD COLUMN ... TEXT NULL` migrations after table-column discovery; repeat that pattern for nullable metadata columns. [VERIFIED: codebase grep] |
| BRKT-03 | A pre-change row is distinguishable from a post-change row whose bracket was genuinely absent. | Add nullable `archidekt_metadata_captured_utc`; `NULL` means not captured, while non-null plus `archidekt_edh_bracket IS NULL` means captured absent. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
</phase_requirements>

## Summary

Phase 5 is an internal harvest-persistence change, not a product UI or analysis phase. The existing Archidekt importer already has the exact deck payload in memory, and the existing category harvest already has centralized processed-row write points for both bulk and URL import paths. The planner should preserve the existing `Task<List<DeckEntry>> ImportAsync(...)` contract for broad caller compatibility and add an Archidekt-specific metadata-bearing result/overload for harvest paths. [VERIFIED: codebase grep]

The evidenced top-level Archidekt fields are `id`, `name`, `createdAt`, `updatedAt`, `deckFormat`, `edhBracket`, `viewCount`, `theorycrafted`, and `points`; the checked fixture has `deckFormat: 3`, `edhBracket: null`, `theorycrafted: false`, ISO UTC `createdAt`/`updatedAt`, `viewCount: 805`, and `points: 1`. [VERIFIED: codebase grep] Phase 2 findings also record sampled live payload observations for `deckFormat`, `theorycrafted`, `createdAt`, `updatedAt`, `viewCount`, `points`, and `edhBracket`, and state that these are currently unparsed and unstored. [CITED: .planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md]

**Primary recommendation:** Add `ArchidektDeckImportResult(Entries, Metadata)`, keep `ImportAsync` as a compatibility wrapper, and pass one `ArchidektDeckMetadata` record into both `MarkDeckProcessedAsync` and `MarkUrlDeckProcessedAsync` so bulk and URL harvest share the same metadata write semantics. [VERIFIED: codebase grep]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Parse Archidekt deck-level payload metadata | API / Backend | External Archidekt API | The backend importer owns the single request to `api/decks/{deckId}/` and already parses card entries from that payload. [VERIFIED: codebase grep] |
| Persist metadata capture coverage | Database / Storage | API / Backend | `deck_queue` already stores per-deck harvest state and processed markers, so storage owns captured/absent semantics. [VERIFIED: codebase grep] |
| Bulk harvest propagation | API / Backend | Database / Storage | `ArchidektDeckCacheSession` imports, hashes, persists categories, and marks queued decks processed. [VERIFIED: codebase grep] |
| Admin URL import propagation | Frontend Server (MVC) | API / Backend, Database / Storage | `AdminHarvestController.SubmitUrl` imports one Archidekt URL, persists category rows, and calls the web persistence adapter. [VERIFIED: codebase grep] |
| Future commander x bracket filtering | Database / Storage | API / Backend | Future analysis can filter `deck_queue` rows by captured timestamp and bracket without touching raw payloads. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |

## Project Constraints (from AGENTS.md)

No `AGENTS.md` exists in this worktree root, so there are no additional project-level directives from that file. [VERIFIED: codebase grep]

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.302 via `dotnet.exe` | Build and test the C# solution | The repository is a .NET solution with Core/Web/Web.Tests projects; `dotnet.exe` is available in this WSL environment while Linux `dotnet` is not. [VERIFIED: command output] |
| `System.Text.Json` | BCL | Parse Archidekt JSON payloads | `ArchidektApiDeckImporter` already uses `JsonDocument.Parse(body)` and `JsonElement` traversal. [VERIFIED: codebase grep] |
| RestSharp | existing project dependency | Execute Archidekt API requests | `ArchidektApiDeckImporter` already uses injected `RestClient` and `RestRequest`. [VERIFIED: codebase grep] |
| Dapper | existing project dependency | Execute schema and repository SQL | `CategoryCacheSchema` and `DeckQueueRepository` already use Dapper `CommandDefinition`, `QueryAsync`, and `ExecuteAsync`. [VERIFIED: codebase grep] |
| xUnit | 2.9.3 | Unit/integration tests | Test projects reference xUnit 2.9.3 and existing tests are xUnit facts. [VERIFIED: codebase grep] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Testcontainers.PostgreSql | 3.10.0 | Optional Postgres integration coverage | Use only for gated `PostgresFact` tests when Docker and `DECKFLOW_POSTGRES_TESTS=1` are available. [VERIFIED: codebase grep] |
| Microsoft.Data.Sqlite | existing project dependency | Fast SQLite repository verification | Existing repository/schema tests use temporary SQLite databases for most storage behavior. [VERIFIED: codebase grep] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ArchidektDeckImportResult` overload | Add metadata fields to every `DeckEntry` | Rejected because `DeckEntry` is card-level data and the context explicitly says deck-level metadata belongs in a separate result/record. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| Curated nullable columns | Raw JSON payload column | Rejected by locked scope; raw payload persistence is deferred for storage/privacy/review reasons. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| Backfill existing deck rows | Prospective capture only | Rejected by locked scope; nullable columns and captured timestamp let old rows remain valid. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |

**Installation:**
```bash
# No new external packages are required for Phase 5.
```

**Version verification:** `dotnet.exe --version` returned `10.0.302`; test projects reference `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.4`, `Microsoft.NET.Test.Sdk` `17.14.1`, and Web tests reference `Testcontainers.PostgreSql` `3.10.0`. [VERIFIED: command output]

## Architecture Patterns

### System Architecture Diagram

```text
Archidekt deck id/url
        |
        v
ArchidektApiDeckImporter
  GET api/decks/{deckId}/ once
        |
        +--> parse cards[] -> List<DeckEntry>
        |
        +--> parse top-level deck metadata -> ArchidektDeckMetadata
        |
        v
ArchidektDeckImportResult
        |
        +--> Bulk harvest: ArchidektDeckCacheSession.PersistDeckAsync
        |       -> compute card-list content_hash
        |       -> replace category rows only when hash changed
        |       -> MarkDeckProcessedAsync(deckId, commander, metadata)
        |
        +--> Admin URL import: AdminHarvestController.SubmitUrl
                -> persist URL category rows
                -> MarkUrlDeckProcessedAsync(deckId, commander, metadata)
                        |
                        v
                  deck_queue nullable columns
                  archidekt_metadata_captured_utc
                  archidekt_edh_bracket
                  archidekt_deck_format
                  archidekt_theorycrafted
                  archidekt_created_utc
                  archidekt_updated_utc
```

### Recommended Project Structure

```text
DeckFlow.Core/
├── Integration/        # ArchidektDeckMetadata and ArchidektDeckImportResult with importer parsing
└── Knowledge/          # deck_queue schema migration and shared processed-row metadata writes

DeckFlow.Web/
└── Services/Persistence/ # ICategoryKnowledgeStore adapter signature for URL metadata propagation

DeckFlow.Core.Tests/
├── ArchidektApiDeckImporterTests.cs
├── ArchidektDeckCacheSessionTests.cs
├── CategoryKnowledgeRepositoryTests.cs
└── CategoryCacheSchemaParityTests.cs

DeckFlow.Web.Tests/
├── AdminHarvestControllerTests.cs
└── Integration/PostgresStorageTests.cs
```

### Pattern 1: Compatibility Wrapper Around Metadata Result
**What:** Keep `IArchidektDeckImporter.ImportAsync` returning `List<DeckEntry>` and add a new metadata-bearing method such as `ImportWithMetadataAsync`. [VERIFIED: codebase grep]  
**When to use:** Use `ImportAsync` for existing callers that only need entries; use the new method only in harvest paths that write `deck_queue` metadata. [VERIFIED: codebase grep]  
**Example:**
```csharp
public sealed record ArchidektDeckImportResult(
    List<DeckEntry> Entries,
    ArchidektDeckMetadata Metadata);

public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
{
    var result = await ImportWithMetadataAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
    return result.Entries;
}
```
Source: `MoxfieldApiDeckImporter` already uses a richer `ImportWithSourceAsync` pattern while keeping `ImportAsync` as the simple entry-list API. [VERIFIED: codebase grep]

### Pattern 2: Nullable Additive `deck_queue` Columns
**What:** Add nullable columns in the initial `CREATE TABLE IF NOT EXISTS deck_queue` statement and in idempotent `ALTER TABLE ... ADD COLUMN ... NULL` blocks after `GetTableColumnsAsync`. [VERIFIED: codebase grep]  
**When to use:** Use this for every curated metadata field because existing rows must survive without backfill. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Recommended columns:**
```sql
archidekt_edh_bracket INTEGER NULL
archidekt_deck_format INTEGER NULL
archidekt_theorycrafted INTEGER NULL
archidekt_created_utc TEXT NULL
archidekt_updated_utc TEXT NULL
archidekt_metadata_captured_utc TEXT NULL
```
Source: existing schema stores UTC timestamps as `TEXT` and booleans as integer-compatible values across SQLite/Postgres; `processed` and `skipped` are `INTEGER`, and timestamp columns are `TEXT`. [VERIFIED: codebase grep]

### Pattern 3: One Metadata Record Through Both Processed Writers
**What:** Add one `ArchidektDeckMetadata? metadata = null` parameter to `MarkDeckProcessedAsync` and `MarkUrlDeckProcessedAsync` in `DeckQueueRepository`, `CategoryKnowledgeRepository`, `ICategoryKnowledgeStore`, and `CategoryKnowledgeStore`. [VERIFIED: codebase grep]  
**When to use:** Use metadata when import succeeded and payload was parsed; pass null for skip/failure paths. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Example:**
```csharp
await _repository.MarkDeckProcessedAsync(
    deckId,
    commanderName,
    metadata: import.Metadata,
    skip: false,
    cancellationToken: cancellationToken);
```
Source: existing bulk and URL paths already call processed-row methods after successful import and category persistence. [VERIFIED: codebase grep]

### Anti-Patterns to Avoid
- **Adding a second Archidekt request for metadata:** Violates BRKT-01 because the payload is already fetched in `ImportAsync`. [VERIFIED: codebase grep]
- **Putting deck metadata onto `DeckEntry`:** Confuses deck-level payload state with card-level rows and contradicts D-07. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- **Using a sentinel bracket value:** The captured timestamp provides the required three-state semantics without fake bracket data. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- **Tying metadata writes to content-hash changes:** `PersistDeckAsync` returns `Unchanged` before category rewrites, but `RunAsync` still calls `MarkDeckProcessedAsync`, so metadata can be written for unchanged decks without changing the hash contract. [VERIFIED: codebase grep]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Archidekt bracket inference | Local bracket classifier | Copy `edhBracket` only | D-02 says bracket is captured as declared data, not inferred. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| JSON storage for future flexibility | Raw top-level payload archive | Curated columns | Raw JSON is explicitly deferred. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| Dialect-specific migration framework | Separate migration runner | Existing `CategoryCacheSchema.EnsureSchemaAsync` additive column pattern | The current schema owner already handles SQLite/Postgres table creation and column discovery. [VERIFIED: codebase grep] |
| Per-path metadata SQL | Duplicate SQL in controller/session | Shared repository metadata write parameters | Bulk and URL import must share semantics. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |

**Key insight:** This phase is about preserving payload state at the moment it is already available; any inference, backfill, or raw archive turns a narrow provenance capture into a broader product/data-retention decision. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]

## Common Pitfalls

### Pitfall 1: Losing Three-State Semantics
**What goes wrong:** A null bracket is interpreted as "no bracket declared" even for rows harvested before the metadata column existed. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Why it happens:** Only `archidekt_edh_bracket` is stored, with no capture marker. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**How to avoid:** Always write `archidekt_metadata_captured_utc` on successful metadata-bearing imports, even when every optional metadata field is null. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Warning signs:** Tests assert only bracket values and never assert captured-null versus pre-change-null rows. [ASSUMED]

### Pitfall 2: Breaking Existing `List<DeckEntry>` Callers
**What goes wrong:** Changing `ImportAsync` return type forces unrelated callers and fakes to change. [VERIFIED: codebase grep]  
**Why it happens:** `IArchidektDeckImporter` is used by several Web/Core tests and services as an entry-list importer. [VERIFIED: codebase grep]  
**How to avoid:** Add a new metadata method with a default interface implementation if appropriate, or implement it directly on `ArchidektApiDeckImporter` while preserving `ImportAsync`. [VERIFIED: codebase grep]  
**Warning signs:** Large unrelated diffs in deck convert, comparison, meta-gap, or packet tests. [VERIFIED: codebase grep]

### Pitfall 3: Metadata Changes Rewriting Category Facts
**What goes wrong:** A deck whose cards are unchanged but whose top-level metadata changed triggers category-row delete/reinsert. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Why it happens:** Metadata is accidentally included in `DeckCategoryCacheWriter.ComputeCanonicalHash`. [VERIFIED: codebase grep]  
**How to avoid:** Keep `content_hash` card-list based and write metadata in `MarkDeckProcessedAsync` after `PersistDeckAsync` returns. [VERIFIED: codebase grep]  
**Warning signs:** `ContentHashDedupTests.RunAsync_UnchangedDeck_SkipsFactTableWrites` needs expectation changes. [VERIFIED: command output]

### Pitfall 4: Skip Rows Fabricate Capture
**What goes wrong:** Failed imports get `archidekt_metadata_captured_utc` despite no successful payload parse. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Why it happens:** The skip branch calls the same mark-processed method and a default metadata record is created outside the importer. [VERIFIED: codebase grep]  
**How to avoid:** Treat metadata as nullable on the repository API; pass null on failure/skip paths. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]  
**Warning signs:** Tests show skipped decks with a non-null captured timestamp. [ASSUMED]

### Pitfall 5: URL Import Commander Extraction Mismatch
**What goes wrong:** URL import fails to find commander metadata because it checks `entry.Category == "Commander"` while importer moves `Commander` into `Board = "commander"` and strips board categories from `Category`. [VERIFIED: codebase grep]  
**Why it happens:** `AdminHarvestController.SubmitUrl` currently differs from `ArchidektDeckCacheSession`, which uses `Board`. [VERIFIED: codebase grep]  
**How to avoid:** Plan a small correction to extract URL commanders from `Board == "commander"` while touching the URL metadata path, or explicitly test current behavior before changing it. [VERIFIED: codebase grep]

## Code Examples

Verified patterns from existing code:

### Additive Column Migration
```csharp
var deckQueueColumns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
if (!deckQueueColumns.Contains("content_hash"))
{
    var addContentHashCommand = connection.CreateCommand();
    addContentHashCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;";
    await addContentHashCommand.ExecuteNonQueryAsync(cancellationToken);
}
```
Source: `CategoryCacheSchema.EnsureSchemaAsync`. [VERIFIED: codebase grep]

### Compatibility Rich Import Pattern
```csharp
public async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
{
    var entries = await ImportAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
    return new MoxfieldImportResult(entries, MoxfieldImportSource.Direct);
}
```
Source: `IMoxfieldDeckImporter` default method. [VERIFIED: codebase grep]

### Existing Processed-Row Write Point
```csharp
await _repository.MarkDeckProcessedAsync(deckId, commanderName, skip: false, cancellationToken: cancellationToken);
```
Source: `ArchidektDeckCacheSession.RunAsync`. [VERIFIED: codebase grep]

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Parse only `cards[]` from Archidekt deck payload | Parse `cards[]` plus curated top-level metadata from same payload | Phase 5 planned for Cycle 21 | Enables prospective bracket/filter metadata without additional API calls. [VERIFIED: codebase grep] |
| Treat `deck_queue` as processed/skipped/hash/commander only | Add nullable Archidekt metadata columns with capture timestamp | Phase 5 planned for Cycle 21 | Allows future analysis to filter by known capture coverage. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| Phase 2 Postgres corpus has no stored `createdAt`/`updatedAt`/`edhBracket` | Future rows can store declared metadata prospectively | Phase 5 planned for Cycle 21 | Does not backfill Phase 2/3 but improves future corpus hygiene. [CITED: .planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md] |

**Deprecated/outdated:**
- Using only `archidekt_edh_bracket IS NULL` to measure bracket absence is insufficient once legacy rows exist; use `archidekt_metadata_captured_utc` as the capture marker. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Warning signs listed for missing tests and skip-row fabricated capture are predictive rather than directly observed defects. | Common Pitfalls | Low; they guide planner verification, not implementation design. |

## Open Questions

1. **Should `viewCount` and `points` be persisted in Phase 5?**
   - What we know: The fixture and Phase 2 findings evidence `viewCount` and `points` as top-level payload fields. [VERIFIED: codebase grep]
   - What's unclear: The Phase 5 context names `deckFormat`, `theorycrafted`, `createdAt`, `updatedAt`, and `edhBracket` as directly useful examples, but does not require `viewCount` or `points`. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
   - Recommendation: Do not persist `viewCount` or `points` unless the planner adds an explicit corpus-hygiene use case; avoid expanding the column set beyond bracket/filter/provenance needs. [ASSUMED]

2. **Should a read API be added for metadata verification?**
   - What we know: Phase 5 does not need a UI, and read APIs are discretionary only if needed for verification/admin diagnostics. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
   - What's unclear: Tests can verify via direct SQLite/Postgres SQL without adding public read methods. [VERIFIED: codebase grep]
   - Recommendation: Prefer direct test SQL helpers over adding production read APIs unless implementation needs them. [ASSUMED]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| `dotnet.exe` | Build/test .NET solution | Yes | 10.0.302 | Use `dotnet.exe` from WSL. [VERIFIED: command output] |
| `dotnet` | Native Linux build/test command | No | — | Use `dotnet.exe`. [VERIFIED: command output] |
| Docker | Postgres Testcontainers integration tests | No in WSL PATH | — | SQLite tests cover core behavior; Postgres tests auto-skip unless `DECKFLOW_POSTGRES_TESTS=1` and Docker is available. [VERIFIED: command output] |
| `DECKFLOW_POSTGRES_TESTS` | Enables gated Postgres tests | Not set | — | Leave Postgres tests skipped locally or enable Docker Desktop WSL integration. [VERIFIED: command output] |

**Missing dependencies with no fallback:**
- None for planning or SQLite/Core validation. [VERIFIED: command output]

**Missing dependencies with fallback:**
- Docker is missing for local Postgres integration runs; use existing gated `PostgresFact` behavior or run SQLite-focused tests. [VERIFIED: command output]

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1 [VERIFIED: codebase grep] |
| Config file | Test project `.csproj` files; no separate xUnit config found in requested scope. [VERIFIED: codebase grep] |
| Quick run command | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ArchidektApiDeckImporterTests|FullyQualifiedName~ArchidektDeckCacheSessionTests|FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests" --nologo` [VERIFIED: command output] |
| Full suite command | `dotnet.exe test DeckFlow.sln --nologo` [VERIFIED: command output] |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BRKT-01 | `ImportWithMetadataAsync` parses `edhBracket`, `deckFormat`, `theorycrafted`, `createdAt`, `updatedAt` from the same fixture-backed Archidekt response while `ImportAsync` still returns the same entries. | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektApiDeckImporterTests --nologo` | ✅ extend existing |
| BRKT-01 | Bulk harvest calls the deck importer once per deck and metadata is carried to the processed-row write without extra importer/API calls. | integration/unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektDeckCacheSessionTests --nologo` | ✅ extend existing |
| BRKT-02 | Fresh SQLite schema creates nullable metadata columns and old SQLite schema gains them idempotently. | integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~CategoryCacheSchemaParityTests --nologo` | ✅ extend existing |
| BRKT-02 | Postgres schema/write path accepts nullable metadata columns. | integration gated | `DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter FullyQualifiedName~PostgresStorageTests --nologo` | ✅ extend existing |
| BRKT-03 | Old rows have `archidekt_metadata_captured_utc IS NULL`; post-change captured rows with absent bracket have non-null captured timestamp and null bracket. | integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~CategoryKnowledgeRepositoryTests --nologo` | ✅ extend existing |
| BRKT-03 | URL import writes the same metadata semantics as bulk import. | unit/controller | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter FullyQualifiedName~AdminHarvestControllerTests --nologo` | ✅ extend existing |

### Sampling Rate
- **Per task commit:** Run the narrow Core/Web filters above for touched components. [ASSUMED]
- **Per wave merge:** Run `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --nologo` and `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --nologo`. [ASSUMED]
- **Phase gate:** Full solution green before `$gsd-verify-work`; Postgres integration either run with Docker enabled or explicitly recorded as skipped by environment. [ASSUMED]

### Wave 0 Gaps
- [ ] Extend `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs` with metadata-bearing import assertions; current tests only cover entries. [VERIFIED: codebase grep]
- [ ] Extend `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` or `CategoryKnowledgeRepositoryTests.cs` with metadata-column and captured-vs-absent assertions. [VERIFIED: codebase grep]
- [ ] Extend `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs` fake importer to expose metadata when testing bulk propagation. [VERIFIED: codebase grep]
- [ ] Extend `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` or fake store to assert URL metadata propagation. [VERIFIED: codebase grep]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | no | No auth/session surface changes; admin routes remain behind existing controller/security setup. [VERIFIED: codebase grep] |
| V3 Session Management | no | No session/cookie changes. [VERIFIED: codebase grep] |
| V4 Access Control | no | No new admin endpoint is required; URL import remains existing admin route. [VERIFIED: codebase grep] |
| V5 Input Validation | yes | Parse Archidekt metadata with explicit nullable coercion; malformed or missing fields become null while capture timestamp records that payload was seen. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| V6 Cryptography | no | No crypto or secret handling changes. [VERIFIED: codebase grep] |

### Known Threat Patterns for .NET/Dapper Harvest Persistence

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection through deck metadata | Tampering | Continue using Dapper parameters; existing repository writes parameterize deck id, timestamps, and commander name. [VERIFIED: codebase grep] |
| Storing excessive third-party payload data | Information Disclosure | Persist curated nullable columns only; do not store raw payload JSON. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |
| False provenance by inferred bracket | Tampering | Copy declared `edhBracket`; never infer bracket locally in this phase. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md] |

## Sources

### Primary (HIGH confidence)
- `.planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md` - locked decisions, deferred scope, target code paths. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` - current single-request payload parse and `cards[]` handling. [VERIFIED: codebase grep]
- `DeckFlow.Core.Tests/Fixtures/archidekt-background-companion.json` - observed top-level payload fields and example values. [VERIFIED: codebase grep]
- `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` - schema and additive migration pattern. [VERIFIED: codebase grep]
- `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` and `CategoryKnowledgeRepository.cs` - processed-row write points. [VERIFIED: codebase grep]
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`, `ICategoryKnowledgeStore.cs`, `CategoryKnowledgeStore.cs` - URL import propagation path. [VERIFIED: codebase grep]
- `DeckFlow.Core.Tests/*` and `DeckFlow.Web.Tests/*` named in this research - existing validation hooks. [VERIFIED: command output]

### Secondary (MEDIUM confidence)
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md` and `.json` - Phase 2 corpus hygiene notes and sampled live payload observations. [CITED: .planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md]
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-08-SUMMARY.md` - context for non-gating bracket capture and future use. [CITED: .planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-08-SUMMARY.md]

### Tertiary (LOW confidence)
- None; no unverified external web sources were used. [VERIFIED: command output]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - validated from project files and local command availability. [VERIFIED: command output]
- Architecture: HIGH - all write paths and schema owners were traced in code. [VERIFIED: codebase grep]
- Pitfalls: HIGH for compatibility/hash/skip semantics from context and code; MEDIUM for recommended exact test warning signs. [CITED: .planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-CONTEXT.md]

**Research date:** 2026-07-29  
**Valid until:** 2026-08-28
