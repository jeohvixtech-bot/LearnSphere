namespace LearnSphere.API.Services;

// Shared password rules — used by registration and by change-password, so the policy
// lives in exactly one place, the way NameValidator already does for full names.
//
// Registration previously accepted anything at all, including a single character, while
// the name on the same form was carefully validated. These accounts hold children's names,
// birth dates, schools and billing history, so a floor is warranted.
//
// The rule is a length floor plus a modest variety requirement rather than the classic
// "one upper, one lower, one digit, one symbol": long passphrases are what actually
// survive guessing, and stacking character-class rules mostly pushes people toward
// "Password1!" and a sticky note.
public static class PasswordValidator
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    // Rejected outright regardless of length — these are the first things tried.
    private static readonly string[] Obvious =
    {
        "password", "12345678", "123456789", "qwerty", "letmein", "welcome",
        "admin123", "changeme", "learnsphere", "iloveyou", "abc12345"
    };

    /// <summary>Null when acceptable; otherwise the reason, phrased for the end user.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password is required.";

        if (password.Length < MinLength)
            return $"Password must be at least {MinLength} characters.";

        if (password.Length > MaxLength)
            return $"Password must be {MaxLength} characters or fewer.";

        var lowered = password.ToLowerInvariant();
        if (Obvious.Any(o => lowered == o || lowered.Contains(o)))
            return "That password is too easy to guess. Please choose another.";

        // Two of the four categories. A long passphrase of plain words clears this on
        // letters plus a space or apostrophe, which is the intent.
        var categories = 0;
        if (password.Any(char.IsLower)) categories++;
        if (password.Any(char.IsUpper)) categories++;
        if (password.Any(char.IsDigit)) categories++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) categories++;

        if (categories < 2)
            return "Password must combine at least two of: lower case, upper case, numbers, symbols.";

        return null;
    }
}
