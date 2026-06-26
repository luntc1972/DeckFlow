# DATA-02 Decision — Reconcile of the 10 Published Orphans

**Phase:** 65 — Prod Content Artifact Reconcile
**Requirement:** DATA-02
**Decided:** 2026-06-22
**Status:** Code fix committed on `cycle11`; operator deploy + 1-row decision outstanding (see Operator checkpoint)

---

## Inputs

- Serving source = `/app/content-kb` (committed repo tree), confirmed live. See `65-DATA01-DECISION.md`.
- Published-orphan count = **10**. See `65-PROBE-RESULTS.md`:
  - 9 × `salubrious-snail/*` — **slug mismatch**; bodies exist in the repo under `salubrioussnail/`.
  - 1 × `the-command-zone/e3qGnuupp8U.md` — uncommitted (P58 DirectPush dogfood); not in the seed.

## Mechanism discovered during execution (changes the reconcile path)

`ContentKbSeedLoader.LoadIfPresentAsync` runs at **startup** (`Program.cs:256`) and upserts every
committed seed entry via `UpsertRowPreservingVisibilityAsync`. That upsert's
`ON CONFLICT (natural_key) DO UPDATE SET artifact_path = EXCLUDED.artifact_path` **updates the
artifact path while preserving `is_visible`/`is_hidden`** (`ContentSiteIndexStore.cs:876-888`).

→ Fixing the **committed seed** slug and redeploying auto-corrects the prod rows' `artifact_path` on
the next boot — **no manual prod-DB SQL required**, visibility preserved. This is a repo change
(Claude-coded), not the operator prod-DB UPDATE the plan originally assumed.

## Chosen reconcile

### Group 1 — 9 salubrious-snail orphans → seed slug fix (DONE in repo)

- **Action taken:** rewrote all 19 `artifactPath` slugs `content-kb/salubrious-snail/` →
  `content-kb/salubrioussnail/` in `content-kb/seed/index-seed.json` (the canonical slug per the
  Cycle-8 consolidation; all 19 ids verified to have a committed body under `salubrioussnail/`).
  JSON re-validated (86 entries), LF preserved.
- **Effect on deploy:** the seed loader upserts the corrected `artifact_path` for those rows
  (matched by YouTube id), preserving their `is_visible=TRUE` → the 9 detail pages render their
  (already-committed) bodies. No information loss, no prod write.
- **Why not a prod UPDATE:** the seed is the deploy-time source of truth and the originating cause
  (prod was seeded from the hyphenated seed). Fixing the seed fixes the root cause durably; a manual
  prod UPDATE would be re-overwritten by the next seed load anyway.

### Group 2 — 1 the-command-zone `e3qGnuupp8U` → operator decision

This row is visible in prod, has no committed body, and is **not in the seed** (it was pushed via
Studio DirectPush during the P58 dogfood). The seed fix does not touch it. Two options:

- **(a) Commit the artifact** — if the operator still has the distill output locally
  (`content-kb/the-command-zone/e3qGnuupp8U.md`), add it to the repo tree; on deploy the live page
  renders. Preferred if the content is wanted.
- **(b) Unpublish the row** — set `is_visible=FALSE` via the admin console `/Admin/ContentKb` (or a
  one-row prod UPDATE). Lowest effort; removes the body-less page. Recommended default — it's a
  single dogfood video, not core content.

## Operator checkpoint (outstanding — blocks phase close)

1. **Deploy the seed fix to prod.** Prod deploys from `main`; the fix is on `cycle11`. Either land
   it on `main` now (cherry-pick `content-kb/seed/index-seed.json`) for an immediate live fix, or
   accept it ships when `cycle11 → main`. Until deployed, the 9 pages stay body-less in prod.
2. **Decide Group 2** (`e3qGnuupp8U`): commit the artifact (a) or unpublish (b, recommended).
3. **(Optional) Fix the local Studio `content-kb.db`** so a future DirectPush doesn't reintroduce the
   `salubrious-snail` slug.

## SC3 — post-reconcile verification

After the deploy + Group-2 decision, re-run the read-only check to confirm zero unexplained
published orphans:

- Re-run prod probe Query C (visible `artifact_path`s) and cross-check against the committed repo
  `content-kb/` tree (the `/app` serving base) — expect 0 missing.
- Or run the new `content-kb-check` CLI (Plan 03) against a prod-pulled local DB with
  `--artifact-root` = the repo root (content base), expecting `Published (missing): 0`, exit 0.

Expected post-fix state: 24 of 25 visible rows render bodies immediately on deploy; the 25th
(`e3qGnuupp8U`) is resolved per the Group-2 choice (committed → renders, or unpublished → no longer
counted).
