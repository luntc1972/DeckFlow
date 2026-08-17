# Follow-up: prove Dapper column-type mappings against Postgres

**Raised:** 2026-08-17, out of the MEDIUM-1 fix review
(`.planning/reviews/2026-08-16-category-weights-code-gate.md` § MEDIUM-1 discharge).
**Status:** SCOPED, not started. Independent of the MEDIUM-1 fix — do not block that on this.
**Size:** small. One new test file, one env/settings change outside the repo.

## Why this exists

While reviewing the MEDIUM-1 fix, the new `CategoryDeckCountRow` DTO typed `SourceId` as `string`
against a `source_id INTEGER NOT NULL` column (`CategoryCacheSchema.cs:152`). **The full 4768-test
suite passed.** It passed because `DeckFlow.Core.Tests` is SQLite-backed and SQLite is dynamically
typed — Dapper happily coerced the integer into a string. Production runs Postgres
(`DECKFLOW_DATABASE_PROVIDER`), where Npgsql binds `int4` to a `string` property and throws at
runtime.

Caught by hand, on review. Nothing in the suite could have caught it.

### The general rule this establishes

> **A SQLite-backed test suite cannot prove a Dapper column-type mapping.**
> Provider-agnostic assertions about *values* gain nothing from Postgres.
> Assertions about *type binding* gain everything, and only Postgres can make them.

This is the third defect in this feature of the same shape — **two layers disagreeing about
identity**:

| Finding | Boundary | Layers disagreeing about |
|---|---|---|
| BLOCK-1 | SQL `GROUP BY` -> .NET dictionary | case sensitivity |
| MEDIUM-1 | raw label -> canonical key | alias identity, at which grain |
| this one | `INTEGER` column -> DTO property | CLR type |

## The infrastructure already exists — this is wiring, not building

Everything needed is in `DeckFlow.Web.Tests/Integration/`, already written and already paid for:

| Piece | Purpose |
|---|---|
| `PostgresContainerFixture.cs` | Testcontainers PostgreSQL container; lazy start, self-skips when Docker is absent |
| `PostgresFactAttribute.cs` | `[PostgresFact]` — skips unless `DECKFLOW_POSTGRES_TESTS=1` |
| `Testcontainers.PostgreSql` 3.10.0 | Already referenced by `DeckFlow.Web.Tests.csproj` |
| `DapperTypeHandlerRoundTripTests.cs` | Existing precedent — the closest analog; model the new test on it |

## Scope

### In scope

1. **One new `[PostgresFact]`** proving `GetCategoryDeckCountsAsync`'s row mapping binds under Npgsql:
   every `CategoryDeckCountRow` property (`SourceId`, `CardId`, `Board`, `Category`, `DeckCount`)
   round-trips from a real Postgres table without a cast exception. Assert on **binding**, not on the
   aggregation arithmetic — the SQLite tests already cover the math.
2. **Location:** `DeckFlow.Web.Tests/Integration/`, beside the existing Postgres tests. NOT
   `DeckFlow.Core.Tests` — see Constraints.
3. **Prove it red.** Temporarily revert `SourceId` to `string`, confirm the new test fails under
   Postgres with a bind/cast error, restore. A `[PostgresFact]` that has never failed is unproven.
4. **Turn on Docker Desktop's WSL integration** so the existing `[PostgresFact]` suite stops silently
   skipping. Settings change, outside the repo — user action.

### Out of scope

- Porting `CategoryKnowledgeRepositoryTests` (24 tests) to Postgres. Slow, and the value is in type
  binding, not in re-running arithmetic under a second engine.
- Any change to `DeckFlow.Core.Tests`' provider wiring or to `CreateRepository()`.
- Auditing every other DTO in the codebase for the same defect. Worth doing, but it is a separate
  sweep — see Related below.
- Making Postgres tests run in CI. Decide separately; CI has no Docker today.

## Constraints

- ⛔ **No new package references without explicit approval.** `DeckFlow.Core.Tests` has **no**
  `Testcontainers` reference, and adding one needs a decision. Putting the test in
  `DeckFlow.Web.Tests` avoids the question entirely — take that route unless told otherwise.
- `CategoryKnowledgeRepositoryTests.CreateRepository()` is `new(_databasePath)` — a SQLite-path
  constructor with **no provider seam**. Do not add one just for this; the Web.Tests route sidesteps it.
- `[PostgresFact]` + `PostgresContainerFixture` must both be used, so the test skips cleanly rather
  than failing when Docker is down.
- Repo conventions apply: `[Fact]`-family not `[Theory]` for the red proof, file-scoped namespaces,
  `sealed`, xmldoc, `// Why:` comments on non-obvious choices.
- Preserve each touched file's existing line endings; the repo default is LF.

## Acceptance criteria

- [ ] New `[PostgresFact]` exists in `DeckFlow.Web.Tests/Integration/`, modeled on
      `DapperTypeHandlerRoundTripTests`.
- [ ] With `DECKFLOW_POSTGRES_TESTS=1` and Docker up, it **passes**.
- [ ] With `SourceId` reverted to `string`, it **fails** with a bind/cast error — red proof recorded.
- [ ] With Docker down, it **skips** and the default suite stays green.
- [ ] Full suite still 4768+/0 with no new warnings.

## ⚠ Honest limit — read before assuming this closes the risk

**A skipped test is a green test.** Even wired up, this only catches the defect on machines where
Docker is running. It does not make the manual check unnecessary:

> When adding or editing a Dapper DTO property, verify its CLR type against the column type in
> `CategoryCacheSchema.cs` by hand.

If that guarantee matters more than the test does, the stronger move is a schema-vs-DTO assertion that
runs under SQLite too — reflect over the DTO, compare against the declared DDL. Larger job; not scoped
here. Flagged so the choice is deliberate rather than assumed.

## Related / next probes

- **Stage-2 sweep, still owed** from the same review: does any *other* consumer of
  `card_category_observations` pair a case-sensitive `GROUP BY` with a case-insensitive container?
  (`.planning/reviews/2026-08-16-category-weights-code-gate.md` § Stage 2.)
- **Codebase-wide DTO audit:** Codex checked `CardCategoryRepository.cs` only and found no other
  INTEGER→string mapping *in that file*. Every other repository DTO is unaudited.
- 2-viewport UI pass on the weighted table — separate open item on the same feature.
