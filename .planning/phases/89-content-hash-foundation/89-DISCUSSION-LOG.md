# Phase 89: Content-Hash Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 89-content-hash-foundation
**Areas discussed:** Hash input scope, Unified signature shape, Render-guard behavior, Backfill/rollout, Body normalization, Prod DDL path

---

## Hash input scope

| Option | Description | Selected |
|--------|-------------|----------|
| Body-only post-SplitHeader | Hash body after frontmatter split — same SplitHeader render already runs; targets mojibake; header already in column signature | ✓ |
| Full raw .md bytes | Hash entire file incl. YAML frontmatter; simplest but header edits double-counted vs column signature | |

**User's choice:** Body-only post-SplitHeader
**Notes:** Publish-compute and render-guard must call the identical SplitHeader so the two hashes are comparable. → CONTEXT D-01.

---

## Unified signature shape

| Option | Description | Selected |
|--------|-------------|----------|
| Extend column-sig + body hash | Canonical sig = full ContentSiteIndexContentSignature column set + appended body_sha256; classifier subset Fingerprint retired; keep indexed_utc direction logic | ✓ |
| Fresh minimal signature | Retire BOTH old schemes; new sig = natural key + body_sha256 + minimal core columns | |

**User's choice:** Extend column-sig + body hash
**Notes:** Body hash becomes the equal-timestamp tie-breaker replacing the old Fingerprint compare; UTC direction logic (F-51-PG-01 guard) preserved. → CONTEXT D-03/D-04.

---

## Render-guard behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Fail-open + log both | On mismatch OR missing-hash: serve anyway + structured warning/metric. No flag (D-13 stance) | ✓ |
| Closed on mismatch, open on legacy | Hard-refuse present-but-mismatched; fail-open when hash absent; warrants a flag | |
| Fail-closed both | Refuse any row not hash-verified; needs full backfill first + flag | |

**User's choice:** Fail-open + log both
**Notes:** Zero risk of vanishing content during rollout; fail-closed tightening deferred to a later phase once backfill guarantees coverage. Guard applies only at the detail render (body is read there). → CONTEXT D-05/D-06/D-07.

---

## Backfill / rollout

| Option | Description | Selected |
|--------|-------------|----------|
| One-time backfill at seed/publish | Compute hashes for all existing .md in a deterministic pass (web startup/seed for prod, Studio/publish for local); unlocks future fail-closed | ✓ |
| Lazy on next publish | Only newly published rows get a hash; render must fail-open indefinitely; drift-detection blind on old rows | |

**User's choice:** One-time backfill at seed/publish
**Notes:** Every row hashed up front is the precondition that makes a later fail-closed guard safe. → CONTEXT D-08.

---

## Body normalization (derived, correctness-critical)

| Option | Description | Selected |
|--------|-------------|----------|
| Normalize to LF UTF-8 | Decode UTF-8, normalize EOL to LF, then SHA-256; prevents CRLF/LF + encoding false-mismatches across git-tree/overlay/OS | ✓ |
| Hash raw bytes as-is | SHA-256 exact on-disk bytes; strictest fidelity but EOL/encoding variance trips false mismatch | |

**User's choice:** Normalize to LF UTF-8
**Notes:** Motivated by CP437 mojibake incident + .gitattributes LF enforcement. → CONTEXT D-02.

---

## Prod DDL rollout path (derived, carried constraint)

| Option | Description | Selected |
|--------|-------------|----------|
| Web startup EnsureSchema only | Dialect-guarded idempotent ALTER rides web startup/seed; Studio prod stores stay schema-ensure OFF (P88 D-10); seed JSON gains field | ✓ |
| Revisit — discuss further | Flag as needing more thought (e.g. dedicated migration step) | |

**User's choice:** Web startup EnsureSchema only
**Notes:** Confirms P88 D-10 continuity — web app owns prod schema, Studio never DDLs prod. → CONTEXT D-09.

---

## Claude's Discretion

- Home/name of the shared body-hash helper (prefer folding into `ContentSiteIndexContentSignature`).
- Whether the render guard emits a metric/counter alongside the log.
- Backfill mechanics (UPDATE-where-null vs recompute-all) — smaller safe-on-re-run option.
- Whether local backfill piggybacks an existing publish path or is a discrete command.

## Deferred Ideas

- Fail-closed render guard — later phase, flag-gated, unlocked by D-08 backfill.
- Reconciler body-hash-mismatch discrepancy type — SYNC-11, Phase 91.
- End-to-end body_sha256 integration test — SYNC-16, Phase 93.
