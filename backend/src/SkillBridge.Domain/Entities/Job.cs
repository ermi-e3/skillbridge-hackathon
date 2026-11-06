using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Domain.Entities;

/// <summary>An opening posted by an employer. Must require at least one skill (protects the match division).</summary>
public class Job
{
    public const int MinSkills = 1;
    public const int MaxSkills = 25;

    private readonly List<JobSkill> _requiredSkills = new();
    private readonly List<JobApplication> _applications = new();

    private Job() { } // EF

    public Job(
        string employerId,
        string title,
        string description,
        string? location,
        IEnumerable<int> requiredSkillIds,
        DateTime createdAt,
        JobType jobType = JobType.FullTime,
        WorkArrangement? workArrangement = null,
        ExperienceLevel experienceLevel = ExperienceLevel.EntryLevel,
        int minExperienceYears = 0,
        IEnumerable<int>? preferredSkillIds = null,
        IEnumerable<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>? skillDetails = null,
        DateTime? deadline = null)
    {
        EmployerId = employerId;
        Title = title.Trim();
        Description = description.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        IsOpen = true;
        CreatedAt = createdAt;
        Deadline = deadline;
        JobType = jobType;
        ExperienceLevel = experienceLevel;
        MinExperienceYears = Math.Max(0, minExperienceYears);

        if (workArrangement.HasValue)
        {
            WorkArrangement = workArrangement.Value;
        }
        else if (Location != null && Location.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            WorkArrangement = WorkArrangement.Remote;
        }
        else if (Location != null && Location.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            WorkArrangement = WorkArrangement.Hybrid;
        }
        else
        {
            WorkArrangement = WorkArrangement.OnSite;
        }

        if (skillDetails != null)
        {
            var detailsList = skillDetails.DistinctBy(d => d.SkillId).ToList();
            var reqCount = detailsList.Count(d => d.IsRequired);
            if (reqCount is < MinSkills or > MaxSkills)
                throw new BusinessRuleException("validation", "Invalid skills",
                    $"Job must require between {MinSkills} and {MaxSkills} required skills.");

            foreach (var detail in detailsList)
            {
                _requiredSkills.Add(new JobSkill(this, detail.SkillId, detail.IsRequired, detail.MinProficiency));
            }
        }
        else
        {
            var reqSkillIdSet = requiredSkillIds.Distinct().ToList();
            if (reqSkillIdSet.Count is < MinSkills or > MaxSkills)
                throw new BusinessRuleException("validation", "Invalid skills",
                    $"Job must require between {MinSkills} and {MaxSkills} skills.");

            foreach (var skillId in reqSkillIdSet)
            {
                _requiredSkills.Add(new JobSkill(this, skillId, isRequired: true));
            }

            if (preferredSkillIds != null)
            {
                var prefSet = preferredSkillIds.Distinct().Where(id => !reqSkillIdSet.Contains(id)).ToList();
                foreach (var prefId in prefSet)
                {
                    _requiredSkills.Add(new JobSkill(this, prefId, isRequired: false));
                }
            }
        }
    }

    public int Id { get; private set; }
    public string EmployerId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? Location { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? Deadline { get; private set; }
    public JobType JobType { get; private set; } = JobType.FullTime;
    public WorkArrangement WorkArrangement { get; private set; } = WorkArrangement.OnSite;
    public ExperienceLevel ExperienceLevel { get; private set; } = ExperienceLevel.EntryLevel;
    public int MinExperienceYears { get; private set; } = 0;

    public IReadOnlyCollection<JobSkill> RequiredSkills => _requiredSkills;
    public IReadOnlyCollection<JobApplication> Applications => _applications;

    public static readonly TimeSpan EditingWindow = TimeSpan.FromHours(2);

    public bool IsOwnedBy(string userId) => EmployerId == userId;

    public bool IsEditingAllowed(DateTime utcNow) => utcNow - CreatedAt <= EditingWindow;

    public bool IsExpired(DateTime utcNow) => Deadline.HasValue && utcNow >= Deadline.Value;

    public bool CheckExpiration(DateTime utcNow)
    {
        if (IsOpen && IsExpired(utcNow))
        {
            IsOpen = false;
            return true;
        }
        return false;
    }

    public void Close() => IsOpen = false;

    public void Update(
        string title,
        string description,
        string? location,
        IEnumerable<int> requiredSkillIds,
        JobType jobType,
        WorkArrangement? workArrangement,
        ExperienceLevel experienceLevel,
        int minExperienceYears,
        IEnumerable<int>? preferredSkillIds = null,
        IEnumerable<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>? skillDetails = null,
        DateTime? deadline = null)
    {
        Title = title.Trim();
        Description = description.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        JobType = jobType;
        ExperienceLevel = experienceLevel;
        MinExperienceYears = Math.Max(0, minExperienceYears);
        Deadline = deadline;

        if (workArrangement.HasValue)
        {
            WorkArrangement = workArrangement.Value;
        }
        else if (Location != null && Location.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            WorkArrangement = WorkArrangement.Remote;
        }
        else if (Location != null && Location.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            WorkArrangement = WorkArrangement.Hybrid;
        }
        else
        {
            WorkArrangement = WorkArrangement.OnSite;
        }

        UpdateSkills(requiredSkillIds, preferredSkillIds, skillDetails);
    }

    public void UpdateSkills(
        IEnumerable<int> requiredSkillIds,
        IEnumerable<int>? preferredSkillIds = null,
        IEnumerable<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>? skillDetails = null)
    {
        var targetDetails = new List<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>();
        if (skillDetails != null)
        {
            targetDetails.AddRange(skillDetails.DistinctBy(d => d.SkillId));
        }
        else
        {
            var reqSkillIdSet = requiredSkillIds.Distinct().ToList();
            foreach (var id in reqSkillIdSet)
            {
                targetDetails.Add((id, true, SkillProficiency.Intermediate));
            }

            if (preferredSkillIds != null)
            {
                var prefSet = preferredSkillIds.Distinct().Where(id => !reqSkillIdSet.Contains(id));
                foreach (var id in prefSet)
                {
                    targetDetails.Add((id, false, SkillProficiency.Intermediate));
                }
            }
        }

        var reqCount = targetDetails.Count(d => d.IsRequired);
        if (reqCount is < MinSkills or > MaxSkills)
            throw new BusinessRuleException("validation", "Invalid skills",
                $"Job must require between {MinSkills} and {MaxSkills} required skills.");

        var targetDict = targetDetails.ToDictionary(d => d.SkillId);

        // Remove skills no longer present
        _requiredSkills.RemoveAll(js => !targetDict.ContainsKey(js.SkillId));

        // Update existing or add new skills
        foreach (var (skillId, isRequired, minProficiency) in targetDetails)
        {
            var existing = _requiredSkills.FirstOrDefault(js => js.SkillId == skillId);
            if (existing != null)
            {
                existing.UpdateRequirement(isRequired, minProficiency);
            }
            else
            {
                _requiredSkills.Add(new JobSkill(this, skillId, isRequired, minProficiency));
            }
        }
    }
}
