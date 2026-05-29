# Phase 6 — Deferred Items

Out-of-scope discoveries logged during execution; do not block the originating plan.

## DEFER-06-01 — Build version stamp renders as literal Razor text in `_AdminLayout.cshtml` ✅ RESOLVED 2026-05-02

**Resolved:** Folded into 06-03 closure commit (one-line `v@(VersionService.GetVersion())` parens fix on `_AdminLayout.cshtml:30`). Build clean (0 warnings, 0 errors). See `06-03-SUMMARY.md` §"Rule 2 (Auto-add critical functionality) — folded from DEFER-06-01" and `06-03-CHECKPOINT-FEEDBACK.md §Resolution`. Post-merge prod verification of the corrected version stamp rides the same gate as the AdminFeedback layout-swap visual check.

**Discovered during:** 06-03 execution (live curl of `/Admin/Feedback` after layout swap).

**File:** `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml:30`

**Symptom:** Top bar build stamp renders as the literal string `v@VersionService.GetVersion()` instead of the evaluated version (e.g., `v1.1.0`).

**Root cause:** Razor parser treats `v@VersionService.GetVersion()` as ambiguous — the leading `v` is not a tag/whitespace boundary, so Razor does not switch into code context for the `@VersionService` expression. Other expressions on lines 22–25 evaluate correctly because they sit at the start of an attribute or element.

**Fix (one-line, straightforward):** Change line 30 to:

```cshtml
<span class="admin-topbar__version">v@(VersionService.GetVersion())</span>
```

(or split: `v<text></text>@VersionService.GetVersion()`).

**Why deferred:** D-15 in 06-CONTEXT.md scoped 06-03 to "layout-swap only — keep route Admin/Feedback, keep Views/AdminFeedback/ folder." The file `_AdminLayout.cshtml` is a Plan 06-01 deliverable already merged. Touching it from 06-03 violates the scope boundary in `<deviation_rules>` ("Only auto-fix issues DIRECTLY caused by the current task's changes").

**Owner:** Belongs to a follow-up trivial fix plan or fold into 06-04+. Not blocking the human-verify checkpoint for 06-03 — operator can still confirm sidebar/active-state/no-theme-leakage; only the version digit on the right of the top bar is wrong.
