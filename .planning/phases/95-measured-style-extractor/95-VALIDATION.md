---
phase: 95
slug: measured-style-extractor
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-11
---

# Phase 95 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests / DeckFlow.Web.Tests) |
| **Config file** | none — existing test projects cover this phase |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~MeasuredStyle` |
| **Full suite command** | `dotnet test DeckFlow.sln` |
| **Estimated runtime** | ~60 seconds (Core) / longer full-solution |

---

## Sampling Rate

- **After every task commit:** Run the quick filtered command for the touched area.
- **After every plan wave:** Run the full suite command.
- **Before `/gsd:verify-work`:** Full suite must be green.
- **Max feedback latency:** ~60 seconds (Core filtered run).

---

## Per-Task Verification Map

*One row per task across all 7 finalized plans. Wave 0 test scaffolds are authored INLINE within their producing task (there is no separate Wave 0 plan), so `File Exists` reflects "created by this task".*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 95-01-01 | 01 | 1 | CS-10 | T-95-01-01 | Typed JSON nested field round-trips (no dynamic eval) | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorStyleProfileStoreTests"` | inline | ⬜ pending |
| 95-01-02 | 01 | 1 | CS-04a | T-95-01-02 | Dapper-parameterized store, no interpolated SQL | build | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` | inline | ⬜ pending |
| 95-01-03 | 01 | 1 | CS-04a | T-95-01-01 | Folder-weight map round-trips key-for-key | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorProfileSourceStoreTests"` | inline | ⬜ pending |
| 95-02-01 | 02 | 1 | CS-04b | T-95-02-01 | Cache writes only creator_deck_cache, never corpus tables | build | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` | inline | ⬜ pending |
| 95-02-02 | 02 | 1 | CS-04b | T-95-02-01 | Content-hash freshness + per-creator scoping isolation | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CreatorDeckCacheStoreTests"` | inline | ⬜ pending |
| 95-03-01 | 03 | 1 | CS-05 | — | Curated staple set membership deterministic | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ContentTagVocabularyTests"` | inline | ⬜ pending |
| 95-03-02 | 03 | 1 | CS-07 | T-95-03-01 | Server-side GROUP BY aggregate, no 322k raw-row pull | build | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` | inline | ⬜ pending |
| 95-03-03 | 03 | 1 | CS-07 | T-95-03-01 | Aggregate proven exact on hand-computed fixture | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"` | inline | ⬜ pending |
| 95-04-01 | 04 | 2 | CS-04c | T-95-04-03 | Host-agnostic contract (no HttpClient/Web refs) | build | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` | inline | ⬜ pending |
| 95-04-02 | 04 | 2 | CS-05, CS-04c | T-95-04-01 | Staple-strip + >105 drop before any ratio | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~StapleStripperTests"` | inline | ⬜ pending |
| 95-04-03 | 04 | 2 | CS-04d | T-95-04-02 | Graded folder weight + fractional effective sample | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~FolderWeightingTests"` | inline | ⬜ pending |
| 95-05-01 | 05 | 3 | CS-06 | T-95-05-03 | Multi-bucket counting, no `.First()`/`.Take(1)` | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CategoryCounterTests"` | inline | ⬜ pending |
| 95-05-02 | 05 | 3 | CS-07 | T-95-05-01, T-95-05-02 | Lift demotes staples; zero-baseline guard (no NaN) | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~LiftCalculatorTests"` | inline | ⬜ pending |
| 95-06-01 | 06 | 3 | CS-04a | T-95-06-02, T-95-06-03 | SSRF domain guard + hard MaxPages/MaxDecks cap | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~ArchidektOwnerClientTests"` | inline | ⬜ pending |
| 95-06-02 | 06 | 3 | CS-04c | T-95-06-01, T-95-06-05 | Filter-first >105 drop; no corpus-table writes | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | inline | ⬜ pending |
| 95-06-03 | 06 | 3 | CS-04b | T-95-06-04 | Content-hash read-through skips re-hitting Archidekt | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CreatorProfileDeckCrawlerTests"` | inline | ⬜ pending |
| 95-07-01 | 07 | 4 | CS-06, CS-08, CS-09 | T-95-07-01 | Null-graceful combo/Tagger fallback, multi-bucket | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | inline | ⬜ pending |
| 95-07-02 | 07 | 4 | CS-10 | T-95-07-03 | Every metric carries NumDecks + EffectiveSampleSize; insufficient_sample below floor | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | inline | ⬜ pending |
| 95-07-03 | 07 | 4 | CS-08, CS-09 | T-95-07-01, T-95-07-03 | End-to-end profile persists; null-combo does not throw | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~MeasuredStyleProfileBuilderTests"` | inline | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*No separate Wave 0 plan exists. The test scaffolds below are authored INLINE within their producing tasks (marked `tdd="true"` where the RED-first cycle applies), so `wave_0_complete` stays `false` by design — there is nothing to gate a distinct Wave 0 on.*

- [ ] Pure extraction-contract tests (staple-strip, category counting, lift math, folder weighting) — authored inline in Plans 04 (Tasks 2/3) and 05 (Tasks 1/2) in `DeckFlow.Core.Tests/MeasuredStyleExtraction/`.
- [ ] Corpus-aggregate fixture (deterministic 4-deck baseline) — authored inline in Plan 03 Task 3 (`CategoryKnowledgeRepositoryTests`). Full Snail 39-deck round-trip is a Manual-Only verification (below), not an inline scaffold (D-12).

*Existing xUnit infrastructure covers the framework; no install needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Archidekt `ownerUsername` crawl against a real creator | CS-04a/b | External API, non-deterministic, rate-limited | Run crawler harness against Salubrious Snail; confirm deck-ID list + folder tags returned |
| Full Snail 39-deck extractor round-trip (D-12) | CS-05/06/07 | Requires the real crawled corpus; too large/slow for a unit fixture | Crawl Snail, run MeasuredStyleProfileBuilder, sanity-check emitted MeasuredMetric[] (staples stripped, lift plausible) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies — every task carries a concrete `<automated>` command (build or filtered `dotnet test`); no `MISSING` references remain
- [x] Sampling continuity: no 3 consecutive tasks without automated verify — every task has an automated verify
- [x] Wave 0 covers all MISSING references — no separate Wave 0; test scaffolds authored inline within producing tasks (no `MISSING` refs anywhere in the 7 plans)
- [x] No watch-mode flags — all commands are single-shot `dotnet build` / `dotnet test --filter` (no `--watch`)
- [x] Feedback latency < 60s — filtered Core/Web runs are targeted single-suite executions
- [x] `nyquist_compliant: true` set in frontmatter
- [ ] `wave_0_complete` — intentionally `false`: no distinct Wave 0 exists (scaffolds inline)

**Approval:** approved 2026-07-11
