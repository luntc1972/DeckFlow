using Microsoft.AspNetCore.Mvc;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the judge questions workflow.
/// </summary>
public sealed class JudgeQuestionsController : DeckToolControllerBase
{
    /// <summary>
    /// Renders the "Ask a Judge" page that primarily links to the live MTG judge chat
    /// and offers a secondary ChatGPT prompt generator. Optionally pre-fills a card name
    /// passed in via query string from a Card Lookup deep link.
    /// </summary>
    /// <param name="card">Optional card name to pre-populate the question form.</param>
    [HttpGet("/judge-questions")]
    [FeatureFlagGate("tool.judge-questions.enabled")]
    public IActionResult JudgeQuestions(string? card)
    {
        return View("JudgeQuestions", new JudgeQuestionViewModel
        {
            ActiveTab = DeckPageTab.JudgeQuestions,
            PrefilledCardName = string.IsNullOrWhiteSpace(card) ? null : card.Trim(),
        });
    }
}
