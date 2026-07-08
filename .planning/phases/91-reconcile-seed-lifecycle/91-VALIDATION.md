---
phase: 91
slug: reconcile-seed-lifecycle
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 91 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`, `DeckFlow.Studio.Tests`); bUnit 2.7.2 available in Studio tests |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`; `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` |
| **Quick run command** | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Reconcile|FullyQualifiedName~SeedManaged|FullyQualifiedName~SeedIndexFileReader"` |
| **Full suite command** | `dotnet.exe test DeckFlow.sln --no-restore` |
| **Estimated runtime** | ~unknown, CI-authoritative; repo constraint: VSTest is unreliable in WSL, so run via Windows `dotnet.exe` from WSL or push-and-watch CI |

---

## Sampling Rate

- **After every task commit:** Run `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Reconcile|FullyQualifiedName~SeedManaged|FullyQualifiedName~SeedIndexFileReader"`
- **After every plan wave:** Run `dotnet.exe test DeckFlow.sln --no-restore`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~unknown, CI-authoritative

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 91-XX-01 (TBD at planning) | TBD | 0 | SYNC-17 / `seed_managed` additive DDL | D-01 | SQLite and Postgres dialect SQL adds `seed_managed` idempotently without rewriting existing rows; PG live assertion is integration/deferred-to-93 if no containerized suite hook lands | unit / integration | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedManagedSchema"` | ❌ W0 | ⬜ pending |
| 91-XX-02 (TBD at planning) | TBD | 0 | SYNC-17 / write-path stamping | D-01 | Prod-publish paths stamp `seed_managed=true`; local-distill path remains unmarked via a bound `@seedManaged` parameter, never a shared SQL literal | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedManagedWritePath"` | ❌ W0 | ⬜ pending |
| 91-XX-03 (TBD at planning) | TBD | 0 | SYNC-17 / D-02 backfill | D-02 | Legacy rows are classified by current seed membership: present -> true, absent -> false | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedManagedBackfill"` | ❌ W0 | ⬜ pending |
| 91-XX-04 (TBD at planning) | TBD | 0 | SYNC-17 / D-02 idempotency | D-02 | Backfill writes only null/unclassified rows and preserves already-classified true/false values on rerun | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedManagedBackfill"` | ❌ W0 | ⬜ pending |
| 91-XX-05 (TBD at planning) | TBD | 0 | SYNC-11 / published-orphan detection | D-07 | Visible prod row with no git body is emitted as `published-orphan` | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileClassifier"` | ❌ W0 | ⬜ pending |
| 91-XX-06 (TBD at planning) | TBD | 0 | SYNC-11 / file-orphan detection | D-07 | Git `.md` body with no prod row is emitted as `file-orphan` | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileClassifier"` | ❌ W0 | ⬜ pending |
| 91-XX-07 (TBD at planning) | TBD | 0 | SYNC-11 / seed-drift detection | D-07 | Prod row absent from `index-seed.json` is emitted as `seed-drift` | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileClassifier"` | ❌ W0 | ⬜ pending |
| 91-XX-08 (TBD at planning) | TBD | 0 | SYNC-11 / body-hash-mismatch detection | P89 hash reuse | Stored `body_sha256` differing from `ComputeBodySha256` over git body is emitted as `body-hash-mismatch`; no second hash path | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileClassifier"` | ❌ W0 | ⬜ pending |
| 91-XX-09 (TBD at planning) | TBD | 0 | SYNC-11 / deterministic IDs | D-05 | Same discrepancy class and natural key produce stable IDs across runs and input ordering | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileDiscrepancyId"` | ❌ W0 | ⬜ pending |
| 91-XX-10 (TBD at planning) | TBD | 0 | SYNC-11 / store idempotency | D-05 | Re-running the same dry-run upserts existing discrepancies and creates zero duplicate rows | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileStore"` | ❌ W0 | ⬜ pending |
| 91-XX-11 (TBD at planning) | TBD | 0 | SYNC-11 / resolution-by-absence | D-05 | A previously persisted discrepancy that is absent on rerun is marked resolved, not deleted or recreated | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileStore"` | ❌ W0 | ⬜ pending |
| 91-XX-12 (TBD at planning) | TBD | 0 | SYNC-11 / scoped partial runs | D-05 | Scope-tagged reruns resolve only in-scope discrepancies and never false-resolve out-of-scope rows | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileStore"` | ❌ W0 | ⬜ pending |
| 91-XX-13 (TBD at planning) | TBD | 0 | SYNC-12 / soft-hide only | D-03 | Apply sets `is_visible=false` and retains row, marker, `body_sha256`, and timestamps; no hard-delete path exists | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedRemoval"` | ❌ W0 | ⬜ pending |
| 91-XX-14 (TBD at planning) | TBD | 0 | SYNC-12 / seed-owned only | SYNC-17 invariant | Apply touches `seed_managed=true` rows only; prod-owned `seed_managed=false` rows are never hidden or deleted | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedRemoval"` | ❌ W0 | ⬜ pending |
| 91-XX-15 (TBD at planning) | TBD | 0 | SYNC-12 / logging | D-03 | Each soft-hide writes an audit/loggable discrepancy outcome with natural key and retained hash metadata | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileApply"` | ❌ W0 | ⬜ pending |
| 91-XX-16 (TBD at planning) | TBD | 0 | SYNC-12 / Postgres timestamptz | F-51-PG-01 | Soft-hide SQL preserves existing timestamptz handling on Postgres; PG-live check is integration/deferred-to-93 if not runnable in-suite | integration | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --no-restore --filter "FullyQualifiedName~Postgres"` | ❌ W0 | ⬜ pending |
| 91-XX-17 (TBD at planning) | TBD | 0 | SYNC-12 / two-step apply gate | D-08 | Apply requires a prior dry-run of the same discrepancy set and re-runs the diff before writing | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileCoordinator"` | ❌ W0 | ⬜ pending |
| 91-XX-18 (TBD at planning) | TBD | 0 | SYNC-12 / stale apply rejected | D-08 | Apply rejects stale dry-run state when the current seed-drift removal set differs from the reviewed-removal set | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileCoordinator"` | ❌ W0 | ⬜ pending |
| 91-XX-19 (TBD at planning) | TBD | 0 | SYNC-11 / dry-run always available | D-09 | Detection, dry-run, discrepancy persistence, and report generation run while `sync.reconcile` is OFF | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileCoordinator"` | ❌ W0 | ⬜ pending |
| 91-XX-20 (TBD at planning) | TBD | 0 | SYNC-12 / destructive apply flag | D-09 | `sync.reconcile` gates only soft-hide writes; OFF or indeterminate flag refuses Apply | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileCoordinator"` | ❌ W0 | ⬜ pending |
| 91-XX-21 (TBD at planning) | TBD | 0 | SYNC-12 / web-DB flag accessor | D-10 | Studio reads `sync.reconcile` through the P90 web-DB accessor, not local config | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ProdContentReader|FullyQualifiedName~ReconcileFlag"` | ❌ W0 | ⬜ pending |
| 91-XX-22 (TBD at planning) | TBD | 0 | SYNC-12 / seeded OFF flag | D-10 | `sync.reconcile` is registered in web feature flags and seeded OFF in the feature-flag store | unit | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --no-restore --filter "FullyQualifiedName~FeatureFlag"` | ❌ W0 | ⬜ pending |
| 91-XX-23 (revision - Codex HIGH) | 91-01 | 0 | SYNC-17 / seed availability distinction | T-91-03 | `SeedIndexFileReader.Read` distinguishes present-and-parsed (incl. valid EMPTY set) from absent/unreadable/parse-failed; SeedAvailable=false is never collapsed into an empty membership set | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedIndexFileReader"` | ❌ W0 | ⬜ pending |
| 91-XX-24 (revision - Codex HIGH) | 91-03 | 0 | SYNC-17 / backfill availability gate | T-91-07 | An unavailable/unreadable seed yields ZERO backfill writes (rows stay NULL, repairable later); a valid EMPTY seed still classifies null rows false | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SeedManagedBackfill"` | ❌ W0 | ⬜ pending |
| 91-XX-25 (revision - Codex LOW) | 91-04 | 0 | SYNC-11 / file-orphan identity | T-91-17 | file-orphan matched by ARTIFACT PATH (primary); TryDerive only as a trusted-metadata fallback; no front-matter/filename inference rescues a `.md` from file-orphan | unit | `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileClassifier"` | ❌ W0 | ⬜ pending |
| 91-XX-26 (revision - Codex MED) | 91-08 | 0 | SYNC-12 / removal-scoped Apply | T-91-24 | Reviewed set + Apply stale-check are scoped to seed-drift (removal) IDs only; a mixed-class dry-run (all four classes) still allows Apply of the seed-drift removals without false-rejecting as stale | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileCoordinator"` | ❌ W0 | ⬜ pending |
| 91-XX-27 (revision - Codex MED) | 91-05 | 0 | SYNC-11 / stored Kind exposed | D-05 | `GetOpenAsync` round-trips discrepancy Kind (mapped from the persisted kind column) so 91-08 can filter open discrepancies to the seed-drift removal class | unit | `dotnet.exe test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --no-restore --filter "FullyQualifiedName~ReconcileStore"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/Content/SeedManagedSchemaTests.cs` — SQLite/Postgres dialect SQL-shape and idempotent additive DDL coverage for SYNC-17
- [ ] `DeckFlow.Core.Tests/Content/SeedIndexFileReaderTests.cs` — 3-outcome reader: two-entry file -> (SeedAvailable, 2 keys); valid empty file -> (SeedAvailable, empty); missing file -> (unavailable, empty); malformed JSON -> (unavailable, empty) (Codex-HIGH)
- [ ] `DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs` — D-02 present/absent classification, null-only idempotency, UNAVAILABLE-seed -> zero writes / rows stay NULL, valid-EMPTY-seed -> classify false (Codex-HIGH)
- [ ] `DeckFlow.Core.Tests/Content/SeedManagedWritePathTests.cs` — bound-parameter invariant: prod-publish/seed-loader rows true, local-distill rows unmarked
- [ ] `DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs` — four discrepancy classes, deterministic IDs, body hash reuse, seed-owned hide eligibility, file-orphan artifact-path identity + no path-inference rescue (Codex-LOW)
- [ ] `DeckFlow.Studio.Tests/Services/ContentKbReconcileStoreTests.cs` — local discrepancy-store idempotent upsert, resolution-by-absence, scope-tagged partial runs, Kind round-trip on GetOpenAsync (Codex-MED)
- [ ] `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs` — dry-run available with flag OFF, two-step re-validated apply, stale (seed-drift) apply rejected, destructive apply flag gate, removal-scoped Apply (mixed-class dry-run still applies seed-drift removals) (Codex-MED)
- [ ] `DeckFlow.Studio.Tests/Services/ProdContentReaderReconcileFlagTests.cs` — P90 web-DB accessor reads `sync.reconcile` and fails closed for indeterminate apply reads
- [ ] `DeckFlow.Web.Tests/Services/FeatureFlags/ReconcileFeatureFlagTests.cs` — `sync.reconcile` catalog registration and seeded-OFF store behavior
- [ ] `DeckFlow.Web.Tests/Integration/ContentSiteIndexStorePostgresSeedManagedTests.cs` — containerized PG coverage for `seed_managed` DDL and timestamptz-preserving soft-hide if feasible in Phase 91; otherwise explicitly defer live PG round-trip to Phase 93 / SYNC-16
- [ ] Confirm `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` remains the home for Studio-side unit/bUnit tests; this project exists in the current checkout, so do not create a new Studio test project.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Studio operator dry-run review | SYNC-11 | Requires an operator git checkout, readable report review, and local Studio state inspection | Run Studio against a fixture checkout, trigger Reconcile dry-run with `sync.reconcile` OFF, confirm all four classes appear in the report/store and no prod writes occur |
| Studio dry-run -> Apply UX | SYNC-12 | Human confirmation and stale-state review are operator workflow, not just service logic | Run dry-run (surfacing multiple classes), mutate seed/prod fixture before Apply, confirm stale Apply is refused; rerun dry-run with matching state, flip `sync.reconcile` ON in the web DB, Apply, and confirm only seed-owned seed-drift removals are soft-hidden while non-removal classes are shown but untouched |
| Actual Render redeploy/reseed behavior | SYNC-12 / SYNC-16 | Needs production-like Render deploy/reseed timing and is the Phase 93 full round-trip gate | In the Phase 93 pre-flip environment, redeploy after a seed removal and confirm no deleted/hidden row is resurrected unexpectedly, body hashes remain stable, and prod-owned rows remain visible |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < ~unknown, CI-authoritative
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
</content>
