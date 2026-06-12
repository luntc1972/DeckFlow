---
phase: 38-controller-srp-split
plan: 05
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.CLI/ContentKbCliPaths.cs
  - DeckFlow.CLI/CommandRunners.cs
  - DeckFlow.CLI/DeckCommandRunners.cs
  - DeckFlow.CLI/ContentKbCommandRunners.cs
  - DeckFlow.CLI/Program.cs
  - DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs
  - DeckFlow.Core.Tests/BlockedVideoStoreTests.cs
  - DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs
  - DeckFlow.Core.Tests/CommandRunnerCorpusResetTests.cs
  - DeckFlow.Core.Tests/RunDistillAsyncTests.cs
autonomous: true
requirements: [SRP-02]
must_haves:
  truths:
    - "Every CLI command (compare, probe, export-moxfield, archidekt-*, content-*, distill, harvest, block/unblock/list-blocked, corpus-reset, content-index-export, category-find, card-lookup, scryfall-probe) is still registered and invocable"
    - "Deck-domain runners and content-KB runners live in separate classes"
    - "The internal-overload test seam on distill/harvest/block/unblock/list-blocked/corpus-reset is preserved and the 5 CLI test files compile against the new class"
  artifacts:
    - path: "DeckFlow.CLI/ContentKbCliPaths.cs"
      provides: "Shared content-KB path helpers (commit 1)"
      contains: "static class ContentKbCliPaths"
    - path: "DeckFlow.CLI/DeckCommandRunners.cs"
      provides: "Deck-domain runners (compare/probe/export/archidekt/category-find/card-lookup/scryfall-probe)"
      contains: "class DeckCommandRunners"
    - path: "DeckFlow.CLI/ContentKbCommandRunners.cs"
      provides: "Content-KB runners (source add/set-enabled, distill, harvest, block/unblock/list, corpus-reset, index-export)"
      contains: "class ContentKbCommandRunners"
  key_links:
    - from: "DeckFlow.CLI/Program.cs"
      to: "DeckCommandRunners + ContentKbCommandRunners"
      via: "static call targets"
      pattern: "(DeckCommandRunners|ContentKbCommandRunners)\\."
    - from: "DeckFlow.Core.Tests"
      to: "ContentKbCommandRunners"
      via: "test-seam internal overloads"
      pattern: "ContentKbCommandRunners\\.(RunDistillAsync|RunHarvestAsync|RunCorpusResetAsync)"
---

<objective>
Split `DeckFlow.CLI/CommandRunners.cs` (2185 lines) at the deck-domain / content-KB boundary, per SRP-02's explicit two-commit discipline: (commit 1) extract the shared content-KB path helpers into a static `ContentKbCliPaths`, then (commit 2) split the runners into `DeckCommandRunners` (deck-domain) and `ContentKbCommandRunners` (content-KB), update `Program.cs` static call targets, and re-point the 5 `DeckFlow.Core.Tests` files that call the content-KB runners. All CLI commands stay registered and invocable (SC3); the internal-overload test seam is preserved.

Purpose: SRP-02. `CommandRunners` is a god-class mixing deck-compare/export/lookup with the entire content-KB harvest/distill/block pipeline. Splitting at the content-KB boundary gives two focused units.

Output: New `ContentKbCliPaths.cs`, `DeckCommandRunners.cs`, `ContentKbCommandRunners.cs`; `CommandRunners.cs` deleted after its members are redistributed; `Program.cs` + 5 test files re-pointed.

SCOPE NOTE (gap surfaced during planning): CONTEXT D-04 lists the headline runners but is NOT exhaustive. The real `CommandRunners` also contains deck-domain methods `RunCategoryFindAsync`, `RunCardLookupAsync`, `RunScryfallProbeAsync`, `GetCacheDurationSeconds`, `CollectInterestingPaths`, `PrintCard`, `PrintHeaderIfPresent`, and deck-load helpers (`LoadMoxfieldEntriesAsync`, `LoadArchidektEntriesAsync`, `ValidateDeckSize`); plus content-KB methods `RunHarvestAsync` (+ harvest helpers/records/counters), `ParseVideoIds`, and the content-source helpers (`HandleContentSourceUniqueViolationAsync`, `IsValidContentSourceType`, `IsContentSourceUniqueViolation`, `ContentIndexExportRow`, distill validators/counters/consts). The allocation rule below is deterministic: a method goes to ContentKbCommandRunners iff it touches a content-KB store/artifact path (ContentVideoStore, BlockedVideoStore, ContentSourceStore, ContentSiteIndexStore, distill/harvest pipeline, the Resolve*ContentKb* paths); everything else is deck-domain. `ExceptionContains` is content-source-only (used solely by content helpers) -> ContentKbCommandRunners.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/38-controller-srp-split/38-CONTEXT.md

@DeckFlow.CLI/CommandRunners.cs
@DeckFlow.CLI/Program.cs
@DeckFlow.CLI/AssemblyInfo.cs

<interfaces>
Test-seam: DeckFlow.CLI/AssemblyInfo.cs already has [assembly: InternalsVisibleTo("DeckFlow.Core.Tests")] — it covers ANY internal in the DeckFlow.CLI assembly, so the new ContentKbCommandRunners internal overloads remain test-visible with no AssemblyInfo change.

DECK-DOMAIN -> DeckCommandRunners (public statics + deck helpers):
- RunCompareAsync, LoadMoxfieldEntriesAsync, LoadArchidektEntriesAsync, ValidateDeckSize
- RunProbeAsync, RunExportMoxfieldAsync, RunArchidektCategoriesAsync, RunArchidektCategoryCardsAsync
- RunArchidektHarvestRecentAsync, RunArchidektCacheAsync
- RunCategoryFindAsync (calls RunArchidektCacheAsync + GetCacheDurationSeconds), GetCacheDurationSeconds
- RunCardLookupAsync, RunScryfallProbeAsync, CollectInterestingPaths, PrintCard, PrintHeaderIfPresent
  (Note: RunArchidektHarvestRecentAsync is the Archidekt *deck* harvest — NOT the content-KB video harvest; it writes deck knowledge, so it is deck-domain.)

CONTENT-KB -> ContentKbCommandRunners (public statics + internal seam overloads + all content helpers/types):
- RunContentSourceAddAsync, RunContentSourceSetEnabledAsync
- RunDistillAsync (public + internal overload + DistillVideoAsync, MarkSkippedOverCapAsync, GetContentNaturalKey(+Info), ValidateTranscriptLength, ValidateSummary, ValidateClips, CountWords, EstimateTokenCount, DistillCounts, DistillVideoOutcome + distill consts)
- RunBlockVideoAsync (public + internal), RunUnblockVideoAsync (public + internal), RunListBlockedAsync (public + internal)
- RunCorpusResetAsync (public + internal)
- RunContentIndexExportAsync, ContentIndexExportRow
- RunHarvestAsync (public + internal) + HarvestExplicitVideoIdsAsync, HarvestSourceAsync, HarvestVideoAsync, ResolveHarvestVideoIdAsync, PersistTranscriptResultAsync, WarnIfFfmpegUnavailableAsync, MarkFailedIfPossibleAsync, LogFetch, LogFallbackRatio, GetCaptionTrackKind, HarvestVideoResolution, HarvestCounts/TranscriptCounts, ParseVideoIds + harvest consts (ShortVideoMaxDuration etc.)
- HandleContentSourceUniqueViolationAsync, IsValidContentSourceType, IsContentSourceUniqueViolation, ExceptionContains
- SHARED PATHS: ResolveContentKbDatabasePath, ResolveContentKbArtifactRoot -> these become ContentKbCliPaths (commit 1), then ContentKbCommandRunners calls ContentKbCliPaths.* instead of local methods.

Program.cs call sites to re-point (DeckFlow.CLI/Program.cs):
- DeckCommandRunners.*: RunCompareAsync (L157), GetCacheDurationSeconds (L185,236), RunArchidektCacheAsync (L186,237), RunProbeAsync (L211), RunExportMoxfieldAsync (L216), RunArchidektCategoriesAsync (L221), RunArchidektCategoryCardsAsync (L226), RunArchidektHarvestRecentAsync (L231), RunCategoryFindAsync (L242), RunCardLookupAsync (L247), RunScryfallProbeAsync (L252)
- ContentKbCommandRunners.*: RunContentSourceAddAsync (L257), RunContentSourceSetEnabledAsync (L262), RunHarvestAsync + ParseVideoIds (L267), RunBlockVideoAsync (L272), RunUnblockVideoAsync (L277), RunListBlockedAsync (L282), RunCorpusResetAsync (L297), RunDistillAsync + ParseVideoIds (L302), RunContentIndexExportAsync (L307)

5 test files to re-point (all reference content-KB methods only -> ContentKbCommandRunners):
- CommandRunnerValidateClipsTests.cs: CommandRunners.RunDistillAsync
- BlockedVideoStoreTests.cs: CommandRunners.RunBlockVideoAsync / RunListBlockedAsync / RunUnblockVideoAsync
- CommandRunnerHarvestTests.cs: CommandRunners.ParseVideoIds / RunHarvestAsync
- CommandRunnerCorpusResetTests.cs: CommandRunners.RunCorpusResetAsync
- RunDistillAsyncTests.cs: CommandRunners.RunContentSourceSetEnabledAsync / RunDistillAsync
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1 (COMMIT 1): Extract ContentKbCliPaths shared helper</name>
  <read_first>
    - DeckFlow.CLI/CommandRunners.cs (L1930-1943 — ResolveContentKbDatabasePath + ResolveContentKbArtifactRoot; grep all call sites of both)
    - ./CLAUDE.md (LF on existing files; CRLF for NEW files; no reformatting; two-commit discipline)
  </read_first>
  <action>
    Create new file DeckFlow.CLI/ContentKbCliPaths.cs (CRLF — new file). Declare internal static class ContentKbCliPaths in namespace DeckFlow.CLI. Move the two helpers verbatim into it as `public static string ResolveDatabasePath(FileInfo? db)` and `public static string ResolveArtifactRoot(FileInfo? db)` (rename drops the redundant ContentKb prefix since the class name carries it; bodies byte-for-byte including the D-11/HSK-04 Why-comment). Add XML docs.
    In CommandRunners.cs: DELETE the two private path methods, and replace EVERY call site of ResolveContentKbDatabasePath(...) with ContentKbCliPaths.ResolveDatabasePath(...) and ResolveContentKbArtifactRoot(...) with ContentKbCliPaths.ResolveArtifactRoot(...). Grep first to find all ~10 call sites (L426,458,483,484,539,541,542,585,617,643,762 region). Touch only those lines + the deletion; preserve LF; do not reformat.
    This is COMMIT 1: commit with message "refactor(cli): extract ContentKbCliPaths shared path helper" (plain author per project convention, no Co-Authored-By). Build BOTH DeckFlow.CLI and DeckFlow.Core.Tests green before committing.
  </action>
  <acceptance_criteria>
    - ContentKbCliPaths.cs declares internal static class with ResolveDatabasePath + ResolveArtifactRoot.
    - grep -nE "ResolveContentKbDatabasePath|ResolveContentKbArtifactRoot" DeckFlow.CLI/CommandRunners.cs returns NOTHING (definitions + calls all replaced).
    - grep -c "ContentKbCliPaths.Resolve" DeckFlow.CLI/CommandRunners.cs matches the original call-site count (~10).
    - DeckFlow.CLI builds clean AND DeckFlow.Core.Tests builds clean: 0 errors, 0 new warnings.
    - A commit exists with the extract message and only the path-helper changes (git diff scoped to ContentKbCliPaths.cs + CommandRunners.cs).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj 2>&1 | grep -E "error|Build succeeded" | tail -3; grep -cE "ResolveContentKbDatabasePath|ResolveContentKbArtifactRoot" DeckFlow.CLI/CommandRunners.cs</automated>
  </verify>
  <done>ContentKbCliPaths owns both path helpers; CommandRunners calls them; CLI + Core.Tests build clean; commit 1 landed.</done>
</task>

<task type="auto">
  <name>Task 2 (COMMIT 2): Split into DeckCommandRunners + ContentKbCommandRunners, re-point Program.cs and tests</name>
  <read_first>
    - DeckFlow.CLI/CommandRunners.cs (full file — every method, nested type, const; use the deterministic allocation rule from the objective)
    - DeckFlow.CLI/Program.cs (L150-310 — every CommandRunners.* call site)
    - DeckFlow.CLI/ContentKbCliPaths.cs (created in Task 1)
    - DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs, BlockedVideoStoreTests.cs, CommandRunnerHarvestTests.cs, CommandRunnerCorpusResetTests.cs, RunDistillAsyncTests.cs
  </read_first>
  <action>
    Create DeckFlow.CLI/DeckCommandRunners.cs (CRLF) — internal static class DeckCommandRunners in namespace DeckFlow.CLI. Move into it (verbatim bodies, attributes, XML docs) all DECK-DOMAIN members listed in the interfaces block: RunCompareAsync, LoadMoxfieldEntriesAsync, LoadArchidektEntriesAsync, ValidateDeckSize, RunProbeAsync, RunExportMoxfieldAsync, RunArchidektCategoriesAsync, RunArchidektCategoryCardsAsync, RunArchidektHarvestRecentAsync, RunArchidektCacheAsync, RunCategoryFindAsync, GetCacheDurationSeconds, RunCardLookupAsync, RunScryfallProbeAsync, CollectInterestingPaths, PrintCard, PrintHeaderIfPresent. Add only the usings these methods reference.
    Create DeckFlow.CLI/ContentKbCommandRunners.cs (CRLF) — internal static class ContentKbCommandRunners in namespace DeckFlow.CLI. Move into it (verbatim) all CONTENT-KB members listed in the interfaces block, INCLUDING the internal test-seam overloads (RunDistillAsync/RunHarvestAsync/RunBlockVideoAsync/RunUnblockVideoAsync/RunListBlockedAsync/RunCorpusResetAsync internal overloads stay `internal static`), all harvest/distill private helpers, nested records/classes (DistillCounts, DistillVideoOutcome, HarvestCounts, TranscriptCounts, HarvestVideoResolution, ContentIndexExportRow), distill/harvest consts, ParseVideoIds, and the content-source helpers (HandleContentSourceUniqueViolationAsync, IsValidContentSourceType, IsContentSourceUniqueViolation, ExceptionContains). These call ContentKbCliPaths.ResolveDatabasePath/ResolveArtifactRoot (already extracted in Task 1) — keep those calls. Add only the usings these methods reference (DeckFlow.Core.Content, DeckFlow.Core.Knowledge, DeckFlow.Core.Storage, etc. — verify by build).
    DELETE DeckFlow.CLI/CommandRunners.cs entirely (git rm) — all members redistributed. Verify by grep that no member is left behind / dropped: every public+internal method name from the original must appear in exactly one of the two new files.
    Re-point DeckFlow.CLI/Program.cs: change every CommandRunners.X call to DeckCommandRunners.X or ContentKbCommandRunners.X per the call-site map in the interfaces block (e.g. CommandRunners.RunCompareAsync -> DeckCommandRunners.RunCompareAsync; CommandRunners.RunDistillAsync -> ContentKbCommandRunners.RunDistillAsync; CommandRunners.ParseVideoIds -> ContentKbCommandRunners.ParseVideoIds). Touch only the call-target token on each line; preserve all arguments + LF.
    Re-point the 5 test files: in each, replace CommandRunners. with ContentKbCommandRunners. (every referenced method in all 5 files is content-KB — confirm by grep that no DeckCommandRunners method is referenced in these files; if any is, route it correctly). Touch only the qualifier token; preserve LF.
    This is COMMIT 2: commit message "refactor(cli): split CommandRunners into Deck + ContentKb runners". Build DeckFlow.CLI AND DeckFlow.Core.Tests green before committing. (Per project Constraints, VSTest is unreliable in WSL — verification is dotnet build clean on both projects, not a test run.)
  </action>
  <acceptance_criteria>
    - DeckCommandRunners.cs + ContentKbCommandRunners.cs exist; CommandRunners.cs is deleted (git status shows removal).
    - grep -rn "class CommandRunners" DeckFlow.CLI returns NOTHING; grep -rn "CommandRunners\." DeckFlow.CLI/Program.cs returns NOTHING (all re-pointed).
    - All 9 content-KB + 11 deck-domain Program.cs call sites resolve (build proves it). grep counts: DeckFlow.CLI/Program.cs has DeckCommandRunners. and ContentKbCommandRunners. and zero bare CommandRunners.
    - The 5 test files contain ContentKbCommandRunners. and zero CommandRunners. (grep -rc "CommandRunners\." on the 5 files == 0 after qualifying; ContentKbCommandRunners count > 0).
    - DeckFlow.CLI builds clean AND DeckFlow.Core.Tests builds clean: 0 errors, 0 new warnings.
    - Internal seam preserved: grep "internal static" ContentKbCommandRunners.cs finds the RunDistillAsync/RunHarvestAsync/RunBlockVideoAsync/RunUnblockVideoAsync/RunListBlockedAsync/RunCorpusResetAsync overloads.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj 2>&1 | grep -E "error|Build succeeded" | tail -4; grep -rc "CommandRunners\." DeckFlow.CLI/Program.cs; grep -rl "CommandRunners\." DeckFlow.Core.Tests/CommandRunnerValidateClipsTests.cs DeckFlow.Core.Tests/BlockedVideoStoreTests.cs DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs DeckFlow.Core.Tests/CommandRunnerCorpusResetTests.cs DeckFlow.Core.Tests/RunDistillAsyncTests.cs 2>/dev/null | grep -v ContentKb | head; test -f DeckFlow.CLI/CommandRunners.cs && echo "STILL-EXISTS-FAIL" || echo "deleted-ok"</automated>
  </verify>
  <done>CommandRunners split into two focused classes; CommandRunners.cs deleted; Program.cs + 5 test files re-pointed; CLI + Core.Tests build clean; test seam intact; commit 2 landed.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| CLI argv -> command runners | Pre-existing; unchanged. Same commands, same parsing, same file/URL inputs after the split. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-07 | Tampering | CLI command surface (Program.cs registration) | accept | No new attack surface — pure move refactor; the same commands invoke the same logic via re-pointed static targets. Inputs/parsing identical pre/post. |
| T-38-SC | Tampering | npm/pip/cargo installs | accept | No package installs in this plan — zero new dependencies (CLAUDE.md hard constraint). No legitimacy gate needed. |

No new inputs; no auth changes. No HIGH-severity threats.
</threat_model>

<verification>
- DeckFlow.CLI + DeckFlow.Core.Tests both build clean after each commit (two-commit discipline: commit 1 path-helper, commit 2 split).
- No member dropped: every original CommandRunners public/internal method appears in exactly one new class.
- SC3: every command in Program.cs resolves to a runner in one of the two new classes (build proves registration + invocability).
- Test seam preserved: internal overloads remain internal and the 5 Core.Tests files compile against ContentKbCommandRunners.
</verification>

<success_criteria>
- ContentKbCliPaths extracted (commit 1); CommandRunners split into DeckCommandRunners + ContentKbCommandRunners (commit 2).
- Program.cs + 5 test files re-pointed; CommandRunners.cs deleted.
- All CLI commands still registered + invocable; both projects build clean.
</success_criteria>

<output>
Create `.planning/phases/38-controller-srp-split/38-05-SUMMARY.md` when done. Record: the two commit hashes (extract, split), the deck-vs-contentkb method allocation, the Program.cs + test re-point counts, confirmation CommandRunners.cs is deleted, and that the internal test seam is intact. Note the D-04 list was non-exhaustive and the deterministic allocation rule applied to the extra methods.
</output>
