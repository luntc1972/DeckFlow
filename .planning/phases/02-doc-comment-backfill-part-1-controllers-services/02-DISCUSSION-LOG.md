# Phase 2: Doc-Comment Backfill — Part 1 (Controllers + Services) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-24
**Phase:** 2-doc-comment-backfill-part-1-controllers-services
**Areas discussed:** inheritdoc policy, Method-depth threshold, Summary source/voice, Record param docs

---

## inheritdoc policy (interface/impl pairs)

| Option | Description | Selected |
|--------|-------------|----------|
| Interface owns prose, impl uses inheritdoc | Full summary on interface; `<inheritdoc/>` on impl class + members. DRY, .NET-standard. | ✓ |
| Full summaries on both | Duplicate prose; risks drift. | |
| Prose on impl, inheritdoc on interface | Inverts the norm. | |

**User's choice:** Interface owns prose, impl uses inheritdoc (Recommended)
**Notes:** ~18 of 45 in-scope types are interface/impl pairs. Standalone classes get full summaries directly.

---

## Method-level depth threshold

| Option | Description | Selected |
|--------|-------------|----------|
| ≥2 real params OR non-obvious return; trailing CancellationToken doesn't count | Documents params at ≥2 real args; `<returns>` when meaning isn't obvious. | ✓ |
| Literal SC2: any 2+ params incl. CancellationToken, non-void | Mechanical but adds boilerplate cancellationToken param lines. | |
| Summary-only everywhere | Would violate SC2. | |

**User's choice:** ≥2 real params OR non-obvious return; trailing CancellationToken doesn't count (Recommended)
**Notes:** Satisfies SC2 without boilerplate explosion CLAUDE.md discourages.

---

## Summary source & voice

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid: seed from CLAUDE.md component table, verify against code, "why not what" | Reuse existing one-liners where present; write fresh otherwise. | ✓ |
| Read every type fresh | Most accurate, slowest, risks vocabulary drift. | |
| CLAUDE.md table only | Fast but leaves gaps (records/DTOs/caches not in table). | |

**User's choice:** Hybrid (Recommended)
**Notes:** Verify seeded text against current code; many of the 45 aren't in the table.

---

## Record `<param>` docs (positional DTO records)

| Option | Description | Selected |
|--------|-------------|----------|
| Type-level summary always; `<param>` only when field name isn't self-evident | Summary on every record; param tags only for non-obvious fields. | ✓ |
| `<param>` on every positional field | Thorough but heavy/noisy. | |
| Type-level summary only | Loses meaning on records with cryptic fields. | |

**User's choice:** Type-level summary always; `<param>` only when field name isn't self-evident (Recommended)
**Notes:** ~9 positional records in scope.

---

## Claude's Discretion

- Plan splitting / batching of the 45 types (planner's call).
- Exact summary wording within the hybrid sourcing rule.

## Deferred Ideas

- Part 2 dirs (`Models/`, `Models/Api/`, `Infrastructure/`, `Security/`, `ViewModels/`) + `NoWarn` strip → Phase 8.
