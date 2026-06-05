---
phase: 29
slug: core-xml-doc-backfill-gate-widen
status: verified
threats_open: 0
threats_closed: 20
asvs_level: 1
created: 2026-06-05
---

# Phase 29 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| developer edit -> source tree | Mass `///` insertions across SQL-bearing files risk accidental code or literal mutation | Source code diff; SQL raw-string content; C# init-accessor syntax |
| temp probe file -> repo | A lingering `DeckFlow.Core/.editorconfig` would permanently alter solution-wide Roslyn warning severity; `__TempUndocProbe.cs` would ship a junk public type into DeckFlow.Core | Warning-severity configuration; public API surface |
| concurrent Wave-1 subagents -> shared worktree | Two parallel agents creating/deleting `DeckFlow.Core/.editorconfig` simultaneously corrupts the other plan's probe result | Build warning inventory; probe file contents |
| developer edit -> build config | `.editorconfig` path glob change alters gated diagnostic scope solution-wide; an over-broad glob could mis-gate the test project | Warning-severity scope for DeckFlow.Core vs DeckFlow.Core.Tests |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-29-01-01 | Tampering | Raw-string SQL in Postgres/Sqlite dialect files | mitigate | `///` lines added above declarations only; `git diff 0b129f5..6e132ce -- DeckFlow.Core/` shows 0 removed lines and 0 non-doc non-blank added lines; triple-quote delimiters confirmed present at same positions | closed |
| T-29-01-02 | Tampering | Temp probe `DeckFlow.Core/.editorconfig` | mitigate | Atomic mkdir-lock acquire/release with EXIT/INT/TERM trap per plan; `DeckFlow.Core/.editorconfig` confirmed ABSENT from filesystem and from `git status` | closed |
| T-29-01-03 | Tampering | Concurrent sibling probe collision | mitigate | Atomic `mkdir DeckFlow.Core/.editorconfig.probe.lock` mutex in every Wave-1 plan; 120s cap with stale recovery; folder-scoped warning filter per plan; `DeckFlow.Core/.editorconfig.probe.lock` confirmed ABSENT | closed |
| T-29-01-04 | Denial of Service | Build pipeline | accept | Doc comments are compile-stripped; zero runtime/behavior change possible — see Accepted Risks Log AR-29-DoS | closed |
| T-29-01-SC | Tampering | npm/pip/cargo installs | n/a | No package installs in this phase; no supply-chain surface | closed |
| T-29-02-01 | Tampering | Raw-string instruction constants in ReconciliationReporter.cs | mitigate | `///` lines added above `public const string` declarations only; git diff for range `0b129f5..6e132ce` shows 0 removed lines; ReconciliationReporter.cs contains 4 triple-quote delimiters at positions 13/45/49/62 — all confirmed intact by filesystem read | closed |
| T-29-02-02 | Tampering | Temp probe `DeckFlow.Core/.editorconfig` | mitigate | Same atomic mkdir-lock + trap protocol as T-29-01-02; confirmed absent at phase close | closed |
| T-29-02-03 | Tampering | Concurrent sibling probe collision | mitigate | Same mutex protocol as T-29-01-03; folder-scoped filter to `Reporting/|Filtering/` | closed |
| T-29-02-04 | Denial of Service | Build pipeline | accept | See AR-29-DoS | closed |
| T-29-02-SC | Tampering | npm/pip/cargo installs | n/a | No package installs | closed |
| T-29-03-01 | Tampering | 50 SQL raw strings in CategoryKnowledgeRepository.cs | mitigate | `///` lines added only above member declarations; `grep -c '"""' CategoryKnowledgeRepository.cs` = 50 (100 delimiters for 50 strings), confirming no string was split or merged; full diff range shows 0 removed lines and 0 non-doc added lines | closed |
| T-29-03-02 | Tampering | CS1573 param-set completions positional order | mitigate | All 5 missing params inserted (`boardFilter` line 242, `board` lines 417/518, `deckCount` line 418, `deckCountIncrement` line 519); positional order verified by line-number distribution across correct method boundaries; probe reported 0 CS1573 for Knowledge/ | closed |
| T-29-03-03 | Tampering | Temp probe `DeckFlow.Core/.editorconfig` | mitigate | Same atomic lock protocol; confirmed absent | closed |
| T-29-03-04 | Tampering | Concurrent sibling probe collision | mitigate | Same mutex protocol; folder-scoped filter to `Knowledge/` | closed |
| T-29-03-05 | Denial of Service | Build pipeline | accept | See AR-29-DoS | closed |
| T-29-03-SC | Tampering | npm/pip/cargo installs | n/a | No package installs | closed |
| T-29-04-01 | Tampering | 17-file mass doc sweep | mitigate | Path-scoped `git diff --stat` over six folders per task; full range diff 0 removed lines, 0 non-doc non-blank added lines; `executeAsync` param at MoxfieldApiDeckImporter.cs:21 confirmed; enum summaries at PrintingChoice.cs:3/8/10/12 confirmed | closed |
| T-29-04-02 | Tampering | MoxfieldApiDeckImporter CS1573 + enum member docs | mitigate | `grep -c 'param name="executeAsync"'` = 1 (line 21); `grep -c "/// <summary>" PrintingChoice.cs` = 4 (type + 3 members); probe over six folders reported 0 CS1573/CS1591/CS1587 | closed |
| T-29-04-03 | Tampering | Temp probe `DeckFlow.Core/.editorconfig` | mitigate | Same atomic lock protocol; confirmed absent | closed |
| T-29-04-04 | Tampering | Concurrent sibling probe collision | mitigate | Same mutex protocol; folder-scoped filter to `Integration/|Exporting/|Parsing/|Models/|Normalization/|Diffing/` | closed |
| T-29-04-05 | Denial of Service | Build pipeline | accept | See AR-29-DoS | closed |
| T-29-04-SC | Tampering | npm/pip/cargo installs | n/a | No package installs | closed |
| T-29-05-01 | Tampering | `.editorconfig` Do-Not-Modify file | mitigate | Blocking human-verify checkpoint approved by user 2026-06-05; `git diff 6e132ce^..6e132ce -- .editorconfig` shows 6 inserted lines only (no removed or changed lines); existing `[DeckFlow.Web/**.cs]` section (lines 111-115) and global `[*.cs]` suppressor (lines 96-98) byte-identical to before | closed |
| T-29-05-02 | Tampering | `[DeckFlow.Core/**.cs]` path glob mis-matching DeckFlow.Core.Tests | mitigate | Full-solution `DeckFlow.sln -c Release` log shows 0 warnings and 0 errors; no `DeckFlow.Core.Tests` CS1591/1573/1587 lines; glob anchored to `.editorconfig` directory — literal prefix `DeckFlow.Core/` does not match sibling top-level dir `DeckFlow.Core.Tests/` | closed |
| T-29-05-03 | Tampering | Temp `__TempUndocProbe.cs` | mitigate | `DeckFlow.Core/__TempUndocProbe.cs` confirmed ABSENT from filesystem; absent from `git status`; no commit in range `0b129f5..HEAD` touches that path | closed |
| T-29-05-04 | Spoofing | False-green gate (suppressor still active) | mitigate | Inject-probe `__TempUndocProbe` fired 4x CS1591 (type + member) before removal — proves gate is real; build run with `-warnaserror:CS1591,CS1573,CS1587` (all three codes); full-log grep (not tail) found zero surviving matches; two surviving Instance singletons caught by gate and fixed in commit `1222476` before final commit `6e132ce` | closed |
| T-29-05-05 | Denial of Service | Build pipeline | accept | See AR-29-DoS | closed |
| T-29-05-SC | Tampering | npm/pip/cargo installs | n/a | No package installs | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · n/a (not applicable)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-29-DoS | T-29-01-04, T-29-02-04, T-29-03-05, T-29-04-05, T-29-05-05 | XML doc comments are stripped by the C# compiler and produce no executable code. The `.editorconfig` gate widen only elevates diagnostic severity; it has no runtime effect. Neither change alters any data path, HTTP call, database query, or memory allocation. Build pipeline denial-of-service from a doc-only phase is not a credible threat. | user (project owner) | 2026-06-05 |
| AR-29-StaleProbe | residual | Wave-1 plans share an `mkdir`-based mutex with a 120-second timeout and stale-lock recovery. A theoretical race remains if a lock dir is abandoned by a crashed agent for less than 5 minutes AND a sibling agent enters the stale-recovery check within that window — both could attempt `mkdir` in close succession. In practice: (a) each plan's locked section completes in seconds; (b) all four Wave-1 plans completed successfully with no stale-lock events reported; (c) this risk only affects per-plan probe accuracy, never the final gate build, which uses no mutex. Residual probability is negligible; accepted as LOW. | user (project owner, via plan approval) | 2026-06-05 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Threat Flags

No SUMMARY.md `## Threat Flags` sections declared new unregistered attack surface. The one implementation deviation (two `Instance` singleton survivors caught by the 29-05 gate and fixed in commit `1222476`) was within the declared scope of T-29-05-04 (false-green gate spoofing mitigation) and does not constitute new attack surface.

Review finding IN-02 (code review noted that the gate covers the full project, not just the 30 reviewed files) was pre-empted by the gate itself and resolved before the final commit. Not a new threat flag — maps to T-29-05-04 CLOSED.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-05 | 30 | 30 | 0 | gsd-security-auditor (Claude Sonnet 4.6) |

*Threat count includes 5x n/a (supply chain) entries; all dispositions verified.*

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / n/a)
- [x] Accepted risks documented in Accepted Risks Log (AR-29-DoS, AR-29-StaleProbe)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-05
