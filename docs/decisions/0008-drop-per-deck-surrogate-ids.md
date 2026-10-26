# 0008 — Drop surrogate ids from per-deck category tables

**Date:** 2026-10-20

## Context

Production Postgres has 15 GB of disk that cannot be grown and currently uses 13,855,614,540 bytes. `card_category_observations_pkey` consumes 793,960,448 bytes (757 MiB) and `card_deck_totals_pkey` consumes 742,113,280 bytes (708 MiB). The corresponding tables have approximately 22 million and 8 million rows. The application has no reader for either surrogate `id`; the census found only DDL and schema-parity test references. The existing grain indexes already provide the required uniqueness, and their 0–1 scans are sufficient for the write paths.

## Decision

Apply D6: fresh Postgres tables use their grain unique index as the primary key, and omit the surrogate `id`. Fresh SQLite follows OQ3: it omits `id` and retains the grain as a unique index with no declared primary key. Existing SQLite files remain unchanged.

At schema ensure, Postgres automatically compacts each legacy per-deck table. In a transaction, it sets a five-second local lock timeout, drops the existing primary-key constraint, drops `id`, then promotes the existing grain index with `PRIMARY KEY USING INDEX`. The drop must precede the add because Postgres permits only one primary key. A lock timeout or other database error rolls back that table, logs a warning, and retries at a later process start. Task 1 selected the `boot-automatic` conversion choice.

## Consequences

The production conversion is one-way: restoring an identity column would require a full table rewrite. Dropping the indexes frees index capacity, but heap bytes are reclaimed only through reuse or a deferred `VACUUM FULL`. The rollout pauses harvest while conversion runs.
