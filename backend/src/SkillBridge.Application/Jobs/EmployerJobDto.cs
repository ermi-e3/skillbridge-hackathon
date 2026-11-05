using SkillBridge.Application.Skills;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed record ApplicantCountsDto(
    int Total,
    int Received,
    int Shortlisted,
    int Rejected,
    int Withdrawn);

public sealed record EmployerJobDto(
    int Id,
    string Title,
    string? Location,
    bool IsOpen,
    DateTime CreatedAt,
    IReadOnlyList<SkillDto> RequiredSkills,
    ApplicantCountsDto ApplicantCounts,
    IReadOnlyList<SkillDto>? PreferredSkills = null,
    JobType JobType = JobType.FullTime,
    WorkArrangement WorkArrangement = WorkArrangement.OnSite,
    ExperienceLevel ExperienceLevel = ExperienceLevel.EntryLevel,
    int MinExperienceYears = 0,
    string? Description = null);
