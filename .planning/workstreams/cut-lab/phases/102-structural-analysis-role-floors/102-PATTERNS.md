# Phase 102: Structural Analysis & Role Floors - Pattern Map

**Mapped:** 2026-07-19
**Files analyzed:** 16 (7 new C#, 4 new test files, 5 modified surfaces)
**Analogs found:** 14 / 16 (2 partial — see "No Analog Found")

All analogs below were read directly this session; line numbers are verified against the working tree on `gsd/cycle18-cut-lab` (HEAD `7f7e2424`).

> **Correction to 102-RESEARCH:** the serializer cap is **256 KB** (`CutLabStateSerializer.MaxUploadBytes = 262_144`, `CutLabStateSerializer.cs:11`), not 1 MB. Floors are still trivially inside it, but any copy or comment about the cap must say 256 KB.
>
> **Correction to 102-RESEARCH Wave 0 paths:** existing Cut Lab tests live FLAT in `DeckFlow.Web.Tests/` (e.g. `CutLabLockStateTests.cs`, `CutLabPageServiceTests.cs`) with the single namespace `DeckFlow.Web.Tests` — there is no `DeckFlow.Web.Tests/CutLab/` folder. New test files should follow the flat-root convention (per-project single test namespace, root `CLAUDE.md` conventions).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs` (NEW) | pure rule service (static) | transform | `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs` (NEW) | pure rule service (static) | transform | `DeckFlow.Web/Services/CutLab/CutLabLockRules.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` (NEW) | pure aggregator (static) | transform | `DeckFlow.Core/Analysis/WinConMapAggregator.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs` (NEW) | derivation service | transform | `ManabaseAnalysisService.ResolveBaseline`/`BuildCommunityBaseline` | role-match |
| `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (MODIFY) | orchestration service | request-response | itself + `ManabaseAnalysisService.TagPlanRolesAsync` | exact |
| `DeckFlow.Web/Models/CutLab/CutLabState.cs` (MODIFY) | serializable state model | state round-trip | itself + `SpellRequirement.PlanRoles` (`ManabaseModels.cs:222`) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` (MODIFY) | serializer/tamper gate | state round-trip | itself (commander-lock choke point) | exact |
| `DeckFlow.Web/Models/CutLabViewModel.cs` (MODIFY) | view model | request-response | itself | exact |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` (MODIFY) | Razor view | request-response | itself + `site-common.css` idioms | exact |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (MODIFY) | browser module | event-driven | itself | exact |
| `DeckFlow.Web/wwwroot/css/site-common.css` (MODIFY) | stylesheet | — | gold-warning advisory idiom (2849-2852) + cutlab section (4141-4147) | exact |
| `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` (MODIFY) | Core calculator | transform | itself (one-word `internal`→`public`, line 113) | exact |
| `DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs` etc. (NEW ×4) | xUnit tests | — | `DeckFlow.Web.Tests/CutLabLockStateTests.cs` | exact |
| `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` (MODIFY) | xUnit service tests | — | itself (+ `TestDoubles/FakeCategoryKnowledgeStore.cs`) | exact |
| `DeckFlow.Web/ts-tests/` vitest (NEW or extend) | Vitest unit | — | `ts-tests/cut-lab-lock-interactions.test.ts` | exact |
| `DeckFlow.Web/e2e/` spec (NEW or extend) | Playwright e2e | — | `e2e/cut-lab-smoke.spec.ts` | exact |

## Pattern Assignments

### `CutLabRoleAssigner.cs` (pure rule service, transform) — NEW

**Analog:** `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` (full 230-line pure static classifier) + `DeckFlow.Core/Analysis/DeckStatClassifier.cs` predicates + `CutLabLockRules.IsLand`.

**File shape to copy** (`PlanRoleClassifier.cs:1-4, 24`): file-scoped namespace, `public static class`, caller supplies all I/O results as parameters, exhaustive xmldoc on the class explaining precedence and deliberate exclusions:

```csharp
using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services.Manabase;

public static class PlanRoleClassifier
```

**Core call it composes — the pre-gate out-param overload** (`PlanRoleClassifier.cs:43-48`). This is the ONLY entry point CutLabRoleAssigner should use for interaction (Pitfall 2 — the permanent gate at lines 81-85 strips one-shot removal like Swords to Plowshares from the returned flags, but line 70 captures interaction merit first):

```csharp
public static PlanRole Classify(
    CardFact fact,
    IReadOnlyList<string> categories,
    bool isComboPiece,
    ManabaseMode mode,
    out bool interactionMeritPreGate)
```

**Verified predicate signatures for the eight-role table** (all read this session):

| Role key | Signal | Verified source |
|----------|--------|-----------------|
| `lands` | `CutLabLockRules.IsLand(string? typeLine)` | `CutLabLockRules.cs:123-124` (front-face via `CardTypeLine.FrontFace`) |
| `ramp` | `DeckStatClassifier.IsRampCard(string typeLine, string oracleText)` | `DeckStatClassifier.cs:16-23` |
| `draw` | `DeckStatClassifier.IsDrawCard(string oracleText)` | `DeckStatClassifier.cs:29-34` |
| `interaction` | `interactionMeritPreGate` out-param OR `IsBoardWipeCard(oracle)` OR `IsTargetedRemovalCard(typeLine, oracle)` | `PlanRoleClassifier.cs:70`, `DeckStatClassifier.cs:54-59, 138-144` |
| `protection` | `DeckStatClassifier.IsProtectionCard(string name, string oracleText)` | `DeckStatClassifier.cs:180-185` |
| `engines` | `roles.HasFlag(PlanRole.Engine)` | `PlanRoleClassifier.cs:136-139, 177-184` |
| `payoffs` | `roles.HasFlag(PlanRole.Payoff)` (permanent-gated) | `PlanRoleClassifier.cs:92, 112-115, 162-165` |
| `wincons` | `DeckStatClassifier.IsClosingPowerCard(typeLine, oracle)` OR name ∈ Spellbook `IncludedCombo` set | `DeckStatClassifier.cs:78-85` |

**Bridge from Cut Lab's resolved cards to `CardFact`:** `ScryfallCardFactMapper.ToCardFact(ScryfallCardData card, int quantity, bool isCommander = false)` — `DeckFlow.Core/Manabase/ScryfallCardFactMapper.cs:16`. Do not hand-map: it derives front-face mana value for MDFCs (root `cmc` is COMBINED for split cards — comment at lines 26-30) and joins oracle text across faces.

**Copy the doc style:** `PlanRoleClassifier.cs:6-23` documents precedence, mode gating, and deliberate exclusions in the class xmldoc; `PermanentOnlyRoles` const (line 92) carries a `// Why:`-style comment. New role-key strings must be the stable serialized keys `lands ramp draw interaction protection engines payoffs wincons` (UI-SPEC Component Contract 1).

---

### `CutLabFloorRules.cs` (pure rule service, transform) — NEW

**Analog:** `DeckFlow.Web/Services/CutLab/CutLabLockRules.cs` — the Phase 101 pure, immutable, tamper-defending rule class this phase's contract must mirror.

**Immutable with-mutation + invariant re-application pattern** (`CutLabLockRules.cs:12-27`):

```csharp
public static CutLabState EnforceCommanderLock(CutLabState state)
{
    ArgumentNullException.ThrowIfNull(state);

    if (!state.Pool.Any(card => card.IsCommander && !card.IsLocked))
    {
        return state;   // no-op fast path -> idempotency is testable (CutLabLockStateTests.cs:24-34)
    }

    return state with
    {
        Pool = state.Pool
            .Select(card => card.IsCommander && !card.IsLocked ? card with { IsLocked = true } : card)
            .ToArray(),
    };
}
```

**Unknown-input no-op pattern** (`CutLabLockRules.cs:103-110`) — `BulkLockRoleGroup` returns the state unchanged for unsupported group names; `ClampFloors` should treat unknown role keys the same way (drop/ignore, never throw). Note `BulkLockRoleGroup` is currently hard-coded to `"lands"` only — Phase 102's eight role groups supersede this; the planner should extend or replace it alongside the pill change (UI-SPEC A7).

**Case-insensitive key comparison helpers** (`CutLabLockRules.cs:126-130`): private static `NamesMatch`/`PackageIdsMatch` using `StringComparison.OrdinalIgnoreCase` — reuse the idiom for role-key matching.

**The Phase 103 contract:** `Evaluate(roleCounts, floors, candidateCutRoleMemberships) -> broken-floor warnings`. Per Pitfall 7, state in the class xmldoc that Phase 103 MUST route every proposed cut through it (the xmldoc-as-contract style is `PlanRoleClassifier.cs:6-23`). Warning copy pattern is fixed by UI-SPEC: *"Cutting {card} drops {role} to {newCount}, below your floor of {floor}."*

---

### `CutLabStructuralFindings.cs` (pure aggregator, transform) — NEW

**Analog:** `DeckFlow.Core/Analysis/WinConMapAggregator.cs` — pure static `Compute` with an explicit source-availability flag.

**The `comboDataAvailable` degradation pattern to copy** (`WinConMapAggregator.cs:22-27, 49-61`) — this is the direct answer to Pitfall 6 (never report a confident false-negative when a fail-open source was down):

```csharp
/// <param name="comboDataAvailable"><see langword="true"/> when combo lookup ran (even if it
/// found nothing); <see langword="false"/> when lookup failed/was unavailable.</param>
public static WinConMap Compute(
    IReadOnlyList<WinConComboInput> combos,
    IReadOnlyList<WinConNearComboInput> nearCombos,
    IEnumerable<WinConClosingCardInput> closingCards,
    bool comboDataAvailable)
{
    ...
    if (!comboDataAvailable)
    {
        // Combo lookup failed/unavailable: no combos or near-combos to report, but the
        // closing-power read still stands — a combo-less/unavailable deck still gets a
        // win-condition read from its non-combo closers.
        return new WinConMap(Array.Empty<WinConCombo>(), Array.Empty<WinConNearCombo>(), 0,
            closingList, false, WinConBand.Unknown);
    }
```

`CutLabStructuralFindings` needs TWO such flags: `comboDataAvailable` (gates enabler-starved + combo win-cons) and `categoryDataAvailable` (gates stranded-subthemes). UI-SPEC fixes the degraded-copy strings.

**Enabler-starved evidence source** (`CommanderSpellbookService.cs:26-30`, cited by RESEARCH):

```csharp
public sealed record SpellbookAlmostCombo(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results,
    string Instructions);
```

**Threshold constants:** named constants with `// Why:` comments, mirroring `ManabaseRampDrawBudgetCalculator`'s deadband style (`ManabaseRampDrawBudget.cs:84-93` — `Math.Abs(rampDelta) <= 2.0` with a comment explaining the both-axes rule). Numbers are product constants flagged for sign-off (RESEARCH A3 / UI-SPEC A3).

---

### `CutLabFloorDefaults.cs` (derivation service, transform) — NEW

**Analog:** `ManabaseAnalysisService.ResolveBaseline` + `BuildCommunityBaseline` (`ManabaseAnalysisService.cs:563-629`) and `ManabaseRampDrawBudgetCalculator` (`ManabaseRampDrawBudget.cs:113-124`).

**Bracket fallback to invert** (`ManabaseAnalysisService.cs:567-575`) — value AND provenance decided in one place so they can never disagree (copy that comment discipline):

```csharp
private static (int Bracket, ManabaseBracketSource Source) ResolveBaseline(ManabaseAnalysisOptions options)
    => options.Bracket is int explicitBracket
        ? (explicitBracket, options.BracketSource ?? ManabaseBracketSource.Override)
        : (options.Mode switch
        {
            ManabaseMode.Cedh => 5,
            ManabaseMode.Focused => 3,
            _ => 2,
        }, ManabaseBracketSource.Fallback);
```

Cut Lab needs the reverse mapping too (PlayExperience string → `ManabaseMode` for classification; bracket for floors — Open Q2 recommendation). The provenance tuple pattern directly feeds the UI-SPEC "Default for B{bracket}: {value} — based on {fallback}" sub-label.

**Fail-open baseline read + null row handling** (`ManabaseAnalysisService.cs:587-592`):

```csharp
(int bracket, ManabaseBracketSource bracketSource) = ResolveBaseline(options);
ManabaseBracketBaseline? row = _manabaseBaseline.TryGetBracketBaseline(bracket);
if (row is null)
{
    return null;   // B1 / missing snapshot -> caller falls through (Pitfall 5: never a 0 floor)
}
```

Provider API confirmed: `ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)` (`ManabaseBaselineProvider.cs:21, 66`); registered singleton + warm-loaded (`Program.cs:95, 310`); `ICedhLandBaselineProvider` singleton at `Program.cs:94, 307`.

**Ramp/draw split** (`ManabaseRampDrawBudget.cs:113-124`) — confirmed `internal static int CalculateTargetRamp(double threshold)` at line 113. Pitfall 4 stands: promote to `public` (one-word diff, preferred) rather than duplicating the switch; `targetDraw = 24 - targetRamp` (line 79).

---

### `CutLabPageService.cs` (orchestration service, request-response) — MODIFY

**Analog:** itself (Phase 101 pipeline) + `ManabaseAnalysisService.TagPlanRolesAsync` for the new classification stage.

**Where the new stages slot in** (`CutLabPageService.cs:158-196`): after `ResolveCommanderSelection` (line 158) and before `BuildState`/serialize (lines 171-183). The pipeline shape to preserve: every stage returns early via the `Error(message, warnings)` helper (lines 417-422) on hard failure; warnings accumulate in a `List<string>`.

**CRITICAL data gap:** `ResolvedCutLabEntry` (private record, `CutLabPageService.cs:424-428`) currently keeps only `Name, Quantity, TypeLine, IsCommander` and DISCARDS the resolved `ScryfallCardData` (used only transiently at lines 205-221). Stage A requires extending this record (or a parallel structure) to carry the `ScryfallCardData` per POST so `ScryfallCardFactMapper.ToCardFact` can run — per-POST only, never serialized (Pitfall 3).

**Optional-dependency constructor pattern for the two new I/O deps** — copy `ManabaseAnalysisService` (`ManabaseAnalysisService.cs:313-337`), which takes them as nullable optional params so existing tests keep compiling:

```csharp
private readonly ICategoryKnowledgeStore? _categoryKnowledge;
private readonly ICommanderSpellbookService? _spellbook;
// ctor: ICategoryKnowledgeStore? categoryKnowledge = null, ICommanderSpellbookService? spellbook = null
```

Existing ctor guard style (`CutLabPageService.cs:77-91`): `ArgumentNullException.ThrowIfNull` for required deps, `logger ?? NullLogger<CutLabPageService>.Instance` for the optional logger. DI registration stays `AddScoped` (`Program.cs:181`); `IManabaseBaselineProvider`/`ICedhLandBaselineProvider` are already-registered singletons to inject.

**The classification I/O stage to mirror** (`ManabaseAnalysisService.cs:835-877`) — Spellbook once, categories in ONE batch, both fail-open:

```csharp
// Source 2 (combo pieces), fetched once. Fail-open: a Spellbook outage leaves the set empty.
var comboNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (_spellbook is not null)
{
    try
    {
        CommanderSpellbookResult? combos =
            await _spellbook.FindCombosAsync(deckCards, cancellationToken).ConfigureAwait(false);
        if (combos is not null)
            foreach (SpellbookCombo combo in combos.IncludedCombos)
                foreach (string cardName in combo.CardNames)
                    comboNames.Add(cardName);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception exception)
    {
        _logger.LogWarning(exception, "Plan-presence: Commander Spellbook fetch failed; continuing without combo roles.");
    }
}
```

and the batched fail-open category helper (`ManabaseAnalysisService.cs:905-929`) with its static `EmptyCategories` dictionary — the batching rationale comment at lines 870-873 (~65 sequential queries ≈ 20 s) must be honored: ONE `GetCategoriesForNamesAsync` call for the whole pool. Track `comboDataAvailable`/`categoryDataAvailable` booleans off these try/catches for the findings stage (the Manabase version doesn't need them; Cut Lab does — Pitfall 6).

**Floor merge:** user-set floors from `priorState.RoleFloors` win over recomputed defaults (Pattern 3); derived role data is computed here every POST and passed to the view model, never into `CutLabState`.

---

### `CutLabState.cs` (state model, round-trip) — MODIFY

**Analog:** itself + the additive-JSON precedent `SpellRequirement.PlanRoles` (`ManabaseModels.cs:222`, confirmed: `public PlanRole PlanRoles { get; init; } = PlanRole.None;` with the "Additive — defaults ... JSON round-trips are unaffected" comment).

**Every property in this file follows one shape** (`CutLabState.cs:7-20`): `sealed record`, `{ get; init; }` with an initializer default, xmldoc per member:

```csharp
public sealed record CutLabState
{
    /// <summary>Resolved commander name for the working session, or empty when unknown.</summary>
    public string Commander { get; init; } = string.Empty;

    /// <summary>Imported pool cards, including commander identity and lock/package assignment state.</summary>
    public IReadOnlyList<CutLabPoolCard> Pool { get; init; } = [];
    ...
}
```

New `RoleFloors` follows identically: `public IReadOnlyList<CutLabRoleFloor> RoleFloors { get; init; } = [];` plus a `CutLabRoleFloor` record (`Role` string key / `Floor` int / `IsUserSet` bool) in the same file, matching the co-located `CutLabPoolCard`/`CutLabPackage`/`CutLabIntent` layout. **Formatting carve-out (binding):** never let a formatter convert `{ get; init; }` to `{ get; }` — System.Text.Json silently skips get-only properties.

Pre-102 blobs in open tabs deserialize cleanly because the initializer default (`[]`) covers a missing `roleFloors` JSON member — that is the whole point of the precedent.

---

### `CutLabStateSerializer.cs` (tamper-defense choke point) — MODIFY

**Analog:** itself. The choke-point pattern to extend (`CutLabStateSerializer.cs:37-53`):

```csharp
public static CutLabState Deserialize(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return new CutLabState();
    try
    {
        var state = JsonSerializer.Deserialize<CutLabState>(json, Options) ?? new CutLabState();
        return CutLabLockRules.EnforceCommanderLock(state);   // <- add CutLabFloorRules.ClampFloors here
    }
    catch (JsonException)
    {
        return new CutLabState();
    }
}
```

Add `CutLabFloorRules.ClampFloors(...)` in the same return expression (non-negative, known role keys only, ≤ pool size — pool size may need clamping later at BuildState since Deserialize sees the PRIOR pool; planner decides the exact clamp site, but the invariant-at-deserialize idiom is this line). `Options` is `JsonSerializerDefaults.Web` (line 13) — camelCase, matching the TS contract. Cap: `MaxUploadBytes = 262_144` (line 11).

---

### `CutLabViewModel.cs` (view model, request-response) — MODIFY

**Analog:** itself. Shape (`CutLabViewModel.cs:7-77`): `sealed record`, init-only props with defaults, one static `From(request, result)` factory copying service-result fields. New members (role groups for 8 sections, findings list, floor rows with count/default/user-set) extend `From` the same way.

**Phase 101 open item to fix here (Pitfall 9):** `PoolStatusText` (`CutLabViewModel.cs:48-49, 75, 79-88`) is dead-and-triplicated copy — the view computes its own string (`CutLab.cshtml:128`) and TS overwrites it (`cut-lab.ts:166`); `CutLabPageServiceTests.cs:52` asserts it. Delete/consolidate as part of the first UI plan and update that test assertion.

---

### `Views/Deck/CutLab.cshtml` (Razor view, request-response) — MODIFY

**Analog:** itself; plus the accordion/chip/advisory idioms from `site-common.css` (below).

**Result-panel + panel-heading + focal chip pattern** (`CutLab.cshtml:124-131`) — each of the three new sections repeats this shell:

```html
<section class="result-panel">
    <div class="panel-heading">
        <div>
            <h2>Lock your pool</h2>
            <p class="prompt-size-note">@Model.CardCount cards in pool · @lockedCount locked (protected from any future cut)</p>
```

**Pool-table row attribute surface the new code must upgrade** (`CutLab.cshtml:188-202`): `data-cut-lab-role` is currently the single value `"land"` computed inline (lines 190-194); UI-SPEC A9 upgrades it to a space-separated role-key list (`data-cut-lab-role="lands ramp"`) sourced from the server-computed role assignment. The "Type / role" cell (lines 214-219) is replaced by the compact role list. The standalone "Lock all lands" pill (lines 171-175) is superseded by the Lands group's pill (UI-SPEC A7) — remove it and update the e2e/vitest that assert it.

**Member-chip flow for role groups and finding evidence** (`CutLab.cshtml:272-277` — the package member chips):

```html
<div class="kb-chip-area__chips">
    @foreach (var member in members)
    {
        <span class="kb-chip">@member.Name</span>
    }
</div>
```

**Accordion idiom:** no `<details>` exists in CutLab.cshtml yet; copy the `.kb-expert-accordion` summary CSS shape (`site-common.css:495-510` — `--panel` background, `--accent` left border, 0.75rem 1rem summary padding, `[open]` bottom border) for the new `.cutlab-role-group`. Sitewide `<details>/<summary>` precedent: `Manabase.cshtml:125` (per UI-SPEC).

**Hidden state field + form contract** (`CutLab.cshtml:22-28`): single POST form, `@Html.AntiForgeryToken()`, `<input type="hidden" name="CutLabStateJson" ...>` — floors ride this same field; the new "Recalculate analysis" button re-submits this form via `form.requestSubmit()` (UI-SPEC A6). Number-input precedent for the floor cell: `AdminYoutubeExport/Index.cshtml:26` (native `type="number"`, styled by `.field input`).

---

### `wwwroot/ts/cut-lab.ts` (browser module, event-driven) — MODIFY

**Analog:** itself. Structure to extend, not replace: typed snapshot interfaces (lines 1-28), a pure exported `api` object on `globalThis.DeckFlowCutLab` for unit testing (lines 49-97), DOM query helpers via `data-cut-lab-*` attributes (lines 102-137), one `refreshAndSerialize()` funnel (lines 544-548) that every mutation calls, delegated `document`-level click/change handlers (lines 568-624).

**Serialization contract to extend** (`buildCutLabStateJson`, lines 69-94): explicit field-by-field camelCase normalization with the commander force-lock (`isLocked: card.isCommander ? true : card.isLocked`). Add `roleFloors: [{ role, floor, isUserSet }]` the same way — key names must match `JsonSerializerDefaults.Web` camelCase of the C# record (byte-exact contract test below).

**Function that must change for multi-role tokens** (lines 65-67, currently exact-equality):

```typescript
isLandRole(role: string | null | undefined): boolean {
  return (role ?? '').trim().toLowerCase() === 'land';
},
```

→ token matching over the space-separated `data-cut-lab-role` list (UI-SPEC A9); `lockAllLands` (lines 351-367) generalizes to per-role bulk lock driven by the same pool-table checkboxes (single lock source, Pitfall 8 / UI-SPEC A1).

**Phase 101 open item to fix here:** `getForm` hard-codes `'form[action="/cut-lab"]'` (lines 102-103) — breaks under a path base; switch to a `data-` hook (e.g. the existing `data-cache-key="cut-lab"`).

**Single-source live updates:** `updateLockedCountChip` (lines 151-167) shows the pattern for live text updates (also the triplicated copy — consolidate with the view-model fix). Floor at-floor markers update client-side the same way; counts stay server-rendered.

---

### `wwwroot/css/site-common.css` — MODIFY

**Analog:** the shipped advisory idiom + the existing Cut Lab section. All new classes go beside the existing Cut Lab block (`site-common.css:4141-4147`):

```css
.cutlab-lock-badge--commander {
  border-left: 3px solid var(--commander-gold, #d4af37);
}

.cutlab-package--locked {
  border-left: 3px solid var(--accent);
}
```

**Finding-block advisory idiom to copy verbatim** (`.manabase-beta-notice`, `site-common.css:2849-2852`) — this is the exact `.cutlab-finding` recipe the UI-SPEC prescribes:

```css
border-left: 3px solid var(--gold-warning, var(--warning, #c8a040));
background: color-mix(in srgb, var(--gold-warning, var(--warning, #c8a040)) 10%, var(--panel-soft-bg, var(--panel)));
color: var(--ink, inherit);
```

(Variant at 14% mix: `.manabase-verdict--issues`, lines 2977-2980. Text-only gold marker for the at-floor state: `.manabase-lens-short`, lines 2849-2852 region — `color: var(--gold-warning, ...); font-weight: 600;`.)

**Accordion summary** (`site-common.css:495-510`): `--accent` left border 4px, `--panel` background, `.kb-expert-accordion__summary { cursor: pointer; padding: 0.75rem 1rem; font-weight: 600; color: var(--accent-strong); }`, `[open]` summary gains `border-bottom: 1px solid var(--line)`.

**Chips** (`site-common.css:546-580`): `.kb-chip-area__chips { display:flex; flex-wrap:wrap; gap:0.5rem; }`, `.kb-chip { ...border-radius:999px; font-size:var(--fs-xs); background:var(--panel-soft-bg); }`, `.kb-chip-area__empty-hint { color:var(--muted); font-size:var(--fs-sm); }`.

**Pill** (`site-common.css:~2495-2515`): `.manabase-pill` — `border-radius:999px; padding:0.3rem 0.85rem; background:var(--panel-soft-bg, var(--panel))` with the visually-hidden radio specificity note. **Responsive table**: `table[data-prompt-cedh-reference-table]` mobile stacked `data-label` pattern at lines 1154-1263 (already used by the pool table — the floor table reuses the same attribute).

Binding constraints: layout CSS in `site-common.css` ONLY; zero new `:root` tokens needed (UI-SPEC verified all consumed tokens exist with fallbacks); never `--theme-surface`; no `--danger` on any Phase 102 surface.

---

### xUnit tests (4 NEW + extend `CutLabPageServiceTests`) 

**Analog:** `DeckFlow.Web.Tests/CutLabLockStateTests.cs` (flat root, `namespace DeckFlow.Web.Tests`, `public sealed class XxxTests`).

**Pure-rule test shape to copy** (`CutLabLockStateTests.cs:10-34`) — state factory helper at the bottom, one behavior per `[Fact]`, idempotency asserted by value equality on records:

```csharp
[Fact]
public void EnforceCommanderLock_CommanderSubmittedUnlocked_ForcesCommanderLocked()
{
    var state = CreateState(
        new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, IsCommander = true, IsLocked = false },
        new CutLabPoolCard { Name = "Swords to Plowshares", Quantity = 1, IsLocked = false });

    var result = CutLabLockRules.EnforceCommanderLock(state);

    Assert.True(result.Pool.Single(card => card.IsCommander).IsLocked);
    Assert.False(result.Pool.Single(card => !card.IsCommander).IsLocked);
}
...
private static CutLabState CreateState(params CutLabPoolCard[] pool) => ...
```

Apply to `CutLabRoleAssignerTests` (feed hand-built `CardFact`s + category lists + combo sets; assert pre-gate interaction visibility for Swords/Counterspell per mode), `CutLabFloorRulesTests` (clamping of negative/junk/oversized floors; break→warning; idempotency), `CutLabFloorDefaultsTests` (B1/no-bracket fallbacks; user-override merge), `CutLabStructuralFindingsTests` (each detector + both degradation flags).

**Service-test seam** (`CutLabPageServiceTests.cs:17-53`): private `FakeLoader`/`FakeResolver`/`FakeBanListService` records passed to the real service; extend with fakes for the two new optional deps. `FakeCategoryKnowledgeStore` already exists at `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs:12` (`public sealed class ... : ICategoryKnowledgeStore`). Spellbook: `CommanderSpellbookService` has an internal test ctor via `[InternalsVisibleTo]` (Manabase tests exercise this seam), or stub `ICommanderSpellbookService` directly. Note the `PoolStatusText` assertion (`CutLabPageServiceTests.cs:52`) that must change with the view-model cleanup. Regression guards that must stay green: `CutLabLockStateTests`, `CutLabStateSerializerTests` (old blobs without `roleFloors` still deserialize), `CutLabRoleGroupLockTests`.

---

### Vitest (`ts-tests/`) — NEW or extend `cut-lab-lock-interactions.test.ts`

**Analog:** `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts`.

**Byte-exact camelCase contract test** (lines 63-96) — extend with `roleFloors` so the C#↔TS JSON contract is pinned:

```typescript
it('serializes the exact camelCase contract and forces the commander locked', () => {
  const json = api.buildCutLabStateJson({ ... });
  expect(json).toBe('{"commander":"Atraxa, ...');   // full-string equality, not shape matching
});
```

**DOM-harness pattern** (lines 98-205): set `document.body.innerHTML` to a minimal form + table with the real `data-cut-lab-*` attributes, `document.dispatchEvent(new Event('DOMContentLoaded'))`, click, then assert checkbox state, chip text, and the parsed hidden-field JSON. The existing `isLandRole` test (lines 56-61) and the `data-cut-lab-role="land"` fixtures (lines 110-130) MUST be updated in the same plan as the multi-token attribute change (UI-SPEC A9). Import side-effect module via `import '../wwwroot/ts/cut-lab';` (line 2); api handle from `globalThis.DeckFlowCutLab` (lines 41-43).

---

### Playwright e2e — extend `e2e/cut-lab-smoke.spec.ts` or new `e2e/cut-lab-structure.spec.ts`

**Analog:** `DeckFlow.Web/e2e/cut-lab-smoke.spec.ts` (confirmed path: `DeckFlow.Web/e2e/`, not a root `tests/` dir).

**Flag + admin-lock harness to copy** (lines 44-78): `test.describe.configure({ mode: 'serial' })`, `acquireAdminLockForTest`/`releaseAdminLockForTest` from `./support/admin-lock`, `setToolEnabled(page, 'Cut Lab', true)` in `beforeEach`, OFF + release in `afterEach` try/finally.

**Import helper + persistence assertion pattern** (lines 50-64, 96-127): `importPool(page)` fills the paste form and waits on the "Lock your pool" heading (30 s timeout); persistence tests mutate, assert the hidden field with regex (`await expect(hiddenState).toHaveValue(/"name":"Plains".*"isLocked":true/)`), resubmit, and re-assert rendered state. The Phase 102 floor test follows exactly: adjust a floor input, assert `roleFloors` in the hidden JSON, resubmit, assert the Adjusted badge + at-floor marker persisted.

**Theme×viewport screenshot matrix** (lines 10-19, 129-147): 3 themes (`site.css`/`site-azorius.css`/`site-nyx.css` via `deckflow-theme` cookie) × 2 viewports (1440/430 wide), `fullPage` screenshots into `.planning/ui-design/cut-lab/screenshots/`.

**Assertions that break with A7/A9:** lines 62 and 99-101 assert `[data-cut-lab-lock-all-lands]` — update when the standalone pill is superseded by the Lands group pill. Run only via `scripts/run-web-test.sh` (never a Windows browser); probe for a stale Windows server on 5173 first.

---

### `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` — MODIFY (one word)

`internal static int CalculateTargetRamp(double threshold)` at line 113 → `public`. Precedent: Phase 101 already promoted/consumed Core helpers for Cut Lab (`CardTypeLine.FrontFace` via `CutLabLockRules.IsLand`). Add a one-line xmldoc noting the second consumer (`CutLabFloorDefaults`), keep the switch expression untouched (formatting carve-out: preserve switch expressions).

## Shared Patterns

### Fail-open I/O wrapper
**Source:** `ManabaseAnalysisService.cs:905-926` (`GetCategoriesFailOpenAsync`)
**Apply to:** every new I/O touchpoint in `CutLabPageService` (Spellbook, categories)
```csharp
try { return await _dep.CallAsync(...).ConfigureAwait(false); }
catch (OperationCanceledException) { throw; }                       // NEVER swallow cancellation
catch (Exception exception)
{
    _logger.LogWarning(exception, "Cut Lab: <source> failed; <degradation>.");  // structured template, named placeholders
    return Empty;                                                    // static empty sentinel, availability flag = false
}
```

### Tamper-defense at the deserialize choke point
**Source:** `CutLabStateSerializer.cs:44-48` + `CutLabLockRules.EnforceCommanderLock` (`CutLabLockRules.cs:12-27`)
**Apply to:** `RoleFloors` clamping (`CutLabFloorRules.ClampFloors`), chained in the same return expression. Never trust client-submitted floors or role data; role/finding data is never persisted at all (recomputed per POST).

### Additive `{ get; init; }` state extension
**Source:** `ManabaseModels.cs:222` (`PlanRoles`), `CutLabState.cs` throughout
**Apply to:** every new serialized record member. Initializer default (`= []` / `= string.Empty` / `= false`) so old JSON deserializes; NEVER `{ get; }` (System.Text.Json skips get-only — enforced carve-out).

### Pure-static rule class, caller does I/O
**Source:** `PlanRoleClassifier.cs`, `CutLabLockRules.cs`, `WinConMapAggregator.cs`
**Apply to:** all four new rule classes. `public static class`, `ArgumentNullException.ThrowIfNull` at entry, xmldoc states the contract (including the Phase 103 MUST-call clause on `CutLabFloorRules`), no logger, no async, unit-testable without HTTP.

### Optional nullable DI deps + NullLogger
**Source:** `ManabaseAnalysisService.cs:313-337`; `CutLabPageService.cs:77-91`
**Apply to:** `CutLabPageService` ctor extension — new deps as `= null` optional params so existing test call sites compile unchanged.

### `data-cut-lab-*` attribute contract + single serialize funnel
**Source:** `cut-lab.ts:102-137, 544-548`; `CutLab.cshtml:192-202`
**Apply to:** all new interactive surfaces (floor inputs `data-cut-lab-floor="{roleKey}"`, group pills). Every mutation ends in `refreshAndSerialize()`; the pool-table checkboxes remain the single lock source (UI-SPEC A1).

### e2e admin flag gating
**Source:** `cut-lab-smoke.spec.ts:44-78`
**Apply to:** any new spec — serial mode, admin lock, flag ON in beforeEach / OFF in afterEach.

## No Analog Found

| File/Piece | Role | Data Flow | Reason |
|------|------|-----------|--------|
| Static floor-default table (interaction/protection/engines/payoffs/wincons numbers) | data constants | — | No bracket-derived targets for these five roles exist anywhere in the codebase (verified by RESEARCH; community numbers are folklore). Product constants requiring sign-off — RESEARCH A3 / UI-SPEC A3. Pattern for presentation only: named constants with `// Why:` comments (`ManabaseRampDrawBudget.cs:84-87` deadband style). |
| Floor stepper/editor control | UI component | event-driven | Only shipped number input is `AdminYoutubeExport/Index.cshtml:26` (weak analog, admin-only styling). UI-SPEC A5 resolves: native `<input type="number" min="0" step="1">` styled by existing `.field input`, no custom component. |

## Metadata

**Analog search scope:** `DeckFlow.Web/Services/{CutLab,Manabase}/`, `DeckFlow.Web/Models/`, `DeckFlow.Core/{Analysis,Manabase}/`, `DeckFlow.Web/Views/Deck/`, `DeckFlow.Web/wwwroot/{ts,css}/`, `DeckFlow.Web/{ts-tests,e2e}/`, `DeckFlow.Web.Tests/`, `Program.cs`
**Files read this session:** 18 (full) + 6 targeted-range reads
**Pattern extraction date:** 2026-07-19
