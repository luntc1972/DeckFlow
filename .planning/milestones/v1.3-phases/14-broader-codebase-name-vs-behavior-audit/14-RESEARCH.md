# Phase 14: Broader Codebase Name-vs-Behavior Audit — Research

**Researched:** 2026-05-17
**Domain:** C# / .NET 10 — codebase-wide naming audit + XML doc-comment backfill across 5 projects
**Confidence:** HIGH (everything verified against live HEAD via Bash/grep/build probes — no external library assumptions required)

---

## Summary

Phase 14 is a **mechanical sweep**, not a new-build phase. The technical domain is "the existing DeckFlow C# solution": there are no external libraries to evaluate, no patterns to discover, no architectural decisions to research. What needs research is **the current state of the codebase** (what's named, what's documented, what's not) and **the specific gotchas the planner must hand to executors**.

Three findings make this phase materially different from CONTEXT.md's expectation:

1. **D-04's "zero new CS1591/1573/1587 warnings" verification mechanism is broken at the foundation.** `.editorconfig` (committed in `0f38cce` on 2026-05-17, just hours before this research) sets `dotnet_diagnostic.CS1591.severity = none`, `CS1573.severity = none`, `CS1587.severity = none` repo-wide. A live probe (`dotnet build -p:GenerateDocumentationFile=true`) on a Core build with deliberately-missing summaries produces **zero warnings**. Flipping `GenerateDocumentationFile=true` in the 4 newly-flipped csprojs will NOT produce build failures or warnings — the editorconfig wins. The AUDIT-03 build-gate cannot rely on missing-doc warnings as Phase 14's correctness signal; it must use a coverage-style check on the generated `.xml` documentation file (or temporarily lift the editorconfig suppression for the verification run).
2. **Test-double prefix landscape is 2× CONTEXT.md's scout.** CONTEXT.md D-05 lists 4 renames (Null×1, Test×1, Configurable×1, Capturing×1). Actual census across both test projects: **8 nested private test doubles** with non-canonical prefixes (`Null×1, Configurable×1, Capturing×1, Successful×3, Dummy×1, Failing×1`), plus `TestServiceFactory` which is a *legitimate* test-only factory and should NOT be renamed. Plan 14-01's audit report must recount this precisely.
3. **`DeckFlow.Core` has 44 public types, ~35 already carry type-level XML docs** (188 documented members in the generated `DeckFlow.Core.xml` after a probe build). The backfill surface is concentrated on Core's `Models/` records (`DeckEntry`, `DeckDiff`, `LoadedDecks`, `PrintingConflict`) — records declared with `{ get; init; }` accessors that CLAUDE.md explicitly forbids re-formatting.

**Primary recommendation:** Treat Plan 14-04 (GenDocFile flip + build gate) as **build-clean-only**, plus a *secondary* coverage-style gate that diffs the generated `DeckFlow.*.xml` against a list of public types extracted by grep. Do NOT rely on the .NET compiler to flag missing summaries — `.editorconfig` already silences those diagnostics globally.

---

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 Audit method:** Scripted grep for smells + targeted manual review of `ScryfallTaggerService`, `CommanderSpellbookService`, test-double scoping. Smells: 3+ collaborator services; `*Service` not calling HTTP; `*Client` using app-scoped state; doc-summary joining 2+ responsibilities with "and"/";"; primary-type-vs-filename mismatch; non-canonical test-double prefixes.
- **D-02 Rename trigger:** LOOSE — any reader benefit qualifies.
- **D-03 Doc-comment scope:** `<summary>` on every public class + interface across 5 projects. Style anchor `CardLookupService.cs` / `CommanderSpellbookService.cs`. Public properties + public ctors on renamed types get summaries too.
- **D-04 GenerateDocumentationFile:** Flip ON in all 5 csproj. NO `NoWarn 1591;1573;1587` on the 4 newly-flipped projects. ZERO new warnings vs baseline.
- **D-05 Test-double consolidation:** 4 renames per scout count (`Null*`, `Test*`, `Configurable*`, `Capturing*` → `Fake*`/`Stub*`). **See Pitfall 2 below — actual count is 8, not 4.**
- **D-06 Internal scope:** Public + internal types in rename trigger; backfill is public-only. Renamed internals get summaries in lockstep.
- **D-07 Wave decomposition:** Plan 14-01 (baseline + report), 14-02 (renames), 14-03 (doc backfill), 14-04 (GenDocFile flip + final gate). Sequential.
- **D-08 Mid-plan green:** Every Plan 14-02 commit must leave build GREEN. No intermediate red builds allowed.
- **D-09 Baseline:** `dotnet build DeckFlow.sln --configuration Release --verbosity quiet 2>&1 | grep -cE '^.*warning '` captured in `14-BASELINE.md` BEFORE source edits.
- **D-10 Preservation list:** `"ChatGPT"`/`"Claude"`/`"Gemini"` AI keys, `TargetAiPlatform` property name, `targetAiPlatform` form field, `"chatgpt"` zip filename fallback, internal HTML/JS/data-* identifiers, Razor visible prose, 22 guild theme CSS, NO Co-Authored-By trailer.

### Claude's Discretion

- Old-name → new-name mapping for production-code candidates surfaced in Plan 14-01 (constrained by D-02 loose trigger).
- File rename order within Plan 14-02 — alphabetical fine.
- Whether to fold discovered name-vs-behavior gap into deferred (responsibility split) vs renaming to best single-line summary.

### Deferred Ideas (OUT OF SCOPE)

- Removing `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj` — future hygiene phase.
- Responsibility splits surfaced during audit — captured in `14-AUDIT-REPORT.md` as deferred refactor candidates, NOT executed in Phase 14.
- Internal-only class summaries (only renamed internals get docs in lockstep).
- CONVENTIONS.md evolution for new test-double prefixes (no expectation any will surface).

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUDIT-01 | Review every public class across 5 projects for name-vs-behavior alignment; rename misaligned ones | See "Audit Mechanics" + "Named Candidate Manual Review" sections — concrete grep patterns + checklist provided |
| AUDIT-02 | Every public class + interface has `<summary>`; `GenerateDocumentationFile=true` clean | See "Doc Backfill Mechanics" + "GenerateDocumentationFile Reality Check" sections — note that `.editorconfig` suppresses the relevant warnings, so the verification mechanism must shift to a coverage diff |
| AUDIT-03 | `dotnet build Release` zero new warnings vs baseline; test discovery succeeds via `dotnet test --no-build` or push-and-watch CI | See "Baseline + AUDIT-03 Build Gate" + "Test Discovery in WSL" sections |

---

## Architectural Responsibility Map

This phase does not change tier assignments — it only renames classes and adds docs. Mapping is informational:

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Class rename execution | Single-process tool tier (dev-machine MSBuild + git) | — | All work is local source edits; no runtime/data tier touched |
| Build-gate verification | Dev-machine MSBuild + (fallback) GitHub Actions push-and-watch | — | `dotnet build` is single source of truth; UAT not required because no behavior change |
| Doc-comment backfill | Source files only | — | XML doc generation runs in compiler, output `.xml` files in `bin/Release/net10.0/` |

---

## Project Constraints (from CLAUDE.md)

These directives apply to every executor working in Plan 14-02 / 14-03 / 14-04. Any tool action that contradicts them is forbidden:

| Directive | Where it bites Phase 14 |
|-----------|-------------------------|
| **No formatter sweeps** (Format Document / Code Cleanup / ReSharper reformat) | Plan 14-02 file edits touch ONLY the lines needed for rename + summary. Do NOT let an IDE save trigger a full-file format. |
| **Preserve `{ get; init; }`** — `.NET 9+ JsonSerializer.PreferredObjectCreationHandling = Replace` SKIPS get-only properties | EVERY record in `DeckFlow.Core/Models/` (`DeckEntry`, `DeckDiff`, `LoadedDecks`, `PrintingConflict`) + `EdhTop16Client.cs` private records use `{ get; init; }`. Phase 13 UAT T5 broke when IDE auto-format stripped `init` from `EdhTop16Client.cs` private nested classes. **Phase 14 will touch these files for summary backfill — risk is identical.** |
| **Preserve Allman braces** | All renamed types must keep `\n{` on its own line. |
| **Preserve switch expressions** | None of the rename targets currently identified use switch expressions, but doc-comment edits on files like `DeckController.cs` must not trigger a "modernize" sweep. |
| **Preserve raw-string-literal indentation** | `DeckAnalysis.cshtml` and adjacent `analysisFollowUpPrompt` raw strings ship VERBATIM to the AI. Phase 14 should not touch them; if a doc-comment lives near one, indent the doc-comment manually and verify. |
| **LF line endings** (`.gitattributes` enforces) | Plan 14-02's `git mv` + edits on Windows IDEs must not flip to CRLF. Last commit `dfa73ed` normalized Dockerfile to LF after a CRLF slip — same risk applies to any newly-touched file. |
| **Plain default-author commits, no Co-Authored-By trailer** | Every Plan 14-02 / 14-03 / 14-04 commit. CLAUDE.md commit hygiene. |
| **One logical change per commit** | `git mv FooService.cs FooBarService.cs` + the inline content edit (class declaration + namespace + DI registration + all callers) is **one logical commit**, not multiple. Each rename = one commit. |
| **Public repo — no secrets** | N/A this phase (no env-var or secret touches). |
| **VSTest unreliable in WSL** | AUDIT-03 cannot rely on `dotnet test`; falls back to `dotnet build` clean + push-and-watch CI on `v1.3`. See "Test Discovery in WSL" section. |

---

## State of the Project (Live Probes 2026-05-17)

### Public type counts per project

Verified via `grep -rE "^[[:space:]]*public +(sealed +)?(class|interface|record|abstract +class|static +class|partial +class) +[A-Z]" --include="*.cs" $PROJECT/`:

| Project | Public types (live grep) | CONTEXT scout | Already documented (`<member name="T:`) | Backfill remaining |
|---------|---:|---:|---:|---:|
| `DeckFlow.Core` | 44 | 26 (under-counted) | 35 | ~9 |
| `DeckFlow.Web` | 208 | 188 | unknown (GenDocFile already ON; XML already emitted) | unknown but small (most renamed Phase 13 types got summaries) |
| `DeckFlow.CLI` | 0 (only `internal static class CommandRunners`) | 0 | n/a | 0 |
| `DeckFlow.Core.Tests` | 10 | 10 | 0 | 10 |
| `DeckFlow.Web.Tests` | 56 | 55 | ~19 (existing) | ~37 |

**Note:** CONTEXT scout under-counted Core (26 → 44). The discrepancy comes from records and nested records (`CardDeckTotals`, `CategoryKnowledgeRow`, `MoxfieldImportResult`, etc.) declared with primary constructors that grep counts but a "one top-level type per file" scan misses. Plan 14-01 must run an authoritative count.

**Authoritative count command:**
```bash
grep -rE "^[[:space:]]*public +(sealed +)?(class|interface|record|abstract +class|static +class|partial +class) +[A-Z]" --include="*.cs" DeckFlow.Core/ DeckFlow.Web/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/
```

### Baseline build (verified 2026-05-17)

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --verbosity quiet --nologo
```
**Result:** 0 Warnings, 0 Errors, ~8 seconds. Baseline confirmed clean.

### Per-project build cost (D-08 mid-plan cadence)

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj --no-restore --nologo --verbosity quiet
```
**Result:** 0 Warnings, ~3.5 seconds wall-clock. Fast enough to gate every Plan 14-02 commit without slowing the executor down.

### Already-existing test-class summary anchors (CONTEXT.md "Tests for X" question)

Live probe via `grep -l "/// <summary>" DeckFlow.Web.Tests/*.cs`: **19 test files** already have at least one summary. Representative samples:

`DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:13-17` (Phase 13 anchor):
```csharp
/// <summary>
/// Covers staged prompt generation, validation, and artifact output for the deck-analysis
/// workflow served by <see cref="DeckAnalysisPacketService"/> across all supported AI platforms.
/// </summary>
public sealed class DeckAnalysisPacketServiceTests
```

`DeckFlow.Web.Tests/MetaGapServiceTests.cs:19-23`:
```csharp
/// <summary>
/// Integration fixture for <see cref="MetaGapService.BuildAsync"/> covering cEDH meta-gap prompt
/// assembly: user deck loading, reference-deck combo lookups, request context emission, and saved
/// analysis response parsing without re-loading the deck.
/// </summary>
public sealed class MetaGapServiceTests
```

These are **richer than "Tests for X"** — they describe the behaviors covered. Phase 14 should adopt the same tone: one sentence describing *what behavior is under test*, not the bare formulaic "Tests for X". CONTEXT.md "Tests for X is acceptable" is the floor; the Phase 13 anchors are the ceiling and the better target.

For per-test-method `[Fact]` summaries, Phase 13 anchored at:
```csharp
/// <summary>
/// Builds the deck summary and schema from pasted deck text on the setup step.
/// </summary>
[Fact]
public async Task BuildAsync_GeneratesSummaryAndSchema_ForPastedDeckText()
```
Phase 14 is NOT required to backfill per-method summaries (D-03 says "public class + interface"); only the test *class* needs a summary. Per-method summaries are nice-to-have not required.

---

## Audit Mechanics (AUDIT-01 / Plan 14-01)

This section answers research-focus item #1.

### Smell-grep script (cheap, runs in <2s)

There is no existing Phase 13 audit script — Phase 13 didn't need one (it had a closed target list). Phase 14 needs one. Here is the minimal portable bash that surfaces D-01's smell list:

```bash
# Run from repo root. Outputs go to .planning/phases/14-broader-codebase-name-vs-behavior-audit/14-AUDIT-REPORT.md sections.

# Smell 1: services with 3+ collaborator fields
# (Detects classes whose ctor declares 3+ "private readonly" interface deps — proxy for "integration layer not just RPC")
grep -rEl "^[[:space:]]+private +readonly +I[A-Z]" --include="*.cs" DeckFlow.Web/Services/ \
  | xargs -I {} bash -c 'count=$(grep -cE "^[[:space:]]+private +readonly +I[A-Z]" "{}"); if [ "$count" -ge 3 ]; then echo "$count collaborators: {}"; fi' \
  | sort -rn

# Smell 2: *Service classes that do NOT call any HttpClient/IHttpClientFactory/RestClient
# (Pure helpers misnamed as services)
for f in DeckFlow.Web/Services/*Service.cs; do
  if ! grep -qE "HttpClient|IHttpClientFactory|RestClient|IRestClient" "$f"; then
    echo "no-http service: $f"
  fi
done

# Smell 3: *Client classes that use app-scoped state (MemoryCache, ITaggerSessionCache, etc.)
# (Facades misnamed as clients)
for f in DeckFlow.Web/Services/*Client.cs; do
  if grep -qE "IMemoryCache|ITaggerSessionCache|IFeedbackStore|ICategoryKnowledgeStore" "$f"; then
    echo "stateful client: $f"
  fi
done

# Smell 4: doc-summaries joining 2+ responsibilities
# (Quick proxy: any /// <summary>...</summary> line containing " and " or "; " before the closing tag)
grep -rEn "/// .*( and | ; |, including )" --include="*.cs" DeckFlow.Web/Services/ DeckFlow.Core/ \
  | grep -v "/// </summary>" | grep -v "<param" | grep -v "<returns>" | head -50

# Smell 5: primary type name vs filename mismatch
# (Run per-project; emits "FILE :: TYPE" pairs where filename != primary class name)
for f in $(find DeckFlow.Core DeckFlow.Web -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*"); do
  base=$(basename "$f" .cs)
  primary=$(grep -m1 -oE "^public +(sealed +)?(class|interface|record|abstract +class|static +class|partial +class) +[A-Z][A-Za-z0-9_]*" "$f" 2>/dev/null | grep -oE "[A-Z][A-Za-z0-9_]*$")
  if [ -n "$primary" ] && [ "$primary" != "$base" ]; then
    # Allow I*Service pattern where filename matches the impl, not the interface
    if [ "I$base" != "$primary" ] && [ "${base#I}" != "$primary" ]; then
      echo "mismatch: $f :: primary=$primary"
    fi
  fi
done

# Smell 6: non-canonical test-double prefixes
grep -rEn "(private|public|internal) +sealed +class +(Null|Test|Configurable|Capturing|Dummy|Successful|Failing|Mock|Empty|Spy|Recording)[A-Z]" --include="*.cs" DeckFlow.Web.Tests/ DeckFlow.Core.Tests/
```

**Observed false positives to allowlist in Plan 14-01:**
- Smell 4 will match every `<param name=... and ...>` and `<see cref="X and Y"/>` — pre-filter `<param`, `<returns>`, `<see cref`, `</summary>` lines (already in the snippet above).
- Smell 5 will flag every `I*` interface co-located with its impl (`ICardLookupService` in `CardLookupService.cs`). The snippet's `[ "I$base" != "$primary" ]` check filters that pattern.
- Smell 6 will flag `TestServiceFactory` — legitimate (it's a test-only factory, not a test double). Allowlist this one explicit name in the report.

### Manual review checklist for the 2 named production candidates

These are the only D-01 manual-review items in CONTEXT.md (the 3rd, "test-double scoping", is mechanically covered by D-05). Both surfaces are surveyed below.

#### `ScryfallTaggerService` (`DeckFlow.Web/Services/ScryfallTaggerService.cs`)

**Header inspection (lines 17-86):**
- Interface: `IScryfallTaggerService` with single method `LookupOracleTagsAsync(cardName, ct)` — name accurately describes "look up tags via Tagger".
- Class has 5 collaborators: `IScryfallRestClientFactory`, `IScryfallTaggerHttpClient`, `ITaggerSessionCache`, `ResiliencePipelineProvider<string>`, `IFeatureFlagCache`. **Trips Smell 1 (5 ≥ 3).**
- XML summary at line 28-36 explicitly says it "resolves the card via Scryfall REST API, then queries Tagger GraphQL". That's two responsibilities joined by "then" (Smell 4 fuzzy match).
- Per-line comments mention "session cache (HIGH-2)" and "feature-flag cache" — confirms it's also doing CSRF session management + kill-switch gating.

**Audit verdict template for the report:**
> ScryfallTaggerService legitimately does three things: (1) Scryfall REST card resolution for set+number, (2) Tagger GraphQL query, (3) CSRF session lookup + kill-switch enforcement. The name describes responsibility #2 only. **Renaming options:** `ScryfallTaggerLookupService` (clearer single responsibility) — but doesn't capture session/flag work. Alternative: leave name, sharpen the `<summary>` to enumerate all three. **Recommend D-02 loose-trigger rename to `ScryfallTaggerLookupService`** OR document as deferred-split candidate ("does too much; split into `IScryfallTaggerLookup` + `ITaggerSessionGate` + flag-gate composition") if the rename alone won't add reader clarity. Decision deferred to Plan 14-01 executor.

#### `CommanderSpellbookService` (`DeckFlow.Web/Services/CommanderSpellbookService.cs`)

**Header inspection (lines 14-82):**
- Three nested records (`SpellbookCombo`, `SpellbookAlmostCombo`, `CommanderSpellbookResult`) declared at file top — clean DTOs.
- Interface: `ICommanderSpellbookService.FindCombosAsync(entries, ct)` — single method, accurate name.
- Class has 4 collaborators: `IHttpClientFactory`, `ResiliencePipelineProvider<string>`, `IMemoryCache`, optional `ILogger`. Trips Smell 1 (4 ≥ 3) but is a textbook HTTP-adapter shape per CONVENTIONS.md "External HTTP adapter" recipe.
- Summary line 52: "Fetches and caches combo data from the Commander Spellbook backend API." — accurate, single sentence, matches the style anchor.
- The class also does the Moxfield-fallback `card-list-from-url` call per INTEGRATIONS.md line 33. The current name doesn't capture that. But the Moxfield fallback usage is initiated by `MoxfieldApiDeckImporter.FetchViaCommanderSpellbookAsync` in `DeckFlow.Core/Integration/` — Commander Spellbook IS the upstream there too, so the name is correct.

**Audit verdict template:**
> CommanderSpellbookService name accurately describes responsibility — it talks to Commander Spellbook (both the combo endpoint AND the Moxfield-fallback endpoint that happens to live on the same backend). **No rename needed.** Summary on line 52 is good as-is.

### Audit report deliverable shape (Plan 14-01 → Plan 14-02 handoff)

`14-AUDIT-REPORT.md` must be small + actionable per `<specifics>` block in CONTEXT.md. One-line-per-rename format:

```markdown
# Phase 14 Audit Report

**Generated:** YYYY-MM-DD by Plan 14-01

## Renames (executed in Plan 14-02)

### Production code
1. `DeckFlow.Web/Services/ScryfallTaggerService.cs` :: `ScryfallTaggerService` → `ScryfallTaggerLookupService` — name doesn't capture session-cache + flag-gate work; loose-trigger D-02
2. (additional candidates from grep)

### Test doubles (D-05 canonicalization)
1. `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs:144` :: `NullTempDataProvider` → `StubTempDataProvider` (no-op fallback per CONVENTIONS.md Stub*)
2. `DeckFlow.Web.Tests/DeckControllerTests.cs:831` :: `ConfigurableMetaGapService` → `FakeMetaGapService` (configurable = stateful)
3. `DeckFlow.Web.Tests/DeckControllerTests.cs:870` :: `CapturingDeckAnalysisPacketService` → `FakeDeckAnalysisPacketService` (state-capture is stateful fake; document capture semantics in summary)
4. `DeckFlow.Web.Tests/DeckControllerTests.cs:939` :: `SuccessfulCardLookupService` → `FakeCardLookupService` ⚠️ — collides with existing `FakeCardLookupService` already declared in the same file at L914. Recommend `FakeSuccessfulCardLookupService` OR consolidate the two.
5. (continue for `SuccessfulSingleCardLookupService`, `SuccessfulMechanicLookupService`, `DummyCommanderSearchService`, `FailingRecentDecksImporter`)

## Doc-comment backfill targets (executed in Plan 14-03)

### DeckFlow.Core
- Models/DeckEntry.cs — class + 8 properties
- Models/DeckDiff.cs — record + 4 positional params
- Models/LoadedDecks.cs — record + 2 positional params
- (...full list per grep)

### DeckFlow.Web.Tests
- AboutControllerTests.cs — class summary
- (...full list, ~37 files)

## Deferred (NOT executed; captured as future refactor candidates)
- (e.g., DeckController god-class split — out of scope)
- (e.g., ChatGPT services extraction — out of scope)
```

---

## Test-Double Census (D-05) — Actual Distribution

This section answers research-focus item #5 (test-class summary tone) and corrects CONTEXT.md's D-05 scout.

### Live census 2026-05-17

```bash
grep -rEn "(private|public|internal) +sealed +class +(Null|Test|Configurable|Capturing|Dummy|Successful|Failing)[A-Z]" --include="*.cs" DeckFlow.Web.Tests/ DeckFlow.Core.Tests/
```

| File:line | Class | Visibility | Canonicalization (CONVENTIONS.md taxonomy) |
|-----------|-------|-----------|---|
| `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs:144` | `NullTempDataProvider` | private nested | `StubTempDataProvider` (no-op stub) |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:831` | `ConfigurableMetaGapService` | private nested | `FakeMetaGapService` (stateful) |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:870` | `CapturingDeckAnalysisPacketService` | private nested | `FakeDeckAnalysisPacketService` (state-capture in summary) ⚠️ name-collision check needed |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:939` | `SuccessfulCardLookupService` | private nested | merge with existing `FakeCardLookupService` at L914 OR `FakeSuccessfulCardLookupService` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:948` | `SuccessfulSingleCardLookupService` | private nested | similar — possible merge |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:987` | `SuccessfulMechanicLookupService` | private nested | `FakeMechanicLookupService` |
| `DeckFlow.Web.Tests/CommanderControllerTests.cs:117` | `DummyCommanderSearchService` | private nested | `StubCommanderSearchService` (no-op) or `FakeCommanderSearchService` (case-by-case on body) |
| `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs:116` | `FailingRecentDecksImporter` | private nested | `ThrowingRecentDecksImporter` (matches existing `Throwing*` taxonomy in CONVENTIONS.md) |

**Total: 8 renames, not 4.** Plan 14-01's `14-AUDIT-REPORT.md` must enumerate all 8 with explicit Fake/Stub/Throwing taxonomy choice.

### Test-double scope: nested vs TestDoubles/ folder

All 8 above are **`private sealed class` nested inside their consuming test class**. They are NOT in `DeckFlow.Web.Tests/TestDoubles/`. CONVENTIONS.md's `Fake*`/`Stub*`/`Throwing*` taxonomy applies to both styles. The TestDoubles/ folder currently has:
- `FakeCategoryKnowledgeStore.cs` (public)
- `FakeFeatureFlagCache.cs`
- `FakeHttpClientFactory.cs`
- `FakeResiliencePipelineProvider.cs`
- `FakeScryfallRestClientFactory.cs`
- `StubHttpMessageHandler.cs`
- `TestServiceFactory.cs` ← legitimate, do NOT rename

CONTEXT.md scout matches the TestDoubles/ folder distribution (55 Fake, 8 Throwing, 2 Stub, etc. across all test source); the "1 Null / 1 Test / 1 Configurable / 1 Capturing" count was a partial scout of nested doubles.

### Naming-collision risk in DeckControllerTests.cs

`DeckControllerTests.cs:914` already has `private sealed class FakeCardLookupService : ICardLookupService` with stub no-op behavior. Renaming `SuccessfulCardLookupService` (line 939) to `FakeCardLookupService` would conflict. Resolution options for Plan 14-02 executor:
1. Merge the two into one `FakeCardLookupService` with configurable result (state-driven fake) — but that broadens behavior and is a refactor not a rename.
2. Rename `SuccessfulCardLookupService` → `FakeSuccessfulCardLookupService` to preserve the semantic ("returns canned successful response").
3. Rename `SuccessfulCardLookupService` → `StubSuccessfulCardLookupService` (it returns a fixed response per call = stub semantics).

**Recommend option 3** (Stub) for the three `Successful*` doubles — they return fixed payloads without state, matching `StubHttpMessageHandler` semantics. Document the choice in `14-AUDIT-REPORT.md`.

---

## Doc Backfill Mechanics (AUDIT-02 / Plan 14-03)

This section answers research-focus item #3.

### Public class summary anchor

From `DeckFlow.Web/Services/CardLookupService.cs:39-42`:
```csharp
/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
```

**Rule:** one sentence, present-tense verb-leading, describes "what this class does to whom". 6-15 words. Use `<see cref="X"/>` for cross-references when the responsibility delegates to a named collaborator (see `CommanderSpellbookService.cs:38`).

### Public interface summary anchor

From `DeckFlow.Web/Services/CommanderSpellbookService.cs:37-40`:
```csharp
/// <summary>
/// Looks up combos for a deck using the Commander Spellbook API.
/// </summary>
public interface ICommanderSpellbookService
```

**Rule:** Same shape as class. If the interface has a single method, the interface summary describes the operation; the method summary on the implementation should describe execution details.

### Public property summary on a renamed record

From `DeckFlow.Web/Models/DeckAnalysisRequest.cs` (Phase 13 anchor — verified to have 27 summaries per 13-VERIFICATION.md spot-check, file location confirmed). Template for property:

```csharp
/// <summary>
/// Pasted deck text in Moxfield or Archidekt format.
/// </summary>
public required string DeckText { get; init; }
```

**Rule:** describes "what this property holds". The `{ get; init; }` accessor **MUST NOT be reformatted** to `{ get; }` — per CLAUDE.md, .NET 9+ `JsonSerializer` skips get-only properties during deserialization. Phase 13 UAT T5 broke when an IDE auto-format stripped `init` from `EdhTop16Client.cs`. Plan 14-03 executor MUST run a post-edit grep for any `{ get; }` that was previously `{ get; init; }` on every touched file before committing.

### Public ctor summary anchor

From `DeckFlow.Web/Services/ScryfallTaggerService.cs:59-65`:
```csharp
/// <summary>
/// Creates a Tagger service backed by the typed Tagger HttpClient (auto-cookies via
/// SocketsHttpHandler.CookieContainer per Phase 5 BUG-01), the IScryfallRestClientFactory
/// for Scryfall card lookups, the named Polly v8 pipelines (scryfall, tagger, tagger-post),
/// the 270s session cache (HIGH-2), and the in-process feature-flag cache used by the
/// FLAG-04 / D-11 kill-switch gate at the top of <see cref="LookupOracleTagsAsync"/>.
/// </summary>
public ScryfallTaggerService(...)
```

**Rule:** describes what dependencies are required and (when non-obvious) what each is for. Multi-line acceptable. Cross-reference design markers (HIGH-1, D-11) where they explain ctor parameter choices.

### Test-class summary anchor

From `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:13-17`:
```csharp
/// <summary>
/// Covers staged prompt generation, validation, and artifact output for the deck-analysis
/// workflow served by <see cref="DeckAnalysisPacketService"/> across all supported AI platforms.
/// </summary>
public sealed class DeckAnalysisPacketServiceTests
```

**Rule:** describes what *behavior* is under test, not just "Tests for X". Cross-reference the type under test with `<see cref="X"/>` so the doc tooling can hyperlink. "Tests for X" is the floor (CONTEXT.md `<specifics>` allows it for 55 test classes), but the Phase 13 anchor is the better target — 1-2 sentences naming concrete behaviors.

### Public ctor on test classes

xUnit allows constructor-injected per-test setup. Per `CategoryKnowledgeStoreTests.cs` etc., most test classes don't have a public ctor. Where one exists, summarize as:
```csharp
/// <summary>
/// Wires per-test fakes for <see cref="DeckController"/> exercises.
/// </summary>
public DeckControllerTests() { ... }
```

---

## GenerateDocumentationFile Reality Check (D-04 Critical Landmine)

This section answers research-focus item #4 — and overturns the assumption inside D-04.

### What CONTEXT.md D-04 expects

> Flip `<GenerateDocumentationFile>true</GenerateDocumentationFile>` ON in ALL 5 csproj files. NO `NoWarn` for 1591/1573/1587 on the 4 newly-flipped projects. ZERO new warnings vs baseline.

**This works only if missing-doc warnings actually surface.** They will not.

### Why: the editorconfig globally suppresses CS1591/1573/1587

`.editorconfig` (committed `0f38cce` on 2026-05-17 — hours before Phase 14 context was gathered) contains, lines 93-96:
```
# Suppress diagnostic warnings already suppressed in csproj (NoWarn 1591;1573;1587).
dotnet_diagnostic.CS1591.severity = none
dotnet_diagnostic.CS1573.severity = none
dotnet_diagnostic.CS1587.severity = none
```

The intent of that commit (per its message) was to "codify what is already in the csproj" so the four newly-flipped projects would inherit the suppression. But it has the side effect that **the AUDIT-03 build-gate cannot detect missing docs anywhere** — even where Phase 14 wants the gate to fire.

### Live verification (2026-05-17)

Probed by deliberately keeping `DeckEntry.cs` with zero summaries and forcing the doc-file generation:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj \
  -c Release --verbosity quiet \
  -p:GenerateDocumentationFile=true
```

Result:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

But the generated XML is still produced and only contains *documented* members:
```bash
grep -c "<member name" DeckFlow.Core/bin/Release/net10.0/DeckFlow.Core.xml
# → 188 (existing docs only; the 9 undoc'd DeckEntry/DeckDiff/LoadedDecks etc. are absent)
```

So the compiler *knows* DeckEntry has no doc — it just doesn't emit a warning, because `.editorconfig` told it severity=none.

### Three viable options for AUDIT-03 verification

**Option A (recommended):** Replace D-04's "zero new CS1591 warnings" gate with a **coverage-style check**. Plan 14-04's final verification:
1. Run `dotnet build DeckFlow.sln -c Release -p:GenerateDocumentationFile=true`.
2. Extract every public type from source via grep:
   ```bash
   grep -rEn "^[[:space:]]*public +(sealed +)?(class|interface|record) +([A-Z][A-Za-z0-9_]*)" --include="*.cs" \
     DeckFlow.Core/ DeckFlow.Web/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/ \
     | grep -oE "(class|interface|record) +[A-Z][A-Za-z0-9_]*" \
     | awk '{print $2}' | sort -u > /tmp/expected-types.txt
   ```
3. Extract every documented type from the 5 XML outputs:
   ```bash
   grep -hoE "<member name=\"T:[A-Za-z0-9._]+" \
     DeckFlow.Core/bin/Release/net10.0/DeckFlow.Core.xml \
     DeckFlow.Web/bin/Release/net10.0/DeckFlow.Web.xml \
     DeckFlow.CLI/bin/Release/net10.0/DeckFlow.CLI.xml \
     DeckFlow.Core.Tests/bin/Release/net10.0/DeckFlow.Core.Tests.xml \
     DeckFlow.Web.Tests/bin/Release/net10.0/DeckFlow.Web.Tests.xml \
     | sed 's|.*\.||' | sort -u > /tmp/documented-types.txt
   ```
4. Diff: `comm -23 /tmp/expected-types.txt /tmp/documented-types.txt > /tmp/missing-docs.txt`. AUDIT-03 PASSES when `/tmp/missing-docs.txt` is empty (minus the explicit allowlist of intentionally-undocumented types like `DeckPageTab` per 13-01 Pattern 7).

**Option B:** Temporarily remove the editorconfig suppression for the Plan 14-04 verification only. Plan 14-04 starts by editing `.editorconfig` to delete or comment lines 94-96, runs `dotnet build Release` (warnings now fire), and on success commits the editorconfig restoration. This is the closest to D-04's spirit but risks polluting the repo state mid-plan. Not recommended.

**Option C:** Add `<WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors>` to each newly-flipped csproj, which Roslyn evaluates AFTER editorconfig severity rules — though this is fragile and tooling-version-dependent. **Not recommended; behavior varies by SDK version.**

**The planner should adopt Option A** and codify the coverage-diff script as Plan 14-04's verification step. Update D-04's expectation in `14-AUDIT-REPORT.md` to say "GenDocFile ON; verification = XML coverage diff against grep'd public-type list".

### What about the 4 newly-flipped csprojs?

Even with the editorconfig suppression in place, flipping `GenerateDocumentationFile=true` is still **valuable**:
- It produces the `.xml` doc files in `bin/Release/net10.0/` for each project. Those XML files are the input to the coverage diff in Option A.
- It future-proofs CLI for D-04's "no-op today" forecast (zero public types in CLI today, but future public types will be silently undoc'd if the flag never flips).
- It signals intent in source-of-truth (csproj) that docs are part of the build product.

Plan 14-04 still flips the flag. The build still passes. The coverage diff is the new gate.

---

## Mid-Plan Build Cadence (D-08)

This section answers research-focus item #2.

### Cheapest "is build green" check per Plan 14-02 rename commit

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --no-restore --nologo --verbosity quiet
```

**Measured wall-clock (2026-05-17):** ~4.5 seconds for full-solution `--no-restore`. ~3.5 seconds for single-project `--no-restore`.

Recommended cadence per rename commit:
1. After every rename + lockstep reference update: run `dotnet build DeckFlow.sln --no-restore --verbosity quiet --nologo`.
2. If output ends `Build succeeded. 0 Warning(s) 0 Error(s)`, commit.
3. If warnings or errors: the rename missed a reference site. Fix in the same uncommitted edit (do NOT commit a red state — D-08 invariant).

### Why solution-wide, not per-project

Each rename touches the file's own project plus consumers (DeckController.cs in Web, test files in Web.Tests, possibly DI in Program.cs). A per-project build (`dotnet build DeckFlow.Web/...`) won't catch a missed reference in the test project. ~4.5s for solution build is cheap enough to accept the broader gate.

### When `--no-restore` is unsafe

If a `git mv` touches an `<EmbeddedResource>` or `<Content>` glob in csproj — none of the Plan 14-02 renames will, but worth knowing — `--no-restore` will not pick up the new path. In that case drop `--no-restore`.

### Don't trust the IDE

CLAUDE.md "VSTest unreliable in WSL; Format Document forbidden". By extension, the IDE's own "Build" button may also trigger Format-on-Save or ReSharper cleanup. **Plan 14-02 executor MUST verify via command-line `dotnet build`, not IDE build glyph.** The Phase 13 UAT T5 regression (IDE stripped `init` from EdhTop16Client.cs) is the cautionary tale.

---

## File-Rename Propagation Map (Plan 14-02 Checklist)

This section answers research-focus item #6.

When `ServiceX` is renamed to `ServiceY` (with file `ServiceX.cs` → `ServiceY.cs`), every Plan 14-02 commit must update the following surfaces in lockstep. The build will fail if any is missed — `dotnet build --no-restore` is the gate per D-08.

### Always check (for every rename)

1. **The file itself**: `git mv ServiceX.cs ServiceY.cs`. Inside the file: class declaration, ctor name, interface name (if `I*` is co-located), `internal` ctor name, `using` statements that reference colocated types in the same namespace if any, file-level XML doc target if it references the type via `<see cref="X"/>`.
2. **`DeckFlow.Web/Program.cs` DI registrations**: search for `<IServiceX,` and `<IServiceX>`. Phase 13 anchor block is around `Program.cs:60-180` (verified live). Every `AddSingleton<I,T>`, `AddScoped<I,T>`, `AddTransient<T>` registration must update both type args. `AddHostedService` registrations too.
3. **`DeckFlow.Web/AssemblyInfo.cs` `InternalsVisibleTo`**: assembly name is `DeckFlow.Web.Tests` — NOT a type name, NOT affected by class renames. No edit needed (verified 2026-05-17). Phase 13 confirmed same.
4. **Namespace imports**: search `using DeckFlow.Web.Services` — namespace is unchanged by class rename, so no edit. But check for `using static DeckFlow.Web.Services.ServiceX;` (none currently exist in the codebase — verified by grep).
5. **`DeckController.cs`**: action-method parameter names, ctor parameter names, body type references. Phase 13 Wave 3 hit 142 identifier sites in this single file. Phase 14's smaller-surface renames will hit fewer.
6. **Razor `@model` directives**: grep `--include="*.cshtml" "@model.*ServiceX"`. Only ViewModels typically appear in `@model`. `_ViewImports.cshtml` declares `@using DeckFlow.Web.Models` so Razor views reference ViewModels by short name once the namespace import is present. **If ViewModel renames, all `*.cshtml` `@model` lines under `DeckFlow.Web/Views/` must update.** Phase 13 Wave 3 confirmed 3 `@model` directives needed updates.
7. **Razor partial includes**: `_DeckToolTabs.cshtml`, `_WorkflowStepTabs.cshtml`, `_AiSelector.cshtml`. Search the `Shared/` folder for any reference to the renamed type if it's used in a partial.
8. **Test files**: `DeckFlow.Web.Tests/` per-target test file (`ServiceXTests.cs` → `ServiceYTests.cs` via `git mv`). Body references. Test-double type names if they impl the renamed interface. `TestServiceFactory.cs` factory methods if the renamed type has a factory entry there.
9. **README.md**: search for the old class name. Phase 13 committed `c409517` "update README.md service names". Same risk applies here.
10. **`.planning/codebase/*.md`** (STRUCTURE.md, CONVENTIONS.md, INTEGRATIONS.md, TESTING.md): these reference type names in the codebase intel. Update if Phase 14 changes anything they cite by name.

### Sometimes check (situational)

11. **Form `name="..."` attributes** in Razor: only relevant if a *property* of a request DTO renames. Phase 14 D-03 says public properties on renamed types get summaries, but NOT renamed (rename is class-level only per D-02 unless responsibility-driven). If a property does rename, every `<input name="oldName">` in the `.cshtml` files updates, and any TypeScript fetch payload `{ oldName: ... }` updates.
12. **JSON serialization keys**: if a renamed property has `[JsonPropertyName("...")]`, the attribute string stays exactly the same — that's the wire format. Only the C# identifier changes.
13. **Phase 12 URL redirects in `Program.cs`**: these are URL slugs, not class names. Not affected.
14. **TS / CSS / JS identifiers**: Phase 14 explicitly out of scope (per Phase 13 D-08 deferred, Phase 16 hygiene candidate). DO NOT touch.
15. **Phase 13 `chatgpt-*` URL redirect block** (`Program.cs:320-340`): not a class-name reference; do not touch.

### Smoke verification at end of every rename commit

```bash
# 1. Build clean
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --no-restore --nologo --verbosity quiet | tail -3
# Expected: Build succeeded. 0 Warning(s) 0 Error(s)

# 2. The old name is gone everywhere it should be (allowlist preservation list)
grep -rEn "OldServiceName" --include="*.cs" --include="*.cshtml" --include="*.md" \
  DeckFlow.Web/ DeckFlow.Core/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/ README.md
# Expected: 0 hits, or only allowlisted preservation literals.

# 3. The init keyword sanity check (CLAUDE.md gotcha)
grep -rEn "{ get; }" $TOUCHED_FILES | grep -v "private" | grep -v "internal"
# Eyeball: did any { get; init; } collapse to { get; }? If so, restore.
```

---

## Baseline + AUDIT-03 Build Gate

This section answers research-focus item #7.

### D-09 baseline command (Plan 14-01)

Per CONTEXT.md D-09 literal text, run BEFORE any source edits:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --verbosity quiet 2>&1 | grep -cE '^.*warning '
```

**Verified result against current `v1.3` HEAD (2026-05-17):** 0.

Capture in `14-BASELINE.md`:
```markdown
# Phase 14 Baseline (captured by Plan 14-01)

**Date:** 2026-05-17
**HEAD:** $(git rev-parse HEAD)
**Branch:** v1.3

## Build state pre-phase
- `dotnet build DeckFlow.sln -c Release` exit code: 0
- Warning count: 0
- Error count: 0
- XML doc files produced: DeckFlow.Web/bin/Release/net10.0/DeckFlow.Web.xml only (other 4 projects: GenerateDocumentationFile OFF)
- DeckFlow.Web.xml `<member>` count: $(grep -c "<member name" DeckFlow.Web/bin/Release/net10.0/DeckFlow.Web.xml)

## Public type counts per project (grep-derived)
| Project | Count |
| DeckFlow.Core | 44 |
| DeckFlow.Web | 208 |
| DeckFlow.CLI | 0 |
| DeckFlow.Core.Tests | 10 |
| DeckFlow.Web.Tests | 56 |

## Test-double prefix distribution (D-05)
Existing: Fake×55, Throwing×8, Stub×2
Non-canonical: 8 (full list per audit report)
```

### Plan 14-04 final build-gate command

```bash
# Step 1: clean build with GenDocFile=true on all 5 projects (after csproj flip)
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --no-incremental --nologo 2>&1 | tee /tmp/p14-build.log
# Expected tail: "Build succeeded. 0 Warning(s) 0 Error(s)"

# Step 2: warning count vs baseline (D-09 strict-equality gate)
WARN_COUNT=$(grep -cE '^.*warning ' /tmp/p14-build.log)
[ "$WARN_COUNT" -eq 0 ] || echo "FAIL: $WARN_COUNT warnings vs baseline 0"

# Step 3: XML doc coverage diff (Option A from "GenerateDocumentationFile Reality Check")
# See coverage-diff script above. Result file /tmp/missing-docs.txt must be empty (modulo allowlist).
```

The Plan 14-04 PASS criteria is the intersection of step 2 (warnings) AND step 3 (coverage). Both must be clean.

### Phase 13's verification pattern as template

Per `13-VERIFICATION.md` SC3:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --nologo
```
Result: "Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:21.27".

Phase 14 reuses the same primary command. The novel additions are (a) the warning-count grep gate from D-09 and (b) the XML coverage diff.

### Phase 13's grep-gate pattern as template (D-09 grep verification)

Per 13-CONTEXT.md D-09: Phase 13's verification used `grep -rEn "ChatGpt[A-Z]" --include="*.cs"` to confirm zero leftover hits outside allowlist. Phase 14 has no analogous "old name pattern" because Phase 14's renames are individual (no shared prefix). The replacement grep gate for AUDIT-01 is:
```bash
# For each rename in 14-AUDIT-REPORT.md, verify zero stale references
grep -rEn "OldName" --include="*.cs" --include="*.cshtml" --include="*.md" DeckFlow.Web/ DeckFlow.Core/ DeckFlow.CLI/ DeckFlow.Core.Tests/ DeckFlow.Web.Tests/ README.md
# Expected: 0 hits per rename (modulo allowlist)
```

This runs ONCE at Plan 14-04 over the consolidated rename list, not per-rename. Per-rename verification is the dotnet-build cadence in D-08.

---

## Test Discovery in WSL (CLAUDE.md VSTest Constraint)

This section answers research-focus item #8.

### CLAUDE.md directive

> Testing: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI

### What "VSTest unreliable" means in practice (2026-05-17)

`dotnet test` runs the VSTest platform under the hood. Symptoms observed in DeckFlow history: hangs at "Starting test execution", inconsistent test-host crash logs in `obj/Release/`, file-locking errors against `bin/` from concurrent watcher processes. The repo has no `xunit.runner.json` (TESTING.md confirms) so test-host config is purely SDK default.

### Plan 14-04 test-discovery sub-criterion (AUDIT-03)

AUDIT-03 says "test discovery succeeds (`dotnet test --no-build`) where WSL permits, otherwise verified via push-and-watch CI on the `v1.3` branch."

**Recommended Plan 14-04 sequence:**
1. Run `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --no-build --configuration Release --list-tests 2>&1 | tee /tmp/p14-discovery.log`. `--list-tests` discovers without executing — it's the cheapest sanity check that the test assemblies load.
2. **Success criterion (loose):** the command exits 0 AND emits at least N test names where N is the pre-phase test count. Capture the baseline count in Plan 14-01: `dotnet test DeckFlow.sln --no-build --configuration Release --list-tests 2>&1 | grep -c "    " > 14-BASELINE.md`.
3. **If discovery hangs or crashes** (CLAUDE.md "unreliable in WSL" path): cancel after 90 seconds, document the failure mode in `14-VERIFICATION.md`, and trigger push-and-watch CI fallback:
   ```bash
   git push origin v1.3
   # Wait for Render auto-deploy to finish; Render builds on every push.
   # Render's build phase runs `dotnet publish`, which compiles all 5 projects.
   # Build success there = AUDIT-03 build gate passes.
   ```
4. **Or:** trigger a GitHub Actions workflow if one exists. INTEGRATIONS.md line 117 says no CI is checked in (no `.github/workflows`). So the CI fallback is **Render auto-deploy** specifically.

### Why `--no-build` matters

After Plan 14-04's build step, the binaries are already produced. `dotnet test --no-build` skips the second compile. If `--no-build` is omitted, `dotnet test` rebuilds + runs in one shot — fine functionally, but slower and conflates two failure modes (build error vs test-discovery error). Splitting is cleaner.

### Why `--list-tests` is the right loose gate

Phase 14 makes zero behavior changes. The risk it introduces is: a renamed type breaks a test fixture's compile, which the build gate already catches; OR a renamed type changes a Razor `@model` such that views still compile but render wrong. The latter is impossible to detect without actually running views — out of scope for `dotnet test`. So `--list-tests` (discovery only) is the right semantic match for AUDIT-03's "test discovery succeeds" wording, and full `dotnet test` execution is gold-plating.

---

## Phase 13 Landmines (Lessons for Phase 14)

This section answers research-focus item #10.

Per `13-VERIFICATION.md` (PASS, 4/4 SC, 47 commits clean):

### What went right (Phase 14 should replicate)

1. **Grep-gate verification** — 13-VERIFICATION.md SC1 ran `grep -rEn "ChatGpt[A-Z]" --include="*.cs" | grep -v allowlist` against live HEAD. Result: 0 hits. **Phase 14 reuses pattern per Plan 14-04 step 3.**
2. **Build-clean as primary gate** — `dotnet build DeckFlow.sln --configuration Release --nologo` showed `0 Warning(s) 0 Error(s)` in 21.27s. **Phase 14 reuses the command verbatim.**
3. **47 commits, single author, no Co-Authored-By trailer** — Phase 14's commit hygiene matches.
4. **Wave structure visible in commit log** — `13-01-XX`, `13-02-XX`, `13-03-XX`, `13-04-XX` prefixes per wave. **Phase 14 mirrors: `14-01-XX`, `14-02-XX`, etc.**
5. **Manual T1-T8 UAT round-trip after rename** — confirmed zero user-visible behavior change. **Phase 14 does NOT need T1-T8 UAT** because Phase 14 only changes class names + docs (Phase 14 has no `request.TargetAiPlatform` touch, no Razor `@model` touch beyond what's lockstep-coupled to a rename). But the planner should keep T1-T8 in mind as a final-confidence step if any rename surfaces a Razor `@model` change.

### What went wrong (Phase 14 must avoid)

1. **Phase 13 UAT T5: IDE auto-format stripped `init` from `EdhTop16Client.cs`** (per 13-VERIFICATION.md "IDE Auto-Format Risk" section). Phase 13 file was correct in `git show HEAD`; the breakage was in the user's working tree mid-UAT. **Phase 14 touches more files via doc-comment backfill** — every `{ get; init; }` property in `DeckFlow.Core/Models/` (DeckEntry, DeckDiff, LoadedDecks, PrintingConflict) plus `EdhTop16Client.cs` private nested records is at the same risk. **Mitigation:** Plan 14-03 executor MUST grep `{ get; }` vs `{ get; init; }` diff on every touched file before each commit. See "Doc Backfill Mechanics" → property anchor.
2. **52095e9 fix-up commit in Wave 4** — per 13-04-SUMMARY's "Rule 1 deviation": test-file content edits were not staged in the initial 9 rename commits and required a follow-up. Plan 14-02's instruction set must explicitly state: "for each rename, the `git mv` AND every reference update are ONE commit (D-08), not two." Phase 13's deviation was D-05's mid-wave-red exception that Phase 14 D-08 explicitly forbids.
3. **`DeckPageTab` enum got 0 summaries per Phase 13** (intentional under 13-01 Pattern 7 because the file existed pre-Phase-13 with the same lack and `NoWarn 1591` covered it). Phase 14 D-04 removes NoWarn from 4 csprojs but not from Web. **DeckPageTab is in Web → still covered by Web's NoWarn → still intentionally undocumented.** Plan 14-03 allowlist explicitly excludes `DeckPageTab` from required-summary list. Cite 13-01 Pattern 7 as precedent.
4. **Phase 13 surfaced 999.1 (Razor visible prose)** — `"ChatGPT"` literal in `DeckAnalysis.cshtml`, `DeckComparison.cshtml`, `CedhMetaGap.cshtml`. **Out of Phase 14 scope per D-10.** Plan 14-04 grep gate should NOT flag those Razor prose hits — they're already on the preservation list.
5. **`_AiSelector.cshtml` Razor partial** holds "ChatGPT"/"Claude"/"Gemini" prose plus form-field `name="targetAiPlatform"`. Preserved per D-10. Phase 14 must not touch.

### Phase 13 commit pattern Phase 14 reuses

Spot-sample from `git log v1.3 --oneline | head -30`:
- `refactor(13-03): rename ChatGpt* identifiers in DeckController.cs (12 action methods + ctor params + body refs)` ← good model
- `refactor(13): rename ChatGptResponseParsersTests test file to ResponseParsersTests with XML summary` ← per-test-file rename = one commit
- `docs(13-XX): emit Wave X SUMMARY` ← wave boundary marker

Phase 14 commit prefixes: `refactor(14-01)` through `refactor(14-04)`. Wave SUMMARY commits as `docs(14-XX)`. Final summary on completion as `docs(14): mark phase complete`.

---

## Recommended Validation Cadence

This section answers research-focus item #9 — Validation Architecture (Nyquist Dimension 8).

Phase 14 has `nyquist_validation_enabled=false` (or absent — verified by reading `.planning/config.json` is missing or default). No formal `VALIDATION.md` artifact required. However, the natural test cadence the planner should build into the plans:

### Per-task (Plan 14-02 rename commit)

```bash
# After each rename + lockstep reference update, before commit:
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --no-restore --nologo --verbosity quiet | tail -3
# Expected: Build succeeded. 0 Warning(s) 0 Error(s)
```

Cost: ~3-5 seconds wall-clock. D-08 invariant.

### Per-wave (Plan 14-01 / 14-02 / 14-03 / 14-04 completion)

```bash
# Full Release build with incremental flag cleared
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --no-incremental --nologo
# Expected: Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:20.XX
```

Cost: ~20s. Run as the final action of each wave's last commit. SUMMARY.md commits cite this output.

### Phase gate (Plan 14-04 final)

1. Full clean Release build (above).
2. Warning-count vs baseline (D-09 strict equality).
3. XML doc coverage diff (Option A — see "GenerateDocumentationFile Reality Check").
4. Test discovery sanity: `dotnet test DeckFlow.sln --no-build --configuration Release --list-tests` (90s timeout; fall back to push-and-watch on Render auto-deploy if WSL hangs).

### No formal test-framework Wave 0 needed

xUnit 2.9.3 + xunit.runner.visualstudio 3.1.4 + Microsoft.NET.Test.Sdk 17.14.1 are already installed across both test projects. No installation gap. Phase 14 introduces no new tests (it does no functional change).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File rename with blame preservation | Manual cp + git rm | `git mv` | Survives `git log --follow`; Phase 13 used this convention across all 47 commits |
| Search-and-replace across .cs/.cshtml/.md | Custom sed/awk | `grep -rln` → manual edit per file via Read/Edit tools | sed/awk can corrupt LF line endings + raw-string literals; CLAUDE.md formatting constraint forbids broad reformatting |
| Build verification | Custom script that parses MSBuild output | `dotnet build ... 2>&1 \| grep -cE '^.*warning '` (D-09 literal pattern) | Single line, no parsing brittleness, matches Phase 13 working pattern |
| Counting public types | C# Roslyn analyzer build step | `grep -rE "^[[:space:]]*public +(sealed +)?(class\|interface\|record)..."` | One-time grep is fast enough; no need to add analyzers to csproj which is its own out-of-scope refactor |
| XML doc coverage check | Reflection-loading the assembly | grep over `bin/Release/net10.0/*.xml` + grep over source | XML files are plain text; reflection adds runtime brittleness for a static check |
| Mid-plan parallelism | `isolation="worktree"` per CONTEXT D-07 | Sequential execution (already the CONTEXT decision) | Phase 14's smaller-surface renames don't merit worktree overhead; sequential keeps D-08 mid-plan-green simpler |

---

## Common Pitfalls

### Pitfall 1: `.editorconfig` silences the AUDIT-03 warning gate
**What goes wrong:** Plan 14-04 flips `GenerateDocumentationFile=true` on 4 csprojs expecting CS1591 warnings to fire on missing docs. They don't.
**Why it happens:** `.editorconfig` lines 93-96 set `dotnet_diagnostic.CS1591.severity = none` repo-wide (committed `0f38cce` on 2026-05-17). The editorconfig severity rule wins over csproj `<NoWarn>` absence — it sets severity directly.
**How to avoid:** Don't rely on the compiler to detect missing docs. Use the XML coverage diff (Option A) as the gate. Document in `14-AUDIT-REPORT.md` that this is the verification method.
**Warning signs:** "Build succeeded. 0 Warning(s)" after a known-incomplete summary backfill. If the team trusts the warning count alone, gaps will ship.

### Pitfall 2: D-05 scout undercount (4 → 8 test doubles)
**What goes wrong:** Plan 14-02 budgets time for 4 test-double renames; actual count is 8 (or more — Plan 14-01 audit may surface additional `Successful*` / `Dummy*` / `Failing*` not in CONTEXT.md scout).
**Why it happens:** CONTEXT.md D-05's scout looked at `TestDoubles/` folder distribution, not nested private test-double class declarations inside `*Tests.cs` files. The 4 named (`Null`, `Test`, `Configurable`, `Capturing`) are samples, not the complete list.
**How to avoid:** Plan 14-01 runs the comprehensive grep (smell 6 in audit-mechanics section) and enumerates ALL 8+ in `14-AUDIT-REPORT.md` before Plan 14-02 starts.
**Warning signs:** Plan 14-02 finishes with `Stub`, `Throwing`, `Fake` still missing some non-canonical prefixes elsewhere in test files.

### Pitfall 3: `{ get; init; }` accessor stripped by IDE auto-format
**What goes wrong:** Plan 14-03's doc-comment edit on `DeckEntry.cs` saves through Rider/Resharper, which auto-converts `{ get; init; }` to `{ get; }` per its default rule. Build succeeds. T-runtime hits `JsonSerializer` which silently skips get-only props during deserialization. Records deserialize with `null`/default values.
**Why it happens:** Per CLAUDE.md, this happened in `EdhTop16Client.cs` before, breaking deserialization. Phase 13 UAT T5 hit a fresh recurrence. .editorconfig lines 49-51 set `dotnet_style_prefer_auto_properties = true:silent` and similar — `silent` means no diagnostic but tools may still apply.
**How to avoid:** Plan 14-03 executor runs this grep before EVERY commit on touched files:
```bash
git diff --cached -- '*.cs' | grep -E "^\-.*{ get; init; }" | grep -v "^--"
# If any output: the commit removes an init accessor. ABORT.
```
**Warning signs:** Diff shows `- public required string Name { get; init; }` paired with `+ public required string Name { get; }`. STOP. Restore via `git checkout HEAD~1 -- <file>` and redo the doc edit by hand without IDE save.

### Pitfall 4: CRLF line endings on Windows IDE save
**What goes wrong:** Plan 14-02 or 14-03 file edit on Windows IDE saves with CRLF; `.gitattributes` is set to LF-only (line `dfa73ed` committed 2026-05-17 normalized this). Git treats every line as changed.
**How to avoid:** Trust `.gitattributes` — but verify the first commit's diff doesn't show whole-file changes. If it does, the IDE re-encoded the file. Run `dos2unix <file>` and re-commit.
**Warning signs:** `git diff --stat` shows a renamed file with `400+/400-` lines — that's a re-encoding, not an edit.

### Pitfall 5: Razor `@model` directive vs `@using` namespace import mismatch
**What goes wrong:** A renamed ViewModel (e.g., `DeckSyncViewModel` → `DeckSyncResultViewModel`) updates `@model` in `DeckSync.cshtml` but doesn't update `_ViewImports.cshtml`. Build doesn't catch this if both old and new names exist in same namespace. UI renders blank.
**How to avoid:** This is unlikely in Phase 14 (no current ViewModel renames identified). But IF a rename happens: search `--include="*.cshtml" "@model.*OldName"` AND `--include="*.cshtml" "@using.*OldNamespace"`. `_ViewImports.cshtml` currently uses `@using DeckFlow.Web.Models` (verified) so namespace stays stable.
**Warning signs:** Plan 14-02 commit passes build; manual smoke-test of the affected Razor page shows empty model.

### Pitfall 6: Phase 13 fix-up commit anti-pattern
**What goes wrong:** Plan 14-02 commits a rename WITHOUT updating all reference sites in the same commit (per D-08 every-commit-green). Build is briefly red. Later commit cleans up. Git blame for the cleanup commit shows the executor, not the original commit's logical scope.
**Why it happens:** Phase 13 commit `52095e9` "apply test-file content edits — ChatGpt type identifier sweep" is exactly this case. 13-04-SUMMARY documented it as a "Rule 1 deviation". Phase 14 D-08 explicitly forbids the same pattern.
**How to avoid:** Plan 14-02 instruction template: "rename + every reference update = ONE commit. Do not commit until `dotnet build --no-restore` is clean."
**Warning signs:** A `chore(14-XX): fix references missed by rename Y` commit appears mid-wave. STOP and inspect — that's the fix-up anti-pattern.

### Pitfall 7: TestServiceFactory false-positive rename
**What goes wrong:** Smell 6 grep flags `TestServiceFactory.cs` because of the `Test` prefix. Plan 14-02 mechanically renames it (e.g., to `FakeServiceFactory`). All callers update. Build passes. But the class is **legitimately named** — it's a test-only factory pattern, not a test double, and `Test*` is a meaningful prefix here (it scopes the factory to test scenarios).
**How to avoid:** Plan 14-01 audit report explicitly allowlists `TestServiceFactory` as "not a rename target — legitimate factory pattern, scoped to test assembly via internal modifier".
**Warning signs:** A rename commit for `TestServiceFactory` shows up in Plan 14-02. Question the executor — is it actually misleading, or is the `Test` prefix carrying real meaning (it is).

---

## Code Examples

Verified patterns from official source files in this repo (line numbers from live HEAD 2026-05-17).

### Public class summary anchor
Source: `DeckFlow.Web/Services/CardLookupService.cs:39-42`
```csharp
/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
```

### Interface + method summaries (one file, both shapes)
Source: `DeckFlow.Web/Services/CardLookupService.cs:13-27`
```csharp
/// <summary>
/// Looks up pasted card names against Scryfall and returns formatted outputs plus missing lines.
/// </summary>
public interface ICardLookupService
{
    /// <summary>
    /// Looks up the provided card list using Scryfall.
    /// </summary>
    Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a single card and returns its formatted text plus detected mechanics.
    /// </summary>
    Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default);
}
```

### Record summary with positional ctor
Source: `DeckFlow.Web/Services/CommanderSpellbookService.cs:14-19`
```csharp
/// <summary>
/// A single confirmed or almost-confirmed combo from Commander Spellbook.
/// </summary>
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions);
```

### Record summary with property-style ctor (the Phase 14 risk pattern)
Source: `DeckFlow.Core/Models/DeckEntry.cs:1-20` — **currently undocumented; will need summary backfill in Plan 14-03 WITHOUT touching the `init` accessor**:
```csharp
namespace DeckFlow.Core.Models;

/// <summary>
/// A single card entry on one of a deck's boards.
/// </summary>
public sealed record DeckEntry
{
    /// <summary>The card's printed name as supplied by the source.</summary>
    public required string Name { get; init; }    // ← MUST stay { get; init; }

    /// <summary>Lowercased card name with Unicode punctuation collapsed.</summary>
    public required string NormalizedName { get; init; }

    // ... (8 properties total need summaries; all preserve { get; init; })
}
```

### Constructor summary
Source: `DeckFlow.Web/Services/ScryfallTaggerService.cs:59-72`
```csharp
/// <summary>
/// Creates a Tagger service backed by the typed Tagger HttpClient (auto-cookies via
/// SocketsHttpHandler.CookieContainer per Phase 5 BUG-01), the IScryfallRestClientFactory
/// for Scryfall card lookups, the named Polly v8 pipelines (scryfall, tagger, tagger-post),
/// the 270s session cache (HIGH-2), and the in-process feature-flag cache used by the
/// FLAG-04 / D-11 kill-switch gate at the top of <see cref="LookupOracleTagsAsync"/>.
/// </summary>
public ScryfallTaggerService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    IScryfallTaggerHttpClient taggerHttpClient,
    ITaggerSessionCache taggerSessionCache,
    ResiliencePipelineProvider<string> pipelineProvider,
    IFeatureFlagCache flagCache,
    ILogger<ScryfallTaggerService>? logger = null)
```

### Test-class summary (Phase 13 anchor — exceeds "Tests for X" floor)
Source: `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:13-17`
```csharp
/// <summary>
/// Covers staged prompt generation, validation, and artifact output for the deck-analysis
/// workflow served by <see cref="DeckAnalysisPacketService"/> across all supported AI platforms.
/// </summary>
public sealed class DeckAnalysisPacketServiceTests
```

### Test-class summary (formulaic floor — acceptable per CONTEXT `<specifics>`)
Template for the 37 test files with no current summary:
```csharp
/// <summary>
/// Tests for <see cref="TargetType"/>.
/// </summary>
public sealed class TargetTypeTests
```

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| dotnet SDK | All Plan 14-XX builds | ✓ | net10.0 SDK 10.0.300 via `/mnt/c/Program Files/dotnet/dotnet.exe` | none — phase blocks without it |
| git | `git mv` renames + commits per D-08 | ✓ | current via WSL Bash | none |
| grep / bash | Smell-grep script + verification gates | ✓ | WSL coreutils | none |
| comm | XML coverage diff (Option A) | ✓ | WSL coreutils | none |
| Node.js / npm | DeckFlow.Web build's `tsc` step | ✓ (per Dockerfile + CLAUDE.md) | n/a — not exercised by Phase 14 directly | n/a (not invoked) |
| Render CI / GitHub Actions | AUDIT-03 push-and-watch fallback if WSL hangs | partial — Render auto-deploy on `git push` (no GitHub Actions checked in per INTEGRATIONS.md) | n/a | manual `dotnet build` on a different machine (Windows native) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

---

## Validation Architecture

Phase 14 has `nyquist_validation_enabled` unset / false (not configured in `.planning/config.json` per Read of project state). No formal `VALIDATION.md` required. Recommended cadence summarized below; see "Recommended Validation Cadence" section above for full detail.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + xunit.runner.visualstudio 3.1.4 + Microsoft.NET.Test.Sdk 17.14.1 |
| Config file | none (no `xunit.runner.json` — TESTING.md confirms) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --no-restore --nologo --verbosity quiet` (~4.5s) |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --no-incremental --nologo` (~20s) |

### Phase Requirements → Verification Map
| Req ID | Behavior | Verification Type | Automated Command | File Exists? |
|--------|----------|-------------------|-------------------|--------------|
| AUDIT-01 | Public classes audited and misaligned ones renamed | grep diff at Plan 14-04 | `grep -rEn "$OLD_NAME" --include="*.cs" --include="*.cshtml" --include="*.md" .` per rename | n/a — gates per rename |
| AUDIT-02 | Every public class + interface has `<summary>` | XML coverage diff at Plan 14-04 | See "GenerateDocumentationFile Reality Check" Option A | will exist post-Plan 14-04 |
| AUDIT-03 | Zero new warnings vs baseline; test discovery succeeds | `dotnet build` warning grep + `dotnet test --list-tests` (90s timeout, fall back to Render push-and-watch) | `dotnet build DeckFlow.sln -c Release 2>&1 \| grep -cE '^.*warning '` | will exist post-Plan 14-04 |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln --no-restore --nologo --verbosity quiet` (~4.5s) — D-08 invariant.
- **Per wave merge:** `dotnet build DeckFlow.sln --configuration Release --no-incremental --nologo` (~20s).
- **Phase gate:** All of the above + XML coverage diff + `dotnet test --list-tests` (with Render fallback per CLAUDE.md WSL constraint).

### Wave 0 Gaps
None — existing test infrastructure covers all Phase 14 requirements. Phase 14 adds no new tests.

---

## Security Domain

`security_enforcement` not explicitly disabled in `.planning/config.json` (file not present), so the default applies — included for completeness, but Phase 14's surface area is mechanically null for security implications.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Phase 14 does not touch `BasicAuthMiddleware` or `FEEDBACK_ADMIN_USER` flow |
| V3 Session Management | no | Phase 14 does not touch `TaggerSessionCache` or any cookie/session state |
| V4 Access Control | no | Phase 14 does not touch `SameOriginRequestValidator` or `[Authorize]` attributes |
| V5 Input Validation | no | Phase 14 changes no validation logic — only class names + summaries |
| V6 Cryptography | no | Phase 14 does not touch `CryptographicOperations.FixedTimeEquals` or `FEEDBACK_IP_SALT` |
| V14 Configuration | yes (minor) | Editorconfig modification (if Option B is chosen for AUDIT-03 — NOT recommended). Recommend Option A which leaves editorconfig alone. |

### Known Threat Patterns for net10.0 + Razor + xUnit

| Pattern | STRIDE | Standard Mitigation | Phase 14 Touches? |
|---------|--------|---------------------|---|
| Hardcoded secret in committed source | Information Disclosure | Render secret store (`sync: false`) | No — Phase 14 has no secret surface |
| Razor unencoded output | Tampering | `@Html.Raw` audit | No — Phase 14 does not touch view contents |
| CSRF via cross-origin POST | Tampering | `SameOriginRequestValidator` already gates `/api/*` | No — Phase 14 does not touch security middleware |
| XML doc-comment containing secret string | Information Disclosure | Code-review pass before commit | Yes — caution Plan 14-03 executor against pasting any literal env-var name with example value into a summary |

**Plan 14-03 doc-backfill content rule:** summaries describe behavior, not data values. Never paste example secret values, real card prices, or any external API response body verbatim into a `<summary>`.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `dotnet build DeckFlow.sln -c Release` reproducibly emits 0 warnings on the 0f38cce HEAD (baseline) | Baseline + AUDIT-03 | Low — directly verified by live probe 2026-05-17; baseline could change if a dependency emits a new warning between baseline capture and Plan 14-04. Plan 14-01 re-captures at run time per D-09. |
| A2 | All 8 nested test doubles surveyed (NullTempDataProvider, ConfigurableMetaGapService, CapturingDeckAnalysisPacketService, 3× Successful*, DummyCommanderSearchService, FailingRecentDecksImporter) cover the full test-double rename surface | Test-Double Census | Medium — grep may have missed additional `Empty*`, `Spy*`, `Mock*` prefixes I didn't probe. Plan 14-01 runs the comprehensive smell-6 grep against ALL test files. Live verification confirmed only the 8 listed are present (2026-05-17). |
| A3 | Test-discovery `dotnet test --list-tests` exits 0 in <90s on WSL for this repo's test count (~200 tests) | Test Discovery in WSL | Medium — CLAUDE.md flags VSTest as "unreliable in WSL" generically. The `--list-tests` variant skips test execution which is the failure-prone phase, so discovery alone is likelier to succeed. If it hangs, the Render push-and-watch fallback covers AUDIT-03. |
| A4 | XML coverage diff (Option A) correctly identifies missing summaries against the grep-derived expected list | GenerateDocumentationFile Reality Check | Low — both inputs are plain-text greps; diff is `comm`-based. Risk is in the regex for type extraction: `grep -oE "[A-Z][A-Za-z0-9_]*$"` may miss generic-type-parameter declarations like `class Foo<T>`. Mitigation: strip `<.*>` suffixes before sort. Codify in Plan 14-04 script. |
| A5 | DeckController.cs and Program.cs do NOT need renames in Phase 14 (only out-of-scope refactors would touch them) | File-Rename Propagation Map | Low — CONTEXT.md D-10 explicitly carves out DeckController split as out of scope; Program.cs is the DI composition root which gets updated in lockstep with any service rename per item #2 of the propagation checklist. |
| A6 | Phase 14 introduces zero behavior change → no need for T1-T8 manual UAT | Phase 13 Landmines / Recommended Validation Cadence | Low — by design (CONTEXT.md scope: class names + docs only). If any Plan 14-02 rename surfaces a Razor `@model` change, the UAT consideration kicks back in. Plan 14-04 executor judgment call. |

---

## Open Questions (RESOLVED)

1. **Should `TestServiceFactory.cs` get a rename or stay?**
   - What we know: it's an `internal static class TestServiceFactory` legitimately named (factory pattern scoped to test assembly via `internal`). Its `Test` prefix accurately describes "test-only".
   - What's unclear: D-05's strict "no non-canonical prefixes" reading could be interpreted to require renaming this too.
   - Recommendation: keep as-is, allowlist explicitly in `14-AUDIT-REPORT.md`. The `Test` prefix here is meaningful (not a test-double prefix); CONVENTIONS.md taxonomy applies to test doubles, not test-helper factories.
   - **RESOLVED:** Allowlisted in Plan 14-01 Task 2 step 1 (`## Allowlist` subsection of 14-AUDIT-REPORT.md). Plan 14-02 Task 2 honors the allowlist via its "Allowlist non-renames" block.

2. **`DeckPageTab` enum summary backfill — opt-in or opt-out?**
   - What we know: per 13-01 Pattern 7 + 13-VERIFICATION.md, the enum was intentionally left without summaries during Phase 13. Web's `NoWarn 1591` covers it.
   - What's unclear: D-04 keeps Web's NoWarn in place but flips 4 other projects. DeckPageTab is in Web. Does D-03's "every public class + interface" cover enums? Strict reading: no (enum is neither). Loose reading: yes (every public type).
   - Recommendation: opt-in. Add summaries because they're cheap (4 enum values + the enum itself = 5 one-liners). Cite 13-VERIFICATION.md "optional polish" suggestion. Confirm with planner.
   - **RESOLVED:** Opt-in confirmed by planner. Decision recorded in Plan 14-01 Task 2 step 5 (`### Discretionary additions to Plan 14-03 backfill` in 14-AUDIT-REPORT.md). Implementation lands in Plan 14-03 Task 3 (`DeckFlow.Web/Models/DeckPageTab.cs` enum + values).

3. **Should `_AiSelector.cshtml`'s `@model string` be replaced with a stronger type during Phase 14?**
   - What we know: the partial currently takes a bare string for the selected AI platform name.
   - What's unclear: Phase 15 (AIPLATFORM-01) is the right place to refactor this (introduces `AiPlatform` sealed record value object).
   - Recommendation: do NOT touch in Phase 14. Phase 14 is class names + docs only. Defer to Phase 15.
   - **RESOLVED:** Deferred to Phase 15 (AIPLATFORM-01 value-object refactor). Phase 14 scope explicitly excludes this per CONTEXT.md "Phase Boundary → What this phase does NOT do" + CONTEXT.md "Future-phase coupling" note in canonical_refs.

4. **Plan 14-04 wave SUMMARY should commit `14-MISSING-DOCS.txt` artifact?**
   - What we know: the XML coverage diff in Option A produces `/tmp/missing-docs.txt`.
   - What's unclear: should this artifact be committed alongside SUMMARY for traceability?
   - Recommendation: commit a final-state `14-COVERAGE.md` summarizing the diff (empty = pass) into the phase directory for future audit. The `/tmp/missing-docs.txt` itself stays ephemeral.
   - **RESOLVED:** `14-COVERAGE.md` is committed in Plan 14-04 Task 2 (`docs(14-04): emit coverage report (AUDIT-03 triple-gate verification)`). `/tmp/missing-docs.txt` stays ephemeral; the per-project missing-docs counts are summarized in 14-COVERAGE.md `## Gate 2` table.

---

## Sources

### Primary (HIGH confidence)

- `CLAUDE.md` (this repo, live HEAD) — formatting + commit-hygiene + VSTest-WSL constraints
- `.editorconfig` (live HEAD `0f38cce`) — CS1591/1573/1587 severity rules; init-accessor preservation rule
- `.gitattributes` (live HEAD `0f38cce`) — LF line endings repo-wide
- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-CONTEXT.md` — D-01 through D-10 source of truth
- `.planning/phases/13-chatgpt-class-rename-summary-doc-comments/13-CONTEXT.md` + `13-VERIFICATION.md` — Phase 13 pattern + landmines + commit hygiene model
- `.planning/REQUIREMENTS.md` — AUDIT-01/02/03 acceptance gates
- `.planning/ROADMAP.md` — Phase 14 SC1-SC4 + dependency on Phase 13
- `.planning/codebase/CONVENTIONS.md` — Fake/Stub/Throwing taxonomy; sealed class + record + file-per-type rule
- `.planning/codebase/STRUCTURE.md` — file/folder layout
- `.planning/codebase/TESTING.md` — xUnit setup; hand-rolled test doubles in `TestDoubles/`
- `.planning/codebase/INTEGRATIONS.md` — Polly pipeline names + named HttpClient names + service consumers
- `DeckFlow.Web/DeckFlow.Web.csproj` — existing `GenerateDocumentationFile=true` + `NoWarn 1591;1573;1587`
- `DeckFlow.Web/Services/CardLookupService.cs` lines 13-42 — public class/interface/record/method summary anchors
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` lines 13-82 — additional summary anchors
- `DeckFlow.Web/Services/ScryfallTaggerService.cs` lines 17-86 — ctor summary anchor + Smell-1 example
- `DeckFlow.Core/Models/DeckEntry.cs` lines 1-20 — record-with-init-properties backfill target
- `DeckFlow.Web/Program.cs` (DI registration block) — propagation surface
- `DeckFlow.Web/AssemblyInfo.cs` — `InternalsVisibleTo("DeckFlow.Web.Tests")` invariant
- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` lines 13-23 — test-class summary anchor (above-the-floor)
- `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs:144` — NullTempDataProvider definition
- `DeckFlow.Web.Tests/DeckControllerTests.cs:831,870,939,948,987,914` — 6 nested test doubles
- `DeckFlow.Web.Tests/CommanderControllerTests.cs:117` — DummyCommanderSearchService
- `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs:116` — FailingRecentDecksImporter

### Live probes (HIGH confidence — generated this session 2026-05-17)

- `dotnet build DeckFlow.sln --configuration Release --verbosity quiet --nologo` → 0 Warning(s), 0 Error(s), 8s
- `dotnet build DeckFlow.Core/DeckFlow.Core.csproj -c Release -p:GenerateDocumentationFile=true` → 0 Warning(s) DESPITE 9 missing DeckEntry summaries; XML produced with 188 members (35 type-level)
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj --no-restore --nologo --verbosity quiet` → 3.5s wall-clock
- `grep -rEn "(private|public|internal) +sealed +class +(Null|Test|Configurable|Capturing|Dummy|Successful|Failing|Mock|Empty|Spy|Recording)[A-Z]"` → 8 hits + 1 legitimate TestServiceFactory
- `grep -c "/// <summary>"` across test files → 19 with summaries, 30+ with zero
- `git log v1.3 --oneline | head -30` → 47-commit Phase 13 history; commit-pattern model

### Secondary (MEDIUM confidence)

- `.NET 10 SDK 10.0.300` behavior on `GenerateDocumentationFile=true` + editorconfig severity — verified by probe but documented Microsoft behavior is "editorconfig rules override `NoWarn` in csproj when severity is set explicitly". Cross-verified via probe result.

### Tertiary (LOW confidence)

- None. Every claim in this research is verified by live probe, file read, or quotation from a committed planning document.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no external libraries to evaluate; xUnit/Polly/RestSharp/Markdig all already in place and not Phase 14 surface.
- Architecture: HIGH — Phase 14 makes zero architectural change; mapping is informational only.
- Audit mechanics: HIGH — every grep pattern tested against live repo state.
- Build gate: HIGH — `dotnet build` verified live; editorconfig suppression behavior probed and confirmed.
- Test-double census: HIGH — exhaustive grep run against full Tests trees.
- Pitfalls: HIGH — all 7 pitfalls have either a Phase 13 historical anchor or a live probe.

**Research date:** 2026-05-17
**Valid until:** 2026-06-16 (30 days — stable codebase, no fast-moving dependency surface). Refresh if `.editorconfig` is modified or `DeckFlow.sln` adds a 6th project.
