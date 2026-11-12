using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed record JobSkillRequirement(
    int SkillId,
    string SkillName,
    bool IsRequired,
    SkillProficiency MinProficiency = SkillProficiency.Intermediate);

public sealed record JobMatchCriteria(
    int JobId,
    string Title,
    string? Location,
    JobType JobType,
    WorkArrangement WorkArrangement,
    ExperienceLevel ExperienceLevel,
    int MinExperienceYears,
    IReadOnlyList<JobSkillRequirement> Skills);

public sealed record CandidateSkillProfile(
    int SkillId,
    string SkillName,
    SkillProficiency Proficiency,
    int YearsOfExperience,
    bool IsPreferred = false);

public sealed record CandidateExperienceSummary(
    string Title,
    string Company,
    int Months);

public sealed record CandidateMatchProfile(
    string UserId,
    string? Location,
    ExperienceLevel? ExperienceLevel,
    int? YearsOfExperience,
    WorkArrangement? PreferredWorkArrangement,
    JobType? PreferredJobType,
    IReadOnlyList<CandidateSkillProfile> Skills,
    IReadOnlyList<CandidateExperienceSummary>? ExperienceHistory = null);

