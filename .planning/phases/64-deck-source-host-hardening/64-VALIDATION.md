---
phase: 64
slug: deck-source-host-hardening
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-21
---

# Phase 64 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests) |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` (existing) |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~DeckSourceHost"` |
| **Full suite command** | `dotnet test DeckFlow.Core.Tests` |
| **Estimated runtime** | ~30–60 seconds (full Core suite) |

> **WSL caveat (CLAUDE.md):** VSTest is unreliable under WSL. Local gate is `dotnet build` clean; authoritative test execution is CI (push-and-watch) or a Windows `dotnet.exe` run. Host-predicate tests are pure in-memory (no HTTP), so they are deterministic wherever the runner works.

---

## Sampling Rate

- **After every task commit:** Run the quick command (host-predicate filter)
- **After every plan wave:** Run the full Core suite
- **Before `/gsd:verify-work`:** Full Core suite green + `dotnet build` clean (0 warnings)
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| host-predicate helper | 1 | SEC-01 | T-64-01 (host spoof) | `IsMoxfield`/`IsArchidekt` accept only apex + `.apex` subdomain; reject `moxfield.com.evil.tld`, `evilmoxfield.com`, `moxfield.com@evil.tld`, trailing-dot | unit | `dotnet test --filter ~DeckSourceHost` | ❌ W0 (new `DeckSourceHostTests.cs`) | ⬜ pending |
| DeckEntryLoader adoption | 2 | SEC-01 | T-64-01 | Loader routes/rejects by helper, not substring | unit | `dotnet test --filter ~DeckEntryLoader` | ✅ (`DeckEntryLoaderTests.cs`) | ⬜ pending |
| Moxfield importer adoption | 2 | SEC-01 | T-64-01 | Importer host check uses helper | unit | `dotnet test --filter ~MoxfieldApiDeckImporter` | ✅ (`MoxfieldApiDeckImporterTests.cs`) | ⬜ pending |
| Spellbook canonical forward | 2 | SEC-02 | T-64-02 (URL forward) | Fallback forwards only `https://moxfield.com/decks/{deckId}`; submitted URL never forwarded | unit | `dotnet test --filter ~MoxfieldApiDeckImporter` | ✅ (`MoxfieldApiDeckImporterTests.cs`) | ⬜ pending |
| PacketArtifactStore adoption | 2 | SEC-01 | T-64-01 | 4th call site uses helper (no substring remains) | unit | `dotnet test --filter ~PacketArtifactStore` | ❌ W0 (verify test exists / add) | ⬜ pending |
| Substring-regression guard | 2 | SEC-03 | T-64-01 | Each spoof case rejected for BOTH Moxfield + Archidekt; fails if `.Contains(` host match reintroduced | unit | `dotnet test --filter ~DeckSourceHost` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/DeckSourceHostTests.cs` — pure predicate matrix (accept/reject table) for SEC-01 + SEC-03
- [ ] Confirm `DeckFlow.Web.Tests` has (or add) a PacketArtifactStore host-gate test for SEC-01 4th-site coverage

*Existing infrastructure (xUnit) covers everything else — no framework install needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| (none) | — | All host-predicate + fallback behaviors are pure and unit-testable | — |

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (`DeckSourceHostTests.cs`)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
