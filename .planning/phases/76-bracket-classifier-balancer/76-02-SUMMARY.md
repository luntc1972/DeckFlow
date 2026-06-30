---
phase: 76-bracket-classifier-balancer
plan: "02"
subsystem: DeckFlow.Web.Services.Bracket + DeckFlow.Web.Models + DeckFlow.Web.Services.FeatureFlags
tags: [bracket, catalog-service, feature-flag, dark-launch, json-backed-shim, startup-warm]
dependency_graph:
  requires:
    - 76-01 (GameChangerCatalog/BracketTier records + bracket-data.json)
  provides:
    - IGameChangerCatalogService / GameChangerCatalogService (JSON->IMemoryCache, warm-loaded at startup)
    - CommanderBracketCatalog JSON-backed shim (no tier literal; bracket-data.json via AppContext.BaseDirectory)
    - tool.bracket.enabled seeded OFF in both dialects (BRACKET-05 dark-launch)
  affects:
    - Phase 76 plans 03-06 (BracketClassificationService, prompt variants, controller, view)
    - All existing analysis/primer/set-upgrade prompts (byte-identical — shim preserves text verbatim)
tech_stack:
  added: []
  patterns:
    - JSON->IMemoryCache singleton service with internal test-seam ctor (analog: HelpContentService)
    - Lazy<T> static file-load shim for backward-compatible migration (analog: none — first of kind)
    - Feature flag dark-launch: seeded present + disabled (first tool flag seeded OFF in project)
    - Linked Content item in test csproj for cross-project data file copy
key_files:
  created:
    - DeckFlow.Web/Services/Bracket/IGameChangerCatalogService.cs
    - DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs
    - DeckFlow.Web.Tests/Bracket/GameChangerCatalogServiceTests.cs
  modified:
    - DeckFlow.Web/Program.cs (AddSingleton + startup warm call)
    - DeckFlow.Web/Models/CommanderBracketCatalog.cs (Lazy shim, no tier literal)
    - DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj (linked bracket-data.json content item)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (tool.bracket.enabled seed OFF)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs (description entry)
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs ([InlineData] for bracket flag)
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs ([InlineData] seeded false)
    - DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs (count 14->15, dark-launch carve-out)
decisions:
  - "GameChangerCatalogService uses JsonSerializerDefaults.Web (case-insensitive) to bind camelCase JSON keys (tiers) to PascalCase record properties (Tiers) — no JsonPropertyName attributes needed on the Core records"
  - "CommanderBracketCatalog uses LazyThreadSafetyMode.PublicationOnly for the static Lazy: cheaper than ExecutionAndPublication, safe for idempotent file reads; multiple threads racing on first access will all read the file but only one result is cached"
  - "Test path for GameChangerCatalogServiceTests navigates 4 levels up from AppContext.BaseDirectory to reach the solution root, then into DeckFlow.Web/Data/. After Task 2 adds the Content item, the same file is available at AppContext.BaseDirectory/Data/ but the Task 1 tests use the repo source to avoid ordering dependency"
  - "tool.bracket.enabled seeded OFF (BRACKET-05 dark-launch): first tool flag in project seeded present-but-disabled; ToolFlagSeedConsistencyTests updated with a named carve-out rather than silently excluding it from count, so the intent is self-documenting"
metrics:
  duration_minutes: 45
  completed_date: "2026-06-28"
  tasks_completed: 3
  files_changed: 11
---

# Phase 76 Plan 02: GameChangerCatalogService + Shim Migration + Flag Wiring Summary

Runtime catalog service (JSON->IMemoryCache warm-loaded at startup), CommanderBracketCatalog migrated from a tier literal to a bracket-data.json-backed Lazy shim preserving byte-identical prompt output, and the four-file-atomic tool.bracket.enabled flag seeded OFF for dark-launch.

## What Was Built

### Task 1: GameChangerCatalogService (JSON -> IMemoryCache) + interface + DI + startup warm + tests

**IGameChangerCatalogService** (`DeckFlow.Web/Services/Bracket/`) — single `GetCatalog()` method returning `GameChangerCatalog`.

**GameChangerCatalogService** — `sealed` singleton:
- DI ctor `(IWebHostEnvironment env, IMemoryCache cache)` sets `_dataFilePath = Path.Combine(env.ContentRootPath, "Data", "bracket-data.json")`
- Internal test-seam ctor `(string dataFilePath, IMemoryCache cache)` for test isolation without a web host
- `GetCatalog()` resolves from cache (`"bracket:game-changer-catalog"` key), else reads file + deserializes with `JsonSerializerOptions(JsonSerializerDefaults.Web)` (binds camelCase `tiers` → `Tiers`), caches 24 hours; throws `InvalidOperationException` on null deserialize (fail-closed per T-76-03)

**Program.cs changes**:
- `AddSingleton<IGameChangerCatalogService, GameChangerCatalogService>()` adjacent to HelpContentService
- Startup warm block after IRequestMetricsStore.EnsureSchemaAsync: `app.Services.GetRequiredService<IGameChangerCatalogService>().GetCatalog()` — satisfies BRACKET-02 "loaded at startup"

**GameChangerCatalogServiceTests** (7 tests):
- 53 Game Changers count assertion
- EffectiveDate == DateOnly(2026, 2, 9)
- Exactly 5 Tiers
- All tiers have non-empty Name/Label/Summary (binding-populated assertion — proves `tiers` key bound, not just that load didn't throw)
- Cache-hit returns same instance (second call)
- Missing file → `FileNotFoundException`
- Garbage JSON → throws any exception (fail-closed path)

### Task 2: Migrate CommanderBracketCatalog to versioned-data shim + copy data into test bin

**CommanderBracketCatalog.cs** refactored:
- Replaced 5-entry tier literal with `Lazy<IReadOnlyList<CommanderBracketOption>>` backed by `LoadOptions()` static helper
- `LoadOptions()` reads `AppContext.BaseDirectory/Data/bracket-data.json` once, deserializes with `JsonSerializerDefaults.Web`, projects each `BracketTier` → `CommanderBracketOption` (Name→Value, Label, Summary, TurnsExpectation verbatim)
- All 17 callers (`CommanderBracketCatalog.Options`, `.Find()`, `.IsCedh()`) compile unchanged — public API surface identical
- Prompt text byte-identical: tier strings copied verbatim from old literal into bracket-data.json in 76-01
- `// Why:` comment documents the BRACKET-02 migration rationale

**DeckFlow.Web.Tests.csproj**:
- Added linked `<Content>` item: `Include="..\DeckFlow.Web\Data\bracket-data.json" Link="Data\bracket-data.json" CopyToOutputDirectory=PreserveNewest`
- Mirrors the existing `Manabase\avatar-facts.json` pattern
- Result: `DeckFlow.Web.Tests/bin/Debug/net10.0/Data/bracket-data.json` present after build

**Verification**: 60 existing analysis/primer/size-parity tests (ResultContractTests, GeminiVariantSizeTests, PrimerPromptVariantTests, AnalysisPromptVariantNoExpertContextTests) pass unchanged from the test bin — byte-identical output confirmed.

### Task 3: tool.bracket.enabled flag wiring (seeded OFF) + seed-consistency test update

Five-file atomic commit (BRACKET-05 dark-launch):

- **FeatureFlagStore.cs**: `('tool.bracket.enabled', FALSE)` in PostgresSeedSql and `('tool.bracket.enabled', 0)` in SqliteSeedSql, both before `ON CONFLICT DO NOTHING`
- **FeatureFlagCatalog.cs**: `["tool.bracket.enabled"] = "Enable the Bracket Check tool…"` description for /Admin/Flags
- **FeatureFlagCatalogTests.cs**: `[InlineData("tool.bracket.enabled")]` added to `Describe_EverySeededFlag_HasNonEmptyDescription` theory
- **FeatureFlagStoreSeedTests.cs**: `[InlineData("tool.bracket.enabled", false)]` — bracket seeded OFF assertion
- **ToolFlagSeedConsistencyTests.cs**: count bumped from 14 to 15; `tool.bracket.enabled` carved out of the all-enabled loop with a self-documenting `// Why:` comment; `RegistryFlagKeys_AreSeeded` unchanged (registry→seed subset direction tolerates seeded-with-no-registry-tool)

## Test Results

```
Full Web test suite: Passed! — Failed: 0, Passed: 955, Skipped: 12, Total: 967
```

- GameChangerCatalogServiceTests: 7 pass
- Catalog-dependent parity tests (Task 2 verification): 60 pass
- FeatureFlagCatalogTests + FeatureFlagStoreSeedTests + ToolFlagSeedConsistencyTests: 44 pass
- Overall suite: 955 pass, 12 skipped (Postgres integration tests expected without container)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Extra ".." in GameChangerCatalogServiceTests DataFilePath**
- **Found during:** Task 1 test run
- **Issue:** `DataFilePath` had 5 `..` segments instead of 4, making the path navigate one level above the solution root → `C:\users\chrislunt\source\personal\DeckFlow.Web\Data\bracket-data.json` (nonexistent)
- **Fix:** Removed the extra `..`; 4 segments navigate from `net10.0/` up to the solution root, then `DeckFlow.Web/Data/bracket-data.json` from there
- **Files modified:** `DeckFlow.Web.Tests/Bracket/GameChangerCatalogServiceTests.cs`
- **Commit:** `9577c617` (fixed before committing)

**2. [Rule 1 - Bug] XML doc cref attribute warning on GameChangerCatalogService**
- **Found during:** Task 1 build
- **Issue:** `<see cref="IWebHostEnvironment.ContentRootPath"/>` produced CS1574 warning (property not resolvable as cref)
- **Fix:** Changed to `<c>IWebHostEnvironment.ContentRootPath</c>` (plain text code element)
- **Files modified:** `DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs`
- **Commit:** `9577c617` (fixed before committing)

**3. [Rule 1 - Bug] Target-typed new() in CommanderBracketCatalog violated source assertion**
- **Found during:** Task 2 acceptance-criteria check
- **Issue:** `new(LoadOptions, LazyThreadSafetyMode.PublicationOnly)` for the Lazy constructor uses target-typed `new(` syntax, causing `grep -c "new("` to return 1 instead of 0 (the plan requires 0 to prove no inline tier literals remain)
- **Fix:** Changed to explicit typed `new Lazy<IReadOnlyList<CommanderBracketOption>>(…)` — no functional change, satisfies grep assertion
- **Files modified:** `DeckFlow.Web/Models/CommanderBracketCatalog.cs`
- **Commit:** `e9e7e936` (fixed before committing)

## Known Stubs

None — this plan builds backend data infrastructure with no UI rendering. No placeholder text or empty data sources introduced.

## Threat Flags

None — all catalog data is public MTG card names and bracket descriptions from an in-repo JSON file. No new network endpoints, auth paths, or schema changes at trust boundaries.

## Self-Check: PASSED
