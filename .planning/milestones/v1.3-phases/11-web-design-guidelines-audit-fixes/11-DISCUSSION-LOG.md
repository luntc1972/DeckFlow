# Phase 11: Web Design Guidelines Audit Fixes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-13
**Phase:** 11-Web Design Guidelines Audit Fixes
**Areas discussed:** Sweep packaging strategy, Sweep execution order, Verification strategy, Risk gating for CSP inline-handler removal

---

## Sweep packaging strategy

| Option | Description | Selected |
|--------|-------------|----------|
| 10 separate PRs | One PR per sweep. Maximum review granularity + easy revert. Heavy review burden (10 PR overhead). FINDINGS.md sequenced this way already. | ✓ |
| 3 grouped PRs | PR-1: cross-cutting foundation (Sweeps 1, 2); PR-2: view-layer sweeps (Sweeps 3, 6, 7, 8); PR-3: behavioral fixes (Sweeps 4, 5, 9, 10) | |
| 1 big PR | Whole phase ships as one PR — minimal review overhead, single revert point. Larger diff = harder to bisect regressions. | |
| Plan-by-plan commits, single PR at phase end | Atomic commits per plan/sweep on v1.3 branch, single PR to main when phase complete. Granular commits + low PR overhead. Matches v1.2 pattern. | |

**User's choice:** 10 separate PRs
**Notes:** Each PR maps cleanly to one WDG-* requirement; review burden accepted for traceability and granular revert capability.

---

## Sweep execution order

| Option | Description | Selected |
|--------|-------------|----------|
| FINDINGS.md order (leverage-first) | Sweep 1 (site-common.css cross-cutting) → Sweep 2 (admin focus-visible) → Sweep 3 (Razor selected bug) → ... → Sweep 10 (misc P1). Foundation first, then dependent sweeps. Default. | ✓ |
| P1 a11y bugs first regardless of dependency | Ship admin focus-visible, df-typeahead keyboard nav, ARIA tablist, info-tooltip, CSP handlers BEFORE foundation. Get visible a11y wins to prod fast. | |
| Risk-ordered (safest first) | Mechanical sweeps first (Razor bool bug, autocomplete attrs, table semantics) — low risk. Behavioral sweeps last (df-typeahead keyboard nav, CSP handlers) — higher regression risk. | |
| Let planner decide based on plan dependency graph | Pass to /gsd-plan-phase — planner derives wave-based order from inter-plan dependencies. | |

**User's choice:** FINDINGS.md order (leverage-first)
**Notes:** Foundation-first ordering ensures Sweep 1's site-common.css additions are in place before view-layer sweeps depend on them (e.g., `.tabular` utility class used by Sweep 6 table fixes).

---

## Verification strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Per-sweep manual UAT + dotnet build clean | After each sweep: dotnet build Release (clean) + dev server, Tab-navigate, screen-reader spot-check. Slowest but catches regressions early. | |
| Batch UAT at phase end | All 10 sweeps land first (build clean per sweep), then one big UAT pass at phase end covering all surface area. Faster turnaround, larger blast radius if a regression slips. | ✓ |
| Mechanical sweeps auto-verify, behavioral sweeps need UAT | Sweeps 3 (Razor bool), 6 (table semantics), 7 (autocomplete attrs), 1 (site-common.css adds) — dotnet build clean is sufficient. Sweeps 2, 4, 5, 8, 9, 10 — UAT in browser. | |
| Push-and-watch CI + light smoke | Push v1.3 after each sweep, watch Render auto-deploy, smoke-test on www.deckflow.gg. Risky if CI catches less than local. | |

**User's choice:** Batch UAT at phase end
**Notes:** Per-sweep gate is `dotnet build Release` clean only (no warnings). User runs dev server for final batch UAT per CLAUDE.md feedback memory `[[feedback_user_starts_server]]`. v1.3 branch does NOT auto-deploy until merged to main at milestone end.

---

## Risk gating for CSP inline-handler removal

| Option | Description | Selected |
|--------|-------------|----------|
| Move handler to JS, keep native confirm() | Move onsubmit="return confirm(...)" into admin-feedback-detail.js as event listener calling window.confirm(). CSP-safe + zero UX change. Lowest effort, retains native modal. | |
| Build proper JS confirm modal | Native confirm() works but is ugly, unstyled, not focus-trapped. Build a small admin modal. More effort but better UX + future-reusable. | |
| Defer CSP work for AdminFeedback Detail Delete to v1.4 | Land WDG-04 CSP cleanup for everything except Delete. Delete keeps inline onsubmit until v1.4 has a real modal pattern. | ✓ |
| Drop confirmation entirely — admin tool, single operator | User is sole admin per CLAUDE.md memory. Delete fires immediately without confirm. Saves modal complexity. Risk: misclick deletes. | |

**User's choice:** Defer CSP work for AdminFeedback Detail Delete to v1.4
**Notes:** WDG-04 in Phase 11 covers the other inline-handler removals (`Error.cshtml` inline `style`, `AdminFeedback/Index.cshtml` `onchange="this.form.submit()"`). The Delete handler stays in place with an inline code comment documenting the deferral and linking back to `260513-wdg-FINDINGS.md`. v1.4 will revisit when there's a real modal pattern.

---

## Claude's Discretion

- Specific utility class name for tabular-nums CSS (`.tabular` vs `.tabular-nums`) — pick whichever matches existing `site-common.css` naming convention.
- Specific `prefers-reduced-motion` timing override values (default to W3C convention `0.01ms`).
- Exact ARIA label strings for `df-typeahead` combobox role (researcher/planner picks context-appropriate text).
- Final wording of the inline code comment in `Detail.cshtml:39` documenting the WDG-04 Delete deferral.

## Deferred Ideas

- AdminFeedback Detail Delete CSP cleanup → v1.4 with proper modal pattern.
- P3 polish items from FINDINGS.md (straight quotes → curly, `&nbsp;`, hardcoded date formats, "N/A" → "—", semver `translate="no"`, `(pointer: coarse)` queries, `env(safe-area-inset-*)` for iOS notch, mobile back-to-top retention) → v1.4 polish phase or per-quick-task.
- 20 unreviewed guild theme stylesheets → assume Sweep 1's site-common.css foundation covers them; v1.4 if not.
- Full code review of 5 large TS modules (deck-sync, df-select, site, card-lookup, category-suggestions) — v1.4 audit.
