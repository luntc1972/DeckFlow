---
phase: 43
slug: approval-status-safe-upsert
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-13
---

# Phase 43 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Data-layer-only phase (approval_status column + safe upsert + approved-only export). No UI, no new network surface, no new packages.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| in-process → local SQLite/Postgres | DDL + UPDATE + upsert + filtered SELECT in `ContentSiteIndexStore`; only "input" is content already in the operator's local store (no untrusted end-user request reaches this code) | content/nav columns + admin flags (low sensitivity, operator-owned) |
| local store → exported `index-seed.json` | The approved-only filter decides which rows ship to the public repo / prod seed | curated KB rows (public-bound) |
| test setup SQL → temp SQLite | Test-only direct SQL builds legacy schemas; isolated per-fact temp DB deleted on Dispose | synthetic test data |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-43-01 | Tampering / Injection | New ALTER/UPDATE/upsert/SELECT SQL in `ContentSiteIndexStore` | mitigate (LOW) | Row values bound via `RelationalDatabaseConnection.AddParameter` (e.g. `@visible` at `ContentSiteIndexStore.cs:102`); only inline literals are fixed enum-like strings `'pending'`/`'approved'` (constants, not user input). No value interpolation. | closed |
| T-43-02 | Tampering / Data integrity | Self-healing grandfather backfill in `EnsureSchemaAsync` | mitigate (MED) | ALTER runs once (column-existence guard); grandfather UPDATE runs each pass but bounded `WHERE approval_status = 'pending'` (`:99`) AND visible → recoverable after ALTER-then-crash, never re-stamps operator-changed (non-pending) statuses. Proven by `EnsureSchemaAsync_Grandfather_DoesNotRestampOperatorChangedStatus` (passing). | closed |
| T-43-03 | Information Disclosure | Approved-only export filter (`GetApprovedRowsAsync` + `ExportIndexAsync` switch) | mitigate (HIGH) | `GetApprovedRowsAsync` uses literal `WHERE approval_status = 'approved'` (`:321`); `ExportIndexAsync` switched `GetAllRowsAsync`→`GetApprovedRowsAsync` (`ContentKbOrchestrator.cs:610`). Proven by `GetApprovedRowsAsync_ReturnsOnlyApprovedRows` (seeds approved+pending+rejected, asserts only approved returned — passing). Pending/rejected content cannot reach the public seed. | closed |
| T-43-04 | Tampering | Exported JSON byte-shape | accept/guard (LOW) | `approval_status` deliberately NOT added to `ContentIndexExportRow` (grep count = 0). Phase 42 golden fixture test remains the guard; not modified this phase. | closed |
| T-43-SC | Tampering | npm/pip/cargo/NuGet installs | n/a | No package installs (no `.csproj` PackageReference changes in `6b0a4fe`/`cb64b16`). Legitimacy gate not applicable. | closed |
| T-43-T1 | Tampering / Injection | Direct setup SQL in tests | mitigate (LOW) | Row values bound via `AddParameter`; only fixed status-string literals inlined. Mirrors existing visibility-test discipline. Test-only surface, isolated temp DB. | closed |
| T-43-T2 | (verification of) Information Disclosure | `GetApprovedRowsAsync` filter | mitigate (HIGH→verified) | Executable proof of T-43-03 + asserts `ApprovalStatus` populated from SELECT. Passing in Core.Tests 342/342. | closed |
| T-43-T3 | (verification of) Data integrity | Grandfather idempotency / no-restamp | mitigate (MED→verified) | Executable proof of T-43-02 (fresh-store re-run preserves operator-changed status). Passing. | closed |

*Status: open · closed*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|

No accepted risks (all threats closed by mitigation/guard).

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-13 | 8 | 8 | 0 | Claude (secure-phase 43; mitigations cross-verified against shipped commits 6b0a4fe + cb64b16, code-review + verifier 4/4, Core.Tests 342/342 incl 10 approval facts) |

---

## Outstanding (non-blocking)

- **LOW — Postgres live-migration column presence:** verifiable only on a Render deploy (CI is SQLite-only). The `approval_status` ALTER path is structurally identical to three production-proven blocks (`is_visible`, `is_evergreen`, `is_hidden`). No code change required; confirm post-deploy.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (none)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-13
