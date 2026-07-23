# Phase 93: Round-Trip Integration Test - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning
**Source:** Code-mapping (two Explore sweeps) + 4 operator design decisions (2026-07-10)

<domain>
## Phase Boundary

Deliver **SYNC-16**: one automated end-to-end integration test that locks the entire Content-KB sync loop —
**distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile** —
against a **real Testcontainers Postgres** and a **real `git init` tree**, so no future change can silently
reintroduce any of the Cycle-16 fixed drift classes (unreachable body, `/app`-shadows-`/data`, revert-after-reseed,
Pull clobber, ghost rows, body-hash mismatch).

Phase 93 is also the **pre-flip gate** for the two prod flags this cycle shipped OFF
(`sync.directpush-gitbody`, `sync.reconcile`). Per operator decision, Phase 93 delivers the test **plus** an operator
pre-flip checklist document; the deferred FU-1/FU-2 code follow-ups from `90-FOLLOWUPS.md` stay deferred (flag flips
are operator-owned and FU-1 hinges on an unmade design decision).

**In scope:** the round-trip xUnit integration test (real PG + real git), its harness/seams, an operator pre-flip
checklist artifact, and the minimal test-project wiring to host it.
**Out of scope:** any production code change to the sync path; the FU-1/FU-2 DirectPush follow-up code; flipping the
prod flags (operator-owned); a CI Postgres service (see D-07); public-app feature changes.

</domain>

<decisions>
## Implementation Decisions

### Test home (Q1 → locked)
- **D-01:** Host the test in **`DeckFlow.Web.Tests`** and add **one `<ProjectReference>` to `DeckFlow.Studio`**.
  Web.Tests already carries `Testcontainers.PostgreSql` 3.10.0 + `PostgresContainerFixture` + `PostgresFactAttribute`
  and references Web (→Core); adding the Studio ref is the only missing edge to reach all three app assemblies
  (Core distill/store/hash, Web body-serve/seed-loader, Studio DirectPush/Pull/Reconcile/Publish coordinators).
  New test type(s) under `DeckFlow.Web.Tests/Integration/RoundTrip/`. **No new NuGet package** (Testcontainers already
  in-solution). No new test project, no re-scaffolded fixture.

### Real Postgres for the prod side (Q1 constraint → locked)
- **D-02:** The **prod store is a real `ContentSiteIndexStore` over the Testcontainers PG container** via the same
  `IProdStoreFactory` path DirectPush/Reconcile use in production (`ProdStoreFactory.Create` →
  `new ContentSiteIndexStore(conn, ensureSchemaEnabled:false)`). Because that factory store has schema-ensure OFF,
  the test **pre-creates the PG schema once** in setup by constructing a schema-ensuring store over the same
  connection and calling `EnsureSchemaAsync` (mirror how `PostgresStorageTests`/`DapperTypeHandlerRoundTripTests`
  build a PG store). This proves the **Postgres dialect** DDL/upsert/`body_sha256`/`seed_managed` round-trip for real
  (SYNC-16 "containerized Postgres" + "body_sha256 matches end-to-end").
- **D-02a — `ProdContentReader` SslMode.Require snag:** `DeckFlow.Studio/Services/ProdContentReader.cs` hardcodes
  Npgsql + `SslMode.Require`, so the **real** reader cannot connect to the plain (no-TLS) Testcontainers container.
  **Ratified default:** front the prod-**read** path (Pull, reconcile dry-run prod read, flag reads) with a
  **fixture prod reader over the SAME real PG store** — reuse the `FixtureProdReader`/`FixtureProdStoreFactory` pattern
  already proven in `DeckFlow.Studio.Tests/ViewModels/ReconcileFixtureDriveTests.cs`, repointed from SQLite to the PG
  container. The prod **data** stays real Postgres; only the read *transport* is a test adapter — SYNC-16 asserts data
  integrity (served == published, hashes match, no-revert), not the SSL handshake. Planner may instead enable
  container TLS or override `SslMode` **iff** trivial and byte-for-byte behavior-preserving; the fixture-reader default
  is the fallback and needs no production change.

### Real git tree (Q4 → locked)
- **D-03:** Use a **real `git init` temp repository** driven by the **real `GitRepository`** (shell-out; WSL has git),
  NOT `FakeGitRepository`. A fake cannot prove commit-persistence or no-revert-after-reseed — the core SYNC-16 claim.
  Simulate the Render deploy as a **working-tree copy** from the committed repo into a separate **`/app` stand-in dir**
  (this is exactly what Render does: git checkout → `/app`); the web body resolver's `ContentKb:ContentBase` points at
  that app dir. **Reseed** = `ContentKbSeedLoader.LoadIfPresentAsync` against the app-dir `index-seed.json` into the
  **prod PG store**. No test does real git today — this is new but feasible; keep git work behind the existing
  `IGitRepository`/`GitRepository` seam and honor `ArtifactPathSafety` for path use.

### Distiller + SFTP + deploy-confirm seams
- **D-04 (distiller faked):** Drive distill through the **real `ContentKbOrchestrator`** but with a **canned
  `ILlmDistillationService`** producing a **deterministic body** → deterministic `body_sha256`. Never invoke the real
  Claude CLI (non-deterministic, network, slow). The distill hop is proven by `ContentArtifactWriter` +
  `ComputeBodySha256` + the content-column upsert (`ContentKbOrchestrator.cs:1357-1358`), not LLM output quality.
- **D-05 (SFTP faked):** Fake `ISshArtifactUploader` (records calls). Under `sync.directpush-gitbody` ON, bodies serve
  from `/app` (git), so the `/data` SFTP overlay is out of the serving path by design — the fake need only satisfy the
  coordinator contract.
- **D-06 (DirectPush flag ON + deploy-confirm):** The round-trip exercises **`sync.directpush-gitbody` ON** (the target
  end-state the flip enables — git-body serving, seed re-export). Default the `IDeployedBodyConfirmer` to a **fake that
  confirms once the `/app` tree carries the matching `body_sha256`** (keeps HTTP/BasicAuth out of the test). Planner may
  instead drive the real `ContentKbDeployedBodyController` against the app tree iff it stays in-process and cheap.

### CI enforcement (Q2 → locked)
- **D-07:** **Local/manual gate, NOT CI-enforced.** The test is `[PostgresFact]` + `IClassFixture<PostgresContainerFixture>`,
  so it **auto-skips** wherever `DECKFLOW_POSTGRES_TESTS=1` + Docker are absent — which includes CI (CI runs
  `dotnet test --no-build` with PG auto-skip and provisions no Docker/Postgres). This matches **every** existing PG test.
  It is the **pre-flip proof harness** — run locally with Docker or push-and-watch — not a per-PR lock. **`.github/workflows/`
  is NOT touched** this phase. The test class comment + the operator checklist must state this explicitly so the skip is
  not mistaken for coverage.

### FU pre-flip scope (Q3 → locked)
- **D-08:** **Test + document only.** Phase 93 ships the SYNC-16 test **and** an operator pre-flip checklist artifact
  consolidating FU-1 (stale-visible-on-update), FU-2 (indeterminate-flag strand), and FU-3 (live reconcile walk) from
  `90-FOLLOWUPS.md` / `91-09`. The FU-1/FU-2 **code** stays deferred — the flag flips are operator-owned and FU-1 is an
  open design decision (accept-by-design vs hide-then-reconfirm). This keeps the final phase's blast radius = **tests + docs**.

### Claude's Discretion
- Exact test decomposition — one comprehensive `[PostgresFact]` walking the whole loop vs. a shared-fixture flow with
  several focused `[PostgresFact]` assertions (served==published, hash-at-every-hop, no-revert-after-reseed, Pull
  field-authority, reconcile-idempotent-zero-dupes). Planner decides; keep each assertion's failure message specific.
- Whether the harness is a reusable `RoundTripHarness`/fixture helper vs. inline test body — follow the
  `ReconcileFixtureDriveTests` driver precedent.
- Where the pre-flip checklist lives (new `93-PREFLIP-CHECKLIST.md` phase artifact vs. appended section) — planner's call.
- Test seams/doubles follow the established `Fake*` / `FixtureProdReader` / `IProdStoreFactory` patterns.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` §SYNC-16 — the single locked requirement
- `.planning/ROADMAP.md` §"Phase 93: Round-Trip Integration Test" — goal + 3 success criteria
- `.planning/STATE.md` — cycle status, prior-phase decisions
- `.planning/phases/90-directpush-correctness-seed-sync/90-FOLLOWUPS.md` — FU-1/FU-2/FU-3 pre-flip items (D-08)

### Test infrastructure to REUSE (do not re-scaffold)
- `DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs` — `IClassFixture`, `postgres:16-alpine`, deferred start,
  `DECKFLOW_POSTGRES_TESTS=1` gate, dynamic skip when Docker absent, `GetConnectionStringOrSkipAsync()`
- `DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs` — discovery-time skip attribute
- `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs`, `DapperTypeHandlerRoundTripTests.cs` — how a PG-backed store is built + schema-created in a test
- `DeckFlow.Studio.Tests/ViewModels/ReconcileFixtureDriveTests.cs` — the end-to-end driver precedent: real store as prod stand-in, real git-style tree + seed, `FixtureProdReader`/`FixtureProdStoreFactory` transport doubles (D-02a, D-03 pattern)
- `DeckFlow.Studio.Tests/TestDoubles/` — `FakeGitRepository` (NOT used here per D-03), `FakeProdStoreFactory`, `FakeProdContentReader`, `FakeContentSiteIndexStore`, `FakeDirectPushFlagReader`, `FakeReconcileFlagReader`, `FakeDeployedBodyConfirmer`, `Fake*` SFTP uploader

### Sync-loop components the test drives (from the code map)
- **Distill:** `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` — `DistillAsync:238`, `DistillVideoAsync:1167`
  (artifact write `:1336`, prompt bake `:1348`, `ComputeBodySha256` `:1357`, `UpsertContentColumnsOnlyAsync` `:1358`);
  `ContentArtifactWriter`; distiller seam `ILlmDistillationService` (`ThrowingLlmDistillationService` precedent)
- **Publish:** `DeckFlow.Studio/ViewModels/PublishCoordinator.cs` — `LoadInitDataAsync:63`, `ExportAndDiffAsync:85`
  (seed export `:97`, body copy `:110`), `CommitAsync:203` (`StampPushedToProdAsync:217`); seed const `:26`;
  shared seed writer `ContentKbOrchestrator.ExportIndexToFileAsync:748`
- **DirectPush:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — ctor 10 deps `:73`, `ComputeDiffAsync/ClassifyDiff:125/147`,
  `UploadArtifactsAsync:212`, `WriteContentAsync:242` (`SeedManaged=true` `:252`, awaiting-confirm `:261`),
  `VerifyAndPublishAsync:299` (tri-state flag `:321`), `ConfirmAndPublishAsync:273` (stamp `pushed_to_prod_utc` + flip `is_visible` `:283-287`),
  `CommitAndPushBodiesAsync:405` (seed re-export `:486`, `[skip render]` gate `:526`), flag const `:48`;
  `DeckFlow.Studio/Services/DeployedBodyConfirmer.cs:66` (URL `:95` → `/Admin/api/contentkb/deployed-body-hash`)
- **Prod store:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — SQLite ctor `:25`, `RelationalDatabaseConnection`
  ctor+`ensureSchemaEnabled` `:37`, `EnsureSchemaAsync:71`, columns `body_sha256:132`/`seed_managed:152`/`awaiting_confirm_utc:140`,
  `UpsertContentColumnsOnly(Batch)Async:226/796`, `StampPushedToProdAsync:687`, `Set/ClearAwaitingConfirmAsync:723/760`,
  `SetVisibilityAsync:876/470`, `SetApprovalStatusAsync:625/649`, `HideSeedManagedAsync:915` (atomic `AND seed_managed=TRUE`),
  reads `GetByNaturalKeyAsync:243`/`GetPublishedRowsAsync:285`/`GetApprovedRowsAsync:323`, `SetBodySha256IfNullAsync:504`,
  `ProdStoreFactory.Create` (`IProdStoreFactory.cs:32`, schema-ensure OFF)
- **Web body resolution + SYNC-03 guard:** `DeckFlow.Web/Controllers/ContentKbController.cs` — `Detail:100`, body read `:136`,
  `ComputeBodySha256(raw):144` + mismatch `LogWarning:145-152` (fail-open); `ContentKbArtifactPathResolver.cs`
  `TryResolveExistingArtifact:102` / `TryResolveGitArtifact:160`; `ContentKbArtifactBodyResolver.TryReadArtifactTextAsync:28`;
  test precedent `DeckFlow.Web.Tests/ContentKbControllerTests.cs`
- **Reseed:** `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` — `LoadIfPresentAsync:43`, `seed_managed=true` `:90`,
  `UpsertRowPreservingVisibilityAsync` (`ContentSiteIndexStore.cs:206`); `DeckFlow.Core/Content/SeedIndexFileReader.cs`
- **PullFromProd:** `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` — `PullAndClassifyAsync:76` (prod read `:92`,
  git-tree resolve `:100`, classify `:125`), `CheckFreshnessAsync:239` (Fetch `:251`, GetBehindCount `:252`, 5s timeout `:247`),
  `ApplyAdoptionsAsync:146` (body←git `:187`, approval←prod `:188`), `ComputeBodyDivergence:285`
- **Reconcile:** `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs:44` (4 classes), `ContentKbReconcileDiscrepancy.cs`
  (enum `:10`, record `:42`, `BuildId:72`), `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs:65`
  (`RunDryRunAsync`, git walk `:108`, persist `:90`, report `:95`), `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs:21`
  (`RunDryRunAsync:76`, `ApplyRemovalsAsync:145`, flag gate `:29/152/274`), `DeckFlow.Studio/Services/ContentKbReconcileStore.cs:16`
- **Body-hash helper (SYNC-01/02, THE one surface):** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` —
  `ComputeBodySha256:131` (SplitHeader-strip `:135`, LF-normalize `:141`, UTF-8 SHA-256), `BuildSignature:65` (incl `body_sha256` `:107`),
  `AreContentEqual:119`; guard test `DeckFlow.Core.Tests/Content/OneSignatureSurfaceGuardTests.cs`

### Existing headless harness precedents
- `DeckFlow.CLI/ContentKbCommandRunners.cs` — real orchestrator over SQLite (`RunDistillAsync:80`, `RunContentIndexExportAsync:318`)
- Studio `*CoordinatorTests` under `DeckFlow.Studio.Tests/ViewModels/` — closest model for headless coordinator drives

### Cycle design docs
- `docs/research/kb-prod-sync-roadmap.md`, `docs/research/kb-prod-sync-fix-design.md` — M1/M2/M3/M6/M7/M8 drift-class origins

### Project constraints
- `CLAUDE.md` — WSL VSTest caveat (build clean + push-and-watch CI), CRLF/LF gate, no new packages without ask,
  `.github/workflows/` protected (untouched per D-07), Testcontainers already in-solution

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PostgresContainerFixture` + `PostgresFactAttribute` — the whole PG-container skip/gate story is already built; the
  test just consumes it via `IClassFixture` (D-01).
- `ReconcileFixtureDriveTests` — a full real-orchestrator + real-git-style-tree + real-seed driver already exists; the
  round-trip test is its natural superset, upgraded to real PG (D-02) + real git (D-03).
- `ComputeBodySha256` / `BuildSignature` — the single hash surface every hop already calls; the test asserts equality
  against it, never re-hashes with a second scheme.
- `IProdStoreFactory` (schema-ensure OFF) + `RelationalDatabaseConnection` — a "prod" store is just a second store over
  the PG conn; pre-create its schema once via a schema-ensuring store.

### Established Patterns
- Every PG test is gated `DECKFLOW_POSTGRES_TESTS=1` + Docker, dynamic-skips otherwise → D-07 CI behavior is automatic.
- Prod is read ephemerally (conn string per call, never DI-materialized); NO DDL against prod at run time (schema-ensure OFF).
- `is_visible`/`is_hidden` never written by content-only upsert — the round-trip asserts this invariant survives reseed.
- Fail-safe on indeterminate signal (null hash, unreadable) → surface, never silently adopt — assert the happy path
  produces `BodyDivergenceStatus.Clean`.

### Integration Points / Snags to plan around
- **Cross-assembly:** add `DeckFlow.Studio` `<ProjectReference>` to `DeckFlow.Web.Tests.csproj` (D-01). Verify no
  circular ref (Studio does not reference Web; Web.Tests referencing both is fine).
- **`ProdContentReader` is Npgsql/SslMode.Require-only** → cannot hit the plain container; front prod reads with a
  fixture reader over the real PG store (D-02a).
- **Schema pre-create on PG** required before the schema-ensure-OFF factory store is used (D-02).
- **Real git in WSL** — `GitRepository` shells to `git`; deterministic commits need `GIT_*` author/committer env or a
  temp `git config` so the temp repo can commit headlessly.
- **Deploy simulation** = copy committed tree → app-dir; web resolver `ContentKb:ContentBase` → app-dir (D-03).

</code_context>

<specifics>
## Specific Ideas

- Round-trip shape to encode: `git init` → distill(faked LLM)→body+row+hash → Publish export seed+commit(real git) →
  copy tree→`/app` + reseed into **PG** → web resolve+hash-assert(served==published, hash match) → DirectPush 2nd row
  into PG (flag ON, faked confirm) + re-export seed + commit → **reseed again (redeploy)** + assert no-revert →
  PullFromProd (field authority, `BodyDivergence.Clean`) → Reconcile dry-run (zero unexpected discrepancies, idempotent re-run zero dupes).
- Assert `body_sha256` equality at **every** hop: distill-computed == seed-json == prod-row == served-body-recompute.
- The no-revert assertion is the load-bearing one — it is the M2/C3 bug the whole flag exists to fix.

</specifics>

<deferred>
## Deferred Ideas

- FU-1 / FU-2 **code** (hide-on-update-to-visible / resume-re-triggers-redeploy) — deferred; Phase 93 documents them as
  operator pre-flip decisions only (D-08).
- A CI Postgres-service job to make SYNC-16 a per-PR lock — deferred (D-07); revisit if the sync path churns again.
- Enabling container TLS / real `ProdContentReader` in the test — only if trivial; fixture-reader default stands (D-02a).
- Actually flipping `sync.directpush-gitbody` / `sync.reconcile` ON in prod — operator-owned, gated on this phase's
  checklist; not a code deliverable.
- SYNC-F1 (retire DirectPush) / SYNC-F2 (scheduled reconcile) — out of cycle.

</deferred>

---

*Phase: 93-round-trip-integration-test*
*Context gathered: 2026-07-10 via code-mapping + 4 operator decisions*
