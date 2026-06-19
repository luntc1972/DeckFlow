# Security Audit — Phase 58 Dogfood

**Phase:** 58 — Dogfood (Cycle-9 Content-KB pipeline validation + SC2 DirectPush fix)
**Auditor:** gsd-security-auditor (claude-sonnet-4-6)
**Audit date:** 2026-06-19
**ASVS Level:** 1
**Commit audited:** HEAD (`4cb333e` SC2 fix merged into `cycle9`)

---

## Overall Result: SECURED

**Threats Closed:** 9/9
**OPEN Threats:** 0
**Unregistered Flags:** 0

---

## Threat Verification

### Plan-time register (T-58-01 through T-58-08) — dogfood validation

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-58-01 | Tampering — verification SQL | mitigate | CLOSED | `.planning/phases/58-dogfood/58-VERIFICATION-QUERIES.md:3` states "All SQL here is SELECT-only"; grep for INSERT/UPDATE/DELETE/DDL in that file returns zero matches. Only SELECT statements present. |
| T-58-02 | Info disclosure — secrets in evidence doc | mitigate | CLOSED | `58-DOGFOOD-RESULTS.md` contains no connection strings, passwords, or key values; grep for "connection string", "ProdConnectionString", "password", "credential" in the evidence doc returns only the advisory instruction "never connection strings or secrets" (`58-DOGFOOD-RESULTS.md:6`). |
| T-58-03 | Tampering — prompt injection via transcript | mitigate | CLOSED | `58-DOGFOOD-RESULTS.md:52` records the operator's higher-quality verdict against the named baseline entry; the operator-inspect gate (SC1) was exercised on the real run. Distill tool access is structurally absent (no file/tool access in the distill slice — plan design constraint, not code). |
| T-58-04 | DoS — uncapped LLM spend | mitigate | CLOSED | `DECKFLOW_LLM_MONTHLY_CAP_USD` confirmed present at `DeckFlow.Core/Content/LlmSpendLedger.cs:36`; `58-DOGFOOD-RESULTS.md` SC3 (`line 85-89`) records cap=$15.00 default, run spend=$0.00 metered, within cap. |
| T-58-05 | Elevation/Tampering — AI writes prod | mitigate | CLOSED | `58-02-PLAN.md` Tasks 1/2/3 carry `type="checkpoint:human-action"` and `type="checkpoint:human-verify"` with explicit "OPERATOR-ONLY" guards (`58-02-PLAN.md:76,129,167`). `58-DOGFOOD-RESULTS.md:59` confirms "AI never wrote prod; all AI prod reads were SELECT-only via the Render read-only MCP." |
| T-58-06 | Info disclosure — prod secrets | mitigate | CLOSED | `StudioConfig.cs:7` is a presence-only record (booleans only, no string values). `DirectPush.razor:397-400` injects `IConfiguration` ephemerally; `rawConnStr` is consumed only inside `Task.Run` and never stored in DI state or surfaced in the UI. `58-DOGFOOD-RESULTS.md` contains no connection string or SCP key values. |
| T-58-07 | Tampering — no-regression query | mitigate | CLOSED | `58-DOGFOOD-RESULTS.md` SC4 (`line 96-108`) records the before/after corpus diff (108→109 rows additive); evidence confirms read-only Render MCP was the only AI-visible prod access. No write SQL in the verification doc. |
| T-58-08 | Repudiation — false PASS verdict | mitigate | CLOSED | `58-DOGFOOD-RESULTS.md:12-24` contains an "Overall Verdict" section. `grep -c "FILL AT RUN"` returns 1, which is the single occurrence in the introductory description prose (line 4), not an unfilled evidence slot. All four SC rows contain concrete values. Verdict is PASS with one named integration gap found-and-fixed, not a silent pass. |

---

### Post-plan code change — T-58-09 (commit `4cb333e`)

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-58-09 | Tampering/Elevation — DirectPush keyed SetVisibilityAsync prod write | assess | CLOSED — see analysis below |

#### T-58-09 Detail Assessment

**New attack surface:** `DirectPush.razor:WriteRowsAsync` now calls `prodStore.SetVisibilityAsync(keys, true)` and `IndexStore.SetVisibilityAsync(keys, true)` after a successful upsert batch. These are UPDATE writes to both the prod Postgres and the local SQLite, keyed by natural key pairs (`natural_key_type`, `natural_key_value`).

**SQL injection — CLOSED.**
The implementation in `ContentSiteIndexStore.cs:628-664` uses Dapper's `CommandDefinition` with a static SQL string containing `@visible`, `@type`, `@value` named parameters and an anonymous object `new { visible, type, value }`. No string interpolation or concatenation in the SQL. Both `@type` and `@value` come from `ContentSiteIndexRow.YoutubeVideoId` / `RssGuid` — values originally ingested and stored under the `natural_key_type IN ('youtube_channel','podcast_rss')` CHECK constraint (`ContentSiteIndexStore.cs:942,968`). The `@visible` parameter is a `bool` literal (`true`), never user-supplied.

**Error message sanitization — CLOSED.**
All three catch paths in `WriteRowsAsync` use bare `catch (Exception)` with no exception variable captured (`DirectPush.razor:698,702,759`). Error messages shown to the operator are static string literals ("Prod upsert failed for this row — see logs.", "Database write failed — check the prod connection configuration and try again."). Comments at lines 574, 653, 705, and 768 explicitly name the risk ("an Npgsql exception can carry host/database/user — NEVER surface ex.Message"). Confirmed: no `ex.Message` or `exception.Message` usage in the file.

**Operator gate (prod write stays operator-gated) — CLOSED.**
`WriteRowsAsync` opens with a hard guard (`DirectPush.razor:668`): `if (!_scpSuccess || _operationInFlight || !_diffReady) { return; }`. This guard is enforced in code, independent of the disabled-button UI state. The Stage 3 button is additionally disabled unless `_scpSuccess` is true (`DirectPush.razor:311`). The method is only callable from the UI via an operator click or the `InvokeWriteRowsForTest()` internal test seam — no anonymous/unauthenticated HTTP endpoint exposes it (Studio is a locally-run Blazor WASM app under `ASPNETCORE_ENVIRONMENT` without network exposure to prod). The page requires `IConfiguration["Studio:ProdConnectionString"]` (user-secrets) to create a `prodStore`; without it `rawConnStr` is empty and `ProdStoreFactory.Create(string.Empty)` produces a non-functional store. AI agents have no mechanism to invoke `WriteRowsAsync` — they cannot drive the Blazor circuit.

**Prod-then-local write ordering (local never over-reports prod) — CLOSED.**
Order at `DirectPush.razor:735-738` is: (1) `prodStore.StampPushedToProdAsync`, (2) `prodStore.SetVisibilityAsync`, (3) `IndexStore.StampPushedToProdAsync`, (4) `IndexStore.SetVisibilityAsync`. If (1) or (2) throws, the local store is never advanced, so the local badge cannot derive `Published` while prod is not yet written — the PUB-01 / HIGH-3 invariant is maintained.

**No new secret exposure — CLOSED.**
`rawConnStr` is materialized from `IConfiguration` inside `Task.Run` at lines 683-684 and used only to construct the `prodStore` transient object. It is never assigned to a field, logged, or included in any error string. `StudioConfig` is a presence-only boolean record (`StudioConfig.cs:7`). No connection string value appears in the rendered HTML or in any variable with DI lifetime.

---

## Unregistered Flags

None. No `SUMMARY.md` was produced for this manual run; no new attack-surface flags were raised by the executor outside the plan register. T-58-09 was pre-registered by the caller and fully assessed above.

---

## Accepted Risks Log

None. All threats are mitigated, not accepted.

---

## Notes

1. **SC3 spend measurement caveat (informational, not a threat):** The subscription claude CLI logs `spend_usd=0.000000` because it is not a metered provider. The $15 cap enforcement in `LlmSpendLedger` is correct for metered providers but does not meter subscription usage. This is a known design property documented in `58-DOGFOOD-RESULTS.md` SC3 — it is not a security gap, merely a spend-visibility limitation.

2. **One LOW accepted-by-design (T-58-09 related):** Codex review surfaced that a DirectPush re-push will un-hide a row that an admin explicitly hid. This was accepted by the operator as a deliberate design decision ("publish-visible"). It is logged here for traceability; the accepted-risk owner is the operator, not the AI.
