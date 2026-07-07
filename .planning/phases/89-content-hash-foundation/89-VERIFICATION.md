---
phase: 89-content-hash-foundation
verified: 2026-07-07T18:31:56Z
status: gaps_found
score: 8/9 truths verified
overrides_applied: 0
gaps:
  - truth: "The web app refuses to render a row whose on-disk body hash does not match its stored body_sha256, logging the mismatch instead of serving stale/corrupt content. (ROADMAP Phase 89 Success Criterion 3 / SYNC-03 literal text)"
    status: partial
    reason: >
      Code review of DeckFlow.Web/Controllers/ContentKbController.cs:118-136 confirms the render
      guard computes ComputeBodySha256(raw) and compares against row.BodySha256, and DOES emit a
      structured LogWarning on mismatch or null-hash — but it does NOT refuse to render. The body
      is served unconditionally afterward (`Markdown.ToHtml(body, Pipeline)` runs regardless of the
      comparison result). This is fail-open, not the fail-closed "refuses to render" the ROADMAP.md
      Phase 89 Success Criterion 3 and REQUIREMENTS.md SYNC-03 text literally state. The phase's own
      89-CONTEXT.md D-05 explicitly narrows this to "Fail-open + log on BOTH mismatch and
      missing-hash, this phase" and states a future phase may tighten to fail-closed once the D-08
      backfill guarantees coverage — so this looks like a deliberate, discussed, and documented
      scope narrowing rather than an oversight. However neither ROADMAP.md's Success Criterion 3 nor
      REQUIREMENTS.md's SYNC-03 bullet text was updated to reflect the softened "refuses to render"
      -> "logs but still serves" wording, so the written contract and the shipped behavior disagree.
    artifacts:
      - path: "DeckFlow.Web/Controllers/ContentKbController.cs"
        issue: "Lines 126-136: computes mismatch, logs warning, but always proceeds to render (fail-open) rather than refusing (fail-closed) as ROADMAP/REQUIREMENTS literally state."
    missing:
      - "Either (a) update ROADMAP.md Phase 89 Success Criterion 3 and REQUIREMENTS.md SYNC-03 wording to say 'logs the mismatch, fail-open this phase' matching D-05's documented scope, or (b) add an explicit override entry to this VERIFICATION.md accepting the D-05 deviation, signed by the developer."
---

# Phase 89: Content-Hash Foundation Verification Report

**Phase Goal:** Every row's body content is hashed end-to-end on one unified signature, so drift is a single indexed comparison and body corruption (e.g. mojibake) is detectable instead of silently served.
**Verified:** 2026-07-07T18:31:56Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `content_site_index` carries a `body_sha256` column (SQLite + Postgres + seed JSON) computed from the on-disk `.md` body at publish time. (SYNC-01) | ✓ VERIFIED | `ContentSiteIndexStore.cs`: dialect-guarded idempotent `ALTER TABLE ... ADD COLUMN body_sha256 TEXT NULL` (line 132-137) + both CREATE TABLE strings (lines 1137, 1161) + all 6 SELECT lists (lines 248, 283, 319, 353, 386, 422) + upsert binding (line 832) + all 3 upsert-variant SQL bodies. `ContentArtifactSpec.cs:155` adds nullable `BodySha256` property. `ContentKbOrchestrator.cs:1357` computes it via the shared helper at publish time and passes it into `UpsertContentColumnsOnlyAsync`. `ContentIndexExportRow.cs` + `ContentKbSeedLoader.cs` round-trip the field through JSON (nullable, camelCase `bodySha256`). |
| 2 | DirectPush, Pull, and reconcile all compare rows using one unified body-inclusive signature — the two previously divergent schemes are gone. (SYNC-02) | ✓ VERIFIED | `grep -rn "Fingerprint"` across all `.cs`/`.razor`/`.cshtml` in production code returns zero hits (only test files reference the deleted name, as regression documentation). `BuildSignature` is defined in exactly one file (`ContentSiteIndexContentSignature.cs`) — enforced by a live regression test `OneSignatureSurfaceGuardTests.BuildSignature_IsDefinedInExactlyOneFile` which scans the source tree, not just the compiled assembly. `DirectPushCoordinator.cs:166` calls `AreContentEqual` directly; `PullFromProdCoordinator.cs:119` calls `ContentSyncDiffClassifier.Classify` which internally calls `AreContentEqual`/`BuildSignature` (`ContentSyncDiffClassifier.cs:63`) — all three consumers share the one signature surface. `BuildSignature` includes `body_sha256` as its final field (line 105-107) with a non-colliding `(nohash)` sentinel for null/legacy rows. |
| 3 | One shared method computes `body_sha256` from a raw artifact's post-`SplitHeader` body (UTF-8 decode + LF normalize + SHA-256), used identically by publish-compute and render-guard. (D-01/D-02) | ✓ VERIFIED | `ContentSiteIndexContentSignature.ComputeBodySha256` (lines 131-145) is the only definition; it calls `ContentArtifactParser.SplitHeader`, normalizes `\r\n`/`\r` to `\n`, UTF-8-encodes, SHA-256-hashes, returns lowercase hex. Exactly 3 call sites in production code: `ContentBodyHashBackfill.cs:77` (backfill), `ContentKbOrchestrator.cs:1357` (publish), `ContentKbController.cs:126` (render guard) — no fourth, hand-rolled hash path exists anywhere in the codebase (other `SHA256` usages — `DeckCategoryCacheWriter.cs`, `IpHasher.cs`, `PacketSessionCache.cs` — are unrelated features, not body-hash). |
| 4 | The F-51-PG-01 timestamp-direction (`ProdNewer`/`Diverged`) branches in the classifier are preserved, with body hash only as the equal-timestamp tie-breaker. (D-04) | ✓ VERIFIED | `ContentSyncDiffClassifier.cs:55-69`: the `prodUtc > localUtc` / `localUtc > prodUtc` UTC-compared branches are unchanged; only the equal-timestamp `else if` branch (line 63) now calls `ContentSiteIndexContentSignature.AreContentEqual` instead of the deleted `Fingerprint` compare — exactly matching D-04's stated scope. |
| 5 | The web detail-render guard logs a structured warning on hash mismatch or missing hash. (SYNC-03 — logging half) | ✓ VERIFIED | `ContentKbController.cs:126-134`: computes `computedHash`, compares to `row.BodySha256`, and on `null` or inequality emits `_logger.LogWarning("Content KB body hash mismatch for row {ContentKbRowId}: stored={StoredHash} computed={ComputedHash}", ...)`. List/browse pages correctly untouched (guard scoped to detail render per D-07 — they don't read the `.md` body). |
| 6 | The web detail-render guard **refuses to render** a mismatched/un-hashed row. (SYNC-03 — refusal half, ROADMAP Phase 89 SC #3 literal text) | ✗ FAILED (see gap) | `ContentKbController.cs:136`: `Markdown.ToHtml(body, Pipeline)` executes unconditionally after the warning is logged — the row is always served. This is intentional fail-open per 89-CONTEXT.md D-05, but the literal ROADMAP.md/REQUIREMENTS.md wording ("refuses to render") does not match the shipped behavior and was not updated to reflect the narrowed scope. See Gaps Summary. |
| 7 | Backfill is a one-time deterministic pass (not lazy), runs on both hosts, is idempotent (null-only), and is host-agnostic (no ASP.NET/HttpClient dependency in Core). (D-08) | ✓ VERIFIED | `ContentBodyHashBackfill.cs` depends only on `Microsoft.Extensions.Logging`, `IContentSiteIndexStore`, `IContentArtifactBodyResolver` (both Core abstractions) — no AspNetCore/HttpClient references. Writes flow only through `SetBodySha256IfNullAsync` (a null-only guarded UPDATE, `WHERE ... AND body_sha256 IS NULL` at `ContentSiteIndexStore.cs:477-479`); rows with a non-null hash are never read via the resolver (`ContentBodyHashBackfill.cs:59-63`). Wired at `DeckFlow.Web/Program.cs:274` (after `EnsureSchemaAsync` line 266 + `LoadIfPresentAsync` line 267) and at `DeckFlow.Studio/Program.cs:208-210` bound explicitly to the local `content-kb.db` `IContentSiteIndexStore` singleton (not a `ProdStoreFactory` prod store — confirmed by source review and inline comment at Studio Program.cs:202-207). |
| 8 | `SetBodySha256IfNullAsync` is a throwing default interface method mirroring `DeleteAllRowsAsync`, so existing test-double implementers compile unchanged. (D-09/mirror pattern) | ✓ VERIFIED | `IContentSiteIndexStore.cs:239-240`: `Task<int> SetBodySha256IfNullAsync(...) => throw new NotSupportedException(...)` — same throwing-default-interface-method idiom as `DeleteAllRowsAsync` (line 105-106) in the same file. Real implementation on `ContentSiteIndexStore` overrides it (line 468). |
| 9 | LF and CRLF variants of the same body hash identically; content divergence (mojibake class) hashes differently. (D-02) | ✓ VERIFIED | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ContentSiteIndexContentSignatureTests"` passes as part of the full 1136/1136 Core suite; test file authored per plan (Tests A–E: LF/CRLF parity, mojibake divergence, empty body, split parity, signature-includes-hash). |

**Score:** 8/9 truths verified (1 FAILED against literal ROADMAP/REQUIREMENTS wording; the underlying deviation is documented and appears intentional — see Gaps Summary for the suggested resolution).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` | `ComputeBodySha256` + body_sha256-inclusive `BuildSignature` | ✓ VERIFIED | Present, substantive, wired (3 call sites), tested |
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` | `ContentSiteIndexRow.BodySha256` nullable property | ✓ VERIFIED | Line 155, `{ get; init; }` preserved (carve-out honored) |
| `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` | `Fingerprint` deleted, unified signature used | ✓ VERIFIED | No `Fingerprint` definition anywhere; `AreContentEqual` called at line 63 |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | `body_sha256` column DDL/model/upsert plumbing | ✓ VERIFIED | ALTER + 2 CREATE TABLE + 6 SELECT + 3 upsert variants + `SetBodySha256IfNullAsync` |
| `DeckFlow.Core/Content/ContentBodyHashBackfill.cs` | Host-agnostic one-time backfill service | ✓ VERIFIED | No AspNetCore/HttpClient deps; null-only write path |
| `DeckFlow.Core/Content/IContentArtifactBodyResolver.cs` | Resolver seam interface | ✓ VERIFIED | One method, `TryReadArtifactTextAsync` |
| `DeckFlow.Web/Services/Content/ContentKbArtifactBodyResolver.cs` | Web adapter | ✓ VERIFIED | Wraps `ContentKbArtifactPathResolver`, never throws |
| `DeckFlow.Studio/Services/StudioContentArtifactBodyResolver.cs` | Studio adapter | ✓ VERIFIED | Containment-guarded read mirroring `ReviewCoordinator.ReadRelativeSafe` |
| `DeckFlow.Web/Controllers/ContentKbController.cs` | Render guard (D-05/D-07) | ⚠️ PARTIAL | Logs correctly; does not refuse-to-render (see Gap) |
| `content-kb/seed/index-seed.json` | `body_sha256` field populated | ℹ️ INFO (expected empty this phase) | Schema/loader supports the field (nullable round-trip verified by golden test), but the checked-in seed file has zero `body_sha256` values today. Per 89-CONTEXT.md phase boundary, the actual re-export of a hash-populated seed is explicitly deferred to Phase 90 ("DirectPush git-body serving flip + seed re-export (P90)") — not a Phase 89 gap. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `ContentSiteIndexContentSignature.ComputeBodySha256` | `ContentArtifactParser.SplitHeader` | direct call | ✓ WIRED | Line 135 |
| `ContentKbOrchestrator` (publish) | `ComputeBodySha256` | direct call | ✓ WIRED | Line 1357, result flows into `UpsertContentColumnsOnlyAsync` |
| `ContentKbController` (render) | `ComputeBodySha256` | direct call | ✓ WIRED | Line 126, but result only feeds a log statement, not a rendering decision |
| `ContentBodyHashBackfill` | `IContentSiteIndexStore.SetBodySha256IfNullAsync` | direct call | ✓ WIRED | Line 78, null-only guarded write |
| `DeckFlow.Web/Program.cs` startup | `ContentBodyHashBackfill.RunAsync` | DI resolve + await, after EnsureSchemaAsync + LoadIfPresentAsync | ✓ WIRED | Lines 266-267, 274 |
| `DeckFlow.Studio/Program.cs` startup | `ContentBodyHashBackfill.RunAsync` | DI resolve + await, bound to local store | ✓ WIRED | Lines 208-210 |
| `PullFromProdCoordinator` | `ContentSyncDiffClassifier.Classify` → `AreContentEqual` | direct call | ✓ WIRED | Line 119 → internally line 63 |
| `DirectPushCoordinator` | `ContentSiteIndexContentSignature.AreContentEqual` | direct call | ✓ WIRED | Line 166 |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds clean | `dotnet build DeckFlow.sln -c Debug` | 0 Warnings, 0 Errors, 6 projects built | ✓ PASS |
| DeckFlow.Core.Tests full suite | `dotnet test DeckFlow.Core.Tests --no-build` | 1136/1136 passed | ✓ PASS |
| DeckFlow.Web.Tests full suite | `dotnet test DeckFlow.Web.Tests --no-build` | 1226 passed, 12 skipped (PG-only, expected — no local Postgres), 0 failed | ✓ PASS |
| DeckFlow.Studio.Tests full suite | `dotnet test DeckFlow.Studio.Tests --no-build` | 296/296 passed | ✓ PASS |
| No stray `Fingerprint` in production code | `grep -rn "Fingerprint" --include="*.cs" --include="*.razor" --include="*.cshtml" .` (excluding Tests dirs) | zero hits | ✓ PASS |
| No second hand-rolled body-hash path | `grep -rn "ComputeBodySha256"` production code | exactly 3 call sites (backfill, publish, render-guard) | ✓ PASS |

All test counts independently re-run by the verifier and matched the SUMMARY.md claims exactly.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SYNC-01 | 89-01, 89-02, 89-04, 89-05, 89-06 | `body_sha256` column end-to-end (SQLite + Postgres + seed JSON), computed at publish | ✓ SATISFIED | See Truths 1, 3, 7 |
| SYNC-02 | 89-01, 89-03 | One unified body-inclusive signature replaces the two divergent schemes | ✓ SATISFIED | See Truths 2, 4 |
| SYNC-03 | 89-05 | Web app refuses to render a row whose hash mismatches, logging the mismatch | ⚠️ PARTIAL | Logging half satisfied (Truth 5); refusal half NOT satisfied as literally worded (Truth 6) — matches 89-CONTEXT.md D-05's documented fail-open scope-narrowing, but REQUIREMENTS.md text was not updated to match |

No orphaned requirements found — SYNC-01/02/03 are the complete Phase 89 requirement set per REQUIREMENTS.md's traceability table, and all three were claimed by at least one plan's frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ContentSiteIndexStore.cs` | 734 | comment: "use the placeholders" | ℹ️ Info | Refers to legitimate error-path variable substitution, unrelated to Phase 89 scope; not a debt marker |
| `ContentArtifactSpec.cs` | 17-18 | `"XXXXXXXXXXX"` in xmldoc example | ℹ️ Info | Example placeholder text in a doc comment, not a code stub |

No `TODO`/`FIXME`/`TBD`/`HACK` markers found in any of the 26 files touched by Phase 89's six plans. No blocker-level anti-patterns.

### Human Verification Required

None. All Phase 89 truths are verifiable by static/behavioral checks against the codebase; no UI/visual/real-time behavior is in scope for this phase (the render-guard change is log-only, not user-visible).

## Gaps Summary

**One gap, likely intentional and already documented — needs a developer decision, not a code fix.**

ROADMAP.md's Phase 89 Success Criterion 3 and REQUIREMENTS.md's SYNC-03 bullet both use the words "refuses to render" for the detail-page hash-mismatch guard. The shipped code (`ContentKbController.cs:126-136`) logs a structured warning on mismatch/missing-hash but **always serves the body anyway** — this is fail-open, not fail-closed.

This divergence is not hidden: 89-CONTEXT.md's D-05 explicitly states the decision — "Fail-open + log on BOTH mismatch and missing-hash, this phase... Zero risk of live content vanishing during rollout... A future phase may tighten to fail-closed once backfill (D-06) guarantees every live row is hashed" — and the CONTEXT doc's `<specifics>` section notes "the user consciously ratified 'fail-open this phase, tighten later.'" All six plan SUMMARYs and the code match D-05 faithfully.

The gap is a **documentation drift**, not an implementation defect: ROADMAP.md and REQUIREMENTS.md were written/checked off using the pre-narrowing "refuses to render" language, but were never edited to say "logs the mismatch, serves fail-open this phase" once D-05 locked in the softer scope during CONTEXT gathering. Two ways to close this:

1. **Update ROADMAP.md Phase 89 Success Criterion 3 and REQUIREMENTS.md's SYNC-03 bullet** to match the D-05 fail-open wording (recommended — keeps the paper trail honest for Phase 90+ readers who will otherwise expect a hard refusal that doesn't exist yet).
2. **Add an override** to this VERIFICATION.md accepting the deviation as-is.

**This looks intentional.** To accept this deviation via override, add to VERIFICATION.md frontmatter:

```yaml
overrides:
  - must_have: "Web app refuses to render a row whose on-disk body hash does not match its stored body_sha256"
    reason: "D-05 (89-CONTEXT.md): fail-open + log this phase by design; fail-closed refusal deferred to a later phase once D-08 backfill guarantees full coverage. User ratified this two-step rollout during context gathering."
    accepted_by: "{your name}"
    accepted_at: "{current ISO timestamp}"
```

No other gaps found. All artifact, wiring, and data-flow checks for SYNC-01 and SYNC-02 pass without qualification. Test suite (1136 Core / 1226+12skip Web / 296 Studio) and clean 0-warning build were independently re-run and match SUMMARY.md's claims exactly.

---

_Verified: 2026-07-07T18:31:56Z_
_Verifier: Claude (gsd-verifier)_

## Gap Resolution (post-verification)

The single gap (SYNC-03 wording said "refuses to render" / fail-closed, but shipped
behavior is fail-open + log per ratified decision D-05) was closed via **option (a):
doc-wording alignment**. The code is correct — it implements D-05 exactly. Updated:

- `REQUIREMENTS.md` SYNC-03 → "detects and logs … fail-open + log this phase per D-05;
  fail-closed refuse-to-render deferred to a future phase once D-08 backfill guarantees
  coverage."
- `ROADMAP.md` Phase 89 Success Criterion 3 → same fail-open framing.

No code change. Written contract and shipped behavior now agree. Phase verdict: **PASS**.
