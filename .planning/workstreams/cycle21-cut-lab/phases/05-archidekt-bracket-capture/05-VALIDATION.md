---
phase: 05
slug: archidekt-bracket-capture
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-29
---

# Phase 05 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 / Microsoft.NET.Test.Sdk 17.14.1 |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`, `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Quick run command** | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ArchidektApiDeckImporterTests|FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests" --no-restore` |
| **Full suite command** | `dotnet.exe test DeckFlow.sln --no-restore` |
| **Estimated runtime** | ~30-180 seconds depending on suite selection |

---

## Sampling Rate

- **After every task commit:** Run the narrowest relevant focused test filter for changed files; keep this as the fast feedback loop.
- **After every plan wave:** Run `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore` and any changed Web test project slice.
- **Before `/gsd:verify-work`:** Full solution build and relevant test projects must be green.
- **Max focused feedback latency target:** 30 seconds for per-task filters. Slower Core/Web project slices, gated Postgres tests, and full-solution checks are wave/phase verification rather than per-task feedback.

---

## Per-Task Verification Map

Eight tasks across three plans and three waves (2 / 3 / 3). Task IDs are keyed `05-0P-0T`. The Round 3 fold added tests, not tasks — the store-level persistence test lands inside `05-03-03` and the URL-upsert D-10 test inside `05-02-01`/`05-02-02` — so the count stays eight and every new test is covered by an existing row's command.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | BRKT-01 | T-05-01, T-05-03, T-05-12 | Red tests pin one-request capture (`Assert.Equal(1, handler.RequestCount)`), try-parse-only malformed handling, captured-vs-absent, and `Metadata == null` for an unrecognizable payload | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektApiDeckImporterTests --no-restore` | Yes | pending |
| 05-01-02 | 01 | 1 | BRKT-01 | T-05-01, T-05-02, T-05-03, T-05-12 | Metadata parsed from the already-fetched payload; throwing default interface member never fabricates `CapturedUtc`; no metadata value can make `ImportAsync` throw | unit + build | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektApiDeckImporterTests --no-restore` then `dotnet.exe build DeckFlow.sln --no-restore` | Yes | pending |
| 05-02-01 | 02 | 2 | BRKT-02, BRKT-03 | T-05-05, T-05-06, T-05-13 | Red tests pin fresh + from-scratch legacy migration, three-state semantics, the two-step anti-wipe guarantee on both write paths, the D-10 URL-upsert partition (Test 5 all-non-null overwrite / Test 5c non-null record with a null field clears the stale value / Test 5b null record preserves), and the dialect-neutral parameter-type contract | unit/schema | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests|FullyQualifiedName~ArchidektDeckMetadataParametersTests" --no-restore` | Yes | pending |
| 05-02-02 | 02 | 2 | BRKT-02, BRKT-03 | T-05-04, T-05-06, T-05-07, T-05-13 | Additive nullable columns with no backfill; null metadata leaves captured columns untouched; the URL upsert gates per record via `CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL` rather than per-column `COALESCE` (D-10), with all six metadata columns present in the INSERT list so `excluded.*` is meaningful (F4-4); all parameters flow through `ArchidektDeckMetadataParameters.From` as `int?`/`string?` — **From-routing MUST be test-enforced (upgraded 2026-08-01): timestamps are caught by the `+00:00`-vs-`Z` rendering split, and `archidekt_theorycrafted` — previously caught by nothing runnable — is now provable via a `[PostgresFact]` against `PostgresContainerFixture`, since the gated suite runs green locally (2231/0/1, every `[PostgresFact]` executing). Add that test; F4-1's carry-forward applies only if it proves impossible, with the reason stated.** | unit/schema | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests|FullyQualifiedName~ArchidektDeckMetadataParametersTests" --no-restore` | Yes | pending |
| 05-02-03 | 02 | 2 | BRKT-02 | T-05-04, T-05-05, T-05-13 | Dialect parity gated by the dialect-independent parameter-type test; Postgres run is positive-proof-only and otherwise recorded as NOT VERIFIED on Postgres | unit + gated integration | `WSLENV="${WSLENV:+$WSLENV:}DECKFLOW_POSTGRES_TESTS/w" DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter FullyQualifiedName~PostgresStorageTests --no-restore` | Yes | pending |
| 05-03-01 | 03 | 3 | BRKT-01, BRKT-03 | T-05-08, T-05-09 | Red propagation tests pin bulk metadata write, unchanged-card-list metadata refresh, fresh-row skip nulls, URL metadata pass-through, and the exact D-09 banner string | unit/integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektDeckCacheSessionTests --no-restore` and `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter FullyQualifiedName~AdminHarvestControllerTests --no-restore` | Yes | pending |
| 05-03-02 | 03 | 3 | BRKT-01, BRKT-03 | T-05-08, T-05-10, T-05-11 | Bulk harvest forwards importer metadata (and nulls) without touching the card-list content hash; `ContentHashDedupTests` assertions unchanged | unit/integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ArchidektDeckCacheSessionTests|FullyQualifiedName~ContentHashDedupTests" --no-restore` | Yes | pending |
| 05-03-03 | 03 | 3 | BRKT-02, BRKT-03 | T-05-09, T-05-11, T-05-14 | URL path shares the repository metadata surface via a new 4-arg overload; 3-arg member untouched so out-of-scope implementers still compile; D-09 commander extraction with no backfill; **and the store→repository hop is proven, not assumed** — a `CategoryKnowledgeStoreTests` fact drives the real store's 4-arg overload and reads `archidekt_edh_bracket` / `archidekt_metadata_captured_utc` back out of `deck_queue` over a raw `SqliteConnection`, so an overload that silently drops the metadata argument turns this row red | unit + build | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~AdminHarvestControllerTests|FullyQualifiedName~CategoryKnowledgeStoreTests" --no-restore` then `dotnet.exe build DeckFlow.sln --no-restore` | Yes | pending |

*Status: pending / green / red / flaky*

Every task additionally runs `scripts/format-check-changed.sh staged` as its plan's final `<automated>` step; CI `format-gate` is the authoritative enforcer.

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. Add or extend tests in the existing xUnit projects; no new test framework is needed.

---

## Manual-Only Verifications

All phase behaviors should have automated verification. Manual production verification is limited to optional post-deploy SQL inspection of `deck_queue` metadata coverage and is not required for local phase completion.

**Postgres fallback note.** `[PostgresFact]` reads `DECKFLOW_POSTGRES_TESTS` from the Windows process environment (`DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs:14-18`), a WSL-side assignment does not cross into `dotnet.exe` without `WSLENV` — and the `WSLENV` entry needs the **`/w` flag** (`DECKFLOW_POSTGRES_TESTS/w`) to translate WSL→Win32; a bare entry fails the same silent way — and `.github/workflows/ci.yml` is out of scope for this phase, so the Postgres path is provable in **no** currently available environment. Only output showing `[PostgresFact]` tests **passed** counts as Postgres verification; a skipped or zero-test run is recorded as "**NOT VERIFIED on Postgres**" — never "skipped as expected" — and carried into the phase summary as an open production risk, since Render runs Postgres. The substitute gate that does run everywhere is `ArchidektDeckMetadataParametersTests`, which asserts every metadata SQL parameter is `int?`/`string?` and never `bool` or `DateTimeOffset`.

---

## Validation Sign-Off

- [x] All tasks have automated verify commands or existing test infrastructure
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target documented
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
