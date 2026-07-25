---
slug: add-a-option-to-use-the-sideboard-as-par
status: complete
completed: 2026-07-20
---

# Summary: Cut Lab sideboard + maybeboard pool option

**Outcome:** Shipped. Cut Lab now exposes an opt-in **Include sideboard & maybeboard cards** intake checkbox that expands the analyzed pool to include those boards, persists through Cut Lab state/no-JS round-trips, and keeps the default mainboard-only behavior unchanged when left off.

**Shipping commits:**
- `7283978d feat(260720-f3o): persist cut lab pool board option`
- `0fc1e759 feat(260720-f3o): add cut lab pool option checkbox`
- `91cc6858 test(260720-f3o): cover expanded cut lab pool boards`

**Verification:** `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabPageService"` passed (41/41), `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` passed (215/215), and `dotnet build DeckFlow.sln -clp:ErrorsOnly` succeeded with the expected 9 pre-existing warnings and 0 errors.
