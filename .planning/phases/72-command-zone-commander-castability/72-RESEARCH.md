# Phase 72: Command-Zone Modeling & Commander Castability — Research

**Researched:** 2026-06-27
**Domain:** DeckFlow manabase tool — companion/partner detection, castability simulation, display layer
**Confidence:** HIGH (all findings from direct codebase reads; one MEDIUM item flagged)

---

<user_constraints>
## User Constraints (from 72-SPEC.md)

### Locked Decisions

- **Scope fence:** SPEC sections A, B, C, D, F, G only. Section E (deck-analysis prompt
  command-zone awareness / `DeckAnalysisPacketService` prompt variants) is OUT OF SCOPE —
  moved to Phase 73.
- **Byte-identity guard (IS in scope):** When the flag is OFF, every byte of the manabase
  output AND the deck-analysis packet for the same deck must be identical to current
  production. No `DeckEntry.Board` remap of any kind; companion must travel as side
  metadata only.
- **Companion +3 tax is a HEURISTIC** tagged in code with `// HEURISTIC:`. Not a rules
  dispute; document it, ship it.
- **Companion NOT counted in `commanderCount`** for Karsten. It is never cast from the
  command zone; Karsten formula stays unchanged.
- **Background routing:** "Background" Archidekt category → route as a second commander
  (adds to `commanderCount`, gets its own castability row, `IsCommander = true`).
- **Partner commanders:** Both cards `IsCommander = true`. `commanderCount = 2` is already
  handled mathematically by `ManabaseClassifier.cs`.
- **Phase 71 coordination:** `ManabaseRampDrawBudget.DetermineThreshold()` already uses
  `.Max()` over commander ManaValues; no change needed to the budget calculator.
- **Display-only move-out:** Commander rows removed from the visible castability TABLE and
  from the displayed table-average, but `report.Castability` list and
  `report.AvgOnCurvePercent` are NOT mutated. Filtering in the Razor view via a local
  variable; report objects unchanged.
- **Flag:** `manabase.commander-castability`, seeded OFF (`FALSE` / `0`) in both Postgres
  and SQLite dialects.
- **Fallback path:** Moxfield Spellbook-fallback can only read `commanders` + `main`;
  companion unavailable on that path. Use manual designator text box as fallback UX.
- **CSS:** Layout CSS goes in `site-common.css`, token additions go in `:root` of each
  theme file.

### Claude's Discretion

- Exact HTML/CSS for the new commander callout block (above the castability table).
- Companion `SpellRequirement` construction detail (ManaValue adjustment vs. param extension).
- `ManabaseAnalysisResult` field naming for the companion callout data.
- Whether the companion callout is its own ViewModel field or folded into the report's
  `Castability` list with a separate flag.

### Deferred Ideas (OUT OF SCOPE)

- Section E: `DeckAnalysisPacketService` command-zone prompt variants — Phase 73.
- Companion-in-hand starting rule simulation (beyond the +3 heuristic).
- Visual badge for the partner split (e.g., WR icon per commander).
- Moxfield Spellbook-fallback auto-detection of companion (no data available there).
</user_constraints>

---

## Summary

Phase 72 extends the manabase tool to model Background/partner commanders as a pair and
display their individual castability in a dedicated callout, while also modeling a
companion's out-of-library +3-generic tax in the same callout. All changes are gated behind
`manabase.commander-castability` (OFF by default) and must produce byte-identical output
when the flag is OFF.

The core challenge is that three separate systems must stay untouched:

1. **`report.Castability` / `report.AvgOnCurvePercent`** — computed at the Core layer;
   display filtering happens only in Razor via a local variable.
2. **`DeckAnalysisPacketService` byte-identity** — the packet service filters by EXCLUSION
   (sideboard/maybeboard only); any new `DeckEntry.Board` value would be included as deck
   content. Companion must be carried as side metadata, never as a remapped board.
3. **`FeatureFlagCatalogTests` + `FeatureFlagStoreSeedTests`** — both test classes enforce
   that every seeded flag key has a catalog description; the new flag must be registered in
   all four places (seed SQL Postgres, seed SQL SQLite, `FeatureFlagCatalog.Descriptions`,
   both test `[InlineData]` lists) or CI fails.

**Primary recommendation:** Thread the companion as a `string? DetectedCompanionName` field
on `ManabaseAnalysisResult` (service layer), not as a mutated `DeckEntry`. All five data
reads (Moxfield direct, Moxfield Spellbook fallback, Archidekt, paste text, Moxfield-via-URL
designator) must handle the absence of a companion gracefully.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Companion auto-detection (Moxfield direct) | `DeckFlow.Core` importer | — | `MoxfieldApiDeckImporter.FetchDirectAsync` reads API boards; add companions board read |
| Background auto-detection (Archidekt) | `DeckFlow.Web` service | — | `ManabaseAnalysisService` detects via `DeckEntry.Category` after import |
| Companion side metadata thread | `DeckFlow.Web` service result | — | `ManabaseAnalysisResult` new field; never in `DeckEntry` |
| Companion castability row (flag ON) | `DeckFlow.Core` `ManabaseAnalyzer` | service caller | `SpellRequirement` with `ManaValue += 3`; separate `Simulate()` call |
| Partner headline determinism (worst-of) | `DeckFlow.Core` `ManabaseAnalyzer` | — | `SelectHeadlineSpell` must use `MinBy(CastPercent)` not `FirstOrDefault` |
| Commander callout display | Razor view | ViewModel | Filter in `castRows` local; callout iterates commander rows |
| Display-average (non-commander rows) | Razor view | `ManabaseDisplay` helper | `ManabaseDisplay.AvgOnCurve(castRows)` already accepts filtered list |
| Flag seeding | `FeatureFlagStore` | `FeatureFlagCatalog` | Existing pattern; must update both |
| Feature flag gate in analysis | `ManabaseAnalysisService` | — | `IsFlagOn(CommanderCastabilityFlagKey)` pattern |
| Phase 71 threshold (Phase 72: no-op) | `ManabaseRampDrawBudget` | — | `.Max()` already handles multiple commanders; no change needed |

---

## Standard Stack

No new packages. Phase 72 uses existing project libraries only.

### Core (existing — no change)
| Component | File | What Phase 72 Uses |
|-----------|------|--------------------|
| `CastabilitySimulator` | `DeckFlow.Core/Manabase/CastabilitySimulator.cs` | `Simulate(spell, turn, genericReduction)` — companion SpellRequirement only |
| `ManabaseAnalyzer` | `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` | `BuildCastability`, `SelectHeadlineSpell` — both need changes |
| `ManabaseClassifier` | `DeckFlow.Core/Manabase/ManabaseClassifier.cs` | `commanderCount` accumulation (partner = 2, already correct) |
| `ManabaseRampDrawBudget` | `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` | `DetermineThreshold` — already correct, no change |
| `MoxfieldApiDeckImporter` | `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` | `AddBoardEntries` + new `"companions"` board read |
| `ArchidektApiDeckImporter` | `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` | `DetermineBoard` — add "Background" → "commander" routing |
| `ManabaseAnalysisService` | `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` | `IsFlagOn`, `AnalyzedBoards`, `IsCommander` assignment |
| `FeatureFlagStore` | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | Seed new flag in both dialects |
| `FeatureFlagCatalog` | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` | Add description for new flag |

---

## Package Legitimacy Audit

No external packages added in Phase 72. Skip.

---

## Architecture Patterns

### System Architecture Diagram

```
Moxfield API v2
  "companions" board (direct path only)
        |
        v
MoxfieldApiDeckImporter.FetchDirectAsync
  AddBoardEntries("companions", "mainboard")  <-- board stays "mainboard"
  Side output: DetectedCompanionName (string?)
        |
Archidekt API
  category "Background" → DetermineBoard → "commander"   (new routing)
  category "Companion"  → DetermineBoard → "mainboard"   (unchanged)
  DeckEntry.Category="Companion" preserved for detection at service layer
        |
        v
ManabaseAnalysisService.AnalyzeAsync
  IsFlagOn("manabase.commander-castability")
  If ON:
    Detect companion from DeckEntry.Category OR DetectedCompanionName
    Companion → CompanionSpellRequirement { ManaValue = printed + 3 }   [HEURISTIC]
    Call CastabilitySimulator.Simulate(companionReq, turn, 0)
    → CardCastability row (IsCompanion = true, not IsCommander)
  Build ManabaseAnalysisResult { CommanderCastabilityEnabled=bool, CompanionRow=CardCastability? }
        |
        v
ManabaseController → ManabaseViewModel { ShowCommanderCastability, CompanionCallout }
        |
        v
Manabase.cshtml
  Flag OFF: castRows = report.Castability (all rows, including commanders in table)
  Flag ON:
    castRows = report.Castability.Where(c => !c.IsCommander && !c.IsCompanion).ToList()
    Commander callout block (above table):
      foreach row where IsCommander → display with star + per-turn probability
      if CompanionCallout != null → companion row with ◇ glyph + "+3 generic heuristic" note
    Table: castRows (spells only)
    Table avg: ManabaseDisplay.AvgOnCurve(castRows) (excludes commanders)
        |
DeckAnalysisPacketService
  Board filter at :165-169: EXCLUSION-BASED (excludes sideboard/maybeboard only)
  Companion Board="mainboard" → included as normal deck card   ← BYTE-IDENTICAL to current prod
  (No change to DeckAnalysisPacketService in Phase 72)
```

### Recommended Project Structure

No new files required. Changes touch existing files. New field goes on existing result record.

```
DeckFlow.Core/
├── Integration/
│   ├── MoxfieldApiDeckImporter.cs      # +companions board read, DetectedCompanionName output
│   └── ArchidektApiDeckImporter.cs     # +Background → commander routing
├── Manabase/
│   ├── ManabaseAnalyzer.cs             # SelectHeadlineSpell worst-of fix
│   └── ManabaseModels.cs               # (read-only in Phase 72 — report.Castability unchanged)
DeckFlow.Web/
├── Services/
│   ├── Manabase/
│   │   └── ManabaseAnalysisService.cs  # flag gate, companion detection, companion row build
│   └── FeatureFlags/
│       ├── FeatureFlagStore.cs         # seed new flag
│       └── FeatureFlagCatalog.cs       # add description
├── Models/
│   ├── ManabaseViewModel.cs            # +ShowCommanderCastability, +CompanionCallout
│   └── ManabaseAnalysisResult.cs       # +CommanderCastabilityEnabled, +CompanionRow
├── Views/Deck/
│   └── Manabase.cshtml                 # callout section, castRows filter, table loop
└── wwwroot/css/
    └── site-common.css                 # callout layout CSS (NOT site.css)
```

### Pattern 1: Flag-Gated Feature with Byte-Identity Guard

Existing pattern from Phase 71 (plain-language verdict):

```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (lines 269-271)
private bool IsFlagOn(string key)
    => flags.Snapshot().TryGetValue(key, out bool enabled)
    && enabled;
```

Fail-safe OFF: absent key returns false (same behavior as explicit false). Phase 72
adds:

```csharp
public const string CommanderCastabilityFlagKey = "manabase.commander-castability";

// In AnalyzeAsync:
bool commanderCastabilityOn = IsFlagOn(CommanderCastabilityFlagKey);
```

### Pattern 2: Companion +3 Generic Tax (HEURISTIC)

The companion castability is modeled as a single simulation call with `ManaValue` pre-adjusted.
`genericReduction` in `Simulate()` shaves off generic cost; there is no "add generic" path
(the `Math.Max(0, genericReduction)` clamp makes negative reduction a no-op). Adjust
`ManaValue` directly on the `SpellRequirement` instead:

```csharp
// Source: DeckFlow.Core/Manabase/CastabilitySimulator.cs lines 178-187 (verified)
// effectiveGeneric = Math.Max(0, (ManaValue - totalPips) - Math.Max(0, genericReduction))
// genericReduction = -3 would be clamped to 0 by Math.Max — DOES NOT add cost.
// Correct approach: build the companion SpellRequirement with ManaValue already +3.

var companionReq = new SpellRequirement(
    name: companionName,
    manaValue: printedMv + 3,   // HEURISTIC: +3 generic "to hand" tax
    pips: printedPips,
    isCommander: false,
    importance: SpellImportance.Companion);  // if importance enum is extended, else Normal
```

The library used for simulation is built from `deck.Sources` (which comes from the 99 + lands);
the companion has no `ManaSource` entry and therefore is naturally absent from the library.
Call `Simulate()` on the companion `SpellRequirement` against the same library that was
built for the main deck — no `librarySize` adjustment needed.

### Pattern 3: Display-Only Filter (Razor Layer)

```razor
@* Source: Manabase.cshtml line 164 — today assigns all rows to castRows *@
@{
    var castRows = Model.ShowCommanderCastability
        ? report.Castability.Where(c => !c.IsCommander).ToList()
        : (IReadOnlyList<CardCastability>)report.Castability;
}
@* Right-lens avg at line 206 uses castRows — automatically excludes commanders *@
@* Table at line 430 must be changed from report.Castability to castRows *@
```

Commander callout placement (between budget block and castability table heading):

```razor
@* After manabase-rampdraw block (~line 290), before manabase-castability-heading (~line 415) *@
@if (Model.ShowCommanderCastability && report.Castability.Any(c => c.IsCommander))
{
    <section class="manabase-cmd-castability">
        <h3>Command-zone castability</h3>
        @foreach (var cmd in report.Castability.Where(c => c.IsCommander))
        {
            @* row with ★ glyph, per-turn probability *@
        }
        @if (Model.CompanionCallout is { } comp)
        {
            @* companion row with ◇ glyph, +3 heuristic note *@
        }
    </section>
}
```

### Pattern 4: Moxfield "companions" Board Read

```csharp
// Source: DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs line 95-98 (verified)
// Today only reads 4 boards. Add the 5th:
AddBoardEntries(root, "commanders", "commander", authorTags, entries);
AddBoardEntries(root, "mainboard", "mainboard", authorTags, entries);
AddBoardEntries(root, "maybeboard", "maybeboard", authorTags, entries);
AddBoardEntries(root, "sideboard", "sideboard", authorTags, entries);
AddBoardEntries(root, "companions", "mainboard", authorTags, entries);  // NEW — board stays "mainboard"
// After the call, extract companion name separately:
string? detectedCompanion = root.TryGetProperty("companions", out var compBoard) && compBoard.ValueKind == JsonValueKind.Object
    ? compBoard.EnumerateObject().FirstOrDefault().Value.GetProperty("card").GetProperty("name").GetString()
    : null;
```

CRITICAL: Board for companions entries MUST be `"mainboard"`, not `"companion"`. The
`DeckAnalysisPacketService` at lines 165-169 uses inclusion-by-exclusion: only `maybeboard`
and `sideboard` are excluded. A new board value would leak into deck-analysis content.

### Pattern 5: Archidekt Background Category Routing

```csharp
// Source: DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs lines 126-143 (verified)
// Today: only "Commander", "Maybeboard", "Sideboard" are recognized.
// Add "Background" as a second commander category:
if (categories.Any(c => string.Equals(c, "Commander", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(c, "Background", StringComparison.OrdinalIgnoreCase)))
    return "commander";

// IsBoardCategory: add "Background" to keep entries from appearing as user categories.
// "Companion" stays as mainboard (it IS in the 99; companion zone is Archidekt display only).
```

`IsBoardCategory` at lines 150-154 must also include "Background" so the card's category
string is not treated as a deckbuilder category on the manabase service's `IsCategory` check.

### Pattern 6: SelectHeadlineSpell Worst-Of Fix

```csharp
// Source: DeckFlow.Core/Manabase/ManabaseAnalyzer.cs SelectHeadlineSpell lines 854-875 (verified)
// Today:
CardCastability? commander = castability.FirstOrDefault(c => c.IsCommander);
// Non-deterministic with two commanders. Fix:
CardCastability? commander = castability
    .Where(c => c.IsCommander)
    .MinBy(c => c.CastPercent);   // worst-of among all command-zone partners
```

### Pattern 7: Flag Seeding (four-file synchronized change)

```csharp
// File 1: FeatureFlagStore.cs — Postgres seed SQL
("manabase.commander-castability", FALSE)  ON CONFLICT (key) DO NOTHING

// File 2: FeatureFlagStore.cs — SQLite seed SQL
("manabase.commander-castability", 0)

// File 3: FeatureFlagCatalog.cs — operator description
["manabase.commander-castability"] =
    "Show command-zone castability: individual cast probability for each commander and " +
    "(Casual only) a companion's on-curve chance including the +3 generic rule tax. " +
    "Off by default.",

// File 4a: FeatureFlagCatalogTests.cs — add InlineData
[InlineData("manabase.commander-castability")]

// File 4b: FeatureFlagStoreSeedTests.cs — add InlineData
[InlineData("manabase.commander-castability", false)]
```

Miss any one of these four → CI fails.

### Anti-Patterns to Avoid

- **Remapping `DeckEntry.Board` to `"companion"`:** `DeckAnalysisPacketService` lines 165-169
  filter by EXCLUSION (`!= maybeboard && != sideboard`). Any new board value is treated as
  deck content. NEVER add "companion" as a board value.
- **Mutating `report.Castability` to remove commanders:** `report.AvgOnCurvePercent` (lines
  799-825 of `ManabaseModels.cs`) is a computed property over the list. Removing rows changes
  the health metric and breaks byte-identity for the `.txt` download. Filter in Razor only.
- **Using `genericReduction = -3` to model companion tax:** `Math.Max(0, genericReduction)`
  clamps negative values to 0. The +3 has no effect via that path. Adjust `ManaValue` on
  the `SpellRequirement` instead.
- **`FirstOrDefault(c => c.IsCommander)` for multi-commander headline:** Non-deterministic
  ordering. Use `MinBy(c => c.CastPercent)` (worst-of) for a deterministic and
  user-meaningful result.
- **Counting companion in `commanderCount`:** Companion is never cast from the command zone;
  Karsten formula must exclude it. `commanderCount += card.Quantity` (lines 109-111 of
  `ManabaseClassifier.cs`) must only increment for `IsCommander` cards.
- **Putting layout CSS in `site.css`:** CLAUDE.md constraint. `site.css` is per-theme;
  layout goes in `site-common.css`. Token additions go in `:root` of each theme file.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Companion cast probability under +3 tax | Custom simulator with "extra_cost" param | `SpellRequirement` with `ManaValue = printed + 3`, pass to existing `Simulate()` | `Simulate()` already handles arbitrary MV; no simulator extension needed |
| Multi-commander worst-of | Custom sort + pick | `MinBy(c => c.CastPercent)` on commander rows | LINQ, one line, deterministic |
| Flag-gated display | New flag infrastructure | Existing `IsFlagOn(key)` + existing seed/catalog pattern | Already tested; 4-file sync is the contract |
| Companion detection from Moxfield | New import abstraction | `AddBoardEntries(..., "companions", "mainboard")` + name extraction inline | Importer already parses JSON via the same property-walk; extend in-place |

---

## Common Pitfalls

### Pitfall 1: DeckAnalysisPacketService Board Filter Is Exclusion-Based
**What goes wrong:** Developer adds `"companion"` as a `DeckEntry.Board` to cleanly separate
companion cards. Companion shows up in deck-analysis prompts as a normal deck card. Flag-ON
byte-identity test passes but real decks break deck-analysis output.

**Why it happens:** `DeckAnalysisPacketService` at lines 165-169 and 409-418 uses
`!= "maybeboard" && != "sideboard"` — an allowlist would catch this but exclusion does not.

**How to avoid:** Never remap Board. Keep companion `Board = "mainboard"`. Carry companion
identity as a side-metadata string field on the result/ViewModel.

**Warning signs:** Any `DeckEntry.Board` value other than mainboard/commander/maybeboard/
sideboard in the importer output.

### Pitfall 2: AvgOnCurvePercent Mutation
**What goes wrong:** Developer removes commander rows from `report.Castability` to make the
table show only spells. `AvgOnCurvePercent`, health rating, and the `.txt` download all
silently change. Flag-OFF byte-identity test fails.

**Why it happens:** `AvgOnCurvePercent` is a computed property at `ManabaseModels.cs:799-825`
iterating `Castability` directly. It is not a stored value.

**How to avoid:** `report.Castability` is read-only after `BuildCastability` returns.
All filtering is in the Razor view via `castRows` local variable. Never call `.Remove()` or
reassign the list on the report.

**Warning signs:** `report.Castability.Count` changes after `BuildCastability` returns.

### Pitfall 3: Four-File Flag Registration Sync
**What goes wrong:** Developer adds flag seed SQL and the new `const string` key but forgets
`FeatureFlagCatalog.Descriptions` or one of the two test `[InlineData]` entries. CI fails
on `FeatureFlagCatalogTests` or `FeatureFlagStoreSeedTests`.

**Why it happens:** The consistency is enforced by test, not by compilation.

**How to avoid:** The four-file checklist: (1) seed Postgres, (2) seed SQLite, (3) catalog
description, (4a) `FeatureFlagCatalogTests` InlineData, (4b) `FeatureFlagStoreSeedTests`
InlineData. Treat as an atomic change.

**Warning signs:** CI red on `FeatureFlagCatalogTests.Describe_returns_nonempty_for_seeded_key`.

### Pitfall 4: genericReduction = -3 Silent No-Op
**What goes wrong:** Developer passes `genericReduction = -3` to `Simulate()` expecting it
to add 3 generic mana to the cast cost. The simulation runs silently with no extra cost.
Companion cast probability is over-estimated.

**Why it happens:** `effectiveGeneric = Math.Max(0, printedGeneric - Math.Max(0, genericReduction))`
— the inner `Math.Max(0, -3)` = 0, so `effectiveGeneric = printedGeneric`. No effect.

**How to avoid:** Build the companion's `SpellRequirement` with `ManaValue = printedMv + 3`
before calling `Simulate()`.

**Warning signs:** Companion's `CastPercent` equals its unmodified cast probability (e.g.,
same as if the companion were a normal spell of the same printed MV).

### Pitfall 5: SelectHeadlineSpell Non-Determinism Under Partners
**What goes wrong:** With two commanders, `FirstOrDefault(c => c.IsCommander)` returns
whichever commander appears first in the list. The "headline" on the health section depends
on list ordering, which is not guaranteed. A partner pair might show the cheaper commander
as the headline, masking the harder-to-cast partner.

**Why it happens:** `BuildCastability` at `ManabaseAnalyzer.cs` pins commanders first via
`OrderByDescending(r => r.IsCommander).ThenBy(r => r.IsCommander ? 0 : r.CastPercent)` —
secondary sort for commanders is all `0`, so their relative order is undefined.

**How to avoid:** Use `MinBy(c => c.CastPercent)` across commander rows in
`SelectHeadlineSpell`. This gives worst-of (most concerning), which is also more useful
to the user.

### Pitfall 6: Spellbook Fallback Has No Companion Data
**What goes wrong:** Plan assumes companion auto-detection works on all Moxfield decks. The
Spellbook fallback path at `FetchViaCommanderSpellbookAsync` reads only `commanders` + `main`
boards — the `companions` board is not accessible.

**Why it happens:** `FetchViaCommanderSpellbookAsync` (lines 103 onward) does not call the
Moxfield v2 deck API; it reconstructs from Spellbook data.

**How to avoid:** Detection from the companions board only works on the `FetchDirectAsync`
path. The plan must include a manual designator text box on the manabase form so users can
name a companion when the fallback path is used.

### Pitfall 7: Archidekt "Companion" Category Routing
**What goes wrong:** Developer routes Archidekt "Companion" category → `"companion"` board
(mirroring the Background → commander logic). Companion leaks into deck-analysis as deck
content (same as Pitfall 1).

**Why it happens:** Symmetry with Background routing; developer treats both as "special zone".

**How to avoid:** Background → commander (it casts from the command zone). Companion →
mainboard stays (it is in the 99-card registered deck; only the play restriction differs).
Companion identity must be inferred from `DeckEntry.Category` at the manabase service layer,
not from board routing.

---

## Code Examples

### Flag Check (Verified)
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs lines 269-271
private bool IsFlagOn(string key)
    => flags.Snapshot().TryGetValue(key, out bool enabled)
    && enabled;

// New constant alongside existing ones:
public const string CommanderCastabilityFlagKey = "manabase.commander-castability";
```

### Moxfield Board Read Extension (Verified entry point)
```csharp
// Source: DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs lines 95-98
// Current (read 4 boards):
AddBoardEntries(root, "commanders", "commander", authorTags, entries);
AddBoardEntries(root, "mainboard", "mainboard", authorTags, entries);
AddBoardEntries(root, "maybeboard", "maybeboard", authorTags, entries);
AddBoardEntries(root, "sideboard", "sideboard", authorTags, entries);

// Phase 72 addition (companions board → mainboard, side-detect name separately):
AddBoardEntries(root, "companions", "mainboard", authorTags, entries);
```

### Archidekt Background Routing (Verified entry point)
```csharp
// Source: DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs lines 126-143
// Current DetermineBoard — add Background case:
if (categories.Any(c =>
    string.Equals(c, "Commander", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(c, "Background", StringComparison.OrdinalIgnoreCase)))
    return "commander";
```

### AvgOnCurvePercent (READ-ONLY — verified it is computed property)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseModels.cs lines 799-825
// DO NOT mutate report.Castability — this property iterates the full list.
public int AvgOnCurvePercent
{
    get
    {
        if (Castability.Count == 0) return 0;
        long sum = 0;
        foreach (CardCastability row in Castability) sum += row.CastPercent;
        return (int)Math.Round((double)sum / Castability.Count);
    }
}
```

### Display Filter in Razor (Verified line 164)
```razor
@* Manabase.cshtml line 164 today: var castRows = report.Castability; *@
@{
    var castRows = Model.ShowCommanderCastability
        ? (IReadOnlyList<CardCastability>)report.Castability
              .Where(c => !c.IsCommander)
              .ToList()
        : report.Castability;
}
@* Line 430 table loop must change from report.Castability to castRows *@
@foreach (var c in castRows) { ... }
```

### SelectHeadlineSpell Determinism Fix (Verified line 854)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseAnalyzer.cs lines 854-875
// Current (non-deterministic with multiple commanders):
CardCastability? commander = castability.FirstOrDefault(c => c.IsCommander);
// Phase 72 fix (worst-of):
CardCastability? commander = castability
    .Where(c => c.IsCommander)
    .MinBy(c => c.CastPercent);
```

### Byte-Identity Test Pattern (Verified structure)
```csharp
// Source: DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs line 372
// Existing Phase 71 pattern to mirror:
[Fact]
public async Task AnalyzeAsync_PlainLanguageFlagOff_LeavesResultNullAndPromptByteIdentical()

// Phase 72 needs three byte-identity tests:
// 1. Flag OFF → manabase output identical (no callout, no commander-row removal)
// 2. Flag OFF → deck-analysis bytes identical (companion stays mainboard, packet unchanged)
// 3. Flag ON → deck-analysis bytes identical (companion metadata inert to DeckAnalysisPacketService)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No command-zone modeling in manabase | Phase 72 adds per-commander castability rows + companion callout | Phase 72 | Manabase tool surfaces commander-specific cast probability |
| `SelectHeadlineSpell` single commander assumed | `MinBy(CastPercent)` across commander rows | Phase 72 | Deterministic worst-of headline for partner pairs |
| Archidekt "Background" → mainboard | Archidekt "Background" → "commander" | Phase 72 | Background commanders counted in commanderCount + shown in callout |
| Moxfield "companions" board silently dropped | Companions board read; board="mainboard"; name extracted as side metadata | Phase 72 | Companion detected from Moxfield direct path; modeled with +3 tax |

**Not changing:**
- `report.Castability` population logic or `AvgOnCurvePercent`
- `ManabaseRampDrawBudget.DetermineThreshold` (`.Max()` already handles multiple commanders)
- `commanderCount` accumulation in `ManabaseClassifier` (partner = 2 already correct)
- `DeckAnalysisPacketService` board filter (Phase 73)

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Moxfield API v2 `"companions"` board shape is identical to `"commanders"` board (dict of `{cardId: {quantity, boardType, card: {name, set, cn, ...}}}`) | Pattern 4 | `AddBoardEntries` fails to parse; companion not detected; no functional regression but feature silently absent |
| A2 | Archidekt category string for companion is exactly `"Companion"` (matched OrdinalIgnoreCase) | Pattern 5 | Companion detection misses Archidekt companion; fallback to manual designator UX |
| A3 | Archidekt category string for Background is exactly `"Background"` (matched OrdinalIgnoreCase) | Pattern 5 | Background not routed to commander board; appears as mainboard spell in manabase |
| A4 | Companion callout is only shown in Casual mode (matching existing `ShowCastability` guard on Casual-only) | Architecture Patterns | Companion callout visible in cEDH mode where it is less relevant |

**A1 note:** `[ASSUMED]` from third-party Moxfield Crystal library wrapper confirming `companions` board exists. The JSON shape was inferred from the wrapper's field access patterns; not confirmed from Moxfield's official API documentation (which is not publicly published). Planner should add a Wave 0 task: create a Moxfield JSON fixture with a companion-deck snapshot before implementing the board read.

**A2/A3 note:** `[ASSUMED]` — Archidekt does not publish its internal category string list. The `DetermineBoard` method at lines 126-143 handles "Commander", "Maybeboard", "Sideboard" with their exact casing (OrdinalIgnoreCase comparison). "Background" and "Companion" are inferred from Archidekt UI conventions. Planner should add a verification step: capture a real Archidekt companion/background deck JSON response to confirm exact category string values before the importer change ships.

---

## Open Questions (RESOLVED in PLAN)

> All four resolved during planning (2026-06-27): Q1/Q2 → Wave-0 [BLOCKING] fixture probes in plan 72-02;
> Q3 → plan 72-06 Task 1 gates the WHOLE command-zone callout (commanders + companion) to Casual via
> `&& Model.ShowCastability` (simpler than a split-mode rule and matches the per-card table's visibility);
> Q4 → companion kept OUT of `report.Castability`, returned as a separate `CompanionRow` field (plan 72-05).

1. **Moxfield companion board exact JSON structure** — RESOLVED: Wave-0 task 72-02 captures the fixture.
   - What we know: Third-party Crystal wrapper confirms a `"companions"` key exists at the same level as `"commanders"` in the Moxfield API v2 deck response. Same property-access pattern as other boards.
   - What's unclear: Whether the `boardType` field differs (e.g., `"companion"` vs `"companions"`), whether `card` sub-object has any extra fields, whether a deck with no companion has an absent key or empty object.
   - Recommendation: Wave 0 task — capture a real Moxfield companion deck JSON (e.g., a Yorion deck) and create a test fixture. `AddBoardEntries` gracefully handles absent properties (returns 0 entries), so this is low-risk even if the key is absent; but the fixture is needed for the xUnit test.

2. **Archidekt companion/background category strings**
   - What we know: `DetermineBoard` uses OrdinalIgnoreCase; "Commander" is confirmed. The surrounding code suggests categories are user-facing label strings from the Archidekt editor.
   - What's unclear: Whether Archidekt emits "Background" or "Partner Commander" or similar for the choose-a-background mechanic.
   - Recommendation: Wave 0 task — API probe a known Background deck (e.g., Raised by Giants) and log the raw category strings.

3. **Companion callout in cEDH mode**
   - What we know: SPEC sections show callout primarily in Casual context (companion mechanic is less common in cEDH). Existing `ShowCastability` is Casual-only (`ManabaseMode.Casual`).
   - What's unclear: Whether the commander castability callout (partners, not companion) should also show in cEDH.
   - Recommendation: For initial implementation, gate companion-specific callout behind Casual mode (`ShowCastability`). Commander-pair castability callout shows in both modes (partners are common in cEDH). Revisit in SPEC confirmation with user if needed.

4. **`SpellImportance` enum extension**
   - What we know: `SpellImportance` is used by `SelectHeadlineSpell` to weight the headline. Adding a `Companion` importance variant could suppress the companion from becoming the headline.
   - What's unclear: Whether the companion row should be in `report.Castability` at all, or only in the callout via a separate `CompanionRow` field.
   - Recommendation: Keep companion row OUT of `report.Castability` (it is not a deck spell). Return it as a separate `CardCastability? CompanionRow` on `ManabaseAnalysisResult`. This avoids any need to extend `SpellImportance` or filter the companion from the main table logic.

---

## Environment Availability

Phase 72 is code-only changes (Core + Web + Razor + CSS). No external tools required beyond
the normal build pipeline.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build | Verified (CI green) | 10.0 | — |
| Node.js + tsc | TypeScript compile | Verified (CI green) | Node 20 | — |
| Playwright (WSL) | e2e tests | Verified (scripts/run-web-test.sh) | Current | Manual browser verify |
| Moxfield API (live) | Companion fixture creation | Available (public) | v2 | Hand-craft JSON fixture from docs |

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (SDK default) |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter "Category=Manabase"` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` |

### Phase Requirements → Test Map

| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| B-01 | `ManabaseClassifier` partner pair → commanderCount=2 | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests"` | ✅ — add test method |
| B-02 | `ManabaseClassifier` Background → commanderCount=2 | unit | same | ✅ — add test method |
| B-03 | Companion NOT in commanderCount | unit | same | ✅ — add test method |
| C-01 | Companion `SpellRequirement` with `ManaValue = printed + 3` → correct CastPercent | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CastabilitySimulatorTests"` | ✅ — add test method |
| C-02 | Companion library size unchanged (not in 99) | unit | same | ✅ — add test method |
| D-01 | `SelectHeadlineSpell` worst-of with 2 commanders | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseAnalyzerTests"` | ✅ — add test method |
| D-02 | `report.Castability` unchanged when flag ON (no mutation) | unit | same | ✅ — add test method |
| F-01 | Flag OFF → manabase result byte-identical to current prod | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ManabaseAnalysisServiceTests"` | ✅ — add test method |
| F-02 | Flag OFF → deck-analysis bytes identical (companion as mainboard) | unit | same | ✅ — add test method |
| F-03 | Flag ON → deck-analysis bytes identical (companion side metadata inert) | unit | same | ✅ — add test method |
| G-01 | Commander callout renders above castability table (flag ON, Casual) | e2e | `DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-commander-callout` | ❌ Wave 0 |
| G-02 | Commander rows absent from castability table (flag ON) | e2e | same | ❌ Wave 0 |
| G-03 | Table avg excludes commanders (flag ON) | e2e | same | ❌ Wave 0 |
| G-04 | Companion row shown in callout with +3 note (flag ON) | e2e | same | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests`
- **Per wave merge:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln`
- **Phase gate:** Full suite green (Core + Web + e2e smoke) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web/e2e/manabase-commander-callout.spec.ts` — new live-only e2e spec (REQ G-01 through G-04)
- [ ] Moxfield companion board JSON fixture (needed for xUnit test of `MoxfieldApiDeckImporter` with companion board)
- [ ] Archidekt Background deck JSON fixture (needed for xUnit test of `ArchidektApiDeckImporter` Background routing)

---

## Security Domain

`security_enforcement` not explicitly false in config. Covering applicable ASVS categories.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | n/a |
| V3 Session Management | No | n/a |
| V4 Access Control | No | n/a |
| V5 Input Validation | Yes | companion name is a string extracted from Moxfield API JSON — must be length-bounded and HTML-encoded in Razor (`@comp.Name`) |
| V6 Cryptography | No | n/a |

### Known Threat Patterns for This Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Companion name from API rendered without encoding | Tampering / XSS | Razor `@` encoding is on by default; do NOT use `@Html.Raw` for companion name |
| Companion ManaValue from API used in arithmetic | Tampering | Clamp: `ManaValue = Math.Max(0, Math.Min(20, printedMv)) + 3`; never allow unbounded arithmetic |
| Large `"companions"` board in adversarial JSON | DoS | `AddBoardEntries` takes first companion card only; already bounded by existing loop for commanders board |

---

## Sources

### Primary (HIGH confidence — direct codebase reads)
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` — board read pattern, FetchDirectAsync, AddBoardEntries
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — DetermineBoard, IsBoardCategory (lines 126-154)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — Simulate() effectiveGeneric formula (lines 178-187)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — AvgOnCurvePercent computed property (lines 799-825)
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — SelectHeadlineSpell (lines 854-875), BuildCastability
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — commanderCount accumulation (lines 109-111)
- `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` — DetermineThreshold (lines 123-133)
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — IsFlagOn, AnalyzedBoards, IsCommander (lines 270, 114-115, 345)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed SQL patterns (Postgres + SQLite)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — description format
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — castRows (line 164), right-lens (line 206), table loop (line 430), commander row classes (lines 433-437)
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — existing fields, ShowCastability computed property
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — exclusion-based board filter (lines 165-169)
- `DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs` — byte-identity test pattern (line 372)
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — InlineData guard pattern
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — seed consistency InlineData

### Secondary (MEDIUM confidence)
- `github.com/spoved/moxfield.cr` (Crystal library) — confirms `companions` board exists in Moxfield API v2 response; same property-access pattern as other boards. Not official documentation.

### Tertiary (LOW confidence — N/A)
No LOW confidence claims needed for planning.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all existing project components; no new packages
- Architecture: HIGH — all patterns verified from direct code reads
- Pitfalls: HIGH — all rooted in verified code paths
- Moxfield companion board shape: MEDIUM — confirmed board exists from unofficial wrapper; exact JSON schema not from official docs

**Research date:** 2026-06-27
**Valid until:** 2026-07-25 (stable codebase; `MoxfieldApiDeckImporter` / Moxfield API shape could change if Moxfield updates their API)
