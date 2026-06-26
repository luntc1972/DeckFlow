---
phase: 65
slug: prod-content-artifact-reconcile
status: secured
threats_open: 0
asvs_level: 1
created: 2026-06-22
---

# Phase 65 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register authored at plan-time (all 3 PLAN.md had `<threat_model>` blocks); verified by
> implementation inspection. Phase executed inline on the `cycle11` worktree.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| AI/operator → prod Postgres | Read-only SELECT via Render MCP `query_render_postgres` (read-only by design); no write path exercised | content-index metadata, counts |
| AI/operator → Render service logs | Read-only `list_logs` (resolver-base confirmation) | startup log line (resolved base path) |
| DB `artifact_path` → filesystem | Untrusted relative path combined with a content base before `File.Exists` | path string |
| CLI args (`--db`, `--artifact-root`) → local filesystem | Operator-supplied LOCAL paths; never prod | path strings |
| operator → prod DB / `/data` reconcile | All reconcile writes are operator-run (chosen path = repo seed edit + deploy; no AI prod write) | — |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-65-03-PATH | Tampering (path traversal) | `ContentKbOrphanScanner.Scan` path combine (65-03) | mitigate | `ValidateArtifactPath` rejects rooted / Windows-rooted / `..`-segment `artifact_path` BEFORE `Path.Combine` (`ContentKbOrphanScanner.cs:105-118`, called at `:75`); locked by `Scan_RootedArtifactPath_Throws` + `Scan_DotDotArtifactPath_Throws`. **HIGH if unguarded.** | closed |
| T-65-03-PRODWRITE | Tampering | `content-kb-check` CLI vs prod DB/`/data` | accept (non-threat) | No prod access introduced — `new ContentSiteIndexStore(dbPath)` against a LOCAL `--db` only (`ContentKbCommandRunners.cs:371`); no Npgsql/ProdStore/ProdConnectionString in the handler. | closed |
| T-65-03-INFO | Information Disclosure | CLI catch `Console.Error.WriteLine(ex.Message)` | accept | Command touches only a local sqlite db + local files; the only exception surface is local-file/sqlite text — no prod connection string or SSH host can appear (D-07-safe). | closed |
| T-65-01-INFO | Information Disclosure | Probe output in `65-PROBE-RESULTS.md` / `65-DATA01-DECISION.md` | mitigate | Only metadata rows, counts, and the resolved base path are recorded. Grep of the artifacts found no connection strings / passwords / pg URLs / `sslmode`. The `postgresId` (`dpg-…`) is a non-secret, read-only resource identifier (already in project memory), not a credential. | closed |
| T-65-01-PRODWRITE | Tampering | Prod DB / prod `/data` | accept (non-threat) | All prod DB access was SELECT-only via the read-only Render MCP tool; `list_logs` is read-only. No write path. | closed |
| T-65-02-WRITE | Tampering | Prod reconcile writes | mitigate | The chosen reconcile is a committed-repo **seed edit** (`index-seed.json` slug fix) that the startup seed loader applies on deploy — no AI prod write. Residual prod actions (deploy, 1-row decision) are operator-run; `65-02` is `autonomous: false`. | closed |
| T-65-02-INFO | Information Disclosure | Decision/execution log in `65-DATA02-DECISION.md` | mitigate | No connection strings / SCP host / raw Npgsql/SSH exception text recorded (verified by grep); only row paths, ids, slugs, and counts. | closed |
| T-65-02-PITFALL3 | Integrity | Reconcile source correctness | mitigate | The chosen fix re-points to bodies that already exist in the committed repo tree (all 19 ids verified present under `salubrioussnail/`); no "re-upload from a missing source" path was taken — the RESEARCH Pitfall-3 hazard does not apply. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-65-1 | T-65-03-PRODWRITE / T-65-01-PRODWRITE | No prod write path is introduced by this phase; all prod interaction is read-only (Render MCP SELECT + log read) or operator-run. Documented non-threat. | operator | 2026-06-22 |
| AR-65-2 | T-65-03-INFO | CLI catch surfaces only local-file/sqlite exception text; no prod credential surface. Accepted-low. | operator | 2026-06-22 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-22 | 8 | 8 | 0 | Claude (inline verification — small concrete surface; mitigations grep-verified) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: secured` set in frontmatter

**Approval:** verified 2026-06-22
