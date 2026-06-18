# Phase 51 — HARD-03 Postgres Parity Results

**Recorded:** 2026-06-17
**Plan:** 51-03
**Requirement:** HARD-03

## UPDATE 2026-06-17 — FIXED, HARD-03 now PASSES (19/19)

F-51-PG-01 was fixed in commit `c4b625e` (Claude-authored under the temporary override; Codex out of tokens, user-authorized). `AddDeckIdsAsync` now casts `deck_queue.last_checked_utc` to `timestamptz` in the comparison **on Postgres only** (`::timestamptz`, parses any stored text-date format → no backfill); SQLite keeps its lexical TEXT comparison (dialect-guarded). Re-ran the gated suite:

```
DeckFlow.Web.Tests — Integration — Total tests: 19   Passed: 19   Failed: 0
```
SQLite CategoryKnowledge/DeckQueue tests 20/20 unchanged. Changed-lines format gate clean. **HARD-03 CLOSED.** The original failure analysis below is retained for the record.

---

## Result: FAIL — real Postgres parity defect found (2 confirmed + 1 same-root-cause) [original run, now fixed]

The Postgres-gated suite was run live against a real Testcontainers `postgres:16-alpine`
container (Docker Desktop 29.4.3, Windows `dotnet test`, `DECKFLOW_POSTGRES_TESTS=1`).

```
DeckFlow.Web.Tests — filter FullyQualifiedName~Integration
Total tests: 19   Passed: 16   Failed: 3   (29.96s)
```

### Passed (16) — Dapper type-handler parity is clean

All `DapperTypeHandlerRoundTripTests` pass on BOTH Postgres and SQLite:
DateTimeOffset, DateTime, Bool, Decimal, Guid handlers round-trip correctly. The Phase 49
type-handler conversion itself is sound. `CategoryKnowledgeRepository_CrudAndDeckQueue_Roundtrips`
and `FeedbackStore_*_Roundtrips` also pass.

### Failed (3) — `CategoryKnowledgeRepository.AddDeckIdsAsync` breaks on Postgres

| Test | Error |
|------|-------|
| `PostgresStorageTests.CategoryKnowledgeRepository_CommanderRows_UseLiveSourceIntegerLink` (line 129) | `Npgsql.PostgresException : 42883: operator does not exist: text <= timestamp with time zone` |
| `PostgresStorageTests.CategoryKnowledgeRepository_NonLiveSources_DoNotEnterCommanderAggregateOrQueue` (line 160) | same `42883` |
| (3rd — truncated from captured tail) | same root cause: a third `AddDeckIdsAsync`-on-live-source path |

All three fail in the same place: `CategoryKnowledgeRepository.AddDeckIdsAsync`
(`DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:715`), in the `ON CONFLICT DO UPDATE`
`CASE` at lines 724 / 729.

## Finding F-51-PG-01 — DateTime param bound as timestamptz compared against TEXT column (Postgres)

**Severity:** High (any deck re-queue on a Postgres deployment throws; affects the live
prod Postgres path that Render uses).

**Root cause:**
- Schema (`CategoryKnowledgeRepository.cs:77,80`): `inserted_utc TEXT NOT NULL`, `last_checked_utc TEXT` — both columns are **TEXT** on both SQLite and Postgres.
- Query (`:724`, `:729`): `... OR deck_queue.last_checked_utc <= @requeueBeforeUtc THEN 0`.
- `@requeueBeforeUtc` (and `@insertedUtc`) are `DateTime` values (`:705-706`). Dapper/Npgsql binds a `DateTime` as **`timestamp with time zone`**.
- Postgres has no `text <= timestamptz` operator → `42883`. SQLite stores datetimes as TEXT and compares lexically, so the same SQL succeeds there — the divergence was invisible until this gated run.

**Why it slipped through:** Phase 49 (Dapper adoption) converted these calls; the SQLite
unit tests passed, and the Postgres-gated tests were never run as a release gate until now —
which is exactly the gap HARD-03 was created to close.

**Suggested fix (for a follow-up phase — NOT applied here):** make the parameter binding
match the TEXT column. Bind the cutoff/inserted values as ISO-8601 UTC **strings**
(e.g. `insertedUtc.ToString("O")` / a round-trip format) so the comparison is `text <= text`
on both providers; mirror whatever format the existing TEXT datetime columns already store
(verify `inserted_utc`/`last_checked_utc` write format elsewhere in the repo for consistency).
Alternatively migrate the columns to `timestamptz` on Postgres — heavier, touches prod data,
not recommended for a hardening cycle. Confirm the fix by re-running this gated suite to green.

**Disposition:** Captured as a follow-up finding. Per the phase contract (verification-only,
Codex-implements/Claude-reviews, and the Codex review-only window through 2026-06-18), the fix
is NOT applied in Phase 51. Recommend routing F-51-PG-01 to a Cycle 8 follow-up (fits the
Phase 53 architecture/Dapper burn-down or a dedicated hotfix plan).

## Reproduction

```
scripts\_run-pg-tests.bat   (sets DECKFLOW_POSTGRES_TESTS=1, runs DeckFlow.Web.Tests Integration via Windows dotnet)
```
Requires Docker Desktop running (Testcontainers pulls `postgres:16-alpine` and tears it down).
