# Phase 58 Dogfood — Verification Queries & Baseline

**Authored:** 2026-06-18 (58-01 scaffolding). All SQL here is **SELECT-only** and is run only
against **local** SQLite DBs. Never run against a prod-write connection (project rule: AI never
writes prod).

---

## Local DB layout discovered this session (important)

The two apps do **not** share one site-index DB locally:

| App | Site-index DB (real path this session) | Notes |
|-----|----------------------------------------|-------|
| **Studio** (`:5271`) | `artifacts/studio/content-kb.db` (created on first harvest; `content_site_index` table lives inside the harvest DB) | also holds `llm_spend_ledger`, harvest tables |
| **Web** (`:5173`) | `artifacts/content-site-index.db` (94 rows, has `pushed_to_prod_utc`, **0 stamped**) | local dev copy; separate file |
| **PROD** | Render Postgres `deckflow` | where real `pushed_to_prod_utc` values live |

Consequence: locally **every Web row derives `Never published`** (0 rows have `pushed_to_prod_utc`).
The real `Published` corpus + the no-regression check are **PROD-side**. So SC2 two-surface and SC4
regression are exercised by publishing to prod (DirectPush, or git-Publish + Render deploy) and
checking the **deployed** `/Admin/ContentKb` — not the local `:5173`. Studio's own badge is local.

---

## (1) Baseline entry for the quality comparison (SC1)

**Primary baseline:** `content-kb/the-command-zone/6oS1E5BGi0U.md`
- "Why Your Deck Feels Clunky and How to Fix It" — The Command Zone 730
- `generated_utc: 2026-06-15` → distilled under the **pre-`00c3bc7` (old) prompt** = valid pre-Cycle-9 baseline.
- Summary is one dense wall-of-text paragraph → good contrast against the Phase-57 paste-ready rework.
- **Fair comparison:** harvest a NEW *The Command Zone* video (same creator/format) for the dogfood.

**Alternate baseline:** `content-kb/salubrioussnail/0nYzkbsMFgE.md` (use if harvesting a Salubrious Snail video instead).

---

## (2) Publish-state inputs for the NEW entry (the 3 `PublishStateDeriver` inputs)

`PublishStateDeriver.Derive(pushedToProdUtc, isVisible, localIndexedUtc)`. There is **no `youtube_id`
column** — identity is `natural_key_value` (the YouTube video id) and/or `video_url`.

Run against **Studio's** DB after harvest/approve (replace `NEWID`):

```sql
-- DB: artifacts/studio/content-kb.db
SELECT natural_key_value, video_url, approval_status,
       pushed_to_prod_utc, is_visible, indexed_utc
FROM   content_site_index
WHERE  natural_key_value = 'NEWID';
```

To read the same row from the **Web** local DB: same query against `artifacts/content-site-index.db`.

---

## (3) No-regression snapshot (SC4)

Capture BEFORE publish, re-run AFTER, diff. Locally (Web db) all rows are `Never published`
(0 pushed), so the meaningful regression check is **PROD** (read-only) — use the deployed
`/Admin/ContentKb` grid or a read-only prod SELECT via the sanctioned `prod-readonly-query` skill.

Local snapshot shape (illustrative; capture key + the 3 deriver inputs for every row):

```sql
-- DB: artifacts/content-site-index.db  (or prod, read-only, via prod-readonly-query)
SELECT natural_key_value, pushed_to_prod_utc, is_visible, indexed_utc
FROM   content_site_index
ORDER  BY natural_key_value;
```

Success condition: no row that was `Published` before the run flips to `Local-newer` or
`Never published` after. (Local baseline this session: 94 rows, 0 `Published`, 7 `is_visible`.)

---

## (4) Two-surface `Published` confirmation checklist (SC2)

The four locked display strings (verbatim, from `PublishState.cs`):
`Never published` · `Pushed-hidden` · `Published` · `Local-newer`.

- [ ] **Studio** `/review` (or `/publish`): new entry's badge reads exactly `Published`.
- [ ] **Web** `/Admin/ContentKb`: new entry's publish-state column reads exactly `Published`.
      - Requires the `content.kb.enabled` flag ON.
      - Because local `:5173` ≠ prod, this is checked on the **deployed** site after the row is
        pushed to prod (DirectPush stamps `pushed_to_prod_utc`, or git-Publish + Render deploy).
- [ ] Capture proof: screenshot paths or pasted status cells for both surfaces.
