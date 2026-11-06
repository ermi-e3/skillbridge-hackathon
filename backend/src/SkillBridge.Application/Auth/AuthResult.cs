namespace SkillBridge.Application.Auth;

public sealed record AuthResult(
    AuthResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

