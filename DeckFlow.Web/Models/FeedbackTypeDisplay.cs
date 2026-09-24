using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DeckFlow.Web.Models;

/// <summary>Provides display text for feedback types.</summary>
public static class FeedbackTypeDisplay
{
    /// <summary>Gets the display name, or enum name when no display attribute exists.</summary>
    public static string GetDisplayName(FeedbackType type)
    {
        return typeof(FeedbackType)
            .GetField(type.ToString())?
            .GetCustomAttribute<DisplayAttribute>()?
            .GetName() ?? type.ToString();
    }
}
