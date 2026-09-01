using System.Text.RegularExpressions;

namespace LearnSphere.API.Services;

// Shared full-name validation — used by registration (AuthController) and child
// profile create/edit (StudentsController) so the character rule lives in exactly
// one place. Profanity checking itself is delegated to ProfanityFilter, shared with
// every other free-text field. Mirrored client-side in
// frontend/app/services/name-validation.service.js; kept in sync manually.
public static class NameValidator
{
    private static readonly Regex NamePattern = new(@"^[\p{L}\s.'-]+$", RegexOptions.Compiled);

    // Returns an error message, or null if the name is valid.
    public static string? Validate(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length < 2 || trimmed.Length > 60)
            return "Name must be between 2 and 60 characters.";
        if (!NamePattern.IsMatch(trimmed))
            return "Name can only contain letters, spaces, hyphens, and apostrophes — no numbers or special characters.";
        if (ProfanityFilter.ContainsProfanity(trimmed))
            return "Please enter a valid, appropriate name.";

        return null;
    }
}
