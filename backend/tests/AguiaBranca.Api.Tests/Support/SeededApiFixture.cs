using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Tests.Support;

namespace AguiaBranca.Api.Tests.Support;

/// <summary>
/// API real + MongoDB com os dados de demonstração (seed) e um token por perfil, criados uma única vez por classe de teste.
/// <see cref="ClientAs"/> devolve clientes já autenticados; <see cref="Anonymous"/> não envia credenciais.
/// </summary>
public sealed class SeededApiFixture : IAsyncLifetime
{
    public const string Password = "aguiabranca123";

    private readonly Dictionary<Role, string> _tokens = [];
    private TestDatabase _db = null!;

    public ApiFactory Factory { get; private set; } = null!;
    public HttpClient Anonymous { get; private set; } = null!;

    public static string EmailOf(Role role) => role switch
    {
        Role.LIDER => "lider@aguiabranca.com",
        Role.GESTOR => "gestor@aguiabranca.com",
        _ => "operador@aguiabranca.com"
    };

    public async Task InitializeAsync()
    {
        var mongo = new MongoFixture();
        await mongo.InitializeAsync();
        _db = await mongo.CreateDatabaseAsync();

        Factory = new ApiFactory().WithMongo(mongo.ConnectionString, _db.Name)
            .With("Jwt:Key", ApiIntegrationTest.JwtKey)
            .With("Seed:Enabled", "true")
            .With("RateLimiting:AuthPermitLimit", "1000");
        Anonymous = Factory.CreateClient(); // sobe o host: índices + seed

        foreach (var role in Enum.GetValues<Role>())
        {
            var response = await Anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = EmailOf(role), password = Password });
            response.EnsureSuccessStatusCode();
            _tokens[role] = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
        }
    }

    public async Task DisposeAsync()
    {
        Anonymous.Dispose();
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    public string TokenOf(Role role) => _tokens[role];

    public HttpClient ClientAs(Role role)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokens[role]);
        return client;
    }

    /// <summary>Cliente para o papel informado, ou anônimo quando <c>null</c>.</summary>
    public HttpClient ClientAs(Role? role) => role is { } r ? ClientAs(r) : Factory.CreateClient();
}
