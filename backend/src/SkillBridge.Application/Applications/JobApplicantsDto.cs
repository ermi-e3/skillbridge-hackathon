using SkillBridge.Application.Jobs;
using SkillBridge.Application.Skills;

namespace SkillBridge.Application.Applications;

public sealed record JobSummaryDto(
    int Id,
    string Title,
    bool IsOpen,
    IReadOnlyList<SkillDto> RequiredSkills);

public sealed record CandidateSummaryDto(
    string UserId,
    string FullName,
    string Email,
    string? Headline,
    string? GitHubUrl,
    IReadOnlyList<SkillDto> Skills);

public sealed record JobApplicantItemDto(
    int ApplicationId,
    CandidateSummaryDto Candidate,
    string Status,
    DateTime AppliedAt,
    DateTime? StatusChangedAt,
    string? CoverNote,
    JobMatchDto Match);

public sealed record JobApplicantsDto(
    JobSummaryDto Job,
    IReadOnlyList<JobApplicantItemDto> Applicants);
