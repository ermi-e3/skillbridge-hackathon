namespace SkillBridge.Application.Auth;

public sealed record AuthUser(
    string UserId,
    string Email,
    string FullName,
    string Role,
    string? CompanyName);
