# Phase 73: Deck-Analysis Command-Zone Awareness — Research

**Researched:** 2026-06-27
**Domain:** DeckAnalysisPacketService plumbing + three analysis prompt variants
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from objective / locked decisions)

### Locked Decisions
- Awareness ONLY — no castability callout, no on-page sim (that is Phase 72 manabase work).
- Edit all THREE prompt variants (ChatGpt/Claude/Gemini). DO NOT extract a shared helper —
  ADR `docs/decisions/0001-prompt-variants-decoupled.md` mandates variants stay decoupled and
  hand-edited.
- Companion carried as SIDE METADATA, NOT remapped to a `Board`.
- Companion auto-detect-first (Archidekt/Moxfield direct); a designator-UI fallback parity
  with manabase is a PLAN decision.
- Flag-gated: may share `manabase.commander-castability` or a deck-analysis-specific flag —
  PLAN decides. Flag OFF → `DeckAnalysisPacketService` + all three variants MUST be
  BYTE-IDENTICAL to current output.
- Depends on Phase 72 (already landed on main): command-zone detection + companion
  side-metadata on `DeckSourceLoadResult`.

### Claude's Discretion
- Whether to use a new `analysis.command-zone-awareness` flag vs. reusing
  `manabase.commander-castability` (research recommends separate flag — see §Flag Mechanism).
- Whether to add a `CompanionName` designator field to `DeckAnalysisRequest` for manual
  fallback (research recommends YES for parity — see §Manabase Designator-UI Fallback).
- How to signal command-zone info to the three variants (interface change vs. enriched string
  — research recommends adding a `string? companionName` parameter to the interface for
  explicit companion labeling; `commanderName` is enriched to the multi-name string
  in-service — see §Architecture Patterns).

### Deferred Ideas (OUT OF SCOPE)
- Castability callout, on-page simulation, command tax modeling.
- Per-commander cast-rate rendering on the deck-analysis page.
- Any changes to how ManabaseAnalysisService works (Phase 72 already shipped that).
</user_constraints>

---

## Summary

Phase 73 gives the `/deck-analysis` prompt artifact command-zone AWARENESS. The generated
analysis prompt currently surfaces a SINGULAR `commanderName` (first entry with
`Board == "commander"`) to all three AI platforms. For partner pairs, commander+Background
decks, and companion decks, the AI receives incomplete command-zone information. Phase 73
fixes this without touching any UI surface, manabase logic, or castability simulation.

The work is in two layers. First, `DeckAnalysisPacketService.BuildAsync` must be updated to
(a) capture `loaded.DetectedCompanionName` — currently discarded at lines 404-406 — and (b)
collect ALL commander entries (not just `FirstOrDefault`) to build a full command-zone picture.
Second, all three prompt variants receive this richer info (an enriched `commanderName` string
for partners/Background, plus a new explicit `companionName` parameter for the companion) and
independently render it in their platform-native format.

Phase 72 (already shipped) established the data plumbing: `DeckSourceLoadResult.DetectedCompanionName`
on the load result, `MoxfieldImportResult.DetectedCompanionName` from the direct API, and the
Background-routes-to-commander-board fact. Phase 73 consumes that plumbing in the deck-analysis
path, which Phase 72 intentionally left unchanged (byte-identity guaranteed by existing test at
`DeckAnalysisPacketServiceTests.cs:879`).

**Primary recommendation:** Enrich `commanderName` to the partner/Background display string in
the service, add a `string? companionName` parameter to `IAnalysisPromptVariant.Build()`, gate
both changes behind a new `analysis.command-zone-awareness` flag (seeded OFF), and add
flag-OFF byte-identity + flag-ON rendering unit tests.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Command-zone detection (who's the commander?) | API/Backend — `DeckAnalysisPacketService` | Importers (already classify boards) | Importers already set `Board = "commander"` for all partners/Background; service collects them |
| Companion auto-detect | Core/Integration — `MoxfieldApiDeckImporter` | `DeckEntryLoader` (propagates it) | Already ships `DetectedCompanionName` on load result; service must capture it |
| Companion manual designator | Frontend — `DeckAnalysisRequest` + view | Service (reads from request) | Same pattern as `ManabaseRequest.CompanionName`; parity with manabase is a plan decision |
| Flag-gate logic | API/Backend — `DeckAnalysisPacketService` | — | Reads `_flagCache.Snapshot().TryGetValue(...)` — default-OFF explicit pattern |
| Prompt text rendering | API/Backend — three `*AnalysisPromptVariant.cs` | — | ADR 0001: decoupled, each variant owns its text independently |
| Byte-identity guarantee | Tests — `DeckAnalysisPacketServiceTests.cs` | — | Flag-OFF regression test confirms no output change when feature is off |

---

## Standard Stack

No new packages. Phase 73 is pure code edits within the existing ASP.NET 10 / C# 12 project.

### Key Existing Types

| Type | File | Role in Phase 73 |
|------|------|------------------|
| `DeckAnalysisPacketService` | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | Primary edit site: capture companion, build command-zone string, pass to variants |
| `DeckSourceLoadResult` | `DeckFlow.Core/Loading/DeckEntryLoader.cs:45` | Already carries `DetectedCompanionName`; Phase 73 reads it |
| `IAnalysisPromptVariant` | `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` | Interface change: add `string? companionName` parameter |
| `AnalysisPromptVariantRegistry` | `…/Analysis/AnalysisPromptVariantRegistry.cs` | Forward new parameter on every `Build()` call |
| `ChatGptAnalysisPromptVariant` | `…/Analysis/ChatGptAnalysisPromptVariant.cs` | Edit: render enriched commanderName + companion line |
| `ClaudeAnalysisPromptVariant` | `…/Analysis/ClaudeAnalysisPromptVariant.cs` | Edit: render enriched commanderName + companion XML tag |
| `GeminiAnalysisPromptVariant` | `…/Analysis/GeminiAnalysisPromptVariant.cs` | Edit: render enriched commanderName + companion line |
| `DeckAnalysisRequest` | `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | PLAN decision: add `CompanionName` string property for designator-UI fallback |
| `FeatureFlagStore` | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | Four-file registration of new flag (seed OFF both dialects) |
| `FeatureFlagCatalog` | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` | Add catalog entry for new flag |

---

## Package Legitimacy Audit

> No external packages installed in this phase. Section skipped.

---

## Architecture Patterns

### System Architecture Diagram

```
Browser POST /deck-analysis (Step 2)
    │
    ▼
DeckController.DeckAnalysis()
    │
    ▼
DeckAnalysisPacketService.BuildAsync(request)
    │
    ├─► DeckEntryLoader.LoadFromSourceAsync(deckSource)
    │       │ returns DeckSourceLoadResult {
    │       │   Entries,
    │       │   FallbackNotice,
    │       │   DetectedCompanionName  ← Phase 73 CAPTURES this
    │       │ }
    │       │
    │       ├─ Moxfield direct API → MoxfieldApiDeckImporter
    │       │     companions board → DetectedCompanionName populated
    │       │     commanders board → Board="commander" entries
    │       │
    │       └─ Archidekt → ArchidektApiDeckImporter
    │             "Commander" category → Board="commander" entries
    │             "Companion" category → Board="mainboard" (NOT detected)
    │             (no DetectedCompanionName from Archidekt)
    │
    ├─► [Phase 73 NEW] Compute CommandZone when flag ON:
    │       allCommanderNames = deckEntries where Board="commander" → names
    │       commanderName (enriched) = allCommanderNames joined " & " or similar
    │       companionName = ResolveName(request.CompanionName, DetectedCompanionName)
    │       [flag OFF → commanderName = first entry name (today), companionName = null]
    │
    ├─► BuildAnalysisPrompt(request, decklistText, referenceText, schema,
    │       commanderName,     ← now enriched partner string if flag ON
    │       selectedQuestions, bannedCards, comboResult, includeCardVersions,
    │       companionName)     ← NEW parameter (null when flag OFF)
    │       │
    │       ▼
    │   AnalysisPromptVariantRegistry.Build(platform, ..., commanderName, companionName)
    │       │
    │       ├─ ChatGptAnalysisPromptVariant.Build() → prompt text
    │       ├─ ClaudeAnalysisPromptVariant.Build()  → prompt text
    │       └─ GeminiAnalysisPromptVariant.Build()  → prompt text
    │
    └─► DeckAnalysisPacketResult { AnalysisPromptText, ResolvedCommanderName, ... }
```

### Recommended Project Structure

No new files or folders needed unless the planner chooses to add a `CommandZoneContext` record.
All edits are within existing files listed in Standard Stack above.

### Pattern 1: Capturing DetectedCompanionName in BuildAsync

**What:** At the `LoadFromSourceAsync` call site in `DeckAnalysisPacketService.BuildAsync`, the
result carries `DetectedCompanionName` but it is currently discarded.

**Current code (lines 404-406):**
```csharp
// CURRENT — DetectedCompanionName silently dropped
var loaded = await _deckEntryLoader.LoadFromSourceAsync(request.DeckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
_lastImportNotice = loaded.FallbackNotice;
var entries = loaded.Entries;
```

**Phase 73 change (flag-aware):**
```csharp
var loaded = await _deckEntryLoader.LoadFromSourceAsync(request.DeckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
_lastImportNotice = loaded.FallbackNotice;
var entries = loaded.Entries;
var detectedCompanionName = loaded.DetectedCompanionName; // Phase 73: capture
```

The same capture is needed at the second `LoadFromSourceAsync` call site in
`TryComputeCacheKeyAsync` (line 1232) IF companion participates in the cache key.

### Pattern 2: Computing enriched commanderName and companionName

**When flag is ON**, computed after commander entries are identified:

```csharp
// Collect ALL commander entries (already classified by importers)
var commanderEntries = deckEntries
    .Where(e => string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
    .Select(e => e.Name)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
    .ToList();

// Enrich commanderName to partner/background display string
// Single commander: unchanged ("Tymna the Weaver")
// Partners: "Tymna the Weaver & Thrasios, Triton Hero"
// Cmd+Background: "Abdel Adrian, Gorion's Ward & Passionate Archaeologist"
var enrichedCommanderName = commanderEntries.Count switch
{
    0 => commanderName,   // preserve existing resolved value
    1 => commanderEntries[0],
    _ => string.Join(" & ", commanderEntries)
};

// Companion: designator wins over detected (mirrors ManabaseAnalysisService:459-460)
var companionName = ResolveCompanionName(request.CompanionName, detectedCompanionName);
```

Note: `commanderName` may already be oracle-resolved at line 626-629 for the manabase path.
The enrichment above should happen AFTER oracle resolution to avoid stale printed names.

### Pattern 3: Flag-reading (default-OFF)

Use the explicit snapshot `TryGetValue` pattern — the same as `ReferenceDeckStatsFlag` and
`ManabaseAnalysisService.IsFlagOn()`:

```csharp
internal const string CommandZoneAwarenessFlag = "analysis.command-zone-awareness";

// In BuildAsync, after _flagCache is available:
var commandZoneAwareness = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(CommandZoneAwarenessFlag, out var czOn)
    && czOn;
```

`IsEnabled()` must NOT be used here because it defaults missing keys to ON. The explicit
`TryGetValue` pattern is the only correct choice for default-OFF flags in this codebase.

### Pattern 4: IAnalysisPromptVariant interface change

**Current signature (`IAnalysisPromptVariant.cs:26-35`):**
```csharp
string Build(
    DeckAnalysisRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult,
    bool includeCardVersions);
```

**Phase 73 change — add `companionName` as the last parameter:**
```csharp
string Build(
    DeckAnalysisRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,          // enriched to "Name1 & Name2" when flag ON + partners
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult,
    bool includeCardVersions,
    string? companionName = null);  // Phase 73: null when flag OFF (byte-identical guarantee)
```

Using a default-null parameter avoids a breaking change for any callers that do not need
companion (internal variants, registry, tests all update — but the default keeps test setups
that don't care about companion from needing modification).

### Pattern 5: Variant rendering (3 independent edits, per ADR 0001)

**ChatGptAnalysisPromptVariant.cs — DECK CONTEXT section:**

Current (lines 60-63):
```csharp
if (!string.IsNullOrWhiteSpace(commanderName))
{
    builder.AppendLine($"commander: {commanderName}");
}
```

Phase 73 (flag ON enriches commanderName before calling Build; companion added explicitly):
```csharp
if (!string.IsNullOrWhiteSpace(commanderName))
{
    builder.AppendLine($"commander: {commanderName}");
}
if (!string.IsNullOrWhiteSpace(companionName))
{
    builder.AppendLine($"companion: {companionName} (this deck's companion; applies its companion deckbuilding restriction)");
}
```

**Title line (line 48)** also uses `commanderName` — when enriched to "Tymna & Thrasios" the
title becomes "Tymna the Weaver & Thrasios, Triton Hero | Deck Analysis" which is correct.

**ClaudeAnalysisPromptVariant.cs — commander XML tag (lines 58-62):**

Current:
```csharp
if (!string.IsNullOrWhiteSpace(commanderName))
{
    builder.AppendLine($"<commander>{commanderName}</commander>");
}
```

Phase 73:
```csharp
if (!string.IsNullOrWhiteSpace(commanderName))
{
    builder.AppendLine($"<commander>{commanderName}</commander>");
}
if (!string.IsNullOrWhiteSpace(companionName))
{
    builder.AppendLine($"<companion>{System.Security.SecurityElement.Escape(companionName)}</companion>");
    builder.AppendLine("<companion_note>This is the deck's companion; it applies its companion deckbuilding restriction.</companion_note>");
}
```

**GeminiAnalysisPromptVariant.cs — DECK CONTEXT section (lines 65-67):**
Same structural edit as ChatGpt (independent hand-edit):
```csharp
if (!string.IsNullOrWhiteSpace(commanderName))
{
    builder.AppendLine($"commander: {commanderName}");
}
if (!string.IsNullOrWhiteSpace(companionName))
{
    builder.AppendLine($"companion: {companionName} (this deck's companion; applies its companion deckbuilding restriction)");
}
```

**AnalysisPromptVariantRegistry.cs — forward new parameter:**
```csharp
return variant.Build(request, decklistText, referenceText, deckProfileSchemaJson,
    commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions,
    companionName);   // Phase 73: forward companion
```

And the `Build()` method signature on the registry must add `string? companionName = null`.

### Anti-Patterns to Avoid

- **Extracting a shared helper** for companion text: ADR 0001 forbids it. Each variant writes
  its own companion text independently.
- **Remapping Board to "companion"** for the companion entry: the SPEC (§B) explicitly forbids
  this. `DeckAnalysisPacketService` already excludes sideboard/maybeboard (lines 409-418) but
  the companion stays in mainboard and is inert to deck-analysis content.
- **Using `IsEnabled()` for a default-OFF flag**: `IsEnabled()` returns `true` for absent keys.
  Use `Snapshot().TryGetValue(...)` instead.
- **Including `TimingSummary` in byte-identity comparison**: `FlattenPacketText` at line 1511
  already excludes it (Stopwatch ms is environmental). Do not add TimingSummary to a new
  `PacketBytes` variant.
- **Mutating `commanderName` before oracle-resolution**: The oracle name map is applied at
  line 626-629. Enrichment should happen after oracle resolution to avoid stale printed names
  appearing in the prompt.
- **Changing TryComputeCacheKeyAsync without understanding its contract**: The cache key uses
  PRE-Scryfall commander name (line 239). If companion participates in the cache key, it must
  be the pre-Scryfall detected value — not the oracle-resolved value.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Companion name validation/bounding | Custom length check | Mirror `ManabaseAnalysisService.BoundCompanionName()` (lines 462-472) | Already handles null/whitespace/200-char max |
| Flag default-OFF read | `IsEnabled()` with manual default | `Snapshot().TryGetValue(key, out var on) && on` | Pattern is established; `IsEnabled()` defaults missing keys ON |
| Oracle name mapping for enriched string | Re-implement lookup | Reuse existing `cardReferenceBundle.OracleNameMap` (line 626) | Already resolved — apply map to all partner names |

---

## Research Questions Answered

### RQ1: DeckAnalysisPacketService today — commanderName and deck text

**`commanderName` extraction:**
- Primary: `deckEntries.FirstOrDefault(e => e.Board == "commander")?.Name` at line 171-173
- Fallback for Moxfield exports without Commander header: lines 178-216 (Moxfield ordering
  heuristic). The heuristic supports up to 2 leading 1-of entries as partners.
- After Scryfall validation: oracle-name-resolved at line 626-629.
- **KEY GAP**: `FirstOrDefault` → only the first commander name flows into prompts, even for
  partner pairs or Background decks.

**`DetectedCompanionName` gap:**
- `DeckSourceLoadResult` at `DeckFlow.Core/Loading/DeckEntryLoader.cs:45` carries
  `DetectedCompanionName` as an optional string.
- `DeckAnalysisPacketService.BuildAsync` at lines 404-406: only captures `.FallbackNotice`
  and `.Entries`. `DetectedCompanionName` is **silently discarded**.
- The same gap exists at line 1232 (TryComputeCacheKeyAsync call site).

**Deck text (BuildDecklistText, lines 790-841):**
- Commander section: all entries with `Board == "commander"` ordered by name.
- Mainboard: all other entries.
- Both partners/Background correctly appear under "Commander" section in the deck text already.
  The deck text is correct — only the `commander:` field in DECK CONTEXT is wrong (single name).

**`commanderName` flows to:**
- Line 481: `BuildInputSummary`
- Line 484: `BuildDeckProfileSchemaJson`
- Line 485: `BuildRequestContextText`
- Line 635: `BuildAnalysisPrompt` → all three variants
- Line 639/644: `BuildSetUpgradePrompt`
- Line 656: `BuildSuggestedChatTitle`
- Line 674: `DeckAnalysisPacketResult.ResolvedCommanderName`

Phase 73 enriches `commanderName` before these calls (when flag ON). All call sites benefit
automatically. The set-upgrade prompt also gets the enriched name (correct behavior).

### RQ2: Phase 72 command-zone detection

**Commander detection mechanism (unchanged since before Phase 72):**
- Importers set `Board = "commander"` for commander-board cards:
  - Moxfield direct API: `AddBoardEntries(root, "commanders", "commander", ...)` at line 94.
  - Moxfield Spellbook fallback: `AddSpellbookEntries(root, "commanders", "commander", ...)`
    at line 125.
  - Archidekt: `DetermineBoard()` at line 126 maps "Commander" category → "commander" board.
- Background on Archidekt: Uses "Commander" category (per STATE.md fixture confirmation —
  "Background already command-zone via importer"). No reclassification needed.
- Background on Moxfield: Appears in the "commanders" board on the API response (same as
  regular commander). Moxfield treats partner commanders and Background as commanders.

**`commanderCount` (manabase only, NOT for deck-analysis):**
- `ManabaseClassifier.cs:101-103`: counted from `IsCommander` flag on `DeckCardEntry`.
- Manabase-specific, not available to `DeckAnalysisPacketService`.

### RQ3: Companion side-metadata representation (Phase 72)

The companion is NOT remapped to a Board. It flows as side metadata:

```
MoxfieldApiDeckImporter.ImportWithSourceAsync()
    └─ ReadFirstCompanionName(root)  → reads root["companions"] JSON board
    └─ MoxfieldImportResult { DetectedCompanionName = name }

DeckEntryLoader.LoadFromSourceAsync()
    └─ Line 124: propagates to DeckSourceLoadResult.DetectedCompanionName

ManabaseAnalysisService.ResolveAndClassifyAsync()
    └─ Line 370: ResolveCompanionName(companionDesignator, load.DetectedCompanionName)
    └─ Companion resolved → modeled as separate castability row

DeckAnalysisPacketService.BuildAsync()
    └─ Line 404-406: DISCARDS DetectedCompanionName  ← Phase 73 fixes this
```

**Archidekt companion**: the Archidekt importer at `ArchidektApiDeckImporter.cs:126-143`
does NOT handle "Companion" category — it falls through to "mainboard". The Archidekt importer
does not return `MoxfieldImportResult` and has no `DetectedCompanionName` path. For Archidekt
companion decks, Phase 73 relies on the manual designator (if the planner adds it) or the
companion remaining invisible (same as today, but explicitly documented).

### RQ4: Manabase designator-UI fallback (Phase 72)

**Pattern in ManabaseRequest / ManabaseAnalysisService:**

1. `ManabaseRequest.CompanionName` at `DeckFlow.Web/Models/ManabaseRequest.cs:55-58` — plain
   `string` property, bounded by `ManabaseAnalysisService.MaxCompanionNameLength = 200`.
2. View exposes `#manabase-companion-name` input inside a `<details class="manabase-overrides">`
   section (collapsible, confirmed by `manabase-commander-callout.spec.ts:37`).
3. Controller pre-fills from auto-detected companion when available.
4. `ManabaseAnalysisService.ResolveCompanionName(designator, detected)` at line 459-460:
   designator wins over detected; `BoundCompanionName()` at lines 462-472 trims and caps.

**For Phase 73 parity:**
- Add `CompanionName` (or `CompanionDesignator`) to `DeckAnalysisRequest` — same pattern as
  `ManabaseRequest.CompanionName`.
- The deck-analysis Step 1 view would show the companion input (possibly in an Advanced or
  collapsible section, gated on the flag).
- OR: skip designator UI and rely only on auto-detection (Moxfield direct). Planner decides.

**When auto-detect fails (Moxfield Spellbook fallback or Archidekt):** Companion is invisible
to the prompt unless a manual designator is provided. The planner should decide whether to
surface a UI input or document the limitation.

### RQ5: The three prompt variants — command-zone rendering today

**All three variants follow the same structural pattern:**

| Variant | File | commanderName usage |
|---------|------|---------------------|
| ChatGpt | `ChatGptAnalysisPromptVariant.cs` | Title line 47-50; `commander:` in DECK CONTEXT lines 61-63 |
| Claude | `ClaudeAnalysisPromptVariant.cs` | `<commander>{commanderName}</commander>` tag lines 58-62 |
| Gemini | `GeminiAnalysisPromptVariant.cs` | Title line 55-57; `commander:` in DECK CONTEXT lines 65-67 |

**ADR 0001 confirmation:** `docs/decisions/0001-prompt-variants-decoupled.md` (recorded
2026-06-03). States "Keep the per-platform prompt variants fully decoupled. Do not extract
shared guidance text, constants holders, or base prompt builders." Phase 73 must hand-edit all
three independently. The ADR prohibits shared TEXT helpers — it does not prohibit a shared
interface parameter or a service-layer string computation.

**Companion is not surfaced in any variant today.** No `companion:` field exists in any of
the three analysis prompts.

### RQ6: Flag mechanism

**Two flag-read patterns in `DeckAnalysisPacketService`:**

Default-ON (fail-safe ON, gate engages when flag explicitly DISABLED):
```csharp
// analysis.reference.full-oracle-text  — line 594
var recencyGateEnabled = !(_flagCache?.IsEnabled(ReferenceFullOracleFlag) ?? true);
```

Default-OFF (explicit snapshot check — correct for Phase 73):
```csharp
// analysis.reference.deck-stats  — lines 605-607
var deckStatsEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(ReferenceDeckStatsFlag, out var deckStatsOn)
    && deckStatsOn;
```

**Flag options for Phase 73:**

Option A — Reuse `manabase.commander-castability`:
- Already seeded OFF (`FeatureFlagStore.cs:224,255`), already in catalog (`FeatureFlagCatalog.cs:67`).
- No four-file registration needed.
- CONS: The existing byte-identity test at `DeckAnalysisPacketServiceTests.cs:879-910`
  toggles this flag and asserts deck-analysis is byte-identical OFF vs ON. Phase 73 would
  BREAK that test (ON now changes deck-analysis output) — the test would need updating.
- CONS: Operator enabling the manabase callout would silently enable deck-analysis awareness
  too, and vice versa. Conceptually wrong (manabase flag controls non-manabase feature).
- CONS: `manabase.commander-castability` is semantically wrong for deck-analysis (no
  castability involved here).

Option B — New `analysis.command-zone-awareness` flag (RECOMMENDED):
- Follows the `analysis.*` namespace pattern used by all other deck-analysis flags.
- Four-file registration: `PostgresSeedSql`, `SqliteSeedSql`, `FeatureFlagCatalog`,
  `FeatureFlagCatalogTests` — same pattern as Phase 72 Plan 01.
- The existing byte-identity test at line 879-910 remains valid unchanged (it only toggles
  `manabase.commander-castability`, which still produces byte-identical deck-analysis because
  Phase 73 only reads the new flag).
- Operators can enable manabase callout and deck-analysis awareness independently.
- CONS: Requires four-file registration (one plan, ~15 lines of code).

**Recommendation: Option B (`analysis.command-zone-awareness`).**

**Four-file registration pattern (from Phase 72 Plan 01):**
1. `FeatureFlagStore.PostgresSeedSql` — add `('analysis.command-zone-awareness', FALSE)`
2. `FeatureFlagStore.SqliteSeedSql` — add `('analysis.command-zone-awareness', 0)`
3. `FeatureFlagCatalog.Descriptions` — add `["analysis.command-zone-awareness"] = "..."`
4. `FeatureFlagCatalogTests` — add two `[InlineData]` rows (existence + seeded-false)

### RQ7: Testing

**Existing relevant tests in `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs`:**

| Test | Line | What it covers | Phase 73 impact |
|------|------|----------------|----------------|
| `BuildAsync_DetectsPartnerCommandersFromLeadingEntries_WhenNoCommanderSectionHeader` | 912+ | Partner pair detection from Moxfield ordering | Baseline; unaffected by Phase 73 (flag OFF) |
| `BuildAsync_DoesNotLeakCompanionDeckContent_WhenImportCarriesDetectedCompanionMetadata` | 851-876 | Companion excluded from deck-analysis (byte-identical to no-companion) | Must remain passing — companion STILL excluded from deck-text when flag OFF |
| `BuildAsync_IsByteIdentical_WhenCommanderCastabilityFlagTogglesForCompanionBackgroundDeck` | 879-910 | `manabase.commander-castability` OFF == ON for deck-analysis | Remains valid if Phase 73 uses new flag (Option B) |

**`FlattenPacketText` (line 1500-1514)** — concatenates:
```
InputSummary | ReferenceText | AnalysisPromptText | SetUpgradePromptText |
RequestContextText | DeckProfileSchemaJson | SuggestedChatTitle | ResolvedCommanderName
```
TimingSummary excluded (non-deterministic Stopwatch ms — comment at line 1511).

**Phase 73 new tests needed:**

1. **Flag-OFF byte-identity (single commander)**: `analysis.command-zone-awareness = false` →
   `PacketBytes(result)` == `PacketBytes(baselineResult)` (single commander, same as today).

2. **Flag-OFF byte-identity (companion+background deck)**: Same as test at line 879-910 but
   using the new flag. Companion metadata detected but discarded when flag OFF.

3. **Flag-ON — partner pair**: `commanderName` in `AnalysisPromptText` contains both partner
   names (e.g., "Tymna the Weaver & Thrasios, Triton Hero"). All three platforms independently
   tested (or one platform as representative with platform-specific test for companion tag).

4. **Flag-ON — companion rendered**: `companionName` appears in `AnalysisPromptText` as the
   platform-appropriate tag/field. Companion NOT in deck text (still mainboard).

5. **Flag-ON — single commander (no regression)**: single-commander deck with flag ON still
   produces the same `commanderName` rendering as today (no spurious `&` appended).

**Existing test infrastructure:**
- `FakeMoxfieldDeckImporter` at line 1623: supports `detectedCompanionName` parameter.
- `FakeFeatureFlagCache` at line 885+: supports arbitrary key→bool dict.
- `CreateCompanionFixtureEntries(includeBackgroundCommander: true/false)` at line 1463: ready
  for partner+Background fixture.
- Current Web test count: 875 passing, 12 skipped (verified 2026-06-27).

**e2e coverage:**
- `deck-analysis-render.spec.ts` tests Step 3 rendering (deck_profile parse/display) — no
  prompt generation involved, no Scryfall calls. Unaffected by Phase 73.
- No existing e2e for Step 2 prompt generation (requires live Scryfall).
- Phase 73 does not change any UI surface visible to the e2e suite → **no new e2e tests
  needed** (unless the planner adds a designator-UI input to Step 1, in which case one smoke
  test checking the input renders when flag ON would be appropriate).

---

## Common Pitfalls

### Pitfall 1: Oracle-name mutation order

**What goes wrong:** Enriching `commanderName` to "Name1 & Name2" BEFORE the oracle-name
resolution at line 626-629 uses the original (printed) name as the map key.

**Why it happens:** The oracle name map is keyed on original deck entry name. If `commanderName`
has already been concatenated (e.g., "Ragavan & Esika // The Prismatic Bridge"), the map lookup
fails to find it and printed name leaks into the prompt.

**How to avoid:** Compute per-name oracle resolution first, THEN concatenate:
```csharp
// Resolve each partner name through oracle map individually
var resolvedCommanderNames = commanderEntries
    .Select(name => cardReferenceBundle.OracleNameMap.TryGetValue(name, out var oracle)
        ? oracle : name)
    .ToList();
var enrichedCommanderName = string.Join(" & ", resolvedCommanderNames);
```

**Warning signs:** "Esika // The Prismatic Bridge" appears in the prompt instead of "Esika,
God of the Tree" (Moxfield sometimes uses the full DFC name).

### Pitfall 2: Breaking the existing byte-identity test if flag is shared

**What goes wrong:** If Phase 73 reuses `manabase.commander-castability`, the test at line
879-910 toggles that flag and asserts deck-analysis is byte-identical OFF vs ON. Phase 73
would make that assertion false (flag ON now enriches the prompt).

**How to avoid:** Use a separate `analysis.command-zone-awareness` flag (Option B). The
existing test remains valid unchanged.

**Warning signs:** CI failure on `BuildAsync_IsByteIdentical_WhenCommanderCastabilityFlagTogglesForCompanionBackgroundDeck`.

### Pitfall 3: Cache key invalidation for enriched commanderName

**What goes wrong:** `BuildDeckAnalysisCacheInputs` at line 238-246 uses
`Commander: commanderName ?? string.Empty` as the cache key. If Phase 73 changes
`commanderName` from "Tymna" to "Tymna & Thrasios" for the same deck, the cache key changes.
This is CORRECT behavior (different command-zone → different cache entry). However, if
`commanderName` enrichment is applied inconsistently between `BuildAsync` and
`TryComputeCacheKeyAsync`, the keys diverge and the cache never hits.

**How to avoid:** `TryComputeCacheKeyAsync` uses `ResolvePreScryfallCommanderState` to
compute the key, which only returns the first commander name. If Phase 73 enriches
`commanderName` in `BuildAsync` but NOT in `TryComputeCacheKeyAsync`, cache hits become
impossible for partner decks when flag ON.

**Option 1 (simpler):** Keep the cache key using just the first commander name regardless of
flag state. Cache is not required for correctness — only for performance. This is safe.

**Option 2:** Enrich the cache key too by collecting all commander names in both paths. More
complex, requires touching `ResolvePreScryfallCommanderState` or `BuildDeckAnalysisCacheInputs`.

**Recommendation:** Option 1 (simpler). Document that the cache key remains pre-Scryfall
first-commander for now; enrichment optimization deferred.

### Pitfall 4: Companion in deck text vs. companion in prompt DECK CONTEXT

**What goes wrong:** For Archidekt the companion stays classified `mainboard`
(`ArchidektApiDeckImporter.cs`), so `BuildDecklistText` legitimately lists it (e.g. "Jegantha,
the Wellspring") in the DECKLIST section. If the DECK CONTEXT `companion:` copy claims the
companion is "outside the 99" / "not in the deck", it CONTRADICTS that mainboard placement and
is simply false — confusing the AI.

**How to avoid (LOCKED, Codex HIGH-1 — awareness only):** Do NOT mutate, filter, or move the
deck text — that preserves flag-OFF byte-identity and avoids a risky display-only decklist
change. Render the companion ONLY as DECK CONTEXT / `<companion>` side metadata, and use
location-AGNOSTIC copy that names the companion and notes its restriction WITHOUT asserting
which zone it sits in (true for both Archidekt, where it is in the 99, and Moxfield, where it is
detected separately):
```
companion: Jegantha, the Wellspring (this deck's companion; applies its companion deckbuilding restriction)
```

The companion may still appear among the listed cards; that is acceptable. Excluding the
companion from the deck text is explicitly OUT OF SCOPE for this awareness-only phase.

### Pitfall 5: Archidekt companion invisible without designator UI

**What goes wrong:** Archidekt's "Companion" category routes to "mainboard" (not detected).
`DeckSourceLoadResult.DetectedCompanionName` is always null for Archidekt. If Phase 73 does
not add a `CompanionName` field to `DeckAnalysisRequest` AND the Archidekt importer is not
updated, Archidekt companion decks get no companion awareness even when the flag is ON.

**How to avoid:** Either (a) add `CompanionName` to `DeckAnalysisRequest` as a manual
designator field (parity with manabase), or (b) document the Archidekt companion limitation
and recommend users use Moxfield for companion decks. Planner decides.

---

## Code Examples

### Example 1: Flag-OFF byte-identity guarantee (key invariant)

When `analysis.command-zone-awareness = false`:
- `companionName` parameter is `null` for all three variants.
- `commanderName` is the same first-commander string as today (unchanged code path).
- `Build()` methods produce byte-identical output to current production.

Enforced by: new xUnit test asserting `PacketBytes(flagOffResult) == PacketBytes(baseline)`.

### Example 2: Partner pair prompt rendering (ChatGpt)

Flag ON, two commanders (Tymna + Thrasios):
```
Title this chat: Tymna the Weaver & Thrasios, Triton Hero | Deck Analysis

## DECK CONTEXT
format: Commander
commander: Tymna the Weaver & Thrasios, Triton Hero
target_bracket: Optimized
```

### Example 3: Claude companion rendering

```xml
<commander>Esika, God of the Tree</commander>
<companion>Keruga, the Macrosage</companion>
<companion_note>This is the deck's companion; it applies its companion deckbuilding restriction.</companion_note>
```

### Example 4: Resolving companion with designator priority

```csharp
// Mirror ManabaseAnalysisService.ResolveCompanionName (lines 459-460)
private static string? ResolveCompanionName(string? designator, string? detected)
    => BoundCompanionName(designator) ?? BoundCompanionName(detected);

private static string? BoundCompanionName(string? name)
{
    if (string.IsNullOrWhiteSpace(name)) return null;
    var trimmed = name.Trim();
    return trimmed.Length <= 200 ? trimmed : trimmed[..200];
}
```

This can live as private static helpers on `DeckAnalysisPacketService` (same file, same
pattern as `NormalizeSingleLine`, `ParseCardNameList`).

---

## State of the Art

| Old Approach | Current Approach (shipped) | Phase 73 Change |
|--------------|---------------------------|-----------------|
| Single `commanderName` string | First `Board="commander"` entry only | Collect ALL commander entries when flag ON |
| No companion in deck-analysis | Companion discarded (line 404-406) | Capture `DetectedCompanionName`; pass to variants when flag ON |
| Companion invisible to AI | AI sees companion in Mainboard only | AI gets explicit `companion:` / `<companion>` field in DECK CONTEXT |
| Partner deck: "Tymna" in title | "Tymna" only | "Tymna & Thrasios" (enriched from all commander entries) |

**Deprecated/outdated in Phase 73 context:**
- The Phase 72 byte-identity test at line 879-910 (if planner chooses Option A — shared flag).
  If Option B (separate flag), this test remains valid.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Background on Archidekt uses "Commander" category → already in command zone | RQ3 | If some Background cards use "Background" category instead, they'd appear as mainboard; audit the DetermineBoard code (confirmed: it only checks "Commander", "Maybeboard", "Sideboard") |
| A2 | Background on Moxfield appears in the "commanders" API board | RQ2 | If Moxfield puts Background in a separate "backgrounds" board, the importer won't route it; needs a live fixture to confirm |
| A3 | Archidekt importer produces no `DetectedCompanionName` | RQ3 | `ArchidektApiDeckImporter` does not return `MoxfieldImportResult`; confirmed by code inspection |
| A4 | Variant edit sites are lines 47-63 (ChatGpt), 58-62 (Claude), 55-67 (Gemini) | RQ5 | Line numbers are from current `main` as of 2026-06-27; may shift by 1-2 lines if intervening commits touched these files |
| A5 | `SuggestedChatTitle` already uses the single `commanderName` | RQ1 | Confirmed at line 656; enriching commanderName enriches the title automatically (desired) |

---

## Open Questions (RESOLVED)

> All four resolved during planning (2026-06-27). Decisions are locked in ROADMAP.md Phase 73
> and implemented across plans 73-01..73-04.

1. **Companion designator UI (plan decision)**
   - What we know: ManabaseRequest has `CompanionName` string; manabase view shows an input.
   - What's unclear: Should deck-analysis Step 1 add a companion input field?
   - Recommendation: YES, add `CompanionName` to `DeckAnalysisRequest` for parity. Gate its
     rendering behind the flag so Step 1 only shows the input when flag ON.
   - **RESOLVED:** YES — add `DeckAnalysisRequest.CompanionName` (73-01) + flag-gated Step-1
     input (73-04), parity with the manabase commander-callout pattern. Manual input (no
     auto-detect pre-fill; detected companion is only known post-Step-2).

2. **Cache key parity with enriched commanderName**
   - What we know: `TryComputeCacheKeyAsync` uses `ResolvePreScryfallCommanderState` → first
     commander. `BuildAsync` would enrich to multi-name string. Keys diverge for partner decks
     when flag ON.
   - What's unclear: Is cache correctness required for flag-ON operation?
   - Recommendation: Keep cache key using first commander for now (correctness, not performance).
     Document deviation; optimize later if cache hit rate is an issue.
   - **RESOLVED:** Cache key UNCHANGED — stays on the first (pre-Scryfall) commander; 73-02
     acceptance criteria forbids edits to `TryComputeCacheKeyAsync`. Correctness over hit-rate;
     also closes the cache-poisoning threat (T-73, RESEARCH §Security).

3. **Flag: new `analysis.command-zone-awareness` vs. reuse `manabase.commander-castability`**
   - What we know: See RQ6 — separate flag is recommended.
   - What's unclear: Whether the operator wants joint control ("enable awareness everywhere
     at once") or separate control.
   - Recommendation: Separate flag (`analysis.command-zone-awareness`). The operator can toggle
     both independently. Four-file registration is low-cost.
   - **RESOLVED:** NEW separate flag `analysis.command-zone-awareness`, seeded OFF (73-01).
     Keeps the existing byte-identity test (DeckAnalysisPacketServiceTests.cs:879, toggles the
     manabase flag) valid unchanged; gives independent operator control.

4. **Archidekt companion limitation**
   - What we know: Archidekt importer doesn't detect companion; no `DetectedCompanionName`
     produced for Archidekt decks.
   - What's unclear: Whether to update the Archidekt importer to detect "Companion" category
     OR just document the limitation.
   - Recommendation: Add `CompanionName` to `DeckAnalysisRequest` (designator UI) so Archidekt
     users can manually name the companion. Updating the importer is out of scope for Phase 73.
   - **RESOLVED:** Designator UI (Q1) covers Archidekt/pasted decks via manual companion entry;
     the Archidekt importer is NOT modified (out of scope for Phase 73).

---

## Environment Availability

> Step 2.6: SKIPPED. Phase 73 is purely code/config edits — no external tools, services,
> runtimes, or CLIs beyond `dotnet` (already available) are required.

---

## Validation Architecture

> `workflow.nyquist_validation = true` — section included.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| Quick run command | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "DeckAnalysisPacketServiceTests" -q` |
| Full suite command | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -q` |
| Current passing | 875 passing, 12 skipped (verified 2026-06-27) |

### Phase Requirements to Test Map

| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| Flag OFF byte-identity | When `analysis.command-zone-awareness=false`, prompt is byte-identical to current output | unit | `dotnet.exe test --filter "BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff"` | Wave 0 (new) |
| Partner pair rendering | Flag ON + 2 commanders → both names in `AnalysisPromptText` ("A & B" format) | unit | `dotnet.exe test --filter "BuildAsync_CommandZoneAwareness_RendersPartnerPair"` | Wave 0 (new) |
| Companion rendered | Flag ON + companion → `companion:` / `<companion>` in prompt; companion NOT in mainboard deck text | unit | `dotnet.exe test --filter "BuildAsync_CommandZoneAwareness_RendersCompanion"` | Wave 0 (new) |
| Single commander no regression | Flag ON + 1 commander → prompt unchanged vs. flag OFF (commander name same, no spurious `&`) | unit | `dotnet.exe test --filter "BuildAsync_CommandZoneAwareness_SingleCommanderUnchanged"` | Wave 0 (new) |
| Companion leak guard (existing) | `DetectedCompanionName` on load result does NOT affect deck text when flag OFF | unit | `dotnet.exe test --filter "BuildAsync_DoesNotLeakCompanionDeckContent"` | ✅ exists line 851 |
| Manabase flag unchanged (existing) | `manabase.commander-castability` still produces byte-identical deck-analysis (if separate flag used) | unit | `dotnet.exe test --filter "BuildAsync_IsByteIdentical_WhenCommanderCastabilityFlag"` | ✅ exists line 879 |

### Sampling Rate

- **Per task commit:** `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "DeckAnalysisPacketServiceTests" -q`
- **Per wave merge:** `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -q`
- **Phase gate:** Full suite green (875+ passing) before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] New test `BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff` — covers flag-OFF byte-identity
- [ ] New test `BuildAsync_CommandZoneAwareness_RendersPartnerPair` — covers partner enrichment
- [ ] New test `BuildAsync_CommandZoneAwareness_RendersCompanion` — covers companion field per platform
- [ ] New test `BuildAsync_CommandZoneAwareness_SingleCommanderUnchanged` — regression for solo commander

---

## Security Domain

> `security_enforcement` not explicitly set to false — section included.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Not applicable (no auth changes) |
| V3 Session Management | No | Not applicable |
| V4 Access Control | No | Not applicable (deck-analysis is public) |
| V5 Input Validation | Yes | `CompanionName` (if added to `DeckAnalysisRequest`) must be bounded. Mirror `BoundCompanionName()` (200-char cap, null-safe). |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for This Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prompt injection via companion name | Tampering | `BoundCompanionName()` trims + caps to 200 chars. Do not HTML-encode inside the prompt (prompt text is plain text passed to AI, not rendered as HTML). |
| Cache poisoning via enriched commanderName | Tampering | Cache key uses pre-Scryfall first commander name (unchanged from today). Enrichment does NOT affect the cache key, so a crafted deck cannot plant a malicious cache entry. |
| CompanionName reflected into artifact ZIP | Information Disclosure | The artifact ZIP (analysis prompt text) is user-generated content — same trust level as the deck source. No additional risk introduced. |

---

## Sources

### Primary (HIGH confidence)

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — full read, all key lines cited
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` — full read
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs` — full read
- `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` — full read
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` — full read
- `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs` — full read
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — lines 290-475 read
- `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` — full read
- `DeckFlow.Core/Loading/DeckEntryLoader.cs` — lines 1-60 read
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` — lines 85-165 read
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — lines 120-156 read
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs` — full read
- `DeckFlow.Web/Models/ManabaseRequest.cs` — full read
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — lines 190-260 read
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — lines 11-70 read
- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` — lines 851-910, 1463-1517 read
- `docs/decisions/0001-prompt-variants-decoupled.md` — full read
- `.planning/phases/72-command-zone-commander-castability/72-SPEC.md` — full read
- `.planning/STATE.md` — full read

### Secondary (MEDIUM confidence)

- `.planning/ROADMAP.md` — Phase 72/73 sections read; Phase 72 completion notes confirm
  fixture corrections (Background already command-zone, Archidekt companion dropped)
- `DeckFlow.Web/e2e/manabase-commander-callout.spec.ts` — lines 1-58 read (companion
  designator UI pattern)
- `DeckFlow.Web/e2e/deck-analysis-render.spec.ts` — full read (no companion/command-zone
  coverage — confirmed no new e2e needed for Phase 73)

---

## Metadata

**Confidence breakdown:**
- DeckAnalysisPacketService plumbing: HIGH — all key lines read and cited
- IAnalysisPromptVariant interface change: HIGH — full read, confirmed no other implementors
- Flag mechanism and four-file pattern: HIGH — confirmed from FeatureFlagStore + Phase 72 Plan 01
- Archidekt companion detection: HIGH — confirmed DetermineBoard code; no "Companion" case
- Moxfield Background on "commanders" board: MEDIUM — confirmed for Moxfield direct API; assumed for Background specifically (A2 in Assumptions Log)
- Cache key behavior with enriched commanderName: HIGH — confirmed from BuildDeckAnalysisCacheInputs

**Research date:** 2026-06-27
**Valid until:** 2026-07-27 (stable codebase; flag infrastructure and prompt variants are stable)
