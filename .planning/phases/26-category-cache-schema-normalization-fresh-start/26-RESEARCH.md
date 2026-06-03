# Phase 26: Category Cache Schema Normalization (fresh-start) - Research

**Researched:** 2026-05-24
**Domain:** Relational schema design (Postgres 18 + SQLite dual-dialect) for a denormalized 4M-row analytics cache on a 256 MB RAM Render instance
**Confidence:** HIGH (root cause + current schema + read/write surface all verified directly in source; sizing measured on prod 2026-05-24)

## Summary

Phase 26 redesigns the category-knowledge cache from two wide, TEXT-keyed fact tables (`card_category_observations` ~3.9M rows / 1040 MB; `card_deck_totals` ~4.1M rows / 1039 MB) into a normalized, integer-keyed star schema. The root cause of the footprint is **width, not bloat** (autovacuum is keeping dead-tuple % near 0): `source` (~20-char `archidekt_live:{deckId}`), `card_name`, and `normalized_card_name` TEXT are repeated on every one of ~4M rows, and the composite multi-column TEXT primary keys cost ~456 MB of index each. On a `basic_256mb` plan the working set vastly exceeds RAM, so lookups are disk-bound.

The fix is to intern deck identity and card names into integer-keyed dimension tables (`decks`, `cards`) and slim the fact tables to reference `deck_id INT` + `card_id INT`. Because the user authorized a **full DB reset** (runbook `docs/ops/db-full-reset.md`) and chose schema-first, there is **no online migration of live rows** — the hardest risk is gone. The work is: design the new dual-dialect schema, port the write path (`DeckCategoryCacheWriter` / `CategoryKnowledgeRepository` persist methods) to populate dimensions + integer-keyed facts, port the read path (4 public read methods) to produce **identical** results, and confirm every hot query is index-backed via EXPLAIN.

**Primary recommendation:** Intern `source` → `decks.id` (reuse/extend `deck_queue` as the deck dimension keyed by an integer surrogate — it already holds `commander_name`/`processed`/`last_checked_utc`, eliminating the brittle `o.source = 'archidekt_live:' || q.deck_id` join) and intern card names → `cards(id, normalized_card_name UNIQUE, display_name)`. Keep `category` and `board` **inline as short TEXT** in the facts (do NOT over-intern — see Architectural Responsibility Map). Keep `card_deck_totals` as a separate slim fact table (do not derive at query time). Reserve a nullable `content_hash` column on the deck dimension for Phase 27 but do not populate it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Deck identity interning (`source` → int) | DB schema (`DeckFlow.Core/Storage` + `Knowledge`) | Write path (`CategoryKnowledgeRepository`) | The repeated TEXT `source` is the single biggest width cost; a deck dimension is the canonical owner. `deck_queue` already IS the deck dimension — extend it, do not add a parallel `decks` table. |
| Card name interning (`normalized_card_name`/`card_name` → int) | DB schema | Write path | Card names repeat millions of times; a `cards` dimension keyed by INT shrinks heap + makes lookups integer-equality. |
| Category/board representation | Fact tables (inline TEXT) | — | Low-leverage to intern (small per-row cost vs. join complexity); keeping inline preserves the simple `GROUP BY category` read path. |
| Result-parity of lookups | Read path (`CategoryKnowledgeRepository` read methods) | `CategoryFilter`/`CardNormalizer` (unchanged) | The 4 public read methods must return byte-identical results; normalization + filter helpers are reused verbatim. |
| Schema creation / drop | `EnsureSchemaAsync` (idempotent) + full-reset runbook | Ops (user runs the wipe) | Fresh-start: `EnsureSchemaAsync` builds new tables on a clean DB; old tables vanish with the schema drop. |
| Index strategy | DB schema | — | Compact secondary indexes back card-lookup and commander-aggregate; must be index scans (EXPLAIN). |

## Standard Stack

No new packages. Everything is in-repo and pinned by the deployed app.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Data.Sqlite | 10.0.0 | SQLite provider (dev + tests + default file DB) | Already the dual-dialect SQLite backend `[VERIFIED: CLAUDE.md tech stack + RelationalDatabaseConnection.cs]` |
| Npgsql | 10.0.0 | Postgres provider (prod via `DECKFLOW_DATABASE_PROVIDER=Postgres`) | Already the dual-dialect Postgres backend `[VERIFIED: RelationalDatabaseConnection.cs]` |
| `IRelationalDialect` pattern | in-repo | Dialect-specific DDL fragments (id column type, etc.) | The established extension point for dialect divergence `[VERIFIED: IRelationalDialect.cs]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Extending `deck_queue` as the deck dimension | A separate `decks(id, source)` table | A separate table duplicates deck identity already in `deck_queue`; the commander aggregate already joins `deck_queue`. Reusing it avoids a redundant 128K-row table and a fragile string-concat join. **Recommend extend `deck_queue` with an integer surrogate id.** |
| Separate `card_deck_totals` fact | Derive from observations via view/materialized rollup | See Pitfall + tradeoff section below — keep separate. |
| Interning category/board | Inline TEXT | Inlining is recommended; interning adds joins for negligible width savings. |

**Installation:** None — no `dotnet add package`. Per CLAUDE.md, no new packages without explicit user approval.

**Version verification:** Versions taken from CLAUDE.md tech-stack section and `RelationalDatabaseConnection.cs` imports `[VERIFIED: codebase]`. No registry lookup needed — no packages are being added.

## Package Legitimacy Audit

Not applicable — this phase installs **no external packages**. All work uses existing in-repo abstractions and already-referenced providers (Microsoft.Data.Sqlite 10.0.0, Npgsql 10.0.0). slopcheck was not run because no package is introduced.

## Architecture Patterns

### System Architecture Diagram (new normalized schema)

```text
                       Archidekt harvest
                              |
                   ArchidektDeckCacheSession.PersistDeckAsync
                              |  source = "archidekt_live:{deckId}"
                              v
                 DeckCategoryCacheWriter.ReplaceDeckEntriesAsync
                     (delete-then-insert per deck)
                              |
                              v
                  CategoryKnowledgeRepository (write path)
            +-------------------+-------------------+
            v                   v                   v
   [intern deck]        [intern card]        [write facts by INT]
   deck_queue           cards                card_category_observations
   (id PK surrogate,    (id PK,              (deck_id INT FK,
    deck_id TEXT UNIQUE, normalized_name      card_id INT FK,
    commander_name,      UNIQUE,              category TEXT,
    processed,           display_name)        board TEXT,
    last_checked_utc,                         count INT, deck_count INT,
    content_hash NULL                         last_seen_utc)
    <-Phase 27 reserve)                     card_deck_totals
                                              (deck_id INT FK,
                                               card_id INT FK,
                                               board TEXT,
                                               deck_count INT, last_seen_utc)

  READ PATHS (must be identical to today):
   GetCategoriesAsync(card)            -> join cards on normalized_name -> facts by card_id -> GROUP BY category
   GetCategoryRowsForCardAsync(card)   -> "" + SUM(count), SUM(deck_count), display_name from cards
   GetCardDeckTotalsAsync(card)        -> join cards -> card_deck_totals by card_id -> SUM by board
   GetCategoryRowsForCommanderAsync()  -> join deck_queue(commander) -> facts by deck_id -> cards for display_name
```

The diagram shows the data flow; file-to-responsibility mapping is in the Component table below.

### Recommended Schema (concrete DDL — dual-dialect)

Two dialect divergences only: the **surrogate-id column type** and **ON CONFLICT** (both already handled patterns). Everything else is identical SQL. Express the id-column divergence through a new `IRelationalDialect` member (mirroring `FeedbackIdColumnType`), not via string branching inside `EnsureSchemaAsync`.

**`IRelationalDialect` additions (recommended):**
- `SqliteRelationalDialect`: `SurrogateIdColumnType => "INTEGER PRIMARY KEY AUTOINCREMENT"` `[CITED: SqliteRelationalDialect.cs uses this exact form for FeedbackIdColumnType]`
- `PostgresRelationalDialect`: `SurrogateIdColumnType => "BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY"` `[CITED: PostgresRelationalDialect.cs uses this for FeedbackIdColumnType]`

> ⚠️ **SQLite caveat:** SQLite only allows `AUTOINCREMENT` on an `INTEGER PRIMARY KEY` column declared exactly that way; you cannot also declare a separate composite PK on the same table. Surrogate-keyed dimension tables are fine; fact tables use the surrogate id as PK and a UNIQUE constraint for the natural key. `[ASSUMED]` — confirm with a targeted SQLite harness (VSTest unreliable in WSL).

**`cards` dimension:**
```sql
CREATE TABLE IF NOT EXISTS cards (
    id {SurrogateIdColumnType},
    normalized_card_name TEXT NOT NULL,
    display_name TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_cards_normalized ON cards(normalized_card_name);
```
Interning key is `normalized_card_name` (the lookup key). `display_name` keeps the most-recent human form (read path returns `card_name` today — see parity note).

**`deck_queue` as deck dimension (extend existing table):** add an integer surrogate `id` plus the reserved hash. The natural key `deck_id TEXT` stays UNIQUE so the harvest UPSERT idiom (`ON CONFLICT(deck_id)`) keeps working unchanged.
```sql
-- on a FRESH DB this is the new shape; on existing DBs EnsureSchema would ALTER ADD.
CREATE TABLE IF NOT EXISTS deck_queue (
    id {SurrogateIdColumnType},
    deck_id TEXT NOT NULL,
    inserted_utc TEXT NOT NULL,
    processed INTEGER NOT NULL DEFAULT 0,
    skipped INTEGER NOT NULL DEFAULT 0,
    last_checked_utc TEXT,
    commander_name TEXT NULL,
    content_hash TEXT NULL     -- Phase 27 reserve; NOT populated this phase
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_deck_queue_deck_id ON deck_queue(deck_id);
```

**`card_category_observations` (slim fact):**
```sql
CREATE TABLE IF NOT EXISTS card_category_observations (
    id {SurrogateIdColumnType},
    deck_id INTEGER NOT NULL,      -- FK -> deck_queue.id (surrogate, NOT the TEXT deck_id)
    card_id INTEGER NOT NULL,      -- FK -> cards.id
    category TEXT NOT NULL,
    board TEXT NOT NULL DEFAULT 'mainboard',
    deck_count INTEGER NOT NULL DEFAULT 0,
    count INTEGER NOT NULL,
    last_seen_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_obs_grain
    ON card_category_observations(deck_id, card_id, category, board);
CREATE INDEX IF NOT EXISTS ix_obs_card ON card_category_observations(card_id);
CREATE INDEX IF NOT EXISTS ix_obs_deck ON card_category_observations(deck_id);
```
The old PK `(source, normalized_card_name, category, board)` becomes the UNIQUE grain index on `(deck_id, card_id, category, board)` — four INT/short columns instead of two wide TEXT + two TEXT. This is where the ≥50% index-footprint reduction (success criterion 2) comes from.

**`card_deck_totals` (slim fact):**
```sql
CREATE TABLE IF NOT EXISTS card_deck_totals (
    id {SurrogateIdColumnType},
    deck_id INTEGER NOT NULL,      -- FK -> deck_queue.id
    card_id INTEGER NOT NULL,      -- FK -> cards.id
    board TEXT NOT NULL DEFAULT 'mainboard',
    deck_count INTEGER NOT NULL DEFAULT 0,
    last_seen_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_totals_grain ON card_deck_totals(deck_id, card_id, board);
CREATE INDEX IF NOT EXISTS ix_totals_card ON card_deck_totals(card_id);
```

> **Foreign keys:** declaring `REFERENCES` is optional. SQLite does NOT enforce FKs unless `PRAGMA foreign_keys=ON` per-connection; Postgres always enforces. For a write-heavy harvest cache the simplest dual-dialect-safe choice is to **document the relationship via index/naming but NOT declare hard FK constraints** (avoids enforcement-divergence between dialects and FK-check overhead on 4M inserts). `[ASSUMED — recommend; confirm with the planner/Codex]`

### Pattern 1: Intern-on-write (get-or-insert dimension id)
**What:** Before writing a fact row, resolve `card_id`/`deck_id` via get-or-insert against the dimension.
**When to use:** In the per-deck write path (`PersistObservedCategoriesAsync` / `PersistCardDeckTotalsAsync` / `ReplaceSourceRowsAsync`).
**Example (dialect-safe upsert returning id):**
```sql
-- Source: pattern mirrors existing crawl_state UPSERT (CategoryKnowledgeRepository.cs:787)
INSERT INTO cards (normalized_card_name, display_name)
VALUES (@normalized, @display)
ON CONFLICT(normalized_card_name) DO UPDATE SET display_name = excluded.display_name
RETURNING id;
```
`RETURNING id` works on both SQLite (3.35+) and Postgres `[CITED: SqliteRelationalDialect.FeedbackInsertReturningIdSql uses RETURNING id]`. Cache the id in-process per harvest run to avoid a round-trip per row (the deck dimension id is stable for the whole deck; card ids can be memoized in a `Dictionary<string,long>`).

### Anti-Patterns to Avoid
- **String-concat join `o.source = 'archidekt_live:' || q.deck_id`** (current `GetCategoryRowsForCommanderAsync`, line 304): this is non-sargable and re-derives identity. Replace with `o.deck_id = q.id` integer join. This is a correctness+performance win the normalization unlocks.
- **Per-row dimension lookups without memoization:** would add 2 extra round-trips per fact row across millions of inserts. Memoize within the write session.
- **Hard FK constraints differing per dialect:** SQLite won't enforce them by default, masking bugs that Postgres rejects. Keep enforcement uniform (either both off via no-constraint, or accept Postgres-only enforcement knowingly).
- **Putting layout/schema branching as `if (IsSqlite)` strings inside `EnsureSchemaAsync`:** use the `IRelationalDialect` member instead (project convention).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dialect id-column divergence | `if (IsSqlite) "INTEGER..." else "BIGINT..."` inline | New `IRelationalDialect.SurrogateIdColumnType` member | Established project pattern (`FeedbackIdColumnType`); keeps DDL one string |
| Card-name normalization | New normalizer | `CardNormalizer.Normalize` (unchanged) | Parity depends on identical normalization; reuse verbatim |
| Category include/fallback filtering | New filter | `CategoryFilter.IncludedOrFallback` + `FilterGenericCategoryRowsWithFallback` | Read-result parity requires identical filtering — reuse the existing helpers |
| Upsert-returning-id | Manual SELECT-then-INSERT race | `INSERT ... ON CONFLICT ... RETURNING id` | Dialect-safe, already used in repo; avoids race in concurrent harvest |
| Big-table COUNT | `SELECT COUNT(1)` on facts | Existing `reltuples` fast-path (CategoryKnowledgeStore.cs:126) | Already optimized; the new `card_category_observations` keeps the same name so `to_regclass('public.card_category_observations')` still works |

**Key insight:** The whole phase is a re-shaping of *existing, well-understood* SQL. The risk is in **parity** and **dual-dialect DDL**, not in inventing algorithms. Reuse every normalization/filter helper untouched so read results are provably identical.

## Runtime State Inventory

This is a rename/refactor-adjacent phase (schema reshape + fresh-start cutover). The 5 categories:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | Postgres prod (`dpg-d7oj8...`): `card_category_observations` ~3.9M rows, `card_deck_totals` ~4.1M rows, `deck_queue` ~128K rows. SQLite `category-knowledge.db` under `MTG_DATA_DIR`/artifacts (dev + any non-PG deploy). | **Data migration: NONE** (fresh-start — full wipe + re-harvest per `docs/ops/db-full-reset.md`). Code edit: new schema only. |
| Live service config | Render Postgres connection via `DECKFLOW_DATABASE_PROVIDER` / `DECKFLOW_DATABASE_CONNECTION_STRING` env vars (Render dashboard, `sync:false`). Out-of-band index doc `docs/ops/cat-01-suggestion-indexes.md` — superseded once indexes are part of `EnsureSchemaAsync`. | None to env. Update/retire `cat-01-suggestion-indexes.md` reference (indexes now schema-created on clean DB). |
| OS-registered state | None — no Task Scheduler / launchd / pm2 entries reference these table names. | None — verified by scope (web app + harvest only). |
| Secrets / env vars | `DECKFLOW_DATABASE_CONNECTION_STRING` (unchanged — connection string, not schema). No secret encodes table/column names. | None. |
| Build artifacts | None — no generated code or egg-info equivalents reference the schema; SQL is inline raw strings in `.cs`. The full-reset runbook's `TRUNCATE` table list (db-full-reset.md:54) names old tables and **must be updated** to the new table set after redesign. | Update the runbook's table list (doc-only). |

**The canonical question:** After every file is updated, what still has the old shape? → Only the **live Postgres data** (handled by the authorized wipe) and the **runbook's TRUNCATE list** (doc edit). No silent caches.

## Common Pitfalls

### Pitfall 1: SQLite AUTOINCREMENT + composite PK conflict
**What goes wrong:** Trying to declare both `INTEGER PRIMARY KEY AUTOINCREMENT id` and a composite `PRIMARY KEY (...)` on the same table fails in SQLite (only one PK allowed).
**Why:** SQLite reserves `INTEGER PRIMARY KEY` for the rowid alias.
**How to avoid:** Surrogate `id` is the PK; enforce the natural grain with a separate `CREATE UNIQUE INDEX`. The DDL above already does this.
**Warning signs:** "table has more than one primary key" / syntax error on `EnsureSchemaAsync` against SQLite.

### Pitfall 2: Result non-parity from display-name drift
**What goes wrong:** Read methods return `card_name` (display) today. With a `cards` dimension storing one `display_name` per normalized name, a card that appeared under two display spellings collapses to one — changing returned `card_name`.
**Why:** Interning de-duplicates by normalized name; old design stored display per (source,card).
**How to avoid:** Define a deterministic `display_name` rule (last-writer-wins via the upsert `DO UPDATE SET display_name = excluded.display_name`, matching the old `card_name = excluded.card_name` behavior on line 524/562). Document it. The fixed parity sample (Sol Ring + one commander) must match.
**Warning signs:** Parity test diff on the `CardName`/`displayName` column.

### Pitfall 3: `{ get; init; }` JSON gotcha (CLAUDE.md)
**What goes wrong:** System.Text.Json silently skips get-only properties in .NET 9+; converting `{ get; init; }` to `{ get; }` has broken `EdhTop16Client` before.
**Why:** Any new result record/DTO this phase adds must keep `init` setters.
**How to avoid:** New records (e.g., a `CardDimension` row record if added) use `{ get; init; }` or `required ... { get; init; }`. Never let a formatter strip `init`. CLAUDE.md forbids that conversion.

### Pitfall 4: Raw-string SQL literal indentation
**What goes wrong:** Re-indenting C# raw-string (`"""`) SQL literals changes the shipped SQL; CLAUDE.md explicitly forbids reformatting these.
**How to avoid:** When adding/editing the inline SQL, match existing indentation exactly; do NOT run Format Document / Code Cleanup. Touch only the lines that change.

### Pitfall 5: VSTest unreliable in WSL
**What goes wrong:** `dotnet test` may hang/fail spuriously in WSL.
**How to avoid:** Verify via `dotnet build` clean (use `/mnt/c/Program Files/dotnet/dotnet.exe` per project memory) + a targeted manual harness or push-and-watch CI. The Postgres integration tests are gated behind a `PostgresContainerFixture`/`[PostgresFact]` that skips when no container — they won't run locally without Docker. `[VERIFIED: PostgresStorageTests.cs uses IClassFixture<PostgresContainerFixture> + [PostgresFact]]`

### Pitfall 6: `EnsureSchemaAsync` idempotency on the extended `deck_queue`
**What goes wrong:** On a fresh DB the new `deck_queue` has `id` + `content_hash`; the existing `EnsureDeckQueueColumnsAsync` ALTER-ADD logic (lines 114-136) must not double-add or conflict.
**How to avoid:** For the fresh-start the simplest path is the new `CREATE TABLE` shape; keep the column-probe ALTER logic for `content_hash`/`id` additive and guarded by the existing `GetTableColumnsAsync` check. Note: adding an `INTEGER PRIMARY KEY AUTOINCREMENT` via `ALTER TABLE ADD COLUMN` is **not possible in SQLite** — fresh-start avoids this, but a non-reset SQLite dev DB would need a rebuild. Since this is fresh-start, document that dev SQLite DBs should be deleted, not migrated.

## Code Examples

### Card lookup (GetCategoriesAsync) — ported to integer join
```sql
-- new: join cards dimension on normalized name, then facts by card_id
SELECT o.category
FROM card_category_observations o
JOIN cards c ON c.id = o.card_id
WHERE c.normalized_card_name = @normalized
GROUP BY o.category
ORDER BY LOWER(o.category), o.category;
-- index: ux_cards_normalized (point lookup) -> ix_obs_card (card_id) => index scans
```

### Commander aggregate (GetCategoryRowsForCommanderAsync) — string-concat join removed
```sql
SELECT o.category, c.display_name AS card_name,
       SUM(o.count) AS total, COUNT(DISTINCT q.id) AS deck_total
FROM card_category_observations o
JOIN deck_queue q ON q.id = o.deck_id          -- integer join (was: o.source = 'archidekt_live:'||q.deck_id)
JOIN cards c ON c.id = o.card_id
WHERE LOWER(q.commander_name) = LOWER(@commanderName)
  AND q.processed = 1
GROUP BY o.category, c.display_name
ORDER BY total DESC, LOWER(o.category), o.category;
```

### Verify index scans (run on Postgres after re-harvest sample)
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT o.category FROM card_category_observations o
JOIN cards c ON c.id = o.card_id
WHERE c.normalized_card_name = 'sol ring' GROUP BY o.category;
-- success criterion 3: expect Index Scan / Index Only Scan, not Seq Scan
```

## State of the Art

| Old Approach | Current (new) Approach | When Changed | Impact |
|--------------|------------------------|--------------|--------|
| Wide TEXT-keyed facts (`source`, `card_name`, `normalized_card_name` per row) | Integer-keyed star: `decks`(=`deck_queue`) + `cards` dims, INT FKs on facts | This phase | ~456 MB → small INT indexes; working set fits 256 MB → fast |
| Composite TEXT PK `(source, normalized_card_name, category, board)` | Surrogate `id` PK + UNIQUE grain index on INT/short cols | This phase | Index width cut ≥50% per success criterion |
| `o.source = 'archidekt_live:'\|\|q.deck_id` join | `o.deck_id = q.id` integer join | This phase | Sargable, indexable, no string build per row |
| Out-of-band `CREATE INDEX CONCURRENTLY` (cat-01 doc) | Indexes in `EnsureSchemaAsync` on a clean DB | This phase | No giant-table concurrent build needed post-reset |

**Deprecated/outdated:** `docs/ops/cat-01-suggestion-indexes.md` (out-of-band index build) is superseded for the new schema — indexes ship in `EnsureSchemaAsync` and build instantly on an empty table.

## Result-Parity: Read Methods to Port

Every method below MUST return identical results to today for the same data. All live in `CategoryKnowledgeRepository.cs` and are surfaced via `CategoryKnowledgeStore` → `ICategoryKnowledgeStore`.

| Method | Current grain/source | New query | Callers |
|--------|----------------------|-----------|---------|
| `GetCategoriesAsync(card)` | `WHERE normalized_card_name=@n GROUP BY category` | join `cards` by normalized, facts by `card_id` | `CategorySuggestionService:114` |
| `GetCategoryRowsForCardAsync(card, board?)` | `SUM(count)`, `SUM(deck_count)`, `card_name`, optional board | + `cards.display_name` as card_name | `CategoryKnowledgeStore.GetCategoryRowsAsync` |
| `GetCardDeckTotalsAsync(card, board?)` | `card_deck_totals` SUM by board | join `cards`, facts by `card_id` | `CategorySuggestionService:118` |
| `GetCategoryRowsForCommanderAsync(commander)` | concat-join `deck_queue`, `SUM(count)`, `COUNT(DISTINCT q.deck_id)` | integer join `q.id=o.deck_id`, `COUNT(DISTINCT q.id)` | `CommanderCategoryService:54` |
| `GetCommanderDeckCountAsync` / `GetPagedProcessedCommanderRowsAsync` / `GetDistinctProcessedCommanderCountAsync` / `GetTopCommandersAsync` | `deck_queue` only — **unchanged** (no fact join) | unchanged | admin grid, stats |
| `GetTotalObservationCountAsync` | `reltuples` of `card_category_observations` (name kept) | unchanged | harvest stats |

**Write methods to port:** `ReplaceSourceRowsAsync`, `PersistObservedCategoriesAsync`, `PersistCardDeckTotalsAsync`, `DeleteSourceDataAsync`, `HasSourceDataAsync` — all key off `source` TEXT today and must intern to `deck_id`. `DeckCategoryCacheWriter.PersistDeckEntriesAsync` is the caller; `ArchidektDeckCacheSession` constructs `source="archidekt_live:{deckId}"` — that string can stay as the deck natural key resolved to `deck_id` once.

**Test doubles to update:** `FakeCategoryKnowledgeStore` (Web.Tests) + 2 other inline fakes implement `ICategoryKnowledgeStore` — if the interface signature is unchanged (recommended: keep it stable, change only the SQL underneath), fakes need **no** change. Repository-level tests (`CategoryKnowledgeRepositoryTests`, `DeckCategoryCacheWriterTests`, `ArchidektDeckCacheSessionTests`, `PostgresStorageTests`) seed via the write path and assert via the read path — they should pass unchanged IF parity holds, which makes them the parity guard.

## Forward-Compat with Phase 27 (content-hash dedup)

Phase 27 (`deck-cache-content-hash-refresh.md`) adds a per-deck `content_hash` to skip rewriting unchanged decks. Phase 26 must **reserve, not implement**:
- Add `content_hash TEXT NULL` to the deck dimension (`deck_queue`) in the new schema. Leave it NULL; do not compute or read it this phase.
- Phase 27 also wants the refresh cooldown moved from 1→5 days and the requeue predicate keyed on `last_checked_utc` — **out of scope here**, do not touch `DeckRefreshCooldown` (line 18) or `AddDeckIdsAsync`.
- Keep `ReplaceDeckEntriesAsync` delete+reinsert semantics intact (Phase 27 gates it; Phase 26 just re-points it at integer keys).

## Open Questions

1. **Reuse `deck_queue` as the deck dimension vs. a dedicated `decks` table.**
   - Know: `deck_queue` already holds deck_id, commander_name, processed, last_checked_utc and is already joined by the commander aggregate.
   - Unclear: whether the planner wants the harvest-queue concern and the deck-dimension concern in one table (SRP tension per CLAUDE.md SOLID).
   - Recommendation: **extend `deck_queue`** (add integer `id`). It is already the de-facto deck dimension; a parallel `decks` table duplicates identity. If the planner prefers separation, a `decks(id, deck_id UNIQUE)` table with `deck_queue` FK is acceptable but adds a join. Flag for plan decision.

2. **Hard FK constraints on facts?** Recommend NO declared FKs (uniform dual-dialect behavior, no 4M-insert FK-check cost). Planner/Codex to confirm.

3. **Display-name collapse rule** (Pitfall 2): last-writer-wins matches current `card_name = excluded.card_name`. Confirm acceptable for parity sample.

4. **Should `card_deck_totals` survive?** See tradeoff: **keep it** — deriving it from observations (`COUNT(DISTINCT deck_id) per card/board`) at query time would scan the largest table on every card lookup on a RAM-starved instance; a slim 2-INT-keyed totals table is far cheaper to read than a 256 MB-constrained aggregate. A materialized rollup adds refresh complexity for marginal gain on a fresh-start. Keep separate, slimmed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/verify | ✓ (per project) | net10.0 | — (use `/mnt/c/Program Files/dotnet/dotnet.exe`) |
| Postgres (prod) | live schema + EXPLAIN verify | ✓ (Render, read-only MCP) | PG 18 | SQLite local for unit verify |
| Docker (PG integration tests) | `PostgresContainerFixture` | unknown in WSL | — | `[PostgresFact]` auto-skips; rely on SQLite + push-and-watch CI |
| slopcheck | package audit | n/a | — | no packages added |

**Missing dependencies with no fallback:** None blocking — schema/code change.
**Missing with fallback:** Docker for PG integration tests → tests skip; verify Postgres path via push-and-watch CI / prod EXPLAIN.

## Validation Architecture

> `.planning/config.json` not inspected for `nyquist_validation`; treating as enabled (default).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 `[VERIFIED: CLAUDE.md tech stack]` |
| Config file | none (xUnit convention) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` (build-clean gate; VSTest unreliable in WSL) |
| Full suite command | push-and-watch CI, or `dotnet test DeckFlow.Core.Tests` where the WSL runner cooperates |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DBO-01 | New schema: integer-keyed dims + slim facts | unit (schema create on SQLite) | repo round-trip test seeds via write, reads back | ✅ `CategoryKnowledgeRepositoryTests` (extend) |
| DBO-01 | `GetCategoriesAsync` parity (Sol Ring) | unit | existing repo tests | ✅ extend with fixed-sample assertion |
| DBO-01 | `GetCategoryRowsForCommanderAsync` parity | unit | `CategoryKnowledgeRepositoryTests:260` already covers commander | ✅ keep green = parity proof |
| DBO-01 | Index scans (EXPLAIN) | manual/CI | EXPLAIN on prod after re-harvest sample | ❌ manual (SC #3) |
| DBO-01 | Postgres dual-dialect DDL | integration | `PostgresStorageTests` `[PostgresFact]` | ✅ extend (Docker-gated) |

### Sampling Rate
- **Per task commit:** `dotnet build` clean (no new warnings — CLAUDE.md Definition of Done).
- **Per wave merge:** Core + Web test suites (CI if WSL VSTest flaky).
- **Phase gate:** build clean + Core/Web tests green (except known `AdminCssPhase1Tests` debt) + manual EXPLAIN on a re-harvested sample showing index scans.

### Wave 0 Gaps
- [ ] Extend `CategoryKnowledgeRepositoryTests` with a fixed-sample parity assertion (Sol Ring categories + one commander aggregate) that survives the schema swap — this IS success criterion 3.
- [ ] Add a dimension-interning test (same card across 2 decks → one `cards` row, two facts).
- [ ] Confirm `PostgresStorageTests` covers the new DDL (Docker-gated; CI).
- *(No new framework needed — xUnit + fixtures already present.)*

## Security Domain

> `security_enforcement` config not located; including a minimal pass since this touches a DB write/read path.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | All SQL is parameterized via `RelationalDatabaseConnection.AddParameter` — keep it; never string-interpolate card/commander names into SQL. The only `string.Format` use (boardFilter clause) injects a constant fragment, not user data — preserve that boundary. |
| V6 Cryptography | no | No crypto here (content_hash is Phase 27, and is a non-security digest). |
| V2/V3/V4 Auth/Session/Access | no | No auth surface changes; admin grid auth unchanged. |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via card/commander name | Tampering | Parameterized queries only (existing pattern) — verify every new query uses `AddParameter`, not interpolation |
| Resource exhaustion (unbounded fact growth) | DoS | Out of scope (Phase 27/retention); note: normalization reduces per-row footprint, partial relief |

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — full current schema, all read/write SQL, EnsureSchemaAsync, index creation
- `DeckFlow.Core/Knowledge/DeckCategoryCacheWriter.cs` — write path (delete+reinsert)
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — `source="archidekt_live:{deckId}"`, PersistDeckAsync
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` + `ICategoryKnowledgeStore.cs` — store facade, interface surface, reltuples fast-path
- `DeckFlow.Core/Storage/{IRelationalDialect,SqliteRelationalDialect,PostgresRelationalDialect,RelationalDatabaseConnection}.cs` — dual-dialect id-column pattern (`FeedbackIdColumnType`), `RETURNING id`
- `DeckFlow.Core/Normalization/CardNormalizer.cs` — normalization rule (parity-critical)
- `.planning/research/db-storage-query-optimization.md` — prod measurements + design decision (fresh-start)
- `.planning/specs/deck-cache-content-hash-refresh.md` — Phase 27 forward-compat (`content_hash`)
- `docs/ops/db-full-reset.md` — authorized wipe runbook + TRUNCATE list to update
- `CLAUDE.md` (project) — constraints: dual-dialect, no new packages, `{get;init;}` gotcha, no reformatting, VSTest-WSL, commit/author rules

### Secondary (MEDIUM confidence)
- Read-path callers verified via grep: `CommanderCategoryService.cs:54,57`, `CategorySuggestionService.cs:114,118`
- Test doubles: `FakeCategoryKnowledgeStore.cs` + repo/PG tests (`CategoryKnowledgeRepositoryTests`, `DeckCategoryCacheWriterTests`, `ArchidektDeckCacheSessionTests`, `PostgresStorageTests`)

### Tertiary (LOW confidence)
- SQLite `INTEGER PRIMARY KEY AUTOINCREMENT` + composite-PK restriction `[ASSUMED]` — verify with targeted harness
- FK-enforcement divergence recommendation `[ASSUMED]` — confirm with planner/Codex

## Project Constraints (from CLAUDE.md)

- **Codex implements, Claude reviews.** This research feeds a PLAN that Codex executes; Codex must list required test cases. Plan goes to Codex for peer review before execute.
- **No new packages** without explicit user approval — none needed here.
- **Dual-dialect storage** (SQLite + Postgres) is mandatory — every DDL/SQL change must work on both via `IRelationalDialect`.
- **Do NOT run Format Document / Code Cleanup / ReSharper reformatting.** Touch only changed lines. Never convert `{ get; init; }` → `{ get; }`. Never re-indent raw-string SQL literals.
- **Commits:** plain default-author (`luntc1972`), no Co-Authored-By trailer; one logical change per commit; README updated when behavior changes.
- **Testing:** xUnit; VSTest unreliable in WSL → `dotnet build` clean + targeted harness / push-and-watch CI. Build via `/mnt/c/Program Files/dotnet/dotnet.exe`.
- **Migrations:** existing committed migrations immutable — but this project uses runtime `EnsureSchemaAsync`, not migration files. Schema change is via `EnsureSchemaAsync` on a fresh DB (authorized reset).
- **Do Not Modify without permission:** lockfiles, `.gitignore`, infra/deploy config (`Dockerfile`, `render.yaml`, `fly.toml`). The full-reset runbook doc edit (TRUNCATE list) is allowed (it's a planning/ops doc, not infra config).
- **Public repo:** no secrets in commits. Connection string stays in Render dashboard.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | SQLite forbids `INTEGER PK AUTOINCREMENT` + separate composite PK on same table | Pitfall 1 / DDL | Schema DDL fails on SQLite; mitigated by surrogate-id-PK + UNIQUE-index design (correct regardless) |
| A2 | No declared FK constraints is the right dual-dialect choice | DDL / Open Q2 | Postgres would enforce, SQLite wouldn't; if planner wants FKs, declare with explicit dialect note |
| A3 | Last-writer-wins `display_name` preserves read parity | Pitfall 2 | Parity sample diff on card_name column |
| A4 | Reusing `deck_queue` as deck dimension is preferred over separate `decks` table | Open Q1 | SRP critique in review; both designs documented |
| A5 | Keeping the table name `card_category_observations` preserves the `reltuples` fast-path | Don't Hand-Roll | If renamed, `to_regclass('public.card_category_observations')` returns NULL → COUNT fallback (slow but correct) — keep the name |
| A6 | Interface `ICategoryKnowledgeStore` signature stays unchanged (SQL-only change) → fakes need no edit | Test doubles | If signature changes, 4 implementors break (documented prior observation #1548) |
| A7 | `RETURNING id` works on the deployed SQLite 10.0.0 + Postgres 18 | Pattern 1 | Already used for feedback insert — high confidence, but confirm SQLite ≥3.35 in Microsoft.Data.Sqlite 10 |

## Metadata

**Confidence breakdown:**
- Schema design: HIGH — current schema + sizing + read/write surface all read directly; design follows textbook star-schema interning.
- Dual-dialect DDL: MEDIUM — id-column + ON CONFLICT + RETURNING patterns verified in repo; SQLite AUTOINCREMENT+PK and FK-enforcement specifics flagged ASSUMED pending harness.
- Parity: HIGH — every read/write method and its callers enumerated; normalization/filter helpers reused verbatim.
- Pitfalls: HIGH — drawn from CLAUDE.md explicit constraints + observed SQLite/JSON behaviors.

**Research date:** 2026-05-24
**Valid until:** ~2026-06-23 (stable — internal schema, no fast-moving external deps)
