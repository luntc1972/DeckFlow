---
phase: 22-content-kb-site-integration
plan: 02
task_scope: task-1-only
subsystem: content-kb-cli-seed
tags: [content-kb, cli, seed-json]

requires: [22-01]
provides:
  - content-index-export CLI verb
  - RunContentIndexExportAsync using the CLI/Core ContentSiteIndexStore layer
  - committed content-kb/seed/index-seed.json with 10 index-only rows
affects: [content-kb-seed-loader, content-kb-public-browse]

tech-stack:
  added: []
  patterns: [CLI/Core store composition, whitelist JSON export DTO, committed seed artifact]

key-files:
  created:
    - content-kb/seed/index-seed.json
    - .planning/phases/22-content-kb-site-integration/22-02-SUMMARY.md
  modified:
    - DeckFlow.CLI/Program.cs
    - DeckFlow.CLI/CommandRunners.cs

requirements-completed: [KB-08]
completed: 2026-06-02
---

# Phase 22: Content KB Site Integration Plan 02 Summary

## Scope

Task 1 only was implemented. Tasks 2-4 remain pending behind the human-approval checkpoint; no `.gitignore`, `.dockerignore`, `Dockerfile`, `content-kb/README.md`, or `content-kb/{source-slug}/` artifact-copy edits were made.

## What Built

- Added the `content-index-export` CLI verb with `--db` and `--output`.
- Added `RunContentIndexExportAsync(FileInfo? db, FileInfo? output)` using `ResolveContentKbDatabasePath(db)` and `new ContentSiteIndexStore(dbPath)`.
- Export rows are serialized through a whitelist DTO containing only `naturalKeyType`, `naturalKeyValue`, `source`, `title`, `videoUrl`, `artifactPath`, `publishedUtc`, `indexedUtc`, `archetypeTags`, `bracketTags`, and `cardCategoryTags`.
- Added `content-kb/seed/index-seed.json` from the 10 existing artifact frontmatters under `artifacts/content-kb/**`. The source artifacts had no `publishedUtc` frontmatter field, so the seed uses `null` for `publishedUtc`.

## Delivery Decisions Recorded

- Delivery route for later tasks: Dockerfile explicit `COPY content-kb/ ./content-kb/`.
- Realized runtime artifact directory: `/app/content-kb`.
- Resolver base for Plan 03: `ContentRootPath`; seed `artifactPath` values keep the `content-kb/` prefix.
- Docker image `ls /app/content-kb` verification did not run in Task 1 because Task 3 delivery edits are still blocked by the human checkpoint.

## Verification

- Pre-change red check: `dotnet run --project DeckFlow.CLI/DeckFlow.CLI.csproj -- --help | rg "content-index-export"` exited 1 before implementation.
- CLI build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj -c Debug` passed with 0 warnings and 0 errors.
- Green command check: `dotnet run --project DeckFlow.CLI/DeckFlow.CLI.csproj -- --help | rg "content-index-export"` found the new verb.
- Runner grep: `RunContentIndexExportAsync` uses `ResolveContentKbDatabasePath(db)` and `new ContentSiteIndexStore(dbPath)`.
- Web-layer exclusion grep: no `DeckFlowDatabaseConnectionFactory` or `IWebHostEnvironment` references in `DeckFlow.CLI/Program.cs` or `DeckFlow.CLI/CommandRunners.cs`.
- Seed assertion: .NET 10 file-based C# check with `System.Text.Json` passed, confirming 10 objects, no forbidden keys (`transcript`, `audio`, `spend`, `isVisible`, `is_visible`), only allowed keys, and every `artifactPath` starts with `content-kb/`.

## Pending Checkpoint

Task 2 must still obtain human approval before Task 3 touches `.gitignore`, `.dockerignore`, `Dockerfile`, copies markdown artifacts into `content-kb/{source-slug}/`, or adds `content-kb/README.md`.
