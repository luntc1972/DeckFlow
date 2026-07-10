# 91-09 SUMMARY — Operator verification of the reconcile workflow

**Plan:** 91-09 (wave 7, `autonomous: false` — human-verify gate)
**Requirements:** SYNC-11, SYNC-12
**Outcome:** APPROVED (operator sign-off 2026-07-09) — closed on the automated fixture driver; the
genuinely-live UI/prod-Postgres walk is deferred to the pre-flag-flip gate (`sync.reconcile` ships OFF).

## How it was verified

Rather than a hand walk-through, the two blocking checkpoints were driven by an end-to-end
integration harness — `DeckFlow.Studio.Tests/ViewModels/ReconcileFixtureDriveTests.cs`
(`test(91-09)` commit `5cb654ae`). It exercises the **real** `ContentKbReconcileOrchestrator` and
`ReconcileCoordinator` against an on-disk fixture:

- a real SQLite `ContentSiteIndexStore` standing in for prod, seeded with one row per discrepancy
  class plus a prod-owned (`seed_managed=false`) control,
- a real `content-kb/**` git-style body tree,
- a real `content-kb/seed/index-seed.json`,
- the real local `ContentKbReconcileStore`.

Only the Postgres transport is faked (`IProdContentReader` + `IProdStoreFactory` point at the SQLite
fixture; there is no local test Postgres and the harness never touches real prod). Everything else —
the git-tree file walk, seed parse, pure classifier, local-store persistence, D-06 report writer, the
flag gate, the stale-check, and the ownership-scoped `HideSeedManagedAsync` soft-hide — runs unchanged.

## Checkpoint 1 — dry-run detection (read-only, flag OFF) — PASS

- `SeedAvailable = true`; all four classes detected with exactly the seeded counts:
  PublishedOrphan 1, FileOrphan 1, SeedDrift 1, BodyHashMismatch 1.
- The D-06 report file was written under the checkout (`content-kb/reconcile-report.md`) with all four
  sections present.
- No prod write occurred: every seeded row remained visible after the dry-run (asserted).
- The dry-run ran with `sync.reconcile` OFF — detection is flag-independent.

## Checkpoint 2 — gated, re-validated Apply + prod-owned safety — PASS

- Apply with the flag **OFF** → refused (`FlagNotEnabled`); the seed-drift row stayed visible.
- Apply with the flag **null / indeterminate** → refused (`FlagNotEnabled`); row stayed visible.
- Apply with a **stale** reviewed set (flag ON) → refused (`StaleReviewSet`); row stayed visible.
- Apply with a **matching** reviewed set (flag ON) → applied, `HiddenCount = 1`: only the seed-owned
  (`seed_managed=true`) seed-drift row was soft-hidden (`is_visible=false`, row RETAINED, not deleted).
- The prod-owned control (`seed_managed=false`, absent from the seed) and the untargeted
  published-orphan / body-hash-mismatch rows all remained VISIBLE — the SYNC-17 invariant holds live
  through the real coordinator + real store path (backed by the ownership-predicated
  `HideSeedManagedAsync` SQL from the Codex-91-08 TOCTOU fix).

Full-suite gate: build 0/0; Core 1204, Web (reconcile/flag/contentkb) 141, Studio 386/390 (4 PG-skip)
all green.

## Scope / residual (pre-flag-flip gate)

The harness covers the substantive logic against SQLite + a faked Postgres transport with no Blazor
render. It does NOT exercise: (a) real Render Postgres prod, (b) the actual Studio `/reconcile` Blazor
page interactions, or (c) a real `sync.reconcile` flip in the live web DB. Because `sync.reconcile`
ships OFF, these are recorded as a **pre-flag-flip operator gate** in `90-FOLLOWUPS.md` (FU-3) — to be
walked once, live, before the flag is ever enabled in prod.
