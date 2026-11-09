using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Infrastructure.Identity;

public sealed class RefreshTokenService(
    IAppDbContext dbContext,
    IIdentityService identityService,
    IClock clock,
    IOptions<JwtOptions> options) : IRefreshTokenService
{
    public async Task<RefreshTokenResult> CreateRefreshTokenAsync(
        string userId,
        Guid? familyId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateSecureToken();
        var tokenHash = RefreshToken.HashToken(rawToken);
        var effectiveFamilyId = familyId ?? Guid.NewGuid();
        var days = options.Value.RefreshTokenExpirationDays > 0 ? options.Value.RefreshTokenExpirationDays : 7;
        var expiresAt = clock.UtcNow.AddDays(days);

        var entity = RefreshToken.Create(
            userId,
            effectiveFamilyId,
            tokenHash,
            clock.UtcNow,
            expiresAt,
            ipAddress);

        dbContext.RefreshTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResult(rawToken, entity, expiresAt);
    }

    public async Task<(AuthUser User, RefreshTokenResult NewToken)> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new UnauthorizedException(
                "auth.invalid_refresh_token", "Invalid refresh token", "Refresh token was not provided.");
        }

        var incomingHash = RefreshToken.HashToken(rawRefreshToken.Trim());
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == incomingHash, cancellationToken);

        if (token is null)
        {
            throw new UnauthorizedException(
                "auth.invalid_refresh_token", "Invalid refresh token", "Refresh token does not exist.");
        }

        // Token reuse detection: if a revoked/rotated token is used again, revoke the whole family
        if (token.IsRevoked)
        {
            var compromisedFamilyTokens = await dbContext.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var familyToken in compromisedFamilyTokens)
            {
                familyToken.Revoke(clock.UtcNow, "RevokedDueToTokenReuseDetection", ipAddress);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException(
                "auth.refresh_token_reuse",
                "Refresh token reuse detected",
                "This token was already rotated. For security, all active sessions in this token family have been revoked.");
        }

        if (token.IsExpired(clock.UtcNow))
        {
            throw new UnauthorizedException(
                "auth.refresh_token_expired", "Refresh token expired", "Refresh token has expired. Please sign in again.");
        }

        var user = await identityService.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException(
                "auth.user_not_found", "User not found", "User account associated with this token was not found.");
        }

        // Generate replacement token in the same family
        var newRawToken = GenerateSecureToken();
        var newTokenHash = RefreshToken.HashToken(newRawToken);
        var days = options.Value.RefreshTokenExpirationDays > 0 ? options.Value.RefreshTokenExpirationDays : 7;
        var newExpiresAt = clock.UtcNow.AddDays(days);

        var newEntity = RefreshToken.Create(
            token.UserId,
            token.FamilyId,
            newTokenHash,
            clock.UtcNow,
            newExpiresAt,
            ipAddress);

        dbContext.RefreshTokens.Add(newEntity);

        // Immediately revoke the old token with pointer to the new one
        token.Revoke(clock.UtcNow, "Rotated", ipAddress, newEntity.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (user, new RefreshTokenResult(newRawToken, newEntity, newExpiresAt));
    }

    public async Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        string reason = "Logout",
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        var incomingHash = RefreshToken.HashToken(rawRefreshToken.Trim());
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == incomingHash, cancellationToken);

        if (token is not null && !token.IsRevoked)
        {
            token.Revoke(clock.UtcNow, reason, ipAddress);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(
        string userId,
        string reason = "RevokeAll",
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (tokens.Count != 0)
        {
            foreach (var token in tokens)
            {
                token.Revoke(clock.UtcNow, reason, ipAddress);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

