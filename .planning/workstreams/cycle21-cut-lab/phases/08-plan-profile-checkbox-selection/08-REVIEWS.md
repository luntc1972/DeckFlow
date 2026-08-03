# Phase 8 (Plan-Profile Checkbox Selection) — Claim-vs-Code Plan Review

**Round 1 — 2026-08-03. Reviewer: Codex `gpt-5.6-sol`, effort medium, `-s read-only`, rooted at
`gsd/cycle21-cut-lab` (HEAD `21ad9c53`, rebased onto `main` `ea3dca2a` the same day).
Verdict: CHANGES REQUIRED. 2 BLOCK · 7 HIGH · 4 MEDIUM. Not folded.**

## Why this round exists

This is the **owed** Codex plan review recorded at `STATE.md:47` — deferred by the operator on
2026-08-02 with a same-family checker pass accepted in its place, and required before
`/gsd-execute-phase 8`.

The checker had graded these eight plans **clean**. This round returned two BLOCKs. Every BLOCK and
most HIGHs are census failures — a constructor count, an e2e spec count, a `ResolveDefaults` call
count, a "legacy sessions are preserved" claim — none of which a text-vs-text reviewer can detect,
because each requires grepping the repository. That asymmetry is the documented reason both
reviewers are run.

⚠ Two stale claims elsewhere in the workstream say this plan set exists **only** on
`feat/ui-audit-batch-a` and is not on `gsd/cycle21-cut-lab` or `main`: `STATE.md:45-46` and
`.planning/notes/2026-08-02-milestone-sequencing.md:29-31`. Both are wrong as of the rebase — the
plans are on this branch. `08-.../README.md:3` also still says "Not yet planned."

## BLOCK

**B-1 — `08-06-PLAN.md:199` — the constructor census is incomplete; execution will not compile.**
Adding a required `ICutLabPlanAffinityFactory` breaks **70** `CutLabPageService` constructions, four
`CutLabUiPatchBuilder` constructions and two `CutLabApiController` constructions across seven test
files. `CutLabPageService` additionally cannot take a required parameter appended after its existing
optional parameters. *Fix:* make the dependency explicitly optional with a null-object fallback, or
enumerate and update every construction site — and add all affected test files to `files_modified`.

**B-2 — `08-07-PLAN.md:143` — the legacy-session preservation claim is false; it silently destroys
user state.** Production TypeScript still reads the soon-to-be-removed `PrimaryPlan` and
`SecondaryPlan` controls at `cut-lab.ts:1197,1226`. The new checkbox change handler rebuilds state
with empty legacy values, wiping them in restored sessions. *Fix:* preserve
`persistedState.intent.primaryPlan/secondaryPlan` when the controls are absent, and add a
legacy-session TypeScript regression test.

## HIGH

**H-1 — `08-06-PLAN.md:110` — PLPR-06 can never fire in production.** Fetching card lists only for
**checked** themes makes the stranded-off-plan-package detector impossible: `ResolveAll` can
populate `OffPlanThemes` only from `themeCardNamesBySlug`, while the factory supplies lists only for
checked themes. *Fix:* fetch and pass the necessary unchecked-theme membership lists, or redesign
the detector around another complete membership source.

**H-2 — `08-07-PLAN.md:65` — the "six existing e2e specs" census is false.** Three further live
specs reference `#cut-lab-primary-plan`: `cut-lab-nav-themes.spec.ts`,
`cut-lab-theme-readability.spec.ts` and `cut-lab-whatif.spec.ts`. Removing the field leaves the full
e2e suite red. *Fix:* add and migrate all three.

**H-3 — `08-07-PLAN.md:164` — the request-binding design does not match the Razor DOM.** The intake
form ends at `CutLab.cshtml:229`, while the reserved plan-panel slot sits in the results wizard
around line 1207. Checkbox `name` attributes there will not bind to `CutLabRequest`, and the plan
forbids the fetch/post needed to apply changes immediately. *Fix:* define an explicit state-carrying
submit contract or form association, and a server round trip, before asserting that engine output
changes.

**H-4 — `08-07-PLAN.md:178` — the first-presentation marker does not exist.** It is unnamed and
absent from both `CutLabRequest` and `CutLabPlanProfile`, so the server cannot implement the
required "never presented" versus "presented and cleared" distinction. *Fix:* use the
already-designed `PlanProfile == null` versus non-null-empty-profile distinction, or add a concrete
persisted `{ get; init; }` marker threaded through C#, Razor and TypeScript.

**H-5 — `08-06-PLAN.md:155` — the request-amplification mitigation is false.** Client-controlled
session JSON can carry an unbounded, duplicated `CommanderThemes` collection, and the factory issues
one sequential fetch per entry; `CutLabStateSerializer` imposes no profile collection cap. *Fix:*
intersect themes with the fetched known-theme list, deduplicate slugs, impose a hard maximum before
any card-list request, and test **crafted session JSON** rather than only trusted form input.

**H-6 — `08-07-PLAN.md:197` — Task 1 cannot satisfy its own acceptance criteria within declared
scope.** It requires two new assertions, but neither its `<files>` nor plan-level `files_modified`
includes any Web test file. *Fix:* add the specific `CutLabPageServiceTests` / `CutLabControllerTests`
files and prescribe the new cases.

**H-7 — `08-08-PLAN.md:84` — the e2e interaction asserts an effect that cannot occur.** It assumes
checking a box immediately changes the proposal, but `08-07` explicitly adds no fetch and only
rewrites hidden JSON — no engine request happens on checkbox change. *Fix:* have the test perform
the explicit plan-apply / state-carrying post defined in `08-07` before asserting proposal or
finding changes.

## MEDIUM

**M-1 — `08-08-PLAN.md:53`** — the admin-helper claim is false: `setToolEnabled(page, label, enabled)`
selects a **UI label**, and every existing Cut Lab spec passes `"Cut Lab"`, not the key
`"tool.cut-lab.enabled"`. *Fix:* specify `setToolEnabled(page, "Cut Lab", ...)`, and capture/restore
state with `getToolEnabled`.

**M-2 — `08-04-PLAN.md:103`** — the `ResolveDefaults` census is stale: the repository has **19**
calls, not twelve — one production and eighteen test. *Fix:* correct the count and list the
additional `CutLabFloorDefaultsTests` sites. The optional trailing parameter still preserves
compilation.

**M-3 — `08-02-PLAN.md:219`** — the score-cap mutation requirements contradict each other: the
action says changing `OnPlanScoreCap` must fail the test, while acceptance says changing it from 3
to 2 should still pass. *Fix:* lock the intended cap with an independent expected value, or drop the
claim that the test guards the numeric constant.

**M-4 — `08-05-PLAN.md:312`** — the proposed exclusion test has the wrong verdict: five
theme-supporting cards with one on-plan leave **four** stranded, which equals the threshold and must
produce a finding, not suppress it. *Fix:* assert one finding with count and evidence of exactly
four, and verify the on-plan card is absent.

## Status

**Not folded. Phase 8 must not execute until at least B-1 and B-2 are resolved** — B-1 does not
compile and B-2 silently destroys restored user sessions.

B-1 and Phase 7's H-1 (`07-01-PLAN.md:53`, the missing reserved wizard slot) are the same
cross-phase conflict seen from opposite ends; fold them together. H-3, H-4 and H-7 are one cluster
about how a checkbox change reaches the server — resolve that contract once and all three fold
together.
