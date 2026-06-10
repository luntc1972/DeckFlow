---
status: complete
phase: 28-housekeeping-bundle
source: [28-01-SUMMARY.md, 28-02-SUMMARY.md, 28-03-SUMMARY.md, 28-04-SUMMARY.md]
started: 2026-06-04T14:19:57-06:00
updated: 2026-06-04T14:30:00-06:00
---

## Current Test

[testing complete]

## Tests

### 1. Retro v1.4 VERIFICATION files present
expected: The v1.4 archive (.planning/milestones/v1.4-phases/) contains VERIFICATION.md files for phases 16, 17, 18, 21, 21.2, 22, and 23 — each marked retroactive: true and citing v1.4-MILESTONE-AUDIT.md.
result: pass

### 2. Phase 20 status corrected to passed
expected: Archived 20-VERIFICATION.md and 20-HUMAN-UAT.md show status: passed with a dated provenance note citing the 2026-05-27 UAT pass.
result: pass

### 3. CLI content-kb artifact root unified (D-11)
expected: Running a DeckFlow.CLI content-kb command without MTG_DATA_DIR set writes artifacts under ./content-kb at the current directory (repo root) — no second artifact tree appears at the old split location.
result: pass

### 4. Duplicate milestone audit removed
expected: .planning/v1.4-MILESTONE-AUDIT.md (root copy) is gone; only .planning/milestones/v1.4-MILESTONE-AUDIT.md remains.
result: pass

### 5. Retro Phase 26/24 summaries present
expected: Archive contains 26-01-SUMMARY.md, 26-02-SUMMARY.md, 24-SUMMARY.md, and 24-VERIFICATION.md, all marked retroactive: true.
result: pass

### 6. HSK-02 re-demote tracked consistently (D-03)
expected: 28-DISCOVERY.md exists with the codex isolation evidence; ROADMAP backlog "Codex Distill Backend" entry carries the 2026-06-04 findings + re-investigation trigger; Phase 28 SC #1 amended; 28-04 marked SKIPPED; REQUIREMENTS.md HSK-02 marked re-demoted.
result: pass

### 7. Codex provider stub intact, no code shipped
expected: LlmDistillationProviderFactory still throws NotSupportedException for the codex provider; openai and claude distill paths build and behave unchanged (solution builds clean).
result: pass

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
