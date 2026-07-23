# Foreman Ledger - Cycle 19 Cut Lab Upgrade Hardening (2026-07-23)

baseline_commit: bf79c2b59
branch: main
mode: GSD milestone creation with Foreman ledger capture
run_started: 2026-07-23T12:58-06:00

## Objective

Create the next GSD milestone for Cut Lab upgrades after the structural card-pill locking fix landed on `main`.

## Inputs

- User request: "when done use foreman and gsd to create phase or milestone for upgrades to the cut lab"
- GSD state before switch: between milestones on mainline
- Source backlog: `.planning/milestones/ws-cut-lab-2026-07-23/BACKLOG-cut-lab-followups-2026-07-22.md`
- Recently merged fix: structural evidence card pills lock/unlock canonical pool cards

## Decision

Create a new milestone rather than inserting into the archived Cycle 18 roadmap.

Rationale:
- `.planning/STATE.md` reported mainline as between milestones.
- Cycle 18 Cut Lab is shipped and archived.
- The backlog entries are upgrade/hardening work with multi-surface regression risk, not emergency patch inserts.
- Phase numbering should continue after Cut Lab phases 101-107.

## Scope

| phase | status | purpose |
|-------|--------|---------|
| 108 | PLANNED | Server-authored Cut Lab UI patch DTOs replace client-side domain re-derivation |
| 109 | PLANNED | Consolidate what-if preview/commit into a shared service |
| 110 | PLANNED | Add Cut-Lab-scoped mobile sticky jump navigation |
| 111 | PLANNED | Regression gate for card-pill locking, Structural evidence behavior, themes, and test suites |

## Artifacts

- `.planning/PROJECT.md` - active milestone summary
- `.planning/REQUIREMENTS.md` - Cycle 19 requirements CLUP-01..CLUP-10
- `.planning/ROADMAP.md` - phases 108-111
- `.planning/STATE.md` - milestone switch and next step

## Verification

- GSD `state.milestone-switch` used for `.planning/STATE.md` frontmatter/body consistency.
- `gsd-sdk query phases.clear --confirm` returned `cleared: 0`.
- No code changes are part of this ledger entry.

## Foreman Direction Protocol

Foreman is the active requirements-control ledger for Cycle 19.

Rules:
- Capture user-proposed Cut Lab upgrade requirements here first.
- Separate approved decisions from open questions.
- Do not update GSD phase docs from a raw conversation note; update them only from approved Foreman entries.
- Keep implementation planning blocked until each requirement has an owner phase and acceptance criteria.
- Use GSD after Foreman approval to persist the accepted scope in `.planning/REQUIREMENTS.md` and `.planning/ROADMAP.md`.

## Foreman Intake - 2026-07-23 Requirement Discovery

Source user requests:
- Filter the lock pool by locked state.
- Search for a card.
- Collapse Cut Lab sections.
- Add anchors and section links similar to the Manabase page.
- Readjust weak floor cases where combos have missing cards.
- Add short directions for package assignment.
- Research the best way to show card oracle text, either by image or text, and where to show it.
- Recognize that cards are in a combo, not only that a combo partner is missing.

Research finding:
- Use inline per-card disclosure for oracle and combo context.
- Prefer text-first oracle display because current Scryfall DTO/cache paths already carry oracle text.
- Defer card-image display unless image URLs are added to the Scryfall mapping/cache path deliberately.
- Avoid hover-only tooltip behavior for essential card text because it is fragile on mobile and keyboard.
- Avoid modal-first behavior because Cut Lab users need repeated scan/compare actions inside tables and sections.

Requirement decisions:

| id | question | current recommended default | status |
|----|----------|-----------------------------|--------|
| F-C19-Q1 | Should lock/search filters apply only to the main "Lock your pool" table, or also role groups and structural evidence chips? | Main table first; chips remain lock/unlock controls, not filtered views. | APPROVED |
| F-C19-Q2 | Should collapsed sections remember their state during the session, across reloads, or reset each page load? | Remember in browser local storage per deck/page. | APPROVED |
| F-C19-Q3 | Should anchor links mirror Manabase as a top sticky nav, an in-page compact nav, or both desktop/mobile variants? | Compact in-page nav with mobile sticky behavior. | APPROVED |
| F-C19-Q4 | Should oracle text appear in the lock pool only, structural findings only, or every card surface that has a lockable card pill? | Lock pool rows first, then structural/combo evidence via the same disclosure component. | APPROVED |
| F-C19-Q5 | For combo recognition, should the UI show complete combos, near-combos, or both? | Both: complete combo membership and near-combo missing partner state. | APPROVED |
| F-C19-Q6 | Should package assignment directions be a short static help block, inline helper text by the select, or an expandable mini-guide? | Short static help block plus one-line inline helper near the package select. | APPROVED |

Approved requirement decisions:
- Main Lock your pool table gets locked-state filtering and card search first; role-group and structural chips remain lock/unlock controls, not separately filtered views.
- Collapsed section state persists in browser local storage per deck/page.
- Cut Lab uses compact in-page anchors with mobile sticky jump behavior, borrowing the Manabase page pattern.
- Oracle context is text-first: lock pool rows first, then structural/combo evidence via the same disclosure component.
- Combo recognition shows both complete combo membership and near-combo missing partner state.
- Package assignment gets a short static help block plus one-line inline helper near the package select.

GSD propagation:
- Approved decisions map to Phase 110, renamed/expanded to Cut Lab Navigation and Pool Discovery.
- Phase 111 remains the final regression gate and must cover the expanded Phase 110 surfaces.
- Propagated to `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/PROJECT.md`, and `.planning/STATE.md` on 2026-07-23.

## Next Step

Run `/gsd-plan-phase 108` when ready to start implementation planning.
