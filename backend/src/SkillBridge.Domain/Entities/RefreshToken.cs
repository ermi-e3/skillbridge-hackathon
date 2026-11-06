using System.Security.Cryptography;
using System.Text;

namespace SkillBridge.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public Guid FamilyId { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? RevokedByIp { get; private set; }

    private RefreshToken() { }

    public RefreshToken(
        string userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        string? createdByIp = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public static RefreshToken Create(
        string userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        string? createdByIp = null)
    {
        return new RefreshToken(userId, familyId, tokenHash, createdAt, expiresAt, createdByIp);
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt != null;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string reason, string? revokedByIp = null, Guid? replacedByTokenId = null)
    {
        RevokedAt = utcNow;
        RevokedReason = reason;
        RevokedByIp = revokedByIp;
        ReplacedByTokenId = replacedByTokenId;
    }

    public static string HashToken(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

