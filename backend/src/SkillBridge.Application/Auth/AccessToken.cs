namespace SkillBridge.Application.Auth;

public sealed record AccessToken(string Value, DateTime ExpiresAt);
