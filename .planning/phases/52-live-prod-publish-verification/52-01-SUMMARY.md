# 52-01 Summary — Live prod-publish verification (HARD-02)

**Status:** PASS · **Date:** 2026-06-17

Exercised the P47 DirectPush prod-publish path live end-to-end against prod Postgres `deckflow`
(`dpg-d7oj8iugvqtc73fso0g0-a`) + Render `/data`. Operator ran the publish (Studio DirectPush:
Compute Diff new=8/updated=0 → SCP artifacts → content-columns-only upsert); AI did read-only
before/after snapshots via Render MCP + a read-only SSH `ls` of `/data`. No AI prod write.

**HARD-02 guard PASS:** admin_fingerprint over the 86 pre-existing rows is byte-identical
before/after (`6074848f…`) — every is_visible/is_evergreen/is_hidden/approval_status preserved;
content_fingerprint also identical (updated=0). 8 new rows inserted (94 total), all with
`approval_status=pending` / `is_visible=false` — proving the upsert writes content but never admin
fields, even on insert. All 8 artifacts confirmed on `/data/content-kb/…` (timestamped today).

Caveat: updated=0, so the live UPDATE-preserves-admin path wasn't hit on an overlapping row;
covered by P47 automated tests. No product code changed. No secrets in artifacts.

Security follow-up still owed (operator): redeploy web onto new cred `deckflow_28g4_user`,
then delete the leaked `deckflow_admin` credential. Full detail: `52-PROD-PUBLISH-RESULTS.md`.
