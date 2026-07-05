---
phase: 83-packet-service-srp-split
reviewed: 2026-07-04T21:14:03Z
depth: standard
files_reviewed: 22
files_reviewed_list:
  - DeckFlow.Web/Services/Packets/PacketTextAssembler.cs
  - DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs
  - DeckFlow.Web/Services/Packets/DeckEntryReflagHelper.cs
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web/Services/DeckComparisonService.cs
  - DeckFlow.Web/Services/MetaGapService.cs
  - DeckFlow.Web/Services/DeckPrimerPacketService.cs
  - DeckFlow.Web.Tests/PacketTextAssemblerTests.cs
  - DeckFlow.Web.Tests/ScryfallReferenceResolverTests.cs
  - DeckFlow.Web.Tests/DeckEntryReflagHelperTests.cs
  - DeckFlow.Web.Tests/PacketByteIdentityFixtures.cs
  - DeckFlow.Web.Tests/DeckAnalysisByteIdentityTests.cs
  - DeckFlow.Web.Tests/DeckComparisonByteIdentityTests.cs
  - DeckFlow.Web.Tests/MetaGapByteIdentityTests.cs
  - DeckFlow.Web.Tests/DeckPrimerByteIdentityTests.cs
  - DeckFlow.Web.Tests/DeckComparisonServiceTests.cs
  - DeckFlow.Web.Tests/MetaGapServiceTests.cs
  - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
  - DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs
  - DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs
  - DeckFlow.Web/Controllers/DeckPacketController.cs
  - DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 83: Code Review Report

**Reviewed:** 2026-07-04T21:14:03Z
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

## Summary

Reviewed the four migrated packet services (`DeckAnalysisPacketService`, `DeckComparisonService`,
`MetaGapService`, `DeckPrimerPacketService`) and the three new `DeckFlow.Web/Services/Packets/`
collaborators (`PacketTextAssembler`, `ScryfallReferenceResolver`, `DeckEntryReflagHelper`) against
the phase's stated byte-identical/behavior-preservation goal, with the review lens specifically
tuned to error-path fidelity and edge-case parity rather than the already-green happy-path golden
tests.

Cross-checked every migrated `BuildSectionedDecklistText`/`AppendKeyValueLine`/
`ReflagCommanderEntry` call site against the pre-refactor source (via `git show <commit>^:<file>`)
line-by-line, and confirmed the do-not-unify fence (Analysis's partner-aware reflag, per-service
`NormalizeOracleText`/`CollapseWhitespace`, combo-reference formatters, cache-key text, JSON
round-trip validation guards) is untouched. The three extracted collaborators are correctly
stateless, well-documented, and their characterization tests exercise the documented divergence
axes (H1 Possible-Includes-stays-plain asymmetry, H2 single-slash normalize-fallthrough).

One real, previously-unflagged behavior divergence was found in the error path: the three
Scryfall-resolving services' `try/catch (HttpRequestException)` wraps around the **entire**
`ScryfallReferenceResolver.ResolveBatchAsync` call, not just the `cards/collection` HTTP failure
it was designed to re-label. Because `ResolveBatchAsync`'s per-name fallback loop is inside that
same try, an upstream failure from the *fallback search* (`SearchFallbackCardAsync` /
`SearchPrintingFallbackCardAsync`, e.g. a genuine Scryfall 429/503 hit while resolving a
collection-miss) is now re-labeled with the same "cards/collection ... packet" message the
collection-call failure uses — producing a different final user-facing string than before the
refactor for that specific (narrow, upstream-error-only) path. This is exactly the class of
landmine the phase's prior executors were told to watch for; they verified the *collection-call*
failure case correctly but did not trace the *fallback-call* failure case through the same
`UpstreamErrorMessageBuilder`/`DeckPacketController` chain. No test (old or new) exercises this
path, so it shipped unnoticed. See WR-01.

A second, lower-severity divergence: `DeckComparisonService`'s post-migration card dedup was
broadened from "dedupe fallback-recovered cards only" to "dedupe every resolution by `Card.Name`,
collection-hit or fallback," which changes a latent (pre-existing, never observed) crash-on-`
ToDictionary`-duplicate-key path into a silent dedup. Almost certainly a net improvement, but it is
an undocumented behavior change relative to the original code, not a byte-identical migration of
existing logic. See WR-02.

No Critical findings — no security issues, crashes, or data-loss risks were found. The remaining
items are Info-level design/quality notes.

## Warnings

### WR-01: Exception re-wrap conflates fallback-search failures with cards/collection failures, changing user-facing error copy for a real upstream-error scenario

**File:** `DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs:96-138` (root cause), consumed at `DeckFlow.Web/Services/DeckComparisonService.cs:373-393`, `DeckFlow.Web/Services/MetaGapService.cs:557-579`, `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1882-1901`

**Issue:** `ScryfallReferenceResolver.ResolveBatchAsync` has two distinct HTTP failure sources inside
one method: (1) the `cards/collection` batch call, which throws its own explicit
`HttpRequestException` when non-2xx/null-`Data` (lines 106-113), and (2) the per-name
`fallbackStrategy` delegate invoked for unresolved names (line 129), which is NOT wrapped inside
the resolver and can itself throw `HttpRequestException` — `ScryfallCardResolver
.SearchFallbackCardAsync` throws `"Scryfall fallback lookup failed while resolving {cardName} with
HTTP {code}."` on a non-2xx/non-404 response, and `SearchPrintingFallbackCardAsync` throws
`"Scryfall returned HTTP {code}."` via `ScryfallThrottle.ThrowIfUpstreamUnavailable` for 429/5xx.

Each of the three consuming services wraps the **entire** `ResolveBatchAsync` call in a single
`catch (HttpRequestException exception)` and unconditionally re-throws with a NEW,
collection-call-flavored message (e.g. Comparison: `"{deckLabel} Scryfall card reference lookup
failed while building the comparison packet with HTTP {code}."`). This re-wrap cannot distinguish
"the collection call itself failed" from "a per-name fallback search failed" — both paths produce
the identical re-wrapped text.

Traced end-to-end against `UpstreamErrorMessageBuilder` (`DeckFlow.Web/Services
/UpstreamErrorMessageBuilder.cs:67-90`) and `DeckPacketController`'s catch blocks
(`DeckFlow.Web/Controllers/DeckPacketController.cs:457-463` for Comparison, unconditional
`BuildScryfallMessage` calls for Analysis/MetaGap), this is a genuine divergence from pre-refactor
behavior for the fallback-failure case specifically (confirmed via `git show <pre-refactor
commit>^:...` — none of the three original implementations wrapped the fallback-search call in a
try/catch; only the collection-call validation branch had an explicit `throw`):

- **Comparison:** pre-refactor, a fallback-search 5xx propagated uncaught as `"Scryfall fallback
  lookup failed while resolving {cardName} with HTTP {code}."` — this message contains neither
  `"Deck A"`/`"Deck B"` nor `"cards/collection"`, so the controller fell through to
  `UpstreamErrorMessageBuilder.BuildScryfallMessage` → `BuildSiteSpecificMessage` → final text
  `"Scryfall returned HTTP {code}. Try again shortly."` Post-refactor, the SAME failure is
  re-wrapped to `"{deckLabel} Scryfall card reference lookup failed while building the comparison
  packet with HTTP {code}."`, which DOES contain `"Deck A"`/`"Deck B"`, so the controller now shows
  this raw, differently-worded message directly (bypassing `UpstreamErrorMessageBuilder` entirely).
- **Analysis:** pre-refactor, `SearchPrintingFallbackCardAsync`'s 429/5xx propagated as `"Scryfall
  returned HTTP {code}."`, which does not contain `"cards/collection"`/`"analysis packet"`, so it
  fell through to the generic `"Scryfall returned HTTP {code}. Try again shortly."` copy.
  Post-refactor it is unconditionally re-wrapped to `"Scryfall card reference lookup
  (cards/collection) returned HTTP {code} while building the analysis packet."`, which DOES match
  `BuildDetailedScryfallMessage`'s first branch, producing a different final string
  (`"Scryfall card reference lookup failed while building the analysis packet with HTTP {code}.
  Try again shortly."`). The 83-06 plan's own investigation only traced the collection-call-failure
  case (correctly found no divergence there) and did not consider the fallback-call-failure case.
- **MetaGap:** coincidentally unaffected — neither the original fallback-failure message nor the
  re-wrapped message matches `BuildDetailedScryfallMessage`'s branches, so both routes converge on
  the same generic final text. No user-visible change here, but the underlying conflation is the
  same code smell.

No existing test (old or new, including the 83-01 byte-identity harness, which is explicitly
happy-path-only) exercises a fallback-strategy-throws scenario, so this shipped unnoticed and is
not gated against regressing further.

**Fix:** Scope the re-wrap to the collection call only. Concretely, either (a) have
`ScryfallReferenceResolver` catch and tag/wrap ONLY its own `cards/collection` validation-failure
throw (e.g. a dedicated marker or a distinct exception message prefix the resolver owns), and let
`fallbackStrategy` exceptions propagate completely untouched so each service's existing per-name
fallback method's own message reaches the controller unchanged (matching pre-refactor behavior
exactly), or (b) move the `try/catch` inside `ResolveBatchAsync` to wrap only the
`ExecuteCollectionAsync` call (lines 106-113), not the surrounding loop that also invokes
`fallbackStrategy`, and have each service's caller `catch` a more specific condition (e.g. check
`exception.Data` or a resolver-owned exception subtype) before re-wrapping. Add a regression test
per service that makes the fallback delegate throw and asserts the pre-refactor message text/route
survives.

### WR-02: DeckComparisonService's card dedup was silently widened beyond the original scope

**File:** `DeckFlow.Web/Services/DeckComparisonService.cs:395-403`

**Issue:** The pre-refactor `LookupCardDetailsAsync` (see `git show 81d09887^:...`) built
`resolvedCards` via `resolvedCards.AddRange(response.Data.Data)` (all collection-call cards,
UNDEDUPED) plus a fallback-only dedup check (`!resolvedCards.Any(card => equals(card.Name,
fallbackCard.Name))`) before adding a fallback-recovered card. If the `cards/collection` response
itself ever contained two entries with the same `Name` for two distinct submitted identifiers (a
rare aliasing edge case), the original code would add both, and the downstream
`cardLookup = cards.ToDictionary(card => card.Name, ...)` (line 418) would throw
`ArgumentException` at runtime — a latent, pre-existing crash risk.

The migrated code instead dedupes ALL resolutions — both collection hits and fallback hits — via a
single `seenCardNames` `HashSet<string>` (lines 396-403), so the same aliasing scenario now silently
drops the duplicate instead of crashing. This is very likely a strict improvement, but it is an
undocumented widening of scope beyond "migrate the existing fallback-dedup verbatim" — the phase's
own stated goal is byte-identical migration, and this one code path now has different failure
semantics (silent dedup vs. crash) for an edge case the byte-identity harness cannot exercise
(it only feeds through fixture data, never a duplicate-name Scryfall response). Also note:
`resolvedCards`'s element ORDER changed from "API-response order, chunk by chunk, with fallback
entries appended after their own chunk's collection block" to "alphabetical-by-original-request-
name across the whole batch" — harmless today since `Cards` only feeds a `ToDictionary` lookup and
a `.Count` used in a timing log, but worth flagging since any future consumer that relies on list
order would silently get different results than before.

**Fix:** No functional fix required (the new behavior is safer), but document this as an
intentional, reviewed scope change in the migration's decision record rather than leaving it
described only as "matching the original code's fallback-merge dedup" (which undersells that the
dedup now also covers the collection-hit path the original never deduped).

## Info

### IN-01: ScryfallReferenceResolver is not DI-registered; each service hand-constructs its own instance

**File:** `DeckFlow.Web/Services/DeckComparisonService.cs:93`, `DeckFlow.Web/Services/MetaGapService.cs:87`, `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:199`

**Issue:** 83-RESEARCH.md's "Recommended registration" section proposed registering
`ScryfallReferenceResolver` in `PacketServiceCollectionExtensions.AddDeckFlowPacketServices()` as a
Scoped service. The executed implementation instead has each of the three consuming services do
`_scryfallReferenceResolver = new ScryfallReferenceResolver(scryfallCardResolver);` directly in
their own constructor. Functionally safe today (the collaborator is stateless and just wraps
whatever `IScryfallCardResolver` instance DI hands it, so there's no captive-dependency risk), but
it means three separate `ScryfallReferenceResolver` instances now exist per request instead of one
shared instance, and the collaborator can't be swapped/mocked via DI container overrides should a
future test or feature need that seam.

**Fix:** Optional cleanup — register `ScryfallReferenceResolver` in
`PacketServiceCollectionExtensions.AddDeckFlowPacketServices()` and inject it via constructor
instead of `new`-ing it in each of the three services, matching the sibling `PacketTextAssembler`/
`DeckEntryReflagHelper` static-class pattern's spirit of "one owned place" (though those two are
static and need no DI at all).

### IN-02: Source-scan tripwire test is filesystem-path-dependent

**File:** `DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs:23-47`

**Issue:** `SourceFile_ReferencesNoScryfallResolutionType` walks up from
`AppContext.BaseDirectory` looking for `DeckFlow.Web/Services/DeckPrimerPacketService.cs` on disk,
throwing `InvalidOperationException` (not a clean test failure) if the source tree isn't reachable
from the test binary's output directory (e.g., a hypothetical future CI configuration that runs
tests from a published/copied artifact without the full repo checked out alongside). This mirrors
an existing accepted pattern in `DeckFlow.Core.Tests/CarveOutGuardTests.cs`, so it's a pre-existing
convention, not a new anti-pattern — flagged for awareness only, not urgent.

**Fix:** None required now; if this pattern is used again, consider a graceful `Assert.Fail` (or
`Assert.Skip`) instead of throwing when the path can't be located, so a CI environment change fails
loudly as a test failure rather than an unhandled exception.

### IN-03: AppendKeyValueLine's delegate parameter name bakes in a "single line" assumption

**File:** `DeckFlow.Web/Services/Packets/PacketTextAssembler.cs:149-157`

**Issue:** `AppendKeyValueLine`'s delegate parameter is named `normalizeSingleLine`
(`Func<string?, string, string> normalizeSingleLine`), which is accurate for how all four current
callers use it (each passing their own `NormalizeSingleLine`), but couples the generic
key:value-line-writer's parameter name to one specific normalization concept. Purely cosmetic; no
behavior impact.

**Fix:** Optional rename to `normalize` or `valueNormalizer` for a slightly more generic API surface
if this collaborator gains a fifth consumer with a differently-shaped normalizer.

---

_Reviewed: 2026-07-04T21:14:03Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
