---
phase: 47
plan: 01
subsystem: DeckFlow.Studio
tags: [scaffold, interfaces, test-seams, ssh-net, prod-publish]
requires:
  - DeckFlow.Core/Content/ContentSiteIndexStore.cs (RelationalDatabaseConnection ctor)
  - DeckFlow.Core/Storage/PostgresConnectionStringNormalizer.cs
provides:
  - ISshArtifactUploader + SshUploadRequest + SshUploadResult contracts
  - IProdStoreFactory + ProdStoreFactory (on-demand Postgres store)
  - StudioConfig.IsScpConfigured
  - FakeSshArtifactUploader, FakeProdStoreFactory test doubles
  - FakeContentSiteIndexStore.UpsertMethodCalls tracking (SC3/D-08 seam)
  - DirectPushPageTests (8 stub facts)
affects:
  - DeckFlow.Studio/Program.cs (temp 2-arg StudioConfig call, TODO 47-02)
tech-stack:
  added:
    - SSH.NET 2025.0.0 (DeckFlow.Studio only — D-01 approved exception)
  patterns:
    - request-based uploader contract (Codex HIGH-1 path-traversal guard)
    - on-demand prod store factory (D-03)
key-files:
  created:
    - DeckFlow.Studio/Services/ISshArtifactUploader.cs
    - DeckFlow.Studio/Services/IProdStoreFactory.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
  modified:
    - DeckFlow.Studio/DeckFlow.Studio.csproj
    - DeckFlow.Studio/StudioConfig.cs
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
decisions:
  - "ISshArtifactUploader takes SshUploadRequest(LocalPath, RemoteRelativePath) not IReadOnlyList<string> (Codex HIGH-1) — supersedes 47-PATTERNS.md/47-RESEARCH.md string-list shape so remote paths are built + traversal-validated inside the uploader"
  - "SshUploadResult carries RemoteRelativePath in addition to LocalPath so the per-file reconcile list (SC4) can key by the remote path"
  - "Program.cs StudioConfig call temporarily 2-arg new StudioConfig(isProdConfigured, false) with TODO(47-02) — Plan 02 wires real SCP detection"
metrics:
  duration: ~12m
  completed: 2026-06-16
---

# Phase 47 Plan 01: Wave-0 Test Seams + SSH.NET + Contracts Summary

Lays the interface-first scaffold for the Direct Prod-DB + SCP publish path: two new Studio
contracts (`ISshArtifactUploader` with the request-based shape, `IProdStoreFactory` with the
real on-demand Postgres `ProdStoreFactory`), the `SSH.NET 2025.0.0` package reference (Studio
only), `StudioConfig.IsScpConfigured`, two new test doubles, an `UpsertMethodCalls` tracking
seam on the existing fake, and a stubbed `DirectPushPageTests` with all 8 named req/SC facts —
so Plan 02 (SFTP impl) and Plan 03 (page + tests) build against handed-down contracts.

## What Was Built

| Task | Description | Commit | Key files |
|------|-------------|--------|-----------|
| 1 | SSH.NET 2025.0.0 + ISshArtifactUploader + IProdStoreFactory | `a1d14ed` | csproj, ISshArtifactUploader.cs, IProdStoreFactory.cs |
| 2 | StudioConfig.IsScpConfigured + 2 test doubles + upsert tracking | `a9f272d` | StudioConfig.cs, Program.cs, FakeSshArtifactUploader.cs, FakeProdStoreFactory.cs, FakeContentSiteIndexStore.cs |
| 3 | DirectPushPageTests with 8 stub facts | `e687b9b` | DirectPushPageTests.cs |

## Key Decisions

- **HIGH-1 request-based contract honored:** `UploadArtifactsAsync` takes
  `IReadOnlyList<SshUploadRequest>` where each request pairs the absolute `LocalPath` with the
  row's relative `RemoteRelativePath`. This supersedes the `IReadOnlyList<string>` shape shown
  in 47-PATTERNS.md and 47-RESEARCH.md, per the plan's explicit instruction (Codex HIGH-1):
  the impl builds and traversal-validates the remote path under `RemoteArtifactRoot` rather than
  guessing it from an absolute local path. The XML doc on `RemoteRelativePath` states the
  validation contract; the `FailureReason` doc states it is sanitized and never contains
  host/key/path secrets (D-07).
- **`SshUploadResult` extended with `RemoteRelativePath`** so the per-file reconcile list (SC4)
  and the `FakeSshArtifactUploader.FilesToFail` injection key by the remote path.
- **`ProdStoreFactory` built on-demand (D-03):** `Create` normalizes via
  `PostgresConnectionStringNormalizer.Normalize`, builds
  `RelationalDatabaseConnection(Postgres, normalized)`, and returns `new ContentSiteIndexStore(conn)` —
  no Core change (the `RelationalDatabaseConnection` ctor overload already exists at
  `ContentSiteIndexStore.cs:30`). A `// Why:` comment documents that this is never a DI startup
  singleton.
- **`FakeContentSiteIndexStore.UpsertMethodCalls`** records the method name in all three Upsert*
  methods so Plan 03's SC3 test can assert ONLY `UpsertContentColumnsOnlyAsync` is invoked on
  prod (D-08 / T-47-03).

## Deviations from Plan

**1. [Rule 3 - Blocking fix] Program.cs StudioConfig call site updated to 2-arg form**
- **Found during:** Task 2 (adding the second positional `IsScpConfigured` parameter to the
  record breaks the existing `new StudioConfig(isProdConfigured)` call).
- **Issue:** Extending the record without updating its single call site would leave Plan 01's
  own build red (CS7036).
- **Fix:** Updated `DeckFlow.Studio/Program.cs:47` to `new StudioConfig(isProdConfigured, false)`
  with a `// TODO(47-02): real SCP detection` marker. This is explicitly directed by the plan's
  Task 2 action (keep this plan's build green; Plan 02 replaces the `false`). Not a discovery —
  recorded here for traceability.
- **Files modified:** DeckFlow.Studio/Program.cs
- **Commit:** `a9f272d`

No other deviations. SSH.NET install succeeded (D-01 approved package; no checkpoint needed).

## Authentication Gates

None — no live SSH or Postgres connection is made in this Wave-0 scaffold plan.

## Verification

- `DeckFlow.Studio` builds clean: **Build succeeded** (0 errors, 0 new warnings).
- `DeckFlow.Studio.Tests` builds clean: **Build succeeded** (0 errors, 0 new warnings).
- `--filter "DirectPush"`: **8 passed, 0 failed** (stub facts discoverable).
- Full Studio suite: **29 passed, 0 failed, 0 skipped**.
- `SSH.NET` present in `DeckFlow.Studio.csproj` only; absent from `DeckFlow.Studio.Tests.csproj`
  and `DeckFlow.Core/`.
- `UploadArtifactsAsync` signature uses `IReadOnlyList<SshUploadRequest>` (HIGH-1).
- `ProdStoreFactory.Create` uses `RelationalDatabaseProvider.Postgres` +
  `PostgresConnectionStringNormalizer.Normalize`.

### Pre-existing warning (out of scope)
`DeckFlow.Core/Orchestration/IContentIndexExporter.cs(40,20): warning CS1574` (unresolvable
`StageAndCommitAsync` cref) is pre-existing in DeckFlow.Core and unrelated to this plan's
changes. Logged here; not fixed (scope boundary).

## Known Stubs

`DirectPushPageTests.cs` contains 8 intentional stub facts
(`Assert.True(true, "stub — implemented in 47-03")`) and a `RenderDirectPush` helper with the
`Render<DirectPush>()` line commented out (`// TODO(47-03)`). This is the planned Wave-0
scaffold: `DirectPush.razor` does not exist until Plan 03, which fills the fact bodies and
un-stubs the render call. Documented as intentional per the plan; SC verification is Plan 03's
responsibility.

## Self-Check: PASSED

- FOUND: DeckFlow.Studio/Services/ISshArtifactUploader.cs
- FOUND: DeckFlow.Studio/Services/IProdStoreFactory.cs
- FOUND: DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs
- FOUND: DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs
- FOUND: DeckFlow.Studio.Tests/DirectPushPageTests.cs
- FOUND commit: a1d14ed (Task 1)
- FOUND commit: a9f272d (Task 2)
- FOUND commit: e687b9b (Task 3)
