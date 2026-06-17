---
phase: 47
slug: direct-prod-db-scp-publish-path
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-16
---

# Phase 47 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + bUnit 2.7.2 (DeckFlow.Studio.Tests, added Phase 46) |
| **Config file** | none — inherits from `DeckFlow.Studio.Tests.csproj` |
| **Quick run command** | `dotnet test DeckFlow.Studio.Tests/ --filter "DirectPush"` |
| **Full suite command** | `dotnet test DeckFlow.Studio.Tests/` |
| **Estimated runtime** | ~15 seconds (bUnit component tests) |

> Note: VSTest is unreliable in WSL (CLAUDE.md). Rely on `dotnet build` clean + the targeted `--filter "DirectPush"` harness, or push-and-watch CI for the authoritative run.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test DeckFlow.Studio.Tests/ --filter "DirectPush"`
- **After every plan wave:** Run `dotnet test DeckFlow.Studio.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| W0 | 01 | 0 | PUB-04/05 | — | test seams + fakes exist | scaffold | `dotnet build DeckFlow.Studio.Tests/` | ❌ W0 | ⬜ pending |
| diff | — | — | PUB-05 (SC1) | — | Diff shows New/Updated counts before any write | bUnit | `dotnet test ... --filter "DirectPush_DiffPreview_ShowsNewUpdatedCounts"` | ❌ W0 | ⬜ pending |
| gate | — | — | PUB-04 (SC2) | — | Confirmation checkbox gates Stage 2 (SCP) button | bUnit | `dotnet test ... --filter "DirectPush_CheckboxGates_ScpButton"` | ❌ W0 | ⬜ pending |
| order | — | — | PUB-04 (SC2) | — | Stage 3 (DB) button disabled until Stage 2 SCP full success | bUnit | `dotnet test ... --filter "DirectPush_Stage3Locked_UntilScpSuccess"` | ❌ W0 | ⬜ pending |
| safe-upsert | — | — | PUB-04 (SC3) | T-tamper-clobber | Only `UpsertContentColumnsOnlyAsync` called on prod (never full-row upsert) | bUnit | `dotnet test ... --filter "DirectPush_UsesContentColumnsOnlyUpsert"` | ❌ W0 | ⬜ pending |
| scp-fail | — | — | PUB-05 (SC4) | — | SCP partial failure → Stage 3 stays locked + per-file list shown | bUnit | `dotnet test ... --filter "DirectPush_ScpPartialFailure_Stage3Locked"` | ❌ W0 | ⬜ pending |
| db-fail | — | — | PUB-05 (SC4) | — | DB partial failure → per-row list shown; does not re-lock Stage 2 | bUnit | `dotnet test ... --filter "DirectPush_DbPartialFailure_PerRowListShown"` | ❌ W0 | ⬜ pending |
| redact | — | — | SC5 | T-info-secrets | Secrets (conn string, SSH host/user/key, remote path) never appear in rendered markup | bUnit | `dotnet test ... --filter "DirectPush_Secrets_NeverInMarkup"` | ❌ W0 | ⬜ pending |
| disabled | — | — | PUB-04 (SC2) | — | `not configured` (prod or SCP) disables all action buttons | bUnit | `dotnet test ... --filter "DirectPush_NotConfigured_ButtonsDisabled"` | ❌ W0 | ⬜ pending |
| diff-leak | — | — | SC5 | T-info-secrets / HIGH-2 | Diff-read exception (sentinel-bearing) → sanitized copy, no secret in markup | bUnit | `dotnet test ... --filter "DirectPush_DiffReadFailure_SecretsNeverSurface"` | ❌ W0 | ⬜ pending |
| db-leak | — | — | SC5 | T-info-secrets / HIGH-2 | DB-write exception (sentinel-bearing) → sanitized Reason, no secret in markup | bUnit | `dotnet test ... --filter "DirectPush_DbWriteFailure_SecretsNeverSurface"` | ❌ W0 | ⬜ pending |
| guard | — | — | PUB-04 (SC2) | MEDIUM-1 | Stage-3 invoked before SCP success → WriteRowsAsync hard-guard, no prod upsert | bUnit | `dotnet test ... --filter "DirectPush_Stage3InvokedBeforeScp_NoUpsert"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Plan/Wave columns finalized by the planner when task IDs are assigned.*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Studio/Services/ISshArtifactUploader.cs` — interface + `SshUploadResult` record (per-file outcome)
- [ ] `DeckFlow.Studio/Services/IProdStoreFactory.cs` — interface + `ProdStoreFactory` impl (on-demand Postgres `ContentSiteIndexStore`)
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` — per-file success/fail injection (no `SSH.NET` ref in test project)
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` — returns a second `FakeContentSiteIndexStore` representing prod rows (no live Postgres)
- [ ] `DeckFlow.Studio.Tests/DirectPushPageTests.cs` — stubs for all 8 req/SC test cases above

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live SCP upload to Render `/data` | PUB-04 (SC2) | Requires Render SSH access (operator-dependent; not in CI) | Operator configures `Studio:Scp:*` user-secrets, runs Stage 2, confirms files land under `/data/content-kb/{slug}/` |
| Live prod Postgres diff + upsert | PUB-05 (SC1/SC3) | Requires prod Render Postgres (operator-dependent; not in CI) | Operator queries prod before/after; confirms diff matches, rows upserted, `is_visible`/`is_evergreen` preserved |
| Partial-failure reconcile in live conditions | PUB-05 (SC4) | Real network/SSH failure modes can't be reliably simulated live | Operator induces a failure (e.g., bad path) and confirms per-item list is accurate enough to reconcile |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (5 files above)
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-16 (plans passed gsd-plan-checker + Codex peer review)
