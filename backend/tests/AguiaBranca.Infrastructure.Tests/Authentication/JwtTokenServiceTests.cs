using System.Text;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Authentication;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AguiaBranca.Infrastructure.Tests.Authentication;

public class JwtTokenServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    private const string Key = "chave-de-teste-com-mais-de-32-bytes-0123456789";

    private sealed class FixedClock(DateTime now) : IClock { public DateTime UtcNow { get; } = now; }

    private static JwtTokenService Service(int accessMinutes = 30, int refreshDays = 7, string key = Key) =>
        new(Options.Create(new JwtOptions { Issuer = "iss", Audience = "aud", Key = key, AccessMinutes = accessMinutes, RefreshDays = refreshDays }),
            new FixedClock(Now));

    private static AppUser User() =>
        AppUser.Create("Ana Gestora", "ana@aguiabranca.com", Role.GESTOR, Division.LOGISTICA, Now);

    private static TokenValidationParameters Validation(string key = Key, string audience = "aud") => new()
    {
        ValidateIssuer = true, ValidIssuer = "iss",
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (nbf, exp, _, _) => nbf <= Now && Now < exp, // relógio fixo do teste
        NameClaimType = "name", RoleClaimType = "role"
    };

    [Fact]
    public async Task AccessToken_HasTheDocumentedClaims_AndValidSignature()
    {
        var user = User();

        var token = Service().CreateAccessToken(user);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, Validation());

        result.IsValid.Should().BeTrue(result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        jwt.Claims.Single(c => c.Type == "sub").Value.Should().Be(user.Id);
        jwt.Claims.Single(c => c.Type == "email").Value.Should().Be("ana@aguiabranca.com");
        jwt.Claims.Single(c => c.Type == "name").Value.Should().Be("Ana Gestora");
        jwt.Claims.Single(c => c.Type == "role").Value.Should().Be("GESTOR");
        jwt.Claims.Single(c => c.Type == "division").Value.Should().Be("LOGISTICA");
        jwt.Claims.Single(c => c.Type == "jti").Value.Should().NotBeNullOrEmpty();
        jwt.Issuer.Should().Be("iss");
        jwt.Audiences.Should().Equal("aud");
        jwt.Alg.Should().Be("HS256");
    }

    [Fact]
    public void AccessToken_Expires30MinutesAfterIssue()
    {
        var token = Service().CreateAccessToken(User());

        token.ExpiresAt.Should().Be(Now.AddMinutes(30));
        token.ExpiresInSeconds.Should().Be(1800);
        var jwt = new JsonWebToken(token.Value);
        jwt.ValidTo.Should().Be(Now.AddMinutes(30));
        jwt.ValidFrom.Should().Be(Now);
    }

    [Fact]
    public void AccessToken_LifetimeFollowsConfiguration() =>
        Service(accessMinutes: 5).CreateAccessToken(User()).ExpiresInSeconds.Should().Be(300);

    [Fact]
    public async Task AccessToken_IsRejected_WithWrongKey_Audience_OrAfterExpiry()
    {
        var handler = new JsonWebTokenHandler();
        var token = Service().CreateAccessToken(User()).Value;

        (await handler.ValidateTokenAsync(token, Validation(key: "outra-chave-completamente-diferente-0123456789"))).IsValid.Should().BeFalse();
        (await handler.ValidateTokenAsync(token, Validation(audience: "outra-audiencia"))).IsValid.Should().BeFalse();

        var expired = Validation();
        expired.LifetimeValidator = (nbf, exp, _, _) => Now.AddMinutes(31) < exp;
        (await handler.ValidateTokenAsync(token, expired)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AccessToken_TamperedPayload_IsRejected()
    {
        var parts = Service().CreateAccessToken(User()).Value.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("GESTOR", "LIDER_");
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        (await new JsonWebTokenHandler().ValidateTokenAsync(tampered, Validation())).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AccessTokens_HaveUniqueJti()
    {
        var service = Service();
        var jtis = Enumerable.Range(0, 20)
            .Select(_ => new JsonWebToken(service.CreateAccessToken(User()).Value).Id).ToList();
        jtis.Distinct().Should().HaveCount(20);
    }

    [Fact]
    public void RefreshToken_Has256BitsOfEntropy_IsUrlSafe_AndUnique()
    {
        var service = Service();

        var tokens = Enumerable.Range(0, 50).Select(_ => service.GenerateRefreshToken()).ToList();

        tokens.Select(t => t.Raw).Distinct().Should().HaveCount(50);
        tokens.Should().OnlyContain(t => t.Raw.Length == 43 && System.Text.RegularExpressions.Regex.IsMatch(t.Raw, "^[A-Za-z0-9_-]+$"));
    }

    [Fact]
    public void RefreshToken_HashIsDeterministicSha256Hex_AndNeverEqualsTheRawValue()
    {
        var service = Service();
        var token = service.GenerateRefreshToken();

        token.Hash.Should().Be(service.HashRefreshToken(token.Raw));
        token.Hash.Should().MatchRegex("^[0-9a-f]{64}$").And.NotBe(token.Raw);
        service.HashRefreshToken("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    public void RefreshLifetime_FollowsConfiguration(int days) =>
        Service(refreshDays: days).RefreshLifetime.Should().Be(TimeSpan.FromDays(days));
}
