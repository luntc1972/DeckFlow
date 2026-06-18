# Phase 52 — Live Prod-Publish Results (HARD-02)

**Recorded:** 2026-06-17
**Plan:** 52-01
**Requirement:** HARD-02
**Target:** prod Postgres `deckflow` (`dpg-d7oj8iugvqtc73fso0g0-a`, Oregon) + Render `/data` disk
**Method:** AI does READ-ONLY snapshots via Render MCP `query_render_postgres`; the live publish (SCP + content-columns-only upsert) is OPERATOR-run via Studio DirectPush. No prod write by the AI. No secret values recorded.

## Studio configured
- `Studio prod connection: configured` + `Studio SCP: configured` (boot log, presence-only).
- Conn string supplied via global env var `Studio__ProdConnectionString` (User scope), using the NEW prod credential `deckflow_28g4_user` (rotation from the leaked `deckflow_admin` — see Security note).

## BEFORE snapshot (read-only, 2026-06-17)
`content_site_index` — 86 rows.

| Metric | Value |
|--------|-------|
| total rows | 86 |
| is_visible = true | 24 |
| is_evergreen = true | 0 |
| is_hidden = true | 42 |
| approval_status = 'approved' | 24 |
| **admin_fingerprint** (md5 over `natural_key_value|is_visible|is_evergreen|is_hidden|approval_status` for all rows, ordered) | `6074848f279dbcc76452f498d609d3ed` |
| content_fingerprint (md5 over `natural_key_value|title|artifact_path|card_category_tags`) | `00fd30f39bc5406fc700ef86aaca97d0` |

The 86 pre-existing `natural_key_value`s were captured this session; the AFTER admin check is scoped to exactly those keys (so newly-inserted rows from the publish don't mask a preservation regression).

**HARD-02 guard:** AFTER the publish, the admin_fingerprint recomputed over the SAME 86 keys must equal `6074848f279dbcc76452f498d609d3ed` (every pre-existing row's is_visible / is_evergreen / is_hidden / approval_status unchanged), while content_fingerprint may change for re-published rows.

## Live publish (operator) — DONE
Operator ran Studio DirectPush against prod: Stage 1 Compute Prod Diff = **new 8 / updated 0** (the 8 local approved rows had no natural-key overlap with the prod 86, so all inserts, zero updates); Stage 2 SCP artifacts → `/data`; Stage 3 content-columns-only Postgres upsert. AI performed no prod write.

## AFTER snapshot + assertion — PASS
Read-only, post-publish:

| Check | BEFORE | AFTER | Verdict |
|-------|--------|-------|---------|
| admin_fingerprint over the 86 pre-existing keys | `6074848f279dbcc76452f498d609d3ed` | `6074848f279dbcc76452f498d609d3ed` | **IDENTICAL** — every pre-existing row's is_visible/is_evergreen/is_hidden/approval_status preserved |
| content_fingerprint over the 86 | `00fd30f39bc5406fc700ef86aaca97d0` | `00fd30f39bc5406fc700ef86aaca97d0` | IDENTICAL — pre-existing content untouched (updated=0) |
| pre-existing rows still present | 86 | 86 | preserved |
| visible / evergreen / hidden / approved (whole table) | 24 / 0 / 42 / 24 | 24 / 0 / 42 / 24 | unchanged |
| total rows | 86 | 94 | +8 new inserted |
| new rows' admin state | — | all 8: `is_visible=false`, `is_hidden=false`, `approval_status='pending'` | **admin fields NOT written by the upsert** even on insert — defaults applied; prod admin keeps control |

The content-columns-only contract is proven: the upsert wrote content + artifact_path for the 8 new rows but left every admin field (the 86 existing AND the 8 new) at its prior/default value.

## Render /data + page smoke — PASS
Read-only SSH `ls /data/content-kb/` confirms all 8 artifacts landed (timestamped today): 7 under `salubrioussnail/` (Din4kwnOyVI, GpLNTVF1UqY, JVophcFxxmI, LbWhyElEbLg, PnxdctuFTQ0, YXpd-vcVv24, vJ78fos7nGQ `.md`) + `the-command-zone/6oS1E5BGi0U.md` (4273 bytes). Public KB page smoke n/a — the 8 new rows are `is_visible=false` so they don't render publicly until the prod admin publishes them.

## Outcome — PASS (HARD-02 satisfied)
The direct prod-publish path (SCP → `/data` + content-columns-only Postgres upsert) ran live end-to-end. **Admin fields preserved**: the 86 pre-existing rows are byte-identical (admin + content fingerprints), and the 8 inserted rows carry default admin state (the upsert never writes admin columns).

**Caveat (honest):** with updated=0, the live UPDATE-preserves-admin path wasn't exercised on a real overlapping row (no local approved natural key matched the prod 86). The INSERT path proved admin-fields-are-never-written; the UPDATE-preservation path is covered by the P47 automated tests (`UpsertContentColumnsOnlyAsync`). HARD-02 is satisfied via the live insert run + the no-collateral-damage proof.

**No secret values appear in this file** (grep: no `Host=`/`Password=`/`postgres://`).

## Security follow-up (operator, still owed)
Rotation underway: new default credential `deckflow_28g4_user` created + in use by Studio. STILL TO DO: redeploy the `DeckFlow` web service onto the new default credential, confirm site 200 + `deckflow_admin` open-connections → 0, then DELETE `deckflow_admin` to kill the leaked password.

## Security note
The original prod credential `deckflow_admin` password was exposed in-session and is being rotated: a new default credential `deckflow_28g4_user` was created and is now in use. Cleanup still owed by operator: redeploy the `DeckFlow` web service onto the new default credential, confirm site 200 + `deckflow_admin` open-connections → 0, then DELETE `deckflow_admin` to kill the leaked password.
