---
phase: 47
plan: 02
subsystem: DeckFlow.Studio
tags: [scp, sftp, ssh-net, prod-publish, transport, di-wiring]
requires:
  - DeckFlow.Studio/Services/ISshArtifactUploader.cs (Plan 01 — request-based contract)
  - DeckFlow.Studio/Services/IProdStoreFactory.cs (Plan 01 — on-demand prod store)
  - DeckFlow.Studio/StudioConfig.cs (Plan 01 — 2-arg presence record)
  - SSH.NET 2025.0.0 (Plan 01 — Studio-only package)
provides:
  - SftpArtifactUploader (SSH.NET SftpClient transport, per-file results, path-safety, secret sanitization)
  - Program.cs real SCP presence detection (isScpConfigured)
  - ISshArtifactUploader + IProdStoreFactory DI registrations
  - StudioConfig 2-arg construction from real config (TODO(47-02) resolved)
  - presence-only "Studio SCP: configured/not configured" startup log
affects:
  - DeckFlow.Studio/Pages/DirectPush.razor (Plan 03 — resolves both services from DI)
tech-stack:
  added: []
  patterns:
    - per-call SftpClient (not thread-safe across concurrent calls — Pitfall 5)
    - sanitized-literal-only failure reason (never ex.Message — D-07)
    - EnsureRemoteDirectory walks each segment (SFTP no nested-parent create — MEDIUM-3)
    - presence-only secret logging (extends prod-conn SC5 template to SCP)
key-files:
  created:
    - DeckFlow.Studio/Services/SftpArtifactUploader.cs
  modified:
    - DeckFlow.Studio/Program.cs
decisions:
  - "Implemented against the Wave-1 request-based contract (IReadOnlyList<SshUploadRequest> -> SshUploadResult{LocalPath,RemoteRelativePath,Success,FailureReason}), NOT the IReadOnlyList<string> shape in 47-PATTERNS.md/47-RESEARCH.md — the 47-01-SUMMARY (actual built code) wins per project-notes guidance"
  - "Connect-level SshException/IOException marks ALL not-yet-attempted requests failed with the sanitized reason (whole-batch failure path) rather than throwing"
  - "EnsureRemoteDirectory only creates levels at/below RemoteArtifactRoot; root ancestors assumed to exist on /data"
  - "ILogger<SftpArtifactUploader> is optional and only ever logs constant messages — never config/exception values (D-07)"
metrics:
  duration: ~10m
  completed: 2026-06-16
---

# Phase 47 Plan 02: SFTP Transport + Studio Host Wiring Summary

Delivers PUB-04's artifact-upload transport: a real `SSH.NET SftpClient`-backed
`SftpArtifactUploader` implementing the Wave-1 request-based `ISshArtifactUploader` contract
with per-file `SshUploadResult`s, path-traversal guards, nested-remote-dir creation, and
sanitized failure reasons — plus the `Program.cs` wiring that replaces the temporary
`new StudioConfig(isProdConfigured, false) // TODO(47-02)` stub with real presence-only SCP
detection, registers both new services, and logs SCP presence only. After this plan the
Plan 03 page can resolve `ISshArtifactUploader` + `IProdStoreFactory` from DI and
`StudioConfig.IsScpConfigured` reflects real config presence.

## What Was Built

| Task | Description | Commit | Key files |
|------|-------------|--------|-----------|
| 1 | SftpArtifactUploader (SSH.NET, per-file results, path-safety, secret sanitization) | `9d45dc4` | SftpArtifactUploader.cs |
| 2 | SCP detection + StudioConfig 2-arg + service registrations + presence-only log | `e85312b` | Program.cs |

## Key Decisions

- **Wave-1 contract shape honored (not PATTERNS/RESEARCH):** `UploadArtifactsAsync` takes
  `IReadOnlyList<SshUploadRequest>` (each pairing absolute `LocalPath` with relative
  `RemoteRelativePath`) and returns `SshUploadResult(LocalPath, RemoteRelativePath, Success,
  FailureReason)`. This is the shape the Wave-1 executor actually built (per Codex HIGH-1),
  superseding the `IReadOnlyList<string>` shape shown in 47-PATTERNS.md and 47-RESEARCH.md. The
  uploader builds and traversal-validates the remote path internally from `RemoteRelativePath`
  rather than guessing from the local path.
- **Path-safety (T-47-02c / V5):** `TryBuildRemotePath` rejects rooted paths
  (`Path.IsPathRooted` / leading `/`), any `..` segment, and any candidate that does not stay
  under `RemoteArtifactRoot + "/"` (boundary-safe check, not a loose prefix). A rejected request
  is marked failed with the sanitized reason and skipped — the batch is never aborted.
- **Nested remote dirs (MEDIUM-3 / Pitfall 6):** `EnsureRemoteDirectory` walks each `/`-segment
  at or below the root and `CreateDirectory`s each missing level (SFTP `CreateDirectory` does
  not create nested parents). Root ancestors on `/data` are assumed to exist.
- **Secret sanitization (D-07 / T-47-02a / Pitfall 3):** the only `FailureReason` ever surfaced
  is the constant `"SSH upload failed — check SCP configuration and Render SSH access."` —
  `ex.Message` is never put into a result or a log line (it can carry the host or remote path).
  The three `grep`-visible `ex.Message` strings are all inside `// Why:` comments.
- **One SftpClient per call (Pitfall 5):** `SftpClient` is not thread-safe across concurrent
  calls, so a single client is opened per `UploadArtifactsAsync`, uploads run sequentially in a
  foreach, and the client is disconnected/disposed in `finally`. Connect-level
  `SshException`/`IOException` marks every not-yet-attempted request failed (whole-batch path).
- **Program.cs wiring (D-02/D-03/D-07/SC5):** `isScpConfigured` is presence-only from
  Host+Username+KeyFile+RemoteArtifactRoot (KeyPassphrase excluded — optional);
  `new StudioConfig(isProdConfigured, isScpConfigured)` replaces the `false` stub and the
  `TODO(47-02)` marker is removed; both services registered as singletons; the prod connection
  string is still read on-demand and never materialized into a DI singleton (D-03).

## Deviations from Plan

None — plan executed as written. Per the project-notes guidance, the implementation followed
the Wave-1 `SshUploadRequest`/`SshUploadResult` contract (the actual built code) rather than the
older `IReadOnlyList<string>` shape that still appears in 47-PATTERNS.md/47-RESEARCH.md. This was
explicitly anticipated by the plan and the SUMMARY's `interfaces` block, so it is recorded as a
key decision rather than a deviation.

## Authentication Gates

None — no live SSH or Postgres connection is made in this Wave-2 plan. The `SftpArtifactUploader`
connects only when `UploadArtifactsAsync` is invoked (Plan 03's page), not at construction or DI
startup. SSH.NET (D-01 approved package) was already referenced in Wave 1; no package install or
checkpoint occurred.

## Verification

- `DeckFlow.Studio` builds clean: **Build succeeded** (0 errors). Only warning is the
  pre-existing `DeckFlow.Core/Orchestration/IContentIndexExporter.cs(40,20): CS1574` (unresolvable
  `StageAndCommitAsync` cref) — out of scope, already logged in 47-01-SUMMARY.
- `--filter "DirectPush"`: **8 passed, 0 failed** (Wave-0 stub facts still discoverable; no
  regression — page tests fill in Plan 03).
- Source assertions (Task 1): `client.Connect`/`client.Disconnect`/`client.UploadFile` each
  present once; `progress?.Report` present; `EnsureRemoteDirectory` walks segments;
  `IReadOnlyList<SshUploadRequest>` used; all six `Studio:Scp:*` keys read; LF endings (0 CRLF);
  no `ex.Message` in any surfaced string (only in `// Why:` comments).
- Source assertions (Task 2): both `AddSingleton<ISshArtifactUploader, SftpArtifactUploader>()`
  and `AddSingleton<IProdStoreFactory, ProdStoreFactory>()` present; `Studio SCP:` presence-only
  log present; `new StudioConfig(isProdConfigured, isScpConfigured)` (no literal `false`); no
  `TODO(47-02)` remains; no `AddSingleton` holds `Studio:ProdConnectionString`.

## Known Stubs

None introduced by this plan. The 8 `DirectPushPageTests` stub facts remain (Plan 01's Wave-0
scaffold) and are Plan 03's responsibility to fill — unchanged here.

## Self-Check: PASSED

- FOUND: DeckFlow.Studio/Services/SftpArtifactUploader.cs
- FOUND: DeckFlow.Studio/Program.cs (modified — isScpConfigured + registrations + SCP log)
- FOUND commit: 9d45dc4 (Task 1)
- FOUND commit: e85312b (Task 2)
