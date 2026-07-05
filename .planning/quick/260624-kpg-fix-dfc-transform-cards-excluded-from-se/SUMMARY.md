---
slug: 260624-kpg-fix-dfc-transform-cards-excluded-from-set-packet
status: complete
completed: 2026-07-05
---

# Summary: Fix DFC/transform cards excluded from set-packet top-60

**Outcome:** Shipped. Transform / MDFC cards (parent oracle_text null, real
text/cost on `card_faces[]`) are now face-aware scored and included in the
set-packet top-60 when on-theme, instead of being silently dropped.

**Shipping commits:**
- `06d9bf06 fix(set-packet): include transform/MDFC cards via face-aware scoring` — primary fix.
- `582b9734 fix(set-packet): gate DFC P/T fallback to parent-empty case; prove cut closes` — follow-up correctness gate.
- `a01473cc test(set-packet): cover transform-card inclusion and single-face regression` — regression tests.

A detailed executor summary already exists in this dir as
`260624-kpg-SUMMARY.md`; this file is the audit-recognized closure marker
(`status: complete`). Verified via `git log`.
