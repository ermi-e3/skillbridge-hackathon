namespace SkillBridge.Application.CandidateProfiles;

public sealed record UpdateCandidateProfileRequest(
    string Headline,
    string? Bio,
    string? GitHubUrl,
    IReadOnlyList<int> SkillIds);
