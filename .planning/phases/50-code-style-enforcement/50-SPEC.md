# Phase 50: Code-Style Enforcement — ReSharper Reconciliation + PR Gate — Specification

**Created:** 2026-06-14
**Ambiguity score:** 0.16 (gate: ≤ 0.20)
**Requirements:** 5 locked

## Goal

The operator's ReSharper code-style is exported, diffed against the committed `.editorconfig`, and reconciled into it — with the five bug-driven carve-outs overriding any conflicting ReSharper preference — and the reconciled style is enforced on **new and changed lines only** via a local pre-commit hook and a CI gate, with no mass reflow of existing files and the project `CLAUDE.md` rewritten to name `.editorconfig` the enforced source of truth.

## Background

A committed `.editorconfig` already exists (121 lines). It was deliberately built (Phases 23/29) to make "Format Document" a **no-op** against existing code and to protect five bug-driven carve-outs, each with an in-file rationale:
- `{ get; init; }` accessors must not become `{ get; }` — .NET 9+ `JsonSerializer` silently skips get-only properties; stripping `init` has broken `EdhTop16Client` deserialization before (lines 45–48).
- Raw-string literal indentation must be preserved — re-indenting changes the literal value shipped to the AI (e.g. `analysisFollowUpPrompt` in `DeckAnalysis.cshtml`) (lines 79–82).
- `[Attribute]` placement stays on its own line — never inlined onto the property (lines 63–70).
- Switch expressions are preferred and preserved (lines 54–55).
- XML-doc comments use single-space indent, not ReSharper's 5-space wrap default (lines 72–76).

The operator's local ReSharper settings (in their Windows JetBrains profile, **not committed**) **differ** from this committed file. The request is to make the operator's ReSharper formatting the enforced standard. The tension: the project's `CLAUDE.md` currently *forbids* ReSharper-style reformatting outright, and the operator does **not** want existing files reflowed. Therefore reconciliation cannot be a blind overwrite — it must (a) capture the operator's RS prefs as `.editorconfig` keys, (b) reject any RS pref that collides with a carve-out, (c) enforce the result only on changed lines so untouched legacy code is never reformatted.

Enforcement infrastructure today: a CI workflow (`.github/workflows/ci.yml`) builds + runs xUnit/Vitest/Playwright; there is **no** formatting check and **no** git hook (default `.git/hooks`, no `.githooks` dir). `dotnet format` is available via the .NET SDK but operates on whole files by default — a changed-lines-only gate requires scoping it to the PR diff.

This phase is tech-debt/process, independent of the v1.7 publish-studio track. It lands **after** Phase 44 (shipped) and **before** Phase 49 (re-sequenced 2026-06-14 at operator request — original spec said after-49). Landing the format gate before the Dapper refactor means 49's new/changed data-access lines conform to the reconciled `.editorconfig` from the start; the changed-lines-only scope guarantees 49's untouched legacy code is never reflowed.

## Requirements

1. **Capture + reconcile ReSharper style**: The operator's ReSharper code-style is exported and merged into `.editorconfig`, carve-outs winning.
   - Current: `.editorconfig` encodes the codebase's existing style; the operator's divergent RS prefs are uncommitted and unrepresented
   - Target: The operator exports their ReSharper code-style (to `.editorconfig` keys or a `.DotSettings` translated to keys); a reconciliation pass merges non-conflicting prefs into the committed `.editorconfig`; any RS pref conflicting with a carve-out is rejected
   - Acceptance: A `50-RECONCILIATION.md` lists every RS-vs-existing difference with a KEEP-EXISTING / ADOPT-RS / CARVE-OUT-WINS resolution; the final `.editorconfig` still contains all five carve-out rules verbatim in effect; `git diff .editorconfig` shows only additive/keyed style rules, no carve-out weakening

2. **Carve-outs are non-negotiable**: The five bug-driven carve-outs override any conflicting ReSharper preference.
   - Current: Carve-outs live in `.editorconfig` (lines 45–82) and as prose in project `CLAUDE.md`
   - Target: After reconciliation, the carve-outs are still enforced; a conflicting RS pref is documented as rejected with its reason in the reconciliation report
   - Acceptance: A guard test/script asserts the four code-affecting carve-outs round-trip unchanged — a sample `{ get; init; }`, a raw-string literal, an own-line `[Attribute]`, and a switch expression are byte-identical before/after running the formatter with the new config

3. **Changed-lines-only CI gate**: CI fails a PR when added/modified lines violate `.editorconfig`, but does not flag untouched legacy lines.
   - Current: `ci.yml` has no formatting step
   - Target: A CI job runs a formatter/verify scoped to the PR diff (changed files/lines) and fails the build on violations; existing unmodified lines are not evaluated
   - Acceptance: A test PR that adds a deliberately mis-formatted line fails CI; a test PR that only touches one line in a legacy file with pre-existing (non-carve-out) style quirks elsewhere passes; CI logs show the gate ran

4. **Pre-commit hook**: The same changed-lines check runs locally before commit.
   - Current: No git hook (default `.git/hooks`, no `.githooks`)
   - Target: A versioned hook (e.g. `.githooks/pre-commit` + a documented `git config core.hooksPath .githooks` opt-in, or equivalent) runs the changed-lines formatter check and blocks the commit on violation
   - Acceptance: With the hook installed, committing a mis-formatted staged change is blocked with a clear message; committing clean changes succeeds; the hook is tracked in the repo and its install step is documented in README/CLAUDE.md

5. **Project CLAUDE.md updated**: The blanket "never reformat" rule is replaced with the source-of-truth model.
   - Current: Project `CLAUDE.md` Constraints/Formatting sections forbid ReSharper-style reformatting outright
   - Target: That rule is replaced with: `.editorconfig` is the enforced source of truth; the five carve-outs remain protected and override conflicting prefs; new/changed code must satisfy the gate; existing files are not mass-reflowed
   - Acceptance: `CLAUDE.md` no longer contains a blanket "DO NOT run Format Document / never reformat" prohibition; it instead references the gate + carve-out protection; the five carve-out specifics are still present (not deleted)

## Boundaries

**In scope:**
- Export the operator's ReSharper code-style and produce a reconciliation report
- Merge non-conflicting RS prefs into the committed `.editorconfig`
- Preserve all five carve-outs as overriding rules
- A changed-lines-only formatting gate in CI (`ci.yml`)
- A versioned pre-commit hook running the same check, with documented install
- Rewrite the project `CLAUDE.md` formatting rule to the source-of-truth model
- Documentation of how to install the hook and how the gate behaves

**Out of scope:**
- **Mass reflow of existing files** — explicitly excluded; the operator chose new/changed-lines-only, and whole-repo reformat risks the carve-out bugs
- **Committing a `.DotSettings` as the source of truth** — `.editorconfig` is the tool-agnostic source of truth; a `.DotSettings` may be exported as an input but is not the enforced artifact
- **Weakening or removing any carve-out** — they are protected by REQ-2
- **Reformatting non-C# assets beyond what `.editorconfig` already covers** (TS/CSS/JSON style overhaul) — only the existing `.editorconfig` scopes apply
- **Editing the global `~/.claude/CLAUDE.md`** — only the project-repo `CLAUDE.md` is touched
- **Blocking-merge severity tuning beyond pass/fail** — the gate is binary; no per-rule severity dashboards

## Constraints

- **Carve-outs override everything.** Any RS pref that would re-indent raw strings, strip `init`, inline attributes, rewrite switch expressions, or 5-space-indent xmldoc is rejected — these are bug-driven, not aesthetic.
- **No mass reflow.** The phase's own `git diff` must show no whole-file formatting churn; only `.editorconfig`, `ci.yml`, the hook, docs, and `CLAUDE.md` change in code terms.
- **Changed-lines scoping is required**, not whole-file `dotnet format` — otherwise touching one line in a legacy file would force reformatting the entire file and reintroduce carve-out risk. (Implementation mechanism is a discuss-phase decision.)
- **LF line endings preserved** (`.gitattributes`); the hook/gate must not normalize EOL on the `.ps1/.bat/.cmd` CRLF exception files.
- **Both protected files are editable this task** — `.editorconfig` and project `CLAUDE.md` — per explicit operator permission; the global config file is not.
- **CI must stay green and not slow materially** — the format job runs alongside existing build/test, not replacing them.

## Acceptance Criteria

- [ ] `50-RECONCILIATION.md` exists, listing every RS-vs-existing difference with a KEEP/ADOPT/CARVE-OUT-WINS resolution
- [ ] Reconciled `.editorconfig` still enforces all five carve-outs; a guard test proves `{ get; init; }`, a raw-string literal, an own-line `[Attribute]`, and a switch expression are byte-identical after formatting
- [ ] CI gate fails a PR with a mis-formatted added line; passes a PR that only touches a legacy file without introducing new violations
- [ ] Versioned pre-commit hook blocks a mis-formatted staged commit and allows a clean one; install step documented
- [ ] Phase `git diff` shows no whole-file reflow of existing source — only config/CI/hook/doc files change
- [ ] Project `CLAUDE.md` replaces the blanket "never reformat" rule with the `.editorconfig`-source-of-truth model, carve-out specifics retained
- [ ] CI remains green end-to-end with the new job present

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                        |
|--------------------|-------|------|--------|--------------------------------------------------------------|
| Goal Clarity       | 0.86  | 0.75 | ✓      | Reconcile RS→editorconfig, enforce changed-lines, no reflow  |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | Explicit: no mass reflow, no .DotSettings SoT, carve-outs win |
| Constraint Clarity | 0.82  | 0.65 | ✓      | Carve-out override + changed-lines scoping are hard rules     |
| Acceptance Criteria| 0.82  | 0.70 | ✓      | 7 pass/fail checks incl. carve-out guard test                |
| **Ambiguity**      | 0.16  | ≤0.20| ✓      | Residual: exact changed-lines tooling = discuss-phase HOW     |

Status: ✓ = met minimum, ⚠ = below minimum (planner treats as assumption)

## Interview Log

| Round | Perspective     | Question summary                                  | Decision locked                                                                 |
|-------|-----------------|---------------------------------------------------|---------------------------------------------------------------------------------|
| 0     | Researcher      | What enforcement exists today? (scouted)          | `.editorconfig` exists (carve-outs); CI has no format gate; no git hook          |
| 1     | Boundary Keeper | Resolve the CLAUDE.md "never reformat" conflict    | Codify in `.editorconfig` (tool-agnostic), update CLAUDE.md to source-of-truth   |
| 1     | Boundary Keeper | Reflow existing code, or gate new/changed only?    | New/changed lines only — existing files untouched                                |
| 1     | Failure Analyst | How does the PR gate enforce?                      | Pre-commit hook + CI gate                                                         |
| 2     | Researcher      | Does operator's ReSharper match the config?        | Differs — phase must export RS, diff, reconcile (carve-outs win)                 |
| 2     | Boundary Keeper | May the two protected files be edited?             | Yes — `.editorconfig` + project `CLAUDE.md`, this task                            |

---

*Phase: 50-code-style-enforcement*
*Spec created: 2026-06-14*
*Next step: /gsd:discuss-phase 50 — implementation decisions (changed-lines formatter mechanism, hook install model, reconciliation workflow)*
