# Phase 49: Dapper Data-Access Adoption - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-14
**Phase:** 49-dapper-data-access-adoption
**Areas discussed:** Column mapping strategy, Type-handler provider branching

---

## Area selection

| Area | Description | Discussed |
|------|-------------|-----------|
| Per-provider test mechanism | How to prove parity on both SQLite + Postgres | (left to research/planner) |
| Column mapping strategy | snake_case DB → PascalCase C# | ✓ |
| Type-handler provider branching | Single global handler self-detecting provider | ✓ |
| Wave grouping + gate mechanics | Convert order + FAIL-path halt | (left to research/planner) |

---

## Column mapping strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Global underscore matching | `DefaultTypeMap.MatchNamesWithUnderscores = true` once at init; clean SELECTs | ✓ |
| Explicit AS aliases | Alias every column in SQL; verbose, silent-null risk on new columns | |
| Per-type SetTypeMap | Custom column→property map per result type; most boilerplate | |

**User's choice:** Global underscore matching.
**Notes:** We own 100% of queries so the global flag is safe; avoids forgotten-alias silent nulls.

---

## Type-handler provider branching

| Option | Description | Selected |
|--------|-------------|----------|
| Self-detect on runtime type | Each handler branches in Parse/SetValue on value/parameter type; single global set, idempotent registration | ✓ |
| Ambient provider flag | Handlers read a startup-set provider; breaks when both providers run in one process | |
| Handlers for DateTime/decimal only | Let Dapper native-map bool/Guid; fails on SQLite (bool=int, Guid=text) | |

**User's choice:** Self-detect on runtime type.
**Notes:** Only robust option given Dapper keys handlers globally by CLR type; test suite exercises both providers in one process, so an ambient flag is unsafe. Registered once via thread-safe idempotent guard. Parity with today's exact coercion (incl. DateTime O-format) is the bar.

---

## Claude's Discretion

- File/namespace for type handlers + registration chokepoint.
- Anonymous-object params vs `DynamicParameters` per call site; `RETURNING` via `ExecuteScalarAsync<long>`.

## Deferred Ideas

None — discussion stayed within phase scope. Two in-scope areas (per-provider test mechanism, wave grouping) intentionally routed to research/planner rather than locked here; flagged as Open Items in CONTEXT.md.
