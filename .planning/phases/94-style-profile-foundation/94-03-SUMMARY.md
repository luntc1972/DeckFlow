---
phase: 94-style-profile-foundation
plan: 03
status: complete
requirements: [CS-04, CS-03]
executor: codex (gpt-5.4)
reviewer: claude
---

# Plan 94-03 Summary — round-trip tests (SQLite unconditional + gated Postgres)

## What was built

CS-04 both-dialect round-trip fidelity coverage for the CreatorStyleProfile substrate.

- **`DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`** — added `Testcontainers.PostgreSql` `3.10.0` (in-solution package, already used by DeckFlow.Web.Tests — no approval needed). `packages.lock.json` unchanged by restore.
- **`DeckFlow.Core.Tests/Integration/PostgresFactAttribute.cs`** — project-local gating `[PostgresFact]`; `Skip` unless env `DECKFLOW_POSTGRES_TESTS == "1"`.
- **`DeckFlow.Core.Tests/Integration/PostgresContainerFixture.cs`** — project-local Testcontainers fixture (postgres:16-alpine, db deckflow_tests, semaphore-gated lazy start, `GetConnectionStringOrSkipAsync` → SkipException when Docker/flag absent), mirrored from DeckFlow.Web.Tests.
- **`DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs`** — 8 unconditional SQLite `[Fact]`s: schema idempotency, unknown-slug null, full-shape round-trip (nested Distribution + Conflict asserted), below-floor `InsufficientSample=true` survival (CS-03), measured-only / stated-only / fused-only empty-not-null (D-07, `Assert.Empty`), re-upsert single-row overwrite with strictly-later `UpdatedUtc`.
- **`DeckFlow.Core.Tests/Integration/CreatorStyleProfileStorePostgresTests.cs`** — 5 gated `[PostgresFact]`s (`IClassFixture<PostgresContainerFixture>`, unique slug per test): full-shape, insufficient_sample survival, measured/stated/fused-only empty-not-null — the full D-07 trio on the Postgres dialect.

## Decisions honored

- CS-04 both dialects; CS-03 insufficient_sample survives; D-06 flag; D-07 measured-only/stated-only/fused-only empty-not-null trio proven on BOTH dialects; D-04 re-upsert refreshes updated_utc.
- **Locked override**: Postgres tests live in DeckFlow.Core.Tests with re-created project-local fixture + gating attribute (cross-project reuse impossible). SYNC-16 gating pattern (Cycle 16) reused.

## Verification

- `dotnet build DeckFlow.Core.Tests -c Debug`: 0 errors, 0 warnings.
- **`dotnet build DeckFlow.sln`: 0 errors, 0 warnings** (post-merge gate — no cross-plan integration breakage).
- **Full `DeckFlow.Core.Tests` run: 1234 passed, 0 failed, 5 skipped** — the 5 Postgres tests skip cleanly with `DECKFLOW_POSTGRES_TESTS` unset; all 8 new SQLite tests pass, no existing-test regressions.
- Scope: exactly the 5 intended files; unrelated `.planning` deletions untouched; lockfile unchanged. LF (0 CRLF). Package pinned 3.10.0.

## Commits

- `12a84358` test(94): add Testcontainers.PostgreSql + project-local Postgres test infra to Core.Tests
- `935e5f00` test(94): add unconditional SQLite round-trip tests for CreatorStyleProfileStore
- `2eaf7a0f` test(94): add gated Postgres round-trip tests for CreatorStyleProfileStore

## Follow-ups

- Postgres tests are opt-in; to prove them locally: `DECKFLOW_POSTGRES_TESTS=1` + Docker → 5 [PostgresFact]s pass against a real container (matches Cycle 16 SYNC-16 green run).

## Self-Check: PASSED
