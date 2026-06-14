# Phase 50: Code-Style Enforcement - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-14
**Phase:** 50-code-style-enforcement
**Areas discussed:** Changed-lines scoping, ReSharper export + reconcile, Hook install model, CI gate shape, Shared check, RS input disposition

---

## Changed-lines scoping

| Option | Description | Selected |
|--------|-------------|----------|
| True line-level (diff-intersect) | Run dotnet format on a copy, git-diff, fail only if changes intersect PR added/modified line ranges. Fully satisfies REQ-3. | ✓ |
| File-level + conformant-only rules | dotnet format --verify-no-changes on changed FILES; safe only if new rules already satisfied by existing code. | |
| You decide | Defer to researcher/planner. | |

**User's choice:** True line-level (diff-intersect)
**Notes:** REQ-3 acceptance literally requires a one-line edit in a quirky legacy file to PASS; newly-merged RS rules could make legacy code non-conformant, so file-level would false-fail. Line-level is required, not optional.

---

## ReSharper export + reconcile

| Option | Description | Selected |
|--------|-------------|----------|
| RS → .editorconfig export, Claude reconciles | "Export to .editorconfig" on Windows; agent diffs vs committed, writes 50-RECONCILIATION.md, merges non-conflicting (carve-outs win). | ✓ |
| .DotSettings → translated keys | Export .DotSettings; agent translates keys. More manual/error-prone; SPEC says .DotSettings is input only. | |
| You decide | Defer to researcher. | |

**User's choice:** RS → .editorconfig export, Claude reconciles
**Notes:** Tool-agnostic, matches SPEC source-of-truth model.

---

## Hook install model

| Option | Description | Selected |
|--------|-------------|----------|
| .githooks/ + core.hooksPath opt-in | Versioned hook + documented `git config core.hooksPath .githooks`. | |
| Bootstrap script auto-installs | scripts/install-hooks.{sh,ps1}. | |
| You decide | Planner picks lightest cross-platform delivery. | ✓ |

**User's choice:** You decide
**Notes:** Planner picks; must protect .gitattributes LF/CRLF rules. SPEC suggests .githooks + core.hooksPath opt-in as the baseline.

---

## CI gate shape

| Option | Description | Selected |
|--------|-------------|----------|
| Separate job, PR + push | New format-gate job parallel to build-and-test, own checkout w/ fetch-depth. | ✓ |
| Step inside build-and-test | Add format step to existing job. | |
| You decide | Defer to planner. | |

**User's choice:** Separate job, PR + push
**Notes:** Isolated pass/fail; doesn't slow/couple the build job. Matches current ci.yml push + pull_request triggers.

---

## Shared check

| Option | Description | Selected |
|--------|-------------|----------|
| One shared script | scripts/format-check-changed.sh — CI + hook both invoke it (hook on staged hunks, CI on PR-base diff). DRY. | ✓ |
| Separate implementations | Each does its own diff; risk of local/CI drift. | |

**User's choice:** One shared script
**Notes:** Line-scoping logic lives once; cross-platform bash (WSL + Git-Bash).

---

## RS input disposition

| Option | Description | Selected |
|--------|-------------|----------|
| Keep as phase artifact | Store rs-export under .planning/phases/50/. | |
| Delete after merge | Use transiently; 50-RECONCILIATION.md is the audit trail. | ✓ |

**User's choice:** Delete after merge
**Notes:** Reconciliation report records every diff + resolution, so the raw input need not persist.

---

## Claude's Discretion

- Pre-commit hook install model — planner picks lightest cross-platform (WSL + Windows) delivery; protect .gitattributes EOL rules.
- Exact `dotnet format` invocation + diff-intersect implementation details.

## Deferred Ideas

None — discussion stayed within phase scope (HOW-only; WHAT locked by SPEC).
