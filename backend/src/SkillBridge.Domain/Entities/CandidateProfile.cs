using SkillBridge.Domain.Common;

namespace SkillBridge.Domain.Entities;

/// <summary>
/// The candidate's matchable profile. Shares its primary key with AppUser (UserId).
/// Created empty at registration; filled in on the profile page.
/// </summary>
public class CandidateProfile
{
    public const int MaxSkills = 30;

    private readonly List<CandidateSkill> _skills = new();

    private CandidateProfile() { } // EF

    public CandidateProfile(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; private set; } = null!;
    public string? Headline { get; private set; }
    public string? Bio { get; private set; }
    public string? GitHubUrl { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    //public AppUser User { get; private set; } = null!;
    public IReadOnlyCollection<CandidateSkill> Skills => _skills;

    public bool HasSkills => _skills.Count > 0;

    
    //public void Update(string headline, string? bio, string? gitHubUrl, IEnumerable<int> skillIds, DateTime nowUtc)
    //{
    //    Headline = headline.Trim();
    //    Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
    //    GitHubUrl = string.IsNullOrWhiteSpace(gitHubUrl) ? null : gitHubUrl.Trim();
    //    ReplaceSkills(skillIds);
    //    UpdatedAt = nowUtc;
    //}

    //private void ReplaceSkills(IEnumerable<int> skillIds)
    //{
    //    var wanted = skillIds.ToHashSet();
    //    if (wanted.Count is < 1 or > MaxSkills)
    //        throw new BusinessRuleException(ErrorCodes.Validation, "Invalid skills",
    //            $"Pick between 1 and {MaxSkills} skills.");

    //    // Removed rows become orphans of a required relationship, so EF deletes them on save.
    //    _skills.RemoveAll(s => !wanted.Contains(s.SkillId));

    //    var existing = _skills.Select(s => s.SkillId).ToHashSet();
    //    foreach (var skillId in wanted.Where(id => !existing.Contains(id)))
    //        _skills.Add(new CandidateSkill(UserId, skillId));
    //}
}
