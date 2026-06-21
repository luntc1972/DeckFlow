# Phase 60 — Pull-from-Prod Reconcile: Live Operator Verification (60-04)

**Date:** 2026-06-21
**Operator:** Chris Lunt (Windows, self-contained Studio exe `DeckFlowStudio-2026.06.21`, HEAD `44778575`)
**Result:** PASS

## Summary

The operator ran the real Studio "Pull from Prod" against live Render prod and resolved entries
locally. The feature works end-to-end. One connection blocker and two app bugs were found and fixed
during verification; a separate pre-existing prod-data gap was discovered and backlogged.

## Success criteria

| SC | Criterion | Result |
|----|-----------|--------|
| SC1 | Pull connects to prod, reads `content_site_index`, renders per-entry diff | PASS — 109 prod rows pulled, Stage-1 diff table rendered (all "Missing locally" — local store had drifted) |
| SC2 | Artifacts download into `pull-staging` | PASS (partial by prod data) — files that exist on prod `/data` download (23 rows under 3 creators); the 86 rows whose artifacts are missing on prod are correctly shown "not downloaded / adopt updates row only" |
| SC3 | Operator resolves ≥1 adopt-prod AND ≥1 keep-local; LOCAL store changes | PASS — adopt + keep-local applied with no failures; local `content-kb.db` written (08:37) |
| SC4 (R5) | Production is NOT modified (read-only proof) | PASS — `ProdContentReader` is SELECT-only, `SftpArtifactDownloader` never writes remote; UI banner "Production was not modified"; prod stays 109 rows |

## Blocker + fixes applied during verification

1. **Connection blocker (root cause): stale Render IP allowlist.** Prod connect failed with
   `EndOfStreamException` after TLS — Render's proxy terminates TLS then drops a source IP not on the
   DB allowlist. The operator's home ISP had issued a new IP after 2026-06-17. Proven not-SSL via
   OpenSSL + raw .NET SslStream (TLS handshakes fine) and a raw Postgres StartupMessage probe
   (EOF post-TLS). Fixed by adding `24.10.192.114/32` to the Render Access Control allowlist. Verified
   end-to-end (Npgsql connects, row count 109).
2. **`fix(60-02)` `37b1940e`** — force `SslMode=Require` in `ProdContentReader`, drop obsolete
   (no-op in Npgsql 10) `TrustServerCertificate`. Valid hardening; not the root cause.
3. **`fix(60-03)` `d004ca6c`** — per-entry apply catch now logs the full exception server-side so the
   "see logs" note is truthful.
4. **`fix(60-03)` `44778575`** — adopt's artifact `File.Move(staged→live)` threw when the staged
   source was absent (already-promoted / already-present locally), wrongly failing the whole entry even
   though the row upsert had succeeded. Promotion is now tolerant: moves when staged exists, treats
   "already present locally" as success, logs+skips when neither exists, and never fails the row on a
   promotion miss. Studio test suite: PullFromProd 11/11; full suite 79/80 (1 pre-existing BlockedPage
   parallel-isolation flake, passes in isolation, unrelated to this change).

## Separate finding (backlogged, NOT a phase-60 defect)

Prod `content_site_index` has 109 rows but Render `/data/content-kb` holds artifacts for only 3
creators (23 rows); 86 rows reference `.md` files missing from the prod disk. Captured as a
high-priority ROADMAP backlog item (`docs(backlog)` `fa0431cb`).

## Disposition

Phase 60 (SYNC-01/02/03) is functionally complete and operator-verified. Optional follow-up:
`/gsd-secure-phase 60`. Diagnostic probes used during verification were throwaway and removed.
