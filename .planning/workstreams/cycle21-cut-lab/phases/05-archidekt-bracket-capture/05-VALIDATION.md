---
phase: 05
slug: archidekt-bracket-capture
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-29
---

# Phase 05 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 / Microsoft.NET.Test.Sdk 17.14.1 |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`, `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Quick run command** | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ArchidektApiDeckImporterTests|FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests" --no-restore` |
| **Full suite command** | `dotnet.exe test DeckFlow.sln --no-restore` |
| **Estimated runtime** | ~30-180 seconds depending on suite selection |

---

## Sampling Rate

- **After every task commit:** Run the narrowest relevant focused test filter for changed files; keep this as the fast feedback loop.
- **After every plan wave:** Run `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore` and any changed Web test project slice.
- **Before `/gsd:verify-work`:** Full solution build and relevant test projects must be green.
- **Max focused feedback latency target:** 30 seconds for per-task filters. Slower Core/Web project slices, gated Postgres tests, and full-solution checks are wave/phase verification rather than per-task feedback.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | BRKT-01 | T-05-01 | Archidekt payload parsed once; no second request for metadata | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektApiDeckImporterTests --no-restore` | Yes | pending |
| 05-01-02 | 01 | 1 | BRKT-02, BRKT-03 | T-05-02 | Nullable schema migration preserves existing rows and captured timestamp distinguishes absent metadata | unit/schema | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests|FullyQualifiedName~CategoryCacheSchemaParityTests" --no-restore` | Yes | pending |
| 05-01-03 | 01 | 1 | BRKT-01, BRKT-03 | T-05-03 | Bulk and URL imports write metadata only after successful payload parse | unit/integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ArchidektDeckCacheSessionTests --no-restore` and matching Web controller tests | Yes | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. Add or extend tests in the existing xUnit projects; no new test framework is needed.

---

## Manual-Only Verifications

All phase behaviors should have automated verification. Manual production verification is limited to optional post-deploy SQL inspection of `deck_queue` metadata coverage and is not required for local phase completion.

---

## Validation Sign-Off

- [x] All tasks have automated verify commands or existing test infrastructure
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target documented
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
