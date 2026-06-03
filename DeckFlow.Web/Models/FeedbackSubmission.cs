using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models;

/// <summary>Request model bound from the public feedback submission form.</summary>
public sealed class FeedbackSubmission
{
    /// <summary>Kind of feedback submitted by the user.</summary>
    [Required]
    public FeedbackType Type { get; set; } = FeedbackType.Comment;

    /// <summary>Feedback message body supplied by the user.</summary>
    [Required]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Message must be 10–4000 characters.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional contact email supplied by the user.</summary>
    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    // Honeypot field. Must remain empty. Never surface to users.
    /// <summary>Hidden honeypot field used to reject automated submissions.</summary>
    public string? Website { get; set; }
}
