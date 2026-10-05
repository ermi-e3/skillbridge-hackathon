using SkillBridge.Domain.Common;

namespace SkillBridge.Domain.Entities;

/// <summary>An opening posted by an employer. Must require at least one skill (protects the match division).</summary>
public class Job
{
    public const int MinSkills = 1;
    public const int MaxSkills = 15;

    private readonly List<JobSkill> _requiredSkills = new();
    private readonly List<JobApplication> _applications = new();

    private Job() { } // EF

    

    public int Id { get; private set; }
    public string EmployerId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? Location { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTime CreatedAt { get; private set; }

    //public AppUser Employer { get; private set; } = null!;
    public IReadOnlyCollection<JobSkill> RequiredSkills => _requiredSkills;
    public IReadOnlyCollection<JobApplication> Applications => _applications;

    public bool IsOwnedBy(string userId) => EmployerId == userId;

    public void Close() => IsOpen = false;
}
