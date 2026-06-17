---
phase: 42
slug: orchestrator-extraction
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-13
---

# Phase 42 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Phase 42 is a behavior-preserving extraction (ContentKb domain logic CLI → Core). No new external surface, network egress, auth boundary, or user-facing input was introduced. Threats are data-integrity/parity invariants and secret-non-disclosure.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| CLI host ↔ DeckFlow.Core orchestrator | Provider/connection selection (D-05) + path resolution (D-06) stay host-side; Core receives only resolved values | artifactRoot path (via ContentKbOrchestratorOptions); no connection string |
| Studio process ↔ local SQLite Content KB DB | Studio smoke service reads the local KB DB via the maintenance slice; no write, no network, no prod connection | local SQLite path only |
| Studio config ↔ prod Postgres connection string | Prod conn string stays presence-only (StudioConfig); never constructed, logged, or surfaced | presence boolean only |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-42-01 | Tampering / Data-integrity | DistillResult shape | mitigate | `required bool Success` forces explicit construction; can express Success/AbortedReason/zero-rows so the validation-fail short-circuit cannot be silently lost. Pinned by re-pointed ValidateClips anchor (42-03). | closed |
| T-42-02 | Information Disclosure | Progress sink messages | accept | Progress strings carry the same per-video ids/counts today's logs already carry; no secrets. CLI/Studio sinks are local. | closed |
| T-42-03 | Tampering / Data-integrity | DistillAsync validation short-circuit | mitigate | Orchestrator calls `DistillationValidation.Validate{Clips,Summary,TranscriptLength}` (3 call sites confirmed) before any Insert/Upsert, so a malformed distill writes no partial rows. | closed |
| T-42-04 | Tampering | Spend-ledger record-before-next-call ordering | mitigate | DistillVideoAsync lifted verbatim — "record incurred cost BEFORE the next call" comment + WouldExceedCap gates between summary/clips/tags preserved (HIGH-1/FIX-1). Verified by reviewer diff vs CLI. | closed |
| T-42-04b | Tampering | AddSource invalid-type short-circuit | mitigate | `if (!IsValidContentSourceType(type))` is the first returnable branch (orchestrator line 103), returns InvalidType + exact message before any store call. Pinned by 42-05 ContentSourceOrchestratorParityTests. | closed |
| T-42-05 | Information Disclosure | progress sink / logger lines | accept | Same per-video ids/counts already logged today; no secrets. Core holds no connection string (provider selection host-side, D-05); options record carries only artifactRoot. | closed |
| T-42-06 | Tampering / Data-integrity | result → exit-code mapping | mitigate | CLI adapter maps DistillResult.Success=false (metered refusal) → exit 1, validation-fail success → exit 0 as today. Pinned by anchor + 42-05 parity tests. | closed |
| T-42-07 | Repudiation | live Console progress ordering | mitigate | CLI ConsoleOrchestratorProgress implements the synchronous IOrchestratorProgress (Console.WriteLine direct); `grep "new Progress<"` in CLI adapter = 0, preserving per-video line interleaving. | closed |
| T-42-08 | Information Disclosure | corpus-reset Postgres connection string | mitigate | Postgres conn string stays host-side (CLI); `grep -iE "Npgsql\|ConnectionString\|Postgres"` over DeckFlow.Core/Orchestration/ = 0. Options record carries only artifactRoot. | closed |
| T-42-09 | Information Disclosure | Studio prod connection string | mitigate | Studio reads `Studio:ProdConnectionString` for presence only (isProdConfigured = !IsNullOrEmpty); value never constructed/logged (log line emits "configured"/"not configured"). Runtime-confirmed "not configured". Smoke stores use local SQLite. | closed |
| T-42-10 | Elevation of Privilege / accidental scope | Studio gaining a CLI dependency | mitigate | `grep -rn "DeckFlow.CLI" DeckFlow.Studio/` = 0; no CLI ProjectReference in Studio csproj (SC4). | closed |
| T-42-11 | Tampering | Studio smoke op performing a write | mitigate | ProbeAsync calls only ListBlockedAsync (read-only, returns rows). Integration services are real Core impls invoked nowhere on the read path. | closed |
| T-42-12 | Tampering / Data-integrity | content-index-export JSON seed shape | mitigate | 42-05 golden-fixture test serializes through the real CLI `SerializeContentIndexExportRows` (not a fork); any drift in property order/camelCase/indentation/null/row-order/trailing-newline fails. Newline-normalized for platform stability. | closed |
| T-42-13 | Tampering / Data-integrity | CLI exit-code parity (add-source/maintenance/distill) | mitigate | 42-05 parity tests pin the orchestrator RESULT records the CLI maps to exit 0/1/2/3, incl. metered refusal (isSubscriptionProvider:false) → exit 1. Core.Tests 330/330. | closed |
| T-42-14 | Availability / container build | orchestrator ctor fails to resolve | mitigate | Studio registers every ctor dep (real local SQLite stores + options) — no implicit throwing fake, no bare-string param. Runtime-confirmed: Studio started on :5271, full ctor resolved at startup. | closed |
| T-42-SC | Tampering / Supply-chain | npm/pip/cargo/NuGet installs | mitigate (n/a) | 0 new packages across the phase. csproj diff shows only a test `CopyToOutputDirectory` fixture item — no PackageReference additions. DI.Abstractions resolved via existing transitive Logging.Abstractions. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-42-01 | T-42-02, T-42-05 | Progress-sink / logger lines carry only per-video ids + counts already present in today's logs; no secrets traverse the sink. CLI/Studio sinks are local-only. Behavior unchanged from pre-extraction. | luntc1972 | 2026-06-13 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-13 | 16 | 16 | 0 | Claude (inline verify; register authored at plan time) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-13
