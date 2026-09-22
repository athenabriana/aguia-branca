using System.Security.Cryptography;
using System.Text;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AguiaBranca.Infrastructure.Authentication;

/// <summary>Emite o access token (JWT HS256) e os refresh tokens opacos (256 bits; só o SHA-256 é persistido).</summary>
internal sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signing;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        _signing = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256);
    }

    public TimeSpan RefreshLifetime => TimeSpan.FromDays(_options.RefreshDays);

    public AccessToken CreateAccessToken(AppUser user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _signing,
            // Claims explícitas (design §7.2): sub, email, name, role, division, jti.
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id,
                [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
                [JwtRegisteredClaimNames.Name] = user.Name,
                ["role"] = user.Role.ToString(),
                ["division"] = user.Division.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            }
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires, (int)(expires - now).TotalSeconds);
    }

    public RefreshTokenValue GenerateRefreshToken()
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenValue(raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
