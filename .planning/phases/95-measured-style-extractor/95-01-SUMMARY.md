# Plan 95-01 Summary

## What Was Built

- Added `MetricDistribution.EffectiveSampleSize` as a nullable nested field so measured metrics can carry the D-10 folder-weighted effective sample without changing any top-level `MeasuredMetric` fields.
- Added the new `creator_profile_source` substrate: `CreatorProfileSource`, `ICreatorProfileSourceStore`, and a dialect-guarded `CreatorProfileSourceStore` with SQLite/Postgres DDL, slug-keyed UPSERT, nullable `last_crawled_utc`, and a single-column `SetLastCrawledAsync` freshness stamper.
- Added targeted SQLite tests plus gated Postgres tests for `CreatorProfileSourceStore`, and extended the existing P94 round-trip fixture/tests to cover both populated and null `EffectiveSampleSize`.

## Key Files

- Modified `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs`
- Modified `DeckFlow.Core.Tests/CreatorStyleProfileTestData.cs`
- Modified `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs`
- Added `DeckFlow.Core/Content/CreatorProfileSource.cs`
- Added `DeckFlow.Core/Content/ICreatorProfileSourceStore.cs`
- Added `DeckFlow.Core/Content/CreatorProfileSourceStore.cs`
- Added `DeckFlow.Core.Tests/CreatorProfileSourceStoreTests.cs`

## Decisions And Deviations

- Mirrored `CreatorStyleProfileStore`'s constructor/schema-gate/UPSERT structure exactly, including the internal `connectionFactoryOverride` seam and paired Postgres/SQLite DDL constants.
- Stored `FolderWeights` as nullable JSON text when empty, with typed `Dictionary<int, double>` deserialization on reads.
- No scope deviations. No DI wiring, program changes, or out-of-fence file edits were made.

## Verification Status

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorStyleProfileStoreTests"`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorProfileSourceStoreTests"`: PASSED
- Gated Postgres tests were discovered by the filtered run and skipped as designed because `DECKFLOW_POSTGRES_TESTS=1` was not set in-agent.

## Self-Check

- PASSED
