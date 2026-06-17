---
phase: 45
slug: harvest-distill-ui
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-15
---

# Phase 45 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

**Audit date:** 2026-06-15
**Phase:** 45 — Harvest + Distill UI (v1.7 Blazor Studio)
**Auditor:** gsd-security-auditor (sonnet)
**block_on:** high
**Result:** SECURED — 17 `mitigate` threats verified CLOSED against implemented code; 4 `accept` threats confirmed documented. `register_authored_at_plan_time: true` — mitigations verified, no retroactive scan.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Operator browser → Studio app | Single click can incur real LLM spend / raise the cap; gate (dry-run + confirm + cap) sits here | Distill requests, cap-override numeric input |
| Provider decision → distiller + spend flag | One `DECKFLOW_LLM_PROVIDER` read must drive BOTH the distiller and `IsSubscriptionProvider`; a mismatch is the HIGH-1 silent-spend bug | Provider env value |
| Studio config → ledger / orchestrator | The single shared `LlmSpendLedger` singleton is the only cap source; a duplicate would bypass the override | Cap value, spend ledger |
| Operator paste input → YoutubeExplode | Channel/video URL/ID strings are untrusted input parsed before any HTTP call | Untrusted URLs/IDs |
| Blazor circuit → background `Task.Run` | Long-running IO crosses off the SignalR sync context; cancellation + render marshalling must follow circuit lifecycle | Progress callbacks, CTS |
| UI re-distill confirm → orchestrator | Double-confirm decides `redistill=true`; orchestrator honors it ONLY for targeted videos, never blanket | Re-distill flag, target video IDs |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-45-01 | Elevation of Privilege | Cap getter (`GetMonthlyCapUsd`) | mitigate | Read-only; delegates to `ReadMonthlyCapUsd()` (`SpendLedgerBase.cs:136`); no write path | closed |
| T-45-02 | Tampering | `WouldExceedCapAsync` cap source | mitigate | Cap read via `ReadMonthlyCapUsd()` actually drives the decision (`SpendLedgerBase.cs:125`); 9/9 ledger tests pass | closed |
| T-45-15 | Elevation of Privilege | redistill force flag | mitigate | `if (redistill && requestedKeys is not null && requestedKeys.Contains(naturalKey))` (`ContentKbOrchestrator.cs:313`); non-targeted distilled videos `continue` (325–330) — never blanket | closed |
| T-45-16 | Tampering | redistill overwriting prior output | mitigate | `ClearDistillOutputAsync` before re-distill (`ContentKbOrchestrator.cs:322`); gated behind `_redistillCheck1 && _redistillCheck2` double-confirm (`Harvest.razor:393`) | closed |
| T-45-04 | Tampering | Duplicate `ILlmSpendLedger` instances | mitigate | Single `new LlmSpendLedger(...)` registration; singleton + override captured in resolver closure, shared with orchestrator (`Program.cs:67–73`) | closed |
| T-45-17 | Spoofing / Repudiation | Distiller / spend-flag mismatch (HIGH-1) | mitigate | One `providerEnv` read (`Program.cs:57–58`) drives both `LlmDistillationProviderFactory.Resolve(providerEnv, …)` (80) and `StudioDistillConfig(isSubscriptionProvider)` (81); no hardcoded `new LlmDistillationService` at the registration site | closed |
| T-45-05 | Information Disclosure | Provider/cap in logs | mitigate | New logging emits only "configured / not configured" (`Program.cs:109`); no provider/cap/connection-string values | closed |
| T-45-06 | Tampering | Paste-queue ID validation | mitigate | IDs resolved via `GetByIdsAsync` (`VideoId.TryParse`); `ArgumentException` caught → `_queueAddError`, no crash (`Harvest.razor:917–920`) | closed |
| T-45-08 | Denial of Service | Circuit blocking / AngleSharp concurrency | mitigate | All IO in `Task.Run`; single `_operationInFlight`; no `Task.WhenAll` over lister; `_cts` cancelled+disposed on `Dispose()` (`Harvest.razor:1472–1473`) | closed |
| T-45-18 | DoS / Crash | Post-Dispose progress callback | mitigate | All three sinks marshal `_logLines.Add` + `StateHasChanged` through `InvokeAsync`, swallowing `ObjectDisposedException` / `InvalidOperationException` (`Harvest.razor:1097–1107, 1266–1276, 1356–1366`) | closed |
| T-45-09 | Information Disclosure | Page + log output | mitigate | Page renders only video metadata + progress text; no connection string, provider, or ledger key | closed |
| T-45-10 | Tampering | Silent re-distill of distilled videos | mitigate | `redistillConfirmed = _redistillCheck1 && _redistillCheck2` (`Harvest.razor:393`); enforced at `distillIds` build and `redistill:` named arg on both stages (1289, 1378) | closed |
| T-45-11 | Elevation of Privilege | Unbounded LLM spend | mitigate | Stage B requires successful dry-run + `_distillSpendConfirmed` checkbox + cap gate (`Harvest.razor:552, 555–556, 596`); orchestrator independently enforces `WouldExceedCapAsync` (`ContentKbOrchestrator.cs:346`) | closed |
| T-45-13 | Spoofing / Repudiation | Metered distill shown as success | mitigate | Non-subscription run returns `Success=false + AbortedReason` (`ContentKbOrchestrator.cs:243–252`); page renders `alert-danger`, success card only when `Success \|\| VideosDistilled > 0` (`Harvest.razor:636–643`) | closed |
| T-45-14 | Information Disclosure | Spend/cap/provider in logs | mitigate | Cap/spend shown to operator (intended, `Harvest.razor:418–420`); no connection string / provider / ledger key in logs or markup | closed |
| T-45-03 | Elevation of Privilege | `SessionCapOverride` raised to extreme value | accept | In-memory, app-scoped, resets on restart; XML doc states the app-scoped reality (`SessionCapOverride.cs:3–9`); no persistence path. Cost bounded to local operator. | closed |
| T-45-12 | Elevation of Privilege | Session cap raised to extreme value (UI) | accept | Raise writes only in-memory `CapOverride.OverrideUsd` (`Harvest.razor:1236`); non-negative validated before write (1227–1228); resets on restart | closed |
| T-45-07 | Spoofing (SSRF) | Channel URL input | accept | All outbound HTTP through YoutubeExplode (trusted, YouTube-only); Studio binds localhost-only, single-operator, no public endpoint | closed |
| T-45-SC | Tampering | npm/pip/cargo/NuGet installs | accept | Zero new packages across all four plans; in-file fakes only; CLAUDE.md no-new-packages constraint honored | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-45-1 | T-45-03 | `SessionCapOverride` is in-memory app-scoped, no persistence, resets on restart; cost exposure bounded to the local single operator. | operator (plan disposition) | 2026-06-15 |
| AR-45-2 | T-45-12 | UI cap-raise writes only the in-memory override; non-negative validated; resets on restart. Single-operator local tool. | operator (plan disposition) | 2026-06-15 |
| AR-45-3 | T-45-07 | Outbound HTTP is YoutubeExplode-only (YouTube-scoped); Studio is localhost-only single-operator with no public endpoint, so SSRF surface is not reachable. | operator (plan disposition) | 2026-06-15 |
| AR-45-4 | T-45-SC | No new packages added in any Phase 45 plan; supply-chain surface unchanged. | operator (plan disposition) | 2026-06-15 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-15 | 19 | 19 | 0 | gsd-security-auditor (sonnet) |

**Audit notes (adversarial scrutiny, resolved CLOSED):**
- T-45-17: `LlmDistillationProviderFactory.cs:41` constructs `new LlmDistillationService(httpClient)` *internally* for the openai branch — correct encapsulation. The mitigation requires no hardcoded construction at the `Program.cs` call site, which is confirmed.
- T-45-08: Browse/AddToQueue use per-call local CTS (`browseCts`, `addCts`), not shared `_cts`. Does not weaken the mitigation — only long-running harvest/distill ops hold `_cts` and are cancelled on Dispose.
- T-45-11: Subscription providers (`IsSubscriptionProvider=true`) reach Stage B without a Stage A dry-run by design ($0 marginal cost); the re-distill double-confirm gate still applies. Documented in 45-04 SUMMARY.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-15
