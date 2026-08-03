---
created: 2026-08-03T20:10:00.000Z
title: Fix the non-deterministic cut-lab-tuning.spec.ts flake
area: testing
files:
  - DeckFlow.Web/e2e/cut-lab-tuning.spec.ts:67-68
  - DeckFlow.Web/Views/Deck/CutLab.cshtml:193-210
  - DeckFlow.Web/Views/Deck/Manabase.cshtml:160-180
  - DeckFlow.Web/e2e/bracket-smoke.spec.ts
---

## Problem

`cut-lab-tuning.spec.ts` fails non-deterministically: **2 of its tests fail per run, a different
pair each time**. Playwright's error is:

```
<span>Focused</span> intercepts pointer events
```

**This is pre-existing, not a regression.** Reproduced on `feat/ui-audit-batch-a` with no Batch G
code present, during the 2026-08-02 UI-audit work. It did not block that UAT (which passed 16/16 on
2026-08-03), but it is the only finding from that session not captured anywhere else, and it makes
every full e2e run ambiguous — a real failure in this spec is currently indistinguishable from the
flake.

## Root cause — hypothesis, not yet confirmed

Not reproduced under instrumentation; this is read from the markup and the error string, and the
first task below is to confirm or kill it.

`cut-lab-tuning.spec.ts:68` does:

```ts
await page.locator('input[name="PlayExperience"][value="Focused"]').check();
```

The target input at `CutLab.cshtml:203` sits inside a styled pill:

```html
<label class="manabase-pill">
  <input type="radio" name="PlayExperience" value="Focused" />
  <span>Focused</span>
</label>
```

`.check()` performs an actionability check and then clicks the **input's** center point. The
`.manabase-pill` styling visually replaces the native radio with the `<span>`, so the input is
zero-size or covered and the hit-test lands on the span instead — exactly the reported message.

The **non-determinism** is the part the hypothesis does not yet explain, and is what the
investigation must actually pin down. Candidate causes, in order of suspicion:

1. A layout/paint race — the pill's final geometry lands after the check begins, so whether the
   hit-test hits the input or the span depends on timing.
2. Test-order or state dependence — a different pair fails each run, which smells like shared state
   (the persistent dev-database tool flags, or a scenario saved by an earlier test) rather than
   pure timing.

"A different pair each time" is the key clue: pure hit-test geometry would fail the *same* tests
every run. Do not accept the geometry explanation alone until the varying-pair behavior is explained.

## Likely fix

Once confirmed, the mechanical fix is one of — click the `<label>`, use `.check({ force: true })`,
or `setChecked` — but pick the one that still proves the control works rather than bypassing the
thing under test. `force: true` disables the actionability check entirely and would hide a real
regression in the pill, so prefer clicking the label if that reproduces a genuine user gesture.

## Also in scope

`Manabase.cshtml:160-180` uses the identical `label.manabase-pill > input + span` shape for its
`Mode` radios, including its own `<span>Focused</span>`. Any spec that `.check()`s a manabase pill
has the same latent trap — sweep for it rather than fixing only the one failing call site.

`bracket-smoke.spec.ts` carries a separate but related latent trap noted during Batch G: it restores
a tool flag to a **hardcoded** value in `afterEach` rather than capturing the prior value. Correct
today by luck, and it is the exact pattern that silently broke 5 tests across 3 files during Batch G.
Fix it in the same pass.

## Acceptance

- The varying-pair non-determinism is explained, not just suppressed
- `cut-lab-tuning.spec.ts` passes 10 consecutive single-worker runs
- The fix is mutation-proven: breaking the pill control fails the spec (i.e. the test still has teeth)
- No spec hardcodes a tool-flag restore value

## Context

- Session record: `.planning/quick/260802-b7q-fix-five-batch-g-form-correctness-defec/SUMMARY.md`
- Separate, unrelated: the full **parallel** e2e suite shows ~13 timeout-shaped failures on this
  machine (Debug build + parallel workers), reproduced on `origin/main`. Single-worker is clean.
  Do not conflate the two.
