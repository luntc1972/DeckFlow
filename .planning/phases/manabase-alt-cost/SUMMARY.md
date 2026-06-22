# Summary — Manabase alt/reduced cost overrides

**Status:** SHIPPED (reconstructed on reconcile 2026-06-22) · **Date executed:** 2026-06-21

> Reconstructed during a main-branch planning reconcile. Implemented + committed to `main`,
> deployed to prod; SUMMARY never written. Closed from git history.

## What shipped

Lets the analyzer account for spells whose real mana cost differs from their printed cost
(alternative/reduced costs), with an editable per-card override:

- **Detect alt/reduced self-costs** from the card's own text (`d893501b`).
- **Apply overrides as the effective requirement** — substituted into the castability/source math
  (`811cffb3`).
- **Web plumbing** for overrides + suggestions through request/service/controller (`da85f257`).
- **Overrides box + applied-cost marker UI** — editable per-card cost with a visual marker when an
  override is in effect (`32ae9659`).
- Codex code-review fix (self-anchor + strict cost) `82f6e197`; README + in-app help documented
  (`fca3d0ea`, `1d8f2ac7`).

Plan history: `6b4b079c` (initial) → `06e69711` (BLOCK resolved) → `13ebba42` (braced cost format,
Codex APPROVE-WITH-CHANGES).

## Notes

- ⚠ Done directly on `main` (milestone-branch deviation; already prod-deployed).
- Feature originally built on worktree branch `feature/manabase-alt-cost`, since landed on `main`.
- Codex review-only per the active override.
