using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.Auth;

public sealed record RefreshTokenResult(
    string RawToken,
    RefreshToken Entity,
    DateTime ExpiresAt);

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> CreateRefreshTokenAsync(
        string userId,
        Guid? familyId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<(AuthUser User, RefreshTokenResult NewToken)> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        string reason = "Logout",
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}

