namespace SkillBridge.Application.Jobs;

public sealed record JobMatchBreakdownDto(
    int RequiredSkillsScore,
    int PreferredSkillsScore,
    int ExperienceScore,
    int WorkArrangementScore,
    int LocationScore,
    int JobTypeScore,
    int TotalScore,
    string ExperienceCompatibility,
    string WorkArrangementCompatibility,
    string LocationCompatibility,
    string JobTypeCompatibility);

