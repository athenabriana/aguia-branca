using AguiaBranca.Domain.Common;

namespace AguiaBranca.Domain.Entities;

/// <summary>Refresh token opaco: só o hash (SHA-256) é persistido. Rotativo, agrupado por família.</summary>
public sealed class RefreshToken
{
    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string FamilyId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(string userId, string tokenHash, DateTime now, TimeSpan lifetime, string? familyId = null) =>
        new()
        {
            Id = EntityId.New(),
            UserId = userId,
            FamilyId = familyId ?? EntityId.New(),
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };

    public bool IsExpired(DateTime now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    /// <summary>Já foi trocado por um novo token (rotação). Reapresentá-lo indica possível roubo.</summary>
    public bool WasRotated => ReplacedByHash is not null;
    public bool IsActive(DateTime now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTime now) => RevokedAt ??= now;

    /// <summary>Marca este token como usado e aponta para o sucessor.</summary>
    public void Rotate(string newTokenHash, DateTime now)
    {
        Revoke(now);
        ReplacedByHash = newTokenHash;
    }
}
