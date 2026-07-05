namespace DeckFlow.Web.Tests;

/// <summary>Golden constants for <see cref="MetaGapByteIdentityTests"/>, captured from a real
/// MetaGapService.BuildAsync run against the unrefactored service (no hand-typed goldens). All
/// "\r\n" line endings are normalized to "\n" before capture (captured via Windows dotnet.exe;
/// CI runs ubuntu-latest) — see PacketByteIdentityFixtures.NormalizeForGoldenComparison. MetaGap
/// has no live-timestamp field to normalize (confirmed by grep of MetaGapService.cs).</summary>
internal static class MetaGapGoldens
{
    public static string BaselinePromptText(string platform) => platform switch
    {
        "ChatGPT" => """
Title this chat: Kraum, Ludevic's Opus | cEDH Meta Gap

ROLE:
You are a cEDH deck optimization analyst.
Compare MY_DECK against 1 REF deck(s).

EVIDENCE PRIORITY:
1. Use the supplied decklists as the primary evidence.
2. Use the supplied Commander Spellbook combo sections as verified combo evidence.
3. Only infer patterns that are strongly supported by the supplied cards.
4. If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.

RULES:
- Read every supplied decklist before answering.
- Base every conclusion ONLY on observable card overlap and deck construction.
- Do NOT assume combo lines unless supported by card presence in the lists.
- Cite specific card names as evidence.
- Clearly label any interpretation as inference.
- If evidence is weak or unclear, explicitly say so in the relevant field.
- Do NOT invent card text or interactions.

INPUT DATA:
MY_DECK (Kraum, Ludevic's Opus):
1 Arcane Signet
1 Sol Ring

Commander Spellbook combos for MY_DECK:
(none found)

R1 (Reference Pilot, #1, Fixture Championship):
Counterspell
Kraum, Ludevic's Opus
Sol Ring

Commander Spellbook combos for R1:
(none found)

ANALYSIS TASK:
Use the input data above and complete every section below.

1. WIN CONDITIONS
- Identify primary and backup win lines in MY_DECK.
- Identify primary and backup win lines across REF decks (consensus).
- List win lines present in multiple REF decks but missing in MY_DECK.

2. INTERACTION AUDIT
- Count and compare counterspells, removal, free interaction, and stax pieces.
- Determine if MY_DECK is under, over, or aligned vs REF decks.
- Identify key missing interaction pieces.

3. SPEED & TEMPO
- Classify each deck as turbo (T2-3), fast (T3-4), mid (T4-5), or grind (T5+).
- Estimate MY_DECK vs REF average goldfish speed.
- Identify cards contributing to faster starts (fast mana, free spells).

4. MANA EFFICIENCY
- Compare fast mana count (0-1 CMC ramp), total ramp density, and land count.
- Count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.
- Identify missing high-impact acceleration pieces.

5. CARD OVERLAP ANALYSIS
- Core convergence: cards in all 1 REF decks. Flag whether MY_DECK has them.
- High-frequency staples: cards in 2+ REF decks but not in MY_DECK = missing staples.
- Cards unique to MY_DECK (in 0 REF decks) = potential cuts.
- Categorize each by role: ramp, interaction, draw, wincon, protection, stax, tutor, utility, land.

6. CONSISTENCY & REDUNDANCY
- Compare tutor density, redundant combo pieces, and draw engine count.
- Determine whether MY_DECK is more or less consistent than the REF sample.

7. TOP IMPROVEMENTS
- Top 5-10 adds: include what each replaces and justify using overlap evidence.
- Top 5-10 cuts: explain why each is low-impact or non-meta.

8. META POSITIONING
- Determine if MY_DECK is faster or slower than the field, more or less interactive.
- Identify which archetype it most resembles (turbo, midrange, control, stax).
- Assign a 1-10 cEDH readiness score with 2-sentence justification.

OUTPUT CONTRACT:
- First, provide a concise human-readable meta gap summary.
- Then return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.
- The prose summary must come before the JSON block.
- Fill every field in meta_gap.
- Use empty strings, 0, 0.0, false, or [] when evidence is missing.
- Keep all detail and justification concise, specific, and evidence-based.
- Put the consistency/redundancy summary and meta-positioning summary into meta_summary and optimization_path.
- Do not add any extra sections after the JSON block.

JSON SHAPE:
Use this exact shape:
{"meta_gap":{"commander":"","color_id":"","ref_deck_count":0,"readiness_score":0,"readiness_justification":"","win_lines":{"my_deck":{"primary":"","backup":""},"ref_consensus":{"primary":"","backup":""},"missing_lines":[""]},"interaction":{"my_count":0,"ref_avg_count":0,"verdict":"","detail":""},"speed":{"my_classification":"","my_avg_turn":"","ref_classification":"","ref_avg_turn":"","detail":""},"mana_efficiency":{"my_fast_mana":0,"ref_avg_fast_mana":0,"my_avg_cmc":0,"ref_avg_cmc":0,"my_lands":0,"ref_avg_lands":0,"detail":""},"core_convergence":[{"card":"","role":"","in_my_deck":true}],"missing_staples":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"potential_cuts":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"top_10_adds":[{"card":"","replaces":"","role":"","why":""}],"top_10_cuts":[{"card":"","role":"","why":""}],"meta_summary":"","optimization_path":""}}
""",
        "Claude" => """
<role>
You are a cEDH deck optimization analyst.
</role>

<my_deck>
  <commander>Kraum, Ludevic's Opus</commander>
  <list>
1 Arcane Signet
1 Sol Ring
  </list>
  <combos>
Commander Spellbook combos for MY_DECK:
(none found)
  </combos>
</my_deck>

<reference_decks>
  <reference>
  player: Reference Pilot
  standing: #1
  tournament: Fixture Championship
  <list>
Counterspell
Kraum, Ludevic's Opus
Sol Ring
  </list>
  <combos>
Commander Spellbook combos for R1:
(none found)
  </combos>
  </reference>
</reference_decks>

<output_schema>
{"meta_gap":{"commander":"","color_id":"","ref_deck_count":0,"readiness_score":0,"readiness_justification":"","win_lines":{"my_deck":{"primary":"","backup":""},"ref_consensus":{"primary":"","backup":""},"missing_lines":[""]},"interaction":{"my_count":0,"ref_avg_count":0,"verdict":"","detail":""},"speed":{"my_classification":"","my_avg_turn":"","ref_classification":"","ref_avg_turn":"","detail":""},"mana_efficiency":{"my_fast_mana":0,"ref_avg_fast_mana":0,"my_avg_cmc":0,"ref_avg_cmc":0,"my_lands":0,"ref_avg_lands":0,"detail":""},"core_convergence":[{"card":"","role":"","in_my_deck":true}],"missing_staples":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"potential_cuts":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"top_10_adds":[{"card":"","replaces":"","role":"","why":""}],"top_10_cuts":[{"card":"","role":"","why":""}],"meta_summary":"","optimization_path":""}}
</output_schema>

<task>
Compare MY_DECK against 1 REF deck(s).
Use the supplied decklists as the primary evidence.
Use the supplied Commander Spellbook combo sections as verified combo evidence.
Only infer patterns that are strongly supported by the supplied cards.
If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.
Read every supplied decklist before answering.
Base every conclusion ONLY on observable card overlap and deck construction.
Do NOT assume combo lines unless supported by card presence in the lists.
Cite specific card names as evidence.
Clearly label any interpretation as inference.
If evidence is weak or unclear, explicitly say so in the relevant field.
Do NOT invent card text or interactions.
When assessing mana efficiency, count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.

Provide readable analysis first covering:
- WIN CONDITIONS
- INTERACTION AUDIT
- SPEED & TEMPO
- MANA EFFICIENCY
- CARD OVERLAP ANALYSIS
- CONSISTENCY & REDUNDANCY
- TOP IMPROVEMENTS
- META POSITIONING
After the readable summary, return a single JSON object matching <output_schema>.
Return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.
</task>
""",
        "Gemini" => """
You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.
You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.

Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.

Title this chat: Kraum, Ludevic's Opus | cEDH Meta Gap

ROLE:
You are a cEDH deck optimization analyst.
Compare MY_DECK against 1 REF deck(s).

EVIDENCE PRIORITY:
1. Use the supplied decklists as the primary evidence.
2. Use the supplied Commander Spellbook combo sections as verified combo evidence.
3. Only infer patterns that are strongly supported by the supplied cards.
4. If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.

RULES:
- Read every supplied decklist before answering.
- Base every conclusion ONLY on observable card overlap and deck construction.
- Do NOT assume combo lines unless supported by card presence in the lists.
- Cite specific card names as evidence.
- Clearly label any interpretation as inference.
- If evidence is weak or unclear, explicitly say so in the relevant field.
- Do NOT invent card text or interactions.

INPUT DATA:
MY_DECK (Kraum, Ludevic's Opus):
1 Arcane Signet
1 Sol Ring

Commander Spellbook combos for MY_DECK:
(none found)

R1 (Reference Pilot, #1, Fixture Championship):
Counterspell
Kraum, Ludevic's Opus
Sol Ring

Commander Spellbook combos for R1:
(none found)

ANALYSIS TASK:
Use the input data above and complete every section below.

1. WIN CONDITIONS
- Identify primary and backup win lines in MY_DECK.
- Identify primary and backup win lines across REF decks (consensus).
- List win lines present in multiple REF decks but missing in MY_DECK.

2. INTERACTION AUDIT
- Count and compare counterspells, removal, free interaction, and stax pieces.
- Determine if MY_DECK is under, over, or aligned vs REF decks.
- Identify key missing interaction pieces.

3. SPEED & TEMPO
- Classify each deck as turbo (T2-3), fast (T3-4), mid (T4-5), or grind (T5+).
- Estimate MY_DECK vs REF average goldfish speed.
- Identify cards contributing to faster starts (fast mana, free spells).

4. MANA EFFICIENCY
- Compare fast mana count (0-1 CMC ramp), total ramp density, and land count.
- Count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.
- Identify missing high-impact acceleration pieces.

5. CARD OVERLAP ANALYSIS
- Core convergence: cards in all 1 REF decks. Flag whether MY_DECK has them.
- High-frequency staples: cards in 2+ REF decks but not in MY_DECK = missing staples.
- Cards unique to MY_DECK (in 0 REF decks) = potential cuts.
- Categorize each by role: ramp, interaction, draw, wincon, protection, stax, tutor, utility, land.

6. CONSISTENCY & REDUNDANCY
- Compare tutor density, redundant combo pieces, and draw engine count.
- Determine whether MY_DECK is more or less consistent than the REF sample.

7. TOP IMPROVEMENTS
- Top 5-10 adds: include what each replaces and justify using overlap evidence.
- Top 5-10 cuts: explain why each is low-impact or non-meta.

8. META POSITIONING
- Determine if MY_DECK is faster or slower than the field, more or less interactive.
- Identify which archetype it most resembles (turbo, midrange, control, stax).
- Assign a 1-10 cEDH readiness score with 2-sentence justification.

OUTPUT CONTRACT:
Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.
- First, provide a concise human-readable meta gap summary.
- Then return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.
- Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.
- The prose summary must come before the JSON block.
- Fill every field in meta_gap.
- Use empty strings, 0, 0.0, false, or [] when evidence is missing.
- Keep all detail and justification concise, specific, and evidence-based.
- Put the consistency/redundancy summary and meta-positioning summary into meta_summary and optimization_path.
- Do not add any extra sections after the JSON block.

JSON SHAPE:
Use this exact shape:
{"meta_gap":{"commander":"","color_id":"","ref_deck_count":0,"readiness_score":0,"readiness_justification":"","win_lines":{"my_deck":{"primary":"","backup":""},"ref_consensus":{"primary":"","backup":""},"missing_lines":[""]},"interaction":{"my_count":0,"ref_avg_count":0,"verdict":"","detail":""},"speed":{"my_classification":"","my_avg_turn":"","ref_classification":"","ref_avg_turn":"","detail":""},"mana_efficiency":{"my_fast_mana":0,"ref_avg_fast_mana":0,"my_avg_cmc":0,"ref_avg_cmc":0,"my_lands":0,"ref_avg_lands":0,"detail":""},"core_convergence":[{"card":"","role":"","in_my_deck":true}],"missing_staples":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"potential_cuts":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"top_10_adds":[{"card":"","replaces":"","role":"","why":""}],"top_10_cuts":[{"card":"","role":"","why":""}],"meta_summary":"","optimization_path":""}}

MANDATORY — DO NOT SKIP: Your response MUST end with a <result>...</result> block containing a single JSON object that matches the schema above. The JSON block is REQUIRED even if you have already produced a complete readable analysis — without it your response is invalid and DeckFlow will reject the upload. Do not summarise. Do not say "and the JSON is...". Output the literal <result> tag, then the JSON object, then </result>. Nothing else after </result>.
""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static string BaselineRequestContextText(string platform) => platform switch
    {
        "ChatGPT" => """
workflow_step: 2
commander: Kraum, Ludevic's Opus
target_ai_platform: ChatGPT
time_period: ONE_YEAR
sort_by: TOP
min_event_size: 50
selected_reference_indexes:
- 0

""",
        "Claude" => """
workflow_step: 2
commander: Kraum, Ludevic's Opus
target_ai_platform: Claude
time_period: ONE_YEAR
sort_by: TOP
min_event_size: 50
selected_reference_indexes:
- 0

""",
        "Gemini" => """
workflow_step: 2
commander: Kraum, Ludevic's Opus
target_ai_platform: Gemini
time_period: ONE_YEAR
sort_by: TOP
min_event_size: 50
selected_reference_indexes:
- 0

""",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    public static readonly string BaselineDecklistText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Sol Ring
""";

    public static readonly string CollectionMissFallbackPromptText = """
Title this chat: Kraum, Ludevic's Opus | cEDH Meta Gap

ROLE:
You are a cEDH deck optimization analyst.
Compare MY_DECK against 1 REF deck(s).

EVIDENCE PRIORITY:
1. Use the supplied decklists as the primary evidence.
2. Use the supplied Commander Spellbook combo sections as verified combo evidence.
3. Only infer patterns that are strongly supported by the supplied cards.
4. If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.

RULES:
- Read every supplied decklist before answering.
- Base every conclusion ONLY on observable card overlap and deck construction.
- Do NOT assume combo lines unless supported by card presence in the lists.
- Cite specific card names as evidence.
- Clearly label any interpretation as inference.
- If evidence is weak or unclear, explicitly say so in the relevant field.
- Do NOT invent card text or interactions.

INPUT DATA:
MY_DECK (Kraum, Ludevic's Opus):
1 Blex, Vexing Pest
1 Sol Ring

Commander Spellbook combos for MY_DECK:
(none found)

R1 (Reference Pilot, #1, Fixture Championship):
Counterspell
Kraum, Ludevic's Opus
Sol Ring

Commander Spellbook combos for R1:
(none found)

ANALYSIS TASK:
Use the input data above and complete every section below.

1. WIN CONDITIONS
- Identify primary and backup win lines in MY_DECK.
- Identify primary and backup win lines across REF decks (consensus).
- List win lines present in multiple REF decks but missing in MY_DECK.

2. INTERACTION AUDIT
- Count and compare counterspells, removal, free interaction, and stax pieces.
- Determine if MY_DECK is under, over, or aligned vs REF decks.
- Identify key missing interaction pieces.

3. SPEED & TEMPO
- Classify each deck as turbo (T2-3), fast (T3-4), mid (T4-5), or grind (T5+).
- Estimate MY_DECK vs REF average goldfish speed.
- Identify cards contributing to faster starts (fast mana, free spells).

4. MANA EFFICIENCY
- Compare fast mana count (0-1 CMC ramp), total ramp density, and land count.
- Count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.
- Identify missing high-impact acceleration pieces.

5. CARD OVERLAP ANALYSIS
- Core convergence: cards in all 1 REF decks. Flag whether MY_DECK has them.
- High-frequency staples: cards in 2+ REF decks but not in MY_DECK = missing staples.
- Cards unique to MY_DECK (in 0 REF decks) = potential cuts.
- Categorize each by role: ramp, interaction, draw, wincon, protection, stax, tutor, utility, land.

6. CONSISTENCY & REDUNDANCY
- Compare tutor density, redundant combo pieces, and draw engine count.
- Determine whether MY_DECK is more or less consistent than the REF sample.

7. TOP IMPROVEMENTS
- Top 5-10 adds: include what each replaces and justify using overlap evidence.
- Top 5-10 cuts: explain why each is low-impact or non-meta.

8. META POSITIONING
- Determine if MY_DECK is faster or slower than the field, more or less interactive.
- Identify which archetype it most resembles (turbo, midrange, control, stax).
- Assign a 1-10 cEDH readiness score with 2-sentence justification.

OUTPUT CONTRACT:
- First, provide a concise human-readable meta gap summary.
- Then return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.
- The prose summary must come before the JSON block.
- Fill every field in meta_gap.
- Use empty strings, 0, 0.0, false, or [] when evidence is missing.
- Keep all detail and justification concise, specific, and evidence-based.
- Put the consistency/redundancy summary and meta-positioning summary into meta_summary and optimization_path.
- Do not add any extra sections after the JSON block.

JSON SHAPE:
Use this exact shape:
{"meta_gap":{"commander":"","color_id":"","ref_deck_count":0,"readiness_score":0,"readiness_justification":"","win_lines":{"my_deck":{"primary":"","backup":""},"ref_consensus":{"primary":"","backup":""},"missing_lines":[""]},"interaction":{"my_count":0,"ref_avg_count":0,"verdict":"","detail":""},"speed":{"my_classification":"","my_avg_turn":"","ref_classification":"","ref_avg_turn":"","detail":""},"mana_efficiency":{"my_fast_mana":0,"ref_avg_fast_mana":0,"my_avg_cmc":0,"ref_avg_cmc":0,"my_lands":0,"ref_avg_lands":0,"detail":""},"core_convergence":[{"card":"","role":"","in_my_deck":true}],"missing_staples":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"potential_cuts":[{"card":"","role":"","ref_count":0,"priority":1,"why":""}],"top_10_adds":[{"card":"","replaces":"","role":"","why":""}],"top_10_cuts":[{"card":"","role":"","why":""}],"meta_summary":"","optimization_path":""}}
""";

    public static readonly string NoCommanderSectionDecklistText = """
Commander
1 Kraum, Ludevic's Opus

Mainboard
1 Arcane Signet
1 Sol Ring
""";
}
