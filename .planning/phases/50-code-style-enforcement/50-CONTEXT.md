# Phase 50: Code-Style Enforcement — Context

**Gathered:** 2026-06-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Reconcile the operator's ReSharper code-style into the committed `.editorconfig` (the five bug-driven carve-outs win every conflict), then enforce the reconciled style on **new/changed lines only** via a versioned pre-commit hook + a CI gate — with no mass reflow of existing files — and rewrite the project `CLAUDE.md` formatting rule from "never reformat" to "`.editorconfig` is the enforced source of truth." Lands AFTER Phase 44 (shipped) and BEFORE Phase 49 (re-sequenced 2026-06-14 at operator request — original was after-49) so the reconciled style + gate are in place when 49's Dapper refactor lands; changed-lines-only scope means 49's untouched legacy code is never reflowed.

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**5 requirements are locked (FMT-01..05).** See `50-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `50-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- Export the operator's ReSharper code-style and produce a reconciliation report
- Merge non-conflicting RS prefs into the committed `.editorconfig`
- Preserve all five carve-outs as overriding rules
- A changed-lines-only formatting gate in CI (`ci.yml`)
- A versioned pre-commit hook running the same check, with documented install
- Rewrite the project `CLAUDE.md` formatting rule to the source-of-truth model
- Documentation of how to install the hook and how the gate behaves

**Out of scope (from SPEC.md):**
- Mass reflow of existing files (new/changed-lines-only is the explicit choice)
- Committing a `.DotSettings` as the source of truth (it is an input only)
- Weakening/removing any carve-out
- Reformatting non-C# assets beyond existing `.editorconfig` scopes
- Editing the global `~/.claude/CLAUDE.md` (only the project-repo `CLAUDE.md`)
- Blocking-merge severity tuning beyond pass/fail (gate is binary)

</spec_lock>

<decisions>
## Implementation Decisions

### Changed-lines scoping mechanism
- **D-01:** Use **true line-level (diff-intersect)** scoping, NOT file-level. Mechanism: run `dotnet format` against a copy/in-place-then-revert, `git diff` the result, and fail ONLY when the formatter's changes intersect the PR's added/modified line ranges. Rationale: SPEC REQ-3's acceptance literally requires that a PR touching one line in a legacy file with pre-existing (non-carve-out) quirks PASSES. File-level (`--verify-no-changes` on changed files) would false-fail if any newly-merged ReSharper rule makes a touched legacy file non-conformant — line-level is the only mechanism that survives that case. The existing `.editorconfig` is already a no-op against existing code (built Phases 23/29), but the reconciled RS additions may not be, so line-level is required, not optional.

### ReSharper export + reconciliation workflow
- **D-02:** Operator exports their ReSharper/Rider code-style via **"Export to .editorconfig"** (tool-agnostic keyed output), not `.DotSettings`. Codex/Claude then diffs the exported file against the committed `.editorconfig` and writes `50-RECONCILIATION.md` (every RS-vs-existing difference → KEEP-EXISTING / ADOPT-RS / CARVE-OUT-WINS), and merges only non-conflicting prefs into `.editorconfig`. Carve-outs win every collision.
- **D-03:** The exported RS `.editorconfig` input is **deleted after merge** — it is used transiently; `50-RECONCILIATION.md` is the permanent audit trail of exactly what was merged and why, so the raw input need not persist in the tree.

### Shared check (DRY)
- **D-04:** The CI gate and the pre-commit hook both invoke **ONE shared versioned script** (e.g. `scripts/format-check-changed.sh`) holding the diff-intersect line-scoping logic. The hook runs it against staged hunks; CI runs it against the PR-base diff. Rationale: the line-scoping logic lives once and cannot drift between local and CI. Cross-platform bash (WSL + Git-Bash on Windows).

### CI gate shape
- **D-05:** Add a **separate `format-gate` job** in `ci.yml`, parallel to `build-and-test`, with its own checkout (sufficient `fetch-depth` for the diff base). Runs on both `push` and `pull_request` (matching the current `ci.yml` triggers). Isolated pass/fail; does not slow or couple with the build/test job.

### Claude's Discretion
- **Pre-commit hook install model** (user said "you decide"): planner picks the lightest cross-platform (WSL + Windows) delivery. SPEC's suggested model is a versioned `.githooks/pre-commit` + a documented `git config core.hooksPath .githooks` opt-in; a bootstrap install script is an acceptable alternative. Must protect the `.gitattributes` LF/CRLF rules (no EOL normalization on the `.ps1/.bat/.cmd` CRLF-exception files).
- Exact `dotnet format` invocation + diff-intersect implementation details — planner/researcher determine the cleanest approach that actually achieves line-scoping on this repo.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked requirements
- `.planning/phases/50-code-style-enforcement/50-SPEC.md` — Locked requirements (FMT-01..05), boundaries, acceptance criteria. MUST read before planning.

### Files this phase edits (code terms)
- `.editorconfig` (121 lines) — the reconciliation target + source of truth; carve-outs at lines 45–82 (init accessors 45–48, switch expressions 54–55, attribute placement 63–70, xmldoc indent 72–76, raw-string indent 79–82). Editable this task per operator permission.
- `CLAUDE.md` (project root) — Constraints/Formatting sections; rewrite blanket "never reformat" → source-of-truth model, carve-out specifics retained. Editable this task per operator permission (NOT the global `~/.claude/CLAUDE.md`).
- `.github/workflows/ci.yml` — single `build-and-test` job today (ubuntu, push + pull_request); add the parallel `format-gate` job here.
- `.gitattributes` — `* text=auto eol=lf`; `.cs/.cshtml/.razor/.csproj` etc. pinned `eol=lf`. The hook/gate must not normalize EOL or touch the CRLF-exception files. (Reference only — not edited.)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `dotnet format` is available via the Windows .NET SDK (`format` verb present) — the formatter for the gate; reads `.editorconfig`.
- Existing `.editorconfig` was deliberately authored (Phases 23/29) to be a no-op against current code — existing files already pass the CURRENT config, so only newly-merged RS rules introduce potential legacy non-conformance (the reason D-01 needs line-level).

### Established Patterns
- CI: one `build-and-test` job on `ubuntu-latest`, steps = checkout → setup-dotnet → setup-node → restore → build (Release) → test → npm test → playwright e2e. New format job sits alongside.
- No git hooks today (`.git/hooks` default, no `.githooks` dir, `core.hooksPath` unset) — greenfield for the hook delivery.
- LF enforced repo-wide via `.gitattributes`; the hook/gate scripts must respect it.

### Integration Points
- CI gate + hook share `scripts/format-check-changed.sh` (D-04) — one diff-intersect implementation, two invocation contexts (staged hunks vs PR-base diff).
- Carve-out guard test (SPEC REQ-2): a sample `{ get; init; }`, raw-string literal, own-line `[Attribute]`, and switch expression must be byte-identical after formatting with the reconciled config.

</code_context>

<specifics>
## Specific Ideas

- The diff-intersect check is the load-bearing decision: it must let a one-line edit in a quirky legacy file pass while failing a mis-formatted ADDED line (SPEC REQ-3 test-PR acceptance). Prove both directions with test PRs.
- One shared script (D-04) is explicitly preferred over duplicated CI/hook logic to prevent local-vs-CI drift.
- `50-RECONCILIATION.md` is the durable record; the RS export file is throwaway (D-03).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope (HOW-only; the WHAT is locked by SPEC).

</deferred>

---

*Phase: 50-code-style-enforcement*
*Context gathered: 2026-06-14*
