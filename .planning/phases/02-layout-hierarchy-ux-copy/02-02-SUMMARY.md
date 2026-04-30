---
phase: 02-layout-hierarchy-ux-copy
plan: 02
subsystem: web-razor
tags: [razor, markup, hub, feedback, admin, voice, partial]
requires:
  - phase-02-plan-01 (.hub-hero, .hub-card--primary, .feedback-panel amend, .admin-feedback-detail, .admin-action-form)
provides:
  - Home.cshtml hub hero band markup (UI-LH-01)
  - Home.cshtml 3 .hub-card--primary modifiers (UI-LH-01)
  - Feedback/Index.cshtml verb-noun voice + inline-style-free panel (UX-03 + UI-LH-02)
  - AdminFeedback Index.cshtml + Detail.cshtml inline-style-free markup (UI-LH-02)
  - _MoxfieldBulkEditHint verb-parameterized partial (UX-01)
  - 6 call-site verb wirings across 5 view files (UX-01)
affects:
  - DeckFlow.Web/Views/Deck/Home.cshtml
  - DeckFlow.Web/Views/Feedback/Index.cshtml
  - DeckFlow.Web/Views/AdminFeedback/Index.cshtml
  - DeckFlow.Web/Views/AdminFeedback/Detail.cshtml
  - DeckFlow.Web/Views/Shared/_MoxfieldBulkEditHint.cshtml
  - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
  - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
  - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
  - DeckFlow.Web/Views/Deck/DeckConvert.cshtml
  - DeckFlow.Web/Views/Deck/DeckSync.cshtml
tech-stack:
  added: []
  patterns:
    - Razor 1-arg partial (@model string) — analog _FormError.cshtml
    - Defensive IsNullOrWhiteSpace fallback in partial header
    - BEM modifier class composition (.hub-card.hub-card--primary)
key-files:
  created: []
  modified:
    - DeckFlow.Web/Views/Deck/Home.cshtml
    - DeckFlow.Web/Views/Feedback/Index.cshtml
    - DeckFlow.Web/Views/AdminFeedback/Index.cshtml
    - DeckFlow.Web/Views/AdminFeedback/Detail.cshtml
    - DeckFlow.Web/Views/Shared/_MoxfieldBulkEditHint.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml
    - DeckFlow.Web/Views/Deck/DeckConvert.cshtml
    - DeckFlow.Web/Views/Deck/DeckSync.cshtml
decisions:
  - "Hub hero is full-width <a> band above first .hub-group, linking to /chatgpt-packets (D-01, D-04)"
  - "Per-group .hub-card--primary applied only to Deck Comparison, Deck Sync, Card Lookup; Categories group intentionally excluded (D-02)"
  - "Feedback ViewData[\"Title\"] = \"Send Feedback\" — ASCII hyphen suffix from _Layout.cshtml renders as 'Send Feedback - DeckFlow' (UI-SPEC reconciliation, no em-dash override of layout)"
  - "_MoxfieldBulkEditHint partial uses @model string + @verb local with IsNullOrWhiteSpace fallback for defensive backward compat (D-07)"
metrics:
  duration: ~7 min (4 code commits + build)
  completed: 2026-04-30
  tasks: 5
  files: 10
  commits: 4
requirements:
  completed:
    - UI-LH-01
    - UI-LH-02
    - UX-01
    - UX-03
---

# Phase 02 Plan 02: Razor Markup Wiring Summary

**One-liner:** Wires every Razor markup change for Phase 02 — Home hub hero band + 3 per-group primary modifiers, Feedback page verb-noun voice fix, all flagged inline `style=` attributes removed across Feedback + AdminFeedback views, and `_MoxfieldBulkEditHint` parameterized with a host-page verb (6 call sites).

## What Shipped

| Surface | File | Change |
|---------|------|--------|
| Hub hero band | `DeckFlow.Web/Views/Deck/Home.cshtml` | Insert `.hub-hero` `<a>` after `.hub-lede`, before first `.hub-group` |
| Hub primary cards | `Home.cshtml` | Add `.hub-card--primary` to Deck Comparison, Deck Sync, Card Lookup |
| Feedback voice | `Feedback/Index.cshtml` | Title `"Feedback"` → `"Send Feedback"`, `<h1>Send feedback</h1>` → `<h1>Send Feedback</h1>`, button `Send` → `Send Feedback` |
| Feedback panel | `Feedback/Index.cshtml` | Drop inline `style="background: var(--panel); border: 1px solid var(--line);"` (now in `.feedback-panel` rule) |
| Admin archive form | `AdminFeedback/Index.cshtml:74` | `style="display:inline"` → `class="admin-action-form"` |
| Admin panel | `AdminFeedback/Detail.cshtml:6` | Drop 6-property inline style on `.admin-feedback-detail` (now in CSS rule) |
| Admin forms | `AdminFeedback/Detail.cshtml:27/34/39` | `style="display:inline"` → `class="admin-action-form"` on markRead, archive, delete |
| Partial parameterization | `Shared/_MoxfieldBulkEditHint.cshtml` | Add `@model string` + `var verb = string.IsNullOrWhiteSpace(Model) ? "Submit" : Model;` + step 3 uses `@verb` |
| Call-site verbs | 5 view files (6 occurrences) | Each `Html.PartialAsync("_MoxfieldBulkEditHint")` becomes 2-arg with host-page verb |

## Final hub-hero markup (verbatim)

```razor
<a class="hub-hero" href="@Url.Content("~/chatgpt-packets")">
    <span class="hub-hero__eyebrow">Headline workflow</span>
    <span class="hub-hero__title">Analyze Your Deck with ChatGPT</span>
    <span class="hub-hero__description">Five-step workflow: load your deck, pick your questions, copy the prompt, paste into ChatGPT, review the structured response.</span>
</a>
```

## Final .hub-card--primary lines (verbatim)

```razor
<a class="hub-card hub-card--primary" href="@Url.Content("~/chatgpt-deck-comparison")">
<a class="hub-card hub-card--primary" href="@Url.Content("~/sync")">
<a class="hub-card hub-card--primary" href="@Url.Content("~/card-lookup")">
```

## Verification Evidence

### UI-SPEC verification gates (#1, #2, #5, #6) — all PASS

```
=== UI-SPEC #1: Hub hierarchy ===
  hub-hero count: 1 (expected 1)
  hub-card--primary count: 3 (expected 3)
=== UI-SPEC #2: Zero inline styles in 3 flagged files ===
  Combined style= count: 0 (expected 0)
=== UI-SPEC #5: Verb-noun titles ===
  PASS
=== UI-SPEC #6: Partial verb param ===
  bare calls: 0 (expected 0)
  verb calls: 6 (expected 6)
=== ALL GATES PASS ===
```

### Combined `grep -c 'style='` across the 3 flagged files

```
DeckFlow.Web/Views/Feedback/Index.cshtml:0
DeckFlow.Web/Views/AdminFeedback/Index.cshtml:0
DeckFlow.Web/Views/AdminFeedback/Detail.cshtml:0
Sum: 0
```

D-15 verifier gate satisfied.

### Verb call-site map (all 6 occurrences)

| File | Line | Verb |
|------|------|------|
| `Views/Deck/ChatGptDeckComparison.cshtml` | 225 | `Run Compare` |
| `Views/Deck/ChatGptPackets.cshtml` | 150 | `Run Analysis` |
| `Views/Deck/ChatGptCedhMetaGap.cshtml` | 99 | `Run Gap Analysis` |
| `Views/Deck/DeckConvert.cshtml` | 67 | `Convert` |
| `Views/Deck/DeckSync.cshtml` | 112 | `Run Sync` |
| `Views/Deck/DeckSync.cshtml` | 147 | `Run Sync` |

Zero bare `Html.PartialAsync("_MoxfieldBulkEditHint")` calls remain.

### Build gate

`dotnet build DeckFlow.sln -c Debug` →

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:01:39.56
```

## Commits

| # | Hash | Subject |
|---|------|---------|
| 1 | `96d6cbc` | feat(02-02): add hub hero band and 3 .hub-card--primary modifiers on Home |
| 2 | `2db13a9` | feat(02-02): apply verb-noun voice and remove inline style on Feedback page |
| 3 | `c26f723` | feat(02-02): remove inline styles from admin feedback views |
| 4 | `c73d30e` | feat(02-02): parameterize _MoxfieldBulkEditHint with verb model |

## Forward Signal to Plan 03

The `form.feedback-form` selector is wired and ready for the Plan 03 TypeScript handler `attachFeedbackBusyState()`. Specifically:

- `Feedback/Index.cshtml:19` — `<form ... class="feedback-form" novalidate>` is unchanged (Plan 02 confirmed pre-wired).
- `Feedback/Index.cshtml:43` — `<button type="submit" class="feedback-submit">Send Feedback</button>` is the toggle target. Class is unchanged from Plan 01 expectations; only the button text was updated.
- The Plan 03 TS handler must NOT call `event.preventDefault()` (D-08); the form retains `novalidate` and posts normally.
- Plan 01 already shipped `.feedback-submit--busy` CSS rule; Plan 03 only needs to add the class on submit + swap text to "Sending…" + set `button.disabled = true`.

## Deviations from Plan

None — plan executed exactly as written. All five tasks landed verbatim per UI-SPEC values; no Rule 1/2/3 auto-fixes triggered; no Rule 4 architectural questions surfaced.

Note on the Task 4 verifier expression: the plan's per-page verb-pair check used `grep -rqE 'filename.cshtml.*Verb'` which depends on grep prefixing the filename in the matched line — POSIX grep matches against content only, so the regex is structurally unable to match. The actual semantic conditions (1 verb call per file, 2 in DeckSync) were verified via per-file `grep -c` instead, all 6 occurrences confirmed. This is a verifier-script limitation, not a markup defect — actual file contents match the spec.

## Known Stubs

None. All 5 tasks land complete, build-clean markup. The Plan 03 TS handler is its own plan (UX-02 / UI-SPEC §6) and is explicitly tracked separately, not a stub.

## Threat Flags

None. The threat model in 02-02-PLAN.md flagged `_MoxfieldBulkEditHint @verb` interpolation as `mitigate` — the mitigation (Razor's default HTML encoding via `@verb`) is applied automatically. All current callers pass hardcoded literal strings; no user input flows through this surface.

## Self-Check: PASSED

- File `.planning/phases/02-layout-hierarchy-ux-copy/02-02-SUMMARY.md` written.
- Commits `96d6cbc`, `2db13a9`, `c26f723`, `c73d30e` all present in `git log --oneline`.
- All 10 modified view files modified per plan.
- `dotnet build` clean: 0 Warning(s), 0 Error(s).
- All UI-SPEC gates #1, #2, #5, #6 PASS.
