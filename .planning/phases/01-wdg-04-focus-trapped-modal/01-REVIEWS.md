---
phase: 1
reviewers: [claude-plan-checker, codex]
reviewed_at: 2026-05-23T17:41:00Z
plans_reviewed: [01-01-PLAN.md]
codex_verdict_final: APPROVED (round 3)
claude_verdict: VERIFICATION_PASSED (with 1 warning, addressed in r1)
ship_gate: GREEN_LIGHT_EXECUTE_PHASE
convergence_rounds: 3
convergence_history:
  - round: 1
    verdict: REVISE_REQUIRED
    concerns: [HIGH-scope-grep-whole-file, HIGH-truths2-overstatement, MED-showConfirm-fail-closed, MED-danger-hover-cascade]
  - round: 2
    verdict: REVISE_REQUIRED
    resolved_from_r1: [HIGH-scope-grep-whole-file, HIGH-truths2-overstatement, MED-showConfirm-fail-closed, MED-danger-hover-cascade]
    new_concerns: [HIGH-hex-grep-counts-comments, MED-try-block-grep-line-local]
  - round: 3
    verdict: APPROVED
    resolved_from_r2: [HIGH-hex-grep-counts-comments, MED-try-block-grep-line-local]
    new_concerns: []
---

# Cross-AI Plan Review — Phase 1 (WDG-04 Focus-Trapped Modal)

## Codex Review (AUTHORITATIVE per CLAUDE.md cross-AI rule)

**Verdict:** **REVISE_REQUIRED** — 2 HIGH severity issues block execute-phase.

**Summary:** Implementation shape is sound (native `<dialog>`, IIFE/global TypeScript, structural Razor partial, scoped admin CSS, CSRF-preserving form submit). Closes the targeted `onsubmit` in `Detail.cshtml:39`. Two plan/gate issues can block or misstate completion.

### Strengths

- Correctly handles `module: "none"` with `window.DeckFlowAdminModal` matching `tsconfig.json:4` + existing `df-select.ts` pattern
- Preserves server security boundary: `AdminFeedbackController.Apply` remains `[ValidateAntiForgeryToken]`; all 3 form tokens preserved
- Correctly fixes native `<dialog>` misconception per MDN: `showModal()` defaults to `closedby="closerequest"` — backdrop click needs explicit handling
- Scope is tight: 5 source files, no layout/controller/package churn, no new deps

### Concerns

#### 🔴 HIGH: Task 3 scope-discipline grep is guaranteed to fail on the existing file

The proposed `grep -vE ... admin.css | grep -vE ...` runs against ALL of `admin.css`, not just the new Phase 1 block. Pre-existing unchanged lines already trip the rule:
- Multiline comment bodies
- `:root {`
- `[role="tab"]:focus-visible {`
- Grid-template string lines

**Effect:** Automated scope gate is unusable even for a correct implementation. False-positive failure on every run.

**Fix:** Rewrite scope guard to inspect only added lines OR only the new `/* === Phase 1 (v1.4) — WDG-04 === */` section. Use `awk` to extract section + `grep` on that subset; OR `git diff` against pre-Phase-1 commit + `grep` on `^+` lines only.

#### 🔴 HIGH: must_haves truth #2 contradicts success_criteria SC#2 (Phase 1 cannot verify what frontmatter claims)

Frontmatter `must_haves.truths[1]` claims: *"Tab/Shift+Tab cycle stays inside modal across native form controls AND nested `df-select`/`df-typeahead`, verified by HUMAN-UAT in a Razor view containing both"*.

But `success_criteria` SC#2 correctly notes: Phase 1's `Detail.cshtml` does NOT contain `df-select`/`df-typeahead`; nested-custom-element verification deferred to Phase 7 reuse per RESEARCH UAT-9.

**Effect:** With the listed 5 files, Phase 1 CANNOT satisfy must_have truth #2 as written. Either the truth lies (overstates Phase 1 coverage), or the plan is missing a synthetic Razor view that nests `df-select`/`df-typeahead` for UAT-only verification.

**Fix:** Either (a) reword must_haves truth #2 to scope it to native button cycling for Phase 1 + defer custom-element cycling to Phase 7 with explicit cross-reference, OR (b) add a synthetic verification harness view to Phase 1 scope (likely overkill for a single-modal phase).

*(Claude plan-checker flagged this as WARNING; Codex elevates to HIGH because the wording can falsely-pass at verification time if it's interpreted literally.)*

#### 🟡 MEDIUM: `showConfirm` says "any error resolves false" but task does not implement that

`dialog.showModal()` can throw if dialog is already open or unavailable. Older/unsupported dialog environments also fail. Without try-catch guard, the promise REJECTS instead of fail-closing.

**Fix:** Task 1 action must include:
```typescript
if (dialog.open) { resolve(false); return; }
if (typeof dialog.showModal !== 'function') { resolve(false); return; }
try { dialog.showModal(); } catch { resolve(false); return; }
```
Plus cleanup function around the failure paths to detach event listeners.

#### 🟡 MEDIUM: Danger hover color visually altered by existing + new hover filters

Existing `.admin-action-form button:hover { filter: brightness(1.1); }` at `admin.css:138` STILL applies to `button.danger` after Phase 1 adds the `.danger` rule (CSS cascade). Planned `.admin-modal__button--confirm:hover { filter: brightness(1.1); }` (if present) also applies to `.admin-modal__button--confirm.admin-modal__button--danger`.

**Effect:** Specified hover `#b91c1c` is NOT actually rendered — `#dc2626 + filter: brightness(1.1)` produces a different visual.

**Fix:** Change hover selectors to exclude danger:
- `.admin-action-form button:not(.danger):hover { filter: brightness(1.1); }`
- `.admin-modal__button--confirm:not(.admin-modal__button--danger):hover { filter: brightness(1.1); }`
- Add `.admin-modal__button--danger:hover { filter: none; background: #b91c1c; }`

### Risk Assessment

**Implementation risk:** MEDIUM (well-designed but two real footguns)
**Plan execution risk:** HIGH (verification gate guaranteed-fail + must_have overstatement)

### Verdict

**REVISE_REQUIRED** — fix 2 HIGH issues before execute-phase. MEDIUM fixes recommended but not blocking.

---

## Claude Plan-Checker Review (Sonnet, secondary gate per workflow)

**Verdict:** **VERIFICATION PASSED** with 1 minor warning.

10/10 focus dimensions passed:
1. Goal-backward coverage (4/4 SCs mirrored verbatim to must_haves.truths)
2. CONTEXT D-01..D-09 (all 9 reflected; D-09 scope guards enforced)
3. RESEARCH corrections incorporated (D-01 IIFE, D-03 backdrop click, D-06 focus restore)
4. UI-SPEC contract preserved (28 grep assertions verbatim + WIG amendments)
5. R-3 anchored greps (mostly compliant; minor unanchored greps use unambiguous unique tokens)
6. R-6 formatting paranoia (Format-Document forbidden, LF preserved, byte-identical preservation)
7. Threat model (3 trust boundaries + 8 STRIDE entries; CSRF preserved)
8. Task atomicity (concrete identifiers throughout)
9. No v1.3 regression risk (497-test baseline gated)
10. Scope guards verifiable (Verification Gate 4 enforces 7 out-of-bounds paths)

**Single warning:** must_haves.truths[1] overstates Phase 1 verification scope re: nested custom-element cycling. (Codex elevates to HIGH.)

---

## Consensus

### Agreed Strengths
- IIFE pattern + window.DeckFlowAdminModal namespace correct (both reviewers)
- AntiForgeryToken preservation + CSRF boundary preservation correct (both)
- Scope guards via Verification Gate 4 enforce D-09 (both)
- 28 grep UI-SPEC assertions land verbatim in Task 3 (both)
- Threat model coverage adequate for modest UX-only attack surface (both)

### Agreed Concerns
- **must_haves truth #2 overstates Phase 1 coverage** (Claude: WARNING; Codex: HIGH)
  → Reword truth #2 OR add synthetic UAT harness (Codex recommends defer to Phase 7 cross-reference)

### Codex-Only HIGH (Claude missed)
- **Task 3 scope-discipline grep guaranteed-fail on existing file** — Claude's review didn't actually test the grep against current admin.css contents; Codex did.

### Codex-Only MEDIUMs (Claude missed)
- `showConfirm` fail-closed not actually implemented (try-catch missing)
- Danger hover broken by `filter: brightness(1.1)` cascade

---

## Decision

**BLOCKED** per CLAUDE.md cross-AI rule (Codex authoritative, HIGH severity blocks execute).

### Required Actions Before Execute

1. **Fix scope-discipline grep** (Task 3 verify block) — scope grep to Phase 1 CSS section only, not whole file
2. **Reword must_haves truth #2** — scope to Phase 1 native button cycling; explicit Phase 7 cross-reference for nested custom-element cycling
3. **Recommended: also fix MEDIUMs** — add try-catch + dialog.open guard in admin-modal.ts; fix danger hover cascade in CSS

### Options

- `/gsd-plan-phase 1 --reviews` — replan with this REVIEWS.md as input
- Manual surgical edit of PLAN.md for the 2 HIGH fixes (smaller scope)
- Discuss specific fixes inline

---

## Convergence Trail

### Round 2 (after r1 planner revision)

**Codex verdict:** REVISE_REQUIRED — 4/4 r1 concerns RESOLVED but 2 NEW surfaced from the revision:

1. **NEW HIGH:** `grep -ciE '#dc2626'` would count both declaration + revision-comment hex mentions (`/* hover would render as #dc2626 + brightness(1.1), NOT the UI-SPEC #b91c1c. */`) → expect 1, actual 2. Gate guaranteed-fail.
2. **NEW MEDIUM:** `grep -cE "try \{[^}]*dialog\.showModal\(\)"` was line-local. Natural multi-line formatting (`try {\n  dialog.showModal();\n} catch {}`) would fail the gate even on correct implementation.

**r2 surgical edits applied:**

- **Fix NEW HIGH:** Replaced unanchored `grep -ciE '#dc2626'` with declaration-anchored `grep -cE '^[[:space:]]+background:[[:space:]]*#dc2626'` (and same for `#b91c1c`). Counts only CSS `background: #...` declarations, NOT comment-text mentions. Verified against simulated post-Phase-1 content: matches exactly 1 of each.
- **Fix NEW MEDIUM:** Replaced line-local grep with `tr '\n' ' ' < ... | grep -cE 'try[[:space:]]*\{[^{}]*dialog\.showModal\(\)[^{}]*\}[[:space:]]*catch'`. Newline-collapse + non-greedy brace matching handles both single-line and multi-line try-catch formatting.

### Round 3 (after r2 surgical edits)

**Codex verdict:** **APPROVED**. Both r2 NEW concerns RESOLVED. Zero further NEW concerns. Ship recommendation: **GREEN_LIGHT_EXECUTE_PHASE**.

### Convergence Summary

- 3 rounds of cross-AI review (Codex authoritative per CLAUDE.md)
- 6 total concerns surfaced + resolved (4 from r1 review of initial plan + 2 from r2 review of r1 revision)
- All issues mechanically verifiable (anchored greps, multiline-tolerant patterns)
- No HIGH or MEDIUM concerns remain
- Plan ready for `/gsd-execute-phase 1`
