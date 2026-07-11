# Phase 90 — Deferred Follow-ups (Codex code-review)

Phase 90 shipped with the HIGH finding fully fixed. Three items were consciously
deferred. All are **safe** (no false publish, no data corruption) and become
relevant only after `sync.directpush-gitbody` is flipped **ON** in prod — which
does not happen until **Phase 93** (round-trip integration test) gates the flip.
These are the pre-flag-flip checklist for Phase 93.

## FU-1 (MED, deferred) — Updated-visible row shows stale-but-visible content during the deploy window

**Codex Finding 2 (original review).** For an already-visible prod row being
*updated* via DirectPush, the content-only upsert changes title/tags/artifact/
`body_sha256` while `is_visible` stays `true` (visibility is intentionally
excluded from the content-only upsert, `ContentSiteIndexStore.cs:1206`). Until
Render redeploys, the row serves updated metadata over the *old* deployed body,
and if the deploy fails it remains visible with stale content. The Stage-4 UI
copy that says rows stay "hidden + awaiting-confirm" is inaccurate for this
update-to-visible case.

- **Why deferred:** hiding a live row on every edit would cause a publish
  flicker/outage on each content update — arguably worse than a brief stale
  window. This is a design decision, not a clear bug.
- **Pre-flip action (P93):** decide explicitly — accept-by-design (and fix the
  Stage-4 copy to be accurate for updates), or hide-then-reconfirm on update.

## FU-2 (MED, deferred) — ON row can strand after a Stage-4 *indeterminate* flag read

**Codex re-review round 3.** If the Stage-4 flag read is indeterminate (prod
flag DB briefly unreachable) while prod is genuinely **ON**, `ReadFlagAsync`
fails closed to `false`, so the commit is pushed with `[skip render]` and **no
Render redeploy is triggered**. Stage 5 then reads `true`/`null`, correctly
declines the immediate path, polls `/app`, 404s (never redeployed), and leaves
the row awaiting-confirm. The resume path (`GetAwaitingConfirmRowsAsync` +
resume) only re-calls `VerifyAndPublishAsync` (poll) — it does **not** create a
fresh non-`[skip render]` commit or trigger a redeploy, so resume alone cannot
un-strand the row.

- **Safe:** no false publish; the row is operator-visible as awaiting-confirm.
- **Recoverable:** re-running the full DirectPush (Stage 4 flag read now
  succeeds → drops `[skip render]` → redeploys → Stage 5 confirms), or any later
  normal deploy that makes `/app` catch up.
- **Why deferred:** requires an indeterminate read *exactly* at Stage 4 while
  prod is ON (Phase 93+ regime); narrow, safe, and re-push-recoverable.
- **Pre-flip action (P93):** consider letting the resume/awaiting-confirm action
  re-trigger the git redeploy stage (drop `[skip render]` on resume) so a
  stranded ON row is self-recoverable without a full re-push.

## FU-3 (P91 91-09, deferred) — Live Studio-UI + prod-Postgres reconcile walk before flipping `sync.reconcile`

**Phase 91 operator gate (91-09).** The reconcile workflow's safety story was verified end-to-end
by an automated fixture driver (`ReconcileFixtureDriveTests`, commit `5cb654ae`): real orchestrator +
coordinator against a real SQLite prod stand-in + real git tree + real seed, proving all four classes
detect read-only, flag/stale Apply refusals, seed-owned-only soft-hide, and prod-owned-stays-visible.
The harness does **not** exercise (a) real Render Postgres prod, (b) the actual Studio `/reconcile`
Blazor page interactions, or (c) a real `sync.reconcile` flip in the live web DB.

- **Safe:** `sync.reconcile` ships **OFF**; the destructive Apply cannot run in prod until it is flipped.
- **Pre-flip action (before enabling `sync.reconcile` in prod):** run the `/reconcile` page once, live —
  dry-run against real prod (expect file-orphans in the hundreds order of magnitude per the 2026-07-05
  audit), review the readable D-06 report, confirm no prod write, then a scoped dry-run→Apply with the
  flag ON confirming only seed-owned rows soft-hide and a known prod-owned row stays visible.

## Closed this phase (for the record)

- **HIGH (original)** — flag-OFF DirectPush could never publish (verify flow not
  flag-gated). **Fixed** `6d570f1f` (flag-gate) + `9b047765` (fail-safe
  tri-state on indeterminate read).
- **MED (original)** — seed-only edit left `index-seed.json` uncommitted.
  **Fixed** `6d570f1f`.
