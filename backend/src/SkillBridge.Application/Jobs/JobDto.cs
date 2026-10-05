using SkillBridge.Application.Skills;

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
    bool? IsOwner);
