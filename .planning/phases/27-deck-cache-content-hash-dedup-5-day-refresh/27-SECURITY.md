---
phase: 27-deck-cache-content-hash-dedup-5-day-refresh
audited: 2026-05-26
asvs_level: 1
threats_total: 7
threats_closed: 7
threats_open: 0
status: SECURED
---

# Phase 27 — Security Audit (Deck-Cache Content-Hash Dedup + 5-Day Refresh)

Threat register authored at plan time. Each declared mitigation verified against implemented code.
Implementation files were not modified.

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-27-01 | Tampering | accept | CLOSED | SHA-256 is the algorithm used: `SHA256.HashData(...)` at `DeckFlow.Core/Knowledge/DeckCategoryCacheWriter.cs:116`. BCL `System.Security.Cryptography` (using at line 3). No weak/custom hash. Integrity-only collision risk accepted — logged below. |
| T-27-02 | Tampering | mitigate | CLOSED | `ComputeCanonicalHash` calls the SAME `BuildCanonicalBatch` the writer persists (`DeckCategoryCacheWriter.cs:93` vs writer consumer at `:45`). Hash covers BOTH kinds: observations loop `:96-104` and totals loop `:106-112`. Tests: `ComputeHash_DistinguishesContent` (tests:44), `ComputeHash_UncategorizedCardChangesHash` (tests:90), `ComputeHash_BoardMoveChangesHash` (tests:105, asserts NotEqual on mainboard vs sideboard), `ComputeHash_AggregatesDuplicates` (tests:75), `RunAsync_ChangedDeck_RewritesAndUpdatesHash` (tests:220). |
| T-27-05 | Tampering | mitigate | CLOSED | Length-prefixed field encoding `EncodeRecord` emits UTF-8 byte length + `:` + value per field (`DeckCategoryCacheWriter.cs:120-131`); no bare pipe/newline join — records joined with `\n` only after length-framing. Test `ComputeHash_DelimiterInjectionSafe` (tests:116-124) asserts crafted colliding-under-naive-delimiter inputs (`"A\|B"`/`"c"` vs `"A"`/`"b\|c"`) hash DIFFERENTLY. |
| T-27-06 | Tampering | mitigate | CLOSED | Changed-path clear-before/set-after ordering in `PersistDeckAsync`: `SetContentHashAsync(deckId, null, ...)` BEFORE `ReplaceDeckEntriesAsync` (`ArchidektDeckCacheSession.cs:189-190`), then `SetContentHashAsync(deckId, newHash, ...)` only AFTER success (`:191`). Test `ChangedPath_PartialFailureLeavesNullHash` (tests:250-268) injects a real SQLite ABORT trigger on observation insert, asserts the replace throws, and asserts `GetContentHashAsync` returns NULL afterward (tests:267). |
| T-27-03 | DoS | mitigate | CLOSED | Idempotent ADD COLUMN guard in `EnsureSchemaAsync`: `GetTableColumnsAsync(connection, "deck_queue", ...)` then `if (!deckQueueColumns.Contains("content_hash"))` runs bare `ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;` (`CategoryKnowledgeRepository.cs:78-84`) — no `IF NOT EXISTS` (unsupported by SQLite). `GetTableColumnsAsync` is cross-dialect: PRAGMA table_info for SQLite, information_schema for Postgres (`:1223-1262`). Test `EnsureSchema_IsIdempotentForContentHash` (tests:174). |
| T-27-04 | Information Disclosure | mitigate | CLOSED | All deck_queue hash reads/writes parameterized via `RelationalDatabaseConnection.AddParameter`: GetContentHashAsync `SELECT ... WHERE deck_id = @deckId` with `@deckId` bound (`CategoryKnowledgeRepository.cs:925-926`); SetContentHashAsync `UPDATE ... SET content_hash = @hash WHERE deck_id = @deckId` with `@deckId` and `@hash` bound (`:946-948`). NULL hash bound via `(object?)hash ?? DBNull.Value` (`:948`), never string-concatenated. Requeue predicate also parameterized (`:747-749`). |
| T-27-SC | Tampering | n/a | CLOSED | No new PackageReference added (`git diff main` on both csproj files: NO_PACKAGE_CHANGES). SHA-256 sourced from BCL `System.Security.Cryptography` (`DeckCategoryCacheWriter.cs:3`). No package-manager install introduced. |

## Accepted Risks Log

- **T-27-01 (Tampering — hash collision serves stale categories):** Accepted. SHA-256 collision probability is cryptographically negligible for content dedup; the hash is integrity-only (not a security control). A collision would at worst skip one rewrite of one deck's category cache, never cross a trust boundary or leak data. Verified the algorithm is SHA-256 (BCL), not a weak/custom hash, so the accept rests on the declared algorithm actually being in use.

## Unregistered Flags

None. SUMMARY.md contains no `## Threat Flags` section; no new attack surface was reported by the executor during implementation, and the audit was scoped to the authored register (no blind vulnerability scan per role).

## Notes

- All seven register entries resolve to CLOSED. Six mitigations confirmed by locating the actual code call/pattern in the cited file; the one `accept` entry is documented above with algorithm verification.
- Implementation files unchanged (audit is read-only; only this SECURITY.md was written).
