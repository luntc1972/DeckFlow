---
phase: 28
slug: housekeeping-bundle
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-04
audit_date: 2026-06-04
block_on: critical
threats_total: 13
threats_closed: 13
---

# Phase 28 Security Audit Report

**Phase:** 28 — Housekeeping Bundle
**Audited:** 2026-06-04
**Auditor:** gsd-security-auditor (Claude Sonnet 4.6)
**ASVS Level:** 1
**Block-on:** critical

---

## Summary

All 13 threats in the phase 28 threat register are CLOSED. No blockers. No unregistered flags. Phase may ship.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| planning-doc author → repo | Markdown docs only (28-01, 28-02 retro docs); no executable surface | verification verdicts, status corrections |
| CLI invocation env (MTG_DATA_DIR) → artifact write path | operator-controlled env var selects artifact output directory (28-02) | filesystem path for content-kb artifact root |
| untrusted transcript (prompt-injected) → codex exec agent | core 28-03 threat: malicious transcript could instruct codex to read+echo local files | YouTube transcript text → LLM CLI |
| codex CLI → local filesystem | codex exec is an agent; --sandbox read-only blocks writes but NOT reads | local file contents (potential exfil channel) |
| operator env (DECKFLOW_LLM_PROVIDER, model, CLI override) → process spawn | env selects provider, model, override command (28-04, skipped) | process launch spec |

---

## Threat Verification

### Mitigate-disposition threats

| Threat ID | Category | Component | Disposition | Verdict | Evidence |
|-----------|----------|-----------|-------------|---------|----------|
| T-28-01 | Tampering | retro VERIFICATION docs misstate verdicts | mitigate | CLOSED | `retroactive: true` present in all 7 retro VERIFICATION files (grep confirmed). `evidence_source:` YAML list citing `v1.4-MILESTONE-AUDIT.md` present in all 7 files. Sample spot-checks: `16-VERIFICATION.md:7,8,11`, `17-VERIFICATION.md:7,8,12`, `21.2-VERIFICATION.md:7,8,11`, `23-VERIFICATION.md:7,8,14`. Every SC verdict cell in the body cites a concrete source file or audit row (non-empty Evidence columns confirmed in all sampled files). |
| T-28-02 | Repudiation | corrected status labels lack provenance | mitigate | CLOSED | Dated correction note found verbatim in both Phase 20 files. `20-VERIFICATION.md:188`: "corrected 2026-06-04 (HSK-03, D-10) — UAT PASSED 2026-05-27 per `.planning/milestones/v1.4-MILESTONE-AUDIT.md` (5-channel harvest, 10/10 captions)". Same note at `20-HUMAN-UAT.md:35`. Both files' frontmatter `status:` confirmed as `passed` (not `human_needed` or `partial`). |
| T-28-03 | Tampering | artifact root resolves outside intended tree | mitigate | CLOSED | `DeckFlow.CLI/CommandRunners.cs:1664-1665`: `ResolveContentKbArtifactRoot` unset branch returns `Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"))`. No user-string concatenation; MTG_DATA_DIR-set branch at line 1661 (`Path.GetFullPath(Path.Combine(dataDir, "content-kb"))`) is unchanged. Why-comment at line 1664 cites D-11/HSK-04. |
| T-28-04 | Repudiation | retro P24/P26 docs misstate shipped state | mitigate | CLOSED | `24-VERIFICATION.md:7-9` has `evidence_source:` list citing `v1.4-MILESTONE-AUDIT.md` and `24-UAT.md`. SC table evidence cells cite live smoke PASSED 2026-05-25, milestone audit wired-filter evidence, and ROADMAP. `26-02-SUMMARY.md:5,10` has `retroactive: true` and `evidence:` list including `b1a5cc8`; body explicitly documents SC2 amendment to ~38% achieved scope vs. original >=50% target (non-invented verdict). |
| T-28-05 | Information Disclosure | codex reads arbitrary local files under prompt injection | mitigate | CLOSED | D-03 re-demote path triggered. `28-DISCOVERY.md` Decision section states "RE-DEMOTE HSK-02 to backlog per D-03." All three candidate mechanisms investigated with documented/structural evidence (binary-embedded text for Candidate A; structural strings analysis for Candidate B; exhaustive help text for Candidate C). No codex implementation code shipped — `LlmDistillationProviderFactory.cs:49-52` still throws `NotSupportedException` with Phase 21.3 deferral message. `28-04-SUMMARY.md` `status: skipped` with `skip_reason` citing D-03. |
| T-28-06 | Elevation of Privilege | codex agent tool use beyond distillation | mitigate | CLOSED | Same re-demote verification as T-28-05. No codex execution path exists in the codebase. `CliLlmDistillationService.cs` contains no "codex" or "Codex" identifiers beyond what pre-existed (grep returns 0 matches for codex in that file). The agent tool-use threat is moot because no codex backend shipped. |
| T-28-07 | Information Disclosure | codex reads/echoes local files (28-04 impl) | mitigate | CLOSED (MOOT) | 28-04 confirmed skipped. No sentinel-exfil test for codex was added: `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` contains a `sentinel` const at line 125 but it belongs to a pre-existing claude error-sanitization test (line 128-130 use `ClaudeEnvelope`, not codex). No `Codex_MaliciousTranscript` or codex-specific sentinel test exists. No codex envelope code exists. Skip record confirmed in `28-04-SUMMARY.md`. |
| T-28-08 | Tampering | codex output bypasses JSON-repair/ValidateTags (28-04) | mitigate | CLOSED (MOOT) | 28-04 skipped; no codex output path exists. `ExtractWithRetryAsync` (shared path) is untouched. JSON-repair and `ValidateTags` remain on the claude path only. Skip confirmed. |
| T-28-09 | Spoofing | malformed override command-spec (28-04) | mitigate | CLOSED | `BuildOverrideCommandSpec` fail-fast JSON-array validation is present and unchanged: `CliLlmDistillationService.cs:131-174` enforces JSON deserialization (lines 136-144), null-element check (151-154), empty-executable check (156-159), and exactly-one-`{instruction}`-placeholder check (161-170). The method returns `CliEnvelopeKind.ClaudeJson` at line 174, unchanged from pre-28-04 state. No codex override path was added. |

### Accept-disposition threats

For accept-disposition threats, the rationale is verified against actual git changes in the phase 28 commit range (`069c34e..5314456`). A `git diff` of all `.csproj`, `package.json`, and `package-lock.json` files produced no output — confirming zero dependency changes.

| Threat ID | Category | Component | Disposition | Verdict | Evidence |
|-----------|----------|-----------|-------------|---------|----------|
| T-28-SC-01 | Tampering | package installs (28-01) | accept | CLOSED | No `.csproj` or `package.json` changes in phase 28 commits (git diff confirmed empty for dependency manifests). Plan 28-01 touches only Markdown docs and planning files — no executable surface. |
| T-28-SC-02 | Tampering | package installs (28-02) | accept | CLOSED | Single BCL-only code change (`Directory.GetCurrentDirectory()` + `Path.GetFullPath`) in `CommandRunners.cs`; no new packages. Git diff on manifests empty. |
| T-28-SC-03 | Tampering | package installs (28-03) | accept | CLOSED | Discovery-only plan; creates only `28-DISCOVERY.md` and `28-03-SUMMARY.md`. Codex CLI is operator-provided host tooling, not a project dependency. No manifest changes. |
| T-28-SC-04 | Tampering | package installs (28-04) | accept | CLOSED | Plan skipped; no code written. No manifest changes. |

---

## Unregistered Flags

None. No `## Threat Flags` sections exist in any phase 28 SUMMARY file (`28-01-SUMMARY.md`, `28-02-SUMMARY.md`, `28-03-SUMMARY.md`). No new attack surface appeared during implementation that lacks a threat mapping.

---

## Accepted Risks Log

| Threat ID | Risk | Rationale | Accepted By |
|-----------|------|-----------|-------------|
| T-28-SC-01 | No new package installs | Docs-only plan; no executable surface introduced | Phase plan (28-01-PLAN.md threat_model) |
| T-28-SC-02 | No new package installs | BCL-only code change (`System.IO` + `System.Environment`); no dependency surface | Phase plan (28-02-PLAN.md threat_model) |
| T-28-SC-03 | No new package installs | Discovery/docs only; codex CLI is operator-provided | Phase plan (28-03-PLAN.md threat_model) |
| T-28-SC-04 | No new package installs | Plan skipped; no code written | Phase plan (28-04-PLAN.md threat_model) |

---

## Open Threats

None.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-04 | 13 | 13 | 0 | gsd-security-auditor (Claude Sonnet) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

---

## Audit Notes

- The T-28-05/T-28-06 re-demote path is the most significant security outcome of this phase. The ADVERSARIAL stance was applied: verification started from the hypothesis that the NotSupportedException stub had been replaced. Grep of `LlmDistillationProviderFactory.cs` lines 49-52 confirmed the stub is intact, and `28-04-SUMMARY.md` `status: skipped` with explicit `provides: "Skip record only — NO code was written"` corroborates the skip claim.

- T-28-07's sentinel-exfil test check required distinguishing the pre-existing claude sentinel test (lines 125-137 of `CliLlmDistillationServiceTests.cs`, which uses `ClaudeEnvelope`) from any hypothetical codex sentinel test. The existing sentinel is a claude error-sanitization regression, not a codex read-isolation test. No codex-specific sentinel-exfil test was added — consistent with 28-04 being skipped.

- T-28-09 verification confirms `BuildOverrideCommandSpec` at line 174 still returns `CliEnvelopeKind.ClaudeJson` exclusively, with no codex `CliEnvelopeKind.Raw` branch added. The fail-fast validation chain (lines 136-170) is intact.
