namespace SkillBridge.Domain.Entities;

/// <summary>Shared tag vocabulary. Seeded only; never created by users.</summary>
public class Skill
{
    private Skill() { } // EF

    public Skill(int id, string name)
    {
        Id = id;
        Name = name.Trim();
        NormalizedName = Normalize(name);
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
