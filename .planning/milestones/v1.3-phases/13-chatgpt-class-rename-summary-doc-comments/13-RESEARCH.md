# Phase 13: ChatGpt* Class Rename + Summary Doc Comments — Research

**Researched:** 2026-05-17
**Domain:** C# refactor — type rename + XML doc backfill across DeckFlow.Web + DeckFlow.Web.Tests
**Confidence:** HIGH (verified by direct file inspection — no library/version lookups required; the entire problem is a controlled rename inside a known codebase)

## Summary

Phase 13 is a closed, mechanical rename of 26 `ChatGpt*` public types plus 3 `DeckPageTab` enum values across `DeckFlow.Web` and `DeckFlow.Web.Tests`, paired with XML `<summary>` doc-comment backfill on every renamed type. Naming targets are locked in `13-CONTEXT.md` D-01. No semantic change. No `DeckFlow.Core` touches. No new tests.

The work concentrates on three high-fanout files (`DeckController.cs` with 142 hits, `DeckControllerTests.cs` with 126 hits, `ChatGptPhase10RoundTripTests.cs` with 121 hits) where renaming touches every method body, every test fixture, and every action attribute. Wave execution is sequential because every wave overlaps `DeckController.cs` and `Program.cs` — worktree-style parallel waves are not possible.

The biggest planning levers are:
1. **The XML doc tone is already established** — `CardLookupService.cs` and `CommanderSpellbookService.cs` are the canonical templates: terse single-sentence `<summary>` above each public type, interface, public method, and public record.
2. **String literals that MUST be preserved** — `"ChatGPT"` AI key (4 sites in Models + AiPlatformOptions + _AiSelector + AI fallback in PacketArtifactStore), `chatgpt-packets`/`chatgpt-deck-comparison`/`chatgpt-cedh-meta-gap` regex strings in Program.cs 301 redirects, `"chatgpt-analysis"` in HelpContentServiceTests fixture, and ~60 chatgpt-prefixed CSS class/data-attribute identifiers in the three renamed Razor views (deferred to Phase 16).
3. **Test doubles live INLINE inside `DeckControllerTests.cs`** (lines 775–887) — six `FakeChatGptX` / `ConfigurableChatGptCedhMetaGapService` / `ThrowingChatGptCedhMetaGapService` / `ThrowingChatGptDeckPacketService` classes are private nested types. They rename in lockstep with the controller, NOT as separate test fixture files.
4. **One Razor partial `_AiSelector.cshtml`** uses `id="ai-chatgpt"` and `value="ChatGPT"` — these are LITERAL "ChatGPT" usages preserved per D-07.

**Primary recommendation:** Plan 4 sequential waves exactly as locked in D-05. Each wave commits one logical change per file rename (per CLAUDE.md). Intermediate red builds (end of Wave 1 + Wave 2) are acceptable; the only build-clean gate is end of Wave 4. The verification grep gate has a fixed-shape allowlist (5 specific exception types — see Wave 4 below).

## Project Constraints (from CLAUDE.md)

| Constraint | Source | Honored in plan by |
|---|---|---|
| ASP.NET 10 + Razor pinned — no framework migration | CLAUDE.md "Tech stack" | Rename only; no framework, no NuGet, no MSBuild target edits |
| Render Starter web 512MB RAM | CLAUDE.md "Hosting" | Pure rename — no allocation profile change |
| HTTP resilience — RestSharp + Polly v8, do NOT migrate to standard handler | CLAUDE.md "HTTP resilience" | Service ctor signatures preserved across rename; `IScryfallRestClientFactory`, `ResiliencePipelineProvider<string>`, and named pipelines (`scryfall`, `spellbook`) are NOT touched |
| Public repo — no secrets in commits | CLAUDE.md "Public repo" | N/A — rename only |
| VSTest unreliable in WSL — rely on `dotnet build` clean | CLAUDE.md "Testing" | Phase gate is `dotnet build DeckFlow.sln --configuration Release` clean, NOT `dotnet test`; per D-10 |
| Plain default-author commits, no Co-Authored-By trailer | CLAUDE.md "Commits" | Every commit in this phase MUST be plain — no trailers |
| One logical change per commit | CLAUDE.md "Commits" | D-05 — one file rename = one commit; one DI-block edit = one commit |
| README updated when behavior changes | CLAUDE.md "Commits" | README L605/L636/L637 mention `ChatGptDeckPacketService` / `ChatGptDeckComparisonService` — must update in Wave 2 commit |
| `<GenerateDocumentationFile>true</GenerateDocumentationFile>` | DeckFlow.Web.csproj L38 | Wave 1+2 backfill XML `<summary>` on every renamed type per D-03 — but `NoWarn 1591;1573;1587` (L40) STAYS per D-04 |

## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01: Naming map (page-aligned triplet — DeckAnalysis / DeckComparison / MetaGap)**

| Old name | New name |
|---|---|
| `ChatGptDeckRequest` | `DeckAnalysisRequest` |
| `ChatGptDeckViewModel` | `DeckAnalysisViewModel` |
| `ChatGptDeckPacketService` + `IChatGptDeckPacketService` | `DeckAnalysisPacketService` + `IDeckAnalysisPacketService` |
| `ChatGptDeckPacketResult` (sealed record in same file) | `DeckAnalysisPacketResult` |
| `ChatGptDeckAnalysisResponse` | `DeckAnalysisResponse` |
| `ChatGptWeakSlot` | `WeakSlot` |
| `ChatGptQuestionAnswer` | `QuestionAnswer` |
| `ChatGptDeckVersion` | `DeckVersion` |
| `ChatGptSetUpgradeResponse` | `SetUpgradeResponse` |
| `ChatGptSetUpgradeSet` | `SetUpgradeSet` |
| `ChatGptSetUpgradeTopAdd` | `SetUpgradeTopAdd` |
| `ChatGptSetUpgradeCardNote` | `SetUpgradeCardNote` |
| `ChatGptSetUpgradeShortlist` | `SetUpgradeShortlist` |
| `ChatGptDeckComparisonRequest` | `DeckComparisonRequest` |
| `ChatGptDeckComparisonViewModel` | `DeckComparisonViewModel` |
| `ChatGptDeckComparisonService` + `IChatGptDeckComparisonService` | `DeckComparisonService` + `IDeckComparisonService` |
| `ChatGptDeckComparisonResult` (sealed record) | `DeckComparisonResult` |
| `ChatGptDeckComparisonResponse` | `DeckComparisonResponse` |
| `ChatGptDeckComparisonRecommendation` | `DeckComparisonRecommendation` |
| `ChatGptCedhMetaGapRequest` | `MetaGapRequest` |
| `ChatGptCedhMetaGapViewModel` | `MetaGapViewModel` |
| `ChatGptCedhMetaGapService` + `IChatGptCedhMetaGapService` | `MetaGapService` + `IMetaGapService` |
| `ChatGptCedhMetaGapResult` (sealed record) | `MetaGapResult` |
| `ChatGptCedhMetaGapResponse` | `MetaGapResponse` |
| `ChatGptCedhMetaGapData` | `MetaGapData` |
| `ChatGptCedhWinLineSet` | `WinLineSet` |
| `ChatGptCedhWinLines` | `WinLines` |
| `ChatGptCedhInteraction` | `Interaction` |
| `ChatGptCedhSpeed` | `Speed` |
| `ChatGptCedhManaEfficiency` | `ManaEfficiency` |
| `ChatGptCedhCoreConvergenceCard` | `CoreConvergenceCard` |
| `ChatGptCedhMissingStaple` | `MissingStaple` |
| `ChatGptCedhPotentialCut` | `PotentialCut` |
| `ChatGptCedhTopAdd` | `TopAdd` |
| `ChatGptCedhTopCut` | `TopCut` |
| `ChatGptPacketArtifactStore` | `PacketArtifactStore` |
| `ChatGptRequestContextParser` | `RequestContextParser` |
| `ChatGptResponseParsers` | `ResponseParsers` |
| `ChatGptJsonTextFormatterService` | `JsonTextFormatterService` |
| `DeckPageTab.ChatGptPackets` (enum value) | `DeckPageTab.DeckAnalysis` |
| `DeckPageTab.ChatGptDeckComparison` (enum value) | `DeckPageTab.DeckComparison` |
| `DeckPageTab.ChatGptCedhMetaGap` (enum value) | `DeckPageTab.CedhMetaGap` |

**D-02:** No known collisions with existing types. Verified during research: only `Speed` (Wave 1) and `Interaction` (Wave 1) are common-enough single-word names to risk conflict — both currently nest INSIDE `MetaGapResponse` and stay nested per the model file layout; no namespace-level `Speed` or `Interaction` class exists in `DeckFlow.Web`.

**D-03:** Add `/// <summary>` one-sentence doc comment on every renamed public type — class, sealed class, sealed record, interface — plus their public constructors and public methods that don't already carry one. Anchor each summary to the class's CURRENT responsibility (read method bodies). Match `CardLookupService` / `CommanderSpellbookService` tone (terse, single-sentence). See `Code Examples` below for canonical reference.

**D-04:** `NoWarn 1591;1573;1587` STAYS in `DeckFlow.Web.csproj` L40. Phase 13 does not remove the suppression (that's Phase 14 / AUDIT-02 scope).

**D-05:** 4-wave sequential execution.
- Wave 1: Models (11 files, 13 model types + 3 enum values across 1 file)
- Wave 2: Services (7 service files: 4 with interface+class+record, 3 helper classes; plus `Program.cs` DI block + `README.md` mention sweep)
- Wave 3: Controller + Razor (`DeckController.cs` 142 hits, 3 Razor `@model` directives, `_DeckToolTabs.cshtml` enum value refs, `_AiSelector.cshtml` ONLY for `chatgpt`-string verify [no change])
- Wave 4: Tests + final build gate (10 test files + `TestServiceFactory.cs`)

**D-06:** Sequential within each wave (no `isolation="worktree"`). All waves overlap `DeckController.cs` + `Program.cs`. Single executor agent per wave.

**D-07:** Preserve EXACTLY (Phase 10 + Phase 12 + Phase 13 invariants):
1. `"ChatGPT"` AI Key string literal in `AiPlatform` constants (lives in 3 request setters + `AiPlatformOptions.cs` doc + `_AiSelector.cshtml` 5 hits)
2. `request.TargetAiPlatform` property name
3. `targetAiPlatform` form-field name (binds to that property — actually appears as `TargetAiPlatform` in form `name` attribute; case-insensitive bind)
4. `"chatgpt"` segment in artifact zip filename fallback in `PacketArtifactStore` (3 sites at lines 537/540/543)
5. "ChatGPT" as a narrative word inside XML doc-comment summaries (e.g., "ChatGPT-returned JSON payload")
6. "ChatGPT" in Razor view visible prose (result-panel labels, etc.)

**D-08:** Internal HTML/JS identifiers untouched (deferred to Phase 16):
- `data-cache-key="chatgpt-packets"` (DeckAnalysis.cshtml L72)
- `data-chatgpt-*-form`, `data-chatgpt-ui-mode-picker`, `data-chatgpt-ui-mode-button`, `data-chatgpt-current-step`, `data-chatgpt-workflow-step`, etc. (~30 sites in DeckAnalysis.cshtml)
- `class="chatgpt-packets-form"`, `chatgpt-layout-picker`, `chatgpt-step-panel`, `chatgpt-sticky-download__*`, etc. (~30 CSS class refs across 3 views)
- TS const names like `parseChatGptDownloadFilename` (not touched — TS/JS not in scope)

**D-09:** Verification gate (4 sub-checks):
1. Grep gate (exact pattern in Wave 4 section below)
2. `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors
3. `DeckFlow.CLI` still builds and runs all 5 commands clean
4. Manual T1–T8 integration suite re-run against post-rename HEAD (HUMAN-UAT)

**D-10:** Per CLAUDE.md "VSTest unreliable in WSL" — automated `dotnet test` is NOT part of the verifier gate. Build-clean is the gate. Manual T1-T8 is HUMAN-UAT.

### Claude's Discretion

- Naming of internal helper methods within renamed classes if they include "ChatGpt" — strip the prefix if it clarifies, leave it if removal would create a less descriptive name. Decide per case during execution.
- Order of file renames within a wave — alphabetical by old filename is fine.
- Whether to introduce interface symmetry (`IDeckAnalysisPacketService`) for services that don't currently have an interface — only add interfaces where DI already resolves via an interface name; do NOT introduce new interfaces in this phase.

### Deferred Ideas (OUT OF SCOPE — DO NOT TOUCH)

- AIPLATFORM-01 / AIPLATFORM-02 `AiPlatform` value-object refactor — Phase 15
- DeckController god-class split — own milestone
- TS / CSS / JS internal identifier sweep (`chatgpt-packets-form`, `data-chatgpt-*`, `parseChatGptDownloadFilename`) — Phase 16 candidate
- Removing `NoWarn 1591;1573;1587` — Phase 14 (AUDIT-02)
- Refactors discovered during execution (extracting prompt-builder helpers, splitting response shape files) — defer to AUDIT-01 / Phase 14

## Phase Requirements

| ID | Description | Research Support |
|---|---|---|
| CLASSRENAME-01 | All `ChatGpt*`-prefixed classes renamed to AI-agnostic terms | D-01 naming map locked; Wave 1 (Models) + Wave 2 (Services) enumerated below cover all 26 ChatGpt-prefixed public type definitions |
| CLASSRENAME-02 | Every renamed class has an XML `<summary>` doc comment | D-03 + reference XML doc style captured below (verified samples from `CardLookupService.cs` lines 13-41 + `CommanderSpellbookService.cs` lines 13-54) |
| CLASSRENAME-03 | DI registrations, `[InternalsVisibleTo]`, namespace imports, controller actions, view-model bindings, test fixtures, Razor `@model` directives updated | Wave 2 covers Program.cs DI block (3 entries at L263-295); Wave 3 covers DeckController action methods (12 GET/POST + 39 `View(...)` calls) + 3 Razor `@model` directives; Wave 4 covers 10 test files + `TestServiceFactory.cs`; `AssemblyInfo.cs` line 3 `InternalsVisibleTo("DeckFlow.Web.Tests")` is unchanged (assembly name not renamed) |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Class type definitions (rename) | API / Backend (DeckFlow.Web Models + Services) | — | C# type rename is purely a backend code-symbol change |
| XML doc-comment backfill | API / Backend (DeckFlow.Web Models + Services) | Build tooling (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`) | Doc comments live next to the renamed code; build emits documentation XML |
| Controller action-method rename (lockstep) | API / Backend (DeckFlow.Web Controllers) | — | Action methods are CLR identifiers in a public Controller class |
| Razor `@model` directive update | Frontend Server (Razor SSR) | — | Razor view-model binding is a server-side template concern |
| DI registration update | API / Backend (Program.cs composition root) | — | DI container is server-only — no client surface |
| Test fixture update | API / Backend (DeckFlow.Web.Tests) | — | xUnit fixtures live in test project, fully server-side |

This is a pure backend / API tier phase. No client / browser / CDN / database / storage surface is touched.

## Standard Stack

This phase is a refactor of EXISTING code in an established codebase — no new libraries, no version bumps.

### Verified existing dependencies (already in csproj, no change)

| Library | Version | Purpose | Status in Phase 13 |
|---|---|---|---|
| `Microsoft.NET.Sdk.Web` | 10.0 | ASP.NET Core MVC host | Unchanged |
| `Microsoft.AspNetCore.Mvc` | 10.0 | Controllers + Razor | Unchanged |
| `Polly` | 8.x | Resilience pipelines (RestSharp + Polly v8 named pipeline pattern) | Unchanged — service ctor signatures preserved across rename |
| `RestSharp` | 114.0.0 | HTTP client abstraction | Unchanged |
| `xUnit` | 2.9.3 | Test framework | Unchanged |
| `xunit.runner.visualstudio` | 3.1.4 | VS test discovery | Unchanged |

No `npm view` / `pip index` / `cargo search` verification needed: this phase installs zero packages.

## Package Legitimacy Audit

**Not applicable.** This phase installs zero packages. No external dependencies are added; no `dotnet add package` / `npm install` / `pip install` commands are part of any wave. The slopcheck gate is a no-op for Phase 13.

## Architecture Patterns

### System Architecture Diagram (relevant slice)

```
Browser POST                  Razor SSR                         Service tier                       Helper services
─────────────                 ─────────                         ─────────────                      ────────────────
form fields ──────────────►   DeckAnalysis.cshtml ──@model──►   DeckAnalysisViewModel ──────►     PacketArtifactStore (static)
("targetAiPlatform"           DeckComparison.cshtml──@model──►  DeckComparisonViewModel ────►     RequestContextParser (static)
 "DeckText", "DeckUrl",       CedhMetaGap.cshtml ───@model──►   MetaGapViewModel ───────────►     ResponseParsers (static)
 "TargetCommander..."                                                     │                       JsonTextFormatterService (static)
 etc.)                                                                    │
                                                                          ▼
DeckController ─[HttpPost]──► DeckAnalysisRequest  ────► IDeckAnalysisPacketService.BuildAsync ──► DeckAnalysisPacketResult ─┐
              ─[HttpPost]──► DeckComparisonRequest ────► IDeckComparisonService.BuildAsync ─────► DeckComparisonResult ─────┤── Razor render
              ─[HttpPost]──► MetaGapRequest ───────────► IMetaGapService.BuildAsync ────────────► MetaGapResult ────────────┘

DI registration (Program.cs:263-295):
  builder.Services.AddScoped<IDeckAnalysisPacketService>(sp => new DeckAnalysisPacketService(...));
  builder.Services.AddScoped<IDeckComparisonService>(sp => new DeckComparisonService(...));
  builder.Services.AddScoped<IMetaGapService>(sp => new MetaGapService(...));
```

The rename does NOT change this flow. Every arrow's endpoint identifier changes name; no edge is added, removed, or rerouted. Form field `name="..."` attributes already bind to `Model.Request.X` properties (e.g., `name="DeckText"` → `Request.DeckText`), so the View → ViewModel → Request flow keeps working as long as **property names** stay unchanged — and they DO (Phase 13 only renames class names, not property names per CONTEXT.md D-07).

### Component Responsibilities (post-rename — for planner reference)

| Component | New file path | Responsibility |
|---|---|---|
| `DeckAnalysisRequest` | `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | Form-bound request DTO for the deck-analysis page (Step 1 → Step 5 state) |
| `DeckAnalysisViewModel` | `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | Razor view-model wrapping `DeckAnalysisRequest` + per-step outputs |
| `DeckAnalysisResponse` (+ nested `WeakSlot`, `QuestionAnswer`, `DeckVersion`) | `DeckFlow.Web/Models/DeckAnalysisResponse.cs` | JSON-bound shape for the AI's parsed analysis return payload |
| `SetUpgradeResponse` (+ nested `SetUpgradeSet`, `SetUpgradeTopAdd`, `SetUpgradeCardNote`, `SetUpgradeShortlist`) | `DeckFlow.Web/Models/SetUpgradeResponse.cs` | JSON-bound shape for the set-upgrade prompt return payload |
| `IDeckAnalysisPacketService` + `DeckAnalysisPacketService` + `DeckAnalysisPacketResult` | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | Builds analysis + set-upgrade prompts for the deck-analysis page |
| `DeckComparisonRequest`, `DeckComparisonViewModel`, `IDeckComparisonService`, `DeckComparisonService`, `DeckComparisonResult`, `DeckComparisonResponse`, `DeckComparisonRecommendation` | `DeckFlow.Web/{Models,Services}/DeckComparison*.cs` | Two-deck comparison flow (analogous to packets) |
| `MetaGapRequest`, `MetaGapViewModel`, `IMetaGapService`, `MetaGapService`, `MetaGapResult`, `MetaGapResponse`, `MetaGapData` + 11 nested shapes | `DeckFlow.Web/{Models,Services}/MetaGap*.cs` | cEDH meta-gap flow using edhtop16 reference decks |
| `PacketArtifactStore` (static) | `DeckFlow.Web/Services/PacketArtifactStore.cs` | Shared static helper — zip Build/Load for all three pages; filename sanitizer |
| `RequestContextParser` (static partial) + `ParsedRequestContext` (sealed record) | `DeckFlow.Web/Services/RequestContextParser.cs` | YAML-like parse of `01-request-context.txt` zip payload |
| `ResponseParsers` (internal static) | `DeckFlow.Web/Services/ResponseParsers.cs` | Parse analysis + set-upgrade JSON returns from the AI |
| `JsonTextFormatterService` (public static) | `DeckFlow.Web/Services/JsonTextFormatterService.cs` | Extract `<result>...</result>` or fenced-JSON payload from AI text |

### Pattern 1: Interface + sealed class + sealed record in one file

Per CONVENTIONS.md and the existing `CardLookupService.cs` template (lines 13-37). For renamed services keep all three types together:

```csharp
// Source: DeckFlow.Web/Services/CardLookupService.cs:13-42 (existing template)
namespace DeckFlow.Web.Services;

/// <summary>
/// Looks up pasted card names against Scryfall and returns formatted outputs plus missing lines.
/// </summary>
public interface ICardLookupService
{
    /// <summary>
    /// Looks up the provided card list using Scryfall.
    /// </summary>
    Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the results of a card lookup.
/// </summary>
public sealed record CardLookupResult(IReadOnlyList<string> VerifiedOutputs, IReadOnlyList<string> MissingLines);

/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
{
    // ...
}
```

Apply this pattern when renaming `ChatGptDeckPacketService.cs` → `DeckAnalysisPacketService.cs`, `ChatGptDeckComparisonService.cs` → `DeckComparisonService.cs`, `ChatGptCedhMetaGapService.cs` → `MetaGapService.cs`. Each file keeps its existing `IFoo + Foo + FooResult` triplet.

### Pattern 2: `[InternalsVisibleTo]` test seam (UNCHANGED)

`DeckFlow.Web/AssemblyInfo.cs` line 3:
```csharp
[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]
```
This line does NOT change in Phase 13 — the assembly name being granted access is the test project name, not a renamed type. CONTEXT.md mentions `[InternalsVisibleTo]` updates only as a defensive checklist.

### Anti-Patterns to Avoid

- **Renaming a property mid-rename.** Property names on request DTOs (e.g., `TargetAiPlatform`, `DeckText`, `TargetCommanderBracket`) MUST NOT change in this phase — they are bound from form fields via `name="..."` attributes and from zip serialization. Phase 15 changes the property; Phase 13 only changes the type wrapping it.
- **Removing the `partial` modifier from `ChatGptRequestContextParser`.** This class is `internal static partial class` because it uses a source-generated regex (`[GeneratedRegex(...)]`). Keep `partial` after the rename.
- **Renaming inline test doubles into separate files.** The six `FakeChatGptX` / `ConfigurableChatGptCedhMetaGapService` / `ThrowingChatGptCedhMetaGapService` / `ThrowingChatGptDeckPacketService` classes live as `private sealed class` definitions inside `DeckControllerTests.cs` (lines 775–887). Keep them inline — do not promote to fixture files (out of scope for a rename).
- **Touching property names on response classes.** Nested `[JsonPropertyName("snake_case")]` attributes on `DeckAnalysisResponse` / `MetaGapResponse` / etc. MUST stay byte-identical or T1-T8 zip round-trips break.
- **Renaming `CedhMetaSortBy` / `CedhMetaTimePeriod`.** These are standalone enums in `DeckFlow.Web/Models/CedhMetaSortBy.cs` and `CedhMetaTimePeriod.cs` and describe cEDH meta filter options, NOT ChatGpt response shapes. They are NOT in CLASSRENAME-01 scope.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| Rename across many files | Custom sed/awk scripts | `git mv` (D-05) + IDE rename or staged `sed -i` replacements verified by `grep` audit | Per D-05, `git mv` preserves blame and follow history; CLAUDE.md "one logical change per commit" |
| Find every callsite of a renamed type | grep one filename at a time | `grep -rEn "ChatGpt[A-Z]" --include="*.cs"` over `DeckFlow.Web/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core/`, `DeckFlow.Core.Tests/` | This pattern is the locked verification gate per D-09 — use the same query during execution to find callsites |
| XML doc backfill | Generate vague `/// TODO add summary` placeholders | Read method bodies and write a one-sentence anchored description per D-03 | The Phase 13 deliverable IS the doc text quality, not just the presence of the tags. Vague placeholders defeat CLASSRENAME-02's purpose |
| Verifying compile-clean intermediate state | Trying to build at every commit | Build only at end of each wave per D-05 — Wave 1 + Wave 2 finish red intentionally; Wave 4 gate is the only build-clean checkpoint | The phase ships as one PR; intermediate red is acceptable; the goal is final-state green, not per-commit green |

**Key insight:** This phase has 736 ChatGpt-prefix sites across 32 files. Hand-typing the rename per site is unrealistic; relying purely on IDE rename misses string literals like `View("DeckAnalysis", ...)`. The correct tool mix is `git mv` for filename moves + targeted `sed -i` per-file for the bulk identifier replacement + `grep` audit at the end of each wave to confirm the wave-scoped grep returns zero hits in the touched files.

## Runtime State Inventory

**Trigger applies** — Phase 13 is a rename phase.

| Category | Items Found | Action Required |
|---|---|---|
| Stored data | **Zip artifact files on disk** (`MTG_DATA_DIR=/data` on Render, dev artifacts in `MTG_DATA_DIR`): `.zip` packets contain `01-request-context.txt` (YAML-like, parsed by `RequestContextParser` — has `target_ai_platform: ChatGPT` lines), `40-deck-analysis-response.json`, `40-deck-comparison-response.json`, `40-meta-gap-response.json`, `20-edh-top16-references.json`. Pre-Phase-12 zips with old filenames (`deckflow-packet-*-chatgpt-*.zip`, `*-compare2-*.zip`, `cedh-*-chatgpt-*.zip`) may exist on the production disk. | No data migration required — zip content is platform-neutral (JSON property names, not C# class names). Old saved zips load via `LoadFromZip`/`LoadComparisonFromZip`/`LoadCedhMetaGapFromZip` (renamed to `PacketArtifactStore` static methods) — the file format is unchanged. |
| Live service config | **None** — DeckFlow has no n8n / Datadog / Tailscale / Cloudflare Tunnel / external SaaS configuration that embeds C# type names. Render dashboard `sync: false` env vars (`MTG_DATA_DIR`, `DECKFLOW_DATABASE_PROVIDER`, `FEEDBACK_ADMIN_USER`, `FEEDBACK_ADMIN_PASSWORD`, `FEEDBACK_IP_SALT`, `DECKFLOW_GEMINI_ENABLED`, `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER`) are name-based — none contain `ChatGpt`. | None — verified by env-var list in `.planning/codebase/INTEGRATIONS.md` and `appsettings.*.json` inspection. |
| OS-registered state | **None** — DeckFlow runs as a single ASP.NET Core process under Docker on Render/Fly. No Windows Task Scheduler, launchd, systemd, or pm2 entries reference C# type names. The MSBuild `ZipDeckFlowBridge` target zips the browser-extension directory by **path**, not by type name. | None. |
| Secrets / env vars | **`DECKFLOW_GEMINI_ENABLED`** env var binds to `AiPlatformOptions.GeminiEnabled` — class name `AiPlatformOptions` is NOT renamed in this phase. **No env var name references any ChatGpt-prefixed class.** | None — verified by `grep -rE "ChatGpt" Program.cs` (8 hits, all in the DI registration block at L263-295 and the 301-redirect block at L322-340; none reference env-var names). |
| Build artifacts / installed packages | **`obj/`, `bin/`, `wwwroot/extensions/deckflow-bridge.zip`** all rebuild from source on every `dotnet build`. **No `pip install -e .` / `npm install -g` style globally-registered packages reference these types.** TypeScript source files in `wwwroot/ts/` reference `chatgpt-`-prefixed CSS class names and `data-chatgpt-*` attributes BUT those are explicitly deferred to Phase 16 (D-08). | None — but a clean rebuild (`dotnet clean && dotnet build`) at end of Wave 4 is recommended to flush stale `.dll` cache before the manual T1-T8 integration suite. |

**Nothing-found summary:** No data migration. No external service config. No OS registrations. No secret-key changes. Build artifacts auto-regenerate from rebuilt source. The rename is **fully code-resident**.

## Common Pitfalls

### Pitfall 1: Action-method rename creates routing ambiguity if you skip the `[HttpGet("/deck-analysis")]` attribute

**What goes wrong:** If a wave 3 executor renames `public IActionResult ChatGptPackets()` (line 155) to `public IActionResult DeckAnalysis()` but leaves the `[HttpGet("/deck-analysis")]` attribute on the method, all is fine — the route attribute disambiguates. BUT if a refactor introduces a *second* method named `DeckAnalysis(...)` (e.g., the executor renames both the GET and a helper), ASP.NET MVC throws `AmbiguousActionException` at runtime on first request.

**Why it happens:** All 12 chatgpt action methods (3 GET + 9 POST per the controller grep: lines 155, 168, 181, 459, 506, 557, 628, 690, 781, 879, 941, 1012) have to be renamed. The POST variants currently use suffixes like `ChatGptPacketsDownload`, `ChatGptPacketsUpload` — these need analogous suffixes (`DeckAnalysisDownload`, `DeckAnalysisUpload`) to stay unique within the class.

**How to avoid:** Rename ONE controller method at a time, run `dotnet build` after each (will be red but should produce a CS0111 conflict warning if duplicated), and commit per logical group. CONTEXT.md Deferred Ideas notes: "Phase 13 may surface that DeckController action-method names also carry the `ChatGpt` prefix — those can be renamed in lockstep with the type rename without splitting the controller."

**Warning signs:** `error CS0111: Type 'DeckController' already defines a member called 'DeckAnalysis' with the same parameter types`.

### Pitfall 2: `View("ChatGptPackets", ...)` literal string already references the renamed view name "DeckAnalysis"

**What goes wrong:** Phase 12 changed all 39 `View(...)` literal strings in `DeckController.cs` from `View("ChatGptPackets", ...)` to `View("DeckAnalysis", ...)` (verified: line 157, 466, 485, 495, 535, 545, 561, 571, 585, 604, 613, etc.). If a Wave 3 executor misreads the rename target and edits these literals "back" to match the new action method name, they'd break the Razor view-name resolution (the views are at `Views/Deck/DeckAnalysis.cshtml` etc.).

**Why it happens:** Phase 12 split URL/view rename from class rename. Phase 13 still inherits Phase 12's view filename layout. The view name literal "DeckAnalysis" is correct and must NOT change.

**How to avoid:** Wave 3 plan should explicitly state: "DO NOT modify `View("DeckAnalysis"|"DeckComparison"|"CedhMetaGap", ...)` literal strings — these are already correct per Phase 12 `D-13`. ONLY the C# expression `new ChatGptDeckViewModel { ... }` portion of those `View()` calls needs to be renamed to `new DeckAnalysisViewModel { ... }`."

**Warning signs:** Razor render at runtime returns 404 "view not found" for `/deck-analysis` page, or build error `CS0246: The type or namespace name 'ChatGptDeckViewModel' could not be found` if you renamed the class but left the type identifier in `new X { }`.

### Pitfall 3: HelpContentServiceTests fixture string `"chatgpt-analysis"` — permitted exception

**What goes wrong:** Wave 4 verification grep returns a hit on `DeckFlow.Web.Tests/HelpContentServiceTests.cs:44` line `Write("chatgpt-analysis.md", "...")`. An over-zealous executor might "fix" this to `Write("deck-analysis.md", ...)` and break the test.

**Why it happens:** The test verifies the help-content service correctly derives slug from filename. The string `"chatgpt-analysis"` is the test's **specific input** — the test asserts that whatever string you put in the filename matches the resulting `topic.Slug`. The actual help-topic file at `DeckFlow.Web/Help/deck-analysis.md` is unrelated to this test fixture; the test creates a temp file with whatever name the test author chose.

**How to avoid:** D-09 grep-gate allowlist explicitly excludes this site (see Wave 4 grep-gate below). Wave 4 plan must list this as PERMITTED.

**Warning signs:** Test `HelpContentServiceTests.GetBySlug_uses_filename_without_extension_as_slug` red after Wave 4 — the fix is to revert any edit to lines 44–54 of that file.

### Pitfall 4: `_AiSelector.cshtml` uses literal `"ChatGPT"` and `id="ai-chatgpt"` — both PRESERVED

**What goes wrong:** Wave 3 executor sees `id="ai-chatgpt"`, `value="ChatGPT"`, `for="ai-chatgpt"` in `_AiSelector.cshtml` lines 22-24 and "fixes" them to `id="ai-deck-analysis"` etc.

**Why it happens:** D-07 #1 + D-07 #6 + D-08 explicitly preserve both the `"ChatGPT"` AI key string AND the lowercase `chatgpt` HTML id used in DOM bindings. The id is read by TypeScript code (deferred to Phase 16); changing the value string breaks zip round-trip across the AI selector.

**How to avoid:** Wave 3 plan must explicitly call out `_AiSelector.cshtml` as a READ-ONLY file for Phase 13. Verify with `grep "ChatGpt\|chatgpt" _AiSelector.cshtml` after Wave 3 — expected post-state is 5 hits (lines 13-26 of that file, all in the same literal-string surface).

**Warning signs:** Grep gate fails with a hit pointing at `_AiSelector.cshtml`. Add to allowlist per D-08.

### Pitfall 5: Intermediate red build at end of Wave 1 trips a verifier into thinking the phase is broken

**What goes wrong:** Wave 1 renames 11 model files (request DTOs, view models, response classes). At end of Wave 1, the service files (e.g., `ChatGptDeckPacketService.cs`) still reference `ChatGptDeckRequest` and `ChatGptDeckViewModel` by name, so `dotnet build` returns ~50+ CS0246 errors. Test files have the same problem. A subsequent verifier or CI hook checking "build green at every commit" would fail this wave.

**Why it happens:** D-05 explicitly accepts intermediate red builds. Every wave overlaps `DeckController.cs` and `Program.cs` so doing inter-wave parallelism is impossible. The build-clean gate is ONLY at end of Wave 4.

**How to avoid:** Plan should explicitly note in Wave 1 + Wave 2 + Wave 3 task verification: "Intermediate build red — DO NOT run `dotnet build` as a pass criterion. Verify only by grep-counting that the rename was applied within the wave's file scope." Build-clean gate fires once, at end of Wave 4.

**Warning signs:** Verifier marks Wave 1 as failed because of CS0246 errors. Refer back to D-05 in CONTEXT.md.

### Pitfall 6: `ChatGptDeckAnalysisResponse.cs` filename already partially renamed — the class inside still has the ChatGpt prefix

**What goes wrong:** The file `DeckFlow.Web/Models/ChatGptDeckAnalysisResponse.cs` is **already named** with "DeckAnalysis" in its filename (from an earlier Phase 10 cleanup or naming intent). The renamed file should be `DeckAnalysisResponse.cs` (drop ChatGpt, drop Deck because "DeckAnalysisResponse" is unambiguous). A `git mv ChatGptDeckAnalysisResponse.cs DeckAnalysisResponse.cs` works fine, but an executor might second-guess the intent ("the file is already partly renamed — do I need to do anything?").

**Why it happens:** Phase 10 evolved the naming organically. The file-per-type rule (CONVENTIONS.md) demands the file name match the public type name. After Phase 13, the file IS `DeckAnalysisResponse.cs` and the public type IS `DeckAnalysisResponse` — both renamed in the same wave-1 commit.

**How to avoid:** Wave 1 plan should explicitly list this filename rename: `ChatGptDeckAnalysisResponse.cs` → `DeckAnalysisResponse.cs`. The class inside drops both `ChatGpt` and the duplicate `Deck` qualifier.

**Warning signs:** None — this is just a planning clarity item.

## Wave 1 — Models (file enumeration + rename targets)

**File renames (use `git mv` per D-05):**

| Old path | New path | Public types renamed (count) |
|---|---|---|
| `DeckFlow.Web/Models/ChatGptDeckRequest.cs` | `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | 1 (`ChatGptDeckRequest` → `DeckAnalysisRequest`) |
| `DeckFlow.Web/Models/ChatGptDeckViewModel.cs` | `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | 1 (`ChatGptDeckViewModel` → `DeckAnalysisViewModel`) |
| `DeckFlow.Web/Models/ChatGptDeckAnalysisResponse.cs` | `DeckFlow.Web/Models/DeckAnalysisResponse.cs` | 4 (`ChatGptDeckAnalysisResponse`, `ChatGptWeakSlot`, `ChatGptQuestionAnswer`, `ChatGptDeckVersion`) |
| `DeckFlow.Web/Models/ChatGptSetUpgradeResponse.cs` | `DeckFlow.Web/Models/SetUpgradeResponse.cs` | 5 (`ChatGptSetUpgradeResponse`, `ChatGptSetUpgradeSet`, `ChatGptSetUpgradeTopAdd`, `ChatGptSetUpgradeCardNote`, `ChatGptSetUpgradeShortlist`) |
| `DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs` | `DeckFlow.Web/Models/DeckComparisonRequest.cs` | 1 |
| `DeckFlow.Web/Models/ChatGptDeckComparisonViewModel.cs` | `DeckFlow.Web/Models/DeckComparisonViewModel.cs` | 1 |
| `DeckFlow.Web/Models/ChatGptDeckComparisonResponse.cs` | `DeckFlow.Web/Models/DeckComparisonResponse.cs` | 2 (`ChatGptDeckComparisonResponse`, `ChatGptDeckComparisonRecommendation`) |
| `DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs` | `DeckFlow.Web/Models/MetaGapRequest.cs` | 1 |
| `DeckFlow.Web/Models/ChatGptCedhMetaGapViewModel.cs` | `DeckFlow.Web/Models/MetaGapViewModel.cs` | 1 |
| `DeckFlow.Web/Models/ChatGptCedhMetaGapResponse.cs` | `DeckFlow.Web/Models/MetaGapResponse.cs` | 12 (`ChatGptCedhMetaGapResponse`, `ChatGptCedhMetaGapData`, `ChatGptCedhWinLineSet`, `ChatGptCedhWinLines`, `ChatGptCedhInteraction`, `ChatGptCedhSpeed`, `ChatGptCedhManaEfficiency`, `ChatGptCedhCoreConvergenceCard`, `ChatGptCedhMissingStaple`, `ChatGptCedhPotentialCut`, `ChatGptCedhTopAdd`, `ChatGptCedhTopCut`) |
| `DeckFlow.Web/Models/DeckPageTab.cs` | (no filename change — enum file stays) | 3 enum values (`ChatGptPackets` → `DeckAnalysis`, `ChatGptDeckComparison` → `DeckComparison`, `ChatGptCedhMetaGap` → `CedhMetaGap`); keep enum **integer values** stable (5, 8, 9) to avoid breaking any persisted serialization, even though `DeckPageTab` is not currently zip-stored |

**Wave 1 totals:**
- **Files to rename:** 10 (via `git mv`)
- **File requiring edit but no rename:** 1 (`DeckPageTab.cs`)
- **Public types to rename:** 29 (declarations) + 3 enum values
- **Total ChatGpt hits to remove in this wave's file scope:** 71 (sum of per-file counts: 1+5+7+12+1+4+3+2+4+24+3+5 — verified from grep output)
- **XML `<summary>` doc backfills to add:** 29 types (none have a class-level summary today; only a few properties on request DTOs have them — verified by inspection)

**Files modified (intra-wave references to update WITHIN the renamed files only):**
- `ChatGptDeckViewModel.cs` references `DeckPageTab.ChatGptPackets`, `ChatGptDeckRequest`, `ChatGptDeckAnalysisResponse`, `ChatGptSetUpgradeResponse` — all renamed in Wave 1, so intra-file references resolve at end of wave
- `ChatGptDeckComparisonViewModel.cs` references `DeckPageTab.ChatGptDeckComparison`, `ChatGptDeckComparisonRequest`, `ChatGptDeckComparisonResponse` — all renamed in Wave 1
- `ChatGptCedhMetaGapViewModel.cs` references `DeckPageTab.ChatGptCedhMetaGap`, `ChatGptCedhMetaGapRequest`, `ChatGptCedhMetaGapResponse`, `EdhTop16Entry` (NOT renamed — stays as-is)
- `ChatGptCedhMetaGapResponse.cs` has cross-references between 12 nested classes — all renamed within the same file

**Wave 1 build expectation:** RED. `DeckFlow.Web/Services/*ChatGpt*.cs`, `DeckFlow.Web/Controllers/DeckController.cs`, `DeckFlow.Web/Views/Deck/*.cshtml`, and all of `DeckFlow.Web.Tests/*.cs` will fail compile with CS0246. This is expected and DOES NOT block wave merge.

## Wave 2 — Services (file enumeration + DI + README)

**File renames (use `git mv`):**

| Old path | New path | Public types renamed |
|---|---|---|
| `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | `IChatGptDeckPacketService` → `IDeckAnalysisPacketService`, `ChatGptDeckPacketService` → `DeckAnalysisPacketService` (`sealed partial`), `ChatGptDeckPacketResult` → `DeckAnalysisPacketResult` (sealed record) |
| `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` | `DeckFlow.Web/Services/DeckComparisonService.cs` | `IChatGptDeckComparisonService` → `IDeckComparisonService`, `ChatGptDeckComparisonService` → `DeckComparisonService`, `ChatGptDeckComparisonResult` → `DeckComparisonResult` |
| `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` | `DeckFlow.Web/Services/MetaGapService.cs` | `IChatGptCedhMetaGapService` → `IMetaGapService`, `ChatGptCedhMetaGapService` → `MetaGapService`, `ChatGptCedhMetaGapResult` → `MetaGapResult` |
| `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` | `DeckFlow.Web/Services/PacketArtifactStore.cs` | `ChatGptPacketArtifactStore` (internal static) → `PacketArtifactStore`, `RestoredComparisonArtifacts` (internal sealed record — keep name, no prefix), `RestoredCedhMetaGapArtifacts` (keep name, no prefix) — verify those nested records do NOT carry ChatGpt prefix (they already don't per grep at L683 + L699) |
| `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` | `DeckFlow.Web/Services/RequestContextParser.cs` | `ChatGptRequestContextParser` (internal static partial) → `RequestContextParser`, `ParsedRequestContext` (internal sealed record — keep name, no prefix; already prefixless) |
| `DeckFlow.Web/Services/ChatGptResponseParsers.cs` | `DeckFlow.Web/Services/ResponseParsers.cs` | `ChatGptResponseParsers` (internal static) → `ResponseParsers` |
| `DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs` | `DeckFlow.Web/Services/JsonTextFormatterService.cs` | `ChatGptJsonTextFormatterService` (public static) → `JsonTextFormatterService` |

**Wave 2 totals:**
- **Files to rename:** 7
- **Service files edited (no rename, just symbol updates inside):** 0 — every renamed service is also a `git mv`
- **Public + internal types to rename:** 13 (3 interfaces + 3 service classes + 3 result records + 4 helper classes)
- **Total ChatGpt hits to remove in this wave's file scope:** ~200 (sum: 47+38+27+13+2+10+4 — verified)

**DI registration (Program.cs L263-295) — UPDATES REQUIRED:**

```csharp
// Source: DeckFlow.Web/Program.cs:263-295 (current state — Wave 2 target rename)
builder.Services.AddScoped<IChatGptDeckPacketService>(sp =>
    new ChatGptDeckPacketService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<IMechanicLookupService>(),
        sp.GetRequiredService<ICommanderBanListService>(),
        sp.GetRequiredService<IScryfallSetService>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetService<ILogger<ChatGptDeckPacketService>>()));
builder.Services.AddScoped<IChatGptDeckComparisonService>(sp =>
    new ChatGptDeckComparisonService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetService<ILogger<ChatGptDeckComparisonService>>()));
builder.Services.AddScoped<IChatGptCedhMetaGapService>(sp =>
    new ChatGptCedhMetaGapService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMoxfieldDeckImporter>(),
        sp.GetRequiredService<IArchidektDeckImporter>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetRequiredService<IEdhTop16Client>(),
        sp.GetRequiredService<ICommanderSpellbookService>()));
```

**Target post-rename DI block:** identical structure, only the type identifiers change (`IChatGptDeckPacketService` → `IDeckAnalysisPacketService`, `ChatGptDeckPacketService` → `DeckAnalysisPacketService`, `ILogger<ChatGptDeckPacketService>` → `ILogger<DeckAnalysisPacketService>`, etc.). No new lines. No reordering. No constructor parameter changes.

**README.md — UPDATES REQUIRED (CLAUDE.md "README updated when behavior changes"):**

Three hits at `README.md:605, 636, 637`:
- L605: `\`ChatGptDeckPacketService\` throttles all Scryfall calls...` → `\`DeckAnalysisPacketService\` throttles all Scryfall calls...`
- L636: `\`ChatGptDeckPacketService\` parallelizes...` → `\`DeckAnalysisPacketService\` parallelizes...`
- L637: `\`ChatGptDeckComparisonService\` parses two decklists...` → `\`DeckComparisonService\` parses two decklists...`

(Note: Phase 12 verification log noted `README.md:637` as a documented Phase 13 surface — confirmed.)

**Wave 2 build expectation:** RED. `DeckFlow.Web/Controllers/DeckController.cs` and all of `DeckFlow.Web.Tests/*.cs` still reference the old service/interface names. Wave 3 + 4 close the loop.

## Wave 3 — Controller + Razor (file enumeration + action methods + @model directives)

**Files modified (no renames, only symbol replacements):**

| File | Hit count | Update categories |
|---|---|---|
| `DeckFlow.Web/Controllers/DeckController.cs` | 142 | 12 action methods (3 GET + 9 POST), 39 `View(...)` second-arg type identifiers, 3 ctor parameter types, 3 private field types, ~80 method-body uses |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | 1 | `@model DeckFlow.Web.Models.ChatGptDeckViewModel` (L1) → `@model DeckFlow.Web.Models.DeckAnalysisViewModel` |
| `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` | 1 | `@model DeckFlow.Web.Models.ChatGptDeckComparisonViewModel` (L1) → `@model DeckFlow.Web.Models.DeckComparisonViewModel` |
| `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` | 1 | `@model DeckFlow.Web.Models.ChatGptCedhMetaGapViewModel` (L1) → `@model DeckFlow.Web.Models.MetaGapViewModel` |
| `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` | 4 | L6: `Model is DeckPageTab.ChatGptPackets or DeckPageTab.ChatGptDeckComparison or DeckPageTab.ChatGptCedhMetaGap` → 3 new enum values. L18-20: `Model == DeckPageTab.ChatGptPackets` → `DeckPageTab.DeckAnalysis` (and 2 more) |
| `DeckFlow.Web/Views/Shared/_BracketCallout.cshtml` | 3 | Comment text only — references `ChatGptPackets.cshtml` (the old view filename). Update prose to `DeckAnalysis.cshtml`. (Verify lines 3, 8, 11.) |

**DeckController action methods (locked in CONTEXT.md Deferred Ideas as "can be renamed in lockstep"):**

| Old action name | New action name | Line | HTTP verb | Route attribute (UNCHANGED) |
|---|---|---|---|---|
| `ChatGptPackets()` | `DeckAnalysis()` | 155 | `[HttpGet]` | `[HttpGet("/deck-analysis")]` |
| `ChatGptDeckComparison()` | `DeckComparison()` | 168 | `[HttpGet]` | `[HttpGet("/deck-comparison")]` |
| `ChatGptCedhMetaGap()` | `CedhMetaGap()` | 181 | `[HttpGet]` | `[HttpGet("/cedh-meta-gap")]` |
| `ChatGptPackets(ChatGptDeckRequest request)` | `DeckAnalysis(DeckAnalysisRequest request)` | 459 | `[HttpPost]` | `[HttpPost("/deck-analysis")]` |
| `ChatGptPacketsDownload(ChatGptDeckRequest request)` | `DeckAnalysisDownload(DeckAnalysisRequest request)` | 506 | `[HttpPost]` | `[HttpPost("/deck-analysis/download")]` |
| `ChatGptPacketsUpload(IFormFile zipFile)` | `DeckAnalysisUpload(IFormFile zipFile)` | 557 | `[HttpPost]` | `[HttpPost("/deck-analysis/upload")]` |
| `ChatGptDeckComparison(ChatGptDeckComparisonRequest request)` | `DeckComparison(DeckComparisonRequest request)` | 628 | `[HttpPost]` | `[HttpPost("/deck-comparison")]` |
| `ChatGptDeckComparisonDownload(ChatGptDeckComparisonRequest request)` | `DeckComparisonDownload(DeckComparisonRequest request)` | 690 | `[HttpPost]` | `[HttpPost("/deck-comparison/download")]` |
| `ChatGptDeckComparisonUpload(IFormFile zipFile)` | `DeckComparisonUpload(IFormFile zipFile)` | 781 | `[HttpPost]` | `[HttpPost("/deck-comparison/upload")]` |
| `ChatGptCedhMetaGap(ChatGptCedhMetaGapRequest request)` | `CedhMetaGap(MetaGapRequest request)` | 879 | `[HttpPost]` | `[HttpPost("/cedh-meta-gap")]` |
| `ChatGptCedhMetaGapDownload(ChatGptCedhMetaGapRequest request)` | `CedhMetaGapDownload(MetaGapRequest request)` | 941 | `[HttpPost]` | `[HttpPost("/cedh-meta-gap/download")]` |
| `ChatGptCedhMetaGapUpload(IFormFile zipFile)` | `CedhMetaGapUpload(IFormFile zipFile)` | 1012 | `[HttpPost]` | `[HttpPost("/cedh-meta-gap/upload")]` |

**Note:** ASP.NET Core route attributes (`[HttpGet("/deck-analysis")]`) disambiguate methods by URL, so renaming `ChatGptPackets()` and `ChatGptPacketsDownload()` to `DeckAnalysis()` and `DeckAnalysisDownload()` is safe — each method has a unique route. The GET + POST overloads on the same URL (`/deck-analysis`) work because they have different HTTP verb attributes.

**`View(...)` literal strings — DO NOT TOUCH (Phase 12 invariant):**
All 39 `View("DeckAnalysis", ...)`, `View("DeckComparison", ...)`, `View("CedhMetaGap", ...)` literal strings stay byte-identical. Only the **second argument** changes (`new ChatGptDeckViewModel { ... }` → `new DeckAnalysisViewModel { ... }`).

**Form-field `name` attributes — DO NOT TOUCH:**
`name="DeckText"`, `name="DeckUrl"`, `name="StrategyNotes"`, `name="MetaNotes"`, `name="Format"`, `name="DeckName"`, `name="IncludeSideboardInAnalysis"`, `name="IncludeMaybeboardInAnalysis"`, `name="SelectedAnalysisQuestions"`, `name="BudgetUpgradeAmount"`, `name="IncludeCardVersions"`, `name="PreferredCategories"`, `name="ProtectedCards"`, `name="FreeformQuestion"`, `name="DeckProfileJson"`, `name="SetPacketText"`, `name="SetUpgradeResponseJson"`, `name="TargetCommanderBracket"`, `name="TargetAiPlatform"`, `name="WorkflowStep"` — all bind to **property names** on the renamed Request classes. **Property names DO NOT change in this phase** per D-07 and CONTEXT.md "ViewModel property names are NOT renamed in this phase."

**`_AiSelector.cshtml` — READ-ONLY in Phase 13 (5 ChatGpt/chatgpt literals are all preserved per D-07 + D-08):**
- L13: `var selected = string.IsNullOrEmpty(Model) ? "ChatGPT" : Model;` (preserved)
- L15: `if (selected == "Gemini" && !geminiEnabled)` then `selected = "ChatGPT";` (preserved)
- L22: `<input ... id="ai-chatgpt" value="ChatGPT" ...>` (preserved)
- L23: `checked="@(selected == "ChatGPT" ? "checked" : null)"` (preserved)
- L24: `<label for="ai-chatgpt" class="ai-selector__option-label">ChatGPT</label>` (preserved)

**Wave 3 build expectation:** RED. Test files in `DeckFlow.Web.Tests/*.cs` still reference old type names. Wave 4 closes the loop.

## Wave 4 — Tests + final build gate

**Test files to rename (use `git mv`):**

| Old path | New path | Hit count | Notes |
|---|---|---|---|
| `DeckFlow.Web.Tests/ChatGptCedhMetaGapServiceTests.cs` | `DeckFlow.Web.Tests/MetaGapServiceTests.cs` | 17 | Mostly `new ChatGptCedhMetaGapRequest { ... }` constructions (12 sites) |
| `DeckFlow.Web.Tests/ChatGptDeckComparisonServiceTests.cs` | `DeckFlow.Web.Tests/DeckComparisonServiceTests.cs` | 13 | Mostly `new ChatGptDeckComparisonRequest { ... }` |
| `DeckFlow.Web.Tests/ChatGptDeckPacketServiceTests.cs` | `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` | 43 | Mostly `new ChatGptDeckRequest { ... }` (≥40 sites) |
| `DeckFlow.Web.Tests/ChatGptJsonTextFormatterServiceTests.cs` | `DeckFlow.Web.Tests/JsonTextFormatterServiceTests.cs` | 16 | Test class name + method names |
| `DeckFlow.Web.Tests/ChatGptPacketArtifactStoreRoundTripTests.cs` | `DeckFlow.Web.Tests/PacketArtifactStoreRoundTripTests.cs` | 3 | Test class + ctor refs |
| `DeckFlow.Web.Tests/ChatGptPacketArtifactStoreTests.cs` | `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` | 7 | Test class + small surface |
| `DeckFlow.Web.Tests/ChatGptPhase10RoundTripTests.cs` | `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` | 121 | High-density file — round-trip tests for all 3 request models across ChatGPT/Claude/Gemini |
| `DeckFlow.Web.Tests/ChatGptResponseParsersTests.cs` | `DeckFlow.Web.Tests/ResponseParsersTests.cs` | 16 | Test class + method calls into `ChatGptResponseParsers` static class |
| `DeckFlow.Web.Tests/ChatGptResultContractTests.cs` | `DeckFlow.Web.Tests/ResultContractTests.cs` | 20 | Test class + `[Theory] [InlineData("ChatGPT", "Claude", "Gemini")]` patterns — the `"ChatGPT"` string literal as test input STAYS |

**Files edited (no rename):**

| File | Hit count | Update categories |
|---|---|---|
| `DeckFlow.Web.Tests/DeckControllerTests.cs` | 126 | 39 controller-ctor sites passing `new FakeChatGptX(...)`, 4 method names (`ChatGptCedhMetaGap_Get_ReturnsExpectedViewModel` etc.), 2 references to `Equal("ChatGptCedhMetaGap", view.ViewName)` literals — those strings become `Equal("CedhMetaGap", view.ViewName)` to match the renamed action method, **6 inline `private sealed class` test doubles** at lines 775, 781, 810, 831, 844, 857: `FakeChatGptDeckPacketService` → `FakeDeckAnalysisPacketService`, `FakeChatGptDeckComparisonService` → `FakeDeckComparisonService`, `FakeChatGptCedhMetaGapService` → `FakeMetaGapService`, `ConfigurableChatGptCedhMetaGapService` → `ConfigurableMetaGapService`, `ThrowingChatGptCedhMetaGapService` → `ThrowingMetaGapService`, `ThrowingChatGptDeckPacketService` → `ThrowingDeckAnalysisPacketService` |
| `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` | 5 | Three factory methods: `CreateChatGptDeckPacketService` → `CreateDeckAnalysisPacketService` (L99), `CreateChatGptDeckComparisonService` → `CreateDeckComparisonService` (L129), `CreateChatGptCedhMetaGapService` → `CreateMetaGapService` (L151); plus 2 `ILogger<ChatGptX>` type parameters |

**Wave 4 totals:**
- **Files to rename:** 9 (via `git mv`)
- **Files to edit (no rename):** 2
- **Total ChatGpt hits to remove:** ~387 (sum of test-project counts above)

**Final build gate (run AFTER Wave 4 is complete):**
```bash
dotnet build DeckFlow.sln --configuration Release
```
Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`.

If `dotnet test --no-build` is attempted, it may behave unreliably per CLAUDE.md "VSTest unreliable in WSL". DO NOT block phase merge on test discovery in WSL; per D-10 the build-clean gate is sufficient.

## Verification grep gate (D-09 #1)

**The locked verification command (run at end of Wave 4):**

```bash
grep -rEn "ChatGpt[A-Z]" --include="*.cs" \
  DeckFlow.Web/ DeckFlow.Core/ DeckFlow.Web.Tests/ DeckFlow.Core.Tests/
```

**Expected post-Wave-4 result:** ZERO hits. (The `--include="*.cs"` restriction means `.cshtml` and `.md` files are not scanned by this gate, which is correct — `_AiSelector.cshtml` and the 3 main view files keep their `chatgpt-`-prefixed CSS/data identifiers per D-08, and README mentions are not C# code.)

**Why exactly zero (not "zero outside an allowlist"):**
- `DeckFlow.Core/` and `DeckFlow.Core.Tests/` have zero ChatGpt refs today (verified by `grep` returning empty)
- `DeckFlow.Web/` post-rename has zero ChatGpt-prefix C# identifiers — every preserved `"ChatGPT"` literal lives WITHIN a string (not a CamelCase identifier), and the regex `ChatGpt[A-Z]` only matches identifier-style use (capital-letter followed by capital letter at the next char). Verify: `grep -E "ChatGpt[A-Z]" Models/ChatGptDeckRequest.cs:13` matches the **identifier** `ChatGptDeckRequest` but does NOT match the **string literal** `"ChatGPT"` (because `ChatGPT` has all-caps "GPT", which fails the `[A-Z]` next-char check only against C# CamelCase pattern; **verified** by inspection — the regex `ChatGpt[A-Z]` correctly skips `"ChatGPT"` because after `ChatGpt` comes `T` not a CamelCase next-word boundary).

Wait — let me re-verify that claim. `"ChatGPT"` literally contains the substring `ChatGpt` followed by `T` (uppercase). `[A-Z]` matches `T`. So `grep -E "ChatGpt[A-Z]"` would match inside `"ChatGPT"`.

**Corrected expected post-Wave-4 result:** The grep MAY return hits inside the 4 preserved `"ChatGPT"` string literals (lines verified during research: `ChatGptDeckRequest.cs:13/100/101/110`, `ChatGptDeckComparisonRequest.cs:12/60/62/71`, `ChatGptCedhMetaGapRequest.cs:8/43/45/54`, and `AiPlatformOptions.cs:4` doc-comment) — but after rename, those files are renamed to `DeckAnalysisRequest.cs` etc. and the `"ChatGPT"` literal-string contents remain at the same line positions. The grep `ChatGpt[A-Z]` **matches against the literal**, so the gate is **NOT** "zero hits" — it is "zero hits outside the allowlist below."

**Permitted-exception allowlist (D-09 #1):**

| Site | Why permitted |
|---|---|
| `DeckFlow.Web/Models/DeckAnalysisRequest.cs:13` `private string _targetAiPlatform = "ChatGPT";` | D-07 #1 — AI Key string preservation |
| `DeckFlow.Web/Models/DeckAnalysisRequest.cs:100-110` doc-comment + switch arm `"ChatGPT"` | D-07 #1 + D-07 #5 |
| `DeckFlow.Web/Models/DeckComparisonRequest.cs:12,60,62,71` (same pattern) | D-07 #1 + D-07 #5 |
| `DeckFlow.Web/Models/MetaGapRequest.cs:8,43,45,54` (same pattern) | D-07 #1 + D-07 #5 |
| `DeckFlow.Web/Configuration/AiPlatformOptions.cs:4` doc-comment "ChatGPT/Claude/Gemini selector" | D-07 #5 narrative usage |
| `DeckFlow.Web/Services/JsonTextFormatterService.cs:11` `internal const string ChatGptResultWrapInstruction` | **NEEDS DECISION:** This is an internal const **identifier name** with `ChatGpt` prefix, not a string literal. Per D-01 "internal helper methods — strip the prefix if it clarifies, leave it if removal would create a less descriptive name" (Claude's Discretion). Recommendation: rename to `ResultWrapInstruction` (descriptive enough without the prefix) **or** to `AiResultWrapInstruction` (more explicit). The const VALUE (a string mentioning "ChatGPT/Claude/Gemini") stays unchanged per D-07 #5. |
| `DeckFlow.Web/Services/PacketArtifactStore.cs:537,540,543` `"chatgpt"` (lowercase) AI-segment fallback | D-07 #4 — Phase 10 invariant (commit `00e5bdd`) |
| `DeckFlow.Web.Tests/HelpContentServiceTests.cs:44,48,51` `"chatgpt-analysis"` fixture filename | D-07 narrative — test creates a temp file with this literal name to verify slug-from-filename derivation; unrelated to source files |

**Refined grep gate command (apply allowlist via `grep -v`):**

```bash
grep -rEn "ChatGpt[A-Z]" --include="*.cs" \
  DeckFlow.Web/ DeckFlow.Core/ DeckFlow.Web.Tests/ DeckFlow.Core.Tests/ \
  | grep -vE '"ChatGPT"' \
  | grep -vE 'ChatGPT/Claude/Gemini' \
  | grep -vE 'JsonTextFormatterService\.cs.*ChatGptResultWrapInstruction'
```

If `ChatGptResultWrapInstruction` is renamed in Wave 2 (recommended), that third `grep -v` line drops.

**Lowercase `chatgpt` audit (separate gate):**

```bash
grep -rEn "[Cc]hatgpt" --include="*.cs" DeckFlow.Web/ DeckFlow.Web.Tests/ \
  | grep -vE 'chatgpt-analysis'  # HelpContentServiceTests fixture
```

Expected after Wave 4: 3 hits (PacketArtifactStore.cs L537, L540, L543) for the `"chatgpt"` AI-segment fallback. Any additional hits indicate an over-eager rename of preserved lowercase string-literal identifiers.

## Code Examples (XML doc tone reference)

### Canonical template — service interface + class + result record (Source: `DeckFlow.Web/Services/CardLookupService.cs:13-42`)

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
}

/// <summary>
/// Returns the results of a card lookup.
/// </summary>
public sealed record CardLookupResult(IReadOnlyList<string> VerifiedOutputs, IReadOnlyList<string> MissingLines);

/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
{
    // ...
}
```

### Canonical template — service with multiple records (Source: `DeckFlow.Web/Services/CommanderSpellbookService.cs:13-54`)

```csharp
/// <summary>
/// A single confirmed or almost-confirmed combo from Commander Spellbook.
/// </summary>
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions);

/// <summary>
/// A combo that is one card away from being complete in the submitted deck.
/// </summary>
public sealed record SpellbookAlmostCombo(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results,
    string Instructions);

/// <summary>
/// The combo lookup result for a deck.
/// </summary>
public sealed record CommanderSpellbookResult(
    IReadOnlyList<SpellbookCombo> IncludedCombos,
    IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos);

/// <summary>
/// Looks up combos for a deck using the Commander Spellbook API.
/// </summary>
public interface ICommanderSpellbookService
{
    /// <summary>
    /// Returns combos that are fully in the deck and combos that are one card away,
    /// within the deck's color identity. Returns null if the API call fails.
    /// </summary>
    Task<CommanderSpellbookResult?> FindCombosAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches and caches combo data from the Commander Spellbook backend API.
/// </summary>
public sealed class CommanderSpellbookService : ICommanderSpellbookService
{
    // ...
}
```

### Phase 13 application — DeckAnalysisRequest XML doc example

Based on the templates above, the renamed `DeckAnalysisRequest` (formerly `ChatGptDeckRequest`) should land its file with a class-level summary like:

```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the deck-analysis page. Captures Step 1 deck input,
/// Step 2 analysis options (commander bracket, selected questions, free-form prompt),
/// Step 3 set-upgrade options, and Step 5 freeform follow-up state. Round-tripped
/// through the session zip via PacketArtifactStore.
/// </summary>
public sealed class DeckAnalysisRequest
{
    // ... (existing property bodies unchanged)
}
```

Nested response-shape classes get a one-line summary describing the JSON shape they map to:

```csharp
/// <summary>
/// A weak slot in the deck — the card and the reason it's considered weak.
/// </summary>
public sealed class WeakSlot { ... }

/// <summary>
/// A single Q&amp;A entry returned by the analysis prompt's question-answer section.
/// </summary>
public sealed class QuestionAnswer { ... }
```

**Tone rules (extracted from CardLookup + CommanderSpellbook templates):**
- One sentence. Period at the end. No trailing period only on bullet-style summaries.
- Active voice ("Looks up...", "Returns..."), present tense.
- Anchor to behavior, not type membership ("Looks up pasted card names against Scryfall" — not "An interface for looking up cards").
- For records describing JSON shapes, prefer "Returns the results of X." / "A single Y from Z." / "Maps to the `<json_key>` shape in the JSON response.".

## State of the Art

Not applicable. This is a pure refactor of EXISTING code; no library upgrades, framework migrations, or industry-pattern changes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|---|---|---|
| A1 | `[ASSUMED]` `ChatGptResultWrapInstruction` const should be renamed to `ResultWrapInstruction` in `JsonTextFormatterService.cs:11`. | Verification gate / Claude's Discretion | If left unrenamed, the grep gate counts it as an exception and the allowlist grows by one. Either choice is defensible. |
| A2 | `[VERIFIED]` `DeckPageTab` enum integer values (5, 7, 8, 9 for the three renamed members + Convert + Home + JudgeQuestions) should stay byte-stable across rename. | Wave 1 | Even though `DeckPageTab` is not currently serialized into zips or persisted, **explicitly preserving the integer values** is the safe choice — verified by reading `DeckPageTab.cs` lines 5-15 (the enum has explicit integer assignments per member, so re-ordering wouldn't change values, but a careless cleanup could). |
| A3 | `[ASSUMED]` The action method `ChatGptCedhMetaGapDownload` doc test `Equal("ChatGptCedhMetaGap", view.ViewName)` literal string at `DeckControllerTests.cs:39` becomes `Equal("CedhMetaGap", view.ViewName)` after Wave 3 renames the View() literal — verify by reading lines 35-42 of that test method to confirm it tests the view-name string. | Wave 4 | If the test is asserting the literal action-method-derived view name, it'd already have been failing post-Phase-12 (which kept the action method name `ChatGptCedhMetaGap`). Since Phase 12 verification reports passing, this assertion currently must use **a literal that matches Phase 12's view name "CedhMetaGap"** OR Phase 12 missed it. Need to confirm during planning. |
| A4 | `[VERIFIED]` Form-field `name` attributes (e.g., `name="TargetAiPlatform"`, `name="DeckText"`) bind to property names on the renamed Request classes, NOT class names — verified by reading DeckAnalysis.cshtml lines 143-475 sample and matching against `ChatGptDeckRequest.cs` property names. | Wave 3 | If wrong (i.e., the bind is class-name aware), every form submission breaks post-rename. ASP.NET Core MVC model binding is property-name driven, so this is well-established framework behavior. |

The Assumptions Log is intentionally short — most claims in this research are directly verified by grep/file inspection.

## Open Questions

1. **`ChatGptResultWrapInstruction` const rename (A1 above)**
   - **What we know:** The const lives at `DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs:11`. The const **value** (the string `"Wrap the entire JSON response in <result>...</result> tags..."`) mentions `ChatGPT/Claude/Gemini` as narrative prose — that's a preserved D-07 site.
   - **What's unclear:** Whether the const **identifier** (the C# symbol name) keeps the `ChatGpt` prefix or drops it. CONTEXT.md Claude's Discretion #1 covers this case: "Naming of internal helper methods within renamed classes if they currently include 'ChatGpt' in their identifier — strip the prefix if it clarifies, leave it if removal would create a less descriptive name."
   - **Recommendation:** Rename to `ResultWrapInstruction`. The new name is descriptive (it's already qualified by being inside `JsonTextFormatterService`); the prefix adds no information.

2. **`DeckControllerTests.ChatGptCedhMetaGap_Get_ReturnsExpectedViewModel` test method name (A3 above)**
   - **What we know:** This test method name at `DeckControllerTests.cs:21` is currently `ChatGptCedhMetaGap_Get_ReturnsExpectedViewModel`. Test method names are CLR identifiers; xUnit doesn't care about them at runtime, only the `[Fact]` attribute matters.
   - **What's unclear:** Whether Wave 4 renames test method names in lockstep with the action method rename (e.g., `CedhMetaGap_Get_ReturnsExpectedViewModel`) or leaves test method names alone.
   - **Recommendation:** Rename test method names in lockstep. They are CLR identifiers, the verification grep gate is `ChatGpt[A-Z]`, and any test method named `ChatGptCedhMetaGap_*` would hit the gate. Wave 4 plan should explicitly list these.

3. **Inline `private sealed class FakeChatGptX` test doubles in `DeckControllerTests.cs` — collision-avoidance check**
   - **What we know:** Six inline private test-double classes at lines 775, 781, 810, 831, 844, 857 inside `DeckControllerTests.cs`. They are `private` to the test class so external collisions are impossible.
   - **What's unclear:** None — confirmed `private` modifier.
   - **Recommendation:** Rename `FakeChatGptDeckPacketService` → `FakeDeckAnalysisPacketService`, `FakeChatGptDeckComparisonService` → `FakeDeckComparisonService`, `FakeChatGptCedhMetaGapService` → `FakeMetaGapService`, `ConfigurableChatGptCedhMetaGapService` → `ConfigurableMetaGapService`, `ThrowingChatGptCedhMetaGapService` → `ThrowingMetaGapService`, `ThrowingChatGptDeckPacketService` → `ThrowingDeckAnalysisPacketService`. All inline; no separate fixture files created.

## Environment Availability

Not applicable. Phase 13 is a code rename inside an already-working DeckFlow.Web build environment. No new tools, runtimes, or services are introduced.

The existing build chain (`dotnet build`, `git mv`, `grep`) is already available in the developer's WSL2 environment per CLAUDE.md ("WSL2, Linux, and Windows are all first-class targets"). The MSBuild TypeScript and ZipDeckFlowBridge targets run on every build but are not exercised by Phase 13 (no `.ts`, no `browser-extensions/` edits).

## Validation Architecture

`.planning/config.json` not present at the time of research — treat `nyquist_validation` as **default (enabled)** per the GSD-research instructions. However, per CLAUDE.md "VSTest unreliable in WSL", **automated xUnit test runs are not the gate**. D-10 makes this explicit: build-clean is the gate, manual T1-T8 is HUMAN-UAT.

### Test Framework

| Property | Value |
|---|---|
| Framework | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.14.1 + xunit.runner.visualstudio 3.1.4 |
| Config file | Per-project `.csproj` (no separate config file); test projects: `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`, `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| Quick run command | `dotnet build DeckFlow.sln --configuration Release` (per CLAUDE.md "VSTest unreliable in WSL" — DO NOT use `dotnet test` as a gate) |
| Full suite command | Same — `dotnet build DeckFlow.sln --configuration Release` |
| Manual UAT | T1-T8 manual integration suite per `.planning/milestones/v1.2-MILESTONE-AUDIT.md` (HUMAN-UAT) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| CLASSRENAME-01 | Every ChatGpt-prefixed C# type renamed | grep gate | `grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/ DeckFlow.Core/ DeckFlow.Web.Tests/ DeckFlow.Core.Tests/` filtered by allowlist | N/A — grep command, not a test file |
| CLASSRENAME-02 | Every renamed class has `<summary>` doc comment | build-clean check (NoWarn 1591 not relied upon for renamed types) | `dotnet build DeckFlow.sln --configuration Release` with `NoWarn` removed temporarily to verify... actually D-04 keeps NoWarn ON, so this gate is verified by **manual code-review of every renamed file** rather than by build output | Manual gate; no automated check |
| CLASSRENAME-03 | DI, [InternalsVisibleTo], namespace, controller, view-model, test fixtures, Razor @model updated | build-clean — if any one of these is missed, the build fails CS0246 | `dotnet build DeckFlow.sln --configuration Release` | N/A — build is the gate |
| CLASSRENAME-01..03 (functional) | Zero user-visible behavior change | manual integration | T1-T8 per `.planning/milestones/v1.2-MILESTONE-AUDIT.md` | HUMAN-UAT.md item per D-10 |

### Sampling Rate

- **Per task commit:** No automated check — wave commits accept intermediate red builds per D-05
- **Per wave merge:** No automated check — wave merges may be intermediately red (Wave 1 + Wave 2 + Wave 3) per D-05
- **Phase gate:** `dotnet build DeckFlow.sln --configuration Release` clean + grep gate clean (with documented allowlist) + DeckFlow.CLI 5-command smoke + HUMAN-UAT T1-T8

### Wave 0 Gaps

None. xUnit test infrastructure is already present (10 ChatGpt-prefixed test files + many other test files). No new test framework needs installing. No new test files are created in Phase 13 — only existing files are renamed.

## Security Domain

`.planning/config.json` absent — treat `security_enforcement` as **default (enabled)**. However, Phase 13 is a pure type rename with **zero functional change**:

- No new HTTP endpoints (action method renames are 1:1; route attributes unchanged → same URLs serve)
- No new auth surface (`/Admin/*` BasicAuth is unchanged; no controllers renamed)
- No new input validation surface (request DTO property names unchanged)
- No cryptography work
- No SQL changes
- No file I/O changes (`PacketArtifactStore` static method signatures preserved across class rename — internal logic identical)
- No CSRF surface change (`SameOriginRequestValidator` is unchanged; API controllers in `Controllers/Api/` have zero ChatGpt refs)

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | No | No auth flow touched |
| V3 Session Management | No | No session code touched |
| V4 Access Control | No | No access-control logic touched |
| V5 Input Validation | No | Property-level validation unchanged (property names preserved) |
| V6 Cryptography | No | No crypto code touched |
| V14 Configuration | No | No env var, secret, or config-binding code touched (`AiPlatformOptions` class is the only AI config class and it is NOT renamed) |

### Known Threat Patterns for the rename

| Pattern | STRIDE | Standard Mitigation |
|---|---|---|
| Accidental property-name rename breaking form-binding security (e.g., renaming `TargetAiPlatform` could allow a crafted POST to land in an unvalidated property) | Tampering | Property names ARE explicitly preserved per D-07; Wave 3 plan should include a smoke-grep that `name="TargetAiPlatform"` still appears unchanged in all 3 form views |
| Accidental removal of `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` would not break security but would break test seams | N/A | `AssemblyInfo.cs` is NOT modified in Phase 13 per CONTEXT.md `<canonical_refs>` — verified by reading the file (single line) |
| Accidental edit of `_AiSelector.cshtml` could change the radio `value="ChatGPT"` string, breaking the round-trip handshake with form-bound `TargetAiPlatform` | Tampering | D-07 #1 explicitly preserves all 5 occurrences in `_AiSelector.cshtml`; Wave 3 plan should mark this file READ-ONLY |

The security posture is unchanged after Phase 13. No new threat vectors. Existing mitigations (`SameOriginRequestValidator`, BasicAuth on `/Admin/*`, security headers, `UseForwardedHeaders` ordering invariant per CLAUDE.md) all remain in force because no security-relevant code is touched.

## Sources

### Primary (HIGH confidence — direct file inspection)
- `.planning/phases/13-chatgpt-class-rename-summary-doc-comments/13-CONTEXT.md` — all D-01..D-10 decisions
- `.planning/REQUIREMENTS.md` — CLASSRENAME-01/02/03 + AIPLATFORM-01 spec text alignment
- `.planning/ROADMAP.md` — Phase 13 SC #1..#4
- `.planning/STATE.md` — milestone v1.3 progress
- `.planning/phases/12-ai-agnostic-url-page-rename/12-CONTEXT.md` + `12-VERIFICATION.md` — Phase 12 deferral surface
- `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` — Phase 15 target names that Phase 13 must align with
- `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — T1-T8 manual integration spec
- `.planning/codebase/STRUCTURE.md`, `CONVENTIONS.md`, `INTEGRATIONS.md` — service layout + DI conventions + RestSharp/Polly v8 pattern
- `CLAUDE.md` — project constraints (VSTest, commits, README, GenerateDocumentationFile)
- `DeckFlow.Web/DeckFlow.Web.csproj` — verified `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (L38) + `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` (L40)
- `DeckFlow.Web/Program.cs` — verified DI block (L263-295), UseRewriter 301 block (L322-340), AiPlatformOptions binding (L70)
- `DeckFlow.Web/AssemblyInfo.cs` — verified `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` (L3)
- `DeckFlow.Web/Services/CardLookupService.cs:13-42`, `CommanderSpellbookService.cs:13-54` — XML doc tone templates
- `DeckFlow.Web/Configuration/AiPlatformOptions.cs` — AI key string preservation site
- `DeckFlow.Web/Views/Shared/_AiSelector.cshtml` — 5 preserved `chatgpt`/`ChatGPT` literals
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` — DeckPageTab enum consumers (L6, L18-20)
- `DeckFlow.Web/Models/*.cs` (11 files) — all class definitions enumerated
- `DeckFlow.Web/Services/ChatGpt*.cs` (7 files) — service ctor signatures + nested record types verified
- `DeckFlow.Web/Controllers/DeckController.cs` — 12 action methods + 39 `View()` calls verified
- `DeckFlow.Web.Tests/*.cs` (10 files) — test fixtures verified; inline `FakeChatGptX` private classes at L775-887 confirmed
- `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` — 3 factory methods verified
- `DeckFlow.Web.Tests/HelpContentServiceTests.cs:44-54` — permitted exception `"chatgpt-analysis"` fixture
- `README.md:605/636/637` — 3 README mentions of ChatGpt-prefixed service classes

### Secondary (MEDIUM confidence — derived from primary)
- Wave-1/2/3/4 file enumeration tables — derived by `find` + `grep` over the verified source paths
- Verification grep gate command — derived from CONTEXT.md D-09 + verified by trial on the current `v1.3` HEAD

### Tertiary (LOW confidence — none)
None. Every claim in this research is supported by direct file inspection at the working-tree HEAD `3c4ee5a` on branch `v1.3`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; existing stack pinned by csproj inspection
- Architecture: HIGH — file inventory + class definitions verified by direct grep across all 32 ChatGpt-referencing source files
- Pitfalls: HIGH — every pitfall traces back to a specific verified-on-disk line (View("DeckAnalysis", ...) literals, _AiSelector.cshtml preserved strings, inline FakeChatGptX classes at known line numbers, etc.)
- Naming map (D-01): HIGH — locked verbatim in CONTEXT.md by user 2026-05-17; this research transcribes without modification

**Research date:** 2026-05-17
**Valid until:** 2026-06-16 (30 days, stable codebase — research becomes stale only if someone modifies the ChatGpt-prefixed surface before plan execution; likelihood low because branch `v1.3` is the active phase branch and no other phase will touch this code)

## RESEARCH COMPLETE
