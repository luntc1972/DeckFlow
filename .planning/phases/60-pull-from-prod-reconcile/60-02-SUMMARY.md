# 60-02 SUMMARY — SFTP downloader + read-only prod reader

**Plan:** 60-02 (Wave 1) · **Requirement:** SYNC-01 · **Status:** Complete

## What was built

Two Stage-1 building blocks in `DeckFlow.Studio/Services/` (no DI/page wiring — Plan 03 owns that):

- **`ISshArtifactDownloader` + `SftpArtifactDownloader`** — structural mirror of the existing
  upload pair using `SftpClient.DownloadFile`. One `SftpClient` per call, sequential downloads,
  per-file and connect-level failures returned (never thrown) carrying only a sanitized literal
  (no `ex.Message`, no host/key/path leak — D-07). Guards BOTH sides of the transfer: the remote
  path via the uploader's `TryBuildRemotePath` (under `RemoteArtifactRoot`) and the local
  destination via a new `TryBuildLocalPath` (rejects rooted/`..`, then `Path.GetFullPath`
  containment under the staging root) because the prod-DB `ArtifactPath` is untrusted. Read-only
  toward the remote — never creates remote directories.
- **`IProdContentReader` + `ProdContentReader`** (R1) — a dedicated read-only prod reader exposing
  exactly one `ReadAllAsync` method and zero mutators, so the production side is structurally
  write-free (R2). Builds the Npgsql connection on-demand from the raw connection string (the
  `ProdStoreFactory` convention) and runs a SINGLE plain `SELECT` matching the store's column set —
  NO `EnsureSchemaAsync`, NO `CREATE`/`ALTER` DDL, NO information-schema introspection. Row mapping
  mirrors `ContentSiteIndexStore.ToContentSiteIndexRow` (natural-key split + tag deserialize).

## Verification

- `dotnet build DeckFlow.Studio` — clean (0 errors).
- `git diff DeckFlow.Studio.csproj` — empty (no new package; Dapper flows transitively via Core,
  SSH.NET 2025.1.0 already present).
- `grep ex.Message` downloader — 3 hits, ALL in comments (the no-leak promise); zero real usage.
- `grep DownloadFile` downloader — present; local `Path.GetFullPath` containment guard present.
- `grep EnsureSchemaAsync|CREATE TABLE|ALTER TABLE|information_schema` reader — 2 hits, ALL in
  comments (the no-DDL promise); zero real DDL.
- `IProdContentReader` mutator grep (`Upsert|Delete|Set[A-Z]|EnsureSchema|Stamp|Clear`) — 0.
- `SELECT` present in reader.

## Key files

- created: `DeckFlow.Studio/Services/ISshArtifactDownloader.cs`
- created: `DeckFlow.Studio/Services/SftpArtifactDownloader.cs`
- created: `DeckFlow.Studio/Services/IProdContentReader.cs`
- created: `DeckFlow.Studio/Services/ProdContentReader.cs`

## Commits

- `cb2ef35e` feat(60-02): SFTP artifact downloader + read-only prod content reader

## Notes / deviations

- No unit tests in this plan — both classes are I/O glue (live SFTP + live Npgsql) with no pure
  logic to test in isolation; the path-traversal guards and the read-only/structural-write-free
  guarantees are exercised by the Plan 03 bUnit suite via fakes. (Per the project rule, pure glue
  skips dedicated tests; the structural R1/R2 guarantee is verified by the build-time mutator/DDL
  greps above.)

## Self-Check: PASSED
