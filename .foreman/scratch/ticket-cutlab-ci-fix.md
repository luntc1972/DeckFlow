TASK: Fix a CI e2e test failure in the DeckFlow repo caused by an ambiguous CSS class selector.

CONTEXT:
Commit 153d76a4 "feat(cut-lab): group the pool by type and search by subtype" added a new
"By type" section in DeckFlow.Web/Views/Deck/CutLab.cshtml whose <details> elements reuse
class="cutlab-role-group" — the same class used by the pre-existing "How your pool competes"
role-groups section. The e2e test DeckFlow.Web/e2e/cut-lab-structure.spec.ts line 150 asserts
`expect(page.locator('details.cutlab-role-group')).toHaveCount(8)` (written before the
type-groups section existed, expecting only role groups). Now the selector matches both
sections (8 role groups + 7 type groups = 15), failing CI's Playwright E2E smoke job on both
chromium-desktop and chromium-mobile.

Working directory: this repo checkout (branch feat/cutlab-fixes). NOTE: there are already
OTHER uncommitted changes in this worktree (CutLab.cshtml has an unrelated sticky-bar diff
around line ~353-360, plus modified cut-lab.ts, site-common.css, cut-lab-lock-interactions.test.ts,
and new untracked files .foreman/ledger-*.md and CutLabViewRenderTests.cs). These are pre-existing
in-progress work from a different task — DO NOT touch, revert, or commit them. Your change must
be additive only to the specific lines below, and your commit must stage ONLY the two files this
ticket names.

MUST DO:
1. In DeckFlow.Web/Views/Deck/CutLab.cshtml:
   - Find the RoleGroups `<details class="cutlab-role-group">` in the "How your pool competes"
     section (inside `@foreach (var group in Model.RoleGroups)`, roughly line 528-575). Add the
     attribute `data-cutlab-group-kind="role"` to that `<details>` tag.
   - Find the TypeGroups `<details class="cutlab-role-group">` in the "By type" section (inside
     `@foreach (var group in Model.TypeGroups)`, roughly line 585-612). Add the attribute
     `data-cutlab-group-kind="type"` to that `<details>` tag.
   - Do NOT change the `cutlab-role-group` class itself, and do NOT touch site-common.css —
     shared styling between the two sections is intentional.
2. In DeckFlow.Web/e2e/cut-lab-structure.spec.ts, around line 150-151 (inside the test
   "renders the three structure sections with 8 collapsed role groups and 8 floor inputs"):
   - Change `const roleGroups = page.locator('details.cutlab-role-group');` to scope to only
     role groups: `page.locator('details.cutlab-role-group[data-cutlab-group-kind="role"]')`.
   - Change the very next assertion `page.locator('details.cutlab-role-group[open]')` the same
     way, so it also scopes to `[data-cutlab-group-kind="role"][open]`.
   - Do NOT change any other e2e spec file (cut-lab-pill-interactions.spec.ts,
     cut-lab-nav-themes.spec.ts, cut-lab-smoke.spec.ts, cut-lab-theme-readability.spec.ts) —
     they filter by unique visible text ("Lands"/"Interaction") and are not failing.

MUST NOT:
- Touch any file other than DeckFlow.Web/Views/Deck/CutLab.cshtml and
  DeckFlow.Web/e2e/cut-lab-structure.spec.ts.
- Stage or commit any of the pre-existing uncommitted changes already in this worktree.
- Change line endings on touched lines — this repo is LF-enforced by .gitattributes; preserve
  every other line byte-for-byte.
- Rename, remove, or alter the `cutlab-role-group` class or any CSS.

VERIFICATION REQUIRED:
- Run `dotnet build` from the repo root; must be 0 errors, 0 new warnings.
- If feasible in this environment, start the web app per scripts/run-web-test.sh
  (sets DECKFLOW_DISABLE_AUTO_BROWSER=true) and run
  `npx --no-install playwright test cut-lab-structure.spec.ts` from DeckFlow.Web. If a full
  Playwright run isn't practical here, at minimum trace the DOM construction in
  DeckFlow.Web/Models/CutLabViewModel.cs (RoleGroups/TypeGroups) to confirm exactly 8
  RoleGroups entries will exist for the test's fixture pool, and run xUnit
  (`dotnet test DeckFlow.Web.Tests`) + vitest (`npx --no-install vitest run` in DeckFlow.Web)
  to confirm nothing else broke.
- Report exactly what you ran and its result — do not claim a test passed without running it.

OUTPUT FORMAT: start your final message with one of DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT /
BLOCKED, then: the exact diff of both files, and the verification commands you ran with their
results. Do NOT commit — leave the change staged/unstaged for review; the caller will commit.

WRITE SET: DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/e2e/cut-lab-structure.spec.ts
