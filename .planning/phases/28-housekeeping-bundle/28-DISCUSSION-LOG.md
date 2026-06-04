# Phase 28: Housekeeping Bundle - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-04
**Phase:** 28-Housekeeping Bundle
**Areas discussed:** Codex isolation bar, Codex UAT depth, VERIFICATION backfill method, Artifact-tree + hygiene depth

---

## Codex isolation bar

| Option | Description | Selected |
|--------|-------------|----------|
| Full backlog bar | Proven read isolation + sentinel-file exfil test; demotion reason was exactly this | ✓ |
| Reduced bar | Verify + document codex no-tools/sandbox config; skip adversarial exfil test | |
| Minimal bar | Wire CliEnvelopeKind.Raw path; document residual risk; match Phase 28 SC1 wording only | |

**User's choice:** Full backlog bar

| Option | Description | Selected |
|--------|-------------|----------|
| Researcher decides | Researcher investigates codex CLI options (no-tools config, sandbox modes, stdin-only container), picks the provable one | ✓ |
| Codex no-tools config | Lock to codex exec with tool use disabled via config flags | |
| Container/stdin-only | Lock to sandbox/container exposing only stdin | |

**User's choice:** Researcher decides

| Option | Description | Selected |
|--------|-------------|----------|
| Re-demote HSK-02 | Drop from Phase 28, back to backlog with findings; security bar absolute | ✓ |
| Ship behind warning | Implement anyway; docs + CLI warning that transcripts must be trusted | |
| Pause and ask | Surface findings mid-phase, decide then | |

**User's choice:** Re-demote HSK-02 (fallback if no provable boundary)

| Option | Description | Selected |
|--------|-------------|----------|
| Automated xUnit test | Permanent seam-level regression test in DeckFlow.Core.Tests + one-time live probe in VERIFICATION | ✓ |
| Live probe only | One-time manual exfil probe, documented in VERIFICATION.md | |
| Both, live probe scripted | Suite test + committed scripts/ probe for repeatable live runs | |

**User's choice:** Automated xUnit test

---

## Codex UAT depth

| Option | Description | Selected |
|--------|-------------|----------|
| Full backlog UAT | Live codex distill over 10-video UAT db; valid artifacts; E5/E6 human sample — same gate as claude backend 21.2 | ✓ |
| Single transcript | One live test transcript end-to-end (Phase 28 SC1 wording) | |
| Seam tests only | No live codex run this phase | |

**User's choice:** Full backlog UAT
**Notes:** Spend=0 + JSON-repair/ValidateTags/timeout/ledger-bypass parity locked from backlog acceptance — not re-asked.

| Option | Description | Selected |
|--------|-------------|----------|
| Configurable, default mini | Model via env/config (claude backend pattern); default mini-tier — distillation is extraction | ✓ |
| Match claude-backend tier | Consistency over cost | |
| Researcher decides | Researcher checks codex CLI model flags + quality needs | |

**User's choice:** Configurable, default mini

---

## VERIFICATION backfill method

| Option | Description | Selected |
|--------|-------------|----------|
| Retro-document | Write from existing evidence with explicit 'retroactive' marker + citations | ✓ |
| Re-run verifier | gsd-verifier per phase against current code (7 agent runs) | |
| Hybrid | Retro most; verifier only for weakest chains | |

**User's choice:** Retro-document

| Option | Description | Selected |
|--------|-------------|----------|
| Archive dirs | Write directly into .planning/milestones/v1.4-phases/<phase>/ | ✓ |
| Active tree then re-archive | Restore, backfill, re-archive | |

**User's choice:** Archive dirs

| Option | Description | Selected |
|--------|-------------|----------|
| Sweep all v1.4 files | Grep all VERIFICATION/UAT files for human_needed/partial/unknown; correct with citations | ✓ |
| Audit-listed only | Fix only 20-VERIFICATION.md | |

**User's choice:** Sweep all v1.4 files

---

## Artifact-tree + hygiene depth

| Option | Description | Selected |
|--------|-------------|----------|
| CLI default → repo tree | CLI default output becomes repo-root content-kb/ when MTG_DATA_DIR unset; artifacts/content-kb/ retired | ✓ |
| Drift check only | Keep both trees; add diff/warn check | |
| Document only | README note declaring canonical tree; no code | |

**User's choice:** CLI default → repo tree

| Option | Description | Selected |
|--------|-------------|----------|
| Retro-SUMMARYs both | 26-01/26-02 SUMMARYs from git history + ROADMAP block; P24 retro SUMMARY + VERIFICATION citing 24-UAT.md | ✓ |
| P26 only | Leave P24 as-is | |
| Pointer stubs | Minimal stubs, no narrative reconstruction | |

**User's choice:** Retro-SUMMARYs both

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, keep milestones/ copy | Delete root v1.4-MILESTONE-AUDIT.md; milestones/ matches v1.2/v1.3 convention | ✓ |
| Yes, keep root copy | Delete milestones/ copy | |
| Leave both | Identical today; don't touch | |

**User's choice:** Yes, keep milestones/ copy (discovered during scout — folded into HSK-04)

---

## Claude's Discretion

- Plan split/sequencing across the three independent tracks
- Retro-marker wording and VERIFICATION file structure
- Commit granularity for archive-dir doc edits

## Deferred Ideas

- None. (Todo `spike-combo-data-to-primer-grounding.md` matched on keywords but is tagged resolves_phase: 31 — not folded.)
