using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.CandidateProfiles;

public sealed record CandidateProfileResponse(
    string UserId,
    string? Headline,
    string? Bio,
    string? GitHubUrl,
    DateTime? UpdatedAt,
    IReadOnlyList<CandidateProfileSkillResponse> Skills,
    string? Location = null,
    ExperienceLevel? ExperienceLevel = null,
    int? YearsOfExperience = null,
    WorkArrangement? PreferredWorkArrangement = null,
    JobType? PreferredJobType = null,
    IReadOnlyList<CandidateExperienceDto>? ExperienceHistory = null,
    IReadOnlyList<CandidateProfileSkillResponse>? PreferredSkills = null);

public sealed record CandidateProfileSkillResponse(
    int Id,
    string Name,
    SkillProficiency Proficiency = SkillProficiency.Intermediate,
    int YearsOfExperience = 1,
    bool IsPreferred = false);
