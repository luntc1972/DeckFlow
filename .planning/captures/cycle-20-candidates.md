# Cycle 20 Candidates (Backlog)

Captured 2026-07-24 while planning Phase 111 (Cut Lab Upgrade Regression Gate, last of Cycle 19).

---

## C20-01 — Cut Lab beginner guidance density (view-mode vs progressive disclosure)

**Origin:** User question during Phase 111 planning — "Deck Analysis has Full/Compact/Advanced views (Full gives more directions); could that be utilized in Cut Lab, to allow longer instructions for beginning users?"

**Research findings (grounded, 2026-07-24):**

- The Deck Analysis picker is `data-prompt-ui-mode-button` → `guided`/`focused`/`expert`
  (`DeckAnalysis.cshtml:89-91`), driven by CSS in `site.css:389-430` and TS in `deck-sync.ts`.
- The mechanism is **subtractive, not additive**: `guided` (Full) is the baseline showing all
  already-authored guidance (step eyebrows/badges/notes/context-notes, helper panels,
  `.prompt-instructions`); `focused` (Compact) and `expert` (Advanced) `display:none` progressively
  more of it. Full adds no text — the guidance is always authored in the view.
- So the mode does **not** directly "allow longer instructions." Its real value is as a
  **permission structure**: it lets you author richer beginner guidance without taxing experts,
  because experts strip it via Advanced. Content still has to be written.

**Value assessment:** marginal-to-moderate, with an overlap caution.

- Cut Lab is genuinely more complex than the prompt builder (pools, roles, packages, cut rounds,
  what-if, structural/combo findings) → a higher beginner-guidance ceiling is justified.
- **But Cycle 19 already shipped the opposite pattern**: collapsibles (CLUP-13), text-first
  disclosures (CLUP-16), package help block (CLUP-15) — all *progressive disclosure*
  (minimal-first, expand-for-more). The prompt-ui-mode is *maximal-first, strip-for-experts*.
  Two density systems with opposite defaults = confusing overlap. Reconcile before adopting both.

**Two options (pick one, not both):**

1. **(Recommended) Beginner "How Cut Lab works" help block** — lean into the existing disclosure
   system. Add a beginner-oriented overview panel, expanded on first visit, collapse state
   remembered (same localStorage pattern as CLUP-13). Lower risk, no second density framework,
   directly answers "longer instructions for beginners."
2. **Global Full/Compact/Advanced toggle** — port the `prompt-ui-mode` pattern to Cut Lab
   (picker partial + CSS + TS). Low reuse cost, but you still author the content AND must
   reconcile it with the per-section collapsibles so users don't face two competing "hide" controls.

**Effort:** Option 1 small; Option 2 moderate. **Not Phase 111** (regression gate — no feature work).

---

## C20-02 — Cut Lab theme-readability enforcement (follow-through from Phase 111 CLUP-19)

Phase 111 adds the first *automated* all-theme contrast regression (`cut-lab-theme-readability.spec.ts`
+ `e2e/support/contrast.ts`). Today contrast is disciplined but only **documented, not enforced** —
e.g. `site-nyx.css` `--accent-contrast` carries a hand-measured "white-on-accent 4.19:1 fails 4.5:1;
dark purple-black clears 4.73:1" note. Candidate: generalize the Phase 111 contrast harness beyond
Cut Lab to a **site-wide** all-theme readability gate covering shared components (hub cards, nav,
result panels, forms) across all 22 guild themes, so the manual per-token WCAG notes become a CI check.

---

## C20-03 — EDHREC community deck delta substrate and first surface

**Origin:** User research thread 2026-07-24 after downloading EDHREC's published `data.tgz`
and `averages.tgz` archives.

**Full capture:** `.planning/captures/edhrec-data-feature-plans-2026-07-24.md`

**Value thesis:** Do not recreate EDHREC pages. Use EDHREC's sanctioned aggregate data to explain
the user's exact deck: present/missing commander staples, commander-signature cards, generic staple
pressure, off-meta cards, and confidence/support. This fits DeckFlow's one-round-trip prompt artifact
value better than a generic population dashboard.

**Recommended first milestone slice:**

1. EDHREC community-stat substrate for local `averages.csv` + `edhrec.csv` imports.
2. Confidence/support labels, including partner-denominator caveats.
3. Signature-vs-staple classifier using commander inclusion, global rate, and ubiquity.
4. Deck Analysis "Community Delta" panel + prompt-artifact block.

**Effort:** Medium-Large for the substrate, then Medium for the first user-visible surface.
Small follow-ons include Commander Fit and confidence labels; Large/Large-XL follow-ons include
missing-role analysis, budget replacements, commander comparison, and precon-effect detection.

**Follow-on candidates from the capture:** missing-role-not-missing-card, Cut Lab statistical
evidence, uniqueness/homogenization score, budget replacement finder, commander fit score,
precon-effect detector, and commander comparison/build-direction tool.
