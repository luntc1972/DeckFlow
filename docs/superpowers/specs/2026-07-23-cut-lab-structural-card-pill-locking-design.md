# Cut Lab Structural Card Pill Locking

## Goal

Let users lock and unlock a pool card by clicking its evidence pill in the
Structural findings section. Preserve explanatory evidence as display-only
text and keep the pool table checkbox as the single source of truth.

## Scope

- Make Structural evidence interactive only when its text identifies a
  lockable card in the current pool. Match case-insensitively against either
  the exact card name or the existing `Card Name · MV N` display format.
- Leave non-card evidence, missing cards, and permanently locked cards
  non-interactive.
- Support both Structural rendering paths:
  - the initial Razor-rendered page;
  - the TypeScript-rendered replacement after a live cut or restore decision.
- Keep all representations of a card synchronized: pool checkbox, role-group
  card pills, Structural card pills, lock counts, `aria-pressed`, and serialized
  Cut Lab state.

This change does not add a new lock-state model, change Structural finding
content, or make arbitrary evidence text clickable.

## Design

Structural card pills will use the existing `data-cut-lab-chip-card` contract
and delegated click handler. The handler continues to locate the matching pool
row, toggle its enabled lock checkbox, then run the existing refresh and
serialization path.

The initial Razor renderer will compare each evidence value with the current
pool. Matching is deterministic: the value must equal the card name or start
with that exact card name followed by the established ` · MV ` delimiter. A
matching, lockable card becomes a `button` with the shared card-pill classes,
`data-cut-lab-chip-card`, and its current `aria-pressed` value. Everything else
remains a `span`.

The live TypeScript renderer will perform the same check against the current
pool rows before creating each evidence element. It will create the same button
contract for lockable matches and a display-only span otherwise. Reusing the
shared data attribute lets the existing refresh path update every pill for the
same card without a Structural-specific state or event handler.

## Accessibility and Visual Behavior

- Interactive evidence uses a real `button type="button"`.
- `aria-pressed` reflects the canonical checkbox state.
- Locked, hover, and focus styling reuse the card-pill behavior introduced for
  role-group card pills.
- Display-only evidence remains a non-focusable `span`.

## Testing

Follow red-green-refactor:

1. Add a failing interaction test where a Structural card pill toggles the
   canonical checkbox, serialized state, locked class, and `aria-pressed` in
   both directions.
2. Prove a non-card Structural evidence pill stays inert.
3. Cover the live Structural re-render path so a card pill created after a
   decision remains interactive.
4. Run the focused interaction suite, full frontend test suite, TypeScript
   compilation, .NET build, and diff validation.

## Acceptance Criteria

- Clicking a Structural pill for a lockable pool card locks it.
- Clicking it again unlocks it.
- All other views of that card update immediately.
- Non-card evidence and permanently locked cards are not presented as buttons.
- The behavior survives a live Structural findings re-render.
- Existing Cut Lab interaction and build checks remain green.
