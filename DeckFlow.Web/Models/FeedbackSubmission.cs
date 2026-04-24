using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models;

public sealed class FeedbackSubmission
{
    [Required]
    public FeedbackType Type { get; set; } = FeedbackType.Comment;

    [Required]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Message must be 10–4000 characters.")]
    public string Message { get; set; } = string.Empty;

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    // Honeypot field. Must remain empty. Never surface to users.
    public string? Website { get; set; }
}
