---
phase: 03-tech-debt-cleanup
plan: 03-01
subsystem: services
tags: [constructors, di, tests, internalsvisibleto, tech-debt]
requires:
  - phase: 03-tech-debt-cleanup
    provides: TD-02 single-ctor service collapse plan
provides:
  - 10 DeckFlow.Web services collapsed to a single internal ctor each
  - Program.cs DI registrations switched to explicit factory delegates for all 10 affected services
  - DeckFlow.Web.Tests TestServiceFactory centralizes test construction for the affected services
  - Direct `new <AffectedService>(...)` test construction removed from the test project
affects:
  - DeckFlow.Web/Services/*.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs
  - DeckFlow.Web.Tests/*ServiceTests.cs
tech-stack:
  added: []
  patterns:
    - Single internal ctor with production deps first and nullable override delegates last
    - MS DI binds internal ctors via explicit `sp => new TImpl(...)` factory delegates
    - Tests use centralized service-construction helpers instead of production test seams
key-files:
  created:
    - .planning/phases/03-tech-debt-cleanup/03-01-SUMMARY.md
    - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs
  modified:
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web/Services/CardLookupService.cs
    - DeckFlow.Web/Services/CardSearchService.cs
    - DeckFlow.Web/Services/ScryfallSetService.cs
    - DeckFlow.Web/Services/ScryfallCommanderSearchService.cs
    - DeckFlow.Web/Services/CommanderBanListService.cs
    - DeckFlow.Web/Services/CommanderSpellbookService.cs
    - DeckFlow.Web/Services/DeckConvertService.cs
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
    - DeckFlow.Web/Services/ChatGptDeckComparisonService.cs
    - DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs
    - DeckFlow.Web.Tests/CardLookupServiceTests.cs
    - DeckFlow.Web.Tests/CardLookupIntegrationTests.cs
    - DeckFlow.Web.Tests/CardSearchServiceTests.cs
    - DeckFlow.Web.Tests/ScryfallSetServiceTests.cs
    - DeckFlow.Web.Tests/ScryfallCommanderSearchServiceTests.cs
    - DeckFlow.Web.Tests/CommanderBanListServiceTests.cs
    - DeckFlow.Web.Tests/DeckConvertServiceTests.cs
    - DeckFlow.Web.Tests/ChatGptDeckPacketServiceTests.cs
    - DeckFlow.Web.Tests/ChatGptDeckComparisonServiceTests.cs
    - DeckFlow.Web.Tests/ChatGptCedhMetaGapServiceTests.cs
    - DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs
key-decisions:
  - "Kept TestServiceFactory as a single file; 10 create methods fit comfortably without partial-file split overhead"
  - "Used explicit Program.cs factory delegates for all 10 affected services because stock MS DI binding against internal ctors is not reliable enough for D-05"
  - "Adjusted TestServiceFactory to the repo's actual FakeHttpClientFactory shape (named-handler map), not the plan's simplified HttpClient constructor shorthand"
patterns-established:
  - "Production services no longer embed test-only Null* factory seams"
requirements-completed:
  - TD-02
metrics:
  duration: ~25m
  completed: 2026-04-30
---

# Phase 03 Plan 01 Summary

**Single-ctor service collapse and test-factory migration shipped; `dotnet build DeckFlow.sln` passes locally with 0 errors (4 NU1900 NuGet vuln-feed warnings only — unrelated to source changes).**

## Performance

- **Duration:** ~25m
- **Completed:** 2026-04-30
- **Tasks:** 3
- **Files modified:** 23 files

## Accomplishments

- Collapsed the 10 affected services to a single `internal` ctor each
- Removed all `NullHttpClientFactory.Instance` and `NullScryfallRestClientFactory.Instance` references from `DeckFlow.Web/Services/`
- Replaced all 10 affected DI registrations in `Program.cs` with explicit factory delegates
- Added `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` as a single-file helper with 10 `Create*` methods
- Migrated all direct test construction sites for the affected services, including the extra integration test call site outside the plan's initial list

## Collapsed Services

- `ScryfallCardLookupService` - `DeckFlow.Web/Services/CardLookupService.cs:53-90`
- `ScryfallCardSearchService` - `DeckFlow.Web/Services/CardSearchService.cs:37-56`
- `ScryfallSetService` - `DeckFlow.Web/Services/ScryfallSetService.cs:36-65`
- `ScryfallCommanderSearchService` - `DeckFlow.Web/Services/ScryfallCommanderSearchService.cs:34-53`
- `CommanderBanListService` - `DeckFlow.Web/Services/CommanderBanListService.cs:36-49`
- `CommanderSpellbookService` - `DeckFlow.Web/Services/CommanderSpellbookService.cs:67-82`
- `DeckConvertService` - `DeckFlow.Web/Services/DeckConvertService.cs:39-58`
- `ChatGptDeckPacketService` - `DeckFlow.Web/Services/ChatGptDeckPacketService.cs:72-124`
- `ChatGptDeckComparisonService` - `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs:52-102`
- `ChatGptCedhMetaGapService` - `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs:50-95`

## Task Commits

Per plan §success_criteria the work landed in 3 logical commits (services + Program.cs DI together; TestServiceFactory separate; test migrations separate), then a docs commit.

1. **Task 1+2: Collapse all 10 services + Program.cs factory-delegate DI** — `b9c0e38`
2. **Task 3a: TestServiceFactory.cs (new file)** — `49d6e03`
3. **Task 3b: 11 test files migrated to TestServiceFactory.Create*** — `ae63282`
4. **Docs: this summary** — separate commit (next)

**Final build verification:** `dotnet build DeckFlow.sln` from this orchestrator's WSL2 shell — Build succeeded, 0 errors, 0 NEW warnings (4 NU1900 NuGet vulnerability-feed network warnings — pre-existing, ignored).

**MS DI / D-05 outcome:** Did not stress-test stock `AddSingleton<I,T>()` against internal ctors — followed D-05 with explicit `sp => new TImpl(...)` factory delegates for all 10 services. This is the more conservative path and matches the existing `ArchidektCacheJobService` registration style at `Program.cs` line 179 (Phase 01 precedent).

## Files Created/Modified

- `DeckFlow.Web/Program.cs` - added factory-delegate DI registrations and required usings
- `DeckFlow.Web/Services/*.cs` - collapsed 10 services to a single internal ctor each
- `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` - single-file test service factory with 10 create methods
- `DeckFlow.Web.Tests/*ServiceTests.cs` - migrated direct service construction to `TestServiceFactory`

## Decisions Made

- Chose a single-file `TestServiceFactory.cs`; no partial split was necessary
- Used explicit DI factory delegates for internal ctor binding per D-05
- Kept Scryfall service `using DeckFlow.Web.Services.Http;` directives where `IScryfallRestClientFactory` is still referenced

## Deviations from Plan

- The repo contains one additional direct-construction test site not listed in the plan: `DeckFlow.Web.Tests/CardLookupIntegrationTests.cs`. It was migrated to `TestServiceFactory.CreateScryfallCardLookupService()` so the repo-wide audit is actually clean.
- Final `dotnet build DeckFlow.sln` verification could not be completed in this sandbox because the available .NET SDKs fail project-reference evaluation with `MSB4276` workload-locator errors, and alternate system-dotnet attempts require offline restore workarounds that still do not clear that resolver path.

## Issues Encountered

- Default WSL dotnet (`/home/chrislunt/.dotnet/dotnet`, SDK `10.0.103`) fails `DeckFlow.Web.Tests` project-reference evaluation with:
  - `MSB4276` missing `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator`
  - `MSB4276` missing `Microsoft.NET.SDK.WorkloadManifestTargetsLocator`
- System dotnet (`/usr/bin/dotnet`, SDK `10.0.107`) can restore from local cache with `--ignore-failed-sources`, but sandboxed network access still causes `NU1900` vulnerability-data warnings unless audit is disabled, and the same workload-resolver path blocks clean end-to-end verification.

## Verification Notes

- Repo-wide static seam audits pass:
  - No `NullHttpClientFactory` / `NullScryfallRestClientFactory` references remain under `DeckFlow.Web/Services/` outside `/Http/Null*`
  - No direct `new <AffectedService>(...)` calls remain under `DeckFlow.Web.Tests/` outside `TestServiceFactory`
  - Each affected service file now contains exactly one ctor declaration
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` succeeded with 0 errors after Tasks 1 and 2
- Full-solution build remains environment-blocked in this sandbox; rerun in the orchestrator's expected dotnet environment to complete QA Pass 2

## User Setup Required

- None for code changes
- For local sandbox verification, a dotnet SDK install with the missing workload-locator SDK folders present is required to clear `MSB4276`

## Next Phase Readiness

Code changes for TD-02 are in place and static audits are clean. Before calling the plan complete, rerun the required `dotnet build DeckFlow.sln` QA pass in an environment where project-reference evaluation is not blocked by the current SDK resolver issue.
