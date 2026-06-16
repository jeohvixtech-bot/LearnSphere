namespace LearnSphere.API.Models;

public class Institution
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty; // Singapore | Malaysia
    public string Type { get; set; } = string.Empty;    // Primary | Secondary | Junior College | Polytechnic/Vocational | University/Tertiary
}
