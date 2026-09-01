namespace LearnSphere.API.Models;

// AI Speed Match scoring config (admin-configurable, see AdminController). Key is
// the stable identifier the match-score calculator switches on — Label is just
// display text for the admin panel and isn't guaranteed unique/stable enough to
// key off of. "na1"/"na2" are reserved slots for future criteria; they carry a
// weightage but no computable score, so they never contribute points.
public class ScoringWeightage
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty; // rating | activeness | disputes | experience | na1 | na2
    public string Label { get; set; } = string.Empty;
    public int Percent { get; set; } = 0;
    public int SortOrder { get; set; } = 0;
}
