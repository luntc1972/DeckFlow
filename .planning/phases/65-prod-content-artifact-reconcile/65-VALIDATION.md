---
phase: 65
slug: prod-content-artifact-reconcile
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-22
---

# Phase 65 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> This phase is investigation + operator-run reconcile; the only new code is a
> read-only `content-kb-check` CLI command. Validation centers on that command's
> orphan-detection logic plus the operator-run post-reconcile gate.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests) |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| **Quick run command** | `dotnet build --no-incremental 2>&1 \| tail -3` (format + compile gate) |
| **Full suite command** | `dotnet test DeckFlow.Core.Tests --no-build` |
| **Estimated runtime** | ~30–60 seconds build; tests ~10s |

> Note: VSTest is unreliable in WSL (per project CLAUDE.md). Primary gate is a clean
> `dotnet build`; targeted unit tests run via `dotnet test` filter or push-and-watch CI.

---

## Sampling Rate

- **After every task commit:** Run `dotnet build --no-incremental 2>&1 | tail -3`
- **After every plan wave:** Run `dotnet test DeckFlow.Core.Tests --no-build` (CLI scanner tests)
- **Before `/gsd:verify-work`:** `content-kb-check` exits 0 against post-reconcile local state
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 65-01-xx | 01 | 1 | DATA-01 | — | Serving-path source documented + inspection-confirmed (no DB body column) | manual / doc | inspection note in DECISION.md | n/a | ⬜ pending |
| 65-03-xx | 03 | 1 | DATA-02 | T-65-01 | Orphan scanner rejects `..`/rooted `artifact_path` before `Path.Combine` | unit | `dotnet test --filter "FullyQualifiedName~ContentKbOrphan"` | ❌ W0 | ⬜ pending |
| 65-03-xx | 03 | 1 | DATA-02 | — | Scanner classifies missing artifact as published-orphan only when `is_visible=TRUE && is_hidden=FALSE` | unit | `dotnet test --filter "FullyQualifiedName~ContentKbOrphan"` | ❌ W0 | ⬜ pending |
| 65-03-xx | 03 | 1 | DATA-02 | — | `content-kb-check` exit code = 1 when published-orphan count > 0, else 0 | unit + CLI | `dotnet test --filter "FullyQualifiedName~ContentKbCheck"` | ❌ W0 | ⬜ pending |
| 65-02-xx | 02 | 2 | DATA-02 | — | Chosen reconcile path executed; post-reconcile probe shows 0 published orphans | manual (operator) | prod probe Query B + `content-kb-check` | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/Content/ContentKbOrphanScannerTests.cs` — unit tests for the orphan-detection helper (missing-file detection, published vs hidden classification, path-traversal rejection)
- [ ] Extract orphan-detection as a pure function/helper in `DeckFlow.Core` (e.g. `ContentKbOrphanScanner`) so the CLI handler stays thin and the logic is unit-testable without console/IO glue

*If the scanner is not extracted to a pure helper, DATA-02 logic falls back to manual CLI runs only — surface that explicitly in the plan.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live site serves body from `/data` `.md`, not DB column | DATA-01 | Pure code-inspection + prod startup-log confirmation; no behavior change to assert | Cite `ContentKbController.Detail` + `ContentSiteIndexStore` DDL; confirm Render startup log `Content KB content base resolved to /data` |
| Published-orphan count on prod | DATA-02 | Read-only prod probe via Render MCP / Postgres console; not reproducible in CI | Run probe Query A/B/C (see RESEARCH.md) against `dpg-d7oj8iugvqtc73fso0g0-a` |
| Reconcile execution (re-upload / unpublish / delete / accept) | DATA-02 | Operator-run via Studio DirectPush or admin console; AI never writes prod | Execute chosen path; re-run probe + `content-kb-check`; record in DECISION.md |

---

## Validation Sign-Off

- [ ] All code tasks (Plan 03) have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (manual-only prod/doc tasks are inherent to this phase and flagged above)
- [ ] Wave 0 covers the orphan-scanner unit tests
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
