using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AguiaBranca.Api.Tests.Support;

public sealed record LoginResult(HttpResponseMessage Response, JsonElement Body)
{
    public string AccessToken => Body.GetProperty("accessToken").GetString()!;
    public string RefreshToken => Body.GetProperty("refreshToken").GetString()!;
}

/// <summary>Base dos testes que exercitam a API contra um MongoDB real (banco próprio por classe de teste).</summary>
[Trait("Category", "Integration")]
public abstract class ApiIntegrationTest(MongoFixture mongo) : IAsyncLifetime
{
    public const string DefaultPassword = "aguiabranca123";
    public const string JwtKey = "test-key-test-key-test-key-test-key-0123456789";

    protected TestDatabase Db { get; private set; } = null!;
    protected ApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    /// <summary>Ajustes de configuração antes de o host subir.</summary>
    protected virtual void Configure(ApiFactory factory) { }

    public virtual async Task InitializeAsync()
    {
        Db = await mongo.CreateDatabaseAsync();
        Factory = new ApiFactory().WithMongo(mongo.ConnectionString, Db.Name)
            .With("Jwt:Key", JwtKey)
            .With("RateLimiting:AuthPermitLimit", "1000"); // não interfere nos demais testes
        Configure(Factory);
        Client = Factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await Db.DisposeAsync();
    }

    protected async Task<AppUser> CreateUserAsync(
        string email, Role role = Role.OPERADOR, Division division = Division.LOGISTICA,
        string name = "Usuário Teste", string password = DefaultPassword)
    {
        using var scope = Factory.Services.CreateScope();
        var user = AppUser.Create(name, email, role, division, DateTime.UtcNow);
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityService>().CreateUserAsync(user, password, default);
        result.IsSuccess.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
        return user;
    }

    protected async Task<LoginResult> LoginAsync(string email, string password = DefaultPassword, HttpClient? client = null)
    {
        var response = await (client ?? Client).PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var text = await response.Content.ReadAsStringAsync();
        return new LoginResult(response, JsonDocument.Parse(text).RootElement);
    }

    protected static HttpRequestMessage Request(HttpMethod method, string url, string? bearer = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    protected static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Token forjado com a chave de teste (para simular expirado, audiência errada etc.).</summary>
    protected static string ForgeToken(
        string userId, string role = "OPERADOR", string? key = null, string issuer = "aguiabranca-api",
        string audience = "aguiabranca-app", DateTime? expires = null, DateTime? notBefore = null) =>
        TestJwt.Forge(userId, role, key, issuer, audience, expires, notBefore);
}

public static class TestJwt
{
    public static string Forge(
        string userId, string role = "OPERADOR", string? key = null, string issuer = "aguiabranca-api",
        string audience = "aguiabranca-app", DateTime? expires = null, DateTime? notBefore = null)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = notBefore ?? now.AddMinutes(-1),
            NotBefore = notBefore ?? now.AddMinutes(-1),
            Expires = expires ?? now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? ApiIntegrationTest.JwtKey)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId, ["name"] = "Forjado", ["email"] = "f@x.com", ["role"] = role, ["division"] = "LOGISTICA"
            }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
