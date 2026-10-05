using SkillBridge.Application.Skills;

namespace SkillBridge.Application.Jobs;

public sealed record JobMatchDto(
    int Percent,
    IReadOnlyList<SkillDto> MatchedSkills,
    IReadOnlyList<SkillDto> MissingSkills);
