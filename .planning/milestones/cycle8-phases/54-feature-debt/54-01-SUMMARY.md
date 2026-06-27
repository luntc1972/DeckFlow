---
phase: 54-feature-debt
plan: 01
subsystem: web-services
tags: [commander-spellbook, deck-primer, combo-ranking, parsing]
requires:
  - SpellbookCombo record (CommanderSpellbookService)
  - BuildComboReferenceText ranking stub (DeckPrimerPacketService)
provides:
  - SpellbookCombo.Popularity / SpellbookCombo.ManaValueNeeded
  - Popularity-DESC / ManaValueNeeded-ASC combo ranking in Deck Primer
affects:
  - DeckFlow.Web combo lookup + Deck Primer prompt artifact
tech-stack:
  added: []
  patterns:
    - Tolerant top-level JSON scalar read via TryGetProperty + ValueKind + TryGetInt32
    - Stable LINQ ranking with null-coalescing fallbacks + index tiebreak
key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/CommanderSpellbookService.cs
    - DeckFlow.Web/Services/DeckPrimerPacketService.cs
    - DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs
    - DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs
    - README.md
decisions:
  - "Used TryGetInt32 (not GetInt32) so decimal/out-of-range/string ranking values degrade to null without throwing"
  - "Ranking: Popularity ?? 0 DESC, ManaValueNeeded ?? int.MaxValue ASC, then stable API index"
  - "Removed GetImmediacyRank entirely — it was the sole caller in the replaced stub"
metrics:
  duration: ~25m
  completed: 2026-06-17
  tasks: 2
  files: 5
  commits: 2
---

# Phase 54 Plan 01: SpellbookCombo Ranking Fields + Deck Primer Combo Ranking Summary

Capture the two combo ranking scalars the Commander Spellbook parser previously dropped (`popularity`, `manaValueNeeded`) onto the `SpellbookCombo` record and use them to priority-rank combos in the Deck Primer combo-reference block (popularity DESC, mana-value ASC tiebreak, stable API order), replacing the produces-immediacy/piece-count ranking stub (FEAT-02 / PRM-08).

## What Was Built

**Task 1 — Parse ranking fields (commit `0ef53d2`)**
- Added two additive positional params to `SpellbookCombo`: `int? Popularity = null`, `int? ManaValueNeeded = null` (positional record form preserved per carve-out; no reorder; `SpellbookAlmostCombo` untouched).
- `ParseVariants` now reads both fields top-level off each variant element using `TryGetProperty` + `ValueKind == JsonValueKind.Number` + `TryGetInt32(out var)`. A string, decimal, out-of-Int32-range, or absent value degrades to `null` and never throws / never fails the whole `ParseResponse`.
- Added 3 regression facts: present (5000/3), missing (both null), and malformed (string popularity + decimal mana + out-of-range integers → all null, combos still present, no throw).

**Task 2 — Rank Deck Primer combos (commit `beb8e6d`)**
- Replaced the `spikeVerdict == "sufficient"` inner LINQ chain in `BuildComboReferenceText` with: `.OrderByDescending(Popularity ?? 0).ThenBy(ManaValueNeeded ?? int.MaxValue).ThenBy(Index)`. The `?? 0` sinks unknown-popularity combos; `?? int.MaxValue` sinks unknown-cost combos; the trailing index keeps it stable (API order) when tied/absent.
- Removed the stale "Known limitation (31-03 scope fence)" comment block and replaced it with a one-line ranking explanation.
- Removed the now-unreferenced private `GetImmediacyRank` method (it was the sole caller in the replaced stub — re-grepped to 0 references before deleting).
- `ComboRankingVerdict = "sufficient"` constant and the branch guard left unchanged.
- Added 3 ranking regression facts: higher-popularity-first, equal-popularity → cheaper-mana-first, both-null → API order preserved.
- README: added one sentence to the Deck Primer combo-injection bullets describing popularity-DESC / mana-ASC ordering with API-order fallback.

## Build & Test Status

**Build-verified (primary WSL gate):** `dotnet build DeckFlow.sln` (Windows dotnet) — **Build succeeded, 0 errors**. The only build warning is a **pre-existing** `CS1574` (unresolved xmldoc cref `StageAndCommitAsync`) in `DeckFlow.Core/Orchestration/IContentIndexExporter.cs`, a file not touched by this plan. **0 new warnings.** Note: an initial build failed with `MSB3026/MSB3027/MSB3021` file-lock errors because `DeckFlow.Web` and `DeckFlow.Studio` dev servers were holding `DeckFlow.Core.dll`; these are post-build copy locks, not compilation errors. Stopped the two processes and the rebuild was clean.

**Tests run/passed (WSL VSTest worked this run):**
- `CommanderSpellbookServiceTests` filter: **9 passed, 0 failed** (includes 3 new parse facts).
- `DeckPrimerPacketServiceTests` filter: **14 passed, 0 failed** (includes 3 new ranking facts + the existing `RankingBranch_FallbackEmitsApiOrderInstruction`).
- Full `DeckFlow.Web.Tests`: **628 passed, 0 failed, 11 skipped** — confirms no construction-site regressions (PrimerPromptVariantTests, MetaGapServiceTests, DeckComparisonServiceTests all still compile/pass against the additive record).

**Format gate:** `scripts/format-check-changed.sh staged` exit 0 (clean) for both commits. LF line endings verified (0 CRLF) on all changed C# files. (Repo `core.hooksPath` is the default `.git/hooks`, so the gate is not auto-run on commit; ran it manually before each commit.)

## must_haves Coverage

| Truth | Status |
|-------|--------|
| ParseVariants captures popularity + manaValueNeeded when present | Met — `ParseVariants_PopularityAndManaValueNeeded_Parsed` (5000/3) |
| Omitted/malformed (string/decimal/out-of-range) → null, no throw, no whole-result failure | Met — `MissingRankingFields_DefaultsToNull` + `MalformedRankingFields_DegradeToNull` (both combos survive) |
| Higher-popularity combos listed before lower | Met — `RankingBranch_PopularityDESC_HigherPopularityFirst` |
| Popularity tie → cheaper (lower manaValueNeeded) first | Met — `RankingBranch_PopularityTie_CheaperManaFirst` |
| Both fields absent for all → stable API order preserved | Met — `RankingBranch_BothFieldsAbsent_PreservesApiOrder` |
| Non-primer consumers unaffected (additive only) | Met — full Web.Tests suite green; record params additive with null defaults; JSON artifact shape unchanged |

Artifacts: `int? Popularity` present in CommanderSpellbookService.cs; `OrderByDescending` on `Popularity` present in DeckPrimerPacketService.cs; `TryGetProperty("popularity"` present; `Known limitation` count 0; `GetImmediacyRank` count 0; bare `GetInt32()` count 0.

## Deviations from Plan

None functional. One environmental note: the first `dotnet build` failed on file-lock copy errors (MSB3026/27/21) from running dev servers, not code. Resolved by stopping `DeckFlow.Web`/`DeckFlow.Studio` processes (Rule 3 — blocking issue, environment-only); no code change required.

## Known Stubs

None. The 31-03 ranking stub this plan was scoped to replace is now removed.

## Threat Flags

None. No new security surface — `popularity`/`manaValueNeeded` come from the already-trusted (cached) Commander Spellbook API boundary, not user input, and the tolerant `TryGetInt32` parse is the explicit T-54-01/T-54-02 mitigation (hostile/oversized/decimal values degrade to null, never throw or fail the result). Additive record params keep the artifact shape (T-54-03) unchanged.

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/CommanderSpellbookService.cs (int? Popularity at line 20)
- FOUND: DeckFlow.Web/Services/DeckPrimerPacketService.cs (OrderByDescending Popularity at line 423)
- FOUND: DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs (3 new facts)
- FOUND: DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs (3 new facts)
- FOUND: README.md (popularity ranking sentence)
- FOUND: commit 0ef53d2
- FOUND: commit beb8e6d
