using System.Text.RegularExpressions;

namespace LearnSphere.API.Services;

// Shared profanity check for free-text fields (chat, reviews, lesson reports, bio,
// learning goals, booking/counter-proposal messages, issue reports, admin notes) —
// deliberately separate from NameValidator's character whitelist, since free text
// needs to allow digits and normal punctuation. Mirrored client-side in
// frontend/app/services/profanity-filter.service.js; kept in sync manually —
// Words, LetterClasses, Suffixes and Exceptions must match there exactly.
public static class ProfanityFilter
{
    private static readonly string[] Words =
    {
        "fuck", "shit", "bitch", "bastard", "cunt", "dick", "piss", "pussy", "cock", "slut", "whore",
        "asshole", "nigger", "nigga", "fag", "faggot", "retard", "rape", "rapist", "porn", "sex",
        "damn", "hell", "crap", "douche", "wanker", "twat", "prick", "skank"
    };

    // Common look-alike substitutions (f*ck, sh1t, b!tch, a$$hole, fvck). Letters not
    // listed only match themselves.
    private static readonly Dictionary<char, string> LetterClasses = new()
    {
        ['a'] = "a4@*", ['e'] = "e3*", ['i'] = "i1!|*", ['o'] = "o0*",
        ['u'] = "uv*", ['s'] = "s5$", ['t'] = "t7"
    };

    // Inflections/compounds accepted after a listed word (fucking, shitty, bitches,
    // dickhead, douchebag). Doubled letters like shi-tt-y are covered by the per-letter
    // repeat below, not by listing "ty".
    private static readonly string[] Suffixes =
    {
        "s", "es", "ed", "er", "ers", "ing", "in", "y", "ies", "head", "heads",
        "face", "hole", "holes", "bag", "bags"
    };

    // Innocent words the suffix/repeat rules would otherwise catch.
    private static readonly HashSet<string> Exceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pricked", "pricking", "cocky", "cocker", "cockers"
    };

    private static string LetterPattern(char c) =>
        LetterClasses.TryGetValue(c, out var cls)
            ? "[" + Regex.Escape(cls).Replace("]", "\\]") + "]+"
            : Regex.Escape(c.ToString()) + "+";

    // Each letter may repeat (fuckk, fuuuck, shiiit) and may be a look-alike, optionally
    // followed by one suffix. Boundaries are "no letter/digit on either side" rather
    // than \b, since look-alikes like @ $ ! * aren't word characters — and so words
    // merely containing a listed one (class, Sussex, shell, Scunthorpe) stay clean.
    private static readonly Regex Pattern = new(
        @"(?<![a-z0-9])(?:" +
        string.Join("|", Words.Select(w => string.Concat(w.Select(LetterPattern)))) +
        @")(?:" + string.Join("|", Suffixes) + @")?(?![a-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ContainsProfanity(string? text) =>
        !string.IsNullOrEmpty(text) &&
        Pattern.Matches(text).Any(m => !Exceptions.Contains(m.Value));

    // Returns an error message, or null if clean.
    public static string? Validate(string? text) =>
        ContainsProfanity(text) ? "Please remove inappropriate language before submitting." : null;
}
