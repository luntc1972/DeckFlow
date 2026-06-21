# Phase 59 — Deferred Items (out-of-scope discoveries during execution)

## DEF-59-01: Pre-existing DeckFlow.Web.Tests build break (TestServiceFactory)

- **Discovered during:** Plan 59-01 full-solution build verification.
- **File:** `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs:128`
- **Error:** `CS1503: Argument 10: cannot convert from 'ILogger<DeckAnalysisPacketService>' to 'IFeatureFlagCache?'`
- **Root cause:** `DeckAnalysisPacketService`'s constructor gained an `IFeatureFlagCache?`
  parameter (FeatureFlags work in a prior cycle), but this Web.Tests factory double was never
  updated — it still passes the `logger` argument into the `IFeatureFlagCache?` slot.
- **Why out of scope:** Plan 59-01 only touches `DeckFlow.Core` + `DeckFlow.Core.Tests`
  (the AUTO-02 auto-approve seam). The break is in `DeckFlow.Web.Tests`, an unrelated project,
  and is not caused by any 59-01 change. Per the executor SCOPE BOUNDARY rule, pre-existing
  failures in unrelated files are logged, not fixed.
- **In-scope verification status:** `DeckFlow.Core` and `DeckFlow.Core.Tests` both build clean
  (Build succeeded, 0 errors); all 14 new 59-01 tests pass.
- **Suggested fix (future):** update `TestServiceFactory.CreateDeckAnalysisPacketService` to pass an
  `IFeatureFlagCache` (or a test double) in the correct argument position before `logger`.
