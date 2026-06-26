---
phase: 65-prod-content-artifact-reconcile
plan: 03
status: complete
requirements: [DATA-02]
completed: 2026-06-22
---

# Plan 65-03 Summary — content-kb-check CLI + ContentKbOrphanScanner

**Outcome:** SC3 repeatable check delivered. A read-only `content-kb-check` CLI command reports
content_site_index orphans against local artifact files, backed by a pure, unit-tested
`ContentKbOrphanScanner` Core helper. Exits 1 when a published orphan exists, else 0.

## What was built

- **`DeckFlow.Core/Content/ContentKbOrphanScanner.cs`** — pure static `Scan(rows, contentBase)`
  returning `ContentKbOrphanScanResult` (TotalRows, RowsWithArtifact, MissingCount,
  PublishedOrphanCount, HiddenOrphanCount, per-row `ContentKbRowCheck` list). Existence mirrors the
  live serving path exactly: `File.Exists(Path.GetFullPath(Path.Combine(contentBase, row.ArtifactPath)))`
  — `row.ArtifactPath` already begins with `content-kb/`, so `contentBase` is its PARENT (identical
  to `ContentKbArtifactPathResolver.ResolveArtifactFullPath`). Published-orphan =
  `missing && IsVisible && !IsHidden`. Reuses the `ValidateArtifactPath` rooted/`..` guard
  (same messages) before any combine. No Console/DB access in the classification path.
- **`DeckFlow.Core.Tests/Content/ContentKbOrphanScannerTests.cs`** — 7 xUnit tests: OK resolution
  (regression that fails under the old double-prefix), published-orphan, hidden-orphan,
  is_hidden-excludes-published, content-base resolution, rooted + `..` rejection.
- **`DeckFlow.CLI/ContentKbCommandRunners.cs`** — `RunContentKbCheckAsync(db, artifactRoot)`:
  resolves a LOCAL db, normalizes `--artifact-root` to a content base (`NormalizeToContentBase`:
  trailing `content-kb` segment → parent), runs the scanner, prints per-row + summary, returns the
  exit code. Never touches prod.
- **`DeckFlow.CLI/Program.cs`** — `content-kb-check` command registered (`--db`, `--artifact-root`)
  mirroring `content-index-export`.

## Key files

- created: `DeckFlow.Core/Content/ContentKbOrphanScanner.cs`
- created: `DeckFlow.Core.Tests/Content/ContentKbOrphanScannerTests.cs`
- modified: `DeckFlow.CLI/ContentKbCommandRunners.cs`
- modified: `DeckFlow.CLI/Program.cs`

## Verification

- `dotnet build` (Core.Tests, CLI): **0 errors**; only pre-existing warnings (NU1903 SQLitePCLRaw,
  obsolete TrustServerCertificate, prior XML-doc nits) — none from the new files.
- `dotnet test --filter "FullyQualifiedName~ContentKbOrphan"`: **7/7 passed**.
- Smoke: `content-kb-check` against an empty local db → `Total rows: 0`, exit 0; command appears in
  `--help`. Both `--artifact-root` conventions normalize identically (unit-covered).

## Notes / deviations

- EOL: new C# files use **LF** to match existing siblings (`.gitattributes` enforces LF repo-wide;
  the CLAUDE.md "prefer CRLF for new files" carve-out yields to matching the sibling/gate). Plan
  said "match sibling EOL" — siblings are LF.
- The published-orphan **exit-1** end-to-end path is locked by unit tests + the handler's
  `PublishedOrphanCount > 0 ? 1 : 0`; the CLI smoke exercised the empty-db exit-0 path (seeding a
  visible-but-missing row end-to-end would require the full harvest/upsert stack — out of scope).

## Threat model

- T-65-01 (path traversal) **mitigated** — guard reused + tested (`Scan_RootedArtifactPath_Throws`,
  `Scan_DotDotArtifactPath_Throws`).
- T-65-02-INFO **accepted** — catch surfaces only local-file/sqlite messages; no prod string.
- T-65-03-PRODWRITE **non-threat** — no prod access introduced; LOCAL `--db` only.

## Commits

- `f60a9dd8` feat(content-kb): add ContentKbOrphanScanner + xUnit tests
- `49dde99d` feat(content-kb): add content-kb-check CLI command
