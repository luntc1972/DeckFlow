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
    - "Deck-domain runners and content-KB runners live in separate classes, and EVERY member of the original CommandRunners.cs (every public/internal method and every nested type) is allocated to exactly one of the new classes — none left behind (the original file is deleted)"
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

SCOPE NOTE (gap surfaced during planning + codex review): CONTEXT D-04 lists the headline runners but is NOT exhaustive. The allocation below is the COMPLETE, member-by-member enumeration of the live `CommandRunners.cs` (every public, internal, and private method AND every nested type) — verified against source. The allocation rule is deterministic: a member goes to ContentKbCommandRunners iff it touches a content-KB store/artifact/pipeline (ContentVideoStore, BlockedVideoStore, ContentSourceStore, ContentSiteIndexStore, the distill/harvest pipeline, the Resolve*ContentKb* paths); everything else is deck-domain. `ExceptionContains` is content-source-only (used solely by content helpers) -> ContentKbCommandRunners. `IsTerminalSuccess` is harvest-pipeline only (called inside HarvestVideoAsync over IContentVideoStore) -> ContentKbCommandRunners. The shared path helpers (ResolveContentKbDatabasePath/ResolveContentKbArtifactRoot) are extracted to `ContentKbCliPaths` in commit 1 (the one home for a helper used across content-KB members). After the split NO member of the original CommandRunners.cs may be left unallocated — the file is fully emptied and DELETED (git rm), mirroring the DeckController treatment in Plans 02-04.

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

DECK-DOMAIN -> DeckCommandRunners (EXHAUSTIVE — every deck-domain member of CommandRunners.cs, verified against source):
Public:
- RunCompareAsync (L38), LoadMoxfieldEntriesAsync (L105), LoadArchidektEntriesAsync (L136), ValidateDeckSize (L165)
- ResolveConflicts (L170, public — used by RunCompareAsync at L60; emits the printing-conflict reconciliation set)
- RunProbeAsync (L189), RunExportMoxfieldAsync (L266), RunArchidektCategoriesAsync (L290), RunArchidektCategoryCardsAsync (L323)
- RunArchidektHarvestRecentAsync (L349), RunArchidektCacheAsync (L378)
- RunCategoryFindAsync (L1689, calls RunArchidektCacheAsync + GetCacheDurationSeconds), GetCacheDurationSeconds (L1765)
- RunCardLookupAsync (L1768), RunScryfallProbeAsync (L1807), CollectInterestingPaths (L1727)
Private helpers / nested types:
- BuildProbeRequest (L1867, private — builds the RestRequest for RunScryfallProbeAsync at L1823)
- CreateDeckEntryLoader (L2171, private — the IDeckEntryLoader factory used by the deck-load helpers + ValidateDeckSize + RunArchidektCategoryCardsAsync)
- PrintCard (L2145, private — used by RunCardLookupAsync), PrintHeaderIfPresent (L1897, private — used by RunScryfallProbeAsync)
- ScryfallCardDto (L2178, private record — the card DTO consumed by PrintCard)
  (Note: RunArchidektHarvestRecentAsync is the Archidekt *deck* harvest — NOT the content-KB video harvest; it writes deck knowledge, so it is deck-domain.)

CONTENT-KB -> ContentKbCommandRunners (EXHAUSTIVE — public statics + internal seam overloads + ALL content helpers/types):
Public:
- RunContentSourceAddAsync (L418), RunContentSourceSetEnabledAsync (L448)
- RunDistillAsync (public L472), RunBlockVideoAsync (public L529), RunCorpusResetAsync (public L563), RunUnblockVideoAsync (public L608), RunListBlockedAsync (public L635)
- RunContentIndexExportAsync (L758), RunHarvestAsync (public L998)
Internal test-seam overloads (stay `internal static`):
- RunBlockVideoAsync (L654), RunUnblockVideoAsync (L688), RunCorpusResetAsync (L709), RunListBlockedAsync (L735), RunDistillAsync (L788), RunHarvestAsync (L1051)
- ParseVideoIds (L984, internal — used by harvest + distill)
Harvest pipeline (private):
- HarvestExplicitVideoIdsAsync (L1117), HarvestSourceAsync (L1174), HarvestVideoAsync (L1197), ResolveHarvestVideoIdAsync (L1247), PersistTranscriptResultAsync (L1285), WarnIfFfmpegUnavailableAsync (L1320), MarkFailedIfPossibleAsync (L1331), LogFetch (L1345), LogFallbackRatio (L1353), IsTerminalSuccess (L1361 — harvest-only, called in HarvestVideoAsync over IContentVideoStore), GetCaptionTrackKind (L1364)
Distill pipeline (private):
- DistillVideoAsync (L1374), MarkSkippedOverCapAsync (L1583), GetContentNaturalKey (L1624), ValidateTranscriptLength (L1641), ValidateSummary (L1649), ValidateClips (L1657), CountWords (L1675), EstimateTokenCount (L1686)
Content-source helpers (private):
- HandleContentSourceUniqueViolationAsync (L1906), IsValidContentSourceType (L2005), IsContentSourceUniqueViolation (L2008), ExceptionContains (L2012, content-source-only)
Nested types (all currently private in CommandRunners; carry verbatim):
- HarvestVideoResolution (record L1343), ContentIndexExportRow (record L1945), DistillCounts (class L2041), DistillVideoOutcome (record L2083), HarvestCounts (class L2104)
  (No `TranscriptCounts` type exists in the live file — the prior draft listed it in error; HarvestCounts is the only harvest counter type. Distill/harvest consts that live as fields inside CommandRunners move with their owning class.)
SHARED PATHS: ResolveContentKbDatabasePath (L1930), ResolveContentKbArtifactRoot (L1933) -> these become ContentKbCliPaths (commit 1), then ContentKbCommandRunners calls ContentKbCliPaths.* instead of local methods.

COMPLETENESS INVARIANT: the two lists above cover EVERY member of the original CommandRunners.cs. No member is left behind. At execution time, before deleting CommandRunners.cs, grep every method/type name from the original file and confirm each appears in exactly one of DeckCommandRunners.cs / ContentKbCommandRunners.cs / ContentKbCliPaths.cs (line numbers above are planning-time anchors — re-confirm against the file at execution).
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
    - DeckFlow.CLI builds clean AND DeckFlow.Core.Tests builds clean: 0 errors, 0 new warnings. Capture the warning count BEFORE any edits in this plan (grep `: warning ` on a clean `dotnet build DeckFlow.CLI` of HEAD) and record it as the CLI baseline in 38-05-SUMMARY; the post-commit-1 warning count must not exceed that baseline.
    - A commit exists with the extract message and only the path-helper changes (git diff scoped to ContentKbCliPaths.cs + CommandRunners.cs).
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj 2>&1 | tee /tmp/38-05-cli.log | grep -E "error|Build succeeded" | tail -3; echo "cli-warning-count:"; grep -c ': warning ' /tmp/38-05-cli.log; grep -cE "ResolveContentKbDatabasePath|ResolveContentKbArtifactRoot" DeckFlow.CLI/CommandRunners.cs</automated>
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
    Create DeckFlow.CLI/DeckCommandRunners.cs (CRLF) — internal static class DeckCommandRunners in namespace DeckFlow.CLI. Move into it (verbatim bodies, attributes, XML docs) ALL DECK-DOMAIN members enumerated in the interfaces block, including the private helpers and nested type that the prior draft omitted: RunCompareAsync, LoadMoxfieldEntriesAsync, LoadArchidektEntriesAsync, ValidateDeckSize, ResolveConflicts, RunProbeAsync, RunExportMoxfieldAsync, RunArchidektCategoriesAsync, RunArchidektCategoryCardsAsync, RunArchidektHarvestRecentAsync, RunArchidektCacheAsync, RunCategoryFindAsync, GetCacheDurationSeconds, RunCardLookupAsync, RunScryfallProbeAsync, CollectInterestingPaths, PrintCard, PrintHeaderIfPresent, BuildProbeRequest (private), CreateDeckEntryLoader (private), and the ScryfallCardDto nested record (private). Add only the usings these methods reference.
    Create DeckFlow.CLI/ContentKbCommandRunners.cs (CRLF) — internal static class ContentKbCommandRunners in namespace DeckFlow.CLI. Move into it (verbatim) ALL CONTENT-KB members enumerated in the interfaces block, INCLUDING the internal test-seam overloads (RunDistillAsync/RunHarvestAsync/RunBlockVideoAsync/RunUnblockVideoAsync/RunListBlockedAsync/RunCorpusResetAsync internal overloads stay `internal static`), all harvest/distill private helpers (incl. IsTerminalSuccess), the nested records/classes that actually exist in the file — DistillCounts, DistillVideoOutcome, HarvestCounts, HarvestVideoResolution, ContentIndexExportRow (NOTE: there is NO TranscriptCounts type — the prior draft listed it in error; do not invent one), distill/harvest consts, ParseVideoIds, and the content-source helpers (HandleContentSourceUniqueViolationAsync, IsValidContentSourceType, IsContentSourceUniqueViolation, ExceptionContains). These call ContentKbCliPaths.ResolveDatabasePath/ResolveArtifactRoot (already extracted in Task 1) — keep those calls. Add only the usings these methods reference (DeckFlow.Core.Content, DeckFlow.Core.Knowledge, DeckFlow.Core.Storage, etc. — verify by build).
    DELETE DeckFlow.CLI/CommandRunners.cs entirely (git rm) — ALL members redistributed, file fully emptied, mirroring the DeckController treatment in Plans 02-04. Before deleting, run the COMPLETENESS gate: extract every method + nested-type name from the original CommandRunners.cs (the baseline copy from git HEAD before commit 2) and grep-confirm each appears in EXACTLY ONE of DeckCommandRunners.cs / ContentKbCommandRunners.cs / ContentKbCliPaths.cs. If any name is missing from all three, STOP — a member was dropped; allocate it per the content-KB-store rule before proceeding.
    Re-point DeckFlow.CLI/Program.cs: change every CommandRunners.X call to DeckCommandRunners.X or ContentKbCommandRunners.X per the call-site map in the interfaces block (e.g. CommandRunners.RunCompareAsync -> DeckCommandRunners.RunCompareAsync; CommandRunners.RunDistillAsync -> ContentKbCommandRunners.RunDistillAsync; CommandRunners.ParseVideoIds -> ContentKbCommandRunners.ParseVideoIds). Touch only the call-target token on each line; preserve all arguments + LF.
    Re-point the 5 test files: in each, replace CommandRunners. with ContentKbCommandRunners. (every referenced method in all 5 files is content-KB — confirm by grep that no DeckCommandRunners method is referenced in these files; if any is, route it correctly). Touch only the qualifier token; preserve LF.
    This is COMMIT 2: commit message "refactor(cli): split CommandRunners into Deck + ContentKb runners". Build DeckFlow.CLI AND DeckFlow.Core.Tests green before committing. (Per project Constraints, VSTest is unreliable in WSL — verification is dotnet build clean on both projects, not a test run.)
  </action>
  <acceptance_criteria>
    - DeckCommandRunners.cs + ContentKbCommandRunners.cs exist; CommandRunners.cs is deleted (git status shows removal).
    - grep -rn "class CommandRunners" DeckFlow.CLI returns NOTHING; grep -rn "CommandRunners\." DeckFlow.CLI/Program.cs returns NOTHING (all re-pointed).
    - EXHAUSTIVE ALLOCATION (no member left unallocated): every method + nested-type name from the original CommandRunners.cs (baseline at git HEAD before commit 2) appears in EXACTLY ONE of DeckCommandRunners.cs / ContentKbCommandRunners.cs / ContentKbCliPaths.cs. Concretely: the four previously-omitted members ResolveConflicts, BuildProbeRequest, CreateDeckEntryLoader are present in DeckCommandRunners.cs and the ScryfallCardDto record is present in DeckCommandRunners.cs (grep -c each name across the three files == 1). The completeness gate from the action passed.
    - All 9 content-KB + 11 deck-domain Program.cs call sites resolve (build proves it). grep counts: DeckFlow.CLI/Program.cs has DeckCommandRunners. and ContentKbCommandRunners. and zero bare CommandRunners.
    - The 5 test files contain ContentKbCommandRunners. and zero CommandRunners. (grep -rc "CommandRunners\." on the 5 files == 0 after qualifying; ContentKbCommandRunners count > 0).
    - DeckFlow.CLI builds clean AND DeckFlow.Core.Tests builds clean: 0 errors; warning count from `grep -c ': warning '` must not exceed the CLI baseline recorded in 38-05-SUMMARY (commit 1).
    - Internal seam preserved: grep "internal static" ContentKbCommandRunners.cs finds the RunDistillAsync/RunHarvestAsync/RunBlockVideoAsync/RunUnblockVideoAsync/RunListBlockedAsync/RunCorpusResetAsync overloads.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj 2>&1 | tee /tmp/38-05-t2.log | grep -E "error|Build succeeded" | tail -4; echo "test-warning-count:"; grep -c ': warning ' /tmp/38-05-t2.log; echo "previously-omitted members (each must be 1 across the new files):"; for m in ResolveConflicts BuildProbeRequest CreateDeckEntryLoader ScryfallCardDto; do echo -n "$m="; grep -hc "$m" DeckFlow.CLI/DeckCommandRunners.cs DeckFlow.CLI/ContentKbCommandRunners.cs DeckFlow.CLI/ContentKbCliPaths.cs 2>/dev/null | paste -sd+ | bc; done; grep -rc "CommandRunners\." DeckFlow.CLI/Program.cs; test -f DeckFlow.CLI/CommandRunners.cs && echo "STILL-EXISTS-FAIL" || echo "deleted-ok"</automated>
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
Create `.planning/phases/38-controller-srp-split/38-05-SUMMARY.md` when done. Record: the CLI warning baseline (captured before commit 1), the two commit hashes (extract, split), the EXHAUSTIVE deck-vs-contentkb member allocation (every method + nested type, confirming none left behind — explicitly note ResolveConflicts/BuildProbeRequest/CreateDeckEntryLoader/ScryfallCardDto landed in DeckCommandRunners and IsTerminalSuccess in ContentKbCommandRunners), the Program.cs + test re-point counts, confirmation CommandRunners.cs is deleted, the post-split warning count vs baseline, and that the internal test seam is intact. Note the D-04 list was non-exhaustive and the deterministic content-KB-store allocation rule applied to every additional member.
</output>
