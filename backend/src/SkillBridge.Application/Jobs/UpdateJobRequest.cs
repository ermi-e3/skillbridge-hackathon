using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed record UpdateJobRequest(
    string Title,
    string Description,
    string? Location,
    IReadOnlyList<int> RequiredSkillIds,
    JobType JobType = JobType.FullTime,
    WorkArrangement? WorkArrangement = null,
    ExperienceLevel ExperienceLevel = ExperienceLevel.EntryLevel,
    int MinExperienceYears = 0,
    IReadOnlyList<int>? PreferredSkillIds = null,
    IReadOnlyList<JobSkillRequirementDto>? SkillRequirements = null,
    DateTime? Deadline = null,
    IReadOnlyList<string>? CustomSkills = null,
    IReadOnlyList<string>? CustomPreferredSkills = null);

