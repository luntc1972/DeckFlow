# Phase 14 Audit Report

**Generated:** 2026-05-17 by Plan 14-01
**HEAD at generation:** 421108589f91712967ba9ab2420a14c59357c9cd
**Branch:** v1.3

Plans 14-02 and 14-03 read this file as their sole input. No re-discovery needed.

---

## Renames (executed in Plan 14-02)

### Production code

1. `DeckFlow.Web/Services/ScryfallTaggerService.cs` :: `ScryfallTaggerService` → `ScryfallTaggerLookupService` — name describes responsibility #2 only (Tagger GraphQL); class also does Scryfall REST card resolution (resp. #1) and CSRF session lookup + kill-switch enforcement (resp. #3); loose-trigger D-02 rename to most descriptive single name; responsibility split deferred (see `## Deferred` below)

**Manual review — no rename:**
- `CommanderSpellbookService` — name accurately describes responsibility; talks to Commander Spellbook (combo endpoint + Moxfield-fallback endpoint on same backend); `<summary>` at line 52 is accurate as-is; no rename per RESEARCH.md verdict

**Smell-grep results — no renames required for Smell 2 no-HTTP services:**
- `HelpContentService` — no HTTP by design; loads/caches Markdown from disk; name accurate
- `VersionService` — no HTTP by design; reads assembly metadata; name accurate
- `JsonTextFormatterService` — no HTTP by design; static text parser; name accurate (`Service` suffix is loose but acceptable)
- `CommanderCategoryService` — no HTTP by design; reads knowledge cache; name accurate
- `ArchidektCacheJobService` — no HTTP direct call (delegates to `IArchidektRecentDecksImporter`); name accurate as hosted background cache job
- `CategorySuggestionService` — no HTTP direct call (delegates to sub-services); name accurate as router/coordinator
- `DeckSyncService` — no HTTP direct call (delegates to deck importers); name accurate

**Smell 3 — no hits:** No `*Client` class uses app-scoped state (IMemoryCache, ITaggerSessionCache, etc.)

**Smell 4 — no renames from summary audit:** Multi-responsibility summaries found in `DeckAnalysisPacketService` and `DeckComparisonService`; these classes legitimately span multiple responsibilities — the summaries describe what the class does, not a naming error. Deferred to responsibility-split milestone.

**Smell 5 — file-vs-type-name allowlist (co-located interface+impl pattern per CONVENTIONS.md):**
All Smell 5 hits are instances of the canonical CONVENTIONS.md co-located pattern:
- `CommanderSpellbookService.cs` → primary type is `SpellbookCombo` (first public record in file); file is named for the primary service, not the first record. This is the established multi-type-per-file pattern. No rename.
- `DeckConvertService.cs` → primary type `DeckConvertResult` is a result record co-located with the service. No rename.
- `ScryfallCommanderSearchService.cs` → primary grep type is `ICommanderSearchService` (interface first); file is named for the implementation. No rename.
- `ScryfallDtos.cs` → multi-type DTO file; named for its purpose (Scryfall data transfer objects). No rename.
- `TaggerSessionCache.cs` → primary type is `TaggerSession` (session model); file named for the cache. No rename.
- `DeckImporterInterfaces.cs` → multi-interface file; named for its purpose. No rename.
- `AnalysisQuestionCatalog.cs`, `CommanderBracketCatalog.cs`, `WorkflowStepTabsModel.cs` → multi-type model files; named for primary catalog/model concept. No rename.
- `AdminFeedbackController.cs`, `AdminFlagsController.cs` → ViewModel co-located with controller; standard MVC pattern. No rename.
- `HarvestRunModels.cs`, `HarvestStatsModels.cs` → multi-model files for the Harvest subsystem; named for the domain concept. No rename.
- `SuggestionResponses.cs` → multi-DTO file for suggestion API responses. No rename.

---

### Test doubles (D-05 canonicalization)

**Execution order constraint:** Row (a) MUST execute BEFORE row (c). A `FakeMetaGapService`
already exists at L810; renaming L831 to `FakeMetaGapService` before L810 is renamed will
cause a compile-time duplicate class name error. Rows (b), (d)–(i) are order-independent.

| Row | File:line | Old name | New name | Taxonomy rationale |
|-----|-----------|----------|----------|--------------------|
| (a) | `DeckFlow.Web.Tests/DeckControllerTests.cs:810` | `FakeMetaGapService` | `StubMetaGapService` | Canned-response semantics: returns hardcoded `MetaGapResult` regardless of input; per CONVENTIONS.md canned-response = Stub. Preliminary rename frees `FakeMetaGapService` name for row (c). |
| (b) | `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs:144` | `NullTempDataProvider` | `StubTempDataProvider` | No-op fallback stub — returns empty/default and does nothing; Stub* per CONVENTIONS.md |
| (c) | `DeckFlow.Web.Tests/DeckControllerTests.cs:831` | `ConfigurableMetaGapService` | `FakeMetaGapService` | Configurable = stateful fake; takes `MetaGapResult` in ctor and returns it; Fake* per CONVENTIONS.md. Depends on row (a) completing first. |
| (d) | `DeckFlow.Web.Tests/DeckControllerTests.cs:870` | `CapturingDeckAnalysisPacketService` | `FakeDeckAnalysisPacketService` | State-capture = stateful fake; records call arguments for assertion; document capture semantics in new `<summary>` |
| (e) | `DeckFlow.Web.Tests/DeckControllerTests.cs:939` | `SuccessfulCardLookupService` | `StubSuccessfulCardLookupService` | Returns fixed successful payload per call (no state); Stub semantics. Preserves "Successful" qualifier to disambiguate from existing `FakeCardLookupService` at L914. |
| (f) | `DeckFlow.Web.Tests/DeckControllerTests.cs:948` | `SuccessfulSingleCardLookupService` | `StubSuccessfulSingleCardLookupService` | Same Stub rationale; preserves "Successful" qualifier |
| (g) | `DeckFlow.Web.Tests/DeckControllerTests.cs:987` | `SuccessfulMechanicLookupService` | `StubSuccessfulMechanicLookupService` | Same Stub rationale |
| (h) | `DeckFlow.Web.Tests/CommanderControllerTests.cs:117` | `DummyCommanderSearchService` | `StubCommanderSearchService` | No-op stub: body returns empty/canned results without state; Stub* per CONVENTIONS.md |
| (i) | `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs:116` | `FailingRecentDecksImporter` | `ThrowingRecentDecksImporter` | Exception injection; matches existing `Throwing*` taxonomy (e.g., `ThrowingCardSearchService`) |

**Total: 9 renames** (8 non-canonical prefix hits + 1 preliminary collision-resolution rename for L810).

---

## Allowlist

**`DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs`** — NOT a rename target. This is a
legitimate test-only factory pattern (creates configured service instances for test fixtures),
not a test double. The `Test` prefix here scopes the factory to the test assembly; it does
not communicate stub/fake/throwing semantics. Internal modifier keeps it scoped to test assembly.

---

### Name-collision notes

**Collision 1 — `FakeCardLookupService` at L914:**
`DeckControllerTests.cs:914` already has `private sealed class FakeCardLookupService : ICardLookupService`
(no-op stub behavior — returns `null`/empty). Renaming `SuccessfulCardLookupService` (L939),
`SuccessfulSingleCardLookupService` (L948), or `SuccessfulMechanicLookupService` (L987) to
`FakeCardLookupService` would create a duplicate class name in the same outer class.
**Resolution:** Use `Stub` prefix + preserve "Successful" qualifier (rows e/f/g above). This
correctly reflects the canned-response semantics AND avoids the name collision.

**Collision 2 — `FakeMetaGapService` at L810 vs `ConfigurableMetaGapService` at L831:**
`DeckControllerTests.cs:810` has `private sealed class FakeMetaGapService` (canned-response =
mis-prefixed; should be Stub). `DeckControllerTests.cs:831` has `ConfigurableMetaGapService`
(stateful fake = correctly described by Fake prefix). Renaming L831 to `FakeMetaGapService`
without first renaming L810 would create two `FakeMetaGapService` classes in the same outer
class — compile error.

**Resolution: Option A (from RESEARCH.md "Naming-collision risk" section):**
- Step 1 (row a): rename L810 `FakeMetaGapService` → `StubMetaGapService` (correct taxonomy:
  canned-response = Stub)
- Step 2 (row c): rename L831 `ConfigurableMetaGapService` → `FakeMetaGapService` (now the
  name is free; correct taxonomy: configurable = stateful Fake)

Result: `StubMetaGapService` (L810, canned), `FakeMetaGapService` (L831, configurable),
`ThrowingMetaGapService` (L844, unchanged) — clean three-pattern Stub/Fake/Throwing set.

Option B (qualifier-preserving `ConfigurableMetaGapService` → `FakeConfigurableMetaGapService`)
was rejected: it preserves a known-wrong prefix (`Configurable`) in the canonical Fake name
and counts as a deferred-cleanup smell. Option A is one extra rename for full semantic
correctness — worth it for Phase 14 scope.

---

### Discretionary additions to Plan 14-03 backfill

**DeckPageTab enum (`DeckFlow.Web/Models/DeckPageTab.cs`) — OPT-IN to summaries:**
`DeckPageTab` carries zero summaries (intentional under Phase 13 Pattern 7 per 13-VERIFICATION.md SC2).
`DeckFlow.Web.csproj` `NoWarn 1591` still covers it (CONTEXT.md D-04 leaves Web.csproj NoWarn intact;
D-04 does NOT remove the suppression). However, per Phase 14 D-03 scope (every public class +
interface across 5 projects) and D-02 loose trigger (any reader benefits), the decision is:

**Opt-in: add 5 one-line summaries (enum type + all 11 values)** in Plan 14-03.

Values at HEAD: `Sync`, `SuggestCategories`, `CommanderCategories`, `CardLookup`, `MechanicLookup`,
`DeckAnalysis`, `Convert`, `DeckComparison`, `CedhMetaGap`, `Home`, `JudgeQuestions` (11 values).

Rationale: Phase 14's doc-backfill pass touches every public type; adding 5-line summaries to this
small enum is cheap and consistent. The `NoWarn 1591` in Web.csproj means the build won't fail
without these summaries, but the XML coverage diff (Plan 14-04 gate) will flag the gap.

---

## Doc-comment backfill targets (executed in Plan 14-03)

Derived from `14-BASELINE-PUBLIC-TYPES.txt` + missing-summary grep. Plan 14-03 executor
reads this list and adds `/// <summary>` to each file's type declaration.

**Style anchor:** `DeckFlow.Web/Services/CardLookupService.cs:39-42` (class) +
`DeckFlow.Web/Services/CommanderSpellbookService.cs:37-40` (interface). One sentence,
present-tense verb-leading, 6-15 words. Test-class anchor:
`DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:13-17` (1-2 sentences describing
behaviors covered, cross-referencing the type under test with `<see cref="X"/>`).

**DO NOT touch `{ get; init; }` accessors.** CLAUDE.md constraint: IDE may auto-convert
to `{ get; }` which silently breaks `JsonSerializer` deserialization. Run the
`{ get; init; }` preservation gate before every commit (see `## Plan 14-02 per-commit
{ get; init; } preservation gate` below — same gate applies to Plan 14-03).

### DeckFlow.Core (37 files missing type-level summary)

`DeckFlow.Core/Diffing/DiffEngine.cs` — `DiffEngine` class
`DeckFlow.Core/Exporting/MoxfieldTextExporter.cs` — `MoxfieldTextExporter` static class
`DeckFlow.Core/Exporting/FullImportExporter.cs` — `FullImportExporter` static class
`DeckFlow.Core/Exporting/DeltaExporter.cs` — `DeltaExporter` static class
`DeckFlow.Core/Filtering/DeckEntryFilter.cs` — `DeckEntryFilter` static class
`DeckFlow.Core/Integration/ArchidektApiUrl.cs` — `ArchidektApiUrl` static class
`DeckFlow.Core/Integration/DeckImporterInterfaces.cs` — `MoxfieldImportResult` record + `IMoxfieldDeckImporter` + `IArchidektDeckImporter` interfaces
`DeckFlow.Core/Integration/MoxfieldApiUrl.cs` — `MoxfieldApiUrl` static class
`DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` — `MoxfieldApiDeckImporter` class
`DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` — `IArchidektRecentDecksImporter` interface + `ArchidektRecentDecksImporter` class
`DeckFlow.Core/Integration/EdhrecCardLookup.cs` — `EdhrecCardLookup` partial class
`DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — `ArchidektApiDeckImporter` class
`DeckFlow.Core/Knowledge/BoardCategoryComparer.cs` — `BoardCategoryComparer` class
`DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — `ArchidektDeckCacheSession` class
`DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — `CategoryKnowledgeRepository` class
`DeckFlow.Core/Loading/DeckLoadRequest.cs` — `DeckLoadRequest` record/class
`DeckFlow.Core/Models/DeckDiff.cs` — `DeckDiff` record (preserve `{ get; init; }`)
`DeckFlow.Core/Models/DeckEntry.cs` — `DeckEntry` record (preserve `{ get; init; }`)
`DeckFlow.Core/Models/LoadedDecks.cs` — `LoadedDecks` record (preserve `{ get; init; }`)
`DeckFlow.Core/Models/PrintingConflict.cs` — `PrintingConflict` record (preserve `{ get; init; }`)
`DeckFlow.Core/Parsing/IParser.cs` — `IParser` interface
`DeckFlow.Core/Parsing/DeckParseException.cs` — `DeckParseException` class
`DeckFlow.Core/Parsing/MoxfieldParser.cs` — `MoxfieldParser` class
`DeckFlow.Core/Parsing/ArchidektParser.cs` — `ArchidektParser` class
`DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` — `CategorySuggestionReporter` class
`DeckFlow.Core/Reporting/CategoryFilter.cs` — `CategoryFilter` class/record
`DeckFlow.Core/Reporting/CategoryInferenceReporter.cs` — `CategoryInferenceReporter` class
`DeckFlow.Core/Reporting/CategoryCountReporter.cs` — `CategoryCountReporter` class
`DeckFlow.Core/Reporting/CardDeckTotals.cs` — `CardDeckTotals` record
`DeckFlow.Core/Reporting/CategoryCardReporter.cs` — `CategoryCardReporter` class
`DeckFlow.Core/Reporting/DeckCategoryEntry.cs` — `DeckCategoryEntry` record
`DeckFlow.Core/Reporting/CategoryKnowledgeReporter.cs` — `CategoryKnowledgeReporter` class
`DeckFlow.Core/Reporting/ReconciliationReporter.cs` — `ReconciliationReporter` class
`DeckFlow.Core/Storage/IRelationalDialect.cs` — `IRelationalDialect` interface
`DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — `RelationalDatabaseConnection` class
`DeckFlow.Core/Storage/SqliteRelationalDialect.cs` — `SqliteRelationalDialect` class
`DeckFlow.Core/Storage/PostgresRelationalDialect.cs` — `PostgresRelationalDialect` class

### DeckFlow.Web (targeted — see note)

Per CONTEXT.md "Deferred Ideas": removing `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj` is
a future hygiene phase. The v1.1-era undocumented public types in Web remain out of Plan 14-03
scope EXCEPT:

1. **Types renamed in Plan 14-02** — `ScryfallTaggerLookupService` (renamed from `ScryfallTaggerService`)
   gets `<summary>` in lockstep with the rename commit in Plan 14-02. Do NOT list here; handled
   by Plan 14-02.
2. **`DeckFlow.Web/Models/DeckPageTab.cs`** — opted-in above; backfill enum type + all 11 values.

### DeckFlow.CLI (0 files)

No public types in `DeckFlow.CLI/`. `GenerateDocumentationFile=true` flip is still valuable
(future-proofs any new public type added to CLI) but no backfill work today.

### DeckFlow.Core.Tests (9 files missing type-level summary)

`DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs`
`DeckFlow.Core.Tests/FilteringTests.cs`
`DeckFlow.Core.Tests/EdhrecLookupTests.cs`
`DeckFlow.Core.Tests/DiffEngineTests.cs`
`DeckFlow.Core.Tests/DeckCategoryCacheWriterTests.cs`
`DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs`
`DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs`
`DeckFlow.Core.Tests/ParserTests.cs`
`DeckFlow.Core.Tests/ReportingTests.cs`

(Note: `DeckFlow.Core.Tests/ExporterTests.cs` already has a summary — correctly excluded.)

### DeckFlow.Web.Tests (37 files missing type-level summary)

`DeckFlow.Web.Tests/AboutControllerTests.cs`
`DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs`
`DeckFlow.Web.Tests/AnalysisQuestionCatalogTests.cs`
`DeckFlow.Web.Tests/ArchidektCacheJobServiceTests.cs`
`DeckFlow.Web.Tests/ArchidektCacheJobsControllerTests.cs`
`DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs`
`DeckFlow.Web.Tests/CardLookupIntegrationTests.cs`
`DeckFlow.Web.Tests/CardLookupServiceTests.cs`
`DeckFlow.Web.Tests/CardSearchServiceTests.cs`
`DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs`
`DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs`
`DeckFlow.Web.Tests/CommanderBanListServiceTests.cs`
`DeckFlow.Web.Tests/CommanderControllerTests.cs`
`DeckFlow.Web.Tests/DeckControllerTests.cs`
`DeckFlow.Web.Tests/DeckFlowDatabaseConnectionFactoryTests.cs`
`DeckFlow.Web.Tests/EdhTop16ClientTests.cs`
`DeckFlow.Web.Tests/Extensions/HarvestServiceCollectionExtensionsTests.cs`
`DeckFlow.Web.Tests/FeatureFlagGateAttributeTests.cs`
`DeckFlow.Web.Tests/FeedbackControllerTests.cs`
`DeckFlow.Web.Tests/FeedbackStoreTests.cs`
`DeckFlow.Web.Tests/HelpContentServiceTests.cs`
`DeckFlow.Web.Tests/HelpControllerTests.cs`
`DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs`
`DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs`
`DeckFlow.Web.Tests/MechanicLookupServiceTests.cs`
`DeckFlow.Web.Tests/ScryfallSetServiceTests.cs`
`DeckFlow.Web.Tests/ScryfallTaggerParsersTests.cs`
`DeckFlow.Web.Tests/ScryfallThrottleTests.cs`
`DeckFlow.Web.Tests/Security/AdminBruteForceTrackerStoreTests.cs`
`DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs`
`DeckFlow.Web.Tests/Services/DeckFlowDatabaseConnectionFactoryPostgresUriTests.cs`
`DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs`
`DeckFlow.Web.Tests/SuggestionsApiControllerTests.cs`
`DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs`
`DeckFlow.Web.Tests/TestDoubles/StubHttpMessageHandler.cs`
`DeckFlow.Web.Tests/UpstreamErrorMessageBuilderTests.cs`
`DeckFlow.Web.Tests/VersionServiceTests.cs`

**Note:** Test-double classes renamed in Plan 14-02 (rows a–i) get `<summary>` in lockstep
with the rename commit. Do NOT list them separately in Plan 14-03 to avoid duplicate work.
The file-level `/// <summary>` on the outer test class (`DeckControllerTests`, etc.) is Plan
14-03's responsibility; the inner private class summaries are Plan 14-02's responsibility.

---

## XML Coverage Diff (AUDIT-03 verification mechanism)

**This section OVERRIDES CONTEXT.md D-04's broken warning-gate assumption.**

**Finding:** `.editorconfig` lines 93-96 (committed `0f38cce`, 2026-05-17) set
`dotnet_diagnostic.CS1591.severity = none`, `CS1573.severity = none`, `CS1587.severity = none`
repo-wide. Flipping `GenerateDocumentationFile=true` in 4 csprojs produces zero CS1591/1573/1587
warnings even with deliberately-missing summaries — the editorconfig wins over csproj setting.
The AUDIT-03 build-gate cannot rely on missing-doc warnings as the correctness signal.

**Verification mechanism: Option A (3-step coverage diff script)**

Run this at the end of Plan 14-04, after flipping `GenerateDocumentationFile=true` in all 4
newly-enabled csprojs and running a clean Release build:

```bash
# Step 1: Extract every public type from source
grep -rEn "^[[:space:]]*public +(sealed +)?(class|interface|record) +([A-Z][A-Za-z0-9_]*)" --include="*.cs" \
  DeckFlow.Core/ DeckFlow.Web/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/ \
  | grep -oE "(class|interface|record) +[A-Z][A-Za-z0-9_]*" \
  | awk '{print $2}' | sort -u > /tmp/expected-types.txt

# Step 2: Extract every documented type from the 5 XML outputs
grep -hoE "<member name=\"T:[A-Za-z0-9._]+" \
  DeckFlow.Core/bin/Release/net10.0/DeckFlow.Core.xml \
  DeckFlow.Web/bin/Release/net10.0/DeckFlow.Web.xml \
  DeckFlow.CLI/bin/Release/net10.0/DeckFlow.CLI.xml \
  DeckFlow.Core.Tests/bin/Release/net10.0/DeckFlow.Core.Tests.xml \
  DeckFlow.Web.Tests/bin/Release/net10.0/DeckFlow.Web.Tests.xml \
  | sed 's|.*\.||' | sort -u > /tmp/documented-types.txt

# Step 3: Diff — AUDIT-03 PASSES when /tmp/missing-docs.txt is empty
comm -23 /tmp/expected-types.txt /tmp/documented-types.txt > /tmp/missing-docs.txt
cat /tmp/missing-docs.txt
```

**Plan 14-04 pass criteria:**
- `/tmp/missing-docs.txt` MUST be empty (no public types missing `<summary>`)
- Explicit allowlist for Phase 14: **empty** — DeckPageTab is opted-in to summaries (see
  `### Discretionary additions to Plan 14-03 backfill` above), so the allowlist is empty for
  this phase
- WARNING: `DeckFlow.Web.xml` type count will be higher than the 4 newly-flipped projects
  because Web's `GenerateDocumentationFile` was already ON; this is expected
- Also keep the strict-equality warning-count gate from D-09 as necessary-but-not-sufficient:

```bash
WARN_COUNT=$(grep -cE '^.*warning ' /tmp/p14-build.log)
[ "$WARN_COUNT" -eq 0 ] || echo "FAIL: $WARN_COUNT warnings vs baseline 0"
```

**Two gates required:** warning count (= 0) AND XML coverage diff (`/tmp/missing-docs.txt` empty).
Both must be clean for Plan 14-04 to pass AUDIT-03.

---

## Plan 14-02 reference-propagation checklist

When `ServiceX` is renamed to `ServiceY` (with file `ServiceX.cs` → `ServiceY.cs`), every
Plan 14-02 commit must update the following in lockstep. Build must be GREEN after each commit.

### Always check (for every rename)

1. **The file itself:** `git mv ServiceX.cs ServiceY.cs`; inside the file: class declaration,
   ctor name, interface name (if `I*` is co-located), `internal` ctor name, file-level XML doc
   target if it references the type via `<see cref="X"/>`.
2. **`DeckFlow.Web/Program.cs` DI registrations:** search `<IServiceX,` and `<IServiceX>`.
   Every `AddSingleton<I,T>`, `AddScoped<I,T>`, `AddTransient<T>`, `AddHostedService` must update
   both type args. (~Program.cs:60-180)
3. **`DeckFlow.Web/AssemblyInfo.cs` `InternalsVisibleTo`:** assembly name is `DeckFlow.Web.Tests`
   — NOT a type name, NOT affected by class renames. No edit needed (verified 2026-05-17).
4. **Namespace imports:** `using DeckFlow.Web.Services` namespace is unchanged by class rename.
   Check for `using static DeckFlow.Web.Services.ServiceX;` (none currently in codebase).
5. **`DeckController.cs`:** action-method parameter names, ctor parameter names, body type
   references. Phase 13 Wave 3 hit 142 identifier sites here.
6. **Razor `@model` directives:** `grep --include="*.cshtml" "@model.*ServiceX"`. Only ViewModels
   in `@model`; `_ViewImports.cshtml` declares `@using DeckFlow.Web.Models` so short name lookup
   works. If ViewModel renames, all `*.cshtml` `@model` lines under `DeckFlow.Web/Views/` update.
7. **Razor partial includes:** `_DeckToolTabs.cshtml`, `_WorkflowStepTabs.cshtml`, `_AiSelector.cshtml`.
   Search `Shared/` for any reference to the renamed type.
8. **Test files:** `ServiceXTests.cs` → `ServiceYTests.cs` via `git mv`. Body references.
   Test-double type names if they implement the renamed interface. `TestServiceFactory.cs`
   factory methods if the renamed type has an entry there.
9. **README.md:** search for the old class name. Phase 13 committed `c409517` to update service names.
10. **`.planning/codebase/*.md`** (STRUCTURE.md, CONVENTIONS.md, INTEGRATIONS.md, TESTING.md):
    update if Phase 14 changes anything they cite by name.

### Sometimes check (situational)

11. **Form `name="..."` attributes:** only relevant if a *property* of a request DTO renames.
    Phase 14 D-03 says property names don't change (class-level renames only per D-02).
12. **JSON serialization keys:** if a renamed property has `[JsonPropertyName("...")]`, the
    attribute string stays exactly the same — wire format is frozen.
13. **Phase 12 URL redirects in `Program.cs`:** URL slugs, not class names. Not affected.
14. **TS / CSS / JS identifiers:** Phase 14 explicitly out of scope (Phase 16 hygiene candidate).
15. **Phase 13 `chatgpt-*` URL redirect block** (`Program.cs:320-340`): not a class-name
    reference; do not touch.

### Smoke verification at end of every rename commit

```bash
# 1. Build clean
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --no-restore --nologo --verbosity quiet | tail -3
# Expected: Build succeeded. 0 Warning(s) 0 Error(s)

# 2. Old name is gone everywhere it should be
grep -rEn "OldServiceName" --include="*.cs" --include="*.cshtml" --include="*.md" \
  DeckFlow.Web/ DeckFlow.Core/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/ README.md
# Expected: 0 hits, or only allowlisted preservation literals (D-10)

# 3. init accessor sanity check
grep -rEn "{ get; }" $TOUCHED_FILES | grep -v "private" | grep -v "internal"
# Eyeball: did any { get; init; } collapse to { get; }? If so, restore.
```

---

## Plan 14-02 per-commit `{ get; init; }` preservation gate

Run this git diff check before EVERY commit in Plans 14-02 AND 14-03 on any touched `*.cs` file:

```bash
git diff --cached -- '*.cs' | grep -E "^\-.*\{ get; init; \}" | grep -v "^--"
# If any output: ABORT the commit. The IDE/edit stripped init. Restore and redo.
```

**Why this matters:** `.NET 9+ JsonSerializer` silently skips get-only properties during
deserialization. `EdhTop16Client.cs` and every record in `DeckFlow.Core/Models/`
(`DeckEntry`, `DeckDiff`, `LoadedDecks`, `PrintingConflict`) use `{ get; init; }`. Phase 13
UAT T5 broke when IDE auto-format stripped `init`. Phase 14 touches these exact files for
doc-comment backfill — same risk. `.editorconfig` line 49 `dotnet_style_prefer_auto_properties
= true:silent` means the IDE may apply this silently on save.

---

## Deferred (NOT executed; captured as future refactor candidates)

- **`ScryfallTaggerService` responsibility split** — class does three things: (1) Scryfall REST
  card resolution, (2) Tagger GraphQL query, (3) CSRF session lookup + kill-switch enforcement.
  Phase 14 renames to `ScryfallTaggerLookupService` (best single-line name for current behavior).
  Split into `IScryfallTaggerLookup` + `ITaggerSessionGate` composition is out of Phase 14 scope
  per CONTEXT.md AUDIT-01 boundary (renames-only this phase, no responsibility splits).
  Candidate for a future refactor milestone.

- **`DeckController` god-class split** — `DeckController.cs` has 142+ action-method + body
  references per Phase 13 Wave 3 analysis. Phase 14's smell-grep Smell 1 would flag it (likely
  ≥ 7 collaborators as seen in `DeckAnalysisPacketService`). Out of Phase 14 scope per CLAUDE.md
  "Out of Scope" — own refactor milestone.

- **ChatGPT services extraction** (`DeckAnalysisPacketService` / `DeckComparisonService` split
  into PromptBuilder + ScryfallReferenceResolver helpers) — out of Phase 14 scope; own refactor
  milestone per CLAUDE.md.

- **`NoWarn 1591;1573;1587` removal from `DeckFlow.Web.csproj`** — would expose ~88+ v1.1-era
  undoc'd Web public types. Future hygiene phase per CONTEXT.md "Deferred Ideas".

- **Internal-only class summaries** — out of Phase 14 D-06 scope; only renamed internals
  get summaries in lockstep. No separate sweep of internal-only code.

- **`.planning/codebase/CONVENTIONS.md` evolution** — no new legitimate test-double prefix
  surfaced by audit; CONVENTIONS.md unchanged for Phase 14.
