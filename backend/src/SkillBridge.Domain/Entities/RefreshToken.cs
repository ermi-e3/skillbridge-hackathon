namespace SkillBridge.Domain.Entities;

/// <summary>
/// One refresh token. Only a SHA-256 hash is stored, never the token itself.
/// Every token belongs to a family (one sign-in session). Rotation creates the next token
/// in the same family; reusing an old token revokes the whole family.
/// </summary>
public class RefreshToken
{
    private RefreshToken() { } // EF

    public static RefreshToken Create(string userId, string tokenHash, Guid familyId, DateTime nowUtc, DateTime expiresAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAt = nowUtc,
            ExpiresAt = expiresAtUtc
        };

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public Guid FamilyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    /// <summary>Set when this token was rotated: the id of the token that replaced it.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin): two refreshes with the same token can't both win.</summary>
    public uint Version { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    public void Revoke(DateTime nowUtc, Guid? replacedByTokenId = null)
    {
        if (IsRevoked) return;
        RevokedAt = nowUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}
