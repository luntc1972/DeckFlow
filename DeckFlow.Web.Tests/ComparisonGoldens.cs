namespace DeckFlow.Web.Tests;

/// <summary>Golden constants for <see cref="DeckComparisonByteIdentityTests"/>, captured from a real
/// DeckComparisonService.BuildAsync run against the unrefactored service (no hand-typed goldens).
/// The live `generated_at_utc: ...Z` timestamp (DeckComparisonService.cs:576) is normalized to a
/// fixed placeholder, and all "\r\n" line endings are normalized to "\n" (captured via Windows
/// dotnet.exe; CI runs ubuntu-latest) — see PacketByteIdentityFixtures.NormalizeForGoldenComparison.</summary>
internal static class ComparisonGoldens
{
    public static string BaselineComparisonPrompt(string platform) => platform switch
    {
        "ChatGPT" => """
Title this chat: Kraum, Ludevic's Opus vs Kraum, Ludevic's Opus | Deck Comparison

You are an expert Magic: The Gathering deck analyst specializing in Commander.

## TASK
Based only on the provided deck contents and supplied context, compare the decks in a typical multiplayer Commander environment.
Provide a grounded, evidence-based comparison instead of a speculative matchup prediction.
Read all supplied deck data and context before beginning the comparison.

## RULES
- Treat the supplied decklists, commander names, bracket selections, combo findings, and derived comparison context as the source of truth.
- Do not invent cards, colors, commander identities, or card text not supported by the provided context.
- Do not assume a card's role unless it is supported by the deck contents or provided context.
- Do not claim exact card text unless it is included in the packet.
- If a conclusion is not well-supported by the provided deck contents, say that explicitly instead of guessing.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- When uncertain, mark the statement as low-confidence and add the reason to confidence_notes.
- For each major conclusion, reference the deck patterns, card packages, or commander incentives that support it.
- Base conclusions on observable deck construction rather than vague impressions.
- Do not make claims about exact metagames unless explicitly provided.
- If the two decks target different brackets, note the mismatch prominently and explain how it affects the comparison.

## COMPARISON AXES
For each axis, write 2-4 sentences comparing the two decks. State the conclusion first, then the evidence.
- Commander role and game plan for Kraum Value
- Commander role and game plan for Atraxa Superfriends
- Speed and setup tempo
- Ramp
- Draw
- Spot interaction
- Sweepers
- Recursion
- Closing power, including complete combos and near-combos as part of the win-condition comparison
- Resilience
- Consistency
- Mana stability
- Dependence on commander
- Likely table fit
- Major overlap and major differences

## OUTPUT FORMAT
Structure your response as follows:

A. Readable comparison — one subsection per axis above, then a concise side-by-side summary.
B. Five concrete cards or packages that best explain the gap between the two decks, with one sentence of reasoning each.
C. Final verdict — which deck is stronger overall and why, in 2-4 sentences.
D. You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block. The top-level object must be named deck_comparison.


JSON requirements:
- Return valid JSON only inside the fenced ```json code block.
- Do not include comments in the JSON.
- Do not omit required fields.
- Use arrays instead of prose where appropriate.
- The JSON must match this schema exactly:

```json
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
```

## DECK A
Name: Kraum Value
Commander: Kraum, Ludevic's Opus
Bracket: Bracket 3: Upgraded
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Bracket turn expectation: Expect to play at least six turns before you win or lose.
Normalized decklist:
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Sol Ring

Combo summary:
Kraum Value combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)

## DECK B
Name: Atraxa Superfriends
Commander: Kraum, Ludevic's Opus
Bracket: Bracket 3: Upgraded
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Bracket turn expectation: Expect to play at least six turns before you win or lose.
Normalized decklist:
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Counterspell
1 Sol Ring

Combo summary:
Atraxa Superfriends combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)

## COMPARISON CONTEXT
comparison_context:
generated_at_utc: <TIMESTAMP>

commander_bracket_definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.

deck_a:
  name: Kraum Value
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=1, draw=0, interaction=0, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

deck_b:
  name: Atraxa Superfriends
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=0, draw=0, interaction=1, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

comparison_signals:
shared_categories: (none)
ramp_gap: Kraum Value 1 vs Atraxa Superfriends 0
draw_gap: Kraum Value 0 vs Atraxa Superfriends 0
interaction_gap: Kraum Value 0 vs Atraxa Superfriends 1
wipe_gap: Kraum Value 0 vs Atraxa Superfriends 0
recursion_gap: Kraum Value 0 vs Atraxa Superfriends 0
closing_power_gap: Kraum Value 0 vs Atraxa Superfriends 0
combo_gap: Kraum Value 0 complete combos vs Atraxa Superfriends 0 complete combos
average_mana_value_gap: Kraum Value 1.50 vs Atraxa Superfriends 1.50
""",
        "Claude" => """
<role>
You are an expert Magic: The Gathering deck analyst specializing in Commander.
</role>

<deck_a>
  <name>Kraum Value</name>
  <commander>Kraum, Ludevic's Opus</commander>
  <bracket>
    <label>Bracket 3: Upgraded</label>
    <summary>Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.</summary>
    <turn_expectation>Expect to play at least six turns before you win or lose.</turn_expectation>
  </bracket>
  <list>
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Sol Ring
  </list>
  <combos>
Kraum Value combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)
  </combos>
</deck_a>

<deck_b>
  <name>Atraxa Superfriends</name>
  <commander>Kraum, Ludevic's Opus</commander>
  <bracket>
    <label>Bracket 3: Upgraded</label>
    <summary>Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.</summary>
    <turn_expectation>Expect to play at least six turns before you win or lose.</turn_expectation>
  </bracket>
  <list>
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Counterspell
1 Sol Ring
  </list>
  <combos>
Atraxa Superfriends combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)
  </combos>
</deck_b>

<comparison_context>
comparison_context:
generated_at_utc: <TIMESTAMP>

commander_bracket_definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.

deck_a:
  name: Kraum Value
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=1, draw=0, interaction=0, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

deck_b:
  name: Atraxa Superfriends
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=0, draw=0, interaction=1, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

comparison_signals:
shared_categories: (none)
ramp_gap: Kraum Value 1 vs Atraxa Superfriends 0
draw_gap: Kraum Value 0 vs Atraxa Superfriends 0
interaction_gap: Kraum Value 0 vs Atraxa Superfriends 1
wipe_gap: Kraum Value 0 vs Atraxa Superfriends 0
recursion_gap: Kraum Value 0 vs Atraxa Superfriends 0
closing_power_gap: Kraum Value 0 vs Atraxa Superfriends 0
combo_gap: Kraum Value 0 complete combos vs Atraxa Superfriends 0 complete combos
average_mana_value_gap: Kraum Value 1.50 vs Atraxa Superfriends 1.50
</comparison_context>

<output_schema>
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
</output_schema>

<task>
Based only on the provided deck contents and supplied context, compare the decks in a typical multiplayer Commander environment.
Provide a grounded, evidence-based comparison instead of a speculative matchup prediction.
Read all supplied deck data and context before beginning the comparison.
Treat the supplied decklists, commander names, bracket selections, combo findings, and derived comparison context as the source of truth.
Do not invent cards, colors, commander identities, or card text not supported by the provided context.
Do not assume a card's role unless it is supported by the deck contents or provided context.
Do not claim exact card text unless it is included in the packet.
If a conclusion is not well-supported by the provided deck contents, say that explicitly instead of guessing.
If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
When uncertain, mark the statement as low-confidence and add the reason to confidence_notes.
For each major conclusion, reference the deck patterns, card packages, or commander incentives that support it.
Base conclusions on observable deck construction rather than vague impressions.
Do not make claims about exact metagames unless explicitly provided.
If the two decks target different brackets, note the mismatch prominently and explain how it affects the comparison.

Write readable comparison prose first with:
- Commander role and game plan for Kraum Value
- Commander role and game plan for Atraxa Superfriends
- Speed and setup tempo
- Ramp
- Draw
- Spot interaction
- Sweepers
- Recursion
- Closing power, including complete combos and near-combos as part of the win-condition comparison
- Resilience
- Consistency
- Mana stability
- Dependence on commander
- Likely table fit
- Major overlap and major differences
- Five concrete cards or packages that best explain the gap between the two decks, with one sentence of reasoning each.
- A final verdict naming which deck is stronger overall and why, in 2-4 sentences.
After the readable comparison, return a single JSON object matching <output_schema>.
You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block. The top-level object must be named deck_comparison.
</task>
""",
        "Gemini" => """
You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.
You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.

Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.

Title this chat: Kraum, Ludevic's Opus vs Kraum, Ludevic's Opus | Deck Comparison

## TASK
Based only on the provided deck contents and supplied context, compare the decks in a typical multiplayer Commander environment.
Provide a grounded, evidence-based comparison instead of a speculative matchup prediction.
Read all supplied deck data and context before beginning the comparison.

## RULES
- Treat the supplied decklists, commander names, bracket selections, combo findings, and derived comparison context as the source of truth.
- Do not invent cards, colors, commander identities, or card text not supported by the provided context.
- Do not assume a card's role unless it is supported by the deck contents or provided context.
- Do not claim exact card text unless it is included in the packet.
- If a conclusion is not well-supported by the provided deck contents, say that explicitly instead of guessing.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- When uncertain, mark the statement as low-confidence and add the reason to confidence_notes.
- For each major conclusion, reference the deck patterns, card packages, or commander incentives that support it.
- Base conclusions on observable deck construction rather than vague impressions.
- Do not make claims about exact metagames unless explicitly provided.
- If the two decks target different brackets, note the mismatch prominently and explain how it affects the comparison.

## COMPARISON AXES
For each axis, write 2-4 sentences comparing the two decks. State the conclusion first, then the evidence.
- Commander role and game plan for Kraum Value
- Commander role and game plan for Atraxa Superfriends
- Speed and setup tempo
- Ramp
- Draw
- Spot interaction
- Sweepers
- Recursion
- Closing power, including complete combos and near-combos as part of the win-condition comparison
- Resilience
- Consistency
- Mana stability
- Dependence on commander
- Likely table fit
- Major overlap and major differences

## OUTPUT FORMAT
Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.

Structure your readable analysis (placed BEFORE the <result> wrapper) as follows:

A. Readable comparison — one subsection per axis above, then a concise side-by-side summary.
B. Five concrete cards or packages that best explain the gap between the two decks, with one sentence of reasoning each.
C. Final verdict — which deck is stronger overall and why, in 2-4 sentences.
D. You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block. The top-level object must be named deck_comparison.

Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.

JSON requirements:
- Return valid JSON only inside the fenced ```json code block.
- Do not include comments in the JSON.
- Do not omit required fields.
- Use arrays instead of prose where appropriate.
- The JSON must match this schema exactly:

```json
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
```

## DECK A
Name: Kraum Value
Commander: Kraum, Ludevic's Opus
Bracket: Bracket 3: Upgraded
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Bracket turn expectation: Expect to play at least six turns before you win or lose.
Normalized decklist:
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Sol Ring

Combo summary:
Kraum Value combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)

## DECK B
Name: Atraxa Superfriends
Commander: Kraum, Ludevic's Opus
Bracket: Bracket 3: Upgraded
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Bracket turn expectation: Expect to play at least six turns before you win or lose.
Normalized decklist:
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Counterspell
1 Sol Ring

Combo summary:
Atraxa Superfriends combos
Commander bracket: Bracket 3: Upgraded
Complete combos: 0
Near-combos: 0

Key combos:
(none found)

Near-combos:
(none found)

## COMPARISON CONTEXT
comparison_context:
generated_at_utc: <TIMESTAMP>

commander_bracket_definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.

deck_a:
  name: Kraum Value
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=1, draw=0, interaction=0, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

deck_b:
  name: Atraxa Superfriends
  commander: Kraum, Ludevic's Opus
  bracket: Bracket 3: Upgraded
  bracket_summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
  bracket_turn_expectation: Expect to play at least six turns before you win or lose.
  mainboard_cards: 2
  lands: 0
  creatures: 0
  average_mana_value: 1.50
  mana_curve: 0-1=1, 2=1, 3=0, 4=0, 5+=0
  color_identity: R, U
  categories: (none detected)
  role_counts: ramp=0, draw=0, interaction=1, wipes=0, recursion=0, closing_power=0
  combos_included: 0
  combos_almost_included: 0
  key_combos: (none found)
  almost_combos: (none found)

comparison_signals:
shared_categories: (none)
ramp_gap: Kraum Value 1 vs Atraxa Superfriends 0
draw_gap: Kraum Value 0 vs Atraxa Superfriends 0
interaction_gap: Kraum Value 0 vs Atraxa Superfriends 1
wipe_gap: Kraum Value 0 vs Atraxa Superfriends 0
recursion_gap: Kraum Value 0 vs Atraxa Superfriends 0
closing_power_gap: Kraum Value 0 vs Atraxa Superfriends 0
combo_gap: Kraum Value 0 complete combos vs Atraxa Superfriends 0 complete combos
average_mana_value_gap: Kraum Value 1.50 vs Atraxa Superfriends 1.50

MANDATORY — DO NOT SKIP: Your response MUST end with a <result>...</result> block containing a single JSON object that matches the schema above. The JSON block is REQUIRED even if you have already produced a complete readable analysis — without it your response is invalid and DeckFlow will reject the upload. Do not summarise. Do not say "and the JSON is...". Output the literal <result> tag, then the JSON object, then </result>. Nothing else after </result>.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static string BaselineFollowUpPrompt(string platform) => platform switch
    {
        "ChatGPT" => """
You are an expert Magic: The Gathering deck analyst specializing in Commander.

## TASK
Revise the existing deck comparison using the follow-up questions and answers in this chat.
Re-read the original decklists and packet context before revising.

## RULES
- Preserve the original comparison structure: readable summary, side-by-side comparison, verdict, then JSON.
- Incorporate the new follow-up Q&A without contradicting the supplied deck contents or packet context.
- Keep using the decklists and packet context as the source of truth.
- Do not invent cards, colors, or card text not supported by the provided context.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- If a new conclusion is uncertain, mark it as low-confidence and explain why in confidence_notes.
- For each revised conclusion, reference the deck patterns, card packages, or commander incentives that support it.

## COMPARISON AXES
Re-evaluate every axis from the original comparison where the follow-up information is relevant:
commander role, game plan, speed, ramp, draw, spot interaction, sweepers, recursion, closing power, resilience, consistency, mana stability, commander dependence, and table fit.

## OUTPUT FORMAT
- Return the updated readable comparison with 2-4 sentences per axis that changed.
- Include a revised verdict.
- Then regenerate the full JSON inside a fenced ```json code block (triple-backtick json) with the top-level object named deck_comparison. Do not return raw JSON outside a code block.
- Keep the JSON valid and include every required field from this schema:

```json
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
```
""",
        "Claude" => """
<role>
You are an expert Magic: The Gathering deck analyst specializing in Commander.
</role>

<output_schema>
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
</output_schema>

<task>
Revise the existing deck comparison using the follow-up questions and answers in this chat.
Re-read the original decklists and packet context before revising.
Preserve the original comparison structure: readable summary, side-by-side comparison, verdict, then JSON.
Incorporate the new follow-up Q&A without contradicting the supplied deck contents or packet context.
Keep using the decklists and packet context as the source of truth.
Do not invent cards, colors, or card text not supported by the provided context.
If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
If a new conclusion is uncertain, mark it as low-confidence and explain why in confidence_notes.
For each revised conclusion, reference the deck patterns, card packages, or commander incentives that support it.
Return updated readable comparison prose first with 2-4 sentences per axis that changed, then a revised verdict.
After the readable revision, return a single JSON object matching <output_schema>.
Regenerate the full JSON inside a fenced ```json code block (triple-backtick json) with the top-level object named deck_comparison. Do not return raw JSON outside a code block.
</task>
""",
        "Gemini" => """
You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.
You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.

Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.

## TASK
Revise the existing deck comparison using the follow-up questions and answers in this chat.
Re-read the original decklists and packet context before revising.

## RULES
- Preserve the original comparison structure: readable summary, side-by-side comparison, verdict, then JSON.
- Incorporate the new follow-up Q&A without contradicting the supplied deck contents or packet context.
- Keep using the decklists and packet context as the source of truth.
- Do not invent cards, colors, or card text not supported by the provided context.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- If a new conclusion is uncertain, mark it as low-confidence and explain why in confidence_notes.
- For each revised conclusion, reference the deck patterns, card packages, or commander incentives that support it.

## COMPARISON AXES
Re-evaluate every axis from the original comparison where the follow-up information is relevant:
commander role, game plan, speed, ramp, draw, spot interaction, sweepers, recursion, closing power, resilience, consistency, mana stability, commander dependence, and table fit.

## OUTPUT FORMAT
Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.
- Return the updated readable comparison with 2-4 sentences per axis that changed.
- Include a revised verdict.
- Then regenerate the full JSON inside a fenced ```json code block (triple-backtick json) with the top-level object named deck_comparison. Do not return raw JSON outside a code block.
- Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.
- Keep the JSON valid and include every required field from this schema:

```json
{
  "deck_comparison":   {
    "deck_a_name": "Kraum Value",
    "deck_b_name": "Atraxa Superfriends",
    "deck_a_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_b_commander": "Kraum, Ludevic\u0027s Opus",
    "deck_a_gameplan": "",
    "deck_b_gameplan": "",
    "deck_a_bracket": "Bracket 3: Upgraded",
    "deck_b_bracket": "Bracket 3: Upgraded",
    "shared_themes": [],
    "major_differences": [],
    "deck_a_strengths": [],
    "deck_b_strengths": [],
    "deck_a_weaknesses": [],
    "deck_b_weaknesses": [],
    "speed_comparison": "",
    "resilience_comparison": "",
    "interaction_comparison": "",
    "mana_consistency_comparison": "",
    "closing_power_comparison": "",
    "combo_comparison": "",
    "overall_verdict": "",
    "key_gap_cards_or_packages": [],
    "deck_a_key_combos": [],
    "deck_b_key_combos": [],
    "recommended_for": {
      "deck_a": [],
      "deck_b": []
    },
    "confidence_notes": []
  }
}
```

MANDATORY — DO NOT SKIP: Your response MUST end with a <result>...</result> block containing a single JSON object that matches the schema above. The JSON block is REQUIRED even if you have already produced a complete readable analysis — without it your response is invalid and DeckFlow will reject the upload. Do not summarise. Do not say "and the JSON is...". Output the literal <result> tag, then the JSON object, then </result>. Nothing else after </result>.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static string BaselineRequestContextText(string platform) => platform switch
    {
        "ChatGPT" => """
workflow_step: 2
deck_a_name: Kraum Value
deck_b_name: Atraxa Superfriends
deck_a_bracket: Upgraded
deck_b_bracket: Upgraded
target_ai_platform: ChatGPT

""",
        "Claude" => """
workflow_step: 2
deck_a_name: Kraum Value
deck_b_name: Atraxa Superfriends
deck_a_bracket: Upgraded
deck_b_bracket: Upgraded
target_ai_platform: Claude

""",
        "Gemini" => """
workflow_step: 2
deck_a_name: Kraum Value
deck_b_name: Atraxa Superfriends
deck_a_bracket: Upgraded
deck_b_bracket: Upgraded
target_ai_platform: Gemini

""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static readonly string PrintedNameFallbackDeckAListText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Sol Ring
1 Perfect Defense // Denting Blows [printed as: Ya viene el coco]
""";

    public static readonly string NoCommanderSectionDeckAListText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Counterspell
1 Sol Ring
""";
}
