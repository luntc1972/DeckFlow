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
            "EXECUTE NOW — perform the entire task defined below and output the complete result in this reply. Do not ask which task to run, do not ask for confirmation, and do not wait for further instructions; the full task is specified below.\r\n" +
            "\r\n" +
            "## Bracket Classification: Literal classification deck\r\n" +
            "\r\n" +
            "This deck classifies as **Bracket 3: Upgraded** (Upgraded).\r\n" +
            "\r\n" +
            "## WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 1 Game Changer (B3 allows up to 3).\r\n" +
            "- No two-card win combos detected.\r\n" +
            "- 0 mass land denial.\r\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "\r\n",
        "Claude" =>
            "<bracket_classification>\r\n" +
            "<deck_name>Literal classification deck</deck_name>\r\n" +
            "<verdict>This deck classifies as Bracket 3: Upgraded (Upgraded).</verdict>\r\n" +
            "\r\n" +
            "<why_this_bracket>\r\n" +
            "WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 1 Game Changer (B3 allows up to 3).\r\n" +
            "- No two-card win combos detected.\r\n" +
            "- 0 mass land denial.\r\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "</why_this_bracket>\r\n" +
            "\r\n" +
            "<effective_date_note>\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "</effective_date_note>\r\n" +
            "</bracket_classification>\r\n",
        "Gemini" =>
            "You are an expert Commander deck analyst. Review the following bracket classification and provide guidance.\r\n" +
            "\r\n" +
            "Think carefully through the evidence before responding.\r\n" +
            "\r\n" +
            "## Bracket Classification: Literal classification deck\r\n" +
            "\r\n" +
            "This deck classifies as **Bracket 3: Upgraded** (Upgraded).\r\n" +
            "\r\n" +
            "## WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 1 Game Changer (B3 allows up to 3).\r\n" +
            "- No two-card win combos detected.\r\n" +
            "- 0 mass land denial.\r\n" +
            "- 0 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "\r\n",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };

    public static string Balancer(string platform) => platform switch
    {
        "ChatGPT" =>
            "EXECUTE NOW — perform the entire task defined below and output the complete result in this reply. Do not ask which task to run, do not ask for confirmation, and do not wait for further instructions; the full task is specified below.\r\n" +
            "\r\n" +
            "## Bracket Classification: Literal balancer deck\r\n" +
            "\r\n" +
            "This deck classifies as **Bracket 4: Optimized** (Optimized).\r\n" +
            "\r\n" +
            "## WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 4 Game Changers (B4 has no cap).\r\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\r\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\r\n" +
            "- 1 mass land denial: Armageddon.\r\n" +
            "- 1 mass land denial: Ravages of War.\r\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "\r\n" +
            "## FLOOR VIOLATIONS — cards that exceed B2 Core\r\n" +
            "\r\n" +
            "- Mana Crypt [Game Changer]\r\n" +
            "- The One Ring [Game Changer]\r\n" +
            "- Jeweled Lotus [Game Changer]\r\n" +
            "- Cyclonic Rift [Game Changer]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Demonic Consultation [Combo half]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Tainted Pact [Combo half]\r\n" +
            "- Armageddon [Mass land denial]\r\n" +
            "- Ravages of War [Mass land denial]\r\n" +
            "\r\n" +
            "## STARTER CUTS to reach B2 Core\r\n" +
            "\r\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\r\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\r\n" +
            "- Cut Armageddon — no mass land denial at B2.\r\n" +
            "- Cut Ravages of War — no mass land denial at B2.\r\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\r\n" +
            "\r\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\r\n",
        "Claude" =>
            "<bracket_classification>\r\n" +
            "<deck_name>Literal balancer deck</deck_name>\r\n" +
            "<verdict>This deck classifies as Bracket 4: Optimized (Optimized).</verdict>\r\n" +
            "\r\n" +
            "<why_this_bracket>\r\n" +
            "WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 4 Game Changers (B4 has no cap).\r\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\r\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\r\n" +
            "- 1 mass land denial: Armageddon.\r\n" +
            "- 1 mass land denial: Ravages of War.\r\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "</why_this_bracket>\r\n" +
            "\r\n" +
            "<effective_date_note>\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "</effective_date_note>\r\n" +
            "\r\n" +
            "<balancer>\r\n" +
            "<floor_violations heading=\"FLOOR VIOLATIONS — cards that exceed B2 Core\">\r\n" +
            "- Mana Crypt [Game Changer]\r\n" +
            "- The One Ring [Game Changer]\r\n" +
            "- Jeweled Lotus [Game Changer]\r\n" +
            "- Cyclonic Rift [Game Changer]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Demonic Consultation [Combo half]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Tainted Pact [Combo half]\r\n" +
            "- Armageddon [Mass land denial]\r\n" +
            "- Ravages of War [Mass land denial]\r\n" +
            "</floor_violations>\r\n" +
            "\r\n" +
            "<starter_cuts heading=\"STARTER CUTS to reach B2 Core\">\r\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\r\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\r\n" +
            "- Cut Armageddon — no mass land denial at B2.\r\n" +
            "- Cut Ravages of War — no mass land denial at B2.\r\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\r\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\r\n" +
            "</starter_cuts>\r\n" +
            "</balancer>\r\n" +
            "</bracket_classification>\r\n",
        "Gemini" =>
            "You are an expert Commander deck analyst. Review the following bracket classification and provide guidance.\r\n" +
            "\r\n" +
            "Think carefully through the evidence before responding.\r\n" +
            "\r\n" +
            "## Bracket Classification: Literal balancer deck\r\n" +
            "\r\n" +
            "This deck classifies as **Bracket 4: Optimized** (Optimized).\r\n" +
            "\r\n" +
            "## WHY THIS BRACKET\r\n" +
            "\r\n" +
            "- 4 Game Changers (B4 has no cap).\r\n" +
            "- 2-card win combo: Thassa's Oracle + Demonic Consultation.\r\n" +
            "- 2-card win combo: Thassa's Oracle + Tainted Pact.\r\n" +
            "- 1 mass land denial: Armageddon.\r\n" +
            "- 1 mass land denial: Ravages of War.\r\n" +
            "- 2 extra-turn cards (informational only; does not affect bracket number).\r\n" +
            "- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.\r\n" +
            "\r\n" +
            "Game Changers list effective 2026-02-09. Re-confirm Game Changers membership before suggesting swaps.\r\n" +
            "\r\n" +
            "## FLOOR VIOLATIONS — cards that exceed B2 Core\r\n" +
            "\r\n" +
            "- Mana Crypt [Game Changer]\r\n" +
            "- The One Ring [Game Changer]\r\n" +
            "- Jeweled Lotus [Game Changer]\r\n" +
            "- Cyclonic Rift [Game Changer]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Demonic Consultation [Combo half]\r\n" +
            "- Thassa's Oracle [Combo half]\r\n" +
            "- Tainted Pact [Combo half]\r\n" +
            "- Armageddon [Mass land denial]\r\n" +
            "- Ravages of War [Mass land denial]\r\n" +
            "\r\n" +
            "## STARTER CUTS to reach B2 Core\r\n" +
            "\r\n" +
            "- Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win combo.\r\n" +
            "- Cut Thassa's Oracle or Tainted Pact — breaks the two-card win combo.\r\n" +
            "- Cut Armageddon — no mass land denial at B2.\r\n" +
            "- Cut Ravages of War — no mass land denial at B2.\r\n" +
            "- Trim 4 of: Mana Crypt, The One Ring, Jeweled Lotus, Cyclonic Rift — B2 allows up to 0 Game Changers; you run 4.\r\n" +
            "\r\n" +
            "A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.\r\n",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };
}
