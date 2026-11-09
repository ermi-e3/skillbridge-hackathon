using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Domain.Entities;

/// <summary>
/// The candidate's matchable profile. Shares its primary key with AppUser (UserId).
/// Created empty at registration; filled in on the profile page and onboarding.
/// </summary>
public class CandidateProfile
{
    public const int MaxSkills = 30;

    private readonly List<CandidateSkill> _skills = new();
    private readonly List<CandidateExperience> _experienceHistory = new();

    private CandidateProfile() { } // EF

    public CandidateProfile(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; private set; } = null!;
    public string? Headline { get; private set; }
    public string? Bio { get; private set; }
    public string? GitHubUrl { get; private set; }
    public string? Location { get; private set; }
    public ExperienceLevel? ExperienceLevel { get; private set; }
    public int? YearsOfExperience { get; private set; }
    public WorkArrangement? PreferredWorkArrangement { get; private set; }
    public JobType? PreferredJobType { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public string? DefaultCvFileName { get; private set; }
    public string? DefaultCvOriginalFileName { get; private set; }
    public string? DefaultCvContentType { get; private set; }
    public long? DefaultCvFileSizeBytes { get; private set; }
    public DateTime? DefaultCvUploadedAt { get; private set; }

    public bool HasDefaultCv => !string.IsNullOrWhiteSpace(DefaultCvFileName);

    public IReadOnlyCollection<CandidateSkill> Skills => _skills;
    public IReadOnlyCollection<CandidateExperience> ExperienceHistory => _experienceHistory;

    public bool HasSkills => _skills.Count > 0;

    /// <summary>
    /// Indicates whether the candidate has completed their initial profile setup with required matching criteria
    /// (at least one skill, headline, experience level, work arrangement, and job type preferences).
    /// </summary>
    public bool HasCompletedProfileSetup =>
        HasSkills &&
        ExperienceLevel.HasValue &&
        PreferredWorkArrangement.HasValue &&
        PreferredJobType.HasValue &&
        !string.IsNullOrWhiteSpace(Headline);

    public void SetDefaultCv(
        string storedFileName,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        DateTime nowUtc)
    {
        DefaultCvFileName = storedFileName;
        DefaultCvOriginalFileName = originalFileName;
        DefaultCvContentType = contentType;
        DefaultCvFileSizeBytes = fileSizeBytes;
        DefaultCvUploadedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public void RemoveDefaultCv(DateTime nowUtc)
    {
        DefaultCvFileName = null;
        DefaultCvOriginalFileName = null;
        DefaultCvContentType = null;
        DefaultCvFileSizeBytes = null;
        DefaultCvUploadedAt = null;
        UpdatedAt = nowUtc;
    }

    public void Update(
        string headline,
        string? bio,
        string? gitHubUrl,
        IEnumerable<int> skillIds,
        DateTime nowUtc)
    {
        Update(
            headline,
            bio,
            gitHubUrl,
            skillIds,
            Location,
            ExperienceLevel,
            YearsOfExperience,
            PreferredWorkArrangement,
            PreferredJobType,
            nowUtc);
    }

    public void Update(
        string headline,
        string? bio,
        string? gitHubUrl,
        IEnumerable<int> skillIds,
        string? location,
        ExperienceLevel? experienceLevel,
        int? yearsOfExperience,
        WorkArrangement? preferredWorkArrangement,
        JobType? preferredJobType,
        DateTime nowUtc,
        IEnumerable<(int SkillId, SkillProficiency Proficiency, int Years, bool IsPreferred)>? skillDetails = null,
        IEnumerable<CandidateExperience>? experiences = null,
        IEnumerable<int>? preferredSkillIds = null)
    {
        if (skillDetails != null)
        {
            ReplaceSkillsWithDetails(skillDetails);
        }
        else
        {
            ReplaceSkills(skillIds, preferredSkillIds);
        }

        if (experiences != null)
        {
            ReplaceExperiences(experiences);
        }

        Headline = headline.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        GitHubUrl = string.IsNullOrWhiteSpace(gitHubUrl) ? null : gitHubUrl.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        
        ExperienceLevel = experienceLevel;
        YearsOfExperience = yearsOfExperience.HasValue ? Math.Max(0, yearsOfExperience.Value) : null;
        PreferredWorkArrangement = preferredWorkArrangement;
        PreferredJobType = preferredJobType;

        UpdatedAt = nowUtc;
    }

    public void ReplaceSkills(IEnumerable<int> skillIds, IEnumerable<int>? preferredSkillIds = null)
    {
        var primaryIds = (skillIds ?? Enumerable.Empty<int>()).ToHashSet();
        var preferredIds = (preferredSkillIds ?? Enumerable.Empty<int>()).ToHashSet();

        // Enforce mutual exclusivity: if any skill is in both, remove it from preferred
        preferredIds.ExceptWith(primaryIds);

        var allWanted = primaryIds.Concat(preferredIds).ToHashSet();
        if (allWanted.Count is < 1 or > MaxSkills || allWanted.Any(id => id <= 0))
        {
            throw new BusinessRuleException("validation.failed", "Invalid skills",
                $"Pick between 1 and {MaxSkills} valid skills.");
        }

        _skills.RemoveAll(skill => !allWanted.Contains(skill.SkillId));

        var existingMap = _skills.ToDictionary(s => s.SkillId);
        foreach (var skill in _skills)
        {
            var isPref = preferredIds.Contains(skill.SkillId);
            skill.SetPreferred(isPref);
        }

        foreach (var skillId in allWanted.Where(id => !existingMap.ContainsKey(id)))
        {
            var isPref = preferredIds.Contains(skillId);
            _skills.Add(new CandidateSkill(UserId, skillId, isPreferred: isPref));
        }
    }

    public void ReplaceSkillsWithDetails(IEnumerable<(int SkillId, SkillProficiency Proficiency, int Years, bool IsPreferred)> skillDetails)
    {
        var detailsList = skillDetails.DistinctBy(d => d.SkillId).ToList();
        if (detailsList.Count is < 1 or > MaxSkills || detailsList.Any(d => d.SkillId <= 0))
        {
            throw new BusinessRuleException("validation.failed", "Invalid skills",
                $"Pick between 1 and {MaxSkills} valid skills.");
        }

        var wantedIds = detailsList.Select(d => d.SkillId).ToHashSet();
        _skills.RemoveAll(skill => !wantedIds.Contains(skill.SkillId));

        foreach (var detail in detailsList)
        {
            var existing = _skills.FirstOrDefault(s => s.SkillId == detail.SkillId);
            if (existing != null)
            {
                existing.Update(detail.Proficiency, detail.Years, detail.IsPreferred);
            }
            else
            {
                _skills.Add(new CandidateSkill(UserId, detail.SkillId, detail.Proficiency, detail.Years, detail.IsPreferred));
            }
        }
    }

    public void ReplaceExperiences(IEnumerable<CandidateExperience> experiences)
    {
        _experienceHistory.Clear();
        foreach (var exp in experiences)
        {
            _experienceHistory.Add(exp);
        }
    }
}
