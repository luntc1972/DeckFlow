---
phase: 46
slug: review-queue-commit-publish-path
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-16
---

# Phase 46 — Validation Strategy

> Reconstructed from artifacts (State B) on 2026-06-16. No VALIDATION.md existed at
> execution time; this audit cross-referenced the five plan SUMMARYs against the test
> tree, then filled the Studio UI coverage gap with a new bUnit test project.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (Core + Studio) · bUnit 2.7.2 (Studio Blazor component tests) |
| **Config file** | none — `DeckFlow.Core.Tests.csproj`, `DeckFlow.Studio.Tests.csproj` |
| **Quick run command** | `dotnet test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj -c Debug` |
| **Full suite command** | `dotnet test DeckFlow.sln -c Debug` |
| **Estimated runtime** | ~1s (Studio.Tests) · ~30s (phase Core.Tests filter) |

> WSL VSTest is unreliable; the verified runner is the Windows host:
> `"/mnt/c/Program Files/dotnet/dotnet.exe" test ...`.

---

## Sampling Rate

- **After every task commit:** Run the relevant project's test command
- **After every plan wave:** Run the full suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 46-01 | 01 | 1 | REVQ-02, REVQ-03 | T-46-01-01..04 | Approval-status mutation: allow-list validation, atomic batch transaction, admin-field preservation, Dapper-parameterized keys | unit | `dotnet test DeckFlow.Core.Tests --filter ContentSiteIndexStoreApprovalTests` | ✅ | ✅ green (10 facts) |
| 46-02 | 02 | 1 | PUB-03 | T-46-02-01,04,06 | LF-only approved-seed write; data-root→repo artifact copy with both-ends containment guard; git shell-out via ArgumentList, no push verb | unit | `dotnet test DeckFlow.Core.Tests --filter "ContentIndexSeedWriteTests|ContentArtifactCopyTests"` | ✅ | ✅ green (13 facts) |
| 46-03 | 03 | 2 | REVQ-02, REVQ-03 | T-46-03-01..04 | Review queue: filter-tab counts, per-row optimistic approve/reject calls store, atomic batch overload, natural-key derivation, path-containment graceful-degrade | bUnit | `dotnet test DeckFlow.Studio.Tests --filter ReviewPageTests` | ✅ | ✅ green (11 tests) |
| 46-04 | 04 | 3 | PUB-03 | T-46-04-01,02,05,06,09 | Publish: branch/approved-count display, export+copy+diff stage, reviewed-diff commit gate, scoped commit, SHA + push reminder, foreign-staged error surfaced, IGitRepository has no push method (structural) | bUnit | `dotnet test DeckFlow.Studio.Tests --filter PublishPageTests` | ✅ | ✅ green (10 tests) |
| 46-05 | 05 | 3 | REVQ-02, REVQ-03, PUB-03 | — | Phase-level gate: full sln build 0/0, changed-lines format gate clean | gate | `dotnet build DeckFlow.sln -c Debug` + `bash scripts/format-check-changed.sh ci` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing xUnit infrastructure covered the Core requirements at execution time. The Studio
UI surface had **no test project** at execution (documented accepted gap in 46-05-SUMMARY).
This audit closed that gap by adding `DeckFlow.Studio.Tests` (bUnit 2.7.2) — 21 component
tests for `Review.razor` and `Publish.razor`. No remaining MISSING references.

---

## Manual-Only Verifications

*All phase behaviors now have automated verification.*

The Plan 03 / Plan 04 human-verify checkpoints (cosmetic tinting, spinner copy, exact
Bootstrap classes) remain useful smoke checks but are no longer the SOLE verification of
any requirement — the behavioral contracts (REVQ-02, REVQ-03, PUB-03) are covered by the
bUnit suite.

---

## Audit Findings

**Product bug caught by new bUnit coverage (fixed 2026-06-16):**
`Publish.razor` `CommitAsync` set `_diffReady = false` on a successful commit, which tore
down the `@if (_diffReady)` Stage-2 card that *contained* the success alert — so a
successful commit rendered neither the commit SHA nor the `git push origin` reminder,
defeating PUB-03's post-commit guidance. Fix: the `@if (_commitSuccess)` success/SHA/push
block was moved outside the `_diffReady` guard. Regression guard:
`PublishPageTests.SuccessfulCommit_ShowsShaAndPushReminder`.

---

## Validation Sign-Off

- [x] All tasks have automated verification (no Wave 0 dependencies outstanding)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (Studio UI gap closed)
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-16
