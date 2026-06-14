---
phase: 49
slug: dapper-data-access-adoption
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-14
---

# Phase 49 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Mechanism swap (hand-written ADO.NET → Dapper) behind unchanged `IRelationalDialect`/`RelationalDatabaseConnection`. SQL kept verbatim; no new public surface.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| application code → SQL execution | Store methods bind user/source-derived values (feedback text/email/URL/UA, slugs, flag values, IP keys, harvested content, card names, deck ids) into queries | user + source-derived strings/ids |
| CLR object → DB write (type handlers) | Parameter values encoded by `TypeHandler<T>.SetValue`; built-in typeMap removed (D-07) so the handler is the resolved write-path binder | DateTime/decimal/bool/Guid/DateTimeOffset |
| DB reader → CLR object (type handlers) | Stored (possibly legacy/malformed) DB values deserialized by handlers' `Parse()` | encoded primitives |
| process init → SqlMapper global state | Handler registration mutates global Dapper state; both providers run in one process | static registration |
| transaction scope → Dapper calls | A missed `transaction:` arg would run a write outside the atomic scope (CategoryKnowledgeRepository) | UPSERT/RETURNING writes |
| spike verdict → sweep authorization | A wrong VERDICT read would propagate a broken pattern or needlessly abort | gate decision |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-49-01 | Tampering | FeedbackStore Dapper params (SQLi regression) | mitigate | SQL verbatim + parameterized via anon objects; no interpolated value in SQL (verified across phase diff) | closed |
| T-49-02 | Tampering | Type-handler `Parse()` malformed values (D-05 parity) | mitigate | `switch` + `Convert.*`/`Guid.Parse`; fail-closed `InvalidCastException` fallback; round-trip parity test | closed |
| T-49-02b | Tampering | `SetValue` not firing on built-in primitive write path | mitigate | `EnsureRegistered` unconditionally `RemoveTypeMap(T)`+`RemoveTypeMap(T?)` before `AddTypeHandler` (D-07); grep==10; raw on-disk write-path assertions (plain SqliteConnection reader) prove firing | closed |
| T-49-03 | DoS | `EnsureRegistered()` race / both providers one process | mitigate | `Interlocked.Exchange` idempotent guard (DapperTypeHandlers.cs:21); handlers branch on runtime param type, not ambient flag | closed |
| T-49-SC | Tampering | Dapper package install (supply chain) | **accept** | Dapper 2.1.79 (DapperLib official, ~13yr, 500M+ dl); single approved package; user-approved | closed (accepted) |
| T-49-GATE | EoP | Sweep dispatched despite FAIL verdict | mitigate | Structural: sweep `depends_on: ["49-01b"]` blocking gate; machine-checkable `VERDICT: PASS` → `GATE: AUTHORIZED` recorded | closed |
| T-49-04 | Tampering | Wave-2 store params (SQLi regression) | mitigate | SQL verbatim + parameterized; UPSERT arithmetic unchanged; 0 interpolation (diff-verified, 7 stores) | closed |
| T-49-05 | Tampering | `DateTimeOffsetTypeHandler.Parse` legacy strings (D-05 parity) | mitigate | Two-step parse (RoundtripKind then AssumeUniversal\|AdjustToUniversal fallback); fail-closed on malformed | closed |
| T-49-05b | Tampering | `DateTimeOffsetTypeHandler.SetValue` not firing | mitigate | Same D-07 RemoveTypeMap pattern (T+T?); DateTimeOffset raw on-disk "O" assertion in round-trip test | closed |
| T-49-06 | Repudiation | AdminBruteForceTracker UPSERT semantics drift | mitigate | INTERVAL/julianday arithmetic verbatim; 8 brute-force tests assert lockout unchanged | closed |
| T-49-07 | Tampering | Wave-3 store params (SQLi regression) | mitigate | SQL verbatim + parameterized; UPSERT/RETURNING/ON CONFLICT unchanged; 0 interpolation (4 stores) | closed |
| T-49-08 | Tampering | HarvestRunStore Guid + nullable DateTimeOffset coercion (D-05 parity) | mitigate | Guid+DTO handlers round-trip proven; nullable `DateTimeOffset?` binds DBNull; fail-closed; HarvestRunStore tests pass | closed |
| T-49-09 | Tampering | DDL/constraint migration accidentally rewritten | mitigate | Constraint-migration/ALTER/schema-introspection kept raw with `// Why:` (HarvestRunStore:79/477, ContentSiteIndexStore:53/523/530/551); no CREATE/ALTER rewritten | closed |
| T-49-10 | Tampering | CategoryKnowledgeRepository params (SQLi regression) | mitigate | UPSERT/RETURNING/INSERT OR IGNORE verbatim + parameterized; 0 interpolation | closed |
| T-49-11 | Tampering | Transaction-scope correctness (missing `transaction:` arg) | mitigate | 11 `transaction: transaction` call sites; 0 leftover `command.Transaction=` in non-DDL; repo parity/dedup tests assert atomic behavior | closed |
| T-49-12 | Tampering | Carve-out drift (`RequestMetricsStore.UpsertBatchAsync` rewritten) | mitigate | Diff comment-only (3 `// Why:` lines, `c0b47f9`); unnest-array body unchanged | closed |
| T-49-13 | Info Disclosure | Type-handler malformed-value on repository reads (D-05 parity) | mitigate | DateTimeTypeHandler round-trip + write-path proof; fail-closed fallback; repository parity tests | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-49-01 | T-49-SC | Dapper 2.1.79 is the single new dependency; DapperLib official org, ~13yr maturity, 500M+ downloads; audited in 49-RESEARCH §Package Legitimacy; pinned exact version | luntc1972 (phase plan) | 2026-06-14 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-14 | 17 | 17 | 0 | Claude (orchestrator) + gsd-security-auditor (sonnet) |

Notes: build 0/0; Core.Tests 346/0; Web.Tests 622/0/11-skip (independently re-run). Postgres parity remains a documented MANUAL gate (`DECKFLOW_POSTGRES_TESTS=1`) — not run in this audit. Out-of-fence `CLAUDE.md` edit made during execution was reverted (`72136ba`).

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-14
