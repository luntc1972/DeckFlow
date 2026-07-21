# Phase 105: Builder-Compatible Export - Research

**Researched:** 2026-07-21
**Domain:** ASP.NET Core MVC / DeckFlow.Core exporters, diff, banlist, color-identity, Cut Lab session state
**Confidence:** HIGH (all claims verified by direct file reads in this repo; no external libraries or docs needed)

## Summary

Phase 105 is almost entirely wiring. `DeckFlow.Core/Exporting/FullImportExporter.cs` and `DeltaExporter.cs` already
support both `"Moxfield"` and `"Archidekt"` `targetSystem` output (card-name slash formatting, category-tag vs
category-suffix formatting, board headers) — **no new Archidekt exporter class is needed**. `DiffEngine.Compare`
already produces exactly the add/cut patch shape (`ToAdd` / `OnlyInArchidekt` / `CountMismatch` / `PrintingConflicts`)
that criterion 2 asks for. `CommanderBanListService` already gates legality elsewhere in Cut Lab intake and can be
reused unchanged for a pre-export re-check.

The one real gap is data, not math: **Cut Lab's persisted `CutLabState.Pool` (`CutLabPoolCard`) does not carry
`Board`, `SetCode`, `CollectorNumber`, or `Category`**, and after the very first `Process` call, the controller
(`RehydrateIntakeRequestFromState`) **reconstructs `DeckText` from `Pool` (name + quantity only)** and resubmits that
synthetic text on every subsequent request instead of the user's original Moxfield/Archidekt URL or paste. That means
the full-fidelity original import (with printings, categories, and true board placement) is only available for the
single request in which it was originally loaded — it is not persisted anywhere afterward. This phase must add a
capture point for the original imported `DeckEntry` list (or at minimum a name+quantity+board snapshot) so criterion 2
("relative to their original builder list") has a stable baseline across the whole Cut Lab session, including after
saved-scenario reload.

Color-identity legality (part of criterion 3) also has a data gap: `ScryfallCardData` (the model Cut Lab already
resolves and caches for every pool card) has no `ColorIdentity` field, even though the underlying `ScryfallCard` DTO
does (`color_identity`). The commander-identity-subset check itself already exists as a private pattern in
`ScryfallSetService.IsPlayableInCommanderIdentity` — extend `ScryfallCardData`/`ScryfallCardDataMapper` to carry
`ColorIdentity` through (mirroring the existing `ProducedMana` field) and reuse the same subset-check logic, rather
than adding a new Scryfall call at export time.

**Primary recommendation:** Reuse `FullImportExporter.ToText` (final list, both formats) and `DiffEngine.Compare`
+ `DeltaExporter.ToText` (patch, adds/cuts) exactly as `DeckConvertService` already does; add one persisted
"original entries" snapshot to `CutLabState` at first intake (never overwritten by decisions/rehydration) as the
patch baseline; extend `ScryfallCardData` with `ColorIdentity`; add one new controller action (`/cut-lab/export`)
plus a `CutLabExportViewModel`/section following the existing `Decide`/`Goals`/`Whatif` action pattern.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Final-list text formatting (Moxfield/Archidekt) | API/Backend (`DeckFlow.Core.Exporting`) | — | Pure text formatting, already framework-free in Core |
| Add/cut diff computation | API/Backend (`DeckFlow.Core.Diffing`) | — | Pure comparison logic, already framework-free in Core |
| Original-list capture/persistence | API/Backend (`DeckFlow.Web.Services.CutLab` + `CutLabState`) | — | Session-state concern, mirrors existing `BaselineSnapshot` pattern |
| Color-identity + banlist validation | API/Backend (`DeckFlow.Web.Services`) | Database/Storage (Scryfall cache) | Both existing services already live in Web/Services and call out to cached Scryfall data |
| Export UI (buttons, copy-to-clipboard, textareas) | Browser/Client (`wwwroot/ts/cut-lab.ts`) | Frontend Server (Razor partial) | Matches existing Decide/Whatif/Goals pattern: Razor renders, TS handles copy/clipboard-only interactions |

## User Constraints

No `CONTEXT.md` exists yet for phase 105 (discuss-phase has not run). `ROADMAP.md`/`REQUIREMENTS.md` are the only
locked inputs; treat all implementation-shape questions (UI surface, hard-block vs warn, patch format) as open until
discuss-phase runs — see Open Questions below.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EXPORT-01 | User can export the finished 100-card list in Moxfield- and Archidekt-compatible text formats | `FullImportExporter.ToText(entries, [], MatchMode.Loose, targetSystem, null, CategorySyncMode.SourceTags)` — exact pattern already used by `DeckConvertService.ConvertAsync` (`DeckFlow.Web/Services/DeckConvertService.cs:106`) |
| EXPORT-02 | User can export an add/cut patch relative to the original builder list | `new DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries)` → `DeckDiff.OnlyInArchidekt` = cuts, `.ToAdd` = adds; render via `DeltaExporter.ToText(diff.ToAdd, targetSystem)` for adds and an equivalent call for cuts (gap — see Reuse Map) |
| EXPORT-03 | Validate exactly 100 cards, color-identity legal, banlist clean before export | Count: `CutLabRoundPlan.CardsRemainingToTarget == 0` already computed (`CutLabCutRoundEngine.cs:298`); banlist: `ICommanderBanListService.GetBannedCardsAsync` already used in `CutLabPageService.ResolveBannedCardsPresentAsync` (`CutLabPageService.cs:717-736`); color identity: gap, see below |

## Reuse Map

| Success criterion | Existing type/method to call | File:line | Gap (if any) |
|---|---|---|---|
| 1. Moxfield-format final list | `MoxfieldTextExporter.ToText(List<DeckEntry>)` OR `FullImportExporter.ToText(entries, [], MatchMode.Loose, "Moxfield", null, CategorySyncMode.SourceTags)` | `DeckFlow.Core/Exporting/MoxfieldTextExporter.cs:28`; `DeckFlow.Core/Exporting/FullImportExporter.cs:61` | None — prefer `FullImportExporter` for parity with the patch's target-system parameterization and because it already handles commander board placement/dedup (`FullImportExporter.cs:283-307`) |
| 1. Archidekt-format final list | `FullImportExporter.ToText(entries, [], MatchMode.Loose, "Archidekt", null, CategorySyncMode.SourceTags)` — same call, `targetSystem: "Archidekt"` | `DeckFlow.Core/Exporting/FullImportExporter.cs:61,161-186` (Archidekt slash-name + `[Commander,Category]` bracket suffix already implemented) | None — **no separate Archidekt exporter class exists or is needed**; confirmed no `ArchidektTextExporter`/`ArchidektExporter` file anywhere in repo (`find` returned nothing) |
| 2. Add/cut patch vs. original list | `new DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries)` → `DeckDiff` | `DeckFlow.Core/Diffing/DiffEngine.cs:27`; shape at `DeckFlow.Core/Models/DeckDiff.cs:6-10` | `DeltaExporter` only renders the **add** side (`toAdd` param, `DeckFlow.Core/Exporting/DeltaExporter.cs:16-46`). There is no existing "cut list" text renderer. Cheapest reuse: call `DeltaExporter.ToText(diff.OnlyInArchidekt, targetSystem)` too (same formatting logic, different input list) rather than writing new formatting code — confirm this renders acceptably as a "remove" list in discuss-phase, or add a one-line `"// Cuts"` / `"// Adds"` two-section wrapper around two `DeltaExporter.ToText` calls |
| 2. Patch baseline = "original builder list" | **Not currently persisted with full fidelity anywhere** — see "Patch Baseline Question" below | `DeckFlow.Web/Models/CutLab/CutLabState.cs` (no such field); `CutLabController.RehydrateIntakeRequestFromState` (`CutLabController.cs:304-346`) destroys board/set/collector/category fidelity after round 1 | **New field required**: persist original `List<DeckEntry>` (or a lighter `IReadOnlyList<CutLabOriginalEntry>` DTO with Name/Quantity/Board/SetCode/CollectorNumber/Category) on `CutLabState`, captured once at first successful `ProcessAsync`, never overwritten thereafter |
| 3. Exactly 100 cards | `CutLabRoundPlan.CardsRemainingToTarget` (0 = at target); underlying math `workingList.Sum(card => card.Quantity) - 100` | `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs:48,298` | None for the *count* check. Note this counts `Pool`-derived working list quantities, which already includes the commander (see `CutLabPageService.BuildState`, `CutLabPageService.cs:670-686`, and `CutLabWorkingList.Derive`) — 1 commander + 99 = 100 is already the invariant Cut Lab targets, matches criterion 1's "100-card list" |
| 3. Color-identity legal | Pattern exists but not wired to Cut Lab's already-resolved cards: `ScryfallSetService.IsPlayableInCommanderIdentity(ScryfallCard, IReadOnlySet<string>)` (private) at `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs:548-563`; commander-identity-lookup pattern in `DeckAnalysisPacketService.LookupCommanderColorIdentityAsync` (`DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1780-1799`, via `cards/collection` POST) | Both are **private methods on other classes**, not directly callable | **Gap**: `ScryfallCardData` (`DeckFlow.Core/Manabase/ScryfallCardData.cs:11-60`) has no `ColorIdentity` property, even though the raw `ScryfallCard` DTO does (`color_identity` at `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs:47`), and `ScryfallCardDataMapper.ToCardData` (`DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs:16-35`) does not copy it over. Since Cut Lab already resolves every pool card to `ScryfallCardData` and caches it (`CutLabResolvedCardCache`, `CutLabAnalysisContextBuilder`), the lowest-risk fix is: add `ColorIdentity` to `ScryfallCardData` + copy it in the mapper (mirrors existing `ProducedMana` handling), then write a small new static helper (`IsWithinCommanderIdentity` or similar) reusing the same subset-check logic as `ScryfallSetService.IsPlayableInCommanderIdentity` — no new Scryfall HTTP calls needed at export time because the cards are already resolved earlier in `CutLabPageService.ProcessAsync` |
| 3. Commander-banlist clean | `ICommanderBanListService.GetBannedCardsAsync(cancellationToken)` — exact same call already used at intake | `DeckFlow.Web/Services/CommanderBanListService.cs:18-19`; consumer pattern at `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:717-736` | None — call again against the final working list (not just the imported pool) right before export; the intake-time check only validated the *imported* pool, not the *post-cut* 100-card list, so a fresh check at export time is a genuinely new call site (same method, new call) |

## Patch Baseline Question

"Original builder list" (criterion 2) has exactly one candidate today, and it is fragile:

- `CutLabRequest.DeckSource` (`DeckUrl` or `DeckText`) is what `IDeckEntryLoader.LoadFromSourceAsync` parses into full
  `DeckEntry` objects (with `Board`, `SetCode`, `CollectorNumber`, `Category`) on **every** `ProcessAsync` call
  (`CutLabPageService.cs:172-215`).
- On the very first POST to `/cut-lab`, this is the user's real Moxfield/Archidekt URL or paste — full fidelity.
- On every subsequent POST (`Decide`, `Goals`, `Whatif`), `CutLabController.RehydrateIntakeRequestFromState`
  (`CutLabController.cs:304-346`) **overwrites** `request.DeckText`/`DeckUrl` with a synthetic
  `"Commander\n<qty> <name>\n\nDeck\n<qty> <name>..."` block built from `CutLabState.Pool` — which only carries
  `Name`, `Quantity`, `TypeLine`, `IsCommander`, `IsLocked`, `PackageId` (`CutLabState.cs:82-101`). Board is
  collapsed to just "commander" vs "not commander" (sideboard/maybeboard entries that were pulled in via
  `IncludeSideboard`/`IncludeMaybeboard` become indistinguishable mainboard lines), and `SetCode`/`CollectorNumber`/
  `Category` are dropped entirely.
- `CutLabState` has no field today that captures the pristine original import once and holds it immutably.

**Conclusion:** the planner must add a new persisted field to `CutLabState` (e.g. `OriginalEntries`) populated once,
on the first successful load (when `state.OriginalEntries` is empty/null and `priorState.Pool.Count == 0`), from the
full `DeckEntry` list `IDeckEntryLoader.LoadFromSourceAsync` returns — mirroring how `BaselineSnapshot` is already
captured once-and-preserved (`CutLabPageService.cs:315-337`, `if (state.BaselineSnapshot is null) { ... }`). This is
the same "capture once, never overwrite" pattern already in the codebase; no new architecture, just one more
first-capture field. Byte-size impact: `CutLabStateSerializer.MaxUploadBytes` is 262,144 bytes
(`CutLabStateSerializer.cs:10`) — a 150-card original-entries snapshot (name/qty/board/set/collector/category per
card) is small (~150 short JSON objects), should not meaningfully threaten the existing cap, but the planner should
verify against `MaxDecisions`/`MaxPackages` headroom already reserved.

## Architecture Patterns

### Recommended Project Structure
```
DeckFlow.Core/
├── Exporting/
│   ├── FullImportExporter.cs      # REUSE unchanged — final-list, both targetSystem values
│   └── DeltaExporter.cs           # REUSE unchanged — call twice (adds, cuts) or wrap
├── Diffing/
│   └── DiffEngine.cs              # REUSE unchanged — Compare(final, original)
DeckFlow.Web/
├── Models/CutLab/
│   └── CutLabState.cs             # ADD: OriginalEntries snapshot field (capture-once pattern)
├── Services/CutLab/
│   ├── CutLabPageService.cs       # ADD: capture OriginalEntries once; call banlist + color-identity + count checks pre-export
│   └── CutLabExportService.cs     # NEW (thin): orchestrates FullImportExporter + DiffEngine + DeltaExporter + validation, returns text blobs
├── Controllers/
│   └── CutLabController.cs        # ADD: [HttpPost("/cut-lab/export")] action, mirrors Decide/Goals/Whatif pattern
├── Models/
│   └── CutLabViewModel.cs         # ADD: export section fields (Moxfield text, Archidekt text, patch text, validation summary)
├── Views/Deck/
│   └── CutLab.cshtml              # ADD: export panel/section + copy-to-clipboard buttons (mirrors existing Decide/Whatif form blocks)
```

### System Architecture Diagram
```
[Cut Lab session: working list at 100 cards]
        |
        v
CutLabController.Export(request)  --(no new HTTP calls; cards already resolved earlier)
        |
        v
CutLabExportService.BuildExport(state)
        |-- 1. Reconstruct final 100-card DeckEntry list from state.Pool (kept cards)
        |         + state.OriginalEntries metadata (Board/SetCode/CollectorNumber/Category) by name match
        |-- 2. Validation gate:
        |     a. count == 100            -> CutLabRoundPlan.CardsRemainingToTarget == 0 (existing)
        |     b. banlist clean            -> ICommanderBanListService.GetBannedCardsAsync (existing, re-called)
        |     c. color-identity legal     -> NEW: ScryfallCardData.ColorIdentity subset-check vs commander identity
        |-- 3. Export text (both formats) -> FullImportExporter.ToText(final, [], Loose, "Moxfield"/"Archidekt", ...)
        |-- 4. Patch (adds/cuts)          -> DiffEngine.Compare(final, state.OriginalEntries) -> DeltaExporter.ToText(...)
        v
CutLabExportViewModel (validation summary + 4 text blobs: Moxfield full, Archidekt full, Moxfield patch, Archidekt patch)
        |
        v
Views/Deck/CutLab.cshtml export panel (copy-to-clipboard textareas, matches existing panel styling)
```

### Pattern 1: Full-list export via existing FullImportExporter (both target systems)
**What:** Feed the final kept-card `DeckEntry` list as `sourceEntries`, empty list as `targetEntries` (no merge target),
`MatchMode.Loose`, and the desired `targetSystem` string.
**When to use:** Criterion 1 (both Moxfield and Archidekt full-list export).
**Example (verified live pattern already in production code):**
```csharp
// Source: DeckFlow.Web/Services/DeckConvertService.cs:100-108
var targetSystem = isTargetArchidekt ? "Archidekt" : "Moxfield";
var text = FullImportExporter.ToText([.. entries], [], MatchMode.Loose, targetSystem, null, CategorySyncMode.SourceTags);
```
Call this twice for Phase 105 — once with `"Moxfield"`, once with `"Archidekt"` — using the same `finalEntries` list.

### Pattern 2: Add/cut patch via existing DiffEngine + DeltaExporter
**What:** `DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries)` treats the first argument as the
"wanted" list and the second as "what's currently there" — `ToAdd` = present in final but not original (should be
empty for a pure-cut workflow unless quantities changed), `OnlyInArchidekt` = present in original but not final
(**the cuts**), `CountMismatch` = quantity deltas.
**When to use:** Criterion 2.
**Example:**
```csharp
// Source: DeckFlow.Core/Diffing/DiffEngine.cs:27; DeckFlow.Core/Models/DeckDiff.cs:6-10
var diff = new DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries);
var addsText = DeltaExporter.ToText(diff.ToAdd.ToList(), targetSystem);
var cutsText = DeltaExporter.ToText(diff.OnlyInArchidekt.ToList(), targetSystem); // gap: confirm naming/format in discuss-phase
```

### Pattern 3: Capture-once persisted state field (mirrors BaselineSnapshot)
**What:** `CutLabState.BaselineSnapshot` is only computed when null and then carried forward unchanged forever.
**When to use:** For the new `OriginalEntries` field.
**Example:**
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabPageService.cs:315-337
if (state.BaselineSnapshot is null)
{
    // ... compute once ...
    state = state with { BaselineSnapshot = baselineSnapshot };
}
```

### Pattern 4: Re-check banlist against a card-name list (existing, reusable verbatim)
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabPageService.cs:717-736
IReadOnlyList<string> bannedCards = await _banListService.GetBannedCardsAsync(cancellationToken).ConfigureAwait(false);
var bannedSet = bannedCards.ToHashSet(StringComparer.OrdinalIgnoreCase);
var present = entries.Select(entry => entry.Name).Where(name => bannedSet.Contains(name))...
```

### Anti-Patterns to Avoid
- **Writing a new `ArchidektTextExporter` class:** unnecessary — `FullImportExporter`/`DeltaExporter` already branch
  on `targetSystem` string for all Archidekt-specific formatting (slash names, category bracket suffix). Adding a
  parallel class would duplicate and risk drifting from this logic.
- **Re-deriving the "original list" from `CutLabState.Pool` at export time:** `Pool` is the CURRENT (post-cut, in
  some cases post-restore) card set, not the original — using it as the diff baseline would make every patch empty
  or wrong. Use a dedicated captured-once snapshot instead.
- **New Scryfall HTTP call for color identity at export time:** cards are already resolved to `ScryfallCardData` and
  cached earlier in `CutLabPageService.ProcessAsync` (`CutLabResolvedCardCache`); extend the existing model instead
  of re-fetching.
- **Skipping `ScryfallThrottle`/`ResiliencePipelineProvider` if any new Scryfall calls turn out to be necessary**
  (e.g., if `OriginalEntries` needs re-resolution) — must use the same named pipeline (`"scryfall"`) and
  `ScryfallThrottle.ExecuteAsync` wrapper as every other Scryfall-calling service (`DeckConvertService.cs:51-58`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Moxfield/Archidekt text formatting | New per-format string builders | `FullImportExporter.ToText` / `MoxfieldTextExporter.ToText` (targetSystem param) | Already handles board headers, slash-card-name Archidekt convention, category tag vs bracket-suffix formatting, commander dedup |
| Add/cut diffing | New card-set comparison logic | `DiffEngine.Compare(final, original)` | Already handles loose/strict matching, commander fallback matching, printing conflicts, quantity deltas |
| Commander banlist check | New scrape/parse of mtgcommander.net | `ICommanderBanListService.GetBannedCardsAsync` | Already cached 6h, already used at Cut Lab intake |
| Card color identity lookup | New Scryfall API call at export time | Extend already-resolved+cached `ScryfallCardData` with `ColorIdentity` | Cards are already fetched and cached earlier in the same request pipeline; avoid a second round-trip and a second rate-limited Scryfall call |
| 100-card count validation | New count logic | `CutLabRoundPlan.CardsRemainingToTarget` (already 0 at target) | Already computed every `ProcessAsync` call |

**Key insight:** every piece of "export math" this phase needs (formatting, diffing, banlist) already exists and is
already proven by tests (`DeckFlow.Core.Tests/ExporterTests.cs`, `DeckFlow.Core.Tests/DiffEngineTests.cs`). The only
genuinely new logic is (a) the original-list capture-once field and (b) the color-identity subset check — both are
small, mechanical additions that mirror existing patterns in the same files.

## Common Pitfalls

### Pitfall 1: Treating `CutLabState.Pool` as the export baseline
**What goes wrong:** Diffing the final list against `Pool` instead of a captured original snapshot produces an
empty or nonsensical patch, since `Pool` already reflects the current (possibly restored/whatif-swapped) state, not
the user's actual builder list.
**Why it happens:** `Pool` is the only card list visible in `CutLabState` today; it is easy to assume it is "the
original."
**How to avoid:** Add and use the dedicated `OriginalEntries` capture-once field (see Reuse Map / Patch Baseline
Question).
**Warning signs:** Patch always empty, or patch reflects only the most recent decision instead of the whole session.

### Pitfall 2: Losing board/printing fidelity via `RehydrateIntakeRequestFromState`
**What goes wrong:** Any code path that re-derives "the deck" by reparsing `request.DeckText` after the first
request will get the lossy synthetic text (`CutLabController.BuildDeckText`, `CutLabController.cs:328-346`), not the
user's real export with printings/categories/sideboard placement.
**Why it happens:** This rehydration exists deliberately to keep round-trips small/stateless in URL/paste terms;
it's correct for Cut Lab's own mechanics (which only need name/qty/typeline) but wrong as an export/diff source.
**How to avoid:** Export logic must read from the captured-once `OriginalEntries` field, never from
`request.DeckText`/`DeckUrl` post-intake.

### Pitfall 3: Validating card count against the wrong list
**What goes wrong:** `CutLabPoolValidator.ValidateCardCount` validates the **imported pool** (101-150 cards,
non-commander) at intake — reusing it unchanged at export time would always fail, since the export-time working
list should be exactly 99 non-commander + 1 commander = 100.
**Why it happens:** Same class name ("validator"), different invariant.
**How to avoid:** Use `CutLabRoundPlan.CardsRemainingToTarget == 0` (or equivalent direct sum check on the derived
working list) for the export-time count gate, not `CutLabPoolValidator`.

### Pitfall 4: Color-identity check silently no-ops for un-resolved cards
**What goes wrong:** Cards whose Scryfall resolution failed (`ScryfallCardData? Card` is `null` — see
`CutLabPageService.ResolvedCutLabEntry`, `CutLabPageService.cs:745-750`) have no `ColorIdentity` to check. If the
new legality check simply skips nulls, an unresolved illegal card slips through export silently.
**Why it happens:** Cut Lab already tolerates unresolved cards gracefully elsewhere (empty `TypeLine`, warnings) —
easy to copy that "fail open" habit into the export gate where it's less safe.
**How to avoid:** Treat unresolved cards as a blocking warning at export time ("couldn't verify color identity for
X — check manually") rather than silently passing them as legal.

## Code Examples

### Existing full-list export call site (verbatim, to mirror)
```csharp
// Source: DeckFlow.Web/Services/DeckConvertService.cs:100-108
var targetSystem = isTargetArchidekt ? "Archidekt" : "Moxfield";
var text = FullImportExporter.ToText([.. entries], [], MatchMode.Loose, targetSystem, null, CategorySyncMode.SourceTags);
```

### Existing diff test shape to mirror for new export-diff tests
```csharp
// Source: DeckFlow.Core.Tests/DiffEngineTests.cs:14-24
var diff = new DiffEngine(MatchMode.Loose).Compare(moxfield, archidekt);
Assert.Empty(diff.ToAdd);
Assert.Single(diff.PrintingConflicts);
```

### Existing capture-once state pattern to mirror for OriginalEntries
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabPageService.cs:315-337
if (state.BaselineSnapshot is null)
{
    try
    {
        CutLabMetricSnapshot baselineSnapshot = await _simulationService.BuildSnapshot(...).ConfigureAwait(false);
        state = state with { BaselineSnapshot = baselineSnapshot };
    }
    catch (Exception exception) { /* warn, continue without it */ }
}
```

### Existing commander-identity subset check to mirror (currently private, needs extraction or duplication)
```csharp
// Source: DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs:548-563
private static bool IsPlayableInCommanderIdentity(ScryfallCard card, IReadOnlySet<string> commanderIdentity)
{
    var cardIdentity = (card.ColorIdentity ?? Array.Empty<string>())
        .Where(color => !string.IsNullOrWhiteSpace(color))
        .Select(color => color.Trim().ToUpperInvariant());
    foreach (var color in cardIdentity)
    {
        if (!commanderIdentity.Contains(color)) return false;
    }
    return true;
}
```

## State of the Art

Not applicable in the traditional sense (no external library/framework choice here) — this section instead flags
the one internal drift the phase must correct:

| Old approach (current Cut Lab behavior) | Needed approach (Phase 105) | When it matters | Impact |
|---|---|---|---|
| Session state (`CutLabState.Pool`) drops Board/SetCode/CollectorNumber/Category after first request | Persist a full-fidelity original-entries snapshot once at intake | Any export/diff/color-identity feature | Without this, criteria 2 and 3 (color identity) cannot be correctly implemented |
| `ScryfallCardData` omits `color_identity` | Add `ColorIdentity` to `ScryfallCardData` + mapper | Color-identity validation (criterion 3) | Without this, legality check requires a redundant Scryfall round-trip |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Rendering the "cuts" side of the patch by calling `DeltaExporter.ToText(diff.OnlyInArchidekt, targetSystem)` (same formatter, different input list) will read acceptably to a user as a "remove these cards" list, without a dedicated cut-specific header/format | Reuse Map (criterion 2), Pattern 2 | If the format reads ambiguously (e.g., a builder site's paste-import doesn't distinguish "add" vs "remove" text blocks), users could accidentally re-add cut cards instead of removing them; discuss-phase should confirm the exact patch presentation (e.g., "paste this into your builder's cut list" vs. two separate labeled blocks) |
| A2 | `CutLabRoundPlan.CardsRemainingToTarget == 0` is sufficient/correct as the "exactly 100" export gate (i.e., it already includes the commander in its count) | Reuse Map (criterion 3), Pitfall 3 | Verified by reading `CutLabPageService.BuildState` (pool includes commander) and `CutLabCutRoundEngine.cs:298`, but not verified end-to-end against a live 100-card session in this research pass — low risk, but planner should add/keep an explicit unit test asserting `Pool.Sum(Quantity) == 100` at export time |
| A3 | Extending `ScryfallCardData` with a `ColorIdentity` property is safe and won't break `ScryfallCardDataMapperTests` or downstream manabase consumers | Reuse Map (criterion 3), Don't Hand-Roll | Adding a nullable list property to a `sealed record` with `JsonPropertyName` is additive/non-breaking in this codebase's pattern (mirrors `ProducedMana`); risk is low but the planner should still run `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests/Manabase/ScryfallCardDataMapperTests.cs` after the change |

## Open Questions (RESOLVED)

> All four resolved at discuss-phase (2026-07-21). See `105-CONTEXT.md` for the locked
> decisions (D1–D4). Per-question resolution noted inline below.

1. **[RESOLVED — D1]** **UI surface: dedicated `/cut-lab/export` page/section, or an always-visible panel once at 100 cards?**
   - **Resolution:** New **"Export" step tab** in the Cut Lab step strip (`Process → Decide →
     Goals → Export`), unlocked only at exactly 100 cards, via a new `CutLabController.Export`
     action + section in `CutLab.cshtml`. (Chosen over the researcher's in-page-panel lean.)
     Covered by Plan 105-04.
   - What we know: Existing sub-flows (`Decide`, `Goals`, `Whatif`) are all sections within the single `CutLab.cshtml`
     view, gated by state (e.g., goals panel only meaningful once a pool is loaded). `_WorkflowStepTabs.cshtml`
     provides shared step-navigation chrome used across Deck tools.
   - What's unclear: Whether export should be its own step-tab destination, or an in-page panel that appears once
     `CardsRemainingToTarget == 0`.
   - Recommendation: Follow the existing in-page-panel pattern (like Whatif/Goals) rather than a new route, since
     Cut Lab is a single stateful session page today — but confirm with discuss-phase/UI-SPEC before locking.

2. **[RESOLVED — D2]** **Validation hard-block vs. warn-and-allow export?**
   - **Resolution:** Always show the 3-check summary; **hard-block the final-list copy ONLY
     when count ≠ 100**. Color-identity + banlist failures are prominent warnings that name the
     offending card(s) but do NOT disable export ("guide, don't gate"). Covered by Plan 105-03
     (`HardBlock = !CountOk`) + 105-04.
   - What we know: Existing Cut Lab intake treats banlist failures as soft warnings (`CutLabPageService.cs:255-260`,
     "Banned-card check unavailable... legality was not verified"), i.e., fail-open on service errors but presumably
     fail-closed (or at least warn loudly) when banned cards are *found*. Criterion 3 says "validated before export"
     which implies some blocking behavior.
   - What's unclear: Should export be fully blocked (button disabled) if count != 100, banlist-dirty, or
     color-identity-illegal, or should it be allowed with a prominent warning?
   - Recommendation: Discuss-phase should lock this explicitly; suggested default (consistent with rest of Cut Lab's
     "guide, don't gate" philosophy) is warn-and-allow with a clear visual flag, except possibly hard-block on the
     card-count mismatch (100 is the entire point of Cut Lab).

3. **[RESOLVED — D3]** **Patch format for a builder that has no explicit "remove" import syntax (e.g., Moxfield plain-text import merges
   by name, it doesn't diff)?**
   - **Resolution:** Readable **CUT (remove) / ADD (keep-or-added)** two-section patch, each line
     in builder syntax, rendered in **both** Moxfield and Archidekt dialects (two copy blocks) —
     manual paste-and-edit instructions, not a machine diff. Covered by Plan 105-03 + 105-04.
   - What we know: Moxfield/Archidekt's own paste-import UIs are additive/replace-all, not diff-aware — the "patch"
     is realistically instructions for the user to manually delete/add, not a machine-consumable diff format.
   - What's unclear: Exact expected format — plain "cards to remove" list + "cards to add" list as two labeled
     text blocks? A single combined block with +/- prefixes?
   - Recommendation: Confirm with the user in discuss-phase; `DeltaExporter`'s existing board-header convention
     (`// Commander`, `// Sideboard`) suggests a natural `// Cuts` / `// Adds` two-section convention using the same
     formatter twice.

4. **[RESOLVED — explicit test, Plan 105-01 Task 2]** **What happens to `OriginalEntries` on saved-scenario reload (Phase 104's scenario/localStorage feature)?**
   - **Resolution:** `OriginalEntries` is a capture-once field on the serialized `CutLabState`,
     so it must survive scenario reload. Plan 105-01 Task 2 adds an **explicit test** of the
     scenario-reload path (not an assumption) to confirm it persists and is not re-detected as
     empty on a subsequent `ProcessAsync`.
   - What we know: Phase 104 added named scenario save/reload via `localStorage` (JS-only), capturing goals/locks/
     intent. `CutLabState` is the server-side envelope round-tripped via hidden field.
   - What's unclear: Whether a reloaded scenario's `CutLabStateJson` will still carry the originally-captured
     `OriginalEntries` (it should, since it's just another field on the same serialized state), or whether scenario
     reload triggers a fresh `ProcessAsync` that could re-detect `OriginalEntries` as "already set" vs. "empty."
   - Recommendation: When implementing the capture-once field, explicitly test the scenario-reload path (not just
     fresh intake) to confirm `OriginalEntries` survives — add this as an explicit test case, not just an assumption.

## Environment Availability

No new external dependencies, tools, or services are introduced by this phase — all reused services
(`ICommanderBanListService`, Scryfall REST client, `IDeckEntryLoader`) are already registered and operational in
`DeckFlow.Web/Program.cs`. Skipped: no new environment probing needed.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| Config file | Standard SDK test project (`.csproj`), no custom xunit.runner.json found |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~Exporter\|FullyQualifiedName~DiffEngine` / `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLab` |
| Full suite command | `dotnet build` (per CLAUDE.md, VSTest is unreliable in WSL — rely on build-clean + targeted harness or CI) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EXPORT-01 | Final list exports correctly in Moxfield format | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~ExporterTests` (existing file, add new cases) | ✅ `DeckFlow.Core.Tests/ExporterTests.cs` |
| EXPORT-01 | Final list exports correctly in Archidekt format | unit | same file, new test cases (`targetSystem: "Archidekt"`) | ✅ existing file, new cases needed |
| EXPORT-02 | Diff/patch computed correctly against original entries | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~DiffEngineTests` (existing file, add cut-lab-shaped cases) | ✅ `DeckFlow.Core.Tests/DiffEngineTests.cs` |
| EXPORT-02 | OriginalEntries captured once and survives further decisions/reload | unit | new test in `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` or `CutLabStateSerializerTests.cs` | ❌ Wave 0 — new test needed |
| EXPORT-03 | Export blocked/flagged when count != 100 | unit | new test, likely in a new `CutLabExportServiceTests.cs` | ❌ Wave 0 |
| EXPORT-03 | Export blocked/flagged when banlist-dirty | unit | new test in same file, mirrors `CutLabPageServiceTests` banlist test pattern | ❌ Wave 0 |
| EXPORT-03 | Export blocked/flagged when color-identity illegal | unit | new test in same file + `ScryfallCardDataMapperTests.cs` (ColorIdentity mapping) | ❌ Wave 0 |
| EXPORT-01/02/03 UI | Export panel renders, copy buttons work, full/patch text visible at 100 cards | e2e (Playwright) | `npx --no-install playwright test e2e/cut-lab-export.spec.ts` | ❌ Wave 0 — new spec, mirror `e2e/cut-lab-whatif.spec.ts` structure |

### Sampling Rate
- **Per task commit:** targeted `dotnet test` filter for touched test classes (Core exporter/diff tests + new
  `CutLabExportServiceTests`)
- **Per wave merge:** full `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests` + new e2e spec
- **Phase gate:** Full suite green (unit + e2e, both themes/viewports per CLAUDE.md UI-change rule) before
  `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CutLabExportServiceTests.cs` (new) — covers EXPORT-01/02/03 validation gating
- [ ] New cases in `DeckFlow.Core.Tests/ExporterTests.cs` — Archidekt-target full-list export from a Cut-Lab-shaped
      entry list (with commander board)
- [ ] New cases in `DeckFlow.Core.Tests/DiffEngineTests.cs` — cut-only scenario (final ⊂ original, no adds)
- [ ] New case in `DeckFlow.Web.Tests/Manabase/ScryfallCardDataMapperTests.cs` — `ColorIdentity` copied from
      `ScryfallCard` to `ScryfallCardData`
- [ ] `DeckFlow.Web/e2e/cut-lab-export.spec.ts` (new) — mirror `cut-lab-whatif.spec.ts` structure/fixtures
- [ ] Framework install: none — xUnit and Playwright are already present and configured

## Security Domain

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Cut Lab has no auth surface (public tool) |
| V3 Session Management | Yes (indirectly) | `CutLabStateJson` hidden-field round-trip already size-capped (`CutLabStateSerializer.MaxUploadBytes`, 262,144 bytes) and validated on deserialize — new `OriginalEntries` field must stay inside this budget |
| V4 Access Control | No | No per-user access boundaries in this tool |
| V5 Input Validation | Yes | New export endpoint must apply `[ValidateAntiForgeryToken]` + `[RequestSizeLimit(2 * 1024 * 1024)]`, matching every other `POST /cut-lab/*` action (`CutLabController.cs:39-40,81-82,122-123,174-175`) |
| V6 Cryptography | No | No new crypto surface |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on new export POST endpoint | Tampering/Spoofing | `[ValidateAntiForgeryToken]` — every existing `/cut-lab/*` POST action already carries this; the new action must too |
| Oversized state payload exhausting server memory (512MB Render cap) | DoS | `CutLabStateSerializer.MaxUploadBytes` cap already enforced on deserialize; ensure the new `OriginalEntries` field doesn't push realistic 150-card sessions over budget (see Patch Baseline Question sizing note) |
| Same-origin bypass on any new JSON/API export sub-endpoint (if one is added instead of a Razor POST) | Spoofing | `SameOriginRequestValidator` must be applied per the existing anti-pattern note ("Skipping SameOriginRequestValidator on API endpoints") if this phase adds anything under `Controllers/Api/` |

## Sources

### Primary (HIGH confidence — direct file reads in this repo/session)
- `DeckFlow.Core/Exporting/MoxfieldTextExporter.cs`, `DeltaExporter.cs`, `FullImportExporter.cs`, `CategoryNormalization.cs`
- `DeckFlow.Core/Diffing/DiffEngine.cs`
- `DeckFlow.Core/Models/DeckDiff.cs`, `DeckEntry.cs`, `MatchMode.cs`
- `DeckFlow.Web/Services/DeckConvertService.cs` (canonical full-list export call site)
- `DeckFlow.Web/Services/CommanderBanListService.cs`
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs`, `CutLabWorkingList.cs`, `CutLabPoolValidator.cs`,
  `CutLabCutRoundEngine.cs`, `CutLabDecisionApplier.cs`, `CutLabResolvedCardCache.cs`
- `DeckFlow.Web/Models/CutLab/CutLabState.cs`, `CutLabViewModel.cs`, `CutLabRequest.cs`
- `DeckFlow.Web/Controllers/CutLabController.cs`
- `DeckFlow.Core/Manabase/ScryfallCardData.cs`, `DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs`
- `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs`, `ScryfallSetService.cs`
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (commander color-identity lookup pattern)
- `DeckFlow.Core.Tests/ExporterTests.cs`, `DiffEngineTests.cs`
- `.planning/workstreams/cut-lab/ROADMAP.md`, `REQUIREMENTS.md` (EXPORT-01/02/03, phase 105 success criteria)
- `.planning/config.json` (workflow flags: nyquist_validation=true, plan_check=true)

### Secondary (MEDIUM confidence)
- None required — no external library/framework claims made in this research.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries; entirely internal reuse verified by direct reads
- Architecture: HIGH — traced the full data path from intake through export candidates with file:line citations
- Pitfalls: HIGH — each pitfall traced to a specific existing code behavior (rehydration lossiness, validator scope
  mismatch, color-identity gap), not speculative

**Research date:** 2026-07-21
**Valid until:** Should remain valid through Phase 105 execution (no external dependency drift risk); re-verify if
Phase 104 code changes land on `CutLabState`/`CutLabController` before 105 execution begins.
