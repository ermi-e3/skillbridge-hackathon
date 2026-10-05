namespace SkillBridge.Application.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    string FullName,
    string Role,
    string? CompanyName);
