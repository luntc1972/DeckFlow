# Debug: PROD harvest 42883 `text <= timestamp with time zone`

**Status:** resolved (root cause = deployment gap; fix already authored on `cycle8`)
**Opened:** 2026-06-17
**Environment:** PRODUCTION (Render web `mtg-deck-studio`, Postgres)

## Symptom
Admin Run Log shows every Bulk harvest run Failed, 0 decks, 3600s, error:

```
42883: operator does not exist: text <= timestamp with time zone POSITION: 364
```

Repeats once/minute (each scheduled bulk run dies immediately).

## Root cause
`CategoryKnowledgeRepository.AddDeckIdsAsync` requeue upsert compares the TEXT
column `deck_queue.last_checked_utc` against `@requeueBeforeUtc`, which Dapper
binds as a native `timestamptz` on Postgres. Postgres has no
`text <= timestamptz` operator → 42883. POSITION 364 = the second `WHEN ...
last_checked_utc <= @requeueBeforeUtc` clause in the uncast SQL.

This is **F-51-PG-01**, already fixed on branch `cycle8` (commit `c4b625e`) by
dialect-guarding the column with `::timestamptz` on Postgres only.

**Why prod still fails:** Render deploys from `main`. `cycle8` is 10 commits
ahead of `origin/main` and **unpushed/unmerged**. `origin/main` has ZERO
`::timestamptz` casts (verified) → prod runs the pre-fix code.

```
origin/main  : deck_queue.last_checked_utc <= @requeueBeforeUtc   (uncast → 42883)
cycle8 c4b625e: {sqlite ? col : col::timestamptz} <= @requeueBeforeUtc  (fixed)
```

## Remedy (operator/deploy — no new code)
The fix exists; it must reach prod. Two paths:
- **Hotfix (recommended):** cherry-pick `c4b625e` onto local `main`, user
  `git push origin main` (ff), Render redeploys. Keeps unfinished `cycle8`
  milestone work out of prod. Squash-merge later reconciles the dup commit.
- **Full merge:** premature — milestone not closed.

AI does not push `main`; user pushes.

## Evidence
- `git show origin/main:.../CategoryKnowledgeRepository.cs` → uncast `<=`, 0 casts
- `cycle8` line 719-721 + 734/739 → cast present (F-51-PG-01 comment)
- `git merge-base --is-ancestor c4b625e origin/main` → NO
