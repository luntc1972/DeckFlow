---
phase: 112
slug: cycle-17-code-port
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-24
---

# Phase 112 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `112-RESEARCH.md` §Validation Architecture (build-proven in a disposable worktree).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| **Config file** | none — plain `.csproj`-driven, no `xunit.runner.json` |
| **Quick run command** | `dotnet build DeckFlow.sln` |
| **Full suite command** | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj && dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Estimated runtime** | build ~6–25s; full suite minutes |

**WSL caveat (project convention):** VSTest is unreliable under WSL. Run tests through the Windows dotnet at `"/mnt/c/Program Files/dotnet/dotnet.exe"`, or push and watch CI. Do NOT set `MTG_DATA_DIR`. `dotnet build` is the primary, reliable gate for this phase's build and DI claims.

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln` (fast, ~6–25s observed)
- **After each of the two port commits (D-07):** targeted `dotnet test --filter` scoped to creator-style suites, for fast isolation of port-specific breaks
- **Before `/gsd:verify-work`:** full suite green on both test projects
- **Max feedback latency:** ~25s at task granularity

---

## Baseline (captured on `feat/personal-tools` BEFORE the port)

Success criterion 2 is "no **new** errors and no **new** warnings" — a delta, not an absolute. The baseline must be captured before the first port commit or the delta is unprovable.

| Metric | Baseline | How captured |
|--------|----------|--------------|
| Build errors | 0 | `dotnet build DeckFlow.sln` |
| Build warnings | 9 | `dotnet build DeckFlow.sln` warning count, recorded in `112-RESEARCH.md` |
| Core.Tests / Web.Tests pass counts | see RESEARCH §"Current green baseline" | ⚠ MEDIUM confidence — static `[Fact]`/`[Theory]` proxy count, not an actual `dotnet test` run. **A plan task must re-capture this by actually running the suites before the first port commit.** |

---

## Per-Task Verification Map

Task IDs are assigned when plans are written; rows below are the requirement-level contract each plan's tasks must satisfy.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | 1 | PORT-01 | — | N/A | build | `dotnet build DeckFlow.sln` — 0 errors, warning count ≤ 9 | ✅ existing tooling | ⬜ pending |
| TBD | TBD | 1 | PORT-01 | — | N/A | unit | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CreatorStyle\|StatedRules\|ProfileFusion\|MeasuredStyleExtraction\|CardGrounding"` | ✅ ported per RESEARCH §"Port Allowlist — Commit 1" | ⬜ pending |
| TBD | TBD | 2 | PORT-02 | — | N/A | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | ✅ existing tooling | ⬜ pending |
| TBD | TBD | 2 | PORT-02 | T-112-01 | Real DI graph resolves; no `KeyNotFoundException` from an unregistered Polly key | integration | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CreatorStyleDiRegistration"` | ❌ W0 — needs strengthening, see below | ⬜ pending |
| TBD | TBD | 2 | PORT-02 | — | N/A | unit | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~ProgramStartup"` (D-19 fork-join dual-fault logging) | ✅ ported | ⬜ pending |
| TBD | TBD | 1–2 | PORT-01, PORT-02 | — | N/A | gate | `scripts/format-check-changed.sh staged` per commit (D-04) | ✅ existing script | ⬜ pending |
| TBD | TBD | 1–2 | PORT-01, PORT-02 | — | N/A | audit | `git diff --name-status main` contains only allowlisted paths (D-08), plus the never-arrived grep | ✅ existing tooling | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] **Strengthen `CreatorStyleDiRegistrationTests.cs`** — port as-is, then add a companion assertion that resolves `IArchidektOwnerClient` (or something transitively requiring it, e.g. `CreatorProfileDeckCrawler`) through the **real** `ArchidektOwnerClient`, not `FakeArchidektOwnerClient`. Per D-20 / RESEARCH Finding 2, the faked version cannot detect the missing-`archidekt`-pipeline failure mode, so success criterion 3 would pass on paper while the real app throws. This is the single most load-bearing test in the phase.
- [ ] **Add `<None Include="StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt">`** to `DeckFlow.Core.Tests.csproj` — build-verified requirement for `StatedRulesExtractorTests.cs`'s golden fixture. **Do NOT** add the `Testcontainers.PostgreSql` `PackageReference` alongside it; D-15 excludes the tests that would need it, and CLAUDE.md forbids unapproved package additions.
- [ ] **Re-capture the real test-pass baseline** by running both suites before the first port commit (the RESEARCH figure is a static proxy count).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| App starts locally with all creator-style services resolving (ROADMAP success criterion 3) | PORT-02 | Startup is a process-level behavior; the automated D-13/D-20 test covers the DI graph but not the full host boot including the D-19 seed fork-join | Run `scripts/run-web-test.sh` (sets `DECKFLOW_DISABLE_AUTO_BROWSER=true` — never open a browser on the Windows host). Confirm no missing-registration exception and that the startup log line for seed load appears. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 25s at task granularity
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
