TASK: Follow-up one-line fix in the same worktree/branch as your last ticket (feat/cutlab-fixes).
Your previous fix (data-cutlab-group-kind="role"/"type" disambiguation) is correct and should
remain as-is, uncommitted, in the working tree. Do not revert or redo it.

CONTEXT: DeckFlow.Web/Models/CutLabViewModel.cs's RoleGroupDisplayOrder (line 58-62) is
`CutLabFloorRules.RoleKeys` (8 entries) + a deliberate "other" fallback bucket appended for
display — this "other" bucket was added by an earlier commit already on this branch and is
correct, intentional behavior (BuildRoleGroups always renders all 9 entries in
RoleGroupDisplayOrder unconditionally, even when a bucket is empty). So the "How your pool
competes" section always renders 9 `<details class="cutlab-role-group" data-cutlab-group-kind="role">`
elements, not 8. The e2e test DeckFlow.Web/e2e/cut-lab-structure.spec.ts was written before the
"other" bucket existed and still hardcodes 8.

MUST DO: In DeckFlow.Web/e2e/cut-lab-structure.spec.ts, in the test
"renders the three structure sections with 8 collapsed role groups and 8 floor inputs":
- Change `await expect(roleGroups).toHaveCount(8);` to `await expect(roleGroups).toHaveCount(9);`
  (the `roleGroups` locator you already scoped to `[data-cutlab-group-kind="role"]` last ticket).
- Leave `await expect(page.locator('input[data-cut-lab-floor]')).toHaveCount(8);` unchanged —
  floor inputs are driven by CutLabFloorRules.RoleKeys (8 entries, no "other"), that count is
  correct as-is.
- If the test's title string literally says "8 collapsed role groups", update it to "9" too, so
  the title matches the assertion (e.g. "renders the three structure sections with 9 collapsed
  role groups and 8 floor inputs"). Check whether any other spec file or doc references this
  test by its exact title string (grep for the old title) before renaming it; if referenced
  elsewhere, tell me instead of silently breaking that reference.

MUST NOT: touch any other file or line.

VERIFICATION REQUIRED:
- Re-run `npx --no-install playwright test cut-lab-structure.spec.ts` from DeckFlow.Web (start
  the server per scripts/run-web-test.sh with DECKFLOW_DISABLE_AUTO_BROWSER=true first, per this
  project's UI-testing rules — never open a real browser on the Windows host). Report the exact
  pass/fail result. This must go GREEN before you report DONE.
- Re-run `npx --no-install vitest run` from DeckFlow.Web and confirm still green.
- Run `dotnet build` from repo root, confirm 0 errors/warnings.

OUTPUT FORMAT: start final message with DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED,
then the diff and verification results. Do not commit.

WRITE SET: DeckFlow.Web/e2e/cut-lab-structure.spec.ts
