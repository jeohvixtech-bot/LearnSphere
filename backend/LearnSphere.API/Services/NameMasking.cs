namespace LearnSphere.API.Services;

// Masks a parent's real name for display on a class remark — keeps the last
// 2 characters of each space-separated word, replaces everything before that
// with '*'. Single-character words are left unmasked (nothing to mask).
// e.g. "Tan Wei Ming" -> "**n **i **ng". Kept in exactly one place so every
// remark surface (parent sessions, tutor bulletin board, catalog, welcome
// page, admin dispute queue) applies the identical rule.
public static class NameMasking
{
    public static string Mask(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

        var words = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var masked = words.Select(w =>
        {
            if (w.Length <= 2) return w;
            return new string('*', w.Length - 2) + w[^2..];
        });
        return string.Join(' ', masked);
    }
}
