---
phase: 86
slug: ui-audit-re-score-studio-stage-4-admin-flags-closeout
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-05
---

# Phase 86 — Validation Strategy (THEME UI GAPS pass; requirement UIAUDIT-02)

> Per-phase validation contract. This is a CSS/markup visual-polish phase; validation is dominated by
> Playwright visual-regression + interaction assertions plus a full build/test gate. Scope = UIAUDIT-02 only.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (build/render) + Playwright 1.60 e2e (visual/interaction) |
| **Config file** | `DeckFlow.Web/playwright.config.ts` (webServer `--launch-profile http-no-browser`) |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` (expect 0/0) |
| **Full suite command** | build + `dotnet.exe test DeckFlow.sln` + `cd DeckFlow.Web && npx --no-install playwright test` (headless, via run-web-test.sh / http-no-browser; never opens a Windows browser) |
| **Estimated runtime** | build ~40s; xUnit ~2–3m; e2e ~2–4m |

---

## Sampling Rate

- **After every task commit:** `dotnet.exe build DeckFlow.sln` (0/0) + the task's `<automated>` grep/contrast gate.
- **After every plan wave:** run the touched e2e specs for that wave's themes.
- **Before `/gsd:verify-work`:** full build + full xUnit + full Playwright e2e green.
- **Max feedback latency:** ~40s (build) per task.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|--------|
| 86-01 | 01 | 1 | UIAUDIT-02 | — | N/A (no secret/endpoint) | grep-clean + build | zero `rgba(43,108,176,…)` in non-Jeskai CSS (via `grep … \| wc -l` == 0) + build 0/0 | ⬜ pending |
| 86-02 | 02 | 2 | UIAUDIT-02 | — | N/A | contrast + grep + build | every `.prompt-step-tab.is-active` *winning* block resolves `background: var(--accent)`; per-theme white-on-`--accent` ≥4.5:1 or `--accent-contrast` present; build 0/0 | ⬜ pending |
| 86-03 | 03 | 3 | UIAUDIT-02 | — | aria-label present (a11y) | grep + build | bucket toggle has `aria-label`; toggle rule has no standalone pill border; build 0/0 | ⬜ pending |
| 86-04 | 04 | 4 | UIAUDIT-02 | — | N/A | build | focused/expert rules produce a measurable delta on an always-rendered element; build 0/0 | ⬜ pending |
| 86-05 | 05 | 5 | UIAUDIT-02 | — | N/A | e2e + re-score (human-verify) | new specs green: active-tab computed bg ≠ inactive ≠ `--panel-soft-bg` across {≥1 light @import, jund, dimir, Classic=explicit site.css} desktop+mobile; clear-cache hover bg not blue on non-Jeskai; mode toggle measurable layout delta + guided positive-style assertion; full xUnit + full e2e green; `tasks/UI-REVIEW.md` re-score ≥20/24 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Web/e2e/theme-active-affordance.spec.ts` — visual-regression: active-tab computed background differs (proves filled pill) across representative themes, desktop+mobile (created in 86-05).
- [ ] `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts` — interaction: Compact/Advanced produce a measurable layout delta vs guided; guided has a positive style (created in 86-05).

*Existing Playwright infra (`playwright.config.ts`, `deckflow-theme` cookie pattern in bracket-smoke/print-button specs) covers everything else.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 6-pillar UI re-score ≥20/24 | UIAUDIT-02 | Subjective per-pillar scoring against `tasks/UI-REVIEW.md` rubric | Re-run the 6-pillar audit post-fix across representative themes desktop+mobile; record per-pillar deltas; confirm ≥20/24. Human sign-off on the filled-pill look + per-theme contrast. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (the 2 new e2e specs)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
