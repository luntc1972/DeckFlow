# Phase 83: Packet-Service SRP Split - Research

**Researched:** 2026-07-04
**Domain:** C# service-layer refactor (ASP.NET Core / DeckFlow.Web) — extracting shared collaborators from four parallel prompt-packet builders without changing any generated artifact byte
**Confidence:** HIGH (all claims below are grounded in direct reads of the four target files, their DI registration, existing test seams, and the Phase-82 triage record; no external library research was needed — this is an in-repo structural refactor)

## Summary

The four packet services (`DeckAnalysisPacketService` 2372 LOC, `DeckComparisonService` 1033,
`MetaGapService` 956, `DeckPrimerPacketService` 904) already share one real collaborator layer:
each delegates its actual prompt **prose** to a per-family `*PromptVariantRegistry` +
`IXxxPromptVariant` (ChatGpt/Claude/Gemini) set under `DeckFlow.Web/Services/PromptBuilders/*`
(ADR-0001's decoupled-variant pattern), and two of the four already share a Core-level pure
aggregator (`DeckStatAggregator`). What is NOT shared is the layer *between* "raw deck entries"
and "the text blocks handed to the prompt variant": deck-loading/commander-resolution, Scryfall
batch-lookup-with-fallback, canonical decklist text formatting, and canonical cache-key text
building are each hand-rolled 3-4 times with small, easy-to-miss behavioral differences. This is
exactly the "prompt assembly" and "Scryfall reference resolution" mechanical layer the ROADMAP's
two mandated collaborators target — and the differences are the landmines: naively unifying any
of these five clusters can silently change bytes in one service while leaving the artifact
correct in the other three.

Critically, **`DeckPrimerPacketService` never calls Scryfall for card-level resolution at all**
(no `IScryfallCardResolver` reference in the file) — its only external calls are Commander
Spellbook, EDH Top 16, and the category-knowledge store. This means PKTSVC-02's "single
resolver consumed by all four" claim needs a research-informed correction for the plan: Primer
either (a) has no card-resolution duplication to remove (it has none today), or (b) the phase
could choose to route it through the shared resolver for a *future* need, but doing so is
**out of scope** for a byte-identical-gated phase since it would be new behavior, not
deduplication. The plan should state explicitly that Primer satisfies PKTSVC-02 by having zero
duplicate Scryfall resolution paths, not by consuming a resolver it never needed.

**Primary recommendation:** Build two NEW, narrowly-scoped collaborators in
`DeckFlow.Web/Services/` (not `DeckFlow.Core`, since both need RestSharp/`IScryfallCardResolver`/
`CommanderSpellbookResult` — Web-layer types) — a `ScryfallReferenceResolver` (wraps the existing
`IScryfallCardResolver` with the shared chunk→collect→fallback orchestration, parameterized by
which fallback strategy and which output shape each caller needs) and a `PacketTextAssembler` (or
similar) that owns the decklist/possible-includes text-block layout and the key:value
request-context line-writer — while leaving the four distinct per-service combo-reference
formatters, the three deck-loading/commander-inference variants, and Primer's differently-shaped
cache-key text alone, because those are genuine behavioral divergences, not copy-paste debt.
Sequence the work as: (1) build + unit-test both collaborators against characterization
(golden-string) tests capturing current byte-for-byte output, (2) migrate one service at a time
behind the same golden-string guard, starting with the two structurally closest services
(Comparison and MetaGap, which share the literal `ReflagCommanderEntry` method), (3) Analysis last
(largest, most flag interactions), (4) Primer's migration is resolver-free and lowest-risk.

## User Constraints

No `CONTEXT.md` exists yet for Phase 83 at research time
(`.planning/phases/83-packet-service-srp-split/` contained no `*-CONTEXT.md` file). The binding
constraints instead come directly from `.planning/REQUIREMENTS.md` (PKTSVC-01..04) and
`.planning/ROADMAP.md`'s Phase 83 success criteria, both reproduced under
`## Phase Requirements` below. If `/gsd:discuss-phase 83` is run before planning, its
`CONTEXT.md` supersedes any discretionary call this document makes.

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor, no framework migration this milestone.
- **HTTP resilience:** RestSharp + Polly v8 named `ResiliencePipeline<RestResponse>` pattern only
  — do not introduce `Microsoft.Extensions.Http.Resilience`'s standard handler. Both new
  collaborators must keep using the existing `IScryfallCardResolver` abstraction (which already
  wraps RestSharp+Polly+`ScryfallThrottle`) rather than bypassing it.
- **Testing:** VSTest unreliable in WSL — build via `dotnet build`/`dotnet.exe` and run xUnit
  targeted, not full VSTest discovery. No new test framework or mocking library without asking
  (project already uses hand-rolled `Fake*`/`Stub*` doubles, no Moq/NSubstitute present in these
  test files — keep that convention for any new collaborator tests).
- **Formatting:** changed-lines-only format gate (`.editorconfig`); do not reflow whole files.
  None of the four target files or the `PromptBuilders/*` tree contain C# raw-string literals
  (`grep '"""'` returned zero hits), so the raw-string-reindent carve-out does not concretely
  apply here — but the *equivalent* risk (accidentally reordering/re-wrapping a
  `StringBuilder.AppendLine` chain) is the single highest-risk category for this phase; see
  Common Pitfalls.
- **Commits:** plain default-author commits, no Co-Authored-By trailer, README updated when
  behavior changes (this phase changes no user-facing behavior, so a README update is likely N/A
  unless a public architecture doc references these services by name).
- **Dependency additions:** none anticipated — both new collaborators are pure C# composed from
  existing injected interfaces (`IScryfallCardResolver`, `ICommanderSpellbookService`, etc.).
- **Delegation rule (temporary override, expires 2026-06-18 — already lapsed as of 2026-07-04):**
  the override text refers to a window that ended two weeks before this research date; the
  standing default ("Codex codes, Claude reviews") applies unless the session's live CLAUDE.md
  copy still shows an active override at execution time — re-check the override's date fields at
  `/gsd:execute-phase` time, not from this research doc.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PKTSVC-01 | Shared prompt-assembly orchestration extracted into a reusable collaborator each of the four services delegates to, without collapsing per-variant prompt prose (ADR-0001 preserved). | See "Duplication Map" cluster D (decklist/possible-includes text blocks) and cluster E (request-context key:value lines) below — these are the actual mechanical-assembly duplication, distinct from the already-extracted `PromptBuilders/*` prose layer. Cluster F (combo-reference formatters) is flagged as NOT extractable without behavior change — see landmines. |
| PKTSVC-02 | Scryfall reference-resolution logic extracted into a single reusable resolver consumed by all four services; no duplicate resolution path remains. | See "Duplication Map" cluster A (Scryfall batch-chunk-collect-fallback). Primer has zero existing Scryfall card-resolution code — see Summary correction. Concrete interface boundary proposed in "Collaborator Boundaries" §2. |
| PKTSVC-03 | Each service reduced to an orchestration shell; collaborators unit-tested in isolation; no service materially larger than its collaborators. | See "Sequencing" for per-service migration order and "Architecture Patterns" for the existing `*Coordinator` extraction precedent (DeckFlow.Studio) this repo already uses for exactly this shape of split. |
| PKTSVC-04 | Automated byte-identical regression guard proves analysis/comparison/meta-gap/primer artifacts unchanged pre/post refactor, 3 AI variants x flags ON/OFF. | See "Byte-Identical Regression Harness" — proposes golden-string characterization tests built from the existing `CreateService(...)` test-seam pattern already used in `DeckAnalysisPacketServiceTests.cs` / `DeckComparisonServiceTests.cs` / `MetaGapServiceTests.cs`, extended to capture full `BuildAsync()` artifact text (not just the prompt-variant dispatch layer that `ResultContractTests.cs` already covers). |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Per-AI-platform prompt prose | API/Backend (`PromptBuilders/*` strategy classes) | — | Already correctly isolated per ADR-0001; untouched by this phase. |
| Prompt-assembly orchestration (decklist text, request-context lines, section ordering) | API/Backend (new collaborator in `DeckFlow.Web/Services/`) | — | Needs `DeckEntry`, `AiPlatform`, `CommanderSpellbookResult` — Web-layer types; not portable to `DeckFlow.Core` without a large DTO-mapping detour that isn't justified for this cycle. |
| Scryfall reference resolution (batch lookup + fallback) | API/Backend (new collaborator wrapping `IScryfallCardResolver`) | — | `IScryfallCardResolver` already centralizes the HTTP/Polly/throttle mechanics (Web/Services/Scryfall/ScryfallCardResolver.cs); the NEW collaborator sits one layer up, owning the batch-chunk + per-request-shape + fallback-strategy orchestration that today is copy-pasted into each packet service. |
| Deck loading / commander inference | API/Backend (existing `IDeckEntryLoader` + per-service inline logic) | — | Deliberately NOT unified in this phase — see landmine "commander-inference divergence" below; each service's inference heuristic has small, intentional differences load-bearing for its own artifact shape. |
| Persistence of built artifacts to session zip | API/Backend (`PacketArtifactStore`, static class) | — | Explicitly deferred by Phase 82's triage (row 6) to "Phase 83's own scope check" — recommend this phase makes an explicit deferral decision (see Open Questions) rather than silently expanding scope. |

## Standard Stack

No new libraries. This phase is a pure in-repo refactor using already-registered dependencies:
`RestSharp` 114.0.0, `Polly` 8.x (via `ResiliencePipelineProvider<string>`), `xUnit` 2.9.3 /
`xunit.runner.visualstudio` 3.1.4 for the new collaborator tests, all `[VERIFIED: DeckFlow.Web.csproj / Directory.Packages / repo grep]`.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Two Web-layer collaborators (`ScryfallReferenceResolver`, `PacketTextAssembler`) | One "god collaborator" combining resolution + assembly | Rejected — conflates two orthogonal concerns the ROADMAP explicitly separates into two success criteria (SC1 vs SC2); would recreate a smaller god-object. |
| Extracting into `DeckFlow.Core` | Keep in `DeckFlow.Web/Services/` | `DeckFlow.Core` currently has zero `RestSharp`/`ASP.NET` dependencies outside the `Integration/*` importers; forcing the new collaborators into Core would require mapping `CommanderSpellbookResult`/`ScryfallCard`/`AiPlatform` into Core-only DTOs — a bigger, riskier change than this cycle's stated scope. |

## Package Legitimacy Audit

Not applicable — no new packages are installed by this phase (pure in-repo class extraction).

## Duplication Map

Evidence is cited `file:line`. Each cluster is labeled MECHANICAL (safe extraction candidate,
same behavior wanted) or DIVERGENT (looks similar, is NOT byte-identical across services today —
extracting a single shared implementation would change output unless carefully parameterized).

### Cluster A — Scryfall batch-chunk-collect-fallback (MECHANICAL core, DIVERGENT at the edges)

Three independent implementations of "chunk names into batches of 75, POST `cards/collection`,
validate 2xx+non-null `Data`, map hits into an oracle-name map, then fall back per-unresolved-name":

- `DeckAnalysisPacketService.LookupCardReferencesAsync`
  (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1950-2043`) — batches via a private
  `Chunk<T>` (2163-2176), calls `ScryfallCardResolver.NormalizeForScryfall` on names before
  submission (1965), on miss falls back to **`SearchPrintingFallbackCardAsync`** (2005) — the
  richer fallback (`printed:` OR `name:` search across all printings, then `cards/named?fuzzy=`)
  — and builds a 9-field `CardReference` record (2367) including `IsMdfcLand`, `ReleasedAt`,
  `Quantity`, `IsCommander`, plus a separate `mechanicNames` `HashSet` extracted via
  `ExtractMechanicNames` (2299-2326).
- `DeckComparisonService.LookupCardDetailsAsync`
  (`DeckFlow.Web/Services/DeckComparisonService.cs:385-445`) — batches via its OWN private
  `Chunk<T>` (943-956, byte-identical implementation to Analysis's), does NOT call
  `NormalizeForScryfall` before submission, on miss falls back to **`SearchFallbackCardAsync`**
  (430) — the simpler exact-name search — and returns a plain `IReadOnlyList<ScryfallCard>` plus
  `oracleNameMap`, no mechanic extraction.
- `MetaGapService.ResolveOracleNameMapAsync`
  (`DeckFlow.Web/Services/MetaGapService.cs:562-621`) — batches via a THIRD, differently-shaped
  `Chunk(IReadOnlyList<string>, int)` (759-765, uses `Skip`/`Take` instead of the manual loop the
  other two use — same output, different implementation), also falls back to
  **`SearchFallbackCardAsync`** (610, same choice as Comparison), and returns ONLY the
  `oracleNameMap` — no `ScryfallCard` list retained at all.
- `DeckPrimerPacketService` — **no Scryfall card-resolution code of any kind.** Confirmed by full
  read of `DeckFlow.Web/Services/DeckPrimerPacketService.cs`; it never references
  `IScryfallCardResolver`. Its external calls are `ICommanderSpellbookService`,
  `IEdhTop16Client`, `ICategoryKnowledgeStore` only.

**Landmine:** the fallback-method choice (`SearchPrintingFallbackCardAsync` for Analysis vs.
`SearchFallbackCardAsync` for Comparison/MetaGap) is load-bearing, not accidental drift —
`SearchPrintingFallbackCardAsync` searches across ALL printings (`unique=prints`,
multilingual-inclusive) and is the only path that can recover `ReleasedAt`/`IsMdfcLand` data
Analysis needs; `SearchFallbackCardAsync` is a plain `unique=cards` exact-name search sufficient
for Comparison/MetaGap's oracle-name-only need. **Do not unify these into one hardcoded fallback
call** — the shared collaborator must accept the fallback strategy (or the two existing
`IScryfallCardResolver` methods directly) as a parameter/delegate, or a switched-for-a-different-
card result could silently change which printing a comparison/meta-gap prompt cites.

### Cluster B — `ReflagCommanderEntry` helper (MECHANICAL, true duplicate)

- `DeckComparisonService.ReflagCommanderEntry` (`DeckFlow.Web/Services/DeckComparisonService.cs:363-383`)
- `MetaGapService.ReflagCommanderEntry` (`DeckFlow.Web/Services/MetaGapService.cs:465-485`)

These two are **byte-identical** private methods (same signature, same body: walk the list,
reflag the FIRST `Quantity==1` name-match to `Board="commander"`, leave everything else alone).
Safe, low-risk extraction to a shared static helper.

`DeckAnalysisPacketService` has a DIFFERENT, NOT-equivalent inline reflag at
`ResolvePreScryfallCommanderState` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:229-286`,
mutation at 270-279) and again at `BuildAsync` (610-616) — it reflags **every** entry whose name
is in a `HashSet` of inferred commander names (supports partner-pair commanders), not just the
first match. **Do not fold Analysis onto the Comparison/MetaGap `ReflagCommanderEntry`
semantics** — it would break partner-commander decks.

### Cluster C — `BuildCanonicalDeckSourceText` (cache-key text; MECHANICAL among 3, DIVERGENT for Primer)

- Analysis (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:322-338`), Comparison
  (`DeckFlow.Web/Services/DeckComparisonService.cs:125-143`, plus a `commander|Name\n` prefix
  line Analysis/MetaGap don't have), MetaGap
  (`DeckFlow.Web/Services/MetaGapService.cs:119-136`) all sort
  `Board,Name,SetCode,CollectorNumber` and emit `Board|Qty|Name|SetCode|CollectorNumber\n` per
  entry — structurally identical modulo Comparison's extra commander-prefix line.
- Primer's version (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:801-818`) sorts
  `Board,Name,Quantity` (no SetCode/CollectorNumber) and emits `Board|Qty|Name\n` — a
  **deliberately different, smaller shape** — the method's own doc comment states *"Order/format
  MUST NOT change"* because `EvaluateStaleness` (820-852) depends on hash parity with this exact
  text for the fresh/stale UI banner.

This text feeds `PacketSessionCache.ComputeKey` (`DeckFlow.Web/Services/PacketSessionCache.cs:45-`)
— an in-memory, 5-minute-TTL cache (`EntryTtl`, line ~29) with no persistence, so a format change
here is a correctness-neutral cache-key churn, NOT a byte-identical-artifact risk. Still: (a) keep
Primer's shape completely separate from any shared helper (it is a distinct, narrower canonical
form used for a distinct purpose — deck-only multiset hash for staleness, not the full
cache-input bag), and (b) if Analysis/Comparison/MetaGap's three near-identical copies are
unified, the shared helper needs an optional-prefix-line parameter for Comparison's leading
`commander|` line.

### Cluster D — Decklist / Possible-Includes text-block layout (MECHANICAL — the real PKTSVC-01 target)

All four services independently build a "Commander (if any) / Mainboard / Possible Includes"
plain-text block from `IReadOnlyList<DeckEntry>`:

- Analysis: `BuildDecklistText` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1087-1139`) +
  `FormatDecklistLine` (1053-1080) — supports `includeVersions` (set/collector-number suffix) and
  an `oracleNameMap` "[printed as: X]" annotation.
- Comparison: `BuildDecklistText` (`DeckFlow.Web/Services/DeckComparisonService.cs:864-906`) +
  `FormatDecklistLine` (908-916) — supports the SAME `oracleNameMap` "[printed as: X]" annotation,
  no version suffix.
- MetaGap: `BuildCanonicalDecklistText` (`DeckFlow.Web/Services/MetaGapService.cs:296-336`, no
  oracleNameMap, used for the zip round-trip artifact only) AND a completely separate
  `BuildCompactDecklist`/`BuildCompactRefDecklist` pair (674-720, grouped/normalized by
  `CardNormalizer`, no section headers — this is the prompt-facing decklist, structurally
  different from the canonical/zip one).
- Primer: `BuildDecklistText` (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:682-719`) — same
  three-section shape as Analysis/Comparison, no oracleNameMap (Primer never resolves oracle
  names).

The Commander/Mainboard/Possible-Includes **section-ordering and header-text mechanics** are
genuinely identical across Analysis, Comparison, and Primer (modulo the two optional features:
`includeVersions` and `oracleNameMap` annotation). This is the cleanest, lowest-risk PKTSVC-01
target: a single parameterized `BuildSectionedDecklistText(entries, possibleIncludes,
includeVersions, oracleNameMap)` helper can reproduce all three exactly by construction (same
section labels: `"Commander"`, `"Mainboard"`, `"Possible Includes"`, same blank-line placement).
MetaGap's two decklist builders are intentionally different shapes for different purposes and
should NOT be forced into this helper.

### Cluster E — Request-context key:value text (MECHANICAL pattern, DIVERGENT field sets)

- `DeckAnalysisPacketService.BuildRequestContextText` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:2232-2264`)
- `DeckComparisonService.BuildRequestContextText` (`DeckFlow.Web/Services/DeckComparisonService.cs:289-300`)
- `MetaGapService.BuildRequestContextText` (`DeckFlow.Web/Services/MetaGapService.cs:342-365`)
- `DeckPrimerPacketService.BuildRequestContextText` (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:732-751`)

All four use the identical micro-pattern `builder.AppendLine($"{key}: {NormalizeSingleLine(value, fallback)}")`
repeated per field, with each service's own field SET and fallback values. The per-field lines
are mechanically identical in shape; the field lists are genuinely different (Comparison has no
`selected_analysis_questions`, MetaGap has `selected_reference_indexes`, etc.). Extract a tiny
`AppendKeyValueLine(StringBuilder, string key, string? value, string fallback)` helper (and reuse
each service's own `NormalizeSingleLine`, which is itself duplicated 3x with IDENTICAL bodies at
`DeckAnalysisPacketService.cs:2178-2179`, `DeckPrimerPacketService.cs:551-552`, and inline as
`JsonTextFormatterService.NormalizeSingleLine` calls in Comparison/MetaGap — confirm
`JsonTextFormatterService.NormalizeSingleLine` already IS the shared version Comparison/MetaGap
use; Analysis/Primer should be migrated onto that existing shared helper rather than a new one).
Do NOT attempt to unify the four field lists themselves — that is a per-service concern, not
duplication.

### Cluster F — Combo-reference text formatters (DIVERGENT — do NOT unify)

Four different formats over the same `CommanderSpellbookResult`/`SpellbookCombo` data:

- `DeckAnalysisPacketService.BuildComboReferenceText` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1145-1189`) — "Commander Spellbook combo reference (verified data...)" header, numbered `COMPLETE COMBOS IN THIS DECK (n):` / `COMBOS ONE CARD AWAY...` sections.
- `MetaGapService.BuildComboReferenceText(string label, ...)` (`DeckFlow.Web/Services/MetaGapService.cs:768-810`) — "Commander Spellbook combos for {label}:" header, "Complete combos: n" / "Near-combos: n" counts, different numbering style.
- `DeckPrimerPacketService.BuildComboReferenceText(..., string spikeVerdict)` (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:481-549`) — Markdown `## Known Combos` / `## Speculative Synergies` / `## Near-Combos` sections, with a popularity/mana-value ranking sort Analysis/MetaGap don't have.
- `DeckComparisonService.BuildComboArtifactText` (`DeckFlow.Web/Services/DeckComparisonService.cs:996-1032`) — per-deck summary combo text, a fourth distinct shape (`{DeckName} combos` header, `Complete combos: n` / `Near-combos: n` counts, `Key combos:` / `Near-combos:` bulleted lists).

These read as "the same kind of thing" but are four genuinely different renderings tuned to each
service's surrounding prompt structure. **Recommendation: leave these four as-is.** Attempting a
single parameterized formatter risks either (a) losing a format-specific nuance (Primer's
popularity ranking, MetaGap's per-label header) or (b) building a formatter so parameterized it
no longer reduces duplication. If the planner still wants to touch this cluster, it must be
covered by the same golden-string byte-identical guard as everything else and treated as its own
low-priority, separately-reviewable slice — not bundled into the PKTSVC-01/02 core work.

### Cluster G — `NormalizeOracleText` / `CollapseWhitespace` (DIVERGENT — do NOT unify without care)

- Analysis's `NormalizeOracleText` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:2106-2154`)
  includes face name, mana cost, type line, oracle text, AND power/toughness per face (joined
  `" | "`), plus a top-level power/toughness suffix — needed because Analysis's reference block
  shows full card text directly in the prompt.
- Comparison's `NormalizeOracleText` (`DeckFlow.Web/Services/DeckComparisonService.cs:919-936`) —
  oracle text only (card + face), no name/mana-cost/power-toughness — sufficient because
  Comparison only feeds this into `DeckStatAggregator` stat inputs, never displays it directly.
- `CollapseWhitespace`: Analysis (2156-2161) and Comparison (938-941) are byte-identical
  (`\r\n`→`\n`, split on `\n`, `TrimEntries`, rejoin with `" "` — collapses newlines only, leaves
  internal multi-space runs untouched). **Primer's `CollapseWhitespace`
  (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:554-576`) is a DIFFERENT, char-by-char
  implementation that collapses ANY whitespace run (spaces, tabs, newlines) to a single space** —
  a real behavioral difference for input containing double-spaces or tabs. Do not consolidate
  Primer's `CollapseWhitespace` with Analysis/Comparison's without a dedicated
  before/after-diff test — the difference is observable for real card names/oracle text that
  contain irregular whitespace.

## Architecture Patterns

### Existing extraction precedent: `*Coordinator` classes (DeckFlow.Studio)

This exact "god-component → orchestration shell + tested collaborators" shape was already
executed twice in this repo (both merged to `main`, per project memory
`project_studio_h1_extract_branch` / `project_studio_branches_merged_verified`):

- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (480 LOC) + its test file
  `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` (577 LOC) — extracted from the
  `DirectPush.razor.cs` code-behind. Doc comment at the top of the file states the pattern
  explicitly: *"This type performs no rendering and holds no per-page UI state — the page keeps
  all busy guards, error-copy mapping, logging, cancellation, and `StateHasChanged`. Behavior is
  identical to the prior inline implementation."*
- Sibling coordinators: `PublishCoordinator.cs`, `PullFromProdCoordinator.cs`,
  `ReviewCoordinator.cs`, `HarvestQueueCoordinator.cs`, `CreatorManagementCoordinator.cs`,
  `AutoApproveSettingsCoordinator.cs`, `SpendCapCoordinator.cs` — all in
  `DeckFlow.Studio/ViewModels/`, each independently unit-tested (no bUnit required) in
  `DeckFlow.Studio.Tests/ViewModels/`.

**Applied to Phase 83:** the same shape applies, translated to services instead of Razor
code-behinds — the packet SERVICE keeps request validation, orchestration ordering, cache
read/write, timing, and flag-latching; the NEW collaborators (`ScryfallReferenceResolver`,
`PacketTextAssembler`) own the pure card-resolution and text-assembly mechanics and get their own
`DeckFlow.Web.Tests` files, mirroring the Studio Coordinator pattern's test-file naming
(`{Collaborator}Tests.cs`).

### Existing shared Core collaborator precedent: `DeckStatAggregator`

`DeckFlow.Core/Analysis/DeckStatAggregator.cs` (252 LOC) is ALREADY consumed by both
`DeckAnalysisPacketService` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:818,1360`) and
`DeckComparisonService` (`DeckFlow.Web/Services/DeckComparisonService.cs:509`) for lands/creatures/
mana-curve/role-count tallying — proof that a shared, independently-tested, pure aggregator
consumed by 2+ of these four services already exists and works in production. This is the
template for how far "shared" can safely go: `DeckStatAggregator` takes a plain
`IEnumerable<DeckStatCardInput>` (a small DTO), has no dependency on `RestSharp`/`AiPlatform`, and
lives in `DeckFlow.Core` because its inputs are already Core-shaped. The two NEW PKTSVC
collaborators cannot follow it into Core (they need Web-layer types), but they should follow its
DI/test shape: constructor-injected only what's needed, pure/static where possible, independently
unit-tested against fixture inputs (no live HTTP).

### DI registration pattern: `PacketServiceCollectionExtensions`

All four services are registered via one already-extracted extension method
(`DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:41-95`,
`AddDeckFlowPacketServices()`), called once from `Program.cs:164`. Confirmed lifetimes:

| Component | Lifetime | Evidence |
|-----------|----------|----------|
| `PacketSessionCache` | Singleton | `PacketServiceCollectionExtensions.cs:45` |
| `MoxfieldParser` / `ArchidektParser` | Transient | `PacketServiceCollectionExtensions.cs:46-47` |
| `IDeckAnalysisPacketService`, `IDeckComparisonService`, `IMetaGapService`, `IDeckPrimerPacketService` | Scoped | `PacketServiceCollectionExtensions.cs:49,63,72,80` |
| `*PromptVariantRegistry` (all 7 families) + `IXxxPromptVariant` implementations | Singleton | `PromptVariantServiceCollectionExtensions.cs:32-60` |
| `IScryfallCardResolver` | Registered in `ScryfallServiceCollectionExtensions.cs` (not read in full this session — verify lifetime before wiring the new resolver collaborator; likely Scoped or Singleton alongside the other Scryfall-family services registered by the sibling `AddDeckFlowScryfallServices()` called at `Program.cs:89`) | `[ASSUMED]` — confirm exact lifetime in that file before choosing the new collaborator's lifetime; a resolver collaborator wrapping a Scoped dependency must itself be Scoped, not Singleton, to avoid captive-dependency lifetime mismatches. |

**Recommended registration for the two new collaborators:** add them to
`PacketServiceCollectionExtensions.AddDeckFlowPacketServices()` (the natural existing home) as
Scoped (matching the four packet services they serve and, pending the lifetime check above,
matching `IScryfallCardResolver`), constructor-injecting `IScryfallCardResolver` for the resolver
collaborator and nothing external for the pure-text assembler (candidate for a `static` class or
a trivially Singleton-safe stateless class, mirroring `PacketArtifactStore`'s `internal static`
shape at `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs:13`).

### Collaborator Boundaries (concrete proposal)

**1. `PacketTextAssembler` (or similarly named; static or stateless singleton)**

```csharp
// Illustrative signature — final naming/shape is the planner's call.
internal static class PacketTextAssembler
{
    // Cluster D: reproduces Analysis/Comparison/Primer's Commander/Mainboard/Possible-Includes
    // layout exactly. includeVersions and oracleNameMap default to the "off" values every
    // existing non-Analysis caller already passes, so callers that don't need them see zero
    // behavior change.
    internal static string BuildSectionedDecklistText(
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<DeckEntry> possibleIncludeEntries,
        bool includeVersions = false,
        IReadOnlyDictionary<string, string>? oracleNameMap = null);

    // Cluster E: the key:value line-writer. Each service keeps its own field list/order —
    // this only removes the per-line NormalizeSingleLine+AppendLine boilerplate.
    internal static void AppendKeyValueLine(
        StringBuilder builder, string key, string? value, string fallback);
}
```

Do NOT add combo-reference formatting (Cluster F) or per-service canonical-cache-key text
(Cluster C) to this collaborator — both are DIVERGENT clusters per the Duplication Map.

**2. `ScryfallReferenceResolver` (Scoped, wraps `IScryfallCardResolver`)**

```csharp
// Illustrative signature.
internal sealed class ScryfallReferenceResolver
{
    public ScryfallReferenceResolver(IScryfallCardResolver cardResolver);

    // Cluster A's shared batch-chunk-collect loop. `fallbackStrategy` lets Analysis keep using
    // SearchPrintingFallbackCardAsync while Comparison/MetaGap keep SearchFallbackCardAsync —
    // preserving each service's current, intentionally-different miss-handling.
    public Task<ScryfallBatchResolution> ResolveBatchAsync(
        IReadOnlyList<string> cardNames,
        Func<string, CancellationToken, Task<ScryfallCard?>> fallbackStrategy,
        CancellationToken cancellationToken);
}

// Bundle rich enough for all three current consumption shapes: Analysis maps this into its own
// 9-field CardReference + mechanic extraction; Comparison keeps the ScryfallCard list; MetaGap
// uses only OracleNameMap.
internal sealed record ScryfallBatchResolution(
    IReadOnlyList<ScryfallCard> ResolvedCards,
    IReadOnlyDictionary<string, string> OracleNameMap);
```

Each of the three consuming services keeps its own post-processing (Analysis's
`CardReference`+mechanic-name extraction, Comparison's stat-input mapping, MetaGap's
oracle-name-only usage) — the collaborator's job ends at "resolved cards + oracle name map",
matching where the three implementations actually diverge today. `NormalizeForScryfall`
pre-submission normalization (Analysis-only today, Cluster A) should become an explicit
opt-in parameter (default off) so Comparison/MetaGap's behavior doesn't change.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Batching a list into fixed-size chunks | A 4th private `Chunk<T>`/`Chunk(IReadOnlyList<string>,int)` | One shared `static IEnumerable<List<T>> Chunk<T>(...)` in the new collaborator or a small shared utility class | Three near-identical private copies exist today (`DeckAnalysisPacketService.cs:2163-2176`, `DeckComparisonService.cs:943-956`, `MetaGapService.cs:759-765`) — pure, zero-risk consolidation. |
| Deck-stat tallying (lands/creatures/curve/roles) | A new per-service tally loop | `DeckFlow.Core.Analysis.DeckStatAggregator.Compute(...)` (already shared by Analysis + Comparison) | Already extracted and tested; MetaGap/Primer do not currently need it (they don't render curve/role stats), so no action required there — do not force it in. |
| Single-line normalization for prompt-safe text | A 3rd/4th private `NormalizeSingleLine` | `JsonTextFormatterService.NormalizeSingleLine` (already used by Comparison and MetaGap) | Analysis (`DeckAnalysisPacketService.cs:2178-2179`) and Primer (`DeckPrimerPacketService.cs:551-552`) each have their own private copy that appears behaviorally equivalent to the shared one — verify equivalence with a characterization test, then migrate both onto the existing shared helper instead of writing a third. |

**Key insight:** the actual "god-file" problem here is not that these services lack ANY shared
collaborators — `DeckStatAggregator`, `JsonTextFormatterService`, `IScryfallCardResolver`, and the
seven `PromptBuilders/*` registries are all real, working shared collaborators already in
production. The problem is a second, THINNER layer of copy-pasted orchestration glue (chunking,
fallback loops, section-text assembly, key:value line writers) sitting directly inside each
service instead of being pulled out one level further, alongside genuinely divergent logic
(commander inference, combo-text formatting) that LOOKS like duplication but isn't.

## Byte-Identical Regression Harness

### Existing precedent to build on

1. **`DeckFlow.Web.Tests/ResultContractTests.cs`** already exercises all four/seven
   `*PromptVariantRegistry` families across all 3 `AiPlatforms` (`["ChatGPT","Claude","Gemini"]`,
   line 25) by constructing the registry directly with concrete variants — no DI container, no
   live HTTP. This proves the prose layer already has a byte-checkable seam.
2. **`DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs`** (and the sibling
   `InteractionAuditPromptParityTests.cs`, `WinConMapPromptParityTests.cs`) already implement the
   EXACT byte-identical pattern PKTSVC-04 needs, at a smaller scope: `Score_NullPath_
   ByteIdenticalToExcisedScorePath` (`AnalysisScorePromptParityTests.cs:80-92`) builds the SAME
   request with a flag-block present vs. a sentinel-marked absent value, and asserts the null-path
   output equals the with-value output MINUS the exact inserted bytes
   (`Environment.NewLine + sentinel + Environment.NewLine`). This is the template to generalize
   across the whole refactor, not just the flag-block insertion point.
3. **`DeckAnalysisPacketServiceTests.CreateService(...)`**
   (`DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:1846-1890`) is the existing test seam:
   constructs the REAL `ScryfallCardResolver` with `executeCollectionAsyncOverride`/
   `executeSearchAsyncOverride`/`executeNamedAsyncOverride` delegates (deterministic fixture
   responses, no live HTTP), the REAL `AnalysisPromptVariantRegistry` with all 3 concrete
   variants, and a fresh `PacketSessionCache`. `DeckComparisonServiceTests.cs` and
   `MetaGapServiceTests.cs` follow the analogous pattern for their own services (confirmed by
   file presence/size: 492 LOC and 888 LOC respectively). `DeckPrimerPacketServiceTests.cs` uses
   the SECOND internal constructor (`DeckFlow.Web/Services/DeckPrimerPacketService.cs:188-217`)
   with override delegates (`loadDeckEntriesAsyncOverride`, `findCombosAsyncOverride`, etc.) —
   no live Scryfall dependency needed since Primer never calls it.

### Recommended harness design

Build ONE new test file per service (or one shared `PacketByteIdentityFixtures.cs` + four
`*ByteIdentityTests.cs` files) that:

1. **Fixture decks:** reuse 2-3 existing fixture decks already used across the current test
   suites (e.g. the Kraum/Background companion fixture already in
   `DeckAnalysisPacketServiceTests.CreateCompanionFixtureEntries`, line 1911) so the harness
   doesn't need new fixture data — it needs deterministic, already-proven-stable inputs.
2. **Capture BEFORE refactor:** for each service, call the existing `CreateService(...)` +
   `BuildAsync(request)` for every `(platform in [ChatGPT, Claude, Gemini]) x (each
   PromptMutatingAnalysisFlags entry: ON, all OFF)` combination that applies to that service
   (Analysis has 4 mutating flags per `PromptMutatingAnalysisFlags`,
   `DeckAnalysisPacketService.cs:158-164`; Comparison/MetaGap/Primer currently have none — confirm
   this against each service before assuming "flags OFF only" is sufficient for them). Capture the
   exact artifact string(s) — `AnalysisPromptText`/`SetUpgradePromptText` for Analysis,
   `ComparisonPromptText`/`FollowUpPromptText` for Comparison, `PromptText` for MetaGap,
   `PromptTextsByPlatform` (already keyed per-platform, so Primer's single `BuildAsync` call
   yields all 3 platforms at once) for Primer.
3. **Persist as golden literals, not files:** given `.editorconfig`'s changed-lines-only format
   gate and the "never re-indent a literal that changes shipped bytes" carve-out philosophy,
   prefer storing the captured golden text as C# verbatim string constants (or as checked-in
   `.txt` fixture files loaded via `File.ReadAllText`, mirroring how existing prompt-template
   fixtures are handled elsewhere in the repo — check `prompt-templates/deck-comparison/` for
   precedent before deciding) rather than inline `Assert.Equal(giant string, ...)` — the exact
   mechanism is a planner/executor choice, but MUST use `StringComparison.Ordinal` throughout
   (matching the existing parity tests' `Assert.Contains(..., StringComparison.Ordinal)` /
   `Assert.Equal` conventions) since ordinal-exact-byte equality is the actual requirement, not
   culture-aware string equality.
4. **Run the SAME capture AFTER each migration step** (see Sequencing) and assert exact equality.
   A single collaborator migration that breaks even one platform x flag-state combination for one
   service fails immediately, before moving to the next service.
5. **`dotnet build` + targeted xUnit run**, not full VSTest discovery, per the WSL constraint —
   e.g. `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` via
   `dotnet.exe` from WSL against the Windows SDK, consistent with existing CI/local practice.

### Cycle 12-14 analysis flags (the "flag ON/OFF" axis for PKTSVC-04)

Confirmed via `DeckAnalysisPacketService.PromptMutatingAnalysisFlags`
(`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:158-164`) — the authoritative registry of
every flag that can mutate a cached artifact:

| Flag key | Constant | Introduced |
|----------|----------|------------|
| `analysis.command-zone-awareness` | `CommandZoneAwarenessFlag` (line 122) | Phase 73 |
| `analysis.multi-axis-score` | `MultiAxisScoreFlag` (line 130) | Phase 77 |
| `analysis.interaction-audit` | `InteractionAuditFlag` (line 138) | Phase 79 |
| `analysis.wincon-map` | `WinConMapFlag` (line 147) | Phase 80 |

All four are ANALYSIS-ONLY (they live on `DeckAnalysisPacketService`, gate blocks folded into
`AnalysisPromptText` only). Comparison, MetaGap, and Primer have no equivalent
prompt-mutating-flag registry today — confirm this is still true at execution time (grep each
service for `IFeatureFlagCache`/flag-key constants before assuming "flags OFF only" is a complete
test matrix for the other three services). `analysis.reference.full-oracle-text` and
`analysis.reference.deck-stats` (lines 100, 114) also mutate `AnalysisPromptText`'s reference block
but are notably ABSENT from `PromptMutatingAnalysisFlags` — this appears intentional (the recency
gate and deck-stats block are additive/legacy-preserving rather than novel content the cache-bypass
gate needs to guard against replay), but the byte-identity harness should still exercise both ON
and OFF for these two as well, since PKTSVC-04's stated scope is "flag ON and OFF" broadly, not
narrowly scoped to the cache-bypass registry.

## Common Pitfalls

### Pitfall 1: Treating "looks like the same code" as "is the same behavior"

**What goes wrong:** Extracting `NormalizeOracleText`, `CollapseWhitespace`, the combo-reference
formatters, or the commander-inference/reflag logic into ONE shared implementation, silently
adopting whichever service's version "won," and shipping a refactor that changes bytes for the
other 1-3 services.
**Why it happens:** these methods have near-identical names and superficially similar bodies
(Clusters F and G above), so a fast read-and-merge pass mistakes "structurally similar" for
"provably identical."
**How to avoid:** for every extraction candidate, diff the CURRENT bodies of all N call sites
line-by-line (not just skim) before writing the shared version, and add the extraction to Cluster
A-E (safe) vs F-G (do-not-merge) buckets from this research rather than re-deriving the list from
scratch.
**Warning signs:** a shared method needs an "if this caller then do X" branch inside it more than
once — that is a sign the callers were never truly duplicate and the extraction should either take
the differing behavior as an explicit parameter/delegate (Cluster A's fallback-strategy pattern)
or should not happen at all (Cluster F/G).

### Pitfall 2: `StringBuilder.AppendLine` reordering during "readability" cleanup

**What goes wrong:** `AppendLine()` calls with no arguments (blank-line separators) are easy to
accidentally drop, duplicate, or reorder relative to their neighbors when a method is split into
smaller pieces — each blank `AppendLine()` is invisible in a diff review unless the reviewer
specifically checks it. Since these files contain no raw-string literals, THIS is the actual
"never re-indent a literal that changes shipped bytes" risk vector for this phase (the
`.editorconfig` carve-out's underlying concern — literal-byte preservation — applies here even
though the specific carve-out rule about triple-quote raw strings does not).
**Why it happens:** section boundaries (e.g. the blank line between "Mainboard" and "Possible
Includes" in `BuildDecklistText`) carry no semantic marker in the code — they're just an
`AppendLine()` call that's easy to lose when extracting a method.
**How to avoid:** the byte-identical golden-string harness (above) is the primary defense — run
it after EVERY collaborator extraction, not just at the end of the phase.
**Warning signs:** a diff of the golden-string test output shows a difference of exactly one
`Environment.NewLine` — this is the signature of a dropped/added blank-line call, not a logic bug.

### Pitfall 3: Assuming `DeckPrimerPacketService` needs the new Scryfall resolver

**What goes wrong:** PKTSVC-02's wording ("single reusable resolver consumed by all four packet
services") could be read as a literal requirement that Primer's `BuildAsync` must call the new
resolver even though it has no card-resolution need today. Wiring Scryfall calls into Primer to
satisfy the letter of the requirement would be a **net-new feature** (Primer currently ships
without per-card oracle text) and violates the milestone's "no net-new user-facing feature" gate
(`.planning/REQUIREMENTS.md:3,68`).
**Why it happens:** literal requirement-text pattern-matching without checking what the fourth
service actually does today.
**How to avoid:** the plan/discuss-phase should explicitly record that PKTSVC-02 is satisfied for
Primer by verifying it has ZERO duplicate Scryfall-resolution code (a true statement today), not
by adding a new call.
**Warning signs:** any task in the plan that adds an `IScryfallCardResolver`/`ScryfallReferenceResolver`
constructor parameter to `DeckPrimerPacketService` should be treated as suspect and re-justified
against the "no net-new feature" gate before proceeding.

### Pitfall 4: `PacketArtifactStore` scope creep

**What goes wrong:** `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` (949 LOC) has its
own, separately-noted duplication (4 parallel `Suggest*ZipFileName`/`Load*FromZip` families per
artifact type) that Phase 82's triage (row 6,
`.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-TRIAGE.md:36`) explicitly
deferred to "Phase 83's own scope check" specifically to avoid two overlapping refactors touching
the same file in one cycle.
**Why it happens:** it's adjacent (same packet-artifact domain) and easy to fold in "while we're
in there."
**How to avoid:** this phase's success criteria (ROADMAP.md Phase 83, SC1-SC4) do not mention
`PacketArtifactStore` at all — it is about prompt-assembly and Scryfall-resolution, not zip
serialization. Recommend an explicit Open Questions decision (see below) rather than silent scope
expansion.
**Warning signs:** any plan wave that touches `PacketArtifactStore.cs` without an explicit,
separately-justified reason.

## Runtime State Inventory

Not applicable — this is a pure C#-source structural refactor with no rename, no data migration,
no external service reconfiguration, and no persisted-state schema change. `PacketSessionCache`
is in-memory-only with a 5-minute TTL (`DeckFlow.Web/Services/PacketSessionCache.cs:29`) — any
cache-key text-format change from Cluster C consolidation only affects in-flight requests within
that window, not any durable store.

## Code Examples

### Existing byte-identical excision pattern to generalize (Pitfall 2's primary defense)

```csharp
// Source: DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs:76-92
[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_NullPath_ByteIdenticalToExcisedScorePath(string platformName)
{
    const string sentinel = "SCOREBLOCK_PARITY_SENTINEL";

    var withScore = Build(platformName, sentinel);
    var nullPath = Build(platformName, scoreBlockText: null);

    var insertedBlock = Environment.NewLine + sentinel + Environment.NewLine;
    // ... asserts nullPath == withScore with insertedBlock removed, Ordinal comparison.
}
```

### Existing DI-free registry construction pattern (reuse for the byte-identity harness)

```csharp
// Source: DeckFlow.Web.Tests/ResultContractTests.cs:29-35
private static AnalysisPromptVariantRegistry BuildAnalysisRegistry() =>
    new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
    {
        new ChatGptAnalysisPromptVariant(),
        new ClaudeAnalysisPromptVariant(),
        new GeminiAnalysisPromptVariant(),
    });
```

### Existing full-service test seam (extend for BuildAsync-level golden capture)

```csharp
// Source: DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs:1846-1890 (abridged)
private static DeckAnalysisPacketService CreateService(/* fakes/overrides */)
{
    return new DeckAnalysisPacketService(
        new ScryfallCardResolver(
            new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
            new FakeResiliencePipelineProvider(),
            executeCollectionAsyncOverride: /* deterministic fixture */,
            executeSearchAsyncOverride: /* deterministic fixture */,
            executeNamedAsyncOverride: /* deterministic fixture */),
        new DeckEntryLoader(/* fakes */),
        new FakeMechanicLookupService(),
        new FakeCommanderBanListService(),
        new FakeScryfallSetService(),
        /* spellbookService */ ?? new FakeCommanderSpellbookService(),
        /* catalogService */ ?? new FakeGameChangerCatalogService(EmptyGameChangerCatalog()),
        new AnalysisPromptVariantRegistry(/* all 3 variants */),
        new SetUpgradePromptVariantRegistry(/* all 3 variants */),
        new PacketSessionCache(),
        flagCache,
        NullLogger<DeckAnalysisPacketService>.Instance);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Prompt prose inline per service, per AI | `PromptBuilders/*` strategy registries (7 families) | Phase 15-02 (per in-file comments, e.g. `DeckAnalysisPacketService.cs:1416`) | Already done; not this phase's job to redo. |
| DI wiring inline in `Program.cs` | `PacketServiceCollectionExtensions.AddDeckFlowPacketServices()` / `PromptVariantServiceCollectionExtensions.AddDeckFlowPromptVariants()` extension methods | Predates this research session (both extension classes already exist and are called from `Program.cs:162,164`) | This phase's new collaborators should register into `PacketServiceCollectionExtensions`, continuing the pattern rather than reopening `Program.cs`. |

**Deprecated/outdated:** none — no old approach needs removing; this phase is additive
(collaborator extraction) with the four services shrinking as a result.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IScryfallCardResolver`'s DI lifetime (Scoped vs Singleton) in `ScryfallServiceCollectionExtensions.cs` was not directly re-confirmed this session (only its consumption sites were read) — Architecture Patterns' DI table flags this as needing a pre-wiring check. | Architecture Patterns / DI registration | If it's actually Singleton and the new `ScryfallReferenceResolver` is registered Scoped over it, that's safe (Scoped may depend on Singleton); if it's Scoped and the resolver is registered Singleton, that's a captive-dependency bug. Low risk either way since the fix is a one-line lifetime check before wiring, not a design change. |
| A2 | The "temporary CLAUDE.md override" (Codex reviews-only, expires 2026-06-18) has already lapsed as of this research's 2026-07-04 date, so the standing "Codex codes, Claude reviews" delegation default applies at execution time. | Project Constraints | If the live CLAUDE.md at execution time still shows an active override (e.g., renewed/extended), the executor must re-check rather than trust this document's snapshot. |
| A3 | Comparison, MetaGap, and Primer currently have NO prompt-mutating feature-flag registry equivalent to Analysis's `PromptMutatingAnalysisFlags` — this was confirmed by full-file reads of all three services (no `IFeatureFlagCache` reference found in Comparison or MetaGap; Primer has one flag, `StaleFlag` = `tool.primer.stale-flag`, but it gates a UI banner, not prompt text, per its own doc comment at `DeckPrimerPacketService.cs:112-118`). | Byte-Identical Regression Harness | If a later flag was added to one of these three services after this research session, the "flags OFF only is sufficient" simplification for those three services would be wrong and the harness would under-cover them. |

## Open Questions

> **RESOLVED at plan time (Phase 83 planning, 2026-07-04):**
>
> 1. **PacketArtifactStore zip-serialization dedup -> DEFERRED (out of scope).** Not named in any of
>    PKTSVC-01..04's success criteria (they cover prompt-assembly and Scryfall-resolution, not zip
>    serialization). Recorded as an explicit deferral in 83-01's summary output; remains in
>    REFACTOR-BACKLOG.md row 6. Nothing silently dropped.
> 2. **ReflagCommanderEntry (Cluster B) placement -> its own tiny static class DeckEntryReflagHelper**
>    (plan 83-02), keeping the two named collaborators focused on their ROADMAP-named responsibilities.
> 3. **Two-collaborator boundary confirmed** (ScryfallReferenceResolver + PacketTextAssembler), with
>    DeckEntryReflagHelper as a third micro-helper; no god-collaborator.


1. **Should `PacketArtifactStore.cs`'s parallel `Suggest*ZipFileName`/`Load*FromZip` duplication be
   folded into this phase's scope, or explicitly deferred again?**
   - What we know: Phase 82's triage explicitly punted this decision to "Phase 83's own scope
     check" (`REFACTOR-TRIAGE.md:36`) to avoid two overlapping refactors on the same file.
   - What's unclear: whether the planner considers it in-scope (it's not named in any of the four
     PKTSVC-01..04 success criteria) or out-of-scope.
   - Recommendation: treat as OUT of scope for Phase 83 (the stated success criteria are about
     prompt-assembly and Scryfall-resolution, not zip serialization) and record an explicit
     backlog note, mirroring how Phase 82 itself recorded deferrals with reasons rather than
     silently dropping them.

2. **Is the two-collaborator split (`ScryfallReferenceResolver` + `PacketTextAssembler`) the
   right unit boundary, or should Cluster B (`ReflagCommanderEntry`) and Cluster E (key:value line
   writer) become a third small collaborator (e.g. a `DeckEntryHelpers` static utility) instead of
   living inside one of the two named collaborators?**
   - What we know: Cluster B/E are both small, pure, stateless helpers with no natural home in
     either named collaborator (they're not Scryfall-resolution and not decklist-text-assembly).
   - What's unclear: whether a third micro-utility class is worth the file-count overhead versus
     just placing 2-3 static methods in whichever of the two main collaborators is topically
     closest (e.g. `ReflagCommanderEntry` next to deck-loading, which isn't cleanly owned by
     either new collaborator).
   - Recommendation: the planner's call — either is defensible; a third small static utility
     class (e.g. `DeckEntryReflagHelper` or similar) keeps the two named collaborators focused on
     their ROADMAP-named responsibilities (assembly, resolution) without stretching either to
     cover commander-inference plumbing.

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the .NET 10 SDK and
existing NuGet packages already restored in this repo (no new package, no new external service).

## Validation Architecture

`workflow.nyquist_validation` is explicitly `true` in `.planning/config.json`'s `workflow`
block (confirmed by direct read), so this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + xunit.runner.visualstudio 3.1.4 (`DeckFlow.Web.Tests.csproj`) |
| Config file | none dedicated — standard SDK-style test project, no `xunit.runner.json` found in this session's reads |
| Quick run command | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~PacketService\|FullyQualifiedName~ByteIdentity"` (build via WSL-invoked Windows `dotnet.exe`, per CLAUDE.md's WSL/VSTest-unreliable constraint) |
| Full suite command | `dotnet.exe test DeckFlow.Web.Tests` (and `DeckFlow.Core.Tests` if any extracted logic lands there) |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PKTSVC-01 | Shared decklist/request-context assembly produces identical text to today's per-service versions | unit (characterization/golden) | `dotnet test --filter FullyQualifiedName~PacketTextAssembler` | ❌ Wave 0 — new collaborator + new test file |
| PKTSVC-02 | Shared Scryfall resolver reproduces each service's current resolved-card/oracle-name-map output for fixture inputs | unit (characterization/golden) | `dotnet test --filter FullyQualifiedName~ScryfallReferenceResolver` | ❌ Wave 0 — new collaborator + new test file |
| PKTSVC-03 | Each service's `BuildAsync` still returns the exact same `*Result` record shape/values after delegating to the collaborators | unit (existing test files, extended) | `dotnet test --filter FullyQualifiedName~DeckAnalysisPacketServiceTests\|DeckComparisonServiceTests\|MetaGapServiceTests\|DeckPrimerPacketServiceTests` | ✅ existing files, need new cases added per migration step |
| PKTSVC-04 | Full artifact byte-identity across 3 platforms x flag ON/OFF, pre/post refactor | unit (new golden-string suite) | `dotnet test --filter FullyQualifiedName~ByteIdentity` | ❌ Wave 0 — new test file(s), see Byte-Identical Regression Harness |

### Sampling Rate

- **Per task commit:** targeted filter run scoped to the collaborator/service touched that commit.
- **Per wave merge:** full `DeckFlow.Web.Tests` run.
- **Phase gate:** full suite green (`dotnet.exe test DeckFlow.Web.Tests` + `DeckFlow.Core.Tests`)
  before `/gsd:verify-work`, per the "No ship with failing tests" project convention.

### Wave 0 Gaps

- [ ] New test file(s) for `ScryfallReferenceResolver` (fixture-driven, no live HTTP — mirror the
      `executeCollectionAsyncOverride` pattern already used in `DeckAnalysisPacketServiceTests.cs`).
- [ ] New test file(s) for `PacketTextAssembler` (pure input/output, no fakes needed beyond plain
      `DeckEntry` fixtures).
- [ ] New byte-identity golden-string test suite (one file per service or one shared fixture file
      + four per-service files) capturing pre-refactor output for the 3-platform x flag-matrix.
- [ ] Confirm whether `DeckFlow.Core.Tests` needs anything — likely NOT, since both new
      collaborators are Web-layer per the Architectural Responsibility Map.

## Security Domain

`security_enforcement` is absent from `.planning/config.json` (treated as enabled per default).
This phase touches no authentication, session, or new external-input surface — it re-wires
internal call graphs behind already-validated request models (`DeckAnalysisRequest`,
`DeckComparisonRequest`, `MetaGapRequest`, `DeckPrimerRequest`), whose existing input validation
(deck-source parsing, bracket lookup, JSON-payload size caps like `MaxScoreJsonLength`/
`MaxInteractionAuditJsonLength`/`MaxWinConMapJsonLength` at
`DeckAnalysisPacketService.cs:1612,1668,1744`) is untouched by this refactor.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — no auth surface touched |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A |
| V5 Input Validation | yes (unchanged) | Existing request-model validation and the JSON-length caps above stay exactly as-is; the refactor must not relax any existing `ArgumentNullException.ThrowIfNull`/size-cap guard while moving code between files. |
| V6 Cryptography | no | `PacketSessionCache.ComputeKey` (SHA-256) is untouched cache-key hashing, not a security boundary. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Untrusted round-tripped JSON hidden fields (`ScoreJson`/`InteractionAuditJson`/`WinConMapJson`) deserialized without validation after a refactor accidentally drops a guard | Tampering | The existing `IsStructurallyValid*` guards (`DeckAnalysisPacketService.cs:1595-1605,1643-1651,1700-1712`) must be preserved verbatim if this logic is ever touched by a collaborator extraction — recommend NOT moving these guards at all, since they are unrelated to the two named PKTSVC collaborators. |

## Sources

### Primary (HIGH confidence — direct repo reads this session)

- `.planning/REQUIREMENTS.md` (PKTSVC-01..04 text, cross-cutting gate)
- `.planning/ROADMAP.md` (Phase 83 goal, depends-on, success criteria 1-4)
- `.planning/STATE.md` (phase ordering rationale, Pending Todos pointing to `REFACTOR-BACKLOG.md`/`REFACTOR-TRIAGE.md`)
- `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-TRIAGE.md` (row 6 — `PacketArtifactStore` deferral)
- `docs/decisions/0001-prompt-variants-decoupled.md` (ADR-0001 full text)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (full file, 2372 lines)
- `DeckFlow.Web/Services/DeckComparisonService.cs` (full file, 1034 lines)
- `DeckFlow.Web/Services/MetaGapService.cs` (full file, 957 lines)
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (full file, 905 lines)
- `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` (full file)
- `DeckFlow.Web/Services/PacketSessionCache.cs` (full file)
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` (partial read, header + allowed-names list)
- `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs`, `PromptVariantServiceCollectionExtensions.cs` (full files)
- `DeckFlow.Web/Services/PromptBuilders/Analysis/{AnalysisPromptVariantRegistry.cs,IAnalysisPromptVariant.cs}` (full files, representative of all 7 families)
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (header/doc comment + constructor)
- `DeckFlow.Web.Tests/{ResultContractTests.cs,AnalysisScorePromptParityTests.cs,DeckAnalysisPacketServiceTests.cs}` (partial reads, representative sections)
- `.planning/config.json` (workflow flags)
- `CLAUDE.md` (project instructions, byte-identical gate, delegation rule, WSL testing constraint)

### Secondary (MEDIUM confidence)

- File sizes for `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs`,
  `DeckComparisonServiceTests.cs`, `MetaGapServiceTests.cs`, `DeckPrimerPacketServiceTests.cs`
  (measured via `wc -l`, not fully read line-by-line) — sizes are exact, internal structure
  inferred from the one fully-read sibling test file (`DeckAnalysisPacketServiceTests.cs`).

### Tertiary (LOW confidence)

- `IScryfallCardResolver`'s exact DI lifetime in `ScryfallServiceCollectionExtensions.cs` — not
  read this session; flagged as Assumption A1, needs a one-line confirmation before wiring the
  new resolver collaborator's lifetime.

## Metadata

**Confidence breakdown:**
- Duplication map (Clusters A-G): HIGH — every cluster is a direct file:line read of the actual current code, not inferred.
- Collaborator boundaries: MEDIUM-HIGH — the boundary proposal is a reasoned design grounded in the duplication evidence, but the final shape/naming is the planner's decision to make, not a verified fact.
- Byte-identical harness design: HIGH — built directly from three already-existing, already-passing test patterns in this repo, not invented from scratch.
- DI lifetime for `IScryfallCardResolver`: LOW (unverified this session — see Assumption A1).

**Research date:** 2026-07-04
**Valid until:** 30 days (stable in-repo refactor target; re-verify file line numbers if Phase 82's remaining work or any hotfix touches these four files before Phase 83 executes).
