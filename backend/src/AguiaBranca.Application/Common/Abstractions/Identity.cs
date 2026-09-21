using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Application.Common.Abstractions;

public sealed record AccessToken(string Value, DateTime ExpiresAt, int ExpiresInSeconds);

/// <summary>Refresh token opaco: <see cref="Raw"/> vai ao cliente; só <see cref="Hash"/> é persistido.</summary>
public sealed record RefreshTokenValue(string Raw, string Hash);

public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user);
    RefreshTokenValue GenerateRefreshToken();
    string HashRefreshToken(string raw);
    TimeSpan RefreshLifetime { get; }
}

public enum CredentialStatus { Success, Invalid, LockedOut }

public sealed record CredentialCheckResult(CredentialStatus Status, AppUser? User = null, TimeSpan? RetryAfter = null)
{
    public static CredentialCheckResult Ok(AppUser user) => new(CredentialStatus.Success, user);
    public static CredentialCheckResult Invalid() => new(CredentialStatus.Invalid);
    public static CredentialCheckResult Locked(TimeSpan retryAfter) => new(CredentialStatus.LockedOut, RetryAfter: retryAfter);
}

/// <summary>Fronteira com o ASP.NET Identity (hash de senha, lockout), mantendo a Application livre do framework.</summary>
public interface IIdentityService
{
    /// <summary>Valida e-mail/senha aplicando lockout (5 falhas → 15 min). Não distingue "usuário inexistente" de "senha errada".</summary>
    Task<CredentialCheckResult> CheckCredentialsAsync(string email, string password, CancellationToken ct);

    /// <summary>Cria o usuário com a senha (hash PBKDF2, política mínima de 8 caracteres).</summary>
    Task<Result<AppUser>> CreateUserAsync(AppUser user, string password, CancellationToken ct);
}
