---
phase: 11
plan: 08
subsystem: web-views-accessibility
tags: [a11y, razor, css, wdg, sweep-8, info-tooltip, details-summary]
dependency_graph:
  requires:
    - 11-03 (Razor selected= ternary sweep — must not regress SuggestCategories selected= edits)
  provides:
    - WDG-05 satisfied: info-tooltip indicators on SuggestCategories + CommanderCategories are keyboard- and screen-reader-accessible
  affects:
    - DeckFlow.Web/Views/Deck/SuggestCategories.cshtml
    - DeckFlow.Web/Views/Commander/CommanderCategories.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
tech_stack:
  added: []
  patterns:
    - "HTML5 <details><summary> disclosure widget as a no-JS info tooltip (CLAUDE.md D-10)"
    - "Cross-cutting layout/a11y CSS lives in site-common.css per D-07"
key_files:
  created: []
  modified:
    - DeckFlow.Web/Views/Deck/SuggestCategories.cshtml
    - DeckFlow.Web/Views/Commander/CommanderCategories.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
decisions:
  - "D-10 applied verbatim: details/summary chosen over <button aria-describedby>; no JS dependency."
  - "D-07 applied: the .info-tooltip details/summary styles live in site-common.css (one place) rather than being duplicated in 22 theme forks."
  - "Legacy circle styling left untouched in site.css (its .info-tooltip rule still targets a <span>, but no <span class='info-tooltip'> remains in the repo for it to match — site-common.css now owns the active rules for the new element)."
metrics:
  duration: "~10 min"
  completed_date: "2026-05-13"
  tasks_completed: 1
  commits: 1
requirements_satisfied: [WDG-05]
---

# Phase 11 Plan 08: WDG-05 Info-Tooltip A11y Sweep Summary

**One-liner:** Replaced the two mouse-only `<span class="info-tooltip" title="…">i</span>` tooltips on SuggestCategories and CommanderCategories with the HTML5 `<details><summary>i</summary><p>…</p></details>` disclosure widget (D-10), and migrated the round-"i" indicator styling to `site-common.css` so every guild theme inherits the keyboard- and SR-accessible affordance without a per-fork edit.

## What Changed

### `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml`

Line 161 changed from:

```html
<span class="info-tooltip" title="These categories are ordered by how many cached decks contained the card (descending).">i</span>
```

to:

```html
<details class="info-tooltip">
    <summary>i</summary>
    <p>These categories are ordered by how many cached decks contained the card (descending).</p>
</details>
```

The title-attribute text moved verbatim into the `<p>` content. Surrounding markup, sibling elements, and the panel layout untouched. The 11-03 `selected="@(...)"` ternary edits at lines 40-43 and 88-89 remain intact (verified via `grep -q 'selected="@('`).

### `DeckFlow.Web/Views/Commander/CommanderCategories.cshtml`

Line 67 changed analogously, with the title text "Categories sorted by how many cached commander decks included this card (descending)." moved into the new `<p>`.

### `DeckFlow.Web/wwwroot/css/site-common.css`

Appended (after `.ai-selector__hint`) a banner-commented block:

```
/* === WDG audit fixes (WDG-05, Phase 11 Sweep 8) — info-tooltip details/summary === */
details.info-tooltip { … }
details.info-tooltip > summary { … }
details.info-tooltip > summary::-webkit-details-marker { display: none; }
details.info-tooltip > summary::marker { content: ""; }
details.info-tooltip > summary:focus-visible { outline: 2px solid var(--accent-strong, var(--accent)); outline-offset: 2px; }
details.info-tooltip > p { … }
```

What this buys, in concrete terms:

- The `<details>` itself becomes `display: inline-block`, so the indicator sits next to surrounding headings exactly where the old `<span>` did.
- The round "i" circle (1.2rem disc, accent background, on-accent text, weight 600) is rendered by the `<summary>` child — i.e., the styling that used to apply to the whole `<span>` now applies to the always-visible interactive child of `<details>`.
- The default browser disclosure marker is suppressed in both legacy WebKit (`::-webkit-details-marker`) and the modern spec (`::marker { content: ""; }`).
- `focus-visible` outline added on the `<summary>` so keyboard navigation has a clear, accent-colored focus ring.
- The disclosed `<p>` gets soft panel chrome (background falls back through `--panel-soft-bg → --surface → transparent`, capped at 32rem width so long tooltips don't sprawl).

## Verification

| Check | Result |
| ----- | ------ |
| `dotnet build DeckFlow.sln --configuration Release` | Build succeeded — 0 Warning(s), 0 Error(s) |
| `grep -q '<details' SuggestCategories.cshtml` | PASS |
| `grep -q '<summary' SuggestCategories.cshtml` | PASS |
| `grep -q '<details' CommanderCategories.cshtml` | PASS |
| `grep -q '<summary' CommanderCategories.cshtml` | PASS |
| `! grep -Eq '<span class="info-tooltip" title=' SuggestCategories.cshtml` | PASS (old pattern absent) |
| `! grep -Eq '<span class="info-tooltip" title=' CommanderCategories.cshtml` | PASS (old pattern absent) |
| `grep -q 'selected="@(' SuggestCategories.cshtml` | PASS (11-03 edits preserved) |
| `grep -q '<caption' CommanderCategories.cshtml` | Not yet applicable (see Deviations) |

## Deviations from Plan

**1. [Observation, not regression] 11-06 `<caption>` acceptance check is anticipatory, not yet applicable**

The plan's acceptance criteria include `grep -q '<caption' CommanderCategories.cshtml` — meant to confirm Sweep 11-06's caption edit (lines 74-79 of CommanderCategories.cshtml) was not regressed. As of this worktree's base commit (`72bb07d docs(phase-11): update tracking after wave 2`), 11-06 has not landed: there is no `<caption>` element anywhere in `CommanderCategories.cshtml`, and no `feat(11-06)` or `fix(11-06)` commit exists in `git log`. This is consistent with the wave layout (this plan is wave 4 / depends_on: [11-03] only); 11-06 will presumably land in a different wave or be merged after this one. **No regression was introduced by this plan** — the file simply never contained `<caption>` at our base. Rule 4 was not triggered because no architectural change is implied; this is just a plan-authoring artifact (the acceptance assertion was written assuming all earlier-numbered sweeps had already landed).

No other deviations. Rules 1-3 were not triggered. No auto-fixed bugs, no missing critical functionality discovered, no blocking issues. No auth gates.

## Files

**Modified:**

- `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` — line 161 span replaced with details/summary block (4 lines added vs. the single-line span).
- `DeckFlow.Web/Views/Commander/CommanderCategories.cshtml` — line 67 span replaced with details/summary block.
- `DeckFlow.Web/wwwroot/css/site-common.css` — 61-line block appended at end (banner-commented per the plan's "if a CSS edit is needed" guidance).

**Created:** None.

**Deleted:** None.

## Commits

- `93c39d2` — `fix(11-08): convert info-tooltip to details/summary (WDG-05 Sweep 8)`

## Known Stubs

None. The disclosure widget renders real, user-readable text (the same copy that was previously stored in the now-removed `title` attribute) and is wired to no JS — there are no placeholders or empty/mock-data sites in this plan.

## Threat Flags

None. The change is HTML structure + CSS only. No new network endpoints, auth paths, file-access patterns, or schema changes are introduced.

## Self-Check: PASSED

- File `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` exists and contains `<details` + `<summary` and no `<span class="info-tooltip" title=` — confirmed.
- File `DeckFlow.Web/Views/Commander/CommanderCategories.cshtml` exists and contains `<details` + `<summary` and no `<span class="info-tooltip" title=` — confirmed.
- File `DeckFlow.Web/wwwroot/css/site-common.css` exists and contains the WDG-05 banner block — confirmed.
- Commit `93c39d2` exists in `git log` — confirmed.
- `dotnet build DeckFlow.sln --configuration Release` exited 0 with 0 warnings / 0 errors — confirmed.
