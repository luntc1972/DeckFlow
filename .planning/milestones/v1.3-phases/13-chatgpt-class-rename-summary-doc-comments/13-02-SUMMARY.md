---
phase: 13
plan: 13-02
wave: 2
subsystem: web-services
tags: [refactor, rename, xml-docs, di-registration, deck-analysis, deck-comparison, meta-gap]
requirements: [CLASSRENAME-01, CLASSRENAME-02, CLASSRENAME-03]
requires:
  - 13-01
provides:
  - DeckFlow.Web.Services.IDeckAnalysisPacketService
  - DeckFlow.Web.Services.DeckAnalysisPacketService
  - DeckFlow.Web.Services.DeckAnalysisPacketResult
  - DeckFlow.Web.Services.IDeckComparisonService
  - DeckFlow.Web.Services.DeckComparisonService
  - DeckFlow.Web.Services.DeckComparisonResult
  - DeckFlow.Web.Services.IMetaGapService
  - DeckFlow.Web.Services.MetaGapService
  - DeckFlow.Web.Services.MetaGapResult
  - DeckFlow.Web.Services.PacketArtifactStore
  - DeckFlow.Web.Services.RequestContextParser
  - DeckFlow.Web.Services.ResponseParsers
  - DeckFlow.Web.Services.JsonTextFormatterService
  - DeckFlow.Web.Services.JsonTextFormatterService.ResultWrapInstruction (renamed from ChatGptResultWrapInstruction)
affects:
  - DeckFlow.Web/Controllers/DeckController.cs (Wave 3 — references all renamed interfaces, services, and result records)
  - DeckFlow.Web.Tests/* (Wave 4 — test fixtures and test class names: ChatGptDeckPacketServiceTests, ChatGptDeckComparisonServiceTests, ChatGptCedhMetaGapServiceTests, ChatGptPacketArtifactStore[RoundTrip]Tests, ChatGptResponseParsersTests, ChatGptJsonTextFormatterServiceTests, ChatGptResultContractTests, ChatGptPhase10RoundTripTests)
tech-stack:
  added: []
  patterns:
    - Pattern 1 (interface + sealed class + result record triplet — DeckAnalysisPacketService.cs preserves `sealed partial class` per [GeneratedRegex] coupling; the other two adopt plain `sealed class`)
    - Pattern 5 (static helper class — PacketArtifactStore, RequestContextParser [partial], ResponseParsers, JsonTextFormatterService)
    - Pattern 6 (DI registration triplet update — interface arg + new ImplementationName() + ILogger<T> generic — three blocks in Program.cs L263-295)
key-files:
  created:
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/DeckComparisonService.cs
    - DeckFlow.Web/Services/MetaGapService.cs
    - DeckFlow.Web/Services/PacketArtifactStore.cs
    - DeckFlow.Web/Services/RequestContextParser.cs
    - DeckFlow.Web/Services/ResponseParsers.cs
    - DeckFlow.Web/Services/JsonTextFormatterService.cs
  modified:
    - DeckFlow.Web/Program.cs
    - README.md
  deleted:
    - DeckFlow.Web/Services/ChatGptDeckPacketService.cs
    - DeckFlow.Web/Services/ChatGptDeckComparisonService.cs
    - DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs
    - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs
    - DeckFlow.Web/Services/ChatGptRequestContextParser.cs
    - DeckFlow.Web/Services/ChatGptResponseParsers.cs
    - DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs
decisions:
  - D-01: applied — all 7 service files renamed and all 13 types renamed per the locked naming map; const ChatGptResultWrapInstruction renamed to ResultWrapInstruction per Open Question 1
  - D-03: applied — class-level <summary> doc comments added to every renamed type using behavior-anchored verb-object form per CardLookupService/CommanderSpellbookService tone analogs
  - D-05: applied — `git mv` used for all 7 file renames; each rename committed atomically; intermediate red build accepted (controller + tests still reference old names)
  - D-07 #1: preserved — `"ChatGPT"` AI key string default values in request DTOs unchanged (Wave 1 already preserved); internal helper method suffixes BuildAnalysisPromptChatGpt/Claude/Gemini, BuildSetUpgradePromptChatGpt/Claude/Gemini, BuildComparisonPromptChatGpt/Claude/Gemini left byte-identical (these distinguish per-AI prompt variants)
  - D-07 #4: preserved — 3 `"chatgpt"` lowercase fallback literals in PacketArtifactStore.cs L537/L540/L543 BYTE-IDENTICAL (Phase 10 commit 00e5bdd invariant)
  - D-07 #5: preserved — "ChatGPT" inside <summary> doc-comment narrative permitted; inside ResultWrapInstruction const VALUE string permitted; inside README architecture-bullet phrase "structured ChatGPT prompts with a JSON output schema" permitted
  - D-08: respected — IEdhTop16Client / EdhTop16Entry references in MetaGapService preserved (15 hits in MetaGapService.cs)
  - Claude's Discretion #1: internal method-name suffixes `*ChatGpt`, `*Claude`, `*Gemini` retained — they distinguish per-AI prompt-builder variants and removing the AI-name suffix would be less descriptive
  - Wave 3 forward-looking note: doc-comment narrative refs to the Wave-4 test class identifier `ChatGptResultContractTests` were proactively rewritten to "the AI result contract tests" (no C# identifier) to satisfy the Wave 2 grep gate
metrics:
  duration_minutes: ~50
  tasks_completed: 3
  files_renamed: 7
  files_edited: 2
  types_renamed: 13
  const_renamed: 1
  di_registrations_updated: 3
  readme_lines_updated: 3
  commits: 10
  completed_date: 2026-05-17
---

# Phase 13 Plan 13-02: Services + DI + README — ChatGpt* Class Rename + XML Summaries (Wave 2)

Renamed all 7 `ChatGpt*` service files in `DeckFlow.Web/Services/` via `git mv` to AI-agnostic filenames per D-01, renamed all 13 type declarations inside (3 interfaces + 3 sealed service classes + 3 sealed result records + 4 static helper classes), renamed the internal const `ChatGptResultWrapInstruction` → `ResultWrapInstruction` (resolving Open Question 1 in 13-RESEARCH.md L734 per Claude's Discretion #1 in CONTEXT.md), updated all 3 DI registrations in `Program.cs:263-295`, updated the 3 README service-name mentions at L605/L636/L637, and backfilled `/// <summary>` XML doc comments on every renamed type using the canonical `CardLookupService.cs` / `CommanderSpellbookService.cs` tone analogs.

## Goal

Bring the C# service-layer naming in line with the Phase 12 user-facing AI-agnostic page slugs (`deck-analysis`, `deck-comparison`, `cedh-meta-gap`) and the Wave 1 model rename (29 model types already renamed in plan 13-01). The build remains RED at end of Wave 2 because `DeckController.cs` and `DeckFlow.Web.Tests/*` still reference the old service interface/class names — closed by Waves 3 and 4 per the locked D-05 wave plan.

## What Was Built

### File renames (7 × `git mv`)

| Old path | New path | Types renamed inside |
|---|---|---|
| `Services/ChatGptDeckPacketService.cs` | `Services/DeckAnalysisPacketService.cs` | `IChatGptDeckPacketService` → `IDeckAnalysisPacketService`; `ChatGptDeckPacketService` (sealed partial class) → `DeckAnalysisPacketService`; `ChatGptDeckPacketResult` (sealed record) → `DeckAnalysisPacketResult` |
| `Services/ChatGptDeckComparisonService.cs` | `Services/DeckComparisonService.cs` | `IChatGptDeckComparisonService` → `IDeckComparisonService`; `ChatGptDeckComparisonService` (sealed class) → `DeckComparisonService`; `ChatGptDeckComparisonResult` (sealed record) → `DeckComparisonResult` |
| `Services/ChatGptCedhMetaGapService.cs` | `Services/MetaGapService.cs` | `IChatGptCedhMetaGapService` → `IMetaGapService`; `ChatGptCedhMetaGapService` (sealed class) → `MetaGapService`; `ChatGptCedhMetaGapResult` (sealed record) → `MetaGapResult` |
| `Services/ChatGptPacketArtifactStore.cs` | `Services/PacketArtifactStore.cs` | `ChatGptPacketArtifactStore` (internal static class) → `PacketArtifactStore` |
| `Services/ChatGptRequestContextParser.cs` | `Services/RequestContextParser.cs` | `ChatGptRequestContextParser` (internal static **partial** class) → `RequestContextParser` (partial modifier preserved) |
| `Services/ChatGptResponseParsers.cs` | `Services/ResponseParsers.cs` | `ChatGptResponseParsers` (internal static class) → `ResponseParsers` |
| `Services/ChatGptJsonTextFormatterService.cs` | `Services/JsonTextFormatterService.cs` | `ChatGptJsonTextFormatterService` (public static class) → `JsonTextFormatterService` |

**Total: 7 files renamed, 13 public/internal types renamed in lockstep.**

### Internal const rename (per Open Question 1 in 13-RESEARCH.md L734)

| Old name | New name | File | Notes |
|---|---|---|---|
| `ChatGptResultWrapInstruction` | `ResultWrapInstruction` | `Services/JsonTextFormatterService.cs:13` | Const VALUE string preserved byte-identical (mentions "ChatGPT/Claude/Gemini" as narrative prose, permitted under D-07 #5). 15 callers in the renamed service files (DeckAnalysisPacketService, DeckComparisonService, MetaGapService) updated in lockstep. |

### Intra-file model reference updates (Wave-1 lockstep)

All references to Wave-1-renamed model types updated across the 7 renamed service files:

- `ChatGptDeckRequest` → `DeckAnalysisRequest`
- `ChatGptDeckAnalysisResponse` (+ nested `ChatGptWeakSlot`, `ChatGptQuestionAnswer`, `ChatGptDeckVersion`) → `DeckAnalysisResponse` (+ `WeakSlot`, `QuestionAnswer`, `DeckVersion`)
- `ChatGptSetUpgradeResponse` (+ nested `ChatGptSetUpgradeSet`, `ChatGptSetUpgradeTopAdd`, `ChatGptSetUpgradeCardNote`, `ChatGptSetUpgradeShortlist`) → `SetUpgradeResponse` (+ `SetUpgradeSet`, `SetUpgradeTopAdd`, `SetUpgradeCardNote`, `SetUpgradeShortlist`)
- `ChatGptDeckComparisonRequest` → `DeckComparisonRequest`
- `ChatGptDeckComparisonResponse` (+ nested `ChatGptDeckComparisonRecommendation`) → `DeckComparisonResponse` (+ `DeckComparisonRecommendation`)
- `ChatGptCedhMetaGapRequest` → `MetaGapRequest`
- `ChatGptCedhMetaGapResponse` (+ 11 nested `ChatGptCedh*` shape classes) → `MetaGapResponse` (+ `MetaGapData`, `WinLineSet`, `WinLines`, `Interaction`, `Speed`, `ManaEfficiency`, `CoreConvergenceCard`, `MissingStaple`, `PotentialCut`, `TopAdd`, `TopCut`)

### Cross-service narrative `<see cref>` updates

`<see cref="ChatGptDeckPacketService.BuildRequestContextText(DeckFlow.Web.Models.ChatGptDeckRequest, string?)" />` in `RequestContextParser.cs:8` → updated to `<see cref="DeckAnalysisPacketService.BuildRequestContextText(DeckFlow.Web.Models.DeckAnalysisRequest, string?)" />`. Similar narrative `Mirrors <see cref="ChatGptDeckPacketService"/>` comments in `DeckComparisonService.cs:193` and `MetaGapService.cs:239` updated.

### Program.cs DI registrations (3 triplets at L263-295)

All three `AddScoped<...>` factory blocks updated in lockstep:

| Old registration | New registration |
|---|---|
| `AddScoped<IChatGptDeckPacketService>(sp => new ChatGptDeckPacketService(..., sp.GetService<ILogger<ChatGptDeckPacketService>>()))` | `AddScoped<IDeckAnalysisPacketService>(sp => new DeckAnalysisPacketService(..., sp.GetService<ILogger<DeckAnalysisPacketService>>()))` |
| `AddScoped<IChatGptDeckComparisonService>(sp => new ChatGptDeckComparisonService(..., sp.GetService<ILogger<ChatGptDeckComparisonService>>()))` | `AddScoped<IDeckComparisonService>(sp => new DeckComparisonService(..., sp.GetService<ILogger<DeckComparisonService>>()))` |
| `AddScoped<IChatGptCedhMetaGapService>(sp => new ChatGptCedhMetaGapService(...))` | `AddScoped<IMetaGapService>(sp => new MetaGapService(...))` |

Constructor parameter ORDER + parameter TYPES + `AddScoped` lifetime + every `sp.GetRequiredService<T>()` call preserved BYTE-IDENTICAL. Only the three identifier surfaces per Pattern 6 changed (interface arg, `new X(` impl name, `ILogger<X>` generic arg). Total Program.cs diff: 8 insertions / 8 deletions across the 16 changed identifier lines.

### Program.cs 301-redirect block preservation (Phase 12 invariant)

The 12 `AddRedirect("^chatgpt-...")` rewrite rules at L329-340 are BYTE-IDENTICAL pre/post Wave 2:

```
chatgpt-packets         → 3 hits (L330-332)
chatgpt-deck-comparison → 4 hits (L333-335, L340)
chatgpt-cedh-meta-gap   → 3 hits (L336-338)
chatgpt-analysis        → 1 hit  (L339, help/chatgpt-analysis)
```

These are Phase 12 URL-slug redirect SOURCES, not renamed C# types. They are coupled to externally-bookmarked URLs and stay forever per D-07/D-08.

### README.md mentions (3 lines)

| Line | Before | After |
|---|---|---|
| L605 | `\`ChatGptDeckPacketService\` throttles all Scryfall calls to ~110ms apart` | `\`DeckAnalysisPacketService\` throttles all Scryfall calls to ~110ms apart` |
| L636 | `\`ChatGptDeckPacketService\` parallelizes independent fetches (banned-list, set-packet, Commander Spellbook)` | `\`DeckAnalysisPacketService\` parallelizes independent fetches (banned-list, set-packet, Commander Spellbook)` |
| L637 | `\`ChatGptDeckComparisonService\` parses two decklists, resolves cards via Scryfall, ...generates structured ChatGPT prompts with a JSON output schema.` | `\`DeckComparisonService\` parses two decklists, resolves cards via Scryfall, ...generates structured ChatGPT prompts with a JSON output schema.` |

Surrounding prose preserved byte-identical including "structured ChatGPT prompts" narrative (D-07 #5 — AI platform name as visible prose).

### XML `<summary>` doc-comment backfill (D-03)

Each renamed type carries a behavior-anchored one-sentence summary anchored to its actual responsibility (verb-object form, present tense, anchored to call-site behavior per CardLookupService.cs / CommanderSpellbookService.cs tone analogs):

| File | Summary tone |
|---|---|
| `IDeckAnalysisPacketService` | "Builds analysis and set-upgrade prompt packets for the deck-analysis page." |
| `DeckAnalysisPacketResult` | "Returns the results of a deck-analysis packet build." |
| `DeckAnalysisPacketService` | "Builds analysis and set-upgrade prompt packets by hydrating decks via Scryfall, banlist, and Commander Spellbook lookups, then composing the JSON-bound prompt artifacts saved to the session zip." |
| `IDeckComparisonService` | "Builds the deck-comparison prompt packet by hydrating two decks side-by-side." |
| `DeckComparisonResult` | "Returns the results of a deck-comparison packet build." |
| `DeckComparisonService` | "Hydrates two decks via Scryfall, queries Commander Spellbook for each, derives the side-by-side comparison context (role counts, mana curves, combo gaps), and composes the JSON-bound comparison prompt artifacts saved to the session zip." |
| `IMetaGapService` | "Builds the cEDH meta-gap prompt packet using edhtop16 reference decks." |
| `MetaGapResult` | "Returns the results of a cEDH meta-gap packet build." |
| `MetaGapService` | "Fetches top edhtop16 reference decks for the user's commander, hydrates them via Scryfall and Commander Spellbook, derives the cEDH meta-gap context (core convergence, missing staples, potential cuts), and composes the JSON-bound meta-gap prompt artifacts saved to the session zip." |
| `PacketArtifactStore` | "Builds, saves, and loads session zip artifacts for the deck-analysis, deck-comparison, and cEDH meta-gap pages, including AI-segment filename sanitization. Pure CPU work, no filesystem access." |
| `RequestContextParser` | "Parses the YAML-like `01-request-context.txt` payload emitted by `DeckAnalysisPacketService.BuildRequestContextText(...)`." (existing summary from earlier; cref updated) |
| `ResponseParsers` | "Parses the analysis and set-upgrade JSON returns from the AI into the strongly-typed response shapes used by the deck-analysis page. Pure helpers — no side effects, no I/O, safe to call from anywhere." |
| `JsonTextFormatterService` | "Extracts the `<result>...</result>` payload or fenced-JSON block from AI text returns so the response parsers can consume well-formed JSON." |

## Wave 2 Verification Gate

```bash
$ grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Services/ DeckFlow.Web/Program.cs
# (zero output)
$ grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Services/ DeckFlow.Web/Program.cs | wc -l
0
```

ZERO `ChatGpt[A-Z]` identifier hits across all 7 renamed service files + Program.cs. Wave 2 gate passes.

### Preservation checks

- **PacketArtifactStore.cs `"chatgpt"` AI-segment fallbacks** — 3 hits at L537, L540, L543 BYTE-IDENTICAL (D-07 #4 / Phase 10 commit `00e5bdd` invariant). Verified by `grep -c '"chatgpt"' DeckFlow.Web/Services/PacketArtifactStore.cs` → **3**.
- **RequestContextParser `partial` modifier** — `grep -c 'internal static partial class RequestContextParser' DeckFlow.Web/Services/RequestContextParser.cs` → **1**. `[GeneratedRegex(...)]` attribute on `TopLevelKeyPattern()` byte-identical.
- **ResultWrapInstruction const** — `grep -c 'ResultWrapInstruction' DeckFlow.Web/Services/JsonTextFormatterService.cs` → **1** (the const declaration). 15 callers across the 3 service files reference the renamed name.
- **Old ChatGptResultWrapInstruction absent** — `grep -c 'ChatGptResultWrapInstruction' DeckFlow.Web/Services/JsonTextFormatterService.cs` → **0**.
- **Program.cs 301-redirect block** — `grep -cE 'chatgpt-(packets|deck-comparison|cedh-meta-gap)' DeckFlow.Web/Program.cs` → **10** (3+4+3 hits across the 12 AddRedirect rules including `help/chatgpt-analysis`). Phase 12 URL invariants preserved.
- **Program.cs DI registrations** — `grep -cE 'AddScoped<I(DeckAnalysisPacket|DeckComparison|MetaGap)Service>' DeckFlow.Web/Program.cs` → **3**.
- **AI-platform method-name suffixes** — `BuildAnalysisPromptChatGpt`, `BuildAnalysisPromptClaude`, `BuildAnalysisPromptGemini` (and SetUpgrade + Comparison variants) preserved BYTE-IDENTICAL. The substring `ChatGpt(` (paren) does NOT match `ChatGpt[A-Z]` so these pass the Wave 2 gate.
- **README service mentions** — `grep -cE 'ChatGptDeck(Packet|Comparison)Service' README.md` → **0**.
- **IEdhTop16Client/EdhTop16Entry** — 15 hits in MetaGapService.cs preserved (out of CLASSRENAME scope per D-08).

## Commits (10 plain-author, no Co-Authored-By trailer)

| Hash | Message |
|---|---|
| `ef059b1` | refactor(13-02): rename ChatGptDeckPacketService triplet to DeckAnalysisPacketService with XML summaries (rename-only) |
| `8760c3b` | refactor(13-02): apply ChatGptDeckPacketService -> DeckAnalysisPacketService identifier rewrites (content fixup) |
| `1e4cf1d` | refactor(13-02): rename ChatGptDeckComparisonService triplet to DeckComparisonService with XML summaries |
| `580b300` | refactor(13-02): rename ChatGptCedhMetaGapService triplet to MetaGapService with XML summaries |
| `fce97c9` | refactor(13-02): rename ChatGptPacketArtifactStore to PacketArtifactStore with XML summaries (preserve chatgpt AI-segment fallback) |
| `1f643e4` | refactor(13-02): rename ChatGptRequestContextParser to RequestContextParser (preserve partial modifier) with XML summary |
| `ad51e06` | refactor(13-02): rename ChatGptResponseParsers to ResponseParsers with XML summaries |
| `af3102d` | refactor(13-02): rename ChatGptJsonTextFormatterService to JsonTextFormatterService and ChatGptResultWrapInstruction to ResultWrapInstruction with XML summaries |
| `89f1981` | refactor(13-02): update Program.cs DI registrations for renamed ChatGpt* services |
| `c409517` | docs(13-02): update README.md service names to DeckAnalysisPacketService and DeckComparisonService |

Plain author across all 10: `Chris Lunt <luntc1972@yahoo.com>`. No Co-Authored-By trailer.

## Deviations from Plan

### 1. [Rule 3 — process fix] Split first file rename into two commits (rename + content fixup)

- **Found during:** Task 1, File 1 commit step
- **Issue:** The first `git mv` + content-rewrite cycle produced a commit (`ef059b1`) that captured ONLY the rename (similarity 100% in git's view) without the bulk identifier substitutions. The disk file was correct (verified zero `ChatGpt[A-Z]` hits before the commit), but the index had retained the post-`git mv` pre-edit state. A second commit (`8760c3b`) was needed to land the identifier rewrites.
- **Root cause:** When running `git mv A B` immediately followed by Python in-place edits to B, the index keeps the rename of the unedited content; a follow-up `git add B` (or `git add -u B`) is required to refresh the index with the working-tree changes. I missed the explicit re-`add` before the first commit.
- **Fix for remaining files:** Adopted the pattern `git mv` → in-place edits → `git add -u` (or `git add B`) → check `git diff --cached --find-renames --stat` → commit. The remaining 6 file renames each landed in ONE commit at 80-98% similarity (`1e4cf1d`, `580b300`, `fce97c9`, `1f643e4`, `ad51e06`, `af3102d`).
- **Net deviation count:** +1 commit beyond the plan's projected "8-9 commits" → 10 commits total. The first file rename ended up as a 2-commit logical unit (`ef059b1` rename + `8760c3b` content) instead of 1, but each commit is still atomic per CLAUDE.md "one logical change per commit". This is the same flavor of net-+1-commit deviation Wave 1 logged (12 commits instead of 11).
- **Files modified:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (only — fixup commit was scoped to this one file).
- **Commits:** `ef059b1` (rename only) + `8760c3b` (identifier rewrites + summary updates).

### 2. [Rule 2 — required for grep-gate correctness] Pre-rewrite of Wave-4 test-class identifier in narrative doc-comments

- **Found during:** Task 1 — Wave 2 grep verification preview
- **Issue:** 6 doc-comment narrative references to the Wave-4 test class `ChatGptResultContractTests` appeared in `ChatGptDeckPacketService.cs`, `ChatGptDeckComparisonService.cs`, and `ChatGptCedhMetaGapService.cs` (1+3+2 occurrences in `///` or `//` comment lines next to internal-for-test dispatcher methods). The Wave 2 grep gate `grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Services/ DeckFlow.Web/Program.cs` matches `ChatGptResultContractTests` because `[A-Z]` matches `R`.
- **Why this is in-scope for Wave 2:** Same precedent as Wave 1 deviation #1 — D-07 #5 permits "ChatGPT" inside `<summary>` narrative AS AN AI NAME WORD (e.g., "Parses the ChatGPT-returned JSON payload"); it does NOT permit a renamed C# **identifier** to remain. Leaving these identifier-references would fail the Wave 2 gate and would also be stale post-Wave-4 anyway.
- **Fix:** Rewrote narrative to drop the C# identifier and use generic phrasing: "exercised by the AI result contract tests" (instead of `exercised by ChatGptResultContractTests`). This is non-prescriptive about the Wave 4 rename target name (Wave 4 will pick a final name; this Wave 2 deviation does NOT presume what that name is).
- **Files modified:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`, `DeckFlow.Web/Services/DeckComparisonService.cs`, `DeckFlow.Web/Services/MetaGapService.cs` (3 files, 6 line edits total — all within service-rename commits, no separate commit).
- **Commit:** rolled into the per-service rename commits (`8760c3b` for DeckAnalysisPacketService, `1e4cf1d` for DeckComparisonService, `580b300` for MetaGapService).

### 3. [Rule 2 — required for grep-gate correctness] Pre-rewrite of cross-service narrative `<see cref>` doc-comment references

- **Found during:** Task 1 — narrative reference scan
- **Issue:** `DeckFlow.Web/Services/DeckComparisonService.cs:193`, `MetaGapService.cs:239`, `RequestContextParser.cs:8` each contained narrative `Mirrors <see cref="ChatGptDeckPacketService"/>` or `<see cref="ChatGptDeckPacketService.BuildRequestContextText(...)" />` references. The substring `ChatGptDeckPacketService` matched `ChatGpt[A-Z]`.
- **Fix:** Updated all narrative `<see cref>` references to the Wave-2-renamed identifier `DeckAnalysisPacketService` (since that rename landed in this same wave). This is NOT a presumed rename — `DeckAnalysisPacketService` was committed in `ef059b1`+`8760c3b` before these narrative references needed updating.
- **Files modified:** `DeckComparisonService.cs`, `MetaGapService.cs`, `RequestContextParser.cs` (rolled into the same per-file rename commits).

### 4. [Rule 2 — narrative comment cleanup] Inline-comment narrative ref `ChatGptDeck* services`

- **Found during:** Task 2, File 4 (JsonTextFormatterService.cs)
- **Issue:** Inline comment at `JsonTextFormatterService.cs:8-9` read `// Phase 10: shared <result>...</result> wrap directive used by all three / ChatGptDeck* services to ensure cross-AI parsing parity.` — the `ChatGptDeck*` wildcard-style identifier matched the grep gate.
- **Fix:** Rewrote to `// deck-analysis, deck-comparison, and meta-gap services to ensure cross-AI parsing parity.` — names the three pages explicitly per the locked Phase 12 / Phase 13 page-name layer.
- **Commit:** `af3102d`.

### Naming-map exact application

Naming map applied byte-stable to D-01 with the Open Question 1 resolution (`ChatGptResultWrapInstruction` → `ResultWrapInstruction`). No architectural changes. No untouched-target deviations.

## Self-Check: PASSED

- All 7 new service files exist on disk; all 7 old service files removed:
  - FOUND: DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - FOUND: DeckFlow.Web/Services/DeckComparisonService.cs
  - FOUND: DeckFlow.Web/Services/MetaGapService.cs
  - FOUND: DeckFlow.Web/Services/PacketArtifactStore.cs
  - FOUND: DeckFlow.Web/Services/RequestContextParser.cs
  - FOUND: DeckFlow.Web/Services/ResponseParsers.cs
  - FOUND: DeckFlow.Web/Services/JsonTextFormatterService.cs
  - REMOVED (verified `ls DeckFlow.Web/Services/ChatGpt*.cs` returns "No such file"): all 7 old paths
- All 10 commits present in `git log -10 --format='%h %s'`:
  - FOUND: ef059b1, 8760c3b, 1e4cf1d, 580b300, fce97c9, 1f643e4, ad51e06, af3102d, 89f1981, c409517
- Wave 2 grep gate `grep -rEn "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/Services/ DeckFlow.Web/Program.cs` returns ZERO hits.
- Preservation literals verified byte-identical (3 `"chatgpt"` in PacketArtifactStore L537/540/543; `partial` modifier on RequestContextParser; 10 `chatgpt-*` slug hits in Program.cs 301-redirect block).
- Const rename verified: `ResultWrapInstruction` declared in JsonTextFormatterService, 15 callers updated in lockstep, ZERO `ChatGptResultWrapInstruction` references remain.
- Plain-author across all 10 commits (`Chris Lunt <luntc1972@yahoo.com>`); zero `Co-Authored-By` trailers.

## Forward-Looking Note (Wave 3 prep)

The model + service layer is now renamed. As of this commit:
- `DeckFlow.Web/Controllers/DeckController.cs` still references the renamed services + view models by their OLD names → BREAKS BUILD until Wave 3 closes the controller surface.
- `DeckFlow.Web/Views/Deck/*.cshtml` `@model X` directives still reference OLD view model names → Wave 3.
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` may reference old `DeckPageTab` enum names → Wave 1 already renamed those, so this should be a no-op or trivial — verify in Wave 3.
- `DeckFlow.Web.Tests/*` test fixtures + test class names → Wave 4.

`dotnet build` was NOT run as a pass criterion in this plan per D-05. **Build-clean gate fires only at end of Wave 4 (plan 13-04).** This is the intended intermediate state.

Wave 3 (plan 13-03) will:
1. Rename `DeckController.cs` action methods (where they carry the `ChatGpt` prefix) and update all 142 internal type references to renamed Wave-1 model types + Wave-2 service interfaces (`IDeckAnalysisPacketService` etc.).
2. Update 3 Razor `@model` directives across `Views/Deck/DeckAnalysis.cshtml`, `Views/Deck/DeckComparison.cshtml`, `Views/Deck/CedhMetaGap.cshtml`.
3. Update `Views/Shared/_DeckToolTabs.cshtml` to use renamed `DeckPageTab.DeckAnalysis` / `DeckPageTab.DeckComparison` / `DeckPageTab.CedhMetaGap` enum branches (verify Wave 1 already covered this — if so, Wave 3 has no view-tab work).
4. After Wave 3 the build should compile clean EXCEPT for test fixture errors in `DeckFlow.Web.Tests/` which Wave 4 closes.
