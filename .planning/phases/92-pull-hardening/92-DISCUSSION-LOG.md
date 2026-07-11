# Phase 92: Pull Hardening — Discussion Log

**Date:** 2026-07-10
*Human-reference audit trail. Not consumed by downstream agents — see 92-CONTEXT.md for the canonical decisions.*

## Gray areas presented

Four code-grounded gray areas surfaced from scouting `PullFromProdCoordinator`:

1. Staleness guard (SYNC-14) — warn vs refuse; auto-pull vs operator-pull-first; detection mechanism.
2. Divergence handling (SYNC-15) — body-hash vs prod `body_sha256` mismatch behavior.
3. Field authority (SYNC-13) — the per-field adopt master split.
4. Flag gating — `sync.*` flag vs always-on.

**User selected:** 2 (Divergence), 3 (Field authority), 4 (Flag gating).
**Not selected:** 1 (Staleness guard) → left to Claude's discretion / planner.

## Decisions

### Divergence handling (SYNC-15)
- **Options:** Block entry + require ack / Reuse P91 reconcile vocab / Warn + adopt anyway.
- **Selected:** **Block entry + require ack** (recommended). Divergent entries = distinct diff class, excluded from default adopt set, per-entry explicit operator opt-in. Kept on the single Pull page (rejected splitting onto the reconcile page). → D-01 / D-01a.

### Field authority (SYNC-13)
- **Options:** Ratify current split / Index columns ← git seed.
- **Selected:** **Ratify current split** (recommended). Body file ← git; index columns ← prod; approval ← prod-mirror (pull/adopt direction, no conflict with push-direction P90 D-03); `is_visible`/`is_hidden` preserved-local. → D-02 / D-02a.

### Flag gating
- **Options:** Always-on, no flag / Behind a `sync.*` flag.
- **Selected:** **Always-on, no flag** (recommended). Pull is LOCAL-only writes, no destructive-prod blast radius; guards are protective. → D-03.

## Claude's discretion
- SYNC-14 staleness guard mechanism + warn-vs-refuse left to planner/research. Preferred direction noted in CONTEXT: add a behind-detection `IGitRepository` seam (fetch + behind-count) and warn-then-proceed; never SFTP/prod.

## Deferred
- Auto-`git pull` remediation; any prod-side write / SFTP body fetch; merging Pull divergence into the P91 reconcile page.
