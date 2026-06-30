# UI-SPEC — Phase 78: Auto-Refreshing Primer (stale flag)

**Requirements covered:** PRIMER-01 (stale indicator), PRIMER-02 (multiset-hash semantics — reorder/printing = fresh, add/remove/qty = stale), PRIMER-03 (explicit regenerate only, flag never clobbers), PRIMER-04 (golden tests lock semantics — non-UI, noted for plan-phase).

This is a thin, high-clarity affordance: one stale banner plus its explicit Regenerate action on the existing `/deck-primer` page. No new page, no new tile, no new workflow step.

---

## 1. Surface overview — where the banner appears + when

**Host page:** `Views/Deck/DeckPrimer.cshtml` (3-step workflow: Step 1 import → Step 2 build → Step 3 results).

**Slot:** top of **Step 3 — Results** (`<section class="result-panel" id="primer-step-panel-3">`), inserted immediately after the `.chatgpt-step-heading` block and **before** the suggested-title / Deck Summary / `primer.txt` panels. Rationale: the stale flag is a statement about the *displayed artifact* (the generated primer in Step 3), so it sits with that artifact, not with the form controls.

**Secondary mirror (optional, plan-phase call):** the same banner markup may also render at the top of **Step 2** (`#primer-step-panel-2`, just under its step heading) so a user customizing sections sees the warning without scrolling to results. If mirrored, both instances are the *same* server-rendered flag — one boolean, two render sites — never two independent computations.

**When it renders (server-side gate):**

```
showStale = primerWasGenerated                      // Model.PrimerPromptText is non-empty
         && currentDeckHash != generatedPrimerHash  // multiset hash compare (PRIMER-02)
```

- `generatedPrimerHash` = the canonical name+quantity multiset hash captured at generation time (reuse the primer's existing cache-key computation — PRIMER-02) and round-tripped with the session (it already persists in the download/upload `.zip`, so the stored hash travels with the restored primer).
- `currentDeckHash` = the same hash recomputed over the deck currently loaded in Step 1.
- Banner is **purely a flag**: it never triggers a fetch or a rebuild on its own (PRIMER-03). Regeneration happens only when the user submits.

---

## 2. ASCII wireframe — stale banner in context (Step 3)

```
┌─ Step 3 ─────────────────────────────────────────────────────────────┐
│ Step 3                                            [ Paste-ready output ]│
│ Copy the generated primer prompt                                       │
│ The prompt below is the variant for the currently selected AI platform.│
│                                                                        │
│ ╓────────────────────────────────────────────────────────────────────╖ │  ← .deck-restored-notice
│ ┃▌ ⚠ Deck changed since this primer was generated — 3 cards differ.   ┃ │     .deck-restored-notice--stale
│ ┃▌   Regenerate to refresh the primer.        [ Regenerate primer ]   ┃ │     (gold left rail, role=status)
│ ╙────────────────────────────────────────────────────────────────────╜ │
│                                                                        │
│  ┌ Suggested ChatGPT conversation title ───────────────────  [Copy] ┐  │
│  │ Najeela cEDH — Combo Turbo Primer                                 │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│  ┌ Deck Summary ────────────────────────────────────────────────────┐  │
│  │ 100 cards · Najeela, the Blade-Blossom · 5C …                     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│  ┌ primer.txt ──────────────────────────────────────────────  [Copy] ┐  │
│  │ You are a Commander deck-primer writer …                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

**Fresh state (no banner):** the notice is simply absent; Step 3 renders exactly as it does today (byte-identical when fresh).

---

## 3. Components reused + net-new

**Reused (no new visual language):**
- `.deck-restored-notice` — flex row, space-between, `gap .75rem`, `border 1px var(--line)`, `border-left 3px var(--accent)`, `border-radius 12px`, `bg var(--panel)`, `color var(--muted)`. This is the cross-tool notice shape called out in UI-VOCABULARY line 20.
- **`div.warning-banner` tone** — the gold/amber caution rail (`border-left 4px #c8860b` in site-common). We adopt its *tone* via the theme token `--gold-warning` (#c8a040) rather than copying the hardcoded hex, so the stale notice reads as caution, not as the neutral/accent "restored" notice.
- `.run-button` — primary CTA, already has `:disabled` + busy handling; used for the Regenerate action (it is a real `type="submit"` that re-posts the primer form, i.e. the *existing* explicit generate action).
- `role="status"`, `.sr-only` — existing a11y primitives.

**Net-new (one class, modifier only, fully tokenized):**

```css
/* Stale-primer caution tone: reuses .deck-restored-notice geometry, swaps the
   neutral accent rail for the warning gold so "deck changed" reads as caution.
   Token-only — no hardcoded color — so all 22 themes inherit correctly. */
.deck-restored-notice--stale {
  border-left-color: var(--gold-warning, #c8a040);
  color: var(--ink);
}
```

That is the entire CSS surface for the feature. It belongs in `site-common.css` (layout/cross-cutting, inherited by all themes) per the theme-system constraint — no per-theme fork edits. The Regenerate button needs no new class (reuses `.run-button`); the message text needs no new class.

---

## 4. States

| State | Trigger | Render |
|-------|---------|--------|
| **never-generated** | `PrimerPromptText` empty (no primer yet) | No banner. Step 3 shows the existing "Generate the primer to see…" info-banner. |
| **fresh** | primer exists AND `currentDeckHash == generatedPrimerHash` (incl. reorder / printing-swap — same multiset → same hash) | **No banner.** Step 3 byte-identical to today. |
| **stale** | primer exists AND `currentDeckHash != generatedPrimerHash` (card add/remove or quantity change) | Banner visible: ⚠ + message naming the changed-card count + `[ Regenerate primer ]` CTA. |
| **post-regenerate** | user clicks Regenerate (or the normal Generate Primer) → form re-posts → new primer generated for the now-current deck → `generatedPrimerHash` re-captured | Hashes match again ⇒ banner **clears** on the re-rendered page. No client-side dismiss; the flag is server-truth and recomputes each render. |

**Invariant (PRIMER-03):** the banner never edits, replaces, or auto-rebuilds the displayed primer. The old primer stays put and copy-able until the user explicitly regenerates. There is no client-side "x to dismiss" — dismissing would lie about server truth; the only way to clear it is to regenerate (or revert the deck).

---

## 5. Exact microcopy

**Stale message (names the changed-card count, pluralized):**
- ≥2 changed: `Deck changed since this primer was generated — 3 cards differ. Regenerate to refresh the primer.`
- exactly 1 changed: `Deck changed since this primer was generated — 1 card differs. Regenerate to refresh the primer.`
- count-suppressed fallback (if a precise diff count is ever unavailable): `Deck changed since this primer was generated. Regenerate to refresh the primer.`

**Regenerate button label:** `Regenerate primer`

**Screen-reader prefix (visually-hidden, leads the status text):** `Status:` (so `role="status"` announces "Status: Deck changed since this primer was generated, 3 cards differ…").

**What NOT to say (PRIMER-03 — no auto-rebuild language):**
- ✗ "Auto-updating your primer…", "Refreshing automatically", "Syncing primer", "Live primer"
- ✗ "We rebuilt your primer", "Primer updated" (nothing changed until the user acts)
- ✗ "Out of date" framed as an error ("Error:", red/danger styling) — it is caution, not failure
- ✗ Any wording implying an upstream re-fetch happens on its own, or that the old primer was discarded.

Count copy must say **"differ" / "differs"** (added + removed + quantity-changed cards), not "added" alone, since the multiset diff spans all three change kinds.

---

## 6. Theme tokens

| Token | Use |
|-------|-----|
| `--gold-warning` (#c8a040) | stale left rail (caution tone) — net-new modifier |
| `--line` | banner border |
| `--accent` | fresh `.deck-restored-notice` rail (inherited; overridden only in `--stale`) |
| `--panel` | banner background |
| `--ink` | message text (raised from `--muted` for stale emphasis) |
| `--fs-sm` | Regenerate button text (via `.run-button` / pill conventions) |
| `--focus` (fallback `--accent`) | keyboard focus ring on the Regenerate button |

No hardcoded colors in the feature except the documented `--gold-warning` fallback hex, mirroring the existing token's own default.

---

## 7. Responsive

- `.deck-restored-notice` is already `display:flex; justify-content:space-between; gap:.75rem`. On narrow viewports the message can crowd the button.
- Plan-phase to add (in `site-common.css`, inside the existing mobile breakpoint, **only if** the live mobile check shows crowding): `.deck-restored-notice--stale { flex-wrap: wrap; }` so the `[ Regenerate primer ]` CTA wraps to a full-width second row under the message at ≤ ~480px. The message stays left-aligned; the button goes `width:100%` on wrap.
- The ⚠ glyph + text must never truncate; only the layout reflows.
- Verify at 2 viewports (desktop ~1280, mobile ~390) across `site.css` / `site-azorius.css` / `site-nyx.css` per the UI-phase visual-verify rule.

---

## 8. Accessibility

- **Live region:** `role="status"` (polite) on the banner `<div>` — it is informational caution, NOT an error, so `role="alert"` (assertive, interrupts) is wrong here. It announces when it appears after a re-render without stealing focus.
- **Not color-only:** the meaning is carried by (a) the `⚠` glyph, (b) the literal text "Deck changed…", and (c) the gold rail — color is the *third* signal, not the only one. Passes WCAG 1.4.1.
- **Visually-hidden label:** an `.sr-only` "Status:" prefix gives screen-reader users immediate context.
- **Focus management:** the banner does **not** grab focus on render (no focus-stealing on a passive notice). The `[ Regenerate primer ]` button is a normal tab stop with the universal `:focus-visible` ring (`--focus`). After regenerate, the page re-renders fresh (banner gone) and focus follows the normal post-submit flow (the existing busy-indicator → results); plan-phase may move focus to the `#primer-output` heading on successful regenerate so SR users land on the refreshed artifact.
- **Contrast:** `--ink` on `--panel` already meets AA in all themes; the gold rail is decorative reinforcement, not load-bearing for contrast.
- **Button is a real submit**, not a JS-only control — works without scripting and is keyboard-operable by default.

---

## 9. Staleness is a hash compare — banner must NOT fire on cosmetic edits (PRIMER-02)

The stale gate compares the **canonical card-name + quantity multiset hash**, reusing the primer's existing cache-key computation:

- **Reordering** cards in the list → identical multiset → identical hash → **fresh** (no banner).
- **Swapping a printing** (same card name, different set/collector number) → canonical name unchanged, quantity unchanged → identical hash → **fresh** (no banner).
- **Adding / removing a card**, or **changing a quantity** → multiset changes → hash differs → **stale** (banner shows).

The UI must therefore drive its visibility *solely* off the server-computed hash-inequality boolean — it must not, for example, mark stale on any raw-text change or on a printing field. The changed-card count shown in the microcopy is the multiset diff cardinality (added + removed + quantity-delta entries), so a pure reorder/printing-swap yields a count of 0 and the banner is suppressed entirely (consistent with "fresh").

---

## 10. Open questions for plan-phase

1. **Mirror at Step 2?** Render the banner only at Step 3 results, or also mirror at the top of Step 2? (Spec defaults to Step 3 only; one flag, optional second render site.)
2. **Hash persistence across resume:** confirm the `.zip` session round-trip stores the generated-primer hash so a *restored* primer can also detect staleness against a re-imported deck. If not currently persisted, plan a `{ get; init; }` field on the persisted request/result (carve-out: never convert to get-only — STJ skips it).
3. **Changed-card count source:** does the existing cache-key path expose a per-card multiset cheaply enough to compute the diff *cardinality*, or only the final hash? If only the hash, fall back to the count-suppressed microcopy variant (§5) rather than computing a second pass.
4. **Focus-on-regenerate:** move focus to `#primer-output` after a successful regenerate, or leave default post-submit flow? (a11y nicety, not required.)
5. **Mobile wrap:** confirm via live 390px screenshot whether `flex-wrap: wrap` on the stale modifier is actually needed before adding the rule.
6. **Flag gating:** is this behind a namespaced feature flag (e.g. `tool.primer.stale-flag`) seeded OFF for prod byte-identity, consistent with TAP-04 / sibling phases? REQUIREMENTS lists no flag for PRIMER, but the cycle convention is flag-gated — confirm.
