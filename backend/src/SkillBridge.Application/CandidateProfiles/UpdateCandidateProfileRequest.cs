using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.CandidateProfiles;

public sealed record CandidateSkillDetailDto(
    int SkillId,
    SkillProficiency Proficiency = SkillProficiency.Intermediate,
    int YearsOfExperience = 1,
    bool IsPreferred = false);

public sealed record CandidateExperienceDto(
    int? Id,
    string Title,
    string Company,
    string? Location,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsCurrent,
    string? Description);

public sealed record UpdateCandidateProfileRequest(
    string Headline,
    string? Bio,
    string? GitHubUrl,
    IReadOnlyList<int> SkillIds,
    string? Location = null,
    ExperienceLevel? ExperienceLevel = null,
    int? YearsOfExperience = null,
    WorkArrangement? PreferredWorkArrangement = null,
    JobType? PreferredJobType = null,
    IReadOnlyList<CandidateSkillDetailDto>? SkillDetails = null,
    IReadOnlyList<CandidateExperienceDto>? ExperienceHistory = null,
    IReadOnlyList<int>? PreferredSkillIds = null);
