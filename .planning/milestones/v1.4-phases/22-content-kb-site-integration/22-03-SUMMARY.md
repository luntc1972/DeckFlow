---
phase: 22-content-kb-site-integration
plan: 03
task_scope: tasks-1-2-3-complete
subsystem: public-content-kb-site
tags: [content-kb, public-browse, artifact-detail, seed-loader, feature-flag]

requires: [22-01, 22-02]
provides:
  - flag-gated public Content KB browse page
  - id-keyed artifact detail page with content-kb subtree guards
  - ordered content base resolver
  - curation-preserving startup seed loader
  - local facet filtering and copy-for-ChatGPT behavior
  - conditional public nav link
affects: [content-kb-public-surface, content-site-index-startup]

key-files:
  created:
    - DeckFlow.Web/Services/ContentArtifactParser.cs
    - DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs
    - DeckFlow.Web/Services/IContentKbSeedLoader.cs
    - DeckFlow.Web/Services/ContentKbSeedLoader.cs
    - DeckFlow.Web/Controllers/ContentKbController.cs
    - DeckFlow.Web/Models/ContentKbBrowseViewModel.cs
    - DeckFlow.Web/Models/ContentKbDetailViewModel.cs
    - DeckFlow.Web/Views/ContentKb/Index.cshtml
    - DeckFlow.Web/Views/ContentKb/Detail.cshtml
    - DeckFlow.Web/Views/ContentKb/_ViewStart.cshtml
    - DeckFlow.Web/wwwroot/ts/content-kb.ts
  modified:
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web/Services/HelpContentService.cs
    - DeckFlow.Web/Views/Shared/_Layout.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css

requirements-completed: [KB-08, KB-09]
completed: 2026-06-02
---

# Phase 22: Content KB Site Integration Plan 03 Summary

## Scope

Implemented Tasks 1, 2, and 3 only. Task 4 remains a pending human UAT checkpoint.

No `DeckFlow.Core/*`, `DeckFlow.CLI/*`, `site.css`, or theme-fork CSS files were edited.

## What Built

- Added `ContentArtifactParser` and updated `HelpContentService` to delegate frontmatter splitting to it; no SplitHeader duplication remains.
- Added `ContentKbArtifactPathResolver` with the locked ordered candidates and one-line chosen-base logging.
- Added `IContentKbSeedLoader` / `ContentKbSeedLoader`, loading `content-kb/seed/index-seed.json` with `Id = 0` rows and `UpsertRowPreservingVisibilityAsync`.
- Wired `IContentSiteIndexStore`, resolver, and seed loader before `builder.Build()`, then ensured schema and loaded the seed in the post-build startup block.
- Added `ContentKbController` with `[FeatureFlagGate("content.kb.enabled")]` on both actions, published-only browse, id-keyed detail, and the D-22F subtree guards before filesystem access.
- Added browse/detail view models and Razor views, local facet filtering, local clipboard copy, the four public `.kb-` class names in `site-common.css`, and a conditional "Knowledge Base" nav link.

## Runtime Notes

- Local dev server was not run, so no resolved `ContentBase` log line was observed in local dev logs.
- Expected local resolver choice for this workspace is the repo root (`/mnt/c/users/chrislunt/source/personal/deckflow`) because it contains `content-kb/`.
- Task 4 is still pending for human verification of flag-off 503, published browse rows, detail rendering/copy, 375px behavior, and hidden/missing negative cases.

## Verification

- Task 1 build: `Build succeeded.`
- Task 1 greps: registration-region `app.Environment` -> `0`; resolver `ContentRootPath` -> `5`, `Directory.Exists` -> `2`, `LogInformation` -> `1`; seed loader `UpsertRowPreservingVisibilityAsync` -> `1`, `UpsertRowAsync` -> `0`, `Id = 0` -> `1`; Program.cs `LoadIfPresentAsync` -> `1`.
- Task 2 build: `Build succeeded.`
- Task 2 greps: `FeatureFlagGate` -> `2`; `{id:long}` -> `1`; `StartsWith("content-kb/"` -> `1`; `Path.Combine(.*ContentBase.*content-kb` -> `1`; total `StartsWith` -> `2`.
- Task 2 source-order check: hidden/null row guard and `content-kb/` artifact path guard appear before `File.Exists` and `ReadAllTextAsync`.
- Task 3 build: `Build succeeded.`
- Task 3 emitted `DeckFlow.Web/wwwroot/js/content-kb.js`.
- Task 3 greps: `navigator.clipboard.writeText` -> `1`; `card-lookup` in `content-kb.ts` -> `0`; `kb-empty` in browse view -> `2`; `Clear filters` -> `1`; layout `IsEnabled("content.kb.enabled")` -> `1`.
- Theme bleed check: the literal glob returns only the allowed `DeckFlow.Web/wwwroot/css/site-common.css`; excluding that allowed file, `.kb-` matches in `site.css` and theme forks -> no output.
- Unique `.kb-` class names in `site-common.css`: `.kb-artifact-prose`, `.kb-empty`, `.kb-filter-bar`, `.kb-tag`.
