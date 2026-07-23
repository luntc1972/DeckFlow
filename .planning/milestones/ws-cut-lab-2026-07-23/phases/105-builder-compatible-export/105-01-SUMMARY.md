---
phase: 105-builder-compatible-export
plan: 01
status: complete
commit: 5f70a792
---

# 105-01 Summary — OriginalEntries baseline (EXPORT-02)

**Built:** Capture-once `CutLabState.OriginalEntries` — the immutable baseline of the
original imported deck entries (name, quantity, board, set/collector/category),
captured at intake and preserved across scenario reload. Serializer clamps it like the
other bounded collections; empty initializer keeps pre-105 JSON blobs deserializing.

**Why:** The builder-compatible CUT/ADD patch (105-03) diffs the finished 100-card
working list against what the user originally imported, so that original list must be
captured once and travel with the session rather than being re-derived from the
(mutated) working list.

**Verification:** 6/6 new baseline tests; ~CutLab 267/267 at wave close. Blind-verifier PASS.

**Deviations:** none.
