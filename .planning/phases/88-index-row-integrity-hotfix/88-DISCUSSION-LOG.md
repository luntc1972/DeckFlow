# Phase 88: Index-Row Integrity Hotfix - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 88-index-row-integrity-hotfix
**Areas discussed:** Approved-write mechanics, Composite-key fix shape, DDL guard design, Prod verify & rollout

---

## Approved-write mechanics

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror local row value | Upsert writes row.ApprovalStatus from the local row; 'approved' today, stays correct if the read filter changes | ✓ |
| Hardcode 'approved' | Literal 'approved' on insert + update; simplest, silently wrong for future non-approved callers | |
| You decide | Claude picks at plan time | |

**User's choice:** Mirror local row value

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, overwrite on update | Insert AND update write mirrored approval; re-push heals drifted pending prod rows | ✓ |
| Insert only | Existing drifted rows stay pending until backfill | |
| You decide | Confirm no prod-side approval mutation path first | |

**User's choice:** Yes, overwrite on update

| Option | Description | Selected |
|--------|-------------|----------|
| One-time SQL backfill, operator-run | Idempotent UPDATE (visible+pending → approved) run by operator | |
| Let re-push heal them | Drift heals only on next DirectPush | |
| Defer to Phase 91 reconciler | Reconciler detects + fixes as discrepancies | ✓ |

**User's choice:** Defer to Phase 91 reconciler
**Notes:** D-04 serve-side filter neutralizes the drifted rows in the meantime.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add serve-side filter | Public KB query gains approval filter; stops serving drifted rows immediately | ✓ |
| No, writer-side fix only | Drifted rows keep serving until Phase 91 | |
| You decide | Check public query shape at plan time | |

**User's choice:** Yes, add serve-side filter

---

## Composite-key fix shape

| Option | Description | Selected |
|--------|-------------|----------|
| Extract shared helper | One natural-key helper in Core used by classifier AND DirectPushCoordinator | ✓ |
| Local classifier fix only | Two hand-rolled keying implementations remain | |
| You decide | | |

**User's choice:** Extract shared helper

| Option | Description | Selected |
|--------|-------------|----------|
| Stored columns | row.NaturalKeyType/NaturalKeyValue — DB-enforced UNIQUE key | ✓ |
| Keep derived heuristic | 'youtube'/'podcast' derivation persists | |
| You decide | | |

**User's choice:** Stored columns

| Option | Description | Selected |
|--------|-------------|----------|
| Stored vocabulary end-to-end | SyncDiffEntry emits 'youtube_channel'/'podcast_rss'; consumers updated in-phase | ✓ |
| Map at the edge | Two vocabularies persist | |
| You decide | | |

**User's choice:** Stored vocabulary end-to-end

| Option | Description | Selected |
|--------|-------------|----------|
| Skip + log warning | Row excluded, structured log names it | ✓ |
| Skip silently (current) | Invisible corruption | |
| Throw | One corrupt row blocks all sync ops | |

**User's choice:** Skip + log warning

---

## DDL guard design

| Option | Description | Selected |
|--------|-------------|----------|
| Schema-ensure OFF switch on store | Ctor flag / factory option; prod stores built with it off | ✓ |
| Split reads from EnsureSchema | Changes behavior for all callers incl. local | |
| Pre-flight ensure once | Larger store-contract refactor | |

**User's choice:** Schema-ensure OFF switch on store

| Option | Description | Selected |
|--------|-------------|----------|
| All prod stores, always | Studio never runs DDL against prod, reads or writes | ✓ |
| Diff-read only | Literal SYNC-06 reading; writes keep auto-DDL | |
| You decide | | |

**User's choice:** All prod stores, always

| Option | Description | Selected |
|--------|-------------|----------|
| Regression test on SQL text | Recording connection asserts no CREATE/ALTER/DROP | ✓ |
| Test + runtime throw | Adds InvalidOperationException on EnsureSchema in prod mode | |
| Also revoke DDL on prod login | DB-layer defense, operator task | |

**User's choice:** Regression test on SQL text
**Notes:** DDL-rights revocation noted as possible later operator hardening (deferred).

| Option | Description | Selected |
|--------|-------------|----------|
| Sweep all sync-path claims | Every no-DDL / H3-style claim made true or deleted | ✓ |
| Fix the one cited comment | Only ComputeDiffAsync doc corrected | |

**User's choice:** Sweep all sync-path claims

---

## Prod verify & rollout

| Option | Description | Selected |
|--------|-------------|----------|
| Unflagged | Correctness fixes have no legitimate 'off' state | ✓ |
| Flag the serve-side filter only | Kill-switch on the only public-surface change | |
| Flag everything | One sync.integrity-hotfix flag | |

**User's choice:** Unflagged

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-audit + post-deploy re-check | Read-only Render MCP query before merge + after autodeploy | ✓ |
| Post-deploy check only | | |
| Pre-audit only | | |

**User's choice:** Pre-audit + post-deploy re-check

| Option | Description | Selected |
|--------|-------------|----------|
| Unit + e2e on public page | xUnit set + Playwright e2e (KB page never renders pending row) | ✓ |
| Unit tests only | | |
| You decide | | |

**User's choice:** Unit + e2e on public page

| Option | Description | Selected |
|--------|-------------|----------|
| Early ff to main | Fold Phase 88 to local main linear; operator pushes → autodeploy now | ✓ |
| Ride cycle squash | Ships at 2026.07.3 | |
| Decide at verify time | Choose after seeing pre-audit row count | |

**User's choice:** Early ff to main

---

## Claude's Discretion

- Exact shape of the schema-ensure switch (ctor flag vs factory option vs subclass)
- Home + naming of the shared natural-key helper
- Recording-connection test mechanics

## Deferred Ideas

- One-time backfill of drifted visible-while-pending prod rows → Phase 91 reconciler
- Revoke DDL rights on Studio prod DB login → possible operator hardening task later
