# 0001 — Per-platform prompt variants are intentionally decoupled

Date: 2026-05-27 (decision), 2026-06-03 (recorded)

## Context

The Analysis and MetaGap prompt builders each have three platform variants
(`DeckFlow.Web/Services/PromptBuilders/{Analysis,MetaGap}/{ChatGpt,Claude,Gemini}*PromptVariant.cs`).
Platform-neutral guidance prose — the MDFC-land rule, win-turn/bracket weighting, and
`deck_profile` field descriptions — appears in all three variants of each family, with
small wording differences (e.g. the Claude variant joins the MDFC sentence with ", and"
and omits the `- ` bullet prefix).

Code reviewers repeatedly flag this as duplication/drift. A consolidation was attempted
on 2026-05-27: commit `a1fa5ad` extracted an `AnalysisPromptShared` constants holder and
normalized the Claude wording to match ChatGPT/Gemini. It was reverted the same day in
`b2ffba7`.

The wording differences are platform-format adaptations, not decay: the Claude variant's
`<task>` block is prose-without-bullets (Anthropic XML-tag prompt style), while the
ChatGPT and Gemini variants use markdown bullet lists. The semantic content is identical.

## Decision

Keep the per-platform prompt variants fully decoupled. Do not extract shared guidance
text, constants holders, or base prompt builders across the ChatGpt/Claude/Gemini
variants. Each variant owns its complete prompt text, including sentences that happen to
be near-identical across platforms.

Prompt bytes are the product (the core value is paste-ready, one-round-trip prompts), so
each platform's prompt must remain independently tunable without risk of a shared-text
change silently altering another platform's output.

## Consequences

- A guidance-content change (as opposed to formatting) must be hand-applied to every
  variant in the family — up to 3 edits for Analysis, 3 for MetaGap. Reviewers should
  verify all variants received the change.
- Reviews and automated cleanup passes must not report cross-variant prose duplication
  or wording differences as findings (re-flagged and re-confirmed during the 2026-06-03
  /simplify pass).
- Wording may legitimately differ between variants where it serves the platform's prompt
  format; only semantic divergence (different rules or thresholds) is a defect.
