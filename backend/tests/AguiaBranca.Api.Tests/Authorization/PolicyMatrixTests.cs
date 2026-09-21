using System.Net;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Tests.Authorization;

/// <summary>Cada policy × cada identidade (anônimo, OPERADOR, GESTOR, LIDER) → 401 / 403 / 200 (design §20.3).</summary>
[Trait("Category", "Integration")]
public sealed class PolicyMatrixTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    // rota → perfis com acesso
    private static readonly Dictionary<string, Role[]> Allowed = new()
    {
        ["gestor"] = [Role.GESTOR],
        ["lider"] = [Role.LIDER],
        ["create-idea"] = [Role.OPERADOR, Role.GESTOR],
        ["projects-read"] = [Role.GESTOR, Role.LIDER],
        ["users-read"] = [Role.GESTOR, Role.LIDER],
        ["plain"] = [Role.OPERADOR, Role.GESTOR, Role.LIDER] // sem atributo → policy padrão: qualquer autenticado
    };

    public static IEnumerable<object?[]> Cases()
    {
        foreach (var (route, allowed) in Allowed)
        {
            yield return [route, null, HttpStatusCode.Unauthorized];
            foreach (var role in Enum.GetValues<Role>())
                yield return [route, role, allowed.Contains(role) ? HttpStatusCode.OK : HttpStatusCode.Forbidden];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Policy_ReturnsExpectedStatus(string route, Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);

        var response = await client.GetAsync($"/api/v1/policyprobe/{route}");

        response.StatusCode.Should().Be(expected, $"{route} como {role?.ToString() ?? "anônimo"}");
    }

    [Theory]
    [InlineData(null, 401, "TOKEN_INVALID")]
    [InlineData(Role.OPERADOR, 403, "FORBIDDEN")]
    public async Task Rejections_UseTheApiErrorFormat(Role? role, int status, string code)
    {
        using var client = api.ClientAs(role);

        var response = await client.GetAsync("/api/v1/policyprobe/lider");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        ((int)response.StatusCode).Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Endpoint_WithoutAnyAttribute_RequiresAuthentication_ByTheFallbackPolicy()
    {
        (await api.Anonymous.GetAsync("/api/v1/policyprobe/plain")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.Anonymous.GetAsync("/api/v1/policyprobe/open")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task InfrastructureEndpoints_StayPublic(string path) =>
        (await api.Anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task UnknownRoutes_DoNotRevealThemselves_ToAnonymousCallers()
    {
        // A fallback policy também cobre URLs sem endpoint: anônimo recebe 401 (não descobre quais rotas existem)...
        (await api.Anonymous.GetAsync("/api/v1/rota-que-nao-existe")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...e quem está autenticado recebe o 404 padronizado.
        using var client = api.ClientAs(Role.OPERADOR);
        var response = await client.GetAsync("/api/v1/rota-que-nao-existe");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString().Should().Be("RESOURCE_NOT_FOUND");
    }

    [Theory]
    [InlineData(Role.LIDER, "Líder INOVAGAB", "CORPORATIVO", 0)]
    [InlineData(Role.GESTOR, "Gestor INOVAGAB", "LOGISTICA", 0)]
    [InlineData(Role.OPERADOR, "Operador INOVAGAB", "LOGISTICA", 295)]
    public async Task SeededUsers_LoginAndSeeTheirProfile(Role role, string name, string division, int points)
    {
        using var client = api.ClientAs(role);

        var me = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me")).RootElement;

        me.GetProperty("name").GetString().Should().Be(name);
        me.GetProperty("role").GetString().Should().Be(role.ToString());
        me.GetProperty("division").GetString().Should().Be(division);
        me.GetProperty("points").GetInt32().Should().Be(points);
    }

    [Fact]
    public async Task SeededOperator_HasTheBadgesEarnedByTheSeedData()
    {
        using var client = api.ClientAs(Role.OPERADOR);
        var me = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me")).RootElement;

        me.GetProperty("badges").EnumerateArray().Select(b => b.GetString())
            .Should().BeEquivalentTo(["Primeira Ideia", "Estrategista", "Impacto Real"]);
    }

    [Fact]
    public async Task TokenOfOneRole_CannotBeUsedToActAsAnother()
    {
        // O papel vem do token assinado; alterar o papel exige nova assinatura (coberto em AuthApiTests.TamperedJwtPayload).
        using var operador = api.ClientAs(Role.OPERADOR);
        (await operador.GetAsync("/api/v1/policyprobe/lider")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await operador.GetAsync("/api/v1/policyprobe/gestor")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
