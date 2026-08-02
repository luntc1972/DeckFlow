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

Task IDs are keyed `05-0P-0T`; the plans are the canonical source for task composition and gates.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | BRKT-01 | T-05-01, T-05-03, T-05-12 | Red tests pin one-request capture (`Assert.Equal(1, handler.RequestCount)`), try-parse-only malformed handling, captured-vs-absent, and `Metadata == null` for an unrecognizable payload | unit | See 05-01 Task 1's expected-failure harness in `05-01-PLAN.md`. | Yes | pending |
| 05-01-02 | 01 | 1 | BRKT-01 | T-05-01, T-05-02, T-05-03, T-05-12 | Metadata parsed from the already-fetched payload; throwing default interface member never fabricates `CapturedUtc`; no metadata value can make `ImportAsync` throw | unit + build | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektApiDeckImporterTests --no-restore` then `dotnet.exe build DeckFlow.sln --no-restore` | Yes | pending |
| 05-02-01 | 02 | 2 | BRKT-02, BRKT-03 | T-05-05, T-05-06, T-05-13 | Red tests pin fresh + from-scratch legacy migration, three-state semantics, the two-step anti-wipe guarantee on both write paths, the D-10 URL-upsert partition (Test 5 all-non-null overwrite / Test 5c non-null record with a null field clears the stale value / Test 5b null record preserves), and the dialect-neutral parameter-type contract | unit/schema | See 05-02 Task 1's expected-failure harness in `05-02-PLAN.md`. | Yes | pending |
| 05-02-02 | 02 | 2 | BRKT-02, BRKT-03 | T-05-04, T-05-06, T-05-07, T-05-13 | Implements Task 1's Tests 3-7 and Task 2's repository-write action, including D-10 per-record upsert gating. | unit/schema | See 05-02 Task 2's action and gates in `05-02-PLAN.md`. | Yes | pending |
| 05-02-03 | 02 | 2 | BRKT-02 | T-05-04, T-05-05, T-05-13 | Re-runs Task 2's exact TRX-checked Postgres fact as final dialect validation. | gated integration | See Task 3's gates in `05-02-PLAN.md`. | Yes | pending |
| 05-03-01 | 03 | 3 | BRKT-01, BRKT-03 | T-05-08, T-05-09 | Red propagation tests pin bulk metadata write, unchanged-card-list metadata refresh, fresh-row skip nulls, URL metadata pass-through, and the exact D-09 banner string | unit/integration | See 05-03 Task 1's expected-failure harness in `05-03-PLAN.md`. | Yes | pending |
| 05-03-02 | 03 | 3 | BRKT-01, BRKT-03 | T-05-08, T-05-10, T-05-11 | Bulk harvest forwards importer metadata (and nulls) without touching the card-list content hash; `ContentHashDedupTests` assertions unchanged | unit/integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ArchidektDeckCacheSessionTests|FullyQualifiedName~ContentHashDedupTests" --no-restore` | Yes | pending |
| 05-03-03 | 03 | 3 | BRKT-02, BRKT-03 | T-05-09, T-05-11, T-05-14 | URL path and store→repository persistence are verified per Task 3. The local raw readback asserts every metadata value, defeating a partial-forward mutation. | unit + build | See 05-03 Task 3's gates in `05-03-PLAN.md`. | Yes | pending |

*Status: pending / green / red / flaky*

Each code-changing task uses its own explicit file fence with `git add -- <owned paths>` before the staged formatter; see its plan gate. The fence stages no unrelated work, while the staged-nonempty C# assertion closes the empty-index false-green hole.

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. Add or extend tests in the existing xUnit projects; no new test framework is needed.

---

## Manual-Only Verifications

All phase behaviors should have automated verification. Manual production verification is limited to optional post-deploy SQL inspection of `deck_queue` metadata coverage and is not required for local phase completion.

Docker Desktop and `PostgresContainerFixture` make the required Postgres fact runnable locally. Task 2's TRX gate requires the named fact to pass and rejects skipped or not-executed outcomes; its WSLENV `/w` bridge is part of that canonical command. Docker must be running.

---

## Validation Sign-Off

- [x] All tasks have automated verify commands or existing test infrastructure
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target documented
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
