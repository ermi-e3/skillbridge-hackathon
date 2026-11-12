using SkillBridge.Application.CandidateProfiles;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Applications;

public sealed record ApplicantProfileDto(
    string CandidateId,
    string FullName,
    string Email,
    string? Headline,
    string? Bio,
    string? GitHubUrl,
    string? Location,
    ExperienceLevel? ExperienceLevel,
    int? YearsOfExperience,
    WorkArrangement? PreferredWorkArrangement,
    JobType? PreferredJobType,
    IReadOnlyList<CandidateProfileSkillResponse> Skills,
    IReadOnlyList<CandidateExperienceDto> ExperienceHistory,
    int ApplicationId,
    string ApplicationStatus,
    DateTime AppliedAt,
    DateTime? StatusChangedAt,
    string? CoverNote,
    JobMatchDto? Match,
    bool HasCv,
    string? CvOriginalFileName,
    long? CvFileSizeBytes,
    DateTime? CvUploadedAt,
    string? CvDownloadUrl);

