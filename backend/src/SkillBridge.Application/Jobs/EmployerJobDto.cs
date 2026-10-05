using SkillBridge.Application.Skills;

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
    ApplicantCountsDto ApplicantCounts);
