namespace LearnSphere.API.Models;

// One published version of the Terms and Conditions. Rows are never edited after
// publishing: a change means a new row, so each user's TermsVersionId keeps pointing
// at the exact text they agreed to. The newest row (highest Id) is the current one.
public class TermsVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;   // label, e.g. "2026-10-14"
    // Plain text: "## " starts a heading, "- " starts a bullet, blank line separates
    // paragraphs, **bold** is allowed. Never rendered as HTML.
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public int? PublishedByUserId { get; set; }
}
