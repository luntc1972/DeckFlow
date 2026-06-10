---
phase: 28
reviewed: 2026-06-04T19:35:00Z
depth: standard
files_reviewed: 1
files_reviewed_list:
  - DeckFlow.CLI/CommandRunners.cs
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
status: issues_found
---

# Phase 28: Code Review Report

**Reviewed:** 2026-06-04T19:35:00Z
**Depth:** standard
**Files Reviewed:** 1
**Status:** issues_found

## Summary

Phase 28 (plan 28-02) contains a single source change: the default branch of
`ResolveContentKbArtifactRoot` in `DeckFlow.CLI/CommandRunners.cs` was
rewritten to resolve relative to `Directory.GetCurrentDirectory()` + `"content-kb"`
instead of the old db-adjacent `artifacts/content-kb` path. The MTG_DATA_DIR
branch is untouched. No other source files changed in the 67fc148..HEAD range;
the commit is correctly scoped.

The change achieves the single-tree goal when the CLI is invoked from the
repo root — the intended and documented use case. Two issues are raised:

1. **WARNING (WR-01):** The new default for the artifact root and the existing
   default for the database path now resolve to *different* directories when
   MTG_DATA_DIR is unset, breaking co-location. `ResolveContentKbDatabasePath`
   still defaults to `CWD/artifacts/content-kb.db`, while
   `ResolveContentKbArtifactRoot` now defaults to `CWD/content-kb/`. A user
   running `distill` from the repo root with no `--db` flag will write
   artifacts to `content-kb/` but open the database from `artifacts/content-kb.db`.
   This is functionally correct (they are independent paths, and the design
   explicitly intends two separate trees on disk), but it means the old
   intuition that "artifact root is the folder containing the db file" is gone.
   The concern is that `RunDistillAsync` at line 482-483 resolves both paths
   independently; if a caller passes a non-default `--db` pointing to a
   path inside `content-kb/`, the artifact root will still be `CWD/content-kb/`
   regardless — which is correct — but the diverged defaults leave the two
   directories semantically disconnected with no documentation or comment
   linking them for future maintainers.

2. **INFO (IN-01):** `RunContentIndexExportAsync` (line 534) computes its own
   hardcoded default output path as the bare relative string
   `Path.Combine("content-kb", "seed", "index-seed.json")` — it does NOT call
   `ResolveContentKbArtifactRoot`. This path is CWD-relative but not wrapped
   in `Path.GetFullPath`, so the path printed to the console at line 545 may
   display as a relative path rather than an absolute one, which is a minor
   UX inconsistency. More importantly, if the single-tree consolidation goal is
   extended to this command in the future, the hardcoded default will need a
   separate change — it is not covered by the D-11 fix.

## Warnings

### WR-01: Database default and artifact-root default no longer co-locate; no comment documents the intentional divergence

**File:** `DeckFlow.CLI/CommandRunners.cs:1653-1665`

**Issue:** After the change, the two resolver defaults produce paths in
different directories when MTG_DATA_DIR is unset and no `--db` flag is passed:

- `ResolveContentKbDatabasePath(null)` → `CWD/artifacts/content-kb.db`
- `ResolveContentKbArtifactRoot(null)` → `CWD/content-kb/`

The plan (28-02-PLAN.md task 1) explicitly states
`ResolveContentKbDatabasePath` is "NOT changed; stays artifacts/content-kb.db"
so this divergence is intentional. However, there is no comment at either
method explaining why the db lives in `artifacts/` while artifacts live in
`content-kb/`. A future maintainer reading the two methods side-by-side will
see the mismatch and may "fix" it back to co-location, re-introducing the
drift. The Why comment at line 1664 explains D-11 on the artifact side but
says nothing about the database default.

**Fix:** Add a `// Why:` comment to `ResolveContentKbDatabasePath` (or a
paired note on `ResolveContentKbArtifactRoot`) that names the intentional
split. For example:

```csharp
// Why: database stays in artifacts/ (legacy location, CLI --db flag overrides);
// artifact tree is at repo-root content-kb/ (D-11 / HSK-04). The two directories
// are intentionally separate — do not consolidate without updating both defaults.
private static string ResolveContentKbDatabasePath(FileInfo? db)
    => db?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "content-kb.db");
```

## Info

### IN-01: RunContentIndexExportAsync uses its own hardcoded relative output path, not ResolveContentKbArtifactRoot

**File:** `DeckFlow.CLI/CommandRunners.cs:534`

**Issue:** The default output path for `content-index-export` is computed
inline as `Path.Combine("content-kb", "seed", "index-seed.json")` — a bare
relative path that is not wrapped in `Path.GetFullPath`. This means:

- The path logged to the console (line 545) may print as a relative path,
  making it harder for users to locate the file.
- The command is not covered by the D-11 consolidation; if `ResolveContentKbArtifactRoot`
  is updated again, this command's default will silently drift.

**Fix:** Derive the default output path from `ResolveContentKbArtifactRoot`
for consistency, or at minimum wrap it in `Path.GetFullPath`:

```csharp
// Option A — derive from the canonical resolver (preferred for single-tree consistency):
var outputPath = output?.FullName
    ?? Path.Combine(ResolveContentKbArtifactRoot(db), "seed", "index-seed.json");

// Option B — minimal fix, just ensure absolute path is printed:
var outputPath = output?.FullName
    ?? Path.GetFullPath(Path.Combine("content-kb", "seed", "index-seed.json"));
```

---

_Reviewed: 2026-06-04T19:35:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
