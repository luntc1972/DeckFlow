namespace DeckFlow.Web.Tests;

/// <summary>
/// Complete prompts captured from six placeholder-test failures on Windows .NET.
/// Explicit CRLF escapes preserve the emitted bytes independently of this source file's LF endings.
/// </summary>
internal static class BracketPromptGoldens
{
    public static string Classification(string platform) => platform switch
    {
        "ChatGPT" =>
            "EXECUTE NOW — perform the entire task defined below and output the complete result in this reply. Do not ask which task to run, do not ask for confirmation, and do not wait for further instructions; the full task is specified below.\n" +
            "\n" +
            "## Bracket Classification: Literal classification deck\n" +
            "\n" +
            "This deck classifies as **Bracket 3: Upgraded** (Upgraded).\n" +
            "\n" +
            "## WHY THIS BRACKET\n" +
            "\n" +
            "- 1 Game Changer (B3 allows up to 3).\n" +
            "- No two-card win combos detected.\n" +
            "- 0 mass land denial.\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "\n",
        "Claude" =>
            "<bracket_classification>\n" +
            "<deck_name>Literal classification deck</deck_name>\n" +
            "<verdict>This deck classifies as Bracket 3: Upgraded (Upgraded).</verdict>\n" +
            "\n" +
            "<why_this_bracket>\n" +
            "WHY THIS BRACKET\n" +
            "\n" +
            "- 1 Game Changer (B3 allows up to 3).\n" +
            "- No two-card win combos detected.\n" +
            "- 0 mass land denial.\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "</why_this_bracket>\n" +
            "\n" +
            "<effective_date_note>\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "</effective_date_note>\n" +
            "</bracket_classification>\n",
        "Gemini" =>
            "You are an expert Commander deck analyst. Review the following bracket classification and provide guidance.\n" +
            "\n" +
            "Think carefully through the evidence before responding.\n" +
            "\n" +
            "## Bracket Classification: Literal classification deck\n" +
            "\n" +
            "This deck classifies as **Bracket 3: Upgraded** (Upgraded).\n" +
            "\n" +
            "## WHY THIS BRACKET\n" +
            "\n" +
            "- 1 Game Changer (B3 allows up to 3).\n" +
            "- No two-card win combos detected.\n" +
            "- 0 mass land denial.\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "\n",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };

    public static string Balancer(string platform) => platform switch
    {
        "ChatGPT" =>
            "EXECUTE NOW — perform the entire task defined below and output the complete result in this reply. Do not ask which task to run, do not ask for confirmation, and do not wait for further instructions; the full task is specified below.\n" +
            "\n" +
            "## Bracket Classification: Literal balancer deck\n" +
            "\n" +
            "This deck classifies as **Bracket 4: Optimized** (Optimized).\n" +
            "\n" +
            "## WHY THIS BRACKET\n" +
            "\n" +
            "- 4 Game Changers (B4 has no cap).\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\n" +
            "- 1 mass land denial: Armageddon.\n" +
            "- 1 mass land denial: Ravages of War.\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "\n" +
            "## FLOOR VIOLATIONS — cards that exceed B2 Core\n" +
            "\n" +
            "- Mana Crypt [Game Changer]\n" +
            "- The One Ring [Game Changer]\n" +
            "- Jeweled Lotus [Game Changer]\n" +
            "- Cyclonic Rift [Game Changer]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Demonic Consultation [Combo half]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Tainted Pact [Combo half]\n" +
            "- Armageddon [Mass land denial]\n" +
            "- Ravages of War [Mass land denial]\n" +
            "\n" +
            "## STARTER CUTS to reach B2 Core\n" +
            "\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\n" +
            "- Cut Armageddon — no mass land denial at B2.\n" +
            "- Cut Ravages of War — no mass land denial at B2.\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\n" +
            "\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\n",
        "Claude" =>
            "<bracket_classification>\n" +
            "<deck_name>Literal balancer deck</deck_name>\n" +
            "<verdict>This deck classifies as Bracket 4: Optimized (Optimized).</verdict>\n" +
            "\n" +
            "<why_this_bracket>\n" +
            "WHY THIS BRACKET\n" +
            "\n" +
            "- 4 Game Changers (B4 has no cap).\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\n" +
            "- 1 mass land denial: Armageddon.\n" +
            "- 1 mass land denial: Ravages of War.\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "</why_this_bracket>\n" +
            "\n" +
            "<effective_date_note>\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "</effective_date_note>\n" +
            "\n" +
            "<balancer>\n" +
            "<floor_violations heading=\"FLOOR VIOLATIONS — cards that exceed B2 Core\">\n" +
            "- Mana Crypt [Game Changer]\n" +
            "- The One Ring [Game Changer]\n" +
            "- Jeweled Lotus [Game Changer]\n" +
            "- Cyclonic Rift [Game Changer]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Demonic Consultation [Combo half]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Tainted Pact [Combo half]\n" +
            "- Armageddon [Mass land denial]\n" +
            "- Ravages of War [Mass land denial]\n" +
            "</floor_violations>\n" +
            "\n" +
            "<starter_cuts heading=\"STARTER CUTS to reach B2 Core\">\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\n" +
            "- Cut Armageddon — no mass land denial at B2.\n" +
            "- Cut Ravages of War — no mass land denial at B2.\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\n" +
            "</starter_cuts>\n" +
            "</balancer>\n" +
            "</bracket_classification>\n",
        "Gemini" =>
            "You are an expert Commander deck analyst. Review the following bracket classification and provide guidance.\n" +
            "\n" +
            "Think carefully through the evidence before responding.\n" +
            "\n" +
            "## Bracket Classification: Literal balancer deck\n" +
            "\n" +
            "This deck classifies as **Bracket 4: Optimized** (Optimized).\n" +
            "\n" +
            "## WHY THIS BRACKET\n" +
            "\n" +
            "- 4 Game Changers (B4 has no cap).\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\n" +
            "- 1 mass land denial: Armageddon.\n" +
            "- 1 mass land denial: Ravages of War.\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\n" +
            "\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\n" +
            "\n" +
            "## FLOOR VIOLATIONS — cards that exceed B2 Core\n" +
            "\n" +
            "- Mana Crypt [Game Changer]\n" +
            "- The One Ring [Game Changer]\n" +
            "- Jeweled Lotus [Game Changer]\n" +
            "- Cyclonic Rift [Game Changer]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Demonic Consultation [Combo half]\n" +
            "- Thassa's Oracle [Combo half]\n" +
            "- Tainted Pact [Combo half]\n" +
            "- Armageddon [Mass land denial]\n" +
            "- Ravages of War [Mass land denial]\n" +
            "\n" +
            "## STARTER CUTS to reach B2 Core\n" +
            "\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\n" +
            "- Cut Armageddon — no mass land denial at B2.\n" +
            "- Cut Ravages of War — no mass land denial at B2.\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\n" +
            "\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\n",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };
}
