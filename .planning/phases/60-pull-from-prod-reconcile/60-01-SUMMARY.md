# 60-01 SUMMARY — ContentSyncDiffClassifier + SyncDiffEntry

**Plan:** 60-01 (Wave 1) · **Requirement:** SYNC-02 · **Status:** Complete

## What was built

The pure, side-effect-free diff classifier at the heart of SYNC-02, living in `DeckFlow.Core/Content/`
beside `PublishStateDeriver`.

- **`SyncDiffEntry.cs`** — `SyncDiffKind` enum with exactly four members (`ProdNewer`,
  `MissingLocally`, `LocalOnly`, `Diverged` — no fifth `LocalNewer`; local-newer folds into
  `Diverged` + `LocalIsNewer`) and an immutable `SyncDiffEntry` record carrying the natural key
  (type + value), both row snapshots, the artifact path, and the `LocalIsNewer` / `ArtifactDownloaded`
  hints the Studio page (Plan 03) needs.
- **`ContentSyncDiffClassifier.cs`** — `public static IReadOnlyList<SyncDiffEntry> Classify(prodRows, localRows)`.
  Indexes both sides by natural key (`YoutubeVideoId ?? RssGuid`), classifies each key, and OMITS
  identical in-sync pairs (R3). Timestamps compared via `ToUniversalTime().UtcDateTime`
  (F-51-PG-01 class guard). Equal-timestamp content divergence decided by a unit-separator content
  fingerprint (`Title + ArtifactPath + Archetype/Bracket/CardCategory tags`).

## Verification

- `dotnet build DeckFlow.Core` — clean (0 errors).
- `dotnet test --filter ContentSyncDiffClassifier` — **14/14 passed**.
- Coverage: all four kinds reachable, `LocalIsNewer` direction, R3 identical-pair-omitted,
  same-instant-different-offset treated equal, mixed youtube/podcast keys, title fallback,
  empty/one-sided sets, null-arg guards.

## Key files

- created: `DeckFlow.Core/Content/SyncDiffEntry.cs`
- created: `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs`
- created: `DeckFlow.Core.Tests/Content/ContentSyncDiffClassifierTests.cs`

## Commits

- `fa8deeac` feat(60-01): add SyncDiffEntry + SyncDiffKind contract
- `01ac8241` feat(60-01): implement ContentSyncDiffClassifier

## Notes / deviations

- `tdd_mode` is off project-wide, so the RED test commit was folded into the GREEN
  implementation commit rather than committed separately; tests and impl land together,
  every commit builds clean.
- Classifier defensively skips rows with no natural key (neither YouTube id nor RSS guid) to
  avoid a null-key dictionary crash — the store never emits such rows, but it keeps `Classify`
  total (T-60-01 DoS mitigation).

## Self-Check: PASSED
