# 44-01 Summary

## Task 1 — RED

- Updated `EnsureSchemaAsync_CreatesDeckQueueIndexes` to assert:
  - `ix_deck_queue_processed` exists
  - `ix_deck_queue_processed_inserted_deck` exists
  - `ix_deck_queue_processed_commander` does not exist
  - `ix_deck_queue_processed_commander_lower` does not exist
  - `ix_deck_queue_commander_lower_processed` exists
- Updated `GetDeckQueueIndexNamesAsync()` so the helper can actually observe `ix_deck_queue_commander_lower_processed`.
- Verification:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` succeeded.
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~EnsureSchemaAsync_CreatesDeckQueueIndexes"` failed before the repository change, as expected:

```text
[xUnit.net 00:00:00.88]     DeckFlow.Core.Tests.CategoryKnowledgeRepositoryTests.EnsureSchemaAsync_CreatesDeckQueueIndexes [FAIL]
  Failed DeckFlow.Core.Tests.CategoryKnowledgeRepositoryTests.EnsureSchemaAsync_CreatesDeckQueueIndexes [367 ms]
  Error Message:
   Assert.DoesNotContain() Failure: Item found in collection
                                        ↓ (pos 1)
Collection: ["ix_deck_queue_processed", "ix_deck_queue_processed_commander", "ix_deck_queue_processed_commander_lower", "ix_deck_queue_processed_inserted_deck"]
Found:      "ix_deck_queue_processed_commander"
```

## Task 2 — GREEN

- Replaced the two old commander index `CREATE` statements in `CategoryKnowledgeRepository.EnsureSchemaAsync` with:
  - `-- Why:` SQL comment inside the raw string literal
  - `CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed ON deck_queue(LOWER(commander_name)) WHERE processed = 1;`
  - `DROP INDEX IF EXISTS ix_deck_queue_processed_commander;`
  - `DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower;`
- The new `CREATE` appears before both `DROP` statements inside the same batched SQL literal.
- No dialect branch added. No extra try/catch added. The existing C# `// Why:` comment after the literal was left in place.
- Verification:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"` passed.

## Task 3 — EXPLAIN Evidence

- Method:
  - Built a throwaway SQLite DB.
  - Ran `EnsureSchemaAsync`.
  - Seeded processed and unprocessed `deck_queue` rows.
  - Ran `ANALYZE`.
  - Executed `EXPLAIN QUERY PLAN` for both queries.
- Outcome:
  - No full table scan of `deck_queue` appeared.
  - SQLite used `ix_deck_queue_processed`, not `ix_deck_queue_commander_lower_processed`.
  - This means the plan’s ideal “new index referenced” evidence was **not** observed in this throwaway DB. The consolidation stands, but SC3 is not overstated here.

Verbatim EXPLAIN output:

```text
COUNT QUERY
3|0|0|USE TEMP B-TREE FOR count(DISTINCT)
6|0|90|SEARCH deck_queue USING INDEX ix_deck_queue_processed (processed=?)
PAGED QUERY
12|0|90|SEARCH deck_queue USING INDEX ix_deck_queue_processed (processed=?)
19|0|0|USE TEMP B-TREE FOR GROUP BY
73|0|0|USE TEMP B-TREE FOR ORDER BY
```
