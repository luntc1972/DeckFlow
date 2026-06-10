---
phase: 17
reviewers: [codex]
reviewed_at: 2026-05-24
plans_reviewed: [17-01-PLAN.md, 17-02-PLAN.md]
---

# Cross-AI Plan Review — Phase 17

> Claude CLI skipped (self — review ran inside Claude Code). Gemini/Cursor not installed. Codex is the authoritative plan reviewer per project CLAUDE.md.

## Codex Review

**Summary**

The plans are close, but should NOT be executed as-is. Runtime risk is low (comment-only), but plan-completion risk is real: the checked-in plans reference a non-existent phase path, and current per-type scanning shows `ScryfallCard` in `ScryfallDtos.cs:40` will still fail the declaration-anchored rule because its existing summary is separated from the record by a blank line.

**Strengths**
- Per-type verification (not file-level `grep -L '<summary>'`) is the right failure mode.
- R-6 discipline: no `get; init;` churn, no attribute inlining, no broad formatting.
- `NoWarn` retention explicit and matches `DeckFlow.Web.csproj`.
- 17-01 and 17-02 source file sets are disjoint → safe to parallelize.
- D-01..D-04 mostly clear, especially interface-prose / impl-`<inheritdoc/>` split.

**Concerns**
- **HIGH — Plan paths wrong.** Both plans reference `.planning/phases/02-doc-comment-backfill-part-1-controllers-services/...` but the actual dir is `17-doc-comment-backfill-part-1-controllers-services/`. Breaks context loading / writes summaries to wrong place. *(Root cause: the 1→16 / 2→17 phase renumber renamed file prefixes but missed directory-name path strings inside plan bodies.)*
- **HIGH — `ScryfallCard` omitted.** Lacks a declaration-attached doc comment at `ScryfallDtos.cs:40`: a summary exists at lines 36–38 but the blank line at 39 detaches it from the record declaration. 17-02 only covers `ScryfallCardFace`, so the per-type SC1 can still fail.
- **MEDIUM — Source-of-truth counts conflict.** `17-CONTEXT.md:11` says 45 undocumented types; roadmap/plans cover a smaller cluster; Codex scan shows 19 missing declaration-attached docs. Plan should state the authoritative count and why the 45 inventory is stale.
- **MEDIUM — 17-02 count mismatch.** Says "10 public Services types" but its own list is 11 (4 interfaces + 4 impls + 2 records + 1 static class). `17-02-PLAN.md:319`.
- **LOW — Verification still relies on visual confirmation after grep.** Automate the per-type gate now that this blind spot already caused a miss.

**Suggestions**
1. Fix all `02-doc-comment...` references to `17-doc-comment...` (context, read_first, output).
2. Add `ScryfallCard` to 17-02 (Task 2 or a small Task 4). Cleanest: delete the blank line between its existing summary and declaration (requires relaxing "only added `///` lines" to allow one blank-line deletion); else add a fresh summary immediately above.
3. Replace manual per-type verification with an automated checker that fails on declarations lacking attached `summary`/`inheritdoc`; keep file-level grep as secondary smoke check.
4. Enumerate D-02-triggering methods in `IFeedbackStore` (as the plan already does for `BuildSetPacketAsync`).
5. Clarify the test gate: run the full configured test command, or cite the WSL/VSTest constraint and make `dotnet build -c Release` the local gate + CI/push-watch for full suite.

**Risk Assessment: MEDIUM.** Runtime/security risk low (XML comments only). Execution risk medium: two concrete issues can make the phase look complete while failing its own acceptance criteria (wrong phase paths, omitted/detached `ScryfallCard`). Fix before handing to Codex.

---

## Consensus Summary

Single external reviewer (Codex). Net: plans are structurally sound for a comment-only phase, but **blocked on 2 HIGH issues** before execution.

### Agreed Strengths
- Per-type verification approach; R-6 discipline; NoWarn retention; disjoint parallel-safe file sets.

### Agreed Concerns (must fix before execute)
- HIGH: stale `02-` directory path strings in plan bodies (renumber miss).
- HIGH: `ScryfallCard` declaration-attached doc gap (blank-line detachment) not covered.

### Divergent Views
- None (single reviewer).

---

## Convergence Trail (Codex rounds)

**Round 1** — 2 HIGH (stale `02-` paths; ScryfallCard detached), 2 MED (count drift), 1 LOW. → orchestrator fixed paths; planner `d25597b` fixed ScryfallCard + counts + awk gate + IFeedbackStore enum + test-gate.

**Round 2** — ScryfallCard RESOLVED. NEW: member-level D-02/inheritdoc enumerated for only 2 of 4 interface pairs (HIGH); FeedbackStore inheritdoc-only fails file-level grep (HIGH); awk gate too loose (MED). → planner `d8bb59c` enumerated all 4 pairs + per-member inheritdoc; demoted file-level grep to non-blocking; tightened awk gate to require `<summary>`/`<inheritdoc`.

**Round 3** — member coverage RESOLVED; awk gate RESOLVED; ROADMAP drift RESOLVED. Remaining HIGH: ROADMAP §17 SC1 text still asserted file-level grep "must also pass" + "every type has `<summary>`" (conflicts with inheritdoc-only impls). NEW MED: member-level completeness still manual (awk checks type decls only). → orchestrator rewrote SC1: per-declaration check accepting `<summary>` OR `<inheritdoc/>` is authoritative; file-level grep explicitly NON-BLOCKING.

**MED accepted (not blocking):** member-level inheritdoc/param completeness is verified via explicit per-member acceptance items in the plan, not the awk gate. Full C#-member automated parsing is out of scope for a doc phase.

**Round 4** — ROADMAP SC1 conflict RESOLVED. **VERDICT: GREEN (no HIGH).** MED (manual member-level verification) accepted non-blocking for a doc-only phase. Plans cleared for execution.
