---
slug: 260627-flag-key-namespacing
status: complete
completed: 2026-07-05
---

# Summary: Flag-key namespacing + admin "feature"->"tool"

**Outcome:** Shipped. Feature-flag keys were reorganized into consistent
functional prefixes (`tool.*` / `service.*` / `analysis.*`) with an idempotent,
prod-state-preserving rename migration, and the admin Flags page copy was
relabeled from "Feature" to "Tool".

**Shipping commits:**
- `2d8b1a7b refactor(flags): namespace flag keys into tool./service./analysis.` — the 14-key rename.
- `3145eabd test(flags): cover flag-key rename migration` — migration test (fresh/existing/re-run/both-present).
- `0d8dbbb1 feat(admin): describe tool.* feature flags on Admin Flags page` and `bb2e96e8 fix(admin-flags): drop trailing dot from namespace pill labels` — admin UI copy.
- `cbf731de refactor(flags): namespace the plain-language-verdict flag key too` and `38cadf4b refactor(flags): namespace commander-castability into analysis.* + migrate` — follow-on key migrations.

(Project memory records the merge as `ebec49df`.) Closure record added
2026-07-05 during the Cycle 15 pre-close audit. Verified via `git log`.
