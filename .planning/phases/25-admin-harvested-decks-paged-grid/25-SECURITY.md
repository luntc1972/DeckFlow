---
phase: 25
slug: admin-harvested-decks-paged-grid
status: verified
threats_open: 0
asvs_level: 1
created: 2026-05-24
---

# Phase 25 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register authored at plan time (both 25-01 + 25-02 PLAN.md carried `<threat_model>` blocks); mitigations verified against implementation + the Phase 25 code review (25-REVIEW.md).

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| client → /Admin/Harvest | Admin-only surface, gated by the `/Admin/*` BasicAuth branch (`DeckFlow.Web/Program.cs:385`). The `page` query-string param is the only untrusted input reaching this phase. | `page` integer (untrusted) |
| service → database | Store/repository issue SQL against SQLite (local) or Postgres (prod). | Harvested public deck metadata (deck ids, commander names, timestamps) |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-25-01 | Tampering | `page` → OFFSET (SQL injection) | mitigate | `@limit`/`@offset` parameterized via `RelationalDatabaseConnection.AddParameter` (`CategoryKnowledgeRepository.cs:360-361`); no string concat. `to_regclass('public.card_category_observations')` is a hardcoded literal. | closed |
| T-25-02 | Denial of Service | huge `page` → enormous OFFSET scan | mitigate | Controller upper-clamp `page = Math.Min(page, deckTotalPages)` (`AdminHarvestController.cs:91`); server-side fixed `pageSize = 25` (never client-controlled); controller test asserts page=999999 clamps to DeckTotalPages and the clamped page reaches the store. | closed |
| T-25-03 | Denial of Service | load full deck_queue into memory (Render 512MB cap) | mitigate | Aggregate query is `LIMIT @limit OFFSET @offset` only; repository returns at most pageSize rows. Repo test asserts page slice ≤ pageSize, never the whole table. | closed |
| T-25-04 | Information Disclosure | `reltuples` / `pg_class` read | accept | `pg_class.reltuples` is non-sensitive planner metadata; admin-only surface; no PII. `to_regclass('public....')` is schema-qualified, so it cannot match a same-named relation in another schema. | closed |
| T-25-05 | Tampering | negative/zero `page` → negative OFFSET | mitigate | `Math.Max(page, 1)` floor in controller (`AdminHarvestController.cs:85`) AND independent repo self-clamp `page/pageSize = Math.Max(_, 1)` (`CategoryKnowledgeRepository.cs:343-344`); page=0 lower-clamp test. | closed |
| T-25-06 | Elevation of Privilege | new unauthenticated surface | accept | No new route added; `Index` stays under the existing `/Admin/*` BasicAuth branch (`Program.cs:385-386`). No new surface. | closed |
| T-25-07 | Information Disclosure | deck ids / commander names rendered in grid | accept | Already-stored harvested public deck metadata, shown only to authenticated admins; no PII. | closed |
| T-25-SC | Tampering | supply chain (npm/pip/cargo installs) | accept | No package installs in this phase — no new dependencies added. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-25-1 | T-25-04 | `reltuples`/`pg_class` is non-sensitive planner metadata on an admin-only surface; schema-qualified lookup constrains it to `public`. | luntc1972 | 2026-05-24 |
| AR-25-2 | T-25-06 | No new route; reuses the existing `/Admin/*` BasicAuth gate. | luntc1972 | 2026-05-24 |
| AR-25-3 | T-25-07 | Harvested public deck metadata, admin-only, no PII. | luntc1972 | 2026-05-24 |
| AR-25-4 | T-25-SC | No package installs in this phase. | luntc1972 | 2026-05-24 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-05-24 | 8 | 8 | 0 | gsd-secure-phase (Claude orchestrator; mitigations verified in source + 25-REVIEW.md) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter
