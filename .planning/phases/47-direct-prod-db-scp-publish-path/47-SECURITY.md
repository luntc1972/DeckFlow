# Phase 47 Security Audit — Direct Prod-DB + SCP Publish Path

**Phase:** 47 — Direct Prod-DB + SCP Publish Path (DeckFlow.Studio)
**Audited:** 2026-06-16
**Result:** SECURED — 14/14 threats CLOSED
**Disposition:** 13 mitigate, 1 accept (guarded)
**Threats open:** 0

Verification confirmed each declared mitigation exists in the implemented code (not just
documentation). Implementation files were not modified. This phase writes to PRODUCTION
Postgres and SCPs to the live Render `/data` disk; the secret-leak and write-safety threats
were treated as highest-priority and each verified against the actual code path.

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence (file:line) |
|-----------|----------|-------------|--------|----------------------|
| T-47-SC | Tampering (supply chain) | mitigate | CLOSED | `DeckFlow.Studio.csproj:22` pins exact `SSH.NET` Version `2025.1.0`, canonical casing. Present only in Studio csproj — `DeckFlow.Core` and `DeckFlow.Studio.Tests` have zero SSH.NET/Renci references (grep verified; test-project hit is a comment stating it does NOT reference SSH.NET). Operator-approved bump 2025.0.0→2025.1.0 at the blocking-human checkpoint (commit a5c291c; 47-03-SUMMARY:112-123); sole transitive `BouncyCastle.Cryptography`. |
| T-47-01 | Tampering | mitigate | CLOSED | `IProdStoreFactory.cs:18-31` — `ProdStoreFactory.Create` is the sole Postgres-store construction path; `// Why:` at :23-25 documents on-demand build (D-03). `Program.cs:55` registers only the factory, not a live prod store; the local SQLite store stays the DI default (`Program.cs:58`). |
| T-47-02 | Info disclosure | mitigate | CLOSED | `ISshArtifactUploader.cs:44-47` — `FailureReason` XML doc mandates "Sanitized; never contains host/key/path secrets" (D-07). |
| T-47-03 | Tampering (clobber) | mitigate | CLOSED | `FakeContentSiteIndexStore.cs:19-21,56` — `UpsertMethodCalls` seam records each upsert method; SC3 test asserts only `UpsertContentColumnsOnlyAsync`. MEDIUM-4 hardening: full-row upserts throw `InvalidOperationException` (`:38-52`). |
| T-47-02a | Info disclosure | mitigate | CLOSED | `SftpArtifactUploader.cs:84-97,136-142` catch `SshException`/`IOException` and surface only the const `SanitizedFailureReason` (`:19-20`). No `ex.Message` in any returned `SshUploadResult` or log — grep confirms all `.Message` strings live inside `// Why:` comments only. |
| T-47-02b | Info disclosure | mitigate | CLOSED | `Program.cs:42-45` presence-only boolean from Host+Username+KeyFile+RemoteArtifactRoot; `Program.cs:118-119` logs "configured/not configured" only, no value. |
| T-47-02c | Tampering (path join) | mitigate | CLOSED | `SftpArtifactUploader.cs:150-189` `TryBuildRemotePath` rejects rooted paths and `..` segments, then confirms `candidate == root` OR `candidate.StartsWith(root + "/")` (boundary-safe, not loose prefix) before upload; rejected request marked failed and skipped, batch not aborted. |
| T-47-02d | Tampering (concurrency) | accept (guarded) | CLOSED | `SftpArtifactUploader.cs:59-78` one `SftpClient` per `UploadArtifactsAsync` call, sequential `foreach`; `// Why:` at :59-60 documents non-thread-safety (Pitfall 5). No parallel uploads introduced. Accepted-risk disposition matches the code. |
| T-47-02e | Tampering (conn in DI) | mitigate | CLOSED | `Program.cs:38-39` reads prod conn string into a local only for presence detection; no `AddSingleton` holds it (grep: sole `ProdConnectionString` reference is the local read). On-demand construction via `IProdStoreFactory` only (D-03). |
| T-47-03a | Tampering (write path) | mitigate | CLOSED | `DirectPush.razor:695` calls `UpsertContentColumnsOnlyAsync` only; `UpsertRowAsync`/`UpsertRowPreservingVisibilityAsync` absent from the page (grep verified). Test `DirectPush_UsesContentColumnsOnlyUpsert` (`DirectPushPageTests.cs:180-200`) asserts count + `DoesNotContain` both full-row methods. |
| T-47-03b | Tampering (stage gating) | mitigate | CLOSED | Stage 2 button disabled `@(!_prodReviewed ...)` (`DirectPush.razor:225`); Stage 3 disabled `@(!_scpSuccess ...)` (`:311`); `WriteRowsAsync` hard-guard `if (!_scpSuccess || _operationInFlight || !_diffReady) return;` (`:668-671`, MEDIUM-1) — not just the disabled button. 4 tests enforce: CheckboxGates_ScpButton, Stage3Locked_UntilScpSuccess, ScpPartialFailure_Stage3Locked, Stage3InvokedBeforeScp_NoUpsert (`DirectPushPageTests.cs:122,152,203,344`). |
| T-47-03c | Info disclosure | mitigate | CLOSED | All three catch paths use sanitized literals, never `ex.Message`: diff-read `:575`, SSH `:655`, per-row DB-write `:707`, plus init `:476` and final DB catch `:748`. 3 secret tests with sentinel `Host=...;Password=hunter2` assert no sentinel substrings reach markup: Secrets_NeverInMarkup, DiffReadFailure_SecretsNeverSurface, DbWriteFailure_SecretsNeverSurface (`DirectPushPageTests.cs:252,299,319`). |
| T-47-03d | Repudiation | mitigate | CLOSED | `DirectPush.razor` per-file list `_fileResults` (`:441`) and per-row list `_rowResults` (`:447`) persist after the run and render in reconcile tables (`:243-279`, `:329-367`) so the operator can identify the exact failed subset (SC4/D-05). |
| T-47-03e | Tampering (traversal write) | mitigate | CLOSED | Upstream: `ContentSiteIndexStore.ValidateArtifactPath` (`ContentSiteIndexStore.cs:653-671`) rejects rooted/`..` paths and is called from `UpsertContentColumnsOnlyAsync` (`:206`). Defense-in-depth: `SftpArtifactUploader.TryBuildRemotePath` re-confirms the resolved remote path stays under `RemoteArtifactRoot` (`SftpArtifactUploader.cs:150-189`, V5). |

## Unregistered Flags

None. No `## Threat Flags` section is present in any of the three SUMMARY.md files
(grep verified). No new attack surface appeared during implementation without a mapped
threat ID.

## Notes / Residual Risk

- The live prod publish has NOT been run with real prod/SCP secrets (47-03-SUMMARY:120-123,
  items B3 and C remain operator-only). This audit verified the CODE mitigations only;
  runtime prod behavior is the operator's manual smoke responsibility per 47-VALIDATION.md.
- T-47-02d is an accepted (guarded) risk: sequential single-client uploads. If a future
  change introduces parallel uploads or a shared client, this acceptance is void and the
  threat must be re-evaluated.
- Pre-existing out-of-scope warning `DeckFlow.Core/Orchestration/IContentIndexExporter.cs(40,20)
  CS1574` is unrelated to this phase (logged in all three summaries); not a security finding.
