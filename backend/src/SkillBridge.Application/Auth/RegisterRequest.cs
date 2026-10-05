namespace SkillBridge.Application.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? CompanyName = null);
