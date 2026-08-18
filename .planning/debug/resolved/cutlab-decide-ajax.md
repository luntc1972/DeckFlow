# Debug: Cut Lab decide-flow AJAX bugs

Status: root-caused (investigation complete; fixes not yet applied)
Date: 2026-07-24
Reporter: user UAT (Cut Lab dark feature, local)
Feature branch home: main (Cut Lab shipped 2026.07.9; recent UAT batch 68988e12 rewrote cut-lab.ts +501 LOC)

## Symptom 1 — "Accept cut" needs two clicks

First click on **Accept cut** shows "This is taking longer than expected. Try again in a
moment." and nothing happens; second click performs the cut.

### Root cause (CONFIRMED)

`handleDecisionSubmit` in `DeckFlow.Web/wwwroot/ts/cut-lab.ts:2693` arms a client abort at
`cutLabDecisionTimeoutMs = 3000` (line 220):

```
const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs); // 2709
...
} catch (error) {
  renderDecisionError(form, error instanceof DOMException && error.name === 'AbortError'
    ? cutLabDecisionTimeoutCopy   // "This is taking longer than expected..."
    : cutLabDecisionErrorCopy);
}
```

The first `/api/cut-lab/decide` round-trip is a cold request (ASP.NET JIT warmup + simulation
+ metrics engine) that exceeds 3s. The client aborts, shows the timeout copy, and applies NO
patch (does nothing). The second click hits a warmed server (<3s) and the patch applies. No
double-cut occurs because client state only advances when a patch is received.

### Fix direction (needs Codex)

Client-timeout tuning. Options: raise `cutLabDecisionTimeoutMs` well above cold-start latency
(e.g. 15000–20000ms), OR drop the abort timeout entirely and keep the busy/spinner state until
the server responds. Same 3000ms timeout is reused by the adjust (2571) and what-if (2520)
submits, so decide whether they share the new value. Small, low-risk.

## Symptom 2 — "Compare to baseline" doesn't change after cutting

After cutting 3 cards, the **Compare to baseline** section (and the goals baseline-trend) stays
frozen at the page-load values.

### Root cause (CONFIRMED)

The decide UI patch does not carry baseline-vs-current metric rows. `CutLabUiPatch`
(cut-lab.ts:145-162) exposes `currentCount` but no per-metric before/current rows and no goal
probabilities; grep confirms no `baseline` field in `CutLabUiPatchDto.cs` /
`CutLabUiPatchBuilder.cs`. `applyServerPatch` (cut-lab.ts:2479-2502) re-renders proposal,
sticky bar, cuts-made, structural findings, quantity tuners, addable basics, export state,
what-if options — but NEVER the "Compare to baseline" table (`Views/Deck/CutLab.cshtml:1271-1295`)
or the goals baseline-trend table (`CutLab.cshtml:745,771`). Those sections are server-rendered
once at page load, so AJAX cuts leave them stale until a full reload.

### Fix direction (needs Codex — non-trivial)

Extend the decide patch to carry updated compare-to-baseline metric rows (and likely the goal
probabilities), spanning `CutLabUiPatchDto` (server DTO), `CutLabUiPatchBuilder` (populate),
and a new `renderCompareToBaseline` in `applyServerPatch` (client). Also updates the
"BaselineCount → CurrentCount" line (CutLab.cshtml:1277). Cross-layer; needs a small plan.

## Related (separate bug, already dispatched)

Bug: Talismans/mana rocks not counted as ramp — root-caused to `DeckStatClassifier.IsRampCard`
missing mana-symbol producers; Codex fixing on worktree `deckflow-ramp-fix`
(branch `fix/cutlab-ramp-manasymbol`). Independent of the decide-flow bugs above.
