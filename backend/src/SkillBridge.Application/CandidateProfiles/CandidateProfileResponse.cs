namespace SkillBridge.Application.CandidateProfiles;

public sealed record CandidateProfileResponse(
    string UserId,
    string? Headline,
    string? Bio,
    string? GitHubUrl,
    DateTime? UpdatedAt,
    IReadOnlyList<CandidateProfileSkillResponse> Skills);

public sealed record CandidateProfileSkillResponse(int Id, string Name);
