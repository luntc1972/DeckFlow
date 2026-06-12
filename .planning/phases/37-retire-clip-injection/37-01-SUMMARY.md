# 37-01 Summary

## What Was Built / Removed

- Removed the deck-analysis Content KB clip-injection path and expert-selection threading from prompt builders, `DeckAnalysisPacketService`, request/view models, packet zip write/read paths, the DeckAnalysis page, and the related API/service/model types.
- Removed the admin relevance-score preview surface so `IContentKbRelevanceService` could be deleted in the same build-green wave while keeping the curation grid, visibility actions, evergreen toggle, and reload-seed flow intact.
- Removed the `/content-kb` browse-page selection tray, pin/follow buttons, and `kb-selection` script while keeping browse search/filter/listing intact.
- Added RET-01 regression coverage in `AnalysisPromptVariantNoExpertContextTests.cs` and RET-05 legacy zip load coverage in `PacketLegacyZipBackCompatTests.cs`.

## Files Changed

- Production edits/deletes stayed within Plan 37-01's fenced file set.
- Deleted orphaned production files:
  `ContentKbRelevanceService.cs`, `ContentKbArchetypeDeriver.cs`, `ContentKbClipSanitizer.cs`, `ContentKbExcerpt.cs`, `ContentKbSearchApiController.cs`, `_ContentKbPanel.cshtml`, `kb-selection.ts`, `content-kb-admin.ts`.
- Added new regression tests:
  `DeckFlow.Web.Tests/AnalysisPromptVariantNoExpertContextTests.cs`,
  `DeckFlow.Web.Tests/PacketLegacyZipBackCompatTests.cs`.
- Pruned the retired injection test suite and updated incidental tests (`DeckControllerTests`, `DeckAnalysisPacketServiceTests`, `DeckAnalysisRequestTests`, `PacketArtifactStoreTests`, `AiPlatformExtensionTests`, `AdminContentKbControllerTests`).

## Verification

- Build:
  `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`
  Result: `Build succeeded.`
  `0 Warning(s)`
  `0 Error(s)`
- Acceptance checks:
  `AnalysisPromptVariantNoExpertContextTests.cs` contains 3 negative `DoesNotContain("## Expert Context", ...)` assertions.
  `PacketLegacyZipBackCompatTests.cs` exercises legacy `32-expert-context.json` and `33-expert-selection.json` load without throw.
  Greps for removed prompt/admin/browse/injection symbols returned nothing.
  `test -f` checks for deleted TS/test files returned false as expected.
  Final solution-wide sweep returned nothing.
  `git diff --stat` before commit showed no tracked edits outside the fenced set.

## Deviations

- The requested `/home/clunt/.claude/CLAUDE.md-equivalent` path did not exist in the environment, so execution used the repo `CLAUDE.md` plus the phase docs.
- The worktree was not initially clean due unrelated untracked paths (`.gstack/`, `.superpowers/`, `SECURITY.md`); they were left untouched and unstaged.

## Requirement Coverage

- `RET-01`: satisfied by removing `## Expert Context` from all three analysis prompt variants and removing the DeckAnalysis expert UI plus browse-page selection UI.
- `RET-02`: satisfied by deleting the retired injection types/consumers, removing admin score preview, and building clean at `0/0` with a clean removal sweep.
- `RET-05`: satisfied by keeping legacy packet zip load tolerant of `32-`/`33-` entries and covering that path with `PacketLegacyZipBackCompatTests`.
