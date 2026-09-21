using System.Net;
using System.Security.Cryptography;
using System.Text;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Api.Tests.Auth;

[Collection(MongoCollection.Name)]
public sealed class AuthApiTests(MongoFixture mongo) : ApiIntegrationTest(mongo)
{
    private const string Email = "lider@aguiabranca.com";

    // ---------- login ----------

    [Fact]
    public async Task Login_Success_ReturnsTokensAndProfile_WithDocumentedClaims()
    {
        var user = await CreateUserAsync(Email, Role.LIDER, Division.CORPORATIVO, "Líder INOVAGAB");

        var login = await LoginAsync(Email);

        login.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Body.GetProperty("expiresIn").GetInt32().Should().Be(1800);
        login.RefreshToken.Should().NotBeNullOrEmpty();
        var profile = login.Body.GetProperty("user");
        profile.GetProperty("id").GetString().Should().Be(user.Id);
        profile.GetProperty("role").GetString().Should().Be("LIDER");
        profile.GetProperty("division").GetString().Should().Be("CORPORATIVO");
        profile.GetProperty("points").GetInt32().Should().Be(0);
        profile.GetProperty("badges").GetArrayLength().Should().Be(0);

        var jwt = new JsonWebToken(login.AccessToken);
        jwt.Claims.Single(c => c.Type == "sub").Value.Should().Be(user.Id);
        jwt.Claims.Single(c => c.Type == "role").Value.Should().Be("LIDER");
        jwt.Claims.Single(c => c.Type == "division").Value.Should().Be("CORPORATIVO");
        jwt.Claims.Single(c => c.Type == "name").Value.Should().Be("Líder INOVAGAB");
        jwt.Claims.Single(c => c.Type == "email").Value.Should().Be(Email);
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_NeverLeaksCredentialFields()
    {
        await CreateUserAsync(Email);

        var login = await LoginAsync(Email);
        var me = await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", login.AccessToken));

        foreach (var text in new[] { login.Body.GetRawText(), await me.Content.ReadAsStringAsync() })
            text.Should().NotContainAny("passwordHash", "securityStamp", "PasswordHash", "normalizedEmail", DefaultPassword);
    }

    [Fact]
    public async Task Login_UnknownEmail_AndWrongPassword_ReturnTheSameResponse()
    {
        await CreateUserAsync(Email);

        var wrong = await LoginAsync(Email, "senha-errada-123");
        var unknown = await LoginAsync("ninguem@aguiabranca.com", "senha-errada-123");

        wrong.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrong.Body.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
        foreach (var field in new[] { "code", "title", "detail", "status", "type" })
            wrong.Body.GetProperty(field).ToString().Should().Be(unknown.Body.GetProperty(field).ToString(), field);
        wrong.Body.GetProperty("errors").ToString().Should().Be(unknown.Body.GetProperty("errors").ToString());
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount_With429AndRetryAfter()
    {
        await CreateUserAsync(Email);

        for (var attempt = 1; attempt <= 4; attempt++)
            (await LoginAsync(Email, "errada-123")).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"tentativa {attempt}");

        var fifth = await LoginAsync(Email, "errada-123");

        fifth.Response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        fifth.Body.GetProperty("code").GetString().Should().Be("ACCOUNT_LOCKED");
        int.Parse(fifth.Response.Headers.GetValues("Retry-After").Single()).Should().BeInRange(890, 900);

        // Mesmo com a senha certa, continua bloqueada.
        var correct = await LoginAsync(Email);
        correct.Response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        correct.Response.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Theory]
    [InlineData("""{}""", "email")]
    [InlineData("""{"email":"","password":"x"}""", "email")]
    [InlineData("""{"email":"nao-e-email","password":"x"}""", "email")]
    [InlineData("""{"email":"a@b.com"}""", "password")]
    public async Task Login_InvalidBody_Returns400_WithFieldInPortuguese(string json, string field)
    {
        var response = await Client.PostAsync("/api/v1/auth/login", new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await JsonOf(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        body.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task Login_MalformedJson_Returns400() =>
        (await Client.PostAsync("/api/v1/auth/login", new StringContent("{ isso nao e json", Encoding.UTF8, "application/json")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Login_ResetsFailureCounter_OnSuccess()
    {
        await CreateUserAsync(Email);
        for (var i = 0; i < 3; i++) await LoginAsync(Email, "errada-123");

        (await LoginAsync(Email)).Response.StatusCode.Should().Be(HttpStatusCode.OK);

        for (var i = 0; i < 4; i++) (await LoginAsync(Email, "errada-123")).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- refresh ----------

    [Fact]
    public async Task Refresh_RotatesTokens_AndTheOldOneStopsWorking()
    {
        await CreateUserAsync(Email);
        var login = await LoginAsync(Email);

        var refreshed = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken }));
        var body = await JsonOf(refreshed);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("refreshToken").GetString().Should().NotBe(login.RefreshToken);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("email").GetString().Should().Be(Email);

        var replay = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken }));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await JsonOf(replay)).GetProperty("code").GetString().Should().Be("TOKEN_INVALID");
    }

    [Fact]
    public async Task Refresh_ReuseOfARotatedToken_RevokesTheWholeFamily()
    {
        await CreateUserAsync(Email);
        var login = await LoginAsync(Email);
        var first = await JsonOf(await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken })));
        var legitimateSuccessor = first.GetProperty("refreshToken").GetString();

        // Atacante reapresenta o token antigo → a família inteira é revogada
        await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken }));

        var afterTheft = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = legitimateSuccessor }));
        afterTheft.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "o sucessor legítimo também foi revogado");
    }

    [Fact]
    public async Task RefreshToken_IsStoredOnlyAsSha256Hash()
    {
        await CreateUserAsync(Email);
        var login = await LoginAsync(Email);

        var docs = await Db.Raw(Collections.RefreshTokens).Find(new BsonDocument()).ToListAsync();

        docs.Should().ContainSingle();
        docs[0]["tokenHash"].AsString.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.RefreshToken))).ToLowerInvariant());
        docs[0].ToJson().Should().NotContain(login.RefreshToken, "o token em texto puro nunca é persistido");
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        await CreateUserAsync(Email);
        var login = await LoginAsync(Email);
        await Db.Raw(Collections.RefreshTokens).UpdateManyAsync(
            new BsonDocument(), Builders<BsonDocument>.Update.Set("expiresAt", DateTime.UtcNow.AddMinutes(-1)));

        var response = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("lixo-que-nao-existe", 401)]
    [InlineData("", 400)]
    public async Task Refresh_GarbageOrEmpty(string token, int status) =>
        ((int)(await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = token }))).StatusCode)
            .Should().Be(status);

    [Fact]
    public async Task Refresh_ReflectsCurrentPointsInTheProfile()
    {
        var user = await CreateUserAsync(Email);
        var login = await LoginAsync(Email);
        await Db.Raw(Collections.Users).UpdateOneAsync(
            new BsonDocument("_id", new ObjectId(user.Id)), Builders<BsonDocument>.Update.Set("points", 45));

        var refreshed = await JsonOf(await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken })));

        refreshed.GetProperty("user").GetProperty("points").GetInt32().Should().Be(45);
    }

    // ---------- logout ----------

    [Fact]
    public async Task Logout_RevokesTheSession_ThenRefreshFails()
    {
        await CreateUserAsync(Email);
        var login = await LoginAsync(Email);

        var logout = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/logout", login.AccessToken, new { refreshToken = login.RefreshToken }));
        var refresh = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login.RefreshToken }));

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RequiresAuthentication() =>
        (await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/logout", body: new { refreshToken = "x" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Logout_WithAnotherUsersToken_DoesNothing()
    {
        await CreateUserAsync(Email);
        await CreateUserAsync("gestor@aguiabranca.com", Role.GESTOR);
        var victim = await LoginAsync(Email);
        var attacker = await LoginAsync("gestor@aguiabranca.com");

        var logout = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/logout", attacker.AccessToken, new { refreshToken = victim.RefreshToken }));
        var victimRefresh = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = victim.RefreshToken }));

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent, "resposta neutra");
        victimRefresh.StatusCode.Should().Be(HttpStatusCode.OK, "a sessão da vítima segue válida");
    }

    // ---------- me / JWT ----------

    [Fact]
    public async Task Me_ReturnsFreshProfile_WithPointsAndBadges()
    {
        var user = await CreateUserAsync(Email, Role.OPERADOR);
        var login = await LoginAsync(Email);
        await Db.Raw(Collections.Users).UpdateOneAsync(new BsonDocument("_id", new ObjectId(user.Id)),
            Builders<BsonDocument>.Update.Set("points", 30).Set("badges", new BsonArray { "Primeira Ideia" }));

        var me = await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", login.AccessToken));
        var body = await JsonOf(me);

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("points").GetInt32().Should().Be(30);
        body.GetProperty("badges").EnumerateArray().Select(b => b.GetString()).Should().Equal("Primeira Ideia");
        body.GetProperty("role").GetString().Should().Be("OPERADOR");
    }

    [Fact]
    public async Task Me_ForATokenOfAUserThatNoLongerExists_Returns401()
    {
        var ghost = ForgeToken("665f00000000000000000abc");
        (await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", ghost))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CurrentUser_IsPopulatedFromTheJwtClaims()
    {
        var user = await CreateUserAsync(Email, Role.GESTOR, Division.COMERCIO, "Ana Gestora");
        var login = await LoginAsync(Email);

        var body = await JsonOf(await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/policyprobe/whoami", login.AccessToken)));

        body.GetProperty("id").GetString().Should().Be(user.Id);
        body.GetProperty("name").GetString().Should().Be("Ana Gestora");
        body.GetProperty("role").GetString().Should().Be("GESTOR");
        body.GetProperty("division").GetString().Should().Be("COMERCIO");
        body.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
    }

    public static IEnumerable<object[]> BadTokens()
    {
        var id = "665f00000000000000000001";
        yield return ["sem token", null!];
        yield return ["lixo", "isto-nao-e-um-jwt"];
        yield return ["assinatura errada", ForgeToken(id, key: "outra-chave-completamente-diferente-0123456789")];
        yield return ["expirado", ForgeToken(id, notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1))];
        yield return ["audiência errada", ForgeToken(id, audience: "outra-audiencia")];
        yield return ["emissor errado", ForgeToken(id, issuer: "outro-emissor")];
        yield return ["ainda não válido", ForgeToken(id, notBefore: DateTime.UtcNow.AddHours(1), expires: DateTime.UtcNow.AddHours(2))];
    }

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task InvalidJwt_IsRejectedWith401_InApiFormat(string _, string? token)
    {
        var response = await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", token));
        var body = await JsonOf(response);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.GetProperty("code").GetString().Should().Be("TOKEN_INVALID");
    }

    [Fact]
    public async Task TamperedJwtPayload_IsRejected()
    {
        await CreateUserAsync(Email, Role.OPERADOR);
        var login = await LoginAsync(Email);
        var parts = login.AccessToken.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("OPERADOR", "LIDER___");
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        (await Client.SendAsync(Request(HttpMethod.Get, "/api/v1/policyprobe/whoami", tampered))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file static class Base64UrlEncoder
{
    public static byte[] DecodeBytes(string value) => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(value);
    public static string Encode(string value) => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(value);
}
