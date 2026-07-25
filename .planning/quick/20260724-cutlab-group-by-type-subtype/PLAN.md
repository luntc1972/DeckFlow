---
slug: cutlab-group-by-type-subtype
status: in-progress
created: 2026-07-24
---

# Cut Lab: group the pool by type and by subtype

## Goal
Add two new always-visible sections to the Cut Lab pool view — **By type** and **By subtype** —
mirroring how Moxfield groups a decklist. The existing role grouping stays; these are additive.

## Decisions (locked, adjustable)
- **By type:** one bucket per card, chosen by primary-type priority (Creature > Planeswalker >
  Battle > Instant > Sorcery > Artifact > Enchantment > Land > Other). Front face only (before
  `//`); ignore supertypes (Legendary/Basic/Snow/World/Ongoing/Host). Counts sum to pool size.
- **By subtype:** a card appears under EACH of its subtype tokens (front face, after the `—`).
  Cards with no subtype are omitted from this section. Subtype counts may exceed pool size.
- Rendering mirrors the existing role-group `<details>` markup (chips, popup wiring, theme classes).
- Grouping is server-rendered in the view model (like `RoleGroups`); no new AJAX.

## Tasks
1. Core: add `CardTypeLine.PrimaryType(typeLine)` (priority bucket) + `CardTypeLine.Subtypes(typeLine)`
   (front-face subtype tokens). xUnit tests in `DeckFlow.Core.Tests` (DFC, adventure, multi-type,
   supertype, no-subtype, empty).
2. Web: `CutLabViewModel.TypeGroups` + `SubtypeGroups` (reuse `CutLabRoleGroupView`), built by
   `BuildTypeGroups(pool)` / `BuildSubtypeGroups(pool)` mirroring `BuildRoleGroups`.
3. View: two `<details>` sections after the role-group section, mirroring its markup (member chips
   clickable → existing card popup), with per-group counts.
4. CSS: reuse role-group classes; add minimal parallel classes only if needed, in `site-common.css`.
5. Tests: xUnit for the parser + the group builders.

## Verification
- `dotnet build` clean; xUnit (Core parser + Web group builders) green.
- UI review: screenshots desktop + mobile across representative guild themes; cross-AI UI check.
- README + help note the new grouping sections.
