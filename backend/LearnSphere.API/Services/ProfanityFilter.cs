using System.Text.RegularExpressions;

namespace LearnSphere.API.Services;

// Shared profanity check for free-text fields (chat, reviews, lesson reports, bio,
// learning goals, booking/counter-proposal messages, issue reports, admin notes) —
// deliberately separate from NameValidator's character whitelist, since free text
// needs to allow digits and normal punctuation. Mirrored client-side in
// frontend/app/services/profanity-filter.service.js; kept in sync manually.
public static class ProfanityFilter
{
    private static readonly string[] Words =
    {
        "fuck", "shit", "bitch", "bastard", "cunt", "dick", "piss", "pussy", "cock", "slut", "whore",
        "asshole", "nigger", "nigga", "fag", "faggot", "retard", "rape", "rapist", "porn", "sex",
        "damn", "hell", "crap", "douche", "wanker", "twat", "prick", "skank"
    };

    // Word-boundary match (\b...\b) rather than the name-check's split-on-punctuation
    // approach — free text has commas, exclamation marks, parentheses, etc. as word
    // boundaries too, not just spaces/hyphens/apostrophes/periods.
    private static readonly Regex Pattern = new(
        @"\b(" + string.Join("|", Words.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ContainsProfanity(string? text) =>
        !string.IsNullOrEmpty(text) && Pattern.IsMatch(text);

    // Returns an error message, or null if clean.
    public static string? Validate(string? text) =>
        ContainsProfanity(text) ? "Please remove inappropriate language before submitting." : null;
}
