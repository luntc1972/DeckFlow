---
phase: 89-content-hash-foundation
plan: 01
subsystem: content-kb
tags: [content-hash, signature, sha256, sync]
dependency-graph:
  requires: []
  provides:
    - "ContentSiteIndexRow.BodySha256"
    - "ContentSiteIndexContentSignature.ComputeBodySha256"
    - "body_sha256-inclusive BuildSignature/AreContentEqual"
  affects:
    - "DeckFlow.Core/Content/ContentSyncDiffClassifier.cs (P89-02 consumer)"
    - "DeckFlow.Core/Content/ContentSiteIndexStore.cs (P89 schema plans)"
    - "DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs (auto body-hash-aware via AreContentEqual)"
tech-stack:
  added: []
  patterns:
    - "One shared normalize-then-hash helper (ComputeBodySha256) reused by publish-compute and render-guard, D-01/D-02"
    - "Null-sentinel-guarded signature field append, matching existing NullDateSentinel idiom"
key-files:
  created:
    - "DeckFlow.Core.Tests/Content/ContentSiteIndexContentSignatureTests.cs"
  modified:
    - "DeckFlow.Core/Knowledge/ContentArtifactSpec.cs"
    - "DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs"
decisions:
  - "D-01/D-02/D-03 honored exactly as specified in 89-CONTEXT.md — no deviations"
metrics:
  duration: "~25 minutes"
  completed: "2026-07-07"
---

# Phase 89 Plan 01: Content-Hash Foundation Summary

One shared `ComputeBodySha256` hash helper (UTF-8 decode + LF-normalize + SHA-256 over the post-`SplitHeader` body) plus a body-hash-inclusive `ContentSiteIndexContentSignature.BuildSignature`, establishing the single content-drift-detection surface every later Phase 89-93 plan builds on.

## What Was Built

- **`ContentSiteIndexRow.BodySha256`** (`DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`) — new nullable `{ get; init; }` property, placed next to `ApprovalStatus`. Nullable because legacy rows have no hash until the Phase 89 backfill (D-08, later plan). `{ get; init; }` preserved exactly per the System.Text.Json carve-out in CLAUDE.md.
- **`ContentSiteIndexContentSignature.ComputeBodySha256(string rawArtifactText)`** — the one hash surface: calls `ContentArtifactParser.SplitHeader` (never re-implements frontmatter stripping, D-01), normalizes `\r\n`→`\n` then bare `\r`→`\n` (D-02), UTF-8 encodes, `SHA256.HashData`, returns lowercase 64-char hex via `Convert.ToHexStringLower`.
- **`BuildSignature` extended** with one more delimited field appended after `card_category_tags`: `row.BodySha256 ?? NullShaSentinel` where `NullShaSentinel = "(nohash)"` — not a valid 64-hex string, so it can never collide with a real hash (T-89-02). `AreContentEqual` was left untouched (still a one-line delegate to `BuildSignature`) and is now body-hash-aware for free — this automatically upgrades `DirectPushCoordinator.ClassifyDiff` (no code change needed there, confirmed by the pattern map).
- **`DeckFlow.Core.Tests/Content/ContentSiteIndexContentSignatureTests.cs`** (new, 7 tests) — Tests A–E from the plan: LF/CRLF parity, mojibake/content divergence, empty-body stability (no throw, 64-char hex), split-parity (hash equals SHA-256 of `SplitHeader(...).Body` normalized independently), and two signature-inclusion tests (differing hash → not equal; equal hash + equal columns → equal) plus the null-sentinel non-collision test (T-89-02).

## TDD Gate Compliance

- RED: `f2e844f1` `test(89-01): add failing tests for ComputeBodySha256 + signature hash inclusion` — confirmed fail-fast via compile error (`CS0117: does not contain a definition for 'ComputeBodySha256'`), since the target method did not exist yet.
- GREEN: `2225dff6` `feat(89-01): add ComputeBodySha256 helper, extend BuildSignature with body_sha256` — all 7 new tests pass; full `DeckFlow.Core.Tests` suite 1116/1116 passing (was 1109 before this plan, +7 new tests, 0 regressions).
- REFACTOR: not needed — implementation matched the plan's structural template on the first pass; no refactor commit.

## Task Commits

| Task | Commit | Summary |
|------|--------|---------|
| 1 | `79683701` | `feat(89-01): add nullable BodySha256 to ContentSiteIndexRow` |
| 2 (RED) | `f2e844f1` | `test(89-01): add failing tests for ComputeBodySha256 + signature hash inclusion` |
| 2 (GREEN) | `2225dff6` | `feat(89-01): add ComputeBodySha256 helper, extend BuildSignature with body_sha256` |

## Verification

- `dotnet build DeckFlow.Core` — 0 warnings / 0 errors (both after Task 1 and after Task 2).
- `dotnet build DeckFlow.sln` — 0 warnings / 0 errors across all 6 projects.
- `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ContentSiteIndexContentSignatureTests"` — 7/7 passed.
- `dotnet test DeckFlow.Core.Tests` (full suite) — 1116/1116 passed, 0 failed, 0 skipped.
- `scripts/format-check-changed.sh staged` — clean, no changed-lines format violations.
- No new NuGet packages — `System.Security.Cryptography.SHA256` is BCL (T-89-SC honored).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Forward `<see cref>` reference caused a build warning before Task 2 landed**
- **Found during:** Task 1 verification (`dotnet build DeckFlow.Core` acceptance criterion requires 0 warnings)
- **Issue:** The Task 1 xmldoc for `BodySha256` initially used `<see cref="DeckFlow.Core.Content.ContentSiteIndexContentSignature.ComputeBodySha256"/>`, which does not exist until Task 2, producing `CS1574: XML comment has cref attribute ... that could not be resolved`.
- **Fix:** Changed to a plain `<c>ContentSiteIndexContentSignature.ComputeBodySha256</c>` text reference (non-resolving, no warning) so Task 1 satisfies its own acceptance criterion independently of Task 2's completion.
- **Files modified:** `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`
- **Commit:** `79683701`

**2. [Rule 2 - missing critical documentation] Class-level `<remarks>` doc did not mention `body_sha256`**
- **Found during:** Task 2, reviewing `ContentSiteIndexContentSignature`'s summary/remarks before extending `BuildSignature`
- **Issue:** The class summary explicitly enumerated the signed column set (source, title, ... card_category_tags) and the remarks explicitly listed excluded columns — neither mentioned the new `body_sha256` field being added, which would leave the doc inaccurate and out of step with the "one signature, one home" invariant this phase establishes (SYNC-02).
- **Fix:** Added `body_sha256` to the summary's column list and a new `<para>` in `<remarks>` explaining its role as the body-drift-detection field (D-03).
- **Files modified:** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs`
- **Commit:** `2225dff6`

None outside these two documentation/build-hygiene fixes — plan executed as written otherwise.

## Known Stubs

None. Both artifacts (`BodySha256` property, `ComputeBodySha256` + extended `BuildSignature`) are fully wired and exercised by tests; no placeholder/mock data paths introduced.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or trust-boundary schema changes. `ComputeBodySha256` is a pure in-memory transform over a `string` parameter already owned by the caller (matches the plan's `<threat_model>` T-89-01/T-89-02/T-89-SC dispositions exactly; no new surface beyond what the threat register already covers).

## Self-Check: PASSED

- FOUND: `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`
- FOUND: `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs`
- FOUND: `DeckFlow.Core.Tests/Content/ContentSiteIndexContentSignatureTests.cs`
- FOUND: commit `79683701`
- FOUND: commit `f2e844f1`
- FOUND: commit `2225dff6`
