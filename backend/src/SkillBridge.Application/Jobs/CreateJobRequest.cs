namespace SkillBridge.Application.Jobs;

public sealed record CreateJobRequest(
    string Title,
    string Description,
    string? Location,
    IReadOnlyList<int> RequiredSkillIds);
