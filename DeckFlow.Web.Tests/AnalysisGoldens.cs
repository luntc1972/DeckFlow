using DeckFlow.Web.Services;

namespace DeckFlow.Web.Tests;

/// <summary>Golden constants for <see cref="DeckAnalysisByteIdentityTests"/>, captured from a real
/// DeckAnalysisPacketService.BuildAsync run against the unrefactored service. Every value below was
/// captured verbatim from a real test run (see 83-01-PLAN.md's golden-capture-integrity instruction) —
/// none of this text was hand-typed or approximated. The live `generated_at_utc: ...Z` timestamp
/// embedded by DeckAnalysisPacketService.cs:1253 is normalized to a fixed placeholder, and all
/// "\r\n" line endings are normalized to "\n" (see PacketByteIdentityFixtures.NormalizeForGoldenComparison)
/// so this suite is OS-independent (captured via Windows dotnet.exe; CI runs ubuntu-latest).</summary>
internal static class AnalysisGoldens
{
    private const string ChatGptImmediateHeader = PacketByteIdentityFixtures.ChatGptImmediateHeader;

    public static string BaselineAnalysisPrompt(string platform) => platform switch
    {
        "ChatGPT" => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        "Claude" => """
<role>
You are an expert Magic: The Gathering deck analyst specializing in Commander.
</role>

<commander>Kraum, Ludevic's Opus</commander>

<bracket>
target_bracket: Bracket 3: Upgraded
summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
turns_expectation: Expect to play at least six turns before you win or lose.
All bracket options:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
</bracket>

<deck>
format: Commander
decklist:
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
</deck>

<reference>
  <cards>
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.
  </cards>
  <banlist>
official_commander_banned_cards: Dockside Extortionist, Mana Crypt
  </banlist>
</reference>

<questions>
1. What are the strengths and weaknesses of this deck?
</questions>

<output_schema>
{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}
</output_schema>

<task>
Read every section above before responding.
Use the mechanic definitions and card reference in <reference><cards> as authoritative. Do not invent card text or rules.
When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly. Label inferences from deck construction, curve, redundancy, or play patterns explicitly.
If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
Do not recommend cards listed in <reference><banlist>.
Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base, and weight them higher than a plain land since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

Answer every numbered question in <questions> with 6-12 sentences of detailed reasoning that cites specific cards from <deck> or <reference>. Do not skip, merge, or partially answer any question.
After writing the readable analysis, copy every answer into the JSON object's question_answers array with the same numbering and the same full answer text expanded to JSON form.
Return a Requested Question Answers section first, then recommendation sections for Top Adds and Top Cuts before the final structured output.


After the full analysis, return a JSON object matching <output_schema> with one question_answers entry per question, in the same order as <questions>.
The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
Field-level detail requirements for the deck_profile JSON:
- game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
- speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
- estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
- can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
- assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
- bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
- strengths: each item should be 1-2 sentences with a specific card or interaction reference.
- weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
- deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
- weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
</task>
""",
        "Gemini" => """
You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.
You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.

Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.

Title this chat: Kraum, Ludevic's Opus | Deck Analysis

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.

Structure your readable analysis (placed BEFORE the <result> wrapper) as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.

Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.

   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring

MANDATORY — DO NOT SKIP: Your response MUST end with a <result>...</result> block containing a single JSON object that matches the schema above. The JSON block is REQUIRED even if you have already produced a complete readable analysis — without it your response is invalid and DeckFlow will reject the upload. Do not summarise. Do not say "and the JSON is...". Output the literal <result> tag, then the JSON object, then </result>. Nothing else after </result>.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static string BaselineReferenceText(string platform) => platform switch
    {
        "ChatGPT" => """
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.
""",
        "Claude" => """
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.
""",
        "Gemini" => """
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static string BaselineRequestContextText(string platform) => platform switch
    {
        "ChatGPT" => """
workflow_step: 2
format: Commander
deck_name: 
commander: Kraum, Ludevic's Opus
target_commander_bracket: Upgraded
target_ai_platform: ChatGPT
include_candidate_references_in_analysis: False
card_specific_question_card_names:
budget_upgrade_amount: 
selected_analysis_questions:
- strengths-weaknesses
selected_set_codes:

deck_source:
https://www.moxfield.com/decks/byte-identity-baseline

""",
        "Claude" => """
workflow_step: 2
format: Commander
deck_name: 
commander: Kraum, Ludevic's Opus
target_commander_bracket: Upgraded
target_ai_platform: Claude
include_candidate_references_in_analysis: False
card_specific_question_card_names:
budget_upgrade_amount: 
selected_analysis_questions:
- strengths-weaknesses
selected_set_codes:

deck_source:
https://www.moxfield.com/decks/byte-identity-baseline

""",
        "Gemini" => """
workflow_step: 2
format: Commander
deck_name: 
commander: Kraum, Ludevic's Opus
target_commander_bracket: Upgraded
target_ai_platform: Gemini
include_candidate_references_in_analysis: False
card_specific_question_card_names:
budget_upgrade_amount: 
selected_analysis_questions:
- strengths-weaknesses
selected_set_codes:

deck_source:
https://www.moxfield.com/decks/byte-identity-baseline

""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static readonly string BaselineDecklistText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""";

    public static string SingleFlagOnAnalysisPrompt(string flagKey) => flagKey switch
    {
        DeckAnalysisPacketService.CommandZoneAwarenessFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        DeckAnalysisPacketService.MultiAxisScoreFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded


DECK SCORE (coarse 0-5 bands - magnitude, not quality)
  Power:       0/5  None      (0 Game Changers, combo data unavailable, 0 fast-mana sources)
  Speed:       1/5  Low       (avg MV 1.33, 0 fast-mana, 2 ramp/draw under 3 MV)
  Control:     0/5  None      (0 interaction pieces, 0 board wipes, 0 counters)
  Consistency: 0/5  None      (0 tutors, combo data unavailable, smooth 1.33 curve)
Cross-check: Score aligns with the Bracket 2 classification.
(These bands are DeckFlow heuristic estimates from decklist signals - re-check and refine.)
## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## HEURISTIC VALIDATION
Before beginning the analysis:
1. Validate every proposed combo.
2. Validate every interaction count.
3. Validate every tutor count.
4. Validate every fast mana source.
5. Validate the estimated power/speed scores.
6. Identify every discrepancy between the DeckFlow heuristic blocks above and the actual deck.
7. Use the validated results for the remainder of the analysis.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        DeckAnalysisPacketService.InteractionAuditFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded


INTERACTION AUDIT (DeckFlow heuristic first pass - verify against the cards)
  Targeted removal: approximately 0 confident - none found
  Board wipes: approximately 0 confident - none found
  Counterspells: approximately 0 confident - none found
  Protection or recursion: approximately 0 confident - none found
  Stax or taxation: approximately 0 confident - none found
Coverage gaps to verify: 0 counterspells, no board wipes, no targeted removal, no protection or recursion (possible graveyard-hate / protection gap), no stax or taxation
Use these approximately counted buckets as a starting point only - verify every count and card role against the supplied card text.
## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## HEURISTIC VALIDATION
Before beginning the analysis:
1. Validate every proposed combo.
2. Validate every interaction count.
3. Validate every tutor count.
4. Validate every fast mana source.
5. Validate the estimated power/speed scores.
6. Identify every discrepancy between the DeckFlow heuristic blocks above and the actual deck.
7. Use the validated results for the remainder of the analysis.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        DeckAnalysisPacketService.WinConMapFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded


WIN CONDITION & COMBO MAP (DeckFlow heuristic first pass - the AI must confirm castability, board state, and color access before treating any line below as a live win condition)
Combo data unavailable (Commander Spellbook did not respond) - this is not a claim the deck has no win conditions.
Near-combos, one card away (not currently a win line): none found
Non-combo closers to verify: none found
## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## HEURISTIC VALIDATION
Before beginning the analysis:
1. Validate every proposed combo.
2. Validate every interaction count.
3. Validate every tutor count.
4. Validate every fast mana source.
5. Validate the estimated power/speed scores.
6. Identify every discrepancy between the DeckFlow heuristic blocks above and the actual deck.
7. Use the validated results for the remainder of the analysis.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        DeckAnalysisPacketService.ReferenceFullOracleFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        DeckAnalysisPacketService.ReferenceDeckStatsFlag => ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

deck_stats (counts computed from this deck's Scryfall-resolved cards, as a counting aid; any card that failed lookup is omitted):
cards: 4 (excludes commander)
lands: 1
creatures: 0
average_mana_value: 1.33 (nonland)
mana_curve: 0-1=3 2=1 3=0 4=0 5+=0
role_counts: ramp=1 draw=1 interaction=0 wipes=0 recursion=0 closing_power=0

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""",
        _ => throw new ArgumentOutOfRangeException(nameof(flagKey)),
    };

    public static readonly string AllFourMutatingFlagsOnAnalysisPrompt = ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus & Passionate Archaeologist | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus & Passionate Archaeologist
target_bracket: Bracket 3: Upgraded


DECK SCORE (coarse 0-5 bands - magnitude, not quality)
  Power:       0/5  None      (0 Game Changers, combo data unavailable, 0 fast-mana sources)
  Speed:       1/5  Low       (avg MV 1.33, 0 fast-mana, 2 ramp/draw under 3 MV)
  Control:     0/5  None      (0 interaction pieces, 0 board wipes, 0 counters)
  Consistency: 0/5  None      (0 tutors, combo data unavailable, smooth 1.33 curve)
Cross-check: Score aligns with the Bracket 2 classification.
(These bands are DeckFlow heuristic estimates from decklist signals - re-check and refine.)

INTERACTION AUDIT (DeckFlow heuristic first pass - verify against the cards)
  Targeted removal: approximately 0 confident - none found
  Board wipes: approximately 0 confident - none found
  Counterspells: approximately 0 confident - none found
  Protection or recursion: approximately 0 confident - none found
  Stax or taxation: approximately 0 confident - none found
Coverage gaps to verify: 0 counterspells, no board wipes, no targeted removal, no protection or recursion (possible graveyard-hate / protection gap), no stax or taxation
Use these approximately counted buckets as a starting point only - verify every count and card role against the supplied card text.

WIN CONDITION & COMBO MAP (DeckFlow heuristic first pass - the AI must confirm castability, board state, and color access before treating any line below as a live win condition)
Combo data unavailable (Commander Spellbook did not respond) - this is not a claim the deck has no win conditions.
Near-combos, one card away (not currently a win line): none found
Non-combo closers to verify: none found
## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## HEURISTIC VALIDATION
Before beginning the analysis:
1. Validate every proposed combo.
2. Validate every interaction count.
3. Validate every tutor count.
4. Validate every fast mana source.
5. Validate the estimated power/speed scores.
6. Identify every discrepancy between the DeckFlow heuristic blocks above and the actual deck.
7. Use the validated results for the remainder of the analysis.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. What are the strengths and weaknesses of this deck?

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.


   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Background: A keyword ability used for fixture determinism.
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck.
[current_deck] Arcane Signet | {2} | Artifact | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Command Tower |  | Land | {T}: Add one mana of any color in your commander's color identity.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Passionate Archaeologist | {2}{R} | Legendary Enchantment — Background | Commander creatures you own have "Whenever you cast a spell from exile, this creature deals damage equal to that spell's mana value to target opponent."
[current_deck] Ponder | {U} | Sorcery | Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus
1 Passionate Archaeologist

Mainboard
1 Arcane Signet
1 Command Tower
1 Ponder
1 Sol Ring
""";

    public static readonly string VersionedDecklistAnalysisPrompt = ChatGptImmediateHeader + """
Title this chat: Kraum, Ludevic's Opus | Deck Analysis

You are an expert Magic: The Gathering deck analyst specializing in Commander.

Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.

## DECK CONTEXT
format: Commander
commander: Kraum, Ludevic's Opus
target_bracket: Bracket 3: Upgraded

## EVIDENCE RULES
- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.
- Do not invent card text or rules.
- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.
- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.
- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.
- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.
- Cards labeled candidate_include in the reference are not part of the current deck. Treat them only as candidate additions.
- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).
- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.

## BRACKET GUIDANCE
Commander bracket definitions:
- Bracket 1: Exhibition: Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization. Expect to play at least nine turns before you win or lose.
- Bracket 2: Core: Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay. Expect to play at least eight turns before you win or lose.
- Bracket 3: Upgraded: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins. Expect to play at least six turns before you win or lose.
- Bracket 4: Optimized: Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play. Expect to play at least four turns before you win or lose.
- Bracket 5: cEDH: Metagame-tuned competitive Commander decks built for maximum efficiency and consistency. Games can end on any turn.
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
Target the Commander experience of Bracket 3: Upgraded.
Bracket summary: Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.
Turn expectation: Expect to play at least six turns before you win or lose.
Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.

## ANALYSIS QUESTIONS
Answer each question below. Use the same numbering in your response.
1. Create a Bracket 2 version of this deck.

## OUTPUT FORMAT
Structure your response as follows:

A. Start with a section titled Requested Question Answers.
   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.
   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.
   - Do not skip, merge, or partially answer any question.
   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.

B. After the question answers, include these recommendation sections:
   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.
   - Top Cuts: 5-10 cards with one sentence of reasoning per card.

C. Full decklist output requirements:
   For every requested deck-version or upgrade-path question, output the full 100-card Commander decklist.
   List every card on its own line — 1 commander and 99 other cards.
   After writing each list, count the total lines. If the count is not exactly 100, add or remove cards until it is. Show the count at the end as `// Total: 100`.
   When a question asks for 3 upgrade paths, produce 3 separate full decklists — one per path.
   Format as plain text: quantity CardName (SET) collectorNumber (one card per line, e.g. '1 Sol Ring (CMM) 1'). Start with the commander line.
   For cards retained from the original deck, use the exact set code and collector number from the decklist below.
   For newly added cards, omit the set code and collector number — the deck builder will pick the default printing.
   Return each full list in its own clearly labeled ```text fenced code block (e.g. ```text Budget Efficiency).
   The goal is a list that can be pasted directly into the deck builder's bulk-edit field.

   After each complete decklist, output:
   - Cards Added — a bulleted list of every card in the new deck that was NOT in the original.
   - Cards Cut — a bulleted list of every card in the original deck that is NOT in the new deck.
   - A deck_profile JSON block for this version, using the same schema as the main deck_profile. Return it in a ```json fenced code block labeled with the version name (e.g. ```json deck_profile — Budget Efficiency).

D. After the full analysis, return a JSON object named deck_profile matching the schema below.
   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.
   The question_answers array must contain one entry per question, in the same order as the numbered list above.
   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.
   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.
   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.
   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.
   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.
   Deliver the ENTIRE required output — the Requested Question Answers, Top Adds, Top Cuts, and the complete deck_profile JSON — in this single response. Do NOT refuse, claim the output is too long, ask which part to produce, ask to continue, or offer to restructure, summarize, or split the output; a complete response for this task is a few hundred lines and fits well within one reply.
   If the full output would genuinely approach your hard output limit, do not refuse or drop any section: shorten each question answer to 4-6 sentences (apply the SAME shortened text in both the readable section and the JSON question_answers so they still mirror each other), and cap Top Adds and Top Cuts at 5 entries each. Every section and every JSON field must still be present.

   The deck_versions array must contain one entry per requested deck version or upgrade path.
   Each entry's decklist field must contain the complete 100-card list (one card per line, same format as the text code blocks above).
   Do not abbreviate or truncate any decklist in the JSON — every card must be present.

   Field-level detail requirements for the deck_profile JSON:
   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
   - assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.
   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.

{
  "format": "Commander",
  "commander": "Kraum, Ludevic\u0027s Opus",
  "game_plan": "",
  "primary_axes": [],
  "speed": "",
  "estimated_win_turn": 0,
  "can_answer_win_turn": false,
  "assessed_bracket": "",
  "bracket_justification": "",
  "strengths": [],
  "weaknesses": [],
  "deck_needs": [],
  "weak_slots": [
    {
      "card": "",
      "reason": ""
    }
  ],
  "synergy_tags": [],
  "question_answers": [
    {
      "question_number": 1,
      "question": "",
      "answer": "",
      "basis": "authoritative|inference|mixed"
    }
  ],
  "deck_versions": [
    {
      "version_name": "",
      "decklist": "complete 100-card decklist, one card per line, same format as the text code blocks",
      "cards_added": [],
      "cards_cut": []
    }
  ]
}

## REFERENCE DATA
reference_context:
source: Scryfall Oracle and official Wizards Comprehensive Rules
generated_at_utc: <TIMESTAMP>
format: Commander

official_commander_banned_cards: Dockside Extortionist, Mana Crypt

mechanics:
Flying: A keyword ability used for fixture determinism.
Haste: A keyword ability used for fixture determinism.
Pest: A keyword ability used for fixture determinism.

cards:
[current_deck] = active deck. [candidate_include:sideboard] and [candidate_include:maybeboard] = optional candidates only.
[current_deck] submitted_name: Blex, Vexing Pest / Search for Blex | resolved_card: Blex, Vexing Pest // Search for Blex |  | Legendary Creature — Pest // Sorcery | Blex, Vexing Pest | {2}{B}{G} | Legendary Creature — Pest | Other Pests, Bats, Insects, Snakes, and Spiders you control get +1/+1. | 3/2 Search for Blex | {X}{2}{B/G}{B/G} | Sorcery | Look at the top five cards of your library. You may reveal any number of creature cards with mana value X or less from among them and put the revealed cards into your hand. Put the rest on the bottom of your library in a random order. You lose 3 life.
[current_deck] Kraum, Ludevic's Opus | {3}{U}{R} | Legendary Creature — Zombie Horror | Flying, haste Whenever an opponent casts their second spell each turn, draw a card. 4/4
[current_deck] Sol Ring | {1} | Artifact | {T}: Add {C}{C}.
[candidate_include:maybeboard] Swords to Plowshares | {W} | Instant | Exile target creature. Its controller gains life equal to its power.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus (C16) 39

Mainboard
1 Blex, Vexing Pest (TSR) 96 [printed as: Blex, Vexing Pest / Search for Blex]
1 Sol Ring (C16) 272

Possible Includes
1 Swords to Plowshares
""";

    public static readonly string WhitespaceRequestContextText = "workflow_step: 2\nformat: Commander\ndeck_name: Kraum\tPartner   Deck\ncommander: Kraum, Ludevic's Opus\ntarget_commander_bracket: Upgraded\ntarget_ai_platform: ChatGPT\ninclude_candidate_references_in_analysis: False\ncard_specific_question_card_names:\nbudget_upgrade_amount: \nselected_analysis_questions:\n- strengths-weaknesses\nselected_set_codes:\n\nstrategy_notes:\nLine one\nLine\ttwo\nLine  three   with   gaps\rTrailing bare CR\n\nmeta_notes:\nMeta:\tgrindy\n\nSlow   pods\r stax-lite\n\ndeck_source:\nhttps://www.moxfield.com/decks/byte-identity-baseline\n";
}
