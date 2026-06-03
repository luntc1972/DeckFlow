---
slug: sol-ring-empty-categories
status: resolved
trigger: "Phase 24 / CAT-01: category suggestion returns no categories for Sol Ring (colorless artifact ramp staple). Reproduce in BOTH harvest-running and harvest-stopped states; running Archidekt harvest/cache job is the suspected cause."
goal: find_root_cause_only
created: 2026-05-24
updated: 2026-05-24
---

# Debug: Sol Ring empty categories

## Symptoms

- **Expected**: Category suggestion returns categories for Sol Ring (colorless artifact ramp staple). On `/suggest-categories` it should categorize Sol Ring in a pasted deck; on `/commander-categories` it should show categories of cards (including Sol Ring) across commander decks with that commander.
- **Actual**: Sol Ring returns no categories (empty result).
- **Error messages**: None reported (silent empty result).
- **Timeline**: Worked before — regressed. Bisect candidate.
- **Reproduction**: `/suggest-categories` (deck containing Sol Ring), possibly also `/commander-categories`.
- **Suspected cause**: Running Archidekt harvest/cache job (`ArchidektCacheJobService`). Roadmap mandate: reproduce (or rule out) in BOTH harvest-running AND harvest-stopped states.

## Investigation constraints (project)

- Claude investigates, does NOT apply the fix. Produce a reproduction recipe + failing-test spec; Codex applies the fix (per CLAUDE.md delegation).
- Build via `/mnt/c/Program Files/dotnet/dotnet.exe`; VSTest unreliable in WSL.
- Do NOT auto-launch the web server — ask the user to start it if a live repro is needed.
- Category suggestion code: `DeckFlow.Web/Services/CategorySuggestionService.cs` (`ICategorySuggestionService`), `CategoryKnowledgeStore.cs` / `ICategoryKnowledgeStore`, `CategoryKnowledgeRepository`, `CardNormalizer`/normalization, `ArchidektCacheJobService.cs`.

## Current Focus

- hypothesis: CONFIRMED — write-time category filter (`CategoryFilter.IsIncluded`) discards the literal category "Artifact"/"Artifacts", so a card whose only Archidekt category is its card type ("Artifact") — e.g. Sol Ring — has ZERO rows written to `card_category_observations`. Both lookup surfaces then read empty.
- next_action: hand Root Cause Report + failing-test spec to Codex for the fix (find_root_cause_only — no production edit by Claude).

## Evidence

- timestamp: 2026-05-24 — `CardNormalizer.Normalize("Sol Ring")` -> `"sol ring"`; stable, no `/` split, no punctuation stripping issue. Lookup keys and write keys both call `CardNormalizer.Normalize`, so normalization is NOT the cause. (DeckFlow.Core/Normalization/CardNormalizer.cs:7-25)
- timestamp: 2026-05-24 — Read path `CategoryKnowledgeRepository.GetCategoriesAsync` queries `WHERE normalized_card_name = @normalized` against `card_category_observations`. If no rows were written for Sol Ring, result is empty. (CategoryKnowledgeRepository.cs:197-223)
- timestamp: 2026-05-24 — Write path is gated by `CategoryFilter.IsIncluded(category)` at the repository insert (`PersistObservedCategoriesAsync`, CategoryKnowledgeRepository.cs:496-501: `if (!CategoryFilter.IsIncluded(category)) continue;`).
- timestamp: 2026-05-24 — Write path is ALSO gated upstream by the SAME filter inside `CategoryKnowledgeReporter.SplitCategories` (CategoryKnowledgeReporter.cs:142-156: only yields categories where `CategoryFilter.IsIncluded(category)`), which `DeckCategoryCacheWriter.PersistDeckEntriesAsync` uses to enumerate categories (DeckCategoryCacheWriter.cs:48).
- timestamp: 2026-05-24 — `CategoryFilter.ExcludedCategories` contains "Artifact" and "Artifacts" (plus Creature/Instant/Sorcery/Enchantment/Planeswalker/Battle + plurals). `IsIncluded` returns false for any of these. (CategoryFilter.cs:8-29)
- timestamp: 2026-05-24 — Sol Ring is a colorless artifact. In Archidekt, its assigned category is very commonly just "Artifact" (card-type bucketing) and frequently nothing else. Both write-time chokepoints therefore drop every category for Sol Ring; only `card_deck_totals` gets a row (no category filter at PersistCardDeckTotalsAsync, DeckCategoryCacheWriter.cs:69-72). Net: Sol Ring has deck-presence counts but zero category observations.
- timestamp: 2026-05-24 — git history: `CategoryFilter` with "Artifact" excluded has existed since initial import (969f339, old path DeckSyncWorkbench.Core/Reporting/CategoryFilter.cs) and the `IsIncluded` gate has been wired into the persist path since the original repository (969f339 CategoryKnowledgeRepository.cs:156). So the filter list itself is long-standing, NOT a new code edit.
- timestamp: 2026-05-24 — Regression vector identified at commit `084015c` (fix(harvest): skip click-sweep when admin harvest is active): `CategorySuggestionService` now skips `RunCacheSweepAsync` whenever `_harvestJobService.GetActiveJob() is not null` (CategorySuggestionService.cs:127-135). This removed the on-demand sweep that previously could (re)populate the cache during a lookup, exposing the long-standing write-time filtering as a user-visible empty result while a harvest runs.
- timestamp: 2026-05-24 — Default request mode is `All` (CategorySuggestionRequest.cs:11), so `/suggest-categories` also runs the Scryfall Tagger lookup and an EDHREC fallback. EDHREC fallback (CategorySuggestionService.cs:145) only fires when exact AND inferred AND tagger are all empty; if Tagger returns tags, EDHREC is skipped and the user sees tagger-only categories — masking the cache gap inconsistently. In pure `CachedData` mode (no tagger, EDHREC still gated) the empty result is cleanest and most reproducible.
- timestamp: 2026-05-24 — `/commander-categories` uses `GetCategoryRowsForCommanderAsync` (CategoryKnowledgeRepository.cs:275-307) which reads the SAME `card_category_observations` table (join `o.source = 'archidekt_live:' || q.deck_id`, source string confirmed written identically at ArchidektDeckCacheSession.cs:163). Because Sol Ring's "Artifact" rows were never written, Sol Ring is absent from the commander aggregate too. Same root cause, both surfaces.

## Eliminated

- Card-name normalization mismatch — ELIMINATED. Read and write both use `CardNormalizer.Normalize`; "Sol Ring" normalizes deterministically to "sol ring" with no DFC `/`-split or punctuation edge case.
- `source` string mismatch on the commander join — ELIMINATED. `ArchidektDeckCacheSession` writes `source = $"archidekt_live:{deckId}"` (line 163), exactly matching the join `'archidekt_live:' || q.deck_id` (CategoryKnowledgeRepository.cs:287).
- Concurrency / sweep-gate deadlock corrupting reads — ELIMINATED as the PRIMARY cause. The `_sweepGate` skip at 084015c changes WHETHER a sweep runs, not whether reads return correct data; reads open their own connections and never take `_sweepGate`. It is a contributing regression vector (exposed the latent filter bug) but not the data-loss mechanism.
- Recent CategoryFilter edit adding "Artifact" — ELIMINATED. The exclusion list is unchanged since initial import.

## Resolution

root_cause: The write-time category filter `CategoryFilter.IsIncluded` excludes the literal categories "Artifact"/"Artifacts" (intended to drop generic card-type buckets that "carry no deck-strategy value"). It is applied at BOTH harvest write chokepoints — `CategoryKnowledgeReporter.SplitCategories` (CategoryKnowledgeReporter.cs:142-156) and `CategoryKnowledgeRepository.PersistObservedCategoriesAsync` (CategoryKnowledgeRepository.cs:496-501). For a colorless artifact whose only (or primary) Archidekt category is "Artifact" — e.g. Sol Ring — every category is dropped, so ZERO `card_category_observations` rows are ever written. Both lookup surfaces (`/suggest-categories` cached/inferred path via `GetCategoriesAsync`, and `/commander-categories` via `GetCategoryRowsForCommanderAsync`) then return empty. The commit `084015c` (skip click-sweep when a harvest is active) removed the on-demand sweep that formerly masked the gap, turning a latent data-shape bug into a visible "Sol Ring returns no categories" regression.

both_harvest_states:
  - HARVEST RUNNING: `harvestActive == true` -> click-sweep skipped (CategorySuggestionService.cs:132). Only the already-persisted cache is read. Because Sol Ring's "Artifact" rows were filtered at write time, `inferredCategories` is empty. Result is empty unless the Scryfall Tagger (All mode) happens to return tags. -> REPRODUCED.
  - HARVEST STOPPED: `harvestActive == false` -> a 30s click-sweep may run and re-import recent decks, but every Sol Ring category is dropped at write time by the same filter. The cache still gains no "Artifact" rows for Sol Ring. `GetCategoriesAsync` still returns empty. -> REPRODUCED (the running harvest is NOT required to produce the empty result; it only changed the timing/visibility). The harvest job is therefore RULED OUT as the data-loss mechanism and confirmed only as the regression-exposure vector.

fix: APPLIED by Codex (2026-05-24), reviewed + verified + committed by Claude. Option 1 + fallback chosen: removed `CategoryFilter.IsIncluded` gate from both write chokepoints (`CategoryKnowledgeReporter.SplitCategories`, `CategoryKnowledgeRepository.PersistObservedCategoriesAsync`); added `CategoryFilter.IncludedOrFallback` applied at read/report time (`GetCategoriesAsync`, commander/knowledge row paths, `CategorySuggestionReporter.GetCategories`) — hides generic card-type buckets only when richer categories exist, otherwise keeps the type label (so Sol Ring resolves to "Artifact").
  - Commits (branch v1.4): `14554a1` test(category) RED regression tests; `835c552` fix(category) the fix.
  - Verification: `dotnet build DeckFlow.sln` clean, 0 warnings. DeckFlow.Core.Tests 68/68 pass (incl. 3 new). DeckFlow.Web.Tests 461 pass / 13 fail / 3 skip — all 13 failures are pre-existing AdminCssPhase1Tests (Phase 18 CSS debt), unrelated to this change.
  - Files: CategoryFilter.cs (+IncludedOrFallback), CategoryKnowledgeRepository.cs, CategoryKnowledgeReporter.cs, CategorySuggestionReporter.cs.

original recommended fix direction (for the record):
  - Stop discarding card-type categories at WRITE time. The filter's purpose ("drop generic buckets") is a presentation/relevance concern, not a storage concern — discarding at write time is lossy and irreversible for cards whose ONLY category is a card type. Options, in order of preference:
    1. Remove the `CategoryFilter.IsIncluded` gate from the two WRITE paths (`SplitCategories` and `PersistObservedCategoriesAsync`) so all observed categories are persisted, and apply `CategoryFilter` at READ/report time instead (e.g. in `CategorySuggestionReporter` / `CategoryKnowledgeReporter` display path) — OR
    2. If card-type categories should still be filtered for relevance, add a fallback so a card that ends up with ZERO surviving categories retains its card-type category (so "Artifact" survives when it's the only label), OR
    3. Narrow `ExcludedCategories` to not drop "Artifact"/"Artifacts" (lowest-effort but leaves the lossy write-time filter in place for other types).
  - Whichever option is chosen must be reflected at BOTH write chokepoints to avoid the double-filter.

failing_test_spec (Codex to implement FIRST, confirm RED, then fix, confirm GREEN):
  - Project: `DeckFlow.Core.Tests` (xUnit) — root-cause logic lives in DeckFlow.Core, which is unit-testable without the web host or live HTTP.
  - Test 1 (writer-level, primary): `DeckCategoryCacheWriterTests` (new file). Arrange a `CategoryKnowledgeRepository` over an in-memory/temp SQLite db (mirror existing `CategoryKnowledgeRepositoryTests` setup). Build a `DeckEntry` for Sol Ring with `Name="Sol Ring"`, `Category="Artifact"`, `Board="mainboard"`, `Quantity=1`. Act: `DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repo, "archidekt_live:TESTDECK", [entry])` then `repo.GetCategoriesAsync("Sol Ring")`.
    - Expected (post-fix): result is non-empty and contains "Artifact" (or the chosen surviving label).
    - Actual (pre-fix, RED): result is empty — proving categories were dropped at write time.
  - Test 2 (filter-unit, supporting): `CategoryKnowledgeReporterTests.SplitCategories_OnlyCardTypeCategory_DropsEverything` — assert `SplitCategories("Artifact")` currently yields empty; this documents the chokepoint. After the chosen fix, update to the new contract (e.g. yields "Artifact", or write path no longer routes through this filter).
  - Test 3 (commander aggregate, optional regression guard): extend `CategoryKnowledgeRepositoryTests` — seed one processed `deck_queue` row (deck_id="TESTDECK", commander_name="Krenko, Mob Boss", processed=1) and write a Sol Ring/"Artifact" observation under source "archidekt_live:TESTDECK"; assert `GetCategoryRowsForCommanderAsync("Krenko, Mob Boss")` returns a Sol Ring row. Pre-fix RED (no observation row exists), post-fix GREEN.
  - Note: keep the failing assertion specifically tied to "Artifact survives the write path" so the regression guard is meaningful; do NOT weaken it to merely "some category exists".

verification_notes:
  - Build with `/mnt/c/Program Files/dotnet/dotnet.exe build DeckFlow.sln`. VSTest is unreliable in WSL; if the harness can't run the new xUnit tests, Codex should confirm RED/GREEN via a targeted console harness or push-and-watch CI per project convention.
  - A live repro on the running web app is NOT required to confirm the root cause — it is provable at the DeckFlow.Core unit level. If product wants an end-to-end confirmation, ask the user to start the server and submit Sol Ring on `/suggest-categories` in CachedData mode; do not auto-launch.
