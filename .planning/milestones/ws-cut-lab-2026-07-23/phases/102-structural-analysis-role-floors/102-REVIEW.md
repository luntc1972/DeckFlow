---
phase: 102-structural-analysis-role-floors
reviewed: 2026-07-19T17:52:54Z
depth: standard
files_reviewed: 26
files_reviewed_list:
  - DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs
  - DeckFlow.Web.Tests/CutLabFloorDefaultsTests.cs
  - DeckFlow.Web.Tests/CutLabFloorRulesTests.cs
  - DeckFlow.Web.Tests/CutLabPageServiceTests.cs
  - DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs
  - DeckFlow.Web.Tests/CutLabRoleGroupLockTests.cs
  - DeckFlow.Web.Tests/CutLabStateSerializerTests.cs
  - DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs
  - DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.cs
  - DeckFlow.Web/Models/CutLab/CutLabState.cs
  - DeckFlow.Web/Models/CutLabViewModel.cs
  - DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs
  - DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs
  - DeckFlow.Web/Services/CutLab/CutLabLockRules.cs
  - DeckFlow.Web/Services/CutLab/CutLabPageService.cs
  - DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs
  - DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs
  - DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs
  - DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs
  - DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs
  - DeckFlow.Web/Views/Deck/CutLab.cshtml
  - DeckFlow.Web/e2e/cut-lab-smoke.spec.ts
  - DeckFlow.Web/e2e/cut-lab-structure.spec.ts
  - DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts
findings:
  critical: 1
  warning: 5
  info: 7
  total: 13
status: issues_found
---

# Phase 102: Code Review Report

**Reviewed:** 2026-07-19T17:52:54Z
**Depth:** standard
**Files Reviewed:** 26
**Status:** issues_found

## Summary

Reviewed the Phase 102 Cut Lab structural-analysis & role-floors changes on `gsd/cycle18-cut-lab`
(diff base `a6e36bbb`): the page-service orchestration, role assigner, floor defaults/rules,
structural findings detectors, state serializer, view model, Razor view, client script, CSS block,
and all attached unit/component/e2e tests. Cross-referenced the out-of-scope but load-bearing
`CutLabController.cs`, `Program.cs` registrations, and `CommanderBanListService.cs` to trace
error propagation.

Overall the server-side pure logic (floor clamping, role assignment, findings detectors) is
well-tested and defensively written; untrusted `CutLabStateJson` is re-clamped and the commander
lock invariant is enforced at both deserialize and rebuild. However, one reachable path destroys
the user's entire working session (Critical), the client script and server disagree on how the
role-group locked count is weighted (the C# test and the TS behavior lock in contradictory
semantics), and the serializer's documented upload cap is never enforced on the untrusted
direction.

## Critical Issues

### CR-01: Banlist fetch failure escapes `ProcessAsync` and wipes the user's entire working session

**File:** `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:229, 658-677` (interacts with `DeckFlow.Web/Controllers/CutLabController.cs:50-63` and `DeckFlow.Web/Models/CutLabViewModel.cs:53`)
**Issue:** `ProcessAsync` catches `HttpRequestException` only around `LoadFromSourceAsync` (line 175) and `ResolveEntriesAsync` (line 200). The banlist call at line 229 — `await ResolveBannedCardsPresentAsync(...)` — has no catch. `CommanderBanListService.GetBannedCardsAsync` throws `HttpRequestException` on any non-success response when its 24h memory cache is cold (`CommanderBanListService.cs:94-98`), so a mtgcommander.net outage after app restart propagates out of `ProcessAsync` into the controller's generic `catch (Exception)`. That fallback path calls `CutLabView(request, error)`, which builds a `CutLabViewModel` whose `CutLabStateJson` defaults to `string.Empty` — the hidden field re-renders empty and every lock, package, and user-set role floor the user curated is silently destroyed. The success/handled-error path (`CutLabViewModel.From`, line 122: `result.SerializedStateJson ?? request.CutLabStateJson`) preserves state; only the exception fallback wipes it. Phase 102 raised the stakes here by adding `RoleFloors` to the session envelope. Spellbook and category lookups fail open by design — banlist is the one upstream in this pipeline that fails closed.
**Fix:** Fail open on banlist errors inside `ProcessAsync`, mirroring the spellbook pattern, and preserve state in the controller fallback:

```csharp
// CutLabPageService.ProcessAsync — replace line 229
IReadOnlyList<string> bannedCardsPresent;
try
{
    bannedCardsPresent = await ResolveBannedCardsPresentAsync(resolvedEntries, cancellationToken).ConfigureAwait(false);
}
catch (HttpRequestException exception)
{
    _logger.LogWarning(exception, "Cut Lab: banlist fetch failed; continuing without legality check.");
    bannedCardsPresent = [];
    warnings.Add("Banned-card check unavailable right now — legality was not verified for this import.");
}
```

And in `CutLabController.CutLabView` (out of this diff but one line): set `CutLabStateJson = request.CutLabStateJson` so no exception path ever drops the session envelope.

## Warnings

### WR-01: Role-group locked count — server renders quantity-weighted, client immediately overwrites with row count

**File:** `DeckFlow.Web/wwwroot/ts/cut-lab.ts:287-307` (`syncRoleGroupLockState`), `DeckFlow.Web/Models/CutLabViewModel.cs:158-162`, `DeckFlow.Web/Views/Deck/CutLab.cshtml:319`
**Issue:** `CutLabViewModel.BuildRoleGroups` computes `LockedCount` as `Sum(card => card.Quantity)` — and `CutLabPageServiceTests.ProcessAsync_StackedBasics_WeightLandsCountsAcrossFindingsAndFloorViews` asserts exactly this (38 for a locked 38× Forest stack). But `syncRoleGroupLockState` in the client increments the same `[data-cut-lab-group-locked]` counter by `1` per locked *row*, and it runs on `DOMContentLoaded` via `initializeCutLab → refreshAndSerialize`. So for any pool with stacked basics (the normal Cut Lab case — the e2e fixture itself uses `36 Plains`), the server's "38 locked" is rewritten to "1 locked" the instant the page loads. The TS test (`cut-lab-lock-interactions.test.ts:378`, expects `'2'` for two locked land rows) and the C# test lock in contradictory semantics for the same DOM node. The adjacent summary label `@memberCount cards` (CutLab.cshtml:315,319) is also row-count while `LockedCount` is quantity-weighted, producing server-rendered text like "Lands · 3 cards · 92 locked".
**Fix:** Pick one unit (quantity-weighted matches the floors table and `CutLabAnalyzedCard.Quantity` weighting) and apply it on both sides. In `syncRoleGroupLockState`:

```ts
const quantity = parseRowQuantity(row);
lockedCounts.set(roleKey, previous + (isLocked ? quantity : 0));
```

and render `group.Members.Sum(m => m.Quantity)`-style card counts in the summary (add `Quantity` to `CutLabRoleMemberView`), then align the TS test expectation.

### WR-02: `Deserialize` never enforces `MaxUploadBytes`, and tampered `Packages` are carried verbatim into the rebuilt state

**File:** `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs:10-11, 37-53`, `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:638`
**Issue:** `MaxUploadBytes` is documented as "Maximum allowed UTF-8 payload size for the serialized working-session JSON" but is checked only in `Serialize` (the trusted, server-produced direction). `Deserialize` accepts any payload up to the controller's 2MB `RequestSizeLimit` — 8× the intended 256KB envelope. Additionally, `BuildState` copies `priorState.Packages` verbatim (no pruning to package IDs referenced by pool cards, no count cap), so an attacker-supplied blob with thousands of packages is deserialized, rendered into the packages section, and re-serialized — where `Serialize` then throws and converts the whole import into a hard "too large to save" error. Impact is self-DoS plus wasted server work, but the intake context explicitly treats this field as untrusted, and the cap that the constant's name promises simply isn't applied on upload.
**Fix:** In `Deserialize`, reject oversized input before parsing, and prune orphan packages during rebuild:

```csharp
public static CutLabState Deserialize(string? json)
{
    if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
    {
        return new CutLabState();
    }
    ...
}
```

In `BuildState`: `Packages = priorState.Packages.Where(p => pool.Any(c => string.Equals(c.PackageId, p.Id, StringComparison.OrdinalIgnoreCase))).ToArray()`.

### WR-03: `CutLabFloorRules.Evaluate` is quantity-blind — a cut always decrements role counts by exactly 1

**File:** `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:122-124`
**Issue:** `int newCount = currentCount - 1;` assumes every cut removes one card from each role. Cut Lab pools routinely carry `Quantity > 1` rows (stacked basics: 36× Plains fills 36 `lands` slots, and role counts everywhere else in this phase — findings, floor rows, view model — are quantity-weighted). Cutting that row drops `lands` by 36, but `Evaluate` reports `newCount = 35` and can conclude "stays above floor" when the cut actually smashes through it. The XML doc mandates that Phase 103's cut engine "MUST route every proposed cut through Evaluate" (FLOOR-02: never a silent break), so shipping this contract quantity-blind guarantees silent floor breaks for exactly the card type (basics) most likely to be cut.
**Fix:** Take the cut quantity as a parameter and subtract it:

```csharp
public static IReadOnlyList<CutLabFloorWarning> Evaluate(
    IReadOnlyDictionary<string, int> roleCounts,
    IReadOnlyDictionary<string, int> floors,
    IReadOnlyCollection<string> candidateCutRoles,
    string cardName,
    int cutQuantity = 1)
{
    ...
    int newCount = currentCount - cutQuantity;
```

Add a test with `cutQuantity: 36` against a lands floor.

### WR-04: DI-guard test cannot catch the Program.cs regression its comment claims to guard against

**File:** `DeckFlow.Web.Tests/CutLabPageServiceTests.cs:766-785, 456-475`
**Issue:** `BuildDiGuardProvider` constructs a private `ServiceCollection` with fakes and asserts `HasStructuralAnalysisDependencies` against *that* container. The comment says "this guard catches a Program.cs regression," but if `Program.cs` dropped `ICedhLandBaselineProvider`/`IManabaseBaselineProvider`/spellbook/category registrations, this test would still pass — it never inspects the production container. Because `CutLabPageService` fails open on null optionals, the regression would ship silently as permanently-degraded floors and findings (every user seeing "Combo data unavailable" / fallback lands 36) with green tests. This guard was a Codex plan-review convergence item; as implemented it provides the appearance of coverage without the coverage.
**Fix:** Assert against the real composition root — e.g., a `WebApplicationFactory<Program>` (or the existing app-startup test host if one exists) resolving `ICutLabPageService` and asserting `HasStructuralAnalysisDependencies`, or at minimum reflect over `Program`-built `IServiceCollection` registrations. Also soften the comment so it does not claim Program.cs protection it does not deliver.

### WR-05: Ramp/draw floor threshold uses the first-resolved commander's mana value, not the max — diverges from the calculator it claims to mirror

**File:** `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:454-482`, `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs:67-69`
**Issue:** `BuildRoleAssignments` captures `commanderManaValue` from the *first* commander entry encountered in deck order (`commanderManaValueResolved` latch, lines 478-482). `ManabaseRampDrawBudgetCalculator.DetermineThreshold` — the documented source of the 24-slot split (`ManabaseRampDrawBudget.cs:129-137`) — uses `Max()` over all commanders. For partner pairs (Tymna MV3 / Kraum MV5) Cut Lab's ramp/draw floors depend on which partner the deck file lists first: ramp 10/draw 14 vs ramp 13/draw 11 for the identical deck, and both can disagree with the Manabase tool's own advisory for the same list. Separately, when the commander card fails to resolve (`entry.Card is null`) the value silently stays 0, yielding ramp 8/draw 16 with no warning or provenance note.
**Fix:** Accumulate the max across commanders instead of latching the first:

```csharp
if (commanderNameSet.Contains(entry.Name))
{
    commanderManaValue = Math.Max(commanderManaValue, fact.ManaValue);
}
```

(drop the `commanderManaValueResolved` flag), and add a warning when no commander card resolved so the 0-threshold fallback is visible.

## Info

### IN-01: Redundant duplicate `IsLand` call in role assigner

**File:** `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs:87`
**Issue:** `!CutLabLockRules.IsLand(typeLine)` recomputes what line 74 already stored in `isLand`.
**Fix:** Use `if (!isLand && DeckStatClassifier.IsRampCard(typeLine, oracle))`.

### IN-02: `handlePackageSelectChange` computes `previousPackageId` after the select has already changed — always misses the actual previous package

**File:** `DeckFlow.Web/wwwroot/ts/cut-lab.ts:676-691`
**Issue:** The lookup filters rows by the select's *current* value (`getPackageMemberRows` reads `select.value`), which at `change` time is already the new package, so `previousPackageId` never resolves to the package the card left. Harmless today only because `refreshAndSerialize → syncAllPackageStates` re-syncs everything, making both targeted `syncPackageState` calls dead code.
**Fix:** Delete the `previousPackageId` block and the targeted `syncPackageState(select.value)` call, or track the previous value in a dataset attribute if targeted sync is ever needed.

### IN-03: Floor input clamps on every `input` event, coercing a cleared field to the minimum mid-typing

**File:** `DeckFlow.Web/wwwroot/ts/cut-lab.ts:225-233, 722-730`
**Issue:** `clampFloorValue` rewrites `input.value` on each keystroke; backspacing to empty parses to `NaN`, falls back to `min`, and instantly writes `"0"`, so the user fights the field while retyping (and each keystroke marks the floor user-set).
**Fix:** Clamp on `change`/`blur` only; on `input`, update the marker without rewriting the field value.

### IN-04: Weak-floor-case copy is wrong when the count is below the floor

**File:** `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs:248-257`
**Issue:** The trigger `count <= floor + WeakFloorMargin` includes genuine deficits, producing "Interaction is at 0 against a floor of 7 — every card in this role is effectively protected already" (locked in by `Compute_WeakFloorCase_ReportsZeroCountAgainstPositiveFloor`). A role 7 short of its floor is a shortage, not a protection observation; the lead sentence misinforms.
**Fix:** Branch the copy — when `count < floor`, say the role is already below its floor; keep the "effectively protected" phrasing for `floor <= count <= floor + 1`. Update the test expectation.

### IN-05: Unresolved cards default to mana value 0 and nonland, skewing curve-congestion evidence

**File:** `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:462-494`
**Issue:** When Scryfall resolution and fallback both miss, the card enters `analyzedCards` with `ManaValue = 0`, `IsLand = false`, no roles — landing in the "0-1" curve bucket. A batch of unresolved names (typos, new set lag) can fabricate or inflate a "Curve congestion at 0-1" finding built from cards whose real cost is unknown.
**Fix:** Exclude entries with `entry.Card is null` from `ComputeCurveCongestion` input (e.g., an `IsResolved` flag on `CutLabAnalyzedCard`), or surface an "N cards unresolved" warning so the read is qualified.

### IN-06: Full Scryfall resolution runs before the 101-150 card-count gate

**File:** `DeckFlow.Web/Services/CutLab/CutLabPageService.cs:195-227`
**Issue:** `ValidateCardCount` needs commander names (which need type lines), so a 1,000-card paste (within the 100k char cap) burns ~14 throttled `cards/collection` batches plus per-miss fallback searches before being rejected. A cheap upper-bound pre-check would spare the Scryfall budget.
**Fix:** Before resolving, reject when `analyzedEntries.Sum(e => e.Quantity) > CutLabPoolValidator.MaxPoolCards + a small commander allowance` (total minus max possible commanders still exceeding 150 can never pass).

### IN-07: Three different floor ceilings — server clamp 151, input `max` = pool count, finished-deck semantics 100

**File:** `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:27`, `DeckFlow.Web/Views/Deck/CutLab.cshtml:435`, `DeckFlow.Web/wwwroot/ts/cut-lab.ts:225-233`
**Issue:** `MaxFloor` clamps persisted floors to 151, the view caps the input at `Model.CardCount` (101-150, current pool not the finished deck), and the floors are documented as "minimum counts the finished 100 should keep." A tampered floor of 151 survives the server clamp yet exceeds every achievable finished-deck count; the client would re-clamp it to pool size on first edit only.
**Fix:** Align on one ceiling — clamping to 100 (finished-deck maximum) both server-side (`MaxFloor`) and in the input `max` is the semantically defensible bound.

---

_Reviewed: 2026-07-19T17:52:54Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
