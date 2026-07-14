namespace DeckFlow.Web.Tests;

/// <summary>Golden constants for <see cref="DeckPrimerByteIdentityTests"/>, captured from a real
/// DeckPrimerPacketService.BuildAsync run against the unrefactored service (no hand-typed goldens).
/// All "\r\n" line endings are normalized to "\n" (captured via Windows dotnet.exe; CI runs
/// ubuntu-latest) — see PacketByteIdentityFixtures.NormalizeForGoldenComparison. Primer never
/// embeds a live timestamp (confirmed by grep of DeckPrimerPacketService.cs).</summary>
internal static class PrimerGoldens
{
    private const string ChatGptImmediateHeader = PacketByteIdentityFixtures.ChatGptImmediateHeader;

    public static string BaselinePromptText(string platform) => platform switch
    {
        "ChatGPT" => ChatGptImmediateHeader + """
You are an expert Magic: The Gathering primer writer specializing in Commander.
Build a pilot-facing deck primer that is grounded in the supplied decklist and reference blocks before offering any inference.

## DECK CONTEXT
format: Commander
target_bracket: Bracket 3: Upgraded
selected_sections: 4

## EVIDENCE RULES
- Use the grounded combo, matchup, and category data below as authoritative where present.
- Do not invent card text, combo lines, or metagame facts.
- Keep verified combos separate from speculative ideas.
- If a conclusion depends on inference from the decklist, label it as an inference.

## MATCHUP TARGETS
- Aggro: go-wide combat decks, commander-damage races, and fast pressure backed by efficient threats.
- Control: permission-heavy shells, wraths, and value engines that try to dictate pace over multiple turns.
- Midrange: creature-value and incremental-advantage decks that pivot between pressure and stabilization.
- Combo: proactive decks trying to assemble infinite loops or deterministic wins before turn 8.
- Stax/Hate: tax, denial, and lock-piece strategies that attack mana, card flow, or game actions.

## Known Combos (ground truth — do not speculate)
No verified combos available — treat all synergies as speculative.
(Commander Spellbook API was unreachable at generation time.)

## Speculative Synergies (you propose)
Suggest plausible interactions or lines that are not in the ground-truth block above.
Label every speculative item as unverified and do not restate it as a known combo.

## SECTION DIRECTIVES
Write the primer in the numbered order below. Each section should be concrete, deck-specific, and useful to a pilot in real games.
### 1. Verified Combos
Present grounded combo lines clearly, including what pieces matter, what the line does, and when it is worth pursuing.
### 2. Near-Combos
Highlight one-card-away lines or close assemblies that help the pilot recognize meaningful upgrade or tutor opportunities.
### 3. Role Count Grounding
Ground the primer in role counts like ramp, draw, tutors, and interaction so recommendations stay tied to the deck's actual composition.
### 4. Matchup Archetype Plan
Outline how the deck approaches common opposing archetypes and what strategic posture changes in each matchup.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Sol Ring

## OUTPUT FORMAT
Return the finished primer inside a single ```markdown fenced code block.
Use the same numbered section order as the SECTION DIRECTIVES block.
Cite verified combos only in the known-combos section, keep speculative ideas in their own section, and keep matchup guidance grounded in the supplied targets.
""",
        "Claude" => """
<deck_primer>
<role>
You are an expert Magic: The Gathering primer writer specializing in Commander.
</role>

<context>
format: Commander
target_bracket: Bracket 3: Upgraded
selected_sections: 4
</context>

<evidence_rules>
Use the grounded combo, matchup, and category data below as authoritative where present.
Do not invent card text, combo lines, or metagame facts.
Keep verified combos separate from speculative ideas.
If a conclusion depends on inference from the decklist, label it as an inference.
</evidence_rules>

<matchup_targets>
- Aggro: go-wide combat decks, commander-damage races, and fast pressure backed by efficient threats.
- Control: permission-heavy shells, wraths, and value engines that try to dictate pace over multiple turns.
- Midrange: creature-value and incremental-advantage decks that pivot between pressure and stabilization.
- Combo: proactive decks trying to assemble infinite loops or deterministic wins before turn 8.
- Stax/Hate: tax, denial, and lock-piece strategies that attack mana, card flow, or game actions.
</matchup_targets>

<grounded_combos>
## Known Combos (ground truth — do not speculate)
No verified combos available — treat all synergies as speculative.
(Commander Spellbook API was unreachable at generation time.)

## Speculative Synergies (you propose)
Suggest plausible interactions or lines that are not in the ground-truth block above.
Label every speculative item as unverified and do not restate it as a known combo.
</grounded_combos>

<section_directives>
Write the primer in the numbered order below. Each section should be concrete, deck-specific, and useful to a pilot in real games.
1. Verified Combos
Present grounded combo lines clearly, including what pieces matter, what the line does, and when it is worth pursuing.
2. Near-Combos
Highlight one-card-away lines or close assemblies that help the pilot recognize meaningful upgrade or tutor opportunities.
3. Role Count Grounding
Ground the primer in role counts like ramp, draw, tutors, and interaction so recommendations stay tied to the deck's actual composition.
4. Matchup Archetype Plan
Outline how the deck approaches common opposing archetypes and what strategic posture changes in each matchup.
</section_directives>

<decklist>
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Sol Ring
</decklist>

<primer_output>
Return the finished primer as readable markdown.
Use the same numbered section order as <section_directives>.
Keep verified combos only in the known-combos section, keep speculative ideas separate, and keep matchup guidance grounded in the supplied targets.
</primer_output>
</deck_primer>
""",
        "Gemini" => """
You are an expert Magic: The Gathering analyst and primer writer specializing in Commander.
You produce pilot-facing primers that stay grounded in supplied deck, combo, and matchup evidence.

Think carefully through the problem before responding.

## DECK CONTEXT
format: Commander
target_bracket: Bracket 3: Upgraded
selected_sections: 4

## EVIDENCE RULES
- Use the grounded combo, matchup, and category data below as authoritative where present.
- Do not invent card text, combo lines, or metagame facts.
- Keep verified combos separate from speculative ideas.
- If a conclusion depends on inference from the decklist, label it as an inference.

## Known Combos (ground truth — do not speculate)
No verified combos available — treat all synergies as speculative.
(Commander Spellbook API was unreachable at generation time.)

## Speculative Synergies (you propose)
Suggest plausible interactions or lines that are not in the ground-truth block above.
Label every speculative item as unverified and do not restate it as a known combo.

## GAMEPLAY DIRECTIVES
Write the following gameplay sections in numbered order. Keep the advice grounded in the actual deck composition.
1. Role Count Grounding — Ground the primer in role counts like ramp, draw, tutors, and interaction so recommendations stay tied to the deck's actual composition.

## DECKLIST
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Sol Ring

## MATCHUP DIRECTIVES
Write the following matchup sections in numbered order.
2. Matchup Archetype Plan — Outline how the deck approaches common opposing archetypes and what strategic posture changes in each matchup.

## MATCHUP TARGETS
- Aggro: go-wide combat decks, commander-damage races, and fast pressure backed by efficient threats.
- Control: permission-heavy shells, wraths, and value engines that try to dictate pace over multiple turns.
- Midrange: creature-value and incremental-advantage decks that pivot between pressure and stabilization.
- Combo: proactive decks trying to assemble infinite loops or deterministic wins before turn 8.
- Stax/Hate: tax, denial, and lock-piece strategies that attack mana, card flow, or game actions.

## OUTPUT FORMAT
Return the finished primer as readable markdown.
Use the same numbered section order as the directive blocks that remain in the prompt.
Keep verified combos only in the known-combos section, keep speculative ideas separate, and keep matchup guidance grounded in the supplied targets.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static readonly string BaselineRequestContextText = """
workflow_step: 1
format: Commander
deck_name: 
commander: Kraum, Ludevic's Opus
target_commander_bracket: Upgraded
target_ai_platform: ChatGPT
primer_style: Standard
selected_section_ids:
- verified-combos
- near-combos
- role-count-grounding
- matchup-archetype-plan
deck_source:
Commander
1 Kraum, Ludevic's Opus

1 Sol Ring
1 Arcane Signet
1 Command Tower
""";

    public static readonly string BaselineDecklistText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Command Tower
1 Sol Ring
""";

    public static readonly string WhitespaceRequestContextText = """
workflow_step: 1
format: Commander
deck_name: Kraum Partner Deck
commander: Kraum, Ludevic's Opus
target_commander_bracket: Upgraded
target_ai_platform: ChatGPT
primer_style: Standard
selected_section_ids:
- verified-combos
- near-combos
- role-count-grounding
- matchup-archetype-plan
deck_source:
Commander
1 Kraum, Ludevic's Opus

1 Sol Ring
1 Arcane Signet
1 Command Tower
""";
}
