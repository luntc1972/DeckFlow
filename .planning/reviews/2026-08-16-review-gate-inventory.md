# Code-Review Gate Inventory — unmerged branches

**Date:** 2026-08-16
**Question:** which branches still owe an independent code review?
**Method:** every branch not merged into `main` (backups excluded), measured with
`git diff --stat main...<branch> -- ':!.foreman' ':!.planning'` so planning artifacts and foreman
scratch dumps do not inflate the code volume; cross-referenced against every `*REVIEW*` artifact in
`.planning/` outside `archive/`.

## Headline

**The four `*-REVIEWS.md` files in the cycle21 workstream are PLAN reviews, not code reviews.**
Each opens "Claim-vs-Code **Plan** Review" and was run `-s read-only` against the plan set. They do
not discharge a code-review gate on the code that was subsequently executed. This is the single
biggest gap below.

## Owed, ranked by exposure

⚠ **Re-measured 2026-08-17 at `a7948b10`: three rows below are superseded** — `feat/category-weights`,
`feat/flags-bracket-deck-history-default-on` and `fix/sitemap-help-global-gate` are all merged into
`main` and their branches deleted, and `feat/category-weights` now has a code gate at
`.planning/reviews/2026-08-16-category-weights-code-gate.md`; the other five rows still hold. Table
left as written — this is a census of mutable state, so read it with a measured-at SHA, not a date.

| Branch | Code changed | Last activity | Review artifact | Verdict |
|---|---|---|---|---|
| `gsd/cycle21-cut-lab` | 39 files, +2635/-694 | 2026-08-04 | 4 × PLAN review only | **OWED — largest real gap.** Phases 4/5/7/8 plan-reviewed; the executed code never was. Active workstream, another agent's branch, phase 07-06 parked in `stash@{0}`. |
| `feat/category-weights` | 19 files, +844/-33 | 2026-07-16 | **none anywhere** | **OWED.** No record in `.planning/` at all — no plan, no quick-task dir, no review. A month old, one commit, never merged. Most orphaned branch in the repo. |
| `gsd/cycle20-personal-tools` | 58 files, +8550/-3 | 2026-07-27 | none | **OWED.** Phase 112 port closed but unreviewed and unmerged. Also carries ~19k lines of `.foreman/scratch/*.txt` transcript dumps that were committed and probably should not have been. |
| `feature/deck-tendencies` | 307 files, +37339/-897 | 2026-07-24 | none | **OWED, but check scope first** — the count likely includes vendored data. Merge-base is 2026-07-06 and it is 1200 commits behind `main`; review is not the first problem, staleness is. |
| `spike/role-classification-accuracy` | 26 files, +739/-147 | 2026-07-26 | none | **Probably not owed** — a spike, and memory records it as superseded by Phases 2/3. Confirm it is being retired rather than merged. |
| `feat/flags-bracket-deck-history-default-on` | 8 files, +118/-14 | 2026-08-05 | none | **Low** — seed-default flag flip. Small and mechanical, but it changes production default state, so worth a look before the prod flip it is waiting on. |
| `fix/e2e-pending-hidden-seed` | 1 file, +4/-2 | 2026-07-06 | none | **Negligible** — 4-line e2e seed-skip fix. |
| `plan/cycle-17-creator-style` | (363 ahead, 1200 behind) | 2026-07-19 | n/a | **Not owed** — memory records it as SUPERSEDED by Cycle 20's port-forward; kept at origin as a historical record, explicitly not to be resumed. |

## Not owed

- `chore/statusline-context-position` — 1 file, +3 lines, `.planning/config.json` only. No code.
- `fix/sitemap-help-global-gate` — created today; the fix was Codex-written and Claude-reviewed
  (diff, EOL, scope fence, full suite 2315/0/16) per the Roles table, which puts Claude in the
  code-review seat. Gate satisfied.

## Caveats

- Absence of a `*REVIEW*` file is evidence of an unrecorded review, not proof no review happened —
  a review held only in a chat transcript leaves no trace, which is exactly the failure the
  persist-at-production rule exists to prevent. For the branches above, treat "no artifact" as
  "no discharge", because an undocumented review cannot be relied on.
- `feature/deck-tendencies` and `plan/cycle-17-creator-style` share merge-base `5709f37c`
  (2026-07-06) and are both 1200 behind `main`. Any review of them reviews a codebase that no
  longer exists.
