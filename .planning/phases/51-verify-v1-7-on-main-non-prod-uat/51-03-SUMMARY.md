# 51-03 Summary — Postgres parity suite (HARD-03)

**Status:** PASS — initial run found F-51-PG-01; fixed same-session, gated suite re-ran 19/19 · **Date:** 2026-06-17

Ran the Postgres-gated suite live against a Testcontainers `postgres:16-alpine` (Docker Desktop,
Windows `dotnet test`, `DECKFLOW_POSTGRES_TESTS=1`). Result: 19 tests, **16 passed, 3 failed**.

All Dapper type-handler round-trips (DateTimeOffset/DateTime/Bool/Decimal/Guid) pass on both
providers. The 3 failures all stem from one defect: `CategoryKnowledgeRepository.AddDeckIdsAsync`
(`CategoryKnowledgeRepository.cs:715`, CASE at 724/729) compares the **TEXT** column
`last_checked_utc` against a `DateTime` param Dapper/Npgsql binds as **timestamptz** →
`Npgsql 42883: operator does not exist: text <= timestamp with time zone`. SQLite tolerates it
(lexical text dates); Postgres rejects. High severity — affects deck re-queue on the live prod
Postgres path.

**Fixed same-session** (F-51-PG-01, commit `c4b625e`): `AddDeckIdsAsync` now casts
`last_checked_utc` to `timestamptz` in the comparison on Postgres only (dialect-guarded; SQLite
unchanged; no schema migration). Re-ran the gated suite → **19/19**; SQLite CategoryKnowledge tests
20/20 unchanged. HARD-03 CLOSED. Full detail: `51-PG-PARITY-RESULTS.md`.
