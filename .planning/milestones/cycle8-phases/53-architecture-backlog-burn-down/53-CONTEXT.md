# Phase 53: Architecture backlog burn-down - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning
**Source:** Re-scope of Phase 39 audit (`39-AUDIT.md` findings B–K) against current code, 2026-06-17

<domain>
## Phase Boundary

Burn down the remaining **behavior-preserving** SRP / cohesion / layering findings from the Phase 39 architecture audit (ARCH-01 ranked backlog). Phase 39 executed only Finding **A**; this phase closes the still-open items verified against current code (post Phase 38 SRP-split and Phase 49 Dapper sweep).

**Zero user-visible change.** Every refactor must be provable by existing tests (build clean + suite green) or, where a real coverage gap exists, by a new test added in the same change. No route/CLI/contract/packet-text behavior changes.

### Verified status of the audit backlog (re-checked 2026-06-17)
| ID | Finding | Status now | In scope? |
|----|---------|-----------|-----------|
| A | Extract loader/resolver from 4 packet services | DONE (Phase 39) | no |
| **B** | Split `CategoryKnowledgeRepository` → Schema/Queue/CardCategory | OPEN — 1272 LOC god-file (Phase 49 Dapper'd data access, did NOT SRP-split) | **YES** |
| C | Split `ContentKbCommandRunners` → Harvest/Distill/Source | LARGELY DONE — logic moved to Core orchestrator slices; CLI runner now 557-LOC dispatch glue | no (dropped) |
| **D** | Finish `Services/` foldering + extract `Program.cs` DI extensions | PARTIAL — foldering started but `Services/Content/` empty; `Program.cs` still 552 LOC, DI inline | **YES** |
| **E** | Relocate misplaced domain logic → Core | PARTIAL — distill helpers already moved to `DistillationValidation.cs`; deck-stat classifiers still in `DeckComparisonService` | **YES (classifiers only)** |
| **F** | Strengthen dual-dialect storage abstraction | OPEN/WORSE — 51 `IsPostgres/IsSqlite` branches (audit saw 33; Phase 49 grew them) + 3 `Feedback*` members leaking into Core `IRelationalDialect` | **YES (partial — see decisions)** |
| G–K | ADR notes + residual test gaps | low-tier; several already closed | no (defer) |

</domain>

<decisions>
## Implementation Decisions

### ARCH-B — Split `CategoryKnowledgeRepository` (LOCKED, top value)
- Split `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (1272 LOC, 24 public methods, 5 reasons-to-change) into focused collaborators: **Schema/DDL+migration**, **deck-harvest Queue**, **CardCategory read/query+persistence/upsert** (+ filtering/normalization with the closest concern).
- Technique: **facade-then-extract** — keep `CategoryKnowledgeRepository` as a thin facade implementing the existing public surface, delegating to the new internal collaborators, so Web + CLI + hosted-job callers do not repoint. Behavior provable by existing safety net (17 round-trip facts, `CategoryCacheSchemaParityTests`, `ContentHashDedupTests`) — **no new harness**.
- Mirror the Phase 38 split pattern. `EnsureSchemaAsync` ownership consolidates into the Schema collaborator.

### ARCH-D — `Program.cs` DI extraction + finish `Services/` foldering (LOCKED, lowest risk)
- Extract inline DI from `Program.cs` (552 LOC) into `AddDeckFlowXxx()` extension methods following the established pattern: `AddDeckFlowHttpClients`, `AddDeckFlowScryfallServices`, `AddDeckFlowPromptVariants`, `AddDeckFlowPacketServices` (names indicative).
- Finish concern-foldering: fill the **empty `Services/Content/`**, move remaining flat root files to their concern folders (Scryfall→`Services/Scryfall/`, stores→a `Services/Persistence/`). **Namespaces unchanged = pure file moves** (no using churn). Build + test is full proof.

### ARCH-E — Relocate deck-stat classifiers to Core (LOCKED, scoped down)
- Move the pure deck-stat classifiers (`IsRampCard`/`IsDrawCard`/curve math, ~150 LOC) out of `DeckComparisonService` into `DeckFlow.Core` (pure CPU domain logic per CLAUDE.md). Add Core unit tests for the relocated classifiers (they are currently untestable except through the Web assembly).
- Distill helpers (`ComputeProjectedVideoCostUsd`/`EstimateTokenCount`/`ValidateClips`) are **already in Core** — out of scope.

### ARCH-F — Storage layering fix (LOCKED partial; full strengthening GATED)
- **In scope (low-risk "S"):** remove the 3 Web-only `Feedback*` members from the Core `IRelationalDialect` interface — a clear layering violation. Relocate to a Web-side abstraction.
- **Gated / deferred:** collapsing the 51 `IsPostgres/IsSqlite` branches into richer dialect methods (CREATE TABLE DDL, UPSERT-vs-ON-CONFLICT) is **NOT** done blind — the Postgres DDL path has **no automated guard** (all 11 parity tests are SQLite-only). Only attempt the broader strengthening if a Postgres parity test gate is added first; otherwise defer to a follow-up. Planner: treat the Feedback-leak removal as the committed F deliverable; propose the dialect collapse only behind a PG-parity-test prerequisite.

### Claude's Discretion
- Exact collaborator class names + folder names, plan/wave breakdown, order of B vs D vs E.
- Whether to also drop C as a tiny tidy-up (default: leave it — already dispatch glue).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audit source
- `.planning/milestones/v1.6-phases/39-architecture-review/39-AUDIT.md` — the ranked backlog A–K with per-finding files/problem/effort/risk.
- `.planning/milestones/v1.6-phases/39-architecture-review/39-AUDIT-CODEX.md` — Codex cross-audit.

### Target files
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (B — 1272 LOC)
- `DeckFlow.Web/Program.cs` (D — 552 LOC); `DeckFlow.Web/Services/` (foldering; `Services/Content/` empty)
- `DeckFlow.Web/Services/PromptBuilders/DeckComparisonService.cs` (E — classifiers)
- `DeckFlow.Core/Storage/IRelationalDialect.cs` (F — Feedback* leak); `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs`

### Safety nets (behavior-preservation proof)
- `CategoryCacheSchemaParityTests`, `ContentHashDedupTests`, the 17 round-trip facts (B)
- Phase 38 split pattern: `.planning/milestones/v1.6-phases/38-*/38-*-SUMMARY.md`
- Project rules: `./CLAUDE.md` (changed-lines format gate, carve-outs, no mass reflow, pure domain logic → Core)

</canonical_refs>

<specifics>
## Specific Ideas
- B is the recommended first plan (highest value, strongest safety net, continues Phase 38/39 arc).
- D is lowest-risk (build+test is full proof) — good parallel wave.
- E is small and self-contained.
- F committed deliverable = Feedback-leak removal only; dialect collapse is a stretch goal gated on a new PG parity test.
</specifics>

<deferred>
## Deferred Ideas
- **C** — `ContentKbCommandRunners` split: already substantially addressed by the Core orchestrator slices; CLI runner is dispatch glue. Skip unless trivially tidy.
- **F full** — collapsing all 51 dialect branches: deferred pending Postgres DDL parity test coverage.
- **G–K** — packet cache-key strategy (G, pairs with A), `IScryfallThrottle` (H), MemoryCache SizeLimit doc (I), System.CommandLine pin ADR (J), residual middleware-ordering + Polly policy-shape tests (K). Capture as ADR notes / backlog.
</deferred>

---

*Phase: 53-architecture-backlog-burn-down*
*Context gathered: 2026-06-17 via Phase 39 audit re-scope*
