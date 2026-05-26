---
status: complete
plan: 19-04
phase: 19-content-kb-foundation-local-schema-contracts
requirements-completed:
  - KB-02
  - KB-05
  - KB-08
key-files:
  created:
    - DeckFlow.Web/Services/Content/IWhisperSpendLedger.cs
    - DeckFlow.Web/Services/Content/WhisperSpendLedger.cs
    - DeckFlow.Web.Tests/WhisperSpendLedgerTests.cs
    - DeckFlow.Web/Services/Content/IContentHarvestRunStore.cs
    - DeckFlow.Web/Services/Content/ContentHarvestRunStore.cs
    - DeckFlow.Web.Tests/ContentHarvestRunStoreTests.cs
    - DeckFlow.Web/Services/Content/IContentSiteIndexStore.cs
    - DeckFlow.Web/Services/Content/ContentSiteIndexStore.cs
    - DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs
    - .planning/phases/19-content-kb-foundation-local-schema-contracts/19-04-SUMMARY.md
  modified: []
verification:
  - 'Task 1 RED: filtered Web test failed on missing WhisperSpendLedger.'
  - 'Task 1 GREEN: WhisperSpendLedgerTests passed 5/5; DeckFlow.Web.Tests build grep returned Build succeeded.'
  - 'Task 2 RED: filtered Web test failed on missing ContentHarvestRunStore.'
  - 'Task 2 GREEN: ContentHarvestRunStoreTests passed 2/2; DeckFlow.Web.Tests build grep returned Build succeeded.'
  - 'Task 3 RED: filtered Web test failed on missing ContentSiteIndexStore.'
  - 'Task 3 GREEN: ContentSiteIndexStoreTests passed 5/5; DeckFlow.Web.Tests build grep returned Build succeeded.'
  - 'Final: "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web passed with 0 warnings and 0 errors.'
  - 'Final: filtered DeckFlow.Web.Tests run passed 12/12 for WhisperSpendLedgerTests, ContentHarvestRunStoreTests, and ContentSiteIndexStoreTests.'
completed: 2026-05-26T22:48:25Z
---

# 19-04 Summary

Content KB persistence now has local Whisper spend tracking, local harvest run summaries, and the provider-aware slim site index destined for Render.

## key-files

- `DeckFlow.Web/Services/Content/IWhisperSpendLedger.cs` - spend ledger contract for recording calls, totaling monthly spend, and checking projected cap usage.
- `DeckFlow.Web/Services/Content/WhisperSpendLedger.cs` - local-only spend ledger store with content video parent bootstrap, exact decimal app-side monthly totals, and `DECKFLOW_WHISPER_MONTHLY_CAP_USD` cap check.
- `DeckFlow.Web.Tests/WhisperSpendLedgerTests.cs` - exact decimal, month isolation, cap under/over, and idempotency tests.
- `DeckFlow.Web/Services/Content/IContentHarvestRunStore.cs` - local harvest run summary contract.
- `DeckFlow.Web/Services/Content/ContentHarvestRunStore.cs` - local-only `content_harvest_runs` store with start, complete, and get surfaces.
- `DeckFlow.Web.Tests/ContentHarvestRunStoreTests.cs` - run summary round-trip and idempotency tests.
- `DeckFlow.Web/Services/Content/IContentSiteIndexStore.cs` - slim site-index contract.
- `DeckFlow.Web/Services/Content/ContentSiteIndexStore.cs` - provider-aware `content_site_index` store with three JSON tag columns, normalized natural-key upsert, and artifact path validation.
- `DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs` - site-index upsert/get, re-upsert, path rejection, natural-key validation, and idempotency tests.

## what-was-built

- `WhisperSpendLedger` creates `whisper_spend_ledger` after ensuring `content_videos` exists, with `REFERENCES content_videos(id) ON DELETE CASCADE` in both dialect DDL constants.
- `WhisperSpendLedger.GetMonthlyTotalAsync` reads matching `cost_usd` rows and accumulates with `decimal` in C#; there is no SQL aggregate over the SQLite TEXT money column.
- `WhisperSpendLedger.WouldExceedCapAsync` reads `DECKFLOW_WHISPER_MONTHLY_CAP_USD` from `IConfiguration` or environment, defaults to `15.00m`, and intentionally uses no TOCTOU locking machinery.
- `ContentHarvestRunStore` creates the separate `content_harvest_runs` table and never shares or widens the existing v1.1 `harvest_runs` schema.
- `ContentHarvestRunStore` records started/completed UTC timestamps, processed counts, Whisper call count, exact spend, and optional abort reason.
- `ContentSiteIndexStore` creates the Render-bound slim `content_site_index` table with Postgres and SQLite DDL parity.
- `content_site_index` includes `archetype_tags`, `bracket_tags`, and `card_category_tags` as JSON-array TEXT columns with `NOT NULL DEFAULT '[]'`.
- `ContentSiteIndexStore.UpsertRowAsync` requires exactly one natural key, maps YouTube rows to `(youtube_channel, value)` and RSS rows to `(podcast_rss, value)`, and uses one `ON CONFLICT (natural_key_type, natural_key_value)` path.
- `ContentSiteIndexStore` serializes tags via `ContentArtifactSpec.SerializeTags`, deserializes via `DeserializeTags`, rejects rooted artifact paths, and rejects `..` path segments.

## Task Commits

1. Task 1: Whisper spend ledger - `002bb96`
2. Task 2: Content harvest run store - `90a504a`
3. Task 3: Slim content site index store - `e8888b0`

## deviations

None. The implementation followed the plan and did not modify `.planning/STATE.md` or `.planning/ROADMAP.md`.

## Self-Check: PASSED

- TDD red runs failed for the intended missing store types before each task implementation.
- All focused task tests passed: 5/5 spend ledger, 2/2 harvest run, 5/5 site index.
- Final `DeckFlow.Web` build passed with 0 warnings and 0 errors.
- Final filtered `DeckFlow.Web.Tests` run passed 12/12. The full Web suite was not run per the plan note about the pre-existing unrelated `AdminCssPhase1` failure.
- Acceptance greps confirmed `WhisperSpendLedger.cs` has no `SUM(`, includes `DECKFLOW_WHISPER_MONTHLY_CAP_USD`, uses `_connectionInfo.OpenConnectionAsync`, bootstraps the `content_videos` parent first, and contains no advisory-lock, serializable-transaction, or kill-switch code.
- Acceptance greps confirmed `ContentHarvestRunStore.cs` uses `CreateLocalContentKbConnection`, opens through `_connectionInfo.OpenConnectionAsync`, creates `content_harvest_runs`, and has zero `CREATE TABLE IF NOT EXISTS harvest_runs` matches.
- Acceptance greps confirmed `ContentSiteIndexStore.cs` has both DDL constants, all three tag columns defaulting to `[]`, normalized natural-key uniqueness, one conflict target, `SerializeTags`/`DeserializeTags`, artifact-path guards, `CreateContentSiteIndexConnection`, and no heavy transcript/audio/spend columns in the DDL constants.
