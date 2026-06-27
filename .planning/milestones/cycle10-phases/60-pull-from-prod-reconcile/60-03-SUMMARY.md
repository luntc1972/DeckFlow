# 60-03 SUMMARY — PullFromProd page + nav + DI + bUnit + README

**Plan:** 60-03 (Wave 2) · **Requirements:** SYNC-01, SYNC-03 · **Status:** Complete

## What was built

The operator-facing read mirror of DirectPush, plus its DI/nav wiring, test doubles, bUnit
coverage, and README entry.

- **`DeckFlow.Studio/Pages/PullFromProd.razor`** — 2-stage gated page (`@page "/pull-from-prod"`,
  `@implements IDisposable`). Stage 1 wipes + recreates an isolated `pull-staging/` dir, reads prod
  via the read-only `IProdContentReader` (R1 — never `ProdStoreFactory`/`ContentSiteIndexStore`, no
  `EnsureSchemaAsync`/DDL), SCP-downloads artifacts, runs `ContentSyncDiffClassifier.Classify`
  (in-sync omitted), and renders a per-entry 4-kind diff table. Stage 2 applies each adopt-prod
  LOCAL-only: `UpsertContentColumnsOnlyAsync` + mirror prod `approval_status` (keyed on the
  `ContentSourceType` discriminator derived from the row) + `File.Move` staged→`content-kb/`. R4:
  partial-pull adopt stays selectable, still upserts + mirrors approval, skips only the move.
  LocalOnly is display-only. Sanitized catches (no `ex.Message`), CTS disposed on circuit drop,
  hard-guard + `InvokePullApplyForTest` seam mirror DirectPush.
- **`NavMenu.razor`** — "Pull from Prod" entry after Direct Push (download icon).
- **`Program.cs`** — `ISshArtifactDownloader` + `IProdContentReader` singleton registrations.
- **Test doubles** — `FakeSshArtifactDownloader` (writes staged placeholders on success; fail +
  sentinel injection) and `FakeProdContentReader` (distinct read-only prod fake, no write API,
  read counter, sentinel-throw).
- **`PullFromProdPageTests.cs`** — 11 bUnit tests.
- **README** — new "Pull from Prod" lane documented.

## Verification

- `dotnet build DeckFlow.Studio` + `DeckFlow.Studio.Tests` — clean (0 errors).
- `dotnet test --filter PullFromProd` — **11/11 passed**; full Studio suite **80/80**.
- Gates: page `UpsertRowAsync` = 0, `ProdStoreFactory|EnsureSchemaAsync` = 0 (comment-only),
  `ex.Message` = 0 (comment-only), DI lines ≥ 2, no `*.csproj` change, README has the section.

## Key files

- created: `DeckFlow.Studio/Pages/PullFromProd.razor`
- modified: `DeckFlow.Studio/Shared/NavMenu.razor`, `DeckFlow.Studio/Program.cs`, `README.md`
- created: `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactDownloader.cs`,
  `DeckFlow.Studio.Tests/TestDoubles/FakeProdContentReader.cs`,
  `DeckFlow.Studio.Tests/PullFromProdPageTests.cs`

## Commits

- `a5b2a435` feat(60-03): PullFromProd 2-stage page + nav + DI
- `5ef52b0b` test(60-03): PullFromProd bUnit suite + fakes + README

## Notes / deviations

- **Approval-status key discriminator (correctness fix over the plan's literal text):** the plan
  said to pass `entry.NaturalKeyType` to `SetApprovalStatusAsync`, but the classifier emits the
  short form ("youtube"/"podcast") while the store keys on `ContentSourceType.Youtube`/`Podcast`
  ("youtube_channel"/"podcast_rss"). Passing the short form would match zero rows. The page derives
  the correct discriminator from the row (`ProdRow.YoutubeVideoId is not null ? Youtube : Podcast`).
  The bUnit adopt test asserts the approval call uses `ContentSourceType.Youtube`.
- One transient full-suite failure appeared once (parallel-isolation flake, same class as the known
  Core SQLite parallel flakiness) and did not reproduce; the suite is 80/80 on re-run and
  PullFromProd is 11/11 in isolation. Per-render unique temp staging dirs (`Path.GetRandomFileName`)
  keep the new tests collision-free.

## Self-Check: PASSED
