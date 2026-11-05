using SkillBridge.Application.Skills;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed record JobDto(
    int Id,
    string Title,
    string CompanyName,
    string? Location,
    bool IsOpen,
    DateTime CreatedAt,
    IReadOnlyList<SkillDto> RequiredSkills,
    JobMatchDto? Match,
    int? MyMatchPercent,
    bool? HasApplied,
    string Description,
    bool? IsOwner,
    JobType JobType = JobType.FullTime,
    WorkArrangement WorkArrangement = WorkArrangement.OnSite,
    ExperienceLevel ExperienceLevel = ExperienceLevel.EntryLevel,
    int MinExperienceYears = 0,
    IReadOnlyList<SkillDto>? PreferredSkills = null,
    string? ApplicationStatus = null,
    int? ApplicationId = null);
