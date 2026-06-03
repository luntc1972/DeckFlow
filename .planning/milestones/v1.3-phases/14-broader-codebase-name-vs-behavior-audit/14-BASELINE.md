# Phase 14 Baseline (captured by Plan 14-01)

**Date:** 2026-05-17
**HEAD:** 421108589f91712967ba9ab2420a14c59357c9cd
**Branch:** v1.3
**Captured by:** Plan 14-01 executor

Phase 14 baseline SHA: 421108589f91712967ba9ab2420a14c59357c9cd

Downstream plans read this SHA via:

    PHASE_14_START_SHA=$(grep -oE 'Phase 14 baseline SHA: [0-9a-f]+' .planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE.md | awk '{print $NF}')

and scope cross-plan history queries as `git log "${PHASE_14_START_SHA}..HEAD"`.

---

## Build state pre-phase

### D-09 literal baseline command

    "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --verbosity quiet 2>&1 | grep -cE '^.*warning '

Run from the main repo root (`/mnt/c/users/chrislunt/source/personal/deckflow`), where
`DeckFlow.Web/node_modules/typescript` is populated. This command must be re-run identically
at end of Plan 14-04 and the result must equal the baseline warning count.

**Note on worktree environment:** The git worktree used for Plan 14-01 execution does not
contain `DeckFlow.Web/node_modules/` (gitignored; not present in worktree checkout). The
D-09 command was executed from the main repo checkout at the same HEAD SHA
(`421108589f91712967ba9ab2420a14c59357c9cd`). Plans 14-02/03/04 must also run build
verification from the main repo or ensure `npm install` is run in the worktree first.

- `dotnet build DeckFlow.sln -c Release` exit code: **0**
- Warning count (BASELINE_WARN_COUNT): **0**
- Error count: **0**
- Build time: ~4.85 seconds
- XML doc files produced: `DeckFlow.Web/bin/Release/net10.0/DeckFlow.Web.xml` only
  (other 4 projects: `GenerateDocumentationFile` OFF)
- `DeckFlow.Web.xml` `<member>` count: **825**

**AUDIT-03 verification approach overridden — see 14-AUDIT-REPORT.md → XML Coverage Diff section.**
`.editorconfig` silences CS1591/1573/1587 globally (lines 93-96, committed `0f38cce`),
so warning-count alone is necessary-but-not-sufficient. Two gates required: warning count
AND XML coverage diff (Option A from RESEARCH.md "GenerateDocumentationFile Reality Check").

---

## Public type counts per project (grep-derived)

Command used per project:

    grep -rE "^[[:space:]]*public +(sealed +)?(class|interface|record|abstract +class|static +class|partial +class) +[A-Z]" --include="*.cs" $PROJECT/

Full output saved to `14-BASELINE-PUBLIC-TYPES.txt` (sibling file).

| Project | Count |
|---------|------:|
| `DeckFlow.Core` | 44 |
| `DeckFlow.Web` | 208 |
| `DeckFlow.CLI` | 0 |
| `DeckFlow.Core.Tests` | 10 |
| `DeckFlow.Web.Tests` | 56 |
| **Total** | **318** |

---

## Test-double prefix distribution (D-05)

### Canonical prefixes (Fake/Stub/Throwing)

    grep -rEn "(private|public|internal) +sealed +class +(Fake|Stub|Throwing)[A-Z]" --include="*.cs" DeckFlow.Web.Tests/ DeckFlow.Core.Tests/ | wc -l

**Result: 65 canonical test-double declarations**

### Non-canonical prefix hits (target for D-05 renames in Plan 14-02)

    grep -rEn "(private|public|internal) +sealed +class +(Null|Test|Configurable|Capturing|Dummy|Successful|Failing|Mock|Empty|Spy|Recording)[A-Z]" --include="*.cs" DeckFlow.Web.Tests/ DeckFlow.Core.Tests/

**Result: 8 non-canonical hits** (full list):

| File:line | Class | Non-canonical prefix |
|-----------|-------|----------------------|
| `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs:144` | `NullTempDataProvider` | `Null` |
| `DeckFlow.Web.Tests/CommanderControllerTests.cs:117` | `DummyCommanderSearchService` | `Dummy` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:831` | `ConfigurableMetaGapService` | `Configurable` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:870` | `CapturingDeckAnalysisPacketService` | `Capturing` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:939` | `SuccessfulCardLookupService` | `Successful` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:948` | `SuccessfulSingleCardLookupService` | `Successful` |
| `DeckFlow.Web.Tests/DeckControllerTests.cs:987` | `SuccessfulMechanicLookupService` | `Successful` |
| `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs:116` | `FailingRecentDecksImporter` | `Failing` |

**Additional note:** `DeckFlow.Web.Tests/DeckControllerTests.cs:810` has `FakeMetaGapService`
which is a canonical prefix BUT carries canned-response semantics (returns a hardcoded
`MetaGapResult` — see body). Per CONVENTIONS.md, canned-response = Stub, not Fake. This
mis-prefixed double requires a preliminary rename (see 14-AUDIT-REPORT.md `### Name-collision notes`).
This brings the total rename count to **9** (8 non-canonical + 1 mis-prefixed).

**Allowlist:** `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` — NOT a test double;
legitimate test-only factory pattern. Do NOT rename.

---

## Test discovery baseline

Command (best-effort per CLAUDE.md VSTest WSL constraint):

    timeout 90 "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --no-build --configuration Release --list-tests 2>&1 | grep -cE "^    [A-Z]"

**Result:** BASELINE_TEST_COUNT = **487** (discovery succeeded; no timeout)

Plan 14-04 re-runs this command after all changes and confirms count is ≥ 487.
If WSL hangs: document timeout and fall back to Render auto-deploy push-and-watch CI
on branch `v1.3` per RESEARCH.md "Test Discovery in WSL" section.
