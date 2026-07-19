---
phase: 99-creator-style-artifact-engine
reviewed: 2026-07-18T00:00:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - DeckFlow.Core/Knowledge/CreatorStyleRubric/SubmittedDeckStats.cs
  - DeckFlow.Core/Knowledge/CreatorStyleRubric/RubricScoreResult.cs
  - DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs
  - DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorerTests.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorDeckExemplarSelector.cs
  - DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs
  - DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs
  - DeckFlow.Web.Tests/Services/CreatorStyle/SubmittedDeckStatsBuilderTests.cs
  - DeckFlow.Web/Models/CreatorStyleRequest.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs
  - DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs
  - DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs
  - DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs
  - DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs
findings:
  critical: 2
  warning: 5
  info: 10
  total: 17
status: issues_found
---

# Phase 99: Code Review Report

**Reviewed:** 2026-07-18
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed the Phase 99 creator-style artifact engine: rubric scorer + DTOs (Core), submitted-deck stats builder, exemplar selector, whitelist pool builder, packet service with fail-closed grounding gate, request DTO, DI registration, and all six test files. The stated design invariants mostly hold: exactly one `ValidateAllAsync` batch over exemplar+combo candidates minus the pre-validated whitelist (verified via `BuildAsync_UsesOneDistinctValidationBatchMinusWhitelist`), `GroundingDegraded` OR-ed from whitelist diagnostics + batch upstream failure + any exclusion, numerics via `CultureInfo.InvariantCulture` with a de-DE byte-identity test, `{ get; init; }` carve-out respected, LF line endings clean, no NUL bytes, all DI dependencies used by `PacketServiceCollectionExtensions` are registered (Program.cs:98-112, ScryfallServiceCollectionExtensions.cs:61).

However, two correctness defects put wrong numbers into the shipped artifact — the one thing the project charter says must never be wrong:

1. The rubric scorer and the artifact's "Creator Targets" section consume `profile.FusedTargets` **unfiltered**, and the fusion engine persists superseded-history rows, conditional rows, and philosophy rows into that list. Superseded targets get scored as current, and conditions are silently dropped.
2. A deck that loads but fails Scryfall resolution emits `karsten:target_lands = 0` / `karsten:land_delta = 0` as real measured values, so the rubric confidently reports the deck as e.g. "37 lands under target" with no caveat.

Both are reachable in production and neither triggers any degradation notice. Five warnings cover a coincidence-based exemplar ranking, a fragile positional verdict join in the grounding gate, an inconsistent duplicated Spellbook call, swallowed cancellation, and full-precision double formatting in the artifact.

## Critical Issues

### CR-01: Rubric scores superseded and conditional fused targets as if they were current; artifact drops Source/Verdict/Condition

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:184-186, 348-370`; `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs:30-33`
**Issue:** `ProfileFusionEngine.Fuse` appends superseded-history rows to its output (`CreateSupersededHistory`, ProfileFusionEngine.cs:184-207, `Source = "superseded"`, `Verdict = "superseded"`, `Weight = 1.0`) and also emits conditional rows (`Condition` set) and philosophy rows. The CLI fuse command persists this **full** list into `CreatorStyleProfile.FusedTargets` (ContentKbCommandRunners.cs:152-158). The packet service passes `profile.FusedTargets` unfiltered into `CreatorStyleRubricScorer.Score`, and the scorer scores every target. Consequences in the shipped artifact:
- A superseded rule (e.g. an old "45 lands" statement later revised to "38") produces a second, contradictory rubric row for the same measured key, scored at full weight 1.0, indistinguishable from the active target — the "Creator Targets" section prints only Metric/Value/Weight/StatedMin/StatedMax, omitting `Source`, `Verdict`, `Condition`, and `Conflict`.
- A conditional target ("38 lands *when landfall*") is scored and printed as an unconditional target because `Condition` is dropped everywhere.

The ChatGPT critique will treat outdated and conditional numbers as the creator's current unconditional targets. This is factually wrong artifact content on any creator whose stated rules ever got superseded or carry conditions.
**Fix:**
```csharp
// CreatorStylePacketService.BuildAsync — filter before scoring and before the Creator Targets section:
IReadOnlyList<FusedTarget> scoreableTargets = profile.FusedTargets
    .Where(t => !string.Equals(t.Verdict, "superseded", StringComparison.OrdinalIgnoreCase))
    .ToArray();
// ...score against scoreableTargets; in BuildArtifactText, either iterate scoreableTargets
// or print Source/Verdict/Condition per target so superseded/conditional rows are labeled.
```
Decide explicitly (and test) how conditional targets are handled — either exclude them from scoring or emit `; Condition: {target.Condition}` in the artifact line.

### CR-02: Unresolvable submitted deck emits zeroed Karsten metrics as real measured values, producing false "under" rubric verdicts with no caveat

**File:** `DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs:152-157, 463-497`
**Issue:** When Scryfall returns 200 but no deck card resolves (misspelled paste, unrecognized names — `EmptyResolution()` path, `HasResolvedDeck = false`), `BuildAsync` still emits `metrics["karsten:land_delta"] = 0` and `metrics["karsten:target_lands"] = 0` from `EmptyReport()` (`LandDelta` computes to `0 - 0`, ManabaseModels.cs:660). Only `karsten:health_score` is gated on `HasResolvedDeck` — and even that gate is lossy, since `0` is also the legitimate score for `ManabaseHealth.NeedsWork`. The rubric scorer then joins these keys as comparable measured values and emits e.g. `Metric: karsten:target_lands; Target: 37; Submitted: 0; Delta: -37; Verdict: under` in the artifact. Nothing sets `GroundingDegraded` or adds a notice for this state, so the artifact asserts a fabricated manabase deficit as fact. `SubmittedDeckStatsBuilderTests.BuildAsync_UnresolvableDeck_ReturnsZeroedKarstenAndEmptyDeckContext` (lines 178-207) bakes the zeroed values in without covering the downstream rubric consequence.
**Fix:**
```csharp
if (resolution.HasResolvedDeck)
{
    metrics["karsten:land_delta"] = resolution.Report.LandDelta;
    metrics["karsten:target_lands"] = resolution.Report.TargetLands;
    metrics["karsten:health_score"] = ToHealthScore(resolution.Report.Health);
}
// Omitted keys make CreatorStyleRubricScorer emit "insufficient-measured" for karsten targets,
// which is the truthful verdict when the deck could not be resolved.
```
Also consider surfacing `HasResolvedDeck == false` on `SubmittedDeckAnalysis` so the packet service can OR it into `GroundingDegraded`/`Notice`.

## Warnings

### WR-01: Exemplar ranking by raw `ConfidenceMarker` ordinal-descending is an alphabetical coincidence, not a semantic ranking

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorDeckExemplarSelector.cs:26`; `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs:11-52`
**Issue:** `OrderByDescending(deck => deck.ConfidenceMarker, StringComparer.Ordinal)` ranks markers by reverse alphabet. Today's production domain is `"ok"` (CreatorProfileDeckCrawler.cs:17) vs `"near-precon"`, where `"ok" > "near-precon"` happens to be correct — but only by accident. The tests prove the ordering is not semantic: they use fictional `"high"/"med"/"low"` markers and assert that **"med" outranks "low" outranks "high"** (first test selects the two `med` decks and the `low` deck while discarding both `high` decks). Any future marker (e.g. `"verified"`, `"stale"`) silently reshuffles exemplar selection, and the tests document the wrong mental model for the next maintainer.
**Fix:** Rank via an explicit map, e.g. `private static int Rank(string marker) => marker switch { "ok" => 0, "near-precon" => 1, _ => 2 };` then `.OrderBy(d => Rank(d.ConfidenceMarker))`. Rewrite the tests to use the real marker domain.

### WR-02: Grounding gate correlates verdicts to candidates positionally with silent `Math.Min` truncation; contract does not guarantee order

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:293-308`
**Issue:** `BuildAcceptedByOriginal` maps `accepted[candidateNames[i]] = verdicts[i].CanonicalName` up to `Math.Min(count, count)`. `CardGroundingVerdict` carries no original-name field and `ICardGroundingGuard.ValidateAllAsync`'s XML doc ("Verdicts for the supplied candidates") does not promise same-order/same-count. The current `CardGroundingGuard` implementation happens to preserve order (CardGroundingGuard.cs:56-66), but any reordering implementation misattributes canonical names across cards in the fail-closed gate — and the first packet test already demonstrates this silently: its stub returns verdicts in a different order than the candidate batch, so `"Commander One"` maps to canonical `"Arcane Signet"` and vice versa, and the assertions still pass because they sort the output. A count mismatch is silently truncated instead of failing loudly.
**Fix:** Either add the original candidate name to `CardGroundingVerdict` and join by name, or (minimally) document the 1:1 ordered contract on `ICardGroundingGuard.ValidateAllAsync` and replace `Math.Min` with a hard check: `if (verdicts.Count != candidateNames.Count) throw new InvalidOperationException(...)`.

### WR-03: Commander Spellbook queried twice per request with different entry sets — combo metric and validated combo cards can disagree

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:196`; `DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs:134-141`
**Issue:** `SubmittedDeckStatsBuilder` computes `combo_density:included_per_deck` from `analyzedEntries` (mainboard + commander only). The packet service then makes a **second** upstream `FindCombosAsync` call using `analysis.Entries`, which is the full flagged list *including sideboard and maybeboard*. Two consequences: (a) a duplicate upstream HTTP call per artifact build for the same deck; (b) "Validated Combo Cards" in the artifact can include cards from combos that only exist because of sideboard/maybeboard entries the deck doesn't actually run, while the rubric's combo-density number was computed without them — the artifact contradicts itself.
**Fix:** Compute combos once in the stats builder over the analyzed (mainboard+commander) entries, expose the `CommanderSpellbookResult` (or the included-combo card names) on `SubmittedDeckAnalysis`, and drop the second call — or at minimum filter `analysis.Entries` to the same `AnalyzedBoards` set before calling `FindCombosAsync`.

### WR-04: `ResolveComboCountAsync` swallows `OperationCanceledException`, defeating cancellation

**File:** `DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs:248-252`
**Issue:** The `catch (Exception ex)` around the Spellbook call also catches `OperationCanceledException` thrown when the request token fires, logs it as a warning, and continues the whole build pipeline (Scryfall batches etc.) for a client that already disconnected. Repo convention elsewhere distinguishes `OperationCanceledException` at boundaries.
**Fix:** `catch (Exception ex) when (ex is not OperationCanceledException)` (rethrow cancellation implicitly).

### WR-05: Artifact numbers formatted with full round-trip double precision — 17-digit noise in the ChatGPT prompt

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:434-435`; `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs:49`
**Issue:** `FormatNumber` uses `value.ToString(CultureInfo.InvariantCulture)` (shortest round-trip). Fused targets are averages (e.g. `12.333333333333334`) and `Delta` is a raw double subtraction (`10.5 - 12.8` → `-2.3000000000000007`), so the artifact will routinely carry 16-17 significant digits. Deterministic and invariant, yes — but it bloats tokens and presents spurious precision to the model doing the critique. The tests only exercise "clean" values (12.5, -2, 0.75), so this never surfaces in the suite.
**Fix:** `value.ToString("0.###", CultureInfo.InvariantCulture)` (or similar fixed precision) in `FormatNumber`, and/or round `Delta` at computation (`Math.Round(submittedValue - target.Value, 3)`).

## Info

### IN-01: "on-target" verdict requires exact double equality

**File:** `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs:75-83`
**Issue:** `delta == 0` on doubles means fractional fused targets (averages) essentially never yield "on-target"; a delta of `1e-15` reads "over".
**Fix:** Compare with a small epsilon (e.g. `Math.Abs(delta) < 0.0005`) or document that on-target is integer-exact only.

### IN-02: Mixed metric-key vocabulary within one artifact

**File:** `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs:53, 65`; `CreatorStylePacketService.cs:350`
**Issue:** Mapped rubric rows use the measured key (`category_ratio:ramp`) while insufficient rows use the stated key (`ramp`); the "Creator Targets" section always prints stated keys. The same metric appears under two names in one artifact, which the downstream model must reconcile.
**Fix:** Emit both keys on rubric rows (e.g. `StatedMetric` + `Metric`) or print the mapped key in Creator Targets when one exists.

### IN-03: Degraded notice copy claims cards were withheld even when none were

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:244-246`
**Issue:** A whitelist-only upstream failure with zero exclusions still produces "Some candidate cards were withheld because grounding could not fully validate them."
**Fix:** Branch the notice text on `excludedCount > 0` vs upstream-failure-only.

### IN-04: `CreateUnavailableResult` conflates "profile unavailable" with grounding degradation

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:277-291`
**Issue:** Missing/insufficient profile returns `GroundingDegraded = true` with empty `ArtifactText` and empty `CreatorSlug` in `RubricScores`; consumers must infer "unavailable" from the empty artifact rather than a typed status.
**Fix:** Add an explicit `Unavailable`/status flag to `CreatorStylePacketResult` (keeps `GroundingDegraded` meaning what its XML doc says).

### IN-05: `PacketServiceCollectionExtensions` XML docs are stale

**File:** `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:24-44`
**Issue:** Summary/remarks still say "the four scoped packet-service factories" and the dependency list omits the newly required `CategoryKnowledgeRepository`, `ICreatorStyleProfileStore`, `CreatorWhitelistPoolBuilder`, `ICardGroundingGuard`, and `ICreatorDeckCacheStore`.
**Fix:** Update the doc to list all six registered services and the new prerequisites.

### IN-06: Test namespace inconsistency

**File:** `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs:13`; `CreatorStyleDiRegistrationTests.cs:17`
**Issue:** These two use `DeckFlow.Web.Tests.Services.CreatorStyle` while the other new test files (and the documented project convention: single test namespace per project) use `DeckFlow.Web.Tests`.
**Fix:** Normalize to `DeckFlow.Web.Tests`.

### IN-07: `CreatorWhitelistPoolBuilder.BuildAsync` overload has no production callers

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs:70-74`
**Issue:** Only tests call the convenience overload; the packet service uses `BuildWithDiagnosticsAsync`. Dead public surface unless a later phase consumes it.
**Fix:** Remove it (and point tests at `BuildWithDiagnosticsAsync`) or keep with a doc note on the intended consumer.

### IN-08: Exemplar `CardNames` drop quantities and are not deduplicated

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:226-230`
**Issue:** "Whole-deck exemplars" are emitted as flat name lists: 30 basics collapse to entries without quantity, and two printings of the same card yield duplicate names in the `Cards:` line.
**Fix:** Apply `.Distinct(StringComparer.Ordinal)` after resolution; if deck shape matters to critique, include counts.

### IN-09: Scorer's defensive dictionary copy can throw on case-variant duplicate keys

**File:** `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs:28`
**Issue:** `new Dictionary<string, double>(submittedStats.Metrics, OrdinalIgnoreCase)` throws `ArgumentException` if a caller supplies a case-sensitive dictionary containing e.g. `"Ramp"` and `"ramp"`. Current callers are safe (builder uses OrdinalIgnoreCase), but the public API can crash on plausible input.
**Fix:** Build via a loop with `TryAdd`, or document the precondition on `SubmittedDeckStats.Metrics`.

### IN-10: No test isolates exclusion-only grounding degradation

**File:** `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs:91-145`
**Issue:** The rejection test also sets `HasUpstreamFailure = true`, so the `excludedCount > 0` leg of the `GroundingDegraded` OR is never verified independently.
**Fix:** Add a case with a rejected verdict and `HasUpstreamFailure = false`, asserting `GroundingDegraded` is still true.

---

_Reviewed: 2026-07-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
