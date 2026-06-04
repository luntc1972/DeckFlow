# Phase 28: Housekeeping Bundle - Context

**Gathered:** 2026-06-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Clear v1.4 quality debt across three independent tracks — no web surface, no new features:

1. **HSK-02** — KB-12 codex distill backend: replace the `NotSupportedException` stub in `LlmDistillationProviderFactory` with a working `codex` provider via the existing `CliCommandSpec` + `CliEnvelopeKind.Raw` seam, with a **proven** untrusted-input read-isolation boundary.
2. **HSK-03** — VERIFICATION.md hygiene: backfill the 7 missing v1.4 VERIFICATION files (phases 16, 17, 18, 21, 21.2, 22, 23) and correct stale UAT labels.
3. **HSK-04** — v1.4 artifact hygiene: P26 missing SUMMARYs, P24 quick-fix artifact chain, dual KB artifact-tree drift, and (added during discussion) the duplicated v1.4 milestone-audit file.

</domain>

<decisions>
## Implementation Decisions

### Codex isolation bar (HSK-02 acceptance)
- **D-01:** The **full backlog KB-12 acceptance bar** applies — not just Phase 28 SC1's "no NotSupportedException". Promoting KB-12 out of backlog without the security bar would dissolve the reason it was demoted. Acceptance = proven untrusted-input read isolation + sentinel-file exfil test + same JSON-repair/ValidateTags/timeout/ledger-bypass guarantees as the claude backend.
- **D-02:** Isolation mechanism is **researcher-decided**: the phase researcher investigates codex CLI options (no-tools config, sandbox modes, stdin-only container) and picks the one that is *provable*. Do not pre-commit to a mechanism in planning before research evidence.
- **D-03:** **Fallback if no provable read boundary exists:** re-demote HSK-02 to backlog with findings documented. Do NOT ship behind a warning. The claude backend (Phase 21.2) already covers the subscription-distill use case, so dropping codex is acceptable.
- **D-04:** Sentinel exfil test is a **permanent automated xUnit regression test** in `DeckFlow.Core.Tests` (seam-level: malicious transcript + sentinel file, assert sentinel content never appears in output), PLUS a one-time live probe against the real codex CLI documented in the phase VERIFICATION.

### Codex UAT depth (HSK-02 verification)
- **D-05:** **Full backlog UAT**: live codex distill over the existing 10-video UAT db; valid artifacts emitted; E5/E6 human-sample quality check — the same gate the claude backend passed in Phase 21.2. Consistent bar across providers.
- **D-06:** Spend=0 expectation (subscription-backed CLI) carried from backlog acceptance — locked, not re-discussed.
- **D-07:** Codex model for distillation is **configurable via env/config** (same pattern as the claude backend), **defaulting to a mini-tier model** — distillation is extraction, not deep reasoning.

### VERIFICATION backfill method (HSK-03)
- **D-08:** **Retro-document** all 7 missing VERIFICATION.md files from existing evidence (UAT checkpoint files, `v1.4-MILESTONE-AUDIT.md` requirements table, SUMMARY frontmatter, commit history). Each file carries an explicit **"retroactive" marker** and evidence citations. No gsd-verifier re-runs — the milestone audit already cross-verified all requirements satisfied.
- **D-09:** Backfilled files are written **directly into the archive dirs** (`.planning/milestones/v1.4-phases/<phase>/`) — no restore-to-active-tree/re-archive churn.
- **D-10:** **Stale-label sweep is global**: grep ALL v1.4 VERIFICATION/UAT files for `human_needed` / `partial` / `unknown` and correct each with an evidence citation — not just the audit-named `20-VERIFICATION.md` (human_needed → UAT passed 2026-05-27).

### Artifact-tree + hygiene depth (HSK-04)
- **D-11:** Dual KB artifact-tree drift resolved by **code fix**: change the CLI default output (when `MTG_DATA_DIR` unset) to the repo-root `content-kb/` tree (deploy source of truth); `artifacts/content-kb/` is retired. One tree, drift impossible. See `CommandRunners.cs:1554`.
- **D-12:** **Retro-SUMMARYs for both P26 and P24**: write `26-01-SUMMARY.md` / `26-02-SUMMARY.md` from git history + the ROADMAP verification block (including the SC2 amendment b1a5cc8); for P24 write a retro SUMMARY + VERIFICATION citing `24-UAT.md` (live smoke PASSED 2026-05-25) and the integration-checker evidence (`CategoryKnowledgeRepository.cs:227`). Same retro-marker convention as D-08.
- **D-13:** **Audit-file dedup folded into HSK-04**: delete the `.planning/v1.4-MILESTONE-AUDIT.md` root copy; keep `.planning/milestones/v1.4-MILESTONE-AUDIT.md` (matches v1.2/v1.3 archive convention).

### Claude's Discretion
- Plan split/sequencing across the three tracks (they are independent; HSK-02 is the only code track).
- Exact retro-marker wording and VERIFICATION file structure (follow GSD template conventions).
- Commit granularity for archive-dir doc edits.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + acceptance
- `.planning/ROADMAP.md` — Phase 28 block (goal, SC 1-3) AND the **Backlog → "Codex Distill Backend"** section, which holds the full KB-12 acceptance criteria (read isolation, sentinel exfil test, guarantees parity, live UAT, E5/E6) that D-01 locks in.
- `.planning/REQUIREMENTS.md` — HSK-02/03/04 definitions.
- `.planning/milestones/v1.4-MILESTONE-AUDIT.md` — authoritative tech-debt list HSK-03/04 resolve: 7 missing VERIFICATION files (16, 17, 18, 21, 21.2, 22, 23), stale 20-VERIFICATION label, P24 weak chain, P26 missing SUMMARYs, dual artifact trees. (Root copy `.planning/v1.4-MILESTONE-AUDIT.md` is deleted per D-13 — cite the milestones/ path.)

### Codex backend implementation seam
- `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` — lines 15, 49-52: the `NotSupportedException` stub to replace.
- `DeckFlow.Core/Integration/CliCommandSpec.cs` — `CliEnvelopeKind` enum; codex uses `CliEnvelopeKind.Raw` (per HSK-02 requirement text).
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs` — existing claude CLI backend: envelope handling (`ExtractModelText`, line 271+), command-spec construction (line 174), the pattern codex must follow.
- `DeckFlow.CLI/CommandRunners.cs` — line ~1554: CLI artifact-output default path (HSK-04 D-11 code fix site).
- `.planning/milestones/v1.4-phases/21.2-pluggable-llm-distill-cli-backends-inserted/` — Phase 21.2 artifacts: provider factory design, claude backend UAT gate (the bar D-05 mirrors).

### Evidence sources for retro-docs (HSK-03/04)
- `.planning/milestones/v1.4-phases/24-card-category-lookup-fix-colorless-staple-cards/24-UAT.md` — P24 live smoke evidence.
- `.planning/milestones/v1.4-phases/26-category-cache-schema-normalization-fresh-start/` — 26-01/26-02 PLAN files (SUMMARYs to be written); SC2 amendment commit b1a5cc8.
- `.planning/milestones/v1.4-phases/20-content-kb-ingestion-transcription-local/20-VERIFICATION.md` — the named stale label (human_needed → UAT passed 2026-05-27).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CliLlmDistillationService` + `CliCommandSpec` + `LlmDistillationProviderFactory` (Phase 21.2): the pluggable CLI backend seam — codex provider slots in with a new command-spec builder + `CliEnvelopeKind.Raw` extraction path (already handled at `CliLlmDistillationService.cs:273`).
- Existing claude-backend test suite in `DeckFlow.Core.Tests` — pattern for seam-level distill tests; the sentinel exfil test (D-04) follows it.
- 10-video UAT db from Phase 21.2 — reused as-is for D-05 live UAT.

### Established Patterns
- Provider selection via `DECKFLOW_LLM_PROVIDER` env var; model config via env (claude backend pattern) — D-07 follows it.
- Retro-doc convention: explicit marker + evidence citation (established this phase via D-08, applies to D-12).
- xUnit for Core tests; no new test frameworks or packages without explicit approval.

### Integration Points
- `LlmDistillationProviderFactory.cs:49-52` — codex branch replaces throw.
- `CommandRunners.cs:1554` — CLI default artifact path change (D-11).
- No web-surface changes anywhere in this phase.

</code_context>

<specifics>
## Specific Ideas

- Security bar is non-negotiable: "codex `exec` is an agent and `--sandbox read-only` blocks writes but not reads, so a prompt-injected transcript could read+echo local files; ship codex only once the read boundary is demonstrably closed" (ROADMAP backlog text — D-01 adopts it verbatim).
- E5/E6 human-sample check mirrors the exact gate Phase 21.2 used for the claude backend.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (Spike todo `spike-combo-data-to-primer-grounding.md` surfaced in todo matching but is tagged `resolves_phase: 31`; keyword-noise match, not folded.)

</deferred>

---

*Phase: 28-Housekeeping Bundle*
*Context gathered: 2026-06-04*
